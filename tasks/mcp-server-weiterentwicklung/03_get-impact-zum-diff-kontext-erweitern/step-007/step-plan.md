---
status: done
type: step-plan
task: 03_get-impact-zum-diff-kontext-erweitern
step: 007
corrects: null
title: "Solutionweite Violations & diffbezogene Filterung (interne Stufe)"
epic: EPIC-5
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: stealth/ox-alpha
created_by_model_knowledge_cutoff: unbekannt
created_at: 2026-08-23T09:27:09+02:00
related_to: ["step-002", "step-004"]
---

# Step 007: Solutionweite Violations & diffbezogene Filterung (interne Stufe)

## Bezug

- **Task:** `03_get-impact-zum-diff-kontext-erweitern`
- **Epic:** `EPIC-5` aus `roadmap.md` — offen ist die komplette interne
  Violations-Stufe: solutionweite Berechnung („Linter genau einmal“),
  diffbezogene Filterung, LintRuns-Inkrement. Der Tool-Anschluss
  (`detailLevel=change-context`, Caps, Completeness, INVALID_ARGUMENT)
  bleibt bewusst EPIC-6 und wird hier NICHT angetastet.
- **Konzept-Referenz:** `Konzept.md` §Filterregeln für Violations,
  §Performance- und Größenregeln („Linter genau einmal“), §Tests
  („Nur eine Violation innerhalb Hunk/Symbolspanne wird aufgenommen;
  benachbarte irrelevante Violation derselben Datei nicht“ +
  „Instrumentierter Test/Counter weist nach: Git einmal, Testsolution
  einmal, Linter einmal“).

## Aktueller Projektzustand (JIT-Kontext)

- **`GetViolationsScanner.BuildViolationsTextAsync(GetViolationsScannerParameters)`**
  (`Mcp/Tools/Analysis/GetViolationsScanner.cs`) beschafft die Lint-Ergebnisse
  bereits genau richtig: `new LinterEngine((Config)config, null, null, console,
  null)` + `engine.RunAsync(solution, noCache: true, 0, ct)` — ein
  solutionweiter Lauf. Danach kommen aber Scope-Filter, Trunkierung,
  Snippet-Anreicherung und Report-Format (`GetViolationsResult` mit
  `IsMalfunction`-Flag bei unerwarteter Exception, catch non-OCE). Die
  Engine-Beschaffung ist der Wiederverwendungs-Punkt; die Filterregeln von
  `get_violations` (scopeFilter) sind NICHT die gesuchten Diff-Regeln.
- **`DiffImpactAnalyzer.RunAnalysisAsync`** (internal, gemeinsamer Kern hinter
  `AnalyzeDiffAsync`/`AnalyzeChangeContextAsync`, step-002..004) nimmt über
  `DiffAnalysisRequest` einen optionalen `DiffImpactCounters`-Kanal entgegen
  und inkrementiert `GitRuns` unmittelbar vor dem einzigen `RunGitDiff`.
- **`DiffImpactCounters`** (`Core/DiffImpactAnalysisModels.cs`, step-004) führt
  `GitRuns`/`TestSolutionScans`/`LintRuns` als öffentliche int-Felder mit
  Interlocked-Inkrementen an den Stufen. `LintRuns` hat **bewusst noch keine
  Inkrement-Stelle** — XML-Doc sagt „folgt mit der Violations-Stufe“, und der
  Integrationstest `DiffImpactAnalyzerOnceOnlyTests` pinnt aktuell
  `Assert.Equal(0, counters.LintRuns)` samt Begründungskommentar. Genau diese
  Lücke schließt dieser Step (beide Stellen werden ersetzt — dokumentierte
  Plan-Ausnahme von step-004, kein Symptom-Fix).
- **Skip-empty-Präzedenz:** `TestCoverageBatchScan.FindTestsForSymbolsCoreAsync`
  inkrementiert bei leerer Zielliste weder Scan noch Zähler (per Test gepinnt,
  step-004). Dieselbe Semantik übernimmt die Lint-Stufe: keine Hunks UND keine
  gezeigten Symbole → kein Lint-Lauf, kein Inkrement.
- **Verzeichnislimit:** `src/AiNetLinter/Core` liegt bei exakt 30 Dateien
  (= `MaxDirectoryChildren`-Limit, von step-004 bewusst konsolidiert);
  `src/AiNetLinter/Mcp/Tools/Analysis` hat 11 Dateien. Neue Produktionsdatei
  gehört daher nach `Mcp/Tools/Analysis` (dort sitzt auch die Lint-Wiederverwendung).
