---
status: executing  # executing | done | aborted
task: codegraph-mcp
started_at: 2026-07-31T09:30:00Z
last_updated: 2026-07-31T22:00:00Z
rules_dir: .agents/rules
total_fix_rounds: 3  # Summe aller Fix-Runden über alle Steps (Task-weiter Not-Anker, siehe Config)
current_step: step-009
---

# Task State: codegraph-mcp

## Übersicht

- **Task-Status:** `executing`
- **Fix-Runden gesamt:** 0 (Not-Anker bei `max_total_fix_rounds`, siehe Config)
- **Aktueller Schritt:** `step-010` (planned, EPIC-04, `get_violations`)
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-07-31T09:30:00Z
- **Zuletzt aktualisiert:** 2026-07-31T09:30:00Z

## Steps

<Diese Tabelle wächst mit jedem Planer-Aufruf im Step-Modus um genau
eine Zeile.>

| Step | Epic | Status | Title | Fix-Runden | Coded | Reviewed | Commit |
|------|------|--------|-------|------------|-------|----------|--------|
| step-001 | EPIC-01 | done | CLI-Einstiegspunkt --mcp-server + minimaler stdio-MCP-Server | 0/3 | yes | approved | 3ae6230 |
| step-002 | EPIC-02 | done | Resident McpCodeGraphServer + Lazy-Staleness-Invalidierung | 0/3 | yes | approved | 81cf007 |
| step-003 | EPIC-03 | done | Tool-Infrastruktur + erstes Tool find_symbol (fix-01: solution-not-loaded coverage) | 1/3 | yes | approved | 9d6cecc |
| step-004 | EPIC-03 | done | Zweites Tool find_references | 0/3 | yes | approved | a9e91ed |
| step-005 | EPIC-03 | done | Drittes Tool get_impact (fix-01: stdio subprocess hang fixed) | 1/3 | yes | approved | 8db5f4b |
| step-006 | EPIC-03 | done | Viertes Tool get_file_skeleton | 0/3 | yes | approved | c125511 |
| step-007 | EPIC-03 | done | Fünftes/letztes Tool get_type_hierarchy (fix-01: external base/interface display fixed) | 1/3 | yes | approved | 22e8410 |
| step-008 | EPIC-04 | done | Erstes EPIC-04-Tool get_index_scope | 0/3 | yes | approved | 6624312 |
| step-009 | EPIC-04 | done | Zweites EPIC-04-Tool get_hotspots | 0/3 | yes | approved | 995500e (code), 71779a4 (review) |
| step-010 | EPIC-04 | open (planned) | Drittes EPIC-04-Tool get_violations | 0/3 | no | - | - |

## Config (optional)

Falls `<task-dir>/config.md` existiert, hier die Overrides dokumentieren.
Andernfalls gelten die Defaults aus `../spec.md`.

```
max_fix_rounds_per_step: 3
max_total_fix_rounds: 12
max_batch_items: 8          # siehe ../spec.md §10.6 (Micro-Batches innerhalb eines Epics)
max_batch_diff_lines: 40    # siehe ../spec.md §10.6
build_command: <aus roadmap.md Tech-Stack-Notiz>
test_command: <aus roadmap.md Tech-Stack-Notiz>
target_branch: main
model_planer: Sonnet 5, Stufe High
model_coder: Sonnet 5, Stufe Medium
model_kritiker: Sonnet 5, Stufe Medium
```

<Die drei `model_*`-Felder sind optional und halten eine vom Nutzer
genannte, rollenabhängige Modellwahl fest (typisch: günstigeres Modell
für den Coder, stärkeres für Planer/Kritiker). Werte sind freier Text —
der Workflow validiert sie nie. Sie stehen hier statt nur im Start-Prompt,
weil ein Task in einer **neuen Session** fortgesetzt werden kann
(`../orchestrator.md` Schritt 1, Fall B läuft ohne Rückfrage weiter) —
sonst liefen die Subagenten nach einem Resume still auf dem
Default-Modell. Nicht gesetzt = keine Vorgabe, der Orchestrator fragt
auch nicht nach. Siehe `../spec.md` §10.8.>

## Abbruch-Bedingungen

- **Fix-Budget eines Steps erreicht** (`max_fix_rounds_per_step`, Default
  3, ohne `approved`): dieser eine Step → `blocked`, Loop pausiert,
  Nutzer klärt.
- **Task-weiter Not-Anker erreicht** (`max_total_fix_rounds`, Default 12,
  über alle Steps summiert): Task → `aborted`, siehe `task-summary.md`.
- **Blocker aufgetreten** (Step mit Status `blocked`): Loop pausiert,
  Nutzer klärt.
- **Tech-Debt-Einträge lösen NIE einen Abbruch oder Blocker aus** — sie
  sind reine Beobachtung, kein Steuerungssignal (siehe `../spec.md` §9).

## Pause-Notiz (manueller Stopp, 2026-07-31 20:35 UTC+2)

Der Loop wurde auf **expliziten Nutzer-Wunsch** angehalten, kein
`blocked`-Zustand aus dem Workflow selbst:

- **Stand:** step-009 (`get_hotspots`, EPIC-04) ist vom **Coder fertig
  umgesetzt** — Code-Commit `995500e`, Doku-Commit `6693f6f`,
  `step-result.md` liegt vor, Status `done (pending audit)`. Build/Test
  laut Coder grün (1080/1080), Selbst-Lint OK, Dogfooding dokumentiert.
- **Offen:** Der **Kritiker-Review für step-009 ist nicht
  abgeschlossen** — `step-009/step-review.md` existiert noch nicht. Der
  Kritiker-Subagent hatte während der eigenen Build/Test-Verifikation
  einen hängenden Background-Prozess (0 Byte Output nach >25 Minuten,
  kein laufender `dotnet`/`testhost`-Prozess mehr) — wurde per
  Zwischennachricht zum Neu-Start des Testlaufs angestoßen, dieser lief
  danach sichtbar (echte `dotnet`/`testhost`/`AiNetLinter.Tests.exe`-
  Prozesse beobachtet) und ist inzwischen durchgelaufen (keine
  Test-Prozesse mehr aktiv). Der Kritiker-Agent selbst wurde vor
  Fertigstellung von `step-review.md` gestoppt (Nutzer-Wunsch).
- **Kein offener Prozess/Agent mehr:** verifiziert per Prozessliste
  (`tasklist`) — keine `dotnet`/`MSBuild`/`VBCSCompiler`/`testhost`-
  Prozesse mehr aktiv.

**Wiederaufnahme:** Beim nächsten Orchestrator-Lauf (`orchestrator.md`
Schritt 1, Fall B) direkt mit dem **Kritiker-Aufruf für step-009**
fortsetzen (Input: `step-009/step-plan.md` + `step-009/step-result.md`,
Modus `step`) — **nicht** erneut den Coder aufrufen, der Step ist bereits
vollständig codiert und committet. Erst nach dem Kritiker-Verdict
(`approved`/`issues`/`blocked`) normal weiter im Loop (Schritt 3b für den
nächsten Step bzw. Fix-Step).
