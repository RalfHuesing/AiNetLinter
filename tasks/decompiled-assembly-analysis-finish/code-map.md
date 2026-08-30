# Code-Map: Einheitlicher Roslyn-Analysepfad

Diese Karte ist eine kompakte Navigationshilfe für den Task
`decompiled-assembly-analysis-finish`; sie ist keine vollständige
Repository-Dokumentation. Beziehungen werden von den Rollen gegen den
aktuellen Working Tree und die AiNetLinter-MCP-Abfragen verifiziert.

## Primäraufgabe

Den einheitlichen Roslyn-Analysepfad für dekompilierte Assembly-Analyse
einschließlich Source-Truth, Sessions, Ressourcen, MCP-Capabilities und
Abschlussverifikation fertigstellen.

## Bekannte Einstiegspunkte

- `src/AiNetLinter/Mcp/AnalysisToolCall.cs` — MCP-Dispatcher und gemeinsamer
  Analysepfad.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` — Assembly-Targetauflösung,
  Context-/Session-Aufbau und Inspect-/Analyse-Tools.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/` — Resolver, Session, Registry,
  Source-Selection und Ressourcen-Lifecycle.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` — Mapping, Provider,
  Snapshot-, Cache- und Attestation-Lifecycle.
- `src/AiNetLinter.FastTests/Mcp/` und
  `src/AiNetLinter.IntegrationTests/` — fachbezogene Unit-, Component- und
  MCP-/Integrationstests.

## Korrekturrunde 2: betroffene Produktionssymbole und Aufrufer

- `AnalysisToolCall.ExecuteRouted` → `AssemblyAnalysisDispatcher.ExecuteAsync`
  → `IAssemblyAnalysisRegistry.LeaseAsync`. Der Dispatcher ruft nach dem Root-
  Lease jetzt `AssemblyAnalysisLease.ExpandReferencesAsync` auf; dadurch läuft
  die Expansion über den produktiven gemeinsamen Route-/Toolpfad.
- `AssemblyAnalysisLease.ExpandReferencesAsync` delegiert an
  `References/AssemblyReferenceSessionExpander`; dieser verwaltet eigene
  Consumer-Leases, dedupliziert nach Zielschlüssel und begrenzt Tiefe/Knoten.
  `AssemblyReferenceSession`/`AssemblyReferenceExpansion` liegen in
  `AssemblyAnalysisSessionModels.cs`; `InspectAssemblyTool` projiziert sie in
  `AssemblyReferenceSessionDto` und `InspectAssemblyPayload` sowie in den
  Textkanal.
- `AssemblyAnalysisRegistry.TryLeaseCurrentAsync` hinterlegt pro Entry eine
  `AssemblyReferenceLeaseFactory`. `LeaseReferencedAsync` routet physische
  Ziele und Source-Project-Ziele; die Source-Project-Erzeugung liegt in
  `AssemblyAnalysisRegistry.SourceProjects.cs` und nutzt dieselben Entry-,
  CreationBarrier-, Resource-Budget- und Disposal-Pfade.
- `AssemblyReferenceResolver.ResolveSourceProjectReferences` delegiert an
  `References/SourceProjectReferenceGraph`. Der Graph traversiert ausschließlich
  Projekte der gemappten Snapshot-Solution und macht Missing, Cycle, Dedup,
  Depth-Limit und Node-Limit als `AssemblyReferenceDto`-Zustände sichtbar.
- `AssemblyAnalysisContextFactory.TryCreateSourceBackedContextAsync` und
  `CreateSourceProjectContextAsync` mappen diese Source-Project-Referenzen in
  die Context-Referenzen. `AssemblySourceSelection.ForProject` erzeugt die
  child-spezifische Selection mit eigenem `SourceSnapshotLease`.
- `SourceSnapshotRegistry.Acquire` → `CleanupFailedAcquire` →
  `ReleaseResidentLease` rollt bei fehlgeschlagenem Duplicate-Dispose den
  erfolgreichen Resident-Lease-Inkrement zurück. Nach terminalem Registry-
  Dispose entfernt und entsorgt `ReleaseResidentLease` einen dabei auf null
  fallenden Eintrag außerhalb des Locks; `SourceSnapshotLease.AcquireSibling`
  bewahrt den unabhängigen Snapshot-Lease-Pfad für Source-Project-Kinder.

## Epic 4: Capability-Matrix und Host-Vertrag

- `McpToolRegistrationOptions.TargetedReadOnlyTool` beschreibt den gemeinsamen
  `project|assembly`-Vertrag für Symbolgraph, Struktur, Body und Metriken. Die
  bisherigen `ReadOnlyTool`-Registrierungen bleiben für projektgebundene Regeln,
  Audits, Dateisuche, Test-/Change-Impact und Config-Reload ausdrücklich
  `project-only`; `AssemblyTool` bleibt auf die beiden spezialisierten
  Assembly-Familien begrenzt.
- `McpServerCommand.RunAsync` und `DaemonHostCommand.RunAsync` übergeben dieselbe
  `AssemblyAnalysisHostComposition.Sessions` sowohl an den gemeinsamen
  `AnalysisToolCall`-Target-Dispatcher als auch an die Health-Registrierung.
  `McpServerToolCollectionFactory.Build` und
  `ServerMaintenanceToolRegistrations.Register` halten diese Composition
  optional testbar, im lokalen Default-Host aber vollständig verdrahtet.
- `AssemblyAnalysisDispatcher.CreateRoute` validiert auch bei einer
  unsupported Assembly-Fähigkeit den Target-Pfad und erzeugt den strukturierten
  `ASSEMBLY_TARGET_UNSUPPORTED`-Status mit kanonischem Pfad statt einer
  pfadlosen Erfolgssimulation. `AssemblyReferenceSessionExpander` projiziert
  Missing/Cycle/Node-Limit nun zusätzlich in die gemeinsame Diagnose- und
  Completeness-Liste.
- `FindAssemblyExtensionsTool.ExecuteAsync(AssemblyAnalysisLease, ...)` reicht
  die `ReferenceExpansionDiagnostics` wie `InspectAssemblyTool` in Payload und
  Status weiter. `AssemblyAnalysisResponse.Enrich` verwendet denselben
  Diagnosebestand für `analysis`, einschließlich Source-Snapshot und Revision.
  `AssemblySessionStatusExtensions.ResolveEffectiveStatus` hebt einen
  vollständigen Root bei Expansion-Diagnosen zentral auf `partial`; Spezial-
  payload, gemeinsamer `analysis`-Block und Textheader verwenden denselben
  effektiven Status.
  `AssemblyOrigin.Kind` bleibt als dokumentierter interner Alias zu
  `OriginKind` erhalten.
- `IAssemblyAnalysisRegistry.SnapshotsAsync` und die Health-Snapshot-Methoden in
  `AssemblyAnalysisRegistry.SourceProjects.cs` liefern die residente
  Assembly-/Source-Project-Sicht. `get_server_health` aggregiert Projekt- und
  Assembly-Sessions
  getrennt; ein Assembly-Target lädt/zeigt gezielt eine Session über dieselbe
  Registry und trägt Origin, Snapshot, Hash, Generation, Status und Diagnosen.
- `DaemonHostMcpContractTests` behandelt source-backed Root plus Source-Project
  Child als zwei stabile Resident-Sessions; die Resident-Buchhaltung wird nicht
  durch Wiederholungen kaschiert.

## Korrekturrunde 1: Partiality-Statuskonsistenz und lokaler Bootstrap

- `AssemblyAnalysisDispatcherCapabilityTests` prüft die bereits vorhandenen
  echten Dispatcher-/Inspect-/Extensions-Pfade für Missing, Cycle, Node-Limit
  und Child-Lease-Fehler jetzt über alle Statuskanäle: Spezialpayload,
  gemeinsamer `analysis`-Block und Textantwort müssen jeweils `partial`
  ausweisen.
- `.mcp.json` startet den Repository-Quellstand über `dotnet run` und verweist
  nicht mehr auf das fehlende externe Release-Verzeichnis. Die laufende
  installierte Registry bleibt davon unberührt; ihr `projectRoot`-only-Schema
  und Versionstand sind eine externe Neustart-/Deployment-Voraussetzung.

## Korrekturrunde 3: terminale Snapshot-Rollback-Bereinigung

- `SourceSnapshotRegistry.ReleaseResidentLease` übernimmt für den Rollback-
  Pfad die terminale Null-Lease-Bereinigung des normalen `Release`-Pfads und
  sammelt Snapshot-/Workspace-/Checkout-Dispose-Fehler in der laufenden
  Acquire-Fehlerliste.

## Betroffene Dateien

Produktionspfad:

- `src/AiNetLinter/Mcp/AnalysisToolCall.cs`
- `src/AiNetLinter/Mcp/AnalysisTarget.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisEntry.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.SourceProjects.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSessionModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelection.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/IAssemblyAnalysisRegistry.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyAnalysisLease.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyReferenceSessionExpander.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/References/SourceProjectReferenceGraph.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Snapshots/SourceSnapshotRegistry.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySessionStatusExtensions.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs`
- `src/AiNetLinter/Mcp/Tools/McpToolRegistrationOptions.cs`
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthModels.cs`
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthTool.cs`
- `src/AiNetLinter/Mcp/Registration/AnalysisToolRegistrations.cs`
- `src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs`
- `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs`
- `src/AiNetLinter/Mcp/Registration/SymbolBodyToolRegistrations.cs`
- `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs`
- `src/AiNetLinter/Mcp/Composition/McpServerToolCollectionFactory.cs`
- `src/AiNetLinter/Commands/McpServerCommand.cs`
- `src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs`
- `src/AiNetLinter/Mcp/Registration/McpAgentGuideRegistration.cs`
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`
- `.mcp.json`

