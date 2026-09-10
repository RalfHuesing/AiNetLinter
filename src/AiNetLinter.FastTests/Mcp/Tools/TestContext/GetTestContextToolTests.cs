#nullable enable

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.TestContext;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.TestContext;

[Trait("Category", "Component")]
public sealed class GetTestContextToolTests
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
        Assert.Contains("Add_ReturnsSum", textContent.Text);
        Assert.Contains("Multiply_ReturnsProduct", textContent.Text);
        Assert.Contains("dotnet test", textContent.Text);
        Assert.Contains("--filter FullyQualifiedName~CalculatorTests", textContent.Text);

        Assert.NotNull(result.StructuredContent);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default)!;

        Assert.NotNull(structured);
        Assert.Equal("CoreLib.Calculator", structured.TargetSymbol);
        Assert.False(structured.IsUntested);
        Assert.Equal(2, structured.TotalMatchingTests);
        var testFile = Assert.Single(structured.TestFiles);
        Assert.Equal("CalculatorTests", testFile.TestClassName);
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
        var handoffId = discovery.StructuredContent!.Value
            .GetProperty("results")[0]
            .GetProperty("matches")[0]
            .GetProperty("id")
            .GetString();
        Assert.StartsWith("s:", handoffId, System.StringComparison.Ordinal);

        var result = await GetTestContextTool.ExecuteAsync(
            state,
            new TestContextOptions(handoffId),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal("CoreLib.Calculator", payload!.TargetSymbol);
        Assert.Equal(2, payload.TotalMatchingTests);
    }

    [Fact]
    public async Task ExecuteAsync_DirectMethod_ReturnsOnlyMatchingMethod()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("Calculator.Add"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default)!;

        Assert.NotNull(structured);
        Assert.False(structured.IsUntested);
        Assert.Equal(1, structured.TotalMatchingTests);
        var testFile = Assert.Single(structured.TestFiles);
        Assert.Contains("Add_ReturnsSum", testFile.TestMethods);
        Assert.DoesNotContain("Multiply_ReturnsProduct", testFile.TestMethods);
    }

    [Fact]
    public async Task ExecuteAsync_CoversComment_ReturnsMatchingTests()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("WorkerService"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default)!;

        Assert.NotNull(structured);
        Assert.False(structured.IsUntested);
        Assert.Equal(1, structured.TotalMatchingTests);
        var testFile = Assert.Single(structured.TestFiles);
        Assert.Equal("WorkerCoversTests", testFile.TestClassName);
    }

    [Fact]
    public async Task ExecuteAsync_TypeofReference_ReturnsMatchingTests()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("TypeofTarget"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default)!;

        Assert.NotNull(structured);
        Assert.False(structured.IsUntested);
        Assert.Equal(1, structured.TotalMatchingTests);
        var testFile = Assert.Single(structured.TestFiles);
        Assert.Equal("TypeofReferencerTests", testFile.TestClassName);
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

        Assert.NotNull(result.StructuredContent);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default)!;

        Assert.NotNull(structured);
        Assert.True(structured.IsUntested);
        Assert.Equal("empty", structured.Completeness);
        Assert.Equal("static-test-candidates-only", structured.EvidenceBoundary);
        Assert.Equal(0, structured.TotalMatchingTests);
        Assert.Empty(structured.TestFiles);
        Assert.Equal("tests/CoreLib.Tests/UntestedServiceTests.cs", structured.SuggestedTestFilePath);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifier_Works()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions(SymbolIdentifier: "Calculator"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default)!;

        Assert.NotNull(structured);
        Assert.Equal("CoreLib.Calculator", structured.TargetSymbol);
        Assert.Equal(2, structured.TotalMatchingTests);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResults_TruncatesAndSetsFlag()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("Calculator", MaxResults: 0), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default)!;

        Assert.NotNull(structured);
        Assert.Equal(2, structured.TotalMatchingTests);
        Assert.Single(structured.TestFiles);
        Assert.Equal("complete", structured.Completeness);
        Assert.Empty(structured.TruncatedBy!);
        Assert.Equal(1, structured.ReturnedTestFiles);
        Assert.Null(structured.NextStep);
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

        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        Assert.Equal("truncated", structured.Completeness);
        Assert.Equal(new[] { "maxResults" }, structured.TruncatedBy);
        Assert.Equal(2, structured.TotalTestFiles);
        Assert.Equal(1, structured.ReturnedTestFiles);
        Assert.Contains("testContext", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
        Assert.Contains("maxResults erhöhen", structured.NextStep, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuredStaticCandidateStatus_IsProjectedBySharedNavigation()
    {
        using var tempDir = TestTempDirectory.Create("test-context-navigation-");
        var solutionPath = tempDir.CreateFile("workspace.slnx", string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);
        var payload = new TestContextPayload(
            "CoreLib.Calculator",
            "NamedType",
            "src/CoreLib/Calculator.cs",
            2,
            2,
            [],
            [],
            false,
            true,
            Completeness: "truncated",
            ReturnedTestFiles: 0,
            ReturnedTestMethods: 0,
            TruncatedBy: ["maxResults"],
            NextStep: "Abschnitt testContext: maxResults erhöhen.");

        var result = McpToolResults.WithNavigation(McpToolResults.Text("statische Testkandidaten", payload), target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("truncated", navigation.GetProperty("completeness").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
        Assert.Contains("testContext", navigation.GetProperty("next").GetProperty("action").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NUnitAndMSTestCategories_DetectedCorrectly()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\CustomFrameworkSolution.slnx",
            new ProjectSpec("App.Domain", [
                ("Order.cs", """
                    namespace App.Domain;
                    public class Order { public void Process() { } }
                    """)
            ], VirtualProjectDirectory: "src/App.Domain"),
            new ProjectSpec("App.Domain.IntegrationTests", [
                ("OrderIntegrationTests.cs", """
                    namespace App.Domain.IntegrationTests;
                    public class OrderIntegrationTests
                    {
                        [NUnit.Framework.Category("Integration")]
                        [NUnit.Framework.Test]
                        public void Process_IntegrationTest()
                        {
                            var o = new App.Domain.Order();
                            o.Process();
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "tests/App.Domain.IntegrationTests")
        );

        var state = CreateServer(solutionOwner.Solution);
        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("Order"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default)!;

        Assert.NotNull(structured);
        Assert.False(structured.IsUntested);
        var file = Assert.Single(structured.TestFiles);
        Assert.Equal("Integration", file.Category);
        Assert.Equal("tests/App.Domain.IntegrationTests", file.ProjectDirectory);
        Assert.Contains(structured.RecommendedTestCommands, c => c.Contains("dotnet test tests/App.Domain.IntegrationTests --filter FullyQualifiedName~OrderIntegrationTests"));
    }
}
