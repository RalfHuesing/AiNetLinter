namespace AiNetLinter.Configuration;

/// <summary>
/// Stellt deserialisierte Konfigurationen auf gültige Standardwerte ein.
/// </summary>
public static class LinterConfigNormalizer
{
    private static readonly string[] DefaultClassNamePatterns =
    [
        "{Name}Tests",
        "{Name}Test",
        "{Name}IntegrationTests",
        "{Name}*Tests",
    ];

    /// <summary>
    /// Normalisiert optionale TestSentinel-Felder nach JSON-Deserialisierung.
    /// </summary>
    public static LinterConfig Normalize(LinterConfig config)
    {
        var testSentinel = config.TestSentinel ?? new TestSentinelConfig();
        var patterns = NormalizeClassNamePatterns(testSentinel.ClassNamePatterns);
        var fileFilters = config.FileFilters ?? new FileFiltersConfig();

        return config with
        {
            TestSentinel = testSentinel with
            {
                ClassNamePatterns = patterns,
                ExemptClassNameSuffixes = testSentinel.ExemptClassNameSuffixes ?? Array.Empty<string>(),
                ExemptWhenInheritsFrom = testSentinel.ExemptWhenInheritsFrom ?? Array.Empty<string>(),
            },
            FileFilters = fileFilters,
        };
    }

    private static IReadOnlyList<string> NormalizeClassNamePatterns(IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return DefaultClassNamePatterns;
        }

        var validPatterns = patterns
            .Select((pattern, index) => ValidatePattern(pattern, index))
            .ToArray();

        return validPatterns.Length == 0 ? DefaultClassNamePatterns : validPatterns;
    }

    private static string ValidatePattern(string? pattern, int index)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new InvalidOperationException(
                $"TestSentinel.ClassNamePatterns[{index}] ist null oder leer. " +
                "Jedes Pattern muss den Platzhalter '{{Name}}' enthalten (z. B. \"{{Name}}Tests\").");
        }

        if (!pattern.Contains("{Name}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"TestSentinel.ClassNamePatterns[{index}] ('{pattern}') enthält keinen '{{Name}}'-Platzhalter.");
        }

        return pattern;
    }
}
