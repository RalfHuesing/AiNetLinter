---
unit: 007
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-01
epic: EPIC-07 (Tests-Ausbau) + TD-003 inline
extends:
  - konzept.md Z. 104-107 (EPIC-07 Scope)
  - konzept.md Z. 191-192 (Muss-Haven Tests: Staleness, Integrationstests je Tool, Miss-Hint, Mehrdeutigkeit, Cache-Isolation, CLI-Regression)
  - konzept.md Z. 598-622 (DoD-Kriterien, alle 6 hier geplanten Bereiche)
  - konzept.md Z. 573-588 (Cache-Isolation Konzept: SHA256-basiertes Filename-Pattern)
  - tech-debt.md TD-003 (RegisterMSBuild-Race, Empfehlung 006-Kritiker: "in 007 inline fixen")
  - tech-debt.md TD-011 (SymbolGraphToolRegistrations Footprint, Pflichtmessung pro Einheit)
  - tech-debt.md TD-016 (Fixture-Code-Duplikation, 4 Workspace-Klassen — Out-of-Scope, nur erwähnt)
  - tech-debt.md TD-015 (WarningsSection Dead Code — Out-of-Scope, nur erwähnt)
  - units/004/plan.md (E2E-Fixture-Pattern, neue Test-Datei pro Tool, McpServerCommandFindSymbolTests-Vorbild)
  - units/006/plan.md (Compile-Fehler-Fixture, ConsoleTestCollection-Pattern, McpServerCommandErrorHandlingTests-Vorbild)
  - units/006/review.md (TD-003-Empfehlung des Kritikers: "struktureller Fix sauberer, in 007 inline")
  - AiNetLinterRichtlinien.mdc §1 (Monolithisch & schlank — gilt unverändert)
  - AiNetLinterRichtlinien.mdc §5 (Result-Pattern, sparsame Kommentare, Conventional Commits)
  - AiNetLinter.mdc (AIContextFootprint-Limit 2500, MaxLineCount 500, alle 4 Tool-Tests-Files in 006 ≤ 1450 Z., Footprint-Risiko gering)
---

# Plan Einheit 007 — EPIC-07 Tests-Ausbau + TD-003 Race-Fix

## Ziel der Einheit

Die sechs in `konzept.md` Z. 104-107 und Z. 612-622 explizit als DoD-Pflicht
gelisteten Test-Bereiche werden durch **E2E-Tests gegen den realen
MCP-Subprozess** (analog `McpServerCommandFindSymbolTests.cs` aus 004 und
`McpServerCommandErrorHandlingTests.cs` aus 006) abgesichert, sodass die
DoD-Aussagen nicht nur durch Unit-Tests im Tool-Scanner-Bereich, sondern
**durch den laufenden Server** verifiziert sind. Parallel wird **TD-003
strukturell behoben** (006-Kritiker-Empfehlung): `SourceFileCatalog.
RegisterMSBuild` mit statischem Lock + Check-Lock-Check-Pattern gegen die
Race-Condition bei parallel laufenden Testklassen abgesichert, inkl.
Reproduzier-Test, der den Fix nachweist (A3-Methodik).

**EPIC-07 vollständig erfüllt** nach 007:

- (a) **Integrationstest je Tool** — bereits in `McpServerCommandTests.cs`
  Z. 163-429 für 8/9 Tools vorhanden, Lücke in der konsolidierten Datei
  (500/500 Z. hart am Limit, ein neuer Test passt **nicht** mehr rein →
  neue E2E-Datei pro neuem Bereich).
- (b) **Staleness-Invalidierung E2E** — bisher nur Unit-Test in
  `McpCodeGraphServerTests.cs:35-54`; fehlt: E2E, dass eine Datei-Änderung
  zwischen zwei `find_symbol`-Tool-Calls korrekt propagiert.
- (c) **Miss-Hint komplett E2E** — Unit-Test in
  `FindSymbolToolTests.cs:63-82` (via `FindSymbolScanner`); fehlt: E2E,
  dass ein `find_symbol`-Call nach `userService` (nur in `.js`/`.razor`/
  `.xaml` der `SymbolGraphMini`-Fixture) den expliziten Miss-Hint-Text
  liefert.
- (d) **Mehrdeutigkeits-Abbruch E2E** — Unit-Test in
  `McpServerCommandTests.cs:22-43` (via `ResolveSolutionPathOrError`);
  fehlt: E2E, dass ein Server mit `cwd = Verzeichnis-mit-2-Solutions`
  sauber mit `[ERROR] AMBIGUOUS_SOLUTION` auf stderr abbricht.
- (e) **Cache-Isolation** — kein Test vorhanden. Neu: zwei parallele
  Cache-Loads mit unterschiedlichen Solutions erzeugen unterschiedliche
  Cache-Dateien (SHA256-basiertes Filename-Pattern, Konzept Z. 573-588).
- (f) **CLI-Regression** — `CliIntegrationTests.RunLinterCli_OnWhole
  Solution_ReturnsSuccess` (Z. 14-46) deckt das für die **echte
  AiNetLinter-Solution** ab; **fehlt**: explizite Regression gegen eine
  Mini-Fixture, die nach allen EPIC-01..06-Änderungen den
  CLI-Batch-Modus (`--config rules.json --path <fixture>`) als grün
  verifiziert.

**TD-003-Fix:** `RegisterMSBuild` mit `private static readonly object
_lock = new()` + Check-Lock-Check absichern, inkl. konkurrenter
Reproduzier-Test, der den `InvalidOperationException`-Flake eliminiert
(parallel: 10 Threads rufen `SourceFileCatalog.LoadAsync` gleichzeitig
auf).

## Scope-Entscheidung

**Gewählt: Sechs EPIC-07-Test-Bereiche + TD-003 inline.**

Begründung:

- **EPIC-07 ist die nächste DoD-Pflicht** aus `konzept.md` (Z. 104-107,
  Z. 612-622). Nach 006 (EPIC-06 Robustheit) ist es die letzte große
  P0-Säule vor EPIC-08 (Doku). Die DoD-Punkte sind in `konzept.md`
  wortwörtlich gelistet — kein "Schönheits-Test", sondern harte
  Anforderung.
- **(a) Integrationstest je Tool** ist **bereits weitgehend erfüllt**
  (8/9 Tools in `McpServerCommandTests.cs`), aber die Datei ist mit
  499/500 Z. am Limit. 007 fasst die **neuen** Tests in dedizierten
  E2E-Dateien zusammen (analog 004 + 006), statt die zentrale Datei
  weiter aufzublähen.
- **(b)–(d) sind E2E-Lücken**: die existierenden Unit-Tests beweisen
  das **Verhalten der Scanner-/Helper-Schicht**, nicht das
  **End-to-End-Verhalten** durch den MCP-Server-Subprozess. Genau das
  ist der Unterschied zwischen "Code-Logik korrekt" und "DoD-Kriterium
  erfüllt" — der Server könnte z. B. einen Hint-Text verschlucken
  oder Staleness deaktivieren, ohne dass die Unit-Tests das bemerken.
