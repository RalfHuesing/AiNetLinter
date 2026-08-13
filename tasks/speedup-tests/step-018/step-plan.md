---
status: open
type: step-plan
task: speedup-tests
step: 018
corrects: null
title: "Read-only MCP-Roslyn-Kohorten als plattenfreien In-Memory-Super-Step fertigstellen"
epic: EPIC-4
estimated_risk: medium
step_type: batch
items:
  - id: item-01
    title: "Vier echte Dateisystemvertraege vorwaertsgerichtet ins Legacy-Projekt zurueckverschieben"
    source: "e864407 Roh-Renames; aktuelle Produktvertraege"
  - id: item-02
    title: "Deklarative SymbolGraph-, CompileError- und DI-Spezifikationen bereitstellen"
    source: "konzept.md Technische Leitplanken §2/§5"
  - id: item-03
    title: "MCP-In-Memory-Kontext und Faulting-Solution-Fixture vervollstaendigen"
    source: "step-result.md; Diagnose-Build vom 2026-08-13"
  - id: item-04
    title: "Duplicate-Detection-Tooldispatch migrieren"
    source: "test-migration-ledger.md: DuplicateDetectionToolTests + DuplicateDetectionToolRefactoringDriftTests"
  - id: item-05
    title: "Dependency-Graph-Scanner migrieren"
    source: "test-migration-ledger.md: DependencyGraphScannerTests"
  - id: item-06
    title: "Call-Graph-Traversal und Call-Tree-Tool migrieren"
    source: "test-migration-ledger.md: CallGraphTraversalTests + GetCallTreeToolTests"
  - id: item-07
    title: "SymbolIdentifierResolver und FindReferences migrieren"
    source: "test-migration-ledger.md: SymbolIdentifierResolverTests + FindReferencesToolTests"
  - id: item-08
    title: "Hotspots, Type-Hierarchy und DI-Heuristik migrieren"
    source: "test-migration-ledger.md: GetHotspotsToolTests + GetTypeHierarchyToolTests + DiRegistrationHeuristicsTests"
  - id: item-09
    title: "Violations-, Metrics-, Pattern-Detect- und Safeguard-Kohorten migrieren"
    source: "test-migration-ledger.md: neun Scanner-/Toolklassen"
  - id: item-10
    title: "McpToolResults und LinterAnalyzer-Semantikvertraege migrieren"
    source: "test-migration-ledger.md: drei Klassen"
  - id: item-11
    title: "Ledger, Kategorien und gezielte Gates abschliessen"
    source: "konzept.md Leitplanken §7/§8/§9"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-006
  - step-015
  - step-016
  - step-017
---

# Step 018: Read-only MCP-Roslyn-Kohorten als plattenfreien In-Memory-Super-Step fertigstellen

## Bezug und verbindlicher Ausgangspunkt

- **Task/Epic:** `speedup-tests`, EPIC-4.
- **Batch-Vorgabe:** `max_batch_items: 40`, `max_batch_diff_lines: 800`; reine
  Strukturmigrationen werden als Super-Step gebuendelt.
- **Historie:** Commit `e864407` enthaelt bereits die Roh-Renames von 24 Klassen. Diese Historie
  wird nicht umgeschrieben. Der Coder arbeitet ausschliesslich vorwaerts auf diesem Commit und dem
  vorhandenen, uncommittierten Teilport.
- **Ist-Zustand:** Der einmalige diagnostische `dotnet build --no-restore` ist mit 26
  Compilefehlern rot. `step-result.md` beschreibt den abgebrochenen Versuch; seine Blockerwertung
  ist durch diese Neuplanung aufgehoben. Das Legacy-Ausgangsgate war dort mit 243 Tests gruen.
- **Scope nach Vertragspruefung:** 20 Klassen bleiben im plattenfreien Super-Step. Vier Klassen
  besitzen dagegen einen echten Datei-/Server-Refresh-Vertrag und werden als neue Aenderung gezielt
  ins Legacy-Projekt zurueckverschoben. Der Batch bleibt mit 20 Klassen und 12 fachlichen Kohorten
  gross; es entsteht kein step-019.

