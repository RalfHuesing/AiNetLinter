---
status: done (pending audit)
type: step-plan
task: codegraph-mcp
step: 005
title: "get_impact Tool (Git-Diff- und Symbol-Impact ueber DiffImpactAnalyzer.AnalyzeAsync)"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T23:00:00Z
related_to: [tasks/codegraph-mcp/step-004]
---

# Step 005: get_impact Tool (Git-Diff- und Symbol-Impact ueber DiffImpactAnalyzer.AnalyzeAsync)

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-03` aus `roadmap.md` — Symbolgraph-Tools. Nach step-003
  (`find_symbol`) und step-004 (`find_references`) ist `get_impact` das
  dritte von fuenf Tools; danach bleiben `get_type_hierarchy` und
  `get_file_skeleton` offen fuer weitere EPIC-03-Steps.
- **Konzept-Referenz:** `konzept.md` Tool-Tabelle unter "Wie" (Zeile
  `get_impact | Git-Ref (optional) oder Symbol direkt | Betroffene
  Call-Sites geaenderter Signaturen | DiffImpactAnalyzer.AnalyzeAsync`),
  sowie die neue Muss-Haben-Zeile "Dogfooding pro Tool-Step gegen die
  eigene `AiNetLinter.slnx`" (ersetzt das gestrichene EPIC-09, siehe
  `roadmap.md` Epic-Zeile EPIC-09 und `konzept.md` "Entdeckte Maengel/
  Redundanzen" letzter Eintrag).

## Aktueller Projektzustand (JIT-Kontext)

- `DiffImpactAnalyzer.AnalyzeAsync(Solution solution, string targetPath,
  string? gitSinceRef, bool verbose)`
  (`src/AiNetLinter/Core/DiffImpactAnalyzer.cs:35`) existiert bereits
  vollstaendig und wird von `ImpactCommand.RunAsync`
  (`src/AiNetLinter/Commands/ImpactCommand.cs:28`) fuer den bestehenden
  CLI-Modus `--impact` genutzt — **kein Neubau**, direkte
  Wiederverwendung fuer den Git-Ref-Zweig von `get_impact`.
  - Ablauf: `FindGitRoot(targetPath)` laeuft von `targetPath` aus die
    Verzeichnisse hoch bis `.git` gefunden wird → `git diff -U0
    [gitSinceRef] -- *.cs` im gefundenen Root → `ParseGitDiffHunks` →
    `GetChangedSymbolsFromHunksAsync` (nur `public`/`internal`/
    `protected`-Methoden/Konstruktoren, die eine geaenderte Zeile
    schneiden) → `FindAllCallSitesAsync` ueber `FindCallSitesAsync`
    (dieselbe Methode, die `find_references`/step-004 bereits nutzt).
  - **Wichtiger Fund, der diesen Plan direkt beeinflusst:** `LogGitWarning`
    (Zeile 56-62) ruft bei `verbose == true` **direkt** `Console.WriteLine`
    auf — nicht ueber `ILintConsole`/`_console.WriteError` wie der Rest des
    MCP-Codepfads. Der stdio-MCP-Transport (`StdioServerTransport`, siehe
    `McpServerCommand.cs:38`) nutzt **stdout** fuer das JSON-RPC-Framing;
    ein zusaetzlicher `Console.WriteLine` auf stdout wuerde das Protokoll
    korrumpieren. `get_impact` **muss** `AnalyzeAsync` daher immer mit
    `verbose: false` aufrufen — das ist kein Implementierungsdetail,
    sondern eine harte Korrektheitsbedingung fuer den stdio-Betrieb. Dieser
    Step aendert `AnalyzeAsync`/`LogGitWarning` selbst **nicht** (bleibt
    fuer den CLI-Pfad unveraendert) — die Absicherung passiert
    ausschliesslich am MCP-Tool-Aufrufpunkt.
  - `targetPath` wird fuer `get_impact` aus
    `Path.GetDirectoryName(solution.FilePath)` abgeleitet — derselbe
    `outputRoot`-Ableitungs-Pattern, den `FindSymbolTool`/`FindReferencesTool`
    bereits fuer relative Pfade nutzen (`FindSymbolTool.cs:57`,
    `FindReferencesTool.cs:98`).
- `FindReferencesTool.ResolveSymbolAsync(Solution, string identifier,
  CancellationToken)` (`src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:51`,
  bereits `internal`) loest einen Identifikator (Datei:Zeile:Spalte oder
  qualifizierter/teil-qualifizierter Name) zu genau einem `ISymbol` auf
  und liefert bei Mehrdeutigkeit/Nicht-Fund bereits fertige
  `CallToolResult`-Fehlerantworten (`SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL`).
  Der Symbol-direkt-Zweig von `get_impact` ruft diese Methode **direkt**
  wieder auf statt die Identifikator-Aufloesung ein zweites Mal zu bauen —
  exakt die in `step-004/step-result.md` unter "Beobachtungen" bereits
  vorhergesehene Wiederverwendung ("Der naechste EPIC-03-Step sollte
  `FindReferencesTool.ResolveSymbolAsync` wiederverwenden, falls er
  direkte Symbol-Identifikator-Eingabe braucht").
- `DiffImpactAnalyzer.FindCallSitesAsync(ISymbol, Solution)`
  (bereits `internal`, Zeile 292) ist fuer den Symbol-direkt-Zweig identisch
  zu dem, was `FindReferencesTool.ExecuteAsync` bereits tut — der
  Symbol-direkt-Zweig von `get_impact` liefert de facto dieselbe Antwort
  wie `find_references` fuer genau ein Symbol; das ist laut Konzept-Tabelle
  so vorgesehen (`get_impact` deckt "Symbol direkt" als Alternative zum
  Git-Ref ab, nicht als eigenstaendig andere Logik).
- **Footprint-Risiko (TD-004/TD-005, siehe `tech-debt.md`):** Jede
  bisherige Tool-Klasse mit `McpCodeGraphServer` als Parametertyp naehert
  sich dem `AIContextFootprint`-Limit (2500 Zeilen); `FindReferencesTool`
  riss es bereits einmal knapp. `GetImpactTool` bringt zusaetzlich
  `DiffImpactAnalyzer` als Abhaengigkeit mit — bereits Teil von
  `FindReferencesTool`s Footprint, hier aber ein zweites Mal in einer
  neuen Klasse. Um das Risiko klein zu halten: `GetImpactTool` bleibt
  bewusst duenn (Dispatch-Logik + Delegation an bestehende Methoden,
  **keine** eigene Parsing-/Aufloesungslogik – die liegt bereits in
  `SymbolIdentifierResolver`/`FindReferencesTool`). Selbst-Lint
  (`AIContextFootprint` fuer `GetImpactTool`) ist vor dem Doku-Commit
  explizit zu pruefen (siehe Definition of Done).
- `McpServerOptionsFactory.BuildToolCollection`
  (`src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:40`) registriert bisher
  zwei Tools per `tools.Add(McpServerTool.Create(...))` — drittes Tool wird
  nach demselben Muster ergaenzt (Closure auf `mcpState`, Beschreibung
  benennt C#-only-Scope explizit, siehe EPIC-05-Vorgriff, der hier bereits
  in step-003/004 konsistent gehandhabt wurde).
- **Keine bestehenden Tests fuer den Git-Diff-Zweig von `AnalyzeAsync`:**
  `DiffImpactAnalyzerTests.cs` testet ausschliesslich die reine Funktion
  `ParseGitDiffHunks` mit einem String-Literal — es gibt aktuell **keinen**
  Test, der `AnalyzeAsync` End-to-End gegen einen echten Git-Repo-Zustand
  faehrt (weder auf CLI- noch auf MCP-Ebene). Dieser Step muss daher fuer
  den Git-Ref-Zweig eine neue Test-Fixture-Art einfuehren (echtes
  Temp-Git-Repo, siehe "Tests" unten) statt eine bestehende
  wiederzuverwenden.

## Intention

`get_impact` erlaubt zwei Eingabe-Modi (laut Konzept-Tabelle,
gegenseitig exklusiv): entweder ein optionaler Git-Ref (Aufrufstellen
aller seit diesem Ref geaenderten Signaturen, Default = uncommittete
Aenderungen — identisches Verhalten zum bestehenden CLI-`--impact` ohne
`--impact-ref`) oder ein Symbol-Identifikator direkt (Aufrufstellen genau
dieses einen Symbols, ohne Git-Bezug). Beide Zweige delegieren vollstaendig
an bereits bestehenden Code (`DiffImpactAnalyzer.AnalyzeAsync` bzw.
`FindReferencesTool.ResolveSymbolAsync` + `DiffImpactAnalyzer.FindCallSitesAsync`)
— keine neue Analyselogik, nur Dispatch + MCP-Verdrahtung, damit
`GetImpactTool` selbst klein und footprint-unauffaellig bleibt.

## Konkrete Aenderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` (neu)

