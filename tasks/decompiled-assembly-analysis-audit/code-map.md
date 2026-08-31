# Code-Map: 360-Grad-Audit der externen Assembly-Analyse

## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/` — Analyse-Sessions, Decompilation, Fingerprints, Referenzen, Quellenwahl, Ressourcenregister und Cache-Verträge.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` — Provider, Git-Akquisition, Checkout-Sicherheit, Cache/Refresh und Snapshot-Materialisierung.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`, `src/AiNetLinter/Mcp/Tools/SymbolGraph/`, `src/AiNetLinter/Mcp/Registration/` — Toolverhalten, Navigation, Registrierung und Wire-Texte.

## Betroffene Dateien und Symbole

- Noch durch die Reviewer gegen Working Tree und AiNetLinter-MCP zu verifizieren; die Konzeptpfade sind die bekannten Startpunkte.

## Aufrufer und Abhängigkeiten

- Noch durch die Reviewer zu verifizieren: MCP-Registrierung → Assembly-Tools → Analyse-/Quellen-/Snapshot-Lebenszyklus sowie Konfiguration und externe Provider.

## Relevante Tests, Konfiguration und Dokumentation

- Tests: `src/AiNetLinter.FastTests/Mcp/Assemblies/`, `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`, `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/` und allgemeine MCP-Integrationstests.
- Verträge: `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md`, `README.md`, `rules.json`, `.agents/rules/`.

## Invarianten, Risiken und Unsicherheiten

- Keine Zielassembly laden oder ausführen; Zielpfade absolut und validiert.
- Externe Quellen, Git-Prozesse, Checkout-Pfade, Reparse-Points, Snapshots, Credentials und Toolantworten sind sicherheits- und lebenszyklusrelevant.
- Konkrete Live-DLLs, externe URLs, Installationspfade und Zugangsdaten dürfen nicht in Reports erscheinen.
- Detailbeziehungen und Live-Abdeckungsgrenzen sind vorläufig und werden linsenbezogen verifiziert.

## Verifikation

- Geplant: MCP-first-Semantikabfragen mit `targetType`/absolutem `targetPath`, passende read-only Tests und Konzept-Abschluss-Gates.
- Status: noch nicht ausgeführt.
