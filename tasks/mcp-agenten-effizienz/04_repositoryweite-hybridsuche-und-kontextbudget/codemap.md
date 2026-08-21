---
task: 04_repositoryweite-hybridsuche-und-kontextbudget
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-21
---

# CodeMap: Repositoryweite Hybridsuche und Kontextbudget

Die Einträge sind Pointer auf bestehende Anker für die spätere Umsetzung und Prüfung; sie beschreiben die Relevanz des Ortes, nicht die geplanten Implementierungsschritte.

## MCP-Tool und Ergebnisvertrag

- **`src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternTool.cs`** — Validierung, Loading-/Fehlerpfad und bestehende asynchrone Legacy-Textausgabe des `search_pattern`-Tools.
- **`src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs`** — Zentraler repositoryweiter Scanner für deterministische Treffer, Scope, Filter und Kontext-/Antwortbudgets.
- **`src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScannerEnrichment.cs`** — Koordiniert die optionale Anreicherung der bereits sichtbaren Match-Liste ohne zweite Trefferenumeration und liefert bei Roslyn-Cancellation den lexicalResult-Payload recoverable mit sichtbarem Cancellation-Status zurück.
- **`src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternRoslynEnricher.cs`** — Kleine, cancellation-aware Roslyn-Anreicherung mit sicherer Dokument-/Snapshot-Zuordnung, Symbolauflösung und per-Datei-Cache.
- **`src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScannerRecords.cs`** — Interne Records für Scanneroptionen, immutable semantische Trefferfelder, Legacy-Status, MatchRanges, Scope-Metadaten und Completeness.
- **`src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScannerCompleteness.cs`** — Aggregation von Sichtbar-/Gesamtzahlen, übersprungenen Dateien und Truncation-Gründen.
- **`src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternLegacyFormatter.cs`** — Reine Legacy-Textformatierung aus der sichtbaren strukturierten Trefferliste.
- **`src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternLegacyFileHitScanner.cs`** — Unbudgetierter Legacy-Dateitrefferpfad für `GetFilesWithHits` mit auswertbarem Datei- und Regex-Status.
- **`src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs`** — MCP-Registrierung, Tool-Signatur und Beschreibung von `search_pattern`.
- **`src/AiNetLinter/Mcp/McpToolResults.cs`** — Gemeinsamer Rückgabemechanismus für Legacy-Text, strukturierte Top-Level-Nutzlasten und Fehlerzustände.
- **`src/AiNetLinter/Mcp/McpTruncation.cs`** — Bestehende Text- und Dateilisten-Trunkierung samt kompatibler Meta-Hinweise.
- **`src/AiNetLinter/Mcp/IsErrorPolicy.md`** — Dokumentierte Zuordnung von MCP-Fehlern, Recoverable-Ergebnissen und Loading-Zuständen.
- **`src/AiNetLinter/Mcp/McpServerOptionsFactory.cs`** — Zusammensetzung der registrierten Tools und Ressourcen für den MCP-Server.
- **`src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`** — Tool-Übersicht, Ressourceninhalt und Parität zwischen registrierten Tools und Overview.
- **`src/AiNetLinter/Mcp/ServerInstructions.cs`** — Kompakte globale Agentenhinweise einschließlich Dateityp-Fallback und UTF-8-Budget.
- **`src/AiNetLinter/Mcp/McpCompileDiagnostics.cs`** — Aggregierte Compile-Warnungen, die vor Toolantworten berücksichtigt werden.

## Scope, Dateisystem und Roslyn

- **`src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs`** — Zentrale Generated-/Ausschluss- und cancellation-aware Enumerationslogik für freie Dateisystemläufe.
- **`src/AiNetLinter/Web/WebFileCatalog.cs`** — Bestehende projektbezogene Verzeichnisse und mehrsprachige Web-Dateikatalogisierung.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetIndexScopeScanner.cs`** — Vorhandene Dateityp- und Scope-Inventarisierung über Roslyn- und Dateisystemquellen.
- **`src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextScanner.cs`** — Roslyn-Positions-, Dokumentations-ID-, Projekt- und strukturierte Kontextmuster für optionale Anreicherung.
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolScanner.cs`** — Legacy-Dateisuche und Statusformatierung für Hinweise bei fehlenden C#-Symboltreffern.
- **`src/AiNetLinter/Mcp/McpCodeGraphServer.cs`** — Resident geladener Solution-Snapshot und zentrale Grundlage für Aktualität und Scope der Analyse.
- **`src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScannerRecords.cs`** — Bestehende Records und Nutzlastmuster für strukturierte Treffer mit Zeilen-/Spaltenangaben.

