---
status: executing  # executing | done | aborted
task: 11_epic-projektregistry-und-daemon
started_at: 2026-08-23T12:48:00+02:00
last_updated: 2026-08-23T23:25:00+02:00
rules_dir: .agents/rules  # aus konzept.md Frontmatter übernommen (siehe ../spec.md §3.1)
total_steps: 6  # Summe aller Steps inkl. Korrekturen — Basis für den weichen Deckel (siehe Config, ../spec.md §10.5)
current_step: step-006
---

# Task State: 11_epic-projektregistry-und-daemon

## Übersicht

- **Task-Status:** `executing`
- **Steps gesamt:** 4 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-006` (in_progress — deterministische
  Regressionstest-Korrektur für die zwei Findings aus step-005)
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
- **Persistierter Initialauftrag:** siehe `initial-prompt.md`; bei
  Kontextkompaktierung zuerst gemeinsam mit diesem State und dem aktuellen
  Step-Plan lesen.
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-08-23T12:48:00+02:00
- **Zuletzt aktualisiert:** 2026-08-23T12:48:00+02:00

## Nutzer-Vorgaben (Effizienz, 2026-08-23)

In JEDEN Subagenten-Prompt zu übernehmen:

1. Große Steps: wenige, große Steps (Epic A in 3–5, Epic B in 3–5).
   Doku-/Sync-Pflichten landen im fachlich berührenden Step — keine
   eigenen Mini-Doku-Steps.
2. Tests wirtschaftlich: Coder entwickelt mit gefilterten Läufen
   (Category=Unit bzw. gezielte Filter); kompletter Nicht-Stress-Stack
   EINMAL pro Step vor Abschluss. Kritiker prüft Verträge/Qualität anhand
   step-result.md + Stichproben, wiederholt NICHT den kompletten Testlauf.
3. Kein Overhead bei Kleinem: eindeutige Korrektur-Findings über den
   mechanischen Transkript-Pfad ohne Planer-Aufruf. Tech-Debt nur
   dokumentieren. drift-audit einmal pro EPIC (nicht pro Step).
4. AiNetLinter-MCP-Server durchgehend nutzen (find_symbol, get_impact,
   get_violations, …) statt grep/Volltext-Lesen — Quality-Gates vor jedem Commit.

Modell-Hinweis des Nutzers: Subagenten nutzen dasselbe Modell wie der
Orchestrator; Kontrolle über die Verträge im Konzept, nicht durch Wiederholung.
Qualitätsstandards (TreatWarningsAsErrors, DoD je Epic, Testkatalog) unverändert.

## Steps

| Step | Epic | Status | Title | Corrects | Coded | Reviewed | Commit |
|------|------|--------|-------|----------|-------|----------|--------|
| step-001 | EPIC-A | done | Projektregistry-Grundlage: Definitionsdatei, Loader, Fehlerverträge, Config-Materialisierung | - | e0b25033 | 2026-08-23 (approved) | e0b25033 / 7ee7d805 |
| step-002 | EPIC-A | done | Projektregistry-Kern: Lease, Entry, Registry inkl. Eviction und FAILED-Marker | - | a80ec821 | 2026-08-23 (approved) | a80ec821 / e8b4e367 |
| step-003 | EPIC-A | done (Korrektur ausstehend) | MCP-Wiring auf die Projektregistry: Tool-Leases, harter Cut, Health-/Reload-/Overview-Vertrag | - | ccf7b33a / 790ce251 | 2026-08-23 (issues) | ccf7b33a / 790ce251 / b055ca4e |
| step-004 | EPIC-A | done (Korrektur ausstehend) | Produktions-Kalt-Load, Erstzugriffs-Dedupe und leasegeschützte Overview korrigieren | step-003 | 2ed8bcc0 / 190a1a25 | 2026-08-23 (issues) | 2ed8bcc0 / 190a1a25 / d56e9b59 |
| step-005 | EPIC-A | done (Korrektur ausstehend) | FAILED-Freigabe und Registry-Reservation atomar absichern | step-004 | a50bff9a / 1cd75558 | 2026-08-23 (issues) | a50bff9a / 1cd75558 / 1059cfcb |
| step-006 | EPIC-A | in_progress | Race-Interleavings in den Abnahmetests deterministisch verankern | step-005 | - | - | Plan erstellt |

## Config (optional)

Falls `<task-dir>/config.md` existiert, hier die Overrides dokumentieren.
Andernfalls gelten die Defaults aus `../spec.md`.

```
max_fix_rounds_per_step: 3        # Kettenlänge über `corrects`, siehe ../spec.md §10.5
soft_step_checkin_interval: 40    # weicher Deckel, kein Hard-Abort — siehe ../spec.md §10.5
max_batch_items: 8          # siehe ../spec.md §10.6 (Micro-Batches innerhalb eines Epics)
max_batch_diff_lines: 40    # siehe ../spec.md §10.6
build_command: dotnet build
test_command: dotnet test src/AiNetLinter.FastTests --filter Category!=Stress && dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
target_branch: main
model_planer: <nicht festgelegt>    # optional, siehe unten
model_coder: <nicht festgelegt>     # optional, siehe unten
model_kritiker: <nicht festgelegt>  # optional, siehe unten
```

<Die drei `model_*`-Felder sind optional und halten eine vom Nutzer
genannte, rollenabhängige Modellwahl fest. Werte sind freier Text —
der Workflow validiert sie nie. Nicht gesetzt = keine Vorgabe,
der Orchestrator fragt auch nicht nach. Siehe `../spec.md` §10.8.>

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
