---
unit: 001
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-01
---

# Plan Einheit 001 — Kritiker-Review für `get_violations` (Commit `e63176d`)

## Ziel der Einheit

Den bereits committeten `get_violations`-Code (`e63176d`) gegen den
ursprünglichen `step-010`-Plan, die Projektregeln und das Konzept
prüfen — **kein Neu-Code, kein Coder-Schritt**. Diese Einheit schließt
die offene Halb-Schleife (Coder war vorbei, Kritiker nicht) und
überführt EPIC-04 von 3/4 in 4/4 fertig reviewt, damit der Orchestrator
anschließend `search_pattern` (oder den nächsten Konzept-Punkt) planen
kann. Bezug: `konzept.md` Zeilen 82–91 ("Codiert, aber Review nicht
abgeschlossen — erste Einheit dieses Tasks") und Zeile 663
("Kritiker-Review für den bereits vorhandenen `get_violations`-Code
nachholen — kein Neu-Code").

## Betroffene Dateien / Module (nur lesend durch den Kritiker)

Code-Commit `e63176d` enthält exakt die im `step-010/plan.md` (Commit
`7474226`) vorgesehenen Dateien — `git show --stat e63176d`
bestätigt 15 geänderte Dateien mit dem laut `state.md` und
`step-result.md` erwarteten Scope:

- `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` (neu, 34 Z.)
- `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` (neu, 172 Z.)
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (neu, 42 Z.)
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (mod.)
- `src/AiNetLinter/Commands/McpServerCommand.cs` (mod.)
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` (mod.)
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (mod.)
- `rules.json` (mod., `PathOverrides` für `FindReferencesTool` /
  `FindSymbolTool`)
- `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/ViolationTrigger.cs` (neu, 11 Z.)
- `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs` (neu, 84 Z.)
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (mod.)
- `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs` (mod.)
- `src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs` (mod.)
- `tasks/codegraph-mcp-next/Konzept.md` (mod., Konzept-Verfeinerung,
  **nicht** Inhalt der Review-Einheit — `state.md` Zeile 58–59
  bestätigt „außerhalb dieses Task-Scopes")
- `tasks/codegraph-mcp/step-010/coder-todos.md` (neu, Beifang der
  externen Übernahme, nicht Code-relevant)

`state.md` Zeile 49–55 bestätigt: Build/Test-Stand **1088/1088 grün,
0 Warnungen, 0 Fehler** (Stand nach Merge, `phase 1` Baseline).

## Schritte (nur ein Schritt, kein Coder)

### Einziger Schritt — Kritiker-Aufruf (Subagent)

- **Rolle:** `agents/kritiker.md` mit `units/001/plan.md` (dieser
  Plan) als Eingabe, zuzüglich `units/001/result.md` (aus dem
  Git-Verlauf unter `7474226:tasks/codegraph-mcp/step-010/step-result.md`
  übernommen oder als Stub kopiert, falls eine 1:1-Übernahme
  unsicher ist — siehe "Besonderheit" unten).
- **Eingabe:** Plan (dieser), `konzept.md`, `tech-debt.md`,
  Projektregeln (`.agents/rules/AiNetLinter.mdc` +
  `AiNetLinterRichtlinien.mdc`), Diff/Codestand des Commits
  `e63176d` (oder die fünf Kern-Dateien direkt).
- **Pflicht:** Die vier Ebenen aus `agents/kritiker.md` Zeile 26–36
  durchgehen — Plan-Erfüllung, Rules-Konformität, logische
  Korrektheit, Konzept-Treue. Resultat als
  `units/001/review.md` (Verdict + Findings + sonstige
  Beobachtungen + etwaige Tech-Debt-Einträge).
- **Commit:** durch den Orchestrator (gezielter `git add
  units/001/{plan,review}.md`, A4).
- **Wichtig — kein Coder dazwischen** im Happy Path. Der `kritiker.md`
  ist explizit ("Auch nicht trivial" — A2). Wenn der Kritiker
  `issues` meldet, löst das **eine Fix-Runde** aus (nächster
  Abschnitt), keinen spontanen Coder-Aufruf in Einheit 001.

### Besonderheit — `result.md`-Bezug

Der ursprüngliche Coder-Resultat-Text liegt in Git unter
`7474226:tasks/codegraph-mcp/step-010/step-result.md`. Für die
`review.md` braucht der Kritiker den `result.md` als Eingabe (laut
`agents/kritiker.md` Zeile 19). Empfehlung an den Orchestrator: vor
dem Kritiker-Aufruf `git show 7474226:tasks/codegraph-mcp/step-010/step-result.md
> tasks/codegraph-mcp-server/units/001/result.md` (read-only
Übernahme, kein Schreibvorgang am Original). Falls der
Orchestrator das als eigenen Schritt zählt → A1 hochzählen.

## Erwartete Verdict-Optionen

| Verdict | Folge |
|---|---|
| `approved` | Einheit 001 fertig. EPIC-04 in `konzept.md` Zeile 79
auf "3/4 + 1 reviewt" fortschreiben (Sache des Nutzers / Folge-
Planers, nicht des Planers hier — A7). Nächste Einheit offen für
den nächsten Planer-Aufruf; **kein Vorausplanen** (Kernel Teil B
"Drift"). |
| `issues` | Standard-Fix-Runde nach `orchestrator.md` Phase 2.3:
`units/001/fix-01/` mit eigenem Planer + Coder + Kritiker
(`XX` fortlaufend, erste Runde `01`). `max_fix_pro_einheit` (3)
und `max_fix_gesamt` (12) zählen, Zähler in `state.md`
hochschreiben. Erst die Fix-Runde, dann weiter — nicht direkt
aus dem Issue-Critik in einen Re-Planer springen. |
| `blocked` | Orchestrator fragt den Nutzer (`konzept.md`-Konflikt
oder unauflösbare Mehrdeutigkeit — A6). Kein Selbstentscheid. |

## Prüfkriterien für den Kritiker (4 Ebenen, Checkliste)

### 1 — Plan-Erfüllung (gegen `7474226:tasks/codegraph-mcp/step-010/step-plan.md`)

- [ ] **Datei 1 — `McpCodeGraphServer.cs` Zeile 30–46:** Konstruktor-
  Erweiterung um `Config? config = null, ILintConsole? consoleOverride = null`
  (additiv am Ende, default `null`); Properties `Config` (Zeile 63,
  nie-null normalisiert mit `?? new Config { Global = new GlobalConfig(),
  Metrics = new MetricsConfig() }`) und `Console` (Zeile 71) sind
  vorhanden. **Strikte Additivität** (kein Entfernen / Verschieben
  bestehender Parameter) — wäre Bruch aller bestehenden
  Call-Sites (`McpServerCommand.cs:36` ist der einzige Produktiv-
  Aufrufer; Tests in `McpServerCommandTests`/`*ToolTests`).
- [ ] **Datei 2 — `McpServerCommand.cs` Zeile 36 + 72–79:** `ResolveConfig`
  vorhanden mit der im Plan spezifizierten 1:1-Logik
  (`ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false) ??
  new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() }`);
  Aufruf in `McpCodeGraphServer`-Konstruktion (Zeile 36) eingefügt.
- [ ] **Datei 3 — `GetViolationsTool.cs` Zeile 22–34:** dünner
  Dispatch wie `GetHotspotsTool`-Vorbild; `state.Config` und
  `state.Console` an Scanner durchgereicht; `SOLUTION_NOT_LOADED`-
  Pfad via `McpToolResults.SolutionNotLoaded()`.
- [ ] **Datei 4 — `GetViolationsScanner.cs` Zeile 43–74:** delegiert
  Lint-Arbeit an `LinterEngine.RunAsync(solution, noCache: true,
  cacheTtlMinutes: 0, ct)` — kein Neubau einer Lint-Loop
  (Plan-Erfüllung + `AiNetLinterRichtlinien.mdc` §1
  "Einfachheit vor Abstraktion"). Post-Filter statt Pre-Filter
  (Zeile 106–109). Defensiver `try/catch` (Zeile 64) umgeht
  `OperationCanceledException`.
- [ ] **Datei 5 — `FileStructureToolRegistrations.cs` Zeile 13–17:**
  Klassenkommentar aktualisiert (`get_violations` ausgelagert in
  `AnalysisToolRegistrations`); `get_violations`-Block ist **nicht
  mehr** in dieser Klasse.
- [ ] **Datei 5b — `AnalysisToolRegistrations.cs` Zeile 26–41:** neue
  Registrar-Klasse mit genau dem im Plan spezifizierten
  `tools.Add(McpServerTool.Create(...))`-Block inkl. Description
  (C#-only, kein Disk-Cache, optionaler `scopeFilter`).
- [ ] **Datei 6 — `McpServerOptionsFactory.cs` Zeile 44:** dritter
  `Register`-Aufruf (`AnalysisToolRegistrations`) zusätzlich zu
  den bestehenden beiden.
- [ ] **Datei 7 — `rules.json` Zeile 411–420:** zwei neue
  `PathOverrides` für `FindReferencesTool`/`FindSymbolTool` mit
  `MaxAIContextFootprint: 2700` (Precedent `AuditCommand.cs:407`).
  **Achtung:** für `GetViolationsTool`/`GetViolationsScanner`
  sind **keine** PathOverrides nötig (gemessene Footprints
  2451/1834 laut `step-result.md`, beide unter 2500) — wenn der
  Kritiker dort Overrides findet, ist das **ein Fund**, kein
  Plan-Erfüllungs-Punkt.
- [ ] **Datei 8 — `ViolationTrigger.cs` Zeile 1–11:** `#nullable
  enable`, `public class` **ohne** `sealed` (deterministische
  `EnforceSealedClasses`-Violation), genau eine Datei.
- [ ] **Datei 9 — `GetViolationsToolTests.cs` Zeile 13–83:** alle 5
  im Plan genannten Tests vorhanden mit den spezifizierten Namen:
  `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`,
  `ExecuteAsync_LoadedSolutionNoScopeFilter_ReturnsViolationForKnownFixture`,
  `ExecuteAsync_ScopeFilterMatchesProjectName_RestrictsViolations`,
  `ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessage`,
  `ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable`.
- [ ] **Datei 10 — `McpServerCommandTests.cs`:** `RunAsync_ValidFixture_ServerRespondsWithSevenTools`
  → `…EightTools` umbenannt (Zeile 134, Erwartung `8`); neuer
  E2E-Test `RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation`
  (Zeile 217); zwei `ResolveConfig`-Tests (Zeilen 433, 454).
- [ ] **Datei 11 — `GetIndexScopeToolTests`/`GetHotspotsToolTests`:** Erwartung
  4→5 `.cs`-Dateien angepasst (Fixture-Erweiterung).
- [ ] **Selbst-Lint-Footprint (DoD-Pflicht):** `step-result.md` Zeile
  "Selbst-Lint-Footprint-Kontrolle" dokumentiert: `GetViolationsTool`
  2451, `GetViolationsScanner` 1834, `FileStructureToolRegistrations`
  2480, `AnalysisToolRegistrations` 2459 — alle unter 2500. **Plausibilitäts-
  Stichprobe** (nicht zwingend Re-Run): reicht ein begründeter
  Verweis auf das dokumentierte Resultat. Falls der Kritiker Footprint-
  Re-Runs für nötig hält: gezielt nur die genannten vier Ziele.

### 2 — Rules-Konformität (`AiNetLinter.mdc` + `AiNetLinterRichtlinien.mdc`)

- [ ] **`EnforceNullableEnable`:** jede neue `.cs`-Datei beginnt
  mit `#nullable enable`. Stichproben: `GetViolationsTool.cs:1`,
  `GetViolationsScanner.cs:1`, `AnalysisToolRegistrations.cs:1`,
  `FileStructureToolRegistrations.cs:1` (modifiziert), `McpServerCommand.cs:1`,
  `McpCodeGraphServer.cs:1`, `ViolationTrigger.cs:1`, `GetViolationsToolTests.cs:1`.
  Test-Datei (`GetViolationsToolTests.cs`) hat **kein**
  `#nullable enable` — bewusst, weil sie bereits vor diesem Commit
  ohne Direktive existierte und der `step-result.md` keine
  Änderung dieser Datei an Zeile 1 dokumentiert; das ist **kein**
  Verstoß in der Review-Einheit (kein neuer Code, keine
  Verschärfung des bestehenden Standes).
