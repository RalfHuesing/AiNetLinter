#nullable enable

using System.Collections.Generic;
using System.Linq;
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
        var leaseSet = AssemblyNavigationLeaseAccess.GetLeases(root);
        foreach (var lease in leaseSet.Leases)
        {
            var symbol = ReferenceEquals(lease, target.Lease)
                ? target.Symbol
                : MapToCompilation(target.Symbol, lease.Context.Compilation);
            AddSource(sources, lease, symbol);
        }

        return sources;
    }

    private static void AddSource(
        ICollection<AssemblyNavigationSource> sources,
        AssemblyAnalysisLease lease,
        ISymbol? symbol)
    {
        var view = AssemblyNavigationLeaseAccess.CreateView(lease);
        if (view.Solution is not null && symbol is not null)
        {
            sources.Add(new(symbol, view.Solution, view.CanonicalPath, view.Identity, view.Origin));
        }
    }

    private static ISymbol? MapToCompilation(ISymbol symbol, Compilation compilation)
    {
        var declarationId = DocumentationCommentId.CreateDeclarationId(symbol);
        if (declarationId is not null)
        {
            var mapped = DocumentationCommentId.GetFirstSymbolForDeclarationId(declarationId, compilation);
            if (mapped is not null) return mapped;
        }

        if (symbol is INamedTypeSymbol type)
        {
            return compilation.GetTypeByMetadataName(GetMetadataTypeName(type));
        }

        var containingType = symbol.ContainingType;
        if (containingType is null) return null;

        var mappedType = compilation.GetTypeByMetadataName(GetMetadataTypeName(containingType));
        if (mappedType is null) return null;

        var candidates = mappedType.GetMembers(symbol.Name)
            .Where(candidate => candidate.Kind == symbol.Kind)
            .ToList();
        if (candidates.Count == 1) return candidates[0];

        return declarationId is null
            ? null
            : candidates.FirstOrDefault(candidate =>
                string.Equals(
                    DocumentationCommentId.CreateDeclarationId(candidate),
                    declarationId,
                    StringComparison.Ordinal));
    }

    private static string GetMetadataTypeName(INamedTypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, StringComparison.Ordinal);
}
