---
unit: 002
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-01
epic: EPIC-04 (search_pattern, letztes Tool)
extends:
  - Trunkierung + maxResults (P0/P1, konzept.md Z. 215-225)
  - Plain-Text-Format mit einheitlicher Meta-Zeile (P0/P1, konzept.md Z. 226-233)
  - search_pattern-API als importierbarer Mechanismus für EPIC-05 Miss-Hint (003)
---

# Plan Einheit 002 — `search_pattern` Tool (letztes EPIC-04, inkl. P0/P1 Trunkierung + maxResults)

## Ziel der Einheit

Das neunte und letzte Tool des MCP-Servers umsetzen: `search_pattern` —
Plain-Text- oder Regex-Suche über den **gesamten** Solution-Dateibestand
(anders als `find_symbol` nicht auf C# beschränkt), primär als Fallback
für Namen/Strings in `.js`/`.razor`/`.xaml`/`.html`/`.css`-Dateien, die
der Symbolgraph nicht abdeckt. Die Einheit übernimmt zusätzlich die in
`konzept.md` Z. 206 als "übernommen ins Scope" markierten P0/P1-Punkte
für **alle** Listen-Tools — in 002 entsteht der Trunkierungs-Helper und
die `maxResults`-Standardverdrahtung an `search_pattern`; der Einbau in
`find_symbol`/`find_references`/`get_impact` bleibt bewusst separaten
Folge-Einheiten vorbehalten (Begründung unten). Bezug: `konzept.md`
Z. 95-97 (Tool-Set-Tabellen-Eintrag), Z. 215-225 (Trunkierungs-DoD),
Z. 226-233 (Plain-Text-Format-DoD), Z. 604-606 (Miss-Hint-DoD, mittelbar
durch 002-API), Z. 651-652 (Trunkierungs-DoD, geliefert für
`search_pattern`).

## Vor-der-Planung-Checks (Kernel Teil B "Drift" / "Duplikate durch Blindheit")

### Check 1 — Existierende Datei-Scan-Logik (`SafeEnumerateFiles`/`IsGeneratedPath`)

**Befund (gelesen):**

- `src/AiNetLinter/Web/WebFileCatalog.cs:105-113` (`SafeEnumerateFiles`)
  und `:149-155` (`IsGeneratedPath`) — beide `private`, identische
  Implementierungen.
- `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs:78-94` — dupliziert
  **wortgleich** (TD-006).
- Keine dritte Stelle, an der dieselbe Logik nochmal lebt.

**Entscheidung im Plan (nicht eigenmächtig, sondern begründet +
Varianten aufgeführt):**

- **Default für 002 (Empfehlung Planer):** `SearchPatternScanner.cs`
  dupliziert `SafeEnumerateFiles`/`IsGeneratedPath` ein drittes Mal
  (private Helper, 1:1 aus `GetIndexScopeScanner.cs:78-94` übernommen).
  Begründung: konsistent mit dem bestehenden 2×-Muster, **keine**
  Scope-Erweiterung über `search_pattern` hinaus, `WebFileCatalog`-Refactor
  ist eine TD-006-Schließung und gehört in eine eigene Einheit (jenseits
  EPIC-04, jenseits dieser Einheit).
- **Variante (zur Coder-Entscheidung freigegeben):** `SafeEnumerateFiles`
  und `IsGeneratedPath` in `WebFileCatalog` auf `internal static`
  anheben, von `SearchPatternScanner.cs` wiederverwenden. **Achtung:**
  das wäre eine API-Änderung am `WebFileCatalog` ohne Nutzungs-Pflicht
  in `GetIndexScopeScanner` — risikolos für 002, aber in 003/004+ als
  Aufräumer-Schritt zu überlegen (GetIndexScopeScanner nachziehen). Für
  002 nicht zwingend.
- **Nicht in 002:** eine eigene `FileSystemScanHelpers`-Klasse
  einführen und alle drei Stellen refaktorieren. Das wäre die saubere
  TD-006-Schließung — bewusst nicht in 002, weil Scope-Creep.

**TD-006 bleibt nach 002 unverändert offen** (kein Eingriff in
`tech-debt.md` durch den Planer, A7 — Coder/Kritiker entscheiden).

### Check 2 — Existierende Trunkierungs-Logik

**Befund:** keine. `McpToolResults.cs:107 Z.` enthält 5 Methoden
(`Error`, `SolutionNotLoaded`, `SymbolNotFound`, `AmbiguousSymbol`,
`InvalidArgument`, `FileNotFound`, `Text`) — keine Trunkierung.

**Entscheidung im Plan:**

- Neuer Helper **`src/AiNetLinter/Mcp/McpTruncation.cs`** (eigene Datei
  neben `McpToolResults.cs`) — entspricht der konzept.md-Formulierung
  "Trunkierungs-Helper gehört neben `Mcp/McpToolResults.cs`" wörtlich
  ("neben" = sibling-Datei, nicht "innerhalb"). Signatur:
  ```csharp
  internal static string TruncateLines(
      IReadOnlyList<string> hitLines,
      int totalMatches,
      int maxResults)
  ```
  Liefert entweder die unveränderte `string.Join("\n", hitLines)`-Zeile
  (wenn `totalMatches <= maxResults`) oder den ersten-`maxResults`-Slice
  + Meta-Zeile `[N Treffer gesamt, M gezeigt — Pattern verfeinern oder
  maxResults erhöhen]`. Format der Meta-Zeile ist **fix** (P0/P1) und
  landet im Plan-Beispiel wörtlich; abweichender Wortlaut = Scope-Creep
  (A6 → `blocked`).

- **Bewusst NICHT in 002:** Einbau des Helpers in die drei bestehenden
  Listen-Tools (`find_symbol`/`find_references`/`get_impact`). Begründung
  Scope-Trennung: ein "Trunkierung einbauen in `find_symbol`"-Schritt
  ist ein eigenständiges Tool-Touch mit eigenem A3-Nachweis pro
  existierendem Test (Trunkierung darf das bestehende
  Symbol-Treffer-Verhalten nicht subtil verändern), eigenem
  Footprint-Re-Run pro Tool (durch `McpCodeGraphServer`-Pull-in
  gefährdet) und eigenem Kritiker-Review. Das sind 3 separate Einheiten
  (003 = `find_symbol`+Trunkierung, 004 = `find_references`+Trunkierung,
  005 = `get_impact`+Trunkierung) bzw. eine konsolidierte Einheit, die
  der nächste Planer schneidet. **002 liefert die API, 003+ bauen sie
  ein.**

### Check 3 — Existierende Text/Regex-Scan-Logik

**Befund:** keine performante Text/Regex-Suche über Dateibestand im
gesamten Produktions-Code. `WebFileCatalog` filtert nur nach
Dateitypen, kein Inhalts-Scan. Lint-Checker arbeiten token-basiert via
Roslyn, nicht regex-basiert über das Dateisystem.

**Entscheidung im Plan:** `Regex.Match` mit `File.ReadAllLines`-Schleife
(plain sequential) pro Datei. **Keine Parallelisierung in 002**
(Konzept: Last-Fixture-Messlauf ist EPIC-08-Erweiterung — `konzept.md`
Z. 295-304). Begründung: die SymbolGraphMini-Fixture hat ~10 Dateien,
die reale `AiNetLinter.slnx` ~50 — sequential unter einer Sekunde. Wenn
EPIC-08 bei 500/5000 Dateien Probleme zeigt, nachmessen und in einer
späteren Einheit `Parallel.ForEachAsync` einbauen (kein
Performance-Scope-Creep in 002).

### Check 4 — `McpCodeGraphServer`-Footprint (TD-004 Pflichtmessung)

**Gemessen** (`--footprint <Klasse> --path .` heute 10:55, Stand
`e63176d`):

| Klasse | transitive Z. | Limit 2500 | Puffer |
|---|---:|---:|---:|
| `SymbolGraphToolRegistrations` | 2487 | 2500 | **13** |
| `FileStructureToolRegistrations` | 2480 | 2500 | **20** |
| `AnalysisToolRegistrations` | 2459 | 2500 | **41** |
| `McpServerOptionsFactory` | 2470 | 2500 | 30 |
| `McpToolResults` | 107 | 2500 | — |
| `McpCodeGraphServer` | 2416 | 2500 | 84 |
| `GetViolationsTool` | 2451 | 2500 | 49 |
| `GetHotspotsTool` | 2447 | 2500 | 53 |
| `GetIndexScopeTool` | 2445 | 2500 | 55 |
| `FindReferencesTool` | 2519 | 2700 (PathOverride) | 181 |
| `FindSymbolTool` | 2518 | 2700 (PathOverride) | 182 |
| `GetViolationsScanner` | 1834 | 2500 | — |

(volle Tabelle mit allen Tool-Klassen im Anhang dieses Plans.)

**Semantische Einordnung:** `search_pattern` ist konzeptuell näher an
`get_violations` (Analyse über Dateiinhalte) als an
`get_file_skeleton`/`get_hotspots` (Struktur-Skelette). Der
XMLDoc-Kommentar in `AnalysisToolRegistrations.cs:17` ist explizit
**vorbereitet** für `search_pattern`: *"Vorbereitet fuer das
verbleibende EPIC-04-Tool `search_pattern`."*

**Entscheidung im Plan (TD-004-Vorhersage aus `tech-debt.md` Z. 33/64
widerlegt):**

- **Keine 4. Registrar-Klasse in 002.** `AnalysisToolRegistrations`
  nimmt den `search_pattern`-Block auf. Geschätzter Block-Umfang
  analog `get_violations`-Block (~13-17 Z., konkret: Delegate
  1 Z., `McpServerTool.Create(...)`-Wrapper 2 Z.,
  `McpServerToolCreateOptions` 1 Z., `Name`-Zeile 1 Z., `Description`
  ca. 6-7 Z. weil länger als `get_violations` — nennt Fallback-Charakter
  + Trunkierung + `maxResults`, `}`/`}));` 2 Z.) → **+15-17 Z.** auf
  aktuelle 2459 = **2474-2476 / 2500**, Puffer 24-26 Z. nach Addition.
- **Konsequenz für Folge-Einheiten:** Wenn 003 (Miss-Hint in
  `find_symbol`) oder ein weiteres analyse-orientiertes Tool in
  `AnalysisToolRegistrations` hinzukommt, **muss der nächste Planer
  re-messen** — bei einem vierten Tool in dieser Klasse ist die
  4. Registrar-Klasse wahrscheinlich. Der Coder dieser Einheit ist
  nicht für diese Prognose verantwortlich (kein Vorausplanen).
- **TD-004 wird durch 002 nicht behoben** (kein Eingriff in
  `tech-debt.md` durch den Planer, A7) — Status "offen" bleibt; die
  Mess-Tabelle oben ersetzt die ursprüngliche "voraussichtlich"-Formulierung
  in TD-004 als belegte Faktenbasis für den nächsten Planer.

## Betroffene Dateien / Module

### Neu zu erstellen

| Datei | Zweck | Geschätzte Größe |
|---|---|---|
| `src/AiNetLinter/Mcp/McpTruncation.cs` | Trunkierungs-Helper (sibling zu `McpToolResults.cs`) | ~40 Z. |
| `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` | Dünner Dispatch (TD-005-Muster, vgl. `GetViolationsTool.cs`) | ~30 Z. |
| `src/AiNetLinter/Mcp/Tools/SearchPatternScanner.cs` | Reine Scan-/Format-Logik ohne `McpCodeGraphServer`-Dependency (TD-005-Muster) | ~120-140 Z. |
| `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs` | Unit-Tests + A3-Fehlschlag-Nachweis | ~150-180 Z. |

### Zu modifizieren

| Datei | Änderung |
|---|---|
| `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` | `search_pattern`-Block in `Register` (Z. 27-40) ergänzen, XMLDoc (Z. 9-18) aktualisieren ("aktuell `get_violations` und `search_pattern`") |
| `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` | `ServerRespondsWithEightTools` → `ServerRespondsWithNineTools` (Z. 134 + Assert auf 9 + `Assert.Contains(..., t => t.Name == "search_pattern")`), neuer E2E-Test `RunAsync_ValidFixture_SearchPatternReturnsExpectedHit` analog `…GetViolationsReturnsAtLeastOneViolation` (Z. 217) |
| `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs` | Erwartung `.cs: 5` bleibt unverändert (5 .cs-Dateien), keine Anpassung |
| `src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs` | unverändert |

**Nicht modifiziert** (bewusst):

- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — der 3. Aufruf
  `AnalysisToolRegistrations.Register(tools, mcpState)` in Z. 44 bleibt;
  der `search_pattern`-Block wird **innerhalb** des bestehenden
  `AnalysisToolRegistrations.Register`-Aufrufs registriert, nicht durch
  einen neuen Registrar und neuen Aufruf.
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — `search_pattern`
  braucht **keine** neue Dependency (kein `--mcp-log`, kein
  "lädt noch"-Zustand, keine Config-Property). Damit kein
  TD-009-Risiko (Konstruktor bleibt bei 5/5).
- `rules.json` — kein neuer `PathOverride` erwartet; der `SearchPatternTool`
  wird voraussichtlich im Bereich 2440-2470 landen (vgl. `GetIndexScopeTool`
  2445, `GetHotspotsTool` 2447). Falls die Messung am Ende 2500
  überschreitet: Coder ergänzt **gezielt** `PathOverrides` analog
  `FindSymbolTool`/`FindReferencesTool` (MaxAIContextFootprint: 2700).
- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` und
  `FileStructureToolRegistrations.cs` — `search_pattern` ist semantisch
  kein C#-Symbolgraph-Tool und keine Datei-Struktur, sondern
  datei-inhalts-basiert (wie `get_violations`).
- `konzept.md`, `tech-debt.md`, `state.md`, Projektregeln — A7 (nur lesen).

## Konkretes Vorgehen (Schritt-für-Schritt für den Coder)

### Schritt 1 — `McpTruncation.cs` anlegen

Datei `src/AiNetLinter/Mcp/McpTruncation.cs` mit folgender API:

```csharp
internal static class McpTruncation
{
    /// <summary>
    /// Liefert hitLines als "\n"-verbundene Textzeilen. Wenn totalMatches > maxResults,
    /// werden nur die ersten maxResults Zeilen zurueckgegeben und eine Meta-Zeile
    /// [N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]
    /// angehaengt. Format entspricht konzept.md Z. 230-233 (P0/P1, Plain-Text,
    /// einheitlich fuer alle Listen-Tools).
    /// </summary>
    internal static string TruncateLines(
        IReadOnlyList<string> hitLines,
        int totalMatches,
        int maxResults)
    {
        if (totalMatches <= maxResults)
        {
            return string.Join("\n", hitLines);
        }

        var shown = hitLines.Count <= maxResults ? hitLines : hitLines.Take(maxResults).ToList();
        var meta = $"[{totalMatches} Treffer gesamt, {maxResults} gezeigt — Pattern verfeinern oder maxResults erhöhen]";
        return string.Join("\n", shown) + "\n" + meta;
    }
}
```

**Constraints:**

- `#nullable enable` am Dateianfang.
- `internal static class` (analog `McpToolResults`).
- `MaxMethodLineCount` ≤ 60: Methode ist < 20 Z., passt.
- `MaxMethodParameterCount` ≤ 4: 3 Parameter, passt.
- Kein `try/catch`, keine Config-Logik (reine Formatierung).
- **Reihenfolge-Edge-Case:** wenn `hitLines.Count < totalMatches`
  (z. B. wenn der Aufrufer aus Speicher-Gründen vorab trunkiert hat),
  schreibt die Meta-Zeile den `totalMatches`-Wert — das ist die Quelle
  der Wahrheit, nicht `hitLines.Count`. So bleibt die Meta-Zeile auch
  dann korrekt, wenn der Aufrufer z. B. in einer künftigen EPIC-08-
  Optimierung schon vor-trunkiert.

### Schritt 2 — `SearchPatternScanner.cs` anlegen

Datei `src/AiNetLinter/Mcp/Tools/SearchPatternScanner.cs` mit folgender
öffentlicher API (zwei Methoden — die erste für `search_pattern`, die
zweite als **importierbarer Mechanismus für EPIC-05 / 003** Miss-Hint):

```csharp
internal static class SearchPatternScanner
{
    // Hauptmethode: liefert fertig trunkierten/formatieren Tool-Output.
    internal static string SearchAndFormat(
        string solutionDir,
        string pattern,
        bool isRegex,
        int maxResults);

    // API fuer EPIC-05 / 003: liefert nur die Dateipfad-Liste (kein Text),
    // damit find_symbol bei C#-Leermenge einen "es gibt aber Treffer in
    // diesen Nicht-C#-Dateien"-Hinweis bauen kann, ohne Text zu duplizieren.
    internal static IReadOnlyList<string> GetFilesWithHits(
        string solutionDir,
        string pattern,
        bool isRegex);
}
```

**Implementierungsdetails:**

- Datei-Scan sequentiell: `WebFileCatalog.GetProjectDirectories(solution)`
  → pro Projektverzeichnis `SafeEnumerateFiles` (private Kopie, 1:1 wie
  `GetIndexScopeScanner.cs:78-86`) → pro Datei `IsGeneratedPath` (1:1
  wie `GetIndexScopeScanner.cs:88-94`) rausfiltern, dann pro verbleibender
  Datei `File.ReadAllLines` + `Regex.Match` (bzw. `line.Contains`).
- **Pattern-Modus:** `isRegex == false` → `line.Contains(pattern,
  StringComparison.OrdinalIgnoreCase)` (case-insensitive Substring, der
  von LLMs intuitivste Default für "search 'foo' in code"). `isRegex ==
  true` → `Regex.Match(line, pattern, RegexOptions.IgnoreCase |
  RegexOptions.Compiled | RegexOptions.CultureInvariant)` mit
  `try/catch (ArgumentException)` für ungültige Regex → bei Fehler
  strukturierte Fehlerantwort via `McpToolResults.Error(LinterErrorCodes.InvalidArgument, ...)`
  mit Hint "Pruefe pattern auf gueltige Regex-Syntax".
- **Treffer-Format** (Plain-Text, eine Zeile pro Match):
  `{relativePath}:{lineNumber}: {lineContent}` mit `relativePath =
  Path.GetRelativePath(solutionDir, filePath).Replace('\\', '/')` und
  `lineNumber = 1-based`. Inhalt getrimmt (`TrimEnd()`), um keine
  trailing Whitespace-/Newline-Rauschen ins Agent-Output zu blasen.
- **Sortierung:** pro Datei nach Zeilennummer, Dateien untereinander
  nach `relativePath` ordinal (deterministisch, wichtig für A3 und
  E2E-Tests).
- **Trunkierung:** am Ende `McpTruncation.TruncateLines(hitLines,
  totalMatches, maxResults)` aufrufen. Vorab-Filter (Dateien alphabetisch
  durchgehen, maxResults Zeilen sammeln, Rest abschneiden **bevor** alle
  Dateien gelesen sind) wird **nicht** gemacht — Performance-Impact bei
  Last-Fixture-Größe in EPIC-08 nachmessen, nicht in 002 vermessen.
- **Verzeichnis-Sweep über `WebFileCatalog.GetProjectDirectories`:**
  `Solution` wird als Parameter erwartet, NICHT nur `solutionDir`.
  Grund: konsistent mit `GetIndexScopeScanner`/`WebFileCatalog.Collect`,
  stellt sicher dass nur Dateien in **Projektverzeichnissen der Solution**
  gescannt werden (nicht z. B. Dateien in `bin/`-Resten außerhalb).
  **Achtung — Abweichung von der oben skizzierten Signatur:** finale
  Signatur ist `SearchAndFormat(Solution solution, string pattern, bool
  isRegex, int maxResults)` (nicht `(string solutionDir, ...)`). Die
  zweite Methode analog: `GetFilesWithHits(Solution solution, string
  pattern, bool isRegex)`. Begründung: vermeidet, dass EPIC-05 die
  Solution→solutionDir-Auflösung in `find_symbol` dupliziert.
- **`Solution`-Parameter ist KEIN Grund für `McpCodeGraphServer`-Import
  im Scanner** — `Microsoft.CodeAnalysis.Solution` ist ein
  Roslyn-Typ-Import, nicht `McpCodeGraphServer`. Der Scanner bleibt
  ohne `McpCodeGraphServer`-Dependency (TD-005-Muster eingehalten).
- **Keine `CancellationToken`-Parameter** in den Scanner-Methoden.
  Begründung: `File.ReadAllLines` ist nicht cancellation-aware, und
  sequentieller Scan ist unter Last-Fixture-Größe unter einer Sekunde.
  Falls EPIC-08 lange Scan-Zeiten zeigt, kann `CancellationToken` in
  einer späteren Einheit ergänzt werden (kein Vorausplanen).
- **Defensive Datei-Lese-Fehler:** pro Datei `try/catch (IOException,
  UnauthorizedAccessException)` → Fehler ignorieren, nächste Datei.
  Aggregierte Fehlerzählung in den Tool-Output (z. B. `[Hinweis: N
  Dateien konnten nicht gelesen werden]`) ist **nicht** in 002 — zu
  nah am "nice to have", gehört in eine Folge-Einheit wenn überhaupt.

### Schritt 3 — `SearchPatternTool.cs` anlegen

Datei `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs`. Pattern ist
**exakt** `GetViolationsTool.cs` (TD-005-Muster):

```csharp
internal static class SearchPatternTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string pattern, bool isRegex, int maxResults, CancellationToken ct)
    {
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var text = await Task.Run(
            () => SearchPatternScanner.SearchAndFormat(solution, pattern, isRegex, maxResults),
            ct);
        return McpToolResults.Text(text);
    }
}
```

**Anmerkungen:**

- `await Task.Run(...)` ist **hier** OK (CPU-bound Scan-Arbeit,
  IO-bound Datei-Lesen, beides nicht Roslyn-`async`); andere Tools
  machen es ohne (z. B. `GetViolationsTool` direkt `await
  scanner.BuildViolationsTextAsync(...)`). Begründung: `search_pattern`
  ist potenziell langsamer und soll den `McpCodeGraphServer`-Lock nicht
  unnötig halten. Wenn der Kritiker den `Task.Run` als unnötig wertet,
  ist das ein **MINOR**, kein MAJOR — `A5` ("fertig ist fertig" für
  Stilfragen, die weder Tests noch Korrektheit brechen).
- **Defensive Argument-Validierung:** `maxResults < 1` → als `1`
  normalisieren (Output wäre sonst leer ohne Trunkierungs-Meta-Zeile,
  irreführend). `string.IsNullOrEmpty(pattern)` → strukturierte
  Fehlerantwort `McpToolResults.Error(LinterErrorCodes.InvalidArgument,
  "pattern darf nicht leer sein.")`. **Coder entscheidet**, wo die
  Validierung lebt (im Tool oder im Scanner) — Empfehlung: im Tool,
  damit der Scanner reine Daten bekommt und einfacher unit-testbar
  bleibt.
- **Eingabe-`description`** (registriert in `AnalysisToolRegistrations`):
  ```
  Plain-Text- oder Regex-Suche ueber den Solution-Dateibestand (alle
  Dateitypen, nicht nur C#) — Fallback fuer Namen, die kein C#-Symbol
  sind (z. B. JS-Funktion in .js, Razor-Komponente in .razor,
  WPF-Element in .xaml). Optionaler isRegex-Flag (default false =
  case-insensitive Substring). Trunkiert standardmaessig auf 50 Treffer,
  ueberschreibbar via maxResults. Trunkierungs-Meta-Zeile informiert
  ueber Gesamt-Trefferzahl.
  ```

### Schritt 4 — `AnalysisToolRegistrations.cs` modifizieren

In `Register(...)` nach dem `get_violations`-Block (Z. 28-40) den
`search_pattern`-Block ergänzen. XMLDoc oben (Z. 9-18) von
"aktuell `get_violations`" auf "aktuell `get_violations` und
`search_pattern`" anpassen. Reihenfolge: `search_pattern` **nach**
`get_violations`, damit der E2E-Test die etablierte Reihenfolge
matchen kann (Tools-Liste-Sortierung alphabetisch nach Konvention ist
kein Muss, aber `get_violations` zuerst entspricht der ursprünglichen
Tool-Einführungs-Reihenfolge).

**Geschätzter Block-Umfang: 13-17 Z.** (siehe Vor-der-Planung-Check 4).
Footprint-Re-Run nach Schritt 4 verpflichtend (im `result.md`
dokumentieren).

### Schritt 5 — `McpServerCommandTests.cs` modifizieren

Zwei Änderungen:

1. `RunAsync_ValidFixture_ServerRespondsWithEightTools` (Z. 134)
   umbenennen zu `RunAsync_ValidFixture_ServerRespondsWithNineTools`,
   `Assert.Equal(8, tools.Count)` → `Assert.Equal(9, tools.Count)`,
   `Assert.Contains(tools, t => t.Name == "search_pattern")` ergänzen.
2. Neuer E2E-Test nach `RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation`
   (Z. 217): `RunAsync_ValidFixture_SearchPatternReturnsExpectedHit`
   analog — Subprozess öffnen, `client.CallToolAsync("search_pattern",
   new Dictionary<string, object?> { ["pattern"] = "Greeter" })` (Pattern
   `Greeter` matcht `Greeter.cs` und auch in `GreeterPath` in der
   Fixture), assertieren: kein `IsError`, Text enthält
   `"Greeter.cs"` im relativen Pfad.

### Schritt 6 — `SearchPatternToolTests.cs` anlegen

Unit-Tests analog `GetViolationsToolTests.cs:1-83` (Pattern-Vergleich).
Siehe "Erwartete Tests" weiter unten für die exakte Liste inkl.
A3-Fehlschlag-Nachweis-Plan pro Test.

### Schritt 7 — Build + Test + Dogfooding

1. `dotnet build AiNetLinter.slnx` — muss grün sein, 0 Warnungen.
2. Footprint-Re-Run der 4 kritischen Klassen
   (`SearchPatternTool`/`SearchPatternScanner`/`AnalysisToolRegistrations`/
   `McpToolResults`/`McpTruncation`) — alle ≤ 2500, sonst
   `PathOverrides`-Eintrag in `rules.json` ergänzen (gezielt, mit
   Precedent `FindSymbolTool`/`FindReferencesTool`).
3. `dotnet test AiNetLinter.slnx --no-build` — muss grün sein, alle
   neuen Tests + E2E-Test inklusive, Gesamtzahl **+5-6 Unit + 1
   E2E + 0/1-GetIndexScope-Anpassung** (Erwartungs-Count der
   betroffenen Tests ändert sich nicht, weil keine neue .cs-Datei in
   der Fixture).
4. **Dogfooding (Konzept Z. 193-204, Pflicht):** Ad-hoc-Aufruf
   `ainetlinter --mcp-server --path .` (gegen reale `AiNetLinter.slnx`),
   `tools/list` muss `search_pattern` enthalten. Dann ein
   `search_pattern`-Call mit z. B. `pattern="CodeGraph"` (case-
   insensitive Substring matcht in `tasks/codegraph-mcp-server/konzept.md`,
   `tech-debt.md`, mehreren `.cs`-Dateien) → Output zeigt eine
   Trunkierungs-Meta-Zeile mit hoher Gesamtzahl, demonstriert dass
   die Trunkierung gegen reale Bestandsgröße sinnvoll greift. Pattern
   `"McpCodeGraphServer"` (case-sensitive ist hier egal wegen
   IgnoreCase) als `isRegex=false` → muss konkrete .cs-Datei-Treffer
   liefern. Ergebnis im `result.md` als "Dogfooding"-Abschnitt
   dokumentieren (Konzept-Pflicht).

## 4. Registrar-Klasse — Pflichtmessung, begründet

**Ergebnis: keine 4. Registrar-Klasse in 002.** Volle Begründung in
Vor-der-Planung-Check 4 oben. Kurzform: `AnalysisToolRegistrations` hat
41 Z. Puffer (2459/2500), `search_pattern`-Block kostet 13-17 Z., landet
bei 2472-2476/2500 mit 24-28 Z. Restpuffer (knapp ein weiterer Tool-Block).
Die Vorhersage in TD-004 ("für `search_pattern` voraussichtlich eine
vierte Registrar-Klasse nötig") ist mit den heute gemessenen Werten
**widerlegt**. Der nächste Planer (003 oder später) misst erneut —
voraussichtlich **dann** ist die 4. Registrar-Klasse fällig, sobald ein
drittes analyse-orientiertes Tool hinzukommt.

**Konsequenz für `AnalysisToolRegistrations`-XMLDoc (Z. 9-18):**
"Vorbereitet fuer das verbleibende EPIC-04-Tool `search_pattern`" wird
im Coder-Schritt zu "aktuell `get_violations` und `search_pattern`".
Der `tech-debt.md`-Eintrag TD-004 selbst wird **nicht** durch 002
verändert (A7).

## Erwartete Tests inkl. A3-Fehlschlag-Nachweis-Plan

**Pflicht-Konvention für alle Tests:** `[Collection("ConsoleTestCollection")]`
(vgl. `GetViolationsToolTests.cs:10`, parallele
`LinterConsole`-Verwendung).

### Unit-Tests in `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs`

| # | Test-Name | Assert(s) | **A3-Fehlschlag-Nachweis (Anweisung an Coder)** |
|---|---|---|---|
| 1 | `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode` | `Assert.True(result.IsError)` + `Assert.Contains("SOLUTION_NOT_LOADED", text)` | Vor dem Commit: `SearchPatternTool.cs:Zeile-mit-SolutionNotLoaded-Aufruf` temporär durch `return McpToolResults.Text("dummy");` ersetzen, Test ausführen → muss rot werden (Assert.True schlägt fehl). Rückgängig machen, erneut grün, dokumentieren. |
| 2 | `ExecuteAsync_PlainTextSubstring_FindsExpectedHitsInFixture` | Pattern `"Greeter"` → Output enthält `"Greeter.cs"` und `"Greeter"` im Inhalt (mind. ein Match in `Greeter.cs` selbst) | Vor dem Commit: `pattern == "Greeter"` im Test durch `"thisWillNotMatch_xyz_zzz"` ersetzen, Test ausführen → muss rot werden (Assert.Contains auf `"Greeter.cs"` schlägt fehl). Rückgängig, dokumentieren. |
| 3 | `ExecuteAsync_RegexPattern_FindsExpectedHitsInFixture` | Pattern `^public\s+(class\|interface\|record)` + `isRegex=true` → Output enthält `"public class"` (aus `Greeter.cs`/`Hierarchy.cs`/`OtherCaller.cs`/`ViolationTrigger.cs`) | Vor dem Commit: `isRegex=true` durch `isRegex=false` ersetzen, Test ausführen → muss rot werden (Regex-Sonderzeichen `^` und `\s` werden literal gesucht und matchen nicht — `Assert.Contains("public class")` schlägt fehl). Rückgängig, dokumentieren. |
| 4 | `ExecuteAsync_PlainTextTruncatesAtMaxResults_AppendsMetaLine` | Pattern `"public"` (kommt in mehreren .cs-Dateien vor), `maxResults=2` → Output enthält `"[N Treffer gesamt, 2 gezeigt — Pattern verfeinern oder maxResults erhöhen]"` mit N ≥ 3 und exakt **2** Trefferzeilen vor der Meta-Zeile | Vor dem Commit: In `McpTruncation.TruncateLines` die Meta-Zeile durch `return string.Join("\n", shown);` ersetzen (kein Meta), Test ausführen → muss rot werden (Assert.Contains auf Meta-Zeile schlägt fehl). Rückgängig, dokumentieren. |
| 5 | `ExecuteAsync_NoMatch_ReturnsZeroHitsMessage` | Pattern `"thisStringDoesNotExistAnywhere_zzz_999"` → Output enthält `"0 Treffer"` (oder eine gleichwertige explizite Leermenge-Meldung) | Vor dem Commit: Im Scanner die Leermenge-Behandlung entfernen, sodass ein leerer String zurückgegeben wird, Test ausführen → muss rot werden (Assert.Contains auf `"0 Treffer"` schlägt fehl). Rückgängig, dokumentieren. |
| 6 | `ExecuteAsync_GeneratedObjBinDirectories_ExcludedFromHits` | Analog `GetIndexScopeToolTests.cs:71-91`: vor dem Load `obj/Debug/Generated.cs` mit Inhalt `PATTERN_ANCHOR_999` anlegen, `search_pattern(pattern="PATTERN_ANCHOR_999")` → Output enthält **nicht** `"Generated.cs"` | Vor dem Commit: `IsGeneratedPath`-Filter in `SearchPatternScanner` temporär durch `return false;` ersetzen, Test ausführen → muss rot werden (Assert.DoesNotContain schlägt fehl, weil `Generated.cs` durchschlägt). Rückgängig, dokumentieren. |
| 7 | `ExecuteAsync_InvalidRegex_ReturnsInvalidArgumentError` | Pattern `"(unclosed"` + `isRegex=true` → `result.IsError == true` + `Assert.Contains("INVALID_ARGUMENT", text)` | Vor dem Commit: Den `try/catch (ArgumentException)` im Scanner temporär entfernen, Test ausführen → muss rot werden (Regex-Exception propagiert, Test crasht oder Assert.True(IsError) schlägt fehl). Rückgängig, dokumentieren. |
| 8 | `ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError` | Pattern `""` → `result.IsError == true` + `Assert.Contains("INVALID_ARGUMENT", text)` | Vor dem Commit: Die `IsNullOrEmpty`-Validierung im Tool temporär entfernen, Test ausführen → muss rot werden (Test läuft mit leerem Pattern durch, crasht oder liefert Treffer, Assert.True(IsError) schlägt fehl). Rückgängig, dokumentieren. |

**Acht Tests** — 1+2+3+4 decken die Kern-Pflichten (Trunkierung,
Plain-Text, Regex) ab, 5+6 die Edge-Cases (Leermenge, generierte
Verzeichnisse), 7+8 die defensive Fehlerbehandlung. Die ersten vier
sind Pflicht; 5+6 sind Pflicht für `konzept.md` Z. 604-606
(Leermenge-Hinweis-Verhalten in der Nähe der Miss-Hint-Semantik) und
Z. 651-652 (Trunkierung); 7+8 sind Pflicht für `AiNetLinterRichtlinien.mdc` §5
(Result-Pattern statt `throw`).

### E2E-Test in `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

| Test-Name | Assert(s) | **A3-Fehlschlag-Nachweis (Anweisung an Coder)** |
|---|---|---|
| `RunAsync_ValidFixture_SearchPatternReturnsExpectedHit` | Subprozess, `client.CallToolAsync("search_pattern", { pattern = "Greeter" })` → `Assert.NotEqual(true, result.IsError)` + `Assert.Contains("Greeter.cs", text)` | Vor dem Commit: In `McpServerOptionsFactory.cs:44` den `AnalysisToolRegistrations.Register(...)`-Aufruf **nur für `search_pattern`** deaktivieren (z. B. via `if (false)`-Wrap), Test ausführen → muss rot werden (ToolNotFoundError oder `search_pattern` fehlt in `tools/list`). Rückgängig, dokumentieren. |
| (mod) `RunAsync_ValidFixture_ServerRespondsWithNineTools` | `Assert.Equal(9, tools.Count)` + 9 Contains-Asserts inkl. `search_pattern` | A3: Erwartung auf `8` zurücksetzen, Test ausführen → rot. Rückgängig, dokumentieren. |

### Was NICHT in 002 getestet wird (bewusst)

- Performance gegen Last-Fixture — gehört in EPIC-08.
- Parallelisierungs-Korrektheit — kein `Parallel.ForEachAsync` in 002.
- Integration mit `find_symbol`-Miss-Hint — ist EPIC-05 / 003.
- Trunkierung in `find_symbol`/`find_references`/`get_impact` — ist
  003/004/005 (oder konsolidiert).

## Bezug zu Projektregeln (Kurzgrund pro Datei)

| Regel | Datei | Kurzgrund |
|:---|:---|:---|
| `AiNetLinter.mdc#EnforceNullableEnable` | `McpTruncation.cs`, `SearchPatternTool.cs`, `SearchPatternScanner.cs`, Test-Datei | `#nullable enable` am Dateianfang |
| `AiNetLinter.mdc#EnforceSealedClasses` | `SearchPatternTool.cs:internal static` (entfällt implizit) | TD-005-Muster |
| `AiNetLinter.mdc#AIContextFootprint` ≤ 2500 | `SearchPatternTool.cs`, `SearchPatternScanner.cs`, `AnalysisToolRegistrations.cs`, `McpTruncation.cs` | Re-Run im `result.md` dokumentieren; bei Überschreitung `PathOverrides` in `rules.json` |
| `AiNetLinter.mdc#MaxLineCount` ≤ 500 | alle 4 neuen Klassen | Scan-Logik passt locker unter 500 |
| `AiNetLinter.mdc#MaxMethodLineCount` ≤ 60 | Scanner-Methoden | bei Bedarf weiter aufteilen (z. B. `ScanFile` + `CollectHits` + `FormatOutput` als separate Methoden) |
| `AiNetLinter.mdc#MaxMethodParameterCount` ≤ 4 | `SearchAndFormat(Solution, string, bool, int)` = 4 ✓; `GetFilesWithHits(Solution, string, bool)` = 3 ✓; `TruncateLines(IReadOnlyList<string>, int, int)` = 3 ✓ | passt |
| `AiNetLinterRichtlinien.mdc#§1` (Einfachheit vor Abstraktion) | `SearchPatternScanner.cs` | Keine eigene Regex-Engine, `System.Text.RegularExpressions.Regex` direkt; `File.ReadAllLines` direkt |
| `AiNetLinterRichtlinien.mdc#§2` (kein DI) | `AnalysisToolRegistrations.cs` | Delegate-Closure wie bestehende 3 Registrar-Klassen |
| `AiNetLinterRichtlinien.mdc#§5` (Result-Pattern) | `SearchPatternScanner.cs` | `try/catch (ArgumentException)` für invalid Regex → `McpToolResults.Error(LinterErrorCodes.InvalidArgument, ...)`, kein rethrow |
| `AiNetLinterRichtlinien.mdc#§3` (PowerShell-konformes `dotnet build`/`dotnet test`) | Build/Test-Schritt | vorgegeben |
| `AiNetLinterRichtlinien.mdc#§4` (Doku-Update-Pflicht) | `Docs/agent-api.md` (Tool-Beschreibung), `Docs/ROADMAP.md` | **bewusst NICHT in 002** — beide Doku-Updates sind EPIC-08, Konzept-Befreiung wie bei step-010 (vgl. `units/001/plan.md` Zeile "Konzept-Befreiung explizit") |

## Annahmen und offene Fragen, die der Coder klären soll

- **Frage A — Wo lebt die Argument-Validierung?** Empfehlung: im
  `SearchPatternTool` (`maxResults < 1` → 1, leeres `pattern` → Error).
  Coder darf abweichen, wenn er die Validierung lieber im Scanner hat
  (z. B. für direktere Unit-Tests am Scanner). Begründung warum die
  Empfehlung im Tool steht: Tool bekommt `CancellationToken` mit,
  Validierung vor `Task.Run` spart Scan-Start.
- **Frage B — `WebFileCatalog.SafeEnumerateFiles`/`IsGeneratedPath`
  auf `internal` anheben?** Empfehlung: NEIN, private Kopie im
  `SearchPatternScanner` (siehe Vor-der-Planung-Check 1). Coder darf
  abweichen, wenn er die Duplikation als unangenehm empfindet —
  Risiko gering, Nutzen ebenfalls (kein TD-006-Close in 002). Falls
  abweichend: kurz in `result.md` "Abweichungen" begründen.
- **Frage C — `McpTruncation.cs` vs. Method in `McpToolResults.cs`?**
  Empfehlung: separate Datei `McpTruncation.cs` (konzept.md: "neben
  `Mcp/McpToolResults.cs`" wörtlich). Coder darf die Methode statt
  dessen in `McpToolResults.cs` einfügen, wenn `McpToolResults` nicht
  über 2500 Zeilen wächst (107 + ~30 = ~137, kein Problem). Begründung
  warum Empfehlung separate Datei: thematisch sauber, 003/004/005
  brauchen die Methode genauso und finden sie schneller in einer
  dedizierten Datei.
- **Frage D — Soll die `description` in `AnalysisToolRegistrations`
  noch konkreter werden (z. B. mit Beispiel-Pattern)?** Empfehlung:
  nein, aktuelle 6-7-Zeilen-Beschreibung reicht. LLM-Tools wie
  Claude Code lesen die `description` ohnehin im Tool-Listing.
- **Frage E — Soll `isRegex: true` mit `RegexOptions.Multiline`
  default-mäßig arbeiten?** Empfehlung: nein, nur `IgnoreCase` +
  `Compiled` + `CultureInvariant` (Standardeinstellung in der Plan-
  Skizze oben). Begründung: `^`/`$`-Anker sind auf einer einzelnen
  Zeile (weil `File.ReadAllLines` schon zeilenweise splittet) ohnehin
  nur als Zeilen-Anker sinnvoll, Multiline würde Verwirrung stiften.
  Falls Coder `Multiline` zusätzlich haben will: Begründung in
  `result.md`.
- **Frage F — `path`-Ausgabe Forward-Slashes (Unix-Style) konsistent
  mit `get_violations`?** Empfehlung: ja (`Replace('\\', '/')` in der
  Treffer-Formatierung, analog `GetViolationsScanner.cs:162`).
  Begründung: Agenten lesen Forward-Slashes zuverlässiger,
  konsistent mit den übrigen Tools.

## Schnittstellen, die 002 für Folge-Einheiten liefert (kein Vorausplanen der Inhalte)

- **`SearchPatternScanner.GetFilesWithHits(Solution, string, bool)`** →
  wird in 003 (Miss-Hint in `find_symbol`) verwendet, um bei einer
  C#-Leermenge die Datei-Pfad-Liste der Nicht-C#-Treffer für den
  Hinweis-Text zu liefern. **Wichtig für 003:** die Methode MUSS
  rein die Treffer-Dateipfade zurückgeben (ohne Zeileninfo), damit
  003 sie für "kein C#-Symbol, aber Texttreffer in `<Datei>` (nicht
  Teil des Graphs)"-Formulierung nutzen kann — `IReadOnlyList<string>`.
- **`McpTruncation.TruncateLines(IReadOnlyList<string>, int, int)`** →
  wird in 003/004/005 (Trunkierungs-Einbau in `find_symbol`/
  `find_references`/`get_impact`) direkt aufgerufen, **ohne** dass
  diese Einheit den Aufruf bereits in den bestehenden Tools verdrahtet.
  Format-Kontrakt der Meta-Zeile ist fix: `[N Treffer gesamt, M
  gezeigt — Pattern verfeinern oder maxResults erhöhen]`.
- **`McpToolResults.Error(LinterErrorCodes.InvalidArgument, ...)`** →
  wird in 003/004/005 ebenfalls für `isRegex`/Trunkierungs-Argument-
  Validierung verwendet (konsistentes Fehlerbild).

**Kein Vorausplanen** der Folge-Einheiten-Inhalte — der nächste Planer
(003) sieht den realen Code-Stand nach 002 und entscheidet selbst.

## Anhang — vollständige Footprint-Messung (Stand `e63176d`, 2026-08-01 10:55)

| Klasse | transitive Z. | Limit | Anmerkung |
|---|---:|---:|---|
| `FindReferencesTool` | 2519 | 2700 (PathOverride) | OK (PathOverride 2700) |
| `FindSymbolTool` | 2518 | 2700 (PathOverride) | OK (PathOverride 2700) |
| `GetImpactTool` | 2490 | 2500 | OK, knapp |
| `SymbolGraphToolRegistrations` | 2487 | 2500 | OK, knapp |
| `FileStructureToolRegistrations` | 2480 | 2500 | OK |
| `McpServerOptionsFactory` | 2470 | 2500 | OK |
| `GetFileSkeletonTool` | 2460 | 2500 | OK |
| `AnalysisToolRegistrations` | 2459 | 2500 | OK, +41 Z. Puffer → `search_pattern` (~15 Z.) passt |
| `GetTypeHierarchyTool` | 2455 | 2500 | OK |
| `GetViolationsTool` | 2451 | 2500 | OK |
| `GetHotspotsTool` | 2447 | 2500 | OK |
| `GetIndexScopeTool` | 2445 | 2500 | OK |
| `McpCodeGraphServer` | 2416 | 2500 | OK, `search_pattern` braucht keine neue Dependency (kein TD-009-Risiko) |
| `GetViolationsScanner` | 1834 | 2500 | OK, Vergleichswert für `SearchPatternScanner` (~120-140 erwartet) |
| `GetHotspotsScanner` | 151 | 2500 | Vergleichswert (150 Z. ähnlich) |
| `GetIndexScopeScanner` | 116 | 2500 | Vergleichswert (115 Z. ähnlich) |
| `McpToolResults` | 107 | 2500 | OK, `McpTruncation`-Helper kommt in sibling-Datei |
| `GetTypeHierarchyFormatter` | 105 | 2500 | Vergleichswert |
| `SymbolIdentifierResolver` | 58 | 2500 | Vergleichswert |

**Limiterkenntnis für den Coder:** `SymbolGraphToolRegistrations` hat
nur 13 Z. Puffer — wenn ein zukünftiges Symbolgraph-Tool dazukommt
(sehr wahrscheinlich, da 5 Symbolgraph-Tools in der Klasse sind), ist
eine 5. Registrar-Klasse nötig. **Nicht 002-Scope**, nur Notiz.
