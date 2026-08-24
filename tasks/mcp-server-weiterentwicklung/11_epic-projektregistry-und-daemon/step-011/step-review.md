---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 011
epic: EPIC-B
step_type: single
reviewed_by: kritiker
reviewed_by_model: GPT-5
reviewed_by_model_knowledge_cutoff: 2024-06
reviewed_at: 2026-08-24T04:00:00+02:00
verdict: issues
resolved_by: step-012
tech_debt_ids: []
---

# Review Step 011: DaemonHost-Korrektur

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: Produktionskorrekturen und fokussierte in-proc Contracts sind vorhanden; die im Plan ausdrücklich vorgesehenen direkten Host-/MCP-Prozess-Contracts fehlen.
- [x] Rules-Konformität: `get_violations` meldet 0 Verstöße im Daemon-Produktionsscope; `safeguard` steht bei 10,00/10; die betroffenen Typen und der MCP-Session-Runner liegen innerhalb der Metrikgrenzen.
- [x] Logische Korrektheit: Lock-Erwerb liegt vor `AcceptLoopAsync`, Connection-Registrierung vor Handlerstart, Completion-Bereinigung ist idempotent, MRU-Roots werden vor Speicherung kanonisiert und Shutdown schreibt unabhängig von `dirty`; die reale Prozessgrenze ist aber nicht als Regressionstest belegt.
- [x] Konzept-Treue: ThinClient, Connect-or-Start, Stdio-Pump, Retry/Hänger-Schutz und externe Verdrahtung bleiben außerhalb des Verdicts; der aus B.5/B.6 verlangte direkte Host-/MCP-Prozessvertrag ist innerhalb des Step-Scopes nicht vollständig umgesetzt.
- [x] Build: laut `step-result.md` grün (0 Warnungen, 0 Fehler; nicht wiederholt).
- [x] Tests: laut `step-result.md` grün (1713 FastTests und 352 IntegrationTests, jeweils ohne Stress; nicht wiederholt); fokussierter Daemon-Lauf selbst grün (27 Tests).

## Befund

### Plan-Erfüllung

Die vier Produktionsänderungen sind im Commit `1c7ee714` erkennbar. `DaemonHost.RunAsync` erwirbt den endpointgebundenen Lock vor dem Start von `AcceptLoopAsync`; `RegisterConnection` registriert Client, Task und Handle unter `lifecycleGate`, bevor `HandleConnectionAsync` gestartet wird; `CompleteConnection` entfernt die zugehörigen Einträge und setzt die Completion genau einmal. `MruStateStore.Read` normalisiert gültige Roots und verwirft ungültige Daten, `DisposeAsync` erzwingt einen finalen Snapshot-Schreibversuch.

Die Plan-Elemente `DaemonHostProcessContractTests` und `DaemonHostMcpProcessContractTests` aus `step-011/step-plan.md:250-258` fehlen jedoch vollständig: `src/AiNetLinter.IntegrationTests/Mcp/Daemon/` existiert nicht und der semantische Symbolfund liefert für beide Testklassen keine Treffer. Die vorhandenen Tests sind `DaemonPipeTransportContractTests.InstanceLock_AllowsOneOwnerAndReleasesForNextOwner`, `DaemonHostLifecycleTests.RunAsync_WhenInstanceLockIsHeld_ReturnsNonZeroBeforeAccepting`, `RunAsync_QuickEofCleansConnectionBeforeIdleExitAndReleasesLock`, `RunAsync_WritesHandshakeBeforeStartingSessionRunner` und der direkte `DaemonHostCommand.RunMcpSessionAsync`-EOF-Test. Sie belegen die in-proc-Pfade, aber nicht die vom Plan geforderte echte Prozessgrenze.

### Rules-Konformität

Die MCP-Quality-Gates sind für `src/AiNetLinter/Mcp/Daemon` grün: 0 Violations, Safeguard 10,00/10. `DaemonHost`, `DaemonInstanceLock`, `MruStateStore`, `DaemonHostCommand.RunMcpSessionAsync` und die relevanten Produktionsmetriken liegen innerhalb der konfigurierten Limits; kein Rules-Finding trägt das Verdict.

### Logische Korrektheit

Der Lock ist ein nicht dateibasierter benannter BCL-Semaphore mit dem effektiven Pipe-Namen, wird in `DaemonHost.RunAsync` vor dem ersten Accept erworben und durch `DaemonHostCommand.RunAsync` erst beim vollständigen `await using`-Shutdown freigegeben. Der Lock-Verlierer schreibt die sachliche Fehlermeldung und liefert `1`, ohne den Transport anzufassen.

Der Registration-Race ist strukturell geschlossen: `DaemonHost.RegisterConnection` legt beide Dictionaries und den Clientzähler vor dem Handlerstart an; `HandleConnectionAsync` führt Handshake und Session aus; `CompleteConnection` entfernt unter demselben Gate und verhindert durch die Registrierungskontrolle doppelte Dekrementierung. Der gezielte Daemon-Testlauf bestätigt 27 grüne Tests.

