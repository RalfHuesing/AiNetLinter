#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Mcp;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal static class SearchPatternLegacyFormatter
{
    internal static string Format(SearchPatternScanResult result)
    {
        var completeness = result.Payload.Completeness;
        if (completeness.TotalMatchedLineCount == 0)
        {
            var text = "0 Treffer fuer das angegebene Pattern.";
            if (result.IsRegex == false && !string.IsNullOrEmpty(result.Pattern) && (result.Pattern.Contains('*') || result.Pattern.Contains('?')))
            {
                text += "\nHinweis: Das Pattern enthaelt Wildcard-Zeichen ('*' oder '?'), aber isRegex=false. Fuer Wildcards/Regex bitte isRegex: true setzen.";
            }

            return AppendHints(text, result, Array.Empty<string>());
        }

        var hitLines = result.Payload.Matches
            .Select(match => $"{match.FilePath}:{match.Line}: {match.LineText.TrimEnd()}")
            .ToList();
        var formatted = FormatHitLines(result, hitLines);
        if (result.IsRegexAutoPromoted)
        {
            formatted = "[Auto-Detect: Suchmuster automatisch als Regex ausgefuehrt]\n" + formatted;
        }

        return AppendHints(formatted, result, hitLines);
    }

    private static string FormatHitLines(
        SearchPatternScanResult result,
        IReadOnlyList<string> hitLines)
    {
        var reasons = result.Payload.Completeness.TruncatedBy;
        var onlyMaxResults = reasons.Contains("maxResults")
            && !reasons.Contains("maxFiles")
            && !reasons.Contains("maxResponseBytes");
        if (!onlyMaxResults) return string.Join("\n", hitLines);
        return McpTruncation.TruncateLines(hitLines, result.TotalMatchedLineCount, result.MaxResults);
    }

    private static string AppendHints(
        string text,
        SearchPatternScanResult result,
        IReadOnlyList<string> hitLines)
    {
        var completeness = result.Payload.Completeness;
        var builder = new System.Text.StringBuilder(text);
        if (completeness.TruncatedBy.Contains("maxResults")
            && !IsOnlyMaxResults(completeness.TruncatedBy))
        {
            builder.Append($"\n[{result.TotalMatchedLineCount} Treffer gesamt, {hitLines.Count} gezeigt — ");
            builder.Append("Pattern verfeinern oder maxResults erhoehen]");
        }

        AppendBudgetHint(builder, completeness);
        AppendScanStateHint(builder, completeness);
        return builder.ToString();
    }

    private static void AppendBudgetHint(
        System.Text.StringBuilder builder,
        SearchPatternCompleteness completeness)
    {
        if (completeness.TruncatedBy.Contains("maxFiles"))
        {
            builder.Append($"\n[{completeness.MatchedFileCount} Dateien mit Textfund gesamt, ");
            builder.Append($"{completeness.ShownMatchedFileCount} gezeigt — maxFiles erhoehen oder Scope verfeinern]");
        }

        if (completeness.TruncatedBy.Contains("maxResponseBytes"))
        {
            builder.Append("\n[Antwort wegen maxResponseBytes begrenzt — Kontextbreite oder Budget anpassen]");
        }
    }

    private static void AppendScanStateHint(
        System.Text.StringBuilder builder,
        SearchPatternCompleteness completeness)
    {
        if (completeness.TruncatedBy.Contains("regexTimeout"))
        {
            builder.Append("\n[Regex-Timeout — Pattern vereinfachen oder Scope verfeinern]");
        }

        if (completeness.CancellationRequested)
        {
            builder.Append("\n[Suche abgebrochen — Ergebnis ist unvollstaendig]");
        }

        if (completeness.EnumerationErrorCount > 0)
        {
            builder.Append($"\n[{completeness.EnumerationErrorCount} Dateisystembereiche konnten nicht gelesen werden — Ergebnis ist unvollstaendig]");
        }

        if (completeness.SkippedBinaryFileCount > 0 || completeness.SkippedUnreadableFileCount > 0)
        {
            builder.Append($"\n[{completeness.SkippedBinaryFileCount} binaere und ");
            builder.Append($"{completeness.SkippedUnreadableFileCount} unlesbare Dateien uebersprungen]");
        }
    }

    private static bool IsOnlyMaxResults(IReadOnlyList<string> reasons) =>
        reasons.Contains("maxResults")
        && !reasons.Contains("maxFiles")
        && !reasons.Contains("maxResponseBytes");
}
