---
status: done (pending audit)
type: step-plan
task: 04_repositoryweite-hybridsuche-und-kontextbudget
step: 003
corrects: null
title: "Opt-in C#-Roslyn-Enrichment und MCP-Vertrag synchronisieren"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-21T13:56:27+02:00
related_to:
  - step-002/step-result.md
  - step-002/step-review.md
---

# Step 003: Opt-in C#-Roslyn-Enrichment und MCP-Vertrag synchronisieren

## Bezug

- **Task:** `04_repositoryweite-hybridsuche-und-kontextbudget`
- **Primäres Epic:** `EPIC-04` — sichere, optionale C#-Syntax-/SemanticModel-Anreicherung auf dem bereits stabilisierten lexikalischen Ergebnis.
- **Kompatibel gebündelt:** Die für den neuen Parameter und die neuen `semantic`-Felder unvermeidbaren MCP-Registrierungs-, Overview-, Server-Instruktions- und öffentlichen Dokumentationsänderungen aus `EPIC-05`.
- **Konzept-Referenz:** „Must-have: optionale C#-Anreicherung ohne falsche Sicherheit“, Umsetzungsschritt 5 sowie die Dokumentations-/Vertragsteile der Definition of Done.

## Aktueller Projektzustand (JIT-Kontext)

- `SearchPatternScanner.Scan(...)` erzeugt bereits deterministisch sortierte `SearchPatternMatch`-Records mit solution-relativem Pfad, Zeile, MatchRanges, unverändertem Zeilentext, Kontext und Projektname. `SearchPatternTool.ExecuteAsync(...)` liefert dieses Payload additiv über `McpToolResults.Text<T>`; Text- und StructuredContent-Pfad dürfen daher nicht dupliziert werden.
- `SearchPatternScanner` hat 441 Type-LOC bei einem Limit von 500. Die Anreicherung wird deshalb in einen separaten, kleinen Helper ausgelagert; der lexikalische Scanner bleibt für Scope, Budgets, Cancellation und Legacy-Formatierung zuständig.
- Die geladene `Solution` enthält `Project.Documents`; vorhandene Roslyn-Muster verwenden `Document.GetSyntaxRootAsync(ct)`, `Document.GetSemanticModelAsync(ct)`, `SemanticModel.GetSymbolInfo(...)` und `ISymbol.TryGetDocCommentId()`. `FeatureContextScanner` liefert bereits relative Pfade, Projektbezug, stabile DocumentationCommentIds und sichere Fallbacks als wiederverwendbare Referenz, aber keine textbasierte Scheinsicherheit.
- Die aktuelle Suche arbeitet absichtlich auch auf Dateien außerhalb des Roslyn-Snapshots. Ein `.cs`-Treffer ohne zuordenbares geladenes `Document` muss daher als `unavailable` sichtbar werden, nicht als aufgelöstes Symbol.
- `AnalysisToolRegistrations.AddSearchPattern(...)`, `OverviewResourceRegistration.ToolSummaries`, `ServerInstructions.Text` und die Tabellen/Abschnitte in `README.md`, `Docs/agent-api.md` und `Docs/integration.md` beschreiben den neuen strukturierten lexikalischen Vertrag noch nicht vollständig. Die globale Server-Instruktion hat ein hart geprüftes UTF-8-Budget.
- `step-002` ist approved; die drei Findings sind behoben. Tech-Debt ist leer, daher gibt es kein opportunistisches Batch-Item und keine zusätzliche Debt-Arbeit.

## Intention

`search_pattern` soll bei expliziter Aktivierung mit `enrichCSharp=true` jeden sichtbaren Treffer deterministisch gegen den geladenen C#-Snapshot einordnen, ohne aus einem Texttreffer eine falsche Symbolreferenz abzuleiten. Die Ausgabe bleibt für alle bisherigen Aufrufe text- und strukturkompatibel; die neue Semantic-Information wird als additive, klar dokumentierte Nutzlast ausgeliefert. Die öffentlichen MCP-Beschreibungen werden in derselben Einheit auf den tatsächlich implementierten Vertrag synchronisiert.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScannerRecords.cs`

