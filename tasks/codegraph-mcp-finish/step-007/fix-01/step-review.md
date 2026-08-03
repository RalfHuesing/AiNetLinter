---
status: done
type: step-review
task: codegraph-mcp-finish
step: 007/fix-01
epic: EPIC-02
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-03
verdict: approved
tech_debt_ids:
  - TD-005
  - TD-006
---

# Review Step 007/fix-01: TD-Referenzen + Satzreste aus 3 Produktionsdateien + 5 Test-Dateien entfernt

## Verdict

- [x] **approved** — alle vier Prüfebenen ok, keine CRITICAL/MAJOR-Findings
- [ ] **issues** — Fix-Step nötig
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

### Plan-Erfüllung

Alle 3 MAJOR-Findings aus `step-007/step-review.md` (TD-009 + `units/011/plan.md`-Verweis in `McpCodeGraphServerOptions.cs:9-15,32-38`; TD-009 in `McpCodeGraphServer.cs:29-32`; TD-014-Slot in `McpServerOptionsFactory.cs:8-15`) sind in `cf3d7ac1` sauber beseitigt — alte Tokens weg, neue ID-freie *Why*-Texte eingefügt. MINOR-Mitnahme (vom Plan als optional markiert) umgesetzt: 5 Test-Dateien analog aufgeräumt, `McpTestClientRetryOptions.cs` korrekt ausgelassen (Doc bereits sauber, im step-result.md begründet). Sprachliche Glättung im Planvorschlag für `McpCodeGraphServerOptions.From()` (Streichung der nicht durch Code verifizierbaren Zahl „65 Call-Sites") ist Coder-initiierte Mini-Abweichung mit dokumentierter Begründung — Inhalt der *Why*-Substanz unverändert. Commit-Format konventionskonform (Conventional Commit auf Deutsch, imperativ, Task-Suffix `[codegraph-mcp-finish]`).

### Rules-Konformität

§5 (`AiNetLinterRichtlinien.mdc`) eingehalten — die neuen Wording-Vorschläge verweisen ausschließlich auf regel-dateien (`AiNetLinter.mdc`, `AiNetLinterRichtlinien.mdc §2`) und Code-Symbole (`<see cref="McpCodeGraphServer"/>` etc.), keine TD-NNN, keine `units/011/plan.md`, keine `step-NNN`, keine `EPIC-NN`. Auch die zuvor abgeschnittenen Satzreste (`Eingefuehrt mit`/`lag`/`und McpCodeGraphServerOptions.cs).`, `exakt erreichte —`, `aufgeteilt : haette`, schwebendes `<c>` in `Create()`) sind in allen 8 Dateien sauber zu vollständigen Sätzen geschlossen. Grep gegen `src/AiNetLinter/Mcp` und `src/AiNetLinter.Tests/Mcp` liefert für `TD-(009|014|019)|units/011|plan-Abweichung|Eingefuehrt mit` keine Treffer. Die §5-erlaubte Ausnahme „Aufräumen im selben Zug" deckt die MINOR-Mitnahme sauber ab, kein Scope-Drift.

### Logische Korrektheit

Reine XML-Doc-/Inline-Kommentar-Text-Edits, keine Verhaltensänderung möglich (Doku-Kommentare landen nicht in der optimierten IL). Selbst-Nachprüfung: Build grün (0 Warnungen, 10.37s), Volltest 1186 Tests / 1184 grün / 2 fehlgeschlagen / 0 übersprungen (4m 16s). Die 2 Failures betreffen **ausschließlich** `AiNetLinter.Tests.Commands.McpServerCommandErrorHandlingTests.RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError` und `…RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection` — beide nicht in den 8 geänderten Dateien, beide mit Stack am `SubprocessConcurrencyGate.AcquireAsync:30` → `SemaphoreSlim.WaitUntilCountOrTimeoutAsync` und Dauer 30.07s bzw. 35.23s (exakt der 30s-Wait-Timeout des Gates + Overhead). Reproduziert die im Coder-Schritt-Result dokumentierte Last-Flake-Signatur (1-2 Failures pro Lauf, immer in dieser Klasse, immer mit Gate-Timeout-Stack, keine der geänderten Dateien berührt). Klassifikation: **infrastructure** (Test-Gate-Sättigung, scope-extern), kein `issues`-Finding, kein Fix-Versuch verbraucht.

### Konzept-Treue

Fix-Modus-Planer hat gegen den `step-007/step-review.md`-Befund geplant, nicht gegen `Konzept.md` (siehe `spec.md` §6.2.1) — daher hier kein Konzept-Vergleichsmaßstab. Keine Non-Goals aus `Konzept.md` berührt, keine zusätzlichen Features eingebaut, keine Verhaltensänderung am Produktions-Code. Scope sauber auf die 3 MAJOR-Findings + optionale MINOR-Mitnahme begrenzt, beides innerhalb des `step-007/fix-01`-Plans.

## Build-/Test-Status

```
dotnet build AiNetLinter.slnx             → grün, 0 Warnungen, 0 Fehler, 10.37s
dotnet test AiNetLinter.slnx --no-build   → 1186 Tests / 1184 grün / 2 fehlgeschlagen (Last-Flake in McpServerCommandErrorHandlingTests, infrastructure), 4m 16s
```

## Tech-Debt-Einträge aus diesem Review

- **TD-005** (mittel) — `SubprocessConcurrencyGate`-Sättigung als bekanntes Volllauf-Risiko. Siehe `tech-debt.md` § „TD-005".
- **TD-006** (niedrig) — UTF-8-BOM auf `.agents/rules/AiNetLinter.mdc` (Working-Tree-vs-Index-Diskrepanz, semantisch leerer Diff). Siehe `tech-debt.md` § „TD-006".

Beide projektweit, nicht aus dem Fix-Scope folgend → Tech-Debt-Kanal, kein Verdict-Impact.

## Modell-Info

- `reviewed_by_model`: claude-sonnet-5
- `reviewed_by_model_knowledge_cutoff`: 2026-01
- Stufe (aus task-state.md, per Kritiker-Aufruf): High
