## Epic-1-Stand

Epic 1 ist im Working Tree umgesetzt: eager WholeProject-Decompilation, persistenter Cache, konfigurierbarer CacheRoot/Timeout, atomare Generation-/Pointer-Veröffentlichung, Wiederverwendung und Aufräumung bei Cancellation bzw. Locks. Epic 2/M11 (Entfernung der bisherigen On-Demand-Body-Auflösung) bleibt bewusst außerhalb dieses Auftrags.

## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs` — erzeugt mit `WholeProjectDecompiler` echte `.cs`-Dateien und ein echtes `.csproj` in einem Staging-Verzeichnis.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs` — Cache-Key, Staging, Manifest, Generation-Retention und atomare Veröffentlichung.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyCacheGenerationStorage.cs` — Manifest-/Pfadvalidierung und Wiederlesen veröffentlichter Generationen.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.Generation.cs` — Fresh-Build, Publish und Snapshot-Erzeugung als zweiter Partial-Slice der Session.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs` — Cache-Hit/Fresh-Build, Snapshot-Erzeugung aus den finalen Cache-Pfaden und Cancellation-Aufräumung.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs` — lädt das dekompilierte Projektfile als Roslyn-Projekt.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisHostComposition.cs` — lädt und verdrahtet Assembly-Analyse-Konfiguration in Registry-Fallback-Sessions.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs` — setzt für eager Assembly-Snapshots `ContentMode=decompiledProject` und `BodyAvailability=available`.

## Aufrufer und Abhängigkeiten

- `AssemblyAnalysisRegistry`/`AssemblyAnalysisRegistryEntryFactory` erzeugen Sessions; MCP-Assembly-Routen leasen über diese Registry.
- `AssemblyAnalysisSession` löst Referenzen weiterhin nur als Metadaten auf; die WholeProject-Decompilation läuft ausschließlich für die Ziel-Assembly.
- Navigations-, Skeleton-, Symbol-Body- und Call-Tree-Tools konsumieren den Roslyn-Snapshot über `AssemblyAnalysisLease`/`ISolutionStateProvider`.
- `ICSharpCode.Decompiler`/`WholeProjectDecompiler` ist die verwendete Decompilation-Abhängigkeit.
- Cache-Vertrag: `<CacheRoot>/<cache-key>/generation-<n>` plus `current`-Pointer; laufende Builds liegen als `generation-<guid>.tmp` daneben. Manifest und Pointer werden über temporäre Dateien veröffentlicht.

## MCP-first-Befunde

- Semantische Abfragen wurden mit den aktuellen Schemas (`targetType` und absolutem `targetPath=C:\Daten\Entwicklung\Ralf\AiNetLinter`) auf Adapter, Cache, Session, Registry, Host-Composition und WorkspaceFactory ausgeführt.
- Verifiziert: Registry-Fallback ist der produktive Konfigurationspfad; direkte Tool-Dispatches umgehen diese Host-Composition absichtlich. `AssemblyAnalysisContextFactory.FromGeneration` ist der zentrale eager Origin-/ContentMode-Vertrag.
- Verifiziert: keine rekursive WholeProject-Decompilation von Referenz-DLLs; Referenzauflösung bleibt metadatenbasiert.
- Gezielt geprüft: `find_duplicates`, `find_dead_code`, `find_magic_values` im Analysis-Scope. Es blieben nur bereits vorhandene, scope-fremde Duplicate-/Low-Confidence-Dead-Code-Hinweise; neue Magic Values wurden nicht festgestellt.

## Tests, Konfiguration und Dokumentation

- Epic-FastTests: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs`, `AssemblyAnalysisCacheTests.cs`, `AssemblyCacheCleanupTests.cs`, `src/AiNetLinter.FastTests/Configuration/AssemblyAnalysisConfigurationLoaderTests.cs` und `AssemblyAnalysisHostCompositionTests.cs`.
- Konfiguration: `src/AiNetLinter/Configuration/AssemblyAnalysisConfiguration.cs`, `appsettings.json`, dokumentiert in `Docs/configuration.md`.
- Task-Artefakt bewusst nur hier aktualisiert; `roadmap.md` und das fremde Working-Tree-Change in `execution-log.md` wurden nicht verändert.

## Invarianten und offene Risiken

- Nur vollständig validierte Staging-Inhalte werden als Generation sichtbar; ein alter `current`-Pointer bleibt bei Fehlern erhalten.
- `.tmp`-Staging-Verzeichnisse werden bei erfolgreichem oder abgebrochenem Build best-effort entfernt; gelockte Dateien verhindern weder den Cache-Publish noch den Cleanup-Aufruf.
- Cache-Hits validieren Fingerprint, Options-Identität, Manifest, Referenzen und tatsächliche Dokumentdateien vor der Wiederverwendung.
- WholeProjectDecompiler kann zusätzliche Hilfsquellen (z. B. `AssemblyInfo.cs`) erzeugen; Tests erwarten deshalb nicht mehr genau ein Dokument.
- Die alte Body-Resolver-/Signature-Only-Infrastruktur ist bis M11 weiterhin vorhanden und wird von diesem Epic nicht entfernt.
