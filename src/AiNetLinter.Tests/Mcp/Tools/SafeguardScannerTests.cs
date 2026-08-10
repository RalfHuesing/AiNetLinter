#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Safeguard;
using AiNetLinter.Models;
using AiNetLinter.Tests;
using AiNetLinter.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="SafeguardScanner"/>. Etabliert ein neues Scanner-Test-Pattern (es
/// existiert keine dedizierte Test-Datei fuer <c>GetViolationsScanner</c>) und deckt den
/// deterministischen Score-Pfad, Threshold-Logik, Edge-Cases und den Malfunction-Pfad ab.
/// </summary>
[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
public sealed class SafeguardScannerTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public SafeguardScannerTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ComputeScoreAsync_EmptySolution_ReturnsHighScore()
    {
        var solution = CreateAdhocSolution();
        var config = CreateConfig();
        var parameters = CreateParameters(solution, config);

        var result = await SafeguardScanner.ComputeScoreAsync(parameters);

        Assert.False(result.IsMalfunction);
        Assert.NotNull(result.Score);
        Assert.True(result.Score!.Score >= 9.0, $"Score {result.Score.Score} sollte >= 9.0 sein.");
        Assert.True(result.Score.Passed);
        Assert.Empty(result.Score.Violations);
    }

    [Fact]
    public async Task ComputeScoreAsync_SingleViolation_LowersScoreBelowThreshold()
    {
        const string source = @"
namespace Test;
public class Greeter
{
    // bewusst keine sealed-Markierung -> EnforceSealedClasses (Default: error, Severity 2)
    public string Hello() => ""hi"";
}";
        var solution = CreateAdhocSolution(("Greeter.cs", source));
        var config = CreateConfig();
        var parameters = CreateParameters(solution, config);

        var result = await SafeguardScanner.ComputeScoreAsync(parameters);

        Assert.False(result.IsMalfunction);
        Assert.NotNull(result.Score);
        Assert.NotEmpty(result.Score!.Violations);
        Assert.True(result.Score.Score < 8.0, $"Score {result.Score.Score} sollte < 8.0 sein.");
        Assert.False(result.Score.Passed);
    }

    [Fact]
    public async Task ComputeScoreAsync_KnownFixture_HasAtLeastOneViolation()
    {
        var solution = _fixture.Catalog.Solution;
        var config = CreateConfig();
        var parameters = CreateParameters(solution, config);

        var result = await SafeguardScanner.ComputeScoreAsync(parameters);

        Assert.False(result.IsMalfunction);
        Assert.NotNull(result.Score);
        Assert.InRange(result.Score!.Score, 0.0, 10.0);
        Assert.NotEmpty(result.Score.Violations);
        // ViolationTrigger provoziert deterministisch eine EnforceSealedClasses-Meldung.
        Assert.Contains(result.Score.Violations, v =>
            v.FilePath.Contains("ViolationTrigger", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ComputeScoreAsync_HighScoreAboveThreshold_Passes()
    {
        const string source = @"
namespace Test;
public sealed class A { public int X() => 1; }
public sealed class B { public int Y() => 2; }
public sealed class C { public int Z() => 3; }
public sealed class D { public int W() => 4; }";
        var solution = CreateAdhocSolution(("Mini.cs", source));
        var config = CreateConfig();
        var parameters = CreateParameters(solution, config);

        var result = await SafeguardScanner.ComputeScoreAsync(parameters);

        Assert.False(result.IsMalfunction);
        Assert.NotNull(result.Score);
        Assert.True(result.Score!.Score > 8.0, $"Score {result.Score.Score} sollte > 8.0 sein.");
        Assert.True(result.Score.Passed);
    }

    [Fact]
    public async Task ComputeScoreAsync_LowScoreBelowThreshold_Fails()
    {
        // Sehr lange Klasse: viele Methoden, daher viele potentielle CC- und Sealed-Verletzungen.
        // Erfuellt mit kleinen Limits in der Config mehrere Lint-Regeln gleichzeitig.
        var methods = string.Join("\n",
            Enumerable.Range(0, 30).Select(i => $"    public int M{i}() {{ if ({i} > 0) return {i}; return 0; }}"));
        var source = $@"
namespace Test;
public class Giant
{{
{methods}
}}";
        var solution = CreateAdhocSolution(("Giant.cs", source));
        var config = CreateConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxLineCount = 10,
                MaxMethodLineCount = 1,
                MaxMethodParameterCount = 4,
                MaxCyclomaticComplexity = 1,
                MaxCognitiveComplexity = 1,
                MaxInheritanceDepth = 0,
                MaxAIContextFootprint = 5,
            },
        };
        var parameters = CreateParameters(solution, config);

        var result = await SafeguardScanner.ComputeScoreAsync(parameters);

        Assert.False(result.IsMalfunction);
        Assert.NotNull(result.Score);
        Assert.True(result.Score!.Score < 8.0, $"Score {result.Score.Score} sollte < 8.0 sein.");
        Assert.False(result.Score.Passed);
        Assert.NotEmpty(result.Score.Remediation.ActionableSteps);
    }

    [Fact]
    public async Task ComputeScoreAsync_ThresholdLogic_ScoreEqualToThreshold_Passes()
    {
        // Threshold-Logik: passed = (score >= threshold). Wir setzen Threshold = 0.0, dann
        // muss jeder nicht-negative Score passen (selbst mit Violations, solange Clamp nicht
        // unter 0 faellt).
        const string source = @"
namespace Test;
public class Greeter { public string Hello() => ""hi""; }";
        var solution = CreateAdhocSolution(("Greeter.cs", source));
        var config = CreateConfig();
        var parameters = new SafeguardScannerParameters(
            Solution: solution, Config: config, Console: NullConsole.Instance,
            ScopeFilter: null, CancellationToken: CancellationToken.None,
            MinScoreThreshold: 0.0);

        var result = await SafeguardScanner.ComputeScoreAsync(parameters);

        Assert.False(result.IsMalfunction);
        Assert.NotNull(result.Score);
        Assert.Equal(0.0, result.Score!.Threshold);
        Assert.True(result.Score.Score >= 0.0);
        Assert.True(result.Score.Passed);
    }

    [Fact]
    public async Task ComputeScoreAsync_Determinismus_ZweiLaufeIdentischerScore()
    {
        var solution = _fixture.Catalog.Solution;
        var config = CreateConfig();
        var parameters1 = CreateParameters(solution, config);
        var parameters2 = CreateParameters(solution, config);

        var first = await SafeguardScanner.ComputeScoreAsync(parameters1);
        var second = await SafeguardScanner.ComputeScoreAsync(parameters2);

        Assert.False(first.IsMalfunction);
        Assert.False(second.IsMalfunction);
        Assert.NotNull(first.Score);
        Assert.NotNull(second.Score);
        Assert.Equal(first.Score!.Score, second.Score!.Score);
        Assert.Equal(first.Score.Summary, second.Score.Summary);
        Assert.Equal(first.Score.Violations.Count, second.Score.Violations.Count);
        Assert.Equal(
            string.Join("|", first.Score.Violations.Select(v => $"{v.FilePath}:{v.LineNumber}:{v.RuleName}")),
            string.Join("|", second.Score.Violations.Select(v => $"{v.FilePath}:{v.LineNumber}:{v.RuleName}")));
    }

    [Fact]
    public async Task ComputeScoreAsync_LinterEngineThrows_ReturnsMalfunctionWithContext()
    {
        // Regressionstest analog GetViolationsToolTests: ein ThrowingTextLoader deterministisch
        // simuliert eine LinterEngine-Malfunction. SafeguardScanner faengt die Exception ab
        // und liefert IsMalfunction=true mit der rohen Exception-Message im Context-Feld.
        var probeDir = Path.Combine(Path.GetTempPath(), "ainetlinter-safeguard-malfunction-" + Guid.NewGuid().ToString("N"));
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

            var config = CreateConfig();
            var parameters = CreateParameters(solution, config);

            var result = await SafeguardScanner.ComputeScoreAsync(parameters);

            Assert.True(result.IsMalfunction);
            Assert.Null(result.Score);
            Assert.NotNull(result.Context);
            Assert.Contains("Simulierter Lesefehler", result.Context, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(probeDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetCompilationWithRetryAsync_TransientFailuresThenSuccess_ReturnsCompilation()
    {
        // Regressionstest fuer den urspruenglichen Live-Repo-Determinismus-Bug: unter paralleler
        // Last kann GetCompilationAsync transient fehlschlagen. Die ersten (CompilationRetryAttempts
        // - 1) Aufrufe simulieren das per Exception, der letzte Versuch liefert eine echte Compilation.
        var expected = CSharpCompilation.Create("RetrySuccess");
        var callCount = 0;

        Func<CancellationToken, Task<Compilation?>> flaky = _ =>
        {
            callCount++;
            if (callCount < SafeguardScanner.CompilationRetryAttempts)
            {
                throw new InvalidOperationException($"Simulierter transienter Fehlschlag #{callCount}.");
            }
            return Task.FromResult<Compilation?>(expected);
        };

        var result = await SafeguardScanner.GetCompilationWithRetryAsync(flaky, "FlakyProject", CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(SafeguardScanner.CompilationRetryAttempts, callCount);
    }

    [Fact]
    public async Task GetCompilationWithRetryAsync_AlwaysThrows_ThrowsSafeguardCompilationExceptionWithInnerException()
    {
        // Dauerhafter Fehlschlag (kein transientes Problem) muss NICHT mehr lautlos null liefern
        // (das wuerde die Klasse still aus der Score-Aggregation ausschliessen), sondern als echte
        // Malfunction durchgereicht werden.
        var callCount = 0;
        var innerException = new InvalidOperationException("Dauerhafter Compile-Fehler.");

        Func<CancellationToken, Task<Compilation?>> alwaysFails = _ =>
        {
            callCount++;
            throw innerException;
        };

        var ex = await Assert.ThrowsAsync<SafeguardCompilationException>(
            () => SafeguardScanner.GetCompilationWithRetryAsync(alwaysFails, "PermanentlyBrokenProject", CancellationToken.None));

        Assert.Equal(SafeguardScanner.CompilationRetryAttempts, callCount);
        Assert.Same(innerException, ex.InnerException);
        Assert.Contains("PermanentlyBrokenProject", ex.Message, StringComparison.Ordinal);
        Assert.Contains(SafeguardScanner.CompilationRetryAttempts.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCompilationWithRetryAsync_AlwaysReturnsNullWithoutException_ThrowsSafeguardCompilationException()
    {
        // Laut Roslyn-Vertrag liefert GetCompilationAsync bei SupportsCompilation == true nie null,
        // aber die Retry-Logik behandelt diesen theoretischen Fall defensiv genauso wie eine Exception
        // (kein stilles Uebergehen), statt sich auf den Vertrag blind zu verlassen.
        var callCount = 0;
        Func<CancellationToken, Task<Compilation?>> alwaysNull = _ =>
        {
            callCount++;
            return Task.FromResult<Compilation?>(null);
        };

        var ex = await Assert.ThrowsAsync<SafeguardCompilationException>(
            () => SafeguardScanner.GetCompilationWithRetryAsync(alwaysNull, "NullReturningProject", CancellationToken.None));

        Assert.Equal(SafeguardScanner.CompilationRetryAttempts, callCount);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task GetCompilationWithRetryAsync_CancellationRequested_ThrowsOperationCanceledExceptionNotMalfunction()
    {
        // Cancellation ist kein Malfunction-Fall — muss durchgereicht werden, nicht in eine
        // SafeguardCompilationException uebersetzt werden (Konsistenz mit dem bestehenden
        // OperationCanceledException-Passthrough in ComputeScoreAsync).
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Func<CancellationToken, Task<Compilation?>> neverCalled = _ =>
            throw new InvalidOperationException("Sollte wegen Cancellation nicht aufgerufen werden.");

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SafeguardScanner.GetCompilationWithRetryAsync(neverCalled, "CancelledProject", cts.Token));
    }

    [Fact]
    public void BuildRemediation_UnknownRuleName_FallsBackToDefaultHint()
    {
        var unknown = new ViolationEntry(
            FilePath: @"C:\Test\Foo.cs",
            LineNumber: 1,
            RuleName: "DefinitelyUnknownRuleName_9999",
            Details: "irrelevant",
            Severity: "error",
            Guidance: "irrelevant");
        var config = CreateConfig();

        var hint = SafeguardScanner.BuildRemediation(new[] { unknown }, config);

        Assert.Equal("DefinitelyUnknownRuleName_9999", hint.TopIssue);
        Assert.NotEmpty(hint.ActionableSteps);
        Assert.Contains(hint.ActionableSteps, s => s.Contains("DefinitelyUnknownRuleName_9999", StringComparison.Ordinal));
        Assert.Contains(hint.ActionableSteps, s => s.Contains("Docs/configuration.md", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildRemediation_EmptyList_ReturnsEmptyRemediation()
    {
        var config = CreateConfig();

        var hint = SafeguardScanner.BuildRemediation(Array.Empty<ViolationEntry>(), config);

        Assert.Contains("Keine Lint-Verstoesse", hint.TopIssue, StringComparison.Ordinal);
        Assert.Empty(hint.ActionableSteps);
    }

    [Fact]
    public void BuildScoreResult_ClampsScoreToZeroAndTen()
    {
        // Eingabe-Sub-Score so konstruieren, dass der Roh-Wert > 10 ist (z. B. massiver
        // Sealed-Bonus + keine Penalties) bzw. < 0 ist (z. B. sehr viele Errors, kein
        // Sealed-Bonus). BuildScoreResult muss clampen.
        var config = CreateConfig();

        // Roh > 10: keine Violations, kein CC-Over, kein Footprint-Over, viele sealed Klassen.
        var manySealed = Enumerable.Range(0, 50)
            .Select(i => new ScannedClass($"C{i}", MaxCognitiveComplexity: 1, AIContextFootprint: 1, IsSealed: true))
            .ToList();
        var highRaw = SafeguardScanner.BuildScoreResult(new BuildScoreResultParameters(
            Violations: Array.Empty<RuleViolation>(),
            Classes: manySealed,
            Config: config,
            Threshold: 8.0,
            MaxRemediationEntries: 20));
        Assert.InRange(highRaw.Score, 0.0, 10.0);

        // Roh < 0: keine Klassen (Sealed-Bonus = 0), viele Errors, die den Score unter 0 druecken.
        var manyErrors = Enumerable.Range(0, 100)
            .Select(i => new RuleViolation
            {
                FilePath = $@"C:\Test\Foo{i}.cs",
                LineNumber = i,
                RuleName = "FakeRule",
                Details = "x",
                Guidance = "y",
                EffectiveSeverity = "error",
            })
            .ToList();
        var lowRaw = SafeguardScanner.BuildScoreResult(new BuildScoreResultParameters(
            Violations: manyErrors,
            Classes: Array.Empty<ScannedClass>(),
            Config: config,
            Threshold: 8.0,
            MaxRemediationEntries: 20));
        Assert.InRange(lowRaw.Score, 0.0, 10.0);
        Assert.False(lowRaw.Passed);
    }

    [Fact]
    public void SafeguardScannerParameters_DefaultThreshold_Is8()
    {
        var solution = CreateAdhocSolution();
        var config = CreateConfig();
        var parameters = new SafeguardScannerParameters(
            Solution: solution, Config: config, Console: NullConsole.Instance,
            ScopeFilter: null, CancellationToken: CancellationToken.None);

        Assert.Equal(SafeguardScanner.DefaultMinScoreThreshold, parameters.MinScoreThreshold);
        Assert.Equal(8.0, parameters.MinScoreThreshold);
        Assert.Equal(SafeguardScanner.DefaultMaxRemediationEntries, parameters.MaxRemediationEntries);
    }

    [Fact]
    public void SafeguardScannerParameters_ExplicitOverrides_AreRespected()
    {
        var solution = CreateAdhocSolution();
        var config = CreateConfig();
        var parameters = new SafeguardScannerParameters(
            Solution: solution, Config: config, Console: NullConsole.Instance,
            ScopeFilter: null, CancellationToken: CancellationToken.None,
            MinScoreThreshold: 5.5, MaxRemediationEntries: 7);

        Assert.Equal(5.5, parameters.MinScoreThreshold);
        Assert.Equal(7, parameters.MaxRemediationEntries);
    }

    private static Config CreateConfig() => TestHelper.CreateDefaultConfig();

    private static SafeguardScannerParameters CreateParameters(Solution solution, Config config)
        => new(Solution: solution, Config: config, Console: NullConsole.Instance,
               ScopeFilter: null, CancellationToken: CancellationToken.None);

    private static Solution CreateAdhocSolution(params (string fileName, string content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, file.content);
        }
        return solution;
    }

    /// <summary>
    /// Stiller Konsolen-Stub fuer Scanner-Tests. Verhindert, dass ein realer LinterEngine-Lauf
    /// in den Test-Output schreibt; gleichzeitig nimmt die Scanner-Signatur weiterhin einen
    /// <c>ILintConsole</c>-Parameter (Pattern-Konsistenz mit <c>GetViolationsScanner</c>).
    /// </summary>
    private sealed class NullConsole : AiNetLinter.Output.ILintConsole
    {
        public static readonly NullConsole Instance = new();
        public void WriteLine(string message) { }
        public void WriteError(string message) { }
    }

    /// <summary>
    /// Test-Fake: wirft beim Textzugriff eine unspezifische Exception, um eine echte
    /// LinterEngine-Malfunction deterministisch zu simulieren (analog zum
    /// <c>ThrowingTextLoader</c> in <c>GetViolationsToolTests</c>). IOException/UnauthorizedAccess
    /// werden von Roslyn intern abgefangen — daher der unspezifische Exception-Typ.
    /// </summary>
    private sealed class ThrowingTextLoader : TextLoader
    {
        public override Task<TextAndVersion> LoadTextAndVersionAsync(
            LoadTextOptions options, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulierter Lesefehler fuer Malfunction-Regressionstest.");
        }
    }
}
