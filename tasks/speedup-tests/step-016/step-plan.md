---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 016
corrects: null
title: "Refactoring-Drift-Scanner auf die In-Memory-Testplattform migrieren"
epic: EPIC-4
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5.6-sol Medium
created_by_model_knowledge_cutoff: 2024-06
created_at: 2026-08-12
related_to: [step-015]
---

# Step 016: Refactoring-Drift-Scanner auf die In-Memory-Testplattform migrieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-4` aus `roadmap.md` — nach den freigegebenen Skeleton-/Filter- und
  Duplicate-Detection-Scannerteilen bleiben weitere In-Memory-Scanner und Tools offen; dieser
  Step schliesst genau die `RefactoringDriftScannerTests`-Kohorte.
- **Konzept-Referenz:** `konzept.md` §1 „Testebenen und erlaubte Abhaengigkeiten", §2
  „Gemeinsame Testplattform", §7 „Sparsame Verifikation", §8 „Strangler-Migration" und §9
  „Grosse Drift-Loop-Steps".

## Aktueller Projektzustand (JIT-Kontext)

`step-015` ist `approved` und hat `RoslynTestSolutionFactory` um deterministische virtuelle
`Solution.FilePath`-/`Document.FilePath`-Werte erweitert. Diese vorhandene Seam ist unmittelbar
fuer `RefactoringDriftScanner` geeignet: `ScanAsync` arbeitet bereits auf einer geladenen
Roslyn-`Solution`; die interne Caller-Aufloesung kombiniert den Solution-Root mit relativen
`CallSiteEntry.FilePath`-Werten und findet anschliessend die Dokumente semantisch wieder. Eine
Produktcode- oder Factory-Erweiterung ist fuer diesen Step daher nicht erforderlich.

Die sieben Legacy-Vertraege in
`src/AiNetLinter.Tests/Mcp/Tools/RefactoringDriftScannerTests.cs` bauen dagegen noch einen lokalen
`AdhocWorkspace`, erzeugen BCL-Referenzen pro Test erneut und materialisieren virtuelle
Quelldokumente zusaetzlich als echte Temp-Dateien. Das Dateischreiben ist fuer den getesteten
Scannervertrag unnoetig; die bestehende Factory aus `AiNetLinter.TestKit` liefert die benoetigten
Pfadwerte ohne IO und muss wiederverwendet werden. Die Tests bleiben unabhaengige
`Category=Component`-Szenarien; weder `PreparedSolutionFixture` noch eine serialisierende
Collection ist gerechtfertigt.

Der Roslyn-Aufrufbaum zeigt neben den sieben direkten Scannervertraegen nur den produktiven
`DuplicateDetectionTool`-Dispatch und dessen separate Legacy-Tooltests als Konsumenten. Deshalb
bleiben `DuplicateDetectionToolTests` und `DuplicateDetectionToolRefactoringDriftTests` bewusst
ausserhalb dieses Scanner-Schnitts. Der produktseitige Coverage-Audit zeigt zugleich eine
nicht-triviale, dokumentierte Caller-Normalisierung fuer Lambda-/anonyme Funktionssymbole, die
von der bisherigen Matrix nicht direkt belegt wird: Ein korrekter Helper-Aufruf innerhalb einer
Lambda darf nicht als Drift-Kandidat erscheinen. Dieser Fall wird als neuer Component-Vertrag
auf der guenstigsten Ebene ergaenzt; bestehende Assertions werden nicht abgeschwaecht.

Im Tech-Debt-Index gibt es keinen Eintrag im beruehrten Scanner-/FastTests-Bereich. Die beiden
offenen `auto_fixable: ja`-Eintraege betreffen `.agents/rules/AiNetLinter.mdc` bzw.
`MsBuildFixtureHostTests.cs` und werden nicht epic- oder bereichsfremd angehaengt. Die fremden
Aufgaben `tasks/validate-file` und `tasks/magic-values-in-mcp` bleiben unangetastet.

## Intention

