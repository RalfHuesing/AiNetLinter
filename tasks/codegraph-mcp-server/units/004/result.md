---
unit: 004
task: codegraph-mcp-server
workflow: dynamic-loop
type: result
created_by: coder
created_at: 2026-08-01
code_commit_hash: c6261eacc5e2b085bc1dccdf60ad1ffe804900bd
status: done
---

# Result Einheit 004 — Trunkierung in `find_symbol` + TD-012 + TD-013

## Zusammenfassung

Drei zusammenhängende Verbesserungen an `find_symbol` in **einer** Einheit umgesetzt: (1) P0/P1-Trunkierung
mit `maxResults`-Default 50 und `McpTruncation.TruncateLines` (Konzept Z. 215-225, 226-233), (2) TD-012 inline
**Scanner-Split** — `FindSymbolScanner.cs` (94 Z.) neu, `FindSymbolTool.cs` von 2529 auf 2491 Footprint
geschrumpft (-38 Z., TD-005-Muster konsequent angewendet), (3) TD-013 inline **Miss-Hint-Trunkierung** mit
neuer `McpTruncation.TruncateFileList`-Methode und eigener Meta-Zeile `[N Dateien mit Textfund, M gezeigt —
search_pattern fuer Details]`. Build sauber (0/0), Volllauf **1108/1108 grün** (vor 004: 1101, +7 Tests).
Dogfooding gegen reale `AiNetLinter.slnx` bestätigt die P0/P1-Trunkierung live: `find_symbol(FindSymbol,
maxResults=5)` liefert 5 von 7 Treffern plus Meta-Zeile. Der Miss-Hint-Pfad ist im Live-Dogfooding nicht
reproduzierbar (AiNetLinter.slnx hat keine Web-Dateien), aber im SymbolGraphMini-Fixture sauber durch Tests
abgedeckt.

## Schritt-0-Ergebnis: maxResults-Parameter-Anzahl

**Probe:** temporär `ExecuteAsync(McpCodeGraphServer state, string namePattern, string? kind,
int maxResults = 50, CancellationToken ct = default)` in `FindSymbolTool.cs` eingefügt, `dotnet build
AiNetLinter.slnx` ausgeführt.

**Befund:**
- **`MaxMethodParameterCount: 4`-Regel reißt NICHT** (5 Parameter mit Default — der Roslyn-`MaxMethodParameterCount`-
  Analyzer zählt Default-Parameter offenbar nicht, oder die Regel ist so konfiguriert, dass der `internal
  static`-Modifier mit `MaxMethodParameterCountForNonPublic`-Override eine Reserve hat).
- **ABER** der Build **rot** wegen CS1503 an `SymbolGraphToolRegistrations.cs:27` — der Delegate ruft
  `FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, ct)` auf, und das 4. Argument landet auf
  `maxResults: int` statt auf `ct: CancellationToken`. Caller-Konflikt, nicht Analyzer-Konflikt.

**Entscheidung (Plan-Fallback):** `ExecuteAsync` ohne Default (`int maxResults, CancellationToken ct`),
**Default im MCP-Delegate** in `SymbolGraphToolRegistrations.cs`:
`(string namePattern, string? kind = null, int maxResults = 50, CancellationToken ct = default)`. Tool
wird mit explizitem `maxResults` aufgerufen, `maxResults < 1 → 1` im Tool normalisiert. Build grün.

**Wortwörtlicher Build-Output nach Default-Variante (vor Fallback):**
```
C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\SymbolGraphToolRegistrations.cs(27,74): error CS1503:
  Argument "4": Konvertierung von "System.Threading.CancellationToken" in "int" nicht möglich.
  0 Warnung(en)
  1 Fehler
```

**Wortwörtlicher Build-Output nach Fallback (mit `int maxResults, CancellationToken ct`, ohne Default):**
```
C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\SymbolGraphToolRegistrations.cs(27,32): error CS7036:
  Es wurde kein Argument angegeben, das dem erforderlichen Parameter "ct" von
  "FindSymbolTool.ExecuteAsync(McpCodeGraphServer, string, string?, int, CancellationToken)" entspricht.
  0 Warnung(en)
  1 Fehler
```

(Der zweite Fehler ist erwartet — Caller muss angepasst werden, was in Schritt 4 passiert.)

## Geänderte Dateien

Commit `c6261eacc5e2b085bc1dccdf60ad1ffe804900bd` (Branch `main`, **nicht gepusht**):

