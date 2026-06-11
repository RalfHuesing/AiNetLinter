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
        if (MatchesClassNamePattern(sourceClassName, index.TestClassNames, config.ClassNamePatterns))
        {
            return true;
        }

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
        foreach (var testClass in testClassNames)
        {
            if (AnyPatternMatches(sourceName, testClass, patterns))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnyPatternMatches(string sourceName, string testClass, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (MatchesPattern(sourceName, testClass, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesPattern(string sourceName, string testClassName, string pattern)
    {
        var expected = pattern.Replace("{Name}", sourceName, StringComparison.Ordinal);
        if (!expected.Contains('*'))
        {
            return string.Equals(testClassName, expected, StringComparison.Ordinal);
        }

        var prefix = expected[..expected.IndexOf('*')];
        var suffix = expected[(expected.LastIndexOf('*') + 1)..];
        return testClassName.StartsWith(prefix, StringComparison.Ordinal) &&
               testClassName.EndsWith(suffix, StringComparison.Ordinal);
    }
}
