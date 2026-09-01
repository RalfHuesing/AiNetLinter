## Primäre Einstiegspunkte

- Assembly-MCP-Verträge und Analysefluss in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` sowie `src/AiNetLinter/Mcp/Assemblies/Analysis/`.
- Navigations- und Strukturtools in `src/AiNetLinter/Mcp/Tools/`.

## Betroffene Dateien und Symbole

- Verifiziert und geändert: `McpToolResults.Error`, `McpToolResults.Recoverable`, `McpToolResults.CompilationError` und `McpErrorPayload` (typisierte Fehlerpayload, unveränderte IsError-Policy).
- Verifiziert und geändert: `AssemblyAnalysisResponse.Enrich`/`FitsResponseBudget`/`Unsupported`, `AssemblyAnalysisResponseLimits` und `AssemblyAnalysisResponseLimits.Budget` (typisierte globale 8-KiB-Projektion für Text und JSON einschließlich nachgelagerter Enrichment-Metadaten, sichere Singleton-Kappung sowie Diagnose-/Referenzprojektionen), `AssemblyAnalysisModels`, `AssemblyAnalysisService.Inspect`/`FindExtensions`, `InspectAssemblyTool`, `InspectAssemblyFormatter` und `FindAssemblyExtensionsTool` (vollständige Pflichtfelder, shown/total/truncated/truncatedBy sowie gemeinsame Text/JSON-Auswahl). Der alte JSON-Surgery-Compactor bleibt entfernt; das exakte `TryRemoveLastDiagnostic`-Duplikat ist durch einen gemeinsamen generischen Helper ersetzt.
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
- Das 8-KiB-Budget wird für Dispatcher-Aufrufe mit dem finalen `AssemblyAnalysisResponse.Enrich`-Ergebnis vermessen. Kann ein sichtbares Item allein das Budget überschreiten, wird es als letzte sichere Reduktionsstufe entfernt; `totalCount` bleibt erhalten und `responseBudget` wird als Trunkierungsgrund gesetzt. Die Header-/Metadatenbasis selbst wird nicht künstlich gekürzt.
- Fremde Assemblies werden nicht ausgeführt; Source-Trust bleibt fail-closed.
- Windows-Reparse-/8.3-Kanonisierung bleibt ohne reproduzierbaren Alias-Befund zurückgestellt.

## Verifikation

- Gezielte Budget-/Assembly-FastTests nach der letzten Codeänderung: `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisToolTests|FullyQualifiedName~AssemblyAnalysisDispatcherCapabilityTests" --no-restore` — 27/27 bestanden. Die vorhandenen Tests decken Producer-Budget, Dispatcher-Expansion und Status-/Zählerverträge ab; in diesem terminalen Kurzversuch wurden keine zusätzlichen Testmethoden angelegt.
- MCP-Qualitätschecks nach der letzten Codeänderung im Scope `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis`: `find_duplicates` mit `targetType=project`, absolutem Projektpfad, `scopeDir=src/AiNetLinter/Mcp/Tools/AssemblyAnalysis`, `scopeType=production`, `similarityThreshold=exact`, `maxResults=50` — 0 Cluster bei 89 Methoden; `find_dead_code` mit Projekt-Target, Scope-Filter und `confidence=high` — 0 High-Confidence-Funde; `find_magic_values` mit Projekt-Target, Scope-Filter und `changedOnly=true` — 0 Treffer.
- `git diff --check` nach der letzten Codeänderung — erfolgreich; Git meldete nur erwartete LF/CRLF-Hinweise, keine Whitespace-Fehler.
- Abschließender MCP-Nachweis nach der letzten Codeänderung: `get_violations` mit `targetType=project`, absolutem `targetPath=C:\Daten\Entwicklung\Ralf\AiNetLinter`, `scopeFilter=src/AiNetLinter/Mcp/Tools/AssemblyAnalysis`, `includeSnippet=true`, `contextLines=1`, `maxResults=200` — 0 Verstöße.
- Vollständiger Build und die beiden vollständigen Nicht-Stress-Gates wurden in diesem Korrekturversuch nicht ausgeführt; der gezielte Testlauf kompilierte die betroffenen Projekte erfolgreich. Die letzte unabhängige Reviewer-Verifikation meldete zuvor einen vollständigen Build mit 0 Warnungen/0 Fehlern, jedoch nicht auf diesem finalen Working-Tree-Zustand.
