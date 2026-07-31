---
status: executing
task: codegraph-mcp
workflow: dynamic-loop
started_at: 2026-07-31T21:30:00Z
last_updated: 2026-07-31T21:30:00Z
rules_dir: .agents/rules
max_rollen: 2
max_aufrufe: 40
max_fix_pro_einheit: 3
max_fix_gesamt: 12
total_fix_rounds: 0
current_unit: units/001
previous_workflow: drift-loop  # Fremde Artefakte unter step-NNN/ + task-state.md

## Baseline (Phase 2, 2026-07-31T21:35 UTC+2)

- `dotnet build AiNetLinter.slnx -c Debug` → grün, **0 Warnungen**,
  0 Fehler, 10.66s. Konsistent mit step-010-Result.
- `dotnet test AiNetLinter.slnx -c Debug --no-build` →
  **1088/1088 grün**, 0 Fehler, 0 übersprungen, 6m 56s. Konsistent
  mit step-010-Result.
- **Was bereits vor dem dynamic-Loop rot war:** nichts.
- **Bewertung:** grüne Baseline, Phase 3 kann starten.
---

# Task State: codegraph-mcp (dynamic-loop)

## Übersicht

- **Task-Status:** `executing`
- **Fix-Runden gesamt (dynamic-loop-Zähler):** 0/12
- **Aktuelle Einheit:** `units/001` — step-010-Audit nachziehen
- **Konzept:** `konzept.md` (verbindlich, nur lesbar — A6)
- **Projektregeln:** `.agents/rules/AiNetLinter.mdc` (auto-generierte Codequalität)
  und `.agents/rules/AiNetLinterRichtlinien.mdc` (Architektur & Workflow)
- **Vorgänger-Workflow:** `drift-loop` (siehe `task-state.md`, `roadmap.md`,
  `tech-debt.md`, `step-001/` bis `step-010/`) — bleibt unangetastet liegen,
  wird nur als Input/Realitäts-Beleg gelesen (Phase 0 Fall 3)
- **Gestartet:** 2026-07-31T21:30:00Z
- **Zuletzt aktualisiert:** 2026-07-31T21:30:00Z

## Vorbefund (Phase 0, Fall 3)

Der Task wurde von einem `drift-loop`-Lauf übernommen, der auf **expliziten
Nutzer-Wunsch** angehalten wurde. Realität (gegen Code + `git log` verifiziert,
nicht gegen Status-Labels):

- **EPIC-01..03 vollständig abgeschlossen:** step-001 bis step-007 inkl. drei
  Fix-Runden, alle `approved` (Commits `3ae6230`, `81cf007`, `9d6cecc`,
  `a9e91ed`, `8db5f4b`, `c125511`, `22e8410`).
- **EPIC-04 teilweise:** step-008 (`get_index_scope`, `approved`,
  Commit `6624312`), step-009 (`get_hotspots`, `approved`, Commits `995500e`
  Code + `71779a4` Review) — beide real grün und dokumentiert.
- **EPIC-04 step-010 (`get_violations`) — codiert, Review offen:**
  Code-Commit `e63176d` (extern durch Nutzer angelegt, Conventional-Format
  nicht erfüllt — laut Skill-Regel kein History-Rewrite, **Rest des
  Code-Inhalts ist 1:1 wie geplant umgesetzt**, siehe `step-010/step-result.md`).
  Doku-Commit `7474226`. Build/Test vom Coder dokumentiert grün
  (1088/1088, 0 Warnungen). **Kritiker-Subagent brach bei Initialisierung
  mit `task_error: aborted` ab** — kein inhaltliches Finding. Working-Tree
  ist clean (per `git status --porcelain` verifiziert: 0 Änderungen).
- **EPIC-04 step-011 (`search_pattern`)** — letztes offenes EPIC-04-Tool, nicht
  begonnen. Aus `konzept.md` Tool-Tabelle: Text-/Regex-Fallback über den
  Solution-Dateibestand (für Config-Werte, Kommentare, alles kein Symbol).
- **EPIC-05..08 — noch offen** (siehe `roadmap.md`):
  - EPIC-05: Scope-Kommunikation in Tool-`description` + `instructions`-
    Feld, `find_symbol`-Miss-Hint für Nicht-`.cs`-Dateien.
  - EPIC-06: Robustheit bei Compile-/Solution-Fehlern, 9-Tools-Fehlerpfad-
    Audit.
  - EPIC-07: Tests (Staleness, Integration je Tool, Miss-Hint,
    Mehrdeutigkeit, Cache-Isolation, Regression CLI).
  - EPIC-08: Doku (`Docs/agent-api.md` MCP-Abschnitt, `Docs/integration.md`,
    `Docs/ROADMAP.md`, `README.md`).