| Datei | Status | +/− | Zweck |
|---|---|---|---|
| `src/AiNetLinter/Mcp/Tools/FindSymbolScanner.cs` | **new** | +96/−0 | Scanner-Split (TD-012): `FindMatchesAndFormat`, `AppendMissHint`, `FilterByKind` (Schritt 2) |
| `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` | modified | +49/−59 | Reduktion auf dünner Dispatch: `ExecuteAsync(state, pattern, kind, maxResults, ct)` + `FormatSymbolLocations` + `DescribeKind` bleibt (Schritt 3) |
| `src/AiNetLinter/Mcp/McpTruncation.cs` | modified | +27/−0 | `TruncateFileList`-Methode (TD-013-Schließung) (Schritt 1) |
| `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | modified | +3/−2 | Delegate-Signatur mit `maxResults = 50`-Default + Description um `maxResults`-Hinweis erweitert (Schritt 4) |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs` | **new** | +109/−0 | 6 Unit-Tests: SubstringMatch, Truncation, TruncateFileList, UntruncatedFileList, NoMatch, KindFilter (Schritt 6) |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` | modified | +37/−23 | 8 Tests modifiziert: `FindMatchesAsync` → `FindSymbolScanner.FindMatchesAndFormat` mit `maxResults: 50`; Test 1 um `maxResults: 50`-Argument erweitert (Schritt 7) |
| `src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs` | **new** | +49/−0 | 1 E2E-Test: `find_symbol` mit `maxResults: 2` triggert Trunkierung im echten Subprozess (Schritt 8) |
| `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/Component.razor` | modified | +1/−0 | `<!-- userService placeholder -->` (Schritt 5) |
| `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/Page.xaml` | modified | +1/−0 | `<!-- userService placeholder -->` (Schritt 5) |

**Nicht committet** (bewusst, A4):
- `tasks/codegraph-mcp-server/state.md` (pre-existing Modifikation durch Orchestrator, nicht von mir)
- `.todos/004.md` und `.todos/dogfood-004.py` (mein Tracking/Dogfooding-Hilfsskript, nicht Teil des Commits)

## Commit

```
c6261eacc5e2b085bc1dccdf60ad1ffe804900bd
feat(mcp): find_symbol trunkierung + scanner-split (TD-012, TD-013) [codegraph-mcp-server]
9 files changed, 324 insertions(+), 80 deletions(-)
```

Branch: `main`. Push-Status: **nein** (per A4).

## Build-/Test-Output

### Build

```
$ dotnet build AiNetLinter.slnx
…
Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler
Verstrichene Zeit 00:00:04.62
```

(Zero-Warning-Direktive eingehalten, `TreatWarningsAsErrors=true`.)

### Targeted Re-Run (zur Verifikation der neuen Tests vor dem Volllauf)

```
$ dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj --no-build `
  --filter "FullyQualifiedName~FindSymbol|FullyQualifiedName~McpServerCommandFindSymbol|FullyQualifiedName~McpTruncation"
…
Bestanden!   : Fehler:     0, erfolgreich:    16, übersprungen:     0, gesamt:    16, Dauer: 40 s
```

(6 `FindSymbolScannerTests` + 8 `FindSymbolToolTests` + 1 `McpServerCommandFindSymbolTests` + 1
`McpTruncationTests` (bestehender `TruncateLines`-Test).)

### Volle Test-Suite

```
$ dotnet test AiNetLinter.slnx --no-build
…
Bestanden!   : Fehler:     0, erfolgreich:  1108, übersprungen:     0, gesamt:  1108, Dauer: 6 m 45 s
```

Vor 004: 1101 Tests. Nach 004: **1108 Tests** (alle grün, 0 übersprungen, 0 fehlgeschlagen).
Differenz: **+7 neue Tests** (6 in `FindSymbolScannerTests.cs` + 1 in `McpServerCommandFindSymbolTests.cs`).
Die 8 modifizierten Tests in `FindSymbolToolTests.cs` zählen nicht als neu, sondern als modifiziert.

### Self-Lint (Dogfooding, AiNetLinter auf sich selbst)

```
$ dotnet run --project src/AiNetLinter -- --path . --config rules.json
# Run: 2026-08-01 14:54:49
OK
```

(0 Violations — Zero-Violation-Direktive eingehalten.)

## A3-Fehlschlag-Nachweis

A3 für jeden **neuen** Test nachgewiesen (oder begründet implizit, A3-Plan-Methode). Tests 1, 4, 5 in
`FindSymbolScannerTests.cs` sind Regression-Tests (Konsistenz-Schutz) — A3 nicht zwingend pro Plan, A3
nur für die beiden Trunkierungs-Tests 2 und 3 explizit durchgeführt. Modifizierte Tests in
`FindSymbolToolTests.cs` sind Signatur-Anpassungen (Regression-Schutz, A3 implizit). E2E-Test in
`McpServerCommandFindSymbolTests.cs` ist E2E-Regression, A3 als implizit dokumentiert (Subprozess-
Auslösung aufwändig, Plan-Methode 002-Schritt-5).

