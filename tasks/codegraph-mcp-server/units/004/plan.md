---
unit: 004
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-01
epic: EPIC-05/P0P1 (Trunkierung in find_symbol)
extends:
  - konzept.md Z. 215-225 (P0/P1 Trunkierung + maxResults)
  - konzept.md Z. 226-233 (Plain-Text-Format, einheitliche Meta-Zeile)
  - TD-012 (FindSymbolTool ohne Scanner-Split, inline)
  - TD-013 (find_symbol-Miss-Hint-Datei-Liste ohne Trunkierung, inline)
  - units/002/plan.md (McpTruncation-Signatur, SearchPatternTool/Scanner-Vorbild)
  - units/003/plan.md (Miss-Hint-Pfad, GetFilesWithHits-Wiederverwendung)
---

# Plan Einheit 004 — Trunkierung in `find_symbol` (P0/P1) + TD-012/TD-013 inline

## Ziel der Einheit

Drei thematisch eng zusammenhängende Verbesserungen an `find_symbol`
in **einer** Einheit, weil sie am selben Tool ansetzen und sich
gegenseitig die Footprint-Situation günstig beeinflussen lassen:

1. **P0/P1-Trunkierung** in `find_symbol` — `maxResults`-Parameter
   (Default 50) am Tool, Anwendung von `McpTruncation.TruncateLines`
   auf den Haupt-Treffer-Output, einheitliche Meta-Zeile (Konzept
   Z. 215-225, 226-233).
2. **TD-012 inline: Scanner-Split** — `FindSymbolScanner.cs` neu
   anlegen, `FindSymbolTool.cs` auf dünner Dispatch reduzieren
   (TD-005-Muster, analog `SearchPatternTool`/`SearchPatternScanner`
   in 002). Erwarteter Effekt: `FindSymbolTool` schrumpft deutlich,
   `FindSymbolScanner` eigenständig klein (vergleichbar mit
   `SearchPatternScanner` 179/2500).
3. **TD-013 inline: Miss-Hint-Trunkierung** — zweite Variante
   `McpTruncation.TruncateFileList` mit eigener Meta-Zeile
   ("[N Dateien mit Textfund, M gezeigt — search_pattern fuer
   Details]") auf die Miss-Hint-Datei-Liste anwenden.

Bezug: Konzept Z. 215-225 (Trunkierungs-DoD), Z. 226-233
(Meta-Zeile-Format), Z. 604-606 (Miss-Hint-DoD), Kritiker-Vorschläge
aus `units/003/review.md` "Vorschlag 1" (TD-012) und "Vorschlag 2"
(TD-013).

## Scope-Entscheidung

**Gewählt: alle drei Punkte in 004.** Begründung:

- (a) Trunkierung ist **eigenständige P0/P1-Pflicht** (Konzept
  Z. 215-225 nennt `find_symbol` explizit als erstes Listen-Tool).
- (b) TD-012 ist **explizit als "inline beim nächsten Anlass"** für
  die `find_symbol`-Trunkierung markiert (Kritiker-Vorschlag in
  `units/003/review.md` Vorschlag 1). Würde man es verschieben,
  entstünde eine eigenständige Refactor-Einheit, die laut TD-005
  "nachträglich teurer" ist als inline.
- (c) TD-013 ist analog **explizit als "inline beim nächsten
  `find_symbol`-Anlass"** markiert (Kritiker-Vorschlag 2).
- **Alle drei** setzen am selben Tool an, teilen sich die
  Argument-Validierung, den Pflicht-Footprint-Re-Run, und
  die Test-Fixture-Erweiterung — eine Einheit ist günstiger als
  drei kleine.
- **Günstiger Footprint-Effekt:** Scanner-Split lässt `FindSymbolTool`
  voraussichtlich deutlich schrumpfen (von 2529 auf ~2540 mit
  Trunkierungs-Code, was die PathOverride-Diskussion entspannt),
  der `FindSymbolScanner` ist als eigenständige 2500er-Klasse klein.
  Wäre (a) allein, würde `FindSymbolTool` weiter wachsen
  (Trunkierung-Code) — Scanner-Split federt das ab.

**Alternative (im Plan erwogen, verworfen):** TD-013 (Miss-Hint-
Trunkierung) in 005 verschieben, nur (a)+(b) in 004. Verworfen,
weil die Miss-Hint-Trunkierung 6-8 Zeilen zusätzlichem Code ist
und keine zusätzlichen Konzept-Entscheidungen braucht — der
Scope-Bloat ist minimal, der TD-013-Schließungs-Vorteil konkret.

**Bewusst NICHT in 004:**

- **Keine** Trunkierung in `find_references` oder `get_impact`
  (005/006, getrennt).
- **Keine** sonstigen P0/P1-Extensions (Kaltstart, Auto-Discovery,
  Staleness-Sweep-`mtime`, `--mcp-log`, etc.) — alle in
  Folge-Einheiten.
- **Keine** Änderung an `McpServerOptionsFactory` über eine
  Pflicht-Footprint-Messung hinaus.
- **Keine** `PathOverrides`-Wert-Erhöhung in `rules.json`.
- **Kein** Eingriff in `McpCodeGraphServer`, `LinterErrorFormatter`,
  `McpToolResults`.
- **Keine** Doku (`Docs/agent-api.md`, `Docs/ROADMAP.md`) — EPIC-08.

## Vor-der-Planung-Checks (Kernel Teil B "Drift" / "Duplikate durch Blindheit")

### Check 1 — Aktueller Stand `FindSymbolTool` (112 Z. nach 003)

**Befund (gelesen, `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs`):**

- **Kein** `maxResults`-Parameter in `ExecuteAsync` oder
  `FindMatchesAsync` — Trunkierung muss neu eingeführt werden.
- `ExecuteAsync(McpCodeGraphServer state, string namePattern,
  string? kind, CancellationToken ct)` — 4 Parameter (Limit 4, am
  Limit). **Addition von `maxResults` würde 5/4 reißen.**
  → Lösung: `maxResults` Default-Parameter oder via Input-`record`
  (TD-009-Logik). Da `maxResults` optional ist, **eignet sich ein
  Default-Parameter mit `= 50`** (bricht das Limit nicht — Default-
  Parameter zählen in der Signatur, Achtung).
  → Re-Verifikation: in C# zählt der Default-Wert **nicht** für
  `MaxMethodParameterCount` (roslyn-Analyzer regelbasiert auf
  declared parameters, nicht effective call). Trotzdem **besser:**
  `int maxResults = 50` (Default-Parameter), weil der
  Tool-Aufrufer im Normalfall nichts übergibt. Wenn der Analyzer
  Default-Parameter zählt (5 statt 4), siehe Schritt 2 unten
  (alternativ: `record`-Wrapper).
- `FindMatchesAsync(Solution solution, string namePattern,
  string? kind, CancellationToken ct)` — 4 Parameter, gleiches
  Problem bei `maxResults`-Hinzufügung. **Hier im Scanner** ist
  das Problem weniger akut (Scanner hat keine
  `McpCodeGraphServer`-Dependency, also keinen Footprint-Druck),
  aber für Konsistenz und um den `ExecuteAsync`-Aufruf mit
  explizitem `maxResults` zu erlauben, sollte der Scanner den
  Parameter ebenfalls haben.
- **Methoden im Tool aktuell (zu verschieben / zu behalten):**
  - `ExecuteAsync` → bleibt im Tool (dünner Dispatch).
  - `FindMatchesAsync` → wandert in Scanner.
  - `FilterByKind` (private) → wandert in Scanner.
  - `DescribeKind` (private) → wandert in Scanner.
  - `FormatSymbolLocations` (internal static, Z. 92-102) → **bleibt
    im Tool** (wird von `FindReferencesTool` für die
    Ambiguity-Fehlermeldung wiederverwendet, siehe XMLDoc
    Z. 87-91). Wenn in den Scanner verschoben, müsste
    `FindReferencesTool` `FindSymbolScanner.FormatSymbolLocations`
    referenzieren — semantisch unsauber (Scanner-Logik von einem
    anderen Tool aufgerufen).
- **Miss-Hint-Pfad** (Z. 51-66): aktuell im Tool. Nach Scanner-
  Split: Scanner ruft `SearchPatternScanner.GetFilesWithHits` auf,
  trunkiert mit `McpTruncation.TruncateFileList`, baut den
  Hint-String. Tool bekommt vom Scanner den fertigen
  `FindMatchesAsync`-Output (Trunkierungs-Meta-Zeile inklusive
  oder nicht, je nach Treffer-Anzahl), Tool gibt ihn 1:1 zurück.
- **Code-Style:** deutsche Umlaut-Ersetzungen (`fuer`, `ue`,
  `Bestaetigung`) — bei neuem Code fortsetzen.

**Entscheidung im Plan:**

- **Scanner-Split in 004** durchziehen (TD-012), `FormatSymbolLocations`
  ausgenommen.
