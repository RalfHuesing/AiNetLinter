---
task: speedup-tests
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-12
---

<!-- step-008: EPIC-2 Teil 3 -- FilterMini-Fixture (Disk + In-Memory-Spec + Fidelity-Test) real im
     Bestand, EPIC-2 damit abgeschlossen. -->

<!-- step-013: EPIC-4 Teil 1 -- Skeleton-/Filterkohorte auf FilterMini migriert; Scanner und
     weitere MCP-Tools bleiben bewusst ausserhalb dieses Steps. -->

<!-- planning step-015: EPIC-4 Teil 2 -- als naechster geschlossener Scannerteil ist ausschliesslich
     DuplicateDetectionScanner samt zentraler virtueller Factory-Pfadkalibrierung vorgesehen. -->

<!-- step-007: EPIC-2 Teil 2 -- IsolatedFixtureLease (TestKit) und MsBuildFixtureHost (IntegrationTests)
     real im Bestand. -->

<!-- step-006: EPIC-2 Teil 1 -- RoslynTestSolutionFactory/PreparedSolutionFixture real im Bestand. -->


<!-- step-004: EPIC-1 abgeschlossen (Minimum Safety Envelope, Legacy-Build-Gate, InternalsVisibleTo,
     Gate-Switch). -->

<!-- Rows marked "step-001"/"step-002" existieren jetzt tatsaechlich im Bestand; Rows mit "planning"
     sind weiterhin offene Platzhalter/Beobachtungen aus der Konzeptphase. -->


# CodeMap: speedup-tests

Task-scoped Landkarte nach dem Pointer-Prinzip: Jeder Eintrag nennt nur Ort und Relevanz; Details
werden vor jedem Drift-Loop-Step im aktuellen Bestand nachgelesen.

## Projekt- und Laufvertraege

