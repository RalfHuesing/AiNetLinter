# CSS-Linting in AiNetLinter

Dieses Dokument spezifiziert die Implementierung von CSS-Linting-Regeln zur Optimierung der AI-Lesbarkeit und Modifizierbarkeit von Stylesheets.

---

## 1. Intention & Idee

Große, verschachtelte und globale CSS-Dateien führen bei KI-Agenten häufig zu Fehlern. Die Regeln in diesem Modul sollen:

* **Größenbeschränkung erzwingen:** Dateigrößen so limitieren, dass sie problemlos in das Aufmerksamkeitsfenster (Attention Window) des Modells passen, ohne dass beim Erzeugen von Diffs Fehler entstehen.
* **CSS-Isolation fördern:** Die Verwendung von Blazor Scoped CSS (`.razor.css`) gegenüber unübersichtlichen globalen Dateien (`app.css`) incentivieren, um Nebeneffekte (Butterfly-Effekt) bei Code-Änderungen auszuschließen.
* **Selektor-Vereinfachung:** Komplexe, tief geschachtelte CSS-Selektoren verhindern, da diese für Modelle schwer zuzuordnen sind.

---

## 2. Technische Bibliothek: ExCSS

Wir verwenden das NuGet-Paket `ExCSS` (MIT-Lizenz). Es ist ein rein C#-basierter CSS-Parser, der ein vollständiges Abstract Syntax Tree (AST) erzeugt.

```csharp
using ExCSS;

public sealed class CssAnalyzer
{
    public IReadOnlyList<RuleViolation> Analyze(string cssContent, string filePath, WebConfig config)
    {
        var violations = new List<RuleViolation>();
        var cfg = config.Css;

        // 1. Zeilenzählung (kein Parser nötig)
        var lineCount = cssContent.Split('\n').Length;
        if (lineCount > cfg.MaxCssLineCount)
        {
            violations.Add(new RuleViolation(
                "CSS_MaxCssLineCount",
                $"CSS-Datei hat {lineCount} Zeilen (erlaubt: {cfg.MaxCssLineCount}).",
                filePath, line: 1));
        }

        // 2. PreferScopedCss: Nur für globale CSS-Dateien (nicht .razor.css)
        if (cfg.PreferScopedCss && !filePath.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase))
        {
            // Parser nur laden wenn PreferScopedCss-Check oder SelectorComplexity-Check aktiv
            var parser = new StylesheetParser();
            Stylesheet stylesheet;
            try
            {
                stylesheet = parser.Parse(cssContent);
            }
            catch (Exception ex)
            {
                violations.Add(new RuleViolation(
                    "CSS_ParseError",
                    $"CSS-Datei konnte nicht geparst werden: {ex.Message}",
                    filePath, line: 1));
                return violations;
            }

            var ruleCount = stylesheet.StyleRules.Count();
            if (ruleCount > cfg.PreferScopedCssMinRuleCount)
            {
                violations.Add(new RuleViolation(
                    "CSS_PreferScopedCss",
                    $"Globale CSS-Datei enthält {ruleCount} Stil-Regeln (Schwellenwert: {cfg.PreferScopedCssMinRuleCount}). "
                    + "Verschiebe komponentenspezifische Stile in eine '.razor.css'-Scoped-CSS-Datei, "
                    + "um den globalen Butterfly-Effekt bei KI-Edits zu eliminieren.",
                    filePath, line: 1));
            }

            // 3. Selektor-Komplexität
            foreach (var rule in stylesheet.StyleRules)
            {
                CheckSelectorComplexity(rule, filePath, violations, cfg);
            }
        }
        else if (!filePath.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase))
        {
            // PreferScopedCss deaktiviert, aber SelectorComplexity trotzdem prüfen
            var parser = new StylesheetParser();
            try
            {
                var stylesheet = parser.Parse(cssContent);
                foreach (var rule in stylesheet.StyleRules)
                    CheckSelectorComplexity(rule, filePath, violations, cfg);
            }
            catch (Exception ex)
            {
                violations.Add(new RuleViolation(
                    "CSS_ParseError",
                    $"CSS-Datei konnte nicht geparst werden: {ex.Message}",
                    filePath, line: 1));
            }
        }

        return violations;
    }

    private static void CheckSelectorComplexity(
        StyleRule rule,
        string filePath,
        List<RuleViolation> violations,
        CssConfig cfg)
    {
        var selectorText = rule.Selector.Text;
        var depth = selectorText
            .Split(new[] { ' ', '>', '+', '~' }, StringSplitOptions.RemoveEmptyEntries)
            .Length;

        if (depth > cfg.MaxCssSelectorComplexity)
        {
            violations.Add(new RuleViolation(
                "CSS_MaxCssSelectorComplexity",
                $"CSS-Selektor '{selectorText}' ist zu komplex "
                + $"(Tiefe: {depth}, erlaubt: {cfg.MaxCssSelectorComplexity}). "
                + "Nutze Scoped CSS oder vereinfache den Selektor.",
                filePath, line: rule.Location.Line));
        }
    }
}
```