- [ ] **`EnforceSealedClasses`:** Tool-Klassen sind `internal static`
  (statisch impliziert nicht-erweiterbar → `sealed`-Anforderung
  entfällt, `GetViolationsTool.cs:22`, `GetViolationsScanner.cs:33`,
  `AnalysisToolRegistrations.cs:19`, `FileStructureToolRegistrations.cs:19`).
  Test-Klassen sind explizit `public sealed`
  (`GetViolationsToolTests.cs:11`, `McpServerCommandTests.cs`
  bereits vorbestehend). `McpCodeGraphServer.cs:22`: `internal
  sealed` ✓.
- [ ] **`AIContextFootprint` ≤ 2500:** siehe Plan-Erfüllung —
  alle vier kritischen Klassen unter 2500, dokumentiert in
  `step-result.md`. PathOverrides nur dort, wo Regression
  auftritt (`FindReferencesTool`/`FindSymbolTool` Z. 411/416,
  +200 Pull-in durch `using AiNetLinter.Configuration;` in
  `McpCodeGraphServer.cs`).
- [ ] **`MaxLineCount` ≤ 500:** `GetViolationsTool.cs` 34 Z.,
  `GetViolationsScanner.cs` 172 Z., `AnalysisToolRegistrations.cs`
  42 Z. — alle weit unter Limit. `GetViolationsToolTests.cs`
  84 Z. ✓.