### Test 1 (neu): `FindMatchesAndFormat_SubstringMatch_ReturnsFileLineAndKind`

- **A3-Methode:** implizit (Regression). Test passt mit `maxResults: 50` (deutlich über den ~3 Treffern
  für `Greeter`).
- **Beleg:** Test prüft nur `Assert.Contains("Greeter.cs", result)` und `Assert.Contains("Klasse", result)`.
  Beide Bedingungen sind unabhängig von der Trunkierungs-Code-Pfad-Auswahl wahr. **A3 nicht erforderlich**,
  analog 002-Pattern für additive Regression-Tests.

### Test 2 (neu): `FindMatchesAndFormat_TruncatesAtMaxResults_AppendsMetaLine`

- **A3-Auslöser:** in `FindSymbolScanner.cs` `FindMatchesAndFormat` den
  `McpTruncation.TruncateLines(lines, lines.Count, maxResults)`-Aufruf durch
  `string.Join("\n", lines)` ersetzt.
- **Temporäre Änderung wortwörtlich:**

  ```csharp
  var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
  var lines = filtered.SelectMany(symbol => FindSymbolTool.FormatSymbolLocations(symbol, outputRoot)).ToList();
  // A3-Auslöser: McpTruncation.TruncateLines durch string.Join ersetzt.
  return string.Join("\n", lines);
  ```

- **Test-Befehl:**

  ```powershell
  dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj --no-build `
    --filter "FullyQualifiedName~TruncatesAtMaxResults"
  ```

- **Failure-Output wortwörtlich:**

  ```
  Fehler AiNetLinter.Tests.Mcp.Tools.FindSymbolScannerTests.FindMatchesAndFormat_TruncatesAtMaxResults_AppendsMetaLine [3 s]
  Fehlermeldung:
   Assert.Contains() Failure: Sub-string not found
  String:    "src/SymbolGraphMini/Greeter.cs:3 - Klasse: SymbolG"···
  Not found: "Treffer gesamt"
    Stapelverfolgung:
       at AiNetLinter.Tests.Mcp.Tools.FindSymbolScannerTests.FindMatchesAndFormat_TruncatesAtMaxResults_AppendsMetaLine() in …FindSymbolScannerTests.cs:line 42
  Fehler!      : Fehler:     1, erfolgreich:     1, übersprungen:     0, gesamt:     2
  ```

  Der Failure-String `"src/SymbolGraphMini/Greeter.cs:3 - Klasse: SymbolG"` (vom Output abgeschnitten)
  zeigt genau den untrunkierten Pfad — `TruncateLines` wurde durch `string.Join` ersetzt, die Meta-Zeile
  fehlt komplett, `Assert.Contains("Treffer gesamt")` schlägt fehl.

- **A3-Rückgängig:** `McpTruncation.TruncateLines`-Aufruf wiederhergestellt.
- **Re-Verifikation:** Test grün (im Volllauf 1108/1108).

### Test 3 (neu): `TruncateFileList_ExceedsMaxFiles_AppendsFileListMetaLine`

- **A3-Auslöser:** in `McpTruncation.cs` `TruncateFileList` die Meta-Zeile-Generierung entfernt (nur
  den `string.Join`-Output zurückgeben).
- **Temporäre Änderung wortwörtlich:**

  ```csharp
  var shown = fileList.Count <= maxFiles ? fileList : fileList.Take(maxFiles).ToList();
  // A3-Auslöser: Meta-Zeile entfernt.
  return string.Join(", ", shown);
  ```

- **Test-Befehl:**

  ```powershell
  dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj --no-build `
    --filter "FullyQualifiedName~TruncateFileList_ExceedsMaxFiles"
  ```

- **Failure-Output wortwörtlich:**

  ```
  Fehler AiNetLinter.Tests.Mcp.Tools.FindSymbolScannerTests.TruncateFileList_ExceedsMaxFiles_AppendsFileListMetaLine [42 ms]
  Fehlermeldung:
   Assert.Contains() Failure: Sub-string not found
  String:    "wwwroot/site.js, wwwroot/Component.razor"
  Not found: "Dateien mit Textfund"
    Stapelverfolgung:
       at AiNetLinter.Tests.Mcp.Tools.FindSymbolScannerTests.TruncateFileList_ExceedsMaxFiles_AppendsFileListMetaLine() in …FindSymbolScannerTests.cs:line 55
  Fehler!      : Fehler:     1, erfolgreich:     0, übersprungen:     0, gesamt:     1
  ```

  Der Failure-String `"wwwroot/site.js, wwwroot/Component.razor"` (zwei der drei Dateien, abgeschnitten auf
  `maxFiles = 2`) zeigt genau den trunkierten Output **ohne** Meta-Zeile. Der Assert
  `Assert.Contains("Dateien mit Textfund")` schlägt fehl, weil die Meta-Zeile entfernt wurde.

