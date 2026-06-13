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
    /// <returns>Die Gesamtzahl transitiv referenzierter Codezeilen.</returns>
    public static int Calculate(INamedTypeSymbol classSymbol)
    {
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        QueueSymbols(classSymbol, visited);

        int totalLines = 0;
        var visitedTrees = new HashSet<SyntaxTree>();

        foreach (var symbol in visited)
        {
            totalLines += SumLinesForSymbol(symbol, visitedTrees);
        }

        return totalLines;
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

    private static void QueueSymbols(ITypeSymbol? typeSymbol, HashSet<INamedTypeSymbol> visited)
    {
        if (typeSymbol == null)
        {
            return;
        }

        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            QueueSymbols(arrayType.ElementType, visited);
            return;
        }

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            QueueNamedSymbol(namedType, visited);
        }
    }

    private static void QueueNamedSymbol(INamedTypeSymbol namedType, HashSet<INamedTypeSymbol> visited)
    {
        var originalSymbol = namedType.OriginalDefinition;
        if (originalSymbol.DeclaringSyntaxReferences.Length == 0)
        {
            return;
        }

        if (!visited.Add(originalSymbol))
        {
            return;
        }

        foreach (var member in originalSymbol.GetMembers())
        {
            QueueMemberSymbols(member, visited);
        }

        if (originalSymbol.IsGenericType)
        {
            QueueGenericArguments(originalSymbol, visited);
        }
    }

    private static void QueueMemberSymbols(ISymbol member, HashSet<INamedTypeSymbol> visited)
    {
        if (member is IFieldSymbol field)
        {
            QueueSymbols(field.Type, visited);
        }
        else if (member is IPropertySymbol prop)
        {
            QueueSymbols(prop.Type, visited);
        }
        else if (member is IMethodSymbol method)
        {
            QueueMethodSymbols(method, visited);
        }
    }

    private static void QueueMethodSymbols(IMethodSymbol method, HashSet<INamedTypeSymbol> visited)
    {
        QueueSymbols(method.ReturnType, visited);
        foreach (var param in method.Parameters)
        {
            QueueSymbols(param.Type, visited);
        }
    }

    private static void QueueGenericArguments(INamedTypeSymbol originalSymbol, HashSet<INamedTypeSymbol> visited)
    {
        foreach (var typeArg in originalSymbol.TypeArguments)
        {
            QueueSymbols(typeArg, visited);
        }
    }
}
