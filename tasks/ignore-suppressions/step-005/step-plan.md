---
status: done (pending audit)
type: step-plan
task: ignore-suppressions
step: "005"
title: "Dokumentation in Docs/configuration.md, Docs/ROADMAP.md und README.md mit --ignore-suppressions synchronisieren"
epic: EPIC-05
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: Gemini 3.6 Flash (High)
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T08:36:00+02:00
related_to:
  - tasks/ignore-suppressions/step-001/step-plan.md
  - tasks/ignore-suppressions/step-002/step-plan.md
  - tasks/ignore-suppressions/step-003/step-plan.md
  - tasks/ignore-suppressions/step-004/step-plan.md
---

# Step 005: Dokumentation in Docs/configuration.md, Docs/ROADMAP.md und README.md mit --ignore-suppressions synchronisieren

## Bezug

- **Task:** `ignore-suppressions`
- **Epic:** `EPIC-05` aus `roadmap.md` — Documentation & Roadmap Sync.
- **Konzept-Referenz:** `konzept.md` §Muss-Haben / §Verifikation & Doku.

## Aktueller Projektzustand (JIT-Kontext)

Die CLI-Option `--ignore-suppressions`, die Engine, die Header-Ausgabe und alle Unit-/Integrationstests wurden in Step 001 bis 004 implementiert und auditiert. Nun müssen die Projektdokumentationen in `Docs/configuration.md`, `Docs/ROADMAP.md` und `README.md` aktualisiert und die Agenten-Regeln über `--sync-agent-rules-only` synchronisiert werden.

## Intention

Vollständige Dokumentation der `--ignore-suppressions` CLI-Option inklusive erlaubter Sprachparameter (`all`, `cs`/`c#`, `razor`, `js`, `css`), Default-Verhalten, Komma-Separation und Beispielen in den Projektdokumenten.

## Konkrete Änderungen

### Datei 1: `Docs/configuration.md`

- **Was:** Ergänzung der CLI-Referenz um `--ignore-suppressions [sprachen...]` inklusive Parametern und Beispielen.
- **Warum:** Zentrale CLI-Konfigurationsdokumentation.

### Datei 2: `Docs/ROADMAP.md`

- **Was:** Aktualisierung der Roadmap bezüglich Suppressions Bypass / CLI Features.
- **Warum:** Meilenstein-Nachverfolgung.

### Datei 3: `README.md`

- **Was:** Ergänzung der `--ignore-suppressions` Option in der CLI-Übersichtstabelle/Abschnitt.
- **Warum:** Erste Anlaufstelle für Entwickler.

### Befehl: Agent-Rules Sync

- **Was:** Ausführen von `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` zur Aktualisierung von `.agents/rules/AiNetLinter.mdc`.
- **Warum:** Regel 4 in `AGENTS.md` fordert die Synchronisation der Agent-Regeln bei CLI-/Regel-Änderungen.

## Tests

- [ ] `dotnet test`
- [ ] Build & Sync Verification

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command (`dotnet build`) grün
- [ ] Test-Command (`dotnet test`) grün
- [ ] Agent-Rules synchronisiert (`dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`)
- [ ] Commit auf aktuellem Branch
- [ ] `tasks/ignore-suppressions/step-005/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `AGENTS.md#4.-dokumentations---regel-synchronisation` — Doku- und `--sync-agent-rules-only` Synchronisationspflicht.

## Bekannte Ausnahmen

- Keine.

## Notes

- Nach der Doku-Aktualisierung wird die gesamte Task in `roadmap.md` als abgeschlossen markiert.