- [ ] **`MaxMethodLineCount` ≤ 60:** Stichprobe —
  `GetViolationsScanner.cs:BuildViolationsTextAsync` 31 Z.
  (43–74), `FormatReport` 36 Z. (100–135), `AppendSection` 24 Z.
  (143–166) — alle unter 60. `ResolveConfig` 8 Z.
  (`McpServerCommand.cs:72–79`) ✓.
- [ ] **`MaxMethodParameterCount` ≤ 4:** `McpCodeGraphServer.cs:155
  TryApplyContentChange` hat 5 Parameter — **vorbestehend**, bereits
  in `tech-debt.md` TD-007 dokumentiert (Override
  `MaxMethodParameterCountForNonPublic: 6`). Kein neuer Verstoß
  in dieser Einheit (A5: bereits committeter Code wird nicht
  ungefragt erneut angefasst).
- [ ] **`AiNetLinterRichtlinien.mdc` §1 (Einfachheit vor Abstraktion):**
  `get_violations` ist konsequent dünn um `LinterEngine` gewickelt
  (kein Neubau einer Lint-Loop, `GetViolationsScanner.cs:56–62`).
- [ ] **`AiNetLinterRichtlinien.mdc` §2 (kein DI, kein ALC):** Tool-
  Registrierung per Delegate-Closure
  (`AnalysisToolRegistrations.cs:28–30`, identisches Muster wie
  `FileStructureToolRegistrations.cs:28–37`).
