---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 012
corrects: null
title: "Gemeinsame Host-Komposition für direkte Assembly-MCP-Tools verdrahten"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T22:00:54+02:00
related_to:
  - step-010/step-plan.md
  - step-010/step-result.md
  - step-011/step-plan.md
  - step-011/step-result.md
  - step-011/step-review.md
context_budget:
  read_first:
    - "tasks/decompiled-assembly-analysis/step-011/step-result.md"
    - "tasks/decompiled-assembly-analysis/step-011/step-review.md"
    - "tasks/decompiled-assembly-analysis/codemap.md"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs"
    - "src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs"
    - "src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs"
    - "src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs"
    - "src/AiNetLinter/Mcp/McpServerOptionsFactory.cs"
    - "src/AiNetLinter/Commands/McpServerCommand.cs"
    - "src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs"
  read_on_demand:
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs — vorhandene Orchestrator-Überladung, Scope- und Result-Builder-Grenze"
    - "src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs und src/AiNetLinter/Mcp/Assemblies/UnavailableExternalSourceProvider.cs — Provider-Port, Default-Fallback und Ownership ohne Dispose-Vertrag"
    - "src/AiNetLinter/Mcp/AnalysisToolCall.cs — bereits geprüfter Assembly-Dispatch; nur für die unveränderte Callback-Grenze"
    - "src/AiNetLinter.FastTests/Mcp/WiringContractTests.cs und src/AiNetLinter.FastTests/Mcp/McpServerOptionsFactoryTests.cs — Tool-Inventar und Factory-Kompatibilitätsregressionen"
    - "src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostMcpContractTests.cs — bestehender Session-Test für den expliziten Composition-Parameter"
    - "src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs und src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostMcpProcessContractTests.cs — vorhandene Stdio-/Daemon-Handshake- und Assembly-Toolpfade"
    - "src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs, src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs und src/AiNetLinter.TestKit/AssemblyTestHelper.cs — vorhandene Recording-Provider-, Snapshot- und DLL-Fixtures wiederverwenden; TD-004 nicht duplizieren"
  out_of_scope:
    - "Änderungen an AnalysisToolCall, AnalysisTargetRequest, AnalysisTarget, ProjectRegistry, DaemonRuntimeContext oder den übrigen MCP-Registrierungen; AnalysisToolCall bleibt die bereits geprüfte kanonische Assembly-Callback-Grenze"
    - "Änderungen an ExternalSourceMapping, ExternalSourceConfigurationLoader-Schema, ExternalSourceSnapshot, SourceSnapshotIdentity, SourceSnapshotRegistry.Acquire/Lease, AssemblySourceMatchResolver, AssemblySourceSelection, AssemblyAnalysisContextRequest oder AssemblyAnalysisContextFactory"
    - "Gitea-Clone/Fetch, Authentifizierung, Branch-/Refresh-Logik, Netzwerk, echte Provider-Akquisition, lokale Source-of-Truth, Solution-Akquisition, persistenter Source-Cache und EPIC-04-Fehlersemantik"
    - "Transitive Referenzen, Capability-Matrix, zusätzliche Toolfamilien, Health-/Kapazitäts-/TTL-/LRU-Verträge, Refresh, Binary-/PDB-/SourceLink-Versionsbeweis und externe Testausführung"
    - "Neue DI-/Plugin-/AnalysisRegistry-Infrastruktur, Assembly.Load, Reflection-Ausführung, AssemblyLoadContext, Runtime-Ausführung fremder Assemblies oder Fremdprojekt-Restore"
    - "CLI-Optionen, appsettings-/Mapping-Schema, Docs, README, rules.json, task-state.md, codemap.md, tech-debt.md oder Änderungen an step-001 bis step-011"
    - "TD-001, TD-002, TD-003 und TD-004 sowie breite DRY-/MagicValues-/DeadCode-Sweeps; nur ein direkt berührter und architektonisch sicherer Fund dürfte im Implementierungsschritt mitgezogen werden, aktuell ist keiner erkennbar"
---

# Step 012: Gemeinsame Host-Komposition für direkte Assembly-MCP-Tools verdrahten

## Bezug und tatsächliche Lücke

