# CSS-Selektor-Zeilennummern in CssAnalyzer

Dieses Dokument beschreibt einen bekannten Defekt in der Phase-1-Implementierung des CSS-Linters
und spezifiziert den Fix.

---

## 1. Problem

`CssAnalyzer.CreateViolation` setzt für **alle** CSS-Violations `LineNumber = 1` (Hardcode).
Das ist für datei-weite Befunde korrekt (`CSS_MaxCssLineCount`, `CSS_PreferScopedCss`,
`CSS_ParseError`), aber **falsch** für `CSS_MaxCssSelectorComplexity`:

ExCSS liefert über `rule.Location.Line` die tatsächliche Quellzeile der Regel —
diese Information wird derzeit verworfen.

**Auswirkung im Linter-Output:**

```
CSS_MaxCssSelectorComplexity  site.css:1  — Selektor '.nav .menu .item .link' zu komplex
```

Der Selektor steht in Zeile 247. Der Agent navigiert zu Zeile 1 und findet nichts.

---

## 2. Betroffene Datei

`src/AiNetLinter/Web/CssAnalyzer.cs` — zwei Stellen:

| Stelle | Zeile (ca.) | Was |
|--------|-------------|-----|
| `CreateViolation`-Helper | 178 | `LineNumber = 1` — wird von allen Violations genutzt |
| `CheckSelectorComplexity` | 115 | Ruft `CreateViolation` auf, ohne Zeilennummer zu übergeben |

---

## 3. Fix

### Schritt 1 — `CreateViolation` um optionalen `lineNumber`-Parameter erweitern

```csharp
// vorher:
internal static RuleViolation CreateViolation(
    string filePath, string ruleName, string details, string guidance) =>
    new RuleViolation
    {
        FilePath = filePath,
        LineNumber = 1,
        RuleName = ruleName,
        Details = details,
        Guidance = guidance,
    };

// nachher:
internal static RuleViolation CreateViolation(
    string filePath, string ruleName, string details, string guidance,
    int lineNumber = 1) =>
    new RuleViolation
    {
        FilePath = filePath,
        LineNumber = lineNumber,
        RuleName = ruleName,
        Details = details,
        Guidance = guidance,
    };
```

Alle bestehenden Aufrufe ohne `lineNumber`-Argument bleiben unverändert (Default = 1).

### Schritt 2 — `CheckSelectorComplexity` die ExCSS-Zeilennummer übergeben

```csharp
// vorher:
violations.Add(CreateViolation(
    filePath,
    "CSS_MaxCssSelectorComplexity",
    $"CSS-Selektor '{Truncate(selectorText, 80)}' ist zu komplex " +
    $"(Tiefe: {depth}, erlaubt: {maxComplexity}).",
    "Nutze Scoped CSS (.razor.css) oder vereinfache den Selektor. " +
    "Verschachtelte Klassen-Kombinationen sind fuer Modelle schwer zuzuordnen — " +
    "ein klar benannter Wurzel-Selektor reduziert Fehlzuordnungen."));

// nachher:
violations.Add(CreateViolation(
    filePath,
    "CSS_MaxCssSelectorComplexity",
    $"CSS-Selektor '{Truncate(selectorText, 80)}' ist zu komplex " +
    $"(Tiefe: {depth}, erlaubt: {maxComplexity}).",
    "Nutze Scoped CSS (.razor.css) oder vereinfache den Selektor. " +
    "Verschachtelte Klassen-Kombinationen sind fuer Modelle schwer zuzuordnen — " +
    "ein klar benannter Wurzel-Selektor reduziert Fehlzuordnungen.",
    lineNumber: rule.Location.Line));
```

**Hinweis zu ExCSS:** `StyleRule.Location` ist vom Typ `TextPosition` (ExCSS 4.x).
`TextPosition.Line` ist 1-basiert — kein Offset nötig.

---

## 4. Test-Erweiterung

In `src/AiNetLinter.Tests/Web/CssAnalyzerTests.cs` einen neuen Test hinzufügen,
der die korrekte Zeilennummer verifiziert:

```csharp
[Fact]
public void Analyze_ReportsCorrectLineNumber_ForSelectorComplexityViolation()
{
    const string css = """
        .simple { color: red; }
        .another { font-size: 1em; }
        .a .b .c .d { color: blue; }
        """;
    var config = NewCssConfig(maxSelectorComplexity: 3);

    var violations = CssAnalyzer.Analyze(css, "C:\\app\\wwwroot\\css\\x.css", config);

    Assert.Single(violations);
    Assert.Equal("CSS_MaxCssSelectorComplexity", violations[0].RuleName);
    Assert.Equal(3, violations[0].LineNumber); // Selektor steht in Zeile 3
}
```

---

## 5. Nicht ändern

- `CSS_MaxCssLineCount` → `LineNumber = 1` bleibt korrekt (file-level Befund).
- `CSS_PreferScopedCss` → `LineNumber = 1` bleibt korrekt (file-level Befund).
- `CSS_ParseError` → `LineNumber = 1` bleibt korrekt (Datei nicht parsebar).
- Alle anderen Aufrufer von `CreateViolation` → bleiben unverändert, da der Default `1` ist.

---

## 6. Aufwand

~5 Zeilen Produktionscode, 1 neuer Test. Kein Architektur-Eingriff.
