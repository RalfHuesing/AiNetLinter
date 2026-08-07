---
status: done
type: step-review
task: flaky-and-test-performance
step: 008
epic: EPIC-02
step_type: batch
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-07T16:25:00+02:00
verdict: approved
tech_debt_ids: [TD-004]
---

# Review Step 008: Category-Traits für Output-Tests Teil 2/2 (Batch 7)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-<NNN>`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle vier Items umgesetzt — `git show 95ab4d5 --stat` bestätigt exakt 4 Dateien / 4 `+`-Zeilen / 0 `-`-Zeilen in `PathNormalizerTests.cs:5` (Standard-Insert), `RuleLegendRegistryTests.cs:13` (XML-Doc-Variante, korrekt zwischen `</summary>` Z. 12 und Klasse Z. 14), `ViolationMarkdownFormatterTests.cs:8` (Standard-Insert, Heavyweight 473 → 474 Z.) und `ViolationSummaryBuilderTests.cs:6` (Standard-Insert); `TestLintConsole.cs` (Helper, Heuristik-Punkt 6) und die 5 step-007-Dateien sind erwartungsgemäß nicht im Diff; `codemap.md` `Output/`-Eintrag auf `zuletzt: step-008` finalisiert, Schnitt-Annotation entfernt, Heuristik-Punkt 6 als abgehakt verlinkt; step-006-Planer-Hypothese (`ListEvalsCommandTests`-Subprozess-Vermutung) bleibt im `Evals/`-Eintrag korrekt als widerlegt vermerkt.

### Rules-Konformität