EPIC-03 ist offen. Die genehmigten Steps 005/006 liefern Mapping und Provider-
Port, Step 007 die Snapshot-Registry mit Lease, Step 008 Match, Step 009 die
Source-backed-Factory mit Decompilation-Fallback, Step 010 den
`AssemblySourceSelectionOrchestrator` samt Support-Überladung und Step 011 die
Lease-/Cancellation-/Result-Builder-Regressionen.

Der tatsächliche Code bestätigt die noch offene Grenze:

- `AssemblyAnalysisToolSupport` kann bereits einen Orchestrator nutzen, aber
  `InspectAssemblyTool` und `FindAssemblyExtensionsTool` rufen aus ihren
  MCP-Registrierungen weiterhin den Legacy-Overload ohne Orchestrator auf.
- `AnalysisToolCall.ExecuteAssemblyAsync` liest und validiert `targetType` /
  `targetPath` und reicht den kanonischen DLL-Pfad an einen Callback weiter.
  Für die Source-Auswahl fehlt dort keine Semantik; der Callback ist der
  richtige Adapterpunkt. `AnalysisToolCall` muss deshalb unverändert bleiben.
- `McpServerOptionsFactory.BuildToolCollection` registriert beide direkten
  Assembly-Tools, besitzt aber noch keinen Composition-Parameter.
- `McpServerCommand.RunAsync` (Stdio) und
  `DaemonHostCommand.RunAsync`/`RunMcpSessionAsync` (Daemon) bauen die
  Tool-Collection an getrennten Hostpfaden und erzeugen derzeit keine
  gemeinsame Loader-/Provider-/Snapshot-Registry-/Orchestrator-Instanz.

Die vier genannten Bereiche passen in einen einzigen vertikalen Step, wenn
`AnalysisToolCall` nicht aufgeweitet wird: ein primärer
`AssemblyAnalysisHostComposition`-Vertrag, ein eng gekoppelter direkter
Registration-Adapter, drei Schichten und höchstens acht Akzeptanzkriterien.
Die Hostpfade sind dabei Verbraucher desselben Kontexts, kein dritter
Fachvertrag. Eine epic-große Verdrahtung mit weiteren Tools oder Referenz-
auflösung wäre nicht Teil dieses Steps.

## Split-Gate

- **Primäre Verträge:** genau ein neuer Host-Composition-Vertrag plus ein eng
  gekoppelter Adaptervertrag für die zwei direkten Assembly-Registrierungen.
  Der bestehende Target-/Dispatch-Vertrag und die Source-/Snapshot-/Match-/
  Fallback-Verträge werden nur konsumiert.
- **Schichten:** (1) Host-Komposition und Ownership, (2) direkter MCP-
  Registration-Adapter einschließlich der zwei dünnen Tool-Overloads,
  (3) Stdio-/Daemon-Verbraucher und fokussierte Regressionen.
- **Akzeptanzkriterien:** acht, siehe unten.
- **`read_first`:** zwölf Dateien, siehe `context_budget`; weitere Test- und
  Supportdateien sind bewusst `read_on_demand`.
- **Kontextgrenze:** keine Änderungen an bereits genehmigten Source-,
  Snapshot-, Match-, Factory- oder Fallback-Verträgen außer deren minimaler
  Consumer-Komposition.

## Intention

Eine explizite, hostlebenslange `AssemblyAnalysisHostComposition` soll den
bereits vorhandenen Loader-Result, den Provider-Port, die
`SourceSnapshotRegistry` und den `AssemblySourceSelectionOrchestrator`
zusammenhalten. Stdio erhält genau eine solche Instanz für seine MCP-Session;
der Daemon erhält genau eine Instanz für seine gesamte Lebensdauer und reicht
dieselbe Instanz in jede Session weiter. Dadurch bleibt die Snapshot-Registry
gemeinsamer Owner der residenten Snapshots, während die einzelnen Toolaufrufe
nur kurzlebige Leases besitzen.

