---
status: done (pending audit)
type: step-plan
task: 03_get-impact-zum-diff-kontext-erweitern
step: 002
corrects: null
title: "Strukturiertes DiffImpactAnalysis-Ergebnisobjekt im DiffImpactAnalyzer"
epic: EPIC-2
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: stealth/ox-alpha
created_by_model_knowledge_cutoff: unbekannt
created_at: 2026-08-22T19:55:00+02:00
related_to: [step-001]
---

# Step 002: Strukturiertes DiffImpactAnalysis-Ergebnisobjekt im DiffImpactAnalyzer

## Bezug

- **Task:** `03_get-impact-zum-diff-kontext-erweitern`
- **Epic:** `EPIC-2` aus `roadmap.md` — erster von zwei geplanten Steps:
  das strukturierte interne Ergebnisobjekt samt Wrapper-Analyse und
  kompakten Hunk-Ranges. Der breite Diff-Symbolscanner folgt als
  nächster Step (Planungsnotiz in der Roadmap); der Scanner-Scope bleibt
  in diesem Step unverändert schmal (public/internal Methoden +
  Konstruktoren).
- **Konzept-Referenz:** `Konzept.md` §Internes Ergebnisobjekt
  (DiffImpactAnalysis-Record, AnalyzeEntriesAsync als kompatibler
  Wrapper, Git genau einmal), §Scope Must-have (Geänderte Symbole mit
  stabiler ID/Accessibility/Kind/Anzeigename/Projekt/Datei/
  Deklarationszeilen; changedFiles mit kompakten Hunk-Ranges),
  §Performance-Regeln (Git genau einmal), Audit A.2/A.4.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des aktuellen Codes (nach dem step-001-Umbau) vorgefunden:

- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` (376 Zeilen): public
  `AnalyzeAsync` (einziger externer Aufrufer: `Commands/ImpactCommand.cs:32`,
  CLI) ist dünner Wrapper über internal `AnalyzeEntriesAsync` (Aufrufer:
  `GetImpactTool.ExecuteGitRefBranchAsync`, GetImpactTool.cs:78). Die
  Zwischenstruktur — `Dictionary<string, List<int>>` expandierter
  Zeilen je Datei (`ParseGitDiffHunks`) und `List<ISymbol>`
  (`GetChangedSymbolsFromHunksAsync`) — wird aktuell zugunsten der
  flachen `List<CallSiteEntry>` verworfen. `RunGitDiff` läuft pro Aufruf
  genau einmal; nicht auflösender `gitSinceRef` wirft
  `GitDiffFailedException` (Fluss von `GetImpactTool` abhängig —
  unverändert lassen). Der Symbolfilter ist
  `IsPublicOrInternal` (Public/Internal/Protected/ProtectedOrInternal)
  über `MethodDeclarationSyntax` + `ConstructorDeclarationSyntax` nur.
- **`ParseGitDiffHunks` hat externe Nutzer** (per find_references am
  laufenden Server verifiziert): `FindMagicValuesScanner` (2 Call-Sites,
  changedOnly-Modus) und der Bestands-Unit-Test
  `ParseGitDiffHunks_WithValidDiff_ParsesHunksCorrectly` (prüft
  expandierte Einzellinien). Signatur und Verhalten sind daher fixiert;
  das neue Range-Parsing muss daneben existieren und die Expansion
  idealerweise daraus ableiten (DRY nach innen).
- **Stabile-ID-Logik existiert bereits:** `CallGraphTraversal.GetStableSymbolId`
  (private): `DocumentationCommentId.CreateDeclarationId(symbol) ??
  symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` —
  exakt die im Konzept geforderte Form „DocCommentId oder
  deterministischer Fallback". Wird analog zum step-001-Muster
  (`ResolveEnclosingMemberAsync`) auf `internal` gestellt und
  wiederverwendet statt dupliziert.
- **`ReferenceTraversalResult` (+ `TransitiveCallSiteEntry`,
  `TraversalCompleteness`) existiert** in
  `Mcp/Tools/SymbolGraph/TransitiveCallGraphModels.cs` und ist laut
  Konzept/KodeMap Wiederverwendungsquelle für die References-Komponente.
  Es gibt bislang **keine** Core→Mcp-Namespace-Referenz — die neue
  Modelldatei in `AiNetLinter.Core` referenziert den Typ bewusst via
  `using AiNetLinter.Mcp.Tools.SymbolGraph;` (Monolith, eine Assembly,
  keine Zyklen; Verschiebung der TransitiveCallGraphModels nach Core
  wäre Churn ohne Nutzen — siehe Notes).
- **Git-basierte Testinfrastruktur existiert:**
  `IntegrationTests/Fixtures/FixtureWorkspaces.cs` stellt
  `GitImpactMiniFixtureWorkspace` (echtes Temp-Git-Repo mit Initial-
  Commit, in CodeMap nachgetragen) bereit — Basis der bestehenden
  get_impact-Git-Branch-Integrationstests; wiederverwendet statt neuer
  Fixture. Kein bestehender Test ruft `AnalyzeEntriesAsync` direkt auf;
  die Abwärtskompatibilität des `callers`-Modus wird über die
  Bestands-Tool-/Subprozess-Tests abgesichert — diese müssen UNVERÄNDERT
  grün bleiben.
- Regeldateien gelesen (Schritt 4a): Grenzwerte (MaxLineCount 500 →
  Modelle in eigene Datei; MaxMethodParameterCount 4; `sealed`;
  `#nullable enable`), DRY/Kommentar-Disziplin, xUnit-Pflicht.

