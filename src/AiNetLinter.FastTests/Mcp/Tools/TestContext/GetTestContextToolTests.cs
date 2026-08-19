#nullable enable

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: solution)));

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
        Assert.Contains("# Test-Coverage-Kontext: CoreLib.Calculator", textContent.Text);
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
    public async Task ExecuteAsync_UntestedSymbol_ReturnsUntestedNoticeAndPathSuggestion()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions("UntestedService"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Für dieses Symbol wurden keine direkten Tests gefunden", textContent.Text);
        Assert.Contains("Empfehlung:", textContent.Text);
        Assert.Contains("src/AiNetLinter.FastTests/", textContent.Text);

        Assert.NotNull(result.StructuredContent);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default)!;

        Assert.NotNull(structured);
        Assert.True(structured.IsUntested);
        Assert.Equal(0, structured.TotalMatchingTests);
        Assert.Empty(structured.TestFiles);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierAlias_WorksEqually()
    {
        using var solutionOwner = CreateTestScenario();
        var state = CreateServer(solutionOwner.Solution);

        var result = await GetTestContextTool.ExecuteAsync(state, new TestContextOptions(Symbol: null, SymbolIdentifier: "Calculator"), CancellationToken.None);

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
    }
}
