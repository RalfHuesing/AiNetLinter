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
  erfolgreichen Resident-Lease-Inkrement zurück. `SourceSnapshotLease.AcquireSibling`
  bewahrt den unabhängigen Snapshot-Lease-Pfad für Source-Project-Kinder.

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
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs`

Test-/Fixturepfad:

- `src/AiNetLinter.FastTests/Fixtures/ExternalSourceSnapshotTestFactory.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRegistryTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRouteTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs`

## Verifizierte Tests und vorhandene Nachweise

- `AssemblyAnalysisRouteTests.AssemblyRoute_ResolvesRootReferenceAndAllowsLazyTransitiveTarget`
  dispatcht Root und Transitivziel über genau einen produktiven Dispatcher-/Tool-Aufruf.
- `AssemblyAnalysisRouteTests.AssemblyRoute_ExpandsMappedSourceProjectReferenceThroughOneDispatcherCall`
  prüft eine gemappte Solution, in der die Source-Project-Referenz nicht über
  einen DLL-Nachbarpfad aufgelöst werden kann, einschließlich Child-Session und
  Source-Origin.
- `SourceSnapshotRegistryTests.Acquire_FailedDuplicateDisposeRollsBackResidentSnapshotLease`
  deckt ausschließlich den belegten Duplicate-Acquire-/Dispose-Fehlerpfad ab.
- Bereits vorhandene `AssemblyAnalysisRegistryTests`,
  `AssemblyAnalysisContextFactoryTests`, `AssemblyAnalysisToolTests`,
  `SourceSnapshotRegistryTests` und `ExternalResourceRegistryTests` wurden als
  bestehende Nachweise für Lease-, Graph-, Resolver-, Budget-, Health-, TTL/LRU-,
  CreationBarrier- und Cancellation-Verträge wiederverwendet.

Die aktuelle MCP-Tool-Registry akzeptiert für diese Installation nur das
Pflichtfeld `projectRoot` (absolut); die Workflow-Regel nennt zusätzlich
`targetType=project`/`targetPath`. Die tatsächlichen Abfragen wurden daher mit
`projectRoot=C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter` und passenden
`scopeFilter`-Werten protokolliert. Nach den letzten Codeänderungen wurde der
gezielte Violations-Check erneut als abschließender codebezogener MCP-Schritt
ausgeführt.

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
  `Docs/integration.md`, `Docs/configuration.md`, `Docs/ROADMAP.md`,
  `Docs/rationale.md`.