`MruStateStore.Read` wendet `TryNormalizeEntry` vor Grouping und Dictionary-Speicherung an. `Remove` verwendet denselben `CanonicalizeRoot`-Schlüssel; `DisposeAsync` ruft `WriteSnapshotAsync` auch ohne Touch oder `dirty` auf, und Schreibfehler werden geloggt. Die beiden neuen MRU-Tests decken leere/korrupt geladene Dateien sowie den Alias-Root ab.

Die verbleibende Lücke ist der Nachweis gegen zwei unabhängig gestartete Host-Prozesse und die echte Host-/MCP-Prozessgrenze. Ein direkter BCL-Lock-Test plus ein injizierter `TrackingInstanceLock`-Host-Test kann einen Fehler in Endpoint-Erzeugung, CLI-Routing, Prozesshandle oder tatsächlichem Lock-Lifecycle nicht erkennen.

### Konzept-Treue (Ebene 4)

Die ausdrücklich späteren ThinClient-/Connect-or-Start-Themen wurden nicht vorweggenommen. `Program.Main` routet den unveränderten `--mcp-server`-Pfad weiterhin zu `McpServerCommand.RunAsync`; dieser baut weiterhin `ProjectRegistry` und `StdioServerTransport` auf. EPIC-A-Registry- und Step-009-Handshake-Verträge zeigen in den semantischen Referenzen keine betroffene Änderung.

B.5 verlangt für den Doppelstart einen sauberen stderr-/Exitcode-Vertrag an der Host-CLI-Grenze, und B.6/der Step-Plan verlangen dafür direkte Host-/MCP-Prozess-Contracts. Die Umsetzung dokumentiert diese Tests in `step-result.md:63-67` als ausgelassen, begründet dies aber mit ThinClient-/Connect-or-Start-Scope. Diese Begründung trifft den ausdrücklich direkten Host-Vertrag nicht; der Muss-Haben-Nachweis bleibt daher unvollständig.

### Build-/Test-Status

```text
dotnet build → laut step-result.md grün (0 Warnungen, 0 Fehler; nicht wiederholt)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → laut step-result.md grün (1713 Tests, 0 Fehler; nicht wiederholt)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → laut step-result.md grün (352 Tests, 0 Fehler; nicht wiederholt)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~Mcp.Daemon" --no-restore → grün (27 Tests, 0 Fehler)
Stress-Tests → nicht ausgeführt
Drift-Audit → nicht ausgeführt
```

## Findings

1. `src/AiNetLinter.IntegrationTests/Mcp/Daemon/` (fehlt); Bezug zu `step-011/step-plan.md:250-258` — **[MAJOR] [Plan/Logik]** Der endpointgebundene Lock ist implementiert und durch `DaemonInstanceLock` sowie einen Host-Test mit injiziertem Verlierer-Seam abgedeckt, aber der geforderte echte Zwei-Prozess-Contract fehlt. Es gibt weder `DaemonHostProcessContractTests` noch einen Test, der zwei echte `--daemon-start`-Prozesse auf demselben effektiven Pipe-Endpoint startet, den zweiten deterministisch mit stderr und Nicht-Null-Exit abweist, die Erreichbarkeit des ersten Hosts prüft und anschließend die Lock-Freigabe verifiziert. **Fix:** Den im Step-Plan vorgesehenen direkten Host-Prozess-Contract unter `src/AiNetLinter.IntegrationTests/Mcp/Daemon/` ergänzen; er darf ausschließlich `--daemon-start`/Pipe/Lock prüfen und keine ThinClient-, Connect-or-Start- oder Stdio-Pump-Logik voraussetzen.

2. `src/AiNetLinter.IntegrationTests/Mcp/Daemon/` (fehlt); Bezug zu `step-011/step-plan.md:253-258` — **[MAJOR] [Plan/Logik]** Der direkte MCP-Nachweis bleibt auf `DaemonHostMcpContractTests.RunMcpSessionAsync_UsesTheExistingMcpSessionRunnerOnConnectionEof` mit einem leeren `MemoryStream` begrenzt. Damit ist der echte Host-/MCP-Prozessvertrag aus B.6 nicht regressionsfrei belegt: keine echte Pipe-Verbindung führt Handshake, MCP-Session-Erstellung/Durchsatz, verbindungsbezogene Cancellation, Registry-Erhalt und Idle-Exit zusammen aus; insbesondere bleibt der im Plan genannte `DaemonHostMcpProcessContractTests`-Contract aus. **Fix:** Einen direkten Host-/MCP-Integrationstest ergänzen, der ohne ThinClient über die Host-Pipe den Step-009-Handshake und eine echte MCP-SDK-Session ausführt, danach nur diese Verbindung trennt, Registry/andere Sessions unberührt lässt und den kontrollierten Idle-Exit samt MRU-Flush nachweist.
