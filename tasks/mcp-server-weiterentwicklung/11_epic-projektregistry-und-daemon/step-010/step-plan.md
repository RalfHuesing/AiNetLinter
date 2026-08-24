---
status: done
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 010
corrects: null
title: "DaemonHost-Lifecycle: interner Startpfad, Idle-Exit und MRU-Warmup"
epic: EPIC-B
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-24T02:14:27+02:00
related_to:
  - step-009/step-result.md
  - step-009/step-review.md
---

# Step 010: DaemonHost-Lifecycle: interner Startpfad, Idle-Exit und MRU-Warmup

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** `EPIC-B` aus `roadmap.md` — die geprüfte Transport-/Handshake-Grundlage
  wird erstmals mit der geteilten Projektregistry und einem langlebigen Host-Prozess
  verbunden.
- **Konzept-Referenz:** `konzept.md` B.1, B.3, B.4, B.5 Schritte 2 und 5 sowie B.6/B.7.

## Aktueller Projektzustand (JIT-Kontext)

- `Program.Main` routet `--mcp-server` aktuell direkt zu `McpServerCommand.RunAsync`;
  dieser Pfad erzeugt die `ProjectRegistry`, baut den vorhandenen
  `McpServerOptionsFactory`-Stack und verwendet `StdioServerTransport`. Er bleibt in
  diesem Step als bestehender Stdio-Vertrag unverändert.
- `ProjectRegistry` ist ein vorhandener, asynchron disposable Registry-Kern mit Lease-,
  LRU-/TTL-, FAILED- und Pending-Adoption-Verträgen. Der Host besitzt keine zweite
  Registry und erweitert diese nur um Prozess-Lifecycle-Aufrufe, Warmup und geordnetes
  Dispose.
- `McpServerOptionsFactory` kann bereits Tool-/Resource-Collections für eine Registry
  bauen. Der Host verwendet dieses Muster je Verbindung, statt Toolregistrierungen oder
  `McpCodeGraphServer`-Instanzen zu duplizieren.
- `Mcp/Daemon/DaemonProtocol.cs`, `DaemonHandshake.cs` und
  `DaemonPipeTransport.cs` sind durch step-009 `approved`: Pipe-Name/ACL, NDJSON,
  Handshake, Anti-Ping-Pong und verbindungsbezogene Cancellation werden konsumiert,
  nicht neu interpretiert.
- `McpServerLifetime` enthält den bestehenden `ParentProcessWatchdog`. Dieser wird im
  Daemon bewusst nicht verwendet; der Host steuert seine Lebensdauer ausschließlich
  über verbundene Clients, aktive Loads/Warmups und Idle-Exit.
- `tech-debt.md` bleibt außerhalb des Scopes: TD-002/TD-005 betreffen den noch offenen
  Diagnosekanal, TD-004 die nicht entschiedene Soft-Cap-Semantik und TD-006 die
  Testinfrastruktur. Keine dieser Beobachtungen wird automatisch behoben.

## Intention

Nach diesem Step existiert ein eigenständig startbarer `DaemonHost`, der die geprüfte
Pipe-/Handshake-Schicht mit genau einer geteilten `ProjectRegistry` und einer MCP-Session
je Verbindung verbindet. Er startet über den internen CLI-Pfad `--daemon-start`, wärmt
MRU-Kandidaten begrenzt vor, verschiebt den Idle-Exit bei laufenden Loads und beendet sich
graceful mit atomarer MRU-Persistierung. Der bestehende `--mcp-server`-Stdio-Pfad bleibt
bis zum späteren ThinClient-Step unverändert.

## Umfang

- B.3 DaemonHost-Lifecycle: interner `--daemon-start`-Pfad, Named-Pipe-Akzeptanz,
  `hello`/`welcome`-Verarbeitung aus step-009, MCP-Session je Verbindung gegen die
  gemeinsame Registry, Clientzählung, aktive Load-/Warmup-Zählung, graceful Shutdown
  und kein Parent-Reaper.
