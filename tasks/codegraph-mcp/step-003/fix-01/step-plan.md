---
status: done (pending audit)
type: step-plan
task: codegraph-mcp
step: 003/fix-01
title: "Fix: Test-Abdeckung für den 'Solution nicht geladen'-Fehlerpfad in find_symbol"
epic: EPIC-03
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T19:00:00Z
related_to: ["tasks/codegraph-mcp/step-003/step-review.md"]
---

# Step 003/fix-01: Fix: Test-Abdeckung für den "Solution nicht geladen"-Fehlerpfad in find_symbol

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-03` (unverändert — dieser Fix korrigiert step-003, legt
  kein neues Epic an)
- **Auslöser:** `tasks/codegraph-mcp/step-003/step-review.md`, Abschnitt
  „Findings", Punkt 1 — `[MAJOR]` `[Logische Korrektheit]`: Der Pfad
  `FindSymbolTool.ExecuteAsync` mit `state.GetCurrentSolution() == null`
  (`McpCodeGraphServer.IsLoaded == false`) →
  `McpToolResults.SolutionNotLoaded()` ist an keiner Stelle automatisiert
  getestet; `McpToolResults` selbst hat keine eigenen Tests.

## Aktueller Projektzustand (JIT-Kontext)

Verifiziert direkt am Code (nicht nur laut Review-Behauptung):

- `McpCodeGraphServer`-Konstruktor
  (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs:28`):
  `public McpCodeGraphServer(SourceFileCatalog? catalog, ILintConsole? console = null)`.
  `catalog` ist nullable, `console` hat einen Default. Der vom Kritiker
  vorgeschlagene Aufruf `new McpCodeGraphServer(null)` ist **exakt so**
  ohne Anpassung kompilierbar/aufrufbar — kein Subprozess, keine Fixture
  nötig. Der Konstruktor ruft bei `catalog is null` `InitializeFileState`
  gar nicht auf (Zeile 33-36), es passiert nichts Aufwendiges beim
  Konstruieren. `IsLoaded => _catalog is not null` (Zeile 39) ist danach
  `false`, `GetCurrentSolution()` liefert `null` (Zeile 49).
- `FindSymbolTool.ExecuteAsync`
  (`src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs:27-35`) prüft
  `state.GetCurrentSolution()` und gibt bei `null` sofort
  `McpToolResults.SolutionNotLoaded()` zurück, ohne `FindMatchesAsync`
  aufzurufen — der zu testende Pfad ist also rein synchron erreichbar,
  keine Solution/Fixture im Spiel.
- `McpToolResults.SolutionNotLoaded()`
  (`src/AiNetLinter/Mcp/McpToolResults.cs:35-41`) ruft
  `Error(LinterErrorCodes.SolutionNotLoaded, ...)` auf.
  `LinterErrorCodes.SolutionNotLoaded` (`src/AiNetLinter/Output/LinterErrorCodes.cs:21`)
  hat den Wert `"SOLUTION_NOT_LOADED"`.
  `LinterErrorFormatter.Format` (`src/AiNetLinter/Output/LinterErrorFormatter.cs:13-22`)
  erzeugt Text im Format `[ERROR]: {code}: {message}...` — der
  resultierende `CallToolResult.Content`-Text enthält also wörtlich
  `[ERROR]: SOLUTION_NOT_LOADED:`.
- `CallToolResult.IsError` (Typ `ModelContextProtocol.Protocol.CallToolResult`)
  wird von `Error(...)` auf `true` gesetzt (`McpToolResults.cs:26`).
- Bestehende Testdatei `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs`
  ist bereits mit `[Collection("ConsoleTestCollection")]` annotiert und
  testet ausschließlich `FindMatchesAsync` (die reine Formatierungs-
  logik), nicht `ExecuteAsync` — passender Ort, um den fehlenden
  `ExecuteAsync`/Null-Solution-Test zu ergänzen.
- Es existiert noch keine `McpToolResultsTests.cs`
  (`src/AiNetLinter.Tests/Mcp/` enthält nur `McpCodeGraphServerTests.cs`
  und `Tools/FindSymbolToolTests.cs`, siehe Glob-Ergebnis) — die vom
  Kritiker optional vorgeschlagene direkte Testdatei für
  `McpToolResults` existiert also noch nicht und wird neu angelegt.

## Intention

