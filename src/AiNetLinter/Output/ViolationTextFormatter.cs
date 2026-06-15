using System.Text;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Output;

/// <summary>
/// Formatiert Regelverstöße als kompakte, token-effiziente Textausgabe für LLM-Agenten.
/// </summary>
public static class ViolationTextFormatter
{
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
        output.Append(BuildInstructionBlock(outputRoot));

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

    private static string BuildInstructionBlock(string projectRoot)
    {
        var sb = new StringBuilder();
        sb.Append("\n## Handlungsanweisung\n\n");
        sb.Append("Analysiere die Violations im Kontext der Architektur und Coding-Richtlinien dieses Projekts.\n");

        var cursorRulesPath = Path.Combine(projectRoot, ".cursor", "rules");
        var claudeMdPath = Path.Combine(projectRoot, "CLAUDE.md");

        if (Directory.Exists(cursorRulesPath))
            sb.Append("Projektkonfiguration erkannt: `.cursor/rules` — Architektur-Constraints und Regeln dort beachten.\n");
        if (File.Exists(claudeMdPath))
            sb.Append("Projektkonfiguration erkannt: `CLAUDE.md` — Architektur-Constraints dort beachten.\n");

        sb.Append("\n**Schritt 1 — False-Positive-Prüfung (PFLICHT vor jeder Änderung)**\n");
        sb.Append("Prüfe für jede Violation: Ist das ein echter Verstoß oder ein False-Positive, der durch die Architektur des Projekts gerechtfertigt ist?\n");
        sb.Append("Konfigurationsoptionen erkunden:\n");

        var exePath = Environment.ProcessPath ?? "ainetlinter";
        sb.Append($"  `{exePath} --readme`\n");

        sb.Append("Bei vermutetem False-Positive: Nutzer explizit informieren, Optionen mit Empfehlung nennen, Einverständnis einholen — BEVOR du etwas änderst.\n");

        sb.Append("\n**Schritt 2 — Behebung echter Violations**\n");
        sb.Append("Minimal und präzise. Kein Refactoring außerhalb betroffener Zeilen.\n");
        sb.Append("Reihenfolge: Code-Fix → Konfigurationsanpassung → Suppression-Kommentar (letztes Mittel, nur nach Nutzer-Freigabe).\n\n");

        return sb.ToString();
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
        ["StaticTestSentinel"] = "-> StaticTestSentinel: Fehlende Testabdeckung fuer komplexe Klasse. Schreibe einen Unit-Test.",
        ["EnforceExplicitStateImmutability"] = "-> EnforceExplicitStateImmutability: Vermeide veraenderlichen Zustand. Verwende 'readonly' Felder, 'init'-only Properties oder 'record' Typen.",
        ["EnforceStrictBoundaryForBusinessLogic"] = "-> EnforceStrictBoundaryForBusinessLogic: Business-Logic-Klassen (z.B. Calculator, Rule) duerfen keine I/O-Operationen oder Datenbankzugriffe durchfuehren. Kapsle diese in Interfaces.",
        ["PreventContextDependentOverloads"] = "-> PreventContextDependentOverloads: Zu viele Methodenueberladungen mit identischer Parameteranzahl, die sich nur durch primitive Typen unterscheiden. Verwende explizite Methodennamen.",
        ["RequireExplicitTruncationHandling"] = "-> RequireExplicitTruncationHandling: Zeichenketten-Abschneiden (Truncation) muss explizit behandelt werden, um Datenverlust zu vermeiden.",
        ["EnforceNamespaceDirectoryMapping"] = "-> EnforceNamespaceDirectoryMapping: Der Namespace der Datei muss dem Pfad im Dateisystem entsprechen.",
        ["DetectAndBanPhantomDependencies"] = "-> DetectAndBanPhantomDependencies: Banned dependencies detected (Phantom-Abhaengigkeiten). Nutze nur explizit erlaubte Namespace-Pfade.",
        ["EnforceNoMagicValues"] = "-> EnforceNoMagicValues: Vermeide magische Literale. Deklariere Konstanten ('const' oder 'static readonly') oder benutze Enums.",
        ["EnforceNoVariableShadowing"] = "-> EnforceNoVariableShadowing: Shadowing von Variablen/Parametern verboten. Benenne lokale Variablen eindeutig.",
        ["EnforceReadonlyParameters"] = "-> EnforceReadonlyParameters: Parameter sind schreibgeschuetzt und duerfen innerhalb der Methode nicht neu zugewiesen werden.",
        ["EnforceReadonlyFields"] = "-> EnforceReadonlyFields: Private Felder, die nur im Konstruktor zugewiesen werden, muessen als 'readonly' deklariert sein.",
        ["MaxDirectoryDepth"] = "-> MaxDirectoryDepth: Die Verzeichnistiefe des Projekts ueberschreitet das erlaubte Maximum.",
        ["MaxInheritanceDepth"] = "-> MaxInheritanceDepth: Zu tiefe Vererbungshierarchie. Bevorzuge Komposition statt Vererbung.",
        ["MaxAIContextFootprint"] = "-> MaxAIContextFootprint: Der transitive Code-Footprint fuer KI-Agenten ist zu gross. Reduziere Kopplung und Abhaengigkeiten.",
        ["MaxMethodOverloads"] = "-> MaxMethodOverloads: Zu viele Methodenueberladungen. Verwende verschiedene Methodennamen oder optionale Parameter.",
        ["MaxConstructorDependencies"] = "-> MaxConstructorDependencies: Zu viele Konstruktor-Abhaengigkeiten. Teile die Klasse auf oder nutze ein Parameter-Objekt."
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
