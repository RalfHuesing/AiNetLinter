#nullable enable

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AiNetLinter.Configuration;

internal static class PathGlobMatcher
{
    internal static bool Matches(string input, string pattern)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(pattern)) return false;

        var normalizedInput = NormalizeSeparators(input);
        var normalizedPattern = NormalizeSeparators(pattern);
        var regexPattern = BuildRegex(normalizedPattern);
        return Regex.IsMatch(
            normalizedInput,
            regexPattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizeSeparators(string path) => path.Replace('\\', '/');

    private static string BuildRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (character == '*')
            {
                AppendStarPattern(builder, pattern, ref index);
                continue;
            }

            if (character == '{' && TryAppendBracePattern(builder, pattern, ref index))
            {
                continue;
            }

            builder.Append(character == '?' ? "[^/]" : Regex.Escape(character.ToString()));
        }

        builder.Append('$');
        return builder.ToString();
    }

    private static bool TryAppendBracePattern(StringBuilder builder, string pattern, ref int index)
    {
        var closingIndex = pattern.IndexOf('}', index + 1);
        if (closingIndex < 0) return false;

        var content = pattern.Substring(index + 1, closingIndex - index - 1);
        if (!content.Contains(',')) return false;

        var parts = content.Split(',');
        builder.Append("(?:");
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) builder.Append('|');
            builder.Append("(?:");
            var part = parts[i].Trim();
            for (var p = 0; p < part.Length; p++)
            {
                var ch = part[p];
                if (ch == '*')
                {
                    AppendStarPattern(builder, part, ref p);
                }
                else
                {
                    builder.Append(ch == '?' ? "[^/]" : Regex.Escape(ch.ToString()));
                }
            }
            builder.Append(')');
        }
        builder.Append(')');
        index = closingIndex;
        return true;
    }

    private static void AppendStarPattern(StringBuilder builder, string pattern, ref int index)
    {
        if (index + 1 < pattern.Length && pattern[index + 1] == '*')
        {
            index++;
            if (index + 1 < pattern.Length && pattern[index + 1] == '/')
            {
                index++;
                builder.Append("(?:.*/)?");
                return;
            }

            builder.Append(".*");
            return;
        }

        builder.Append("[^/]*");
    }
}
