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
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies.Navigation;

[Trait("Category", "Integration")]
// @covers AssemblyAnalysisRegistry
// @covers AssemblySymbolResolver
// @covers AssemblyFindReferencesTool
public sealed class AssemblyHandoffLifecycleIntegrationTests
{
    [Fact]
    public async Task AssemblyHandoff_TransitiveOwnerReopensAfterEvictionAndRestartWithoutRootClosure()
    {
        using var temp = TestTempDirectory.Create("assembly-handoff-transitive-owner-");
        var leafPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TransitiveOwnerLeaf",
            "namespace Probe; public sealed class Leaf { public int Value => 1; }");
        var ownerPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TransitiveOwnerB",
            "namespace Probe; public sealed class Target { public int Read() => new Leaf().Value; }",
            leafPath);
        var bridgePath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TransitiveOwnerA",
            "namespace Probe; public sealed class Bridge { public int Read() => new Target().Read(); }",
            ownerPath);
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TransitiveOwnerRoot",
            "namespace Probe; public sealed class Root { public int Read() => new Bridge().Read(); }",
            bridgePath);

        string handoffId;
        var clock = new ManualTimeProvider();
        using var resources = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(
            IdleTtl: TimeSpan.FromMinutes(1),
            Clock: clock));
        await using var registry = new AssemblyAnalysisRegistry(resourceRegistry: resources);

        var owner = await registry.LeaseAsync(ownerPath);
        Assert.Null(owner.Error);
        using (var ownerLease = owner.Lease!)
        {
            handoffId = AnalysisSymbolIdentity.ForAssembly(
                    ownerLease.CanonicalPath,
                    ownerLease.Context.Origin.ContentHash,
                    ownerLease.Context.Generation)
                .FormatHandoff(LeaseSymbol(ownerLease, "Read"))!;
        }

        clock.Advance(TimeSpan.FromMinutes(2));
        var afterEviction = await registry.LeaseAsync(rootPath);
        Assert.Null(afterEviction.Error);
        using (var rootLease = afterEviction.Lease!)
        {
            await AssertTransitiveOwnerScopesAsync(rootLease, handoffId, bridgePath, ownerPath, leafPath);
        }

        await using var restartedRegistry = new AssemblyAnalysisRegistry();
        var afterRestart = await restartedRegistry.LeaseAsync(rootPath);
        Assert.Null(afterRestart.Error);
        using (var rootLease = afterRestart.Lease!)
        {
            await AssertTransitiveOwnerScopesAsync(rootLease, handoffId, bridgePath, ownerPath, leafPath);
        }
    }

    [Fact]
    public async Task AssemblyHandoff_SurvivesEvictionAndRestartButRejectsChangedOrForeignTarget()
    {
        using var temp = TestTempDirectory.Create("assembly-handoff-lifecycle-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "AssemblyHandoffLifecycleProbe",
            "namespace Probe; public sealed class Target { public int Read() => 1; }");
        var foreignPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "AssemblyHandoffForeignProbe",
            "namespace Probe; public sealed class Target { public int Read() => 2; }");
        string handoffId;

        var clock = new ManualTimeProvider();
        using var resources = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(
            IdleTtl: TimeSpan.FromMinutes(1),
            Clock: clock));
        await using var registry = new AssemblyAnalysisRegistry(resourceRegistry: resources);

        var first = await registry.LeaseAsync(assemblyPath);
        Assert.Null(first.Error);
        object? firstServer = null;
        using (var firstLease = first.Lease!)
        {
            firstServer = firstLease.Server;
            var symbol = LeaseSymbol(firstLease, "Read");
            handoffId = AnalysisSymbolIdentity.ForAssembly(
                    firstLease.CanonicalPath,
                    firstLease.Context.Origin.ContentHash,
                    firstLease.Context.Generation)
                .FormatHandoff(symbol)!;
        }

        clock.Advance(TimeSpan.FromMinutes(2));
        var afterEviction = await registry.LeaseAsync(assemblyPath);
        Assert.Null(afterEviction.Error);
        using (var reloadedLease = afterEviction.Lease!)
        {
            Assert.NotSame(firstServer, reloadedLease.Server);
            var afterEvictionResult = await AssemblyFindReferencesTool.ExecuteAsync(
                reloadedLease,
                new AssemblyFindReferencesRequest(handoffId, 10, 1, false),
                CancellationToken.None);
            Assert.False(afterEvictionResult.IsError == true, Text(afterEvictionResult));
            var bodyAfterEviction = await GetSymbolBodyTool.ExecuteAsync(
                reloadedLease,
                new GetSymbolBodyRequest([handoffId]),
                CancellationToken.None);
            Assert.False(bodyAfterEviction.IsError == true, Text(bodyAfterEviction));
        }

        await using (var restartedRegistry = new AssemblyAnalysisRegistry())
        {
            var restarted = await restartedRegistry.LeaseAsync(assemblyPath);
            Assert.Null(restarted.Error);
            using var restartedLease = restarted.Lease!;
            var afterRestart = await AssemblyFindReferencesTool.ExecuteAsync(
                restartedLease,
                new AssemblyFindReferencesRequest(handoffId, 10, 1, false),
                CancellationToken.None);
            Assert.False(afterRestart.IsError == true, Text(afterRestart));
            var bodyAfterRestart = await GetSymbolBodyTool.ExecuteAsync(
                restartedLease,
                new GetSymbolBodyRequest([handoffId]),
                CancellationToken.None);
            Assert.False(bodyAfterRestart.IsError == true, Text(bodyAfterRestart));

            var foreign = await restartedRegistry.LeaseAsync(foreignPath);
            Assert.Null(foreign.Error);
            using var foreignLease = foreign.Lease!;
            var wrongTarget = await AssemblyFindReferencesTool.ExecuteAsync(
                foreignLease,
                new AssemblyFindReferencesRequest(handoffId, 10, 1, false),
                CancellationToken.None);
            Assert.Equal("TARGET_MISMATCH", StructuredCode(wrongTarget));
        }

        var replacementPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "AssemblyHandoffChangedProbe",
            "namespace Probe; public sealed class Target { public int Read() => 3; }");
        File.Copy(replacementPath, assemblyPath, overwrite: true);

        await using var changedRegistry = new AssemblyAnalysisRegistry();
        var changed = await changedRegistry.LeaseAsync(assemblyPath);
        Assert.Null(changed.Error);
        using var changedLease = changed.Lease!;
        var stale = await AssemblyFindReferencesTool.ExecuteAsync(
            changedLease,
            new AssemblyFindReferencesRequest(handoffId, 10, 1, false),
            CancellationToken.None);
        Assert.Equal("STALE_SNAPSHOT", StructuredCode(stale));
    }

    private static ISymbol LeaseSymbol(AssemblyAnalysisLease lease, string memberName) =>
        Assert.Single(
            lease.Context.Compilation.GetTypeByMetadataName("Probe.Target")!.GetMembers(memberName));

    private static string StructuredCode(CallToolResult result) =>
        result.StructuredContent!.Value.GetProperty("code").GetString()!;

    private static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().SingleOrDefault()?.Text
        ?? result.ToString()
        ?? string.Empty;

    private static async Task AssertTransitiveOwnerScopesAsync(
        AssemblyAnalysisLease rootLease,
        string handoffId,
        string bridgePath,
        string ownerPath,
        string leafPath)
    {
        var ownerOnly = await AssemblyFindReferencesTool.ExecuteAsync(
            rootLease,
            new AssemblyFindReferencesRequest(handoffId, 10, 1, false),
            CancellationToken.None);

        Assert.False(ownerOnly.IsError == true, Text(ownerOnly));
        AssertNavigation(ownerOnly, "symbol_owner_only");
        var ownerLease = Assert.Single(rootLease.ReferenceLeasesSnapshot());
        Assert.Equal(Path.GetFullPath(ownerPath), ownerLease.CanonicalPath, ignoreCase: true);
        Assert.Empty(ownerLease.ReferenceLeasesSnapshot());
        Assert.DoesNotContain(
            ReferenceLeaseDescendants(rootLease),
            lease => string.Equals(lease.CanonicalPath, Path.GetFullPath(bridgePath), StringComparison.OrdinalIgnoreCase));

        var closure = await AssemblyFindReferencesTool.ExecuteAsync(
            rootLease,
            new AssemblyFindReferencesRequest(handoffId, 10, 1, true),
            CancellationToken.None);

        Assert.False(closure.IsError == true, Text(closure));
        AssertNavigation(closure, "bounded_reference_closure");
        Assert.Contains(
            ReferenceLeaseDescendants(rootLease),
            lease => string.Equals(lease.CanonicalPath, Path.GetFullPath(leafPath), StringComparison.OrdinalIgnoreCase));
    }

    private static System.Collections.Generic.IEnumerable<AssemblyAnalysisLease> ReferenceLeaseDescendants(
        AssemblyAnalysisLease lease)
    {
        foreach (var child in lease.ReferenceLeasesSnapshot())
        {
            yield return child;
            foreach (var descendant in ReferenceLeaseDescendants(child)) yield return descendant;
        }
    }

    private static void AssertNavigation(CallToolResult result, string effectiveSearchMode)
    {
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal(effectiveSearchMode, navigation.GetProperty("effectiveSearchMode").GetString());
        Assert.Equal(
            effectiveSearchMode == "bounded_reference_closure",
            navigation.GetProperty("requestedIncludeReferences").GetBoolean());
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }
}
