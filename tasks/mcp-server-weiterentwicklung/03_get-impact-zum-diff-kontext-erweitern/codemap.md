---
task: 03_get-impact-zum-diff-kontext-erweitern
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-23
---

# CodeMap: 03_get-impact-zum-diff-kontext-erweitern

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`<task-dir>` gelöscht, kein projektweites Artefakt. Enthält **nur**, was
für diesen Task relevant ist (Module/Dateien/Bereiche, die ein Step
tatsächlich berührt hat oder für die Planung des nächsten Steps
gebraucht wird) — kein Anspruch auf vollständige Projektabdeckung.

**Pointer-Prinzip — wie Regel-Index (`roadmap.md`) und Tech-Debt-Index
(`tech-debt.md`):** Jeder Eintrag ist Ort + **ein Satz**, was dort ist
und wozu es für diesen Task relevant ist — keine Verhaltensbeschreibung,
kein „wie funktioniert das im Detail". Verhaltensbehauptungen veralten,
Ortsangaben kaum. Wer mehr wissen muss, liest die Datei selbst nach —
das ersetzt die Map nie, sie beschleunigt nur das Finden.

**Warum das trotzdem verlässlich bleibt (anders als generische Doku):**
Der gesamte Loop läuft strikt seriell — genau ein Subagent gleichzeitig
(drift-loop `spec.md` §6). Zwischen einem Coder-Update und dem nächsten
Lesezugriff kann sich am Code strukturell nichts geändert haben, was hier
nicht auch eingetragen wurde. Die Map ist also, solange sie gepflegt wird,
tatsächlich aktuell — kein Snapshot mit Drift-Risiko. **Schritt 2 im
Step-Modus des Planers („tatsächlichen Projektzustand lesen",
`spec.md` §7.2) bleibt trotzdem Pflicht** — die Map sagt *wo* nachschauen,
ersetzt nie das Nachschauen selbst.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem
  Grobüberblick, den er beim Ableiten der Epics ohnehin über den
  Bestandscode gewinnt (planer-SKILL Roadmap-Modus Schritt 1).
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich
  angelegte oder geänderte Module, **vor** dem Doku-Commit
  (coder-SKILL Schritt 6a).
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen,
  ergänzt neue Bereiche, die er beim Lesen des Ist-Zustands entdeckt.
  Zusätzlich Grundlage für den Anti-Loop-Check (siehe unten).
- **Kritiker:** prüft stichprobenartig, ob die Map dem tatsächlichen Diff
  entspricht (Teil von Ebene 1, Plan-Erfüllung) — schreibt selbst nur bei
  offensichtlicher Lücke/Fehler nach, ist aber nicht Haupt-Pfleger.

## Anti-Loop-Nutzen

Bevor der Planer im Step-Modus einen neuen Step plant, gleicht er sein
Vorhaben gegen die hier verzeichneten, bereits getroffenen Entscheidungen
ab. Widerspricht der neue Plan erkennbar einem hier festgehaltenen,
bereits umgesetzten Stand (z. B. ein späterer Step würde zurückdrehen, was ein
früherer Schritt laut Map bewusst so gebaut hat): entweder im neuen Step-Plan explizit als
Erweiterung begründen, oder den alten Eintrag hier als „obsolet —
<Grund>" markieren (nicht löschen) — nie stillschweigend widersprechen.
Das verhindert kein Kreisen zu 100 %, macht ein Hin-und-Her aber
wenigstens sichtbar und begründungspflichtig statt stillschweigend.

## Karte

Initialbefüllung aus dem Grobüberblick des Roadmap-Modus; noch keine
Steps abgeschlossen, daher überall „(zuletzt: roadmap)".

Produktionscode:

- **`src/AiNetLinter/Core/DiffSymbolScanner.cs`** (neu in step-003) —
  breiter Diff-Symbolscanner: Enum `DiffSymbolScope` (`Callers`/
  `ChangeContext`), Kandidatensammlung je Scope, Range-Überlappung,
  Innerste-Deklarations-Regel, Accessibility-Filter, knotenbasierte
  Entry-Bildung und artabhängige Displaynames; beide Scopes laufen durch
  dieselbe Pipeline. (zuletzt: step-003)