- **Testmuster vorhanden:** GetViolationsScanner hat keine direkten
  Determinismus-Unit-Tests (nur Live-Dogfood-Integrationstests), aber FastTests
  führen `new LinterEngine(config)` bereits auf In-Memory-Solutions mit
  Ad-hoc-Config (z. B. `MetricsConfig { MaxLineCount = 5 }` → deterministische
  Violation auf bekannter Zeile; Muster `FileFilterEvaluatorTests`,
  `MaxPartialClassFilesTests`). Die Fixture-Basis
  `ChangeContextMiniWorkspace`/`ChangeContextScenarioFactory` (step-004) steht
  für den zusammengesetzten Zähler-Nachweis bereit.
- **Anti-Loop-Check gegen CodeMap:** kein Widerspruch — die Map vermerkt
  `GetViolationsScanner` ausdrücklich als Basis für „Linter genau einmal plus
  diffbezogene Filterung (EPIC-5)“. Ergänzt wurden die beim JIT-Lesen
  entdeckten, bisher nicht verzeichneten Bereiche `ViolationScopeFilter.cs`
  (Ordnungs-/Pfadvergleichs-Muster) und `LinterEngine.RunAsync(Solution,…)`
  (Wiederverwendungspunkt).
- **Tech-Debt-Index:** TD-001 (`auto_fixable: nein`, FastTests-Server-Ergonomie)
  berührt diesen Bereich nicht sinnvoll anhängbar; TD-002 ist durch step-003
  in der gemeinsamen ID-Quelle gelöst. Keine Batch-Items.

## Intention

