---
unit: 002
fix_round: 01
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-01
code_commit: bd9e6fd
plan_commit: 517bebe
trigger_review: units/002/review.md (Verdict: issues, 1 MAJOR M-1)
verdict: approved
---

# Review Einheit 002/fix-01 — M-1: `McpToolResults.InvalidArgument`-Helper liefert irreführenden Hint für `search_pattern`

**Verdict: approved** — M-1 sauber behoben, Test 8 scharf genug, A3-Nachweis
plausibel und mit wortwörtlichem Failure-Output dokumentiert, Build/Test
1097/1097 grün, keine Nebenwirkungen in `McpToolResults.cs` oder anderen
Dateien, Conventional-Commit-Format eingehalten, gezielter `git add`
(nicht `-A`/`.`), kein Push, kein History-Rewrite.

## Selbst-Verifikation

**Plausibilitätsbewertung** auf Basis `result.md` (Commit `b1a08a3`),
`plan.md` (Commit `517bebe`), `units/002/review.md` (Commit `f9bbeb5`,
M-1-Befund Z. 148-196) und direkter Code-Inspektion der beiden geänderten
Dateien gegen Commit `bd9e6fd`. **Kein Re-Run** durchgeführt.

Begründung: der Coder hat den A3-Nachweis selbst mit wortwörtlichem
Failure-Output dokumentiert (`result.md` Z. 144-161: `Not found: "Pattern
angeben"` + String-Auszug `[ERROR]: INVALID_ARGUMENT: pattern darf nicht
leer···`). Diese Diagnose ist **exakt** die im Plan Z. 244-247 erwartete
(`Assert.Contains("Pattern angeben", …)` als primäre A3-Detektion, weil
sie zuerst läuft). Bei genauem Lesen bestätigt sich:

- der `[ERROR]: INVALID_ARGUMENT:`-Prefix passt zum Helper-Pfad von
  `McpToolResults.InvalidArgument` (`McpToolResults.cs:74-80`),
- das fehlende "Pattern angeben" passt zur ursprünglichen Hint-
  Hartkodierung "Entweder gitRef ODER symbolIdentifier angeben, nie
  beide." (`McpToolResults.cs:79`) — dieses Wort kommt dort **nicht**
  vor,
- die `···` im String-Auszug sind ein PowerShell-cp1252-Encoding-
  Artefakt (`result.md` Z. 232-242 dokumentiert das ausdrücklich), kein
  semantischer Verlust.

Da der Plan-Workflow explizit vorsieht, dass gezielte Re-Runs nur bei
konkretem Verdacht gemacht werden und ich keinen Verdacht habe, der
nicht durch das `result.md` entkräftet wird, lasse ich es bei der
Plausibilitätsbewertung. (Zur Vergleichsmethodik: dasselbe Vorgehen wie
im 002-Review, dort auch Plausibilität mit gezielten Re-Runs auf
`SearchPattern`-Filter und Footprint-Stichproben, nie den vollen Lauf.)

## Konzept-Konformität (Vor-der-Kritik-Check)

- Konzept Z. 567-568 (Fehlerfälle liefern strukturierte Fehlerantwort
  im bestehenden `[ERROR]`-Format): **jetzt erfüllt** für den
  EmptyPattern-Pfad. Der neue Aufruf
  (`SearchPatternTool.cs:40-43`) nutzt `McpToolResults.Error(
  LinterErrorCodes.InvalidArgument, "pattern darf nicht leer sein.",
  hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.")`, was
  via `McpToolResults.Error` → `LinterErrorFormatter.Format` exakt
  das geforderte `[ERROR]: INVALID_ARGUMENT: <message>\n  hint:
  <hint>`-Format liefert (`McpToolResults.cs:21-29`).
- Konzept-Treue ansonsten unverändert: keine Edits an `konzept.md`,
  keine Edits an `Docs/**`, keine Edits an `rules.json`, keine Edits
  an `state.md`, keine Edits an `tech-debt.md` (A7).

## Findings sortiert nach Ebene

### Ebene 1 — Plan-Erfüllung

Alle 7 Plan-Punkte aus `plan.md` umgesetzt:

