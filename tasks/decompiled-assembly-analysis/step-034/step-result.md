# Step-034 Ergebnis: Strikter CacheRoot-Vertrag und terminale Config-Failure-Weitergabe

## Status

Der Implementierungs- und Teststand ist im Commit
`fcad25e5594923a362fffc113ed709c21d2a6535` enthalten. Der Commit umfasst die
Produktionsänderungen, die fokussierten Regressionen und die Policy-Anpassung.
Diese Ergebnisdatei und die Codemap werden anschließend in einem separaten
Dokumentationscommit ergänzt. Der Step ist **done (pending critic review)**;
ein Kritiker ist für die abschließende Vertragsprüfung erforderlich.

## Vertragsabdeckung

- `ExternalSourceConfigurationPath` klassifiziert den rohen `CacheRoot` vor
  jeder Kanonisierung side-effect-frei. Abgewiesen werden URI-/Credential-
  und Authority-Formen, Query/Fragment, Nicht-Drive-Doppelpunkte,
  Device-/reservierte Namen sowie `.`-/`..`-Segmente und ungültige
  Segmentzeichen. Relative Pfade werden weiterhin relativ zur Settings-Datei
  aufgelöst; gültige Laufwerks- und UNC-Pfade bleiben verwendbar.
- `ExternalSourceCacheOptions` und
  `ExternalSourceRepositoryCacheOptionsFactory` verwenden dieselbe strikte
  CacheRoot-Grenze. Die generische Root-/Reparse-/Ownership-Prüfung für
  andere Cache-Unterpfade wurde nicht global verschärft. Die bestehende
  RefreshInterval-, `<CacheRoot>/source`-, Factory- und Policy-Verdrahtung
  bleibt erhalten. Der gemeinsame generische CacheRoot-Fehlertext beseitigt
  außerdem die lokale Options-/Factory-Duplikation.
- Ein explizit fehlerhafter Loader-Load bleibt `Succeeded == false` und
  `Configuration == null`; er fällt nicht auf Default-Options oder eine leere
  Source-Auswahl zurück. Diagnosecode und Meldung enthalten weder Roh-CacheRoot
  noch Secret.
- `AssemblySourceSelectionStatus.ConfigurationFailure` ist an der Scope von
  `NoMatch`, `Ambiguous`, `Matched` und `ProviderUnavailable` unterscheidbar.
  `ResolveAsync` beendet den Config-Failure-Pfad vor Provider-Aufruf und
  Registry-Acquisition. Die gewöhnlichen No-Match-, Provider- und
  Capability-Fallbacks bleiben statische Decompilation-Erfolge.
- `AssemblyAnalysisToolSupport` prüft den terminalen Status vor
  `CreateContextAsync` und liefert über `McpToolResults.Recoverable` ein
  strukturiertes `isError=false`-Resultat mit Diagnosecode und sicherem Hint.
  In diesem Pfad entstehen kein Context, keine `OriginKind=decompiled`-
  Payload, kein `BuildResult` und kein stiller Erfolg. Scope und Registry
  werden deterministisch beendet.

## Geänderte Dateien

- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
- `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`
- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheOptionsFactory.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs`
- `src/AiNetLinter/Mcp/IsErrorPolicy.md`
- `src/AiNetLinter.FastTests/Configuration/ExternalSourceCacheRootValidationTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisConfigurationFailureTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`
- `tasks/decompiled-assembly-analysis/codemap.md`
- `tasks/decompiled-assembly-analysis/step-034/step-result.md`

`Docs/configuration.md` war nicht zu ändern: Die bestehende Dokumentation
beschreibt bereits die jetzt durchgesetzte strikte Ablehnung.

## Deterministische lokale Nachweise

| Lauf | Ergebnis |
|---|---:|
| fokussierte Step-034-/angrenzende FastTests | 47 bestanden, 0 Skips, 47 gesamt |
| `dotnet build --no-restore` | 0 Warnungen, 0 Fehler |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --no-restore` | 2.123 bestanden, 2 Skips, 2.125 gesamt |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore` | 370 bestanden, 0 Skips, 370 gesamt |
| Stress | nicht ausgeführt |

Die beiden transparenten FastTest-Skips bleiben unverändert:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

Beide Host-Skips beruhen auf `ERROR_PRIVILEGE_NOT_HELD (1314)` beim Erzeugen
des realen Reparse-Falls. Es wurde keine globale Reparse-/Win32-Sperre ergänzt.
Es wurden keine Netzwerk-/Credential-Zugriffe und keine fremden Assemblies
geladen oder ausgeführt; die Regression erzeugt nur eine lokale Test-DLL für
den bereits metadata-only getesteten Toolpfad.

## MCP- und Qualitätsnachweis

Alle semantischen MCP-Aufrufe verwendeten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`; `rg` wurde nur für
Text-/Dateisuche verwendet.

### Feature-Kontext und Symbolgraph

`get_feature_context` ergab für alle betroffenen Produktionssymbole 0 offene
Violations. Die gemessenen Werte sind:

| Symbol | Type LOC | AI-Context-Footprint |
|---|---:|---:|
| `ExternalSourceConfigurationPath` | 143 | 407 |
| `ExternalSourceCacheOptions` | 29 | 407 |
| `AssemblySourceSelectionOrchestrator` | 73 | 1.108 |
| `AssemblySourceSelectionScope` | 41 | 624 |
| `AssemblyAnalysisToolSupport` | 134 | 2.162 |
| `ExternalSourceRepositoryCacheOptionsFactory` | 19 | 968 |
| `AssemblyAnalysisToolRegistrations` | 132 | 2.496 |

