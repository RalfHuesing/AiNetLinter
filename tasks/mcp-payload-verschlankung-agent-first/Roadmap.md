# Roadmap – MCP-Payload-Verschlankung (agent-first)

Stand: 2026-09-12
Ausgangscommit: `84b8f07a7375e2f6c49e5995dce0870a59de5af1`

Diese Roadmap wird nach jedem abgeschlossenen Schritt im selben, geprüften
Commit fortgeschrieben. Die Umsetzung erfolgt strikt seriell: pro Schritt ein
frischer Subagent, anschließend Prüfung und Commit.

- [X] Slice 01 – Globalen Content-only-Vertrag rot absichern
- [X] Slice 02 – Typisierte interne Ownership herstellen
- [X] Slice 03 – Einen Agentenrenderer und das Contentbudget etablieren
- [X] Slice 04 – `structuredContent` restlos hart entfernen
- [X] Slice 05 – Integrationstests und Dokumentation auf den Endzustand schneiden
- [X] Slice 06 – Agentische Verifikation und Release-Gate
- [X] Abschlussaudit – Scope prüfen und alle Findings proaktiv beheben

## Durchführungsprotokoll

### Slice 01 – Globalen Content-only-Vertrag rot absichern (2026-09-12)

- `McpContentOnlyContractTests` deckt die gemeinsamen Erfolgs-, Empty-,
  Loading-, Fehler- und Response-Budget-Pfade ab und sammelt alle Verstöße.
  Der fokussierte FastTest ist erwartungsgemäß rot: nur `error` und
  `response-budget` setzen weiterhin `StructuredContent`; alle fünf Pfade
  liefern bereits genau einen nichtleeren Textblock.
- `McpContentOnlyProductionArchitectureGuardTests` durchsucht ausschließlich
  `src/AiNetLinter` und berichtet jede Lese-/Schreibreferenz sowie jedes
  öffentliche Vertragsliteral zu `StructuredContent`/`structuredContent`.
  Der fokussierte Integrationstest ist erwartungsgemäß rot und zeigt den
  verbliebenen Dual-Output in gemeinsamen Ergebnissen, Navigation,
  Budgetierung, Composites, Registrierungen und Toolimplementierungen.
- `McpContentOnlyRawWireContractTests` prüft gegen einen frischen MCP-Host
  repräsentative Erfolg-, Empty-, Truncated- und InvalidArgument-Responses.
  Der Test ist erwartungsgemäß ausschließlich wegen des vorhandenen
  `structuredContent`-Feldes rot; die Textblock-Invariante ist in allen vier
  Fällen erfüllt. Die Ausgangsgrößen und die zu bewahrenden Agenteninformationen
  stehen in `ResponseMatrix.md`.
- Rot-Test-Evidenz:
  - `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~McpContentOnlyContractTests --logger "trx;LogFileName=Slice01FastRed.trx"` – 1 erwartet fehlgeschlagener Test; ausschließlich zwei `StructuredContent`-Verstöße.
  - `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpContentOnlyRawWireContractTests --logger "trx;LogFileName=Slice01RawWireRed.trx"` – 1 erwartet fehlgeschlagener Test; ausschließlich vier Raw-Wire-`structuredContent`-Verstöße.
  - `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpContentOnlyProductionArchitectureGuardTests --logger "trx;LogFileName=Slice01ArchitectureRed.trx"` – 1 erwartet fehlgeschlagener Test; ausschließlich Produktionsreferenzen bzw. Vertragsliterale zum entfernten Kanal.
- Bestehende Verträge und Build bleiben grün:
  - `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~McpToolResultsTests --logger "trx;LogFileName=Slice01ExistingFastGreen.trx"` – 31 bestanden.
  - `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandJsonRpcFramingTests.HandshakeAndSingleToolCall_AllStdoutLinesAreValidJsonRpcFrames --logger "trx;LogFileName=Slice01ExistingRawWireGreen.trx"` – 1 bestanden.
  - `dotnet build` – 0 Warnungen, 0 Fehler.
- MCP-Qualitätsbefunde: `safeguard` für den neuen FastTest mit `minScore: 10`
  ist 10,0/10; `get_violations` für `src/AiNetLinter.IntegrationTests` meldet
  0 Verstöße; der gezielte Dead-Code-Scan meldet 0 Kandidaten. Der einzige
  Magic-Values-Kandidat ist das erforderliche Wire-Propertyliteral `"text"`
  und wird nicht durch ein `nameof` ersetzt.
