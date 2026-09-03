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
        var payload = await BuildHierarchyAsync(type, solution, maxResults, ct);
        return (FormatText(payload), payload.SubtypesTruncated);
    }

    internal static async Task<TypeHierarchyPayload> BuildHierarchyAsync(
        INamedTypeSymbol type,
        Solution solution,
        int maxResults,
        CancellationToken ct,
        bool absolutePaths = false)
    {
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";

        var baseTypes = FormatBaseTypes(type, outputRoot, absolutePaths).ToList();
        var interfaces = FormatInterfaces(type, outputRoot, absolutePaths).ToList();
        var subtypeProjection = await ProjectSubtypesAsync(type, solution, outputRoot, maxResults, absolutePaths, ct);
        var diHits = await DiRegistrationHeuristics.FindRegistrationsAsync(solution, type, ct);
        return new(
            type.ToDisplayString(),
            baseTypes,
            interfaces,
            type.TypeKind == TypeKind.Interface ? "Implementierende Typen:" : "Abgeleitete Klassen:",
            subtypeProjection.ShownLines,
            subtypeProjection.TotalCount,
            subtypeProjection.ShownCount,
            subtypeProjection.IsTruncated,
            subtypeProjection.IsTruncated ? ["maxResults"] : [],
            diHits);
    }

    internal static string FormatText(TypeHierarchyPayload payload)
    {
        var sections = new List<string>
        {
            FormatSection("Basisklassen:", payload.BaseTypes, "Keine Basisklasse."),
            FormatSection("Implementierte Interfaces:", payload.Interfaces, "Keine Interfaces."),
            FormatSubtypesSection(payload),
        };
        if (payload.DiRegistrations.Count > 0)
        {
            sections.Add(FormatDiRegistrationSection(payload.DiRegistrations));
        }

        return string.Join("\n\n", sections);
    }

    private static string FormatDiRegistrationSection(IReadOnlyList<string> hits)
    {
        var header = "DI-Registrierungen (heuristisch, Convention-/Factory-basiertes Scanning nicht abgedeckt):";
        return $"{header}\n{string.Join("\n", hits)}";
    }

    private static IEnumerable<string> FormatBaseTypes(INamedTypeSymbol type, string outputRoot, bool absolutePaths)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            foreach (var line in FormatHierarchyTypeReference(current, outputRoot, absolutePaths))
            {
                yield return line;
            }

            current = current.BaseType;
        }
    }

    private static IEnumerable<string> FormatInterfaces(INamedTypeSymbol type, string outputRoot, bool absolutePaths)
    {
        return type.AllInterfaces.SelectMany(i => FormatHierarchyTypeReference(i, outputRoot, absolutePaths));
    }

    /// <summary>
    /// Formatiert einen Basistyp/ein Interface fuer die Basisklassen-/Interface-Sektionen. Anders als
    /// <see cref="FindSymbolTool.FormatSymbolLocations"/> (gedacht fuer lokale Symbol-Fundstellen,
    /// daher auf <c>IsInSource</c> gefiltert) verwirft dies Typen ohne Quell-Location nicht: BCL-/NuGet-
    /// Basistypen und -Interfaces (z. B. <c>object</c>, <c>IDisposable</c>, <c>CSharpSyntaxWalker</c>)
    /// sind hier der Normalfall, kein Sonderfall, und muessen sichtbar bleiben statt spurlos zu
    /// verschwinden.
    /// </summary>
    private static IEnumerable<string> FormatHierarchyTypeReference(
        INamedTypeSymbol symbol, string outputRoot, bool absolutePaths)
    {
        var sourceLines = FindSymbolTool.FormatSymbolLocations(
            symbol,
            outputRoot,
            absolutePaths: absolutePaths).ToList();
        if (sourceLines.Count > 0)
        {
            return sourceLines;
        }

        var kindLabel = symbol.TypeKind == TypeKind.Interface ? "Interface" : "Klasse";
        return new[] { $"{kindLabel}: {symbol.ToDisplayString()} (extern, keine Datei im Repo)" };
    }

    private static async Task<SubtypeProjection> ProjectSubtypesAsync(
        INamedTypeSymbol type,
        Solution solution,
        string outputRoot,
        int maxResults,
        bool absolutePaths,
        CancellationToken ct)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(
                type, solution, transitive: true, cancellationToken: ct);
            return ProjectSubtypes(implementations.ToList(), outputRoot, maxResults, absolutePaths);
        }

        var derived = await SymbolFinder.FindDerivedClassesAsync(
            type, solution, transitive: true, cancellationToken: ct);
        return ProjectSubtypes(derived.ToList(), outputRoot, maxResults, absolutePaths);
    }

    /// <summary>
    /// Trunkiert auf Typ-Ebene (nicht auf Zeilen-Ebene) — ein Typ mit mehreren Quell-Locations
    /// (z. B. <c>partial class</c>) darf nicht mehrere "Slots" im Limit verbrauchen. Meta-Zeile
    /// nennt die Gesamtzahl der TYPEN, nicht der formatierten Zeilen.
    /// </summary>
    private static SubtypeProjection ProjectSubtypes(
        IReadOnlyList<ISymbol> types, string outputRoot, int maxResults, bool absolutePaths)
    {
        var isTruncated = types.Count > maxResults;
        var shown = isTruncated ? types.Take(maxResults).ToList() : types;
        var lines = shown.SelectMany(s => FindSymbolTool.FormatSymbolLocations(
            s,
            outputRoot,
            absolutePaths: absolutePaths));
        return new(types.Count, shown.Count, isTruncated, lines.ToList());
    }

    private static string FormatSubtypesSection(TypeHierarchyPayload payload)
    {
        var body = payload.Subtypes.Count == 0
            ? "Keine abgeleiteten Typen."
            : string.Join("\n", payload.Subtypes);
        return payload.SubtypesTruncated
            ? $"{payload.SubtypeHeading}\n{body}\n[{payload.TotalSubtypeCount} Typen gesamt, {payload.ShownSubtypeCount} gezeigt — maxResults erhoehen]"
            : $"{payload.SubtypeHeading}\n{body}";
    }

    private static string FormatSection(string heading, IEnumerable<string> lines, string emptyMessage)
    {
        var materialized = lines.ToList();
        var body = materialized.Count == 0 ? emptyMessage : string.Join("\n", materialized);
        return $"{heading}\n{body}";
    }

    private sealed record SubtypeProjection(
        int TotalCount,
        int ShownCount,
        bool IsTruncated,
        IReadOnlyList<string> ShownLines);
}
