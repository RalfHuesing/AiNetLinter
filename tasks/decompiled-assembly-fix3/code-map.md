## Primäre Einstiegspunkte

- Assembly-MCP-Verträge und Analysefluss in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` sowie `src/AiNetLinter/Mcp/Assemblies/Analysis/`.
- Navigations- und Strukturtools in `src/AiNetLinter/Mcp/Tools/`.

## Betroffene Dateien und Symbole

- Initiale Startpunkte aus Konzept Paket 1: `McpToolResults`, `AssemblyAnalysisResponse`, `AssemblyAnalysisResponseBudgetCompactor`, `AssemblyAnalysisResponseLimits`, `AssemblyAnalysisModels`, `AssemblyAnalysisService`, `InspectAssemblyTool`, `InspectAssemblyFormatter`, `FindAssemblyExtensionsTool`, `FindSymbolTool`, `SolutionDocumentPathResolver`, `GetFileTreeScanner`.
- Konkrete Symbole und weitere Abhängigkeiten werden vom Implementierer MCP-first gegen den aktuellen Working Tree verifiziert und ergänzt.

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

- Noch nicht ausgeführt. Jeder Implementierer ergänzt hier konkrete geänderte Symbole, Tests und MCP-Nachweise; Reviewer und Audit verifizieren die Karte gegen ihren Scope.
