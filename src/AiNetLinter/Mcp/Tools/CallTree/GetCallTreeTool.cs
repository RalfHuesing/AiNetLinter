#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.CallTree;

/// <summary>
/// MCP-Tool <c>get_call_tree</c>: loest einen Symbol-Identifikator wie <see cref="FindReferencesTool"/>
/// auf und liefert dessen transitiven Aufrufer- oder Aufgerufene-Baum als echte Eltern-Kind-Struktur —
/// im Unterschied zur flachen Top-N-Liste von <c>find_references</c>/<c>get_impact</c> mit <c>depth&gt;1</c>.
/// Traversierung via <see cref="CallGraphTreeBuilder.BuildTreeAsync"/> (eigene, hoehere Grenzwerte
/// als die flache Aggregation), Ausgabe als ASCII-Baum (<see cref="MetricsTreeRenderer"/>,
/// wiederverwendet aus <c>metrics_tree</c>) oder Mermaid-Flowchart
/// (<see cref="CallTreeMermaidRenderer"/>). Deckt nur .cs-Dateien ab (Roslyn-Symbolgraph).
/// Die Richtung ist standardmaessig eingehend (<c>incoming</c>); <c>outgoing</c> und <c>both</c> werden
/// optional unterstuetzt. Deckt nur .cs-Dateien ab (Roslyn-Symbolgraph).
/// </summary>
internal static class GetCallTreeTool
{
    private const string MermaidFormat = "mermaid";

    /// <summary>
    /// Tool-Einstiegspunkt: prueft Solution-Ladezustand, loest den Identifikator auf, baut den
    /// Aufrufer- oder Aufgerufene-Baum und rendert ihn im angeforderten Format. Fehlerbehandlung/Warnhinweis-Muster
    /// identisch zu <see cref="FindReferencesTool.ExecuteAsync"/> (Compile-Fehler-Warnhinweis,
    /// defensiver try/catch, Sufficiency-Hinweis nur fuer nicht-trunkierte Ergebnisse).
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, GetCallTreeInput input, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrEmpty(input.SymbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifierHint);
        }

        if (!TryParseDirection(input.Direction, out var direction))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Ungueltiger Wert fuer 'direction': '{input.Direction}'.",
                hint: "direction muss 'incoming', 'outgoing' oder 'both' sein.");
        }

        try
        {
            var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
                solution,
                input.SymbolIdentifier,
                ct,
                state.AssemblySymbolIdentity);
            if (error is not null) return error;

            var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
            var topN = input.TopN < 1 ? 1 : input.TopN;
            var (root, truncated) = await CallGraphTreeBuilder.BuildTreeAsync(
                new CallTreeBuildRequest(solution, symbol!, input.Depth, topN, direction), ct);

            var body = RenderTree(root, input.Format, topN);
            // "truncated" deckt nur den 250-Knoten-Hardcap von BuildTreeAsync ab. Der Renderer
            // kappt zusaetzlich pro Ebene auf topN und haengt bei Ueberschuss eine eigene
            // "... und N weitere"-Zeile an (MetricsTreeRenderer/CallTreeMermaidRenderer) — ohne
            // diesen Fall zeigte der Sufficiency-Hinweis faelschlich "vollstaendig" an, obwohl der
            // Baum sichtbar gekappt war. Marker-String-Erkennung analog zum "hard-cap"-Muster in
            // FindReferencesTool.
            var topNTruncated = HasTreeOverflow(root, topN);
            var finalBody = truncated
                ? body + "\n\n" + BuildTruncationMeta()
                : topNTruncated
                    ? body + "\n\n" + BuildTopNTruncationMeta()
                    : McpSufficiencyHints.Append(body);

            return McpToolResults.Text(
                FindSymbolTool.PrependWarning(warning, finalBody),
                new CallTreePayload(
                    root,
                    CallTreeDirectionNames.For(direction),
                    input.Depth,
                    topN,
                    truncated || topNTruncated,
                    topNTruncated));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_call_tree: {ex.Message}",
                context: input.SymbolIdentifier);
        }
    }

    internal static bool TryParseDirection(string? value, out CallTreeDirection direction)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, CallTreeDirectionNames.Incoming, StringComparison.OrdinalIgnoreCase))
        {
            direction = CallTreeDirection.Incoming;
            return true;
        }

        if (string.Equals(value, CallTreeDirectionNames.Outgoing, StringComparison.OrdinalIgnoreCase))
        {
            direction = CallTreeDirection.Outgoing;
            return true;
        }

        if (string.Equals(value, CallTreeDirectionNames.Both, StringComparison.OrdinalIgnoreCase))
        {
            direction = CallTreeDirection.Both;
            return true;
        }

        direction = default;
        return false;
    }

    internal static string RenderTree(MetricsTreeNode root, string? format, int topN) =>
        string.Equals(format, MermaidFormat, StringComparison.OrdinalIgnoreCase)
            ? CallTreeMermaidRenderer.Render(root, topN)
            : MetricsTreeRenderer.Render(root, topN, sortDescending: false);

    internal static bool HasTreeOverflow(MetricsTreeNode root, int topN) =>
        root.Children.Count > topN || root.Children.Any(child => HasTreeOverflow(child, topN));

    private static string BuildTruncationMeta() =>
        $"[Baum trunkiert — hard-cap {CallGraphTreeBuilder.MaxCallTreeNodes} Knoten erreicht, " +
        "depth oder topN reduzieren fuer einen vollstaendigeren Teilbaum]";

    private static string BuildTopNTruncationMeta() =>
        "[Baum trunkiert — mindestens eine Ebene hat mehr Kinder als topN, siehe " +
        "\"... und N weitere\"-Zeilen; topN erhoehen fuer einen vollstaendigeren Teilbaum]";
}
