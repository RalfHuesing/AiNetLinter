#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static class AssemblyNavigationSourceFactory
{
    internal static IReadOnlyList<AssemblyNavigationSource> CreateSources(
        AssemblyAnalysisLease root,
        AssemblySymbolTarget target)
    {
        var sources = new List<AssemblyNavigationSource>();
        AddTargetSource(sources, target.Lease, target.Symbol);
        if (!ReferenceEquals(root, target.Lease))
        {
            AddMappedRootSource(sources, root, target.Symbol);
        }

        return sources;
    }

    private static void AddTargetSource(
        ICollection<AssemblyNavigationSource> sources,
        AssemblyAnalysisLease lease,
        ISymbol symbol)
    {
        var view = AssemblyNavigationLeaseAccess.CreateView(lease);
        if (view.Solution is not null)
        {
            sources.Add(new(symbol, view.Solution, view.CanonicalPath, view.Identity, view.Origin));
        }
    }

    private static void AddMappedRootSource(
        ICollection<AssemblyNavigationSource> sources,
        AssemblyAnalysisLease root,
        ISymbol symbol)
    {
        var view = AssemblyNavigationLeaseAccess.CreateView(root);
        var mapped = view.Solution is null ? null : MapToCompilation(symbol, root.Context.Compilation);
        if (view.Solution is not null && mapped is not null)
        {
            sources.Add(new(mapped, view.Solution, view.CanonicalPath, view.Identity, view.Origin));
        }
    }

    private static ISymbol? MapToCompilation(ISymbol symbol, Compilation compilation)
    {
        var declarationId = DocumentationCommentId.CreateDeclarationId(symbol);
        return declarationId is null
            ? null
            : DocumentationCommentId.GetFirstSymbolForDeclarationId(declarationId, compilation);
    }
}
