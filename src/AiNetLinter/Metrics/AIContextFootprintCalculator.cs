#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Metrics;

/// <summary>
/// Berechnet den transitiven AI-Context-Footprint (Summe der Codezeilen aller abhängigen Typen).
/// </summary>
public static class AIContextFootprintCalculator
{
    /// <summary>
    /// Berechnet den transitiven AI-Context-Footprint für ein bestimmtes Typ-Symbol.
    /// </summary>
    /// <param name="classSymbol">Das Typ-Symbol der Klasse, deren Footprint berechnet werden soll.</param>
    /// <param name="ignoreNamespacePrefixes">Namespace-Präfixe von Typen, die nicht mitgezählt werden.</param>
    /// <returns>Die Gesamtzahl transitiv referenzierter Codezeilen.</returns>
    public static int Calculate(INamedTypeSymbol classSymbol, IReadOnlyCollection<string>? ignoreNamespacePrefixes = null, IReadOnlyCollection<string>? ignoreTypeNames = null)
    {
        return CalculateDetailed(classSymbol, ignoreNamespacePrefixes, ignoreTypeNames).TotalLines;
    }

    /// <summary>
    /// Berechnet den transitiven AI-Context-Footprint und ermittelt die Top-Abhängigkeiten.
    /// </summary>
    public static (int TotalLines, List<(string Name, int Lines)> TopDependencies) CalculateDetailed(
        INamedTypeSymbol classSymbol,
        IReadOnlyCollection<string>? ignoreNamespacePrefixes = null,
        IReadOnlyCollection<string>? ignoreTypeNames = null)
    {
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        QueueSymbols(classSymbol, visited, ignoreNamespacePrefixes, ignoreTypeNames);

        int totalLines = 0;
        var visitedTrees = new HashSet<SyntaxTree>();

        foreach (var symbol in visited)
        {
            totalLines += SumLinesForSymbol(symbol, visitedTrees);
        }

        var targetOriginal = classSymbol.OriginalDefinition;
        var deps = new List<(string Name, int Lines)>();
        foreach (var symbol in visited)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, targetOriginal))
            {
                continue;
            }

            var symbolTrees = symbol.DeclaringSyntaxReferences.Select(r => r.SyntaxTree).Distinct().ToList();
            int symLines = symbolTrees.Sum(t => t.GetText().Lines.Count);

            deps.Add((symbol.ToDisplayString(), symLines));
        }

        var topDeps = deps.OrderByDescending(d => d.Lines)
            .Take(3)
            .ToList();

        return (totalLines, topDeps);
    }

    private static int SumLinesForSymbol(INamedTypeSymbol symbol, HashSet<SyntaxTree> visitedTrees)
    {
        int lines = 0;
        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            var tree = syntaxRef.SyntaxTree;
            if (visitedTrees.Add(tree))
            {
                lines += tree.GetText().Lines.Count;
            }
        }
        return lines;
    }

    private static void QueueSymbols(ITypeSymbol? typeSymbol, HashSet<INamedTypeSymbol> visited, IReadOnlyCollection<string>? ignoreNamespacePrefixes = null, IReadOnlyCollection<string>? ignoreTypeNames = null)
    {
        if (typeSymbol == null)
        {
            return;
        }

        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            QueueSymbols(arrayType.ElementType, visited, ignoreNamespacePrefixes, ignoreTypeNames);
            return;
        }

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            QueueNamedSymbol(namedType, visited, ignoreNamespacePrefixes, ignoreTypeNames);
        }
    }

    private static void QueueNamedSymbol(INamedTypeSymbol namedType, HashSet<INamedTypeSymbol> visited, IReadOnlyCollection<string>? ignoreNamespacePrefixes = null, IReadOnlyCollection<string>? ignoreTypeNames = null)
    {
        var originalSymbol = namedType.OriginalDefinition;
        if (originalSymbol.DeclaringSyntaxReferences.Length == 0) return;
        if (IsIgnoredSymbol(originalSymbol, ignoreNamespacePrefixes, ignoreTypeNames)) return;
        if (!visited.Add(originalSymbol)) return;

        foreach (var member in originalSymbol.GetMembers())
        {
            QueueMemberSymbols(member, visited, ignoreNamespacePrefixes, ignoreTypeNames);
        }

        if (originalSymbol.IsGenericType)
        {
            QueueGenericArguments(originalSymbol, visited, ignoreNamespacePrefixes, ignoreTypeNames);
        }
    }

    private static bool IsIgnoredSymbol(
        INamedTypeSymbol symbol,
        IReadOnlyCollection<string>? ignoreNamespacePrefixes,
        IReadOnlyCollection<string>? ignoreTypeNames)
    {
        if (ignoreTypeNames != null && ignoreTypeNames.Count > 0
            && ignoreTypeNames.Contains(symbol.Name, StringComparer.OrdinalIgnoreCase))
            return true;

        if (ignoreNamespacePrefixes == null || ignoreNamespacePrefixes.Count == 0) return false;

        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        foreach (var prefix in ignoreNamespacePrefixes)
        {
            if (ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void QueueMemberSymbols(ISymbol member, HashSet<INamedTypeSymbol> visited, IReadOnlyCollection<string>? ignoreNamespacePrefixes = null, IReadOnlyCollection<string>? ignoreTypeNames = null)
    {
        if (member is IFieldSymbol field)
        {
            QueueSymbols(field.Type, visited, ignoreNamespacePrefixes, ignoreTypeNames);
        }
        else if (member is IPropertySymbol prop)
        {
            QueueSymbols(prop.Type, visited, ignoreNamespacePrefixes, ignoreTypeNames);
        }
        else if (member is IMethodSymbol method)
        {
            QueueMethodSymbols(method, visited, ignoreNamespacePrefixes, ignoreTypeNames);
        }
    }

    private static void QueueMethodSymbols(IMethodSymbol method, HashSet<INamedTypeSymbol> visited, IReadOnlyCollection<string>? ignoreNamespacePrefixes = null, IReadOnlyCollection<string>? ignoreTypeNames = null)
    {
        QueueSymbols(method.ReturnType, visited, ignoreNamespacePrefixes, ignoreTypeNames);
        foreach (var param in method.Parameters)
        {
            QueueSymbols(param.Type, visited, ignoreNamespacePrefixes, ignoreTypeNames);
        }
    }

    private static void QueueGenericArguments(INamedTypeSymbol originalSymbol, HashSet<INamedTypeSymbol> visited, IReadOnlyCollection<string>? ignoreNamespacePrefixes = null, IReadOnlyCollection<string>? ignoreTypeNames = null)
    {
        foreach (var typeArg in originalSymbol.TypeArguments)
        {
            QueueSymbols(typeArg, visited, ignoreNamespacePrefixes, ignoreTypeNames);
        }
    }
}
