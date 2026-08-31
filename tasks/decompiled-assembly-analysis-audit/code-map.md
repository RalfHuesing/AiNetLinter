# Code-Map: 360-Grad-Audit der externen Assembly-Analyse

## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/` — Analyse-Sessions, Decompilation, Fingerprints, Referenzen, Quellenwahl, Ressourcenregister und Cache-Verträge.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` — Provider, Git-Akquisition, Checkout-Sicherheit, Cache/Refresh und Snapshot-Materialisierung.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`, `src/AiNetLinter/Mcp/Tools/SymbolGraph/`, `src/AiNetLinter/Mcp/Registration/` — Toolverhalten, Navigation, Registrierung und Wire-Texte.

## Betroffene Dateien und Symbole

- Routing: `src/AiNetLinter/Mcp/AnalysisToolCall.cs` (`AssemblyAnalysisDispatcher.ExecuteAsync`, `AssemblyAnalysisDispatcher.CreateRoute`) validiert `targetType`/`targetPath`, leased die Assembly-Session und erweitert aktuell vor jedem Assembly-Tool-Handler die Referenzen.
- Zielvalidierung: `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs` und `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs` prüfen absolute vorhandene `.dll`-Pfade; `Path.GetFullPath` ist die sichtbare Kanonisierung.
- Decomp-/Sessionkern: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs`, `AssemblyAnalysisRegistry.cs`, `AssemblyAnalysisResponse.cs` und `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs`.
- Navigation: `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs`, `AssemblyNavigationSupport.cs`, `FindReferencesTool.cs`, `AssemblyAnalysisModels.cs`; Assembly-Inspection-Member DTOs exponieren Signatur-/Parameterdaten, aber keine eigene stabile Member-ID.
- Wire-/Budgetpfad: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs`, `InspectAssemblyTool.cs`, `FindAssemblyExtensionsTool.cs`, `InspectAssemblyFormatter.cs`.
- External Source: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs`, `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` mit Konfigurationsvalidierung, Provider, Git-Prozess, Checkout-Attestation, Cache/Refresh und Snapshot-Registry.

## Aufrufer und Abhängigkeiten

- MCP-Registrierung: `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs` registriert `inspect_assembly` und `find_assembly_extensions`; `SymbolGraphToolRegistrations.cs` verbindet Assembly-Varianten von `find_symbol`, `find_references` und `get_call_tree`.
- Lebenszyklus: `AssemblyAnalysisDispatcher` → `AssemblyAnalysisRegistry.LeaseAsync` → `AssemblyAnalysisSession`/`AssemblyAnalysisLease` → optionales Referenz-Leasing; Source-Auswahl und Snapshot-Erzeugung liegen im Host-Composition-Pfad.
- External-Source-Lebenszyklus: Mapping/Provider → Git-Akquisition → Attestation → `SourceSnapshotRegistry` → Source-backed Roslyn-Kontext; Fehler und Degradierung werden in die Toolantwort projiziert.

## Relevante Tests, Konfiguration und Dokumentation

- Tests: `src/AiNetLinter.FastTests/Mcp/Assemblies/`, `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`, `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/` und allgemeine MCP-Integrationstests.
- Verträge: `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md`, `README.md`, `rules.json`, `.agents/rules/`.

## Invarianten, Risiken und Unsicherheiten

- Keine Zielassembly laden oder ausführen; Zielpfade absolut und validiert.
- Externe Quellen, Git-Prozesse, Checkout-Pfade, Reparse-Points, Snapshots, Credentials und Toolantworten sind sicherheits- und lebenszyklusrelevant.
- Konkrete Live-DLLs, externe URLs, Installationspfade und Zugangsdaten dürfen nicht in Reports erscheinen.
- Bestätigte Architekturbeobachtung: Der Dispatcher ruft `ExpandReferencesAsync` vor dem Assembly-Handler unabhängig vom sichtbaren `includeReferences`-Parameter auf.
- Bestätigte Wire-Beobachtung: Eine einmalige 4-KiB-Sampleauswahl wird im strukturierten `inspect_assembly`-Payload sowohl top-level als auch in verschachtelten Summary-Feldern wiederverwendet; die globale serialisierte Antwortgröße ist damit nicht aus dem Code ersichtlich begrenzt.
- Live-Decomp-Probe: lokales neutrales Build-Artefakt wurde metadata-only untersucht; wegen nicht identischer Referenzversionen und semantischer Decompilerdiagnosen war das Ergebnis `partial` und enthielt keine Typen. Dies ist als Umgebungs-/Abdeckungsgrenze, nicht automatisch als Produktdefekt, zu werten.
- Sicherheits-/Lebenszyklus-Tests sind im Repository umfangreich vorhanden.
  Die nachträgliche MCP-Live-Probe mit einer konfigurierten gemappten DLL
  erzeugte einen Gitea-Checkout und Source-Dateien, aber die MCP-Antworten
  blieben `origin=decompiled`, `sourcePath=none` und `snapshot=none`; damit
  ist die Source-backed-Bereitstellung nicht bestanden.

## Verifikation

- Durchgeführt: `get_file_tree`, `get_index_scope`, `get_feature_context`, `find_symbol`, `find_references`, `inspect_assembly`, `find_assembly_extensions`, `get_violations`, `safeguard`, `find_duplicates`, `find_dead_code`, `find_magic_values` mit explizitem `targetType` und absolutem `targetPath`.
- Abschluss-Qualitätsaudit im External-Source-/Assembly-Scope: drei nur
  `near`/`fuzzy`-Duplikatcluster, ausschließlich niedrig-konfidente
  Dead-Code-Heuristiken und ein lokalisierbarer String-Kandidat; kein
  eindeutiger sicherer Korrekturkandidat, daher keine Produktionsänderung.
- Durchgeführt: `dotnet build` erfolgreich; die beiden vollständigen Nicht-Stress-Testläufe folgen als Abschluss-Gate.
- Abdeckungslimit: unabhängige Reviewer konnten wegen `collab spawn failed: agent thread limit reached` nicht ausgeführt werden; die Reports sind Orchestrator-Fallbacks und entsprechend markiert.