- **`AiNetLinter.slnx`** — enthaelt jetzt fuenf Projekte: Produkt, Legacy-Testprojekt und die drei neuen Zielprojekte (FastTests, IntegrationTests, TestKit). (zuletzt: step-001)
- **`src/AiNetLinter.Tests/AiNetLinter.Tests.csproj`** — zentraler Testprojektvertrag mit xUnit-v3-, Runsettings- und Produktreferenz; unveraendert (Legacy, Strangler-Quelle). (zuletzt: planning)
- **`src/AiNetLinter.FastTests/`** — schnelle Assembly (SDK-Testprojekt), importiert `tests/AiNetLinter.TestProject.props` und referenziert `AiNetLinter` + `AiNetLinter.TestKit`. (zuletzt: step-010)
- **`src/AiNetLinter.FastTests/Core/Checkers/`** — Zielort der 28 aus dem Legacy-Projekt migrierten Checker-Testklassen. (zuletzt: step-010)
- **`src/AiNetLinter.FastTests/TestHelper.cs`** — FastTests-lokale Teilmenge der Syntax-/Compilation-Helper fuer die migrierte Checker-Kohorte. (zuletzt: step-010)
- **`src/AiNetLinter.FastTests/Web/`** — Zielort der fuenf migrierten Unit-Testklassen fuer CSS-, JS- und Razor-Analyse sowie Web-Suppression. (zuletzt: step-011)
- **~~`src/AiNetLinter.Tests/Web/`~~** — als Parser-Quelle seit step-011 obsolet; die fuenf Testklassen liegen unter `src/AiNetLinter.FastTests/Web/`. (zuletzt: step-011)
- **`src/AiNetLinter.FastTests/Mcp/Tools/*RendererTests.cs`** — Zielort der zwei migrierten Renderer-Testklassen. (zuletzt: step-012)
- **~~`src/AiNetLinter.Tests/Mcp/Tools/CallTreeMermaidRendererTests.cs` / `MetricsTreeRendererTests.cs`~~** — als Renderer-Quelle seit step-012 obsolet; die zwei Testklassen liegen unter `src/AiNetLinter.FastTests/Mcp/Tools/`. (zuletzt: step-012)
- **`src/AiNetLinter/Mcp/Tools/CallTree/CallTreeMermaidRenderer.cs` / `MetricsTree/MetricsTreeRenderer.cs`** — interne, rein formatierende Renderer ueber vorbereiteten `MetricsTreeNode`-Baeumen; der rekursive Top-N-pro-Ebene-Vertrag ist fuer den Coverage-Audit der Renderer-Kohorte relevant. (zuletzt: planning step-012)
- **`src/AiNetLinter.IntegrationTests/`** — neue Infrastruktur-Assembly, eigenes `xunit.runner.json` (`parallelizeAssembly: false`), referenziert `AiNetLinter` + `AiNetLinter.TestKit`; enthaelt bisher eine Proof-Testklasse (`Configuration/ProjectOverrideRealSolutionTests.cs`), sonst noch leer. (zuletzt: step-001)
- **`src/AiNetLinter.TestKit/`** — SDK-Class-Library fuer die gemeinsame Testplattform, importiert `tests/AiNetLinter.TestProject.props`, referenziert nur `AiNetLinter`, keine xUnit-Abhaengigkeit; enthaelt die deterministische `RoslynTestSolutionFactory`, `PreparedSolutionFixture`, `IsolatedFixtureLease` und `RecordingLintConsole` als gemeinsamen `ILintConsole`-Sink der Zieltestassemblies. (zuletzt: step-013)
- **`src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHost.cs`** — `IAsyncLifetime`-Assembly-Fixture: kopiert `BaselineMini` einmal ueber `IsolatedFixtureLease` und laedt sie einmal echt via `SourceFileCatalog.LoadAsync` (Properties `Catalog`/`Solution`); bewusst in `AiNetLinter.IntegrationTests` statt `TestKit`, weil `FastTestsDependencyGuardTests` MSBuild-Referenzen in `TestKit.dll` als Verletzung meldet. Registrierung ueber `MsBuildFixtureHostAssemblyFixture.cs` (`[assembly: AssemblyFixture(typeof(MsBuildFixtureHost))]`, analog zu `PreparedSolutionAssemblyFixture.cs`). (zuletzt: step-007)
- **`src/AiNetLinter.TestKit/FilterMiniSolutionSpec.cs`** — deklarative In-Memory-Spiegelung der Disk-Fixture `FilterMini`: `CreateProjectSpecs()` liefert das `ProjectSpec[]`-Paar (`FilterMini` mit `Core/Widget.cs`/`Utils/Formatter.cs`, `FilterMini.Tests` mit `Core/WidgetTests.cs` plus `ProjectReferences: ["FilterMini"]`), Quelltext textuell identisch zu den physischen Dateien unter `tests/Fixtures/FilterMini/`. (zuletzt: step-008)
- **`src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs` / `src/AiNetLinter.FastTests/Platform/RoslynTestSolutionFactoryTests.cs`** — In-Memory-Builder und Plattformverträge stellen neben gecachten BCL-Referenzen optionale normalisierte virtuelle Solution- und Dokumentpfade bereit. (zuletzt: step-015)
- **`src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionScannerTests.cs` / `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionScanner.cs`** — sieben Component-Verträge für den Scanner verwenden die Factory mit virtuellen Pfaden und die kalibrierten Clone-Methoden des FastTests-Helpers. (zuletzt: step-015)
- **~~`src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionScannerTests.cs`~~** — als Legacy-Quelle seit step-015 obsolet; die Scannerverträge liegen unter `src/AiNetLinter.FastTests/Mcp/Tools/`. (zuletzt: step-015)
- **`src/AiNetLinter.Tests/Output/TestLintConsole.cs`** — von zahlreichen weiterhin pending Legacy-Klassen konsumiertes `ILintConsole`-Testdouble; die Datei kann bei der Filtermigration nicht physisch mitverschoben werden, während ein gemeinsames Zielassembly-Double erst durch reale Fast-/Integration-Konsumenten gerechtfertigt ist. (zuletzt: planning step-013)
- **`src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs`** — Fidelity-/Formvergleichstest zwischen Disk- und In-Memory-`FilterMini`, einschließlich positiver und negativer Testprojekt-Erkennung. (zuletzt: step-013)
- **`tasks/speedup-tests/test-migration-ledger.md`** — Inventar aller 183 Legacy-Testklassen mit Status, Legacy-Filter und neuem Abdeckungsort; 28 Checker-, fuenf Web-Parser- und zwei Renderer-Zeilen stehen auf `migrated`. (zuletzt: step-012)
- **`src/AiNetLinter.IntegrationTests/Migration/TestMigrationLedgerConsistencyTests.cs`** — Ledger-Konsistenzguard (Category=Integration): scannt die Legacy-Testklassen in `src/AiNetLinter.Tests` per Roslyn-Syntaxbaum und prueft alle vier Konsistenzregeln aus dem Ledger-Kopf gegen den tatsaechlichen Bestand. (zuletzt: step-002)
- **`src/AiNetLinter.FastTests/Architecture/FastTestsDependencyGuardTests.cs`** — statischer Deny-Listen-Guard (Category=Unit) ueber die kompilierten Metadaten (AssemblyRef/TypeRef/MemberRef via System.Reflection.Metadata) von `AiNetLinter.FastTests.dll`/`AiNetLinter.TestKit.dll` gegen MSBuild-/Workspace-/Process-/`SourceFileCatalog.LoadAsync`-Referenzen. (zuletzt: step-002)
- **`src/AiNetLinter.FastTests/Architecture/FastTestsRuntimeDependencyGuardFixture.cs`** — Laufzeit-Gegenstueck des Deny-Listen-Guards: `ICollectionFixture`, deren Dispose `AppDomain.CurrentDomain.GetAssemblies()` gegen dieselbe Deny-Liste prueft; Best-Effort-Nachweis (keine Prozessisolationsgarantie, siehe XML-Doc der Klasse). (zuletzt: step-002)
- **`src/AiNetLinter.FastTests/Architecture/TestCategoryProfileGuardTests.cs`** — Kategorien-/Profilguard fuer `AiNetLinter.FastTests`: jede Testklasse mit `[Fact]`/`[Theory]` braucht genau einen Trait aus {Unit, Component}. (zuletzt: step-002)
- **`src/AiNetLinter.IntegrationTests/Architecture/TestCategoryProfileGuardTests.cs`** — gleiches Prinzip fuer `AiNetLinter.IntegrationTests`, erlaubte Kategorien {Integration, Dogfood, Performance, Stress}. (zuletzt: step-002)
- **`src/AiNetLinter.FastTests/Core/LinterEngineSolutionAnalysisTests.cs`** — MSE-Baustein "vorbereitete Solution analysieren": Component-Test, ruft `LinterEngine.RunAsync(Solution)` direkt gegen eine per `RoslynTestSolutionFactory.CreateSolution` aufgebaute Zwei-Klassen-Solution auf (kein MSBuild), prueft Verletzungs- und regelkonformen Pfad; erster echter Konsument der Testplattform-Factory. (zuletzt: step-006)
- **`src/AiNetLinter.FastTests/Platform/PreparedSolutionAssemblyFixture.cs`** — assembly-weite xUnit-v3-`[assembly: AssemblyFixture(typeof(PreparedSolutionFixture))]`-Registrierung fuer `AiNetLinter.FastTests` (kein Testcode, nur Registrierung); erste echte Assembly-Fixture im Bestand statt zwangsserialisierender `ICollectionFixture`. (zuletzt: step-006)
- **`src/AiNetLinter.FastTests/Platform/RoslynTestSolutionFactoryTests.cs`** — Vertragstests (Category=Component) fuer `RoslynTestSolutionFactory`: Mehrprojekt-Referenzaufloesung, Nullable-Context-Diagnosen, Preprocessor-Symbole, Referenz-Caching (Objektidentitaet), Fehlerpfad bei unbekanntem Projektnamen. (zuletzt: step-006)
- **`src/AiNetLinter.FastTests/Platform/PreparedSolutionFixtureTests.cs`** — Vertragstests (Category=Component) fuer `PreparedSolutionFixture`: lazy Materialisierung, Isolation zwischen Szenarien, Thread-Sicherheit bei parallelen `GetOrCreate`-Aufrufen; bezieht die Fixture ueber die Assembly-Fixture-Registrierung per Konstruktor-Injektion. (zuletzt: step-006)
- **`src/AiNetLinter.IntegrationTests/Cli/CliAdapterExitCodeTests.cs`** — MSE-Baustein "CLI-Adapter mit Exit-Code": ruft `Program.Main(string[])` in-process gegen zwei isolierte Kopien der Fixture `tests/Fixtures/BaselineMini` auf (eigene minimale, vollstaendig kontrollierte `rules.json`, sealed/unsealed-Kontrast statt der Original-Fixture-Regeln), prueft Exit-Code 0 vs. ungleich 0. (zuletzt: step-004)
- **`src/AiNetLinter.IntegrationTests/Mcp/McpHandshakeToolRegistrationTests.cs`** — MSE-Baustein "MCP-Handshake/Toolregistrierung": startet `AiNetLinter.exe --mcp-server` als echten Subprozess gegen `tests/Fixtures/BaselineMini`, eigener schlanker `McpClient`-Handshake (kein Kopieren von `AiNetLinter.Tests.Mcp.McpTestClient`, keine TestKit-Extraktion), prueft `tools/list`. (zuletzt: step-004)
- **`src/AiNetLinter.IntegrationTests/Migration/LegacyProjectBuildGateTests.cs`** — Legacy-Build-Gate (konzept.md Leitplanke 8): prueft mechanisch ueber `AiNetLinter.slnx`, dass `AiNetLinter.Tests` Solution-Mitglied und seine `.csproj` auf der Platte vorhanden bleibt, solange `test-migration-ledger.md` noch `pending`-Zeilen hat. (zuletzt: step-004)
- **`tasks/speedup-tests/baseline-measurement.md`** — Vorher-Baseline (Median ueber 3 Laeufe) fuer `Category=Unit` und `Category!=Stress` plus einmalig gestoppte Build-Zeit; dokumentiert auch eine bereits vor step-002 bestehende Flakiness in `McpServerCommandJsonRpcFramingTests` unter Volllast. (zuletzt: step-002)
- **`src/AiNetLinter.Tests/xunit.runner.json`** — steuert Collection-Parallelitaet, Threadzahl und Long-Running-Diagnostik. (zuletzt: planning)
- **`.runsettings`** — definiert Ergebnisablage und TRX-Logging fuer Laufzeitvergleiche; wird jetzt auch von FastTests/IntegrationTests referenziert, unveraendert im Inhalt. (zuletzt: planning)
- **`AGENTS.md`** — normales Gate ist jetzt auf `dotnet test src/AiNetLinter.FastTests`/`src/AiNetLinter.IntegrationTests --filter Category!=Stress` umgeschaltet; `AiNetLinter.Tests` (Legacy) ausdruecklich als quarantaeniert dokumentiert (baubar, gezielt ausfuehrbar ueber Ledger-Filter, nicht im Standardgate). (zuletzt: step-004)
- **`.agents/rules/AiNetLinterRichtlinien.mdc`** — enthaelt die projektspezifischen Test-, Parallelitaets-, MCP- und Commitregeln; die TRX-Diagnoseregel (`TestResults/latest.trx`) verweist jetzt auf `AGENTS.md` als alleinige Quelle der aktuell gueltigen Gate-Kommandos statt sie zu duplizieren; die MCP-&-Dogfood-Testing-Regel (§4) nennt jetzt `McpHandshakeToolRegistrationTests` (`AiNetLinter.IntegrationTests`) als aktuellen Weg statt ausschliesslich `McpLiveRepositoryTests`/`McpTestClient` (`AiNetLinter.Tests`) zu behaupten. (zuletzt: step-005)
- **`tests/AiNetLinter.TestProject.props`** — existiert jetzt: explizit importierte gemeinsame Props (`TargetFramework net10.0`, `Nullable`, `TreatWarningsAsErrors`, `IsPackable false`) plus `Microsoft.Build.Framework`/`Microsoft.NET.StringTools`-Pinning (18.8.2) fuer alle drei neuen Projekte, ohne festes `RunSettingsFilePath`. (zuletzt: step-001)
- **`src/Directory.Build.props`** — pinnt bereits heute `Microsoft.Build.Framework` mit `PrivateAssets`/`ExcludeAssets` fuer alle Projekte unter `src/`; die neue `TestProject.props` ergaenzt/spiegelt dieses Pinning fuer die drei neuen Projekte, kollidiert nicht damit (verifiziert: `dotnet build` gruen). (zuletzt: step-001)

