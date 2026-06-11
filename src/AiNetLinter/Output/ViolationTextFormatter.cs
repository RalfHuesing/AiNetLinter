using System.Text;
using AiNetLinter.Models;

namespace AiNetLinter.Output;

/// <summary>
/// Formatiert Regelverstöße als kompakte, token-effiziente Textausgabe für LLM-Agenten.
/// </summary>
public static class ViolationTextFormatter
{
    private const string InstructionLine =
        "Behebe nur die gelisteten Verstöße. Minimaler Diff — kein Refactoring ausserhalb betroffener Stellen/Zeilen.";

    /// <summary>
    /// Erzeugt die vollständige Textausgabe inklusive LLM-Anweisungsheader und sortierter Verstoßliste.
    /// </summary>
    public static string Format(IReadOnlyCollection<RuleViolation> violations, string outputRoot)
    {
        if (violations.Count == 0)
        {
            return string.Empty;
        }

        var byFile = ViolationSummaryBuilder.BuildByFile(violations, outputRoot);
        var byRule = ViolationSummaryBuilder.BuildByRule(violations);
        var detailLines = violations
            .OrderBy(v => PathNormalizer.ToRelative(outputRoot, v.FilePath), StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .Select(v => FormatViolationLine(v, outputRoot))
            .ToArray();

        var output = new StringBuilder();
        output.Append($"# AiNetLinter · {violations.Count} violations\n");
        output.Append(InstructionLine);
        output.Append("\n\n## Summary · by file\n");
        output.Append(FormatFileSummary(byFile));
        output.Append("\n\n## Summary · by rule\n");
        output.Append(FormatRuleSummary(byRule));
        output.Append("\n\n## Violations\n");
        output.Append(string.Join('\n', detailLines));
        return output.ToString();
    }

    private static string FormatFileSummary(IReadOnlyList<FileViolationCount> byFile)
    {
        return string.Join('\n', byFile.Select(x => $"{x.Count} {x.RelativePath}"));
    }

    private static string FormatRuleSummary(IReadOnlyList<RuleViolationCount> byRule)
    {
        var lines = new List<string> { "| Rule | Count |", "|------|------:|" };
        lines.AddRange(byRule.Select(x => $"| {x.RuleName} | {x.Count} |"));
        return string.Join('\n', lines);
    }

    private static string FormatViolationLine(RuleViolation violation, string outputRoot)
    {
        var relativePath = PathNormalizer.ToRelative(outputRoot, violation.FilePath);
        return $"{relativePath}:{violation.LineNumber} {violation.RuleName} | {violation.Details}";
    }
}
