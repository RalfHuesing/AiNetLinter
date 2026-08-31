## Primäre Einstiegspunkte

- Assembly-Dispatch: `src/AiNetLinter/Mcp/AnalysisToolCall.cs`, Symbol `AssemblyAnalysisDispatcher`.
- Assembly-Navigation: `src/AiNetLinter/Mcp/Tools/SymbolGraph/`, `src/AiNetLinter/Mcp/Tools/CallTree/`.
- External-Source: `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs` und `src/AiNetLinter/Mcp/Assemblies/ExternalSource/`.
- Session-/Cache-Lebensdauer: `src/AiNetLinter/Mcp/Assemblies/Analysis/`.
- Health-/Wire-Projektion: `src/AiNetLinter/Mcp/Tools/ServerMaintenance/` und Assembly-Tool-Response-Modelle.

## Aktueller Zuschnitt nach Zwischencommit

- `AssemblyAnalysisRegistry` ist eine schmale Fassade. Die Source-Project-
  Lease- und Shared-State-Grenze liegt in
  `AssemblyAnalysisSourceProjectLeaseCoordinator`; Idle-Eviction und
  Retirement liegen in `AssemblyAnalysisRegistryEvictionCoordinator`.
- `AssemblyAnalysisRegistryIdentity` kapselt Fingerprint-Erzeugung und die
  Freshness-Probe des aktuellen Source-Snapshot-Identifiers. Der produktive
  `AssemblySourceSelectionOrchestrator` stellt dafür bekannte Snapshot-IDs
  ohne wiederholte Provider-Auflösung bereit; generische Resolver werden
  weiterhin direkt geprüft. Der Health-Pfad nutzt
  `AssemblyAnalysisHealthSnapshotProvider` für eine read-only Projektion.
- `GetServerHealthResponseBuilder` orchestriert nur noch den Markdown-/Payload-
  Zusammenbau; `GetServerHealthProjection` kapselt Status-, Diagnose- und
  stabile Datenprojektionen. Der externe Wire-Vertrag bleibt unverändert.
- `AssemblyAnalysisResponseLimits` delegiert die Messung und Kompaktierung der
  kompletten serialisierten Assembly-StructuredContent-Payload an
  `AssemblyAnalysisResponseBudgetCompactor` und hält global 4.096 UTF-8-Bytes
  ein; `AssemblyAnalysisResponse` synchronisiert den Diagnoseabschnitt im Text
  mit der finalen StructuredContent-Auswahl.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/AnalysisTarget.cs`: `AnalysisToolDispatch` trägt die Route-Callbacks; hier wird die explizite `ExpandAssemblyReferences`-Fähigkeit ergänzt.
- `src/AiNetLinter/Mcp/AnalysisToolCall.cs`: `AssemblyAnalysisDispatcher.ExecuteAsync` akquiriert den Root-Lease; Referenzexpansion ist über `AnalysisToolDispatch.ExpandAssemblyReferences` eine explizite Handler-Fähigkeit.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs`: bounded Referenzsuche; Batch-Navigation, Diagnostics und Trunkierung werden patternübergreifend aggregiert.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs`: Root-/Child-Symbolauflösung; erwartete `SYMBOL_NOT_FOUND`-Nichttreffer bleiben intern und verschlechtern keine globale Completeness.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs` und `SymbolIdentifierResolver`: gemeinsame Datei-/Zeile-/Spalte-Auflösung; Positionen werden gegen `SourceText` validiert, bevor Roslyn `FindToken` erhält.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs`: Lease-Menge, Diagnostics und Navigation-Summary.
- `src/AiNetLinter/Mcp/Tools/CallTree/AssemblyGetCallTreeTool.cs`: bounded Assembly-Call-Tree mit `includeReferences`.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs` und `AssemblyAnalysisResponseBudgetCompactor.cs`: globale UTF-8-Budgetprüfung und stufenweise Kompaktierung der serialisierten Assembly-StructuredContent-Payload.
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
- MCP-Kontext nach Abschluss: `AssemblyAnalysisRegistry` 371 LOC,
  `AssemblySourceSelectionOrchestrator` 328 LOC/2399 AI-Footprint,
  `AssemblyAnalysisResponseLimits` 261 LOC/661 AI-Footprint,
  `AssemblyAnalysisResponseBudgetCompactor` 237 LOC/294 AI-Footprint und
  `GetServerHealthResponseBuilder` 40 LOC/72 AI-Footprint; die betroffenen
  Scopes melden keine Violations.
- E1-Entscheidung: `AnalysisToolDispatch.ExpandAssemblyReferences` ist standardmäßig `false`; nur Handler mit explizitem Referenzvertrag aktivieren ihn. `inspect_assembly`/`find_assembly_extensions` bleiben wegen ihrer bestehenden Referenzprojektion opt-in; `includeReferences` steuert Symbol-/Referenz-/Call-Tree-Tools.

## Verifikation

- MCP-first-Abfragen und Abschluss-Audit ausgeführt: `get_violations` für
  Assembly-Analysis, Assembly-Response, Server-Maintenance und Dispatcher
  liefern 0 Violations; `find_duplicates` exact liefert 0 Cluster,
  `find_duplicates` refactoring-drift liefert 0 Kandidaten, `find_dead_code`
  im Assembly-Scope liefert 0 High-Confidence-Kandidaten und
  `find_magic_values` im geänderten Assembly-Scope liefert keine Dateien.
  Der near-clone-Lauf zeigt nur dokumentierte bestehende/absichtlich parallele
  Paare. Vollständige Build-/Fast-/Integration-Gates sind grün.
