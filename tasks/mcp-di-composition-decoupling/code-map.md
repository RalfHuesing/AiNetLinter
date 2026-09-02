# Code Map: MCP-Komposition entkoppeln und Qualitätsgrenzen wiederherstellen

## Primäre Einstiegspunkte

- `AssemblySymbolResolver.ResolveAsync` verarbeitet Symbolauflösung über den
  Root-Lease und dessen Reference-Leases.
- `AssemblyHealthProjection.Project` projiziert Assembly-Health-Daten jetzt nur
  mit den benötigten primitiven Diagnoseoptionen; dadurch bleibt die Health-
  Projection von `GetServerHealthOptions` und `DaemonRuntimeContext` getrennt.
- Die konkrete Lease-Zustandsgrenze bleibt `ISolutionStateProvider`; Epic 1
  ist unverändert abgeschlossen.
- Session- und Cache-Verhalten sind in getrennten Component-Testklassen
  organisiert.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs`
  - `AssemblySymbolResolver.ResolveAsync` baut Navigation, Distinct- und
    Ambiguous-Ergebnis unverändert auf.
  - `ResolveCandidatesAsync` enthält jetzt ausschließlich den bestehenden
    Lease-Loop inklusive Cancellation, Solution-Fehlern und Diagnostics.
  - `ResolveLeaseAsync` und `FindInLeaseAsync` bleiben unverändert zuständig
    für Einzel-Lease-Auflösung und typisierte Resolution-Misses.
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs`
  - `FromSnapshot`, `FromLease`, `CountStatuses` und Statusauflösung bleiben
    fachlich unverändert; `Project` nimmt statt des gesamten Options-Records
    nur `includeDiagnostics` und `maxDiagnostics` entgegen.
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs`
  - Übergibt an die Projection nur `options.IncludeDiagnostics` und
    `options.MaxDiagnostics`; Runtime-/Daemon-Komposition verbleibt im Builder.
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`
  - Konsolidiert den stabilen `PrimaryCtor-Param`-Marker in einer privaten
    Konstanten für Erzeugung und Filterung.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessLauncher.cs`
  - Konsolidiert die identische primäre Prozessstart-Exception-Nachricht in
    einer privaten Konstanten.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs`
  - enthält weiterhin die 15 Session-/Decompilation-Tests; physisch 374
    Zeilen im Working Tree, MCP-Code-LOC 285.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisCacheTests.cs`
  - enthält die unverändert übernommenen Cache-Policy-, Concurrent-Publish-
    und Delayed-Publish-Tests samt `CreatePublishRequest`.

## Aufrufer und Abhängigkeiten

- `AssemblyFindReferencesTool` ruft `ResolveAsync` in
  `ExecuteWithReferencesAsync` auf.
- `AssemblyGetCallTreeTool` ruft `ResolveAsync` in `BuildResponseAsync` auf.
- `ResolveCandidatesAsync` verwendet nur `AssemblyNavigationLeaseAccess`,
  `AssemblyNavigationSupport`, `FindReferencesTool` und die bestehende
  `AssemblyAnalysisLease`-Vertragsgrenze.
- `GetServerHealthResponseBuilder` ist der einzige direkte Consumer der
  geänderten `AssemblyHealthProjection.Project`-Signatur; die vier direkten
  Projection-Aufrufstellen bleiben laut MCP vollständig sichtbar.
- Cache-Tests verwenden weiterhin `TestTempDirectory`; keine Test-Collection
  und keine globale Serialisierung wurde eingeführt.
- `ProjectLease`, `ProjectRegistry`, Regeln, Logging, CLI, `rules.json` und
  DI-Abhängigkeiten sind nicht betroffen.

## Relevante Tests, Konfiguration und Dokumentation

- `SolutionStateProviderContractTests` bestätigt die Epic-1-Interface-Grenze.
- `AssemblyDecompiledBodyResolverTests` bestätigt Direct-Member-/Body-
  Resolution und typed unavailable results.
- `AssemblyAnalysisRegistryRetirementRaceTests` bestätigt die relevante
  Retirement-/Lease-Concurrency; die bestehende Testabdeckung war ausreichend,
  daher wurden dort keine Assertions ergänzt oder abgeschwächt.
- `AssemblyAnalysisSessionTests` und `AssemblyAnalysisCacheTests` sichern die
  Session-, Cache- und Publish-Concurrency-Invarianten.
- `GetServerHealthToolTests` deckt weiterhin Compact-, Detailed- und Session-
  Diagnostics sowie Session-Limits ab; `GetClassStructureToolTests` deckt den
  Primary-Constructor-Filter ab. Es wurden keine Testsemantiken oder
  Parallelitätsgrenzen verändert.
- Keine Konfigurations- oder Dokumentationsänderung ist für die scoped
  Projection-/lokalen Konsolidierungen erforderlich.

## Invarianten, Risiken und Unsicherheiten

- Resolver-Reihenfolge, Cancellation-Prüfung, Assembly-ID-Filter,
  Diagnostics-Reihenfolge sowie Distinct-/Ambiguous-Ausgabe bleiben erhalten.
- Die Cache-Concurrency-Tests behalten ihre `Barrier`, verzögerte Rückgabe,
  `Task.WhenAll`- und Timeout-Invarianten.
- MCP `get_feature_context`/`find_references` bestätigt zwei direkte
  Resolver-Aufrufstellen; die Tiefen-3-Traversierung liefert 9 Aufrufstellen
  insgesamt (2 direkt, 7 transitiv), einschließlich Registrierungs- und
  Testpfaden. Einen weiteren direkten Resolver-Aufruf gibt es nicht.
- Initialer Epic-3-MCP-Befund: `get_violations` meldete genau den direkten
  `AssemblyHealthProjection`-Footprint `2564 > 2500`; `get_feature_context`,
  `get_symbol_body`, `find_references` und `get_impact` waren für den
  Projection-Pfad vollständig ohne Trunkierung (direkt 4, Tiefe 2 insgesamt
  16 Call-Sites). Die belegte Ursache war der `GetServerHealthOptions`-
  Parameter mit transitivem `McpCodeGraphServer`.
- Nach der Änderung: `AssemblyHealthProjection` misst 1642/2500 Footprint und
  77 LOC; `get_violations` meldet im MCP-Scope 0 Verstöße.
- Verifizierte MCP-Metriken: `ResolveAsync` 40 Codezeilen; Session-Testklasse
  285 Codezeilen / 375 Footprint; Cache-Testklasse 125 Codezeilen / 159
  Footprint. Physische Zeilenzählung: 139 Resolver-, 374 Session- und 146
  Cache-Dateizeilen; alle Datei-/Methodenlimits sind eingehalten.
- Der vorherige Magic-Value-Testscan meldete bestehende testbezogene Literal-Kandidaten
  in `AssemblyAnalysisSessionTests`; sie gehören zur späteren Audit-Triage und
  wurden in diesem Epic nicht semantisch verändert.
- Der aktuelle Produktionsscan liefert 252 eindeutige heuristische Magic-
  Value-Kandidaten (256 Vorkommen in 318 Dateien, bei `maxResults=300` alle
  252 Einträge sichtbar). Der verbleibende `PrimaryCtor-Param`-Fund in
  `GetClassStructureTool.cs` ist ein stabiler Wire-/Format-Marker; die
  Bufferwerte in `MagicValuesStringHeuristics.cs:52` definieren bewusst die
  vollständige erkannte Standardmenge und ihre Zuordnung in Zeilen 59-62.
  Beide sind `accepted-deferred`: Eine weitere Änderung würde entweder den
  stabilen Output-Vertrag verschleiern oder nur Literal-Indirektion ohne
  fachlichen Gewinn einführen. Die übrigen einmaligen Heuristik-Kandidaten
  (Diagnosecodes, Formatter-Header, Exception-Texte und Erkennungspräfixe)
  sind für dieses Epic keine belegten, sicheren lokalen Bereinigungen.

## Verifikation

- `dotnet build` nach der letzten Codeänderung: 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --no-build --filter
  "FullyQualifiedName~WiringProjectContractTests"`: 13/13 bestanden.
