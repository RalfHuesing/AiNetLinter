---
status: done
type: step-plan
task: ainetlinter-feedback-r1
step: "007"
corrects: null
title: "Doku-, Schemata- und Konfig-Abschluss-Synchronisation"
epic: EPIC-07
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gemini-3.7-flash
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-15T19:35:00+02:00
related_to: []
---

# Step 007: Doku-, Schemata- und Konfig-Abschluss-Synchronisation

## Bezug

- **Task:** `ainetlinter-feedback-r1`
- **Epic:** `EPIC-07` aus `roadmap.md` — Doku-, Schemata- und Konfig-Abschluss-Synchronisation
- **Konzept-Referenz:** `konzept.md` §Doku & Sync

## Aktueller Projektzustand (JIT-Kontext)

Alle 6 funktionalen Feedback-Features (FB-02, FB-03, FB-04, Teil B, Teil A, FB-01) sind implementiert, mit FastTests abgesichert und von Kritiker abgenommen. Die Dokumentation in `Docs/`, die Agent-Rules-Synchronisation und die abschließende Test- und Drift-Verifikation stehen noch an.

## Intention

1. `Docs/configuration.md` aktualisieren (`MaxPublicMembersPerTypeApplyToTestFiles`).
2. `Docs/agent-api.md` aktualisieren (`find_duplicates` scopeType/Header, `get_violations` includeSnippet/contextLines, `get_class_structure` Tool-Beschreibung & Structured Output).
3. `Docs/ROADMAP.md` aktualisieren (Feedback-Runde 1 als Meilenstein dokumentieren).
4. `dotnet run --project src/AiNetLinter -- --config rules.json --sync-agent-rules-only` ausführen zur Synchronisation von `.agents/rules/AiNetLinter.mdc`.
5. Drift-Audit (`find_duplicates` Prüfung) ausführen.
6. Gesamte Testsuite (FastTests und IntegrationTests ohne Stress) grün verifizieren.

## Konkrete Änderungen

### Datei 1: `Docs/configuration.md`
- **Was:** Dokumentation von `MaxPublicMembersPerTypeApplyToTestFiles` in JSON-Schema und Tabellen.

### Datei 2: `Docs/agent-api.md`
- **Was:** Dokumentation von `get_class_structure`, `get_violations` (Snippets), `find_duplicates` (scopeType).

### Datei 3: `Docs/ROADMAP.md`
- **Was:** Dokumentation der Feedback-Runde 1 Änderungen.

### Datei 4: `.agents/rules/AiNetLinter.mdc`
- **Was:** Re-generieren via Sync-Command.

## Tests

- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- [ ] `dotnet build`

## Definition of Done

- [ ] Alle Dokumente aktualisiert und synchron
- [ ] Vollständige Testsuite (FastTests + IntegrationTests) grün
- [ ] Code-Commit & Doku-Commit auf aktuellem Branch
- [ ] `step-007/step-result.md` geschrieben

## Rules-Refs

- `AGENTS.md` §2 & §3 — Doku- & Regel-Sync
