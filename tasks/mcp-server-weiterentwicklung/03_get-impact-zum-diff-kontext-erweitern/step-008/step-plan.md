---
status: open
type: step-plan
task: 03_get-impact-zum-diff-kontext-erweitern
step: 008
corrects: null
title: "get_impact-Vertrag „change-context" & strukturierte Antwort"
epic: EPIC-6
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: stealth/ox-alpha
created_by_model_knowledge_cutoff: unbekannt
created_at: 2026-08-23T10:40:00+02:00
related_to: [step-007]
---

# Step 008: get_impact-Vertrag „change-context" & strukturierte Antwort

## Bezug

- **Task:** `03_get-impact-zum-diff-kontext-erweitern`
- **Epic:** `EPIC-6` aus `roadmap.md` — letztes offene Code-Epic (EPIC-7 ist
  Doku): die internen Stufen aus EPIC-2..5 (Diff-Analyse, Batch-Tests,
  Violations-Filterung) werden an den öffentlichen `get_impact`-Vertrag
  angebunden; danach ist nur noch EPIC-7 offen.
- **Konzept-Referenz:** §Öffentlicher Vertrag (drei neue Optionen),
  §StructuredContent (JSON-Feldnamen sind VERTRAGLICH EXAKT),
  §Performance- und Größenregeln (Kappung VOR teuren Folgeanalysen,
  keine Source-Bodies), Audit D.3 (`depth` im gesamten Git-Branch
  wirkungslos), D.6 (Record-/Delegat-Grenzwerte prüfen), D.7
  (`BuildAggregateWarningAsync` an echten `ct`), §Tests.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des aktuellen Codes (GetImpactTool.cs, DiffImpactAnalyzer.cs,
DiffImpactAnalysisModels.cs, GetViolationsScanner.cs/DiffViolationScanner.cs,
TestCoverageBatchScan.cs, SymbolGraphToolRegistrations.cs, McpToolResults.cs,
TransitiveCallGraphModels.cs) vorgefunden:

