#nullable enable

using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AiNetLinter.FastTests.Architecture;

/// <summary>
/// Laufzeitcheck-Gegenstueck zu <see cref="FastTestsDependencyGuardTests"/>: die statische
/// Deny-Liste prueft nur Referenzen, keine tatsaechliche Ausfuehrung (konzept.md Leitplanke 6,
/// "Was die Guards wirklich koennen"). Wird ueber <see cref="FastTestsRuntimeDependencyGuardCollection"/>
/// als geteilte Collection-Fixture eingehaengt; ihr Dispose laeuft nach den Tests der Collection und
/// prueft, ob in der Zwischenzeit eine MSBuild-/Workspace-Assembly in den Prozess geladen wurde.
/// Bewusst keine pauschale Serialisierung der gesamten FastTests-Assembly (Regel-Ref
/// AiNetLinterRichtlinien.mdc §4) -- nur diese eine Collection teilt sich die Fixture, andere
/// Collections bleiben CPU-parallel. Der Check ist damit ein Best-Effort-Nachweis fuer den
/// ueblichen Lauf, keine absolute Prozessisolationsgarantie (dafuer bräuchte es einen eigenen
/// Testhost-Prozess pro Assembly-Fixture-Scope).
/// </summary>
public sealed class FastTestsRuntimeDependencyGuardFixture : IDisposable
{
    private static readonly string[] DeniedAssemblyNamePrefixes =
    {
        "Microsoft.Build",
        "Microsoft.CodeAnalysis.Workspaces.MSBuild",
    };

    public void Dispose()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetName().Name ?? string.Empty)
            .Where(name => DeniedAssemblyNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (loaded.Count > 0)
        {
            throw new InvalidOperationException(
                $"Waehrend des Fast-Laufs wurden verbotene Assemblies geladen: {string.Join(", ", loaded)}");
        }
    }
}

[CollectionDefinition("FastTestsRuntimeDependencyGuard")]
public sealed class FastTestsRuntimeDependencyGuardCollection : ICollectionFixture<FastTestsRuntimeDependencyGuardFixture>
{
}
