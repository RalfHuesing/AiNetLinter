#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Begrenzt die Anzahl gleichzeitig laufender <c>AiNetLinter.exe</c>-Subprozesse ueber alle
/// Testklassen hinweg, ohne die betroffenen Klassen komplett zu serialisieren. Ersetzt die
/// vormalige Zwangsserialisierung ueber <c>ConsoleTestCollection</c> fuer Subprozess-Tests durch
/// eine gezielte, zaehlende Bremse (siehe AiNetLinterRichtlinien.mdc §4).
/// </summary>
public static class SubprocessConcurrencyGate
{
    private const int MaxConcurrentSubprocesses = 4;

    private static readonly SemaphoreSlim Gate = new(MaxConcurrentSubprocesses, MaxConcurrentSubprocesses);

    /// <summary>
    /// Wartet auf einen freien Slot und gibt ein <see cref="IDisposable"/> zurueck, das den Slot
    /// beim Dispose wieder freigibt. Wie lange der Aufrufer das Lease haelt (nur Start/Handshake
    /// vs. gesamte Prozesslaufzeit) entscheidet er selbst — kein universeller Vertrag; siehe
    /// <c>McpTestClient.ConnectAsync</c> (Lease nur fuer Start+Handshake) vs. <c>CliProcessRunner</c>
    /// (Lease bis Prozessende) fuer zwei unterschiedliche, beide gueltige Nutzungsmuster.
    /// </summary>
    public static async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease();
    }

    private sealed class Lease : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                Gate.Release();
            }
        }
    }
}
