#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.PatternDetect;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="PatternDetectScanner"/> — deckt die Zuordnung je Pattern (1 Test pro
/// Pattern, siehe <see cref="PatternCatalog"/>) sowie Edge-Cases (0 Treffer, Scope ohne Treffer,
/// Trunkierung, Malfunction) ab. Kleine, gezielte virtuelle Solutions statt der geteilten
/// Live-Fixture — pro Test genau der Code, der die jeweilige Regel
/// deterministisch ausloest (Pattern 1:1 von <c>SafeguardScannerTests</c> uebernommen).
/// </summary>
[Trait("Category", "Component")]
public sealed class PatternDetectScannerTests
{
    [Fact]
    public async Task BuildReportAsync_MaxPublicMembersPerTypeViolation_AttributedToGodClassPattern()
    {
        const string source = @"
namespace Test;
public sealed class Wide
{
    public void M1() {}
    public void M2() {}
    public void M3() {}
}";
        var config = CreateConfig() with
        {
            Metrics = new MetricsConfig { MaxPublicMembersPerType = 2 },
        };
        var result = await RunAsync(("Wide.cs", source), config);

        var godClass = result.Payload!.Patterns.Single(p => p.Id == "god-class");
        Assert.True(godClass.Occurrences > 0);
        Assert.Contains(godClass.Items, i => i.RuleName == "MaxPublicMembersPerType");
    }

    [Fact]
    public async Task BuildReportAsync_AsyncVoidMethod_AttributedToAsyncVoidPattern()
    {
        const string source = @"
using System.Threading.Tasks;
namespace Test;
public sealed class Foo
{
    public async void Run() { await Task.Delay(0); }
}";
        var result = await RunAsync(("Foo.cs", source), CreateConfig());

        var asyncVoid = result.Payload!.Patterns.Single(p => p.Id == "async-void");
        Assert.Equal(1, asyncVoid.Occurrences);
        Assert.Equal("BanAsyncVoid", asyncVoid.Items.Single().RuleName);
    }