Nach diesem Step existiert eine interne, tool-unabhängige Violations-Stufe:
ein Aufruf führt den Linter **genau einmal** solutionweit aus und filtert das
Ergebnis rein diffbezogen — eine Violation ist relevant, wenn sie in einem
geänderten Hunk ODER in der Deklarationsspanne eines gezeigten geänderten
Symbols liegt; andere Violations derselben Datei bleiben außen vor. Der
LintRuns-Zähler bekommt seine einzige Produktions-Inkrement-Stelle, womit das
Konzept-DoD-Tripel (Git/Test/Lint je genau einmal) instrumentiert nachweisbar
ist. EPIC-6 muss danach nur noch die Stufe an den Antwortvertrag binden.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`

- **Was:** Die Lint-Beschaffung (konkreter `(Config)`-Downcast +
  `new LinterEngine(...)` + `RunAsync(solution, noCache: true, 0, ct)`) aus
  `BuildViolationsTextAsync` in einen kleinen internal Helper extrahieren
  (z. B. `RunSolutionLintAsync(Solution, ILinterEngineConfig, ILintConsole,
  CancellationToken)`, 4 Parameter) und beide Aufrufer darauf legen.
  Verhalten von `get_violations` byteidentisch — reiner DRY-Schnitt.
- **Warum:** Es darf künftig genau EINE Stelle geben, die die Engine
  konstruiert (Richtlinien §5 DRY); die neue Stufe und `get_violations`
  teilen sie sich.

### Datei 2: `src/AiNetLinter/Mcp/Tools/Analysis/DiffViolationScanner.cs` (neu)

- **Was:** Internal static Klasse mit zwei Ebenen:
  1. **Stufe (I/O):**
     `CollectAsync(DiffViolationScanRequest)` →
     `DiffViolationScanResult(IReadOnlyList<RuleViolation> Violations, bool
     IsMalfunction = false, string? Context = null)`.
     Request-Record (Parameter-Object wegen Grenzwert):
     `DiffViolationScanRequest(Solution, ILinterEngineConfig Config,
     ILintConsole Console, string RepositoryRoot, IReadOnlyList<ChangedFileRange>
     ChangedFiles, IReadOnlyList<ChangedSymbolEntry> ShownSymbols,
     DiffImpactCounters? Counters = null, CancellationToken CancellationToken =
     default)`.
     Ablauf: Skip-empty-Guard (beide Listen leer → leeres Ergebnis, KEIN
     Lint, KEIN Inkrement) → `Interlocked.Increment(ref counters.LintRuns)`
     unmittelbar vor dem einen Lint-Lauf über den gemeinsamen Helper →
     pure Filterung → deterministisch sortierte Liste. Unerwartete non-OCE-
     Exception des Laufs → `IsMalfunction=true` + `Context`-Message
     (Muster `GetViolationsResult`), keine Violations-Teilliste.
     `ShownSymbols` bewusst als explizite Eingabe: EPIC-6 übergibt hier die
     GEZEIGTEN (gekappten) Symbole — Konzept „Deklarationsspannen GEZEIGTER
     Symbole“, Kappung vor teuren Folgeanalysen.
  2. **Pure Funktion:** `FilterDiffRelevantViolations(...)` — behält eine
     Violation iff (a) ihre Datei+Zeile liegt in einem Hunk einer geänderten
     Datei, ODER (b) Datei+Zeile liegt innerhalb `StartLine..EndLine`
     (inklusive) eines `ShownSymbols`-Eintrags derselben Datei. Doppelte
     Bedingungserfüllung → genau ein Eintrag (Dedup). Ausgabe sortiert
     FilePath (OrdinalIgnoreCase) → LineNumber → RuleName (OrdinalIgnoreCase),
     analog `ViolationScopeFilter`. Kein scopeFilter, keine Trunkierung.
- **Warum:** Erfüllt §Filterregeln (Hunk ∪ Symbolspanne, sonst nichts) und
  §Performance-Regeln („Linter genau einmal“) als testbare interne Stufe,
  ohne den EPIC-6-Antwortvertrag vorwegzunehmen.

### Datei 3: `src/AiNetLinter/Core/DiffImpactAnalysisModels.cs`

- **Was:** XML-Doc von `DiffImpactCounters` (Klasse + `LintRuns`-Feld)
  aktualisieren: der Linter-Zähler hat jetzt seine Inkrement-Stufe in der
  Violations-Stufe (alter Text „hat noch keine Inkrement-Stelle“ stimmt dann
  nicht mehr). Kein Verhaltenscode.
- **Warum:** Verhaltensbehauptungen in Doku dürfen nicht veralten.

### Datei 4: `src/AiNetLinter.FastTests/Mcp/Tools/Analysis/DiffViolationFilterTests.cs` (neu)

- **Was:** Unit-Tests der pure Filterfunktion auf synthetischen
  `RuleViolation`-Listen + synthetischen Ranges/Symbol-Einträgen (kein Lint,
  kein Git) sowie ein Stage-Test mit echter `LinterEngine` auf einer
  In-Memory-Ad-hoc-Config-Solution (Bestandsmuster, z. B.
  `MetricsConfig { MaxLineCount = … }` für eine deterministische Violation auf
  bekannter Zeile):
  - Violation im Hunk → enthalten; Violation derselben Datei außerhalb von
    Hunks/Spannen → NICHT enthalten (Konzept-Kernfall „benachbarte
    irrelevante Violation“).
  - Violation ausschließlich in der Deklarationsspanne eines gezeigten
    Symbols (kein Hunk-Treffer) → enthalten.
  - Randwerte: erster/letzter Hunk- bzw. Spannen-Strich inklusive;
    `HunkRange` mit `LineCount=0` matcht nie (gemäß XML-Doc).
  - Pfadsemantik gepinnt: Hunks repo-root-relativ (native Trenner), Symbol-
    Einträge solution-relativ, Violations absolut — Normalisierung macht alle
    vergleichbar; Trenner `/` vs. `\` und Groß-/Kleinschreibung egal.
  - Beide Bedingungen erfüllt → genau ein Eintrag; Reihenfolge deterministisch.
  - Stage: Lint-Lauf zählt `LintRuns==1`; leerer Input → leeres Ergebnis,
    `LintRuns==0`, keine Malfunction.
- **Warum:** Die Filterregel ist der Kern dieses Steps und muss ohne
  Prozess-/Git-Abhängigkeit deterministisch absicherbar sein; der Stage-Test
  belegt den Einmal-Lauf bereits auf Unit-Ebene.

### Datei 5: `src/AiNetLinter.IntegrationTests/Core/DiffImpactAnalyzerOnceOnlyTests.cs`

- **Was:** Den bestehenden zusammengesetzten Lauf erweitern: nach Git-Stufe
  (`RunAnalysisAsync`, Counters) und Batch-Test-Stufe
  (`FindTestsForSymbolsCoreAsync`, Counters) ruft der Test die neue
  Violations-Stufe mit DEMSELBEN Counters-Objekt auf (Fixture
  `ChangeContextMiniWorkspace`, Ad-hoc-Config) und assertet das volle Tripel
  `GitRuns==1 && TestSolutionScans==1 && LintRuns==1`. Die alte
  `LintRuns==0`-Pin-Assertion samt veralteten Kommentar entfernt.
- **Warum:** Schließt den Konzept-Nachweis „Git einmal, Testsolution einmal,
  Linter einmal“ pro change-context-artigem Lauf; ersetzt die dokumentierte
  step-004-Ausnahme durch den echten Beleg.

## Tests

- [ ] `Filter_*`: Violation im Hunk enthalten; irrelevante Violation derselben Datei ausgeschlossen
- [ ] `Filter_*`: Violation in Deklarationsspanne eines gezeigten Symbols enthalten (auch ohne Hunk-Treffer)
- [ ] `Filter_*`: Hunk-/Spannen-Ränder inklusive; `LineCount=0`-Hunk matcht nie
- [ ] `Filter_*`: Pfadsemantik gepinnt (repo-root-relativ ↔ absolut ↔ solution-relativ, Trenner/Case tolerant)
- [ ] `Filter_*`: Doppelbedingung → Dedup; deterministische Sortierung FilePath→Zeile→Regel
- [ ] `Collect_*`: echte `LinterEngine` auf Ad-hoc-Solution → Treffer im Hunk gefiltert dabei, `LintRuns==1`
- [ ] `Collect_*`: leerer Input → kein Lint, `LintRuns==0`, leeres Ergebnis ohne Malfunction
- [ ] Integration: zusammengesetzter Lauf auf `ChangeContextMiniWorkspace` weist `GitRuns==1 && TestSolutionScans==1 && LintRuns==1` nach

Keine Doku-Änderungen in diesem Step: rein interne Stufe ohne
Vertrags-/Feature-Oberfläche; `Docs/agent-api.md`, README und ROADMAP-Doku
sind EPIC-6/EPIC-7 zugeordnet.

## Definition of Done

- [ ] Alle „Konkreten Änderungen“ umgesetzt (inkl. XML-Doc-Korrektur am Counter)
- [ ] `dotnet build` grün (0 Warnungen, 0 Fehler)
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` UND
      `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün
- [ ] Dogfood-Lint (`dotnet run --project src/AiNetLinter -- --config rules.json --path ./AiNetLinter.slnx`) grün;
      qualitätsrelevante Zusatzchecks (`find_duplicates`, `find_magic_values`,
      `find_dead_code`, `metrics_lookup` auf geänderten Symbolen) ohne neue Funde
- [ ] Kein neues MCP-Tool registriert; `GetImpactTool`/`GetImpactInput` unverändert (EPIC-6-Grenze respektiert)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Deutsch, imperativ)
- [ ] `step-007/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` #Grenzwerte (Produktion) — ≤60 Zeilen/Methode,
  ab 5 Parametern Input-`record`, `sealed`, `#nullable enable`,
  `AIContextFootprint` ≤2500, `MaxDirectoryChildren`=30 (Core ist voll → neue
  Datei nach `Mcp/Tools/Analysis`), `EnforceSealedClasses`, `AvoidExcessiveMiddleMen`.
