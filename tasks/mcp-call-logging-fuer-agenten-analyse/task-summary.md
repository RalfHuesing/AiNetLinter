---
task: mcp-call-logging-fuer-agenten-analyse
completed_at: 2026-08-05T15:25:00+02:00
final_status: done
total_iterations: 1
total_commits: 25
total_epics: 4
total_tech_debt_entries: 3
---

# Task Summary: mcp-call-logging-fuer-agenten-analyse

## Ergebnis

Der Task hat das in `konzept.md` (status: `ready`) beschriebene Feature
vollständig umgesetzt: `--mcp-log` aktiviert jetzt einen vorhersagbaren
Default-Pfad (`<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`),
bricht bei nicht auflösbarer Solution sauber mit Exit 1 ab, und
unbehandelte Tool-Handler-Exceptions werden in derselben JSONL-Datei als
strukturierte Error-Zeilen (`level=error`, `error_type`,
`error_message`, `stack_trace` mit 4 KB Cap) persistiert. Die
zentrale `McpCallLog.ExecuteCallAsync`-Hülle bündelt das
Call-/Error-Logging für alle 10 Tool-Wrapper ohne per-Tool-`try/catch`.
Doku (agent-api.md, configuration.md, ROADMAP.md) und CLI-Description
sind mit der Implementierung konsistent. 1279/1279 Tests grün, Build
0/0, Linter-Dogfooding 0 Violations. Konzept DoD 1–6 vollständig
erfüllt; DoD 7 (`konzept.md`-Status `ready`) ist bereits vor dem
Loop gesetzt (Frontmatter `status: ready`).

## Roadmap-Status

Alle **4 Epics** aus `roadmap.md` sind abgehakt:

- [x] **EPIC-01** (Default-Pfad-Konvention + harter Error-Exit) → step-001, `approved`
- [x] **EPIC-02** (`RecordError` mit Schema, Lock, 4 KB Stack-Trace-Cap) → step-002, `approved`
- [x] **EPIC-03** (Error-Hook im Lifecycle via `ExecuteCallAsync` + 10 Tool-Wrapper-Refactor) → step-003, `approved`
- [x] **EPIC-04** (Doku-Sync + End-to-End-Verifikation) → step-004 + fix-01, `approved`

Letzter Status-Commit: `3885afb chore(task): step-004 finaler Commit-Eintrag in task-state`.

## Steps-Übersicht

| Step | Epic | Status | Title | Code-Commit | Review-Commit | Notiz |
|------|------|--------|-------|-------------|---------------|-------|
| step-001 | EPIC-01 | done | Default-Pfad-Konvention + harter Error-Exit bei fehlender Solution | `1cefdce0` | `b87ee95` | approved |
| step-002 | EPIC-02 | done | `McpCallLog.RecordError` (Schema, Lock, 4 KB Stack-Trace-Cap) | `c3fe3c5f` | `b2088d2` | approved (nach User-Workaround PathOverride-Bumps `17bda1d`) |
| step-003 | EPIC-03 | done | `McpCallLog.ExecuteCallAsync` Shared-Helper + 10 Tool-Wrapper-Refactor | `d1642df4` | `2d6d687` | approved |
| step-004 | EPIC-04 | done | Doku-Sammel-Step (6 Items) + finaler Test-Volllauf | `fc550f2` | `e0b6ac2` | issues (2 MAJOR), gefixt in fix-01 |
| step-004/fix-01 | EPIC-04 | done | `error_type`-Schema-Doku angleichen + Test-Count 5/5 → 9/9 | `d91438a` | `e0b6ac2` | approved |

## Globale Audit-Befunde (Kritiker, Modus `global`)

### Konzept erfüllt?

**Muss-Haben 1–5 (alle erfüllt):**

