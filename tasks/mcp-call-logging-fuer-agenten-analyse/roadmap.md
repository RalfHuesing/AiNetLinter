---
status: active  # active | done
task: mcp-call-logging-fuer-agenten-analyse
derived_from: konzept.md
created_at: 2026-08-05T12:00:00+02:00
last_updated: 2026-08-05T15:45:00+02:00
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: mcp-call-logging-fuer-agenten-analyse

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../../.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

<Aus dem Projekt abgeleitet, einmalig hier (nicht pro Step neu):>

- **Build-Command:** `dotnet build` (Solution `AiNetLinter.slnx`, csproj-Target `net10.0`; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` aktiv)
- **Test-Command:** `dotnet test` (Volllauf vor Task-Abschluss Pflicht, siehe `AGENTS.md` §2). Schnelle Iteration während Entwicklung: `dotnet test --filter Category=Unit`
- **Lint-Command:** nicht extern — Lint ist das Tool selbst (`AiNetLinter --check` / Pre-Commit-Lint, siehe `AGENTS.md` §3)
- **Test-Logging:** `.runsettings` schreibt automatisch `TestResults/latest.trx`; bei roten Tests dort Diagnose-Output lesen statt erneuten Lauf
- **Code-Style-Kurzfassung:**
  - C#-Kurz-Stil: `sealed` für konkrete Klassen, Methoden ≤60 Zeilen (≤150 mit Compound-Suppression), Dateien ≤500 Zeilen, `#nullable enable` am Dateianfang (Pflicht).
  - Grenzwerte (Produktion): Cyclomatic ≤12, Cognitive ≤15, `MaxConstructorDependencies` ≤5, `MaxPublicMembersPerType` ≤15, `AIContextFootprint` ≤2500.
  - Verben: `Result<T>`-Pattern bevorzugt, Exceptions nur für exogene Ausnahmefälle; `dynamic` verboten; `out` nur in `Try*`-Methoden.
  - Kommentare: sparsam (Clean Code), keine Task-/Step-/EPIC-/TD-Verweise (Ordner werden gelöscht → Verweis wertlos), keine redundanten Nacherzählungen sprechender Namen, keine Refactoring-Historie.
  - Architektur: monolithisches CLI, **kein** DI-Container, **kein** `AssemblyLoadContext`, statische Kompilierung.
  - Agenten-Stil: Sparring-Modus für Vorhaben > Trivial, prägnante Antworten, Commit-Vorschlag-Block Pflicht am Ende jeder Änderungs-Antwort.
- **Commit-Konventionen:**
  - Conventional Commits **auf Deutsch, imperativ** (z. B. `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`)
  - Subject ≤72 Zeichen
  - **Pflicht-Suffix `[mcp-call-logging-fuer-agenten-analyse]`** an jedem Commit dieses Tasks (auch an Code-Commits, nicht nur an Doku-Commits) — siehe `spec.md` §10.3
  - Body-Trailer: `Refs: <task-dir>/step-NNN` (siehe `skills/coder/SKILL.md` Schritt 5)
  - Mehrere kleine Commits pro Step statt einem großen: Code-Commit → Doku-Commit → Planungs-Commit → Review-Commit (Details `spec.md` §10.3)
- **Shell:** PowerShell 7, kein Bash, keine `sed -i`-Inline-Edits, Git immer mit `--no-pager`
- **Nicht im Workflow:** Push übernimmt der Nutzer; der Loop macht nur lokale Commits.

## Regel-Index

<Ein Eintrag pro Datei in `<rules_dir>/**` — **Kurzbeschreibung, kein
Volltext**. Zweck: Der Step-Modus-Planer ist pro Aufruf eine frische,
isolierte Session ohne Erinnerung an diesen Roadmap-Modus-Aufruf — er
kann `<rules_dir>/**` nicht bei jedem Step neu komplett lesen (Kosten),
liest aber diesen Index (steht ja schon hier in `roadmap.md`) und dann
gezielt nur die 1-2 Dateien, die zum aktuellen Step passen, siehe
`../spec.md` §7.2 / `../skills/planer/SKILL.md` Schritt 4a. Wird laufend
gepflegt: fällt beim Roadmap-Abgleich (Schritt 1, Step-Modus) eine neue,
im Index fehlende Regeldatei auf, wird sie hier ergänzt.>

- `.agents/rules/AiNetLinterRichtlinien.mdc` — manuell gepflegte Architektur-Leitplanken, Workflow-Regeln und Agenten-Verhaltenscodex (monolithisch/kein DI, Windows/PowerShell-Constraints, xUnit-v3-Pflicht, Zero-Warning-Direktive, Result-Pattern-Bevorzugung, Clean-Code-Kommentar-Politik inkl. Verbot von Task-/TD-/EPIC-Verweisen, Commit-Vorschlag-Pflicht).
- `.agents/rules/AiNetLinter.mdc` — auto-generiert aus `rules.json` (AiNetLinter 1.0.79): konkrete numerische Linter-Grenzwerte (MaxLineCount 500, MaxMethodLineCount 60, MaxMethodParameterCount 4, AIContextFootprint 2500, EnforceSealedClasses, EnforceNamespaceDirectoryMapping, BanAsyncVoid, EnforceNoSilentCatch, etc.) inkl. Compound-Suppression-Tabelle und `*.Tests`-Overrides (MaxMethodLineCount 100, EnforceSealedClasses off).

## Epics

<Ein Epic = grober Cluster mehrerer Steps, kein einzelner Step. Format:>

- [x] EPIC-01: Default-Pfad-Konvention für `--mcp-log` Opt-in (→ step-001) — leerer/Whitespace-Pfad-Wert erzeugt automatisch `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`. **Kein Fallback-Pfad** (per User-Entscheidung 2026-08-05): wenn keine Solution auflösbar, bricht `--mcp-server` mit Fehlermeldung und Exit ≠ 0 ab, statt wie bisher `null` zu liefern. Bezieht sich auf Muss-Haven 1–3 aus `konzept.md`. Berührt `Commands/McpServerCommand.cs` (neuer `BuildDefaultLogPath`-Helper, `TryCreateCallLog`-Semantik-Umkehr, Error-Exit wenn keine Solution), `Cli/CliOptionFactory.cs:230-233` (`ArgumentArity.ZeroOrOne` plus Description anpassen) und passt die Tests in `Tests/Commands/McpServerCommandCallLogTests.cs` an: 1 obsoleter Test wird gelöscht (`TryCreateCallLog_WhitespacePath_ReturnsNull`), 3 bestehende Tests werden auf die neue 4-Parameter-Signatur umgestellt, 4 neue Tests dokumentieren Default-Pfad-Konstruktion (`TryCreateCallLog_WhitespacePath_CreatesDefaultLog`, `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull`, `BuildDefaultLogPath_WithSolution_IncludesSolutionName`, `BuildDefaultLogPath_DateIsLocal`). **Achtung für den Step-Modus-Planer:** `TryCreateCallLog_PathNotSet_ReturnsNull` (Argument `null`) bleibt inhaltlich korrekt (Flag nicht gesetzt → kein Log) und sollte **nicht** ersetzt werden.
- [x] EPIC-02: Error-Record-Methode in `McpCallLog` (→ step-002) — neue `McpCallLog.RecordError(tool, args, exception)` schreibt JSONL-Zeile mit `level=error`, `error_type` (Exception-Typ-Name), `error_message`, `stack_trace` (4 KB Cap) unter demselben `_writeLock` wie `RecordEnd` (`McpCallLog.cs:29`), damit Call- und Error-Einträge zeitlich geordnet erscheinen. Bezieht sich auf Muss-Habe 4 aus `konzept.md` (Error-Sink, Schema, Lock, Stack-Trace-Cap). Berührt `Mcp/McpCallLog.cs` (neue Methode, ggf. lokale Helper für Stack-Trace-Truncation) und `Tests/Mcp/McpCallLogTests.cs` (neue Tests: Schema-Validierung, Lock-Reihenfolge, Stack-Trace-Cap bei 100 KB Input, Interaktion mit `RecordEnd`).
- [x] EPIC-03: Error-Hook im MCP-Server-Lifecycle (→ step-003) — Tool-Wrapper in den vier `*ToolRegistrations`-Klassen (`SymbolGraphToolRegistrations`, `FileStructureToolRegistrations`, `AnalysisToolRegistrations`, `SymbolBodyToolRegistrations`) fangen unbehandelte Exceptions ab und rufen `callLog?.RecordError(tool, args, ex)`, sofern `callLog != null` (kein Overhead im Opt-out-Pfad). Bezieht sich auf Muss-Habe 5 aus `konzept.md`. **Offene Frage für den Step-Modus:** konkrete SDK-Stelle verifizieren — entweder pro-Tool-`try/catch` in den ~8 Wrappern (ggf. Refactor auf Shared-Helper zur Vermeidung von 8-facher Wiederholung) oder globaler SDK-Error-Handler auf `McpServerOptions`-Ebene (Trade-off: Tool-Name/Args-Kontext). Ebenfalls zu klären: zählt das Verhalten als „An error occurred invoking X" im Sinne von DoD 2 nur für Tool-Handler-Exceptions oder auch für Transport-/Initialize-Errors?
- [x] EPIC-04: Dokumentation synchronisieren & End-to-End-Verifikation (→ step-004, inkl. fix-01) — Aktualisierung von `Docs/agent-api.md:311-341` (Default-Pfad-Korrektur von `<solutionDir>/.mcp-log/calls.log` auf `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` plus neues Error-Schema-Beispiel), `Docs/configuration.md:1087` (Default-Wert-Beschreibung des `--mcp-log`-Eintrags), `Docs/ROADMAP.md` (Meilenstein-Eintrag) sowie abschließender `dotnet test`-Volllauf zur Verifikation von DoD 1–6. Bezieht sich auf Muss-Habe „Doku" aus `konzept.md` und DoD 5 + DoD 6. Berührt keine Produktiv-Logik mehr, ausschließlich Doku und Verifikation — eignet sich ggf. als finaler `step_type: batch`-Sammel-Step innerhalb dieses Epics.

<Begründungen gehören **an das Epic**, nicht in eine Liste darunter: Ein
Epic, das der Planer nachträglich ergänzt hat, trägt den Grund in seiner
eigenen Zeile, z. B. „- [ ] EPIC-03: ... (Muss-Haben aus `konzept.md`
§X, ohne Entsprechung in der ursprünglichen Roadmap — erkannt in
step-004)". Wann das passiert ist, steht in `git log`.>
