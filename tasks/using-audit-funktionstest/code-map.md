# Code-Map: Behebung der Usability- & Token-Cost-Findings des AiNetLinter MCP-Servers

## Primäre Einstiegspunkte
- `src/AiNetLinter/Mcp/Tools/CallTree/GetCallTreeTool.cs`: Call-Tree-Logik & Sufficiency-Hint
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs`: Symbol-Auflösung via Datei:Zeile
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs`: Zeilen-Tokens, StableId, DocCommentId
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs`: Projekt-Symbol-Formatierung & ID-Ausgabe
- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs`: Ermittlung von CallSites & umschließenden Methoden
- `src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextFormatter.cs`: Formatierung der FeatureContext-Caller
- `src/AiNetLinter/Core/TestDetector.cs`: Heuristik für zugehörige Testklassen
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs` & `GetFileTreeRenderer.cs`: Tree-Tiefe, Truncation & Completeness
- `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesStringHeuristics.cs` & `FindMagicValuesTool.cs`: Header-Heuristik & CLI-Optionen
- `src/AiNetLinter/Mcp/Registration/DuplicateDetectionToolRegistrations.cs`: Scope-Default
- `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs`: includeReferences Default
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs` & `.Budget.cs`: Budgetgröße & Member-Trimming
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs` & `FindAssemblyExtensionsResponseBuilder.cs`: Teillisten-Truncation
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs`: Diagnostics-Sampling

## Betroffene Dateien und Symbole
- `GetCallTreeTool.ExecuteAsync`
- `FindReferencesTool.ResolveByLineAsync`
- `SymbolIdentifierResolver.ResolveSymbolsOnLine`, `TryResolveByStableIdAsync`, `FindExactStableIdAsync`
- `FindSymbolTool.FormatEntry`
- `DiffImpactAnalyzer.FindCallSiteEntriesAsync`, `CallSiteEntry`
- `FeatureContextFormatter.AppendCallersSection`
- `TestDetector.MatchesTestClassName`
- `GetFileTreeScanner.FileTreeAccumulator`, `BuildTruncationReasons`
- `GetFileTreeRenderer.AppendCompleteness`, `AppendTree`
- `MagicValuesStringHeuristics.ClassifyHeaderIdentifierCandidate`
- `FindMagicValuesToolArgs.MinOccurrences`
- `DuplicateDetectionToolRegistrations.AddFindDuplicates`
- `AssemblyAnalysisToolRegistrations.AddInspectAssembly`
- `AssemblyAnalysisResponseLimits.MaxResponseBytes`, `FitsResponseBudget`, `TryRemoveLastMember`
- `InspectAssemblyFormatter.AppendTypes`
- `FindAssemblyExtensionsResponseBuilder.BuildMarkdown`
- `AssemblyFindSymbolTool.BuildResponseAsync`

## Aufrufer und Abhängigkeiten
- MCP Tool Routes (`AnalysisToolRoute`, `AssemblyToolRoute`)
- MCP CLI Registration & Protocol Handlers
- Client AI Agents (Antigravity, Cursor, etc.) via JSON-RPC

## Relevante Tests, Konfiguration und Dokumentation
- `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/SymbolIdentifierResolverTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/CallTree/GetCallTreeToolTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetFileTreeScannerTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetFileTreeRendererTests.cs`
- `src/AiNetLinter.FastTests/Core/TestDetectorTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimitsBudgetTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/MagicValues/MagicValuesStringHeuristicsTests.cs`
- `Docs/configuration.md`, `Docs/agent-api.md`, `Docs/ROADMAP.md`

## Invarianten, Risiken und Unsicherheiten
- Invariante: Warnungsfreiheit (`TreatWarningsAsErrors = true`).
- Invariante: Safeguard-Score muss auf 10,00/10 bleiben.
- Invariante: Rückwärtskompatibilität der Tool-Schemas (`tools/list`).
- Risiko: Bei Änderung von Limits oder Defaults können bestehende Snapshot-/Vertragstests in `FastTests` angepasst werden müssen.

## Verifikation
- Schnelle Testläufe: `dotnet test src/AiNetLinter.FastTests --filter Category=Unit`
- Vollständige Verifikation: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- MCP-Checks: `safeguard`, `get_violations`, `find_dead_code`, `find_magic_values`, `find_duplicates`
