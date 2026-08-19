#nullable enable

using AiNetLinter.Core;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Tools.MetricsLookup;

/// <summary>
/// Formatiert das Ergebnis von <see cref="MetricsLookupResultDto"/> in lesbares Markdown.
/// </summary>
internal static class MetricsLookupFormatter
{
    internal static string Format(MetricsLookupResultDto dto)
    {
        var mb = new MarkdownBuilder();

        mb.Heading(3, $"{dto.SymbolKind}: {dto.QualifiedName}").BlankLine();

        if (dto.Location != null)
        {
            mb.Line($"- **Ort:** `{dto.Location.FilePath}:{dto.Location.StartLine}-{dto.Location.EndLine}`");
        }

        if (!string.IsNullOrEmpty(dto.DocCommentId))
        {
            mb.Line($"- **Id:** `{dto.DocCommentId}`");
        }

        mb.BlankLine();

        if (dto.ThresholdChecks.Count > 0)
        {
            mb.Heading(4, "Schwellwert-Abgleich & Metriken").BlankLine();
            var table = new MarkdownTableBuilder()
                .AddColumn("Metrik")
                .AddColumn("Wert", ColumnAlign.Right)
                .AddColumn("Grenzwert", ColumnAlign.Right)
                .AddColumn("Status", ColumnAlign.Center)
                .AddColumn("Regel");

            foreach (var check in dto.ThresholdChecks)
            {
                var limitStr = check.Limit > 0 ? $"<= {check.Limit}" : "-";
                var statusBadge = $"[{check.Status}]";
                var ruleStr = !string.IsNullOrEmpty(check.RuleId) ? check.RuleId : "-";

                table.AddRow(
                    FormatMetricDisplayName(check.Metric),
                    check.Value,
                    limitStr,
                    statusBadge,
                    ruleStr);
            }

            mb.Table(table);
            mb.BlankLine();
        }

        if (dto.MethodMetrics != null)
        {
            FormatMethodDetails(mb, dto.MethodMetrics);
        }
        else if (dto.TypeMetrics != null)
        {
            FormatTypeDetails(mb, dto.TypeMetrics);
        }
        else if (dto.PropertyMetrics != null)
        {
            FormatPropertyDetails(mb, dto.PropertyMetrics);
        }

        return mb.Build().TrimEnd();
    }

    private static void FormatMethodDetails(MarkdownBuilder mb, MethodMetricsDto method)
    {
        if (method.IgnoredParameters.Count > 0)
        {
            mb.Line($"**Ignorierte Parameter (vom Zählen ausgenommen):** {string.Join(", ", method.IgnoredParameters)}");
            mb.BlankLine();
        }
    }

    private static void FormatTypeDetails(MarkdownBuilder mb, TypeMetricsDto type)
    {
        mb.Heading(4, "Typ-Struktur").BlankLine();
        mb.Line($"- **Code-Zeilen (LOC):** {type.CodeLines}");
        mb.Line($"- **AI-Context-Footprint:** {type.AiContextFootprint} Zeilen");
        mb.Line($"- **Members:** {type.TotalMemberCount} gesamt ({type.PublicMemberCount} public, {type.MethodCount} Methoden, {type.PropertyCount} Properties)");
        mb.BlankLine();

        if (type.TopDependencies.Count > 0)
        {
            mb.Line("**Top-Abhängigkeiten (AI-Context-Footprint):**");
            foreach (var dep in type.TopDependencies)
            {
                mb.Line($"- `{dep.Name}`: {dep.Lines} Zeilen");
            }
            mb.BlankLine();
        }
    }

    private static void FormatPropertyDetails(MarkdownBuilder mb, PropertyMetricsDto prop)
    {
        mb.Heading(4, "Property-Details").BlankLine();
        mb.Line($"- **Code-Zeilen (LOC):** {prop.CodeLines}");
        mb.Line($"- **Zyklomatische Komplexität:** {prop.CyclomaticComplexity}");
        mb.Line($"- **Kognitive Komplexität:** {prop.CognitiveComplexity}");
        var accessors = (prop.HasGetter, prop.HasSetter) switch
        {
            (true, true) => "Getter & Setter",
            (true, false) => "Nur Getter",
            (false, true) => "Nur Setter",
            _ => "Keine expliziten Accessoren"
        };
        mb.Line($"- **Accessoren:** {accessors}");
        mb.BlankLine();
    }

    private static string FormatMetricDisplayName(string metric) => metric switch
    {
        MetricNames.LineCount => "Code-Zeilen (LOC)",
        LinterRuleIds.MaxLineCount => "Code-Zeilen (Typ LOC)",
        LinterRuleIds.MaxMethodLineCount => "Code-Zeilen (Methode LOC)",
        MetricNames.CyclomaticComplexity => "Zyklomatische Komplexität",
        MetricNames.CognitiveComplexity => "Kognitive Komplexität",
        MetricNames.ParameterCount => "Parameter-Anzahl (effektiv)",
        LinterRuleIds.AIContextFootprint => "AI-Context-Footprint",
        MetricNames.PublicMemberCount => "Public Member Anzahl",
        _ => metric
    };
}