## Intention

Nach diesem Step hält `DiffImpactAnalyzer` sein Zwischenergebnis in einem
strukturierten `DiffImpactAnalysis`-Objekt fest (RepositoryRoot, SinceRef,
ChangedFiles mit kompakten Hunk-Ranges, ChangedSymbols-Einträge inkl.
stabiler ID, References als `ReferenceTraversalResult`), führt Git weiter
genau einmal aus und gibt es über einen neuen internen Kern zurück;
`AnalyzeEntriesAsync` ist nur noch Wrapper, dessen Ausgabe (inklusive
Reihenfolge) identisch zum heutigen Stand bleibt — der `callers`-Modus
ist damit abwärtskompatibel, ohne dass etwas Sichtbares passiert. Das ist
die Grundlage, auf der der nächste Step den breiten Scannerpfad einhängt
und EPIC-3 den Einmal-Ausführungs-Nachweis instrumentiert.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Core/DiffImpactAnalysisModels.cs` (neu)

- **Was:** Vier `internal sealed record`-Typen, Namespace
  `AiNetLinter.Core`, `#nullable enable`:
  - `HunkRange(int StartLine, int LineCount)` — kompakte Hunk-Range
    (1-basiert, wie aus `@@ -a,b +c,d @@`).
  - `ChangedFileRange(string FilePath, IReadOnlyList<HunkRange> Ranges)`
    — FilePath repo-root-relativ mit nativen Trennern (wie heute die
    Diff-Keys).
  - `ChangedSymbolEntry(string SymbolId, string DisplayName, string Kind,
    Accessibility Accessibility, string ProjectName, string FilePath,
    int StartLine, int EndLine)` — SymbolId via stabile-ID-Logik
    (DocCommentId ?? FullyQualifiedFormat-Fallback), DisplayName als
    `"EnthaltenderTyp.Membername"` (konsistent zu `CallSiteEntry.SymbolName`),
    Kind = `symbol.Kind`-Name, Deklarationszeilen 1-basis aus
    `GetLineSpan()`, FilePath solution-relativ via `PathNormalizer.ToRelative`.
  - `DiffImpactAnalysis(string RepositoryRoot, string? SinceRef,
    IReadOnlyList<ChangedFileRange> ChangedFiles,
    IReadOnlyList<ChangedSymbolEntry> ChangedSymbols,
    ReferenceTraversalResult References)` mit
    `using AiNetLinter.Mcp.Tools.SymbolGraph;` (bewusste Entscheidung,
    siehe Notes).
