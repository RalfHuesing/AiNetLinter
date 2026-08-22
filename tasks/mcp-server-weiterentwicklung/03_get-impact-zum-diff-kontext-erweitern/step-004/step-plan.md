---
status: open
type: step-plan
task: 03_get-impact-zum-diff-kontext-erweitern
step: 004
corrects: null
title: "Testfundament, gebatchte Test-Zuordnung & recommendedTestCommands (EPIC-3+4)"
epic: EPIC-3+EPIC-4
estimated_risk: medium
step_type: single
items: []
created_by: orchestrator
created_by_model: stealth/ox-alpha
created_by_model_knowledge_cutoff: unbekannt
created_at: 2026-08-22T22:20:00+02:00
related_to: [step-002, step-003]
---

# Step 004: Testfundament, gebatchte Test-Zuordnung & recommendedTestCommands (EPIC-3+4)

## Bezug

- **Task:** `03_get-impact-zum-diff-kontext-erweitern`
- **Epics:** Konsolidierung laut Nutzerentscheidung (task-state.md) — EPIC-3
  (Testfundament & Einmal-Ausführungs-Nachweis) und EPIC-4 (gebatchte
  Test-Zuordnung & recommendedTestCommands) in einem Step.
- **Konzept-Referenz:** §Scope Must-have („Tests werden für alle gezeigten
  geänderten Symbole in einem gebatchten Solution-Scan zugeordnet; kein
  vollständiger Testprojekt-Scan pro Symbol", „deduplizierte dotnet test-
  Filterbefehle pro betroffenem Testprojekt als recommendedTestCommands",
  „changedFiles mit kompakten Hunk-Ranges"), §Performance-Regeln („Testdokumente
  pro Aufruf höchstens einmal parsen/semantisch auswerten"), §Tests
  (Fixture ≥2 Produktionsprojekte + 1 Testprojekt; Diff ändert zwei Methoden in
  zwei Dateien, eine privat ohne externe Aufrufstellen; direkte Invocation und
  Namenskonvention als getrennte Evidenzarten; instrumentierter Counter: Git
  einmal, Testsolution einmal), Audit C.2 (größter Einzelblock des Tasks).

## Aktueller Projektzustand (JIT-Kontext)

Verifiziert am Codestand nach step-003 (`get_file_skeleton`, Datei-Lektüre):

1. **`TestCoverageScanner.FindTestsForSymbolAsync(ISymbol, Solution, ct)`**
   ist per-Symbol-API: jeder Aufruf iteriert alle Testprojekte/-dokumente neu
   (Audit-C-Muster „N-mal Vollscan"). Die Dokument-Auswertung liegt in
   `ProcessDocumentAsync`/`AnalyzeDocument` (root + semanticModel + Ziel),
   Evidenzkonstanten in `TestCoverageMatchReasons` (Direct Member Match /
   Naming Convention / @covers / typeof), Prioritätslogik in
   `GetMatchReasonPriority`. Ergebnis: `TestCoverageScannerResult`
   (TotalMatchingTests, TestFiles mit FilePath/TestClassName/Category/
   MatchReason/TestMethods).
2. **`GetTestContextTool.BuildRecommendedCommands(IReadOnlyList<TestFileCoverageResult>)`**
   baut bereits ausführbare `dotnet test`-Filterbefehle je Testdatei —
   Formatvorlage für `recommendedTestCommands`; aktuell `private`.
3. **Mehrprojekt-Fixtures existieren:** `RoslynTestSolutionFactory.CreateSolution(params ProjectSpec[])`
   mit `ProjectReferences` (TestKit) und
   `McpInMemoryTestContext.CreateScenario(ProjectSpec)` (FastTests-Muster aus
   step-001-Kettentests). Echte-Git-Seite: `GitImpactMiniFixtureWorkspace`
   (ein Produktionsprojekt Calculator inkl. privater `Normalize` +
   Änderungsmethoden) — für die Pfad-Trennungsnachweise aus step-003 ausreichend,
   wird hier nicht verbogen.
4. **Einmal-Nachweis-Lücke:** Weder Analyzer noch Scanner exposez heute Zähler.
   Für „Git einmal"/„Testsolution einmal" genügt ein interner, optionaler
   Zählerkanal; „Linter einmal" ist erst mit der Violations-Stufe (step-005)
   beweisbar und wird dort nachgezogen.

## Intention

Nach diesem Step kann die Engine die statische Test-Zuordnung für VIELE
geänderte Symbole in EINEM Solution-Durchlauf liefern (Dokumente genau einmal
parsen/semantisch auswerten, Match gegen alle Ziele), inklusive deduplizierter
`recommendedTestCommands` je betroffenem Testprojekt — und es gibt eine
wiederverwendbare neutrale Mehrprojekt-Fixture sowie instrumentierte Einmal-
Nachweise für Git und Testsolution. Sichtbares Tool-Wiring bleibt EPIC-6.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Core/TestCoverageScanner.cs`

- **Was:** Neue Batch-API
  `FindTestsForSymbolsAsync(IReadOnlyList<ISymbol> targetSymbols, Solution solution, CancellationToken ct)`
  → neuer Ergebnistyp `TestCoverageBatchScanResult` (je Ziel: Symbol-ID via
  bestehender stabiler ID-Logik bzw. ISymbol-Key, `TestCoverageScannerResult`-
  äquivalente Datei-Treffer; zusätzlich solutionweite Dedup-Info).
  Umbau des Innenlebens: Projekte/Dokumente werden GENAU EINMAL iteriert;
  pro Dokument SyntaxRoot + SemanticModel genau einmal beziehen, dann gegen
  ALLE Ziele matchen (Ziel-Paar-Informationen TypeName/MemberName vorab
  normalisieren). Evidenzarten/Konstanten/Prioritäten unverändert wiederverwendet.
  Die BESTEHENDE `FindTestsForSymbolAsync` bleibt Signatur-/Verhaltensidentisch
  bestehen und delegiert auf die Batch-API mit `[symbol]` (keine zweite Logik).
- **Warum:** Konzept-Muss-Have gebatchte Zuordnung; DRY; Bestandstests bleiben grün.

### Datei 2: `src/AiNetLinter/Mcp/Tools/TestContext/GetTestContextTool.cs`

- **Was:** `BuildRecommendedCommands` von `private` zu `internal` und als
  schmale Weiterleitung auf einen neuen gemeinsamen Helper
  `TestRecommendationBuilder.BuildDotNetTestCommands(...)` (Core oder
  gleichnamige interne Klasse im TestContext-Ordner) — so kann EPIC-6 später
  dieselbe Quelle für `recommendedTestCommands` nutzen. Zusätzlich Dedup:
  mehrere Treffer im selben Testprojekt ergeben EINEN Befehl je Projekt
  (Filter über die Vereinigung der Klassennamen), deterministisch sortiert.
- **Warum:** Konzept verlangt deduplizierte Befehle PRO Testprojekt als
  vertraglichen Bestandteil; eine Command-Wahrheit statt zweier Formatter.

### Datei 3: `src/AiNetLinter/Core/DiffImpactCounters.cs` (neu, klein)

- **Was:** Interner Record/Container `DiffImpactCounters` mit
  Interlocked-Zählern (GitRuns, TestSolutionScans, LintRuns — letzterer wird
  erst in step-005 inkrementiert) plus optionale Übergabe an
  `AnalyzeDiffAsync`/`AnalyzeChangeContextAsync` (über den bestehenden
  Request-Record ergänzt, kein fünfter Parameter) und an die neue Batch-Scan-
  Stufe. Ohne Übergabe verhält sich alles exakt wie heute (Null-Objekt).
- **Warum:** Konzept fordert instrumentierten Nachweis „Git einmal,
  Testsolution einmal"; Null-Overhead im Produktivpfad.

### Datei 4: `src/AiNetLinter.FastTests/Fixtures/ChangeContextScenarioFactory.cs` (neu)

- **Was:** Neutrale Wiederverwendungs-Fixture (statische Factory auf
  `McpInMemoryTestContext.CreateScenario`/`RoslynTestSolutionFactory`):
  drei Projekte `App.Core` → `App` (Referenz) → `App.Tests` (Referenz auf
  beide, xUnit-ähnliche Klassen ohne echtes Paket). Zwei geänderte Methoden
  in zwei Dateien: `App.OrderService.PlaceAsync` (public, hat Call-Sites)
  und `App.Core.AuditLogger.LogInternal` (private, KEINE externen Aufruf-
  stellen); synthetische Hunk-Ranges wie in den step-002/003-Tests. Liefert
  Solution + Symbol-Handles + Hunk-Ranges + erwartete Testtreffer (direkte
  Invocation in einem Test, Namenskonvention in einem zweiten, beides gegen
  `PlaceAsync`; `LogInternal` erhält Naming-Convention-Treffer).
- **Warum:** Konzept-Testfixture (≥2 Prod-Projekte + 1 Testprojekt, private
  Methode ohne Aufrufstellen) als Grundlage dieses UND der folgenden Steps.

### Datei 5: `src/AiNetLinter.FastTests/Core/TestCoverageBatchScannerTests.cs` (neu)

- **Was:** Tests auf der Fixture: Batch-Zuordnung liefert für BEIDE Ziele
  Treffer aus EINEM Durchlauf (per `DiffImpactCounters.TestSolutionScans == 1`);
  Evidenzarten getrennt (Direct Member Match vs. Naming Convention Match);
  private `LogInternal` erscheint trotz fehlender externer Call-Sites;
  Single-Symbol-Wrapper liefert identische Ergebnisse wie zuvor (Bestands-
  `TestCoverageScannerTests` bleiben unangetastet grün);
  `recommendedTestCommands`: zwei Trefferklassen im selben Testprojekt →
  genau ein deduplizierter Befehl, deterministisch.
- **Warum:** xUnit-Pflicht; Kernnachweise von EPIC-4.

### Datei 6: `src/AiNetLinter.FastTests/Core/DiffImpactAnalyzerOnceOnlyTests.cs` (neu)

- **Was:** Counter-Nachweise: ein `change-context`-artiger Lauf über die
  Fixture mit N=2 geänderten Symbolen inkrementiert `GitRuns` um GENAU 1 und
  `TestSolutionScans` um GENAU 1 (nicht N). Hinweis im Testkommentar: LintRuns
  folgt mit der Violations-Stufe (step-005).
- **Warum:** Konzept-Testpunkt „Instrumentierter Test/Counter"; Teilnachweis
  hier, Vollzug (inkl. Linter) in step-005 dokumentiert.

## Tests

- [ ] Batch-Zuordnung: beide Ziele aus EINEM Scan (`TestSolutionScans == 1`)
- [ ] Evidenzarten getrennt: Direct Member Match ≠ Naming Convention Match
- [ ] Private Methode ohne externe Call-Sites erhält Naming-Convention-Treffer
- [ ] `FindTestsForSymbolAsync` (Wrapper) unverändert: alle Bestands-Tests grün
- [ ] `recommendedTestCommands` je Testprojekt dedupliziert + deterministisch
- [ ] `GitRuns == 1` und `TestSolutionScans == 1` bei Multi-Symbol-Lauf
- [ ] Alle übrigen bestehenden FastTests + IntegrationTests bleiben unangepastet grün

## Definition of Done

- [ ] Alle „Konkreten Änderungen" umgesetzt
- [ ] Kein vollständiger Testprojekt-Scan PRO Symbol mehr (Counter-Beweis)
- [ ] Build grün (Zero-Warning), beide Gate-Commands grün (Category!=Stress)
- [ ] Dogfooding: `metrics_lookup` für neue/geänderte Symbole im Grünen,
      `find_duplicates` ohne neue Cluster
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-004/step-result.md` geschrieben; Status→`done (pending audit)`
- [ ] CodeMap aktualisiert (TestCoverageScanner, neue Fixture, Counter)

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#grenzwerte-produktion` — ≤500 Zeilen/Datei
  (Scanner ist großzügig gesplittet halten!), ≤60 Zeilen/Methode, ≤4 Parameter,
  `sealed`, `#nullable enable`, kein leeres catch
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention` — DRY
  (Wrapper statt Duplikat, ein Command-Builder), Zero-Warning, keine Task-ID-
  Kommentare; `.agents/rules/AiNetLinterRichtlinien.mdc#4-updates-tests` —
  xUnit v3, keine Serialisierungs-Collection, `TestTempDirectory` falls Temp
  nötig (hier: in-memory, nicht nötig)

## Bekannte Ausnahmen

- „Linter einmal"-Nachweis ist in diesem Step strukturell NICHT möglich
  (Violations-Stufe existiert noch) — bewusst nach step-005 verschoben;
  Counter-Feld existiert bereits.
- TD-001 (`CreateScenario`-Ergonomie, `auto_fixable: nein`) wird bewusst
  NICHT angehängt.

## Notes

- **Anti-Loop-Check:** CodeMap widerspricht nicht — step-002 legte das
  Ergebnisobjekt, step-003 den breiten Scanner; dieser Step fügt die
  Test-Zuordnungsstufe hinzu, ohne bestehende Entscheidungen zu drehen.
- **Nicht in diesem Step:** Violations-Berechnung/Filterung, Tool-Wiring
  (`detailLevel`, Caps, Completeness), Doku (EPIC-7), `maxTestsPerSymbol`-
  Cap-Anwendung (bleibt Tool-Ebene EPIC-6; die Batch-API nimmt die
  vorgekappte Zielliste entgegen).
- **Determinismus:** Ergebnis-Reihenfolge je Ziel stabil (Dateipfad,
  dann Klassenname, dann Methode); Befehlssortierung alphabetisch.
