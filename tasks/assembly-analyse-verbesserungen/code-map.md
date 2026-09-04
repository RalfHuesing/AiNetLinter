## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Composition/McpServerToolCollectionFactory.cs` baut die MCP-Tool-Collection.
- `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs` registriert die Assembly-only-Tools einschließlich `get_assembly_context`.
- `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs` und `FileStructureToolRegistrations.cs` routen Assembly-fähige Navigation/Strukturtools einschließlich `get_file_tree`.
- `src/AiNetLinter/Mcp/AnalysisToolCall.cs` stellt die gemeinsame Projekt-/Assembly-Zielroute bereit.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs`: `InspectAssemblyPayload`, `FindAssemblyExtensionsPayload`, Paging-Envelope und stabile Assembly-Signatur-/Member-IDs.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs`: trimmt Inspect-/Extension-Projektionen und rekonstruiert Paging-/Count-/Truncation-Felder aus der tatsächlich zurückgegebenen Projektion und dem Caller-Offset.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/*ResponseBuilder.cs`: übergibt den gelesenen Cursor-Offset an die Budgetprojektion, damit Folgecursor keine Ergebnisse überspringen.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextTool.cs`: wendet `topN` auf Caller-/Impact-Auswahl an und übergibt den Composite-Envelope vor dem Dispatcher-Enrichment an denselben gemeinsamen Wire-Trim; optionale Sektionen erhalten bei Entfernung Status, Truncation und Detail-/Paging-Hinweis.
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`, `SourceSymbolBodyResolver.cs`, `Assemblies/Analysis/Bodies/IAssemblyBodyContext.cs`, `Assemblies/Analysis/References/AssemblyAnalysisLease.cs`: leiten Body-Provenienz aus dem tatsächlichen Assembly-Lease-Kontext ab.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs`: letzter zentraler Wire-Trim für alle Assembly-Session-Routen; misst Text, Structured Content und Wire-Metadaten gemeinsam, synchronisiert die Wire-Metadaten nach dem finalen Trim und lehnt nicht repräsentierbare Budgets unterhalb der dokumentierten Mindestgröße recoverable ab. Nicht registrierte Arrays werden nicht als generische Properties entfernt; für `parameters`, `attributes`, `genericParameters` und `constraints` werden bei tatsächlicher Kürzung additive Array-Envelopes mit Counts, Grund, Cursor und Detailhinweis erzeugt.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisResponse.UnknownArrays.cs`: fokussierter namespace-konformer Helper für die verlustsichtbare Kürzung der unbekannten Signatur-Arrays; hält den zentralen Wire-Trim unter dem Datei-/Komplexitätslimit.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponseEnvelope.cs`, `AssemblyAnalysisResponseRequest.cs`: kapseln die route-spezifische Rekonstruktion für `types`/`extensions`, fileTree-Dateien/-Verzeichnisse, callSites, Body-Results, Members, Referenzen, Diagnostics, Samples und Namespaces sowie das Outer-/Inner-Composite-Merging. Für fileTree-Dateien werden Completeness, Counts, Truncation-Grund und Cursor aktualisiert; für Verzeichnisse zusätzlich getrennte Top-Level-/Completeness-Felder und Detailhinweise.
- `src/AiNetLinter/Mcp/AnalysisTarget.cs`, `AnalysisToolCall.cs`: tragen `maxResponseBytes`, `detailLevel` und `cursor` bis zum finalen Assembly-Enricher weiter; explizit befüllt werden diese Optionen derzeit in den Registrierungen für `inspect_assembly`, `find_assembly_extensions` und `get_assembly_context`.
- `src/AiNetLinter/Mcp/AssemblyAnalysisExecutionOptions.cs`: bündelt Dispatcher-Optionen einschließlich CancellationToken und verhindert Parameterdrift.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/ResponseBudgetOptions.cs`: interne, sichtbare Projektionsträgerstruktur für Response-Budget und Cursor-Offset.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs`, `AssemblyFindReferencesTool.cs`, `AssemblySymbolResolver.cs`, `AssemblyReferenceNavigator.cs`, `TransitiveCallGraphModels.cs` sowie `src/AiNetLinter/Mcp/Tools/CallTree/AssemblyGetCallTreeTool.cs`: Assembly-Symbolauflösung, Navigation, Scope-/Truncation-Metadaten.
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTree*.cs` und `AssemblyGetFileTreeTool.cs`: physischer Dateibaum für Projekte sowie den Source-/Decompiler-Root einer Assembly-Session.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/*ResponseBuilder.cs`: strukturierte Assembly-Antworten und Textprojektion.

## Aufrufer und Abhängigkeiten

- `McpServerToolCollectionFactory` → Registrar → `AnalysisToolCall` → `AssemblyAnalysisDispatcher` → `IAssemblyAnalysisRegistry`/`AssemblyAnalysisLease`.
- Assembly-Navigation nutzt denselben Lease-Snapshot; `includeReferences` öffnet bounded Referenz-Sessions.
- `get_file_tree` nutzt die gemeinsame Target-Route; Projekt-Ziele scannen den Projektroot, Assembly-Ziele den verifizierten Source-/Decompiler-Root der Lease-Session und liefern andernfalls `unsupported`.
- `get_assembly_context` bündelt den Assembly-Inspect-Payload und optionale Folgeanalysen; der gemeinsame Dispatcher ergänzt den Herkunfts-/Status-Envelope.

## Relevante Tests, Konfiguration und Dokumentation

- Konzept-Vertrag: `tasks/assembly-analyse-verbesserungen/Konzept.md`.
- Abschluss-Gates: `src/AiNetLinter.FastTests`, `src/AiNetLinter.IntegrationTests`, `dotnet build`.
- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md` sind die maßgeblichen MCP-/Konfigurationsverträge; nur unmittelbar geänderte Assembly-Verträge aktualisieren.
- Regressionen: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`, `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/` und `src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerAssemblyHealthE2ETests.cs`; `AssemblyAnalysisDispatcherCapabilityTests.ResponseBudget.cs` enthält den finalen 4096-Byte-Wire-/Cursor- und Composite-Envelope-Nachweis, die Fixture liegt wegen der Längenregel in `AssemblyAnalysisDispatcherCapabilityTests.Fixture.cs`.

## Invarianten, Risiken und Unsicherheiten

- Externe Assemblies und Repositories bleiben read-only.
- Source darf nur bei verifiziertem Mapping/Checkout als Originalquelle ausgewiesen werden.
- Read-only-Analyse und externe Source-Repositories bleiben unverändert; Source-/Cache-/Mehrdaemon-Lifecycle ist Epic 2/3 und wird hier nicht umgebaut.
- Bestehende CLR-/Wire-Felder (`Results`, `ShownCount`, `Truncated`, `Navigation`, `analysis`) bleiben kompatibel; neue kanonische Felder müssen additiv und nicht redundant serialisiert werden.
- `get_assembly_context` ist registriert und per Wiring-/Live-Tool-Contract sichtbar; `inspect_assembly` und Extensions liefern additive `totalCount`/`returnedCount`/`isTruncated`/`continuationToken`-Felder.
- Erfolgreiche Assembly-Wire-Envelopes überschreiten ihr effektives `maxResponseBytes` nicht; ein explizites Budget unter `MinimumResponseBytes` liefert stattdessen einen recoverable `INVALID_ARGUMENT` ohne unvollständigen Envelope.
- Für die bereits registrierten Ergebnisarrays werden Counts, Completeness-/Truncation-Gründe und — soweit der jeweilige Route-Vertrag dies vorsieht — Fortsetzungstoken nach dem finalen Trim rekonstruiert. `fileTree.directories` besitzt zusätzlich einen eigenen Directory-Envelope in Top-Level und `completeness`; nicht registrierte Arrays werden vor generischer Property-Entfernung geschützt und die relevanten Symbolsignatur-Arrays erhalten additive, vollständige Truncation-Envelopes. Entfernte Composite-Sektionen bleiben als Status-/Detail-Hinweis oder über ihre verschachtelten Ergebnis-Envelopes adressierbar.

## Verifikation

- MCP-first: `get_file_tree(summary)`, `get_index_scope`, `find_symbol` und `get_feature_context` für Factory, Response-Limits, Registrierungen und Navigation ausgeführt; Root-Tree war wegen `maxDepth` absichtlich gekürzt.
- Fast-Regressionen decken Budget-Konfiguration, stabile IDs, Cursor-Paging, fileTree-Datei- und -Verzeichnis-Counts, unbekannte Signatur-Arrays mit vollständigem Array-Envelope, callSites-/Body-Results-Counts, Outer-/Inner-Composite-Status und strukturierte Bodies ab. Die Dispatcher-Regressionen prüfen nach dem finalen Trim tatsächliche Counts/Continuation bei 4096 Byte, Mindestbudget-Ablehnung, Aufrufbudget gegenüber Lease-Default, die gemeinsame Text-/Structured-Gesamtgröße sowie den Body-Detailhinweis; `topN` und Body-Provenienz bleiben regressionsgesichert. Die abschließenden Audit- und Testnachweise werden im terminalen Bericht dieses Korrekturlaufs geführt.