- **EPIC-09** — gestrichen, ersetzt durch Dogfooding pro Tool-Step
  (im `drift-loop` bereits etabliert, gilt auch hier).
- **`tasks/codegraph-mcp-next/`** — Schwester-Task mit Konzept-Verfeinerung
  (insb. `rules.json` als "active policy engine"), aktuell separat, nicht
  Teil dieses dynamischen Loops.

**Reale Fix-Runden (drift-loop):** 3 verbraucht (step-003/fix-01,
step-005/fix-01, step-007/fix-01). Der dynamic-loop-Zähler startet formal
bei 0/12 (Default, keine `konfig.md` angelegt) — die 3 reale Runden
**aus dem Vorgänger-Workflow werden nicht gegen den dynamic-loop-Deckel
gezählt**, weil sie in einem anderen Workflow mit eigenem Regelwerk
verbraucht wurden. Transparenz: wenn der Nutzer eine harte Brücke will,
`konfig.md` anlegen und `total_fix_rounds_offset: 3` setzen (geht nur
über Nutzer — A1).

## Aktionsplan (Phase 3, Reihenfolge)

| Einheit | Inhalt | Status | Fix-Runden |
| :--- | :--- | :--- | :---: |
| `units/001` | step-010-Audit nachziehen: Kritiker-Review auf vorhandenes `step-010/step-result.md` (kein Re-Code). Verdict → `approved` / `issues` / `blocked`. | offen | 0/3 |
| `units/002` | step-011 `search_pattern` (planen, codieren, reviewen) | offen | 0/3 |
| `units/003` | EPIC-05 Scope-Kommunikation & Miss-Hint | offen | 0/3 |
| `units/004` | EPIC-06 Robustheit bei Compile-/Solution-Fehlern | offen | 0/3 |
| `units/005` | EPIC-07 Tests (Staleness, Integration je Tool, Miss-Hint, Mehrdeutigkeit, Cache-Isolation, Regression) | offen | 0/3 |
| `units/006` | EPIC-08 Dokumentation (`agent-api.md`, `integration.md`, `ROADMAP.md`, `README.md`) | offen | 0/3 |

`Meta-Review` (Phase 4) nach jeweils 3 Einheiten — Involution: Rollen-
Prompts werden auf Basis des Verlaufs justiert (nur `agents/**`, nie
`kernel.md`).

## Abbruch-Bedingungen (A1, Defaults)

- `max_fix_rounds_per_step` (3) erreicht → Einheit `blocked`.
- `max_total_fix_rounds` (12) erreicht → Task `aborted`.
- `max_aufrufe` (40) erreicht → Task `aborted`.
- `blocked` aus Subagenten-Findings → Loop pausiert, Nutzer klärt.

## Pause-Notiz (Übergabe vom drift-loop, 2026-07-31 22:30 UTC+2)

- `step-010` ist vom Coder vollständig umgesetzt (Commit `e63176d`).
- Doku-Commit `7474226` setzt `step-010` formal auf `done`.
- `step-010/step-review.md` existiert **nicht** (Kritiker-Initialisierung
  brach ab, technischer Fehler, kein inhaltliches Finding).
- Working-Tree clean, keine Background-Tasks, keine offenen Prozesse.
- `dotnet test` laut Coder grün (1088/1088) — wird in Phase 2 neu
  verifiziert.

## Risk Notes

- **Externer Code-Commit-Format `e63176d`:** Conventional-Format mit
  `[codegraph-mcp]`-Suffix nicht erfüllt; laut Skill-Regel kein
  History-Rewrite. Wird in `units/001/`-Plan explizit als bekannt
  vermerkt, nicht in `units/001/result.md` als Finding hochgestuft.
- **Tech-Debt `TD-007` (5-Parameter-Methode `TryApplyContentChange`):**
  vorbestehend, nicht im Scope. Bleibt in `tech-debt.md`.
- **Kritiker-Subagent-Stabilität:** der Initialisierungsabbruch bei
  step-010 zeigt, dass Subagenten-Kontext-Aufbau bei extern angelegten
  Commits unzuverlässig sein kann. Falls in `units/001` reproduzierbar,
  wird der Prompt in `agents/kritiker.md` robuster geschrieben (siehe
  Phase 4).
