#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Unit")]
public sealed class GetNamespaceTreeHandoffTests
{
    [Fact]
    public async Task ExecuteAsync_SourceTypeHandoff_IsReusableByGetSymbolBody()
    {
        using var fixture = new McpInMemoryTestContext();
        var server = fixture.CreateServer();

        var tree = await GetNamespaceTreeTool.ExecuteAsync(
            server,
            new GetNamespaceTreeInput(
                Project: "SymbolGraphMini",
                NamespacePrefix: "SymbolGraphMini",
                IncludeTypes: true),
            CancellationToken.None);

        var handoffId = TypeHandoffOf(TextOf(tree), "Greeter");
        var body = await GetSymbolBodyTool.ExecuteAsync(server, [handoffId], 80, CancellationToken.None);

        Assert.NotEqual(true, body.IsError);
        Assert.Contains("class Greeter", TextOf(body), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AssemblyTypeHandoff_IsReusableByGetSymbolBody()
    {
        using var snapshot = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\NamespaceTreeProbe.slnx",
            new ProjectSpec("NamespaceTreeProbe", [("Api.cs", "namespace Probe; public sealed class Api { public void Execute() { } }")]));
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                Catalog: null,
                ReadOnlySolutionSnapshot: snapshot.Solution,
                AssemblySymbolIdentity: AnalysisSymbolIdentity.ForAssembly(
                    @"C:\virtual\NamespaceTreeProbe.dll",
                    "A1B2C3D4E5F60718293A4B5C6D7E8F90123456789ABCDEF00123456789ABCDEF",
                    generation: 1))));

        var tree = await GetNamespaceTreeTool.ExecuteAsync(
            server,
            new GetNamespaceTreeInput(Project: "NamespaceTreeProbe", NamespacePrefix: "Probe", IncludeTypes: true),
            CancellationToken.None);

        var handoffId = TypeHandoffOf(TextOf(tree), "Api");
        var body = await GetSymbolBodyTool.ExecuteAsync(server, [handoffId], 80, CancellationToken.None);

        Assert.NotEqual(true, body.IsError);
        Assert.Contains("class Api", TextOf(body), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task ExecuteAsync_AssemblyWithoutFilters_ListsNamespaces(int depth, bool includeTypes)
    {
        using var snapshot = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\NamespaceTreeProbe.slnx",
            new ProjectSpec("NamespaceTreeProbe", [
                ("Api.cs", "namespace Probe.Api; public sealed class Api { }")
            ]));
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                Catalog: null,
                ReadOnlySolutionSnapshot: snapshot.Solution,
                AssemblySymbolIdentity: AnalysisSymbolIdentity.ForAssembly(
                    @"C:\virtual\NamespaceTreeProbe.dll",
                    "A1B2C3D4E5F60718293A4B5C6D7E8F90123456789ABCDEF00123456789ABCDEF",
                    generation: 1))));

        var tree = await GetNamespaceTreeTool.ExecuteAsync(
            server,
            new GetNamespaceTreeInput(Depth: depth, IncludeTypes: includeTypes),
            CancellationToken.None);

        Assert.NotEqual(true, tree.IsError);
        Assert.Contains("\n- Probe", TextOf(tree), StringComparison.Ordinal);
    }

    private static string TypeHandoffOf(string text, string typeName)
    {
        var match = Regex.Match(text, $@"- {typeName} \([^\r\n]+handoffId: `(?<id>h:[^`]+)`");
        Assert.True(match.Success, $"Expected reusable handoff for {typeName} in: {text}");
        return match.Groups["id"].Value;
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
