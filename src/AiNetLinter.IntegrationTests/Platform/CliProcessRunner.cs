#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.IntegrationTests.Platform;

internal readonly record struct CliProcessResult(int ExitCode, string Output, string Error, bool TimedOut = false);

internal static class CliProcessRunner
{
    public static string FindSolutionRoot() => SolutionRootLocator.Find();

    public static string FindLinterDll(string rootDir)
    {
        var binDir = Path.Combine(rootDir, "src", "AiNetLinter", "bin");
        if (!Directory.Exists(binDir))
        {
            throw new DirectoryNotFoundException($"Das Build-Ausgabeverzeichnis existiert nicht: {binDir}");
        }

        var files = Directory.GetFiles(binDir, "AiNetLinter.dll", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            throw new FileNotFoundException("Die Datei 'AiNetLinter.dll' wurde in keinem Build-Unterordner gefunden.");
        }

        return files.OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc).First();
    }

    public static async Task<CliProcessResult> RunLinterAsync(string arguments, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = rootDir,
        };

        return await RunAsync(startInfo, timeout, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CliProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var lease = await SubprocessLifetimeBudget.Shared.AcquireAsync(cancellationToken).ConfigureAwait(false);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Konnte den Prozess nicht starten ('{startInfo.FileName} {startInfo.Arguments}').");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        if (timeout is { } timeoutValue)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutValue);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort-Kill
                }
                var timedOutOutput = await outputTask.ConfigureAwait(false);
                var timedOutError = await errorTask.ConfigureAwait(false);
                return new CliProcessResult(-1, timedOutOutput, timedOutError, TimedOut: true);
            }
        }
        else
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        return new CliProcessResult(process.ExitCode, output, error);
    }
}
