#nullable enable

using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FeatureContext;

[Trait("Category", "Component")]
public sealed class GetFeatureContextToolTests
{
    private readonly McpInMemoryTestContext _fixture = new();

    private static RoslynTestSolution CreateFullTestScenario() => RoslynTestSolutionFactory.CreateSolution(
        @"C:\virtual\FeatureContextSolution.slnx",
        new ProjectSpec("CoreLib", [
            ("Calculator.cs", """
                namespace CoreLib;

                public class Calculator
                {
                    public int Add(int a, int b)
                    {
                        if (a < 0) return b;
                        if (b < 0) return a;
                        return a + b;
                    }

                    public int Multiply(int x, int y) => x * y;
                }
                """),
            ("Consumer.cs", """
                namespace CoreLib;

                public class Consumer
                {
                    public void Run()
                    {
                        var calc = new Calculator();
                        _ = calc.Add(1, 2);
                    }

                    public void RunOther()
                    {
                        var calc = new Calculator();
                        _ = calc.Add(10, 20);
                    }
                }
                """)
        ], VirtualProjectDirectory: "src/CoreLib"),
        new ProjectSpec("CoreLib.Tests", [
            ("CalculatorTests.cs", """
                namespace CoreLib.Tests;

                public class CalculatorTests
                {
                    [Xunit.Fact]
                    public void Add_PositiveNumbers_ReturnsSum()
                    {
                        var calc = new CoreLib.Calculator();
                        _ = calc.Add(2, 3);
                    }

                    [Xunit.Fact]
                    public void Multiply_ReturnsProduct()
                    {
                        var calc = new CoreLib.Calculator();
                        _ = calc.Multiply(2, 3);
                    }
                }
                """)
        ], VirtualProjectDirectory: "tests/CoreLib.Tests")
    );

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetFeatureContextTool.ExecuteAsync(state, new FeatureContextOptions("Calculator.Add"), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_EmptySymbol_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await GetFeatureContextTool.ExecuteAsync(state, new FeatureContextOptions(""), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentSymbol_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetFeatureContextTool.ExecuteAsync(state, new FeatureContextOptions("NonExistentClass.NonExistentMethod"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidMethod_ReturnsAllFiveSections()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));

        var result = await GetFeatureContextTool.ExecuteAsync(state, new FeatureContextOptions("Calculator.Add"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        var text = textContent.Text;

        // 1. Deklaration
        Assert.Contains("# Feature-Kontext: CoreLib.Calculator.Add(int, int)", text);
        Assert.Contains("## 1. Symbol & Deklaration", text);
        Assert.Contains("Calculator.cs", text);
        Assert.Contains("Method", text);
        Assert.Contains("public int", text);

        // 2. Metriken
        Assert.Contains("## 2. Metriken & Budget (ainetlinter-rules.json)", text);
        Assert.Contains("Cyclomatic Complexity", text);
        Assert.Contains("Cognitive Complexity", text);
        Assert.Contains("Budget verbleibend", text);

        // 3. Callers
        Assert.Contains("## 3. Statische Referenzen/Call-Sites", text);
        Assert.Contains("Consumer.cs", text);
        Assert.Contains("Consumer.Run()", text);
        Assert.Contains("Consumer.RunOther()", text);

        // 4. Test-Kontext
        Assert.Contains("## 4. Test-Kontext (statische Testkandidaten", text);
        Assert.Contains("CalculatorTests.cs", text);
        Assert.Contains("Add_PositiveNumbers_ReturnsSum", text);

        // 5. Violations
        Assert.Contains("## 5. Offene Violations auf dieser Datei", text);

        // StructuredContent Pruefung
        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<FeatureContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(payload);
        Assert.Equal("Method", payload.Declaration.Kind);
        Assert.True(payload.Declaration.LineCount >= 3);
        Assert.NotNull(payload.Metrics);
        Assert.NotNull(payload.Callers);
        Assert.Equal(2, payload.Callers.TotalCallers);
        Assert.Equal("static-references/call-sites", payload.Callers.Semantics);
        Assert.All(payload.Callers.CallSites, caller =>
        {
            Assert.False(string.IsNullOrWhiteSpace(caller.CallerId));
            Assert.NotNull(caller.CallerLocation);
            Assert.True(caller.CallerLocation!.StartLine > 0);
            Assert.True(caller.CallerLocation.EndLine >= caller.CallerLocation.StartLine);
        });
        Assert.NotNull(payload.Tests);
        Assert.True(payload.Tests.TotalMatchingTests >= 1);
        Assert.Equal("static-test-candidates-only", payload.Tests.EvidenceBoundary);
        Assert.Contains("testContext", result.StructuredContent.Value.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("\"impact\"", result.StructuredContent.Value.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"callers\"", result.StructuredContent.Value.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("coverage", result.StructuredContent.Value.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(payload.Violations);
        Assert.Equal("complete", payload.Violations.Status);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRulesKeepsStaticSectionsAndDoesNotClaimCleanViolations()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var result = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions("Calculator.Add"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = JsonSerializer.Deserialize<FeatureContextPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Callers);
        Assert.NotNull(payload.Tests);
        Assert.Equal("not_configured", payload.MetricsStatus);
        Assert.Equal("not_configured", payload.Violations!.Status);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Keine Linter-Verstoesse", text, StringComparison.Ordinal);
        Assert.Contains("nicht bewertet", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithSymbolIdentifierProperty_ResolvesSameAsSymbol()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));

        var result = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(SymbolIdentifier: "Calculator.Add"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Feature-Kontext: CoreLib.Calculator.Add(int, int)", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidType_ReturnsTypeMetricsAndDeclaration()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));

        var result = await GetFeatureContextTool.ExecuteAsync(state, new FeatureContextOptions("Calculator"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        var text = textContent.Text;

        Assert.Contains("# Feature-Kontext: CoreLib.Calculator", text);
        Assert.Contains("NamedType", text);
        Assert.Contains("Type LOC", text);
        Assert.Contains("AI-Context-Footprint", text);

        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<FeatureContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(payload);
        Assert.Equal("NamedType", payload.Declaration.Kind);
        Assert.NotNull(payload.Metrics?.TypeMetrics);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludeSections_RespectsFlags()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));

        var options = new FeatureContextOptions(
            SymbolIdentifier: "Calculator.Add",
            IncludeCallers: false,
            IncludeTests: false,
            IncludeMetrics: false,
            IncludeViolations: false
        );

        var result = await GetFeatureContextTool.ExecuteAsync(state, options, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        var text = textContent.Text;

        Assert.Contains("## 1. Symbol & Deklaration", text);
        Assert.DoesNotContain("## 2. Metriken & Budget", text);
        Assert.DoesNotContain("## 3. Direkte Aufrufer", text);
        Assert.DoesNotContain("## 4. Test-Kontext", text);
        Assert.DoesNotContain("## 5. Offene Violations", text);

        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<FeatureContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(payload);
        Assert.Null(payload.Metrics);
        Assert.Null(payload.Callers);
        Assert.Null(payload.Tests);
        Assert.Null(payload.Violations);
    }

    [Fact]
    public async Task ExecuteAsync_MaxCallersTruncation_SetsTruncationFlagAndNote()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));

        var options = new FeatureContextOptions(
            SymbolIdentifier: "Calculator.Add",
            MaxCallers: 1
        );

        var result = await GetFeatureContextTool.ExecuteAsync(state, options, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        var text = textContent.Text;

        Assert.Contains("Zeige 1 von 2 statischen Referenzen", text);

        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<FeatureContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(payload?.Callers);
        Assert.True(payload.Callers.IsTruncated);
        Assert.Single(payload.Callers.CallSites);
        Assert.Equal(2, payload.Callers.TotalCallers);
        Assert.Equal(new[] { "maxCallers" }, payload.Callers.TruncatedBy);
    }

    [Fact]
    public async Task ExecuteAsync_ByDocCommentId_ResolvesSymbol()
    {
        var state = _fixture.CreateServer();

        var result = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions("T:SymbolGraphMini.Greeter"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Feature-Kontext: SymbolGraphMini.Greeter", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ByLinePosition_ResolvesSymbol()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));

        var result = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions("src/CoreLib/Calculator.cs:5"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Add", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_PropertySymbol_ReturnsPropertyMetrics()
    {
        var state = _fixture.CreateServer();

        var result = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions("Greeter.Prefix"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Property", textContent.Text);
        Assert.Contains("Prefix", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDoesNotReturnPartialPayload()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GetFeatureContextTool.ExecuteAsync(state, new FeatureContextOptions("Calculator.Add"), cancellation.Token));
    }

    [Fact]
    public async Task CallerSearch_CancellationIsForwardedToRoslyn()
    {
        using var scenario = CreateFullTestScenario();
        var project = scenario.Solution.Projects.Single(p => p.Name == "CoreLib");
        var compilation = await project.GetCompilationAsync();
        var calculator = compilation!.GetTypeByMetadataName("CoreLib.Calculator")!;
        var symbol = calculator.GetMembers("Add").Single();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DiffImpactAnalyzer.FindCallSiteEntriesAsync(symbol, scenario.Solution, cancellation.Token));
    }

    [Fact]
    public void FormatReport_DoesNotPresentUnavailableViolationsAsEmpty()
    {
        var declaration = new SymbolDeclarationDto(
            "Missing", "Method", "public", "Missing.cs", 1, 1, 1, null, "void", [], null);
        var payload = new FeatureContextPayload(
            declaration,
            null,
            null,
            null,
            new ViolationsReportDto(
                0, 0, [], false, FeatureContextStatus.NotDecidable,
                FeatureContextReasonCodes.SourceFileUnavailable));

        var text = FeatureContextFormatter.FormatReport(payload);

        Assert.Contains("Status: not_decidable", text, StringComparison.Ordinal);
        Assert.Contains("ReasonCode: `source-file-unavailable`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Linter-Verstoesse", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompleteness_SectionFailureMapsRootToPartialWithCauseAndNextStep()
    {
        var declaration = new SymbolDeclarationDto(
            "Broken", "Method", "public", "Broken.cs", 1, 1, 1, null, "void", [], null);
        var violations = new ViolationsReportDto(
            0,
            0,
            [],
            false,
            FeatureContextStatus.Error,
            FeatureContextReasonCodes.ViolationsScanFailed,
            [],
            "Abschnitt violations: den Lint-Abschnitt erneut anfordern und den Workspace-Fehler prüfen.");

        var completeness = FeatureContextScanner.ResolveCompleteness(
            FeatureContextStatus.Complete,
            callers: null,
            tests: null,
            violations: violations);
        var payload = new FeatureContextPayload(
            declaration,
            null,
            null,
            null,
            violations,
            Completeness: completeness,
            NextStep: violations.NextStep);
        var text = FeatureContextFormatter.FormatReport(payload);

        Assert.Equal(FeatureContextStatus.Partial, completeness);
        Assert.Contains("**Composite-Completeness:** `partial`", text, StringComparison.Ordinal);
        Assert.Contains("ReasonCode: `violations-scan-failed`", text, StringComparison.Ordinal);
        Assert.Contains("Workspace-Fehler prüfen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("**Composite-Completeness:** `error`", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_StaticReferencesAreNotDescribedAsRuntimeCoverage()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));

        var result = await GetFeatureContextTool.ExecuteAsync(
            state,
            new FeatureContextOptions("Calculator.Add", IncludeTests: false, IncludeMetrics: false, IncludeViolations: false),
            CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Statische Referenzen/Call-Sites", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Laufzeit-Coverage", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ClassTarget_ReturnsMemberSignaturesInDeclaration()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));

        var result = await GetFeatureContextTool.ExecuteAsync(
            state,
            new FeatureContextOptions("CoreLib.Calculator", IncludeTests: false, IncludeMetrics: false, IncludeViolations: false),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Member", text, StringComparison.Ordinal);
        Assert.Contains("Add", text, StringComparison.Ordinal);
        Assert.Contains("Multiply", text, StringComparison.Ordinal);

        var declaration = result.StructuredContent!.Value.GetProperty("declaration");
        Assert.True(declaration.TryGetProperty("members", out var membersProp));
        var members = membersProp.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.NotNull(members);
        Assert.Contains(members, m => m!.Contains("Add", StringComparison.Ordinal));
        Assert.Contains(members, m => m!.Contains("Multiply", StringComparison.Ordinal));
    }
}