- **Warum:** Konzept §Internes Ergebnisobjekt fordert genau dieses
  Record-Set; eigene Datei hält `DiffImpactAnalyzer.cs` unter dem
  MaxLineCount-Limit 500 (Muster: `TransitiveCallGraphModels.cs`).

### Datei 2: `src/AiNetLinter/Core/DiffImpactAnalyzer.cs`

- **Was:**
  - Neu: `internal static async Task<DiffImpactAnalysis?> AnalyzeDiffAsync(
    Solution solution, string targetPath, string? gitSinceRef, bool verbose)`
    — übernimmt die heutige Steuerung 1:1: `FindRoot` null → Warnung
    (verbose) + Rückgabe null; `RunGitDiff` **genau einmal**, leerer/null
    Output → null; sonst `GitDiffFailedException` unverändert durchreichen.
    Baut: ChangedFiles aus neuem Range-Parsing (nutzt bestehendes
    `TryExtractHunkRange`; count=0-Ranges werden wie heute zu keinen
    Zeilen expandiert), ChangedSymbols über die BESTEHENDE schmale
    Symbolermittlung (Methoden+Konstruktoren, `IsPublicOrInternal` —
    kein Scope-Flag, keine Verhaltensänderung), gemappt auf
    `ChangedSymbolEntry`; References aus der bestehenden per-Symbol-
    Call-Site-Suche, je Eintrag ein `TransitiveCallSiteEntry` mit
    Depth=1 und ReachedFromSymbolId = stabile ID des jeweiligen
    geänderten Symbols, eingepackt in ein vollständiges
    `ReferenceTraversalResult` (RequestedDepth=EffectiveDepth=1,
    VisitedNodeCount = Anzahl ausgewerteter geänderter Symbole,
    TotalCallSiteCount=ShownCallSiteCount, TruncatedBy*=false —
    Kappung bleibt wie heute Sache des Tools).
  - `IntersectsWithChangedLines` arbeitet künftig auf Hunk-Ranges
    (Überlappungsprüfung; semantisch äquivalent zur heutigen
    Einzellinien-Mitgliedschaft).
  - `ParseGitDiffHunks` behält Signatur/Verhalten bei (Nutzer:
    `FindMagicValuesScanner`, Bestandstest), leitet seine Expansion
    intern aus den Ranges ab (eine Parse-Wahrheit, DRY).
  - `AnalyzeEntriesAsync` wird Wrapper: `analysis is null` → leere Liste,
    sonst `References.CallSites` feldidentisch und **reihenfolgetreu**
    auf `List<CallSiteEntry>` abbilden. `AnalyzeAsync` (CLI) unberührt.
- **Warum:** Konzept §Internes Ergebnisobjekt (Wrapper bleibt, nutzt
  intern das Ergebnisobjekt; Git genau einmal) + §Performance-Regeln;
  Bestandsverhalten des `callers`-Modus bleibt snapshot-kompatibel.

### Datei 3: `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs`

- **Was:** `GetStableSymbolId` von `private` auf `internal` stellen und
  mit kurzem Why-XML-Doc versehen (gemeinsame Quelle für Traversal- und
  Analyzer-Symbol-IDs).
- **Warum:** DRY — die Konzept-Stabile-ID-Logik existiert genau einmal;
  gleiches Muster wie `ResolveEnclosingMemberAsync` in step-001.

### Datei 4: `src/AiNetLinter.FastTests/Core/DiffImpactAnalyzerTests.cs`

