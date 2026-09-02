# Code Map: MCP-Komposition entkoppeln und Qualitätsgrenzen wiederherstellen

## Primäre Einstiegspunkte

- `AssemblySymbolResolver.ResolveAsync` verarbeitet Symbolauflösung über den
  Root-Lease und dessen Reference-Leases.
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
- Keine Konfigurations- oder Dokumentationsänderung ist für dieses reine
  Extract-Method-/Test-Split-Refactoring erforderlich.

## Invarianten, Risiken und Unsicherheiten

- Resolver-Reihenfolge, Cancellation-Prüfung, Assembly-ID-Filter,
  Diagnostics-Reihenfolge sowie Distinct-/Ambiguous-Ausgabe bleiben erhalten.
- Die Cache-Concurrency-Tests behalten ihre `Barrier`, verzögerte Rückgabe,
  `Task.WhenAll`- und Timeout-Invarianten.
- MCP-Impact bestätigt vier Aufrufstellen (zwei direkte, zwei transitive) und
  keine zusätzliche betroffene Produktionskette.
- Verifizierte MCP-Metriken: `ResolveAsync` 40 Codezeilen; Session-Testklasse
  285 Codezeilen / 375 Footprint; Cache-Testklasse 125 Codezeilen / 159
  Footprint. Physische Zeilenzählung: 139 Resolver-, 374 Session- und 146
  Cache-Dateizeilen; alle Datei-/Methodenlimits sind eingehalten.
- Der Magic-Value-Testscan meldet bestehende testbezogene Literal-Kandidaten
  in `AssemblyAnalysisSessionTests`; sie gehören zur späteren Audit-Triage und
  wurden in diesem Epic nicht semantisch verändert.

## Verifikation

- `dotnet build` nach der letzten Codeänderung: 0 Warnungen, 0 Fehler.
- Gezielter FastTest-Slice nach der letzten Codeänderung: 25/25 bestanden,
  0 übersprungen, für Session-, Cache-, Interface-, Body- und Retirement-Race-
  Tests.
- MCP `get_impact`, Projektziel absolut, für `ResolveAsync`: 4 vollständige
  Aufrufstellen, keine Trunkierung.
- MCP `find_duplicates`, production scope `src/AiNetLinter/Mcp`, exact:
  0 Cluster bei 1524 gescannten Methoden.
- MCP `find_dead_code`, private/internal high-confidence, production scope:
  0 Kandidaten bei 783 Symbolen.
- MCP `find_magic_values`, production scope, `changedOnly=true`: 0 Treffer.
- Testscope-Audits: `find_duplicates` 0 Cluster bei 75 Methoden und
  `find_dead_code` 0 Kandidaten bei 17 Symbolen.
- Abschließender gezielter MCP-`get_violations`-Check nach der letzten
  Codeänderung: 0 Verstöße im Resolver-Scope und 0 Verstöße im gesamten
  Assembly-Analysis-Testscope (15 Dateien).
