---
status: done (pending audit)
type: step-plan
task: 04_repositoryweite-hybridsuche-und-kontextbudget
step: 005
corrects: null
title: "Wirksamkeits-, Performance- und Abschlussvalidierung"
epic: EPIC-06
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: GPT-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-21T15:24:48+02:00
related_to:
  - step-004/step-result.md
  - step-004/step-review.md
  - tech-debt.md
---

# Step 005: Wirksamkeits-, Performance- und Abschlussvalidierung

## Bezug

- **Task:** `04_repositoryweite-hybridsuche-und-kontextbudget`
- **Epic:** `EPIC-06` aus `roadmap.md` — die implementierte hybride `search_pattern`-Nutzlast wird gegen reproduzierbare Fixture-Oracles und kontrollierte Budgets validiert; erst danach wird über belastbare Dokumentationsaussagen entschieden.
- **Konzept-Referenz:** `04_repositoryweite-hybridsuche-und-kontextbudget.md`, Abschnitte „Priorität und erwarteter Nutzen“, „Nice-to-have“, „Bewusste Abgrenzung zu `rg`/`grep`“, „Umsetzungsschritte“ 6–7, „Tests und Messungen“ sowie „Definition of Done“.

## Aktueller Projektzustand (JIT-Kontext)

- `SearchPatternScanner.Scan(...)` ist die einzige lexikalische Enumeration und erzeugt bereits `SearchPatternPayload` mit MatchRanges, Kontext, Scope-/Snapshot-Metadaten, Completeness, Sichtbar-/Gesamtzahlen, Skip-Zählern, Regex-Timeout und Cancellation.
- `SearchPatternScannerEnrichment.ScanAsync(...)` reichert nur die bereits sichtbare Matchliste an. Der approved Fix aus `step-004` gibt bei Roslyn-Cancellation den vorhandenen lexicalen Payload recoverable zurück; ein zweiter Scanner-/Dateisystem-Scan ist damit ausgeschlossen.
- `SearchPatternTool` liefert Legacy-Text und Structured Content gemeinsam über `McpToolResults.Text<T>`. Das `maxResponseBytes`-Limit bezieht sich auf die serialisierte Structured-Payload; die Evaluierung muss deshalb Payload-Bytes und die tatsächlich kombinierte Toolantwort getrennt ausweisen.
- `src/AiNetLinter.FastTests/Mcp/Tools/Analysis/SearchPatternScannerTests.cs` deckt bereits Ranges, Regex, Kontext, Scope, Budgets, Encoding/Binary/Unreadable, Regex-Timeout, Cancellation und den Enrichment-Cancellation-Fallback ab. `src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternToolTests.cs` deckt Legacy-Text, Structured Content, gemischte Fixture-Dateien, Filter, Budgets und C#-Enrichment ab.
- Die neutrale Fixture `tests/Fixtures/SymbolGraphMini` enthält `Greeter.cs`, weitere C#-Dateien, `search-fixture.md`, `wwwroot/search-fixture.json`, `site.js`, `styles.css`, `Component.razor`, `Page.xaml` und `index.html`. Für problematische Fälle werden in einer isolierten Fixture-Kopie nur testseitig `obj`-/generierte Dateien, `.min.*`, eine Binärdatei mit NUL-Byte, invalides UTF-8 und eine begrenzte große Textdatei erzeugt; die statische gemeinsame Fixture wird nicht dauerhaft mit Testartefakten angereichert.
- Der Drift-Audit wurde mit `find_duplicates(scopeDir="src", minTokens=20)` sowie dem strukturellen Scan ausgeführt. Es gibt keinen Exact-Clone und keinen neuen mechanisch sicheren auto-fixbaren Befund im Such-/MCP-Scope; die geprüften Near-/Structural-Kandidaten sind Testvarianten, Enrichment-/Test-Hooks oder außerhalb dieses Tasks liegende Architekturcluster. Es wird keine opportunistische Konsolidierung geplant.

## Intention

Dieser Step baut eine reproduzierbare Evaluations- und Messabdeckung für die bereits implementierte Suche auf. Ein unbudgetierter Fixture-Oracle liefert die erwartete Treffer-/Dateimenge; budgetierte, kontextreiche, semantisch angereicherte, abgebrochene und problematische Läufe werden dagegen mit sichtbaren Verlustgründen, UTF-8-Bytes, Laufzeitverteilungen und expliziten Folgeaufrufen verglichen.