## Vollstaendige Ursache der 26 Compilefehler

Die Fehler sind keine Produktregression, sondern sechs unvollstaendig spezifizierte Portierungs-
luecken des Roh-Moves:

1. **1 Fehler – nullable MaxLineCount:**
   `Fixtures/McpInMemoryTestContext.cs:45` uebergibt `int?` an den nicht-nullbaren
   `McpCodeGraphServerOptionsFromParameters.MaxLineCount` (`int`, Default 700).
2. **11 Fehler – entfernte Legacy-Workspace-Fassade:**
   `FindReferencesToolTests` (8), `GetFileSkeletonToolTests` (1) und
   `GetSymbolBodyToolTests` (2) greifen noch auf `_fixture.Workspace.GreeterPath`, `CallerPath`
   oder `OtherCallerPath` zu, obwohl der neue `SymbolGraphCatalogFixture` keine
   `Workspace`-Property besitzt.
3. **7 Fehler – Compile-Error-Fixtures fehlen:**
   sechs Verwendungen von `CompileErrorMiniFixtureWorkspace` in `GetFileSkeletonToolTests`,
   `FindReferencesToolTests`, `GetHotspotsToolTests`, `GetCallTreeToolTests`,
   `GetViolationsToolTests` und `GetTypeHierarchyToolTests`, plus eine Verwendung von
   `SingleCompileErrorMiniFixtureWorkspace` in `GetHotspotsToolTests`.
4. **2 Fehler – gemeinsame Header-Assertion fehlt:**
   `GetHotspotsToolTests` referenziert zweimal `CompileErrorHeaderAssertions`, die noch nur im
   Legacy-Projekt existiert.
5. **1 Fehler – DI-Fixture fehlt:**
   `GetTypeHierarchyToolTests` referenziert `DiRegistrationMiniFixtureWorkspace`.
6. **4 Fehler – Malfunction-Fixture fehlt:**
   `GetViolationsToolTests`, `PatternDetectScannerTests`, `SafeguardScannerTests` und
   `SafeguardToolTests` referenzieren `TestHelper.CreateFaultySolution`; der Legacy-Helper erzeugt
   dabei eine reale Probe-Datei und ist im FastTests-Projekt weder sichtbar noch zulaessig.

Summe: **1 + 11 + 7 + 2 + 1 + 4 = 26**.

## Scope-Entscheidung: 20 bleiben, vier gehen vorwaerts zurueck

Folgende vier Roh-Renames werden als eigener normaler Diff (nicht per Reset/Rebase) von
`src/AiNetLinter.FastTests` nach `src/AiNetLinter.Tests` zurueckverschoben. Namespace, Usings und
Kategorie werden dabei auf den Stand des Legacy-Vertrags gebracht; bereits angefangene
FastTests-Teilportierungen dieser vier Dateien werden gezielt verworfen:

- `Mcp/Tools/DependencyGraphToolTests.cs`: Der Toolvertrag akzeptiert reale/relative
  `filePath`-Argumente. `McpCodeGraphServer.GetCurrentSolution()` entfernt nicht existierende
  Dokumentpfade beim Refresh; virtuelle Pfade koennen deshalb den Dispatchvertrag ohne neuen
  Produkt-Seam nicht tragen.
- `Mcp/Tools/GetFileSkeletonToolTests.cs`: Das Tool ist ausschliesslich ein Dateipfad-Dispatch und
  loest ueber `DiffImpactAnalyzer.FindDocumentByPath` nach dem Server-Refresh auf.
- `Mcp/Tools/GetSymbolBodyToolTests.cs`: Zwei explizite Datei:Zeile:Spalte-Vertraege laufen durch
  denselben Server-Refresh. Ein Wechsel nur auf stabile Symbol-IDs wuerde Abdeckung entfernen.
