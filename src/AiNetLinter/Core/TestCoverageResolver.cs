using AiNetLinter.Configuration;

namespace AiNetLinter.Core;

/// <summary>
/// Prüft, ob eine Quellklasse durch Testsignale abgedeckt ist.
/// </summary>
public static class TestCoverageResolver
{
    /// <summary>
    /// Ermittelt, ob für die Quellklasse Testabdeckung nachgewiesen wurde.
    /// </summary>
    public static bool IsCovered(string sourceClassName, TestCoverageIndex index, TestSentinelConfig config)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(config);
        ValidateSourceClassName(sourceClassName);

        if (MatchesClassNamePattern(sourceClassName, index.TestClassNames, config.ClassNamePatterns))
        {
            return true;
        }

        return HasSignalCoverage(sourceClassName, index, config);
    }

    private static void ValidateSourceClassName(string sourceClassName)
    {
        if (string.IsNullOrWhiteSpace(sourceClassName))
        {
            throw new ArgumentException(
                "Der Quellklassenname darf nicht null oder leer sein.",
                nameof(sourceClassName));
        }
    }

    private static bool HasSignalCoverage(
        string sourceClassName,
        TestCoverageIndex index,
        TestSentinelConfig config)
    {
        if (config.RecognizeTypeofReference && index.ReferencedTypeNames.Contains(sourceClassName))
        {
            return true;
        }

        return config.RecognizeCoversComment && index.CoversComments.Contains(sourceClassName);
    }

    private static bool MatchesClassNamePattern(
        string sourceName,
        IEnumerable<string> testClassNames,
        IReadOnlyList<string> patterns)
    {
        EnsurePatternsConfigured(patterns);

        foreach (var testClass in testClassNames)
        {
            if (AnyPatternMatches(sourceName, testClass, patterns))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsurePatternsConfigured(IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            throw new InvalidOperationException(
                "TestSentinel.ClassNamePatterns ist nicht konfiguriert. " +
                "Setze mindestens ein Pattern wie \"{Name}Tests\" in rules.json.");
        }
    }

    private static bool AnyPatternMatches(string sourceName, string? testClass, IReadOnlyList<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(testClass))
        {
            return false;
        }

        foreach (var pattern in patterns)
        {
            if (MatchesPattern(sourceName, testClass, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesPattern(string sourceName, string testClassName, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var expected = pattern.Replace("{Name}", sourceName, StringComparison.Ordinal);
        if (!expected.Contains('*'))
        {
            return string.Equals(testClassName, expected, StringComparison.Ordinal);
        }

        return MatchesWildcardPattern(testClassName, expected);
    }

    private static bool MatchesWildcardPattern(string testClassName, string expected)
    {
        var prefix = expected[..expected.IndexOf('*')];
        var suffix = expected[(expected.LastIndexOf('*') + 1)..];
        return testClassName.StartsWith(prefix, StringComparison.Ordinal) &&
               testClassName.EndsWith(suffix, StringComparison.Ordinal);
    }
}
