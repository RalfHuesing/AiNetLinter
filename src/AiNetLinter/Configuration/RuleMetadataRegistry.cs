using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Core;

namespace AiNetLinter.Configuration;

public static class RuleMetadataRegistry
{
    public static IReadOnlyCollection<string> KnownRuleNames =>
        RuleRegistry.All.Where(r => !string.IsNullOrEmpty(r.Warum)).Select(r => r.RuleId).ToList().AsReadOnly();

    public static RuleMetadataEntry Resolve(string ruleName, Config config)
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

    public static bool HasErrorSeverity(IEnumerable<Models.RuleViolation> violations, Config config)
    {
        foreach (var v in violations)
        {
            if (v.EffectiveSeverity != null)
            {
                if (v.EffectiveSeverity.Equals("error", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                continue;
            }
            var meta = Resolve(v.RuleName ?? "", config);
            if (meta.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