    [Fact]
    public async Task BuildReportAsync_MaxMethodLineCountViolation_AttributedToLongMethodPattern()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public int Sum(int a, int b)
    {
        var result = a + b;
        return result;
    }
}";
        var config = CreateConfig() with
        {
            // CompoundSuppressions=[] noetig: der MetricsConfig-Default enthaelt eine Suppression
            // fuer MaxMethodLineCount bei CC<=3 und CogC<=5 (RelaxedLimit 150) — genau der Fall
            // dieser trivialen Sum-Methode. Ohne das Leeren der Liste wuerde die Violation
            // stillschweigend unterdrueckt (empirisch in einem fruehen Testlauf hier gefunden).
            Metrics = new MetricsConfig { MaxMethodLineCount = 1, CompoundSuppressions = [] },
        };
        var result = await RunAsync(("Foo.cs", source), config);

        var longMethod = result.Payload!.Patterns.Single(p => p.Id == "long-method");
        Assert.True(longMethod.Occurrences > 0);
        Assert.Contains(longMethod.Items, i => i.RuleName == "MaxMethodLineCount");
    }

    [Fact]
    public async Task BuildReportAsync_MissingXmlDoc_AttributedToPublicWithoutDocPattern()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void Run() {}
}";
        var config = CreateConfig() with
        {
            Global = QuietGlobal() with { EnforceXmlDocumentation = true },
        };
        var result = await RunAsync(("Foo.cs", source), config);

        var publicWithoutDoc = result.Payload!.Patterns.Single(p => p.Id == "public-without-doc");
        Assert.True(publicWithoutDoc.Occurrences > 0);
        Assert.Contains(publicWithoutDoc.Items, i => i.RuleName == "EnforceXmlDocumentation");
    }

    [Fact]
    public async Task BuildReportAsync_EmptyCatchBlock_AttributedToEmptyCatchPattern()
    {
        const string source = @"
using System;
namespace Test;
public sealed class Foo
{
    public void Run()
    {
        try { }
        catch (InvalidOperationException) { }
    }
}";
        var result = await RunAsync(("Foo.cs", source), CreateConfig());

        var emptyCatch = result.Payload!.Patterns.Single(p => p.Id == "empty-catch");
        Assert.Equal(1, emptyCatch.Occurrences);
        Assert.Equal("EnforceNoSilentCatch", emptyCatch.Items.Single().RuleName);
    }

    [Fact]
    public async Task BuildReportAsync_MiddleManClass_AttributedToFeatureEnvyPattern()
    {
        const string source = @"
namespace Test;
public class Collaborator
{
    public void DoStuff() {}
    public int Value => 42;
}
public class MiddleManClass
{
    private readonly Collaborator _c = new();
    public void M1() => _c.DoStuff();
    public void M2() { _c.DoStuff(); }
    public int P1 => _c.Value;
    public int P2 { get { return _c.Value; } }
    public void M3() => _c.DoStuff();
}";
        var result = await RunAsync(("MiddleMan.cs", source), CreateConfig());

        var featureEnvy = result.Payload!.Patterns.Single(p => p.Id == "feature-envy");
        Assert.Equal(1, featureEnvy.Occurrences);
        Assert.Equal("AvoidExcessiveMiddleMen", featureEnvy.Items.Single().RuleName);
    }

    [Fact]
    public async Task BuildReportAsync_CleanSolution_AllPatternsZeroHits()
    {
        using var testSolution = CreateSolution();
        var result = await RunAsync(testSolution.Solution, CreateConfig());

        Assert.NotNull(result.Payload);
        Assert.All(result.Payload!.Patterns, p => Assert.Equal(0, p.Occurrences));
        Assert.Equal(0, result.Payload.Summary.PatternsWithHits);
        Assert.Equal(0, result.Payload.Summary.TotalOccurrences);
        Assert.Contains("Keine.", result.Text!);
    }

    [Fact]
    public async Task BuildReportAsync_ScopeFilterMatchesNoFile_ReturnsExplicitMessageWithoutPayload()
    {
        const string source = "namespace Test; public sealed class Foo { }";
        using var testSolution = CreateSolution(("Foo.cs", source));
        var solution = testSolution.Solution;

        var result = await PatternDetectScanner.BuildReportAsync(new PatternDetectScannerParameters(
            Solution: solution,
            Config: CreateConfig(),
            Console: AiNetLinter.Output.LinterConsole.Instance,
            ScopeFilter: "DoesNotExistAnywhere",
            Patterns: PatternCatalog.Patterns,
            CancellationToken: CancellationToken.None));

        Assert.False(result.IsMalfunction);
        Assert.Null(result.Payload);
        Assert.Contains("Keine Dateien im Scope", result.Text!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildReportAsync_MoreHitsThanMaxResultsPerPattern_TruncatesItemsButKeepsFullOccurrenceCount()
    {
        var files = Enumerable.Range(0, 5)
            .Select(i => ($"Async{i}.cs", $@"
using System.Threading.Tasks;
namespace Test;
public sealed class Foo{i}
{{
    public async void Run() {{ await Task.Delay(0); }}
}}"))
            .ToArray();
        using var testSolution = CreateSolution(files);
        var solution = testSolution.Solution;

        var result = await PatternDetectScanner.BuildReportAsync(new PatternDetectScannerParameters(
            Solution: solution,
            Config: CreateConfig(),
            Console: AiNetLinter.Output.LinterConsole.Instance,
            ScopeFilter: null,
            Patterns: PatternCatalog.Patterns,
            CancellationToken: CancellationToken.None,
            MaxResultsPerPattern: 2));

        var asyncVoid = result.Payload!.Patterns.Single(p => p.Id == "async-void");
        Assert.Equal(5, asyncVoid.Occurrences);
        Assert.Equal(2, asyncVoid.Items.Count);
        Assert.Contains("Treffer gesamt", result.Text!, StringComparison.Ordinal);
        Assert.Contains("gezeigt", result.Text!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildReportAsync_PatternsSubsetRequested_OnlyRequestedPatternsInPayload()
    {
        using var testSolution = CreateSolution();
        var result = await RunAsync(
            testSolution.Solution,
            CreateConfig(),
            PatternCatalog.Patterns.Where(p => p.Id == "async-void").ToList());

        Assert.Single(result.Payload!.Patterns);
        Assert.Equal("async-void", result.Payload.Patterns.Single().Id);
    }

    [Fact]
    public async Task BuildReportAsync_LinterEngineThrows_ReturnsMalfunctionWithContext()
    {
        using var faulty = new FaultingSolutionFixture();
        var solution = faulty.Solution;

        var result = await PatternDetectScanner.BuildReportAsync(new PatternDetectScannerParameters(
            Solution: solution,
            Config: CreateConfig(),
            Console: AiNetLinter.Output.LinterConsole.Instance,
            ScopeFilter: null,
            Patterns: PatternCatalog.Patterns,
            CancellationToken: CancellationToken.None));

        Assert.True(result.IsMalfunction);
        Assert.Null(result.Payload);
        Assert.NotNull(result.Context);
        Assert.Contains("Simulierter Lesefehler", result.Context, StringComparison.Ordinal);
    }

    private static async Task<PatternDetectResult> RunAsync(
        (string FileName, string Source) file, Config config, IReadOnlyList<PatternDefinition>? patterns = null)
    {
        using var testSolution = CreateSolution(file);
        return await RunAsync(testSolution.Solution, config, patterns);
    }

    private static async Task<PatternDetectResult> RunAsync(
        Solution solution, Config config, IReadOnlyList<PatternDefinition>? patterns = null)
    {
        return await PatternDetectScanner.BuildReportAsync(new PatternDetectScannerParameters(
            Solution: solution,
            Config: config,
            Console: AiNetLinter.Output.LinterConsole.Instance,
            ScopeFilter: null,
            Patterns: patterns ?? PatternCatalog.Patterns,
            CancellationToken: CancellationToken.None));
    }

    private static Config CreateConfig() => TestHelper.CreateDefaultConfig() with { Global = QuietGlobal() };

    /// <summary>Deaktiviert alle vom pattern_detect-Scope unabhaengigen Regeln, damit jeder Test
    /// nur den beabsichtigten Pattern-Treffer produziert (analog zu MaxPublicMembersPerTypeTests/
    /// SilentCatchAllowedTypesTests). BanAsyncVoid, EnforceNoSilentCatch, AvoidExcessiveMiddleMen
    /// bleiben auf ihren Code-Defaults (alle drei bereits <see langword="true"/>).</summary>
    private static GlobalConfig QuietGlobal() => new()
    {
        EnforceSealedClasses = false,
        AllowDynamic = false,
        AllowOutParameters = false,
        EnforcePascalCase = false,
        EnforceXmlDocumentation = false,
        EnforceSemanticNaming = false,
        EnforceNullableEnable = false,
        EnforceValueObjectContracts = false,
        EnforceExplicitStateImmutability = false,
        PreventContextDependentOverloads = false,
        EnforceNamespaceDirectoryMapping = false,
        DetectAndBanPhantomDependencies = false,
    };

    private static RoslynTestSolution CreateSolution(params (string fileName, string content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\PatternDetectScannerTests.slnx",
            new ProjectSpec("TestProject", files, VirtualProjectDirectory: "."));

}
