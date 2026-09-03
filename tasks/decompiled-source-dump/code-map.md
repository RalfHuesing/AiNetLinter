## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs` — führt die eager `WholeProjectDecompiler`-Materialisierung in das Cache-Staging aus; meldet Syntaxfehler dekompilierter Dateien als Warnings; optionaler Konstruktor-Delegat für Test-Resilienz.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs` — baut den `AdhocWorkspace` aus den materialisierten Dokumenten mit ihren absoluten Dateipfaden und dem erzeugten `.csproj`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.Generation.cs` — erstellt/installiert den Roslyn-Snapshot ohne Body-Resolver-Delegat; stuft Syntaxfehler in `ValidateCompilation` als Warning ein und erhält den Snapshot als `Partial`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs` — setzt Session-Status bei Decompilations-Diagnosen auf `Partial`; stellt flexible Konstruktor-Injektion für Testbarkeit bereit.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyCacheCleanup.cs` — lock-tolerante Bereinigung von Dateien, Verzeichnissen und Generationen unter Datei-Sperren.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSessionModels.cs` — berechnet aus dem materialisierten `.csproj` und den echten `.cs`-Dokumenten den absoluten Projekt-/SourceRoot-Vertrag je Generation.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs` und `Responses/InspectAssemblyResponseBuilder.cs` — geben die drei `decompiled*`-Pfade kompakt im Header und als Top-Level-Payload aus.
- `src/AiNetLinter/Mcp/AnalysisToolCall.cs` und `Registration/FileStructureToolRegistrations.cs` — direkte physische `get_file_tree`-Route ohne Roslyn-Lease/Projektregistrierung.
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` — löst Assembly- und Source-Symbole über denselben direkten `SourceSymbolBodyResolver.Resolve`-Pfad auf.

## Betroffene Dateien und Symbole

- `AssemblyDecompilationAdapter.cs` — Syntaxfehler und leere Dateien in dekompiliertem Output als Warning deklariert; optionaler `decompileOverride`-Delegat im Konstruktor für isolierte Resilienz-Tests; Klasse bleibt `sealed` (`EnforceSealedClasses`).
- `AssemblyAnalysisSession.Generation.cs` — `ValidateCompilation` erzeugt für Syntaxfehler und semantische Diagnosen `AssemblyDiagnosticSeverity.Warning`; `CreateSnapshotAsync` verwirft den Snapshot bei Syntaxfehlern nicht (`snapshot.Dispose()` entfällt für Syntaxfehler), sondern stuft die Generation auf `AssemblySessionStatus.Partial` herab.
- `AssemblyAnalysisSession.cs` — `DetermineStatus` prüft zusätzlich `decompilation.Diagnostics.Count == 0`, um bei Decompilations-Warnungen `Partial` zu signalisieren; Konstruktor nimmt optionale Test-Abhängigkeiten entgegen.
- `AssemblyCacheCleanup.cs` — `DeleteFile`, `DeleteDirectory` und `RetainGenerations` fangen `IOException` und `UnauthorizedAccessException` bei Dateisperren (z. B. durch `rg` oder Scanner) ab.
- Entfernt: `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs`, `AssemblyBodySyntax.cs` und `AssemblyDecompilationSourceText.cs`; inklusive Typ-für-Typ-On-Demand-Dekompilierung, Syntax-Nachbearbeitung und `throw null!;`-Stub-Erzeugung.
- Entfernt: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyDecompiledBodyResolverTests.cs` sowie die beiden Stub-/Signature-only-Tests aus `AssemblyAnalysisSessionTests.cs`.
- `AssemblyAnalysisSessionModels.cs`, `AssemblyAnalysisService.cs` und `AssemblyAnalysisContextFactory.cs` — `AssemblyBodyResolver`/`BodyResolver` und der ungenutzte `DecompilerResolver` aus den Datenverträgen entfernt.
- `AssemblyReferenceResolver.cs` — liefert nur noch Roslyn-Metadatenreferenzen; erzeugt keinen separaten Decompiler-Resolver für Body-Nachladung.
- `AssemblyAnalysisSession.Generation.cs`, `AssemblyAnalysisContextFactory.cs`, `AssemblyAnalysisService.cs` und `AssemblyAnalysisModels.cs` — transportieren die drei absoluten Pfade vom Cache/Fresh-Build bis `inspect_assembly`; source-backed Kontexte bleiben ohne `decompiled*`-Felder.
- `Tools/SymbolGraph/FindSymbolTool.cs`, `CallGraphTraversal.cs`, `CallGraphTreeBuilder.cs`, `GetTypeHierarchy*` und `Tools/DependencyGraph/DependencyGraphTool.cs` — Assembly-Locations werden auf die echten absoluten `.cs`-Dateien normalisiert; Projektziele behalten ihre relativen Pfade.
- `Bodies/IAssemblyBodyContext.cs` und `References/AssemblyAnalysisLease.cs` — Kontextvertrag auf `Solution` und `AssemblySymbolIdentity` reduziert; `ResolveBodyAsync`, `IsDecompiled` und deren Aufrufer entfernt.
- `SourceSymbolBodyResolver.cs` — übernimmt die Prüfung für Interface-, abstract-, extern- und accessor-basierte Symbole lokal und bleibt der einzige Body-Extraktionspfad.
- `Tools/CallTree/` und `Tools/SymbolGraph/` — Signature-only-Suppressions, `decompiledSignatureOnly`-Hinweise und die zugehörigen Request-/Response-Felder entfernt; Assembly-Navigation verwendet die vollständigen Snapshot-Bodies.
- `Docs/agent-api.md` und `Docs/integration.md` — Body-/Content-Vertrag auf eager `WholeProjectDecompiler`-Projekt-Snapshots, echte Roslyn-Dokumente, den direkten `SourceSymbolBodyResolver`-Pfad und `contentMode=source` für `get_symbol_body` aktualisiert; Interface-/abstract-/extern-Grenzen bleiben dokumentiert.

