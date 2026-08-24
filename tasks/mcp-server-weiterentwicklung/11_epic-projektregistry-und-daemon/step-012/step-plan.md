---
status: done
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 012
corrects: step-011
title: "Direkte Prozess-Contracts für Daemon-Doppelstart und MCP-Pipe"
epic: EPIC-B
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-24T04:06:32+02:00
related_to:
  - step-011/step-plan.md
  - step-011/step-result.md
  - step-011/step-review.md
---

# Step 012: Direkte Prozess-Contracts für Daemon-Doppelstart und MCP-Pipe

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** `EPIC-B` aus `roadmap.md` — die zwei MAJOR-Findings aus dem
  Review von step-011 schließen, ohne ThinClient- oder externes Wiring
  vorwegzunehmen.
- **Korrektur:** `corrects: step-011`; ausschließlich die beiden Findings aus
  `step-011/step-review.md` sind Umfang dieses Steps.
- **Konzept-Referenz:** `Konzept.md` B.5 (sauberer `--daemon-start`-
  Doppelstartvertrag) sowie B.6 (sparsame echte Zwei-Prozess- und MCP-E2E-
  Contracts, nicht als Stresssuite).

## Aktueller Projektzustand (JIT-Kontext)

- `DaemonHostCommand.RunAsync` erstellt bereits den produktiven
  `DaemonHost` mit realem `DaemonPipeTransport`; `DaemonHost.RunAsync`
  erwirbt den endpointgebundenen Lock vor dem Accept-Loop und gibt bei
  Verlust eine sachliche stderr-Diagnose mit Exit-Code `1` zurück.
- `DaemonPipeTransport` stellt den echten Named-Pipe-Endpoint, Client-
  Verbindung und Frame-Serialisierung bereit. Nach dem Daemon-Handshake
  verwendet `DaemonHostCommand.RunMcpSessionAsync` denselben Stream für
  `StreamServerTransport` und den bestehenden MCP-SDK-Server.
- Die vorhandenen Daemon-FastTests nutzen kontrollierte Transports bzw.
  `MemoryStream` und können deshalb weder CLI-Routing/Prozesshandles noch
  die reale Pipe-zu-MCP-Grenze beweisen. Unter
  `src/AiNetLinter.IntegrationTests/Mcp/` existieren bereits
  `McpProcessRunner`, `SubprocessLifetimeBudget`, Fixture-Definitionen und
  MCP-SDK-Patterns; ein `Mcp/Daemon`-Anker fehlt noch.
- Der neue Contract verwendet deshalb echte `AiNetLinter.exe`-Prozesse und
  `NamedPipeClientStream` mit begrenzter Readiness-/Shutdown-Wartezeit.
  `McpProcessHost`/`McpRawWireTestHarness` bleiben für ihren bestehenden
  Stdio-Vertrag unverändert; ThinClient, Connect-or-Start und Retry-/Hänger-
  Schutz werden nicht implementiert.

## Intention

Nach diesem Step ist der Lock-Vertrag an der tatsächlichen CLI-/Prozessgrenze
regressionsfest: Zwei unabhängig gestartete `--daemon-start`-Prozesse können
nicht denselben Endpoint übernehmen, und der erste Host bleibt währenddessen
erreichbar. Zusätzlich belegt ein direkter MCP-SDK-Client auf der Host-Pipe
den Daemon-Handshake, die anschließende MCP-Initialisierung und mindestens
eine echte MCP-Anfrage; ein leerer `MemoryStream` gilt dafür nicht mehr als
Nachweis.

## Umfang / Nicht-Umfang

- Nur die beiden MAJOR-Findings aus `step-011/step-review.md`.
- Kein `ThinClientProxy`, kein Connect-or-Start, kein detached Client-Spawn,
  kein Retry-/Ping-/Kill-/Restart-Verhalten und kein vollständiges externes
  Wiring.
- Keine neue MCP-Toolregistrierung, kein zweiter JSON-RPC-Server, kein
  EXE-Injektions- oder Stellvertreterprozess. Die Testeinstiege starten die
  gebaute `AiNetLinter.exe` direkt mit `--daemon-start`.
