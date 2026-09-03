## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Composition/McpServerToolCollectionFactory.cs` baut die MCP-Tool-Collection.
- `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs` registriert die Assembly-only-Tools einschließlich `get_assembly_context`.
- `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs` und `FileStructureToolRegistrations.cs` routen Assembly-fähige Navigation/Strukturtools einschließlich `get_file_tree`.
- `src/AiNetLinter/Mcp/AnalysisToolCall.cs` stellt die gemeinsame Projekt-/Assembly-Zielroute bereit.

## Betroffene Dateien und Symbole

- `Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs`: `InspectAssemblyPayload`, `FindAssemblyExtensionsPayload`, Paging-Envelope und stabile Assembly-Signatur-/Member-IDs.
- `Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits*.cs`: Diagnose-/Referenz-Projektion, konfigurierbares 16-KiB-Standardbudget und harte technische 32-KiB-Grenze.
- `Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextTool.cs`: kompakter Composite mit optionalen Metrics, Referenzen, Caller/Impact, Body und Klassenstruktur.
- `Mcp/Tools/GetSymbolBodyTool.cs`: additive strukturierte Body-Batch-Ergebnisse mit stabiler ID, relativer Position, Herkunftsmodus und Truncation-Flag.
- `Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs`: gemeinsamer `analysis`-Envelope für Folgeaufrufe.
- `Mcp/Tools/SymbolGraph/Assembly*` und `TransitiveCallGraphModels.cs`: Assembly-Symbolauflösung, Navigation, Scope-/Truncation-Metadaten.
- `Mcp/Tools/FileStructure/GetFileTree*.cs` und `AssemblyGetFileTreeTool.cs`: physischer Dateibaum für Projekte sowie den Source-/Decompiler-Root einer Assembly-Session.
- `Mcp/Tools/AssemblyAnalysis/Responses/*ResponseBuilder.cs`: strukturierte Assembly-Antworten und Textprojektion.

## Aufrufer und Abhängigkeiten

- `McpServerToolCollectionFactory` → Registrar → `AnalysisToolCall` → `AssemblyAnalysisDispatcher` → `IAssemblyAnalysisRegistry`/`AssemblyAnalysisLease`.
- Assembly-Navigation nutzt denselben Lease-Snapshot; `includeReferences` öffnet bounded Referenz-Sessions.
- `get_file_tree` nutzt die gemeinsame Target-Route; Projekt-Ziele scannen den Projektroot, Assembly-Ziele den verifizierten Source-/Decompiler-Root der Lease-Session und liefern andernfalls `unsupported`.
- `get_assembly_context` bündelt den Assembly-Inspect-Payload und optionale Folgeanalysen; der gemeinsame Dispatcher ergänzt den Herkunfts-/Status-Envelope.

## Relevante Tests, Konfiguration und Dokumentation

- Konzept-Vertrag: `tasks/assembly-analyse-verbesserungen/Konzept.md`.
- Abschluss-Gates: `src/AiNetLinter.FastTests`, `src/AiNetLinter.IntegrationTests`, `dotnet build`.
- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md` sind die maßgeblichen MCP-/Konfigurationsverträge; nur unmittelbar geänderte Assembly-Verträge aktualisieren.
- Regressionen: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`, `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/` und `.../Mcp/Tools/McpServerAssemblyHealthE2ETests.cs`.

## Invarianten, Risiken und Unsicherheiten

- Externe Assemblies und Repositories bleiben read-only.
- Source darf nur bei verifiziertem Mapping/Checkout als Originalquelle ausgewiesen werden.
- Read-only-Analyse und externe Source-Repositories bleiben unverändert; Source-/Cache-/Mehrdaemon-Lifecycle ist Epic 2/3 und wird hier nicht umgebaut.
- Bestehende CLR-/Wire-Felder (`Results`, `ShownCount`, `Truncated`, `Navigation`, `analysis`) bleiben kompatibel; neue kanonische Felder müssen additiv und nicht redundant serialisiert werden.
- `get_assembly_context` ist registriert und per Wiring-/Live-Tool-Contract sichtbar; `inspect_assembly` und Extensions liefern additive `totalCount`/`returnedCount`/`isTruncated`/`continuationToken`-Felder.

## Verifikation

- MCP-first: `get_file_tree(summary)`, `get_index_scope`, `find_symbol` und `get_feature_context` für Factory, Response-Limits, Registrierungen und Navigation ausgeführt; Root-Tree war wegen `maxDepth` absichtlich gekürzt.
- Fast-Regressionen decken Budget-Konfiguration, stabile IDs, Cursor-Paging und strukturierte Bodies ab; Live-Contract-Regression deckt Tool-Anzahl/-Gruppierung sowie den Composite-Daemon-Call ab. Final geprüft: Build/Fast- und relevante Integrationstests grün; DRY meldet vier bestehende Budget-Parallelcluster als P2, Dead-Code und Magic Values 0, letzter `get_violations`-Check im Assembly-Scope 0.
