---
status: done (pending audit)
type: step-plan
task: codegraph-mcp
step: 004
title: "find_references Tool (Symbol- und Positions-Aufloesung + Aufrufstellen)"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T21:00:00Z
related_to: [step-003]
---

# Step 004: find_references Tool (Symbol- und Positions-Aufloesung + Aufrufstellen)

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-03` aus `roadmap.md` — Symbolgraph-Tools. step-003 (inkl.
  `fix-01`) lieferte die Tool-Registrierungs-Infrastruktur
  (`McpServerOptionsFactory`, `McpToolResults`) und das erste Tool
  `find_symbol`, beide `approved`. Offen: `find_references`, `get_impact`,
  `get_type_hierarchy`, `get_file_skeleton`. Dieser Step liefert
  `find_references`.
- **Konzept-Referenz:** `konzept.md` Tool-Tabelle unter "Wie" — Zeile
  `find_references` | Input `Symbol-Identifikator (Datei:Zeile:Spalte oder
  qualifizierter Name)` | Output `Alle Aufrufstellen: Datei:Zeile,
  aufrufender Kontext, Projekt` | Basis
  `DiffImpactAnalyzer.FindCallSitesAsync` (bereits vorhanden). Ebenfalls
  relevant: Muss-Haben "Fehlerbehandlung ohne Absturz" (Solution nicht
  geladen → strukturierte Fehlerantwort) — bereits durch
  `McpToolResults.SolutionNotLoaded()` aus step-003 abgedeckt, hier nur
  wiederverwendet, nicht neu gebaut.

## Aktueller Projektzustand (JIT-Kontext)

- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (aus step-003, private
  `BuildToolCollection(McpCodeGraphServer mcpState)` mit genau einem
  `tools.Add(McpServerTool.Create(...))`-Aufruf fuer `find_symbol`) ist der
  etablierte, einzige Registrierungspunkt fuer alle MCP-Tools — hier wird
  eine zweite `tools.Add(...)`-Zeile ergaenzt, keine neue Registrierungs-
  Datei noetig (siehe Abschnitt "TD-004" unten fuer die Begruendung, warum
  keine strukturelle Aufteilung noetig ist).
- `src/AiNetLinter/Mcp/McpToolResults.cs` (aus step-003) bietet bereits
  `Error(code, message, context, hint)`, `SolutionNotLoaded()`, `Text(text)`
  — wird direkt wiederverwendet, keine Duplikation.
- `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` (aus step-003) ist das
  strukturelle Vorbild fuer den neuen `FindReferencesTool`: statische
  Klasse, `ExecuteAsync(McpCodeGraphServer state, ..., CancellationToken ct)`
  prueft zuerst `state.GetCurrentSolution()`, delegiert dann an eine reine,
  von `McpCodeGraphServer` unabhaengige Kernfunktion (`FindMatchesAsync`
  dort, hier `FindReferenceLocationsAsync`) — direkt unit-testbar ohne
  Solution-Load-Overhead im schnellen Testpfad.
- **`src/AiNetLinter/Core/DiffImpactAnalyzer.cs`** (vollstaendig gelesen,
  keine Aenderung an der fachlichen Logik geplant, nur Sichtbarkeit):
  - `FindCallSitesAsync(ISymbol symbol, Solution solution)` (Zeile 281-302,
    aktuell `private static`) liefert bereits exakt das geforderte Format
    (`{relativePath}:{line} - Aufruf von '{Type}.{Member}' in Projekt
    '{ProjectName}'`) ueber `SymbolFinder.FindReferencesAsync` — das ist
    fachlich identisch zu dem, was `find_references` laut Konzept-Tabelle
    liefern soll. Wird in diesem Step auf `internal static` angehoben und
    direkt vom neuen Tool aufgerufen (kein Nachbau).
  - `FindDocumentByPath(Solution solution, string filePath)` (Zeile
    176-184, aktuell `private static`) wird ebenfalls auf `internal static`
    angehoben — wird fuer die Positions-basierte Symbolaufloesung (Datei:
    Zeile:Spalte → Document) gebraucht, exakt dieselbe Suche, die
    `DiffImpactAnalyzer` intern schon fuer Hunk-Zuordnung nutzt.
  - Beide Methoden bleiben inhaltlich unveraendert — reine
    Sichtbarkeitsaenderung (`private` → `internal`), keine Signaturaenderung,
    kein Verhaltensrisiko fuer die bestehenden `DiffImpactAnalyzer`-Nutzer
    (`--impact`-Command/-Tests bleiben unberuehrt).
- **`src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs`**: `FormatSymbolLocations`
  (Zeile 76-86, aktuell `private static`) formatiert bereits genau die
  Datei:Zeile/Kind/Signatur-Zeile, die fuer die Ambiguitaets-Fehlermeldung
  bei mehrdeutigem qualifiziertem Namen gebraucht wird (Liste der
  Kandidaten). Wird auf `internal static` angehoben und vom neuen Tool
  wiederverwendet statt dupliziert.
- **Test-Fixture-Lage:** `tests/Fixtures/BaselineMini/` (einzige bestehende
  Solution-Fixture) enthaelt nur `ViolatingClass` mit einer einzigen
  `Value`-Property, **keine** Aufrufstelle irgendeiner Art im Fixture selbst
  — ungeeignet, um `find_references` sinnvoll gegen eine echte
  Aufrufstelle zu testen. `BaselineMini` wird bewusst **nicht** um eine
  Consumer-Klasse erweitert, weil mehrere bestehende Tests
  (`src/AiNetLinter.Tests/Baseline/BaselineCliTests.cs`,
  `Cli/CliIntegrationTests.cs`, `Suppression/DisableAllCliTests.cs`) gegen
  das **gesamte** `workspace.RootPath`-Verzeichnis linten/Baseline bilden —
  eine zusaetzliche Datei wuerde deren Verstoss-/Baseline-Zaehlung
  unkontrolliert veraendern. Stattdessen: neue, eigene, minimale Solution-
  Fixture `tests/Fixtures/SymbolGraphMini/` (zwei Klassen, eine ruft die
  andere auf) nur fuer Symbolgraph-Tool-Tests — analog zum bestehenden
  Muster `BaselineMiniFixtureWorkspace` (isolierte Temp-Kopie pro Testlauf).
- **TD-004 (`McpServerOptionsFactory` nahe `AIContextFootprint`-Limit)
  geprueft, Ergebnis: Risiko fuer diesen Step gering, keine strukturelle
  Vorab-Aenderung noetig.** `src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs`
  vollstaendig gelesen: die Metrik traversiert ausschliesslich **Member-
  Signaturen** (Feld-/Property-Typen, Methoden-Parameter/Rueckgabetypen)
  rekursiv ueber eigene Typen — **nicht** Methodenkoerper (bestaetigt exakt
  die Beobachtung aus `step-003/step-result.md`: `mcpState` als lokale
  Variable blieb unsichtbar, als Parameter wurde sie sichtbar). Ein weiterer
  `tools.Add(McpServerTool.Create(lambda => FindReferencesTool.ExecuteAsync(...), ...))`
  innerhalb der **bestehenden** Methode `BuildToolCollection(McpCodeGraphServer mcpState)`
  aendert deren Signatur nicht — die erfasste Delegate-Closure erzeugt zwar
  eine Compiler-generierte Display-Klasse, aber `QueueMemberSymbols` in der
  Metrik behandelt nur `IFieldSymbol`/`IPropertySymbol`/`IMethodSymbol` explizit,
  nicht generische verschachtelte `INamedTypeSymbol`-Member — Display-Klassen
  werden also gar nicht rekursiv aufgeloest. Die neue `FindReferencesTool`-
  Klasse selbst hat exakt dieselbe Form wie `FindSymbolTool`
  (`McpCodeGraphServer`, `string`, `CancellationToken` als Parameter) und
  ruft `DiffImpactAnalyzer.FindCallSitesAsync` nur im Methodenkoerper auf
  (kein `DiffImpactAnalyzer`-Parameter/Rueckgabetyp in einer eigenen
  Member-Signatur) — `DiffImpactAnalyzer`s eigene, groessere transitive
  Typkette (Git-Diff-Parsing etc.) fliesst dadurch **nicht** in
  `FindReferencesTool`s Footprint ein. Trotzdem Pflicht-Verifikation per
  Selbst-Lint (siehe Definition of Done) — falls die Erwartung hier doch
  nicht zutrifft (z. B. wegen eines Metrik-Details, das beim Lesen
  uebersehen wurde), ist die dokumentierte Ausweich-Option: die
  `tools.Add(...)`-Aufrufe aus `BuildToolCollection` in kleinere, pro
  Tool(-Gruppe) benannte private Methoden aufteilen (z. B.
  `RegisterFindSymbol`/`RegisterFindReferences`), **nicht** sofort eine
  komplett neue Registrierungs-Datei je Tool — das waere der im
  Coder-Skill vorgesehene "Vorab-Klassifikation"-Fix, kein Vorgriff auf
  einen groesseren Umbau.

## Intention

`find_references` aufloest einen Symbol-Identifikator (entweder
`Datei:Zeile:Spalte` oder qualifizierter/teil-qualifizierter Name) zu genau
einem Roslyn-`ISymbol` und liefert dessen Aufrufstellen ueber die bereits
bestehende `DiffImpactAnalyzer.FindCallSitesAsync`-Logik — kein Neubau der
Referenzsuche, nur der zusaetzliche Schritt "Identifikator → Symbol", den
`get_impact` (bereits ueber Git-Diff-Hunks vorgefindet) bisher nicht
brauchte. Nach diesem Step kann ein Agent gezielt "wer ruft X auf" fragen,
ohne selbst erst `find_symbol` + manuelles Nachschlagen der Zeile
kombinieren zu muessen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` (Zeile 176-184, 281-302)

