---
status: active  # active | done
task: speedup-tests
derived_from: konzept.md
created_at: 2026-08-12
last_updated: 2026-08-13  # EPIC-4 implementiert; step-019 wartet auf Audit
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: speedup-tests

> **ACHTUNG AN ALLE AGENTEN:**
> Die Zielarchitektur (EPIC-1 und 2) steht. Um den Markdown-Overhead pro Step in der anstehenden Migrationsphase (EPIC-4 bis 6) zu reduzieren, **ist das alte Batch-Limit von 8 aufgehoben**. 
> Fasse Dateien ab sofort zu großen, logischen "Super-Steps" zusammen (bis zu 40 Dateien pro Step)! Minimiere die Anzahl der neu generierten `step-*`-Ordner, indem du komplette Kohorten am Stück umsetzt.

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

- [x] EPIC-1: Fundament — Zielprojekte, TestKit, Architekturguards, Ledger, Minimum Safety Envelope,
      Legacy-Quarantäne — Konzept §Grober Lösungsansatz 1-2, Leitplanke 0/6/8. **Abgeschlossen →
      step-001, step-002 (Korrektur step-003), step-004 (Korrektur step-005)** (step-001 deckte die
      drei neuen Projekte samt gemeinsamer `TestProject.props`, Solution-Wiring und die beiden produktiven
      Konfigurationsverträge `ProjectOverrides`/`TestProjectNameSuffixes` aus Leitplanke 0 ab;
      step-002 deckte zusätzlich `test-migration-ledger.md` samt Konsistenzguard, die
      Architekturguards (statische Deny-Liste + Laufzeitcheck in `AiNetLinter.FastTests`/`TestKit`,
      Kategorien-/Profilguard je Ziel-Assembly) und die Baseline-Messung vor dem ersten Refactoring
      (Messmethodik Leitplanke 10) ab; step-003/step-004 schlossen die Minimum Safety Envelope
      (Config laden, vorbereitete Solution analysieren, CLI-Adapter mit Exit-Code,
      MCP-Handshake/Toolregistrierung), das Legacy-Build-Gate, `InternalsVisibleTo` für die neuen
      Assemblies und das Umschalten des normalen Gates auf die neuen schnellen Profile ab —
      step-005 korrigierte dabei ein von step-004 hinterlassenes MAJOR-Finding in
      `AiNetLinterRichtlinien.mdc` §4 (Text verwies noch ausschließlich auf die Legacy-MCP-Testklasse
      statt auf `McpHandshakeToolRegistrationTests`), inzwischen `approved`). Umfasst insgesamt: neue
      Projekte `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests`/`AiNetLinter.TestKit` samt
      gemeinsamer `TestProject.props`; Anpassung von `rules.json` (`ProjectOverrides`), `TestSentinel.
      TestProjectNameSuffixes`, `InternalsVisibleTo`; `test-migration-ledger.md` mit
      Konsistenzguard; Legacy-Build-Gate; Baseline-Messung vor dem ersten Refactoring
      (Messmethodik Leitplanke 10); Minimum Safety Envelope (Config laden, vorbereitete Solution
      analysieren, CLI-Adapter, MCP-Handshake) und Umschalten des normalen Gates auf die neuen
      schnellen Profile.
