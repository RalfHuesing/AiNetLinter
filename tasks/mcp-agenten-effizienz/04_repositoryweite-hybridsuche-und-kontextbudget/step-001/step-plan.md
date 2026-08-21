---
status: open
type: step-plan
task: 04_repositoryweite-hybridsuche-und-kontextbudget
step: 001
corrects: null
title: "Strukturierte repositoryweite Suche mit Legacy-Kompatibilität und Kontextbudget"
epic: EPIC-01
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-21T11:32:43+02:00
related_to: []
---

# Step 001: Strukturierte repositoryweite Suche mit Legacy-Kompatibilität und Kontextbudget

## Bezug

- **Task:** `04_repositoryweite-hybridsuche-und-kontextbudget`
- **Primäres Epic:** `EPIC-01` aus `roadmap.md` — Baseline und sichere Suchgrundlage; dieser zusammenhängende Coding-Step bedient zusätzlich die unmittelbar darauf aufbauenden Implementierungsteile von `EPIC-02` und `EPIC-03`.
- **Konzept-Referenz:** `04_repositoryweite-hybridsuche-und-kontextbudget.md`, Abschnitte „Must-have: strukturiertes lexikalisches Suchresultat“, „Must-have: Kontextbudget und Vollständigkeit“, „Must-have: kontrollierbarer Repository-Scope“ sowie Umsetzungsschritte 1–4.
- **Bewusste spätere Steps:** C#-Roslyn-Enrichment bleibt `EPIC-04`; umfassende README-/API-/Integrations-/Overview-Dokumentation und vollständige Wire-Vertragsangleichung bleiben `EPIC-05`; ein optionaler `rg`-Vergleich bleibt `EPIC-06`.

## Aktueller Projektzustand (JIT-Kontext)

- `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs` ist ein `static`-Scanner mit `SearchAndFormat(...)` und `GetFilesWithHits(...)`. Er enumeriert derzeit nur die von `WebFileCatalog.GetProjectDirectories(solution)` gelieferten Projektverzeichnisse, nutzt `FileSystemExclusionHelpers.SafeEnumerateFiles(...)` plus `IsGeneratedPath(...)`, liest mit `File.ReadAllLines`, trimmt Zeilenende und liefert ausschließlich deterministisch sortierten Legacy-Text. Match-Spans, Kontext, Dateibudgets, Scope-Metadaten und Vollständigkeit fehlen.
- `GetFilesWithHits(...)` wird von `FindSymbolScanner` für den Nicht-C#-Miss-Hint verwendet. Diese API und ihre solution-relativen Forward-Slash-Pfade müssen als schlanker, kompatibler Pfad erhalten bleiben; sie darf nicht versehentlich die neuen Treffer-/Antwortbudgets erben.
- `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternTool.cs` validiert `pattern`, normalisiert `maxResults` wie bisher, behandelt Loading/SOLUTION_NOT_LOADED und führt den Scan via `Task.Run` mit `CancellationToken` aus. Anschließend wird der bestehende Compile-Warnhinweis über `FindSymbolTool` vorangestellt und `McpToolResults.Text(...)` verwendet.
- `src/AiNetLinter/Mcp/McpToolResults.cs` besitzt mit `Text<T>(text, payload)` bereits den zentralen, kompakt serialisierten CamelCase-Structured-Content-Pfad; der Payload muss ein Top-Level-Objekt bleiben. `src/AiNetLinter/Mcp/McpTruncation.cs` enthält die bestehenden Legacy-Meta-Zeilen und darf für den bisherigen `maxResults`-Textvertrag nicht semantisch verändert werden.
- `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs` ist der gemeinsame Dateisystem-Helper für sichere Enumeration und generierte Pfade; `src/AiNetLinter/Configuration/FileFilterEvaluator.cs` und `FileFiltersConfig` liefern bereits case-insensitive Dateiname-/Verzeichnis- und Glob-Semantik. Die neue Suche soll diese Pfade erweitern bzw. wiederverwenden, nicht eine zweite Exclusion-/Glob-Implementierung anlegen.
- `WebFileCatalog` kennt Projektverzeichnisse und dedupliziert physische Dateien; `SourceFileCatalog.IsValidDocument(...)` und `SourceFileCatalog.IsGeneratedPath(...)` zeigen die bestehende Roslyn-/Generated-Policy. Für die repositoryweite Suche muss der sichere Root jedoch aus `Path.GetDirectoryName(solution.FilePath)` kommen, ohne einen vom Agenten gelieferten Pfad ungeprüft zu verwenden.
- `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScannerRecords.cs` zeigt das etablierte Muster separater interner Records für Parameter, Payload, Einträge und Summary. Dieses Muster ist für den Search-Payload wiederzuverwenden; C#-SemanticModel-Auflösung gehört ausdrücklich nicht in diesen Step.
- Bestehende Tests liegen primär in `src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternToolTests.cs` (10 Tests für Plain-/Regex-Suche, Legacy-Trunkierung, Exclusions, Fehler und Loading) sowie in `McpServerAllToolsE2ETests`, `McpServerCommandContractTests` und `McpServerCommandJsonRpcFramingTests`. `SymbolGraphMini` deckt bereits C#, JS, CSS, Razor, XAML und HTML ab; JSON/Markdown und budgetierte/excludierte Fälle müssen für die neutrale Baseline ergänzt werden.
- `McpCodeGraphServer.GetCurrentSolution()` liefert den residenten, staleness-aktualisierten Solution-Snapshot. Der Scan darf keinen zweiten MSBuild-/Roslyn-Load und keinen Session-/Cursor-State einführen.
- Der Task besitzt aktuell kein `tech-debt.md`; daher wird kein künstlicher Tech-Debt-Eintrag angelegt.