Nach diesem Fix ist der zweite Hauptpfad von `FindSymbolTool.ExecuteAsync`
(„keine Solution geladen → strukturierte `[ERROR]`-Antwort statt
Absturz") durch einen günstigen In-Process-Unit-Test abgedeckt, sowie die
darunterliegende, für alle 9 EPIC-03-Tools wiederverwendete
`McpToolResults`-Infrastruktur (`Error`/`SolutionNotLoaded`/`Text`) durch
eigene, gezielte Unit-Tests — beides ohne Subprozess/Fixture, da
`new McpCodeGraphServer(null)` und `McpToolResults`s Methoden direkt
synchron aufrufbar sind.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs`

- **Was:** Neuer Testfall
  `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`:
  - `var state = new McpCodeGraphServer(null);`
  - `var result = await FindSymbolTool.ExecuteAsync(state, "irrelevant", null, CancellationToken.None);`
  - `Assert.True(result.IsError);`
  - Text aus `result.Content` extrahieren (ein `TextContentBlock`,
    Cast wie in bestehenden Tests/`McpServerCommandTests.cs` üblich,
    z. B. `Assert.Single(result.Content)` +
    `Assert.IsType<TextContentBlock>(result.Content[0])` → `.Text`) und
    `Assert.Contains("SOLUTION_NOT_LOADED", text)` prüfen.
  - Testklasse bleibt wie bisher `[Collection("ConsoleTestCollection")]`
    annotiert (kein neuer Bedarf dafür in diesem Test, aber Konsistenz
    mit den übrigen Tests derselben Klasse).
- **Warum:** Direkter Beleg, dass der laut `konzept.md` geforderte
  Fehlerpfad für `find_symbol` tatsächlich über das echte Tool-Delegate
  funktioniert — schließt exakt die vom Kritiker benannte Lücke.

### Datei 2: `src/AiNetLinter.Tests/Mcp/McpToolResultsTests.cs` (neu)

- **Was:** `internal`/`public sealed class McpToolResultsTests` mit drei
  kleinen Testfällen gegen `AiNetLinter.Mcp.McpToolResults` direkt (kein
  `McpCodeGraphServer`/keine Solution nötig):
  - `Error_BuildsIsErrorResultWithFormattedText`: `McpToolResults.Error("TEST_CODE", "Testnachricht")`
    aufrufen, `Assert.True(result.IsError)`, Text enthält
    `"[ERROR]: TEST_CODE: Testnachricht"`.
  - `SolutionNotLoaded_ReturnsErrorWithSolutionNotLoadedCode`:
    `McpToolResults.SolutionNotLoaded()` aufrufen, `Assert.True(result.IsError)`,
    Text enthält `"SOLUTION_NOT_LOADED"`.
  - `Text_BuildsNonErrorResultWithGivenText`: `McpToolResults.Text("Hallo")`
    aufrufen, `Assert.True(result.IsError is null or false)`, Text ist
    exakt `"Hallo"`.
  - Kein `[Collection("ConsoleTestCollection")]` nötig — keine
    `SourceFileCatalog.LoadAsync`-Nutzung, keine TD-003-Kollisionsgefahr.
- **Warum:** Vom Kritiker als sinnvolle Ergänzung benannt, da
  `McpToolResults` neue, von allen 9 EPIC-03-Tools wiederverwendete
  Infrastruktur ist — direkte Tests hier sind günstiger und präziser als
  sie nur indirekt über `FindSymbolTool` mitzutesten.

## Tests

- [ ] `FindSymbolToolTests.ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode` (neu, Datei 1)
- [ ] `McpToolResultsTests.Error_BuildsIsErrorResultWithFormattedText` (neu, Datei 2)
- [ ] `McpToolResultsTests.SolutionNotLoaded_ReturnsErrorWithSolutionNotLoadedCode` (neu, Datei 2)
- [ ] `McpToolResultsTests.Text_BuildsNonErrorResultWithGivenText` (neu, Datei 2)
- [ ] Bestehende Tests bleiben grün (`dotnet test AiNetLinter.slnx`)

## Definition of Done

- [ ] Beide „Konkrete Änderungen" umgesetzt (Datei 1, Datei 2)
- [ ] `dotnet build AiNetLinter.slnx` grün, keine neuen Warnungen
      (`TreatWarningsAsErrors`)
- [ ] `dotnet test AiNetLinter.slnx` grün (neue + bestehende Tests)
- [ ] Selbst-Lint (`ainetlinter --config rules.json --path .`) weiterhin
      `OK`, 0 Violations
- [ ] Commit auf aktuellem Branch (Conventional Commit, Englisch, Suffix
      `[codegraph-mcp]`, siehe Tech-Stack-Notiz in `roadmap.md`)
- [ ] `step-003/fix-01/step-result.md` geschrieben
- [ ] `status` in diesem `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt
- [ ] `### Commit-Vorschlag`-Abschnitt am Ende der Coder-Antwort
      (`AiNetLinterRichtlinien.mdc` §4)

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — `#nullable enable` in neuer Datei
  (`McpToolResultsTests.cs`), `sealed` auf der neuen Testklasse, kein
  leeres `catch`, Methodenlänge/Parameteranzahl (hier trivial erfüllt,
  jeder Testfall ist eine kurze eigenständige Methode ohne Parameter).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — Commit-Vorschlag-Pflicht.
  §5 Zero-Warning-Direktive.

## Bekannte Ausnahmen

- Keine.

## Notes

- **Scope-Disziplin:** Dieser Fix behebt ausschließlich Finding 1 aus
  `step-003/step-review.md`. Keine weiteren Änderungen an
  `FindSymbolTool.cs`, `McpToolResults.cs`, `McpServerCommand.cs` oder
  sonstigen step-003-Dateien. `TD-004` (Tech-Debt aus demselben Review)
  ist explizit nicht Teil dieses Fixes.
- Der exakte Weg, den Text aus `CallToolResult.Content` zu extrahieren,
  sollte sich am bereits bestehenden Muster in
  `McpServerCommandTests.cs` orientieren (dort wird `CallToolResult`
  bereits aus einem echten `CallToolAsync`-Ergebnis ausgewertet) — Coder
  verifiziert die exakte Cast-Syntax gegen `TextContentBlock`, falls sie
  von der hier skizzierten abweicht.