- **Was:** `internal static class GetImpactTool` mit
  `ExecuteAsync(McpCodeGraphServer state, string? gitRef, string?
  symbolIdentifier, CancellationToken ct)`:
  1. `state.GetCurrentSolution()` pruefen (`SolutionNotLoaded`, wie
     bestehende Tools).
  2. Validierung: sind **beide** Parameter gesetzt (nicht null/leer) →
     neuer Fehlercode `INVALID_ARGUMENT` ("gitRef und symbolIdentifier
     sind gegenseitig exklusiv — genau einen angeben oder beide
     weglassen fuer Git-Diff gegen uncommittete Aenderungen.").
  3. Ist `symbolIdentifier` gesetzt (und `gitRef` nicht) → Symbol-direkt-
     Zweig: `FindReferencesTool.ResolveSymbolAsync(solution,
     symbolIdentifier, ct)` aufrufen, bei Fehler diesen direkt
     durchreichen, sonst `DiffImpactAnalyzer.FindCallSitesAsync(symbol,
     solution)` aufrufen und als Text zurueckgeben (identisches
     Antwortformat wie `find_references`).
  4. Sonst (Git-Ref-Zweig, `gitRef` optional/leer = uncommittete
     Aenderungen): `targetPath = Path.GetDirectoryName(solution.FilePath)
     ?? ""`, `DiffImpactAnalyzer.AnalyzeAsync(solution, targetPath, gitRef,
     verbose: false)` aufrufen (**immer** `verbose: false`, siehe
     "Aktueller Projektzustand" — stdout-Sicherheit fuer den
     stdio-Transport) und die Liste als Text zurueckgeben (leere Liste →
     `"Keine betroffenen Aufrufstellen gefunden fuer <gitRef oder
     'uncommittete Aenderungen'>"`, analog zum bestehenden Muster in
     `FindReferencesTool.ExecuteAsync`/`FindSymbolTool.FindMatchesAsync`).
  - Beide Zweige nutzen ausschliesslich bereits bestehende Methoden — keine
    eigene Symbol-/Diff-Logik in dieser Datei.
- **Warum:** deckt beide Konzept-Tabellen-Eingaben ab, ohne bestehende
  Analyselogik zu duplizieren; Dispatch bleibt duenn genug, um das
  `AIContextFootprint`-Risiko aus TD-004/TD-005 nicht weiter zu
  verschaerfen.

### Datei 2: `src/AiNetLinter/Output/LinterErrorCodes.cs` (Zeile ~23)

- **Was:** Neue Konstante `InvalidArgument = "INVALID_ARGUMENT"` ergaenzen
  (analog zu `SymbolNotFound`/`AmbiguousSymbol` aus step-004).
- **Warum:** die gegenseitige Exklusivitaet von `gitRef`/`symbolIdentifier`
  braucht einen eigenen, maschinenlesbaren Fehlercode — keiner der
  bestehenden Codes passt semantisch (kein "nicht gefunden", sondern
  "falsch benutzt").

### Datei 3: `src/AiNetLinter/Mcp/McpToolResults.cs`

- **Was:** Neue Kurzform `InvalidArgument(string message)` ergaenzen, die
  `Error(LinterErrorCodes.InvalidArgument, message, hint: "Entweder
  gitRef ODER symbolIdentifier angeben, nie beide.")` zurueckgibt — analog
  zu `SymbolNotFound`/`AmbiguousSymbol`.
- **Warum:** haelt das Boilerplate-Bau-Muster konsistent zu den
  bestehenden Kurzformen in derselben Datei.

### Datei 4: `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs`

- **Was:** Dritten `tools.Add(McpServerTool.Create(...))`-Aufruf in
  `BuildToolCollection` ergaenzen: Name `get_impact`, Delegate
  `(string? gitRef = null, string? symbolIdentifier = null,
  CancellationToken ct = default) => GetImpactTool.ExecuteAsync(mcpState,
  gitRef, symbolIdentifier, ct)`, Description benennt explizit: C#-only-
  Scope, die zwei Eingabe-Modi und ihre gegenseitige Exklusivitaet (z. B.
  "Findet Aufrufstellen geaenderter C#-Signaturen. Entweder gitRef (Git-
  Commit-Ref, leer = uncommittete Aenderungen) ODER symbolIdentifier
  (Datei:Zeile:Spalte oder qualifizierter Name) angeben, nie beide. Deckt
  nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien.").
- **Warum:** identisches Registrierungsmuster wie die beiden bestehenden
  Tools; `McpServerOptionsFactory` selbst bleibt laut TD-004-Prognose
  bislang unauffaellig, trotzdem Selbst-Lint nach dieser Aenderung pruefen
  (dritter Tool-Eintrag mit einer weiteren Abhaengigkeitskette
  `DiffImpactAnalyzer`).

### Datei 5: `tests/Fixtures/SymbolGraphMini/` (Ergaenzung, kein neues Fixture-Verzeichnis)

- **Was:** Keine Datei-Aenderung an sich, aber Pruefen/Bestaetigen: das
  bestehende Fixture (`Greeter.cs`/`Caller.cs`/`OtherCaller.cs`) reicht
  fuer den Symbol-direkt-Zweig von `get_impact` (identischer Anwendungsfall
  wie `find_references`-Tests aus step-004) — **kein neues Fixture noetig**
  fuer diesen Teil.
- **Warum:** vermeidet Fixture-Duplikation; der Symbol-direkt-Zweig ist
  Verhaltens-identisch zu `find_references` fuer ein einzelnes Symbol.

### Datei 6: `tests/Fixtures/GitImpactMini/` (neu, fuer den Git-Ref-Zweig)

- **Was:** Neue Mini-Solution nach demselben Muster wie `SymbolGraphMini`
  (`.slnx`, `.csproj`, ein bis zwei `.cs`-Dateien mit einer Methode +
  einem Aufrufer). Der zugehoerige Test-Fixture-Workspace (Datei 7)
  initialisiert zusaetzlich ein **echtes Git-Repository** im kopierten
  Temp-Verzeichnis (`git init`, initialer Commit mit dem
  Ausgangs-/Signatur-Zustand der Methode), damit `AnalyzeAsync`s
  `FindGitRoot`/`git diff`-Aufrufe einen echten `.git`-Ordner vorfinden.
  Nach dem initialen Commit veraendert der Test die betroffene Methode
  (Signatur- bzw. Body-Zeile) **ohne** zu committen — das ist der Fall
  "uncommittete Aenderungen" (`gitSinceRef == null`), den `get_impact`
  ohne Parameter abdeckt.
- **Warum:** es existiert noch kein Test-Fixture-Muster fuer den
  Git-Diff-Zweig (siehe "Aktueller Projektzustand") — dieser Step muss es
  einmalig etablieren, da `get_impact` der erste MCP-Tool-Konsument dieses
  Zweigs ist. Ein separates, kleines Fixture statt Wiederverwendung von
  `SymbolGraphMini` haelt das Git-Repo-Setup (zusaetzlicher Prozess-Start)
  von den bestehenden, rein Roslyn-basierten Fixture-Tests getrennt.

### Datei 7: `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` (neu)

- **Was:** Analog zu `SymbolGraphMiniFixtureWorkspace`
  (Temp-Kopie + `IDisposable`), zusaetzlich:
  - `git init` + `git config user.email/user.name` (lokal im Temp-Repo,
    **nicht** global) + `git add -A` + initialer Commit ueber
    `System.Diagnostics.Process` (gleiches Aufruf-Muster wie
    `DiffImpactAnalyzer.RunGitDiff`, `CreateNoWindow = true`,
    `UseShellExecute = false`).
  - Eine Methode zum gezielten Aendern einer Zeile in der Ziel-Datei nach
    dem Commit (fuer den "uncommittete Aenderungen"-Testfall).
  - `Dispose()` loescht das komplette Temp-Verzeichnis inkl. `.git`
    (wie bestehendes Muster, `Directory.Delete(recursive: true)`).
- **Warum:** haelt das Git-Repo-Boilerplate an einer Stelle, wiederverwendbar
  fuer kuenftige Tests, die ebenfalls den Git-Ref-Zweig brauchen (z. B.
  falls EPIC-07 weitere Staleness-/Impact-Tests ergaenzt).

### Datei 8: `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs` (neu)

- **Was:** Tests gemaess "Tests" unten.
- **Warum:** `EnableTestSentinel`-Regel (`AiNetLinter.mdc`) verlangt fuer
  komplexe Typen eine Testklasse — analog zu `FindReferencesToolTests.cs`.

### Datei 9: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:** Bestehenden E2E-Test (`RunAsync_ValidFixture_ServerRespondsWithBothTools`
  o. ae., siehe step-004) auf drei registrierte Tools anpassen
  (`tools/list` erwartet jetzt `find_symbol`, `find_references`,
  `get_impact`). Kein neuer eigener Subprozess-E2E-Test fuer `get_impact`
  selbst noetig (TD-002 vermerkt bereits die Kosten weiterer
  Subprozess-Tests) — die funktionale Tiefe deckt `GetImpactToolTests.cs`
  bereits ab.
