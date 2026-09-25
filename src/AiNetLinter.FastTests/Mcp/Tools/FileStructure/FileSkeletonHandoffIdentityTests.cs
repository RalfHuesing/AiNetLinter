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

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Component")]
public sealed class FileSkeletonHandoffIdentityTests
{
    [Theory]
    [InlineData("public Widget()", "Widget.Widget()", "first-default-body", "FirstDefaultCall", "second-default-body")]
    [InlineData("public Widget(int value)", "Widget.Widget(int)", "first-value-body", "FirstValueCall", "second-value-body")]
    public async Task GetFileSkeleton_ConstructorHandoffResolvesOnlyItsDeclaringProject(
        string skeletonSignature,
        string expectedBodySignature,
        string expectedBodyMarker,
        string expectedCaller,
        string otherProjectBodyMarker)
    {
        using var scenario = CreateConstructorScenario();
        var state = CreateServer(scenario.Solution);

        var skeleton = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/First/Widget.cs"], CancellationToken.None);
        var skeletonText = TextOf(skeleton);
        var row = skeletonText.Split('\n').SingleOrDefault(line =>
            line.Contains(skeletonSignature, StringComparison.Ordinal));
        Assert.True(row is not null, skeletonText);

        var handoffId = ExtractHandoffId(row!);
        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 50, Depth: 1), CancellationToken.None);
        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoffId], 80, CancellationToken.None);
        var feature = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(handoffId), CancellationToken.None);

        var failures = new[]
        {
            ResolutionFailure(references, "find_references", ["Widget..ctor", "FirstCalls.cs"], ["SecondCalls.cs"]),
            ResolutionFailure(body, "get_symbol_body", [expectedBodySignature, expectedBodyMarker], [otherProjectBodyMarker]),
            ResolutionFailure(feature, "get_feature_context", [expectedBodySignature, expectedCaller], ["SecondCalls.cs"])
        }.Where(failure => failure is not null).ToArray();
        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task GetFileSkeleton_TypeHandoffResolvesOnlyItsDeclaringProject()
    {
        using var scenario = CreateTypeScenario();
        var state = CreateServer(scenario.Solution);

        var skeleton = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/First/SharedType.cs"], CancellationToken.None);
        var skeletonText = TextOf(skeleton);
        var row = skeletonText.Split('\n').SingleOrDefault(line =>
            line.StartsWith("### SharedType", StringComparison.Ordinal));
        Assert.True(row is not null, skeletonText);

        var handoffId = ExtractHandoffId(row!);
        var structure = await GetClassStructureTool.ExecuteAsync(
            state, handoffId, "name", CancellationToken.None);
        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 50, Depth: 1), CancellationToken.None);

        var failures = new[]
        {
            ResolutionFailure(structure, "get_class_structure", ["SharedType", "FirstOnly"], ["SecondOnly"]),
            ResolutionFailure(references, "find_references", ["SharedType", "FirstTypeCall"], ["SecondTypeCall"])
        }.Where(failure => failure is not null).ToArray();
        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task FindSymbol_TypeHandoffResolvesOnlyItsDeclaringProject()
    {
        using var scenario = CreateTypeScenario();
        var state = CreateServer(scenario.Solution);

        var search = await FindSymbolTool.ExecuteAsync(
            state, ["SharedType"], "class", 50, CancellationToken.None);
        var searchText = TextOf(search);
        var row = searchText.Split('\n').SingleOrDefault(line =>
            line.Contains("src/First/SharedType.cs", StringComparison.Ordinal));
        Assert.True(row is not null, searchText);

        var handoffId = ExtractHandoffId(row!);
        var structure = await GetClassStructureTool.ExecuteAsync(
            state, handoffId, "name", CancellationToken.None);
        var structureText = TextOf(structure);

        Assert.Contains("FirstOnly", structureText, StringComparison.Ordinal);
        Assert.DoesNotContain("SecondOnly", structureText, StringComparison.Ordinal);
    }

    private static RoslynTestSolution CreateConstructorScenario() => RoslynTestSolutionFactory.CreateSolution(
        @"C:\ainetlinter-virtual\FileSkeletonConstructorIdentity.slnx",
        new ProjectSpec("First", [
            ("Widget.cs", """
                namespace Shared.Contracts;

                public sealed class Widget
                {
                    public Widget() { _ = "first-default-body"; }
                    public Widget(int value) { _ = value; _ = "first-value-body"; }
                }
                """),
            ("FirstCalls.cs", """
                namespace Shared.Contracts;

                public static class FirstCalls
                {
                    public static Widget FirstDefaultCall() => new();
                    public static Widget FirstValueCall() => new(1);
                }
                """)
        ], VirtualProjectDirectory: "src/First"),
        new ProjectSpec("Second", [
            ("Widget.cs", """
                namespace Shared.Contracts;

                public sealed class Widget
                {
                    public Widget() { _ = "second-default-body"; }
                    public Widget(int value) { _ = value; _ = "second-value-body"; }
                }
                """),
            ("SecondCalls.cs", """
                namespace Shared.Contracts;

                public static class SecondCalls
                {
                    public static Widget SecondDefaultCall() => new();
                    public static Widget SecondValueCall() => new(2);
                }
                """)
        ], VirtualProjectDirectory: "src/Second"));

    private static RoslynTestSolution CreateTypeScenario() => RoslynTestSolutionFactory.CreateSolution(
        @"C:\ainetlinter-virtual\FileSkeletonTypeIdentity.slnx",
        new ProjectSpec("First", [
            ("SharedType.cs", """
                namespace Shared.Contracts;
                public sealed class SharedType { public void FirstOnly() { } }
                """),
            ("FirstTypeCalls.cs", """
                namespace Shared.Contracts;
                public static class FirstTypeCalls
                {
                    public static SharedType FirstTypeCall() => new SharedType();
                }
                """)
        ], VirtualProjectDirectory: "src/First"),
        new ProjectSpec("Second", [
            ("SharedType.cs", """
                namespace Shared.Contracts;
                public sealed class SharedType { public void SecondOnly() { } }
                """),
            ("SecondTypeCalls.cs", """
                namespace Shared.Contracts;
                public static class SecondTypeCalls
                {
                    public static SharedType SecondTypeCall() => new SharedType();
                }
                """)
        ], VirtualProjectDirectory: "src/Second"));

    private static McpCodeGraphServer CreateServer(Microsoft.CodeAnalysis.Solution solution) =>
        new(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: solution)));

    private static string ExtractHandoffId(string row)
    {
        var handoffId = Regex.Match(row, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);
        return handoffId;
    }

    private static string? ResolutionFailure(
        CallToolResult result,
        string toolName,
        string[] expectedText,
        string[] absentText)
    {
        var text = TextOf(result);
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