- **(e) Cache-Isolation** ist die einzige DoD-Aussage, für die **gar
  kein** Test existiert. Konzept Z. 619-621 listet sie explizit als
  Pflicht ("Zwei MCP-Server-Instanzen für unterschiedliche Solutions
  laufen parallel ohne Cache-Datei-Kollision" + "MCP-Server +
  gleichzeitiger CLI-Lint-Lauf auf derselben Solution"). Der
  Unit-Test in `AnalysisCacheManagerTests.cs` deckt nur
  Cache-Schreiben/-Lesen, nicht die Isolations-Eigenschaft.
- **(f) CLI-Regression**: bestehender `CliIntegrationTests.RunLinter
  Cli_OnWholeSolution_ReturnsSuccess` testet die **echte** Solution.
  007 ergänzt eine **Mini-Fixture-Variante**, die nachweist, dass auch
  eine fremde, kleine Solution (SymbolGraphMini) im CLI-Modus 0
  Violations liefert. Das schützt gegen künftige Regressionen, die nur
  bei kleinen Solutions sichtbar werden (z. B. ein Path-Override, der
  nur bei Mini-Fixtures greift).
- **TD-003-Fix** ist die 006-Kritiker-Empfehlung: "Sammlungs-Umgehung
  ausreichend, aber struktureller Fix sauberer". Jede weitere E2E-
  Test-Klasse (insbesondere (d) Mehrdeutigkeit-Subprozess-Start und
  (e) Cache-Isolation) erhöht die parallele Test-Last und damit die
  Race-Wahrscheinlichkeit. **TD-003 muss VOR** diesen neuen
  Subprozess-Tests strukturell gefixt sein, sonst flaken die neuen
  Tests selbst.

**Bewusst NICHT in 007:**

- **Keine** P0/P1-Rest-Erweiterungen (Kaltstart entkoppeln,
  `rules.json`-Auto-Discovery, Staleness-Sweep mit Verzeichnis-`mtime`,
  `--mcp-log` Call-Log, Verzeichnis-Sweep für neu/gelöschte Dateien,
  `ILintConsole` für MCP, Konzept Z. 265-323). Diese sind separate
  Folge-Einheiten (008 oder später) — die P0/P1-Punkte sind im Konzept
  als P0+P1 markiert, aber **nicht** in EPIC-07 (EPIC-07 ist Tests,
  nicht Features).
- **Kein** EPIC-08-Doku (`Docs/agent-api.md`, `Docs/integration.md`,
  `Docs/ROADMAP.md`, `README.md`).
- **Keine** Trunkierungs-Änderungen (alle 4 Listen-Tools fertig in
  002/004/005).
- **Keine** Miss-Hint-Logik-Änderungen (003 abgeschlossen — 007 testet
  nur, ändert nicht).
- **Keine** `--path`-Mehrdeutigkeits-Logik-Änderung (in 001 + 006
  umgesetzt, in `McpServerCommand.ResolveSolutionPathOrError` sauber
  getrennt von `SourceFileCatalog.FindSolutionFile`).
- **Keine** `PathOverrides`-Wert-Erhöhung in `rules.json` (kein Eingriff
  in existierende Tool-Footprints vorgesehen).
- **Keine** Scanner-Splits.
- **Keine** TD-015 (Dead Code `WarningsSection`) / TD-016
  (Fixture-Code-Duplikation) — beide offen, aber separat (nicht in
  007-Scope).
- **Keine** Änderung an `McpCodeGraphServer` (kein Bedarf — die
  Staleness-Logik ist bereits korrekt, fehlt nur der E2E-Test).
- **Kein** Cold-Path: `McpServerCommandTests.cs` 499/500 Z. — alle
  **neuen** E2E-Tests in dedizierten Dateien.

## Vor-der-Planung-Checks (Kernel Teil B "Drift" / "Duplikate durch Blindheit")

### Check 1 — Bestand an Integrationstests pro Tool (Lücken-Audit, gemessen 2026-08-01)

**Befund (gelesen, alle `McpServerCommand*Tests.cs`/`McpServerCommandTests.cs`):**

| Tool | E2E-Datei / -Methode | Status |
|---|---|---|
| `find_symbol` | `McpServerCommandTests.cs:RunAsync_ValidFixture_FindSymbolReturnsMatch` (Z. 272-296) | ✓ |
| `find_references` | `McpServerCommandTests.cs:RunAsync_ValidFixture_FindReferencesReturnsCallSite` (Z. 298-322) | ✓ |
| `get_impact` | `McpServerCommandTests.cs:RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite` (Z. 324-349) + `_WithoutGitRefUncommittedReturnsCallSite` (Z. 351-376) | ✓ |
| `get_file_skeleton` | `McpServerCommandTests.cs:RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature` (Z. 378-402) | ✓ |
| `get_type_hierarchy` | `McpServerCommandTests.cs:RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy` (Z. 404-429) | ✓ |
| `get_index_scope` | `McpServerCommandTests.cs:RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown` (Z. 189-215) | ✓ |
| `get_hotspots` | `McpServerCommandTests.cs:RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture` (Z. 163-187) | ✓ |
| `get_violations` | `McpServerCommandTests.cs:RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation` (Z. 217-242) | ✓ |
| `search_pattern` | `McpServerCommandTests.cs:RunAsync_ValidFixture_SearchPatternReturnsExpectedHit` (Z. 244-270) | ✓ |
| **Tool-Set-Discovery** | `McpServerCommandTests.cs:RunAsync_ValidFixture_ServerRespondsWithNineTools` (Z. 133-161) | ✓ |
| **Trunkierung** (003/004/005) | `McpServerCommandFindSymbolTests.cs:RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates` (Z. 23-48) | ✓ |

**Erkenntnisse:**

- **9/9 Tools haben bereits E2E-Tests** in der zentralen Datei. DoD-
  Kriterium "ein Integrationstest je Tool" (Konzept Z. 598-601) ist
  **bereits erfüllt** — keine Lücke.
- **8 Tests** in `McpServerCommandTests.cs` (Z. 1-499) plus
  **4 dedizierte E2E-Dateien** (`McpServerCommandFindSymbolTests.cs`/
  `McpServerCommandFindReferencesTests.cs`/
  `McpServerCommandGetImpactTests.cs`/
  `McpServerCommandErrorHandlingTests.cs`). Muster "neue E2E-Datei pro
  Bereich" etabliert.
- **Zentrale Datei `McpServerCommandTests.cs` ist 499/500 Z.** — 1 Zeile
  Puffer zum `MaxLineCount`-Limit. **Keine** neuen Tests in dieser
  Datei. Neue E2E-Tests in dedizierten Dateien (analog 004/006).

**Entscheidung im Plan:**

- (a) **Bereich (a) = "abgeschlossen"** markieren in `result.md`. Kein
  Coder-Eingriff nötig, nur Dokumentation, dass die Lücke nicht
  existiert. Spart Aufwand vs. "auf Vorrat einen weiteren Test
  hinzufügen".
- (b)–(f) E2E-Tests in **neuen dedizierten Dateien** (analog
  004/006-Muster).

### Check 2 — Staleness-Validierungs-Pfad im E2E

**Befund (gelesen, `src/AiNetLinter/Mcp/McpCodeGraphServer.cs:120-181`):**

- `RefreshStaleDocuments()` wird bei jedem `GetCurrentSolution()`-Aufruf
  aufgerufen (Z. 77-86). Iteriert über alle Projekte/Dokumente, prüft
  `mtime` (Z. 146-150), bei Abweichung `TryApplyContentChange` (Z.
  155-181) mit `IOException`-Fallback.
- `InitializeFileState` (Z. 90-102) hasht beim Server-Start alle
  initialen Dateien, füllt `_fileState`.
- **Test-Infrastruktur:** `McpCodeGraphServerTests.cs:35-54` deckt
  `FileModifiedOnDisk_ReflectsNewContent` bereits als **Unit-Test**
  ab. `GetCurrentSolution_FileDeletedOnDisk_DoesNotThrow` (Z. 71-85)
  und `GetCurrentSolution_ConcurrentCalls_DoNotThrow` (Z. 87-116)
  ebenfalls.

**Lücke:** Kein E2E-Test, der nachweist, dass der **MCP-Server**
(Datei-Änderung auf Disk) → (nächster Tool-Call liefert neuen Inhalt)
**propagiert**. Unit-Test beweist nur, dass `GetCurrentSolution` die
`Solution` aktualisiert — was der Server tut, ist ein zusätzlicher
Schritt (Tool delegiert an `GetCurrentSolution`, das ist korrekt für
alle 9 Tools, aber nicht explizit E2E verifiziert).

**Test-Design:**

- **Fixture:** `SymbolGraphMiniFixtureWorkspace` (existiert, hat
  `Caller.cs` als mutable Datei).
- **Schritt 1:** MCP-Server starten (analog `McpServerCommandFind
  SymbolTests.cs:30-37`).
- **Schritt 2:** `find_symbol`-Call für `Caller` → liefert
  `Caller.cs`-Treffer (analog existierender Test).
- **Schritt 3:** `Caller.cs` mit neuem Inhalt überschreiben
  (z. B. `public class CallerRenamedXyz { }`), `mtime` auf
  `+2s` setzen (analog `McpCodeGraphServerTests.cs:45`).
- **Schritt 4:** Zweiter `find_symbol`-Call für `CallerRenamedXyz` →
  muss den neu hinzugefügten Symbolnamen finden.
- **Assertion:** Output enthält `CallerRenamedXyz` UND
  `Caller.cs:NN` (Datei-Position).

**Schwierigkeit:** E2E-Tests laufen in einem **Subprozess** (siehe
`McpServerCommandFindSymbolTests.cs:30-37` `StdioClientTransport`).
Datei-Änderungen im Test-Fixture-Verzeichnis sind aus dem
**Test-Prozess** heraus sichtbar, der Subprozess sieht sie auch (selbe
Filesystem). Es gibt keine Sandbox-Trennung, die das verhindern würde.
**Machbar.**

**Entscheidung im Plan:**

- **E2E-Staleness-Test** in neuer Datei
  `McpServerCommandStalenessTests.cs` (analog 004/006-Pattern mit
  `ConsoleTestCollection`).
- **Genau 1 Test** (A3: ein Test reicht, um die Staleness-Propagierung
  durch den Server zu beweisen — der Mechanismus selbst ist
  Unit-getestet).

### Check 3 — Miss-Hint-Pfad im E2E

**Befund:**

- Unit-Test `FindSymbolToolTests.cs:63-82` deckt
  `FindMatchesAndFormat_NoCsMatchButNonCsHit_ReturnsMissHintWith
  FileList` ab. `userService` (kommt in `site.js`, `Component.razor`,
  `Page.xaml` der `SymbolGraphMini`-Fixture vor) liefert:
  - `"Keine Treffer fuer 'userService'"` (C#-Leermenge)
  - `"Hinweis: kein C#-Symbol, aber Textfund"` (Miss-Hint-Markierung)
  - `"site.js"`, `"Component.razor"`, `"Page.xaml"` (Datei-Liste)
  - `"search_pattern"` (Fallback-Verweis)
- **Lücke:** kein **E2E**-Test, der nachweist, dass der MCP-Server
  diese Antwort **durch den stdio-Transport** an einen Client liefert.
  Im Prinzip könnte ein Wire-Encoding-Fehler (z. B. ein Bug in der
  TextContent-Block-Serialisierung) den Hint verschlucken — das
  fängt der Unit-Test nicht.

**Test-Design:**

- **Fixture:** `SymbolGraphMiniFixtureWorkspace` (existiert, hat die
  drei Non-C#-Dateien mit `userService`-Token).
- **Schritt 1:** MCP-Server starten.
- **Schritt 2:** `find_symbol`-Call für `userService` (kein C#-Symbol).
- **Assertion:** Output enthält `"Keine Treffer fuer 'userService'"`,
  `"Hinweis: kein C#-Symbol"`, `"site.js"`, `"Component.razor"`,
  `"Page.xaml"`, `"search_pattern"`.

**Entscheidung im Plan:**

- **E2E-Miss-Hint-Test** in neuer Datei
  `McpServerCommandMissHintTests.cs` (analog 004/006-Pattern).
- **Genau 1 Test** (analog Check 2: A3 reicht für
  Wire-Encoding-Propagierung).

### Check 4 — Mehrdeutigkeits-Abbruch im E2E

**Befund (gelesen, `src/AiNetLinter/Commands/McpServerCommand.cs:87-135`):**

- `ResolveSolutionPathOrError(targetPath, console)` wird in `RunAsync`
  Z. 32-33 aufgerufen, **bevor** `TryLoadSolutionAsync` läuft.
- `FindSolutionCandidates` (Z. 110-116) listet `.slnx` + `.sln`
  alphabetisch sortiert. Bei `Count > 1` → `ReportAmbiguousSolution`
  (Z. 127-135) → schreibt `AMBIGUOUS_SOLUTION`-Fehler auf
  `ILintConsole.WriteError` (typischerweise `Console.Error`).
- **Unit-Test:** `McpServerCommandTests.cs:22-43` deckt das über
  `ResolveSolutionPathOrError` direkt ab (Z. 31: `McpServerCommand.
  ResolveSolutionPathOrError(tempDir, console)`).
- **Lücke:** kein **E2E**-Test, der nachweist, dass der
  **Server-Subprozess** sauber mit Exit-Code ≠ 0 abbricht und
  `AMBIGUOUS_SOLUTION` auf stderr schreibt, wenn das `cwd` ein
  Verzeichnis mit 2 Solutions ist (bzw. `--path` auf ein solches
  Verzeichnis zeigt).

**Wichtige Designentscheidung — Verzeichnis-Logik:**

- Die **MCP-Variante** (Konzept Z. 124-138) ruft
  `ResolveSolutionPathOrError` auf, BEVOR der Transport startet.
  Konsequenz: bei Mehrdeutigkeit → Server **startet gar nicht erst**,
  `RunAsync` returnt 1 (Z. 33: `if (solutionPath is null) return 1;`).
  Kein `McpClient`-Connect möglich, der Server-Process beendet sich
  sofort.
- **Test-Design:** Subprozess **ohne** `McpClient` starten, auf
  Exit warten, `stderr` lesen, Exit-Code assertieren.

**Test-Design (E2E, Subprozess-Start ohne MCP-Client):**

- **Schritt 1:** Temp-Verzeichnis anlegen, 2 `.slnx`-Dateien
  hineinkopieren.
- **Schritt 2:** `Process.Start(AiNetLinter.exe, "--mcp-server --
  path <tempDir>")`, `RedirectStandardError = true`, auf Exit warten.
- **Assertion:** Exit-Code ≠ 0, `stderr` enthält `AMBIGUOUS_SOLUTION`
  und beide Dateinamen.

**Entscheidung im Plan:**

- **E2E-Mehrdeutigkeit-Test** in neuer Datei
  `McpServerCommandAmbiguityE2ETests.cs` (analog 006
  `McpServerCommandErrorHandlingTests.cs`).
- **Genau 1 Test** (analog 006-Pattern).
- **Vorbild:** `McpServerCommandTests.cs:22-43` für die
  Datei-Erstellung; **Abweichung:** statt `ResolveSolutionPathOrError`
  direkt aufzurufen, wird der **echte Subprozess** gestartet (analog
  `McpServerCommandErrorHandlingTests.cs:36-45`).

### Check 5 — Cache-Isolation

**Befund (gelesen, `src/AiNetLinter/Cache/AnalysisCacheManager.cs:90-97`):**

```csharp
private static string BuildCacheFilePrefix(string solutionPath, string rulesJsonContent)
{
    var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
    var hashInput = solutionPath.ToLowerInvariant() + rulesJsonContent;
    var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
    var hash8 = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
    return $"{solutionName}-{hash8}";
}
```

- Cache-Filename = `{solutionName}-{SHA256(solutionPath + rulesJson)
  [..8]}-{buildTimestamp}.json`.
- **Konzept Z. 619-621:** "Zwei MCP-Server-Instanzen für
  unterschiedliche Solutions laufen parallel ohne Cache-Datei-
  Kollision" + "MCP-Server + gleichzeitiger CLI-Lint-Lauf auf
  derselben Solution laufen ohne Cache-Datei-Konflikt".
- **Wichtige Konzeptklarstellung Z. 175-183:** "get_violations
  umgeht den bestehenden Disk-Cache" → im MCP-Modus entsteht
  **kein** Cache-File durch den Server. Die Isolations-Aussage für
  MCP-Instanzen untereinander ist also trivial wahr (kein File).
- **Die echte Isolations-Pflicht ist CLI ↔ MCP:** wenn der CLI-Lauf
  eine Cache-Datei schreibt und der MCP-Server sie **nicht** liest/
  schreibt (Bypass), gibt es **keine** Kollision.
- **Sekundäre Isolations-Pflicht:** Zwei CLI-Läufe auf
  unterschiedlichen Solutions → unterschiedliche Cache-Files. Das
  ist die echte `AnalysisCacheManager`-Pflicht, abgesichert durch
  das SHA256-Pattern.

**Lücke:** Kein Test verifiziert:

- (i) `AnalysisCacheManager.Load(exeDir, solA, rules, ttl)` und
  `Load(exeDir, solB, rules, ttl)` erzeugen **unterschiedliche**
  Cache-Filenamen.
- (ii) Zwei CLI-Prozesse auf **derselben** Solution teilen sich
  denselben Cache-Filenamen (gewollt, Konzept Z. 586-588).
- (iii) Ein CLI-Lauf + ein MCP-Server auf derselben Solution
  kollidieren **nicht** (MCP schreibt keine Datei, liest sie
  auch nicht).

**Test-Design:**

- **Test (i) Cache-Filename-Isolation:** Unit-Test in
  `AnalysisCacheManagerTests.cs` (oder neuer
  `AnalysisCacheManagerIsolationTests.cs`). Zwei
  `AnalysisCacheManager.Load`-Aufrufe mit unterschiedlichen
  Solution-Pfaden (z. B. `c:\temp\SolutionA.slnx` vs.
  `c:\temp\SolutionB.slnx`), identischen Rules, identischem
  `exeDir` → Cache-Filenamen **müssen** sich im
  Hash-Teil unterscheiden. Assertions auf den zurückgegebenen
  `AnalysisCacheManager`-internen Cache-Pfad (über `Internals
  VisibleTo` oder neue `internal`-Property `CachePath`).
- **Test (ii) Cache-Filename-Gleichheit:** Unit-Test analog,
  aber gleicher Solution-Pfad → identischer Hash-Teil, nur
  Timestamp kann variieren.
- **Test (iii) MCP-Disk-Cache-Bypass:** Unit-Test in neuem
  `McpServerCommandCacheBypassTests.cs`. Subprozess
  `--mcp-server --path <fixture>` starten, einen
  `get_violations`-Call absetzen, parallel einen CLI-Lauf
  `dotnet AiNetLinter.dll --config rules.json --path
  <fixture>` ausführen, **danach** das `cache/`-Verzeichnis
  prüfen: nur **ein** Cache-File (das vom CLI-Lauf), nicht
  zwei. **Vorsicht:** dieser Test erfordert `AiNetLinter.exe`
  + `AiNetLinter.dll`-Koordination, ist komplexer als (i)/(ii).
  **Alternative:** Unit-Test, der nachweist, dass
  `McpCodeGraphServer` **keine** `AnalysisCacheManager`-Referenz
  hat (Reflection-Test) und `get_violations` über die
  resident gehaltene `Compilation` läuft (nicht über Cache).

**Entscheidung im Plan:**

- (i) und (ii) in **neuer Datei**
  `AnalysisCacheManagerIsolationTests.cs` (3-4 Tests, einer
  pro Pflicht-Aspekt).
- (iii) als **1 Test** in neuer Datei
  `McpServerCommandCacheBypassTests.cs` (E2E, mit Cache-File-
  Anzahl-Count).
- **Insgesamt 4-5 neue Tests** für Cache-Isolation.

### Check 6 — CLI-Regression

**Befund (gelesen, `src/AiNetLinter.Tests/Cli/CliIntegrationTests.cs:14-46`):**

```csharp
[Fact]
public void RunLinterCli_OnWholeSolution_ReturnsSuccess()
{
    // Startet dotnet mit AiNetLinter.dll --config rules.json --path <solutionRoot>
    // Erwartet Exit-Code 0, Output enthält "OK".
}
```

- **Bestehender Test:** E2E CLI-Regression gegen die **echte**
  AiNetLinter-Solution. Deckt `--config rules.json --path .` ab.
- **Lücke:** kein Test gegen eine **Mini-Fixture** (z. B.
  `SymbolGraphMini` oder `BaselineMini`). Vorteil einer
  Mini-Fixture: schneller, deterministischer, kein Aufräumen
  nach Test nötig (Fixtures sind versioniert und idempotent).
- **Zusätzliche Lücke:** kein Test, der sicherstellt, dass die
  CLI-Optionen `--map`, `--impact`, `--hotspots`,
  `--sync-agent-rules-only` weiterhin funktionieren — diese
  sind aber durch andere Tests bereits abgedeckt
  (`SyncAgentRulesCommandTests`, `AuditCommandTests` etc.) und
  nicht EPIC-07-spezifisch.

**Test-Design:**

- **1 neuer Test in `CliIntegrationTests.cs`** (353/500 Z., 147
  Z. Puffer, Platz da) ODER in neuer Datei
  `CliBatchRegressionTests.cs` (analog 006-Pattern, saubere
  Trennung).
- **Test:** `dotnet AiNetLinter.dll --config <tempRules.json> --
  path <symbolGraphMiniFixture>` → Exit-Code 0, Output enthält
  mindestens 1 Verstoß (deterministische
  `ViolationTrigger.cs`-Verletzung in `SymbolGraphMini`).

**Entscheidung im Plan:**

- **Neuer Test** in neuer Datei
  `CliBatchRegressionTests.cs` (Konsistenz mit 004/006-Muster:
  neue Bereiche in neue Datei).
- **Genau 1 Test** (analog Check 2/3: A3 reicht).

### Check 7 — TD-003 Race-Fix (`SourceFileCatalog.RegisterMSBuild`)

**Befund (gelesen, `src/AiNetLinter/Baseline/SourceFileCatalog.cs:223-246`):**

```csharp
private static void RegisterMSBuild()
{
    if (!MSBuildLocator.IsRegistered)
    {
        BuildHostPatcher.PatchBuildHostForVs2026();
        try
        {
            MSBuildLocator.RegisterDefaults();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN]: ...");
            MSBuildLocator.RegisterDefaults();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", null);
            ...
        }
    }
}
```

- **Race:** `if (!MSBuildLocator.IsRegistered)` ist
  **Check-then-Act** ohne Lock. Bei parallelen Aufrufen
  (z. B. zwei Test-Klassen, die gleichzeitig
  `SourceFileCatalog.LoadAsync` erstmalig aufrufen) kann
  Thread A die `IsRegistered`-Prüfung passieren, Thread B
  ebenso, beide rufen `RegisterDefaults()` auf → zweite
  Aufruf wirft `InvalidOperationException` ("MSBuildLocator
  wurde bereits registriert").
- **Workaround 006:** alle 006-Tests in
  `[Collection("ConsoleTestCollection")]` serialisiert. Aber:
  jede weitere E2E-Test-Klasse in 007 (Staleness, Miss-Hint,
  Cache-Isolation) startet einen **frischen Subprozess** mit
  eigenem Prozess-State — die Serialisierung greift **nur
  innerhalb** des Test-Prozesses, nicht über Prozessgrenzen.
  Zwischen zwei Subprozessen gibt es keine Race (separate
  Prozesse, separate `MSBuildLocator`-Instanzen). **Aber:**
  der **TD-003-fix** ist trotzdem strukturell sauberer, weil
  innerhalb EINES Test-Prozesses die parallele Test-Last mit
  007 weiter wächst.

**Fix-Design (Check-Lock-Check-Pattern):**

```csharp
private static readonly object _msbuildLock = new();

private static void RegisterMSBuild()
{
    if (MSBuildLocator.IsRegistered) return;  // Fast-Pfad: kein Lock

    lock (_msbuildLock)
    {
        if (MSBuildLocator.IsRegistered) return;  // Double-Check: Thread B sieht
                                                  // Thread A's Registrierung

        BuildHostPatcher.PatchBuildHostForVs2026();
        try
        {
            MSBuildLocator.RegisterDefaults();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN]: Error during MSBuild registration: {ex.Message}");
            MSBuildLocator.RegisterDefaults();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", null);
            Environment.SetEnvironmentVariable("MSBuildExtensionsPath", null);
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", null);
        }
    }
}
```

- **Statischer Lock** (`private static readonly object _msbuildLock`)
  prozessweit, weil `MSBuildLocator` ein **prozessglobaler** State
  ist.
- **Doppelter Check** (Fast-Pfad ohne Lock + Re-Check unter Lock)
  vermeidet Lock-Contention im Normalfall (99 % der Aufrufe nach
  dem ersten sind Fast-Pfad).
- **Kein** neuer `try/catch` nötig, das bestehende `try/catch`+
  `finally` bleibt unverändert.

**A3-Nachweis (Reproduzier-Test):**

- **Vor dem Fix:** Test schreiben, der **20 Threads** parallel
  `SourceFileCatalog.LoadAsync` aufruft (mit unterschiedlichen
  Fixture-Pfaden, damit MSBuildWorkspace nicht den gleichen State
  hat). Test wirft mit hoher Wahrscheinlichkeit
  `InvalidOperationException` (TD-003-Befund).
- **Nach dem Fix:** Test grün (alle 20 Calls returnen
  `SourceFileCatalog` ohne Exception).

**Schwierigkeit:** Der 006-Kritiker schreibt, dass die Race
"intermittierend" ist — der Test ist also möglicherweise flaky
**vor** dem Fix (gut für A3) und **stabil** nach dem Fix. Das
ist genau die richtige A3-Charakteristik: ein Fix muss einen
beobachtbaren Bug beheben, kein "Test, der immer grün ist".

**Test-Datei:** `src/AiNetLinter.Tests/Baseline/
SourceFileCatalogRegisterMSBuildTests.cs` (NEU, thematisch
fokussiert). Konsistent mit
`SourceFileCatalogTests.cs`-Existenz (gleiche Directory).

**Entscheidung im Plan:**

- **SourceFileCatalog.cs** +6-8 Z. (Lock-Feld + 2 Check-Zeilen
  + Lock-Block-Öffnung). Footprint-Effekt vernachlässigbar
  (Klasse ist nicht in 2500-Nähe).
- **Neue Test-Datei** mit 2 Tests: einer, der die
  Parallelität ohne Exception nachweist (positiv), einer,
  der verifiziert, dass die Registration idempotent ist
  (zweiter Aufruf returnt sofort ohne erneute
  Registration).

### Check 8 — Footprint-Lage vor 007 (TD-011-Pflicht, gemessen 2026-08-01)

**Befund (gemessen via `wc -l` und Stand 006-Plan-Check 6, leicht
verändert seit 006 um McpCompileDiagnostics-Eingriff):**

| Klasse | Z. | Limit | Puffer | TD-Status |
|---|---:|---:|---:|---|
| `FindSymbolTool` | 2491 | 2700 (PathOverride) | 209 | TD-008-Schutz |
| `FindReferencesTool` | 2522 | 2700 (PathOverride) | 178 | TD-008-Schutz |
| `GetImpactTool` | 2495 | 2500 | **5** ⚠ | TD-011-Knappheit |
| `GetTypeHierarchyTool` | ~1490 | 2500 | — | unverändert |
| `GetFileSkeletonTool` | ~1000 | 2500 | — | unverändert |
| `GetIndexScopeTool` | ~900 | 2500 | — | unverändert |
| `GetHotspotsTool` | ~990 | 2500 | — | unverändert |
| `GetViolationsTool` | ~1450 | 2500 | — | unverändert |
| `SearchPatternTool` | 2482 | 2500 | 18 | TD-010 |
| `SymbolGraphToolRegistrations` | 2494 | 2500 | **6** ⚠ | TD-011 versschärft |
| `McpServerOptionsFactory` | 2484 | 2500 | 16 | TD-014 |
| `McpToolResults` | ~110 | 2500 | — | unverändert |
| `McpCompileDiagnostics` | ~80 | 2500 | — | NEU in 006 |
| `McpCodeGraphServer` | 184 | 2500 | — | unverändert |
| `SourceFileCatalog` | 293 | 500 | — | **+6-8 in 007** (Lock-Feld + Check-Lock-Check) |
| `McpServerCommandTests.cs` | 499 | 500 | **1** ⚠ | keine neuen Tests in dieser Datei |
| `McpServerCommandStalenessTests.cs` (NEU) | n/a | 500 | — | wird in 007 angelegt |
| `McpServerCommandMissHintTests.cs` (NEU) | n/a | 500 | — | wird in 007 angelegt |
| `McpServerCommandAmbiguityE2ETests.cs` (NEU) | n/a | 500 | — | wird in 007 angelegt |
| `McpServerCommandCacheBypassTests.cs` (NEU) | n/a | 500 | — | wird in 007 angelegt |
| `AnalysisCacheManagerIsolationTests.cs` (NEU) | n/a | 500 | — | wird in 007 angelegt |
| `CliBatchRegressionTests.cs` (NEU) | n/a | 500 | — | wird in 007 angelegt |
| `SourceFileCatalogRegisterMSBuildTests.cs` (NEU) | n/a | 500 | — | wird in 007 angelegt |

**007-Eingriffspunkte (Schätzung):**

- **`SourceFileCatalog.cs` (+6-8 Z.):** TD-003-Lock-Feld +
  Check-Lock-Check + Lock-Block-Öffnung. Klasse ist 293/500 Z.,
  bleibt weit unter `MaxLineCount: 500`.
- **Keine** Footprint-Änderungen an Tool-Klassen oder
  Registrar-Klassen. **Kein** `PathOverrides`-Eingriff nötig.
- **Neue Test-Dateien:** 7 Stück (siehe Tabelle), alle voraussichtlich
  < 100 Z. (je 1-3 Tests, A3-Methodik pro Test).

**Entscheidung im Plan:**

- (a) **Pflichtmessung** vor und nach allen 007-Coder-Schritten in
  `result.md` Abschnitt "Footprint-Baseline" (analog 005/006-Pattern).
- (b) **`McpServerCommandTests.cs` bleibt unangetastet** (1 Z.
  Puffer, keine neuen Tests hinzu — disziplinierter Lücken-Bericht
  in `result.md`).
- (c) **Falls `GetImpactTool` > 2500 nach 007** (unwahrscheinlich,
  kein Eingriff in `GetImpactTool`): Pflichtmeldung in `result.md`,
  ggf. Description-Kürzung in 008.

## Konkretes Vorgehen (Schritt-für-Schritt für den Coder)

### Schritt 0 — Pre-Build-Check + Footprint-Baseline (gemessen)

Vor jeder Code-Änderung:

1. `dotnet build AiNetLinter.slnx` — muss grün sein.
2. `dotnet test AiNetLinter.slnx --no-build` — muss grün sein
   (Baseline 1127/1127, A3).
3. Footprint-Messung pro betroffener Klasse:
   - `SourceFileCatalog.cs` (Z. 293) — **wird** sich um +6-8 Z.
     erhöhen.
   - Alle Tool-Klassen + Registrar-Klassen (siehe Check 8).
4. Stand in `result.md` Abschnitt "Footprint-Baseline" eintragen.

**Erwartetes Ergebnis:** Build grün, Tests grün (1127/1127),
Footprints wie in Check 8 dokumentiert.

### Schritt 1 — TD-003 Fix: `SourceFileCatalog.RegisterMSBuild` mit Lock absichern

**Datei:** `src/AiNetLinter/Baseline/SourceFileCatalog.cs`

**Änderung (innerhalb der Klasse, vor `RegisterMSBuild`):**

```csharp
// TD-003: Statischer Lock + Check-Lock-Check-Pattern gegen Race
// bei parallel laufenden Test-Klassen, die SourceFileCatalog.LoadAsync
// erstmalig aufrufen. MSBuildLocator ist prozessglobal.
private static readonly object _msbuildRegistrationLock = new();
```

**Änderung in `RegisterMSBuild` (Z. 223-246):**

```csharp
private static void RegisterMSBuild()
{
    if (MSBuildLocator.IsRegistered) return;  // Fast-Pfad

    lock (_msbuildRegistrationLock)
    {
        if (MSBuildLocator.IsRegistered) return;  // Double-Check

        BuildHostPatcher.PatchBuildHostForVs2026();
        try
        {
            MSBuildLocator.RegisterDefaults();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN]: Error during MSBuild registration: {ex.Message}");
            MSBuildLocator.RegisterDefaults();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", null);
            Environment.SetEnvironmentVariable("MSBuildExtensionsPath", null);
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", null);
        }
    }
}
```

**A3-Methodik (zwingend):**

1. **Test-Datei `src/AiNetLinter.Tests/Baseline/SourceFileCatalogRegisterMSBuildTests.cs`
   (NEU)** mit folgenden Tests:
   - `LoadAsync_TwentyParallelCalls_NoRaceException`: 20 Tasks, die
     gleichzeitig `SourceFileCatalog.LoadAsync` auf
     unterschiedliche Mini-Fixture-Pfade aufrufen. **Vor dem Fix:**
     Test schlägt mit `InvalidOperationException` fehl
     (TD-003-Befund). **Nach dem Fix:** grün.
   - `RegisterMSBuild_TwiceIdempotent_DoesNotRegisterTwice`:
     Verifiziert, dass der zweite Aufruf von `LoadAsync` die
     `BuildHostPatcher`/`Environment.SetEnvironmentVariable`-Calls
     **nicht** erneut ausführt (über Console-Capture oder
     Marker-File in `BuildHostPatcher` — **Coder prüft**, ob
     `BuildHostPatcher` testbar ist; falls nicht, ggf. nur
     Test (i)).
2. **Vor dem Fix:** Test schreiben, **ausführen**, Failure-Output
   wortwörtlich in `result.md` Abschnitt "TD-003 A3-Nachweis"
   dokumentieren.
3. **Fix anwenden** (siehe oben).
4. **Nach dem Fix:** Test grün, in `result.md` dokumentieren.

**Footprint-Effekt:** `SourceFileCatalog.cs` 293 → 299-301 Z.
(+6-8). Bleibt unter `MaxLineCount: 500` und unter jedem
`AIContextFootprint`-Limit (Klasse ist nicht in 2500-Nähe, +
Lock-Feld ist 1 Zeile, Lock-Block-Aufbau 4 Zeilen).

### Schritt 2 — E2E-Staleness-Test in neuer Datei

**Datei (NEU):** `src/AiNetLinter.Tests/Commands/McpServerCommandStalenessTests.cs`

**Inhalt (~50-70 Z.):**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// E2E-Test fuer EPIC-07 Staleness-Invalidierung: eine Datei-Aenderung
/// auf Disk zwischen zwei Tool-Calls muss beim naechsten betroffenen
/// Call korrekt erkannt werden. Unit-Tests in
/// <c>McpCodeGraphServerTests.cs</c> beweisen die Scanner-Logik; dieser
/// Test beweist die Wire-Propagierung durch den realen MCP-Subprozess.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandStalenessTests
{
    [Fact]
    public async Task RunAsync_ValidFixture_FileChangeBetweenCalls_ReflectedInSecondCall()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        Assert.True(File.Exists(exePath));

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ainetlinter-mcp-staleness-test-client",
            Command = exePath,
            Arguments = ["--mcp-server", "--path", fixture.RootPath],
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);

        // 1) Initialer Call: find_symbol fuer "CallerRenamedXyz" muss leer sein.
        var initialResult = await client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "CallerRenamedXyz" },
            cancellationToken: cts.Token);
        Assert.NotEqual(true, initialResult.IsError);
        var initialText = Assert.IsType<TextContentBlock>(Assert.Single(initialResult.Content)).Text;
        Assert.Contains("Keine Treffer fuer 'CallerRenamedXyz'", initialText);

        // 2) Datei aendern: Caller.cs umbenennen in CallerRenamedXyz.
        var callerPath = Path.Combine(fixture.RootPath, "src", "SymbolGraphMini", "Caller.cs");
        var original = File.ReadAllText(callerPath);
        File.WriteAllText(callerPath, original + "\npublic class CallerRenamedXyz { }");
        File.SetLastWriteTimeUtc(callerPath, DateTime.UtcNow.AddSeconds(2));

        // 3) Zweiter Call: muss die neue Klasse finden.
        var updatedResult = await client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "CallerRenamedXyz" },
            cancellationToken: cts.Token);
        Assert.NotEqual(true, updatedResult.IsError);
        var updatedText = Assert.IsType<TextContentBlock>(Assert.Single(updatedResult.Content)).Text;
        Assert.Contains("CallerRenamedXyz", updatedText);
        Assert.Contains("Caller.cs", updatedText);
    }
}
```

**Vor dem Fix:** Test schreiben. **Schritt 1 muss zuerst fertig
sein**, sonst flakt der Test ggf. durch TD-003. **Nach Schritt 1
fertig:** Test grün (TD-003-Fix neutralisiert die Race, der
Staleness-Pfad selbst ist in 002/006/001 schon korrekt).

**A3-Methodik:** Test ist die E2E-Propagierung; das **echte**
Staleness-Verhalten ist Unit-getestet. Hier geht es um den
Nachweis, dass der Server die Propagierung nicht **verschluckt**.
Falls in `McpCodeGraphServer.RefreshStaleDocuments` ein Bug
eingeführt würde, der `RefreshStaleDocuments` no-op't, würde
dieser E2E-Test fehlschlagen. **Test schlägt fehl, wenn der
Staleness-Pfad disabled ist** — A3 erfüllt.

**Schwierigkeit / offene Frage:** `Caller.cs` hat in
`SymbolGraphMini` schon eine `Caller`-Klasse. Der Staleness-
Mechanismus hasht beim Start und prüft `mtime`. Wenn der
erste Tool-Call `GetCurrentSolution` aufruft, wird die Datei
initial-gehasht. Beim **Schreiben** (Schritt 2) ändert sich
`mtime`. Beim **zweiten** Tool-Call wird `mtime`-Check
`TryRefreshDocument` triggern, der `WithDocumentText` aufruft.
Symbol-Suche für `CallerRenamedXyz` matcht die neue Klasse.
**Sollte funktionieren.** Falls nicht, **Coder prüft** mit
`rg` auf dem Fixture-Pfad, ob die Erweiterung sichtbar ist.

### Schritt 3 — E2E-Miss-Hint-Test in neuer Datei

**Datei (NEU):** `src/AiNetLinter.Tests/Commands/McpServerCommandMissHintTests.cs`

**Inhalt (~50-70 Z.):**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// E2E-Test fuer EPIC-07 Miss-Hint-Vollstaendigkeit (Konzept Z. 612-615):
/// Eine Anfrage nach einem Namen, der nur in .js/.razor/.xaml vorkommt,
/// liefert die explizite Miss-Hint-Meldung statt einer stillen Leermenge.
/// Unit-Test in <c>FindSymbolToolTests.cs:63-82</c> beweist die Scanner-
/// Logik; dieser Test beweist die Wire-Propagierung.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandMissHintTests
{
    [Fact]
    public async Task RunAsync_ValidFixture_NonCsOnlyMatch_ReturnsExplicitMissHint()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        Assert.True(File.Exists(exePath));

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ainetlinter-mcp-miss-hint-test-client",
            Command = exePath,
            Arguments = ["--mcp-server", "--path", fixture.RootPath],
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);

        var result = await client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "userService" },
            cancellationToken: cts.Token);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        // EPIC-07 DoD Kriterium: explizite Miss-Hint-Meldung statt stiller Leermenge.
        Assert.Contains("Keine Treffer fuer 'userService'", text);
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", text);
        Assert.Contains("site.js", text);
        Assert.Contains("Component.razor", text);
        Assert.Contains("Page.xaml", text);
        Assert.Contains("search_pattern", text);
    }
}
```

