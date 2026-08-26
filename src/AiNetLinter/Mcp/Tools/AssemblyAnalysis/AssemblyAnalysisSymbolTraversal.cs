#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisSymbolTraversal
{
    internal static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol) =>
        GetTypeTree(namespaceSymbol.GetTypeMembers())
            .Concat(namespaceSymbol.GetNamespaceMembers()
                .OrderBy(child => child.Name, StringComparer.Ordinal)
                .SelectMany(GetAllTypes));

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
        => GetTypeTree(type.GetTypeMembers());

    private static IEnumerable<INamedTypeSymbol> GetTypeTree(IEnumerable<INamedTypeSymbol> types)
    {
        foreach (var type in types.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type)) yield return nested;
        }
    }
}