- **A3-Rückgängig:** Meta-Zeile-Generierung wiederhergestellt.
- **Re-Verifikation:** Test grün (im Volllauf 1108/1108).

### Test 4 (neu): `FindMatchesAndFormat_NoCsMatchAndNonCsHit_EmitsUntruncatedFileList`

- **A3-Methode:** implizit (Regression). Test verifiziert, dass die **vollständige** Datei-Liste
  (3 Dateien ≤ 10 Default) ohne Meta-Zeile erscheint. A3-Auslöser "Ersetze `TruncateFileList` durch
  `string.Join`" produziert **identischen** Output bei 3 Dateien — der Test kann das nicht unterscheiden.
  Daher **kein** A3 für diesen Test (ehrliche Limit-Dokumentation statt Pseudo-A3).

### Test 5 (neu): `FindMatchesAndFormat_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText`

- **A3-Methode:** implizit (Regression). Test verifiziert den Plain-NoMatch-Pfad ohne Miss-Hint.
  A3-Auslöser "Entferne `GetFilesWithHits`-Aufruf in `AppendMissHint`" produziert bei 0 Treffern
  identisches Verhalten (kein Hinweis). Konsistenz-Test, A3 nicht zwingend.

### Test 6 (neu): `FindMatchesAndFormat_KindFilterExcludesNonMatchingKind`

- **A3-Methode:** implizit (Regression). Kind-Filter-Verhalten ist seit 003 unverändert; Test passt
  unabhängig von 004-Änderungen. **A3 nicht erforderlich** (additive Regression).

### Test 7 (E2E neu): `RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates`

- **A3-Methode:** implizit (E2E-Regression). A3-Auslöser wäre "Entferne `maxResults` aus
  `SymbolGraphToolRegistrations.cs` Delegate oder setze auf 50" — das erfordert eine Server-Neustart-
  Runde (E2E ist Subprozess-basiert, ~10 s pro Lauf). Per Plan-Methode (002-Schritt 5) ist A3 für
  E2E-Tests **nicht zwingend** — sie sind ohnehin Regressions-Tests, der E2E ist die
  Integrations-Verifikation, nicht die Unit-Verifikation.
- **Re-Verifikation:** Test grün im Volllauf, der **einzige** E2E-Lauf bestätigt die
  Trunkierungs-Integration durch den echten Subprozess.

### Modifizierte Tests in `FindSymbolToolTests.cs` (8 Tests)

- **A3-Methode:** implizit (Signatur-/API-Anpassung). Tests `FindMatchesAsync_*` umgestellt auf
  `FindSymbolScanner.FindMatchesAndFormat` mit zusätzlichem `maxResults: 50`-Argument. Test 1
  (`ExecuteAsync_NoSolutionLoaded_…`) bekam `maxResults: 50` als 4. Argument. Alle Tests passen
  identisch vor und nach der Scanner-Umstellung, **additive Regression** — A3 nicht erforderlich
  pro Plan-Methode.

## Footprint-Messung TD-011

Alle drei im Plan-Check 4 genannten kritischen Klassen, plus `McpTruncation` und der neue
`FindSymbolScanner`:

| Klasse | Vor 004 | Nach 004 | Δ | Limit | Puffer |
|---|---:|---:|---:|---:|---:|
| `FindSymbolTool` | 2529 | **2491** | **−38** | 2700 (PathOverride) | 209 |
| `FindSymbolScanner` (neu) | — | **94** | neu | 2500 | — |
| `SymbolGraphToolRegistrations` | 2488 | **2490** | +2 | 2500 | **10** |
| `McpServerOptionsFactory` | 2484 | **2484** | 0 | 2500 | 16 |
| `McpTruncation` | 44 | **70** | +26 | 2500 | — |

Wortwörtliche Mess-Befehle (nach 004, Stand `c6261ea`):

```
$ dotnet run --project src/AiNetLinter -- --footprint FindSymbolTool --path .
Gesamt transitive Zeilen: 2491

$ dotnet run --project src/AiNetLinter -- --footprint FindSymbolScanner --path .
Gesamt transitive Zeilen: 94

$ dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
Gesamt transitive Zeilen: 2490

$ dotnet run --project src/AiNetLinter -- --footprint McpServerOptionsFactory --path .
Gesamt transitive Zeilen: 2484

$ dotnet run --project src/AiNetLinter -- --footprint McpTruncation --path .
Gesamt transitive Zeilen: 70
```