Trait-Attribute folgen der etablierten Konvention `[Trait("Category", "Unit")]` mit CamelCase-Großbuchstabe; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` bleibt grün (0 Warnungen, 0 Fehler); Code-Commit-Subject **68** Zeichen, Doku-Commit-Subject **67** Zeichen — beide unter der 72-Zeichen-Grenze aus `AiNetLinterRichtlinien.mdc` §4 / `spec.md` §10.3 (eigene Verifikation per `git log --format='%s' -1 <hash> | ForEach-Object { $_.Length }` → 68 / 67, TD-002-Disziplin weiterhin gehalten); keine `step-`/`TD-`/`EPIC-`-Verweise im neuen Code; `*.Tests`-Overrides aus `AiNetLinter.mdc` (`MaxMethodLineCount: 100`, `EnforceSealedClasses: false`) greifen — die 4 `sealed`-Klassen sind regelkonform, die längste Methode in `ViolationMarkdownFormatterTests.cs` ist 24 Z. (deutlich unter 100).

### Logische Korrektheit

Eigener Subprozess-Marker-Grep (`Process\.Start|McpTestClient|CliProcessRunner|Program\.Main|IClassFixture`) liefert 0/0/0/0 Treffer pro Datei — bestätigt die Unit-Klassifikation für alle 4 Klassen; eigene Nachzählung per `Select-String -Pattern '\[(Fact|Theory)\]'` ergibt 4+5+30+4 = **43** Methoden, exakt deckungsgleich mit Planer-Prognose und Coder-Bericht; per-Klasse-Filter-Lauf bestätigt die `[Theory]+[MemberData]`-Expansion in `RuleLegendRegistryTests` (3×59 = 177 Cases + 2 Facts = **179** Test-Cases) und damit das Filter-Delta **+221** (8+179+30+4); **gesamter `Output/`-Ordner mit step-008 vollständig abgeschlossen** (9 Test-Klassen + 1 Helper, Heuristik-Punkt 6 nach 2. Anwendung ohne Ausnahme bestätigt = vollständig abgehakt); EOL/BOM-Status der 4 editierten Dateien korrekt konserviert (uniform CRLF, kein UTF-8-BOM, Trailing-NL — verifiziert per `[System.IO.File]::ReadAllBytes` Byte-Scan: `PathNormalizerTests` 48/48/0/`\n`, `RuleLegendRegistryTests` 67/67/0/`\n`, `ViolationMarkdownFormatterTests` 474/474/0/`\n`, `ViolationSummaryBuilderTests` 94/94/0/`\n`); TD-003 (LF-only `McpLintConsoleTests.cs`, step-007) bleibt nach step-008 erwartungsgemäß unverändert (eigene Byte-Scan-Verifikation: `CR=0 LF=63`); Planer-Annahme im step-plan §"item-02" ("Leerzeile Z. 13 zwischen `</summary>` und Klasse") vom Coder korrekt als Planer-Unschärfe erkannt (Realität: keine Leerzeile, direkt adjacent) — Trait-Ergebnis (Z. 13 Trait, Z. 14 Klasse) ist funktional identisch zur Planer-Erwartung.

### Konzept-Treue (Ebene 4)

Liefert genau den von `konzept.md` §"Wie" Schritt 2 / §"Muss-Haven" Traits-Punkt geforderten Beitrag zur EPIC-02-DoD „Alle Tests tragen einen Category-Trait" — siebter und letzter der alphabetisch halbierten `Output/`-Batches (5+4 = 9, mit step-007 D–O done, step-008 P–V done), reine Schließung des `Output/`-Ordners ohne Mischung mit `Configuration/` o. ä.; kein Non-Goal aus `konzept.md` verletzt, kein Muss-Haben-Punkt übergangen, der `KnownRuleNames.Count = 59` (für `RuleLegendRegistryTests`-Expansion) ist korrekt — eigene Zählung der `new(`-Literale in den 4 `partial class RuleRegistry`-Dateien ergibt 18+4+22+15 = **59** (Planer-Wert verifiziert, kein `Warum: null` in den 4 Dateien).

### Build-/Test-Status

```
dotnet build                                                      → grün (0 Warnungen, 0 Fehler)
dotnet test --no-build (voller Lauf)                              → grün (1325/1325, 0 Fehler, 1 m 48 s)
dotnet test --no-build --filter "Category=Unit"                   → grün ( 589/ 589, 0 Fehler,   18 s)  [Baseline step-007: 368 → +221 ✓]
dotnet test --no-build --filter "Category=Integration"            → grün ( 113/ 113, 0 Fehler, 1 m 44 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK
```

Per-Klasse-Verifikation (eigener Filter-Lauf, `--filter "FullyQualifiedName~<Klasse>&Category=Unit"`):
`PathNormalizerTests` **8/8** ✓, `RuleLegendRegistryTests` **179/179** ✓, `ViolationMarkdownFormatterTests` **30/30** ✓, `ViolationSummaryBuilderTests` **4/4** ✓ — Summe **221 = +221** Delta, exakt Planer-Prognose.

## Sonstige Beobachtungen / MINOR / NITPICK

- **TD-002-Disziplin (Subject ≤ 72 Z.)** zum 5. aufeinanderfolgenden Mal in diesem Task eingehalten (step-002 Doku 71 Z., step-003 Code 85 Z. — siehe TD-002, step-005 Code 47 Z., step-006 Code 53 Z., step-007 Code 68 / Doku 67 Z., **step-008 Code 68 / Doku 67 Z.**) — kein neuer TD-Eintrag (TD-002 dokumentiert das Muster bereits).
- **Coder-Bericht im Detail korrekt:** die im step-result.md §"Numerische Plausibilitätsprüfung" angegebenen Zahlen 8+179+30+4 = 221 Test-Cases, 43 Methoden, +221 Filter-Delta, 589 Unit-Tests sind alle eigenständig verifiziert — keine der im Coder-Bericht befürchteten Off-by-1- oder 5×59+8+4+30=297-Fehlerrechnungen ist aufgetreten.

## Tech-Debt-Einträge aus diesem Review

- `TD-004` (siehe `tech-debt.md`) — `#nullable enable`-Inkonsistenz in 5 von 10 `Output/`-Test-Dateien (pre-existing, durch Coder in step-result §"Bekannte Unschärfen" benannt).
