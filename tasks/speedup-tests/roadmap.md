---
status: active  # active | done
task: speedup-tests
derived_from: konzept.md
created_at: 2026-08-12
last_updated: 2026-08-12
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: speedup-tests

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build` (Solution `AiNetLinter.slnx`, .NET 9/10 SDK). Messmethodik
  verlangt Trennung von Build und Testlauf: erst `dotnet build`, dann Testläufe mit `--no-build`.
- **Test-Command (heute, Legacy-Projekt `AiNetLinter.Tests`, xUnit v3):**
  - Schnelle Iteration: `dotnet test --filter Category=Unit` (~23-24s) oder
    `dotnet test --filter Category!=Integration`.
  - Abschluss-Gate (heute verbindlich, siehe `AGENTS.md` §2): `dotnet test --filter Category!=Stress`
    — **das ist laut Konzept-Diagnosewert >800s/real ~228s Wall Clock bei 1.471 Tests und für
    Zwischenschritte während der Umsetzung selbst zu teuer.**
  - Lastintensiv, nie automatisch: `dotnet test --filter Category=Stress`.
  - Ergebnis-Log: `TestResults/latest.trx` (`.runsettings` überschreibt fix; Leitplanke 10 verlangt
    künftig pro Profil eigenen `LogFileName`, das ist Teil des Fundament-/Messmethodik-Epics).
  - **Empfehlung für Zwischenschritte während dieses Refactorings** (Konzept §7 „Sparsame
    Verifikation"): pro Step nur die neu/geänderte Testklasse, der betroffene Namespace oder das
    kleinste passende neue Laufprofil (`dotnet test --filter FullyQualifiedName~<Klasse>` bzw. nach
    Fundament-Step projektbezogen `dotnet test src/AiNetLinter.FastTests`); nie der volle
    `Category!=Stress`-Lauf pro Step. Voller Lauf (alle Profile getrennt) nur an
    Epic-/Architekturgrenzen und am Task-Ende (Abschlussverifikation).
- **Lint-Command:** AiNetLinter lintet sich selbst (Dogfooding) — `AiNetLinter.exe` gegen
  `AiNetLinter.slnx`/`rules.json`; Ausführung ausschließlich im Hauptarbeitsverzeichnis, nie aus
  einem Git-Worktree (strukturell falsch-grün sonst, siehe Regel-Index unten).
- **Code-Style-Kurzfassung:** `sealed` für konkrete Klassen, `#nullable enable`, Methoden ≤60
  Zeilen (Testprojekte: 100), max. 4 Parameter (>4 → Parameter-`record`), kein `dynamic`, `out` nur
  in `Try*`, kein leeres `catch`, Result-Pattern statt Exceptions für erwartbare Fehlerfälle, sparsame
  Kommentare ohne Task-/Step-ID-Referenzen. `*.Tests`-Override: `MaxMethodLineCount` 100,
  `EnforceSealedClasses` aus — **muss laut Leitplanke 0 auf die drei neuen Projektnamen
  (`AiNetLinter.FastTests`, `AiNetLinter.IntegrationTests`, `AiNetLinter.TestKit`) erweitert werden**,
  sonst greifen für sie versehentlich die vollen Produktionsregeln.
- **Commit-Konventionen:** Conventional Commits auf Deutsch, imperativ, mit Task-Suffix
  `[speedup-tests]`; kein Amend/Rebase, kein Push durch den Loop (Konzept, Definition of Done).
  Commit-Vorschlag ist laut `.agents/rules/AiNetLinterRichtlinien.mdc` §6 ohnehin Pflicht bei jeder
  versionierten Dateiänderung.

## Regel-Index

- `.agents/rules/AiNetLinter.mdc` — auto-generierte Kurzfassung der aktiven Linter-Grenzwerte/-Regeln
  aus `rules.json` (Methodenlänge, Parameterzahl, Sealed-Pflicht, Test-Projekt-Overrides etc.).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — manuell gepflegte Architektur-/Workflow-Leitplanken:
  Windows/PowerShell-Tooling, `dotnet test`/TRX-Diagnose (§3), Testparallelität/Collection-Regeln
  (§4), Doku-Update-Pflicht und Commit-Vorschlag-Pflicht, Kommentar-Regeln (§5).

## Epics