- [ ] **`AiNetLinterRichtlinien.mdc` §5 (Result-Pattern, kein
  `dynamic`/leeres `catch`):** defensiver `try/catch` im Scanner
  ruft `LinterErrorFormatter.Format(...)` und liefert Text zurück
  (`GetViolationsScanner.cs:64–71`), kein rethrow, kein leerer
  catch. `OperationCanceledException` korrekt ausgenommen
  (Shutdown-Schutz).
- [ ] **Konvention `dotnet build` ohne Warnungen:** `state.md`
  Zeile 81 dokumentiert 0 Warnungen. Plausibilitäts-Stichprobe
  reicht (kein Re-Build zwingend).

### 3 — Logische Korrektheit

- [ ] **Disk-Cache-Bypass verifiziert:** `LinterEngine.RunAsync(solution,
  noCache: true, cacheTtlMinutes: 0, ct)` ist der korrekte
  Aufrufer-Pfad. `step-result.md` Abschnitt "Cache-Bypass-
  Verifikation" (Zeile "Cache-Bypass-Verifikation (DoD-Pflicht)")
  dokumentiert: nach `GetViolations`-Test-Filter **0** Cache-Files
  → Pflicht erfüllt, Nachweis erbracht. **Plausibilitäts-Stichprobe
  reicht**, kein Re-Run.