- **MH 1 (Default-Pfad-Konvention):** `McpServerCommand.BuildDefaultLogPath` konstruiert `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` (verifiziert `McpServerCommand.cs:113-130`). Verzeichnisse werden via `McpCallLog`-Konstruktor (Z. 38-39) automatisch angelegt.
- **MH 2 (Kein Fallback, harter Abbruch):** User-Korrektur 2026-08-05 sauber umgesetzt. `BuildDefaultLogPath` liefert `null` + `RESOURCE_NOT_FOUND`-Meldung auf stderr, wenn Solution-Pfad leer/whitespace ist oder `Path.GetFileNameWithoutExtension` einen leeren String liefert (McpServerCommand.cs:119-126). `RunAsync` macht bei `wasOptedIn && callLog is null` `return 1` (Z. 68). Test `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull` (Z. 115-128) verifiziert das.
- **MH 3 (Datum lokal):** `DateTime.Now.ToString("yyyy-MM-dd")` (McpServerCommand.cs:128) — lokal, nicht UTC. Test `BuildDefaultLogPath_DateIsLocal` (Z. 148-164) verifiziert das.
- **MH 4 (`McpCallLog.RecordError`):** implementiert in `McpCallLog.cs:99-134`. Schema: `level=error`, `error_type = exception.GetType().Name` (Z. 121, ohne Namespace — Konsistenz zu Test-Assertions), `error_message = exception.Message` (Z. 122), `stack_trace` mit 4 KB Cap via `string.Concat(span, marker)` (Z. 110-113). Selber `_writeLock` wie `RecordEnd` (Z. 127). `_entryCount++` (Z. 132) verhindert Auto-Delete leerer Datei nach Error-only-Eintrag.
- **MH 5 (Error-Hook im Lifecycle):** `McpCallLog.ExecuteCallAsync` (Z. 147-167) ist die zentrale try/catch-Hülle; ruft `StartRecording`, awaited das Tool-Delegate, schließt Scope bei Erfolg, persistiert bei nicht-OCE-Exception `RecordError` + `throw`, filtert `OperationCanceledException` via `when`-Klausel. 10 Tool-Handler in den 4 `*ToolRegistrations`-Klassen (`AnalysisToolRegistrations` 2, `FileStructureToolRegistrations` 3, `SymbolBodyToolRegistrations` 1, `SymbolGraphToolRegistrations` 4) delegieren 1:1 an den Helper — verifiziert per `git grep -c ExecuteCallAsync` (10 Treffer in den 4 Registrar-Dateien) und Inspektion (`FileStructureToolRegistrations.cs:51-52, 78-79, 106-107` exemplarisch).
- **MH „Doku":** `Docs/agent-api.md:311-356` (Default-Pfad + Error-Schema-Beispiel mit `get_file_skeleton`), `Docs/configuration.md:1087` (CLI-Option-Eintrag mit Default-Pfad + Error-Schema-Verweis), `Docs/ROADMAP.md:477-482` (EPIC-09-Meilenstein-Eintrag in der `## MCP-Codegraph-Server`-Sektion) und `src/AiNetLinter/Cli/CliOptionFactory.cs:232` (`--mcp-log`-Description) sind alle mit der Implementierung konsistent (nach fix-01).

**DoD 1–7 (alle 6 messbaren DoD erfüllt; DoD 7 User-Aufgabe):**