**A3-Methodik:** Wenn ein Wire-Encoding-Bug den Hint
verschluckt oder umformuliert, schlägt der Test fehl. Konkret
prüfbar: temporär in `FindSymbolScanner.AppendMissHint` den
`return` durch `return baseText;` ersetzen (Miss-Hint-Pfad
deaktivieren) → Test schlägt fehl. **Test grün nur, wenn
der volle Hint-Text durch das stdio-Framing propagiert.**

### Schritt 4 — E2E-Mehrdeutigkeit-Test in neuer Datei

**Datei (NEU):** `src/AiNetLinter.Tests/Commands/McpServerCommandAmbiguityE2ETests.cs`

**Inhalt (~60-80 Z.):**

```csharp
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// E2E-Test fuer EPIC-07 Mehrdeutigkeits-Abbruch (Konzept Z. 617-618):
/// Ein Zielverzeichnis mit mehreren .sln/.slnx-Kandidaten ohne explizites
/// --path fuehrt zu einem Server-Start-Abbruch mit klarer Fehlermeldung
/// auf stderr statt einer stillschweigend falschen Solution-Auswahl.
/// Unit-Test in <c>McpServerCommandTests.cs:22-43</c> beweist die Helper-
/// Logik; dieser Test beweist das Verhalten des realen Server-Subprozesses.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandAmbiguityE2ETests
{
    [Fact]
    public void RunAsync_DirectoryWithTwoSlnx_AbortsWithAmbiguousSolutionError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ainetlinter-mcp-ambiguity-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "First.slnx"), "");
            File.WriteAllText(Path.Combine(tempDir, "Second.slnx"), "");

            var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
            Assert.True(File.Exists(exePath));

            var processInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--mcp-server --path \"{tempDir}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(processInfo);
            Assert.NotNull(process);
            Assert.True(process!.WaitForExit(TimeSpan.FromSeconds(10)), "Server-Prozess hat nicht innerhalb 10s beendet.");

            var stderr = process.StandardError.ReadToEnd();
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("AMBIGUOUS_SOLUTION", stderr);
            Assert.Contains("First.slnx", stderr);
            Assert.Contains("Second.slnx", stderr);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
```

