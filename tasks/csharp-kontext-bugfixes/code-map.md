## Primäre Einstiegspunkte

- `FeatureContextScanner.ScanAsync` aggregiert den projektgebundenen
  `get_feature_context`-Kontext.
- `ProjectToolCall.WithDegradedHeader` ergänzt projektweite Freshness-Hinweise.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/Tools/FeatureContext/` — Modelle, Scanner, Formatter und Tool-Registrierung.
- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` — wiederverwendete Caller-Suche.
- `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs` — gemeinsamer Antwort-Wrapper.

## Aufrufer und Abhängigkeiten

- `get_feature_context`-Tool und `McpToolResults.Text<T>` liefern Text und
  StructuredContent.
- Roslyn `SymbolFinder.FindReferencesAsync` ist die Quelle der statischen
  Referenzen/Call-Sites.

## Relevante Tests, Konfiguration und Dokumentation

- `src/AiNetLinter.FastTests/` — Feature-Context- und Projektantwort-Tests.
- `src/AiNetLinter.IntegrationTests/` — Live-MCP-Vertragstests.
- `Docs/agent-api.md`, gegebenenfalls `Docs/integration.md`.
- Fachlicher Vertrag: `tasks/csharp-kontext-bugfixes/Konzept.md`.

## Invarianten, Risiken und Unsicherheiten

- Erfolgreich leerer Violations-Report bleibt vom Fehlerstatus unterscheidbar,
  aber kompatibel.
- Cancellation darf keine partielle Erfolgs-Payload erzeugen.
- Text und StructuredContent müssen dieselbe begrenzte Auswahl und denselben
  Freshness-/Statuszustand zeigen.

## Verifikation

- Initiale MCP-Kontextabfragen und gezielte Tests werden vom Implementierer
  ergänzt.