- `Suppression/SuppressionScannerTests.cs`: `SuppressionScanner.ScanFile` prueft `File.Exists` und
  liest mit `File.ReadLines`; die Platte ist hier Produktvertrag, nicht Testarrangement.

Kein Kompatibilitaetswrapper, kein Abschwaechen/Loeschen von Assertions und keine neue
Produkt-Seam sind erlaubt. Die vier Klassen bleiben im Ledger `pending` und werden spaeter mit den
Platten-/Adapterkohorten eingeordnet.

Die verbleibenden **20 Klassen** sind:

- `DuplicateDetectionToolTests`, `DuplicateDetectionToolRefactoringDriftTests`
- `DependencyGraphScannerTests`
- `CallGraphTraversalTests`, `GetCallTreeToolTests`
- `SymbolIdentifierResolverTests`, `FindReferencesToolTests`
- `GetHotspotsToolTests`, `GetTypeHierarchyToolTests`, `DiRegistrationHeuristicsTests`
- `GetViolationsToolTests`
- `MetricsTreeRoslynScannerTests`, `MetricsTreeToolTests`
- `PatternDetectScannerTests`, `PatternDetectToolTests`
- `SafeguardScannerTests`, `SafeguardToolTests`
- `McpToolResultsTests`
- `LinterAnalyzerArchitectureRuleTests`, `LinterAnalyzerTests`

## Exakte Fixture- und Besitzergrenzen

### 1. Deklarative Specs unter `src/AiNetLinter.FastTests/Fixtures/`

Die Specs enthalten nur Namen, Quelltexte, virtuelle Pfadkonstanten und `ProjectSpec`-Erzeugung;
sie besitzen weder Workspace noch Catalog/Server und fuehren keine IO aus.

#### `SymbolGraphMiniSolutionSpec.cs`

- Spiegelt die physischen C#-Quellen **zeilengetreu** als Raw-Strings:
  `Greeter.cs`, `Caller.cs`, `OtherCaller.cs`, `Hierarchy.cs`, `ViolationTrigger.cs`. Die aktuelle
  einzeilige Teilportierung ist zu ersetzen, weil die Positionsvertraege Zeilen 2/3/5/7/8
  benoetigen.
- Stellt zwei bewusst getrennte Erzeuger bereit:
  - `CreateServerSnapshot()` ohne Solution-Dateipfad. Dokumente haben keine `FilePath`s und werden
    deshalb vom Staleness-Refresh eines `McpCodeGraphServer` nicht als geloeschte Plattendateien
    entfernt. Konsumenten sind alle symbolnamen-/stable-id-basierten Tooltests.
  - `CreatePathSnapshot()` mit rein virtuellem Solutionpfad
    `C:\ainetlinter-virtual\SymbolGraphMini.slnx`, Projektname `src` und Dokumentnamen
    `SymbolGraphMini/<Datei>.cs`. Dieser Snapshot wird **nur direkt gegen Solution-/Scanner-
    Funktionen** verwendet, nie hinter `McpCodeGraphServer.GetCurrentSolution()`.
- Exponiert die aus der Spezifikation abgeleiteten Konstanten `GreeterPath`, `CallerPath` und
  `OtherCallerPath` (`C:\ainetlinter-virtual\src\SymbolGraphMini\...`). Es gibt keine nachgebaute
  `Workspace`-Fassade.
- Konsumenten: serverloser Positionszweig von `FindReferencesToolTests`,
  `SymbolIdentifierResolverTests`, `DependencyGraphScannerTests` und alle lokalen Scanner-Szenarien,
  die virtuelle Pfade fuer Ausgabe/Projektklassifikation brauchen.

#### `CompileErrorMiniSolutionSpec.cs`

- `CreatePlural()` enthaelt genau sechs Dokumente im Projekt `CompileErrorMini`:
  `ValidClassA/B/C.cs` zeilengetreu sowie
  `BrokenClassA.cs` (`public void F( { } }`),
  `BrokenClassB.cs` (`: DoesNotExist`) und
  `BrokenClassC.cs` (`UndefinedType`). Damit entstehen Fehler in exakt drei Dateien.
