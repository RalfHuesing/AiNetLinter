---
status: done
type: step-plan
task: 03_get-impact-zum-diff-kontext-erweitern
step: 003
corrects: null
title: "Breiter Diff-Symbolscanner (change-context-Scope) mit kollisionsfreien stabilen IDs"
epic: EPIC-2
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: stealth/ox-alpha
created_by_model_knowledge_cutoff: unbekannt
created_at: 2026-08-22T21:15:00+02:00
related_to: [step-001, step-002]
---

# Step 003: Breiter Diff-Symbolscanner (change-context-Scope) mit kollisionsfreien stabilen IDs

## Bezug

- **Task:** `03_get-impact-zum-diff-kontext-erweitern`
- **Epic:** `EPIC-2` aus `roadmap.md` — Teil 2 von 2 (Planungsnotiz): Teil 1
  (strukturiertes Ergebnisobjekt) ist mit step-002 done/approved; offen ist
  der breite Diff-Symbolscanner. Der Epic-Haken bleibt bis zu diesem Step
  offen. Das Tool-Wiring (`detailLevel=change-context`, Caps, Antwortform)
  gehört bewusst NICHT hierher — das ist EPIC-6.
- **Konzept-Referenz:** `Konzept.md` §Scope Must-have (breiter Symbolscope,
  innerste Deklaration pro Zeile, partielle Typen über Datei + Spanne,
  stabile ID, Accessibility/Kind/Anzeigename/Projekt/Datei/Deklarationszeilen,
  private Methode erscheint ohne externe Call-Sites, bisheriger
  `callers`-Scope unverändert), §Internes Ergebnisobjekt letzter Absatz
  (zwei klar benannte Scannerpfade ODER expliziter Scope-Parameter — KEIN
  verstecktes bool-Flag), Audit A.2 (heutiger Filter `IsPublicOrInternal`)
  und D.4 (stabile IDs lokale Funktionen).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des aktuellen Codes nach step-002 vorgefunden (verifiziert am
Dateistand und teils live über den eigenen MCP-Server):

- **Einhängepunkt steht:** `DiffImpactAnalyzer.AnalyzeDiffAsync`
  (internal, 4 Parameter) baut `DiffImpactAnalysis` und delegiert die
  Symbolermittlung an `GetChangedSymbolsFromHunksAsync` →
  `GetChangedSymbolsAsync` — dort hart verdrahtet: nur
  `MethodDeclarationSyntax` + `ConstructorDeclarationSyntax`, gefiltert
  durch `IsPublicOrInternal` (`DiffImpactAnalyzer.cs:273-343`). Genau
  diese Stelle wird der zweite, klar benannte Scannerpfad.
- **`DiffImpactAnalyzer.cs` hat 485 von 500 Zeilen** (`MaxLineCount`): der
  breite Scanner (Kandidatenmenge, Innerste-Deklaration-Filter,
  Display-Namen) passt hier nicht mehr hinein → eigene Core-Datei, der
  Analyzer behält nur dünne Eintrittspunkte. (CodeMap entsprechend
  gepflegt.)
- **Stabile-ID-Quelle existiert genau einmal:** 
  `CallGraphTraversal.GetStableSymbolId` (internal, Z.121-123) =
  `DocumentationCommentId.CreateDeclarationId(symbol) ??
  ToDisplayString(FullyQualifiedFormat)`. Live per `find_references`
  verifizierte Nutzer: `CreateChangedSymbolEntry` (Z.351),
  `BuildReferencesAsync` (Z.378, → `ReachedFromSymbolId`),
  `CallGraphTraversal.CreateCallSiteEntry` (Z.114) plus ein Test.
