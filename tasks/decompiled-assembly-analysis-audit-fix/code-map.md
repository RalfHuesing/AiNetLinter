## Primäre Einstiegspunkte

- Assembly-Dispatch: `src/AiNetLinter/Mcp/AnalysisToolCall.cs`, Symbol `AssemblyAnalysisDispatcher`.
- Assembly-Navigation: `src/AiNetLinter/Mcp/Tools/SymbolGraph/`, `src/AiNetLinter/Mcp/Tools/CallTree/`.
- External-Source: `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs` und `src/AiNetLinter/Mcp/Assemblies/ExternalSource/`.
- Session-/Cache-Lebensdauer: `src/AiNetLinter/Mcp/Assemblies/Analysis/`.
- Health-/Wire-Projektion: `src/AiNetLinter/Mcp/Tools/ServerMaintenance/` und Assembly-Tool-Response-Modelle.

## Betroffene Dateien und Symbole

- Noch durch MCP-first-Kontextaufnahme zu verifizieren: Dispatcher-Fähigkeiten, Positionsauflösung, Batch-Summaries, URL-Normalisierung, Checkout-Acquirer, Materialisierung, Registry/Fingerprints, Wire-Budget und Health-Projektion.

## Aufrufer und Abhängigkeiten

- Tool-Registrierungen unter `src/AiNetLinter/Mcp/Registration/` rufen Assembly-Navigation und Dispatch auf.
- Daemon-Komposition unter `src/AiNetLinter/Mcp/Daemon/` und `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisHostComposition.cs` liefert Registry-/Session-Abhängigkeiten.
- Externe Quelle wird von Analysis-Context-/Registry-Factories verwendet.

## Relevante Tests, Konfiguration und Dokumentation

- FastTests: `src/AiNetLinter.FastTests/Mcp/Assemblies/` und `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`.
- IntegrationTests: `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/` sowie allgemeine MCP-Integrationstests.
- Dokumentation: `Docs/agent-api.md`, `Docs/configuration.md`, bei sichtbaren Vertragsänderungen `Docs/ROADMAP.md`/`README.md`.
- Projekt-/Regelbasis: `ainetlinter.project.json`, `rules.json`.

## Invarianten, Risiken und Unsicherheiten

- Assemblies bleiben metadata-only; keine Runtime-Ausführung oder versteckter Restore.
- `includeReferences=false` ist Root-only; harte Referenz-/Session-/Payload-Limits bleiben erhalten.
- Externe Remotes sind ausschließlich öffentlich und ohne Credentials in URL, Logs oder Diagnosen zulässig.
- Checkout-Leases besitzen ab Acquirer-Rückgabe genau einen Owner; Cleanup darf aktuelle/geleaste/geschützte Generationen nicht löschen.
- Source-backed, Decompiled-Fallback und Materialisierungsfehler müssen unterscheidbar bleiben.
- MCP-Schemas und Toolfähigkeit sind gegen den laufenden Server zu verifizieren; konkrete Symbole/Pfade werden nach MCP-Antworten ergänzt.

## Verifikation

- Initial noch nicht ausgeführt; E1 ergänzt gezielte MCP-Abfragen, Tests und `get_violations`.
