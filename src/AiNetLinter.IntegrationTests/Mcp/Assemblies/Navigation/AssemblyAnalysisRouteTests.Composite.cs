#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies.Navigation;

public sealed partial class AssemblyAnalysisRouteTests
{
    [Fact]
    public async Task AssemblyContext_IncludeReferencesControlsEverySymbolCompositeSection()
    {
        using var temp = TestTempDirectory.Create("assembly-context-composite-reference-scope-");
        var referencePath = AssemblyTestHelper.EmitAssembly(temp, "AssemblyContextReference", "namespace Probe; public sealed class ReferenceOnly { public int Read() => 42; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(temp, "AssemblyContextRoot", "namespace Probe; public sealed class Root { public int Read() => new ReferenceOnly().Read(); }", referencePath);
        await using var registry = new AssemblyAnalysisRegistry();
        var route = AssemblyAnalysisDispatcher.CreateRoute(registry);
        Task<ModelContextProtocol.Protocol.CallToolResult> ExecuteAsync(bool includeReferences) => AnalysisToolCall.ExecuteRouted(route, new AnalysisToolCallRequest(new AnalysisTargetRequest(rootPath), new AnalysisToolDispatch(AssemblySessionCall: lease => AssemblyAnalysisContextTool.ExecuteAsync(lease, new AssemblyAnalysisContextArguments("Probe.ReferenceOnly.Read", true, includeReferences, false, false, true, true, 100, 80, 100, 1, 10, 0, null, null), CancellationToken.None), ExpandAssemblyReferences: includeReferences), CancellationToken.None));
        var rootOnly = (await ExecuteAsync(false)).StructuredContent!.Value;
        Assert.Equal("root", rootOnly.GetProperty("scope").GetString());
        AssertCompositeSectionErrors(rootOnly);
        var expanded = (await ExecuteAsync(true)).StructuredContent!.Value;
        Assert.Equal("root+references", expanded.GetProperty("scope").GetString());
        Assert.Equal("NOT_CONFIGURED", expanded.GetProperty("metrics").GetProperty("code").GetString());
        Assert.NotEmpty(expanded.GetProperty("body").GetProperty("results").EnumerateArray());
        Assert.Equal("Probe.ReferenceOnly", expanded.GetProperty("classStructure").GetProperty("typeName").GetString());
    }

    [Fact]
    public async Task AssemblyRoute_ResolvesRootReferenceAndAllowsLazyTransitiveTarget()
    {
        using var temp = TestTempDirectory.Create("assembly-route-reference-target-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(temp, "RoutedReferenceTarget", "namespace Probe; public sealed class DependencyType { public int Value => 1; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(temp, "RoutedReferenceRoot", "namespace Probe; public sealed class Root { public DependencyType Value { get; } = new(); }", dependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();
        var rootResult = await AnalysisToolCall.ExecuteRouted(AssemblyAnalysisDispatcher.CreateRoute(registry), new AnalysisToolCallRequest(new AnalysisTargetRequest(rootPath), new AnalysisToolDispatch(AssemblySessionCall: lease => InspectAssemblyTool.ExecuteAsync(lease, new InspectAssemblyArguments(lease.CanonicalPath, null, null, null, true, 100)), ExpandAssemblyReferences: true), CancellationToken.None));
        Assert.NotNull(rootResult.StructuredContent);
        var reference = Assert.Single(rootResult.StructuredContent!.Value.GetProperty("references").EnumerateArray(), item => item.GetProperty("name").GetString() == "RoutedReferenceTarget");
        Assert.Equal(Path.GetFullPath(dependencyPath), reference.GetProperty("resolvedPath").GetString(), StringComparer.OrdinalIgnoreCase);
        var dependencySession = Assert.Single(rootResult.StructuredContent.Value.GetProperty("referenceSessions").EnumerateArray(), item => item.GetProperty("reference").GetProperty("name").GetString() == "RoutedReferenceTarget");
        Assert.Equal("RoutedReferenceTarget", dependencySession.GetProperty("identity").GetProperty("name").GetString());
        Assert.Equal("complete", dependencySession.GetProperty("sessionStatus").GetString());
        Assert.Contains("RoutedReferenceTarget", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(rootResult.Content)).Text, StringComparison.Ordinal);
        Assert.Equal(1, registry.ResidentCount);
    }

    [Fact]
    public async Task AssemblyRoute_IncludeReferencesNavigatesSymbolsReferencesAndCallTree()
    {
        using var temp = TestTempDirectory.Create("assembly-route-symbol-graph-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(temp, "RoutedSymbolDependency", "namespace Probe; public sealed class DependencyType { public int Value => 1; public int Read() => Value; }");
        var secondDependencyPath = AssemblyTestHelper.EmitAssembly(temp, "RoutedSymbolSecondDependency", "namespace Probe; public sealed class ExclusiveDependencyType { public int Value => 2; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(temp, "RoutedSymbolRoot", "namespace Probe; public sealed class Root { public int Read() => new DependencyType().Read(); public int ReadOther() => new ExclusiveDependencyType().Value; }", dependencyPath, secondDependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();
        var route = AssemblyAnalysisDispatcher.CreateRoute(registry);
        var symbolResult = await AnalysisToolCall.ExecuteRouted(route, new AnalysisToolCallRequest(new AnalysisTargetRequest(rootPath), new AnalysisToolDispatch(AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(lease, new AssemblyFindSymbolRequest(["ExclusiveDependencyType"], null, 50, true), CancellationToken.None), ExpandAssemblyReferences: true), CancellationToken.None));
        Assert.NotEqual(true, symbolResult.IsError);
        var symbolPayload = symbolResult.StructuredContent!.Value;
        var symbolMatch = Assert.Single(symbolPayload.GetProperty("results")[0].GetProperty("matches").EnumerateArray());
        Assert.Equal("Probe.ExclusiveDependencyType", symbolMatch.GetProperty("name").GetString());
        Assert.Equal("decompiled", symbolMatch.GetProperty("origin").GetProperty("originKind").GetString());
        Assert.True(symbolPayload.GetProperty("navigation").GetProperty("includeReferences").GetBoolean());
        Assert.True(symbolPayload.GetProperty("navigation").GetProperty("totalAssemblyCount").GetInt32() >= 3);
        Assert.Equal("partial", symbolPayload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
        Assert.NotEmpty(symbolPayload.GetProperty("navigation").GetProperty("diagnostics").EnumerateArray());
        var impactResult = await AnalysisToolCall.ExecuteRouted(route, new AnalysisToolCallRequest(new AnalysisTargetRequest(rootPath), new AnalysisToolDispatch(AssemblySessionCall: lease => GetImpactTool.ExecuteAsync(lease, new GetImpactInput(null, "Probe.Root.Read", 50, 1), CancellationToken.None), ExpandAssemblyReferences: true), CancellationToken.None));
        Assert.NotEqual(true, impactResult.IsError);
        var impactPayload = impactResult.StructuredContent!.Value;
        Assert.Equal("decompiled", impactPayload.GetProperty("analysis").GetProperty("origin").GetString());
        Assert.DoesNotContain("ASSEMBLY_TARGET_UNSUPPORTED", Text(impactResult), StringComparison.Ordinal);
        var referenceResult = await AnalysisToolCall.ExecuteRouted(route, new AnalysisToolCallRequest(new AnalysisTargetRequest(rootPath), new AnalysisToolDispatch(AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(lease, new AssemblyFindReferencesRequest("Probe.DependencyType.Read", 50, 1, true), CancellationToken.None), ExpandAssemblyReferences: true), CancellationToken.None));
        Assert.True(referenceResult.IsError != true, string.Join("\n", referenceResult.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(block => block.Text)));
        var referencePayload = referenceResult.StructuredContent!.Value;
        Assert.True(referencePayload.GetProperty("navigation").GetProperty("includeReferences").GetBoolean());
        Assert.True(referencePayload.GetProperty("navigation").GetProperty("requestedIncludeReferences").GetBoolean());
        Assert.Equal("bounded_reference_closure", referencePayload.GetProperty("navigation").GetProperty("effectiveSearchMode").GetString());
        Assert.Equal("partial", referencePayload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
        Assert.True(referencePayload.TryGetProperty("callSites", out _));
        var referenceText = Text(referenceResult);
        Assert.DoesNotContain("handoff=", referenceText, StringComparison.Ordinal);
        Assert.DoesNotContain("id: `a:", referenceText, StringComparison.Ordinal);
        var callSites = referencePayload.GetProperty("callSites");
        if (callSites.GetArrayLength() > 0)
        {
            var referenceCallSite = callSites[0];
            Assert.StartsWith("a:", referenceCallSite.GetProperty("id").GetString(), StringComparison.Ordinal);
            Assert.Equal("member", referenceCallSite.GetProperty("handoffKind").GetString());
            Assert.False(referenceCallSite.TryGetProperty("targetPath", out _));
            Assert.False(referenceCallSite.TryGetProperty("snapshot", out _));
            Assert.False(referenceCallSite.TryGetProperty("allowedFollowUpTools", out _));
            Assert.False(referenceCallSite.TryGetProperty("origin", out _));
        }
        var treeResult = await AnalysisToolCall.ExecuteRouted(route, new AnalysisToolCallRequest(new AnalysisTargetRequest(rootPath), new AnalysisToolDispatch(AssemblySessionCall: lease => AssemblyGetCallTreeTool.ExecuteAsync(lease, new AssemblyGetCallTreeRequest(new GetCallTreeInput("Probe.DependencyType.Read", 2, null, 10, null), true), CancellationToken.None), ExpandAssemblyReferences: true), CancellationToken.None));
        Assert.NotEqual(true, treeResult.IsError);
        Assert.Contains("assembly=", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(treeResult.Content)).Text, StringComparison.Ordinal);
        var treePayload = treeResult.StructuredContent!.Value;
        Assert.True(treePayload.GetProperty("navigation").GetProperty("includeReferences").GetBoolean());
        var graph = treePayload.GetProperty("graph");
        Assert.NotEmpty(graph.GetProperty("nodes").EnumerateArray());
        Assert.True(graph.TryGetProperty("edges", out _));
    }
}