- **TD-002 (Wirkstelle genau hier):** `CreateDeclarationId` liefert für
  lokale Funktionen die Doc-ID der *einschließenden Methode* (im Test
  `CreateChangedSymbolEntry_ForLocalFunction_UsesSharedStableIdLogic`
  gepinnt) — der Fallback greift nie. Sobald der breite Scanner lokale
  Funktionen einschließt, bekämen ALLE lokalen Funktionen einer Methode
  denselben `SymbolId`. Wichtig: der step-002-Pinntest asserted nur
  „identisch zu `GetStableSymbolId`, deterministisch, ≠ Fallback“ — ein
  Sonderfall INNERHALB von `GetStableSymbolId` lässt ihn unverändert grün.
- **Lokale Funktionen als Reached-From-Knoten sind heute real:**
  `NormalizeToOwningMember` (`Core/RoslynSymbolExtensions.cs`) mappt nur
  Accessoren auf ihr Property/Event hoch — lokale Funktionen bleiben
  erhalten. Eine Call-Site im Body einer lokalen Funktion trägt daher
  HEUTE schon deren (mehrdeutige) Doc-ID als `ReachedFromSymbolId` in
  `find_references`/get_impact-Symbol-Branch. Der Sonderfall fixt diese
  latente Mehrdeutigkeit mit.
- **Keine wiederverwendbare breite Deklarations-Erkennung existiert:** die
  Fundstellen für `LocalFunctionStatementSyntax` (Checkers,
  DuplicateDetection, MagicValuesClassifier, OutgoingCallScanner) lösen
  jeweils andere Probleme (Containment für Komplexität, Duplikatsammlung,
  Body-Auflösung). Ein Innerste-Deklarations-Scanner muss neu gebaut
  werden; wiederverwendet werden stattdessen die Bausteine
  `IntersectsWithChangedLines` (Range-Überlappung),
  `ParseGitDiffHunkRanges`/`BuildChangedFiles`, `ChangedSymbolEntry`,
  `BuildReferencesAsync`.
- **Partielle Typen/partielle Methoden sind ein echter Korrektheitspunkt:**
  `CreateChangedSymbolEntry` leitet Datei/Spanne aus
  `symbol.Locations.First(IsInSource)` ab. Bei partiellen Typen (ein
  gemergtes ISymbol, mehrere Deklarationen) und partiellen Methoden zeigt
  das FIRST-Location nicht notwendigerweise auf die geänderte Datei. Die
  heutige Kandidatenmenge (Methoden/Konstruktoren, nicht-partiell im
  Normalfall) trifft das praktisch nicht — die breite Menge (Typen!) schon.
- Regeldateien gelesen (Schritt 4a): Grenzwerte (Datei ≤500 Zeilen,
  Methode ≤60 Zeilen, ≤4 Parameter → sonst Input-Record, `sealed`,
  `#nullable enable`, MaxBoolParameterCount 1), Richtlinien §1 (Records,
  Roslyn-Zugriffe sparsam, Dogfooding) und §5 (DRY/eine Parse- bzw.
  ID-Wahrheit, Zero-Warning, Kommentar-Disziplin ohne Task-/TD-Referenzen).

## Intention

