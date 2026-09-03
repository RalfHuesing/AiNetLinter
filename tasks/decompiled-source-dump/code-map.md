## Epic-1-Stand

Epic 1 ist im Working Tree umgesetzt: eager WholeProject-Decompilation, persistenter Cache, konfigurierbarer CacheRoot/Timeout, atomare Generation-/Pointer-Veröffentlichung, Wiederverwendung und Aufräumung bei Cancellation bzw. Locks. Epic 2/M11 (Entfernung der bisherigen On-Demand-Body-Auflösung) bleibt bewusst außerhalb dieses Auftrags.

## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs` — erzeugt mit `WholeProjectDecompiler` echte `.cs`-Dateien und ein echtes `.csproj` in einem Staging-Verzeichnis.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs` — Cache-Key, Staging, Manifest, Generation-Retention und atomare Veröffentlichung.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.PointerPublishing.cs` — atomarer Current-Pointer, Nachvalidierung und konservative Schutzprüfung vor Generation-Cleanup.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyCacheGenerationStorage.cs` — Manifest-/Pfadvalidierung und Wiederlesen veröffentlichter Generationen.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.Generation.cs` — Fresh-Build, Publish und Snapshot-Erzeugung als zweiter Partial-Slice der Session.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs` — Cache-Hit/Fresh-Build, Snapshot-Erzeugung aus den finalen Cache-Pfaden und Cancellation-Aufräumung.
- `src/AiNetLinter/Configuration/AssemblyAnalysisConfiguration.cs` — validiert konfigurierbare Timeout-Sekunden gegen die Millisekunden-Grenze von `CancellationTokenSource.CancelAfter`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs` — registriert die dekompilierten Dateien in einem `AdhocWorkspace` und verwendet das generierte `.csproj` als Projektpfad.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisHostComposition.cs` — lädt und verdrahtet Assembly-Analyse-Konfiguration in Registry-Fallback-Sessions.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs` — setzt für eager Assembly-Snapshots `ContentMode=decompiledProject` und `BodyAvailability=available`.

## Aufrufer und Abhängigkeiten

- `AssemblyAnalysisRegistry`/`AssemblyAnalysisRegistryEntryFactory` erzeugen Sessions; MCP-Assembly-Routen leasen über diese Registry.
- `AssemblyAnalysisSession` löst Referenzen weiterhin nur als Metadaten auf; die WholeProject-Decompilation läuft ausschließlich für die Ziel-Assembly.
- `AssemblyAnalysisSession.RefreshGenerationAsync` erhält beim Cache-Hit `CachedDecompilationGeneration.ProjectFilePath` bis `CreateSnapshotAsync` und `AssemblyRoslynWorkspaceFactory.CreateProjectInfo`.
- `AssemblyAnalysisSession.BuildFreshGenerationAsync` verwirft nicht vollständige `DecompilationResult`s vor Snapshot-/Cache-Erzeugung; `AssemblyDecompilationCache.ValidatePublishRequest` bleibt die zweite Publish-Schranke.
- `AssemblyDecompilationCache.TryPublishPointer`/`PublishPointerAttempt` validieren den neuen Pointer nach atomarem Ersetzen. `PublishCore` löscht eine fehlgeschlagene Generation nur, wenn der Pointerzustand sicher gelesen werden kann und nicht auf diese Generation zeigt.
- `AssemblyDecompilationOptions.IsSupportedTimeout` ist die gemeinsame Grenze für Konfigurationsloader, Session-Validierung, eager Decompilation und den verbliebenen Body-Resolver.
- Navigations-, Skeleton-, Symbol-Body- und Call-Tree-Tools konsumieren den Roslyn-Snapshot über `AssemblyAnalysisLease`/`ISolutionStateProvider`.
- `ICSharpCode.Decompiler`/`WholeProjectDecompiler` ist die verwendete Decompilation-Abhängigkeit.
- Cache-Vertrag: `<CacheRoot>/<cache-key>/generation-<n>` plus `current`-Pointer; laufende Builds liegen als `generation-<guid>.tmp` daneben. Manifest und Pointer werden über temporäre Dateien veröffentlicht.

## MCP-first-Befunde

- Semantische Abfragen wurden mit den aktuellen Schemas (`targetType` und absolutem `targetPath=C:\Daten\Entwicklung\Ralf\AiNetLinter`) auf Adapter, Cache, Session, Registry, Host-Composition und WorkspaceFactory ausgeführt.
- Verifiziert: Registry-Fallback ist der produktive Konfigurationspfad; direkte Tool-Dispatches umgehen diese Host-Composition absichtlich. `AssemblyAnalysisContextFactory.FromGeneration` ist der zentrale eager Origin-/ContentMode-Vertrag.
- Verifiziert: keine rekursive WholeProject-Decompilation von Referenz-DLLs; Referenzauflösung bleibt metadatenbasiert.
- Gezielt geprüft: `find_duplicates`, `find_dead_code`, `find_magic_values` im Analysis-Scope. Es blieben nur bereits vorhandene, scope-fremde Duplicate-/Low-Confidence-Dead-Code-Hinweise; neue Magic Values wurden nicht festgestellt.

## Relevante Tests, Konfiguration und Dokumentation

- Epic-FastTests: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs` (Cache-Hit-Projektpfad, ungültiger Timeout), `AssemblyAnalysisCacheTests.cs` (unvollständige Generation, Cache-Hit-Verwerfung, Pointer-Lock), `AssemblyCacheCleanupTests.cs`, `src/AiNetLinter.FastTests/Configuration/AssemblyAnalysisConfigurationLoaderTests.cs` (CancelAfter-Grenze) und `AssemblyAnalysisHostCompositionTests.cs`.
- Konfiguration: `src/AiNetLinter/Configuration/AssemblyAnalysisConfiguration.cs`, `appsettings.json`, dokumentiert in `Docs/configuration.md`.
- Task-Artefakt bewusst nur hier aktualisiert; `roadmap.md` und das fremde Working-Tree-Change in `execution-log.md` wurden nicht verändert.

