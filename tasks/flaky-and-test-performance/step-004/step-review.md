---
status: done
type: step-review
task: flaky-and-test-performance
step: 004
epic: EPIC-02
step_type: batch
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-07T11:15:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 004: Category-Traits für `src/AiNetLinter.Tests/Web/` (Batch 3)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step angelegt
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haven)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Alle 5 Items exakt wie geplant umgesetzt — 5 Trait-Zeilen zwischen `</summary>` und `public sealed class` in den 5 `Web/`-Dateien, Diff-Statistik `5 files changed, 5 insertions(+)` (eigene `git show 57f7f03`-Verifikation), 0 Deletionen, deutlich unter `max_batch_diff_lines: 40`. Klassifikations-Heuristik sauber angewendet: keine Subprozess-Marker im `Web/`-Ordner (eigener Grep `McpTestClient`/`CliProcessRunner`/`Program\.Main`/`IClassFixture` — 0 Treffer), alle 5 Klassen korrekt als `Unit`. CodeMap-Korrektur korrekt umgesetzt: `Web/`-Eintrag listet jetzt 5 Klassen mit `RazorAnalyzerExtendedTests` (in `RazorAnalyzerTests.Extended.cs`) und Status `zuletzt: step-004` (eigene `git show ecd9dfa -- codemap.md`-Verifikation, `Korrektur in step-004`-Annotation sauber konsolidiert). `step-plan.md` Status auf `done (pending audit)` gesetzt. `///`-XML-Doc-Sections und `// @covers`-Marker (3 von 5 Dateien) unangetastet — Trait korrekt darunter eingefügt.

### Rules-Konformität

Die im `step-plan.md` unter „Rules-Refs" zitierte Auswahl (`AiNetLinterRichtlinien.mdc` §4 Testsuite-Parallelität, §5 sparsame Kommentare / Zero-Warning / Symptom-Fixing, §4 Commit-Vorschlag-Pflicht) ist vollständig eingehalten: Trait-Attribute berühren Parallelität nicht (nur `[Collection]`/`DisableParallelization` täten das, hier nicht verwendet), sind XML-Attribute keine Kommentare, folgen der exakten Schreibweise `[Trait("Category", "Unit")]` (verifiziert per Grep über 100+ bestehende Trait-Vorkommen im Projekt — gleicher CamelCase-Großbuchstabe), `dotnet build` ist mit 0 Warnungen grün (Zero-Warning-Direktive mit `TreatWarningsAsErrors=true` geprüft), Test-Logik unverändert (kein Symptom-Fixing). `### Commit-Vorschlag`-Block im `step-result.md` vorhanden.

### Logische Korrektheit

Klassifikation `Unit` für alle 5 Klassen ist semantisch korrekt: `RazorAnalyzer.Analyze(null!, …)` in `RazorAnalyzerTests.Extended.cs:31` ist ein in-process API-Edge-Test (verifiziert per eigener `Select-String`-Sichtung der Aufruf-Stelle + Konfiguration der Methode im Analyzer-Code) — passt zur etablierten Negativ-Abgrenzung "in-process API-Edge-Test" aus step-002, **kein** Subprozess-Marker. Numerische Plausibilität stimmt exakt: eigene `Select-String -Pattern '\[Fact\]'`-Zählung über `Web/` ergibt 15+20+15+18+6 = **74** `[Fact]`, 0 `[Theory]`, 0 `[InlineData]` — exakt deckungsgleich mit dem `dotnet test --filter "Category=Unit"`-Delta (278 nach step-004 vs. 204 nach step-003 = +74, **keine** numerische Abweichung). Die zwei unabhängigen Methoden (statische Grep-Zählung + dynamischer Filter-Lauf) bestätigen die Klassifikation. Keine Edge-Cases übersehen, keine Tests durch das Attributing funktional verändert.

### Konzept-Treue (Ebene 4)

Konzept-Muss-Haven "konsequente Category-Traits ... auf **allen** Tests" (`konzept.md` §Muss-Haven) wird durch diesen Step planmäßig vorangetrieben (5 weitere Klassen, 74 weitere Methoden, ~63 % Klassen-Fortschritt nach step-004). Keine Non-Goals berührt (kein Framework-Wechsel, keine CLI-/MCP-Verhaltensänderung, kein fester Zeit-Budget-Anspruch, kein CI-Workflow). Scope eingehalten — rein additives Attribut auf Klassen-Ebene, kein Eingriff in Produktionscode, in Fixtures oder in Test-Logik. EOL-/Trailing-NL-Status der 5 Dateien wurde byte-genau konserviert (eigene Verifikation: `RazorAnalyzerTests.cs` = LF+kein-Trail, `RazorAnalyzerTests.Extended.cs` = CRLF+kein-Trail, übrige 3 = LF+Trail — exakt wie vor dem Edit; die byte-genaue Python-Helper-Lösung statt eines naiven Edit-Tool war hier notwendig und korrekt, ohne sie wäre der Diff auf 577 Zeilen aufgebläht worden).

### Build-/Test-Status

```
dotnet build                                                         → grün (0 Warnungen, 0 Fehler, 2.28 s)
dotnet test (voll)                                                   → grün (1325/1325, 1 m 51 s)
dotnet test --filter "Category=Unit"                                 → grün (278/278, 14 s)
dotnet test --filter "Category=Integration"                          → grün (113/113, 1 m 55 s, 1. Versuch — kein Flake)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK
```

## Sonstige Beobachtungen / MINOR / NITPICK

- **TD-002-Disziplin-Fortschritt (positiv, kein TD-Eintrag):** Der Planer hat in diesem Step **konkret vorgegebene** Subject-Strings geliefert (Code 61 Zeichen, Doku 67 Zeichen) und der Coder hat **beide exakt übernommen** — eigene Verifikation per `git log --format='%s' -1 <hash> | ForEach-Object { $_.Length }` ergibt `61` bzw. `67`, beide unter der 72-Zeichen-Grenze aus `AiNetLinterRichtlinien.mdc` §4 / `spec.md` §10.3. Das ist der erste Step in diesem Task, in dem ein vom Planer vorgegebener Subject unverändert übernommen wurde (step-001/002/003 hatten Abweichungen, in TD-002 dokumentiert). Beleg für die in TD-002-Variante (a) empfohlene Vorgehensweise "Planer gibt Subject konkret vor" — kein neuer Eintrag nötig, der bestehende TD-002 bleibt offen und dokumentiert das Muster.
- **Coder-Notiz EOL-Helper (beobachtet, nicht eskaliert):** Der Coder hat in `step-result.md` §Beobachtungen dokumentiert, dass ein byte-genauer Python-Helper nötig war, um den gemischten EOL/Trailing-NL-Status zu konservieren. Coder weist explizit darauf hin, dass dies in einem Folge-Step (z. B. `Core/Checkers/`) erneut relevant werden könnte. Wie vom Coder richtig eingeordnet: kein Tech-Debt-Eintrag, sondern einmaliger implementationsspezifischer Workaround — Beobachtung zur Kenntnis genommen.
- **Self-Lint-Output-Zeitstempel `2026-08-07 11:14:20` weicht von Commit-Zeit `11:07/11:08` ab:** Konsistent damit, dass der Coder Self-Lint vor dem Commit lief; spielt für die Verifikation keine Rolle (`OK` ist `OK`).

## Tech-Debt-Einträge aus diesem Review

Keine.
