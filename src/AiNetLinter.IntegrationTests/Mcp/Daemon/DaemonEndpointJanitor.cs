#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Daemon;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

/// <summary>
/// Suite-weites Cleanup/Gating fuer den benutzergebundenen Daemon-Pipe-Endpunkt
/// (<c>ainetlinter.analyzer.v1.&lt;user&gt;</c>): vor dem ersten Endpunkt-Zugriff eines Testlaufs
/// werden uebrig gebliebene Daemon-Prozesse eigener Bauart beendet und der Endpunkt per
/// Client-Probe verifiziert. Identifikation ausschliesslich ueber Prozesse, deren Bildpfad
/// <c>AiNetLinter.exe</c> lautet UND entweder der eigenen Test-AiNetLinter.exe
/// (<see cref="OwnExecutablePath"/>) entspricht oder unterhalb des Repositories liegt
/// (<c>SolutionRootLocator.Find()</c>) — niemals ueber blinde Prozessnamen-Matches auf
/// Installationsorte ausserhalb des Repos; Prozesse, deren Bildpfad nicht ermittelt werden
/// kann, werden nie angetastet. Bleibt der Endpunkt danach durch einen NICHT
/// identifizierbaren Prozess belegt, melden Endpunkt-bindende Tests das transparent als
/// Skip mit Begruendung statt als raetselhafte Ausfaelle am Doppelstart-Lock.
/// </summary>
internal static class DaemonEndpointJanitor
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string? contaminationReason;
    private static int ensured;

    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;

    internal static string OwnExecutablePath =>
        Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");

    /// <summary>Build-Ausgaben dieses Repositories (alle Projekt-Bins); Daemons aus
    /// <c>dotnet run</c>/manuellen Gate-Sessions landen hier ebenso wie die Test-Kopie.</summary>
    private static readonly string RepositoryRoot =
        SolutionRootLocator.Find().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Stellt genau einmal pro Testlauf sicher, dass der Endpunkt frei ist (idempotent und
    /// parallel-sicher); nachfolgende Aufrufe sind ohne Zusatzkosten.
    /// </summary>
    internal static async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref ensured) == 1)
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref ensured) == 1)
            {
                return;
            }

            contaminationReason = await CleanEndpointAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref ensured, 1);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Gated einen Endpunkt-bindenden Test: laeuft nach <see cref="EnsureReadyAsync"/> und
    /// loest ein xUnit-Skip aus, wenn der Endpunkt nicht freigemacht werden konnte.
    /// </summary>
    internal static async Task SkipIfEndpointBlockedAsync(CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var reason = Volatile.Read(ref contaminationReason);
        if (reason is not null)
        {
            Assert.Skip(reason);
        }
    }

    private static async Task<string?> CleanEndpointAsync(CancellationToken cancellationToken)
    {
        foreach (var leftover in FindOwnExecutableProcesses())
        {
            await TerminateAsync(leftover, cancellationToken).ConfigureAwait(false);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await IsListenerPresentAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var leftovers = FindOwnExecutableProcesses();
            if (leftovers.Count > 0)
            {
                foreach (var leftover in leftovers)
                {
                    await TerminateAsync(leftover, cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            if (DateTime.UtcNow > deadline)
            {
                return "Der Daemon-Pipe-Endpunkt ainetlinter.analyzer.v1.<Benutzer> bleibt durch einen "
                    + "nicht identifizierbaren Prozess belegt (kein Prozess der eigenen Test-EXE); "
                    + "Endpunkt-bindende Daemon-Tests werden uebersprungen.";
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Auf eigene Bauart begrenzte Kandidatensuche: nur Prozesse, deren Bildpfad
    /// <c>AiNetLinter.exe</c> lautet und zur Test-Ausgabe oder zu einer anderen Build-Ausgabe
    /// dieses Repositories gehoert (Fremdinstallationen ausserhalb des Repos und Systemprozesse
    /// scheiden aus; unlesbare Bildpfade werden bewusst uebersprungen).</summary>
    private static List<Process> FindOwnExecutableProcesses()
    {
        var ownProcessId = Environment.ProcessId;
        var matches = new List<Process>();
        foreach (var process in Process.GetProcesses())
        {
            var isMatch = false;
            try
            {
                if (process.Id != ownProcessId && !process.HasExited)
                {
                    isMatch = IsOwnBuildImage(process);
                }
            }
            catch (Exception exception) when (
                exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // Bildpfad nicht ermittelbar (Schutzstufe/Rechte): nicht identifizierbar,
                // daher wird dieser Prozess bewusst nie beendet.
                isMatch = false;
            }
            finally
            {
                if (isMatch)
                {
                    matches.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
        }

        return matches;
    }

    private static bool IsOwnBuildImage(Process process)
    {
        var imagePath = process.MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return false;
        }

        if (!string.Equals(Path.GetFileName(imagePath), "AiNetLinter.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fullImagePath = Path.GetFullPath(imagePath);
        if (string.Equals(fullImagePath, OwnExecutablePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fullImagePath.StartsWith(RepositoryRoot, StringComparison.OrdinalIgnoreCase)
            && fullImagePath.Length > RepositoryRoot.Length
            && fullImagePath[RepositoryRoot.Length] is '\\' or '/';
    }

    private static async Task TerminateAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception
                or InvalidOperationException
                or TimeoutException)
        {
            // Prozess wurde zwischenzeitlich bereits beendet oder ist nicht mehr kill-bar;
            // der anschliessende Endpunkt-Probe entscheidet verlaesslich ueber den Rest.
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task<bool> IsListenerPresentAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var client = new NamedPipeClientStream(
            ".",
            DaemonProtocol.GetPipeName(DaemonProtocol.CurrentUserName),
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await client.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (IOException exception) when (IsPipeAbsent(exception))
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static bool IsPipeAbsent(IOException exception)
    {
        var errorCode = exception.HResult & 0xFFFF;
        return errorCode is ErrorFileNotFound or ErrorPathNotFound;
    }
}

/// <summary>
/// xUnit-v3-Assembly-Fixture-Host fuer den <see cref="DaemonEndpointJanitor"/>: xUnit erzeugt
/// die Instanz beim ersten Injizieren und stellt damit sicher, dass das Endpoint-Cleanup vor den
/// Tests endpunktbindender Klassen gelaufen ist. Die statische Sicherstellung im Janitor bleibt
/// daneben der universelle Choke-Point (siehe <c>DaemonProcessContractHarness.AcquireEndpointAsync</c>)
/// und kostet nach dem ersten Lauf nichts mehr.
/// </summary>
public sealed class DaemonEndpointJanitorFixture : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(DaemonEndpointJanitor.EnsureReadyAsync(CancellationToken.None));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
