#nullable enable

using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.DeadCode;

/// <summary>
/// Hilfsfunktionen fuer Filter- und Formatierungs-Logik von Symbolen bei find_dead_code.
/// </summary>
internal static class DeadCodeFilters
{
    internal static bool ShouldCheckSymbol(INamedTypeSymbol symbol, FindDeadCodeArgs args)
    {
        if (!MatchesKindFilter(symbol, args.Kind)) return false;
        if (!MatchesAccessibilityFilter(symbol.DeclaredAccessibility, args.Accessibility)) return false;
        return true;
    }

    internal static bool MatchesKindFilter(ISymbol symbol, DeadCodeKindFilter kindFilter)
    {
        if (kindFilter == DeadCodeKindFilter.All) return true;

        return symbol switch
        {
            INamedTypeSymbol named => MatchesNamedTypeKind(named, kindFilter),
            IMethodSymbol => kindFilter is DeadCodeKindFilter.Method,
            IPropertySymbol => kindFilter is DeadCodeKindFilter.Property,
            IFieldSymbol => kindFilter is DeadCodeKindFilter.Field,
            IEventSymbol => kindFilter is DeadCodeKindFilter.Event,
            _ => false
        };
    }

    private static bool MatchesNamedTypeKind(INamedTypeSymbol symbol, DeadCodeKindFilter kindFilter)
    {
        if (kindFilter == DeadCodeKindFilter.Type) return true;
        if (kindFilter == DeadCodeKindFilter.Class && symbol.TypeKind == TypeKind.Class) return true;
        if (kindFilter == DeadCodeKindFilter.Delegate && symbol.TypeKind == TypeKind.Delegate) return true;
        return false;
    }

    internal static bool ShouldCheckMemberKind(ISymbol member, DeadCodeKindFilter kindFilter)
    {
        if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.Constructor })
            return MatchesKindFilter(member, kindFilter);
        if (member is IPropertySymbol or IFieldSymbol or IEventSymbol)
            return MatchesKindFilter(member, kindFilter);
        return false;
    }

    internal static bool MatchesAccessibilityFilter(Accessibility accessibility, DeadCodeAccessibilityFilter filter)
    {
        return filter switch
        {
            DeadCodeAccessibilityFilter.All => true,
            DeadCodeAccessibilityFilter.Private => accessibility == Accessibility.Private,
            DeadCodeAccessibilityFilter.Internal => accessibility == Accessibility.Internal,
            DeadCodeAccessibilityFilter.Public => accessibility == Accessibility.Public,
            DeadCodeAccessibilityFilter.PrivateInternal => accessibility is Accessibility.Private or Accessibility.Internal,
            _ => true
        };
    }

    internal static string GetSymbolKindString(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol t => GetNamedTypeKindString(t.TypeKind),
            IMethodSymbol m => m.MethodKind == MethodKind.Constructor ? "constructor" : "method",
            IPropertySymbol => "property",
            IFieldSymbol => "field",
            IEventSymbol => "event",
            _ => "symbol"
        };
    }

    private static string GetNamedTypeKindString(TypeKind typeKind)
    {
        return typeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => "type"
        };
    }

    internal static string GetAccessibilityString(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Private => "private",
            Accessibility.Internal => "internal",
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "unknown"
        };
    }
}
