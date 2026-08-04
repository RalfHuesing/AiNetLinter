---
status: executing  # executing | done | aborted
task: codegraph-mcp-finish
started_at: 2026-08-03
last_updated: 2026-08-04
rules_dir: .agents/rules
total_fix_rounds: 2  # Summe aller Fix-Runden über alle Steps (Task-weiter Not-Anker, siehe Config)
current_step: step-010 (done approved, EPIC-05 abgehakt; Kritiker approved mit 2 MINOR + TD-008; bereit fuer EPIC-06)
---

# Task State: codegraph-mcp-finish

## Übersicht

- **Task-Status:** `executing`
- **Fix-Runden gesamt:** 2 (Not-Anker bei `max_total_fix_rounds`, siehe Config)
- **Aktueller Schritt:** `step-010` (done approved, EPIC-05 abgehakt) — bereit fuer EPIC-06
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-08-03
- **Zuletzt aktualisiert:** 2026-08-04

## Steps

<Diese Tabelle wächst mit jedem Planer-Aufruf im Step-Modus um genau
eine Zeile.>

| Step | Epic | Status | Title | Fix-Runden | Coded | Reviewed | Commit |
|------|------|--------|-------|------------|-------|----------|--------|
| step-001 | EPIC-01 | done | ConsoleTestCollection-Regression beheben (F.1) | 0/3 | e466020 | approved | e466020, 8581a4d |
| step-002 | EPIC-01 | done | CliProcessRunner-Helper (F.2) | 0/3 | a566ea4 | approved | a566ea4, 172bdc5 |
| step-003 | EPIC-01 | done | Core/-Testordner subgliedern, MaxDirectoryChildren aktivieren (F.3) | 0/3 | 8cae25c | approved | 8cae25c, cede919 |
| step-004 | EPIC-01 | done | Test-Data-Builder statt ad-hoc-Konstruktion, Teil 1/2 (F.4) | 0/3 | 26fd08f | approved | 26fd08f, ae4d6d1 |
| step-005 | EPIC-01 | done | Test-Data-Builder Rest-Cluster (F.4 Teil 2/2) + `#nullable enable`-Randmitnahme (F.5) | 0/3 | d744dc9 | approved | d744dc9, 0f6c2cd, fcf1d32 |
| step-006 | EPIC-01 | done | Laufzeitmessung vorher/nachher dokumentieren (F.6) | 0/3 | 3821111 | approved | 3821111, 4e5f1dc |
| step-007 | EPIC-02 | done | Einheit-011-Abschluss: Verifikation + nachgeholtes Kritiker-Review | 1/3 | 7b3f193 | issues → fix-01 approved | 7b3f193, 830b513, 831b2d3, 7f23841, cf3d7ac, 48871af, (review-commit) |
| step-007/fix-01 | EPIC-02 | done | TD-Referenzen + abgeschnittene Satzreste aus 3 Produktionsdateien entfernen (Rules §5-Konformität) | (1/3) | cf3d7ac | approved | cf3d7ac, 48871af, (review-commit) |
| step-008 | EPIC-03 | done | ILinterEngineConfig-Interface extrahieren, PathOverride-Liste auf Rest reduzieren (Muss-Haben C, TD-008/TD-010) | 0/3 | fd395c2 | approved | fd395c2, be6ff6a, 8d4e9c3, (review-commit) |
| step-009 | EPIC-04 | done (fix-01 pending) | rules.json-Auto-Discovery (B.1) + Verzeichnis-Sweep für neue/gelöschte .cs-Dateien (B.2) — silent-falsche Tool-Antworten beheben (Muss-Haben B, Punkte 1-2) | 0/3 | 1fd09c1 | issues → fix-01 | 1fd09c1, 677bef2, 914e0ba, (review-commit) |
| step-009/fix-01 | EPIC-04 | done | 3 B.1-Unit-Tests + step-result-Korrektur + 2 Kommentar-Sanierungen + stille-Catch-Sanierung in McpCodeGraphServerRefresh (Review-Findings 1-3 + Code-Qualität) | 0/3 | 60429e2 | approved | 60429e2, 6b24fe5, (review-commit) |
| step-010 | EPIC-05 | done | Last-Fixture + Skalierungsnachweis (B.3) + Kaltstart-Entkopplung (B.4) + mtime-Cache (B.5) + TD-005-Sanity-Fix + TD-007-Mitnahme | 0/3 | 0458250 | approved | 0458250, c3f926f, 60d32db, (review-commit) |

## Config (optional)

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
model_kritiker: Sonnet 5, Stufe High
```

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
