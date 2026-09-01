## Primäre Einstiegspunkte

- Assembly-MCP-Verträge und Analysefluss in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` sowie `src/AiNetLinter/Mcp/Assemblies/Analysis/`.
- Navigations- und Strukturtools in `src/AiNetLinter/Mcp/Tools/`.

## Betroffene Dateien und Symbole

- Verifiziert und geändert: `McpToolResults.Error`, `McpToolResults.Recoverable`, `McpToolResults.CompilationError` und `McpErrorPayload` (typisierte Fehlerpayload, unveränderte IsError-Policy).
- Verifiziert und geändert: `AssemblyAnalysisResponse.Enrich`/`FitsResponseBudget`/`Unsupported`, `AssemblyAnalysisResponseLimits` und `AssemblyAnalysisResponseLimits.Budget` (typisierte globale 8-KiB-Projektion für Text und JSON einschließlich nachgelagerter Enrichment-Metadaten, sichere Singleton-Kappung sowie Diagnose-/Referenzprojektionen), `AssemblyAnalysisModels`, `AssemblyAnalysisService.Inspect`/`FindExtensions`, `InspectAssemblyTool`, `InspectAssemblyFormatter` und `FindAssemblyExtensionsTool` (vollständige Pflichtfelder, shown/total/truncated/truncatedBy sowie gemeinsame Text/JSON-Auswahl). Der alte JSON-Surgery-Compactor bleibt entfernt; das exakte `TryRemoveLastDiagnostic`-Duplikat ist durch einen gemeinsamen generischen Helper ersetzt.
- Neue fokussierte Regressionen: `AssemblyAnalysisDispatcherCapabilityTests.ResponseBudget` enthält `AssemblyRoute_BudgetsFinalEnrichedResponseThroughDispatcher` für eine absichtlich große Antwort über Lease, Dispatcher und abschließendes `Enrich`; `AssemblyAnalysisToolTests.ResponseBudget` enthält die Producer-Projektionen sowie `InspectAssembly_GlobalResponseBudgetRemovesOversizedSingletonMember` für die letzte Singleton-Reduktionsstufe bei einem einzelnen übergroßen Member. Die Partial-Dateien halten die fachlich zusammengehörigen Tests getrennt und alle betroffenen Dateien unter 500 Zeilen.
- Verifiziert und geändert: `FindSymbolTool`, `FindSymbolScanner`, `AssemblySymbolSearch` und `AssemblySymbolResolver` (generationgebundene `id:`-Folgekennungen); `SolutionDocumentPathResolver` und `GetFileSkeletonTool` (relative/virtuelle Pfade ohne CWD-Fallback, Mehrdeutigkeit recoverable); `GetFileTreeScanner` (effektive Tiefe inklusive `MaxDepth`).

## Aufrufer und Abhängigkeiten

- Tool-Registrierungen erzeugen Argumente und MCP-Handler; Formatter und Structured-Content-Builder müssen dieselbe Auswahl verwenden.
- `AssemblyAnalysisDispatcher.ExecuteAsync` erwirbt über `IAssemblyAnalysisRegistry.LeaseAsync` den Root-Lease, expandiert optional über `AssemblyAnalysisLease.ExpandReferencesAsync`, führt den leasegebundenen Producer aus und reichert danach mit `AssemblyAnalysisResponse.Enrich` an; die neue Dispatcher-Regression nutzt genau diese Verdrahtung.
- Assembly-Sessions, Symbol-IDs und Dokumentpfade sind gemeinsame Abhängigkeiten für Folgeaufrufe.

## Relevante Tests, Konfiguration und Dokumentation

- Relevante Teststartpunkte aus dem Konzept: `src/AiNetLinter.FastTests/Mcp/Assemblies/`, `src/AiNetLinter.FastTests/Mcp/`, `src/AiNetLinter.IntegrationTests/`; die Producer-/Singleton-Budgetregressionen liegen in `AssemblyAnalysisToolTests.ResponseBudget.cs`, die Lease-/Enrichment-Regression in `AssemblyAnalysisDispatcherCapabilityTests.ResponseBudget.cs`; die jeweiligen Stammdateien enthalten den übrigen Fachbereich derselben Partial-Testklasse.
- Abschlussgates: Solution-Build und beide Nicht-Stress-Testprojekte gemäß `AGENTS.md`.
- Konzeptvertrag: `tasks/decompiled-assembly-fix3/Konzept.md`; Roadmap: diese Task-Datei.

## Invarianten, Risiken und Unsicherheiten

- Text und Structured Content dürfen nicht auseinanderlaufen; `isError`-Policy bleibt unverändert.
- Das 8-KiB-Budget wird für Dispatcher-Aufrufe mit dem finalen `AssemblyAnalysisResponse.Enrich`-Ergebnis vermessen. Kann ein sichtbares Item allein das Budget überschreiten, wird es als letzte sichere Reduktionsstufe entfernt; `totalCount` bleibt erhalten und `responseBudget` wird als Trunkierungsgrund gesetzt. Die Header-/Metadatenbasis selbst wird nicht künstlich gekürzt.
- Fremde Assemblies werden nicht ausgeführt; Source-Trust bleibt fail-closed.
- Windows-Reparse-/8.3-Kanonisierung bleibt ohne reproduzierbaren Alias-Befund zurückgestellt.

## Verifikation

- Gezielte Budget-/Assembly-FastTests nach der letzten Teständerung: `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisToolTests|FullyQualifiedName~AssemblyAnalysisDispatcherCapabilityTests" --no-restore` — 29/29 bestanden; der Testumfang enthält Producer-Budget, Dispatcher-/Enrichment-Budget, Singleton-Reduktion, Dispatcher-Expansion und Status-/Zählerverträge. Stammdateien: 492 bzw. 448 Zeilen; Partial-Dateien: 41 bzw. 139 Zeilen.
- MCP-Qualitätschecks nach der letzten Codeänderung im erweiterten Test-Scope `src/AiNetLinter.FastTests/Mcp`: `find_duplicates` mit `targetType=project`, absolutem Projektpfad, `scopeDir=src/AiNetLinter.FastTests/Mcp`, `scopeType=tests`, `similarityThreshold=exact`, `maxResults=50` — 0 Cluster bei 1035 Methoden; `find_dead_code` mit Projekt-Target, `includeTests=true`, Scope-Filter und `confidence=high` — 0 High-Confidence-Funde; `find_magic_values` mit Projekt-Target, `includeTests=true`, `changedOnly=true` — 54 Vorkommen in 36 Einträgen über 2 Dateien, bestehende testbezogene Literale ohne sichere scope-nahe Zentralisierung.
- `git diff --check` nach der letzten Codeänderung — erfolgreich; Git meldete nur erwartete LF/CRLF-Hinweise, keine Whitespace-Fehler.
- Abschließender MCP-Nachweis nach der letzten Codeänderung: `get_violations` mit `targetType=project`, absolutem `targetPath=C:\Daten\Entwicklung\Ralf\AiNetLinter`, erweitertem `scopeFilter=src/AiNetLinter.FastTests/Mcp`, `includeSnippet=true`, `contextLines=1`, `maxResults=200` — 0 Verstöße in 134 Dateien; beide betroffenen Testdateien und ihre Partial-Dateien sind im Scope enthalten.
- Abschlussgates: `dotnet build` — 0 Warnungen/0 Fehler; gezielte Assembly-Tests — 29/29 bestanden; `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — 2324 bestanden, 5 fehlgeschlagen, 2 übersprungen; `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — 376 bestanden, 1 fehlgeschlagen. Die vollständigen roten Befunde sind scopefremd und werden unverändert übergeben.
