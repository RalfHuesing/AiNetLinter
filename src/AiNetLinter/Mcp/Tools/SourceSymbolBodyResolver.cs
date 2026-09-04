#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp.Assemblies.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools;

internal static class SourceSymbolBodyResolver
{
    internal static AssemblyBodyResolution Resolve(
        ISymbol symbol,
        int maxBodyLines,
        AssemblyOrigin? origin = null,
        int startLine = 1)
    {
        var hasSyntax = symbol.DeclaringSyntaxReferences.Any();
        var unavailable = HasUnavailableBody(symbol, hasSyntax);
        var hint = GetHint(symbol, hasSyntax, unavailable);
        var (body, totalLines, displayedStart, displayedEnd, hasMore) = Extract(symbol, maxBodyLines, startLine);
        return new(
            body,
            unavailable ? "unavailable" : origin?.BodyAvailability ?? "available",
            origin?.ContentMode ?? "source",
            hint,
            totalLines,
            displayedStart,
            displayedEnd,
            hasMore);
    }

    private static bool HasUnavailableBody(ISymbol symbol, bool hasSyntax) =>
        !hasSyntax || GetDeclaringType(symbol)?.TypeKind == TypeKind.Interface
        || symbol switch
        {
            IMethodSymbol method => method.IsAbstract || HasExternModifier(method),
            IPropertySymbol property => HasNoBody(property),
            IEventSymbol eventSymbol => eventSymbol.AddMethod?.IsAbstract == true
                || eventSymbol.RemoveMethod?.IsAbstract == true,
            _ => false,
        };

    private static string? GetHint(ISymbol symbol, bool hasSyntax, bool unavailable)
    {
        if (GetDeclaringType(symbol)?.TypeKind == TypeKind.Interface)
            return "Interfaces stellen keinen ausführbaren Body bereit.";
        if (!hasSyntax) return "Für das Symbol ist kein Quell-Syntax verfügbar.";
        return unavailable ? "Das Symbol ist abstract oder extern und besitzt keinen Body." : null;
    }

    private static INamedTypeSymbol? GetDeclaringType(ISymbol symbol) =>
        symbol as INamedTypeSymbol ?? symbol.ContainingType;

    private static bool HasNoBody(IPropertySymbol property) =>
        property.GetMethod?.IsAbstract == true
        || property.SetMethod?.IsAbstract == true
        || HasExternModifier(property.GetMethod)
        || HasExternModifier(property.SetMethod);

    private static bool HasExternModifier(ISymbol? symbol) =>
        symbol?.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<MemberDeclarationSyntax>()
            .Any(member => member.Modifiers.Any(SyntaxKind.ExternKeyword)) == true;

    private static (string Body, int TotalLines, int DisplayedStart, int DisplayedEnd, bool HasMore) Extract(
        ISymbol symbol,
        int maxBodyLines,
        int startLine)
    {
        var normalizedMax = Math.Max(1, maxBodyLines);
        var normalizedStart = Math.Max(1, startLine);
        var declaringReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (declaringReference is null)
            return ($"// Kein Quell-Syntax verfuegbar fuer '{symbol.ToDisplayString()}' (externes Symbol).", 0, 1, 0, false);

        var text = declaringReference.GetSyntax().ToFullString();
        var lines = text.Split('\n');
        var totalLines = lines.Length;

        if (normalizedStart > totalLines)
        {
            return (
                $"// startLine {normalizedStart} liegt ausserhalb der Methode (total {totalLines} Zeilen).",
                totalLines,
                normalizedStart,
                normalizedStart,
                false);
        }

        var startIndex = normalizedStart - 1;
        var count = Math.Min(normalizedMax, totalLines - startIndex);
        var selectedLines = lines.Skip(startIndex).Take(count).ToArray();
        var displayedEnd = normalizedStart + count - 1;
        var hasMore = displayedEnd < totalLines;

        var body = string.Join("\n", selectedLines).TrimEnd();
        if (hasMore)
        {
            body += $"\n// ... truncated, total {totalLines} Zeilen (angezeigt {normalizedStart}-{displayedEnd}), startLine/maxBodyLines anpassen fuer mehr";
        }

        return (body, totalLines, normalizedStart, displayedEnd, hasMore);
    }
}
