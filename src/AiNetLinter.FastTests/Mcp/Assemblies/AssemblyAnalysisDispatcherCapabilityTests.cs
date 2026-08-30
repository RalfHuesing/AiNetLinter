#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
// @covers AssemblyAnalysisDispatcher
// @covers AssemblyReferenceSessionExpander
public sealed class AssemblyAnalysisDispatcherCapabilityTests
{
    [Fact]
    public async Task AssemblyRoute_MissingReference_ReportsPartialSessionAndDiagnostic()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-missing-");
        var missing = new AssemblyReferenceDto(
            "MissingRouteDependency",
            "1.0.0.0",
            "neutral",
            Resolved: false,
            ResolutionState: "missing",
            Diagnostic: "missing-route-diagnostic");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, [missing]);

        var result = await fixture.ExecuteInspectAsync();

        var payload = Structured(result);
        AssertPartialStatusConsistency(result);
        Assert.Contains("missing-route-diagnostic", Diagnostics(payload));
        var session = Assert.Single(payload.GetProperty("referenceSessions").EnumerateArray());
        Assert.Equal("missing", session.GetProperty("sessionStatus").GetString());
        Assert.Contains("missing-route-diagnostic", session.GetProperty("diagnostics")[0].GetString());
    }

    [Fact]
    public async Task AssemblyRoute_CycleReference_ReportsTerminalSessionAndDiagnostic()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-cycle-");
        var cycle = new AssemblyReferenceDto(
            "CycleRouteDependency",
            "1.0.0.0",
            "neutral",
            Resolved: true,
            ResolvedPath: Path.Combine(temp.DirectoryPath, "CycleRouteDependency.dll"),
            ResolutionState: "cycle",
            Diagnostic: "cycle-route-diagnostic");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, [cycle]);

        var result = await fixture.ExecuteInspectAsync();

        var payload = Structured(result);
        AssertPartialStatusConsistency(result);
        Assert.Contains("cycle-route-diagnostic", Diagnostics(payload));
        var session = Assert.Single(payload.GetProperty("referenceSessions").EnumerateArray());
        Assert.Equal("cycle", session.GetProperty("sessionStatus").GetString());
    }

    [Fact]
    public async Task AssemblyRoute_NodeLimit_ReportsBoundedExpansionInsteadOfSilentSuccess()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-limit-");
        var references = Enumerable.Range(0, AssemblyReferenceResolver.MaxReferenceNodes + 1)
            .Select(index => new AssemblyReferenceDto(
                $"LimitRouteDependency{index:D4}",
                "1.0.0.0",
                "neutral",
                Resolved: true,
                ResolvedPath: Path.Combine(temp.DirectoryPath, $"LimitRouteDependency{index:D4}.dll"),
                Diagnostic: "limited-route-failure"))
            .ToArray();
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, references, FailingReferenceFactory);

        var result = await fixture.ExecuteInspectAsync();

        var payload = Structured(result);
        AssertPartialStatusConsistency(result);
        Assert.Equal(AssemblyReferenceResolver.MaxReferenceNodes + 1, payload.GetProperty("referenceSessions").GetArrayLength());
        Assert.Contains(Diagnostics(payload), diagnostic => diagnostic.Contains("Begrenzung von", StringComparison.Ordinal));
        Assert.Contains(
            payload.GetProperty("referenceSessions").EnumerateArray(),
            session => session.GetProperty("reference").GetProperty("resolutionState").GetString() == "node_limit");
    }

    [Fact]
    public async Task AssemblyRoute_ExtensionsIncludeReferenceLeaseDiagnostics()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-extensions-diagnostics-");
        var reference = new AssemblyReferenceDto(
            "FailedExtensionDependency",
            "1.0.0.0",
            "neutral",
            Resolved: true,
            ResolvedPath: Path.Combine(temp.DirectoryPath, "FailedExtensionDependency.dll"));
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, [reference], FailingReferenceFactory);

        var result = await fixture.ExecuteExtensionsAsync();

        var payload = Structured(result);
        AssertPartialStatusConsistency(result);
        Assert.Contains(Diagnostics(payload), diagnostic => diagnostic.Contains("FailedExtensionDependency", StringComparison.Ordinal));
        Assert.Equal("assembly", payload.GetProperty("analysis").GetProperty("targetType").GetString());
        Assert.Equal("decompiled", payload.GetProperty("analysis").GetProperty("origin").GetString());
        Assert.Contains(
            payload.GetProperty("analysis").GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetString()!.Contains("FailedExtensionDependency", StringComparison.Ordinal));
    }

    private static Task<AssemblyAnalysisLeaseResult> FailingReferenceFactory(
        AssemblyReferenceDto reference,
        CancellationToken cancellationToken) =>
        Task.FromResult(new AssemblyAnalysisLeaseResult(
            null,
            McpToolResults.Recoverable(
                LinterErrorCodes.AnalysisFailed,
                $"Testweise konnte die Referenz '{reference.Name}' nicht eröffnet werden.")));

    private static System.Text.Json.JsonElement Structured(CallToolResult result)
    {
        Assert.NotNull(result.StructuredContent);
        return result.StructuredContent!.Value;
    }

    private static IReadOnlyList<string> Diagnostics(System.Text.Json.JsonElement payload) =>
        payload.GetProperty("diagnostics").EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static void AssertPartialStatusConsistency(CallToolResult result)
    {
        var payload = Structured(result);
        Assert.Equal("partial", payload.GetProperty("sessionStatus").GetString());
        Assert.Equal("partial", payload.GetProperty("completeness").GetString());

        var analysis = payload.GetProperty("analysis");
        Assert.Equal("partial", analysis.GetProperty("status").GetString());
        Assert.Equal("partial", analysis.GetProperty("completeness").GetString());

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("status=partial", text, StringComparison.Ordinal);
        Assert.Contains("completeness=partial", text, StringComparison.Ordinal);
        Assert.Contains("Vollständigkeit: `partial`", text, StringComparison.Ordinal);
    }

    private sealed class SyntheticAssemblyFixture : IAsyncDisposable
    {
        private readonly AssemblyAnalysisRegistry backingRegistry;
        private readonly AssemblyAnalysisLease backingLease;
        private readonly AssemblyAnalysisEntry entry;
        private readonly SingleLeaseRegistry registry;

        private SyntheticAssemblyFixture(
            AssemblyAnalysisRegistry backingRegistry,
            AssemblyAnalysisLease backingLease,
            AssemblyAnalysisEntry entry,
            AssemblyAnalysisLease lease,
            string assemblyPath)
        {
            this.backingRegistry = backingRegistry;
            this.backingLease = backingLease;
            this.entry = entry;
            registry = new(lease);
            AssemblyPath = assemblyPath;
        }

        private string AssemblyPath { get; }

        internal static async Task<SyntheticAssemblyFixture> CreateAsync(
            TestTempDirectory temp,
            IReadOnlyList<AssemblyReferenceDto> references,
            AssemblyReferenceLeaseFactory? referenceLeaseFactory = null)
        {
            var assemblyPath = AssemblyTestHelper.EmitAssembly(
                temp,
                "DispatcherCapabilityProbe",
                "namespace Probe; public static class Probe { public static void Run() { } }");
            var backingRegistry = new AssemblyAnalysisRegistry();
            var backingResult = await backingRegistry.LeaseAsync(assemblyPath);
            Assert.Null(backingResult.Error);
            var backingLease = backingResult.Lease!;
            var context = backingLease.Context with
            {
                References = references,
                Diagnostics = Array.Empty<string>(),
            };
            var entry = AssemblyAnalysisEntry.Create(new AssemblyAnalysisEntryCreateParameters(
                backingLease.CanonicalPath,
                backingLease.Server.GetCurrentSolution()!,
                context,
                Lifetime: null,
                ReferenceLeaseFactory: referenceLeaseFactory));
            Assert.True(entry.TryAcquireLease(out var lease));
            return new SyntheticAssemblyFixture(backingRegistry, backingLease, entry, lease!, assemblyPath);
        }

        internal async Task<CallToolResult> ExecuteInspectAsync()
        {
            var route = AssemblyAnalysisDispatcher.CreateRoute(registry);
            return await AnalysisToolCall.ExecuteRouted(
                route,
                new AnalysisToolCallRequest(
                    new AnalysisTargetRequest("assembly", AssemblyPath),
                    new AnalysisToolDispatch(
                        AssemblySessionCall: lease => InspectAssemblyTool.ExecuteAsync(
                            lease,
                            new InspectAssemblyArguments(
                                lease.CanonicalPath,
                                null,
                                null,
                                null,
                                PublicOnly: true,
                                MaxResults: 100))),
                    CancellationToken.None));
        }

        internal async Task<CallToolResult> ExecuteExtensionsAsync()
        {
            var route = AssemblyAnalysisDispatcher.CreateRoute(registry);
            return await AnalysisToolCall.ExecuteRouted(
                route,
                new AnalysisToolCallRequest(
                    new AnalysisTargetRequest("assembly", AssemblyPath),
                    new AnalysisToolDispatch(
                        AssemblySessionCall: lease => FindAssemblyExtensionsTool.ExecuteAsync(
                            lease,
                            new FindAssemblyExtensionsArguments(
                                lease.CanonicalPath,
                                null,
                                null,
                                null,
                                100))),
                    CancellationToken.None));
        }

        public async ValueTask DisposeAsync()
        {
            registry.Lease.Dispose();
            await entry.DisposeAsync();
            backingLease.Dispose();
            await backingRegistry.DisposeAsync();
        }
    }

    private sealed class SingleLeaseRegistry(AssemblyAnalysisLease lease) : IAssemblyAnalysisRegistry
    {
        internal AssemblyAnalysisLease Lease { get; } = lease;

        public int ResidentCount => 1;

        public Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> SnapshotsAsync() =>
            Task.FromResult<IReadOnlyList<AssemblyAnalysisHealthSnapshot>>(Array.Empty<AssemblyAnalysisHealthSnapshot>());

        public Task<AssemblyAnalysisLeaseResult> LeaseAsync(
            string assemblyPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssemblyAnalysisLeaseResult(Lease, null));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
