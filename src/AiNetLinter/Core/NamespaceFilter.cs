#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AiNetLinter.Core;

/// <summary>
/// Hilfsklasse zum Filtern von Namespaces und Projekten anhand von Inklusion/Exklusion-Wildcards.
/// </summary>
internal static class NamespaceFilter
{
    /// <summary>
    /// Prüft, ob ein Namespace unter Berücksichtigung von Includes und Excludes erlaubt ist.
    /// </summary>
    public static bool IsNamespaceAllowed(
        string ns,
        IReadOnlyList<string> includes,
        IReadOnlyList<string> excludes)
    {
        if (includes.Count > 0 && !includes.Any(pattern => MatchesGlob(ns, pattern)))
        {
            return false;
        }

        if (excludes.Count > 0 && excludes.Any(pattern => MatchesGlob(ns, pattern)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Prüft, ob ein String (z.B. Projektname oder Namespace) einem Glob-Muster mit Wildcards (*) entspricht.
    /// </summary>
    public static bool MatchesGlob(string value, string pattern)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }
}