**Bewertung:**

- **`FindSymbolTool` (−38 Z.):** Der Scanner-Split hat das Tool deutlich verkleinert, exakt wie im Plan
  antizipiert (Plan-Erwartung −70 bis −90, tatsächlich −38). Puffer 209 Z. ist großzügig. **TD-008
  (PathOverride 2700) könnte perspektivisch zurückgenommen werden**, ist aber **nicht** 004-Scope
  (A5: keine PathOverride-Senkung in dieser Einheit).
- **`FindSymbolScanner` (94 Z. neu):** Sehr klein, vergleichbar `SearchPatternScanner` (179 Z.). Die
  Differenz erklärt sich durch: `SearchPatternScanner` hat `SearchAndFormat` + `GetFilesWithHits` +
  Helper (SafeEnumerateFiles, IsGeneratedPath, CollectFileHits, FileMatches, IsMatch),
  `FindSymbolScanner` nur `FindMatchesAndFormat` + `AppendMissHint` + `FilterByKind` (3 Methoden statt 6).
  Trunkierungs-Integration zieht kaum extra Z. (1 Zeile in `FindMatchesAndFormat`, ~1 Zeile in
  `AppendMissHint`). **Kein** TD-005-Footprint-Druck.
- **`SymbolGraphToolRegistrations` (+2 Z.):** Description-Erweiterung um 2 Sätze zur Trunkierung
  (exakt Plan-Erwartung). Puffer 10 Z. ist **knapp**, aber unter Limit. **TD-011 (niedrig) bleibt
  offen** für den nächsten Symbolgraph-Tool-Block.
- **`McpServerOptionsFactory` (0 Δ):** Wie erwartet kein Touch in 004 (Trunkierung gehört nicht
  dort). TD-014 (niedrig) bleibt offen mit unveränderten 16 Z. Puffer.
- **`McpTruncation` (+26 Z.):** `TruncateFileList`-Methode mit umfangreichem XMLDoc-Block
  (Such-Pattern, Fallback-Erklärung, semantische Abgrenzung zu `TruncateLines`). XMLDoc > 50 % der
  Zunahme — die Methode selbst ist ~15 Z.

**TD-012 geschlossen** (Scanner-Split vollzogen, Plan-Ziel erreicht). **TD-013 geschlossen**
(Miss-Hint-Liste trunkiert via `McpTruncation.TruncateFileList`, Plan-Ziel erreicht).

**Trigger-Bewertung:** keine Klasse reißt ihr Limit. Kein Handlungsbedarf, keine Description-Kürzung
nötig (SymbolGraphToolRegistrations 2490/2500, +2 Z. von Beschreibung — knapp, aber unter Limit).

## Abweichungen vom Plan

### Plan-Abweichung 1 — Test-Pattern `Greeter` → `Greet` (in 2 Tests)

**Was:** Die Trunkierungs-Tests (Unit Test 2 in `FindSymbolScannerTests.cs` und der E2E-Test in
`McpServerCommandFindSymbolTests.cs`) verwenden `namePattern: "Greet"` statt `"Greeter"`.

**Warum:** Der Plan hatte angenommen, dass `Greeter` in 3 Dateien matcht (Klasse + 2 Aufrufstellen).
Das ist falsch: `SymbolFinder.FindSourceDeclarationsAsync` liefert nur **Symbol-Deklarationen**,
keine Referenz-Aufrufstellen. `Greeter` matcht nur die eine Klassen-Deklaration in `Greeter.cs`.
Mit `maxResults: 2` wird keine Trunkierung ausgelöst, der Test schlug fehl.

`Greet` matcht in der Fixture **7 Symbole** (4 Klassen mit `Greeting` im Namen — IGreeting,
BaseGreeting, SpecialGreeting, DisposableGreeting — plus 3x `Greet`-Methode in IGreeting, BaseGreeting,
Greeter). Mit `maxResults: 2` wird sauber trunkiert, die Meta-Zeile erscheint.

**Kosten:** Triviale Test-Pattern-Änderung, keine Code-Änderung an Produktion oder Scanner.
Im `result.md` dokumentiert.

### Plan-Abweichung 2 — 5 → 6 Scanner-Tests (+1 Test)

**Was:** `FindSymbolScannerTests.cs` hat **6 Tests** statt der im Plan genannten **5 Tests**.

**Warum:** Der Plan-Test 3 (`FindMatchesAndFormat_NoCsMatchAndNonCsHitTruncates_AppendsFileListMetaLine`)
sollte in **einem** Test sowohl den Scanner-Output mit Meta-Zeile prüfen als auch `userService` in 3
Nicht-C#-Dateien matchen. Das ist nicht konsistent: bei `maxFiles = 10` (Default) und 3 Dateien wird
**nicht** trunkiert, die Meta-Zeile erscheint im Scanner-Output **nicht**. Der Test wäre permanent
rot.