- **Was:** `SearchPatternToolArguments` und `SearchPatternScannerParameters` um ein opt-in `EnrichCSharp` ergänzen. `SearchPatternMatch` erhält ein optionales `SearchPatternSemantic?`-Feld; dafür immutable Records mit `Kind`, `Resolution` und optionaler stabiler `SymbolId` definieren.
- **Warum:** Der bestehende lexikalische Vertrag bleibt unverändert, während die Semantik maschinenlesbar und nur auf ausdrückliche Aktivierung sichtbar wird. Die Werte müssen die Konzeptkategorien `declaration`, `symbol_reference`, `comment`, `string`, `code`, `unknown` sowie `resolved`, `not_applicable`, `ambiguous` und `unavailable` abbilden.

### Datei 2: `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternRoslynEnricher.cs` (neu)

- **Was:** Einen separaten, internen Enricher für die bereits sichtbaren `SearchPatternMatch`-Records implementieren. C#-Dokumente über `Solution.Projects.SelectMany(project => project.Documents)` und kanonische Pfade zuordnen, Syntaxbaum und SemanticModel pro Dokument höchstens einmal laden und die Trefferbereiche auf Snapshot-Zeichenpositionen abbilden.
- **Was:** Kommentare und String-Literale ausschließlich als `comment`/`string` mit `resolution=not_applicable` markieren. Deklarationsknoten über `GetDeclaredSymbol` als `declaration` klassifizieren. Aufruf-/Identifier-Knoten nur dann als `symbol_reference` ausgeben, wenn `GetSymbolInfo` genau ein Symbol liefert; `TryGetDocCommentId()` liefert die stabile `symbolId`. Nicht eindeutig auflösbare Kandidaten werden `unknown`/`ambiguous`, fehlendes SemanticModel, fehlendes Document oder ein abweichender Disk-/Snapshot-Zeilentext `unknown`/`unavailable`.
- **Was:** Nicht-C#-Treffer und C#-Textstellen ohne symbolisch anwendbare Syntax explizit als `not_applicable` bzw. `unknown` nach dem dokumentierten Vertrag kennzeichnen. Die Zuordnung darf keine Semantik allein aus dem Matchtext oder aus einem Symbolnamen ableiten.
- **Was:** Enrichment nur nach der lexikalischen Sichtbarkeitsauswahl ausführen, Cancellation weiterreichen und pro Datei cachen. Bei Roslyn-/Snapshot-Grenzfällen recoverable im StructuredContent bleiben; keine zusätzliche Solution-Ladung, kein Cursor und kein Session-State.
- **Warum:** Die bestehende 499-Zeilen-nahe Scannerklasse bleibt unter ihrem Qualitätsbudget; Roslyn-Kosten entstehen nur opt-in und nur für sichtbare Treffer. Explizite Unschärfen verhindern falsche Sicherheit.

### Datei 3: `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs`

- **Was:** Den Enricher nach `SelectVisibleMatches(...)` nur bei `EnrichCSharp=true` aufrufen und die angereicherten Records in `SearchPatternPayload` übernehmen. Legacy-Formatter, `GetFilesWithHits`, Scope-/Budget-/Completeness-Zählungen und die Reihenfolge der Treffer unverändert lassen.
- **Warum:** Text und StructuredContent bleiben aus derselben sichtbaren Trefferliste abgeleitet; die Semantik ist eine additive Projektion und darf nicht die lexikalische Trefferentscheidung verändern.

### Datei 4: `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternTool.cs` und `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs`

