---
status: done
type: step-result
task: speedup-tests
step: 002
epic: EPIC-1
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-12
code_commit_hash: cd1c80f
status_after: done
blocker_category: n/a
---

# Result Step 002: Migrationsledger, Architekturguards und Baseline-Messung

## Zusammenfassung

Das vollständige Migrationsledger (183 Legacy-Testklassen, alle `pending`) existiert jetzt mit
maschinellem Konsistenzguard. Zwei Architekturguard-Ebenen (statische Metadaten-Deny-Liste +
Laufzeitcheck) schützen `AiNetLinter.FastTests`/`AiNetLinter.TestKit` gegen MSBuild-/
Prozess-Infrastruktur, je ein Kategorien-/Profilguard erzwingt gültige Traits pro Zielassembly. Die
Vorher-Baseline (Median über 3 Läufe je Profil, Build separat) ist dokumentiert.

## Geänderte Dateien

- `tasks/speedup-tests/test-migration-ledger.md` (neu) — Inventar aller 183 Legacy-Testklassen
  (Quelldatei, Testklasse, Produktbereich, Status, Legacy-Filter, neuer Abdeckungsort), Statuslegende
  und die vier Konsistenzregeln.
- `src/AiNetLinter.IntegrationTests/Migration/TestMigrationLedgerConsistencyTests.cs` (neu) —
  Ledger-Konsistenzguard (`Category=Integration`): scannt `src/AiNetLinter.Tests` per
  `CSharpSyntaxTree.ParseText` (kein Assembly-Load, da IntegrationTests das Legacy-Projekt nicht
  referenziert) und prüft alle vier Fehlerfälle aus Leitplanke 8 (fehlender Eintrag,
  migrated/consolidated mit noch existierender Legacy-Datei, migrated/consolidated ohne
  existierenden neuen Abdeckungsort, removed-trivial ohne Begründung).