- `.agents/rules/AiNetLinterRichtlinien.mdc` #Qualitätsdrift-Prävention —
  DRY-Konsolidierung (Lint-Aufruf nur an einer Stelle), Result-Pattern statt
  Exceptions, Zero-Warning, Kommentar-Disziplin OHNE Task-/Step-/EPIC-/TD-
  Referenzen im Code.
- `.agents/rules/AiNetLinterRichtlinien.mdc` #Updates-&-Tests — xUnit v3
  Pflicht, keine Serialisierungs-Collection für die neuen Testklassen,
  `TestTempDirectory` falls neue dateibasierte Fixtures nötig werden.

## Bekannte Ausnahmen

- Keine als flaky bekannten Tests. Das Integrations-Gate dauert ~2 Minuten
  (Subprozesse/Roslyn-Loads) — normal, kein Grund zur Abkürzung.
- Bewusste Ersetzung zweier step-004-Rückständen: die `LintRuns==0`-Assertion
  und ihr Begründungskommentar im OnceOnly-Test sowie der XML-Doc-Hinweis am
  Counter werden durch den echten Zustand ersetzt (dokumentierte Plan-Ausnahme
  von damals, kein Symptom-Fixing).

## Code-Skizze (optional)

```csharp
internal static class DiffViolationScanner
{
    // Stufe: genau ein Lint-Lauf, danach rein diffbezogene Filterung.
    internal static async Task<DiffViolationScanResult> CollectAsync(
        DiffViolationScanRequest request)
    {
        if (request.ChangedFiles.Count == 0 && request.ShownSymbols.Count == 0)
            return new DiffViolationScanResult([]);          // kein Lint, kein Zaehler

        if (request.Counters is { } counters)
            Interlocked.Increment(ref counters.LintRuns);    // EINE Inkrement-Stufe

        IReadOnlyCollection<RuleViolation> violations;
        try { violations = await GetViolationsScanner.RunSolutionLintAsync(request.Solution, request.Config, request.Console, request.CancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return new DiffViolationScanResult([], IsMalfunction: true, Context: ex.Message); }

        var filtered = FilterDiffRelevantViolations(violations, request.ChangedFiles,
            request.ShownSymbols, request.RepositoryRoot, SolutionDirOf(request.Solution));
        return new DiffViolationScanResult(filtered);
    }
}

// Pure Regel: Hunk-Treffer ODER Spannen-Treffer eines gezeigten Symbols — sonst nie.
// Pfade zentral normalisieren (GetFullPath(Combine(root, rel)) vs. absolute Violation-Pfade),
// Vergleich OrdinalIgnoreCase; Zeilen 1-basisch inklusive, LineCount=0 matcht nie.
```