## Produktive Konfigurationsvertraege mit Bezug zur Projektstruktur

- **`rules.json`** — `ProjectOverrides` matcht jetzt `"*Tests"` (statt `"*.Tests"`, deckt `AiNetLinter.Tests`/`FastTests`/`IntegrationTests` ab) plus separatem Schluessel `"AiNetLinter.TestKit"`; `TestSentinel.TestProjectNameSuffixes` um `"TestKit"` erweitert. `InternalsVisibleTo` fuer die neuen Assemblies bewusst noch nicht ergaenzt (kein Bedarf, siehe step-001 JIT-Kontext). (zuletzt: step-001)
- **`src/AiNetLinter/Configuration/ProjectConfigResolver.cs`** — uebersetzt Override-Schluessel in Regex und entscheidet, welche Regeln fuer ein Projekt gelten. (zuletzt: planning)
- **`src/AiNetLinter/Core/TestProjectDetector.cs`** — erkennt Testprojekte ueber Metadatenreferenzen und Namenssuffixe; relevant fuer die Einordnung von TestKit und den neuen Assemblies. (zuletzt: planning)
- **`src/AiNetLinter/Core/PostAnalysisChecks.cs`** — enthaelt den `StaticTestSentinel`, dessen Abdeckungsindex von den Testprojekten der geladenen Solution abhaengt. (zuletzt: planning)
- **`src/AiNetLinter/Core/TestCoverageCollector.cs` / `TestCoverageIndex.cs` / `TestCoverageResolver.cs`** — sammeln und aufloesen die Abdeckungssignale (Testklassenname, `typeof`/`nameof`, `@covers`) und sind als mechanisches Suchsignal fuer den Coverage-Audit relevant. (zuletzt: planning)
- **`src/AiNetLinter/Core/LinterEngine.cs`** — traegt jetzt drei `InternalsVisibleTo`-Eintraege (`AiNetLinter.Tests`, `AiNetLinter.FastTests`, `AiNetLinter.IntegrationTests`); je weiterer Test-Assembly mit `internal`-Seam-Zugriff ist ein zusaetzlicher Eintrag noetig. (zuletzt: step-004)