- Keine Stresskategorie und keine lastintensive Paralleltestserie; höchstens
  zwei gleichzeitig laufende echte Hostprozesse im gezielten Contract.

## Konkrete Änderungen

### `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostProcessContractTests.cs`

- **Was:** Einen echten Prozess-Contract auf Basis von
  `SubprocessLifetimeBudget` und `McpProcessRunner` anlegen. Prozess A wird
  mit `--daemon-start` und einer kurzen, deterministischen
  `--mcp-daemon-idle-exit-minutes`-Dauer gestartet; die Bereitschaft wird
  ausschließlich über eine begrenzte Verbindung zum realen
  `NamedPipeClientStream` am `DaemonPipeTransport`-Endpoint festgestellt.
  Prozess B startet dieselbe EXE mit demselben Endpoint und denselben
  Daemon-Argumenten. Assertiere: B beendet sich ohne Timeout mit Exit-Code
  ungleich null und stderr enthält die bestehende endpointbezogene
  Lock-Diagnose; A bleibt erreichbar. Nach dem kontrollierten Disconnect
  muss A auslaufen, und ein abschließender dritter Start darf den Endpoint
  wieder übernehmen, damit die Freigabe des realen Prozess-Locks belegt ist.
- **Warum:** Nur dieser Test erkennt Fehler in CLI-Routing, realem
  Named-Handle-Lifecycle, Pipe-Bind oder Rückgabe des Lock-Verlierers; ein
  `IDaemonInstanceLock`-Double oder ein in-proc Host kann diese Grenze nicht
  prüfen.

### `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostMcpProcessContractTests.cs`

- **Was:** Einen direkten Client-Harness für die Host-Pipe ergänzen, der
  `NamedPipeClientStream` mit dem bestehenden Endpoint verbindet, den
  `DaemonHello` als echtes Pipe-Frame sendet und das `DaemonWelcome` vor dem
  MCP-Durchsatz validiert. Danach denselben verbundenen Stream über den
  bestehenden MCP-SDK-`StreamClientTransport` an `McpClient.CreateAsync`
  übergeben und `ListToolsAsync` ausführen; mindestens die bereits
  registrierten Tools `find_symbol` und `get_violations` werden assertiert.
  Der Client wird sauber disposed/disconnected und der gestartete Host muss
  innerhalb der kurzen Idle-Exit-Grenze enden.
- **Warum:** Der Contract beweist die Reihenfolge
  Daemon-Handshake → MCP-Initialize → echte MCP-Request/Response über die
  Produktions-Pipe und ersetzt den bisherigen leeren `MemoryStream`-Test.

### Test-Harness innerhalb `src/AiNetLinter.IntegrationTests/Mcp/Daemon/`

- **Was:** Nur falls die beiden Tests gemeinsame Prozessverwaltung brauchen,
  einen kleinen internen Helper für Start, begrenzte Readiness-Prüfung und
  resilienten Cleanup teilen. Bestehende `McpProcessRunner`,
  `SubprocessLifetimeBudget`, `DaemonPipeTransport`-Framing und das
  `StreamClientTransport`-Pattern werden wiederverwendet; für Fixture- oder
  Logdateien gilt `TestTempDirectory`. Keine parallele Collection-
  Serialisierung und keine zweite allgemeine MCP-Testinfrastruktur.
- **Warum:** Der Helper verhindert doppelte Prozess-/Pipe-Aufräumlogik,
  bleibt aber auf diesen direkten Host-Contract begrenzt.

## Akzeptanzkriterien

- [ ] Zwei echte `AiNetLinter.exe --daemon-start`-Prozesse auf demselben
      effektiven Pipe-Endpoint sind im Test beteiligt; B erhält deterministisch
      stderr plus Exit-Code `!= 0`, A wird nicht ersetzt oder beendet.
- [ ] Der Test zeigt die Lock-Freigabe nach dem vollständigen Shutdown durch
      einen erfolgreichen erneuten Hoststart; kein Prozess bleibt bei Fehlern
      hängen.