- **`src/AiNetLinter/Core/DiffImpactAnalyzer.cs`** — Git-Diff-Auswertung
  (`RunGitDiff`, Range-Parsing `ParseGitDiffHunkRanges`, DRY-Expansion in
  `ParseGitDiffHunks`) bis zu den Call-Sites; seit step-003 zwei klar
  benannte Eintrittspunkte (`AnalyzeDiffAsync` = Callers,
  `AnalyzeChangeContextAsync` = ChangeContext) auf dem gemeinsamen
  Request-Record `DiffAnalysisRequest`; Symbolermittlung delegiert an
  `DiffSymbolScanner`; `CreateChangedSymbolEntry` mit knotenbasierter
  Location-Überladung. Seit step-004 trägt `DiffAnalysisRequest` optionale
  `Counters`; der gemeinsame Kern `RunAnalysisAsync` ist internal und
  inkrementiert GitRuns unmittelbar vor dem einzigen Git-Aufruf.
  Seit step-008 kappet ein optionaler `ChangedSymbolCap` am Request die
  geaenderten Symbole im Kern VOR der Referenz-Stufe (`ApplyChangedSymbolCap`,
  deterministische Sortierung Projekt→Datei→Zeile→ID, No-op ohne wirksamen
  Cap); das Ergebnis traegt `ChangedSymbolsTotal` + `ShownSymbolHandles`.
  (zuletzt: step-008)
- **`src/AiNetLinter/Core/DiffImpactAnalysisModels.cs`** (neu in
  step-002) — Records `HunkRange`, `ChangedFileRange`,
  `ChangedSymbolEntry`, `DiffImpactAnalysis`; referenziert bewusst
  `AiNetLinter.Mcp.Tools.SymbolGraph` (Monolith, keine Verschiebung der
  TransitiveCallGraphModels). FilePath-Bedeutungen im XML-Doc;
  SymbolId-Vertrag nennt seit step-003 den lokalen-Funktions-Sonderfall.
  Seit step-004 zusaetzlich `DiffImpactCounters` (GitRuns/TestSolutionScans/
  LintRuns-Felder, Interlocked-Inkremente an den Stufen, Null-Verhalten ohne
  Uebergabe); seit step-007 hat jedes Feld seine benannte Produktions-
  Inkrement-Stelle — LintRuns in der Violations-Stufe. Seit step-008
  traegt `DiffImpactAnalysis` additive optionale Member
  `ChangedSymbolsTotal`/`ShownSymbolHandles`. (zuletzt: step-008)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs`** —
  flache BFS-Aufrufer-Traversierung für `find_references` und den
  `get_impact`-Symbol-Branch; depth>1-Korrektur umgesetzt:
  `EnqueueChildrenAsync` enqueued je Referenzlocation den einschließenden
  Aufrufer über den gemeinsamen Helper `ResolveEnclosingMemberAsync`
  (internal, wird auch vom Caller-Baum genutzt); `GetStableSymbolId`
  internal und gemeinsame Quelle der stabilen Symbol-IDs — seit step-003
  mit deterministischem `#lf:`-Sonderfall für lokale Funktionen
  (TD-002-Auflösung IN der gemeinsamen Quelle). Nach dem Split des
  Tree-Paths in `CallGraphTreeBuilder` unter dem MaxLineCount-Limit.
  (zuletzt: step-003)
