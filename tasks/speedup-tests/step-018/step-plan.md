---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 018
corrects: null
title: "Recovery 6: fuenf rohe FastTests-Helper auf virtuelle Snapshots schliessen"
epic: EPIC-4
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "DependencyGraph-Scanner auf RoslynTestSolutionFactory umstellen"
    source: "offener statischer 23-Scope-Guard"
  - id: item-02
    title: "DuplicateDetection-Tooltests ueber McpInMemoryTestContext ausfuehren"
    source: "offener statischer 23-Scope-Guard"
  - id: item-03
    title: "PatternDetect- und Safeguard-Scanner rein virtuell aufbauen"
    source: "offener statischer 23-Scope-Guard"
  - id: item-04
    title: "62 Testvertraege und alle bereits gruenen Gates unveraendert nachweisen"
    source: "belegter Recovery-5-Gatestand"
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

# Step 018 - Recovery 6: fuenf rohe FastTests-Helper auf virtuelle Snapshots schliessen

## Ziel und harte Scope-Grenze

Recovery 6 beseitigt ausschliesslich den letzten offenen Plan-Guard in fuenf bereits migrierten
FastTests-Dateien. Testnamen, Testanzahl, Testdaten und Assertions bleiben erhalten. Es gibt keine
Produktcode-, TestKit-, Shared-Fixture-, Ledger- oder Legacy-Aenderung und keine neue Abstraktion.

Der bestehende uncommittierte Working Tree wird in-place fortgesetzt. Kein Reset, Checkout,
History-Rewrite oder pauschales Verwerfen. `SuppressionScannerTests` bleibt im Legacy-Projekt und
im Ledger `pending`.

Vom Fixbudget sind **4 von 6 Versuchen verbraucht**. Fuer die Implementierung und Diagnose dieser
Recovery bleiben hoechstens zwei ursachengebundene Fixversuche; die Stop-Kriterien unten sind
verbindlich.

## Aktueller, belegter Zustand

- Enger bisheriger Recovery-Gate: **126/126 gruen**.
- `dotnet build`: **0 Fehler, 0 Warnungen**.
- Kombinierter 23-Klassen-Gate plus Snapshot-Seam/Factory: **253/253 gruen**.
- Legacy-Live-Refresh: **8/8 gruen**.
- Legacy-`SuppressionScannerTests`: **1/1 gruen**.
- FastTests Dependency-/Category-Guards: **3/3 gruen**.
- Ledger-/Legacy-Gates: **5/5 gruen**.
- Ledger: exakt **23 `migrated`**, `SuppressionScannerTests` weiterhin `pending`.
- Einzig offen ist der statische 23-Scope-Check. Er findet Dateisystem- bzw.
  `SourceFileCatalog`-Helper in genau den unten beschriebenen fuenf Dateien.

Die vorhandenen Bausteine reichen aus:

- `RoslynTestSolutionFactory.CreateSolution(string, ProjectSpec[])` erzeugt eine besitzende
  `RoslynTestSolution` mit virtuellen Solution-/Dokumentpfaden und ohne Datei- oder
  Verzeichnisanlage.
- `ProjectSpec.VirtualProjectDirectory` kann auf `"."` gesetzt werden, damit bisherige relative
  Erwartungen wie `FileA.cs` bzw. `MyProject.Tests/AAATest.cs` trotz virtueller Solutionwurzel
  unveraendert bleiben.
- `McpInMemoryTestContext(RoslynTestSolution)` besitzt den Snapshot, entsorgt dessen Workspace und
  erzeugt den Server ueber `ReadOnlySolutionSnapshot`.
- `FaultingSolutionFixture` besitzt bereits virtuelle Pfade und einen werfenden `TextLoader`; ein
  zusaetzliches reales Probeverzeichnis ist funktionslos.
- Die aktuelle `LinterEngine` liest Dokumentinhalt ueber `Document.GetTextAsync`; der bestehende
  virtuelle Snapshot-Pfad ist durch die gruenen PatternDetect-/Safeguard-Tooltests und den
  253er-Gate belegt. `ScopeChecker` beendet die Projektverzeichnissuche bei einem nicht existenten
  virtuellen Verzeichnis ohne Fehler.

