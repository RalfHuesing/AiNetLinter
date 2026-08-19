---
status: done
type: step-review
task: markdown-builder
step: 002
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-19
verdict: approved
tech_debt_ids: []
---

# Review Step 002: MarkdownTableBuilder zeilenweise API + EPIC-01 DoD präzisieren

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-002`)
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

Alle vier geplanten Änderungen umgesetzt: `MarkdownBuilder.cs` um `BuildHeaderLine`/`BuildSeparatorLine`/`BuildRowLine(params)` + privaten `FormatRow`-Helper erweitert, `AppendTo` auf diese Methoden refaktoriert, `using System.Linq` in `MarkdownBuilder.cs` (und korrekt in `GetViolationsScanner.cs` für `OrderBy`/`ThenBy` belassen); `GetViolationsScanner.AppendSection` Z.235-282 nutzt durchgängig `MarkdownTableBuilder` als Single-Source-of-Truth; 6 neue Unit-Tests in `MarkdownBuilderTests` (Z.308-378) decken Standard-/Pipe-Escape-/Alignment-/Cell-Padding-/Null-Cases ab; EPIC-01-DoD in `roadmap.md` Z.61 vom Orchestrator vorab präzisiert (verifiziert per `Select-String`).

### Rules-Konformität

`sealed` auf `MarkdownTableBuilder` (Z.16) und `MarkdownBuilder` (Z.106), `#nullable enable` (Z.1), `MaxMethodLineCount: 60` für alle neuen Prod-Methoden deutlich unterschritten (BuildHeaderLine 9 Z., BuildSeparatorLine 15 Z., BuildRowLine 10 Z., FormatRow 4 Z., AppendTo 10 Z. nach Refactor), `MaxLineCount: 500` klar eingehalten (MarkdownBuilder.cs 143 Z., GetViolationsScanner.cs 279 Z., MarkdownBuilderTests.cs 299 Z.); Testklasse `MarkdownBuilderTests` mit `[Trait("Category", "Unit")]` an der Klasse (Z.9) und **ohne** `sealed` (Z.10), neue Testmethoden alle 9-12 Z. (≤100); keine `// step-002` / `// EPIC-01` / Task-IDs in Code-Kommentaren (per `Select-String` über alle drei Dateien verifiziert); Zero-Warning-Direktive eingehalten (0 Warnings, 0 Errors).

### Logische Korrektheit

`table.Build()` bleibt byte-stabil (verifiziert via unveränderter `VollstaendigeTabelle_SnapshotDesOutputs` Z.132-150, grün); 16/16 `GetViolationsToolTests` grün — byte-stabile Verifikation der `AppendSection`-Reihenfolge Zeile → Snippet → Zeile → Snippet; 6 neue Unit-Tests verriegeln die API gegen Drift: HeaderLine escapt Header-Pipes korrekt, SeparatorLine emittiert `:---|`/`---:|`/`:---:|` exakt, RowLine escapt Cell-Pipes und füllt fehlende/null Cells konsistent mit `AddRow` zu `-`; 31/31 `ViolationMarkdownFormatterTests` grün (`| EnforceSealedClasses | 1 | 1 | 0 | - |` Z.274 unverändert, `EscapeCell`-konform gedeckt durch präzisierte DoD).

### Konzept-Treue (Ebene 4)

Finding #1 aus `step-001/step-review.md` vollständig aufgelöst — die in `GetViolationsScanner.AppendSection` beanstandete Inline-`string.Join` + `align switch`-Reimplementierung ist entfernt; Alignment-Format-Strings (`:---|`, `---:`, `:---:`) sind jetzt **nur noch** in `MarkdownTableBuilder.BuildSeparatorLine` (`MarkdownBuilder.cs:55-60`) definiert, `GetViolationsScanner.cs` enthält keine `align switch`-Konstruktion mehr (per `Select-String` bestätigt: null Treffer für `Select(`/`align switch`-Pattern in beiden Dateien); Finding #2 durch die vom Orchestrator separat angewendete DoD-Präzisierung in `roadmap.md` Z.61 („strukturell byte-stabil" + „`EscapeCell`-konform") sauber abgedeckt — der 1-Byte-Drift in `BuildSummaryTable` (leer/whitespace → `-`) ist jetzt Vertragskonformität, kein Findings-Auslöser mehr. Keine Non-Goals aus `konzept.md` umgesetzt, kein Muss-Haben-Punkt ausgelassen. **Anmerkung zum Konzept-Beispiel in §3 Prio 2 (Z.262-308):** Das dort gezeigte „Nach"-Beispiel mit `mb.Table(table)` am Schleifenende ist im Konzept selbst fehlerhaft (würde Snippets vor die Tabelle emittieren, nicht interleaved) — die step-001/002-Implementierung weicht mit Recht ab und behält die korrekte Zeile→Snippet→Zeile→Snippet-Reihenfolge. Die zeilenweise API-Erweiterung in step-002 ist genau der saubere Weg, dies Single-Source-of-Truth-konform zu lösen, ohne den semantischen Fehler des Konzept-Beispiels zu reproduzieren. Kein Findings-Auslöser.

### Build-/Test-Status

```
dotnet build                                                                                            → grün (0 Warnungen, 0 Fehler, 14 s)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress                                         → grün (1428 Tests, 0 Fehler, 8 s) — Plan-DoD ≥1428 erfüllt (1422 aus step-001 + 6 neu)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~MarkdownBuilderTests                  → grün (30/30, +6 neu)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~GetViolationsToolTests                → grün (16/16, byte-stabile Verifikation AppendSection)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ViolationMarkdownFormatterTests       → grün (31/31, EscapeCell-Vertrag bestätigt)
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~CliRepositoryDogfoodTests|FullyQualifiedName~BaselineCliTests|FullyQualifiedName~McpServerCommandStaleness|FullyQualifiedName~GetImpact" → grün (20/20, 54 s)
dotnet run -- --config rules.json --path .                                                              → grün (Dogfood sauber)
```