- `CreateSingular()` enthaelt im Projekt `SingleCompileErrorMini` genau `ValidClass.cs` und das
  syntaktisch defekte `BrokenClass.cs`; damit entsteht genau eine Fehlerdatei.
- Beide Erzeuger liefern pfadlose `RoslynTestSolutionFactory`-Snapshots. Der zugehoerige
  `SourceFileCatalog` wird mit `hasLoadingErrors: false` erzeugt: Die erwartete Warnung stammt aus
  `Compilation.GetDiagnostics()`, nicht aus einem simulierten MSBuild-Ladefehler.
- Konsumenten: Plural in `FindReferencesToolTests`, `GetHotspotsToolTests`,
  `GetCallTreeToolTests`, `GetViolationsToolTests`, `GetTypeHierarchyToolTests`; Singular nur in
  `GetHotspotsToolTests`. Der entfallene `GetFileSkeletonToolTests`-Konsument bleibt Legacy.

#### `DiRegistrationMiniSolutionSpec.cs`

- Projekt `DiRegistrationMini`, ein `Program.cs` mit `IReporter`, `ConsoleReporter`, `Composition`
  und den drei Aufrufen `AddScoped<IReporter, ConsoleReporter>()`,
  `AddSingleton<IReporter>()`, `AddTransient<IReporter>()`; die Variable
  `MyAddScopedHelper = "not a match"` bleibt als Negativsignal erhalten.
- Damit Roslyn die Aufrufe semantisch statt ueber `dynamic` bindet, enthaelt derselbe deklarative
  Source minimale `IServiceCollection`-/Extension-Stubs im Namespace
  `Microsoft.Extensions.DependencyInjection`. Keine NuGet-/MSBuild-Abhaengigkeit.
- Konsumenten: `DiRegistrationHeuristicsTests` und der DI-Abschnitt in
  `GetTypeHierarchyToolTests`; beide verwenden dieselbe Spec, keine lokale zweite Sourcekopie.

### 2. `McpInMemoryTestContext.cs`

- Der Kontext besitzt nur `SourceFileCatalog`/`McpCodeGraphServer`-Erzeugung um eine uebergebene
  immutable `Solution`; Workspace-Lebensdauer bleibt beim aufrufenden `RoslynTestSolution` bzw.
  bei `PreparedSolutionFixture`.
- `CreateServer(int? maxLineCount = null, ...)` erzeugt bei `null` die Options-Defaultgrenze 700
  (entweder getrennte Konstruktionszweige oder `maxLineCount ?? 700`), niemals eine nullable-
  Uebergabe. Optional bleiben nur bereits vorhandene Config-/UsedDefaultConfig-Eingaenge.
- Kein `SourceFileCatalog.LoadAsync`, kein MSBuild, keine Temp-Datei, keine Collection-Fixture.
- Server-Snapshots stammen aus `SymbolGraphMiniSolutionSpec.CreateServerSnapshot()` oder den
  pfadlosen CompileError-/DI-Specs. Die aktuell im Teilport definierte Klasse
  `SymbolGraphCatalogFixture` wird nicht als Legacy-Kompatibilitaetsfassade fortgefuehrt.
- Read-only Standardsnapshot wird ueber die vorhandene assemblyweite `PreparedSolutionFixture`
  unter einem eindeutigen Szenarionamen materialisiert. Mutierende oder fehlerwerfende Szenarien
  besitzen einen lokalen `RoslynTestSolution` und werden mit `using` entsorgt. Keine
  `[Collection("SymbolGraphCatalog")]`-Attribute bleiben zur Serialisierung zurueck.

### 3. `FaultingSolutionFixture.cs`

- Fokussierter FastTests-lokaler Owner fuer den Malfunction-Vertrag; **kein**
  `TestHelper.CreateFaultySolution`-Kompatibilitaetswrapper.
