#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal sealed class AssemblyFileFilter
{
    private const string RegexSpecialChars = ".+^$(){}|[]";

    private static readonly RegexOptions FilterRegexOptions =
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;
    private static readonly TimeSpan FilterTimeout = TimeSpan.FromMilliseconds(100);

    private readonly Regex _regex;
    private readonly bool _isNegated;

    private AssemblyFileFilter(Regex regex, bool isNegated)
    {
        _regex = regex;
        _isNegated = isNegated;
    }

    internal static AssemblyFileFilter? Create(string? pattern, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return null;

        var trimmed = pattern.Trim();
        var isNegated = false;
        if (trimmed.StartsWith('!'))
        {
            isNegated = true;
            trimmed = trimmed[1..].Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return null;
        }

        if (IsGlobPattern(trimmed))
        {
            var regexPattern = ConvertGlobToRegex(trimmed);
            try
            {
                var regex = new Regex(regexPattern, FilterRegexOptions, FilterTimeout);
                return new AssemblyFileFilter(regex, isNegated);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException($"{parameterName} ungueltiger Glob-Filter: {exception.Message}", parameterName, exception);
            }
        }

        try
        {
            var regex = new Regex(trimmed, FilterRegexOptions, FilterTimeout);
            return new AssemblyFileFilter(regex, isNegated);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException($"{parameterName} muss ein gueltiger Glob- oder Regex-Filter sein: {exception.Message}", parameterName, exception);
        }
    }

    internal bool IsMatch(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        var matched = _regex.IsMatch(normalized) || _regex.IsMatch(fileName);
        return _isNegated ? !matched : matched;
    }

    private static bool IsGlobPattern(string text)
    {
        if (text.Contains("(?") || text.Contains(@"\b") || text.Contains(@"\d") || text.Contains(@"\w") || text.Contains(@"\s") || text.StartsWith('^') || text.EndsWith('$'))
        {
            return false;
        }

        return text.Contains('*') || text.Contains('?');
    }

    internal static string ConvertGlobToRegex(string glob)
    {
        var sb = new StringBuilder("^");
        foreach (var c in glob)
        {
            AppendGlobChar(sb, c);
        }
        sb.Append('$');
        return sb.ToString();
    }

    private static void AppendGlobChar(StringBuilder sb, char c)
    {
        if (c == '*')
        {
            sb.Append(".*");
        }
        else if (c == '?')
        {
            sb.Append('.');
        }
        else if (c is '/' or '\\')
        {
            sb.Append("[/\\\\]");
        }
        else if (RegexSpecialChars.Contains(c))
        {
            sb.Append('\\').Append(c);
        }
        else
        {
            sb.Append(c);
        }
    }
}
