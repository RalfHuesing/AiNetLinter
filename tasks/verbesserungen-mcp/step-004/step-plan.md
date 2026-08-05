---
status: open
type: step-plan
task: verbesserungen-mcp
step: 004
title: "EPIC-03-Batch: get_symbol_body-ID-Korruption, get_violations-Meldung, ainetlinter://overview-Status-Race, depth-Hard-Cap-Doku"
epic: EPIC-03
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "Property-/Event-Accessor-Symbol zentral in ResolveSymbolAsync auf Owner normalisieren (ID-Korruption, alle vier Symbolgraph-Tools)"
    source: "Konzept.md Scope P2 'get_symbol_body-ID-Korruption beheben'; Wo-im-Projekt: GetSymbolBodyTool.cs (Ursache liegt im gemeinsamen FindReferencesTool.ResolveSymbolAsync, siehe step-003)"
  - id: item-02
    title: "get_violations: 'N Dateien im Scope, 0 Violations' von 'keine Datei im Scope' unterscheiden"
    source: "Konzept.md Scope P2 'get_violations-Meldung praezisieren'; GetViolationsScanner.cs:113-121"
  - id: item-03
    title: "McpCodeGraphServer.LoadState: Race zwischen abgeschlossenem Hintergrund-Load und Catalog-Adoption beheben"
    source: "Konzept.md Scope P3 'ainetlinter://overview-Status synchronisieren'; OverviewResourceRegistration.DescribeSolution"
  - id: item-04
    title: "find_references/get_impact: 200-Knoten-Hard-Cap in Tool-Beschreibung dokumentieren"
    source: "Konzept.md Scope P3 'depth-Hard-Cap dokumentieren'; CallGraphTraversal.cs:25 (MaxRecursionNodes)"
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T15:30:00Z
related_to: [step-003]
---

# Step 004: EPIC-03-Batch — vier P2/P3-Konsistenz-Fixes

## Bezug

