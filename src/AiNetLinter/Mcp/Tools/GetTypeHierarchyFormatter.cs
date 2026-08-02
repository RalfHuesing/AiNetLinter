#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Reine Traversierungs-/Formatierungslogik fuer <see cref="GetTypeHierarchyTool"/> — in eine eigene
/// Datei ausgelagert, damit <see cref="GetTypeHierarchyTool"/>s eigener <c>AIContextFootprint</c>
/// (siehe <c> klein bleibt, analog zu
/// <see cref="SymbolIdentifierResolver"/> fuer <see cref="FindReferencesTool"/>. Keine Abhaengigkeit
/// von <see cref="McpCodeGraphServer"/> — direkt unit-testbar.
/// </summary>
internal static class GetTypeHierarchyFormatter
{
    internal static async Task<string> BuildHierarchyTextAsync(
        INamedTypeSymbol type, Solution solution, CancellationToken ct)
    {
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";

        var sections = new List<string>
        {
            FormatSection("Basisklassen:", FormatBaseTypes(type, outputRoot), "Keine Basisklasse."),
            FormatSection("Implementierte Interfaces:", FormatInterfaces(type, outputRoot), "Keine Interfaces."),
            await FormatSubtypesSectionAsync(type, solution, outputRoot, ct),
        };

        return string.Join("\n\n", sections);
    }

    private static IEnumerable<string> FormatBaseTypes(INamedTypeSymbol type, string outputRoot)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            foreach (var line in FormatHierarchyTypeReference(current, outputRoot))
            {
                yield return line;
            }

            current = current.BaseType;
        }
    }

    private static IEnumerable<string> FormatInterfaces(INamedTypeSymbol type, string outputRoot)
    {
        return type.AllInterfaces.SelectMany(i => FormatHierarchyTypeReference(i, outputRoot));
    }

    private static IEnumerable<string> FormatHierarchyTypeReference(INamedTypeSymbol symbol, string outputRoot)
    {
        var sourceLines = FindSymbolTool.FormatSymbolLocations(symbol, outputRoot).ToList();
        if (sourceLines.Count > 0)
        {
            return sourceLines;
        }

        var kindLabel = symbol.TypeKind == TypeKind.Interface ? "Interface" : "Klasse";
        return new[] { $"{kindLabel}: {symbol.ToDisplayString()} (extern, keine Datei im Repo)" };
    }

    private static async Task<string> FormatSubtypesSectionAsync(
        INamedTypeSymbol type, Solution solution, string outputRoot, CancellationToken ct)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(
                type, solution, transitive: true, cancellationToken: ct);
            var lines = implementations.SelectMany(s => FindSymbolTool.FormatSymbolLocations(s, outputRoot));
            return FormatSection("Implementierende Typen:", lines, "Keine implementierenden Typen.");
        }

        var derived = await SymbolFinder.FindDerivedClassesAsync(
            type, solution, transitive: true, cancellationToken: ct);
        var derivedLines = derived.SelectMany(s => FindSymbolTool.FormatSymbolLocations(s, outputRoot));
        return FormatSection("Abgeleitete Klassen:", derivedLines, "Keine abgeleiteten Typen.");
    }

    private static string FormatSection(string heading, IEnumerable<string> lines, string emptyMessage)
    {
        var materialized = lines.ToList();
        var body = materialized.Count == 0 ? emptyMessage : string.Join("\n", materialized);
        return $"{heading}\n{body}";
    }
}
