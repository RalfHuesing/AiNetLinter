---
status: open
type: step-plan
task: speedup-tests
step: 018
corrects: null
title: "Recovery 4: verbleibende Snapshot-Fixtures mechanisch schliessen und 23er Batch gaten"
epic: EPIC-4
estimated_risk: medium
step_type: batch
items:
  - id: item-01
    title: "Verbleibende Live-Catalog-Testkonstruktionen auf Snapshot-Server umstellen"
    source: "TestResults/latest.trx; aktueller git diff"
  - id: item-02
    title: "CompileError-, DI- und Faulting-Specs pfadfidel vereinheitlichen"
    source: "42 Fehler im engen 11-Klassen-Filter"
  - id: item-03
    title: "Zehn rote Klassen in ursachengebundener Reihenfolge schliessen"
    source: "TestResults/latest.trx vom 2026-08-13 18:18:48"
  - id: item-04
    title: "Drei pfadgebundene Toolklassen wieder nach FastTests aufnehmen"
    source: "implementierte ReadOnlySolutionSnapshot-Seam"
  - id: item-05
    title: "Snapshot-/Live-Vertraege, 23er Gate und Ledger abschliessen"
    source: "konzept.md §3; test-migration-ledger.md"
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

# Step 018 – Recovery 4: verbleibende Snapshot-Fixtures mechanisch schliessen und 23er Batch gaten

## Scope dieser Recovery-Runde

Diese Runde plant **keine neue Architektur**. Sie bewahrt den uncommittierten, gruen bauenden
Working Tree und schliesst ausschliesslich die im letzten engen TRX bzw. aktuellen Diff sichtbaren
Restarbeiten:

1. Specs und Testkontext konsistent auf virtuelle Pfade/Snapshot-Zugriff bringen.
2. Die zehn noch rot protokollierten Klassen ursachengebunden gruen machen.
3. Erst danach die drei bereits vorgesehenen Toolklassen wieder nach FastTests aufnehmen.
4. Dann kombinierte Gates und Ledger ausfuehren.

Kein Reset, Checkout, Rebase, History-Rewrite oder pauschales Verwerfen bestehender Hunk-Gruppen.
Keine Assertion wird abgeschwaecht. Keine echten Dateien werden fuer FastTests angelegt.

## Aktueller, verifizierter Zustand

- `dotnet build --no-restore` ist gruen.
- Die produktive Snapshot-Architektur ist implementiert:
  - `McpCodeGraphServerOptionsFromParameters.ReadOnlySolutionSnapshot` ist additiv und optional.
  - `McpCodeGraphServer` weist Snapshot plus Catalog/LoadFunc ab.
  - Der Snapshot-Zweig kapselt die `Solution` in einen nicht-MSBuild-besitzenden Catalog,
    initialisiert keinen File-State und liefert in `GetCurrentSolution()` ohne Refresh zurück.
  - Catalog-/LoadFunc-Zweige bleiben unveraendert live und refreshbar.
  - `ProjectSpec.VirtualProjectDirectory` ist additiv implementiert.
  - `McpInMemoryTestContext.CreateServer()` setzt `ReadOnlySolutionSnapshot`.
- Damit entspricht die Seam dem fachlichen Kern des Plans. **Es fehlt keine weitere
  Produktverhaltensaenderung.** Offen sind nur Seam-Vertragstests sowie Testfixture-/Call-Site-
  Konsistenz. `McpCodeGraphServerRefresh` darf nicht mehr veraendert werden.
- Das letzte enge TRX enthaelt **124 Tests, 82 gruen, 42 rot** in zehn Klassen.
- Das TRX ist von 18:18:48; `SymbolGraphMiniSolutionSpec.cs` wurde um 18:19:09 noch einmal
  korrigiert. Die darin enthaltenen 27 SymbolGraph-Pfadfehler sind deshalb ein **stale
  Diagnoseblock**, nicht als weiterhin reproduziert zu behaupten. Es wird kein zusaetzlicher
  Planer-Testlauf benoetigt: Die verbleibenden nicht-stalen Ursachen sind aus Code und TRX
  eindeutig.

