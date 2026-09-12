#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Wire;

/// <summary>
/// Wendet die kanonischen, agentisch sichtbaren Texttransformationen auf jede Tool-Antwort an.
/// Weitere verlustfreie Transformationen werden als weiterer Eintrag in <see cref="TextFilters"/>
/// ergänzt.
/// </summary>
internal static class McpToolResponsePipeline
{
    private static readonly IReadOnlyList<Func<string, string>> TextFilters =
    [
        NormalizeLineEndings,
    ];

    internal static CallToolResult Apply(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = result.Content.Select(ApplyTextFilters).ToList(),
        };
    }

    private static ContentBlock ApplyTextFilters(ContentBlock block)
    {
        if (block is not TextContentBlock { Text: { } text }) return block;

        var filtered = text;
        foreach (var filter in TextFilters)
        {
            filtered = filter(filtered);
        }

        return ReferenceEquals(filtered, text)
            ? block
            : new TextContentBlock { Text = filtered };
    }

    private static string NormalizeLineEndings(string text)
    {
        if (!text.Contains('\r', StringComparison.Ordinal)) return text;

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