- Besitzt einen `AdhocWorkspace`, ein C#-Projekt mit den gecachten
  `RoslynTestSolutionFactory.CoreReferences` und ein Dokument `Faulty.cs` mit
  `DocumentInfo`/einem privaten `ThrowingTextLoader`.
- `filePath` und `Solution.FilePath` bleiben `null`. `SourceFileCatalog.IsValidDocument` verwendet
  dann den Dokumentnamen, waehrend der MCP-Staleness-Refresh keine virtuelle Datei als geloescht
  entfernen kann. Der Loader wirft weiter deterministisch
  `InvalidOperationException("Simulierter Lesefehler ...")` beim Textzugriff.
- Exponiert nur `Solution` und entsorgt den Workspace. Kein Probe-Verzeichnis, kein
  `File.WriteAllText`, kein Cleanup-`try/finally`.
- Genau vier Konsumenten: `GetViolationsToolTests`, `PatternDetectScannerTests`,
  `SafeguardScannerTests`, `SafeguardToolTests`.

### 4. `CompileErrorHeaderAssertions.cs`

- FastTests-lokaler Assertion-Helper unter `src/AiNetLinter.FastTests/Mcp/` (kein TestKit-Code):
  `AssertStartsWithCompileErrorHeader(text, expectedFileCount)` prueft `Hinweis:` sowie exakt
  Singular `1 Datei hat Compile-Fehler` oder Plural `N Dateien haben Compile-Fehler`.
- Konsument im verbleibenden Scope: `GetHotspotsToolTests`. Keine Produktlogik und kein Zugriff
  aus IntegrationTests.

## Kohortenarbeiten am verbleibenden Batch

- **Duplicate Detection (2):** Alle 19 Tool-Dispatchvertraege behalten. Temp-Verzeichnisse und
  manuelle `SolutionInfo`-/Dateischreib-Builder durch pfadlose lokale `ProjectSpec`s ersetzen;
  Mode-/Argumentfehler, Scannerfehler, Structured Content, Sufficiency und Truncation unveraendert.
- **Dependency Graph Scanner (1):** Alle Dokument-/Typ-, incoming/outgoing/both-, Depth-, Zyklus-,
  Aggregations-, Self-edge-, BCL-, Truncation- und Testprojekt-Sortiervertraege direkt auf
  `CreatePathSnapshot()` bzw. lokalen virtuellen Mehrprojekt-`ProjectSpec`s ausfuehren. Die Platte
  war hier nur Builder-Implementation; `DependencyGraphScanner` bekommt `Document`/`Solution`.
- **Call Graph/Tree (2):** Standardsnapshot bzw. CompileError-Plural verwenden; Gruppierung,
  Depth-Cap, Top-N, ASCII/Mermaid, Warnung und Fehlerantworten erhalten.
- **Symbolauflösung/References (2):** Name/stable ID gegen den pfadlosen Serversnapshot;
  Datei:Zeile(:Spalte) direkt gegen `CreatePathSnapshot()` und dessen Pfadkonstanten. Ambiguitaet,
  Accessor-Normalisierung, Call-Sites, Structured Content, Depth und Warnung erhalten.
- **Hotspots/Hierarchy/DI (3):** MaxLineCount-Varianten ueber den Kontext, Singular/Plural ueber die
  CompileError-Specs, Hierarchie/Interfaces ueber SymbolGraph und DI-Sektion ueber die eine DI-Spec.
- **Violations (1):** Lint-/Format-/Defaultconfig-/Truncation-Vertraege gegen pfadlose Specs;
  Malfunction ueber `FaultingSolutionFixture`.
- **Metrics (2):** Standardsnapshot plus isolierte lokale `ProjectSpec`s; bestehende
  `[Collection("SymbolGraphCatalog")]`-Attribute und irrefuehrende Workspace-Kommentare entfernen.
- **Pattern Detect (2):** Alle lokalen `TempSourceDirectory`-/`File.WriteAllText`-Builder in
  pfadlose oder virtuelle `ProjectSpec`s ueberfuehren; Malfunction ueber Faulting-Fixture.
