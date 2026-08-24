---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 011
epic: EPIC-B
step_type: single
coded_by: coder
coded_by_model: GPT-5
coded_by_model_knowledge_cutoff: 2024-06
coded_at: 2026-08-24T03:49:16+02:00
code_commit_hash: 1c7ee714
status_after: done
blocker_category: n/a
---

# Result Step 011: DaemonHost-Korrektur

## Zusammenfassung

Die vier Step-010-Findings sind gezielt korrigiert: Ein endpointgebundener,
nicht dateibasierter BCL-Semaphore-Claim entscheidet Doppelstarts vor dem ersten
Pipe-Bind und wird erst nach Registry-/MRU-Shutdown freigegeben. MRU-Lesen
normalisiert kanonische Roots, toleriert leere/korrupt eingelesene Daten und
erzwingt beim Dispose einen atomaren gültigen Snapshot. Der Host registriert
Verbindungen vor dem Handlerstart mit idempotenter Completion-Bereinigung; direkte
Run-/Accept-/Handshake- und bestehende MCP-Session-Runner-Contracts belegen den
Lifecycle-Vertrag.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Daemon/DaemonInstanceLock.cs` (neu) — endpointgebundener OS-Claim ohne Dateistate.
- `src/AiNetLinter/Mcp/Daemon/DaemonHost.cs` — Lock-Lifecycle und registrierungsfeste Connection-Bereinigung.
- `src/AiNetLinter/Mcp/Daemon/DaemonPipeTransport.cs` — kontrollierbarer Accept-Transport ohne Änderung am Named-Pipe-Multiplexing.
- `src/AiNetLinter/Mcp/Daemon/MruStateStore.cs` — kanonische Root-Schlüssel und erzwungener Shutdown-Flush.
- `src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs` — bestehender MCP-Session-Runner für direkte Contracts zugänglich.
- `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostLifecycleTests.cs` — Run-/Accept-/EOF-/Handshake-/Lock-Loser-Contracts.
- `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostMcpContractTests.cs` (neu) — direkter MCP-Session-Runner-Contract.
- `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonPipeTransportContractTests.cs` — Exklusivitäts- und Freigabe-Contract.
- `src/AiNetLinter.FastTests/Mcp/Daemon/MruStateStoreTests.cs` — Korrupt-/Leer-Flush und Alias-Entfernung.

## Commit

- **Code-Commit-Hash:** `1c7ee714`
- **Message:**
  ```
  fix: Daemon-Lifecycle-Korrekturen [11_epic-projektregistry-und-daemon]

  Sichere Host-Exklusivitaet, MRU-Normalisierung und registrierungsfeste Connection-Cleanup-Pfade; ergaenze direkte Host-/MCP-Contracts. Refs: tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-011
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1713 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (352 Tests, 0 Fehler)
```

## Abweichungen vom Plan

Die direkten Host-/MCP-Contracts wurden im bestehenden FastTests-Daemon-Anker
umgesetzt. Ein zusätzlicher Zwei-Prozess-Integrationstest wurde nicht ergänzt,
weil dieser Step ausdrücklich ThinClient-/Connect-or-Start-Wiring ausklammert;
der produktive CLI-/Pipe-Lock-Pfad bleibt über den echten BCL-Claim und den
direkten Host-Contract abgesichert.

## Beobachtungen

Der erste Lock-Entwurf mit `Mutex` wäre wegen threadgebundener Besitzfreigabe
nicht async-lifecyclefest gewesen; er wurde vor dem Commit durch einen benannten
BCL-Semaphore mit Kapazität eins ersetzt. Der weiterhin ausstehende Epic-B-Teil
umfasst ThinClient, Connect-or-Start, stdio↔Pipe-Pump, Retry/Hänger-Schutz,
externes Wiring und Health/Observability; diese Bereiche wurden nicht vorweggenommen.

## Bekannte Unschärfen

Der Semaphore-Name folgt dem bereits effektiven benutzerspezifischen Pipe-Namen
und ist damit an denselben Endpoint gebunden. Die Prozessgrenze ist durch den
Named-Handle-Vertrag gegeben; ein externer ThinClient-Race bleibt bewusst dem
nachgelagerten Fachschritt vorbehalten. Der Drift-Audit bleibt gemäß Task-State
bis zum EPIC-B-Abschluss offen.
