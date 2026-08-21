# Tech-Debt: Repositoryweite Hybridsuche und Kontextbudget

## Offene Einträge

Keine.

## Erledigte Einträge

### TD-003-001 — Overview-Grenzen vervollständigen

- **Erzeugt in:** `step-003/step-review.md`
- **Erledigt in:** `step-004`
- **Status:** erledigt
- **Nachweis:** `OverviewResourceRegistration.ToolSummaries` beschreibt `enrichCSharp=true` als Opt-in innerhalb des geladenen Solution-/Projekt-Snapshots, weist `ambiguous`/`unavailable` aus und nennt bei Trunkierung die Folge über Pattern, Scope oder Limits. `OverviewResourceRegistrationTests` prüft diese vier Vertragsbestandteile zusätzlich zur bestehenden Tool-Parität.
