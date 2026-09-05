#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;
using AiNetLinter.Mcp.Tools.Common;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal sealed class AssemblyFileFilter
{
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

        if (!RegexAutoDetector.TryCreateFilterRegex(pattern, out var regex, out var isNegated, out var errorMessage))
        {
            throw new ArgumentException($"{parameterName} muss ein gueltiger Glob- oder Regex-Filter sein: {errorMessage}", parameterName);
        }

        return regex != null ? new AssemblyFileFilter(regex, isNegated) : null;
    }

    internal bool IsMatch(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        var matched = _regex.IsMatch(normalized) || _regex.IsMatch(fileName);
        return _isNegated ? !matched : matched;
    }
}
