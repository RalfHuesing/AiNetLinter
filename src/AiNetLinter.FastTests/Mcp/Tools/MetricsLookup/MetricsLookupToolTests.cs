#nullable enable

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.MetricsLookup;

[Trait("Category", "Component")]
public sealed class MetricsLookupToolTests
{
    private readonly McpInMemoryTestContext _fixture = new();

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await MetricsLookupTool.ExecuteAsync(state, "irrelevant", CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyIdentifier_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, "", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentSymbol_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, "NonExistentClass.NonExistentMethod", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_MethodSymbol_ReturnsMethodMetricsAndThresholds()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, "Greeter.Greet", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text);
        Assert.Contains("Schwellwert-Abgleich", textContent.Text);
        Assert.Contains("[OK]", textContent.Text);

        Assert.NotNull(result.StructuredContent);
        var dto = JsonSerializer.Deserialize<MetricsLookupResultDto>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(dto);
        Assert.Equal("Greet", dto.SymbolName);
        Assert.NotNull(dto.MethodMetrics);
        Assert.Equal(1, dto.MethodMetrics.TotalParameters);
        Assert.Equal(1, dto.MethodMetrics.EffectiveParameters);
        Assert.Equal(1, dto.MethodMetrics.CyclomaticComplexity);
        Assert.NotEmpty(dto.ThresholdChecks);
        Assert.Contains(dto.ThresholdChecks, c => c.Metric == MetricNames.LineCount && c.Status == ThresholdStatus.Ok);
        Assert.Contains(dto.ThresholdChecks, c => c.Metric == MetricNames.CyclomaticComplexity && c.Status == ThresholdStatus.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_TypeSymbol_ReturnsTypeMetricsAndFootprint()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, "Greeter", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greeter", textContent.Text);
        Assert.Contains("AI-Context-Footprint", textContent.Text);

        Assert.NotNull(result.StructuredContent);
        var dto = JsonSerializer.Deserialize<MetricsLookupResultDto>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(dto);
        Assert.Equal("Greeter", dto.SymbolName);
        Assert.NotNull(dto.TypeMetrics);
        Assert.True(dto.TypeMetrics.CodeLines > 0);
        Assert.True(dto.TypeMetrics.AiContextFootprint > 0);
        Assert.True(dto.TypeMetrics.TotalMemberCount >= 2);
        Assert.Contains(dto.ThresholdChecks, c => c.RuleId == LinterRuleIds.MaxLineCount);
        Assert.Contains(dto.ThresholdChecks, c => c.RuleId == LinterRuleIds.AIContextFootprint);
    }

    [Fact]
    public async Task ExecuteAsync_PropertySymbol_ReturnsPropertyMetrics()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, "Greeter.Prefix", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Prefix", textContent.Text);

        Assert.NotNull(result.StructuredContent);
        var dto = JsonSerializer.Deserialize<MetricsLookupResultDto>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(dto);
        Assert.Equal("Prefix", dto.SymbolName);
        Assert.NotNull(dto.PropertyMetrics);
        Assert.True(dto.PropertyMetrics.HasGetter);
        Assert.True(dto.PropertyMetrics.HasSetter);
    }

    [Fact]
    public async Task ExecuteAsync_ComplexMethodWithViolations_ReportsViolationsCorrectly()
    {
        using var customSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ComplexTest.slnx",
            new ProjectSpec("ComplexTest", [
                ("Complex.cs", """
                    namespace ComplexTest;

                    public class ComplexClass
                    {
                        public int ComplexMethod(int a, int b, int c, int d, int e)
                        {
                            if (a > 0)
                            {
                                if (b > 0)
                                {
                                    while (c > 0)
                                    {
                                        c--;
                                    }
                                }
                            }
                            return a + b + c + d + e;
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/ComplexTest"));

        using var customFixture = new McpInMemoryTestContext(customSolution);
        var config = new Config
        {
            Global = new(),
            Metrics = new MetricsConfig
            {
                MaxMethodParameterCount = 4,
                MaxCyclomaticComplexity = 2,
                MaxCognitiveComplexity = 2
            }
        };

        var state = customFixture.CreateServer(config: config);

        var result = await MetricsLookupTool.ExecuteAsync(state, "ComplexClass.ComplexMethod", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var dto = JsonSerializer.Deserialize<MetricsLookupResultDto>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(dto);
        Assert.NotNull(dto.MethodMetrics);
        Assert.Equal(5, dto.MethodMetrics.TotalParameters);
        Assert.Equal(5, dto.MethodMetrics.EffectiveParameters);

        Assert.Contains(dto.ThresholdChecks, c => c.Metric == MetricNames.ParameterCount && c.Status == ThresholdStatus.Violation);
        Assert.Contains(dto.ThresholdChecks, c => c.Metric == MetricNames.CyclomaticComplexity && c.Status == ThresholdStatus.Violation);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoredParameterTypes_ExcludesFromEffectiveCount()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\IgnoreParamTest.slnx",
            new ProjectSpec("IgnoreParamTest", [
                ("ParamTest.cs", """
                    using System.Threading;

                    namespace IgnoreParamTest;

                    public class ParamTestClass
                    {
                        public void Execute(string name, int count, CancellationToken ct)
                        {
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/IgnoreParamTest"));

        using var fixture = new McpInMemoryTestContext(solution);
        var config = new Config
        {
            Global = new(),
            Metrics = new MetricsConfig
            {
                MethodParameterCountIgnoreTypeNames = ["CancellationToken"],
                MaxMethodParameterCount = 4
            }
        };

        var state = fixture.CreateServer(config: config);

        var result = await MetricsLookupTool.ExecuteAsync(state, "ParamTestClass.Execute", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var dto = JsonSerializer.Deserialize<MetricsLookupResultDto>(
            result.StructuredContent.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(dto);
        Assert.NotNull(dto.MethodMetrics);
        Assert.Equal(3, dto.MethodMetrics.TotalParameters);
        Assert.Equal(2, dto.MethodMetrics.EffectiveParameters);
        Assert.Single(dto.MethodMetrics.IgnoredParameters);
        Assert.Contains("ct (CancellationToken)", dto.MethodMetrics.IgnoredParameters[0]);
    }
}
