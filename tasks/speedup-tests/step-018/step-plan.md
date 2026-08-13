---
status: open
type: step-plan
task: speedup-tests
step: 018
corrects: null
title: "MCP-Read-only-Snapshot-Seam einziehen und 23-Klassen-Super-Step abschliessen"
epic: EPIC-4
estimated_risk: medium
step_type: batch
items:
  - id: item-01
    title: "Interne Read-only-Solution-Snapshot-Seam im MCP-Server ergaenzen"
    source: "konzept.md §3 Laden und Ausfuehren trennen"
  - id: item-02
    title: "ProjectSpec-Pfadfidelitaet und vier In-Memory-Spezifikationen korrigieren"
    source: "TestResults/latest.trx; aktueller Recovery-Stand"
  - id: item-03
    title: "MCP-Testkontext auf virtuelle Read-only-Snapshots umstellen"
    source: "McpCodeGraphServer.GetCurrentSolution/RefreshStaleDocuments"
  - id: item-04
    title: "Drei pfadgebundene Toolklassen wieder in FastTests aufnehmen"
    source: "e864407 Roh-Renames; Snapshot-Seam"
  - id: item-05
    title: "20-Klassen-Recovery-Batch gegen die Snapshot-Seam stabilisieren"
    source: "142/220 gruen, 78 Fehler in TestResults/latest.trx"
  - id: item-06
    title: "Suppression-Dateivertrag im Legacy-Projekt belassen"
    source: "SuppressionScanner.ScanFile"
  - id: item-07
    title: "Ledger, Guards und gezielte Gates abschliessen"
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

# Step 018: MCP-Read-only-Snapshot-Seam einziehen und 23-Klassen-Super-Step abschliessen

## Verbindlicher Ausgangspunkt

- Commit `e864407` enthaelt die Roh-Renames von 24 Klassen. Der aktuelle Working Tree enthaelt
  darauf aufbauend die zweite Recovery-Portierung, vier vorwaertsgerichtete Rueck-Moves und die
  neuen FastTests-Fixtures. Kein Reset, Rebase, Checkout oder pauschales Restore dieses Stands.
- `dotnet build --no-restore` ist jetzt **gruen**. Dieser Fortschritt bleibt erhalten.
- Der kombinierte 20er-FastTests-Filter lief mit **220 Tests: 142 gruen, 78 rot**. Die vorhandene
  `TestResults/latest.trx` ist die Diagnosequelle; der Planer hat den Lauf nicht wiederholt.
- Die vorige Trennung in einen pfadlosen Server-Snapshot und einen virtuellen Direkt-Snapshot ist
  widerlegt. Viele fachliche Operationen benoetigen `Solution.FilePath` und `Document.FilePath`
  fuer relative Pfade. Ein virtueller Pfad allein reicht hinter `McpCodeGraphServer` ebenfalls
  nicht, weil der Live-Staleness-Refresh nicht existente Dateien korrekt als geloescht behandelt.
- EPIC-4 verlangt laut `konzept.md` §3 gerade die Trennung von Laden und objektbasierter
  Ausfuehrung. Deshalb gilt **Entscheidung A**: eine kleine interne Read-only-Snapshot-Seam im
  produktiven MCP-Server. Der Live-Catalog-/Refresh-Pfad bleibt unveraendert Default.

## Diagnose der 78 Fehler

### Verteilung nach Testklasse

| Fehler | Klasse |
|---:|---|
| 17 | `FindReferencesToolTests` |
| 9 | `CallGraphTraversalTests` |
| 8 | `GetHotspotsToolTests` |
| 8 | `GetTypeHierarchyToolTests` |
| 7 | `MetricsTreeToolTests` |
| 6 | `GetCallTreeToolTests` |
| 6 | `GetViolationsToolTests` |
| 6 | `MetricsTreeRoslynScannerTests` |
| 5 | `PatternDetectToolTests` |
| 4 | `SafeguardToolTests` |
| 2 | `SafeguardScannerTests` |

Damit schlagen elf der 20 Klassen fehl; neun Klassen sind im engen Lauf bereits vollstaendig
gruen. Die elf Klassen zerfallen nicht in elf unabhaengige Produktfehler, sondern in zwei
zusammenhaengende Infrastrukturursachen:

1. **37 unmittelbare Pfadausnahmen:** 19 `ArgumentNullException(path)`, neun
   `ArgumentException(path is empty)` und neun `ArgumentException(relativeTo is empty)` aus
   `SolutionFileWalker`, `PathNormalizer`, `CallGraphTraversal` und verwandten Formatierern.
   Pfadlose Dokumente sind fuer diese Operationen kein gueltiger Analyse-Input.
2. **41 Folgefehler in Toolresultaten/Assertions:** 23 `Assert.NotEqual`-Fehler, sechs
   `Assert.False`, vier `Assert.Contains`, vier `Assert.Null`, zwei `Assert.DoesNotContain` und
   zwei `Assert.StartsWith`. Bei servergebundenen virtuellen Szenarien entfernt
   `RefreshStaleDocuments` die nicht auf Platte vorhandenen Dokumente; bei pfadlosen Szenarien
   werden Pfadausnahmen als MCP-Fehlerantworten gefangen. Dadurch fehlen Symbole, Warnheader,
   Violations und strukturierte Inhalte oder `IsError` kippt.

Nach Aufrufgrenze sind **61 Fehler in acht servergebundenen Klassen**
(`FindReferences`, `Hotspots`, `TypeHierarchy`, `MetricsTreeTool`, `CallTree`, `Violations`,
`PatternDetectTool`, `SafeguardTool`) von der neuen Snapshot-Seam abhaengig. **17 Fehler in drei
direkten Scanner-/Traversal-Klassen** (`CallGraphTraversal`, `MetricsTreeRoslynScanner`,
`SafeguardScanner`) benoetigen keine Server-Seam, aber denselben vollstaendig pfadtragenden
virtuellen Snapshot. Es gibt keinen Hinweis auf 78 fachliche Produktregressionen.

## Entscheidung A: produktive Read-only-Snapshot-Seam

### Produktdateien und API

#### `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs`

- `McpCodeGraphServerOptions` erhaelt eine interne optionale Property
  `Solution? ReadOnlySolutionSnapshot` (Default `null`).
- `McpCodeGraphServerOptionsFromParameters` erhaelt am Ende den optionalen, benannten Parameter
  `Solution? ReadOnlySolutionSnapshot = null`. Bestehende positionale/benannte Call-Sites bleiben
  quellkompatibel; `From(...)` kopiert die Property.
- Zulaessige Zustaende:
  - `Catalog` oder `LoadFunc`: bestehender **LiveFileSystem**-Pfad mit Staleness-Refresh.
  - `ReadOnlySolutionSnapshot`: neuer immutable **Snapshot**-Pfad ohne Dateiabgleich.
  - alles `null`: bestehender `SolutionNotLoaded`-Zustand.
- `ReadOnlySolutionSnapshot` darf nicht gleichzeitig mit `Catalog` oder `LoadFunc` gesetzt sein.
  Der Serverkonstruktor validiert dies mit einer klaren `ArgumentException`; keine stille
  Prioritaetsregel.
- Die Property/API bleibt `internal`. Es entsteht keine Test-only-Bedingung, kein `#if TESTING`
  und keine oeffentliche Oberflaeche.

#### `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`

- Der Konstruktor erkennt den Snapshot-Zustand, kapselt die uebergebene `Solution` intern mit
  `new SourceFileCatalog(solution, hasLoadingErrors: false)` und markiert den Server als
  read-only Snapshot. Der Wrapper besitzt keinen MSBuild-Workspace; sein Dispose ist ein No-op.
- Im Snapshot-Zustand:
  - `LoadState` ist sofort `Loaded`.
  - `GetCurrentSolution()` liefert exakt den residenten immutable Snapshot und ruft weder
    `InitializeFileState` noch `RefreshStaleDocuments` auf.
  - Es werden keine Hashes, mtimes, `File.Exists`-Checks oder Directory-Sweeps ausgefuehrt.
  - `RefreshCount` bleibt deterministisch 0.
  - Der Server besitzt **nicht** den `AdhocWorkspace`; der aufrufende Testkontext besitzt und
    entsorgt `RoslynTestSolution`.
- Im bestehenden Catalog-/Background-Load-Zustand bleibt das Verhalten byte-for-byte fachlich
  gleich: Initialzustand cachen, geloeschte/neue/modifizierte Dateien erkennen, Catalog besitzen
  und entsorgen. `McpServerCommand` setzt keinen Snapshot und bleibt damit immer LiveFileSystem.
- ReloadConfig, Config-Snapshot, Loading/LoadFailed und Toolregistrierung werden nicht veraendert.

