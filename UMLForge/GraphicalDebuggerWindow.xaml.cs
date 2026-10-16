using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using UMLForge.Services;

namespace UMLForge;

public partial class GraphicalDebuggerWindow : Window
{
    private readonly string _root;
    private DapDebugSession? _session;
    public GraphicalDebuggerWindow(string root)
    {
        InitializeComponent(); _root = Path.GetFullPath(root);
        SourceFileBox.Text = Directory.GetFiles(_root, "*.cs", SearchOption.AllDirectories).FirstOrDefault(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) ?? "";
        Output("Sélectionnez netcoredbg, la DLL et le fichier source. Build Debug génère la cible avant le lancement.");
        Closed += async (_, _) => { if (_session is not null) await _session.DisposeAsync(); };
    }

    private void BrowseAdapter_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Title = "Sélectionner l'adaptateur DAP (netcoredbg)", Filter = "Exécutable|*.exe;netcoredbg|Tous les fichiers|*.*" };
        if (d.ShowDialog() == true) AdapterPathBox.Text = d.FileName;
    }
    private void BrowseDll_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Title = "Sélectionner la DLL .NET compilée", Filter = ".NET DLL|*.dll|Tous les fichiers|*.*", InitialDirectory = _root };
        if (d.ShowDialog() == true) TargetDllBox.Text = d.FileName;
    }
    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Title = "Sélectionner le fichier C# où placer les points d'arrêt", Filter = "C#|*.cs", InitialDirectory = _root };
        if (d.ShowDialog() == true) SourceFileBox.Text = d.FileName;
    }

    private async void BuildDebug_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Output("> dotnet build --configuration Debug");
            var psi = new ProcessStartInfo("dotnet", "build --configuration Debug --nologo") { WorkingDirectory = _root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de démarrer dotnet.");
            var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync();
            Output(await stdout + Environment.NewLine + await stderr);
            if (p.ExitCode != 0) { Output("Compilation Debug échouée. Corrigez les erreurs avant de lancer le débogueur."); return; }
            var dll = Directory.GetFiles(_root, "*.dll", SearchOption.AllDirectories).Where(f => f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "Debug" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "refint" + Path.DirectorySeparatorChar)).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (dll is not null) { TargetDllBox.Text = dll; Output("DLL Debug sélectionnée : " + dll); }
            else Output("Build réussi mais DLL non détectée. Sélectionnez-la manuellement.");
        }
        catch (Exception ex) { Output("Build Debug impossible : " + ex.Message); }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_session is not null) await _session.DisposeAsync();
            var breakpoints = BreakpointLinesBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseBreakpoint).Where(b => b is not null).Cast<DebugBreakpoint>().ToArray();
            _session = new DapDebugSession();
            _session.MessageReceived += msg => Dispatcher.BeginInvoke(new Action(() => Output(msg)));
            await _session.StartAsync(AdapterPathBox.Text.Trim(), TargetDllBox.Text.Trim(), _root, SourceFileBox.Text.Trim(), breakpoints);
        }
        catch (Exception ex) { Output("Démarrage DAP impossible : " + ex.Message); }
    }
    private async void Continue_Click(object sender, RoutedEventArgs e) => await Invoke(s => s.ContinueAsync());
    private async void Pause_Click(object sender, RoutedEventArgs e) => await Invoke(s => s.PauseAsync());
    private async void Next_Click(object sender, RoutedEventArgs e) => await Invoke(s => s.NextAsync());
    private async void StepIn_Click(object sender, RoutedEventArgs e) => await Invoke(s => s.StepInAsync());
    private async void StepOut_Click(object sender, RoutedEventArgs e) => await Invoke(s => s.StepOutAsync());
    private async void Stack_Click(object sender, RoutedEventArgs e) { if (_session is null) return; try { DetailsBox.Text = await _session.GetStackTraceAsync(); } catch (Exception ex) { Output(ex.Message); } }
    private async void Variables_Click(object sender, RoutedEventArgs e) { if (_session is null) return; try { DetailsBox.Text = await _session.GetVariablesAsync(); } catch (Exception ex) { Output(ex.Message); } }
    private async void Stop_Click(object sender, RoutedEventArgs e) { if (_session is not null) { await _session.StopAsync(); Output("Arrêt demandé."); } }
    private async void Threads_Click(object sender, RoutedEventArgs e) { if (_session is null) return; try { DetailsBox.Text = await _session.GetThreadsAsync(); } catch (Exception ex) { Output(ex.Message); } }
    private async void EvaluateWatch_Click(object sender, RoutedEventArgs e) { if (_session is null) return; try { DetailsBox.Text = await _session.EvaluateAsync(WatchExpressionBox.Text.Trim()); } catch (Exception ex) { Output("Watch : " + ex.Message); } }
    private static DebugBreakpoint? ParseBreakpoint(string raw)
    {
        var parts = raw.Split('?', 2, StringSplitOptions.TrimEntries);
        if (!int.TryParse(parts[0], out var line) || line <= 0) return null;
        return new DebugBreakpoint(line, parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null);
    }
    private async Task Invoke(Func<DapDebugSession, Task> action) { if (_session is null) { Output("Démarrez d'abord une session."); return; } try { await action(_session); } catch (Exception ex) { Output("Commande DAP : " + ex.Message); } }
    private void Output(string message) => OutputBox.Text = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}{Environment.NewLine}" + OutputBox.Text;
}
