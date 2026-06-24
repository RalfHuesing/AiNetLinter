#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using Esprima;
using Esprima.Ast;

namespace AiNetLinter.Web;

/// <summary>
/// Analysiert JavaScript-Inhalte auf Zeilenlimit, ES6-Modul-Verwendung und Window-Pollution.
/// Verwendet Esprima (BSD-3-Clause-Lizenz) als standardkonformer ECMAScript-Parser.
/// Implementiert die Regeln aus Research/Extend-Web-Features/02_JS_Linting.md Phase 2.
/// </summary>
internal static class JsAnalyzer
{
    /// <summary>
    /// Analysiert JavaScript-Quelltext und liefert alle Regelverstoesse fuer die drei JS-Regeln.
    /// </summary>
    /// <param name="jsContent">Roher JavaScript-Quelltext.</param>
    /// <param name="filePath">Absoluter Pfad zur JS-Datei (fuer Violation-Metadata).</param>
    /// <param name="config">Aktuelle effektive JsConfig (bereits mit ProjectOverride aufgeloest).</param>
    /// <returns>Liste der Regelverstoesse; nie null, ggf. leer.</returns>
    public static IReadOnlyList<RuleViolation> Analyze(string jsContent, string filePath, JsConfig config)
    {
        var violations = new List<RuleViolation>();

        if (string.IsNullOrEmpty(jsContent))
        {
            return violations;
        }

        CheckMaxJsLineCount(jsContent, filePath, config, violations);

        var body = TryParse(jsContent, filePath, violations, out var isModule, out var parsed);
        if (!parsed)
        {
            // Syntax-Fehler wurde bereits gemeldet (TryParse fuegt JS_SyntaxError ein).
            return violations;
        }

        if (config.EnforceJsModules)
        {
            CheckEnforceJsModules(body, isModule, filePath, violations);
        }

        return violations;
    }

    private static void CheckMaxJsLineCount(
        string jsContent, string filePath, JsConfig config, List<RuleViolation> violations)
    {
        if (config.MaxJsLineCount <= 0) return;

        var lineCount = CountLines(jsContent);
        if (lineCount <= config.MaxJsLineCount) return;

        violations.Add(CreateViolation(
            filePath,
            "JS_MaxJsLineCount",
            $"JavaScript-Datei hat {lineCount} Zeilen (erlaubt: {config.MaxJsLineCount}). " +
            "Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.",
            "JavaScript in Blazor-Interop-Dateien minimal halten. " +
            "Groessere Logik in C# verschieben (Methoden im IJSObjectReference-Handler). " +
            "Hintergrund: Lange JS-Dateien uebersteigen das Kontextfenster und fuehren zu " +
            "'Lost in the Middle'-Fehlern bei KI-Diffs."));
    }

    /// <summary>
    /// Versucht ParseModule zuerst (Blazor-Interop-Dateien sind grundsaetzlich ES6-Module);
    /// faellt auf ParseScript zurueck, wenn die Datei kein Modul-strict parsebares Programm ist.
    /// Setzt parsed=false und meldet JS_SyntaxError wenn beide Versuche scheitern.
    /// Eine Datei gilt nur dann als Modul, wenn ParseModule gelingt UND der Body
    /// mindestens eine Export- oder Import-Deklaration enthaelt. (Esprima 3.x parst
    /// Skript-Code ebenfalls als Modul, sofern keine Modul-Features verwendet werden.)
    /// </summary>
    private static NodeList<Statement> TryParse(
        string jsContent,
        string filePath,
        List<RuleViolation> violations,
        out bool isModule,
        out bool parsed)
    {
        var parser = new JavaScriptParser();

        try
        {
            var module = parser.ParseModule(jsContent);
            isModule = ContainsModuleDeclaration(module.Body);
            parsed = true;
            return module.Body;
        }
        catch (ParserException)
        {
            // Kein strict-mode-Modul-Code — als klassisches Script versuchen.
            // Bewusst leer: Esprima wirft ParserException, wenn der Quelltext kein
            // valides Modul ist. Das ist erwartetes Verhalten und kein Fehler.
            isModule = false;
        }

        try
        {
            var script = parser.ParseScript(jsContent);
            isModule = false;
            parsed = true;
            return script.Body;
        }
        catch (ParserException ex)
        {
            violations.Add(CreateSyntaxErrorViolation(filePath, ex));
            isModule = false;
            parsed = false;
            return default;
        }
    }

    /// <summary>
    /// Prueft, ob der Body mindestens eine Import- oder Export-Deklaration enthaelt.
    /// Nur dann gilt die Datei semantisch als ES6-Modul.
    /// </summary>
    private static bool ContainsModuleDeclaration(NodeList<Statement> body)
    {
        foreach (var statement in body)
        {
            if (statement is ImportDeclaration) return true;
            if (statement is ExportNamedDeclaration) return true;
            if (statement is ExportDefaultDeclaration) return true;
            if (statement is ExportAllDeclaration) return true;
        }
        return false;
    }

    private static void CheckEnforceJsModules(
        NodeList<Statement> body,
        bool isModule,
        string filePath,
        List<RuleViolation> violations)
    {
        if (!isModule)
        {
            violations.Add(CreateViolation(
                filePath,
                "JS_EnforceJsModules",
                "JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein " +
                "(mindestens ein 'export'-Statement). " +
                "Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen " +
                "nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.",
                "Fuege ein 'export'-Statement hinzu (z. B. 'export function myHelper() { ... }' " +
                "oder 'export { myHelper };'). Import in Blazor via " +
                "'await JSRuntime.InvokeAsync<IJSObjectReference>(\"import\", \"./myModule.js\")'."));
            return;
        }

        // ES6-Modul: Auf window.*-Zuweisungen pruefen.
        CheckWindowPollution(body, filePath, violations);
    }

    private static void CheckWindowPollution(
        NodeList<Statement> body, string filePath, List<RuleViolation> violations)
    {
        foreach (var statement in body)
        {
            if (statement is not ExpressionStatement expr) continue;
            if (expr.Expression is not AssignmentExpression assign) continue;
            if (assign.Left is not MemberExpression member) continue;
            if (member.Object is not Identifier id) continue;
            if (!string.Equals(id.Name, "window", StringComparison.Ordinal)) continue;

            violations.Add(CreateViolation(
                filePath,
                "JS_EnforceJsModules",
                "Zuweisungen an das globale 'window'-Objekt sind verboten. " +
                "Nutze stattdessen exportierte Funktionen: 'export function myFunc() {...}' " +
                "und importiere via 'await JSRuntime.InvokeAsync(\"imports\", ...)'.",
                "Statt 'window.myFunc = ...' ein ES6-Export verwenden " +
                "('export function myFunc() { ... }'). Hintergrund: Globales window-Pollution " +
                "umgeht die Modul-Isolation und erzeugt unvorhersehbare Seiteneffekte bei " +
                "KI-Edits, die andere Skripte beeintraechtigen koennen."));
        }
    }

    private static RuleViolation CreateSyntaxErrorViolation(string filePath, ParserException ex) =>
        new RuleViolation
        {
            FilePath = filePath,
            LineNumber = ex.LineNumber,
            RuleName = "JS_SyntaxError",
            Details = $"Syntax-Fehler in JavaScript: {ex.Description}",
            Guidance = "Korrigiere den Syntaxfehler im JavaScript (z. B. fehlende Klammern, " +
                "ungueltige Statements). Nach Korrektur wird die volle Analyse ausgefuehrt.",
        };

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