## Restfehler – exakte Cluster und mechanische Korrekturen

### Cluster A – zehn Tests laufen noch ueber den Live-Catalog statt Snapshot

**Ursache:** Virtuelle Dokumentpfade sind korrekt, werden aber von direkt konstruierten
`McpCodeGraphServer(... Catalog ...)`-Instanzen beim ersten `GetCurrentSolution()` als nicht
existente Dateien entfernt. Das ist kein Produktfehler; diese Test-Call-Sites umgehen noch den
neuen Kontext.

**Dateien/Methoden:**

- `GetHotspotsToolTests.cs`
  - `ExecuteAsync_MidRangeMaxLineCount_MarksFileAsWarning`
  - ersetzt `_fixture.Catalog`-Server durch `_fixture.CreateServer(maxLineCount: 7)`.
- `MetricsTreeToolTests.cs`
  - alle vier roten Methoden
    `ExecuteAsync_CodeSizeMode_ReturnsTreeSortedByLocDescending`,
    `ExecuteAsync_CommentDensityMode_ReturnsTreeSortedByRatioAscending`,
    `ExecuteAsync_FileFilterExcludesMatchingFiles_NarrowsTree`,
    `ExecuteAsync_MaxDepth_DoesNotThrowAndClampsGracefully`.
  - lokales `CreateState()` darf keinen `_fixture.Catalog` mehr verwenden, sondern gibt
    `_fixture.CreateServer()` zurück.
- `MetricsTreeRoslynScannerTests.cs`
  - `ExecuteAsync_ViolationDensityMode_ReturnsTreeSortedByViolationCountDescending`
  - `ExecuteAsync_ViolationDensityMode_MaxDepth_DoesNotThrowAndClampsGracefully`
  - `ExecuteAsync_ComplexityMode_RootPointingToSingleFile_ReturnsSingleNodeTree`
  - `ExecuteAsync_ComplexityMode_HighComplexityMethodVsTrivialMethod_SortsHighComplexityFirst`
  - `ExecuteAsync_ComplexityMode_FileWithoutMethods_ReturnsZeroMetricsWithoutCrash`
  - `CreateState()` wird `_fixture.CreateServer()`; die beiden lokalen `scenario`-Pfade werden
    jeweils mit `using var context = new McpInMemoryTestContext(scenario)` und
    `context.CreateServer()` ausgefuehrt. Die manuellen `SourceFileCatalog`-Variablen entfallen.

**Helpergrenze:** Anschliessend wird `McpInMemoryTestContext.Catalog` geloescht. Ein erneut
erzeugter nicht-besitzender Catalog ist im FastTests-Scope nicht mehr erlaubt und wuerde denselben
Fehler wieder ermoeglichen.

### Cluster B – vier CompileError-Tests verwenden noch pfadlose, inhaltlich verkuerzte Specs

**Ursache:** `CompileErrorMiniSolutionSpec.CreatePlural/CreateSingular` rufen die Factory ohne
virtuellen Solutionpfad auf. Ausserdem heisst die Methode in `ValidClassA` aktuell `A`, waehrend
CallTree den Legacy-Vertrag `ValidClassA.DoWork` aufloest.

**Mechanischer Spec-Fix in `CompileErrorMiniSolutionSpec.cs`:**

- `CreatePlural()` verwendet virtuellen Solutionpfad
  `C:\ainetlinter-virtual\CompileErrorMini.slnx`, Projektname `CompileErrorMini` und
  `VirtualProjectDirectory: "src/CompileErrorMini"`.
- Quellen werden zeilen-/namensgetreu aus der bestehenden Mini-Fixture gespiegelt:
  - `ValidClassA`: `DoWork()` und `Compute(int x)`;
  - `ValidClassB`: `Greet(string name)`;
  - `ValidClassC` im Namespace `CompileErrorMini.Sub` mit `Process()`;
  - `BrokenClassA`: defekte `F`-Signatur;
  - `BrokenClassB : DoesNotExist`;
  - `BrokenClassC` mit `UndefinedType`-Feld.
