#nullable enable

using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AiNetLinter.FastTests.Architecture;

/// <summary>
/// Prüft vor und nach dem vollständigen Fast-Testlauf auf verbotene Runtime-Abhängigkeiten.
/// </summary>
public sealed class FastTestsRuntimeDependencyGuardFixture : IDisposable
{
    private static readonly string[] DeniedAssemblyNamePrefixes =
    {
        "Microsoft.Build",
        "Microsoft.CodeAnalysis.Workspaces.MSBuild",
    };

    public FastTestsRuntimeDependencyGuardFixture() => EnsureNoDeniedAssembly("Initialisierung");

    public void Dispose() => EnsureNoDeniedAssembly("Abschluss");

    internal static IReadOnlyList<string> FindLoadedDeniedAssemblies()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetName().Name ?? string.Empty)
            .Where(name => DeniedAssemblyNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static void EnsureNoDeniedAssembly(string phase)
    {
        var loaded = FindLoadedDeniedAssemblies();
        if (loaded.Count > 0)
        {
            throw new InvalidOperationException(
                $"Fast-Runtime-Dependency-Guard in Phase '{phase}' fand verbotene Assemblies: {string.Join(", ", loaded)}");
        }
    }
}