## Intention

In diesem Step wird der bestehende `search_pattern`-Scanner zu einer deterministischen, repositoryweiten lexikalischen Suche mit strukturierten Treffern, MatchRanges, optionalem Kontext, sicherem Scope und expliziter Completeness erweitert. Die bisherige Legacy-Textantwort und die `GetFilesWithHits`-Nutzung bleiben kompatibel; neue Budgets werden additiv aktiviert und niemals durch ein neues Suchtool oder eine `rg`-Abhängigkeit umgesetzt.

Die drei eng gekoppelten Implementierungsepen `EPIC-01` bis `EPIC-03` bilden hier bewusst eine Review-Einheit: Baseline-Fixture und Kompatibilitätstests sichern den Ist-Vertrag, das Modell/der Formatter trennt Daten von Darstellung, und Scope-/Budget-/Truncation-Logik wird gegen genau dieses Modell geprüft. C#-Enrichment, umfassende Dokumentation und Mess-/`rg`-Prototypen bleiben außerhalb des Steps.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScannerRecords.cs` (neu)

- **Was:** Interne, unveränderliche Records nach dem Muster `FindMagicValuesScannerRecords` anlegen: ein Input-/Options-Record für `pattern`, `isRegex`, `maxResults`, `maxFiles`, `contextLines`, `maxResponseBytes`, Scope- und Filterwerte sowie Cancellation; ein Root-Payload für `matches`, `completeness` und sichtbare Scope-/Snapshot-Metadaten; `SearchPatternMatch` mit solution-relativem Forward-Slash-`filePath`, 1-basierter `line`, `matchRanges`, unverändertem `lineText`, `contextBefore`, `contextAfter` und optionalem `projectName`; `SearchPatternMatchRange` mit 1-basierter `column` und `length`; und ein Completeness-Record mit Gesamt-/Sichtbarzahlen, `scanCompleted`, `truncated`, getrennten `truncatedBy`-Gründen sowie Zählern für übersprungene Binär-/unlesbare Dateien.
- **Warum:** Structured Content soll dieselben Daten tragen wie der Legacy-Formatter, mehrere Matchbereiche einer Zeile nicht verlieren und als kompaktes Top-Level-Objekt über `McpToolResults.Text<T>` serialisierbar sein. `maxResults` bleibt explizit ein Limit für sichtbare Trefferzeilen; MatchRanges werden nicht stillschweigend zu zusätzlichen Trefferzeilen umdefiniert.

### Datei 2: `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs` (aktuelle Zeilen 29–162)

