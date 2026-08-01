---
unit: 005
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-01
code_commit: 3eb13bfce5562fb7cf6e559b98566f06d5736ee9
plan_commit: 9d2dd99
result_commit: d6023e8
verdict: approved
---

# Review Einheit 005 — Trunkierung in `find_references` + `get_impact` (P0/P1)

**Verdict: approved** (0 CRITICAL, 0 MAJOR, 2 MINOR)

P0/P1-Trunkierung in den zwei verbleibenden Listen-Tools
`find_references` und `get_impact` sauber umgesetzt, exakt im
004-Pattern (maxResults-Default im MCP-Delegate, `McpTruncation.TruncateLines`
am Tool-Output, einheitliche Meta-Zeile). DoD-Kriterium aus `konzept.md`
Z. 631-634 damit für **alle vier** Listen-Tools erfüllt
(`search_pattern` 002, `find_symbol` 004, `find_references` + `get_impact`
005). 1114/1114 grün, A3 für die 3 Unit-Tests wortwörtlich dokumentiert,
keine Plan-Abweichung ausgelöst, harte Scope-Grenze eingehalten.

## Selbst-Verifikation

**Re-Run durchgeführt** (kein blinder Vertrauens-Bewertung):

| Schritt | Befehl | Ergebnis |
|---|---|---|
| Build | `dotnet build AiNetLinter.slnx` | grün, 0/0 |
| Footprint | `--footprint GetImpactTool` | **2495** (Coder: 2495) ✓ |
| Footprint | `--footprint SymbolGraphToolRegistrations` | **2494** (Coder: 2494) ✓ |
| Footprint | `--footprint FindReferencesTool` | **2522** (Coder: 2522) ✓ |
| E2E-Tests | `McpServerCommandFindReferences` + `McpServerCommandGetImpact` | **3/3 grün** ✓ |
| Unit-Tests | `FindReferencesToolTests` + `GetImpactToolTests` | **15/15 grün** ✓ |
| Self-Lint | `AiNetLinter --path . --config rules.json` | **OK** (0 Violations) ✓ |
| Volllauf | `dotnet test AiNetLinter.slnx --no-build` | **1114/1114 grün** ✓ |

Alle Behauptungen aus `result.md` baustein-genau verifiziert.

## Plan-Erfüllung (Schritt-für-Schritt)

| Plan-Schritt | Status | Beleg |
|---|---|---|
| 0 — Pre-Build-Check `maxResults`-Parameter | ✓ | `result.md` Z. 34-76 (CS1503-Fallback wortwörtlich) |
| 1 — `FindReferencesTool.cs` Trunkierung | ✓ | `FindReferencesTool.cs:43-45` |
| 2 — `GetImpactTool.cs` beide Branches | ✓ | `GetImpactTool.cs:58-60` (Symbol) + `:74-76` (Git) |
| 3 — `SymbolGraphToolRegistrations` Delegate + Description | ✓ | `SymbolGraphToolRegistrations.cs:38-49` (find_references) + `:51-63` (get_impact) |
| 4 — Fixture-Erweiterung `Caller.cs`/`CalculatorCaller.cs` | ✓ | `Caller.cs:11-21` (RunTwice/RunThrice) + `CalculatorCaller.cs:11-21` |
| 5 — Plan-Abweichung 1/2/3 (Footprint-Notbremse) | ✓ | Nicht ausgelöst — begründet, +5/+4 statt +10-12/+6-10 (siehe `result.md` Z. 269-273) |
| 6 — `FindReferencesToolTests` Trunkierungs-Test | ✓ | `FindReferencesToolTests.cs:97-113` |
| 7 — `GetImpactToolTests` Symbol + Git Trunkierung | ✓ | `GetImpactToolTests.cs:97-114` + `:116-135` |
| 8 — E2E-Tests neue Dateien | ✓ | `McpServerCommandFindReferencesTests.cs` (42 Z.) + `McpServerCommandGetImpactTests.cs` (67 Z.) |
| 9 — Build/Tests/Footprint-Messung | ✓ | siehe Selbst-Verifikation oben |
| 10 — Dogfooding | ✓ | `result.md` Z. 292-344 (3 Calls gegen `AiNetLinter.slnx`) |
| 11 — Commit + gezielter `git add` | ✓ | Commit `3eb13bf` (`feat(mcp): ...`), 9 files, +244/−23, kein Push |

