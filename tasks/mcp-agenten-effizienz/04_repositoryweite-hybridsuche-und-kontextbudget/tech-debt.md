# Tech-Debt: Repositoryweite Hybridsuche und Kontextbudget

## Offene Einträge

Keine.

## Erledigte Einträge

### TD-003-001 — Overview-Grenzen vervollständigen

- **Erzeugt in:** `step-003/step-review.md`
- **Erledigt in:** `step-004`
- **Status:** erledigt
- **Nachweis:** `OverviewResourceRegistration.ToolSummaries` beschreibt `enrichCSharp=true` als Opt-in innerhalb des geladenen Solution-/Projekt-Snapshots, weist `ambiguous`/`unavailable` aus und nennt bei Trunkierung die Folge über Pattern, Scope oder Limits. `OverviewResourceRegistrationTests` prüft diese vier Vertragsbestandteile zusätzlich zur bestehenden Tool-Parität.

## Drift-Audit 2026-08-21

- Tokenbasierter `exact`-Scan über `src` mit `minTokens=20`: keine Cluster.
- Tokenbasierter `near`-Scan über `src` mit `minTokens=20`: 25 Cluster; die geprüften Paare sind überwiegend Testvarianten, symmetrische Fixture-/Option-Builder oder getrennte fachliche Scanner. Kein neuer, mechanisch sicherer Refactoring-Schritt im aktuellen Such-/MCP-Scope.
- Struktureller Scan über `src` mit `minTokens=10`: 14 Kandidatencluster. Die `CliOptionFactory`-/Fixture-/Dispose-Gruppen sind absichtliche Boilerplate bzw. unterschiedliche Lebenszykluskontexte; `MetricsTreeScanner` und DuplicateDetection-Cluster liegen außerhalb dieses Tasks und benötigen separate Architekturentscheidung. Keine automatische Konsolidierung vorgenommen.
- Im geänderten SearchPattern-/MCP-Scope wurde kein Exact-Clone gefunden. EPIC-04/05 werden daher ohne neuen Tech-Debt-Eintrag abgeschlossen; EPIC-06 bleibt als nächster großer Evaluations-Step offen.
