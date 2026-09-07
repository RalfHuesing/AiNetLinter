# Finding: `safeguard`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

## 1 Schema-Kurzfazit

Beschreibung steuert den **Default-Call richtig**: Quality-Gate 0–10, Pass/Fail gegen `minScore` (Default 8,0), Top-Violations plus Remediation, `targetType=project` + absoluter `targetPath`. `scopeFilter` und Aliase `scope`/`path` (Projektname oder Pfad-Substring), `maxViolations` (Default 20) stehen in der Prosa und werden angenommen.

JSON-Schema schwächer als die Beschreibung: `targetType` ohne Enum; `minScore`/`maxViolations` ohne min/max; Property-Beschreibungen fehlen, Aliase nur in der Tool-Prosa. Assembly laut Beschreibung **ausdrücklich unsupported** — live wird zuerst Dateiexistenz geprüft (`INVALID_ARGUMENT` auf fehlende/kein-`.dll`-Pfad), der dokumentierte Code `ASSEMBLY_TARGET_UNSUPPORTED` war in diesem Lauf **nicht erreichbar** (keine Workspace-DLL, externes Ziel vom Auto-Review blockiert).

Die Beschreibung **führt beim Scope in die Irre**: sie verspricht Eingrenzung, die Antwort behauptet Completeness „für den angefragten Scope“, die **Top-Befunde sind aber lösungweit**.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`. Alle ausgeführten Calls **schnell** (kein Timeout). Antworten kompakt (~0,5–1,2k Zeichen), außer Fehlerzeilen.

| # | Absicht | Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Happy Path | `project` + Root, Defaults | ~1k Zeichen, **nicht** trunkiert | Score **8,88/10**, Threshold 8,00, **PASS**. 1 Verstoß, 2481 Klassen. Top: `SetupText` / `MaxPublicMembersPerType` / Setup. Completeness-Hinweis: vollständig, kein extra Read/Grep. |
| 2 | Assembly externe Assembly | `assembly` + `Example.External.Process.dll` | — | **Nicht ausgeführt** (Auto-Review / Approval fehlgeschlagen). |
| 3 | Fehler, Pfad außerhalb | `project`, `C:\Workspace\DoesNotExist` | — | **Nicht ausgeführt** (Auto-Review, Scope). |
| 4 | Leer, Filter | `scopeFilter=ZZZNoSuchScope_xyz123` | ~1k Zeichen | Score **8,50**, PASS, **0 Klassen**, trotzdem **1 Verstoß `SetupText`**. Completeness „Scope vollständig“. |
| 5 | Ungültiges `targetType` | `bogus` | ~3 Zeilen | `INVALID_ARGUMENT`: nur `project` oder `assembly`. |
| 6 | Assembly, fehlende Workspace-DLL | `assembly` + `…\bin\Debug\net8.0\Sample.Project.dll` | ~4 Zeilen | `INVALID_ARGUMENT`: Pfad muss auf vorhandene Datei zeigen. **Nicht** `ASSEMBLY_TARGET_UNSUPPORTED`. |
| 7 | Fehler, kein Projekt | `project`, `…\Sample.Project\DoesNotExist` | ~12 Zeilen | `PROJECT_NOT_INITIALIZED` + JSON-Vorlage für `ainetlinter.project.json`. |
| 8 | `minScore=9.5` | Defaults sonst | wie Call 1 | Score 8,88, Threshold 9,50, **FAIL**. Gleiche Top-Befunde. |
| 9 | `minScore=0` | — | wie Call 1 | Threshold **0,00**, PASS. |
| 10 | `maxViolations=1` | — | wie Call 1 | Ununterscheidbar vom Default (nur 1 Verstoß). |
| 11 | Truncation hoch | `maxViolations=100` | wie Call 1 | Weiter 1 Verstoß, Completeness vollständig. **Keine Token-Flut.** |
| 12 | `scopeFilter=…Setup` | — | ~1k | Score **8,67**, 186 Klassen, `SetupText` (**liegt in Setup**, stimmig). |
| 13 | `scopeFilter=Domain.Firmenkalender` | — | ~1k | Score **8,93**, **7 Klassen**, Top trotzdem **`SetupText` (Setup)** — außerhalb des Filters. Completeness lügt. |
| 14 | Alias `scope` | `scope=…Setup` | wie Call 12 | Identisch zu Call 12. Alias wirkt. |
| 15 | Alias `path` | `path=Domain.Firmenkalender` | wie Call 13 | Identisch zu Call 13. Alias wirkt. |
| 16 | Truncation leer | `maxViolations=0` | ~0,4k | Score 8,88, **„0 von 1 Verstößen (Top-Auswahl wegen maxViolations)“**. Hinweis: `get_violations` aufrufen. Keine Top-Liste. |
| 17 | Relativer Pfad | `targetPath=Sample.Project` | ~3 Zeilen | `INVALID_ARGUMENT`: Pfad muss absolut sein. |
| 18 | `minScore=-1` | — | wie Call 1 | Threshold **still 0,00**. Kein Fehler. |
| 19 | Assembly auf Ordner | `assembly` + Projektroot | ~4 Zeilen | `INVALID_ARGUMENT`: vorhandene Datei, Hint `.dll`/`.exe`. Wieder nicht `ASSEMBLY_TARGET_UNSUPPORTED`. |
| 20 | `maxViolations=-1` | — | wie Call 16 | Wie `maxViolations=0` (Truncation-Hinweis). |
| 21 | `minScore=10` | — | wie Call 1 | Threshold 10,00, **FAIL**. |
| 22 | Alias-Konflikt | `scopeFilter=…Setup` **und** `path=Domain.Firmenkalender` | wie Call 12 | **186 Klassen** = `scopeFilter` gewinnt, `path` still. |
| 23 | Dateiname-Filter | `scopeFilter=SetupText.cs` | ~1k | Score **8,50**, **1 Klasse**, `SetupText` — hier stimmig. |
| 24 | `scopeFilter=Tests.Logic` | — | ~1k | Score **9,42**, **567 Klassen**, Top trotzdem **`SetupText` (Setup)**. |
| 25 | Truncation Cap-Versuch | `maxViolations=500`, `minScore=8` | wie Call 1 | Weiter 1 Verstoß. Lösung ist „grün“, Ausgabe bleibt klein. |
| 26 | Assembly extern, Retry+Approval | wie Call 2 | — | Approval/Kontext fehlgeschlagen, kein Ergebnis. |
| 27 | Kombi optional | `scopeFilter=Contracts`, `minScore=8.88`, `maxViolations=1` | ~1k | Score **8,50**, Threshold 8,88, **FAIL**, **116 Klassen**, Top trotzdem **`SetupText` (Setup)**. |

## 3 Verdict

**Teilweise wie beschrieben.** Unscoped Happy Path, Pass/Fail, `minScore`, Aliase, absolute-Pfad-Pflicht, `PROJECT_NOT_INITIALIZED` und `INVALID_ARGUMENT` bei `targetType` stimmen. `maxViolations=0`/`-1` schaltet die Liste ab und verweist auf `get_violations` — das ist der dokumentierte Truncation-Pfad.

Abweichungen: **Top-Befunde ignorieren `scopeFilter`/`scope`/`path`**, während Score und Klassenzahl scoped sind. Leerer Filter (0 Klassen) liefert trotzdem denselben Verstoß. Completeness-Hinweis behauptet Scope-Vollständigkeit. `minScore<0` und `maxViolations<0` werden **still geklemmt**. Dokumentiertes Assembly-Unsupported wurde nicht als eigener Fehlercode beobachtet (Existenzcheck zuerst). JSON-Schema ohne Enums/Bounds.

## 4 Schwere

**`degraded`**

Als lösungweites CI-Gate (Default, kein Filter) kompakt, deterministisch, schnell — der Kernnutzen sitzt. Sobald ein Agent `scopeFilter` für ein Abschluss-Audit eines Verticals/Projekts setzt, sind die **Top-Befunde faktisch falsch** und der Completeness-Satz **verbietet Nachprüfung**. Das ist kein reines Schema-Reibungsproblem. Nicht `broken` für den Default-Pfad (Score/PASS der ganzen Solution bleibt brauchbar). Nicht nur `friction` (die Scope-Lüge ist ein Korrektheitsfehler).

## 5 Nutzbarkeit

**nur mit Workaround**

- **Ja** für: unscoped Quality-Gate vor Merge (`project` + Root, Default `minScore`).
- **Nein** für: scoped Abschluss-Audit — Top-Liste nicht trauen.
- Workaround: Score/Klassenzahl aus `safeguard` nehmen; Verstöße im Scope über `get_violations` (Hinweis existiert nur bei Truncation durch `maxViolations`). Filter `SetupText.cs` trifft zufällig, weil die eine echte Violation genau dort liegt.

## 6 Bugs+FP/FN

| Befund | Art | Call |
|---|---|---|
| Top-Befund `SetupText` (Setup) bei Filter `Domain.Firmenkalender` (7 Klassen) | **FP / Scope-Leak** | 13, 15 |
| Dasselbe bei `Tests.Logic` (567 Klassen) und `Contracts` (116 Klassen) | **FP / Scope-Leak** | 24, 27 |
| Filter ohne Treffer: 0 Klassen, trotzdem 1 Verstoß + PASS | **FP**, keine Leermenge | 4 |
| Completeness „vollständig für den angefragten Scope“ bei Leak | Irreführend | 4, 13, 24, 27 |
| `minScore=-1` → Threshold 0,00 | Clamp ohne Fehler | 18 |
| `maxViolations=-1` wie 0 | Clamp; Truncation-Hinweis ok | 20 |
| Alias-Konflikt: `scopeFilter` still über `path` | Undokumentierte Präzedenz | 22 |
| `ASSEMBLY_TARGET_UNSUPPORTED` nicht reproduziert | Fehlerpfad vs. Beschreibung | 2, 6, 19, 26 |

**Stichprobe intern (ohne zweites Tool):** Pfad `Sample.Project.Setup/Setup/SetupText.cs` steht in jeder Top-Zeile. Das widerspricht den scoped Klassenzahlen 0 / 7 / 116 / 567, außer Call 12/14 (Setup) und Call 23 (`SetupText.cs`). Memberzahl 19 vs. Limit 15 nicht unabhängig gegen die Datei geprüft (Auftrag: nur `safeguard`). Kein zweiter Top-Befund in dieser Solution — Truncation durch Menge war **nicht** auslösbar.

Leermenge (Call 4) ist **kein** klares „keine Dateien“, sondern ein scheinbar gültiges Gate mit Leak.

## 7 Token/IDs

Default (Call 1): **agententauglich klein**, kein Flood. `maxViolations=500` ändert nichts, weil nur 1 Verstoß existiert. Die Befürchtung „safeguard kann groß sein“ trifft **diese** Platform-Solution derzeit nicht; der Truncation-Mechanismus ist trotzdem verdrahtet (Call 16).

Keine Symbol-IDs, kein Cursor. Folge-Call: relativer Dateipfad + Zeile + Regelname. Bei Truncation expliziter Hint `get_violations`. Bei „vollständig“ **kein** Hint auf Folge-Tools — und genau dann ist der Scope-Leak am gefährlichsten.

StructuredContent in der Agent-Antwort nicht sichtbar; nur Fließtext + Completeness-/Truncation-Hinweis.

## 8 Roslyn-Wünsche

- Top-Violations **denselben `scopeFilter` anwenden** wie die Klassenzählung; bei 0 Typen im Scope: echte Leermenge, kein lösungweiter Rest.
- Completeness-Satz nur, wenn die gezeigte Liste wirklich im Scope liegt; sonst Truncation/Leak explizit.
- `INVALID_ARGUMENT` bei `minScore` außerhalb 0–10 und `maxViolations < 0` (statt Clamp).
- Schema: Enum `targetType`, Bounds an den Zahlen, Alias-Präzedenz (`scopeFilter` > `scope` > `path`) dokumentieren.
- Assembly: `ASSEMBLY_TARGET_UNSUPPORTED` **vor** der Dateiexistenz, oder Beschreibung an den Existenzcheck anpassen.
- Stabile Symbol-ID neben Datei/Zeile, damit `get_symbol_body` / `get_violations` ohne Namensraten folgen.
- Optional: `maxViolations`-Footer analog Hotspots (`N gesamt, M gezeigt`), auch wenn N=1.

## 9 Phase 3

Quelle: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Kein Patch in diesem Task.

### Scope-Leak der Top-Befunde (Calls 4, 13, 15, 24, 27) — `degraded`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Safeguard\SafeguardScanner.cs`
- **Symbol:** `SafeguardScanner.ComputeScoreAsync`
- **Ansatz:** `LinterEngine.RunAsync` läuft lösungweit; `scopeFilter` greift nur in `EnumerateConcreteClassesAsync` → `ShouldIncludeDocument`. Vor `BuildScoreResult` dieselbe Filterung wie `get_violations`: `ViolationScopeFilter.BuildFileToProjectMap` + `FilterAndSortViolations` (`src\AiNetLinter\Mcp\Tools\Analysis\ViolationScopeFilter.cs`). Penalty, Top-Liste und `TotalViolationCount` nur über die gefilterte Menge. Bei 0 Typen im Scope: leere Violations, Summary ohne Rest der Solution.