- B.3 Idle-Exit: konfigurierbarer `--mcp-daemon-idle-exit-minutes`-Wert, Default 10,
  injizierbare `TimeProvider`-Clock für die Zustandslogik; Exit erst bei null Clients,
  abgelaufener Idle-Zeit und null aktiven Loads/Warmups; Dispose aller Registry-Keys
  genau einmal.
- B.3 MRU-Warmup: maximal zwei parallele Warmup-Loads, derselbe Resolve-/Dedupe-Pfad
  wie interaktive Registry-Leases, interaktive Calls warten nicht hinter der Warmup-
  Begrenzung; Warmup-Fehler blockieren den Host nicht.
- B.4 `MruStateStore`: produktiver Pfad
  `%LOCALAPPDATA%\RalfHuesing\AiNetLinter\daemon-state.json`, Einträge
  `{rootPath,lastUsedUtc}`, Begrenzung auf `maxProjects`, tolerantes Lesen korrupt-/leer-
  Dateien, Entfernen nicht mehr initialisierter Roots, debounced Schreiben frühestens
  etwa 30 Sekunden nach dem letzten Touch und Schreiben beim Shutdown, atomar über
  Temp-Datei plus `File.Move` mit Überschreiben.
- CLI-/Doku-Vertrag: `--daemon-start` wird in `--help` als `[internal]` sichtbar,
  Doppelstart endet mit einer sachlichen stderr-Diagnose und Exit-Code ungleich null;
  `--mcp-daemon-idle-exit-minutes` wird invariant als positiver Decimal-Wert geparst
  und bei ungültigem Wert hart abgewiesen.

## Nicht-Umfang

- Kein `ThinClientProxy`/`ThinClientLauncher`, kein Connect-or-Start, kein detached
  Spawn aus `--mcp-server`, kein Retry nach Pipe-Abbruch, kein Ping-/Kill-/Restart-
  Verhalten und keine `AINETLINTER_NO_DAEMON`-Escape-Verdrahtung.
- `--mcp-server` bleibt der bisherige direkte Stdio-/MCP-Pfad; keine Migration von
  `.mcp.json`, Hermes oder anderen Clientregistrierungen.
- Kein Health-/Observability-Ausbau um Modus, PID, Uptime, Connection-ID oder
  Daemon-Version; die Host-Semantik wird zunächst über die bestehenden Verträge und
  gezielte Tests abgenommen.
- Keine Änderung an MCP-Toolanzahl, `projectRoot`-Vertrag, Registry-Lease-Semantik,
  Handshake-Versionierung oder Pipe-Framing aus step-009.
- Keine Windows-Service-/Autostart-/Taskplaner-Unterstützung, keine neuen NuGet-
  Abhängigkeiten und kein Umbau der Batch-Pipeline.
- Kein Stress-Test und kein Epic-Drift-Audit; der Drift-Audit erfolgt genau einmal
  vor dem EPIC-B-Abschluss.

## Akzeptanzkriterien

- [ ] `--daemon-start` startet genau einen Host mit einer gemeinsamen Registry; jede
      akzeptierte Verbindung erhält eine eigene MCP-Session gegen denselben Registry-
      Bestand, und eine doppelte Pipe-Bindung endet deterministisch mit stderr-Fehler
      und Exit-Code ungleich null.
- [ ] Der Host verwendet den step-009-Handshake vor dem MCP-Durchsatz, akzeptiert
      parallele Verbindungen und zählt Clients verbindungsbezogen; ein Disconnect
      beendet nur die zugehörige Session/Cancellation.
- [ ] Der Daemon verwendet keinen `ParentProcessWatchdog`; sein Prozess endet nur nach
      dem definierten Idle-Exit-/Shutdown-Vertrag oder expliziter Host-Cancellation.
- [ ] Idle-Exit erfolgt nur bei null verbundenen Clients, abgelaufener konfigurierter
      Idle-Zeit und null aktiven Loads/Warmups; laufende Loads/Warmups verschieben den
      Exit und werden nie unter halbfertigem Zustand disposed.
