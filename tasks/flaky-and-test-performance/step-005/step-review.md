---
status: done
type: step-review
task: flaky-and-test-performance
step: 005
epic: EPIC-02
step_type: batch
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-07T11:38:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 005: Category-Traits für 4 kleine Unit-Ordner (Arch/Diag/FP/Cache, 7 Klassen, Batch 4)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-005`)
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

Alle 7 Items exakt wie geplant umgesetzt — je eine `[Trait("Category", "Unit")]`-Zeile in den vorgegebenen 4 Platzierungs-Varianten (3× direkt über `public sealed class`, 2× zwischen `</summary>` und Klasse, 1× zwischen `// @covers`-Block und Klasse, 1× zwischen `</summary>` und Klasse bei `AnalysisCacheManagerIsolationTests` mit `: IDisposable`); `git show b15a198` belegt `7 files changed, 7 insertions(+)` und jede Hinzufügung am vorgegebenen Ort (Z. 9 / 14 / 14 / 16 / 18 / 16 / 21). Subprozess-Marker-Grep (`McpTestClient`/`CliProcessRunner`/`Program\.Main`/`IClassFixture<…>`) in den 7 Dateien liefert 0 Treffer, alle 7 Klassen korrekt als `Unit`. Die 4 bestehenden method-level `[Trait("Category", "Unit")]` in `AnalysisCacheManagerIsolationTests.cs` sind unverändert (Z. 29/49/67/87 nach Edit = Z. 28/48/66/86 vor Edit + 1 Zeile für den Klassen-Trait) — also rein additiv, keine Doppelt-Zählung im Filter. BOM/EOL/Trailing-NL-Status in allen 7 Dateien identisch zum Plan (3 mit BOM + 4 ohne, alle CRLF + Trailing-NL, kein Drift). `step-plan.md` Status auf `done (pending audit)`. `codemap.md` im Doku-Commit sauber aktualisiert: 4 Verzeichnis-Einträge auf `zuletzt: step-005`, `last_updated` auf 11:30 fortgeschrieben, Cache-Beschreibung um den additiv-Hinweis auf die 4 method-level Traits erweitert.

### Rules-Konformität

Die im `step-plan.md` unter „Rules-Refs" zitierte Auswahl (`AiNetLinterRichtlinien.mdc` §4 Testsuite-Parallelität, §5 sparsame Kommentare / Zero-Warning / Symptom-Fixing, §4 Commit-Vorschlag-Pflicht) ist vollständig eingehalten: Trait-Attribute berühren Parallelität nicht (nur `[Collection]`/`DisableParallelization` täten das, hier nicht verwendet), sind XML-Attribute keine Kommentare, folgen der exakten Konvention `[Trait("Category", "Unit")]` mit CamelCase-Großbuchstabe (eigene Verifikation über alle 11 in den 7 Dateien vorhandenen `[Trait(`-Vorkommen), `dotnet build` mit 0/0 (Zero-Warning mit `TreatWarningsAsErrors=true` in beiden Projekten), Test-Logik unverändert (kein Symptom-Fixing, keine Assertions, keine Fixtures angefasst — rein additives Attribut), `### Commit-Vorschlag`-Pflicht über die zwei Commits (Code-Subject 65 / Doku-Subject 67, beide unter 72-Grenze, explizite Längen-Doku im `step-result.md`) erfüllt.

### Logische Korrektheit

Klassifikation `Unit` für alle 7 Klassen semantisch korrekt: `ArchitectureTests`/`FalsePositiveTests`/`FalsePositiveExtensionsTests` arbeiten auf in-process `CSharpCompilation` + `LinterAnalyzer.Analyze(...)` / `AIContextFootprintCalculator.Calculate(...)` (kein Subprozess), `PerformanceProfilerTests` auf in-process `PerformanceProfiler` + `ConfigLoader.TryLoadConfig(...)` + `File.WriteAllText`/`ReadAllText` auf `AppDomain.CurrentDomain.BaseDirectory`-Subpfad, `AnalysisCacheManagerTests` / `AnalysisCacheManagerIsolationTests` / `CacheEntryMapperTests` auf in-process `AnalysisCacheManager.Load(...)` / `CacheEntryMapper.To*` + `TestTempDirectory` (kein Subprozess, das `: IDisposable` ist nur Temp-Dir-Cleanup). Numerische Plausibilität exakt: eigene `Select-String -Pattern '\[Fact\]'`-Zählung über die 7 Dateien ergibt 13+3+15+12+7+4+4 = **58** Facts, 0 Theories (Plan-Erwartung: 58, Übereinstimmung). Eigene Filter-Läufe reproduzieren die im Coder-Bericht dokumentierten Zahlen exakt: Unit 332 (Δ+54 = 278 + 58 − 4 method-level-Vorab-Tagging), Integration 113 (unverändert), Total 1325 (unverändert) — die zwei unabhängigen Methoden (statische Grep-Zählung + dynamische Filter-Läufe) bestätigen die Klassifikation übereinstimmend. Heuristik-Fortschreibung Punkt 4 (Klassen-Trait additiv zu bestehenden method-level Traits bei homogenen Klassen) sauber praktiziert: keine Doppelt-Zählung (xUnit wertet Klassen-Oder-Methoden-Trait), zukünftige neue Methoden in `AnalysisCacheManagerIsolationTests` werden automatisch vom Filter erfasst.

### Konzept-Treue (Ebene 4)