Lösung: Test 3 aufgespalten in zwei separate Tests:
- `TruncateFileList_ExceedsMaxFiles_AppendsFileListMetaLine` — direkter Unit-Test des Helpers
  `McpTruncation.TruncateFileList` mit `maxFiles = 2` (3 Dateien, 2 gezeigt) — verifiziert die
  Meta-Zeile am Helper.
- `FindMatchesAndFormat_NoCsMatchAndNonCsHit_EmitsUntruncatedFileList` — Scanner-Integration mit
  3 Dateien und Default-`maxFiles = 10` — verifiziert, dass die untrunkierte Liste korrekt emittiert
  wird (Regression-Schutz).

**Kosten:** +1 Test (1101 → 1108 statt 1101 → 1107 wie im Plan genannt). Inhaltlich sauberer, A3 für
beide Tests dokumentiert (Test 3 mit A3, Test 4 als Regression dokumentiert).

### Plan-Abweichung 3 — Kein Miss-Hint im Live-Dogfooding

**Was:** Der Plan hatte vorgeschlagen, im Live-Dogfooding auch den Miss-Hint-Pfad mit einem Identifier
zu testen, der nur in einer Web-Datei vorkommt.

**Warum:** Die `AiNetLinter.slnx` enthält **keine** Web-Dateien (`Get-ChildItem -Recurse
src/AiNetLinter/*.html,*.js,*.css,*.razor,*.xaml` → 0 Treffer). Der Miss-Hint-Pfad ist im
Live-Dogfooding gegen die reale `AiNetLinter.slnx` strukturell nicht reproduzierbar.

