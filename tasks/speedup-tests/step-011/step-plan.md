---
status: open
type: step-plan
task: speedup-tests
step: 011
corrects: null
title: "EPIC-3 Teil 2 — Web-Parser-Kohorte (5 Klassen) nach AiNetLinter.FastTests migrieren"
epic: EPIC-3
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: "gpt-5.6-sol Medium"
created_by_model_knowledge_cutoff: "nicht ausgewiesen"
created_at: 2026-08-12
related_to: [step-010]
---

# Step 011: EPIC-3 Teil 2 — Web-Parser-Kohorte (5 Klassen) nach AiNetLinter.FastTests migrieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-3` aus `roadmap.md` — nach der in step-010 migrierten Checker-Kohorte sind die
  Parser- und Renderer-Kohorten noch offen; dieser Step deckt ausschließlich die Parser-Kohorte ab.
- **Konzept-Referenz:** `konzept.md` §9 „Sinnvolle Kohorten" Punkt 2 und Leitplanke 1
  (reine Parser-/Renderer-Tests in `AiNetLinter.FastTests`, ohne MSBuild, Prozess oder echtes Repo).

## Aktueller Projektzustand (JIT-Kontext)

- `src/AiNetLinter.Tests/Web/` enthält genau fünf `[Trait("Category", "Unit")]`-Testklassen:
  `CssAnalyzerTests` (15 Facts), `JsAnalyzerTests` (20), `RazorAnalyzerTests` (15),
  `RazorAnalyzerExtendedTests` (18) und `WebSuppressionDetectorTests` (6), insgesamt 74 Testfälle.
- Die Testklassen rufen ausschließlich die internen statischen Produktklassen unter
  `src/AiNetLinter/Web/` mit In-Memory-Strings und Konfigurationsobjekten auf. Sie referenzieren
  weder `TestHelper` noch `SourceFileCatalog`, MSBuild, Prozesse, Dateien, Verzeichnisse oder das
  echte Repository. Deshalb ist keine neue Fixture, kein neuer Helper und kein produktiver Seam
  erforderlich.
- `AiNetLinter.FastTests` referenziert das Produktprojekt bereits, besitzt seit step-004 die nötige
  `InternalsVisibleTo`-Freigabe und hat die statischen sowie Laufzeit-Deny-List-Guards aus EPIC-1.
  Diese bestehenden Strukturen werden unverändert wiederverwendet. Der in step-010 eingeführte
  `TestHelper` bleibt unberührt, weil die Web-Kohorte ihn nicht benötigt.
- Alle fünf Ledger-Zeilen stehen noch auf `pending`; unter `src/AiNetLinter.FastTests/Web/` existiert
  noch keine der Klassen. Im Tech-Debt-Index gibt es keinen Eintrag für diesen Bereich und keinen
  hier opportunistisch anzuhängenden `auto_fixable: ja`-Fund.
- Die Renderer-Klassen `CallTreeMermaidRendererTests` und `MetricsTreeRendererTests` bleiben bewusst
  außerhalb dieses Steps. Damit bleibt der Move als eine fachlich geschlossene, rein mechanische
  Parser-Kohorte in einer Review-Runde prüfbar.

## Intention

Nach diesem Step laufen alle 74 Web-Parser-/Textanalyse-Tests im schnellen Unit-Profil, ihre fünf
Legacy-Quelldateien sind physisch entfernt und das Migrationsledger zeigt die realen neuen
Abdeckungsorte. Testlogik und Assertions bleiben unverändert; außer Namespace und Ablageort ändert
sich kein Testvertrag.

## Konkrete Änderungen

### Verschiebung: fünf Dateien `src/AiNetLinter.Tests/Web/*.cs` → `src/AiNetLinter.FastTests/Web/*.cs`

- **Was:** Folgende Dateien mit unverändertem Dateinamen nach `src/AiNetLinter.FastTests/Web/`
  verschieben und ausschließlich den Namespace von `AiNetLinter.Tests.Web` auf
  `AiNetLinter.FastTests.Web` ändern:
  - `CssAnalyzerTests.cs`
  - `JsAnalyzerTests.cs`
  - `RazorAnalyzerTests.cs`
  - `RazorAnalyzerTests.Extended.cs` (enthält die Klasse `RazorAnalyzerExtendedTests`)
  - `WebSuppressionDetectorTests.cs`
  Alle `using`-Direktiven, `@covers`-Marker, Traits, Testmethoden und Assertions bleiben unverändert.
  Danach die fünf Legacy-Quelldateien physisch löschen; keine Parallelkopien oder Skips belassen.
- **Warum:** Die Klassen sind bereits reine Unit-Tests und erfüllen ohne Umbau die FastTests-Grenze.
  Der Ziel-Namespace folgt der bestehenden Verzeichnis-/Namespace-Konvention aus step-010.

