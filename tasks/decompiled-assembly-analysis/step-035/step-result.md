---
status: done
type: step-result
task: decompiled-assembly-analysis
step: 035
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-30T03:14:33+02:00
code_commit_hash: 5c830e444eb0920523dd8df7088f51011040bf7a
status_after: done
blocker_category: n/a
---

# Result Step 035: ConfigurationFailure unabhängig von Diagnosen terminal bis zum Assembly-Tool propagieren

## Zusammenfassung

Step 035 korrigiert den zentralen Step-034-Befund: Der Selection-Scope trägt jetzt das unveränderte `ExternalSourceConfigurationLoadResult` und prüft den expliziten `Succeeded`-Status. Damit bleiben `Failure([])` und `Failure(nonempty)` gleichermaßen terminale `ConfigurationFailure`; eine leere Diagnose-Liste wird nicht mehr als `NoMatch` fehlinterpretiert. Der bestehende Recoverable-Vertrag bleibt erhalten und behauptet im strukturierten Toolresultat explizit `IsError=false`.

Die gemeinsame strikte `CacheRoot`-Semantik wurde an der bestehenden Factory-/Options-Grenze geprüft und um die vollständige lokale URI-/UNC-/Device-/Reserved-/Query-/Fragment-/Doppelpunkt-Matrix ergänzt. Gültige relative, Laufwerks- und UNC-Pfade bleiben abgedeckt. Eine echte Loader-zu-Tool-Regression deckt leere und nichtleere Konfigurationsdiagnosen ab und stellt sicher, dass Provider, Registry, Context, Decompilation und `BuildResult` nicht erreicht werden.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`: Expliziten Load-Result-Status in den Scope propagiert; `ConfigurationFailure` vor Provider-Fallbacks und unabhängig von der Diagnose-Anzahl klassifiziert.
- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`: Gemeinsame rohe CacheRoot-Prüfung verschärft; Server-only-UNC-Pfade werden neben den bereits gesperrten URI-, Device-, Reserved-, Query-, Fragment- und Doppelpunktformen abgelehnt.
- `src/AiNetLinter.FastTests/Configuration/ExternalSourceCacheRootValidationTests.cs`: Gemeinsame adversariale MemberData-Matrix sowie gültige relative, Laufwerks- und UNC-Pfade für Loader und Factory ergänzt.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisConfigurationFailureTests.cs`: End-to-End-Regressionsfall für `Failure([])` ergänzt und terminalen No-Provider-/No-Context-/No-Build-Vertrag geprüft; `IsError=false`, Diagnosecode und sicherer Hint explizit assertiert.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`: Bestehenden nichtleeren Konfigurationsfehler auf die exakte `IsError=false`-Policy umgestellt.
- `src/AiNetLinter/Mcp/IsErrorPolicy.md`: Terminalität vor Context-Erzeugung und expliziter Recoverable-Vertrag dokumentiert.

## Commits

Code-, Test- und Policy-Commit:

- `5c830e444eb0920523dd8df7088f51011040bf7a` — `fix: Stoppe Config-Failures terminal [decompiled-assembly-analysis]`
- Branch: `main`
- Push: nicht ausgeführt

Dieser Result-Nachweis, die Codemap und der Step-Plan werden in einem separaten Doku-Commit abgelegt.

## Verifikation

- Fokussierte Step-035-/angrenzende Tests: **82 passed, 0 skipped, 82 total**.
- `dotnet build --no-restore`: **0 warnings, 0 errors**.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --no-restore`: **2.158 passed, 2 skipped, 2.160 total**.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore`: **370 passed, 0 skipped, 370 total**.
- Stress-Tests: **nicht ausgeführt**.

Die beiden bekannten FastTest-Skips betreffen die absichtliche Reparse-Prüfung in `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains` und `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`. Beide werden auf Windows wegen `Win32 ERROR_PRIVILEGE_NOT_HELD (1314)` übersprungen; es gab keine zusätzlichen Skips.

## MCP-/Qualitätsnachweis

