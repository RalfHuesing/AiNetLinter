## Primäre Einstiegspunkte

- Assembly-MCP-Verträge und Analysefluss in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` sowie `src/AiNetLinter/Mcp/Assemblies/Analysis/`.
- Navigations- und Strukturtools in `src/AiNetLinter/Mcp/Tools/`.

## Betroffene Dateien und Symbole

- Verifiziert und geändert: `McpToolResults.Error`, `McpToolResults.Recoverable`, `McpToolResults.CompilationError` und `McpErrorPayload` (typisierte Fehlerpayload, unveränderte IsError-Policy).
- Verifiziert und geändert: `AssemblyAnalysisResponse.Enrich`/`Unsupported`, `AssemblyAnalysisResponseLimits` (typed Vorprojektion für Diagnosen/Referenzen), `AssemblyAnalysisModels`, `AssemblyAnalysisService.Inspect`/`FindExtensions`, `InspectAssemblyTool`, `InspectAssemblyFormatter` und `FindAssemblyExtensionsTool` (vollständige Pflichtfelder, shown/total/truncated/truncatedBy sowie Text/JSON-Konsistenz). Der alte JSON-Surgery-Compactor wurde entfernt.
- Verifiziert und geändert: `FindSymbolTool`, `FindSymbolScanner`, `AssemblySymbolSearch` und `AssemblySymbolResolver` (generationgebundene `id:`-Folgekennungen); `SolutionDocumentPathResolver` und `GetFileSkeletonTool` (relative/virtuelle Pfade ohne CWD-Fallback, Mehrdeutigkeit recoverable); `GetFileTreeScanner` (effektive Tiefe inklusive `MaxDepth`).

## Aufrufer und Abhängigkeiten

- Tool-Registrierungen erzeugen Argumente und MCP-Handler; Formatter und Structured-Content-Builder müssen dieselbe Auswahl verwenden.
- Assembly-Sessions, Symbol-IDs und Dokumentpfade sind gemeinsame Abhängigkeiten für Folgeaufrufe.

## Relevante Tests, Konfiguration und Dokumentation

- Relevante Teststartpunkte aus dem Konzept: `src/AiNetLinter.FastTests/Mcp/Assemblies/`, `src/AiNetLinter.FastTests/Mcp/`, `src/AiNetLinter.IntegrationTests/`.
- Abschlussgates: Solution-Build und beide Nicht-Stress-Testprojekte gemäß `AGENTS.md`.
- Konzeptvertrag: `tasks/decompiled-assembly-fix3/Konzept.md`; Roadmap: diese Task-Datei.

## Invarianten, Risiken und Unsicherheiten

- Text und Structured Content dürfen nicht auseinanderlaufen; `isError`-Policy bleibt unverändert.
- Fremde Assemblies werden nicht ausgeführt; Source-Trust bleibt fail-closed.
- Windows-Reparse-/8.3-Kanonisierung bleibt ohne reproduzierbaren Alias-Befund zurückgestellt.

## Verifikation

- Gezielte FastTests nach letzter Codeänderung: `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~McpToolResultsTests|FullyQualifiedName~AssemblyAnalysisToolTests|FullyQualifiedName~FindSymbolToolTests|FullyQualifiedName~GetFileTreeScannerTests|FullyQualifiedName~AssemblyAnalysisPathContractTests" --no-restore` — 53/53 bestanden.
- MCP-Qualitätschecks im Scope `src/AiNetLinter/Mcp`: `find_duplicates` (1 bestehender Near-Cluster), `find_dead_code` (0 high-confidence Funde), `find_magic_values` (4 Hinweise, unverändert zurückgestellt).
- Abschließender MCP-Nachweis: `get_violations` mit `targetType=project`, absolutem `targetPath=C:\Daten\Entwicklung\Ralf\AiNetLinter`, `scopeFilter=src/AiNetLinter`, `includeSnippet=true`, `contextLines=1`, `maxResults=200` — 0 Fehler, 7 Warnungen; vier davon betreffen neue Parameterzahlgrenzen, ein bestehendes AIContext-Footprint-Limit.
