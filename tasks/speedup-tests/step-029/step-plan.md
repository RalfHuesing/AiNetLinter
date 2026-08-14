---
status: open
type: step-plan
task: speedup-tests
step: 029
corrects: null
title: "Master-Superstep: EPIC-6/7-Rest und Taskabschluss"
epic: EPIC-6, EPIC-7
estimated_risk: high
step_type: batch
items:
  - id: package-1
    title: "EPIC-6-Rest: 15 CLI-/MCP-/Performance-/Stressklassen"
  - id: package-2
    title: "EPIC-7-Restmigration: 38 verbleibende Ledgerklassen"
  - id: package-3
    title: "Legacy-Loeschung, Abschlussprofile und Messbericht"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-14T14:20:00+02:00
related_to:
  - master-low-cost-handoff.md
  - step-028/step-review.md
---

# Step 029: Master-Superstep fuer den gesamten Task-Rest

## Intention und Scope

Ein einziger hybrider Master-Step ersetzt weitere Detail-Stepordner. Der externe Coder arbeitet
die drei Pakete aus `../master-low-cost-handoff.md` nacheinander ab, darf innerhalb eines Pakets
kohaerente Zwischencommits setzen und wartet nicht auf neue Planung. Ein Audit erfolgt jeweils
erst nach dem vollstaendigen Paket.

## Aktueller Zustand

- Step 028 ist `approved`; Step 026/027 gelten ueber die Korrekturkette als geschlossen.
- Ledger: 53 pending. Paketgrenzen: 15 Klassen -> 38 pending; 38 Klassen -> 0 pending; danach
  Legacyprojekt/Support loeschen und Abschlussgates/Messung.
- Zielarchitektur und vorhandene TestKit-/Host-Seams stehen. Paket 1/2 duerfen keinen Produktcode
  und keine neue Produkt-Seam einfuehren.

## Verbindlicher Detailplan

Vollstaendig und ausschliesslich:

- `tasks/speedup-tests/master-low-cost-handoff.md`

Dort stehen pro Paket deterministische Auswahl, konkrete Klassenlisten, Zielprofile,
Fixtures/Hosts/Isolation, erlaubte/verbotene Aenderungen, Methoden-/Discovery-Baseline,
Kommandos/TRX-Namen, Fixbudget, Prozesscleanup, Commitgrenzen und Stopkriterien.

## Definition of Done

- Paket 1: 15 Klassen migriert, 38 pending; Dogfood/Performance/Stress nur discovered.
- Paket 2: alle 38 Restklassen migriert, 0 pending, Legacyprojekt noch baubar.
- Paket 3: Legacyprojekt/Support/Solution-/Rules-/Dokuvertraege bereinigt; `dotnet build`, Fast
  `Category!=Stress`, Integration `Category!=Stress`, Profilmessung und Drift-Audit erfolgreich.
- Stress ist migriert/kompiliert/discovered, wird aber ohne neue Nutzerfreigabe nicht ausgefuehrt
  und darf nicht als gruen behauptet werden.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc`
- `.agents/rules/AiNetLinterRichtlinien.mdc`
- `.agents/skills/drift-audit/SKILL.md`