- **Safeguard (2):** Read-only Score-/Retry-/Dispatchvertraege auf Specs; Malfunction ueber
  Faulting-Fixture. Keine reale Probe-Datei.
- **Ergebnis-/Analyzervertraege (3):** `McpToolResultsTests` sowie die beiden reinen
  `LinterAnalyzer`-Klassen als `Unit`; nur Namespace/Kategorie/Usings korrigieren, keine neue
  Infrastruktur erzwingen.

Alle Solution-/Server-Vertraege tragen `Category=Component`, reine Parser-/Formatter-/Analyzer-
Vertraege `Category=Unit`. Kommentare nennen keine Task-IDs. Nullable bleibt aktiv. Bestehende
Assertions und Testfaelle werden weder entfernt noch abgeschwaecht.

## Verbindliche Coder-Reihenfolge

1. **Arbeitsbaum sichern/verstehen:** Auf `e864407` und dem vorhandenen Teilport weiterarbeiten;
   keine Historienumschreibung, kein pauschales Restore fremder Aenderungen.
2. **Vier Rueck-Moves zuerst:** Die vier oben genannten Dateien als neue Renames nach
   `AiNetLinter.Tests` zurueckfuehren und ihre Teilport-Hunks gezielt auf Legacy-Kompatibilitaet
   bringen. Danach darf kein FastTests-Code sie referenzieren.
3. **Specs vor Konsumenten:** `SymbolGraphMiniSolutionSpec`, `CompileErrorMiniSolutionSpec` und
   `DiRegistrationMiniSolutionSpec` mit den exakt beschriebenen Sources/Pfaden anlegen.
4. **Owner vervollstaendigen:** `McpInMemoryTestContext` korrigieren, `FaultingSolutionFixture` und
   `CompileErrorHeaderAssertions` anlegen; erst danach Konsumenten umstellen.
5. **Compilefehler kohortenweise schliessen:** zuerst SymbolGraph-Pfade, dann CompileError-
   Konsumenten, dann DI, dann die vier Faulting-Konsumenten. Nach dieser Phase muss der
   Diagnosebestand von 26 Fehlern auf null fallen.
6. **Temp-Builder entfernen:** Duplicate-, DependencyScanner- und Pattern-Kohorten auf
   `ProjectSpec` umstellen; danach im verbleibenden 20er FastTests-Scope kein `File.*`,
   `Directory.*`, `Path.GetTempPath`, `TestTempDirectory`, `SourceFileCatalog.LoadAsync` oder
   `Microsoft.CodeAnalysis.MSBuild`.
7. **Restliche Klassen/Kategorien bereinigen:** Collections entfernen, Owner korrekt disposen,
   Namespaces/Usings und Unit/Component-Traits finalisieren.
8. **Ledger erst bei gruenem Code:** Genau die 20 erfolgreich migrierten Klassen auf `migrated`
   mit ihrem FastTests-Ziel setzen; die vier Rueck-Moves bleiben `pending`. Danach Codemap/Result
   aktualisieren.

## Gates (sparsam, kein Stress- oder Vollprofil)

1. Statischer Scope-Check: Im 20er FastTests-Scope keine verbotenen Platte-/MSBuild-Aufrufe und
   keine `SymbolGraphCatalog`-Collection; alle 20 Klassen besitzen genau einen gueltigen Trait.
2. `dotnet build --no-restore` — zwingend, weil der uebernommene Zwischenstand mit 26 Fehlern rot
   ist und auch die vier rueckverschobenen Legacy-Klassen Teil der Solution bleiben.
3. Ein kombinierter, exakt auf die 20 Klassen gefilterter Lauf von
   `src/AiNetLinter.FastTests`; keine Wiederholung pro Klasse.
4. Ein kombinierter Legacy-Filter fuer die vier rueckverschobenen Klassen, um den bewahrten
   Datei-/Refreshvertrag nachzuweisen.
