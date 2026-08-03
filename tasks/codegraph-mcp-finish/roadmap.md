---
status: active  # active | done
task: codegraph-mcp-finish
derived_from: Konzept.md
created_at: 2026-08-03
last_updated: 2026-08-03
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

- [ ] EPIC-01: Testsuite-Performance (Block F) — `ConsoleTestCollection`
      von 21 auf begründete Mitglieder eingrenzen (größter Laufzeit-Hebel,
      F.1), `CliProcessRunner`-Helper für 8 Subprozess-Teststellen (F.2),
      `Core/`-Testordner sub-gliedern + danach `MaxDirectoryChildren`
      aktivieren (F.3), Test-Data-Builder/Object-Mother (F.4),
      `#nullable enable`-Pragma nur als Randmitnahme in ohnehin
      angefassten Dateien (F.5), Laufzeitmessung vorher/nachher
      dokumentieren (F.6). Läuft laut Konzept-Vorgabe **zuerst**, damit
      alle nachfolgenden Einheiten von kürzeren Volllauf-Zeiten
      profitieren. Bezug: Konzept.md „Muss-Haben F".
- [ ] EPIC-02: Einheit-011-Abschluss (Muss-Haben A) — offene
      `AiNetLinter.exe`/`testhost.exe`-Prozesse bereinigen, Volllauf
      frisch fahren (nicht nur Coder-Bericht übernehmen), Kritiker-Review
      für die 6 lokalen 011-Commits (TD-009 Konstruktor-Record, TD-014
      Factory-Aufteilung, TD-019 Test-Flake-Retry) inkl. der
      9-Datei-`PathOverride`-Erweiterung als akzeptiertem Pragmatik-Fix
      nachholen, anschließend Push der 11 lokalen Commits. Bezug:
      Konzept.md „Muss-Haben A".
- [ ] EPIC-03: `ILinterEngineConfig`-Refactor (Muss-Haben C, TD-008/
      TD-010) — schlankes Interface für `McpCodeGraphServer.Config`
      extrahieren, `rules.json`-`PathOverride`-Liste (13 Einträge) auf
      tatsächlich verbleibenden Bedarf reduzieren (mit Begründung pro
      Rest-Override). Bewusst **vor** Block B eingeplant, damit B gegen
      den entlasteten Footprint umgesetzt wird. Bezug: Konzept.md
      „Muss-Haben C".
- [ ] EPIC-04: Betriebsrisiko-Fixes — `rules.json`-Auto-Discovery (B.1)
      + Verzeichnis-Sweep für neue/gelöschte `.cs`-Dateien (B.2). Beide
      beheben silent-falsche Tool-Antworten, deshalb laut Konzept-Vorgabe
      vor den zeitbasierten Punkten (B.3-B.5). Bezug: Konzept.md
      „Muss-Haben B", Punkte 1-2.
- [ ] EPIC-05: Last-Fixture + Performance-Fixes — generierte
      Last-Fixture als Skalierungsnachweis inkl. Messlauf (B.3, bewusst
      **vor** B.4/B.5, damit deren Umsetzung gegen echte Zahlen erfolgt),
      Kaltstart-Entkopplung (Transport zuerst, Solution-Load im
      Hintergrund, dritter "lädt noch"-Zustand, B.4), Staleness-Sweep
      über Verzeichnis-`mtime` kurzschließen (B.5, kombinierbar mit
      B.2-Sweep-Mechanismus). Bezug: Konzept.md „Muss-Haben B", Punkte
      3-5.
- [ ] EPIC-06: Robustheit & Observability — eigene `ILintConsole` für
      den MCP-Modus, die stdout strukturell als reinen Protokollkanal
      schützt, plus E2E-Test für JSON-RPC-Framing (B.6); Opt-in
      Call-Log `--mcp-log` (B.7). Bezug: Konzept.md „Muss-Haben B",
      Punkte 6-7.
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
      „Muss-Haben D".
- [ ] EPIC-08: Symbolgraph-Erweiterungen (Muss-Haben E, aus
      `codegraph-mcp-next` übernommen) — `get_symbol_body` + stabile
      Symbol-IDs in `get_file_skeleton` (E.1, löst TD-011 mit, fünfte
      Symbolgraph-Registrar-Klasse falls nötig), `depth`-Parameter an
      `find_references`/`get_impact` mit aggregierter Ausgabe ab
      `depth > 1` (E.2), DI-Registrierungs-Hinweis als Zusatzzeile in
      `get_type_hierarchy` (E.3). Läuft laut Konzept-Vorgabe **zuletzt**,
      da alle drei von dem in EPIC-03 entlasteten Footprint profitieren.
      Bezug: Konzept.md „Muss-Haben E".

**Nicht als eigenes Epic geführt, aber Teil der Definition of Done jedes
betroffenen Epics:** laufende Doku-Pflege (`Docs/agent-api.md`,
`Docs/integration.md`, `Docs/ROADMAP.md` Zeilen 478-493 von "Geplant" auf
den tatsächlichen Stand, E.1-E.3 dort neu ergänzen, `README.md`,
`Docs/configuration.md` bei Konfig-Änderungen) sowie das Löschen von
`tasks/test-optimierung/` als letztem verbleibenden Vorgänger-Ordner
(Konzept.md, Abschluss-Kriterium der Definition of Done) — beides wird
vom Step-Modus-Planer bei den jeweils passenden Steps mit eingeplant,
nicht als separates Epic.
