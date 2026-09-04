#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Factories;

internal sealed class AssemblyAnalysisRegistryEntryFactory
{
    private readonly AssemblyAnalysisResourceBudget resourceBudget;
    private readonly Func<AssemblyReferenceLeaseFactory> referenceLeaseFactory;
    private readonly Action<AssemblyAnalysisEntry> requestTemporaryReferenceEviction;
    private AssemblyDecompilationConfiguration? decompilationConfiguration;

    internal AssemblyAnalysisRegistryEntryFactory(
        AssemblyAnalysisResourceBudget resourceBudget,
        Func<AssemblyReferenceLeaseFactory> referenceLeaseFactory,
        Action<AssemblyAnalysisEntry> requestTemporaryReferenceEviction,
        AssemblyDecompilationConfiguration? decompilationConfiguration = null)
    {
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
        ExternalResourceOperationLease? operation = null;
        var resourceTransferred = false;
        AssemblyAnalysisSession? session = null;
        try
        {
            operation = resourceBudget.BeginOperation(creationToken);

            session = new AssemblyAnalysisSession(new AssemblyAnalysisSessionOptions(
                canonicalPath,
                decompilationConfiguration?.Options,
                decompilationConfiguration?.CacheRoot,
                targetGeneration - 1));

            var refresh = await session.RefreshAsync(creationToken).ConfigureAwait(false);
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
                ResponseBudgetBytes = decompilationConfiguration?.ResponseBudgetBytes
                    ?? AssemblyAnalysisResponseLimits.DefaultResponseBytes,
            };

            var entry = AssemblyAnalysisEntryFactory.Create(new AssemblyAnalysisEntryCreateParameters(
                canonicalPath,
                sessionGeneration.Snapshot.Solution,
                context,
                session,
                resourceLease,
                referenceLeaseFactory(),
                resourceBudget.Clock,
                requestTemporaryReferenceEviction));

            session = null;
            resourceTransferred = true;
            return entry;
        }
        finally
        {
            operation?.Dispose();
            if (!resourceTransferred) resourceLease?.DisposeAndRemove();
            if (session is not null)
            {
                await AssemblyAnalysisRegistryDisposal.TryDisposeAsync(session, "Assembly-Session").ConfigureAwait(false);
            }
        }
    }
}
