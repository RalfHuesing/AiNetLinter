---
status: done
type: step-review
task: codegraph-mcp-finish
step: 008
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-03T21:55:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 008: `ILinterEngineConfig`-Interface extrahieren, PathOverride-Liste auf Rest reduzieren

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Fix-Step `step-008/fix-XX/` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (bzw. TD-005-Baseline beachtet)

## Befund

### Plan-Erfüllung

Alle 5 Touch-Points (`ILinterEngineConfig.cs` NEU, `Config.cs`, `McpCodeGraphServer.cs`, `McpCodeGraphServerOptions.cs`, `GetViolationsScanner.cs`) plus `rules.json` wie geändert; Weg A (Downcast am Call-Site) wie vom Planer präferiert umgesetzt, `McpCodeGraphServerOptions.From(...)` bleibt strukturell kompatibel → 12-13 Test-Dateien kompilieren ohne Inhalts-Änderung (empirisch durch 1185/1186 Test-Lauf bewiesen).

### Rules-Konformität

`MaxConstructorDependencies: 5` (Richtlinien §2/§4) weiter eingehalten — `McpCodeGraphServer` hat 1 Konstruktor-Parameter, `McpCodeGraphServerOptions` 4 Properties; das neue Interface fügt keine Dependencies hinzu, kein DI-Container. §5 (keine TD-/Plan-Artefakt-Referenzen): nur Architektur-Rationale-Kommentare (Record-Semantik, `MaxConstructorDependencies: 5`, Footprint-Entkopplung), keine `step-`/`TD-`/`EPIC-`-Verweise. `Config` bleibt `public sealed record`, `McpCodeGraphServer`/`McpCodeGraphServerOptions` `internal sealed`. `TreatWarningsAsErrors` hält (Build 0/0).

### Logische Korrektheit

`ILinterEngineConfig` exportiert exakt die 11 Properties, die in `LinterEngine` (3 Property-Lesungen: `SolutionBasePath`, `TestSentinel`, `FileFilters`) und allen übrigen `config.X`-Konsumenten (`PostAnalysisChecks`, `WebFileSeparationChecker`, `SourceFileCatalog`, `AgentRulesGenerator`, `RuleMetadataRegistry`, `UiFileSeparationChecker`, `ConfigNormalizer`, `ConfigLoader`, `PartialClassLineAggregator`, `AuditCommand`) benötigt werden — Interface ist vollständig, keine Property fehlt (per Grep über `src/AiNetLinter` gegen alle `config.(Global|Metrics|TestSentinel|FileFilters|UiSeparation|Web|RuleMetadata|ForbiddenNamespaceDependencies|ProjectOverrides|PathOverrides|SolutionBasePath)`-Zugriffe verifiziert). Downcast in `GetViolationsScanner.cs:53` ist strukturell sicher: `ILinterEngineConfig` ist `internal` und projektweit nur **einmal** implementiert (`Config.cs:7`, per Grep bestätigt). Verhalten der 12 Tool-Klassen, die `Config` nur transitiv über `McpCodeGraphServer` referenzierten, ist semantisch unverändert (sie lesen die Property gar nicht). Die 2 verbleibenden PathOverrides (`FindReferencesTool` 2529, `FindSymbolTool` 2516) sind plausibel begründet — Symbol-Graph-Lookups koppeln strukturell an `Configuration`-Sub-Typen, Aufspaltung gehört zu EPIC-08.

### Konzept-Treue (Ebene 4)

`Konzept.md` „Muss-Haben C" Zeile 285-292 ist 1:1 umgesetzt: `internal interface ILinterEngineConfig` mit Linter-/Tool-bedarfsgenauen Properties ✓, `McpCodeGraphServer.Config`/`McpCodeGraphServerOptions.Config` auf Interface-Typ ✓, `PathOverride`-Liste von 14 auf 2 Rest-Einträge mit per-Eintrag-Begründung reduziert ✓, Reihenfolge vor EPIC-04/05/06/07/08 eingehalten ✓. TD-008/TD-010 nicht als neue Tech-Debt-Einträge erzeugt (waren nie im Index, sind mit diesem Step inhaltlich erledigt — der Coder hat das korrekt gehandhabt; Planer hatte in `step-plan.md` Punkt 6 explizit so vorgesehen).

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx  → grün (0 Warnungen, 0 Fehler, 3.06s inkrementell)
dotnet test  AiNetLinter.slnx --no-build  → 1185/1186 in 4m 16s, 1 TD-005-Flake (infrastructure)
dotnet test  AiNetLinter.slnx --no-build  → 1185/1186 in 5m 11s, 1 TD-005-Flake (infrastructure)
dotnet test  ... --filter "FullyQualifiedName~McpServerCommandErrorHandlingTests"  → 2/2 grün (16s, isoliert)
dotnet test  ... --filter "FullyQualifiedName~RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection"  → 1/1 grün (10s, isoliert)
```

Test-Fehlerdetails: `McpServerCommandErrorHandlingTests.RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection` (Lauf 1: 30.09s, exakt Gate-Timeout; Lauf 2: 37.47s, Cancellation-Propagation aus dem Gate in den `McpClient.ConnectAsync`-Stack hinein). **Klassifikation: `infrastructure`** (TD-005-Last-Signatur, scope-extern): Stack-Bottom ist `SubprocessConcurrencyGate.AcquireAsync:30` (`SemaphoreSlim.WaitUntilCountOrTimeoutAsync`-Timeout) in beiden Läufen, isolierte Läufe der Klasse und des Einzeltests laufen grün → Last-Sättigung am 4-Slot-Gate unter Volllauf-Druck, nicht change-bedingt. Kein `content`-Finding, kein Fix-Versuch verbraucht, konsistent mit step-007/fix-01-Befund.

## Sonstige Beobachtungen / MINOR / NITPICK

- **Coder-Doku-Ungenauigkeit in `step-result.md`:** Der Coder schreibt „Lauf 1: 1185/1186 grün, 1 Failure (`McpServerCommandErrorHandlingTests.RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError`...)" — in **meinen** beiden Reproduktionsläufen schlug stattdessen `RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection` fehl, ebenfalls in `McpServerCommandErrorHandlingTests` und mit derselben `SubprocessConcurrencyGate.AcquireAsync:30`-Signatur. Klassifikation (`infrastructure`) und Begründung bleiben korrekt; die exakte Test-Bezeichnung im Result ist aber von mir nicht reproduziert worden. Falls der Coder einen anderen Test gesehen hat, lohnt sich beim nächsten `TD-005`-Followup ein Blick darauf, ob die ganze `McpServerCommandErrorHandlingTests`-Klasse am Gate-Timeout hängt (zwei verschiedene Tests deuten eher auf Klassen- als auf Test-Ebene-Anfälligkeit hin).