- **Was:** Den optionalen Parameter `enrichCSharp` mit Default `false` durch Toolargumente und Scanner weiterreichen, im Toolnamen `search_pattern` registrieren und die Beschreibung auf opt-in, Snapshot-Grenze und explizite `unavailable`-/`ambiguous`-Fälle erweitern. Die bestehende Legacy-Signatur für direkte Aufrufer sowie die Parameter `pattern`, `isRegex`, `maxResults`, `maxFiles`, `contextLines`, `maxResponseBytes`, `scope`, `includePatterns` und `excludePatterns` bleiben erhalten.
- **Warum:** Kein neues Suchtool und kein Breaking Change; der Agent entscheidet bewusst, wann Roslyn-Kosten und SemanticContent benötigt werden.

### Datei 5: `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs` und `src/AiNetLinter/Mcp/ServerInstructions.cs`

- **Was:** Overview-Summary und globale Instruktion knapp um den opt-in-Workflow ergänzen: lexikalische Suche bleibt Standard/Fallback, `enrichCSharp=true` ordnet nur geladene C#-Dokumente ein, bei Trunkierung/Snapshot-Grenzen sind Folgeaufrufe oder Scope-Verfeinerung nötig. Das bestehende UTF-8-Limit der Instruktionen messen und einhalten.
- **Warum:** Tool-Discovery, Overview und globale Hinweise dürfen keine widersprüchliche Semantik oder unbelegte Vollständigkeit suggerieren.

### Datei 6: `src/AiNetLinter.FastTests/Mcp/Tools/Analysis/SearchPatternScannerTests.cs`

- **Was:** Unit-Regressionen ergänzen: `Scan_CSharpEnrichment_ResolvesDeclarationAndReference`, `Scan_CSharpEnrichment_DoesNotResolveCommentsOrStrings`, `Scan_CSharpEnrichment_MarksAmbiguousAndUnavailableCases` und `Scan_EnrichmentDisabled_LeavesSemanticFieldUnset`. Assertions prüfen Kind, Resolution, stabile DocumentationCommentId, Projektname, 1-basierte Positionen und unveränderte Legacy-/MatchRange-Daten.
- **Warum:** Die Kernlogik wird ohne MCP-Prozess und ohne externe Dateien geprüft; insbesondere werden False-Positive-Symbolreferenzen und Disk-/Snapshot-Abweichungen reproduzierbar abgesichert.

### Datei 7: `src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternToolTests.cs`, `McpServerCommandContractTests.cs` und `McpServerCommandJsonRpcFramingTests.cs`

- **Was:** Direkten Tooltest, SDK-/Registrierungstest und Raw-Wire-Test für `enrichCSharp=true` ergänzen. Prüfen, dass `structuredContent.semantic` ein Objekt mit dokumentierten Werten ist, `structuredContent` ein Objekt bleibt, Legacy-Text unverändert vorhanden ist und der Default-Aufruf keine Semantic-Anreicherung erzwingt.
- **Warum:** Scanner-Unit-Vertrag, tatsächliche Argumentbindung und JSON-RPC-Wire-Vertrag müssen getrennt regressionssicher sein.

### Datei 8: `src/AiNetLinter.FastTests/Mcp/OverviewResourceRegistrationTests.cs`, `McpServerOptionsFactoryTests.cs` und `src/AiNetLinter.IntegrationTests/Mcp/McpDocumentationSmokeTests.cs`

- **Was:** Tool-/Overview-Parität, Instruktions-UTF-8-Budget und die öffentlich dokumentierten Namen/Parameter auf den neuen Vertrag prüfen; keine testseitige Zwangsserialisierung einführen.
- **Warum:** Die gebündelte EPIC-05-Arbeit darf Discovery und Dokumentationsoberfläche nicht vom implementierten Toolschema entkoppeln.

### Datei 9: `README.md`, `Docs/agent-api.md`, `Docs/integration.md`, `Docs/ROADMAP.md` und bei bestehender Vertragsreferenz `Docs/configuration.md`