- [x] EPIC-2: Testplattform-Fundamente — `RoslynTestSolutionFactory`, `PreparedSolutionFixture`,
      `MsBuildFixtureHost`, `IsolatedFixtureLease`, gecachte `MetadataReference`n, lazy
      Materialisierung, `FilterMini`-Fixture — Konzept §2/§4. Voraussetzung für die breite Migration
      in EPIC-3/4/5, da diese Bausteine von den migrierten Tests konsumiert werden. **Abgeschlossen →
      step-006, step-007, step-008.** step-006 deckt `RoslynTestSolutionFactory`, die gecachten
      `MetadataReference`n und `PreparedSolutionFixture` mit lazy Szenario-Materialisierung ab, inkl.
      Migration des bestehenden lokalen `CreateAdhocSolution`-Helpers aus
      `LinterEngineSolutionAnalysisTests` auf die neue Plattform als erster echter Konsument.
      step-007 deckt `MsBuildFixtureHost` (einmaliger echter `MSBuildWorkspace`-Load einer
      kanonischen Mini-Solution) und `IsolatedFixtureLease` (isolierte Kopie für Mutations-Tests) ab.
      step-008 deckt die letzte offene Konzept-§2/§4-Zutat ab: die kalibrierte `FilterMini`-Fixture
      (Disk-Solution mit Produktions-/Testprojekt, mehreren Namespaces, public/private Membern,
      Projektbezug) plus dieselbe Spezifikation als In-Memory-`ProjectSpec`-Paar
      (`FilterMiniSolutionSpec` in `AiNetLinter.TestKit`) und einem ersten
      Fidelity-/Formvergleichstest zwischen beiden Welten (Konzept §4 „Fidelity-/Paritätstests",
      struktureller Formvergleich). **Bewusst nicht Teil von step-008 und damit weiterhin offen für
      EPIC-4:** die eigentliche Migration der 18-fälligen `FilterCliIntegrationTests`-Matrix auf die
      neue `FilterMini`-Fixture (Konzept §2/§9 Punkt 3 nennt das explizit als EPIC-4-Aufgabe,
      „voraussichtlich zusammen mit der Filtermatrix-Migration") — `FilterMini` ist jetzt als
      Fundament vorhanden, der Konsument fehlt noch bewusst.
- [x] EPIC-3: Checker-/Parser-/Renderer-Kohorte auf Unit-Ebene migrieren — reine Logik-/Syntax-/
      kleine-Compilation-Tests ohne MSBuild/Prozess/Repo aus `AiNetLinter.Tests` nach
      `AiNetLinter.FastTests` (Unit) — Konzept §9 „Sinnvolle Kohorten" Punkt 2, Leitplanke 1.
      **Abgeschlossen → step-010, step-011 und step-012 (`approved`).** Teil 1 ist durch step-010
      `approved`: komplette
      `Core/Checkers`-Kohorte, 28 Testklassen + eigene `TestHelper.cs`-Teilmenge in
      `AiNetLinter.FastTests`. Teil 2 ist durch step-011 `approved`: die komplette Parser-Kohorte
      (`Web/*AnalyzerTests` plus `WebSuppressionDetectorTests`, fünf Klassen). Step-012 migrierte als
      letzten Teil die zwei reinen Renderer-Testklassen (`Mcp/Tools/*RendererTests`) samt
      Coverage-Audit; das EPIC-3-Grenzgate ist mit 326 grünen Unit-Tests nachgewiesen.
- [x] EPIC-4: In-Memory-Roslyn-/Filter-/Scanner-/Tool-Kohorte migrieren, inkl. objektbasierter
      Produkt-Seams (Laden/Ausführen trennen, z. B. `SkeletonMapBuilder`) — Konzept §9 Punkt 3,
      Leitplanke 3, Definition of Done „Filtermatrix gegen kalibrierte Solution". **In Arbeit →
      step-013 (Korrektur step-014, inzwischen `approved`) schloss als ersten Teil die
      Skeleton-/Filterkohorte:** 18 Filterverträge gegen die vorbereitete `FilterMini`-Solution,
      objektbasierter `SkeletonMapBuilder`-Kern und zwei echte Pfad-/MSBuild-Adapterverträge.
      **step-015 (`approved`) schloss als zweiten Teil den `DuplicateDetectionScanner`:** sieben
      Component-Verträge auf `RoslynTestSolutionFactory` samt virtueller Pfadkalibrierung und
      produktseitigem Coverage-Audit. **step-016 (`approved`) schloss als dritten Teil den
      `RefactoringDriftScanner`:** sieben Legacy-Verträge wechselten auf dieselbe In-Memory-Factory;
      ein zusätzlicher Lambda-Caller-Vertrag schloss die beim produktseitigen Audit gefundene Lücke
      der Caller-Normalisierung. **step-017 (`approved`) schloss als vierten Teil die gemeinsame
      Duplicate-Detection-Engine-Familie:** `DuplicateDetectionEngineTests` (zwei Dateien) und
      `RefactoringDriftEngineTests` wechselten gemeinsam auf die vorhandene In-Memory-Factory; ein
      zusätzlicher Local-Function-Vertrag schloss die beim Engine-Audit gefundene Lücke.
      **step-018 ist nach dem in `e864407` committeten 24er Roh-Move und dem gruenen Recovery-Build
      erneut JIT-geplant:** Eine interne `ReadOnlySolutionSnapshot`-Seam trennt im MCP-Server die
      objektbasierte Ausfuehrung vom live refreshbaren Catalog, wie in Konzept §3 vorgesehen.
      Dadurch koennen 23 Analyzer-/Scanner-/Toolklassen gegen pfadfidele virtuelle SymbolGraph-,
      CompileError-, DI- und Faulting-Solutions nach FastTests wechseln; auch
      `DependencyGraphToolTests`, `GetFileSkeletonToolTests` und `GetSymbolBodyToolTests` bleiben im
      In-Memory-Schnitt. Nur `SuppressionScannerTests` wird vorwaertsgerichtet ins Legacy-Projekt
      zurueckverschoben, weil `ScanFile` selbst `File.Exists`/`File.ReadLines` als Produktvertrag
      besitzt. Live-Refresh bleibt Produktionsdefault und wird mit bestehenden Legacy-Vertraegen
      gegengeprueft. Nicht-C#-, Config-/Call-Log-, Git-, MSBuild-, Prozess-, Repo- und EPIC-5/6-
      Kohorten bleiben ausserhalb. **Step-018 ist nach der kumulativen Doku-Korrektur `b1a59b7`
      und dem Re-Audit `9cc8b73` approved. Als letzter EPIC-4-Grenzrest ist step-019 offen:**
      `FindSymbolScannerTests` und `FindSymbolToolTests` wurden in step-019 entlang ihrer echten
      Vertragsgrenze geteilt: elf Roslyn-/Dispatch-Vertraege laufen gegen die vorhandenen
      In-Memory-Snapshots in FastTests, neun bei C#-Leermenge ausgefuehrte Datei-Fallback-Vertraege
      gegen eine isolierte `SymbolGraphMini`-Diskkopie in IntegrationTests. Das Build-, gezielte
      Fast-/Integration- und Component-Grenzgate sind gruen; EPIC-4 ist damit implementiert und
      wartet auf das Step-019-Audit. Keine neue Produkt-Seam; die fuenf verbleibenden Datei-/Config-/
      Call-Log-/Git-Toolklassen gehoeren weiterhin EPIC-5/6.
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
