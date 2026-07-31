---
status: open
type: step-plan
task: codegraph-mcp
step: 003
title: "Tool-Registrierungs-Infrastruktur + erstes Tool: find_symbol"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T17:00:00Z
related_to: []
---

# Step 003: Tool-Registrierungs-Infrastruktur + erstes Tool: find_symbol

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-03` aus `roadmap.md` — Symbolgraph-Tools (`find_symbol`,
  `find_references`, `get_impact`, `get_type_hierarchy`,
  `get_file_skeleton`). Dieser Step deckt EPIC-03 **nicht vollständig**
  ab (5 Tools sind zu groß für einen Step): er liefert die
  Tool-Registrierungs-Infrastruktur, mit der **jedes** der 5 Tools später
  angebunden wird, plus das **erste** konkrete Tool, `find_symbol`. Die
  restlichen 4 Tools folgen in weiteren EPIC-03-Steps.
- **Konzept-Referenz:** `konzept.md` Muss-Haben "Tool-Set wie unten unter
  'Wie' beschrieben (9 Tools)", "Fehlerbehandlung ohne Absturz" Teil 1
  (Solution lädt gar nicht → jeder Tool-Call liefert `[ERROR]`), Tabelle
  unter "Wie" Zeile `find_symbol` (Basis: `SymbolFinder.FindDeclarationsAsync`,
  Input "Name/Pattern (Substring/Glob), optionaler Kind-Filter", Output
  "Fundstellen: Datei:Zeile, Kind, Signatur, umschließender Typ").
  **Bewusst nicht Teil dieses Steps:** der Miss-Hint-Text-Fallback bei
  fehlendem C#-Treffer und die zentrale Scope-Kommunikation im
  `initialize`-`instructions`-Feld — das ist laut `roadmap.md` explizit
  EPIC-05 ("betrifft mehrere der in EPIC-03 gebauten Tools nachträglich,
  daher eigenes Epic"). `find_symbol` liefert in diesem Step bei keinem
  Treffer nur eine schlichte "keine Treffer"-Antwort, keinen Text-Fallback
  über `.js`/`.razor`/etc.

## Aktueller Projektzustand (JIT-Kontext)

- **`McpServerCommand.RunAsync`** (`src/AiNetLinter/Commands/McpServerCommand.cs:31-45`)
  konstruiert `mcpState` (den in step-002 gebauten `McpCodeGraphServer`)
  lokal in `RunAsync` und übergibt ihn **nicht** an `CreateServerOptions()`
  — die `McpServerOptions.ToolCollection` ist aktuell eine leere
  `McpServerPrimitiveCollection<McpServerTool>()` (Zeile 136), komplett
  unabhängig von `mcpState`. Dieser Step muss `CreateServerOptions`
  umbauen, damit sie den `mcpState`/die zu registrierenden Tools
  entgegennimmt (Tools müssen den Server-Zustand per Closure erreichen,
  da kein DI-Container existiert — siehe `konzept.md` "Zielplattformen").
- **`McpCodeGraphServer.GetCurrentSolution()`** (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs:45-54`)
  ist der einzige Zugriffspunkt auf die aktuelle, staleness-geprüfte
  `Solution` — liefert `null`, wenn beim Start keine Solution geladen
  werden konnte (`IsLoaded == false`). **Jedes** Tool muss diesen Weg
  nehmen (nicht `_catalog` direkt, das ist `private`), und muss den
  `null`-Fall in eine `[ERROR]`-Antwort übersetzen, statt eine
  `NullReferenceException` zu riskieren — das ist die konkrete Umsetzung
  von `konzept.md`s "Solution lädt gar nicht → jeder Tool-Call liefert
  eine strukturierte Fehlerantwort, Server bleibt am Leben" für das
  erste Tool.
