#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp.Assemblies.Analysis;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools;

internal static class SourceSymbolBodyResolver
{
    internal static AssemblyBodyResolution Resolve(
        ISymbol symbol,
        int maxBodyLines)
    {
        var hasSyntax = symbol.DeclaringSyntaxReferences.Any();
        var unavailable = HasUnavailableBody(symbol, hasSyntax);
        var hint = GetHint(symbol, hasSyntax, unavailable);
        return new(
            Extract(symbol, maxBodyLines),
            unavailable ? "unavailable" : "available",
            "source",
            hint);
    }

    private static bool HasUnavailableBody(ISymbol symbol, bool hasSyntax) =>
        !hasSyntax || symbol.ContainingType?.TypeKind == TypeKind.Interface || HasUnavailableMember(symbol);

    private static bool HasUnavailableMember(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol method => method.IsAbstract || AssemblyBodySyntax.HasExternModifier(method),
            IPropertySymbol property => AssemblyBodySyntax.HasNoBody(property),
            IEventSymbol eventSymbol => eventSymbol.AddMethod?.IsAbstract == true
                || eventSymbol.RemoveMethod?.IsAbstract == true,
            _ => false,
        };

    private static string? GetHint(ISymbol symbol, bool hasSyntax, bool unavailable)
    {
        if (symbol.ContainingType?.TypeKind == TypeKind.Interface)
            return "Interfaces stellen keinen ausführbaren Body bereit.";
        if (!hasSyntax) return "Für das Symbol ist kein Quell-Syntax verfügbar.";
        return unavailable ? "Das Symbol ist abstract oder extern und besitzt keinen Body." : null;
    }

    private static string Extract(ISymbol symbol, int maxBodyLines)
    {
        var normalized = Math.Max(1, maxBodyLines);
        var declaringReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (declaringReference is null)
            return $"// Kein Quell-Syntax verfuegbar fuer '{symbol.ToDisplayString()}' (externes Symbol).";

        var text = declaringReference.GetSyntax().ToFullString();
        var lines = text.Split('\n');
        if (lines.Length <= normalized) return text.TrimEnd();

        return string.Join("\n", lines.Take(normalized)).TrimEnd()
            + $"\n// ... truncated, total {lines.Length} Zeilen, maxBodyLines erhoehen fuer mehr";
    }
}