| Plan-Punkt | Datei:Zeile | Beleg | Status |
|---|---|---|---|
| 1. Code-Fix in `SearchPatternTool.cs:38-44` | `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs:38-44` | `McpToolResults.Error(LinterErrorCodes.InvalidArgument, "pattern darf nicht leer sein.", hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.")` — wortwörtlich wie Plan Z. 85-95, gleiche Argument-Reihenfolge, gleiche `hint`-Position als benannter Parameter, gleicher `LinterErrorCodes.InvalidArgument`-Code | ✓ |
| 2. Test 8 um 3 Assertions erweitert | `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs:179-181` | `Assert.Contains("Pattern angeben", …)`, `Assert.DoesNotContain("gitRef", …)`, `Assert.DoesNotContain("symbolIdentifier", …)` — alle drei mit `StringComparison.Ordinal` | ✓ |
| 3. Kommentar über den Assertions (Hint-Wortlaut-Kopplung Code↔Test) | `SearchPatternToolTests.cs:174-178` | 5-zeiliger Block: nennt M-1-Regression-Schutz, dokumentiert die `get_impact`-Hartkodierung als Regressionsform, weist auf die Wortlaut-Kopplung hin | ✓ (exakt wie Plan Z. 173-177) |
| 4. A3-Methodik durchgeführt (alle 6 Schritte) | `result.md` Z. 93-216 | Erstlauf grün (Z. 106-124) → A3-Auslöser (Z. 126-141) → A3-Lauf rot (Z. 143-161, wortwörtlich: `Not found: "Pattern angeben"`) → A3-Rückgängig (Z. 178-198) → Volllauf 1097/1097 grün (Z. 200-203) → Build 0 Warnungen/0 Fehler (Z. 49-61) | ✓ |
| 5. Conventional-Commit-Format | Commit `bd9e6fd` | `fix(mcp): search_pattern leerer-pattern-Hint [codegraph-mcp-server]` — Conventional-Format `fix(mcp):`, deutscher Imperativ, Task-Suffix analog 002-Result (Z. 220-224) | ✓ |
| 6. Gezielter `git add` (nicht `-A`/`.`) | `git show --name-status bd9e6fd` | Nur die 2 explizit benannten Dateien: `M  src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs`, `M  src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` — keine weiteren Edits | ✓ (A4) |
| 7. Kein Push, kein History-Rewrite | `git status` + `git log origin/main..HEAD` | Working tree clean, Branch 12 commits ahead of `origin/main` (lokal), keine Force-Push-/Rebase-/Amend-Operationen sichtbar | ✓ (A4) |

**Zusatz-Check — Datei-Scope:** `git show --stat bd9e6fd^` zeigt **exakt**
die 2 im Plan benannten Dateien + die `result.md` (die im selben Loop-
Commit vom Orchestrator committed wird, nicht im Code-Commit). Keine
Seiteneffekte auf `McpToolResults.cs` (`git log --oneline -1 -- src/
AiNetLinter/Mcp/McpToolResults.cs` zeigt letzten Commit `c125511 feat
(mcp): add get_file_skeleton tool`, also vor dem M-1-Fix — Helper
intakt für `get_impact`, dokumentierter Zweck erhalten, **kein** un-
gewollter Refactor).

### Ebene 2 — Rules-Konformität (minimal-invasiv, nur die 2 geänderten Dateien)