## Produktive Lade- und Ausfuehrungsgrenzen

- **`src/AiNetLinter/Web/CssAnalyzer.cs`, `JsAnalyzer.cs`, `RazorAnalyzer*.cs`, `WebSuppressionDetector.cs`** — interne, direkt aufrufbare Parser-/Textanalyse-Vertraege der fuenf Legacy-Web-Testklassen; benoetigen weder MSBuild noch Testplattform-Fixtures. (zuletzt: planning step-011)
- **`src/AiNetLinter/Baseline/SourceFileCatalog.cs`** — besitzt den zentralen MSBuild-Solution-Load sowie bereits einen internen Konstruktor fuer vorhandene `Solution`-Snapshots. (zuletzt: planning)
- **`src/AiNetLinter/Core/LinterEngine.cs`** — lokales Referenzmuster fuer getrennte Pfad-, Catalog- und Solution-Einstiege. (zuletzt: planning)
- **`src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs`** — Pfadadapter lädt einmal per `SourceFileCatalog` und delegiert an den internen `Solution`-Kern mit `SkeletonMapBuildRequest`. (zuletzt: step-013)
- **`src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs`** — Component-Zielort der 18 Skeleton-Filterverträge gegen den assembly-weit vorbereiteten `FilterMini`-Snapshot. (zuletzt: step-013)
- **`src/AiNetLinter.IntegrationTests/Maps/Skeleton/SkeletonMapBuilderAdapterTests.cs`** — Integration-Zielort der zwei Pfad-/MSBuild-Adapterverträge gegen eine isolierte `FilterMini`-Kopie. (zuletzt: step-013)
- **~~`src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs` / `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonMapBuilderTests.cs`~~** — Legacy-Quellen seit step-013 obsolet; die Abdeckung liegt in den Fast-/Integration-Zieltests. (zuletzt: step-013)
- **`src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs`** — uebergibt Catalog oder Hintergrund-Loader an den residenten MCP-Server. (zuletzt: planning)
- **`src/AiNetLinter/Mcp/McpCodeGraphServer*.cs`** — verwaltet residente Solution, Refresh und Datei-Staleness und trennt read-only von mutierenden Testszenarien. (zuletzt: planning)
- **`src/AiNetLinter/Mcp/Tools/`** — enthaelt Scanner- und Tool-Einstiege, die ueberwiegend gegen eine vorhandene Solution testbar sind. (zuletzt: planning)