**Modifizierte Tests mit `maxResults: 50`-Argument:**
- `FindReferencesToolTests.cs:19` (NoSolutionLoaded) — Argument ergänzt ✓
- `FindReferencesToolTests.cs:90` (ValidQualifiedName) — Argument ergänzt ✓
- `GetImpactToolTests.cs:19, 33, 47, 61, 76, 90` (alle 6 bestehenden Tests) — Argument ergänzt ✓

**Keine Änderung an `McpServerCommandTests.cs`:** `git show 3eb13bf -- src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` liefert keine Ausgabe (Datei im Commit nicht enthalten) ✓.

## Trunkierungs-Korrektheit

**Meta-Zeile exakt wie Konzept:**
- Konzept Z. 232: `"[342 Treffer gesamt, 50 gezeigt — Pattern verfeinern oder maxResults erhöhen]"`
- `McpTruncation.cs:40`: `$"[{totalMatches} Treffer gesamt, {maxResults} gezeigt — Pattern verfeinern oder maxResults erhöhen]"`
- **Formattreue 1:1.** Die Interpunktion (Komma, „—"-Em-Dash, Punkt am Ende fehlt bewusst — Konzept hat auch keinen) ist identisch.

**Boundary-Bedingungen (3 Branches × 4 Boundaries = 12 Fälle):**

| Boundary | `find_references` | `get_impact` Symbol | `get_impact` Git |
|---|---|---|---|
| `maxResults: 0` → `normalizedMaxResults = 1` | `FindReferencesTool.cs:43` ✓ | `GetImpactTool.cs:58` ✓ | `GetImpactTool.cs:74` ✓ |
| `maxResults: 1` (1 Treffer, keine Trunkierung) | via `TruncateLines` (Z. 34: `if (totalMatches <= maxResults)`) ✓ | identisch ✓ | identisch ✓ |
| `maxResults: 50` (Default) | Default im Delegate `SymbolGraphToolRegistrations.cs:39` ✓ | Default im Delegate `:52` ✓ | Default im Delegate `:52` ✓ |
| Trunkierung ausgelöst (Test `maxResults: 2` bei ≥3 Call-Sites) | Test 1 grün + A3 ✓ | Test 2 grün + A3 ✓ | Test 3 grün + A3 ✓ |

**`callSites.Count` als `totalMatches`-Argument:** für beide Branches
verwendet (`FindReferencesTool.cs:45`, `GetImpactTool.cs:60, 76`).
`DiffImpactAnalyzer.FindCallSitesAsync` liefert vollständige Liste (kein
pre-truncation), `AnalyzeAsync` liefert `Task<List<string>>` (implizit
`IReadOnlyList<string>`, passt 1:1 auf `TruncateLines` Signatur).
→ **Plan-Frage A sauber geklärt** (siehe `result.md` Z. 273).

## Rules-Konformität

| Regel | Limit | Geprüfte Klassen | Status |
|---|---|---|---|
| `EnforceNullableEnable` | `#nullable enable` | `McpServerCommandFindReferencesTests.cs:1` ✓ + `McpServerCommandGetImpactTests.cs:1` ✓ | ✓ |
| `EnforceSealedClasses` | `sealed` | `McpServerCommandFindReferencesTests:20` (`public sealed class`) ✓ + `McpServerCommandGetImpactTests:21` ✓ | ✓ |
| `MaxLineCount` (Prod) | 500 | 42 + 67 Z. (deutlich unter) | ✓ |
| `MaxLineCount` (Prod) | 500 (alt: 500/500 voll) | `McpServerCommandTests.cs` = 426 Z. (74 Puffer, NICHT 499 wie in 003 dokumentiert) | ✓ nicht angetastet |
| `MaxMethodLineCount` (Tests) | 100 (Override) | Längste Methode: `RunAsync_..._GetImpactGitBranchWithMaxResultsTruncates` ~20 Z. | ✓ |
| `MaxMethodParameterCount` (Prod) | 4 | siehe Tabelle unten | ✓ |
| `MaxAIContextFootprint` | 2500 / 2700 (PathOverride) | 2494 / 2495 / 2522 (alle unter Limit) | ✓ |
| `EnforcePascalCase` | PascalCase | alle Klassen/Methoden ✓ | ✓ |

**`MaxMethodParameterCount`-Prüfung (explizite User-Parameter ohne `ct`):**

| Methode | User-Parameter | ct | Effektiv | Status |
|---|---:|---:|---|---|
| `FindReferencesTool.ExecuteAsync` | 3 (`state`, `symbolIdentifier`, `maxResults`) | 1 | 3 | ✓ |
| `GetImpactTool.ExecuteAsync` | 4 (`state`, `gitRef`, `symbolIdentifier`, `maxResults`) | 1 | 4 | ✓ am Limit (Plan erlaubt, A8-konform) |
| `GetImpactTool.ExecuteSymbolBranchAsync` | 3 (`solution`, `symbolIdentifier`, `maxResults`) | 1 | 3 | ✓ |
| `GetImpactTool.ExecuteGitRefBranchAsync` | 3 (`solution`, `gitRef`, `maxResults`) | 0 | 3 | ✓ |
| `ResolveSymbolAsync` | 2 (`solution`, `identifier`) | 1 | 2 | ✓ (unverändert) |
| `ResolveByPositionAsync` | 5 (`solution`, `identifier`, `path`, `line`, `column`) | 1 | 5 | ✓ `private` → `MaxMethodParameterCountForNonPublic: 6` |
| `ResolveByNameAsync` | 2 (`solution`, `identifier`) | 1 | 2 | ✓ (unverändert) |

**Zur Pre-Build-Check-Interpretation:** der Coder hat richtig erkannt,
dass `MaxMethodParameterCount: 4` nur die **echten User-Parameter** zählt
(nicht den `CancellationToken`). `GetImpactTool.ExecuteAsync` mit
4 User-Parametern + `ct` ist am Limit, nicht darüber. Plan-konform
(siehe `plan.md` Z. 1314-1318 — gleiche Logik).

## Logische Korrektheit

**A3-Nachweis für die 3 Unit-Tests (alle dokumentiert + verifiziert):**

1. `FindReferencesToolTests:98` — wortwörtlich `"Not found: 'Treffer gesamt'"` nach `string.Join`-Ersatz ✓
2. `GetImpactToolTests:98` (Symbol-Branch) — wortwörtlich `"Not found: 'Treffer gesamt'"` nach `string.Join`-Ersatz ✓
3. `GetImpactToolTests:117` (Git-Branch) — wortwörtlich `"Not found: 'Treffer gesamt'"` nach `string.Join`-Ersatz ✓

**A3-Bewertung E2E-Tests (3 Stück, implizit):** der Coder hat E2E
**nicht** aktiv rot-getestet. **Bewertung: akzeptabel.** Begründung:

- A3 verlangt Nachweis, dass ein **neuer** Test scheitert, wenn die
  Änderung weggenommen wird. Bei E2E-Subprozess-Tests müsste man
  `McpTruncation.TruncateLines` im Subprozess-Server dekativieren, was
  nur durch eine separate "Kill-Switch"-Konfiguration ginge — Aufwand
  ~5-10 Min Subprozess-Restart pro Test, ohne klaren Erkenntnisgewinn
  gegenüber dem analogen Unit-Test-A3.
- 002-Plan (`units/002/plan.md`) hat explizit A3-implizit für
  Subprozess-Tests als Methode etabliert (vom Planer festgelegt).
  004 hat das genauso gehandhabt (siehe 004-Review MINOR-Beobachtungen).
- Die 3 Unit-Tests haben A3-Nachweis → die Trunkierung **als Mechanismus**
  ist nachweislich wirksam. E2E-Tests verifizieren nur, dass der Mechanismus
  **durch den MCP-Subprozess** korrekt propagiert (Delegate-→ Tool-Aufruf).

**Symbol-Branch-Delegation (Plan-Abweichung 1) — nicht genommen:**

Korrekte Entscheidung. Plan-Bedingung war "> 2500 Z. Footprint".
`GetImpactTool` ist bei 2495 Z. (5 Z. Puffer) geblieben — der
Mehr-Aufwand der Delegation (Architektur-Komplexität, 2 statt 1
Aufrufpfad) wäre nicht gerechtfertigt. Code bleibt lesbar:
`ExecuteSymbolBranchAsync` ist 17 Z. lang, fast nur `ResolveSymbolAsync` +
Trunkierung. **Keine unnötige Komplexität.**

**Tests echt (nicht "assert-true" oder Reimplementation):**

- Test 1 (`FindReferencesToolTests:97-113`): prüft 3 distinkte Substrings
  im Output (`"Treffer gesamt"`, `"2 gezeigt"`, `"Pattern verfeinern..."`).
  A3 belegt, dass ohne `McpTruncation.TruncateLines` keiner der drei
  matcht. **Echter Test.**
- Test 2 + 3 (Symbol/Git): identische Struktur. **Echter Test.**

## Konzept-Treue

**Konzept Z. 215-225 (Trunkierung + `maxResults`):**
- `maxResults` als optionaler Parameter mit Default 50 ✓
- An allen vier Listen-Tools: search_pattern 002 ✓, find_symbol 004 ✓,
  find_references 005 ✓, get_impact 005 ✓
- Einheitliche Meta-Zeile (Plain-Text, eine Form) ✓
- `McpTruncation`-Helper als sibling zu `McpToolResults` ✓
- DoD-Kriterium "unter konfigurierter Zeilengrenze" durch `maxResults` ✓

**Konzept Z. 226-233 (Plain-Text-Format):**
- Alle Outputs bleiben über `McpToolResults.Text` (Plain-Text) ✓
- Kein Wechsel zu JSON für Trunkierungs-Metadaten ✓
- Meta-Zeile exakt wie spezifiziert ✓

**Konzept Z. 631-634 (DoD alle Listen-Tools trunkiert):**
- **4 von 4 Listen-Tools** trunkiert ✓
- `get_type_hierarchy` ist korrekt als Nicht-Listen-Tool erkannt
  (Vererbungs-Hierarchie, max. ~10 Zeilen) — keine Trunkierung nötig ✓

## Findings

Keine CRITICAL, keine MAJOR.

### MINOR

**MINOR-1 — Pre-existing: `FindReferencesToolTests.cs` + `GetImpactToolTests.cs` ohne `#nullable enable`** (`src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs:1` + `GetImpactToolTests.cs:1`)

Diese Dateien haben kein `#nullable enable` am Dateianfang — im Gegensatz
zu `McpServerCommandFindReferencesTests.cs:1` und `McpServerCommandGetImpactTests.cs:1`
(den neuen E2E-Dateien). Pre-existing-Issue aus 003 (siehe 003-Review MINOR 2,
`state.md` Z. 269-270), im 005-Plan explizit ausgeschlossen (Z. 506-514,
A5: "kein Eingriff in Dateien, die nicht sowieso berührt werden").
**Plan-konform**, kein 005-Issue. Sollte beim nächsten Anlass, der
diese Dateien ohnehin anfasst (z. B. 006 oder ein Refactor-Block), inline
nachgezogen werden.

**MINOR-2 — `McpServerCommandTests.cs` ist 426/500 Z., nicht 499/500 wie in 003 dokumentiert**

`Get-Content | Measure-Object -Line` ergibt 426 Z. (74 Z. Puffer). Der
005-Plan Z. 305-311 + Z. 1323-1327 hat den alten 003-Stand übernommen
und daher vorsichtshalber eigene Test-Dateien für die E2E-Tests angelegt.
Das ist die **konservativere** und **korrekte** Entscheidung — die
neuen Tests landen in dedizierten Dateien (analog 004). **Kein
funktionaler Befund**, nur eine Beobachtung zum Plan-Stand, der für 006+
korrigiert werden könnte.

## Tech-Debt-Beobachtungen

**TD-011 sollte aktualisiert werden** (kein neuer Eintrag — TD-011 ist
bereits offen und genau das richtige Vehikel):

- **Aktuell in `tech-debt.md` Z. 40** (Stand `28e6e58`): "2487/2500 (13 Z. Puffer)"
- **Tatsächlicher Stand nach 005** (gemessen, verifiziert): "2494/2500 (6 Z. Puffer, Stand `3eb13bf`)"

Verschärfung der Vorhersage: das nächste Symbolgraph-Tool, das in
`SymbolGraphToolRegistrations` registriert wird (z. B. `get_symbol_body`
aus P2-Backlog), wird die 5. Registrar-Klasse **zwingend** nötig machen
(6 Z. Puffer reichen für eine `tools.Add(...)`-Zeile + Description —
knapp, riskant).

**TD-010** (`SearchPatternTool` 2482/2500) — unverändert, nicht in 005
angetastet. Bleibt offen.

**TD-004** (Generelles Registrar-Footprint-Druck) — durch TD-011-
Verschärfung indirekt bestätigt. Kein separater Eintrag nötig (gleicher
Befund).

**Neuer TD-Eintrag nicht erforderlich.** Die Beobachtung
"`SymbolGraphToolRegistrations`-Puffer 6 Z. nach 005" ist exakt der
TD-011-Scope; eine Verdopplung wäre Drift.

## Sonstige Beobachtungen (informativ)

- **Commit-Format** `3eb13bf` (`feat(mcp): find_references + get_impact
  trunkierung (P0/P1) [codegraph-mcp-server]`) — Conventional Commits,
  deutscher Stil, mit [codegraph-mcp-server]-Suffix konsistent zu
  001-004. ✓
- **A5 / A4-Konformität:** ein Commit, gezielter `git add` (9 files),
  kein Push, kein History-Rewrite, keine nachträgliche Schönheits-
  Korrektur. ✓
- **Working-Tree nach Commit:** clean außer untracked `.todos/`-Helfer
  und `coder-todo.md` (lokal, nicht für Commit vorgesehen, A4). ✓
- **Cache-Existenz nach `dotnet test`:** strukturell (siehe
  `state.md` Z. 50-55), kein Step-Regress. ✓
- **`Caller.Run` vs. `RunTwice`/`RunThrice`:** der Coder hat im
  `result.md` Z. 284 explizit verifiziert, dass die SymbolFinder-Suche
  strikt auf `name == lastSegment` filtert (siehe
  `FindReferencesTool.cs:91`). `RunTwice`/`RunThrice` werden bei
  Suche nach "Run" **nicht** erfasst → bestehender
  `ResolveSymbolAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbolError`-
  Test bleibt korrekt (2 Matches: `Caller.Run` + `OtherCaller.Run`).
  **Saubere Risiko-Analyse im Voraus.**

## Plan-Erfüllung — klare Aussage

**Alle 11 Schritte des Plans vollständig umgesetzt.** Keine
Plan-Abweichungen ausgelöst (alle drei Notbremsen-Bedingungen nicht
eingetreten — Footprint-Wachstum niedriger als geschätzt). Harte
Scope-Grenze eingehalten: keine Scanner-Splits, keine
`McpServerOptionsFactory`-Änderung, keine `PathOverrides`-Änderung,
keine Doku-Änderung, keine `konzept.md`/`tech-debt.md`-Änderung (A7).

**Konzept-P0/P1-Trunkierung in `find_references` und `get_impact`
ist live verifiziert** (3 Dogfooding-Calls gegen `AiNetLinter.slnx` mit
echten Trunkierungs-Ausgaben). DoD-Kriterium Z. 631-634 vollständig
erfüllt — alle vier Listen-Tools trunkieren.

---

## Verdict: approved

**Anzahl Findings nach Severity:** CRITICAL=0, MAJOR=0, MINOR=2

**Selbst-Verifikation:** Re-Run gemacht (Build, Footprint für 3 Klassen,
3 Test-Filter-Läufe, Self-Lint, Volllauf 1114/1114). Alle Maße stimmen
mit `result.md` überein.

**Nächste Aktion des Orchestrators:**
005 ist fertig. P0/P1-Trunkierung in allen 4 Listen-Tools erfüllt
(`search_pattern` 002, `find_symbol` 004, `find_references` + `get_impact`
005). Nächste Kandidaten: EPIC-06 (Robustheit), EPIC-07 (Tests),
EPIC-08 (Doku), dann die restlichen P0/P1-Erweiterungen (Kaltstart,
Auto-Discovery, Staleness-Sweep, Call-Log, `RefreshStaleDocuments`-
Verzeichnis-Sweep, `ILintConsole` für MCP). Planer entscheidet JIT.

**Empfehlung für Tech-Debt-Pflege:** TD-011 in `tech-debt.md` von
"2487/2500 (13 Z. Puffer, Stand `28e6e58`)" auf
"2494/2500 (6 Z. Puffer, Stand `3eb13bf`)" aktualisieren
(Index-Zeile Z. 40 + Body Z. 119). Reine Stand-Korrektur, keine
inhaltliche Neubewertung.