- [ ] Shutdown disposed die Registry und alle residenten Keys geordnet genau einmal
      und persistiert den MRU-State auch bei einem leeren oder korrupt eingelesenen
      Vorgängerzustand.
- [ ] MRU-Warmup lädt höchstens zwei Kandidaten gleichzeitig, nutzt den bestehenden
      Registry-/Dedupe-Pfad, blockiert keinen interaktiven Load, verwirft tote Roots
      einschließlich Entfernung aus dem persistierten State und lässt einzelne
      Warmup-Fehler den Host weiterbetreiben.
- [ ] MRU-Lesen ignoriert leere/korrupt serialisierte Dateien ohne Host-Fehler;
      Schreiben ist debounced und atomar, und ein Schreibfehler bleibt eine geloggte,
      nicht daemon-blockierende State-Hilfsfunktion.
- [ ] Der aktuelle `--mcp-server`-Stdio-/Toolvertrag und EPIC-A-Verhalten bleiben
      unverändert; ThinClient- und Health-Wiring sind nicht vorweggenommen.

## Konkrete Änderungen

### `src/AiNetLinter/Mcp/Daemon/DaemonHost.cs` und ggf. schlanke Host-Helfer

- **Was:** Host-Lifecycle als kleine, direkt instanziierbare Komposition aus
  `DaemonPipeTransport`, `ProjectRegistry`, `McpServerOptionsFactory` und einer
  testbaren Host-Clock. Der Accept-/Session-Pfad führt den bestätigten Handshake aus,
  erstellt für jede Verbindung den vorhandenen MCP-SDK-Server auf dem verbundenen
  Pipe-Stream und entsorgt Session, Connection und Lease-Zustand unabhängig voneinander.
  Der Host besitzt keine eigene Analyse- oder Toolimplementierung.
- **Warum:** B.3 verlangt einen geteilten Prozess, ohne den bestehenden Toolvertrag oder
  die Registry-Wahrheit zu duplizieren. Die Session-Grenze muss pro Verbindung getrennt
  bleiben, während die Registry processweit geteilt wird.

### `src/AiNetLinter/Mcp/Daemon/MruStateStore.cs`

- **Was:** Toleranter, atomarer MRU-Store mit injizierbarem Dateipfad/Clock für Tests
  und produktivem `%LOCALAPPDATA%`-Default. Lesen validiert Root-/Zeitfelder und
  behandelt fehlende, leere oder korrupt serialisierte Dateien als „kein Warmup“;
  Schreiben begrenzt, dedupliziert und sortiert Kandidaten nach `lastUsedUtc`, nutzt
  Temp-Datei plus `File.Move`-Overwrite und meldet IO-Fehler über den vorgesehenen
  Diagnoseweg ohne den Host zu stoppen. Touches werden über einen Timer debounced.
- **Warum:** B.4 macht MRU zu einem verzichtbaren Warmstart-Hinweis und verlangt
  Prozessabsturz-/Korruptionssicherheit ohne parallele per-Touch-Prozesse.

### `src/AiNetLinter/Mcp/Daemon/DaemonWarmup.cs` oder Host-interne Warmup-Komponente

- **Was:** Warmup-Orchestrierung mit gebundener Parallelität von höchstens zwei,
  Ausschluss bereits aktiver/interaktiv angeforderter Keys und Removal verworfener
  Roots aus dem MRU-Store. Warmup-Aufgaben werden beim Shutdown beobachtbar und
  verhindern den Dispose bis alle Tasks beendet oder abgebrochen sind.
- **Warum:** B.3 fordert denselben Dedupe-Pfad wie interaktive Calls, keine Queue-
  Priorisierung vor dem Nutzer-Call und keinen halbfertigen Dispose.

