---
status: open
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 009
corrects: null
title: "Transport-/Handshake-Grundlage für den Daemon"
epic: EPIC-B
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-24T01:34:06+02:00
related_to:
  - step-008/step-result.md
  - step-008/step-review.md
---

# Step 009: Transport-/Handshake-Grundlage für den Daemon

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** `EPIC-B` aus `roadmap.md` — die durch EPIC-A abgeschlossene
  Projektregistry soll in einem langlebigen, geteilten Prozess erreichbar
  werden, ohne den bestehenden MCP-Toolvertrag zu ändern.
- **Konzept-Referenz:** `konzept.md` B.1–B.2 sowie der Transport-Layer in
  B.5. Dieser Step behandelt ausschließlich die wiederverwendbare
  Pipe-/Handshake-Grundlage und die dazugehörigen B.6-In-Proc-Contracts.

## Aktueller Projektzustand (JIT-Kontext)

- `Program.Main` routet `--mcp-server` direkt zu
  `McpServerCommand.RunAsync`. Dieser erstellt bereits eine
  `ProjectRegistry`, baut über `McpServerOptionsFactory` den bestehenden
  Tool-/Resource-Vertrag und startet den SDK-`StdioServerTransport`.
- Die Registry- und `McpCodeGraphServer`-Strukturen sind daher ein
  vorhandener geteilter Kern und werden nicht dupliziert oder in diesem
  Step umgebaut. Die aktuelle MCP-Verbindung bleibt unverändert stdio.
- `McpServerLifetime` kapselt heute nur Cancellation plus den
  `ParentProcessWatchdog`; der Daemon darf diesen Reaper später bewusst
  nicht verwenden. Ein `Mcp/Daemon/`-Transport, eine Pipe-Identität und
  ein Pipe-Level-Handshake existieren noch nicht.
- `McpRawWireTestHarness`, `McpServerCommandJsonRpcFramingTests` und
  `McpHandshakeToolRegistrationTests` prüfen bereits stdio-/MCP-Framing
  und SDK-Handshake. Sie sind Baseline und Wiederverwendungshinweis,
  ersetzen aber keinen separaten Pipe-Level-Vertrag.
- Der MCP-Impact zeigt `McpServerCommand.RunAsync` als einzigen
  Produktionsaufrufer und `McpServerOptionsFactory.Create` als bestehend
  testverankerte Optionsfabrik. Die neue Schicht wird deshalb zunächst
  unabhängig und in-proc testbar angelegt, ohne CLI-Routing oder SDK-Pump
  vorwegzunehmen.
- TD-002 und TD-005 (nicht zugeordnete `Console.Error`-Diagnosen) sowie
  TD-004 (Registry-Soft-Cap) bleiben dokumentierte, nicht automatisch zu
  lösende Tech-Debts. Dieser Step führt keine neue Diagnosekanal- oder
  Kapazitätsentscheidung ein.

## Intention

Nach diesem Step gibt es einen kleinen, deterministischen Transportvertrag
unter `Mcp/Daemon/`, auf dem DaemonHost und ThinClient später aufsetzen
können: benutzergebundener Named-Pipe-Endpunkt, newline-delimited JSON,
Pipe-Level-Handshake und per-Verbindung-Cancellation. Die
Versions-/Shutdown-Entscheidung wird als in-proc testbare Zustandslogik
festgelegt, damit ein Versions-Mismatch bei null anderen Verbindungen
kontrolliert beendet wird und bei konkurrierenden Verbindungen als
`VERSION_CONFLICT` sichtbar bleibt. Der bestehende `--mcp-server`-Pfad und
der MCP-SDK-Handshake bleiben in diesem Step unverändert.

## Akzeptanzkriterien

- [ ] Ein Pipe-Endpunkt wird ausschließlich als
  `ainetlinter.analyzer.v1.<username>` für den aktuellen Benutzer erzeugt;
  seine ACL lässt nur den aktuellen Benutzer zu.
- [ ] Jede Pipe-Nachricht ist genau ein newline-delimited JSON-Objekt;
  ungültige oder mehrzeilige Frames werden deterministisch abgewiesen und
  MCP-/JSON-RPC-Nutzdaten werden nach dem Handshake bytegenau durchgereicht.
