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
- `CallTreePayload`, `TypeHierarchyPayload` und `MetricsTreePayload` sind produktiv vorhanden; der gezielte 38/38-Lauf prüft deren Text-/Verhaltensverträge, enthält aber keine Structured-Content-Assertions. `ReloadConfigPayload` ist produktiv vorhanden; `ReloadConfigToolTests` prüfen weiterhin nur Text und Serverzustand, nicht `StructuredContent`.
- `AssemblyAnalysisToolTests.InspectAssembly_WithConsumerSolution_ResolvesAssemblyDirectoryDependencies` fordert Referenzdetails nun explizit an; `InspectAssembly_TargetedInspectionRequiresExplicitReferenceDetails` deckt Default, `false` und `true` ab.

## Relevante Tests, Konfiguration und Dokumentation

- Betroffene Tests: `src/AiNetLinter.FastTests/` für Assembly-/CallTree-/TypeHierarchy-/MetricsTree-Verträge sowie `src/AiNetLinter.IntegrationTests/` für Health und ReloadConfig.
- `rules.json` und CLI-Verträge wurden nicht geändert.
- `Konzept.md` und `roadmap.md` wurden nicht geändert. `execution-log.md` war bereits vor diesem Versuch verändert und wurde nicht angefasst.
- Diese Map dokumentiert den aktuellen Korrekturstand; sie dokumentiert keine nachträgliche Testbehauptung.

## Invarianten und offene Kriterien

- Die zwei scopefremden `FindSymbolScanner`-Warnungen bleiben unverändert; der vollständige MCP-Produktionsscope enthält zusätzlich die bereits bestehende `AIContextFootprint`-Warnung für `InspectAssemblyTool` (Zeile 17), aber keinen neuen Fehler aus dem `BuildResult`-Refactor.
- Runtime umgesetzt; der gezielte Inspect-Default sowie explizites `includeReferences=false/true` sind geprüft. Die produktiven CallTree-/TypeHierarchy-/MetricsTree-/ReloadConfig-Payloads existieren, aber die 38/38-Verhaltens- bzw. Reload-Tests belegen ihre Structured-Content-Form nicht. Die fokussierten Regressionen für Diagnose-Samples, `includeSessions`/`maxSessions` und die vier Structured-Content-Erfolgstools wurden in diesem vorzeitig beendeten Versuch nicht erweitert.
- `McpServerAssemblyHealthE2ETests` deckt weiterhin weder `includeSessions`/`maxSessions` noch die Diagnose-Sample-/Trunkierungsfelder end-to-end ab; der gezielte Health-/Reload-Lauf bleibt bei 12/14 wegen der direkten Builder-Assertions in `GetServerHealthToolTests.cs:112` und `:171` rot.
- Die beiden `GetServerHealthToolTests`-Assertions bei den früheren Stellen 112/171 erwarten weiterhin globale Assemblies trotz `includeSessions=false` und bleiben rot; die notwendige Testaktualisierung wurde auf Nutzerwunsch nicht mehr vorgenommen.
- Kein Sage-/Wire-Nachweis gegen den aktuell gebauten Stand wurde ausgeführt; der externe MCP-Health-Server ist für den Build-Wire nicht als aktualisiert belegt.

## Verifikation dieses Versuchs

- Frischer `dotnet build --no-restore` nach Prüfung der veralteten Hand-off-TRX: erfolgreich, 0 Warnungen / 0 Fehler.
- Frischer gezielter Assembly-Testlauf (`TestResults/package2-fix3-fast-assembly-terminal2.trx`): 18/18 bestanden; enthält die aktualisierte Consumer-Assertion und die neue Default/`false`/`true`-Regression.
- Frischer gezielter Payload-Lauf (`TestResults/package2-fix3-fast-payloads.trx`): 38/38 bestanden; bestehende CallTree-/TypeHierarchy-/MetricsTree-Text-/Verhaltensverträge blieben grün, ohne Structured-Content-Assertions.
- Frischer gezielter Health-/Reload-Lauf (`TestResults/package2-fix3-integration-health-reload.trx`): 12/14 bestanden; `GetServerHealthToolTests.cs:112` und `:171` schlagen wegen der bekannten veralteten globalen Sessionassertion fehl.
- `git diff --check`: erfolgreich; nur bestehende Zeilenende-Hinweise.
- MCP `get_violations` mit absolutem Projekt-Target und vollständigem `scopeFilter=src/AiNetLinter/Mcp`: 3 Warnungen – die bestehende `AIContextFootprint`-Warnung in `InspectAssemblyTool.cs:17` sowie die zwei unveränderten `FindSymbolScanner`-Warnungen in `:40` und `:60`; kein neuer Fehler aus dem Refactor. `metrics_lookup` bestätigt für `BuildResult` 1/4 effektive Parameter.
- MCP-DRY-/Dead-Code-/Magic-Value-Prüfungen: keine Befunde im betroffenen MCP-Produktionsscope während des Versuchs.
- Vollständige Nicht-Stress-Gates beider Testprojekte sowie ein externer Wire-Proof: nicht ausgeführt.

## Tech-Debt-Disposition

- Produktionsviolations: `BuildResult`-Parameterbefund durch den internen Request-Vertrag behoben; die bestehende `AIContextFootprint`-Warnung sowie die scopefremden `FindSymbolScanner`-Warnungen bleiben `accepted-deferred`.
- Paket-2-Regressionstest-Drift: `includeReferences`-Regression aktualisiert/ergänzt; Health-Assertions und die geforderten neuen Diagnose-/Session-/vier-Payload-Regressionen bleiben offen (`accepted-deferred` für diesen terminalen Zwischenstand), nicht in `tech-debt.md` verschoben.
- Kein Commit erstellt.