---

## 3. Spezifikation der Regeln & Konfigurations-Defaults

### Regel 1: `CSS_MaxCssLineCount`

* **Intention:** Verhindert unlesbar große Stylesheets (Lost-in-the-Middle-Effekt).
* **Default-Limit:** `300` Zeilen.
* **Fehlermeldung:** *"CSS-Datei hat {actual} Zeilen (erlaubt: {limit})."*
* **Empfohlene Behebung:** CSS-Dateien nach Features splitten oder in Scoped-CSS (`.razor.css`) überführen.

### Regel 2: `CSS_PreferScopedCss`

* **Intention:** Minimiert globale Stile, da diese bei KIs oft unvorhergesehene visuelle Regressionen erzeugen (Butterfly-Effekt).
* **Prüfung (konkrete Heuristik):** Eine CSS-Datei, die **nicht** auf `.razor.css` endet und **nicht** in einem ExemptPath liegt, und die mehr als `PreferScopedCssMinRuleCount` Stil-Regeln enthält, wird gewarnt.
* **Begründung für diese Heuristik:** CSS-Dateien mit wenigen Regeln (z.B. CSS-Reset, Custom Properties, `@font-face`) sind legitimerweise global. Dateien mit vielen Regeln sind fast immer komponentenspezifisch und profitieren von Scoped CSS.
* **Default:** `true` (aktiviert). **Schwellenwert:** `PreferScopedCssMinRuleCount: 5`.

### Regel 3: `CSS_MaxCssSelectorComplexity`

* **Intention:** Verhindert fehleranfälliges CSS-Over-Engineering.
* **Default-Limit:** `3` (max. 3 Selektor-Segmente, z.B. `.card > .header .title`).
* **Default-Severity:** `warning`.

---

## 4. Umgang mit False-Positives & Suppression

### Ausschließen von Bibliotheken (ExemptPaths)

Framework- und Bibliotheks-CSS (Bootstrap, MudBlazor) werden über `ExemptPaths` ignoriert:

```json
"ExemptPaths": [
  "**/wwwroot/lib/**",
  "**/node_modules/**",
  "**/*.min.css"
]
```

Der bestehende `FileFilterEvaluator` übernimmt das Glob-Matching — keine neue Implementierung nötig.

### Inline-Unterdrückung (Suppression)

```css
/* ainetlinter-disable CSS_MaxCssLineCount */
/* Komplexes Legacy-Stylesheet wird in Sprint 3 migriert */

/* ainetlinter-disable CSS_MaxCssSelectorComplexity */
.container .sub-container .panel .content .button {
    color: red;
}
```

---

## 5. Unit-Tests & Edge-Cases

Testprojekt `AiNetLinter.Tests` wird um `Web/CssAnalyzerTests.cs` erweitert.

| Szenario | Erwartetes Ergebnis |
| :--- | :--- |
| **A** — CSS-Datei unter 300 Zeilen, einfache Selektoren | Keine Violations |
| **B** — CSS-Datei mit 350 Zeilen | `CSS_MaxCssLineCount` |
| **C** — Selektor `.a .b .c .d` (Tiefe 4) | `CSS_MaxCssSelectorComplexity` |
| **D** — Globale CSS mit 6 Regeln, `PreferScopedCss: true` | `CSS_PreferScopedCss` |
| **E** — `.razor.css`-Datei mit 6 Regeln | Keine `CSS_PreferScopedCss` (ist bereits Scoped CSS) |
| **F** — Suppression `/* ainetlinter-disable CSS_MaxCssSelectorComplexity */` | Keine Violation trotz komplexem Selektor |
| **G** — `*.min.css` in ExemptPaths | Datei wird vollständig übersprungen |
| **H** — Ungültiges CSS (fehlende geschweifte Klammern) | `CSS_ParseError`, kein Absturz |