- [ ] `hello`/`welcome` transportieren Protokollversion, Daemon-/EXE-Version,
  PID und effektive Daemon-Konfiguration. Protokollversionen außerhalb des
  unterstützten Vertrags werden abgewiesen.
- [ ] Bei abweichender EXE-Version wird bei null weiteren Verbindungen genau
  ein Pipe-Level-`shutdown`-Entscheid möglich; bei weiteren Verbindungen
  entsteht stattdessen `VERSION_CONFLICT` ohne Ping-Pong-Neustart.
- [ ] Eine erkannte Konfigurationsdivergenz ist strukturiert und genau einmal
  als Warnereignis auswertbar; sie verändert weder `projectRoot` noch die
  Registry-Semantik.
- [ ] Der Disconnect einer Verbindung cancelt nur deren in-flight Token; eine
  unabhängige Verbindung und ihr gemeinsamer Warm-State bleiben aktiv.
- [ ] Der bestehende `--mcp-server`-Stdio-/MCP-Contract bleibt unverändert;
  DaemonHost, ThinClient, MRU, Idle-Exit und Health-Wiring gehören nicht zu
  diesem Step.

## Konkrete Änderungen

### `src/AiNetLinter/Mcp/Daemon/DaemonProtocol.cs`

- **Was:** Neue interne Protokolltypen und Konstanten für die
  Pipe-Grenze: Protokollversion `1`, Pipe-Name
  `ainetlinter.analyzer.v1.<username>`, `hello`, `welcome`, `shutdown`,
  `VERSION_CONFLICT` sowie die effektive Daemon-Konfiguration mit
  `maxProjects`, `idleExitMinutes` und Log-Ziel. Die JSON-Form bleibt
  newline-delimited; `projectRoot` und MCP-Tool-Payloads gehören nicht in
  den Pipe-Handshake.
- **Warum:** B.1/B.2 benötigen einen stabilen, vom SDK entkoppelten
  Transportvertrag. Die Records/Value-Objects sollen von DaemonHost und
  ThinClient gemeinsam verwendet werden, statt parallele String-/JSON-
  Konstruktionen zu erzeugen.

### `src/AiNetLinter/Mcp/Daemon/DaemonHandshake.cs`

- **Was:** Reine Handshake-State-Machine für `hello`/`welcome` und
  `shutdown`: Protokollversion vergleichen, Versionsabweichung des
  Executables erkennen, bei `activeConnectionCount == 0` einen kontrollierten
  Shutdown-Entscheid ausgeben und sonst ausschließlich den deterministischen
  Fehler `VERSION_CONFLICT` liefern. Ein injizierbarer
  Versions-/PID-Provider sowie ein strukturierter
  Konfigurations-Divergenz-Entscheid werden vorgesehen; die spätere
  stderr-/Observability-Ausgabe bleibt Aufgabe der aufrufenden Schicht.
- **Warum:** B.2 verlangt Anti-Ping-Pong und sichtbare Konfigurations-
  divergences, ohne den Handshake an den späteren Prozess- oder
  Observability-Lifecycle zu koppeln.

### `src/AiNetLinter/Mcp/Daemon/DaemonPipeTransport.cs`

- **Was:** Named-Pipe-Endpoint-/Stream-Fabrik mit aktuellem Benutzer im
  Namen und ACL auf den aktuellen Benutzer sowie eine asynchrone
  newline-delimited JSON-Verbindung. Der Lese-/Schreibpfad verwendet pro
  Verbindung ein eigenes Cancellation-Token; Disconnect beendet nur die
  in-flight Arbeit dieser Verbindung. Nach dem Handshake werden
  MCP-/JSON-RPC-Bytes opak weitergereicht, ohne SDK-Interpretation oder
  stdout-Ausgaben.
- **Warum:** Damit sind die B.2-Transportgrenzen und die spätere
  Wiederverwendung durch DaemonHost und ThinClient an einer Stelle
  festgelegt. Der Transport erhält keinen Registry-Zustand und kann daher
  beim Disconnect keine anderen warmen Keys beeinflussen.

### `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHandshakeContractTests.cs`

