#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Zentraler Helper fuer Kind-Filterung und Typen-Deskriptoren ueber MCP-Tools hinweg.
/// Konsolidiert String-zu-Kind Parser und Kind-zu-String Formatierer.
/// </summary>
internal static class SymbolKindClassifier
{
    private static readonly HashSet<string> ValidTypeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "class", "interface", "record", "record class", "record struct", "struct", "enum", "delegate", "all",
    };

    internal static bool IsValidTypeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return true;
        return ValidTypeKinds.Contains(kind);
    }

    internal static bool MatchesTypeKind(INamedTypeSymbol type, string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (kind.StartsWith("record", StringComparison.OrdinalIgnoreCase))
        {
            return MatchesRecordKind(type, kind);
        }

        return kind.ToLowerInvariant() switch
        {
            // Records have a Roslyn TypeKind of Class/Struct as well. They
            // are distinct public MCP kinds and must not leak into a plain
            // class/struct query.
            "class" => type.TypeKind == TypeKind.Class && !type.IsRecord,
            "interface" => type.TypeKind == TypeKind.Interface,
            "struct" => type.TypeKind == TypeKind.Struct && !type.IsRecord,
            "enum" => type.TypeKind == TypeKind.Enum,
            "delegate" => type.TypeKind == TypeKind.Delegate,
            _ => false,
        };
    }

    private static bool MatchesRecordKind(INamedTypeSymbol type, string kind)
    {
        if (!type.IsRecord) return false;
        return kind.ToLowerInvariant() switch
        {
            "record" => true,
            "record class" => type.TypeKind == TypeKind.Class,
            "record struct" => type.TypeKind == TypeKind.Struct,
            _ => false,
        };
    }

    internal static bool MatchesSymbolKind(ISymbol symbol, string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (symbol is IMethodSymbol)
        {
            return kind.Equals("method", StringComparison.OrdinalIgnoreCase);
        }

        if (symbol is IPropertySymbol)
        {
            return kind.Equals("property", StringComparison.OrdinalIgnoreCase);
        }

        if (symbol is INamedTypeSymbol namedType)
        {
            return MatchesTypeKind(namedType, kind);
        }

        if (symbol is ITypeSymbol typeSymbol)
        {
            return MatchesTypeKindFallback(typeSymbol, kind);
        }

        // An explicit kind filter is a closed vocabulary. Fields, events,
        // namespaces and other Roslyn symbols are not canonical find_symbol
        // kinds and must never pass the filter accidentally.
        return false;
    }

    private static bool MatchesTypeKindFallback(ITypeSymbol typeSymbol, string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "class" => typeSymbol.TypeKind == TypeKind.Class,
            "interface" => typeSymbol.TypeKind == TypeKind.Interface,
            "struct" => typeSymbol.TypeKind == TypeKind.Struct,
            "enum" => typeSymbol.TypeKind == TypeKind.Enum,
            "delegate" => typeSymbol.TypeKind == TypeKind.Delegate,
            _ => false,
        };
    }

    internal static string DescribeNamedTypeKind(INamedTypeSymbol namedType, bool specificRecord = false)
    {
        if (namedType.IsRecord)
        {
            return namedType.TypeKind == TypeKind.Struct ? "record struct" : (specificRecord ? "record class" : "record");
        }

        return namedType.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => namedType.TypeKind.ToString().ToLowerInvariant(),
        };
    }

    internal static string DescribeSymbolKind(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol named)
        {
            return DescribeNamedTypeKind(named);
        }

        if (symbol is ITypeSymbol { TypeKind: TypeKind.Class }) return "class";
        if (symbol is ITypeSymbol { TypeKind: TypeKind.Interface }) return "interface";
        if (symbol.Kind == SymbolKind.Method) return "method";
        if (symbol.Kind == SymbolKind.Property) return "property";
        return symbol.Kind.ToString().ToLowerInvariant();
    }
}