Die Entscheidung bleibt proxy- und evidenzbasiert: Es werden keine Tokenersparnis, keine allgemeine Performanceüberlegenheit und keine `rg`-Parität behauptet. Öffentliche Dokumentation wird nur geändert, wenn die Wiederholungsmessung eine belastbare, klar begrenzte Aussage trägt; ansonsten dokumentiert ausschließlich `step-005/step-result.md` die Ergebnisse und Unsicherheiten.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.FastTests/Mcp/Tools/Analysis/SearchPatternScannerEvaluationTests.cs` (neu)

- **Was:** Einen fokussierten Unit-Evaluationsharness auf Basis von `TestTempDirectory`, `RoslynTestSolution` und dem bestehenden `SearchPatternScanner`-/`SearchPatternScannerEnrichment`-Pfad anlegen. Die Datei erzeugt pro Test eine isolierte Fixture-Kopie bzw. testseitige Overlay-Dateien und räumt sie über die zentrale TestKit-Infrastruktur auf.
- **Was:** Messfälle für Plain-Text, Regex, mehrere MatchRanges, Kontext, `enrichCSharp=true`, `maxResults`, `maxFiles`, `maxResponseBytes`, invalides UTF-8/Binary, generierte/`obj`-/`.min.*`-Dateien, Regex-Timeout sowie Pre- und Post-Lexical-Cancellation definieren. Die vorhandene Regression `Scan_CSharpEnrichmentCancellation_ReusesLexicalPayloadWithoutRescan` bleibt der harte Einmal-Scan-Beleg und wird nicht durch eine neue Test-Doppelstruktur dupliziert.
- **Was:** Für jeden Fall einen unbudgetierten Oracle-Lauf (`maxResults=0`, `maxFiles=0`, `maxResponseBytes=0`, `contextLines=0`) und danach den budgetierten Lauf erfassen. Verglichen werden erwartete Dateipfade/Zeilen/Ranges, `MatchedFileCount` versus `ShownMatchedFileCount`, `TotalMatchedLineCount` versus `ShownMatchedLineCount`, `Skipped*`-Zähler, `scanCompleted`, `truncatedBy`, `CancellationRequested` und `RegexTimedOut`.
- **Warum:** Die Tests beweisen, dass ein sichtbarer Treffer-/Dateiverlust entweder durch das angeforderte Budget oder durch einen transparent ausgewiesenen Problemzustand erklärt ist. Ein bloß kleinerer Output wird nicht als Wirksamkeitsgewinn gewertet.

### Datei 2: `src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternEvaluationTests.cs` (neu)

- **Was:** Einen direkten Tool-Evaluationslauf gegen `SymbolGraphMiniFixtureWorkspace` und `LoadedFixture` ergänzen. Die vorhandenen statischen Dateien `search-fixture.md`, `wwwroot/search-fixture.json`, `Greeter.cs`, `site.js`, `styles.css`, `Component.razor`, `Page.xaml` und `index.html` bilden die gemischte Dateityp-Baseline; problematische Dateien werden nur in der isolierten Fixture-Kopie des jeweiligen Tests ergänzt.
- **Was:** Pro Szenario Legacy-Text (`TextContentBlock.Text`), Structured-Payload (`StructuredContent.GetRawText()`), Treffer-/Dateizahlen und `Completeness` auslesen. Die Messung schreibt keine Rohdaten in das Repository, sondern erzeugt eine kompakte, in `step-result.md` übertragbare Falltabelle.
- **Was:** Einen kleinen, zustandslosen Folgeaufruf-Loop testen: ein absichtlich begrenzter breiter Aufruf (`maxFiles`/`maxResults` oder `maxResponseBytes`) wird nur dann durch einen zweiten Aufruf mit engerem `scope`-/Include-Filter fortgesetzt, wenn `Completeness.truncated=true` und der erwartete Zieltreffer noch fehlt. Ein unbudgetierter Oracle-Fall muss die erwartete Zielmenge in einem Aufruf liefern.
- **Warum:** Die Zahl „notwendiger Folgeaufrufe“ wird als reproduzierbarer Fixture-Proxy für genau definierte Suchziele gemessen, ohne Cursor-, Session- oder serverseitigen Zustand einzuführen.

### Datei 3: Bestehende Fixture-Dateien und testseitige Problemdateien

- **Was:** Die bestehenden Anker `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/search-fixture.md` und `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/search-fixture.json` für wiederholte Marker, Kontext und mehrere Bereiche verwenden; `Greeter.cs`/weitere C#-Dateien für Legacy-/Enrichment-Fälle sowie `wwwroot/site.js`, `styles.css`, `Component.razor`, `Page.xaml` und `index.html` für Nicht-C#-Reichweite verwenden.
- **Was:** Im Testlauf kontrolliert zusätzliche Dateien unterhalb der isolierten Fixture-Kopie anlegen: `obj/Debug/Generated.cs`, eine generierte Datei wie `Generated.g.cs` außerhalb von `obj`, `bundle.min.js`, `binary-anchor.bin` mit NUL-Byte, `invalid-encoding.dat` mit ungültiger UTF-8-Sequenz und eine begrenzte `large-search.txt` mit wiederholten Markern. Falls ein Regex-Timeout-Fall verwendet wird, muss das bestehende deterministische Timeout-Muster aus den Scanner-Tests wiederverwendet werden.
- **Warum:** Die problematischen Dateien prüfen Ausschluss-, Binary-, Encoding-, Größen-, Timeout- und Completeness-Zähler ohne dauerhaft veränderte gemeinsame Fixtures, Betriebssystem-ACL-Tricks oder Ad-hoc-OS-Temp-Pfade.

### Datei 4: `step-005/step-result.md` (durch den Coder zu schreiben)

- **Was:** Die Ergebnisse in einer festen Tabelle dokumentieren. Pflichtschema je Fall: `caseId`, Fixture-/Snapshot-Quelle, `pattern`, `isRegex`, `enrichCSharp`, Scope/Include/Exclude, Limits, Oracle-Dateien/-Zeilen, sichtbare Dateien/-Zeilen, Verlustdateien/-zeilen, `truncatedBy`/Skip-/Cancellation-/Timeout-Status, `legacyUtf8Bytes`, `structuredPayloadUtf8Bytes`, `combinedToolUtf8Bytes`, Warmup-/Iterationszahl, `min/median/p95` in Millisekunden, `followUpCalls` und optionaler `rgStatus`.
- **Was:** Zusätzlich die Messbedingungen (Build-Konfiguration, Wiederholungszahl, Fixture-Größe, geladenes Snapshot-Projekt) und eine explizite Einstufung `bestätigt`, `nicht bestätigt` oder `nicht entscheidbar` je Aussage festhalten. `structuredPayloadUtf8Bytes <= maxResponseBytes` wird nur für das Structured-Payload-Limit geprüft; `combinedToolUtf8Bytes` wird separat berichtet und nicht fälschlich als Limitsemantik dokumentiert.
- **Warum:** Die Step-Historie enthält damit reproduzierbare Proxies und keine modell-/hostabhängigen Tokenbehauptungen.

### Datei 5: `README.md` und `Docs/ROADMAP.md` (nur bedingt)

- **Was:** Nur wenn mindestens drei unabhängige Wiederholungen unter identischen Fixture-/Build-Bedingungen dieselbe begrenzte Aussage tragen, die konkrete Aussage mit Messbereich und Einschränkung ergänzen. Beispiele sind ein bestätigter Completeness-Folgeweg oder eine reproduzierbare Byte-/Payload-Eigenschaft; keine unbelegte Aussage wie „spart Tokens“ oder „ist schneller“.
- **Was:** Bei Streuung, fehlender `rg`-Installation, nicht reproduzierbarem Timeout oder rein fixture-spezifischem Ergebnis keine öffentliche Änderung vornehmen; die Unsicherheit bleibt ausschließlich in `step-result.md`.
- **Warum:** Der Dokumentations-Workflow verlangt sachliche, implementierte und belegte Aussagen. EPIC-06 darf keine Evaluationsergebnisse in allgemeine Produktversprechen umdeuten.

## Messschema und Entscheidungskriterien

- **Oracle:** Der unbudgetierte Lauf auf demselben residenten Snapshot ist die Referenzmenge. Erwartete Pfade/Zeilen/Ranges werden aus der bekannten Fixture-Konfiguration abgeleitet; absichtlich ausgeschlossene generierte/Binary-/invaliden Dateien sind als Skip-Fälle und nicht als verlorene Treffer zu zählen.
- **Antwortgröße:** `Encoding.UTF8.GetByteCount(legacyText)`, UTF-8-Bytezahl des Structured-JSON-Rohtexts und deren Summe getrennt erfassen. Es wird keine Tokenzahl berechnet oder aus Bytes abgeleitet.
- **Treffer-/Dateiverlust:** `oracleTotal - shown` je Zeilen-/Dateizähler bestimmen. Jeder Verlust muss durch `truncatedBy` (`maxResults`, `maxFiles`, `maxResponseBytes`, `cancellation`, `regexTimeout`, `enumerationError`) oder einen passenden Skip-Zähler erklärbar sein; unerklärter Verlust ist ein harter Befund.
- **Laufzeit:** Nach einem Warmup mindestens sieben Messiterationen je Fall ausführen und `min`, `median` und `p95` mittels `Stopwatch` dokumentieren. Absolute Werte gelten nur für die konkrete Fixture-/Umgebungsbeschreibung; eine öffentliche Performanceaussage ist nur bei stabiler Wiederholung zulässig.
- **Cancellation/Abbruch:** Vor der Enumeration und während der Enrichment-Grenze prüfen. Erwartet werden der bereits vorhandene Payload, `ScanCompleted=false`, `CancellationRequested=true`, `truncatedBy` mit `cancellation` und kein zweiter Scanner-/Dateisystemlauf.
- **Folgeaufrufe:** Für jedes definierte Ziel die minimal nötige Anzahl unabhängiger Aufrufe im Testprotokoll zählen. Ein Folgeaufruf ist nur dann „notwendig“ im Proxy-Sinn, wenn der erste Aufruf durch ein angefordertes Budget trunciert und das Ziel im Oracle noch nicht sichtbar ist; daraus wird keine allgemeine Agentenquote abgeleitet.
- **Entscheidung:** `bestätigt`, wenn alle Oracle-/Completeness-/Cancellation-Invarianten erfüllt und die Messwerte unter gleichen Bedingungen wiederholbar sind; `nicht bestätigt`, wenn ein harter Invariantenfehler vorliegt; `nicht entscheidbar`, wenn nur die Laufzeitstreuung oder externe Toolverfügbarkeit unklar ist. Performance- und Tokenversprechen sind ohne stabile Evidenz ausdrücklich `nicht entscheidbar`.

## Optionaler diagnostischer `rg`-Vergleich

- `rg` wird im Normaltestlauf weder vorausgesetzt noch aus Produktionscode gestartet. Nur wenn der verwaltete Scanner bei der Evaluierung eine ungeklärte Reichweiten- oder Laufzeitfrage offenlässt, darf test-/diagnostikseitig die verfügbare Binary geprüft und ein neutraler Vergleich ausgeführt werden.
- Vergleichsmenge sind normalisierte relative Pfade, 1-basierte Zeilen und Trefferanzahlen; `rg`-JSON-Bytes, Shell-Ausgabe und PCRE2-Details sind kein MCP-Vertrag. Nicht installiert oder nicht ausführbar wird als `not-run` dokumentiert und ist kein Fehler.
- Beispiel für den manuellen, Windows-kompatiblen Diagnoseaufruf, nur auf der isolierten Fixture-Kopie:
  `rg --json --ignore-case --glob '!obj/**' --glob '!**/*.min.*' "search-anchor" <fixture-root>`
- Es darf keine Produktionsabhängigkeit, kein Pflicht-Test-Gate und keine neue Suchfunktion aus diesem Vergleich entstehen.

## Nicht-Ziele und Step-Grenzen

- Keine neue Suchfunktion oder Toolregistrierung, kein alternatives Produktionsbackend und keine Änderung an `SearchPatternScanner`-/MCP-Vertragssemantik ohne reproduzierten Evaluationsbefund.
- Kein RAG, keine Embeddings, kein LLM-Ranking, kein Semantic Kernel, kein Cursor-/Session-State und keine serverseitige Folgeaufrufverwaltung.
- Keine Modelltokenmessung, keine allgemeine Performancegarantie und kein `rg`-Zwang.
- Keine opportunistische Konsolidierung der außerhalb des Such-/MCP-Scopes liegenden Drift-Audit-Cluster und kein neuer Tech-Debt-Step daraus.
- Keine Änderungen, kein Lesen, kein Wiederherstellen und kein Staging unter `tasks/mcp-server-weiterentwicklung`.
- Öffentliche Docs nur bei belastbarer Evaluation; keine Dokuänderung allein wegen einer nicht entscheidbaren Messung.

## Tests und Verifikation

- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~SearchPatternScannerEvaluationTests"` — Oracle-, Problemdatei-, Budget-, Byte- und Cancellation-Messfälle.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~SearchPatternScannerTests"` — bestehende Scanner-/Enrichment-Regressionsabdeckung einschließlich Einmal-Scan-Cancellation.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~SearchPatternEvaluationTests"` — Legacy-/Structured-Bytes, gemischte Dateitypen und definierte Folgeaufrufe.
- [ ] `dotnet build` — alle Projekte ohne Fehler und Warnungen.
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json --path .` — projektinterner Lint-/Violation-Lauf; bereits bekannte gitignorierte `temp`-Artefakte getrennt vom Step-Befund ausweisen.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — vollständiges Fast-Test-Gate.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — vollständiges Integrations-Gate.
- [ ] Optionaler manueller `rg`-Vergleich nur nach den oben genannten Kriterien; Ergebnis `run` oder `not-run` dokumentieren, niemals als Pflicht-Gate behandeln.

