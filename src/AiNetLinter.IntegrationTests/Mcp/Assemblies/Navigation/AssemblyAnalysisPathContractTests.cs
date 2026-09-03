#nullable enable

using System;
using System.IO;
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

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies.Navigation;

[Trait("Category", "Integration")]
// @covers AssemblyRoslynWorkspaceFactory
// @covers DiffImpactAnalyzer
// @covers GetFileSkeletonTool
// @covers GetSymbolBodyTool
// @covers GetCallTreeTool
// @covers DependencyGraphTool
// @covers DecompiledProjectPaths
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
        var structure = lease.Context.Compilation.GetTypeByMetadataName("Probe.Structure")!;
        var state = lease.Context.Compilation.GetTypeByMetadataName("Probe.State")!;
        var contract = lease.Context.Compilation.GetTypeByMetadataName("Probe.IContract")!;
        var record = lease.Context.Compilation.GetTypeByMetadataName("Probe.Record")!;

        var typeBody = await GetBodyTextAsync(lease, type);
        var propertyBody = await GetBodyTextAsync(lease, property);
        var structureBody = await GetBodyTextAsync(lease, structure);
        var stateBody = await GetBodyTextAsync(lease, state);
        var contractBody = await GetBodyTextAsync(lease, contract);
        var recordBody = await GetBodyTextAsync(lease, record);

        Assert.Contains("bodyAvailability: `available`; contentMode: `decompiledProject`", typeBody, StringComparison.Ordinal);
        Assert.Contains("class Document", typeBody, StringComparison.Ordinal);
        Assert.Contains("Name", propertyBody, StringComparison.Ordinal);
        Assert.Contains("get", propertyBody, StringComparison.Ordinal);
        Assert.Contains("return name", propertyBody, StringComparison.Ordinal);
        Assert.Contains("set", propertyBody, StringComparison.Ordinal);
        Assert.Contains("name = value", propertyBody, StringComparison.Ordinal);
        Assert.Contains("struct Structure", structureBody, StringComparison.Ordinal);
        Assert.Contains("enum State", stateBody, StringComparison.Ordinal);
        Assert.Contains("bodyAvailability: `unavailable`; contentMode: `decompiledProject`", contractBody, StringComparison.Ordinal);
        Assert.Contains("Interfaces", contractBody, StringComparison.Ordinal);
        Assert.Contains("bodyAvailability: `available`; contentMode: `decompiledProject`", recordBody, StringComparison.Ordinal);
        Assert.Contains("Record", recordBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyRoute_BodyProvenanceMatchesDecompiledAnalysisEnvelope()
    {
        using var temp = TestTempDirectory.Create("assembly-body-provenance-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "BodyProvenanceProbe",
            "namespace Probe; public sealed class Document { public int Read() => 42; }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        Assert.Null(leaseResult.Error);
        using var lease = leaseResult.Lease!;
        var method = Assert.Single(
            lease.Context.Compilation.GetTypeByMetadataName("Probe.Document")!.GetMembers("Read").OfType<IMethodSymbol>());
        var methodId = new AnalysisSymbolIdentity(lease.Context.Origin.ContentHash, lease.Context.Generation)
            .Format(DocumentationCommentId.CreateDeclarationId(method))!;

        var result = await DispatchAsync(
            registry,
            assemblyPath,
            activeLease => GetSymbolBodyTool.ExecuteAsync(activeLease, [methodId], 80, CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        Assert.Equal("decompiledProject", payload.GetProperty("results")[0].GetProperty("contentMode").GetString());
        Assert.Equal("decompiledProject", payload.GetProperty("analysis").GetProperty("contentMode").GetString());
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
    public async Task AssemblyRoute_ResolvesPropertyIndexerAndEventBodies()
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

        var firstPropertyBody = await GetBodyTextAsync(lease, firstProperty);
        var secondPropertyBody = await GetBodyTextAsync(lease, secondProperty);
        var indexerBody = await GetBodyTextAsync(lease, indexer);
        var firstEventBody = await GetBodyTextAsync(lease, firstEvent);
        var secondEventBody = await GetBodyTextAsync(lease, secondEvent);

        Assert.Contains("return 11", firstPropertyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("return 22", firstPropertyBody, StringComparison.Ordinal);
        Assert.Contains("return 22", secondPropertyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("return 11", secondPropertyBody, StringComparison.Ordinal);
        Assert.Contains("31", indexerBody, StringComparison.Ordinal);
        Assert.Contains("32", indexerBody, StringComparison.Ordinal);
        Assert.Contains("add", firstEventBody, StringComparison.Ordinal);
        Assert.Contains("FirstMarker", firstEventBody, StringComparison.Ordinal);
        Assert.Contains("add", secondEventBody, StringComparison.Ordinal);
        Assert.Contains("SecondMarker", secondEventBody, StringComparison.Ordinal);
        Assert.NotEqual(firstEventBody, secondEventBody);
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
                public bool Save(bool includeSub = false, bool saveAll = false) => includeSub || saveAll;
            }
            """);
        await using var registry = new AssemblyAnalysisRegistry();

        var leaseResult = await registry.LeaseAsync(assemblyPath);
        Assert.Null(leaseResult.Error);
        using var lease = leaseResult.Lease!;
        var solution = lease.Server.GetCurrentSolution()!;
        var document = Assert.Single(
            solution.Projects.Single().Documents.Where(document =>
                string.Equals(document.Name, "Document.cs", StringComparison.OrdinalIgnoreCase)));
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
        Assert.Contains("Save(bool includeSub = false, bool saveAll = false)", bodyText, StringComparison.Ordinal);
        Assert.Contains(Path.GetFullPath(document.FilePath!), bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("decompiledProjectDirectory", bodyText, StringComparison.Ordinal);

        var findSymbol = await DispatchAsync(
            registry,
            assemblyPath,
            activeLease => AssemblyFindSymbolTool.ExecuteAsync(
                activeLease,
                new AssemblyFindSymbolRequest(["Document"], "class", 10, false),
                CancellationToken.None));
        Assert.NotEqual(true, findSymbol.IsError);
        Assert.Contains(Path.GetFullPath(document.FilePath!), Text(findSymbol), StringComparison.OrdinalIgnoreCase);
        var findSymbolPayload = findSymbol.StructuredContent!.Value;
        var location = Assert.Single(
            findSymbolPayload.GetProperty("results")[0].GetProperty("matches").EnumerateArray());
        var locationPath = location.GetProperty("filePath").GetString();
        Assert.True(Path.IsPathFullyQualified(locationPath!));
        Assert.True(File.Exists(locationPath));
        Assert.Equal(Path.GetFullPath(document.FilePath!), Path.GetFullPath(locationPath!), StringComparer.OrdinalIgnoreCase);

        var callTree = await DispatchAsync(
            registry,
            assemblyPath,
            activeLease => GetCallTreeTool.ExecuteAsync(
                activeLease.Server,
                new GetCallTreeInput(methodId, 1, null, 10, "outgoing"),
                CancellationToken.None));
        Assert.NotEqual(true, callTree.IsError);
        Assert.Contains(Path.GetFullPath(document.FilePath!), Text(callTree), StringComparison.OrdinalIgnoreCase);
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

    private static async Task<string> GetBodyTextAsync(AssemblyAnalysisLease lease, ISymbol symbol)
    {
        var declarationId = DocumentationCommentId.CreateDeclarationId(symbol);
        Assert.NotNull(declarationId);
        var identity = new AnalysisSymbolIdentity(lease.Context.Origin.ContentHash, lease.Context.Generation);
        var result = await GetSymbolBodyTool.ExecuteAsync(
            lease,
            [identity.Format(declarationId!)!],
            80,
            CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        return Text(result);
    }
}