Test-/Fixturepfad:

- `src/AiNetLinter.FastTests/Fixtures/ExternalSourceSnapshotTestFactory.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRegistryTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRouteTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisDispatcherCapabilityTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostMcpContractTests.cs`
- `src/AiNetLinter.FastTests/Mcp/WiringContractTests.cs`
- `src/AiNetLinter.FastTests/Mcp/McpAgentGuideRegistrationTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs`
- `src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs`

## Verifizierte Tests und vorhandene Nachweise

- `AssemblyAnalysisRouteTests.AssemblyRoute_ResolvesRootReferenceAndAllowsLazyTransitiveTarget`
  dispatcht Root und Transitivziel über genau einen produktiven Dispatcher-/Tool-Aufruf.
- `AssemblyAnalysisRouteTests.AssemblyRoute_ExpandsMappedSourceProjectReferenceThroughOneDispatcherCall`
  prüft eine gemappte Solution, in der die Source-Project-Referenz nicht über
  einen DLL-Nachbarpfad aufgelöst werden kann, einschließlich Child-Session und
  Source-Origin.
- `SourceSnapshotRegistryTests.Acquire_FailedDuplicateDisposeRollsBackResidentSnapshotLease`
  deckt ausschließlich den belegten Duplicate-Acquire-/Dispose-Fehlerpfad ab.