Der direkte Registration-Adapter reicht diese Komposition ausschließlich über
`AnalysisToolCall.ExecuteAssemblyAsync` an die beiden bestehenden
Assembly-Tool-Wrapper weiter. Die Wrapper nutzen den bereits genehmigten
`AssemblyAnalysisToolSupport.ExecuteAsync`-Overload mit Orchestrator. Der
Legacy-Aufruf ohne Komposition bleibt für bestehende Factory-/Testaufrufe
verfügbar und bleibt der reine Decompilation-Fallback. Es wird keine zweite
Mappingwahl, Registry-Acquire- oder Result-Builder-Implementierung eingeführt.

## Kontext-Handoff

### Invarianten

- `AssemblyAnalysisHostComposition` lädt die externe Konfiguration genau einmal
  über `ExternalSourceConfigurationLoader.Load(settingsPath)` und bewahrt den
  unveränderlichen `ExternalSourceConfigurationLoadResult` samt Diagnosen. Bei
  fehlendem Provider wird ausschließlich der bestehende
  `UnavailableExternalSourceProvider` eingesetzt; Mapping-Schema und
  Provider-Semantik ändern sich nicht.
- Die Komposition hält genau eine Provider-Referenz, genau eine
  `SourceSnapshotRegistry` und genau einen
  `AssemblySourceSelectionOrchestrator`. Sie erzeugt nichts pro Toolaufruf und
  im produktiven Daemon nichts pro Verbindung. Der aktuelle Provider-Port hat
  keinen Dispose-Vertrag; die Komposition erweitert ihn nicht künstlich. Die
  Registry wird von der Komposition besessen und beim Hostende genau einmal
  freigegeben.
- Die Snapshot-Registry bleibt Owner des Snapshots. Ein direkter Toolaufruf
  besitzt nur den vom Orchestrator eröffneten Lease-Scope; dieser reicht bis
  nach Factory und `BuildResult` und wird auch bei Cancellation oder Exception
  freigegeben.
- Die Auswahl bleibt exakt die vorhandene Folge aus statischer
  `AssemblyReferenceResolver`-Identität, validiertem Alias, Providerresultat,
  `SourceSnapshotRegistry.Acquire`, `AssemblySourceMatchResolver` und
  `AssemblySourceSelection`. Kein Dateiname, Repositoryname, Pfad oder
  Consumer-Projekt wird als Ersatzsignal verwendet.
- `AssemblyAnalysisToolRegistrations` reicht den kanonischen `assemblyPath` aus
  `AnalysisToolCall.ExecuteAssemblyAsync` an den bestehenden Support weiter.
  `targetType`, `targetPath`, Filter, Limits, Cancellation und
  `CallToolResult`-Semantik bleiben unverändert. Die Registrierung dupliziert
  weder Source-Selection noch Diagnosezusammenführung.
- `McpServerOptionsFactory` erhält nur einen expliziten Durchleitungsweg für die
  Komposition. Der bestehende Aufruf ohne Komposition bleibt als kompatibler
  Legacy-/Testpfad erhalten; kein Factory- oder Registration-Code erzeugt
  heimlich eine neue Host-Komposition.
- Stdio besitzt die Komposition länger als `McpServer`; der Daemon besitzt sie
  länger als alle von `DaemonHost` gestarteten MCP-Sessions. Die
  `ProjectRegistry`-Leases und der `DaemonRuntimeContext` bleiben davon
  getrennt. Parallele Daemon-Sessions teilen die Registry sicher, nicht eine
  Session-spezifische Selection.
- Die Tool-Liste, MCP-Schemata und alle Projekt-Toolpfade bleiben unverändert.
  Die Assembly-Beschreibung darf nur minimal ergänzen, dass eine verfügbare
  explizite Source-Zuordnung source-backed genutzt wird; sie darf keine
  Gitea-Verfügbarkeit oder transitive Fähigkeiten behaupten.

### Risiken

- Wird die Komposition in jedem Registration-Lambda erzeugt, entstehen mehrere
  Snapshot-Registry-Owner und die Deduplizierungs-/Dispose-Grenze driftet. Die
  Hostpfade müssen daher die Instanz explizit erstellen und in die Collection
  durchreichen.
- Wird die Komposition am Daemon-Connection-Callback statt am Host erstellt,
  ist die Ownership trotz funktionierender Einzeltests falsch. Der
  `SessionRunner` muss die eine Daemon-Instanz capturen; die Session-Methode
  darf nur den bereits vorhandenen `runtimeContext` ergänzen.
