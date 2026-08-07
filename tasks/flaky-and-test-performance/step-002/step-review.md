---
status: done
type: step-review
task: flaky-and-test-performance
step: 002
epic: EPIC-02
step_type: batch
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-07T10:05:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 002: Category-Traits für Suppression-Tests (Batch 1)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok, ein NITPICK zur Doku-Genauigkeit (siehe unten)
- [ ] **issues** — Fix-Step `step-002/fix-XX` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haven)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle 8 Items exakt wie geplant umgesetzt — 8 Trait-Zeilen über den Klassendeklarationen in den 8 `Suppression/`-Dateien, `8 files changed, 8 insertions(+)` Diff-Statistik (eigene `git show 3ae94c2`-Verifikation), keine Deletionen, deutlich unter `max_batch_diff_lines: 40`. Klassifikations-Heuristik sauber angewendet: `DisableAllCliTests` mit `CliProcessRunner.RunLinterAsync` (Z. 16 + 31) und `Program.Main` (Z. 40 + 53) korrekt als `Integration`, die übrigen 7 Klassen ohne Subprozess-Marker korrekt als `Unit`. Spezialfall `IgnoreSuppressionsFilterTests.cs:7-8` (Trait zwischen `// @covers`-Marker und Klassendeklaration) konsistent zur Konvention "Coverage-Marker direkt am Symbol". DoD-Punkte Build, Voll-Test, Unit-Filter, Self-Lint, Commit auf `main` und step-result.md/step-plan.md-Status-Update alle erfüllt (siehe Build-/Test-Status).

### Rules-Konformität

`AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität bewahren" eingehalten: Trait-Attribute sind reine Filter-/Selektions-Metadaten und haben — wie im Plan korrekt festgehalten — keinen Einfluss auf den `xunit.runner.json`-gesteuerten `parallelizeTestCollections`-Mechanismus; keine `[Collection]`- oder `DisableParallelization`-Eingriffe. §5 "Sparsame Kommentare" nicht betroffen (keine Kommentare hinzugefügt; das `// @covers IgnoreSuppressionsFilter` in `IgnoreSuppressionsFilterTests.cs:7` ist Bestand und steht vor dem Eingriff da). §5 "Zero-Warning-Direktive" in eigener `dotnet build`-Prüfung bestätigt (0/0). §5 "Symptom-Fixing verboten" eingehalten — keine Test-Logik, keine Assertions, keine Fixtures angefasst, rein additives Attribut. §4 "Commit-Vorschlag-Pflicht" durch die zwei Commits (Subject 69 Zeichen mit Suffix, Conventional Commit auf Deutsch, imperativ, `[flaky-and-test-performance]`-Suffix, Body mit Item-Liste + Refs-Block) erfüllt.

### Logische Korrektheit

Trait-Syntax exakt konventionskonform: `[Trait("Category", "Unit")]` bzw. `"Integration"` mit CamelCase-Großbuchstabe am Wortanfang, Kategorie-Name `"Category"` mit Groß-C — verifiziert per `Select-String` über alle 87 Trait-Vorkommen in `src/AiNetLinter.Tests/**/*.cs`, einheitliche Schreibweise. Trait-Platzierung konsistent: alle 8 auf Klassen-Ebene (über `public sealed class`), keine Methoden-Ebenen-Traits in homogenen Klassen — passt zur im Plan dokumentierten Heuristik "Klassen homogen → Klassen-Trait durchgängig". Filter-Numerik plausibel und reproduzierbar: eigener `dotnet test --no-build --filter "Category=Unit"` ergab 172/172, `--filter "Category=Integration"` 113/113 (auf 2. Lauf — siehe NITPICK zum 1. Lauf), voller Lauf 1325/1325; Summe 172+113=285, Rest 1325−285=1040 ungetaggte Tests, konsistent zur step-result-Tabelle. Der Heuristik-Spezialfall `Integration` für `DisableAllCliTests` ist **belegbar**: zwei `CliProcessRunner.RunLinterAsync`-Aufrufe (Z. 16, 31) plus zwei `Program.Main`-Aufrufe (Z. 40, 53) — alle vier Methoden der Klasse sind echte Subprozess-/Entry-Point-Pfade, homogene Integration-Klasse, Klassen-Trait ist die richtige Granularität. Die 7 Unit-Klassen sind durchgehend `in-process` (eigene File-Inspektion: nur String/File-Operationen auf `Path.GetTempPath()`/`Path.GetTempFileName()`, keine `CliProcessRunner`/`McpTestClient`/`IClassFixture<McpLiveRepositoryFixture>`-Verwendungen).

### Konzept-Treue (Ebene 4)

