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
    AssemblySourceFallbackMetadata? Fallback = null);

internal sealed class AssemblyAnalysisRegistryEntryFactory
{
    private readonly IAssemblySourceResolver? sourceOrchestrator;
    private readonly AssemblyAnalysisResourceBudget resourceBudget;
    private readonly Func<AssemblySourceSelection?, AssemblyReferenceLeaseFactory> referenceLeaseFactory;
    private readonly Action<AssemblyAnalysisEntry> requestTemporaryReferenceEviction;
    private AssemblyDecompilationConfiguration? decompilationConfiguration;

    internal AssemblyAnalysisRegistryEntryFactory(
        IAssemblySourceResolver? sourceOrchestrator,
        AssemblyAnalysisResourceBudget resourceBudget,
        Func<AssemblySourceSelection?, AssemblyReferenceLeaseFactory> referenceLeaseFactory,
        Action<AssemblyAnalysisEntry> requestTemporaryReferenceEviction,
        AssemblyDecompilationConfiguration? decompilationConfiguration = null)
    {
        this.sourceOrchestrator = sourceOrchestrator;
        this.resourceBudget = resourceBudget;
        this.referenceLeaseFactory = referenceLeaseFactory;
        this.requestTemporaryReferenceEviction = requestTemporaryReferenceEviction;
        this.decompilationConfiguration = decompilationConfiguration;
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
                    sourceAttempt.Fallback)).ConfigureAwait(false);
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
            decompilationConfiguration?.Options,
            decompilationConfiguration?.CacheRoot,
            parameters.TargetGeneration - 1));
        try
        {
            var refresh = await session.RefreshAsync(parameters.CreationToken).ConfigureAwait(false);
            var sessionGeneration = session.CurrentGeneration;
            if (sessionGeneration is null)
            {
                if (refresh.Failure is { Kind: AssemblySessionFailureKind.MetadataUnavailable } failure)
                {
                    throw new AssemblyAnalysisRegistryRecoverableFailureException(failure);
                }

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
                    FallbackReason = parameters.Fallback?.Reason,
                    SourceDiagnostics = parameters.Fallback?.Diagnostics,
                },
                ResponseBudgetBytes = decompilationConfiguration?.ResponseBudgetBytes
                    ?? AssemblyAnalysisResponseLimits.DefaultResponseBytes,
            };
            var fallbackEntry = AssemblyAnalysisEntryFactory.Create(new AssemblyAnalysisEntryCreateParameters(
                parameters.CanonicalPath,
                sessionGeneration.Snapshot.Solution,
                context,
                session,
                parameters.ResourceLease,
                referenceLeaseFactory(parameters.SourceSelection),
                resourceBudget.Clock,
                requestTemporaryReferenceEviction));
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

    private async Task<(AssemblyAnalysisEntry? Entry, IDisposable? Scope, IReadOnlyList<string> Diagnostics, AssemblySourceSelection? Selection, AssemblySourceFallbackMetadata? Fallback)> TryCreateSourceEntryAsync(
        string canonicalPath,
        long generation,
        CancellationToken creationToken,
        ExternalResourceLease? resourceLease)
    {
        if (sourceOrchestrator is null) return (null, null, Array.Empty<string>(), null, null);

        var resolution = await sourceOrchestrator.ResolveForRegistryAsync(canonicalPath, creationToken).ConfigureAwait(false);
        var diagnostics = AssemblyAnalysisDiagnostics.FormatExternalDiagnostics(resolution.Diagnostics).ToArray();
        if (resolution.Selection is null) return (null, resolution.Lifetime, diagnostics, null, resolution.Fallback);

        try
        {
            var sourceResult = await AssemblyAnalysisContextFactory.CreateAsync(
                new AssemblyAnalysisContextRequest(
                    canonicalPath,
                    ConsumerSolution: null,
                    ReceiverType: null,
                    resolution.Selection,
                    creationToken,
                    resolution.Fallback)).ConfigureAwait(false);
            if (sourceResult.Context is null) return (null, resolution.Lifetime, diagnostics, null, resolution.Fallback);

            if (sourceResult.Context.Origin.IsDecompiled)
            {
                var fallback = CreateFallbackMetadata(sourceResult.Context.Origin, resolution.Fallback);
                var fallbackDiagnostics = diagnostics
                    .Concat(sourceResult.Context.Diagnostics)
                    .Distinct(StringComparer.Ordinal)
                    .Take(100)
                    .ToArray();
                return (null, resolution.Lifetime, fallbackDiagnostics, null, fallback);
            }

            var context = sourceResult.Context with
            {
                Generation = generation,
                Diagnostics = sourceResult.Context.Diagnostics
                    .Concat(diagnostics)
                    .Distinct(StringComparer.Ordinal)
                    .Take(100)
                    .ToList(),
                ResponseBudgetBytes = decompilationConfiguration?.ResponseBudgetBytes
                    ?? AssemblyAnalysisResponseLimits.DefaultResponseBytes,
            };
            var entry = AssemblyAnalysisEntryFactory.Create(new AssemblyAnalysisEntryCreateParameters(
                canonicalPath,
                resolution.Selection.SourceLease.Snapshot.Solution,
                context,
                resolution.Lifetime,
                resourceLease,
                referenceLeaseFactory(resolution.Selection),
                resourceBudget.Clock,
                requestTemporaryReferenceEviction));
            return (entry, null, diagnostics, resolution.Selection, resolution.Fallback);
        }
        catch
        {
            AssemblyAnalysisRegistryDisposal.TryDispose(resolution.Lifetime, "Source-Selection-Scope nach Creation-Fehler");
            throw;
        }
    }

    private static AssemblySourceFallbackMetadata? CreateFallbackMetadata(
        AssemblyOrigin origin,
        AssemblySourceFallbackMetadata? existing)
    {
        if (string.IsNullOrWhiteSpace(origin.FallbackReason)) return existing;
        return new(
            origin.FallbackReason,
            origin.SourceDiagnostics ?? existing?.Diagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>());
    }
}