- Ein neuer Source-Pfad direkt in `InspectAssemblyTool` oder
  `FindAssemblyExtensionsTool` würde die Support-Grenze duplizieren. Die
  beiden Overloads dürfen nur Parametersatz und Orchestrator an den
  gemeinsamen Support delegieren.
- Ein optionaler Parameter in der Factory darf nicht dazu führen, dass der
  produktive Host den Legacy-Fallback nutzt. Die beiden produktiven Aufrufe
  müssen in Tests oder statischer MCP-Semantik als explizit kompositionsgeführt
  erkennbar bleiben.
- Ein Provider ist in diesem Step weiterhin ein injizierter Port. Unavailable,
  fehlende Mappings und Loaderdiagnosen bleiben sichtbare Fallback-Zustände;
  Netzwerk-, Auth- oder Refresh-Fehler werden nicht vorweggenommen.

### Relevante MCP-Symbole

- `T:AiNetLinter.Mcp.Assemblies.AssemblyAnalysisHostComposition` (neu) —
  gemeinsamer Loader-/Provider-/Registry-/Orchestrator-Owner und Host-Lifetime.
- `T:AiNetLinter.Configuration.ExternalSourceConfigurationLoader` und
  `M:AiNetLinter.Configuration.ExternalSourceConfigurationLoader.Load` —
  einmaliger Loader-Einstieg ohne Schemaänderung.
- `T:AiNetLinter.Mcp.Assemblies.IExternalSourceProvider` und
  `T:AiNetLinter.Mcp.Assemblies.UnavailableExternalSourceProvider` — injizierter
  Provider-Port und deterministischer Default-Fallback.
- `T:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry`,
  `M:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry.Acquire` und
  `T:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionOrchestrator` —
  Snapshot-Ownership und bestehende Selection-Auswahl.
- `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisToolSupport` und
  `M:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisToolSupport.ExecuteAsync`
  — gemeinsamer Source-/Fallback-Verbraucher samt Lease-Scope.
- `T:AiNetLinter.Mcp.Registration.AssemblyAnalysisToolRegistrations` und
  `M:...AssemblyAnalysisToolRegistrations.Register` — einziger direkter
  Registration-Adapter für `inspect_assembly` und
  `find_assembly_extensions`.
- `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.InspectAssemblyTool` und
  `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.FindAssemblyExtensionsTool` —
  dünne Overloads, die den gemeinsamen Support mit Orchestrator aufrufen.
- `M:AiNetLinter.Mcp.AnalysisToolCall.ExecuteAssemblyAsync` — unveränderte
  kanonische Target-/Callback-Grenze.
- `T:AiNetLinter.Mcp.McpServerOptionsFactory`,
  `M:...McpServerOptionsFactory.BuildToolCollection` und
  `M:...McpServerOptionsFactory.Create` — explizite Kompositionsdurchleitung.
- `M:AiNetLinter.Commands.McpServerCommand.RunAsync`,
  `M:AiNetLinter.Mcp.Daemon.DaemonHostCommand.RunAsync`,
  `M:AiNetLinter.Mcp.Daemon.DaemonHostCommand.CreateSessionRunner` und
  `M:AiNetLinter.Mcp.Daemon.DaemonHostCommand.RunMcpSessionAsync` — die zwei
  produktiven Host-Lifetime-/Session-Verbraucher.

### Sicherer Einstiegspunkt

Zuerst die vorhandene Orchestrator-/Registry-/Loader-Lifetime aus Step 010/011
gegen den neuen `AssemblyAnalysisHostComposition`-Typ schneiden. Die
Komposition soll eine kleine statische Erzeugungsfabrik und eine idempotente
`Dispose`-Grenze haben; sie darf weder `ProjectRegistry` noch
`DaemonRuntimeContext` kennen. Danach den expliziten Composition-Parameter
durch `McpServerOptionsFactory` und genau `AssemblyAnalysisToolRegistrations`
führen. Erst wenn beide direkten Wrapper den bestehenden Support-Overload
verwenden, die Stdio- und Daemon-Hostpfade anschließen. Zum Schluss die
Registration- und Host-Regressionspunkte aus `read_on_demand` ergänzen.