- `CreateSingular()` verwendet
  `C:\ainetlinter-virtual\SingleCompileErrorMini.slnx`, Projektname
  `SingleCompileErrorMini`, `VirtualProjectDirectory: "src/SingleCompileErrorMini"` und die
  namensgetreuen `ValidClass`-/`BrokenClass`-Quellen.

**Betroffene Tests:**

- `GetCallTreeToolTests.ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning`
- `FindReferencesToolTests.ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning`
- `GetHotspotsToolTests.ExecuteAsync_CompileErrorFixture_OutputStartsWithPluralAggregateWarning`
- `GetHotspotsToolTests.ExecuteAsync_SingleCompileErrorFixture_OutputStartsWithSingularAggregateWarning`

Keine Assertion aendern; nach dem Spec-Fix muessen Symbolauflösung und Singular-/Pluralheader wie
im Legacy-Test funktionieren.

### Cluster C – ein DI-Test verwendet noch eine pfadlose Spec

**Ursache:** `DiRegistrationMiniSolutionSpec.Create()` besitzt weder Solution- noch Document-Pfad;
`GetTypeHierarchy`/`FindSymbolTool` formatiert die Symbolfundstelle und erhaelt einen leeren Pfad.

**Mechanischer Fix:**

- `DiRegistrationMiniSolutionSpec.Create()` verwendet
  `C:\ainetlinter-virtual\DiRegistrationMini.slnx`, Projektname `DiRegistrationMini`,
  `VirtualProjectDirectory: "src/DiRegistrationMini"`.
- Die bereits semantisch bindbaren `IServiceCollection`-Stubs sowie AddScoped/AddSingleton/
  AddTransient und `MyAddScopedHelper` bleiben unveraendert.
- Betroffener Test:
  `GetTypeHierarchyToolTests.ExecuteAsync_TypeWithDiRegistration_IncludesDiRegistrationSection`.

### Cluster D – Faulting-Spec ist noch pfadlos und zwei Tool-Call-Sites nutzen Live-Catalog

**Ursache:** Der letzte Lauf zeigt hier derzeit keinen roten Test, aber die Implementierung ist
noch inkonsistent mit derselben behobenen Pfad-/Refresh-Ursache und wuerde beim 23er Gate erneut
driften.

**Mechanischer Fix:**

- `FaultingSolutionFixture` setzt einen virtuellen `Solution.FilePath`
  `C:\ainetlinter-virtual\Faulty.slnx` und `Faulty.cs` unter
  `C:\ainetlinter-virtual\FaultyProject\Faulty.cs`; der werfende `TextLoader` bleibt unveraendert.
- `GetViolationsToolTests.ExecuteAsync_LinterEngineThrows_ReturnsMalfunctionWithIsErrorTrueAndRetryHint`
  und
  `SafeguardToolTests.ExecuteAsync_LinterEngineThrows_ReturnsMalfunctionWithIsErrorTrueAndRetryHint`
  konstruieren den Server mit `Catalog: null, ReadOnlySolutionSnapshot: fixture.Solution`, nicht
  ueber einen `SourceFileCatalog`.
- `PatternDetectScannerTests` und `SafeguardScannerTests` bleiben direkte Solution-Konsumenten;
  sie brauchen keinen Serverwrapper.

### Cluster E – 27 im TRX rote SymbolGraph-Tests sind nachgelagert stale

**Ursache im protokollierten Lauf:** Dokumente lagen durch eine doppelte
`src/SymbolGraphMini/src/SymbolGraphMini`-Zusammensetzung nicht an den Pfadkonstanten/Scopefiltern.
Die nach dem TRX gespeicherte aktuelle Spec verwendet jetzt korrekt:

- Projektname `SymbolGraphMini`;
- `VirtualProjectDirectory: "src/SymbolGraphMini"`;
- einfache Document-Namen wie `Greeter.cs`;
- Konstanten `C:\ainetlinter-virtual\src\SymbolGraphMini\*.cs`.

**Betroffene protokollierte Methoden:**

- sieben Nicht-CompileError-Methoden in `FindReferencesToolTests`:
  vier Position/Line-only-Aufloesungen, Ambiguous-Line sowie die transitiven SymbolBody- und
  TypeHierarchy-Aufrufe;
- drei Standard-Snapshot-Methoden in `GetHotspotsToolTests`:
  SmallMaxLineCount critical/StructuredContent und ProjectName-Scope;
- alle sechs roten `GetViolationsToolTests`-Standard-/Scope-/StructuredContent-Vertraege;
- alle fuenf roten `PatternDetectToolTests`;
- zwei rote `SafeguardScannerTests` und vier rote `SafeguardToolTests`.

**Anweisung:** Diese 27 Tests nicht einzeln umschreiben. Nach Cluster A–D zuerst denselben engen
zehn-Klassen-Filter erneut ausfuehren. Nur ein danach noch roter Test darf anhand seines neuen TRX
ursachengerecht bearbeitet werden; erwartete Strings/Positionen nicht prophylaktisch veraendern.

## Implementierte Seam – fehlende Absicherung, keine weitere Produktlogik

Die produktiven Änderungen in `McpCodeGraphServerOptions.cs` und `McpCodeGraphServer.cs` entsprechen
der beschlossenen Grenze. Recovery 4 darf dort nur noch Dokumentations-/Formatkorrekturen vornehmen,
falls Build/Linter sie verlangen. Kein weiterer Refresh-Schalter und keine Aenderung an
`McpCodeGraphServerRefresh`.

Vor der Tool-Wiederaufnahme werden in
`src/AiNetLinter.FastTests/Mcp/McpCodeGraphServerReadOnlySnapshotTests.cs` drei enge Component-
Vertraege ergaenzt:

1. virtueller nicht existierender Dokumentpfad bleibt bei wiederholtem `GetCurrentSolution()`
   erhalten;
2. `RefreshCount` bleibt 0;
3. Snapshot plus Catalog wird mit der implementierten `ArgumentException` abgewiesen.

In `RoslynTestSolutionFactoryTests` kommt genau ein Vertrag hinzu: Projektname
`SymbolGraphMini` bleibt erhalten, waehrend `VirtualProjectDirectory: "src/SymbolGraphMini"` den
erwarteten Dokumentpfad erzeugt. Keine neue Factory-Funktion und keine weitere Produkt-Seam.

## Coder-Reihenfolge Recovery 4

1. **Keine Vorab-Build-/Breitwiederholung.** Aktuellen gruenen Buildstand und alle uncommittierten
   Aenderungen bewahren.
2. **Cluster A:** alle verbleibenden Catalog-Server in Hotspots/Metrics durch Snapshot-Kontext
   ersetzen; `McpInMemoryTestContext.Catalog` entfernen.
3. **Cluster B:** CompileError plural/singular mit virtuellen Pfaden und Legacy-identischen Namen/
   Quellen korrigieren.
4. **Cluster C:** DI-Spec mit virtuellem Solution-/Projektpfad versehen.
5. **Cluster D:** Faulting-Solution pfadtragend machen und die zwei Tooltests auf Snapshot-Options
   umstellen.
6. **Seam-/Factory-Vertragstests** ergaenzen; keine weitere Produktlogik.
7. **Enger Recovery-Gate:** die zehn im letzten TRX roten Klassen kombiniert ausfuehren. Neues TRX
   auswerten. Cluster E gilt erst dann als bestaetigt geschlossen.
