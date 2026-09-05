#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Tools.Common;

/// <summary>
/// Zentrale Konsolidierungsklasse für LLM-/Agenten-Eingaben über alle MCP-Tools hinweg.
/// Bereinigt Markdown-Backticks (`...`), Quotes ("...", '...'), Methodenklammern,
/// Generics und normalisiert absolute Pfade tolerant in relative Workspace-Pfade.
/// </summary>
internal static class McpInputNormalizer
{
    internal static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Entfernt umschließende Markdown-Code-Backticks (`` `...` ``) oder Anführungszeichen ("...", '...'),
    /// die LLMs häufig aus Prompts oder Markdown-Kontexten wörtlich übernehmen.
    /// </summary>
    internal static string StripEnclosingQuotesAndBackticks(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        if ((trimmed.StartsWith('`') && trimmed.EndsWith('`') && trimmed.Length >= 2) ||
            (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2) ||
            (trimmed.StartsWith('\'') && trimmed.EndsWith('\'') && trimmed.Length >= 2))
        {
            return trimmed[1..^1].Trim();
        }

        return trimmed;
    }

    /// <summary>
    /// Normalisiert einen Symbol-Identifikator für die Symbolsuche oder Symbolauflösung:
    /// entfernt Backticks/Quotes, Methodenklammern `()` und Generics `<...>`.
    /// </summary>
    internal static string NormalizeSymbolIdentifier(string? rawIdentifier)
    {
        if (string.IsNullOrWhiteSpace(rawIdentifier)) return string.Empty;
        var cleaned = StripEnclosingQuotesAndBackticks(rawIdentifier);

        var parenMatch = Regex.Match(cleaned, @"^(.+?)\s*\(\s*\)$");
        if (parenMatch.Success)
        {
            cleaned = parenMatch.Groups[1].Value.TrimEnd();
        }

        var genericMatch = Regex.Match(cleaned, @"^(.+?)\s*<.*?>$");
        if (genericMatch.Success)
        {
            cleaned = genericMatch.Groups[1].Value.TrimEnd();
        }

        return cleaned;
    }

    /// <summary>
    /// Normalisiert einen Pfad- oder Scope-Parameter: entfernt Backticks/Quotes, vereinheitlicht
    /// Trennzeichen und wandelt absolute Pfade innerhalb von <paramref name="rootPath"/> tolerant
    /// in den entsprechenden relativen Pfad um.
    /// </summary>
    internal static string NormalizePathOrScope(string? rawPath, string? rootPath = null)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return ".";
        var cleaned = StripEnclosingQuotesAndBackticks(rawPath);
        if (string.IsNullOrWhiteSpace(cleaned)) return ".";

        if (Path.IsPathRooted(cleaned) && !string.IsNullOrWhiteSpace(rootPath))
        {
            try
            {
                var fullCleaned = Path.GetFullPath(cleaned);
                var fullRoot = Path.GetFullPath(rootPath);
                if (IsWithinRoot(fullCleaned, fullRoot))
                {
                    var relative = Path.GetRelativePath(fullRoot, fullCleaned);
                    var normalizedRelative = PathNormalizer.NormalizeSeparators(relative).Trim('/');
                    return string.IsNullOrEmpty(normalizedRelative) ? "." : normalizedRelative;
                }
            }
            catch (Exception ignored)
            {
                _ = ignored;
                // Fallback bei ungültigen Pfaden
            }
        }

        var normalized = PathNormalizer.NormalizeSeparators(cleaned).Trim('/');
        return string.IsNullOrEmpty(normalized) ? "." : normalized;
    }

    /// <summary>
    /// Prüft, ob ein Zielpfad innerhalb des Wurzelverzeichnisses liegt.
    /// </summary>
    internal static bool IsWithinRoot(string path, string root)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = fullRoot + Path.DirectorySeparatorChar;
        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Versucht, ein für Plain-Suche erfolgloses Suchmuster in eine spezialisierte Regex zu promoten
    /// (Wildcards, Methoden-Klammern, Generics, gequotete Identifier).
    /// </summary>
    internal static bool TryBuildPromotedRegex(string pattern, out Regex? promotedRegex)
    {
        promotedRegex = null;

        var unquoted = StripEnclosingQuotesAndBackticks(pattern);
        var candidate = !string.IsNullOrWhiteSpace(unquoted) ? unquoted : pattern;

        if (TryBuildMethodParenthesesRegex(candidate, out promotedRegex)) return true;
        if (TryBuildGenericTypeRegex(candidate, out promotedRegex)) return true;
        if (TryBuildMetaOrWildcardRegex(candidate, out promotedRegex)) return true;
        if (candidate != pattern && TryBuildUnquotedRegex(pattern, out promotedRegex)) return true;

        return false;
    }

    private static bool TryBuildMetaOrWildcardRegex(string pattern, out Regex? regex)
    {
        regex = null;
        if (!RegexAutoDetector.HasRegexMetaCharacters(pattern)) return false;

        if (RegexAutoDetector.IsValidRegex(pattern, out regex, RegexTimeout))
        {
            return true;
        }

        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var wildcardRegexStr = RegexAutoDetector.ConvertWildcardToRegex(pattern);
            return RegexAutoDetector.IsValidRegex(wildcardRegexStr, out regex, RegexTimeout);
        }

        return false;
    }

    private static bool TryBuildMethodParenthesesRegex(string pattern, out Regex? regex)
    {
        regex = null;
        var trimmed = pattern.Trim();
        var parenMatch = Regex.Match(trimmed, @"^(.+?)\s*\(\s*\)$");
        if (!parenMatch.Success) return false;

        var target = parenMatch.Groups[1].Value.TrimEnd();
        if (string.IsNullOrWhiteSpace(target)) return false;

        var wordBoundary = char.IsLetterOrDigit(target[0]) || target[0] == '_' ? @"\b" : "";
        var regexPattern = $@"{wordBoundary}{Regex.Escape(target)}\s*\(";
        return RegexAutoDetector.IsValidRegex(regexPattern, out regex, RegexTimeout);
    }

    private static bool TryBuildGenericTypeRegex(string pattern, out Regex? regex)
    {
        regex = null;
        var trimmed = pattern.Trim();
        var genericMatch = Regex.Match(trimmed, @"^(.+?)\s*<.*?>$");
        if (!genericMatch.Success) return false;

        var typeName = genericMatch.Groups[1].Value.TrimEnd();
        if (string.IsNullOrWhiteSpace(typeName)) return false;

        var wordBoundary = char.IsLetterOrDigit(typeName[0]) || typeName[0] == '_' ? @"\b" : "";
        var regexPattern = $@"{wordBoundary}{Regex.Escape(typeName)}\s*<";
        return RegexAutoDetector.IsValidRegex(regexPattern, out regex, RegexTimeout);
    }

    private static bool TryBuildUnquotedRegex(string pattern, out Regex? regex)
    {
        regex = null;
        var trimmed = pattern.Trim();
        var unquoted = StripEnclosingQuotesAndBackticks(trimmed);
        if (string.IsNullOrWhiteSpace(unquoted) || unquoted == trimmed) return false;

        var wordBoundary = char.IsLetterOrDigit(unquoted[0]) || unquoted[0] == '_' ? @"\b" : "";
        var regexPattern = $@"{wordBoundary}{Regex.Escape(unquoted)}";
        return RegexAutoDetector.IsValidRegex(regexPattern, out regex, RegexTimeout);
    }
}
