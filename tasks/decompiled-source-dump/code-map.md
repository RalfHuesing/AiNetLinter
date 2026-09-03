## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs` — führt die eager `WholeProjectDecompiler`-Materialisierung in das Cache-Staging aus; enthält keinen On-Demand-Decompiler mehr.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs` — baut den `AdhocWorkspace` aus den materialisierten Dokumenten mit ihren absoluten Dateipfaden und dem erzeugten `.csproj`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.Generation.cs` — erstellt/installiert den Roslyn-Snapshot ohne Body-Resolver-Delegat.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSessionModels.cs` — berechnet aus dem materialisierten `.csproj` und den echten `.cs`-Dokumenten den absoluten Projekt-/SourceRoot-Vertrag je Generation.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs` und `Responses/InspectAssemblyResponseBuilder.cs` — geben die drei `decompiled*`-Pfade kompakt im Header und als Top-Level-Payload aus.
- `src/AiNetLinter/Mcp/AnalysisToolCall.cs` und `Registration/FileStructureToolRegistrations.cs` — direkte physische `get_file_tree`-Route ohne Roslyn-Lease/Projektregistrierung.
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` — löst Assembly- und Source-Symbole über denselben direkten `SourceSymbolBodyResolver.Resolve`-Pfad auf.

## Betroffene Dateien und Symbole

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

- `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/AssemblyAnalysisPathContractTests.cs` — sechs Assembly-Routen-Tests; echte Typ-, Property-, Indexer-, Event- und Methodenkörper werden über `GetSymbolBodyTool` aus dem Snapshot gelesen.
- `AssemblyAnalysisPathContractTests.AssemblyRoute_ResolvesGeneratedDocumentAndStableParameterMethodAcrossTools` — verifiziert `get_symbol_body`, `find_symbol` und `get_call_tree` gegen denselben existierenden absoluten `.cs`-Pfad ohne `decompiledProjectDirectory`-Header.
- `src/AiNetLinter.FastTests/Mcp/WiringFilesystemContractTests.cs` — direkter `get_file_tree`-Scan eines unregistrierten SourceRoots ohne Registry-Lease.
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerAssemblyHealthE2ETests.cs` — E2E-Kette `inspect_assembly` → absolute Pfade/physische Existenz → `get_file_tree` → `rg` auf dem ausgewiesenen SourceRoot.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs` — eager Projektmaterialisierung mit realen `.cs`-/`.csproj`-Dateien und ohne `throw null!;`.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/AssemblyNavigationResponseContractTests.cs` und `src/AiNetLinter.FastTests/Mcp/Tools/CallTree/GetCallTreeToolTests.cs` — Navigation-/Call-Tree-Verträge ohne alte Signature-only-Suppression.
- Nicht betroffen und bewusst nicht geändert: `tasks/decompiled-source-dump/roadmap.md`, `execution-log.md`, `tech-debt.md`; keine Step-Dateien und kein Commit.

## Invarianten, Risiken und Unsicherheiten

- Jede Assembly-Body-Abfrage verwendet den bereits geladenen Roslyn-Syntaxbaum; es gibt keinen Request-Pfad zu `CSharpDecompiler.DecompileTypeAsString` oder einer nachträglichen Stub-/SourceText-Transformation.
- `AssemblyBodyResolution.ContentMode` ist beim direkten Resolver `source`; der Assembly-Analyseheader bleibt `decompiledProject` und beschreibt die Snapshot-Herkunft.
- `DecompiledProjectPaths.DecompiledSourceRoot` ist der tiefste gemeinsame physische Verzeichnisroot aller materialisierten `.cs`-Dokumente; dadurch sind `rg` und `get_file_tree(targetType="project", targetPath=...)` direkt anschließbar. Fehlen Projektpfad oder absolute Dokumentpfade, bleiben die optionalen `decompiled*`-Felder leer statt erfundene Pfade auszugeben.
- Die drei Pfadfelder stehen ausschließlich im `inspect_assembly`-Payload/Header. Die gemeinsame Assembly-Response-Metadatenzeile bleibt für M1/M2/M6/M11 unverändert; Folgeantworten liefern nur ihre konkrete Datei-Location.
- Interfaces sowie abstract-/extern-Symbole bleiben `unavailable` mit Hinweis; verfügbare dekompilierte Member liefern ihren echten Syntaxausschnitt.
- Der Snapshot wird weiterhin als `AdhocWorkspace` erstellt und Compilation-Diagnosen werden nach dem Epic-1-Vertrag behandelt; M7-/Resilienzänderungen sind kein Bestandteil dieses Epics.
- Vorhandene, scope-fremde FastTest-Race-Ausfälle aus Epic 1 bleiben ein Abschlussrisiko und wurden nicht durch dieses Epic bearbeitet.

## Verifikation

