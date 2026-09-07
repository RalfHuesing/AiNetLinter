## Primäre Einstiegspunkte

- `FeatureContextScanner.ScanAsync` aggregiert den projektgebundenen
  `get_feature_context`-Kontext.
- `ProjectToolCall.WithDegradedHeader` ergänzt projektweite Freshness-Hinweise.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/Tools/FeatureContext/` — Modelle, Scanner, Formatter und Tool.
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
  Status-, Cancellation-, Sortier- und Methoden-Cap-Verträge.
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
  Methodennamen. Truncation-Grund und Counts bleiben sichtbar.
- Cancellation darf keine partielle Erfolgs-Payload erzeugen.
- Text und StructuredContent müssen dieselbe begrenzte Auswahl und denselben
  Freshness-/Statuszustand zeigen.

## Verifikation

- MCP-Kontextaufnahme am 2026-09-07: `get_feature_context` für
  `FeatureContextScanner.ScanAsync`, `DiffImpactAnalyzer.FindCallSiteEntriesAsync` und
  `ProjectToolCall.WithDegradedHeader`; alle drei vollständig im Projektindex sichtbar.
- Implementierung abgeschlossen. Nach letzter Codeänderung grün: 17 Feature-Context-Tests,
  16 DiffImpactAnalyzer-Tests, 1 Degraded-Wiring-Test und 1 Live-MCP-Vertragstest.
- Nach letzter Codeänderung ausgeführter Audit: 13 bestehende, scope-ferne/unklare
  Duplikat-Cluster; 3 bestehende Low-Confidence-Dead-Code-Kandidaten; 0 Magic Values.
  Der einzige eigene Low-Confidence-Cancellation-Befund wurde durch tatsächliche Nutzung
  des Reason-Codes in `Exception.Data` erledigt.
- Abschließender letzter codebezogener MCP-Check: `get_violations` mit
  `targetType=project`, absolutem Repository-Root, `scopeFilter=src/AiNetLinter`,
  `minSeverity=info`, `includeSnippet=true`, `contextLines=2`, `maxResults=200` —
  0 Violations in 831 Dateien.