- **MCP-SDK-API tatsächlich verifiziert** (Reflection gegen
  `src/AiNetLinter/bin/Debug/net10.0/ModelContextProtocol.Core.dll`,
  Paketversion `2.0.0`, da Doku/Web-Recherche zu diesem SDK dünn ist):
  - `McpServerTool.Create(Delegate method, McpServerToolCreateOptions options)`
    ist die Low-Level-Factory ohne Attribute/Assembly-Scan/DI — passend
    zur in step-001 getroffenen Entscheidung "kein `[McpServerToolType]`/
    `WithToolsFromAssembly()`". `McpServerToolCreateOptions.Name`/
    `.Description` steuern, was `tools/list` meldet.
  - `McpServerOptions.Capabilities.Tools.ToolCollection` ist ein
    `McpServerPrimitiveCollection<McpServerTool>` mit `.Add(McpServerTool)`
    — die bereits in `CreateServerOptions()` verwendete leere Instanz
    (Zeile 136) wird ab jetzt befüllt statt leer zu bleiben.
  - Tool-Delegates können `CallToolResult` (Typ `ModelContextProtocol.Protocol.CallToolResult`,
    Properties `IList<ContentBlock> Content`, `bool? IsError`) direkt
    zurückgeben (`ValueTask<CallToolResult>`/`Task<CallToolResult>` oder
    synchron) — das SDK reicht ihn laut `AIFunctionMcpServerTool.InvokeAsync`-
    Signatur (`ValueTask<CallToolResult>`) unverändert durch. Für den
    `[ERROR]`-Pfad heißt das: `IsError = true` **und** `Content` mit dem
    bestehenden `LinterErrorFormatter.Format(...)`-Text als
    `TextContentBlock` — beides zusammen, nicht nur der Text (Protokoll-
    Ebene + bestehendes Text-Format kombiniert).
  - Fällt beim Implementieren auf, dass sich diese Reflection-Ergebnisse
    (z. B. durch eine andere tatsächlich aufgelöste Paketversion) nicht
    bestätigen: wie in step-001 vorgemacht kurz in `step-result.md`
    dokumentieren, welche tatsächliche API genutzt wurde — kein Blocker,
    solange kein DI-Container/Assembly-Scan eingeführt wird.
- **`DiffImpactAnalyzer.FindCallSitesAsync`** (`src/AiNetLinter/Core/DiffImpactAnalyzer.cs:281-302`)
  ist das bestehende Vorbild für "Symbol → formatierte Fundstellen-Zeile":
  `PathNormalizer.ToRelative(outputRoot, filePath)` für den Pfad,
  `Path.GetDirectoryName(solution.FilePath)` als `outputRoot`, ein
  String pro Fundstelle. `find_symbol` übernimmt dieses Formatierungsmuster
  (Datei:Zeile - Kind 'ContainingType.Name' - Signatur), nutzt aber
  `SymbolFinder.FindSourceDeclarationsAsync(Solution, Func<string, bool> predicate, SymbolFilter, CancellationToken)`
  statt `FindReferencesAsync` — das ist die Solution-weite (nicht
  Projekt-weite) Deklarationssuche mit einem Namens-Prädikat, exakt
  passend für Substring-Matching über die ganze Solution in einem
  Aufruf statt manueller Iteration über `solution.Projects`.
- **`McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithEmptyToolList`**
  (`src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs:130-149`)
  asserted aktuell `Assert.Empty(tools)` — das bricht durch diesen Step
  zwangsläufig, sobald `find_symbol` registriert ist. Der bestehende Test
  muss umbenannt/angepasst werden (`tools/list` liefert jetzt genau 1
  Tool namens `find_symbol`), **nicht** einfach gelöscht — er bleibt der
  Beleg, dass `tools/list` grundsätzlich funktioniert. Gleichzeitig ist
  er das Vorbild für einen neuen E2E-Test, der tatsächlich
  `client.CallToolAsync("find_symbol", ...)` gegen die `BaselineMini`-
  Fixture aufruft (siehe "Tests").
