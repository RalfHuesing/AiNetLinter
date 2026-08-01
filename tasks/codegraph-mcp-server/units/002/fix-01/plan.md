---
unit: 002
fix_round: 01
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-01
fix_target: M-1
trigger_review: units/002/review.md (Verdict: issues, 1 MAJOR + 6 MINOR)
trigger_result: units/002/result.md (Commit 91278ea)
trigger_plan: units/002/plan.md (Commit 286233d)
---

# Plan Fix-Runde 002/fix-01 — M-1: `McpToolResults.InvalidArgument`-Helper liefert irreführenden Hint für `search_pattern`

## Ziel der Fix-Runde

**Genau ein** funktionaler Bug wird behoben: `SearchPatternTool` ruft beim
leeren `pattern` den `get_impact`-spezifischen Helper
`McpToolResults.InvalidArgument` auf, dessen Hint hartcodiert
"Entweder `gitRef` ODER `symbolIdentifier` angeben, nie beide."
(`McpToolResults.cs:74-80`) ist und für `search_pattern` semantisch
falsch liegt. Der Fix ersetzt den Aufruf durch den **bereits im selben
Tool korrekt verwendeten** Helper-Pfad (`SearchPatternTool.cs:57-60`)
und schärft Test 8 so, dass eine spätere Regression in dieselbe Falle
**vom Test gefangen** wird (A3).

## Harte Scope-Grenze (was diese Runde NICHT macht)

Explizit wiederholt, damit der Coder keine Eigenmächtigkeit hat:

- **Keine** der 6 MINOR-Beobachtungen aus `units/002/review.md`
  (O-1 bis O-6) werden angefasst. A5/A2 — kein Scope-Creep in
  Fix-Runden. Jeder MINOR ist ein eigener Tech-Debt-Vorschlag
  (TD-010, TD-011) oder eine eigene Folge-Einheit; der Nutzer
  entscheidet separat.
- **Keine** Edits an `tech-debt.md`, `konzept.md`, `state.md`,
  `Docs/**`, `README.md`, `rules.json` (A7, EPIC-08 nicht in
  002-Scope).
- **Keine** Umbenennung oder Refaktorierung von
  `McpToolResults.InvalidArgument` zu `InvalidArgumentExclusiveParams`
  o. ä. — wäre Scope-Creep. Der Helper bleibt für `get_impact`
  (seinen dokumentierten Zweck in `McpToolResults.cs:71-73`);
  `search_pattern` nutzt einfach den korrekten Helper.
- **Keine** Änderungen an `SearchPatternScanner.cs`,
  `AnalysisToolRegistrations.cs`, `McpTruncation.cs`,
  `McpServerCommandTests.cs`, `McpCodeGraphServer.cs`. M-1 ist
  ausschließlich im Tool.
- **Keine** Änderungen an den 7 anderen Unit-Tests in
  `SearchPatternToolTests.cs` oder am E2E-Test. Nur Test 8 wird
  erweitert.
- **Keine** Footprint-Re-Messung TD-004. M-1 ist eine 1-Zeilen-
  Korrektur ohne Bezug zu `McpCodeGraphServer.Config`; der
  existierende 2482/2500-Wert aus `units/002/result.md:169` bleibt
  gültig.
- **Keine** `git`-Operationen durch den Coder, die über
  `git add` + `git commit` hinausgehen. Kein Push, kein Amend,
  kein Rebase (A4).

## Betroffene Dateien (genau 2)

| Datei | Änderung |
|---|---|
| `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` | 1 Zeile Aufruf-Ersatz (Z. 40), analog Z. 57-60 |
| `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs` | Test 8 (Z. 162-174) um Hint-Assertion erweitern |

Keine weiteren Dateien. Keine `using`-Änderungen nötig
(`LinterErrorCodes` ist bereits via `using AiNetLinter.Output;` in
`SearchPatternTool.cs:6` importiert; im Test deckt das bestehende
`Assert.Contains("Pruefe pattern auf gueltige Regex-Syntax", …)`
in Z. 158 das Muster ab, an dem Test 8 sich orientiert).

## Konkrete Code-Änderung 1 — `SearchPatternTool.cs:40`

**Alter Aufruf (Z. 38-41, wortwörtlich):**

```csharp
if (string.IsNullOrEmpty(pattern))
{
    return McpToolResults.InvalidArgument("pattern darf nicht leer sein.");
}
```

**Neuer Aufruf (Z. 38-41, wortwörtlich):**

```csharp
if (string.IsNullOrEmpty(pattern))
{
    return McpToolResults.Error(
        LinterErrorCodes.InvalidArgument,
        "pattern darf nicht leer sein.",
        hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.");
}
```

**Begründung (Konsistenz im selben Tool, A1):** 14 Zeilen tiefer
(`SearchPatternTool.cs:55-61`) ist der Regex-Pfad bereits exakt so
implementiert:

```csharp
catch (ArgumentException ex)
{
    return McpToolResults.Error(
        LinterErrorCodes.InvalidArgument,
        $"Ungueltige Regex: {ex.Message}",
        hint: "Pruefe pattern auf gueltige Regex-Syntax.");
}
```

Der neue Aufruf folgt diesem Muster 1:1 (gleiche `Error`-Signatur,
gleiche Argument-Reihenfolge, gleiche `hint`-Position als benannter
Parameter, gleicher `[ERROR]: INVALID_ARGUMENT: …\n  hint: …`-
Output über `LinterErrorFormatter.Format`). Das behebt die
Inkonsistenz im selben Tool und liefert dem Agenten einen
**semantisch passenden** Hint ohne `get_impact`-Bezug.

**Hint-Wortlaut — Planer-Empfehlung** (Coder darf Wortlaut wählen,
Empfehlung: `"Pattern angeben — leeres Pattern ist nicht erlaubt."`):

- nennt das konkrete Argument (`Pattern`) — semantisch klar für
  `search_pattern`,
- enthält das Em-Dash `—` (U+2014) analog zur Trunkierungs-Meta-
  Zeile in `McpTruncation.cs:40` (Konsistenz im
  Linter-Output-Format),
- hat **keinen** Bezug zu `gitRef`/`symbolIdentifier` (das ist der
  scharfe A3-Test, siehe unten),
- ist Negativ-Formulierung statt "Pruefe …" — direkter, kein
  Doppel-Hinweis.

**Negativ-Ausschluss (vom Coder zu prüfen):** der finale Hint darf
im Wortlaut **nicht** die Strings `gitRef` oder `symbolIdentifier`
enthalten — sonst ist die A3-Assertion zu schwach und fängt die
Regressionsform nicht.

## Konkrete Code-Änderung 2 — Test 8 in `SearchPatternToolTests.cs:162-174`

**Alter Test 8 (Z. 162-174, wortwörtlich):**

```csharp
[Fact]
public async Task ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError()
{
    using var fixture = new SymbolGraphMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    using var state = new McpCodeGraphServer(catalog);

    var result = await SearchPatternTool.ExecuteAsync(
        state, pattern: "", isRegex: false, maxResults: 50, CancellationToken.None);

    Assert.True(result.IsError);
    var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
    Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
}
```

**Neuer Test 8 (Z. 162-?, wortwörtlich):**

```csharp
[Fact]
public async Task ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError()
{
    using var fixture = new SymbolGraphMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    using var state = new McpCodeGraphServer(catalog);

    var result = await SearchPatternTool.ExecuteAsync(
        state, pattern: "", isRegex: false, maxResults: 50, CancellationToken.None);

    Assert.True(result.IsError);
    var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
    Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    // M-1-Regression-Schutz: der Hint muss search_pattern-spezifisch sein, nicht der
    // get_impact-Hartkodierung ("Entweder gitRef ODER symbolIdentifier angeben, nie beide.")
    // aus McpToolResults.InvalidArgument. Der konkrete Hint-Wortlaut ist im Tool fixiert;
    // diese Assertion haengt an der gleichen Formulierung wie der Code-Fix. Bei
    // Aenderung des Hint-Wortlauts im Tool ist diese Assertion mitzuaendern.
    Assert.Contains("Pattern angeben", textContent.Text, StringComparison.Ordinal);
    Assert.DoesNotContain("gitRef", textContent.Text, StringComparison.Ordinal);
    Assert.DoesNotContain("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
}
```

**Begründung der Schärfe (A3-Pflicht):**

- `Assert.Contains("INVALID_ARGUMENT", …)` bleibt — sichert den
  Fehler-Code (auch der ursprüngliche Bug liefert diesen Code).
- `Assert.Contains("Pattern angeben", …)` — der **neue** Assertion-
  Teil. Würde bei der ursprünglichen `McpToolResults.InvalidArgument`-
  Nutzung **rot** werden, weil der `get_impact`-Hint
  "Entweder `gitRef` ODER `symbolIdentifier` angeben, nie beide."
  das Wort "Pattern angeben" **nicht** enthält. Das ist der
  primäre Bug-Detektor.
- `Assert.DoesNotContain("gitRef", …)` + `Assert.DoesNotContain("symbolIdentifier", …)`
  — Defensiv-Assertions, die den `get_impact`-Hint-Wortlaut
  explizit ausschließen. Würde bei der ursprünglichen
  `McpToolResults.InvalidArgument`-Nutzung ebenfalls **rot**
  werden. Doppelte Absicherung: falls der Hint-Wortlaut im Tool
  später zu "Pattern" abgeschwächt würde (z. B. nur "Pattern"
  statt "Pattern angeben"), fängt das DoesNotContain die
  `get_impact`-Regression weiterhin.
