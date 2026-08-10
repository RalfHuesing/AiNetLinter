#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Reine Traversierungs-/Formatierungslogik fuer <see cref="GetTypeHierarchyTool"/> — in eine eigene
/// Datei ausgelagert, damit <see cref="GetTypeHierarchyTool"/>s eigener <c>AIContextFootprint</c>
/// (siehe <c>AiNetLinter.mdc</c>) klein bleibt, analog zu
/// <see cref="SymbolIdentifierResolver"/> fuer <see cref="FindReferencesTool"/>. Keine Abhaengigkeit
/// von <see cref="McpCodeGraphServer"/> — direkt unit-testbar.
/// </summary>
internal static class GetTypeHierarchyFormatter
{
    /// <summary>
    /// Baut den Hierarchie-Text fuer <paramref name="type"/>: Basisklassen-Kette, implementierte
    /// Interfaces sowie (je nach <see cref="ITypeSymbol.TypeKind"/>) abgeleitete Klassen bzw.
    /// implementierende Typen — letztere Sektion trunkiert auf <paramref name="maxResults"/>
    /// (Basisklassen/Interfaces bleiben untrunkiert, weil sie durch die eigene Deklaration des
    /// Typs begrenzt sind; abgeleitete/implementierende Typen sind dagegen transitiv ueber die
    /// GESAMTE Solution aufgeloest — z. B. bei einem weit implementierten Marker-Interface wie
    /// <c>IDisposable</c> potenziell hunderte Treffer, die ohne Limit den Client-Token-Guard
    /// sprengen koennten, dieselbe Bug-Klasse wie bei <c>get_violations</c>/<c>get_hotspots</c>).
    /// Anhaengend eine 4. Sektion mit heuristischen DI-Registrierungs-Funden via
    /// <see cref="DiRegistrationHeuristics"/> (nur wenn Treffer vorhanden — bei 0 Treffern wird die
    /// Sektion weggelassen, um die uebliche Antwort nicht zu verlangern).
    /// </summary>
    internal static async Task<(string Text, bool IsTruncated)> BuildHierarchyTextAsync(
        INamedTypeSymbol type, Solution solution, int maxResults, CancellationToken ct)
    {
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";

        var (subtypesSection, isTruncated) = await FormatSubtypesSectionAsync(type, solution, outputRoot, maxResults, ct);
        var sections = new List<string>
        {
            FormatSection("Basisklassen:", FormatBaseTypes(type, outputRoot), "Keine Basisklasse."),
            FormatSection("Implementierte Interfaces:", FormatInterfaces(type, outputRoot), "Keine Interfaces."),
            subtypesSection,
        };

        var diHits = await DiRegistrationHeuristics.FindRegistrationsAsync(solution, type, ct);
        if (diHits.Count > 0)
        {
            sections.Add(FormatDiRegistrationSection(diHits));
        }

        return (string.Join("\n\n", sections), isTruncated);
    }

    private static string FormatDiRegistrationSection(IReadOnlyList<string> hits)
    {
        var header = "DI-Registrierungen (heuristisch, Convention-/Factory-basiertes Scanning nicht abgedeckt):";
        return $"{header}\n{string.Join("\n", hits)}";
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

    /// <summary>
    /// Formatiert einen Basistyp/ein Interface fuer die Basisklassen-/Interface-Sektionen. Anders als
    /// <see cref="FindSymbolTool.FormatSymbolLocations"/> (gedacht fuer lokale Symbol-Fundstellen,
    /// daher auf <c>IsInSource</c> gefiltert) verwirft dies Typen ohne Quell-Location nicht: BCL-/NuGet-
    /// Basistypen und -Interfaces (z. B. <c>object</c>, <c>IDisposable</c>, <c>CSharpSyntaxWalker</c>)
    /// sind hier der Normalfall, kein Sonderfall, und muessen sichtbar bleiben statt spurlos zu
    /// verschwinden.
    /// </summary>
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

    private static async Task<(string Text, bool IsTruncated)> FormatSubtypesSectionAsync(
        INamedTypeSymbol type, Solution solution, string outputRoot, int maxResults, CancellationToken ct)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(
                type, solution, transitive: true, cancellationToken: ct);
            return FormatTruncatedSubtypesSection(
                "Implementierende Typen:", implementations.ToList(), outputRoot, "Keine implementierenden Typen.", maxResults);
        }

        var derived = await SymbolFinder.FindDerivedClassesAsync(
            type, solution, transitive: true, cancellationToken: ct);
        return FormatTruncatedSubtypesSection(
            "Abgeleitete Klassen:", derived.ToList(), outputRoot, "Keine abgeleiteten Typen.", maxResults);
    }

    /// <summary>
    /// Trunkiert auf Typ-Ebene (nicht auf Zeilen-Ebene) — ein Typ mit mehreren Quell-Locations
    /// (z. B. <c>partial class</c>) darf nicht mehrere "Slots" im Limit verbrauchen. Meta-Zeile
    /// nennt die Gesamtzahl der TYPEN, nicht der formatierten Zeilen.
    /// </summary>
    private static (string Text, bool IsTruncated) FormatTruncatedSubtypesSection(
        string heading, IReadOnlyList<ISymbol> types, string outputRoot, string emptyMessage, int maxResults)
    {
        var isTruncated = types.Count > maxResults;
        var shown = isTruncated ? types.Take(maxResults).ToList() : types;
        var lines = shown.SelectMany(s => FindSymbolTool.FormatSymbolLocations(s, outputRoot));
        var text = FormatSection(heading, lines, emptyMessage);
        if (isTruncated)
        {
            text += $"\n[{types.Count} Typen gesamt, {maxResults} gezeigt — maxResults erhoehen]";
        }
        return (text, isTruncated);
    }

    private static string FormatSection(string heading, IEnumerable<string> lines, string emptyMessage)
    {
        var materialized = lines.ToList();
        var body = materialized.Count == 0 ? emptyMessage : string.Join("\n", materialized);
        return $"{heading}\n{body}";
    }
}
