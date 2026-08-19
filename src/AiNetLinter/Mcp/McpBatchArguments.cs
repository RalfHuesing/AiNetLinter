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
    /// Kombiniert ein optionales Einzelelement und ein optionales Batch-Array zu einer deduplizierten Liste nicht-leerer Strings.
    /// </summary>
    internal static List<string> Collect(string? single, string[]? multiple, StringComparer? comparer = null)
    {
        var list = new List<string>();
        var effectiveComparer = comparer ?? StringComparer.Ordinal;

        if (!string.IsNullOrWhiteSpace(single))
        {
            list.Add(single.Trim());
        }

        if (multiple is null) return list;

        foreach (var item in multiple)
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
