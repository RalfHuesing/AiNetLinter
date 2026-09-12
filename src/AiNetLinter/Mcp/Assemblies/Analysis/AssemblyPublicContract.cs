#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

/// <summary>Projects internal assembly-session data onto the stable agent contract.</summary>
internal static partial class AssemblyPublicContract
{
    [GeneratedRegex("(?i)(?<=— `)(?:[A-Z]:[\\\\/]|\\\\\\\\|/)[^`]+(?=`)")]
    private static partial Regex BodyLocationPathRegex();

    internal static CallToolResult Project(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var content = result.Content
            .Select(block => block is TextContentBlock text
                ? new TextContentBlock { Text = SanitizeText(text.Text) }
                : block)
            .ToList();

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = content,
        };
    }

    private static string SanitizeText(string text)
    {
        var lines = text.Split('\n')
            .Where(line => !line.Contains("Pfade: decompiledProject", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.TrimStart().StartsWith("- Generation:", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.TrimStart().StartsWith("- GeneratedPath:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line
                .Replace("generatedPath=", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("generation=", string.Empty, StringComparison.OrdinalIgnoreCase));
        return BodyLocationPathRegex().Replace(string.Join('\n', lines), "<decompiled-source>");
    }

}
