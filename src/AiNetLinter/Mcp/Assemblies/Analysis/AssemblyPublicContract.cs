#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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

    [GeneratedRegex("(?i)(?:[A-Z]:[\\\\/]|\\\\\\\\|/)(?:[^\\\\/\\s`]+[\\\\/])+[^\\\\/\\s`]+")]
    private static partial Regex AbsolutePathRegex();

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
            .Select(line => SanitizeLine(line
                .Replace("generatedPath=", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("generation=", string.Empty, StringComparison.OrdinalIgnoreCase)));
        return string.Join('\n', lines);
    }

    private static string SanitizeLine(string line)
    {
        if (line.TrimStart().StartsWith("decompileRoot:", StringComparison.Ordinal)) return line;
        var withoutBodyLocation = BodyLocationPathRegex().Replace(line, "<decompiled-source>");
        return AbsolutePathRegex().Replace(withoutBodyLocation, match => Path.GetFileName(match.Value));
    }

}
