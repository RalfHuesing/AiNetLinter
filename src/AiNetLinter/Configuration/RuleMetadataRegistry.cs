namespace AiNetLinter.Configuration;

/// <summary>
/// Liefert Standard-Metadaten für bekannte Regeln und merged benutzerdefinierte Einträge.
/// </summary>
public static class RuleMetadataRegistry
{
    private static readonly IReadOnlyDictionary<string, RuleMetadataEntry> Defaults =
        new Dictionary<string, RuleMetadataEntry>(StringComparer.Ordinal)
        {
            ["MaxLineCount"] = new() { Severity = "error", Intent = "agent-context" },
            ["MaxMethodLineCount"] = new() { Severity = "error", Intent = "agent-context" },
            ["MaxCognitiveComplexity"] = new() { Severity = "error", Intent = "agent-context" },
            ["MaxCyclomaticComplexity"] = new() { Severity = "error", Intent = "agent-context" },
            ["AllowOutParameters"] = new() { Severity = "warning", Intent = "csharp-idiom" },
            ["StaticTestSentinel"] = new() { Severity = "warning", Intent = "test-coverage" },
            ["EnforceMinimalApiAsParameters"] = new() { Severity = "error", Intent = "aspnet-binding" },
            ["EnforceNoSilentCatch"] = new() { Severity = "error", Intent = "agent-resilience" },
            ["ForbiddenNamespaceDependency"] = new() { Severity = "error", Intent = "architecture" },
            ["EnforceResultPatternOverExceptions"] = new() { Severity = "error", Intent = "control-flow" },
            ["EnforceNoVariableShadowing"] = new() { Severity = "error", Intent = "agent-resilience" },
            ["EnforceReadonlyParameters"] = new() { Severity = "error", Intent = "agent-resilience" },
            ["EnforceReadonlyFields"] = new() { Severity = "error", Intent = "agent-resilience" },
            ["EnforceNoMagicValues"] = new() { Severity = "error", Intent = "general" },
            ["MaxMethodOverloads"] = new() { Severity = "warning", Intent = "agent-context" },
            ["MaxConstructorDependencies"] = new() { Severity = "warning", Intent = "agent-context" },
        };

    /// <summary>
    /// Ermittelt Severity und Intent für eine Regel (Konfiguration überschreibt Defaults).
    /// </summary>
    public static RuleMetadataEntry Resolve(string ruleName, LinterConfig config)
    {
        if (config.RuleMetadata.TryGetValue(ruleName, out var configured))
        {
            return configured;
        }

        if (Defaults.TryGetValue(ruleName, out var fallback))
        {
            return fallback;
        }

        return new RuleMetadataEntry();
    }

    /// <summary>
    /// Mappt eine Severity-Zeichenkette auf SARIF level.
    /// </summary>
    public static string ToSarifLevel(string severity) =>
        severity.Equals("warning", StringComparison.OrdinalIgnoreCase) ? "warning" : "error";
}