- **`tests/Fixtures/BaselineMini/src/BaselineMini/ViolatingClass.cs`**
  ist die einzige Quelldatei der bestehenden Mini-Fixture — reicht als
  Ziel für einen Substring-Treffer-Test (`find_symbol("Violating")` →
  ein Treffer), Kind-Filter-Test und "kein Treffer"-Test
  (`find_symbol("DoesNotExist")`). Kein neues Fixture nötig.
- **TD-003 (Tech-Debt, nur zur Kenntnis, nicht Scope dieses Steps):**
  `SourceFileCatalog.RegisterMSBuild` hat eine bekannte, nicht gefixte
  Race Condition bei parallelen `LoadAsync`-Erstaufrufen
  (`tech-debt.md`). Neue Unit-Tests in diesem Step, die `SourceFileCatalog.LoadAsync`
  gegen die `BaselineMini`-Fixture aufrufen, erhöhen die
  Kollisionswahrscheinlichkeit weiter, falls sie parallel zu anderen
  Testklassen laufen, die das ebenfalls zum ersten Mal tun. Bestehende
  Tests mit demselben Bedarf (`McpServerCommandTests`) sind bereits mit
  `[Collection("ConsoleTestCollection")]` (`DisableParallelization = true`,
  `src/AiNetLinter.Tests/ConsoleTestCollection.cs`) annotiert —
  `McpCodeGraphServerTests.cs` dagegen **nicht**, was laut
  `step-002/step-review.md` mit ein Faktor für den beobachteten Flake
  war. **Kein Auftrag, TD-003 zu fixen** — aber neue Testklassen dieses
  Steps sollten aus Vorsicht ebenfalls `[Collection("ConsoleTestCollection")]`
  verwenden, wo sie `SourceFileCatalog.LoadAsync` aufrufen, um die
  Kollisionswahrscheinlichkeit nicht zusätzlich zu erhöhen (lokale
  Vorsichtsmaßnahme im eigenen Testcode, kein Fix der zugrunde liegenden
  Race in `RegisterMSBuild` selbst).

## Intention

Nach diesem Step registriert `McpServerCommand` sein erstes echtes Tool
(`find_symbol`) über eine wiederverwendbare Registrierungs-/Fehlerformat-
Infrastruktur, die alle folgenden EPIC-03-Tools (Folge-Steps) direkt
weiterverwenden können, statt sie pro Tool neu zu erfinden. `find_symbol`
durchsucht die vom `McpCodeGraphServer` resident gehaltene, staleness-
geprüfte `Solution` per Substring auf Namen (optionaler Kind-Filter) und
liefert Datei:Zeile/Kind/Signatur/umschließenden Typ pro Treffer zurück;
ist keine Solution geladen, liefert der Tool-Call eine strukturierte
`[ERROR]`-Antwort statt eines Absturzes.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Output/LinterErrorCodes.cs`

- **Was:** Neue Konstante `internal const string SolutionNotLoaded = "SOLUTION_NOT_LOADED";`
  ergänzen (Vorbild: `AmbiguousSolution` aus step-001).
- **Warum:** Eigener, sprechender Code für den Fall "Tool-Call, aber
  `McpCodeGraphServer.IsLoaded == false"`, statt den thematisch anderen
  `ResourceNotFound` zweckzuentfremden.

### Datei 2: `src/AiNetLinter/Mcp/McpToolResults.cs` (neu)

- **Was:** `internal static class McpToolResults` mit zwei kleinen
  Hilfsmethoden, die von `find_symbol` und allen folgenden Tools
  wiederverwendet werden:
  - `internal static CallToolResult Error(string code, string message, string? context = null, string? hint = null)`
    — baut einen `CallToolResult` mit `IsError = true` und genau einem
    `TextContentBlock`, dessen `Text` über das bestehende
    `LinterErrorFormatter.Format(code, message, context, hint)` erzeugt
    wird (`Output/LinterErrorFormatter.cs`, unverändert wiederverwendet).
  - `internal static CallToolResult SolutionNotLoaded()` — Kurzform für
    den in jedem Tool wiederkehrenden Fall
    `McpToolResults.Error(LinterErrorCodes.SolutionNotLoaded, "Solution ist nicht geladen — der MCP-Server konnte beim Start keine gültige Solution laden.", hint: "Server-Log auf [WARN]-Zeilen zum Ladefehler prüfen.")`.
  - `internal static CallToolResult Text(string text)` — baut einen
    `CallToolResult` mit `IsError` unbesetzt (bzw. `false`) und genau
    einem `TextContentBlock` mit dem übergebenen Text, für den
    Erfolgsfall.
