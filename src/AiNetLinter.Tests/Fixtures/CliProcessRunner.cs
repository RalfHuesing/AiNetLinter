#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Ergebnis eines über <see cref="CliProcessRunner"/> gestarteten Subprozesses.
/// <see cref="TimedOut"/> ist nur bei <see cref="CliProcessRunner.RunAsync"/> mit gesetztem
/// Timeout relevant — der Aufrufer entscheidet selbst per Assert, wie er darauf reagiert.
/// </summary>
public readonly record struct CliProcessResult(int ExitCode, string Output, string Error, bool TimedOut = false);

/// <summary>
/// Konsolidiert Solution-Root-/Linter-DLL-Auflösung sowie Prozessstart-/Output-Capture-/
/// <see cref="SubprocessConcurrencyGate"/>-Boilerplate für CLI-Subprozess-Tests. Ersetzt die
/// vormals in mehreren Testklassen fast identisch dupliierten
/// <c>FindSolutionRoot</c>/<c>FindLinterDll</c>/<c>RunLinter</c>-Implementierungen (Konzept F.2).
/// </summary>
public static class CliProcessRunner
{
    /// <summary>
    /// Sucht ausgehend vom Testlauf-Basisverzeichnis nach oben nach der Projektmappe
    /// <c>AiNetLinter.slnx</c>.
    /// </summary>
    public static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }

    /// <summary>
    /// Sucht die neueste gebaute <c>AiNetLinter.dll</c> unterhalb von <c>src/AiNetLinter/bin</c>
    /// (nach letzter Schreibzeit, falls mehrere Build-Ausgabeverzeichnisse existieren).
    /// </summary>
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

    /// <summary>
    /// Komfort-Methode für den haeufigsten Fall: <c>dotnet &lt;AiNetLinter.dll&gt; &lt;arguments&gt;</c>,
    /// loest Solution-Root und DLL-Pfad intern auf.
    /// </summary>
    public static async Task<CliProcessResult> RunLinterAsync(string arguments, CancellationToken cancellationToken = default)
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
        };

        return await RunAsync(startInfo, timeout: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generische, <see cref="SubprocessConcurrencyGate"/>-abgesicherte Variante für Aufrufer mit
    /// eigenem <see cref="ProcessStartInfo"/> (z. B. direkter Exe-Start statt <c>dotnet dll</c>).
    /// Bei gesetztem <paramref name="timeout"/> liefert das Ergebnis <c>TimedOut = true</c> statt
    /// einer Exception, falls der Prozess nicht rechtzeitig beendet — der Aufrufer entscheidet
    /// selbst per Assert, wie er reagiert.
    /// </summary>
    public static async Task<CliProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var lease = await SubprocessConcurrencyGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Konnte den Prozess nicht starten ('{startInfo.FileName} {startInfo.Arguments}').");

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

    /// <summary>
    /// Rein synchrone Variante ohne <see cref="SubprocessConcurrencyGate"/>-Nutzung, für Aufrufer
    /// aus nicht-<c>async</c>-fähigem Kontext (z. B. Konstruktoren). Nutzt das
    /// <c>BeginOutputReadLine</c>-Event-Muster statt <c>ReadToEnd()</c>, um Deadlocks bei langen
    /// stdout/stderr-Strömen zu vermeiden.
    /// </summary>
    public static CliProcessResult RunSync(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Konnte den Prozess nicht starten ('{startInfo.FileName} {startInfo.Arguments}').");

        if (startInfo.RedirectStandardInput)
        {
            process.StandardInput.Close();
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.Append(e.Data).Append('\n'); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.Append(e.Data).Append('\n'); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        return new CliProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
