#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.TestContext;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.TestContext;

[Trait("Category", "Component")]
public sealed partial class GetTestContextToolTests
{
    private static McpCodeGraphServer CreateServer(Solution? solution = null) =>
        new(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                Config: new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
                ReadOnlySolutionSnapshot: solution)));

    private static RoslynTestSolution CreateTestScenario() => RoslynTestSolutionFactory.CreateSolution(
        @"C:\virtual\TestContextSolution.slnx",
        new ProjectSpec("CoreLib", [
            ("Calculator.cs", """
                namespace CoreLib;

                public class Calculator
                {
                    public int Add(int a, int b) => a + b;
                    public int Multiply(int a, int b) => a * b;
                }
                """),
            ("UntestedService.cs", """
                namespace CoreLib;

                public class UntestedService
                {
                    public void Process() { }
                }
                """),
            ("WorkerService.cs", """
                namespace CoreLib;

                public class WorkerService
                {
                    public void DoWork() { }
                }
                """),
            ("TypeofTarget.cs", """
                namespace CoreLib;

                public class TypeofTarget
                {
                    public void Run() { }
                }
                """)
        ], VirtualProjectDirectory: "src/CoreLib"),
        new ProjectSpec("CoreLib.Tests", [
            ("CalculatorTests.cs", """
                namespace CoreLib.Tests;

                public class CalculatorTests
                {
                    [Xunit.Fact]
                    public void Add_ReturnsSum()
                    {
                        var calc = new CoreLib.Calculator();
                        _ = calc.Add(1, 2);
                    }

                    [Xunit.Fact]
                    public void Multiply_ReturnsProduct()
                    {
                        var calc = new CoreLib.Calculator();
                        _ = calc.Multiply(2, 3);
                    }
                }
                """),
            ("WorkerCoversTests.cs", """
                // @covers WorkerService
                namespace CoreLib.Tests;

                public class WorkerCoversTests
                {
                    [Xunit.Fact]
                    public void Execute_CoversWorker()
                    {
                    }
                }
                """),
            ("TypeofReferencerTests.cs", """
                namespace CoreLib.Tests;

                public class TypeofReferencerTests
                {
                    private static readonly System.Type Target = typeof(CoreLib.TypeofTarget);

                    [Xunit.Fact]
                    public void VerifyTarget()
                    {
                    }
                }
                """)
        ], VirtualProjectDirectory: "tests/CoreLib.Tests")
    );

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("Calculator"), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_EmptySymbol_ReturnsRecoverableInvalidArgument()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions(""), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbol_ReturnsSymbolNotFound()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("NonExistentClass"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NamingConvention_ReturnsMatchingTestsAndCommand()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("Calculator"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Test-Kontext (statische Testkandidaten): CoreLib.Calculator", textContent.Text);
        Assert.Contains("statische Testzuordnung", textContent.Text);
        Assert.Contains("CalculatorTests", textContent.Text);
        Assert.DoesNotContain("Add_ReturnsSum", textContent.Text);
        Assert.DoesNotContain("Multiply_ReturnsProduct", textContent.Text);
        Assert.Contains("dotnet test", textContent.Text);
        Assert.Contains("--filter FullyQualifiedName~CalculatorTests", textContent.Text);

        Assert.Contains("typeNamingConvention", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("confidence=low", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("2 Tests auf Klassenebene", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeConvention_RendersClassEvidenceWithoutMethodCount()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(
            state, new TestContextOptions("Calculator"), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("typeNamingConvention", text, StringComparison.Ordinal);
        Assert.Contains("confidence=low", text, StringComparison.Ordinal);
        Assert.Contains("2 Tests auf Klassenebene", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0 von 2 Testmethoden", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatReport_MixedEvidenceUsesNeutralAggregateLabel()
    {
        var payload = new TestContextPayload(
            "CoreLib.Calculator",
            "NamedType",
            "src/CoreLib/Calculator.cs",
            3,
            2,
            [
                new StaticTestCandidateFile("tests/CalculatorTests.cs", "CalculatorTests", "Unit",
                    TestCoverageMatchReasons.DirectMemberMatch, ["Add_Works"], 1),
                new StaticTestCandidateFile("tests/CalculatorMoreTests.cs", "CalculatorMoreTests", "Unit",
                    TestCoverageMatchReasons.NamingConventionMatch, [], 2, EvidenceKind: "typeNamingConvention",
                    Confidence: "low", TotalTestCount: 2)
            ],
            [],
            false,
            false,
            ReturnedTestFiles: 2,
            ReturnedTestMethods: 1);

        var text = TestContextFormatter.FormatReport(payload);

        Assert.Contains("Evidenztreffer", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1 von 3 Testmethoden", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SourceHandoffIdFromFindSymbol_ReturnsTestContext()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var discovery = await AiNetLinter.Mcp.Tools.SymbolGraph.FindSymbolTool.ExecuteAsync(
            state,
            ["Calculator"],
            kind: "class",
            maxResults: 50,
            CancellationToken.None);
        var handoffId = ExtractHandoffId(Assert.IsType<TextContentBlock>(Assert.Single(discovery.Content)).Text);

        var result = await GetTestContextTool.ExecuteAsync(
            state,
            new TestContextOptions(handoffId),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CoreLib.Calculator", text, System.StringComparison.Ordinal);
        Assert.Contains("CalculatorTests", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DirectMethod_ReturnsOnlyMatchingMethod()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("Calculator.Add"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Add_ReturnsSum", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Multiply_ReturnsProduct", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CoversComment_ReturnsMatchingTests()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("WorkerService"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("WorkerCoversTests", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeofReference_ReturnsMatchingTests()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("TypeofTarget"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("TypeofReferencerTests", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NoStaticAssignment_ReturnsNoticeAndPathSuggestion()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("UntestedService"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("In der statischen Test-Zuordnung wurden für dieses Symbol keine direkten Tests gefunden", textContent.Text);
        Assert.Contains("Empfehlung:", textContent.Text);
        Assert.Contains("tests/CoreLib.Tests/UntestedServiceTests.cs", textContent.Text);
        Assert.DoesNotContain("vollständig", textContent.Text, StringComparison.OrdinalIgnoreCase);

    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifier_Works()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions(SymbolIdentifier: "Calculator"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("CoreLib.Calculator", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResults_TruncatesAndSetsFlag()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("Calculator", MaxResults: 0), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("vollständig", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatReport_Truncated_DoesNotClaimCompleteScopeAndMirrorsMetadata()
    {
        var payload = new TestContextPayload(
            "CoreLib.Calculator",
            "NamedType",
            "src/CoreLib/Calculator.cs",
            4,
            2,
            [],
            [],
            false,
            true,
            Completeness: "truncated",
            ReturnedTestFiles: 1,
            ReturnedTestMethods: 3,
            TruncatedBy: ["maxResults", "responseBudget"],
            NextStep: "Abschnitt testContext: maxResults erhöhen.");

        var text = TestContextFormatter.FormatReport(payload);

        Assert.Contains("**Status:** `truncated`", text, StringComparison.Ordinal);
        Assert.Contains("Counts:", text, StringComparison.Ordinal);
        Assert.Contains("1 von 2 Testdateien", text, StringComparison.Ordinal);
        Assert.Contains("**TruncatedBy:** `maxResults, responseBudget`", text, StringComparison.Ordinal);
        Assert.Contains("Abschnitt testContext: maxResults erhöhen.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("vollständig", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResults_ReportsStaticCandidateTruncationAndSafeNextStep()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\TestContextTruncation.slnx",
            new ProjectSpec("CoreLib", [
                ("Calculator.cs", "namespace CoreLib; public class Calculator { public int Add() => 1; }")
            ], VirtualProjectDirectory: "src/CoreLib"),
            new ProjectSpec("CoreLib.Tests", [
                ("CalculatorTestsA.cs", "namespace CoreLib.Tests; public class CalculatorTestsA { [Xunit.Fact] public void Add_A() { new CoreLib.Calculator().Add(); } }")
            ], VirtualProjectDirectory: "tests/CoreLib.Tests"),
            new ProjectSpec("CoreLib.MoreTests", [
                ("CalculatorTestsB.cs", "namespace CoreLib.MoreTests; public class CalculatorTestsB { [Xunit.Fact] public void Add_B() { new CoreLib.Calculator().Add(); } }")
            ], VirtualProjectDirectory: "tests/CoreLib.MoreTests"));

        var state = CreateServer(solutionOwner.Solution);
        var result = await GetTestContextTool.ExecuteAsync(
            state, new TestContextOptions("Calculator.Add", MaxResults: 1), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Status:** `truncated`", text, StringComparison.Ordinal);
        Assert.Contains("maxResults", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeAndGeneratedFilterSelectTestEvidenceWithoutFallbackToAll()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\TestContextScope.slnx",
            new ProjectSpec("CoreLib", [("Calculator.cs", "namespace CoreLib; public class Calculator { public int Add() => 1; }")], VirtualProjectDirectory: "src/CoreLib"),
            new ProjectSpec("CoreLib.Tests", [
                ("CalculatorTests.cs", "namespace CoreLib.Tests; public class CalculatorTests { [Xunit.Fact] public void Add_Works() { _ = new CoreLib.Calculator().Add(); } }"),
                ("CalculatorGeneratedTests.cs", "// <auto-generated />\nnamespace CoreLib.Tests; public class CalculatorGeneratedTests { [Xunit.Fact] public void Add_Generated() { _ = new CoreLib.Calculator().Add(); } }")
            ], VirtualProjectDirectory: "tests/CoreLib.Tests"));
        var state = CreateServer(scenario.Solution);

        var production = await GetTestContextTool.ExecuteAsync(state,
            new TestContextOptions("Calculator.Add", Scope: new McpScopeInput(McpScopeType.Production, false)), CancellationToken.None);
        var tests = await GetTestContextTool.ExecuteAsync(state,
            new TestContextOptions("Calculator.Add", Scope: new McpScopeInput(McpScopeType.Tests, false)), CancellationToken.None);
        var allWithGenerated = await GetTestContextTool.ExecuteAsync(state,
            new TestContextOptions("Calculator.Add", Scope: new McpScopeInput(McpScopeType.All, true)), CancellationToken.None);

        var productionText = Assert.IsType<TextContentBlock>(Assert.Single(production.Content)).Text;
        var testsText = Assert.IsType<TextContentBlock>(Assert.Single(tests.Content)).Text;
        var generatedText = Assert.IsType<TextContentBlock>(Assert.Single(allWithGenerated.Content)).Text;
        Assert.Contains("keine direkten Tests", productionText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CalculatorTests", testsText, StringComparison.Ordinal);
        Assert.DoesNotContain("CalculatorGeneratedTests", testsText, StringComparison.Ordinal);
        Assert.Contains("CalculatorGeneratedTests", generatedText, StringComparison.Ordinal);
    }

    private static string ExtractHandoffId(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, "handoffId: `(?<id>s:[^`]+)`");
        Assert.True(match.Success, text);
        return match.Groups["id"].Value;
    }
}