**A3-Methodik:** Wenn `ResolveSolutionPathOrError` die
Mehrdeutigkeit nicht mehr erkennt (z. B. wenn ein Refactor
versehentlich `FindSolutionCandidates` durch
`SourceFileCatalog.FindSolutionFile` ersetzt, das `files[0]`
silent wählt), schlägt der Test fehl. **Test grün nur, wenn
der Server-Start wirklich mit Exit-Code ≠ 0 abbricht und
AMBIGUOUS_SOLUTION auf stderr schreibt.**

**Vorsicht:** Server-Process hat **kein** Timeout in
`McpServerCommand.RunAsync` für den Pre-Transport-Abbruch —
läuft synchron, schreibt Fehler, returnt 1, `McpServer.Create`
wird **nicht** aufgerufen. Process endet sauber. 10s-Timeout
sollte reichen.

### Schritt 5 — Cache-Isolation-Tests in neuer Datei

**Datei (NEU):** `src/AiNetLinter.Tests/Cache/AnalysisCacheManagerIsolationTests.cs`

**Inhalt (~80-120 Z., 3-4 Tests):**

```csharp
#nullable enable

using System;
using System.IO;
using AiNetLinter.Cache;
using Xunit;

namespace AiNetLinter.Tests.Cache;

/// <summary>
/// EPIC-07 Cache-Isolation (Konzept Z. 619-621): Zwei Cache-Loads mit
/// unterschiedlichen Solution-Pfaden muessen unterschiedliche Cache-
/// Filenamen erzeugen. Zwei Cache-Loads mit gleichem Solution-Pfad
/// denselben Hash-Anteil. Das Filename-Pattern ist
/// "{solutionName}-{SHA256(solutionPath + rulesJson)[..8]}-{timestamp}.json".
/// </summary>
public sealed class AnalysisCacheManagerIsolationTests
{
    [Fact]
    public void Load_DifferentSolutionPaths_ProduceDifferentHashes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ainetlinter-cache-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Zwei verschiedene Loesungen (Datei-Namen irrelevant, Pfad geht in den Hash).
            var solAPath = Path.Combine(tempDir, "SolutionA.slnx");
            var solBPath = Path.Combine(tempDir, "SolutionB.slnx");
            File.WriteAllText(solAPath, "");
            File.WriteAllText(solBPath, "");

            var managerA = AnalysisCacheManager.Load(tempDir, solAPath, "{}", TimeSpan.Zero);
            var managerB = AnalysisCacheManager.Load(tempDir, solBPath, "{}", TimeSpan.Zero);

            // ... Assertions auf internen Cache-Pfad ueber Reflection
            // oder eine neue internal-Property `CachePath`.
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_SameSolutionPath_ProduceSameHash()
    {
        // ... analog, aber mit gleichem Solution-Pfad
    }

    [Fact]
    public void Load_DifferentRulesJson_ProduceDifferentHashes()
    {
        // ... analog, aber mit unterschiedlichem rulesJson-Content
    }
}
```

