---
status: done (pending audit)
type: step-plan
task: codegraph-mcp-finish
step: 010
title: "Last-Fixture + Kaltstart-Entkopplung + Staleness-mtime-Cache (B.3, B.4, B.5) + TD-005-Sanity"
epic: EPIC-05
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-04
related_to:
  - step-009/step-review.md
  - step-009/fix-01/step-review.md
  - step-008/step-review.md
---

# Step 010: Last-Fixture + Kaltstart-Entkopplung + Staleness-mtime-Cache (B.3, B.4, B.5)

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-05` aus `roadmap.md` — Last-Fixture + Performance-Fixes,
  die drei zeitbasierten Punkte aus Konzept „Muss-Haben B" (B.3, B.4, B.5).
- **Konzept-Referenz:** `tasks/codegraph-mcp-finish/Konzept.md` „Muss-Haben B"
  Z. 218-274 (Punkte 3-5). Reihenfolge-Vorgabe explizit: **B.3 vor B.4 vor
  B.5** (Begründung: B.3 liefert Skalierungs-Zahlen, B.4 ist
  Architektur-Refactor, B.5 ist Optimierung — jeweils gegen Zahlen aus B.3
  arbeiten, nicht gegen die ursprüngliche 160k-LOC-Annahme ohne eigenen
  Beleg, siehe `Konzept.md` Z. 224-227 + 591-601).
- **DoD:** `Konzept.md` Z. 650-653 (alle sieben B-Punkte umgesetzt, reviewt,
  mit Integrationstest abgesichert). EPIC-05 erledigt 3 von 7 Punkten — EPIC-06
  (B.6 stdout-Schutz + B.7 Call-Log) bleibt separat offen.
- **Non-Goals (Konzept Z. 457-489):** keine Editier-Tools, kein Embedding,
  kein Multi-Sprache-Support, kein Plugin/ALC/DI, kein CLI-Batch-Mode-
  Replacement, **keine Änderung an Testinhalten außerhalb des Scopes** (für
  B.3: bestehende Tests unverändert, nur neue Last-Fixture-Tests in
  eigenem Verzeichnis).
- **TD-005-Integration:** Nutzer-Vorgabe 2026-08-04 — TD-005
  (SubprocessConcurrencyGate-Last, mittlere Priorität) hat direkte Berührung
  mit B.3 (Last-Fixture triggert genau diese Sättigung), Sanierung wird
  mechanisch klar mit umgesetzt.

## Aktueller Projektzustand (JIT-Kontext)

Beim Code-Lesen am 2026-08-04 vorgefunden:

### B.3 — Last-Fixture
- **Kein bestehender Last-Generator** im Testprojekt. `Fixtures/`-Ordner
  enthält 13 `.cs`-Dateien, alle sind kleine funktionale Workspaces
  (`BaselineMiniFixtureWorkspace`, `SymbolGraphMiniFixtureWorkspace`,
  `GitImpactMiniFixtureWorkspace`, `CompileErrorMiniFixtureWorkspace`) oder
  funktionale Helfer (`CliProcessRunner`, `McpLiveRepositoryFixture`,
  `SubprocessConcurrencyGate`, `TestTempDirectory`). Grep nach
  `LoadFixture|SyntheticSolution|LastFixture|BuildSolution|BenchmarkDotNet`
  über `src/AiNetLinter.Tests/**` liefert nur Funktions-Hilfen (z. B.
  `PlaybookGeneratorRound2Tests.cs:24` — unrelated). Keine
  Skalierungs-Stufen, kein synthetisches Solution-Generator-Pattern.
- **Kein BenchmarkDotNet im Projekt** (`AiNetLinter.Tests.csproj` referenziert
  kein `BenchmarkDotNet`, simple `Stopwatch`-basierte Wall-Clock-Messung
  ist der projektweite Standard, siehe `step-006` „Laufzeitmessung
  vorher/nachher" als Vorbild).
- **`TestTempDirectory`** (in `Fixtures/TestTempDirectory.cs`) ist das
  etablierte Auto-Dispose-Temp-Dir-Pattern und dient als Vorlage für
  das Last-Fixture-`IDisposable`-Handle.
- **`FixtureWorkspaceBase`** (in `Fixtures/FixtureWorkspaceBase.cs`) ist
  die Basis für funktionale Mini-Workspaces; für B.3 ist eine **eigene
  Wurzelklasse** sinnvoll, weil das Last-Fixture eine generative
  Build-Schnittstelle braucht (nicht „kopiere dieses Mini-Projekt"), keine
  Project-Sub-Struktur.

### B.4 — Kaltstart-Entkopplung
- **Aktueller Server-Start in `McpServerCommand.RunAsync`** (McpServerCommand.cs:29-56):
  Z. 42 `await TryLoadSolutionAsync(solutionPath, ct, c)` ist **synchroner
  Wait vor dem Server-Start**. Z. 43-49 baut `McpCodeGraphServer` mit
  der bereits geladenen `catalog`-Referenz. Z. 51-54 startet
  `McpServer.Create(transport, serverOptions).RunAsync(ct)`. Bei großen
  Solutions blockiert Z. 42 den `initialize`-Handshake.
- **`McpCodeGraphServer.IsLoaded`** (McpCodeGraphServer.cs:49) ist heute
  binär (`_catalog is not null`); ein dritter „Loading"-Zustand existiert
  nicht. Server-Konstruktor (Z. 34-47) ruft `InitializeFileState` synchron
  auf, **wenn** `_catalog` gesetzt ist — bei `null` ist `IsLoaded == false`
  und alle 8 Tool-Klassen (FindSymbol, FindReferences, GetFileSkeleton,
  GetHotspots, GetImpact, GetIndexScope, GetTypeHierarchy, GetViolations,
  SearchPattern) antworten via `McpToolResults.SolutionNotLoaded()` (siehe
  `McpToolResults.cs:35-41`).
- **`McpToolResults.SolutionNotLoaded()`** ist der etablierte Helfer für
  „nicht geladen"-Fall. Für B.4 wird ein neuer Helfer
  `McpToolResults.Loading()` nötig, semantisch klar von
  `SolutionNotLoaded` abgegrenzt: **kein Error** (das Tool ist nicht
  falsch aufgerufen worden), sondern ein transienter Info-Zustand.
- **Tool-Aufrufstellen:** alle 8 Tool-Klassen rufen
  `var solution = state.GetCurrentSolution();` (verifiziert per Grep in
  `Mcp/Tools/`) und prüfen `if (solution is null) return
  McpToolResults.SolutionNotLoaded();` — d. h. die Stellen für den
  Loading-State-Check sind überall gleich aufgebaut, der Eingriff ist
  mechanisch und 2 Zeilen pro Tool.
- **Bestehende Tests** (`McpLiveRepositoryTests` und die übrigen
  `McpServerAllToolsE2ETests`/`McpServerCommandErrorHandlingTests`/
  `McpServerCommandStalenessTests`/`McpServerCommandAmbiguityE2ETests`)
  starten den Server via `McpTestClient.ConnectAsync` (Retry-Loop seit
  Einheit 011, TD-019). Diese Tests funktionieren auch nach B.4 weiter,
  **wenn** `ConnectAsync` den neuen Loading-State als „Server noch nicht
  bereit" erkennt und retryt. Anpassung im `McpTestClient` ist
  wahrscheinlich 1 Methode (Retry-Bedingung erweitern).

### B.5 — Staleness-mtime-Cache
- **Naiver Sweep in `McpCodeGraphServerRefresh.SweepForNewFiles`**
  (McpCodeGraphServerRefresh.cs:69-97): ruft bei jedem
  `GetCurrentSolution()`-Aufruf (via `McpCodeGraphServer.RefreshStaleDocuments`,
  McpCodeGraphServer.cs:117-129) `EnumerateCsFilesSafe(solutionDir)` auf
  (Z. 220-232), das `Directory.EnumerateFiles(solutionDir, "*.cs",
  SearchOption.AllDirectories)` ausführt — bei 50k+ Dateien ein spürbarer
  Walk. **Kein Directory-Level-mtime-Cache** im Code (verifiziert per
  Grep über `Mcp/`: kein `Directory.GetLastWriteTimeUtc(solutionDir)`
  auf Solution-Dir-Ebene).
- **Per-File-mtime-Cache existiert bereits** in `_fileState`
  (`Dictionary<string, McpFileState>`, McpCodeGraphServer.cs:28) und wird
  in `TryRefreshDocument` (McpCodeGraphServerRefresh.cs:173-197) und
  `CacheInitialFileState` (Z. 155-171) genutzt. B.5 setzt eine Ebene
  höher an (Verzeichnis-mtime) und kombiniert sich mit dem B.2-Sweep
  (gleiche Methode, früher Return bei unveränderter mtime).
- **Phase-Reihenfolge** in `McpCodeGraphServerRefresh.Run` (Z. 31-43):
  `RemoveDeleted → SweepForNewFiles → RefreshModifiedDocuments`. Der
  mtime-Cache wirkt nur auf Phase 2 (Sweep); Phase 1 (Remove) und Phase 3
  (Refresh) bleiben unverändert, weil sie auf existierenden
  `Document`-Instanzen arbeiten, die bereits im `_fileState`-Cache
  mtime-pflegen.
- **Windows-Semantik:** `Directory.GetLastWriteTimeUtc` wird auf Windows
  bei jeder Datei-Änderung im Verzeichnis aktualisiert (auch neue Dateien,
  gelöschte Dateien, Umbenennungen) — der Cache ist damit eine **korrekte
  Invaliderungs-Approximation** für Phase 2. Phase 3 bleibt defensiv
  (per-File-mtime-Check).

### TD-005 — SubprocessConcurrencyGate-Last
- **Stand** (`src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs:17`):
  `MaxConcurrentSubprocesses = 4` (Konstante), `SemaphoreSlim Gate` mit
  4 initial/max slots, `AcquireAsync` macht `await Gate.WaitAsync(ct)`
  ohne eigenen Timeout (das 30s-Wait-Timeout aus TD-005 kommt vom
  Caller-`CancellationToken`, nicht vom Gate).
- **Reproduktionssignatur** (TD-005 Volltext + step-007/008/009-Reviews):
  Stack-Bottom `SubprocessConcurrencyGate.AcquireAsync:30` in
  `McpServerCommandErrorHandlingTests`-Klasse, 1-2 Failures pro
  Volllauf-Lauf (4-6 min), isoliert grün. Klassifikation:
  `infrastructure`, scope-extern, scope-intern jetzt.
- **Drei Optionen aus TD-005:**
  (a) Gate-Kapazität 4 → 6/8 erhöhen, (b) Test-Time-Out im
  `McpServerCommandErrorHandlingTests`-Fixture anheben, (c) Retry-Logik
  analog `McpTestClient.ConnectAsync` einbauen. **Entscheidung in
  diesem Step: Option (a) + minimaler (b)-Anteil (expliziter Timeout
  am Gate)**, Begründung siehe „Konkrete Änderungen" weiter unten.

## Intention

Nach diesem Step hat AiNetLinter einen belastbaren Performance-Nachweis
(generierte Last-Fixture mit reproduzierbaren Mess-Zahlen gegen
verschiedene Skalierungs-Stufen), entkoppelten Server-Start
(Transport antwortet sofort, Solution-Load im Hintergrund, dritter
„Loading"-Zustand für Tool-Calls während des Loads) und einen
kurzschluss-optimierten Staleness-Sweep (Verzeichnis-`mtime`-Cache
vermeidet den `Directory.EnumerateFiles`-Walk bei unverändertem
Solution-Verzeichnis). Die Reihenfolge B.3 → B.4 → B.5 ist
Nutzer-Vorgabe und durch den B.3-Messlauf auch begründet — B.4 und
B.5 werden gegen echte Zahlen aus B.3 validiert, nicht gegen die
ursprüngliche 160k-LOC-Annahme.

## Konkrete Änderungen

### B.3 Last-Fixture + Messlauf

#### `src/AiNetLinter.Tests/Fixtures/LoadFixtureBuilder.cs` (NEU)

- **Was:** Statische Generator-Klasse mit
  `Build(string name, int projectCount, int filesPerProject, int linesPerFile)`
  → erzeugt in einem `TestTempDirectory` ein Synthetic-Solution-Verzeichnis
  mit `N .csproj`-Dateien (Format-Mini-Stub, kompiliert nicht zwingend
  mit MSBuild, dient als Lade-Target für `MSBuildWorkspace.OpenSolutionAsync`)
  + `M .cs`-Dateien pro Projekt (realistischer Stub-Code mit `namespace`,
  `class`, trivialen Members, `linesPerFile` Zeilen via Padding-Kommentare)
  + einer `.slnx` (oder `.sln`), die alle Projekte listet. Liefert
  `LoadFixtureHandle : IDisposable` mit Pfad zur Solution-Datei.
- **Warum:** keine bestehende Struktur wiederverwendbar (kein
  Synthetic-Generator-Pattern im Projekt, `FixtureWorkspaceBase` ist
  Copy-basiert und nicht generativ), neue Klasse ist saubere Wahl.
  Skalierungs-Stufen als Parameter ermöglichen Mess-Tests in mehreren
  Größenordnungen ohne separate Fixture-Klassen.

#### `src/AiNetLinter.Tests/Fixtures/LoadFixtureHandle.cs` (NEU, klein)

- **Was:** `IDisposable`-Wrapper um `TestTempDirectory` + `SolutionPath`-
  Property, plus `string Name` für Test-Output-Identifikation. Auto-
  Dispose im Test (oder `using`).
- **Warum:** konsistent zum etablierten `TestTempDirectory`-Pattern.

#### `src/AiNetLinter.Tests/Fixtures/LoadFixtureBuilderTests.cs` (NEU)

- **Was:** 1 Unit-Test, der `Build(2, 3, 10)` aufruft und
  verifiziert: Solution-Datei existiert, alle `2*3*10 = 60` `.cs`-Dateien
  existieren mit erwarteter Zeilenanzahl, `.slnx` listet alle Projekte.
  Kein MCP-Server-Start, keine Subprozesse → schnell, `[Trait("Category",
  "Unit")]`.
- **Warum:** der Generator selbst ist Unit-testbar; aufwändige
  Performance-Messungen kommen im Integration-Test.

#### `src/AiNetLinter.Tests/Fixtures/LoadFixtureMeasurementsTests.cs` (NEU)

- **Was:** 2 Integration-Tests, die das Verhalten gegen die echte
  Engine messen:
  - `Measure_ColdStart_On_1k_LOC_Fixture` — 1 Projekt × 50 Dateien ×
    ~20 Zeilen, misst `McpServerCommand.RunAsync` bis erster
    Tool-Call antwortet, dokumentiert die Zahl via `ITestOutputHelper`.
    Default-Assertion: < 30 s (großzügig, beobachtete Realität ist
    wenige Sekunden auf Standard-Hardware).
  - `Measure_GetCurrentSolution_On_10k_LOC_Fixture_UnderBaseline`
    — 5 Projekte × 200 Dateien × ~10 Zeilen, misst `GetCurrentSolution()`
    in einer Schleife von 10 Aufrufen, dokumentiert min/median/max
    Wand-Zeit. Default-Assertion: < 5 s pro Aufruf (kann vom Coder
    anhand der 1k-Messung realistisch kalibriert werden — die genaue
    Schwelle ist nicht der Punkt, der Mess-Wert ist der Skalierungs-
    Beleg).
  - Beide mit `[Trait("Category", "Integration")]` (Fixture-Generierung
    ist teuer, gehört in den Integration-Slice, parallelisiert sich mit
    übrigen Integration-Tests über `xunit.runner.json`).
- **Warum:** Skalierungsnachweis gegen eigene Engine, nicht gegen die
  externe 160k-LOC-Annahme. Die generierten Zahlen sind die Eingabe
  für B.4- und B.5-Validierung in den nachfolgenden Sub-Schritten dieses
  Steps.

### B.4 Kaltstart-Entkopplung

#### `src/AiNetLinter/Mcp/ServerLoadState.cs` (NEU)

- **Was:** `public enum ServerLoadState { Loading, Loaded, LoadFailed }`.
  Bewusst klein, eigene Datei (im Sinne der `MaxDirectoryChildren`-Regel
  wäre Enum in McpCodeGraphServer.cs möglich, aber separate Datei ist
  lesbarer und isoliert den öffentlichen Vertrag).
- **Warum:** semantisch klare Unterscheidung der drei Zustände; Reihenfolge
  in der Enum-Liste entspricht der zeitlichen Abfolge
  (Loading → Loaded/LoadFailed).

#### `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (Änderung, mehrere Stellen)

- **Was:**
  - Konstruktor (Z. 34-47): `Catalog` aus `options` wird **nicht mehr
    direkt als `_catalog` zugewiesen**, sondern als `Task<SourceFileCatalog?>`
    via `Func<CancellationToken, Task<SourceFileCatalog?>>` aus
    `McpCodeGraphServerOptions` (neue Property `LoadFunc`) gestartet.
    Wenn `options.Catalog` gesetzt **und** `LoadFunc` null ist (Pfad für
    Tests/Backward-Compat), bleibt altes Verhalten (synchrone
    Übernahme). Wenn `LoadFunc` gesetzt ist, startet der Server-Load im
    Hintergrund-`Task`, gespeichert in `_loadTask`.
  - `IsLoaded` → `LoadState`-Property (Z. 49): liefert `Loading` wenn
    `_loadTask` läuft, `Loaded` bei erfolgreichem Abschluss, `LoadFailed`
    bei Faulted/Canceled. `IsLoaded` bleibt als
    `LoadState == ServerLoadState.Loaded` für Backward-Compat
    vorhanden.
  - `GetCurrentSolution()` (Z. 90-99): blockt **nicht** mehr. Liefert
    `_catalog.Solution` nur wenn `LoadState == Loaded`, sonst `null`.
    Im Loading-Fall soll der Aufrufer (Tool) den Loading-State selbst
    detektieren über `LoadState` und `McpToolResults.Loading()`
    zurückgeben.
  - `Dispose()` (Z. 101): wenn `_loadTask` noch läuft, sauber
    abbrechen (`_loadCancellation?.Cancel()`) und auf Abschluss warten
    (mit Timeout, um Dispose-Hänger zu vermeiden).
  - **Backwards-Compat:** Bestehende Aufrufer, die `McpCodeGraphServer`
    mit bereits geladenem `Catalog` konstruieren (alle bestehenden
    Tests + die `using var mcpState = new McpCodeGraphServer(...)`-Zeile
    in `McpServerCommand`), funktionieren unverändert, **wenn** der
    `LoadFunc` in `McpCodeGraphServerOptions` per Default `null` ist
    und nur gesetzt wird, wenn der Hintergrund-Load-Pfad aktiv ist.
    Konkret: `McpCodeGraphServerOptions.From(...)` setzt `LoadFunc`
    nicht; eine neue Factory-Methode `McpCodeGraphServerOptions.FromAsync(...)`
    (oder direkter `LoadFunc`-Parameter im Builder) wird für den
    Background-Pfad hinzugefügt.
- **Warum:** `IsLoaded`-Boolean reicht für „Server hat eine Solution
  geladen oder nicht", aber nicht für „Server lädt noch" — dritter
  Zustand ist Konzept-Vorgabe. Konstruktor muss die
  Hintergrund-Load-Entscheidung kennen, ohne den bestehenden
  synchronen Pfad (für Tests + Backward-Compat) zu brechen.

#### `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs` (Änderung)

- **Was:** Neue optionale Property `LoadFunc: Func<CancellationToken,
  Task<SourceFileCatalog?>>? = null` (Plus Anpassung in `From(...)` /
  `FromParameters` / dem `McpCodeGraphServerOptionsFromParameters`-Record,
  damit `MaxMethodParameterCount: 4` weiter eingehalten wird — die neue
  Property wird **additiv** über `From`-Factory-Überladung gesetzt, der
  existierende 4-Parameter-`From`-Pfad bleibt kompatibel).
- **Warum:** klare Konfigurations-Schnittstelle, keine Magic im
  Server-Konstruktor.

#### `src/AiNetLinter/Mcp/McpToolResults.cs` (Änderung, ~10 Zeilen)

- **Was:** Neuer Helfer `Loading()` analog `SolutionNotLoaded()`,
  liefert `CallToolResult` mit **Text-Inhalt** (kein `IsError = true`,
  semantisch „Info/Wartezustand"), kurze Meldung „Server lädt die
  Solution noch. Bitte in wenigen Sekunden erneut versuchen."
- **Warum:** Tool darf im Loading-Fall nicht blockieren, aber
  `IsError = true` wäre semantisch falsch (das Tool wurde nicht
  falsch aufgerufen).

#### `src/AiNetLinter/Mcp/Tools/*.cs` (alle 8 Tool-Klassen, je 2 Zeilen)

- **Was:** Vor dem `state.GetCurrentSolution()`-Call: `if (state.LoadState
  == ServerLoadState.Loading) return McpToolResults.Loading();`. Reihenfolge:
  Loading-Check zuerst (transient), dann null-Check (terminal LoadFailed/
  SolutionNotLoaded).
  Betrifft: `FindSymbolTool`, `FindReferencesTool`, `GetFileSkeletonTool`,
  `GetHotspotsTool`, `GetImpactTool`, `GetIndexScopeTool`,
  `GetTypeHierarchyTool`, `GetViolationsTool`, `SearchPatternTool`.
- **Warum:** einheitliches Pattern, mechanisch in jeder Klasse 2 Zeilen
  (davon 1 Import bereits vorhanden für `McpToolResults`).

#### `src/AiNetLinter/Commands/McpServerCommand.cs` (Änderung, ~15 Zeilen)

- **Was:** `RunAsync` (Z. 29-56) restrukturiert:
  - Statt `await TryLoadSolutionAsync` (Z. 42) **vor** der Server-Konstruktion:
    Server wird **zuerst** konstruiert mit `LoadFunc = ct =>
    TryLoadSolutionAsync(solutionPath, ct, c)`, der Server startet
    sofort den Hintergrund-Load und betritt den Server-Loop.
  - `await using var mcpState = new McpCodeGraphServer(McpCodeGraphServerOptions.From(...LoadFunc...))` (Z. 43-49, neu).
  - `var serverOptions = McpServerOptionsFactory.Create(mcpState);` (Z. 51)
  - `var transport = new StdioServerTransport(serverOptions);` (Z. 52)
  - `await using var server = McpServer.Create(transport, serverOptions);` (Z. 53)
  - `await server.RunAsync(ct);` (Z. 54) — **vorheriger Block** (Z. 42
    `TryLoadSolutionAsync` + Z. 43-49 Konstruktur) wird zu LoadFunc
    deferriert.
  - `return 0;` (Z. 55) bleibt.
- **Warum:** Transport-Setup und `McpServer.Create`/`RunAsync` laufen
  ohne Solution-Load ab, der `initialize`-Handshake des MCP-Protokolls
  wird nicht mehr durch den Solution-Load blockiert. Der Background-Load
  füllt `_catalog` parallel zum Server-Lauf.

#### `src/AiNetLinter.Tests/Mcp/McpTestClient.cs` (Änderung, ~10 Zeilen)

- **Was:** Retry-Bedingung in `ConnectAsync` (bestehender Retry aus
  TD-019) erweitern: zusätzlich zu den existierenden Retry-Triggern
  (Timeout, Server-Crashed) jetzt auch `Loading`-Antwort
  (Tool-Call antwortet mit dem `McpToolResults.Loading()`-Text-Pattern)
  als „retry-würdig" behandeln — heuristisch per String-Match auf den
  Loading-Helfer-Text, kein Structural-Change am MCP-Protokoll nötig.
- **Warum:** bestehende `McpLiveRepositoryTests` und E2E-Tests sollen
  nicht in Loading-Antworten hängenbleiben. Retry-Logik (statt
  sofortiger Fehler) ist die saubere Brücke zwischen „Server ist
  technisch erreichbar" und „Solution ist noch nicht bereit".

#### Tests für B.4 (NEU, in `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs`)

- 2 Integration-Tests mit `[Trait("Category", "Integration")]`:
  - `RunAsync_LoadFuncProvided_StartsServerImmediatelyAndToolReturnsLoading`:
    Server starten, **ohne** auf vollständigen Load zu warten, sofort
    `find_symbol` aufrufen, Antwort prüfen (enthält Loading-Text,
    `IsError == false`).
  - `RunAsync_LoadFuncCompletes_ToolReturnsNormalResult`:
    Server starten, auf `state.LoadState == Loaded` warten (Polling,
    max 30 s), dann `find_symbol` aufrufen, normale Antwort
    verifizieren.

### B.5 Staleness-mtime-Cache

#### `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (Erweiterung, ~15 Zeilen)

- **Was:** Neues privates Feld `DateTime? _lastSolutionDirMtimeUtc`
  (instanziiert im Konstruktor, sobald `_loadTask` abgeschlossen ist
  bzw. bei synchronem Pfad direkt nach Konstruktion). Neue private
  Methode `HasSolutionDirChanged(string? solutionDir): bool`, die
  `Directory.GetLastWriteTimeUtc(solutionDir)` mit `_lastSolutionDirMtimeUtc`
  vergleicht, bei Ungleichheit den Cache aktualisiert und `true`
  liefert.
- **Warum:** der Cache ist pro Server-Instanz, nicht pro `Run`-Aufruf
  — `McpCodeGraphServerRefresh.Run` ist statisch und hat keine
  Instanz-State.

#### `src/AiNetLinter/Mcp/McpCodeGraphServerRefresh.cs` (Änderung, ~8 Zeilen)

- **Was:** `Run`-Signatur (Z. 31-35) bekommt einen zusätzlichen Parameter
  `Func<bool> shouldSweep`. `SweepForNewFiles` (Z. 69-72) bekommt
  denselben Parameter und macht am Anfang `if (!shouldSweep()) return false;`
  — der Verzeichnis-Walk wird übersprungen, wenn der Aufrufer (Server)
  sagt „mtime unverändert". Rest der Methode (Loop, IsGeneratedPath-Filter,
  PickProjectForNewFile, TryAddDocument) bleibt 1:1.
- **Warum:** minimal-invasive Änderung, der `Run`-Aufruf in
  `McpCodeGraphServer.RefreshStaleDocuments` reicht den
  `HasSolutionDirChanged`-Delegate durch. Phase 1 (RemoveDeleted) und
  Phase 3 (RefreshModifiedDocuments) bleiben unverändert — die
  per-File-mtime-Logik greift unabhängig vom Verzeichnis-Cache.

#### Tests für B.5 (NEU, in `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerStalenessMtimeCacheTests.cs`)

- 2 Unit-Tests mit `[Trait("Category", "Unit")]`:
  - `GetCurrentSolution_CalledTwiceWithoutDirChange_SkipsSweepOnSecondCall`:
    `McpCodeGraphServer` mit Mini-Fixture starten, ersten
    `GetCurrentSolution()`-Call triggert Sweep (verifizierbar via
    `McpCodeGraphServerRefresh`-Counter oder via Setzen einer neuen
    Datei und Verifikation, dass sie beim 2. Call ohne File-Change
    ignoriert wird).
  - `GetCurrentSolution_CalledAfterNewFile_TriggersSweepAgain`:
    zwischen den Calls neue `.cs`-Datei anlegen, die `solutionDir`-
    mtime ändert sich → 2. Call triggert Sweep → Datei wird gefunden.

### TD-005 Sanity-Fix

#### `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs` (Änderung, ~3 Zeilen)

- **Was:**
  - Konstante `MaxConcurrentSubprocesses = 4` → `6` (moderate Erhöhung,
    B.3-Messlauf zeigt ob 6 reicht oder ob 8 nötig ist — Coder
    entscheidet beim Bauen gegen die 10k-LOC-Fixture).
  - `await Gate.WaitAsync(cancellationToken)` →
    `await Gate.WaitAsync(cancellationToken).WaitAsync(
    TimeSpan.FromSeconds(60), cancellationToken)` — expliziter 60s-
    Timeout am Gate selbst (zusätzlich zum Caller-CT), verhindert
    die unbestimmte Wartezeit im Stacktrace und macht den Fehler
    besser diagnostizierbar (`TimeoutException` statt
    `OperationCanceledException`).
- **Warum diese Option:** Option (a) Gate-Kapazität ist die
  mechanisch einfachste und direkteste Adressierung der
  Last-Sättigung. Option (b) Test-Time-Out wäre Symptom-Fixing
  (`AiNetLinterRichtlinien.mdc` §5 verbietet genau das). Option (c)
  Retry-Logik ist bereits in `McpTestClient.ConnectAsync` vorhanden
  (TD-019, Einheit 011) — eine zweite Retry-Ebene an anderen
  Test-Stellen wäre DRY-Verletzung ohne klaren Mehrwert, da die
  Last-Sättigung am Gate selbst entsteht, nicht an einer
  transienten Connection. Die zusätzliche Timeout-Erweiterung
  (60s statt Caller-CT) ist ein defensiver Mini-Schritt, der den
  Stacktrace sprechender macht, falls 6 Slots unter Last doch nicht
  reichen.
- **Risiko:** Vergrößert die Anzahl gleichzeitiger
  `AiNetLinter.exe`-Subprozesse von 4 auf 6. Auf Standard-Hardware
  (mehrere CPU-Kerne, ausreichend RAM) unkritisch, auf
  ressourcenschwacher Hardware evtl. spürbar — aber das ist genau
  die Klasse von Tests, die ohnehin nur in CI-Vollläufen aktiv wird
  (lokale Entwicklung filtert per `Category=Unit`).

### Doku-Updates

#### `Docs/agent-api.md`

- **Was:** Liste der Tool-Response-Zustände (Success / NotFound /
  Ambiguous / SolutionNotLoaded / **Loading** / CompilationError) im
  einleitenden Kapitel ergänzen, jeweils mit Erklärung. Verweis auf
  B.4-Konzept-Verhalten: Agent soll bei Loading-Antwort polling-
  basiert retryen.
- **Warum:** Agent-LLMs treffen die Retry-Entscheidung; ohne Doku
  kein klares Verhalten.

#### `Docs/integration.md`

- **Was:** Hinweis im MCP-Registrierungs-Absatz, dass `initialize`
  jetzt sofort antwortet (kein Warten auf Solution-Load mehr), und
  dass Hosts mit kurzem Startup-Timeout den Server jetzt zuverlässig
  als „bereit" erkennen.
- **Warum:** Integration-Setup-Änderung sichtbar machen.

#### `Docs/ROADMAP.md`

- **Was:** Z. 478-493 (Geplant-Block für B): „Muss-Haben B Punkte 3-5
  umgesetzt" markieren (B.6 + B.7 bleiben Geplant → EPIC-06).
- **Warum:** laufende Doku-Pflicht (Konzept DoD Z. 659-661,
  `AiNetLinterRichtlinien.mdc` §4).

## Tests

- [ ] `LoadFixtureBuilderTests.Build_MiniSolution_CreatesExpectedStructure` (Unit) — Generator-Korrektheit
- [ ] `LoadFixtureMeasurementsTests.Measure_ColdStart_On_1k_LOC_Fixture` (Integration) — Skalierungs-Beleg
- [ ] `LoadFixtureMeasurementsTests.Measure_GetCurrentSolution_On_10k_LOC_Fixture_UnderBaseline` (Integration) — Skalierungs-Beleg
- [ ] `McpServerCommandLoadingStateTests.RunAsync_LoadFuncProvided_StartsServerImmediatelyAndToolReturnsLoading` (Integration) — B.4 Loading-State
- [ ] `McpServerCommandLoadingStateTests.RunAsync_LoadFuncCompletes_ToolReturnsNormalResult` (Integration) — B.4 Loaded-Übergang
- [ ] `McpCodeGraphServerStalenessMtimeCacheTests.GetCurrentSolution_CalledTwiceWithoutDirChange_SkipsSweepOnSecondCall` (Unit) — B.5 Cache-Hit
- [ ] `McpCodeGraphServerStalenessMtimeCacheTests.GetCurrentSolution_CalledAfterNewFile_TriggersSweepAgain` (Unit) — B.5 Cache-Miss
- [ ] Bestehende `McpLiveRepositoryTests` + `McpServerAllToolsE2ETests` + `McpServerCommandErrorHandlingTests` grün (B.4 + TD-005 dürfen keine Regression in bestehenden Tests verursachen; `McpTestClient` muss Loading-Antworten handhaben)
- [ ] `McpCodeGraphServerStalenessTests` (bestehend) grün (B.5 darf Phase 1 + Phase 3 nicht verändern)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün — 0/0, kein Zero-Warning-Verstoß
- [ ] Test-Command aus Tech-Stack-Notiz grün — Volllauf ohne TD-005-Flake (mind. 2 reproduzierte Läufe)
- [ ] `dotnet test --filter "Category=Unit"` grün (schnelle Iteration)
- [ ] `dotnet test --filter "Category=Integration"` grün inkl. B.3/B.4-Tests
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch, imperativ, Task-Suffix `[codegraph-mcp-finish]`)
- [ ] Doku-Commits für `Docs/agent-api.md` + `Docs/integration.md` + `Docs/ROADMAP.md` (gemäß `spec.md` §10.3 zwei Commits: Code + Doku)
- [ ] `step-010/step-result.md` geschrieben mit: B.3-Mess-Zahlen (1k + 10k LOC, min/median/max Wand-Zeit), B.4-Lade-Dauer (Fixture ohne Hintergrund-Load vs. mit), B.5-Sweep-Counter vorher/nachher (z. B. „2. Call ohne File-Change: 0 Sweep-Iterationen, davor: N Iterationen"), TD-005-Sanity-Beweis (Volllauf ohne Flake)
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt
- [ ] Volllauf-Laufzeit notiert in `step-result.md` (z. B. „vorher ~2:30, nachher ~2:35" — wenn keine signifikante Veränderung, transparent dokumentieren statt zu beschönigen)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1` (monolithisch, statische
  Kompilierung) — `McpCodeGraphServer` bleibt sealed internal, keine
  Plugin-/ALC-/DI-Verletzung; `LoadFunc` ist Func<>-Delegate, kein
  DI-Container.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2` (kein DI, kein Plugin,
  kein ALC) — eingehalten, Func<>-Parameter im `McpCodeGraphServerOptions`
  ist Konfigurations-Schnittstelle, kein DI-Container.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3` (Windows-Shell, Prozess-
  Bereinigung) — vor Build/Test `Get-Process AiNetLinter,testhost` leeren
  (siehe Konzept „Entdeckte Mängel"); `SubprocessConcurrencyGate`-
  Anpassung kann kurzfristig mehr Prozesse gleichzeitig laufen lassen,
  kein neues Aufräumproblem.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` (xUnit v3, Testsuiten-
  Parallelität, MCP via C#-Testinfra, Doku-Update-Pflicht) — neue Tests
  mit `[Trait("Category", "Unit"|"Integration")]`; B.3-Mess-Tests
  ausdrücklich als Integration (teure Fixture-Generierung); drei
  Doku-Updates verpflichtend.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` (Zero-Warning, Result-
  Pattern, **Verbot Task-/Planungsartefakt-Referenzen**, Verbot
  Symptom-Fixing, Verbot leerer catch) — `EnforceNoSilentCatch` im
  neuen `HasSolutionDirChanged` durch expliziten Empty-Body-Default
  (oder Return-False) ersetzen falls nötig; **keine** Kommentare wie
  „// B.5-mtime-Cache" oder „// EPIC-05" im Code; XML-Docs an
  `LoadState`, `Loading()`, `HasSolutionDirChanged` als
  forward-looking Rationale.
- `.agents/rules/AiNetLinter.mdc` (Grenzwerte: `MaxMethodLineCount`
  60/100, `MaxCyclomaticComplexity` 12/15, `MaxAIContextFootprint`
  2500, `MaxMethodOverloads` 5) — `McpCodeGraphServer` darf durch
  B.4 nicht über das AIContextFootprint-Limit wachsen (2-Properties-
  Enum + Delegate-Param entsprechen ~30-50 Zeilen, im aktuellen
  Footprint-Budget); `McpCodeGraphServerRefresh.Run` bekommt einen
  Parameter mehr (5 statt 4) → `MaxMethodParameterCount: 4` greift
  nicht bei 5, aber `MaxMethodParameterCountForNonPublic: 6` ist das
  projektweite Limit für statische Helper (siehe `rules.json:117`
  und `step-009/fix-01` Review), bleibt eingehalten. `EnforceSealedClasses`
  weiter OK (kein neuer Klassen-Typ, nur Enum + Property); `EnforceNullableEnable`
  in allen neuen Dateien Z. 1; `EnforceAsciiIdentifiers` durchgehend
  (LoadState, LoadFunc, HasSolutionDirChanged, SweepCounter).
- `.agents/rules/AiNetLinter.mdc` `EnforceNoSilentCatch` (relevant
  für B.5: `mtime`-Cache-Fehler brauchen Behandlung) — `try/catch (IOException)`
  in `HasSolutionDirChanged` analog dem bestehenden
  `CacheInitialFileState`-Pattern (Z. 161-170) mit `[WARN]`-Emission
  via `_console.WriteError`, kein leerer Catch.
- `.agents/rules/AiNetLinter.mdc` `BanAsyncVoid`, `BanBlockingTaskAccess`
  — `LoadFunc` ist `Func<CancellationToken, Task<...>>`, nicht async-void;
  kein `.Result`/`.Wait()`-Zugriff auf den `_loadTask`.

## Bekannte Ausnahmen

- **TD-005-Reproduktion in Volllauf-Läufen** (historisch 1-2 Failures
  pro Lauf, in 3 step-Reviews dokumentiert): wird in diesem Step aktiv
  angegangen (Gate-Kapazität 4 → 6, expliziter 60s-Timeout). Falls
  nach der Anpassung in extremen Läufen weiterhin ein Flake auftritt,
  als klassischer Last-Flake in `step-result.md` dokumentieren, kein
  Blocker.
- **B.3-Mess-Werte sind Umgebungs-abhängig** (Hardware, gleichzeitige
  Last, Disk-Cache-Zustand). Die Test-Assertions sind bewusst großzügig
  kalibriert (z. B. < 30s für 1k-LOC-Cold-Start), die genauen Zahlen
  werden via `ITestOutputHelper` ausgegeben für Vergleichbarkeit
  zwischen Läufen. Keine harte Performance-Garantie.
- **`McpLiveRepositoryTests`-Patterns** bleiben unverändert: sie starten
  den Server und warten via `McpTestClient.ConnectAsync`-Retry auf
  Server-Bereitschaft. Mit B.4 muss `McpTestClient` den Loading-State
  erkennen (geplant in „Konkrete Änderungen → B.4 → McpTestClient.cs"),
  danach grün. Falls ein bestehender Test den Loading-State nicht
  retryt und in einen Loading-Antwort-Fehler läuft: das ist ein
  Hinweis auf einen vergessenen Retry-Pfad in `McpTestClient`, nicht
  ein Test-Problem — in dem Fall nachbessern, nicht Test lockern.
- **`FindSymbolTool` Zeile 22-23 im XML-Doc** (abgeschnitten, siehe
  step-007/008-Beobachtungen): im selben Zug sanieren gemäß
  `AiNetLinterRichtlinien.mdc` §5 „Aufräumen erlaubt" — betrifft
  diese Datei direkt (Loading-Check kommt dorthin).

## Code-Skizze (optional)

```csharp
// --- B.4: ServerLoadState.cs (NEU) ---
namespace AiNetLinter.Mcp;
public enum ServerLoadState { Loading, Loaded, LoadFailed }

// --- B.4: McpCodeGraphServer.cs (Erweiterung) ---
public ServerLoadState LoadState => _loadTask switch
{
    null => _catalog is null ? ServerLoadState.LoadFailed : ServerLoadState.Loaded,
    { IsCompletedSuccessfully: true } => ServerLoadState.Loaded,
    { IsFaulted: true } or { IsCanceled: true } => ServerLoadState.LoadFailed,
    _ => ServerLoadState.Loading
};

// --- B.4: McpToolResults.cs (Erweiterung) ---
internal static CallToolResult Loading() => new()
{
    IsError = false,
    Content = new List<ContentBlock> { new TextContentBlock
    {
        Text = "[INFO]: Server laedt die Solution noch. " +
               "Bitte in wenigen Sekunden erneut versuchen."
    } }
};

// --- B.5: McpCodeGraphServer.cs (Erweiterung) ---
private DateTime? _lastSolutionDirMtimeUtc;
private bool HasSolutionDirChanged(string? solutionDir)
{
    if (string.IsNullOrEmpty(solutionDir)) return false;
    try
    {
        var current = Directory.GetLastWriteTimeUtc(solutionDir);
        if (_lastSolutionDirMtimeUtc == current) return false;
        _lastSolutionDirMtimeUtc = current;
        return true;
    }
    catch (IOException)
    {
        return true; // defensiv: lieber Sweep als stale Cache
    }
}

// --- B.5: McpCodeGraphServerRefresh.cs (Erweiterung) ---
public static (Solution solution, bool changed) Run(
    Solution current, string? solutionDir,
    Dictionary<string, McpFileState> fileState, Action<string> writeWarn,
    Func<bool> shouldSweep)  // NEU
{
    var updated = current;
    var anyChanged = false;
    var removedIds = RemoveDeletedDocuments(ref updated, solutionDir, fileState, ref anyChanged);
    anyChanged |= SweepForNewFiles(ref updated, solutionDir, fileState, writeWarn, shouldSweep);
    RefreshModifiedDocuments(ref updated, solutionDir, removedIds, fileState, writeWarn, ref anyChanged);
    return (updated, anyChanged);
}

// --- TD-005: SubprocessConcurrencyGate.cs (Erweiterung) ---
private const int MaxConcurrentSubprocesses = 6;
public static async Task<IDisposable> AcquireAsync(CancellationToken ct = default)
{
    await Gate.WaitAsync(ct).WaitAsync(TimeSpan.FromSeconds(60), ct).ConfigureAwait(false);
    return new Lease();
}
```

## Notes

- **Reihenfolge-Disziplin:** B.3 ist vor B.4 vor B.5 umzusetzen. Wenn
  der Coder aus Aufwands-Gründen B.4 zuerst baut (Verlockung, weil
  B.4+Skelett sofort lauffähig ist), wird B.3 nicht gegen echte Zahlen
  validiert und B.5 nur gegen Annahmen. Die Reihenfolge ist nicht
  verhandelbar — die Konzept-Begründung in Z. 224-227 ist explizit.
- **Scope-Disziplin:** EPIC-06 (B.6 stdout-Schutz + B.7 Call-Log) bleibt
  bewusst **außerhalb** dieses Steps. Die Konzept-Vorgabe (Block-B
  Reihenfolge) ist B.3-B.7, aber Nutzer-Wunsch „weniger Mini-Steps"
  wird erfüllt durch *einen* Schritt für die thematisch eng
  zusammenhängenden B.3-B.5 + TD-005, nicht durch *einen* Schritt für
  alle sieben B-Punkte. B.6 + B.7 bleiben eigenständige Schritte.
- **EPIC-05-Sub-Schritt-Grenze:** der Plan adressiert alle drei
  Konzept-Punkte (B.3, B.4, B.5) + TD-005-Sanity, aber **nicht**
  EPIC-07 (TD-001/002/004/006/007, die nicht im Scope sind) und nicht
  EPIC-08 (Symbolgraph-Erweiterungen, Konzept Block E).
- **TD-007-Mitnahme:** in `McpCodeGraphServerOptions.cs:42-46, 62-64`
  ist die „ehemaligen 5 Parameter"-XML-Doc-Sanierung ein „Aufräumen
  erlaubt"-Kandidat (§5), weil diese Datei in B.4 ohnehin angefasst
  wird (neue `LoadFunc`-Property). Im selben Zug mitsanieren,
  TD-007-Eintrag schließen.
- **Doku-Commits:** zwei Commits gemäß `spec.md` §10.3 — ein
  Code-Commit (B.3+B.4+B.5+TD-005+TD-007) und ein Doku-Commit
  (agent-api.md, integration.md, ROADMAP.md). Beide mit
  Task-Suffix `[codegraph-mcp-finish]`.
- **Verifikations-Strategie am Step-Ende:** Volllauf 2× reproduzieren
  (TD-005-Flake-Reproduzierbarkeit war in den letzten 3 Reviews
  schwankend, 2 Läufe geben belastbare Aussage). Falls beide
  Vollläufe TD-005-Flake-frei sind: TD-005 in `step-result.md` als
  „im Rahmen dieses Steps behoben" markieren und `tech-debt.md`-
  Status auf „geschlossen" setzen. Falls ein Flake bleibt:
  dokumentieren, ggf. in einem späteren Schritt nachjustieren.
- **`Bekannte Ausnahmen` vs. `Tech-Debt`:** TD-005 wird mit diesem
  Step geschlossen (nicht als „Ausnahme" geführt), TD-007 wird
  mitgenommen (ebenfalls geschlossen). Andere TD-Einträge (TD-001,
  002, 004, 006) bleiben unverändert offen und sind EPIC-07-Scope.
