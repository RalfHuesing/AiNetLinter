---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 012
epic: EPIC-B
step_type: single
reviewed_by: kritiker
reviewed_by_model: GPT-5
reviewed_by_model_knowledge_cutoff: 2024-06
reviewed_at: 2026-08-24T04:56:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 012: Direkte Prozess-Contracts für Daemon-Doppelstart und MCP-Pipe

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: beide MAJOR-Verträge sind durch die neuen echten Prozess-/Pipe-Contracts abgedeckt.
- [x] Rules-Konformität: neue IntegrationTests sind nicht als Stress markiert, nutzen `TestTempDirectory`, begrenzte Prozess-/Pipe-Cleanup-Pfade und haben 0 Violations.
- [x] Logische Korrektheit: der Doppelstart prüft stderr, Nicht-Null-Exit, Erreichbarkeit des ersten Hosts und erfolgreichen dritten Start nach Lock-Freigabe; der MCP-Test prüft realen Daemon-Handshake, MCP-SDK-Initialize und `tools/list`.
- [x] Konzept-Treue: ThinClient, Connect-or-Start, Retry-/Hänger-Schutz und externes Wiring bleiben außerhalb; der direkte B.5/B.6-Nachweis ist geschlossen.
- [x] Build: laut `step-result.md` grün mit 0 Fehlern und 0 Warnungen (nicht wiederholt).
- [x] Tests: gezielter Lauf beider neuen Tests grün (2/2); der vollständige Nicht-Stress-Stack ist laut `step-result.md` grün und wurde nicht wiederholt.

## Befund

### Plan-Erfüllung

`DaemonHostProcessContractTests.TwoDaemonProcessesOnOneEndpointRejectSecondAndReleaseLock` in `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostProcessContractTests.cs:11` startet die gebaute EXE zweimal am selben Named-Pipe-Endpunkt, belegt den ersten Host, assertiert den zweiten Prozess über `McpProcessRunner.RunAsync` mit stderr und Exitcode, prüft A erneut und verifiziert die Lock-Freigabe mit einem dritten erfolgreichen Start. `DaemonHostMcpProcessContractTests.HostPipeHandshakeThenMcpInitializeListsToolsAndExitsIdle` in `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostMcpProcessContractTests.cs:14` verbindet direkt per `DaemonPipeTransport`, konsumiert `DaemonWelcome` und lässt `McpClient.CreateAsync` anschließend `tools/list` über den bestehenden Stream ausführen; ein leerer `MemoryStream` kommt in diesem Pfad nicht vor.

### Rules-Konformität

Der neue Scope `src/AiNetLinter.IntegrationTests/Mcp/Daemon/` meldet 0 Violations und Safeguard 10,00/10; beide Klassen tragen ausschließlich `Trait("Category", "Integration")`, der scoped MCP-Pattern-Check findet keine Stress-Kategorie und keine `MemoryStream`-Verwendung, die Testmetriken liegen innerhalb der Grenzwerte.

### Logische Korrektheit

Der Produktionspfad bleibt real: `DaemonHostCommand.RunAsync` erstellt `DaemonPipeTransport` und `DaemonHost`, dessen `RunAsync` den `DaemonInstanceLock` vor dem Accept-Loop erwirbt; `RunMcpSessionAsync` erstellt `StreamServerTransport` und `McpServer`, während der Testclient mit `McpClient.CreateAsync` die MCP-Initialisierung ausführt. Die bestehende `McpProcessRunner.RunAsync` liest stdout und stderr parallel und liefert den echten Exitcode samt Timeoutstatus zurück. Stichproben der bestehenden `ProjectRegistryTests`, `McpHandshakeToolRegistrationTests`, `McpServerCommandContractTests` und `McpLiveRepositoryTests` zeigen unveränderte Vertragsanker; der Code-Commit `ffb60157` enthält ausschließlich die drei neuen IntegrationTest-Dateien.

### Konzept-Treue (Ebene 4)

Die beiden in `Konzept.md` B.5/B.6 geforderten direkten Prozessgrenzen sind erfüllt, ohne den ausdrücklich ausgeklammerten ThinClient-/Connect-or-Start-/Retry-/Hänger-Schutz- oder externen-Wiring-Scope vorwegzunehmen; die Tests bleiben sparsame IntegrationTests und sind nicht Stress.

### Build-/Test-Status

```text
dotnet build → laut step-result.md grün (0 Fehler, 0 Warnungen; nicht wiederholt)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → laut step-result.md grün (1713 Tests, 0 Fehler; nicht wiederholt)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → laut step-result.md grün (354 Tests, 0 Fehler; nicht wiederholt)
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~DaemonHostProcessContractTests|FullyQualifiedName~DaemonHostMcpProcessContractTests" --no-restore → grün (2 Tests, 0 Fehler)
Stress-Tests → nicht ausgeführt
Drift-Audit → nicht ausgeführt gemäß Task-Vorgabe
```
