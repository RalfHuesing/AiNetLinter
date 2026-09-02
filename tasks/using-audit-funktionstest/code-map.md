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
- Decompiled Bodies: `AssemblyDecompiledBodyResolver.DecompileBodyAsync`, `FindMember`,
  `MatchesParameters` und `MatchesParameterType`; gemeinsame Bereinigung in
  `AssemblyDecompilationSourceText.RemoveCompilerGeneratedNestedTypes`.

## Aufrufer und Abhängigkeiten

- `AnalysisToolCall` routet Projekt- und Assembly-Targets; Assembly-Symbolgraph-Aufrufer
  übergeben die aktuelle `AnalysisSymbolIdentity`.
- `TestDetector.IsTestProject` und `TestDetector.IsTestFile` sind die bestehende
  projektweite Heuristik für `scopeType`.
- `FileSystemExclusionHelpers` und `WebFileCatalog.GetProjectDirectories` liefern den
  geschützten Dateisystem-Walk für Index-Scope.
- `AssemblyDecompiledBodyResolver.ResolveAsync` dekompiliert den deklarierenden Typ und ordnet
  anschließend den Roslyn-Member dem dekompilierten C#-Syntaxbaum zu; `AssemblyReferenceResolver`
  liefert dafür Referenzen und Partial-Diagnosen.
- Der On-Demand-Bodypfad verwendet `decompileMemberBodies: true`. Vor dem Roslyn-Parsing werden
  jetzt dieselben Compiler-Generated-Bereinigungen wie im vollständigen Assembly-Snapshot
  angewendet; damit werden insbesondere ungültige VB-Closure-Namen wie `_Closure$__...` entfernt.

## Relevante Tests, Konfiguration und Dokumentation

- Fast-Tests: Symbolgraph-, Hotspot-, Pattern- und Wiring-Verträge.
- Integration: `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetIndexScopeToolTests.cs`.
- API-Dokumentation: `Docs/agent-api.md`, `Docs/integration.md`, `Docs/ROADMAP.md`.
- Decompiled-Body-Tests: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyDecompiledBodyResolverTests.cs`
  sowie die Partial-Referenz-Szenarien in `AssemblyAnalysisSessionTests.cs`.
- Neue Regression: `RemoveCompilerGeneratedNestedTypes_RemovesVisualBasicClosureTypes` deckt die
  konkreten `$VB$Local...`-Felder aus dem Sage-Decompilat ab; ein separater Smoke-Test gegen die
  installierte Sage-DLL bestätigte `Beleg.Save` als verfügbaren Body.

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
- Für dekompilierte VB.NET-Signaturen muss ein Roslyn-Member mit weniger Parametern auf eine
  C#-Syntaxsignatur mit zusätzlichen Default-Parametern abgebildet werden können. Ein einfacher
  Typnamen-Fallback darf nur bei nicht auflösbaren Error-Typen greifen, damit gleichnamige Typen
  aus verschiedenen Namespaces nicht versehentlich vermischt werden.
- Compiler-generierte verschachtelte Typen dürfen nicht in den Syntaxbaum des Body-Resolvers
  gelangen, wenn der Decompiler deren VB-Namen als nicht gültiges C# ausgibt. Die Bereinigung muss
  auf den On-Demand- und Snapshot-Pfad gleichermaßen angewendet werden und echte Methoden-Bodies
  unverändert lassen.

## Verifikation

- Gezielte Regressionstests: Fast-Tests für Symbolgraph/Hotspots/Wiring grün (117 Tests),
  relevante Integrationstests grün (23 Tests). `dotnet build` ist mit 0 Warnungen und 0
  Fehlern durchgelaufen. Vollständige Nicht-Stress-Gates: FastTests 2.425 bestanden,
  2 übersprungen; IntegrationTests 385 bestanden, 0 übersprungen, 0 Fehler.
- Audit-MCP-Abfragen für Duplikate, Dead Code und Magic Values wurden ausgeführt; der
  gezielte `get_violations`-Nachcheck für `src/AiNetLinter/Mcp` meldet 0 Verstöße.
- Für die Decompiled-Body-Erweiterung sind fokussierte Resolver-/Assembly-Route-Tests grün
  (10 Tests); der Audit-Scope `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies` meldet keine
  exakten Duplikate, keinen Dead Code und keine Magic Values. Der finale Violations-Check für
  denselben Scope meldet 0 Verstöße. Die vollständigen Solution-Gates sind nach der letzten
  Codeänderung grün: `dotnet build` mit 0 Warnungen/0 Fehlern, FastTests 2.428 bestanden und
  2 übersprungen sowie IntegrationTests 384 bestanden und 0 übersprungen. Der Release-Build
  wurde anschließend nach `C:\Daten\Tools\AiNetLinter-win-x64` deployt. Ein frischer MCP-
  Stdio-Prozess der Installation liefert für Sage `Beleg.Save(bool)` `bodyAvailability:
  available` und der abschließende Violations-Check meldet 0 Verstöße.
