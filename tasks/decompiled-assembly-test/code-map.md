# Code-Map: decompiled-assembly-test

> Navigationshilfe für den Task **Test- und Basiskorrekturen für MCP-Assembly- und Tool-Filter**.
> Stand: Run 2 (Implementierer-Fortsetzung) — Pfade/Symbole gegen Working Tree und MCP verifiziert (v1.0.157, Projekt-Session `C:/Daten/Entwicklung/Ralf/AiNetLinter`).

## Primäre Einstiegspunkte
- MCP-Server für C#-Semantik: `targetType=project`, `targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter` (Solution `AiNetLinter.slnx`, `rules.json`).
- Konzept: `tasks/decompiled-assembly-test/Konzept.md` (status: draft, fachlich vollständig; Muss-Kriterien 1–3; Production-Code seit Commit `1c3faff6` grün).

## Betroffene Dateien und Symbole
### Production (Run-1, verifiziert, NICHT in Run 2 verändert)
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs` — `AssemblyExtensionSearchOptions` (Reihenfolge: ExtensionName, NamespaceFilter, ReceiverType, MaxResults); `FindAssemblyExtensionsArguments` (AssemblyPath, ReceiverType, ExtensionName, Namespace, MaxResults).
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs` — `BuildResult` übergibt `arguments.ReceiverType`.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs` — `FindExtensions` (Filter-Kette inkl. `MatchesReceiverType` auf `Parameters[0].Type`), `MatchesReceiverType` (Zeile 290: unqualifiziert = `ITypeSymbol.Name`, qualifiziert = `CSharpErrorMessageFormat`, ordinal; `NormalizeReceiverFilter` entfernt nur `global::`), `ToExtensionDto` (Applicability via `context.Receiver` = Consumer-Auflösung `AssemblyAnalysisContextFactory.ResolveReceiver`).
- `src/AiNetLinter/Configuration/AssemblyPathValidation.cs` — zentrale Validierung: `IsSupportedAssemblyPath`, `HasSupportedAssemblyExtension` (.dll/.exe, OrdinalIgnoreCase), `WithoutAssemblyExtension`.
- `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs` — Assembly-Pfad-Validierung über `AssemblyPathValidation` (.dll/.exe), Fehlermeldung „.dll- oder .exe-Datei“.
- `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs` — `NormalizeAssembly` nutzt `WithoutAssemblyExtension` (entfernt jetzt auch `.exe`).
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceMatchResolver.cs` — nutzt `WithoutAssemblyExtension` (Zeile 226) für Alias-Normalisierung.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs` — `ResolveReceiver` (Consumer-Compilation via `GetTypeByMetadataName`/AllTypes-Fallback, entfernt `global::`); Consumer-Auflösung NUR im Consumer-Projekt.
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs` — enthält `GetFileTreeScanner` (statisch, `Scan`: `effectiveDepth = input.MaxDepth ?? input.TreeDepth`, 0 = Root-Ebene; Walk via `FileSystemWalkOptions.ForFileTree`) UND `FileTreeAccumulator` (Zeile 45–300: `Build` trennt Aggregation `BuildDirectoryCandidates` von ausgegebenen Einträgen; summary = nur Tiefe ≤ 1 + `maxResults`-Begrenzung mit `directoriesTruncated` → `TruncatedBy: maxResults`; `FileTreeScanResult` trägt `_input.TreeDepth` für den Renderer).
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeTool.cs` — `DefaultMaxResults = 200`, `MaxDepthCap = 32`, Dispatch zu Scanner/Renderer.
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeRenderer.cs` — summary-spezifische Truncation-Warnung („Dateien aggregiert, Verzeichnisliste begrenzt“).
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeInputValidator.cs` — maxDepth/treeDepth 0–32, maxResults 1–2000.
- Ebenfalls Run-1: Tool-Descriptions/Registrierungen (`AssemblyAnalysisToolSupport`, `McpToolRegistrationOptions`, `ServerMaintenanceToolRegistrations`) — .dll/.exe und treeDepth-Description bereits aktualisiert.

### Tests (Run 2, geändert/neu)
- `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetFileTreeScannerTests.cs` — angepasst: `Scan_SummaryViewExposesNoFileListButMarksMaxResultsDirectoryTruncation` (NEUER Vertrag: Truncated=true + maxResults, Directories=1); NEU: `Scan_TreeDepthZeroScansOnlyFilesAtRoot`, `Scan_TreeDepthOneIncludesDirectSubdirectoryFiles`, `Scan_TreeDepthTwoReachesNestedProjectFiles`, `Scan_MaxDepthTakesPrecedenceOverTreeDepth`, `Scan_SummaryViewListsOnlyTopLevelDirectoryAggregates`.
- `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetFileTreeToolTests.cs` — ersetzt: `ExecuteAsync_TreeViewWithTreeDepthZeroShowsOnlyRootFiles` (Root-Datei sichtbar, tiefe Datei unsichtbar, „1 Dateien gezeigt“).
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs` — ersetzt: `FindAssemblyExtensions_ReceiverFilterWithoutMatchIsIndependentOfConsumerProject` (ohne Server, 0 Treffer), NEU: `FindAssemblyExtensions_ReceiverFilterMatchesUnqualifiedQualifiedAndGlobalPrefixOrdinal` (Object/Person/Probe.Extensions.Person/global::-Präfix/„person“/„string“ → ordinale, case-sensitive Semantik). Hinweis: der ersetzte Test war einzige Abdeckung der Consumer-Applicability (applicable/not_applicable via `ReduceExtensionMethod`) — Abdeckung entfällt (siehe Hand-off).
- `src/AiNetLinter.FastTests/Configuration/AssemblyPathValidationTests.cs` — NEU (Unit): IsSupportedAssemblyPath/HasSupportedAssemblyExtension (.dll/.exe, Fehlerfälle .txt/.bin/ohne Endung/.dllx/.exe.tmp), WithoutAssemblyExtension (nur .dll/.exe-Suffix, case-insensitiv).
- `src/AiNetLinter.FastTests/Mcp/AnalysisTargetResolverTests.cs` — NEU: `Resolve_Assembly_AcceptsExistingExeFile`; erweitert: `Resolve_Assembly_RejectsDirectoryAndWrongExtension` (+.bin, Assertion „.dll- oder .exe-Datei“).
- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblySourceMatchResolverTests.cs` — NEU: `Resolve_MatchesConfiguredExeAliasToProjectAssemblyName`, `Resolve_DoesNotStripUnsupportedExtensionFromAlias` (.txt bleibt stehen → no-match).
- `src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs` — `.exe`-Fall in `Load_GueltigeMapping_NormalisiertSolutionPathUndAssemblySuffix` integriert (`Bar.exe` → `Bar`); Datei ≤ 500 Zeilen gehalten (MaxLineCount-Limit).

## Aufrufer und Abhängigkeiten
- `AssemblyPathValidation` (internal): Aufrufer = AnalysisTargetResolver:46, AssemblyAnalysisService:47, ExternalSourceMappingValidator:349, AssemblySourceMatchResolver:226 (MCP-get_impact verifiziert).
- `MatchesReceiverType`: nur aus `FindExtensions` (privater Helfer).
- `GetFileTreeScanner.Scan`: Aufrufer = GetFileTreeTool:33 + FastTests; `FileTreeAccumulator.Build`: nur aus Scan (Zeilen 31/41).
- Doku-Abhängigkeiten: `Docs/agent-api.md` (Tool-Referenz: assembly-Targets, get_file_tree-Parameter, inspect_assembly/find_assembly_extensions); `.agents/rules/AiNetLinter-McpWorkflow.mdc` (Assembly-Tools .dll/.exe).

## Relevante Tests, Konfiguration und Dokumentation
- Gezielte FastTests-Klassen (Run-2-Filter): GetFileTreeScannerTests, GetFileTreeToolTests, AnalysisTargetResolverTests, AssemblySourceMatchResolverTests, AssemblyPathValidationTests, ExternalSourceConfigurationLoaderTests, AssemblyAnalysisToolTests → 128 Tests grün (Zwischenstand mit separatem Loader-Exe-Test), Final-Lauf nach letzter Änderung (siehe Verifikation).
- Doku-Sync (Run 2): `Docs/agent-api.md` (Z. 310: assembly = .dll/.exe; get_file_tree: effektive Tiefe = maxDepth ?? treeDepth, 0 = Root, summary-Top-Level + maxResults; inspect_assembly/find_assembly_extensions: .dll/.exe-Pfad; receiverType syntaktisch/ordinal/global::), `.agents/rules/AiNetLinter-McpWorkflow.mdc` (DLL → .dll/.exe-Formulierungen). `AiNetLinter.mdc` NICHT betroffen (keine rules.json-Änderung).

## Invarianten, Risiken und Unsicherheiten
- Muss-Kriterium 1: receiverType-Filter ist syntaktisch, ordinal, case-sensitiv; unqualifiziert = `ITypeSymbol.Name` (z. B. „Object“, „String“ — NICHT C#-Keywords „object“/„string“); qualifiziert = Fehlerformat-Name (z. B. „Probe.Extensions.Person“); `global::` wird entfernt; Consumer-Auflösung nur für Applicability im Consumer-Projekt.
- Muss-Kriterium 2: .dll/.exe gleichwertig in allen 4 Validierern; Metadaten-/Existenzprüfung unverändert; Alias-/Namenssuffixe .dll/.exe case-insensitiv entfernbar, andere Endungen bleiben unverändert.
- Muss-Kriterium 3: effectiveDepth = `MaxDepth ?? TreeDepth`; 0 = Root-Ebene; `maxDepth` Vorrang; summary: keine Files, nur Top-Level ≤ 1, maxResults begrenzt Verzeichnisse (TruncatedBy=maxResults), ShownFileCount=0.
- Risiko: Consumer-Applicability-Abdeckung (ReduceExtensionMethod-Pfade) durch Test-Ersatz entfallen — Produktionslogik unverändert (Run-1), kein P0/P1.
- Nicht-Ziele respektiert: kein Decompiler-Body, kein Snapshot-Materializer/Git-Checkout, kein Server-Health-Restrukturierung, keine rules.json-Änderung.
- Fremde Nutzeränderungen (`tasks/decompiled-assembly-fix1/findings1.md`, `findings2.md` gelöscht; `execution-log.md` vom Orchestrator) — unangetastet, keine Commits.

## Verifikation
- `dotnet build`: 0 Warnungen/0 Fehler (nach letzter Teständerung).
- Gezielte FastTests (Filter siehe oben): Final-Lauf nach letzter Codeänderung — siehe Hand-off-Evidenz.
- get_violations (project, C:/Daten/Entwicklung/Ralf/AiNetLinter): Einzeldateien + `src/AiNetLinter`-Scope 0 Violations nach letzter Änderung; MaxLineCount-Befund der Loader-Datei (514 Z.) in Run 2 behoben (498 Z.).
- get_impact (change-context, uncommitted): 6 Dateien, 14 geänderte Symbole, 0 Call-Sites, 0 Violations, 14 Test-Treffer.
- find_duplicates/find_dead_code/find_magic_values (tests/changedOnly): keine neuen Befunde im Änderungsbereich (Magic-Values = etablierte Test-Fixture-Strings).