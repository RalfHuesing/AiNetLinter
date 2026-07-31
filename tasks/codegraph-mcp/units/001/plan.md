---
status: in_progress
type: unit-plan
task: codegraph-mcp
unit: 001
title: "step-010 Audit nachziehen (Kritiker-Review auf vorhandenes Coder-Result)"
unit_type: audit
epic: EPIC-04
related_step: step-010
created_by: orchestrator
created_at: 2026-07-31T21:40:00Z
---

# Plan Unit 001: step-010 Audit nachziehen

## Bezug

- **Task:** `codegraph-mcp`
- **EPIC:** EPIC-04 (Struktur-/Qualitäts-Tools)
- **Vorgänger-Step (drift-loop):** `step-010` (`get_violations`) — vom
  Coder fertig umgesetzt (Commits `e63176d` Code + `7474226` Doku), aber
  **Kritiker-Review nie abgeschlossen** (Subagent brach bei
  Initialisierung mit `task_error: aborted` ab, kein inhaltliches
  Finding).
- **Dynamische Loop-Realität:** die zu prüfende Arbeit ist real
  vorhanden, nur das formale Verdict fehlt. Diese Einheit schließt
  genau diese Lücke — **kein Re-Code**, **kein Plan-Rewrite** des
  Coder-Schritts.

## Auftrag (genau diese Einheit)

Subagent: **Kritiker** (siehe `agents/kritiker.md`).

Input:

- `<task-dir>/step-010/step-plan.md` (Coder-Plan)
- `<task-dir>/step-010/step-result.md` (Coder-Protokoll, Status
  `done`)
- `<task-dir>/konzept.md` Tool-Tabelle Zeile `get_violations` +
  Muss-Haven "Cache umgehen" (Definition of Done)
- Code im Working-Tree (Commit `e63176d`):
  - `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`
  - `src/AiNetLinter/Commands/McpServerCommand.cs`
  - `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` (neu)
  - `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` (neu)
  - `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (neu)
  - `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`
  - `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs`
  - `rules.json` (PathOverrides)
  - `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/ViolationTrigger.cs`
  - `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs` (neu)
  - `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`
  - `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs`
  - `src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs`

Output: `<task-dir>/units/001/review.md` mit Verdict
`approved` / `issues` / `blocked`.

## Was der Kritiker prüft (Reihenfolge)

1. **Plan-Konformität:** Coder-Ergebnis gegen `step-010/step-plan.md`:
   1:1 Scope, keine Drift. Plan sagt explizit "dritte Registrar-Klasse
   als Ausweich-Option" — wurde umgesetzt.
2. **Konzept-Konformität:** `get_violations` matcht die Konzept-Tool-
   Tabelle (Input "Datei- oder Symbol-Scope", Output "aktuelle
   Lint-Verstöße", Basis `RuleRegistry`/`LinterEngine`). Muss-Haven
   "Cache umgehen" ist im Code (`noCache: true, cacheTtlMinutes: 0`)
   sichtbar.
3. **Build/Test-Nachweis (A3):** `result.md` zeigt 1088/1088 grün.
   **Kritiker prüft das Protokoll** — `dotnet test` selbst nicht
   erneut laufen lassen, nur einen gezielten Test, falls Verdacht.
4. **Fehlschlag-Nachweis:** `result.md` enthält den Filter-Test
   `GetViolations` (6 bestanden) als Cache-Bypass-Beleg. **Fehlschlag-
   Nachweis für die 5 neuen Unit-Tests + 1 E2E-Test** prüfen — der
   Coder-Eintrag "Cache-Bypass-Verifikation" zeigt nur den positiven
   Pfad. Falls dort kein expliziter "vorher rot → nachher grün"-Beleg
   ist, ist das ein `issues`-Befund (innerhalb).
5. **--footprint:** `result.md` zeigt GetViolationsTool 2451,
   GetViolationsScanner 1834, FileStructureToolRegistrations 2480,
   AnalysisToolRegistrations 2459 — alle < 2500. **Beleg-Frage:** hat
   der Coder `ainetlinter --footprint` selbst ausgeführt oder
   geschätzt? Im Zweifel `issues` mit Bitte um Nachmessung.
6. **PathOverrides-Regression:** `rules.json` hat PathOverrides für
   `FindReferencesTool` und `FindSymbolTool` (je 2700) — das ist
   vorbestehendes Tech-Debt-Potential (Kandidat für `tech-debt.md`,
   NICHT Verdict-Hindernis — A2). **Nicht** als `issues` werten.
7. **Commit-Format:** `e63176d` Message ist `tasks: codegraph-mcp-
   next verfeinert` (kein Conventional-Format, kein `[codegraph-mcp]`-
   Suffix, deutsch). Das ist **bekannte Unschärfe** (externer Commit,
   Skill-Regel verbietet History-Rewrite). **Nicht** als `issues`
   werten — im Review als "bekannte Unschärfe" anerkennen.
8. **Konventionen:** keine `step-XXX`-Referenzen im neuen Code, keine
   Refactoring-Historie, `#nullable enable` an allen neuen Dateien,
   `sealed` auf den Tool-Klassen.

## Was der Kritiker NICHT tut

- Kein Re-Code. Kein "kleiner Fix" an Tool/Scanner. A2.
- Kein Nachmessen des `dotnet test`-Gesamtlaufs. A3 (Protokoll
  reicht).
- Keine Scope-Erweiterung. A2.
- Falls Subagent-Initialisierung wieder abbricht: `blocked` mit
  Fehlermeldung im `review.md`-Header zurückmelden (gelernt aus
  dem drift-loop-Vorfall).

## Verdict-Erwartung (vom Orchestrator)

Mein Bauchgefühl nach Lesen von `step-010/step-result.md` und
`step-010/step-plan.md`: **vermutlich `issues`**, weil:
- Der Coder-Eintrag "Cache-Bypass-Verifikation" zeigt nur den
  positiven Pfad, nicht den expliziten "vorher rot → nachher grün"-
  Beleg für die 5+1 neuen Tests. Das ist ein A3-Befund, klein
  und innerhalb des Scopes.
- Der `--footprint` ist berichtet, aber die Ausgabe des
  `--footprint`-Commands selbst ist nicht im `result.md`. Das ist
  ein A3-Befund (Befehls-Output wortwörtlich fehlt), ebenfalls
  innerhalb des Scopes.

Falls der Kritiker dieselben Befunde hebt, ist die Fix-Runde
klein: Coder ergänzt den Fehlschlag-Nachweis und die
--footprint-Cmd-Outputs. Falls der Kritiker darüber hinaus
substantielle Findings hat, ist der Plan ggf. anzupassen
(`blocked`).

## Risiko-Hinweise

- **Subagent-Initialisierung könnte wieder abbrechen** (gelernt aus
  `task_error: aborted` im drift-loop). Falls reproduzierbar: dieser
  Unit bleibt formal `blocked`, der Audit wird vom Orchestrator
  manuell nachgeholt, und `agents/kritiker.md` wird in Phase 4
  robuster gemacht.
- **Konzept-Referenz `tasks/codegraph-mcp-next/Konzept.md`** wurde
  im externen Commit `e63176d` umstrukturiert — das ist nicht Inhalt
  dieses Reviews (Schwester-Task), nur Notiz im `review.md`.

## Zähler

- **Fix-Runden dieser Einheit:** 0/3
- **Fix-Runden gesamt (Task-weit):** 0/12
- **Subagent-Aufrufe (Task-weit):** 1/40 (Kritiker, diese Einheit)