- `src/AiNetLinter.FastTests/Architecture/FastTestsDependencyGuardTests.cs` (neu) — statischer
  Deny-Listen-Guard (`Category=Unit`) über `AssemblyRef`/`TypeRef`/`MemberRef`-Tabellen von
  `AiNetLinter.FastTests.dll`/`AiNetLinter.TestKit.dll`, gelesen via `System.Reflection.Metadata`
  (`PEReader`/`MetadataReader`), gegen `Microsoft.Build.*`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`,
  `MSBuildWorkspace`, `SourceFileCatalog.LoadAsync`, `System.Diagnostics.Process`.
- `src/AiNetLinter.FastTests/Architecture/FastTestsRuntimeDependencyGuardFixture.cs` (neu) —
  Laufzeit-Gegenstück: `ICollectionFixture`, deren `Dispose()` `AppDomain.CurrentDomain.GetAssemblies()`
  gegen dieselbe Deny-Liste prüft; an die Collection `FastTestsRuntimeDependencyGuard` gehängt statt
  die ganze Assembly zu serialisieren (Regel-Ref AiNetLinterRichtlinien.mdc §4).
- `src/AiNetLinter.FastTests/Architecture/TestCategoryProfileGuardTests.cs` (neu) —
  Kategorien-/Profilguard für `AiNetLinter.FastTests`: jede Testklasse mit `[Fact]`/`[Theory]`
  braucht genau einen Trait aus `{Unit, Component}`.
- `src/AiNetLinter.IntegrationTests/Architecture/TestCategoryProfileGuardTests.cs` (neu) — gleiches
  Prinzip für `AiNetLinter.IntegrationTests`, erlaubte Kategorien `{Integration, Dogfood,
  Performance, Stress}`.
- `tasks/speedup-tests/baseline-measurement.md` (neu) — Vorher-Baseline nach Leitplanke 10.

## Commit

- **Code-Commit-Hash:** `cd1c80f`
- **Message:**
  ```
  feat(tests): Migrationsledger, Architekturguards und Baseline-Messung [speedup-tests]

  Legt das vollstaendige Migrationsledger (183 Legacy-Testklassen, alle pending) mit
  maschinellem Konsistenzguard an (TestMigrationLedgerConsistencyTests, Roslyn-basierter
  Scan statt Assembly-Reflection, da AiNetLinter.IntegrationTests das Legacy-Projekt nicht
  referenziert). Ergaenzt zwei Architekturguard-Ebenen in AiNetLinter.FastTests: eine
  statische Deny-Liste ueber kompilierte Metadaten (AssemblyRef/TypeRef/MemberRef via
  System.Reflection.Metadata) gegen MSBuild-/Workspace-/Process-/SourceFileCatalog.LoadAsync-
  Referenzen sowie einen Laufzeitcheck ueber eine Collection-Fixture. Erzwingt zusaetzlich je
  einen Kategorien-/Profilguard fuer AiNetLinter.FastTests ({Unit, Component}) und
  AiNetLinter.IntegrationTests ({Integration, Dogfood, Performance, Stress}).

  Dokumentiert die Vorher-Baseline (Median ueber 3 Laeufe) fuer Category=Unit und
  Category!=Stress plus einmalig gestoppte Build-Zeit in baseline-measurement.md, inklusive
  einer bereits vor diesem Step bestehenden Flakiness in McpServerCommandJsonRpcFramingTests
  unter Volllast.

  Refs: tasks/speedup-tests/step-002
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx (nach dotnet clean)                                          → grün, 20,47 s, 0 Warnungen/Fehler, 5 Projekte
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests → grün (4 Tests)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~FastTestsDependencyGuardTests    → grün (2 Tests)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~TestCategoryProfileGuardTests    → grün (1 Test)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestCategoryProfileGuardTests → grün (1 Test)
dotnet test --filter Category=Unit (Baseline-Läufe 1-3, --no-build)                        → grün, Median-Wall-Clock 74,21 s, 1353 Tests
dotnet test --filter Category!=Stress (Baseline-Läufe 1+3, --no-build)                     → grün, Median-Wall-Clock 224 s, 1527 Tests
```

Details, Rohdaten und Median-Berechnung: `tasks/speedup-tests/baseline-measurement.md`.

## Abweichungen vom Plan

- **Datei 2 (Ledger-Konsistenzguard):** wie im Plan als „Umsetzungsdetail beim Coder" freigestellt,
  per Roslyn-`CSharpSyntaxTree.ParseText`-Scan statt Reflection auf eine geladene Testassembly
  umgesetzt (kein Assembly-Load von `AiNetLinter.Tests.dll` nötig, keine zusätzliche
  Testframework-Metadatenreferenz in `AiNetLinter.IntegrationTests`).
- **Datei 3 (Laufzeitcheck):** wie im Plan vorgesehen über eine `ICollectionFixture` gelöst statt
  über xUnit v3s natives `IAssemblyFixtureAttribute` (dieses Interface existiert zwar in
  `xunit.v3.core` 3.2.2, die zugehörige konkrete `AssemblyFixtureAttribute`-Klasse zum unmittelbaren
  Einsatz wurde in der referenzierten Paketversion nicht gefunden). Dokumentiert in der XML-Doc der
  Fixture-Klasse: der Check ist ein Best-Effort-Nachweis für den üblichen Lauf, keine absolute
  Prozessisolationsgarantie — das deckt sich mit der Definition-of-Done-Formulierung „durch Guards
  erkannt und rot", nicht „technisch unmöglich" (Leitplanke 6).
- **Ledger-Inventar:** 183 Zeilen statt der im JIT-Kontext geschätzten „183 mit mindestens einem
  Trait" — beide Zahlen stimmen exakt überein (eigenständig per Skript aus dem Bestand ermittelt,
  nicht aus dem Plan übernommen). Eine Datei (`DuplicateDetectionEngineFalsePositiveTests.cs`) ist
  eine `partial class`-Erweiterung von `DuplicateDetectionEngineTests` und teilt sich deshalb eine
  Ledger-Zeile mit zwei Quelldateien statt zwei getrennte Zeilen zu bekommen — sonst hätte der
  Konsistenzguard beim Scan der gleichnamigen Klasse fälschlich eine „doppelte" Erfassung gemeldet.
- **Baseline-Build-Zeit:** zusätzlich zum reinen `dotnet build` ein vorheriges `dotnet clean`
  eingefügt, weil ein Inkrementalbuild ohne Änderungen eine für den Vorher-/Nachher-Vergleich
  unbrauchbare Nahe-Null-Zeit gemessen hätte; der Plan spezifiziert das Cleanup nicht explizit, ist
  aber mit „Build von Messung trennen" (Leitplanke 10) konsistent.

## Beobachtungen

- **Nachtrag (step-003): Ledger-Konsistenzguard nachweislich rot bei simulierter Lücke.** Testweise
  die Zeile `ArchitectureTests` aus `tasks/speedup-tests/test-migration-ledger.md` entfernt und
  `dotnet test src/AiNetLinter.IntegrationTests --filter
  FullyQualifiedName~TestMigrationLedgerConsistencyTests` ausgeführt: Ergebnis rot (1 von 4 Tests
  fehlgeschlagen) mit Fehlermeldung `Testklassen ohne Ledger-Eintrag: ArchitectureTests` aus
  `AllLegacyTestClasses_HaveLedgerEntry`. Danach die Zeile exakt wiederhergestellt (`git diff` für
  `test-migration-ledger.md` leer) und denselben Testlauf erneut ausgeführt: grün (4 von 4 Tests).
- **Während der Baseline-Messung entdeckt (nicht durch step-002 verursacht):** ein `dotnet test
  --filter Category!=Stress`-Lauf auf frischem Build schlug mit 2 Fehlern in
  `AiNetLinter.Tests.Mcp.McpServerCommandJsonRpcFramingTests` fehl
  (`HandshakeOnly_AllStdoutLinesAreValidJsonRpcFrames`,
  `Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine`). Isoliert laufen beide
  sofort grün — die Fehlschläge traten nur unter der Prozesslast des vollen Parallel-Laufs auf. Das
  ist eine bereits vor step-002 bestehende Flakiness der Legacy-Suite unter Volllast (stdout-Framing
  gegen einen echten MCP-Subprozess), keine durch diesen Step eingeführte Regression. Details in
  `baseline-measurement.md` Abschnitt „Ausreißer/Fremdlast-Hinweis".
- **Selbst gefundene und selbst behobene Kollision (im Scope, da eigener neuer Code betroffen):**
  der ursprüngliche Entwurf von `TestMigrationLedgerConsistencyTests.cs` enthielt das Literal
  `"src/AiNetLinter.Tests"` als zusammenhängenden String. Da `AiNetLinter.IntegrationTests` von der
  bestehenden `FilterCliIntegrationTests`-Selbstlint-Prüfung (`ExcludeProjects = ["*.Tests"]`, matcht
  nur Projekte mit Suffix `.Tests`, **nicht** `AiNetLinter.IntegrationTests`) nicht ausgeschlossen
  wird, tauchte das Legacy-Projektname-Literal im Skeleton-Map-Output auf und ließ zwei bestehende
  Legacy-Tests (`SkeletonMap_ExcludeProjectByGlob_OutputExcludesTests`,
  `SkeletonMap_ExcludeNamespaceGlob_ExcludesAllTestNamespaces`) fehlschlagen. Behoben, indem der
  Pfad im eigenen neuen Code aus zwei Segmenten zusammengesetzt statt als ein Literal geschrieben
  wird (siehe Kommentar an der betroffenen Stelle). **Nicht selbst behoben, weil außerhalb des
  Scopes:** die zugrunde liegende Ursache — der Glob `*.Tests` in `FilterCliIntegrationTests`
  schließt nur `AiNetLinter.Tests` aus, nicht `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests`
  — bleibt bestehen. Jede künftige Datei in einem der drei neuen Projekte, die den zusammenhängenden
  String `"AiNetLinter.Tests"` enthält, kann dieselben zwei Legacy-Tests erneut zum Kippen bringen.
  Das betrifft auch schon vorhandenen Code aus step-001
  (`ProjectOverrideResolutionTests.cs` Zeile 12, XML-Doc-Kommentar) — dort bislang folgenlos, weil
  offenbar nicht im vom Skeleton-Map-Renderer erfassten Bereich, aber fragil.
- **`.agents/rules/AiNetLinter.mdc` driftet weiter:** während der Baseline-Messläufe (die echte
  Self-Lint-/CLI-Dogfood-Tests wie `CliIntegrationTests`/`SyncAgentRulesCommandTests` enthalten) wurde
  diese auto-synchronisierte Datei als Nebeneffekt lokal neu geschrieben und wich von der committeten
  Version ab (`*Tests`/`AiNetLinter.TestKit`-Overrides fehlten dort noch, obwohl `rules.json` sie
  bereits seit step-001 enthält). Bewusst **nicht** mitcommittet (außerhalb des Step-002-Scopes,
  keine step-002-Änderung an `rules.json`) — lokale Änderung per `git checkout` zurückgesetzt, damit
  der Commit sauber bleibt. Der Kritiker könnte das als eigenständigen, überfälligen
  Tech-Debt-Eintrag aus step-001 aufnehmen.

## Bekannte Unschärfen

- **Aggregierte Testzeit in der Baseline nur für `AiNetLinter.Tests`, nicht für
  `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests`:** `--logger LogFileName` gilt pro
  `dotnet test`-Aufruf, nicht pro Projekt; da ein Profil-Lauf mehrere Testhost-Prozesse (einen je
  Projekt) nacheinander im selben Aufruf startet, überschreibt der letzte Prozess die TRX der
  vorherigen. Die Wall-Clock der kleineren Projekte ist trotzdem vollständig aus der Konsolenausgabe
  jedes Laufs erfasst (siehe `baseline-measurement.md` Rohdaten) und fließt in die
  Gesamt-Wall-Clock ein — nur die *aggregierte* Testzeit (Summe der Einzeldauern) fehlt für die
  beiden kleinen Projekte. Bei aktuell 9 bzw. 8 Tests dort ist der Effekt auf die Gesamtzahl
  vernachlässigbar; das wird relevanter, sobald die ersten Fachkohorten nach `FastTests`/
  `IntegrationTests` migriert sind.
- **Laufzeitcheck-Fixture (Datei 3) garantiert keine strikte „letzte Position":** die
  `FastTestsRuntimeDependencyGuardFixture` prüft beim Dispose der Collection
  `FastTestsRuntimeDependencyGuard`, nicht beim Ende der gesamten Assembly — bei CPU-paralleler
  Ausführung mehrerer Collections ist nicht absolut garantiert, dass alle anderen Testklassen bereits
  gelaufen sind. Für den heutigen, noch kleinen Bestand an `FastTests`-Klassen ist das Risiko gering;
  bei wachsender Kohorte lohnt sich eine Prüfung, ob eine spätere xUnit-v3-Version die native
  `AssemblyFixture`-Attribut-Klasse mitliefert.
- **Ledger-Konsistenzguard prüft aktuell nur die Richtung „Testklasse → Ledger-Eintrag vorhanden",
  nicht umgekehrt** (verwaiste `pending`-Zeilen ohne mehr existierende Klasse). Das ist laut Plan
  über die vier benannten Fehlerfälle hinaus nicht gefordert (die betreffen nur `migrated`/
  `consolidated`/`removed-trivial`), aber relevant, falls künftig eine Legacy-Datei ohne
  Statusänderung im Ledger gelöscht wird.

## Modell-Info

- **coded_by_model:** claude-sonnet-5
- **coded_by_model_knowledge_cutoff:** 2026-01