| Regel | Datei:Zeile | Status |
|---|---|---|
| `EnforceNullableEnable` | `SearchPatternTool.cs:1` (`#nullable enable`) | ✓ unverändert |
| `EnforceNullableEnable` | `SearchPatternToolTests.cs:1` (Test-Datei — **kein** `#nullable enable`, im 002-Review M-1 dokumentiert) | ✓ **außerhalb fix-01-Scope** — der Test-Header wurde nicht angefasst, der Befund ist eine vorbestehende Beobachtung aus Einheit 002, gehört zu O-1 dort, nicht zu M-1 |
| `MaxLineCount` ≤ 500 | `SearchPatternTool.cs` 67 Z. (Puffer 433), `SearchPatternToolTests.cs` 182 Z. (Puffer 318) | ✓ |
| `MaxMethodLineCount` ≤ 60 (Prod) | `ExecuteAsync` jetzt ~35 Z. (vorher ~30, +5 durch den mehrzeiligen `Error`-Aufruf) | ✓ (Puffer ~25 Z.) |
| `EnforceSealedClasses` | `SearchPatternTool` `internal static` (statisch, nicht relevant), `SearchPatternToolTests` `public sealed class` (`tests.cs:14` unverändert) | ✓ |
| `MaxMethodParameterCount` ≤ 4 | `ExecuteAsync` weiterhin 4 Parameter (state, pattern, isRegex, maxResults, ct — `CancellationToken` via `MethodParameterCountIgnoreTypeNames` ignoriert) | ✓ |
| `MaxCyclomaticComplexity` ≤ 12 / `MaxCognitiveComplexity` ≤ 15 | `ExecuteAsync` weiterhin flach (5+1 in einer `if`/`try`/`catch`/`if`/`return`-Folge) | ✓ |
| `EnforceNamespaceDirectoryMapping` | `SearchPatternTool.cs:9` → `AiNetLinter.Mcp.Tools` ✓, `SearchPatternToolTests.cs:11` → `AiNetLinter.Tests.Mcp.Tools` ✓ | ✓ |
| `AiNetLinterRichtlinien.mdc` §1 (Einfachheit vor Abstraktion) | Fix ist die **kleinstmögliche** Änderung: 1 Zeile Aufruf-Ersatz + 3 Assertions in einem Test. Kein Helper-Refactor, keine API-Erweiterung, keine `McpToolResults.InvalidArgument`-Umbenennung (A5, Scope sauber). | ✓ |
| `AiNetLinterRichtlinien.mdc` §5 (Result-Pattern, kein `throw`) | Fix **erhält** das Result-Pattern, ersetzt nur den irreführenden Helper durch den korrekten. Kein rethrow, keine Exception-Propagierung. | ✓ |
| `AiNetLinterRichtlinien.mdc` §5 (Zero-Warning-Direktive) | Build 0 Warnungen, 0 Fehler (`result.md` Z. 49-61), `TreatWarningsAsErrors=true` würde sonst abbrechen | ✓ |

### Ebene 3 — Logische Korrektheit

| Aspekt | Beleg | Bewertung |
|---|---|---|
| Bug-Elimination (Kernfrage): Test 8 grün mit neuem Code | `result.md` Z. 64-70 (Erstlauf), Z. 192-196 (A3-Rückgängig-Lauf) | ✓ — beide male `1/1 grün` |
| Bug-Elimination (Kernfrage): Test 8 rot mit altem Code, **wortwörtlich** Failure an der richtigen Stelle | `result.md` Z. 144-161: `Assert.Contains() Failure: Sub-string not found` + `String: "[ERROR]: INVALID_ARGUMENT: pattern darf nicht leer···"` + `Not found: "Pattern angeben"` | ✓ — exakt der im Plan Z. 244-247 erwartete Output |
| Hint-Wortlaut `Pattern angeben` trifft tatsächlich den alten `InvalidArgument`-Hint | `McpToolResults.cs:79` enthält `"Entweder gitRef ODER symbolIdentifier angeben, nie beide."` — das Wort "Pattern angeben" kommt dort **nicht** vor (auch nicht "Pattern") | ✓ — A3 funktioniert |
| Defensiv-Assertions scharf, nicht trivial | `Assert.DoesNotContain("gitRef", …)` + `Assert.DoesNotContain("symbolIdentifier", …)` würden bei der alten `InvalidArgument`-Nutzung ebenfalls rot werden (beide Strings kommen im alten Hint vor). Sie sind Doppel-Absicherung für den Fall, dass der Hint-Wortlaut im Tool später abgeschwächt wird (z. B. nur "Pattern" statt "Pattern angeben") — dann fängt das `DoesNotContain` weiterhin die `get_impact`-Regression. | ✓ sinnvoll scharf, nicht überzogen (Plan Z. 195-201 hat das explizit so vorgesehen) |
| Neuer Aufruf semantisch identisch mit Z. 57-60 (Regex-Pfad) | beide nutzen `McpToolResults.Error(LinterErrorCodes.InvalidArgument, <msg>, hint: <hint>)` mit gleicher Argument-Reihenfolge, gleichem Code, gleicher `hint`-Position als benannter Parameter | ✓ |
| Hint-Wortlaut `Pattern angeben — leeres Pattern ist nicht erlaubt.` ist `search_pattern`-spezifisch, kein `get_impact`-Bezug | string enthält weder "gitRef" noch "symbolIdentifier" noch "get_impact" | ✓ |

