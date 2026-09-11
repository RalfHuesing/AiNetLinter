#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Scope;
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
    internal const int DefaultMaxResponseBytes = 32 * 1024;

    private sealed record CallTreeValidation(
        CallTreeDirection Direction,
        McpScopeType ScopeType,
        int MaxResponseBytes,
        CallToolResult? Error = null);

    /// <summary>
    /// Tool-Einstiegspunkt: prueft Solution-Ladezustand, loest den Identifikator auf, baut den
    /// Aufrufer- oder Aufgerufene-Baum und rendert ihn im angeforderten Format. Die Fehlerbehandlung
    /// verwendet einen defensiven try/catch; ein Sufficiency-Hinweis erscheint nur fuer
    /// nicht-trunkierte Ergebnisse.
    /// </summary>
    // ainetlinter-disable MaxMethodLineCount — Projekt- und Assembly-Aufloesung teilen bewusst denselben Toolvertrag.
    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, GetCallTreeInput input, CancellationToken ct)
    {
        var validation = ValidateInput(input);
        if (validation.Error is not null) return validation.Error;
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var symbolIdentifier = input.SymbolIdentifier;

        try
        {
            var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
                solution,
                symbolIdentifier!,
                ct,
                state.HandoffSymbolIdentity);
            if (error is not null) return error;

            var effectiveDepth = Math.Clamp(input.Depth, 1, CallGraphTreeBuilder.MaxCallTreeDepth);
            var topN = input.TopN < 1 ? 1 : input.TopN;
            var graph = await CallGraphTreeBuilder.BuildGraphAsync(
                new CallTreeBuildRequest(
                    solution,
                    symbol!,
                    input.Depth,
                    topN,
                    validation.Direction,
                    state.AssemblySymbolIdentity is not null,
                    input.IncludeBcl,
                    state.HandoffSymbolIdentity,
                    validation.ScopeType,
                    input.IncludeGenerated),
                ct);
            return CallGraphResponseBudget.CreateResult(
                new CallGraphResponseBudget.CallGraphResponseRequest(
                    graph,
                    input.Format,
                    CallTreeDirectionNames.For(validation.Direction),
                    input.Depth,
                    effectiveDepth,
                    topN,
                    validation.ScopeType,
                    input.IncludeGenerated,
                    validation.MaxResponseBytes));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_call_tree: {ex.Message}",
                context: symbolIdentifier);
        }
    }

    private static CallTreeValidation ValidateInput(GetCallTreeInput input)
    {
        if (string.IsNullOrEmpty(input.SymbolIdentifier))
        {
            return new(
                default,
                McpScopeType.All,
                DefaultMaxResponseBytes,
                McpToolResults.Recoverable(
                    LinterErrorCodes.InvalidArgument,
                    "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                    hint: McpToolResults.SymbolIdentifierHint));
        }

        if (!TryParseDirection(input.Direction, out var direction))
        {
            return new(
                default,
                McpScopeType.All,
                DefaultMaxResponseBytes,
                McpToolResults.Recoverable(
                    LinterErrorCodes.InvalidArgument,
                    $"Ungueltiger Wert fuer 'direction': '{input.Direction}'.",
                    hint: "direction muss 'incoming', 'outgoing' oder 'both' sein."));
        }

        if (!TryParseFormat(input.Format, out _))
        {
            return new(
                direction,
                McpScopeType.All,
                DefaultMaxResponseBytes,
                McpToolResults.InvalidArgument(
                    $"Ungueltiger Wert fuer 'format': '{input.Format}'.",
                    "format muss 'ascii' oder 'mermaid' sein (Default: 'ascii').",
                    "$.format"));
        }

        var scope = FindSymbolTool.ValidateScopeType(input.ScopeType);
        if (scope.Error is not null)
        {
            return new(direction, default, DefaultMaxResponseBytes, scope.Error);
        }

        var budgetError = ValidateMaxResponseBytes(input.MaxResponseBytes);
        return budgetError is not null
            ? new(direction, scope.ScopeType, DefaultMaxResponseBytes, budgetError)
            : new(
                direction,
                scope.ScopeType,
                input.MaxResponseBytes <= 0 ? DefaultMaxResponseBytes : input.MaxResponseBytes);
    }

    private static CallToolResult? ValidateMaxResponseBytes(int value) =>
        value > McpResponseBudgetLimits.MaxBytes
            ? McpToolResults.InvalidArgument(
                $"maxResponseBytes darf höchstens {McpResponseBudgetLimits.MaxBytes} sein.",
                "maxResponseBytes auf höchstens 65536 setzen.",
                "$.maxResponseBytes")
            : value > 0 && value < McpResponseBudgetLimits.MinimumStructuredBytes
                ? McpToolResults.InvalidArgument(
                    $"maxResponseBytes muss mindestens {McpResponseBudgetLimits.MinimumStructuredBytes} Bytes betragen.",
                    "maxResponseBytes weglassen oder mindestens 512 setzen.",
                    "$.maxResponseBytes")
                : null;

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

    internal static string RenderTree(
        MetricsTreeNode root,
        string? format,
        int topN,
        bool includeHandoffMetadata = true)
    {
        var rendered = TryParseFormat(format, out var parsedFormat) && parsedFormat == MermaidFormat
            ? CallTreeMermaidRenderer.Render(root, topN)
            : MetricsTreeRenderer.Render(root, topN, sortDescending: false);
        return includeHandoffMetadata
            ? rendered
            : Regex.Replace(rendered, @"\s+\[handoff=(?:true|false);[^\]]*\]", string.Empty);
    }

    internal static string RenderGraph(CallGraphPayload graph, string? format) =>
        TryParseFormat(format, out var parsedFormat) && parsedFormat == MermaidFormat
            ? CallTreeMermaidRenderer.Render(graph, 0)
            : CallGraphTextRenderer.RenderAscii(graph);

    internal static bool TryParseFormat(string? value, out string format)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "ascii", StringComparison.OrdinalIgnoreCase))
        {
            format = "ascii";
            return true;
        }

        if (string.Equals(value, MermaidFormat, StringComparison.OrdinalIgnoreCase))
        {
            format = MermaidFormat;
            return true;
        }

        format = string.Empty;
        return false;
    }

    internal static bool HasTreeOverflow(MetricsTreeNode root, int topN) =>
        root.Children.Count > topN || root.Children.Any(child => HasTreeOverflow(child, topN));

}