### Completeness-Satz bei Leak — `degraded`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Safeguard\SafeguardTool.cs`
- **Symbol:** `SafeguardTool.BuildSufficiencyHint`
- **Ansatz:** Hint „vollständig für den angefragten Scope“ nur, wenn die gezeigte Liste wirklich scoped ist (`ViolationsTruncated == false` **und** nach Scope-Filter). Sonst Truncation-/Leak-Satz wie bei `maxViolations`, plus Verweis auf `get_violations`.

### `minScore`/`maxViolations` stilles Clamping (Calls 18, 20) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Safeguard\SafeguardTool.cs`
- **Symbol:** `SafeguardTool.ExecuteAsync`
- **Ansatz:** `Math.Clamp(minScore, 0.0, 10.0)` und `Math.Max(0, maxViolations)` entfernen. Stattdessen `INVALID_ARGUMENT` (bereits `McpToolResults.Recoverable` / `InvalidArgument`), analog `MetricsTreeTool.ExecuteAsync` für `depth`/`topN`. Kein Roslyn.

### Alias-Präzedenz `scopeFilter` > `scope` > `path` (Call 22) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- **Symbol:** `AnalysisToolRegistrations.AddSafeguard` (`effectiveScope = scopeFilter ?? scope ?? path`)
- **Ansatz:** Reihenfolge in `SafeguardDescription` schreiben. Optional Konflikt (`scopeFilter` und `path` gesetzt, Werte verschieden) als `INVALID_ARGUMENT`. Kein Roslyn.