Nach diesem Step gibt es im internen Analyzer einen zweiten, klar
benannten Scannerpfad `change-context`, der den vollen Konzept-Scope
(private/protected/internal/public Methoden und Konstruktoren,
Properties/Indexer, Events, Felder, Typdeklarationen, lokale Funktionen)
als `ChangedSymbolEntry`s liefert — pro geänderter Zeile die innerste
passende Deklaration, partielle Deklarationen über Datei + Spanne
unterscheidbar, mit kollisionsfreien stabilen IDs auch für lokale
Funktionen (TD-002-Auflösung). Der bisherige `callers`-Pfad bleibt
Verhalten, Signatur und Ausgabe nach identisch; Referenz-/Call-Site-Stufe
wird unverändert gemeinsam genutzt. Sichtbar wird das alles erst mit
EPIC-6 am Tool — dieser Step liefert die interne Fähigkeit plus Tests.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Core/DiffSymbolScanner.cs` (neu)

- **Was:** Der breite Scannerpfad, `internal static`, `#nullable enable`:
  - Enum `DiffSymbolScope` (eigener kleiner Typ, keine bool-Flag):
    `Callers` (heutiger schmaler Pfad) und `ChangeContext` (breiter Pfad).
  - Kandidatensammlung pro Dokument über die Syntaxwurzel:
    `MethodDeclarationSyntax`, `ConstructorDeclarationSyntax`,
    `PropertyDeclarationSyntax`, `IndexerDeclarationSyntax`,
    `EventDeclarationSyntax`, `EventFieldDeclarationSyntax`,
    `FieldDeclarationSyntax`, `TypeDeclarationSyntax` (class/struct/
    interface/record), `EnumDeclarationSyntax`, `DelegateDeclarationSyntax`,
    `LocalFunctionStatementSyntax`. Bewusst NICHT: Accessor-Deklarationen
    (gehen über Containment in ihrem Property/Indexer/Event auf), lambdas/
    anonyme Funktionen, lokale Variablen/Parameter/Statements (Non-Goal).
  - Innerste-Deklaration-Regel: sammle Kandidaten, deren Spanne mit den
    Hunk-Ranges überlappt (Wiederverwendung der bestehenden
    Range-Überlappungslogik), und wirke danach jeden Kandidaten weg, dessen
    Spanne eine andere passende Kandidatenspanne vollständig enthält —
    übrig bleibt je geänderter Bereich die innerste Deklaration (Methode
    statt enthaltendem Typ; lokale Funktion statt ihrer Methode).
  - Entry-Bildung pro Kandidaten-KNOTEN (nicht pro Symbol):
    SemanticModel.GetDeclaredSymbol(node), Spanne/Datei aus dem Knoten
    selbst (`node.GetLocation()`), damit partielle Typen/partielle Methoden
    je geänderter Teildeklaration korrekt und unterscheidbar erscheinen
    (gleiches `SymbolId`, verschiedene FilePath/Spanne — genau der
    Konzept-Wortlaut). Accessibility/Kind unverändert aus dem Symbol
    (lokale Funktionen tragen `NotApplicable` bzw. den Symbolwert — wie
   Roslyn ihn liefert — und `Kind="Method"`).
  - Artabhängiger Anzeigename: Member wie bisher `"EnthaltenderTyp.Name"`
    (dazu wird der bestehende Analyzer-Helper `FormatMemberDisplayName`
    von `private` auf `internal` erweitert und delegiert genutzt — der
    Ausdruck bleibt eine Quelle der Wahrheit); Typdeklarationen
    `"Namespace.Name"` bzw. verschachtelt `"EnthaltenderTyp.Name"`
    (ohne Namespace nur `"Name"`); lokale Funktionen
    `"EnthaltendeMethode-im-bisherigen-Format.Name"` (z. B.
    `LocalFuncHost.Run.Scale`).
  - Accessibility-Filter: keiner (alle vier Stufen + lokale Funktionen);
    das ist DER Unterschied zum `Callers`-Pfad neben der Kandidatenmenge.
- **Warum:** Eigene Datei wegen `MaxLineCount` 500 (Analyzer bei 485);
  Konzept fordert den breiten Scope als klar benannten zweiten Pfad;
  Wiederverwendung der bestehenden Parse-/Range-/Entry-Bausteine statt
  zweiter Implementierung (Richtlinien §5).

### Datei 2: `src/AiNetLinter/Core/DiffImpactAnalyzer.cs`

