---
unit: 001
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-01
verdict: approved
---

# Review Einheit 001 — `get_violations` (Commit `e63176d`)

**Verdict: approved** — alle vier Ebenen geprüft, kein CRITICAL/MAJOR,
eine Handvoll MINOR-Beobachtungen, ein neuer Tech-Debt-Vorschlag.

## Selbst-Verifikation

**Kein Re-Run von Build/Test.** Ich habe ausschließlich das
`result.md`-Protokoll bewertet — das entspricht sowohl der Vorgabe im
Planer-Plan ("Plausibilitäts-Stichprobe reicht", Z. 174–179) als auch
der Orchestrator-Vorgabe im Auftrag ("Plausibilitäts-Stichprobe des
dokumentierten Aufrufs reicht — kein Re-Run erforderlich") und
`agents/kritiker.md` Z. 66–69 ("Der Kritiker führt **nicht**
routinemäßig nach, sondern bewertet das Protokoll"). Im Verlauf der
Prüfung habe ich keinen konkreten Verdacht entwickelt, der einen
gezielten Re-Run gerechtfertigt hätte (alle Stichproben in den
vier Ebenen bestätigen das `result.md`).

Konkrete Belege, die ich gegen den realen Codestand verifiziert habe
(per `Read`/`Grep`, nicht per `dotnet test`):

- Commit `e63176d` selbst via `git --no-pager show --stat` (15
  Dateien, Inhalt wie dokumentiert).
- Alle 13 in `state.md`/`plan.md` referenzierten Code-Dateien — Zeilen
  der zitierten Symbole manuell verifiziert, nicht aus dem Plan
  übernommen.
- Vorversion von `McpCodeGraphServer.cs` (Commit `995500e`) zur
  Bestätigung der **strikten Additivität** des Konstruktors
  (vorher 3 Parameter, jetzt 5 — beide neuen am Ende, beide mit
  Default `null`, kein bestehender Aufrufer bricht).
- `LinterEngine.RunAsync`-Signatur (`Core/LinterEngine.cs:64`) zur
  Bestätigung, dass `noCache: true, cacheTtlMinutes: 0` exakt der
  intendierte Aufrufer-Pfad ist.
- `RuleViolation`/`RuleRegistry.TryResolve`/`McpToolResults.SolutionNotLoaded`/
  `SourceFileCatalog.IsValidDocument` zur Bestätigung, dass die
  im Scanner genutzten Member existieren und die richtige Signatur haben.
- `rules.json`-`PathOverrides`-Block (Z. 405–421) — nur
  `FindReferencesTool`/`FindSymbolTool`/`AuditCommand` mit `2700`,
  **keine** PathOverrides für `GetViolationsTool`/`GetViolationsScanner`
  (wie vom Plan gefordert — gemessene Footprints 2451/1834 unter
  2500).

---

## 1 — Plan-Erfüllung (gegen `7474226:tasks/codegraph-mcp/step-010/step-plan.md`)

Alle 12 Plan-Punkte erfüllt.

| Plan-Punkt | Beleg | Status |
|---|---|---|
| `McpCodeGraphServer.cs:30-46` — additive Konstruktor-Parameter `Config? config = null, ILintConsole? consoleOverride = null` | `McpCodeGraphServer.cs:30-35` (exakt diese Signatur, beide am Ende, beide default `null`); Vorversion in `995500e` hatte nur `(catalog, console, maxLineCount)` | ✓ |
| `McpCodeGraphServer.cs:63, 71` — `Config`-Property (nie-null) + `Console`-Property | `McpCodeGraphServer.cs:63, 71`; Normalisierung im Konstruktor (Z. 40) `config ?? new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() }` | ✓ |
| `McpServerCommand.cs:36, 72-79` — `ResolveConfig` mit 1:1-Logik | `McpServerCommand.cs:72-79` (exakt die spezifizierte Form, Z. 75 + 77-78); Aufruf in Z. 36 als 4. Argument | ✓ |
| `GetViolationsTool.cs:22-34` — dünner Dispatch | `GetViolationsTool.cs:22-33`; `state.GetCurrentSolution()`, `SolutionNotLoaded()`-Pfad Z. 28, `state.Config`/`state.Console` durchgereicht Z. 31 | ✓ |
| `GetViolationsScanner.cs:43-74` — delegiert an `LinterEngine.RunAsync(..., noCache: true, cacheTtlMinutes: 0, ct)` | `GetViolationsScanner.cs:62` (`noCache: true, cacheTtlMinutes: 0, ct`); Post-Filter Z. 106-109; defensiver `try/catch` Z. 54-71; `OperationCanceledException` ausgenommen Z. 64 | ✓ |
| `FileStructureToolRegistrations.cs:13-17` — Klassenkommentar aktualisiert, `get_violations` raus | `FileStructureToolRegistrations.cs:9-18` (Kommentar erwähnt `AnalysisToolRegistrations`-Auslagerung); kein `get_violations`-Block mehr im `Register` (Z. 26-62) | ✓ |
| `AnalysisToolRegistrations.cs:26-41` — neue Registrar-Klasse mit `tools.Add(McpServerTool.Create(...))` inkl. Description | `AnalysisToolRegistrations.cs:28-40`; Description benennt C#-only, kein Disk-Cache, optionaler `scopeFilter` (Z. 34-39) | ✓ |
| `McpServerOptionsFactory.cs:44` — dritter `Register`-Aufruf | `McpServerOptionsFactory.cs:44` (`AnalysisToolRegistrations.Register(tools, mcpState)`); die anderen beiden in Z. 42-43 unverändert | ✓ |
| `rules.json` Z. 411-420 — `PathOverrides` für `FindReferencesTool`/`FindSymbolTool` (2700) | `rules.json:411-420` exakt wie spezifiziert; Precedent `AuditCommand.cs:407` in Z. 406-410 | ✓ |
| `ViolationTrigger.cs` Z. 1-11 — `#nullable enable`, `public class` ohne `sealed` | `ViolationTrigger.cs:1, 5-6`; `#nullable enable` Z. 1, `public class` ohne `sealed` Z. 5-6; genau eine Datei | ✓ |
| `GetViolationsToolTests.cs` Z. 13-83 — alle 5 Tests mit den spezifizierten Namen | Z. 14, 26, 42, 57, 71 — alle fünf im Plan genannten Test-Namen vorhanden | ✓ |
| `McpServerCommandTests.cs` — `ServerRespondsWithEightTools` + neuer E2E-Test + 2 `ResolveConfig`-Tests | Z. 134 (`ServerRespondsWithEightTools`, Erwartung `8`); Z. 159 (`Assert.Contains(tools, t => t.Name == "get_violations")`); Z. 217 (`RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation`); Z. 433 + 454 (zwei `ResolveConfig`-Tests) | ✓ |
| `GetIndexScopeToolTests`/`GetHotspotsToolTests` — 4→5 `.cs`-Dateien | `GetIndexScopeToolTests.cs:37` (`.cs: 5 Dateien`); `GetHotspotsToolTests.cs:83` (`Gescannt: 5 .cs-Dateien`) | ✓ |
| Selbst-Lint-Footprint (DoD-Pflicht) | `result.md` Z. 56-62 dokumentiert: `GetViolationsTool` 2451, `GetViolationsScanner` 1834, `FileStructureToolRegistrations` 2480, `AnalysisToolRegistrations` 2459 — alle unter 2500 | ✓ Plausibilität (kein Re-Run) |

Keine Plan-Abweichung. Eine **vom Plan vorgesehene Ausweich-Option
wurde umgesetzt**: `AnalysisToolRegistrations` als dritte
Registrar-Klasse. Das ist die im Plan (`step-010/step-plan.md`
"Datei 5"/"Datei 5b"-Block) explizit antizipierte Variante für den Fall,
dass `FileStructureToolRegistrations` das 2500-Limit reißt — laut
`result.md` Z. 82 mit Footprint 2492/2500 exakt eingetreten.
Plan-Erfüllung im Sinne des Plans.

## 2 — Rules-Konformität (`AiNetLinter.mdc` + `AiNetLinterRichtlinien.mdc`)

| Regel | Beleg | Status |
|---|---|---|
| `EnforceNullableEnable` (alle 7 modifizierten/neuen `.cs`) | `GetViolationsTool.cs:1`, `GetViolationsScanner.cs:1`, `AnalysisToolRegistrations.cs:1`, `FileStructureToolRegistrations.cs:1` (mod.), `McpServerCommand.cs:1`, `McpCodeGraphServer.cs:1`, `ViolationTrigger.cs:1` — alle mit `#nullable enable` Z. 1 | ✓ |
| `EnforceNullableEnable` (Test-Datei) | `GetViolationsToolTests.cs:1` ohne Direktive — **vorbestehend** (kein Eingriff in dieser Einheit), Orchestrator-Vorgabe: "kein Verstoß in dieser Einheit" | ✓ akzeptiert |
| `EnforceSealedClasses` (Tool-Klassen `internal static` → entfällt) | `GetViolationsTool.cs:22 internal static`; `GetViolationsScanner.cs:33 internal static`; `AnalysisToolRegistrations.cs:19 internal static`; `FileStructureToolRegistrations.cs:19 internal static` | ✓ |
| `EnforceSealedClasses` (konkrete Klassen) | `McpCodeGraphServer.cs:22 internal sealed`; `GetViolationsToolTests.cs:11 public sealed`; `McpServerCommandTests.cs:19 public sealed` (vorbestehend) | ✓ |
| `AIContextFootprint` ≤ 2500 | `result.md` Z. 56-62 — alle vier kritischen Klassen unter 2500 | ✓ Plausibilität |
| `MaxLineCount` ≤ 500 | `GetViolationsTool.cs` 34 Z. (Z. 1-34), `GetViolationsScanner.cs` 172 Z. (Z. 1-172), `AnalysisToolRegistrations.cs` 42 Z. (Z. 1-42), `GetViolationsToolTests.cs` 84 Z. (Z. 1-84) — alle weit unter Limit | ✓ |
| `MaxMethodLineCount` ≤ 60 | `BuildViolationsTextAsync` Z. 43-74 = 32 Z.; `FormatReport` Z. 100-135 = 36 Z.; `AppendSection` Z. 143-166 = 24 Z.; `ResolveConfig` Z. 72-79 = 8 Z. — alle unter 60 | ✓ |
| `MaxMethodParameterCount` ≤ 4 | `McpCodeGraphServer.cs:155` `TryApplyContentChange` hat 5 Parameter — **vorbestehend**, in `tech-debt.md` TD-007 dokumentiert (Override `MaxMethodParameterCountForNonPublic: 6`); `result.md` Z. 91 referenziert TD-005/TD-007 | ✓ kein neuer Verstoß |
| `AiNetLinterRichtlinien.mdc` §1 (Einfachheit vor Abstraktion) | `GetViolationsScanner.cs:56-62` — `LinterEngine.RunAsync(solution, noCache: true, ...)` wiederverwendet, keine eigene Lint-Loop | ✓ |
| `AiNetLinterRichtlinien.mdc` §2 (kein DI, kein ALC) | `AnalysisToolRegistrations.cs:28-30` Delegate-Closure auf `GetViolationsTool.ExecuteAsync`; `McpServerOptionsFactory.cs:44` analog zu den bestehenden Registrars | ✓ |
| `AiNetLinterRichtlinien.mdc` §5 (Result-Pattern, kein leeres `catch`) | `GetViolationsScanner.cs:54-71` — defensiver `try/catch` ruft `LinterErrorFormatter.Format(LinterErrorCodes.AnalysisFailed, ...)` und liefert Text, kein rethrow; `OperationCanceledException` ausgenommen (`AllowCancellationShutdownCatch`) | ✓ |
| `dotnet build` ohne Warnungen | `state.md` Z. 81 dokumentiert 0 Warnungen | ✓ Plausibilität |

## 3 — Logische Korrektheit

| Aspekt | Beleg | Status |
|---|---|---|
| Disk-Cache-Bypass | `LinterEngine.RunAsync(Solution, bool noCache, int cacheTtlMinutes, CancellationToken)` Signatur in `Core/LinterEngine.cs:64`; Aufruf in `GetViolationsScanner.cs:62` mit `noCache: true, cacheTtlMinutes: 0`; `result.md` Z. 66-78 dokumentiert Filter-Test mit 0 Cache-Files | ✓ |
| Cache-Existenz als Vorbehalt | 6 Cache-Files aus pre-existing Tests (`LinterEngineCacheTests`/`StaticTestSentinelExemptionTests`) — `result.md` "Caveat" Z. 78 dokumentiert; **kein Step-Regress** | ✓ akzeptiert |
| Scope-Filter-Semantik | `GetViolationsScanner.cs:91-98` — case-insensitive `Contains` auf `projectName` ODER `Path.GetRelativePath(solutionDir, filePath)`; konsistent mit `get_hotspots` (`state.md`/`konzept.md` Z. 175-183) | ✓ |
| Markdown-Formatierung | `GetViolationsScanner.cs:156` `\| Datei \| Zeile \| Regel \| Details \|`; Z. 162 Forward-Slashes; Z. 128-132 Severity-Trennung Fehler/Warnungen | ✓ |
| Thread-Sicherheit | `McpCodeGraphServer.GetCurrentSolution` lockt (Z. 79); Roslyn-`Solution` immutable; `LinterEngine` pro-call `new` (Z. 56); parallele Lese-Zugriffe auf `Document`s Roslyn-intern erlaubt — keine neuen Locks nötig | ✓ |
| Tests echt (kein Pseudo-Coverage) | siehe Details unten | ✓ |
| A3-Fehlschlag-Nachweis | `result.md` "Cache-Bypass-Verifikation" Z. 66-78 — Filter-Test 0 Cache-Files; Test-Filter belegt den noCache-Pfad | ✓ |

**Test-Plausibilität (Stichprobe mit konkreten Assertions):**

- `ExecuteAsync_LoadedSolutionNoScopeFilter_ReturnsViolationForKnownFixture` (`GetViolationsToolTests.cs:26-39`):
  Assertion `Assert.Contains("ViolationTrigger", textContent.Text, StringComparison.Ordinal)`
  — würde bei Entfernen von `ViolationTrigger.cs` aus der Fixture (oder
  wenn `EnforceSealedClasses` deaktiviert wäre) auf rot kippen.
  → **Echter Test.**

- `ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable`
  (`GetViolationsToolTests.cs:71-83`):
  Assertion auf `"| Datei | Zeile | Regel | Details |"` — würde bei
  nicht-Markdown-Output (z. B. Plain-Text-Liste) rot kippen.
  → **Echter Test.**

- `ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessage`
  (`GetViolationsToolTests.cs:57-68`):
  Assertion auf `"Keine Dateien im Scope"` — würde bei fehlender
  Fallunterscheidung im Scanner (`FormatReport` Z. 111-114) rot kippen.
  → **Echter Test.**

- `RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation`
  (`McpServerCommandTests.cs:217-241`):
  Echter Subprozess-Aufruf gegen die Fixture, Assertion auf
  `"ViolationTrigger"`-Substring — würde bei nicht-registriertem Tool
  (d. h. AnalyseToolRegistrations nicht in
  `McpServerOptionsFactory:44` eingebunden) rot kippen, weil der
  `client.CallToolAsync("get_violations", ...)`-Call dann mit
  ToolNotFoundError fehlschlagen würde.
  → **Echter E2E-Test.**

- `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
  (`GetViolationsToolTests.cs:14-23`):
  Assertion `Assert.True(result.IsError)` + `"SOLUTION_NOT_LOADED"` — würde
  bei fehlendem `McpToolResults.SolutionNotLoaded()`-Pfad in
  `GetViolationsTool.cs:28` rot kippen.
  → **Echter Test.**

Keine "assert(true)"-Pseudo-Tests, keine reine Implementierungs-
Nachsprecher. Die 5 Unit-Tests + 1 E2E-Test sind echte, voneinander
unabhängige Plausibilitäts-Anker.

## 4 — Konzept-Treue (`konzept.md`)

| Muss-Haven / Aspekt | Beleg | Status |
|---|---|---|
| "`get_violations` umgeht den bestehenden Disk-Cache" (Z. 175-183) | `GetViolationsScanner.cs:62` `noCache: true, cacheTtlMinutes: 0`; Begründung in XMLDoc Z. 23-27 | ✓ |
| "Explizite Scope-Kommunikation" (C#-only, kein Cache, scopeFilter) | `AnalysisToolRegistrations.cs:34-39` nennt dreifach: ".cs-Dateien, keine .js/.razor/.xaml/.html/.css-Dateien" + "Kein Disk-Cache" + "Optionaler scopeFilter" | ✓ |
| "Thread-sicherer Zugriff" (Z. 185-187) | siehe Ebene 3, keine neuen Locks nötig | ✓ |
| "Dogfooding pro Tool-Step" (Z. 193-204) | `result.md` Abschnitt "Dogfooding" Z. 116-144 — Ad-hoc-Aufruf gegen `AiNetLinter.slnx`, 0 Violations, konsistent mit CLI-Lauf | ✓ Plausibilität |
| Tool-Set-Tabelle Z. 550 ("codiert, Review offen") | Verschiebung auf "fertig" ist Sache des Nutzers nach `approved` (A7 — Konzept nicht selbst anpassen) | ✓ wartet auf Folge-Schritt |
| Konzept-Update `tasks/codegraph-mcp-next/Konzept.md` im selben Commit | außerhalb dieses Task-Scopes (`state.md` Z. 58-59, `konzept.md` "Bewusst außerhalb dieses Tasks"); nicht Inhalt der Review-Einheit | ✓ akzeptiert |

**Konzept-Konflikte: keine.**

**Beobachtung (außerhalb der `issues`-Bewertung, da reine Notiz):**
Die Konzept-Referenz "Tool-Set-Tabelle Z. 550" — ich habe Z. 550
verifiziert: dort steht im Konzept die Liste der 9 Tools mit
Status-Spalte. Der exakte Wortlaut ist hier nicht entscheidend (die
Stelle existiert, der Inhalt passt zur Plan-Aussage). Nach
`approved` dieser Einheit kann der Nutzer/Konsolidierungs-Planer den
Status von `get_violations` von "Review offen" auf "reviewt"
verschieben.

---

## Sonstige Beobachtungen (MINOR)

Diese Punkte landen **nicht** in `issues`, weil sie weder Build/Tests
brechen noch echte Logikfehler sind noch Regel- oder
Konzept-Verstöße mit Substanz darstellen. Sie sind stilistisch oder
strukturell, aber alle in "fertig, fertig" — kein Anlass für eine
Fix-Runde (A5).

- **M-1 — `consoleOverride`-Parameter ungenutzt.**
  `McpCodeGraphServer.cs:35, 38` führt einen `ILintConsole?
  consoleOverride = null`-Parameter ein, der in keinem aktiven
  Aufrufer genutzt wird. `McpServerCommand.cs:36` und alle Tests
  übergeben nur `console`. Im Plan als "Redundanz-Erlaubnis für
  künftige Aufrufer" und im `result.md` Z. 93 explizit als
  "beibehalten, weil (a) der Plan ihn explizit so vorsah, (b) keine
  aktive Nutzung im Step-Scope" dokumentiert. Konsequenz: der
  Konstruktor landet mit 5 Parametern exakt am
  `MaxConstructorDependencies`-Limit (5/5), siehe TD-Vorschlag
  weiter unten.

- **M-2 — `LinterArgs`-Test-Duplikation zwischen `ResolveMaxLineCount` und `ResolveConfig`.**
  `McpServerCommandTests.cs:402-430` und Z. 432-462 sind strukturell
  identisch (Temp-Dir, Mini-`rules.json` mit `MaxLineCount: 5`,
  `LinterArgs`, `Assert.Equal(5, result.X.Y)`). Die Duplikation ist
  symmetrisch (beide Resolver testen das gleiche Verhalten, nur
  mit anderer Rückgabe-Entität), kein Verstoß gegen TD-006/DRY,
  aber auffällig. Beibehalten wegen Lesbarkeit.

- **M-3 — `IReadOnlyCollection<RuleViolation> violations;` ohne Initialisierung.**
  `GetViolationsScanner.cs:53` deklariert die Variable ohne
  Initialisierung; C# definite-assignment ist erfüllt, weil der
  `catch`-Pfad in Z. 66-71 ein `return` trägt und `FormatReport` nur
  im Erfolgsfall aufgerufen wird. Defensiver wäre
  `Array.Empty<RuleViolation>()` als Default. Kein Logikfehler.

- **M-4 — `ViolationTrigger.cs` ist eine bewusste Lint-Verletzung in der Test-Fixture.**
  `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/ViolationTrigger.cs:5-6`
  (`public class ViolationTrigger` ohne `sealed`) ist eine
  deterministische `EnforceSealedClasses`-Verletzung — genau der vom
  Plan (`step-plan.md` Datei 7) vorgesehene Test-Anker. Im
  Produktions-Selbst-Lint unauffällig, weil die Fixture-Dateien
  nicht zum `--path .`-Scan gehören. Erwähnenswert, weil ein
  Aufräumer in 6 Monaten die Datei versehentlich "reparieren"
  könnte (Test würde dann rot kippen, ohne dass Code geändert
  wurde). Plan-konform.

- **M-5 — `GetViolationsScanner` reichert `LinterEngine` pro Aufruf neu.**
  `GetViolationsScanner.cs:56` (`new LinterEngine(...)` pro Call) ist
  die im Plan (`step-plan.md` "Thread-Sicherheit") explizit
  dokumentierte Entscheidung (kein Shared-Mutation, keine Locks
  nötig). Bei hoher Call-Frequenz messbare Allokation, irrelevant
  für ein Orientierungs-Tool. Beobachtung, kein Tech-Debt-Eintrag
  wert — strukturell sauber.

---

## Tech-Debt-Vorschlag

**TD-009 (Vorschlag) — `McpCodeGraphServer`-Konstruktor mit 5 Parametern am `MaxConstructorDependencies`-Limit [Priorität: mittel]**

- **Ort:** `src/AiNetLinter/Mcp/McpCodeGraphServer.cs:30-46`
- **Befund:** Der Konstruktor hat jetzt 5 Parameter (`SourceFileCatalog?, ILintConsole?, int, Config?, ILintConsole?`). Das deckt sich exakt mit dem `MaxConstructorDependencies: 5`-Limit aus `rules.json` (siehe `AiNetLinter.mdc` Z. 27) — der Selbst-Lint schlägt derzeit nicht an, weil der Wert exakt erreicht ist. **Die Reserve ist weg.** Die `konzept.md` P0/P1-Erweiterungen ("`--mcp-log`", "Kaltstart entkoppeln", "Staleness-Sweep Verzeichnis-`mtime`", "`rules.json`-Auto-Discovery" usw., Z. 207-324) werden `McpCodeGraphServer` in den nächsten Schritten mit hoher Wahrscheinlichkeit erneut erweitern — die erste sechste Dependency reißt das Limit und damit den Build.
- **Vorschlag:** Bei der nächsten Erweiterung an `McpCodeGraphServer` den Konstruktor auf ein Input-`record` umstellen (analog zum Vorschlag in TD-007 für `TryApplyContentChange`). Konkret: ein `internal sealed record McpCodeGraphServerOptions(SourceFileCatalog? Catalog, ILintConsole Console, int MaxLineCount, Config Config)` (oder vergleichbar). Dadurch wachsen zukünftige Konfigurations-Erweiterungen am `record` (additive Property), nicht an der Parameterliste.
- **Status:** offen

Falls angenommen, **Index-Zeile** (am Dateianfang von `tech-debt.md`
einzufügen, direkt nach TD-008):

```
| TD-009 | `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (Konstruktor) | mittel | 5/5 Parameter am `MaxConstructorDependencies`-Limit, keine Reserve für die P0/P1-`McpCodeGraphServer`-Erweiterungen aus `konzept.md`. |
```

---

## Hinweis zu TD-008 (kein direkter Edit, A7/A5)

`tech-debt.md` Z. 37/90-95 (`TD-008`) dokumentiert die
`PathOverrides`-Lösung für die `Config`-Pull-in-Regression in
`FindReferencesTool`/`FindSymbolTool` als "pragmatisch statt
strukturell". **Meine Bewertung: TD-008 ist inhaltlich weiter gültig**
und sollte **nicht** auf "gefixt" gesetzt werden:

- Die `PathOverrides` (`rules.json:411-420`) sind unverändert aktiv.
- Die strukturelle Schuld (`ILinterEngineConfig`-Kapselung oder
  vergleichbar) ist **nicht** behoben.
- Die im Konzept (Z. 207-324) angekündigten Erweiterungen
  (`rules.json`-Auto-Discovery, `--mcp-log`, "lädt noch"-Zustand) werden
  `McpCodeGraphServer` weiter aufwerten und damit den
  Pull-in-Druck auf Tool-Klassen tendenziell **verschärfen**, nicht
  auflösen.

Mein Vorschlag (nur als Hinweis im Review, **nicht** als direkter
`tech-debt.md`-Edit — A7/A5, der Nutzer entscheidet): TD-008 bei der
Konsolidierung der Tech-Debt-Liste für die kommenden Schritte
priorisiert im Auge behalten, weil der Schwellwert "wann lohnt sich
der 4-6h-Refactor" (`result.md` Z. 91) durch die `konzept.md`-P0/P1-
Erweiterungen schneller erreicht wird als im `result.md`-Stand
angenommen.

---

## Verdict-Begründung (Zusammenfassung)

- **Plan-Erfüllung:** 12/12 Plan-Punkte + Selbst-Lint-Footprint
  dokumentiert. Keine Plan-Abweichung, eine vom Plan ausdrücklich
  vorgesehene Ausweich-Option (`AnalysisToolRegistrations`) umgesetzt.
- **Rules-Konformität:** 9/9 Regeln gehalten; einzige
  Test-Datei-ohne-`#nullable enable` ist vorbestehend (Orchestrator-
  Vorgabe), `MaxMethodParameterCount` an `McpCodeGraphServer.cs:155`
  ist vorbestehend (TD-007) — keine neuen Verstöße in dieser Einheit.
- **Logische Korrektheit:** Disk-Cache-Bypass korrekt umgesetzt,
  Scope-Filter-Semantik konsistent mit `get_hotspots`, Tests sind
  echte Plausibilitäts-Anker (5 Stichproben mit `file:line`-Belegen),
  keine Pseudo-Coverage, Thread-Sicherheit durch bestehenden
  `McpCodeGraphServer`-Lock abgedeckt.
- **Konzept-Treue:** alle vier für `get_violations` relevanten
  Muss-Havens (Cache-Bypass, Scope-Kommunikation, Thread-Sicherheit,
  Dogfooding) erfüllt; Tool-Status-Verschiebung wartet auf den
  Nutzer/Folge-Planer nach `approved`; `tasks/codegraph-mcp-next/
  Konzept.md`-Update im selben Commit ist außerhalb dieses
  Task-Scopes.

**Keine CRITICAL/MAJOR-Findings.** Verdict: `approved`.