## Tests, Fixtures und Wire-Verträge

- **`src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternToolTests.cs`** — Aktuelle direkte Tests für Plain-/Regex-Suche, Limits, Ausschlüsse, ungültige Eingaben, Legacy-Text und opt-in-Semantik.
- **`src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternEvaluationTests.cs`** — LoadedFixture-Harness für gemischte Dateitypen, Legacy-/Structured-UTF-8-Bytes und den definierten Folgeaufruf-Proxy (zuletzt: step-005).
- **`src/AiNetLinter.FastTests/Mcp/Tools/Analysis/SearchPatternScannerTests.cs`** — Schnelle Scanner-/Formatter-/Roslyn-Regressionen für Ranges, Kontext, Scope, Encoding, Cancellation, Completeness und semantische Kategorien.
- **`src/AiNetLinter.FastTests/Mcp/Tools/Analysis/SearchPatternScannerEvaluationTests.cs`** — Isolierter Fixture-/Overlay-Harness für SearchPattern-Oracle, Budgets, Skip-Zähler, Timeout und Cancellation (zuletzt: step-005).
- **`src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs`** — SDK-nahe End-to-End-Abdeckung für Plain-Suche, Regex-Suche und fehlende Parameter.
- **`src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs`** — MCP-Tool-Vertrag, Toolbestand und Suchverhalten gegen die Integrations-Fixture.
- **`src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandJsonRpcFramingTests.cs`** — Raw-Wire-JSON-RPC, stdout-Framing und maschinenlesbare Suchantworten.
- **`src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandMissHintTests.cs`** — Nicht-C#-Dateitypen und Suchhinweise bei fehlenden Symboltreffern.
- **`src/AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs`** — Dogfood-Abdeckung gegen das reale Repository und die residente Lösung.
- **`src/AiNetLinter.FastTests/Mcp/OverviewResourceRegistrationTests.cs`** — Schnelle Prüfungen für Overview-Inhalt, Tool-Parität und empfohlene MCP-Workflows.
- **`src/AiNetLinter.FastTests/Mcp/McpServerOptionsFactoryTests.cs`** — Schnelle Prüfungen für Registrierung, Instruktionsbudget und Tool-/Dateityp-Hinweise.
- **`src/AiNetLinter.IntegrationTests/Mcp/Platform/ReadOnlyMcpHostFixture.cs`** — Wiederverwendbare schreibgeschützte MCP-Host-Fixture für Integrationstests.
- **`src/AiNetLinter.IntegrationTests/Mcp/Platform/RepositoryMcpHostFixture.cs`** — Repository-basierte Host-Fixture für residenten Analysezustand und Dogfood-Szenarien.
- **`src/AiNetLinter.IntegrationTests/Mcp/Platform/McpRawWireTestHarness.cs`** — Gemeinsamer Prozess- und JSON-RPC-Harness für Wire-Level-Tests.
- **`tests/Fixtures/SymbolGraphMini`** — Mehrsprachige Testressourcen mit C#, JavaScript, CSS, Razor, XAML und HTML für Datei-unabhängige Suchfälle.
- **`tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/search-fixture.md`** und **`wwwroot/search-fixture.json`** — Neutrale Markdown-/JSON-Suchmuster für Wiederholungen, Regex, Kontext und mehrere MatchRanges.

## Dokumentation und Projektvertrag

- **`README.md`** — Öffentliche CLI-/MCP-Übersicht und Beschreibung der aktuellen `search_pattern`-Fallbackrolle.
- **`Docs/agent-api.md`** — Verbindlicher Toolvertrag, Parameter, Antwortformate, Trunkierung und MCP-Ressourcen für Agenten.
- **`Docs/integration.md`** — Integrationsleitfaden zur Reihenfolge von MCP-Symbolsuche, `search_pattern` und direktem `rg`.
- **`Docs/ROADMAP.md`** — Historische und laufende MCP-Epics einschließlich der bisherigen `search_pattern`-Meilensteine.
- **`Docs/configuration.md`** — Konfigurations- und Filterregeln für Dateien, Projekte, generierte Inhalte und Build-Artefakte.
- **`src/AiNetLinter.IntegrationTests/Mcp/McpDocumentationSmokeTests.cs`** — Dokumentationsregressionen für Toolnamen und die öffentlich beschriebene MCP-Oberfläche.