- **`maxResults` als Default-Parameter** in Tool **und** Scanner.
  Falls der `MaxMethodParameterCount`-Analyzer Default-Parameter
  zählt (5 statt 4), greift `Compound Suppressions` für öffentliche
  Methoden nicht (`MaxMethodParameterCount: 4` ohne
  Compound-Suppression), und `MaxMethodParameterCountForNonPublic: 6`
  für private — `ExecuteAsync` und `FindMatchesAsync` sind `internal`
  static, also greift weder 4 noch 6 zuverlässig. **Vorsicht:**
  Coder prüft das im Build (Schritt 0); falls 5/4 gerissen, **Fallback:**
  `int maxResults` **ohne** Default im Scanner-Aufruf, Tool
  normalisiert `maxResults < 1 → 1` und reicht den expliziten
  Wert weiter.
- **Miss-Hint-Pfad lebt im Scanner** — Tool bleibt frei von
  Hint-Logik (TD-005-Muster eingehalten).

### Check 2 — `SearchPatternTool`/`SearchPatternScanner`-Trennung als Vorbild

**Befund (gelesen):**

- `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` (67 Z., Footprint
  2485/2500): dünner Dispatch — Argument-Validierung (`string.
  IsNullOrEmpty(pattern)` → `McpToolResults.Error` mit Hint),
  `maxResults`-Normalisierung (`< 1 → 1`), Lade-Solution-Check
  (`SolutionNotLoaded()`), `await Task.Run(SearchPatternScanner
  .SearchAndFormat(solution, ...))` in `try/catch` für
  `ArgumentException` (Regex), `McpToolResults.Text`.
- `src/AiNetLinter/Mcp/Tools/SearchPatternScanner.cs` (179 Z.,
  Footprint 179/2500 — **sehr klein**): gesamte Scan- und
  Format-Logik. KEINE `McpCodeGraphServer`-Dependency. Verwendet
  `WebFileCatalog.GetProjectDirectories`, private Kopien von
  `SafeEnumerateFiles`/`IsGeneratedPath` (TD-006, 1:1 dupliziert).
  Zwei öffentliche Methoden: `SearchAndFormat(Solution, pattern,
  isRegex, maxResults)` und `GetFilesWithHits(Solution, pattern,
  isRegex)`.

**Entscheidung im Plan:**

- **`FindSymbolTool.cs` wird analog gebaut** (dünner Dispatch,
  ~30-40 Z.). `maxResults`-Normalisierung im Tool.
- **`FindSymbolScanner.cs`** (neu) enthält `FindMatchesAsync`,
  `FilterByKind`, `DescribeKind`. KEINE `McpCodeGraphServer`-
  Dependency. `Microsoft.CodeAnalysis.Solution` ist OK (kein
  Server-Typ).
- **Miss-Hint-Pfad im Scanner**: `FindSymbolScanner` ruft
  `SearchPatternScanner.GetFilesWithHits(solution, namePattern,
  isRegex: false)` auf (genau wie bisher in 003), und
  trunkiert mit der neuen `McpTruncation.TruncateFileList`-
  Variante.
- **Scanner-Methoden-Signatur** (final, nach Konsolidierung):
  ```csharp
  internal static string FindMatchesAndFormat(
      Solution solution,
      string namePattern,
      string? kind,
      int maxResults);
  ```
  Liefert den fertig formatierten + trunkierten Text
  (Haupt-Treffer inkl. `McpTruncation.TruncateLines` + optionaler
  Miss-Hint mit `McpTruncation.TruncateFileList`).
- **`FormatSymbolLocations` bleibt im Tool** (siehe Check 1).
  Wird intern vom Tool für die `FindReferencesTool.AmbiguousSymbol`-
  Kandidatenliste weiterhin aufgerufen.

### Check 3 — Aktuelle Footprints (TD-011 Pflicht, gemessen 2026-08-01 14:18-14:19)

| Klasse | Z. | Limit | Puffer |
|---|---:|---:|---:|
| `FindSymbolTool` | **2529** | 2700 (PathOverride) | 171 |
| `SymbolGraphToolRegistrations` | **2488** | 2500 | **12** |
| `McpServerOptionsFactory` | **2484** | 2500 | 16 |
| `SearchPatternTool` (ref) | 2485 | 2500 | 15 |
| `SearchPatternScanner` (ref) | 179 | 2500 | — |
| `McpTruncation` (ref) | 44 | 2500 | — |
| `McpServerCommandTests.cs` (Datei) | 499 | 500 (MaxLineCount) | **1** ⚠ |

Wortwörtliche Mess-Befehle (gerade ausgeführt, Stand
`5b962dd chore(task): unit 003 approved`):

```
$ dotnet run --project src/AiNetLinter -- --footprint FindSymbolTool --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.Tools.FindSymbolTool':
Gesamt transitive Zeilen: 2529

$ dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.SymbolGraphToolRegistrations':
Gesamt transitive Zeilen: 2488

$ dotnet run --project src/AiNetLinter -- --footprint McpServerOptionsFactory --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.McpServerOptionsFactory':
Gesamt transitive Zeilen: 2484

$ dotnet run --project src/AiNetLinter -- --footprint McpTruncation --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.McpTruncation':
Gesamt transitive Zeilen: 44
```

**Entscheidung im Plan (TD-011/TD-014-Trigger-Bewertung):**

- **`SymbolGraphToolRegistrations` (Puffer 12 Z.):** 004 erweitert
  die `find_symbol`-Description um 1-2 Sätze
  ("Bei 0 Treffern wird auf Textvorkommen in Nicht-C#-Dateien
  hingewiesen. Trunkiert standardmaessig auf 50 Treffer,
  ueberschreibbar via maxResults."). Geschätzter Zuwachs: +8-12 Z.
  → 2488 + 12 = **2500 exakt oder knapp daneben.** **Knapp,
  reicht wahrscheinlich, aber riskant.** Coder entscheidet im
  Zweifel: Description prägnant halten, ggf. auf den
  `ServerInstructions`-Text verweisen statt zu duplizieren. **Falls
  exakt 2500 erreicht/überschritten:** kosmetische Description-
  Kürzung (siehe 003-Plan Schritt 2).
- **`McpServerOptionsFactory` (Puffer 16 Z.):** 004 ändert diese
  Klasse **nicht** (Trunkierung lebt nicht dort, `ServerInstructions`
  bleibt unverändert). Coder misst trotzdem (TD-014-Pflicht).
- **`FindSymbolTool` (PathOverride-Puffer 171 Z.):** 004 reduziert
  die Klasse durch den Scanner-Split drastisch. Geschätzt:
  Tool-Datei ~30-40 Z. (`ExecuteAsync` + `FormatSymbolLocations` +
  Klassen-Container), Footprint vermutlich ~2440-2460 (durch
  `McpCodeGraphServer`-Pull-in **kleiner** als aktuell, weil
  weniger eigene Logik). **Scanner-Footprint** ist eigenständig
  ~200-300 Z. (vergleichbar `SearchPatternScanner` 179 Z.).
  → **Netto-Effekt positiv** für TD-008 (PathOverride könnte
  perspektivisch zurückgenommen werden, **aber nicht in 004** —
  wäre Scope-Creep, A5).
- **`McpServerCommandTests.cs` (VOLL, 499/500 Z.):** **Harte
  Einschränkung.** KEIN weiterer Test darf in diese Datei. Alle
  neuen E2E-Tests in 004 gehen in eine **neue** Datei
  `McpServerCommandFindSymbolTests.cs` (analog
  `McpServerOptionsFactoryTests.cs` in 003). Der bestehende
  `RunAsync_ValidFixture_FindSymbolReturnsMatch` (Z. 273) bleibt
  unverändert in `McpServerCommandTests.cs`.
- **TD-008 / TD-010 / TD-011 / TD-012 / TD-013 / TD-014** alle
  nach 004 zu prüfen: TD-012 wird durch 004 geschlossen
  (Scanner-Split vollzogen), TD-013 wird durch 004 geschlossen
  (Miss-Hint trunkiert). TD-008/TD-010/TD-011/TD-014 bleiben
  offen (kein Eingriff in 004).

### Check 4 — `McpTruncation.TruncateLines` und geplante `TruncateFileList`

**Befund (gelesen, `src/AiNetLinter/Mcp/McpTruncation.cs`):**

```csharp
internal static string TruncateLines(
    IReadOnlyList<string> hitLines,
    int totalMatches,
    int maxResults)
```

Liefert entweder `string.Join("\n", hitLines)` oder ersten
`maxResults`-Slice + Meta-Zeile `[N Treffer gesamt, M gezeigt —
Pattern verfeinern oder maxResults erhöhen]`. **Quell-Datei 44 Z.,
sehr klein.**

**Bewertung für 004-Miss-Hint-Trunkierung:**

- **Erste Variante unverändert** (Haupt-Treffer-Output von
  `search_pattern` UND neu `find_symbol`).
- **Zweite Variante `TruncateFileList`** wird benötigt: andere
  Meta-Zeile (Dateien statt Zeilen), anderer Fallback-Hinweis
  (search_pattern statt "Pattern verfeinern"). Empfohlene
  Signatur:
  ```csharp
  internal static string TruncateFileList(
      IReadOnlyList<string> fileList,
      int totalFiles,
      int maxFiles);
  ```
  Liefert entweder `string.Join(", ", fileList)` oder ersten
  `maxFiles`-Slice + Meta-Zeile `[N Dateien mit Textfund, M
  gezeigt — search_pattern fuer Details]`. **Bewusst kleiner
  Cutoff** (z. B. `maxFiles = 10` im Tool normalisiert, oder als
  Default-Parameter) — viele Dateien in einer Hint-Zeile sind
  ohnehin UX-feindlich.
- **Architektur-Entscheidung:** zweite Methode, **nicht**
  Generalisierung der ersten mit Enum/String-Parameter. Grund:
  semantisch unterschiedlich (Zeilen-Treffer vs. Datei-Liste,
  unterschiedliche Fallback-Hinweise), und eine geteilte Methode
  würde die bestehende `search_pattern`-Verwendung subtil
  ändern (Risiko, A5).
- **`maxFiles` wo setzen?** Empfehlung: Default `10` in der
  TruncateFileList-Signatur (`internal static string
  TruncateFileList(..., int maxFiles = 10)`). Tool reicht
  den `maxResults`-Wert vom User durch (nicht nötig, eine
  eigene Größe zu erlauben — Hint-Liste ist UI-Detail, nicht
  User-steuerbar). Coder entscheidet; **Empfehlung bleibt bei
  `maxFiles = 10` als Default-Parameter** ohne User-Param.

**Entscheidung im Plan:**

- `McpTruncation.cs` wird um **eine** zweite Methode
  `TruncateFileList` erweitert. Erste Methode unverändert
  (`search_pattern` 002 + `find_symbol` 004 nutzen sie).
- Meta-Zeile Wortlaut (TD-013-Kritiker-Empfehlung wörtlich):
  `"[N Dateien mit Textfund, M gezeigt — search_pattern fuer Details]"`.

### Check 5 — Tests-Fixture-Erweiterung

**Befund (gelesen, `tests/Fixtures/SymbolGraphMini/`):**

- 5 Web-Dateien in `wwwroot/`: `site.js`, `Component.razor`,
  `index.html`, `Page.xaml`, `styles.css`. `site.js` hat seit
  003 den Identifier `userService` (Planer-Empfehlung, von Coder
  übernommen, A3-belegt).
- 5 C#-Dateien im Projekt: `Caller.cs`, `Greeter.cs`,
  `Hierarchy.cs`, `OtherCaller.cs`, `ViolationTrigger.cs`. Keine
  dieser Dateien enthält `userService` (003-A3-Pflicht verifiziert,
  `rg "userService" tests/Fixtures/SymbolGraphMini --type cs` →
  no matches).

**Bedarf für 004-Tests:**

1. **Trunkierung Haupt-Output**: braucht ein `namePattern`, das
   in C# **mindestens 3** Symbole trifft (damit
   `maxResults = 2` → Trunkierung sicher ausgelöst). `Greeter`
   matcht in `Greeter.cs` (Klasse) + `Caller.cs`/`OtherCaller.cs`
   (Aufrufstellen) → garantiert ≥ 3 Treffer-Zeilen. Funktioniert.
