#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AiNetLinter.Core;

internal static class NamespaceFilter
{
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
