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

        var reportText = FormatTextReport(result, args);
        var finalText = reportText;

        return McpToolResults.Text(finalText);
    }

    private static string FormatTextReport(DeadCodeScanResult result, FindDeadCodeArgs args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Dead-Code-Analyse: Kandidaten (Heuristik-Audit)");
        sb.AppendLine("Ergebnisart: candidate; deletionClaim=false. Statische Referenzsuche beweist keine Loeschbarkeit. Reflection, DI, Generatoren und dynamic koennen ausserhalb der Evidenz liegen.");
        sb.AppendLine();

        AppendResultDetails(sb, result, args);
        AppendSummary(sb, result, args);

        return sb.ToString().TrimEnd();
    }

    private static void AppendResultDetails(StringBuilder sb, DeadCodeScanResult result, FindDeadCodeArgs args)
    {
        if (result.Summary.DocumentsInScope == 0)
        {
            AppendEmptyScopeDetails(sb, args);
            return;
        }

        if (result.DeadSymbols.Count == 0)
        {
            AppendNoDeadCodeDetails(sb, result.Summary.DocumentsInScope, args.Mode);
            return;
        }

        AppendDeadSymbols(sb, result);
    }

    private static void AppendEmptyScopeDetails(StringBuilder sb, FindDeadCodeArgs args)
    {
        var scopeSuffix = string.IsNullOrWhiteSpace(args.ScopeFilter)
            ? ""
            : $" (Filter: '{args.ScopeFilter}')";
        sb.AppendLine($"Keine Symbole im Scope gescannt{scopeSuffix}.");
        sb.AppendLine("Der scopeFilter matcht keine Datei, oder der Scope besteht ausschliesslich aus Testprojekten (includeTests=false).");
        sb.AppendLine("includeTests=true setzen oder den scopeFilter (Projekt-Name oder Pfad-Substring) anpassen.");
    }

    private static void AppendNoDeadCodeDetails(StringBuilder sb, int documentsInScope, DeadCodeMode mode)
    {
        if (mode == DeadCodeMode.Locals)
        {
            sb.AppendLine($"Alle {documentsInScope} Zieldokumente im Scope wurden auf ungenutzte Locals und Felder geprueft; keine Compiler-Diagnose gefunden.");
        }
        else
        {
            sb.AppendLine("Kein unreferenzierter Code im angegebenen Scope gefunden.");
        }
    }

    private static void AppendDeadSymbols(StringBuilder sb, DeadCodeScanResult result)
    {
        sb.AppendLine($"## Kandidaten ({result.DeadSymbols.Count}{(result.IsTruncated ? " gezeigt" : "")})");
        sb.AppendLine();

        foreach (var sym in result.DeadSymbols)
        {
            sb.AppendLine($"- {sym.File}:{sym.Line}:{sym.Column} [{sym.Confidence.ToUpperInvariant()}] ({sym.Kind}, {sym.Accessibility}) - {sym.SymbolName} in '{sym.ContainerType}'");
            sb.AppendLine($"  Grund: {sym.Reason}");
            sb.AppendLine($"  Evidenzgrenze: {sym.EvidenceBoundary}");
            sb.AppendLine("  Countercheck: Reflection, DI, Generatoren, dynamic und externe Consumer pruefen.");
            if (sym.LimitsApplies.Count > 0)
            {
                sb.AppendLine($"  Limits: {string.Join(", ", sym.LimitsApplies)}");
            }
        }
    }

    private static void AppendSummary(StringBuilder sb, DeadCodeScanResult result, FindDeadCodeArgs args)
    {
        sb.AppendLine();
        sb.AppendLine("## Zusammenfassung");
        sb.AppendLine($"- Zieldokumente im Scope: {result.Summary.DocumentsInScope}");
        sb.AppendLine($"- Gescannt: {result.Summary.ScannedSymbols} Symbole");
        sb.AppendLine($"- Kandidaten: {result.Summary.TotalDead} ({result.Summary.High} high, {result.Summary.Low} low)");
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
    }
}