- **Was:** Nur implementierte Felder und Semantik dokumentieren: `enrichCSharp`, `semantic.kind`, `semantic.resolution`, `symbolId`, Snapshot-/Projektgrenze, Default `false`, Legacy-Text, Limits und die vorgesehenen Folgewege bei `ambiguous`/`unavailable`/Trunkierung. Den Agenten-Workflow mit `find_symbol`/`get_feature_context` vor `search_pattern` und optionalem `enrichCSharp` konsistent halten; `rg` ausdrücklich erlaubt lassen.
- **Warum:** EPIC-05 wird genau für den tatsächlich gebauten Vertrag synchronisiert. Keine behauptete Tokenersparnis, keine Dokumentation des noch offenen EPIC-06 und keine Erweiterung zu RAG/Ranking.

### Datei 10: `tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget/codemap.md`

- **Was:** Der Coder aktualisiert die CodeMap im separaten Doku-Commit um den neuen Enricher, Semantic-Record und Test-/Dokumentationsanker; bestehende Einträge werden nicht stillschweigend gelöscht.
- **Warum:** Der nächste JIT-Planer muss die tatsächlich entstandene Trennung zwischen lexikalischem Scan, Roslyn-Enrichment und öffentlichem Vertrag sehen.

## Nicht-Ziele und harte Grenzen

- Kein neues Suchtool, kein Breaking Change am Legacy-Text, kein Entfernen von `structuredContent` oder `GetFilesWithHits`.
- Kein verpflichtender SemanticModel-Aufruf für Standard-Suchen; `enrichCSharp` bleibt opt-in und verändert weder Treffer-, Scope- noch Budgetzählung.
- Keine semantische Behauptung aus einem bloßen Textmatch; Kommentare, Strings, mehrdeutige Kandidaten und nicht geladene `.cs`-Dateien bleiben explizit markiert.
- Kein RAG, keine Embeddings, kein LLM-Ranking, kein Cursor-/Session-State, keine neue Solution-Ladung und kein Produktions-`rg`-Backend.
- EPIC-06-Messungen, Benchmark-Fixtures, Folgeaufruf-Evaluation und eine Entscheidung über einen diagnostischen `rg`-Vergleich werden nicht vorgezogen.
- Keine Änderungen unter `tasks/mcp-server-weiterentwicklung`.

## Tests und Verifikation

- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` — schnelle Roslyn-Enrichment-, Range- und Default-Kompatibilitätsregressionen.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~SearchPatternScannerTests` — gezielter Scannerlauf mit deklarierter, referenzierter, Kommentar-/String-, ambiger und unavailable Fixture.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~SearchPatternToolTests` — direkte Toolargumente, StructuredContent und Legacy-Text.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandContractTests` — SDK-Schema und Argumentbindung.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandJsonRpcFramingTests` — Raw-Wire-Objekt und Textkompatibilität.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~OverviewResourceRegistrationTests` und `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~McpServerOptionsFactoryTests` — Discovery-/Instruktionsparität und UTF-8-Budget.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpDocumentationSmokeTests` — öffentliche MCP-Doku bleibt konsistent.
- [ ] `dotnet build` — alle Projekte warnungsfrei bei `TreatWarningsAsErrors`.
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json --path .` — projektinterner Lint-/Regelcheck; bei Regeländerungen zusätzlich Agent-Regel-Sync, in diesem Step aber keine `rules.json`-Änderung planen.
- [ ] Abschluss-Gate: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.

## Akzeptanzkriterien / Definition of Done