## Konkrete Änderungen

### Schicht 1 — Gemeinsame Assembly-Host-Komposition und Ownership

#### `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisHostComposition.cs` (neu)

- **Was:** Einen internen, `IDisposable`-fähigen Host-Kontext mit
  `ExternalSourceConfigurationLoadResult`, `IExternalSourceProvider`,
  `SourceSnapshotRegistry` und `AssemblySourceSelectionOrchestrator` als
  expliziten Bestandteilen anlegen. Die statische Factory lädt den Result über
  den vorhandenen Loader und verwendet bei fehlender Injection den
  `UnavailableExternalSourceProvider`.
- **Was:** Registry und Orchestrator aus genau diesem Result, Provider und
  Registry erzeugen. Der Provider bleibt ein Port ohne neues Dispose-/DI-
  Modell; die Komposition besitzt nur die aktuelle Provider-Referenz und
  beendet ausschließlich die von ihr erzeugte Snapshot-Registry.
- **Was:** `Dispose` idempotent machen und nach dem Dispose keine neuen
  Selection-Aufrufe zulassen. Keine Synchronisations-, TTL- oder Refresh-
  Semantik ergänzen; die vorhandene Registry bleibt für parallele Toolaufrufe
  zuständig.
- **Warum:** Loader, Provider, Registry und Orchestrator erhalten eine
  nachvollziehbare Host-Lifetime. Damit wird die im Konzept geforderte
  gemeinsame Ownership hergestellt, ohne die Source-/Snapshot-Verträge zu
  ändern oder einen globalen Service-Locator einzuführen.

### Schicht 2 — Direkter Registration-Adapter für zwei Assembly-Tools

#### `McpServerOptionsFactory.cs`, `AssemblyAnalysisToolRegistrations.cs`,
`InspectAssemblyTool.cs` und `FindAssemblyExtensionsTool.cs`

- **Was:** `BuildToolCollection`/`Create` und
  `AssemblyAnalysisToolRegistrations.Register` um einen expliziten,
  kompositionsgeführten Pfad erweitern. Der bestehende Aufruf ohne
  Komposition bleibt als dünner Legacy-/Test-Overload erhalten und erzeugt
  keine Komposition. Nur die beiden direkten Assembly-Registrierungen erhalten
  den Kontext; Projekt-, Symbol-, Maintenance- und Duplicate-Tools bleiben
  unverändert.
- **Was:** Die beiden Registrierungs-Lambdas weiterhin über
  `AnalysisToolCall.ExecuteAssemblyAsync` führen und im Kompositionspfad
  ausschließlich `composition.Orchestrator` an den jeweiligen Tool-Wrapper
  weiterreichen. `AnalysisToolCall` selbst nicht ändern.
- **Was:** In `InspectAssemblyTool` und
  `FindAssemblyExtensionsTool` je einen internen Overload ergänzen, der den
  bereits bestehenden `AssemblyToolExecutionParameters`-Aufbau und Result-
  Builder verwendet und nur den Support-Aufruf mit Orchestrator auswählt.
  Keine parallelen Result-Builder, Diagnose- oder Filterpfade einführen.
- **Was:** Die kurzen Tool-Beschreibungen nur insoweit präzisieren, dass bei
  einer verfügbaren expliziten Source-Zuordnung die bestehende
  source-backed-Auswahl genutzt wird und sonst die statische Decompilation
  greift. Target-Schema, Consumer-Projekt-Aussage, Limits und
  „DLL wird weder geladen noch ausgeführt“ bleiben erhalten.
- **Warum:** Der vorbereitete Support-Consumer wird an genau einem direkten
  Adapter sichtbar genutzt. Der kanonische Dispatch bleibt stabil, und beide
  Assembly-Tools erhalten identische Source-/Fallback-Semantik.

### Schicht 3 — Stdio-/Daemon-Verbraucher und fokussierte Regressionen

#### `src/AiNetLinter/Commands/McpServerCommand.cs` und
`src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs`

