#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
// @covers AssemblyAnalysisDispatcher
// @covers AssemblyReferenceSessionExpander
public sealed partial class AssemblyAnalysisDispatcherCapabilityTests
{
    [Fact]
    public async Task AssemblyRoute_SkipsReferenceExpansionWhenHandlerDoesNotRequestIt()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-root-only-");
        var reference = new AssemblyReferenceDto(
            "UnrequestedDependency",
            "1.0.0.0",
            "neutral",
            Resolved: false,
            ResolutionState: "missing",
            Diagnostic: "must-not-be-expanded");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, [reference]);

        var result = await fixture.ExecuteRootOnlyAsync(lease =>
        {
            Assert.Empty(lease.ReferenceSessions);
            Assert.Empty(lease.ReferenceExpansionDiagnostics);
            return Task.FromResult(McpToolResults.Text("root-only"));
        });

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("root-only", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

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
        var referenceSummary = payload.GetProperty("referenceSummary");
        Assert.Equal(AssemblyReferenceResolver.MaxReferenceNodes + 1, referenceSummary.GetProperty("totalReferenceCount").GetInt32());
        var shownReferences = payload.GetProperty("references").GetArrayLength();
        Assert.InRange(shownReferences, 1, AssemblyAnalysisResponseLimits.MaxReferences);
        Assert.Equal(shownReferences, referenceSummary.GetProperty("shownReferenceCount").GetInt32());
        Assert.True(referenceSummary.GetProperty("referencesTruncated").GetBoolean());
        var shownSessions = payload.GetProperty("referenceSessions").GetArrayLength();
        Assert.InRange(shownSessions, 1, AssemblyAnalysisResponseLimits.MaxReferenceSessions);
        Assert.Equal(shownSessions, referenceSummary.GetProperty("shownReferenceSessionCount").GetInt32());
        Assert.Equal(AssemblyReferenceResolver.MaxReferenceNodes + 1, referenceSummary.GetProperty("totalReferenceSessionCount").GetInt32());
        Assert.True(referenceSummary.GetProperty("referenceSessionsTruncated").GetBoolean());
        Assert.Contains(Diagnostics(payload), diagnostic => diagnostic.Contains("limited-route-failure", StringComparison.Ordinal));
        Assert.All(
            payload.GetProperty("referenceSessions").EnumerateArray(),
            session => Assert.Equal("partial", session.GetProperty("sessionStatus").GetString()));
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
        Assert.False(payload.GetProperty("analysis").TryGetProperty("diagnostics", out _));
        Assert.False(payload.GetProperty("analysis").TryGetProperty("diagnosticsSummary", out _));
    }

    [Fact]
    public async Task AssemblyRoute_AggregatesRootAndTransitiveDiagnosticsWithSharedSamples()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-diagnostics-");
        var reference = new AssemblyReferenceDto(
            "TransitiveDiagnosticDependency",
            "1.0.0.0",
            "neutral",
            Resolved: true,
            ResolvedPath: Path.Combine(temp.DirectoryPath, "TransitiveDiagnosticDependency.dll"));
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [reference],
            FailingReferenceFactory,
            ["root diagnostic\r\nwith a second line"]);

        var result = await fixture.ExecuteInspectAsync();

        var payload = Structured(result);
        var summary = payload.GetProperty("diagnosticsSummary");
        Assert.Equal(1, summary.GetProperty("root").GetProperty("totalCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("transitive").GetProperty("totalCount").GetInt32());
        Assert.Equal(2, summary.GetProperty("totalCount").GetInt32());
        Assert.Equal(
            payload.GetProperty("diagnostics").GetArrayLength(),
            summary.GetProperty("shownCount").GetInt32());

        var samples = payload.GetProperty("diagnostics").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("root diagnostic with a second line", text, StringComparison.Ordinal);
        Assert.Contains("TransitiveDiagnosticDependency", text, StringComparison.Ordinal);
        Assert.All(samples, sample => Assert.Contains($"- {sample}", text, StringComparison.Ordinal));
        Assert.DoesNotContain("\r", samples.Single(sample => sample.Contains("root diagnostic", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AssemblyRoute_DeduplicatesRootAndTransitiveDiagnostics()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-diagnostics-dedup-");
        var reference = new AssemblyReferenceDto(
            "DuplicateDiagnosticDependency",
            "1.0.0.0",
            "neutral",
            Resolved: true,
            ResolvedPath: Path.Combine(temp.DirectoryPath, "DuplicateDiagnosticDependency.dll"));
        var sharedDiagnostic = "Referenz-Session für 'DuplicateDiagnosticDependency' konnte nicht eröffnet werden.";
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [reference],
            FailingReferenceFactory,
            [sharedDiagnostic]);

        var result = await fixture.ExecuteInspectAsync();

        var payload = Structured(result);
        var summary = payload.GetProperty("diagnosticsSummary");
        Assert.Equal(1, summary.GetProperty("root").GetProperty("totalCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("transitive").GetProperty("totalCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, payload.GetProperty("diagnostics").GetArrayLength());
        Assert.Equal(
            payload.GetProperty("diagnostics").GetArrayLength(),
            summary.GetProperty("shownCount").GetInt32());
        Assert.Equal(
            payload.GetProperty("diagnostics").GetArrayLength(),
            payload.GetProperty("diagnostics").EnumerateArray().Select(item => item.GetString()).Distinct().Count());
    }

    [Fact]
    public void DiagnosticsProjection_DeduplicatesAfterDisplayTruncation()
    {
        var sharedPrefix = $"long diagnostic: {new string('x', AssemblyAnalysisResponseLimits.MaxDiagnosticCharacters * 2)}";
        var summary = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            [sharedPrefix + " root detail"],
            [sharedPrefix + " transitive detail"]);

        Assert.Equal(2, summary.TotalCount);
        Assert.Single(summary.Samples);
        Assert.Single(summary.Root.Samples);
        Assert.Empty(summary.Transitive.Samples);
        Assert.Equal(summary.Samples.Count, summary.Samples.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(summary.ShownCount, summary.Root.ShownCount + summary.Transitive.ShownCount);
    }

    [Fact]
    public void DiagnosticsProjection_UsesOneGlobalSampleBudget()
    {
        var diagnostics = Enumerable.Range(0, AssemblyAnalysisResponseLimits.MaxDiagnostics * 2)
            .Select(index => $"diagnostic-{index:D3}: {new string('x', AssemblyAnalysisResponseLimits.MaxDiagnosticCharacters * 2)}")
            .ToArray();

        var summary = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            diagnostics.Take(AssemblyAnalysisResponseLimits.MaxDiagnostics),
            diagnostics.Skip(AssemblyAnalysisResponseLimits.MaxDiagnostics));

        Assert.Equal(diagnostics.Length, summary.TotalCount);
        Assert.Equal(summary.Samples.Count, summary.ShownCount);
        Assert.True(summary.ShownCount <= summary.TotalCount);
        Assert.Equal(summary.Samples.Count, summary.Samples.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(summary.ShownCount, summary.Root.ShownCount + summary.Transitive.ShownCount);
        Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(string.Join("\n", summary.Samples))
            <= AssemblyAnalysisResponseLimits.MaxDiagnosticBytes);
        Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(string.Join("\n", summary.Root.Samples.Concat(summary.Transitive.Samples)))
            <= AssemblyAnalysisResponseLimits.MaxDiagnosticBytes);
    }

    [Fact]
    public void DiagnosticsProjection_TruncatedBy_DoesNotIncludeMaxDiagnosticBytesWhenOnlySlotLimitHit()
    {
        // 3 kurze Root- und 3 kurze Transitive-Meldungen bei Limit = 2
        var summary = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            ["root-1", "root-2", "root-3"],
            ["trans-1", "trans-2", "trans-3"],
            requestedLimit: 2);

        Assert.Equal(6, summary.TotalCount);
        Assert.Equal(2, summary.ShownCount);
        Assert.True(summary.Truncated);
        Assert.Contains("maxDiagnostics", summary.TruncatedBy);
        Assert.DoesNotContain("maxDiagnosticBytes", summary.TruncatedBy);
        Assert.DoesNotContain("maxDiagnosticBytes", summary.Root.TruncatedBy);
        Assert.DoesNotContain("maxDiagnosticBytes", summary.Transitive.TruncatedBy);
    }

    [Fact]
    public async Task AssemblyRoute_StructuredContentUsesOneGlobalDiagnosticsBudget()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-diagnostic-budget-");
        var diagnostics = Enumerable.Range(0, AssemblyAnalysisResponseLimits.MaxDiagnostics + 10)
            .Select(index => $"diagnostic-{index:D3}: {new string('x', AssemblyAnalysisResponseLimits.MaxDiagnosticCharacters * 2)}")
            .ToArray();
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            Array.Empty<AssemblyReferenceDto>(),
            diagnostics: diagnostics);

        var result = await fixture.ExecuteInspectAsync();

        var payload = Structured(result);
        var samples = payload.GetProperty("diagnostics").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        var summary = payload.GetProperty("diagnosticsSummary");
        Assert.Equal(diagnostics.Length, summary.GetProperty("totalCount").GetInt32());
        Assert.True(summary.GetProperty("truncated").GetBoolean());
        Assert.Equal(samples.Length, summary.GetProperty("shownCount").GetInt32());
        Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(string.Join("\n", samples))
            <= AssemblyAnalysisResponseLimits.MaxDiagnosticBytes);
        Assert.True(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length
            <= AssemblyAnalysisResponseLimits.MaxDiagnosticBytes);

        var analysis = payload.GetProperty("analysis");
        Assert.False(analysis.TryGetProperty("diagnostics", out _));
        Assert.False(analysis.TryGetProperty("diagnosticsSummary", out _));

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains($"Diagnosen: {samples.Length} von {diagnostics.Length} (gekürzt)", text, StringComparison.Ordinal);
        Assert.All(samples, sample => Assert.Contains($"- {sample}", text, StringComparison.Ordinal));
        Assert.DoesNotContain(new string('x', AssemblyAnalysisResponseLimits.MaxDiagnosticCharacters + 1), text, StringComparison.Ordinal);
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
        Assert.False(analysis.TryGetProperty("diagnostics", out _));
        Assert.False(analysis.TryGetProperty("diagnosticsSummary", out _));

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
            AssemblyReferenceLeaseFactory? referenceLeaseFactory = null,
            IReadOnlyList<string>? diagnostics = null,
            string? sourceCode = null)
        {
            var assemblyPath = AssemblyTestHelper.EmitAssembly(
                temp,
                "DispatcherCapabilityProbe",
                sourceCode ?? "namespace Probe; public static class Probe { public static void Run() { } }");
            var backingRegistry = new AssemblyAnalysisRegistry();
            var backingResult = await backingRegistry.LeaseAsync(assemblyPath);
            Assert.Null(backingResult.Error);
            var backingLease = backingResult.Lease!;
            var context = backingLease.Context with
            {
                References = references,
                Diagnostics = diagnostics ?? Array.Empty<string>(),
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
                                 MaxResults: 100)),
                         ExpandAssemblyReferences: true),
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
                                 100)),
                            ExpandAssemblyReferences: true),
                CancellationToken.None));
        }

        internal Task<CallToolResult> ExecuteRootOnlyAsync(
            Func<AssemblyAnalysisLease, Task<CallToolResult>> call)
        {
            var route = AssemblyAnalysisDispatcher.CreateRoute(registry);
            return AnalysisToolCall.ExecuteRouted(
                route,
                new AnalysisToolCallRequest(
                    new AnalysisTargetRequest("assembly", AssemblyPath),
                    new AnalysisToolDispatch(AssemblySessionCall: call),
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
