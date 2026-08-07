---
task: flaky-and-test-performance
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-07T14:45:00+02:00
---

# CodeMap: flaky-and-test-performance

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`<task-dir>` gelöscht, kein projektweites Artefakt. Enthält **nur**, was
für diesen Task relevant ist (Module/Dateien/Bereiche, die ein Step
tatsächlich berührt hat oder für die Planung des nächsten Steps
gebraucht wird) — kein Anspruch auf vollständige Projektabdeckung.

**Pointer-Prinzip — wie Regel-Index (`roadmap.md`) und Tech-Debt-Index
(`tech-debt.md`):** Jeder Eintrag ist Ort + **ein Satz**, was dort ist
und wozu es für diesen Task relevant ist — keine Verhaltensbeschreibung,
kein „wie funktioniert das im Detail". Verhaltensbehauptungen veralten,
Ortsangaben kaum. Wer mehr wissen muss, liest die Datei selbst nach —
das ersetzt die Map nie, sie beschleunigt nur das Finden.

**Warum das trotzdem verlässlich bleibt (anders als generische Doku):**
Der gesamte Loop läuft strikt seriell — genau ein Subagent gleichzeitig
(`../spec.md` §6). Zwischen einem Coder-Update und dem nächsten Lesezugriff
kann sich am Code strukturell nichts geändert haben, was hier nicht auch
eingetragen wurde. Die Map ist also, solange sie gepflegt wird, tatsächlich
aktuell — kein Snapshot mit Drift-Risiko. **Schritt 2 im Step-Modus des
Planers („tatsächlichen Projektzustand lesen", `../spec.md` §7.2) bleibt
trotzdem Pflicht** — die Map sagt *wo* nachschauen, ersetzt nie das
Nachschauen selbst.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem
  Grobüberblick, den er beim Ableiten der Epics ohnehin über den
  Bestandscode gewinnt (`../skills/planer/SKILL.md` Roadmap-Modus
  Schritt 1).
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich
  angelegte oder geänderte Module, **vor** dem Doku-Commit
  (`../skills/coder/SKILL.md` Schritt 6a).
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen,
  ergänzt neue Bereiche, die er beim Lesen des Ist-Zustands entdeckt.
  Zusätzlich Grundlage für den Anti-Loop-Check (siehe unten).
- **Kritiker:** prüft stichprobenartig, ob die Map dem tatsächlichen Diff
  entspricht (Teil von Ebene 1, Plan-Erfüllung) — schreibt selbst nur bei
  offensichtlicher Lücke/Fehler nach, ist aber nicht Haupt-Pfleger.

## Anti-Loop-Nutzen

Bevor der Planer im Step-Modus einen neuen Step plant, gleicht er sein
Vorhaben gegen die hier verzeichneten, bereits getroffenen Entscheidungen
ab. Widerspricht der neue Plan erkennbar einem hier festgehaltenen,
bereits umgesetzten Stand (z. B. Step-234 würde zurückdrehen, was Step-123
laut Map bewusst so gebaut hat): entweder im neuen Step-Plan explizit als
Erweiterung begründen, oder den alten Eintrag hier als „obsolet —
<Grund>" markieren (nicht löschen) — nie stillschweigend widersprechen.
Das verhindert kein Kreisen zu 100 %, macht ein Hin-und-Her aber
wenigstens sichtbar und begründungspflichtig statt stillschweigend.

## Karte

### Test-Verzeichnisse — berührt

- **`src/AiNetLinter.Tests/Suppression/`** — alle 8 Testklassen (7 Unit, 1 Integration) in step-002 mit `[Trait("Category", ...)]` auf Klassen-Ebene versehen (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Metrics/`** — alle 7 Testklassen in step-003 mit `Unit`-Trait versehen (zuletzt: step-003)
- **`src/AiNetLinter.Tests/Commands/McpServerCommandFindReferencesTests.cs`** — `IClassFixture<SymbolGraphMcpFixture>` durch `[Collection("SymbolGraphMcp")]` ersetzt; einziger Test, read-only (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs`** — dito, einziger Test, read-only (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Commands/McpServerCommandGetImpactTests.cs`** — dito; 2 Tests, read-only, lokal zusätzlich `GitImpactMiniFixtureWorkspace` (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Commands/McpServerCommandMissHintTests.cs`** — dito, einziger Test, read-only (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`** — `SymbolGraphMcpFixture`-Anteil auf Collection umgestellt, `BaselineMcpFixture` (1× verwendet) bleibt `IClassFixture`; 18 Tests, davon 15 lesend über die Collection-Fixture (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs`** — enthält den pre-existing Flaky-Test `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` (Z. 112-150, Poll-Loop mit fixer 5s-Deadline, Thread-Pool-abhängig); Ziel von EPIC-06; `IClassFixture<SymbolGraphCatalogFixture>` (1×-Verwendung, kein Sharing-Hebel) (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Mcp/McpServerAllToolsE2ETests.cs`** — `IClassFixture<SymbolGraphMcpFixture>` durch `[Collection("SymbolGraphMcp")]` ersetzt; **NITPICK aus step-001-Review:** XML-Doc-Kommentar in Z. 15 spricht weiterhin von „einmaliger Fixture- und Client-Instanziierung pro Testklasse", formal unzutreffend (kosmetisch, kein Rule-Verstoß) (zuletzt: step-001)

### Test-Fixtures — im Plan-Scope, noch nicht umgestellt (EPIC-03)

- **`src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpCollection.cs`** — NEU in step-001, leere xUnit-v3-`[CollectionDefinition]`-Klasse als Marker für geteilte `SymbolGraphMcpFixture`-Instanz; Spike-Empfehlung war negativ (kein Performance-Gewinn), finale Entscheidung in EPIC-03 (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpFixture.cs`** — XML-Doc-Kommentar in Z. 13 in step-001 an neue Verwendungsform `[Collection("SymbolGraphMcp")]` angepasst; teure Subprozess-Start-Logik (MCP-Client + Retry-Backoff) im Konstruktor (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Fixtures/SymbolGraphCatalogFixture.cs`** — laut step-001-Code-Inspektion **nur 1× verwendet** (`McpServerCommandLoadingStateTests`); entgegen der Konzept-Annahme „18×" gilt das Sharing-Hebel-Potenzial heute praktisch nicht; in Mini-Solution + `SourceFileCatalog.LoadAsync`, in-process (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs`** — 2× verwendet (`McpDocumentationSmokeTests`, `McpLiveRepositoryTests`); startet Subprozess auf **echtem** `AiNetLinter.slnx` — laut Konzept die schwersten Einzel-Loads im Lauf; EPIC-03-Kandidat mit ähnlichem Profil wie `SymbolGraphMcpFixture`-Spike (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Fixtures/BaselineMcpFixture.cs`** + **`BaselineCatalogFixture.cs`** — je 1× verwendet, kein Sharing-Hebel; bleiben voraussichtlich `IClassFixture` (zuletzt: step-001)

### Test-Fixtures — zentrale Infrastruktur für die Performance-Story

- **`src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs`** — globales `SemaphoreSlim(6, 6)` mit 60s-Timeout, serialisiert gleichzeitige `AiNetLinter.exe`-Subprozessstarts; Grund laut Kommentar: `MSBuildLocator` ist prozessglobaler State; Kapazität wurde bereits von 4 auf 6 erhöht (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Fixtures/CliProcessRunner.cs`** — `RunLinterAsync`/`RunAsync` startet je Aufruf einen `dotnet AiNetLinter.dll`-Subprozess (Muxer-Overhead zusätzlich zum Prozessstart); genutzt von `Cli/CliIntegrationTests`, `Cli/FilterCliIntegrationTests`, `Baseline/BaselineCliTests`, `Commands/CliBatchRegressionTests`, `Suppression/DisableAllCliTests`, `Baseline/WebBaselineTests` (zuletzt: step-001)
- **`src/AiNetLinter.Tests/Fixtures/*MiniFixtureWorkspace.cs`** (Baseline, CompileError, SymbolGraph, BlazorPartial, GitImpact, SingleCompileError) — in-process Mini-Filesystem-Fixtures; in EPIC-02 als **Negativ-Abgrenzung gegen Subprozess-Trait** relevant (Temp-Dir, kein Subprozess) (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Fixtures/LoadFixtureMeasurementsTests.cs`** — 2 Tests mit synthetischen 1k-/5×200-Datei-Solutions, laut Konzept bewusst lange Laufzeit (bis 30s/5s) einkalkuliert (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Fixtures/LoadFixtureBuilderTests.cs`** + **`TD016aRefactorTests.cs`** — bereits mit Category-Trait versehen, fallen aus EPIC-02-Batch-Reihe raus (zuletzt: step-002)

### Test-Verzeichnisse — geplant für EPIC-02-Folge-Batches (noch ungetraggt)

Reihenfolge und Aufteilung wie in `step-002/step-plan.md` §"Notes" skizziert; konkrete Step-Planung ist Sache der jeweiligen Planer-Aufrufe.

- **`src/AiNetLinter.Tests/Web/`** — 5 Klassen (`CssAnalyzerTests`, `JsAnalyzerTests`, `RazorAnalyzerTests`, `RazorAnalyzerExtendedTests` in `RazorAnalyzerTests.Extended.cs`, `WebSuppressionDetectorTests`); mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen (zuletzt: step-004)
- **`src/AiNetLinter.Tests/Architecture/`** — 1 Klasse (`ArchitectureTests`); mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen (zuletzt: step-005)
- **`src/AiNetLinter.Tests/Diagnostics/`** — 1 Klasse (`PerformanceProfilerTests`); mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen (zuletzt: step-005)
- **`src/AiNetLinter.Tests/FalsePositives/`** — 2 Klassen; mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen (zuletzt: step-005)
- **`src/AiNetLinter.Tests/Cache/`** — 3 Klassen (`AnalysisCacheManagerTests`, `AnalysisCacheManagerIsolationTests`, `CacheEntryMapperTests`); mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen; `AnalysisCacheManagerIsolationTests` zudem 4× method-level `Unit` (additiv, unverändert seit step-005) (zuletzt: step-005)
- **`src/AiNetLinter.Tests/Evals/`** — 3 Klassen (`EvalAssemblerTests`, `SpecLoaderTests`, `ListEvalsCommandTests`); **alle 3 Unit (ListEvalsCommandTests-Subprozess-Hypothese in step-006 widerlegt)** mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen (zuletzt: step-006)
- **`src/AiNetLinter.Tests/Output/`** — 9 Test-Klassen + 1 Helper (`TestLintConsole.cs`, ausgenommen — Helper-Klasse ohne `[Fact]`/`[Theory]`, Heuristik-Punkt 6); step-007 = erste 5 Klassen (alphabetisch D–O: `DebtReportBuilderHeaderTests`, `DebtReportBuilderTests`, `LinterErrorFormatterTests`, `McpLintConsoleTests`, `OutputRootResolverTests`) mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen; step-008 = restliche 4 Klassen (alphabetisch P–V: `PathNormalizerTests`, `RuleLegendRegistryTests`, `ViolationMarkdownFormatterTests`, `ViolationSummaryBuilderTests`) noch ausstehend (zuletzt: step-007)
- **`src/AiNetLinter.Tests/Configuration/`** — 8 Klassen; rein Unit, geplant für Batch „Reine-Unit-Ordner, groß" (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Core/Checkers/`** — 27 Klassen; rein Unit, mehrere Batches (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Core/`** — 19 Klassen; rein Unit, mehrere Batches (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Maps/`** + **`Maps/Skeleton/`** — 6 Klassen; rein Unit, dito (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Mcp/Tools/`** — 17 Klassen (Tool-Registrierungs-/Scanner-Tests); fast alle Unit über Mini-Fixture-Workspaces, 2-3 Batches (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Mcp/`** — 19 Klassen, **gemischt**: `McpCodeGraphServer*Tests` (4 Klassen) Unit, `McpLiveRepositoryTests` + `McpDocumentationSmokeTests` Integration via `McpLiveRepositoryFixture`; enthält zudem `McpTestClientParallelTests.cs:18-37` (Long-Running-Test >1 min, 16 parallele Subprozesse → Gate-Kontention) — geplant für Batch „Verzeichnisse mit echtem Subprozess-Anteil" (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Baseline/`** — 10 Klassen, **gemischt**: `BaselineCliTests`, `WebBaselineTests`, `SourceFileCatalogRegisterMSBuildTests.cs:50-97` (20 parallele `SourceFileCatalog.LoadAsync`-Aufrufe **ohne** Gate-Schutz) als Integration, restliche `SourceFileCatalog*Tests` als Unit; dito (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Commands/`** — 17 Klassen, **stark gemischt**; `McpServerCommandTests` als prominenteste gemischte Klasse (5 Unit + 18 Integration in einer Klasse) erfordert pro-Methode-Tagging in eigenem Step; restliche Klassen ähnlich gemischt; geplant für Batch „Commands" (zuletzt: step-002)
- **`src/AiNetLinter.Tests/Cli/`** — 6 Klassen (`CliIntegrationTests`, `FilterCliIntegrationTests`, `IgnoreSuppressionsCliTests`, `IgnoreSuppressionsIntegrationTests`, `ProgramTests`, `CliCommandBuilderMcpLogTests`); gemischt, `ProgramTests` bereits Integration-getraggt (siehe step-002 als Referenz); Aufräum-Batch am Ende (zuletzt: step-002)

### Produktionscode — relevant für EPIC-04, EPIC-05, EPIC-06

- **`src/AiNetLinter/Cli/CliOptionFactory.cs`** — fehlende `--self-lint`-Option, siehe TD-001 in `tech-debt.md`; wird in `roadmap.md` und `konzept.md` als Self-Lint-Befehl referenziert, existiert aber nicht; EPIC-04-relevant (Fast-Path-Befehls-Etablierung hängt indirekt daran) (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/McpCodeGraphServer.cs`** — Hauptimplementierung des MCP-Servers; laut Konzept §"Wie" Schritt 5 und Konzept §"Muss-Haven" letzter Punkt potenzielles Ziel für einen leichteren/mockbaren In-Process-Lade-Pfad (EPIC-05) (zuletzt: step-002)
- **`src/AiNetLinter/Mcp/`** (ohne `Mcp/Tools/`) — enthält u. a. `McpCallLog`, `McpFileState`, `McpServerOptionsFactory`, `ServerLoadState` u. a.; Bereich, in dem EPIC-05 ansetzen würde, falls `SourceFileCatalog`/`McpCodeGraphServer` einen in-process-Pfad bekommen (zuletzt: step-002)
- **`src/AiNetLinter/Mcp/Tools/`** — Tool-Implementierungen (Scanner, Formatter, Resolver); Berührung mit EPIC-05 eher nachgelagert, falls Tool-Aufrufe umgehängt werden (zuletzt: step-002)
- **`src/AiNetLinter/Baseline/SourceFileCatalog.cs`** — In-Process-Loader (MSBuildWorkspace), laut Konzept §"Wie" Schritt 5 Kandidat für einen mockbaren Lade-Pfad (EPIC-05) (zuletzt: step-002)
- **`src/AiNetLinter/Configuration/`** — `FileFilterEvaluator`, `ConfigNormalizer`, `ConfigSyncer`, `ConfigLoader`, `Config` u. a. (18 Dateien insgesamt); berührt von vielen EPIC-02-Test-Klassen; relevant, falls Produktionscode-seitige Trait-Tests nötig werden (zuletzt: step-002)

### Konfiguration, Doku, Workflow — Kontext für Planung und Verifikation

- **`src/AiNetLinter.Tests/xunit.runner.json`** — xUnit-v3-Konfig (`parallelizeTestCollections: true`, `maxParallelThreads: 0` = Prozessorzahl, `longRunningTestSeconds: 3`); zentral für EPIC-03 (Sharing-Erhalt der Parallelität) und EPIC-04 (Fast-Path-Verhalten); Trait-Attribute haben **keinen** Einfluss auf Parallelismus (nur `[Collection]`/`DisableParallelization`) (zuletzt: step-001)
- **`rules.json`** (Projekt-Root) — Linter-Regelwerk, Grundlage für `--self-lint`; muss in step-DoD lauffähig bleiben; Self-Lint-Aufruf erfolgt aktuell über `--config rules.json --path .` als TD-001-konformer Ersatz (zuletzt: step-001)
- **`Docs/configuration.md`** + **`Docs/ROADMAP.md`** — gemäß `AGENTS.md` §3 bei CLI-/Rules-Änderungen mitzupflegen; relevant für TD-001-Auflösung (Variante a: CLI-Option `--self-lint` nachrüsten + Doku-Update) (zuletzt: step-001)
- **`.github/workflows/release.yml`** — einziger Workflow, reiner Build/Publish bei Tag-Push, **führt keine Tests aus**; laut Konzept §"Nice-to-Have" naheliegende Folge, aber explizit out of scope (zuletzt: step-002)
- **`.agents/rules/AiNetLinterRichtlinien.mdc`** — Hauptträger der für diesen Task relevanten Workflow-/Stil-Regeln (Kommentar-Disziplin ohne `step-`/`TD-`/`EPIC-`-Verweise, Commit-Subject ≤ 72 Zeichen, Self-Lint-Pflicht, Testsuite-Parallelität bewahren, Zero-Warning-Direktive, Symptom-Fixing verboten); siehe Regel-Index in `roadmap.md` (zuletzt: step-002)
- **`.agents/rules/AiNetLinter.mdc`** — auto-generierte Codequalitätsregeln aus `rules.json` (sealed-Klassen, Methoden-LOC, `*.Tests`-Overrides wie `EnforceSealedClasses: false`, `MaxMethodLineCount: 100`); bei jedem Code-Step zu prüfen (zuletzt: step-002)