`find_symbol` fand den neuen Status, den gemeinsamen CacheRoot-Helper und die
terminale Resultat-Hilfe. `get_symbol_body` bestätigte die Bodies von
`TryCanonicalizeCacheRoot`, `ResolveAsync`, `Status`, `Dispose` und
`CreateConfigurationFailureResult`. `find_references` bestätigte zwei
produktive Aufrufer des gemeinsamen CacheRoot-Helpers, die getrennte
Status-/Resolve-Nutzung und die einzige Tool-interne Resultat-Erzeugung.
`get_impact` zeigte die bestehenden Orchestrator-/Tool-Aufrufer in Host,
Tools und Tests; kein unerwarteter Consumer wurde ergänzt.

### Safeguard

Der Safeguard wurde scoped mit `minScore=8` und `maxViolations=100` ausgeführt.
Die echten Werte bleiben unter dem Threshold und werden nicht schöngeschrieben:

| Scope | Score | Ergebnis |
|---|---:|---|
| global | 5,66/10, Threshold 8,00 | FAIL, 3 Verstöße, 840 Klassen |
| `src/AiNetLinter/Mcp/Assemblies` | 5,80/10, Threshold 8,00 | FAIL, 3 Verstöße, 74 Klassen |
| `src/AiNetLinter/Configuration` | 5,50/10, Threshold 8,00 | FAIL, 3 Verstöße, 18 Klassen |
| `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis` | 5,50/10, Threshold 8,00 | FAIL, 3 Verstöße, 7 Klassen |

Die verbleibenden Befunde sind der bestehende `Assemblies`-Directory-
Befund (58 Einträge), der bestehende `DaemonHostCommand`-Footprint
(2.971 > 2.500, mit `ExternalSourceConfigurationLoadResult` 407 als Top-
Abhängigkeit) und das bestehende Task-Directory (42 Einträge). Der neue
Registration-Footprint liegt mit 2.496 knapp unter dem Limit; ein globaler
Safeguard-Repair war nicht Bestandteil dieses Steps.

### Scoped DRY-, MagicValues- und DeadCode-Audit

- `find_duplicates` im Configuration-Produktionsscope mit `mode=clone`,
  `minTokens=20`, `similarityThreshold=exact` fand 0 Cluster bei 85
  Methoden; der strukturelle Lauf fand 0 Kandidaten bei 89 Methoden. Der
  vorherige exakte Loader-Diagnosewrapper wurde in diesem Paket auf den
  gemeinsamen `CreateError`-Helper konsolidiert.
- Im Assemblies-Produktionsscope fand der exakte Clone-Lauf 0 Cluster bei
  371 Methoden. Der strukturelle Lauf meldete 4 Prüfcluster bei 423 Methoden;
  sie betreffen bestehende, semantisch getrennte Transport-, Failure- und
  Native-Helper. Keine neue direkte CacheRoot-Duplikation blieb zurück.
- Im AssemblyAnalysis-Produktionsscope fand der exakte Clone-Lauf 1
  bestehendes Cluster bei 50 Methoden: die stark typisierten
  `InspectAssemblyTool`-/`FindAssemblyExtensionsTool`-Entry-Points. Es wurde
  nicht mechanisch zusammengelegt, weil ihre Argument-/Payload-Pipelines
  unabhängig typisiert sind und ein gemeinsamer Shim den Toolvertrag nur
  verbreitern würde. Der strukturelle Lauf meldete 3 bestehende Prüfcluster
  bei 56 Methoden; sie sind als tool-/service-spezifische Mapper bzw.
  Diagnostics-Helfer semantisch getrennt.
- `find_magic_values` mit `changedOnly=true`, `includeTests=true` ergab 39
  Einträge im Configuration-Scope, 1 im Assemblies-Scope und 0 im
  AssemblyAnalysis-Scope. Die 39 sind die bestehende Familie von
  Diagnose-/Vertragskonstanten; der gemeinsame CacheRoot-Fehlertext wird
  zentral verwendet. Es wurde kein neuer Secret-/URL-/Pfad-Magic-Value in
  die Produktionslogik eingeführt.
- `find_dead_code` mit `accessibility=private_internal`,
  `confidence=high`, `kind=all`, `includeTests=true`, `mode=members` ergab
  0 Kandidaten in Configuration, Assemblies und AssemblyAnalysis.
- Es wurden keine globalen TD-001-bis-TD-003-Sweeps und keine neuen
  `tech-debt.md`-Einträge erzeugt; die verbleibenden bestehenden Origin- und
  Drive-Path-Themen liegen außerhalb dieses Vertrags oder benötigen eine
  separate Architekturentscheidung.

## Risiken und Übergabe

- Der breite Safeguard bleibt wegen bestehender Directory-/Footprint-Schuld
  FAIL; der relevante Assemblies-Baselinewert 5,80/10 bei Threshold 8,00 ist
  oben ausdrücklich dokumentiert.
- Die beiden 1314-Reparse-Skips bleiben hostabhängig und sind unter Win32-
  Reparse-Berechtigung nachzuholen.
- Der Registration-Footprint hat nur 4 Zeilen Budgetreserve (2.496/2.500);
  künftige Erweiterungen an der Assembly-Composition sollten diesen Rand
  erneut prüfen.
- Ein Kritiker-Review ist erforderlich, insbesondere für die strikte
  UNC-/Device-/reservierte CacheRoot-Matrix, die secret-freie Resultatgrenze
  und den unveränderten positiven Fallback-Vertrag.
