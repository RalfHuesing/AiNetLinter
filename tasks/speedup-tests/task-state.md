---
status: executing  # executing | blocked | done | aborted
task: speedup-tests
started_at: 2026-08-12
last_updated: 2026-08-14
rules_dir: .agents/rules
total_steps: 27  # Summe aller Steps inkl. Korrekturen — Basis für den weichen Deckel (siehe Config, ../spec.md §10.5)
current_step: step-027 (open; hybrid handoff)
---

# Task State: speedup-tests

## Übersicht

- **Task-Status:** `executing`
- **Steps gesamt:** 27 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-027` (`open`; korrigiert die drei blockierenden Findings aus
  Step 026; Umsetzung im kostenoptimierten Hybrid-Handoff)
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-08-12
- **Zuletzt aktualisiert:** 2026-08-14

## Steps

<Diese Tabelle wächst mit jedem Planer-Aufruf (oder Orchestrator-
Transkript bei eindeutigen Korrekturen, siehe `../spec.md` §6.2.1) um
genau eine Zeile. Die Spalte „Corrects" bleibt bei regulären Steps leer,
bei Korrekturen steht dort der Step, den sie korrigieren — daraus ergibt
sich die Kettenlänge fürs Fix-Budget (§10.5), keine separate Zählung
mehr nötig.>

| Step | Epic | Status | Title | Corrects | Coded | Reviewed | Commit |
|------|------|--------|-------|----------|-------|----------|--------|
| step-001 | EPIC-1 | done | Fundament: Zielprojekte + TestProject.props | - | b1fe9eb | approved | 9d20376 |
| step-002 | EPIC-1 | done | Fundament: Migrationsledger, Guards, Baseline-Messung | - | cd1c80f | issues→approved (via step-003) | c5d4b10 |
| step-003 | EPIC-1 | done | Korrektur: Nachweis Ledger-Guard | step-002 | c16be1a | approved | c16be1a |
| step-004 | EPIC-1 | done | Fundament: IVT, Safety Envelope, Legacy-Gate-Switch | - | a303edb | issues→approved (via step-005) | 59dcff9 |
| step-005 | EPIC-1 | done | Korrektur: AiNetLinterRichtlinien.mdc §4 | step-004 | bffe3e3 | approved | 2c9611c |
| step-006 | EPIC-2 | done | Testplattform: RoslynTestSolutionFactory + PreparedSolutionFixture | - | f258992 | approved | 45322c3 |
| step-007 | EPIC-2 | done | Testplattform: IsolatedFixtureLease + MsBuildFixtureHost | - | b2ebfbb | approved | 45361b5 |
| step-008 | EPIC-2 | done | Testplattform: FilterMini-Fixture (Disk + In-Memory) | - | 968c35a | issues→approved (via step-009) | 243f2db |
| step-009 | EPIC-2 | done | Korrektur: FilterMiniFidelityTests IsTestProject | step-008 | 1d64b47 | approved | 296447f |
| step-010 | EPIC-3 | done | Checkers-Kohorte -> Unit (28 Klassen) | - | 8c1552f | approved | 9245277 |
| step-011 | EPIC-3 | done | Web-Parser-Kohorte -> Unit (5 Klassen) | - | b720e1b | approved | 317f90c |
| step-012 | EPIC-3 | done | Renderer-Kohorte -> Unit + Epic-Grenzgate | - | eb645b8 | approved | be663e6 |
| step-013 | EPIC-4 | done | Skeleton-Filterkohorte auf FilterMini migrieren | - | 8edee78 | issues→approved (via step-014) | 086ce31 |
| step-014 | EPIC-4 | done | Korrektur: Namespace-Glob selektiv kalibrieren | step-013 | f41fd31 | approved | 34eaa0b |
| step-015 | EPIC-4 | done | Duplicate-Detection-Scanner auf In-Memory-Plattform | - | 9abadf9 | approved | 63d3333 |
| step-016 | EPIC-4 | done | Refactoring-Drift-Scanner auf In-Memory-Plattform | - | 14ea50c | approved | 7578235 |
| step-017 | EPIC-4 | done | Duplicate-Detection-Engine auf In-Memory-Plattform | - | b8730a7 | approved | 7f54028 |
| step-018 | EPIC-4 | done | Kumulativer MCP-Read-only-Snapshot-Super-Step (23 Klassen, Suppression Legacy) | - | e864407 -> f0dbacc | approved (Re-Audit 9cc8b73) | Code e864407/f0dbacc; Doku b1a59b7; Review 9cc8b73 |
| step-019 | EPIC-4 | done | EPIC-4-Grenze: Find-Symbol-Snapshotmatrix und Nicht-C#-Dateiadapter | - | 6413510 | issues→approved (via step-020) | Code 6413510; Doku 1c9c531; Review af61a93 |
| step-020 | EPIC-4 | done | Korrektur: doppelten Find-Symbol-No-Match-Vertrag konsolidieren | step-019 | 5041b00 | approved | Code 5041b00; Doku 9f60d03/27a6d9b; Review 28b3cb4 |
| step-021 | EPIC-5 | issues | MSBuild-/Baseline-/Datei-/Refresh-Super-Step (22 Klassen) | - | b2b8fde | issues | 22 Legacy-Dateien entfernt; Korrektur via step-022 |
| step-022 | EPIC-5 | done | Korrektur: globales MSBuild-Loadgate und read-only Server-Ownership | step-021 | 5aa397f/1c5090d | approved | 3f94674 |
| step-023 | EPIC-5 | issues | Config-/Suppression-Dateikohorte und EPIC-5-Grenzgate (21 Klassen) | - | 312b652 | issues | d37a2f7 |
| step-024 | EPIC-5 | done | Korrektur: deterministische EPIC-5-Grenzprofile | step-023 | 30918b9 | approved | Code 30918b9; Doku 00c3e33; Review 8b577ca |
| step-025 | EPIC-6 | issues | EPIC-6-Start: deterministische Mini-MCP-Prozesshosts (21 Klassen) | - | uncommittiert | blocked by Fast-Runtime-Dependency-Cleanup | - |
| step-026 | EPIC-6 | issues | Korrektur: Runtime-sauberer MCP-Vertragsschnitt und vollständiger Hostabschluss | step-025 | 06fdc20 | issues (Audit 2026-08-13; pausiert auf Nutzerwunsch) | 06fdc20 |
| step-027 | EPIC-6 | open | Korrektur: Git-Workspace-Cleanup und Kategorieguard abschliessen | step-026 | - | hybrid handoff; ausstehend | - |

## Config (optional)

> **ACHTUNG AN ALLE AGENTEN (Planer, Coder, Kritiker):**
> Wir befinden uns in einer reinen Struktur-Migrationsphase (Dateien verschieben, Namespaces anpassen).
> Um den extremen Overhead durch generiertes Markdown (`step-plan`/`step-result`) zu stoppen, MÜSSEN ab sofort große logische Batches (bis zu 40 Dateien) in "Super-Steps" zusammengefasst werden.
> Das alte Limit von 8 Dateien pro Step ist explizit aufgehoben. Bündelt Kohorten effizient!

Falls `<task-dir>/config.md` existiert, hier die Overrides dokumentieren.
Andernfalls gelten die Defaults aus `../spec.md`.

```
max_fix_rounds_per_step: 6        # Kettenlänge über `corrects`, siehe ../spec.md §10.5
soft_step_checkin_interval: 40    # weicher Deckel, kein Hard-Abort — siehe ../spec.md §10.5
max_batch_items: 40         # ERHÖHT FÜR MIGRATION: Da die Zielarchitektur steht, Kohorten zu Super-Steps zusammenfassen, um Overhead zu sparen!
max_batch_diff_lines: 800   # ERHÖHT FÜR MIGRATION: Reines Schieben und Namespace-Anpassungen machen große Diffs sicher.
build_command: <aus roadmap.md Tech-Stack-Notiz>
test_command: <aus roadmap.md Tech-Stack-Notiz>
target_branch: <aktueller Branch, nicht hartcodiert>
model_planer: GPT-5.6 Sol, Stufe Medium
model_coder: Adaptiv durch Orchestrator — Terra Medium (low), Terra High (medium/high), Sol High nur bei aussergewoehnlicher Komplexitaet
model_kritiker: GPT-5.6 Terra, Stufe Medium
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

## Abbruch-/Pause-Bedingungen

- **Kettenbudget erreicht** (`max_fix_rounds_per_step`, Default 3, über
  die `corrects`-Kette gezählt, ohne `approved`): der zuletzt korrigierte
  Step → `blocked`, Loop pausiert für diese Kette, Nutzer klärt. **Kein**
  Task-Abbruch dadurch.
- **Weicher Deckel erreicht** (`soft_step_checkin_interval`, Default 40,
  bei jedem Vielfachen der Gesamt-Step-Zahl): Zwischenfrage an den
  Nutzer, kein automatischer Abbruch. Nur eine ausdrückliche Ablehnung →
  Task `aborted`, siehe `task-summary.md`.
- **Blocker aufgetreten** (Step mit Status `blocked`): Loop pausiert,
  Nutzer klärt.
- **Tech-Debt-Einträge lösen NIE einen Abbruch oder Blocker aus** — sie
  sind reine Beobachtung, kein Steuerungssignal (siehe `../spec.md` §9).
  Auch `auto_fixable: ja`-Einträge lösen nichts eigenständig aus, sie
  werden nur an ohnehin laufende Steps angehängt.
</content>
</invoke>
