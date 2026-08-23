---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 004
epic: EPIC-A
step_type: single
coded_by: coder
coded_by_model: GPT-5
coded_by_model_knowledge_cutoff: nicht deklariert
coded_at: 2026-08-23T22:13:21+02:00
code_commit_hash: 2ed8bcc020b39021f196e4f4cadfa63bdd1a7680
status_after: done
blocker_category: n/a
---

# Result Step 004: Produktions-Kalt-Load, Erstzugriffs-Dedupe und leasegeschuetzte Overview korrigieren

## Zusammenfassung

Der produktive Solution-Kalt-Load propagiert nach dem Warn-Log die Originalexception; Tool und Overview liefern daraus den gemeinsamen `PROJECT_LOAD_FAILED`-Vertrag mit Solution-Kontext und Retry-/Restore-Hint. Eine per-Key-Reservation dedupliziert parallele Erstzugriffe ohne Registry-Lock waehrend Factory oder Solution-Load; konkurrierende Caller adoptieren dieselbe residente Instanz. Overview-Snapshot und Rendering bleiben bis zum Antwortaufbau leasegeschuetzt, waehrend FAILED-Marker und Health-Snapshots ihre geforderte Lebensdauer behalten.

## Geaenderte Dateien

- `src/AiNetLinter/Commands/McpServerCommand.cs` — Originalexception im produktiven Kalt-Load nach dem Warn-Log weiterreichen.
- `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs`, `ProjectEntry.cs`, `ProjectCreationReservation.cs` (neu) — Single-Flight-Reservation, Publish/Adopt, FAILED-Lifetime und Retry-Eviction.
- `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs`, `ProjectLoadFailure.cs` (neu) — gemeinsamer LoadFailed-Descriptor und konsistente Fehlerformatierung.
- `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs` — leasegebundener Snapshot-/Rendering-Pfad.
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTests.cs`, `WiringContractTests.cs`, `OverviewResourceLeaseContractTests.cs` (neu), `OverviewTestServers.cs` (neu) — synchronisierte Dedupe-, Marker- und Overview-Lifetime-Vertraege.
- `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs` — direkte und echte Kompositionsregression fuer den produktiven Kalt-Load.

## Commit

- **Code-Commit-Hash:** `2ed8bcc020b39021f196e4f4cadfa63bdd1a7680`
- **Message:**
  ```
  fix: MCP-Registry absichern [11_epic-projektregistry-und-daemon]

  Refs: tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-004
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```text
dotnet build → gruen (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → gruen (1680 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → gruen (351 Tests, 0 Fehler)
```

Gezielte Nachweise: `Category=Unit` → 1192/1192 gruen; Registry-/Overview-/Wiring-Slice → 26/26 gruen; Produktions-Kalt-Load und Health-Regression → 2/2 gruen; unabhaengiger Fixture-Dispose-Test → 1/1 gruen. Stress wurde nicht ausgefuehrt.

## MCP-Gates

- `get_impact` im uncommitteten Change-Context: 0 Violations; 8 geaenderte Dateien, 47 Aufrufstellen, 23 Test-Treffer.
- `get_violations`: Produktions-, FastTests- und IntegrationTests-Scope jeweils 0 Verstoesse.
- `safeguard` fuer `src/AiNetLinter/`: 10,00/10, PASS.
- `metrics_lookup`: ProjectRegistry 301 LOC / AI-Context 1809, OverviewResourceRegistration 150 LOC / AI-Context 2012; geaenderte Methoden innerhalb der bestehenden Grenzwerte.

## Abweichungen vom Plan

Fachlich keine. Die erste vollstaendige Integration-Validierung zeigte noch die Health-Snapshot-Auswirkung der neuen FAILED-Lifetime sowie einen unabhaengigen, gezielt reproduzierbaren Fixture-Dispose-Fehlschlag; die Lifetime wurde daraufhin innerhalb dieses Findings korrigiert, der Fixture-Test lief separat gruen, und der anschliessende finale Nicht-Stress-Abschlusslauf ist vollstaendig gruen. Fuer die DuplicateCode-Gatewarnung wurden gemeinsame Testserver-Helfer in `OverviewTestServers.cs` gebuendelt.

Roadmap, Tech-Debt und Drift-Audit wurden gemaess Auftrag nicht veraendert bzw. nicht ausgefuehrt.

## Beobachtungen

Die Health-Snapshot-Aggregation bleibt leasefrei und unveraendert; nur der Overview-Rendering-Pfad nutzt `SnapshotFor` innerhalb eines Leases. Der Kalt-Load-Kompositionstest haelt den produktiven Delegaten deterministisch vor der echten `TryLoadSolutionAsync`-Ausfuehrung an und prueft anschliessend Originalmeldung, Kontext, Hint und frischen Retry.

## Bekannte Unschärfen

Keine offenen funktionalen Unschärfen. Der Drift-Audit bleibt als Epic-Abschlussaktivitaet ausstehend.