- **Was:** Dünne Umschaltung auf zwei Pfade ohne Verhaltensänderung des
  Bestands:
  - `AnalyzeDiffAsync` (Signatur/Verhalten unverändert) und neuer
    Einstiegspunkt `AnalyzeChangeContextAsync(Solution, string, string?,
    bool)` (ebenfalls 4 Parameter) laufen beide in einen privaten Kern, der
    einen Request-Record (Solution, TargetPath, GitSinceRef, Verbose,
    DiffSymbolScope) nimmt — jede Methode bleibt ≤4 Parameter, kein
    verstecktes bool-Flag, Git läuft weiterhin genau einmal im Kern.
  - Die Symbolermittlung reicht den Scope durch:
    `DiffSymbolScope.Callers` → exakt die heutige Logik
    (Methoden/Konstruktoren + `IsPublicOrInternal`, unverändert);
    `DiffSymbolScope.ChangeContext` → `DiffSymbolScanner`. Der
    Innerste-Filter ist auf dem schmalen Pfad ein No-op (dessen
    Kandidaten enthalten sich gegenseitig nicht) — trotzdem uniform
    angewendet, damit es nur eine Regel gibt.
  - `CreateChangedSymbolEntry` bekommt eine Überladung mit expliziter
    Location/dokumenttreuer Spanne (für knotenbasierte Entries); die
    BESTEHENDE Signatur bleibt bestehen und delegiert mit
    `Locations.First(IsInSource)` — Bestands-Tests bleiben unangetastet
    grün.
  - `RunGitDiff`, Parsing, Wrapper `AnalyzeEntriesAsync`, CLI
    `AnalyzeAsync`, References-Stufe: unberührt.
- **Warum:** Konzept §Internes Ergebnisobjekt (zwei benannte Pfade, kein
  stillschweigender Scope-Wechsel); Grenzwerte (Parameterzahl); minimale
  Regressionsoberfläche für den snapshot-kompatiblen `callers`-Modus.

### Datei 3: `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs`

- **Was:** `GetStableSymbolId` um deterministischen Sonderfall für lokale
  Funktionen erweitern (TD-002-Auflösung, Kernvertrag des Scanners):
  - Für `IMethodSymbol` mit `MethodKind.LocalFunction`: Basis-ID =
    stabile ID des nächsten einschließenden Members, das keine lokale
    Funktion ist (`ContainingSymbol`-Aufstieg; verschachtelte lokale
    Funktionen landen so alle bei derselben Basismethode), plus
    deterministisches Suffix `#lf:<Name>@<Zeile>:<Spalte>` aus Name und
    Deklarationsstartposition (1-basiert, aus der Symbol-Location).
    Beispiel: `M:Ns.C.Run(System.Int32)~System.Int32#lf:Scale@5:32`.
    Zwei lokale Funktionen derselben Methode bekommen damit zwingend
    verschiedene IDs; gleicher Codezustand liefert dieselbe ID.
  - Nicht-lokale Symbole: Pfad UND Ergebnis exakt wie bisher (DocCommentId
    ?? FullyQualified-Fallback) — dadurch bleiben alle bestehenden IDs und
    damit `callers`-Snapshots byte-identisch.
  - Die XML-Doc der Methode wird dabei korrekt neu formuliert: der
    falsche Fallback-Beispielverweis „z. B. lokale Funktionen“ (MINOR aus
    dem step-002-Review, `CallGraphTraversal.cs:119`) entfällt — der
    Sonderfall ist jetzt ja explizit beschrieben. Diese Doc-Zeile liegt
    im selben Dok-Block, den dieser Step ohnehin umbaut: maximale Nähe,
    null Verhaltensrisiko.
- **Warum:** TD-002 verlangt die Entscheidung im Plan DIESES Steps
  (tech-debt.md: „Die ID-Schema-Entscheidung gehört in den Plan des
  nächsten Steps“). Fix in der GEMEINSAMEN Quelle statt eines
  Scanner-lokalen Wrappers, weil `ChangedSymbolEntry.SymbolId` und
  `ReachedFromSymbolId` (beide laufen durch `GetStableSymbolId`) per
  Konstruktion konsistent bleiben müssen — EPIC-4/EPIC-6 joinen später
  darüber. Scanner-lokal würde zwei ID-Wahrheiten erzeugen und die
  latente Traversal-Mehrdeutigkeit bestehen lassen.

