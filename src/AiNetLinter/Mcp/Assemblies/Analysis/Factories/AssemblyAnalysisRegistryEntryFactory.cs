#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Factories;

internal sealed record AssemblyAnalysisFallbackEntryCreationParameters(
    string CanonicalPath,
    long TargetGeneration,
    CancellationToken CreationToken,
    ExternalResourceLease? ResourceLease,
    IReadOnlyList<string> Diagnostics,
    AssemblySourceSelection? SourceSelection,
    string? FallbackReason = null,
    IReadOnlyList<ExternalSourceConfigurationDiagnostic>? SourceDiagnostics = null);

internal sealed class AssemblyAnalysisRegistryEntryFactory
{
    private readonly IAssemblySourceResolver? sourceOrchestrator;
    private readonly AssemblyAnalysisResourceBudget resourceBudget;
    private readonly Func<AssemblySourceSelection?, AssemblyReferenceLeaseFactory> referenceLeaseFactory;

    internal AssemblyAnalysisRegistryEntryFactory(
        IAssemblySourceResolver? sourceOrchestrator,
        AssemblyAnalysisResourceBudget resourceBudget,
        Func<AssemblySourceSelection?, AssemblyReferenceLeaseFactory> referenceLeaseFactory)
    {
        this.sourceOrchestrator = sourceOrchestrator;
        this.resourceBudget = resourceBudget;
        this.referenceLeaseFactory = referenceLeaseFactory;
    }

    internal async Task<AssemblyAnalysisEntry> CreateAsync(
        string canonicalPath,
        long targetGeneration,
        CancellationToken creationToken,
        ExternalResourceLease? resourceLease)
    {
        IDisposable? sourceScope = null;
        ExternalResourceOperationLease? operation = null;
        var resourceTransferred = false;
        try
        {
            operation = resourceBudget.BeginOperation(creationToken);

            var sourceAttempt = await TryCreateSourceEntryAsync(
                canonicalPath,
                targetGeneration,
                creationToken,
                resourceLease).ConfigureAwait(false);
            sourceScope = sourceAttempt.Scope;
            if (sourceAttempt.Entry is not null)
            {
                sourceScope = null;
                resourceTransferred = true;
                return sourceAttempt.Entry;
            }

            var fallbackEntry = await CreateFallbackEntryAsync(
                new AssemblyAnalysisFallbackEntryCreationParameters(
                    canonicalPath,
                    targetGeneration,
                    creationToken,
                    resourceLease,
                    sourceAttempt.Diagnostics,
                    sourceAttempt.Selection,
                    sourceAttempt.FallbackReason,
                    sourceAttempt.SourceDiagnostics)).ConfigureAwait(false);
            resourceTransferred = true;
            return fallbackEntry;
        }
        finally
        {
            operation?.Dispose();
            if (!resourceTransferred) resourceLease?.DisposeAndRemove();
            AssemblyAnalysisRegistryDisposal.TryDispose(sourceScope, "Source-Selection-Scope");
        }
    }

    private async Task<AssemblyAnalysisEntry> CreateFallbackEntryAsync(
        AssemblyAnalysisFallbackEntryCreationParameters parameters)
    {
        AssemblyAnalysisSession? session = new AssemblyAnalysisSession(new AssemblyAnalysisSessionOptions(
            parameters.CanonicalPath,
            GenerationStart: parameters.TargetGeneration - 1));
        try
        {
            var refresh = await session.RefreshAsync(parameters.CreationToken).ConfigureAwait(false);
            var sessionGeneration = session.CurrentGeneration;
            if (sessionGeneration is null)
            {
                throw new InvalidOperationException(string.Join(" ", refresh.Diagnostics));
            }

            var context = AssemblyAnalysisContextFactory.FromGeneration(sessionGeneration);
            context = context with
            {
                Diagnostics = context.Diagnostics
                    .Concat(parameters.Diagnostics)
                    .Distinct(StringComparer.Ordinal)
                    .Take(100)
                    .ToList(),
                Origin = context.Origin with
                {
                    FallbackReason = parameters.FallbackReason,
                    SourceDiagnostics = parameters.SourceDiagnostics,
                },
            };
            var fallbackEntry = AssemblyAnalysisEntry.Create(new AssemblyAnalysisEntryCreateParameters(
                parameters.CanonicalPath,
                sessionGeneration.Snapshot.Solution,
                context,
                session,
                parameters.ResourceLease,
                referenceLeaseFactory(parameters.SourceSelection),
                resourceBudget.Clock));
            session = null;
            return fallbackEntry;
        }
        finally
        {
            if (session is not null)
            {
                await AssemblyAnalysisRegistryDisposal.TryDisposeAsync(session, "Assembly-Session").ConfigureAwait(false);
            }
        }
    }

    private async Task<(AssemblyAnalysisEntry? Entry, IDisposable? Scope, IReadOnlyList<string> Diagnostics, AssemblySourceSelection? Selection, string? FallbackReason, IReadOnlyList<ExternalSourceConfigurationDiagnostic>? SourceDiagnostics)> TryCreateSourceEntryAsync(
        string canonicalPath,
        long generation,
        CancellationToken creationToken,
        ExternalResourceLease? resourceLease)
    {
        if (sourceOrchestrator is null) return (null, null, Array.Empty<string>(), null, null, null);

        var resolution = await sourceOrchestrator.ResolveForRegistryAsync(canonicalPath, creationToken).ConfigureAwait(false);
        var diagnostics = AssemblyAnalysisDiagnostics.FormatExternalDiagnostics(resolution.Diagnostics).ToArray();
        if (resolution.Selection is null) return (null, resolution.Lifetime, diagnostics, null, resolution.FallbackReason, resolution.SourceDiagnostics);

        try
        {
            var sourceResult = await AssemblyAnalysisContextFactory.CreateAsync(
                new AssemblyAnalysisContextRequest(
                    canonicalPath,
                    ConsumerSolution: null,
                    ReceiverType: null,
                    resolution.Selection,
                    creationToken,
                    resolution.FallbackReason,
                    resolution.SourceDiagnostics)).ConfigureAwait(false);
            if (sourceResult.Context is null) return (null, resolution.Lifetime, diagnostics, null, resolution.FallbackReason, resolution.SourceDiagnostics);

            var context = sourceResult.Context with
            {
                Generation = generation,
                Diagnostics = sourceResult.Context.Diagnostics
                    .Concat(diagnostics)
                    .Distinct(StringComparer.Ordinal)
                    .Take(100)
                    .ToList(),
            };
            var entry = AssemblyAnalysisEntry.Create(new AssemblyAnalysisEntryCreateParameters(
                canonicalPath,
                resolution.Selection.SourceLease.Snapshot.Solution,
                context,
                resolution.Lifetime,
                resourceLease,
                referenceLeaseFactory(resolution.Selection),
                resourceBudget.Clock));
            return (entry, null, diagnostics, resolution.Selection, resolution.FallbackReason, resolution.SourceDiagnostics);
        }
        catch
        {
            AssemblyAnalysisRegistryDisposal.TryDispose(resolution.Lifetime, "Source-Selection-Scope nach Creation-Fehler");
            throw;
        }
    }
}