1. **`GetImpactInput` hat 4 Parameter** (`GitRef, SymbolIdentifier,
   MaxResults, Depth`); Registrierungs-Lambda in `AddGetImpact` reicht genau
   diese plus `ct` durch. Neue Optionen sind ohne Lambda-Erweiterung
   protokollseitig unerreichbar — der bestehende Eintrag wird deshalb
   additiv erweitert (siehe „Bekannte Ausnahmen": DoD-Deutung).
2. **Kappung muss in den Analyzer-Kern:** `RunAnalysisAsync` ermittelt
   `List<ChangedSymbolMatch>` (Paarung ISymbol + Entry) und ruft
   `BuildReferencesAsync` über ALLE Symbole auf — die teure Call-Site-Suche
   läuft also heute ungekappt INNERHALB des Kerns. Die Konzept-Regel
   „Kappung VOR teuren Folgeanalysen" ist nur erfüllbar, wenn Sortierung +
   `Take(cap)` zwischen `GetChangedSymbolsFromHunksAsync` und
   `BuildReferencesAsync` sitzen. Das Ergebnisobjekt `DiffImpactAnalysis`
   trägt bisher weder die ISymbol-Handles noch die Gesamtzahl vor Kappung —
   beides wird additiv benötigt.
3. **Batch-Testzuordnung braucht ISymbols:** `FindTestsForSymbolsCoreAsync(
   IReadOnlyList<ISymbol>, Solution, counters, ct)` — die Handles der
   GEZEIGTEN Symbole müssen die Kappung überleben (kein Re-Resolve).
   Ergebnis `TestCoverageBatchSymbolResult(SymbolId, TotalMatchCount,
   TestFiles)` mit `TestFileCoverageResult(FilePath, TestClassName, Category,
   MatchReason, TestMethods, TotalClassTests, ProjectDirectory)` bildet 1:1
   auf die Konzept-Felder `testAssociations{symbolId, filePath, testMethods,
   matchReason}`; die Liste ist bereits nach MatchReason-Priorität → FilePath
   sortiert (deterministische Test-Kappung darauf aufsetzen).
4. **Violations-Stufe fertig verdrahtbar:** `DiffViolationScanner.CollectAsync(
   DiffViolationScanRequest(Solution, Config, Console, RepositoryRoot,
   ChangedFiles, ShownSymbols, Counters, ct))`. Config/Console beschafft das
   Tool wie `GetViolationsTool` über `state.GetConfigSnapshot()` bzw.
   `state.Console` — KEINE Registrierungssignatur-Änderung dafür nötig.
   Malfunction-Muster (IsError=true, Code AnalysisFailed) wie in
   `GetViolationsTool.ExecuteAsync` übernehmen.
5. **Serialisierung:** `McpToolResults.Text<T>` nutzt `McpJsonOptions.Default`
   (CamelCase, kompakt, IgnoreNull, **KEIN JsonStringEnumConverter**) — die
   Roslyn-`Accessibility`-Enum würde als ZAHL serialisieren. Der
   Antwort-DTO mappt sie deshalb explizit auf String („Public" …), damit
   §StructuredContent exakt eingehalten wird. Payload muss Objekt bleiben
   (Top-Level-Array verboten, siehe XML-Doc an `Text<T>`).
6. **recommendedTestCommands-Quelle steht:**
   `TestRecommendationBuilder.BuildDotNetTestCommands(IReadOnlyList<
   TestFileCoverageResult>)` dedupliziert je Testprojekt (EIN Befehl,
   quotierter Vereinigungsfilter) — über die NACH Test-Cap gezeigten Treffer
   aller Symbole aufrufen.
7. **ct-Lücke exakt lokalisert:** `ExecuteGitRefBranchAsync(solution, input)`
   nimmt kein `ct`; Zeile 92 ruft
   `FindSymbolTool.BuildAggregateWarningAsync(solution, CancellationToken.None)`
   — das ist die Audit-D.7-Wirkstelle.
8. **Grenzwerte empirisch entschärft (D.6):** Registrierungs-Lambdas mit >4
   Parametern existieren lint-grün (`search_pattern`: 11,
   `dependency_graph`: 6) — `MaxMethodParameterCount: 4` trifft benannte
   Methoden; `ExecuteAsync` behält weiterhin seinen Input-Record. Neuer
   Antwort-Builder als eigene Datei hält `MaxMethodLineCount`/Dateigrößen
   ein; `Mcp/Tools/SymbolGraph` hat 14 Dateien (+1 = 15 ≤
   `MaxDirectoryChildren` 30).
9. **Anti-Loop-Check gegen codemap.md:** kein Widerspruch — codemap führt
   `GetImpactTool` ausdrücklich als „Hauptort des neuen
   detailLevel=change-context-Vertrags" und
   `SymbolGraphToolRegistrations` als Kontrollstelle der DoD-Regel mit dem
   Hinweis „neuer Vertrag läuft über den bestehenden Eintrag". Die
   step-007-Stufen werden genutzt, nicht dupliziert.

## Intention

Nach diesem Step liefert `get_impact(detailLevel="change-context")` die
komplette Konzept-Antwort: strukturiertes Objekt mit EXAKT den Feldnamen aus
§StructuredContent, deterministische Symbol-Kappung VOR Call-Site-/Test-/
Violation-Analyse, Vollständigkeitsmetadaten, Sufficiency-Hint bei
vollständigen Ergebnissen, kompakten Text ohne Source-Bodies — während
`detailLevel=callers` (und damit alle Bestandsaufrufe) bytegleich im
bisherigen Verhalten bleibt. Validierungsfehler sind recoverable
INVALID_ARGUMENT-Ergebnisse, der Git-Branch ist an den echten `ct`
angebunden.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (AddGetImpact, Z. 105–125)

- **Was:** Lambda ADDITIV am Ende erweitern (Reihenfolge der bestehenden
  Parameter unverändert, damit positionelle Aufrufer nicht brechen):
  `(..., string? gitRef = null, string? symbolIdentifier = null,
  int maxResults = 50, int depth = 1, string? detailLevel = null,
  int maxChangedSymbols = 20, int maxTestsPerSymbol = 10,
  CancellationToken ct = default)` und Weiterleitung in den Record.
  `GetImpactDescription` erweitern: beide detailLevel-Werte + Default,
  Caps/Defaults, Kombinationsverbot mit `symbolIdentifier` (Hinweis
  `get_feature_context`), `depth` im gesamten Git-Branch wirkungslos
  (Audit D.3 Vertragstext-Pflicht), kein neues Tool (weiterhin EINE
  `tools.Add`-Zeile für `get_impact`).
- **Warum:** MCP-Parameter entstehen aus der Delegat-Signatur; nur so sind
  die drei neuen Optionen erreichbar, ohne ein zweites Tool zu registrieren
  (DoD „Kein neues MCP-Tool wurde registriert" bleibt erfüllt).

### Datei 2: `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`

- **Was:**
  1. `GetImpactInput` um `string? DetailLevel = null`,
     `int MaxChangedSymbols = 20`, `int MaxTestsPerSymbol = 10` ergänzen
     (additive Defaults im Record; Record darf weiter wachsen, Regel
     `MaxMethodParameterCount` betrifft Methoden).
  2. Validierung VOR dem Dispatch: `detailLevel` case-insensitiv parsen;
     `null`/leer/„callers" → bisheriger Pfad UNVERÄNDERT; „change-context"
     zusammen mit `symbolIdentifier` → `McpToolResults.InvalidArgument`
     mit Hint auf `get_feature_context`; unbekannter Wert →
     `InvalidArgument` mit den erlaubten Werten.
  3. `ExecuteGitRefBranchAsync` bekommt `CancellationToken ct` und ruft
     `BuildAggregateWarningAsync(solution, ct)` statt
     `CancellationToken.None` (Audit D.7); Verhalten sonst unverändert.
  4. Neuer Zweig `ExecuteChangeContextBranchAsync`: Normalisierung
     (maxChangedSymbols <1→20, >100→100; maxTestsPerSymbol <1→10,
     >50→50), gekappte `AnalyzeChangeContextAsync`, dann Stufen
     Batch-Tests → Violations, Antwort immer als strukturiertes Objekt
     via `McpToolResults.Text<T>` (auch „kein Repo / leerer Diff" →
     leere Contract-Struktur + Sufficiency-Hint);
     `GitDiffFailedException` → dasselbe Recoverable-Muster wie heute;
     Malfunction der Violations-Stufe → `Error(AnalysisFailed)` analog
     `GetViolationsTool`. Kompakter Text (Counts + gekappte Top-Einträge,
     `maxResults` kappt nur die Text-Topliste), Sufficiency-Hint via
     `McpSufficiencyHints.Append` nur wenn nichts trunkiert wurde, sonst
     Trunkierungs-Meta-Zeile. Jede Methode ≤60 Zeilen — ggf. Hilfsmethoden
     extrahieren; Dispatch selbst bleibt dünn.
- **Warum:** Hauptort des neuen Vertrags (codemap); Validierung gehört vor
  jede teure Arbeit; ct-Bindung schließt Audit D.7.

### Datei 3: `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` (+ ggf. `DiffImpactAnalysisModels.cs` nur XML-Doc)

- **Was:**
  1. `DiffAnalysisRequest` um optionalen Cap-Parameter ergänzen
     (`int ChangedSymbolCap = int.MaxValue` o. ä.; Name Coder-Freiheit,
     Semantik fix: Obergrenze GEZEIGTER Symbole).
  2. `RunAnalysisAsync`: nach `GetChangedSymbolsFromHunksAsync` die Treffer
     deterministisch sortieren (**Projekt → Datei → Startzeile → Symbol-ID**,
     ordinal, Pfade case-insensitive konsistent zum Bestand) und auf den
     Cap kappen — VOR `BuildReferencesAsync` (damit laufen Call-Site-Suche
     und alle Folgeanalysen nur noch über gezeigte Symbole). Neuer interner
     Eintrittspunkt (Überladung von `AnalyzeChangeContextAsync`) mit
     Cap + `DiffImpactCounters`; bestehende Signaturen unverändert.
  3. Ergebnis trägt zusätzlich zur gekappten `ChangedSymbols`-Liste (a) die
     Gesamtzahl VOR Kappung und (b) die ISymbol-Handles der gezeigten
     Symbole in identischer Reihenfolge — als additive optionale Member an
     `DiffImpactAnalysis` (Defaults, damit alle bestehenden Konstruktor-
     aufrufe gültig bleiben) oder als kleines internes Outcome-Record;
     Variante Coder-Freiheit, Semantik fix. `callers`-Pfad läuft weiter
     ohne Cap (Default) und bleibt verhaltensidentisch.
- **Warum:** Nur im Kern kann die Kappung VOR der teuren Referenz-Stufe
  greifen (§Performance- und Größenregeln); Handles + Total vermeiden
  Symbol-Re-Resolution und liefern die Completeness-Metadaten.

### Datei 4: NEU `src/AiNetLinter/Mcp/Tools/SymbolGraph/ChangeContextResponseModels.cs` (Name frei)

- **Was:** DTO-Records mit EXAKT den §StructuredContent-Feldnamen (die
  CamelCase-Policy übersetzt PascalCase-Properties 1:1) plus reine Mapping-
  Funktionen (je ≤60 Zeilen):
  - Payload: `mode` ("gitDiff"), `detailLevel` ("change-context"),
    `changedFiles[{filePath, ranges[{startLine, lineCount}]}]` (aus
    `ChangedFileRange`/`HunkRange`),
    `changedSymbols[{documentationCommentId, displayName, kind,
    accessibility (STRING, z. B. "Public" — Enum.Tostring, nie Zahl!),
    projectName, filePath, startLine, endLine}]` (aus `ChangedSymbolEntry`,
    `SymbolId` → `documentationCommentId`),
    `callSites` (unverändert `TransitiveCallSiteEntry`-Liste),
    `testAssociations[{symbolId, filePath, testMethods[], matchReason}]`
    (aus Batch-Ergebnis, Test-Methoden pro Symbol auf `maxTestsPerSymbol`
    gekappt — Reihenfolge = bestehende MatchReason-Priorität → FilePath),
    `violations[{filePath, lineNumber, ruleName, severity, details}]`
    (kompakt, bewusst KEIN Snippet/Source-Ausschnitt — §Performance-Regel),
    `recommendedTestCommands` (`TestRecommendationBuilder.BuildDotNetTestCommands`
    über die NACH Cap gezeigten Testtreffer aller Symbole),
    `completeness{changedSymbolsTotal, changedSymbolsShown, symbolsTruncated,
    callSitesTruncated, testsTruncated}` — die fünf Feldnamen EXAKT;
    `callSitesTruncated` spiegelt `References.Completeness`
    (TruncatedByMaxResults || TruncatedByNodeLimit), `testsTruncated` =
    mindestens ein Symbol hatte mehr Treffer als der Cap.
- **Warum:** Eigene Datei hält `GetImpactTool` schlank; zentrale DTOs machen
  die Vertragsfeldnamen testbar pinbar; Wiederverwendung der vorhandenen
  Stufen statt zweiter Implementierung.

### Nicht geändert (bewusst)

- `Docs/**`, `README.md`, `Docs/ROADMAP.md` → EPIC-7.
- Bestehende `AnalyzeDiffAsync`/`AnalyzeEntriesAsync`-Signaturen und das
  Verhalten des `callers`-Modus (inkl. seiner Snapshot-/Subprozess-Tests).
- `GetViolationsScanner.RunSolutionLintAsync` wird NICHT direkt vom Tool
  gerufen — der einzige Lint-Zugriff läuft über `DiffViolationScanner.
  CollectAsync`, damit `LintRuns` genau eine Inkrement-Stelle behält.

## Tests

FastTests — `GetImpactToolTests.cs` erweitern (+ reine Mapping-Tests für
Datei 4, dort oder eigene Datei):

- [ ] `detailLevel="change-context"` + `symbolIdentifier` → recoverable
      INVALID_ARGUMENT, Text/Hint nennt `get_feature_context`
- [ ] Unbekannter `detailLevel`-Wert → recoverable INVALID_ARGUMENT mit den
      erlaubten Werten; `null`/""/"callers" (auch groß/klein) wählt den
      Bestands-Pfad
- [ ] Cap-Normalisierung: maxChangedSymbols 150→100 / 0→20;
      maxTestsPerSymbol 99→50 / 0→10
- [ ] StructuredContent-Vertragstest: serialisiertes Payload trägt EXAKT die
      §StructuredContent-Property-Namen inkl. Verschachtelung
      (`changedFiles[].ranges[].startLine|lineCount`, `completeness.*`)
- [ ] `accessibility` serialisiert als String ("Public"), nicht als Zahl
- [ ] Komplettmetadaten bei gekapptem Szenario (2 geänderte Symbole,
      maxChangedSymbols=1 → symbolsTruncated=true, total=2, shown=1; der
      weggekappte Symbol-Eintrag taucht NIRGENDS auf — auch nicht in
      callSites/testAssociations/violations)
- [ ] testsTruncated=true, wenn ein Symbol mehr Testmethoden als Cap hat;
      `recommendedTestCommands` dedupliziert je Testprojekt und enthält nur
      gezeigte Treffer
- [ ] Violation-Einträge ohne Snippet/Source-Body
- [ ] Textform: Sufficiency-Hint bei vollständigem Ergebnis, Meta-Zeile bei
      Kappung, Counts + Top-Einträge, keine Source-Bodies
- [ ] `callers`-Regression: bestehende GetImpactToolTests unverändert grün

IntegrationTests:

- [ ] `GetImpactToolIntegrationTests` auf `ChangeContextMiniWorkspace`:
      Ende-zu-Ende `detailLevel="change-context"` — beide geänderten Methoden
      inkl. privater `LogInternal` in `changedSymbols`, Call-Sites für
      `PlaceAsync`, nicht-leere `testAssociations`/`recommendedTestCommands`,
      Violations nur aus Hunk/Spanne
- [ ] `McpServerCommandGetImpactTests` (Subprozess-Snapshot `callers`)
      bleiben unangetastet und grün — Abwärtskompatibilitätsnachweis

Keine eigenen Tests: ct-Bindung (D.7) ist strukturell review-bar und durch
Bestandstests abgedeckt (Abbruchverhalten dediziert zu testen brächte
Flakiness ohne Mehrwert).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt; JSON-Feldnamen 1:1 mit Konzept
      §StructuredContent abgeglichen (Vertragstest grün)
- [ ] KEIN neues MCP-Tool registriert — bestehender `get_impact`-Eintrag nur
      additiv erweitert (Deutung siehe „Bekannte Ausnahmen")
- [ ] `dotnet build` grün (0 Warnungen, 0 Fehler)
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` UND
      `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
      grün
- [ ] Dogfood-Lint grün; Zusatzchecks via MCP (erst `reload_config`, dann)
      `metrics_lookup` auf neuen/geänderten Symbolen, `find_duplicates`/
      `find_magic_values`/`find_dead_code` im berührten Scope OK
- [ ] Commit auf aktuellem Branch (Conventional Commit, deutsch)
- [ ] `step-008/step-result.md` geschrieben
- [ ] `status` in dieser Datei → `done (pending audit)`

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` #Grenzwerte — Methoden ≤60 Zeilen (Builder
  splitten), ab 5 Parametern Input-Record (Stufen nutzen ohnehin Records),
  `AIContextFootprint` 2500 für den neuen Builder-Typ beachten,
  `MaxDirectoryChildren` 30 (SymbolGraph 14→15), `sealed`, `#nullable enable`.
- `.agents/rules/AiNetLinterRichtlinien.mdc` #5-Qualitätsdrift-Prävention —
  Zero-Warning, `metrics_lookup` vor/nach, DRY/Magic-Values/Dead-Code,
  Kommentar-Disziplin: KEINE Task-/Step-/EPIC-Referenzen im Code.
- `.agents/rules/AiNetLinterRichtlinien.mdc` #4-Updates-&-Tests — xUnit v3,
  `TestTempDirectory` statt OS-Temp, keine Serialisierungs-Collection,
  schnelle Slices während der Iteration, volle Gates nur am Step-Ende.

## Bekannte Ausnahmen

- **Keine flaky Tests bekannt.**
- **DoD-Deutung Registration (dem Orchestrator/Kritiker sichtbar
  dokumentiert):** Der Auftrag lautet wörtlich „Es wird KEIN neues MCP-Tool
  registriert — Registrierungsdatei bleibt unverändert." Wörtlich genommen
  wäre EPIC-6 unerfüllbar: MCP-Parameter entstehen aus der Delegat-Signatur,
  ohne Lambda-Erweiterung erreicht keiner der drei neuen Parameter das Tool.
  Konzept-DoD („Kein neues MCP-Tool wurde registriert") und codemap
  („neuer Vertrag läuft über den bestehenden Eintrag") legen die sinnvolle
  Lesart nahe, die dieser Plan umsetzt: bestehender `AddGetImpact`-Eintrag
  wird ADDITIV erweitert (Parameter + Beschreibung), es entsteht keine
  zweite `tools.Add`-Zeile und kein neues Tool. Falls der Orchestrator die
  wörtliche Lesart will: Step zurückweisen — dann braucht es eine
  Nutzerentscheidung.
- **Namensabweichung Konzept/Code:** Konzept §Öffentlicher Vertrag nennt den
  Git-Parameter „gitSinceRef"; das bestehende Tool-Argument heißt
  `gitRef` (intern im Analyzer `gitSinceRef`). Der Name bleibt
  unverändert (Abwärtskompatibilität); EPIC-7 dokumentiert die
  tatsächlichen Namen.

## Code-Skizze (optional)

```
// DTO-Skelett — Property-Namen erzeugen per CamelCase-Policy EXAKT die Vertragsnamen
internal sealed record ChangeContextPayload(
    string Mode,                              // "gitDiff"
    string DetailLevel,                       // "change-context"
    IReadOnlyList<ChangedFilePayload> ChangedFiles,
    IReadOnlyList<ChangedSymbolPayload> ChangedSymbols,
    IReadOnlyList<TransitiveCallSiteEntry> CallSites,
    IReadOnlyList<TestAssociationPayload> TestAssociations,
    IReadOnlyList<ViolationPayload> Violations,
    IReadOnlyList<string> RecommendedTestCommands,
    CompletenessPayload Completeness);

// Kappung im Analyzer-Kern — NACH Symbolermittlung, VOR Referenz-Stufe
var matches = await GetChangedSymbolsFromHunksAsync(solution, repoRoot, hunkRanges, request.Scope);
var ordered = matches.OrderBy(m => m.Entry.ProjectName, StringComparer.Ordinal)
                     .ThenBy(m => m.Entry.FilePath, comparer: OrdinalIgnoreCase)
                     .ThenBy(m => m.Entry.StartLine)
                     .ThenBy(m => m.Entry.SymbolId, StringComparer.Ordinal)
                     .ToList();
var shown = ordered.Take(request.ChangedSymbolCap).ToList();
var references = await BuildReferencesAsync(shown, request.Solution); // nur noch GEZEIGTE
```

## Notes

- **Wiederverwendungslandkarte (nichts davon neu bauen):**
  `AnalyzeChangeContextAsync`/`RunAnalysisAsync` (EPIC-2/3),
  `FindTestsForSymbolsCoreAsync` (EPIC-4), `TestRecommendationBuilder`
  (step-004), `DiffViolationScanner.CollectAsync` + `RunSolutionLintAsync`
  (step-007), `McpSufficiencyHints.Append`, `McpToolResults.Text<T>`/
  `InvalidArgument`, `McpTruncation.TruncateLines`. Dieser Step VERDRAHTET.
- **Stolperfallen:**
  - `McpJsonOptions.Default` hat keinen Enum-Converter — Accessibility MUSS
    als String gemappt werden, sonst verletzt der Serialisierer den Vertrag.
  - `Text<T>` verbietet Top-Level-Arrays — Payload immer als Objekt.
  - Pfadsemantiken nicht vermischen: Hunks repo-root-relativ,
    Symbol-Einträge solution-relativ, Violations absolut; die Violations-
    Stufe normalisiert bereits selbst (`DiffPathContext`) — nichts doppelt
    bauen, `ShownSymbols` = die GEZEIGTEN Entries.
  - `ChangedSymbolEntry.FilePath` (solution-relativ) ≠
    `ChangedFileRange.FilePath` (repo-root-relativ) — für die DTOs 1:1
    durchreichen, keine Umrechnung.
  - Sufficiency-Hint NUR bei vollständigem Ergebnis; gekappte Antworten
    tragen ihre Meta-Zeile (Muster `GetViolationsTool`/`FindReferencesTool`).
  - Residenter MCP-Server: vor den MCP-Zusatzchecks `reload_config` (neue
    Datei ist sonst unsichtbar, siehe step-007-Beobachtung).
  - `depth` bleibt im GESAMTEN Git-Branch wirkungslos (Audit D.3) — auch im
    change-context; nur der Beschreibungstext weist darauf hin. `maxResults`
    kappet im change-context ausschließlich die Text-Topliste.
  - Counters: der Tool-Zweig legt intern ein `DiffImpactCounters`-Objekt an
    und reicht es durch alle drei Stufen; ein optionaler interner
    ExecuteAsync-Parameter für instrumentierte Tests ist erlaubt, ändert am
    bestehenden Tripel-Integrationstest (Stufen-Ebene) aber nichts.
- **Größe/Einordnung:** größer als step-007, aber ein in sich geschlossener,
  einzeln reviewbarer Vertragsschnitt; Aufteilung würde die
  Antwort-Form (Kern des Epics) vom Validierungs-/Kappungs-Verhalten trennen
  und eine Review-Runde unter Dummy-Bedingungen erzwingen. Deshalb als
  einzelner `step_type: single` geplant; EPIC-6 gilt danach als abgearbeitet
  (Rest: EPIC-7-Doku).
