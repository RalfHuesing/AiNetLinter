#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.DependencyGraph;

/// <summary>
/// MCP-Tool <c>dependency_graph</c>: beantwortet "welche Dateien/Typen haengen von Datei/Typ X ab"
/// direkt, statt mehrere <c>find_symbol</c>/<c>find_references</c>-Umwege zu erzwingen (validierte
/// Navigations-Luecke aus einer Dogfooding-Session). Zwei gegenseitig exklusive Eingabe-Modi —
/// <see cref="DependencyGraphInput.FilePath"/> (ganze Datei, Union aller deklarierten Typen) oder
/// <see cref="DependencyGraphInput.TypeIdentifier"/> (ein einzelner Typ, engerer Scope, aufgeloest
/// ueber <see cref="FindReferencesTool.ResolveSymbolAsync"/>). Bewusst duenner Dispatch ohne eigene
/// Traversierungslogik — die eigentliche BFS/Filter-Arbeit steckt in <see cref="DependencyGraphScanner"/>.
/// Deckt nur .cs-Dateien ab (Roslyn-Symbolgraph).
/// </summary>
internal static class DependencyGraphTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, DependencyGraphInput input, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var hasFilePath = !string.IsNullOrEmpty(input.FilePath);
        var hasSymbolIdentifier = !string.IsNullOrEmpty(input.SymbolIdentifier);
        if (hasFilePath == hasSymbolIdentifier)
        {
            return McpToolResults.InvalidArgument(
                "filePath und symbolIdentifier sind gegenseitig exklusiv — genau einen angeben, nie beide oder keins.",
                hint: "Entweder filePath ODER symbolIdentifier angeben, nie beide.");
        }

        var (includeOutgoing, includeIncoming, directionError) = ParseDirection(input.Direction);
        if (directionError is not null) return directionError;

        try
        {
            return hasFilePath
                ? await ExecuteFileScopeAsync(solution, input, includeOutgoing, includeIncoming, ct)
                : await ExecuteTypeScopeAsync(solution, input, includeOutgoing, includeIncoming, state.AssemblySymbolIdentity, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in dependency_graph: {ex.Message}",
                context: hasFilePath ? input.FilePath : input.SymbolIdentifier);
        }
    }

    /// <summary>Erlaubte Werte case-insensitiv, leer/<see langword="null"/> ist gleichbedeutend mit "both".</summary>
    private static (bool IncludeOutgoing, bool IncludeIncoming, CallToolResult? Error) ParseDirection(string? direction)
    {
        if (string.IsNullOrWhiteSpace(direction)) return (true, true, null);
        return direction.Trim().ToLowerInvariant() switch
        {
            "outgoing" => (true, false, null),
            "incoming" => (false, true, null),
            "both" => (true, true, null),
            _ => (false, false, McpToolResults.InvalidArgument(
                $"Ungueltiger direction-Wert '{direction}' — gueltig sind 'incoming', 'outgoing', 'both'.",
                hint: "direction='incoming', 'outgoing' oder 'both' angeben (Default: 'both').")),
        };
    }

    private static async Task<CallToolResult> ExecuteFileScopeAsync(
        Solution solution, DependencyGraphInput input, bool includeOutgoing, bool includeIncoming, CancellationToken ct)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var absolutePath = Path.GetFullPath(Path.Combine(solutionDir, input.FilePath!));
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, input.FilePath!);
        if (document is null) return McpToolResults.FileNotFound(input.FilePath!);

        var request = new DependencyGraphScanRequest(solution, includeOutgoing, includeIncoming, input.Depth, input.MaxResults);
        var result = await DependencyGraphScanner.ScanFileAsync(document, request, ct);
        var relativePath = PathNormalizer.ToRelative(solutionDir, document.FilePath ?? absolutePath);
        var target = new DependencyGraphTarget("file", relativePath, null);
        return BuildResponse(target, result);
    }

    private static async Task<CallToolResult> ExecuteTypeScopeAsync(
        Solution solution,
        DependencyGraphInput input,
        bool includeOutgoing,
        bool includeIncoming,
        AnalysisSymbolIdentity? assemblyIdentity,
        CancellationToken ct)
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, input.SymbolIdentifier!, ct, assemblyIdentity);
        if (error is not null) return error;

        // Nicht-Typ-Symbole (Methode/Property/Feld) auf den einschliessenden Typ normalisieren —
        // macht symbolIdentifier fuer "Klasse.Member"-Eingaben genauso nutzbar wie fuer reine Typnamen.
        var targetType = symbol as INamedTypeSymbol ?? symbol!.ContainingType;
        if (targetType is null)
        {
            return McpToolResults.InvalidArgument(
                $"'{input.SymbolIdentifier}' loest zu '{symbol!.Kind}' auf — kein Typ und kein Mitglied mit einschliessendem Typ.",
                hint: "symbolIdentifier muss auf einen Typen oder Typ-Member verweisen.");
        }

        var request = new DependencyGraphScanRequest(solution, includeOutgoing, includeIncoming, input.Depth, input.MaxResults);
        var result = await DependencyGraphScanner.ScanTypeAsync(targetType, request, ct);
        var declaringPath = FormatDeclaringPath(solution, targetType);
        var target = new DependencyGraphTarget("type", declaringPath, targetType.Name);
        return BuildResponse(target, result);
    }

    private static string FormatDeclaringPath(Solution solution, INamedTypeSymbol type)
    {
        var location = type.Locations.FirstOrDefault(l => l.IsInSource && l.SourceTree is not null);
        if (location is null) return type.Name;
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        return PathNormalizer.ToRelative(solutionDir, location.SourceTree!.FilePath);
    }

    private static CallToolResult BuildResponse(
        DependencyGraphTarget target, DependencyGraphResult result)
    {
        var body = RenderText(target, result);
        // Sufficiency-Hinweis nur fuer nicht-trunkierte Ergebnisse — trunkiert durch
        // maxResults ODER durch den Traversierungs-Hard-Cap (NodeCapReached), beides zaehlt.
        var finalBody = result.Truncated ? body : McpSufficiencyHints.Append(body);
        var payload = new
        {
            Target = target,
            Direction = DirectionLabel(result),
            Edges = result.Edges,
            ProjectReferences = result.ProjectReferences,
            Truncated = result.Truncated,
        };
        // In ein Objekt gewrappt statt eines nackten Arrays — MCP-Clients validieren structuredContent
        // schema-seitig als JSON-Objekt (siehe McpToolResults.Text``1-Doc-Kommentar).
        return McpToolResults.Text(finalBody, payload);
    }

    private static string DirectionLabel(DependencyGraphResult result) =>
        (result.IncludeOutgoing, result.IncludeIncoming) switch
        {
            (true, true) => "both",
            (true, false) => "outgoing",
            _ => "incoming",
        };

    private static string RenderText(DependencyGraphTarget target, DependencyGraphResult result)
    {
        var label = target.TypeName is null ? target.Path : $"{target.TypeName} ({target.Path})";
        var sb = new StringBuilder();

        if (result.IncludeOutgoing)
        {
            AppendSection(sb, $"Ausgehende Abhaengigkeiten von '{label}' (depth={result.ClampedDepth}):",
                result.Edges.Where(e => e.Direction == "outgoing"), e => e.To);
        }
        if (result.IncludeIncoming)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            AppendSection(sb, "Eingehende Abhaengigkeiten (wer verwendet Typen aus diesem Scope):",
                result.Edges.Where(e => e.Direction == "incoming"), e => e.From);
        }
        if (result.ProjectReferences.Count > 0)
        {
            sb.Append("\n\n");
            sb.Append("Projekt-Referenzen: ");
            sb.Append(string.Join(", ", result.ProjectReferences.SelectMany(
                pr => pr.References.Select(r => $"{pr.Project} -> {r}"))));
        }
        if (result.Truncated)
        {
            sb.Append('\n');
            var capNote = result.NodeCapReached ? $" (Traversierung hart begrenzt auf {DependencyGraphScanner.MaxVisitedFiles} Dateien)" : "";
            sb.Append($"[{result.TotalEdgeCount} Kanten gesamt{capNote}, {result.Edges.Count} gezeigt — depth reduzieren oder maxResults erhoehen]");
        }

        return sb.ToString();
    }

    private static void AppendSection(
        StringBuilder sb, string header, IEnumerable<DependencyEdge> edges, Func<DependencyEdge, string> otherFileSelector)
    {
        sb.Append(header);
        var lines = edges.Select(e => FormatEdgeLine(e, otherFileSelector(e))).ToList();
        sb.Append(lines.Count == 0 ? "\n- (keine)" : "\n" + string.Join("\n", lines));
    }

    private static string FormatEdgeLine(DependencyEdge edge, string otherFile)
    {
        var typeLabel = edge.TypeNames.Count == 1 ? "Typ" : "Typen";
        return $"- {otherFile} ({edge.TypeNames.Count} {typeLabel}: {string.Join(", ", edge.TypeNames)})";
    }
}