- Drei Assertions statt einer — minimal-invasiv, aber
  bug-spezifisch. Konsistent mit Test 7 (Z. 145-159), der bereits
  ein analoges Hint-Assertion-Paar nutzt
  (`Assert.Contains("INVALID_ARGUMENT", …)` +
  `Assert.Contains("Pruefe pattern auf gueltige Regex-Syntax", …)`).

**Hinweis zur Hint-Wortlaut-Kopplung:** der Coder MUSS bei Änderung
des Hint-Wortlauts in `SearchPatternTool.cs` Z. 41 die Assertion in
Z. 173-174 entsprechend anpassen — das ist die bewusste
Kopplung von Code- und Test-Wortlaut, exakt wie bei Test 7
(`Pruefe pattern auf gueltige Regex-Syntax` ↔ Test-Assert auf
denselben String). Steht im Code-Kommentar der neuen Assertions
als Hinweis für künftige Editierer.

## A3-Fehlschlag-Nachweis-Anleitung (PFLICHT)

**Vorbedingung:** Baseline `dotnet test --filter "FullyQualifiedName~SearchPattern"`
ist grün (1097/1097 — vom 002-Coder-Run aus `units/002/result.md:64`
belegt; bei Bedarf frisch messen).

**Schritt-Folge (vom Coder exakt einzuhalten und im `result.md` zu
protokollieren):**

1. **Code-Fix anwenden** (Änderung 1) **und gleichzeitig** Test 8
   erweitern (Änderung 2) — beide im selben Commit.
2. **Erstlauf:** `dotnet test --filter
   "FullyQualifiedName~ExecuteAsync_EmptyPattern"` → **grün**
   (neue Assertion "Pattern angeben" matched den neuen Hint).
   `dotnet test --filter "FullyQualifiedName~SearchPattern"` →
   9/9 grün (alle anderen Tests unberührt).
3. **A3-Auslöser:** in `SearchPatternTool.cs:38-42` den **gerade
   frisch eingebauten** neuen Aufruf
   (`McpToolResults.Error(LinterErrorCodes.InvalidArgument, "pattern
   darf nicht leer sein.", hint: "Pattern angeben — leeres Pattern
   ist nicht erlaubt.")`) **temporär** wieder durch den
   **ursprünglichen** Aufruf
   (`McpToolResults.InvalidArgument("pattern darf nicht leer sein.")`)
   ersetzen. Test-Erweiterung in `SearchPatternToolTests.cs:173-174`
   bleibt **unverändert** aktiv.
4. **A3-Lauf:** `dotnet test --filter
   "FullyQualifiedName~ExecuteAsync_EmptyPattern"` → **rot**.
   Erwarteter Failure-Output (vom Coder wortwörtlich zu protokollieren):
   `Assert.Contains() Failure: Not found: "Pattern angeben"` (oder
   `Assert.DoesNotContain() Failure: Found: "gitRef"` — je nachdem,
   welche Assertion als erste fehlschlägt; `Assert.Contains` läuft
   zuerst, also `Not found: "Pattern angeben"` als primäre Diagnose).
5. **A3-Rückgängig:** den Schritt-3-Replace wieder rückgängig machen
   (neuer Aufruf wieder drin). Test 8 → **grün**.
6. **Volllauf:** `dotnet test AiNetLinter.slnx --no-build` →
   1097/1097 grün (oder 1098/1098 falls neue Hilfs-Test-Methode
   hinzukommt — kommt sie nicht, +0, also 1097/1097).
7. **Build:** `dotnet build AiNetLinter.slnx` → grün, 0 Warnungen,
   0 Fehler (Pflicht wegen `TreatWarningsAsErrors=true` aus
   `AiNetLinterRichtlinien.mdc` Z. 81).
8. **Dokumentation im `result.md`:** die Wortlaut-Protokolle aus
   Schritt 2, 4, 5, 6, 7 mit den exakten `dotnet`-Commands und
   der ersten ~10 Zeilen jedes Failure-Outputs (analog
   `units/002/result.md:132-138` für Test 8 im 002-Lauf).

**Was der A3-Nachweis zeigt:**

- Die `Assert.Contains("Pattern angeben", …)`-Assertion **erkennt**
  die ursprüngliche `McpToolResults.InvalidArgument`-Regression.
- Die `Assert.DoesNotContain("gitRef", …)`-Assertion **erkennt**
  sie ebenfalls (parallel, falls der Hint-Wortlaut im Tool später
  anders gewählt wird).
- Der `Assert.Contains("INVALID_ARGUMENT", …)`-Bestandteil allein
  hätte die Regression **nicht** erkannt — das war der
  A3-Schwäche-Punkt, den der 002-Review identifiziert hat
  (`units/002/review.md:174-179`).

