#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Factories;

internal sealed class AssemblyAnalysisSourceProjectEntryFactory
{
    private readonly AssemblyAnalysisResourceBudget resourceBudget;
    private readonly Func<AssemblySourceSelection?, AssemblyReferenceLeaseFactory> referenceLeaseFactory;

    internal AssemblyAnalysisSourceProjectEntryFactory(
        AssemblyAnalysisResourceBudget resourceBudget,
        Func<AssemblySourceSelection?, AssemblyReferenceLeaseFactory> referenceLeaseFactory)
    {
        this.resourceBudget = resourceBudget;
        this.referenceLeaseFactory = referenceLeaseFactory;
    }

    internal async Task<AssemblyAnalysisEntry> CreateAsync(
        AssemblyAnalysisSourceProjectEntryCreationParameters parameters)
    {
        ExternalResourceOperationLease? operation = null;
        SourceSnapshotLease? projectLease = null;
        var resourceTransferred = false;
        var sourceTransferred = false;
        try
        {
            operation = resourceBudget.BeginOperation(parameters.CreationToken);
            projectLease = parameters.ParentSelection.SourceLease.AcquireSibling();
            var selection = parameters.ParentSelection.ForProject(projectLease, parameters.Project)
                ?? throw new InvalidOperationException("Source-Project-Selection konnte nicht erzeugt werden.");
            var targetPath = parameters.Project.FilePath ?? parameters.Key;
            var sourceResult = await AssemblyAnalysisContextFactory.CreateSourceProjectContextAsync(
                targetPath,
                parameters.Project,
                selection,
                parameters.CreationToken).ConfigureAwait(false);
            if (sourceResult.Context is null)
            {
                throw new InvalidOperationException(sourceResult.Error ?? "Source-Project-Context konnte nicht erzeugt werden.");
            }

            var context = sourceResult.Context with { Generation = parameters.Generation };
            var entry = AssemblyAnalysisEntry.Create(new AssemblyAnalysisEntryCreateParameters(
                targetPath,
                parameters.ParentSelection.SourceLease.Snapshot.Solution,
                context,
                projectLease,
                parameters.ResourceLease,
                referenceLeaseFactory(selection)));
            resourceTransferred = true;
            sourceTransferred = true;
            projectLease = null;
            return entry;
        }
        finally
        {
            operation?.Dispose();
            if (!resourceTransferred) parameters.ResourceLease?.Dispose();
            if (!sourceTransferred) projectLease?.Dispose();
        }
    }
}

internal sealed record AssemblyAnalysisSourceProjectEntryCreationParameters(
    string Key,
    long Generation,
    CancellationToken CreationToken,
    ExternalResourceLease? ResourceLease,
    AssemblySourceSelection ParentSelection,
    Project Project);