- **Was:** `SearchAndFormat(...)` auf den neuen Scan-/Ergebnis-Pfad umstellen und einen strukturierten Scan als einzige Traversierungsquelle einführen. Plain-Text-Suche sammelt alle case-insensitiven, nicht überlappenden Vorkommen je Zeile; Regex-Suche nutzt dieselbe `MatchRange`-Form, einen deterministischen Regex-Timeout und behandelt Zero-Length-Matches konsistent. Die Zeilenreihenfolge bleibt ordinal nach solution-relativem Pfad und Zeilennummer stabil; physische/Linked-Dateien werden dedupliziert.
- **Was:** `lineText` bleibt im strukturierten Ergebnis ohne Zeilenumbruch unverändert; der Legacy-Formatter erhält die bisherige Darstellung inklusive bisherigem Trim-/Meta-Zeilen-Verhalten. `contextLines` liest nur den begrenzten Bereich vor/nach der Trefferzeile und zählt nicht als zusätzlicher Treffer. `maxResults` begrenzt sichtbare Trefferzeilen, `maxFiles` sichtbare physische Trefferdateien; `0` bedeutet bei den neuen optionalen Budgets „nicht zusätzlich begrenzen“, damit alte Aufrufe unverändert bleiben.
- **Was:** Einen sicheren, generischen Scope auf Basis des kanonischen Solution-Verzeichnisses implementieren. Relative Scope-/Include-/Exclude-Filter werden gegen Forward-Slash-Pfade ausgewertet; absolute oder außerhalb des Solution-Roots liegende Agentenwerte werden recoverable abgewiesen. Standardmäßig bleiben Build-, VCS-, temporäre, generierte, Worktree-, binäre und offensichtliche `.min.*`-Dateien ausgeschlossen. Die bestehenden `FileSystemExclusionHelpers`-/`FileFilterEvaluator`-Mechanismen werden erweitert oder direkt wiederverwendet; keine projekt-, agenten- oder fest verdrahteten AiNetLinter-Pfade.
- **Was:** UTF-8-/BOM-erkennendes Lesen, konservative Binärheuristik, unlesbare Dateien, Enumeration-Fehler und Cancellation in die Scan-Summary aufnehmen. Ein Regex-Timeout wird als recoverable Zustand mit sichtbarer Ursache behandelt; keine Exception darf den MCP-Server wegen einer erwartbaren Datei-/Pattern-Grenze beenden.
- **Was:** `maxResponseBytes` deterministisch über der kompakt serialisierten Structured-Payload mit `Encoding.UTF8.GetByteCount` anwenden. Nach der stabilen Aggregation werden sichtbare Treffer in Reihenfolge aufgenommen, bis das Budget erreicht ist; die Gesamtzahlen bleiben erhalten, und `maxResponseBytes` wird zusätzlich zu `maxResults`/`maxFiles` in `truncatedBy` ausgewiesen. Kein Cursor, keine Session-Pagination und kein zweiter Scan für Text und JSON.
- **Warum:** Scope-Sicherheit, Encoding, Truncation und Cancellation werden an einer einzigen Datenquelle entschieden. Dadurch können Legacy-Text, Structured Content und Completeness nicht auseinanderlaufen.

### Datei 3: `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternLegacyFormatter.cs` (neu)

- **Was:** Einen reinen Formatter für die bestehende Trefferzeilen-Ausgabe anlegen. Er erzeugt aus der sichtbaren strukturierten Trefferliste den bisherigen `relativePath:line: text`-Text, behält die `0 Treffer`-Meldung und die bestehende `McpTruncation`-Meta-Zeile für `maxResults` bei und ergänzt neue Budget-/Scope-Hinweise nur, wenn der jeweilige neue Parameter tatsächlich begrenzt hat.
- **Warum:** Der Legacy-Vertrag bleibt getrennt vom Ergebnis-/Scanmodell und die vorhandenen `FindSymbolScanner`-/Text-Clients müssen keinen JSON-Inhalt parsen.

### Datei 4: `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternTool.cs` (aktuelle Zeilen 22–72)

- **Was:** Die neuen optionalen Argumente über einen Input-Record an den Scanner weiterreichen, bestehende Pattern-/Loading-/Solution-NotFound-/Invalid-Regex-Pfade erhalten und den strukturierten Payload additiv über `McpToolResults.Text(legacyText, payload)` zurückgeben. Der bestehende `FindSymbolTool.BuildAggregateWarningAsync`-Prefix bleibt im Legacy-Text erhalten; die Structured-Payload bleibt frei von diesem Präsentationstext.
- **Was:** Negative neue Budget-/Kontextwerte recoverable als `INVALID_ARGUMENT` behandeln; die bisherige `maxResults < 1`-Normalisierung bleibt unverändert. Regex-Timeout, unlesbare Dateien und Cancellation werden über den definierten Result-/Completeness-Pfad sichtbar, ohne `IsError=true` für erwartbare Eingabe-/Scanbedingungen zu missbrauchen.
- **Warum:** Das Tool bleibt ein dünner Dispatcher und nutzt die zentrale MCP-Ergebnis-/JSON-Serialisierung statt eine zweite Wire-Nutzlast zu bauen.

