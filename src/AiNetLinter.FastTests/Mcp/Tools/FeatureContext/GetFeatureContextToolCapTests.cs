#nullable enable

using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FeatureContext;

[Trait("Category", "Component")]
public sealed class GetFeatureContextToolCapTests
{
    private const int ExpectedMethodsPerFile = 50;
    private const int ExpectedMethodsTotal = 200;

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
                MaxTests: maxTests),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var payload = JsonSerializer.Deserialize<FeatureContextPayload>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);
        Assert.NotNull(payload?.Tests);
        Assert.Equal(testFileCount * methodsPerFile, payload.Tests.TotalMatchingTests);
        Assert.Equal(testFileCount, payload.Tests.TotalTestFiles);
        var wireBudgetTruncated = payload.Tests.TruncatedBy?.Contains("responseBudget") == true;
        var expectedReasonsWithWireBudget = expectedReasons.Split(',').ToList();
        if (wireBudgetTruncated) expectedReasonsWithWireBudget.Add("responseBudget");
        Assert.Equal(expectedReasonsWithWireBudget, payload.Tests.TruncatedBy);
        Assert.True(payload.Tests.IsTruncated);
        Assert.Equal("truncated", payload.Tests.Completeness);
        Assert.Contains("testContext", text, StringComparison.Ordinal);
        Assert.Contains("Nächster sicherer Schritt", text, StringComparison.Ordinal);
        Assert.Equal(payload.Tests.DisplayedTestMethods, payload.Tests.TestFiles.Sum(file => file.TestMethods.Count));
        Assert.InRange(payload.Tests.TestFiles.Count, 0, Math.Min(testFileCount, maxTests));
        Assert.All(payload.Tests.TestFiles, file =>
            Assert.InRange(file.TestMethods.Count, 0, Math.Min(methodsPerFile, ExpectedMethodsPerFile)));
        if (!wireBudgetTruncated)
        {
            Assert.Equal(expectedDisplayedMethods, payload.Tests.DisplayedTestMethods);
            Assert.Equal(
                Enumerable.Range(0, payload.Tests.TestFiles.Count)
                    .Select(index => Math.Min(
                        Math.Min(methodsPerFile, ExpectedMethodsPerFile),
                        Math.Max(ExpectedMethodsTotal - index * ExpectedMethodsPerFile, 0))),
                payload.Tests.TestFiles.Select(file => file.TestMethods.Count));
        }
        Assert.Contains(
            $"{payload.Tests.TestFiles.Count} von {testFileCount} Testdateien",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            $"{payload.Tests.DisplayedTestMethods} von {testFileCount * methodsPerFile} Testmethoden",
            text,
            StringComparison.Ordinal);
        if (wireBudgetTruncated) Assert.Contains("Wire-Budget", text, StringComparison.Ordinal);
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
