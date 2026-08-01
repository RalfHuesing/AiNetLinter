---
unit: 005
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-01
epic: P0/P1 (Trunkierung in find_references + get_impact)
extends:
  - konzept.md Z. 215-225 (P0/P1 Trunkierung + maxResults)
  - konzept.md Z. 226-233 (Plain-Text-Format, einheitliche Meta-Zeile)
  - konzept.md Z. 631-634 (DoD: alle Listen-Tools trunkiert)
  - units/004/plan.md (Vorbild: Trunkierung in find_symbol, McpTruncation-Signatur,
    Pattern `maxResults = 50`-Default im MCP-Delegate)
  - TD-005 (McpCodeGraphServer-Pull-in-Muster, beibehalten)
  - TD-008 (PathOverride 2700 für FindReferencesTool, unverändert)
  - TD-011 (SymbolGraphToolRegistrations Footprint knapp, Pflichtmessung)
---

# Plan Einheit 005 — Trunkierung in `find_references` + `get_impact` (P0/P1)

## Ziel der Einheit

Die zwei verbleibenden Listen-Tools (`find_references`, `get_impact`)
bekommen die in `konzept.md` Z. 215-225 verbindlich geforderte
Trunkierung: `maxResults`-Parameter (Default 50) am MCP-Delegate,
Anwendung von `McpTruncation.TruncateLines` auf den jeweiligen
Treffer-Output, einheitliche Meta-Zeile. Damit ist das DoD-Kriterium
aus `konzept.md` Z. 631-634 ("jedes Listen-Tool trunkiert bei
generischer Anfrage") für drei der vier Listen-Tools erfüllt
(`search_pattern` 002, `find_symbol` 004, jetzt `find_references` +
`get_impact` 005); nur `search_pattern` selbst war bereits in 002
umgesetzt.

**Bewusst NICHT in 005:** Scanner-Splits für `find_references`/
`get_impact` (kein TD-005-Generalisierung in dieser Einheit — siehe
"Scope-Entscheidung"). EPIC-06/07/08, P0/P1-Extensions jenseits
der Trunkierung, `McpServerOptionsFactory`-Eingriff, `PathOverrides`-
Erhöhung — alles bewusst außen vor.

## Scope-Entscheidung

**Gewählt: Trunkierung in `find_references` + `get_impact` ohne
Scanner-Splits.** Begründung:

- (a) **Trunkierung ist die stärkste Bindung** aus `konzept.md`:
  die genannte DoD-Formulierung Z. 651-652 nennt alle vier
  Listen-Tools explizit; drei davon sind fertig (002/004), zwei
  offen (005). Das ist die P0/P1-Pflicht, die der Task am
  dringendsten schuldet.
- (b) **Trunkierung ist klein** — 1-2 Zeilen pro Output-Pfad.
  `FindReferencesTool` hat **einen** Output-Pfad (Z. 36-42),
  `GetImpactTool` hat **zwei** (`ExecuteSymbolBranchAsync` Z. 45-58
  + `ExecuteGitRefBranchAsync` Z. 60-72), beide enden in
  `McpToolResults.Text(string.Join("\n", callSites))`. Trunkierung
  ist ein 1-Zeilen-Eingriff pro Pfad — kein Scanner-Split
  gerechtfertigt (Vorgabe explizit: "Trunkierung klein genug, dass
  ein Scanner-Split overkill wäre").
- (c) **TD-005-Muster wird trotzdem eingehalten** — beide Tools
  bleiben "dünner Dispatch": Tool validiert / ruft auf / gibt
  Text zurück. Trunkierung lebt im Tool (1 Zeile + McpTruncation-
  Import), nicht in einem separaten Scanner. Cross-Tool-Konsument
  (z. B. `GetImpactTool` → `FindReferencesTool.ResolveSymbolAsync`
  Z. 48) bleibt unverändert.
- (d) **Footprint-Druck bei `GetImpactTool` 2490/2500** (Puffer
  10 Z., gemessen 2026-08-01 15:22) ist die einzige akute
  Sorge. Trunkierung kostet geschätzt +5-8 Z.
  (`maxResults`-Parameter, Import, ein Aufruf pro Branch). **Mit
  der Plan-Abweichungs-Option (siehe Schritt 5) bleibt das Tool
  unter 2500**; falls nicht, greift "Description-Kürzung statt
  PathOverride" analog 004-Plan-Check 3.
- (e) **`find_references`/`get_impact` ohne Scanner-Split ist
  konsistent mit `search_pattern` in 002** — das wurde ebenfalls
  ohne Split umgesetzt (Scanner kam erst in 002 selbst dazu,
  nicht nachträglich). Erst in 004 (für `find_symbol`) wurde der
  Scanner-Split **inline** mit der Trunkierung nachgeholt (TD-012),
  und zwar weil `find_symbol` schon vorher 112 Z. Logik im Tool
  hatte. `find_references` hat aktuell 102 Z. (davon ~50 Z.
  reine Logik in `ResolveSymbolAsync`/`ResolveByPositionAsync`/
  `ResolveByNameAsync`), `get_impact` hat 73 Z. — beide Tools
  sind **klein genug**, dass die Trunkierung allein ohne
  Refactor-Bonus sauber reinpasst.

**Bewusst NICHT in 005:**

- **Keine** Scanner-Splits für `find_references` oder `get_impact`
  (TD-005-Generalisierung, wäre eigenes Refactor-Thema).
- **Keine** sonstigen P0/P1-Extensions (Kaltstart, Auto-Discovery,
  Staleness-Sweep-`mtime`, `--mcp-log`, etc.) — alle in
  Folge-Einheiten.
- **Keine** Änderung an `McpServerOptionsFactory` über eine
  Pflicht-Footprint-Messung hinaus.
- **Keine** `PathOverrides`-Wert-Erhöhung in `rules.json` und
  **keine** neuen `PathOverride`-Einträge (auch nicht für
  `GetImpactTool` 2490/2500 — Vorgabe explizit).
- **Kein** Eingriff in `McpCodeGraphServer`, `LinterErrorFormatter`,
  `McpToolResults`, `FindSymbolScanner` (nicht in 005-Scope).
- **Keine** Doku (`Docs/agent-api.md`, `Docs/ROADMAP.md`) — EPIC-08.

## Vor-der-Planung-Checks (Kernel Teil B "Drift" / "Duplikate durch Blindheit")

### Check 1 — `FindReferencesTool`-Aktueller Stand (gelesen, 102 Z.)

**Befund (`src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs`):**

- **Kein** `FindReferencesScanner.cs` existiert (nicht versehentlich
  übersehen — Drift-Loop-Commit `a9e91ed` hat `find_references` als
  Tool-only umgesetzt, kein Scanner-Split in 002/003/004 nachgeholt).
- `internal static class FindReferencesTool` mit Methoden:
  - `ExecuteAsync(McpCodeGraphServer state, string symbolIdentifier,
    CancellationToken ct)` — **3 Parameter** (Limit 4, Puffer 1)
  - `ResolveSymbolAsync(Solution solution, string identifier,
    CancellationToken ct)` — `internal static`, **wiederverwendet**
    von `GetImpactTool.cs:48` (Cross-Tool-API, bleibt
    unverändert). 3 Parameter.
  - `ResolveByPositionAsync` (private, 5 Parameter: solution,
    identifier, path, line, column, ct) — bereits **am Limit 4**
    (Limit 4, **5/4 gerissen** würde ich vermuten, Achtung).
    Tatsächlich: `private` → `MaxMethodParameterCountForNonPublic: 6`
    greift (siehe `rules.json` Z. 117) → **legal**, kein Build-Risiko.
  - `ResolveByNameAsync` (private, 3 Parameter).
- **Trunkierung-Eingriffspunkt:** Z. 36-42:
  ```csharp
  var callSites = await DiffImpactAnalyzer.FindCallSitesAsync(symbol!, solution);
  if (callSites.Count == 0)
  {
      return McpToolResults.Text($"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'");
  }
  return McpToolResults.Text(string.Join("\n", callSites));
  ```
  → `string.Join("\n", callSites)` (Z. 42) wird ersetzt durch
  `McpTruncation.TruncateLines(callSites, callSites.Count, maxResults)`.
  **`callSites.Count` als `totalMatches`:** `FindCallSitesAsync` liefert
  die **vollständige** Liste (kein pre-truncation), also passt die
  Konzept-Vorgabe "Quelle der Wahrheit für die Gesamtzahl ist
  `totalMatches`" 1:1.
- **Leere-Treffer-Pfad** (Z. 37-40): unverändert, keine Trunkierung
  bei 0 Treffern (kein List-Output → keine Meta-Zeile nötig).
- **Cross-Tool-Coupling:** `FindReferencesTool.ResolveSymbolAsync`
  wird von `GetImpactTool.ExecuteSymbolBranchAsync` Z. 48 und
  `GetImpactTool.ExecuteAsync` (über `ExecuteSymbolBranchAsync`)
  aufgerufen. Diese Aufruf-Stelle bleibt **unverändert** — die
  Trunkierung in `get_impact` greift erst NACH dem
  `ResolveSymbolAsync`-Aufruf, also nach der Symbol-Auflösung, am
  Output-`string.Join`.
- **Konstruktor-/Dependency-Signatur:** Tool ist `static class`,
  keine Constructor-Deps, TD-009 (5/5 am Limit) **nicht betroffen**.

**Entscheidung im Plan:**

- **`maxResults` als Parameter in `ExecuteAsync`**: Tool-Methode
  bekommt `int maxResults` als 4. Parameter (Limit 4, **am Limit**).
  Der MCP-Delegate in `SymbolGraphToolRegistrations.cs` (Z. 39)
  setzt den Default `= 50` (analog 004-Schritt-4).
  → **Pre-Build-Check (Schritt 0):** Probe-Signatur temporär
  einsetzen, Build-Fallback dokumentieren. Erwartung: 4 Parameter
  mit 2 Defaults (maxResults, ct) — `MaxMethodParameterCount: 4`
  reißt nicht (gleiche Logik wie 004-Schritt-0).
- **Trunkierung inline** (1 Zeile): kein Scanner-Split (Scope).
- **`ResolveSymbolAsync` unverändert** (Cross-Tool-API).

### Check 2 — `GetImpactTool`-Aktueller Stand (gelesen, 73 Z.)

**Befund (`src/AiNetLinter/Mcp/Tools/GetImpactTool.cs`):**

- **Kein** `GetImpactScanner.cs` existiert.
- `internal static class GetImpactTool` mit Methoden:
  - `ExecuteAsync(McpCodeGraphServer state, string? gitRef,
    string? symbolIdentifier, CancellationToken ct)` — **4 Parameter**
    (Limit 4, **am Limit**).
  - `ExecuteSymbolBranchAsync(Solution solution, string
    symbolIdentifier, CancellationToken ct)` (private, 3 Parameter).
  - `ExecuteGitRefBranchAsync(Solution solution, string? gitRef)`
    (private, 2 Parameter — **kein** `CancellationToken`, da
    `DiffImpactAnalyzer.AnalyzeAsync` selbst keinen akzeptiert).
- **Zwei Modi — Output-Pfade:**
  1. **Symbol-Branch** (Z. 45-58): delegiert an
     `FindReferencesTool.ResolveSymbolAsync` + `DiffImpactAnalyzer
     .FindCallSitesAsync`, Output wie `find_references` —
     `McpToolResults.Text(string.Join("\n", callSites))` (Z. 57).
     Trunkierung greift 1:1 an dieser Stelle.
  2. **Git-Ref-Branch** (Z. 60-72): delegiert an
     `DiffImpactAnalyzer.AnalyzeAsync(solution, targetPath, gitRef,
     verbose: false)`, Output `IReadOnlyList<string>` Call-Sites
     (oder vergleichbar — Coder prüft exakten Typ), ebenfalls
     `McpToolResults.Text(string.Join("\n", callSites))` (Z. 71).
     Trunkierung greift 1:1 an dieser Stelle.
- **Leere-Treffer-Pfade** (Z. 52-55, Z. 65-69): unverändert, kein
  List-Output, keine Meta-Zeile.
- **`maxResults`-Propagation:** Da `ExecuteAsync` 4 Parameter hat
  (am Limit), muss `maxResults` als **5. Parameter** hinzukommen
  → **5/4 gerissen** → **das Limit ist das Problem**.

**Konflikt-Erkennung:** `ExecuteAsync` ist `internal static`, **nicht**
`private` → `MaxMethodParameterCountForNonPublic: 6` greift
möglicherweise **nicht** (siehe `rules.json` Z. 117: "non-public"
= `private`/`internal`? Roslyn-`AiNetLinter`-Regel ist
regelbasiert, muss empirisch geprüft werden). 004-Schritt-0
hat gezeigt, dass `internal static`-Methoden mit 5 Parametern +
3 Defaults **legal** sind (genaue Build-Outputs in
`units/004/result.md` Z. 27-63 dokumentiert). **Aber 004
delegiert den Default in den MCP-Delegate**, sodass die
Tool-Methode nur 5 echte Parameter hat, von denen 2 Defaults
sind — und das hat funktioniert. **Für 005 gilt dasselbe
Pattern** → 5 Parameter mit 2 Defaults sollte legal sein.

**Entscheidung im Plan:**

- **`maxResults` als 5. Parameter in `ExecuteAsync`** (analog
  `find_symbol` 004): `ExecuteAsync(McpCodeGraphServer state,
  string? gitRef, string? symbolIdentifier, int maxResults,
  CancellationToken ct)`.
- **Default im MCP-Delegate** in `SymbolGraphToolRegistrations.cs`
  (Z. 49-50): `int maxResults = 50`.
- **Trunkierung in beiden Branches inline** (1 Zeile pro Branch):
  `McpTruncation.TruncateLines(callSites, callSites.Count,
  maxResults)`.
- **Pre-Build-Check (Schritt 0):** Probe-Signatur temporär
  einsetzen, Build-Fallback dokumentieren. Erwartung: legal
  (analog 004).
- **Footprint-Wachstum `GetImpactTool`:** +5-8 Z. (1 Import
  + 1 Parameter + 2 Trunkierungs-Aufrufe + Normalisierung).
  2490 + 8 = 2498 → **knapp, möglicherweise gerissen** (Puffer
  ist 10 Z.). **Falls > 2500:** Plan-Abweichung 1 (siehe
  Schritt 5) — Symbol-Branch delegiert an
  `FindReferencesTool.ExecuteAsync`, spart 5-7 Z. in
  `GetImpactTool`. Git-Branch bleibt inline.

### Check 3 — Aktuelle Footprints (TD-011 Pflicht, gemessen 2026-08-01 15:22)

| Klasse | Z. | Limit | Puffer |
|---|---:|---:|---:|
| `FindReferencesTool` | **2519** | 2700 (PathOverride) | 181 |
| `GetImpactTool` | **2490** | 2500 | **10** ⚠ |
| `SymbolGraphToolRegistrations` | **2490** | 2500 | **10** ⚠ |
| `McpTruncation` | 70 | 2500 | — |
| `McpServerOptionsFactory` | 2484 | 2500 | 16 |
| `McpServerCommandTests.cs` (Datei) | ~499 | 500 (MaxLineCount) | **1** ⚠ |

Wortwörtliche Mess-Befehle (gerade ausgeführt, Stand `38703a9 chore(task):
unit 004 approved`):

```
$ dotnet run --project src/AiNetLinter -- --footprint FindReferencesTool --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.Tools.FindReferencesTool':
Gesamt transitive Zeilen: 2519
Top-Abhängigkeiten:
  + AiNetLinter.Configuration.MetricsConfig (396 Zeilen)
  + AiNetLinter.Configuration.GlobalConfigOverride (357 Zeilen)
  + AiNetLinter.Configuration.MetricsConfigOverride (357 Zeilen)

$ dotnet run --project src/AiNetLinter -- --footprint GetImpactTool --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.Tools.GetImpactTool':
Gesamt transitive Zeilen: 2490
Top-Abhängigkeiten:
  + AiNetLinter.Configuration.MetricsConfig (396 Zeilen)
  + AiNetLinter.Configuration.GlobalConfigOverride (357 Zeilen)
  + AiNetLinter.Configuration.MetricsConfigOverride (357 Zeilen)

$ dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.SymbolGraphToolRegistrations':
Gesamt transitive Zeilen: 2490
Top-Abhängigkeiten:
  + AiNetLinter.Configuration.MetricsConfig (396 Zeilen)
  + AiNetLinter.Configuration.GlobalConfigOverride (357 Zeilen)
  + AiNetLinter.Configuration.MetricsConfigOverride (357 Zeilen)

$ dotnet run --project src/AiNetLinter -- --footprint McpTruncation --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.McpTruncation':
Gesamt transitive Zeilen: 70
```

**`rules.json`-`PathOverrides`-Stand (gelesen, Z. 405-421):**

- `src/AiNetLinter/Commands/AuditCommand.cs` → 2700
- `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` → **2700** (TD-008-Schutz)
- `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` → 2700 (TD-008-Schutz, nach 004 noch nötig? Nein, 004 hat Tool von 2529 auf 2491 geschrumpft, PathOverride-Puffer ist mit 209 Z. sehr großzügig — könnte zurückgenommen werden, **aber nicht in 005**, A5)
- **`GetImpactTool` hat KEINEN PathOverride** → **kein TD-008-Schutz**, der Trunkierungs-Footprint-Zuwachs muss durch minimalen Code abgefangen werden.

**Entscheidung im Plan (TD-008/TD-011-Trigger-Bewertung):**

- **`FindReferencesTool` (PathOverride-Puffer 181 Z.):** Trunkierung
  kostet +5-7 Z. → 2519 + 7 = 2526 → **kein Risiko**, Puffer
  bleibt 174 Z. TD-008 unangetastet.
- **`GetImpactTool` (Puffer 10 Z., kein PathOverride):** Trunkierung
  kostet +5-8 Z. → 2490 + 8 = 2498 → **knapp, möglicherweise
  2498-2502**. **Pragmatische Maßnahme:** Schritt 0 misst nach
  Default-Variante; falls > 2500 → Plan-Abweichung 1
  (Symbol-Branch delegiert an `FindReferencesTool.ExecuteAsync`,
  spart ~5-7 Z.). **Falls immer noch > 2500:** Description im
  MCP-Delegate minimal kürzen, KEIN PathOverride.
- **`SymbolGraphToolRegistrations` (Puffer 10 Z.):** 005 erweitert
  die `find_references`- und `get_impact`-Description um 1-2
  Sätze zur Trunkierung (analog `find_symbol` 004). Geschätzter
  Zuwachs: +6-10 Z. → 2490 + 10 = 2500 (exakt am Limit).
  → **Knapp, reicht wahrscheinlich, aber riskant.** Coder
  entscheidet im Zweifel: Description prägnant halten. **Falls
  exakt 2500 erreicht/überschritten:** kosmetische Description-
  Kürzung (siehe 003-Plan Schritt 2). **TD-011 bleibt offen** für
  den nächsten Symbolgraph-Tool-Block (Puffer ~0 nach 005).
- **`McpServerOptionsFactory` (Puffer 16 Z.):** 005 ändert diese
  Klasse **nicht**. Coder misst trotzdem (TD-014-Pflicht).
- **`McpTruncation` (+0 Z.):** 005 braucht **keine** zweite
  Variante (siehe Check 4). `TruncateLines` deckt beide Tools
  1:1 ab. Datei bleibt 70 Z.
- **`McpServerCommandTests.cs` (VOLL, 499/500 Z.):** **Harte
  Einschränkung.** KEIN weiterer Test darf in diese Datei. Alle
  neuen E2E-Tests in 005 gehen in **neue** Dateien
  `McpServerCommandFindReferencesTests.cs` und
  `McpServerCommandGetImpactTests.cs` (analog
  `McpServerCommandFindSymbolTests.cs` aus 004).
- **TD-008/TD-011/TD-014** alle nach 005 zu prüfen: keine
  Schließung erwartet (alle drei bleiben offen).

### Check 4 — `McpTruncation.TruncateLines`-Anwendung

**Befund (gelesen, `src/AiNetLinter/Mcp/McpTruncation.cs`):**

```csharp
internal static string TruncateLines(
    IReadOnlyList<string> hitLines,
    int totalMatches,
    int maxResults)
```

Liefert entweder `string.Join("\n", hitLines)` oder ersten
`maxResults`-Slice + Meta-Zeile `[N Treffer gesamt, M gezeigt —
Pattern verfeinern oder maxResults erhöhen]`. **70 Z. Datei,
sehr klein.**

**Bewertung für `find_references` und `get_impact`:**

- **`find_references`:** `callSites` aus
  `DiffImpactAnalyzer.FindCallSitesAsync(symbol!, solution)` ist
  typ-`IReadOnlyList<string>` (oder konvertierbar via `.ToList()`).
  → **`TruncateLines(callSites, callSites.Count, maxResults)` passt
  1:1.** Coder prüft den exakten Rückgabe-Typ in Schritt 1 und
  passt bei Bedarf an (`.ToList()` ist explizit 1 Zeile extra,
  nicht im Scope, also direkt `TruncateLines` mit dem exakten
  Typ aufrufen, kein ToList).
- **`get_impact` Symbol-Branch:** delegiert an
  `FindCallSitesAsync` → identische Datenstruktur wie
  `find_references` → 1:1 gleiche Anwendung.
- **`get_impact` Git-Ref-Branch:** `callSites` aus
  `DiffImpactAnalyzer.AnalyzeAsync` — Achtung: möglicherweise
  anderes Datenformat. **Coder prüft:** wenn es bereits
  `IReadOnlyList<string>` ist (vermutlich ja, gleiches Format
  wie `FindCallSitesAsync`), passt `TruncateLines` 1:1. Wenn
  anderes Format (z. B. `List<CallSiteInfo>`), muss entweder
  eine Projektion (`.Select(x => x.ToString()).ToList()`) gemacht
  werden, oder die Methode bleibt **untrunkiert** (siehe
  Annahmen und offene Fragen A).

**Architektur-Entscheidung:**

- **KEINE** zweite Variante in `McpTruncation` (anders als
  `TruncateFileList` aus 004 — das war Miss-Hint-spezifisch).
  `TruncateLines` deckt beide Tools 1:1 ab, weil die Liste der
  Treffer in beiden Fällen `IReadOnlyList<string>` Call-Site-
  Strings ist. Meta-Zeile ist **identisch** (Konzept Z. 230-233,
  einheitlich für alle Listen-Tools).
- **Falls Git-Branch anderes Datenformat hat** (siehe Frage A in
  "Annahmen und offene Fragen"), wird die Liste vor dem
  `TruncateLines`-Aufruf projiziert (1 Zeile `.Select(...)`).
  Keine Generalisierung nötig.

### Check 5 — `SymbolGraphMini`-Fixture für Trunkierung in beiden Tools

**Befund (gelesen, `tests/Fixtures/SymbolGraphMini/`):**

- 5 C#-Dateien im Projekt: `Caller.cs`, `Greeter.cs`, `Hierarchy.cs`,
  `OtherCaller.cs`, `ViolationTrigger.cs`.
- Symbol-Struktur (gemäß 004-Plan-Abweichung 1 + Greeter.cs:
  Z. 5 + Caller.cs Z. 8 + Hierarchy.cs Z. 7, 12):
  - `Greet` (case-insensitive Substring) matcht 4 Symbole in
    `Hierarchy.cs` (IGreeting, BaseGreeting, SpecialGreeting,
    DisposableGreeting) + 3 Methoden `Greet` in IGreeting (Z. 7),
    BaseGreeting (Z. 12), Greeter (Z. 5) = **7 Symbole** (siehe
    `units/004/result.md` Z. 342-352, verifiziert).
  - `Greeter` matcht 1 Symbol (Klassen-Deklaration in `Greeter.cs`).
- Call-Site-Struktur für `find_references` / `get_impact`:
  - `Greeter.Greet` aufgerufen in `Caller.cs:8` (Z. 8
    `greeter.Greet("World")`) → 1 Call-Site.
  - `BaseGreeting.Greet` aufgerufen in `Hierarchy.cs:12` (Z. 12
    `Greet`-Methodendefinition selbst, keine Aufrufstelle) → 0
    Call-Sites extern; nur in `SpecialGreeting` (das aber
    leer ist, Z. 15-17).
  - `IGreeting.Greet` aufgerufen in `Caller.cs:8` (implizit über
    `Greeter`?) → nein, `Caller.cs` nutzt `Greeter`, nicht
    `IGreeting` direkt.
- **Symbol-Tests in `find_references`/`get_impact`:** aktuelle
  Tests (siehe `FindReferencesToolTests.cs` + `GetImpactToolTests.cs`)
  nutzen `"Greeter.Greet"` → 1 Call-Site, **nicht** genug für
  Trunkierung mit `maxResults: 2`.
- **Bedarf für 005-Tests:**
  1. **Trunkierung in `find_references`:** braucht ein Symbol mit
     ≥ 3 Call-Sites, damit `maxResults: 2` → Trunkierung
     ausgelöst. Aktuell hat die Fixture **kein** Symbol mit
     ≥ 3 Call-Sites.
  2. **Trunkierung in `get_impact` Symbol-Branch:** identisches
     Problem.
  3. **Trunkierung in `get_impact` Git-Branch:** braucht eine
     `GitImpactMiniFixtureWorkspace` (existiert bereits für
     `ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite`
     in `GetImpactToolTests.cs:70-80`) mit ≥ 3 Call-Sites.
     **Aktuell** 1 Call-Site (`CalculatorCaller.cs`).

**Entscheidung im Plan (Empfehlung — Coder darf abweichen, wenn
begründet):**

- **Option 1: Fixture erweitern** (analog 004, der
  `Component.razor`+`Page.xaml` um `userService`-Marker erweitert
  hat). 005 braucht zusätzliche Call-Sites in der `SymbolGraphMini`-
  Fixture. Vorschlag: **`Caller.cs` um zwei weitere `Greet`-Aufrufe
  in zwei neuen Methoden erweitern** (z. B. `RunTwice()` +
  `RunThrice()`), sodass `Greeter.Greet` insgesamt 3 Call-Sites
  in `Caller.cs` hat. **Kein** Eingriff in `Hierarchy.cs` (zu
  komplex, würde 005-Scope aufblähen).
  - **Vorteil:** Saubere Fixture-Erweiterung, im 004-Pattern
    (modifizieren, nicht neu anlegen).
  - **Nachteil:** Bricht ggf. 004-Tests, weil `Greeter.Greet`
    plötzlich mehr Call-Sites hat. **Coder prüft:** 004-Tests
    nutzen `Assert.Contains("Caller.cs", ...)` (Z. 93 in
    `FindReferencesToolTests.cs`, Z. 50 in
    `GetImpactToolTests.cs`) — solange der Test nur
    `Contains` prüft, funktioniert er mit mehr Call-Sites
    weiterhin. **Risiko niedrig.**
- **Option 2: Test-spezifische Call-Site-Fixture** (eigene
  kleine Fixture für die Trunkierungs-Tests). **Aufwändiger,
  nicht im 004-Pattern**, daher verworfen.

**Empfehlung: Option 1** (`Caller.cs` um 2 weitere
`Greet`-Methoden erweitern, exakt analog 004-Schritt-5 mit
`Component.razor`/`Page.xaml`).

- **GitImpactMiniFixture-Erweiterung für Git-Branch:** analog
  `Caller.cs`-Erweiterung. Vorschlag: `CalculatorCaller.cs` um
  2 weitere `Calculator.Add`-Aufrufe erweitern.
  - **Coder prüft:** genaue Struktur der Fixture
    (`tests/Fixtures/GitImpactMini/src/GitImpactMini/`), Aufrufe
    analog zu 004-Fixture-Erweiterung-Vorgehen.
- **Eindeutigkeits-Check vor Schritt 1** (im `result.md` wortwörtlich
  dokumentieren):
  ```powershell
  rg "Greet" tests/Fixtures/SymbolGraphMini/ --type cs
  rg "Add" tests/Fixtures/GitImpactMini/ --type cs
  ```
  Darf **nichts** liefern, was die Trunkierung brechen würde
  (z. B. dürfen in `SymbolGraphMini` keine Symbole mit
  case-insensitive "greet" UND ≥ 3 externen Call-Sites existieren,
  außer `Greeter.Greet`).

## Betroffene Dateien / Module

### Neu zu erstellen

| Datei | Zweck | Geschätzte Größe |
|---|---|---|
| `src/AiNetLinter.Tests/Commands/McpServerCommandFindReferencesTests.cs` | E2E-Test für `find_references`-Trunkierung (analog 004, weil `McpServerCommandTests.cs` voll) | ~40-50 Z. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandGetImpactTests.cs` | E2E-Tests für `get_impact` Symbol- und Git-Branch-Trunkierung | ~60-80 Z. (2 Tests, je 1 pro Branch) |

### Zu modifizieren

| Datei | Änderung |
|---|---|
| `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` | `ExecuteAsync` bekommt `int maxResults` als 4. Parameter; `string.Join` (Z. 42) durch `McpTruncation.TruncateLines(callSites, callSites.Count, maxResults)` ersetzen; `ResolveSymbolAsync` und Co. unverändert. |
| `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` | `ExecuteAsync` bekommt `int maxResults` als 5. Parameter; in `ExecuteSymbolBranchAsync` Z. 57 und `ExecuteGitRefBranchAsync` Z. 71 jeweils `string.Join` durch `McpTruncation.TruncateLines(callSites, callSites.Count, maxResults)` ersetzen. **Plan-Abweichung 1 möglich** (Symbol-Branch delegiert an `FindReferencesTool.ExecuteAsync`, siehe Schritt 5). |
| `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | Delegate-Signatur für `find_references` (Z. 39) und `get_impact` (Z. 49-50) um `int maxResults = 50` erweitern. Description für beide Tools um 1-2 Sätze zur Trunkierung erweitern (analog 004-Schritt-4). |
| `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs` | Bestehende Tests um Trunkierungs-spezifische Tests erweitern (1-2 neue Tests). |
| `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs` | Bestehende Tests um Trunkierungs-spezifische Tests erweitern (2 neue Tests, je 1 pro Branch). |
| `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Caller.cs` | +2 Methoden mit `Greet`-Aufrufstellen, sodass `Greeter.Greet` 3 Call-Sites hat. |
| `tests/Fixtures/GitImpactMini/src/GitImpactMini/CalculatorCaller.cs` (oder analog) | +2 Call-Sites, sodass Git-Branch 3 Call-Sites liefert. **Coder prüft** den exakten Datei-Namen und das genaue Methoden-Pattern in der Fixture. |

### Nicht modifiziert (bewusst, gegen Drift-Anfälligkeit)

- `src/AiNetLinter/Mcp/McpTruncation.cs` — `TruncateLines` deckt
  beide Tools 1:1 ab, **keine** zweite Variante nötig.
- `src/AiNetLinter/Mcp/Tools/FindSymbolScanner.cs`,
  `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` — kein Eingriff
  (004-Scope).
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — keine
  Erweiterung (Trunkierung gehört nicht in `ServerInstructions`).
  Pflicht-Re-Messung im `result.md` (TD-014, Coder).
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — keine neue
  Dependency, TD-009-Risiko (5/5) bleibt stabil.
- `src/AiNetLinter/Mcp/McpToolResults.cs` — keine Änderung.
- `src/AiNetLinter/Mcp/Output/LinterErrorFormatter.cs` — keine
  Änderung.
- `src/AiNetLinter/Mcp/SymbolIdentifierResolver.cs` — keine
  Änderung.
- `src/AiNetLinter/Mcp/Tools/FindReferencesScanner.cs` — **nicht
  angelegt** (Scope-Grenze, keine TD-005-Generalisierung in 005).
- `src/AiNetLinter/Mcp/Tools/GetImpactScanner.cs` — **nicht
  angelegt** (Scope-Grenze).
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` —
  **VOLL** (499/500 Z., siehe 003-Review MINOR 1 + 004-Review
  Beobachtung 4). Kein weiterer Test in dieser Datei. Bestehende
  Tests `RunAsync_ValidFixture_FindSymbolReturnsMatch` (Z. 273)
  und alle anderen bleiben unverändert.
- `rules.json` — **kein** neuer `PathOverride` und **keine**
  Erhöhung. Falls `GetImpactTool` oder
  `SymbolGraphToolRegistrations` reißt: Description-Kürzung
  statt PathOverride-Erhöhung (TD-008-Präzedenz bewusst vermeiden,
  A5).
- `konzept.md`, `tech-debt.md`, `state.md`, Projektregeln,
  `Docs/**` — A7 (nur lesen).
- **`#nullable enable` an `FindReferencesToolTests.cs`** und
  **`GetImpactToolTests.cs`:** beide Dateien haben **kein**
  `#nullable enable` am Dateianfang (Stand: `38703a9`,
  gelesen). 003-Review MINOR 2 hat das in `FindSymbolToolTests.cs`
  festgestellt. **Bewusst NICHT in 005-Scope** (A5, kein Eingriff
  in Dateien, die nicht sowieso berührt werden — Tests werden
  modifiziert, aber das ist keine Anlass, einen Datei-weiten
  Nullable-Header nachzuziehen).

## Konkretes Vorgehen (Schritt-für-Schritt für den Coder)

### Schritt 0 — Pre-Build-Check: `maxResults`-Parameter-Anzahl

**Bevor** der Coder Code schreibt:

```powershell
cd C:/Daten/Entwicklung/Ralf/AiNetLinter
dotnet build AiNetLinter.slnx
```

Muss grün sein (Baseline nach 004, Commit `38703a9`).

**Dann:** Test-Signatur-Probe in beiden Tools temporär:

```csharp
// In FindReferencesTool.cs:
internal static async Task<CallToolResult> ExecuteAsync(
    McpCodeGraphServer state, string symbolIdentifier, int maxResults = 50, CancellationToken ct = default)

// In GetImpactTool.cs:
internal static async Task<CallToolResult> ExecuteAsync(
    McpCodeGraphServer state, string? gitRef, string? symbolIdentifier, int maxResults = 50, CancellationToken ct = default)
```

**Falls Build grün** (Defaults zählen nicht): diese Signatur in
Schritt 1-3 übernehmen, Default im MCP-Delegate
(`SymbolGraphToolRegistrations.cs:39` und `:49-50`) belassen.

**Falls Build rot** (Regel reißt für eines der beiden Tools):
temporäre Änderung verwerfen, stattdessen **Fallback-Signatur**
verwenden (analog 004-Schritt-0):

```csharp
// FindReferencesTool.cs:
internal static async Task<CallToolResult> ExecuteAsync(
    McpCodeGraphServer state, string symbolIdentifier, int maxResults, CancellationToken ct)

// GetImpactTool.cs:
internal static async Task<CallToolResult> ExecuteAsync(
    McpCodeGraphServer state, string? gitRef, string? symbolIdentifier, int maxResults, CancellationToken ct)
```

und im `McpServerTool.Create`-Delegate den Default manuell setzen:

```csharp
// In SymbolGraphToolRegistrations.cs:
tools.Add(McpServerTool.Create(
    (string symbolIdentifier, int maxResults = 50, CancellationToken ct = default) =>
        FindReferencesTool.ExecuteAsync(mcpState, symbolIdentifier, maxResults, ct),
    ...));

tools.Add(McpServerTool.Create(
    (string? gitRef = null, string? symbolIdentifier = null, int maxResults = 50, CancellationToken ct = default) =>
        GetImpactTool.ExecuteAsync(mcpState, gitRef, symbolIdentifier, maxResults, ct),
    ...));
```

Tool-Methoden werden mit explizitem `maxResults` aufgerufen,
Normalisierung (`< 1 → 1`) passiert in den `ExecuteAsync`-Bodys.

**Doku im `result.md`:** welcher Fall eingetreten ist, mit
Build-Output (analog 004-`result.md` Z. 27-63).

### Schritt 1 — `FindReferencesTool.cs`: Trunkierung anwenden

Datei `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` öffnen.

**1a. `using AiNetLinter.Mcp;`** hinzufügen (für
`McpTruncation.TruncateLines`). Bereits importiert? Aktuell
nicht (`FindReferencesTool.cs:1-11`):
```csharp
#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Protocol;
```
→ **`using AiNetLinter.Mcp;`** nach Z. 8 (`using AiNetLinter.Core;`)
ergänzen.

**1b. `ExecuteAsync`-Signatur anpassen** (Z. 27-28):

```csharp
internal static async Task<CallToolResult> ExecuteAsync(
    McpCodeGraphServer state, string symbolIdentifier, int maxResults, CancellationToken ct)
```

(Falls Schritt 0 Fallback greift, OHNE Default; sonst optional
mit `= 50`.)

**1c. Body-Anpassung** (Z. 36-42):

```csharp
var callSites = await DiffImpactAnalyzer.FindCallSitesAsync(symbol!, solution);
if (callSites.Count == 0)
{
    return McpToolResults.Text($"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'");
}

var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
return McpToolResults.Text(McpTruncation.TruncateLines(
    callSites, callSites.Count, normalizedMaxResults));
```

**Wichtige Details:**

- **`callSites.Count` als `totalMatches`:** `FindCallSitesAsync`
  liefert die vollständige Liste (kein pre-truncation), also
  passt die Konzept-Vorgabe 1:1.
- **`callSites` Typ:** wahrscheinlich `IReadOnlyList<string>` oder
  konvertierbar. Falls `IEnumerable<string>`, muss
  `.ToList()` aufgerufen werden (Coder prüft exakten Typ in
  `DiffImpactAnalyzer.cs`).
- **Normalisierung `maxResults < 1 → 1`:** im Tool-Body, nicht
  im Scanner (gibt es nicht für `find_references`).
- **Meta-Zeile-Wortlaut:** exakt `[N Treffer gesamt, M gezeigt —
  Pattern verfeinern oder maxResults erhöhen]` (Konzept Z. 230-233).

**1d. Kein** Eingriff in `ResolveSymbolAsync` (Z. 51-60),
`ResolveByPositionAsync` (Z. 62-81), `ResolveByNameAsync` (Z. 83-101)
— Cross-Tool-Coupling zu `GetImpactTool` bleibt stabil.

### Schritt 2 — `GetImpactTool.cs`: Trunkierung in beiden Branches anwenden

Datei `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` öffnen.

**2a. `using AiNetLinter.Mcp;`** hinzufügen. Aktuell
(`GetImpactTool.cs:1-7`):
```csharp
#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
```
→ **`using AiNetLinter.Mcp;`** nach Z. 5 (`using AiNetLinter.Core;`)
ergänzen.

**2b. `ExecuteAsync`-Signatur anpassen** (Z. 21-22):

```csharp
internal static async Task<CallToolResult> ExecuteAsync(
    McpCodeGraphServer state, string? gitRef, string? symbolIdentifier, int maxResults, CancellationToken ct)
```

**2c. Body-Anpassung** (Z. 36-43): keine Änderung am Dispatch
(`hasGitRef`/`hasSymbolIdentifier`/`ExecuteSymbolBranchAsync`/
`ExecuteGitRefBranchAsync` bleiben 1:1).

**2d. `ExecuteSymbolBranchAsync` anpassen** (Z. 45-58):

```csharp
private static async Task<CallToolResult> ExecuteSymbolBranchAsync(
    Solution solution, string symbolIdentifier, int maxResults, CancellationToken ct)
{
    var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, symbolIdentifier, ct);
    if (error is not null) return error;

    var callSites = await DiffImpactAnalyzer.FindCallSitesAsync(symbol!, solution);
    if (callSites.Count == 0)
    {
        return McpToolResults.Text($"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'");
    }

    var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
    return McpToolResults.Text(McpTruncation.TruncateLines(
        callSites, callSites.Count, normalizedMaxResults));
}
```

**2e. `ExecuteGitRefBranchAsync` anpassen** (Z. 60-72):

```csharp
private static async Task<CallToolResult> ExecuteGitRefBranchAsync(
    Solution solution, string? gitRef, int maxResults)
{
    var targetPath = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
    var callSites = await DiffImpactAnalyzer.AnalyzeAsync(solution, targetPath, gitRef, verbose: false);

    if (callSites.Count == 0)
    {
        var refLabel = string.IsNullOrEmpty(gitRef) ? "uncommittete Aenderungen" : gitRef;
        return McpToolResults.Text($"Keine betroffenen Aufrufstellen gefunden fuer '{refLabel}'");
    }

    var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
    return McpToolResults.Text(McpTruncation.TruncateLines(
        callSites, callSites.Count, normalizedMaxResults));
}
```

**Wichtige Details:**

- **`callSites` Typ aus `AnalyzeAsync`:** Coder prüft exakten
  Rückgabe-Typ in `Core/DiffImpactAnalyzer.cs`. Falls
  `IReadOnlyList<string>` → 1:1 Anwendung. Falls `List<CallSiteInfo>`
  o. ä. → vorher projizieren mit `.Select(x => x.ToString()).ToList()`.
- **Aufruf von `ExecuteSymbolBranchAsync` in Z. 39:** muss
  `maxResults` weiterreichen:
  `await ExecuteSymbolBranchAsync(solution, symbolIdentifier!, maxResults, ct);`
- **Aufruf von `ExecuteGitRefBranchAsync` in Z. 42:** muss
  `maxResults` weiterreichen:
  `await ExecuteGitRefBranchAsync(solution, gitRef, maxResults);`

**2f. Plan-Abweichung 1 vorbereiten (Schritt 5):** falls
`GetImpactTool`-Footprint nach Schritten 1+2+3+4 > 2500,
**VOR** dem finalen Commit:

- **Symbol-Branch delegiert an `FindReferencesTool`:** Z. 45-58
  wird ersetzt durch:
  ```csharp
  return await FindReferencesTool.ExecuteAsync(state, symbolIdentifier, maxResults, ct);
  ```
  → spart ~10 Z. in `GetImpactTool` (Body von
  `ExecuteSymbolBranchAsync` + Dispatch-Logik).
- Git-Branch bleibt inline.
- **Voraussetzung:** `FindReferencesTool.ExecuteAsync`-Signatur
  enthält `maxResults` (Schritt 1 hat das erledigt).
- **Konsequenz für Tests:** `GetImpactToolTests.cs`-Test
  `ExecuteAsync_SymbolIdentifierGiven_DelegatesToResolveSymbolAndReturnsCallSites`
  (Z. 39-51) prüft aktuell, dass `Caller.cs` im Output
  enthalten ist — das bleibt wahr, weil `FindReferencesTool`
  dasselbe tut.

### Schritt 3 — `SymbolGraphToolRegistrations.cs`: Delegate + Description

**Drei Änderungen** an
`src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`:

**3a. `find_references`-Delegate (Z. 38-47):**

```csharp
tools.Add(McpServerTool.Create(
    (string symbolIdentifier, int maxResults = 50, CancellationToken ct = default) =>
        FindReferencesTool.ExecuteAsync(mcpState, symbolIdentifier, maxResults, ct),
    new McpServerToolCreateOptions
    {
        Name = "find_references",
        Description = "Findet alle Aufrufstellen eines C#-Symbols (Datei:Zeile:Spalte " +
            "oder qualifizierter/teil-qualifizierter Name). Deckt nur .cs-Dateien ab, " +
            "keine .js/.razor/.xaml/.html/.css-Dateien. Trunkiert standardmaessig auf 50 " +
            "Treffer, ueberschreibbar via maxResults; Trunkierungs-Meta-Zeile meldet die " +
            "Gesamt-Trefferzahl.",
    }));
```

**3b. `get_impact`-Delegate (Z. 49-59):**

```csharp
tools.Add(McpServerTool.Create(
    (string? gitRef = null, string? symbolIdentifier = null, int maxResults = 50, CancellationToken ct = default) =>
        GetImpactTool.ExecuteAsync(mcpState, gitRef, symbolIdentifier, maxResults, ct),
    new McpServerToolCreateOptions
    {
        Name = "get_impact",
        Description = "Findet Aufrufstellen geaenderter C#-Signaturen. Entweder gitRef " +
            "(Git-Commit-Ref, leer = uncommittete Aenderungen) ODER symbolIdentifier " +
            "(Datei:Zeile:Spalte oder qualifizierter Name) angeben, nie beide. Deckt nur " +
            ".cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. Trunkiert " +
            "standardmaessig auf 50 Treffer, ueberschreibbar via maxResults; " +
            "Trunkierungs-Meta-Zeile meldet die Gesamt-Trefferzahl.",
    }));
```

**3c. Footprint-Wachstum-Schätzung:**

- `find_references`-Description: +50 Zeichen (1 Satz), +2 Z.
- `get_impact`-Description: +50 Zeichen (1 Satz), +2 Z.
- `SymbolGraphToolRegistrations` aktuell 2490, **erwartet
  2494** (Puffer 6 Z. nach 005).
- **Knapp**, aber unter Limit. Coder misst nach (Schritt 8
  Pflicht-Re-Messung). **Falls > 2500:** Description-
  Kürzungs-Sätze weglassen und im `result.md` als
  Plan-Abweichung 2 dokumentieren.

### Schritt 4 — Fixture-Erweiterung `Caller.cs` + `CalculatorCaller.cs`

**4a. `Caller.cs` erweitern (SymbolGraphMini):**

Vor Schritt 1 (im `result.md` wortwörtlich dokumentieren):

```powershell
cd C:/Daten/Entwicklung/Ralf/AiNetLinter
rg "Greet" tests/Fixtures/SymbolGraphMini/ --type cs
```

Erwartetes Ergebnis: aktuell matcht `Greet` 4 Methodendefinitionen
(IGreeting.Greet, BaseGreeting.Greet, Greeter.Greet) + 1
Aufrufstelle (Caller.cs:8) = 5 Treffer. Davon sind 3
**Methodendefinitionen** (keine externen Call-Sites für
`find_references`/`get_impact`) und 1 **Aufrufstelle** in
`Caller.cs:8` (`greeter.Greet("World")`).

**Dann:** `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Caller.cs`
am Ende ergänzen um:

```csharp
public string RunTwice()
{
    var greeter = new Greeter();
    return greeter.Greet("World") + " / " + greeter.Greet("World");
}

public string RunThrice()
{
    var greeter = new Greeter();
    return greeter.Greet("World") + " / " + greeter.Greet("World") + " / " + greeter.Greet("World");
}
```

→ **Erwarteter Effekt:** `Greeter.Greet` hat jetzt 5 externe
Call-Sites (1 in `Run` + 2 in `RunTwice` + 2 in `RunThrice`? Eigentlich
3 in `RunTwice` + 3 in `RunThrice` = 7). Mit `maxResults: 2` wird
sauber trunkiert, Meta-Zeile `[7 Treffer gesamt, 2 gezeigt —
Pattern verfeinern oder maxResults erhöhen]`.

**Coder prüft die exakte Treffer-Zahl** im ersten Test-Lauf und
passt `maxResults` ggf. an (z. B. `maxResults: 3` falls
Trunkierung mit `maxResults: 2` zu schwach ist für 7 Treffer).

**4b. `CalculatorCaller.cs` erweitern (GitImpactMini):**

Vor Schritt 1 (im `result.md` wortwörtlich dokumentieren):

```powershell
cd C:/Daten/Entwicklung/Ralf/AiNetLinter
rg "Add" tests/Fixtures/GitImpactMini/ --type cs
```

Erwartetes Ergebnis: `Add` matcht 1-2 Definitionen + 1
Aufrufstelle (`CalculatorCaller.cs`). **Coder prüft exakten
Stand** und erweitert `CalculatorCaller.cs` analog um 2
weitere Methoden mit `Calculator.Add`-Aufrufstellen, sodass
Git-Branch 3-7 Call-Sites liefert.

**4c. Eindeutigkeits-Check** nach Erweiterung:

```powershell
rg "Greet" tests/Fixtures/SymbolGraphMini/ --type cs
rg "Add" tests/Fixtures/GitImpactMini/ --type cs
```

Darf **nichts** liefern, was die 004-Tests bricht
(`Assert.Contains("Caller.cs", ...)` etc. müssen weiterhin
grün sein — die Erweiterung fügt **neue** Methoden hinzu,
bestehende Methoden `Run()` bleibt unverändert).

**4d. Fixture-Änderungen sind Teil des Code-Commits** (A1: ein
Coder, ein Commit).

### Schritt 5 — Plan-Abweichungs-Trigger: Footprint-Notbremse

**Wann greift Plan-Abweichung 1:** wenn nach Schritten 1+2+3+4
**plus** Build grün die `GetImpactTool`-Footprint-Messung
> 2500 ergibt.

**Aktion:**

1. `GetImpactTool.cs` Z. 45-58 (`ExecuteSymbolBranchAsync`-Body
   + Signatur) wird ersetzt durch direkten `FindReferencesTool`-
   Aufruf:

   ```csharp
   if (hasSymbolIdentifier)
   {
       return await FindReferencesTool.ExecuteAsync(state, symbolIdentifier, maxResults, ct);
   }
   ```

2. `ExecuteSymbolBranchAsync`-Methode wird komplett entfernt
   (kein externer Konsument, Tool-intern).

3. `using` für `Microsoft.CodeAnalysis` (`Solution` war hier
   verwendet) wird ggf. überflüssig — Coder prüft.

4. **Git-Branch bleibt inline** (kein Delegations-Partner).

**Wann greift Plan-Abweichung 2** (Description-Kürzung): wenn
nach Schritten 1+2+3+4 die `SymbolGraphToolRegistrations`-
Footprint-Messung > 2500 ergibt. → Beide Trunkierungs-Sätze
werden auf 1 Satz gekürzt:

```csharp
// find_references:
Description = "Findet alle Aufrufstellen eines C#-Symbols " +
    "(Datei:Zeile:Spalte oder qualifizierter/teil-qualifizierter Name). " +
    "Deckt nur .cs-Dateien ab. Trunkiert standardmaessig auf 50 Treffer, " +
    "ueberschreibbar via maxResults.",
// get_impact:
Description = "Findet Aufrufstellen geaenderter C#-Signaturen. " +
    "Entweder gitRef (leer = uncommittete Aenderungen) ODER " +
    "symbolIdentifier angeben, nie beide. Deckt nur .cs-Dateien ab. " +
    "Trunkiert standardmaessig auf 50 Treffer, ueberschreibbar via maxResults.",
```

**Wann greift Plan-Abweichung 3** (Trunkierung Git-Branch
inline nicht möglich): wenn `DiffImpactAnalyzer.AnalyzeAsync`
ein anderes Datenformat als `IReadOnlyList<string>` liefert und
die Trunkierung nicht 1:1 passt. → Aktion: `.Select(x =>
x.ToString()).ToList()` als 1-zeilige Projektion einfügen,
**bevor** Plan-Abweichung 1 greift (sonst würde Plan-Abweichung
1 Git-Branch nicht abdecken).

**Doku im `result.md`:** welche Plan-Abweichung gegriffen hat
+ Begründung + Footprint-Diff.

### Schritt 6 — Tests: `FindReferencesToolTests.cs` erweitern

`src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs`
(modifiziert, aktuell 95 Z., 6 Tests). Hinzufügen:

**Test 7 (neu): `ExecuteAsync_ValidSymbolWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine`**

```csharp
[Fact]
public async Task ExecuteAsync_ValidSymbolWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
{
    using var fixture = new SymbolGraphMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    var state = new McpCodeGraphServer(catalog);

    // Greeter.Greet hat nach Schritt 4a mind. 5 Call-Sites in Caller.cs.
    // maxResults: 2 erzwingt Trunkierung.
    var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 2, CancellationToken.None);

    Assert.NotEqual(true, result.IsError);
    var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
    Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
    Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
    Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);
}
```

**A3-Methode (analog 004-Test 2):** temporär in
`FindReferencesTool.cs:42` den `McpTruncation.TruncateLines`-Aufruf
durch `string.Join("\n", callSites)` ersetzen, Test ausführen,
sollte fehlschlagen mit "Not found: 'Treffer gesamt'".
A3-Rückgängig: Wiederherstellen. Failure-Output wortwörtlich im
`result.md` dokumentieren.

**Test 8 (neu, optional): `ExecuteAsync_NoCallSites_ReturnsNoMatchWithoutTruncation`**

```csharp
[Fact]
public async Task ExecuteAsync_NoCallSites_ReturnsNoMatchWithoutTruncation()
{
    // Symbol mit 0 Call-Sites — kein List-Output, keine Trunkierung.
    // Wichtig: kein "Treffer gesamt" im Output.
    using var fixture = new SymbolGraphMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    var state = new McpCodeGraphServer(catalog);

    // Greeter.Dispose (existiert in DisposableGreeting) hat keine externen Call-Sites.
    // Aber besser: ein Symbol wählen, von dem wir sicher wissen, dass es keine Aufrufer hat.
    // Coder wählt nach Sichtung der Fixture.
    var result = await FindReferencesTool.ExecuteAsync(state, "Greeter", maxResults: 50, CancellationToken.None);

    Assert.NotEqual(true, result.IsError);
    var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
    // Greeter (Klassen-Deklaration) hat 0 Call-Sites, daher "Keine Aufrufstellen" — keine Meta-Zeile.
    Assert.DoesNotContain("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
}
```

**A3-Methode:** implizit (Regression). Test passt mit aktuellem
`McpTruncation`-Verhalten, kein A3-Auslöser zwingend.

### Schritt 7 — Tests: `GetImpactToolTests.cs` erweitern (2 Branches)

`src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs`
(modifiziert, aktuell 95 Z., 6 Tests). Hinzufügen:

**Test 7 (neu): Symbol-Branch-Trunkierung**

```csharp
[Fact]
public async Task ExecuteAsync_SymbolIdentifierWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
{
    using var fixture = new SymbolGraphMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    var state = new McpCodeGraphServer(catalog);

    var result = await GetImpactTool.ExecuteAsync(
        state, gitRef: null, symbolIdentifier: "Greeter.Greet", maxResults: 2, CancellationToken.None);

    Assert.NotEqual(true, result.IsError);
    var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
    Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
    Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
    Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);
}
```

**A3-Methode (analog 004-Test 2):** temporär in
`GetImpactTool.cs:57` (Symbol-Branch) den
`McpTruncation.TruncateLines`-Aufruf durch
`string.Join("\n", callSites)` ersetzen, Test ausführen,
sollte fehlschlagen. A3-Rückgängig: Wiederherstellen.
Failure-Output wortwörtlich im `result.md`.

**Test 8 (neu): Git-Branch-Trunkierung**

```csharp
[Fact]
public async Task ExecuteAsync_GitRefUncommittedWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
{
    using var fixture = new GitImpactMiniFixtureWorkspace();
    fixture.ChangeCalculatorAddBodyWithoutCommitting();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    var state = new McpCodeGraphServer(catalog);

    var result = await GetImpactTool.ExecuteAsync(
        state, gitRef: null, symbolIdentifier: null, maxResults: 2, CancellationToken.None);

    Assert.NotEqual(true, result.IsError);
    var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
    Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
    Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
    Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);
}
```

**A3-Methode:** temporär in `GetImpactTool.cs:71` (Git-Branch)
den `McpTruncation.TruncateLines`-Aufruf durch
`string.Join("\n", callSites)` ersetzen, Test ausführen, sollte
fehlschlagen. A3-Rückgängig: Wiederherstellen. Failure-Output
wortwörtlich im `result.md`.

**Bestehende Tests in `FindReferencesToolTests.cs` (6 Tests) und
`GetImpactToolTests.cs` (6 Tests):** unverändert, weil
`ResolveSymbolAsync`-Aufrufe keine `maxResults`-Parameter
erwarten. Die 4 `ResolveSymbolAsync_*`-Tests in
`FindReferencesToolTests.cs` (Z. 26-80) sind nicht betroffen.

**Test 1 in `FindReferencesToolTests.cs` (Z. 13-23):**
`ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
muss um `maxResults: 50` als Argument erweitert werden (analog
004-Test 1):

```csharp
var result = await FindReferencesTool.ExecuteAsync(state, "irrelevant", maxResults: 50, CancellationToken.None);
```

**Test 1 in `GetImpactToolTests.cs` (Z. 13-23):**
`ExecuteAsync_NoSolutionLoaded_…` muss um `maxResults: 50` als
Argument erweitert werden.

**Test 2 in `GetImpactToolTests.cs` (Z. 25-37, "Both Git-Ref
and Symbol"):** muss um `maxResults: 50` erweitert werden.

**Test 3 in `GetImpactToolTests.cs` (Z. 39-51, "Symbol
Identifier Delegates to ResolveSymbol"):** muss um
`maxResults: 50` erweitert werden.

**Test 4 in `GetImpactToolTests.cs` (Z. 53-65, "Unknown Symbol
Identifier"):** muss um `maxResults: 50` erweitert werden.

**Test 5 in `GetImpactToolTests.cs` (Z. 67-80, "No Git-Ref
Uncommitted Change"):** muss um `maxResults: 50` erweitert
werden.

**Test 6 in `GetImpactToolTests.cs` (Z. 82-94, "No Git
Repository"):** muss um `maxResults: 50` erweitert werden.

**Test 6 in `FindReferencesToolTests.cs` (Z. 82-94, "Valid
Qualified Name Returns Call-Site"):** muss um `maxResults: 50`
erweitert werden.

### Schritt 8 — E2E-Tests in neuen Dateien

**8a. `McpServerCommandFindReferencesTests.cs` (neu):**

Analog `McpServerCommandFindSymbolTests.cs` aus 004
(Commit `c6261ea`):

```csharp
#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Commands;

[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandFindReferencesTests
{
    [Fact]
    public async Task RunAsync_ValidFixture_FindReferencesWithMaxResultsTruncates()
    {
        // Subprozess gegen die echte SymbolGraphMini-Fixture.
        // find_references(Greeter.Greet, maxResults: 2) muss trunkieren
        // (Greeter.Greet hat 5+ Call-Sites nach Schritt 4a).
        //
        // Implementation analog McpServerCommandFindSymbolTests.cs:23-48
        // aus 004: subprocess starten, initialize-Handshake, tools/call mit
        // maxResults: 2, Output-Text enthält "Treffer gesamt" + "2 gezeigt".
    }
}
```

**8b. `McpServerCommandGetImpactTests.cs` (neu):**

Analog, 2 E2E-Tests (Symbol-Branch + Git-Branch), je ~25 Z. pro
Test.

**A3-Methode (E2E):** implizit (E2E-Regression, analog
`McpServerCommandFindSymbolTests.cs` aus 004). E2E-Auslöser ist
aufwändig (Subprozess-Neustart ~10 s pro Lauf), per 002-Plan-
Methode nicht zwingend.

### Schritt 9 — Build/Tests/Footprint-Messung

**Pflicht-Reihenfolge:**

1. `dotnet build AiNetLinter.slnx` → 0/0.
2. Targeted Re-Run:
   ```powershell
   dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj --no-build `
     --filter "FullyQualifiedName~FindReferencesTool|FullyQualifiedName~GetImpactTool|FullyQualifiedName~McpServerCommandFindReferences|FullyQualifiedName~McpServerCommandGetImpact"
   ```
3. Footprint-Messung:
   ```powershell
   dotnet run --project src/AiNetLinter -- --footprint FindReferencesTool --path .
   dotnet run --project src/AiNetLinter -- --footprint GetImpactTool --path .
   dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
   dotnet run --project src/AiNetLinter -- --footprint McpTruncation --path .
   dotnet run --project src/AiNetLinter -- --footprint McpServerOptionsFactory --path .
   ```
4. Volllauf: `dotnet test AiNetLinter.slnx --no-build` → erwartet
   1108+4 = **1112** (4 neue Tests: 2 in
   `FindReferencesToolTests.cs` + 2 in `GetImpactToolTests.cs`,
   plus 2 E2E-Tests = **1114**). **Genau zählen** im `result.md`.
5. Self-Lint: `dotnet run --project src/AiNetLinter -- --path . --config rules.json` → 0 Violations.
6. Falls Schritt 5 eine Plan-Abweichung ausgelöst hat: Re-Messung
   + Re-Volllauf.

### Schritt 10 — Dogfooding gegen `AiNetLinter.slnx`

**`initialize` + `tools/list`:** Standard-Handshake, prüfen
dass `find_references`-Description jetzt Trunkierungs-Hinweis
enthält (analog 004 `result.md` Z. 461-505).

**`find_references`-Dogfooding:** z. B.
`find_references("DiffImpactAnalyzer.AnalyzeAsync", maxResults: 5)`.
Erwartung: ≥ 5 Call-Sites (verwendet in `MapCommand`, `ImpactCommand`,
`GetImpactTool`, etc.), 5 gezeigt + Meta-Zeile.

**`get_impact` Symbol-Branch-Dogfooding:**
`get_impact(symbolIdentifier="DiffImpactAnalyzer.AnalyzeAsync",
maxResults=3)`. Erwartung: 3 gezeigt + Meta-Zeile.

**`get_impact` Git-Branch-Dogfooding:**
`get_impact(gitRef="HEAD~0", maxResults=10)` (= aktuelle
uncommitete Änderungen, falls welche existieren — Coder prüft
vorher `git status`).

**Hinweis:** Die AiNetLinter.slnx hat **keine** Web-Dateien
(004-Plan-Check 3 + `result.md` Z. 354-368), Miss-Hint-Pfad
ist nicht relevant für `find_references`/`get_impact` (nur
`find_symbol` hat Miss-Hint).

### Schritt 11 — Commit

```bash
git add src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs
git add src/AiNetLinter/Mcp/Tools/GetImpactTool.cs
git add src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs
git add src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs
git add src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs
git add src/AiNetLinter.Tests/Commands/McpServerCommandFindReferencesTests.cs
git add src/AiNetLinter.Tests/Commands/McpServerCommandGetImpactTests.cs
git add tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Caller.cs
git add tests/Fixtures/GitImpactMini/src/GitImpactMini/CalculatorCaller.cs
git commit -m "feat(mcp): find_references + get_impact trunkierung [codegraph-mcp-server]"
```

**Gezielter `git add`, kein `-A`/`.`, kein Push, kein
History-Rewrite** (A4). Branch: `main` (per aktuellem Stand,
laut `state.md` Z. 28-29 — Working-Tree clean, Branch `main`
21 Commits ahead of `origin/main`).

## Erwartete Tests

**Unit-Tests (Pflicht-A3 für 4 Trunkierungs-Tests):**

1. **`FindReferencesToolTests.cs` Test 7: Trunkierung Haupt-Output**
   - **A3-Methode:** `McpTruncation.TruncateLines`-Aufruf
     temporär durch `string.Join("\n", callSites)` ersetzen,
     Build + Test, Failure-Output dokumentieren.
   - **Beleg:** "Not found: 'Treffer gesamt'" auf untrunkiertem
     Output (analog 004-`result.md` Z. 184-193).
2. **`GetImpactToolTests.cs` Test 7: Symbol-Branch-Trunkierung**
   - **A3-Methode:** Symbol-Branch `string.Join` durch
     `McpTruncation.TruncateLines` ersetzen ist umgekehrt —
     **A3-Auslöser ist, den `TruncateLines`-Aufruf durch
     `string.Join` zu ersetzen**, Test sollte fehlschlagen
     mit "Not found: 'Treffer gesamt'".
3. **`GetImpactToolTests.cs` Test 8: Git-Branch-Trunkierung**
   - **A3-Methode:** Git-Branch `string.Join` durch
     `McpTruncation.TruncateLines` ersetzen — Test sollte
     fehlschlagen.
4. **2 E2E-Tests (Symbol- und Git-Branch in
   `McpServerCommandGetImpactTests.cs`):** A3 implizit
   (Subprozess-E2E, A3 nicht zwingend analog 004-Methode).
5. **1 E2E-Test in `McpServerCommandFindReferencesTests.cs`:**
   A3 implizit (analog 4).

**Modifizierte Tests (Signatur-Anpassung, A3 implizit):**

- 1 Test in `FindReferencesToolTests.cs` (Test 1, `maxResults: 50`-
  Argument).
- 6 Tests in `GetImpactToolTests.cs` (Tests 1-6, `maxResults: 50`-
  Argument).

**Bestehende Tests (unverändert, Regression-Schutz):**

- 4 `ResolveSymbolAsync_*`-Tests in `FindReferencesToolTests.cs`
  (Z. 26-80).
- Bestehende `find_references`-Tests in
  `McpServerCommandTests.cs` (Z. 273) — E2E-Regression, kein
  `maxResults`-Argument im Aufruf, Default greift.

## Footprint-Messung TD-011 (Pflicht, vor und nach)

**Vor 005 (gemessen 2026-08-01 15:22, Stand `38703a9`):**

| Klasse | Z. | Limit | Puffer |
|---|---:|---:|---:|
| `FindReferencesTool` | 2519 | 2700 (PathOverride) | 181 |
| `GetImpactTool` | 2490 | 2500 | **10** ⚠ |
| `SymbolGraphToolRegistrations` | 2490 | 2500 | **10** ⚠ |
| `McpServerOptionsFactory` | 2484 | 2500 | 16 |
| `McpTruncation` | 70 | 2500 | — |

**Nach 005 (erwartet):**

| Klasse | Δ erwartet | Z. erwartet | Limit | Puffer erwartet |
|---|---:|---:|---:|---:|
| `FindReferencesTool` | +6-8 (1 Import + 1 P + 1 Trunkierung) | 2525-2527 | 2700 (PathOverride) | 173-175 |
| `GetImpactTool` ohne Plan-Abweichung 1 | +10-12 (1 Import + 1 P + 2 Trunkierungen + 2 Normalisierungen) | 2500-2502 | 2500 | **0 (-2)** ⚠ |
| `GetImpactTool` MIT Plan-Abweichung 1 | -3 (Symbol-Branch delegiert → -8 Z., Git-Branch inline +7 Z.) | 2487 | 2500 | 13 |
| `SymbolGraphToolRegistrations` | +4-6 (2 × +2 Z. Description) | 2494-2496 | 2500 | 4-6 |
| `McpServerOptionsFactory` | 0 | 2484 | 2500 | 16 |

**Wortwörtliche Mess-Befehle** (im `result.md` Schritt 9
dokumentiert):

```
$ dotnet run --project src/AiNetLinter -- --footprint FindReferencesTool --path .
$ dotnet run --project src/AiNetLinter -- --footprint GetImpactTool --path .
$ dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
$ dotnet run --project src/AiNetLinter -- --footprint McpTruncation --path .
$ dotnet run --project src/AiNetLinter -- --footprint McpServerOptionsFactory --path .
```

**Trigger-Bewertung:**

- **`FindReferencesTool`:** sicher unter Limit (Puffer > 170 Z.).
- **`GetImpactTool` ohne Plan-Abweichung 1:** **knapp am Limit
  oder knapp drüber**. Falls > 2500 → Plan-Abweichung 1
  zwingend (Symbol-Branch delegiert). Coder misst nach
  Schritten 1+2+3+4, **vor** dem finalen Commit.
- **`SymbolGraphToolRegistrations`:** knapp am Limit (Puffer
  4-6 Z.). Falls > 2500 → Plan-Abweichung 2
  (Description-Kürzung).
- **`McpServerOptionsFactory`:** unverändert, Pflicht-Re-Messung
  zur TD-014-Doku.

**TD-008/TD-011/TD-014** alle nach 005 zu prüfen:
- TD-008: `FindReferencesTool` PathOverride 2700 bleibt
  bestehen (Puffer 173 Z. ist großzügig). **Kein**
  PathOverride für `GetImpactTool` hinzugefügt — Vorgabe
  "kein `PathOverrides`-Wert erhöhen" + "kein neuer
  PathOverride" (A5).
- TD-011: `SymbolGraphToolRegistrations` Puffer ~4-6 Z. nach
  005 → **5. Registrar-Klasse beim nächsten Symbolgraph-Tool
  zwingend nötig**.
- TD-014: `McpServerOptionsFactory` bleibt unverändert, Puffer
  16 Z.

## Bezug zu Projektregeln

- `AiNetLinter.mdc` Z. 27 (`MaxConstructorDependencies: 5`,
  `MaxMethodParameterCount: 4`, `MaxAIContextFootprint: 2500`):
  - `FindReferencesTool.ExecuteAsync` neu 4 P. (Limit 4, am
    Limit) — Schritt 0 prüft Default-Parameter-Verhalten.
  - `GetImpactTool.ExecuteAsync` neu 5 P. (Limit 4, **5/4
    gerissen**) — Schritt 0 mit Fallback wie 004.
  - `GetImpactTool.ExecuteSymbolBranchAsync` neu 4 P. (Limit 4,
    am Limit) — gleiche Logik.
  - `GetImpactTool.ExecuteGitRefBranchAsync` neu 3 P. (Limit 4,
    OK).
- `AiNetLinter.mdc` Z. 27 (`MaxAIContextFootprint: 2500`):
  - `GetImpactTool` aktuell 2490, +10-12 erwartet → **knapp**.
  - `SymbolGraphToolRegistrations` aktuell 2490, +4-6 erwartet
    → knapp.
- `AiNetLinter.mdc` Z. 27 (`MaxLineCount: 500`):
  - `McpServerCommandTests.cs` **voll** (499/500) — keine
    neuen Tests in dieser Datei.
  - Neue E2E-Dateien (`McpServerCommandFindReferencesTests.cs`,
    `McpServerCommandGetImpactTests.cs`) bleiben < 100 Z.
- `AiNetLinter.mdc` Z. 27 (`MaxMethodLineCount: 60`):
  - `ExecuteSymbolBranchAsync` + `ExecuteGitRefBranchAsync`
    werden minimal länger (1-2 Zeilen Trunkierung), bleiben
    unter 20 Z.
- `AiNetLinter.mdc` Z. 27 (`EnforceNullableEnable`):
  - Neue E2E-Dateien mit `#nullable enable` (analog 004).
  - `FindReferencesTool.cs` hat bereits `#nullable enable` (Z. 1).
  - `GetImpactTool.cs` hat bereits `#nullable enable` (Z. 1).
- `AiNetLinterRichtlinien.mdc` §1 (Monolithisch & schlank
  bleiben): Trunkierung in 005 ist ein additive Feature, keine
  neue Dependency, kein Refactor.
- `AiNetLinterRichtlinien.mdc` §2 (Kein DI-Container): Trunkierung
  nutzt bestehende `McpTruncation`-Helper, kein neues Subsystem.
- `AiNetLinterRichtlinien.mdc` §4 (Doku-Update-Pflicht): **bewusst
  NICHT in 005** — Doku (`Docs/agent-api.md`) ist EPIC-08.

## Annahmen und offene Fragen, die der Coder klären soll

- **Frage A — Rückgabe-Typ `DiffImpactAnalyzer.AnalyzeAsync`:** ist
  es `IReadOnlyList<string>` (1:1 für `TruncateLines` passt) oder
  `List<CallSiteInfo>` o. ä. (Projektion nötig)? Coder liest
  `Core/DiffImpactAnalyzer.cs` Schritt 1, dokumentiert Befund im
  `result.md`. Falls Projektion nötig → 1 zusätzliche Zeile
  `.Select(x => x.ToString()).ToList()` in
  `ExecuteGitRefBranchAsync`.

- **Frage B — Default-Parameter-Verhalten in 5-Parameter-Signaturen:**
  hat 004 empirisch gezeigt, dass `MaxMethodParameterCount: 4`
  bei 5 P. mit 2-3 Defaults **nicht** reißt. Coder verifiziert
  das in Schritt 0 für BEIDE Tools (`find_references` und
  `get_impact`) — Build-Output wortwörtlich im `result.md`.

- **Frage C — `McpServerCommandTests.cs` ist seit 003 voll —
  ist der `find_references`-E2E-Test in
  `McpServerCommandTests.cs` (falls vorhanden) zu modifizieren?**
  Coder prüft: gibt es bereits einen
  `RunAsync_ValidFixture_FindReferencesReturnsCallSite`-Test
  in `McpServerCommandTests.cs`? Falls ja, **unverändert** lassen
  (kein `maxResults`-Argument, Default greift) — analog
  004-Behandlung von `RunAsync_ValidFixture_FindSymbolReturnsMatch`.

- **Frage D — Sollte Plan-Abweichung 1 (Symbol-Branch
  delegiert) **vorsorglich** immer umgesetzt werden, statt
  erst nach Footprint-Reißen?** **Empfehlung Planer: NEIN** —
  der Plan-Fallback erst nach Messung ist sauberer (KISS, und
  falls Schritt 0 zeigt, dass Trunkierung +5-7 statt +10-12 Z.
  kostet, wäre die Delegation unnötig). **Coder entscheidet
  nach Schritt 9.1 (Post-Build-Footprint-Messung).** Falls
  knapp → Delegation umsetzen, neu messen, neu Volllauf.

- **Frage E — `get_impact`-Symbol-Branch-Verhalten bei
  `find_references`-Delegation:** wenn Plan-Abweichung 1 greift,
  geht der Output-Pfad des Symbol-Branch durch
  `FindReferencesTool.ExecuteAsync` → Cross-Tool-Coupling wird
  **enger** (vorher: nur `ResolveSymbolAsync` geteilt; nachher:
  auch `ExecuteAsync` geteilt). **Coder bewertet, ob das
  semantisch sauber ist oder ob ein separater Helper in
  `FindReferencesTool` (z. B. `ResolveAndTruncateCallSitesAsync`)
  besser wäre.** Empfehlung Planer: direkte Delegation an
  `ExecuteAsync` ist OK, weil beide Tools semantisch
  identische Operationen ausführen (Symbol-Auflösung +
  Call-Site-Sammlung + Trunkierung). Helper-Einführung wäre
  Scope-Creep (eigener Refactor).

- **Frage F — Reihenfolge der Test-Argument-Updates:** Schritt
  7 modifiziert 7 bestehende Tests (`FindReferencesToolTests.cs`
  Test 1, `GetImpactToolTests.cs` Tests 1-6) um
  `maxResults: 50`. **Coder prüft, ob `maxResults: 50` als
  Default-Wert im Tool diese Modifikationen überflüssig
  machen würde** (siehe Schritt 0-Fallback vs. Default).
  **Empfehlung Planer: BEI FALLBACK SIND MODIFIKATIONEN
  PFLICHT**, weil das Tool `maxResults` als required Parameter
  hat. **BEI DEFAULT-ERLAUBNIS** (Schritt 0 grün für beide
  Tools) **sind Modifikationen OPTIONAL** — die
  `maxResults: 50`-Argumente in den Tests sind dann redundant.
  Coder entscheidet nach Schritt 0.

## Harte Scope-Grenze (wiederholt)

- **KEINE** EPIC-06-Robustheit, EPIC-07-Tests (außer den hier
  geplanten), EPIC-08-Doku.
- **KEINE** P0/P1-Extensions jenseits der Trunkierung in
  `find_references`/`get_impact`.
- **KEINE** Änderung an `McpServerOptionsFactory` über
  Pflichtmessung hinaus.
- **KEIN** `PathOverrides`-Wert erhöhen oder neuen `PathOverride`
  hinzufügen.
- **KEIN** Scanner-Split für `find_references`/`get_impact`
  (TD-005-Generalisierung wäre eigenes Refactor-Thema, nicht
  in 005).
- **KEIN** Eingriff in `McpCodeGraphServer`, `McpToolResults`,
  `LinterErrorFormatter`, `SymbolIdentifierResolver`,
  `McpTruncation` (außer keine Änderung dort).
- **KEIN** Eingriff in `FindSymbolScanner`/`FindSymbolTool`/
  `SearchPatternTool`/`SearchPatternScanner` (004/002-Scope).
- **KEINE** Doku (`Docs/agent-api.md`, `Docs/ROADMAP.md`,
  `README.md`).
- **KEINE** Folge-Einheit planen.
- **NIEMALS** in ein anderes Projektverzeichnis wechseln.
