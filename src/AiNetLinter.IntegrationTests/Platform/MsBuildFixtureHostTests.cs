#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.IntegrationTests.Platform;

/// <summary>
/// Helfer fuer <see cref="MsBuildFixtureHostTests"/>/<see cref="MsBuildFixtureHostSharedInstanceTests"/>:
/// erzwingt und belegt die Objektidentitaet der von <see cref="MsBuildFixtureHost"/> geteilten
/// <see cref="Solution"/> ueber mehrere Testklassen hinweg (Nachweis "einmal geladen", analog zum
/// Referenz-Caching-Test aus step-006). Ordnungsunabhaengig: der Vergleich laeuft bei jeder
/// Instanziierung einer der beiden Testklassen, nicht nur bei einer bestimmten Ausfuehrungsreihenfolge.
/// </summary>
internal static class SharedSolutionIdentityWitness
{
    private static Solution? seen;
    private static readonly object gate = new();

    public static void RecordAndVerify(Solution solution)
    {
        lock (gate)
        {
            if (seen is null)
            {
                seen = solution;
                return;
            }

            if (!ReferenceEquals(seen, solution))
            {
                throw new InvalidOperationException(
                    "MsBuildFixtureHost.Solution unterscheidet sich zwischen Testklassen -- Einmal-Load-Vertrag verletzt.");
            }
        }
    }
}

/// <summary>
/// Vertragstests fuer <see cref="MsBuildFixtureHost"/> (konzept.md §2): belegt mechanisch, dass die
/// isolierte <c>BaselineMini</c>-Kopie tatsaechlich einmal ueber einen echten MSBuild-Workspace geladen
/// wird. Erhaelt die Fixture ueber die assembly-weite
/// <c>[assembly: AssemblyFixture(typeof(MsBuildFixtureHost))]</c>-Registrierung (siehe
/// <c>Platform/MsBuildFixtureHostAssemblyFixture.cs</c>).
/// </summary>
[Trait("Category", "Integration")]
public sealed class MsBuildFixtureHostTests
{
    private readonly MsBuildFixtureHost host;

    public MsBuildFixtureHostTests(MsBuildFixtureHost host)
    {
        this.host = host;
        SharedSolutionIdentityWitness.RecordAndVerify(host.Solution);
    }

    [Fact]
    public void Solution_AfterInjection_IsNotNullAndContainsBaselineMiniProject()
    {
        Assert.NotNull(host.Solution);
        Assert.Contains(host.Solution.Projects, p => p.Name == "BaselineMini");
    }

    [Fact]
    public void Catalog_AfterInjection_IsNotNull()
    {
        Assert.NotNull(host.Catalog);
    }
}

/// <summary>
/// Zweite Testklasse, die dieselbe <see cref="MsBuildFixtureHost"/>-Instanz injiziert bekommt --
/// belegt gemeinsam mit <see cref="MsBuildFixtureHostTests"/> ueber
/// <see cref="SharedSolutionIdentityWitness"/>, dass beide dieselbe <see cref="Solution"/>-Instanz sehen.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MsBuildFixtureHostSharedInstanceTests
{
    private readonly MsBuildFixtureHost host;

    public MsBuildFixtureHostSharedInstanceTests(MsBuildFixtureHost host)
    {
        this.host = host;
        SharedSolutionIdentityWitness.RecordAndVerify(host.Solution);
    }

    [Fact]
    public void Solution_SharedAcrossTestClasses_MatchesWitnessedIdentity()
    {
        SharedSolutionIdentityWitness.RecordAndVerify(host.Solution);
        Assert.NotNull(host.Solution);
    }
}
