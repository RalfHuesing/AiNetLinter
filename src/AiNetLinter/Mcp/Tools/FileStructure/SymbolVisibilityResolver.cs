#nullable enable

using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Gemeinsamer Helper zur Ermittlung der Sichtbarkeit eines Roslyn-Symbols.
/// </summary>
internal static class SymbolVisibilityResolver
{
    internal static string ResolveVisibility(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private",
        };

    internal static string ResolveVisibility(ISymbol symbol) =>
        ResolveVisibility(symbol.DeclaredAccessibility);
}
