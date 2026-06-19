using System.Collections.Generic;
using System.Linq;

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
            ["MaxMethodParameterCount"] = new() { Severity = "warning", Intent = "agent-context" },
            ["MaxCognitiveComplexity"] = new() { Severity = "error", Intent = "agent-context" },
            ["MaxCyclomaticComplexity"] = new() { Severity = "error", Intent = "agent-context" },
            ["MaxInheritanceDepth"] = new() { Severity = "warning", Intent = "agent-context" },
            ["MaxMethodOverloads"] = new() { Severity = "warning", Intent = "agent-context" },
            ["MaxConstructorDependencies"] = new() { Severity = "warning", Intent = "agent-context" },
            ["MaxDirectoryDepth"] = new() { Severity = "warning", Intent = "agent-context" },
            ["MaxDirectoryChildren"] = new() { Severity = "warning", Intent = "agent-context" },
            ["MaxBoolParameterCount"] = new() { Severity = "warning", Intent = "agent-context" },
            ["MaxPartialClassFiles"] = new() { Severity = "warning", Intent = "agent-context" },
            ["MaxPublicMembersPerType"] = new() { Severity = "warning", Intent = "agent-context" },
            ["AIContextFootprint"] = new() { Severity = "warning", Intent = "agent-context" },
            ["PreventContextDependentOverloads"] = new() { Severity = "error", Intent = "agent-context" },
            ["AllowOutParameters"] = new() { Severity = "warning", Intent = "csharp-idiom" },
            ["StaticTestSentinel"] = new() { Severity = "warning", Intent = "test-coverage" },
            ["EnforceNoSilentCatch"] = new() { Severity = "error", Intent = "agent-resilience" },
            ["EnforceExplicitStateImmutability"] = new() { Severity = "error", Intent = "agent-resilience" },
            ["EnforceMinimalApiAsParameters"] = new() { Severity = "error", Intent = "aspnet-binding" },
            ["EnforceResultPatternOverExceptions"] = new() { Severity = "error", Intent = "control-flow" },
            ["ForbiddenNamespaceDependency"] = new() { Severity = "error", Intent = "architecture" },
            ["EnforceNamespaceDirectoryMapping"] = new() { Severity = "error", Intent = "architecture" },
            ["DetectAndBanPhantomDependencies"] = new() { Severity = "error", Intent = "architecture" },
            ["BlazorRequireCodeBehind"] = new() { Severity = "error", Intent = "architecture" },
            ["BlazorRequireCssIsolation"] = new() { Severity = "warning", Intent = "architecture" },
            ["WpfRequireMinimalCodeBehind"] = new() { Severity = "error", Intent = "architecture" },
            ["BanPublicNestedTypes"] = new() { Severity = "error", Intent = "agent-context" },
            ["EnforceSealedClasses"] = new() { Severity = "error", Intent = "general" },
            ["EnforceValueObjectContracts"] = new() { Severity = "error", Intent = "general" },
            ["EnforcePascalCase"] = new() { Severity = "error", Intent = "general" },
            ["EnforceXmlDocumentation"] = new() { Severity = "warning", Intent = "general" },
            ["EnforceSemanticNaming"] = new() { Severity = "error", Intent = "general" },
            ["EnforceNullableEnable"] = new() { Severity = "error", Intent = "general" },
            ["AllowDynamic"] = new() { Severity = "error", Intent = "general" },
        };

    public static IReadOnlyCollection<string> KnownRuleNames => Defaults.Keys.ToList().AsReadOnly();

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
    /// Gibt true zurück wenn mindestens ein Verstoß in der Sammlung Severity "error" hat.
    /// </summary>
    public static bool HasErrorSeverity(IEnumerable<Models.RuleViolation> violations, LinterConfig config)
    {
        foreach (var v in violations)
        {
            var meta = Resolve(v.RuleName ?? "", config);
            if (meta.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Mappt eine Severity-Zeichenkette auf SARIF level.
    /// </summary>
    public static string ToSarifLevel(string severity) =>
        severity.Equals("warning", StringComparison.OrdinalIgnoreCase) ? "warning"
        : severity.Equals("info", StringComparison.OrdinalIgnoreCase) ? "note"
        : "error";
}
