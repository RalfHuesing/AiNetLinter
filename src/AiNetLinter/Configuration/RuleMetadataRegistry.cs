using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Core;

namespace AiNetLinter.Configuration;

/// <summary>
/// Liefert Standard-Metadaten für bekannte Regeln und merged benutzerdefinierte Einträge.
/// </summary>
public static class RuleMetadataRegistry
{
    public static IReadOnlyCollection<string> KnownRuleNames =>
        RuleRegistry.All.Where(r => !string.IsNullOrEmpty(r.Warum)).Select(r => r.RuleId).ToList().AsReadOnly();

    /// <summary>
    /// Ermittelt Severity und Intent für eine Regel (Konfiguration überschreibt Defaults).
    /// </summary>
    public static RuleMetadataEntry Resolve(string ruleName, LinterConfig config)
    {
        if (config.RuleMetadata.TryGetValue(ruleName, out var configured))
        {
            return configured;
        }

        var meta = RuleRegistry.TryResolve(ruleName);
        if (meta != null)
        {
            return new RuleMetadataEntry
            {
                Severity = meta.Severity,
                Intent = meta.Intent
            };
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
}