- **Was:** Sichtbarkeit von `FindDocumentByPath` und `FindCallSitesAsync`
  von `private static` auf `internal static` anheben. Xml-Doc-Kommentar
  ergaenzen ("wird auch von `FindReferencesTool` (MCP) wiederverwendet").
  Keine Logikaenderung.
- **Warum:** Wiederverwendung statt Neubau (`konzept.md`
  "Wiederverwendung statt Neubau", `AiNetLinterRichtlinien.mdc` §1) — exakt
  die im Konzept genannte Basis fuer `find_references`.

### Datei 2: `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` (Zeile 76)

- **Was:** Sichtbarkeit von `FormatSymbolLocations` von `private static`
  auf `internal static` anheben. Keine Logikaenderung.
- **Warum:** Wiederverwendung fuer die Ambiguitaets-Fehlermeldung (Liste
  der Kandidaten bei mehrdeutigem qualifiziertem Namen) in
  `FindReferencesTool`, statt dieselbe Datei:Zeile/Kind/Signatur-
  Formatierung ein zweites Mal zu schreiben.

### Datei 3: `src/AiNetLinter/Output/LinterErrorCodes.cs`

- **Was:** Zwei neue Konstanten ergaenzen: `SymbolNotFound = "SYMBOL_NOT_FOUND"`,
  `AmbiguousSymbol = "AMBIGUOUS_SYMBOL"`.
