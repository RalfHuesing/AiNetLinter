---
status: executing  # executing | done | aborted
task: 11_epic-projektregistry-und-daemon
started_at: 2026-08-23T12:48:00+02:00
last_updated: 2026-08-24T02:14:27+02:00
rules_dir: .agents/rules  # aus konzept.md Frontmatter übernommen (siehe ../spec.md §3.1)
total_steps: 10  # Summe aller Steps inkl. Korrekturen — Basis für den weichen Deckel (siehe Config, ../spec.md §10.5)
current_step: step-010
---

# Task State: 11_epic-projektregistry-und-daemon

## Übersicht

- **Task-Status:** `executing`
- **Nutzer-Scope:** vollständigen Task einschließlich EPIC-A und EPIC-B
  umsetzen; nicht nach dem Abschluss von EPIC-A stoppen.
- **Konfliktregel:** Review-Forderungen gegen inzwischen erreichte Architektur
  nicht erraten oder blind rückbauen; Konflikt als Blocker/Konzept-Entscheidung
  dokumentieren und mit dem nächsten sicheren fachlichen Schritt fortfahren,
  sofern keine Nutzerentscheidung für diesen Schritt erforderlich ist.
- **Steps gesamt:** 10 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-010` (open — EPIC-B DaemonHost-Lifecycle,
  Idle-Exit und MRU-Warmup)
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
- **Persistierter Initialauftrag:** siehe `initial-prompt.md`; bei
  Kontextkompaktierung zuerst gemeinsam mit diesem State und dem aktuellen
  Step-Plan lesen.
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-08-23T12:48:00+02:00
- **Zuletzt aktualisiert:** 2026-08-24T02:14:27+02:00

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
| step-003 | EPIC-A | done | MCP-Wiring auf die Projektregistry: Tool-Leases, harter Cut, Health-/Reload-/Overview-Vertrag | - | ccf7b33a / 790ce251 | 2026-08-23 (issues; korrigiert) | ccf7b33a / 790ce251 / b055ca4e |
| step-004 | EPIC-A | done | Produktions-Kalt-Load, Erstzugriffs-Dedupe und leasegeschützte Overview korrigieren | step-003 | 2ed8bcc0 / 190a1a25 | 2026-08-23 (issues; korrigiert) | 2ed8bcc0 / 190a1a25 / d56e9b59 |
| step-005 | EPIC-A | done | FAILED-Freigabe und Registry-Reservation atomar absichern | step-004 | a50bff9a / 1cd75558 | 2026-08-23 (issues; korrigiert) | a50bff9a / 1cd75558 / 1059cfcb |
| step-006 | EPIC-A | done | Race-Interleavings in den Abnahmetests deterministisch verankern | step-005 | 05b2e157 / 3dac2e2c | 2026-08-23 (issues; korrigiert) | 05b2e157 / 3dac2e2c / e7e0fdfe |
| step-007 | EPIC-A | done | Originalfehler und Creation-Loser im Testvertrag vollständig assertieren | step-006 | 73695524 / 91d9aae2 | 2026-08-24 (approved) | 73695524 / 91d9aae2 |
| step-008 | EPIC-A | done | EPIC-A-Abschluss: Drift-Audit, Overview-Liveprüfung und Meilenstein-Doku | - | 3c01d78a / 2760cf5e | 2026-08-24 (approved) | 3c01d78a / 2760cf5e |
| step-009 | EPIC-B | done | Transport-/Handshake-Grundlage für den Daemon | - | a6a6c40d | 2026-08-24 (approved) | a6a6c40d / 7897dd1b / b5715865 |
| step-010 | EPIC-B | open | DaemonHost-Lifecycle: interner Startpfad, Idle-Exit und MRU-Warmup | - | - | - | - |

## Config (optional)

Falls `<task-dir>/config.md` existiert, hier die Overrides dokumentieren.
Andernfalls gelten die Defaults aus `../spec.md`.

```
max_fix_rounds_per_step: 6        # Nutzerbewusst verdoppelt (3 → 6); Kettenlänge über `corrects`, siehe ../spec.md §10.5
soft_step_checkin_interval: 80    # Nutzerbewusst verdoppelt (40 → 80); weicher Deckel, kein Hard-Abort — siehe ../spec.md §10.5
max_batch_items: 16               # Nutzerbewusst verdoppelt (8 → 16); siehe ../spec.md §10.6
max_batch_diff_lines: 80          # Nutzerbewusst verdoppelt (40 → 80); siehe ../spec.md §10.6
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
