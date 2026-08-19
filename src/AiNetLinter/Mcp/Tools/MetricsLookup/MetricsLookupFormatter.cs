#nullable enable

using System.Text;
using AiNetLinter.Core;

namespace AiNetLinter.Mcp.Tools.MetricsLookup;

/// <summary>
/// Formatiert das Ergebnis von <see cref="MetricsLookupResultDto"/> in lesbares Markdown.
/// </summary>
internal static class MetricsLookupFormatter
{
    internal static string Format(MetricsLookupResultDto dto)
    {
        var sb = new StringBuilder();

        sb.Append("### ").Append(dto.SymbolKind).Append(": ").Append(dto.QualifiedName).AppendLine();
        sb.AppendLine();

        if (dto.Location != null)
        {
            sb.Append("- **Ort:** `").Append(dto.Location.FilePath)
              .Append(':').Append(dto.Location.StartLine).Append('-').Append(dto.Location.EndLine).AppendLine("`");
        }

        if (!string.IsNullOrEmpty(dto.DocCommentId))
        {
            sb.Append("- **Id:** `").Append(dto.DocCommentId).AppendLine("`");
        }

        sb.AppendLine();

        if (dto.ThresholdChecks.Count > 0)
        {
            sb.AppendLine("#### Schwellwert-Abgleich & Metriken");
            sb.AppendLine();
            sb.AppendLine("| Metrik | Wert | Grenzwert | Status | Regel |");
            sb.AppendLine("|:---|---:|---:|:---:|:---|");

            foreach (var check in dto.ThresholdChecks)
            {
                var limitStr = check.Limit > 0 ? $"<= {check.Limit}" : "-";
                var statusBadge = $"[{check.Status}]";
                var ruleStr = !string.IsNullOrEmpty(check.RuleId) ? check.RuleId : "-";

                sb.Append("| ").Append(FormatMetricDisplayName(check.Metric))
                  .Append(" | ").Append(check.Value)
                  .Append(" | ").Append(limitStr)
                  .Append(" | ").Append(statusBadge)
                  .Append(" | ").Append(ruleStr)
                  .AppendLine(" |");
            }

            sb.AppendLine();
        }

        if (dto.MethodMetrics != null)
        {
            FormatMethodDetails(sb, dto.MethodMetrics);
        }
        else if (dto.TypeMetrics != null)
        {
            FormatTypeDetails(sb, dto.TypeMetrics);
        }
        else if (dto.PropertyMetrics != null)
        {
            FormatPropertyDetails(sb, dto.PropertyMetrics);
        }

        return sb.ToString().TrimEnd();
    }

    private static void FormatMethodDetails(StringBuilder sb, MethodMetricsDto method)
    {
        if (method.IgnoredParameters.Count > 0)
        {
            sb.Append("**Ignorierte Parameter (vom Zählen ausgenommen):** ")
              .Append(string.Join(", ", method.IgnoredParameters))
              .AppendLine();
            sb.AppendLine();
        }
    }

    private static void FormatTypeDetails(StringBuilder sb, TypeMetricsDto type)
    {
        sb.AppendLine("#### Typ-Struktur");
        sb.AppendLine();
        sb.Append("- **Code-Zeilen (LOC):** ").Append(type.CodeLines).AppendLine();
        sb.Append("- **AI-Context-Footprint:** ").Append(type.AiContextFootprint).Append(" Zeilen").AppendLine();
        sb.Append("- **Members:** ").Append(type.TotalMemberCount).Append(" gesamt (")
          .Append(type.PublicMemberCount).Append(" public, ")
          .Append(type.MethodCount).Append(" Methoden, ")
          .Append(type.PropertyCount).Append(" Properties)")
          .AppendLine();
        sb.AppendLine();

        if (type.TopDependencies.Count > 0)
        {
            sb.AppendLine("**Top-Abhängigkeiten (AI-Context-Footprint):**");
            foreach (var dep in type.TopDependencies)
            {
                sb.Append("- `").Append(dep.Name).Append("`: ").Append(dep.Lines).AppendLine(" Zeilen");
            }
            sb.AppendLine();
        }
    }

    private static void FormatPropertyDetails(StringBuilder sb, PropertyMetricsDto prop)
    {
        sb.AppendLine("#### Property-Details");
        sb.AppendLine();
        sb.Append("- **Code-Zeilen (LOC):** ").Append(prop.CodeLines).AppendLine();
        sb.Append("- **Zyklomatische Komplexität:** ").Append(prop.CyclomaticComplexity).AppendLine();
        sb.Append("- **Kognitive Komplexität:** ").Append(prop.CognitiveComplexity).AppendLine();
        var accessors = (prop.HasGetter, prop.HasSetter) switch
        {
            (true, true) => "Getter & Setter",
            (true, false) => "Nur Getter",
            (false, true) => "Nur Setter",
            _ => "Keine expliziten Accessoren"
        };
        sb.Append("- **Accessoren:** ").Append(accessors).AppendLine();
        sb.AppendLine();
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