### Datei 4: `src/AiNetLinter/Core/DiffImpactAnalysisModels.cs`

- **Was:** Nur XML-Doc an `ChangedSymbolEntry.SymbolId`: Vertragstext um
  den lokalen-Funktions-Sonderfall ergänzen („DocCommentId oder
  deterministischer Fallback; lokale Funktionen mit deterministischem
  `#lf:`-Sonderfall aus Name + Deklarationsposition“). Kein Record ändert
  sich — Accessibility/Kind/Spanne/Felder decken den breiten Scope bereits
  vollständig ab.
- **Warum:** Doku-Objektivität (Richtlinien §1): die Vertragsbeschreibung
  muss dem realen ID-Verhalten entsprechen, sobald lokale Funktionen
  Scopesymbole werden.

### Datei 5: `src/AiNetLinter.FastTests/Core/DiffImpactAnalyzerBroadScopeTests.cs` (neu)

- **Was:** Unit-Tests über `McpInMemoryTestContext.CreateScenario` +
  synthetische Hunk-Ranges (Muster der step-002-Tests, serverlos):
  Differentialtest `Callers` vs. `ChangeContext` (private Methode nur im
  breiten Pfad); Innerste-Deklaration (Body-Hunk → nur Methode, nicht der
  Typ; Hunk im LF-Body → nur die lokale Funktion); Property-Getter-Hunk →
  genau ein Property-Entry (kein Accessor, kein Typ); Feld-Initializer →
  Field-Entry mit Kind/Accessibility; Event-Deklaration → Event-Entry;
  partieller Typ in zwei Dateien → zwei Entries, gleiches `SymbolId`,
  unterschiedliche FilePath/Spanne; zwei lokale Funktionen in einer
  Methode → zwei verschiedene, deterministische `#lf:`-IDs ≠ Doc-ID der
  Methode; Displayname-Verträge (Namespace-Typ, verschachtelter Typ,
  lokale Funktion). Neuer Testdatei-Name, weil die bestehende
  `DiffImpactAnalyzerTests.cs` sonst ans Datei-Limit stößt.
- **Warum:** xUnit-Pflicht (Richtlinien §4); reine Syntax/Mapping-Logik
  ohne Subprozess absicherbar.

### Datei 6: `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs`

- **Was:** Ein Ende-zu-Ende-Test auf `GitImpactMiniFixtureWorkspace` (nach
  dem Initial-Commit eine private Methode ändern):
  `AnalyzeChangeContextAsync` direkt aufrufen → die private Methode steht
  in `ChangedSymbols` (auch ohne jegliche Call-Sites:
  `References.CallSites` leer, Completeness vollzählig), und der
  schmale Wrapper `AnalyzeEntriesAsync` auf derselben Workspace enthält
  sie NICHT (Pfad-Trennung am echten Git-Repo nachgewiesen).
- **Warum:** Konzept-Muss-Have „Diff an privater Methode erscheint, auch
  ohne externe Call-Sites“ + Abwärtskompatibilitätsnachweis des
  `callers`-Pfads in einem Zug.

## Tests

- [ ] `ChangeContext_ReportsPrivateMethodThatCallersOmits` (Differential, CreateScenario)
- [ ] `ChangeContext_BodyHunk_ReportsInnermostMethodWithoutContainingType`
- [ ] `ChangeContext_HunkInsideLocalFunction_ReportsOnlyTheLocalFunction`
- [ ] `ChangeContext_PropertyGetterHunk_ReportsExactlyOnePropertyEntry`
- [ ] `ChangeContext_FieldInitializerChange_ReportsFieldEntryWithKindAndAccessibility`
- [ ] `ChangeContext_EventDeclarationChange_ReportsEventEntry`
- [ ] `ChangeContext_PartialTypeInTwoFiles_TwoEntriesDistinctByFileAndSpan_SameSymbolId`
- [ ] `GetStableSymbolId_TwoLocalFunctionsInOneMethod_DistinctDeterministicIdsWithLfMarker`
- [ ] `ChangeContext_DisplayNames_FollowTypeNestedAndLocalFunctionContract`
- [ ] Integration: `AnalyzeChangeContextAsync_OnModifiedPrivateMethod_ListsSymbolWithoutCallSites_AndCallersWrapperOmitsIt`
- [ ] Alle Bestands-Tests rund um `get_impact`/`find_references`/`AnalyzeEntriesAsync` bleiben UNVERÄNDERT grün (inkl. `CreateChangedSymbolEntry_ForLocalFunction_UsesSharedStableIdLogic`)

