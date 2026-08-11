#nullable enable

using AiNetLinter.Models;

namespace AiNetLinter.Core;

internal static partial class RuleRegistry
{
    /// <summary>
    /// Loest die effektive Severity eines Regelverstosses auf: <see cref="RuleViolation.EffectiveSeverity"/>
    /// (z. B. von einer Compound-Suppression gesetzt) hat Vorrang, sonst gilt der Registry-Default
    /// der Regel — zentral statt separat in <c>GetViolationsScanner</c>, <c>MetricsTreeRoslynScanner</c>
    /// und <c>SafeguardScanner</c> dupliziert.
    /// </summary>
    public static string ResolveSeverity(RuleViolation v)
    {
        if (!string.IsNullOrEmpty(v.EffectiveSeverity)) return v.EffectiveSeverity;
        return TryResolve(v.RuleName)?.Severity ?? "warning";
    }
}
