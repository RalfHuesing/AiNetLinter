---
task: decompiled-assembly-analysis
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-28T14:37:34+02:00
---

# CodeMap: decompiled-assembly-analysis

Pointer-Karte der für den Task relevanten Bestandscodebereiche. Die Einträge
benennen nur Ort und Zweck; aktuelle Entscheidungen und Detailänderungen
werden im jeweiligen Step-Plan bzw. Step-Ergebnis festgehalten.

## MCP-Ziel- und Session-Infrastruktur

- **`src/AiNetLinter/Mcp/AnalysisTarget.cs`, `AnalysisTargetResolver.cs` und `AnalysisToolCall.cs`** — enthalten den unveränderlichen Target-Request, die strikte Projekt-/Assembly-Pfadauflösung und die gemeinsame Dispatch-Grenze vor Registry-Lease oder Assembly-Metadatenadapter.
- **`src/AiNetLinter/Mcp/Projects/`** — enthält Projektdefinition, projektbezogene Registry, Leases, Creation Barrier, TTL/LRU-Eviction und Snapshot-Zustand als Ausgangspunkt für die gemeinsame Analyse-Registry.
- **`src/AiNetLinter/Mcp/McpCodeGraphServer.cs`** — enthält die residente projektbasierte Roslyn-/MSBuild-Session mit Lade-, Health-, Staleness- und Refresh-Lifecycle.
- **`src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs`** — bündelt Konfiguration und Hooks für die bestehende Projekt-Session und deren readonly Snapshot-Modus.
- **`src/AiNetLinter/Mcp/McpServerOptionsFactory.cs`** — baut die zentrale MCP-Tool-Collection und ist der Übergang zur einheitlichen Tool-Dispatch-Grenze.
- **`src/AiNetLinter/Mcp/Daemon/`** — enthält Daemon-Start, Projekt-Lifecycle und residente MCP-Server-Hostintegration.
- **`src/AiNetLinter/Mcp/Lifetime/`** — enthält gemeinsame Lebensdauer- und Hintergrund-Taktung für residente Serverkomponenten.
- **`src/AiNetLinter/Mcp/Registration/`** — enthält die Registrierungen und Tool-Schemas, die auf den neuen Target-Vertrag ausgerichtet werden müssen.
- **`src/AiNetLinter/Mcp/ServerInstructions.cs`** — enthält die globalen MCP-Instruktionen zur Projektziel- und Tool-Aufrufsemantik.
- **`src/AiNetLinter/Mcp/McpToolResults.cs`** — enthält gemeinsame MCP-Ergebnis-, Warnungs- und Fehlerdarstellung für Herkunft und Ladezustände.
- **`src/AiNetLinter/Commands/McpServerCommand.cs` und `src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs`** — komponieren die residente Projekt-Registry und bauen die MCP-Tool-Collections für Stdio- bzw. Daemon-Sessions.

## Assembly-, Roslyn- und Referenzanalyse

- **`src/AiNetLinter/Mcp/Assemblies/`** — enthält Session-, immutable Generation-/Pointer-Cache-, typisierte Manifest-, Budget-, Workspace- und PE-Referenzbausteine für readonly Roslyn-Snapshots.
- **`src/AiNetLinter/Mcp/Assemblies/AssemblyCacheContract.cs`, `AssemblyDiagnosticCodes.cs`, `AssemblySessionStatusExtensions.cs` und `AssemblyCacheCleanup.cs`** — bündeln Cache-Wirewerte, Assembly-Diagnosecodes, Statusmapping und sichere Bereinigungshilfen.
- **`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`** — enthält die Assembly-Analyse, deren Kontextfabrik die statische Session sowie die bestehenden MCP-Tools und DTOs verbindet.
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/`** — enthält Symbolsuche, Referenzen und Strukturabfragen als zentrale Roslyn-Konsumenten für beide Target-Arten.
- **`src/AiNetLinter/Mcp/Tools/Analysis/`** — enthält allgemeine Analysewerkzeuge, deren Dispatch und Herkunftssemantik erweitert werden müssen.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/`** — enthält Datei-/Dokumentstrukturabfragen für generierte und quellbasierte Roslyn-Dokumente.
- **`src/AiNetLinter/Mcp/Tools/CallTree/`** — enthält Call-Tree-Abfragen, die von transitive Referenz- und Symbolgraph-Sessions abhängen.
- **`src/AiNetLinter/Mcp/Tools/DependencyGraph/`** — enthält Abhängigkeitsgraph-Abfragen für projektinterne und externe Referenzen.
- **`src/AiNetLinter/Mcp/Tools/MetricsLookup/`** — enthält Metrikabfragen für die spätere Capability-Matrix.
- **`src/AiNetLinter/Mcp/Tools/MetricsTree/`** — enthält hierarchische Metrikabfragen für Solution-, Projekt- und externe Analyseziele.
- **`src/AiNetLinter/Mcp/Tools/PatternDetect/`** — enthält Pattern-Erkennung mit ihrer Herkunfts- und Regelprofilgrenze.
- **`src/AiNetLinter/Mcp/Tools/ServerMaintenance/`** — enthält Health-, Reload- und Maintenance-Abfragen für getrennte Session-Kapazitäten.
- **`src/AiNetLinter/Baseline/SourceFileCatalog.cs`** — stellt den aktuellen Roslyn-Solution-/Dokumentkatalog für projektbasierte Sessions bereit.
- **`src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs`** — lädt Solutions über MSBuildWorkspace und bildet die Basis für externe Source-Snapshot-Lader.
- **`src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs`** — bündelt die zentralen `obj`-/`bin`-/Worktree-Ausschlüsse für freie Dateisystem-Scans, die die Integration-Gates vor generierten Assembly-Cachequellen schützen.
- **`Directory.Packages.props` und `src/AiNetLinter/AiNetLinter.csproj`** — zentrale Paketversionen und Runtime-Projekt, an denen die statische Decompiler-Abhängigkeit und ihre Auslieferung angebunden werden.