- **Was:** Unit-Tests ergänzen (Bestandstest bleibt unangetastet grün):
  - Range-Parsing: Multi-File-Diff mit mehreren Hunks je Datei, Einzeiler-Hunk
    (`+c` ohne `,d`), count=0-Hunk → kompakte Ranges exakt.
  - Expansions-Äquivalenz: aus Ranges abgeleitete Zeilenlisten ==
    bisherige Expansion (verhindert Drift zwischen beiden Sichten).
  - `ChangedSymbolEntry`-Mapping über `McpInMemoryTestContext.CreateScenario`:
    public/internal/protected/private Methode → korrekte DocCommentId,
    DisplayName, Kind, Accessibility, Projekt, relative Datei,
    Deklarationszeilen; lokale Funktion → deterministischer Fallback
    (FullyQualifiedFormat) statt null.
  - Wrapper-Mapping: `References.CallSites` → `CallSiteEntry` Feld- und
    Reihenfolgenidentität (reine Funktion, ohne Git testbar).
- **Warum:** Richtlinien §4 (xUnit-Pflicht für jede Logik-Änderung);
  reine Teile ohne Subprozess absicherbar.

### Datei 5: `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs`

- **Was:** Ein Test auf `GitImpactMiniFixtureWorkspace`: Arbeitsdatei nach
  dem Initial-Commit ändern, dann `AnalyzeDiffAsync` direkt aufrufen →
  RepositoryRoot gesetzt, SinceRef=null, ChangedFiles enthält die Datei
  mit kompakten Ranges, ≥1 `ChangedSymbolEntry`, und
  `References.CallSites` ist elementgleich zum Ergebnis von
  `AnalyzeEntriesAsync` auf derselben Solution (Ende-zu-Ende-Wrapper-
  Äquivalenz).
- **Warum:** Nachweis am echten Git-Repo mit vorhandener Fixture statt
  neuer Infrastruktur; sichert den kritischen Teil (Verhaltens-
  erhaltung) integrationseitig.

## Tests

- [ ] `ParseGitDiffHunkRanges_WithMultiFileDiff_ParsesCompactRangesPerFile`
- [ ] `ParseGitDiffHunkRanges_WithSingleLineAndZeroCountHunks_MapsRangesExactly`
- [ ] `ExpandHunkRanges_ProducesLegacyExpandedLines` (Äquivalenz zu `ParseGitDiffHunks`)
- [ ] `CreateChangedSymbolEntry_ForMethodSymbols_CarriesIdAccessibilityKindAndSpan` (CreateScenario)
- [ ] `CreateChangedSymbolEntry_ForLocalFunction_UsesDeterministicFallbackId`
- [ ] `ToCallSiteEntries_MapsReferencesCallSitesOrderAndFieldsIdentically`
- [ ] Integration: `AnalyzeDiffAsync_OnModifiedWorkspace_MatchesEntriesWrapperAndCarriesStructuredData`
- [ ] Alle Bestands-Tests rund um `get_impact`/`find_references` bleiben UNVERÄNDERT grün (Abwärtskompatibilität)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `RunGitDiff` läuft pro Analyse weiterhin genau einmal (Code-Lage
      unverändert: nur der neue Kern ruft ihn)
- [ ] `callers`-Ausgabe inkl. Reihenfolge byte-identisch (Bestandstests
      ohne Anpassung grün)
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün (beide Nicht-Stress-Projekte)
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-002/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Grenzwerte-produktion` — MaxLineCount 500
  (Modelle in eigener Datei), MaxMethodParameterCount 4 (Kern-Methode
  behält 4 Parameter), `sealed`/`#nullable enable`, MaxBoolParameterCount 1.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien-design-philosophie`
  — Records für unveränderliche Datenstrukturen, Roslyn-Zugriffe sparsam,
  eigener MCP-Server proaktiv nutzen (Dogfooding).
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention` —
  Zero-Warning, DRY (keine zweite Stabile-ID-/Parser-Implementierung),
  Kommentare ohne Task-/Step-/EPIC-Referenzen.

## Bekannte Ausnahmen

- Keine bekannten flaky Tests. Der neue Integrationstest startet `git`
  als Subprozess wie die bestehenden `GitImpactMiniFixtureWorkspace`-
  Tests — gleiche Laufzeitklasse, kein Stress-Tag nötig.

## Code-Skizze (optional)

```csharp
internal sealed record DiffImpactAnalysis(
    string RepositoryRoot,
    string? SinceRef,
    IReadOnlyList<ChangedFileRange> ChangedFiles,
    IReadOnlyList<ChangedSymbolEntry> ChangedSymbols,
    ReferenceTraversalResult References);

