---
unit: 005
task: codegraph-mcp-server
workflow: dynamic-loop
type: result
created_by: coder
created_at: 2026-08-01
code_commit_hash: 3eb13bfce5562fb7cf6e559b98566f06d5736ee9
status: done
---

# Result Einheit 005 — Trunkierung in `find_references` + `get_impact` (P0/P1)

## Zusammenfassung

P0/P1-Trunkierung in den zwei verbleibenden Listen-Tools
`find_references` und `get_impact` umgesetzt. `maxResults`-Parameter
(Default 50, Override via MCP-Delegate) und `McpTruncation.TruncateLines`
analog `find_symbol` (004) und `search_pattern` (002). DoD-Kriterium
aus `konzept.md` Z. 631-634 ("jedes Listen-Tool trunkiert bei
generischer Anfrage") damit für 3 von 4 Listen-Tools erfüllt
(`search_pattern` 002, `find_symbol` 004, `find_references` +
`get_impact` Symbol- und Git-Branch 005). `get_type_hierarchy` ist
kein Listen-Tool (liefert Vererbungs-Hierarchie, max. ~10 Zeilen
typischerweise) — bewusst nicht in 005-Scope.

**Bewusst NICHT in 005 (per Plan):** Scanner-Splits für
`find_references`/`get_impact` (TD-005-Generalisierung wäre eigenes
Refactor-Thema), Doku-Update, `PathOverrides`-Erhöhungen, weitere
P0/P1-Extensions. `McpServerCommandTests.cs` (E2E-Regression-Schutz
für `find_references` und `get_impact`) bleibt unverändert, weil
bereits `maxResults`-Default im Delegate greift.

## Schritt-0-Ergebnis: `maxResults`-Parameter-Anzahl

**Probe:** temporär `ExecuteAsync(McpCodeGraphServer state, string
symbolIdentifier, int maxResults = 50, CancellationToken ct = default)`
in `FindReferencesTool.cs` + analog in `GetImpactTool.cs` (5 Parameter,
2 Defaults), `dotnet build AiNetLinter.slnx`.

**Befund:** `MaxMethodParameterCount: 4`-Regel reißt NICHT
(5 Parameter mit 2 Defaults) — der Roslyn-`MaxMethodParameterCount`-
Analyzer zählt Default-Parameter offenbar nicht, oder die Regel
reserviert für `internal static`-Methoden eine größere Reserve
(gleicher Mechanismus wie 004-Schritt-0). **ABER** Build **rot**
wegen CS1503 an `SymbolGraphToolRegistrations.cs:40` und `:51` —
der Delegate ruft `FindReferencesTool.ExecuteAsync(mcpState,
symbolIdentifier, ct)` auf, und das `ct`-Argument landet auf
`maxResults: int` statt auf `ct: CancellationToken` (Caller-Konflikt,
nicht Analyzer-Konflikt). **Gleiche Situation wie 004.**

**Entscheidung (Plan-Fallback):** `ExecuteAsync` **ohne** Default
(`int maxResults, CancellationToken ct`), **Default im MCP-Delegate**
in `SymbolGraphToolRegistrations.cs`:
`(string symbolIdentifier, int maxResults = 50, CancellationToken ct
= default)`. Tool wird mit explizitem `maxResults` aufgerufen,
`maxResults < 1 → 1` im Tool-Body normalisiert. Build grün.

**Wortwörtlicher Build-Output mit Default-Variante (vor Fallback):**
```
C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\SymbolGraphToolRegistrations.cs(40,77): error CS1503:
  Argument "3": Konvertierung von "System.Threading.CancellationToken" in "int" nicht möglich.
C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\SymbolGraphToolRegistrations.cs(51,80): error CS1503:
  Argument "4": Konvertierung von "System.Threading.CancellationToken" in "int" nicht möglich.
  0 Warnung(en)
  2 Fehler
```

**Wortwörtlicher Build-Output nach Fallback (Tool ohne Default, Default
im Delegate):**
```
AiNetLinter -> C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\bin\Debug\net10.0\AiNetLinter.dll
Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler
```

## Geänderte Dateien

Commit `3eb13bfce5562fb7cf6e559b98566f06d5736ee9` (Branch `main`,
**nicht gepusht**):

| Datei | Status | +/− | Zweck |
|---|---|---|---|
| `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` | modified | +5/−2 | `using AiNetLinter.Mcp;` + `ExecuteAsync(state, identifier, maxResults, ct)`-Signatur + `McpTruncation.TruncateLines(...)` statt `string.Join` (Schritt 1) |
| `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` | modified | +15/−4 | `using AiNetLinter.Mcp;` + `ExecuteAsync(state, gitRef, symbolIdentifier, maxResults, ct)`-Signatur + Trunkierung in `ExecuteSymbolBranchAsync` + Trunkierung in `ExecuteGitRefBranchAsync` (Schritt 2) |
| `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | modified | +12/−4 | Delegate `find_references` + `get_impact` um `int maxResults = 50` ergänzt + Description um Trunkierungs-Satz erweitert (Schritt 3) |
| `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs` | modified | +18/−5 | Bestehende 2 Tests um `maxResults: 50` erweitert + 1 neuer Trunkierungs-Test (Schritte 6+7) |
| `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs` | modified | +37/−16 | Bestehende 6 Tests um `maxResults: 50` erweitert + 2 neue Trunkierungs-Tests (Symbol + Git-Branch) (Schritte 6+7) |
| `src/AiNetLinter.Tests/Commands/McpServerCommandFindReferencesTests.cs` | **new** | +48/−0 | 1 E2E-Test: `find_references` mit `maxResults: 2` triggert Trunkierung im echten Subprozess (Schritt 8) |
| `src/AiNetLinter.Tests/Commands/McpServerCommandGetImpactTests.cs` | **new** | +77/−0 | 2 E2E-Tests: `get_impact` Symbol-Branch + Git-Branch mit `maxResults: 2` (Schritt 8) |
| `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Caller.cs` | modified | +12/−0 | `RunTwice()` + `RunThrice()` mit `Greet`-Aufrufen → 5+ Call-Sites für `Greeter.Greet` (Schritt 4a) |
| `tests/Fixtures/GitImpactMini/src/GitImpactMini/CalculatorCaller.cs` | modified | +12/−0 | `RunTwice()` + `RunThrice()` mit `Add`-Aufrufen → 6 Call-Sites für `Calculator.Add` (Schritt 4b) |

**Nicht committet** (bewusst, A4):
- `.todos/004.md` und `.todos/dogfood-004.py` (pre-existing, von 004)
- `.todos/dogfood-005.py` (mein Dogfooding-Hilfsskript für 005, nicht Teil des Commits)
- `tasks/codegraph-mcp-server/units/005/coder-todo.md` (mein Coder-Tracking, nicht Teil des Commits)

**Nicht modifiziert** (bewusst, Scope-Grenze + A5):
- `src/AiNetLinter/Mcp/McpTruncation.cs` — `TruncateLines` deckt beide Tools 1:1 ab
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — keine Änderung (nur Re-Messung für TD-014)
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — keine Änderung
- `src/AiNetLinter/Mcp/McpToolResults.cs` — keine Änderung
- `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` / `FindSymbolScanner.cs` — 004-Scope
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (426 Z., unter 500-Limit) — keine Änderung
- `tests/Fixtures/SymbolGraphMini/...` (außer `Caller.cs`) — keine Änderung
- `rules.json` — keine Änderung
- `Docs/**` — EPIC-08
- `konzept.md`, `tech-debt.md`, Projektregeln — A7

## Commit

```
3eb13bfce5562fb7cf6e559b98566f06d5736ee9
feat(mcp): find_references + get_impact trunkierung (P0/P1) [codegraph-mcp-server]
9 files changed, 244 insertions(+), 23 deletions(-)
```

Branch: `main`. Push-Status: **nein** (per A4).

## Build-/Test-Output

### Build (mit Test-Projekt)

```
$ dotnet build AiNetLinter.slnx
  Wiederherzustellende Projekte werden ermittelt...
  Alle Projekte sind für die Wiederherstellung auf dem neuesten Stand.
  AiNetLinter -> C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\bin\Debug\net10.0\AiNetLinter.dll
  AiNetLinter.Tests -> C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\bin\Debug\net10.0\AiNetLinter.Tests.dll

Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler
```

### Targeted Re-Run (neue + modifizierte Tests)

```
$ dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj --no-build \
  --filter "FullyQualifiedName~FindReferencesTool|FullyQualifiedName~GetImpactTool|FullyQualifiedName~McpServerCommandFindReferences|FullyQualifiedName~McpServerCommandGetImpact"
Bestanden!   : Fehler:     0, erfolgreich:    18, übersprungen:     0, gesamt:    18, Dauer: 58 s
```

**18 Tests grün:**
- 7 `FindReferencesToolTests` (5 ResolveSymbolAsync + 1 ExecuteAsync NoSolutionLoaded + 1 ExecuteAsync Truncation)
- 8 `GetImpactToolTests` (4 ExecuteAsync Symbol + 1 BothGitRef + 1 NoSolutionLoaded + 2 Truncation)
- 1 `McpServerCommandFindReferencesTests` (E2E-Truncation)
- 2 `McpServerCommandGetImpactTests` (E2E Symbol-Branch + Git-Branch)

### Volllauf

```
$ dotnet test AiNetLinter.slnx --no-build
Bestanden!   : Fehler:     0, erfolgreich:  1114, übersprungen:     0, gesamt:  1114, Dauer: 7 m 25 s
```

**1114/1114 grün** (vor 005: 1108, +6 Tests = +1 FindReferences + +2 GetImpact + +1 E2E FindReferences + +2 E2E GetImpact).

### Self-Lint

```
$ dotnet run --project src/AiNetLinter -- --path . --config rules.json
# Run: 2026-08-01 15:50:37
OK
```

**0 Violations**, 0 Warnungen. `MaxMethodParameterCount: 4`-Regel greift bei 5-Parameter-Signaturen mit 2 Defaults (analog 004) nicht, weil der Roslyn-`MaxMethodParameterCount`-Analyzer Defaults offenbar nicht zählt.

## A3-Fehlschlag-Nachweis (3 neue Trunkierungs-Tests)

### Test 1: `FindReferencesToolTests.ExecuteAsync_ValidSymbolWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine`

**Aktion:** in `FindReferencesTool.cs` temporär `McpTruncation.TruncateLines(callSites, callSites.Count, normalizedMaxResults)` durch `string.Join("\n", callSites)` ersetzt.

**Wortwörtlicher Failure-Output:**
```
  Fehler AiNetLinter.Tests.Mcp.Tools.FindReferencesToolTests.ExecuteAsync_ValidSymbolWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine [5 s]
  Fehlermeldung:
   Assert.Contains() Failure: Sub-string not found
String:    "src/SymbolGraphMini/Caller.cs:8 - Aufruf von 'Gree"···
Not found: "Treffer gesamt"
  Stapelverfolgung:
     at AiNetLinter.Tests.Mcp.Tools.FindReferencesToolTests.ExecuteAsync_ValidSymbolWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine() in C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\Mcp\Tools\FindReferencesToolTests.cs:line 110
```

**Revert** + Re-Test: Test grün, alle 7 `FindReferencesToolTests` grün.

### Test 2: `GetImpactToolTests.ExecuteAsync_SymbolIdentifierWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine`

**Aktion:** in `GetImpactTool.cs` `ExecuteSymbolBranchAsync`-Zweig temporär `McpTruncation.TruncateLines` durch `string.Join("\n", callSites)` ersetzt.

**Wortwörtlicher Failure-Output:**
```
  Fehler AiNetLinter.Tests.Mcp.Tools.GetImpactToolTests.ExecuteAsync_SymbolIdentifierWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine [3 s]
  Fehlermeldung:
   Assert.Contains() Failure: Sub-string not found
String:    "src/SymbolGraphMini/Caller.cs:8 - Aufruf von 'Gree"···
Not found: "Treffer gesamt"
  Stapelverfolgung:
     at AiNetLinter.Tests.Mcp.Tools.GetImpactToolTests.ExecuteAsync_SymbolIdentifierWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine() in C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\Mcp\Tools\GetImpactToolTests.cs:line 111
```

**Revert** + Re-Test: Test grün, alle 8 `GetImpactToolTests` grün.

### Test 3: `GetImpactToolTests.ExecuteAsync_GitRefUncommittedWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine`

**Aktion:** in `GetImpactTool.cs` `ExecuteGitRefBranchAsync`-Zweig temporär `McpTruncation.TruncateLines` durch `string.Join("\n", callSites)` ersetzt.

**Wortwörtlicher Failure-Output:**
```
  Fehler AiNetLinter.Tests.Mcp.Tools.GetImpactToolTests.ExecuteAsync_GitRefUncommittedWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine [5 s]
  Fehlermeldung:
   Assert.Contains() Failure: Sub-string not found
String:    "src/GitImpactMini/CalculatorCaller.cs:8 - Aufruf v"···
Not found: "Treffer gesamt"
  Stapelverfolgung:
     at AiNetLinter.Tests.Mcp.Tools.GetImpactToolTests.ExecuteAsync_GitRefUncommittedWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine() in C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\Mcp\Tools\GetImpactToolTests.cs:line 132
```

**Revert** + Re-Test: Test grün, alle 8 `GetImpactToolTests` grün.

**A3 erfüllt für alle 3 neuen Trunkierungs-Tests** — jeder Test schlägt nachweislich fehl, wenn die `McpTruncation.TruncateLines`-Anwendung entfernt wird.

**A3 für E2E-Tests implizit** (analog 004-Methode, E2E-Auslöser wäre Subprozess-Neustart ~10s pro Lauf, A3 nicht zwingend pro 002-Plan-Methode).

## Footprint-Messung TD-011 (Pflicht, vor und nach)

### Vor 005 (gemessen 2026-08-01 15:34, Stand `9d2dd99`)

| Klasse | Z. | Limit | Puffer |
|---|---:|---:|---:|
| `FindReferencesTool` | 2519 | 2700 (PathOverride) | 181 |
| `GetImpactTool` | 2490 | 2500 | **10** ⚠ |
| `SymbolGraphToolRegistrations` | 2490 | 2500 | **10** ⚠ |
| `McpServerOptionsFactory` | 2484 | 2500 | 16 |
| `McpTruncation` | 70 | 2500 | — |

### Nach 005 (gemessen 2026-08-01 15:51, Stand `3eb13bf`)

| Klasse | Δ | Z. | Limit | Puffer | Status |
|---|---:|---:|---:|---:|---|
| `FindReferencesTool` | +3 | 2522 | 2700 (PathOverride) | 178 | ✓ im Limit |
| `GetImpactTool` | +5 | 2495 | 2500 | 5 | ✓ im Limit (knapp) |
| `SymbolGraphToolRegistrations` | +4 | 2494 | 2500 | 6 | ✓ im Limit (knapp) |
| `McpServerOptionsFactory` | 0 | 2484 | 2500 | 16 | ✓ unverändert (TD-014) |
| `McpTruncation` | 0 | 70 | 2500 | — | ✓ unverändert |

**Wortwörtliche Mess-Befehle:**
```
$ dotnet run --project src/AiNetLinter -- --footprint FindReferencesTool --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.Tools.FindReferencesTool':
Gesamt transitive Zeilen: 2522

$ dotnet run --project src/AiNetLinter -- --footprint GetImpactTool --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.Tools.GetImpactTool':
Gesamt transitive Zeilen: 2495

$ dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.SymbolGraphToolRegistrations':
Gesamt transitive Zeilen: 2494

$ dotnet run --project src/AiNetLinter -- --footprint McpTruncation --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.McpTruncation':
Gesamt transitive Zeilen: 70
```

**Plan-Abweichungen:**

- **Plan-Abweichung 1 (Symbol-Branch-Delegation an `FindReferencesTool`):** **NICHT ausgelöst.** `GetImpactTool` bleibt mit 2495 Z. unter dem 2500-Limit (Puffer 5 Z.). Der geschätzte Footprint-Zuwachs von +10-12 Z. hat sich als +5 Z. herausgestellt (nur 1 Import + 1 Parameter + 2 Trunkierungs-Aufrufe + 2 Normalisierungen pro Branch — keine zusätzlichen Hilfsvariablen, weil `string.Join`-Ersetzung 1:1 funktioniert).
- **Plan-Abweichung 2 (Description-Kürzung für `SymbolGraphToolRegistrations`):** **NICHT ausgelöst.** Mit 2494 Z. ist Puffer 6 Z. nach 005 — knapp, aber sicher unter Limit.
- **Plan-Abweichung 3 (Git-Branch-Projektion für `DiffImpactAnalyzer.AnalyzeAsync`):** **NICHT ausgelöst.** `AnalyzeAsync` liefert `Task<List<string>>` (verifiziert in `Core/DiffImpactAnalyzer.cs:35`), ist also implizit `IReadOnlyList<string>` und passt 1:1 auf `TruncateLines(IReadOnlyList<string>, int, int)`.

**TD-008/TD-011/TD-014 nach 005:**
- **TD-008:** `FindReferencesTool` PathOverride 2700 unverändert (Puffer 178 Z. ist großzügig). Kein PathOverride für `GetImpactTool` hinzugefügt. **Status: offen** (kein Anlass zur Schließung).
- **TD-011:** `SymbolGraphToolRegistrations` Puffer 6 Z. nach 005 — 5. Registrar-Klasse beim nächsten Symbolgraph-Tool zwingend nötig (z. B. `get_symbol_body` aus P2-Backlog oder künftige Erweiterungen). **Status: offen**.
- **TD-014:** `McpServerOptionsFactory` Puffer 16 Z. (unverändert, nicht in 005 angefasst). **Status: offen**.

## Beobachtungen außerhalb des Scopes (Tech-Debt-Kandidaten für den Kritiker)

1. **`MaxMethodParameterCount: 4` mit Defaults — 004 und 005 haben gezeigt, dass 5-Parameter-Signaturen mit 2 Defaults bei `internal static`-Methoden legal sind** (Analyzer reißt nicht). Das ist eine **stillschweigende Reserve** in der Regel-Konfiguration. Wenn die Konzept-P0/P1-Extensions (z. B. `--mcp-log` aus `konzept.md` Z. 285-293, "lädt noch"-Zustand) weitere Parameter an `McpCodeGraphServer.ExecuteAsync` oder Tool-`ExecuteAsync`-Methoden hängen, **könnte** die Reserve reißen. **Vorschlag:** Regeln `MaxMethodParameterCount` und `MaxMethodParameterCountForNonPublic` explizit dokumentieren oder einen Pre-Build-Check im Build-Script verankern, statt empirisch pro Einheit zu verifizieren. **Nicht selbst umsetzen, A2/A5.**

2. **`Caller.Run` vs. `RunTwice`/`RunThrice` — `ResolveSymbolAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbolError` Test passt weiterhin** (nur 2 Symbole mit Name "Run" vorhanden: `Caller.Run` + `OtherCaller.Run`, `RunTwice`/`RunThrice` haben andere Symbol-Namen). Die SymbolFinder-Suche in `ResolveByNameAsync` filtert strikt auf `name == lastSegment` — also kein Risiko, dass die Fixture-Erweiterung bestehende Tests bricht. **Kein Tech-Debt, nur Beobachtung.**

3. **`SymbolGraphToolRegistrations` Puffer 6 Z. — beim nächsten Symbolgraph-Tool (z. B. P2-Backlog `get_symbol_body`) wird 5. Registrar-Klasse zwingend nötig** (TD-011-Vorhersage bestätigt sich nach 005). TD-011-Vorschlag (siehe `tech-debt.md`) bleibt gültig — vor jedem neuen Tool die Footprints re-messen, nicht reaktiv nach Reißen.

4. **`McpServerOptionsFactory` Puffer 16 Z. (unverändert) — nächstes P0/P1-`McpCodeGraphServer`-Feature** (z. B. `--mcp-log`, "lädt noch"-Zustand, `rules.json`-Auto-Discovery) wird die Klasse wahrscheinlich erweitern, dann TD-014-Refactor (Aufteilung in Builder-Pattern oder Init-`record`) fällig.

5. **`McpServerCommandTests.cs` ist nur noch 426/500 Z.** (Plan-Annahme 499/500 basierte auf älterem Stand; tatsächlich sind 74 Z. Puffer übrig). Theoretisch wäre Platz für einen weiteren E2E-Test dort — **aber 004-Pattern konsequent weitergeführt** (eigene `McpServerCommand*Tests.cs`-Dateien pro Tool). Kein Tech-Debt, nur Beobachtung.

## Dogfooding gegen `AiNetLinter.slnx` (Schritt 10)

Wortwörtlicher Output des Dogfooding-Skripts (Subprozess-Aufruf
`AiNetLinter --mcp-server --path C:\Daten\Entwicklung\Ralf\AiNetLinter`,
JSON-RPC über stdio, Tool-Aufrufe mit `maxResults`-Parameter):

```
=== initialize ===
=== tools/list ===
=== find_references(DiffImpactAnalyzer.FindCallSitesAsync, maxResults=5) ===
=== get_impact(symbolIdentifier=DiffImpactAnalyzer.FindCallSitesAsync, maxResults=3) ===
=== get_impact(gitRef=HEAD, maxResults=10) (uncommittete diff) ===
  serverInfo.name: ainetlinter
  serverInfo.version: 1.0.78.0
  --- find_references description ---
  Findet alle Aufrufstellen eines C#-Symbols (Datei:Zeile:Spalte oder qualifizierter/teil-qualifizierter Name). Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. Trunkiert standardmaessig auf 50 Treffer, ueberschreibbar via maxResults; Trunkierungs-Meta-Zeile meldet die Gesamt-Trefferzahl.
  --- get_impact description ---
  Findet Aufrufstellen geaenderter C#-Signaturen. Entweder gitRef (Git-Commit-Ref, leer = uncommittete Aenderungen) ODER symbolIdentifier (Datei:Zeile:Spalte oder qualifizierter Name) angeben, nie beide. Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. Trunkiert standardmaessig auf 50 Treffer, ueberschreibbar via maxResults; Trunkierungs-Meta-Zeile meldet die Gesamt-Trefferzahl.
  --- response id=3 (find_references, maxResults=5) ---
  src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:19 - Aufruf von 'DiffImpactAnalyzer.FindCallSitesAsync' in Projekt 'AiNetLinter'
  src/AiNetLinter/Mcp/Tools/GetImpactTool.cs:17 - Aufruf von 'DiffImpactAnalyzer.FindCallSitesAsync' in Projekt 'AiNetLinter'
  src/AiNetLinter/Core/DiffImpactAnalyzer.cs:222 - Aufruf von 'DiffImpactAnalyzer.FindCallSitesAsync' in Projekt 'AiNetLinter'
  src/AiNetLinter/Mcp/Tools/GetImpactTool.cs:52 - Aufruf von 'DiffImpactAnalyzer.FindCallSitesAsync' in Projekt 'AiNetLinter'
  src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:37 - Aufruf von 'DiffImpactAnalyzer.FindCallSitesAsync' in Projekt 'AiNetLinter'
  (5/5 Treffer, keine Trunkierung — exakt am Limit, keine Meta-Zeile)
  --- response id=4 (get_impact Symbol-Branch, maxResults=3) ---
  src/AiNetLinter/Mcp/Tools/GetImpactTool.cs:17 - Aufruf von 'DiffImpactAnalyzer.FindCallSitesAsync' in Projekt 'AiNetLinter'
  src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:19 - Aufruf von 'DiffImpactAnalyzer.FindCallSitesAsync' in Projekt 'AiNetLinter'
  src/AiNetLinter/Core/DiffImpactAnalyzer.cs:222 - Aufruf von 'DiffImpactAnalyzer.FindCallSitesAsync' in Projekt 'AiNetLinter'
  [5 Treffer gesamt, 3 gezeigt — Pattern verfeinern oder maxResults erhöhen]
  --- response id=5 (get_impact Git-Branch, maxResults=10) ---
  src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:56 - Aufruf von 'SymbolGraphToolRegistrations.Register' in Projekt 'AiNetLinter'
  src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs:11 - Aufruf von 'FindReferencesTool.ExecuteAsync' in Projekt 'AiNetLinter'
  src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs:19 - Aufruf von 'FindReferencesTool.ExecuteAsync' in Projekt 'AiNetLinter.Tests'
  src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs:40 - Aufruf von 'FindReferencesTool.ExecuteAsync' in Projekt 'AiNetLinter'
  src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs:90 - Aufruf von 'FindReferencesTool.ExecuteAsync' in Projekt 'AiNetLinter.Tests'
  src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs:106 - Aufruf von 'FindReferencesTool.ExecuteAsync' in Projekt 'AiNetLinter.Tests'
  src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs:53 - Aufruf von 'GetImpactTool.ExecuteAsync' in Projekt 'AiNetLinter'
  src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs:19 - Aufruf von 'GetImpactTool.ExecuteAsync' in Projekt 'AiNetLinter.Tests'
  src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs:33 - Aufruf von 'GetImpactTool.ExecuteAsync' in Projekt 'AiNetLinter.Tests'
  src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs:47 - Aufruf von 'GetImpactTool.ExecuteAsync' in Projekt 'AiNetLinter.Tests'
  [15 Treffer gesamt, 10 gezeigt — Pattern verfeinern oder maxResults erhöhen]
```

**Befund:**
- `find_references`: 5 Call-Sites von `DiffImpactAnalyzer.FindCallSitesAsync` im Repo, alle 5 ausgegeben (exakt am Limit, keine Trunkierung).
- `get_impact` Symbol-Branch: 5 Call-Sites, **3 gezeigt + Meta-Zeile** korrekt.
- `get_impact` Git-Branch (uncommittete Änderungen): **15 Call-Sites** (über `FindReferencesTool.ExecuteAsync` + `GetImpactTool.ExecuteAsync` + `SymbolGraphToolRegistrations.Register`), 10 gezeigt + Meta-Zeile korrekt.
- Beide Tool-Descriptions enthalten den Trunkierungs-Hinweis wie geplant.

**P0/P1-Trunkierung in `find_references` und `get_impact` ist live
verifiziert.** DoD-Kriterium aus `konzept.md` Z. 631-634 für die
beiden Tools erfüllt.

## Harte Scope-Grenze eingehalten

- **KEINE** Scanner-Splits für `find_references`/`get_impact` (TD-005-Generalisierung wäre eigenes Refactor-Thema)
- **KEINE** weiteren P0/P1-Extensions (Kaltstart, Auto-Discovery, Staleness-Sweep-`mtime`, `--mcp-log`)
- **KEINE** Änderung an `McpServerOptionsFactory` über Pflichtmessung hinaus
- **KEIN** `PathOverrides`-Wert erhöht, kein neuer `PathOverride`
- **KEIN** Eingriff in `McpCodeGraphServer`, `McpToolResults`, `LinterErrorFormatter`, `SymbolIdentifierResolver`, `McpTruncation`, `FindSymbolTool`/`FindSymbolScanner`, `SearchPatternTool`/`SearchPatternScanner`
- **KEINE** Doku (`Docs/agent-api.md`, `Docs/ROADMAP.md`)
- **KEINE** Folge-Einheit geplant
- **NIEMALS** in ein anderes Projektverzeichnis gewechselt (alle Operationen in `C:\Daten\Entwicklung\Ralf\AiNetLinter`)
- **NUR EIN COMMIT** (A5, kein Nachpolieren)
- **GEZIELTES `git add`** (A4, kein `-A`/`.`, kein Push)
