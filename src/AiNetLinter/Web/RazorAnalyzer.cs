#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter.Web;

/// <summary>
/// Analysiert Razor/Blazor-Komponenten-Markup auf AI-spezifische Probleme: Dateigroesse,
/// Markup-Verschachtelung, Control-Flow-Komplexitaet, Inline-Lambdas und Ternary-Ausdruecke.
/// Implementiert die Regeln aus Research/Extend-Web-Features/03_Razor_Linting.md Phase 3.
///
/// Implementierungs-Ansatz: rein textbasiert. Die Regeln basieren auf Pattern-Counting
/// (Anzahl Bloecke, Anzahl Verschachtelungen, Anzahl Attribute), nicht auf AST-Semantik.
/// Ein dedizierter Razor-Parser (Microsoft.AspNetCore.Razor.Language) ist fuer diese
/// Pattern-Checks nicht erforderlich — der textbasierte Ansatz ist robuster, schneller
/// und hat keine externen Abhaengigkeiten. C#-Logik innerhalb von @code/@if/@foreach
/// wird nicht analysiert (das ist Aufgabe der Code-Behind-Datei .razor.cs).
/// </summary>
internal static partial class RazorAnalyzer
{
    /// <summary>
    /// Void HTML-Elemente (kein Closing-Tag erforderlich). Zaehlen nicht zur Verschachtelungstiefe.
    /// </summary>
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr",
    };

    private static readonly Regex OpeningTagPattern = new(
        @"<([a-zA-Z][a-zA-Z0-9\-]*)([^>]*?)(/?)>",
        RegexOptions.Compiled);

    private static readonly Regex ClosingTagPattern = new(
        @"</([a-zA-Z][a-zA-Z0-9\-]*)\s*>",
        RegexOptions.Compiled);

    private static readonly Regex RazorCommentPattern = new(
        @"@\*[\s\S]*?\*@|<!--[\s\S]*?-->",
        RegexOptions.Compiled);

    private static readonly Regex CodeBlockStartPattern = new(
        @"@code\s*\{",
        RegexOptions.Compiled);

    private static readonly Regex ControlFlowPattern = new(
        @"@(if|else\s+if|foreach|for|while|switch)\b",
        RegexOptions.Compiled);

    private static readonly Regex ForeachPattern = new(
        @"@foreach\b",
        RegexOptions.Compiled);

    private static readonly Regex ComponentTagPattern = new(
        @"<([A-Z][a-zA-Z0-9.]*)([^>]*?)/?\s*>",
        RegexOptions.Compiled);

    private static readonly Regex InlineTernaryPattern = new(
        @"@\(\s*[^()]*?\?[^():]*:[^()]*\)",
        RegexOptions.Compiled);

    private static readonly Regex AttributePattern = new(
        @"\s+@?[a-zA-Z_][a-zA-Z0-9_.\-]*(:[a-zA-Z_][a-zA-Z0-9_.\-]*)?\s*=",
        RegexOptions.Compiled);

    private static readonly Regex AttributeWithValuePattern = new(
        @"@(\w[\w\-]*)\s*=\s*(?:""([^""]*?)""|'([^']*?)')",
        RegexOptions.Compiled);

    /// <summary>
    /// Analysiert den Razor-Quelltext und liefert alle Regelverstoesse.
    /// </summary>
    public static IReadOnlyList<RuleViolation> Analyze(
        string razorContent, string filePath, RazorConfig config)
    {
        var violations = new List<RuleViolation>();

        if (string.IsNullOrEmpty(razorContent))
        {
            return violations;
        }

        CheckMaxRazorLineCount(razorContent, filePath, config, violations);
        CheckMaxRazorCodeBlockLines(razorContent, filePath, config, violations);
        CheckMaxMarkupNestingDepth(razorContent, filePath, config, violations);
        CheckBanInlineEventLambdas(razorContent, filePath, config, violations);
        CheckMaxControlFlowBlocks(razorContent, filePath, config, violations);
        CheckMaxForeachNestingDepth(razorContent, filePath, config, violations);
        CheckMaxComponentParameterCount(razorContent, filePath, config, violations);
        CheckBanInlineTernaryInAttributes(razorContent, filePath, config, violations);

        return violations;
    }

    private static void CheckMaxRazorLineCount(
        string content, string filePath, RazorConfig config, List<RuleViolation> violations)
    {
        if (config.MaxRazorLineCount <= 0) return;
        var lineCount = CountLines(content);
        if (lineCount <= config.MaxRazorLineCount) return;

        violations.Add(new RuleViolation
        {
            FilePath = filePath,
            LineNumber = 1,
            RuleName = LinterRuleIds.RAZOR_MaxRazorLineCount,
            Details = $"Razor-Datei hat {lineCount} Zeilen (erlaubt: {config.MaxRazorLineCount}).",
            Guidance = "Extrahiere eigenstaendige UI-Bereiche in separate Blazor-Komponenten. " +
                "Hintergrund: Lange Razor-Dateien uebersteigen das Kontextfenster und fuehren " +
                "zu 'Lost in the Middle'-Fehlern bei KI-Diffs.",
        });
    }

    private static void CheckMaxRazorCodeBlockLines(
        string content, string filePath, RazorConfig config, List<RuleViolation> violations)
    {
        if (config.MaxRazorCodeBlockLines <= 0) return;

        foreach (Match match in CodeBlockStartPattern.Matches(content))
        {
            var braceStart = match.Index + match.Length - 1;
            var braceEnd = FindMatchingBrace(content, braceStart);
            if (braceEnd < 0) continue;

            var lineSpan = GetLineRange(content, match.Index, braceEnd);
            var lineCount = lineSpan.EndLine - lineSpan.StartLine + 1;
            if (lineCount > config.MaxRazorCodeBlockLines)
            {
                var componentName = ExtractComponentNameFromPath(filePath);
                var codeBehindHint = string.IsNullOrEmpty(componentName)
                    ? "Verschiebe die Logik in die Code-Behind-Datei '.razor.cs' (partial class)."
                    : $"Verschiebe die Logik in die Code-Behind-Datei '{componentName}.razor.cs' (partial class).";

                violations.Add(new RuleViolation
                {
                    FilePath = filePath,
                    LineNumber = GetLineNumber(content, match.Index),
                    RuleName = LinterRuleIds.RAZOR_MaxRazorCodeBlockLines,
                    Details = $"@code-Block hat {lineCount} Zeilen (erlaubt: {config.MaxRazorCodeBlockLines}).",
                    Guidance = codeBehindHint,
                });
            }
        }
    }

    private static void CheckMaxMarkupNestingDepth(
        string content, string filePath, RazorConfig config, List<RuleViolation> violations)
    {
        if (config.MaxMarkupNestingDepth <= 0) return;

        var sanitized = StripComments(content);
        var maxDepth = ComputeMaxTagNestingDepth(sanitized);
        if (maxDepth > config.MaxMarkupNestingDepth)
        {
            violations.Add(new RuleViolation
            {
                FilePath = filePath,
                LineNumber = 1,
                RuleName = LinterRuleIds.RAZOR_MaxMarkupNestingDepth,
                Details = $"HTML-Verschachtelungstiefe betraegt {maxDepth} Ebenen " +
                    $"(erlaubt: {config.MaxMarkupNestingDepth}).",
                Guidance = "Extrahiere innere Bereiche in eigenstaendige Blazor-Komponenten. " +
                    "Hintergrund: Tiefe Strukturen fuehren bei KIs zu Tag-Mismatch-Halluzinationen.",
            });
        }
    }

    private static void CheckBanInlineEventLambdas(
        string content, string filePath, RazorConfig config, List<RuleViolation> violations)
    {
        if (!config.BanInlineEventLambdas) return;

        foreach (Match match in AttributeWithValuePattern.Matches(content))
        {
            var attributeName = match.Groups[1].Value;
            if (!IsEventOrBindingAttribute(attributeName)) continue;

            var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            if (!IsComplexLambda(value)) continue;

            violations.Add(new RuleViolation
            {
                FilePath = filePath,
                LineNumber = GetLineNumber(content, match.Index),
                RuleName = LinterRuleIds.RAZOR_BanInlineEventLambdas,
                Details = $"Inline-Event-Lambda in '@{attributeName}' ist zu komplex.",
                Guidance = "Extrahiere die Logik in eine Methode in der Code-Behind-Datei " +
                    "('@onclick=\"HandleClick\"' statt '@onclick=\"() => { ... }\"').",
            });
        }
    }

    private static void CheckMaxControlFlowBlocks(
        string content, string filePath, RazorConfig config, List<RuleViolation> violations)
    {
        if (config.MaxControlFlowBlocks <= 0) return;

        var count = ControlFlowPattern.Matches(content).Count;
        if (count > config.MaxControlFlowBlocks)
        {
            violations.Add(new RuleViolation
            {
                FilePath = filePath,
                LineNumber = 1,
                RuleName = LinterRuleIds.RAZOR_MaxControlFlowBlocks,
                Details = $"Datei enthaelt {count} Control-Flow-Bloecke " +
                    $"(erlaubt: {config.MaxControlFlowBlocks}).",
                Guidance = "Extrahiere Teilbereiche in eigenstaendige Komponenten mit klar " +
                    "definierten Eingabe-Parametern.",
            });
        }
    }

    private static void CheckMaxForeachNestingDepth(
        string content, string filePath, RazorConfig config, List<RuleViolation> violations)
    {
        if (config.MaxForeachNestingDepth <= 0) return;

        var maxDepth = ComputeMaxForeachNestingDepth(content);
        if (maxDepth > config.MaxForeachNestingDepth)
        {
            violations.Add(new RuleViolation
            {
                FilePath = filePath,
                LineNumber = 1,
                RuleName = LinterRuleIds.RAZOR_MaxForeachNestingDepth,
                Details = $"@foreach-Verschachtelungstiefe betraegt {maxDepth} Ebenen " +
                    $"(erlaubt: {config.MaxForeachNestingDepth}).",
                Guidance = "Extrahiere die innere Schleife in eine Kind-Komponente.",
            });
        }
    }

    private static void CheckMaxComponentParameterCount(
        string content, string filePath, RazorConfig config, List<RuleViolation> violations)
    {
        if (config.MaxComponentParameterCount <= 0) return;

        foreach (Match match in ComponentTagPattern.Matches(content))
        {
            var tagName = match.Groups[1].Value;
            var attrs = match.Groups[2].Value;
            var attrCount = CountAttributes(attrs);
            if (attrCount > config.MaxComponentParameterCount)
            {
                violations.Add(new RuleViolation
                {
                    FilePath = filePath,
                    LineNumber = GetLineNumber(content, match.Index),
                    RuleName = LinterRuleIds.RAZOR_MaxComponentParameterCount,
                    Details = $"Komponentenaufruf '<{tagName}>' hat {attrCount} Parameter " +
                        $"(erlaubt: {config.MaxComponentParameterCount}).",
                    Guidance = "Fasse verwandte Parameter in ein Parameter-Objekt zusammen oder " +
                        "reduziere die oeffentliche API der Komponente.",
                });
            }
        }
    }

    private static void CheckBanInlineTernaryInAttributes(
        string content, string filePath, RazorConfig config, List<RuleViolation> violations)
    {
        if (!config.BanInlineTernaryInAttributes) return;

        foreach (Match match in InlineTernaryPattern.Matches(content))
        {
            violations.Add(new RuleViolation
            {
                FilePath = filePath,
                LineNumber = GetLineNumber(content, match.Index),
                RuleName = LinterRuleIds.RAZOR_BanInlineTernaryInAttributes,
                Details = "Ternary-Ausdruck im Attributwert gefunden.",
                Guidance = "Berechne den Wert in einer Property der Code-Behind-Datei " +
                    "('class=\"@CssClass\"' statt 'class=\"@(flag ? a : b)\"').",
            });
        }
    }
}