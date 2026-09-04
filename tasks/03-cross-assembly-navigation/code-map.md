# Code-Map: 03-cross-assembly-navigation

## Primäre Einstiegspunkte
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextTool.cs` & `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`: Test-Scans und Short-Circuit bei Assemblies ohne Testreferenzen (EPIC-01).
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblySearchTool.cs`: Suchfilter `declarationOnly` und `kind` (EPIC-02).
- `src/AiNetLinter/Mcp/Tools/TypeResolution/ResolveTypeOriginTool.cs`: Neues Tool zur Typ-Herkunftsauflösung via `Compilation.References` (EPIC-03).
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/OutgoingCallScanner.cs`: Cross-Assembly Aufruferkennung und BCL-Filterung (EPIC-04).
- `src/AiNetLinter/Mcp/Tools/TypeHierarchy/FindImplementationsTool.cs`: Neues Tool für Interface-/Override-Implementierungen (EPIC-05).

## Betroffene Dateien und Symbole
- `TestDetector.cs`: `HasTestFrameworkReferences`, `IsTestFrameworkReference`, `IsDecompiledAssemblyProject`.
- `TestCoverageBatchScan.cs`: `FindTestsForSymbolsCoreAsync`, `ScanAllTestDocumentsAsync`, `ShouldScanProject`.
- `AssemblySearchTool.cs`: `declarationOnly`, `kind`-Filter in `ExecuteAsync` / `ScanLines`.
- `AssemblySearchDeclarationFilter.cs`: Syntax-basierte Deklarationsprüfung (`InitSyntaxTree`, `FilterDeclarationRanges`, `ResolveCallableHeader`, `ResolveMemberHeader`, `ResolveTypeHeader`).
- `AssemblySearchModels.cs`: Argument- und Match-Records für `AssemblySearchTool`.
- `ResolveTypeOriginTool.cs`: `ResolveTypeOriginRequest`, `ResolveTypeOriginResultDto`.
- `OutgoingCallScanner.cs`: Behandlung externer Symbole (`symbol.ContainingAssembly != compilation.Assembly`), `includeBcl`-Flag.
- `GetCallTreeTool.cs` / `AssemblyGetCallTreeTool.cs` / `CallTreeMermaidRenderer.cs`: Referenzknoten `[ref: Assembly]`.
- `FindImplementationsTool.cs`: `FindImplementationsRequest`, `FindImplementationsResultDto`.
- `SymbolGraphToolRegistrations.cs` / `AssemblyAnalysisToolRegistrations.cs`: MCP-Tool-Registrierungen.

## Aufrufer und Abhängigkeiten
- `McpServerCommand.cs`: Zentrale MCP-Registrierung und Dispatching.
- `AssemblyRegistry.cs` / `AssemblyAnalysisLease.cs`: Zugriff auf geladene Metadaten und Pfade.
- `Roslyn`: `Compilation.References`, `MetadataReference`, `INamedTypeSymbol`, `SymbolFinder`.

## Relevante Tests, Konfiguration und Dokumentation
- `src/AiNetLinter.FastTests/Core/TestCoverageAssemblyShortCircuitTests.cs`: Unit-Tests für Assembly Test-Short-Circuit.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`: FastTests für `AssemblySearchTool` und Kontext.
- `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/`: FastTests für Outgoing-Calls, Typauflösung und Implementierungssuche.
- `src/AiNetLinter.IntegrationTests/Mcp/`: E2E-MCP-Tests für neue Tools und Routen.
- `Docs/configuration.md` & `Docs/agent-api.md`: CLI- und MCP-Tool-Spezifikationen.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`: Toolauswahl-Leitfaden für Coding-Agenten.

## Invarianten, Risiken und Unsicherheiten
- Keine ALC- oder dynamischen Assembly-Loads: Nur Roslyn-Metadaten und dekompilierte ILSpy-Informationen.
- Performance: Typauflösung über `Compilation.References` muss deterministisch und schnell sein (< 100ms), ohne tiefes Rekursieren.
- BCL-Filter: Saubere Ausschlussliste (`System.*`, `Microsoft.NETCore.*`), standardmäßig aktiv um Token-Überflutung zu verhindern.
- Abwärtskompatibilität: Bestehende Tool-Parameter und JSON-Signaturen bleiben kompatibel (neue Parameter optional).

## Verifikation
- Gezielte Unit-Tests je Epic in `AiNetLinter.FastTests`
- `get_violations` nach jeder Codeänderung
- Abschließende FastTests (`Category!=Stress`) und IntegrationTests (`Category!=Stress`)
- `dotnet build` (0 Warnungen, 0 Fehler)
