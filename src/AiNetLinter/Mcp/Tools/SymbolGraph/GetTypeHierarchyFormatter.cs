#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.FileStructure;
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

    // ainetlinter-disable MaxMethodParameterCount — alle Parameter bilden den einen bounded Hierarchie-Aufruf.
    internal static async Task<TypeHierarchyPayload> BuildHierarchyAsync(
        INamedTypeSymbol type,
        Solution solution,
        int maxResults,
        CancellationToken ct,
        HierarchyBuildOptions? options = null)
    {
        options ??= new HierarchyBuildOptions();
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var classifier = options.ScopeClassifier ?? new McpScopeClassifier();

        var baseTypes = FormatBaseTypes(type, outputRoot, options.AbsolutePaths, options.HandoffIdentity).ToList();
        var interfaces = FormatInterfaces(type, outputRoot, options.AbsolutePaths, options.HandoffIdentity).ToList();
        var subtypeProjection = await ProjectSubtypesAsync(
            type,
            solution,
            outputRoot,
            maxResults,
            options.AbsolutePaths,
            options.HandoffIdentity,
            options.ScopeType,
            options.IncludeGenerated,
            classifier,
            ct);
        var diHits = await DiRegistrationHeuristics.FindRegistrationsAsync(
            new FindRegistrationsRequest(
                solution,
                type,
                options.ScopeType,
                options.IncludeGenerated,
                classifier),
            ct);
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
            diHits,
            new FindSymbolScopeDto(McpScopeValues.ToWireValue(options.ScopeType), options.IncludeGenerated));
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

    private static IEnumerable<TypeHierarchyEntryDto> FormatBaseTypes(
        INamedTypeSymbol type,
        string outputRoot,
        bool absolutePaths,
        AnalysisSymbolIdentity? handoffIdentity)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            foreach (var entry in FormatHierarchyTypeReference(current, outputRoot, absolutePaths, handoffIdentity))
            {
                yield return entry;
            }

            current = current.BaseType;
        }
    }

    private static IEnumerable<TypeHierarchyEntryDto> FormatInterfaces(
        INamedTypeSymbol type,
        string outputRoot,
        bool absolutePaths,
        AnalysisSymbolIdentity? handoffIdentity)
    {
        return type.AllInterfaces.SelectMany(i => FormatHierarchyTypeReference(i, outputRoot, absolutePaths, handoffIdentity));
    }

    /// <summary>
    /// Formatiert einen Basistyp/ein Interface fuer die Basisklassen-/Interface-Sektionen. Anders als
    /// <see cref="FindSymbolTool.FormatSymbolLocations"/> (gedacht fuer lokale Symbol-Fundstellen,
    /// daher auf <c>IsInSource</c> gefiltert) verwirft dies Typen ohne Quell-Location nicht: BCL-/NuGet-
    /// Basistypen und -Interfaces (z. B. <c>object</c>, <c>IDisposable</c>, <c>CSharpSyntaxWalker</c>)
    /// sind hier der Normalfall, kein Sonderfall, und muessen sichtbar bleiben statt spurlos zu
    /// verschwinden.
    /// </summary>
    private static IEnumerable<TypeHierarchyEntryDto> FormatHierarchyTypeReference(
        INamedTypeSymbol symbol,
        string outputRoot,
        bool absolutePaths,
        AnalysisSymbolIdentity? handoffIdentity)
    {
        var sourceEntries = FindSymbolTool.FormatSymbolLocationEntries(
            symbol,
            outputRoot,
            handoffIdentity,
            absolutePaths: absolutePaths)
            .Select(entry => new TypeHierarchyEntryDto(
                entry.Name,
                entry.Kind,
                entry.FilePath,
                entry.Line,
                entry.Id,
                entry.HandoffKind,
                entry.Origin))
            .ToList();
        if (sourceEntries.Count > 0)
        {
            return sourceEntries;
        }

        var kindLabel = SymbolKindClassifier.DescribeNamedTypeKind(symbol);
        return new[] { new TypeHierarchyEntryDto(
            symbol.ToDisplayString(),
            kindLabel,
            null,
            null,
            null,
            null,
            null) };
    }

    private static async Task<SubtypeProjection> ProjectSubtypesAsync(
        INamedTypeSymbol type,
        Solution solution,
        string outputRoot,
        int maxResults,
        bool absolutePaths,
        AnalysisSymbolIdentity? handoffIdentity,
        McpScopeType scopeType,
        bool includeGenerated,
        McpScopeClassifier classifier,
        CancellationToken ct)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(
                type, solution, transitive: true, cancellationToken: ct);
            return await ProjectSubtypesAsync(
                implementations.ToList(), solution, outputRoot, maxResults, absolutePaths, handoffIdentity,
                scopeType, includeGenerated, classifier, ct).ConfigureAwait(false);
        }

        var derived = await SymbolFinder.FindDerivedClassesAsync(
            type, solution, transitive: true, cancellationToken: ct);
        return await ProjectSubtypesAsync(
            derived.ToList(), solution, outputRoot, maxResults, absolutePaths, handoffIdentity,
            scopeType, includeGenerated, classifier, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Trunkiert auf Typ-Ebene (nicht auf Zeilen-Ebene) — ein Typ mit mehreren Quell-Locations
    /// (z. B. <c>partial class</c>) darf nicht mehrere "Slots" im Limit verbrauchen. Meta-Zeile
    /// nennt die Gesamtzahl der TYPEN, nicht der formatierten Zeilen.
    /// </summary>
    private static async Task<SubtypeProjection> ProjectSubtypesAsync(
        IReadOnlyList<ISymbol> types,
        Solution solution,
        string outputRoot,
        int maxResults,
        bool absolutePaths,
        AnalysisSymbolIdentity? handoffIdentity,
        McpScopeType scopeType,
        bool includeGenerated,
        McpScopeClassifier classifier,
        CancellationToken cancellationToken)
    {
        var scoped = new List<(ISymbol Symbol, McpSymbolScope Scope)>();
        foreach (var symbol in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = await classifier.ClassifySymbolAsync(
                symbol, solution, scopeType, includeGenerated, cancellationToken).ConfigureAwait(false);
            if (scope.IsVisible) scoped.Add((symbol, scope));
        }

        var ordered = scoped
            .OrderBy(item => ProjectRank(item.Scope.ProjectKind))
            .ThenBy(item => SourceRank(item.Scope.SourceKind))
            .ThenBy(item => item.Symbol.ToDisplayString(), StringComparer.Ordinal)
            .ToList();
        var isTruncated = ordered.Count > maxResults;
        var shown = isTruncated ? ordered.Take(maxResults).ToList() : ordered;
        var entries = shown.SelectMany(item => FormatSubtype(item.Symbol, item.Scope, outputRoot, absolutePaths, handoffIdentity));
        return new(ordered.Count, shown.Count, isTruncated, entries.ToList());
    }

    private static IEnumerable<TypeHierarchyEntryDto> FormatSubtype(
        ISymbol symbol,
        McpSymbolScope scope,
        string outputRoot,
        bool absolutePaths,
        AnalysisSymbolIdentity? handoffIdentity)
    {
        var entry = FindSymbolTool.FormatSymbolLocationEntries(
                symbol, outputRoot, handoffIdentity, absolutePaths)
            .FirstOrDefault();
        if (entry is null)
        {
            yield return new TypeHierarchyEntryDto(
                symbol.ToDisplayString(),
                SymbolKindClassifier.DescribeNamedTypeKind((INamedTypeSymbol)symbol));
            yield break;
        }

        yield return new TypeHierarchyEntryDto(
            entry.Name,
            entry.Kind,
            entry.FilePath,
            entry.Line,
            entry.Id,
            entry.HandoffKind,
            entry.Origin,
            McpScopeValues.ToWireValue(scope.ProjectKind),
            McpScopeValues.ToWireValue(scope.SourceKind));
    }

    private static int ProjectRank(McpProjectKind kind) => kind switch
    {
        McpProjectKind.Production => 0,
        McpProjectKind.Tests => 1,
        _ => 2,
    };

    private static int SourceRank(McpSourceKind kind) => kind == McpSourceKind.Editable ? 0 : 1;

    private static string FormatSubtypesSection(TypeHierarchyPayload payload)
    {
        var body = payload.Subtypes.Count == 0
            ? "Keine abgeleiteten Typen."
            : string.Join("\n", payload.Subtypes.Select(FormatEntry));
        return payload.SubtypesTruncated
            ? $"{payload.SubtypeHeading}\n{body}\n[{payload.TotalSubtypeCount} Typen gesamt, {payload.ShownSubtypeCount} gezeigt — maxResults erhoehen]"
            : $"{payload.SubtypeHeading}\n{body}";
    }

    private static string FormatSection(
        string heading,
        IEnumerable<TypeHierarchyEntryDto> entries,
        string emptyMessage)
    {
        var materialized = entries.ToList();
        var body = materialized.Count == 0
            ? emptyMessage
            : string.Join("\n", materialized.Select(FormatEntry));
        return $"{heading}\n{body}";
    }

    private static string FormatEntry(TypeHierarchyEntryDto entry)
    {
        if (entry.FilePath is null || entry.Line is null)
        {
            return $"{entry.Kind}: {entry.Name} (extern, keine Datei im Repo)";
        }

        var origin = entry.Origin is null
            ? string.Empty
            : $" [assembly={entry.Origin.CanonicalPath}; origin={entry.Origin.OriginKind}]";
        return $"{entry.Kind} {entry.Name} — {entry.FilePath}:{entry.Line}{origin}";
    }

    private sealed record SubtypeProjection(
        int TotalCount,
        int ShownCount,
        bool IsTruncated,
        IReadOnlyList<TypeHierarchyEntryDto> ShownLines);
}
