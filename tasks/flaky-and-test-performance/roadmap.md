---
status: active  # active | done
task: flaky-and-test-performance
derived_from: konzept.md
created_at: 2026-08-07T08:58:29+02:00
last_updated: 2026-08-07T08:58:29+02:00
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: flaky-and-test-performance

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

Aus dem Projekt abgeleitet (Konzept, AGENTS.md, AiNetLinterRichtlinien.mdc,
Test-Projekt-Setup), einmalig hier (nicht pro Step neu):

- **Build-Command:** `dotnet build` (Solution-Root; `TreatWarningsAsErrors=true` in beiden Projekten)
- **Test-Command:** `dotnet test` (voller Lauf) — loggt nach `TestResults/latest.trx` via `.runsettings`; xUnit-v3-Konfig in `src/AiNetLinter.Tests/xunit.runner.json` (`parallelizeTestCollections=true`, `longRunningTestSeconds=3`)
- **Fast-Path-Test-Command:** `dotnet test --filter Category=Unit` (oder `Category!=Integration`) — wird in EPIC-04 etabliert/verifiziert
- **Lint-Command:** `dotnet run --project src/AiNetLinter -- --self-lint` (Self-Lint, muss `OK` bleiben)
- **Sprache / Tooling:** .NET 10 (`net10.0`), xUnit v3 (`xunit.v3.core`/`xunit.v3.assert` 3.2.2), PowerShell 7, Windows-only
- **Code-Style-Kurzfassung** (aus `AiNetLinter.mdc` + `AiNetLinterRichtlinien.mdc`):
  - `sealed` für konkrete Klassen; Methoden ≤ 60 Zeilen; ab 5 Parametern `record` als Input-Object; `#nullable enable` am Dateianfang
  - Kein leeres `catch`, kein `dynamic`, `out` nur in `Try*`-Methoden, `async void` verboten (außer Event-Handler)
  - Result-Pattern bevorzugt statt Exceptions für Fehlerfälle
  - Tests-Projekt: `MaxMethodLineCount` 100, `EnforceSealedClasses` aus (`*.Tests`-Override)
  - Architektur: **kein** `AssemblyLoadContext`, **kein** DI-Container, monolithisches CLI bleibt schlank
  - **Sparsame Kommentare** — Self-Documenting Code bevorzugen; **keine** Verweise auf `step-NNN`/`TD-XXX`/`EPIC-XX`/Ticket-IDs in Code-Kommentaren; **keine** Refactoring-Historie; XML-Doc nur bei unkonventionellem *Why*
  - Zero-Warning-Direktive: kein Commit mit rotem Build
- **Commit-Konventionen:** Conventional Commits **auf Deutsch**, imperativ (z. B. `feat:`, `fix:`, `refactor:`), Subject ≤ 72 Zeichen, Body mit `### Commit-Vorschlag`-Block am Ende der Agent-Antwort (Pflicht — siehe AiNetLinterRichtlinien.mdc §4). Für diesen Task zusätzlich mit Suffix `[flaky-and-test-performance]` im Subject.
- **Test-Kategorien (xUnit v3):** `[Trait("Category", "Unit")]` / `"Integration"` (siehe EPIC-02 — aktuell nur 86 von ~1087 Tests getraggt).

## Regel-Index

Ein Eintrag pro Datei in `<rules_dir>/**` — Kurzbeschreibung, kein Volltext.
Zweck: Der Step-Modus-Planer ist pro Aufruf eine frische, isolierte Session
ohne Erinnerung an diesen Roadmap-Modus-Aufruf — er kann `<rules_dir>/**`
nicht bei jedem Step neu komplett lesen (Kosten), liest aber diesen Index
(steht hier in `roadmap.md`) und dann gezielt nur die 1-2 Dateien, die zum
aktuellen Step passen.

- `.agents/rules/AiNetLinter.mdc` — Auto-generierte C#-Codequalitätsregeln aus `rules.json` (sealed, LOC/Cyclomatic/Cognitive-Limits, agent-resilience, Architektur-Verbote wie Phantom-Dependencies, `*.Tests`-Overrides); für jeden Code-Step prüfen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Manuelle Architektur- und Workflow-Leitlinien: monolithisches CLI ohne ALC/DI, Windows/PowerShell-Tooling, xUnit-v3-Parallelität, Self-Lint-Pflicht, Zero-Warning-Direktive, `### Commit-Vorschlag`-Block-Pflicht, sparsame Kommentare ohne Step-/TD-/EPIC-IDs; **besonders relevant** für fast jeden Step in diesem Task (Test-/Fixture-Änderungen berühren §4, §5, §6).

