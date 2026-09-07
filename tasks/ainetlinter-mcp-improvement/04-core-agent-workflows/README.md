# 04 – Core Agent Workflows

## Ziel

Die häufigste Agentenschleife – Kontext vor Änderung, Impact/Tests vor Änderung, Lint/Metriken nach Änderung – soll fachlich konsistente Antworten liefern.

## Scope

- gemeinsame Symbol-/Dateiscope-Sprache zwischen Impact, Violations und Metrics;
- Produktions- und Testtreffer nachvollziehbar ranken und begrenzen;
- Partial-Typen, Testheuristik und Lint-Status mit korrekter Completeness darstellen;
- Composite-Tools so gestalten, dass sie nicht dieselbe Information mehrfach laden;
- statische Testzuordnung klar von Runtime-Coverage trennen.

## Ausgangsbefunde

`get_feature_context`, `get_impact`, `get_violations`, `metrics_lookup`, `get_test_context`, `combo-mcp-first-context`, `combo-impact-lint`.

## Voraussichtliche Quellbereiche

`Tools/FeatureContext/*`, `Tools/SymbolGraph/GetImpactTool`, `FindReferencesTool`, `Tools/Analysis/GetViolationsTool`, `Tools/MetricsLookup/*`, `Tools/TestContext/*`, `DiffImpactAnalyzer` und die Test-Coverage-Scanner.

## Abhängigkeiten

Pakete 01–03; besonders der Identifier- und Completeness-Vertrag. Keine Coverage-Ausweitung außerhalb statischer Analyse.

## Abnahme

- derselbe Anker erzeugt in Impact, Lint und Metrics dieselbe fachliche Einheit;
- ein `VIOLATION`-Metrikstatus wird nicht gleichzeitig als vollständige Lint-Entwarnung ausgegeben;
- Testzuordnung behauptet keine Laufzeitabdeckung;
- Composite-Ausgaben enthalten klare Drilldown-Hints statt redundanter Vollberichte.