- **Warum:** Zentraler, einmal geprüfter Umsetzungsort für "gleiches
  Format wie bestehendes `[ERROR]`-Schema" (`konzept.md`) auf
  Protokoll-Ebene (`IsError`) **und** Text-Ebene (`LinterErrorFormatter`)
  — vermeidet, dass jedes der 9 Tools dasselbe `CallToolResult`-Boilerplate
  einzeln nachbaut (Wiederverwendung statt Duplikation über die
  restlichen EPIC-03-Steps hinweg).

### Datei 3: `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` (neu)

- **Was:** `internal static class FindSymbolTool` mit zwei Methoden:
  - `internal static async Task<CallToolResult> ExecuteAsync(McpCodeGraphServer state, string namePattern, string? kind, CancellationToken ct)`
    — der eigentliche Tool-Einstiegspunkt (wird per Delegate registriert,
    siehe Datei 4): prüft `state.GetCurrentSolution()`, bei `null` →
    `McpToolResults.SolutionNotLoaded()`; sonst Aufruf der reinen,
    unten beschriebenen Formatierungslogik, Ergebnis über
    `McpToolResults.Text(...)`.
  - `internal static async Task<string> FindMatchesAsync(Solution solution, string namePattern, string? kind, CancellationToken ct)`
    — bewusst von `state`/`CallToolResult` entkoppelte, reine Funktion
    (Solution rein, formatierter String raus) für einfache Unit-Tests
    ohne `McpCodeGraphServer`/MCP-Protokoll:
    1. `SymbolFinder.FindSourceDeclarationsAsync(solution, name => name.Contains(namePattern, StringComparison.OrdinalIgnoreCase), SymbolFilter.TypeAndMember, ct)`.
    2. Optionaler Kind-Filter (`kind` case-insensitive, Werte
       `"class"`/`"interface"`/`"method"`/`"property"` — deutsche
       Konzept-Begriffe "Klasse/Methode/Property/Interface" 1:1 auf
       englische, MCP-typische Parameter-Werte gemappt, siehe "Notes"):
       `"class"` → `ITypeSymbol` mit `TypeKind.Class`, `"interface"` →
       `TypeKind.Interface`, `"method"` → `SymbolKind.Method`,
       `"property"` → `SymbolKind.Property`. Kein `kind` → keine
       Einschränkung.
    3. Kein Treffer nach Filterung → `"Keine Treffer für '{namePattern}'" + (kind != null ? $" (Kind-Filter: {kind})" : "")`
       (schlichter Text, **kein** Miss-Hint-Fallback über andere
       Dateitypen — das ist EPIC-05, siehe "Bezug").
    4. Treffer vorhanden → eine Zeile pro Symbol-`Location`
       (`symbol.Locations.Where(l => l.IsInSource)`), Format analog
       `DiffImpactAnalyzer.FindCallSitesAsync`:
       `{relativePath}:{line} - {Kind}: {symbol.ToDisplayString()}`,
       `relativePath` über `PathNormalizer.ToRelative(Path.GetDirectoryName(solution.FilePath) ?? "", location.SourceTree!.FilePath)`,
       `{Kind}` als deutsches Wort (`Klasse`/`Interface`/`Methode`/
       `Property`/Fallback `symbol.Kind.ToString()` für alles andere,
       z. B. `Field`), mit Zeilenumbruch (`\n`) zwischen den Zeilen.
