---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 009
epic: EPIC-B
step_type: single
coded_by: coder
coded_by_model: GPT-5
coded_by_model_knowledge_cutoff: nicht deklariert
coded_at: 2026-08-24T01:59:15+02:00
code_commit_hash: a6a6c40d
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 009: Transport-/Handshake-Grundlage für den Daemon

## Zusammenfassung

Unter `Mcp/Daemon/` sind die transportneutralen Pipe-/Handshake-Verträge für
EPIC-B umgesetzt. Der Endpoint bindet Named Pipes an den aktuellen Benutzer,
die Verbindung validiert newline-delimited JSON-Objekte und reicht gültige
Payload-Bytes unverändert weiter. Die Handshake-State-Machine prüft
Protokoll-/Executable-Version, liefert `welcome`, entscheidet den einmaligen
`shutdown`-Fall ohne Ping-Pong und meldet Konfigurationsdivergenzen einmalig
strukturiert; pro Verbindung bleibt Cancellation isoliert.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Daemon/DaemonProtocol.cs` (neu) — Protokollkonstanten,
  Wire-Records, Konfiguration und Handshake-Ergebnisverträge.
- `src/AiNetLinter/Mcp/Daemon/DaemonHandshake.cs` (neu) — injizierbarer
  Identitätsprovider und deterministische Handshake-State-Machine.
- `src/AiNetLinter/Mcp/Daemon/DaemonPipeTransport.cs` (neu) — aktueller
  Benutzer-Endpoint mit `CurrentUserOnly`, async NDJSON-Framing und
  per-Verbindung-Cancellation.
- `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHandshakeContractTests.cs` (neu) —
  Unit-Contracts für Versionen, Welcome, Divergenz und Anti-Ping-Pong.
- `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonPipeTransportContractTests.cs` (neu) —
  Unit-Contracts für Framing, Benutzerbindung, Byte-Roundtrip und Disconnect.
- `Docs/agent-api.md` — Pipe-Level-Transport-/Handshake-Vertrag und Grenzen.
- `Docs/integration.md` — aktueller, noch nicht verdrahteter Integrationsstand.
- `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/codemap.md` —
  Pointer für Produktions- und Testbereiche.

## Commit

- **Code-Commit-Hash:** `a6a6c40d`
- **Message:**
  ```
  feat(daemon): Baue Pipe-Grundlage [11_epic-projektregistry-und-daemon]

  Refs: tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-009
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit nach Abschluss der Artefakte.

## Build-/Test-Output

- `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~Daemon --no-restore` → grün (11 Tests, 0 Fehler)
- `dotnet build` → grün (0 Warnungen, 0 Fehler)
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` → grün (1693 Tests, 0 Fehler)
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` → grün (352 Tests, 0 Fehler)
- Stress-Tests → nicht ausgeführt

## MCP-Quality-Gates

- `get_violations` für `src/AiNetLinter/Mcp/Daemon` → 0 Violations.
- `safeguard` für denselben Scope → 10,00/10 bei Threshold 8,00, PASS.
- `metrics_lookup` für `DaemonProtocol`, `DaemonHandshake`,
  `DaemonPipeTransport` und `DaemonPipeConnection` → alle LOC-,
  AI-Context-Footprint- und Public-Member-Grenzen eingehalten.
- Vorab wurden `get_feature_context`/`find_symbol` für den neuen Scope sowie
  der bestehende MCP-/Registry-/Lifetime-Bestand verwendet; der bestehende
  Stdio-/Registry-Vertrag blieb unverändert.

## Abweichungen vom Plan

Keine — der fachliche Plan wurde 1:1 umgesetzt. Die vorgesehene
Transportgrundlage bleibt unabhängig von `McpServerCommand`, `Program`,
Registry-Wiring, ThinClient, DaemonHost, Idle-Exit, MRU und Health-Wiring.

## Beobachtungen

Die Named-Pipe-ACL wird über `PipeOptions.CurrentUserOnly` am Serverstream
festgelegt; eine zusätzliche `PipeSecurity`-Abhängigkeit oder explizite
Benutzer-ACL-Builder-Schicht war für diesen transportneutralen Step nicht
erforderlich. Die Stream-Contracts bleiben absichtlich in-proc und vermeiden
echte Zwei-Prozess- oder Stress-Orchestrierung.

## Bekannte Unschärfen

Die ACL wurde im Contract über den gesetzten `CurrentUserOnly`-Pipe-Optionwert
und nicht über einen echten Zwei-Prozess-Zugriffstest nachgewiesen. Der
Handshake erhält `activeConnectionCount` von der späteren Host-Schicht; die
Grundlage besitzt selbst keinen Registry- oder Lifecycle-Zustand. Eine echte
Daemon-Session mit SDK-Pump, Retry, Idle-Exit oder MRU ist nicht Bestandteil
dieses Ergebnisses.

## Falls Status `blocked`

Nicht zutreffend.
