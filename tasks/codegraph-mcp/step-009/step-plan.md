---
status: done
type: step-plan
task: codegraph-mcp
step: 009
title: "get_hotspots Tool (Zeilen-Hotspot-Kennzahlen der Solution)"
epic: EPIC-04
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T18:00:00Z
related_to: []
---

# Step 009: get_hotspots Tool (Zeilen-Hotspot-Kennzahlen der Solution)

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-04` aus `roadmap.md` — zweites von vier EPIC-04-Tools
  (nach `get_index_scope`, approved step-008). Noch offen danach:
  `get_violations`, `search_pattern`.
- **Konzept-Referenz:** `konzept.md` Tool-Tabelle Zeile `get_hotspots`
  ("Optionaler Namespace-/Projekt-Filter" → "Kopplungs-/Hotspot-Kennzahlen",
  Basis `HotspotMapBuilder`), Abschnitt "Wo im Projekt"
  (`Maps/HotspotMapBuilder.cs` explizit als Basis genannt), Muss-Haben
  "Dogfooding pro Tool-Step".

## Aktueller Projektzustand (JIT-Kontext)

- **`HotspotMapBuilder.Build(targetPath, maxLineCount, ILintConsole)`**
  (`src/AiNetLinter/Maps/HotspotMapBuilder.cs`) ist CLI-gebunden: liest
  Dateien direkt von der Platte über
  `StructureMapBuilder.CollectFileInfos(root)` (rekursiver
  `Directory.EnumerateFiles(root, "*.cs", AllDirectories)`, `obj`/`bin`
  ausgeschlossen), unabhängig von jeder `Solution`/`ILintConsole`-Instanz.
  **Kann nicht direkt wiederverwendet werden** — das MCP-Tool muss auf der
  residenten, staleness-geprüften `Solution` operieren (gleiche Begründung
  wie in step-008 für `.cs`-Zählung: der Dateibestand muss aus der
  tatsächlich geladenen Solution kommen, nicht aus einem zweiten,
  unabhängigen Filesystem-Scan, der z. B. Test-Fixtures unter
  `tests/Fixtures/*` fälschlich mitzählen würde, wenn diese im selben
  Verzeichnisbaum liegen).
- **Reguläres Grenzwert-Schema:** `WarnThreshold = 0.80`,
  `CriticalThreshold = 0.95` sind `private const double` in
  `HotspotMapBuilder` — nicht wiederverwendbar ohne die Klasse selbst als
  Abhängigkeit zu ziehen (was denselben `AIContextFootprint`-Umweg wie bei
  `.xaml`/`.html` in step-008 bedeuten würde, nur diesmal ohne echten
  Wiederverwendungs-Gewinn, da die Formatierungs-Methoden `private` sind).
  **Entscheidung:** analog zu TD-006 (dort für `WebFileCatalog`-Hilfsmethoden
  bereits akzeptiert) werden die zwei Schwellwert-Konstanten in der neuen
  Scanner-Klasse dupliziert statt `HotspotMapBuilder` als Abhängigkeit zu
  ziehen — bewusste, kleine Duplikation, kein neuer Tech-Debt-Eintrag nötig
  (zwei `double`-Konstanten, keine wartungsrelevante Logik).
- **`McpCodeGraphServer`** (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs`)
  hält aktuell **keinen** `MaxLineCount`-Wert — der Server lädt bislang gar
  keine `rules.json`/`Config`. Für `get_hotspots` wird aber ein
  Zeilen-Grenzwert gebraucht (Basis für "Hotspot" = Datei nähert sich
  ihrem Limit). **Entscheidung (JIT, bewusst minimal, kein Vorgriff auf
  `get_violations`):** `McpCodeGraphServer` bekommt einen neuen, rein
  additiven optionalen Konstruktor-Parameter `int maxLineCount = 700`
  (700 = aktueller `MetricsConfig.MaxLineCount`-Default) + öffentliche
  `MaxLineCount`-Property. **Kein** volles `Config`-Objekt wird geladen/
  gehalten — das wäre ein Vorgriff auf `get_violations` (das echte
  `RuleRegistry`/`Config`-Anbindung braucht) und würde den Scope dieses
  Steps unnötig vergrößern (JIT-Prinzip, kein Vorausplanen). Rein additiver
  Parameter am Ende der Signatur bricht **keinen** der zahlreichen
  bestehenden Call-Sites (`new McpCodeGraphServer(catalog)`,
  `new McpCodeGraphServer(null)`, `new McpCodeGraphServer(catalog, c)` —
  über 30 Fundstellen in `src/AiNetLinter.Tests/Mcp/**`), da alle
  bestehenden Aufrufe positional/mit Named-Args vor dem neuen Parameter
  enden.
- **`McpServerCommand.RunAsync`** lädt aktuell keine `rules.json` — `args`
  (Typ `LinterArgs`) trägt aber bereits `ConfigPath` (aus `--config`,
  identisch zum CLI-Batch-Modus). Wiederverwendbares Muster:
  `MapCommand.ResolveMaxLineCount(LinterArgs)`
  (`src/AiNetLinter/Commands/MapCommand.cs:55-62`) — lädt bei gesetztem
  `ConfigPath` per `ConfigLoader.TryLoadConfig(args.ConfigPath,
  isRequired: false)` die `rules.json`, sonst `MetricsConfig`-Default.
  Exakt dieselbe Logik wird für `McpServerCommand.RunAsync` gebraucht,
  bevor `McpCodeGraphServer` konstruiert wird — **kein neuer Mechanismus**,
  reine Übernahme des bestehenden Musters.
- **`FileStructureToolRegistrations`** (`src/AiNetLinter/Mcp/
  FileStructureToolRegistrations.cs`) liegt laut `tech-debt.md` TD-004/
  TD-005 (Update step-008) bei **2434/2500** `AIContextFootprint` — nur
  noch 66 Zeilen Puffer, historischer Trend ~11-15 Zeilen pro
  `tools.Add(...)`-Eintrag. `get_hotspots` sollte damit noch hineinpassen,
  aber ohne nennenswerten Puffer für die beiden danach folgenden
  EPIC-04-Tools (`get_violations`, `search_pattern`). Dieser Step **plant
  keine** vorsorgliche dritte Registrar-Klasse (das wäre spekulative
  Vorabarbeit ohne konkreten Anlass — Klasse ist noch unter dem Limit) —
  aber die DoD verlangt zwingend einen eigenen `--footprint
  FileStructureToolRegistrations`-Lauf nach der Registrierung; reißt das
  Limit tatsächlich (2500), ist die Ausweich-Option (dritte Registrar-
  Klasse, z. B. `AnalysisToolRegistrations` für
  `get_hotspots`/`get_violations`/`search_pattern`) **in diesem Step**
  umzusetzen, nicht erst reaktiv im nächsten.
- **Tool-Klassen-Muster (TD-005):** `GetIndexScopeTool`/
  `GetIndexScopeScanner` (step-008) sind das aktuelle Vorbild — dünner
  Dispatch (`GetHotspotsTool`, löst Solution auf, delegiert), separate
  Logik-Datei ohne `McpCodeGraphServer`-Abhängigkeit (`GetHotspotsScanner`,
  nimmt `Solution` + `int maxLineCount` + `string? scopeFilter` entgegen —
  direkt unit-testbar ohne MCP-Infrastruktur). TD-005s Update aus step-008
  gilt unverändert: „von Anfang an dünner Dispatch" reicht allein nicht,
  wenn die ausgelagerte Logik selbst schon > 60-80 Zeilen braucht — die
  Formatierung (zwei Tabellen-Sektionen analog `HotspotMapBuilder.
  AppendSection`) ist mit ~13 Zeilen pro Sektion überschaubar, sollte aber
  beim Bauen selbst per `--footprint GetHotspotsTool` geprüft werden (DoD).
- **Fixture:** `tests/Fixtures/SymbolGraphMini/` (4 kleine `.cs`-Dateien,
  je < 30 Zeilen) hat keine Datei, die real ein 700-Zeilen-Limit
  überschreitet. **Kein neuer, künstlich aufgeblähter Fixture-Code nötig**
  — der neue optionale `maxLineCount`-Konstruktor-Parameter erlaubt Tests,
  stattdessen ein künstlich kleines Limit (z. B. `maxLineCount: 5`) zu
  übergeben, um Warn-/Kritisch-Einstufung an den bestehenden kleinen
  Fixture-Dateien realistisch auszulösen, ohne die Fixture selbst zu
  vergrößern.

## Intention

`get_hotspots` liefert dieselbe Kennzahl wie der bestehende CLI-Map-Typ
`--map hotspots` (Dateien, die sich ihrem konfigurierten `MaxLineCount`-
Limit nähern oder es überschreiten), aber granular gegen die resident
gehaltene Solution statt eines Einmal-Filesystem-Scans, inkl. optionalem
Namespace-/Projekt-Filter (konzept.md-Tabelle) — Orientierungs-Tool für
einen Agenten, der proaktiv wissen will, welche Dateien vor einem geplanten
Edit bereits nah am Limit sind (Drift-Signal, bevor ein Verstoß entsteht).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (Zeile 21-39)

- **Was:** Konstruktor um optionalen dritten Parameter
  `int maxLineCount = 700` erweitern (700 = `new MetricsConfig().
  MaxLineCount`, als Literal mit Kommentar-Verweis, da Default-Parameter-
  Werte compile-time constant sein müssen), neue public Property
  `public int MaxLineCount { get; }` direkt unter `IsLoaded` ergänzen,
  im Konstruktor zuweisen.
- **Warum:** Einziger Ort, an dem der MCP-Server aktuell Zustand hält —
  `get_hotspots` braucht den Grenzwert pro Server-Session, nicht pro
  Tool-Call (Config ändert sich nicht zur Laufzeit). Additiv, bricht keine
  bestehende Call-Site (siehe „Aktueller Projektzustand").

### Datei 2: `src/AiNetLinter/Commands/McpServerCommand.cs` (Zeile 28-42)

- **Was:** Vor `using var mcpState = new McpCodeGraphServer(catalog, c);`
  eine private Hilfsmethode `ResolveMaxLineCount(LinterArgs args)`
  ergänzen (1:1-Übernahme der Logik aus
  `MapCommand.ResolveMaxLineCount`, Zeile 55-62 dort — kein Aufruf der
  privaten Methode aus `MapCommand`, da diese `private static` ist und
  eine Sichtbarkeitsanhebung für eine 6-Zeilen-Methode unverhältnismäßig
  wäre; stattdessen dieselbe kurze Logik lokal), Ergebnis in die
  `McpCodeGraphServer`-Konstruktion einreichen:
  `new McpCodeGraphServer(catalog, c, ResolveMaxLineCount(args))`.
  Neuer `using AiNetLinter.Configuration;`-Import.
- **Warum:** `args.ConfigPath` ist bereits vorhanden (aus `--config`,
  identisch zum CLI-Batch-Modus) — der MCP-Server soll dieselbe
  `rules.json` respektieren wie ein CLI-Lint-Lauf auf derselben Solution,
  sonst würde `get_hotspots` mit einem irreführenden, von der
  Projekt-Konfiguration abweichenden Default arbeiten.

### Datei 3: `src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` (neu)

- **Was:** Dünner Dispatch nach dem `GetIndexScopeTool`-Vorbild:
  ```csharp
  internal static class GetHotspotsTool
  {
      internal static Task<CallToolResult> ExecuteAsync(
          McpCodeGraphServer state, string? scopeFilter, CancellationToken ct)
      {
          var solution = state.GetCurrentSolution();
          if (solution is null) return Task.FromResult(McpToolResults.SolutionNotLoaded());

          var text = GetHotspotsScanner.BuildHotspotsText(solution, state.MaxLineCount, scopeFilter);
          return Task.FromResult(McpToolResults.Text(text));
      }
  }
  ```
- **Warum:** Keine eigene Scan-/Formatierungslogik in der Dispatch-Klasse
  (TD-005-Muster), damit ihr eigener `AIContextFootprint` klein bleibt.

### Datei 4: `src/AiNetLinter/Mcp/Tools/GetHotspotsScanner.cs` (neu)

- **Was:** Reine, `McpCodeGraphServer`-unabhängige Logik (analog
  `GetIndexScopeScanner`):
  - `internal static string BuildHotspotsText(Solution solution, int maxLineCount, string? scopeFilter)`
    — orchestriert Sammeln + Formatieren.
  - Sammel-Schritt: iteriert `solution.Projects`/`project.Documents`,
    filtert über `SourceFileCatalog.IsValidDocument(document, solutionDir)`
    (identisches Muster wie `GetIndexScopeScanner.CountCsFiles`) — liefert
    Datei-Bestand aus der tatsächlich geladenen Solution, nicht aus einem
    zweiten Filesystem-Scan.
  - Scope-Filter (falls `scopeFilter` nicht null/leer): case-insensitive
    `Contains`-Match gegen **entweder** `document.Project.Name` **oder**
    den solution-relativen Pfad der Datei (`Path.GetRelativePath
    (solutionDir, document.FilePath!)`). Deckt sowohl "nur Projekt X" als
    auch "nur Ordner/Namespace-Pfad Y" ab, ohne echte C#-Namespace-Deklaration
    zu parsen (siehe „Bekannte Ausnahmen" unten — bewusste Vereinfachung).
  - Zeilenzählung: `File.ReadAllLines(document.FilePath!).Length` pro
    passendem Dokument, in `try/catch (IOException)` gewrappt (Datei kann
    zwischen Solution-Load und Tool-Call verschwunden sein) — bei Fehler
    Datei überspringen, kein Absturz.
  - Klassifikation: `WarnThreshold = 0.80`, `CriticalThreshold = 0.95`
    als `private const double` (Duplikat aus `HotspotMapBuilder`, siehe
    „Aktueller Projektzustand" — bewusst, kein Tech-Debt-würdiges Muster).
  - Formatierung: Markdown-Tabellen für "Kritisch"/"Warnung" analog
    `HotspotMapBuilder.AppendSection`, plus eine dritte, für dieses Tool
    neue Fallunterscheidung: wenn nach Scope-Filter **0** Dateien
    übrig bleiben, explizite Meldung `"Keine Dateien im Scope (Filter:
    '<scopeFilter>') — Filter pruefen."` statt der irreführenden
    "alles im grünen Bereich"-Meldung (die nur zutrifft, wenn tatsächlich
    Dateien gescannt wurden).
- **Warum:** Gleiche Trennung wie `GetIndexScopeTool`/
  `GetIndexScopeScanner` (TD-005-Muster) — hält `GetHotspotsTool`s eigenen
  `AIContextFootprint` klein, macht die Scan-/Formatierungslogik direkt
  unit-testbar ohne MCP-Server-Infrastruktur.

### Datei 5: `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` (Zeile 17-49)

- **Was:** Neuen `tools.Add(McpServerTool.Create(...))`-Block für
  `get_hotspots` ergänzen (Parameter `string? scopeFilter = null`),
  Klassenkommentar aktualisieren (Liste der aktuell registrierten Tools
  um `get_hotspots` ergänzen, "vorbereitet für" auf die verbleibenden
  zwei EPIC-04-Tools reduzieren). Description-Text benennt explizit:
  nur `.cs`-Dateien, optionaler Filter, Basis `MaxLineCount` aus
  `rules.json`/Default.
- **Warum:** Einziger Registrierungspunkt für dateistruktur-orientierte
  Tools (siehe step-007-Aufteilung `SymbolGraphToolRegistrations`/
  `FileStructureToolRegistrations`).

### Datei 6: `src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs` (neu)

- Tests gegen `GetHotspotsScanner`/`GetHotspotsTool` direkt (kein
  Subprozess), analog `GetIndexScopeToolTests.cs`-Struktur.

### Datei 7: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:**
  - `RunAsync_ValidFixture_ServerRespondsWithSixTools` →
    `RunAsync_ValidFixture_ServerRespondsWithSevenTools` (Erwartung `7`,
    zusätzliches `Assert.Contains(tools, t => t.Name == "get_hotspots")`).
  - Neuer E2E-Test `RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture`
    (Default-`MaxLineCount`, `SymbolGraphMini`-Fixture — alle Dateien
    klein, erwartbar "im grünen Bereich"-Formulierung im Text, kein
    `IsError`). Bewusst **kein** E2E-Test für die Config-gesteuerte
    `MaxLineCount`-Verdrahtung (dafür wäre eine dedizierte `rules.json`-
    Fixture nötig) — das wird stattdessen direkt und günstiger auf
    `McpServerCommand`-Ebene getestet (Datei 8).
- **Warum:** Bestehendes Testmuster (ein E2E-Smoke-Test pro Tool +
  zentraler Tool-Count-Test), siehe TD-002 (Subprozess-Tests bewusst
  sparsam halten).

### Datei 8: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (Ergänzung, kein Subprozess)

- **Was:** Neuer Unit-Test (kein Subprozess) direkt gegen die neue
  `McpServerCommand`-Hilfsmethode, analog dem bereits bestehenden Muster
  für `MapCommand.ResolveMaxLineCount` (falls dafür bereits ein Test
  existiert — sonst neu, minimal): `rules.json` mit
  `"MaxLineCount": 5` in ein Temp-Verzeichnis schreiben, `LinterArgs`
  mit `ConfigPath` darauf zeigen lassen, Ergebnis der neuen
  `ResolveMaxLineCount`-Methode gegen `5` prüfen. Falls die Methode
  `private` bleibt (wie geplant): stattdessen `internal` machen (wie
  bei den übrigen kleinen Hilfsmethoden in `McpServerCommand`, z. B.
  `TryLoadSolutionAsync`) statt eines Reflection-Workarounds.
- **Warum:** Direkter, günstiger Nachweis der Config-Verdrahtung ohne
  Subprozess-Overhead (TD-002).

## Tests

- [ ] `GetHotspotsToolTests.ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
- [ ] `GetHotspotsToolTests.ExecuteAsync_SmallMaxLineCount_MarksFileAsCritical`
      (`maxLineCount: 1` gegen die Fixture — jede Datei mit >0 Zeilen landet
      in der Kritisch-Sektion)
- [ ] `GetHotspotsToolTests.ExecuteAsync_MidRangeMaxLineCount_MarksFileAsWarning`
      (Grenzwert so gewählt, dass eine bekannte Fixture-Datei zwischen 80%
      und 95% landet — exakte Zeilenzahl von `Greeter.cs` vorab per Read
      ermitteln, nicht schätzen)
- [ ] `GetHotspotsToolTests.ExecuteAsync_DefaultMaxLineCount_AllFilesGreen`
      (Default `700` — keine Fixture-Datei kommt in die Nähe, erwartbar
      "im grünen Bereich"-Text)
- [ ] `GetHotspotsToolTests.ExecuteAsync_ScopeFilterMatchesProjectName_ReturnsAllFiles`
      (Filter = Projektname aus `SymbolGraphMini.csproj` → alle 4 `.cs`-
      Dateien weiterhin enthalten)
- [ ] `GetHotspotsToolTests.ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessage`
      (Filter = frei erfundener String → explizite "Keine Dateien im
      Scope"-Meldung, kein `IsError`, keine irreführende "alles grün"-
      Aussage)
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithSevenTools`
      (umbenannt/erweitert, siehe Datei 7)
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture`
      (neu, siehe Datei 7)
- [ ] Neuer Unit-Test für `ResolveMaxLineCount`-Verdrahtung (siehe Datei 8,
      exakter Name durch Coder frei wählbar, z. B.
      `ResolveMaxLineCount_ConfigWithCustomMaxLineCount_ReturnsConfiguredValue`)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build AiNetLinter.slnx` grün, 0 Warnungen
- [ ] `dotnet test AiNetLinter.slnx` grün
- [ ] `--footprint GetHotspotsTool --path .` < 2500 (dokumentiert im
      Ergebnis, wie in step-004..step-008 praktiziert)
- [ ] `--footprint FileStructureToolRegistrations --path .` < 2500 —
      **reißt das Limit:** dritte Registrar-Klasse (z. B.
      `AnalysisToolRegistrations`) als Ausweich-Option in diesem Step
      selbst umsetzen (nicht auf den nächsten Step verschieben, siehe
      „Aktueller Projektzustand")
- [ ] `ainetlinter --config rules.json --path .` selbst-lintet sauber
      (0 Violations)
- [ ] Commit auf `main` (Conventional Commit, `[codegraph-mcp]`-Suffix,
      `### Commit-Vorschlag`-Abschnitt laut `AiNetLinterRichtlinien.mdc` §4)
- [ ] **Dogfooding (Muss-Haben, blockierend):** `get_hotspots` einmal
      ad-hoc gegen die reale `AiNetLinter.slnx` aufrufen (Subprozess wie in
      step-005..step-008, `--path .`), Ergebnis in `step-result.md`
      Abschnitt „Dogfooding" dokumentieren. Plausibilitätsprüfung:
      `rules.json` im Repo-Root setzt `MaxLineCount: 500` — mindestens
      stichprobenartig mit `--footprint`/Dateigröße eines bekannten,
      großen Files (z. B. `MetricsConfig.cs`, aktuell >300 Zeilen laut
      obigem Read) gegenprüfen, ob die vom Tool gemeldete Kategorie
      (grün/warnung/kritisch) zur tatsächlichen Zeilenzahl passt.
- [ ] `step-009/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#AIContextFootprint` — 2500-Zeilen-Limit,
  direkt relevant für `GetHotspotsTool`/`FileStructureToolRegistrations`
  (siehe DoD).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — kein DI-Container (Tools
  erreichen `McpCodeGraphServer` weiter per Delegate-Closure), Result-
  Pattern statt Exceptions (`McpToolResults.SolutionNotLoaded()` statt
  Wurf), Build/Test-Pflicht vor Abschluss, Commit-Vorschlag-Pflicht.

## Bekannte Ausnahmen

- **Kein echtes C#-Namespace-Parsing für den Scope-Filter.** Der Filter
  matched gegen Projekt-Name und solution-relativen Dateipfad, nicht
  gegen die tatsächliche `namespace`-Deklaration im Dateikopf. Für die
  meisten .NET-Projekte (Ordnerstruktur ≈ Namespace-Konvention) liefert
  das praktisch gleichwertige Ergebnisse, ist aber nicht identisch bei
  Datei-Namespace-Abweichungen vom Ordnerpfad. `konzept.md`s Tabelle
  spezifiziert "Namespace-/Projekt-Filter" bewusst vage (Non-Goal-Konflikt
  mit "kein Cross-Language-Symbolgraph" besteht nicht, da rein `.cs`-
  intern) — falls sich das in der Praxis (Dogfooding) als zu ungenau
  erweist, ist eine Nachschärfung (echtes `NamespaceDeclarationSyntax`-
  Parsing) ein Kandidat für einen künftigen Tech-Debt-Eintrag, kein
  Blocker für diesen Step.
- **Keine `FileFiltersConfig`-Anbindung** (Ausschluss-Muster analog
  `WebFileCatalog`) — `.cs`-Zählung läuft ausschließlich über
  `SourceFileCatalog.IsValidDocument`, das `obj`/`bin` bereits
  ausschließt (identisches Muster wie step-008).

## Notes

- **Wiederverwendung, nicht Neubau:** `SourceFileCatalog.IsValidDocument`
  (Solution-Dateibestand), `MapCommand.ResolveMaxLineCount`-Logik
  (Config-Laden), `GetIndexScopeScanner`-Struktur (Aufbau der neuen
  Scanner-Klasse) — keine neue Infrastruktur, drei bestehende Muster
  kombiniert.
- **Vorsicht bei der Zeilenzahl-Ermittlung für die Warning-Test-Fixture:**
  nicht die Zeilenzahl von `Greeter.cs`/`Caller.cs`/etc. schätzen — vor
  dem Schreiben des Tests einmal `wc -l` (oder `Get-Content | Measure-
  Object -Line`) auf die tatsächliche Fixture-Datei anwenden, damit der
  gewählte `maxLineCount`-Testwert die 80-95%-Grenze zuverlässig trifft.
- **`FileStructureToolRegistrations`-Footprint ist die wahrscheinlichste
  Stelle, an der dieser Step überraschend Mehrarbeit braucht** (siehe DoD)
  — beim Selbst-Lint-Schritt zuerst prüfen, dann erst mit den restlichen
  DoD-Punkten (Dogfooding etc.) weitermachen, um nicht doppelt zu
  committen.
