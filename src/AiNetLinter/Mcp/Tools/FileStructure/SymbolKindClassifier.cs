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
        "class", "klasse", "interface", "record", "struct", "enum", "delegate", "all",
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

        return kind.ToLowerInvariant() switch
        {
            "class" or "klasse" => type.TypeKind == TypeKind.Class && !type.IsRecord,
            "interface" => type.TypeKind == TypeKind.Interface,
            "record" => type.IsRecord,
            "struct" => type.TypeKind == TypeKind.Struct && !type.IsRecord,
            "enum" => type.TypeKind == TypeKind.Enum,
            "delegate" => type.TypeKind == TypeKind.Delegate,
            _ => false,
        };
    }

    internal static bool MatchesSymbolKind(ISymbol symbol, string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return kind.ToLowerInvariant() switch
        {
            "class" or "klasse" => symbol is ITypeSymbol { TypeKind: TypeKind.Class } and not INamedTypeSymbol { IsRecord: true },
            "interface" => symbol is ITypeSymbol { TypeKind: TypeKind.Interface },
            "record" => symbol is INamedTypeSymbol { IsRecord: true },
            "struct" => symbol is ITypeSymbol { TypeKind: TypeKind.Struct } and not INamedTypeSymbol { IsRecord: true },
            "enum" => symbol is ITypeSymbol { TypeKind: TypeKind.Enum },
            "delegate" => symbol is ITypeSymbol { TypeKind: TypeKind.Delegate },
            "method" or "methode" => symbol is IMethodSymbol,
            "property" => symbol is IPropertySymbol,
            _ => true,
        };
    }

    internal static string DescribeNamedTypeKind(INamedTypeSymbol namedType, bool englishClass = false)
    {
        if (namedType.IsRecord)
        {
            return namedType.TypeKind == TypeKind.Struct ? "record struct" : (englishClass ? "record class" : "record");
        }

        return namedType.TypeKind switch
        {
            TypeKind.Class => englishClass ? "class" : "Klasse",
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
            return DescribeNamedTypeKind(named, englishClass: false);
        }

        if (symbol is ITypeSymbol { TypeKind: TypeKind.Class }) return "Klasse";
        if (symbol is ITypeSymbol { TypeKind: TypeKind.Interface }) return "Interface";
        if (symbol.Kind == SymbolKind.Method) return "Methode";
        if (symbol.Kind == SymbolKind.Property) return "Property";
        return symbol.Kind.ToString();
    }
}