- **Warum:** haelt den bestehenden Tool-Zaehl-Test synchron mit der
  tatsaechlichen Registrierung, ohne TD-002 durch einen weiteren
  Subprozess-Test zu verschaerfen.

## Tests

- [ ] `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
- [ ] `ExecuteAsync_BothGitRefAndSymbolGiven_ReturnsInvalidArgumentError`
- [ ] `ExecuteAsync_SymbolIdentifierGiven_DelegatesToResolveSymbolAndReturnsCallSites`
      (SymbolGraphMini-Fixture, identisch zum `find_references`-Testfall
      aus step-004 — bestaetigt, dass der Symbol-direkt-Zweig echte
      Ergebnisse liefert statt nur durchzureichen)
- [ ] `ExecuteAsync_UnknownSymbolIdentifier_ReturnsSymbolNotFoundError`
      (SymbolGraphMini, durchgereichter Fehler aus `ResolveSymbolAsync`)
- [ ] `ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite`
      (GitImpactMini-Fixture: initialer Commit, danach unkommittete
      Signatur-/Body-Aenderung an der Zielmethode, `get_impact` ohne
      Parameter findet die Aufrufstelle)
- [ ] `ExecuteAsync_NoGitRepository_ReturnsEmptyResultNotCrash`
      (Temp-Verzeichnis **ohne** `.git` — deckt den bestehenden
      `FindGitRoot`-Rueckgabewert `null` ab; `AnalyzeAsync` liefert
      bereits heute `[]` in diesem Fall, Test bestaetigt, dass
      `GetImpactTool` das als "Keine betroffenen Aufrufstellen..."
      formatiert statt zu crashen)
- [ ] `McpServerCommandTests`: bestehenden Tool-Zaehl-Test auf drei Tools
      angepasst (siehe Datei 9)

## Definition of Done

- [ ] Alle „Konkrete Aenderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`dotnet build AiNetLinter.slnx`) gruen, 0 Warnungen
- [ ] Test-Command aus Tech-Stack-Notiz (`dotnet test AiNetLinter.slnx`) gruen
- [ ] Selbst-Lint (`ainetlinter --config rules.json --path ./src/`) 0
      Violations, insbesondere `AIContextFootprint` fuer `GetImpactTool`
      **und** `McpServerOptionsFactory` explizit im Output geprueft (siehe
      TD-004/TD-005 — dritter Tool-Eintrag koennte die Factory oder die
      neue Tool-Klasse ans Limit bringen; falls ja: gleiches Ausweichmuster
      wie step-004 (kleine, nicht in Signaturen referenzierte Helfer in
      eine eigene Datei auslagern), keine neue Abstraktionsebene)
- [ ] **Dogfooding (siehe `konzept.md` Muss-Haben):** neuer eigener
      Abschnitt „Dogfooding" in `step-result.md` mit mindestens zwei
      Ad-hoc-Aufrufen gegen die eigene `AiNetLinter.slnx`
      (`ainetlinter --mcp-server --path .` starten, wie im bestehenden
      E2E-Testmuster, aber gegen das Repo-Root statt einer Fixture):
      1. Symbol-direkt-Modus gegen einen tatsaechlich existierenden
         Bezeichner (z. B. `symbolIdentifier: "DiffImpactAnalyzer.FindCallSitesAsync"`).
      2. Git-Ref-Modus mit einem **reproduzierbaren, historischen** Ref
         (z. B. `gitRef: "HEAD~1"`) — bewusst **kein** Aufruf ohne
         Parameter (uncommittete Aenderungen), da der Working-Tree waehrend
         dieses Steps selbst veraenderten Code enthaelt und ein Test gegen
         den eigenen WIP-Stand kein aussagekraeftiges Dogfooding-Ergebnis
         liefern wuerde (unklar, was "erwartet" ist). `HEAD~1` liefert
         einen echten, reproduzierbaren Vergleichspunkt unabhaengig vom
         aktuellen Arbeitsstand.
      Kurzergebnis + Auffaelligkeiten dokumentieren, ersetzt keinen der
      obigen automatisierten Tests.
- [ ] Commit auf aktuellem Branch (Conventional Commit, Englisch,
      `[codegraph-mcp]`-Suffix, siehe `roadmap.md` Tech-Stack-Notiz)
- [ ] `step-005/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Grenzwerte (Produktion)` —
  `AIContextFootprint` (2500, siehe TD-004/TD-005-Risiko oben),
  `MaxMethodParameterCount` (4 — `GetImpactTool.ExecuteAsync` hat bereits
  vier Parameter inkl. `ct`, **keine** weiteren Parameter ergaenzen ohne
  Input-Record), `EnforceSealedClasses`/`EnforceNullableEnable` fuer alle
  neuen Dateien.
- `.agents/rules/AiNetLinter.mdc#test-coverage` — `EnableTestSentinel`:
  `GetImpactTool` braucht eine eigene Testklasse (Datei 8).
