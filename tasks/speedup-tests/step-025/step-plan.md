---
status: issues
type: step-plan
task: speedup-tests
step: 025
corrects: null
title: "EPIC-6-Start: deterministische Mini-MCP-Prozesshosts (21 Klassen)"
epic: EPIC-6
estimated_risk: high
step_type: batch
items:
  - id: item-01
    title: "MCP-Policy-, Options-, Loading- und Symbolvertraege auf FastTests schneiden"
    source: "konzept.md Leitplanken 1/3/5 / Legacy-Ledger"
  - id: item-02
    title: "Lazy read-only Mini-MCP-Host fuer idempotente E2E-Smokes"
    source: "konzept.md Leitplanke 5 / Legacy-Ledger"
  - id: item-03
    title: "Exklusive Hosts fuer Framing, Retry, Fehler, Git und Refresh"
    source: "konzept.md Leitplanke 5 / TD-001"
  - id: item-04
    title: "Prozesslebensdauer budgetieren und Integration-Runner deckeln"
    source: "konzept.md Leitplanken 5/6 / SubprocessConcurrencyGate"
  - id: item-05
    title: "Ledger-, Kategorie-, Dependency- und Prozessleck-Grenzen nachziehen"
    source: "konzept.md Leitplanken 6/8 / TD-006"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13T22:32:00+02:00
related_to:
  - step-024/step-review.md
---

