#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
public sealed class ConstructorBodyHandoffIdentityTests
{
    [Fact]
    public async Task GetSymbolBody_ConstructorOutputHandoffCanBeReusedWithoutAmbiguity()
    {
        using var scenario = CreateScenario();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var structure = await GetClassStructureTool.ExecuteAsync(
            state, "src/First/Widget.cs:3", "name", CancellationToken.None);
        var structureText = TextOf(structure);
        var constructorRow = structureText.Split('\n').SingleOrDefault(line =>
            line.Contains("Widget.Widget()", StringComparison.Ordinal));
        Assert.True(constructorRow is not null, structureText);
        var classStructureHandoffId = ExtractHandoffId(constructorRow!);

        var firstBody = await GetSymbolBodyTool.ExecuteAsync(
            state, [classStructureHandoffId], 80, CancellationToken.None);
        var firstBodyText = TextOf(firstBody);
        var bodyHandoffId = ExtractHandoffId(firstBodyText);

        var repeatedBody = await GetSymbolBodyTool.ExecuteAsync(
            state, [bodyHandoffId], 80, CancellationToken.None);
        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(bodyHandoffId, MaxResults: 50, Depth: 1), CancellationToken.None);
        var feature = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(bodyHandoffId), CancellationToken.None);

        var failures = new[]
        {
            ResolutionFailure(firstBody, firstBodyText, "initial get_symbol_body",
                ["Widget.Widget()", @"C:\ainetlinter-virtual\src\First\Widget.cs", "first-constructor-body"],
                [@"C:\ainetlinter-virtual\src\Second\Widget.cs", "second-constructor-body"]),
            ResolutionFailure(repeatedBody, TextOf(repeatedBody), "repeated get_symbol_body",
                ["Widget.Widget()", @"C:\ainetlinter-virtual\src\First\Widget.cs", "first-constructor-body"],
                ["AMBIGUOUS_SYMBOL", @"C:\ainetlinter-virtual\src\Second\Widget.cs", "second-constructor-body"]),
            ResolutionFailure(references, TextOf(references), "find_references",
                ["Widget..ctor", "FirstCalls.cs"], ["AMBIGUOUS_SYMBOL", "SecondCalls.cs"]),
            ResolutionFailure(feature, TextOf(feature), "get_feature_context",
                ["Widget.Widget()", "FirstCalls.cs", "FirstCall"], ["AMBIGUOUS_SYMBOL", "SecondCalls.cs"])
        }.Where(failure => failure is not null).ToArray();

        Assert.True(failures.Length == 0,
            $"classStructureHandoffId={classStructureHandoffId}; bodyHandoffId={bodyHandoffId};"
            + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static RoslynTestSolution CreateScenario() => RoslynTestSolutionFactory.CreateSolution(
        @"C:\ainetlinter-virtual\ConstructorBodyHandoffIdentity.slnx",
        new ProjectSpec("First", [
            ("Widget.cs", """
                namespace Shared.Contracts;

                public sealed class Widget
                {
                    public Widget() { _ = "first-constructor-body"; }
                }
                """),
            ("FirstCalls.cs", """
                namespace Shared.Contracts;

                public static class FirstCalls
                {
                    public static Widget FirstCall() => new();
                }
                """)
        ], VirtualProjectDirectory: "src/First"),
        new ProjectSpec("Second", [
            ("Widget.cs", """
                namespace Shared.Contracts;

                public sealed class Widget
                {
                    public Widget() { _ = "second-constructor-body"; }
                }
                """),
            ("SecondCalls.cs", """
                namespace Shared.Contracts;

                public static class SecondCalls
                {
                    public static Widget SecondCall() => new();
                }
                """)
        ], VirtualProjectDirectory: "src/Second"));

    private static string ExtractHandoffId(string text)
    {
        var handoffId = Regex.Match(text, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);
        return handoffId;
    }

    private static string? ResolutionFailure(
        CallToolResult result,
        string text,
        string toolName,
        string[] expectedText,
        string[] absentText)
    {
        var missing = expectedText.Where(value => !text.Contains(value, StringComparison.Ordinal)).ToArray();
        var unexpected = absentText.Where(value => text.Contains(value, StringComparison.Ordinal)).ToArray();
        if (result.IsError is not true
            && !text.Contains("AMBIGUOUS_SYMBOL", StringComparison.Ordinal)
            && missing.Length == 0
            && unexpected.Length == 0)
        {
            return null;
        }

        return $"{toolName} failed handoff resolution; missing=[{string.Join(", ", missing)}], "
            + $"unexpected=[{string.Join(", ", unexpected)}]; response: {text}";
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
