## Primäre Einstiegspunkte

- MCP-Ergebnisse in `src/AiNetLinter/Mcp/Tools/`.
- Automatische Compile-Hinweise wurden aus den erfolgreichen Tool-Antworten
  entfernt.

## Betroffene Dateien und Symbole

- Die früheren Diagnostics- und Prepend-Helper wurden entfernt.
- Die betroffenen Symbolgraph-, Struktur-, Metrik-, Call-Tree- und Analyse-Tools
  geben ihre normalen Ergebnis- bzw. Sufficiency-Texte direkt aus.
- `GetFileSkeletonTool` erzeugt keine dateispezifische Compile-Warnung mehr.
- Betroffene MCP-Tests, `Docs/agent-api.md` und `Docs/ROADMAP.md`.

## Aufrufer und Abhängigkeiten

- Die zugrunde liegenden `WORKSPACE_DIAGNOSTIC`-Fehlerpfade in `McpToolResults`
  sind davon unabhängig und bleiben erhalten.

## Relevante Tests, Konfiguration und Dokumentation

- Compile-Error-Fixture-Tests in `src/AiNetLinter.FastTests` und
  `src/AiNetLinter.IntegrationTests` erwarten derzeit die automatischen
  Hinweise und müssen auf saubere erfolgreiche Antworten angepasst werden.
- `Docs/agent-api.md` und `Docs/ROADMAP.md` beschreiben nun den bereinigten
  Vertrag ohne automatische Compile-Fehler-Hinweise.

## Invarianten, Risiken und Unsicherheiten

- Keine Änderung an echten strukturierten Fehlerantworten, Ladefehlern oder
  Linter-/Roslyn-Malfunction-Handling.
- Nach Entfernung darf eine normale erfolgreiche MCP-Antwort keinen
  automatisch vorangestellten `Hinweis:`-Compile-Header mehr enthalten.
- Compile-Error-Fixtures bleiben erhalten, um erfolgreiche Tool-Antworten ohne
  automatische Warnung zu regressionsprüfen; unreferenzierte Warninweis-
  Infrastruktur und die nur für Singular-/Plural-Header verwendete Fixture
  wurden entfernt.

## Verifikation

- Nach der Änderung: `rg` auf verbliebene automatische Compile-Hinweis-Aufrufer.
- Build: `dotnet build`.
- Tests: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
- Abschließend gezielte AiNetLinter-MCP-Prüfungen und Working-Tree-/Diff-Check.