- [ ] **Cache-Existenz als Vorbehalt (vom Orchestrator gesetzt):**
  Die in `state.md` Zeile 84–86 + 50–55 + `bin/.../cache/` nach
  vollem `dotnet test` existierenden 6 Cache-Files stammen aus
  pre-existing Tests (`LinterEngineCacheTests` /
  `StaticTestSentinelExemptionTests`), **nicht** aus
  `get_violations` — `step-result.md` "Caveat" dokumentiert das
  explizit. Der Kritiker darf das **nicht** als Step-Regress
  werten.
- [ ] **Scope-Filter-Semantik:** case-insensitive `Contains` auf
  `projectName` **oder** `Path.GetRelativePath(solutionDir, filePath)`
  (`GetViolationsScanner.cs:91–98`) — bewusst vereinfacht (kein
  echtes C#-Namespace-Parsing), konsistent mit `get_hotspots`.
  Diese Vereinfachung steht im `step-010`-Plan und ist keine
  versteckte Lücke. Tests in
  `GetViolationsToolTests.cs:42–68` decken Treffer- und Leermenge-Pfad ab.
- [ ] **Markdown-Formatierung:** `AppendSection` baut
  `| Datei | Zeile | Regel | Details |` (Z. 156), Pfad
  solution-relativ mit Forward-Slashes (Z. 162) — LLM-lesbar,
  konsistent mit `GetHotspotsScanner`. Severity-Trennung
  Fehler/Warnungen (Z. 128–132) entspricht Konzept.
- [ ] **Thread-Sicherheit:** keine neuen Locks nötig (Plan
  ausführlich dokumentiert: `McpCodeGraphServer.GetCurrentSolution`
  lockt bereits, `LinterEngine` pro-call `new`, Roslyn-`Solution`
  immutable, parallele Lese-Zugriffe auf `Document`s erlaubt).
  Ein zusätzlicher Test für parallele Aufrufe ist **nicht** im
  Plan und **nicht** erforderlich (A5: keine Tests über das
  Angeforderte hinaus).
- [ ] **Tests echt, nicht Pseudo-Coverage:** Stichprobe
  `ExecuteAsync_LoadedSolutionNoScopeFilter_ReturnsViolationForKnownFixture`
  (`GetViolationsToolTests.cs:26–39`) — Assertion auf
  `"ViolationTrigger"`-Substring + Header-String. Würde bei
  Entfernen von `ViolationTrigger.cs` aus der Fixture rot
  (kein `"ViolationTrigger"` im Output). `FormatsViolationsAsMarkdownTable`
  prüft Markdown-Header — würde bei nicht-Markdown-Output rot.
  → Echte Tests, nicht Implementierungs-Nachsprecher.
- [ ] **A3-Fehlschlag-Nachweis (vom ursprünglichen Coder erbracht):**
  siehe "Klare Aussage zu A3" weiter unten.

### 4 — Konzept-Treue (`konzept.md`)

- [ ] **Muss-Haven "`get_violations` umgeht den bestehenden
  Disk-Cache"** (`konzept.md` Zeile 175–183) ist umgesetzt:
  `LinterEngine.RunAsync(..., noCache: true, ...)` —
  verifiziert durch "Cache-Bypass-Verifikation" im
  `step-result.md`. Begründung im Scanner-XMLDoc
  (`GetViolationsScanner.cs:23–27`).
- [ ] **Muss-Haven "Explizite Scope-Kommunikation"** (C#-only in
  `description`): `AnalysisToolRegistrations.cs:34–39` nennt
  explizit ".cs-Dateien, keine .js/.razor/.xaml/.html/.css-
  Dateien" + "Kein Disk-Cache" + "Optionaler scopeFilter" —
  dreifach erfüllt.
- [ ] **Muss-Haben "Thread-sicherer Zugriff":** siehe Ebene 3.
- [ ] **Muss-Haven "Dogfooding pro Tool-Step":** `step-result.md`
  Abschnitt "Dogfooding" dokumentiert Ad-hoc-Aufruf gegen
  `AiNetLinter.slnx`, 0 Violations, konsistent mit CLI-Lauf
  (`ainetlinter --config rules.json --path .` → 0 Violations).
  **Plausibilitäts-Stichprobe des dokumentierten Aufrufs reicht**
  (vom Orchestrator gesetzt — kein Re-Run erforderlich).
- [ ] **Tool-Set-Zeile in `konzept.md` Tabelle Z. 550:** `get_violations`
  steht auf "codiert, Review offen" — **nicht** "fertig". Die
  Verschiebung auf "fertig" ist Sache des Nutzers (A7 — Konzept
  nicht selbst anpassen) **nach** `approved` dieser Einheit.
- [ ] **Konzept-Update in `tasks/codegraph-mcp-next/Konzept.md`**
  (mit im Commit): außerhalb dieses Task-Scopes, `state.md`
  Zeile 58–59. **Kein Konzept-Verstoß** der Review-Einheit, nur
  notieren, falls der Kritiker darauf stößt.

## Bezug zu Projektregeln (Kurzgrund pro Datei)

| Regel | Datei | Kurzgrund |
|:---|:---|:---|
| `AiNetLinter.mdc#AIContextFootprint` | `rules.json` (PathOverrides) | Pull-in-Regression `FindReferencesTool`/`FindSymbolTool` durch `Config`-Property in `McpCodeGraphServer`. |
| `AiNetLinter.mdc#EnforceNullableEnable` | alle 7 modifizierten/neuen `.cs`-Dateien | `#nullable enable` am Dateianfang. |
| `AiNetLinter.mdc#EnforceSealedClasses` | `McpCodeGraphServer.cs:22`, `GetViolationsToolTests.cs:11` | Konkrete Klassen explizit `sealed`; Tool-Klassen `internal static` (entfällt). |
| `AiNetLinter.mdc#MaxLineCount`/`MaxMethodLineCount` | neue Klassen | Stichprobe ausreichend; alle unter Limit. |
| `AiNetLinterRichtlinien.mdc#§1` | `GetViolationsScanner.cs:56–62` | "Einfachheit vor Abstraktion" — `LinterEngine` wiederverwendet, keine eigene Lint-Loop. |
| `AiNetLinterRichtlinien.mdc#§2` | `AnalysisToolRegistrations.cs:28–30` | Delegate-Closure statt DI-Container. |
| `AiNetLinterRichtlinien.mdc#§5` | `GetViolationsScanner.cs:64–71` | Result-Pattern: `LinterErrorFormatter.Format(...)` statt `throw`. |
| `AiNetLinterRichtlinien.mdc#§3` | `state.md` Z. 81–82 | PowerShell-konformer `dotnet build`/`dotnet test` (Baseline-Stand). |
| `AiNetLinterRichtlinien.mdc#§4` (Update-Pflicht) | — | **Konzept-Befreiung explizit** im `step-010`-Plan (Z. "Rules-Refs"): `Docs/ROADMAP.md`-Sync erst bei EPIC-08. Kein Verstoß in dieser Einheit. |

## Klare Aussage — kein neues Test-Material nötig (Begründung)

Diese Einheit ist **kein Coder-Schritt** — sie erzeugt weder Code
noch Tests. Die fünf neuen Unit-Tests in `GetViolationsToolTests.cs`
plus der eine E2E-Test plus die zwei `ResolveConfig`-Tests sind
bereits im Commit `e63176d` enthalten und laut `state.md` Zeile 49
**1088/1088 grün**. A3 ("ein neuer Test muss nachweislich
fehlschlagen können, wenn man die Änderung wegnimmt") ist eine
**Coder-Pflicht** im ursprünglichen Schritt, nicht eine
Review-Pflicht — der A3-Nachweis ist im `step-result.md` Abschnitt
"Cache-Bypass-Verifikation" enthalten (Test-Filter-Lauf, 0 Cache-
Files) und für die inhaltliche Korrektheit reicht die
Plausibilitäts-Bewertung der Tests:

- `GetViolationsToolTests.cs:26–39` testet die **deterministische
  Fixture-Violation** (`ViolationTrigger.cs:6` ohne `sealed`) — bei
  Entfernen der Fixture-Datei oder der Determinismus-Verletzung
  wechselt der Test auf rot.
- `GetViolationsToolTests.cs:71–83` testet die **Markdown-Tabellen-
  Struktur** — bei nicht-Markdown-Output wechselt der Test auf rot.
- `GetViolationsToolTests.cs:57–68` testet die **explizite
  Leermengen-Meldung** — bei fehlender Fallunterscheidung rot.
- `McpServerCommandTests.cs:217–…` testet die **echte Tool-Auflistung**
  via Subprozess — bei nicht-registriertem Tool rot.

Der Kritiker bewertet die Plausibilität dieser Tests gegen die
vier Ebenen aus `agents/kritiker.md` — er **führt sie nicht
routinemäßig nach** (A3, Zeile 66–69: "Selbst ausführen nur, um
einen eigenen konkreten Verdacht zu belegen, und dann gezielt
statt voll").

## Wichtige Hinweise für den Kritiker

- **Cache-Existenz nach `dotnet test` ist kein Step-Regress** —
  vorbestehende Tests (`LinterEngineCacheTests`,
  `StaticTestSentinelExemptionTests`) schreiben den Disk-Cache
  weiterhin. Wenn der Kritiker das als Issue wertet, ist das
  außerhalb des Scopes dieser Einheit (→ Tech-Debt-Kandidat, nicht
  `issues`-Verdict).
- **Dogfooding ist plausibel, nicht Re-Run.** Der dokumentierte
  Ad-hoc-Aufruf gegen `AiNetLinter.slnx` (0 Violations, konsistent
  mit CLI) reicht — der Orchestrator hat explizit verfügt, dass
  die Plausibilität des dokumentierten Aufrufs für eine Review-
  Einheit ausreicht.
- **Commit-Format-Unschärfe (`e63176d` ist nicht Conventional-Commit-
  konform)** ist im `step-result.md` Abschnitt "Bekannte Unschärfen"
  dokumentiert und laut A4 ausgeschlossen (kein History-Rewrite).
  Kein `issues`-Punkt für den Kritiker.
- **`tasks/codegraph-mcp-next/Konzept.md` im selben Commit** ist
  Konzept-Pflege, nicht Code-Stand. Außerhalb dieses Task-Scopes
  (`state.md` Zeile 58–59).
- **`GetViolationsToolTests.cs` ohne `#nullable enable`** ist
  vorbestehend (`McpServerCommandTests.cs` ebenfalls) — kein
  Verstoß in dieser Einheit (kein neuer Code).
- **`TD-008` (PathOverrides für `FindReferencesTool`/`FindSymbolTool`):**
  ist im Commit `e63176d` umgesetzt worden, **aber** `tech-debt.md`
  Zeile 36 datiert den Eintrag auf Stand vor diesem Commit. Der
  Kritiker darf den Eintrag **nicht** als "schon gefixt" heraus-
  nehmen — die `PathOverrides` sind im Konzept bewusst eine
  *Pragmatik*-Lösung, der TD-008-Eintrag dokumentiert die
  *strukturelle* Schuld (mögliche `ILinterEngineConfig`-Kapselung).
  Nur der Nutzer entscheidet, ob der Eintrag inhaltlich obsolet
  geworden ist.

## Konkrete Belege für den Kritiker (`file:line` Schnellzugriff)

- `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs:22–34`
- `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs:33–171`
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs:19–41`
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs:19–62`
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:42–44`
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs:30–71`
- `src/AiNetLinter/Commands/McpServerCommand.cs:36, 72–79`
- `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs:13–83`
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs:134, 217, 433, 454`
- `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/ViolationTrigger.cs:1–11`
- `rules.json:411–420` (PathOverrides)
- Plan-Quelle: `git show 7474226:tasks/codegraph-mcp/step-010/step-plan.md`
- Result-Quelle: `git show 7474226:tasks/codegraph-mcp/step-010/step-result.md`