// Wrapper (Signatur/Verhalten unverändert):
internal static async Task<List<CallSiteEntry>> AnalyzeEntriesAsync(
    Solution solution, string targetPath, string? gitSinceRef, bool verbose)
{
    var analysis = await AnalyzeDiffAsync(solution, targetPath, gitSinceRef, verbose);
    if (analysis is null) return [];
    return analysis.References.CallSites.Select(ToCallSiteEntry).ToList();
}
```

## Notes

- **Reihenfolge-/Dedup-Falle (wichtigste Stolperfalle):** Die heutige
  `AnalyzeEntriesAsync`-Ausgabe sortiert NICHT und dedupliziert Einträge
  NICHT — nur die Symbole werden vorab via
  `Distinct(SymbolEqualityComparer.Default)` (Erstvorkommen) vereinigt.
  `TraversalState.CreateResult` macht dagegen `Distinct()` + mehrstufiges
  `OrderBy`. Die Abbildung in `TransitiveCallSiteEntry` darf deshalb
  weder sortieren noch deduplizieren, sonst driftet die `callers`-Text-
  ausgabe. Ebenso `SymbolName` unverändert als
  `"EnthaltenderTyp.Membername"` des gesuchten Symbols übernehmen —
  nicht auf `FormatSymbolName(reference.Definition)` wechseln (anderes
  Format, z. B. bei Konstruktoren).
- **Namespace-Entscheidung bewusst und hier begründet (Anti-Loop-Check
  gegen CodeMap):** `AiNetLinter.Core` referenziert neu
  `AiNetLinter.Mcp.Tools.SymbolGraph` (wegen `ReferenceTraversalResult`).
  Monolith (Richtlinien §1/§2: keine Assembly-Grenzen, keine DI-Layer);
  die Alternative — Umzug von `TransitiveCallGraphModels` nach Core —
  würde mehrere MCP-Dateien churnen, ohne Verhalten zu ändern. Die
  CodeMap führt `TransitiveCallGraphModels.cs` ausdrücklich als
  Wiederverwendungsquelle; dieser Step wiederverwendet, statt zu
  verschieben. Keiner früheren Entscheidung widersprochen.
- `ChangedFileRange.FilePath` bleibt repo-root-relativ (wie die Diff-
  Keys heute), `ChangedSymbolEntry.FilePath` ist solution-relativ via
  `PathNormalizer.ToRelative(Path.GetDirectoryName(solution.FilePath))`
  — konsistent zu `CallSiteEntry`/`TransitiveCallSiteEntry`. Beide
  Bedeutungen im XML-Doc festhalten.
- `depth` bleibt im gesamten Git-Branch wirkungslos (Audit D.3): die
  References-Completeness meldet RequestedDepth=EffectiveDepth=1 — das
  ist Dokumentation des Ist-Zustands, keine neue Semantik.
- Kein verstecktes bool-Flag: Dieser Step berührt den Scanner-Scope gar
  nicht; der nächste Step (breiter Pfad) bekommt einen klar benannten
  zweiten Scannerpfad bzw. expliziten Scope-Parameter (Konzept §Internes
  Ergebnisobjekt, letzter Absatz).
- Dogfooding laut Richtlinien: nach Umbau `metrics_lookup` für geänderte
  Symbole (Methoden ≤60 Zeilen halten) und `find_duplicates` gegen die
  neuen Helper laufen lassen; MCP-Server-Neustart nach Build einkalkulieren
  (erst `get_server_health`).
- Tech-Debt TD-001 (`auto_fixable: nein`, Test-Fixture-Ergonomie) berührt
  diesen Bereich nicht → nicht angehängt (planer-SKILL Schritt 3).
