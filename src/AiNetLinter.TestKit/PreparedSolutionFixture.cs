#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.TestKit;

/// <summary>
/// Assembly-weit geteilter, pro Szenario lazy materialisierender Cache fuer
/// <see cref="RoslynTestSolutionFactory"/>-Solutions. Jedes Szenario wird ueber
/// <see cref="GetOrCreate"/> hoechstens einmal gebaut, unabhaengig davon, wie viele Testklassen
/// gleichzeitig danach fragen (<see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>).
/// Der zurueckgegebene <see cref="Solution"/>-Snapshot ist write-once: Konsumenten mutieren ihn
/// nicht (kein <c>TryApplyChanges</c>), sondern arbeiten auf einem eigenen abgeleiteten Snapshot
/// oder einer eigenen <see cref="RoslynTestSolutionFactory"/>-Instanz.
/// </summary>
public sealed class PreparedSolutionFixture : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<RoslynTestSolution>> scenarios = new(StringComparer.Ordinal);

    /// <summary>
    /// Liefert die Solution des Szenarios <paramref name="scenarioName"/>, materialisiert sie beim
    /// ersten Zugriff ueber <paramref name="factory"/>. Nachfolgende Aufrufe mit demselben
    /// Szenarionamen fuehren <paramref name="factory"/> nicht erneut aus, auch nicht bei
    /// gleichzeitigen Aufrufen aus mehreren Threads.
    /// </summary>
    public Solution GetOrCreate(string scenarioName, Func<RoslynTestSolution> factory)
    {
        var lazy = scenarios.GetOrAdd(
            scenarioName,
            _ => new Lazy<RoslynTestSolution>(factory, LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value.Solution;
    }

    public void Dispose()
    {
        foreach (var lazy in scenarios.Values)
        {
            if (lazy.IsValueCreated)
            {
                lazy.Value.Workspace.Dispose();
            }
        }
    }
}