## Epics

- [ ] EPIC-01: Spike — Fixture-Sharing validieren (Vorarbeit) — 2-3 der am stärksten duplizierten Fixtures (`SymbolGraphCatalogFixture` 18×, `SymbolGraphMcpFixture` 6×) probeweise auf `ICollectionFixture` umstellen, Vorher-/Nachher-Laufzeit messen (isoliert + unter Volllast) und auf Isolationsbrüche prüfen. Spike-Ergebnis entscheidet Umfang von EPIC-03 (Sharing reicht) bzw. ob zusätzlich EPIC-05 (Produktionscode-mockbare Lade-Pfade) nötig wird. Konzept §"Wie" Schritt 1, §"Muss-Haben" Punkt 3.

- [ ] EPIC-02: Category-Traits flächendeckend nachziehen — alle ~1000 ungetraggten Testmethoden mit `[Trait("Category", ...)]` versehen (Taxonomie: `Unit` vs. `Integration`/`Subprocess`/`Live`; genaue Aufteilung JIT in EPIC-01, falls Sharing Tests in andere Klassen verschiebt). Voraussetzung für EPIC-04 (Fast-Path per `--filter`). Kann parallel zu EPIC-01 begonnen werden, Konzept §"Wie" Schritt 2.

- [ ] EPIC-03: Fixture-Sharing im großen Stil umsetzen — geleitet vom Spike aus EPIC-01, die identifizierten Testklassen auf `ICollectionFixture` (mit expliziten `[CollectionDefinition]`-Klassen) umstellen; Reihenfolge so, dass bestehende `parallelizeTestCollections: true` erhalten bleibt; Tests, die Fixture-State mutieren, ggf. durch Reset-Hooks oder eigene (nicht-geteilte) Fixtures ausnehmen. Konzept §"Wie" Schritt 3.

- [ ] EPIC-04: Fast-Path-Befehl etablieren + Doku — dokumentierten/verifizierten Befehl für den Alltag schaffen (z. B. PowerShell-Helferlein oder AGENTS.md-Eintrag), der die `Unit`-Kategorie filtert; sicherstellen, dass Volllauf und Fast-Path beide grün sind; Verifikation mit 3+ Läufen pro Variante. Konzept §"Wie" Schritt 4, §"Definition of Done" Punkt "dokumentierter Fast-Path-Befehl".

- [ ] EPIC-05: Produktionscode — leichterer/mockbarer Lade-Pfad (bedingt / Nice-to-Have) — nur falls EPIC-01 zeigt, dass Fixture-Sharing allein nicht ausreicht: prüfen, ob `SourceFileCatalog` / `McpCodeGraphServer` einen in-process-Lade-Pfad bekommen können, damit Tests, die aktuell nur wegen Testbarkeit einen echten `.exe`-Subprozess starten, ohne diesen auskommen. Sichtbares CLI/MCP-Verhalten darf sich nicht ändern. Konzept §"Wie" Schritt 5, §"Muss-Haven" letzter Punkt; Nice-to-Have-Charakter explizit aus Konzept-Scope.

- [ ] EPIC-06: Flaky-Test strukturell fixen — `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` (Test-Datei Zeile ~112-150) vom Poll-Loop mit fixer 5s-Deadline auf `TaskCompletionSource`/Event-basiertes Warten auf den `LoadState`-Übergang umstellen; Thread-Pool-Timing-Abhängigkeit eliminieren. Verifikation: 10 aufeinanderfolgende **volle** Testläufe grün (nicht isoliert). Konzept §"Wie" Schritt 6, §"Muss-Haven" Flaky-Punkt, §"Definition of Done" Flaky-Kriterium.

- [ ] EPIC-07: Tote `ConsoleTestCollection`-Infrastruktur entfernen (Nice-to-Have) — falls im Zuge von EPIC-03 ohnehin angefasst, dort mit erledigen; sonst eigenständiger Kleinst-Schritt. Nicht erzwingen — laut Konzept §"Nice-to-Have" optional. Konzept §"Wie" Schritt 7, §"Entdeckte Mängel" (tote Serialisierungs-Infrastruktur).

- [ ] EPIC-08: Abschluss-Validierung & Vorher/Nachher-Doku — vollen Testlauf mit optimiertem Setup mehrfach laufen lassen, Median bilden und mit der ~90s-Baseline aus Konzept vergleichen; DoD-Punkte aus Konzept §"Definition of Done" vollständig durchgehen und im Task-Ergebnis dokumentieren (kein festes Zeitziel, nur "spürbar besser"); Self-Lint grün. Konzept §"Definition of Done".
