## Primäre Einstiegspunkte

- Assembly-MCP-Verträge: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`
- Server-Maintenance-Verträge: `src/AiNetLinter/Mcp/Tools/ServerMaintenance/`
- Tool-Registrierung: `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs`

## Betroffene Dateien und Symbole

- `ServerMaintenanceToolRegistrations.AddGetServerHealth`: Request-/Options-Erzeugung und Ausführung in lokale Hilfsmethoden aufgeteilt. Target-/Global-Routing und Cancellation-Verhalten bleiben erhalten.
- `InspectAssemblyTool.BuildResult`: Payload-Erzeugung und Response-Budgetierung in `CreatePayload` und `ApplyResponseBudget` extrahiert; Text- und Structured-Content bleiben aus demselben final budgetierten Payload abgeleitet.
- `GetServerHealthResponseBuilder.Build`: Sessionauswahl, Textaufbau und Payloadaufbau getrennt; `HealthResponseData` bündelt den Zwischenzustand. Globales Default bleibt kompakt ohne Sessionliste; Detail-/Sessionoptionen bleiben begrenzt.
- `ReloadConfigTool`: Erfolgreiche Reloads liefern zusätzlich `ReloadConfigPayload`; die bisherige lesbare Zusammenfassung bleibt additiv erhalten.
- `ReloadConfigModels.ReloadConfigPayload`: registrierungsfähiges Structured-Content-DTO mit vorherigem/aktuellem Config-Pfad sowie Rule-Count und Delta.

## Aufrufer und Abhängigkeiten

- `AddGetServerHealth` registriert die MCP-Route und ruft `GetServerHealthTool` auf.
- `GetServerHealthResponseBuilder` projiziert `ServerHealthSnapshot`-Daten in Text und `ServerHealthAggregatePayload`.
- `InspectAssemblyTool` verwendet die bestehende Assembly-Auswahl, Diagnoseprojektion, Referenzdetail-Option und `ProjectResponseBudget`-Grenzen.
- `ReloadConfigTool` verwendet `ReloadConfigService`; `AddReloadConfig` bleibt die Registrierung.
- `CallTreePayload`, `TypeHierarchyPayload` und `MetricsTreePayload` waren bereits produktiv vorhanden; fokussierte Payload-Assertions wurden in diesem Versuch nicht ergänzt.

## Relevante Tests, Konfiguration und Dokumentation

- Betroffene Tests: `src/AiNetLinter.FastTests/` für Assembly-/CallTree-/TypeHierarchy-/MetricsTree-Verträge sowie `src/AiNetLinter.IntegrationTests/` für Health und ReloadConfig.
- `rules.json` und CLI-Verträge wurden nicht geändert.
- `Konzept.md` und `roadmap.md` wurden nicht geändert. `execution-log.md` war bereits vor diesem Versuch verändert und wurde nicht angefasst.
- Diese Map dokumentiert den aktuellen Korrekturstand; sie dokumentiert keine nachträgliche Testbehauptung.

## Invarianten und offene Kriterien

- Die zwei scopefremden `FindSymbolScanner`-Warnungen bleiben unverändert.
- Ausstehend: gezielter Inspect-Default ohne Referenzdetails, globales Health-Default ohne Sessionliste, sowie fokussierte Assertions für `includeReferences`, Diagnose-`totalCount`/`truncatedBy`, `includeSessions`/`maxSessions` und die CallTree-/TypeHierarchy-/MetricsTree-/ReloadConfig-Payloads.
- Kein Sage-/Wire-Nachweis gegen den aktuell gebauten Stand wurde in diesem Hand-off ausgeführt.

## Verifikation dieses Versuchs

- `dotnet build --no-restore`: erfolgreich, 0 Warnungen / 0 Fehler.
- Gezielte FastTests: 55/56 bestanden; ein bestehender Test erwartet noch die alte Inspect-Referenzdetail-Voreinstellung.
- Gezielte IntegrationTests: 10/12 bestanden; zwei bestehende Health-Tests erwarten noch eine globale Sessionliste bei Default-/Diagnoseabfrage.
- `git diff --check`: erfolgreich; nur bestehende Zeilenende-Hinweise.
- MCP `get_violations` über `src/AiNetLinter/Mcp`: keine Violations an den geänderten Symbolen; genau die zwei bekannten `FindSymbolScanner`-Warnungen bleiben.
- MCP-DRY-/Dead-Code-/Magic-Value-Prüfungen: keine Befunde im betroffenen MCP-Produktionsscope während des Versuchs.
- Vollständige Nicht-Stress-Gates beider Testprojekte sowie Sage-/Wire-Proof: nicht ausgeführt.

## Tech-Debt-Disposition

- Produktionsviolations: behoben; kein neuer Tech-Debt-Eintrag.
- Paket-2-Regressionstest-Drift: für diesen Korrekturversuch offen/deferred, weiterhin im bestehenden Versuchskontext 1/5; nicht in `tech-debt.md` verschoben.
- Kein Commit erstellt.
