## Primäre Einstiegspunkte

- `FeatureContextScanner.ScanAsync` aggregiert den projektgebundenen
  `get_feature_context`-Kontext.
- `ProjectToolCall.WithDegradedHeader` ergänzt projektweite Freshness-Hinweise.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/Tools/FeatureContext/` — Modelle, Scanner, Formatter und Tool;
  insbesondere `FeatureContextScanner.CollectTestsAsync` fuer die drei Test-Caps.
- `src/AiNetLinter/Mcp/Registration/AnalysisToolRegistrations.cs` — Registrierung und Beschreibung
  von `get_feature_context`.
- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` — wiederverwendete Caller-Suche.
- `src/AiNetLinter/Core/TestCoverageScanner.cs` und `TestCoverageBatchScan.cs` — statische Testzuordnung und Cancellation-Grenzen.
- `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs` — gemeinsamer Antwort-Wrapper.

## Aufrufer und Abhängigkeiten

- `get_feature_context`-Tool und `McpToolResults.Text<T>` liefern Text und
  StructuredContent.
- Roslyn `SymbolFinder.FindReferencesAsync` ist die Quelle der statischen
  Referenzen/Call-Sites; die Ergebnisreihenfolge wird im Feature-Context stabilisiert.
- `TestCoverageScanner` liefert pro Datei alle passenden Testmethoden; der Feature-Context
  übernimmt daraus zusätzlich einen globalen und einen Per-Datei-Methoden-Cap.
- `ProjectToolCall.WithDegradedHeader` ergänzt bestehende StructuredContent-Objekte additiv
  um Freshness-Metadaten.

## Relevante Tests, Konfiguration und Dokumentation

- `src/AiNetLinter.FastTests/Mcp/Tools/FeatureContext/GetFeatureContextToolTests.cs` — Feature-Context-
  Status-, Cancellation- und Sortierverträge; 18 bestehende Tests bleiben unverändert.
- `src/AiNetLinter.FastTests/Mcp/Tools/FeatureContext/GetFeatureContextToolCapTests.cs` — kompakte
  Theory mit sieben direkten Cap-Fällen: isolierte Caps sowie alle relevanten Kombinationen;
  Text-/StructuredContent-Ausgabe, Counts, Verteilung und exakte Gründe werden geprüft.
- `src/AiNetLinter.FastTests/Mcp/Wiring/WiringProjectContractTests.cs` — Freshness-Text-/Structured-
  Content-Parität des projektgebundenen Wrappers.
- `src/AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs` — Live-MCP-Vertrag für
  Text/StructuredContent-Parität; der Degraded-/Freshness-Zustand ist deterministisch im
  FastTest abgedeckt.
- `Docs/agent-api.md`, gegebenenfalls `Docs/integration.md`.
- Fachlicher Vertrag: `tasks/csharp-kontext-bugfixes/Konzept.md`.

## Invarianten, Risiken und Unsicherheiten

- Erfolgreich leerer Violations-Report bleibt vom Fehlerstatus unterscheidbar,
  aber kompatibel (`status=complete`, `reasonCode=null`).
- Violations-Fehler liefern Counts/Items getrennt vom Status; Cancellation propagiert als
  Abbruch und erzeugt keine Payload.
- Caller und Violations werden vor der Kappung deterministisch sortiert. Callers bleiben
  statische Referenzen/Call-Sites, nicht Runtime-Coverage.
- `maxTests` bleibt Dateilimit; zusätzlich begrenzen Methoden-Caps die ausgegebenen
  Methodennamen. `maxTestMethodsPerFile` wird nur bei einer konkreten Kürzung
  gegenüber dem verbleibenden Gesamtbudget gemeldet; `maxTestMethodsTotal` nur,
  wenn die nach dem Datei-Cap verfügbare Auswahl das Gesamtbudget überschreitet.
  Truncation-Grund und Counts bleiben sichtbar.
- Cancellation darf keine partielle Erfolgs-Payload erzeugen.
- Text und StructuredContent müssen dieselbe begrenzte Auswahl und denselben
  Freshness-/Statuszustand zeigen.

## Verifikation

- MCP-Kontextaufnahme am 2026-09-07: `get_feature_context` für
  `FeatureContextScanner.ScanAsync`, `DiffImpactAnalyzer.FindCallSiteEntriesAsync` und
  `ProjectToolCall.WithDegradedHeader`; alle drei vollständig im Projektindex sichtbar.
- Korrekturrunde 1: `GetFeatureContextToolTests.cs` enthält 18 Tests; der frische
  Implementierer meldete 27/27 gezielte Tests, der aktuelle MCP-Check der
  Toolbeschreibung und `CollectTestsAsync` ist vollständig aufgelöst.
- Abschlussprüfung des Reviewers: `dotnet build` grün (0 Warnungen/0 Fehler),
  FastTests Nicht-Stress 2278/2278 grün. Integration Nicht-Stress 411/412;
  der einzige Fehler ist die `MaxLineCount`-Violation in
  `GetFeatureContextToolTests.cs` (527 statt maximal 500 Zeilen).
- Korrekturrunde 2: Die ursprüngliche Testklasse ist auf 400 Zeilen reduziert; die neue Cap-Datei
  umfasst 101 Zeilen. Der benannte Fehler ist damit scope-seitig behoben; die kombinatorischen
  Regressionen laufen als 7 Theory-Fälle in insgesamt 22 gezielten Tests.
- Nach der letzten Codeänderung: gezielte FastTests 22/22 grün; benannter Live-Dogfood-Test
  `LiveDogfood_GetFeatureContext_ReturnsTextStructuredParity` 1/1 grün.
- Danach `find_duplicates` (2 bestehende fuzzy Cluster, keine Änderung), `find_dead_code` (0)
  und `find_magic_values` (0); abschließend `get_violations` für Produktions- und Testscope
  jeweils 0. Vollständige Build-/Nicht-Stress-Gates verbleiben beim Orchestrator.
- Frischer Implementierer-`get_violations`-Check war auf `src/AiNetLinter/Mcp`
  begrenzt und meldete 0. Der reviewer-seitige Test-Scope-Check mit
  `targetType=project`, absolutem Repository-Root und
  `scopeFilter=src/AiNetLinter.FastTests/Mcp/Tools/FeatureContext` meldet
  1 `MaxLineCount`-Violation; Produktionscode bleibt im gemeldeten Scope
  violationsfrei.