- **Was:** Neue xUnit-v3-Unit-Contracts für kompatible und unbekannte
  Protokollversionen, Versionsgleichheit, Versions-Mismatch ohne weitere
  Verbindungen (genau ein kontrollierter Shutdown-Entscheid),
  Versions-Mismatch mit weiteren Verbindungen (`VERSION_CONFLICT`),
  `welcome`-Felder sowie Konfigurationsdivergenz mit genau einem
  strukturierten Warnereignis.
- **Warum:** Die kritische Versionslogik wird schnell, deterministisch und
  ohne EXE-/Prozess-Orchestrierung abgesichert; der Konzeptumfang verlangt
  ausdrücklich keinen Zwei-Prozess-Test für den Versions-Mismatch.

### `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonPipeTransportContractTests.cs`

- **Was:** In-proc-Contracts über testbare Streams/Pipe-Doubles für
  genau-ein-JSON-Objekt-pro-Zeile, Roundtrip und Ablehnung ungültiger
  Framingdaten, deterministische Pipe-Namens-/Benutzerbindung,
  aktuelle-Benutzer-ACL sowie bytegenau opake Weiterleitung. Ein
  Disconnect cancelt nur den eigenen Verbindungs-Token; eine zweite
  Verbindung und ein gemeinsam beobachteter Registry-/Key-Marker bleiben
  unbeeinträchtigt.
- **Warum:** B.2 fordert newline-delimited JSON, per-Verbindung-
  Cancellation und Warmth-Erhalt. Die Contracts bleiben in-proc und
  vermeiden einen neuen lastintensiven Stress- oder Zwei-Prozess-Test.

### Dokumentation und Sync innerhalb dieses fachlichen Steps

- **Was:** `Docs/agent-api.md` erhält den sachlichen Pipe-Level-
  Transport-/Handshake-Vertrag einschließlich Version, Frame-Grenze,
  Shutdown-/`VERSION_CONFLICT`-Entscheidung und effektiver Konfiguration.
  `Docs/integration.md` hält die Transportgrenze und den aktuellen Stand
  fest: `--mcp-server` bleibt bis zur späteren Wiring-Änderung der
  bestehende stdio-/In-Proc-Pfad; kein Daemon-Modus wird vorzeitig als
  aktiv dokumentiert.
- **Warum:** B.x legt Transport-/Lifecycle-Dokumentation in den fachlich
  berührten Steps fest; zugleich verbietet die Doku-Regel Aussagen über
  noch nicht verdrahtete Features.
- **Sync-Pflicht:** Da dieser Step keine CLI-Option, kein Config-Feld und
  keine Linter-Regel ändert, sind `Docs/configuration.md`, `README.md`,
  `Docs/ROADMAP.md`, `rules.json` und die generierte
  `.agents/rules/AiNetLinter.mdc` in diesem Step nicht künstlich
  nachzuziehen. Sobald `--daemon-start`,
  `--mcp-daemon-idle-exit-minutes`, der Debug-Escape oder die aktive
  ThinClient-Verdrahtung umgesetzt werden, müssen diese Ziele im dann
  fachlich berührten Step aktualisiert und die mdc-Datei synchronisiert
  werden.

## Tests

- [ ] `DaemonHandshakeContractTests`: kompatible/ungültige
  Protokollversion, Version-Mismatch mit 0 bzw. mehr als 0 weiteren
  Verbindungen, `welcome`-Vertrag, Konfigurationsdivergenz und
  Anti-Ping-Pong.
- [ ] `DaemonPipeTransportContractTests`: NDJSON-Framing, Pipe-Name und
  Benutzer-ACL, opaker Byte-Transport und isolierte
  Disconnect-Cancellation.
- [ ] Während der Implementierung nur gezielte Unit-Slices der neuen
  `Mcp.Daemon`-Tests; keine neuen Tests in `Category=Stress` und kein
  echter Zwei-Prozess-Daemon in diesem Step.
- [ ] Vor Step-Abschluss genau ein vollständiger Nicht-Stress-Lauf je
  Zielprojekt: `dotnet test src/AiNetLinter.FastTests --filter
  Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests
  --filter Category!=Stress`; zusätzlich `dotnet build`.
