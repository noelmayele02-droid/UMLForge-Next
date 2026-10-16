using System.Diagnostics;

namespace UMLForge.Services;

public sealed record CliResult(int ExitCode, string Output, TimeSpan Duration)
{
    public bool Success => ExitCode == 0;
}

public static class DotNetCliService
{
    public static async Task<CliResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        using var process = new Process { StartInfo = start };
        var watch = Stopwatch.StartNew();
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        watch.Stop();
        return new CliResult(process.ExitCode, (await stdout) + Environment.NewLine + (await stderr), watch.Elapsed);
    }
}