- **Warum:** `find_references` braucht zwei neue, vom bestehenden
  `SolutionNotLoaded`/`ResourceNotFound`/`AmbiguousSolution` unterschiedene
  strukturierte Fehlercodes (Symbol-Identifikator loest auf nichts auf /
  loest auf mehrere Symbole auf) — konsistent mit dem bestehenden Muster
  je ein Code pro unterscheidbarem Fehlerfall.

### Datei 4: `src/AiNetLinter/Mcp/McpToolResults.cs`

- **Was:** Zwei neue statische Hilfsmethoden ergaenzen:
  - `SymbolNotFound(string identifier)` → `Error(LinterErrorCodes.SymbolNotFound, ...)`
    mit `identifier` im `context`.
  - `AmbiguousSymbol(string identifier, IEnumerable<string> candidateLines)`
    → `Error(LinterErrorCodes.AmbiguousSymbol, ...)` mit den Kandidaten
    (durch Zeilenumbruch getrennt) im `context`.
- **Warum:** Gleiches Wiederverwendungs-Muster wie `SolutionNotLoaded()` —
  zentrale, fuer alle Tools wiederverwendbare Fehler-Bausteine statt
  Ad-hoc-`Error(...)`-Aufrufe direkt im Tool.

### Datei 5: `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` (neu)