- **Warum:** `konzept.md` Tabellenzeile `find_symbol` (Input, Output,
  Basis `SymbolFinder.FindDeclarationsAsync` — hier bewusst durch das
  passendere `FindSourceDeclarationsAsync` mit Prädikat ersetzt, siehe
  "Aktueller Projektzustand"). Trennung `ExecuteAsync`/`FindMatchesAsync`
  folgt derselben Testbarkeits-Überlegung wie
  `ResolveSolutionPathOrError`/`RunAsync` in step-001 (reine Logik ohne
  Protokoll-/State-Abhängigkeit separat testbar).

### Datei 4: `src/AiNetLinter/Commands/McpServerCommand.cs`

- **Was:**
  - `CreateServerOptions()` bekommt einen neuen Parameter
    `McpCodeGraphServer mcpState` (Signatur:
    `private static McpServerOptions CreateServerOptions(McpCodeGraphServer mcpState)`),
    Aufrufstelle in `RunAsync` entsprechend anpassen
    (`var serverOptions = CreateServerOptions(mcpState);`, nach der
    `using var mcpState = ...`-Zeile, wie bisher).
  - Neue private Methode `BuildToolCollection(McpCodeGraphServer mcpState)`,
    die einen `McpServerPrimitiveCollection<McpServerTool>` mit genau
    einem Eintrag zurückgibt: `find_symbol`, registriert über
    `McpServerTool.Create(delegate, options)` — Delegate ruft
    `FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, ct)`
    (Parameter des Delegates: `string namePattern`, `string? kind`,
    `CancellationToken ct` — Coder verifiziert, welche Parametertypen/
    -reihenfolge das SDK für automatische Schema-Generierung erwartet,
    siehe "Aktueller Projektzustand"), `McpServerToolCreateOptions.Name = "find_symbol"`,
    `.Description` benennt knapp Zweck **und** die C#-only-Grenze (z. B.
    "Sucht C#-Symbole (Klassen, Methoden, Properties, Interfaces) per
    Substring im Namen. Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/
    .html/.css-Dateien." — vollständige, zentrale Scope-Kommunikation
    inkl. `initialize`-`instructions`-Feld bleibt EPIC-05).
  - `CreateServerOptions` befüllt `ToolCollection` jetzt mit
    `BuildToolCollection(mcpState)` statt der bisherigen leeren
    `new McpServerPrimitiveCollection<McpServerTool>()`.
- **Warum:** Verdrahtung des neuen Tools mit dem resident gehaltenen
  Server-Zustand aus step-002, ohne DI-Container (Closure-Capture von
  `mcpState`, konsistent mit `konzept.md` "Zielplattformen").

### Datei 5: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:**
  - `RunAsync_ValidFixture_ServerRespondsWithEmptyToolList` umbenennen zu
    `RunAsync_ValidFixture_ServerRespondsWithFindSymbolTool` und
    `Assert.Empty(tools)` ersetzen durch `Assert.Single(tools)` +
    Assertion, dass `tools[0].Name == "find_symbol"`.
  - Neuer Test `RunAsync_ValidFixture_FindSymbolReturnsMatch`: gleicher
    Subprozess-/`StdioClientTransport`-Aufbau wie der bestehende Test,
    aber `client.CallToolAsync("find_symbol", new Dictionary<string, object?> { ["namePattern"] = "Violating" }, cancellationToken: cts.Token)`
    (Parameter-Dictionary-Form je nach tatsächlicher SDK-Client-API,
    Coder verifiziert), Assertion: Ergebnis enthält `"ViolatingClass"`
    und `IsError` ist nicht `true`.
- **Warum:** Bestehender Test muss der neuen, nicht mehr leeren
  Tool-Liste angepasst werden (sonst rot); neuer Test verifiziert
  `find_symbol` end-to-end über den echten MCP-Client/Subprozess-Pfad,
  nicht nur In-Process.

### Datei 6: `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` (neu)

