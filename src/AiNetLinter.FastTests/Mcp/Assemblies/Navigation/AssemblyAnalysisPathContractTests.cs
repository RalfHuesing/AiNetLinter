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

namespace AiNetLinter.FastTests.Mcp.Assemblies.Navigation;

[Trait("Category", "Component")]
// @covers AssemblyRoslynWorkspaceFactory
// @covers AssemblyDecompiledBodyResolver
// @covers DiffImpactAnalyzer
// @covers GetFileSkeletonTool
// @covers GetSymbolBodyTool
// @covers GetCallTreeTool
// @covers DependencyGraphTool
public sealed class AssemblyAnalysisPathContractTests
{
    [Fact]
    public async Task AssemblyRoute_ResolvesTopLevelTypeAndPropertyAccessorBodies()
    {
        using var temp = TestTempDirectory.Create("assembly-top-level-body-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TopLevelBodyProbe",
            """
            namespace Probe;
            public sealed class Document
            {
                private string name = "initial";
                public string Name
                {
                    get { return name; }
                    set { name = value; }
                }
            }
            public struct Structure { public int Number => 1; }
            public enum State { Ready }
            public interface IContract { void Run(); }
            public record Record(int Value);
            """);
        await using var registry = new AssemblyAnalysisRegistry();

        var leaseResult = await registry.LeaseAsync(assemblyPath);
        Assert.Null(leaseResult.Error);
        using var lease = leaseResult.Lease!;
        var type = lease.Context.Compilation.GetTypeByMetadataName("Probe.Document")!;
        var property = Assert.Single(type.GetMembers("Name").OfType<IPropertySymbol>());
        var getter = property.GetMethod!;
        var setter = property.SetMethod!;
        var structure = lease.Context.Compilation.GetTypeByMetadataName("Probe.Structure")!;
        var state = lease.Context.Compilation.GetTypeByMetadataName("Probe.State")!;
        var contract = lease.Context.Compilation.GetTypeByMetadataName("Probe.IContract")!;
        var record = lease.Context.Compilation.GetTypeByMetadataName("Probe.Record")!;

        var typeBody = await lease.ResolveBodyAsync(type, 80, CancellationToken.None);
        var propertyBody = await lease.ResolveBodyAsync(property, 80, CancellationToken.None);
        var getterBody = await lease.ResolveBodyAsync(getter, 80, CancellationToken.None);
        var setterBody = await lease.ResolveBodyAsync(setter, 80, CancellationToken.None);
        var structureBody = await lease.ResolveBodyAsync(structure, 80, CancellationToken.None);
        var stateBody = await lease.ResolveBodyAsync(state, 80, CancellationToken.None);
        var contractBody = await lease.ResolveBodyAsync(contract, 80, CancellationToken.None);
        var recordBody = await lease.ResolveBodyAsync(record, 80, CancellationToken.None);

        Assert.Equal("available", typeBody.BodyAvailability);
        Assert.Contains("class Document", typeBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", propertyBody.BodyAvailability);
        Assert.Contains("Name", propertyBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", getterBody.BodyAvailability);
        Assert.Contains("get", getterBody.Body, StringComparison.Ordinal);
        Assert.Contains("return name", getterBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", setterBody.BodyAvailability);
        Assert.Contains("set", setterBody.Body, StringComparison.Ordinal);
        Assert.Contains("name = value", setterBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", structureBody.BodyAvailability);
        Assert.Contains("struct Structure", structureBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", stateBody.BodyAvailability);
        Assert.Contains("enum State", stateBody.Body, StringComparison.Ordinal);
        Assert.Equal("unavailable", contractBody.BodyAvailability);
        Assert.Contains("Interfaces", contractBody.Hint, StringComparison.Ordinal);
        Assert.Equal("available", recordBody.BodyAvailability);
        Assert.Contains("Record", recordBody.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyRoute_NamespaceTreeUsesAssemblyOverviewHeader()
    {
        using var temp = TestTempDirectory.Create("assembly-namespace-header-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "NamespaceHeaderProbe",
            "namespace Probe; public sealed class Document { public int Value => 1; }");
        await using var registry = new AssemblyAnalysisRegistry();

        var result = await DispatchAsync(
            registry,
            assemblyPath,
            lease => GetNamespaceTreeTool.ExecuteAsync(
                lease.Server,
                new GetNamespaceTreeInput(),
                CancellationToken.None));

        var text = Text(result);
        Assert.Contains("# Assembly Overview: NamespaceHeaderProbe", text, StringComparison.Ordinal);
        Assert.DoesNotContain("# Solution Overview:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyRoute_ResolvesAccessorBodiesByAssociatedPropertyIndexerAndEvent()
    {
        using var temp = TestTempDirectory.Create("assembly-associated-accessor-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "AssociatedAccessorProbe",
            """
            namespace Probe;
            public sealed class Document
            {
                public int FirstValue
                {
                    get { return 11; }
                    set { }
                }
                public int SecondValue
                {
                    get { return 22; }
                    set { }
                }
                public int this[int index]
                {
                    get { return index == 0 ? 31 : 32; }
                    set { }
                }
                private System.EventHandler? firstHandler;
                public event System.EventHandler FirstChanged
                {
                    add { FirstMarker(); firstHandler += value; }
                    remove { firstHandler -= value; }
                }
                private System.EventHandler? secondHandler;
                public event System.EventHandler SecondChanged
                {
                    add { SecondMarker(); secondHandler += value; }
                    remove { secondHandler -= value; }
                }
                private static void FirstMarker() { }
                private static void SecondMarker() { }
            }
            """);
        await using var registry = new AssemblyAnalysisRegistry();

        var leaseResult = await registry.LeaseAsync(assemblyPath);
        Assert.Null(leaseResult.Error);
        using var lease = leaseResult.Lease!;
        var type = lease.Context.Compilation.GetTypeByMetadataName("Probe.Document")!;
        var firstProperty = Assert.Single(type.GetMembers("FirstValue").OfType<IPropertySymbol>());
        var secondProperty = Assert.Single(type.GetMembers("SecondValue").OfType<IPropertySymbol>());
        var indexer = Assert.Single(type.GetMembers().OfType<IPropertySymbol>().Where(property => property.IsIndexer));
        var firstEvent = Assert.Single(type.GetMembers("FirstChanged").OfType<IEventSymbol>());
        var secondEvent = Assert.Single(type.GetMembers("SecondChanged").OfType<IEventSymbol>());

        var firstPropertyBody = await lease.ResolveBodyAsync(firstProperty.GetMethod!, 80, CancellationToken.None);
        var secondPropertyBody = await lease.ResolveBodyAsync(secondProperty.GetMethod!, 80, CancellationToken.None);
        var indexerBody = await lease.ResolveBodyAsync(indexer.GetMethod!, 80, CancellationToken.None);
        var firstEventBody = await lease.ResolveBodyAsync(firstEvent.AddMethod!, 80, CancellationToken.None);
        var secondEventBody = await lease.ResolveBodyAsync(secondEvent.AddMethod!, 80, CancellationToken.None);

        Assert.Equal("available", firstPropertyBody.BodyAvailability);
        Assert.Contains("return 11", firstPropertyBody.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("return 22", firstPropertyBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", secondPropertyBody.BodyAvailability);
        Assert.Contains("return 22", secondPropertyBody.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("return 11", secondPropertyBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", indexerBody.BodyAvailability);
        Assert.Contains("31", indexerBody.Body, StringComparison.Ordinal);
        Assert.Contains("32", indexerBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", firstEventBody.BodyAvailability);
        Assert.Contains("add", firstEventBody.Body, StringComparison.Ordinal);
        Assert.Contains("FirstMarker", firstEventBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", secondEventBody.BodyAvailability);
        Assert.Contains("add", secondEventBody.Body, StringComparison.Ordinal);
        Assert.Contains("SecondMarker", secondEventBody.Body, StringComparison.Ordinal);
        Assert.NotEqual(firstEventBody.Body, secondEventBody.Body);
    }

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
        Assert.Contains($"id:{methodId}", skeletonText, StringComparison.Ordinal);

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
        Assert.Null(snapshot.Solution.FilePath);
        Assert.Equal(
            Path.Combine(temp.DirectoryPath, "BarePathProbe.csproj"),
            Assert.Single(snapshot.Solution.Projects).FilePath,
            StringComparer.OrdinalIgnoreCase);

        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                ReadOnlySolutionSnapshot: snapshot.Solution,
                AssemblySymbolIdentity: new AnalysisSymbolIdentity(contentHash, 1))));
        Assert.Equal(new AnalysisSymbolIdentity(contentHash, 1), server.AssemblySymbolIdentity);
        var method = Assert.Single(
            snapshot.Compilation.GetTypeByMetadataName("Probe.Document")!.GetMembers("Save").OfType<IMethodSymbol>());
        var methodId = new AnalysisSymbolIdentity(contentHash, 1)
            .Format(DocumentationCommentId.CreateDeclarationId(method))!;

        var skeleton = await GetFileSkeletonTool.ExecuteAsync(server, ["Document.cs"], CancellationToken.None);
        Assert.NotEqual(true, skeleton.IsError);
        Assert.Contains($"id:{methodId}", Text(skeleton), StringComparison.Ordinal);

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
        Assert.NotEqual(true, foreignResult.IsError);
        Assert.Contains("aktuellen Assembly-Generation", Text(foreignResult), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyRoute_ResolvesOverloadedBodyByCompleteParameterSignature()
    {
        using var temp = TestTempDirectory.Create("assembly-overload-body-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "OverloadProbe",
            """
            namespace Probe;
            public sealed class Converter
            {
                public string Convert(int value) => "wrong-int";
                public string Convert(string value) => "selected-string";
            }
            """);
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        Assert.Null(leaseResult.Error);
        using var lease = leaseResult.Lease!;
        var converter = lease.Context.Compilation.GetTypeByMetadataName("Probe.Converter")!;
        var method = Assert.Single(converter.GetMembers("Convert").OfType<IMethodSymbol>(), symbol =>
            symbol.Parameters.Single().Type.SpecialType == SpecialType.System_String);
        var methodId = new AnalysisSymbolIdentity(lease.Context.Origin.ContentHash, lease.Context.Generation)
            .Format(DocumentationCommentId.CreateDeclarationId(method))!;

        var result = await GetSymbolBodyTool.ExecuteAsync(lease, [methodId], 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Text(result);
        Assert.Contains("selected-string", text, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong-int", text, StringComparison.Ordinal);
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
