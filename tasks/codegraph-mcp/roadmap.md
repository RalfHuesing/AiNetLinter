---
status: active  # active | done
task: codegraph-mcp
derived_from: konzept.md
created_at: 2026-07-31T00:00:00Z
last_updated: 2026-07-31T23:00:00Z
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: codegraph-mcp

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../../.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` §7.2.
Diese Datei wird laufend angepasst (Epics abgehakt, ergänzt, umformuliert
oder als obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build AiNetLinter.slnx` (Repo-Root; enthält
  `src/AiNetLinter/AiNetLinter.csproj` — Executable — und
  `src/AiNetLinter.Tests/AiNetLinter.Tests.csproj`). TFM `net10.0`,
  `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in beiden
  Projekten — jede neue Compiler-Warnung bricht den Build.
- **Test-Command:** `dotnet test AiNetLinter.slnx` — xUnit v3
  (`src/AiNetLinter.Tests/*.cs`, u. a. `DiffImpactAnalyzerTests.cs`,
  `LinterEngineTests.cs`, `ProgramTests.cs` als bestehende Vorbilder für
  Integrationstests gegen Test-Fixtures wie `tests/Fixtures/BaselineMini/`).
  `AGENTS.md`: "Beende einen Task erst, wenn `dotnet test` grün
  durchgelaufen ist" — nach jeder Code-Änderung Pflicht.
- **Lint-Command:** `ainetlinter --config rules.json --path ./src/` (das
  Tool lintet sich mit seinen eigenen Regeln selbst — `rules.json` im
  Repo-Root). Nach CLI-/Regel-Änderungen zusätzlich
  `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
  (AGENTS.md §4) um `.agents/rules/AiNetLinter.mdc` zu synchronisieren,
  falls `rules.json` sich durch diesen Task ändert (aktuell nicht
  vorgesehen laut `konzept.md`).
- **Code-Style-Kurzfassung:** `sealed` für konkrete Klassen (Ausnahmen nur
  über `SealedClassExemptSuffixes`), `#nullable enable` pro Datei, kein
  leeres `catch`, kein `dynamic`, `out` nur in `Try*`-Methoden, Methoden
  ≤60 Zeilen (Ausnahme: Initialisierungs-/Builder-Methoden mit
  CC≤3∧CogC≤5 bis 150 Zeilen als `warning`), max. 4 Parameter (ab 5 ein
  Input-`record`), max. 1 `bool`-Parameter, Dateien ≤500 Zeilen, kein
  DI-Container, kein `AssemblyLoadContext`/Plugin-System, Result-Pattern
  für erwartbare Fehler statt Exceptions (Exceptions nur für echte
  exogene Fälle). Vollständig: Regel-Index unten.
- **Commit-Konventionen:** Laut `AGENTS.md` §5 grundsätzlich Conventional
  Commits auf **Englisch**. `../../.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md`
  §10.3 sieht als Workflow-Default **deutsche** Imperativ-Form vor, "sofern
  Projekt-Rules nichts anderes vorgeben" — hier geben sie etwas anderes
  vor. **Für diesen Task gilt daher: Conventional Commits auf Englisch**
  (`feat:`, `fix:`, `docs:`, `chore:`, `test:`), projekteigene Regel
  schlägt den Workflow-Default. Zusätzlich der Task-Kurzname-Suffix
  `[codegraph-mcp]` im Subject (spec.md §10.3). `AiNetLinterRichtlinien.mdc`
  §4 verlangt außerdem einen `### Commit-Vorschlag`-Abschnitt am Ende jeder
  Antwort mit Datei-Änderungen — das ist eine Rolle des Coders, siehe
  Regel-Index.

## Regel-Index

- `.agents/rules/AiNetLinter.mdc` — automatisch aus `rules.json`
  generierte Grenzwerte/Regeln für C#-Codequalität (Zeilenlängen,
  Komplexität, Parameterzahl, Sealed/Nullable/Naming-Konventionen,
  Compound-Suppressions, Projekt-Overrides für `*.Tests`).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Architektur-Leitplanken
  (monolithisch bleiben, kein Plugin-System/ALC/DI-Container), Windows-
  Shell-Konventionen (PowerShell, `git --no-pager`), Build/Test-Pflicht,
  Doku-Update-Pflicht, Commit-Vorschlag-Pflicht, Zero-Warning-Direktive,
  Result-Pattern-Präferenz.

## Epics

- [x] EPIC-01: **abgeschlossen → step-001**. CLI-Einstiegspunkt &
      Server-Grundgerüst — neues Flag `--mcp-server` (`Cli/CliOptions.cs`,
      `CliOptionFactory.cs`, `LinterArgs.cs`), neuer
      `Commands/McpServerCommand.cs` (Vorbild: `ImpactCommand.cs`/
      `MapCommand.cs`), Dispatch in `Program.cs`, `ModelContextProtocol`-
      NuGet-Paket in `AiNetLinter.csproj`, Solution-Auswahl über `--path`
      mit Mehrdeutigkeits-Abbruch bei mehreren `.sln`/`.slnx`-Kandidaten
      (Verschärfung ggü. bestehendem `SourceFileCatalog.FindSolutionFile`-
      Verhalten), Server bleibt bei Ladefehler am Leben und liefert
      strukturierte `[ERROR]`-Antworten (`konzept.md` Muss-Haben "Neuer
      Ausführungsmodus", "Solution-Auswahl beim Start", "Fehlerbehandlung
      ohne Absturz" Teil 1).
      **Entscheidung zum Abschluss (Planer, step-002-Vorbereitung):** Das
      Epic wurde in step-001 selbst bereits bewusst auf genau diese vier
      Teile verengt — die zustandsvolle Resident-Server-Klasse wurde
      explizit EPIC-02 zugeschlagen, das Tool-Set EPIC-03 (siehe die
      vorherige Fassung dieser Zeile sowie `step-001/step-plan.md`).
      Step-001 hat exakt diesen verengten Scope vollständig und `approved`
      geliefert (siehe `step-001/step-review.md`) — es gibt innerhalb
      dieser Abgrenzung nichts Offenes mehr. Das Epic formal weiter offen
      zu lassen, bis EPIC-02 fertig ist, würde die beiden Epics inhaltlich
      wieder vermischen, die step-001 bewusst getrennt hat. Daher: EPIC-01
      abgehakt, EPIC-02 trägt den Rest eigenständig weiter (siehe unten).
      Zur Klarheit unverändert: `McpServerCommand` hält aktuell **keinen**
      wiederverwendbaren Zustand — `TryLoadSolutionAsync` lädt den
      `SourceFileCatalog` in einem `using`-Block und disposed ihn sofort
      wieder, bevor der Server startet (siehe
      `src/AiNetLinter/Commands/McpServerCommand.cs:36`). Genau das ist
      die Lücke, die EPIC-02/step-002 schließt.
- [x] EPIC-02: **abgeschlossen → step-002** (`approved`, siehe
      `step-002/step-review.md`). Server-Zustand & Staleness-Invalidierung
      — zustandshaltende Server-Klasse ohne DI-Container (Vorbild:
      statische Commands/direkte Instanziierung), Hash/mtime-Cache pro
      Datei, lazy Prüfung vor jeder Tool-Antwort, inkrementelles Update
      über `SourceFileCatalog.WithUpdatedSolution` (kein Komplett-Reload),
      Thread-sicherer Zugriff auf `Solution`/`Compilation` (`konzept.md`
      Muss-Haben "Lazy Staleness-Invalidierung", "Thread-sicherer
      Zugriff") — vollständig umgesetzt in `McpCodeGraphServer`
      (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs`). Review vermerkt
      `TD-003` (Race Condition in `SourceFileCatalog.RegisterMSBuild`,
      vorbestehend, kein Blocker) als Tech-Debt, kein offenes Finding.
- [ ] EPIC-03: **in Arbeit → step-004**. Symbolgraph-Tools — `find_symbol`,
      `find_references`, `get_impact`, `get_type_hierarchy`,
      `get_file_skeleton`. Basis:
      `SymbolFinder.FindDeclarationsAsync`/`FindDerivedClassesAsync`/
      `FindImplementationsAsync` (neu einzubinden),
      `DiffImpactAnalyzer.FindCallSitesAsync`/`AnalyzeAsync` (bereits
      vorhanden), `Maps/Skeleton/SkeletonMapBuilder.cs` granularisiert auf
      eine Datei statt Whole-Repo-Dump (`konzept.md` Tool-Tabelle).
      **step-003** (inkl. `fix-01`, beide `approved`) deckte die Tool-
      Registrierungs-Infrastruktur (wiederverwendbare `[ERROR]`-Antwort-
      Helper, Closure-basierte Anbindung an `McpCodeGraphServer`) plus das
      erste Tool `find_symbol` ab (Basis:
      `SymbolFinder.FindSourceDeclarationsAsync`). **step-004** (geplant)
      liefert `find_references` (Basis: `DiffImpactAnalyzer.FindCallSitesAsync`/
      `FindDocumentByPath`, beide auf `internal` angehoben statt neu
      gebaut) inkl. Identifikator-Aufloesung (Datei:Zeile:Spalte oder
      qualifizierter Name) — die restlichen 3 Tools (`get_impact`,
      `get_type_hierarchy`, `get_file_skeleton`) bleiben offen für weitere
      EPIC-03-Steps. **step-005** (geplant) liefert `get_impact`
      (Basis: `DiffImpactAnalyzer.AnalyzeAsync` fuer den Git-Ref-Zweig,
      `FindReferencesTool.ResolveSymbolAsync` + `DiffImpactAnalyzer.FindCallSitesAsync`
      fuer den Symbol-direkt-Zweig, beide bereits vorhanden/wiederverwendet)
      — inkl. `fix-01` (`approved`), das einen echten Subprozess-Hang in
      `DiffImpactAnalyzer.RunGitDiff` unter stdio-Transport behoben hat
      (nur durch die neue Dogfooding-Pflicht entdeckt, siehe
      `step-005/step-review.md` Finding 1). **step-006** (geplant) liefert
      `get_file_skeleton` (Basis: `SkeletonMapBuilder.ExtractFromDocumentAsync`,
      bereits granular pro Datei, nur Sichtbarkeit angehoben) — bewusst vor
      `get_type_hierarchy` eingeordnet, da letzteres eine bislang
      ungenutzte `SymbolFinder`-API neu einbinden muss (höheres Risiko),
      siehe `step-006/step-plan.md` „Reihenfolge-Begründung". Danach bleibt
      `get_type_hierarchy` als letztes offenes EPIC-03-Tool.
      **Neu seit 2026-07-31 (ersetzt EPIC-09, siehe unten):** jeder
      verbleibende EPIC-03-Tool-Step verifiziert sein Tool zusätzlich zu
      den Fixture-Tests einmal ad-hoc gegen die eigene `AiNetLinter.slnx`
      (Abschnitt "Dogfooding" in `step-result.md`, siehe `konzept.md`
      Muss-Haben).
- [ ] EPIC-04: Struktur-/Qualitäts-Tools — `get_index_scope` (Basis:
      `SourceFileCatalog.GetSourceFiles` + `Web/WebFileCatalog.cs`
      `Collect`, kein neuer Datei-Scan nötig, siehe "Entdeckte
      Mängel/Redundanzen" in `konzept.md`), `get_hotspots` (Basis:
      `Maps/HotspotMapBuilder.cs`), `get_violations` (Basis:
      `Core/RuleRegistry.cs`/`Core/LinterEngine.cs`, scoped statt
      Solution-weit, **umgeht bewusst** `Cache/AnalysisCacheManager.cs`
      und rechnet direkt gegen die resident gehaltene `Compilation`),
      `search_pattern` (Text-/Regex-Fallback über den Solution-
      Dateibestand) (`konzept.md` Tool-Tabelle, "Wie" / Cache-Isolation).
      **Gleiche Dogfooding-Pflicht wie EPIC-03** (siehe dort) gilt für
      jeden EPIC-04-Tool-Step.
- [ ] EPIC-05: Scope-Kommunikation & Miss-Hint — jede Tool-`description`
      der Roslyn-basierten Tools benennt explizit die C#-only-Grenze,
      `initialize`-Antwort trägt denselben Hinweis zentral im
      `instructions`-Feld (vom `ModelContextProtocol`-SDK unterstützt),
      `find_symbol`-Fallback bei fehlendem C#-Treffer über nicht vom
      Graph abgedeckte Dateitypen (`.js`/`.razor`/`.cshtml`/`.xaml`/
      `.html`/`.css`) mit expliziter Miss-Hint-Meldung statt stiller
      Leermenge (`konzept.md` Muss-Haben "Explizite Scope-Kommunikation",
      "Miss-Hint statt stiller Leermenge" — betrifft mehrere der in
      EPIC-03 gebauten Tools nachträglich, daher eigenes Epic statt
      Vermischung mit EPIC-03).
- [ ] EPIC-06: Robustheit bei Compile-/Solution-Fehlern — einzelne
      Dateien/Projekte mit Compile-Fehlern liefern für nicht betroffene
      Bereiche weiterhin korrekte Tool-Antworten, für betroffene Bereiche
      einen Warnhinweis statt Absturz (Roslyns bestehende Fehlertoleranz
      nutzen, nicht neu bauen); durchgängige Prüfung, dass alle 9 Tools
      bei nicht ladbarer Solution den strukturierten `[ERROR]`-Pfad aus
      EPIC-01 tatsächlich durchlaufen statt einzeln zu crashen
      (`konzept.md` Muss-Haben "Fehlerbehandlung ohne Absturz" Teil 2,
      Definition of Done "Solution mit Compile-Fehlern", "nicht ladbare
      Solution").
- [ ] EPIC-07: Tests — Unit-Tests für die Staleness-Invalidierung
      (Änderung zwischen zwei Tool-Calls wird erkannt), Integrationstests
      je Tool gegen eine Test-Solution (analog bestehender CLI-
      Integrationstests, z. B. `DiffImpactAnalyzerTests.cs`), Test für
      `get_index_scope` gegen gemischten Code (C#/JS/Razor/XAML/CSS),
      Test für Miss-Hint-Pfad, Test für Mehrdeutigkeits-Abbruch bei
      mehreren `.sln`/`.slnx`, Test für Cache-Isolation zwischen
      MCP-Server und parallelem CLI-Lint-Lauf auf derselben Solution,
      Regressionstest bestehender CLI-Batch-Modus (`konzept.md`
      "Muss-Haben" Tests, Definition of Done — mehrere Punkte dort sind
      testgetrieben abzunehmen).
- [ ] EPIC-08: Dokumentation — `Docs/agent-api.md` (neuer Abschnitt
      MCP-Modus), `Docs/integration.md` (Setup/Registrierung als
      MCP-Server), `Docs/ROADMAP.md`, `README.md` (`konzept.md`
      Muss-Haben "Dokumentation", `AGENTS.md` §4 Update-Pflicht bei
      Feature-Änderungen).
- [x] EPIC-09: **gestrichen (obsolet), 2026-07-31** — ursprünglich
      "Manueller Praxistest gegen `San.smart.Planner.Platform` (~160k
      LOC)". Ersetzt durch kontinuierliches, agentenseitiges Dogfooding
      pro Tool-Step gegen die eigene `AiNetLinter.slnx` (siehe `konzept.md`
      Muss-Haben "Dogfooding pro Tool-Step" sowie "Entdeckte Mängel/
      Redundanzen" für die Begründung — Nutzer-Entscheidung, Chat
      2026-07-31: externe Solution hatte in diesem Checkout nur ~3.600
      statt ~160k LOC, taugte ohnehin nicht mehr als Skalierungsnachweis,
      und "manuell" hätte kein Subagent selbst verifizieren können). Kein
      eigenständiger Step/Epic mehr nötig — die Prüfung ist ab sofort Teil
      jedes Tool-Steps in EPIC-03/EPIC-04 (Abschnitt "Dogfooding" in
      `step-result.md`).
