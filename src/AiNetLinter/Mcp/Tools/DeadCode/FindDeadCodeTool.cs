#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.DeadCode;

/// <summary>
/// Rohe Tool-Argumente fuer find_dead_code.
/// </summary>
public sealed record FindDeadCodeToolArgs(
    string? Accessibility = "private_internal",
    string? Confidence = "both",
    string? Kind = "all",
    string? ScopeFilter = null,
    bool IncludeTests = false,
    string? Mode = "members",
    int MaxResults = FindDeadCodeScanner.DefaultMaxResults);

/// <summary>
/// MCP-Tool find_dead_code: Scannt die geladene Solution nach unreferenziertem Code.
/// </summary>
internal static class FindDeadCodeTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        FindDeadCodeToolArgs rawArgs,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();
        if (!FindDeadCodeArgs.IsKnownAccessibility(rawArgs.Accessibility)) return McpToolResults.InvalidArgument("Unbekannter accessibility-Wert.", fieldPath: "$.accessibility");
        if (!FindDeadCodeArgs.IsKnownConfidence(rawArgs.Confidence)) return McpToolResults.InvalidArgument("Unbekannter confidence-Wert.", fieldPath: "$.confidence");
        if (!FindDeadCodeArgs.IsKnownKind(rawArgs.Kind)) return McpToolResults.InvalidArgument("Unbekannter kind-Wert.", fieldPath: "$.kind");
        if (!FindDeadCodeArgs.IsKnownMode(rawArgs.Mode)) return McpToolResults.InvalidArgument("Unbekannter mode-Wert.", fieldPath: "$.mode");
        if (rawArgs.MaxResults < 1) return McpToolResults.InvalidArgument("maxResults muss mindestens 1 sein.", fieldPath: "$.maxResults");

        var accessibility = FindDeadCodeArgs.ParseAccessibility(rawArgs.Accessibility);
        var confidence = FindDeadCodeArgs.ParseConfidence(rawArgs.Confidence);
        var kind = FindDeadCodeArgs.ParseKind(rawArgs.Kind);
        var mode = FindDeadCodeArgs.ParseMode(rawArgs.Mode);
        var maxResults = rawArgs.MaxResults;

        var args = new FindDeadCodeArgs(
            Accessibility: accessibility,
            Confidence: confidence,
            Kind: kind,
            ScopeFilter: string.IsNullOrWhiteSpace(rawArgs.ScopeFilter) ? null : rawArgs.ScopeFilter,
            IncludeTests: rawArgs.IncludeTests,
            Mode: mode,
            MaxResults: maxResults);

        DeadCodeScanResult result;
        try
        {
            result = await Task.Run(() => FindDeadCodeScanner.ScanAsync(solution, args, ct), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.Error(
                LinterErrorCodes.AnalysisFailed,
                "Unerwarteter Fehler beim Dead-Code-Scan.",
                context: ex.Message,
                hint: "Einmal erneut versuchen.");
        }

        var reportText = FormatTextReport(result);
        var finalText = reportText;

        return McpToolResults.Text(finalText);
    }

    private static string FormatTextReport(DeadCodeScanResult result)
    {
        if (result.Summary.DocumentsInScope == 0) return "dead_code: no analyzable documents";
        if (result.DeadSymbols.Count == 0) return "dead_code: 0 candidates";

        var sb = new StringBuilder();
        sb.Append($"dead_code: candidates={result.Summary.TotalDead}");
        if (result.IsTruncated)
        {
            sb.Append($"; shown={result.DeadSymbols.Count}; truncated=maxResults");
        }
        sb.AppendLine();
        AppendDeadSymbols(sb, result);

        return sb.ToString().TrimEnd();
    }

    private static void AppendDeadSymbols(StringBuilder sb, DeadCodeScanResult result)
    {
        foreach (var sym in result.DeadSymbols)
        {
            sb.Append($"- {sym.File}:{sym.Line}:{sym.Column} [{sym.Confidence.ToUpperInvariant()}] {sym.Kind} {sym.Accessibility} {sym.SymbolName} in {sym.ContainerType}; {sym.Reason}");
            if (sym.LimitsApplies.Count > 0)
            {
                sb.Append($"; limits={string.Join(",", sym.LimitsApplies)}");
            }
            sb.AppendLine();
        }
    }
}
