#nullable enable

using System;
using System.Collections.Generic;

namespace AiNetLinter.Mcp;

/// <summary>
/// Einheitliche Hilfsmethoden zur Extraktion und Bereinigung von MCP-Batch-Argumenten.
/// </summary>
internal static class McpBatchArguments
{
    /// <summary>
    /// Bereinigt ein optionales Batch-Array zu einer deduplizierten Liste getrimmter, nicht-leerer Strings.
    /// </summary>
    internal static List<string> Normalize(string[]? values, StringComparer? comparer = null)
    {
        var list = new List<string>();
        if (values is null) return list;

        var effectiveComparer = comparer ?? StringComparer.Ordinal;

        foreach (var item in values)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            var trimmed = item.Trim();
            if (!list.Contains(trimmed, effectiveComparer))
            {
                list.Add(trimmed);
            }
        }

        return list;
    }
}