## Definition of Done

- [ ] Die gemischte `SymbolGraphMini`-Baseline und die isolierten problematischen Dateien werden über wiederholbare Oracle-/Budgetfälle ausgewertet.
- [ ] Antwortgröße, Treffer-/Dateiverlust, Laufzeitverteilung, Cancellation-/Abbruchstatus und definierte Folgeaufrufe sind mit dem Messschema dokumentiert; keine Tokenbehauptung ist enthalten.
- [ ] Jeder absichtliche Treffer-/Dateiverlust ist durch Budget, Skip- oder Abbruchmetadaten erklärt; unerklärter Verlust blockiert die Freigabe dieses Steps.
- [ ] Legacy-Text bleibt vorhanden, Structured Content bleibt ein Objekt, Wiederholung liefert stabile Reihenfolge, und der approved Einmal-Scan-Cancellation-Vertrag bleibt grün.
- [ ] Der optionale `rg`-Vergleich ist entweder begründet ausgeführt oder als `not-run` dokumentiert und hat keine Produktions-/Pflichttestabhängigkeit erzeugt.
- [ ] README/`Docs/ROADMAP.md` wurden nur bei belastbaren Aussagen geändert; andernfalls sind keine öffentlichen Dokuänderungen nötig.
- [ ] Build, beide vollständigen Non-Stress-Gates und der projektinterne Lint-Lauf sind grün bzw. bekannte gitignorierte `temp`-Artefakte separat ausgewiesen.
- [ ] `step-005/step-result.md` enthält die Messmatrix, Bedingungen, Entscheidung und Restunsicherheiten; der Step wird erst danach auf `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` — nur implementierte, messbare Aussagen dokumentieren; vorhandene MCP-/Scanner-Strukturen wiederverwenden.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` — PowerShell-/Windows-kompatible Commands, zentrale `TestTempDirectory`-Nutzung und MCP-/Dogfood-Tests über C#-Infrastruktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3, vollständige Non-Stress-Gates, Testparallelität und `### Commit-Vorschlag`-Pflicht in der Abschlussantwort.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — keine unbelegten Qualitätsurteile, keine neue Duplikation, Cancellation-/Result-Transparenz und keine opportunistische Tech-Debt-Ausweitung.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Grenzwerte` — neue Test-/Messrecords klein und immutable halten; keine Warnungen, Größen- oder Komplexitätsdrift.
- `.agents/rules/AiNetLinter.mdc#agent-resilience`, `#test-coverage` und `#general/DuplicateCode` — keine stillen Fehlerpfade, Testabdeckung für neue Evaluationslogik und keine zweite Such-/Formatterlogik.

