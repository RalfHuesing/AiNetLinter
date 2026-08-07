---
status: done
type: step-review
task: flaky-and-test-performance
step: 010
epic: EPIC-02
step_type: batch
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-07T14:30:00+02:00
verdict: approved
tech_debt_ids: [TD-006]
---

# Review Step 010: Category-Traits für Core/Checkers-Tests nachziehen (Batch 9, Core/Checkers Teil 1/3)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-010`)
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

Alle 8 items (AsciiIdentifiersTests, AsyncVoidCheckerTests, BlockingTaskCheckerTests, CouplingSemanticTests, DynamicTypeCheckerTests, LinqChainLengthCheckerTests, MaxPartialClassFilesTests, MethodParameterCountAccessibilityTests) mit `[Trait("Category", "Unit")]` auf Klassen-Ebene zwischen `namespace` und `class` versehen; BOM-Konservierung für alle 5 BOM-Dateien (erste 3 Bytes = `EF BB BF` vor/nach Edit) verifiziert; EOL uniform CRLF + Trailing-NL für alle 8 erhalten; numerische Plausibilität (Methoden 50 = Brutto 50 = Netto 50, Filter-Delta Unit 656 → 706 exakt +50) bestätigt; CodeMap-`Core/Checkers/`-Eintrag auf den Pre-Existing-Tag-Befund (7 vorab-getaggte Klassen aus `[codegraph-mcp-finish]`-Refactoring-Commits, 20 ungetaggte in 3 Batches 8+8+4) aktualisiert; `status` auf `done (pending audit)`.

### Rules-Konformität

`AiNetLinter.mdc` `EnforceSealedClasses: false` für `*.Tests`-Override, `MaxMethodLineCount: 100` (Test-Override) und `EnforceNullableEnable` sind eingehalten (alle 8 Klassen `public sealed class`, alle 8 Dateien mit `#nullable enable` am Dateianfang — bei 5 BOM-Dateien direkt nach `EF BB BF`); `AiNetLinterRichtlinien.mdc` §4 Subject-Disziplin (70/67 Zeichen, beide unter der 72-Grenze), §5 (keine `step-NNN`/`TD-XXX`/`EPIC-XX`-Verweise im Code — keine Code-Kommentare verändert) und §6 Zero-Warning-Direktive (Build 0/0) sind eingehalten.

### Logische Korrektheit

Trait-Syntax exakt `[Trait("Category", "Unit")]` mit CamelCase-„U" (analog step-002/007/008/009-Konvention); BOM-Konservierung mechanisch sauber (Standard-Edit-Tool erhält die ersten 3 Bytes durchgängig, keine Sonderbehandlung nötig); die String-Literal-`[Fact]`-Ausschluss-Methodik (NITPICK-Linie aus step-009) ist konsequent angewendet: 0/8 step-010-Dateien mit Verschachtelung (eigener Roh-String-Scan reproduziert das Ergebnis, einzige Datei im Ordner mit `StringLiteralFact: TRUE` ist `MaxPublicMembersPerTypeTests.cs` — außerhalb step-010-Scope, bereits getaggt); das Pre-Existing-Tag-Inventar ist plausibel (15 Klassen mit `[Trait(` im Ordner = 8 step-010 + 7 vorab-getaggt, konsistent mit der Planer-Aussage); keine Subprozess-Marker im `Core/Checkers/`-Set (alle homogen Unit).

### Konzept-Treue (Ebene 4)

Konzept §"Muss-Haven"-Punkt „konsequente Category-Traits … auf **allen** Tests" wird mit 50 weiteren getaggten Tests vorangetrieben (EPIC-02-Fortschritt, 656 → 706 Unit-Tests); Konzept §"Wie" Schritt 2 („alle ~1000 ungetraggten Tests einordnen") wird mit dem 8+8+4-Schnitt im 8-Item-Deckel planmäßig bedient; keine Non-Goals verletzt (insbesondere: kein Wechsel des Test-Frameworks, keine sichtbare Verhaltensänderung am MCP-Server/CLI, keine CI-Workflow-Einführung); Scope entspricht exakt der Plan-Intention (8 von 20 ungetaggten Klassen in `Core/Checkers/`, 1. von 3 alphabetischen Teilbatches).