**Wichtige Designentscheidung — `CachePath` zugänglich machen:**

- Aktuell ist `_cachePath` `private`. Test muss den Pfad
  **lesen** können.
- **Option A:** `internal`-Property `CachePath` (oder
  `InternalsVisibleTo` für `AiNetLinter.Tests` — vermutlich
  bereits gesetzt, **Coder prüft**).
- **Option B:** Reflection im Test (`typeof(AnalysisCacheManager).
  GetField("_cachePath", ...)`).
- **Empfehlung:** Option A (sauberer). `internal string CachePath
  => _cachePath;` hinzufügen, +1-2 Z. Footprint in
  `AnalysisCacheManager.cs` (Klasse ist 140 Z., Puffer 2360).

**A3-Methodik:** Wenn das SHA256-Pattern versehentlich
vereinfacht wird (z. B. nur `Path.GetFileNameWithoutExtension`
ohne Hash), produziert es identische Cache-Filenamen für
verschiedene Solutions → Test (i) schlägt fehl.

### Schritt 6 — E2E-MCP-Disk-Cache-Bypass-Test in neuer Datei

**Datei (NEU):** `src/AiNetLinter.Tests/Commands/McpServerCommandCacheBypassTests.cs`

**Inhalt (~60-100 Z., 1 Test):**