- [ ] `enrichCSharp=false` erhält byte-/textkompatibles Legacy-Verhalten und die bisherige StructuredContent-Reihenfolge; neue Semantic-Felder sind nicht irreführend befüllt.
- [ ] `enrichCSharp=true` liefert für sichtbare C#-Treffer deterministische Kategorien und Resolutionszustände; sichere Deklarationen/Referenzen enthalten, soweit vorhanden, eine stabile DocumentationCommentId und den Projektbezug.
- [ ] Kommentare und Strings werden nicht als `symbol_reference` ausgegeben; mehrdeutige, nicht geladene oder Snapshot-abweichende Fälle sind als `ambiguous`/`unavailable` nachvollziehbar.
- [ ] Nicht-C#-Dateien bleiben lexikalisch suchbar und werden bei aktiviertem Enrichment als nicht anwendbare Semantik kenntlich gemacht.
- [ ] Scope, Limits, Completeness, Cancellation, Regex-Timeout und Legacy-Miss-Hint bleiben unverändert funktionsfähig; Roslyn-Ausfälle machen keinen erwartbaren Tool-Call zum globalen `isError`.
- [ ] Toolregistrierung, Overview, Server-Instruktionen, README, `Docs/agent-api.md`, `Docs/integration.md` und relevante Roadmap-/Smoke-Test-Verweise beschreiben ausschließlich den implementierten Vertrag.
- [ ] `codemap.md`, `step-003/step-result.md` und der Status des Step-Plans werden nach Code-/Testabschluss aktualisiert; `step-result.md` referenziert den Code-Commit.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` — bestehende Roslyn-/Structured-Output-Muster wiederverwenden, Semantik sparsam und opt-in berechnen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — keine repo-spezifischen Pfade, keine neue Tool-/Plugin-/Backend-Abhängigkeit und keine falsche Semantik aus Text.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` — PowerShell-kompatible Tests, MCP-Dogfooding über C#-Testinfrastruktur, UTF-8-/Build-Verifikation.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3-Regressionen, Testparallelität bewahren, zentrale Fixture-/Testinfrastruktur und Doku-Synchronisation.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — kleine Helper, immutable Records, keine stillen Catch-Blöcke, Metrik-/Violation-Gate und keine unbelegten Doku-Wirkungsbehauptungen.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil`, `#Grenzwerte`, `#agent-resilience`, `#test-coverage` — Größenbudget der Scannerklasse einhalten, Cancellation/Fehlerpfade sichtbar halten und neue Logik testen.

## Commit- und Review-Hinweise

- Der Coder erstellt einen Code-/Test-Commit und einen separaten Doku-/Step-Commit; beide Subjects sind deutsche imperative Conventional Commits mit dem Suffix `[04_repositoryweite-hybridsuche-und-kontextbudget]`. Kein Amend, Rebase oder Push.
- Der Code-Commit darf nur die produktiven Enrichment-/MCP-Dateien, die zugehörigen Tests und die notwendigen öffentlichen Dokumentationsänderungen dieses Steps enthalten; `tasks/mcp-server-weiterentwicklung` bleibt vollständig außerhalb.
- Der Kritiker prüft pro Ebene insbesondere Default-Kompatibilität, Roslyn-Snapshot-Grenzen, MatchRange-zu-Syntax-Position, eindeutige Symbolauflösung, False-Positive-Schutz, UTF-8-Instruktionsbudget, Doku-Objektivität und die vollständigen Nicht-Stress-Gates. `issues` ist nur bei CRITICAL/MAJOR zu setzen; außerhalb des Scopes beobachtete Architektur- oder DRY-Punkte gehören in `tech-debt.md`, nicht automatisch in einen Fix-Step.

## Bekannte Ausnahmen

Keine bekannten Ausnahmen. Der optionale `rg`-Vergleich und die Wirksamkeitsmessung sind bewusst als EPIC-06-Abhängigkeit ausgenommen.

## Notes

- Die gemeinsame `SearchPatternMatch`-Struktur ist die einzige Quelle für Legacy-Text und StructuredContent; der Enricher darf keine zweite Trefferenumeration einführen.
- Das geplante `semantic`-Schema muss in Code und Doku explizit zwischen `kind` und `resolution` unterscheiden, damit ein Kommentar/String nicht mit „unaufgelöstes Symbol“ verwechselt wird.
- Falls der tatsächliche Roslyn-Snapshot keine sichere Zuordnung erlaubt, ist `unavailable` die korrekte Ausgabe. Ein `unknown`-/`ambiguous`-Ergebnis darf keine Folgeaktion wie `find_references` automatisch suggerieren.
- Tech-Debt bleibt leer, solange der Kritiker keine neue Beobachtung mit `auto_fixable: ja` dokumentiert; eine proaktive Debt-Sweep-Erweiterung ist nicht Teil dieses Plans.