Alle semantischen Abfragen wurden mit dem projektgebundenen MCP und absolutem `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt.

- `get_feature_context`: `ExternalSourceConfigurationPath` 147 LOC / Footprint 411, `AssemblySourceSelectionOrchestrator` 73 LOC / 1.115, `AssemblySourceSelectionScope` 43 LOC / 627 und `AssemblyAnalysisToolSupport` 134 LOC / 2.169; jeweils 0 direkte Violations.
- `find_symbol`/`get_symbol_body`: `ResolveAsync`, `AssemblySourceSelectionScope.Status`, der Tool-Einstieg und `CreateConfigurationFailureResult` geprüft; der terminale Status-Gate liegt vor Context-Erzeugung und `BuildResult`.
- `find_references`/`get_impact`: `ResolveAsync` mit 17 erwarteten Konsumenten und `ExternalSourceConfigurationPath` mit 4 erwarteten Aufrufern geprüft.
- Direkte `get_violations`-Prüfung der drei geänderten Testdateien: jeweils 0 Violations.
- Scoped `safeguard` für `src/AiNetLinter` mit `minScore=5`: **5,66/10 PASS**, 3 bestehende Struktur-Befunde; kein neuer direkter Befund im Paket. Der ehrliche globale/scoped Vergleich mit Schwelle 8 bleibt **5,66/10 FAIL** und meldet dieselben drei Baseline-Befunde: große Verzeichnisse, `DaemonHostCommand`-Footprint 2.974 und das bestehende Task-Verzeichnis. Die engeren Scopes meldeten 5,50/10 (`Configuration`), 5,80/10 (`Mcp/Assemblies`) und 5,50/10 (`AssemblyAnalysis`) mit derselben Baseline-Vererbung.
- Scoped `find_duplicates`: Configuration Produktion 0/85, Assemblies Produktion 0/371, AssemblyAnalysis Produktion 1/50 bei einem bestehenden semantisch eigenständigen Wrapper-Paar, Configuration-Tests 0/76 und AssemblyAnalysis-Tests 0/44 (jeweils Clone/exact, `minTokens=20`). Die ergänzende Structural/exact-Prüfung mit `minTokens=10` ergab Configuration 0/89, Assemblies 1 bestehenden semantischen Cluster und AssemblyAnalysis 0/56. Keine neue Duplikation aus dem Paket.
- Scoped `find_magic_values`, changed-only/include-tests: Configuration 39 Treffer/39 eindeutige Einträge in 1 Datei, Assemblies 0, AssemblyAnalysis 18/16 in 2 Dateien, gesamt unter `src/AiNetLinter` 82/79 in 5 Dateien. Die Treffer sind bestehende Diagnose-/Vertragskonstanten sowie absichtliche adversariale Testwerte und lokale Fixture-Präfixe; kein neuer produktiver Magic-Value-Befund wurde eingeführt.
- Scoped `find_dead_code`, private/internal high members: Configuration 0 Kandidaten bei 51 Symbolen/27 Dokumenten, Assemblies 0 bei 156/58 und AssemblyAnalysis 0 bei 25/8.

`tech-debt.md` wurde nicht verändert; es wurden keine neuen Tech-Debt-Einträge erzeugt. `roadmap.md` blieb im Fix-Modus unverändert.

## Abweichungen und Risiken

Ein zwischenzeitlicher Entwurf führte ein zusätzliches Bool-Argument im Scope-Konstruktor ein. Der Entwurf wurde vor dem Commit entfernt, weil er einen `MaxConstructorDependencies`-Befund und einen Regressionseinfluss auf den Safeguard-Footprint erzeugte. Die finale Lösung verwendet direkt das bestehende immutable Load-Result und bleibt bei fünf Konstruktorparametern. `AssemblyAnalysisToolSupport` und die Factory wurden nicht unnötig erweitert: Das vorhandene frühe ConfigurationFailure-Gate und die gemeinsame Validator-Grenze waren fachlich ausreichend.

UNC-Positivfälle prüfen Syntax und Kanonisierung ohne Netzwerkzugriff. Die bekannten Win32-1314-Reparse-Skips bleiben bestehen. Die Magic-Value-Heuristik meldet weiterhin absichtliche Rohwerte in der adversarialen Testmatrix; diese sind für den Testvertrag erforderlich und enthalten keine Secrets in Fehlermeldungen.