## Konvention-Commit

- **Message:** `fix(mcp): search_pattern leerer-pattern-Hint [codegraph-mcp-server]`
  (Conventional Commits, deutscher Imperativ, Task-Suffix
  analog `units/002/result.md:223-226`).
- **Branch:** `main`.
- **Push:** nein (lokal; Orchestrator entscheidet).
- **Commit-Dateien:** `git add` gezielt auf die zwei geänderten
  Dateien, kein `-A`/`.` (A4). Commit-Hash im `result.md`
  protokollieren.

## Erwartete Verdict-Optionen

- **`approved`:** M-1 sauber behoben, A3 sauber dokumentiert, Test 8
  scharf genug, keine Nebenwirkungen. → 002 ist **abgeschlossen**,
  der Orchestrator kann die nächste Einheit aus `konzept.md` planen
  (offene Kandidaten: EPIC-05 Miss-Hint, EPIC-06 Robustheit,
  EPIC-07 Tests, EPIC-08 Doku, oder explizite MINOR-/TD-Behandlung).
- **`issues`:** M-1 unzureichend behoben (z. B. Hint-Wortlaut zu
  generisch, A3 nicht gezeigt, Test-Regression in einem anderen
  Test) → `002/fix-02/` (max 3 Fix-Runden pro Einheit laut
  `kernel.md` A1; aktueller Zähler: 0/3 für 002, also 2 verbleibend).
- **`blocked`:** Widerspruch zwischen `konzept.md` und M-1-Fix
  aufgedeckt (z. B. Konzept verlangt eine andere Hint-Strategie),
  oder A3-Nachweis technisch nicht führbar (z. B. weil
  `McpToolResults.Error` und `McpToolResults.InvalidArgument`
  byte-identischen Output liefern würden — was hier nicht der
  Fall ist, also nicht erwartet). → Nutzer klärt (A6).

## Bezug zu Projektregeln (minimal-invasiv, aber explizit)

- **`AiNetLinter.mdc` §4 (Conventional Commits)** — Commit-Message-
  Format beachten, deutscher Imperativ, Task-Suffix (siehe
  Konvention-Commit oben).
- **`AiNetLinterRichtlinien.mdc` §5 (Result-Pattern, kein `throw`)**
  — der Fix **erhält** das Result-Pattern, ersetzt nur den
  irreführenden Helper durch den korrekten. Kein rethrow,
  keine Exception-Propagierung.
- **`AiNetLinterRichtlinien.mdc` §1 (Einfachheit vor Abstraktion)**
  — die vorgeschlagene Lösung ist die **kleinstmögliche** Änderung
  (1 Zeile Aufruf-Ersatz + 3 Assertions in einem Test). Kein
  Helper-Refactor, keine API-Erweiterung, keine
  `McpToolResults.InvalidArgument`-Umbenennung (das wäre die
  TD-012-Variante, **nicht** in `fix-01/`-Scope).
- **`AiNetLinter.mdc` (`MaxMethodLineCount`, `MaxLineCount`,
  `EnforceSealedClasses`)** — durch 1 Zeile hinzu (statt
  1-Zeilen-Aufruf wird 5-Zeilen-`McpToolResults.Error`-Aufruf)
  bleibt die Methode mit 5 statt 1 zusätzlichen Zeilen unter
  60 Z. Methodenlänge (`ExecuteAsync` aktuell 28 Z., neu ~32 Z.,
  Puffer ~28 Z.). `SearchPatternToolTests` bleibt `sealed`,
  `MaxLineCount` ≤ 500 (aktuell 175 Z., neu ~185 Z., Puffer ~315).
- **`kernel.md` A3 (Tests müssen fehlschlagen können)** — der
  A3-Abschnitt oben ist die Pflicht-Operationalisierung dieser
  Regel **für den neuen Assertion-Teil**.

## Zusammenfassung der Zähler (für Orchestrator-Übernahme)

Nach `fix-01/` (Planer jetzt + Coder als nächstes + Kritiker danach)
sind die Aufrufe-Zähler (laut `units/002/...` und
`tasks/codegraph-mcp-server/state.md:95-105`):

- `max_aufrufe`: 5 (Stand jetzt) + 3 (fix-01 Planer + Coder +
  Kritiker) = **8/40**.
- `max_fix_pro_einheit` für 002: 0 (jetzt) → 1 (nach fix-01), 2
  verbleibend.
- `max_fix_gesamt`: 0 → 1, 11 verbleibend.

## Nächste Aktion des Orchestrators (für meinen Abschlussbericht)

- Plan `tasks/codegraph-mcp-server/units/002/fix-01/plan.md`
  committen (gezielt, A4: `git add` auf die eine Datei, kein Push).
- Dann Coder-Aufruf für `002/fix-01/` mit dem Plan als Eingabe.
