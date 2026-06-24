# JavaScript-Linting in AiNetLinter

Dieses Dokument spezifiziert die Implementierung von JavaScript-Linting-Regeln für JS-Interop-Dateien in .NET-Webprojekten.

---

## 1. Intention & Idee

Blazor-Anwendungen nutzen JavaScript primär für Browser-APIs oder bestehende UI-Komponenten (JS Interop). Große JavaScript-Codebasen im gleichen Projekt führen bei KIs zu Verständnisproblemen. Die Regeln in diesem Modul sollen:

* **JS-Dateien minimal halten:** Die Logik gehört primär in C#. Große JS-Dateien werden frühzeitig gemeldet.
* **Modul-Isolierung erzwingen:** Verwendung von ES6-Modulen (`export`) statt globaler Variablen/Funktionen auf dem `window`-Objekt. Das ermöglicht KIs eine saubere Kapselung und macht Interop-Aufrufe robust und testbar.

---

## 2. Technische Bibliothek: Esprima.NET

Wir verwenden das NuGet-Paket `Esprima` (BSD-3-Clause-Lizenz). Es ist ein extrem schneller, standardkonformer ECMAScript-Parser für .NET.

### Architektur-Entscheidung: ParseModule zuerst

Blazor-JS-Interop-Dateien sind grundsätzlich ES6-Module (sie nutzen `export` für Dynamic Import via `IJSRuntime.InvokeAsync`). Daher versucht der Analyzer **immer zuerst `ParseModule()`**. Nur wenn das fehlschlägt, wird auf `ParseScript()` zurückgegriffen.

Diese Strategie löst das `ParseScript`-vs-`ParseModule`-Dilemma: `export`-Statements sind in `ParseScript()` ein Syntax-Fehler. Ein Fallback-Versuch verrät gleichzeitig, ob die Datei überhaupt ein ES6-Modul ist — was für die `EnforceJsModules`-Prüfung direkt genutzt wird.

```csharp
using Esprima;
using Esprima.Ast;

public sealed class JsAnalyzer
{
    public IReadOnlyList<RuleViolation> Analyze(string jsContent, string filePath, WebConfig config)
    {
        var violations = new List<RuleViolation>();
        var cfg = config.Js;

        // 1. Zeilenzählung (kein Parser nötig)
        var lineCount = jsContent.Split('\n').Length;
        if (lineCount > cfg.MaxJsLineCount)
        {
            violations.Add(new RuleViolation(
                "JS_MaxJsLineCount",
                $"JavaScript-Datei hat {lineCount} Zeilen (erlaubt: {cfg.MaxJsLineCount}). "
                + "Komplexe Logik gehört in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.",
                filePath, line: 1));
        }

        // 2. Parse: ParseModule zuerst, Fallback auf ParseScript
        var (program, isModule) = TryParse(jsContent, filePath, violations);
        if (program == null) return violations; // Syntax-Fehler bereits gemeldet

        // 3. EnforceJsModules
        if (cfg.EnforceJsModules)
        {
            if (!isModule)
            {
                violations.Add(new RuleViolation(
                    "JS_EnforceJsModules",
                    "JavaScript-Interop-Dateien müssen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). "
                    + "Blazors Dynamic Import erwartet Module; globale Script-Dateien können nicht per "
                    + "'IJSRuntime.InvokeAsync' isoliert importiert werden.",
                    filePath, line: 1));
            }
            else
            {
                CheckWindowPollution(program, filePath, violations);
            }
        }

        return violations;
    }

    /// <summary>
    /// Versucht ParseModule, fällt auf ParseScript zurück.
    /// Gibt (null, false) zurück wenn beides fehlschlägt (Syntax-Fehler wird gemeldet).
    /// </summary>
    private static (Script? program, bool isModule) TryParse(
        string jsContent,
        string filePath,
        List<RuleViolation> violations)
    {
        var parser = new JavaScriptParser();

        try
        {
            var module = parser.ParseModule(jsContent);
            return (module, isModule: true);
        }
        catch (ParserException)
        {
            // Kein ES6-Modul — als Script versuchen
        }

        try
        {
            var script = parser.ParseScript(jsContent);
            return (script, isModule: false);
        }
        catch (ParserException ex)
        {
            violations.Add(new RuleViolation(
                "JS_SyntaxError",
                $"Syntax-Fehler in JavaScript: {ex.Description}",
                filePath, line: ex.LineNumber));
            return (null, false);
        }
    }

    private static void CheckWindowPollution(Script program, string filePath, List<RuleViolation> violations)
    {
        foreach (var statement in program.Body)
        {
            if (statement is not ExpressionStatement expr) continue;
            if (expr.Expression is not AssignmentExpression assign) continue;
            if (assign.Left is not MemberExpression member) continue;
            if (member.Object is not Identifier id || id.Name != "window") continue;

            violations.Add(new RuleViolation(
                "JS_EnforceJsModules",
                "Zuweisungen an das globale 'window'-Objekt sind verboten. "
                + "Nutze stattdessen exportierte Funktionen: 'export function myFunc() {...}' "
                + "und importiere via 'await JSRuntime.InvokeAsync(\"imports\", ...)'.",
                filePath, line: statement.Location.Start.Line));
        }
    }
}
```

