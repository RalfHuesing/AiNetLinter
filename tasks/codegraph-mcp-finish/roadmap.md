---
status: active  # active | done
task: codegraph-mcp-finish
derived_from: Konzept.md
created_at: 2026-08-03
last_updated: 2026-08-04  # step-011 abgeschlossen (EPIC-06 abgehakt, Kritiker approved); EPIC-07 + EPIC-08 als naechstes offen → step-012 (in Planung)
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: codegraph-mcp-finish

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build AiNetLinter.slnx` — beide Projekte
  (`AiNetLinter.csproj`, `AiNetLinter.Tests.csproj`) sind mit
  `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` konfiguriert
  (Zero-Warning-Direktive, `AiNetLinterRichtlinien.mdc` §5) — kein Commit
  mit rotem Build, keine neuen Warnungen.
- **Test-Command:**
  - Schnelle Iteration während der Entwicklung:
    `dotnet test --filter Category=Unit` (bzw. `Category!=Integration`).
  - Abschluss-Verifikation (Pflicht vor jedem Step-Abschluss/Task-Ende):
    `dotnet test AiNetLinter.slnx --no-build` (Volllauf, aktuell ~8 Min.,
    genau das, was Block F verkürzen soll).
  - Testkategorien: `[Trait("Category", "Unit"|"Integration")]`, bereits
    projektweit etabliert.
  - Diagnose bei Fehlern/langem Output: `TestResults/latest.trx` direkt
    auslesen statt Lauf blind zu wiederholen (`.runsettings` überschreibt
    die Datei bei jedem Lauf automatisch).
  - **Vor jedem Build/Test in diesem Task:** offene `AiNetLinter.exe`-/
    `testhost.exe`-Prozesse prüfen und ggf. beenden (bekannte
    Datei-Sperren-Falle, siehe Konzept.md "Entdeckte Mängel").
- **Lint-Command:** AiNetLinter lintet sich selbst (`rules.json` +
  auto-generierte `.agents/rules/AiNetLinter.mdc`). Sync nach
  Regel-/CLI-Änderungen:
  `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`.
- **Code-Style-Kurzfassung:** siehe Regel-Index unten — Kurzfassung der
  Kurzfassung: `sealed` für konkrete Klassen, Methoden ≤60 Zeilen, ab 5
  Parametern Input-`record`, kein leeres `catch`, `#nullable enable` pro
  Datei, `AIContextFootprint` ≤ 2500 (Kopplung), Result-Pattern statt
  Exceptions wo sinnvoll, keine Task-/Planungsartefakt-Referenzen
  (`step-NNN`, `TD-NNN`, `EPIC-NN`) in Code-Kommentaren.
- **Commit-Konventionen:** Conventional Commits **auf Deutsch**,
  imperativ (`feat:`, `fix:`, `refactor:`, `docs:`, `chore:`, …). Jede
  Antwort mit Datei-Änderungen endet mit einem
  `### Commit-Vorschlag`-Block (reiner Commit-Text, kein Shell-Befehl).
  Zusätzlich gemäß `spec.md` §10.3: Commit-Subject trägt den
  Task-Kurznamen als Suffix `[codegraph-mcp-finish]`, bei **jedem**
  Commit dieses Tasks (Code- wie Doku-Commits).

## Regel-Index

- `.agents/rules/AiNetLinter.mdc` — Auto-generierte Linter-Metriken/
  Grenzwerte aus `rules.json` (Kurz-Stil, `MaxLineCount`,
  `AIContextFootprint`, Compound-Suppressions, aktive Checker-Kategorien
  agent-resilience/architecture/test-coverage/general, Projekt-Overrides
  für `*.Tests`) — verbindlich für jeden neu geschriebenen
  Produktionscode.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Manuell gepflegte
  Architektur-Grundprinzipien (monolithisch, kein DI-Container, kein
  `AssemblyLoadContext`/Plugin-System), Windows-Shell-/Tooling-Regeln,
  Build/Test-Pflichten (Zero-Warning, `xUnit v3`, Testsuite-Parallelität
  bewahren §4), Kommentar-/Dokumentations-Konventionen (§5, u. a. Verbot
  von Task-Artefakt-Referenzen im Code) und Commit-Vorschlag-Pflicht.

