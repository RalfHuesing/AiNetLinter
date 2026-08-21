---
status: done (pending audit)
type: step-plan
task: 04_repositoryweite-hybridsuche-und-kontextbudget
step: 004
corrects: step-003
title: "Cancellation-Fallback und Overview-Grenzen korrigieren"
epic: EPIC-04
step_type: single
estimated_risk: medium
created_by: orchestrator
created_at: 2026-08-21
tech_debt_ids:
  - TD-003-001
---

# Step 004: Cancellation-Fallback und Overview-Grenzen korrigieren

## Bezug

- Step 003 ist funktional weitgehend umgesetzt, wurde aber wegen eines MAJOR-Findings nicht freigegeben.
- Der Korrektur-Step behebt ausschließlich den zweiten lexikalischen Scan im Enrichment-Cancellation-Pfad und bündelt die unmittelbar zugehörige, im Review identifizierte Overview-Vervollständigung `TD-003-001`.
- Der parallele Bereich `tasks/mcp-server-weiterentwicklung` bleibt außerhalb des Scopes.

## Findings und Tech-Debt

### MAJOR-Finding aus step-003

`SearchPatternTool.ExecuteAsync` startet nach einer während der Roslyn-Anreicherung ausgelösten Cancellation erneut `SearchPatternScanner.Scan(...)`. Damit wird die Dateisystem-/Trefferenumeration doppelt ausgeführt, obwohl die bereits sichtbare Match-Liste die einzige Quelle bleiben muss.

### TD-003-001 — Overview-Grenzen vervollständigen

- **Status:** offen, wird in diesem Korrektur-Step erledigt
- **auto_fixable:** ja
- **Scope:** `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs` und die zugehörige Discovery-/Parität-Regression
- **Problem:** Die `search_pattern`-Overview erwähnt `enrichCSharp=true`, nennt aber die Snapshot-Grenze, `unavailable`-/`ambiguous`-Zustände und den Folgeweg bei Trunkierung nicht vollständig.
- **Erledigungsnachweis:** Overview-Text und Test beschreiben genau den implementierten Vertrag und bleiben innerhalb des UTF-8-/Discovery-Budgets.

## Konkrete Änderungen

1. `SearchPatternTool`/Enrichment-Orchestrierung so ändern, dass die lexikalische Auswahl genau einmal erfolgt. Bei Cancellation während Enrichment wird der bereits erzeugte Payload recoverable zurückgegeben bzw. der bereits sichtbare Trefferbestand ohne Semantic-Felder weiterverwendet; kein zweiter Dateisystem-Scan und keine neue Solution-Ladung.
2. Einen Regressionstest ergänzen, der Cancellation nach abgeschlossener lexikalischer Auswahl während Enrichment auslöst und über einen kontrollierten Scanner-/Enricher-Hook oder eine geeignete Test-Doppelstruktur beweist, dass keine zweite Enumeration stattfindet und die vorhandenen Matches/Completeness-Metadaten nicht verworfen werden.
3. `OverviewResourceRegistration.ToolSummaries` um Snapshot-/`unavailable`-/`ambiguous`-Grenzen und den vorgesehenen Folgeweg bei Trunkierung ergänzen; den bestehenden UTF-8-Budgettest beibehalten.
4. `TD-003-001` nach erfolgreicher Implementierung in `tech-debt.md` als erledigt markieren; keine weiteren Debt-Punkte opportunistisch aufnehmen.

## Nicht-Ziele

- Keine Änderung am normalen Roslyn-Enrichment, an Kategorien/Resolutionswerten, Legacy-Text, StructuredContent-Grundform, Limits, Scope oder der MCP-Dokumentation außerhalb der Overview-Vervollständigung.
- Keine neue Trefferenumeration, kein produktives `rg`, keine Messungen, kein RAG/Ranking, kein Cursor-/Session-State.
- Keine Änderungen unter `tasks/mcp-server-weiterentwicklung`.

## Tests und Akzeptanzkriterien

- Cancellation nach lexikalischer Auswahl führt nicht zu einem zweiten `SearchPatternScanner.Scan`/Dateisystem-Scan.
- Bereits sichtbare Matches und Completeness werden nicht durch einen Fallback-Rescan verworfen; Roslyn-Cancellation bleibt recoverable und transparent.
- Overview nennt opt-in-Enrichment, Snapshot-/Projektgrenze, `ambiguous`/`unavailable` und Folgeaufruf/Scope-Verfeinerung bei Trunkierung.
- Gezielte Scanner-/Tool-/Overview-Tests, `dotnet build`, vollständige FastTests und IntegrationTests mit `Category!=Stress` sind grün.
- Projektinterner Lint-/Violation-Check ist ohne neue Step-Verstöße; vorhandene gitignorierte `temp`-Artefakte werden nicht als Codeänderung behandelt.

## Commit- und Review-Hinweise

- Coder: separater Code-/Test-Commit und Doku-/Step-Commit, deutsche imperative Conventional-Commit-Sujets mit `[04_repositoryweite-hybridsuche-und-kontextbudget]`.
- Kritiker prüft insbesondere Cancellation nach der sichtbaren Auswahl, Metadaten-Erhalt, keine zweite Enumeration und Overview-/UTF-8-Parität.