### `src/AiNetLinter/Cli/CliOptionFactory.cs`, `CliOptions.cs`, `LinterArgs.cs`,
`src/AiNetLinter/Cli/CliCommandBuilder.cs`, `src/AiNetLinter/Program.cs`

- **Was:** Internes Boolean-Argument `--daemon-start` und die Decimal-Option
  `--mcp-daemon-idle-exit-minutes` ergänzen, in `LinterArgs` materialisieren und
  invariant/positiv validieren. `Program` routet ausschließlich den expliziten
  Daemon-Start zum Host; `--mcp-server` bleibt auf dem bisherigen direkten Pfad.
  Die Help-Beschreibung markiert `--daemon-start` mit `[internal]`.
- **Warum:** B.5 verlangt einen kontrolliert startbaren Host und B.3 einen testbar
  kurzen Idle-TTL; ein impliziter oder cwd-abhängiger Start würde den späteren
  Connect-or-Start-Vertrag vorwegnehmen.

### `src/AiNetLinter.FastTests/Mcp/Daemon/`

- **Was:** In-proc-Contracts für MRU-Roundtrip/korrupt/leer/atomar/debounced, tote
  Roots, Warmup-Konkurrenz und interaktiven Vorrang, Idle-Exit mit `TimeProvider`,
  Load-/Shutdown-Races, Dispose-Genau-einmal, Clientzählung sowie CLI-Parsing und
  den No-Parent-Reaper-Hostpfad.
- **Warum:** Die Zustands- und Timingregeln sollen deterministisch ohne unnötige
  Prozesslast geprüft werden; echte IPC bleibt auf wenige Integrationsfälle begrenzt.

### `src/AiNetLinter.IntegrationTests/Mcp/Daemon/`

- **Was:** Sparsame Zwei-Prozess-Contracts gegen die echte EXE: `--daemon-start`
  bindet und bedient zwei parallele Pipe-/MCP-Verbindungen mit geteilter Registry;
  ein kurzer Idle-TTL beendet den Host nach Disconnect und schreibt MRU; ein
  Neustart liest den State und wärmt einen gültigen Root; Doppelstart liefert den
  definierten stderr-/Exit-Code-Fehler. Test-State-Pfade bleiben über einen
  injizierbaren Host-/Test-Parameter außerhalb des produktiven Default-Pfads isoliert.
- **Warum:** B.6 verlangt echte Host-/Prozessgrenzen, aber keine lastintensive
  Parallel-Orchestrierung. Der ThinClient wird dabei nicht simuliert oder vorweg
  implementiert; die Testverbindung nutzt die geprüfte Pipe-/Handshake-Schicht.

### Produktdokumentation und Synchronisation

- **Was:** `Docs/agent-api.md` beschreibt den tatsächlich aktiven internen
  DaemonHost-/Idle-/MRU-Vertrag einschließlich des noch unveränderten
  `--mcp-server`-Stdio-Pfads. `Docs/integration.md` dokumentiert Hoststart,
  Doppelstartfehler und die klare Grenze zum späteren ThinClient. `Docs/configuration.md`
  beschreibt `--daemon-start` als internes Help-Argument und
  `--mcp-daemon-idle-exit-minutes` inklusive Default/Decimal-Regel. `README.md` und
  `Docs/ROADMAP.md` erhalten nur sachliche Hinweise auf den implementierten
  Zwischenstand; `../90_bewusst-nicht-umsetzen/Konzept.md` erhält den EPIC-B-
  Wiederöffnungsvermerk in §C.5, sofern dieser im aktuellen Pfad noch fehlt.
- **Sync-Pflicht:** CLI-/Konfigurationsänderungen werden gegen Code und Parser
  verifiziert; gemäß Projektregel werden `Docs/ROADMAP.md`,
  `Docs/configuration.md`, `README.md` und `rules.json` berücksichtigt, ohne
  fachfremde Regeldefinitionen zu erfinden. Eine Änderung an Regel-/CLI-Sync-
  Texten wird mit `--sync-agent-rules-only` in
  `.agents/rules/AiNetLinter.mdc` synchronisiert. Keine Clientregistrierung wird
  vor dem ThinClient-Step migriert.

