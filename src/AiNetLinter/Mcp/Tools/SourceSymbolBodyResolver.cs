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
        AssemblyOrigin? origin = null)
    {
        var hasSyntax = symbol.DeclaringSyntaxReferences.Any();
        var unavailable = HasUnavailableBody(symbol, hasSyntax);
        var hint = GetHint(symbol, hasSyntax, unavailable);
        return new(
            Extract(symbol, maxBodyLines),
            unavailable ? "unavailable" : origin?.BodyAvailability ?? "available",
            origin?.ContentMode ?? "source",
            hint);
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