- Kein Commit: Das Slicekriterium verlangt den absichtlich roten Hard-Cut-Test;
  ein grüner Commit wäre erst nach den nachfolgenden Umbauslices zulässig.

### Slice 02 – Typisierte interne Ownership herstellen (2026-09-12)

- `get_assembly_context` bezieht die `InspectAssemblyPayload`-Evidenz nun direkt
  über `InspectAssemblyTool.BuildPayload`. Der Composite parst oder liest für
  seine Assembly-Inspektion weder `CallToolResult` noch `StructuredContent`.
  `InspectAssemblyResponseBuilder` bleibt allein für die spätere MCP-Projektion
  zuständig; die fachliche, unveränderliche Payload wird vor dem Rendering
  aufgebaut und weitergegeben.
- Red-Test-First: `AssemblyAnalysisContextOwnershipTests` war zunächst rot, weil
  der Composite `InspectAssemblyTool.ExecuteAsync` und dessen
  `StructuredContent` konsumierte; nach dem Umbau grün. Die bestehenden
  Assembly-Kontextregressionen (5) und Navigationstests (2) bleiben grün.
- Verifikation: fokussierter Ownership-Test grün; `dotnet build` grün mit
  0 Warnungen/0 Fehlern. Der vollständige FastTest-Gate hat ausschließlich den
  erwarteten Slice-01-Content-only-Rotpunkt (1/2537) gezeigt. Der vollständige
  Integrationstest wurde nach einem hängenden Testhost beendet und deshalb
  nicht als grüner Abschlussnachweis gewertet; die fokussierten
  Assembly-Kontextregressionen sind grün.
- MCP-Qualitätsgate für `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis`:
  `safeguard` 10,0/10, `get_violations` 0, Dead-Code-Scan 0 Kandidaten und
  geänderter Magic-Values-Scan 0 Kandidaten.
- Kein Commit: Der globale Content-only-Vertrag ist bis Slice 04 erwartbar rot;
  außerdem fehlt wegen des abgebrochenen vollständigen Integrationstests ein
  vollständiger grüner Gate-Nachweis.

### Slice 03 – Einen Agentenrenderer und das Contentbudget etablieren (2026-09-12)

- `AgentContentRenderer` rendert eine deterministische Reihenfolge vollständiger,
  typisierter Evidenzeinheiten in den sichtbaren UTF-8-Text. Er verwirft nur
  komplette optionale Einheiten, prüft den finalen Text erneut und meldet bei
  nicht darstellbarer Pflichtinformation die exakte Mindestbytezahl.
- Die gemeinsame Ergebnisfabrik und ihr Navigationssuffix nutzen diesen einen
  Renderer. Budgetfehler enthalten `minimumResponseBytes` und einen direkt
  ausführbaren Retry mit `maxResponseBytes=<Mindestwert>` im Content.
- `McpResponseSize.TotalBytes` und die betroffenen Assembly-/Symbol-Body-
  Budgetpfade zählen ausschließlich final sichtbaren UTF-8-Text. Die bis Slice
  04 erlaubten strukturierten Zwischenmodelle bleiben dabei von Auswahl und
  Fit-Check entkoppelt. Assembly-Typ-, Member- und Extension-Handoff-IDs
  erscheinen einmalig im Content.
- Red-Test-First: Der neue `AgentContentRendererTests` war zunächst wegen des
  fehlenden Renderers nicht kompilierbar und prüft danach Auswahl ganzer
  Einheiten, UTF-8-Fit sowie Mindest-Retry. Der fokussierte FastTest ist grün
  (36 bestanden); `dotnet build` ist grün mit 0 Warnungen und 0 Fehlern.
- Der vollständige FastTest-Gate hat ausschließlich den erwarteten globalen
  Slice-01-Hard-Cut-Fehler (1/2540) für noch vorhandenes `StructuredContent` in
  den allgemeinen Error- und Response-Budget-Pfaden. Kein Commit bis Slice 04.
- MCP-Qualitätsgate im geänderten Renderer-Scope: `safeguard` 10,0/10 und
  `get_violations` 0. Der gezielte Dead-Code- und Magic-Values-Check fand 0
  Kandidaten; keine fachliche Analyse wurde verändert.

### Slice 04 – `structuredContent` restlos hart entfernen (2026-09-12)