- [ ] Der MCP-Process-Contract verbindet sich direkt mit der Host-Pipe,
      validiert `hello`/`welcome`, erstellt danach eine echte MCP-SDK-Session
      und erhält eine Antwort auf `tools/list`.
- [ ] Kein Test verwendet für den zu beweisenden Host-/MCP-Pfad einen leeren
      `MemoryStream`, eine injizierte EXE oder einen ThinClient.
- [ ] Die Tests sind deterministisch begrenzt, nicht mit `Category=Stress`
      markiert und verwenden die vorhandene Subprozess-/Fixture-Infrastruktur.

## Tests

- [ ] `DaemonHostProcessContractTests` — realer Start/Lock-Verlust/
      Erreichbarkeit/Shutdown/Re-Start mit zwei (kurzzeitig drei) echten EXE-
      Prozessen und Named Pipe.
- [ ] `DaemonHostMcpProcessContractTests` — direkter Pipe-Handshake,
      MCP-SDK-Initialize und `tools/list` über den produktiven Daemon-Pfad.
- [ ] Abschluss-Gates erst im Umsetzungsschritt gemäß `task-state.md`:
      `dotnet build`, beide Nicht-Stress-Testprojekte; in diesem Planungs-
      Schritt werden keine Tests ausgeführt.

## Definition of Done

- [ ] Beide MAJOR-Findings aus `step-011/step-review.md` sind durch die
      genannten realen Prozess-/Pipe-Contracts geschlossen.
- [ ] Die bestehende Produktionssemantik bleibt unverändert, soweit für die
      direkte Beobachtbarkeit kein minimaler Testzugang erforderlich ist;
      ThinClient, Connect-or-Start, Retry/Hänger-Schutz und externes Wiring
      bleiben außerhalb des Steps.
- [ ] Build und vollständiger Nicht-Stress-Teststack sind im
      Umsetzungsschritt grün; keine Stresssuite wird ergänzt.
- [ ] Ein gezielter Conventional-Commit enthält nur die Korrekturdateien und
      den aktualisierten Step-Status/Task-Plan.
- [ ] `step-012/step-result.md` ist geschrieben und der Step-Status nach
      Umsetzung auf `approved` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#agent-resilience` — neue
  Testhelper bleiben klein, nullable und räumen Prozesse/Pipes sichtbar auf.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3. Windows-Umgebung & Tool-Regeln`
  und `#4. Updates & Tests` — Windows-Named-Pipes, `TestTempDirectory`,
  xUnit-v3, zentrale Subprozess-Lebensdauer und keine Stresssuite.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5. Qualitätsdrift-Prävention` —
  bestehende Harness-/Fixture-Strukturen wiederverwenden, keine Symptom-
  Assertions und kein Drift-Audit in diesem Fix.

## Bekannte Ausnahmen

- Der Endpoint ist bewusst der reale benutzergebundene Produktions-Endpoint;
  die Subprozess-Lebensdauer wird über den vorhandenen Test-Gate begrenzt.
  Ein fremder, außerhalb der Tests gestarteter Daemon ist eine externe
  Umgebungsstörung und wird als solcher diagnostiziert, nicht durch Kill oder
  globales State-Aufräumen maskiert.
- Der MCP-Contract prüft `tools/list` statt eines projektladenden Analyse-
  Calls; damit bleibt er fokussiert auf Handshake/Session/Transport und führt
  weder ThinClient-Wiring noch zusätzliche Solution-Load-Latenz ein.

## Notes

- Die vorhandene Lock-Diagnose lautet laut Produktionspfad
  `[ERROR]: Daemon fuer Pipe-Endpunkt '<name>' laeuft bereits.`; der Test
  soll den stabilen sachlichen Teil prüfen, nicht eine volatile PID.
- Die direkte MCP-Verbindung muss das Daemon-Level-Handshake-Frame vor dem
  SDK-Transport konsumieren. Erst danach darf `StreamClientTransport` den
  bereits verbundenen Stream für MCP übernehmen.
- Keine MRU-/Health-/Observability-Erweiterung und keine Konzeptänderung;
  diese Korrektur schließt nur die zwei Review-Findings.
