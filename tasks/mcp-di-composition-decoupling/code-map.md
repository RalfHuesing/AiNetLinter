# Code Map: MCP-Komposition entkoppeln und Qualitätsgrenzen wiederherstellen

## Primäre Einstiegspunkte

- Epic 1 ist umgesetzt: `ISolutionStateProvider` kapselt den für
  Assembly-Analyse-Leases tatsächlich benötigten read-only Lösungszustand.
- `McpCodeGraphServer` implementiert diesen Vertrag; die konkrete
  Konstruktion eines read-only Hosts liegt in
  `AssemblyAnalysisEntryFactory`, nicht in Entry oder Lease.
- Der nächste geplante Einstiegspunkt ist Epic 2:
  `AssemblySymbolResolver.ResolveAsync`.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/Assemblies/Analysis/References/ISolutionStateProvider.cs` —
  `GetCurrentSolution`, `AssemblySymbolIdentity`, `LoadState`,
  `Console` und `GetConfigSnapshot`. Die letzten zwei Capabilities werden
  von MetricsTree/GetImpact auf dem Assembly-Pfad benötigt.
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — implementiert den Vertrag
  über explizite Adapter für Symbolidentität und Konfigurations-Snapshot.
- `Mcp/Assemblies/Analysis/References/AssemblyAnalysisLease.cs` — hält und
  übergibt nur noch `ISolutionStateProvider`; die
  `IAssemblyBodyContext`-Auflösung bleibt `GetCurrentSolution`.
- `Mcp/Assemblies/Analysis/AssemblyAnalysisEntry.cs` — trennt
  `State` von seiner `IAsyncDisposable`-Lebensdauer, behält
  Referenzlease-, Locking- und Fehleraggregationsreihenfolge bei.
- `Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisEntryFactory.cs` —
  neue Kompositionsfactory für den read-only `McpCodeGraphServer`.
  `AssemblyAnalysisRegistryEntryFactory` und
  `AssemblyAnalysisSourceProjectEntryFactory` verwenden sie.
- `Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs` und
  `AssemblyToolExecutionParameters` — akzeptieren den Vertragszustand.
- Die unmittelbar über `lease.Server` erreichbaren Tools benutzen ebenfalls
  nur den Vertrag: CallTree, DependencyGraph, FileStructure
  (ClassStructure/FileSkeleton/NamespaceTree), GetSymbolBody,
  MetricsLookup/MetricsTree sowie SymbolGraph
  (FindReferences/FindSymbol/GetImpact/GetTypeHierarchy).

## Aufrufer und Abhängigkeiten

- Assembly-Leases entstehen über `AssemblyAnalysisEntry.TryAcquireLease`;
  die Entry-Factories liefern den separierten Zustand und dessen Ownership.
- Die Tool-Dispatcher können den vorhandenen konkreten Server weiterhin
  übergeben, weil er den Vertrag implementiert; der Assembly-Lease-Pfad kennt
  den konkreten Typ nicht.
- `ProjectLease` und `ProjectRegistry` bleiben unverändert und konkret.
- Kein DI-Container, `IServiceProvider`, neues NuGet-Paket oder
  `rules.json` wurde eingeführt bzw. geändert.

## Relevante Tests, Konfiguration und Dokumentation

- Neu: `src/AiNetLinter.FastTests/Mcp/SolutionStateProviderContractTests.cs`
  prüft den Vertragsaufruf von `AssemblyAnalysisToolSupport`, die
  Lease-/Tool-Signaturen und die Server-Implementierung per Reflection.
- Angepasste Factory-Aufrufer in
  `AssemblyAnalysisRegistryTests`,
  `AssemblyAnalysisRegistryFreshnessTests` und
  `AssemblyAnalysisDispatcherCapabilityTests`.
- Dieses Artefakt ist der aktualisierte Stand für Epic 1; Roadmap, Execution
  Log und Tech Debt wurden vom Epic nicht geändert.

## Invarianten, Risiken und Unsicherheiten

- `AssemblyAnalysisEntry.DisposeAsync` entsorgt weiter erst den
  zustandsbesitzenden Host und aggregiert Fehler wie zuvor; Cancellation,
  Lease-Drain und Body-Resolution wurden nicht umgebaut.
- Der Interfaceumfang ist aus den realen Assembly-Tool-Aufrufen abgeleitet,
  nicht aus einem generischen Server-Abbild.
- Frischer MCP-Metriknachweis: Lease und Entry liegen mit je 1531
  AI-Context-Footprint unter dem Limit 2500.
- Außerhalb von Epic 1 verbleiben `AssemblyHealthProjection`
  (Footprint 2564 über eine andere Response-/Health-Kette),
  `AssemblySymbolResolver.ResolveAsync` (62 statt 60 Zeilen).

## Verifikation

- `dotnet build`: erfolgreich, 0 Warnungen/0 Fehler.
- Fokussierter FastTest-Slice mit State-Contract, ToolSupport, Registry,
  Freshness, Retirement-Race und Path-Contract: 57/57 bestanden.
- MCP-Audits im Produktionsscope `src/AiNetLinter/Mcp`:
  keine exakten Duplikat-Cluster (1523 Methoden), kein High-Confidence
  Dead Code (783 Symbole). Der Magic-Value-Scan meldet ausschließlich
  bestehende Kandidaten in bereits geänderten Dateien, keine aus Epic 1
  eingeführten Werte.
- Frisches MCP-`get_violations` nach der letzten Codeänderung: die zwei
  oben genannten, scope-fremden Folge-Befunde; keine Lease-/Entry-Verletzung.