- **Was:** Neue statische Klasse `FindReferencesTool`, siehe Code-Skizze
  unten fuer die volle Struktur:
  - `ExecuteAsync(McpCodeGraphServer state, string symbolIdentifier, CancellationToken ct)`
    — Solution-Check (`McpToolResults.SolutionNotLoaded()` bei `null`),
    dann `ResolveSymbolAsync`, dann bei Erfolg
    `DiffImpactAnalyzer.FindCallSitesAsync(symbol, solution)` aufrufen und
    als Text formatieren ("Keine Aufrufstellen gefunden fuer 'X'" bei
    leerer Liste, sonst `string.Join("\n", callSites)`).
  - `ResolveSymbolAsync(Solution solution, string identifier, CancellationToken ct)`
    (`internal`, direkt testbar) — probiert zuerst `TryParsePosition`
    (Datei:Zeile:Spalte), sonst qualifizierter-Name-Pfad ueber
    `SymbolFinder.FindSourceDeclarationsAsync` + Suffix-Abgleich auf
    `ToDisplayString()` ohne Parameterliste. Liefert entweder genau ein
    `ISymbol`, oder ein strukturiertes Fehlerergebnis (kein Treffer /
    mehrdeutig) — siehe Code-Skizze fuer exakte Rueckgabeform
    (`(ISymbol? Symbol, CallToolResult? Error)`-Tuple, damit
    `ExecuteAsync` nicht zwei verschiedene Rueckgabewege selbst bauen muss).
  - `TryParsePosition`, `ResolveSymbolAtToken`, `StripParameterList` als
    private Hilfsmethoden.
- **Warum:** Kernstueck dieses Steps — die Identifikator-zu-Symbol-
  Aufloesung, die `konzept.md`s Tool-Tabelle fuer `find_references`
  fordert und die bisher (nur `get_impact`-Basis ueber Git-Diff-Hunks)
  nicht existiert.

### Datei 6: `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (Zeile 40-55)

- **Was:** In `BuildToolCollection` einen zweiten
  `tools.Add(McpServerTool.Create(...))`-Aufruf ergaenzen:
  ```csharp
  tools.Add(McpServerTool.Create(
      (string symbolIdentifier, CancellationToken ct = default) =>
          FindReferencesTool.ExecuteAsync(mcpState, symbolIdentifier, ct),
      new McpServerToolCreateOptions
      {
          Name = "find_references",
          Description = "Findet alle Aufrufstellen eines C#-Symbols (Datei:Zeile:Spalte " +
              "oder qualifizierter/teil-qualifizierter Name). Deckt nur .cs-Dateien ab, " +
              "keine .js/.razor/.xaml/.html/.css-Dateien.",
      }));
  ```
