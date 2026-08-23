---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 006
epic: EPIC-A
step_type: single
coded_by: coder
coded_by_model: GPT-5
coded_by_model_knowledge_cutoff: nicht deklariert
coded_at: 2026-08-23T23:58:00+02:00
code_commit_hash: 05b2e157
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 006: Race-Interleavings in den Abnahmetests deterministisch verankern

## Zusammenfassung

Die beiden beanstandeten Abnahmetests reproduzieren ihre relevanten Interleavings
jetzt über deterministische Barrieren. Der Cold-Load-Test gibt den Initial-Lease
vor dem Loading-Aufruf frei, lässt den Fault im `BeforeLeaseRelease`-Interleaving
sichtbar werden und prüft danach Fehlerantwort vor frischem Retry. Der Registry-Test
pausiert nach dem Resident-Lookup, lässt konkurrierendes Publish und einen anderen
Root zu und prüft für den Ziel-Root genau einen Factory-/Load-Pfad sowie die
gemeinsame residente Instanz.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` — test-only Barrier-Seam nach dem Lookup mit atomarer Re-Prüfung im normalen Registry-Pfad.
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTests.cs` — deterministischer Lookup→Reservation-Test mit getrennten Ziel-/Other-Root-Zählern und Disposal-Nachweis.
- `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs` — deterministischer Loading→Fault→Release-Harness ohne Zugriff auf `LoadTask`.
- `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/codemap.md` — Pointer für den neuen Seam und die beiden Testanker aktualisiert.
- `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-006/step-plan.md` — Status auf `done (pending audit)` gesetzt.

## Commit

- **Code-Commit-Hash:** `05b2e157`
- **Message:**
  ```
  fix: Race-Tests verankern [11_epic-projektregistry-und-daemon]

  Deterministische Loading-/Fault- und Lookup-/Reservation-Interleavings in den Abnahmetests sichern.

  Refs: tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-006
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater zweiter Commit folgt nach diesem Result-/Codemap-Update.

## Build-/Test-Output

Gezielte Iteration:

```
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner → grün (1 Test, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~ProductionColdLoad_BrokenSlnx_ReturnsOriginalLoadFailedContract → grün (1 Test, 0 Fehler)
```

Einziger vollständiger Abschlusslauf:

```
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1681 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (351 Tests, 0 Fehler)
```

Stress-Tests und Drift-Audit wurden nicht ausgeführt.

## MCP-Quality-Gates

- `get_feature_context`, `get_symbol_body`, `find_references` und `get_impact` prüften den Registry-Pfad, den neuen Seam und beide Testanker; der Impact des Registry-Pfads zeigte die vorhandenen Lease-Aufrufer ohne zusätzliche Scope-Auswirkung.
- `get_violations`: 0 Violations in `src/AiNetLinter`, `src/AiNetLinter.FastTests` und `src/AiNetLinter.IntegrationTests`.
- `safeguard`: 10,00/10 in allen drei geänderten Projekt-Scopes bei Threshold 8,00.
- `metrics_lookup`: Produktionsmethode `TryAdoptOrCreate` 33 LOC / CC 5 / CogC 5; Barrier-Helfer 7 LOC; Registry-Test 55 LOC / CC 3 / CogC 4; Integrationstest 34 LOC / CC 1 / CogC 0; alle Gates PASS.

## Abweichungen vom Plan

Der Plan wurde fachlich eingehalten. Zusätzlich war der ausdrücklich erlaubte
minimale test-only Seam `ProjectRegistryOptions.BeforeCreationReservation` nötig,
weil der bestehende `BeforeLeaseRelease`-Seam den Lookup→Reservation-Punkt nicht
erreicht; der Default-Produktionspfad bleibt unverändert atomar.

## Beobachtungen

Der Step-005-Test-Harness hatte die Ziel- und Other-Root-Zähler zuvor in einer
gemeinsamen Factory, wodurch eine globale `InstancesCreated`-Assertion den
Other-Root-Anker nicht getrennt nachweisen konnte. Der korrigierte Test verwendet
deshalb getrennte Test-Factories innerhalb desselben Registry-Locks.

## Bekannte Unschärfen

Die Step-004-Regression wurde nicht durch einen Historiencheckout ausgeführt.
Der korrigierte Anker startet den zweiten Caller vor der Reservation; bei der
früheren getrennten Lookup-/Reservation-Struktur wären dadurch zwei Creation-Pfade
sichtbar, während der atomare Step-005-Pfad genau einen Ziel-Root-Pfad erzeugt.

## Falls Status `blocked`

Nicht zutreffend.