- **Task:** `verbesserungen-mcp`
- **Epic:** `EPIC-03` aus `roadmap.md` — Cluster der vier Muss-Haben-Punkte
  (P2/P3), explizit für Micro-Batching vorgesehen. Das eine Nice-to-Have
  (EII-Darstellung) ist **nicht** Teil dieses Batches (siehe „Bewusst
  ausgeklammert" unten).
- **Konzept-Referenz:** `Konzept.md` Scope „P2 — get_symbol_body-ID-
  Korruption beheben", „P2 — get_violations-Meldung präzisieren",
  „P3 — ainetlinter://overview-Status synchronisieren", „P3 —
  find_references/get_impact depth-Hard-Cap dokumentieren".
- **`related_to: [step-003]`:** Pointer, kein Cache — item-01 sitzt im
  selben Code-Cluster (`FindReferencesTool.ResolveSymbolAsync`-Umfeld),
  den step-003 zuletzt geändert hat. Vor Umsetzung von item-01 den
  aktuellen Stand von `GetSymbolBodyTool.cs`/`FindReferencesTool.cs`
  erneut lesen (nicht step-003s Plan-Beschreibung ungeprüft übernehmen).

## Aktueller Projektzustand (JIT-Kontext)

Vollständig gelesen: `GetSymbolBodyTool.cs`, `GetViolationsScanner.cs`,
`OverviewResourceRegistration.cs`, `McpCodeGraphServer.cs`,
`ServerLoadState.cs`, `CallGraphTraversal.cs`, `FindReferencesTool.cs`,
`SymbolIdentifierResolver.cs`, `SymbolGraphToolRegistrations.cs`, sowie
die zugehörigen Testklassen und die `SymbolGraphMini`-Test-Fixture
(`Greeter.cs`, `Hierarchy.cs`, `Caller.cs`). Für item-01 und item-03
zusätzlich per isoliertem Roslyn-Repro-Programm (außerhalb des Repos,
gegen exakt `Microsoft.CodeAnalysis.CSharp` 5.6.0 — dieselbe Version wie
`AiNetLinter.csproj` nach dem step-002-Bump) den tatsächlichen Root
Cause verifiziert, nicht nur vermutet.

### item-01 — Root Cause verifiziert, betrifft Property-/Event-Accessoren, nicht Generics/EII

**Wichtigstes Ergebnis dieses Planer-Laufs:** step-003 hat an der
Ursache **nichts** geändert (step-003s eigener Plan-Text sagt das
bereits explizit voraus, siehe `step-003/step-plan.md` „Bezug zum
P2-Punkt") — `ResolveSymbolAtToken`/`ResolveByPositionAsync` sind seit
step-003 unverändert. Der Bug **besteht unverändert weiter**.

Per Roslyn-Repro (identische Package-Version) systematisch jede
Token-Position einer Test-Klasse mit Property, generischer Methode,
expliziter Interface-Implementierung, Indexer, Event und Operator
durchprobiert und mit der ID verglichen, die
`SkeletonSyntaxWalker.BuildMethodInfo`/`BuildPropertyInfo` (jeweils
`_semanticModel.GetDeclaredSymbol(node)` direkt auf dem
Deklarations-Node) für dasselbe Mitglied liefert:

- **Generische Methoden funktionieren bereits korrekt** — Position auf
  dem Methodennamen liefert dieselbe ID wie das Skeleton (inkl. des für
  Menschen ungewohnten, aber standardkonformen `` `` ``-Arity-Encodings,
  z. B. `` M:Ns.Type.Identity``1(``0)~``0 `` — das ist kein Bug, analog
  zur bereits als „kein Bug" eingestuften EII-`#`-Notation in
  `Konzept.md` „Entdeckte Mängel").
- **Der tatsächliche Bug:** Bei einer kompakten Property-Deklaration
  (`public string Name { get; set; }`, die typische Ein-Zeiler-Form)
  liefert eine Position auf dem `get`- oder `set`-Schlüsselwort (statt
  auf dem Property-Namen) über `ResolveSymbolAtToken` den
  **Accessor-`IMethodSymbol`** (`get_Name`/`set_Name`) statt des
  Property-Symbols. `GetSymbolBodyTool.TryGetDeclarationId` ruft darauf
  direkt `DocumentationCommentId.CreateDeclarationId` auf und liefert
  `M:Ns.Type.get_Name~System.String` statt der von `get_file_skeleton`
  gezeigten `P:Ns.Type.Name` — eine Methoden-ID, die den Property-Namen
  in einer für den Wortlaut aus `Konzept.md` („verschachtelte/doppelte
  **Methoden**-ID") plausiblen, tatsächlich reproduzierbaren Weise
  „einbettet" (Praefix `get_`/`set_` + Rückgabetyp-Suffix `~...`), wo
  ein Agent die kompakte `P:...`-ID erwartet. Dieselbe Kategorie Fehler
  träte bei Event-Accessoren (`add_`/`remove_`) auf, aktuell aber ohne
  bekanntes Repro in der Fixture (Event-Feld-Deklarationen haben keine
  separaten `add`/`set`-Keyword-Positionen im Quelltext).
  Explizite Interface-Implementierungen sind **nicht** betroffen, wenn
  die Position auf dem Methodennamen selbst liegt (liefert dieselbe
  `#`-kodierte ID wie das Skeleton) — nur eine Position auf dem
  Interface-Namen-Teil (`IFoo.` vor dem Methodennamen) liefert ein
  anderes (aber nicht „Methoden"-)Symbol; das ist selten genug (Agent
  müsste exakt den Interface-Namen-Teil treffen) und deckt sich nicht
  mit dem „Methoden-ID"-Wortlaut aus `Konzept.md` — bewusst **nicht**
  Teil dieses Fixes (siehe „Bekannte Ausnahmen").
- **Fix-Ansatz (bereits gegen das Repro verifiziert, auf Nutzer-Weisung
  „ordentlich machen, keine Workarounds, keine liegen gelassene
  Tech-Debt" auf den gemeinsamen Einstiegspunkt angehoben — nicht mehr
  nur lokal in `GetSymbolBodyTool.cs`):** Die erste Fassung dieses Plans
  wollte die Normalisierung ausschließlich lokal in
  `GetSymbolBodyTool.cs` einbauen. Das wurde verworfen: `find_references`/
  `get_impact`/`get_type_hierarchy` hätten bei `get`/`set`-Positionen
  weiterhin auf dem Accessor-Symbol gesessen — mit derselben zugrunde
  liegenden Symbolverwechslung, nur mit einem anderen Symptom (Roslyns
  `SymbolFinder`-Referenzsuche auf einem Property-Accessor-Symbol findet
  normale Property-Verwendungsstellen typischerweise **nicht**, da diese
  im Compiler-Modell das Property-Symbol referenzieren, nicht den
  Accessor). Das wäre kein vollständiger Fix gewesen, sondern ein auf
  ein Tool verengter Patch, der dieselbe Ursache in drei anderen Tools
  unangetastet läuft. Stattdessen wird die Normalisierung **zentral** in
  `FindReferencesTool.ResolveSymbolAsync` ergänzt — dem seit `step-003`
  gemeinsamen Einstiegspunkt aller vier Tools:
  `symbol is IMethodSymbol { AssociatedSymbol: { } owner } ? owner : symbol`.
  `IMethodSymbol.AssociatedSymbol` ist die Standard-Roslyn-API, die
  einen Property-/Event-Accessor auf sein Property/Event zurückführt.
  Verifiziert: liefert für `get`/`set`-Positionen exakt die vom Skeleton
  gezeigte `P:...`-ID, unabhängig davon, über welches der vier Tools der
  Aufruf erfolgt. Das ist eine konsistente Fortführung des in `step-003`
  etablierten Prinzips (ein gemeinsamer Einstiegspunkt statt vier
  separate Patches), keine Scope-Erweiterung über `Konzept.md` hinaus —
  Scope P1 „Einheitlicher Symbol-Identifikator-Parser" verlangt explizit,
  dass alle Identifikator-Formate für **dasselbe Symbol** in allen
  betroffenen Tools gleich funktionieren; korrekt aufgelöste
  `get`/`set`-Positionen sind Teil genau dieser Garantie.
- **Body-Konsistenz als Nebeneffekt:** Da `ExtractSymbolBody` danach auf
  dem normalisierten Symbol arbeitet, zeigt der Body künftig die
  vollständige Property-Deklaration statt nur der einzelnen
  Accessor-Zeile (`get;`) — konsistent mit dem, was ein Agent von
  „Body des Members an dieser Position" erwartet.

### item-02 — Bestätigt, unverändert seit dem Original-Bug-Report

`GetViolationsScanner.FormatReport` (aktuell Zeile 106-151, die
konkrete Bedingung Zeile 113-121) berechnet `fileCount` **ausschließlich
aus den gefilterten Violations** (`filtered.Select(v => v.FilePath)...`)
— ist `filtered.Count == 0`, wird „Keine Dateien im Scope" gemeldet,
unabhängig davon, ob überhaupt Dateien im Scope lagen. `MatchesScope`
(Zeile 97-104) bleibt unverändert (Konzept-Vorgabe). Die Methode erhält
bereits `fileToProject` (Dictionary aller Solution-Dateien mit
Projekt-Namen) als Parameter — das reicht, um die tatsächliche
Datei-im-Scope-Zahl unabhängig von den Violations zu berechnen, ohne
neue Parameter einzuführen.

### item-03 — Root Cause liegt NICHT in `OverviewResourceRegistration`, sondern in `McpCodeGraphServer.LoadState`

**Zweitwichtigstes Ergebnis dieses Planer-Laufs:** `Konzept.md`s „Wo im
Projekt" verweist auf `OverviewResourceRegistration.DescribeSolution`
— die tatsächliche Fundstelle liegt aber eine Ebene tiefer.
`McpCodeGraphServer.LoadState` (Zeile 68-75) wertet im
`{ IsCompletedSuccessfully: true }`-Zweig `_catalog is null` aus — aber
`_catalog` wird **nur** innerhalb von `GetCurrentSolution()` (lazy,
beim ersten Aufruf) aus dem abgeschlossenen `_loadTask`-Ergebnis
adoptiert. Ist der Hintergrund-Load bereits fertig (`_loadTask`
`IsCompletedSuccessfully`), aber noch **niemand** hat
`GetCurrentSolution()` aufgerufen (`_catalog` also noch `null`), meldet
`LoadState` fälschlich `LoadFailed` — obwohl der Load tatsächlich mit
einem gültigen, nicht-null Catalog erfolgreich war. Exakt das
Zeitfenster „unmittelbar nach Serverstart" aus `Konzept.md`: Die
`ainetlinter://overview`-Resource wird typischerweise vom Agenten als
**erste** Aktion gelesen (siehe eigener Klassenkommentar in
`OverviewResourceRegistration.cs`), oft bevor irgendein Tool
`GetCurrentSolution()` aufgerufen hat — `DescribeSolution` liest
`mcpState.LoadState`, landet im `LoadFailed`-Zweig, zeigt „Laden
fehlgeschlagen — jeder Tool-Call liefert SOLUTION_NOT_LOADED" an,
obwohl der nächste tatsächliche Tool-Call (der `GetCurrentSolution()`
aufruft und dabei selbst adoptiert) ganz normal funktionieren würde.

Verifiziert per Test-Bestand: `McpServerCommandLoadingStateTests.
RunAsync_LoadFuncCompletes_ServerLeavesLoadingState` deckt nur den Fall
„Load abgeschlossen mit **null**-Resultat" ab (dort ist `LoadFailed`
korrekt — kein Catalog vorhanden, gleich ob adoptiert oder nicht). Der
Fall „Load abgeschlossen mit **nicht-null** Resultat, aber noch nicht
adoptiert" hat aktuell keinen Test — das ist exakt die Lücke.

Auswirkung auf tatsächliche Tool-Aufrufe: **keine** — alle Tool-Guards
prüfen ausschließlich `== ServerLoadState.Loading` (per Grep verifiziert
über alle zehn Tools), nie `LoadFailed`; sie rufen danach unbedingt
`GetCurrentSolution()` auf, was in diesem Fenster korrekt adoptiert und
die Solution liefert. Der Fix ändert daher an keinem bestehenden
Tool-Dispatch-Pfad etwas — ausschließlich am direkt beobachtbaren
`LoadState`-Wert selbst (und damit an der Overview-Anzeige).

**Fix-Ansatz:** `LoadState`-Getter im `IsCompletedSuccessfully:
true`-Zweig zusätzlich das Task-Ergebnis selbst prüfen, falls `_catalog`
noch nicht adoptiert ist (`_catalog ?? _loadTask.GetAwaiter().GetResult()`)
— sicher, weil `IsCompletedSuccessfully` bereits garantiert, dass kein
Warten stattfindet (derselbe Rechtfertigungs-Pattern wie das bestehende
`ainetlinter-disable BanBlockingTaskAccess` in `GetCurrentSolution()`,
Zeile ~103-106 — dort exakt dasselbe Argument). Bewusst **kein**
side-effect-vollständiges Adoptieren in `_catalog` selbst innerhalb des
Getters (kein Lock, keine `InitializeFileState`-Mutation) — reiner
lesender Peek, um den Getter leichtgewichtig und lock-frei zu halten
(er wird bei jedem Tool-Dispatch aufgerufen). `IsLoaded` (`_catalog is
not null`) bleibt bewusst unverändert bei „noch nicht adoptiert" — laut
eigenem XML-Doc-Kommentar ohnehin als „adoptierter" Zustand definiert,
keine neue Inkonsistenz gegenüber vorher.

### item-04 — Bestätigt: Node-Cap fehlt in beiden Tool-Beschreibungen

`SymbolGraphToolRegistrations.FindReferencesDescription`/
`GetImpactDescription` dokumentieren bereits den `depth`-Parameter
(„Default 1, hard cap 3") — aber **nicht** den davon unabhängigen
`CallGraphTraversal.MaxRecursionNodes = 200`-Cap (Knoten-/Locations-
Limit während der Traversierung, unabhängig von `maxResults`). Der Cap
taucht aktuell ausschließlich in der Trunkierungs-Meta-Zeile auf
(`CallGraphTraversal.AggregateAndTruncate`), die ein Agent nur sieht,
wenn er bereits über den Cap gelaufen ist — nicht vorab im Tool-Schema.
`GetTypeHierarchyDescription` ist nicht betroffen (nutzt
`CallGraphTraversal` nicht).

## Bewusst ausgeklammert

Das Nice-to-Have (lesbarere ID-Darstellung für EII) ist **nicht** Teil
dieses Batches: größerer, offenerer Gestaltungsspielraum (wo genau
zusätzlich anzeigen — `get_symbol_body`, `get_file_skeleton`, beides?),
nicht Definition-of-Done-relevant laut `Konzept.md`, und hätte den
Batch-Diff spürbar vergrößert. Bleibt in `roadmap.md` EPIC-03 als
offener Punkt für einen eigenen Folge-Step.

## Batch-Deckelung — Begründung

Geschätzter Diff (Produktion + dedizierter Test je Item, siehe
`Konzept.md` DoD „mind. ein Regressionstest je Muss-Haben-Punkt"):
item-01 ≈ 45 (zentrale Extraktion in `FindReferencesTool.cs` statt
lokalem Patch, dafür zwei Tests statt einem — siehe Begründung im
Aktueller-Projektzustand-Abschnitt zur Zentralisierung), item-02 ≈ 35
(Test in bestehende `GetViolationsToolTests.cs` integriert, keine neue
Datei), item-03 ≈ 30, item-04 ≈ 30 (neue, kleine
`SymbolGraphToolRegistrationsTests.cs`, analog zum bestehenden Muster
`OverviewResourceRegistration.cs` ↔
`OverviewResourceRegistrationTests.cs`) — Summe ca. 140, mit
XML-Doc-Kommentaren/Leerzeilen realistisch bis ca. 160-170 Zeilen (leicht
über der bereits auf 160 angehobenen Deckelung — bei Bedarf im
`step-result.md` transparent machen, kein Blocker, da Ursache eine
bewusste, vom Nutzer bestätigte Qualitätsentscheidung ist, keine
Scope-Ausweitung).
Überschreitet den Default `max_batch_diff_lines: 40` (`../spec.md`
§10.6) deutlich. Alle vier Items sind unabhängig voneinander,
`estimated_risk: low` (siehe Einzelbegründungen oben — item-01/03
ändern nachweislich keine bestehenden Tool-Dispatch-Pfade, item-02/04
sind reine Text-/Berechnungs-Ergänzungen ohne Fremdwirkung), und
`max_batch_items` (4 von 8) ist unkritisch.

**Entscheidung:** `max_batch_diff_lines` für diesen Task auf 160 erhöht
(`tasks/verbesserungen-mcp/config.md`, neu angelegt) statt den Batch auf
zwei Steps aufzuteilen oder ein Item herauszunehmen — begründet primär
damit, dass der Default-Wert 40 laut `../spec.md` §10.6 auf rein
kosmetische Batches kalibriert ist („50 Dateien mit je 1 Zeile"),
während `Konzept.md`s eigene Definition of Done für dieses Epic
zwingend einen dedizierten Test pro Punkt verlangt — das kostet
strukturell mehr als 40 Zeilen, sobald mehr als ein Item mit Test
gebündelt wird. Zusammen mit der expliziten Nutzer-Vorgabe „größere
Brocken, keine Mini-Steps" (`task-state.md`) und der Tatsache, dass
`roadmap.md` EPIC-03 von Anfang an genau für diesen Batch vorgesehen
hat, ist eine Aufteilung in zwei künstlich kleinere Batches (z. B.
„item-02+04" vs. „item-01+03") hier weniger stimmig als eine begründete,
task-lokale Anhebung der Deckelung. Siehe `config.md` für die
vollständige Begründung.

## Konkrete Änderungen

### item-01: get_symbol_body-ID-Korruption — `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` (zentral, siehe „Aktueller Projektzustand")

- **Was:** `ResolveSymbolAsync` umbauen: bisherigen Methodenkörper
  (Stable-ID-Zweig aus `step-003`, dann Position, dann Name) 1:1,
  unverändert in eine neue private Methode `ResolveSymbolCoreAsync`
  auslagern; `ResolveSymbolAsync` selbst ruft nur noch diese auf und
  normalisiert das Ergebnis:
  ```csharp
  internal static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolAsync(
      Solution solution, string identifier, CancellationToken ct)
  {
      var (symbol, error) = await ResolveSymbolCoreAsync(solution, identifier, ct);
      return (NormalizeToOwningMember(symbol), error);
  }

  private static ISymbol? NormalizeToOwningMember(ISymbol? symbol) =>
      symbol is IMethodSymbol { AssociatedSymbol: { } owner } ? owner : symbol;
  ```
  Reine Umbenennung/Extraktion des bisherigen Codes plus ein
  Normalisierungs-Aufruf — keine Logikänderung an Stable-ID-/Position-/
  Name-Auflösung selbst.
- **Warum:** Verifiziertes Repro (siehe „Aktueller Projektzustand")
  zeigt: eine Position auf `get`/`set` innerhalb einer
  Property-Deklaration liefert über `ResolveSymbolAtToken` den
  Accessor-`IMethodSymbol`, dessen DocumentationCommentId (`M:...
  get_Name~...`) nicht mit der vom Skeleton gezeigten Property-ID
  (`P:...Name`) übereinstimmt. `AssociatedSymbol` ist der
  Standard-Roslyn-Weg, einen Accessor auf sein Property/Event
  zurückzuführen. Zentral in `ResolveSymbolAsync` statt lokal in
  `GetSymbolBodyTool.cs` platziert, damit `find_references`/`get_impact`/
  `get_type_hierarchy` bei identischer Eingabe dasselbe korrekt
  aufgelöste Symbol bekommen wie `get_symbol_body` — kein Tool bleibt mit
  der alten, fehlerhaften Accessor-Auflösung zurück.
- **`GetSymbolBodyTool.cs` selbst:** keine Änderung nötig — ruft bereits
  seit `step-003` `FindReferencesTool.ResolveSymbolAsync` auf und
  profitiert automatisch von der zentralen Normalisierung.

### item-01: Test-Fixture — `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Greeter.cs`

- **Was:** Nach der bestehenden `Greet`-Methode (bleibt exakt auf
  Zeile 5, `Datei:5:19` wird von mehreren bestehenden Tests
  hartkodiert referenziert — **nicht verschieben**) eine leere Zeile
  und eine neue Property ergänzen:
  ```csharp
  public string Greet(string name) => $"Hello, {name}";

  public string Prefix { get; set; } = "Hi";
  ```
  Ergebnis: `Prefix` liegt auf der neuen Zeile 7 (Zeile 6 = Leerzeile).
  Spalte des `get`-Schlüsselworts: **28** (verifiziert per
  Zeichen-für-Zeichen-Auszählung von `    public string Prefix { get;
  set; } = "Hi";`, 4 Leerzeichen Einrückung); Spalte von `set`: **33**.
  Vor dem Schreiben des Tests die tatsächliche Datei nach dem Edit noch
  einmal einsehen und die Spalten ggf. nachzählen, falls Editor-Tools
  Whitespace abweichend normalisieren.
- **Warum:** Bestehende Fixture ist bereits an fünf Testklassen verankert
  (`FindReferencesToolTests`, `SearchPatternToolTests`,
  `GetIndexScopeToolTests`, `GetSymbolBodyToolTests`,
  `GetFileSkeletonToolTests`, `SkeletonStableIdTests`,
  `McpServerCommandTests`) — eine neue Datei hätte
  `GetIndexScopeToolTests`s hartkodierte `.cs: 5 Dateien`-Assertion
  gebrochen (siehe `TD-002`-Nachbarschaft); eine Ergänzung **nach**
  Zeile 5 in der bestehenden Datei ändert weder die Dateizahl noch die
  Position der bestehenden `Greet`-Assertionen.

### item-01: Test — `src/AiNetLinter.Tests/Mcp/Tools/GetSymbolBodyToolTests.cs`

- **Was:** Neuer Test
  `ExecuteAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertyIdNotAccessorId`:
  ruft `GetSymbolBodyTool.ExecuteAsync` mit Identifier
  `$"{_fixture.Workspace.GreeterPath}:7:28"` (Position auf `get`) auf,
  erwartet `Assert.Contains("id: \`P:SymbolGraphMini.Greeter.Prefix\`",
  text)` und `Assert.DoesNotContain("get_Prefix", text)`.
- **Warum:** Direkter Regressionsnachweis für den konkreten,
  verifizierten Bug-Fall.

### item-01: Test (zentraler Nachweis) — `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs`

- **Was:** Neuer Test
  `ResolveSymbolAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertySymbolNotAccessor`:
  ruft `FindReferencesTool.ResolveSymbolAsync` (internal, wie die
  bestehenden `ResolveSymbolAsync_StableId_ReturnsSymbolAtId`-Tests aus
  `step-003` bereits direkt testbar) mit Identifier
  `$"{_fixture.Workspace.GreeterPath}:7:28"` auf, erwartet
  `symbol!.Name == "Prefix"` und `Assert.IsAssignableFrom<IPropertySymbol>(symbol)`
  (nicht `IMethodSymbol`).
- **Warum:** Beweist den eigentlichen Zweck der Zentralisierung — nicht
  nur `get_symbol_body`, sondern der gemeinsame Einstiegspunkt selbst
  liefert für alle vier Tools das korrekte Symbol, nicht nur für eines.

### item-02: get_violations-Meldung — `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs`

- **Was:** In `FormatReport` vor der bisherigen
  `filtered.Count == 0`-Bedingung eine neue Berechnung einfügen:
  `var matchingFileCount = fileToProject.Count(kvp =>
  MatchesScope(kvp.Key, kvp.Value, solutionDir, scopeFilter));` — dann
  die bisherige „Keine Dateien im Scope"-Bedingung auf
  `!string.IsNullOrWhiteSpace(scopeFilter) && matchingFileCount == 0`
  umstellen (bisher implizit über `filtered.Count == 0` mitgeprüft) und
  die bestehende lokale Variable `fileCount` (Zeile 123, aus
  `filtered` berechnet) durch `matchingFileCount` ersetzen — die
  bestehende `sb.AppendLine($"Lint-Violations: {filtered.Count}
  Verstoesse in {fileCount} Dateien{scopeSuffix}");`-Zeile entsprechend
  auf `matchingFileCount` umstellen und den Text von „Dateien" auf
  „Dateien im Scope" präzisieren (matcht `Konzept.md`s vorgeschlagenen
  Wortlaut „N Dateien im Scope, 0 Violations"). Zusätzlich Sichtbarkeit
  von `private static string FormatReport(...)` auf `internal static`
  anheben, damit der neue Test direkt (ohne vollen `LinterEngine`-Lauf)
  gegen sie schreiben kann.
- **Warum:** `MatchesScope` bleibt unverändert (Konzept-Vorgabe: „kein
  Eingriff in MatchesScope") — der Fix betrifft ausschließlich, **womit**
  die Dateizahl berechnet wird (alle Solution-Dateien im Scope statt nur
  die, die zufällig eine Violation haben), nicht die Matching-Logik
  selbst.

### item-02: Test — `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs`

- **Was:** Zwei neue `[Fact]`-Methoden direkt gegen
  `GetViolationsScanner.FormatReport` (kein `LinterEngine`-Umweg, keine
  neue Fixture nötig — deterministisch per synthetischem
  `Dictionary<string,string> fileToProject` +
  `Array.Empty<RuleViolation>()`):
  1. `FormatReport_FilesInScopeButZeroViolations_DistinguishesFromNoFilesInScope`
     — `fileToProject` mit einem Eintrag, dessen Projektname/Pfad den
     `scopeFilter` matcht, `violations` leer. Erwartet:
     `Assert.DoesNotContain("Keine Dateien im Scope", text)` **und**
     `Assert.Contains("Dateien im Scope", text)`.
  2. `FormatReport_NoFileMatchesScope_ReturnsExplicitNoFilesMessage`
     — `fileToProject` mit einem Eintrag, der **nicht** matcht.
     Erwartet weiterhin `Assert.Contains("Keine Dateien im Scope
     (Filter: '...')", text)` (Regressionsschutz für den bereits
     bestehenden, korrekten Fall).
  Neue `using`-Zeilen `System.Collections.Generic` und
  `AiNetLinter.Models` ergänzen (für `Dictionary`/`RuleViolation`).
- **Warum:** In dieselbe bestehende Testklasse integriert statt neue
  Datei — testet dieselbe Produktionsklasse, die die Klasse bereits
  (indirekt über `GetViolationsTool.ExecuteAsync`) abdeckt; hält den
  Batch-Diff kleiner als eine neue Testklasse mit eigenem
  Boilerplate/Fixture-Setup.

### item-03: `McpCodeGraphServer.LoadState`-Race — `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`

- **Was:** Im `{ IsCompletedSuccessfully: true }`-Zweig des
  `LoadState`-Switch-Ausdrucks (Zeile 71) `_catalog is null` durch
  `(_catalog ?? _loadTask.GetAwaiter().GetResult()) is null` ersetzen.
  Direkt darüber einen `ainetlinter-disable BanBlockingTaskAccess`-
  Kommentar ergänzen (analog zum bestehenden Kommentar in
  `GetCurrentSolution()`, Zeile ~103-106): Begründung, dass
  `IsCompletedSuccessfully: true` bereits garantiert, dass
  `GetAwaiter().GetResult()` nicht blockiert, und ohne diesen Peek
  `LoadState` fälschlich `LoadFailed` meldet, solange `GetCurrentSolution()`
  noch nicht aufgerufen wurde.
- **Warum:** Siehe „Aktueller Projektzustand" — verifizierte Race
  zwischen Task-Abschluss und lazy `_catalog`-Adoption; betrifft primär
  die `ainetlinter://overview`-Anzeige unmittelbar nach Serverstart,
  ändert keinen bestehenden Tool-Dispatch-Pfad.

### item-03: Test — `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs`

- **Was:** Klasse um `IClassFixture<SymbolGraphCatalogFixture>`
  erweitern (Konstruktor-Parameter + privates Feld, analog zu
  `GetSymbolBodyToolTests`), `using AiNetLinter.Tests.Fixtures;`
  ergänzen. Neuer Test
  `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`:
  konstruiert einen `McpCodeGraphServer` mit `LoadFunc = _ =>
  Task.FromResult<SourceFileCatalog?>(_fixture.Catalog)` (bereits beim
  Konstruktor-Aufruf synchron abgeschlossen — genau das Zeitfenster vor
  jedem `GetCurrentSolution()`-Aufruf) und erwartet unmittelbar danach
  `Assert.Equal(ServerLoadState.Loaded, server.LoadState)`, **ohne**
  vorher `GetCurrentSolution()` aufgerufen zu haben.
- **Warum:** Bestehende zwei Tests in dieser Klasse decken nur
  „Load läuft noch" und „Load abgeschlossen mit null-Resultat" ab — der
  neue Test schließt exakt die Lücke „Load abgeschlossen mit
  nicht-null Resultat, vor Adoption".

### item-04: depth-Hard-Cap-Doku — `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`

- **Was:** `FindReferencesDescription` und `GetImpactDescription` um
  einen Satz zum Node-Cap ergänzen, direkt im Anschluss an den
  bestehenden `depth`-Satz, z. B. „Traversierung zusätzlich hart
  begrenzt auf 200 besuchte Knoten (unabhängig von maxResults)." —
  Wortwahl/genaue Platzierung dem bestehenden Stil der beiden Konstanten
  angleichen (`200` als Literal, analog zum bereits hartkodierten
  `hard cap 3` für `depth` in derselben Datei — kein Grund, hier von der
  etablierten Konvention abzuweichen).
- **Warum:** `CallGraphTraversal.MaxRecursionNodes = 200` ist aktuell
  nur in der Trunkierungs-Meta-Zeile sichtbar (erst **nachdem** der Cap
  bereits erreicht wurde) — nicht vorab im Tool-Schema, das der Agent
  vor dem ersten Aufruf liest.

### item-04: Test — `src/AiNetLinter.Tests/Mcp/SymbolGraphToolRegistrationsTests.cs` (neu)

- **Was:** Neue, kleine Testklasse (analog zum bestehenden Muster
  `OverviewResourceRegistration.cs` ↔ `OverviewResourceRegistrationTests.cs`):
  `ToolDescriptions_FindReferencesAndGetImpact_MentionNodeHardCap` baut
  über `McpServerOptionsFactory.Create(state)` die registrierten Tools
  auf (Muster identisch zu
  `OverviewResourceRegistrationTests.ToolSummaries_MatchesRegisteredToolNames`),
  liest `options.ToolCollection!.ToDictionary(t => t.ProtocolTool.Name,
  t => t.ProtocolTool.Description)` und prüft `Assert.Contains("200",
  descriptions["find_references"])` sowie `descriptions["get_impact"]`.
  Falls `ProtocolTool.Description` nicht nullable/leer sein kann in der
  aktuell installierten `ModelContextProtocol`-SDK-Version: Signatur vor
  dem Schreiben kurz gegenprüfen (Type-Info via IDE/Tooltip), Test ggf.
  minimal anpassen (z. B. Null-Forgiving-Operator) — Kernidee bleibt
  unverändert.
- **Warum:** Neue Testklasse statt Ergänzung einer bestehenden Tool-
  Testklasse, weil die Beschreibungs-Konstanten in
  `SymbolGraphToolRegistrations.cs` liegen (nicht in
  `FindReferencesTool.cs`/`GetImpactTool.cs` selbst) — konsistent mit
  dem bereits etablierten 1:1-Muster „Produktionsdatei ↔ eigene
  Testklasse" in diesem Projekt.

## Tests

- [ ] `GetSymbolBodyToolTests.ExecuteAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertyIdNotAccessorId` (item-01)
- [ ] `FindReferencesToolTests.ResolveSymbolAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertySymbolNotAccessor` (item-01, zentraler Nachweis)
- [ ] `GetViolationsToolTests.FormatReport_FilesInScopeButZeroViolations_DistinguishesFromNoFilesInScope` (item-02)
- [ ] `GetViolationsToolTests.FormatReport_NoFileMatchesScope_ReturnsExplicitNoFilesMessage` (item-02, Regressionsschutz)
- [ ] `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` (item-03)
- [ ] `SymbolGraphToolRegistrationsTests.ToolDescriptions_FindReferencesAndGetImpact_MentionNodeHardCap` (item-04)
- [ ] Bestehende Suiten bleiben unverändert grün, insbesondere
      `GetIndexScopeToolTests` (Datei-Zahl-Assertions gegen
      `SymbolGraphMini`), `FindReferencesToolTests`/
      `GetSymbolBodyToolTests`-Fälle, die `GreeterPath:5:19` hartkodieren,
      und `McpServerCommandLoadingStateTests`s bestehende zwei Tests.
- [ ] Volllauf `dotnet test` grün (Definition of Done).

## Definition of Done

- [ ] Alle vier Items unter „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` (Tech-Stack-Notiz) grün — 0 Fehler, 0 Warnungen
- [ ] `dotnet test` (Volllauf) grün — bei Testhost-Absturz ohne
      Einzeltestfehler: TD-003 zur Kenntnis nehmen (bekanntes
      Sandbox-Problem, nicht dieses Steps), Lauf wiederholen statt als
      Fehlschlag werten
- [ ] **Ein** Commit für den gesamten Batch (Conventional Commit,
      deutsch, Body listet alle vier Items einzeln auf, Refs-Suffix
      `[verbesserungen-mcp]`)
- [ ] `step-004/step-result.md` geschrieben, mit eigenem Absatz je Item
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4` (Updates & Tests) —
  xUnit v3 Pflicht für jede Logik-Änderung (item-01/02/03; item-04 ist
  reine Text-Ergänzung, Test trotzdem sinnvoll als Dokumentations-
  Regressionsschutz); Testsuite-Parallelität erhalten (keine neue
  `[Collection]`/Serialisierung nötig, alle vier Items nutzen
  bestehende Fixtures oder synthetische In-Memory-Daten); Commit-
  Vorschlag-Pflicht am Ende der Antwort.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` (Qualitätsdrift-
  Prävention) — Zero-Warning-Direktive für alle geänderten Dateien.
  Aufräumen-erlaubt-Klausel: **nicht** proaktiv nutzen, um über den
  geplanten Scope hinaus in denselben Dateien aufzuräumen (insbesondere
  `FindReferencesTool.cs`s TD-004-Kommentar bleibt unangetastet, ist
  nicht Teil dieses Batches).
- `.agents/rules/AiNetLinter.mdc` (`BanBlockingTaskAccess`) — item-03
  braucht zwingend den `ainetlinter-disable`-Kommentar mit Begründung
  für `_loadTask.GetAwaiter().GetResult()`, sonst Build-Fehler
  (Zero-Warning-Direktive behandelt aktive Linter-Regelverstöße im
  eigenen Code als Fehler). `MaxMethodLineCount`/`MaxCyclomaticComplexity`
  — alle vier Änderungen bleiben weit darunter (jeweils ein bis zwei
  zusätzliche Zeilen/Ausdrücke).

## Bekannte Ausnahmen

- TD-003 (`dotnet test`-Volllauf stürzt in dieser Sandbox intermittierend
  mit Testhost-Absturz ab, ohne Einzeltestfehler) — bereits dokumentiert,
  unabhängig von diesem Step. Bei Absturz: Lauf wiederholen, nicht als
  Regression werten.
- Explizite Interface-Implementierungen mit Position auf dem
  Interface-Namen-Teil (`IFoo.` vor dem Methodennamen, nicht auf dem
  Methodennamen selbst) liefern weiterhin ein anderes Symbol als
  erwartet (verifiziert im Repro: liefert die Typ-ID des Interfaces,
  keine „Methoden-ID"). Bewusst **nicht** Teil von item-01 — deckt sich
  nicht mit dem in `Konzept.md` beschriebenen „verschachtelte/doppelte
  Methoden-ID"-Symptom, seltener Trefferfall (Agent müsste exakt den
  Interface-Namen-Teil einer EII-Deklaration als Position angeben), und
  eine allgemeine Lösung (z. B. „bei Position irgendwo innerhalb einer
  Member-Deklaration immer zur umschließenden Deklaration hochlaufen")
  würde das absichtliche Verwendungsstellen-Verhalten von
  `ResolveSymbolAtToken` für andere Fälle mit verändern (siehe dessen
  XML-Doc: „sonst das an dieser Stelle referenzierte Symbol"). Falls
  gewünscht: eigener, separater Folge-Punkt.

## Notes

- **Reihenfolge der Umsetzung innerhalb des Batches:** keine
  Abhängigkeiten zwischen den vier Items — beliebige Reihenfolge
  möglich, item-04 (reine Textänderung) eignet sich als risikoärmster
  Einstieg.
- item-01 und item-03 sind die beiden Punkte, bei denen dieser
  Planer-Lauf von der in `Konzept.md`/`roadmap.md` vermuteten
  Fundstelle/Root-Cause abgewichen ist (item-01: Root Cause liegt in
  Property-Accessor-Resolution, nicht in generischen Methoden; item-03:
  Root Cause liegt in `McpCodeGraphServer.LoadState`, nicht in
  `OverviewResourceRegistration.DescribeSolution` selbst) — beide per
  eigenständigem Roslyn-Repro bzw. Code-Analyse verifiziert, nicht nur
  vermutet. Kritiker sollte bei der Konzept-Treue-Prüfung (Ebene 4)
  diese Abweichung als „präzisierte, tiefer lokalisierte Umsetzung
  desselben Konzept-Punkts" werten, nicht als Scope-Abweichung.
- **item-01 zusätzlich nachträglich zentralisiert (nach Nutzer-Weisung):**
  Die ursprüngliche Fassung dieses Plans hätte die Normalisierung nur
  lokal in `GetSymbolBodyTool.cs` eingebaut. Der Nutzer hat explizit
  gefordert, Korrekturen „ordentlich" umzusetzen, ohne Workarounds und
  ohne liegen gelassene Tech-Debt — ein auf ein Tool verengter Patch
  hätte dieselbe Symbolverwechslung in `find_references`/`get_impact`/
  `get_type_hierarchy` unangetastet gelassen (anderes Symptom, gleiche
  Ursache). Die jetzige Fassung behebt die Ursache **einmal, zentral**,
  siehe „Aktueller Projektzustand" und „Konkrete Änderungen" oben —
  direkt vom Orchestrator angepasst (kein erneuter Planer-Subagent-Lauf).
  Der Coder soll nach dem vollen Testlauf gezielt auch
  `GetImpactToolTests`/`GetTypeHierarchyToolTests` auf unerwartete
  Verhaltensänderungen bei `get`/`set`-Positionen sichten (sollte laut
  Blast-Radius-Analyse aus `step-003` keine bestehenden Tests berühren,
  da keiner davon aktuell eine Position auf einem Accessor-Keyword
  verwendet — trotzdem bewusst genannt, damit es nicht übersehen wird).
- `config.md` (neu angelegt) hebt `max_batch_diff_lines` für den
  **gesamten Task** auf 160 an, nicht nur für diesen Step — gilt damit
  auch für künftige Batches in diesem Task, falls es dazu kommt.