```csharp
#nullable enable

using System;
using System.IO;
using System.Linq;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// EPIC-07 Cache-Isolation (Konzept Z. 619-621): Ein MCP-Server + ein
/// gleichzeitiger CLI-Lint-Lauf auf derselben Solution kollidieren nicht.
/// Begruendung: der MCP-Modus umgeht den Disk-Cache
/// (<c>AnalysisCacheManager</c>) per Konzept Z. 175-183, der CLI-Lauf ist
/// alleiniger Schreiber seiner Cache-Datei. Dieser Test verifiziert, dass
/// der MCP-Server tatsaechlich KEIN Cache-File schreibt (oder zumindest
/// nicht in den gemeinsamen cache/-Pfad, der vom CLI-Lauf benutzt wird).
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandCacheBypassTests
{
    [Fact]
    public void McpServerMode_DoesNotWriteToAnalysisCacheDirectory()
    {
        // ... E2E mit Process.Start (MCP-Server starten, einen
        // get_violations-Call absetzen, beenden, dann
        // cache/-Verzeichnis pruefen).
    }
}
```

**Schwierigkeit:** Die exexe/Cache-Logik hängt vom
`Assembly.Location` ab. Test muss das **richtige** `cache/`-
Verzeichnis identifizieren. Pragmatische Vereinfachung:
**Reflection-Test** statt E2E — verifiziert, dass
`McpCodeGraphServer` **keine** `AnalysisCacheManager`-Referenz
hat (kein Feld, keine Property). Wenn jemand in 008+ versehentlich
einen Disk-Cache-Backport macht, schlägt der Test fehl.

