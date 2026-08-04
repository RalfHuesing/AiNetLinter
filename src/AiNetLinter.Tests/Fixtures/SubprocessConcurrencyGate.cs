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
    // Kapazitaet an die Last-Fixture-Skalierung der parallel laufenden MCP-Server-Tests angepasst;
    // beobachtete Spitzenlast im Volllauf erfordert mehr als die urspruenglichen 4 gleichzeitigen
    // Subprozesse, ohne dass eine einzelne Testklasse ueberlastet wird. Werte unterhalb dieser
    // Schwelle fuehren reproduzierbar zu Wait-Stack-Traces am Gate unter Volllauf-Bedingungen.
    private const int MaxConcurrentSubprocesses = 6;

    // Zusaetzlicher expliziter Timeout am Gate selbst (zusaetzlich zum CancellationToken des
    // Aufrufers): macht einen Last-Flake als TimeoutException sprechend sichtbar, bevor der
    // Caller-CT in einen unbestimmten Wait-Stack laeuft.
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(60);

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
        await Gate.WaitAsync(cancellationToken)
            .WaitAsync(WaitTimeout, cancellationToken)
            .ConfigureAwait(false);
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
