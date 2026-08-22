---
status: done
type: step-result
task: 03_get-impact-zum-diff-kontext-erweitern
step: 004
epic: EPIC-3+EPIC-4
step_type: single
coded_by: coder
coded_by_model: stealth/ox-alpha
coded_by_model_knowledge_cutoff: unbekannt
coded_at: 2026-08-22T23:40:00+02:00
code_commit_hash: 7b3b0284
status_after: done
blocker_category: n/a
---

# Result Step 004: Testfundament, gebatchte Test-Zuordnung & recommendedTestCommands

## Zusammenfassung

`TestCoverageScanner` hat eine neue Batch-API (`FindTestsForSymbolsAsync`),
die Projekte/Dokumente GENAU EINMAL iteriert, je Dokument SyntaxRoot und
SemanticModel genau einmal bezieht und dann gegen alle Ziele matcht; der
per-Symbol-Einstieg ist ein duenner Wrapper darauf (Signatur/Ergebnisform
unveraendert). Ausfuehrbare `dotnet test`-Befehle stammen jetzt aus dem
gemeinsamen `TestRecommendationBuilder` — genau ein deduplizierter Befehl je
Testprojekt mit Filter ueber die Vereinigung der Trefferklassen, ordinal
sortiert. Der neue optionale `DiffImpactCounters`-Zaehlerkanal (gereicht ueber
den bestehenden `DiffAnalysisRequest`, Null-Verhalten ohne Uebergabe) weist
GitRuns/TestSolutionScans instrumentiert nach; LintRuns folgt mit der
Violations-Stufe. Die neutrale Mehrprojekt-Fixture (`App.Core`→`App`→
`App.Tests`, public PlaceAsync mit Call-Sites + private LogInternal ohne)
liegt im TestKit und speist fuenf neue FastTests sowie einen Integrationstest,
der den zusammengesetzten change-context-Lauf mit echtem Git misst:
GitRuns==1 UND TestSolutionScans==1 bei N=2 Symbolen.

## Geänderte Dateien

- `src/AiNetLinter/Core/TestCoverageBatchScan.cs` (neu) — partial
  `TestCoverageScanner`: Batch-API + Kern (NormalizeTargets,
  ScanAllTestDocumentsAsync, ScanDocumentAgainstTargetsAsync, BuildBatchResult);
  zaehlt optional einen Solution-Scan pro Aufruf, nie je Symbol.
- `src/AiNetLinter/Core/TestCoverageScanner.cs` — Wrapper delegiert an die
  Batch-API; alte Projekt-/Dokument-Loops entfernt; gemeinsames
  `BuildFileCoverageResult` (Parameter-Record `LoadedTestDocument`);
  Batch-Records (`TestCoverageBatchScanResult`,
  `TestCoverageBatchSymbolResult`) am Dateiende ergaenzt.
- `src/AiNetLinter/Core/DiffImpactAnalysisModels.cs` — `DiffImpactCounters`
  (int-Felder, Interlocked-Inkremente an den Stufen) + XML-Doc.
- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` — `DiffAnalysisRequest` um
  optionale `Counters` ergaenzt; `RunAnalysisAsync` internal (instrumentierte
  Laeufe gehen denselben Pfad), Git-Zaehler unmittelbar vor dem einzigen
  `RunGitDiff`-Aufruf.
- `src/AiNetLinter/Mcp/Tools/TestContext/TestRecommendationBuilder.cs` (neu) —
  Command-Wahrheit: Befehl je Testprojekt, Filter = Vereinigung der
  Klassennamen, ordinal deterministisch.
- `src/AiNetLinter/Mcp/Tools/TestContext/GetTestContextTool.cs` —
  `BuildRecommendedCommands` private→internal, schmale Weiterleitung auf den
  Builder; Logik geloescht.
- `src/AiNetLinter.TestKit/ChangeContextScenarioFactory.cs` (neu) — neutrale
  In-Memory-Fixture: Specs/Quelldateien (original + geaenderte Bodies),
  virtuelle und root-basierte Solution-Variante, Symbol-Handles
  (`ResolveSymbolsAsync`), synthetische Hunk-Ranges (Body-Zeile 7 je Datei),
  Konstanten fuer erwartete Trefferklassen.
- `src/AiNetLinter.TestKit/ChangeContextScenarioSymbols.cs` (neu) —
  `ScenarioSymbols`-Record auf Namespace-Ebene (BanPublicNestedTypes).
- `src/AiNetLinter.FastTests/Core/TestCoverageBatchScannerTests.cs` (neu) —
  5 Unit-Tests: beide Ziele aus einem Scan (Counter==1) mit getrennten
  Evidenzarten; private Methode ohne Call-Sites erhaelt Naming-Convention-
  Treffer; Wrapper≡Batch feldidentisch; Command-Dedup je Projekt
  (exakter Befehlsstring, doppelt berechnet); leere Zielliste zaehlt keinen Scan.
- `src/AiNetLinter.IntegrationTests/Core/DiffImpactAnalyzerOnceOnlyTests.cs`
  (neu) — zusammengesetzter Lauf auf echtem Mini-Git-Workspace:
  GitRuns==1, TestSolutionScans==1 (nicht N), LintRuns bleibt 0, beide
  DisplayName-Eintraege, 3 distinct Testfiles.
- `src/AiNetLinter.IntegrationTests/Fixtures/FixtureWorkspaces.cs` —
  `ChangeContextMiniWorkspace` (Temp-Git-Repo mit Szenario-Quellen,
  uncommittete Body-Aenderung beider Methoden); `RunGit` zum dateiweiten
  `FixtureGit`-Helper extrahiert (auch von GitImpactMini genutzt);
  Attribut-Normalisierung als `FixtureFileAttributes.NormalizeTree` geteilt.

## Commit

- **Code-Commit-Hash:** `7b3b0284`
- **Message:**
  ```
  feat: Batch-Testzuordnung [03_get-impact-zum-diff-kontext-erweitern]

  Der TestCoverageScanner ordnet in der neuen Batch-API alle Ziel-Symbole in
  EINEM Solution-Durchlauf zu ... (Body gekuerzt)

  Refs: tasks/mcp-server-weiterentwicklung/03_get-impact-zum-diff-kontext-erweitern/step-004
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress       → grün (1605 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (348 Tests, 0 Fehler)
```

Schnelliteration waehrend der Entwicklung: nur die neuen Testklassen per
FullyQualifiedName-Filter; Dogfood-Lint (`--config rules.json`) und beide
Gates erneut NACH dem letzten Refactor ausgefuehrt.

## Abweichungen vom Plan

1. **Fixture im TestKit statt `FastTests/Fixtures`; OnceOnly-Test in
   IntegrationTests statt FastTests:** `FastTestsDependencyGuardTests` verbietet
   eine `System.Diagnostics.Process`-TypeRef in FastTests.dll (und TestKit.dll) —
   der GitRuns-Nachweis braucht aber einen echten Git-Subprozess ueber den
   Analyzer-Pfad. Deshalb: Factory (rein in-memory, ohne Process) ins gemeinsam
   referenzierte TestKit verschoben; der zusammengesetzte Counter-Test laeuft im
   Integration-Projekt (Process dort erlaubt). Die Plan-Aussage „ein Lauf, beide
   Counter" ist bewahrt; die FastTests-Batch-Tests decken TestSolutionScans ab.
2. **Git-Mini-Workspace in `FixtureWorkspaces.cs` statt eigener Fixtures-Datei:**
   `McpProcessArchitectureGuardTests` zaehlt `Process.Start(`-Callsites
   DATEIWEISE (genau 3) und pinnt die Owner-Dateien. Neuer Workspace therefore
   in der gepinnten Owner-Datei; `RunGit` zusaetzlich zum geteilten
   `FixtureGit`-Helper extrahiert (DRY mit GitImpactMini, Verhalten unveraendert).
3. **Neue Dateien anders verteilt:** `TestCoverageBatchScanModels.cs` gibt es
   nicht (Records in `TestCoverageScanner.cs`), `DiffImpactCounters.cs` nicht
   eigenstaendig (in `DiffImpactAnalysisModels.cs`), `TestRecommendationBuilder`
   im TestContext-Ordner (vom Plan ausdruecklich erlaubt) statt Core. Grund:
   `MaxDirectoryChildren`=30 fuer `src/AiNetLinter/Core` — mit vier neuen Dateien
   bei 33; nach Konsolidierung exakt 30.
4. **Counter als oeffentliche int-Felder statt Methoden-Container:** die
   geplante „Interlocked-Zaehler"-Klasse mit Count*-Methoden loeste
   `AvoidExcessiveMiddleMen` (100 % Weiterleitungsverhaeltnis) aus. Jetzt Felder;
   Interlocked.Increment(ref …) sitzt an den zwei Produktions-Stufen
   (Analyzer-Kern vor `RunGitDiff`, Batch-Kern nach Leerpruefung).
5. **Interner Einstieg heisst `FindTestsForSymbolsCoreAsync`** (statt einer
   Counters-Ueberladung des oeffentlichen Namens — die waere wegen
   optionaler Parameter mehrdeutig gewesen, CS0121) und **`RunAnalysisAsync`
   ist internal** statt neuer Request-basierter Eintrittspunkte — keine
   Middle-Man-Wrapper, ≤4-Parameter-Grenze gewahrt.
6. **Command-Formataenderung ist beabsichtigt und vertraglich (Plan):** je
   Testprojekt EIN Befehl mit `|-Filter` statt je Klasse einer; der einzige
   Bestands-Assert dazu (`GetTestContextToolTests`, Substring) bleibt ohne
   Anpassung gruen.

## Beobachtungen

- **Match-Schleife ist O(Ziele) je Dokument:** Parse/Semantikmodell passieren
  exakt einmal je Dokument (Konzept-Anforderung), aber `AnalyzeDocument` laeuft
  pro Ziel auf dem geteilten Root/Model — exakt die vom Plan vorgegebene Form
  („dann gegen ALLE Ziele matchen"). Ein echter Ein-Pass-Match gegen alle Ziele
  wuerde einen Umbau der Evidenzlogik bedeuten; Performancegewinn erst bei
  vielen Zielen (Cap 100) relevant. Falls der Kritiker das als Tech-Debt fuehren
  will: gern.
- `GetTestContextTool.BuildRecommendedCommands` ist jetzt reiner Forwarder —
  plangemaess, aber philosophisch nahe an AvoidExcessiveMiddleMen. Wenn EPIC-6
  direkt auf den Builder geht, kann der Forwarder entfallen (dann auch der
  Umweg ueber das Tool-Interno).
- Erster Dogfood-Lauf machte regelrechte Verstoesse meiner neuen Dateien
  sichtbar (MiddleMen, NestedType, DuplicateCode, MaxDirectoryChildren) — alle
  im selben Zug an der Ursache behoben, keine Suppression ausser dem
  bestehenden BanBlockingTaskAccess-Pragma am gezogenen `FixtureGit.Run`.
- Arbeitskopie enthielt fremde Aenderungen (`task-state.md` modified) — nicht
  angeruehrt, nicht committet; mein add war strikt file-weise.

## Bekannte Unschärfen

- **Wrapper-Identitaet** (`FindTestsForSymbolAsync` ≡ bisheriges Verhalten):
  belegt durch alle unangetastet gruenen Bestands-Tests (Scanner-, 
  get_test_context-/get_feature_context-Tests, Dogfood) plus den neuen
  Feldvergleichstest Wrapper↔Batch — nicht durch einen direkten Alt/Neu-Diff.
  Die Treffer-Sortierung nutzt weiterhin `ThenBy(FilePath)` mit
  Kultur-Default (identisch zum Altcode); nur die solutionweite Dedup-Info
  sortiert bewusst ordinal.
- Leere Zielliste fuehrt zu keinem Scan und inkrementiert den Counter NICHT
  (meine Lesart von „Scan einmal pro Lauf"); nicht explizit im Plan geregelt,
  per Test gepinnt.
- LintRuns wird nur als Feld gefuehrt (bleibt 0, per Test gepinnt); echte
  Inkrement-Stelle folgt mit der Violations-Stufe — bekannte Plan-Ausnahme.
- Die Zeilenkonstante `PlaceAsyncBodyLine/LogInternalBodyLine = 7` haengt an
  den Fixture-Quelltexten; verschiebt jemand die Methodenzeilen, schlagen die
  synthetischen Hunk-Tests lautstark (bewusst kein dynamisches Berechnen).