8. **Drei Tool-Wiederaufnahmen:**
   - `src/AiNetLinter.Tests/Mcp/Tools/DependencyGraphToolTests.cs`
   - `src/AiNetLinter.Tests/Mcp/Tools/GetFileSkeletonToolTests.cs`
   - `src/AiNetLinter.Tests/Mcp/Tools/GetSymbolBodyToolTests.cs`
   wieder an ihre durch `e864407` etablierten FastTests-Pfade bringen, Namespace/Trait/Usings auf
   Component setzen und ausschliesslich den Snapshot-Kontext/virtuelle Specs verwenden.
9. `SuppressionScannerTests` bleibt im Legacy-Projekt und `pending`.
10. Erst jetzt Build, 23er Kombinationsgate, Live-Refresh-/Suppression-/Guard-Gates und Ledger.

## Gates

1. Enger zehn-Klassen-Recovery-Filter; Erwartung nach Cluster A–D: 0 Fehler.
2. Snapshot-/Factory-Filter:
   `McpCodeGraphServerReadOnlySnapshotTests|RoslynTestSolutionFactoryTests`.
3. `dotnet build --no-restore` einmal nach den drei Tool-Wiederaufnahmen.
4. Ein kombinierter FastTests-Filter fuer alle 23 migrierten Klassen plus Snapshot-Seam-Testklasse.
5. Legacy-Live-Gate:
   `McpCodeGraphServerConstructorTests|McpCodeGraphServerFileDiscoveryTests|McpCodeGraphServerStalenessMtimeCacheTests`.
6. Legacy-`SuppressionScannerTests`.
7. FastTests Dependency-/Category-Guards und Integration-
   `TestMigrationLedgerConsistencyTests`.
8. Statischer Check im 23er FastTests-Scope: keine `File.*`, `Directory.*`, `Path.GetTempPath`,
   `SourceFileCatalog.LoadAsync`, manuellen `SourceFileCatalog`-Server, MSBuild-Referenz oder
   serialisierende Collection; anschliessend Testmethoden-/Ledger-Abgleich und `git diff --check`.

Kein Stresslauf und kein volles Nicht-Stress-Profil, da EPIC-4 mit diesem Step noch nicht endet.

## Abnahme

- Der Build bleibt gruen und der enge Recovery-Filter ist vollstaendig gruen.
- Snapshot-Seam und `VirtualProjectDirectory` sind durch enge Tests abgesichert; Live-Refresh bleibt
  durch bestehende Legacy-Tests unveraendert nachgewiesen.
- 23 Klassen liegen in FastTests; nur `SuppressionScannerTests` liegt wieder im Legacy-Projekt.
- CompileError erzeugt exakt drei/eine Fehlerdatei und bietet `ValidClassA.DoWork`; DI- und
  Faulting-Specs tragen virtuelle Pfade; keine Platte wird angelegt.
- Genau 23 Ledgerzeilen sind `migrated`, Suppression bleibt `pending`; Result/Codemap stimmen.

## Risiko und Blockerbewertung

**Risiko: medium fuer den Gesamtstep, low fuer Recovery 4.** Die Architekturentscheidung ist
implementiert und der Build gruen. Die verbliebenen Arbeiten sind voraussichtlich **rein
content-mechanisch**: zehn bekannte Call-Sites, drei deklarative Spec-Pfadkorrekturen, zwei
Faulting-Tool-Optionen, vier enge Seam-/Factory-Vertraege und drei bereits festgelegte Moves.

Ein neuer inhaltlicher Blocker ist nicht sichtbar. Falls nach Cluster A–D der neue enge Lauf noch
rot ist, darf nur dessen aktualisiertes TRX die naechste Korrektur bestimmen; nicht zum alten
42-Fehler-Snapshot zurueckkehren.

## MCP-/Recherche-Entscheidung

Das aktuelle TRX, Dateizeitstempel und der konkrete uncommittierte Diff waren fuer diese enge
Recovery genauer als ein vor dem Recovery erzeugter MCP-Index. Es wurde kein Testlauf wiederholt
und kein MCP-Ergebnis redundant erzeugt.
