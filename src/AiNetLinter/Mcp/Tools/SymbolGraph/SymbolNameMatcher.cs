#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Zentraler Name-Matcher fuer Symbol-Suchen (unterstuetzt Substrings, Wildcards *, ?,
/// klammerbereinigte Methodennamen und punktseparierte Typ-/Member-Pfade).
/// </summary>
internal static class SymbolNameMatcher
{
    private const int MinWordLengthForSuggestions = 4;
    private const int MaxSuggestions = 5;

    internal static string CleanPattern(string rawPattern)
    {
        var trimmed = rawPattern.Trim();
        if (trimmed.EndsWith("()", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^2].Trim();
        }

        return trimmed;
    }

    internal static Func<string, bool> CreateDeclarationNameFilter(string pattern)
    {
        var clean = CleanPattern(pattern);
        if (clean.Contains('.'))
        {
            var parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var lastPart = parts[^1];
                var secondLastPart = parts.Length > 1 ? parts[^2] : null;

                var lastFilter = CreatePredicateForSimplePattern(lastPart);
                if (secondLastPart is null) return lastFilter;

                var secondLastFilter = CreatePredicateForSimplePattern(secondLastPart);
                return name => lastFilter(name) || secondLastFilter(name);
            }
        }

        return CreatePredicateForSimplePattern(clean);
    }

    internal static bool MatchesSymbol(ISymbol symbol, string pattern)
    {
        var clean = CleanPattern(pattern);
        if (clean.Contains('.'))
        {
            var display = symbol.ToDisplayString();
            if (display.Contains(clean, StringComparison.OrdinalIgnoreCase)) return true;

            var parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var lastPart = parts[^1];
                if (!MatchesSimplePattern(symbol.Name, lastPart)) return false;

                var containerPrefix = string.Join(".", parts.Take(parts.Length - 1));
                return display.Contains(containerPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }

        return MatchesSimplePattern(symbol.Name, clean);
    }

    internal static async Task<IReadOnlyList<string>> FindSimilarSymbolNamesAsync(
        Solution solution,
        string rawPattern,
        CancellationToken ct)
    {
        var clean = CleanPattern(rawPattern).Trim('*', '?');
        var words = Regex.Matches(clean, @"[A-Z][a-z0-9]+|[a-z0-9]+")
            .Select(m => m.Value)
            .Where(w => w.Length >= MinWordLengthForSuggestions)
            .Take(2)
            .ToList();

        if (words.Count == 0) return Array.Empty<string>();

        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in words)
        {
            ct.ThrowIfCancellationRequested();
            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
                solution,
                name => name.Contains(word, StringComparison.OrdinalIgnoreCase),
                SymbolFilter.Type,
                ct).ConfigureAwait(false);

            foreach (var sym in symbols)
            {
                candidates.Add(sym.Name);
                if (candidates.Count >= MaxSuggestions) break;
            }

            if (candidates.Count >= MaxSuggestions) break;
        }

        return candidates.ToList();
    }

    private static Func<string, bool> CreatePredicateForSimplePattern(string pattern)
    {
        if (pattern.Length >= 2 && pattern.StartsWith('*') && pattern.EndsWith('*')
            && !pattern[1..^1].Contains('*') && !pattern[1..^1].Contains('?'))
        {
            var sub = pattern.Trim('*');
            return name => name.Contains(sub, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return name => regex.IsMatch(name);
        }

        return name => name.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSimplePattern(string name, string pattern)
    {
        if (pattern.Length >= 2 && pattern.StartsWith('*') && pattern.EndsWith('*')
            && !pattern[1..^1].Contains('*') && !pattern[1..^1].Contains('?'))
        {
            return name.Contains(pattern.Trim('*'), StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return name.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
