## Primäre Einstiegspunkte

- Assembly-MCP-Verträge: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`
- Server-Maintenance-Verträge: `src/AiNetLinter/Mcp/Tools/ServerMaintenance/`
- Tool-Registrierung: `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs`

## Betroffene Dateien und Symbole

- `ServerMaintenanceToolRegistrations.AddGetServerHealth`: Request-/Options-Erzeugung und Ausführung in lokale Hilfsmethoden aufgeteilt. Target-/Global-Routing und Cancellation-Verhalten bleiben erhalten.
- `InspectAssemblyTool.BuildResult` (`InspectAssemblyTool.cs:56-66`): Payload-Erzeugung und Response-Budgetierung in `CreatePayload` und `ApplyResponseBudget` extrahiert; Text- und Structured-Content bleiben aus demselben final budgetierten Payload abgeleitet. Die Methode hat weiterhin fünf effektive Parameter und verletzt damit laut `metrics_lookup` `MaxMethodParameterCount` (Limit 4).
- `GetServerHealthResponseBuilder.Build`: Sessionauswahl, Textaufbau und Payloadaufbau getrennt; `HealthResponseData` bündelt den Zwischenzustand. Globales Default bleibt kompakt ohne Sessionliste; Detail-/Sessionoptionen bleiben begrenzt.
- `ReloadConfigTool`: Erfolgreiche Reloads liefern zusätzlich `ReloadConfigPayload`; die bisherige lesbare Zusammenfassung bleibt additiv erhalten.
- `ReloadConfigModels.ReloadConfigPayload`: registrierungsfähiges Structured-Content-DTO mit vorherigem/aktuellem Config-Pfad sowie Rule-Count und Delta.

## Aufrufer und Abhängigkeiten

- `AddGetServerHealth` registriert die MCP-Route und ruft `GetServerHealthTool` auf.
- `GetServerHealthResponseBuilder` projiziert `ServerHealthSnapshot`-Daten in Text und `ServerHealthAggregatePayload`.
- `InspectAssemblyTool` verwendet die bestehende Assembly-Auswahl, Diagnoseprojektion, Referenzdetail-Option und `ProjectResponseBudget`-Grenzen.
- `ReloadConfigTool` verwendet `ConfigLoader`, `McpCodeGraphServer.ReloadConfig` und `ReloadSolutionAsync`; `AddReloadConfig` bleibt die Registrierung.
- `CallTreePayload`, `TypeHierarchyPayload` und `MetricsTreePayload` waren bereits produktiv vorhanden; fokussierte Payload-Assertions wurden in diesem Versuch nicht ergänzt.

## Relevante Tests, Konfiguration und Dokumentation

- Betroffene Tests: `src/AiNetLinter.FastTests/` für Assembly-/CallTree-/TypeHierarchy-/MetricsTree-Verträge sowie `src/AiNetLinter.IntegrationTests/` für Health und ReloadConfig.
- `rules.json` und CLI-Verträge wurden nicht geändert.
- `Konzept.md` und `roadmap.md` wurden nicht geändert. `execution-log.md` war bereits vor diesem Versuch verändert und wurde nicht angefasst.
- Diese Map dokumentiert den aktuellen Korrekturstand; sie dokumentiert keine nachträgliche Testbehauptung.

## Invarianten und offene Kriterien

- Die zwei scopefremden `FindSymbolScanner`-Warnungen bleiben unverändert.
- Runtime umgesetzt, aber nicht vollständig regressionsgesichert: gezielter Inspect-Default ohne Referenzdetails, globales Health-Default ohne Sessionliste, explizites `includeReferences=true`, Diagnose-`totalCount`/`truncatedBy`, `includeSessions`/`maxSessions` und die CallTree-/TypeHierarchy-/MetricsTree-/ReloadConfig-Payloads.
- `InspectAssemblyTool.BuildResult` bleibt trotz erfolgreicher Zeilen-/Komplexitätsrefaktorierung ein aktiver `MaxMethodParameterCount`-Befund (fünf effektive Parameter); der vollständige `get_violations`-Scan meldet ihn derzeit nicht. Die zwei dort gemeldeten `FindSymbolScanner`-Warnungen sind unverändert und scopefremd.
- Kein Sage-/Wire-Nachweis gegen den aktuell gebauten Stand wurde ausgeführt; der externe MCP-Health-Server ist für den Build-Wire nicht als aktualisiert belegt.

## Verifikation dieses Versuchs

- Frischer `dotnet build --no-restore` nach Prüfung der veralteten Hand-off-TRX: erfolgreich, 0 Warnungen / 0 Fehler.
- Frischer gezielter FastTest `InspectAssembly_WithConsumerSolution_ResolvesAssemblyDirectoryDependencies` (`TestResults/package2-review-fast-fresh.trx`): 0/1 bestanden; `AssemblyAnalysisToolTests.cs:282` erwartet bei Type-Filter ohne `includeReferences=true` weiterhin eine Referenz.
- Frische gezielte Health-Tests (`TestResults/package2-review-health-fresh.trx`): 0/2 bestanden; `GetServerHealthToolTests.cs:112` und `:171` erwarten trotz globalem Default ohne `includeSessions=true` weiterhin `payload.Assemblies`.
- `git diff --check`: erfolgreich; nur bestehende Zeilenende-Hinweise.
- MCP `get_violations` mit absolutem Projekt-Target und vollständigem `scopeFilter=src/AiNetLinter/Mcp`: 2 Warnungen ausschließlich in `FindSymbolScanner.cs:40` und `:60`; ein dateigenauer Nachcheck für `InspectAssemblyTool.cs` meldet 0, während `metrics_lookup` für `BuildResult` den Parameterverstoß ausweist.
- MCP-DRY-/Dead-Code-/Magic-Value-Prüfungen: keine Befunde im betroffenen MCP-Produktionsscope während des Versuchs.
- Vollständige Nicht-Stress-Gates beider Testprojekte sowie ein externer Wire-Proof: nicht ausgeführt; die vorhandenen älteren Paket-2-TRX waren vor den Produktionsdateiänderungen datiert und wurden deshalb nicht als frisch gewertet.

## Tech-Debt-Disposition

- Produktionsviolations: Zeilen-/Komplexitätsbefunde behoben, aber `BuildResult` hat weiterhin den per `metrics_lookup` belegten Parameterbefund; kein Produktionscode wurde im Review geändert.
- Paket-2-Regressionstest-Drift: für diesen Korrekturversuch offen/deferred, weiterhin im bestehenden Versuchskontext 1/5; nicht in `tech-debt.md` verschoben.
- Kein Commit erstellt.
