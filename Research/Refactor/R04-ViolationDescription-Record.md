# R04 — `ViolationDescription`-Record für `ReportViolation`-Overloads

**Problem:** `CheckerContext.cs` trägt 3 Inline-Suppressions (`MaxMethodParameterCount`)
weil jede der drei `ReportViolation`-Überladungen 5 Parameter hat (Limit: 4):

```csharp
// ainetlinter-disable MaxMethodParameterCount
internal void ReportViolation(SyntaxNode node, string ruleName, string details, string guidance, string? effectiveSeverity = null)
```

Die 5 Parameter sind alle notwendig — aber 4 davon (`ruleName`, `details`, `guidance`,
`effectiveSeverity`) gehören inhaltlich zusammen: sie beschreiben den Regelverstoß,
nicht den Ort im Code.

---

## Diagnose

Die drei Overloads unterscheiden sich nur im ersten Parameter (wo im Code):
- `(SyntaxNode node, …)` — Position aus Syntax-Node
- `(SyntaxToken token, …)` — Position aus Token
- `(int lineNumber, …)` — explizite Zeilennummer

Die restlichen 4 Parameter sind identisch und könnten als Value Object gebündelt werden.
Die Regel `MaxMethodParameterCount` hat hier keine False Positive — der Code hat
tatsächlich zu viele Parameter pro Methode.

---

## Lösungsansatz: `ViolationDescription` Record

```csharp
/// <summary>
/// Beschreibt einen Regelverstoß (Regel-ID, Nachricht, Leitfaden, Severity).
/// Wird an ReportViolation-Overloads übergeben.
/// </summary>
internal sealed record ViolationDescription(
    string RuleName,
    string Details,
    string Guidance,
    string? EffectiveSeverity = null);
```

Die drei Overloads in `CheckerContext` reduzieren sich auf je 2 Parameter:

```csharp
internal void ReportViolation(SyntaxNode node, ViolationDescription desc) =>
    AddViolation(new RuleViolation
    {
        FilePath          = FilePath,
        LineNumber        = SyntaxHelper.LineOf(node),
        RuleName          = desc.RuleName,
        Details           = desc.Details,
        Guidance          = desc.Guidance,
        EffectiveSeverity = desc.EffectiveSeverity,
    });

internal void ReportViolation(SyntaxToken token, ViolationDescription desc) =>
    AddViolation(new RuleViolation
    {
        FilePath          = FilePath,
        LineNumber        = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
        RuleName          = desc.RuleName,
        Details           = desc.Details,
        Guidance          = desc.Guidance,
        EffectiveSeverity = desc.EffectiveSeverity,
    });

internal void ReportViolationAtLine(int lineNumber, ViolationDescription desc) =>
    AddViolation(new RuleViolation
    {
        FilePath          = FilePath,
        LineNumber        = lineNumber,
        RuleName          = desc.RuleName,
        Details           = desc.Details,
        Guidance          = desc.Guidance,
        EffectiveSeverity = desc.EffectiveSeverity,
    });
```

Die Suppress-Kommentare entfallen.

---

## Call-Site-Änderungen

Alle Checker müssen aktualisiert werden. Aktuelles Aufrufmuster:

```csharp
context.ReportViolation(node, "MaxLineCount", "Datei hat 650 Zeilen.", "Datei splitten.", null);
```

Neues Muster:

```csharp
context.ReportViolation(node, new ViolationDescription("MaxLineCount", "Datei hat 650 Zeilen.", "Datei splitten."));
```

Wenn Severity gesetzt werden muss:
```csharp
context.ReportViolation(node, new ViolationDescription("MaxLineCount", "...", "...", "warning"));
```

### Betroffene Checker (Schätzung)

Alle Dateien unter `src/AiNetLinter/Core/Checkers/` die `ReportViolation` aufrufen:

```powershell
rg "\.ReportViolation\(" src/AiNetLinter/Core/Checkers/ -l
```

Typischerweise ~12–15 Checker-Dateien. Der Aufruf ist mechanisch — Suchen & Ersetzen
per Regex möglich.

**Regex für den Ersatz** (Zeile für Zeile, kein Multi-Line nötig):
```
Suchen:  ReportViolation\((\w+), "([^"]+)", "([^"]+)", "([^"]+)"(?:, null)?\)
Ersetzen: ReportViolation($1, new ViolationDescription("$2", "$3", "$4"))
```

---

## Datei-Ablage für `ViolationDescription`

**Option A:** In `CheckerContext.cs` direkt (selbe Datei, klein genug):
```csharp
// am Ende von CheckerContext.cs
internal sealed record ViolationDescription(
    string RuleName, string Details, string Guidance,
    string? EffectiveSeverity = null);
```

**Option B:** Eigene Datei `src/AiNetLinter/Models/ViolationDescription.cs`

Empfehlung: **Option A** — der Record ist ausschließlich für `CheckerContext` gedacht
und bleibt so nah am Verwendungsort.

---

## Unit Tests

Kein neuer Testcode für das Record selbst nötig (kein Verhalten).

Die bestehenden Tests in `LinterAnalyzerTests.cs`, `LinterEngineTests.cs` etc. validieren
implizit dass die Checker weiterhin korrekt Violations melden.

Explizit testen könnte man, dass `ViolationDescription` die Felder korrekt durchreicht:

```csharp
[Fact]
public void ReportViolation_SetsAllFieldsFromDescription()
{
    var config = TestConfigFactory.Default();
    var ctx = new CheckerContext("test.cs", config, /* semanticModel */ ..., false, null);
    var desc = new ViolationDescription("MyRule", "Details here", "Fix guidance", "warning");

    ctx.ReportViolationAtLine(42, desc);

    var violation = ctx.Violations.Single();
    Assert.Equal("MyRule", violation.RuleName);
    Assert.Equal("Details here", violation.Details);
    Assert.Equal("Fix guidance", violation.Guidance);
    Assert.Equal("warning", violation.EffectiveSeverity);
    Assert.Equal(42, violation.LineNumber);
}
```

---

## Abwägung: Lohnt sich das?

| Aspekt | Pro Record | Contra Record |
|:---|:---|:---|
| Regelkonformität | Suppressions entfallen | — |
| Lesbarkeit Call-Sites | `new ViolationDescription(...)` macht Felder benennt | Mehr Zeichen pro Aufruf |
| Erweiterbarkeit | Neue Felder (z.B. `Category`) ohne Signaturänderung | Geringer Bedarf absehbar |
| Umfang der Änderung | ~12 Dateien, mechanisch | Migrationsaufwand |

**Urteil:** Sinnvoll — aber der geringste Dringlichkeitsgrad der 4 Refactorings.
R01 und R02 haben den größeren Nutzen für weniger Aufwand.

---

## Dokumentation

Keine Benutzer-Dokumentation nötig (internes API).

---

## Commit-Vorschlag

```
refactor: ViolationDescription-Record einführen, ReportViolation-Signaturen auf 2 Parameter reduzieren
```

Entfernt 3 Inline-Suppressions aus CheckerContext.cs. Call-Sites in allen Checker-Klassen
aktualisiert.