## Guard-Bewertung: keine Ausnahme fuer Live-Refresh

Alle Treffer muessen entfernt werden. Keine der fuenf Klassen veraendert eine Datei nach Aufbau
der `Solution` bzw. nach Servererstellung, vergleicht mtime-Werte, ruft einen Refresh auf oder
erwartet neu geladene Inhalte. Die zwei manuellen `SourceFileCatalog`-Instanzen transportieren nur
einen unveraenderten Snapshot; die drei Temp-Verzeichnis-Helper stellen lediglich virtuelle
Pfadwerte bzw. Roslyn-Inhalte auf Platte nach.

Die echten Live-Vertraege bleiben ausschliesslich in
`McpCodeGraphServerConstructorTests`, `McpCodeGraphServerFileDiscoveryTests` und
`McpCodeGraphServerStalenessMtimeCacheTests` im Legacy-Projekt und werden weiterhin durch 8/8
Tests nachgewiesen. Deshalb weder den statischen Guard lockern noch Allowlist, Kommentar-Ausnahme,
Trait oder neuen Live-Test in FastTests einfuehren.

## Mechanische Aenderungen pro Datei

### 1. `DependencyGraphScannerTests.cs` - 15 Verträge

- `TempSourceDirectory`, alle `Directory.CreateDirectory`-/`File.WriteAllText`-Aufrufe und den
  Parameter `baseDir` entfernen.
- Den lokalen `AdhocWorkspace`-/`ProjectInfo`-/`MetadataReference`-Builder durch genau einen
  privaten Helper ersetzen, der eine `RoslynTestSolution` via
  `RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\DependencyGraphScannerTests.slnx",
  new ProjectSpec("TestProject", files, VirtualProjectDirectory: "."))` liefert.
- In jedem der 15 Tests den Owner mit `using var testSolution = ...` halten und danach lokal
  `var solution = testSolution.Solution` verwenden. Der Sonderfall
  `MyProject.Tests/AAATest.cs` bleibt genau dieser Dokumentname; kein reales Unterverzeichnis.
- `GetDocument`, `GetTypeSymbolAsync`, sämtliche Quellen, erwarteten relativen Pfade und alle
  Testnamen unveraendert lassen. Veraltete XML-Kommentare zur Plattenspiegelung auf rein virtuelle
  Pfade korrigieren; unbenutzte IO-/Roslyn-Builder-Usings entfernen.

### 2. `DuplicateDetectionToolTests.cs` - 10 Verträge

- `IDisposable`, `_tempDir`, Konstruktor, Cleanup und den lokalen `BuildServer` mit
  `AdhocWorkspace`, `File.WriteAllText` und manuellem `SourceFileCatalog` entfernen.
- Einen privaten `CreateContext(files)`-Helper verwenden: Er erstellt ueber
  `RoslynTestSolutionFactory` eine virtuelle Solution
  `C:\ainetlinter-virtual\DuplicateDetectionToolTests.slnx` mit Projekt `TestProject` und
  `VirtualProjectDirectory: "."` und uebergibt den Owner an `McpInMemoryTestContext`.
- An jeder der neun bisherigen `BuildServer`-Call-Sites zuerst
  `using var context = CreateContext(...)`, dann `var state = context.CreateServer()` verwenden.
  Der `NoSolutionLoaded`-Test bleibt unveraendert ohne Context.
- Alle Input-, Text-, StructuredContent-, Bucket-, Trunkierungs- und Sufficiency-Assertions sowie
  `BuildMethod` unveraendert lassen. IO-, manuelle Roslyn-Builder- und Catalog-Usings entfernen.

### 3. `DuplicateDetectionToolRefactoringDriftTests.cs` - 9 Verträge

- Dieselbe Umstellung wie in Datei 2, mit virtuellem Solutionpfad
  `C:\ainetlinter-virtual\DuplicateDetectionToolRefactoringDriftTests.slnx`.
- Acht bisherige `BuildServer`-Call-Sites werden jeweils zu besitzendem `McpInMemoryTestContext`
  plus `CreateServer()`; `NoSolutionLoaded` bleibt ohne Context.
