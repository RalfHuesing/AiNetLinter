#nullable enable

using System.Collections.Generic;
using AiNetLinter.Configuration;

namespace AiNetLinter.Core;

internal static class CompoundSuppressionEvaluator
{
    private const int NoSuppressionSentinel = -1;

    internal static int Evaluate(
        string ruleName,
        IReadOnlyList<CompoundSuppression>? suppressions,
        IReadOnlyDictionary<string, int> metrics)
    {
        if (suppressions == null || suppressions.Count == 0)
            return NoSuppressionSentinel;

        foreach (var suppression in suppressions)
        {
            if (suppression.TargetRule != ruleName) continue;
            if (!AllConditionsMet(suppression.WhenAllOf, metrics)) continue;

            return suppression.RelaxedLimit ?? 0;
        }

        return NoSuppressionSentinel;
    }

    internal static CompoundSuppression? FindConfigured(
        string ruleName,
        IReadOnlyList<CompoundSuppression>? suppressions)
    {
        if (suppressions == null) return null;
        foreach (var s in suppressions)
            if (s.TargetRule == ruleName) return s;
        return null;
    }

    internal static string? GetActiveSeverityOverride(
        string ruleName,
        IReadOnlyList<CompoundSuppression>? suppressions,
        IReadOnlyDictionary<string, int> metrics)
    {
        if (suppressions == null || suppressions.Count == 0) return null;
        foreach (var s in suppressions)
        {
            if (s.TargetRule != ruleName) continue;
            if (s.SeverityOverride == null) continue;
            if (!AllConditionsMet(s.WhenAllOf, metrics)) return null;
            return s.SeverityOverride;
        }
        return null;
    }

    private static bool AllConditionsMet(
        IReadOnlyList<MetricCondition> conditions,
        IReadOnlyDictionary<string, int> metrics)
    {
        foreach (var cond in conditions)
        {
            if (!metrics.TryGetValue(cond.Metric, out var value)) return false;
            if (cond.AtMost.HasValue  && value > cond.AtMost.Value)  return false;
            if (cond.AtLeast.HasValue && value < cond.AtLeast.Value) return false;
        }
        return true;
    }

    internal static string BuildConditionSummary(
        IReadOnlyList<MetricCondition> conditions,
        IReadOnlyDictionary<string, int> actual)
    {
        var parts = new List<string>();
        foreach (var c in conditions)
        {
            actual.TryGetValue(c.Metric, out var val);
            var check = c.AtMost.HasValue
                ? (val <= c.AtMost.Value ? "✓" : "✗")
                : (val >= c.AtLeast!.Value ? "✓" : "✗");
            var op = c.AtMost.HasValue ? $"≤ {c.AtMost}" : $"≥ {c.AtLeast}";
            parts.Add($"{c.Metric}={val} {op} {check}");
        }
        return string.Join(", ", parts);
    }

    internal static string BuildThresholdSummary(IReadOnlyList<MetricCondition> conditions)
    {
        var parts = new List<string>();
        foreach (var c in conditions)
        {
            if (c.AtMost.HasValue)  parts.Add($"{c.Metric} ≤ {c.AtMost}");
            if (c.AtLeast.HasValue) parts.Add($"{c.Metric} ≥ {c.AtLeast}");
        }
        return string.Join(" AND ", parts);
    }
}