- [ ] EPIC-1: Fundament — Zielprojekte, TestKit, Architekturguards, Ledger, Minimum Safety Envelope,
      Legacy-Quarantäne — Konzept §Grober Lösungsansatz 1-2, Leitplanke 0/6/8. Umfasst: neue Projekte
      `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests`/`AiNetLinter.TestKit` samt gemeinsamer
      `TestProject.props`; Anpassung von `rules.json` (`ProjectOverrides`), `TestSentinel.
      TestProjectNameSuffixes`, `InternalsVisibleTo`; `test-migration-ledger.md` mit
      Konsistenzguard; Legacy-Build-Gate; Baseline-Messung vor dem ersten Refactoring
      (Messmethodik Leitplanke 10); Minimum Safety Envelope (Config laden, vorbereitete Solution
      analysieren, CLI-Adapter, MCP-Handshake) und Umschalten des normalen Gates auf die neuen
      schnellen Profile.
- [ ] EPIC-2: Testplattform-Fundamente — `RoslynTestSolutionFactory`, `PreparedSolutionFixture`,
      gecachte `MetadataReference`n, lazy Materialisierung, `FilterMini`-Fixture — Konzept §2/§4.
      Voraussetzung für die breite Migration in EPIC-3/4, da diese Bausteine von den migrierten
      Tests konsumiert werden.
- [ ] EPIC-3: Checker-/Parser-/Renderer-Kohorte auf Unit-Ebene migrieren — reine Logik-/Syntax-/
      kleine-Compilation-Tests ohne MSBuild/Prozess/Repo aus `AiNetLinter.Tests` nach
      `AiNetLinter.FastTests` (Unit) — Konzept §9 „Sinnvolle Kohorten" Punkt 2, Leitplanke 1.
- [ ] EPIC-4: In-Memory-Roslyn-/Filter-/Scanner-/Tool-Kohorte migrieren, inkl. objektbasierter
      Produkt-Seams (Laden/Ausführen trennen, z. B. `SkeletonMapBuilder`) — Konzept §9 Punkt 3,
      Leitplanke 3, Definition of Done „Filtermatrix gegen kalibrierte Solution".
- [ ] EPIC-5: MSBuild-/Fixture-/Baseline-/Datei-/Refresh-Kohorte migrieren — `MsBuildFixtureHost`,
      `IsolatedFixtureLease`, Fidelity-/Paritätstests zwischen In-Memory- und echter MSBuild-Welt
      — Konzept §9 Punkt 4, Leitplanke 4.
- [ ] EPIC-6: CLI-, MCP-Prozess-, Dogfood-, Performance- und Stress-Kohorte migrieren — geteilter
      read-only MCP-Host für idempotente Smokes, exklusive Hosts für Framing/Retry/Refresh,
      Parallelitätsbudgets (`SubprocessConcurrencyGate`-Nachfolger), getrennte Runner-Konfiguration
      pro Assembly — Konzept §9 Punkt 5, Leitplanke 5, 6 (Runner-/Prozessparallelität).
- [ ] EPIC-7: Restmigration, Legacy-Löschung, finale Laufprofile, Messbericht, Dokumentation —
      verbleibende Kohorten, Ledger auf `pending = 0`, physisches Löschen von `AiNetLinter.Tests`
      und Solution-Bereinigung, Abschlussverifikation aller Profile (Unit/Component/Integration/
      Dogfood/Performance/Stress getrennt), Vorher-/Nachher-Messbericht nach Leitplanke 10,
      Aktualisierung von `AGENTS.md`/Testdokumentation/Diagnoseregel — Konzept §9 Punkt 6,
      Definition of Done.

<Reihenfolge folgt der Strangler-Abhängigkeit aus Konzept §8: EPIC-1/2 sind Voraussetzung für jede
Migrationskohorte (EPIC-3..6), EPIC-7 setzt voraus, dass alle Kohorten migriert sind. Die genaue
Kohortengrenze (EPIC-3 vs. -4 vs. -5 vs. -6) kann sich im Step-Modus anhand des dann tatsächlichen
Codes verschieben oder weiter aufteilen (Konzept §9 „Aufteilen entlang der Kohortengrenze
ausdrücklich erlaubt") — das ist kein Bruch dieser Roadmap, sondern der vorgesehene Feinschnitt.>
