---
status: done (pending audit)
type: step-plan
task: codegraph-mcp
step: 006
title: "get_file_skeleton Tool (Struktur-Skelett einer einzelnen Datei via SkeletonMapBuilder)"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T23:30:00Z
related_to: [step-004, step-005]
---

# Step 006: get_file_skeleton Tool (Struktur-Skelett einer einzelnen Datei via SkeletonMapBuilder)

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-03` aus `roadmap.md` — Symbolgraph-Tools. step-003
  (Tool-Infrastruktur + `find_symbol`), step-004 (`find_references`) und
  step-005 inkl. `fix-01` (`get_impact`, plus Behebung des
  `RunGitDiff`-Subprozess-Hangs unter stdio-Transport) sind `approved`.
  Offen: `get_type_hierarchy`, `get_file_skeleton`. Dieser Step liefert
  `get_file_skeleton` — bewusst vor `get_type_hierarchy`, siehe
  "Reihenfolge-Begründung" unten.
- **Konzept-Referenz:** `konzept.md` Tool-Tabelle unter "Wie" — Zeile
  `get_file_skeleton` | Input `Dateipfad (relativ)` | Output
  `Struktur-Skelett dieser einen Datei (Signaturen ohne Bodies)` | Basis
  `SkeletonMapBuilder`, granularer statt Whole-Repo (`--map skeleton`).
  Ebenfalls relevant: Muss-Haben "Wiederverwendung statt Neubau" sowie die
  seit 2026-07-31 verbindliche Dogfooding-Pflicht pro EPIC-03-Tool-Step
  (siehe `roadmap.md` EPIC-03-Zeile).

## Reihenfolge-Begründung (`get_file_skeleton` vor `get_type_hierarchy`)

Beide verbleibenden EPIC-03-Tools sind unabhängig voneinander umsetzbar,
daher reine Risiko-/Wiederverwendungs-Abwägung:

- `get_file_skeleton` hat mit `SkeletonMapBuilder`/`SkeletonSyntaxWalker`/
  `SkeletonMarkdownRenderer` (`src/AiNetLinter/Maps/Skeleton/*.cs`,
  vollständig gelesen) eine bereits granulare, pro-Dokument arbeitende
  Basis (`ExtractFromDocumentAsync(Document, ...)` extrahiert schon heute
  pro Datei, nicht erst pro Solution) — die nötige Änderung ist eine reine
  Sichtbarkeits-Anhebung plus dünner Dispatch, kein neuer Roslyn-API-Pfad.
- `get_type_hierarchy` bräuchte dagegen die laut `konzept.md` "neu
  einzubindende" `SymbolFinder.FindDerivedClassesAsync`/
  `FindImplementationsAsync`-API — im gesamten Repo aktuell **nirgends**
  verwendet (verifiziert, siehe unten), also unbekanntes Verhalten bei
  Interfaces vs. abstrakten Klassen, Mehrfach-Implementierung,
  Cross-Projekt-Vererbung — höheres Rest-Risiko für einen einzelnen Step.
- Nach dem in step-005 behobenen `RunGitDiff`-Subprozess-Hang ist es
  sinnvoll, den nächsten Step bewusst risikoärmer zu halten (kein neuer
  Subprozess-/Threading-Pfad, rein synchrone Roslyn-Symbolauflösung wie
  bei `find_symbol`/`find_references`).

`get_type_hierarchy` bleibt damit offen für den nächsten EPIC-03-Step
(JIT-Prinzip — kein Vorausplanen, siehe "Notes").

## Aktueller Projektzustand (JIT-Kontext)

- **`src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs`** (vollständig
  gelesen): `BuildAsync` ist der bestehende CLI-Einstiegspunkt
  (`--map skeleton`, Whole-Solution). Die private Methode
  `ExtractFromDocumentAsync(Document document, string solutionDir,
  LinterArgs args, CancellationToken ct)` (Zeile 73-92) extrahiert bereits
  **pro einzelnem Dokument** eine `IReadOnlyList<SkeletonTypeInfo>` über
  `SkeletonSyntaxWalker` — exakt die Granularität, die `get_file_skeleton`
  laut Konzept-Tabelle braucht ("Struktur-Skelett dieser **einen** Datei").
  Wird in diesem Step auf `internal static` angehoben und direkt vom neuen
  Tool aufgerufen — **kein Nachbau** der Walker-/Extraktionslogik, nur
  eine Sichtbarkeitsänderung (analog zu `FindCallSitesAsync`/
  `FindDocumentByPath` in step-004).
- **`src/AiNetLinter/Maps/Skeleton/SkeletonMarkdownRenderer.cs`**
  (vollständig gelesen): `Render(IReadOnlyList<SkeletonTypeInfo> types,
  string solutionPath, DateTimeOffset generatedAt)` ist bereits eine reine
  Formatierungsfunktion unabhängig von "Whole-Solution vs. eine Datei" —
  eine Liste mit den Typen **einer** Datei rendert korrekt (Header zeigt
  dann automatisch "Typen: 1"/"Typen: 2" etc. für diese Datei statt für
  die ganze Solution). Wird unverändert wiederverwendet, `solutionPath`-
  Parameter wird mit dem übergebenen relativen Dateipfad befüllt (bessere
  Aussagekraft als der Solution-Pfad für ein Einzeldatei-Tool).
- **`src/AiNetLinter/Core/DiffImpactAnalyzer.cs`**
  `FindDocumentByPath(Solution, string filePath)` (bereits `internal
  static` seit step-004) wird wiederverwendet, um den relativen
  Dateipfad-Parameter zu einem `Document` aufzulösen — exakt dieselbe
  Methode, die `find_references`/`get_impact` für die
  Positions-Auflösung nutzen, kein zweiter Nachbau derselben Suche.
- **`src/AiNetLinter/Cli/LinterArgs.cs`** (Properties `IncludeNamespaces`,
  `ExcludeNamespaces`, `PublicOnly` gelesen): `ExtractFromDocumentAsync`
  erwartet ein `LinterArgs`-Objekt für diese drei Filter. Für ein
  Einzeldatei-Tool ohne Namespace-Filter-Bedarf reicht ein Default-Objekt
  (`new LinterArgs { TargetPath = "", Verbose = false }` — Muster bereits
  in `SourceFileCatalogTests.cs` etabliert, `IncludeNamespaces`/
  `ExcludeNamespaces` defaulten dort auf `[]`, `PublicOnly` auf `false`).
  Kein neuer Parameter im MCP-Tool für Namespace-Filter — außerhalb des
  Konzept-Tabellen-Inputs ("Dateipfad (relativ)", kein weiterer Parameter
  vorgesehen).
- **`src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs`/`FindReferencesTool.cs`/
  `GetImpactTool.cs`** (Struktur-Vorbild): `ExecuteAsync(McpCodeGraphServer
  state, ..., CancellationToken ct)` prüft zuerst `state.GetCurrentSolution()`,
  delegiert danach an vorhandene Kernlogik. Gleiches Muster für
  `GetFileSkeletonTool`.
- **`src/AiNetLinter/Mcp/McpToolResults.cs`** (vollständig gelesen):
  bietet bereits `SolutionNotLoaded()`, `Text(string)`. Kein bestehender
  Helper für "Datei nicht gefunden" — wird ergänzt, siehe Datei 2 unten.
  Bewusst **kein** neuer Eintrag in `LinterErrorCodes` nötig:
  `LinterErrorCodes.ResourceNotFound` ("RESOURCE_NOT_FOUND") existiert
  bereits und wird im Repo mehrfach exakt für "Pfad/Datei nicht gefunden"
  verwendet (`McpServerCommand.cs`, `PlaybookCheckCommand.cs`,
  `SyncAgentRulesCommand.cs`) — Wiederverwendung statt eines vierten,
  bedeutungsgleichen Fehlercodes.
- **`SymbolFinder.FindDerivedClassesAsync`/`FindImplementationsAsync`**
  (für `get_type_hierarchy`, nicht Teil dieses Steps): repo-weit per Grep
  geprüft — **keine** bestehende Verwendung, exakt wie `konzept.md`
  ("neu einzubinden") vorhersagt. Bestätigt die Reihenfolge-Begründung
  oben, kein Handlungsbedarf in diesem Step.
- **Test-Fixture-Lage:** `tests/Fixtures/SymbolGraphMini/` (aus step-004,
  bereits erweitert um `OtherCaller.cs` für den Ambiguitäts-Testfall in
  `find_references`) eignet sich direkt weiter — `Greeter.cs` liefert
  eine Klasse mit einer öffentlichen Methode, ideal für einen
  Skeleton-Dump-Test. **Keine neue Fixture nötig** — `filePath`-Parameter
  wird relativ zum Solution-Verzeichnis übergeben (z. B.
  `src/SymbolGraphMini/Greeter.cs`, identisch zu
  `SymbolGraphMiniFixtureWorkspace.GreeterPath`, nur relativ statt
  absolut). `SymbolGraphMiniFixtureWorkspace` (Properties `GreeterPath`/
  `CallerPath`) wird direkt wiederverwendet, keine neue Fixture-Workspace-
  Klasse.
- **`AIContextFootprint`-Risiko geprüft (TD-004/TD-005, Index gelesen,
  volle Einträge nachgeschlagen wegen direkter Themen-Überschneidung):**
  `McpServerOptionsFactory` lag nach step-005 bei 2469/2500 (nur noch 31
  Zeilen Puffer) — ein vierter `tools.Add(...)`-Eintrag riskiert das
  Reißen, wie in TD-004s letztem Update bereits vorhergesagt.
  Zusätzlich neu (nicht in TD-005 antizipiert): `GetFileSkeletonTool`
  selbst zieht über `ExtractFromDocumentAsync`s Parameter/Rückgabetyp
  erstmals `LinterArgs` (eigener Typ, ~30 Properties, aber überwiegend
  `string`/`bool`/`IReadOnlyList<string>` — `AIContextFootprintCalculator`
  zählt laut eigenem Code nur Typen mit `DeclaringSyntaxReferences` und
  traversiert nur Feld-/Property-/Methoden-Signaturen, keine
  Methodenkörper; Roslyn-eigene Typen wie `Document`/`Solution` ohne
  eigene `DeclaringSyntaxReferences` fallen komplett raus) sowie
  `SkeletonTypeInfo`/`SkeletonMemberInfo`/`MemberKind` (klein, siehe
  Datei-Lektüre oben) in seinen eigenen Footprint. Beides zusätzlich zu
  `McpCodeGraphServer` (bereits laut TD-005 der Haupttreiber). Da weder
  `LinterArgs` noch die Skeleton-Typen bisher in einem `Mcp/Tools/*.cs`
  referenziert wurden, ist unklar, wie stark sich das auswirkt — **Pflicht-
  Verifikation per Selbst-Lint in der Definition of Done**, mit
  dokumentierter Ausweich-Option (siehe dort), falls eines der beiden
  Limits reißt.

## Intention

`get_file_skeleton` liefert für eine einzelne, per relativem Dateipfad
benannte `.cs`-Datei ein Struktur-Skelett (Typen, Modifier, Basistypen,
Member-Signaturen ohne Bodies, gruppiert wie im bestehenden
`--map skeleton`-Markdown) — ohne dass ein Agent dafür einen
Whole-Repo-Dump anfordern und selbst nach der relevanten Datei filtern
muss. Reiner dünner Dispatch über bereits granular pro Datei arbeitenden
Code (`SkeletonMapBuilder.ExtractFromDocumentAsync` +
`SkeletonMarkdownRenderer.Render`) — keine neue Extraktions- oder
Rendering-Logik.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs` (Zeile 73-92)

- **Was:** Sichtbarkeit von `ExtractFromDocumentAsync` von `private
  static` auf `internal static` anheben. Xml-Doc-Kommentar ergänzen
  ("wird auch von `GetFileSkeletonTool` (MCP) für die Einzeldatei-
  Extraktion wiederverwendet"). Keine Logikänderung.
- **Warum:** Wiederverwendung statt Neubau — exakt die im Konzept
  genannte granulare Basis für `get_file_skeleton`.

### Datei 2: `src/AiNetLinter/Mcp/McpToolResults.cs`

- **Was:** Neue statische Hilfsmethode `FileNotFound(string relativePath)`
  → `Error(LinterErrorCodes.ResourceNotFound, $"Datei '{relativePath}'
  nicht in der Solution gefunden.", context: relativePath, hint: "Pfad
  relativ zum Solution-Verzeichnis angeben (Forward- oder Backslash),
  'find_symbol' zur Orientierung nutzen.")`.
- **Warum:** Gleiches Wiederverwendungs-Muster wie `SolutionNotLoaded()`/
  `SymbolNotFound(...)` — zentraler, wiederverwendbarer Fehler-Baustein
  statt Ad-hoc-`Error(...)`-Aufruf direkt im Tool. Nutzt den bestehenden
  `ResourceNotFound`-Code statt eines neuen (siehe "Aktueller
  Projektzustand").

### Datei 3: `src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs` (neu)

- **Was:** Neue statische Klasse `GetFileSkeletonTool`, siehe Code-Skizze
  unten:
  - `ExecuteAsync(McpCodeGraphServer state, string filePath,
    CancellationToken ct)` — Solution-Check
    (`McpToolResults.SolutionNotLoaded()` bei `null`), löst `filePath`
    (relativ oder absolut) über `Path.GetFullPath(Path.Combine(solutionDir,
    filePath))` + `DiffImpactAnalyzer.FindDocumentByPath` zu einem
    `Document` auf (`null` → `McpToolResults.FileNotFound(filePath)`),
    ruft dann `SkeletonMapBuilder.ExtractFromDocumentAsync` mit einem
    Default-`LinterArgs` auf. Leere Typenliste → Text
    "Keine Typen gefunden in '{filePath}'" (konsistent mit
    `find_symbol`s "Keine Treffer"-Fallback). Sonst
    `SkeletonMarkdownRenderer.Render(types, filePath, DateTimeOffset.Now)`
    als Text zurückgeben.
- **Warum:** Kernstück dieses Steps — dünner Dispatch, keine eigene
  Extraktions-/Rendering-Logik dupliziert.

### Datei 4: `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (Zeile 65-76)

- **Was:** Vierten `tools.Add(McpServerTool.Create(...))`-Aufruf
  ergänzen:
  ```csharp
  tools.Add(McpServerTool.Create(
      (string filePath, CancellationToken ct = default) =>
          GetFileSkeletonTool.ExecuteAsync(mcpState, filePath, ct),
      new McpServerToolCreateOptions
      {
          Name = "get_file_skeleton",
          Description = "Liefert das Struktur-Skelett (Typen, Signaturen ohne " +
              "Bodies) einer einzelnen C#-Datei per relativem Dateipfad. " +
              "Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien.",
      }));
  ```
- **Warum:** Registrierung über den bestehenden, etablierten Sammelpunkt.
  **Falls Selbst-Lint (siehe DoD) hier das `AIContextFootprint`-Limit
  reißen sieht:** dokumentierte Ausweich-Option (kein Vorgriff, nur
  Notfallplan) — `BuildToolCollection` in zwei private Methoden
  aufteilen, z. B. `RegisterSymbolTools(tools, mcpState)`
  (find_symbol/find_references/get_impact) und
  `RegisterStructureTools(tools, mcpState)` (get_file_skeleton, künftige
  EPIC-03/04-Tools) — beide weiterhin in derselben Datei, keine neue
  Registrierungs-Datei pro Tool. Diese Aufteilung reduziert
  `BuildToolCollection`s eigene Methode nicht im Footprint (Closures
  verschwinden nicht), verkleinert aber den **Umfang pro Methode**, was
  laut `MaxMethodLineCount`/`MaxCyclomaticComplexity` ohnehin sinnvoll
  wäre, sobald 4 Tools in einer Methode stehen. Falls das
  `AIContextFootprint`-Limit dadurch nicht sinkt (wahrscheinlich, da die
  Metrik pro Typ, nicht pro Methode zählt), zusätzliche Ausweich-Stufe:
  `ignoreTypeNames`/`ignoreNamespacePrefixes` in einer eigenen
  `AIContextFootprint`-Rule-Override für `McpServerOptionsFactory` in
  `rules.json` prüfen (nur falls die Struktur-Aufteilung allein nicht
  reicht — kein Vorgriff, nur letzte dokumentierte Stufe).

### Datei 5: `src/AiNetLinter.Tests/Mcp/Tools/GetFileSkeletonToolTests.cs` (neu)

- **Was:** Unit-Tests gegen `GetFileSkeletonTool.ExecuteAsync`, siehe
  "Tests" unten.
- **Warum:** analog zu `FindSymbolToolTests.cs`/`FindReferencesToolTests.cs`
  — reine In-Process-Tests, kein Subprozess nötig für die meisten Fälle.

### Datei 6: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:** Tool-Zähl-Test (aktuell `RunAsync_ValidFixture_ServerRespondsWithThreeTools`)
  umbenennen zu `RunAsync_ValidFixture_ServerRespondsWithFourTools`,
  Assertion auf vier Tools inkl. `get_file_skeleton` erweitern.
  Zusätzlich neuer E2E-Subprozess-Test
  `RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature` (gegen
  `SymbolGraphMiniFixtureWorkspace`, ruft `get_file_skeleton` mit
  relativem Pfad zu `Greeter.cs` auf, prüft `IsError != true` und dass
  der Text die `Greet`-Methodensignatur enthält) — analog zu den beiden
  neuen Subprozess-Tests aus `step-005/fix-01`.
- **Warum:** Der bestehende Tool-Zähl-Test würde sonst fehlschlagen
  (echter Regressions-Fund). Der neue E2E-Test deckt den tatsächlichen
  Produktions-Aufrufkontext ab (wichtig nach dem in step-005/fix-01
  behobenen Hang-Fund — auch wenn `get_file_skeleton` selbst keinen
  Subprozess startet, bestätigt der Test den echten stdio-Pfad
  unabhängig von den In-Process-Tests in Datei 5).

## Tests

- [ ] `GetFileSkeletonToolTests.ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
      — `new McpCodeGraphServer(null)` → `ExecuteAsync` → `IsError == true`,
      Text enthält `SOLUTION_NOT_LOADED`.
- [ ] `GetFileSkeletonToolTests.ExecuteAsync_UnknownFilePath_ReturnsResourceNotFoundError`
      — Pfad `"src/SymbolGraphMini/DoesNotExist.cs"` → `Error` mit
      `RESOURCE_NOT_FOUND`.
- [ ] `GetFileSkeletonToolTests.ExecuteAsync_ValidRelativePath_ReturnsGreeterSkeletonWithGreetMethod`
      — gegen `SymbolGraphMiniFixtureWorkspace`, relativer Pfad zu
      `Greeter.cs` (`"src/SymbolGraphMini/Greeter.cs"`) → Text enthält
      `Greet` und `class Greeter` (bzw. die entsprechende
      Skeleton-Markdown-Überschrift), enthält **nicht** `Caller`/
      `OtherCaller` (Beweis, dass wirklich nur diese eine Datei
      extrahiert wird, nicht die ganze Solution).
- [ ] `GetFileSkeletonToolTests.ExecuteAsync_AbsolutePath_ResolvesSameAsRelativePath`
      — derselbe Aufruf mit dem absoluten `GreeterPath` statt relativ →
      identisches Ergebnis (belegt, dass `Path.Combine` mit einem
      bereits absoluten zweiten Argument den ersten ignoriert, wie
      dokumentiert).
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithFourTools`
      (umbenannt/angepasst, siehe Datei 6 oben).
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature`
      (neu, siehe Datei 6 oben).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" (Datei 1-6) umgesetzt
- [ ] `dotnet build AiNetLinter.slnx` grün, 0 Warnungen
- [ ] `dotnet test AiNetLinter.slnx` grün (alle Tests, inkl. neue)
- [ ] Selbst-Lint (`ainetlinter --config rules.json --path ./src/`) `OK`,
      0 Violations — **explizit auch auf `AIContextFootprint` für
      `McpServerOptionsFactory`/`GetFileSkeletonTool` prüfen** (siehe
      TD-004/TD-005-Abschnitt oben; Ausweich-Stufen unter Datei 4
      dokumentiert, falls eines der beiden Limits reißt)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Englisch,
      `[codegraph-mcp]`-Suffix im Subject, siehe Tech-Stack-Notiz in
      `roadmap.md`)
- [ ] **Dogfooding (Muss-Haben, `konzept.md`):** gebautes
      `AiNetLinter.exe` per `StdioClientTransport` als
      `--mcp-server --path <echtes-Repo-Root>` gegen die reale
      `AiNetLinter.slnx` starten, `get_file_skeleton` mindestens einmal
      mit einem echten Repo-Dateipfad aufrufen (z. B.
      `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs`), Ergebnis auf
      Plausibilität prüfen (enthält erwartete Methoden-/Klassennamen,
      kein Hang/Timeout wie in step-005 vor `fix-01`). In
      `step-result.md` unter eigenem Abschnitt „Dogfooding" dokumentieren
      — Vorbild: `step-005/step-result.md`/`step-005/fix-01/step-result.md`.
- [ ] `step-006/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt
- [ ] `### Commit-Vorschlag`-Abschnitt am Ende der Coder-Antwort
      (`AiNetLinterRichtlinien.mdc` §4)

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — `#nullable enable`, `sealed`
  (Ausnahme: statische Klassen exemptiert, wie bei `FindSymbolTool`/
  `GetImpactTool`/`McpServerOptionsFactory`), Methodenlänge (≤60 Zeilen),
  max. 4 Parameter, `AIContextFootprint` (2500) — siehe TD-004/TD-005-
  Abwägung oben, Pflicht-Verifikation per Selbst-Lint, dokumentierte
  Ausweich-Stufen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1/§2 — kein DI-Container,
  kein Plugin-System: neues Tool wird wie die drei bestehenden per
  Delegate-Closure registriert, keine neue Abstraktionsebene.

## Bekannte Ausnahmen

- Der Fall "Datei existiert, enthält aber keine Typen" (z. B. eine
  Datei nur mit `using`-Direktiven) wird **nicht** durch einen eigenen
  Fixture-Testfall abgedeckt — der Code-Pfad (`types.Count == 0` →
  Text-Fallback) ist trivial und identisch zum bereits getesteten Muster
  in `FindSymbolTool.FindMatchesAsync` ("Keine Treffer"). Keine neue
  Fixture-Datei nur für diesen Nebenfall, um `SymbolGraphMini` nicht
  unnötig weiter aufzublähen (das Fixture dient mehreren Tools/Tests,
  siehe step-004).
- Wie bei `find_symbol`/`find_references`: kein `search_pattern`-Fallback
  für nicht-C#-Dateien in diesem Step — Miss-Hint-Mechanismus ist laut
  `roadmap.md` EPIC-05, nicht EPIC-03 einzeln.

## Code-Skizze (optional)

```csharp
// src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs
internal static class GetFileSkeletonTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string filePath, CancellationToken ct)
    {
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var absolutePath = Path.GetFullPath(Path.Combine(solutionDir, filePath));
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, absolutePath);
        if (document is null) return McpToolResults.FileNotFound(filePath);

        var args = new LinterArgs { TargetPath = "", Verbose = false };
        var types = await SkeletonMapBuilder.ExtractFromDocumentAsync(document, solutionDir, args, ct);
        if (types.Count == 0)
        {
            return McpToolResults.Text($"Keine Typen gefunden in '{filePath}'");
        }

        var markdown = SkeletonMarkdownRenderer.Render(types, filePath, DateTimeOffset.Now);
        return McpToolResults.Text(markdown);
    }
}
```

## Notes

- **`get_type_hierarchy` (letztes offenes EPIC-03-Tool) bewusst nicht Teil
  dieses Steps** (JIT-Prinzip, siehe "Reihenfolge-Begründung" oben) — ein
  Folge-Step muss `SymbolFinder.FindDerivedClassesAsync`/
  `FindImplementationsAsync` neu einbinden und sollte dabei prüfen, ob
  `FindReferencesTool.ResolveSymbolAsync` (Identifikator → `ISymbol`)
  wiederverwendbar ist, statt eine dritte Kopie der Identifikator-
  Auflösung zu bauen (analog zur Notiz in `step-004/step-plan.md`, die
  für `get_impact` bereits denselben Hinweis gab und dort auch befolgt
  wurde).
- **TD-004/TD-005 im Blick behalten:** Dies ist voraussichtlich der Step,
  in dem eines der beiden Footprint-Limits erstmals seit step-004
  tatsächlich reißen könnte (kumulativer Effekt aus drei bestehenden plus
  einem neuen Tool, plus neu hinzukommendem `LinterArgs`-Typ). Falls das
  eintritt: die in Datei 4 dokumentierten Ausweich-Stufen nutzen, **nicht**
  eigenmächtig eine größere Registrierungs-Architektur umbauen (das wäre
  Scope-Ausweitung über diesen Step hinaus — bei Bedarf stattdessen einen
  Tech-Debt-Kommentar durch den Kritiker ergänzen lassen, falls die
  dokumentierten Ausweich-Stufen nicht ausreichen).