- **`src/AiNetLinter/Core/RoslynSymbolExtensions.cs`** — zentrale
  ISymbol-Extensions: `NormalizeToOwningMember` (mappt Accessoren auf
  Property/Event, lokale Funktionen aber NICHT hoch — deshalb können
  LF-Aufrufstellen im Traversal als Reached-From-Knoten auftauchen und
  tragen seit step-003 eindeutige `#lf:`-IDs) und `TryGetDocCommentId`
  (fehlerresistente Doc-ID). Datei selbst unverändert. (zuletzt:
  step-001; step-003 nur Verhaltenskontext)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTreeBuilder.cs`** (neu
  in step-001) — Caller-Tree-Aufbau für `get_call_tree`
  (`BuildTreeAsync`, Tree-Konstanten, Gruppierungs-/Formatierhelfer),
  aus `CallGraphTraversal` herausgelöst; Verhalten unverändert, nutzt
  `CallGraphTraversal.ResolveEnclosingMemberAsync`/`FormatSymbolName`.
  (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`** —
  `get_impact`-Dispatch zwischen Git- und Symbol-Branch inkl.
  `GetImpactInput`-Record (seit step-008 mit detailLevel/maxChangedSymbols/
  maxTestsPerSymbol); Hint-Parität im Symbol-Branch (`McpSufficiencyHints.
  Append`). Seit step-008 Hauptort des `detailLevel=change-context`-
  Vertrags: INVALID_ARGUMENT-Validierung vor dem Dispatch, Cap-Normali-
  sierung, Verdrahtung von Analyzer-/Batch-Test-/Violations-Stufe,
  kompakte Textform mit Sufficiency-Hint bzw. Meta-Zeile; ct-Bindung
  (Audit D.7) geschlossen. (zuletzt: step-008)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/ChangeContextResponseModels.cs`**
  (neu in step-008) — Antwort-DTOs des change-context-Vertrags mit den
  vertraglich exakten JSON-Feldnamen (`ChangeContextPayload` samt
  Sub-Records), `ChangeContextContract` (Default-/Cap-Konstanten + Clamp)
  und `ChangeContextResponseMapper` (Mapping der drei Stufen → Payload,
  Test-Kappung je Symbol, Completeness-Spiegelung). (zuletzt: step-008)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs`** —
  Referenz-Tool mit `ResolveSymbolAsync` und angehängtem Sufficiency-Hint;
  Paritäts-Vorbild für den `GetImpactTool`-Symbol-Branch (EPIC-1).
  (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphModels.cs`** —
  strukturiertes `ReferenceTraversalResult` (callSites + completeness) aus
  der transitive-Ausgaben-Aufgabe; Wiederverwendungsquelle für die
  Call-Sites in `change-context` (EPIC-2/EPIC-6). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphFormatter.cs`** —
  Formatter derselben strukturierten Traversal-Antwort; Muster für die
  kompakte Textzusammenfassung von `change-context` (EPIC-6). (zuletzt:
  roadmap)
- **`src/AiNetLinter/Core/TestCoverageScanner.cs`** — statische
  Test-Zuordnung; seit step-004 duenner per-Symbol-Wrapper auf die Batch-API,
  gemeinsame Treffer-Nachbearbeitung (`BuildFileCoverageResult`) und die
  Batch-Ergebnis-Records (`TestCoverageBatchScanResult`,
  `TestCoverageBatchSymbolResult`) am Dateiende. (zuletzt: step-004)
- **`src/AiNetLinter/Core/TestCoverageBatchScan.cs`** (neu in step-004) —
  gebatchte Scanner-Haelfte (partial `TestCoverageScanner`):
  `FindTestsForSymbolsAsync` (oeffentlich) / `FindTestsForSymbolsCoreAsync`
  (internal, optionaler `DiffImpactCounters`-Zaehler); Projekte/Dokumente
  genau einmal je Aufruf, Root/Model je Dokument einmal, Match gegen alle
  Ziele; solutionweite Dedup-Info (DistinctTestFilePaths, ordinal).
  (zuletzt: step-004)
- **`src/AiNetLinter/Mcp/Tools/TestContext/`** — `get_test_context`-Tool;
  seit step-004 mit `TestRecommendationBuilder.cs` (neu): gemeinsame Quelle
  der ausfuehrbaren `dotnet test`-Befehle (EIN Befehl je Testprojekt,
  Filter = Vereinigung der Trefferklassen, ordinal sortiert) fuer das Tool
  und `recommendedTestCommands`; `GetTestContextTool.BuildRecommendedCommands`
  ist nur noch Weiterleitung. (zuletzt: step-004)
- **`src/AiNetLinter/Mcp/Tools/Analysis/DiffViolationScanner.cs`** (neu in
  step-007) — interne Violations-Stufe des diff-bezogenen Pfads:
  `CollectAsync(DiffViolationScanRequest)` führt den Linter GENAU EINMAL
  solutionweit über den gemeinsamen Helper aus und inkrementiert LintRuns
  unmittelbar davor (Skip-empty: leerer Input → kein Lauf, kein Inkrement);
  pure `FilterDiffRelevantViolations` behält Hunk-Treffer ∪
  Spannen-Treffer GEZEIGTER Symbole (Dedup, Sortierung analog
  Scope-Sortierung) und normalisiert zentral die drei Pfadsemantiken
  (Hunks repo-root-relativ, Symbole solution-relativ, Violations absolut).
  EPIC-6 bindet diese Stufe an den Antwortvertrag. (zuletzt: step-007)
- **`src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`** —
  Violations-Ermittlung (solutionweit/scoped) für `get_violations`; Basis
  für „Linter genau einmal“ plus diffbezogene Filterung auf Hunks/
  Symbolspannen (EPIC-5). Seit step-007 sitzt hier die EINZIGE
  LinterEngine-Beschaffung als gemeinsamer Helper `RunSolutionLintAsync`
  (konkreter `Config`-Downcast, `noCache: true`; geteilt von `get_violations`
  und der Violations-Stufe) plus das Result-Muster (`GetViolationsResult`
  mit IsMalfunction/IsTruncated). (zuletzt: step-007)
- **`src/AiNetLinter/Mcp/Tools/Analysis/ViolationScopeFilter.cs`** —
  gemeinsame Scope-Filter-/Sortierlogik (Datei→Projekt-Map, stabile
  FilePath→Zeile→Regel-Sortierung) für `get_violations`/`pattern_detect`;
  Ordnungs- und Pfadvergleichs-Muster für die diffbezogene
  Violation-Filterung (EPIC-5). (zuletzt: step-007-Planung)
- **`src/AiNetLinter/Core/LinterEngine.cs`** — `RunAsync(Solution, noCache,
  cacheTtlMinutes, ct)` als einzige Lint-Ausführungsquelle; Wiederverwendungs-
  punkt für „Linter genau einmal“ in der internen Violations-Stufe (EPIC-5).
  (zuletzt: step-007-Planung)
- **`src/AiNetLinter/Mcp/McpToolResults.cs`** — Antwort-Helper (`Text<T>`
  mit structuredContent, `Recoverable`, `InvalidArgument`); Formatkanal
  für die strukturierte `change-context`-Antwort (EPIC-6). (zuletzt:
  roadmap)
- **`src/AiNetLinter/Mcp/McpSufficiencyHints.cs`** — Sufficiency-Hint-
  Logik (Vollständigkeits-Marker am Textende); anzuhängen im
  Symbol-Branch von `GetImpactTool` (EPIC-1). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/McpTruncation.cs`** — einheitliche Meta-Zeilen
  und Listen-Trunkierung; relevant für Caps und „höchstens gekappte
  Top-Einträge" im Text (EPIC-6). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`** — Registrierung
  der Symbolgraph-Tools inkl. `get_impact` (Z.111 ff.) — Kontrollstelle für
  die DoD-Regel „kein neues MCP-Tool wurde registriert"; neuer Vertrag
  läuft über den bestehenden Eintrag — dieser ist seit step-008 ADDITIV
  erweitert (drei neue Parameter + Beschreibungstext), kein neues Tool.
  (zuletzt: step-008)

Doku:

- **`Docs/agent-api.md`** — Tool-Referenz der Agent-API (u. a.
  `get_impact`/`find_references`-Verträge, Structured-Output-Schemata,
  Trunkierungs-Format); Doku-Ziel dieses Tasks: JSON-Feldnamen exakt,
  Verhaltenskorrektur depth>1 ausweisen, Grenzen dokumentieren (EPIC-7).
  (zuletzt: roadmap)
- **`README.md`** — MCP-Tool-Tabelle mit Zeile zu `get_impact`; bei
  Vertragsänderung mitzupflegen (EPIC-7). (zuletzt: roadmap)

Tests:

- **`src/AiNetLinter.FastTests/Fixtures/McpInMemoryTestContext.cs`** —
  In-memory-Roslyn-Solution-Fixture mit `CreateScenario(ProjectSpec)` für
  Ad-hoc-Miniszenarien (mehrzeilige Inline-Quelltexte); Wiederverwendungs-
  quelle für Aufruferketten-Tests (EPIC-1) und die neutrale
  Konzept-Fixture (EPIC-3). (zuletzt: planer, step-001-Planung)
- **`src/AiNetLinter.FastTests/Mcp/Tools/CallTree/CallGraphTraversalTests.cs`**
  — Unit-Tests der Traversierung; `ExpandAsync_Depth2_FormatsWithDepthMarker`
  wurde als schwache Assertion bewusst gestärkt (Kettenabschluss auf der
  Default-Fixture), neu dazu gekommen ist der echte Ketten-Nachweis
  `ExpandAsync_Depth2_RealCallerChain_ResolvesBothLevels` über
  `CreateScenario` (EPIC-1, Audit F). (zuletzt: step-001)
- **`src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/FindReferencesToolTests.cs`**
  — Tool-Level-Tests von `find_references`; erweitert um
  `ExecuteAsync_Depth2_RealCallerChain_ReturnsBothLevels` (echte
  Aufruferkette A<-B<-C auf Ad-hoc-Szenario, EPIC-1). (zuletzt: step-001)
- **`src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/GetImpactToolTests.cs`**
  — Unit-Tests des `get_impact`-Dispatchs/der Antwortform; erweitert um
  Symbol-Branch-Kettentest (`Depth2RealCallerChain`) und die
  Sufficiency-Hint-Parität (vollständig → Hinweis, trunkiert → Meta-Zeile,
  EPIC-1); Zielort für `change-context`-Vertrag und
  `INVALID_ARGUMENT`-Fälle (EPIC-6); seit step-008 die Vertragstests des
  change-context-Modus (INVALID_ARGUMENT-Fälle, Cap-Normalisierung,
  StructuredContent-Feldnamen inkl. accessibility als String,
  Komplettmetadaten, testsTruncated, Textform). (zuletzt: step-008)
- **`src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/ChangeContextResponseModelTests.cs`**
  (neu in step-008) — reine Mapping-/Vertragstests des
  `ChangeContextResponseMapper` bzw. der DTOs: Feldnamen-Pinning,
  Accessibility-String-Mapping, Completeness-Spiegelung, Test-Kappung je
  Symbol, leeres Payload. (zuletzt: step-008)
- **`src/AiNetLinter.FastTests/Core/DiffImpactAnalyzerTests.cs`** —
  Unit-Tests zu Hunks/Symbolermittlung; seit step-002 kompakte
  Range-Parsing-/Expansions-Äquivalenz-, `ChangedSymbolEntry`-Mapping-
  (inkl. lokale Funktion) und Wrapper-Mapping-Tests. (zuletzt: step-002)
- **`src/AiNetLinter.FastTests/Core/DiffImpactAnalyzerBroadScopeTests.cs`**
  (neu in step-003) — Unit-Tests des breiten Scannerpfads über
  `CreateScenario` + synthetische Hunk-Ranges: Differential Callers vs.
  ChangeContext, Innerste-Deklaration, Property/Feld/Event, partielle Typen,
  `#lf:`-ID-Pinning und Displayname-Verträge. (zuletzt: step-003)
- **`src/AiNetLinter.FastTests/Core/TestCoverageScannerTests.cs`** —
  Unit-Tests der per-Symbol-Testzuordnung (Wrapper; unveraendert gruen).
  (zuletzt: roadmap)
- **`src/AiNetLinter.FastTests/Core/TestCoverageBatchScannerTests.cs`** (neu
  in step-004) — Batch-Zuordnung auf der ChangeContextScenarioFactory: beide
  Ziele aus einem Scan (`TestSolutionScans==1`) mit getrennten Evidenzarten,
  private Methode ohne Call-Sites per Naming Convention, Wrapper≡Batch,
  Command-Dedup je Testprojekt, leere Zielliste ohne Scan.
  (zuletzt: step-004)
- **`src/AiNetLinter.FastTests/Mcp/Tools/Analysis/DiffViolationFilterTests.cs`**
  (neu in step-007) — 7 Unit-Tests der diffbezogenen Violations-Stufe:
  Hunk-/Spannen-/Randwert-Semantik inkl. `LineCount=0`-Hunks, zentrale
  Pfadnormalisierung (repo-relativ ↔ solution-relativ ↔ absolut, Trenner/
  Case tolerant), Dedup bei Doppelbedingung + deterministische Sortierung
  sowie Stage-Tests mit echter LinterEngine auf der ScenarioFactory
  (LintRuns==1; leerer Input → kein Lint/Inkrement). (zuletzt: step-007)
- **`src/AiNetLinter.IntegrationTests/Core/DiffImpactAnalyzerOnceOnlyTests.cs`**
  (neu in step-004) — zusammengesetzter change-context-Lauf auf dem
  `ChangeContextMiniWorkspace`: seit step-007 weist er das volle Tripel
  GitRuns==1 && TestSolutionScans==1 && LintRuns==1 nach (Violations-Stufe
  läuft mit DEMSELBEN Counters-Objekt über echte Workspace-Hunks/-Symbole).
  (zuletzt: step-007)
- **`src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs`**
  — Integrationstests von `get_impact` im echten Server-Kontext; seit
  step-002 direkter `AnalyzeDiffAsync`-Ende-zu-Ende-Test auf der
  `GitImpactMiniFixtureWorkspace`, seit step-003 zusätzlich
  `AnalyzeChangeContextAsync` an geänderter privater Methode (ohne Call-Sites,
  callers-Wrapper omitiert sie). Seit step-008 E2E
  `detailLevel="change-context"` auf `ChangeContextMiniWorkspace`;
  callers-Subprozess-Tests unangetastet. (zuletzt: step-008)
- **`src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandGetImpactTests.cs`**
  — Subprozess-/Protokoll-Level-Tests von `get_impact`; Absicherung der
  Abwärtskompatibilität des `callers`-Modus (EPIC-3/EPIC-6). (zuletzt:
  roadmap)
- **`src/AiNetLinter.IntegrationTests/Fixtures/FixtureWorkspaces.cs`** —
  Disposable Fixture-Workspaces inkl. `GitImpactMiniFixtureWorkspace`
  (echtes Temp-Git-Repo mit Initial-Commit; Basis der bestehenden
  get_impact-Git-Branch-Integrationstests); seit step-003 zusätzlich
  `ChangeCalculatorNormalizeBodyWithoutCommitting()` für die Änderung einer
  privaten Methode. Seit step-004: `ChangeContextMiniWorkspace` (Temp-Git-Repo
  mit den Szenario-Quellen der ChangeContextScenarioFactory, uncommittete
  Body-Aenderung beider Methoden), geteilter `FixtureGit.Run`-Helper und
  `FixtureFileAttributes.NormalizeTree`. Wiederverwendungsquelle für
  Analyzer-Ergebnisobjekt-Tests (EPIC-2) und die instrumentierte
  Einmal-Ausführungs-Messung (EPIC-3). (zuletzt: step-004)
- **`src/AiNetLinter.TestKit/ChangeContextScenarioFactory.cs`** (neu in
  step-004) — neutrale Mehrprojekt-Fixture fuer die change-context-Kette:
  `App.Core`→`App`→`App.Tests`, zwei geaenderte Methoden in zwei Dateien
  (public `PlaceAsync` mit Call-Sites, private `LogInternal` ohne),
  Quelldatei-Konstanten original/geaendert, virtuelle und root-basierte
  Solution-Variante, Symbol-Handles (`ResolveSymbolsAsync`) und synthetische
  Hunk-Ranges; Grundlage der Batch-/Counter-/Konzept-Tests dieses UND der
  folgenden Steps. (zuletzt: step-004)
- **`src/AiNetLinter.TestKit/ChangeContextScenarioSymbols.cs`** (neu in
  step-004) — `ScenarioSymbols`-Record (PlaceAsync + LogInternal) auf
  Namespace-Ebene. (zuletzt: step-004)
- **`tests/Fixtures/GitImpactMini/`** — Fixture-Vorlage des Mini-Git-Repos,
  die die Workspace-Klassen kopieren; `Calculator.cs` trägt seit step-003
  neben `Add` eine private, nie aufgerufene Methode `Normalize` (Teil des
  Initial-Commits), damit Diffs bestehende private Methoden treffen können.
  (zuletzt: step-003)
