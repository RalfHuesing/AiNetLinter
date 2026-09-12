#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// MCP-Tool <c>find_references</c>: loest einen Symbol-Identifikator (stabile
/// DocumentationCommentId, Datei:Zeile:Spalte oder qualifizierter/teil-qualifizierter Name) zu
/// genau einem Roslyn-<see cref="ISymbol"/> auf und liefert dessen Aufrufstellen ueber den
/// gemeinsamen strukturierten Traversal-Result-Typ. Deckt nur .cs-Dateien ab
/// (Roslyn-Symbolgraph). Optionaler <c>depth</c>-Parameter (Default 1, hard cap 3) loest
/// transitive Aufrufstellen ueber <see cref="CallGraphTraversal"/> auf; Text und
/// der gerenderte Text wird aus derselben aggregierten Trefferliste erzeugt.
/// </summary>
internal static partial class FindReferencesTool
{
    internal const int DefaultMaxResponseBytes = 24 * 1024;
    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, loest den Identifikator zu einem
    /// Symbol auf und liefert dessen Aufrufstellen als Text. Ein defensiver try/catch-Wrapper
    /// faengt unerwartete Roslyn-Exceptions ab und liefert einen strukturierten [ERROR]-Antwort
    /// statt eines Server-Crashs (Defensiv-Pfad).
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        string? symbolIdentifier,
        int maxResults,
        int depth,
        CancellationToken ct) =>
        await ExecuteAsync(
            state,
            new FindReferencesRequest(symbolIdentifier, maxResults, depth),
            ct).ConfigureAwait(false);

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        FindReferencesRequest request,
        CancellationToken ct)
    {
        var precondition = ValidateRequest(state, request, out var solution, out var symbolIdentifier);
        if (precondition is not null) return precondition;

        try
        {
            return await ExecuteResolvedAsync(state, request, solution!, symbolIdentifier!, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in find_references: {ex.Message}",
                context: symbolIdentifier);
        }
    }

    private static CallToolResult? ValidateRequest(
        ISolutionStateProvider state, FindReferencesRequest request, out Solution? solution, out string? symbolIdentifier)
    {
        solution = null; symbolIdentifier = null;
        if (!McpResponseBudgetLimits.IsPublicBudget(request.MaxResponseBytes)) return InvalidResponseBudget();
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();
        symbolIdentifier = request.EffectiveSymbolIdentifier;
        return string.IsNullOrEmpty(symbolIdentifier)
            ? McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument, "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.", hint: McpToolResults.SymbolIdentifierHint)
            : null;
    }

    private static async Task<CallToolResult> ExecuteResolvedAsync(
        ISolutionStateProvider state, FindReferencesRequest request, Solution solution, string symbolIdentifier, CancellationToken ct)
    {
        var scopeFilter = new McpScopeFilter(request.ScopeType, request.IncludeGenerated, request.ScopeClassifier ?? new McpScopeClassifier());
        var (symbol, error) = await ResolveSymbolAsync(solution, symbolIdentifier, ct, state.HandoffSymbolIdentity);
        if (error is not null) return error;
        var traversal = await CallGraphTraversal.ExpandAsync(new ReferenceTraversalRequest(solution, symbol!, request.Depth, Math.Max(1, request.MaxResults), ct, AssemblySymbolIdentity: state.HandoffSymbolIdentity, ScopeFilter: scopeFilter));
        traversal = traversal with { Scope = new FindSymbolScopeDto(McpScopeValues.ToWireValue(request.ScopeType), request.IncludeGenerated) };
        var formatted = ProjectResponseBudget(traversal, request.MaxResponseBytes, symbol!.ToDisplayString());
        return CombinedBytes(formatted) > request.MaxResponseBytes
            ? BudgetTooSmall(request.MaxResponseBytes)
            : McpToolResults.Text(formatted.Text, formatted.StructuredPayload);
    }

    private static TransitiveCallGraphFormatResult ProjectResponseBudget(
        ReferenceTraversalResult traversal,
        int maxResponseBytes,
        string symbolIdentifier)
    {
        var current = traversal;
        var formatted = Format(current, symbolIdentifier);
        while (CombinedBytes(formatted) > maxResponseBytes && current.CallSites.Count > 0)
        {
            var callSites = current.CallSites.Take(current.CallSites.Count - 1).ToList();
            current = current with
            {
                CallSites = callSites,
                Completeness = current.Completeness with
                {
                    ShownCallSiteCount = callSites.Count,
                    TruncatedByResponseBudget = true,
                },
            };
            formatted = Format(current, symbolIdentifier);
        }
        return formatted;
    }

    private static TransitiveCallGraphFormatResult Format(
        ReferenceTraversalResult result,
        string symbolIdentifier) =>
        TransitiveCallGraphFormatter.FormatResponse(
            result,
            result.Completeness.TotalCallSiteCount == 0
                ? $"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'"
                : null);

    private static int CombinedBytes(TransitiveCallGraphFormatResult formatted) =>
        Encoding.UTF8.GetByteCount(formatted.Text)
        + JsonSerializer.SerializeToUtf8Bytes(formatted.StructuredPayload, McpJsonOptions.Default).Length;

    private static CallToolResult InvalidResponseBudget() =>
        McpToolResults.InvalidArgument(
            $"maxResponseBytes muss zwischen {McpResponseBudgetLimits.MinimumStructuredBytes} und {McpResponseBudgetLimits.MaxBytes} Bytes liegen.",
            "maxResponseBytes weglassen oder einen Wert innerhalb dieses Bereichs setzen.",
            "$.maxResponseBytes");

    private static CallToolResult BudgetTooSmall(int budget) => McpToolResults.Error(
        LinterErrorCodes.ResponseBudgetTooSmall,
        $"maxResponseBytes={budget} ist zu klein für die vollständige minimale Referenzprojektion.",
        new McpErrorParameters(Hint: "maxResponseBytes erhöhen; Referenzen werden nur vollständig gekürzt.", FieldPath: "$.maxResponseBytes"));

    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (Mcp.Wire.McpResponseSize.From(result).TotalBytes <= maxResponseBytes) return result;
        return BudgetTooSmall(maxResponseBytes);
    }

    /// <summary>
    /// Loest <paramref name="identifier"/> ueber eine stabile DocumentationCommentId, eine
    /// Datei:Zeile:Spalte-Angabe oder einen qualifizierten/teil-qualifizierten Namen zu genau
    /// einem Symbol auf — gemeinsamer Einstiegspunkt fuer alle drei dokumentierten Formate. Reine
    /// Funktion (Solution rein, Symbol/Fehler raus) ohne Abhaengigkeit von
    /// <see cref="McpCodeGraphServer"/> — direkt unit-testbar. Normalisiert Accessor-Symbole
    /// (Property/Event) auf den zugrunde liegenden Owner, damit eine Position auf einem
    /// <c>get</c>/<c>set</c>/<c>add</c>/<c>remove</c>-Keyword konsistent dieselbe ID liefert
    /// wie eine Position auf dem Property-/Event-Namen.
    /// </summary>
    internal static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolAsync(
        Solution solution,
        string identifier,
        CancellationToken ct,
        AnalysisSymbolIdentity? assemblyIdentity = null)
    {
        var cleaned = McpInputNormalizer.StripEnclosingQuotesAndBackticks(identifier);
        var (symbol, error) = await ResolveSymbolCoreAsync(solution, cleaned, ct, assemblyIdentity);
        return (symbol.NormalizeToOwningMember(), error);
    }

    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolCoreAsync(
        Solution solution,
        string identifier,
        CancellationToken ct,
        AnalysisSymbolIdentity? assemblyIdentity)
    {
        var (stableSymbol, stableError) =
            await SymbolIdentifierResolver.TryResolveByStableIdAsync(solution, identifier, ct, assemblyIdentity);
        if (stableError is not null) return (null, stableError);
        if (stableSymbol is not null) return (stableSymbol, null);

        if (SymbolIdentifierResolver.TryParsePosition(identifier, out var path, out var line, out var column))
        {
            return await ResolveByPositionAsync(solution, identifier, path, line, column, ct);
        }

        if (SymbolIdentifierResolver.TryParseLineOnlyPosition(identifier, out var linePath, out var lineOnly))
        {
            return await ResolveByLineAsync(solution, identifier, linePath, lineOnly, assemblyIdentity, ct);
        }

        return await ResolveByNameAsync(solution, identifier, assemblyIdentity, ct);
    }


}
