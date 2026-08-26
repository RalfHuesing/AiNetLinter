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

            builder.Append(character == '?' ? "[^/]" : Regex.Escape(character.ToString()));
        }

        builder.Append('$');
        return builder.ToString();
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
