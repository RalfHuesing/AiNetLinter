using System.Text;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Output;

/// <summary>
/// Formatiert Regelverstöße als kompakte, token-effiziente Textausgabe für LLM-Agenten.
/// </summary>
public static class ViolationTextFormatter
{
    private const string InstructionLine =
        "Behebe nur die gelisteten Verstoesse. Minimaler Diff - kein Refactoring ausserhalb betroffener Stellen/Zeilen.";

    /// <summary>
    /// Erzeugt die vollständige Textausgabe inklusive LLM-Anweisungsheader und sortierter Verstoßliste.
    /// </summary>
    public static string Format(
        IReadOnlyCollection<RuleViolation> violations,
        string outputRoot,
        LinterConfig? config = null)
    {
        if (violations.Count == 0)
        {
            return string.Empty;
        }

        var byFile = ViolationSummaryBuilder.BuildByFile(violations, outputRoot);
        var byRule = ViolationSummaryBuilder.BuildByRule(violations, config);
        var detailLines = violations
            .OrderBy(v => PathNormalizer.ToRelative(outputRoot, v.FilePath), StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .Select(v => FormatViolationLine(v, outputRoot))
            .ToArray();

        var output = new StringBuilder();
        output.Append($"# AiNetLinter - {violations.Count} violations\n");
        output.Append(InstructionLine);
        output.Append('\n');

        var uniqueRuleNames = violations
            .Select(v => v.RuleName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        foreach (var ruleName in uniqueRuleNames)
        {
            output.Append(GetRuleInstruction(ruleName!)).Append('\n');
        }

        output.Append("\n## Summary - by file\n");
        output.Append(FormatFileSummary(byFile));
        output.Append("\n\n## Summary - by rule\n");
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
        var hasIntent = byRule.Any(x => !string.IsNullOrEmpty(x.Intent));
        if (!hasIntent)
        {
            var lines = new List<string> { "| Rule | Count |", "|------|------:|" };
            lines.AddRange(byRule.Select(x => $"| {x.RuleName} | {x.Count} |"));
            return string.Join('\n', lines);
        }

        var withIntent = new List<string> { "| Rule | Count | Intent |", "|------|------:|--------|" };
        withIntent.AddRange(byRule.Select(x => $"| {x.RuleName} | {x.Count} | {x.Intent} |"));
        return string.Join('\n', withIntent);
    }

    private static string FormatViolationLine(RuleViolation violation, string outputRoot)
    {
        var relativePath = PathNormalizer.ToRelative(outputRoot, violation.FilePath);
        var line = $"{relativePath}:{violation.LineNumber} {violation.RuleName} | {violation.Details}";
        if (!string.IsNullOrWhiteSpace(violation.Guidance))
        {
            line += $" -> {violation.Guidance}";
        }

        return line;
    }

    private static readonly Dictionary<string, string> RuleInstructions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EnforceSealedClasses"] = "-> EnforceSealedClasses: Konkrete Klassen muessen 'sealed' sein. Bei partial Klassen nutze 'sealed partial'. Wenn unmoeglich, nutze '// ainetlinter-disable EnforceSealedClasses' an der betroffenen Zeile. NIEMALS '// ainetlinter-disable all' fuer die ganze Datei verwenden!",
        ["EnforceNoSilentCatch"] = "-> EnforceNoSilentCatch: Exceptions duerfen nicht stumm abgefangen werden. Logge die Exception oder wirf sie mit 'throw;' weiter. Wenn das Abfangen gewollt ist, nutze '// ainetlinter-disable EnforceNoSilentCatch' an der catch-Zeile oder benenne die Exception-Variable 'ignored'. NIEMALS '// ainetlinter-disable all' verwenden!",
        ["MaxLineCount"] = "-> MaxLineCount: Dateizeilenlimit ueberschritten. Teile die Datei in kleinere Klassen oder Vertical Slices auf.",
        ["MaxMethodParameterCount"] = "-> MaxMethodParameterCount: Zu viele Parameter. Kapsle sie in einen C# 'record' (Parameter Object).",
        ["MaxMethodLineCount"] = "-> MaxMethodLineCount: Methode zu lang. Lagere Abschnitte in Hilfsmethoden aus.",
        ["MaxCyclomaticComplexity"] = "-> MaxCyclomaticComplexity: Zu viele Verzweigungen. Teile die Methode auf und reduziere if-Kaskaden.",
        ["MaxCognitiveComplexity"] = "-> MaxCognitiveComplexity: Zu hohe kognitive Last. Benutze Early Returns zur Flachhaltung.",
        ["ForbiddenNamespaceDependency"] = "-> ForbiddenNamespaceDependency: Slice-Abhaengigkeit verletzt. Nutze Abstraktionen oder Events.",
        ["EnforcePascalCase"] = "-> EnforcePascalCase: Benutze PascalCase fuer oeffentliche Bezeichner.",
        ["EnforceXmlDocumentation"] = "-> EnforceXmlDocumentation: Fuege oeffentlichen APIs ein '/// <summary>' XML-Dokument hinzu.",
        ["EnforceSemanticNaming"] = "-> EnforceSemanticNaming: Vermeide generische Namen (data, temp, obj) in oeffentlichen Signaturen.",
        ["EnforceNullableEnable"] = "-> EnforceNullableEnable: Fuege '#nullable enable' am Dateianfang hinzu.",
        ["AllowDynamic"] = "-> AllowDynamic: 'dynamic' ist verboten. Nutze statische Typisierung oder Interfaces.",
        ["AllowOutParameters"] = "-> AllowOutParameters: 'out'-Parameter sind verboten. Benutze Tuples oder Records fuer mehrere Rueckgabewerte.",
        ["StaticTestSentinel"] = "-> StaticTestSentinel: Fehlende Testabdeckung fuer komplexe Klasse. Schreibe einen Unit-Test."
    };

    private static string GetRuleInstruction(string ruleName)
    {
        if (RuleInstructions.TryGetValue(ruleName, out var instruction))
        {
            return instruction;
        }
        return $"-> {ruleName}: Bitte behebe diesen Verstoss gemaess den Richtlinien.";
    }
}