## Tests

- [ ] `MruStateStoreTests`: roundtrip, maxProjects/Dedupe/Sortierung, fehlende/leere/
      korrupt JSON-Datei, tote Roots, atomarer Replace, debounced Touch und tolerierter
      Schreibfehler.
- [ ] `DaemonWarmupTests`: höchstens zwei parallele Warmup-Loads, interaktiver Load
      wartet nicht auf Warmup, Dedupe mit Registry-Lease, Warmup-Fehler isoliert,
      Shutdown wartet/aborted geordnet.
- [ ] `DaemonHostLifecycleTests`: Clientzählung, Idle-Clock, aktive Loads/Warmups
      verschieben Exit, graceful Shutdown/Registry-Dispose genau einmal, kein
      Parent-Reaper und Handshake vor Sessionstart.
- [ ] `ProgramParsingTests`/CLI-Contracts: `--daemon-start`, Help-Markierung,
      invariant-positive Decimal-Werte für `--mcp-daemon-idle-exit-minutes` und harte
      Fehler für ungültige Werte; bestehender `--mcp-server`-Pfad bleibt grün.
- [ ] Integration: zwei echte Host-Verbindungen teilen eine Registry und einen warmen
      Key; Disconnect isoliert die Session; Idle-Exit schreibt MRU; Neustart wärmt einen
      gültigen Root; Doppelstart liefert stderr + Exit-Code ungleich null.
- [ ] Während der Entwicklung nur gezielte Unit-/Component-Slices; keine neuen
      `Category=Stress`-Tests und kein Stress-Lauf.
- [ ] Vor Step-Abschluss genau einmal durch den Coder: `dotnet build`,
      `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
      `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
      Der Kritiker wiederholt den vollständigen Nicht-Stress-Stack nicht und prüft
      `step-result.md` plus Stichproben.

## MCP-Gates

- [ ] Vor Änderungen MCP-first: `get_feature_context`/`find_symbol` für
      `Program`, CLI-Optionen, `McpServerCommand`, `McpServerOptionsFactory`,
      `ProjectRegistry` und die step-009-Daemon-Typen; `find_references`/`get_impact`
      für die betroffenen Routing- und Lifetime-Symbole.
- [ ] Nach Änderungen `get_violations` und `safeguard` mindestens für
      `src/AiNetLinter/Mcp/Daemon`, `src/AiNetLinter/Cli` und die betroffenen
      Host-/Command-Dateien; keine Warnungen oder Architekturverletzungen.
- [ ] `metrics_lookup` für alle neuen/geänderten Produktions-Typen und langen
      Kompositionspunkte; bei Überschreitung in schlanke Options-/Helper-Typen teilen.
- [ ] `get_test_context`/semantische Impact-Prüfung bestätigt, dass CLI-/Host-Tests
      den alten Stdio-Pfad und die Registry-Verträge weiterhin abdecken.
- [ ] Bei MCP-Server-Neustart zuerst `get_server_health` abfragen und erst nach
      `Loaded` weitere projektgebundene MCP-Gates ausführen.

## Definition of Done

- [ ] Umfang und Akzeptanzkriterien dieses Plans sind umgesetzt; ThinClient,
      Health-Wiring und Clientregistrierungs-Migration bleiben unberührt.
- [ ] `--daemon-start` ist als interner Help-Pfad aktiv, startet/stoppt den Host
      deterministisch und verwendet keinen Parent-Reaper; Doppelstart ist sauber.
- [ ] Shared Registry, MCP-Session je Verbindung, Handshake-Reihenfolge,
      verbindungsbezogene Cancellation, Idle-Exit und loadgeschütztes Shutdown-
      Verhalten sind durch Unit- und sparsame Integration-Contracts abgesichert.