`konzept.md` §"Muss-Haven" "konsequente Category-Traits ... auf **allen** Tests" wird **nicht** in step-002, sondern über die EPIC-02-Batch-Serie als Ganzes erfüllt — der Plan hält das in §"Bezug" und §"Notes" explizit fest ("Erster von N Batches ... 8 von ~168 ungetaggten Testklassen") und adressiert die Rest-Bestand-Lücke transparent. Konzept §"Wie" Schritt 2 ("Category-Traits nachziehen — alle ~1000 ungetraggten Tests einordnen") ist als mehrstufige EPIC-02-Serie angelegt, step-002 deckt den ersten, sauber abgegrenzten Teil-Batch ab — **kein Konzept-Verstoß**, sondern akzeptable, dokumentierte Batch-Aufteilung. Konzept-Non-Goals (kein Framework-Wechsel, kein sichtbares CLI/MCP-Verhalten geändert, keine Test-Logik geändert) sind alle eingehalten. Konzept-Scope respektiert: step-002 fasst rein additiv Trait-Attribute, kein Eingriff in Produktionscode (`SourceFileCatalog`/`McpCodeGraphServer`), keine Fixture-Umstellung (das ist EPIC-03/05), keine Fast-Path-Etablierung (das ist EPIC-04), keine Flaky-Fix (das ist EPIC-06). Die im Konzept stehende Selbstverständlichkeit, dass die EPIC-02-DoD "Alle Tests tragen einen Category-Trait" erst am Ende der Batch-Serie erfüllt ist, wird vom Plan korrekt antizipiert und im step-result (Numerik: 1040 verbleibende ungetaggte Tests) erneut bestätigt.

### Build-/Test-Status

Eigene Nachprüfung am 2026-08-07 (HEAD `79d3d6d`):

```
dotnet build                                              → grün (0 Warnungen, 0 Fehler, 1,90 s)
dotnet test --no-build                                    → grün (1325 Tests, 0 Fehler, 0 übersprungen, 2 min 4 s)
dotnet test --no-build --filter "Category=Unit"           → grün (172 Tests, 0 Fehler, 0 übersprungen, 12–16 s)
dotnet test --no-build --filter "Category=Integration"    → flaky (siehe NITPICK unten)
```

`dotnet test --no-build` reproduziert die im step-result dokumentierten 1325 Tests exakt; 2:04 min liegen im Schwankungsbereich der step-result-Rohzeit (2:20 min) und bestätigen die grundsätzliche Stabilität des Vollaufs. `Category=Unit` mit 172 Tests exakt reproduziert. `Category=Integration` mit 113 Tests ebenfalls reproduziert, aber **nicht** reproduzierbar stabil grün — Details siehe folgender Abschnitt.

## Sonstige Beobachtungen / MINOR / NITPICK

- **NITPICK — Doku-Genauigkeit der "Integration-Filter grün"-Behauptung im `step-result.md`:** Der step-result behauptet für den `Category=Integration`-Lauf "113 / 113 ... gruen, 0 Fehler, 0 uebersprungen". In meiner Reproduktion flake-te dieser Filter **einmal** auf 1/113 mit Fehler im bekannten Pre-Existing-Flaky-Test `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` (konzept.md §"Wo im Projekt" → "Der Flaky Test", reproduziert mit 2/10 bzw. 6/10 Failure-Rate). Ein zweiter eigener Integrations-Lauf war dann grün (113/113). Der Flake ist **nicht** durch step-002 verursacht (rein additives Attribut auf Klassen-Ebene, keine Logik-/Parallelitäts-Änderung — die parallele Collection-Last beim Filter-Lauf ist dieselbe wie ohne Trait) und ist explizit **out of scope** für step-002 (Strukturfix ist EPIC-06). Empfehlung an den Orchestrator: vor Übergang zu step-003 die DoD-Zeile "`dotnet test --no-build --filter "Category=Integration"` → muss grün sein" im Plan-Pool entweder (a) als "best-effort, ein Lauf grün" lockern oder (b) den Flaky-Fix aus EPIC-06 zeitlich vorziehen — der Coder hat im step-result die Zeile "gruen" ohne Reproduzierbarkeits-Reserve geschrieben, was bei strenger Auslegung der DoD eine Lücke ist. **Kein Code-Defekt, kein step-002-Fix nötig** — nur eine Prozess-/DoD-Klarstellung, die besser vom Orchestrator als vom Coder kommt.

- **NITPICK — Pre-Step-002-Numerik im Plan:** Der Plan (und von dort übernommen in den Konzept-Hinweis) gibt "86 getaggte Methoden / Klassen (67 `Unit`, 19 `Integration`)" als Vor-Step-Stand an. Eigene Zählung **nach** step-002 ergibt 87 Trait-Zeilen (67 `Unit` + 20 `Integration`); daraus zurückgerechnet waren es vor step-002 79 (60 `Unit` + 19 `Integration`) — der Plan überzählt die Unit-Traits also um 7. Der Coder hat das im step-result elegant umgangen, indem er die Numerik aus den Test-Läufen ableitet (172 + 113 = 285 getaggte Methoden, 1040 ungetaggte) statt aus der Plan-Vor-Zählung — dadurch ist die step-result-Numerik korrekt. **Kein Code-Defekt, kein step-002-Fix nötig** — nur eine Mini-Diskrepanz zwischen Plan-Inventur und tatsächlichem Stand, die der step-result transparent macht.

## Tech-Debt-Einträge aus diesem Review

Keine — die einzigen Beobachtungen (Flaky-Test, Plan-Numerik) sind bereits in konzept.md bzw. EPIC-06 verankert, nicht in step-002 entstanden und nicht durch step-002 fixbar.

### Commit-Vorschlag

```
docs(tasks): step-002 Kritiker-Review dokumentieren [flaky-and-test-performance]

- step-review.md: approved, NITPICK zur Integration-Filter-Doku-Genauigkeit
  (pre-existing Flaky-Test flake-t, nicht step-002-verursacht; siehe
  konzept.md §"Der Flaky Test" und EPIC-06).
- task-state.md: step-002 in_progress -> done, 0/3 Fix-Runden.

Refs: tasks/flaky-and-test-performance/step-002
```