- **Warum:** Registrierung des neuen Tools ueber den bestehenden,
  etablierten Sammelpunkt — siehe "Aktueller Projektzustand" fuer die
  TD-004-Abwaegung, warum keine strukturelle Aufteilung noetig ist.

### Datei 7 (neu, Test-Fixture): `tests/Fixtures/SymbolGraphMini/`

- **Was:** Neue, minimale Solution-Fixture nach Vorbild von
  `tests/Fixtures/BaselineMini/` (`.slnx` + `.csproj`, TFM `net10.0`,
  analoge `PropertyGroup`), aber mit **zwei** Klassen statt einer:
  - `src/SymbolGraphMini/Greeter.cs`:
    ```csharp
    namespace SymbolGraphMini;

    public class Greeter
    {
        public string Greet(string name) => $"Hello, {name}";
    }
    ```
  - `src/SymbolGraphMini/Caller.cs`:
    ```csharp
    namespace SymbolGraphMini;

    public class Caller
    {
        public string Run()
        {
            var greeter = new Greeter();
            return greeter.Greet("World");
        }
    }
    ```
- **Warum:** `BaselineMini` bewusst nicht erweitert (siehe "Aktueller
  Projektzustand"). Diese Fixture ist die kleinste Form, die eine echte,
  ueber Roslyn auffindbare Aufrufstelle (`greeter.Greet("World")` im
  gleichen Projekt, anderer Datei) liefert.

### Datei 8 (neu): `src/AiNetLinter.Tests/Fixtures/SymbolGraphMiniFixtureWorkspace.cs`

- **Was:** 1:1-Analogon zu `BaselineMiniFixtureWorkspace.cs` (isolierte
  Temp-Kopie pro Testlauf), zusaetzlich `GreeterPath`/`CallerPath`-
  Properties analog zu `ViolatingClassPath`.
- **Warum:** gleiches Isolations-Muster wie bei `BaselineMini` — parallel
  laufende Tests duerfen sich nicht dieselbe Solution-Kopie teilen.

### Datei 9 (neu): `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs`

- **Was:** Unit-Tests gegen `FindReferencesTool.ExecuteAsync`/`ResolveSymbolAsync`,
  siehe "Tests" unten fuer die vollstaendige Liste.
- **Warum:** analog zu `FindSymbolToolTests.cs` (step-003) — reine
  In-Process-Tests, kein Subprozess noetig fuer die meisten Faelle.

### Datei 10: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (Zeile 132-152)

- **Was:** `RunAsync_ValidFixture_ServerRespondsWithFindSymbolTool` anpassen
  — mit zwei registrierten Tools ist `Assert.Single(tools)` nicht mehr
  korrekt. Umbenennen zu `RunAsync_ValidFixture_ServerRespondsWithBothTools`,
  `Assert.Equal(2, tools.Count)` + je einen Namens-Check fuer
  `find_symbol` und `find_references`. Zusaetzlich neuer E2E-Test
  `RunAsync_ValidFixture_FindReferencesReturnsCallSite` (gegen die neue
  `SymbolGraphMiniFixtureWorkspace`, ruft `find_references` mit
  `"Greeter.Greet"` auf, prueft `IsError != true` und dass der Text
  `Caller.cs` enthaelt).
- **Warum:** Der bestehende E2E-Test wuerde sonst durch dieses Step
  fehlschlagen (echter Regressions-Fund, kein Scope-Zuwachs) — Konsequenz
  aus der Tool-Zaehl-Annahme in step-003, die mit einem zweiten Tool nicht
  mehr stimmt.

## Tests

- [ ] `FindReferencesToolTests.ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
      — `new McpCodeGraphServer(null)` → `ExecuteAsync` → `IsError == true`,
      Text enthaelt `SOLUTION_NOT_LOADED` (analog zum bestehenden
      `FindSymbolTool`-Test aus `fix-01`).
- [ ] `FindReferencesToolTests.ResolveSymbolAsync_QualifiedName_ReturnsSingleMatch`
      — gegen `SymbolGraphMiniFixtureWorkspace`, Identifikator
      `"Greeter.Greet"` → genau ein `ISymbol` (Methode `Greet`), kein Fehler.
- [ ] `FindReferencesToolTests.ResolveSymbolAsync_UnknownName_ReturnsSymbolNotFoundError`
      — Identifikator `"DoesNotExistXyz"` → `Error` mit `SYMBOL_NOT_FOUND`.
- [ ] `FindReferencesToolTests.ResolveSymbolAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbolError`
      — Identifikator ohne Punkt, der auf mehrere Symbole gleichen
      Namens passt (z. B. `"Run"`, falls im Fixture zwei Methoden mit
      diesem Namen existieren — sonst gezielt einen zweiten Typ mit
      gleichnamiger Methode in `SymbolGraphMini` ergaenzen, um den Pfad
      wirklich zu testen statt ihn nur zu unterstellen) → `Error` mit
      `AMBIGUOUS_SYMBOL`, Text enthaelt beide Fundstellen.
- [ ] `FindReferencesToolTests.ResolveSymbolAsync_PositionIdentifier_ReturnsSymbolAtPosition`
      — Identifikator im Format `<GreeterPath>:3:19` (Zeile/Spalte der
      `Greet`-Methodendeklaration in der Fixture) → dasselbe Symbol wie
      der qualifizierte-Name-Test.
- [ ] `FindReferencesToolTests.ExecuteAsync_ValidQualifiedName_ReturnsCallSiteInCaller`
      — End-to-End innerhalb des Tools (kein MCP-Client noetig):
      `ExecuteAsync(state, "Greeter.Greet", ct)` gegen einen
      `McpCodeGraphServer`, der mit dem `SymbolGraphMini`-Fixture geladen
      wurde → Ergebnis-Text enthaelt `Caller.cs`.
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithBothTools`
      (umbenannt/angepasst, siehe Datei 10 oben).
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_FindReferencesReturnsCallSite`
      (neu, siehe Datei 10 oben).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" (Datei 1-10) umgesetzt
- [ ] `dotnet build AiNetLinter.slnx` gruen, 0 Warnungen
- [ ] `dotnet test AiNetLinter.slnx` gruen (alle Tests, inkl. neue)
- [ ] Selbst-Lint (`ainetlinter --config rules.json --path ./src/`) `OK`,
      0 Violations — **explizit auch auf `AIContextFootprint` fuer
      `McpServerOptionsFactory`/`FindReferencesTool` pruefen** (siehe
      TD-004-Abschnitt oben; Ausweich-Option dort dokumentiert, falls die
      Erwartung nicht zutrifft)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Englisch,
      `[codegraph-mcp]`-Suffix im Subject, siehe Tech-Stack-Notiz in
      `roadmap.md`)
- [ ] `step-004/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt
- [ ] `### Commit-Vorschlag`-Abschnitt am Ende der Coder-Antwort
      (`AiNetLinterRichtlinien.mdc` §4)

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — `#nullable enable`, `sealed` (Ausnahme:
  statische Klassen sind exemptiert, wie bereits bei `FindSymbolTool`/
  `McpToolResults`/`McpServerOptionsFactory`), Methodenlaenge (≤60 Zeilen,
  `ResolveSymbolAsync`/`ExecuteAsync` klein und fokussiert halten, bei
  Bedarf weiter in private Hilfsmethoden zerlegen wie in `FindSymbolTool`
  vorgemacht), max. 4 Parameter, `AIContextFootprint` (2500) — siehe
  TD-004-Abwaegung oben, Pflicht-Verifikation per Selbst-Lint.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1/§2 — kein DI-Container,
  kein Plugin-System: neues Tool wird wie `find_symbol` per Delegate-
  Closure registriert, keine neue Abstraktionsebene.

## Bekannte Ausnahmen

- Die Positions-Parse-Heuristik (`TryParsePosition`: letzte zwei
  `:`-getrennte Segmente muessen Ganzzahlen sein) kann in seltenen
  theoretischen Faellen einen qualifizierten Namen falsch als
  Positions-Angabe fehldeuten, wenn der Name selbst auf
  `:<Ziffern>:<Ziffern>` endet — praktisch ausgeschlossen, da C#-Bezeichner
  keine Doppelpunkte enthalten koennen. Bewusst keine strengere Heuristik
  (z. B. Pfad-Existenzpruefung vor der Zerlegung), da das den Code
  unnoetig verkomplizieren wuerde fuer einen Fall, der syntaktisch nicht
  auftreten kann.
- `ResolveSymbolAtToken` deckt den haeufigen Fall ab (Cursor auf
  Deklaration ODER auf einer Verwendungsstelle), aber nicht jeden
  denkbaren Roslyn-Sonderfall (z. B. Cursor exakt auf einem Operator-Token
  ohne umschliessenden Ausdruck). Kein Blocker fuer diesen Step — die
  Tests decken die beiden praktisch relevanten Faelle
  (Deklarationszeile, siehe Testliste) ab; weitere Sonderfaelle koennen in
  einem spaeteren Fix-Step nachgezogen werden, falls der Kritiker eine
  konkrete Luecke findet.

## Code-Skizze (optional)

```csharp
// src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs
internal static class FindReferencesTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string symbolIdentifier, CancellationToken ct)
    {
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var (symbol, error) = await ResolveSymbolAsync(solution, symbolIdentifier, ct);
        if (error is not null) return error;

        var callSites = await DiffImpactAnalyzer.FindCallSitesAsync(symbol!, solution);
        if (callSites.Count == 0)
        {
            return McpToolResults.Text($"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'");
        }

        return McpToolResults.Text(string.Join("\n", callSites));
    }

    internal static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolAsync(
        Solution solution, string identifier, CancellationToken ct)
    {
        if (TryParsePosition(identifier, out var path, out var line, out var column))
        {
            return await ResolveByPositionAsync(solution, identifier, path, line, column, ct);
        }

        return await ResolveByNameAsync(solution, identifier, ct);
    }

    // ResolveByPositionAsync: DiffImpactAnalyzer.FindDocumentByPath, GetSyntaxRootAsync,
    // GetTextAsync (Lines[line-1].Start + column-1), root.FindToken, ResolveSymbolAtToken.
    // ResolveByNameAsync: SymbolFinder.FindSourceDeclarationsAsync (Filter auf letztes
    // Namenssegment), dann Kandidaten per StripParameterList(...).EndsWith(identifier) filtern,
    // 0 -> SymbolNotFound, 1 -> Erfolg, >1 -> AmbiguousSymbol (via FindSymbolTool.FormatSymbolLocations).
}
```

## Notes

- **Bewusst kein `search_pattern`-Fallback in diesem Step:** Der
  Miss-Hint-Mechanismus (`konzept.md` Muss-Haben) betrifft laut
  `roadmap.md` EPIC-05 mehrere Tools nachtraeglich, nicht EPIC-03 einzeln
  — `find_symbol` hat ihn in step-003 auch nicht bekommen. Kein
  Konzept-Verstoss, sondern konsistent mit der bestehenden Abgrenzung.
- **`get_impact` (naechstes EPIC-03-Tool) profitiert vermutlich von
  derselben Identifikator-Aufloesung** (`ResolveSymbolAsync`), falls es
  eine direkte Symbol-Eingabe statt nur Git-Ref unterstuetzen soll — das
  ist aber bewusst nicht Teil dieses Steps (JIT-Prinzip, kein
  Vorausplanen). Falls ein spaeterer Step das braucht, sollte er
  `FindReferencesTool.ResolveSymbolAsync` wiederverwenden statt eine
  dritte Kopie zu bauen.