- `.agents/rules/AiNetLinterRichtlinien.mdc#2` — kein DI-Container: dritte
  Tool-Registrierung bleibt Closure-basiert wie die ersten beiden.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Zero-Warning-Direktive,
  Result-Pattern-Praeferenz (Fehlerfaelle wie `INVALID_ARGUMENT` als
  `CallToolResult`-Fehlerantwort, keine Exception).

## Bekannte Ausnahmen

- Der Git-Ref-Zweig deckt nur den in `AnalyzeAsync` bereits implementierten
  Umfang ab (nur `MethodDeclarationSyntax`/`ConstructorDeclarationSyntax`,
  nur `public`/`internal`/`protected`/`protected internal`-Sichtbarkeit,
  siehe `GetChangedSymbolsAsync`/`IsPublicOrInternal`) — keine Erweiterung
  dieses Umfangs in diesem Step, das waere eine Aenderung an bestehender
  CLI-Logik ausserhalb des hier definierten Scopes (nur MCP-Verdrahtung).

## Notes

- **Nicht neu bauen:** weder eine zweite Identifikator-Parsing-Logik noch
  eine zweite Call-Sites-Formatierung — beide existieren bereits
  (`SymbolIdentifierResolver`, `FindReferencesTool.ResolveSymbolAsync`,
  `DiffImpactAnalyzer.FindCallSitesAsync`/`AnalyzeAsync`). `GetImpactTool`
  ist bewusst ein duenner Dispatch, kein eigenstaendiger Analyse-Codepfad.
  Wer versucht ist, `AnalyzeAsync` fuer den Symbol-Zweig "wiederzuverwenden"
  (statt `FindCallSitesAsync` direkt aufzurufen): `AnalyzeAsync` erwartet
  einen Git-Diff-Kontext (`targetPath`/`gitSinceRef`) und ist fuer den
  Symbol-direkt-Fall der falsche Einstiegspunkt — `FindCallSitesAsync` ist
  die Methode, die `AnalyzeAsync` selbst am Ende intern aufruft.
- **`verbose: false` ist keine Kleinigkeit:** siehe "Aktueller
  Projektzustand" — ein versehentliches `verbose: true` (oder ein
  durchgereichter Parameter dafuer) wuerde `Console.WriteLine` auf stdout
  ausloesen und den stdio-JSON-RPC-Transport korrumpieren. `GetImpactTool`
  darf keinen `verbose`-Parameter nach aussen exponieren.
- **Git-Repo-Test-Fixture ist neuer Boden fuer diesen Task** (siehe Datei
  6/7) — bewusst als wiederverwendbares Muster angelegt (nicht nur
  Inline-Setup im Testfall), falls EPIC-07 spaeter weitere Staleness-/
  Git-Integrationstests braucht.
- Der Symbol-direkt-Zweig liefert bei Erfolg exakt dasselbe
  Text-Format wie `find_references` — das ist beabsichtigt (Konzept-Tabelle
  beschreibt "Symbol direkt" als Alternative zum Git-Ref, keine eigene
  Formatierung), keine zufaellige Duplikation.
