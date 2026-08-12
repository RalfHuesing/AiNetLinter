#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Core;

/// <summary>
/// MSE-Baustein "vorbereitete Solution analysieren, regelkonformes Ergebnis und deterministischer
/// Fehlerweg": ruft <see cref="LinterEngine.RunAsync(Solution, bool, int, System.Threading.CancellationToken)"/>
/// direkt gegen eine per <see cref="RoslynTestSolutionFactory"/> aufgebaute Zwei-Klassen-Solution auf
/// (kein MSBuild, keine Platte) und prueft sowohl den Verletzungs- als auch den regelkonformen Pfad
/// deterministisch. Nutzt den internal LinterEngine-Konstruktor ueber
/// InternalsVisibleTo("AiNetLinter.FastTests").
/// </summary>
[Trait("Category", "Component")]
public sealed class LinterEngineSolutionAnalysisTests
{
    private static Config CreateConfig() => new()
    {
        Global = new GlobalConfig { EnforceSealedClasses = true },
        Metrics = new MetricsConfig(),
    };

    [Fact]
    public async Task RunAsync_PreparedSolutionWithSealedClassViolation_FlagsViolatingClassAndSparesCompliantClass()
    {
        const string violatingClass = """
            namespace SolutionAnalysis;
            public class UnsealedService
            {
                public void Do() {}
            }
            """;
        const string compliantClass = """
            namespace SolutionAnalysis;
            public sealed class SealedService
            {
                public void Do() {}
            }
            """;

        using var testSolution = RoslynTestSolutionFactory.CreateSolution(new ProjectSpec(
            "SolutionAnalysisTestProject",
            [("UnsealedService.cs", violatingClass), ("SealedService.cs", compliantClass)]));

        var engine = new LinterEngine(CreateConfig());
        var violations = await engine.RunAsync(testSolution.Solution);

        Assert.Contains(violations, v =>
            v.RuleName == nameof(GlobalConfig.EnforceSealedClasses) &&
            v.FilePath.EndsWith("UnsealedService.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, v => v.FilePath.EndsWith("SealedService.cs", StringComparison.Ordinal));
    }
}
