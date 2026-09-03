#nullable enable

using System;
using System.Linq;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Assemblies.Analysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Factories;

internal static class AssemblyAnalysisResponseUnknownArrays
{
    internal static bool TryTrim(JsonObject owner, string propertyName, JsonNode? value)
    {
        if (value is not JsonArray array || !IsUnknownArrayCandidate(propertyName, array)) return false;

        var originalCount = array.Count;
        if (array.Count <= 1) return TryTrimLongArrayValue(array, owner, propertyName, originalCount);

        array.RemoveAt(array.Count - 1);
        AssemblyAnalysisResponseEnvelope.MarkArrayTruncated(owner, propertyName, originalCount, array.Count);
        return true;
    }

    private static bool IsUnknownArrayCandidate(string propertyName, JsonArray array) =>
        propertyName is "parameters" or "attributes" or "genericParameters" or "constraints"
        && (array.Count > 1
            || array.Any(item => item is JsonValue value
                && value.TryGetValue<string>(out var text)
                && text.Length > 256));

    private static bool TryTrimLongArrayValue(
        JsonArray array,
        JsonObject owner,
        string propertyName,
        int originalCount)
    {
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonValue value
                || !value.TryGetValue<string>(out var text)
                || text.Length <= 256) continue;

            array[index] = AssemblyAnalysisResponse.TrimUtf8(text, 256);
            AssemblyAnalysisResponseEnvelope.MarkArrayTruncated(owner, propertyName, originalCount, array.Count);
            return true;
        }

        return false;
    }
}