## Konfiguration und bestehender Cache

- **`src/AiNetLinter/Configuration/`** — enthält die strikte rules-/Projektkonfiguration und ist der Integrationspunkt für globale externe Source-Mappings.
- **`src/AiNetLinter/Cache/AnalysisCacheManager.cs`** — enthält den bestehenden Batch-Analysecache, der vom neuen externen Session-/Decompilation-Cache getrennt bleiben muss.
- **`appsettings.json`** — enthält die aktuelle Logging-Konfiguration und ist der projektweite Einstiegspunkt für optionale externe Analyseeinstellungen.
- **`ainetlinter.project.json`** — definiert den bestehenden projektgebundenen Solution-/Rules-Vertrag, der für reine Assembly-Ziele nicht erforderlich werden soll.

## Tests und Test-Infrastruktur

- **`src/AiNetLinter.FastTests/Mcp/Projects/`** — enthält schnelle Tests für Projekt-Registry, Leases, Ladebarrieren und Eviction.
- **`src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`** — enthält schnelle Assembly-Toolregressionen sowie Session-, Cache-, Grenzwert- und Snapshot-Tests.
- **`src/AiNetLinter.FastTests/Mcp/`** — enthält weitere schnelle MCP-Vertrags-, Result- und Tooltests für Regressionen.
- **`src/AiNetLinter.IntegrationTests/Architecture/McpProcessArchitectureGuardTests.cs`** — enthält den freien Architekturquellscan mit dem zentralen Generated-/bin-Ausschluss für Cachequellen (zuletzt: step-004).
- **`src/AiNetLinter.IntegrationTests/Platform/LoadedFixtureTests.cs`** — enthält den geladenen Fixture-/Source-Katalogscan mit dem zentralen Generated-/bin-Ausschluss (zuletzt: step-004).
- **`src/AiNetLinter.IntegrationTests/Mcp/`** — enthält MCP-Daemon-, Host-, Staleness-, Tool- und End-to-End-Tests für echte Projekt-/Serverabläufe.
- **`src/AiNetLinter.TestKit/Mcp/`** — enthält wiederverwendbare MCP-, Roslyn-, Fixture- und temporäre Dateisystem-Hilfen für isolierte externe Analyse-Tests.
- **`src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs`, `TestTempDirectory.cs` und `ProjectRegistryFixture.cs`** — liefern die vorhandene Adhoc-Roslyn-/Referenz-, Repo-Temp- und Registry-Testinfrastruktur für Assembly-Sessions.
- **`src/AiNetLinter.TestKit/AssemblyTestHelper.cs`, `McpTestResultText.cs` und `TestWaiter.cs`** — enthalten gemeinsame Assembly-Emission, MCP-Text- und Condition-Wait-Helfer für FastTests.

## Dokumentation und Agentenverträge

- **`Docs/agent-api.md`** — dokumentiert MCP-Tools, Request-/Result-Verträge, Lifecycle, Fehlerzustände und die aktuelle metadata-only Assembly-Oberfläche.
- **`Docs/integration.md`** — dokumentiert MCP-Integration, projektgebundene Tool-Aufrufe, Registry-Verhalten und Test-/Client-Nutzung.
- **`Docs/configuration.md`** — dokumentiert Projekt-, Rules-, CLI- und Laufzeitkonfiguration einschließlich strikter Konfigurationsregeln.
- **`Docs/mcp-bootstrap.md`** — dokumentiert MCP-Server-Registrierung, `ainetlinter.project.json` und die projektgebundene Bootstrap-Semantik.
- **`Docs/ROADMAP.md`** — enthält den übergeordneten Produktstand und bereits abgeschlossene metadata-only Assembly-Analyse als Ausgangslage.
- **`.agents/rules/AiNetLinter-McpWorkflow.mdc`** — enthält die Agentenentscheidungshilfe für MCP-vor-`rg`-Discovery und den aktuellen Assembly-Analysevertrag.
