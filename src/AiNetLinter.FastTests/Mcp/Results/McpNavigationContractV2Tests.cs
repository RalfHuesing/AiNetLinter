#nullable enable

using System.IO;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Results;

[Trait("Category", "Unit")]
public sealed class McpNavigationContractV2Tests
{
    [Theory]
    [InlineData("complete", "complete")]
    [InlineData("empty", "complete")]
    [InlineData("truncated", "complete")]
    public void WithNavigation_SourceResponsesProjectDistinctResponseCompleteness(
        string expectedCompleteness,
        string expectedAnalysisQuality)
    {
        var target = CreateTarget("workspace.slnx");
        object payload = expectedCompleteness == "empty"
            ? new { matches = System.Array.Empty<object>() }
            : expectedCompleteness == "truncated"
                ? new { matches = new[] { "match" }, wireTruncated = true }
                : new { matches = new[] { "match" } };

        var result = McpToolResults.WithNavigation(McpToolResults.Text("Antwort", payload), target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal(2, navigation.GetProperty("contractVersion").GetInt32());
        Assert.Equal("ok", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal(expectedCompleteness, navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal(expectedAnalysisQuality, navigation.GetProperty("analysis").GetProperty("quality").GetString());
        Assert.Equal(JsonValueKind.Null, navigation.GetProperty("status").GetProperty("code").ValueKind);
        Assert.NotEqual(true, result.IsError);
    }

    [Fact]
    public void WithNavigation_PartialAssemblyAndTruncatedResponseRemainIndependent()
    {
        var target = CreateTarget("library.dll");
        var result = McpToolResults.WithNavigation(
            McpToolResults.Text("Antwort", new
            {
                assemblyPath = target.CanonicalPath,
                diagnostics = new[] { "missing reference" },
                wireTruncated = true,
            }),
            target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("truncated", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("decompiled", navigation.GetProperty("analysis").GetProperty("mode").GetString());
        Assert.Equal("partial", navigation.GetProperty("analysis").GetProperty("quality").GetString());
        Assert.Contains(
            "ASSEMBLY_DIAGNOSTICS",
            navigation.GetProperty("analysis").GetProperty("limitationCodes").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void WithNavigation_ResponseBudgetErrorIsAtomicAndMarkedAsError()
    {
        var target = CreateTarget("workspace.slnx");
        var result = McpToolResults.WithNavigation(
            McpToolResults.Recoverable(
                AiNetLinter.Output.LinterErrorCodes.ResponseBudgetTooSmall,
                "Budget ist zu klein.",
                new McpErrorParameters(FieldPath: "$.maxResponseBytes")),
            target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.True(result.IsError);
        Assert.Equal("error", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal(AiNetLinter.Output.LinterErrorCodes.ResponseBudgetTooSmall, navigation.GetProperty("status").GetProperty("code").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("analysis").GetProperty("quality").GetString());
        Assert.False(result.StructuredContent.Value.GetProperty("recoverable").GetBoolean());
    }

    [Fact]
    public void WithNavigation_ReprojectsPostNavigationBudgetErrorAsV2()
    {
        var target = CreateTarget("workspace.slnx");
        var result = McpToolResults.WithNavigation(
            McpToolResults.Text("Antwort", new { declaration = new { name = "Demo.Run" } }),
            target,
            maxResponseBytes: 512,
            postNavigationResponseBudget: (_, _) => McpToolResults.Error(
                AiNetLinter.Output.LinterErrorCodes.ResponseBudgetTooSmall,
                "Budget ist zu klein.",
                new McpErrorParameters(FieldPath: "$.maxResponseBytes", RequestedBytes: 512, MinimumResponseBytes: 1024)));
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.True(result.IsError);
        Assert.Equal(2, navigation.GetProperty("contractVersion").GetInt32());
        Assert.Equal("error", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal(AiNetLinter.Output.LinterErrorCodes.ResponseBudgetTooSmall, navigation.GetProperty("status").GetProperty("code").GetString());
        Assert.False(result.StructuredContent.Value.GetProperty("recoverable").GetBoolean());
    }

    [Fact]
    public void WithNavigation_DiscardsLegacyEnvelopeInsteadOfParsingIt()
    {
        var target = CreateTarget("workspace.slnx");
        var result = McpToolResults.WithNavigation(
            McpToolResults.Text("Antwort", new
            {
                matches = System.Array.Empty<object>(),
                navigation = new { contractVersion = 1, status = new { completeness = "complete" } },
            }),
            target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal(2, navigation.GetProperty("contractVersion").GetInt32());
        Assert.Equal("empty", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.False(navigation.TryGetProperty("operationStatus", out _));
    }

    private static AnalysisTarget CreateTarget(string fileName)
    {
        using var tempDir = TestTempDirectory.Create("mcp-contract-v2-");
        var targetPath = Path.Combine(tempDir.DirectoryPath, fileName);
        File.WriteAllText(targetPath, string.Empty);
        return Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(new AnalysisTargetRequest(targetPath)).Target);
    }
}