5. Gezielte Guards: FastTests Dependency-/Category-Guards sowie
   `TestMigrationLedgerConsistencyTests` im IntegrationTests-Projekt.
6. `git diff --check` und Coverage-Abgleich: 20 Ledgerzeilen migriert, vier pending, keine
   verschwundene Testmethode. Kein `Category=Stress` und kein komplettes Nicht-Stress-Profil, da
   step-018 keine EPIC-Grenze schliesst.

## Abnahmekriterien

- Build gruen; alle sechs Ursachenbereiche der 26 Fehler geschlossen.
- Genau 20 Klassen befinden sich vollstaendig und plattenfrei in FastTests; genau vier benannte
  Klassen sind als vorwaertsgerichtete Renames wieder im Legacy-Projekt.
- SymbolGraph-Sources sind zeilengetreu; pfadloser Server- und virtueller Direkt-Snapshot sind
  getrennt. CompileError ergibt exakt drei bzw. eine Fehlerdatei. DI-Heuristik nutzt eine einzige
  semantisch bindbare Spec. Faulting-Solution erzeugt keine Platte.
- Keine neue Produkt-Seam, kein MSBuild-/Prozess-/Repovertrag, kein Kompatibilitaetswrapper.
- Alle bestehenden Testvertraege bleiben erhalten; Ledger und Codemap spiegeln den realen
  20/4-Stand.

## Risiko

**Medium.** Das strukturelle Verschieben ist mechanisch; das Risiko liegt in Pfad-/Zeilenfidelitaet,
Compile-Diagnostics und Workspace-Lebensdauer. Die Trennung pfadloser Server-Snapshots von
virtuellen direkten Pfad-Snapshots verhindert den bekannten Server-Refresh-Konflikt. Der
vorwaertsgerichtete Rueck-Move der vier echten Datei-Vertraege vermeidet eine riskante Produkt-Seam
und haelt den verbleibenden Batch fachlich homogen.

## Nicht-Ziele

- Keine Produktcode-Aenderung oder neue Testbarkeit-Seam.
- Keine Platte, kein MSBuild, kein Prozess, kein Git/Repo und keine EPIC-5/6-Kohorte im 20er Batch.
- Keine Migration der vier Rueck-Moves oder der bereits zuvor ausgeschlossenen sieben
  filesystem-/config-/processgebundenen MCP-Klassen.
- Kein Vollprofil und kein Stresslauf in diesem Step.

## Erwartete Artefakte

- 20 vollstaendig migrierte FastTests-Klassen in den vorhandenen Zielpfaden.
- Vier neue Rueck-Renames nach `src/AiNetLinter.Tests`.
- `src/AiNetLinter.FastTests/Fixtures/SymbolGraphMiniSolutionSpec.cs`
- `src/AiNetLinter.FastTests/Fixtures/CompileErrorMiniSolutionSpec.cs`
- `src/AiNetLinter.FastTests/Fixtures/DiRegistrationMiniSolutionSpec.cs`
- korrigiertes `src/AiNetLinter.FastTests/Fixtures/McpInMemoryTestContext.cs`
- `src/AiNetLinter.FastTests/Fixtures/FaultingSolutionFixture.cs`
- `src/AiNetLinter.FastTests/Mcp/CompileErrorHeaderAssertions.cs`
- aktualisiertes `tasks/speedup-tests/test-migration-ledger.md`, `codemap.md`, `step-result.md`.

## MCP-/Recherche-Entscheidung

Der fruehere MCP-Indexstand war fuer den alten 24er Plan nicht mehr ausreichend: Nach dem
committeten Roh-Move und den uncommittierten Teilports waren Builddiagnostik, `git status/diff` und
gezielte aktuelle Reads die kuerzere und wahrheitsgetreuere Quelle. MCP war nach der ausdruecklichen
Nutzervorgabe optional und wurde fuer diese reine Planrevision nicht benoetigt. Es wurde genau ein
diagnostischer Build ausgefuehrt; aktuelle Resultate wurden danach nicht redundant erneut erzeugt.