## Bestehende Testplattform

- **`src/AiNetLinter.Tests/TestHelper.cs`** — sammelt aktuelle Syntax-/Compilation-/Solution- und allgemeine Testhelper und ist Ausgangspunkt fuer die Infrastrukturentflechtung. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/FixtureWorkspaceBase.cs`** — kopiert kanonische Platten-Fixtures in isolierte Temp-Verzeichnisse. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/*MiniFixtureWorkspace.cs`** — typisierte Zugriffe auf die vorhandenen kleinen Solution-Fixtures. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/BaselineCatalogFixture.cs`** — laedt `BaselineMini` bereits einmal fuer eine xUnit-Lebensdauer. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/SymbolGraphCatalogFixture.cs`** — laedt `SymbolGraphMini` bereits einmal fuer read-only Tooltests; entsorgt beim Dispose nur den Fixture-Workspace, nicht den besitzenden Katalog. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs`** — trotz des Namens ein reiner `LinterAnalyzer`-Regeltest, kein Architekturguard; Namensverwechslung beim Aufbau der neuen Guards vermeiden. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/SymbolGraphCatalogCollection.cs`** — teilt die Catalog-Fixture heute ueber eine Collection und serialisiert dadurch zahlreiche Tooltestklassen. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpCollection.cs`** — teilt einen MCP-Prozess ueber eine bewusst serielle Collection und ist fuer die kuenftige Zustands-/Exklusivitaetspruefung relevant. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/McpMiniFixtureBase.cs`** — im parallelen DRY-Refactoring entstehende gemeinsame Basis fuer Mini-Fixture-MCP-Clients. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs`** — haelt einen echten Repository-MCP-Prozess collection-weit am Leben. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs`** — begrenzt Subprozessstarts und ist fuer kuenftige Start-/Load-/Lifetime-Budgets relevant. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Mcp/McpTestClient.cs`** — kapselt Prozessstart, MCP-Handshake, Loading-Retry, Toolaufrufe und Disposal. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/LoadFixtureBuilder.cs`** — erzeugt synthetische Platten-Solutions fuer definierte Lastprofile. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Fixtures/LoadFixtureMeasurementsTests.cs`** — enthaelt derzeit Performance-Messungen unter der Integration-Kategorie. (zuletzt: planning)

## Kanonische Mini-Solutions

- **`tests/Fixtures/BaselineMini/`** — kleine Baseline-/Lint-Solution. (zuletzt: planning)
- **`tests/Fixtures/BlazorPartialMini/`** — Razor-SDK-/Partial-Class-Solution fuer echte MSBuild-Evaluierung. (zuletzt: planning)
- **`tests/Fixtures/CompileErrorMini/`** — Solution mit mehreren beabsichtigten Compilerfehlern. (zuletzt: planning)
- **`tests/Fixtures/SingleCompileErrorMini/`** — kleine gemischte gueltig/ungueltig-Solution. (zuletzt: planning)
- **`tests/Fixtures/GitImpactMini/`** — kleiner Symbol- und Git-Impact-Bestand. (zuletzt: planning)
- **`tests/Fixtures/DiRegistrationMini/`** — kleine DI-Registrierungsstruktur. (zuletzt: planning)
- **`tests/Fixtures/SymbolGraphMini/`** — gemeinsamer Symbol-, Hierarchie-, Call- und Violation-Bestand. (zuletzt: planning)
- **`tests/Fixtures/FilterMini/`** — real im Bestand: kalibrierte Mehrprojekt-Fixture (Produktions- + Testprojekt mit Projektreferenz, drei Namespaces `FilterMini.Core`/`FilterMini.Utils`/`FilterMini.Tests.Core`, public/private- und public/internal-Mix) fuer Projekt-, Namespace-, Test- und Sichtbarkeitsfilter; In-Memory-Spiegel siehe `FilterMiniSolutionSpec.cs`. (zuletzt: step-008)

## Laufzeit-Hotspots und Migrationskandidaten

- **~~`src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs`~~** — Legacy-Filtermatrix seit step-013 obsolet; ersetzt durch `SkeletonMapFilterTests` gegen `FilterMini`. (zuletzt: step-013)
- **`src/AiNetLinter.Tests/Baseline/SourceFileCatalogRegisterMSBuildTests.cs`** — prueft Registrierung und parallele Loads und benoetigt eine klare Stress-/Integrationsgrenze. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs`** — prueft den echten Razor/MSBuild-Vertrag und sollte einen einmal geladenen Host nutzen. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Mcp/Tools/`** — enthaelt zahlreiche direkte Mini-Solution-Loads in als Unit markierten Tooltests. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Mcp/McpCodeGraphServer*Tests.cs`** — mischt read-only Serverlogik mit Datei- und Refresh-Mutationen und braucht getrennte Fixture-Lebensdauern. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Mcp/McpServerCommandJsonRpcFramingTests.cs`** — teurer echter Prozessvertrag fuer Protokollreinheit und Framing. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs`** — Dogfood-Matrix gegen das echte Repository ueber eine geteilte Serverfixture; assertiert heute ueberwiegend `NotEmpty`/`Contains` und ist damit als eigenstaendiges Pflichtprofil zu schwach. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Baseline/BaselineCliTests.cs`** — teure Baseline-End-to-End-Szenarien mit Datei- und Analysezustand. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Cli/CliIntegrationTests.cs`** — enthaelt mehrere echte Self-Solution-CLI-Workflows, deren Adapter- und Fachvertraege getrennt werden muessen. (zuletzt: planning)
- **`src/AiNetLinter.Tests/Commands/McpServerCommand*Tests.cs`** — enthaelt direkte Command-, Loading-, Fehler- und echte MCP-Prozessvertraege mit unterschiedlichen Ziel-Lebensdauern. (zuletzt: planning)

## Messdaten

- **`TestResults/final-run.trx`** — vorhandener 1.471-Test-Snapshot mit 228,38 Sekunden Wall Clock fuer die Ausgangsdiagnose. (zuletzt: planning)
- **`TestResults/fulltest.trx`** — aelterer 1.349-Test-Snapshot mit 158,18 Sekunden Wall Clock als Hinweis auf Laufzeitdrift. (zuletzt: planning)
