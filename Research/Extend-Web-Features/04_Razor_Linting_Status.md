# Razor-Linting Phase 3 — Status (Stand: 2026-06-24, Commit-Vorbereitung)

Dieses Dokument beschreibt den aktuellen Implementierungsstand von
[03_Razor_Linting.md](03_Razor_Linting.md) zum Zeitpunkt der Unterbrechung und
listet die noch offenen Aufgaben fuer die naechste Session.

---

## 1. Was wurde umgesetzt

### 1.1 Konfiguration

- **`src/AiNetLinter/Configuration/WebConfig.cs`** — `RazorConfig`-Record mit allen
  acht Schaltern aus dem Research-Dokument ergaenzt. `WebConfig.Apply` reicht den
  optionalen `RazorConfigOverride` durch.
- **`src/AiNetLinter/Configuration/LinterConfig.ValueTypes.cs`** — `RazorConfigOverride`
  mit acht nullable Properties fuer Project-Overrides.
- **`src/AiNetLinter/Core/LinterRuleIds.cs`** — acht neue Konstanten
  (`RAZOR_MaxRazorLineCount`, `RAZOR_MaxRazorCodeBlockLines`,
  `RAZOR_MaxMarkupNestingDepth`, `RAZOR_BanInlineEventLambdas`,
  `RAZOR_MaxControlFlowBlocks`, `RAZOR_MaxForeachNestingDepth`,
  `RAZOR_MaxComponentParameterCount`, `RAZOR_BanInlineTernaryInAttributes`).
- **`src/AiNetLinter/Core/RuleRegistry.Web.cs`** — acht `Build*`-Methoden +
  Eintraege in `BuildWebAssetRules`-Array. Severity-Verteilung: 1× error
  (Dateilaenge), 7× warning. Intent: `agent-context`.
- **`rules.json`** — neue Sektion `"Web.Razor"` mit den acht Properties + Defaults
  aus dem Research-Dokument (300 / 20 / 6 / true / 8 / 2 / 5 / true).

### 1.2 Analyzer (textbasiert, ohne externe Parser-Dependency)

- **`src/AiNetLinter/Web/RazorAnalyzer.cs`** (~246 Zeilen) — Hauptklasse als
  `partial class`. Enthaelt:
  - Elf `private static readonly Regex`-Patterns (alle in [RegexOptions.Compiled]).
  - `Analyze(string razorContent, string filePath, RazorConfig config)`.
  - Acht `Check*`-Methoden (eine pro Regel).
- **`src/AiNetLinter/Web/RazorAnalyzer.Helpers.cs`** (~230 Zeilen) — Zweite
  `partial`-Datei mit den Helper-Methoden. Wesentliche Bausteine:
  - `ComputeMaxTagNestingDepth` (Komplexitaet reduziert durch Stack-Sort).
  - `ComputeMaxForeachNestingDepth` + `FindForeachBodyEnd` (Brace-Balance mit
    String-Skip).
  - `FindMatchingBrace` (mit String-Skip).
  - `SkipStringContext` (gemeinsamer String-/Char-/Razor-Comment-Skip-Helper zur
    Reduktion der kognitiven Komplexitaet in den Brace-Matchern).
  - `CountAttributes` (akzeptiert `@bind-Value:event="..."`-Modifikator).
  - `CountLines`, `GetLineNumber`, `ExtractComponentNameFromPath`, `StripComments`.
- **Begruendung fuer textbasierten Ansatz statt Microsoft.AspNetCore.Razor.Language:**
  - Die Regeln basieren auf Pattern-Counting (Anzahl Bloecke, Anzahl Verschachtelung,
    Anzahl Attribute) — kein vollstaendiger AST noetig.
  - Robuster und schneller; keine externe NuGet-Dependency.
  - C#-Logik in `@code`/`@if`/`@foreach` wird ohnehin in der `.razor.cs`-Begleitdatei
    analysiert (Epic 22). Doppelaufwand waere Verschwendung.
  - Im Klassen-XML-Kommentar dokumentiert.

### 1.3 Integration

- **`src/AiNetLinter/Web/WebFileSeparationChecker.cs`** — `AnalyzeRazorEntries`-
  Methode ergaenzt; `IsRazorAnalysisActive` + `IsAnyRazorRuleActive` pruefen,
  ob irgendeine Razor-Regel aktiv ist (sonst Skip). `Run` ruft die Razor-Analyse
  zusaetzlich zu CSS und JS auf.

