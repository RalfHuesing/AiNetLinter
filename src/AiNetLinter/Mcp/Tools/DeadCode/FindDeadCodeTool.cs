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

        var accessibility = FindDeadCodeArgs.ParseAccessibility(rawArgs.Accessibility);
        var confidence = FindDeadCodeArgs.ParseConfidence(rawArgs.Confidence);
        var kind = FindDeadCodeArgs.ParseKind(rawArgs.Kind);
        var mode = FindDeadCodeArgs.ParseMode(rawArgs.Mode);
        var maxResults = Math.Max(1, rawArgs.MaxResults);

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

        var reportText = FormatTextReport(result, args);
        var finalText = result.IsTruncated ? reportText : McpSufficiencyHints.Append(reportText);

        return McpToolResults.Text(finalText, new
        {
            DeadSymbols = result.DeadSymbols,
            Summary = result.Summary,
            Limits = result.Limits,
            RecommendedNextAction = result.RecommendedNextAction,
            IsTruncated = result.IsTruncated
        });
    }

    private static string FormatTextReport(DeadCodeScanResult result, FindDeadCodeArgs args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Dead-Code-Analyse (Heuristik-Audit)");
        sb.AppendLine("Hinweis: Statische Dead-Code-Erkennung kann dynamische Bindungen (Reflection, DI, Serializer, Routing) nicht vollstaendig abbilden. Siehe 'limits' fuer Details.");
        sb.AppendLine();

        if (result.DeadSymbols.Count == 0)
        {
            sb.AppendLine("Kein unreferenzierter Code im angegebenen Scope gefunden.");
        }
        else
        {
            sb.AppendLine($"## Gefundene tote Symbole ({result.DeadSymbols.Count}{(result.IsTruncated ? " gezeigt" : "")})");
            sb.AppendLine();

            foreach (var sym in result.DeadSymbols)
            {
                sb.AppendLine($"- {sym.File}:{sym.Line}:{sym.Column} [{sym.Confidence.ToUpperInvariant()}] ({sym.Kind}, {sym.Accessibility}) - {sym.SymbolName} in '{sym.ContainerType}'");
                sb.AppendLine($"  Grund: {sym.Reason}");
                if (sym.LimitsApplies.Count > 0)
                {
                    sb.AppendLine($"  Limits: {string.Join(", ", sym.LimitsApplies)}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Zusammenfassung");
        sb.AppendLine($"- Gescannt: {result.Summary.ScannedSymbols} Symbole");
        sb.AppendLine($"- Toter Code: {result.Summary.TotalDead} ({result.Summary.High} high, {result.Summary.Low} low)");
        if (result.Summary.ByKind.Count > 0)
        {
            var kinds = string.Join(", ", result.Summary.ByKind.Select(kv => $"{kv.Key}: {kv.Value}"));
            sb.AppendLine($"- Nach Art: {kinds}");
        }
        sb.AppendLine($"- Empfohlene Aktion: {result.RecommendedNextAction.Action} ({result.RecommendedNextAction.Reason})");

        if (result.IsTruncated)
        {
            sb.AppendLine();
            sb.AppendLine($"[HINWEIS]: Ergebnis wurde auf {args.MaxResults} Eintraege gekappt — maxResults erhoehen oder scopeFilter verfeinern.");
        }

        return sb.ToString().TrimEnd();
    }
}
