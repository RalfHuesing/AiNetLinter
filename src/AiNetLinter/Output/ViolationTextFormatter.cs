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

        var lines = violations
            .OrderBy(v => PathNormalizer.ToRelative(outputRoot, v.FilePath), StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .Select(v => FormatViolationLine(v, outputRoot))
            .ToArray();

        return $"# AiNetLinter · {violations.Count} violations\n{InstructionLine}\n\n{string.Join('\n', lines)}";
    }

    private static string FormatViolationLine(RuleViolation violation, string outputRoot)
    {
        var relativePath = PathNormalizer.ToRelative(outputRoot, violation.FilePath);
        return $"{relativePath}:{violation.LineNumber} {violation.RuleName} | {violation.Details}";
    }
}