## Review-Hinweise

- Prüfe zuerst die Oracle-Definition und ob absichtlich ausgeschlossene Dateien nicht fälschlich als Trefferverlust gezählt werden.
- Prüfe getrennt `structuredPayloadUtf8Bytes` gegen `maxResponseBytes` und `combinedToolUtf8Bytes`; eine kleinere Gesamtantwort ist kein Beweis für Tokenersparnis.
- Prüfe, dass Messungen keine testabhängige globale Serialisierung, keinen OS-Temp-Pfad und keine `rg`-Verfügbarkeitsannahme einführen.
- Prüfe den Cancellation-Fall auf vorhandene Matchliste, unveränderte Zählungen und genau eine Enumeration; kein Rescan und kein Session-State.
- Prüfe Folgeaufrufe als explizite Fixture-Proxies mit begrenztem Ziel, nicht als allgemeine Agenten- oder Produktivitätsversprechen.
- Prüfe, dass nur tatsächlich belastbare Ergebnisse in README/`Docs/ROADMAP.md` gelangen und `tasks/mcp-server-weiterentwicklung` vollständig außerhalb des Arbeitsbereichs bleibt.

## Commit- und Übergabehinweise

- Der Coder committe zukünftige Code-/Teständerungen und die Ergebnisdokumentation getrennt nach Projektkonvention; jeder Subject ist deutsch, imperativ, Conventional Commit und trägt `[04_repositoryweite-hybridsuche-und-kontextbudget]`.
- Dieser Planungs-Commit enthält ausschließlich `roadmap.md`, `step-005/step-plan.md` und den workflowbedingt aktualisierten `task-state.md`.
