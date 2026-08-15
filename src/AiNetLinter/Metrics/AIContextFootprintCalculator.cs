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
    /// Maximale Anzahl an Zeilen, die für einen transitiven reinen Deklarations-Typ (DTO, Model, Record ohne Body)
    /// angerechnet werden.
    /// </summary>
    public const int MaxDeclarationLines = 10;

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

        var targetOriginal = classSymbol.OriginalDefinition;
        int totalLines = 0;
        var visitedTrees = new HashSet<SyntaxTree>();

        foreach (var symbol in visited)
        {
            var isTarget = SymbolEqualityComparer.Default.Equals(symbol, targetOriginal);
            totalLines += (!isTarget && IsDeclarationOnlyType(symbol))
                ? CalculateDeclarationLines(symbol, visitedTrees)
                : SumLinesForSymbol(symbol, visitedTrees);
        }

        var deps = new List<(string Name, int Lines)>();
        foreach (var symbol in visited)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, targetOriginal))
            {
                continue;
            }

            int symLines = IsDeclarationOnlyType(symbol)
                ? CalculateDeclarationLines(symbol)
                : symbol.DeclaringSyntaxReferences.Select(r => r.SyntaxTree).Distinct().Sum(t => t.GetText().Lines.Count);

            deps.Add((symbol.ToDisplayString(), symLines));
        }

        var topDeps = deps.OrderByDescending(d => d.Lines)
            .Take(3)
            .ToList();

        return (totalLines, topDeps);
    }

    private static int CalculateDeclarationLines(INamedTypeSymbol symbol, HashSet<SyntaxTree>? visitedTrees = null)
    {
        int declLines = 0;
        bool hasUnvisited = false;
        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            var tree = syntaxRef.SyntaxTree;
            if (visitedTrees is null || visitedTrees.Add(tree))
            {
                hasUnvisited = true;
                var span = syntaxRef.GetSyntax().GetLocation().GetLineSpan();
                declLines += span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
            }
        }
        if (visitedTrees is not null && !hasUnvisited) return 0;
        return Math.Min(declLines > 0 ? declLines : 1, MaxDeclarationLines);
    }

    /// <summary>
    /// Prüft, ob ein Typ ein reiner Deklarations- oder Datenträgertyp ist (DTO, Model, Options, Record ohne Methoden).
    /// Solche Typen werden im transitiven Footprint nur mit ihren Deklarationszeilen (max. <see cref="MaxDeclarationLines"/>)
    /// angerechnet statt des gesamten Datei-Bodys.
    /// </summary>
    public static bool IsDeclarationOnlyType(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind is TypeKind.Enum)
        {
            return true;
        }

        var ordinaryMethods = symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary
                        && !m.IsImplicitlyDeclared
                        && !(symbol.IsRecord && (m.Name is "<Clone>$" or "ToString" or "PrintMembers" or "Equals" or "GetHashCode" or "Deconstruct")));

        if (ordinaryMethods.Any())
        {
            return false;
        }

        var hasPropertiesOrFields = symbol.GetMembers().Any(m => m is IPropertySymbol or IFieldSymbol);
        return hasPropertiesOrFields || symbol.IsRecord;
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
