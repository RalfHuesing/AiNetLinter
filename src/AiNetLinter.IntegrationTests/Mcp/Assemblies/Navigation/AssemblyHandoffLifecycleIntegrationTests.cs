#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
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

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }
}