- **DoD 1:** Default-Pfad-Konvention umgesetzt. Test `TryCreateCallLog_WhitespacePath_CreatesDefaultLog` (Z. 87-113) beweist den Konstruktions-Pfad. DoD 1 hat vier Fälle — alle vier sind dokumentiert (agent-api.md, configuration.md, CLI-Description) und in Tests abgedeckt (`PathNotSet_ReturnsNull`, `WhitespacePath_CreatesDefaultLog`, `WhitespacePathNoSolution_WritesErrorAndReturnsNull`).
- **DoD 2:** Error-Sink in `McpCallLog` mit korrektem Schema. Test `ExecuteCallAsync_ThrowingCall_WritesErrorEntryAndRethrows` beweist Tool-Exception → JSONL-Zeile mit `level=error`, `error_type=TestException`, `error_message` enthält den Race-String, `stack_trace` enthält die ersten Stack-Zeilen, `tool=get_file_skeleton`, `args` durchgereicht — exakt der DoD-2-Use-Case (Konzept DoD 2 zitiert „simuliertes Hot-Reload-Race in get_file_skeleton").
- **DoD 3:** Lock-Reihenfolge. Tests `RecordError_AfterRecordEnd_PreservesOrderInJsonl` und `RecordError_BeforeRecordEnd_PreservesOrderInJsonl` beweisen sequenzielle Reihenfolge in beide Richtungen; `RecordError_ParallelCallsDoNotInterleaveJsonLines` (50 Pairs) und `ExecuteCallAsync_ParallelThrowingCallsDoNotInterleaveJsonLines` (50 Tasks) beweisen atomares `_writeLock` über mehrere Threads (validiert durch `JsonDocument.Parse` auf jeder Zeile).
- **DoD 4:** Stack-Trace-Cap funktioniert. Test `RecordError_StackTraceExceeds4KB_TruncatesToCap` speist 100 KB Input ein und assertet `stack_trace.Length <= 4096` + `EndsWith("...")`. Konzept-Definition: 4096 Chars (analog zum `MaxArgsLength = 200` Char-Cap in `RecordEnd`), im 100KB-ASCII-Test exakt 4096 Bytes = 4 KB.
- **DoD 5:** Test-Stabilität. Bestehende 10 `McpCallLogTests` (vor step-002) und 5 `McpCallLogTests` (vor step-001) sind unverändert grün (verifiziert: 14/14 in McpCallLogTests, davon 10 alt + 4 ExecuteCallAsync neu). `McpServerCommandCallLogTests` 9/9 grün (1 `PathNotSet` + 2 RelativePath/AbsolutePath 4-Param + 4 neue Default-Pfad + 2 unveränderte `ResolveMcpLogPath_*` — passt zu fix-01-Korrektur). `dotnet test` Volllauf 1279/1279 grün. Build 0/0. Keine neuen Compiler-Warnungen.
- **DoD 6:** Doku synchron. agent-api.md (item-01), configuration.md (item-02), ROADMAP.md (item-03, EPIC-09 statt EPIC-20), `tasks/.../roadmap.md:61` TD-001-Korrektur (item-04), CLI-Description (item-05). Nach fix-01: `error_type`-Schema-Doku an Code angepasst (exception type name ohne Namespace), Test-Counts 9/9 statt 5/5 in Step-Doku.
- **DoD 7:** `konzept.md` Status `ready` — Frontmatter zeigt bereits `status: ready` mit `revision_history` (User-Bestätigung 2026-08-05). Diese Bestätigung erfolgte vor dem Drift-Loop, nicht in step-004.

**Non-Goals respektiert:**

- Kein Hot-Reload-Hardening (kein Eingriff in `McpCodeGraphServerRefresh.cs:181-205`).
- Kein Serilog / `Microsoft.Extensions.Logging` — direkter `StreamWriter` analog `RecordEnd`.
- Kein DI-Container — `callLog` wird statisch über `McpServerOptionsFactory.Create(mcpState, callLog)` durchgereicht.
- Keine Log-Cleanup-Strategie (keine Rotation, kein Max-Alter).
- Kein `startup.json` / stderr-Mirror.
- Keine Opt-in→Opt-out-Umkehr — Opt-in explizit beibehalten (User-Wahl).

### Seiteneffekte / Regressionen

**Build:** `0 Warnung(en), 0 Fehler` (frisch verifiziert, Dauer 1.92 s).
**Test-Volllauf:** `1279/1279 grün, 0 Failures, 0 Errors`, Dauer 1 m 55 s (frisch verifiziert).
**Hund-Test:** `CliIntegrationTests` 29/29 grün in 56 s, inkl. `RunLinterCli_OnWholeSolution_ReturnsSuccess` (keine Lint-Regression auf den 5 McpCallLog-Konsumenten trotz +33 Z. in `McpCallLog.cs` aus step-003 — die PathOverride-Puffer aus step-002-Workaround A reichen komfortabel).
**Linter-Dogfooding:** `dotnet run --project src/AiNetLinter -- --config rules.json --path .` → `# Run: 2026-08-05 15:24:49 OK` (0 Violations, frisch verifiziert).

**Keine ungewollten Seiteneffekte** auf andere Projektteile. Die transitive `AIContextFootprint`-Welle in den 5 McpCallLog-Konsumenten (TD-002, mittelfristiges Tech-Debt) ist durch PathOverride-Bumps aufgefangen (Buffer 201-208 Z. pro Datei nach step-002; reicht für 12-18 weitere Wachstumseinheiten à +10-15 Z.).

### Rules-Konformität (Stichproben)

Drei zufällig gewählte Produktiv-Dateien geprüft:

1. **`src/AiNetLinter/Cli/CliOptionFactory.cs:230-233`** — `--mcp-log`-Option. ASCII-only Description (passt zum Datei-Stil, der Umlaute transliteriert), `ArgumentArity.ZeroOrOne` (aus step-001), keine Task-/Step-/EPIC-/TD-Verweise im Description-Text. `internal static`, Returntyp `Option<string?>` — innerhalb der Konvention.
2. **`src/AiNetLinter/Commands/McpServerCommand.cs:32-145`** — `RunAsync` (49 Z., unter `MaxMethodLineCount=60`) + `TryCreateCallLog` (12 Z., 4 Parameter — am Limit `MaxMethodParameterCount=4`, legitimiert durch vorherigen Plan). `BuildDefaultLogPath` 17 Z., 3 Parameter. `using System.Reflection;` korrekt ergänzt (Z. 7). Keine Task-/Step-/EPIC-/TD-Verweise im Code (verifiziert via `git grep`). XML-Doc-Kommentare beschreiben Was/Wie, nicht den Refactoring-Anlass. Datei enthält Umlaute in Kommentaren (z. B. `läßt`, `scheidet`, `stiller`) — `EnforceAsciiIdentifiers` betrifft nur Identifier, das ist der etablierte Datei-Stil (step-001-Review hat die ASCII-Transliteration explizit als „bewusster Stilgriff" markiert).
3. **`src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs:39-114`** — 3 Tool-Handler (`get_file_skeleton`, `get_index_scope`, `get_hotspots`), alle 1:1 auf `ExecuteCallAsync` umgestellt. `if (callLog is null) { return await ...; }` für Fast-Path bleibt erhalten. Tool-Name/Args-Strings 1:1 aus Closure-Lokalen. Keine Verletzung von `EnforceNoSilentCatch`/`BanAsyncVoid`/`EnforceAsciiIdentifiers`. Netto −6 Z. gegenüber step-001-Stand (3 Wrapper × −2 Z.), passt zur step-003-Voraussage.

**Zusatzcheck:** `git grep -nE "step-00[1-4]|EPIC-0[1-4]|TD-00[0-9]"` über `src/AiNetLinter/Mcp/McpCallLog.cs`, `src/AiNetLinter/Commands/McpServerCommand.cs`, `src/AiNetLinter/Cli/CliOptionFactory.cs` liefert **0 Treffer** — Clean-Code-Kommentar-Politik aus `AiNetLinterRichtlinien.mdc` §5 konsequent eingehalten.

## Tech-Debt-Zusammenfassung

Pointer-Volltext in `tech-debt.md`. Übersicht:

- **Hoch:** 0 Einträge
- **Mittel:** 1 Eintrag — `TD-002` (McpCallLog-Wachstum treibt 5 Konsumenten über `AIContextFootprint`-PathOverrides; ~200 Z. Puffer pro Datei, reicht für ~12-18 weitere Wachstumseinheiten)
- **Niedrig:** 2 Einträge — `TD-001` (Roadmap-Inkonsistenz Test-Scope-Notiz in `tasks/.../roadmap.md:61`, in step-004 item-04 vorgenommen — war aber die falsche Stelle, eigentlich korrekt adressiert, die Notiz ist seit item-04 mit der korrekten Schritt-001-Test-Scope-Lesart aktualisiert) und **`TD-003` (NEU, durch globalen Audit angelegt)**: `Docs/ROADMAP.md:482` EPIC-09-Eintrag zählt „5 Tests in `McpServerCommandCallLogTests`" — die Aufzählung sagt 1+3+4=8, real sind es 9 (die 2 unveränderten `ResolveMcpLogPath_*`-Tests fehlen). Doku-Inkonsistenz, vom step-004-Planer in item-03 nicht bemerkt, vom fix-01-Planer explizit ausgeschlossen, vom fix-01-Reviewer für den globalen Audit vorgemerkt.

**Hinweis aus Nutzersicht:** TD-002 ist die einzige mittelfristige Architektur-Frage (`MetricsConfig` schlanker machen oder `McpCallLog` partial-splitten). TD-001 und TD-003 sind reine Doku-Kosmetik mit 1-Zeilen-Fix-Potenzial. Keiner dieser Einträge ist ein Blocker.

## Offene Punkte

- [ ] **TD-003** (`Docs/ROADMAP.md:482`): 1 Zeile an reale Test-Counts angleichen. Optional, 1 Zeile, kein Risiko. Kandidat für nächsten Doku-Pass.
- [ ] **TD-002** (PathOverride-Wellen): Monitoring-relevant. Vor EPIC-03-Entscheidung (jetzt überholt — EPIC-03 ist abgeschlossen) bzw. vor künftigen McpCallLog-Wachstumsschritten ist die mittelfristige Architektur-Option (`MetricsConfig` schlanker oder `McpCallLog` partial-splitten) zu prüfen. Aktuelle Buffer komfortabel, kein Eilbedarf.
- [ ] **Konzept DoD 5 (Z. 138) „4 Call-Tests"-Zahl-Diskrepanz:** Konzept sagt „4 Call-Tests bleiben unverändert grün" als Baseline, real sind 10 in `McpCallLogTests` (vor step-002). Inhaltlich weiterhin erfüllt (alle bestehenden Tests grün), aber die Zahl „4" im Konzept ist historisch falsch. Out-of-scope für diesen Task, gehört in eine Konzept-Korrektur.
- [ ] **`McpCallLog.LogPath` (internal-Sichtbarkeit):** step-001-Reviewer hatte Sichtbarkeit als Re-Evaluationspunkt für EPIC-04 markiert. Aktuelle Konsumenten (nur `McpServerCommand.cs:67` und `McpCallLogTests.cs`) liegen im selben Assembly, `internal` ist weiterhin korrekt. Kein Action-Item.
- [ ] **Hot-Reload-Hardening** (`McpCodeGraphServerRefresh.cs:181-205` vermutetes Race): bewusst out-of-scope in `konzept.md §"Bewusst out of scope"`, eigenes Konzept unter `tasks/mcp-hot-reload-hardening/` vorgesehen. Das Default-Logging macht das Symptom sichtbar, behebt aber nicht die Ursache.

## Empfehlungen

1. **Task kann auf `done` gesetzt werden.** Alle Epics abgehakt, alle MAJOR-Findings gefixt, keine offenen Blocker. Verdict dieses Audits: `done`.
2. **TD-003 (1-Zeilen-Doku-Korrektur)** kann jederzeit mit minimalem Aufwand nachgezogen werden, z. B. im nächsten Doku-Sammel-Pass zusammen mit anderen Roadmaps. Kein eigenes Epic nötig, schlichte 1-Zeilen-Korrektur durch den nächsten Bearbeiter.
3. **TD-002 (PathOverride-Wellen)** ist die einzige mittelfristige Architektur-Frage. Wenn ein absehbarer weiterer McpCallLog-Wachstumsschritt ansteht (z. B. neue Tool-Registrierung oder Schema-Erweiterung), vorher entscheiden, ob die Wellen weiterhin per PathOverride-Bump aufgefangen werden oder ob `MetricsConfig`/`McpCallLog` umstrukturiert wird. Aktuell kein Eilbedarf.
4. **Vor Push / MR-Erstellung:** keine Aktion erforderlich, der finale Volllauf ist in diesem Audit frisch verifiziert (1279/1279 grün, 1 m 55 s).
5. **Push + Auto-Merge + CI-Bestätigung** ist User-Schritt; falls der User den Task hier schließt, sollte er den Branch-Stand in seinem gewohnten Workflow prüfen.

## Statistik

- **Anzahl Epics:** 4, davon abgehakt: **4** (100 %)
- **Anzahl Steps:** 4 regulär + 1 fix-01 = **5** Step-Dokumente
- **Davon approved:** **5** (alle)
- **Davon blocked:** 0 (war in step-002 zwischenzeitlich `blocked`, wurde durch User-Workaround A aufgelöst, finale Bewertung `approved`)
- **Anzahl Commits:** **25** (`git log --oneline -25` im Repo-Root, alle mit Pflicht-Suffix `[mcp-call-logging-fuer-agenten-analyse]` und Pflicht-Trailer `Refs: tasks/.../step-NNN`): step-001 (3) + step-002 (8, inkl. Workaround `17bda1d`) + step-003 (5) + step-004 (6, inkl. planen) + step-004/fix-01 (3) + finaler Status-Commit (`3885afb`) = 25
- **Anzahl Tech-Debt-Einträge:** **3** (TD-001 niedrig, TD-002 mittel, TD-003 niedrig — TD-003 neu durch globalen Audit angelegt)
- **Loop-Iterationen (Fix-Runden):** **1** / 12 (nur step-004 hat eine Fix-Runde gebraucht, andere Steps brauchten keine)
- **Laufzeit:** gestartet `2026-08-05T11:53:13+02:00`, abgeschlossen `2026-08-05T15:25:00+02:00` → ca. **3 h 32 min** (4 Steps + 1 Fix-Runde, inklusive User-Entscheidungs-Latenz für step-002-Workaround A)

**VERDICT: done**