2. **Miss-Hint-Trunkierung**: braucht ein `namePattern`, das in
   **mindestens 3** Nicht-C#-Dateien Textfunde hat. Aktuell
   `userService` nur in `site.js` → 1 Datei. → **Fixture muss
   erweitert werden.** Empfehlung: `userService` zusätzlich in
   `Component.razor` (z. B. `<!-- userService placeholder -->`)
   und `Page.xaml` (z. B. `<!-- userService placeholder -->`)
   hinzufügen → 3 Dateien mit `userService`-Vorkommen.
   Eindeutigkeits-Check bleibt gültig: in keiner `.cs`-Datei der
   Fixture vorhanden.
3. **E2E-Trunkierung**: braucht `find_symbol`-Aufruf mit
   `maxResults: 2` → Trunkierung ausgelöst. Im
   `SymbolGraphMiniFixtureWorkspace` matcht `Greeter` garantiert
   ≥ 3 Symbole.

**Entscheidung im Plan (Empfehlung — Coder darf abweichen, wenn
begründet):**

- **`site.js`**: unverändert (steht schon da seit 003).
- **`Component.razor`**: +1 Zeile mit `userService`-Marker, z. B.
  `<!-- userService placeholder -->`.
- **`Page.xaml`**: +1 Zeile mit `userService`-Marker, z. B.
  `<!-- userService placeholder -->`.
- **Eindeutigkeits-Verifikation** vor Schritt 1:
  ```powershell
  rg "userService" tests/Fixtures/SymbolGraphMini/ --type cs
  ```
  Darf **nichts** liefern. Verifikation im `result.md` wortwörtlich
  dokumentieren (A3-Voraussetzung).
- **Fixture-Dateien modifizieren, nicht neu anlegen** — der
  Datei-Scan in `SymbolGraphMiniFixtureWorkspace.cs:33-45`
  kopiert alle bestehenden Dateien 1:1 (siehe 003-Plan Check 5).
- **Diese Fixture-Änderungen sind Teil des Code-Commits** (A1: ein
  Coder, ein Commit).

## Betroffene Dateien / Module

### Neu zu erstellen

| Datei | Zweck | Geschätzte Größe |
|---|---|---|
| `src/AiNetLinter/Mcp/Tools/FindSymbolScanner.cs` | Reine Scan- und Format-Logik für `find_symbol` (TD-005-Muster, TD-012-Schließung) — `FindMatchesAndFormat(Solution, namePattern, kind, maxResults)`, `FilterByKind`, `DescribeKind` | ~80-100 Z. |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs` | Unit-Tests für Scanner (Trunkierung Haupt-Output, Miss-Hint-Trunkierung) — getrennt von `FindSymbolToolTests.cs`, weil Scanner die Kernlogik hat | ~120-160 Z. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs` | Neue E2E-Test-Datei (weil `McpServerCommandTests.cs` **voll** mit 499/500 Z.) | ~40-50 Z. |

### Zu modifizieren

| Datei | Änderung |
|---|---|
| `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` | Reduktion auf dünner Dispatch: `ExecuteAsync(McpCodeGraphServer, namePattern, kind?, maxResults=50, ct)` mit Argument-Validierung, Lade-Solution-Check, Scanner-Aufruf, `McpTruncation`-Aufruf, `McpToolResults.Text`. `FormatSymbolLocations` bleibt (für `FindReferencesTool`-Wiederverwendung). |
| `src/AiNetLinter/Mcp/McpTruncation.cs` | +1 Methode `TruncateFileList(IReadOnlyList<string>, int, int = 10)` mit Meta-Zeile `[N Dateien mit Textfund, M gezeigt — search_pattern fuer Details]`. Erste Methode `TruncateLines` unverändert. |
| `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | `find_symbol`-Description (Z. 31-33) erweitern: +1-2 Sätze zur Trunkierung (maxResults-Default 50, Meta-Zeile erwähnt). Andere Tools unverändert. |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` | Test 1 (`ExecuteAsync_NoSolutionLoaded_…`) um `maxResults: 50` ergänzen (neuer Signatur-Parameter). Sonst bestehende Tests unverändert (sie rufen `FindMatchesAsync` ohne `maxResults` weiterhin auf, der Scanner hat Default). **Coder prüft:** wenn `maxResults` Pflichtparameter wird, alle bestehenden `FindMatchesAsync`-Aufrufe in Tests um Default ergänzen. |
| `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/Component.razor` | +1 Zeile `<!-- userService placeholder -->` (für Miss-Hint-Trunkierung). |
| `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/Page.xaml` | +1 Zeile `<!-- userService placeholder -->`. |

**Nicht modifiziert** (bewusst, gegen Drift-Anfälligkeit):

- `src/AiNetLinter/Mcp/Tools/SearchPatternScanner.cs` — keine
  Erweiterung der `GetFilesWithHits`-Signatur. `FindSymbolScanner`
  ruft sie unverändert auf, trunkiert selbst.
- `src/AiNetLinter/Mcp/SearchPatternTool.cs`,
  `src/AiNetLinter/Mcp/FindReferencesTool.cs` — kein Eingriff.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — keine
  Erweiterung (Trunkierung gehört nicht in `ServerInstructions`).
  Pflicht-Re-Messung im `result.md` (TD-014, Coder).
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — keine neue
  Dependency, TD-009-Risiko (5/5) bleibt stabil.
