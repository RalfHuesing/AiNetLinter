#nullable enable

using System;
using System.Linq;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Factories;

internal static class AssemblyAnalysisSearchEnvelope
{
    internal static void UpdateKnownContinuation(
        JsonObject obj,
        (string CollectionName, bool Truncated, int Returned, int Total, int Offset) update)
    {
        if (update.CollectionName == "directories") return;
        if (!update.Truncated)
        {
            if (obj["continuationToken"] is not null) obj["continuationToken"] = null;
            return;
        }

        AiNetLinter.Mcp.Assemblies.Analysis.AssemblyAnalysisResponseEnvelope.AddReason(obj, "responseBudget");
        // A search maxFiles truncation is a scope limit, not another page.
        obj["continuationToken"] = update.Returned < update.Total || obj["searchKind"] is null
            ? AssemblyPaging.CreateToken(Math.Max(0, update.Offset) + update.Returned)
            : null;
    }

    internal static void Update(JsonObject obj, bool truncated)
    {
        if (obj["searchKind"] is null) return;

        if (truncated)
        {
            obj["status"] = "truncated";
            obj["detailHint"] = "Suchergebnisse wurden wegen des Antwortbudgets gekürzt; maxResponseBytes erhöhen oder die Suche mit passendem maxResults/maxFiles erneut anfordern.";
            if (obj["completeness"] is JsonValue completeness
                && completeness.TryGetValue<string>(out var label)
                && label == "complete") obj["completeness"] = "truncated";
        }

        if (obj["results"] is not JsonArray results || obj["returnedFileCount"] is null) return;
        obj["returnedFileCount"] = results
            .OfType<JsonObject>()
            .Select(item => item["filePath"] is JsonValue path
                && path.TryGetValue<string>(out var value) ? value : null)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }
}
