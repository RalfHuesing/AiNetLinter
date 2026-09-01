## Primäre Einstiegspunkte

- Assembly-MCP-Verträge: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`
- Server-Maintenance-Verträge: `src/AiNetLinter/Mcp/Tools/ServerMaintenance/`
- Tool-Registrierung: `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs`

## Betroffene Dateien und Symbole

- Paket-3-Korrekturversuch: `AssemblySourceFallbackMetadata` wird von `AssemblySourceSelectionOrchestrator` über `AssemblySourceSelectionScope`, `AssemblySourceResolution`, `AssemblyAnalysisContextFactory` und `AssemblyAnalysisRegistryEntryFactory` bis zum `AssemblyOrigin` transportiert; Workspace-/Compilation-Fehler setzen fail-closed `workspace-failure`.
- `AssemblyDecompilationAdapter` erzeugt den Decompiler und die Typ-/Dokumentauswahl; `AssemblyDecompiledBodyResolver` kapselt die leasegebundene on-demand Body-Auflösung inklusive deterministischer Overload-/Parameteridentität. `AssemblyDecompilationSourceText` enthält den Decompiler-Textscanner.
- `IAssemblyBodyContext` entkoppelt den Body-Tool-Renderpfad von der vollständigen Assembly-Lease; `SourceSymbolBodyResolver` kapselt Source-Body-/abstract-/extern-/Interface-Erkennung.
- `AssemblySourceProviderCoordinator` kapselt Provider-Creation, Snapshot-Acquisition, Identity-Merker und Dispose-Lebenszyklus des Orchestrators. `IAssemblySourceSelectionSnapshotRegistry` und `SourceSnapshotLease` liegen in eigenen fachlich kleinen Snapshot-Dateien.

- `ServerMaintenanceToolRegistrations.AddGetServerHealth`: Request-/Options-Erzeugung und Ausführung in lokale Hilfsmethoden aufgeteilt. Target-/Global-Routing und Cancellation-Verhalten bleiben erhalten.
- `InspectAssemblyTool.BuildResult` (`InspectAssemblyTool.cs`): Verwendet den internen `InspectAssemblyBuildRequest`-Vertrag; Lease- und Nicht-Lease-Pfade behalten dieselbe Payload-Erzeugung und Response-Budgetierung, die Methode hat keine fünf effektiven Parameter mehr.
- `GetServerHealthResponseBuilder.Build`: Sessionauswahl, Textaufbau und Payloadaufbau getrennt; `HealthResponseData` bündelt den Zwischenzustand. Globales Default bleibt kompakt ohne Sessionliste; Detail-/Sessionoptionen bleiben begrenzt.
- `ReloadConfigTool`: Erfolgreiche Reloads liefern zusätzlich `ReloadConfigPayload`; die bisherige lesbare Zusammenfassung bleibt additiv erhalten.
- `ReloadConfigModels.ReloadConfigPayload`: registrierungsfähiges Structured-Content-DTO mit vorherigem/aktuellem Config-Pfad sowie Rule-Count und Delta.
- `AssemblyGetCallTreeTool.ExecuteAsync`/`BuildResponseAsync`: Nur der `includeReferences=true`-Zweig ruft `TransitiveCallGraphFormatter.FormatAssemblyCallTreeResponse` mit `AssemblyCallTreeResponseRequest` auf; der Root-only-Zweig delegiert weiterhin an `GetCallTreeTool`.

## Aufrufer und Abhängigkeiten

- `AddGetServerHealth` registriert die MCP-Route und ruft `GetServerHealthTool` auf.
- `GetServerHealthResponseBuilder` projiziert `ServerHealthSnapshot`-Daten in Text und `ServerHealthAggregatePayload`.
- `InspectAssemblyTool` verwendet die bestehende Assembly-Auswahl, Diagnoseprojektion, Referenzdetail-Option und `ProjectResponseBudget`-Grenzen.
- `ReloadConfigTool` verwendet `ConfigLoader`, `McpCodeGraphServer.ReloadConfig` und `ReloadSolutionAsync`; `AddReloadConfig` bleibt die Registrierung.
- `TransitiveCallGraphFormatter.FormatResponse` ist der Response-Einstieg für `FindReferencesTool`, `AssemblyFindReferencesTool` und den Symbol-Branch von `GetImpactTool`; `Format` delegiert dorthin. `TransitiveCallGraphFormatter.FormatAssemblyCallTreeResponse` ist der gemeinsame, interne Assembly-CallTree-Responseformatter; `AssemblyGetCallTreeTool` übergibt dafür den `AssemblyCallTreeResponseRequest` und projiziert Diagnosen nicht selbst. Der Formatter-Pfad gilt für Assembly-CallTree-Aufrufe mit `includeReferences=true`; Root-only bleibt beim bestehenden `CallTreePayload`-Pfad.
- `CallTreePayload`, `TypeHierarchyPayload` und `MetricsTreePayload` sind produktiv vorhanden; die fokussierten FastTests prüfen ihre erfolgreichen Structured-Content-Payloads zusätzlich zu den bestehenden Text-/Verhaltensverträgen. `ReloadConfigPayload` wird im erfolgreichen expliziten Reload-Test aus `StructuredContent` deserialisiert und mit den Fixture-Werten 17 vorher, 16 nachher und Delta -1 geprüft.
- `TransitiveCallGraphFormatter.ProjectDiagnostics` vereinigt `Completeness.Diagnostics` und `Navigation.Diagnostics`, projiziert diese Eingangsmenge genau einmal und weist dieselbe Sample-/Zählerprojektion beiden Feldern zu. `FormatAssemblyCallTreeResponse` projiziert Navigation plus zusätzliche CallTree-Diagnosen ebenfalls genau einmal und erzeugt Text sowie `AssemblyCallTreeResult` daraus. Formatter- und Assembly-Route-Regressionen prüfen No-Hit-Metadaten, fünf Samples, `totalCount`, `shownCount`, `truncated`, `truncatedBy=["maxDiagnostics"]`, Text/Structured-Content-Gleichheit und den Ausschluss des sechsten kontrollierten Samples; die Assembly-Abdeckung umfasst nun auch den realen `get_call_tree`-Diagnosepfad.
- `AssemblyAnalysisToolTests.InspectAssembly_WithConsumerSolution_ResolvesAssemblyDirectoryDependencies` fordert Referenzdetails nun explizit an; `InspectAssembly_TargetedInspectionRequiresExplicitReferenceDetails` deckt Default, `false` und `true` ab.

## Relevante Tests, Konfiguration und Dokumentation

- Betroffene Tests: `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/TransitiveCallGraphFormatterTests.cs` für den direkten No-Hit-/Assembly-Formattervertrag, `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyNavigationResponseContractTests.cs` bündelt die Diagnoseassertions für Assembly-`find_references` und `get_call_tree`, und `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRouteTests.cs` deckt den echten `get_call_tree(includeReferences=true)`-Diagnosepfad mit kontrollierten sechs fehlenden Referenzen ab; `src/AiNetLinter.IntegrationTests/` bleibt für Health und ReloadConfig relevant.
- `rules.json` und CLI-Verträge wurden nicht geändert.
- `Konzept.md` wurde nicht geändert; `roadmap.md` und `execution-log.md` enthalten nachgelagerte Review-/Checkpoint-Metadaten und gehören nicht zum Produktions- oder Testumfang.
- Diese Map dokumentiert den aktuellen Korrekturstand; sie dokumentiert keine nachträgliche Testbehauptung.

## Invarianten und offene Kriterien

- Die zwei scopefremden `FindSymbolScanner`-Warnungen bleiben unverändert; der vollständige MCP-Produktionsscope enthält zusätzlich die bereits bestehende `AIContextFootprint`-Warnung für `InspectAssemblyTool` (Zeile 17), aber keinen neuen Fehler aus dem `BuildResult`-Refactor.
- Runtime umgesetzt; der gezielte Inspect-Default sowie explizites `includeReferences=false/true` bleiben unverändert. Das globale Health-Default prüft `assemblies=null`, `AssemblyDiagnosticCount=4`, `AssemblyStatusCounts={partial:1}`, `SessionsIncluded=false`, `ShownSessionCount=0`, `SessionsTruncated=false` und leeres `SessionsTruncatedBy`; ein separater Builder-Test prüft eine auf `maxSessions` begrenzte Sessionliste samt Zählern und Trunkierungsgrund.
- `McpServerAssemblyHealthE2ETests` prüft die Default-Auslassung der globalen Sessionliste sowie die explizite `includeSessions=true`-/`maxSessions=1`-Antwort und die Registrierung beider Argumente. Die beiden früheren roten globalen Assertions in `GetServerHealthToolTests` sind damit vertragskonform aktualisiert.
- Die gemeinsamen Diagnose-Samples und die Erfolgspayloads von CallTree, TypeHierarchy, MetricsTree und ReloadConfig sind als fokussierte Regressionen ergänzt; der aktuelle Produktionspatch verlagert die Assembly-CallTree-Diagnoseprojektion in `TransitiveCallGraphFormatter` und erhält den bestehenden `AssemblyCallTreeResult`-Wirevertrag.
- Kein Sage-/Wire-Nachweis gegen den aktuell gebauten Stand wurde ausgeführt; der externe MCP-Health-Server ist für den Build-Wire nicht als aktualisiert belegt.

## Verifikation dieses Versuchs

- Frische Review-Verifikation: fokussierte FastTests 50/50, fokussierte IntegrationTests 19/19, `dotnet build --no-restore` 0 Warnungen/0 Fehler und `git diff --check` grün. Der Produktionsscope-`get_violations`-Check meldet 3 bekannte unabhängige Warnungen; der FastTests-MCP-Scope meldet den bestehenden `AssemblyAnalysisToolTests`-Zeilenbefund sowie die durch die neue Assembly-Testdatei ausgelöste Verzeichnisgrenze; der IntegrationTests-MCP-Scope ist sauber. Vollständige Nicht-Stress-Gates und weitere Audits wurden in diesem Review nicht erneut gestartet.
- Zu den weiterhin relevanten früheren Befunden gehören die zwei unveränderten `FindSymbolScanner`-Warnungen sowie die bestehende `AIContextFootprint`-Warnung in `InspectAssemblyTool.cs`; aus den aktuellen Änderungen ist kein Produktionscode betroffen.
- Kein Commit erstellt; `roadmap.md`, `execution-log.md` und `tech-debt.md` wurden in diesem Schritt nicht geändert.

## Tech-Debt-Disposition

- Produktionsviolations: `BuildResult`-Parameterbefund durch den internen Request-Vertrag behoben; die bestehende `AIContextFootprint`-Warnung sowie die scopefremden `FindSymbolScanner`-Warnungen bleiben `accepted-deferred`.
- Paket-2-Regressionstest-Drift: `includeReferences`-Regression bleibt erhalten; Health-Assertions sowie Diagnose-/Session-/vier-Payload-Regressionen sind ergänzt. Die ReloadConfig-Delta-Prüfung verwendet konkrete Fixture-Zähler 17 vorher und 16 nachher statt einer tautologischen Ableitung; eine belastbare End-to-End-Diagnoseprüfung über den realen Assembly-Referenzpfad wird in diesem kleinen Testscope nicht erweitert.
- Kein Commit erstellt.

## Paket 3 – Source-Backing und Body-/Metadata-Navigation

- `ExternalSourceSnapshotMaterializer.OpenSolutionAsync` (`src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceSnapshotMaterializer.cs`): Workspace-Diagnosen werden begrenzt am `ExternalSourceSnapshot` erhalten. Eine Solution ohne Projekte oder ohne nutzbare C#-Dokumente bleibt ein Materialisierungsfehler; verwertbare Dokumente bleiben trotz Workspace-Diagnosen nutzbar.
- `AssemblySourceSelectionOrchestrator.ResolveAsync` (`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs`): Konfigurations-, Mapping-, Provider-, Snapshot- und Workspace-Fälle liefern stabile `AssemblySourceFallbackReasons` sowie sichere Diagnosen. Provider-/Workspace-Ausnahmen werden fail-closed in einen dekompilierten Fallback überführt; ein `AdhocWorkspace` wird nur für den unabhängigen dekompilierten Roslyn-Snapshot verwendet. Mapping-Vorbereitung und Scope-Diagnosen sind in benannte Helper ausgelagert.
- `AssemblyOrigin`/`AssemblyAnalysisResponse.Enrich`: `fallbackReason`, `bodyAvailability`, `contentMode` und eine gekürzte Source-Diagnosesummary werden am Origin geführt und in Header sowie Structured Content projiziert. Source-backed bleibt `source`; der initiale Decompiler bleibt `decompiledSignatureOnly` mit `onDemand`-Bodyverfügbarkeit.
- `AssemblyAnalysisLease.ResolveBodyAsync`, `AssemblyDecompilationAdapter.CreateBodyResolver`, `AssemblyDecompiledBodyResolver`, `AssemblyBodySyntax` und `GetSymbolBodyTool`: Assembly-Bodies werden ausschließlich innerhalb eines aktiven Leases on demand dekompiliert. Source-backed nutzt weiterhin Roslyn-Syntax; abstract/extern/interface/nicht verfügbare Symbole liefern explizite Hinweise. Deadline, Cancellation und `maxBodyLines` begrenzen den Resolver; interne Cachepfade werden nicht ausgegeben. Die Property-Body-Prüfung liegt einmalig in `AssemblyBodySyntax.HasNoBody`.
- `CSharpLiteralFormatter` (`src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`): `HasConstantValue` wird zentral und invariant in `get_class_structure` formatiert; der gezielte FastTest deckt Zahlen, Strings, Zeichen und boolesche Literalwerte ab.
- Gezielt verifiziert: 75 Paket-3-FastTests (inklusive `AssemblyAnalysisPathContractTests` mit vollständiger Overload-Identität, Source-/Fallback-/Lease-Support und Literalregression), 6 `ExternalSourceSnapshotMaterializerTests`, Build ohne Warnungen und `git diff --check`. Danach wurden die DRY-/Dead-Code-/Magic-Value-Audits und der finale betroffene MCP-`get_violations`-Check ausgeführt.
- Die frühere Review-Navigation zu Source-Context-/Compilation-Fallback und Overload-Auflösung ist umgesetzt: `AssemblyAnalysisContextFactory` bündelt Workspace-Diagnosen weiter im Fallback; `AssemblyDecompiledBodyResolver` vergleicht vollständige Parameter-/Ref-/Typidentität.
- `AssemblyAnalysisContextFactory` nutzt für Project-Compilation, Source-Context-Aufbau und Fallback-Vorbereitung benannte interne Parameterobjekte/Helper; Source- und Fallback-Ownership bleibt unverändert.
- Paket-3-Korrekturversuch 2: die bestätigten strukturellen Befunde der vier Zielbereiche sind im finalen MCP-Check bereinigt. Roadmap, Execution-Log und Tech-Debt wurden nicht geändert; kein Commit erstellt.