### `ASSEMBLY_TARGET_UNSUPPORTED` hinter Existenzcheck (Calls 6, 19) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs` vor `src\AiNetLinter\Mcp\AnalysisToolCall.cs`
- **Symbol:** `AnalysisTargetResolver.Resolve` (File.Exists / `.dll`/`.exe`) → erst danach `ProjectAnalysisDispatcher.ExecuteProjectAsync` → `UnsupportedAssemblyTarget`
- **Ansatz:** Project-only-Tools (`McpToolRegistrationOptions.ReadOnlyTool`) `targetType=assembly` **vor** `File.Exists` ablehnen, oder Resolver um Flag „Assembly nicht unterstützen“ erweitern. Existierender Code: `LinterErrorCodes.AssemblyTargetUnsupported`, `AssemblyAnalysisResponse.Unsupported`.

### JSON-Schema ohne Enums/Bounds — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- **Symbol:** `AddSafeguard`-Lambda (`string targetType`, `double minScore`, `int maxViolations`)
- **Ansatz:** Schema entsteht aus der Methodensignatur (`McpServerTool.Create`). Enum für `targetType`; Range in Validierung (siehe Clamp-Befund). Kein Roslyn-Walk.

### Stabile Symbol-ID neben Datei/Zeile — `wish`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Safeguard\SafeguardModels.cs`
- **Symbol:** `ViolationEntry` (nur `FilePath`/`LineNumber`/`RuleName`)
- **Ansatz:** Am Violation-Span `Document.GetSemanticModelAsync` + `SyntaxTree.GetRootAsync`, Token/Node an der Zeile, `ISymbol.TryGetDocCommentId()` (wie `SearchPatternRoslynEnricher.Resolved`). Feld an `ViolationEntry` und in `BuildTopViolationText` ausgeben.