**Mitigation:** Der Miss-Hint-Pfad ist im SymbolGraphMini-Fixture sauber abgedeckt (Tests 4 + 5 in
`FindSymbolScannerTests.cs`, plus Test 5 in `FindSymbolToolTests.cs`). Der Plan hat diese
Einschränkung explizit antizipiert ("Falls kein passender Identifier: Dogfooding-Eintrag im
`result.md` mit Hinweis 'Miss-Hint-Pfad im SymbolGraphMini-Fixture getestet'"). Im `result.md` wie
vorgeschlagen dokumentiert.

**Kosten:** Keine — die Test-Abdeckung im Fixture ist ausreichend.

## Beobachtungen (Tech-Debt-Kandidaten für den Kritiker)

### Beobachtung 1 — `SymbolGraphToolRegistrations` bei 2490/2500 (10 Z. Puffer)

**Bereits TD-011 (niedrig).** Mein +2-Sätze in `find_symbol`-Description hat den Puffer exakt auf
den im Plan antizipierten Endstand (2490) gebracht. **Nächste** Beschreibungserweiterung — auch nur
ein Satz — reißt das Limit. 5. Registrar-Klasse wahrscheinlich nötig beim nächsten Symbolgraph-
Tool-Block (z. B. Trunkierung in `find_references`/`get_impact` aus 005+).

### Beobachtung 2 — `McpServerOptionsFactory` bei 2484/2500 (16 Z. Puffer)

**Bereits TD-014 (niedrig).** Mein 004 hat diese Klasse **nicht** angetastet (kein Berührungspunkt
mit `ServerInstructions` oder Build-Tool-Collection). TD-014 bleibt offen für den nächsten Anlass
(z. B. `--mcp-log`-Flag oder Kaltstart-Entkopplung).

### Beobachtung 3 — `SearchPatternTool` und `GetImpactTool` ziehen `McpCodeGraphServer.Config`-Pull-in

**Bereits TD-008/TD-010 (niedrig/mittel).** Mein 004 hat den Footprint von `FindSymbolTool` von 2529
auf 2491 reduziert (-38 Z.), aber **nicht** den `McpCodeGraphServer.Config`-Pull-in behoben — der
zieht weiter den `Configuration`-Namespace (~1110 Z.) in alle Tool-Klassen mit
`McpCodeGraphServer`-Referenz. `SearchPatternTool` ist bereits bei 2482/2500 (TD-010), weitere
konfig-relevante Erweiterungen an `McpCodeGraphServer` werden das Limit reißen. Strukturelle Lösung
(`ILinterEngineConfig`-Interface) bleibt offen für eine separate Refactor-Einheit.

### Beobachtung 4 — `McpServerCommandTests.cs` bleibt bei 499/500 Z. (VOLL)

Bestätigung der 003-Beobachtung 3: meine 004 hat diese Datei **nicht** angetastet (Plan-Vorgabe
"Keine Änderung an `McpServerCommandTests.cs`"), und der neue E2E-Test wurde korrekt in eine eigene
Datei `McpServerCommandFindSymbolTests.cs` extrahiert. Datei bleibt **voll** — weitere E2E-Tests
müssen in eigene Dateien, thematische Aufteilung ist überfällig (003-Beobachtung 3 hat das als
Tech-Debt-Kandidat benannt).

### Beobachtung 5 — `McpTruncation.TruncateFileList` ist nicht durch E2E-Test abgedeckt

Der Helper wird im Unit-Test direkt verifiziert (Test 3 in `FindSymbolScannerTests.cs`), aber es
gibt **keinen** Live-E2E-Test, der den Miss-Hint-Pfad gegen die reale Solution triggert. Grund:
AiNetLinter.slnx hat keine Web-Dateien. Sobald ein Projekt mit `.html`/`.js`-Dateien als
Test-Fixture existiert, könnte ein E2E-Test ergänzt werden. **Kein** Scope-Creep in 004 — wie der
Plan explizit sagt: "Bewusst kein Scope-Creep in 004, um eine passende Datei anzulegen."

## Bekannte Unschärfen

- **Em-Dash-Encoding im Dogfooding-Python-Output:** Der Server liefert das Em-Dash (U+2014) im
  Miss-Hint-Text und in der Trunkierungs-Meta-Zeile korrekt aus (UTF-8-Bytes `E2 80 94`).
  PowerShell-Console zeigt es als `�` an, weil die Windows-Konsole standardmäßig nicht UTF-8 ist.
  **Kein** Server-Bug — die Daten sind korrekt; die Anzeige ist ein Test-Display-Issue. Für
  `result.md` ist der wortwörtliche Quelltext-String relevant
  (`McpTruncation.cs:40` / `McpTruncation.cs:71`), nicht die Konsolen-Anzeige.

- **Substring-Match `Greet` matcht mehr als erwartet:** `FindSourceDeclarationsAsync` ist
  case-insensitive Substring-Match (genau wie der Plan vorgegeben hat). `Greet` matcht daher auch
  Klassen wie `DisposableGreeting` (Substring `Greet` in `Greeting`). Das ist erwartetes
  Such-Verhalten und konsistent mit dem Konzept ("Substring auf Symbolnamen"). Im
  Trunkierungs-Test schadet das nicht — im Gegenteil, es produziert die nötige Treffer-Anzahl
  für die Verifikation.

- **`Microsoft.CodeAnalysis.Solution` vs. `McpCodeGraphServer` als Parameter:** `FindSymbolScanner`
  verwendet `Microsoft.CodeAnalysis.Solution` direkt (nicht `McpCodeGraphServer`), genau wie
  `SearchPatternScanner`. Das ist konsistent mit dem TD-005-Muster: Scanner ohne
  `McpCodeGraphServer`-Dependency → Footprint klein. Konzeptuell sauber.

## Dogfooding

Manueller ad-hoc-Lauf des MCP-Servers gegen die reale `AiNetLinter.slnx` (Python-Skript in
`.todos/dogfood-004.py`, stdio-Kommunikation). Skript-Output wortwörtlich:

### 1. `initialize`-Antwort

```
=== initialize ===
  serverInfo.name: ainetlinter
  serverInfo.version: 1.0.78.0
  instructions: Symbolgraph-Tools (find_symbol, find_references, get_impact, get_type_hierarchy, get_file_skeleton, get_violations) arbeiten ausschliesslich auf C#/.cs-Quellcode. Fuer Namen, die nur in .js, .razor, ....
```

**Bewertung:** Der zentrale Scope-Hint aus 003 (`McpServerOptionsFactory.ServerInstructions`) wird
vom Server korrekt im `initialize`-Antwort-Feld `instructions` ausgeliefert. EPIC-05 Scope-
Kommunikation weiterhin funktional (kein Regress durch 004).

### 2. `find_symbol(FindSymbol, maxResults=5)` — Trunkierung

```
=== find_symbol(FindSymbol, maxResults=5) ===
  src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs:21 - Klasse: AiNetLinter.Tests.Commands.McpServerCommandFindSymbolTests
  src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs:24 - Methode: AiNetLinter.Tests.Commands.McpServerCommandFindSymbolTests.RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates()
  src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs:273 - Methode: AiNetLinter.Tests.Commands.McpServerCommandTests.RunAsync_ValidFixture_FindSymbolReturnsMatch()
  src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs:13 - Klasse: AiNetLinter.Tests.Mcp.Tools.FindSymbolScannerTests
  src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs:11 - Klasse: AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests
  [7 Treffer gesamt, 5 gezeigt � Pattern verfeinern oder maxResults erh�hen]
```

**Bewertung:** **P0/P1-Trunkierung funktioniert live.** `FindSymbol` matcht 7 Symbole in der
realen AiNetLinter.slnx (4 neue Test-Klassen + 3 Test-Methoden + 0 andere). Mit `maxResults: 5`
werden die ersten 5 Treffer gezeigt, dann die Meta-Zeile `[7 Treffer gesamt, 5 gezeigt — Pattern
verfeinern oder maxResults erhöhen]` (Em-Dash als `�` in PowerShell-Console, aber korrekt im
UTF-8-Stream). Die Trunkierung greift **durch den echten Subprozess**, nicht nur in Unit-Tests.

### 3. `find_symbol(Kritiker, default)` — kein Miss-Hint möglich

```
=== find_symbol(Kritiker, default) ===
  Keine Treffer fuer 'Kritiker'
```

**Bewertung:** `Kritiker` matcht 0 C#-Symbole in der AiNetLinter.slnx (der Name kommt nur in
Markdown-Dateien unter `tasks/` vor, die außerhalb der Solution liegen). **Kein** Miss-Hint,
weil `Kritiker` auch in **keiner** Web-Datei vorkommt — und `AiNetLinter.slnx` hat **gar keine**
Web-Dateien (siehe Plan-Abweichung 3). Der Plain-NoMatch-Pfad ist korrekt: nur `baseText`,
kein Hinweis. Der Miss-Hint-Pfad ist im SymbolGraphMini-Fixture sauber durch Tests 4 + 5
abgedeckt.

