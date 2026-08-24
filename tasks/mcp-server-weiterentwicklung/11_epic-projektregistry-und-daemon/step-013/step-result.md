---
status: done (pending audit)
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 013
epic: EPIC-B
completed_at: 2026-08-24T10:30:00+02:00
model: GPT-5
model_knowledge_cutoff: nicht deklariert
code_commit: b9605ea5aa1d00c3e29b3f0d5d3e3098df0edd5e
---

# Step 013 Ergebnis: ThinClient und EPIC-B-Abschluss

## Ergebnis

Der normale `--mcp-server`-Einstieg läuft jetzt über einen SDK-freien ThinClient:
Connect-first, detached Spawn-second, bestehender Handshake, opake NDJSON-Frame-Pump
und genau ein begrenztes Replay-Fenster. Der ThinClient besitzt den Parent-Reaper;
der `--daemon-start`-Host bleibt parent-ungebunden. `AINETLINTER_NO_DAEMON=1`
führt weiterhin in den bisherigen in-proc-Stdio-Pfad.

Health liefert im Daemon-Kontext `mode`, `connectionId`, `connections`, `processId`,
`uptimeSeconds`, `keys` und `daemonVersion`. Das Observability-Paket 1.0.3 bietet
keine freien Zusatzfelder; deshalb werden `mode` und `connectionId` zusätzlich im
application-level ServerName/ServerVersion geführt und strukturiert über Health
ausgegeben. Es wurde kein Paket-Bump und keine neue Registry eingeführt.

## Geänderte Bereiche

- `src/AiNetLinter/Mcp/Daemon/`: `ThinClientProxy`, `ThinClientLauncher`,
  `DaemonBytePump`, Runtime-Kontext sowie Welcome-/Transport-/Host-Wiring.
- `src/AiNetLinter/Mcp/`: normales Routing, daemonweiter Flagtransport und
  Health-/Observability-Wiring.
- `src/AiNetLinter.FastTests/Mcp/Daemon/`: Launcher-, Pump- und Welcome-Contracts.
- `src/AiNetLinter.IntegrationTests/Mcp/`: normaler ThinClient, Escape, stdout-
  Purity, Parent-Tod, Daemon-Prozess-/MCP-Contracts und Legacy-Testisolation.
- Doku und Statusdateien: README, `Docs/{agent-api,integration,configuration,ROADMAP}.md`,
  EPIC-Roadmap, Codemap, §C.5, Task-State und dieser Step.

## Verifikation

Gezielte Tests nach den letzten Korrekturen:

- ThinClient-Fast-Contracts: 3/3 grün.
- Normaler ThinClient, `AINETLINTER_NO_DAEMON=1` und NDJSON-Rahmenvertrag: 3/3 grün.
- Parent-Exit-Lifetime: 1/1 grün.
- Direkter Daemon-Host/MCP-Pipe-Contract: 1/1 grün.
- Observability- und Doppelstart-Contracts: im fokussierten Lauf grün.
- Kein Stress-Test wurde ausgeführt.

Die vom Coder geforderten vollständigen Läufe wurden jeweils genau einmal gestartet:

- `dotnet build`: 0 Warnungen, 0 Fehler. Nach der letzten kleinen Codekorrektur wurde
  zusätzlich ein abschließender `dotnet build --no-restore` mit 0/0 bestätigt.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`: 1715/1716
  bestanden; ein bestehender, timingabhängiger EPIC-A-Race-Test
  (`ProjectRegistryTests.Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner`)
  schlug im Vollparallelauf fehl und lief im gezielten Wiederholungslauf 1/1 grün.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`:
  352/356 bestanden. Die vier Fehlschläge traten im gemeinsamen Vollparallelauf
  durch den neuen Shared-Daemon-Endpunkt bzw. eine noch alte Versionsrepräsentation
  in einem bestehenden Harness auf; die betroffenen Verträge wurden danach gezielt
  isoliert, korrigiert und grün nachgewiesen. Der Vollstack wurde gemäß Vorgabe nicht
  wiederholt.

Diese beiden Vollstack-Ausnahmen sind bewusst nicht durch breite Tech-Debt- oder
Stress-Änderungen kaschiert; sie bleiben für den Review sichtbar.

## MCP-, Sync- und Dogfood-Gates

- MCP-Server: `Loaded`, Version `1.0.125`, Solution `AiNetLinter.slnx`.
- Produktionsscope, Fast-Daemon-Testscope und Integration-MCP-Testscope: jeweils
  0 Violations.
- Safeguard: jeweils 10,00/10 PASS.
- Finaler Footprint: ThinClientProxy 226 LOC / 1033 Footprint, Launcher 58/80,
  BytePump 159/194, RuntimeContext 13/29, HealthTool 195/2174; alle Grenzwerte OK.
- Drift-Audit genau einmal und ausschließlich:
  `find_duplicates(scopeDir="src", minTokens=20, similarityThreshold="exact", mode="clone")`.
  Ergebnis: ein bestehender exakter Test-Helper-Cluster zwischen
  `ProjectRegistryTestDoubles.MinimalConfig()` und
  `TestConfigFactory.CreateEmpty()`. Befund ist bewusst No-op/TD-002-ähnlicher
  Test-Infrastruktur-Overlap; kein neuer Code und keine weitere Drift-Analyse.
- `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` war No-op:
  `.agents/rules/AiNetLinter.mdc` bereits aktuell.
- Repo-`.mcp.json` und eigene Hermes-Registrierung wurden geprüft und enthalten
  bereits den finalen `command`-Aufruf mit `--mcp-server`; externe/fremde Dateien
  wurden nicht verändert. Das Live-Dogfood lief über den eigenen C#-Process-Harness
  und prüfte Daemon-Health einschließlich PID, Uptime, Keys, Version und connectionId.

## Bewusste Ausnahmen und Vertragsgrenzen

- Pipe-/ACL-/NDJSON-/Handshake-/Anti-Ping-Pong-/Lock-/Idle-Exit-/MRU-Verträge aus
  EPIC-A und step-012 wurden nicht ersetzt.
- Der ThinClient enthält weder MCP-SDK noch JSON-RPC-Parser; JSON wird nur für den
  bestehenden Pipe-Handshake gelesen. MCP-Nutzdaten werden byte-/frame-opak gepumpt.
- Kein HTTP/TCP, Remote-/Multi-User-Betrieb, Service, neue Registry oder breiter
  Tech-Debt-Fix.
- Die zwei Vollparallel-Suiteabweichungen werden nicht als grüne Vollsuite behauptet;
  ihre fokussierten Nachweise und die Ursache stehen oben.

## Commits

1. `b9605ea5aa1d00c3e29b3f0d5d3e3098df0edd5e` — Code und Tests.
2. folgt nach diesem Doku-/Status-Commit.