`konzept.md` §"Muss-Haven" "konsequente Category-Traits ... auf **allen** Tests" wird planmäßig vorangetrieben — 7 weitere Klassen, 58 weitere Methoden, 4 weitere Verzeichnisse vollständig abgehakt, der in step-002 angelegte "Reine-Unit-Ordner, klein"-Block in der CodeMap damit vollständig abgeschlossen (analog zu step-004 für `Web/`). Keine Non-Goals berührt (kein Framework-Wechsel, keine CLI-/MCP-Verhaltensänderung, kein fester Zeit-Budget-Anspruch, kein CI-Workflow in dieser Aufgabe). Scope eingehalten — rein additives Attribut auf Klassen-Ebene, kein Eingriff in Produktionscode, keine Fixture-Umstellung (EPIC-03), keine Fast-Path-Etablierung (EPIC-04), keine Flaky-Fix (EPIC-06). Test-Abdeckung quantitativ unverändert (Total 1325 grün, keine Tests gestrichen/abgeschwächt) — Konzept-DoD-Punkt "Kein Testabdeckungsverlust" eingehalten.

### Build-/Test-Status

```
dotnet build                                                         → grün (0 Warnungen, 0 Fehler, 2,22 s)
dotnet test --no-build                                               → grün (1325/1325, 0 Fehler, 2 m 17 s)
dotnet test --no-build --filter "Category=Unit"                      → grün (332/332, 0 Fehler, 22 s)
dotnet test --no-build --filter "Category=Integration"               → grün (113/113, 0 Fehler, 1 m 50 s, 1. Versuch — kein Flake)
dotnet run --project src/AiNetLinter -- --config rules.json --path .  → OK
```

Eigene Nachprüfung am 2026-08-07 (HEAD `fe95a08`). Alle vier Coder-seitigen Test-Zahlen exakt reproduziert (1325/332/113). Rohzeit-Schwankungen innerhalb des erwarteten Bereichs (Voll 2:17 vs Coder 1:49, Unit 22 s vs 14 s, Integration 1:50 vs 1:58 — Differenzen durch CPU-/IO-Last erklärbar). Self-Lint `# Run: 2026-08-07 11:37:24 / OK` reproduziert.

## Sonstige Beobachtungen / MINOR / NITPICK

- **TD-002-Disziplin-Trend bestätigt (positiv, kein TD-Eintrag):** Der Planer hat in diesem Step **konkrete Subject-Strings mit korrekter Längenangabe** vorgegeben (Code 65 Zeichen, Doku 67 Zeichen) und der Coder hat beide **unverändert übernommen** — eigene Längenmessung mit `('test: 4 Unit-Ordner Kategorie-taggen [flaky-and-test-performance]').Length = 65` und `('docs(tasks): step-005 Result + CodeMap [flaky-and-test-performance]').Length = 67` bestätigt, beide mit Sicherheitsabstand unter der 72-Zeichen-Grenze aus `AiNetLinterRichtlinien.mdc` §4 / `spec.md` §10.3. Folge der TD-002-Empfehlung Variante (a) "Planer gibt Subject konkret vor" — die in step-004 erstmalig erfolgreich umgesetzte Disziplin wird in step-005 nahtlos fortgeführt. Kein neuer TD-Eintrag, TD-002 bleibt offen und dokumentiert das Muster weiter.

- **Numerische Plausibilität exakt (NITPICK-Linie aus step-003, hier sauber):** Eigene Regex-Zählung (`Select-String -Pattern '\[Fact\]'`) ergibt **58** Facts über die 7 Dateien (13+3+15+12+7+4+4), Plan-Erwartung 58 Facts, Coder-Bericht 58 Facts — alle drei Werte stimmen exakt überein. Das in step-003 vom Coder falsch berechnete Off-by-1 (8 statt 9 für `MaxDirectoryChildrenTests`) ist hier nicht reproduziert; die Coder-Diszplin "regex statt manuell" zahlt sich aus. Die Filter-Δ-Ableitung `58 − 4 method-level = +54` ist sauber hergeleitet und reproduziert sich exakt im `dotnet test --filter "Category=Unit"`-Lauf (278 → 332).

- **`AnalysisCacheManagerIsolationTests`-Spezialfall korrekt umgesetzt:** 4 method-level Traits (Z. 29/49/67/87 = vorher Z. 28/48/66/86 + 1 Zeile für den Klassen-Trait) sind unverändert erhalten; Klassen-Trait auf Z. 21 zwischen `</summary>` und `public sealed class … : IDisposable` korrekt eingefügt. Insgesamt 5 `[Trait(`-Vorkommen in der Datei (1 Klassen-Level + 4 method-level) = Erwartung exakt erfüllt, keine Doppelt-Zählung im Filter (`5. Trait-Vorkommen → 4 effektive Unit-Klassifikationen` — Klassen-Trait subsumiert die 4 method-level).

- **Self-Lint-Zeitstempel `2026-08-07 11:37:24` weicht von Commit-Zeit `11:29/11:30` ab:** Konsistent damit, dass ich Self-Lint nach dem Commit zur Verifikation laufen ließ; spielt für die Verifikation keine Rolle (`OK` ist `OK`).

## Tech-Debt-Einträge aus diesem Review

Keine — im rein additiven Scope von step-005 (7 Trait-Zeilen auf 7 Klassen) ist kein architektur-/anti-pattern-relevanter Fund außerhalb des Step-Scopes zu erwarten, und die Beobachtungen decken sich mit den bereits dokumentierten Mustern (TD-002 Disziplin-Trend, NITPICK-Linie zur numerischen Plausibilität). `McpTestClientParallelTests.ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly` als Long-Running-Test im Volllauf (1:17–1:23 in den 3 parallelen `[Long Running Test]`-Logs) entspricht exakt der Erwartung aus step-002/003/004 — kein Schritt-verursachter Effekt, EPIC-06-Kontext.