### `tasks/speedup-tests/test-migration-ledger.md` — fünf Web-Zeilen aktualisieren

- **Was:** Die Zeilen für `CssAnalyzerTests`, `JsAnalyzerTests`, `RazorAnalyzerTests`,
  `RazorAnalyzerExtendedTests` und `WebSuppressionDetectorTests` von `pending` auf `migrated`
  setzen und als „Neuer Abdeckungsort" jeweils den existierenden Zielpfad unter
  `src/AiNetLinter.FastTests/Web/` eintragen. `last_updated` aktualisieren.
- **Warum:** Die Ledger-Konsistenzregeln verlangen bei gelöschter Legacy-Quelle einen Status mit
  existierendem neuen Abdeckungsort.

### `tasks/speedup-tests/codemap.md` — Parser-Kohorte auf den realen Zielzustand nachführen

- **Was:** Nach dem Move einen Pointer für `src/AiNetLinter.FastTests/Web/` als Ziel der fünf
  migrierten Parser-/Textanalyse-Testklassen ergänzen und den Legacy-Pointer
  `src/AiNetLinter.Tests/Web/` als durch step-011 obsolet markieren, nicht löschen.
- **Warum:** Der nächste JIT-Planer muss erkennen, dass innerhalb von EPIC-3 nur noch die
  Renderer-Kohorte offen ist, ohne die Parser-Migration erneut zu planen.

## Tests

- [ ] Vor dem Move Vergleichsbasis erfassen:
  `dotnet test src/AiNetLinter.Tests --filter FullyQualifiedName~AiNetLinter.Tests.Web` → 74 Tests
  grün. Falls der Working Tree den Move bereits enthält, die unveränderte Step-Start-Basis wie in
  step-010 in einem temporären, anschließend wieder entfernten Worktree messen.
- [ ] `dotnet build src/AiNetLinter.FastTests` → grün.
- [ ] `dotnet build src/AiNetLinter.Tests` → grün; das quarantänierte Legacy-Projekt bleibt trotz
  entfernter Web-Kohorte kompilierbar.
- [ ] `dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~AiNetLinter.FastTests.Web`
  → dieselben 74 Tests grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests`
  → alle Ledger-Konsistenzregeln grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~LegacyProjectBuildGateTests`
  → grün; weitere `pending`-Einträge halten das Legacy-Projekt im Solution-Build.
- [ ] `dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~FastTestsDependencyGuardTests`
  → grün.
- [ ] `dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~TestCategoryProfileGuardTests`
  → grün; alle fünf Klassen behalten genau den Unit-Trait.

Kein voller `Category!=Stress`-Lauf in diesem Step. EPIC-3 bleibt nach dieser Parser-Migration wegen
der noch offenen Renderer-Kohorte in Arbeit; das breite Profilgate gehört an die EPIC-3-Grenze nach
deren Migration beziehungsweise an das Task-Ende.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Die beiden gezielten Projekt-Builds und alle gefilterten Test-Commands aus „Tests" sind grün
- [ ] Vorher-/Nachher-Zahl der Parser-Kohorte stimmt mit 74 Testfällen überein
- [ ] Keine der fünf Legacy-Testklassen existiert parallel zur FastTests-Zielklasse
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch mit Suffix `[speedup-tests]`)
- [ ] `step-011/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` „architecture" und „Projekt-Overrides" — Namespace muss dem
  Zielordner entsprechen; der Testprojekt-Override erlaubt bis zu 100 Methodenzeilen und deaktiviert
  die Sealed-Pflicht, wobei die bereits versiegelten Klassen unverändert bleiben.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 „Windows-Umgebung & Tool-Regeln", §4
  „Test-Parallelität & MCP-Testing" und §5 „Sparsamer Einsatz von Code-Kommentaren" —
  Windows-kompatible Befehle verwenden, Unit-Tests ohne künstliche Collection-Serialisierung
  belassen und keine Task-/Step-Referenzen in C#-Kommentare einführen.

## Bekannte Ausnahmen

Keine.

## Notes

- Die beiden Produktparser-Backends (ExCSS für CSS und Esprima für JavaScript) kommen bereits über
  die Produktreferenz; keine PackageReference darf im FastTests-Projekt dupliziert werden.
- `RazorAnalyzerTests.Extended.cs` trägt absichtlich einen vom Dateinamen abweichenden Klassennamen
  (`RazorAnalyzerExtendedTests`); Ledger-Zeile und Testfilter richten sich nach dem Klassennamen,
  der Zielpfad bleibt nach dem Dateinamen gebildet.
- Renderer, Filtermatrix, WebFileSeparationChecker und produktive Parserlogik sind Non-Goals dieses
  Steps. Bei unerwarteten Fehlern nicht nebenbei refactoren, sondern die rein mechanische
  Migrationsgrenze bewahren und Abweichungen im Step-Result dokumentieren.
