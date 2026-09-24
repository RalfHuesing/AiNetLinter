#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class ConstructorHandoffLifecycleTests
{
    [Theory]
    [InlineData("Overloaded", "Overloaded.Overloaded()", 20)]
    [InlineData("Overloaded", "Overloaded.Overloaded(string label)", 21)]
    [InlineData("Parameterless", "Parameterless.Parameterless()", 22)]
    [InlineData("Positional", "Positional.Positional(string Name)", 23)]
    public async Task GetClassStructureConstructorHandoffs_AreReusableByCommonFollowUpTools(
        string typeName,
        string signature,
        int callSiteLine)
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ConstructorHandoffLifecycle.slnx",
            new ProjectSpec("App", [
                ("Constructors.cs", """
                    namespace TestNs;

                    public sealed class Overloaded
                    {
                        public Overloaded() { }
                        public Overloaded(string label) { _ = label; }
                    }

                    public sealed class Parameterless
                    {
                        public Parameterless() { }
                    }

                    public sealed record Positional(string Name);

                    public static class Consumer
                    {
                        public static void Use()
                        {
                            _ = new Overloaded();
                            _ = new Overloaded("label");
                            _ = new Parameterless();
                            _ = new Positional("record");
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        await AssertConstructorHandoffWorksAsync(state, typeName, signature, callSiteLine);
    }

    private static async Task AssertConstructorHandoffWorksAsync(
        McpCodeGraphServer state,
        string typeName,
        string signature,
        int callSiteLine)
    {
        var structure = await GetClassStructureTool.ExecuteAsync(state, $"TestNs.{typeName}", "name", CancellationToken.None);
        var structureText = TextOf(structure);
        var row = structureText.Split('\n').SingleOrDefault(line =>
            line.StartsWith("| Constructor | .ctor |", StringComparison.Ordinal)
            && line.Contains(signature, StringComparison.Ordinal));
        Assert.True(row is not null, structureText);

        var handoffId = Regex.Match(row!, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);

        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 50, Depth: 1), CancellationToken.None);
        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoffId], 80, CancellationToken.None);
        var context = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(handoffId), CancellationToken.None);

        var expectedCallSite = $"Constructors.cs:{callSiteLine}";
        var failures = new[]
        {
            FollowUpFailure(references, handoffId, "find_references", expectedCallSite, "Consumer.Use"),
            FollowUpFailure(body, handoffId, "get_symbol_body", signature),
            FollowUpFailure(context, handoffId, "get_feature_context", signature, expectedCallSite, "Consumer.Use")
        }.Where(failure => failure is not null).ToArray();
        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    private static string? FollowUpFailure(
        CallToolResult result,
        string handoffId,
        string toolName,
        params string[] expectedContent)
    {
        var text = TextOf(result);
        if (result.IsError is true || text.Contains("SYMBOL_NOT_FOUND", StringComparison.Ordinal))
        {
            return $"{toolName} could not resolve emitted constructor handoff {handoffId}: {text}";
        }

        var missing = expectedContent.Where(content => !text.Contains(content, StringComparison.Ordinal)).ToArray();
        return missing.Length == 0
            ? null
            : $"{toolName} resolved {handoffId} but omitted [{string.Join(", ", missing)}]: {text}";
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