### Ebene 4 — Konzept-Treue

- Konzept Z. 567-568: **jetzt erfüllt** für den EmptyPattern-Pfad (siehe oben).
- Scope sauber: **keine** Edits an `McpToolResults.InvalidArgument` (Helper bleibt
  für `get_impact`, dokumentierter Zweck in `McpToolResults.cs:71-73`), **keine**
  Edits an `SearchPatternScanner.cs`/`AnalysisToolRegistrations.cs`/
  `McpTruncation.cs`/`McpServerCommandTests.cs`/`McpCodeGraphServer.cs`
  (`git show --stat bd9e6fd^` zeigt **nur** die 2 explizit benannten Dateien
  im Code-Commit).
- **Keine** der 6 MINOR-Beobachtungen aus `units/002/review.md` (O-1 bis O-6)
  wurden angefasst (A5, Scope-Treue).
- **Keine** Edits an `tech-debt.md`/`konzept.md`/`state.md`/`Docs/**`/
  `rules.json`/`README.md` (A7, EPIC-08 nicht in 002-Scope).

## Findings — Detail

Keine CRITICAL, keine MAJOR. Keine Pflicht-MINOR (kein echter Befund, der
eine Beobachtung rechtfertigt — der Fix ist sauber, der Test ist scharf,
die Wortlaut-Wahl ist konsistent mit dem etablierten Linter-Output-Format).

## Sonstige Beobachtungen (MINOR)

- **B-1 — `Assert.Contains("Pattern angeben", …)` als primärer A3-Detektor
  deckt nur den aktuellen Hint-Wortlaut ab** —
  `SearchPatternToolTests.cs:179`: bei zukünftiger Änderung des Hint-
  Wortlauts (z. B. "Pattern fehlt" statt "Pattern angeben") wird der
  Test rot, obwohl der Bug-Fix selbst korrekt wäre. Die
  Defensiv-Assertions (`DoesNotContain("gitRef")` /
  `DoesNotContain("symbolIdentifier")`) fangen die `get_impact`-
  Regression weiterhin, würden aber **nicht** andere irreführende
  Hint-Formulierungen erkennen. Der Kommentar Z. 174-178 dokumentiert
  diese Kopplung explizit für künftige Editierer — **MINOR**, nicht
  in `fix-01/-Scope`, eher Tech-Debt-Kandidat: "bei Hint-Wortlaut-
  Reformulierung `search_pattern`/`get_impact`/`find_references` die
  Wortlaut-Kopplungs-Asserts gemeinsam prüfen".

- **B-2 — `McpToolResults.InvalidArgument` als `get_impact`-Helper ist
  strukturell ein latenter Fußangst-Faktor** — `McpToolResults.cs:74-80`:
  der hartkodierte Hint `"Entweder gitRef ODER symbolIdentifier
  angeben, nie beide."` ist nur für `get_impact` korrekt; jedes andere
  Tool, das diesen Helper in Zukunft für "beliebige ungültige
  Argumente" verwendet, erbt den irreführenden Hint. Der M-1-Befund
  ist die direkte Folge dieser Helper-API. Strukturelle Lösung
  (Helper-Umbenennung zu `InvalidArgumentExclusiveParams` oder
  Hint-Parametrisierung) wäre die saubere Variante, **aber** explizit
  außerhalb `fix-01/-Scope` (Plan Z. 41-45 hat das ausdrücklich
  ausgeschlossen, A5). Tech-Debt-Kandidat **TD-012** (mittel,
  Folge-Einheit) — wird hier nur erwähnt, nicht in `tech-debt.md`
  editiert (A7, A2 — Nutzer entscheidet).

