---
status: executing
task: flaky-and-test-performance
started_at: 2026-08-07T08:55:00+02:00
last_updated: 2026-08-07T17:30:00+02:00
rules_dir: .agents/rules  # aus konzept.md Frontmatter übernommen
total_steps: 16  # Summe aller Steps inkl. Korrekturen — Basis für den weichen Deckel (siehe ../spec.md §10.5)
current_step: step-016  # geplant, wartet auf Coder (EPIC-03)
---

# Task State: flaky-and-test-performance

## Übersicht

- **Task-Status:** `executing`
- **Steps gesamt:** 13 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-015` (done, approved — letzter
  EPIC-02-Schritt; Unit 1193 + Integration 132 = Total 1325, Kritiker
  hat projektweit verifiziert: 0 ungetaggte `[Fact]`/`[Theory]` im
  gesamten `src/AiNetLinter.Tests/`-Baum)
- **Roadmap:** siehe `roadmap.md` (EPIC-01 + EPIC-02 abgehakt; EPIC-03
  bis EPIC-08 offen, unangetastet für eine spätere Session).
- **Nutzer-Vorgabe (2026-08-07):** „setze EPIC-03 um, dann stop" — Loop
  läuft weiter bis EPIC-03 abgeschlossen, danach erneuter bewusster Halt.
- **step-016 (geplant):** deckt EPIC-03 vollständig ab — Korrektur einer
  step-001-Fehleinschätzung entdeckt (`SymbolGraphCatalogFixture`
  tatsächlich 18× statt 1× verwendet), inkl. aktivem Dispose-Risiko-Fix.
- **Tech-Debt:** siehe `tech-debt.md` (TD-001 mittel; TD-002..TD-006 niedrig;
  TD-007 durch step-013 erledigt)
- **Gestartet:** 2026-08-07
- **Zuletzt aktualisiert:** 2026-08-07 (step-013 approved, Modellwahl pro
  Rolle auf Sonnet 5 High/Medium/Medium gesetzt)

## Steps

| Step | Epic | Status | Title | Corrects | Coded | Reviewed | Commit |
|------|------|--------|-------|----------|-------|----------|--------|
| step-001 | EPIC-01 | done | Spike — SymbolGraphMcpFixture auf ICollectionFixture umstellen, Vorher/Nachher messen | - | MiniMax-M3 | MiniMax-M3 | bf5de7e / cc395d0 |
| step-002 | EPIC-02 | done | Category-Traits für alle Tests in src/AiNetLinter.Tests/Suppression/ nachziehen (Batch 1 von N) | - | MiniMax-M3 | MiniMax-M3 | 3ae94c2 / 79d3d6d |
| step-003 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Metrics/ nachziehen (Batch 2 von N) | - | MiniMax-M3 | MiniMax-M3 | 67fb86b / 03b04f4 |
| step-004 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Web/ nachziehen (Batch 3 von N) | - | MiniMax-M3 | MiniMax-M3 | 57f7f03 / ecd9dfa |
| step-005 | EPIC-02 | done | Category-Traits für Arch/Diag/FalsePositives/Cache nachziehen (Batch 4 von N) | - | MiniMax-M3 | MiniMax-M3 | b15a198 / fe95a08 |
| step-006 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Evals/ nachziehen (Batch 5 von N) | - | MiniMax-M3 | MiniMax-M3 | f88c223 / 5d7df9b |
| step-007 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Output/ nachziehen Teil 1 (Batch 6a, 5 Klassen D-O) | - | MiniMax-M3 | MiniMax-M3 | 9c4269f / a2e9b3f |
| step-008 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Output/ nachziehen Teil 2 (Batch 6b, 4 Klassen P-V) | - | MiniMax-M3 | MiniMax-M3 | 95ab4d5 / b23a4cf |
| step-009 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Configuration/ nachziehen (Batch 7, 8 Klassen) | - | MiniMax-M3 | MiniMax-M3 | b484627 / b4a8c59 |
| step-010 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Core/Checkers/ nachziehen Teil 1 (Batch 8a, 8 Klassen A-MethodParameterCountAccessibility) | - | MiniMax-M3 | MiniMax-M3 | 44956b7 / 2674a46 |
| step-011 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Core/Checkers/+Core/ nachziehen (Batch 8b, Mega-Batch 20 Klassen) | - | MiniMax-M3 | MiniMax-M3 | bb39619 / 2a4067a / daad777 (3 Commits, Hash-Korrektur) |
| step-012 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Core/+Maps/ nachziehen (Batch 9, Mega-Batch 17 Klassen) | - | MiniMax-M3 | Cursor Grok 4.5 | b2477f5 / 7deeff1 |
| step-013 | EPIC-02 | done | Category-Traits für Mcp/Tools/ (17 Klassen) + TD-007 Hilfsdateien löschen (Mega-Batch) | - | Sonnet 5 | Sonnet 5 | 0d5cee2 / 5c4600c |
| step-014 | EPIC-02 | done | Category-Traits für Rest-EPIC-02 (Mcp/-Root + Baseline/ + Commands/-Teil + Cli/, 20 Klassen, Mega-Batch) | - | Sonnet 5 | Sonnet 5 | c46d839 / 98e2e9a |
| step-015 | EPIC-02 | done | Category-Traits für McpServerCommandTests.cs — letzter EPIC-02-Schritt (20 method-level Items) | - | Sonnet 5 | Sonnet 5 | 2cf236f / e1d316b |
| step-016 | EPIC-03 | done | Fixture-Sharing: SymbolGraphCatalogFixture (18×) + McpLiveRepositoryFixture (2×) auf ICollectionFixture | - | Sonnet 5 | Sonnet 5 | 6dfd588 / 39991a2 |

## Config

Overrides aus `config.md` (Stand 2026-08-08), Rest Defaults aus
`../../.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md`:

```
max_fix_rounds_per_step: 3
soft_step_checkin_interval: 40
max_batch_items: 20
max_batch_diff_lines: 80
build_command: dotnet build
test_command: dotnet test
target_branch: main
model_planer: Sonnet 5, Reasoning-Stufe High (Nutzer-Vorgabe 2026-08-07)
model_coder: Sonnet 5, Reasoning-Stufe Medium (Nutzer-Vorgabe 2026-08-07)
model_kritiker: Sonnet 5, Reasoning-Stufe Medium (Nutzer-Vorgabe 2026-08-07)
```

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
