#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

public sealed partial class AssemblyAnalysisDispatcherCapabilityTests
{
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
            string? sourceCode = null,
            string? sourcePolicy = null)
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
                Origin = sourcePolicy is null
                    ? backingLease.Context.Origin
                    : backingLease.Context.Origin with { SourcePolicy = sourcePolicy },
            };
            var entry = AssemblyAnalysisEntryFactory.Create(new AssemblyAnalysisEntryCreateParameters(
                backingLease.CanonicalPath,
                backingLease.Server.GetCurrentSolution()!,
                context,
                Lifetime: null,
                ReferenceLeaseFactory: referenceLeaseFactory));
            Assert.True(entry.TryAcquireLease(out var lease));
            return new SyntheticAssemblyFixture(backingRegistry, backingLease, entry, lease!, assemblyPath);
        }

        internal async Task<CallToolResult> ExecuteInspectAsync(
            int maxResponseBytes = 0,
            string? detailLevel = null,
            string? cursor = null)
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
                                MaxResults: 100,
                                MaxResponseBytes: maxResponseBytes,
                                DetailLevel: detailLevel,
                                Cursor: cursor)),
                        ExpandAssemblyReferences: true,
                        MaxResponseBytes: maxResponseBytes,
                        DetailLevel: detailLevel,
                        Cursor: cursor),
                    CancellationToken.None));
        }

        internal async Task<CallToolResult> ExecuteExtensionsAsync(bool includeReferences = true)
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
                                100,
                                includeReferences)),
                        ExpandAssemblyReferences: includeReferences),
                    CancellationToken.None));
        }

        internal Task<CallToolResult> ExecuteRootOnlyAsync(
            Func<AssemblyAnalysisLease, Task<CallToolResult>> call,
            int maxResponseBytes = 0,
            string? detailLevel = null,
            string? cursor = null)
        {
            var route = AssemblyAnalysisDispatcher.CreateRoute(registry);
            return AnalysisToolCall.ExecuteRouted(
                route,
                new AnalysisToolCallRequest(
                    new AnalysisTargetRequest("assembly", AssemblyPath),
                    new AnalysisToolDispatch(
                        AssemblySessionCall: call,
                        MaxResponseBytes: maxResponseBytes,
                        DetailLevel: detailLevel,
                        Cursor: cursor),
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
}
