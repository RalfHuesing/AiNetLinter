#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.PatternDetect;
using AiNetLinter.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="PatternDetectScanner"/> — deckt die Zuordnung je Pattern (1 Test pro
/// Pattern, siehe <see cref="PatternCatalog"/>) sowie Edge-Cases (0 Treffer, Scope ohne Treffer,
/// Trunkierung, Malfunction) ab. Kleine, gezielte In-Memory-Solutions (<see cref="AdhocWorkspace"/>)
/// statt der geteilten Live-Fixture — pro Test genau der Code, der die jeweilige Regel
/// deterministisch ausloest (Pattern 1:1 von <c>SafeguardScannerTests</c> uebernommen).
/// </summary>
[Trait("Category", "Unit")]
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
        var result = await RunAsync(CreateAdhocSolution(Path.GetTempPath()), CreateConfig());

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
        using var tempDir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(tempDir.Path, ("Foo.cs", source));

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
        using var tempDir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(tempDir.Path, files);

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
        var result = await RunAsync(
            CreateAdhocSolution(Path.GetTempPath()),
            CreateConfig(),
            PatternCatalog.Patterns.Where(p => p.Id == "async-void").ToList());

        Assert.Single(result.Payload!.Patterns);
        Assert.Equal("async-void", result.Payload.Patterns.Single().Id);
    }

    [Fact]
    public async Task BuildReportAsync_LinterEngineThrows_ReturnsMalfunctionWithContext()
    {
        var probeDir = Path.Combine(Path.GetTempPath(), "ainetlinter-patterndetect-malfunction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(probeDir);
        var faultyPath = Path.Combine(probeDir, "Faulty.cs");
        try
        {
            File.WriteAllText(faultyPath, "class Faulty {}");

            var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var projectInfo = ProjectInfo.Create(
                projectId, VersionStamp.Create(), "FaultyProject", "FaultyProject", LanguageNames.CSharp);
            var solution = workspace.CurrentSolution.AddProject(projectInfo);

            var documentId = DocumentId.CreateNewId(projectId);
            var documentInfo = DocumentInfo.Create(
                documentId, "Faulty.cs", filePath: faultyPath, loader: new ThrowingTextLoader());
            solution = solution.AddDocument(documentInfo);

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
        finally
        {
            Directory.Delete(probeDir, recursive: true);
        }
    }

    private static async Task<PatternDetectResult> RunAsync(
        (string FileName, string Source) file, Config config, IReadOnlyList<PatternDefinition>? patterns = null)
    {
        using var tempDir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(tempDir.Path, file);
        return await RunAsync(solution, config, patterns);
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

    /// <summary>
    /// Baut eine In-Memory-Solution mit auf der Platte real gespiegelten Quelldateien unter
    /// <paramref name="baseDir"/>. Ein rein virtuelles <c>filePath</c> reicht nicht: die
    /// <see cref="AiNetLinter.Core.LinterEngine"/> liest manche Checker-Pfade physisch von der
    /// Platte (nicht nur ueber den Roslyn-<see cref="SourceText"/>-Puffer) — ohne reale Datei
    /// schlaegt <see cref="PatternDetectScanner.BuildReportAsync"/> als Malfunction fehl
    /// ("Could not find a part of the path", empirisch in einem fruehen Testlauf hier gefunden).
    /// </summary>
    private static Solution CreateAdhocSolution(string baseDir, params (string fileName, string content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var taskAsm = MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId, VersionStamp.Create(), "TestProject", "TestProject", LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib, taskAsm })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        // SolutionInfo mit explizitem FilePath (statt des sonst leeren AdhocWorkspace-Defaults):
        // PatternDetectScanner leitet solutionDir aus solution.FilePath ab (identisches Muster wie
        // GetViolationsScanner) und ruft Path.GetRelativePath(solutionDir, ...) auf — bei
        // solution.FilePath == null faellt das auf "" zurueck, und GetRelativePath wirft bei leerem
        // relativeTo eine ArgumentException. In Produktion ist solution.FilePath immer gesetzt (echte
        // .sln/.slnx via MSBuildWorkspace), dieser Fallback wird dort nie erreicht — nur die
        // AdhocWorkspace-Testsolution hier braucht den expliziten Pfad.
        var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(), filePath: Path.Combine(baseDir, "Test.slnx"));
        var solution = workspace.AddSolution(solutionInfo).AddProject(projectInfo);
        foreach (var file in files)
        {
            var fullPath = Path.Combine(baseDir, file.fileName);
            File.WriteAllText(fullPath, file.content);

            var documentId = DocumentId.CreateNewId(projectId);
            // Explizites filePath (statt nur Name) noetig: PatternDetectScanner.BuildFileToProjectMap
            // (analog GetViolationsScanner) ueberspringt Documents mit FilePath == null komplett —
            // ohne echten Pfad landet keine Violation in der Scope-gefilterten Trefferliste, obwohl
            // LinterEngine sie durchaus findet.
            solution = solution.AddDocument(
                documentId, file.fileName, SourceText.From(file.content), filePath: fullPath);
        }
        return solution;
    }

    /// <summary>Erstellt ein eindeutiges Temp-Verzeichnis fuer die auf Platte gespiegelten
    /// Testdateien (siehe <see cref="CreateAdhocSolution"/>) und raeumt es beim Dispose wieder
    /// auf (best-effort, Fehler beim Aufraeumen werden verschluckt).</summary>
    private sealed class TempSourceDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ainetlinter-patterndetect-" + Guid.NewGuid().ToString("N"));

        public TempSourceDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    private sealed class ThrowingTextLoader : TextLoader
    {
        public override Task<TextAndVersion> LoadTextAndVersionAsync(
            LoadTextOptions options, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulierter Lesefehler fuer Malfunction-Regressionstest.");
        }
    }
}
