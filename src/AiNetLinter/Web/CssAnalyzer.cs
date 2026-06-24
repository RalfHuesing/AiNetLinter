#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using ExCSS;

namespace AiNetLinter.Web;

/// <summary>
/// Analysiert CSS-Inhalte auf Groesse, Selektor-Komplexitaet und Scoped-CSS-Verwendung.
/// Verwendet ExCSS als reines CSS-Parsing-Backend (MIT-Lizenz) — kein Code-Generator, nur AST-Walk.
/// Implementiert die Regeln aus Research/Extend-Web-Features/01_CSS_Linting.md Phase 1.
/// </summary>
internal static class CssAnalyzer
{
    /// <summary>
    /// Analysiert CSS-Inhalt und liefert alle Regelverstoesse fuer die drei CSS-Regeln.
    /// </summary>
    /// <param name="cssContent">Roher CSS-Quelltext.</param>
    /// <param name="filePath">Absoluter Pfad zur CSS-Datei (fuer Violation-Metadata).</param>
    /// <param name="config">Aktuelle effektive CssConfig (bereits mit ProjectOverride aufgeloest).</param>
    /// <returns>Liste der Regelverstoesse; nie null, ggf. leer.</returns>
    public static IReadOnlyList<RuleViolation> Analyze(string cssContent, string filePath, CssConfig config)
    {
        var violations = new List<RuleViolation>();

        if (string.IsNullOrEmpty(cssContent))
        {
            return violations;
        }

        CheckMaxCssLineCount(cssContent, filePath, config, violations);

        var isScopedCss = filePath.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase);
        if (!NeedsParser(config, isScopedCss))
        {
            return violations;
        }

        Stylesheet? stylesheet;
        try
        {
            stylesheet = new StylesheetParser().Parse(cssContent);
        }
        catch (Exception ex)
        {
            violations.Add(CreateParseErrorViolation(filePath, ex));
            return violations;
        }

        if (config.PreferScopedCss && !isScopedCss)
        {
            CheckPreferScopedCss(stylesheet, filePath, config, violations);
        }

        if (config.MaxCssSelectorComplexity > 0)
        {
            CheckSelectorComplexity(stylesheet, filePath, config.MaxCssSelectorComplexity, violations);
        }

        return violations;
    }

    private static void CheckMaxCssLineCount(
        string cssContent, string filePath, CssConfig config, List<RuleViolation> violations)
    {
        if (config.MaxCssLineCount <= 0) return;

        var lineCount = CountLines(cssContent);
        if (lineCount <= config.MaxCssLineCount) return;

        violations.Add(CreateViolation(
            filePath,
            "CSS_MaxCssLineCount",
            $"CSS-Datei hat {lineCount} Zeilen (erlaubt: {config.MaxCssLineCount}).",
            "CSS-Datei nach Features aufteilen oder in Scoped CSS (.razor.css) verschieben. " +
            "Hintergrund: Lange Stylesheets uebersteigen das Kontextfenster und fuehren zu " +
            "'Lost in the Middle'-Fehlern bei KI-Diffs."));
    }

    private static bool NeedsParser(CssConfig config, bool isScopedCss) =>
        (config.PreferScopedCss && !isScopedCss) || config.MaxCssSelectorComplexity > 0;

    private static void CheckPreferScopedCss(
        Stylesheet stylesheet, string filePath, CssConfig config, List<RuleViolation> violations)
    {
        var ruleCount = CountStyleRules(stylesheet);
        if (ruleCount <= config.PreferScopedCssMinRuleCount) return;

        violations.Add(CreateViolation(
            filePath,
            "CSS_PreferScopedCss",
            $"Globale CSS-Datei enthaelt {ruleCount} Stil-Regeln " +
            $"(Schwellenwert: {config.PreferScopedCssMinRuleCount}). " +
            "Verschiebe komponentenspezifische Stile in eine '.razor.css'-Scoped-CSS-Datei, " +
            "um den globalen Butterfly-Effekt bei KI-Edits zu eliminieren.",
            "Komponenten-Styles in eine gleichnamige '.razor.css'-Datei extrahieren " +
            "(Blazor scopped automatisch). Globale Dateien sollten nur Resets, " +
            "Custom Properties und Font-Definitionen enthalten."));
    }

    private static void CheckSelectorComplexity(
        Stylesheet stylesheet, string filePath, int maxComplexity, List<RuleViolation> violations)
    {
        foreach (var rule in stylesheet.StyleRules)
        {
            var selectorText = rule.Selector?.Text;
            if (string.IsNullOrEmpty(selectorText)) continue;

            var depth = ComputeMaxSelectorDepth(selectorText);
            if (depth <= maxComplexity) continue;

            violations.Add(CreateViolation(
                filePath,
                "CSS_MaxCssSelectorComplexity",
                $"CSS-Selektor '{Truncate(selectorText, 80)}' ist zu komplex " +
                $"(Tiefe: {depth}, erlaubt: {maxComplexity}).",
                "Nutze Scoped CSS (.razor.css) oder vereinfache den Selektor. " +
                "Verschachtelte Klassen-Kombinationen sind fuer Modelle schwer zuzuordnen — " +
                "ein klar benannter Wurzel-Selektor reduziert Fehlzuordnungen."));
        }
    }

    private static RuleViolation CreateParseErrorViolation(string filePath, Exception ex) =>
        CreateViolation(
            filePath,
            "CSS_ParseError",
            $"CSS-Datei konnte nicht geparst werden: {ex.Message}",
            "Korrigiere den Syntaxfehler im CSS (z. B. fehlende geschweifte Klammern, " +
            "ungueltige Selektor-Syntax). Nach Korrektur wird die volle Analyse ausgefuehrt.");

    private static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        var n = 1;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n') n++;
        }
        return n;
    }

    private static int CountStyleRules(Stylesheet stylesheet)
    {
        var count = 0;
        foreach (var _ in stylesheet.StyleRules) count++;
        return count;
    }

    /// <summary>
    /// Berechnet die maximale Komplexitaet (Anzahl Segmente) einer Selector-List.
    /// Selektor-Segmente werden durch Komma, Whitespace und CSS-Combinators (>, +, ~) getrennt.
    /// </summary>
    internal static int ComputeMaxSelectorDepth(string selectorText)
    {
        var max = 0;
        foreach (var selector in selectorText.Split(','))
        {
            var trimmed = selector.Trim();
            if (trimmed.Length == 0) continue;
            var segments = trimmed.Split(new[] { ' ', '>', '+', '~' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > max)
            {
                max = segments.Length;
            }
        }
        return max;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max] + "…";
    }

    internal static RuleViolation CreateViolation(string filePath, string ruleName, string details, string guidance) =>
        new RuleViolation
        {
            FilePath = filePath,
            LineNumber = 1,
            RuleName = ruleName,
            Details = details,
            Guidance = guidance,
        };
}