- `SourceSnapshotRegistryTests.Acquire_FailedDuplicateDisposeAfterTerminalReleaseDisposesResidentSnapshot`
  erzwingt das terminale Interleaving `Registry.Dispose` → Original-Lease →
  Rollback und prüft die vollständige Resident-/Snapshot-/Checkout-Bereinigung.
- `AssemblyAnalysisDispatcherCapabilityTests` weist Missing, Cycle und
  Node-Limit über den produktiven Dispatcher-/Inspect-Route nach und prüft
  separat, dass ein fehlgeschlagener Child-Lease in `find_assembly_extensions`
  als Diagnose und `partial` erscheint.
- `WiringContractTests.ToolCollection_AdvertisesCompleteProjectAssemblyCapabilityMatrix`
  prüft die Capability-Klassen in `tools/list` für alle 29 Tools einschließlich
  project-only, common read-only und Assembly-only.
- `DaemonHostMcpContractTests.RunMcpSessionAsync_RegisteredAssemblyToolsReuseCompositionAcrossSessions`
  prüft die gemeinsame Host-Composition über zwei MCP-Sessions und den stabilen
  Resident-Count von Root plus Source-Project-Child.
- `McpServerAllToolsE2ETests.GetServerHealth_UsesAggregateProjectAndAssemblyTargetVariants`
  prüft Aggregate, geladenen Projekt-Key und gezielten Assembly-Health-Call über
  den lokalen In-Proc-Default-Host.
- `McpAgentGuideRegistrationTests.BuildResource_IsReadableWithoutProjectAndContainsIntegrationContract`
  prüft, dass der einmalige Bootstrap die gemeinsame Target-Matrix, den lokalen
  Default-Host und die Legacy-Parameter-Grenze an den dauerhaften Workflow anhängt.
- Bereits vorhandene `AssemblyAnalysisRegistryTests`,
  `AssemblyAnalysisContextFactoryTests`, `AssemblyAnalysisToolTests`,
  `SourceSnapshotRegistryTests` und `ExternalResourceRegistryTests` wurden als
  bestehende Nachweise für Lease-, Graph-, Resolver-, Budget-, Health-, TTL/LRU-,
  CreationBarrier- und Cancellation-Verträge wiederverwendet.

Die aktuell installierte MCP-Tool-Registry akzeptiert für diese Installation
nur das Pflichtfeld `projectRoot` (absolut); die Workflow-Regel und die aktuelle
Quellregistrierung verwenden zusätzlich `targetType=project|assembly` und
`targetPath`. Die tatsächlichen semantischen MCP-Abfragen wurden deshalb mit
`projectRoot=C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter` und passenden
`scopeFilter`-Werten protokolliert; die Schemaabweichung bleibt ein
Installations-/Deployment-Risiko und ist kein zusätzlicher Source-Vertrag.
Der laufende Daemon meldete dabei Version 1.0.154; ein Neustart oder
Deployment der MCP-Installation ist erforderlich, bevor die neuen
`targetType`/`targetPath`- und Assembly-Session-Verträge live nutzbar sind.

## Epic-Zuordnung

- Epic 1: gemeinsamer Target-, Session- und Roslyn-Route.
- Epic 2: External Source-of-Truth, Trust, Attestation und Cachegenerationen.
- Epic 3: transitive Assembly-Referenzen sowie getrennte externe Ressourcen.
- Epic 4: Capability-Matrix, Host-Integration und End-to-End-Verträge.
- Epic 5: Dokumentation und Abschluss-Gates.

## Abschluss-Suchpunkte

- MCP-Semantik: `get_feature_context`, `find_symbol`, `find_references`,
  `get_impact`, `get_violations`, `safeguard`.
- Qualitätsaudit: `find_duplicates`, `find_dead_code`, `find_magic_values`.
- Abschlussdokumente: `README.md`, `Docs/agent-api.md`,
  `Docs/integration.md`, `Docs/mcp-bootstrap.md`, `Docs/configuration.md`, `Docs/ROADMAP.md`,
  `Docs/rationale.md`; Epic 4 aktualisiert bereits die Capability-/Health-
  Abschnitte in `README.md`, `Docs/agent-api.md` und `Docs/integration.md`.