- **Was:** Unit-Tests gegen `FindSymbolTool.FindMatchesAsync` (reine
  Funktion, siehe Datei 3) mit einer über `SourceFileCatalog.LoadAsync`
  geladenen `BaselineMini`-Solution (Vorbild: bestehende Tests, die
  `BaselineMiniFixtureWorkspace`/`SourceFileCatalog.LoadAsync` nutzen,
  z. B. `McpCodeGraphServerTests.cs`):
  - `FindMatchesAsync_SubstringMatch_ReturnsFileLineAndKind`: Suche nach
    `"Violating"` findet `ViolatingClass`, Ergebnis-String enthält
    Dateiname, Zeilennummer, `"Klasse"`.
  - `FindMatchesAsync_KindFilterExcludesNonMatchingKind`: Suche nach
    `"Violating"` mit `kind: "method"` liefert **keinen** Treffer (die
    Fixture-Klasse selbst ist keine Methode), Ergebnis ist der
    "Keine Treffer"-Text.
  - `FindMatchesAsync_NoMatch_ReturnsNoResultsText`: Suche nach
    `"DoesNotExistXyz"` liefert den "Keine Treffer für..."-Text, kein
    leerer String, keine Exception.
  - `FindMatchesAsync_CaseInsensitive_MatchesRegardlessOfCase`: Suche
    nach `"violating"` (Kleinschreibung) findet trotzdem
    `ViolatingClass`.
  - Testklasse mit `[Collection("ConsoleTestCollection")]` annotieren
    (siehe "Aktueller Projektzustand"/TD-003-Hinweis).
- **Warum:** Die reine Formatierungs-/Filterlogik direkt testen, ohne
  jedes Mal einen echten MCP-Subprozess zu starten (das bleibt den
  wenigen E2E-Tests in `McpServerCommandTests.cs` vorbehalten, Vorbild
  TD-002-Beobachtung aus step-001: Subprozess-Tests sind teuer,
  sparsam einsetzen).

## Tests

- [ ] `FindSymbolToolTests.cs` (Datei 6) — 4 Testfälle wie oben
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithFindSymbolTool` (umbenannt, Datei 5)
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_FindSymbolReturnsMatch` (neu, Datei 5)
- [ ] Bestehende Tests bleiben grün (`dotnet test AiNetLinter.slnx`),
      insbesondere `McpCodeGraphServerTests.cs` (unverändert, aber
      indirekt betroffen durch `CreateServerOptions`-Signaturänderung —
      sollte keine Auswirkung haben, da diese Tests nicht über
      `McpServerCommand.RunAsync` laufen, kurz verifizieren)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (Dateien 1-6)
- [ ] `dotnet build AiNetLinter.slnx` grün, keine neuen Warnungen
      (`TreatWarningsAsErrors`)
- [ ] `dotnet test AiNetLinter.slnx` grün (neue + bestehende Tests) —
      bei einem `RegisterMSBuild`-Flake (TD-003) einmal wiederholen und
      das Ergebnis in `step-result.md` vermerken (siehe step-002-Vorbild)
- [ ] Manuelle Verifikation optional: `ainetlinter --mcp-server --path tests/Fixtures/BaselineMini` startet weiterhin fehlerfrei
- [ ] Commit auf aktuellem Branch (Conventional Commit, Englisch, Suffix
      `[codegraph-mcp]`, siehe Tech-Stack-Notiz in `roadmap.md`)
- [ ] `step-003/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt
- [ ] `### Commit-Vorschlag`-Abschnitt am Ende der Coder-Antwort
      (`AiNetLinterRichtlinien.mdc` §4)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §1/§2 — kein DI-Container,
  kein Plugin-System: direkt maßgeblich für die Closure-basierte
  Tool-Registrierung (`mcpState` per Delegate-Capture statt
  `IServiceProvider`), fortgeführt aus step-001s Entscheidung. §4
  Commit-Vorschlag-Pflicht. §5 Zero-Warning-Direktive.