## Definition of Done

- [ ] Alle „Konkrete Änderungen“ umgesetzt
- [ ] Zwei klar benannte Pfade (`AnalyzeDiffAsync` = Callers unverändert,
      `AnalyzeChangeContextAsync` = ChangeContext); kein bool-Flag
- [ ] `callers`-Ausgabe inkl. Reihenfolge byte-identisch (Bestandstests
      ohne Anpassung grün)
- [ ] Stabile IDs kollisionsfrei für mehrere lokale Funktionen pro Methode
      (TD-002-Vertrag im Code und in der XML-Doc); step-result dokumentiert
      die Entscheidung
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün (Zero-Warning)
- [ ] Test-Command aus Tech-Stack-Notiz grün (beide Nicht-Stress-Projekte)
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-003/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Grenzwerte-produktion` — MaxLineCount 500
  (neue Scanner-Datei; neue Testdatei), MaxMethodLineCount 60,
  MaxMethodParameterCount 4 (Request-Record statt fünftem Parameter),
  `sealed`/`#nullable enable`, MaxBoolParameterCount 1 (Enum statt Flag).
- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien-design-philosophie`
  — Records, Roslyn-Zugriffe sparsam (ein Syntax-/SemanticModel-Pass pro
  Dokument; keine zusätzliche Vollsolution-Schleife), eigener MCP-Server
  proaktiv nutzen (Dogfooding).
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention` —
  DRY (eine ID-Wahrheit, eine Überlappungsregel, eine
  DisplayName-Quelle), Zero-Warning, Kommentare ohne Task-/TD-/EPIC-
  Referenzen.

## Bekannte Ausnahmen

- Keine bekannten flaky Tests. Der Integrationstest startet `git` wie die
  bestehenden `GitImpactMiniFixtureWorkspace`-Tests — gleiche Laufzeit-
  klasse, kein Stress-Tag nötig.

## Code-Skizze (optional)

```csharp
// CallGraphTraversal.cs — TD-002-Vertrag (nur Skizze):
internal static string GetStableSymbolId(ISymbol symbol)
{
    if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction } local)
    {
        var container = local.ContainingSymbol;
        while (container is IMethodSymbol { MethodKind: MethodKind.LocalFunction })
        {
            container = container.ContainingSymbol;
        }
        var position = local.Locations.First(l => l.IsInSource)
            .GetLineSpan().StartLinePosition;
        return $"{GetStableSymbolId(container!)}#lf:{local.Name}@{position.Line + 1}:{position.Character + 1}";
    }
    return DocumentationCommentId.CreateDeclarationId(symbol) ??
           symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}

// DiffImpactAnalyzer.cs — zwei benannte Eintrittspunkte (nur Skizze):
internal static Task<DiffImpactAnalysis?> AnalyzeDiffAsync(
    Solution solution, string targetPath, string? gitSinceRef, bool verbose)
    => RunAnalysisAsync(new DiffAnalysisRequest(solution, targetPath, gitSinceRef, verbose, DiffSymbolScope.Callers));

internal static Task<DiffImpactAnalysis?> AnalyzeChangeContextAsync(
    Solution solution, string targetPath, string? gitSinceRef, bool verbose)
    => RunAnalysisAsync(new DiffAnalysisRequest(solution, targetPath, gitSinceRef, verbose, DiffSymbolScope.ChangeContext));
```