- **Was:** `McpServerCommand.RunAsync` erstellt nach der Projektregistry genau
  eine `AssemblyAnalysisHostComposition`, reicht sie an
  `BuildToolCollection` weiter und hält sie bis nach dem MCP-Server am Leben.
  Die Deklarations-/Dispose-Reihenfolge muss sicherstellen, dass der Server
  vor der Snapshot-Registry endet.
- **Was:** `DaemonHostCommand.RunAsync` erstellt genau eine Komposition auf
  Hostebene. `CreateSessionRunner` capturt sie; der neue bzw. erweiterte
  `RunMcpSessionAsync` reicht dieselbe Instanz in `BuildToolCollection` jeder
  Verbindung. Es wird keine Komposition je Connection oder je Tool erzeugt.
  Einen vorhandenen Test-Overload nur dann anpassen, wenn der konkrete
  Aufrufer sonst den produktiven Pfad nicht abbildet.
- **Was:** Den bestehenden `runtimeContext` ausschließlich für Logging und
  Maintenance-Tools weiterreichen. Die Assembly-Komposition erhält keinen
  Connectionzustand und wird nicht in `DaemonRuntimeContext` verschoben.
- **Warum:** Beide realen Hostpfade verwenden dieselbe Ownership-Grenze; die
  Daemon-Registry kann Snapshots über parallele Sessions deduplizieren, ohne
  Projekt- und Source-Lifecycle zu vermischen.

#### Regressionen

- **Kompositions-Lifetime:** Eine fokussierte Fast-Testklasse prüft mit dem
  vorhandenen Recording-Provider und den vorhandenen Snapshot-/DLL-Fixtures,
  dass Loader-Result, Provider, Registry und Orchestrator aus einem Hostkontext
  stammen, ein Mapping nicht vor dem Toolaufruf aufgelöst wird und Dispose die
  Registry genau einmal beendet. Kein neuer `CreateSnapshot`-Helper neben der
  TD-004-Duplikation.
- **Direkter Registration-Adapter:** Über die vorhandene in-process-MCP-
  Harness bzw. die bestehende Tool-Collection werden `inspect_assembly` und
  `find_assembly_extensions` mit derselben Komposition ausgeführt. Prüfen:
  source-backed Origin bei Matched, vorhandenes Source-only-Symbol, korrekte
  Filter-/Limit-Weitergabe, sichtbare Providerdiagnose und decompiled Fallback
  bei No-Mapping/unavailable. Die Provider-Aufrufe und Registry-ResidentCount
  müssen die bestehende Deduplizierungssemantik einhalten.
- **Host-Parität:** Die vorhandenen Stdio- und Daemon-Contract-Tests werden
  nur um den expliziten Composition-Pfad bzw. dessen unverändertes Tool-
  Inventar ergänzt. Beide Pfade müssen Handshake, `targetType`/`targetPath`,
  Assembly-Fallback und die 29er Tool-Liste behalten; kein Netzwerk und keine
  Runtime-Assembly-Ausführung.
- **Bestehende Support-Regressionsklasse:** Die in Step 011 genehmigten
  Source-/Fallback-/Lease-Aussagen bleiben unverändert und werden nicht in die
  Hosttests kopiert. Neue Adaptertests verwenden vorhandene TestKit-Fixtures
  oder teilen einen sicheren Helper, statt TD-004 künstlich zu vergrößern.

## Akzeptanzkriterien

- [ ] Es gibt genau eine explizite `AssemblyAnalysisHostComposition`, die
  Loader-Result, Provider-Referenz, `SourceSnapshotRegistry` und
  `AssemblySourceSelectionOrchestrator` für eine Host-Lifetime zusammenführt;
  ihre Dispose-Grenze beendet die von ihr erzeugte Registry idempotent.
- [ ] Der Stdio-Host erstellt eine Komposition pro MCP-Serverlauf und der
  Daemon eine Komposition pro Daemonlauf; jede Daemon-Session verwendet diese
  Instanz, ohne per Connection/Tool neue Registry- oder Provider-Objekte zu
  erzeugen.
- [ ] Nur `AssemblyAnalysisToolRegistrations` erhält die Komposition als
  direkter MCP-Adapter; `inspect_assembly` und `find_assembly_extensions`
  delegieren über `AnalysisToolCall.ExecuteAssemblyAsync` an den vorhandenen
  Orchestrator-Support-Overload.