### Datei 5: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (aktuelle `AddSearchPattern`-/Description-Zeilen 93–111)

- **Was:** `search_pattern` additiv um die neuen optionalen Parameter für `maxFiles`, `contextLines`, `maxResponseBytes`, Scope-Filter sowie generische Include-/Exclude-Filter erweitern und die knappe Toolbeschreibung auf die tatsächlich implementierten Argumente und die additive Structured-Payload aktualisieren.
- **Warum:** Es bleibt exakt ein Tool mit abwärtskompatiblem Namen und bestehendem `pattern`-/`isRegex`-/`maxResults`-Vertrag; die Registrierung muss die neue API maschinenlesbar entdecken lassen.

### Datei 6: `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs` und bei Bedarf `src/AiNetLinter/Configuration/FileFilterEvaluator.cs`

- **Was:** Die gemeinsame sichere Enumeration-/Pfadfilter-Infrastruktur so erweitern, dass der Search-Scanner standardmäßige generische Ausschlüsse, Pfadgrenzen, Reparse-/Enumeration-Fehler und Datei-Leseprobleme mit Zählung nutzen kann, ohne bestehende Aufrufer zu brechen. Vorhandene `FileFiltersConfig`-/Glob-Semantik wird für relative Include-/Exclude-Filter wiederverwendet; neue Defaults bleiben search-spezifisch, sofern eine globale Verhaltensänderung bestehender Linter-Scans nicht sicher ist.
- **Warum:** Die Aufgabe fordert eine repositoryweite Suche ohne duplizierte Exclusion-Policy. Änderungen an der gemeinsamen Infrastruktur müssen die bestehenden `WebFileCatalog`-/`SourceFileCatalog`-Tests und die bisherigen `obj`/`bin`-/Worktree-Fälle unverändert grün halten.

### Datei 7: `tests/Fixtures/SymbolGraphMini/wwwroot/search-fixture.json` und `tests/Fixtures/SymbolGraphMini/search-fixture.md` (neu)

- **Was:** Die vorhandene neutrale Mehrsprach-Fixture um JSON und Markdown mit wiederholten Plain-/Regex-Mustern, mehreren Vorkommen in einer Zeile und Kontextzeilen ergänzen. Generierte, binäre, minifizierte und unlesbare Fälle werden in Tests kontrolliert unter dem Fixture-Root erzeugt und nach jedem Test entfernt; kein ad-hoc OS-Temp-Pfad.
- **Warum:** Die Baseline muss über C#, JSON, Markdown, JS/CSS/Razor/XAML/HTML sowie ausgeschlossene/problematische Dateien deterministisch prüfbar sein, ohne das Produktions-Tool auf ein bestimmtes Repository zu hardcodieren.

### Datei 8: `src/AiNetLinter.FastTests/Mcp/Tools/Analysis/SearchPatternScannerTests.cs` (neu)

- **Was:** Reine Scanner-/Formatter-Tests mit Adhoc-Workspace und zentralem `TestTempDirectory` anlegen: `Scan_PlainText_EmitsAllMatchRangesAndStablePositions`, `Scan_RegexUsesSameRangeModelAndStableOrdering`, `Scan_ContextLines_PreservesUnchangedLineText`, `Scan_MaxFilesAndMaxResponseBytes_ReportSeparateTruncationReasons`, `Scan_ScopeFiltersAndDefaultExclusions_StayInsideSolutionRoot` und `Scan_BinaryUnreadableAndCancelledFiles_AreReflectedInCompleteness`.
- **Warum:** MatchRange-Berechnung, Sortierung, Bytebudget und Sicherheits-/Completeness-Entscheidungen sollen ohne MCP-Prozess- oder Roslyn-Enrichment-Rauschen schnell regressionssicher sein.

### Datei 9: `src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternToolTests.cs`