Diese Seam ist eine reale fachliche Ausfuehrungsgrenze: Der Server kann entweder einen live
geladenen, refreshbaren Catalog oder einen bereits vorbereiteten immutable Analyse-Snapshot
bedienen. Sie folgt `konzept.md` §3 und ist nicht bloss eine Testumgehung.

### Schutz der Seam

- Neue Component-Vertraege in
  `src/AiNetLinter.FastTests/Mcp/McpCodeGraphServerReadOnlySnapshotTests.cs`:
  1. Ein virtueller, nicht auf Platte existierender Solution-/Document-Pfad bleibt nach
     wiederholtem `GetCurrentSolution()` erhalten und liefert denselben Snapshot.
  2. `RefreshCount` bleibt 0.
  3. Snapshot plus Catalog wird sichtbar abgewiesen.
- Bestehende Live-Vertraege bleiben im Legacy-Projekt und werden gezielt ausgefuehrt:
  `McpCodeGraphServerConstructorTests`, `McpCodeGraphServerFileDiscoveryTests` und
  `McpCodeGraphServerStalenessMtimeCacheTests`. Damit ist nachgewiesen, dass der Default weiterhin
  reale Aenderungen entdeckt. Diese Tests werden nicht migriert und nicht abgeschwaecht.

## Pfadfidele In-Memory-Spezifikationen

### `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs`

- `ProjectSpec` erhaelt als letzten optionalen Wert `string? VirtualProjectDirectory = null`.
  Default bleibt `<SolutionDir>/<ProjectSpec.Name>` und veraendert bestehende Specs nicht.
- Bei gesetztem Wert werden nur die virtuellen `Document.FilePath`s unter
  `<SolutionDir>/<VirtualProjectDirectory>/<FileName>` gebildet. Projektname, Referenzen,
  CompilationOptions und Document.Name bleiben unveraendert. Es wird kein Verzeichnis angelegt.
- Relative Segmente werden per `Path.GetFullPath` normalisiert; ein absoluter oder aus dem
  Solution-Verzeichnis ausbrechender Wert wird mit `ArgumentException` abgewiesen. Ein kleiner
  Factory-Vertrag prueft Default und `src/Projekt`-Layout.
- Zweck: SymbolGraph muss zugleich Projektname `SymbolGraphMini` **und** Pfade
  `src/SymbolGraphMini/*.cs` besitzen. Die aktuelle Notloesung Projektname `src` verfälscht
  Projekt-Scope-/Violation-Vertraege.

### FastTests-Fixtures

- `SymbolGraphMiniSolutionSpec.Create()` ersetzt die Server-/Path-Doppelung. Ein einziger
  virtueller Snapshot verwendet Solutionpfad `C:\ainetlinter-virtual\SymbolGraphMini.slnx`,
  Projektname `SymbolGraphMini`, `VirtualProjectDirectory: "src/SymbolGraphMini"` und die
  zeilengetreuen Quellen `Greeter`, `Caller`, `OtherCaller`, `Hierarchy`, `ViolationTrigger`.
  `GreeterPath`, `CallerPath`, `OtherCallerPath` werden daraus abgeleitet.
- `CompileErrorMiniSolutionSpec.CreatePlural/CreateSingular` erhalten virtuelle Solution- und
  Dokumentpfade sowie die bereits portierten drei bzw. eine fehlerhafte Datei. Catalog-
  `hasLoadingErrors` bleibt false; Aggregate-Warnungen kommen aus Compilation-Diagnostics.
- `DiRegistrationMiniSolutionSpec.Create()` erhaelt virtuelle Pfade, behaelt Projektname
  `DiRegistrationMini` und die semantisch bindbaren `IServiceCollection`-Extension-Stubs.
- `FaultingSolutionFixture` erhaelt einen virtuellen `Solution.FilePath`, einen virtuellen
  `Faulty.cs`-Pfad und weiterhin den werfenden `TextLoader`. Kein realer Pfad wird angelegt oder
  abgefragt; der Snapshot-Server entfernt das Dokument nicht.
- `McpInMemoryTestContext` nimmt den besessenen `RoslynTestSolution`, erzeugt Server ausschliesslich
  ueber `ReadOnlySolutionSnapshot` und entsorgt den Workspace. Er erzeugt keinen nicht-besitzenden
  Catalog mehr fuer Testaufrufer. `maxLineCount`, Config und UsedDefaultConfig werden ueber den
  vorhandenen Options-Record weitergereicht.
