# Code-Map: decompiled-assembly-test

> Navigationshilfe für den Task **Test- und Basiskorrekturen für MCP-Assembly- und Tool-Filter**.
> Kein Source of Truth — Pfade/Symbole gegen Working Tree und MCP verifizieren.

## Primäre Einstiegspunkte
- MCP-Server für C#-Semantik: `targetType=project`, `targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter` (Solution `AiNetLinter.slnx`, `rules.json`).
- Konzept: `tasks/decompiled-assembly-test/Konzept.md` (status: draft, fachlich vollständig; 3 Umsetzungspakete).

## Betroffene Dateien und Symbole
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs` — `BuildResult`, Übergabe `ReceiverType`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisModels.cs` — `AssemblyExtensionSearchOptions` (um `ReceiverType` erweitern).
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisService.cs` — `FindExtensions`, neuer `MatchesReceiverType`-Filter.
- `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs` — Assembly-Erweiterungsvalidierung (.dll/.exe).
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Validation/ExternalSourceMappingValidator.cs` — .dll/.exe.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Resolution/AssemblySourceMatchResolver.cs` — .dll/.exe.
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs` — `Scan`, effektive Tiefe (`MaxDepth ?? TreeDepth`, 0 = Root).
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeTool.cs` — Parameter/Doku.
- `src/AiNetLinter/Mcp/Tools/FileStructure/FileTreeAccumulator.cs` — `Build`, Summary-Modus.

## Aufrufer und Abhängigkeiten
- (Wird vom ersten Implementierer mit MCP-first-Kontextphase ergänzt: Aufrufer, Tests, Doku-Stellen.)

## Relevante Tests, Konfiguration und Dokumentation
- Tests laut Konzept: `AssemblyAnalysisToolTests.cs` (Receiver-Filter), `GetFileTreeScannerTests.cs` (treeDepth), Unit-Tests für .dll/.exe-Validierung.
- Doku-Sync-Pflicht bei CLI-/Vertragsänderungen: `Docs/configuration.md`, `Docs/ROADMAP.md` (prüfen); Agenten-Regeln `.agents/rules/AiNetLinter.mdc` via `--sync-agent-rules-only` (nur bei Regeln/rules.json-Änderungen).
- MCP-Regeln: `.agents/rules/AiNetLinter-McpWorkflow.mdc`.

## Invarianten, Risiken und Unsicherheiten
- Keine Änderungen an `ExternalSourceSnapshotMaterializer`, Git-Checkout, Server-Health-Payload (Non-Goals).
- „0 = Root-Ebene, nicht unbegrenzt“ als Tiefensemantik; `maxDepth` hat Vorrang.
- Normalisierung von Typnamen: nur Darstellungspräfixe (z. B. `global::`) entfernen; keine case-insensitive Semantik, kein unsicheres `EndsWith`.
- Working Tree enthält fremde Nutzeränderungen (`tasks/decompiled-assembly-fix1/findings1.md`, `findings2.md` gelöscht) — nicht anfassen, nicht committen.

## Verifikation
- Konzept-Gates: `dotnet build`; `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`; `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
- Gezielter `get_violations`-Check nach der letzten Codeänderung (Scope: Änderungsbereich).