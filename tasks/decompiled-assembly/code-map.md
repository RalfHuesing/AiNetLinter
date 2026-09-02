# Code Map

## Primäre Einstiegspunkte

- Assembly-Analyse und MCP-Tool-Pipeline unter `src/AiNetLinter/Mcp/Assemblies/` und `src/AiNetLinter/Mcp/Tools/`.
- Konkrete Einstiegspunkte werden vom Implementierer per MCP-first-Kontextphase verifiziert.

## Betroffene Dateien und Symbole

- `AssemblyDecompiledBodyResolver`
- `AssemblyDecompilationCache`
- `AssemblyReferenceResolver`
- `SymbolIdentifierResolver`
- `GetServerHealthTool` und `ServerMaintenanceToolRegistrations`
- Weitere betroffene Symbole gemäß Paket-Scope des Konzepts; noch nicht vollständig verifiziert.

## Aufrufer und Abhängigkeiten

- Decompilation-/Roslyn-Workspace-Pipeline, Assembly-Session-/Registry-Lifecycle und MCP-Tool-Registrierungen; konkrete Kanten werden gegen Working Tree und MCP geprüft.

## Relevante Tests, Konfiguration und Dokumentation

- `src/AiNetLinter.FastTests/`
- `src/AiNetLinter.IntegrationTests/`
- `rules.json`
- `Docs/`, `README.md`, `instructions.md`
- Konzeptspezifische Quellen: `tasks/decompiled-assembly/Konzept.md`

## Invarianten, Risiken und Unsicherheiten

- Fremde Assemblies bleiben metadata-only; kein dynamisches Laden oder Ausführen.
- Framework-Unification bleibt auf die im Konzept genannten Systempräfixe begrenzt.
- Der genaue aktuelle Aufrufer-/Testumfang ist vor Änderungen semantisch zu verifizieren.

## Verifikation

- Noch nicht ausgeführt; Implementierer ergänzt die Map und liefert den ersten gezielten MCP-/Testnachweis.
