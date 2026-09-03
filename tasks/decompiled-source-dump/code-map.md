## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs` — führt die eager `WholeProjectDecompiler`-Materialisierung in das Cache-Staging aus; enthält keinen On-Demand-Decompiler mehr.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs` — baut den `AdhocWorkspace` aus den materialisierten Dokumenten mit ihren absoluten Dateipfaden und dem erzeugten `.csproj`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.Generation.cs` — erstellt/installiert den Roslyn-Snapshot ohne Body-Resolver-Delegat.
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` — löst Assembly- und Source-Symbole über denselben direkten `SourceSymbolBodyResolver.Resolve`-Pfad auf.

## Betroffene Dateien und Symbole

- Entfernt: `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs`, `AssemblyBodySyntax.cs` und `AssemblyDecompilationSourceText.cs`; inklusive Typ-für-Typ-On-Demand-Dekompilierung, Syntax-Nachbearbeitung und `throw null!;`-Stub-Erzeugung.
- Entfernt: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyDecompiledBodyResolverTests.cs` sowie die beiden Stub-/Signature-only-Tests aus `AssemblyAnalysisSessionTests.cs`.
- `AssemblyAnalysisSessionModels.cs`, `AssemblyAnalysisService.cs` und `AssemblyAnalysisContextFactory.cs` — `AssemblyBodyResolver`/`BodyResolver` und der ungenutzte `DecompilerResolver` aus den Datenverträgen entfernt.
- `AssemblyReferenceResolver.cs` — liefert nur noch Roslyn-Metadatenreferenzen; erzeugt keinen separaten Decompiler-Resolver für Body-Nachladung.
- `Bodies/IAssemblyBodyContext.cs` und `References/AssemblyAnalysisLease.cs` — Kontextvertrag auf `Solution` und `AssemblySymbolIdentity` reduziert; `ResolveBodyAsync`, `IsDecompiled` und deren Aufrufer entfernt.
- `SourceSymbolBodyResolver.cs` — übernimmt die Prüfung für Interface-, abstract-, extern- und accessor-basierte Symbole lokal und bleibt der einzige Body-Extraktionspfad.
- `Tools/CallTree/` und `Tools/SymbolGraph/` — Signature-only-Suppressions, `decompiledSignatureOnly`-Hinweise und die zugehörigen Request-/Response-Felder entfernt; Assembly-Navigation verwendet die vollständigen Snapshot-Bodies.
- `Docs/agent-api.md` — Body-/Content-Vertrag auf eager dekompilierte Roslyn-Syntaxbäume und `contentMode=source` für `get_symbol_body` aktualisiert.

## Aufrufer und Abhängigkeiten

- `AssemblyAnalysisSession` ruft `AssemblyRoslynWorkspaceFactory.CreateAsync` beim Fresh-Build und Cache-Hit auf; die Generation enthält nur Snapshot, Origin, Referenzen und Diagnosen.
- `AssemblyAnalysisLease` stellt den aktuellen Snapshot über `IAssemblyBodyContext` bereit; `GetSymbolBodyTool` ruft danach direkt `FindReferencesTool.ResolveSymbolAsync` und `SourceSymbolBodyResolver.Resolve` auf.
- `FindReferencesTool`, `AssemblyFindReferencesTool`, `GetCallTreeTool`, `AssemblyGetCallTreeTool` und `TransitiveCallGraphFormatter` liefern ihre normalen Sufficiency-Verträge ohne Signature-only-Ausnahme.
- MCP-Befunde gegen `C:\Daten\Entwicklung\Ralf\AiNetLinter`: `find_symbol` findet keine entfernten Typen/Methoden (`AssemblyDecompiledBodyResolver`, `AssemblyBodySyntax`, `AssemblyDecompilationSourceText`, `ResolveBodyAsync`, `AssemblyBodyResolver`, `CreateDecompiler`); `get_call_tree` bestätigt `SourceSymbolBodyResolver.Resolve -> GetSymbolBodyTool.RenderResolvedSymbol`.
- Referenz-DLLs bleiben metadatenbasiert; `WholeProjectDecompiler` läuft nur für die Ziel-Assembly. Kein `MSBuildWorkspace` und keine rekursive Referenz-Dekompilierung.

## Relevante Tests, Konfiguration und Dokumentation