### 1.4 Tests (33 / 33 gruen)

- **`src/AiNetLinter.Tests/Web/RazorAnalyzerTests.cs`** (~290 Zeilen) — 15 Tests
  (Szenarien A-N + Code-Block + Szenario A). Verwendet `RazorTestConfig.Default.With(...)`
  Builder-Pattern, um 8-Parameter-Konstruktor zu vermeiden
  (siehe MaxMethodParameterCount-Regel: 4 in Produktion, 6 in Tests).
- **`src/AiNetLinter.Tests/Web/RazorAnalyzerTests.Extended.cs`** (~245 Zeilen) —
  18 Tests (Edge-Cases, deaktivierte Regeln, Szenarien-Kombinationen, Helper-Tests).
  Enthaelt `RazorTestConfig` static class + `RazorTestConfigExtensions.With(this RazorConfig, ...)`
  Convenience-Extension.
- **Test-Status:** `dotnet test --filter "FullyQualifiedName~RazorAnalyzer"` →
  **33 / 33 bestanden** (137 ms).

### 1.5 Eigene Code-Qualitaet (Dogfooding) verifiziert

Nach dem Refactoring (Aufteilung in `RazorAnalyzer.cs` + `RazorAnalyzer.Helpers.cs`,
Builder-Pattern statt 8-Parameter-Helper, `SkipStringContext`-Helper):

- `RazorAnalyzer.cs` = 246 Zeilen (< 500)
- `RazorAnalyzer.Helpers.cs` = 230 Zeilen (< 500)
- `RazorAnalyzerTests.cs` = ~290 Zeilen (< 500)
- `RazorAnalyzerTests.Extended.cs` = ~245 Zeilen (< 500)
- `CreateViolation`-Helper entfernt → RuleViolation inline in Check-Methoden (alle
  Check-Methoden nun <= 5 Felder pro `new RuleViolation { ... }`).
- `FindForeachBodyEnd` und `FindMatchingBrace` nutzen gemeinsamen `SkipStringContext`-
  Helper → kognitive Komplexitaet drastisch reduziert.

---

## 2. Was noch offen ist (Reihenfolge der naechsten Session)

### 2.1 Hoch (Pflicht fuer vollstaendigen Epic-Abschluss)

1. **`Docs/ROADMAP.md` — Epic 29 Phase 3 Abschnitt aktualisieren**
   - Status-Checkboxen von `[ ]` auf `[x]` setzen fuer:
     - NuGet-Abhaengigkeit (gestrichen — textbasierter Ansatz, keine externe
       Dependency noetig; Begruendung im Eintrag erwaehnen).
     - Konfigurations-Sektion `Web.Razor`.
     - `RazorAnalyzer`.
     - Acht Regel-IDs.
     - Project-Overrides (`RazorConfigOverride`).
     - Test-Suite (33 / 33 gruen).
     - Dogfooding (Refactoring abgeschlossen).
     - Dokumentation (Sektion 2.2 hier).
   - Hinweis: Go/No-Go-Kriterium entfaellt, da textbasierter Ansatz ohnehin keine
     Zeilennummern-Probleme hat.

2. **`Docs/configuration.md` — Web-Sektion um Razor erweitern**
   - Neue Zeilen in der Einstellungs-Tabelle (8 Properties mit Default + Typ + Beschreibung).
   - Neue Zeilen in der Regel-Tabelle (8 Regeln mit Severity / Intent / Beschreibung).
   - Neues Suppressions-Beispiel fuer Razor-Kommentare:
     `@* ainetlinter-disable RAZOR_MaxRazorLineCount *@`.
   - Hinweis: kein `ExemptPaths` fuer Razor (keine third-party-libs wie bei CSS/JS).

3. **`README.md` — Regel-Tabelle um Razor erweitern**
   - Zeile hinzufuegen mit Verweis auf `RAZOR_MaxRazorLineCount` etc.
   - Aufzaehlung der typischen KI-Fehlerquellen in Razor (Lost-in-the-Middle,
     Tag-Mismatch, Inline-Lambdas, Ternary).

### 2.2 Mittel (optional, aber empfohlen)