## Aufrufer und Abhängigkeiten

- `AssemblyAnalysisSession` ruft `AssemblyRoslynWorkspaceFactory.CreateAsync` beim Fresh-Build und Cache-Hit auf; die Generation enthält Snapshot, Origin, Referenzen, Diagnosen und den abgeleiteten physischen `DecompiledProjectPaths`-Vertrag.
- `AssemblyAnalysisSession.Generation.CreateAndInstallGenerationAsync` erzeugt `DecompiledProjectPaths` aus `ProjectFilePath` und allen `DecompiledDocument.GeneratedPath`-Werten; `AssemblyAnalysisContextFactory.FromGeneration` stellt sie dem Inspect-Builder bereit.
- `FileStructureToolRegistrations.AddGetFileTree` validiert `targetType=project`/absolutes `targetPath` über `AnalysisTargetResolver` und ruft `GetFileTreeTool` direkt auf; die bisherige leasegebundene `ExecuteFilesystemAsync`-Route und deren Loading-/Eviction-Verträge bleiben unverändert.
- `AssemblyAnalysisLease` stellt den aktuellen Snapshot über `IAssemblyBodyContext` bereit; `GetSymbolBodyTool` ruft danach direkt `FindReferencesTool.ResolveSymbolAsync` und `SourceSymbolBodyResolver.Resolve` auf.
- `FindSymbolTool`, `GetSymbolBodyTool`, `CallGraphTraversal`/`CallGraphTreeBuilder`, `GetTypeHierarchyFormatter` und `DependencyGraphTool` nutzen den Assembly-Kontext, um konkrete Locations absolut zu formatieren; Header-Metadaten mit Projektpfaden werden nicht in nachfolgenden Antworten wiederholt.
- `FindReferencesTool`, `AssemblyFindReferencesTool`, `GetCallTreeTool`, `AssemblyGetCallTreeTool` und `TransitiveCallGraphFormatter` liefern ihre normalen Sufficiency-Verträge ohne Signature-only-Ausnahme; ihre Assembly-Call-Sites zeigen absolute Pfade.
- MCP-Befunde gegen `C:\Daten\Entwicklung\Ralf\AiNetLinter`: `find_symbol` findet keine entfernten Typen/Methoden (`AssemblyDecompiledBodyResolver`, `AssemblyBodySyntax`, `AssemblyDecompilationSourceText`, `ResolveBodyAsync`, `AssemblyBodyResolver`, `CreateDecompiler`); `get_call_tree` bestätigt `SourceSymbolBodyResolver.Resolve -> GetSymbolBodyTool.RenderResolvedSymbol`.
- Referenz-DLLs bleiben metadatenbasiert; `WholeProjectDecompiler` läuft nur für die Ziel-Assembly. Kein `MSBuildWorkspace` und keine rekursive Referenz-Dekompilierung.

## Relevante Tests, Konfiguration und Dokumentation

- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRegistryRetirementRaceTests.cs` — Test-Stabilisierung: Bereinigung der Eviction-Assertions bei fremdgehaltenem Lease (`Assert.True(registry.TemporaryReferenceEvictionRequestCount > 0)` vor und nach Eviction, Bereinigung bei nachfolgendem Fingerprint-Refresh).
- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyCacheCleanupTests.cs` — Lock-Toleranz bei Datei- und Verzeichnisbereinigung (`DeleteDirectory_IgnoresLockedFileInDirectoryAsBestEffortCleanup`, M10).
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs` — Partielle Snapshots bei Syntaxfehlern im Cache und in frischer Dekompilation (`RefreshAsync_CachedFileContainsSyntaxError_YieldsPartialStatusAndKeepsSnapshotQueryable`, `RefreshAsync_FreshDecompilationWithSyntaxError_YieldsPartialStatusAndKeepsQueryableSnapshot`, M7).
- `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/AssemblyAnalysisPathContractTests.cs` — sechs Assembly-Routen-Tests; echte Typ-, Property-, Indexer-, Event- und Methodenkörper werden über `GetSymbolBodyTool` aus dem Snapshot gelesen.
- `AssemblyAnalysisPathContractTests.AssemblyRoute_ResolvesGeneratedDocumentAndStableParameterMethodAcrossTools` — verifiziert `get_symbol_body`, `find_symbol` und `get_call_tree` gegen denselben existierenden absoluten `.cs`-Pfad ohne `decompiledProjectDirectory`-Header.
- `src/AiNetLinter.FastTests/Mcp/WiringFilesystemContractTests.cs` — direkter `get_file_tree`-Scan eines unregistrierten SourceRoots ohne Registry-Lease.
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerAssemblyHealthE2ETests.cs` — E2E-Kette `inspect_assembly` → absolute Pfade/physische Existenz → `get_file_tree` → `rg` auf dem ausgewiesenen SourceRoot.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/AssemblyNavigationResponseContractTests.cs` und `src/AiNetLinter.FastTests/Mcp/Tools/CallTree/GetCallTreeToolTests.cs` — Navigation-/Call-Tree-Verträge ohne alte Signature-only-Suppression.
- Nicht betroffen und bewusst nicht geändert: `tasks/decompiled-source-dump/roadmap.md`, `execution-log.md`, `tech-debt.md`; keine Step-Dateien und kein Commit.

## Invarianten, Risiken und Unsicherheiten

- M7: Syntaxfehler und semantische Diagnosen in dekompiliertem Quelltext verwerfen den Snapshot nicht; `ValidateCompilation` und `AssemblyDecompilationAdapter` melden diese als Warnings, die Generation verbleibt im Status `Partial`, und alle funktionierenden Typen und Symbole bleiben resident und über Roslyn abfragbar.
- M9: Unvollständige oder abgebrochene Läufe (Cancellation, Decompiler-Absturz ohne `.csproj` oder 0 Dokumente) veröffentlichen keinen Cache und räumen Staging-Verzeichnisse auf.
- M10: Cache-Bereinigung fängt `IOException` und `UnauthorizedAccessException` bei Datei-Sperren durch Hintergrundprozesse (`rg`, Scanner) als Best-Effort ab, ohne Exception zu werfen.
- Gate-Stabilität (Epic 5): Beide Nicht-Stress-Testsuiten (FastTests und IntegrationTests) laufen deterministisch, ohne Hänger und ohne manuelles Eingreifen durch.
- Jede Assembly-Body-Abfrage verwendet den bereits geladenen Roslyn-Syntaxbaum; es gibt keinen Request-Pfad zu `CSharpDecompiler.DecompileTypeAsString` oder einer nachträglichen Stub-/SourceText-Transformation.
- `AssemblyBodyResolution.ContentMode` ist beim direkten Resolver `source`; der Assembly-Analyseheader bleibt `decompiledProject` und beschreibt die Snapshot-Herkunft.
- `DecompiledProjectPaths.DecompiledSourceRoot` ist der tiefste gemeinsame physische Verzeichnisroot aller materialisierten `.cs`-Dokumente; dadurch sind `rg` und `get_file_tree(targetType="project", targetPath=...)` direkt anschließbar. Fehlen Projektpfad oder absolute Dokumentpfade, bleiben die optionalen `decompiled*`-Felder leer statt erfundene Pfade auszugeben.
- Die drei Pfadfelder stehen ausschließlich im `inspect_assembly`-Payload/Header. Die gemeinsame Assembly-Response-Metadatenzeile bleibt für M1/M2/M6/M11 unverändert; Folgeantworten liefern nur ihre konkrete Datei-Location.
- Interfaces sowie abstract-/extern-Symbole bleiben `unavailable` mit Hinweis; verfügbare dekompilierte Member liefern ihren echten Syntaxausschnitt.

## Verifikation

- MCP-Kontext vor und nach Änderungen: `get_feature_context` für `ValidateCompilation`; MCP-Quality-Checks: `find_duplicates` (0 neue Treffer), `find_dead_code` (0 Treffer), `find_magic_values` (0 Treffer).
- Letzter gezielter `get_violations`-Check nach der letzten C#-Änderung: `src/AiNetLinter/Mcp/Assemblies` (0 Violations), `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis` (0 Violations), `src/AiNetLinter.FastTests/Mcp/Assemblies` (0 Violations).
- Build-Verifikation: `dotnet build --no-restore` — 0 Warnungen, 0 Fehler.
- `git diff --check` — sauber ohne Whitespace-Fehler.
- FastTests `Category!=Stress`: 2.440 bestanden, 2 übersprungen (Symlink-Privilegien-Preflight), 0 Fehler in 1 m 33 s.
- IntegrationTests `Category!=Stress`: 385 bestanden, 0 Fehler in 4 m 30 s.