## Epics

- [x] EPIC-01: Testsuite-Performance (Block F) — `ConsoleTestCollection`
      von 21 auf begründete Mitglieder eingrenzen (größter Laufzeit-Hebel,
      F.1, **erledigt → step-001**, approved), `CliProcessRunner`-Helper
      für 8 Subprozess-Teststellen (F.2, **erledigt → step-002**, approved),
      `Core/`-Testordner sub-gliedern + danach `MaxDirectoryChildren`
      aktivieren (F.3, **erledigt → step-003**, approved), Test-Data-Builder/
      Object-Mother (F.4, **vollständig abgeschlossen → step-004 +
      step-005**, approved: 19 Dateien Kern-Testinfrastruktur in step-004 +
      19 restliche Dateien mit lokaler `CreateConfig`/`ConfigWith`-Methode in
      `Core/Checkers/`+`Metrics/`+`FalsePositives/` in step-005 — bei
      erneuter Code-Sichtung für step-005 auf tatsächlich 19 statt der in
      step-004 geschätzten 23 verifiziert, siehe step-005 „Aktueller
      Projektzustand"), `#nullable enable`-Pragma nur als Randmitnahme in
      ohnehin angefassten Dateien (F.5, **erledigt (Teilfortschritt wie
      laut Konzept vorgesehen) → step-005**, approved: 11 der 19 dort
      angefassten Dateien fehlte die Pragma-Zeile, im selben Step
      nachgerüstet — keine eigene Flächenaktion, wie in `Konzept.md`
      Zeile 432-437 als Nutzer-Entscheidung vorgegeben, damit gilt F.5 als
      abgeschlossen), Laufzeitmessung vorher/nachher dokumentieren
      (F.6, **erledigt → step-006**, approved: Volllauf von ~8 Min. auf
      ~1 m 35-40 s reduziert, Faktor ~4,9x, formal mit zwei
      zeitgestoppten Läufen + TRX-Gegencheck dokumentiert). **Alle sechs
      Teilpunkte F.1-F.6 sind damit vollständig erledigt und approved —
      EPIC-01 ist inhaltlich abgeschlossen.** Bezug: Konzept.md „Muss-Haben F".
      F.1-Ergebnis (step-001): Volllauf von ~8 Min. auf ~1 m 35–41 s
      reduziert (informelle Messung, formale F.6-Dokumentation steht noch
      aus); neue `SubprocessConcurrencyGate`-Bremse
      (`src/AiNetLinter.Tests/Fixtures/`) wie geplant als internes Detail
      von `CliProcessRunner` aufgenommen (step-002, kein zweiter
      konkurrierender Mechanismus entstanden). F.2-Tech-Debt-Fund `TD-002`
      (tote Variable `baselineAfter` in `WebBaselineTests.cs:92`) bleibt
      bewusst offen (Nutzer-Entscheidung, kein automatischer Fix-Step).
      F.3-Ergebnis (step-003): `Core/` von 42 auf 19+27(Checkers)+7(Metrics)
      Dateien sub-gegliedert, `MaxDirectoryChildren` = 30 aktiv,
      F.3-Tech-Debt-Fund `TD-003` (`--sync-agent-rules-only` fehlt in
      `HasStandaloneCommand()`) bleibt offen (Nutzer-Entscheidung).
- [x] EPIC-02: Einheit-011-Abschluss (Muss-Haben A) — **erledigt →
      step-007 + step-007/fix-01**, beide approved. Bezug: Konzept.md
      „Muss-Haben A". Drei Beobachtungen aus dem Planungszeitraum von
      step-007 (2026-08-03), jetzt alle erledigt: (1) Der Push-Teil war
      bereits erfolgt — per `git merge-base --is-ancestor` verifiziert,
      alle 6 Einheit-011-Commits (`4bcd5ab`…`8a663c7`) sind Vorfahren
      von `origin/main`, vermutlich beim Push der step-001..004-Arbeit
      mit hochgeschoben, `Konzept.md`s Stand „11 Commits lokal ohne
      Push" (`git log -1` = `59c2f5e`) ist damit veraltet. (2) Volllauf
      frisch nachgefahren statt nur Coder-Bericht übernommen: `dotnet
      test AiNetLinter.slnx --no-build` 1186/1186 grün, ~1 m 41 s
      (step-007). (3) Nachgeholtes Review der 6 lokalen 011-Commits
      (TD-009 Konstruktor-Record, TD-014 Factory-Aufteilung, TD-019
      Test-Flake-Retry) inkl. der 9-Datei-`PathOverride`-Erweiterung
      (`rules.json`, 14 Einträge mit `MaxAIContextFootprint: 2700`,
      davon 9 neu aus Commit `8a663c7`) als akzeptiertem Pragmatik-Fix
      — im step-007-Review ohne `issues`-Verdict auf den 011-Commits
      selbst. Drei nachträglich entdeckte MAJOR-Findings (TD-/Plan-
      Artefakt-Referenzen + abgeschnittene Satzreste in 3
      Produktionsdateien, `AiNetLinterRichtlinien.mdc` §5) wurden in
      **step-007/fix-01** (`cf3d7ac1`) behoben — 3 Pflichtdateien +
      5 Test-Dateien als MINOR-Mitnahme, Build grün, Tests 1186/1184
      (1 Last-Flake in `McpServerCommandErrorHandlingTests` an
      `SubprocessConcurrencyGate.AcquireAsync`, außerhalb des Step-
      Scopes, als `TD-005` dokumentiert). EPIC-02 ist inhaltlich
      abgeschlossen.
- [x] EPIC-03: `ILinterEngineConfig`-Refactor (Muss-Haben C, TD-008/
      TD-010) — **erledigt → step-008**, approved. Bezug: Konzept.md
      „Muss-Haben C". Schlankes `internal interface ILinterEngineConfig`
      mit den 11 Properties, die `LinterEngine` und die MCP-Tools
      tatsächlich konsumieren, in
      `src/AiNetLinter/Configuration/ILinterEngineConfig.cs` neu
      angelegt; `Config` (`Config.cs:7`) implementiert es implizit.
      `McpCodeGraphServer.Config` und `McpCodeGraphServerOptions.Config`
      sind vom Interface-Typ, womit der `Configuration`-Namespace nicht
      mehr strukturell in den Footprint der Tool-Klassen gezogen wird,
      die `Config` nur transitiv über den `McpCodeGraphServer`-Typ
      referenzierten. `rules.json`-`PathOverrides` von 14 Einträgen auf
      **2 Rest-Einträge** reduziert — die verbleibenden sind
      `FindReferencesTool` (Footprint 2529) und `FindSymbolTool`
      (Footprint 2516), deren Symbol-Graph-Lookups strukturell an
      `Configuration`-Sub-Typen koppeln, eine Aufspaltung gehört zu
      EPIC-08 (E-Block). **Weg A** (Downcast am Call-Site in
      `GetViolationsScanner.BuildViolationsTextAsync`) umgesetzt —
      `LinterEngine` behält den konkreten `Config`-Parametertyp
      (Record-Semantik für `with { SolutionBasePath = dir }` und
      durchgereichte Sub-Properties); der Downcast ist strukturell
      sicher, weil `ILinterEngineConfig` projektweit nur **einmal** von
      `Config` implementiert wird (per Grep verifiziert). 12 Tool-Test-
      Dateien im `Mcp/`-Bereich kompilieren ohne Test-Inhalts-Änderung
      (`McpCodeGraphServerOptions.From(...)` bleibt 1:1 kompatibel).
      Volllauf 1185/1186 grün reproduziert, TD-005-Last-Flake in
      `McpServerCommandErrorHandlingTests` als `infrastructure`
      klassifiziert (scope-extern, keine Fix-Versuche verbraucht) — im
      `step-008/step-result.md` und `step-008/step-review.md` detailliert
      dokumentiert. EPIC-03 ist inhaltlich abgeschlossen.
- [x] EPIC-04: Betriebsrisiko-Fixes — `rules.json`-Auto-Discovery (B.1)
      + Verzeichnis-Sweep für neue/gelöschte `.cs`-Dateien (B.2). Beide
      beheben silent-falsche Tool-Antworten, deshalb laut Konzept-Vorgabe
      vor den zeitbasierten Punkten (B.3-B.5). **→ step-009** + **→
      step-009/fix-01**, beide approved. Bezug: Konzept.md „Muss-Haben B",
      Punkte 1-2. `step-009` (Commit `1fd09c1`): B.1+`UsedDefaultConfig`-
      Propagation + B.2+3 Unit-Tests (`McpCodeGraphServerFileDiscoveryTests`,
      alle mit `[Trait("Category", "Unit")]`). `step-009/fix-01` (Commit
      `60429e2`, `6b24fe5`): die im step-009-Review fehlenden 3
      B.1-Unit-Tests in `McpServerCommandTests.cs` nachgereicht, plus
      `step-result.md`-Dauer/Test-Anzahl-Korrektur, plus 2 Kommentar-
      Sanierungen (forward-looking Rationale statt „frueheren 5-Parameter-
      Konstruktor") und stille-Catch-Sanierung in
      `McpCodeGraphServerRefresh.cs:188-196` (analog `TryAddDocument`,
      `ainetlinter-disable EnforceNoSilentCatch`-Suppression entfernt).
      Volllauf 1192/1192 grün, kein TD-005-Flake.
- [x] EPIC-05: Last-Fixture + Performance-Fixes — generierte
      Last-Fixture als Skalierungsnachweis inkl. Messlauf (B.3, bewusst
      **vor** B.4/B.5, damit deren Umsetzung gegen echte Zahlen erfolgt),
      Kaltstart-Entkopplung (Transport zuerst, Solution-Load im
      Hintergrund, dritter "lädt noch"-Zustand, B.4), Staleness-Sweep
      über Verzeichnis-`mtime` kurzschließen (B.5, kombinierbar mit
      B.2-Sweep-Mechanismus). **→ step-010** (approved mit 2 MINOR +
      TD-008, Build 0/0, Volllauf 1199/1199 in 2:12, kein TD-005-Flake
      reproduziert). B.3-Fixture-Generierung erzeugt valide
      `MSBuildWorkspace.OpenSolutionAsync`-Targets, gemessen Cold-Start
      0,70 s (10k-LOC) und 1k/10k-Stufen, B.4-Drei-Zustands-Lifecycle mit
      `ServerLoadState`-Enum (Loading/Loaded/LoadFailed) und
      `McpToolResults.Loading()`-Antwort in allen 9 Tool-Klassen, B.5
      mtime-Cache (`HasSolutionDirChanged` + rekursives
      `ComputeMaxDirMtimeUtc`, in B.2-Sweep integriert via
      `shouldSweep`-Parameter). TD-005-Sanity (Gate 4→6 Slots, 60s
      Timeout) und TD-007-Mitnahme (XML-Doc-Sanierung in
      `McpCodeGraphServerOptions.cs:42-46 + 62-64`) erledigt. Zwei MINOR:
      XML-Doc-Cleanup in `FindSymbolTool.cs:14-24` (offen) und
      TD-005/TD-007-Status nicht in `tech-debt.md` geschlossen (offen —
      in step-011-Fix nachzuholen, Aufwand je 1 Min.). Bezug: Konzept.md
      „Muss-Haben B", Punkte 3-5.
- [x] EPIC-06: Robustheit & Observability — eigene `ILintConsole` für
      den MCP-Modus, die stdout strukturell als reinen Protokollkanal
      schützt, plus E2E-Test für JSON-RPC-Framing (B.6); Opt-in
      Call-Log `--mcp-log` (B.7). Bezug: Konzept.md „Muss-Haben B",
      Punkte 6-7. **→ step-011** (approved, `step-011/step-review.md`):
      B.6 liefert `McpLintConsole` in `Output/` als `internal sealed`
      Singleton (`WriteLine`+`WriteError` → stderr) plus E2E-Framing-Test
      mit echtem Subprozess und rohem stdout-Read (2 Tests,
      Integration-Kategorie, `>= 2`-Schwelle begründet gelockert); B.7
      liefert `--mcp-log` als vollständiges CLI-Pattern
      (`LinterArgs`/`CliOptions`/`CliCommandBuilder`/`CliOptionFactory`/
      `Program.cs`), `McpCallLog` in `Mcp/` mit JSONL-Format + Lock +
      `IAsyncDisposable`-Scope + Auto-Delete bei 0 Einträgen, Verdrahtung
      über `McpServerOptionsFactory.Create` und 9 mechanische
      Lambda-Wrapper (4+3+2) in den drei `*Registrations.cs` mit
      `if (callLog is null)`-Fast-Path. Beide B.6+B.7 mit allen 5
      dokumentierten Abweichungen plausibel begründet (Scope-`Complete`-
      Pattern, Sub-String-Marker statt literal-Match, Refactor für
      `MaxMethodLineCount`, Pfad-Auflösung im Aufrufer, `>= 2`-Schwelle).
      Build grün (0/0, 2.3 s), Volllauf 1215/1215 grün (3 m 4 s, kein
      TD-005-Flake), 4 PathOverride-Werte in `rules.json` neu justiert
      (SymbolGraph 2650→2850, FileStructure 2640→2810, Analysis
      2640→2800, McpServerOptionsFactory 2640→2800) — begründet durch
      transitiven Pull-in aus `ModelContextProtocol.Protocol.CallToolResult`
      + `System.Text.Json` + `McpCallLog`, Selbst-Lint bestätigt 0/0
      Violations. EPIC-06 ist inhaltlich abgeschlossen.
- [ ] EPIC-07: Restliche Tech-Debt-Einträge (Muss-Haben D) — TD-001
      (ungenutzte transitive Paket-Referenz), TD-002 (Subprozess-E2E-Test
      ohne Fixture-Pool, deckt sich mit F.2), TD-004 (Footprint-Druck auf
      die drei Tool-Registrierungs-Sammelklassen), TD-005 (Gegenmuster
      "dünner Dispatch" bei neuen Tools konsequent anwenden), TD-006
      (`GetIndexScopeScanner`/`WebFileCatalog`-Duplikation
      konsolidieren), TD-007 (`TryApplyContentChange`-Parameter in
      Input-`record` ziehen). Jeder Eintrag muss laut DoD entweder
      geschlossen oder bewusst mit Begründung zurückgestellt werden —
      mehrere Einträge überschneiden sich mit Ansatzpunkten aus anderen
      Epics (TD-002/F.2, TD-011/E.1) und sollten dort mitgeprüft werden,
      bevor dieses Epic sie als offen führt. Bezug: Konzept.md
      „Muss-Haben D". **→ step-012** (in Planung, EPIC-07 + EPIC-08
      zusammengefasst — EPIC-07 mit Footprint-Sanierung läuft vor EPIC-08,
      damit das `get_symbol_body`-Tool der fünften Registrar-Klasse nicht
      gegen den bereits knappen Footprint kämpft).
- [ ] EPIC-08: Symbolgraph-Erweiterungen (Muss-Haben E, aus
      `codegraph-mcp-next` übernommen) — `get_symbol_body` + stabile
      Symbol-IDs in `get_file_skeleton` (E.1, löst TD-011 mit, vierte
      Tool-Registrar-Klasse `SymbolBodyToolRegistrations` wahrscheinlich
      nötig, da `SymbolGraphToolRegistrations` mit 4 Tools schon bei
      2850 PathOverride hängt), `depth`-Parameter an
      `find_references`/`get_impact` mit aggregierter Ausgabe ab
      `depth > 1` (E.2), DI-Registrierungs-Hinweis als Zusatzzeile in
      `get_type_hierarchy` (E.3). Läuft laut Konzept-Vorgabe **zuletzt**,
      da alle drei von dem in EPIC-03 entlasteten Footprint profitieren.
      Bezug: Konzept.md „Muss-Haben E". **→ step-012** (in Planung,
      gemeinsam mit EPIC-07 in einem Schritt).

**Nicht als eigenes Epic geführt, aber Teil der Definition of Done jedes
betroffenen Epics:** laufende Doku-Pflege (`Docs/agent-api.md`,
`Docs/integration.md`, `Docs/ROADMAP.md` Zeilen 478-493 von "Geplant" auf
den tatsächlichen Stand, E.1-E.3 dort neu ergänzen, `README.md`,
`Docs/configuration.md` bei Konfig-Änderungen) sowie das Löschen von
`tasks/test-optimierung/` als letztem verbleibenden Vorgänger-Ordner
(Konzept.md, Abschluss-Kriterium der Definition of Done) — beides wird
vom Step-Modus-Planer bei den jeweils passenden Steps mit eingeplant,
nicht als separates Epic.
