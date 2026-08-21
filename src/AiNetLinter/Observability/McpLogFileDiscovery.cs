#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AiNetLinter.Observability;

internal static class McpLogFileDiscovery
{
    private const string FeedbackLogSuffix = ".feedback.jsonl";

    internal static IReadOnlyList<string> Discover(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            return IsFeedbackLog(inputPath) ? Array.Empty<string>() : new[] { Path.GetFullPath(inputPath) };
        }

        if (Directory.Exists(inputPath))
        {
            return Directory.GetFiles(inputPath, "*.jsonl", SearchOption.AllDirectories)
                .Where(path => !IsFeedbackLog(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (ContainsWildcard(inputPath))
        {
            return DiscoverGlobFiles(inputPath);
        }

        throw new FileNotFoundException($"Kein MCP-Call-Log gefunden: {inputPath}", inputPath);
    }

    private static IReadOnlyList<string> DiscoverGlobFiles(string inputPath)
    {
        var fullPattern = Path.GetFullPath(inputPath);
        var wildcardIndex = fullPattern.IndexOfAny(['*', '?']);
        var separatorIndex = fullPattern.LastIndexOfAny(['\\', '/'], wildcardIndex);
        var searchRoot = separatorIndex < 0
            ? Directory.GetCurrentDirectory()
            : fullPattern[..separatorIndex];

        if (!Directory.Exists(searchRoot))
        {
            throw new FileNotFoundException($"Kein Verzeichnis fuer den MCP-Log-Glob gefunden: {searchRoot}", searchRoot);
        }

        var matcher = new Regex(BuildGlobRegex(fullPattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return Directory.GetFiles(searchRoot, "*.jsonl", SearchOption.AllDirectories)
            .Where(path => !IsFeedbackLog(path) && matcher.IsMatch(Path.GetFullPath(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ContainsWildcard(string path) => path.IndexOfAny(['*', '?']) >= 0;

    private static bool IsFeedbackLog(string path) =>
        path.EndsWith(FeedbackLogSuffix, StringComparison.OrdinalIgnoreCase);

    private static string BuildGlobRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        foreach (var character in pattern)
        {
            switch (character)
            {
                case '*':
                    builder.Append(".*");
                    break;
                case '?':
                    builder.Append('.');
                    break;
                case '\\':
                case '/':
                    builder.Append("[\\\\/]");
                    break;
                default:
                    builder.Append(Regex.Escape(character.ToString()));
                    break;
            }
        }

        builder.Append('$');
        return builder.ToString();
    }
}
