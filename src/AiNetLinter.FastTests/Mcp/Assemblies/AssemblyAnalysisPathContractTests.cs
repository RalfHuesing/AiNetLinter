#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
// @covers AssemblyRoslynWorkspaceFactory
// @covers DiffImpactAnalyzer
// @covers GetFileSkeletonTool
// @covers GetSymbolBodyTool
// @covers GetCallTreeTool
// @covers DependencyGraphTool
public sealed class AssemblyAnalysisPathContractTests
{
    [Fact]
    public async Task AssemblyRoute_ResolvesGeneratedDocumentAndStableParameterMethodAcrossTools()
    {
        using var temp = TestTempDirectory.Create("assembly-path-contract-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "PathContractProbe",
            """
            namespace Probe;
            public sealed class Document
            {
                public bool Save(bool value) => value;
            }
            """);
        await using var registry = new AssemblyAnalysisRegistry();

        var leaseResult = await registry.LeaseAsync(assemblyPath);
        Assert.Null(leaseResult.Error);
        using var lease = leaseResult.Lease!;
        var solution = lease.Server.GetCurrentSolution()!;
        var document = Assert.Single(solution.Projects.Single().Documents);
        var type = lease.Context.Compilation.GetTypeByMetadataName("Probe.Document")!;
        var method = Assert.Single(type.GetMembers("Save").OfType<IMethodSymbol>());
        var identity = new AnalysisSymbolIdentity(lease.Context.Origin.ContentHash, lease.Context.Generation);
        var methodId = identity.Format(DocumentationCommentId.CreateDeclarationId(method))!;
        var typeId = identity.Format(DocumentationCommentId.CreateDeclarationId(type))!;

        var skeleton = await DispatchAsync(
            registry,
            assemblyPath,
            activeLease => GetFileSkeletonTool.ExecuteAsync(activeLease.Server, [document.Name], CancellationToken.None));
        var skeletonText = Text(skeleton);
        Assert.NotEqual(true, skeleton.IsError);
        Assert.Contains($"id: {methodId}", skeletonText, StringComparison.Ordinal);

        var body = await DispatchAsync(
            registry,
            assemblyPath,
            activeLease => GetSymbolBodyTool.ExecuteAsync(activeLease.Server, [methodId], 80, CancellationToken.None));
        var bodyText = Text(body);
        Assert.NotEqual(true, body.IsError);
        Assert.Contains($"id: `{methodId}`", bodyText, StringComparison.Ordinal);
        Assert.Contains("Save(bool value)", bodyText, StringComparison.Ordinal);

        var callTree = await DispatchAsync(
            registry,
            assemblyPath,
            activeLease => GetCallTreeTool.ExecuteAsync(
                activeLease.Server,
                new GetCallTreeInput(methodId, 1, null, 10, "outgoing"),
                CancellationToken.None));
        Assert.NotEqual(true, callTree.IsError);
        Assert.DoesNotContain("Unerwarteter Fehler in get_call_tree", Text(callTree), StringComparison.Ordinal);

        var dependencyGraph = await DispatchAsync(
            registry,
            assemblyPath,
            activeLease => DependencyGraphTool.ExecuteAsync(
                activeLease.Server,
                new DependencyGraphInput(null, typeId, "outgoing", 1, 10),
                CancellationToken.None));
        Assert.NotEqual(true, dependencyGraph.IsError);
        Assert.DoesNotContain("Unerwarteter Fehler in dependency_graph", Text(dependencyGraph), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BareGeneratedDocumentPath_UsesAssemblyDirectoryAndRejectsForeignStableId()
    {
        using var temp = TestTempDirectory.Create("assembly-bare-path-");
        var assemblyPath = temp.GetPath("BarePathProbe.dll");
        var contentHash = new string('a', 64);
        var request = new AssemblyWorkspaceRequest(
            assemblyPath,
            new AssemblyFingerprint(Path.GetFullPath(assemblyPath), 0, DateTime.UtcNow, contentHash),
            [new DecompiledDocument(
                "Document.cs",
                "Probe.Document",
                "namespace Probe; public sealed class Document { public bool Save(bool value) => value; }")],
            Array.Empty<MetadataReference>(),
            AssemblySessionStatus.Complete);

        using var snapshot = await new AssemblyRoslynWorkspaceFactory().CreateAsync(
            request,
            "BarePathProbe",
            contentHash,
            CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Solution.FilePath));
        Assert.Equal(
            Path.Combine(temp.DirectoryPath, "BarePathProbe.csproj"),
            snapshot.Solution.FilePath,
            StringComparer.OrdinalIgnoreCase);

        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                ReadOnlySolutionSnapshot: snapshot.Solution,
                AssemblySymbolIdentity: new AnalysisSymbolIdentity(contentHash, 1))));
        var method = Assert.Single(
            snapshot.Compilation.GetTypeByMetadataName("Probe.Document")!.GetMembers("Save").OfType<IMethodSymbol>());
        var methodId = new AnalysisSymbolIdentity(contentHash, 1)
            .Format(DocumentationCommentId.CreateDeclarationId(method))!;

        var skeleton = await GetFileSkeletonTool.ExecuteAsync(server, ["Document.cs"], CancellationToken.None);
        Assert.NotEqual(true, skeleton.IsError);
        Assert.Contains($"id: {methodId}", Text(skeleton), StringComparison.Ordinal);

        var body = await GetSymbolBodyTool.ExecuteAsync(server, [methodId], 80, CancellationToken.None);
        Assert.NotEqual(true, body.IsError);

        var callTree = await GetCallTreeTool.ExecuteAsync(
            server,
            new GetCallTreeInput(methodId, 1, null, 10, "outgoing"),
            CancellationToken.None);
        Assert.NotEqual(true, callTree.IsError);

        var dependencyGraph = await DependencyGraphTool.ExecuteAsync(
            server,
            new DependencyGraphInput(null, methodId, "outgoing", 1, 10),
            CancellationToken.None);
        Assert.NotEqual(true, dependencyGraph.IsError);

        var foreignId = new AnalysisSymbolIdentity(new string('b', 64), 1)
            .Format(DocumentationCommentId.CreateDeclarationId(method))!;
        var foreignResult = await GetSymbolBodyTool.ExecuteAsync(server, [foreignId], 80, CancellationToken.None);
        Assert.Equal(true, foreignResult.IsError);
        Assert.Contains("aktuellen Assembly-Generation", Text(foreignResult), StringComparison.Ordinal);
    }

    private static async Task<CallToolResult> DispatchAsync(
        AssemblyAnalysisRegistry registry,
        string assemblyPath,
        Func<AssemblyAnalysisLease, Task<CallToolResult>> call)
    {
        return await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("assembly", assemblyPath),
                new AnalysisToolDispatch(AssemblySessionCall: call),
                CancellationToken.None));
    }

    private static string Text(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
