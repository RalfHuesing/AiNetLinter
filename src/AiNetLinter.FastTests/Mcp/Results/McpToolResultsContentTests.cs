#nullable enable

using System.IO;
using System.Linq;
using System.Reflection;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.FastTests.Mcp.Results;

[Trait("Category", "Unit")]
public sealed class McpToolResultsContentTests
{
    [Fact]
    public void Text_HasNoLegacyPayloadOverload()
    {
        var textOverloads = typeof(McpToolResults)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name == nameof(McpToolResults.Text));

        Assert.DoesNotContain(textOverloads, method => method.IsGenericMethodDefinition);
    }

    [Fact]
    public void Error_RendersCodeMessageAndContextInOneTextBlock()
    {
        var result = McpToolResults.Error("TEST_CODE", "Testnachricht", context: "Demo.cs");

        Assert.True(result.IsError);
        Assert.Equal("[ERROR]: TEST_CODE: Testnachricht\n  context: Demo.cs", TextOf(result));
    }

    [Fact]
    public void InvalidArgument_RendersFieldPathAndActionableHint()
    {
        var result = McpToolResults.InvalidArgument(
            "Unbekanntes Argument: projectRoot",
            fieldPath: "$.projectRoot");

        var text = TextOf(result);
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("fieldPath: $.projectRoot", text, StringComparison.Ordinal);
        Assert.Contains("Parameter pruefen", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Navigation_PreservesSuccessfulContentWithoutFooter()
    {
        var result = McpToolResults.WithNavigation(McpToolResults.Text("Antwort"), CreateTarget());

        Assert.Equal("Antwort", TextOf(result));
        Assert.False(result.IsError ?? false);
    }

    [Fact]
    public void Navigation_AppendsRecoverableActionWithoutLeakingTargetPath()
    {
        var target = CreateTarget();
        var result = McpToolResults.WithNavigation(
            McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Argument ist ungueltig.",
                hint: "Scope verfeinern."),
            target);

        var text = TextOf(result);
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("Scope verfeinern.", text, StringComparison.Ordinal);
        Assert.DoesNotContain(target.CanonicalPath, text, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponseBudgetError_RendersMinimumAndExecutableRetry()
    {
        var result = McpToolResults.Error(
            LinterErrorCodes.ResponseBudgetTooSmall,
            "Budget ist zu klein.",
            new McpErrorParameters(FieldPath: "$.maxResponseBytes", RequestedBytes: 512, MinimumResponseBytes: 1024));

        var text = TextOf(result);
        Assert.Contains("minimumResponseBytes: 1024", text, StringComparison.Ordinal);
        Assert.Contains("maxResponseBytes=1024", text, StringComparison.Ordinal);
    }

    private static AnalysisTarget CreateTarget()
    {
        using var tempDir = TestTempDirectory.Create("mcp-content-navigation-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        return Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(new AnalysisTargetRequest(solutionPath)).Target);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
