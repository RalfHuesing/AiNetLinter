# 04 — LLM/Agent-Optimierung

> Befunde und Vorschläge zur Optimierung der **Agent-Integration** und **LLM-Consumption**.  
> Aufbauend auf [`03-Architektur-Refactoring-Vorschlaege.md`](03-Architektur-Refactoring-Vorschlaege.md).

---

## Zielsetzung

AiNetLinter richtet sich explizit an **AI-Agent-Workflows** (Cursor, Claude Code, GitHub Copilot). Der dokumentierte primäre Use-Case:

- AiNetLinter wird als Unit-Test-Hook oder direkt im Agent-Loop aufgerufen
- Output wird in eine **Datei** umgeleitet (`> output.md`) oder direkt gelesen
- **Agent liest die Datei** und behebt die Violations direkt
- **Markdown ist das einzige Output-Format** — JSON, NDJSON und SARIF wurden gestrichen

### Aktueller Stand

Der Linter **produziert** gute LLM-Ausgabe. Aber seine **Struktur** macht Agent-Integration unnötig aufwändig:

- Keine Discovery-Schnittstelle für Agenten (welche Regeln gibt es überhaupt?)
- Fallback-Guidance für neue Regeln ist generisch ("Bitte behebe diesen Verstoss")
- Fehler-Output ist nicht strukturiert genug für automatisches Parsing

---

## L3 — Rule-Discovery via CLI (`--list-rules`, `--describe-rule`)

**Abhängig von:** RuleRegistry (bereits umgesetzt)  
**Aufwand:** S (1 Tag)  
**Nutzen:** ★★★★★

### Problem

Agenten haben keine programmatische Möglichkeit, alle verfügbaren Regeln aufzulisten. Sie müssten drei Generator-Dateien lesen und parsen — oder kennen die Regel-Namen schon aus dem Linter-Output.

### Lösungsansatz

**`--list-rules`:** Gibt alle Regeln mit Intent-Gruppierung aus.

```bash
ainetlinter --list-rules
```

```
Verfügbare Regeln (30 gesamt)

Intent: agent-context
  EnforceSealedClasses          [error]   Konkrete Klassen muessen 'sealed' sein
  BanPublicNestedTypes          [error]   Verbot oeffentlicher nested Typen
  MaxLineCount                  [error]   Maximale Dateilaenge

Intent: agent-resilience
  EnforceNoSilentCatch          [error]   Keine stummen catch-Bloecke
  ...
```

**`--describe-rule <RuleId>`:** Gibt vollständige Metadaten einer Regel aus.

```bash
ainetlinter --describe-rule EnforceSealedClasses
```

```
Regel: EnforceSealedClasses
Severity:      error
Intent:        agent-context
Auto-Fix:      ja (--fix)
ExemptSuffixes: Base, Foundation, Host

Beschreibung:
  Konkrete Klasse ist nicht 'sealed'.

Anleitung:
  Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu ...

Konfiguration:
  Override-Pfad: Global.EnforceSealedClasses
  Default:       true
```

**Implementierung:** Beide Commands lesen aus `RuleRegistry.All`. Command-Klassen in R3-Struktur (`ListRulesCommand`, `DescribeRuleCommand`).

**Nutzen für Agenten:**
- Agent kann vor Edit-Calls prüfen, welche Regeln aktiv sind
- Agent kann gezielt Regel-Details abfragen statt aus dem Output zu erraten
- Hilft beim Schreiben von Suppression-Kommentaren (`// ainetlinter:disable EnforceSealedClasses`)

---

## L4 — Verbesserte LLM-Output-Qualität

**Abhängig von:** RuleRegistry (bereits umgesetzt)  
**Aufwand:** S (mit RuleRegistry als Fundament)  
**Nutzen:** ★★★★★

### Problem

`ViolationTextFormatter.RuleInstructions` (26 Einträge, manuell gepflegt) hat einen generischen Fallback für neue Regeln:

```
-> {ruleName}: Bitte behebe diesen Verstoss gemaess den Richtlinien.
```

Ein LLM bekommt mit diesem Fallback keine klare Anleitung und muss raten.

### Lösungsansatz

Da `RuleRegistry` eingeführt wurde, liest `ViolationTextFormatter.GetRuleInstruction(ruleName)` direkt aus `RuleRegistry.Resolve(ruleName).DetailedGuidance`. Kein manuell gepflegtes Dict mehr, kein Fallback.

Neue Felder in `RuleMetadata` für reichere LLM-Anleitung:

```csharp
public sealed record RuleMetadata(
    // ... bestehende Felder
    string? ExampleViolation = null,  // Code-Snippet der Verletzung
    string? ExampleFix = null,        // Code-Snippet der Lösung
    int SortPriority = 100            // Reihenfolge für LLM-Priorisierung
);
```

**Ausgabe pro Violation (verbessert):**

```markdown
### EnforceSealedClasses × 1 — src/Services/UserService.cs:12

**Problem:** Konkrete Klasse ist nicht 'sealed'.

**Anleitung:**
Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu.
Ausnahmen: Klassen mit Suffixen Base, Foundation, Host.

**Beispiel-Fix:**
```csharp
// Vorher:
public class UserService { }

