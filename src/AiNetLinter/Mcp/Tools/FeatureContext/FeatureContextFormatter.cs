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
        var completeness = ResolveDisplayCompleteness(payload);
        sb.AppendLine($"# Feature-Kontext: {payload.Declaration.Name}");
        sb.AppendLine($"- **Composite-Completeness:** `{completeness}`");
        if (payload.MetricsStatus is not null)
        {
            sb.AppendLine($"- **Metriken-Status:** `{payload.MetricsStatus}`");
        }
        if (!string.IsNullOrWhiteSpace(payload.NextStep))
        {
            sb.AppendLine($"- **Nächster sicherer Schritt:** {payload.NextStep}");
        }
        sb.AppendLine();

        AppendDeclarationSection(sb, payload.Declaration);
        AppendMetricsSection(sb, payload.Metrics, payload.MetricsStatus);
        AppendCallersSection(sb, payload.Callers);
        AppendTestsSection(sb, payload.Tests);
        AppendViolationsSection(sb, payload.Violations, payload.Declaration.FilePath);

        var text = sb.ToString().TrimEnd();
        return text;
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
        if (decl.BaseTypes is { Count: > 0 })
        {
            sb.AppendLine($"- **Basis & Interfaces:** {string.Join(", ", decl.BaseTypes)}");
        }
        if (decl.Parameters.Count > 0)
        {
            sb.AppendLine($"- **Parameter:** {string.Join(", ", decl.Parameters)}");
        }
        if (decl.Members is { Count: > 0 })
        {
            sb.AppendLine($"- **Member ({decl.Members.Count}):** {string.Join("; ", decl.Members)}");
        }
        if (!string.IsNullOrEmpty(decl.DocCommentId))
        {
            sb.AppendLine($"- **DocCommentId:** `{decl.DocCommentId}`");
        }
        sb.AppendLine();
    }

    private static void AppendMetricsSection(
        StringBuilder sb,
        MetricsLookupResultDto? metrics,
        string? status)
    {
        if (metrics == null)
        {
            if (status == FeatureContextStatus.NotConfigured)
            {
                sb.AppendLine("## 2. Metriken & Budget (Status: not_configured)");
                sb.AppendLine("- Metriken nicht bewertet: neben dem Target fehlt `ainetlinter-rules.json`.");
                sb.AppendLine();
            }

            return;
        }

        sb.AppendLine("## 2. Metriken & Budget (ainetlinter-rules.json)");
        FormatMetricsChecks(sb, metrics);
        sb.AppendLine();
    }

    private static void AppendCallersSection(StringBuilder sb, CallersReportDto? callers)
    {
        if (callers == null) return;

        var header = callers.TotalCallers == 1
            ? $"## 3. Statische Referenzen/Call-Sites (1 Fundstelle; Status: {callers.Completeness}; {callers.Semantics})"
            : $"## 3. Statische Referenzen/Call-Sites ({callers.TotalCallers} Fundstellen; Status: {callers.Completeness}; {callers.Semantics})";
        sb.AppendLine(header);
        sb.AppendLine($"- **Counts:** {callers.CallSites.Count} von {callers.TotalCallers} statischen Referenzen/Call-Sites zurückgegeben.");

        if (callers.CallSites.Count == 0)
        {
            sb.AppendLine("- Keine statischen Referenzen/Call-Sites gefunden.");
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

        }

        if (callers.IsTruncated)
        {
            sb.AppendLine($"- *(Betroffener Abschnitt: impact; Zeige {callers.CallSites.Count} von {callers.TotalCallers} statischen Referenzen — Begrenzung: {string.Join(", ", callers.TruncatedBy ?? [])})*");
            sb.AppendLine($"- **TruncatedBy:** `{string.Join(", ", callers.TruncatedBy ?? [])}`");
        }
        if (!string.IsNullOrWhiteSpace(callers.NextStep))
        {
            sb.AppendLine($"- **Nächster sicherer Schritt:** {callers.NextStep}");
        }
        sb.AppendLine();
    }

    private static void AppendTestsSection(StringBuilder sb, StaticTestContextReportDto? tests)
    {
        if (tests == null) return;

        var header = $"## 4. Test-Kontext (statische Testkandidaten: {tests.TotalTestFiles} Testdateien, {tests.TotalMatchingTests} Testkandidaten, Status: {tests.Completeness})";
        sb.AppendLine(header);
        sb.AppendLine($"- **Evidenzgrenze:** `{tests.EvidenceBoundary}`");
        sb.AppendLine($"- **Counts:** {tests.TestFiles.Count} von {tests.TotalTestFiles} Testdateien zurückgegeben; {tests.DisplayedTestMethods} konkrete Testmethoden sichtbar. Insgesamt {tests.TotalMatchingTests} Testkandidaten gefunden.");

        if (tests.TestFiles.Count == 0)
        {
            sb.AppendLine("- Keine statischen Testkandidaten zugeordnet.");
        }
        else
        {
            foreach (var file in tests.TestFiles)
            {
                var classCount = file.TestClassNames?.Count
                    ?? (string.IsNullOrWhiteSpace(file.TestClassName) ? 0 : 1);
                var evidence = $"{file.EvidenceKind}, confidence={file.Confidence}";
                var candidateDescription = file.TestMethods.Count > 0
                    ? $"{file.TestMethods.Count} von {file.TotalMatchingMethods} konkrete Testmethoden"
                    : $"{file.TotalTestCount} Tests auf Klassenebene ({classCount} Testklasse(n)); keine Methode behauptet";
                sb.AppendLine($"- `{file.FilePath}` ({file.Category}, {candidateDescription} — {evidence}; {file.MatchReason})");
                foreach (var method in file.TestMethods)
                {
                    sb.AppendLine($"  - `{method}()`");
                }
            }

        }

        if (tests.IsTruncated)
        {
            sb.AppendLine($"- **TruncatedBy:** `{string.Join(", ", tests.TruncatedBy ?? [])}`");
        }
        if (!string.IsNullOrWhiteSpace(tests.NextStep))
        {
            sb.AppendLine($"- **Nächster sicherer Schritt:** {tests.NextStep}");
        }
        sb.AppendLine();
    }

    private static void AppendViolationsSection(StringBuilder sb, ViolationsReportDto? v, string filePath)
    {
        if (v == null) return;

        var header = v.Status is FeatureContextStatus.Complete or FeatureContextStatus.Truncated
            ? $"## 5. Offene Violations auf dieser Datei ({v.TotalViolationsOnFile} Verstoesse, Status: {v.Status})"
            : $"## 5. Offene Violations auf dieser Datei (Status: {v.Status})";
        sb.AppendLine(header);

        if (v.Status is FeatureContextStatus.NotConfigured or FeatureContextStatus.NotDecidable or FeatureContextStatus.Partial or FeatureContextStatus.Error or FeatureContextStatus.NotApplicable)
        {
            sb.AppendLine($"- Violations nicht bewertet; der Abschnitt liefert keine Aussage über die Anzahl (ReasonCode: `{v.ReasonCode}`).");
            if (!string.IsNullOrWhiteSpace(v.NextStep)) sb.AppendLine($"- **Nächster sicherer Schritt:** {v.NextStep}");
            sb.AppendLine();
            return;
        }

        if (v.Violations.Count == 0)
        {
            sb.AppendLine($"- Keine Linter-Verstoesse auf `{filePath}` im geprüften Scope (0 von {v.TotalViolationsOnFile}).");
        }
        else
        {
            sb.AppendLine($"- **Count:** {v.Violations.Count} von {v.TotalViolationsOnFile} Violations im geprüften Scope.");
            foreach (var item in v.Violations)
            {
                var marker = item.IsDirectlyOnSymbol ? " **[DIREKT AUF SYMBOL]**" : "";
                sb.AppendLine($"- Zeile {item.Line}: `{item.RuleId}` — {item.Message}{marker}");
            }

            if (v.IsTruncated)
            {
                sb.AppendLine($"- *(Betroffener Abschnitt: violations; zeige {v.Violations.Count} von {v.TotalViolationsOnFile} Verstoessen — Begrenzung: {string.Join(", ", v.TruncatedBy ?? [])})*");
                sb.AppendLine($"- **Nächster sicherer Schritt:** {v.NextStep}");
            }
        }
        sb.AppendLine();
    }

    private static string ResolveDisplayCompleteness(FeatureContextPayload payload)
    {
        if (payload.Completeness is not FeatureContextStatus.Complete)
        {
            return payload.Completeness;
        }

        var statuses = new[]
        {
            payload.MetricsStatus,
            payload.Callers?.Completeness,
            payload.Tests?.Completeness,
            payload.Violations?.Status,
        };

        if (statuses.Contains(FeatureContextStatus.Error, StringComparer.Ordinal)) return FeatureContextStatus.Partial;
        if (statuses.Contains(FeatureContextStatus.NotDecidable, StringComparer.Ordinal)) return FeatureContextStatus.NotDecidable;
        if (statuses.Contains(FeatureContextStatus.NotConfigured, StringComparer.Ordinal)) return FeatureContextStatus.NotConfigured;
        if (statuses.Contains(FeatureContextStatus.NotApplicable, StringComparer.Ordinal)) return FeatureContextStatus.NotApplicable;
        if (statuses.Contains(FeatureContextStatus.Truncated, StringComparer.Ordinal)) return FeatureContextStatus.Truncated;
        if (statuses.Contains(FeatureContextStatus.Partial, StringComparer.Ordinal)) return FeatureContextStatus.Partial;
        return FeatureContextStatus.Complete;
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
