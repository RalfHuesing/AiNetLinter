---
task: flaky-and-test-performance
type: task-config
purpose: Task-level Overrides der drift-loop-Defaults aus spec.md §10.5/§10.6
maintained_by: orchestrator
last_updated: 2026-08-08T08:55:00+02:00
---

# Config: flaky-and-test-performance

Task-spezifische Overrides der drift-loop-Defaults. Wirksam ab dem nächsten
Planer-Aufruf im Step-Modus (siehe `../../.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md`
§10.5 für `max_fix_rounds_per_step` / `soft_step_checkin_interval` und §10.6 für
`max_batch_items` / `max_batch_diff_lines`).

## Overrides

```
max_fix_rounds_per_step: 3           # unverändert (spec-Default) — keine Korrekturen ausgewichen
soft_step_checkin_interval: 40       # unverändert (spec-Default) — Soft-Deckel in Steps, noch nicht erreicht
max_batch_items: 20                  # 2.5x spec-Default (8)  — Begründung: User-Feedback "bündle größer"; EPIC-02-Batches sind mechanisch uniform (additive Trait-Zeilen), brauchen keinen 8er-Zwang
max_batch_diff_lines: 80             # 2x spec-Default (40)   — Konservativ bei ~5 Diff-Zeilen/Klasse (Trait + ggf. XML-Doc/Klassen-Deklaration) ergibt 20 Klassen ~60-100 Zeilen
build_command: dotnet build          # aus roadmap.md Tech-Stack-Notiz
test_command: dotnet test            # aus roadmap.md Tech-Stack-Notiz
target_branch: main                  # aktueller Branch, nicht hartcodiert
model_planer: Sonnet 5, Reasoning-Stufe High     # Nutzer-Vorgabe 2026-08-07
model_coder: Sonnet 5, Reasoning-Stufe Medium    # Nutzer-Vorgabe 2026-08-07
model_kritiker: Sonnet 5, Reasoning-Stufe Medium # Nutzer-Vorgabe 2026-08-07
```

## Begründung der Batch-Aufweichung

User-Feedback vom 2026-08-08: "weiter, bündle die coder steps zu größeren, nicht
immer solche mini änderungen."

Konkret:
- Bis step-010 wurden EPIC-02-Batches mit 4-8 Klassen pro Step umgesetzt
  (8 Schritte für 47 Klassen = ~6 Klassen/Step im Schnitt). Die drei
  Subagent-Rollen (Planer/Coder/Kritiker) liefen jeweils pro Step.
- Der Planer-Aufwand war in den EPIC-02-Batches sehr uniform (gleiche
  Heuristik, gleiche Trait-Syntax, gleiche DoD-Struktur), ebenso der
  Coder-Aufwand (rein additiv, mechanisch, byte-genaue EOL/BOM-Sorgfalt
  je nach Befund).
- Mit `max_batch_items: 20` bündeln wir EPIC-02-Restbestand in ~6-8
  Mega-Steps statt 12+ Mini-Steps; Orchestrierungs-Overhead sinkt
  entsprechend.
- Die fachliche Sorgfalt pro Rolle bleibt **unverändert** (Spec §10.6:
  "Sorgfalt pro Item ... dieselbe wie ein eigenständiger Step"). Die
  drei Rollen (Planer/Coder/Kritiker) bleiben **verpflichtend** pro Step
  (Spec §6: "Keine Rolle überspringen").
- Der `step_type: batch`-Mechanismus ist genau für diesen Fall gedacht
  (Spec §10.6: "Micro-Batches. Mehrere einzeln triviale Änderungen würden
  bei strikter 1-Step-pro-Änderung-Regel jede für sich einen vollen
  Planer→Coder→Kritiker-Zyklus samt eigener Commits durchlaufen — Overhead,
  der in keinem Verhältnis zur Änderung steht.").

## Auswirkung auf laufenden Task

Kein Einfluss auf step-001..step-010 (alle bereits abgeschlossen, commits
unveränderlich — siehe spec.md §10.3 History-Reset-Verbot). Wirkt erst ab
dem nächsten Planer-Aufruf (step-011 ff.).
