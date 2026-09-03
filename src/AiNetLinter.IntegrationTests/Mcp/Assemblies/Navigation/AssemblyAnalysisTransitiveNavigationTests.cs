#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Assemblies.Analysis.SourceSelection;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies.Navigation;

[Trait("Category", "Integration")]
// @covers AssemblyNavigationSourceFactory
// @covers AssemblyReferenceNavigator
public sealed class AssemblyAnalysisTransitiveNavigationTests
{
    [Fact]
    public async Task AssemblyRoute_IncludeReferencesNavigatesTransitiveAssembliesAcrossAllSymbolGraphRoutes()
    {
        using var temp = TestTempDirectory.Create("assembly-route-transitive-symbol-graph-");
        var rootPath = EmitAssemblies(temp);
        var mapping = CreateMapping();
        using var snapshot = CreateSnapshot(temp, mapping);
        using var sourceRegistry = new SourceSnapshotRegistry();
        var orchestrator = CreateOrchestrator(temp, snapshot, sourceRegistry);
        await using var registry = new AssemblyAnalysisRegistry(orchestrator);
        var route = AssemblyAnalysisDispatcher.CreateRoute(registry);

        var totalAssemblyCount = await AssertSymbolRouteAsync(route, rootPath);
        await AssertTransitiveNavigationAsync(registry, rootPath, totalAssemblyCount);
    }

    private static string EmitAssemblies(TestTempDirectory temp)
    {
        var transitivePath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedTransitiveDependency",
            "namespace Probe; public sealed class TransitiveType { public void Touch() { } }");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedDirectDependency",
            "namespace Probe; public sealed class DependencyType { public int Read() { new TransitiveType().Touch(); return 3; } }",
            transitivePath);
        return AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedTransitiveRoot",
            "namespace Probe; public sealed class Root { public int Read() => new DependencyType().Read(); }",
            dependencyPath);
    }

    private static ExternalSourceMapping CreateMapping() =>
        new(
            "https://gitea.example/routed-transitive.git",
            "src/Routed.slnx",
            ["RoutedTransitiveRoot"]);

    private static ExternalSourceSnapshot CreateSnapshot(
        TestTempDirectory temp,
        ExternalSourceMapping mapping) =>
        ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec(
                "SourceRoot",
                "RoutedTransitiveRoot",
                "namespace Probe; public sealed class Root { public int Read() => new DependencyType().Read(); }",
                ["SourceDirect"]),
            new ExternalSourceProjectSpec(
                "SourceDirect",
                "RoutedDirectDependency",
                "namespace Probe; public sealed class DependencyType { public int Read() { new TransitiveType().Touch(); return 3; } }",
                ["SourceTransitive"]),
            new ExternalSourceProjectSpec(
                "SourceTransitive",
                "RoutedTransitiveDependency",
                "namespace Probe; public sealed class TransitiveType { public void Touch() { } }"));

    private static AssemblySourceSelectionOrchestrator CreateOrchestrator(
        TestTempDirectory temp,
        ExternalSourceSnapshot snapshot,
        SourceSnapshotRegistry sourceRegistry)
    {
        temp.CreateFile(
            "mappings.json",
            "{ \"repositories\": [{ \"url\": \"https://gitea.example/routed-transitive.git\", \"solutionPath\": \"src/Routed.slnx\", \"assemblies\": [\"RoutedTransitiveRoot\"] }] }");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": \"mappings.json\" } }");
        var configuration = ExternalSourceConfigurationLoader.Load(settingsPath);
        var provider = new AssemblyAnalysisRecordingProvider(
            new ExternalSourceProviderResult(true, [], snapshot));
        return new AssemblySourceSelectionOrchestrator(
            new(
                configuration.Succeeded,
                configuration.Configuration?.Mappings ?? [],
                configuration.Diagnostics),
            new AssemblySourceProviderCoordinator(provider, sourceRegistry));
    }

    private static async Task<int> AssertSymbolRouteAsync(
        AnalysisToolRoute route,
        string rootPath)
    {
        var result = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("assembly", rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(
                        lease,
                        new AssemblyFindSymbolRequest(["Touch"], "method", 50, true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        var totalAssemblyCount = navigation.GetProperty("totalAssemblyCount").GetInt32();
        Assert.InRange(totalAssemblyCount, 3, int.MaxValue);
        Assert.Equal(totalAssemblyCount, navigation.GetProperty("searchedAssemblyCount").GetInt32());
        Assert.Equal("partial", navigation.GetProperty("completeness").GetString());
        Assert.Contains(
            result.StructuredContent.Value.GetProperty("results")[0].GetProperty("matches").EnumerateArray(),
            match => match.GetProperty("name").GetString() == "Probe.TransitiveType.Touch()"
                && match.GetProperty("origin").GetProperty("canonicalPath").GetString()!
                    .EndsWith(Path.Combine("SourceTransitive", "SourceTransitive.csproj"), StringComparison.OrdinalIgnoreCase));
        return totalAssemblyCount;
    }

    private static async Task AssertTransitiveNavigationAsync(
        AssemblyAnalysisRegistry registry,
        string rootPath,
        int totalAssemblyCount)
    {
        var leaseResult = await registry.LeaseAsync(rootPath);
        Assert.Null(leaseResult.Error);
        using var rootLease = leaseResult.Lease!;
        await rootLease.ExpandReferencesAsync();
        var leaseSet = AssemblyNavigationLeaseAccess.GetLeases(rootLease);
        Assert.Equal(totalAssemblyCount, leaseSet.TotalAssemblyCount);
        var transitiveLease = Assert.Single(
            leaseSet.Leases,
            candidate => string.Equals(
                candidate.Context.Identity?.Name,
                "RoutedTransitiveDependency",
                StringComparison.Ordinal));
        var targetSymbol = Assert.Single(
            transitiveLease.Context.Compilation
                .GetTypeByMetadataName("Probe.TransitiveType")!
                .GetMembers("Touch").OfType<IMethodSymbol>());
        var sources = AssemblyNavigationSourceFactory.CreateSources(
            rootLease,
            new AssemblySymbolTarget(targetSymbol, transitiveLease));
        Assert.Contains(
            sources,
            source => source.CanonicalPath.EndsWith(
                Path.Combine("SourceDirect", "SourceDirect.csproj"),
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            sources,
            source => source.CanonicalPath.EndsWith(
                Path.Combine("SourceTransitive", "SourceTransitive.csproj"),
                StringComparison.OrdinalIgnoreCase));

        var navigation = AssemblyNavigationSupport.CreateSummary(
            new AssemblyNavigationSummaryRequest(
                leaseSet.TotalAssemblyCount,
                leaseSet.Leases.Count,
                leaseSet.AssembliesTruncated,
                []));
        var traversal = await AssemblyReferenceNavigator.FindReferencesAsync(
            new AssemblyReferenceTraversalRequest(sources, 50, 1, navigation),
            CancellationToken.None);
        Assert.Contains(
            traversal.CallSites,
            callSite => callSite.ProjectName == "SourceDirect"
                && callSite.Origin?.OriginKind == "source-backed");

        var (tree, truncated, diagnostics) = await AssemblyReferenceNavigator.BuildCallTreeAsync(
            sources,
            targetSymbol,
            new GetCallTreeInput("Probe.TransitiveType.Touch", 1, null, 10, null),
            CancellationToken.None);
        Assert.False(truncated);
        Assert.Empty(diagnostics);
        Assert.Contains(
            tree.Children,
            child => child.DisplayLine.Contains("SourceDirect", StringComparison.Ordinal)
                && child.DisplayLine.Contains("source-backed", StringComparison.Ordinal));
    }
}