- [ ] `AnalysisToolCall`, Target-Schema, Projekt-Dispatch, Filter, Limits,
  Cancellation und Tool-Inventar bleiben unverändert; der Legacy-Factory-
  Overload ohne Komposition bleibt deterministischer Decompilation-Fallback.
- [ ] Source-backed, No-Mapping, unavailable und Loader-/Providerdiagnose
  verhalten sich über den direkten MCP-Adapter genauso wie im genehmigten
  Support-Vertrag; Lease und Snapshot werden erst an den bestehenden
  Scope-/Registry-Grenzen freigegeben.
- [ ] Fokussierte Fast-/Integration-Regressionen decken Kompositionsownership,
  beide direkten Registrierungen sowie Stdio-/Daemon-Parität mit vorhandenen
  deterministischen Fixtures ab; keine Assembly wird geladen, ausgeführt oder
  per Reflection/AssemblyLoadContext untersucht.
- [ ] TD-001 bis TD-004 bleiben unangetastet, weil kein Fund direkt und sicher
  in diesen schmalen Consumer-Schnitt fällt; es gibt keine künstliche
  DRY-/MagicValues-/DeadCode-Bereinigung.
- [ ] `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  sind grün. Stress, Gitea/Netzwerk und Transitivitäts-/Capability-Tests
  bleiben außerhalb.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Semantik, Referenzen, Impact und Violations zuerst über
  AiNetLinter-MCP mit absolutem
  `projectRoot=C:\Daten\Entwicklung\Ralf\AiNetLinter`; `rg` bleibt auf
  Text-/Pfadsuche begrenzt. Vorherige semantische Prüfung zeigte die
  Registrierungs-, Factory- und Host-Callgraphen als vollständig.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — keine
  DI-/Plugin-/Service-Locator-Schicht, kein Runtime-Laden, keine Reflection-
  Ausführung und kein AssemblyLoadContext.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3,
  zentrale TestTempDirectory-/TestKit-Fixtures, deterministische Test-Doubles,
  Build und vollständige Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  Source-/Fallback-/Diagnoseassertions nicht abschwächen; direkte TD-Funde nur
  bei sicherer Berührung, kein Sweep.
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md#Split-Gate vor dem Coder`
  — höchstens ein primärer Vertrag, maximal drei Schichten, acht Kriterien
  und zwölf `read_first`-Dateien.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md` —
  genau ein nächster Step, JIT-Kontext und Handoff; keine Produktionsänderung
  in der Planung.

## Handoff

Der Coder startet ausschließlich mit diesem Step-Plan, den beiden Step-011-
Result-/Review-Dateien und den zwölf `read_first`-Dateien. Für jede weitere
C#-Semantik-, Referenz- oder Impact-Frage zuerst AiNetLinter-MCP mit dem
absoluten Projektroot verwenden; `rg` nur für exakte Textsuche. Keine
Assembly.Load-, Reflection-Ausführung oder AssemblyLoadContext-Verwendung.

Sicherer Implementierungsablauf:

1. `AssemblyAnalysisHostComposition` als kleine Ownership-Grenze ergänzen und
   mit einer vorhandenen Provider-/Snapshot-Fixture testen.
2. Den Kompositionsparameter durch `McpServerOptionsFactory` zu genau
   `AssemblyAnalysisToolRegistrations` führen; die beiden Tool-Overloads nur an
   den bereits vorhandenen `AssemblyAnalysisToolSupport` delegieren.
3. Stdio- und Daemon-Host-Lifetime anschließen, dann fokussierte Registration-
   und Host-Paritätsregressionen ausführen.

Der Handoff darf nicht in `AnalysisToolCall` einsteigen: Das Symbol ist als
kanonische Dispatch-Grenze geprüft und bleibt unverändert. Ebenfalls nicht
erweitern: Source-/Snapshot-/Match-/Fallback-Modelle, transitive Referenzen,
Capability-Matrix oder EPIC-04. Nach erfolgreicher Implementierung schreibt der
Coder ausschließlich sein Step-012-Result; diese Plan- und Roadmap-Datei
bleiben unverändert.