## Invarianten und offene Risiken

- Nur vollständig validierte Staging-Inhalte werden als Generation sichtbar; `IsComplete=false` oder Decompilation-Fehler werden nicht veröffentlicht und fehlerbehaftete Manifeste nicht als Cache-Hit verwendet.
- Bei fehlgeschlagener Pointer-Nachvalidierung wird eine möglicherweise aktuell referenzierte Generation konservativ behalten; dadurch entsteht kein dangling `current.json`, auch wenn eine Dateisperre die Zustandsprüfung verhindert.
- `.tmp`-Staging-Verzeichnisse werden bei erfolgreichem oder abgebrochenem Build best-effort entfernt; gelockte Dateien verhindern weder den Cache-Publish noch den Cleanup-Aufruf.
- Cache-Hits validieren Fingerprint, Options-Identität, Manifest, Referenzen und tatsächliche Dokumentdateien vor der Wiederverwendung.
- Gültige Timeout-Konfigurationen liegen zwischen mehr als null und `int.MaxValue` Millisekunden; die ganzzahlige Sekundenkonfiguration akzeptiert höchstens `int.MaxValue / 1000` Sekunden.
- Der Cache-Hit verwendet den im Manifest gefundenen realen `.csproj`-Pfad; synthetische Fallback-Pfade bleiben nur für bare/in-memory Workspace-Requests ohne Projektpfad.
- WholeProjectDecompiler kann zusätzliche Hilfsquellen (z. B. `AssemblyInfo.cs`) erzeugen; Tests erwarten deshalb nicht mehr genau ein Dokument.
- Die alte Body-Resolver-/Signature-Only-Infrastruktur ist bis M11 weiterhin vorhanden und wird von diesem Epic nicht entfernt.

## Verifikation

- Nach der letzten C#-Codeänderung: gezielter FastTest-Slice mit `FullyQualifiedName~AssemblyAnalysisCacheTests|FullyQualifiedName~AssemblyAnalysisSessionTests|FullyQualifiedName~AssemblyAnalysisConfigurationLoaderTests`, 33/33 bestanden.
- Nach der letzten C#-Codeänderung: gezielte Pfad-/Host-Regressionen mit `FullyQualifiedName~AssemblyAnalysisPathContractTests|FullyQualifiedName~AssemblyAnalysisHostCompositionTests`, 11/11 bestanden.
- Nach der letzten C#-Codeänderung: vollständiger `FastTests`-Nicht-Stresslauf, 2.440/2.446 erfolgreich, 2 übersprungen, 4 Fehler in scope-fremden bestehenden Race-/Repository-Cache-Tests (`ExternalSourceRepositoryCacheWriterTests.PublishAsync_SerializesSameKeyAndLeavesConsistentCurrent`, `ProjectRegistryTests.Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner`, `ProjectRegistryPublishRaceTests.Lease_PublishCreationRace_DisposesLoserOnceOutsideRegistryLock`, `AssemblyAnalysisRegistryRetirementRaceTests.LeaseAsync_FingerprintRefreshClearsPendingRequestForRetiredEntry`).
- Nach der letzten C#-Codeänderung: vollständiger `IntegrationTests`-Nicht-Stresslauf, 384/384 bestanden.
- Nach der letzten C#-Codeänderung: `dotnet build`, erfolgreich ohne Buildfehler.
- Qualitätschecks nach der letzten C#-Codeänderung: `find_duplicates` im Analysis-Produktionsscope meldete nur den bestehenden Near-Duplicate in `AssemblyReferenceResolver`; `find_dead_code` meldete 0 Treffer; `find_magic_values` meldete 0 Treffer.
- Abschließender `get_violations`-Check nach der letzten Codeänderung über `src/AiNetLinter/Mcp/Assemblies/Analysis`, `src/AiNetLinter/Configuration` und `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis`: jeweils 0 Violations.
- Nicht betroffen: `roadmap.md`, `execution-log.md`, `tech-debt.md`, keine Step-Dateien und kein Commit.
