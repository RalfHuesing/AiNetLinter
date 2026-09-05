#nullable enable

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AiNetLinter.Mcp.Tools.Common;

/// <summary>
/// Zentrale Hilfsklasse zur Erkennung und Validierung von Regex-Mustern für alle MCP-Tools.
/// Bietet einheitliches Autodetect-Verhalten, Erkennung eindeutiger Regex-Syntax, sichere
/// Regex-Erstellung mit Timeout sowie Konvertierung von Wildcard-Globs in Regex.
/// </summary>
internal static class RegexAutoDetector
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(100);

    internal static readonly RegexOptions DefaultOptions =
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;

    /// <summary>
    /// Prüft, ob ein Suchmuster eindeutige Regex-Syntax enthält, die in normalem C#-Code
    /// praktisch nie als wörtlicher Substring gesucht wird (z. B. \s, \w, \d, \b, ^..., ...$, (?i)).
    /// </summary>
    internal static bool IsLikelyRegex(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        var trimmed = pattern.Trim();

        // Zeilenanker am Rand (z. B. "^public", "void$")
        if ((trimmed.StartsWith('^') && trimmed.Length > 1) || (trimmed.EndsWith('$') && trimmed.Length > 1))
        {
            if (IsValidRegex(trimmed, out _)) return true;
        }

        // Typische Regex Escape-Sequenzen (\s, \d, \w, \b, \B, \S, \D, \W, \p{...}, \x..)
        if (ContainsRegexEscapeSequence(trimmed))
        {
            if (IsValidRegex(trimmed, out _)) return true;
        }

        // Regex-Gruppen-Syntax ((?i), (?:, (?<, (?=, etc.)
        if (trimmed.Contains("(?") && IsValidRegex(trimmed, out _))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Prüft, ob ein Suchmuster Metazeichen enthält, die als Regex interpretiert werden könnten
    /// (z. B. .*, .+, [A-Z], |, etc.), aber auch in normalem Code vorkommen könnten (wie int[] oder a || b).
    /// </summary>
    internal static bool HasRegexMetaCharacters(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        return pattern.Contains(".*") || pattern.Contains(".+")
            || pattern.Contains('|') || (pattern.Contains('[') && pattern.Contains(']'))
            || pattern.Contains('*') || pattern.Contains('?');
    }

    /// <summary>
    /// Versucht, ein Regex-Objekt mit Standard-Optionen und Timeout zu instanziieren.
    /// </summary>
    internal static bool IsValidRegex(string pattern, out Regex? regex, TimeSpan? timeout = null)
    {
        try
        {
            regex = new Regex(pattern, DefaultOptions, timeout ?? DefaultTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            regex = null;
            return false;
        }
    }

    /// <summary>
    /// Prüft, ob ein String eine typische Regex-Escape-Sequenz enthält (\s, \w, \d, etc.).
    /// </summary>
    internal static bool ContainsRegexEscapeSequence(string text)
    {
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '\\')
            {
                var next = text[i + 1];
                if (next is 's' or 'S' or 'd' or 'D' or 'w' or 'W' or 'b' or 'B' or 'p' or 'P' or 'x')
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Wandelt ein Wildcard-Pattern mit * und ? in ein gültiges Regex-Pattern um,
    /// falls der Aufrufer Glob-Syntax verwendet hat.
    /// Unterstützt optional Verankerung (^ und $) und Verzeichnis-Trennzeichen.
    /// </summary>
    internal static string ConvertWildcardToRegex(string wildcardPattern, bool anchored = false)
    {
        var sb = new StringBuilder();
        if (anchored) sb.Append('^');
        foreach (var c in wildcardPattern)
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
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
            }
        }

        if (anchored) sb.Append('$');
        return sb.ToString();
    }

    /// <summary>
    /// Erstellt tolerant ein Regex-Filterobjekt aus einem Pfad-/Dateifilter (unterstützt Wildcard-Globs wie '*.cs',
    /// '!*Designer*', 'src/*.cs' sowie reguläre Ausdrücke).
    /// Gibt true zurück, wenn erfolgreich (regex ist null, wenn Filter null/whitespace ist).
    /// </summary>
    internal static bool TryCreateFilterRegex(
        string? filter,
        out Regex? regex,
        out bool isNegated,
        out string? errorMessage)
    {
        regex = null;
        isNegated = false;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(filter)) return true;

        var trimmed = filter.Trim();
        if (trimmed.StartsWith('!'))
        {
            isNegated = true;
            trimmed = trimmed[1..].Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return true;
        }

        // Wenn es wie ein typischer Glob aussieht (* oder ? und keine eindeutige Regex-Syntax)
        var isGlob = (trimmed.Contains('*') || trimmed.Contains('?')) && !IsLikelyRegex(trimmed);
        if (isGlob)
        {
            var converted = ConvertWildcardToRegex(trimmed, anchored: true);
            if (IsValidRegex(converted, out regex, DefaultTimeout))
            {
                return true;
            }
        }

        // Als regulären Ausdruck versuchen
        if (IsValidRegex(trimmed, out regex, DefaultTimeout))
        {
            return true;
        }

        errorMessage = $"Ungueltiges Filter-Muster (weder als Glob noch als Regex parsbar): '{filter}'";
        return false;
    }
}