- MCP-Kontext vor und nach Änderungen: `get_feature_context`, `get_impact`, `find_references` und `get_call_tree` für Workspace-Fabrik, Generation, Lease, `IAssemblyBodyContext` und `SourceSymbolBodyResolver` mit `targetType=project` und absolutem Projektroot; nachher bestätigt `SourceSymbolBodyResolver.Resolve -> GetSymbolBodyTool.RenderResolvedSymbol` als einzigen produktiven Body-Aufrufer und sechs reduzierte Kontextverwendungen ohne Interface-Body-Delegat.
- Nach Änderungen: `find_symbol` für alle entfernten Typen/Methoden — 0 C#-Treffer; `rg` in `src`/`Docs` — keine alten Resolver-/Stub-/On-Demand-Namen.
- Post-change Epic-Testlauf: `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisPathContractTests|FullyQualifiedName~AssemblyAnalysisSessionTests|FullyQualifiedName~AssemblyNavigationResponseContractTests|FullyQualifiedName~GetCallTreeToolTests" --no-restore` — 33/33 bestanden.
- Abschluss-Gates im aktuellen Epic-3-Stand: `dotnet build --no-restore` — 0 Warnungen/0 Fehler; `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore` — 385/385 bestanden; ein vollständiger Fast-Nicht-Stresslauf endete mit 2.432 bestanden, 2 übersprungen und 5 bekannten scope-fremden Race-/Timeout-Fehlern, ein späterer Wiederholungslauf reproduzierte `ProjectRegistryPublishRaceTests` und `AssemblyAnalysisRegistryRetirementRaceTests` und wurde nach ausbleibender Endstatistik kontrolliert beendet; der Epic-3-Fast-Slice blieb 31/31 grün.
- Wiederholter Fast-Gate-Lauf ohne Codeänderung (`Epic2FastTestsFinal.trx`): reproduzierte `ProjectRegistryTests`-Timeout- und `AssemblyAnalysisRegistryRetirementRaceTests`-Fehler, blieb anschließend über 12 Minuten ohne Fortschritt/Ergebnisdatei und wurde kontrolliert mit Ctrl+C beendet; keine Epic-2-Fokustests waren betroffen.
- Audit nach Änderungen: `find_duplicates` — nur 10 fuzzy/nahe Cluster ohne sicheren Epic-2-Fix; `find_dead_code` — 37 LOW, 0 HIGH; `find_magic_values` — 0 sichere Befunde.
- Letzter gezielter `get_violations`-Check nach der letzten C#-Änderung: `src/AiNetLinter.FastTests/Mcp/Assemblies` und `src/AiNetLinter.FastTests/Mcp/Tools/CallTree` — jeweils 0; breiter Produktionsscope `src/AiNetLinter/Mcp` — 1 bestehende scope-fremde `AIContextFootprint`-Warnung in `DaemonHostCommand.cs:15`.
- `git diff --check` — ohne Fehler. `roadmap.md`, `execution-log.md` und `tech-debt.md` wurden von diesem Implementierungsschritt nicht geändert; keine Step-Dateien und kein Commit.
- Dokumentationskorrektur nach dem Epic-2-Review: `Docs/integration.md` und `Docs/agent-api.md` wurden ohne Produktions-/Testcodeänderung angepasst; `roadmap.md`, `execution-log.md` und `tech-debt.md` bleiben unverändert, keine Step-Dateien und kein Commit.
- Epic 3 ergänzt: `Docs/agent-api.md` dokumentiert die dekompilierten `inspect_assembly`-Pfade und die direkte `get_file_tree`-Capability; `Docs/integration.md` dokumentiert die Agent-Reihenfolge `inspect_assembly` → `get_file_tree`/`rg`. `roadmap.md`, `execution-log.md` und `tech-debt.md` wurden in diesem Paket nicht geändert; keine Step-Dateien und kein Commit.
- Epic-3-Endverifikation: `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisPathContractTests|FullyQualifiedName~AssemblyNavigationResponseContractTests|FullyQualifiedName~AssemblyAnalysisRouteTests|FullyQualifiedName~AssemblyAnalysisTransitiveNavigationTests|FullyQualifiedName~GetCallTreeToolTests|FullyQualifiedName~WiringFilesystemContractTests" --no-restore` — 31/31; `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~McpServerAssemblyHealthE2ETests" --no-restore` — 5/5; `CliRepositoryDogfoodTests` — 3/3; vollständige Integration-Nicht-Stresssuite — 385/385.
- Physischer Terminal-Nachweis auf dem finalen Build: `inspect_assembly` → drei absolute existente Pfade; direkter `get_file_tree(targetType=project, targetPath=decompiledSourceRoot)` — vollständiger Scan mit 1.025 `.cs`-Dateien und existentem repräsentativem Root-relative-Pfad; `rg --files --glob '*.cs'` und `rg --fixed-strings --glob '*.cs' 'McpCodeGraphServer'` — jeweils Exit 0, 1.025 bzw. 68 Treffer.
- Der separat registrierte MCP-Connector lieferte bei einem Kontrollaufruf noch den alten `generatedPath=source/...`-Header ohne `decompiled*`-Felder; die lokale, aus diesem Working Tree gestartete MCP-Binary lieferte den neuen Vertrag vollständig. Das ist eine Deployment-/Stale-Binary-Grenze, kein fehlender lokaler Dateisystemzugriff.
