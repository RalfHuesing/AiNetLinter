# Code-Map: decompiled-assembly-analysis-finish2

## Primäre Einstiegspunkte

- Assembly-MCP-Dispatcher und Tools für `targetType=assembly`.
- Assembly-Session-/Provider-Komposition, Snapshot-/Registry-Lifecycle und
  External-Source-Konfiguration.

## Betroffene Dateien und Symbole

- Noch durch den Implementierer in der MCP-first-Kontextphase gegen Working
  Tree und Symbolgraph zu verifizieren.
- Bekannte Suchstartpunkte: `src/AiNetLinter/Mcp/Assemblies/`,
  `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` und die zugehörigen
  Konfigurations-/CLI-Dateien.

## Aufrufer und Abhängigkeiten

- MCP-Dispatcher/Host → Assembly-Session/Registry → dekompilierte Roslyn-
  Dokumente, Referenzen und Antwort-Formatter.
- Konfiguration/CLI → External-Source-/Assembly-Limits.

## Relevante Tests, Konfiguration und Dokumentation

- `src/AiNetLinter.FastTests/Mcp/Assemblies/`
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/`
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/`
- `README.md`, `Docs/agent-api.md`, `Docs/integration.md`,
  `Docs/configuration.md`, `Docs/ROADMAP.md` nur bei tatsächlicher
  Vertrags-/Meilensteinänderung.

## Invarianten, Risiken und Unsicherheiten

- Projekt- und Assembly-Sessions bleiben getrennt; externe DLLs werden nicht
  ausgeführt oder verändert.
- Referenzexpansion bleibt ausdrücklich anforderbar und streng begrenzt.
- Die genaue Symbol-/Dateiablage und vorhandene Konfigurationskomposition wird
  durch MCP und aktuelle Quellen verifiziert; diese initiale Karte ist keine
  Source of Truth.

## Verifikation

- Implementierer ergänzt nach MCP-first-Kontext und Änderungen konkrete
  Symbole, Aufrufer, Fixtures und Nachweise.
- Abschluss-Gates und Konzept-Checkliste werden erst nach dem letzten
  Codezustand durch den Orchestrator ausgeführt.