- [ ] Vor dem Commit MCP-Quality-Gates auf dem geänderten Scope:
  `get_violations`, `safeguard` und `metrics_lookup`; Kritiker verwendet
  `step-result.md` plus Stichproben und wiederholt den Vollstack nicht.

## Definition of Done

- [ ] Alle oben genannten Transport-/Handshake-Verträge sind umgesetzt;
  `McpServerCommand`, `Program`, Registry-Wiring und bestehender
  stdio-/MCP-Toolvertrag bleiben unverändert.
- [ ] Named-Pipe-Name und ACL sind auf den aktuellen Benutzer begrenzt;
  Framing ist newline-delimited JSON und der Handshake bleibt vom MCP-SDK
  getrennt.
- [ ] Disconnect-Cancellation ist pro Verbindung isoliert; der Transport
  hält keine Registry-/Key-Wahrheit und lässt andere Verbindungen warm.
- [ ] Alle neuen In-proc-Contracts sind grün und der bestehende EPIC-A-
  Contract bleibt durch den vollständigen Nicht-Stress-Lauf grün.
- [ ] `Docs/agent-api.md` und `Docs/integration.md` sind nur um den
  tatsächlich implementierten Stand ergänzt; keine vorzeitige
  Daemon-Aktivbehauptung in README oder Konfigurationsdoku.
- [ ] `dotnet build` sowie beide Nicht-Stress-Test-Suites sind grün;
  Stress-Tests wurden nicht ausgeführt.
- [ ] MCP-Quality-Gates sind grün; kein unaufgeforderter Tech-Debt-Fix
  und kein Epic-Drift-Audit in diesem Step.
- [ ] Commit auf aktuellem Branch mit deutschem Conventional-Commit,
  `step-009/step-result.md` geschrieben und `status` in diesem Plan nach
  erfolgreicher Umsetzung auf `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#agent-resilience` —
  `sealed`, nullable, kleine async-Methoden, kein blockierender
  Task-Zugriff und kein stilles Catching für neue Transportlogik.
- `.agents/rules/AiNetLinter.mdc#architecture` — Namespace-/Pfad-Mapping
  für `AiNetLinter.Mcp.Daemon` und keine dynamische/reflective
  Transportinstanziierung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1.Grundprinzipien` —
  monolithische direkte Instanziierung, Records für unveränderliche
  Verträge und MCP-first-Dogfooding.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3.Windows-Umgebung & Tool-Regeln`
  und `#4.Updates & Tests` — Windows-kompatible Named Pipes, gezielte
  Unit-Iteration, TestTempDirectory-Testgrenzen, keine erzwungene
  Serialisierung und MCP-Testinfrastruktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5.Qualitätsdrift-Prävention` —
  Zero-Warning, keine Symptom-Fixes, MCP-Gates und kein vorgezogener
  Epic-Drift-Audit.

## Bekannte Ausnahmen

- Der Versions-Mismatch wird bewusst nur in-proc über den injizierbaren
  Provider geprüft; die Konzeptvorgabe schließt dafür einen
  Zwei-Prozess-Integrationstest aus.
- Die neue Schicht wird in diesem Step noch nicht in
  `--mcp-server`, `--daemon-start`, ThinClient, Idle-Exit, MRU oder Health
  verdrahtet. Ein funktionierender Daemon-Prozess ist daher keine
  Akzeptanzbedingung dieses Plans.
- TD-002, TD-004 und TD-005 bleiben offen; ihre Behebung erfordert eine
  spätere Architektur-/Vertragsentscheidung und wird nicht als
  auto-fixable Batch-Item angehängt.

## Notes

- Bestehende `McpRawWireTestHarness`-/Framing-Helpers dürfen als
  Testmuster gelesen werden, aber der neue Pipe-Level-Codec darf nicht
  den MCP-SDK-Handshake oder dessen Tool-Interpretation duplizieren.
- Der Transport muss keine `projectRoot`-Semantik kennen. Diese bleibt im
  bestehenden Registry-/Toolvertrag; `welcome`-Konfiguration ist
  daemonweit und darf per-call-Verträge nicht überschreiben.
- Der einmalige `drift-audit` für EPIC-B erfolgt erst vor dem Epic-
  Abschluss gemäß Roadmap und Nutzervorgabe, nicht in step-009.