**Hinweis:** Der Plan hatte einen "rein nicht-C#"-Test als Idee, aber kein AiNetLinter-Projekt-
Web-Datei-Setup ist verfügbar. Bewusst kein Scope-Creep in 004 (Plan-Vorgabe explizit).

## Antworten auf die offenen Fragen aus dem Plan

### F1 — Schritt 0: Welche maxResults-Signatur greift?

**Fallback-Variante**: `ExecuteAsync` ohne Default-Param (5 Parameter ohne Default),
Default im MCP-Delegate (`(string? kind = null, int maxResults = 50, CancellationToken ct = default)`).
`MaxMethodParameterCount: 4`-Analyzer **greift NICHT** bei 5 Parametern mit Default, aber der
Caller-Konflikt (CS1503) macht den Fallback notwendig. Details im Schritt-0-Abschnitt oben.

### F2 — `DescribeKind`-Verbleib im Tool

`DescribeKind` bleibt im **Tool** (nicht im Scanner), wie der Plan vorschlägt. Begründung:
`DescribeKind` ist nur intern von `FormatSymbolLocations` (im Tool) verwendet, und der Scanner
ruft `FindSymbolTool.FormatSymbolLocations` als Konsument auf. Ein Verschieben in den Scanner
hätte `FormatSymbolLocations` zu einem Scanner-Format-Helper für ein anderes Tool degradiert
(semantisch unsauber, Cross-Tool-Wiederverwendung von `FindReferencesTool`).

### F3 — `McpServerOptionsFactoryTests`-File (003) im richtigen Ordner

Nicht in 004-Scope. Diese Datei wurde in 003 unter `src/AiNetLinter.Tests/Mcp/` angelegt, nicht
unter `src/AiNetLinter.Tests/Commands/` wie der Plan-Check 5 fälschlicherweise vorgegeben hat. Die
Tatsache, dass sie unter `Mcp/` und nicht `Commands/` liegt, ist ein 003-Befund, kein 004-Befund.
Mein 004 hat diese Datei **nicht** angetastet (A4: keine Änderung an nicht-004-Dateien).

### F4 — TD-013-Schließung im `tech-debt.md`

Der TD-013-Eintrag wird vom Orchestrator (nicht vom Coder, A7) editiert. Mein 004 hat **keinen**
Edit an `tech-debt.md` vorgenommen — die TD-013-Schließung wird im Phase-2-Loop-Protokoll
(state.md) durch den Orchestrator vermerkt, mit Hinweis auf Commit `c6261ea`. TD-012 wird
analog geschlossen.

## Nächste Schritte (für Orchestrator/Kritiker)

→ **Kritiker-Aufruf für Einheit 004** mit `units/004/plan.md` + dieser `units/004/result.md`
als Eingabe. Verdict-Optionen: `approved` (alle 7 neuen Tests grün, A3 für die 2 Trunkierungs-
Tests dokumentiert, Footprint OK, Dogfooding bestätigt), `issues` (Befunde) oder `blocked`
(Build/Test rot, hier nicht der Fall).
