## Primäre Einstiegspunkte

- Assembly-Dispatch: `src/AiNetLinter/Mcp/AnalysisToolCall.cs`, Symbol `AssemblyAnalysisDispatcher`.
- Assembly-Navigation: `src/AiNetLinter/Mcp/Tools/SymbolGraph/`, `src/AiNetLinter/Mcp/Tools/CallTree/`.
- External-Source: `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs` und `src/AiNetLinter/Mcp/Assemblies/ExternalSource/`.
- Session-/Cache-Lebensdauer: `src/AiNetLinter/Mcp/Assemblies/Analysis/`.
- Health-/Wire-Projektion: `src/AiNetLinter/Mcp/Tools/ServerMaintenance/` und Assembly-Tool-Response-Modelle.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/AnalysisTarget.cs`: `AnalysisToolDispatch` trägt die Route-Callbacks; hier wird die explizite `ExpandAssemblyReferences`-Fähigkeit ergänzt.
- `src/AiNetLinter/Mcp/AnalysisToolCall.cs:113-195`: `AssemblyAnalysisDispatcher.ExecuteAsync` akquiriert den Root-Lease, expandiert aktuell pauschal und reichert die Antwort an.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs:21-100`: bounded Referenzsuche; `BuildResponseAsync` überschreibt aktuell Navigation je Pattern.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs:17-137`: Root-/Child-Symbolauflösung; erwartete `SYMBOL_NOT_FOUND`-Ergebnisse werden aktuell als globale Diagnostics übernommen.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs:131-210` und `SymbolIdentifierResolver`: gemeinsame Datei-/Zeile-/Spalte-Auflösung; Spaltenbereich wird aktuell vor `FindToken` nicht validiert.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs`: Lease-Menge, Diagnostics und Navigation-Summary.
- `src/AiNetLinter/Mcp/Tools/CallTree/AssemblyGetCallTreeTool.cs`: bounded Assembly-Call-Tree mit `includeReferences`.
- E1-Tests: `AssemblyAnalysisDispatcherCapabilityTests` deckt Root-only, erwarteten Child-Nichttreffer und Referenzdiagnosen ab; `AssemblyAnalysisRouteTests` deckt includeReferences und Batch-Trunkierung ab; `FindReferencesToolTests` deckt ungültige Datei-/Zeile-/Spalte-Positionen ab.

## Aufrufer und Abhängigkeiten

- Tool-Registrierungen unter `src/AiNetLinter/Mcp/Registration/` rufen Assembly-Navigation und Dispatch auf.
- `SymbolGraphToolRegistrations` setzt pro Assembly-Handler die Referenzfähigkeit; `inspect_assembly` und `find_assembly_extensions` benötigen ihre Referenzsessions für ihre bestehende Response-/Diagnoseprojektion, Symbol-/Referenz-/Call-Tree-Suche nur bei `includeReferences=true`.
- Daemon-Komposition unter `src/AiNetLinter/Mcp/Daemon/` und `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisHostComposition.cs` liefert Registry-/Session-Abhängigkeiten.
- Externe Quelle wird von Analysis-Context-/Registry-Factories verwendet.

## Relevante Tests, Konfiguration und Dokumentation

- FastTests: `src/AiNetLinter.FastTests/Mcp/Assemblies/` und `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`.
- Für E1 konkret: `AssemblyAnalysisDispatcherCapabilityTests`, `AssemblyAnalysisRouteTests`, `FindReferencesToolTests` und `FindSymbolToolTests`.
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
- MCP-Kontext: Dispatcher 74 Codezeilen/240 AI-Footprint, `FindReferencesTool` 129/1148, `AssemblyFindSymbolTool` 76/2391, `AssemblyGetCallTreeTool` 112/2452; die betroffenen Dateien meldeten im initialen Feature-Kontext keine Violations.
- E1-Entscheidung: `AnalysisToolDispatch.ExpandAssemblyReferences` ist standardmäßig `false`; nur Handler mit explizitem Referenzvertrag aktivieren ihn. `inspect_assembly`/`find_assembly_extensions` bleiben wegen ihrer bestehenden Referenzprojektion opt-in; `includeReferences` steuert Symbol-/Referenz-/Call-Tree-Tools.

## Verifikation

- Initiale MCP-first-Abfragen ausgeführt: `get_file_tree`/`get_index_scope`, `get_feature_context` und `get_symbol_body` für Dispatcher und Assembly-Navigationssymbole. E1-Codeänderung umfasst explizite Dispatch-Fähigkeit, Positionsgrenzen, expected-miss-Filter und Batch-Summary-Merge; gezielte Tests und `get_violations` folgen nach der letzten Codeänderung.
