#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class LocalFunctionCallerHandoffTests
{
    [Fact]
    public async Task FindReferences_LocalFunctionCaller_RemainsVisibleWithoutNonNavigableHandoff()
    {
        using var context = new McpInMemoryTestContext(McpInMemoryTestContext.CreateScenario(
            new ProjectSpec("LocalFunctionCallerProbe", [
                ("Worker.cs", """
                    namespace LocalFunctionCallerProbe;

                    public static class Worker
                    {
                        public static void Target() { }

                        public static void Run()
                        {
                            void InvokeTarget() => Target();
                            InvokeTarget();
                        }
                    }
                    """)])));
        var state = context.CreateServer();

        var references = await FindReferencesTool.ExecuteAsync(
            state,
            new FindReferencesRequest("LocalFunctionCallerProbe.Worker.Target", MaxResults: 20, Depth: 1),
            CancellationToken.None);
        var referencesText = TextOf(references);
        var callSite = referencesText.Split('\n').Single(line =>
            line.Contains("Worker.cs:", StringComparison.Ordinal)
            && line.Contains("Aufruf von 'Worker.Target'", StringComparison.Ordinal));
        var handleMatch = Regex.Match(
            callSite,
            @"handoffId: `(?<handle>h:[^`]+)`",
            RegexOptions.CultureInvariant);

        string? bodyFailure = null;
        string? referencesFailure = null;
        if (handleMatch.Success)
        {
            var handle = handleMatch.Groups["handle"].Value;
            var body = await GetSymbolBodyTool.ExecuteAsync(
                state, [handle], 80, CancellationToken.None);
            var bodyText = TextOf(body);
            bodyFailure = body.IsError is true
                || bodyText.Contains("SYMBOL_NOT_FOUND", StringComparison.Ordinal)
                || bodyText.Contains("AMBIGUOUS_SYMBOL", StringComparison.Ordinal)
                ? bodyText
                : null;

            var followupReferences = await FindReferencesTool.ExecuteAsync(
                state,
                new FindReferencesRequest(handle, MaxResults: 20, Depth: 1),
                CancellationToken.None);
            var followupReferencesText = TextOf(followupReferences);
            referencesFailure = followupReferences.IsError is true
                || followupReferencesText.Contains("SYMBOL_NOT_FOUND", StringComparison.Ordinal)
                || followupReferencesText.Contains("AMBIGUOUS_SYMBOL", StringComparison.Ordinal)
                ? followupReferencesText
                : null;
        }

        Assert.False(
            handleMatch.Success,
            "A call site enclosed by a local function must remain visible without a non-canonical handoff. "
            + $"The emitted handle was submitted unchanged to both follow-up tools. "
            + $"get_symbol_body failure: {bodyFailure ?? "none"}; "
            + $"find_references failure: {referencesFailure ?? "none"}; "
            + $"call-site row: {callSite}");
        Assert.Contains("Aufruf von 'Worker.Target'", callSite, StringComparison.Ordinal);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
