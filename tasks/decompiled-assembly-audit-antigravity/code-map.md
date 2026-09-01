# Code-Map: Decompiled Assembly Support

## Primäre Einstiegspunkte

- Assembly-Only-MCP-Werkzeuge: `inspect_assembly` und `find_assembly_extensions` (`src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs`).
- Zentraler Assembly-Target-Dispatch: `AnalysisToolCall.ExecuteRouted` und `AssemblyAnalysisDispatcher` (`src/AiNetLinter/Mcp/AnalysisToolCall.cs`).
- Residente Assembly-Registry & Host: `AssemblyAnalysisRegistry` und `AssemblyAnalysisHostComposition` (`src/AiNetLinter/Mcp/Assemblies/Analysis/`).
- Gemeinsame Roslyn-Session: `AssemblyAnalysisSession` (`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs`).
- Folgeabfragen auf Assembly-Snapshots: `find_symbol`, `get_symbol_body`, `find_references`, `get_call_tree`, `get_type_hierarchy`, `dependency_graph`, `get_namespace_tree`, `get_file_skeleton`, `get_class_structure`, `metrics_lookup`, `metrics_tree`.

## Betroffene Komponenten und Dateien

- `src/AiNetLinter/Mcp/Assemblies/Analysis/`:
  - `AssemblyDecompilationAdapter.cs`: ICSharpCode.Decompiler-Integration, Type-Selection, Document-Generierung.
  - `AssemblyDecompilationCache.cs`: Atomares Generation-Publishing, Pointer-Management, Cache-Storage.
  - `AssemblyReferenceResolver.cs`: PE-Metadatenleser, Referenzgraph-Traversierung, Trusted-Platform-Auflösung.
  - `AssemblyRoslynWorkspaceFactory.cs`: AdhocWorkspace, synthetisches C#-Projekt, Referenz-Injektion.
  - `AssemblyAnalysisRegistry.cs`: Leasing, Eviction, Resident-Count, Cache-Zusammenführung.
  - `AssemblyDiagnosticCodes.cs`: Diagnose-Codes für Assembly- und Referenzanalyse.
  - `Bodies/AssemblyDecompiledBodyResolver.cs`: On-Demand-Body-Dekomposition für Symbole.
  - `References/AssemblyReferenceNavigator.cs`: Referenz-Traversierung über bounded Sessions.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/`:
  - `ExternalResourceRegistry.cs`: Budget-, TTL- und Slot-Verwaltung für externe Ressourcen.
  - `ExternalSourceConfigurationLoader.cs`: `appsettings.json`-Parser für `ExternalSources`.
  - `Snapshots/SourceSnapshotRegistry.cs`: Source-Backed Checkout- und Workspace-Snapshot-Verwaltung.
  - `Repository/ExternalSourceRepositoryPathGuard.cs`: Pfad- und Reparse-Point-Validierung.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`:
  - `AssemblyAnalysisService.cs`: Dienstschicht für `inspect_assembly` und `find_assembly_extensions`.
  - `AssemblyAnalysisResponseLimits.cs` & `.Budget.cs`: 8-KB-Response-Budget, Diagnose-Sampling, Member-/Typ-Trunkierung.
  - `InspectAssemblyFormatter.cs`: Markdown-Projektion für `inspect_assembly`.
  - `Responses/InspectAssemblyResponseBuilder.cs`: DTO-Projektion für `structuredContent`.

## Aufrufer- und Datenfluss

1. MCP-Toolaufruf (`inspect_assembly` oder Folgeabfrage mit `targetType="assembly"`)
2. `AnalysisTargetResolver` normalisiert Pfad und prüft Existenz.
3. `AnalysisToolCall.ExecuteRouted` routet an `AssemblyAnalysisRegistry.LeaseAsync`.
4. `AssemblyAnalysisRegistry` holt oder erzeugt `AssemblyAnalysisSession` (über Fingerprint).
5. `AssemblyAnalysisSession.RefreshAsync` prüft Cache (`AssemblyDecompilationCache.TryRead`) oder startet `AssemblyDecompilationAdapter.DecompileAsync` + `AssemblyReferenceResolver.Resolve`.
6. `AssemblyRoslynWorkspaceFactory.CreateAsync` baut AdhocWorkspace und `Compilation`.
7. `AssemblyAnalysisService` oder Symbolgraph-Tool wertet Roslyn-Snapshot aus.
8. `AssemblyAnalysisResponseLimits.ProjectResponseBudget` kürzt Payload auf 8-KB-Limit.
9. `McpToolResults` liefert kombiniertes Markdown und `structuredContent`.

## Invarianten und Sicherheitsgrenzen

- Metadata-only: Zielassemblies werden statisch gelesen; kein dynamisches Laden via `AssemblyLoadContext` oder Reflection-Ausführung.
- Absolute Pfade: Verpflichtende kanonische Pfade mit PathGuard-Validierung gegen Path-Traversal und unsichere Reparse-Points.
- Fail-Closed: Nicht-.NET-PE-Dateien (`FALSE-01`) und korrupte Assemblies werden mit strukturierten Diagnosen recoverable abgewiesen.
- Privacy & Redaction: Keine externen Kundennamen, Pfade oder Symbolsignaturen in versionierten Artefakten. Opake Labels `GIT-01`, `LOCAL-01`, `LOCAL-02`, `LOCAL-03`, `FALSE-01`.
