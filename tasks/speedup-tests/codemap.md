---
task: speedup-tests
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-11
---

# CodeMap: speedup-tests

Task-scoped Landkarte nach dem Pointer-Prinzip: Jeder Eintrag nennt nur Ort und Relevanz; Details
werden vor jedem Drift-Loop-Step im aktuellen Bestand nachgelesen.

## Projekt- und Laufvertraege

- **`AiNetLinter.slnx`** — enthaelt derzeit Produkt- und einziges Testprojekt und ist fuer eine moegliche physische Testtrennung relevant. (zuletzt: planning)
- **`src/AiNetLinter.Tests/AiNetLinter.Tests.csproj`** — zentraler Testprojektvertrag mit xUnit-v3-, Runsettings- und Produktreferenz. (zuletzt: planning)
- **`src/AiNetLinter.FastTests/`** — vorgesehene neue schnelle Assembly fuer Unit- und In-Memory-Component-Tests. (zuletzt: planning)
- **`src/AiNetLinter.IntegrationTests/`** — vorgesehene neue Infrastruktur-Assembly fuer Integration, Dogfood, Performance und Stress. (zuletzt: planning)
- **`src/AiNetLinter.TestKit/`** — vorgesehene gemeinsame Testbibliothek fuer deklarative Builder und guenstige Hilfen ohne teure Hosts. (zuletzt: planning)
- **`tasks/speedup-tests/test-migration-ledger.md`** — vorgesehener lueckenloser Status-, Abdeckungs- und Loeschindex fuer die Strangler-Migration des Legacy-Testprojekts. (zuletzt: planning)
- **`src/AiNetLinter.Tests/xunit.runner.json`** — steuert Collection-Parallelitaet, Threadzahl und Long-Running-Diagnostik. (zuletzt: planning)
- **`.runsettings`** — definiert Ergebnisablage und TRX-Logging fuer Laufzeitvergleiche. (zuletzt: planning)
- **`AGENTS.md`** — enthaelt die heute verbindlichen Unit-/Integration-/Stress-Filter und Abschlussgates. (zuletzt: planning)
- **`.agents/rules/AiNetLinterRichtlinien.mdc`** — enthaelt die projektspezifischen Test-, Parallelitaets-, MCP- und Commitregeln, u. a. die TRX-Diagnoseregel auf `TestResults/latest.trx`. (zuletzt: planning)
- **`Directory.Build.props`** — existiert noch nicht; vorgesehener gemeinsamer Ort fuer Testprojekt-Eigenschaften und MSBuild-Paketpinning. (zuletzt: planning)

## Produktive Konfigurationsvertraege mit Bezug zur Projektstruktur

- **`rules.json`** — enthaelt `ProjectOverrides` (`*.Tests`), `TestSentinel` und `EnableTestSentinel`; die neuen Projektnamen greifen ohne Anpassung nicht. (zuletzt: planning)
- **`src/AiNetLinter/Configuration/ProjectConfigResolver.cs`** — uebersetzt Override-Schluessel in Regex und entscheidet, welche Regeln fuer ein Projekt gelten. (zuletzt: planning)
- **`src/AiNetLinter/Core/TestProjectDetector.cs`** — erkennt Testprojekte ueber Metadatenreferenzen und Namenssuffixe; relevant fuer die Einordnung von TestKit und den neuen Assemblies. (zuletzt: planning)
- **`src/AiNetLinter/Core/PostAnalysisChecks.cs`** — enthaelt den `StaticTestSentinel`, dessen Abdeckungsindex von den Testprojekten der geladenen Solution abhaengt. (zuletzt: planning)
- **`src/AiNetLinter/Core/TestCoverageCollector.cs` / `TestCoverageIndex.cs` / `TestCoverageResolver.cs`** — sammeln und aufloesen die Abdeckungssignale (Testklassenname, `typeof`/`nameof`, `@covers`) und sind als mechanisches Suchsignal fuer den Coverage-Audit relevant. (zuletzt: planning)
- **`src/AiNetLinter/Core/LinterEngine.cs`** — traegt zusaetzlich das einzige `InternalsVisibleTo` (`AiNetLinter.Tests`); je neuer Test-Assembly ist ein Eintrag noetig. (zuletzt: planning)

## Produktive Lade- und Ausfuehrungsgrenzen

- **`src/AiNetLinter/Baseline/SourceFileCatalog.cs`** — besitzt den zentralen MSBuild-Solution-Load sowie bereits einen internen Konstruktor fuer vorhandene `Solution`-Snapshots. (zuletzt: planning)
- **`src/AiNetLinter/Core/LinterEngine.cs`** — lokales Referenzmuster fuer getrennte Pfad-, Catalog- und Solution-Einstiege. (zuletzt: planning)
- **`src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs`** — koppelt den Filter-/Skeleton-Workflow derzeit direkt an einen Pfad-basierten MSBuild-Load. (zuletzt: planning)
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
- **`tests/Fixtures/FilterMini/`** — vorgesehener neuer kalibrierter Mehrprojekt-Bestand fuer Projekt-, Namespace-, Test- und Sichtbarkeitsfilter. (zuletzt: planning)

## Laufzeit-Hotspots und Migrationskandidaten

- **`src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs`** — 18-faellige Filtermatrix gegen die komplette eigene Solution und groesster konsistenter Migrationshebel; assertiert auf den Projektnamen `AiNetLinter.Tests` und ist damit an die heutige Solution-Struktur gebunden. (zuletzt: planning)
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
