---
task: 03_get-impact-zum-diff-kontext-erweitern
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-22T19:35:42+02:00
---

# Tech-Debt-Log: 03_get-impact-zum-diff-kontext-erweitern

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem eigenen Korrektur-Step oder einem neuen Epic. Das entscheidet
grundsätzlich der Nutzer (manuell, z. B. durch Ergänzen eines Epics in
`roadmap.md` mit Verweis auf die Tech-Debt-ID).

**`auto_fixable` (`ja`/`nein`, siehe `../spec.md` §9.1) ist die einzige
Ausnahme:** rein mechanische, entscheidungsfreie Fixes ohne
Architektur-Ermessen dürfen vom Planer opportunistisch an einen ohnehin
laufenden Step angehängt werden (§10.6) — kein eigener Step, kein
eigener Sweep. Default bei Unsicherheit ist `nein`.

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-001 | `src/AiNetLinter.FastTests/Fixtures/McpInMemoryTestContext.cs` | niedrig | nein | `CreateScenario` liefert kein Server-Handle — Tool-Level-Ad-hoc-Tests brauchen Boilerplate-Wrapper |

## Einträge

### TD-001 — CreateScenario ohne direkten Server-Zugriff [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-22; vom Coder
  in den step-result-Beobachtungen gemeldet, vom Kritiker bestätigt)
- **Ort:** `src/AiNetLinter.FastTests/Fixtures/McpInMemoryTestContext.cs`
  (`CreateScenario(ProjectSpec)` — Rückgabe `RoslynTestSolution` ohne
  `CreateServer()`)
- **Befund:** Für Tool-Level-Tests auf Ad-hoc-Szenarien ist ein
  Zweischritt-Boilerplate nötig
  (`new McpInMemoryTestContext(CreateScenario(...))` + `CreateServer()`),
  statt dass das Scenario-Ergebnis direkt einen Server liefert. Das
  wiederholt sich in jedem künftigen Tool-Level-Ketten-/Szenario-Test
  (EPIC-3 baut weitere davon).
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001 —
  API-Entscheidung an der gemeinsamen Test-Fixture, die mehrere
  bestehende Tests berührt; kein Defekt, reine Ergonomie.
- **Vorschlag:** Ergänzende Fabrik-Methode/Überladung (z. B.
  `CreateScenarioServer(...)` oder `CreateServer()` am
  Scenario-Ergebnis), die Szenario + Server in einem Schritt liefert;
  bestehende Aufrufer bleiben unverändert nutzbar.
- **Auto-Fixable:** nein — Design-Entscheidung an geteilter
  Fixture-Infrastruktur (Architektur-Ermessen), kein rein mechanischer
  Fix.
- **Status:** offen  # offen | erledigt | verworfen — Änderung ist
  manuell (Nutzer) bzw. automatisch auf „erledigt" nach erfolgreicher
  Bündelung eines `auto_fixable: ja`-Eintrags; kein Subagent ändert den
  Status eines `nein`-Eintrags selbst
