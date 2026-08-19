#nullable enable

using System;
using System.Linq;
using System.Text;
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
        var decl = payload.Declaration;

        sb.AppendLine($"# Feature-Kontext: {decl.Name}");
        sb.AppendLine();

        // Sektion 1: Deklaration
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

        // Sektion 2: Metriken
        if (payload.Metrics != null)
        {
            sb.AppendLine("## 2. Metriken & Budget (rules.json)");
            FormatMetricsSection(sb, payload.Metrics);
            sb.AppendLine();
        }

        // Sektion 3: Callers
        if (payload.Callers != null)
        {
            var callers = payload.Callers;
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
                    sb.AppendLine($"- `{call.FilePath}:{call.Line}` — Aufruf in `{call.ProjectName}`");
                }

                if (callers.IsTruncated)
                {
                    sb.AppendLine($"- *(Zeige {callers.CallSites.Count} von {callers.TotalCallers} Aufrufern — maxCallers erhoehen fuer alle)*");
                }
            }
            sb.AppendLine();
        }

        // Sektion 4: Test-Abdeckung
        if (payload.Tests != null)
        {
            var tests = payload.Tests;
            var header = $"## 4. Test-Abdeckung ({tests.TotalTestFiles} Testdateien, {tests.TotalMatchingTests} Tests)";
            sb.AppendLine(header);

            if (tests.TestFiles.Count == 0)
            {
                sb.AppendLine("- Keine zugehoerigen Tests identifiziert (Symbol erscheint ungetestet).");
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

        // Sektion 5: Violations
        if (payload.Violations != null)
        {
            var v = payload.Violations;
            var header = $"## 5. Offene Violations auf dieser Datei ({v.TotalViolationsOnFile} Verstoesse)";
            sb.AppendLine(header);

            if (v.Violations.Count == 0)
            {
                sb.AppendLine($"- Keine Linter-Verstoesse auf `{decl.FilePath}`.");
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

        var text = sb.ToString().TrimEnd();
        var hasTruncation = (payload.Callers?.IsTruncated == true) ||
                            (payload.Tests?.IsTruncated == true) ||
                            (payload.Violations?.IsTruncated == true);

        return hasTruncation ? text : McpSufficiencyHints.Append(text);
    }

    private static void FormatMetricsSection(StringBuilder sb, MetricsLookupResultDto metrics)
    {
        if (metrics.MethodMetrics != null)
        {
            var m = metrics.MethodMetrics;
            FormatCheckLine(sb, "Cyclomatic Complexity", m.CyclomaticComplexity, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Cognitive Complexity", m.CognitiveComplexity, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Method LOC", m.CodeLines, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Parameter", m.EffectiveParameters, metrics.ThresholdChecks);
        }
        else if (metrics.TypeMetrics != null)
        {
            var t = metrics.TypeMetrics;
            FormatCheckLine(sb, "Type LOC", t.CodeLines, metrics.ThresholdChecks);
            FormatCheckLine(sb, "AI-Context-Footprint", t.AiContextFootprint, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Public Members", t.PublicMemberCount, metrics.ThresholdChecks);
        }
        else if (metrics.PropertyMetrics != null)
        {
            var p = metrics.PropertyMetrics;
            FormatCheckLine(sb, "Cyclomatic Complexity", p.CyclomaticComplexity, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Cognitive Complexity", p.CognitiveComplexity, metrics.ThresholdChecks);
            FormatCheckLine(sb, "Property LOC", p.CodeLines, metrics.ThresholdChecks);
        }
        else
        {
            foreach (var check in metrics.ThresholdChecks)
            {
                FormatCheckLine(sb, check.Metric, check.Value, metrics.ThresholdChecks);
            }
        }
    }

    private static void FormatCheckLine(
        StringBuilder sb, string metricLabel, int value, System.Collections.Generic.IReadOnlyList<ThresholdCheckDto> checks)
    {
        var match = checks.FirstOrDefault(c => string.Equals(c.Metric, metricLabel, StringComparison.OrdinalIgnoreCase) ||
                                               c.Metric.Contains(metricLabel, StringComparison.OrdinalIgnoreCase));
        if (match == null || match.Limit <= 0)
        {
            sb.AppendLine($"- **{metricLabel}:** {value}");
            return;
        }

        var budget = match.Limit - value;
        var budgetText = budget >= 0 ? $"Budget verbleibend: {budget}" : $"Ueberschreitung: {-budget}";
        sb.AppendLine($"- **{metricLabel}:** {value} / Limit: {match.Limit} (Status: {match.Status}, {budgetText})");
    }
}
