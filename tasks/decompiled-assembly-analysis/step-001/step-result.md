---
status: done (pending audit)
task: decompiled-assembly-analysis
step: 001
coded_by_model: gpt-5
coded_by_model_knowledge_cutoff: nicht angegeben
---

# Step-Ergebnis

## Zusammenfassung

Der MCP-Target-Vertrag verwendet für alle zielgebundenen Tools `targetType` und
den kanonischen absoluten `targetPath`. Projektziele behalten den bestehenden
Registry-/Lease-Lifecycle; spezialisierte Assembly-Tools verwenden den
gemeinsamen Dispatch-Einstieg ohne Consumer-Projekt und ohne Assembly-Laden.
Health-Aggregat, Projektfilter, Assembly-Unsupported und Feedback-Ausnahme sind
getrennt vertraglich abgebildet.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/AnalysisTarget.cs`, `AnalysisTargetResolver.cs` und `AnalysisToolCall.cs` als Target-Modell, Validierung und Dispatcher.
- MCP-Registrierungen, `McpToolRegistrationOptions`, `ServerInstructions`, CLI-/Command-Beschreibungen und `LinterErrorCodes`.
- Resolver-, Dispatcher-, Schema-, Lifecycle-, Wire- und E2E-Tests einschließlich `WiringFilesystemContractTests.cs`.
- `README.md`, `Docs/agent-api.md`, `Docs/integration.md` und `Docs/mcp-bootstrap.md`.

## Commit

- `f14ff5c2` — `feat: MCP-Targets vereinheitlichen [decompiled-assembly-analysis]`

## Build-/Test-Output

- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1.857 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 360 Tests.

## Abweichungen vom Plan

- Die vorhandenen Registrierungen liegen im Repository unter `src/AiNetLinter/Mcp/Registration/`; dort wurde die vollständige Umstellung vorgenommen.
- Die vier Dateisystem-Dispatch-Regressionstests wurden zur Einhaltung der bestehenden MaxLineCount-Regel in `WiringFilesystemContractTests.cs` verschoben.
- Die CodeMap wurde trotz des JIT-Hinweises um die tatsächlich neu angelegte Target-/Dispatcher-Grenze ergänzt, wie für den Coder-Übergang gefordert.

## Beobachtungen

- Der erste Abschluss-Build enthielt eine stehengebliebene alte Lambda-Zeile in `McpServerCommandContractTests.cs`; sie wurde entfernt.
- Der erste Integrationstestlauf meldete danach ausschließlich die durch die Migration gewachsene `WiringContractTests.cs` über dem MaxLineCount-Limit; die Tests wurden ohne Verhaltensänderung thematisch ausgelagert.
- Die finale Wiederholung der drei Pflicht-Gates nach diesen Korrekturen ist vollständig grün.

## Bekannte Unschärfen

- Allgemeine Tools liefern für `targetType=assembly` bewusst `ASSEMBLY_TARGET_UNSUPPORTED`; die residente Assembly-Registry und Decompilation bleiben für die folgenden Epics offen.
- `find_assembly_extensions` kann ohne Consumer-Projekt keine konkrete Roslyn-Reduzierbarkeit bestimmen und weist solche Fälle als `not_decidable` aus.