- `dotnet test src/AiNetLinter.FastTests --no-build --filter
  "FullyQualifiedName~GetClassStructureToolTests"`: 14/14 bestanden.
- `dotnet test src/AiNetLinter.FastTests --no-build --filter
  "FullyQualifiedName~FindMagicValuesScanner"`: 59/59 bestanden.
- `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter
  "FullyQualifiedName~GetServerHealthToolTests"`: 7/7 bestanden.
- MCP `get_impact`, Projektziel absolut, für `ResolveAsync`: 4 vollständige
  Aufrufstellen, keine Trunkierung.
- MCP `find_duplicates`, production scope `src/AiNetLinter/Mcp`, exact:
  0 Cluster bei 1524 gescannten Methoden, `truncated=false`.
- MCP `find_dead_code`, private/internal high-confidence, production scope:
  0 Kandidaten bei 783 Symbolen, `isTruncated=false`; die dokumentierten
  Heuristik-Limits (Reflection, Serializer, Routing, DI) bleiben bestehen.
- MCP `find_magic_values`, production scope, `changedOnly=false`: 252
  eindeutige Einträge / 256 Vorkommen, vollständig sichtbar bei
  `maxResults=300`; die oben genannten Marker/Whitelist-Funde bleiben
  `accepted-deferred`.
- Abschließender gezielter MCP-`get_violations`-Check nach der letzten
  Codeänderung: 0 Verstöße in 319 MCP-Dateien; das Ergebnis ist vollständig.
