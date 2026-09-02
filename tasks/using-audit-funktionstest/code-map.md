## Primäre Einstiegspunkte

- MCP-Registrierungen in `src/AiNetLinter/Mcp/Registration/` für die betroffenen Tools.
- Semantische Einstiegspunkte: `FindReferencesTool.ResolveSymbolAsync`,
  `SymbolIdentifierResolver.TryResolveByStableIdAsync`,
  `GetSymbolBodyTool.ExecuteAsync`, `GetHotspotsScanner.BuildHotspots`,
  `PatternDetectScanner.BuildReportAsync` und `GetIndexScopeScanner.BuildBreakdown`.

## Betroffene Dateien und Symbole

- Assembly-Symbolauflösung: `FindReferencesTool`, `FindSymbolTool.FormatSymbolLocations`,
  `SymbolIdentifierResolver`, `AnalysisSymbolIdentity`.
- API-Aliase: `SymbolBodyToolRegistrations`, `GetSymbolBodyTool`,
  `SymbolGraphToolRegistrations`, `FindSymbolTool`.
- Hotspots: `GetHotspotsTool`, `GetHotspotsScanner`, `HotspotScanOptions`/`HotspotsPayload`.
- Audit-Parameterobjekte: `FindSymbolRequest`, `GetHotspotsRequest`.
- Pattern-Ausgabe: `PatternDetectScanner`.
- Index-Scope: `GetIndexScopeScanner`, `FileTypeBreakdownEntry`.

## Aufrufer und Abhängigkeiten

- `AnalysisToolCall` routet Projekt- und Assembly-Targets; Assembly-Symbolgraph-Aufrufer
  übergeben die aktuelle `AnalysisSymbolIdentity`.
- `TestDetector.IsTestProject` und `TestDetector.IsTestFile` sind die bestehende
  projektweite Heuristik für `scopeType`.
- `FileSystemExclusionHelpers` und `WebFileCatalog.GetProjectDirectories` liefern den
  geschützten Dateisystem-Walk für Index-Scope.

## Relevante Tests, Konfiguration und Dokumentation

- Fast-Tests: Symbolgraph-, Hotspot-, Pattern- und Wiring-Verträge.
- Integration: `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetIndexScopeToolTests.cs`.
- API-Dokumentation: `Docs/agent-api.md`, `Docs/integration.md`, `Docs/ROADMAP.md`.

## Invarianten, Risiken und Unsicherheiten

- Bare DocumentationCommentIds bleiben im Projekt-Target unverändert; im Assembly-Target
  werden sie nur gegen die erwartete aktuelle Session-Identität akzeptiert.
- Neue optionale Aliasparameter dürfen bestehende Array-Aufrufe und StructuredContent nicht
  verändern; widersprüchliche Alias-/Array-Eingaben müssen deterministisch behandelt werden.
- `get_index_scope` darf generierte Pfade nicht zählen und muss Nicht-C#-Extensions dynamisch
  sowie deterministisch sortiert ausgeben.
- Der gezielte MCP-Violations-Check meldete zunächst `MaxMethodParameterCount` in den durch
  F-02/F-03 erweiterten Tool-Einstiegen; die Aufrufer verwenden nun `FindSymbolRequest` bzw.
  `GetHotspotsRequest`. Der Nachcheck ist für `src/AiNetLinter/Mcp` wieder violationsfrei.
- Audit-Altbefunde im betroffenen MCP-Scope sind ausschließlich Low-Confidence-Interop-/DTO-
  Kandidaten bzw. ein nicht taskbezogenes Daemon-Mitglied; wegen Reflection/Serializer/Interop-
  Risiken wurden sie nicht entfernt. Magic-Value-Treffer betreffen ausschließlich die bestehende
  Buffer-Heuristik außerhalb des geänderten Bereichs.

## Verifikation

- Gezielte Regressionstests: Fast-Tests für Symbolgraph/Hotspots/Wiring grün (117 Tests),
  relevante Integrationstests grün (23 Tests). `dotnet build` ist mit 0 Warnungen und 0
  Fehlern durchgelaufen. Vollständige Nicht-Stress-Gates: FastTests 2.425 bestanden,
  2 übersprungen; IntegrationTests 385 bestanden, 0 übersprungen, 0 Fehler.
- Audit-MCP-Abfragen für Duplikate, Dead Code und Magic Values wurden ausgeführt; der
  gezielte `get_violations`-Nachcheck für `src/AiNetLinter/Mcp` meldet 0 Verstöße.