## Notes

- **Drei Pfadbedeutungen sind DIE Falle dieses Steps** (alle drei im Spiel):
  `ChangedFileRange.FilePath` = repo-root-relativ mit nativen Trennern;
  `ChangedSymbolEntry.FilePath` = solution-relativ (`PathNormalizer.ToRelative`
  auf Solution-Verzeichnis); `RuleViolation.FilePath` = absoluter Dokumentpfad
  (Schlüsselform der `fileToProject`-Map in `ViolationScopeFilter`). Die
  Normalisierung gehört in EINE Hilfsstelle im Filter, damit die Unit-Tests
  die Semantik an EINER Stelle pinnen können. Vergleich immer ordinal,
  case-insensitive (Windows-FS), wie der Bestandscode.
- **Zeilen-Semantik:** `HunkRange` ist 1-basisch `[StartLine,
  StartLine+LineCount-1]`; `LineCount=0` expandiert zu keiner Zeile (steht so
  im XML-Doc) — matcht also nie. `ChangedSymbolEntry.StartLine/EndLine` sind
  1-basisch inklusive (aus `GetLineSpan`).
- **Counter-Semantik an GitRuns orientieren:** Inkrement unmittelbar VOR dem
  Lauf — der Zähler misst ausgeführte Stufen, auch wenn der Lauf fehlschlägt
  (Malfunction). Genau eine Produktions-Inkrement-Stufe; Tests gehen denselben
  öffentlichen Pfad (kein Sonderweg).
- **Skip-empty bewusst** gemäß step-004-Präzedenz (Batch-Scan zählt leere
  Zielliste weder Scan noch Zähler) — per Test pinnen und im XML-Doc nennen.
  Praktisch erreicht EPIC-6 die Stufe bei leerem Diff ohnehin nicht
  (`RunAnalysisAsync` liefert dort null), die Guard ist defensive Konsistenz.
- **Wiederverwendung statt Neubau:** Engine-Beschaffung nur noch im gemeinsamen
  Helper (geteilt mit `get_violations`); Malfunction-Muster von
  `GetViolationsResult`; Sortierreihenfolge von `ViolationScopeFilter`;
  Request-/Result-Records als Parameter-Objects wie `GetViolationsScannerParameters`.
- **EPIC-6-Grenze hart:** keine Anbindung an `GetImpactTool`, kein
  `detailLevel`/Caps/Completeness/INVALID_ARGUMENT, keine Antwortformatierung.
  Auch `FindCallSiteEntriesAsync`/Traversal und die Batch-Testzuordnung bleiben
  unangetastet. Die Stufe kapppt selbst NICHT — sie liefert alle
  diff-relevanten Violations; Kappung ist Antwortvertrag (EPIC-6).
- **Kommentare:** Why-Kommentare nur wo nicht offensichtlich (Pfadnormalisierung,
  Skip-empty-Entscheidung), ohne jede Task-/Step-/Epic-/TD-Referenz.
- **Dogfooding:** MCP-Server `ainetlinter` läuft proaktiv; bei „lädt noch“ oder
  seltsamen Antworten zuerst `get_server_health`. Vor/nach dem Refactoring
  `metrics_lookup` auf den geänderten Methoden; `find_duplicates` gegen die
  neue Filterlogik (Gefahr: eigener Where/OrderBy-Duplikat zu
  `ViolationScopeFilter.FilterAndSortViolations` — bewusst unterschiedlich
  begründen oder konsolidieren).
- **Schnelliteration:** neue FastTests-Klasse per
  `--filter FullyQualifiedName~DiffViolation` (Category=Unit), danach erst die
  vollen Gates. Integrationstest läuft unter `Category=Integration`.