- Die Integrationstests enthalten keine Altverträge mehr, die interne DTOs oder
  JSON-Payloads lesen. Die verbleibenden Systemgrenzen prüfen ausschließlich
  sichtbaren Content: der Raw-Wire-Vertrag fordert genau einen nichtleeren
  Textblock und keine zusätzlichen Ergebnisfelder; Source- und
  Assembly-Handoffs, Daemon-Sessions und fachliche Fehler bleiben als reale
  End-to-End-Verträge abgedeckt.
- Entfernte, nur für den abgeschafften Wirekanal verwendete Testhelfer wurden
  zusammen mit ihren letzten Consumern gelöscht. Der Dead-Code-Scan für
  `src/AiNetLinter.IntegrationTests` meldet danach 0 Kandidaten.
- Verifikation:
  - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --logger "trx;LogFileName=Slice04FastFinal.trx"` – 2507 bestanden.
  - `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --logger "trx;LogFileName=Slice04IntegrationGreen.trx"` – 216 bestanden.
  - `dotnet build` – 0 Warnungen, 0 Fehler.
  - MCP für `src/AiNetLinter.IntegrationTests`: `safeguard --minScore 10` ist
    10,0/10, `get_violations` meldet 0 und `find_dead_code` 0 Kandidaten. Der
  einzige Magic-Values-Hinweis ist das verpflichtende Raw-Wire-Feldliteral
  `"text"` und bleibt bewusst unverändert.

### Slice 05 – Integrationstests und Dokumentation auf den Endzustand schneiden (2026-09-12)

- Die repräsentativen Integrationstests bleiben bewusst unverändert: Der
  Raw-Wire-Vertrag bündelt Erfolg, leeres Ergebnis, Trunkierung und Fehler an
  der echten MCP-/Prozessgrenze; der Produktionsguard schützt den Hard Cut.
  Weitere fachliche Varianten gehören in die FastTests.
- Regeln, Runtime-Agent-Guide, Bootstrap-, API- und Integrationsdokumentation
  beschreiben ausschließlich den sichtbaren Content: kopierbare Handoff-IDs,
  `operation`/`completeness`, `isError` und die Wiederholung mit dem im Fehler
  genannten `minimumResponseBytes`. Die IsError-Policy trennt korrigierbare
  Ergebnisse (`isError=false`) von echten Toolfehlern, ohne einen zweiten
  Ausgabekanal zu beschreiben.
- Die Agent-API-Matrix und ihre Smoke-Assertions prüfen den Content-only-
  Vertrag; die frühere StructuredContent-Parität der separaten
  Integrationstestverschlankung ist damit ausdrücklich obsolet. `README.md`,
  `Docs/configuration.md` und `ainetlinter-rules.json` blieben unverändert,
  weil kein CLI-, Konfigurations- oder Regelschema geändert wurde.
- Verifikation: `dotnet build` (0 Warnungen, 0 Fehler), fokussierter
  `McpDocumentationSmokeTests`-Lauf (6 bestanden), FastTests ohne Stress
  (2507 bestanden) und Integrationstests ohne Stress (216 bestanden). Ein
  erster vollständiger Integrationslauf scheiterte ausschließlich an der
  zeitabhängigen doppelten Daemon-Ereigniszählung; der isolierte Test und der
  vollständige Wiederholungslauf sind grün.
- MCP: `safeguard` im MCP-Testscope 10,0/10, `get_violations` 0 und
  `find_dead_code` 0 Kandidaten. `find_magic_values` meldet ausschließlich
  vorbestehende Testliterale (`"opaque-init"`, Raw-Wire-Property `"text"`),
  die für ihre Verträge nicht zu ändern sind.

### Slice 06 – Agentische Verifikation und Release-Gate (abgeschlossen, 2026-09-12)

- Frischhost-Dogfooding ist für Raw-Wire (Erfolg, Empty, Truncation,
  InvalidArgument), Source-Handoff (Eviction/Reload), Assembly-Handoff
  (Eviction/Restart, Fremdtarget und stale Snapshot), Loading/InvalidArgument,
  Budgetretry/Malfunction sowie Daemon-Assembly-Routen grün. Jede geprüfte
  Antwort hat genau einen nichtleeren Textblock und keinen Ersatzkanal.
- Die Raw-Wire-Regression schützt nun zusätzlich pro repräsentativer Route,
  dass der sichtbare Content kleiner als die frühere Text-plus-Structured-
  Payload bleibt; `ResponseMatrix.md` enthält die nachweisbaren Obergrenzen
  und bewahrten Agenteninformationen.
- Entfernt wurden tote File-Skeleton-Wire-DTOs, JSON-Projektoren und
  Post-Navigation-Pfade ohne Content-Consumer. Namespace-Budgetierung zählt
  ausschließlich sichtbare UTF-8-Bytes; `MinimumContentBytes` ersetzt die
  irreführende Legacy-Bezeichnung.
- Nachweise bisher: Build 0 Warnungen/Fehler; fokussierte File-Structure- und
  Renderer-Tests 52 grün; Budget-/Malfunction-Tests 10 grün; Raw-Wire 1,
  Source-Handoff 1, Assembly-Handoff 2, Error/Loading 2 und Daemon/Assembly 1
  grün; vollständige FastTests ohne Stress 2.504 grün.
- Die TRX-Analyse des einmalig fehlgeschlagenen Daemonvertrags belegt keine
  reproduzierbare Ursache: Der vollständige Lauf über alle parallelen
  Collections ist anschließend zweimal mit dem Vertrag grün gelaufen, ohne
  globale Serialisierung oder Prozessbereinigung. Der relevante
  `DaemonHostMcpProcessContractTests`-Lauf blieb dabei repräsentativ an der
  echten Daemon-/Assembly-Grenze.
- Der Dead-Code-Audit fand vier verwaiste Überbleibsel der entfernten
  Dual-Output-Budgetprojektion. Drei unreferenzierte Klassenstruktur-Methoden,
  ihre Zwischenmodelle und eine Namespace-Hilfsmethode wurden entfernt; der
  erneute Scan meldet 0 Kandidaten. Die drei `nameof`-Heuristiken referenzieren
  bewusst stabile JSON- bzw. Argumentfeldnamen und bleiben unverändert.
- Finaler Release-Gate: `dotnet build` 0 Warnungen/Fehler, FastTests ohne
  Stress 2.504 bestanden und Integrationstests ohne Stress 216 bestanden.
- MCP im geänderten File-Structure-Scope: `safeguard --minScore 10` 10,0/10,
  `get_violations` 0 und `find_dead_code` 0 Kandidaten. Der produktionsweite
  MCP-Scan zeigt ausschließlich die zwei vorbestehenden, scope-fremden Befunde
  `DecompiledProjectPaths` (fehlender Sentinel-Test) und
  `McpNavigationProjection.Create` (sechs Parameter). Der README-Vertrag
  enthält noch `structuredContent` und bleibt gemäß ausdrücklicher
  README-Ausnahme unverändert.

### Abschlussaudit – Scope prüfen und Findings proaktiv beheben (2026-09-12)

- Unabhängiger MCP-first-Audit gegen Konzept, Wire-, Handoff-, Budget-,
  Lifecycle-, Test- und Dokumentationsvertrag: Der globale Content-only-Guard
  sowie die Raw-Wire- und Dogfood-Verträge bleiben grün. Die Negativsuche
  findet außerhalb historischer Taskartefakte und des ausdrücklich unveränderten
  README keine aktive `StructuredContent`-/`structuredContent`-Referenz.
- Red-Test-First-Finding: `McpToolResults.Text<T>(text, payload)` war ein
  verbliebener No-op-Kompatibilitätsadapter des entfernten Wirekanals. Der neue
  FastTest `Text_HasNoLegacyPayloadOverload` war zunächst rot und ist nach dem
  Entfernen des Adapters grün. Seine überzähligen Payloadübergaben und die
  dadurch verwaisten Wire-DTOs, Projektoren und Budgetpfade wurden minimal
  entfernt; die fachlichen typisierten Scanner- und Analysemodelle bleiben
  erhalten.
- MCP-Audit: `find_dead_code` für `src/AiNetLinter/Mcp` meldet 0 Kandidaten.
  Der geänderte Magic-Values-Scan zeigt nur drei vorbestehende externe
  Feldnamenliterale für `maxResponseBytes`; sie bleiben als Wirevertrag stabil.
  `safeguard --minScore 10` und `get_violations` zeigen ausschließlich die
  zwei dokumentierten, scopefremden Altbefunde `DecompiledProjectPaths` und
  `McpNavigationProjection.Create`; sie wurden nicht durch diesen Task
  verursacht und daher nicht verändert.
- Abschlussgate: `dotnet build` ohne Warnungen/Fehler; FastTests ohne Stress
  2.505 bestanden; Integrationstests ohne Stress beim Wiederholungslauf
  216 bestanden. Der erste Integrationslauf hatte ausschließlich einen nicht
  reproduzierbaren Daemon-Prozessfehler; TRX-Diagnose und isolierter
  Wiederholungstest waren grün. `git diff --check` ist grün.
