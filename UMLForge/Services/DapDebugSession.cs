using System.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace UMLForge.Services;

/// <summary>Minimal Debug Adapter Protocol client for adapters such as netcoredbg. The adapter is installed separately.</summary>
public sealed class DapDebugSession : IAsyncDisposable
{
    private Process? _process;
    private Stream? _input;
    private Stream? _output;
    private Task? _readerTask;
    private int _sequence;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonDocument>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    public event Action<string>? MessageReceived;
    public event Action<JsonElement>? EventReceived;
    public int? LastStoppedThreadId { get; private set; }
    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(string adapterPath, string targetDll, string workingDirectory, string sourceFile, IReadOnlyList<DebugBreakpoint> breakpoints, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(adapterPath)) throw new FileNotFoundException("DÃ©bogueur DAP introuvable. Installez netcoredbg et sÃ©lectionnez son exÃ©cutable.", adapterPath);
        if (!File.Exists(targetDll)) throw new FileNotFoundException("DLL cible introuvable. Compilez d'abord le projet en configuration Debug.", targetDll);
        var start = new ProcessStartInfo(adapterPath, "--interpreter=vscode")
        { WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        _process = new Process { StartInfo = start, EnableRaisingEvents = true };
        _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) MessageReceived?.Invoke("[adapter] " + e.Data); };
        _process.Exited += (_, _) => MessageReceived?.Invoke("[adapter] Processus terminÃ©.");
        if (!_process.Start()) throw new InvalidOperationException("Impossible de dÃ©marrer le dÃ©bogueur DAP.");
        _process.BeginErrorReadLine(); _input = _process.StandardInput.BaseStream; _output = _process.StandardOutput.BaseStream;
        _readerTask = Task.Run(ReadLoopAsync);
        using (var init = await RequestAsync("initialize", new { clientID = "UMLForge", clientName = "UMLForge Next", adapterID = "coreclr", pathFormat = "path", linesStartAt1 = true, columnsStartAt1 = true, supportsRunInTerminalRequest = false }, cancellationToken)) { }
        using (var launch = await RequestAsync("launch", new { name = "UMLForge Debug", type = "coreclr", request = "launch", program = Path.GetFullPath(targetDll), cwd = Path.GetFullPath(workingDirectory), args = Array.Empty<string>(), stopAtEntry = true, console = "internalConsole", justMyCode = true }, cancellationToken)) { }
        if (breakpoints.Count > 0)
        {
            var normalized = breakpoints.Where(b => b.Line > 0).GroupBy(b => b.Line).Select(g => g.Last()).ToArray();
            using var bp = await RequestAsync("setBreakpoints", new { source = new { name = Path.GetFileName(sourceFile), path = Path.GetFullPath(sourceFile) }, breakpoints = normalized.Select(b => new { line = b.Line, condition = string.IsNullOrWhiteSpace(b.Condition) ? null : b.Condition, hitCondition = string.IsNullOrWhiteSpace(b.HitCondition) ? null : b.HitCondition, logMessage = string.IsNullOrWhiteSpace(b.LogMessage) ? null : b.LogMessage }).ToArray(), sourceModified = false }, cancellationToken);
            MessageReceived?.Invoke("Points d'arrÃªt envoyÃ©s : " + string.Join(", ", normalized.Select(b => b.Condition is null ? b.Line.ToString() : $"{b.Line} si {b.Condition}")));
        }
        using var config = await RequestAsync("configurationDone", new { }, cancellationToken);
        MessageReceived?.Invoke("Session DAP dÃ©marrÃ©e. Si l'adaptateur accepte la configuration, l'application s'arrÃªtera Ã  l'entrÃ©e ou au prochain point d'arrÃªt.");
    }

    public async Task ContinueAsync(CancellationToken ct = default) { if (LastStoppedThreadId is int id) using (await RequestAsync("continue", new { threadId = id }, ct)) { } }
    public async Task PauseAsync(CancellationToken ct = default) { if (LastStoppedThreadId is int id) using (await RequestAsync("pause", new { threadId = id }, ct)) { } }
    public async Task NextAsync(CancellationToken ct = default) { if (LastStoppedThreadId is int id) using (await RequestAsync("next", new { threadId = id }, ct)) { } }
    public async Task StepInAsync(CancellationToken ct = default) { if (LastStoppedThreadId is int id) using (await RequestAsync("stepIn", new { threadId = id }, ct)) { } }
    public async Task StepOutAsync(CancellationToken ct = default) { if (LastStoppedThreadId is int id) using (await RequestAsync("stepOut", new { threadId = id }, ct)) { } }

    public async Task<string> GetStackTraceAsync(CancellationToken ct = default)
    {
        if (LastStoppedThreadId is not int id) return "Le processus n'est pas en pause sur un thread connu.";
        using var doc = await RequestAsync("stackTrace", new { threadId = id, startFrame = 0, levels = 30 }, ct);
        var frames = doc.RootElement.TryGetProperty("body", out var body) && body.TryGetProperty("stackFrames", out var list) ? list : default;
        if (frames.ValueKind != JsonValueKind.Array) return "Aucune pile d'appels retournÃ©e.";
        return string.Join(Environment.NewLine, frames.EnumerateArray().Select(f =>
        {
            var frameId = f.GetProperty("id").GetInt32();
            var name = f.GetProperty("name").GetString();
            var sourcePath = f.TryGetProperty("source", out var src) && src.TryGetProperty("path", out var source)
                ? source.GetString()
                : "?";
            var lineNumber = f.TryGetProperty("line", out var line) ? line.GetInt32() : 0;
            return $"#{frameId} {name} — {sourcePath}:{lineNumber}";
        }));
    }

    public async Task<string> GetThreadsAsync(CancellationToken ct = default)
    {
        using var doc = await RequestAsync("threads", new { }, ct);
        if (!doc.RootElement.TryGetProperty("body", out var body) || !body.TryGetProperty("threads", out var threads) || threads.ValueKind != JsonValueKind.Array) return "Aucun thread retournÃ© par l'adaptateur.";
        return string.Join(Environment.NewLine, threads.EnumerateArray().Select(t => $"Thread {t.GetProperty("id").GetInt32()} â€” {t.GetProperty("name").GetString()}"));
    }

    public async Task<string> EvaluateAsync(string expression, CancellationToken ct = default)
    {
        if (LastStoppedThreadId is not int id) return "Mettez l'exÃ©cution en pause avant d'Ã©valuer une expression.";
        using var stack = await RequestAsync("stackTrace", new { threadId = id, startFrame = 0, levels = 1 }, ct);
        if (!stack.RootElement.TryGetProperty("body", out var body) || !body.TryGetProperty("stackFrames", out var frames) || frames.GetArrayLength() == 0) return "Aucune frame active pour l'Ã©valuation.";
        var frameId = frames[0].GetProperty("id").GetInt32();
        using var result = await RequestAsync("evaluate", new { expression, frameId, context = "watch" }, ct);
        if (!result.RootElement.TryGetProperty("body", out var eval)) return "L'adaptateur n'a retournÃ© aucun rÃ©sultat.";
        var value = eval.TryGetProperty("result", out var r) ? r.GetString() : "<sans valeur>";
        var type = eval.TryGetProperty("type", out var t) ? t.GetString() : null;
        return $"{expression} = {value}" + (string.IsNullOrWhiteSpace(type) ? "" : $" ({type})");
    }

    public async Task<string> GetVariablesAsync(CancellationToken ct = default)
    {
        if (LastStoppedThreadId is not int id) return "Le processus n'est pas en pause.";
        using var stack = await RequestAsync("stackTrace", new { threadId = id, startFrame = 0, levels = 1 }, ct);
        if (!stack.RootElement.TryGetProperty("body", out var body) || !body.TryGetProperty("stackFrames", out var frames) || frames.GetArrayLength() == 0) return "Aucune frame disponible.";
        var frameId = frames[0].GetProperty("id").GetInt32();
        using var scopes = await RequestAsync("scopes", new { frameId }, ct);
        if (!scopes.RootElement.TryGetProperty("body", out var scopeBody) || !scopeBody.TryGetProperty("scopes", out var scopeList)) return "Aucune portÃ©e de variables.";
        var lines = new List<string>();
        foreach (var scope in scopeList.EnumerateArray())
        {
            var refId = scope.GetProperty("variablesReference").GetInt32();
            lines.Add("[" + scope.GetProperty("name").GetString() + "]");
            if (refId == 0) continue;
            using var vars = await RequestAsync("variables", new { variablesReference = refId }, ct);
            if (vars.RootElement.TryGetProperty("body", out var vb) && vb.TryGetProperty("variables", out var vl))
                lines.AddRange(vl.EnumerateArray().Select(v => "  " + v.GetProperty("name").GetString() + " = " + (v.TryGetProperty("value", out var val) ? val.GetString() : "?")));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private async Task<JsonDocument> RequestAsync(string command, object arguments, CancellationToken ct)
    {
        var input = _input ?? throw new InvalidOperationException("Session DAP non dÃ©marrÃ©e.");
        var seq = Interlocked.Increment(ref _sequence);
        var completion = new TaskCompletionSource<JsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[seq] = completion;
        var json = JsonSerializer.SerializeToUtf8Bytes(new { seq, type = "request", command, arguments });
        var header = Encoding.ASCII.GetBytes($"Content-Length: {json.Length}\r\n\r\n");
        await _writeLock.WaitAsync(ct);
        try { await input.WriteAsync(header, ct); await input.WriteAsync(json, ct); await input.FlushAsync(ct); }
        finally { _writeLock.Release(); }
        using var registration = ct.Register(() => completion.TrySetCanceled(ct));
        var response = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        if (response.RootElement.TryGetProperty("success", out var success) && !success.GetBoolean())
        {
            var message = response.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() : "RequÃªte refusÃ©e.";
            response.Dispose(); throw new InvalidOperationException($"DAP {command} : {message}");
        }
        return response;
    }

    private async Task ReadLoopAsync()
    {
        var stream = _output;
        if (stream is null) return;
        try
        {
            while (true)
            {
                var header = await ReadHeaderAsync(stream);
                if (header is null) break;
                if (!header.TryGetValue("Content-Length", out var lengthText) || !int.TryParse(lengthText, out var length) || length < 0 || length > 20_000_000) throw new InvalidDataException("En-tÃªte DAP invalide.");
                var payload = new byte[length]; var offset = 0;
                while (offset < length) { var read = await stream.ReadAsync(payload.AsMemory(offset, length - offset)); if (read == 0) throw new EndOfStreamException(); offset += read; }
                var doc = JsonDocument.Parse(payload); var root = doc.RootElement;
                if (root.TryGetProperty("type", out var type) && type.GetString() == "response" && root.TryGetProperty("request_seq", out var requestSeq) && _pending.TryRemove(requestSeq.GetInt32(), out var waiter)) waiter.TrySetResult(doc);
                else if (root.TryGetProperty("type", out type) && type.GetString() == "event")
                {
                    if (root.TryGetProperty("event", out var evt) && evt.GetString() == "stopped" && root.TryGetProperty("body", out var body) && body.TryGetProperty("threadId", out var thread)) LastStoppedThreadId = thread.GetInt32();
                    MessageReceived?.Invoke("[DAP] " + root.ToString()); EventReceived?.Invoke(root.Clone()); doc.Dispose();
                }
                else { MessageReceived?.Invoke("[DAP] " + root.ToString()); doc.Dispose(); }
            }
        }
        catch (Exception ex) { MessageReceived?.Invoke("[DAP] Lecture interrompue : " + ex.Message); }
    }

    private static async Task<Dictionary<string, string>?> ReadHeaderAsync(Stream stream)
    {
        var bytes = new List<byte>(); var tail = 0;
        while (bytes.Count < 8192)
        {
            var one = new byte[1]; var n = await stream.ReadAsync(one.AsMemory(0, 1)); if (n == 0) return bytes.Count == 0 ? null : throw new EndOfStreamException();
            bytes.Add(one[0]); tail = (tail << 8) | one[0];
            if (tail == 0x0d0a0d0a) break;
        }
        var text = Encoding.ASCII.GetString(bytes.ToArray());
        return text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split(':', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task StopAsync()
    {
        try { if (IsRunning) { using var response = await RequestAsync("disconnect", new { restart = false, terminateDebuggee = true }, CancellationToken.None); } } catch { }
        try { if (_process is { HasExited: false }) _process.Kill(true); } catch { }
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_readerTask is not null) try { await _readerTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        _process?.Dispose(); _process = null; _input = null; _output = null; _writeLock.Dispose();
    }
}

public sealed record DebugBreakpoint(int Line, string? Condition = null, string? HitCondition = null, string? LogMessage = null);
