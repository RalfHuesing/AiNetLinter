#nullable enable

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
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
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

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
        Assert.Contains("## 2. Metriken & Budget", text);
        Assert.Contains("Cyclomatic Complexity", text);
        Assert.Contains("Cognitive Complexity", text);

        // 3. Callers
        Assert.Contains("## 3. Direkte Aufrufer", text);
        Assert.Contains("Consumer.cs", text);

        // 4. Test-Abdeckung
        Assert.Contains("## 4. Test-Abdeckung", text);
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
        Assert.NotNull(payload.Metrics);
        Assert.NotNull(payload.Callers);
        Assert.Equal(2, payload.Callers.TotalCallers);
        Assert.NotNull(payload.Tests);
        Assert.True(payload.Tests.TotalMatchingTests >= 1);
        Assert.NotNull(payload.Violations);
    }

    [Fact]
    public async Task ExecuteAsync_ValidType_ReturnsTypeMetricsAndDeclaration()
    {
        using var scenario = CreateFullTestScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

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
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var options = new FeatureContextOptions(
            Symbol: "Calculator.Add",
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
        Assert.DoesNotContain("## 4. Test-Abdeckung", text);
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
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var options = new FeatureContextOptions(
            Symbol: "Calculator.Add",
            MaxCallers: 1
        );

        var result = await GetFeatureContextTool.ExecuteAsync(state, options, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        var text = textContent.Text;

        Assert.Contains("Zeige 1 von 2 Aufrufern", text);

        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<FeatureContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(payload?.Callers);
        Assert.True(payload.Callers.IsTruncated);
        Assert.Single(payload.Callers.CallSites);
        Assert.Equal(2, payload.Callers.TotalCallers);
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
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

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
    public async Task ExecuteAsync_MaxTestsTruncation_SetsTruncationFlagAndNote()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MultiTestScenario.slnx",
            new ProjectSpec("Lib", [
                ("Service.cs", """
                    namespace Lib;
                    public class Service
                    {
                        public void Execute() {}
                    }
                    """)
            ], VirtualProjectDirectory: "src/Lib"),
            new ProjectSpec("Lib.Tests1", [
                ("ServiceTests1.cs", """
                    namespace Lib.Tests1;
                    public class ServiceTests1
                    {
                        [Xunit.Fact]
                        public void Test1() { var s = new Lib.Service(); s.Execute(); }
                    }
                    """)
            ], VirtualProjectDirectory: "tests/Lib.Tests1"),
            new ProjectSpec("Lib.Tests2", [
                ("ServiceTests2.cs", """
                    namespace Lib.Tests2;
                    public class ServiceTests2
                    {
                        [Xunit.Fact]
                        public void Test2() { var s = new Lib.Service(); s.Execute(); }
                    }
                    """)
            ], VirtualProjectDirectory: "tests/Lib.Tests2")
        );

        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var options = new FeatureContextOptions(Symbol: "Service.Execute", MaxTests: 1);
        var result = await GetFeatureContextTool.ExecuteAsync(state, options, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Zeige 1 von 2 Testdateien", textContent.Text);

        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<FeatureContextPayload>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(payload?.Tests);
        Assert.True(payload.Tests.IsTruncated);
        Assert.Single(payload.Tests.TestFiles);
        Assert.Equal(2, payload.Tests.TotalTestFiles);
    }
}
