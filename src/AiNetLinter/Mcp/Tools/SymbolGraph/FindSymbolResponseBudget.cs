#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>Projects find-symbol responses by complete symbol entries after navigation is present.</summary>
internal static class FindSymbolResponseBudget
{
    private const string BudgetReason = "maxResponseBytes";

    internal static CallToolResult Apply(CallToolResult result, int maxResponseBytes)
    {
        if (McpResponseSize.From(result).TotalBytes <= maxResponseBytes) return result;
        return McpToolResults.Error(
            LinterErrorCodes.ResponseBudgetTooSmall,
            $"maxResponseBytes={maxResponseBytes} ist zu klein für die fachliche Mindestprojektion.",
            new McpErrorParameters(
                Hint: "maxResponseBytes erhöhen; die Antwort wird nur an vollständigen Symbol-Entries gekürzt.",
                FieldPath: "$.maxResponseBytes",
                RequestedBytes: maxResponseBytes));
    }

    private static bool TryRemoveLastMatch(FindSymbolBudgetPayload payload, out FindSymbolBudgetPayload reduced)
    {
        for (var resultIndex = payload.Results.Count - 1; resultIndex >= 0; resultIndex--)
        {
            var pattern = payload.Results[resultIndex];
            if (pattern.Matches.Count == 0) continue;

            var patterns = payload.Results.ToArray();
            patterns[resultIndex] = pattern with
            {
                Matches = pattern.Matches.Take(pattern.Matches.Count - 1).ToList(),
                ReturnedCount = pattern.Matches.Count - 1,
                IsTruncated = true,
                TruncatedBy = AddReason(pattern.TruncatedBy),
            };
            reduced = payload with
            {
                Results = patterns,
                ReturnedCount = patterns.Sum(item => item.ReturnedCount),
                IsTruncated = true,
                TruncatedBy = AddReason(payload.TruncatedBy),
                Navigation = payload.Navigation?.WithResponseBudgetTruncation(),
            };
            return true;
        }

        reduced = payload;
        return false;
    }

    private static CallToolResult CreateResult(FindSymbolBudgetPayload payload, string originalText)
    {
        var text = payload.IsTruncated ? RenderText(payload) : originalText;
        return McpToolResults.Text(text, payload);
    }

    private static IReadOnlyList<string> AddReason(IReadOnlyList<string>? reasons) =>
        reasons is not null && reasons.Contains(BudgetReason, StringComparer.Ordinal)
            ? reasons
            : (reasons ?? []).Append(BudgetReason).ToList();

    private static string RenderText(FindSymbolBudgetPayload payload)
    {
        var lines = new List<string>();
        for (var index = 0; index < payload.Results.Count; index++)
        {
            if (index > 0) lines.Add("---");
            var pattern = payload.Results[index];
            lines.Add($"### Symbol-Suche: `{pattern.NamePattern}`");
            if (pattern.Matches.Count == 0)
            {
                lines.Add($"Keine Treffer fuer '{pattern.NamePattern}'");
                continue;
            }

            lines.AddRange(pattern.Matches.Select(FindSymbolTool.FormatEntry));
            if (pattern.IsTruncated)
            {
                lines.Add($"Treffer gesamt, {pattern.ReturnedCount} gezeigt (gekürzt durch {BudgetReason}).");
            }
        }

        if (payload.Navigation is not null)
        {
            lines.Add(string.Empty);
            lines.Add("## Navigation");
            lines.Add("- status: operation=`ok`, completeness=`truncated`");
            lines.Add("- next: `request_detail` — maxResponseBytes erhöhen oder das Suchmuster verfeinern.");
        }

        return string.Join("\n", lines);
    }

    private sealed record FindSymbolBudgetPayload(
        IReadOnlyList<FindSymbolPatternResultDto> Results,
        int TotalCount = 0,
        int ReturnedCount = 0,
        bool IsTruncated = false,
        IReadOnlyList<string>? TruncatedBy = null,
        FindSymbolScopeDto? Scope = null,
        FindSymbolNavigation? Navigation = null);

    private sealed record FindSymbolNavigation(
        int ContractVersion,
        McpNavigationTarget Target,
        McpNavigationSnapshot Snapshot,
        FindSymbolNavigationStatus Status,
        McpNavigationAnalysis Analysis,
        JsonObject? Scope,
        McpNavigationNext? Next,
        JsonObject? Handoff)
    {
        internal FindSymbolNavigation WithResponseBudgetTruncation() => this with
        {
            Status = Status with { Completeness = "truncated" },
            Next = new McpNavigationNext(
                "request_detail",
                "maxResponseBytes erhöhen oder das Suchmuster verfeinern."),
        };
    }

    private sealed record FindSymbolNavigationStatus(
        string Operation,
        string Completeness,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Code);
}
