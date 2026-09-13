#nullable enable

using System.IO;
using System.Linq;
using System.Reflection;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Wire;
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
    public void ResponsePipeline_NormalizesAllLineEndingVariants()
    {
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "first\r\nsecond\rthird\nfourth" }],
        };

        var filtered = McpToolResponsePipeline.Apply(result);

        Assert.Equal("first\nsecond\nthird\nfourth", TextOf(filtered));
    }

    [Fact]
    public void Text_ExternalizesInternalHandoffBeforeItReachesContent()
    {
        const string internalId = "i:0:target-token:snapshot-token:M:Namespace.Type.Member";

        var text = TextOf(McpToolResults.Text($"handoffId: `{internalId}`"));

        Assert.Matches(@"handoffId: `h:[a-zA-Z0-9]+`", text);
        Assert.DoesNotContain(internalId, text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `i:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponsePipeline_ExternalizesInternalHandoffsFromDirectRenderers()
    {
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "handoffId: `i:1:target:snapshot:T:Namespace.Type`\n%% handoffId: n1 = i:0:target:snapshot:M:Namespace.Type.Member" }],
        };

        var text = TextOf(McpToolResponsePipeline.Apply(result));

        Assert.DoesNotContain("handoffId: `i:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: n1 = i:", text, StringComparison.Ordinal);
        Assert.Matches(@"handoffId: `h:[a-zA-Z0-9]+`", text);
        Assert.Matches(@"handoffId: n1 = h:[a-zA-Z0-9]+", text);
    }

    [Fact]
    public void ExternalizeInternalHandoffs_CounterFailure_StopsResponseInsteadOfRenderingFallback()
    {
        const string internalId = "i:0:target-token:snapshot-token:M:Namespace.Type.Member";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            McpToolResults.ExternalizeInternalHandoffs(
                $"handoffId: `{internalId}`",
                _ => Result<string>.Failure(
                    LinterErrorCodes.HandoffCounterUnavailable,
                    "Counter-Speicher nicht verfuegbar.")));

        Assert.Contains(LinterErrorCodes.HandoffCounterUnavailable, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(internalId, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("nicht verfuegbar", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("requestedBytes: 512", text, StringComparison.Ordinal);
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
