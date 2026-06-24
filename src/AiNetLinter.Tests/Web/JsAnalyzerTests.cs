#nullable enable

using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Web;
using Xunit;

// @covers JsConfig (StaticTestSentinel: Kognitive Komplexitaet 6 > Schwellwert 5; Konfiguration ist ueber diese Tests abgedeckt.)
namespace AiNetLinter.Tests.Web;

/// <summary>
/// Unit-Tests fuer JsAnalyzer. Implementiert die Test-Szenarien A-H aus
/// Research/Extend-Web-Features/02_JS_Linting.md Abschnitt 5.
/// </summary>
public sealed class JsAnalyzerTests
{
    // Szenario A — Sauberes ES6-Modul unter 150 Zeilen mit export function → keine Violations
    [Fact]
    public void Analyze_NoViolations_ForCleanEs6Module()
    {
        const string js = """
            export function showAlert(message) {
                window.alert(message);
            }
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\app.js", config);

        Assert.Empty(violations);
    }

    // Szenario B — JS-Datei mit 200 Zeilen → JS_MaxJsLineCount
    [Fact]
    public void Analyze_ReportsMaxJsLineCount_WhenFileExceedsLimit()
    {
        var lines = Enumerable.Range(1, 200).Select(i => $"export const v{i} = {i};");
        var js = string.Join("\n", lines);
        var config = NewJsConfig(maxLines: 150);

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\huge.js", config);

        Assert.Single(violations);
        Assert.Equal("JS_MaxJsLineCount", violations[0].RuleName);
        Assert.Contains("200", violations[0].Details);
        Assert.Contains("150", violations[0].Details);
    }

    // Szenario C — Script ohne export (kein Modul) → JS_EnforceJsModules (nicht isModule)
    [Fact]
    public void Analyze_ReportsEnforceJsModules_WhenNotAModule()
    {
        const string js = """
            function legacyHelper() {
                return 42;
            }
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\legacy.js", config);

        Assert.Single(violations);
        Assert.Equal("JS_EnforceJsModules", violations[0].RuleName);
    }

    // Szenario D — window-Zuweisung in ES6-Modul → JS_EnforceJsModules (window pollution)
    [Fact]
    public void Analyze_ReportsEnforceJsModules_WhenWindowPollutionInModule()
    {
        const string js = """
            export function init() {}

            window.showAlert = () => {};
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\polluted.js", config);

        Assert.Single(violations);
        Assert.Equal("JS_EnforceJsModules", violations[0].RuleName);
        Assert.Contains("window", violations[0].Details);
    }

    // Szenario E — JsAnalyzer meldet Roh-Violation trotz Suppression-Kommentar.
    // Die Suppression wird eine Ebene hoeher im WebFileSeparationChecker angewandt.
    [Fact]
    public void Analyze_StillReportsViolation_SuppressionIsHandledByChecker()
    {
        const string js = """
            // ainetlinter-disable JS_EnforceJsModules
            window.showAlert = () => {};
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\suppressed.js", config);

        Assert.Single(violations);
        Assert.Equal("JS_EnforceJsModules", violations[0].RuleName);
    }

    // Szenario F — Ungueltiges JS → JS_SyntaxError, kein Crash, korrekte Zeilennummer.
    [Fact]
    public void Analyze_ReportsSyntaxError_ForInvalidJs()
    {
        const string js = "const a = ;";
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\broken.js", config);

        Assert.NotEmpty(violations);
        Assert.Equal("JS_SyntaxError", violations[0].RuleName);
        Assert.Contains("Syntax-Fehler", violations[0].Details);
        Assert.Equal(1, violations[0].LineNumber); // Fehler liegt in Zeile 1
    }

    // Szenario F (Variante) — Syntax-Fehler in Zeile 3 → LineNumber korrekt uebermittelt.
    // Kein export: ParseModule scheitert an Zeile 3, ParseScript ebenfalls an Zeile 3.
    // export-Statements wuerden ParseScript bereits an Zeile 1 scheitern lassen.
    [Fact]
    public void Analyze_ReportsSyntaxError_WithCorrectLineNumber()
    {
        const string js = """
            function a() {}
            function b() {}
            const x = ;
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\broken3.js", config);

        Assert.NotEmpty(violations);
        Assert.Equal("JS_SyntaxError", violations[0].RuleName);
        Assert.Equal(3, violations[0].LineNumber);
    }

    // Szenario G — Datei mit `export` wird von ParseScript abgelehnt, ParseModule gelingt.
    // Folgen: isModule = true, KEINE JS_EnforceJsModules (modul-konform).
    [Fact]
    public void Analyze_AcceptsExport_AsModuleAndSkipsEnforceJsModules()
    {
        const string js = """
            export const VERSION = '1.0';
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\version.js", config);

        Assert.Empty(violations);
    }