- `.agents/rules/AiNetLinter.mdc` — `#nullable enable` in allen neuen
  Dateien, `sealed` wo zutreffend (`internal static class` für
  `McpToolResults`/`FindSymbolTool` — `sealed` gilt nicht für statische
  Klassen, wie bereits in step-001 vermerkt), Methoden ≤60 Zeilen
  (`FindMatchesAsync` ggf. in eine private Hilfsmethode für die
  Kind-Filterung aufteilen, falls sie sonst zu lang wird), max. 4
  Parameter/max. 1 `bool`-Parameter (`FindSymbolTool.ExecuteAsync`/
  `FindMatchesAsync` haben je 4 Parameter — an der Grenze, nicht
  überschreiten), kein leeres `catch`.

## Bekannte Ausnahmen

- **Glob-Pattern-Matching** (`konzept.md` Tabellenzeile nennt
  "Substring/Glob") ist in diesem Step bewusst **nicht** enthalten —
  nur Substring (case-insensitive `Contains`). Begründung: Glob bräuchte
  eine eigene kleine Pattern-Engine oder eine Zusatzabhängigkeit: der
  fachliche Mehrwert ("wo ist X") ist mit Substring bereits weitgehend
  abgedeckt, Glob kann als kleine Ergänzung in einem der folgenden
  EPIC-03-Steps nachgezogen werden, falls sich in der Praxis (EPIC-09,
  `San.smart.Planner.Platform`) ein echter Bedarf zeigt. Kein
  Konzept-Verstoß, da `konzept.md` "Offene Punkte" die exakte
  Parametrisierung explizit dem Planer im drift-loop überlässt.
- **Kein Miss-Hint-Fallback bei keinem Treffer** — explizit EPIC-05
  (siehe "Bezug").

## Notes

- **Parameter-Naming Deutsch vs. Englisch:** `konzept.md` beschreibt
  Muss-Haben und Tool-Tabelle auf Deutsch, aber MCP-Tools/Parameter sind
  laut Konvention (siehe Tool-Namen selbst: `find_symbol`, `get_impact`,
  ...) englisch benannt (`namePattern`, `kind`) — die deutschen
  Kind-Begriffe aus der Konzept-Tabelle ("Klasse/Methode/Property/
  Interface") werden als **Werte** des `kind`-Parameters bewusst NICHT
  übernommen, stattdessen englische, MCP-typische Werte
  (`"class"`/`"method"`/`"property"`/`"interface"`), da ein
  englischsprachiger Agent (Claude Code) die Tools bedient. Nur die
  **Ausgabe** (Kind-Label in der Ergebniszeile) bleibt Deutsch, konsistent
  mit dem Rest der AiNetLinter-Ausgaben (z. B. `DiffImpactAnalyzer`s
  "Aufruf von ..."-Format).
- **`FindSourceDeclarationsAsync` statt `FindDeclarationsAsync`:** die
  Konzept-Tabelle nennt `SymbolFinder.FindDeclarationsAsync` als Basis
  — das Projekt-Overlay zu `FindSourceDeclarationsAsync` mit
  Namens-Prädikat ist die passendere, Solution-weite API für
  Substring-Suche in einem Aufruf (kein manuelles Iterieren über
  `solution.Projects`). Konsistent mit dem Muss-Haben-Sinn
  ("Wiederverwendung statt Neubau" der `SymbolFinder`-Familie), keine
  Abweichung von der fachlichen Absicht.
- **`SymbolFilter.TypeAndMember`** deckt sowohl Typen (Klassen,
  Interfaces) als auch Member (Methoden, Properties) in einem Aufruf ab
  — kein zweiter Suchdurchlauf nötig, der nachträgliche Kind-Filter
  arbeitet auf dem bereits vollständigen Ergebnis.
- **`ProtocolTool.Name`** (bzw. der äquivalente Weg, den Tool-Namen aus
  einem `Tool`-Objekt in `client.ListToolsAsync()`-Ergebnissen zu lesen)
  — Coder verifiziert die exakte Property am `McpClientTool`/`Tool`-Typ
  der Client-Seite beim Schreiben des umbenannten Tests (Datei 5).