## Notes

- **TD-002-Auflösung (bewusst so, Alternativen verworfen):** Der Sonderfall
  sitzt IN `GetStableSymbolId`, nicht im Scanner. Gründe: (1)
  Join-Garantie — `ChangedSymbolEntry.SymbolId` und
  `TransitiveCallSiteEntry.ReachedFromSymbolId` entstehen am selben
  Ausdruck, EPIC-4/6 können ohne Mapping-Layer joinen; (2) der Schritt
  fixt damit auch die heute schon existierende Mehrdeutigkeit von
  Reached-From-IDs bei Call-Sites IM Body lokaler Funktionen
  (`NormalizeToOwningMember` hebt lokale Funktionen NICHT auf — geprüft
  in `Core/RoslynSymbolExtensions.cs`); (3) der step-002-Pinntest bleibt
  unverändert grün, weil er nur Quellen-, Determinismus- und
  Fallback-Eigenschaften assertet. Verworfen: Scanner-lokaler Wrapper
  (zweite ID-Wahrheit) und Zusatzfelder allein (IDs blieben mehrdeutig
  für EPIC-4-Matches über SymbolId). Verhaltensanmerkung für die
  Doku-Pflicht in EPIC-7: die Reached-From-ID lokaler-funktionsumgebener
  Call-Sites in `find_references`/Symbol-Branch ändert ihren String-Wert
  (von mehrdeutig zu eindeutig) — bewusste Korrektur, keine Regression
  des `callers`-Git-Pfads (dessen Scopesymbole sind nie lokale
  Funktionen). Die EPIC-7-Zeile in `roadmap.md` wurde beim
  Roadmap-Abgleich entsprechend präzisiert.
- **MINOR `CallGraphTraversal.cs:119` angehängt (Nähe/Risiko geprüft):**
  die falsche Beispielangabe steht im selben XML-Doc-Block, den der
  ID-Sonderfall sowieso neu formuliert — eine Doc-Zeile im selben
  Edit, kein eigenes Batch-Item nötig, Verhalten unberührt. Hier
  explizit verplant, damit es nicht stillschweigend mitläuft.
- **Anti-Loop-Check (CodeMap):** Dieser Step widerspricht keiner
  festgehaltenen Entscheidung — er setzt genau die Fortsetzung um, die
  step-002 (Result-Beobachtungen) und die Roadmap-Planungsnotiz
  vorbereitet haben: gemeinsame ID-Quelle wird ERWEITERT (nicht ersetzt),
  `Core → Mcp.Tools.SymbolGraph`-Using bleibt das etablierte Muster.
- **Performance-Bewusstsein, aber richtige Ebenentrennung:** der breite
  Scope erhöht die Symbolzahl und damit die per-Symbol-
  `FindReferencesAsync`-Läufe in der unverändert wiederverwendeten
  References-Stufe. Kappung VOR teuren Folgeanalysen ist laut Konzept
  Aufgabe der Tool-Ebene (EPIC-6, `maxChangedSymbols` vor
  Folgeanalysen) — der Analyzer bleibt intern kapplungslos, wie heute.
  Nicht in diesen Step ziehen.
- **Non-Goal-Wachsamkeit:** keine Lambdas/anonymen Funktionen, keine
  Accessoren als eigenständige Ziele, keine lokalen Variablen; kein
  Tool-Wiring, kein `Docs/agent-api.md`/README-Touch (EPIC-6/EPIC-7).
- **Dogfooding laut Richtlinien:** nach Umbau `metrics_lookup` über die
  neuen/geänderten Scanner-Symbole (≤60 LOC/Methode, ≤4 Parameter) und
  `find_duplicates` gegen die neuen Helper (Erwartung: keine Cluster,
  da bewusst delegiert statt dupliziert); bei „lädt noch“ zuerst
  `get_server_health`.