- Keine `SymbolGraphCatalog`-Collection, kein `SourceFileCatalog.LoadAsync`, keine Temp-Datei und
  kein MSBuild in FastTests.

## Batch-Scope: 23 Legacy-Klassen

Die 20 derzeit im Recovery-Batch liegenden Klassen bleiben im Scope. Die Seam plus pfadfidele
Specs adressieren alle elf roten Klassen; die neun bereits gruenen Klassen werden nur auf
unerlaubte Rest-IO/Ownership und unveraenderte Assertions kontrolliert.

Zusaetzlich werden drei der vier aktuellen Rueck-Moves wieder **vorwaerts** nach FastTests
uebernommen, weil ihre Roslyn-/Toolvertraege mit dem Snapshot-Pfad jetzt ohne Platte pruefbar sind:

- `DependencyGraphToolTests`
- `GetFileSkeletonToolTests`
- `GetSymbolBodyToolTests`

Ihre relativen/absoluten Datei- und Datei:Zeile:Spalte-Vertraege bleiben unveraendert und laufen
gegen die virtuelle SymbolGraph-/CompileError-Spec. Die Dateien enden damit wieder an den durch
`e864407` committed Zielpfaden; das ist eine normale Fortsetzung des Working Trees, keine
Historienumschreibung.

Nur `SuppressionScannerTests` bleibt als vorwaertsgerichteter Rueck-Move in
`src/AiNetLinter.Tests/Suppression/`: `SuppressionScanner.ScanFile` prueft `File.Exists` und liest
`File.ReadLines`; dies ist ein echter Produkt-Dateivertrag und gehoert zu EPIC-5.

Der fertige Batch umfasst damit **23 migrierte Legacy-Klassen plus eine neue Snapshot-Seam-
Testklasse**. Fachliche Kohorten: Duplicate Detection (2), Dependency Graph (2), Call Graph/Tree
(2), Symbol/References/Body (3), Skeleton/Hotspots (2), Type/DI (2), Violations (1), Metrics (2),
Pattern Detect (2), Safeguard (2), Toolresults (1), LinterAnalyzer (2).

## Ausfuehrbare Coder-Reihenfolge

1. **Gruenen Buildstand bewahren:** Nur auf dem aktuellen Working Tree weiterarbeiten; keine
   Ruecksetzung. Den bestehenden Build nicht vorsorglich wiederholen.
2. **Produkt-Seam zuerst:** Options-Property/Parameter, Zustandsvalidierung und Snapshot-Zweig im
   Server implementieren. Live-Pfad nicht refactoren. Snapshot-Seam-Tests anlegen.
3. **Factory-Pfadfidelitaet:** `VirtualProjectDirectory` additiv implementieren und mit zwei engen
   Factory-Tests sichern.
4. **Specs vereinheitlichen:** SymbolGraph, CompileError plural/singular, DI und Faulting auf
   virtuelle Solution-/Dokumentpfade umstellen. Pfadlosen `CreateServerSnapshot` entfernen; eine
   kanonische Spec pro Szenario.
5. **Kontext umstellen:** `McpInMemoryTestContext.CreateServer` verwendet ausschliesslich
   `ReadOnlySolutionSnapshot`. Catalog-Property und manuelle Serverkonstruktionen der 23 Klassen
   auf den Kontext abbauen; Owner jeweils deterministisch disposen.
6. **Die 78 Fehler in dieser Reihenfolge schliessen:**
   - direkte Pfadkonsumenten `CallGraphTraversal`, `MetricsTreeRoslynScanner`, `SafeguardScanner`;
   - acht servergebundene rote Klassen;
   - CompileError-/DI-/Faulting-Sonderszenarien.
   Keine Assertion aendern, solange die erwartete Ausgabe nicht gegen die zeilen-/pfadtreue Spec
   nachgewiesen falsch ist.
7. **Drei Tool-Rueck-Moves umkehren:** DependencyGraphTool, FileSkeleton und SymbolBody wieder an
   ihre FastTests-Zielpfade bringen, Namespace/Trait/Usings auf Component setzen und denselben
   Kontext verwenden. Suppression bleibt Legacy.
8. **Statischer Cleanup:** Im 23er FastTests-Scope keine `File.*`, `Directory.*`,
   `Path.GetTempPath`, `TestTempDirectory`, `SourceFileCatalog.LoadAsync`, MSBuild-Referenz oder
   serialisierende Collection. Keine Task-ID-Kommentare; Nullable/Traits vollstaendig.