- `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/AssemblyAnalysisPathContractTests.cs` — sechs Assembly-Routen-Tests; echte Typ-, Property-, Indexer-, Event- und Methodenkörper werden über `GetSymbolBodyTool` aus dem Snapshot gelesen.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs` — eager Projektmaterialisierung mit realen `.cs`-/`.csproj`-Dateien und ohne `throw null!;`.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/AssemblyNavigationResponseContractTests.cs` und `src/AiNetLinter.FastTests/Mcp/Tools/CallTree/GetCallTreeToolTests.cs` — Navigation-/Call-Tree-Verträge ohne alte Signature-only-Suppression.
- Nicht betroffen und bewusst nicht geändert: `tasks/decompiled-source-dump/roadmap.md`, `execution-log.md`, `tech-debt.md`; keine Step-Dateien und kein Commit.

## Invarianten, Risiken und Unsicherheiten

- Jede Assembly-Body-Abfrage verwendet den bereits geladenen Roslyn-Syntaxbaum; es gibt keinen Request-Pfad zu `CSharpDecompiler.DecompileTypeAsString` oder einer nachträglichen Stub-/SourceText-Transformation.
- `AssemblyBodyResolution.ContentMode` ist beim direkten Resolver `source`; der Assembly-Analyseheader bleibt `decompiledProject` und beschreibt die Snapshot-Herkunft.
- Interfaces sowie abstract-/extern-Symbole bleiben `unavailable` mit Hinweis; verfügbare dekompilierte Member liefern ihren echten Syntaxausschnitt.
- Der Snapshot wird weiterhin als `AdhocWorkspace` erstellt und Compilation-Diagnosen werden nach dem Epic-1-Vertrag behandelt; M7-/Resilienzänderungen sind kein Bestandteil dieses Epics.
- Vorhandene, scope-fremde FastTest-Race-Ausfälle aus Epic 1 bleiben ein Abschlussrisiko und wurden nicht durch dieses Epic bearbeitet.

## Verifikation

- MCP-Kontext vor und nach Änderungen: `get_feature_context`, `get_impact`, `find_references` und `get_call_tree` für Workspace-Fabrik, Generation, Lease, `IAssemblyBodyContext` und `SourceSymbolBodyResolver` mit `targetType=project` und absolutem Projektroot; nachher bestätigt `SourceSymbolBodyResolver.Resolve -> GetSymbolBodyTool.RenderResolvedSymbol` als einzigen produktiven Body-Aufrufer und sechs reduzierte Kontextverwendungen ohne Interface-Body-Delegat.
- Nach Änderungen: `find_symbol` für alle entfernten Typen/Methoden — 0 C#-Treffer; `rg` in `src`/`Docs` — keine alten Resolver-/Stub-/On-Demand-Namen.
- Post-change Epic-Testlauf: `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisPathContractTests|FullyQualifiedName~AssemblyAnalysisSessionTests|FullyQualifiedName~AssemblyNavigationResponseContractTests|FullyQualifiedName~GetCallTreeToolTests" --no-restore` — 33/33 bestanden.
- Abschluss-Gates: `dotnet build --no-restore` — 0 Warnungen/0 Fehler; `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress ...` — 384/384 bestanden; `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress ...` — 2.433 bestanden, 2 übersprungen, 3 scope-fremde Race-/Timeout-Fehler in `AssemblyAnalysisRegistryRetirementRaceTests`, `ProjectRegistryTests` und `ThinClientPumpContractTests`, keine Fehler im Epic-2-Scope.
- Wiederholter Fast-Gate-Lauf ohne Codeänderung (`Epic2FastTestsFinal.trx`): reproduzierte `ProjectRegistryTests`-Timeout- und `AssemblyAnalysisRegistryRetirementRaceTests`-Fehler, blieb anschließend über 12 Minuten ohne Fortschritt/Ergebnisdatei und wurde kontrolliert mit Ctrl+C beendet; keine Epic-2-Fokustests waren betroffen.
- Audit nach Änderungen: `find_duplicates` — nur 10 fuzzy/nahe Cluster ohne sicheren Epic-2-Fix; `find_dead_code` — 37 LOW, 0 HIGH; `find_magic_values` — 0 sichere Befunde.
- Letzter gezielter `get_violations`-Check nach der letzten C#-Änderung: `src/AiNetLinter.FastTests/Mcp/Assemblies` und `src/AiNetLinter.FastTests/Mcp/Tools/CallTree` — jeweils 0; breiter Produktionsscope `src/AiNetLinter/Mcp` — 1 bestehende scope-fremde `AIContextFootprint`-Warnung in `DaemonHostCommand.cs:15`.
- `git diff --check` — ohne Fehler. `roadmap.md`, `execution-log.md` und `tech-debt.md` wurden von diesem Implementierungsschritt nicht geändert; keine Step-Dateien und kein Commit.
