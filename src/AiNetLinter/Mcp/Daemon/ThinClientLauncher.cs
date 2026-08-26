#nullable enable

using System.Diagnostics;
using System.Reflection;
using System.ComponentModel;
using AiNetLinter.Cli;

namespace AiNetLinter.Mcp.Daemon;

internal sealed record ThinClientLaunchOptions(
    decimal? ProjectTtlMinutes,
    int? MaxProjects,
    decimal? IdleExitMinutes,
    string? DaemonInstance)
{
    internal ThinClientLaunchOptions(decimal? projectTtlMinutes, int? maxProjects, decimal? idleExitMinutes)
        : this(projectTtlMinutes, maxProjects, idleExitMinutes, null)
    {
    }
}

internal static class ThinClientLauncher
{
    internal static bool TryStartDetached(
        ThinClientLaunchOptions options,
        Action<string>? report = null)
    {
        try
        {
            var process = Process.Start(CreateStartInfo(options));
            var started = process is not null;
            var processId = process?.Id;
            report?.Invoke(!started
                ? "[WARN]: Detached-Daemon lieferte kein Prozesshandle."
                : $"[INFO]: Detached-Daemon gestartet (PID={processId}).");
            if (process is null) return false;

            process.StandardInput.Close();
            process.Dispose();
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            report?.Invoke($"[WARN]: Daemon konnte nicht detached gestartet werden: {exception.Message}");
            return false;
        }
    }

    internal static ProcessStartInfo CreateStartInfo(ThinClientLaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Der aktuelle Prozesspfad ist nicht verfuegbar.");
        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };

        if (IsDotnetHost(processPath))
        {
            startInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        }

        startInfo.ArgumentList.Add(CliOptionFactory.DaemonStart);
        AddOption(startInfo, CliOptionFactory.McpProjectTtlMinutes, options.ProjectTtlMinutes);
        AddOption(startInfo, CliOptionFactory.McpMaxProjects, options.MaxProjects);
        AddOption(startInfo, CliOptionFactory.McpDaemonIdleExitMinutes, options.IdleExitMinutes);
        AddOption(startInfo, CliOptionFactory.DaemonInstance, DaemonInstanceId.Normalize(options.DaemonInstance));
        return startInfo;
    }

    private static void AddOption(ProcessStartInfo startInfo, string name, object? value)
    {
        if (value is null) return;
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!);
    }

    private static bool IsDotnetHost(string processPath) =>
        Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase);

}