Nach diesem Step liegt die vollstaendige `RefactoringDriftScannerTests`-Kohorte als schnelle,
rein in-memory ausgefuehrte Component-Kohorte in `AiNetLinter.FastTests`. Sie verwendet die in
step 015 geschaffene zentrale Pfadkalibrierung, entfernt den lokalen Workspace-/Temp-Datei-Builder
und belegt zusaetzlich die semantisch wichtige Caller-Normalisierung fuer Helper-Aufrufe in Lambdas.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.FastTests/Mcp/Tools/RefactoringDriftScannerTests.cs` (neu)

- **Was:** Alle sieben Legacy-Vertraege in den passenden FastTests-Namespace uebernehmen und als
  `Category=Component` markieren. Jede Szenario-Solution ueber
  `RoslynTestSolutionFactory.CreateSolution(virtualSolutionFilePath, ProjectSpec)` erzeugen und
  den zurueckgegebenen `RoslynTestSolution`-Owner deterministisch entsorgen. Den lokalen
  `AdhocWorkspace`-/`MetadataReference`-/Temp-Verzeichnis-Builder sowie `IDisposable`-Cleanup
  nicht mitkopieren; keine Dateien oder Verzeichnisse materialisieren.
- **Was:** Die bestehenden Verträge unverkuerzt erhalten: historischer Positiv-/Negativfall,
  unbekanntes Symbol, Property statt Methode, zu kurzer Helper, `maxResults`-Trunkierung,
  Helper-Displayname und Leermenge. Beim Coverage-Audit einen zusaetzlichen Fall aufnehmen, in
  dem ein korrekter Helper-Aufruf innerhalb einer Lambda liegt und deshalb ueber die bestehende
  Caller-Normalisierung ausgeschlossen bleibt, waehrend ein kalibrierter Inline-Duplikat-Kandidat
  weiterhin gefunden wird.
- **Warum:** Damit laeuft die Scannerlogik gegen ihre reale `Solution`-Seam, ohne die teure Grenze
  eines selbstgebauten Workspace-/Dateisystem-Setups in die schnelle Assembly zu uebertragen;
  zugleich wird ein produktiv dokumentierter semantischer Zweig erstmals belastbar abgedeckt.

### Datei 2: `src/AiNetLinter.Tests/Mcp/Tools/RefactoringDriftScannerTests.cs`

- **Was:** Nach einem einmaligen gruenen Legacy-Baseline-Lauf und erfolgreichem Alt-/Neu-Abgleich
  die gesamte Legacy-Testklasse physisch loeschen.
- **Warum:** Die Scannerkohorte muss am Step-Ende geschlossen migriert sein; eine parallele oder
  auskommentierte Alt-Kopie widerspricht der Strangler-Invariante.

### Datei 3: `tasks/speedup-tests/test-migration-ledger.md`

- **Was:** Den Eintrag `RefactoringDriftScannerTests` von `pending` auf `migrated` setzen, den
  realen FastTests-Abdeckungsort und die gezielte Verifikationsevidenz eintragen.
- **Warum:** Ledger, Legacy-Bestand und Zielbestand muessen atomar konsistent bleiben.

### Datei 4: `tasks/speedup-tests/codemap.md`

- **Was:** Nach der Umsetzung den neuen FastTests-Scannerort, die Wiederverwendung der virtuellen
  Factory-Pfade und die obsolet gewordene Legacy-Quelle als Pointer nachfuehren.
- **Warum:** Folgende EPIC-4-Toolschritte sollen die vorhandene In-Memory-Struktur erkennen und
  keinen weiteren lokalen `AdhocWorkspace`-/Temp-Datei-Builder erzeugen.

## Tests

- [ ] Vor der Legacy-Loeschung einmalig Baseline:
  `dotnet test src/AiNetLinter.Tests --filter FullyQualifiedName~RefactoringDriftScannerTests`
- [ ] `dotnet build`
- [ ] Gezielter Fast-/Scanner-/Factory-/Guard-Lauf:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~RefactoringDriftScannerTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] Gezielter Ledger-/Legacy-Gate-Lauf:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests"`
- [ ] Kein `Category!=Stress`-Vollprofil: `step-016` ist keine Epic-Grenze. Kein Stresslauf.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Alle sieben vorhandenen `RefactoringDriftScannerTests` sind als Component-Vertraege im
  Fast-Projekt vorhanden; der neue Lambda-Caller-Vertrag ergaenzt die Matrix, ohne einen
  bestehenden Vertrag zu ersetzen.
- [ ] Die Scannerkohorte referenziert weder lokalen `AdhocWorkspace`-Eigenbau noch Temp-
  Dateisystem, `SourceFileCatalog.LoadAsync`, MSBuild, externe Prozesse oder eine
  zwangsserialisierende Collection.
- [ ] Der Lambda-Fall kann bei fehlender Caller-Normalisierung rot werden: der korrekte Caller
  bleibt ausgeschlossen und ein echter Inline-Duplikat-Kandidat bleibt sichtbar.
- [ ] Die Legacy-Klasse ist geloescht und ihr Ledger-Eintrag zeigt auf den realen neuen
  Abdeckungsort; Ledger-Guard und Legacy-Build-Gate sind gruen.
- [ ] Build und die unter „Tests" genannten gezielten Filter sind gruen; kein Vollprofil und kein
  Stresslauf wurde fuer diesen Zwischenstep ausgefuehrt.
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch, imperativ, mit
  `[speedup-tests]`-Suffix)
- [ ] `step-016/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Grenzwerte (Produktion)` — `#nullable enable`,
  kleine Testhelper und keine duplizierten Workspace-/Pfad-Builder.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4. Updates & Tests` — xUnit-v3-Abdeckung, keine
  zwangsserialisierende Collection ohne reale Exklusivitaet und MCP-Nachweise ausschliesslich in
  der C#-Testinfrastruktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5. Qualitätsdrift-Prävention` — Assertions nicht
  abschwaechen, produktseitige Coverage-Luecken auf der guenstigsten Ebene schliessen und keine
  Task-/Step-IDs in Codekommentaren.

## Bekannte Ausnahmen

- Keine. `TD-001` betrifft den echten MCP-Framing-Subprozess und liegt ausserhalb dieses
  in-memory Scanner-Steps.

## Notes

- Nicht in Scope: `DuplicateDetectionToolTests`,
  `DuplicateDetectionToolRefactoringDriftTests`, die beiden
  `DuplicateDetectionEngine*Tests` sowie alle anderen MCP-Scanner/-Tools. Sie bleiben `pending`
  und werden erst in spaeteren JIT-Steps anhand des dann aktuellen Bestands geplant.
- Die virtuellen Pfade duerfen nicht auf der Platte materialisiert werden. Sie sind nur die
  Roslyn-Pfadidentitaet, die `DiffImpactAnalyzer` und `RefactoringDriftScanner` fuer die
  Caller-Aufloesung benoetigen.
- Der Coder wird adaptiv durch den Orchestrator gewaehlt; der Plan trifft dazu keine Vorgabe.