# Step 025: EPIC-6-Start: deterministische Mini-MCP-Prozesshosts (21 Klassen)

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-6` aus `roadmap.md` — CLI-/MCP-Prozess-, Dogfood-, Performance- und
  Stressvertraege entlang echter Host-/Isolationgrenzen migrieren.
- **Konzept-Referenz:** `konzept.md` Leitplanke 5 „MCP- und Prozessstrategie", Leitplanke 6
  „Kategorisierung als Architekturvertrag" samt Runner-/Prozessparallelitaet, Leitplanke 7
  „Sparsame Verifikation", Leitplanke 8 „Strangler-Migration" und §9 „Grosse Drift-Loop-Steps".
- **Freigegebener Ausgangspunkt:** Step 024 ist mit Commit `8b577ca` approved; EPIC-5 ist
  abgeschlossen und seine Max-2-MSBuild-Loadgrenze bleibt unveraendert.

## Aktueller Projektzustand (JIT-Kontext)

- Im Legacy-Ledger stehen noch 74 Klassen auf `pending`. Davon gehoeren 38 zur breiten
  EPIC-6-Inventur (Baseline/CLI/Commands/MCP/Fixtures/Diagnostics). Die uebrigen 36 Core-,
  FalsePositive-, Maps-, Metrics- und Outputklassen sind Restmigration fuer EPIC-7 und werden hier
  nicht vorgezogen.
- Die groesste geschlossene Hostfamilie innerhalb des Super-Step-Budgets sind 21 Legacy-Klassen
  mit 121 Testmethoden/121 statisch sichtbaren xUnit-Faellen: elf
  `Commands/McpServerCommand*Tests`, neun direkte `Mcp/*Tests` und
  `Mcp/Tools/SymbolGraph/GetImpactToolTests`. Ihr Bestand umfasst 3.012 Quellzeilen, wird aber
  ueberwiegend als Git-Rename verschoben und fachlich entlang der Hostgrenze geteilt; die
  inhaltlichen Infrastruktur-/Guard-Aenderungen muessen unter etwa 800 Diffzeilen bleiben.
- Die heutigen idempotenten SymbolGraph-Smokes teilen `SymbolGraphMcpFixture` ueber
  `[Collection("SymbolGraphMcp")]`. Das spart Starts, serialisiert aber sechs Testklassen. Daneben
  startet `McpServerCommandTests` einen zweiten `BaselineMcpFixture`-Host. Die E2E-Matrix ist
  read-only und kann stattdessen einen lazy, thread-sicheren Assembly-Host gegen `SymbolGraphMini`
  teilen; auch Toolregistrierung braucht keinen zweiten Baseline-Prozess.
- Der bestehende `McpTestClient.ConnectAsync` haelt `SubprocessConcurrencyGate` nur bis zum
  Handshake. Der anschliessende Hintergrund-Solution-Load und die restliche Prozesslebensdauer
  laufen unbudgetiert. `CliProcessRunner` und der rohe Framing-Harness halten das Lease dagegen
  bereits bis zum Prozessende. Der Nachfolger muss fuer jeden MCP-Host die gesamte Lebensdauer
  besitzen und darf Permits erst nach Client-/Prozess-Disposal freigeben.
- Transport ist ausschliesslich stdio ueber umgeleitete stdin/stdout/stderr-Pipes; es existieren
  keine TCP-Ports, Listener oder benannten Pipes. Framing prueft rohe JSON-RPC-Zeilen mit 90-s-
  Sicherheitsbudget und 10-s-Shutdown-/stderr-Budgets. Normale Clients verwenden 30 s, die beiden
  Fehlerpfade 60 s; Retry ist 3x mit 500/1.000/2.000 ms, Fixture-Hosts heute 5x mit
  1/2/4/8/16 s. Diese Budgets werden zentral benannt, nicht pauschal vergroessert.
- Read-only/idempotent sind Tool-Liste, Symbol-/Referenz-/Impact-Symbolbranch, Miss-Hint und die
  24 All-Tools-Binding-/Fehlervertraege. Exklusiv bleiben: drei rohe Framing-Vertraege, zwei
  Load-/Compile-Fehlervertraege, drei Git-Impact-Vertraege, ein Refresh/Staleness-Vertrag, der
  Ambiguity-Startabbruch und der reale Retry-Fehlerpfad. Loading-State bleibt mit drei direkten,
  deterministischen Server-Lifecycle-Vertraegen breit in FastTests; der echte Transportpfad wird
  repraesentativ durch Fehler-/Retry-Smokes gedeckt.
- `AiNetLinter.IntegrationTests` besitzt bereits `TestTempDirectory`, `RecordingLintConsole`,
  `SolutionRootLocator`, `IsolatedFixtureLease`-basierte `FixtureWorkspace`-Typen und den echten
  `McpHandshakeToolRegistrationTests`-Smoke. Diese Strukturen werden erweitert. Es entsteht keine
  neue Produkt-Seam und keine zweite allgemeine Root-/Temp-Implementierung.
- `xunit.runner.json` laesst Integration-Collections heute unbegrenzt parallel laufen
  (`maxParallelThreads: 0`). Die Assembly startet gleichzeitig zwei MSBuild-Assembly-Fixtures.
  Der MCP-Prozesshaushalt wird deshalb separat auf zwei vollstaendige Prozesslebensdauern begrenzt
  und die Integration-Collection-Parallelitaet auf vier Threads gedeckelt; Assembly-Parallelitaet
  bleibt aus und Collections bleiben grundsaetzlich parallel.
- TD-001 trifft den Framing-Schnitt direkt und wird durch exklusiven Raw-stdio-Host,
  vollstaendiges Lifetime-Lease und isolierten Volllast-Nachweis geschlossen. TD-006 trifft die
  ohnehin geaenderten Kategorieguards und wird ueber einen xUnit-freien Trait-Inspector im TestKit
  konsolidiert. TD-008 bleibt offen, weil die betroffenen allgemeinen/Compile-Error-Helper nicht
  durch diese Prozessfamilie assembly-uebergreifend stabilisiert werden. TD-010 bleibt offen:
  `FixtureWorkspaceBase` wird weiterhin von den bewusst ausgeschlossenen CLI- und Stressklassen
  gebraucht; die migrierten MCP-Konsumenten nutzen bereits den `IsolatedFixtureLease`-Adapter,
  ohne den Legacy-Rest vorzeitig umzubauen.

## Intention

Nach diesem Step liegt die komplette Mini-Solution-MCP-Familie auf der neuen Testpyramide: reine
Policy-/Loading-/Toolvertraege sind FastTests, echte stdio-/Datei-/Git-Grenzen IntegrationTests.
Ein lazy read-only SymbolGraph-Host darf idempotente Smokes parallel bedienen; jeder mutierende,
framing-, retry- oder fehlerorientierte Vertrag besitzt einen exklusiven Host. Das gemeinsame
Prozessbudget umfasst Start, Hintergrundload, Calls und Disposal statt nur den Handshake.

Dogfood, allgemeine CLI-Self-Repo-Workflows, Performance und Stress werden nicht in diese
Hostfamilie gemischt. Insbesondere wird kein Stressprofil ausgefuehrt.

## Konkrete Änderungen

### item-01: MCP-Policy-, Options-, Loading- und Symbolvertraege auf FastTests schneiden

- Folgende reinen Klassen/Vertragsteile nach `src/AiNetLinter.FastTests/Mcp/` bzw.
  `Mcp/Tools/SymbolGraph/` verschieben und einheitlich als `Unit` oder `Component` auf Klassenebene
  kategorisieren:
  - `McpServerCommandCacheBypassTests`;
  - `McpCodeGraphServerConstructorTests`;
  - `McpServerOptionsBuilderTests` und `McpServerOptionsFactoryTests`;
  - `OverviewResourceRegistrationTests` und `SymbolGraphToolRegistrationsTests`;
  - die zwei reinen `McpTestClientRetryOptions`-Vertraege;
  - die drei `McpServerCommandLoadingStateTests`;
  - die Solution-/Symbolbranches aus `GetImpactToolTests`, soweit sie weder Git noch echten
    MSBuild-/CompileError-Load benoetigen.
- Loading-State gegen einen aus `RoslynTestSolutionFactory`/vorhandenem Snapshot erzeugten
  `SourceFileCatalog(Solution, ...)` ausfuehren. Der interne Konstruktor existiert bereits seit
  Step 024; keine neue Produkt-API, kein `SourceFileCatalog.LoadAsync` und kein Prozess in Fast.
- `GetImpactToolTests` fachlich teilen: Symbolauflösung, StructuredContent, Inputfehler,
  Hard-Cap/Depth und No-Git-Repository bleiben Component; Git-Diff- und CompileError-Faelle gehen
  nach Integration und verwenden isolierte Disk-Fixtures. Das Ledger darf die eine historische
  Klasse als `consolidated` auf den primaeren Fast-Abdeckungsort zeigen; der zweite Zielort und
  alle 14 Verträge werden in der Kohortenevidenz explizit aufgeführt.
- `McpServerCommandTests` nicht als gemischte Kategorieklasse kopieren. Reine Default-/Policy-
  Vertraege nur dann nach Fast schneiden, wenn sie ohne Dateisystem auskommen; Pfadauflösung,
  Configdateien, kaputte Solution, Git und E2E bleiben in getrennten Integration-Klassen.
- Produktseitiger Coverage-Audit: `McpServerCommand`, `McpServerOptionsFactory`/
  `McpServerOptionsBuilder`, `OverviewResourceRegistration`, `SymbolGraphToolRegistrations`,
  `McpCodeGraphServer`-Loading-State und `GetImpactTool` vollständig gegen Erfolgs-, Negativ-,
  Fehler-, Trunkierungs-, Cancellation- und StructuredContent-Branches lesen. Nur eine konkret
  belegte nicht-triviale Lücke ergänzen; keine Produkt-Seam vorsorglich bauen.

### item-02: Lazy read-only Mini-MCP-Host fuer idempotente E2E-Smokes

- In `src/AiNetLinter.IntegrationTests/Mcp/Platform/` einen Integration-lokalen, besitzenden
  `McpProcessHost` aufbauen. Er kapselt `StdioClientTransport`, `McpClient`, Loading-Retry,
  Tool-/Text-/List-Aufrufe, Cancellation-Budgets und das Prozessbudget-Lease. Disposal-Reihenfolge:
  Client/Transport vollständig beenden, dann isolierte Fixture entsorgen, zuletzt Gate-Permit
  freigeben. Kein Helper im TestKit, da FastTests ihn nie konsumieren duerfen.
- Einen assembly-weit registrierten, lazy `ReadOnlyMcpHostFixture` hinzufügen, der genau einen
  `SymbolGraphMini`-Host thread-sicher materialisiert. `McpHandshakeToolRegistrationTests`,
  `McpServerAllToolsE2ETests`, `McpServerCommandFindReferencesTests`,
  `McpServerCommandFindSymbolTests`, der Symbolbranch aus `McpServerCommandGetImpactTests`,
  `McpServerCommandMissHintTests` und die read-only E2E-Methoden aus
  `McpServerCommandTests` verwenden dieselbe Instanz ohne gemeinsame Collection.
- Die bisherigen zwei read-only Legacy-Hosts (`SymbolGraphMcpFixture` plus
  `BaselineMcpFixture`) auf einen Host reduzieren. Toolregistrierung ist fixtureunabhängig und
  wird am SymbolGraph-Host belegt. Die 24 All-Tools-Smokes und die kleineren Command-Smokes bleiben
  diagnostisch getrennte Testmethoden; nur echte semantische Duplikate duerfen konsolidiert werden.
- Host-Selbstvertraege belegen lazy Einmalmaterialisierung bei parallelem Zugriff, dieselbe
  Objektidentität, konkurrierende idempotente Toolcalls, vollständiges Disposal und Permit-
  Rückgabe nach Initialisierungsfehler. Keine `CollectionDefinition`, kein
  `DisableParallelization`, keine globale Testreihenfolge.

### item-03: Exklusive Hosts fuer Framing, Retry, Fehler, Git und Refresh

- Die echten Prozessvertraege nach `src/AiNetLinter.IntegrationTests/Mcp/` bzw. `Commands/`
  migrieren und jeweils einen frischen `IsolatedFixtureLease`-/Host-Owner verwenden:
  `McpServerCommandAmbiguityE2ETests`, `McpServerCommandErrorHandlingTests`,
  `McpServerCommandStalenessTests`, `McpServerCommandJsonRpcFramingTests`, der reale
  `McpTestClientRetryTests`-Fehlerpfad, die drei Git-Impact-E2E-/Toolvertraege und der
  CompileError-Toolvertrag.
- `FixtureWorkspaces.cs` um den bestehenden fachlichen GitImpact-Vertrag erweitern
  (isolierte Kopie, lokales Git-Repo, Mutation/Commit, Windows-ReadOnly-Cleanup). Rootsuche und
  Kopie delegieren weiter an `SolutionRootLocator`/`IsolatedFixtureLease`; keine Kopie von
  `FixtureWorkspaceBase.CopyFixture`.
- Framing erhaelt einen schmalen exklusiven Raw-stdio-Host mit writer/stdout/stderr-Ownership,
  bounded graceful shutdown und anschliessendem `Kill(entireProcessTree: true)` nur fuer den
  eigenen Prozess. Alle drei Tests behalten rohe JSON-RPC-Frames und einen frischen Cold Host.
  TD-001 wird über reproduzierbare Last zusammen mit anderen Mini-MCP-Prozessen untersucht; Fix
  ist Ownership/Budget, nicht pauschale Timeout-Erhoehung oder serielle Collection.
- Retry bleibt ein echter, exklusiver Startfehlervertrag; Optionswerte bleiben Fast. Die Anzahl
  der Startversuche muss über eine injizierte test-infrastrukturelle Connect-Attempt-Funktion oder
  gleichwertige Beobachtung deterministisch nachweisbar sein. Keine Produkt-Seam und kein Sleep als
  Synchronisationsannahme.
- Refresh/Staleness verwendet immer eine eigene SymbolGraph-Kopie und einen eigenen Host; der
  read-only Assembly-Host darf niemals mutiert werden. GitImpact verwendet je Test ein eigenes
  Repository und einen eigenen Host/Catalog. Error- und Ambiguity-Pfade teilen keinen Host mit
  Happy-Path-Smokes.
- Alle bisherigen Loading-/Connect-Retry-Schleifen auf eine test-infrastrukturelle Implementierung
  konsolidieren. ErrorHandling darf keinen lokalen 30x-Loading-Retry mehr duplizieren. Timeoutwerte
  bleiben benannte Sicherheitsbudgets und Cancellation/Fehler räumen Client, Tasks, Prozessbaum,
  Tempverzeichnis und Gate-Permit in `finally` vollständig auf.

### item-04: Prozesslebensdauer budgetieren und Integration-Runner deckeln

- Den Integration-lokalen Nachfolger von `SubprocessConcurrencyGate` als instanztestbaren Gate-
  Kern plus eine statische produktive Testinstanz mit Kapazität **2** implementieren. Ein Lease
  umfasst immer die vollständige Lebensdauer eines `AiNetLinter.exe --mcp-server`-Prozesses; kein
  Handshake-only-Pfad. Der lazy read-only Host belegt einen Slot, parallel kann höchstens ein
  exklusiver MCP-Prozess laden/laufen.
- Gate-Vertraege ohne echte Prozesse prüfen: Maximum zwei, wartender dritter Aufruf, Freigabe nach
  Erfolg, Startfehler, Cancellation und Disposal; alle gestarteten Delegates in `finally`
  freigeben und awaiten. Keine Reset-API, kein austauschbarer globaler Zustand.
- `src/AiNetLinter.IntegrationTests/xunit.runner.json` konkret auf
  `parallelizeAssembly: false`, `parallelizeTestCollections: true`, `maxParallelThreads: 4`
  kalibrieren. FastTests-Runner bleibt unverändert CPU-orientiert. `.runsettings` startet Profile
  nicht parallel und braucht in diesem Step keine globale Aenderung.
- Ein Runner-/Process-Guard liest das Ziel-JSON und die Integration-Quellen: exakt diese
  Runnerwerte; alle `StdioClientTransport`-/`Process.Start`-Callsites der migrierten MCP-Familie
  liegen nur in den besitzenden Hosttypen; kein `SymbolGraphMcp`-Collectionattribut; alle echten
  Prozessklassen liegen in IntegrationTests. Der vorhandene `LoadedFixture`-Max-2-Loadpfad bleibt
  unveraendert und wird nicht mit dem prozessuebergreifend ohnehin unwirksamen Semaphor vermischt.

### item-05: Ledger-, Kategorie-, Dependency- und Prozessleck-Grenzen nachziehen

- Alle 21 Ledgerzeilen auf `migrated`/`consolidated` mit existierenden Zielklassen und
  Fall-/Risiko-/Erfolgs-/Negativ-/Fehler-/Evidenznotiz fortschreiben; Legacy-Dateien physisch
  löschen. Guard muss exakt 53 statt 74 `pending` melden. Keine halb migrierte Quelldatei.
- TD-006 im ohnehin geänderten Guard-Scope schließen: einen xUnit-freien internen
  Kategorie-Trait-Inspector im TestKit bereitstellen und beide Assembly-Guards darauf umstellen.
  TestKit vergibt bereits IVT an beide Testassemblies; keine xUnit-Abhängigkeit hinzufügen.
- Kategorien fachlich setzen: Fast nur `Unit`/`Component`; Mini-Solution-/stdio-/Git-/Datei-
  Verträge `Integration`. Keine dieser 21 Klassen wird Dogfood/Performance/Stress. Die beiden
  ausgeschlossenen Stressklassen bleiben im Legacy-Ledger pending.
- Vor und nach jedem Prozessgate PID/ParentPID/Commandline für `dotnet test`, `testhost`,
  `AiNetLinter.exe --mcp-server` und BuildHosts erfassen. Nachher dürfen keine neuen, zum Lauf
  gehörenden Prozesse verbleiben. Nur eindeutig dem eigenen Lauf zugeordnete PIDs dürfen nach
  einem Diagnoseabbruch beendet werden; nie Kill nach Prozessname.
- Legacy-Guard zusätzlich darauf prüfen, dass keine der 21 migrierten Klassen mehr deklariert ist,
  während `McpTestClientParallelTests`, CLI-/Dogfood-/Performance-/Stress-Reste unverändert
  kompilierbar und pending bleiben. `StaticTestSentinel`-Delta als Coverage-Suchsignal auswerten.

## Vollstaendige EPIC-6-Inventur und bewusste Ausschluesse

- **Dieser Step — Mini-MCP-Hostfamilie (21 Klassen, 121 Faelle):**
  `McpServerCommandAmbiguityE2ETests`, `McpServerCommandCacheBypassTests`,
  `McpServerCommandCallLogTests`, `McpServerCommandErrorHandlingTests`,
  `McpServerCommandFindReferencesTests`, `McpServerCommandFindSymbolTests`,
  `McpServerCommandGetImpactTests`, `McpServerCommandLoadingStateTests`,
  `McpServerCommandMissHintTests`, `McpServerCommandStalenessTests`,
  `McpServerCommandTests`, `GetImpactToolTests`, `McpCallLogTests`,
  `McpCodeGraphServerConstructorTests`, `McpServerAllToolsE2ETests`,
  `McpServerCommandJsonRpcFramingTests`, `McpServerOptionsBuilderTests`,
  `McpServerOptionsFactoryTests`, `McpTestClientRetryTests`,
  `OverviewResourceRegistrationTests`, `SymbolGraphToolRegistrationsTests`.
- **Allgemeine CLI-/Console-Familie — spaeterer EPIC-6-Schnitt (10 Klassen, 49 Methoden/
  60 statische Faelle):** `CliCommandBuilderMcpLogTests`, `CliIntegrationTests`, `ProgramTests`,
  `AuditCommandTests`, `CliBatchRegressionTests`, `DocsCommandTests`, `ListRulesCommandTests`,
  `PlaybookCheckCommandTests`, `SyncAgentRulesCommandTests`, `PerformanceProfilerTests`.
  `CliIntegrationTests` mischt echte Self-Repo-Dogfood-Pfade mit isolierten CLI-Vertraegen und
  wird nicht nur zur vorzeitigen Entfernung des Legacy-Prozesshelpers in diesen Step gezogen.
- **Dogfood — eigener Host/Cadence (2 Klassen, 20 Faelle):**
  `McpDocumentationSmokeTests`, `McpLiveRepositoryTests`; ein read-only Real-Repo-Host, Kategorie
  künftig `Dogfood`, Hauptarbeitsverzeichnis, strukturelle Invarianten statt nur `NotEmpty`.
- **Performance — eigenes Messprofil (2 Klassen, 3 Faelle):** `LoadFixtureBuilderTests` und
  `LoadFixtureMeasurementsTests`; Buildervertrag guenstig, Messungen Kategorie `Performance`,
  Cold-Start nie am warmen Shared Host. `PerformanceProfilerTests` ist oben bei der CLI-Familie
  inventarisiert, bleibt fachlich ein normaler Konfigurations-/Dateivertrag.
- **Stress — ausdrücklich nicht ausführen (2 Klassen, 2 Faelle):**
  `McpTestClientParallelTests` mit 16 gleichzeitigen Servern und
  `SourceFileCatalogRegisterMSBuildTests.LoadAsync_TwentyParallelCallsAcrossFixtures_AllSucceed`.
  Die beiden übrigen RegisterMSBuild-Verträge werden beim späteren Schnitt getrennt als
  Unit/Integration erhalten. Kein `Category=Stress`-Lauf in Step 025; Migration und Nachweis dieser
  Lastverträge bleiben einem ausdrücklichen späteren Profil-Step bzw. Task-Ende vorbehalten.
- **Fixture-Refactoring-Rest (1 Klasse, 8 Faelle):** `TD016aRefactorTests` bleibt zusammen mit
  `FixtureWorkspaceBase` pending, solange CLI/Stress Legacy-Workspace-Typen konsumieren. TD-010
  bleibt deshalb offen; Step 025 erzeugt keine neue Kopierimplementierung.
- **Nicht EPIC-6 (36 Klassen):** 15 Core-, zwei FalsePositive-, drei Maps-, sieben Metrics- und
  neun Output-Klassen bleiben als EPIC-7-Restmigration pending.

## Tests

- [ ] **Legacy-Baseline vor Änderungen, kein Stress:** PID-/Parentketten-Snapshot; danach ein
  einziger projektbezogener Legacy-Lauf mit OR-Filter für die 21 Klassen dieses Steps,
  `--no-build`, `&Category!=Stress`, `--blame-hang --blame-hang-timeout 3m` und eigenem
  `LogFileName=step025-legacy-mini-mcp.trx`. Erwartung: 121 statische Fälle; tatsächliche
  Discovery-/Pass-Zahl und Prozesszahl im Result dokumentieren. Kein Legacy-Vollprofil.
- [ ] **Framing-Baseline separat:**
  `FullyQualifiedName~McpServerCommandJsonRpcFramingTests` im Legacy-Projekt mit eigener TRX und
  gleichzeitigem, begrenztem Mini-MCP-Hintergrundfilter reproduzieren. TD-001-Signatur,
  stdout/stderr, Timeout und PIDs dokumentieren; kein `Category!=Stress`-Volllauf.
- [ ] `dotnet build` nach der Migration; alle fünf Projekte inklusive Legacy bauen, 0 Warnungen,
  0 Fehler.
- [ ] **Fast-Zielgate:** nur die neuen MCP-Unit-/Component-Zielklassen plus
  `FastTestsDependencyGuardTests` im selben Testhost, eigene TRX. Danach enger Runtime-Guard-
  Abschluss: keine MSBuild-/Process-Assembly durch die Fast-Kohorte geladen.
- [ ] **Read-only-Hostgate:** Host-Selbsttests plus Handshake, AllTools, FindReferences,
  FindSymbol, Symbol-Impact und MissHint in einem Integration-Testhost. Tests müssen ohne
  `SymbolGraphMcp`-Collection parallel bleiben, exakt einen read-only Server materialisieren und
  normal disposen.
- [ ] **Exclusive-Hostgate:** Ambiguity, ErrorHandling, Framing, Retry, Git-Impact, CompileError
  und Staleness in einem Integration-Testhost mit eigenem TRX und bounded Hangdiagnose. Gate-
  Telemetrie belegt höchstens zwei gleichzeitig lebende MCP-Prozesse; read-only Fixture bleibt
  unverändert.
- [ ] **Framing-Lastnachweis nach Fix:** dieselben drei Framing-Verträge mehrfach zusammen mit
  den übrigen exklusiven Mini-MCP-Filtern, aber ohne Dogfood/Performance/Stress. Alle stdout-Zeilen
  sind JSON-RPC-Frames, keine Flake-/Hang-Signatur, kein pauschal erhöhtes Timeout.
- [ ] **Guards:** beide `TestCategoryProfileGuardTests`, Fast static/runtime dependency guards,
  neuer Runner-/MCP-Callsiteguard, `TestMigrationLedgerConsistencyTests` und
  `LegacyProjectBuildGateTests`; Ledger meldet 53 pending, alle 21 Legacy-Deklarationen fehlen,
  Zielorte existieren.
- [ ] **Prozessleck-Guard:** nach jedem finalen Integration-Filter keine neue zugehörige
  `testhost`-/MCP-/BuildHost-Prozesskette und keine Temp-Fixture zurückgelassen. Fremde
  Vorher-Prozesse unangetastet lassen.
- [ ] `git --no-pager diff --check`.
- [ ] **Ausdrücklich nicht ausführen:** kein voller Fast-/Integration-`Category!=Stress`-Lauf,
  kein Legacy-/Solution-Volltest, kein Dogfood-, Performance- oder Stressprofil und insbesondere
  weder `McpTestClientParallelTests` noch der 20-fache MSBuild-Paralleltest.

## Definition of Done

- [ ] Alle 21 historischen Klassen und 121 nicht-trivialen Verträge sind eindeutig in FastTests
  oder IntegrationTests abgedeckt; Legacy-Quellen sind gelöscht, Ledgerguard ist grün und
  `pending` sinkt von 74 auf 53.
- [ ] Ein einziger lazy, thread-sicherer, vollständig besitzender SymbolGraph-MCP-Host bedient die
  idempotenten read-only Smokes parallel; kein `SymbolGraphMcp`-Collectionvertrag und kein zweiter
  Baseline-MCP-Prozess bleiben im Zielbestand.
- [ ] Framing, Retry, Error, Git und Refresh verwenden exklusive Fixture-/Prozesshosts; Mutation
  erreicht nie den shared Host. Loading-State ist deterministisch direkt abgedeckt und der echte
  Transport-/Retrypfad bleibt repräsentativ erhalten.
- [ ] Das Prozessbudget besitzt maximal zwei komplette MCP-Prozesslebensdauern. Startfehler,
  Cancellation, Testfehler und Disposal geben Permit, Client, Prozessbaum und Tempverzeichnis
  frei; keine Handshake-only-Lease mehr im Zielharness.
- [ ] Integration-Runner ist auf vier Collection-Threads gedeckelt, Assembly-Parallelität bleibt
  aus, Collections bleiben parallel; keine globale Serialisierung oder Testreihenfolge wurde
  eingeführt.
- [ ] TD-001 und TD-006 sind mit Evidenz geschlossen. TD-008 und TD-010 bleiben mit ihrer
  Assembly-/Restkonsumenten-Begründung offen; kein Debt-Mini-Step und keine vorzeitige
  TestKit-Allzweckhilfe.
- [ ] Build, enges Fast-Zielgate, read-only und exclusive Integration-Gates, Kategorie-/Dependency-/
  Runner-/Ledger-/Legacy-/Prozessleckguards sowie `git diff --check` sind grün. Kein volles
  `Category!=Stress`, kein Dogfood/Performance/Stress.
- [ ] Ein kohärenter deutscher Conventional-Commit mit `[speedup-tests]`; kein Amend/Rebase/Push.
  `step-025/step-result.md` geschrieben, Planstatus `done (pending audit)`.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Projekt-Overrides` — Nullable,
  Methoden-/Parametergrenzen und korrekte Testprojekt-Overrides.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` — PowerShell,
  getrennte TRX-Dateien, PID-Diagnose und projektbezogene Filter.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — gezielte Semaphore-/Fixture-
  Isolation statt globaler Collection-Serialisierung; MCP-Nachweise nur in C#.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — Ursache statt
  Symptomfix, keine Assertionschwächung, keine dauerhaften Task-/Step-Kommentare.

## Bekannte Ausnahmen

- TD-008 bleibt offen: allgemeine TestHelper-/CompileError-Assertions werden nicht allein wegen
  dieser Hostmigration ins TestKit gehoben.
- TD-010 bleibt offen: Legacy-CLI und der ausgeschlossene Stressvertrag benötigen weiterhin die
  alte Workspace-Basis. Die Zieltests nutzen ausschließlich die bestehende
  `IsolatedFixtureLease`-Linie; vollständige Entfernung folgt erst mit den letzten Konsumenten.
- Dogfood, Performance und Stress sind bewusst eigene Laufverträge. Ihre Inventur ist Teil dieses
  Plans, ihre Migration/Ausführung nicht.

## Notes

- Die 21-Klassen-Grenze ist die größte vollständige Mini-MCP-Hostfamilie unter dem aktuellen
  Reviewbudget. Das Hinzunehmen von Self-Repo-CLI, Dogfood oder Lastprofilen würde drei
  unterschiedliche Isolation-/Cadence-Modelle in einen Step mischen.
- Shared bedeutet nur read-only, idempotent, reihenfolge- und cacheunabhängig. Ein Test, der Datei,
  Git-Repo, Config, Call-Log oder Serverzustand verändert, ist automatisch exklusiv.
- Keine neue produktive Seam ist vorgesehen. Falls der Coverage-Audit eine verlangt, muss der
  Coder den tatsächlichen fehlenden Vertrag und den engsten pending Legacy-Filter dokumentieren;
  keine vorsorgliche Test-only-API.