**Empfehlung:** Statt E2E → **Reflection-Test** (2-Assert,
trivial). Spart ~80 Z. Test-Code und ist robuster (kein
`Process.Start`-Coordination-Overhead). Falls Coder die E2E-
Variante bevorzugt, ist das auch ok — `result.md` dokumentiert
die Entscheidung.

**A3-Methodik:** Wenn in `McpCodeGraphServer` versehentlich
`AnalysisCacheManager.Load(...)` aufgerufen wird, schlägt der
Reflection-Test fehl.

### Schritt 7 — CLI-Regression-Test in neuer Datei

**Datei (NEU):** `src/AiNetLinter.Tests/Commands/CliBatchRegressionTests.cs`

**Inhalt (~50-80 Z., 1 Test):**

```csharp
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// EPIC-07 CLI-Regression (Konzept Z. 622): der bestehende CLI-Batch-Modus
/// (ainetlinter --config rules.json --path <dir>) bleibt nach allen
/// EPIC-01..06-Aenderungen unveraendert lauffaehig. Bestehender Test
/// <c>CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess</c>
/// deckt die echte AiNetLinter-Solution ab; dieser Test deckt eine
/// Mini-Fixture ab, die schneller und deterministischer ist.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class CliBatchRegressionTests
{
    [Fact]
    public void RunLinterCli_OnSymbolGraphMiniFixture_ReportsViolationAndExitsZero()
    {
        // ... Process.Start mit dotnet AiNetLinter.dll --config <tempRules> --path <fixture>
        // Erwartet: Exit-Code 0, Output enthaelt "ViolationTrigger" (deterministische
        // Verletzung in SymbolGraphMini).
    }
}
```

**A3-Methodik:** Wenn eine EPIC-01..06-Änderung den CLI-Batch-Modus
bricht (z. B. ein `LinterArgs`-Refactor, der
`args.McpServer` nicht richtig verzweigt), schlägt der Test fehl.

### Schritt 8 — Build + Test + Footprint-Re-Messung

Nach allen 7 Schritten:

1. `dotnet build AiNetLinter.slnx` — muss grün sein.
2. `dotnet test AiNetLinter.slnx --no-build` — muss grün sein.
   **Erwartete Steigerung:** 1127 + ~10 = ~1137 Tests grün.
3. Footprint-Re-Messung pro betroffener Klasse:
   - `SourceFileCatalog.cs` (Z. 299-301) — Erwartung: +6-8.
   - `McpServerCommandTests.cs` (Z. 499) — unverändert.
   - Alle Tool-Klassen, Registrar-Klassen — unverändert.
4. Werte in `result.md` Abschnitt "Footprint nach 007" eintragen.
5. **Falls `GetImpactTool` > 2500 oder
   `SymbolGraphToolRegistrations` > 2500:** sofortige Eskalation
   im `result.md` (Description-Kürzung als Plan-Abweichung).

## Erwartete Tests (Anzahl pro Bereich, mit A3-Methodik)

| Bereich | Datei (NEU) | Tests | A3-Methodik pro Test |
|---|---|---:|---|
| (a) Integrationstest je Tool | n/a (Bestand) | 0 | n/a — Lücke dokumentiert in `result.md` |
| (b) Staleness-Invalidierung E2E | `McpServerCommandStalenessTests.cs` | 1 | Staleness deaktivieren in `McpCodeGraphServer` → Test rot |
| (c) Miss-Hint komplett E2E | `McpServerCommandMissHintTests.cs` | 1 | `AppendMissHint` deaktivieren → Test rot |
| (d) Mehrdeutigkeits-Abbruch E2E | `McpServerCommandAmbiguityE2ETests.cs` | 1 | `FindSolutionCandidates`-Mehrdeutigkeit entfernen → Test rot |
| (e-i) Cache-Filename-Isolation | `AnalysisCacheManagerIsolationTests.cs` | 1-2 | SHA256 durch Plain-String ersetzen → Test rot |
| (e-ii) Cache-Filename-Gleichheit | (selbe Datei) | 1 | Hash weglassen → Test rot |
| (e-iii) MCP-Disk-Cache-Bypass | `McpServerCommandCacheBypassTests.cs` | 1 | `McpCodeGraphServer` baut `AnalysisCacheManager` ein → Test rot |
| (f) CLI-Regression | `CliBatchRegressionTests.cs` | 1 | `Program.Main` ruft direkt `McpServerCommand` auf statt `ExecuteLinterAsync` → Test rot |
| **TD-003 (extra)** | `SourceFileCatalogRegisterMSBuildTests.cs` | 1-2 | Lock entfernen → Test rot (parallel-Calls werfen `InvalidOperationException`) |
| **Gesamt** | 7 neue Test-Dateien | **8-10 neue Tests** | alle A3-dokumentationspflichtig im `result.md` |

