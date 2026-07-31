---
status: done
type: step-review
task: codegraph-mcp
step: 003/fix-01
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T20:00:00Z
verdict: approved
tech_debt_ids: []
---

# Review Step 003/fix-01: Fix: Test-Abdeckung für den "Solution nicht geladen"-Fehlerpfad in find_symbol

## Verdict

- [x] **approved**
- [ ] **issues**
- [ ] **blocked**

## Geprüft (Scope: ausschließlich Finding 1 aus `step-003/step-review.md`)

- [x] Plan-Erfüllung: beide „Konkrete Änderungen" (Datei 1 + Datei 2) 1:1 umgesetzt
- [x] Rules-Konformität: `#nullable enable`, `sealed`, keine neue Warnung
- [x] Logische Korrektheit: das MAJOR-Finding ist tatsächlich geschlossen (Details unten)
- [x] Konzept-Treue: kein Scope-Sprung, kein Non-Goal berührt
- [x] Build: selbst nachgeprüft, grün, 0 Warnungen
- [x] Tests: selbst nachgeprüft, grün (1036 Tests gesamt, inkl. der 8 neuen/erweiterten in den beiden Zieldateien)

## Befund

### Kernfrage: Ist der ursprüngliche MAJOR-Fund wirklich behoben?

Ja. Verifiziert direkt am Produktionscode, nicht nur an der Testbehauptung:

- `McpCodeGraphServer`-Konstruktor (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs:28-37`):
  bei `catalog is null` bleibt `_catalog` `null`, `InitializeFileState` wird
  nicht aufgerufen. `IsLoaded => _catalog is not null` (Zeile 39) ist danach
  tatsächlich `false`. `GetCurrentSolution()` (Zeile 45-54) prüft
  `_catalog is null` **zuerst** und gibt `null` zurück, **bevor**
  `RefreshStaleDocuments()` (das mit `_catalog = null` crashen würde)
  überhaupt erreicht wird — der neue Test
  `new McpCodeGraphServer(null)` bringt den Server also tatsächlich in
  exakt den Zustand, den das Finding verlangt hat, ohne Umweg oder
  versteckten Fallback.
- `FindSymbolTool.ExecuteAsync` (`src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs:27-35`):
  `state.GetCurrentSolution()` liefert `null` → sofortiges `return
  McpToolResults.SolutionNotLoaded()`, `FindMatchesAsync` wird nicht
  aufgerufen. Der neue Test ruft exakt diesen Delegate-Einstiegspunkt auf
  (nicht etwa nur `FindMatchesAsync` erneut) — es ist also wirklich der
  zuvor ungetestete zweite Hauptpfad, nicht eine Wiederholung der
  bestehenden Tests.
- `McpToolResults.SolutionNotLoaded()` → `Error(LinterErrorCodes.SolutionNotLoaded, ...)`
  (`src/AiNetLinter/Mcp/McpToolResults.cs:21-41`) setzt `IsError = true`
  und baut den Text über `LinterErrorFormatter.Format`, der laut
  `LinterErrorCodes.SolutionNotLoaded = "SOLUTION_NOT_LOADED"`
  (`src/AiNetLinter/Output/LinterErrorCodes.cs:21`) wörtlich
  `SOLUTION_NOT_LOADED` im Ergebnistext enthält. Der neue Test prüft
  `Assert.True(result.IsError)` **und** `Assert.Contains("SOLUTION_NOT_LOADED",
  textContent.Text)` — beide vom Finding geforderten Assertions sind
  vorhanden, keine Abschwächung (z. B. kein bloßes „ist nicht null").
- Die neue `McpToolResultsTests.cs` testet zusätzlich `Error`,
  `SolutionNotLoaded` und `Text` isoliert — korrekt als eigenständige,
  wiederverwendbare Infrastruktur für alle 9 EPIC-03-Tools erkannt (im
  Finding als „optional" vorgeschlagen, hier tatsächlich mit umgesetzt).
  Keine Scheinabdeckung: jeder der drei Tests prüft ein unterscheidbares
  Verhalten (Fehlerformat, konkreter Code, Erfolgsfall ohne `IsError`).

Damit ist der im ursprünglichen Review konkret benannte Lückentyp — „sieht
beim Lesen richtig aus, ist aber nie tatsächlich über das echte
Tool-Delegate ausgeführt worden" — geschlossen: der neue Test ruft
tatsächlich `FindSymbolTool.ExecuteAsync` (das echte Delegate, keine
Kopie/kein Mock) mit einem echten `McpCodeGraphServer`-Objekt in
nicht-geladenem Zustand auf.

### Plan-Erfüllung

Beide „Konkrete Änderungen"-Punkte aus dem Fix-Plan wie vorgesehen
umgesetzt (Diff `9d6cecc` deckt sich exakt mit Plan-Beschreibung: Cast
über `Assert.IsType<TextContentBlock>(Assert.Single(result.Content))`,
keine Abweichung). DoD-Punkte (Build grün ohne neue Warnung, Tests grün,
Commit mit Conventional-Commit-Message + `[codegraph-mcp]`-Suffix,
`step-result.md` vorhanden, Status auf `done` gesetzt) erfüllt.

### Rules-Konformität

`#nullable enable` in `McpToolResultsTests.cs` vorhanden, Klasse
`sealed`. Kein leerer `catch`, keine Methodenlängen-/Parameteranzahl-Probleme
(triviale, parameterlose Testmethoden). Kein neuer Verstoß.

### Konzept-Treue

Reine Testergänzung, kein Produktionscode geändert (verifiziert: Diff
enthält ausschließlich die zwei Testdateien). Kein Scope-Sprung über den
Fix-Plan hinaus, `TD-004` bewusst nicht angefasst (wie in „Notes" des
Fix-Plans explizit ausgeschlossen).

### Build-/Test-Status (selbst verifiziert)

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen
dotnet test AiNetLinter.slnx  → grün, 1036 Tests, 0 Fehler, ~1m46s
```

## Findings

Keine.
