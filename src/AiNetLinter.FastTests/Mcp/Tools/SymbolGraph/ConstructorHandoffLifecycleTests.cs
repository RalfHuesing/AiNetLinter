#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.FastTests;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class ConstructorHandoffLifecycleTests
{
    internal static async Task<string> ConstructorFromTypeAsync(McpCodeGraphServer state, string typeId, string signature)
    {
        var structure = await GetClassStructureTool.ExecuteAsync(state, typeId, "name", CancellationToken.None);
        var text = TextOf(structure);
        Assert.False(structure.IsError is true, text);
        var row = Assert.Single(text.Split('\n'), line => line.StartsWith("| Constructor | .ctor |", StringComparison.Ordinal)
            && line.Contains(signature, StringComparison.Ordinal));
        var id = Regex.Match(row, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant).Groups["id"].Value;
        Assert.NotEmpty(id);
        return id;
    }

    [Theory]
    [InlineData("Overloaded", "Overloaded.Overloaded()", "Overloaded.Overloaded()", 21)]
    [InlineData("Overloaded", "Overloaded.Overloaded(string label)", "Overloaded.Overloaded(string)", 22)]
    [InlineData("Parameterless", "Parameterless.Parameterless()", "Parameterless.Parameterless()", 23)]
    [InlineData("Positional", "Positional.Positional(string Name)", "Positional.Positional(string)", 24)]
    public async Task GetClassStructureConstructorHandoffs_AreReusableByCommonFollowUpTools(
        string typeName,
        string classStructureSignature,
        string resolvedSignature,
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
                    public sealed record ParameterlessRecord();

                    public static class Consumer
                    {
                        public static void Use()
                        {
                            _ = new Overloaded();
                            _ = new Overloaded("label");
                            _ = new Parameterless();
                            _ = new Positional("record");
                            _ = new ParameterlessRecord();
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        await AssertConstructorHandoffWorksAsync(state, typeName, classStructureSignature, resolvedSignature, callSiteLine);
    }

    [Theory]
    [InlineData("public Overloaded()", "Overloaded.Overloaded()", 13)]
    [InlineData("public Overloaded(string label)", "Overloaded.Overloaded(string)", 14)]
    public async Task GetFileSkeletonConstructorHandoffs_AreReusableByCommonFollowUpTools(
        string skeletonSignature,
        string resolvedSignature,
        int callSiteLine)
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ConstructorSkeletonHandoffLifecycle.slnx",
            new ProjectSpec("App", [
                ("Constructors.cs", """
                    namespace TestNs;

                    public sealed class Overloaded
                    {
                        public Overloaded() { }
                        public Overloaded(string label) { _ = label; }
                    }

                    public static class Consumer
                    {
                        public static void Use()
                        {
                            _ = new Overloaded();
                            _ = new Overloaded("label");
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var skeleton = await GetFileSkeletonTool.ExecuteAsync(state, ["Constructors.cs"], CancellationToken.None);
        var skeletonText = TextOf(skeleton);
        var row = skeletonText.Split('\n').SingleOrDefault(line =>
            line.Contains(skeletonSignature, StringComparison.Ordinal));
        Assert.True(row is not null, skeletonText);

        var handoffId = Regex.Match(row!, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);

        await AssertFollowUpToolsResolveConstructorAsync(state, handoffId, resolvedSignature, callSiteLine);
    }

    [Fact]
    public async Task VerifyTypeGroup_ExposesReusableConstructorHandoffThroughStructure()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ConstructorVerifyHandoffLifecycle.slnx",
            new ProjectSpec("App", [
                ("Constructors.cs", """
                    namespace TestNs;

                    public sealed record ParameterlessRecord
                    {
                        public ParameterlessRecord() { }
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                Config: TestHelper.CreateDefaultConfig(),
                ReadOnlySolutionSnapshot: scenario.Solution)));

        var verify = await VerifyTool.ExecuteAsync(state, VerifyScope.Solution, CancellationToken.None);
        var verifyText = TextOf(verify);
        var token = Regex.Match(verifyText, @"continuationToken=(?<token>[0-9a-f]{32}:0)", RegexOptions.CultureInvariant)
            .Groups["token"].Value;
        Assert.NotEmpty(token);
        var details = await GetVerifyAdvisoriesTool.ExecuteAsync(state, CancellationToken.None, token);
        var detailText = TextOf(details);
        Assert.Contains("Constructors.cs", detailText, StringComparison.Ordinal);
        var candidate = detailText.Split('\n').SingleOrDefault(line => line.StartsWith("3 | ", StringComparison.Ordinal));
        Assert.True(candidate is not null, detailText);

        var typeId = candidate!.Split(" | ")[2];
        Assert.NotEmpty(typeId);
        Assert.DoesNotContain(verifyText.Split('\n'), row => row.Contains("category=dead_code", StringComparison.Ordinal) && row.Contains("Constructors.cs:5", StringComparison.Ordinal));
        var handoffId = await ConstructorHandoffLifecycleTests.ConstructorFromTypeAsync(state, typeId, "ParameterlessRecord.ParameterlessRecord()");
        Assert.StartsWith("h:", typeId, StringComparison.Ordinal);

        await AssertFollowUpToolsResolveConstructorAsync(
            state,
            handoffId,
            "ParameterlessRecord.ParameterlessRecord()");
    }

    [Theory]
    [InlineData("find_references")]
    [InlineData("get_symbol_body")]
    [InlineData("get_feature_context")]
    public async Task ConstructorFollowUpTools_UnknownHandoff_ReturnsRecoverableError(string toolName)
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\InvalidConstructorHandoff.slnx",
            new ProjectSpec("App", [("Constructors.cs", "namespace TestNs; public sealed record Probe();")], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: scenario.Solution)));

        var result = toolName switch
        {
            "find_references" => await FindReferencesTool.ExecuteAsync(state, new FindReferencesRequest("h:unknown999", MaxResults: 50, Depth: 1), CancellationToken.None),
            "get_symbol_body" => await GetSymbolBodyTool.ExecuteAsync(state, ["h:unknown999"], 80, CancellationToken.None),
            _ => await GetFeatureContextTool.ExecuteAsync(state, new FeatureContextOptions("h:unknown999"), CancellationToken.None)
        };

        var text = TextOf(result);
        Assert.True(result.IsError, text);
        Assert.Contains("HANDOFF_UNKNOWN", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetClassStructureMethodHandoff_RemainsResolvable()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\MethodHandoffLifecycle.slnx",
            new ProjectSpec("App", [
                ("Methods.cs", """
                    namespace TestNs;

                    public sealed class Counterprobe
                    {
                        public void Run() { }
                    }

                    public static class Consumer
                    {
                        public static void Use(Counterprobe value)
                        {
                            value.Run();
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var structure = await GetClassStructureTool.ExecuteAsync(state, "TestNs.Counterprobe", "name", CancellationToken.None);
        var structureText = TextOf(structure);
        var row = structureText.Split('\n').SingleOrDefault(line =>
            line.StartsWith("| Method | Run |", StringComparison.Ordinal));
        Assert.True(row is not null, structureText);

        var handoffId = Regex.Match(row!, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);

        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 50, Depth: 1), CancellationToken.None);
        var referencesText = TextOf(references);

        Assert.False(references.IsError is true, referencesText);
        Assert.Contains("Methods.cs:12", referencesText, StringComparison.Ordinal);
        Assert.Contains("Counterprobe.Run", referencesText, StringComparison.Ordinal);
    }

    private static async Task AssertConstructorHandoffWorksAsync(
        McpCodeGraphServer state,
        string typeName,
        string classStructureSignature,
        string resolvedSignature,
        int callSiteLine)
    {
        var structure = await GetClassStructureTool.ExecuteAsync(state, $"TestNs.{typeName}", "name", CancellationToken.None);
        var structureText = TextOf(structure);
        var row = structureText.Split('\n').SingleOrDefault(line =>
            line.StartsWith("| Constructor | .ctor |", StringComparison.Ordinal)
            && line.Contains(classStructureSignature, StringComparison.Ordinal));
        Assert.True(row is not null, structureText);

        var handoffId = Regex.Match(row!, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);

        await AssertFollowUpToolsResolveConstructorAsync(state, handoffId, resolvedSignature, callSiteLine);
    }

    internal static async Task AssertFollowUpToolsResolveConstructorAsync(
        McpCodeGraphServer state,
        string handoffId,
        string resolvedSignature,
        int? callSiteLine = null)
    {
        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 50, Depth: 1), CancellationToken.None);
        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoffId], 80, CancellationToken.None);
        var context = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(handoffId), CancellationToken.None);

        var expectedCallSite = callSiteLine is int line ? $"Constructors.cs:{line}" : null;
        string[] expectedReferences = expectedCallSite is null ? [] : [expectedCallSite, "..ctor"];
        var failures = new[]
        {
            FollowUpFailure(references, handoffId, "find_references", expectedReferences),
            FollowUpFailure(body, handoffId, "get_symbol_body", resolvedSignature),
            FollowUpFailure(context, handoffId, "get_feature_context", expectedCallSite is null
                ? [resolvedSignature]
                : [resolvedSignature, expectedCallSite, "Consumer.Use"])
        }.Where(failure => failure is not null).ToList();
        if (expectedCallSite is null && TextOf(references).Contains("Aufruf von", StringComparison.Ordinal))
        {
            failures.Add($"find_references returned an unexpected constructor call site for {handoffId}.");
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
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