## Bezug zu Projektregeln (Verhaltensregeln dieses Reviews)

- **A2 (Wer prüft, fixt nicht):** eingehalten — kein Code geändert, kein
  Vorschlag in `issues`, der zu Eigenmächtigkeit verleiten würde.
- **A3 (Tests müssen fehlschlagen können):** vom Coder mit wortwörtlichem
  Failure-Output dokumentiert; Plausibilität von mir bestätigt (siehe
  Ebene 3 oben). Die `Assert.Contains("Pattern angeben", …)`-Assertion
  ist der primäre Bug-Detektor, die `DoesNotContain`-Assertions sind
  Defensiv-Absicherung.
- **A4 (Nichts Unwiederbringliches):** eingehalten — gezielter `git add`
  auf 2 Dateien, kein Push, kein History-Rewrite.
- **A5 (Fertig ist fertig):** eingehalten — keine Edits an
  `McpToolResults.InvalidArgument`, keine Edits an den 6 MINOR-
  Beobachtungen aus 002, keine Edits an `tech-debt.md`/`konzept.md`.
- **A6 (Im Zweifel fragen):** nicht relevant — kein Widerspruch
  aufgedeckt, keine Mehrdeutigkeit.
- **A7 (Eingaben sind Eingaben):** eingehalten — `konzept.md` und
  Projektregeln nicht angefasst, nur gelesen.
- **A8 (Kernel und Rollen sind unantastbar):** nicht relevant — diese
  Review-Datei ist eine `units/.../review.md`, kein Kernel-/Rollen-
  Artefakt.

## Zähler-Update (für Orchestrator-Übernahme)

- `max_aufrufe`: 8 (5 Stand 002-Ende + 3 für fix-01 Planer + Coder +
  Kritiker) → **8/40** (von Coder dokumentiert in
  `result.md` Z. 256-260 nicht ganz konsistent, aber Größenordnung
  korrekt).
- `max_fix_pro_einheit` für 002: jetzt **1/3** (2 verbleibend für 002).
- `max_fix_gesamt`: jetzt **1/12** (11 verbleibend für den Task).

## Nächste Aktion des Orchestrators

**002 ist abgeschlossen** (approved nach 1 Fix-Runde). Nächste Einheit
aus `konzept.md` planen. Aus `konzept.md` und dem Plan 002 sind die
**wahrscheinlichsten Kandidaten für 003**:

1. **EPIC-05 Miss-Hint in `find_symbol` via `GetFilesWithHits`-API** —
   die in 002 exportierte `SearchPatternScanner.GetFilesWithHits`-
   Schnittstelle (`SearchPatternScanner.cs:88-112`) ist genau die
   importierbare API für das Miss-Hint-Feature (Konzept Z. 604-606
   nennt sie explizit als DoD-Voraussetzung). Konsistenz mit 002 ist
   hoch.
2. **EPIC-05 Trunkierungs-Einbau in `find_symbol`** — analog zu
   `search_pattern` in 002 (Konzept Z. 215-225 fordert Trunkierung
   für alle Listen-Tools, in 002 nur für `search_pattern`
   umgesetzt). Die `McpTruncation.cs:40`-Meta-Zeile ist wiederverwendbar.

**Konkret:** Planer-Aufruf für `tasks/codegraph-mcp-server/units/003/`
mit Wahl zwischen (1) und (2) — der Planer entscheidet JIT, ggf. mit
Nutzer-Abstimmung (A6) bei Mehrdeutigkeit.

## Anhang — wortwörtlicher Code-Stand nach Fix

**`src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs:38-44`:**
```csharp
if (string.IsNullOrEmpty(pattern))
{
    return McpToolResults.Error(
        LinterErrorCodes.InvalidArgument,
        "pattern darf nicht leer sein.",
        hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.");
}
```

**`src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs:171-181`:**
```csharp
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
```

Beide Code-Blöcke wortwörtlich wie in Commit `bd9e6fd`.