// Nachher:
public sealed class UserService { }
```

**Auto-Fix verfügbar:** `ainetlinter --fix`
```

**Nutzen für Agenten:**
- Inline-Code-Beispiele direkt im Output → weniger Kontext-Recherche nötig
- `SortPriority` ermöglicht gestaffelte Bearbeitung (einfache Fixes zuerst)
- Kein generischer Fallback mehr — alle Regeln vollständig beschrieben

---

## L8 — Semantische Suche über Rule-Metadaten (`--search-rules`)

**Abhängig von:** RuleRegistry (bereits umgesetzt)  
**Aufwand:** S  
**Nutzen:** ★★★

### Problem

Ein Agent muss den exakten `RuleId` kennen um eine Regel zu finden. Konzeptionelle Suche ("Was prüft das Tool zu Vererbung?") ist nicht möglich.

### Lösungsansatz

```bash
ainetlinter --search-rules "vererbung"
# Output:
# EnforceSealedClasses    [agent-context, error]  Verhindert unkontrollierte Vererbung
# MaxInheritanceDepth     [agent-context, error]  Verbot tiefer Vererbungshierarchien

ainetlinter --search-rules "komplexität"
# Output:
# MaxCyclomaticComplexity [agent-context, error]
# MaxCognitiveComplexity  [agent-context, error]
```

**Implementierung:**

```csharp
public static IReadOnlyList<RuleMetadata> SearchRules(string query)
{
    var q = query.ToLowerInvariant();
    return RuleRegistry.All
        .Where(r =>
            r.RuleId.ToLowerInvariant().Contains(q) ||
            r.DisplayName.ToLowerInvariant().Contains(q) ||
            r.ShortDescription.ToLowerInvariant().Contains(q) ||
            r.Intent.ToLowerInvariant().Contains(q))
        .ToList();
}
```

---

## L9 — Strukturiertes Error-Reporting für Failures

**Abhängig von:** nichts  
**Aufwand:** M (1–2 Tage)  
**Nutzen:** ★★★

### Problem

Wenn der Linter scheitert (MSBuild-Fehler, Config-Fehler), wird einfach `Console.Error.WriteLine(...)` aufgerufen. Für Agenten schwer zu parsen und zu klassifizieren.

### Lösungsansatz

Definiertes Error-Schema für alle Fehlermeldungen:

```
[ERROR]: <error_code>: <short_message>
  context: <file or step>
  hint:    <actionable_suggestion>
```

**Beispiel:**

```
[ERROR]: CONFIG_INVALID: rules.json enthaelt unbekannte Option "MaxFoo"
  context: rules.json (Zeile 23)
  hint:    Entferne die Option oder aktualisiere auf AiNetLinter 1.0.51+
```

**Error-Codes definieren** (Konstanten-Klasse `LinterErrorCodes`):

```csharp
public static class LinterErrorCodes
{
    public const string ConfigInvalid        = "CONFIG_INVALID";
    public const string SolutionNotFound     = "SOLUTION_NOT_FOUND";
    public const string WorkspaceDiagnostic  = "WORKSPACE_DIAGNOSTIC";
    public const string AnalysisFailed       = "ANALYSIS_FAILED";
    public const string BaselineNotFound     = "BASELINE_NOT_FOUND";
}
```

**Nutzen für Agenten:**
- Agent kann Fehler anhand des Codes klassifizieren
- `hint` ist direkt actionable
- Konsistentes Format ermöglicht einfaches String-Matching

---

## L10 — Inline-Fix-Snippets im Violation-Output

**Abhängig von:** RuleRegistry (bereits umgesetzt), R2 (Checker aufteilen für ExampleFix)  
**Aufwand:** M (L4 ist Voraussetzung)  
**Nutzen:** ★★★★★

### Problem

Aktuell: `--fix` ist ein separater Modus. Agent muss zweimal aufrufen (erst lint, dann fix) und weiß nicht im Voraus, ob ein Auto-Fix verfügbar ist.

### Lösungsansatz

Im Markdown-Output jede auto-fixbare Violation mit konkretem Fix-Snippet kennzeichnen:

```markdown
### EnforceSealedClasses × 1 — src/UserService.cs:12

**Auto-fixable** — run `ainetlinter --fix` oder direkt anwenden:

```csharp
// Ersetze:
public class UserService

// Mit:
public sealed class UserService
```
```

Das `ExampleFix`-Feld aus `RuleMetadata` (→ L4) liefert den generischen Hinweis; für eine **datei-spezifische** Vorschau müsste der Linter den tatsächlichen Quelltext parsen (Aufwand-Anstieg auf L). Als erster Schritt reicht der generische Hinweis aus `RuleMetadata.ExampleFix`.

---

## L11 — Bessere Test-Coverage für LLM-Use-Cases

**Abhängig von:** RuleRegistry (bereits umgesetzt)  
**Aufwand:** S  
**Nutzen:** ★★★

### Problem

Tests prüfen funktionale Korrektheit (Regel feuert bei X). Sie prüfen nicht, ob der **LLM-Output** vollständig und korrekt ist.