    // Szenario H — Legacy-Script ohne export und ohne window-Zuweisung → JS_EnforceJsModules
    [Fact]
    public void Analyze_ReportsEnforceJsModules_ForLegacyScriptWithoutExports()
    {
        const string js = """
            'use strict';
            function plainFunction() {
                return computeSomething();
            }
            function computeSomething() {
                return 1 + 2;
            }
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\plain.js", config);

        Assert.Single(violations);
        Assert.Equal("JS_EnforceJsModules", violations[0].RuleName);
    }

    // Zusatztest — Leere JS-Datei liefert keine Violations
    [Fact]
    public void Analyze_NoViolations_ForEmptyContent()
    {
        var config = NewJsConfig();
        var violations = JsAnalyzer.Analyze("", "C:\\app\\wwwroot\\js\\empty.js", config);
        Assert.Empty(violations);
    }

    // Zusatztest — Null-Content liefert keine Violations
    [Fact]
    public void Analyze_NoViolations_ForNullContent()
    {
        var config = NewJsConfig();
        var violations = JsAnalyzer.Analyze(null!, "C:\\app\\wwwroot\\js\\null.js", config);
        Assert.Empty(violations);
    }

    // Zusatztest — MaxJsLineCount deaktiviert (Limit 0) ueberspringt Pruefung
    [Fact]
    public void Analyze_NoMaxJsLineCount_WhenLimitIsZero()
    {
        var lines = Enumerable.Range(1, 200).Select(i => $"export const v{i} = {i};");
        var js = string.Join("\n", lines);
        var config = NewJsConfig(maxLines: 0);

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\huge.js", config);

        Assert.DoesNotContain(violations, v => v.RuleName == "JS_MaxJsLineCount");
    }

    // Zusatztest — EnforceJsModules deaktiviert unterdrueckt Modul-Check
    [Fact]
    public void Analyze_NoEnforceJsModules_WhenDisabled()
    {
        const string js = """
            function legacy() {
                return 42;
            }
            """;
        var config = NewJsConfig(enforceModules: false);

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\legacy.js", config);

        Assert.Empty(violations);
    }

    // Zusatztest — EnforceJsModules aktiv, aber Script-Datei OHNE window-Zuweisung:
    // Es wird NUR die "nicht-Modul"-Violation gemeldet (kein Window-Pollution-Check, da file kein Modul).
    [Fact]
    public void Analyze_ReportsOnlyMissingModule_ForNonModuleScriptWithoutWindow()
    {
        const string js = """
            function plainHelper() {
                return 'no exports, no window';
            }
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\plain.js", config);

        Assert.Single(violations);
        Assert.Equal("JS_EnforceJsModules", violations[0].RuleName);
        Assert.Contains("ES6-Modul", violations[0].Details);
    }

    // Zusatztest — window-Zuweisung in NICHT-Modul-Script: fehlt das `export`, schlaegt
    // bereits die Modul-Pruefung fehl (vor der Window-Pruefung). Beide Faelle werden konsistent gemeldet.
    [Fact]
    public void Analyze_ReportsMissingModule_WhenScriptWithoutExportAlsoPollutesWindow()
    {
        const string js = """
            window.doStuff = function() {};
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\legacy.js", config);

        Assert.Single(violations);
        Assert.Equal("JS_EnforceJsModules", violations[0].RuleName);
        Assert.Contains("ES6-Modul", violations[0].Details);
    }

    // Zusatztest — Modul mit `export` und nur-einer Window-Zuweisung: Window-Violation
    [Fact]
    public void Analyze_ReportsWindowPollution_ForModuleWithSingleWindowAssignment()
    {
        const string js = """
            export function safe() { return 1; }
            window.unsafe = () => 2;
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\mixed.js", config);

        Assert.Single(violations);
        Assert.Equal("JS_EnforceJsModules", violations[0].RuleName);
        Assert.Contains("window", violations[0].Details);
    }

    // Zusatztest — Modul mit mehreren window-Zuweisungen: jede wird einzeln gemeldet
    [Fact]
    public void Analyze_ReportsMultipleWindowPollutions_InModule()
    {
        const string js = """
            export function a() {}
            window.x = 1;
            window.y = 2;
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\multi.js", config);

        Assert.Equal(2, violations.Count);
        Assert.All(violations, v => Assert.Equal("JS_EnforceJsModules", v.RuleName));
    }

    // Zusatztest — `this`-Zuweisung an window zaehlt NICHT als Pollution
    // (der Code `this.foo = ...` in einem Modul landet im strikten Modus auf `undefined`).
    [Fact]
    public void Analyze_NoViolation_ForThisAssignment()
    {
        const string js = """
            export function init() {
                this.value = 42;
            }
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\strict.js", config);

        Assert.Empty(violations);
    }

    // Zusatztest — Zuweisung an `globalThis` (NICHT `window`) wird nicht gemeldet
    // (die Regel ist bewusst auf `window` beschraenkt, um false-positives zu vermeiden).
    [Fact]
    public void Analyze_NoViolation_ForGlobalThisAssignment()
    {
        const string js = """
            export function init() {
                globalThis.value = 42;
            }
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\globalthis.js", config);

        Assert.Empty(violations);
    }

    // Zusatztest — Property-Zugriff `window.alert(...)` ist KEINE Zuweisung an window
    [Fact]
    public void Analyze_NoViolation_ForWindowMethodCall()
    {
        const string js = """
            export function show(msg) {
                window.alert(msg);
            }
            """;
        var config = NewJsConfig();

        var violations = JsAnalyzer.Analyze(js, "C:\\app\\wwwroot\\js\\call.js", config);

        Assert.Empty(violations);
    }

    private static JsConfig NewJsConfig(
        int maxLines = 150,
        bool enforceModules = true) =>
        new JsConfig
        {
            MaxJsLineCount = maxLines,
            EnforceJsModules = enforceModules,
        };
}