- **Was:** Bestehende zehn Legacy-Tests erhalten und um `ExecuteAsync_StructuredContent_PreservesLegacyTextAndReturnsObjectPayload`, `ExecuteAsync_MultipleMatchesAndContext_ReturnRangesAndBoundedContext`, `ExecuteAsync_MaxResultsAndMaxFiles_ReportVisibleAndTotalCounts`, `ExecuteAsync_MaxResponseBytes_SetsCompletenessReason`, `ExecuteAsync_ScopeAndFilters_RespectGenericRelativePaths`, `ExecuteAsync_InvalidBudgets_ReturnRecoverableInvalidArgument` und `ExecuteAsync_DefaultCall_RetainsLegacyOutputSemantics` ergänzen.
- **Warum:** Direkte Tooltests sichern die additive Wire-Nutzlast, die bestehende Warning-/Error-Policy und die Semantik der alten Aufrufer gegen echte Fixture-Dateien.

### Datei 10: `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs` und `src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs`

- **Was:** Einen SDK-/Host-Vertragstest für die neuen `search_pattern`-Argumente und einen E2E-Test für Plain-/Regex-Suche mit structured Content ergänzen. Die vorhandenen Tests für Legacy-Treffer und fehlendes `pattern` bleiben unverändert und müssen weiterhin passieren.
- **Warum:** Die direkte Tool-Signatur und die tatsächliche MCP-Bindung werden getrennt von Scanner-Unit-Tests geprüft.

### Datei 11: `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandJsonRpcFramingTests.cs`

- **Was:** Einen Raw-Wire-Test `SearchPatternCall_RawStructuredContentIsObjectAndLegacyTextRemains` ergänzen. Er ruft das bestehende Tool per JSON-RPC mit Budget-/Kontextparametern auf, prüft gültige JSON-RPC-Framing-Zeilen, `result.structuredContent` als JSON-Objekt mit `matches`, `completeness` und Scope-Metadaten sowie den weiterhin vorhandenen Legacy-Textblock.
- **Warum:** `McpToolResults.Text<T>` hat bereits die zentrale Objekt-Serialisierung; dieser Test verhindert, dass der neue Payload versehentlich als Top-Level-Array oder nur als Text ausgeliefert wird.

### Datei 12: `tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget/codemap.md`

- **Was:** Nach der Umsetzung die tatsächlich angelegten Records, Formatter-/Scope-Helper und neuen Testanker als Pointer ergänzen; bestehende Einträge nur bei realer Verlagerung aktualisieren und nicht löschen.
- **Warum:** Der nächste JIT-Planer muss den tatsächlich gebauten Scanner-/Payload-Stand sehen und darf keine bereits getroffene Strukturentscheidung übergehen.

## Tests

- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` — schnelle Scanner-/Formatter-Regressionen inklusive Range-, Scope-, Encoding- und Completeness-Fällen.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~SearchPatternToolTests` — direkter Toolvertrag, Legacy-Text, Structured Content, Budgets und Fehlerpfade.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandContractTests` — SDK-/Host-Bindung der neuen Parameter und Legacy-Kompatibilität.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerAllToolsE2ETests` — bestehende Plain-/Regex-/Missing-Pattern-E2E-Abdeckung plus neue Structured-Response-Prüfung.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandJsonRpcFramingTests` — Raw-Wire-JSON-Objekt, Legacy-Text und gültiges stdout-Framing.
- [ ] `dotnet build` — alle vier Projekte ohne Warnungen; `TreatWarningsAsErrors` bleibt erfüllt.
- [ ] Abschluss-Gate vor Step-Abschluss: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.

## Definition of Done

- [ ] `search_pattern` bleibt das einzige Suchtool; `pattern`, `isRegex`, `maxResults`, die bisherige Legacy-Textausgabe und `GetFilesWithHits`-Miss-Hint-Nutzung bleiben kompatibel.
- [ ] Structured Content ist ein deterministisches Top-Level-Objekt mit mehreren `matchRanges` pro Zeile, 1-basierten Positionen, unverändertem strukturiertem `lineText`, optionalem Kontext, Projektname sofern sicher zuordenbar und sichtbaren Scope-/Snapshot-Metadaten.
- [ ] `maxResults`, `maxFiles`, `contextLines` und `maxResponseBytes` sind klar getrennt; Gesamt-/Sichtbarzahlen sowie `scanCompleted`, `truncated`, `truncatedBy`, Binär-/Unreadable-Zähler und Cancellation-/Regex-Timeout-Zustände sind maschinenlesbar.
- [ ] Standard-Scope bleibt sicher auf den kanonischen Solution-Root begrenzt; Include-/Exclude-Filter und generische Defaults schließen Build-, VCS-, temporäre, generierte, binäre und minifizierte Dateien deterministisch aus, ohne projekt- oder agentenspezifische Pfade.
- [ ] Legacy-Text und Structured Content stammen aus derselben sichtbaren Trefferliste; wiederholte Aufrufe liefern byte-identische Reihenfolge; es gibt keine Cursor-/Session-Pagination, keinen zweiten Suchbackend und keine `rg`-Produktionsabhängigkeit.
- [ ] Bestehende `FindSymbolScanner`-Dateiliste, Loading-/`isError`-Policy und alle bisherigen SearchPattern-/MCP-Vertragstests bleiben grün.
- [ ] C#-Semantic-Enrichment, umfassende öffentliche Dokumentationsaktualisierung und Performance-/`rg`-Messprototyp sind nicht in diesen Step gerutscht und bleiben als spätere Roadmap-Epics sichtbar.
- [ ] `codemap.md` ist mit dem realen Ergebnis aktualisiert; der Coder schreibt `step-result.md` und setzt den Step erst nach Build-/Test-Abschluss auf `done (pending audit)`.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` — bestehende Records-/Structured-Output-/Formatter-Muster wiederverwenden, Roslyn nur sparsam und in diesem Step gar nicht zur Scheinsicherheit einsetzen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — universelle Scope-/Filterlogik ohne AiNetLinter-, Projekt- oder Pfad-Hardcodings; kein neues Tool und kein dynamisches Backend.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` — PowerShell-/Windows-kompatible Pfade, zentrale Build-/Testbefehle und keine Produktionsabhängigkeit von `rg`.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3-Tests, `TestTempDirectory`, MCP-Verifikation über die vorhandene C#-Testinfrastruktur und Wahrung der Testparallelität.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — Result-/Recoverable-Pattern, DRY über bestehende Exclusion-/Truncation-Helper, keine stillen Catch-Blöcke, deterministische Byte-/Scope-Metriken und kein unnötiger Kommentar-/Abstraktionsdrift.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Grenzwerte` — nullable, `sealed` für konkrete neue Klassen/Records, kleine Methoden/Input-Records ab vier Parametern, Datei-/Methoden-/Komplexitätsbudgets und keine neuen Warnungen.
- `.agents/rules/AiNetLinter.mdc#agent-resilience`, `#architecture`, `#test-coverage` und `#general/DuplicateCode` — keine blockierenden synchronen Zugriffe im neuen Pfad, auflösbare Usings, Test-Sentinel für komplexe neue Typen, ASCII-/semantische Namen und keine neue Such-/Formatterduplikation.

## Notes

- Die Legacy-Kompatibilität ist eine harte Grenze: Für Aufrufe ohne neue Parameter müssen Textzeilen, `0 Treffer` und die bestehende `maxResults`-Meta-Zeile weiter wie bisher erscheinen; Structured Content wird additiv geliefert und nicht an Stelle des Textes.
- `maxResults` zählt Trefferzeilen, `maxFiles` physische Dateien und `matchRanges` die Bereiche innerhalb der sichtbaren Zeilen. Diese Begriffe müssen im Code und in Assertions getrennt bleiben, damit keine falsche Vollständigkeit signalisiert wird.
- Scope-/Filterwerte dürfen nur auf solution-relative, kanonisch normalisierte Pfade wirken. Reparse-/Worktree-/Outside-Root-Fälle sind Sicherheits- bzw. Completeness-Fälle, keine Gelegenheit für einen freien Dateisystem-Walk.
- Die neue Scanner-Schicht darf `GetFilesWithHits` nicht durch Kontext-/Antwortbudgets beschneiden, weil der bestehende `find_symbol`-Fallback weiterhin seine eigene Dateiliste benötigt.
- Keine produktive Roslyn-Semantik aus Text ableiten: `declaration`, `symbol_reference`, `ambiguous`, `unavailable` und ähnliche Kategorien gehören vollständig in den späteren `EPIC-04`-Step.
