#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace AiNetLinter.Suppression;

public sealed class IgnoreSuppressionsFilter
{
    public static IgnoreSuppressionsFilter None { get; } = new(null);

    private readonly bool _ignoreAll;
    private readonly HashSet<string> _ignoredLanguages;

    public IgnoreSuppressionsFilter(IReadOnlyList<string>? rawLanguageTokens)
    {
        _ignoredLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rawLanguageTokens == null || rawLanguageTokens.Count == 0)
        {
            _ignoreAll = false;
            return;
        }

        foreach (var token in rawLanguageTokens)
        {
            if (string.IsNullOrWhiteSpace(token)) continue;
            var trimmed = token.Trim().ToLowerInvariant();
            if (trimmed == "c#") trimmed = "cs";
            _ignoredLanguages.Add(trimmed);
        }

        if (_ignoredLanguages.Contains("all"))
        {
            _ignoreAll = true;
        }
    }

    public bool IsActive => _ignoreAll || _ignoredLanguages.Count > 0;

    public IReadOnlyList<string> ActiveLanguages
    {
        get
        {
            if (!IsActive) return Array.Empty<string>();
            if (_ignoreAll) return new[] { "cs", "razor", "js", "css" };
            var list = new List<string>();
            foreach (var lang in new[] { "cs", "razor", "js", "css" })
            {
                if (_ignoredLanguages.Contains(lang)) list.Add(lang);
            }
            return list;
        }
    }

    public bool ShouldIgnoreSuppression(string languageKind)
    {
        if (!IsActive) return false;
        if (_ignoreAll) return true;
        var normalized = languageKind.Equals("c#", StringComparison.OrdinalIgnoreCase) ? "cs" : languageKind.ToLowerInvariant();
        return _ignoredLanguages.Contains(normalized);
    }

    public bool ShouldIgnoreSuppressionForFile(string filePath)
    {
        if (!IsActive) return false;
        if (_ignoreAll) return true;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var lang = ext switch
        {
            ".cs" => "cs",
            ".razor" => "razor",
            ".js" => "js",
            ".css" => "css",
            _ => "cs"
        };
        return ShouldIgnoreSuppression(lang);
    }
}
