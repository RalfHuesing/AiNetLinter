#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MetricsLookup;

namespace AiNetLinter.Mcp.Tools.FeatureContext;

/// <summary>
/// Formatiert das Aggregations-Ergebnis von <c>get_feature_context</c> in einen uebersichtlichen,
/// strukturierten Markdown-Report.
/// </summary>
internal static class FeatureContextFormatter
{
    internal static string FormatReport(FeatureContextPayload payload)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Feature-Kontext: {payload.Declaration.Name}");
        sb.AppendLine();

        AppendDeclarationSection(sb, payload.Declaration);
        AppendMetricsSection(sb, payload.Metrics);
        AppendCallersSection(sb, payload.Callers);
        AppendTestsSection(sb, payload.Tests);
        AppendViolationsSection(sb, payload.Violations, payload.Declaration.FilePath);

        var text = sb.ToString().TrimEnd();
        var hasTruncation = (payload.Callers?.IsTruncated == true) ||
                            (payload.Tests?.IsTruncated == true) ||
                            (payload.Violations?.IsTruncated == true);

        return hasTruncation ? text : McpSufficiencyHints.Append(text);
    }

    private static void AppendDeclarationSection(StringBuilder sb, SymbolDeclarationDto decl)
    {
        sb.AppendLine("## 1. Symbol & Deklaration");
        var details = !string.IsNullOrEmpty(decl.ReturnType)
            ? $"{decl.Kind} ({decl.Accessibility} {decl.ReturnType})"
            : $"{decl.Kind} ({decl.Accessibility})";
        sb.AppendLine($"- **Art:** {details}");
        sb.AppendLine($"- **Datei:** {decl.FilePath}:{decl.StartLine}-{decl.EndLine} ({decl.LineCount} Zeilen)");

        if (!string.IsNullOrEmpty(decl.ContainerType))
        {
            sb.AppendLine($"- **Container:** {decl.ContainerType}");
        }
        if (decl.Parameters.Count > 0)
        {
            sb.AppendLine($"- **Parameter:** {string.Join(", ", decl.Parameters)}");
        }
        if (!string.IsNullOrEmpty(decl.DocCommentId))
        {
            sb.AppendLine($"- **DocCommentId:** `{decl.DocCommentId}`");
        }
        sb.AppendLine();
    }

    private static void AppendMetricsSection(StringBuilder sb, MetricsLookupResultDto? metrics)
    {
        if (metrics == null) return;

        sb.AppendLine("## 2. Metriken & Budget (rules.json)");
        FormatMetricsChecks(sb, metrics);
        sb.AppendLine();
    }

    private static void AppendCallersSection(StringBuilder sb, CallersReportDto? callers)
    {
        if (callers == null) return;

        var header = callers.TotalCallers == 1
            ? "## 3. Direkte Aufrufer (1 Fundstelle)"
            : $"## 3. Direkte Aufrufer ({callers.TotalCallers} Fundstellen)";
        sb.AppendLine(header);

        if (callers.CallSites.Count == 0)
        {
            sb.AppendLine("- Keine direkten Aufrufer gefunden.");
        }
        else
        {
            foreach (var call in callers.CallSites)
            {
                var callerDesc = !string.IsNullOrEmpty(call.CallerMemberName)
                    ? $"`{call.CallerMemberName}()` in `{call.ProjectName}`"
                    : $"Aufruf in `{call.ProjectName}`";
                sb.AppendLine($"- `{call.FilePath}:{call.Line}` — {callerDesc}");
            }

            if (callers.IsTruncated)
            {
                sb.AppendLine($"- *(Zeige {callers.CallSites.Count} von {callers.TotalCallers} Aufrufern — maxCallers erhoehen fuer alle)*");
            }
        }
        sb.AppendLine();
    }

    private static void AppendTestsSection(StringBuilder sb, TestCoverageReportDto? tests)
    {
        if (tests == null) return;

        var header = $"## 4. Test-Kontext (statische Test-Zuordnung: {tests.TotalTestFiles} Testdateien, {tests.TotalMatchingTests} Tests)";
        sb.AppendLine(header);

        if (tests.TestFiles.Count == 0)
        {
            sb.AppendLine("- Keine Tests statisch zugeordnet.");
        }
        else
        {
            foreach (var file in tests.TestFiles)
            {
                sb.AppendLine($"- `{file.FilePath}` ({file.Category}, {file.TestMethods.Count} Tests — {file.MatchReason})");
                foreach (var method in file.TestMethods)
                {
                    sb.AppendLine($"  - `{method}()`");
                }
            }

            if (tests.IsTruncated)
            {
                sb.AppendLine($"- *(Zeige {tests.TestFiles.Count} von {tests.TotalTestFiles} Testdateien — maxTests erhoehen fuer alle)*");
            }
        }
        sb.AppendLine();
    }

    private static void AppendViolationsSection(StringBuilder sb, ViolationsReportDto? v, string filePath)
    {
        if (v == null) return;

        var header = $"## 5. Offene Violations auf dieser Datei ({v.TotalViolationsOnFile} Verstoesse)";
        sb.AppendLine(header);

        if (v.Violations.Count == 0)
        {
            sb.AppendLine($"- Keine Linter-Verstoesse auf `{filePath}`.");
        }
        else
        {
            foreach (var item in v.Violations)
            {
                var marker = item.IsDirectlyOnSymbol ? " **[DIREKT AUF SYMBOL]**" : "";
                sb.AppendLine($"- Zeile {item.Line}: `{item.RuleId}` — {item.Message}{marker}");
            }

            if (v.IsTruncated)
            {
                sb.AppendLine($"- *(Zeige {v.Violations.Count} von {v.TotalViolationsOnFile} Verstoessen)*");
            }
        }
        sb.AppendLine();
    }

    private static void FormatMetricsChecks(StringBuilder sb, MetricsLookupResultDto metrics)
    {
        if (metrics.MethodMetrics != null)
        {
            var m = metrics.MethodMetrics;
            FormatCheckLine(sb, "Cyclomatic Complexity", MetricNames.CyclomaticComplexity, m.CyclomaticComplexity, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Cognitive Complexity", MetricNames.CognitiveComplexity, m.CognitiveComplexity, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Method LOC", MetricNames.LineCount, m.CodeLines, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Parameter", MetricNames.ParameterCount, m.EffectiveParameters, metrics.ThresholdChecks);
            return;
        }

        if (metrics.TypeMetrics != null)
        {
            var t = metrics.TypeMetrics;
            FormatCheckLine(sb, "Type LOC", MetricNames.LineCount, t.CodeLines, metrics.ThresholdChecks);
            FormatCheckLine(sb, "AI-Context-Footprint", LinterRuleIds.AIContextFootprint, t.AiContextFootprint, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Public Members", MetricNames.PublicMemberCount, t.PublicMemberCount, metrics.ThresholdChecks);
            return;
        }

        if (metrics.PropertyMetrics != null)
        {
            var p = metrics.PropertyMetrics;
            FormatCheckLine(sb, "Cyclomatic Complexity", MetricNames.CyclomaticComplexity, p.CyclomaticComplexity, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Cognitive Complexity", MetricNames.CognitiveComplexity, p.CognitiveComplexity, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Property LOC", MetricNames.LineCount, p.CodeLines, metrics.ThresholdChecks);
            return;
        }

        foreach (var check in metrics.ThresholdChecks)
        {
            FormatCheckLine(sb, check.Metric, check.Metric, check.Value, metrics.ThresholdChecks);
        }
    }

    private static void FormatCheckLine(
        StringBuilder sb, string displayLabel, string metricKey, int value, IReadOnlyList<ThresholdCheckDto> checks)
    {
        var match = checks.FirstOrDefault(c => string.Equals(c.Metric, metricKey, StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(c.Metric, displayLabel, StringComparison.OrdinalIgnoreCase) ||
                                               c.Metric.Contains(metricKey, StringComparison.OrdinalIgnoreCase));
        if (match == null || match.Limit <= 0)
        {
            sb.AppendLine($"- **{displayLabel}:** {value}");
            return;
        }

        var budget = match.Limit - value;
        var budgetText = budget >= 0 ? $"Budget verbleibend: {budget}" : $"Ueberschreitung: {-budget}";
        sb.AppendLine($"- **{displayLabel}:** {value} / Limit: {match.Limit} (Status: {match.Status}, {budgetText})");
    }
}
