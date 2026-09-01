## Primäre Einstiegspunkte

- Assembly-MCP-Verträge und Analysefluss in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` sowie `src/AiNetLinter/Mcp/Assemblies/Analysis/`.
- Navigations- und Strukturtools in `src/AiNetLinter/Mcp/Tools/`.

## Betroffene Dateien und Symbole

- Verifiziert und geändert: `McpToolResults.Error`, `McpToolResults.Recoverable`, `McpToolResults.CompilationError` und `McpErrorPayload` (typisierte Fehlerpayload, unveränderte IsError-Policy).
- Verifiziert und geändert: `AssemblyAnalysisResponse.Enrich`/`Unsupported`, `AssemblyAnalysisResponseLimits` (typisierte globale 8-KiB-Vorprojektion für Text und JSON sowie Diagnose-/Referenzprojektionen), `AssemblyAnalysisModels`, `AssemblyAnalysisService.Inspect`/`FindExtensions`, `InspectAssemblyTool`, `InspectAssemblyFormatter` und `FindAssemblyExtensionsTool` (vollständige Pflichtfelder, shown/total/truncated/truncatedBy sowie gemeinsame Text/JSON-Auswahl). Die Vorprojektion misst die spätere `AssemblyAnalysisResponse.Enrich`-Anreicherung noch nicht; der alte JSON-Surgery-Compactor wurde entfernt.
- Verifiziert und geändert: `FindSymbolTool`, `FindSymbolScanner`, `AssemblySymbolSearch` und `AssemblySymbolResolver` (generationgebundene `id:`-Folgekennungen); `SolutionDocumentPathResolver` und `GetFileSkeletonTool` (relative/virtuelle Pfade ohne CWD-Fallback, Mehrdeutigkeit recoverable); `GetFileTreeScanner` (effektive Tiefe inklusive `MaxDepth`).

## Aufrufer und Abhängigkeiten

- Tool-Registrierungen erzeugen Argumente und MCP-Handler; Formatter und Structured-Content-Builder müssen dieselbe Auswahl verwenden.
- Assembly-Sessions, Symbol-IDs und Dokumentpfade sind gemeinsame Abhängigkeiten für Folgeaufrufe.

## Relevante Tests, Konfiguration und Dokumentation

- Relevante Teststartpunkte aus dem Konzept: `src/AiNetLinter.FastTests/Mcp/Assemblies/`, `src/AiNetLinter.FastTests/Mcp/`, `src/AiNetLinter.IntegrationTests/`; die Budgetregressionen liegen in `AssemblyAnalysisToolTests`.
- Abschlussgates: Solution-Build und beide Nicht-Stress-Testprojekte gemäß `AGENTS.md`.
- Konzeptvertrag: `tasks/decompiled-assembly-fix3/Konzept.md`; Roadmap: diese Task-Datei.

## Invarianten, Risiken und Unsicherheiten

- Text und Structured Content dürfen nicht auseinanderlaufen; `isError`-Policy bleibt unverändert.
- Das 8-KiB-Budget wird aktuell nur vor `AssemblyAnalysisResponse.Enrich` geprüft. Bei einem einzelnen übergroßen Pflicht-Item kann `AssemblyAnalysisResponseLimits` nach dem letzten entfernbaren Item abbrechen und eine weiterhin übergroße Antwort zurückgeben.
- Fremde Assemblies werden nicht ausgeführt; Source-Trust bleibt fail-closed.
- Windows-Reparse-/8.3-Kanonisierung bleibt ohne reproduzierbaren Alias-Befund zurückgestellt.

## Verifikation

- Gezielte Budget-/Assembly-FastTests nach letzter Codeänderung: `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisToolTests|FullyQualifiedName~AssemblyAnalysisDispatcherCapabilityTests" --no-restore` — 27/27 bestanden. Die neuen Budgetfälle rufen die Producer-Overloads direkt auf; der Dispatcher-Pfad mit nachgelagerter `AssemblyAnalysisResponse.Enrich` ist damit nicht abgedeckt.
- MCP-Qualitätschecks nach letzter Codeänderung im Scope `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis`: `find_duplicates` fand 4 Cluster, darunter 2 neue Duplikatpaare der Budget-Hilfslogik; `find_dead_code` fand 0 High-Confidence-Funde; `find_magic_values` fand 0 Treffer.
- Abschließender MCP-Nachweis nach letzter Codeänderung: `get_violations` mit `targetType=project`, absolutem `targetPath=C:\Daten\Entwicklung\Ralf\AiNetLinter`, `scopeFilter=src/AiNetLinter/Mcp/Tools/AssemblyAnalysis`, `includeSnippet=true`, `contextLines=1`, `maxResults=200` — 3 Befunde: `AssemblyAnalysisResponseLimits.cs` überschreitet mit 543 Zeilen das 500-Zeilen-Limit, die beiden neuen `TryRemoveLastDiagnostic`-Überladungen sind exact dupliziert, und `FindAssemblyExtensionsTool` überschreitet das AIContext-Footprint-Limit um 3 Zeilen.
- Vollständiger Build und die beiden vollständigen Nicht-Stress-Gates wurden in diesem Korrekturversuch nicht erneut ausgeführt; der gezielte Testlauf kompilierte die betroffenen Projekte erfolgreich.