4. **CHANGELOG / Versionsnummer**
   - `src/AiNetLinter/AiNetLinter.csproj` `<Version>` von `1.0.58` auf `1.0.59`
     erhoehen.
   - Falls kein automatischer Changelog-Prozess existiert: manueller Eintrag im
     Release-Notes-Workflow.

5. **Cursor-Rules-Generator ueberpruefen**
   - `.cursor/rules/AiNetLinter.mdc` wird automatisch vom Linter generiert.
   - Beim naechsten Dogfooding-Run wird die neue Razor-Sektion automatisch in
     die generierte Datei einfliessen.
   - Pruefen, ob die generierte Datei nach einem erneuten Lint die Razor-Regeln
     korrekt erwaehnt.

### 2.3 Niedrig (Nice-to-have)

6. **Erweiterte Tests (optional)**
   - Integration-Test, der den gesamten `WebFileSeparationChecker` mit einer
     realen `.razor`-Datei aus dem Fixtures-Ordner testet.
   - Snapshot-Test fuer die generierten Violation-Details (um Guidance-Texte
     stabil zu halten).

7. **Beispiel-Fixture anlegen**
   - `tests/Fixtures/Razor/` mit einer `Counter.razor` + `.razor.css` + `.razor.cs`
     zum manuellen Testen via `dotnet run -- --path tests/Fixtures/Razor/`.

---

## 3. Bekannte Issues (NICHT durch Phase 3 verursacht)

### 3.1 Pre-existing CLI-Integration-Test-Fehler (4 Failures)

Folgende 4 Tests in `src/AiNetLinter.Tests/Cli/CliIntegrationTests.cs` schlagen
bereits vor meinen Aenderungen fehl und sind **nicht** durch die Razor-Phase 3
verursacht:

1. `GeneratePlaybook_WithCheckFlag_ReturnsOkWhenUpToDate`
2. `RunLinterCli_OnWholeSolution_ReturnsSuccess`
3. `SyncCursorRulesAndPlaybook_Combined_GeneratesBoth`
4. `GeneratePlaybook_ForSolution_GeneratesAndUpdatesPlaybook`

Diese Tests integrieren die CLI auf die eigene Codebase und scheitern vermutlich
an spezifischen Erwartungen (Playbook-Inhalte, Exit-Codes). **Sollten in einem
separaten Bugfix-Commit adressiert werden**, nicht in Phase 3.

### 3.2 Auto-regenerierte Dateien (nicht committen)

- `.cursor/rules/AiNetLinter.mdc` — wird durch `SyncCursorRulesCommand` auto-
  generiert; Aenderungen sind Folge des Dogfooding-Laufs, nicht manueller
  Source-Code.
- `.cursor/rules/playbook.md` — wird durch `RepoPlaybookGenerator` auto-generiert.
- `tests/Fixtures/BaselineMini/rules.json` — wird durch Baseline-Fixture-Setup
  generiert; Aenderung ist vermutlich ein Side-Effect, kein Source-Code.

Diese Dateien **NICHT** in den Phase-3-Commit aufnehmen.

---

## 4. Commit-Plan

```bash
git add \
  src/AiNetLinter/Configuration/WebConfig.cs \
  src/AiNetLinter/Configuration/LinterConfig.ValueTypes.cs \
  src/AiNetLinter/Core/LinterRuleIds.cs \
  src/AiNetLinter/Core/RuleRegistry.Web.cs \
  src/AiNetLinter/Web/RazorAnalyzer.cs \
  src/AiNetLinter/Web/RazorAnalyzer.Helpers.cs \
  src/AiNetLinter/Web/WebFileSeparationChecker.cs \
  src/AiNetLinter.Tests/Web/RazorAnalyzerTests.cs \
  src/AiNetLinter.Tests/Web/RazorAnalyzerTests.Extended.cs \
  rules.json

git commit -m "feat(web): implementiere Razor/Blazor-Linting (Epic 29 Phase 3)

..."
```

---

## 5. Quick-Resume-Checklist (zum Weitermachen)

```powershell
# Build prüfen
dotnet build src/AiNetLinter/AiNetLinter.csproj

# Nur Razor-Tests (sollten 33/33 gruen sein)
dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj --filter "FullyQualifiedName~RazorAnalyzer"

# Danach: Docs/ROADMAP.md, Docs/configuration.md, README.md aktualisieren
# Danach: optional <Version> in csproj auf 1.0.59
```