---

## 3. Spezifikation der Regeln & Konfigurations-Defaults

### Regel 1: `JS_MaxJsLineCount`

* **Intention:** Hält JS-Interop-Dateien kurz. Komplexe Logik gehört in C# (Blazor).
* **Default-Limit:** `150` Zeilen.
* **Fehlermeldung:** *"JavaScript-Datei hat {actual} Zeilen (erlaubt: {limit}). Komplexe Logik gehört in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#."*

### Regel 2: `JS_EnforceJsModules`

* **Intention:** Verbietet globale Verschmutzung. Erzwingt ES6-Module für Blazors Dynamic Import.
* **Prüfung:**
  1. Datei ist kein ES6-Modul (kein `export`) → Violation.
  2. Datei ist ein ES6-Modul, enthält aber `window.xyz = ...` → Violation.
* **Default:** `true` (aktiviert).
* **Warum kein Check auf fehlende Exports separat:** Wenn `ParseModule()` gelingt, aber keine `export`-Statements vorhanden sind, ist die Datei ein valides Module ohne öffentliche API — das wird durch Regel 2.1 (nicht isModule → false weil ParseScript als Fallback nötig wäre) nicht erfasst. In diesem Fall: ParseModule gelingt → isModule = true → kein JS_EnforceJsModules für fehlende Exports. Window-Pollution wird trotzdem geprüft. **Bewusste Entscheidung:** Ein Modul ohne Exports ist kein Fehler — es könnte ein Side-Effect-Module (z.B. CSS-in-JS-Shim) sein.

### Regel 3: `JS_SyntaxError`

* **Nicht konfigurierbar**, wird immer gemeldet wenn JS nicht geparst werden kann.
* **Fehlermeldung:** *"Syntax-Fehler in JavaScript: {description}"* mit korrekter Zeilennummer.

---

## 4. Umgang mit False-Positives & Suppression

### Ausschließen von Bibliotheken (ExemptPaths)

```json
"ExemptPaths": [
  "**/wwwroot/lib/**",
  "**/node_modules/**",
  "**/*.min.js"
]
```

### Inline-Unterdrückung

```javascript
// ainetlinter-disable JS_MaxJsLineCount
export function hugeLegacyWrapper() {
    // Wird in Sprint 4 aufgeteilt
}

// ainetlinter-disable JS_EnforceJsModules
window.myLegacyFunction = function() {
    console.log("Legacy-Integration, wird migriert");
};
```

---

## 5. Unit-Tests & Edge-Cases

Testprojekt `AiNetLinter.Tests` wird um `Web/JsAnalyzerTests.cs` erweitert.

| Szenario | Erwartetes Ergebnis |
| :--- | :--- |
| **A** — Sauberes ES6-Modul unter 150 Zeilen mit `export function` | Keine Violations |
| **B** — JS-Datei mit 200 Zeilen | `JS_MaxJsLineCount` |
| **C** — Script ohne `export` (kein Modul) | `JS_EnforceJsModules` (nicht isModule) |
| **D** — `window.showAlert = () => {};` in einem ES6-Modul | `JS_EnforceJsModules` (window pollution) |
| **E** — Suppression `// ainetlinter-disable JS_EnforceJsModules` | Keine Violation |
| **F** — Ungültiges JS `const a = ;` | `JS_SyntaxError` mit korrekter Zeilennummer, kein Absturz |
| **G** — Datei mit `export` wird von `ParseScript()` abgelehnt, `ParseModule()` gelingt | isModule = true, kein `JS_EnforceJsModules` |
| **H** — Legacy-Script ohne `export` und ohne `window`-Zuweisung | `JS_EnforceJsModules` (nicht isModule) |
