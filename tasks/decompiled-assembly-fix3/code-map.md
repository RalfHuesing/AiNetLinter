## Primäre Einstiegspunkte

- Assembly-MCP-Verträge: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`
- Server-Maintenance-Verträge: `src/AiNetLinter/Mcp/Tools/ServerMaintenance/`
- Tool-Registrierung: `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs`

## Betroffene Dateien und Symbole

- `ServerMaintenanceToolRegistrations.AddGetServerHealth`: Request-/Options-Erzeugung und Ausführung in lokale Hilfsmethoden aufgeteilt. Target-/Global-Routing und Cancellation-Verhalten bleiben erhalten.
- `InspectAssemblyTool.BuildResult` (`InspectAssemblyTool.cs`): Verwendet den internen `InspectAssemblyBuildRequest`-Vertrag; Lease- und Nicht-Lease-Pfade behalten dieselbe Payload-Erzeugung und Response-Budgetierung, die Methode hat keine fünf effektiven Parameter mehr.
- `GetServerHealthResponseBuilder.Build`: Sessionauswahl, Textaufbau und Payloadaufbau getrennt; `HealthResponseData` bündelt den Zwischenzustand. Globales Default bleibt kompakt ohne Sessionliste; Detail-/Sessionoptionen bleiben begrenzt.
- `ReloadConfigTool`: Erfolgreiche Reloads liefern zusätzlich `ReloadConfigPayload`; die bisherige lesbare Zusammenfassung bleibt additiv erhalten.
- `ReloadConfigModels.ReloadConfigPayload`: registrierungsfähiges Structured-Content-DTO mit vorherigem/aktuellem Config-Pfad sowie Rule-Count und Delta.

## Aufrufer und Abhängigkeiten

- `AddGetServerHealth` registriert die MCP-Route und ruft `GetServerHealthTool` auf.
- `GetServerHealthResponseBuilder` projiziert `ServerHealthSnapshot`-Daten in Text und `ServerHealthAggregatePayload`.
- `InspectAssemblyTool` verwendet die bestehende Assembly-Auswahl, Diagnoseprojektion, Referenzdetail-Option und `ProjectResponseBudget`-Grenzen.
- `ReloadConfigTool` verwendet `ConfigLoader`, `McpCodeGraphServer.ReloadConfig` und `ReloadSolutionAsync`; `AddReloadConfig` bleibt die Registrierung.
- `CallTreePayload`, `TypeHierarchyPayload` und `MetricsTreePayload` sind produktiv vorhanden; die fokussierten FastTests prüfen ihre erfolgreichen Structured-Content-Payloads zusätzlich zu den bestehenden Text-/Verhaltensverträgen. `ReloadConfigPayload` wird im erfolgreichen expliziten Reload-Test aus `StructuredContent` deserialisiert und mit den Fixture-Werten 15 vorher, 14 nachher und Delta -1 geprüft.
- `TransitiveCallGraphFormatter.CreateDiagnosticProjection` ist der gemeinsame Diagnosepfad für die Assembly-Call-Graph-Formate; die fokussierte Regression prüft fünf Samples, `totalCount`, `truncated` und `truncatedBy=["maxDiagnostics"]` einschließlich der konsistenten Textprojektion.
- `AssemblyAnalysisToolTests.InspectAssembly_WithConsumerSolution_ResolvesAssemblyDirectoryDependencies` fordert Referenzdetails nun explizit an; `InspectAssembly_TargetedInspectionRequiresExplicitReferenceDetails` deckt Default, `false` und `true` ab.

## Relevante Tests, Konfiguration und Dokumentation

- Betroffene Tests: `src/AiNetLinter.FastTests/` für Assembly-/CallTree-/TypeHierarchy-/MetricsTree-Verträge sowie `src/AiNetLinter.IntegrationTests/` für Health und ReloadConfig.
- `rules.json` und CLI-Verträge wurden nicht geändert.
- `Konzept.md` und `roadmap.md` wurden nicht geändert. `execution-log.md` war bereits vor diesem Versuch verändert und wurde nicht angefasst.
- Diese Map dokumentiert den aktuellen Korrekturstand; sie dokumentiert keine nachträgliche Testbehauptung.

## Invarianten und offene Kriterien

- Die zwei scopefremden `FindSymbolScanner`-Warnungen bleiben unverändert; der vollständige MCP-Produktionsscope enthält zusätzlich die bereits bestehende `AIContextFootprint`-Warnung für `InspectAssemblyTool` (Zeile 17), aber keinen neuen Fehler aus dem `BuildResult`-Refactor.
- Runtime umgesetzt; der gezielte Inspect-Default sowie explizites `includeReferences=false/true` bleiben unverändert. Das globale Health-Default prüft `assemblies=null`, `AssemblyDiagnosticCount=4`, `AssemblyStatusCounts={partial:1}`, `SessionsIncluded=false`, `ShownSessionCount=0`, `SessionsTruncated=false` und leeres `SessionsTruncatedBy`; ein separater Builder-Test prüft eine auf `maxSessions` begrenzte Sessionliste samt Zählern und Trunkierungsgrund.
- `McpServerAssemblyHealthE2ETests` prüft die Default-Auslassung der globalen Sessionliste sowie die explizite `includeSessions=true`-/`maxSessions=1`-Antwort und die Registrierung beider Argumente. Die beiden früheren roten globalen Assertions in `GetServerHealthToolTests` sind damit vertragskonform aktualisiert.
- Die gemeinsamen Diagnose-Samples und die Erfolgspayloads von CallTree, TypeHierarchy, MetricsTree und ReloadConfig sind als fokussierte Regressionen ergänzt; Produktionscode und die bereits korrigierten `includeReferences`-/ReloadConfig-Änderungen wurden nicht verändert.
- Kein Sage-/Wire-Nachweis gegen den aktuell gebauten Stand wurde ausgeführt; der externe MCP-Health-Server ist für den Build-Wire nicht als aktualisiert belegt.

## Verifikation dieses Versuchs

- Nach der letzten Teständerung sind ausschließlich die betroffenen fokussierten Fast-/IntegrationTests sowie `git diff --check` vorgesehen; vollständige Nicht-Stress-Gates und lange MCP-Audits gehören nicht zu diesem Korrekturversuch.
- Zu den weiterhin relevanten früheren Befunden gehören die zwei unveränderten `FindSymbolScanner`-Warnungen sowie die bestehende `AIContextFootprint`-Warnung in `InspectAssemblyTool.cs`; aus den aktuellen Änderungen ist kein Produktionscode betroffen.
- Kein Commit erstellt; `roadmap.md`, `execution-log.md` und `tech-debt.md` wurden in diesem Schritt nicht geändert.

## Tech-Debt-Disposition

- Produktionsviolations: `BuildResult`-Parameterbefund durch den internen Request-Vertrag behoben; die bestehende `AIContextFootprint`-Warnung sowie die scopefremden `FindSymbolScanner`-Warnungen bleiben `accepted-deferred`.
- Paket-2-Regressionstest-Drift: `includeReferences`-Regression bleibt erhalten; Health-Assertions sowie Diagnose-/Session-/vier-Payload-Regressionen sind ergänzt. Die ReloadConfig-Delta-Prüfung verwendet konkrete Fixture-Zähler statt einer tautologischen Ableitung; eine belastbare End-to-End-Diagnoseprüfung über den realen Assembly-Referenzpfad wird in diesem kleinen Testscope nicht erweitert.
- Kein Commit erstellt.
