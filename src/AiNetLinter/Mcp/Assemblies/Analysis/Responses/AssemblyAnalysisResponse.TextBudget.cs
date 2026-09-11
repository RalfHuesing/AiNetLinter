#nullable enable

using System;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Responses;

internal static partial class AssemblyAnalysisResponse
{
    internal static string TrimUtf8(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value) || maxBytes <= 0) return string.Empty;
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;

        const string ellipsis = "…";
        var ellipsisBytes = Encoding.UTF8.GetByteCount(ellipsis);
        var targetBytes = maxBytes - ellipsisBytes;

        if (targetBytes < 0) return maxBytes >= 1 ? "." : string.Empty;

        var limit = Math.Min(value.Length, targetBytes);
        while (limit > 0 && Encoding.UTF8.GetByteCount(value[..limit]) > targetBytes) limit--;
        if (limit > 0 && char.IsHighSurrogate(value[limit - 1])) limit--;

        return value[..limit] + ellipsis;
    }

    internal static string TrimTextPreservingNavigation(string value, int maxBytes)
    {
        const string marker = "## Navigation";
        var markerIndex = value.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return TrimUtf8(value, maxBytes);

        var navigation = value[markerIndex..];
        var navigationBytes = Encoding.UTF8.GetByteCount(navigation);
        if (navigationBytes >= maxBytes) return TrimUtf8(value, maxBytes);

        var prefixBudget = maxBytes - navigationBytes;
        var prefix = TrimUtf8(value[..markerIndex].TrimEnd(), prefixBudget).TrimEnd();
        return string.IsNullOrEmpty(prefix) ? navigation : prefix + "\n\n" + navigation;
    }
}