- [ ] MRU-State ist tolerant lesbar, atomar/debounced schreibbar, auf
      `maxProjects` begrenzt und entfernt tote Roots; Warmup bleibt auf zwei
      parallele Loads begrenzt und blockiert interaktive Calls nicht.
- [ ] Betroffene Produktdoku ist sachlich aktualisiert; kein noch nicht verdrahteter
      ThinClient wird als aktiv beschrieben; erforderlicher CLI-/Regel-Sync ist
      nachvollzogen oder begründet nicht nötig.
- [ ] MCP-Gates sind grün; kein unaufgeforderter Tech-Debt-Fix und kein Drift-Audit
      in diesem Step.
- [ ] Genau ein vollständiger Nicht-Stress-Teststack wurde vom Coder vor dem
      Step-Abschluss ausgeführt; der Kritiker wiederholt ihn nicht; Stress wurde nicht
      ausgeführt.
- [x] `step-010/step-result.md` ist geschrieben; der Step ist durch
      `step-011` und `step-012` vollständig korrigiert und gemäß Workflow `done`.
- [ ] Der spätere Coder-/Kritiker-Prompt übernimmt unverändert die Nutzer-Overrides:
      `max_fix_rounds_per_step=6`, `soft_step_checkin_interval=80`,
      `max_batch_items=16`, `max_batch_diff_lines=80`.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#agent-resilience` — neue
  Host-/MRU-/Async-Logik bleibt `sealed`, nullable, klein, nicht-blockierend und
  behandelt Shutdown-Cancellation sichtbar.
- `.agents/rules/AiNetLinter.mdc#architecture` — Namespace-/Pfad-Mapping,
  keine Reflection-/Plugin-/dynamische Hostinstanziierung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1.Grundprinzipien` — vorhandene
  Registry-/MCP-/Pipe-Strukturen wiederverwenden, BCL-only, keine neue DI-/RPC-
  Abstraktion und MCP-first bei C#-Semantik.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3. Windows-Umgebung & Tool-Regeln`
  und `#4. Updates & Tests` — Windows-Named-Pipes, `TestTempDirectory`, xUnit-v3,
  gezielte Iteration, genau ein vollständiger Nicht-Stress-Gate-Lauf durch den Coder.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5. Qualitätsdrift-Prävention` —
  Zero-Warning, keine Symptom-Fixes, MCP-Quality-Gates, kein vorgezogener
  Drift-Audit und keine Task-Artefakt-Referenzen im Produktionscode.

## Bekannte Ausnahmen

- Echte Zwei-Prozess-Tests bleiben auf wenige Host-/Lifecycle-Fälle begrenzt;
  Versions-Mismatch bleibt wie in step-009 ein In-Proc-Vertrag und wird nicht erneut
  als Prozessszenario implementiert.
- Der produktive MRU-Pfad ist fest durch B.4 vorgegeben; Tests erhalten nur einen
  injizierbaren Pfad-/Clock-Seam, damit sie nicht in `%LOCALAPPDATA%` oder OS-Temp
  schreiben.
- TD-002, TD-004 und TD-005 bleiben offene Konzept-/Architekturentscheidungen und
  werden nicht als `auto_fixable`-Batch-Items angehängt.

## Notes

- Der Host nutzt die Pipe-/Handshake-Verträge aus step-009 und interpretiert nach
  erfolgreichem Handshake MCP-Nutzdaten über das bestehende SDK; kein eigener
  JSON-RPC-Server und keine zweite Framing-Implementierung.
- `projectRoot` bleibt ausschließlich Call-/Registry-Vertrag; MRU darf Roots nur als
  Warmstart-Hinweis verwenden und niemals Definition, Rules-Pfad oder Registry-
  Wahrheit ersetzen.
- Für jeden späteren Coder-/Kritiker-Aufruf gelten die taskweiten Overrides aus
  `task-state.md`; Review-Forderungen, die der inzwischen erreichten Architektur
  widersprechen, werden als Blocker bzw. Konzept-Entscheidung dokumentiert, nicht
  durch spekulatives Zurückbauen beantwortet.