### Build-/Test-Status

```
dotnet build                                       → grün (0 Warnungen, 0 Fehler)
dotnet test --no-build                             → grün (1325 Tests, 0 Fehler, Dauer 1 m 48 s)
dotnet test --no-build --filter "Category=Unit"    → grün (706 Tests, 0 Fehler, Dauer 10 s)
dotnet test --no-build --filter "Category=Integration" → Re-Run grün (113 Tests, 0 Fehler, Dauer 1 m 45 s) — 1. Lauf mit 1 Fehler (pre-existing Flake, DoD-erlaubt)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK (Self-Lint)
```

## Sonstige Beobachtungen / MINOR / NITPICK

- **TD-002-Disziplin (Subject ≤ 72 Zeichen), 7. aufeinanderfolgender Step mit dieser Disziplin:** Code-Commit `44956b7` 70 Zeichen, Doku-Commit `2674a46` 67 Zeichen — beide komfortabel unter der 72-Grenze, exakt wie im DoD vorgegeben. Kein neuer TD-Eintrag (nur Fortschreibung der bestehenden Beobachtung in `tech-debt.md`).
- **Integration-Filter-Lauf — pre-existing Flake bestätigt:** der erste Integration-Lauf zeigte 1 Fehler in `McpServerCommandLoadingStateTests` (vermutlich der im Plan genannte `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` — Poll-Loop-5s-Deadline, EPIC-06-Ziel). Der **Re-Run** (per DoD erlaubt) war **sauber: 113/113 grün**. Damit ist die DoD-Vorgabe „best-effort, 1 Re-Run erlaubt" eingehalten; die Fehlersignatur passt zur Konzept- und step-001-Diagnose (kein step-010-Issue).
- **Markdown-Dateien von LF auf CRLF normalisiert:** der Coder hat im Doku-Commit `2674a46` zusätzlich `step-plan.md`, `step-result.md` und `codemap.md` auf CRLF konvertiert (alle Repo-Markdown-Dateien uniform CRLF + Trailing-NL ist die Repo-Konvention, die der Coder im step-result nicht eigens dokumentiert hat — bewusste Aufräum-Aktion, kein Side-Effect). Konsistent mit der `AiNetLinterRichtlinien.mdc` §5-Sub-Empfehlung „bei ohnehin berührtem Auftreten einer bestehenden Verletzung darf im selben Zug entfernt werden".
- **String-Literal-`[Fact]`-Linie (NITPICK-Linie aus step-009) konsequent angewendet:** unabhängig vom Coder reproduziert (eigener Roh-String-Scan über alle 27 `Core/Checkers/`-Dateien: 0 step-010-Treffer, 1 Treffer in `MaxPublicMembersPerTypeTests.cs:241` — außerhalb step-010-Scope, bereits getaggt). Damit ist der `MaxPublicMembersPerTypeTests`-Befund für den Folge-Planer (step-011/012) als `Bekannte Ausnahme` im CodeMap-`Core/Checkers/`-Eintrag dokumentierungs-würdig (kein aktueller Step-Scope, kein neuer TD-Eintrag — bestätigt nur, dass die Linie auch im aktuellen Batch hält).

## Tech-Debt-Einträge aus diesem Review

- `TD-006` (siehe `tech-debt.md`) — UTF-8-BOM-Inhomogenität in `Core/Checkers/` (10 von 27 mit BOM, 17 ohne = 37 %/63 %); dritte Inhomogenitäts-Dimension nach `Output/` und `Configuration/`; byte-genau konserviert in step-010, keine funktionale Auswirkung, `auto_fixable: nein`.