**Schritt-1-Pflicht vor jedem A3:** Test schreiben, **ausführen**,
Failure-Output wortwörtlich in `result.md` Abschnitt "A3-Nachweis
[Bereich]" dokumentieren. Dann Code-Änderung. Dann Test grün.

## Bezug zu Projektregeln

| Regel | Datei | Anwendung in 007 |
|---|---|---|
| `sealed` für konkrete Klassen | `AiNetLinter.mdc` Z. 10 | Alle 7 neuen Test-Klassen `sealed`. |
| `#nullable enable` am Dateianfang | `AiNetLinter.mdc` Z. 12 | Alle 7 neuen Test-Dateien. |
| `MaxLineCount: 500` | `AiNetLinter.mdc` Z. 20 | Alle 7 neuen Test-Dateien < 100 Z., kein Risiko. `McpServerCommandTests.cs` bleibt bei 499. |
| `MaxMethodLineCount: 100` (Tests) | `AiNetLinter.mdc` Z. 83 | Alle neuen Test-Methoden < 60 Z. (typisch 20-40 Z.). |
| `AIContextFootprint: 2500` | `AiNetLinter.mdc` Z. 28 | Keine neue Tool-Klasse in 007, nur Test-Klassen. `SourceFileCatalog.cs` +6-8 Z., risikofrei. |
| `Result-Pattern bevorzugen` | `AiNetLinterRichtlinien.mdc` §5 | Tests dürfen `Assert.*` werfen — explizite Ausnahme vom Result-Pattern. |
| Conventional Commits auf Deutsch, imperativ | `AiNetLinterRichtlinien.mdc` §4 | `feat(mcp): EPIC-07 tests-ausbau + TD-003 race-fix` oder zwei separate Commits (siehe Commit-Vorschlag unten). |
| `EnforceSealedClasses` aus für `*.Tests` | `AiNetLinter.mdc` Z. 83 | Tests dürfen `sealed` weglassen, aber wir setzen es trotzdem (Konsistenz mit 004/005/006). |
| `EnforceNamespaceDirectoryMapping` | `AiNetLinter.mdc` Z. 58 | Tests in `AiNetLinter.Tests/Commands/*Tests.cs` → Namespace `AiNetLinter.Tests.Commands`. Tests in `AiNetLinter.Tests/Cache/*Tests.cs` → Namespace `AiNetLinter.Tests.Cache`. |
| **Commit-Vorschlag Pflicht** | `AiNetLinterRichtlinien.mdc` §4 | Plan endet mit konkretem Commit-Vorschlag (siehe unten). |

**Commit-Strategie (zwei separate Commits, vom Orchestrator
festzulegen — A4):**

- **Commit 1: TD-003-Fix** (klein, isoliert, strukturell) —
  `fix(baseline): sourcefilecatalog registermsbuild thread-safe (TD-003)`.
  Diff: 6-8 Z. in `SourceFileCatalog.cs` + 1-2 Tests in
  `SourceFileCatalogRegisterMSBuildTests.cs`.
- **Commit 2: EPIC-07 Tests** (groß, 6 Bereiche, 7 neue Dateien) —
  `feat(tests): EPIC-07 tests-ausbau (6 dod-bereiche abgesichert)`.
  Diff: 7 neue Test-Dateien, ~300-500 Z. insgesamt, 0 produktive
  Code-Änderungen.

**Begründung der Aufteilung:** TD-003 ist ein **strukturelles
Bugfix** mit eigenem Risiko-Charakter; die EPIC-07-Tests sind
**reine Test-Erweiterung**. Zwei separate Commits ermöglichen
gezieltes Revert bei Problemen und sauberes Cherry-Picking.

## Annahmen und offene Fragen für den Coder

1. **Anzahl paralleler Test-Instanzen für Cache-Isolation:** 2-3
   reichen für A3 (mehr macht den Test unnötig langsam). Coder
   wählt 3 (deterministisch, schnell).
2. **Fixture-Wahl für Mehrdeutigkeit-Test:** `SymbolGraphMini`
   (existiert) oder neue Mini-Fixture? Bestehende reicht (kein
   C#-Inhalt nötig, nur `.slnx`-Dateien). Coder wählt die
   einfachste Variante (`.slnx` direkt im Temp-Dir, kein Fixture).
3. **`CachePath` internal-Machen in `AnalysisCacheManager`:** sauberer
   als Reflection (siehe Schritt 5). Coder entscheidet nach Sichtung
   von `AnalysisCacheManager.cs` Z. 13-43.
4. **MCP-Disk-Cache-Bypass-Test als E2E oder Reflection:** Plan
   empfiehlt Reflection (robuster, weniger Code). Coder entscheidet
   nach Verifizierung, dass `McpCodeGraphServer` keine versteckte
   Cache-Referenz hat.
5. **CLI-Regression: eigene `rules.json` für Mini-Fixture oder
   Default?** Mini-Fixture braucht ihre eigene `rules.json` (sonst
   wirft der Default-Config evtl. False Positives). Coder kopiert
   `rules.json` aus dem Solution-Root in ein Temp-Dir, passt
   `TestSentinel.TestProjectNameSuffixes` so an, dass die
   Fixture-Klassen als reguläre Sources zählen (nicht als Tests).
6. **TD-003-Test: 20 Tasks ausreichend?** 006-Kritiker schreibt
   "intermittierend" — d. h. mit 20 Tasks ist die Race mit hoher
   Wahrscheinlichkeit **vor** dem Fix reproduzierbar, **nach** dem
   Fix garantiert grün. Falls 20 nicht reicht, Coder auf 50 erhöhen
   (immer noch < 1s Test-Laufzeit).
7. **`Caller.cs`-Staleness-Erweiterung mit `CallerRenamedXyz`:** die
   hinzugefügte Klasse darf keine `using`-Statements brauchen, die
   die Fixture-Solution nicht hat. Plain `public class
   CallerRenamedXyz { }` reicht. Coder prüft, ob das die Solution
   valide hält (Roslyn sollte das ohne Fehler kompilieren).

## Harte Scope-Grenze (wiederholt)

**In 007 erlaubt:**

- 6 EPIC-07-Bereiche (a-f): Bestands-Audit + E2E-Tests in 7 neuen
  Dateien.
- TD-003-Fix: statischer Lock + Check-Lock-Check in
  `SourceFileCatalog.RegisterMSBuild`.
- 1 internal-Property `CachePath` in `AnalysisCacheManager` (für
  Test-Sichtbarkeit).
- A3-Methodik: alle neuen Tests mit wortwörtlichem
  Failure-Output-Nachweis im `result.md`.

**In 007 verboten:**

- **Keine** P0/P1-Rest-Erweiterungen (Kaltstart, Auto-Discovery,
  Staleness-Sweep, Call-Log, Verzeichnis-Sweep, `ILintConsole`).
- **Kein** EPIC-08-Doku.
- **Keine** Trunkierungs-Änderungen.
- **Keine** Miss-Hint-Logik-Änderungen (003 abgeschlossen — 007
  testet nur).
- **Keine** `PathOverrides`-Wert-Erhöhung.
- **Keine** Scanner-Splits.
- **Keine** Änderung an `McpCodeGraphServer`, `McpToolResults`,
  `McpServerOptionsFactory`, Tool-Klassen, Registrar-Klassen
  (außer dem oben erlaubten `CachePath` in `AnalysisCacheManager`).
- **Keine** TD-015 / TD-016 (nicht in 007-Scope, separat).
- **Kein** Push (A4), **kein** Amend (A4), **kein** Force-Push
  (A4), **kein** `git add -A` (A4).
- **Kein** `McpServerCommandTests.cs`-Edit (499/500, 1 Z. Puffer —
  strikt unangetastet).
- **Keine** Konzept-Änderung (A7).
- **Keine** Kernel-Änderung (A8).
- **Keine** Projektregel-Änderung (A7).