9. **Ledger zuletzt:** Erst nach gruenen Gates genau 23 Zeilen auf `migrated` setzen;
   `SuppressionScannerTests` bleibt `pending`. Codemap und Step-Result auf den realen Stand bringen.

## Gates

1. Enger Seam-/Factory-Filter:
   `McpCodeGraphServerReadOnlySnapshotTests|RoslynTestSolutionFactoryTests`.
2. `dotnet build --no-restore` einmal nach Seam, Specs und drei Wiederaufnahmen. Erwartung: der
   bereits gruene Solution-Build bleibt gruen.
3. Kombinierter FastTests-Filter fuer alle 23 migrierten Klassen plus die neue Snapshot-
   Testklasse. Erwartung: alle zuvor 220 Tests plus die drei wiederaufgenommenen Klassen und neuen
   Seam-Vertraege gruen.
4. Gezielter Legacy-Live-Refresh-Filter:
   `McpCodeGraphServerConstructorTests|McpCodeGraphServerFileDiscoveryTests|McpCodeGraphServerStalenessMtimeCacheTests`.
5. Gezielter Legacy-Filter `SuppressionScannerTests`.
6. FastTests Dependency-/Category-Guards und Integration-
   `TestMigrationLedgerConsistencyTests`.
7. Statischer verbotener-API-Check, Testmethoden-/Ledger-Abgleich und `git diff --check`.
   Kein Stresslauf und kein komplettes Nicht-Stress-Profil; step-018 schliesst EPIC-4 noch nicht.

## Abnahmekriterien

- Der Build bleibt gruen; alle 78 dokumentierten Fehler sind ursachengerecht geschlossen.
- Snapshot-Modus bewahrt virtuelle Dokumente samt Pfaden ohne einen einzigen Dateisystemzugriff;
  Live-Modus behaelt seine Refresh-Vertraege und bleibt Default fuer Produktion.
- Alle 23 Klassen liegen in FastTests und behalten ihre Testmethoden/Assertions. Nur
  `SuppressionScannerTests` liegt wieder im Legacy-Projekt.
- SymbolGraph besitzt gleichzeitig den korrekten Projektnamen und `src/SymbolGraphMini`-Pfade;
  CompileError, DI und Faulting sind vollstaendig virtuell und deterministisch.
- Keine Testumgehung via `File.Exists`, keine temporaeren echten Dateien, kein MSBuild/Prozess/Git,
  keine oeffentliche oder `#if TESTING`-Seam.
- Ledger, Codemap und Result spiegeln 23 migrierte Klassen und einen pending Suppression-Vertrag.

## Risiko

**Medium.** Die neue Produkt-Seam betrifft den zentralen Solution-Zugriff, bleibt aber intern,
additiv und auf einen expliziten Snapshot-Zustand begrenzt. Das Hauptrisiko ist eine versehentliche
Verhaltensaenderung des Live-Refresh-Pfads; die bestehenden drei Legacy-Refresh-Klassen sind deshalb
Pflichtgate. Gegenueber elf weiteren Rueck-Moves ist diese Loesung kleiner, kohärenter und direkt
vom Konzept gedeckt.

## Nicht-Ziele

- Kein Refactoring von `McpCodeGraphServerRefresh` oder seiner Hash-/mtime-/Sweep-Algorithmen.
- Keine Aenderung oeffentlicher MCP-Protokolle, Toolantworten oder CLI-Konfiguration.
- Keine Migration echter Mutation-/Loading-/Config-/Call-Log-/Prozess-/Git-Vertraege.
- Keine Migration von `SuppressionScannerTests` in diesem Step.
- Keine Assertion-Abschwaechung, keine echten FastTests-Dateien und kein Voll-/Stressprofil.

## MCP-/Recherche-Entscheidung

Die aktuelle `latest.trx`, der grüne Buildzustand und gezielte Reads der bereits veraenderten
Server-/Fixture-Dateien waren aktueller als der vor dem Recovery-Lauf erzeugte MCP-Index. Die
relevante Aufrufkette `GetCurrentSolution -> RefreshStaleDocuments -> McpCodeGraphServerRefresh.Run`
sowie alle `Path.GetRelativePath`-/`Document.FilePath`-Konsumenten wurden direkt im aktuellen Code
abgeglichen. Ein MCP-Aufruf haette hier keine zusaetzliche, aktuellere Evidenz geliefert und wurde
daher nicht redundant ausgefuehrt.