- `StubTypes`, `Helper`, `DriftedA`, alle Modus-/Fehler-/Candidate-/StructuredContent-Assertions
  und alle Testnamen unveraendert lassen. Keine Zusammenlegung mit den Scanner-Tests und kein
  neuer gemeinsamer Helper ausserhalb dieser Datei.

### 4. `PatternDetectScannerTests.cs` - 11 Verträge

- `TempSourceDirectory`, `Path.GetTempPath`, Probeverzeichnis samt `try/finally`, reale
  Datei-/Verzeichnisoperationen und den lokalen manuellen Roslyn-Builder entfernen.
- Einen privaten `CreateSolution(files)`-Helper auf
  `RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\PatternDetectScannerTests.slnx",
  new ProjectSpec("TestProject", files, VirtualProjectDirectory: "."))` umstellen.
- Der tuple-basierte `RunAsync`-Helper besitzt und entsorgt die `RoslynTestSolution` und reicht
  nur deren `.Solution` an den bestehenden Scanner-`RunAsync` weiter. Die drei direkten
  Solution-Faelle (leere Solution, Scope ohne Treffer, 50-Dateien-Trunkierung) sowie der
  Pattern-Subset-Fall halten ihren Owner ebenfalls mit `using var`.
- Im Malfunction-Test nur `using var faulty = new FaultingSolutionFixture()` verwenden; keine
  Platte vorbereiten. Quellen, Config, Patternauswahl, MaxResults und Assertions unveraendert.
- Die empirisch ueberholten Kommentare, ein realer Pfad sei notwendig, entfernen bzw. durch den
  aktuellen virtuellen Snapshot-Vertrag ersetzen.

### 5. `SafeguardScannerTests.cs` - 17 Verträge

- Den privaten `CreateAdhocSolution` durch `CreateSolution(files)` ersetzen, der eine besitzende
  `RoslynTestSolution` mit
  `C:\ainetlinter-virtual\SafeguardScannerTests.slnx`, Projekt `TestProject` und
  `VirtualProjectDirectory: "."` erzeugt.
- An den sieben lokalen Create-Call-Sites den Owner mit `using var testSolution` halten und
  `testSolution.Solution` an `CreateParameters` bzw. `SafeguardScannerParameters` geben.
- Im Malfunction-Test das redundante Probeverzeichnis und `try/finally` vollstaendig entfernen;
  `FaultingSolutionFixture` direkt verwenden.
- Die bestehende `_fixture.Solution` fuer KnownFixture/Determinismus sowie Retry-, Score-,
  Threshold-, Remediation- und Malfunction-Assertions unveraendert lassen. Nur unbenutzte IO-,
  Text- und manuelle Builder-Usings bereinigen.

## Unveraenderlichkeits- und Scope-Checks vor dem ersten Test

1. Der Diff beruehrt aus diesem Recovery-Auftrag nur die fuenf Dateien oben sowie die beiden
   Planungsartefakte. Insbesondere keine Produktdatei, Shared-Fixture, Ledgerzeile oder Legacy-Datei.
2. In den fuenf Dateien existieren weiterhin exakt **62** `[Fact]`/`[Theory]`-Methoden:
   15 + 10 + 9 + 11 + 17. Kein Testname und keine Assertion wurde entfernt oder umbenannt.
3. Der etablierte statische Check ueber alle 23 migrierten FastTests-Dateien liefert null Treffer
   fuer `File.*`, `Directory.*`, `Path.GetTempPath`, `SourceFileCatalog.LoadAsync`, manuelle
   `SourceFileCatalog`-Server, MSBuild-Referenzen und serialisierende Collections. Auch die lokalen
   `TempSourceDirectory`-, `_tempDir`- und `BuildServer`-Alt-Helper sind in den fuenf Dateien weg.
4. Ein Treffer wird behoben; er wird nicht per Guard-Ausnahme, Allowlist oder umbenanntem Wrapper
   verborgen.

## Gate-Reihenfolge

Nach bestandenem statischem Vorcheck genau in dieser Reihenfolge ausfuehren:

1. Fuenf-Klassen-Filter
   `DependencyGraphScannerTests|DuplicateDetectionToolTests|DuplicateDetectionToolRefactoringDriftTests|PatternDetectScannerTests|SafeguardScannerTests`
   in `AiNetLinter.FastTests`: Erwartung **62/62**.
2. Bisheriger enger Recovery-Filter: Erwartung **126/126**.
3. `dotnet build`: Erwartung **0 Fehler, 0 Warnungen**.
4. Kombinierter 23-Klassen- plus Snapshot-Seam/Factory-Filter: Erwartung **253/253**.
5. Legacy-Live-Refresh-Filter
   `McpCodeGraphServerConstructorTests|McpCodeGraphServerFileDiscoveryTests|McpCodeGraphServerStalenessMtimeCacheTests`:
   Erwartung **8/8**.
6. Legacy-`SuppressionScannerTests`: Erwartung **1/1**; bleibt `pending`.
7. FastTests Dependency-/Category-Guards: Erwartung **3/3**.
8. Integration-Ledger-/Legacy-Gates: Erwartung **5/5**.
9. Ledger statisch erneut auf exakt 23 `migrated` und Suppression `pending` pruefen; danach den
   23-Scope-Guard nochmals ausfuehren und `git --no-pager diff --check` verlangen.

Kein Stresslauf. Das vollstaendige Nicht-Stress-Profil bleibt dem Task-/Epic-Abschlussgate
vorbehalten; Recovery 6 erweitert seinen Scope nicht.

## Fixbudget und Stop-Kriterien

- Schlaegt der statische Vorcheck fehl, noch keinen Test starten: mechanisch in denselben fuenf
  Dateien nacharbeiten. Das ist Teil der geplanten Implementierung, kein Diagnose-Fixversuch.
- Schlaegt der 62er-Filter fehl, `TestResults/latest.trx` lesen und nur die konkrete Ursache in
  den fuenf Dateien korrigieren. Das verbraucht Versuch **5/6**. Danach zuerst den betroffenen
  Klassenfilter, dann statischen Guard und Gate 1 neu starten.
- Schlaegt ein spaeteres Gate eindeutig wegen dieser Helper-Umstellung fehl, darf genau ein
  weiterer ursachengebundener Versuch **6/6** erfolgen. Danach wieder ab statischem Guard bzw.
  Gate 1 beginnen; keine Assertions abschwaechen.
- Verlangt ein Fehler Produktlogik, eine neue Seam, Guard-Lockerung, Legacy-Migration,
  `SuppressionScannerTests`-Verschiebung oder Aenderungen ausserhalb der fuenf Dateien: sofort
  `blocked`, nicht improvisieren.
- Ist Versuch 6 nicht vollstaendig gruen, Step `blocked` mit exaktem TRX-/Gate-Befund. Kein
  siebter Versuch.
- Sind alle Gates gruen, Step 018 abschliessen; die bereits belegten 23 Ledger-Migrationen bleiben
  unveraendert.

## Abnahme

- Null statische Guard-Treffer im gesamten 23er FastTests-Scope, ohne Ausnahme.
- Alle 62 Testmethoden der fuenf Klassen mit identischen Namen und Assertions vorhanden und gruen.
- Tooltests verwenden `ReadOnlySolutionSnapshot` ausschliesslich ueber
  `McpInMemoryTestContext`; Scanner erhalten besitzende virtuelle `RoslynTestSolution`-Snapshots.
- Keine Testdatei oder Testverzeichnis wird angelegt; Faulting-Vertraege bleiben rein virtuell.
- Die belegten 126/126-, Build-, 253/253-, 8/8-, 1/1-, 3/3- und 5/5-Gates bleiben gruen.
- Ledger bleibt exakt 23 `migrated`; `SuppressionScannerTests` bleibt Legacy und `pending`.

## MCP-/Recherche-Entscheidung

Kein MCP-Aufruf erforderlich. Die fuenf konkreten Dateien, ihre aktuellen Helper, die vorhandenen
Factory-/Snapshot-Kontexte, die relevanten Produktpfade und der uncommittierte Diff liefern den
vollstaendigen mechanischen Zielzustand.
