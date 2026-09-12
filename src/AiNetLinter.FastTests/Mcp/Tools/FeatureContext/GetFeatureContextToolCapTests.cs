#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FeatureContext;

[Trait("Category", "Component")]
public sealed class GetFeatureContextToolCapTests
{
    [Theory]
    [InlineData(2, 1, 1, 1, "maxTests")]
    [InlineData(1, 51, 1, 50, "maxTestMethodsPerFile")]
    [InlineData(5, 50, 10, 200, "maxTestMethodsTotal")]
    [InlineData(2, 51, 1, 50, "maxTests,maxTestMethodsPerFile")]
    [InlineData(6, 50, 5, 200, "maxTests,maxTestMethodsTotal")]
    [InlineData(5, 51, 5, 200, "maxTestMethodsPerFile,maxTestMethodsTotal")]
    [InlineData(6, 51, 5, 200, "maxTests,maxTestMethodsPerFile,maxTestMethodsTotal")]
    public async Task ExecuteAsync_CapCombinations_ReportActualTruncationReasons(
        int testFileCount,
        int methodsPerFile,
        int maxTests,
        int expectedDisplayedMethods,
        string expectedReasons)
    {
        using var scenario = CreateScenario(testFileCount, methodsPerFile);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                Config: new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
                ReadOnlySolutionSnapshot: scenario.Solution)));

        var result = await GetFeatureContextTool.ExecuteAsync(
            state,
            new FeatureContextOptions(
                "Service.Execute",
                IncludeMetrics: false,
                IncludeViolations: false,
                MaxTests: maxTests,
                MaxResponseBytes: 64 * 1024),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Status: truncated", text, StringComparison.Ordinal);
        Assert.Contains($"{testFileCount * methodsPerFile}", text, StringComparison.Ordinal);
        Assert.Contains($"{testFileCount} Testdateien", text, StringComparison.Ordinal);
        foreach (var reason in expectedReasons.Split(','))
        {
            Assert.Contains(reason, text, StringComparison.Ordinal);
        }
        Assert.Contains("testContext", text, StringComparison.Ordinal);
        Assert.Contains($"{expectedDisplayedMethods} konkrete Testmethoden", text, StringComparison.Ordinal);
        Assert.Contains("Nächster sicherer Schritt", text, StringComparison.Ordinal);
    }

    private static RoslynTestSolution CreateScenario(int testFileCount, int methodsPerFile)
    {
        var testProjects = Enumerable.Range(1, testFileCount)
            .Select(i => new ProjectSpec(
                $"Lib.Tests{i}",
                [($"ServiceTests{i}.cs", CreateTestSource(methodsPerFile))],
                VirtualProjectDirectory: $"tests/Lib.Tests{i}"))
            .ToArray();

        return RoslynTestSolutionFactory.CreateSolution(
            $"C:\\virtual\\FeatureContextCaps-{testFileCount}-{methodsPerFile}.slnx",
            [
                new ProjectSpec("Lib", [
                    ("Service.cs", "namespace Lib; public class Service { public void Execute() {} }")
                ], VirtualProjectDirectory: "src/Lib"),
                .. testProjects
            ]);
    }

    private static string CreateTestSource(int methodsPerFile) =>
        "namespace Lib.Tests; public class ServiceTests{" +
        string.Join("", Enumerable.Range(1, methodsPerFile).Select(i =>
            $"[Xunit.Fact] public void Execute_Case{i:000}() {{ new Lib.Service().Execute(); }}")) +
        "}";
}