- `src/AiNetLinter/Mcp/McpToolResults.cs` — keine Änderung.
- `src/AiNetLinter/Mcp/Output/LinterErrorFormatter.cs` — keine
  Änderung.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` —
  **VOLL** (499/500 Z., siehe 003-Review MINOR 1). Kein
  weiterer Test in dieser Datei. Bestehender
  `RunAsync_ValidFixture_FindSymbolReturnsMatch` (Z. 273)
  bleibt unverändert (kein `maxResults`-Argument im Aufruf, der
  Default greift, kein 004-Eingriff nötig).
- `rules.json` — kein neuer `PathOverride`. Falls
  `SymbolGraphToolRegistrations` reißt: Description-Kürzung statt
  PathOverride-Erhöhung (TD-008-Präzedenz bewusst vermeiden).
- `konzept.md`, `tech-debt.md`, `state.md`, Projektregeln,
  `Docs/**` — A7 (nur lesen).
- **`#nullable enable` am Anfang von `FindSymbolToolTests.cs`**
  (003-MINOR 2): pre-existing, **nicht** 004-Scope. Strikt nach
  A2/A5: 004 fasst es nicht an. Coder darf es nachziehen, wenn
  die Datei sowieso berührt wird; **Empfehlung Planer: NEIN**
  (Scope minimal halten).

## Konkretes Vorgehen (Schritt-für-Schritt für den Coder)

### Schritt 0 — Pre-Build-Check: `maxResults`-Parameter-Anzahl

**Bevor** der Coder Code schreibt:

```powershell
cd C:/Daten/Entwicklung/Ralf/AiNetLinter
dotnet build AiNetLinter.slnx
```

Muss grün sein (Baseline nach 003, Commit `5b962dd`).

**Dann:** Test-Signatur-Probe. Temporär in `FindSymbolTool.cs`
folgendes Snippet einsetzen, Build laufen lassen, prüfen ob
`MaxMethodParameterCount`-Regel (Limit 4) bei Default-Parameter
greift:

```csharp
internal static async Task<CallToolResult> ExecuteAsync(
    McpCodeGraphServer state, string namePattern, string? kind, int maxResults = 50, CancellationToken ct = default)
```

**Falls Build grün** (Default zählt nicht): diese Signatur in
Schritt 1/3 übernehmen.

**Falls Build rot** (Regel reißt): temporäre Änderung verwerfen,
stattdessen **Fallback-Signatur** verwenden:

```csharp
internal static async Task<CallToolResult> ExecuteAsync(
    McpCodeGraphServer state, string namePattern, string? kind, int maxResults, CancellationToken ct)
```

und im `McpServerTool.Create`-Delegate in
`SymbolGraphToolRegistrations.cs` den Default manuell setzen:
`(string namePattern, string? kind = null, int maxResults = 50,
CancellationToken ct = default)`. Tool-Methode wird mit
`maxResults` explizit aufgerufen, Normalisierung (`< 1 → 1`)
passiert in `ExecuteAsync`-Body.

**Doku im `result.md`:** welcher Fall eingetreten ist, mit
Build-Output.

### Schritt 1 — `McpTruncation.TruncateFileList` ergänzen

Datei `src/AiNetLinter/Mcp/McpTruncation.cs` öffnen. Am Ende der
Klasse (nach `TruncateLines`, vor der schließenden Klammer) eine
zweite Methode hinzufügen:

```csharp
/// <summary>
/// Liefert <paramref name="fileList"/> als kommaseparierte Dateipfad-Liste. Wenn
/// <paramref name="totalFiles"/> groesser als <paramref name="maxFiles"/> ist, werden nur
/// die ersten <paramref name="maxFiles"/> Dateipfade zurueckgegeben und eine Meta-Zeile
/// "[N Dateien mit Textfund, M gezeigt — search_pattern fuer Details]" angehaengt.
/// Zweite Variante zu <see cref="TruncateLines"/> — andere Meta-Zeile, weil der
/// Fallback-Aufruf ein anderes Tool ist (search_pattern fuer Inhalte) als bei der
/// Haupt-Treffer-Liste (Pattern verfeinern oder maxResults erhoehen). Bewusst als
/// eigenstaendige Methode statt einer parametrisierten Variante, weil semantisch
/// unterschiedlich und eine Generalisierung die bestehende search_pattern-Verwendung
/// subtil aendern wuerde (A5).
/// </summary>
internal static string TruncateFileList(
    IReadOnlyList<string> fileList,
    int totalFiles,
    int maxFiles = 10)
{
    if (totalFiles <= maxFiles)
    {
        return string.Join(", ", fileList);
    }

    var shown = fileList.Count <= maxFiles ? fileList : fileList.Take(maxFiles).ToList();
    var meta = $"[{totalFiles} Dateien mit Textfund, {maxFiles} gezeigt — search_pattern fuer Details]";
    return string.Join(", ", shown) + "\n" + meta;
}
```

**Constraints:**

- `#nullable enable` schon am Dateianfang (`McpTruncation.cs:1`).
- `maxFiles` als Default-Parameter = 10 (UI-Schwelle: mehr als
  10 Dateien in einer Hint-Zeile sind UX-feindlich).
- `MaxMethodLineCount: 60`: Methode ist ~15 Z. ✓
- `MaxMethodParameterCount: 4`: 3 Parameter ✓ (auch mit
  Default-Param).
- Kein `try/catch`, reine Formatierung analog `TruncateLines`.

### Schritt 2 — `FindSymbolScanner.cs` anlegen (TD-012-Schließung)

Neue Datei `src/AiNetLinter/Mcp/Tools/FindSymbolScanner.cs`:

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Reine Symbol-Scan- und Format-Logik fuer <see cref="FindSymbolTool"/> — in eine eigene
/// Datei ausgelagert, damit <see cref="FindSymbolTool"/>s eigener <c>AIContextFootprint</c>
/// (siehe <c>AiNetLinter.mdc</c>) klein bleibt (TD-005-Muster, analog zu
/// <see cref="SearchPatternScanner"/>, TD-012-Scanner-Split). Keine Abhaengigkeit von
/// <see cref="McpCodeGraphServer"/> — direkt unit-testbar. Trunkierung des
/// Haupt-Treffer-Outputs ueber <see cref="McpTruncation.TruncateLines"/>,
/// Trunkierung der Miss-Hint-Datei-Liste ueber
/// <see cref="McpTruncation.TruncateFileList"/> (TD-013-Schliessung).
/// </summary>
internal static class FindSymbolScanner
{
    /// <summary>
    /// Liefert den fertig formatierten und trunkierten Treffer-Text fuer
    /// <paramref name="namePattern"/>. Verwendet <see cref="SymbolFinder"/> fuer die
    /// Symbol-Suche, <see cref="McpTruncation"/> fuer die Trunkierung. Bei null
    /// C#-Treffern wird der Miss-Hint ueber <see cref="SearchPatternScanner.GetFilesWithHits"/>
    /// aufgebaut und ebenfalls trunkiert (TD-013).
    /// </summary>
    /// <param name="solution">Bereits geladene Roslyn-Solution.</param>
    /// <param name="namePattern">Substring-Match auf Symbol-Namen (case-insensitive).</param>
    /// <param name="kind">Optionaler Kind-Filter ("class"/"interface"/"method"/"property").</param>
    /// <param name="maxResults">Obergrenze fuer die Anzahl ausgegebener Trefferzeilen
    /// (siehe <see cref="McpTruncation.TruncateLines"/>); muss >= 1 sein (Aufrufer normalisiert).</param>
    /// <returns>Plain-Text-Output (Trefferzeilen + optionale Trunkierungs-Meta-Zeile,
    /// optionale Miss-Hint-Zeile mit eigener Trunkierungs-Meta-Zeile).</returns>
    internal static async Task<string> FindMatchesAndFormat(
        Solution solution,
        string namePattern,
        string? kind,
        int maxResults)
    {
        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            solution,
            name => name.Contains(namePattern, StringComparison.OrdinalIgnoreCase),
            SymbolFilter.TypeAndMember,
            CancellationToken.None);

        var filtered = FilterByKind(symbols, kind).ToList();
        if (filtered.Count == 0)
        {
            var kindSuffix = kind is null ? "" : $" (Kind-Filter: {kind})";
            var baseText = $"Keine Treffer fuer '{namePattern}'{kindSuffix}";
            return AppendMissHint(solution, namePattern, baseText);
        }

        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var lines = filtered.SelectMany(symbol => FindSymbolTool.FormatSymbolLocations(symbol, outputRoot)).ToList();
        return McpTruncation.TruncateLines(lines, lines.Count, maxResults);
    }

    private static string AppendMissHint(Solution solution, string namePattern, string baseText)
    {
        var missHits = SearchPatternScanner.GetFilesWithHits(
            solution, namePattern, isRegex: false);
        if (missHits.Count == 0)
        {
            return baseText;
        }
        // Trunkierung der Datei-Liste (TD-013): Default 10 Dateien, Meta-Zeile via
        // McpTruncation.TruncateFileList. Forward-Slash-Pfade konsistent mit
        // SearchPatternScanner.GetFilesWithHits.
        var fileList = McpTruncation.TruncateFileList(missHits, missHits.Count);
        return $"{baseText}\nHinweis: kein C#-Symbol, aber Textfund in {fileList} " +
            $"(nicht Teil des Symbolgraphs — fuer Inhalte search_pattern nutzen).";
    }

    private static IEnumerable<ISymbol> FilterByKind(IEnumerable<ISymbol> symbols, string? kind)
    {
        if (kind is null) return symbols;

        return kind.ToLowerInvariant() switch
        {
            "class" => symbols.Where(s => s is ITypeSymbol { TypeKind: TypeKind.Class }),
            "interface" => symbols.Where(s => s is ITypeSymbol { TypeKind: TypeKind.Interface }),
            "method" => symbols.Where(s => s.Kind == SymbolKind.Method),
            "property" => symbols.Where(s => s.Kind == SymbolKind.Property),
            _ => symbols,
        };
    }

    private static string DescribeKind(ISymbol symbol)
    {
        if (symbol is ITypeSymbol { TypeKind: TypeKind.Class }) return "Klasse";
        if (symbol is ITypeSymbol { TypeKind: TypeKind.Interface }) return "Interface";
        if (symbol.Kind == SymbolKind.Method) return "Methode";
        if (symbol.Kind == SymbolKind.Property) return "Property";
        return symbol.Kind.ToString();
    }
}
```

**Wichtige Details:**

- **`FormatSymbolLocations` wird aus dem Tool aufgerufen** (Z.
  `"lines.Add(...)"`-Pattern im `SelectMany`): der Scanner nutzt
  die Methode über `FindSymbolTool.FormatSymbolLocations(...)`,
  weil sie 1:1 passt und im Tool bleiben muss (siehe Check 1).
  Das ist semantisch sauber: `FindSymbolTool` als API-Owner der
  Format-Methode, `FindSymbolScanner` als Konsument.
- **Miss-Hint-Pfad:** `missHits` ist `IReadOnlyList<string>` aus
  `SearchPatternScanner.GetFilesWithHits`. Trunkierung über
  `McpTruncation.TruncateFileList(missHits, missHits.Count)`.
  Bei `missHits.Count == 0` → nur `baseText` zurück.
- **Kein** `CancellationToken` im Scanner (kein
  cancellation-aware Code; Konsistenz mit `SearchPatternScanner`).
- **`DescribeKind`** wandert mit in den Scanner (private), wird
  aktuell nur intern verwendet — falls `FormatSymbolLocations`
  auf `DescribeKind` zugreift, ist `DescribeKind` über `internal`
  static erreichbar (selbe Datei → privater Zugriff OK, aber
  `FindSymbolTool.FormatSymbolLocations` braucht `DescribeKind`
  ebenfalls).
  → **Anpassung:** `DescribeKind` bleibt im Scanner **privat**,
  `FormatSymbolLocations` im Tool referenziert den Scanner-
  Klassenmember **nicht** direkt — `FormatSymbolLocations`
  ist eigenständig (`DescribeKind`-Aufruf wandert mit
  `FormatSymbolLocations` ins Tool). **Pragmatische Lösung:**
  `DescribeKind` bleibt **privat im Tool** (es ist nur 7 Z., und
  der Scanner braucht es nicht direkt — `FindMatchesAndFormat`
  ruft `FindSymbolTool.FormatSymbolLocations` auf, das wiederum
  intern `DescribeKind` aufruft). **Konkret:** `DescribeKind`
  bleibt im Tool bei `FormatSymbolLocations`. Scanner-Code-
  Skizze oben ist entsprechend zu korrigieren: kein
  `DescribeKind` im Scanner.
  → **Korrigierte Skizze:** Scanner hat `FindMatchesAndFormat` +
  `AppendMissHint` + `FilterByKind`. Tool behält
  `ExecuteAsync` + `FormatSymbolLocations` + `DescribeKind`.
- **`SymbolFilter.TypeAndMember`** bleibt unverändert
  (Such-Verhalten identisch zu 003).

**Korrektur-Check für den Coder:** vor dem Kopieren der
obigen Skizze die `DescribeKind`-Diskussion lesen und
Scanner entsprechend **ohne** `DescribeKind` schreiben.

### Schritt 3 — `FindSymbolTool.cs` auf dünner Dispatch reduzieren

Datei `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` umbauen. Die
neue Form (komplett):

```csharp
#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>find_symbol</c>: durchsucht die resident gehaltene Solution per Substring auf
/// Symbolnamen (optionaler Kind-Filter) und liefert Fundstellen (Datei:Zeile, Kind, Signatur).
/// Deckt nur .cs-Dateien ab (Roslyn-Symbolgraph). Trunkiert standardmaessig auf 50 Treffer,
/// ueberschreibbar via <c>maxResults</c>. Argument-Validierung lebt im Tool (nicht im Scanner),
/// damit der Scanner reine Daten bekommt und einfacher unit-testbar bleibt. Bewusst duenner
/// Dispatch auf <see cref="FindSymbolScanner.FindMatchesAndFormat"/> — keine eigene Scan- oder
/// Formatierungslogik (TD-005-Muster, analog <see cref="SearchPatternTool"/>), damit diese
/// Klasse eigener <c>AIContextFootprint</c> (siehe <c>AiNetLinter.mdc</c>) klein bleibt
/// (TD-012-Scanner-Split).
/// </summary>
internal static class FindSymbolTool
{
    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, und delegiert an den Scanner.
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string namePattern,
        string? kind,
        int maxResults,
        CancellationToken ct)
    {
        var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;

        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var text = await FindSymbolScanner.FindMatchesAndFormat(
            solution, namePattern, kind, normalizedMaxResults);
        return McpToolResults.Text(text);
    }

    /// <summary>
    /// Formatiert alle Quell-Fundstellen von <paramref name="symbol"/> als "Datei:Zeile - Kind:
    /// Signatur". Wird auch von <see cref="FindReferencesTool"/> fuer die Ambiguitaets-
    /// Fehlermeldung (Liste der Kandidaten) wiederverwendet. Bewusst im Tool (nicht im Scanner)
    /// geblieben, weil es eine tool-uebergreifend genutzte Format-Methode ist und nicht zur
    /// Scanner-Kernlogik gehoert (Konsument sitzt in einem anderen Tool).
    /// </summary>
    internal static IEnumerable<string> FormatSymbolLocations(ISymbol symbol, string outputRoot)
    {
        var kindLabel = DescribeKind(symbol);
        foreach (var location in symbol.Locations.Where(l => l.IsInSource))
        {
            var lineSpan = location.GetLineSpan();
            var relativePath = PathNormalizer.ToRelative(outputRoot, location.SourceTree!.FilePath);
            var line = lineSpan.StartLinePosition.Line + 1;
            yield return $"{relativePath}:{line} - {kindLabel}: {symbol.ToDisplayString()}";
        }
    }

    private static string DescribeKind(ISymbol symbol)
    {
        if (symbol is ITypeSymbol { TypeKind: TypeKind.Class }) return "Klasse";
        if (symbol is ITypeSymbol { TypeKind: TypeKind.Interface }) return "Interface";
        if (symbol.Kind == SymbolKind.Method) return "Methode";
        if (symbol.Kind == SymbolKind.Property) return "Property";
        return symbol.Kind.ToString();
    }
}
```

**Wichtige Details:**

- `ExecuteAsync` hat 5 Parameter (kein Default, da Schritt 0
  vermutlich ergibt, dass Defaults `MaxMethodParameterCount`
  reißen — siehe Schritt 0-Fallback). Der MCP-Delegate in
  `SymbolGraphToolRegistrations.cs` setzt den Default `50`.
- `FormatSymbolLocations` und `DescribeKind` bleiben im Tool
  (siehe Check 1, Wiederverwendung durch `FindReferencesTool`).
- **`using System.Threading;`** bleibt für `CancellationToken`.
- **Strikte `FilterByKind`-Entfernung:** die Methode lebt jetzt
  nur im Scanner.
- **Strikte `FindMatchesAsync`-Entfernung:** die Methode wird
  durch `FindSymbolScanner.FindMatchesAndFormat` ersetzt. **Bestehende
  Tests in `FindSymbolToolTests.cs` rufen `FindSymbolTool.FindMatchesAsync`**
  auf — diese Aufrufe müssen auf
  `FindSymbolScanner.FindMatchesAndFormat` umgestellt werden (siehe
  Schritt 6). Test-Datei wächst, aber `FindSymbolToolTests.cs` hat
  119/500 Z. (Puffer 381 Z.) — keine `MaxLineCount`-Gefahr.

### Schritt 4 — `SymbolGraphToolRegistrations.cs`: Delegate + Description

**Zwei Änderungen** an
`src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`:

1. **Delegate-Signatur anpassen** (Z. 26-27):
   ```csharp
   tools.Add(McpServerTool.Create(
       (string namePattern, string? kind = null, int maxResults = 50, CancellationToken ct = default) =>
           FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct),
       new McpServerToolCreateOptions
       {
           Name = "find_symbol",
           Description = "Sucht C#-Symbole (Klassen, Methoden, Properties, Interfaces) per " +
               "Substring im Namen. Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. " +
               "Bei 0 Treffern wird auf Textvorkommen in Nicht-C#-Dateien hingewiesen. " +
               "Trunkiert standardmaessig auf 50 Treffer, ueberschreibbar via maxResults; " +
               "Trunkierungs-Meta-Zeile meldet die Gesamt-Trefferzahl.",
       }));
   ```

2. **Description-Erweiterung** (+2 Sätze zur Trunkierung,
   ~50 Zeichen). Gesamt-Description: ~270 Zeichen (statt
   aktuell ~210 Zeichen). Geschätzter Footprint-Zuwachs
   `SymbolGraphToolRegistrations`: +2 Z. (String-Konkatenation
   über mehrere Zeilen, vorher/nachher vergleichbar) → 2488 + 2
   = 2490/2500 (Puffer 10 Z.). **Knapp, aber unter Limit.**
   Coder misst nach und passt Description an falls nötig (siehe
   Schritt 8 Pflicht-Re-Messung).

### Schritt 5 — Fixture-Erweiterung `Component.razor` + `Page.xaml`

**Pflicht-Verifikation vor Schritt 1 (im `result.md` wortwörtlich
dokumentieren):**

```powershell
cd C:/Daten/Entwicklung/Ralf/AiNetLinter
rg "userService" tests/Fixtures/SymbolGraphMini/ --type cs
```

Darf **nichts** liefern (003-Bedingung weiterhin gültig).

**Dann:**

1. `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/Component.razor`
   am Ende ergänzen um:
   ```html
   <!-- userService placeholder -->
   ```
2. `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/Page.xaml`
   am Ende ergänzen um:
   ```xml
   <!-- userService placeholder -->
   ```

**Erwarteter Effekt:** `userService` matcht jetzt in
`site.js` (003) + `Component.razor` (neu) + `Page.xaml` (neu) =
3 Nicht-C#-Dateien. Bei `maxFiles = 10` Default in
`TruncateFileList` wird die Liste NICHT trunkiert (3 ≤ 10), der
Test muss deshalb `maxFiles = 2` o. ä. setzen — **die
`TruncateFileList`-Methode akzeptiert `maxFiles` als Parameter,
Tests rufen sie mit `2` auf** (siehe Schritt 6).

### Schritt 6 — Tests: `FindSymbolScannerTests.cs` (neu)

Neue Datei
`src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs` mit
**5 Unit-Tests** (alle mit A3-Pflicht). Struktur analog
`SearchPatternToolTests.cs`:

```csharp
#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Collection("ConsoleTestCollection")]
public sealed class FindSymbolScannerTests
{
    [Fact]
    public async Task FindMatchesAndFormat_SubstringMatch_ReturnsFileLineAndKind()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "Greeter", kind: null, maxResults: 50);

        Assert.Contains("Greeter.cs", result);
        Assert.Contains("Klasse", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_TruncatesAtMaxResults_AppendsMetaLine()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        // "Greeter" matcht in Greeter.cs (Klasse) + Caller.cs/OtherCaller.cs (Aufrufstellen).
        // maxResults = 2 erzwingt Trunkierung.
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "Greeter", kind: null, maxResults: 2);

        // Meta-Zeile der Haupt-Treffer-Trunkierung.
        Assert.Contains("Treffer gesamt", result);
        Assert.Contains("2 gezeigt", result);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchAndNonCsHitTruncates_AppendsFileListMetaLine()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        // userService matcht in 3 Nicht-C#-Dateien (site.js, Component.razor, Page.xaml).
        // Trunkierung der Miss-Hint-Liste via McpTruncation.TruncateFileList mit
        // maxFiles = 2.
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "userService", kind: null, maxResults: 50);

        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        Assert.Contains("Dateien mit Textfund", result);
        Assert.Contains("2 gezeigt", result);
        Assert.Contains("search_pattern fuer Details", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "DoesNotExistXyzBlub123", kind: null, maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyzBlub123'", result);
        Assert.DoesNotContain("Hinweis: kein C#-Symbol", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_KindFilterExcludesNonMatchingKind()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "Greeter", kind: "method", maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'Greeter' (Kind-Filter: method)", result);
    }
}
```

**A3-Methodik pro Test** (vom Coder im `result.md` wortwörtlich
zu dokumentieren):

1. **Test 1 (Regressions-Test)**: passt vor und nach
   Trunkierungs-Code, weil `maxResults=50` weit über den
   3 Treffern liegt. A3 **nicht erforderlich** (additive
   Regression, A3-Plan-Hinweis 002-Pattern).
2. **Test 2 (Trunkierung Haupt-Output)**: A3-Auslöser =
   `McpTruncation.TruncateLines`-Aufruf im Scanner entfernen
   (durch `string.Join("\n", lines)` ersetzen) → Meta-Zeile
   fehlt → `Assert.Contains("Treffer gesamt")` schlägt fehl.
3. **Test 3 (Miss-Hint-Trunkierung)**: A3-Auslöser =
   `McpTruncation.TruncateFileList`-Aufruf durch
   `string.Join(", ", missHits)` ersetzen → Meta-Zeile fehlt →
   `Assert.Contains("Dateien mit Textfund")` schlägt fehl.
4. **Test 4 (Edge-Case)**: A3 = `SearchPatternScanner
   .GetFilesWithHits`-Aufruf in `AppendMissHint` entfernen →
   `Assert.DoesNotContain("Hinweis")` schlägt nicht fehl
   (Regression-Test auf den unveränderten Pfad) — **eigentlich
   implizit**, Coder entscheidet ob A3-Auslöser notiert wird.
5. **Test 5 (Kind-Filter-Regression)**: passt unabhängig von
   Trunkierung, A3 nicht erforderlich (additive Regression).

### Schritt 7 — Tests: `FindSymbolToolTests.cs` anpassen

Bestehende Datei
`src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs`
(Z. 1-119) modifizieren:

1. **Test 1 (Z. 14-23)**: Signatur anpassen — neuer
   `maxResults: 50`-Parameter im `ExecuteAsync`-Aufruf:
   ```csharp
   var result = await FindSymbolTool.ExecuteAsync(state, "irrelevant", null, 50, CancellationToken.None);
   ```
2. **Tests 2-8 (Z. 26-118)**: Aufrufe von
   `FindSymbolTool.FindMatchesAsync` (Z. 31, 44, 55, 67, 86, 100,
   115) umstellen auf
   `FindSymbolScanner.FindMatchesAndFormat` mit `maxResults: 50`
   (oder kleiner, wo sinnvoll — Test 4 (`NoMatch_Returns
   NoResultsText`) bleibt bei 50). Begründung: die
   `FindMatchesAsync`-Methode wandert in den Scanner umbenannt
   zu `FindMatchesAndFormat` (Scanner-API).
3. **Datei-Header:** `#nullable enable` ergänzen? — **Nein**
   (A2/A5: pre-existing Issue, 003-MINOR 2; strikt nach A2 nicht
   004-Scope). Coder entscheidet; **Empfehlung Planer: NEIN** für
   minimalen Scope.

**Erwartete Datei-Größe nach 004:** ~125-135 Z. (von 119 Z.).
Deutlich unter `MaxLineCount: 500` (Puffer ~370 Z.).

### Schritt 8 — E2E-Test (neue Datei, weil McpServerCommandTests.cs voll)

Neue Datei
`src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs`
(40-50 Z., 1-2 Tests):

```csharp
#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Threading;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Commands;

[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandFindSymbolTests
{
    [Fact]
    public async Task RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        Assert.True(File.Exists(exePath), $"Erwartete AiNetLinter.exe nicht gefunden: {exePath}");

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ainetlinter-mcp-test-client",
            Command = exePath,
            Arguments = ["--mcp-server", "--path", fixture.RootPath],
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
        var result = await client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "Greeter", ["maxResults"] = 2 },
            cancellationToken: cts.Token);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
    }
}
```

**A3-Methodik pro Test:**

1. **Test 1 (Trunkierung E2E)**: A3-Auslöser = `maxResults`-Param
   im `SymbolGraphToolRegistrations`-Delegate entfernen oder
   fest auf 50 setzen → `Assert.Contains("2 gezeigt")` schlägt
   fehl. **Coder prüft:** E2E-Auslöser ist aufwändiger (echter
   Subprozess), A3 ggf. als implizit dokumentieren (E2E-Tests
   sind ohnehin Regressions-Tests, A3 strikt nicht Pflicht
   — siehe 002-Plan Schritt 5-Logik).

**Hinweis zur Datei-Größe:** `MaxLineCount: 500` — Datei mit
1 Test ~45 Z., weiter Puffer für künftige Erweiterungen
analog `McpServerOptionsFactoryTests.cs` (003) und
`McpServerCommandFindSymbolTests.cs`.

### Schritt 9 — Build, Tests, Footprint-Messung (Pflicht)

1. **Build:**
   ```powershell
   cd C:/Daten/Entwicklung/Ralf/AiNetLinter
   dotnet build AiNetLinter.slnx
   ```
   Erwartung: 0 Warnungen, 0 Fehler. Zero-Warning-Direktive
   eingehalten (`TreatWarningsAsErrors=true`).
2. **Targeted Tests** (schnelle Verifikation der neuen Tests):
   ```powershell
   dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj --no-build `
     --filter "FullyQualifiedName~FindSymbolScanner|FullyQualifiedName~McpServerCommandFindSymbol|FullyQualifiedName~FindSymbolTool"
   ```
   Erwartung: alle neu + modifizierten Tests grün.
3. **Volle Test-Suite** (Regressions-Schutz):
   ```powershell
   dotnet test AiNetLinter.slnx --no-build
   ```
   Erwartung: alle Tests grün, Volllauf ~8 min. Test-Anzahl
   1101 (vor 004) + 5 (Scanner) + 1 (E2E) = **1107** Tests.
4. **Pflicht-Footprint-Messung** (TD-011/TD-014):
   ```powershell
   dotnet run --project src/AiNetLinter -- --footprint FindSymbolTool --path .
   dotnet run --project src/AiNetLinter -- --footprint FindSymbolScanner --path .
   dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
   dotnet run --project src/AiNetLinter -- --footprint McpServerOptionsFactory --path .
   dotnet run --project src/AiNetLinter -- --footprint McpTruncation --path .
   ```
   Ergebnis-Tabelle ins `result.md` analog 002/003.
   **Trigger-Bewertung** im `result.md`:
   - Wenn eine Klasse reißt: Description in
     `SymbolGraphToolRegistrations` weiter kürzen (kein
     PathOverride).
   - Wenn `McpServerOptionsFactory` unerwartet wächst: prüfen,
     ob versehentlich etwas geändert wurde (sollte nicht —
     004 hat keinen Touch dort).
5. **Self-Lint** (Dogfooding, Coder macht das wie 003):
   ```powershell
   dotnet run --project src/AiNetLinter -- --path . --config rules.json
   ```
   Erwartung: 0 Violations (analog 003-Dogfooding).

### Schritt 10 — Dogfooding gegen AiNetLinter.slnx (PFLICHT pro Tool-Step)

Analog 003-Plan Schritt 8 + Konzept Z. 193-204. Coder macht
einmal ad-hoc:

1. Server starten: `dotnet run --project src/AiNetLinter --
   --mcp-server --path .`
2. Per `McpClient` oder direktem JSON-RPC:
   - `find_symbol` mit `namePattern: "FindSymbol"` (mehrere
     Treffer) + `maxResults: 2` → Trunkierung prüfen.
   - `find_symbol` mit `namePattern: "Kritiker"` (existiert
     in `units/003/`) → 0 C#-Treffer, Miss-Hint in
     `tasks/`-Markdown? **Nein**, das ist keine Nicht-C#-Datei
     im Solution-Projekt (Tasks sind außerhalb). Coder
     verwendet einen Identifier, der nur in einer Web-Datei der
     AiNetLinter-slnx vorkommt (z. B. `analyticsDashboard` in
     einer `.html`-Datei — vorher prüfen, ob vorhanden).
     **Falls kein passender Identifier:** Dogfooding-Eintrag
     im `result.md` mit Hinweis "Miss-Hint-Pfad im
     SymbolgraphMini-Fixture getestet (siehe Test 3), im
     Dogfooding nicht reproduzierbar, weil AiNetLinter.slnx
     keine passende .js/.razor/.xaml-Datei mit eindeutigem
     Nicht-C#-Identifier hat". **Bewusst kein Scope-Creep
     in 004**, um eine passende Datei anzulegen.
3. Output dokumentieren im `result.md` unter "Dogfooding"
   (4-5 Zeilen pro Szenario, wie 003-Result).

### Schritt 11 — Commit (A4 + Conventional Commits)

Gezielter `git add`:

```powershell
cd C:/Daten/Entwicklung/Ralf/AiNetLinter
git --no-pager add src/AiNetLinter/Mcp/McpTruncation.cs
git --no-pager add src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs
git --no-pager add src/AiNetLinter/Mcp/Tools/FindSymbolScanner.cs
git --no-pager add src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs
git --no-pager add src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs
git --no-pager add src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs
git --no-pager add src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs
git --no-pager add tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/Component.razor
git --no-pager add tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/Page.xaml
```

**Commit-Message (Conventional Commits, deutsch, imperativ,
Task-Suffix):**

```
feat(mcp): find_symbol trunkierung + scanner-split [codegraph-mcp-server]
```

Body (mehrzeilig, analog 002/003-Commit-Bodies):

- Stichpunkt: P0/P1-Trunkierung in `find_symbol`
  (`maxResults`-Default 50, `McpTruncation.TruncateLines`).
- Stichpunkt: TD-012 inline Scanner-Split
  (`FindSymbolScanner.cs`).
- Stichpunkt: TD-013 inline Miss-Hint-Trunkierung
  (`McpTruncation.TruncateFileList`).
- Stichpunkt: `SymbolGraphToolRegistrations` Description
  erweitert um `maxResults`-Hinweis.
- Stichpunkt: 5 neue Unit-Tests + 1 modifizierter E2E-Test
  (in neuer Datei `McpServerCommandFindSymbolTests.cs`, weil
  `McpServerCommandTests.cs` 499/500 Z. erreicht).
- Stichpunkt: SymbolGraphMini-Fixture erweitert
  (`Component.razor` + `Page.xaml` mit `userService`-Marker,
  Eindeutigkeit per `rg` verifiziert).
- Stichpunkt: Footprint-Situation dokumentiert im `result.md`.

**Kein Push** (A4). Kein `--amend`, kein `rebase`, kein
Force-Push.

## Erwartete Tests (mit A3-Methodik)

| # | Datei | Test | Status | A3-Auslöser (Stichpunkt) |
|---|---|---|---|---|
| 1 | `FindSymbolScannerTests.cs` | `FindMatchesAndFormat_SubstringMatch_ReturnsFileLineAndKind` | neu | implizit (Regression) |
| 2 | `FindSymbolScannerTests.cs` | `FindMatchesAndFormat_TruncatesAtMaxResults_AppendsMetaLine` | neu | `McpTruncation.TruncateLines`-Aufruf im Scanner entfernen |
| 3 | `FindSymbolScannerTests.cs` | `FindMatchesAndFormat_NoCsMatchAndNonCsHitTruncates_AppendsFileListMetaLine` | neu | `McpTruncation.TruncateFileList`-Aufruf durch `string.Join(", ", missHits)` ersetzen |
| 4 | `FindSymbolScannerTests.cs` | `FindMatchesAndFormat_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText` | neu | implizit (Regression) |
| 5 | `FindSymbolScannerTests.cs` | `FindMatchesAndFormat_KindFilterExcludesNonMatchingKind` | neu | implizit (Regression) |
| 6 | `FindSymbolToolTests.cs` | 7 bestehende Tests (Z. 14-118) | modifiziert | implizit (Signatur-Anpassung, Scanner-Umzug) |
| 7 | `McpServerCommandFindSymbolTests.cs` | `RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates` | neu | E2E-Auslöser aufwändig, als implizit dokumentieren (E2E-Regression) |

**Gesamt:** 5 neue Unit-Tests + 1 neuer E2E-Test + 7
modifizierte Unit-Tests (Signatur-/Aufruf-Anpassung). Volllauf
1101 → 1107 Tests.

**A3-Pflicht-Dokumentation:** alle **neuen** Unit-Tests (Tests
1-5) brauchen A3-Nachweis im `result.md` (temporäre Änderung,
Test-Lauf mit Filter auf genau diesen Test, wortwörtlicher
Failure-Output, Revert). Format analog 002/003-Result.

## Footprint-Messung (TD-011 Pflicht)

**Vor 004** (gemessen 2026-08-01 14:18-14:19, Stand `5b962dd`):

| Klasse | Z. | Limit | Puffer |
|---|---:|---:|---:|
| `FindSymbolTool` | 2529 | 2700 (PathOverride) | 171 |
| `SymbolGraphToolRegistrations` | 2488 | 2500 | 12 |
| `McpServerOptionsFactory` | 2484 | 2500 | 16 |
| `McpTruncation` | 44 | 2500 | — |

**Erwartung nach 004:**

| Klasse | Z. (Erwartung) | Limit | Δ | Begründung |
|---|---:|---:|---:|---|
| `FindSymbolTool` | **~2440-2460** | 2700 (PathOverride) | **-70 bis -90** | Scanner-Split entfernt Logik (Filter, DescribeKind, FindMatchesAsync); nur Dispatch + FormatSymbolLocations bleiben |
| `FindSymbolScanner` (neu) | **~250-300** | 2500 | +neu | Scanner-Logik ohne `McpCodeGraphServer`-Pull-in, vergleichbar `SearchPatternScanner` 179 Z. + 70-120 Z. für Trunkierungs-Integration |
| `SymbolGraphToolRegistrations` | **~2490-2492** | 2500 | +2 bis +4 | Description-Erweiterung um 2 Sätze |
| `McpServerOptionsFactory` | **2484** (unverändert) | 2500 | 0 | 004 hat keinen Touch auf dieser Klasse |
| `McpTruncation` | **~70-75** | 2500 | +25-30 | `TruncateFileList`-Methode hinzu |

**Coder-Pflicht** im `result.md`: Tabelle mit **gemessenen**
Werten nach 004, inkl. Δ-Spalte. Falls eine Klasse
unerwartet über Limit reißt: Trigger-Bewertung mit
konkreter Entscheidung (Description-Kürzung statt
PathOverride-Erhöhung, TD-008-Präzedenz bewusst vermeiden).

## Bezug zu Projektregeln

| Regel (Datei) | Kurzgrund |
|---|---|
| `AiNetLinter.mdc` → `MaxLineCount: 500` | Scanner-Datei ~100 Z., Test-Dateien ~120-160 Z., alle deutlich unter 500. `McpServerCommandTests.cs` ist 499/500 — wird **nicht** angefasst. |
| `AiNetLinter.mdc` → `MaxMethodLineCount: 60` (Produktion), `100` (Tests) | Alle neuen Methoden ≤ 30 Z. ✓ |
| `AiNetLinter.mdc` → `MaxMethodParameterCount: 4` | `TruncateFileList` 3 P. ✓. `ExecuteAsync`/`FindMatchesAndFormat` 5 P. **knapp/gerissen** — siehe Schritt 0-Fallback. |
| `AiNetLinter.mdc` → `MaxAIContextFootprint: 2500` (PathOverride 2700 für `FindSymbolTool`) | Scanner-Split reduziert `FindSymbolTool` voraussichtlich deutlich, Scanner neu klein (vergleichbar `SearchPatternScanner`). `SymbolGraphToolRegistrations` knapp (12 Z. Puffer vor 004). |
| `AiNetLinter.mdc` → `EnforceNullableEnable` | Alle neuen Dateien mit `#nullable enable` Z. 1. Bestehende `FindSymbolToolTests.cs` hat es nicht (003-MINOR 2) — **nicht** 004-Scope. |
| `AiNetLinter.mdc` → `EnforceSealedClasses` | Test-Klassen `sealed` ✓. Produktiv-Klassen `internal static` (kein `sealed`-Modifier auf `static class` nötig/nutzbar). |
| `AiNetLinter.mdc` → `EnforceAsciiIdentifiers` | Deutsche Umlaut-Ersetzungen (`fuer`, `ue`, `Bestaetigung`) in Code-Bezeichnern ✓. |
| `AiNetLinter.mdc` → `EnforceNamespaceDirectoryMapping` | `AiNetLinter.Tests.Mcp.Tools.FindSymbolScannerTests` matched `src/AiNetLinter.Tests/Mcp/Tools/` ✓. `AiNetLinter.Tests.Commands.McpServerCommandFindSymbolTests` matched `src/AiNetLinter.Tests/Commands/` ✓. |
| `AiNetLinter.mdc` → `EnforcePascalCase` | Öffentliche Typen/Methoden PascalCase ✓. |
| `AiNetLinterRichtlinien.mdc` §1 | Monolithisch & schlank — keine Plugin-Architektur, keine Abstraktion ohne Mehrwert (Scanner-Split ist TD-005-Generalisierung mit echtem Mehrwert). |
| `AiNetLinterRichtlinien.mdc` §2 | Kein DI-Container — Scanner/Tool sind `internal static class` ✓. |
| `AiNetLinterRichtlinien.mdc` §4 | xUnit v3 Tests, Commit-Vorschlag (in `result.md` und Commit-Message, Schritt 11). |
| `AiNetLinterRichtlinien.mdc` §5 | Zero-Warning-Direktive, sparsame Kommentare, Conventional Commits deutsch imperativ. |

## Annahmen und offene Fragen an den Coder

1. **Schritt-0-Trigger (maxResults-Parameter-Anzahl):** welche
   Variante greift (Default-Param oder explizit-Param)? Siehe
   Schritt 0 — Coder entscheidet, dokumentiert im `result.md`.

2. **`DescribeKind`-Verbleib:** der Plan geht davon aus, dass
   `DescribeKind` im Tool bleibt (weil nur von
   `FormatSymbolLocations` aufgerufen, und die Methode ist
   `FormatSymbolLocations`-intern). Falls Coder beim Refactoring
   `DescribeKind` doch in den Scanner verschieben will: `internal`
   static machen, `FormatSymbolLocations` ruft
   `FindSymbolScanner.DescribeKind(...)` auf. Symmetrie-Frage;
   Planer-Empfehlung: **im Tool behalten**.

3. **Trunkierung Haupt-Output + Miss-Hint in derselben Antwort:
   Reihenfolge.** Plan-Skizze: Miss-Hint kommt **nach** dem
   Haupt-Output (passiert nur bei 0 Treffern, also kein
   Konflikt). Aber: bei `maxResults = 0` und gleichzeitig
   Miss-Hint-Vorhandensein — Sonderfall. Coder normalisiert
   `maxResults < 1 → 1` im Tool (Schritt 3), damit der
   Haupt-Output-Pfad nie eine leere Trunkierung mit
   `[0 Treffer gesamt, 1 gezeigt …]`-Meta-Zeile ausgibt.

4. **Bestehender `RunAsync_ValidFixture_FindSymbolReturnsMatch`
   (Z. 273 in `McpServerCommandTests.cs`):** muss angepasst werden
   (neue `ExecuteAsync`-Signatur mit `maxResults`)? **Nein** —
   der MCP-Delegate in `SymbolGraphToolRegistrations.cs` hat den
   Default `maxResults = 50` direkt im Delegate (Schritt 4), der
   bestehende E2E-Test ruft `find_symbol` mit nur `namePattern`
   auf, der Default greift. **Kein Eingriff in
   `McpServerCommandTests.cs` nötig.**

5. **`McpServerCommandFindSymbolTests.cs` (neue E2E-Datei) im
   richtigen Ordner:** `src/AiNetLinter.Tests/Commands/`
   (analog `McpServerCommandTests.cs`). Namespace
   `AiNetLinter.Tests.Commands`. `Collection("ConsoleTestCollection")`
   für Thread-Isolation analog `McpServerOptionsFactoryTests.cs`.

6. **TD-013-Schließung im `tech-debt.md`:** nicht durch den
   Coder (A7 — keine Edits an `tech-debt.md` durch den Coder).
   Der Planer dokumentiert TD-013 als geschlossen in 004 (TD-012
   ebenfalls) — der **Orchestrator** editiert `tech-debt.md`,
   nicht der Coder. Analog TD-009 in 001 (Orchestrator-Edits).

7. **Komplexität `AppendMissHint`:** die Scanner-Methode ist
   klein (~10 Z.), bleibt deutlich unter
   `MaxMethodLineCount: 60`. `MaxCyclomaticComplexity: 12` /
   `MaxCognitiveComplexity: 15` (siehe 003-Review Ebene 2) — 1
   if, 1 string-concat, 1 return → trivial.

8. **`FindMatchesAndFormat` async/sync:** Methode ist `async
   Task<string>`, weil `SymbolFinder.FindSourceDeclarationsAsync`
   awaitet wird. `ExecuteAsync` im Tool awaited den Scanner
   direkt (kein `Task.Run`-Wrapper wie bei `SearchPatternTool` —
   SymbolFinder ist Roslyn-async, Datei-Scan ist nicht im Hot-
   Path, simpler direkter `await` reicht). Begründung im
   Code-Kommentar andeuten, nicht ausschmücken (A5: sparsame
   Kommentare).

9. **Fixture-Erweiterung Concurrency:** die beiden
   Fixture-Erweiterungen (`Component.razor`, `Page.xaml`) werden
   in 1 Commit mit dem Code-Commit zusammengeführt (analog 003
   `site.js`-Erweiterung). Eine Commit-Operation, ein Coder, ein
   Commit (A1).

10. **McpTruncation-Footprint:** derzeit 44 Z., mit
    `TruncateFileList`-Methode ~70-75 Z. (grobe Schätzung:
    +30 Z. inkl. XMLDoc). 2500 Limit irrelevant. **Aber:**
    `MaxMethodLineCount: 60` für die neue Methode (~12 Z.
    Body) und `MaxMethodParameterCount: 4` für 3 Parameter ✓.

## Harte Scope-Grenze (wiederholt)

**004 macht:**

- `maxResults` (Default 50) in `find_symbol` (P0/P1-Pflicht).
- Scanner-Split `FindSymbolScanner.cs` (TD-012 inline).
- Miss-Hint-Datei-Liste trunkiert (TD-013 inline, neue
  `McpTruncation.TruncateFileList`-Methode).
- `SymbolGraphToolRegistrations.find_symbol`-Description um
  Trunkierungs-Hinweis erweitert.
- 5 neue Unit-Tests (`FindSymbolScannerTests.cs`) + 1 neuer
  E2E-Test (`McpServerCommandFindSymbolTests.cs`) + 7
  modifizierte Unit-Tests (`FindSymbolToolTests.cs`).
- SymbolGraphMini-Fixture erweitert (`Component.razor` +
  `Page.xaml`).

**004 macht NICHT:**

- **Keine** Trunkierung in `find_references` oder `get_impact`
  (005/006, getrennt).
- **Keine** EPIC-06/07/08-Themen.
- **Keine** sonstigen P0/P1-Extensions (Kaltstart, Auto-
  Discovery, Staleness-Sweep, `--mcp-log`, etc.).
- **Keine** Änderung an `McpServerOptionsFactory`,
  `McpCodeGraphServer`, `McpToolResults`, `LinterErrorFormatter`.
- **Keine** `PathOverrides`-Wert-Erhöhung in `rules.json`.
- **Keine** Änderung an `SearchPatternScanner`,
  `SearchPatternTool`, `FindReferencesTool`, `GetImpactTool`,
  `GetTypeHierarchyTool`, `GetFileSkeletonTool`.
- **Keine** Doku (`Docs/agent-api.md`, `Docs/ROADMAP.md`,
  `README.md`).
- **Keine** Konzept- oder Projektregel-Edits (A7).
- **Keine** Edits an `tech-debt.md` durch den Coder (A7;
  Orchestrator dokumentiert TD-012/TD-013-Schließung).
- **Kein** Push (A4).
- **Keine** Historie-Rewrites (A4).
- **Kein** pre-existing `#nullable enable`-Fix in
  `FindSymbolToolTests.cs` (A2/A5 — strikt nicht 004-Scope).
- **Keine** E2E-Test-Erweiterung in `McpServerCommandTests.cs`
  (VOLL mit 499/500 Z., siehe 003-Review MINOR 1).
