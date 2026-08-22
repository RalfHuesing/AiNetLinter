---
task: 03_get-impact-zum-diff-kontext-erweitern
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-22T20:55:00+02:00
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
| TD-002 | `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs` (`GetStableSymbolId`) | mittel | nein | Lokale Funktionen erben die Doc-ID der einschließenden Methode — Kollisionsrisiko stabiler IDs, sobald der breite Scanner (EPIC-2 Teil 2) sie einschließt |

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

### TD-002 — Stabile-ID-Kollision bei lokalen Funktionen [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-22; vom Coder in
  den step-result-Beobachtungen gemeldet, vom Kritiker gegen Code und Test
  bestätigt)
- **Ort:** `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs`
  (`GetStableSymbolId`, Z. 121–123); Wirkstellen:
  `ChangedSymbolEntry.SymbolId`
  (`src/AiNetLinter/Core/DiffImpactAnalyzer.cs`, `CreateChangedSymbolEntry`)
  und `TransitiveCallSiteEntry.ReachedFromSymbolId` (`BuildReferencesAsync`)
- **Befund:** `DocumentationCommentId.CreateDeclarationId` liefert für
  lokale Funktionen nicht `null`, sondern die Doc-ID der einschließenden
  Methode (empirisch im Test
  `CreateChangedSymbolEntry_ForLocalFunction_UsesSharedStableIdLogic`
  gepinnt). Der Fallback-Pfad greift dort nie. Schließt der breite Scanner
  (EPIC-2 Teil 2, lokale Funktionen sind explizit im Scope) sie über diese
  gemeinsame ID-Logik ein, erhalten ALLE lokalen Funktionen derselben
  Methode denselben `SymbolId`-Wert — Einträge wären dann nur noch über
  DisplayName/Deklarationszeile unterscheidbar, `ReachedFromSymbolId`
  mehrdeutig. Auch der geplante EPIC-7-Vertragstext „stabile ID =
  DocCommentId oder deterministischer Fallback (lokale Funktionen)“ trifft
  in dieser Form nicht das reale Verhalten (Konzept Audit D.4/F-Prämisse).
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-002 — der
  schmale `callers`-Scanner (Methoden+Konstruktoren) enthält keine lokalen
  Funktionen; heute kein Defekt. Die ID-Schema-Entscheidung (Ermessen)
  gehört in den Plan des nächsten Steps, nicht in einen Korrektur-Step.
- **Vorschlag:** Für lokale Funktionen einen deterministischen Sonderfall
  definieren, der Name + Deklarationsposition einbezieht (oder Eindeutigkeit
  über Zusatzfelder sicherstellen); EPIC-7-Dokumentation zur stabilen ID
  entsprechend korrigieren.
- **Auto-Fixable:** nein — Design-Entscheidung am gemeinsamen ID-Schema
  mit Auswirkung auf künftige Vertragsfelder, keine rein mechanische
  Korrektur.
- **Status:** offen  # offen | erledigt | verworfen — Änderung ist
  manuell (Nutzer) bzw. automatisch auf „erledigt" nach erfolgreicher
  Bündelung eines `auto_fixable: ja`-Eintrags; kein Subagent ändert den
  Status eines `nein`-Eintrags selbst