### Lösungsansatz

**Snapshot-Tests für LLM-Output** (z. B. mit `Verify`-Bibliothek):

```csharp
[Fact]
public async Task Lint_ProducesLLMReadableOutput()
{
    var violations = /* ... */;
    var output = ViolationTextFormatter.Format(violations, outputRoot, config);
    await Verify(output);  // Snapshot-Test
}
```

**Vollständigkeitstests für RuleRegistry:**

```csharp
[Fact]
public void AllRules_HaveCompleteGuidance()
{
    foreach (var rule in RuleRegistry.All)
    {
        Assert.NotEmpty(rule.DetailedGuidance);
        Assert.NotEmpty(rule.ShortDescription);
        Assert.NotEmpty(rule.Intent);
    }
}

[Theory]
[MemberData(nameof(GetAllRuleIds))]
public void AllViolations_HaveNonEmptyGuidance(string ruleId)
{
    var rule = RuleRegistry.Resolve(ruleId);
    Assert.NotEmpty(rule.DetailedGuidance);
}
```

**Integration-Test für vollständige Violation-Struktur:**

```csharp
[Fact]
public async Task AllViolations_HaveRequiredFields()
{
    var violations = await LintAsync(fixture);
    foreach (var v in violations)
    {
        Assert.NotEmpty(v.RuleName);
        Assert.NotEmpty(v.Details);
        Assert.NotEmpty(v.Guidance);
        Assert.True(v.LineNumber > 0);
        Assert.NotEmpty(v.FilePath);
    }
}
```

---

## L12 — Doc-Updates für Agent-Workflows

**Abhängig von:** L3, L4 (Discovery-Commands müssen existieren)  
**Aufwand:** S  
**Nutzen:** ★★★

### Problem

`README.md` und `Docs/configuration.md` sind menschen-orientiert. Für Agenten fehlt ein kompakter Referenz-Block.

### Lösungsansatz

**1. Neuer Abschnitt in README.md: "Agent-Integration"**

```markdown
## Agent-Integration

# Alle verfügbaren Regeln anzeigen:
ainetlinter --list-rules

# Eine Regel im Detail:
ainetlinter --describe-rule EnforceSealedClasses

# Regeln nach Stichwort suchen:
ainetlinter --search-rules "vererbung"

# Lint + Auto-Fix:
ainetlinter --config rules.json --path ./src/
ainetlinter --fix --config rules.json --path ./src/
```

**2. Neue Datei `Docs/agent-api.md`:**

- Alle CLI-Flags mit kurzem Schema
- Workflow: "Lint → Fix"
- Workflow: "Baseline anlegen → ratchet"
- Fehlerformat (→ L9)
- Vollständige `--list-rules`-Ausgabe als Referenz

**3. Cursor/Claude-Snippet in `.cursor/rules/`:**

Workflow-Vorlage für den Agent: welche Reihenfolge bei Lint-Fix-Loop, wie mit Baseline umgehen, welche Auto-Fixes verfügbar sind.

---

## Zusammenfassung LLM-Optimierung

| L3 — Rule-Discovery CLI        | S       | ★★★★★  | RuleRegistry (erledigt) |
| L4 — Bessere LLM-Output        | S       | ★★★★★  | RuleRegistry (erledigt) |
| L8 — Semantische Suche         | S       | ★★★    | RuleRegistry (erledigt) |
| L9 — Strukturiertes Error      | M       | ★★★    | —            |
| L10 — Inline-Fix-Snippets      | M       | ★★★★★  | RuleRegistry (erledigt), L4 |
| L11 — Snapshot-Tests           | S       | ★★★    | RuleRegistry (erledigt) |
| L12 — Doc-Updates              | S       | ★★★    | L3, L4       |

### Empfohlene Reihenfolge

1. **L3 + L4** — Discovery + vollständige Guidance (aufbauend auf RuleRegistry)
2. **L8** — Suche, auf RuleRegistry aufbauend, kleiner Aufwand
3. **L9** — Error-Reporting, unabhängig, verbesserter Diagnosis-Output
4. **L10, L11, L12** — Polish, nach den Kernrefactorings

---

## Strategische Perspektive

AiNetLinter ist heute ein **gutes CLI-Tool mit LLM-Output**. Nach den empfohlenen Refactorings (R2–R11) und den Agent-Optimierungen (L3–L12) wird es ein **vollständig agent-freundliches Werkzeug**:

- Agenten können Regeln selbst entdecken (`--list-rules`, `--describe-rule`)
- Violations enthalten vollständige, regelspezifische Anleitungen statt Fallback-Texte
- Fehler sind strukturiert und programmatisch auswertbar
- Auto-Fix-Verfügbarkeit ist direkt im Output erkennbar

Der Betrieb bleibt dabei explizit als **CLI-Tool** — ohne Server-Modus, ohne Daemon, ohne externe Protokoll-Abhängigkeiten. Agenten rufen das Tool auf und lesen den Datei-Output — dieser Use-Case ist mit den vorgeschlagenen Optimierungen optimal bedient.
