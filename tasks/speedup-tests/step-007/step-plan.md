---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 007
corrects: null
title: "Testplattform-Fundament Teil 2 — MsBuildFixtureHost und IsolatedFixtureLease"
epic: EPIC-2
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-12
related_to: [step-006]
---

# Step 007: Testplattform-Fundament Teil 2 — MsBuildFixtureHost und IsolatedFixtureLease

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-2` aus `roadmap.md` — nach step-006 sind `RoslynTestSolutionFactory` und
  `PreparedSolutionFixture` real im Bestand; offen bleiben die beiden übrigen Testplattform-Bausteine
  `MsBuildFixtureHost` und `IsolatedFixtureLease` (`FilterMini` bleibt bewusst weiter offen, siehe
  `roadmap.md`).
- **Konzept-Referenz:** `konzept.md` §2 „Gemeinsame Testplattform" Zeile 321-380 (vier Bausteine,
  davon zwei noch offen), §2 Zeile 345-358 (gecachte Referenzen/lazy Materialisierung/write-once gilt
  sinngemäß auch für den MSBuild-Pfad), §1 Testebenen-Tabelle Zeile 306-319 (Integration-Ebene erlaubt
  „kleine echte `.slnx`, MSBuild, Temp-Dateisystem").

## Aktueller Projektzustand (JIT-Kontext)

- **step-006 real im Bestand:** `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs` und
  `PreparedSolutionFixture.cs` (In-Memory-`AdhocWorkspace`-Pfad, Component-Ebene). Assembly-Fixture-
  Registrierungsmuster ist verifiziert im Bestand:
  `src/AiNetLinter.FastTests/Platform/PreparedSolutionAssemblyFixture.cs`
  (`[assembly: AssemblyFixture(typeof(PreparedSolutionFixture))]`, `Xunit.AssemblyFixtureAttribute`
  aus `xunit.v3.extensibility.core` 3.2.2, öffentlicher parameterloser Konstruktor Pflicht).
- **Wichtiger Fund, der den naiven Ansatz „alles in TestKit" verhindert:**
  `src/AiNetLinter.FastTests/Architecture/FastTestsDependencyGuardTests.cs` scannt die kompilierten
  Metadaten von **sowohl** `AiNetLinter.FastTests.dll` **als auch** `AiNetLinter.TestKit.dll` gegen
  eine Deny-Liste (`Microsoft.Build*`-Assembly-Refs, `Microsoft.CodeAnalysis.MSBuild`-/
  `MSBuildWorkspace`-TypeRefs, `SourceFileCatalog.LoadAsync`-MemberRef). Würde `MsBuildFixtureHost`
  in `AiNetLinter.TestKit` liegen und dort `SourceFileCatalog.LoadAsync`/`MSBuildWorkspace`
  referenzieren, würde `TestKitAssembly_DoesNotReferenceDeniedInfrastructure` sofort rot — dieser
  Step baut `MsBuildFixtureHost` deshalb bewusst **nicht** in `AiNetLinter.TestKit`, sondern in
  `AiNetLinter.IntegrationTests/Platform/` (dessen `.dll` von diesem Guard nicht gescannt wird, siehe
  auch `konzept.md`-Testebenen-Tabelle: MSBuild gehört auf die Integration-Ebene, nicht in ein von
  Unit/Component referenziertes TestKit).
- **`IsolatedFixtureLease` selbst braucht kein MSBuild** — konzeptionell nur Datei-/Verzeichniskopie
  in einen Temp-Bereich (`konzept.md` Zeile 337-339: „erstellt … eine eigene Kopie"). Dieser Baustein
  **kann** deshalb in `AiNetLinter.TestKit` liegen (verletzt die Deny-Liste nicht) und ist damit für
  `AiNetLinter.FastTests` und `AiNetLinter.IntegrationTests` gleichermaßen wiederverwendbar.
- **Bereits vorhandenes, wiederverwendbares Muster für die Kopierlogik:** Legacy
  `src/AiNetLinter.Tests/Fixtures/FixtureWorkspaceBase.cs` + `TestTempDirectory.cs` kopieren bereits
  eine kanonische Fixture (`tests/Fixtures/<Name>/`) unter Auslassung von `bin`/`obj` in ein
  `Directory.CreateTempSubdirectory`-Verzeichnis. `AiNetLinter.TestKit` referenziert
  `AiNetLinter.Tests` nicht (falsche Abhängigkeitsrichtung, Legacy ist Quarantäne-Projekt) — die Logik
  wird deshalb als eigenständiger `IsolatedFixtureLease`-Baustein in `TestKit` nachgebaut, nicht
  importiert. Gleiches Kopiermuster (Kopieren, `bin`/`obj` auslassen), keine neue Idee.
- **Bereits vorhandener echter MSBuild-Load-Aufrufer zum Vergleich:**
  `src/AiNetLinter.IntegrationTests/Configuration/ProjectOverrideRealSolutionTests.cs` ruft
  `SourceFileCatalog.LoadAsync(rootDir)` direkt gegen die **volle** `AiNetLinter.slnx` auf (bewusst,
  weil dort genau die echten neuen Projektnamen geprüft werden) — kein Kandidat für eine Migration auf
  `MsBuildFixtureHost`, dessen Konzept-Zweck eine **kanonische Mini-Solution** ist (`konzept.md` Zeile
  334: „kopiert eine kanonische Mini-Solution einmal"). Dieser Step migriert deshalb bewusst **keinen**
  bestehenden Legacy-Test auf `MsBuildFixtureHost` — das ist Sache der MSBuild-Kohorten-Migration in
  EPIC-5 (`roadmap.md`); dieser Step liefert nur den Baustein plus Vertragstests, analog zu step-006.
- **`src/AiNetLinter.IntegrationTests/xunit.runner.json`** setzt bereits `parallelizeAssembly: false`
  — eine Assembly-Fixture dort dient primär der Kostenersparnis (kein wiederholtes MSBuild-Laden pro
  Testklasse), nicht der Parallelitätssicherheit; muss trotzdem thread-safe sein, falls das künftig
  geändert wird (Konzept-Vertrag gilt unabhängig vom aktuellen Runner-Setting).
- **Unverifiziert vor der Umsetzung:** Ob `Xunit.AssemblyFixtureAttribute` in Kombination mit einer
  Fixture, die `IAsyncLifetime` implementiert (nötig, weil `SourceFileCatalog.LoadAsync` `Task`-basiert
  ist), von xUnit v3 automatisch aufgerufen wird (`InitializeAsync`/`DisposeAsync`). Laut offizieller
  Doku (https://xunit.net/docs/shared-context, bereits in step-006 gegen die tatsächliche
  `xunit.v3.extensibility.core`-DLL verifiziert) unterstützen Fixtures generell `IAsyncLifetime` —
  die konkrete Assembly-Fixture-Kombination ist aber noch nicht im Bestand belegt. Coder verifiziert
  das zuerst per Reflektion/kleinem Testlauf (wie in step-006 für `AssemblyFixtureAttribute` selbst
  getan); falls die Kombination nicht funktioniert, Fallback: synchroner Load im Konstruktor über
  `.GetAwaiter().GetResult()` (Muster bereits an anderer Stelle im Bestand unüblich, aber technisch
  zulässig) — Entscheidung dokumentiert im `step-result.md` „Abweichungen vom Plan", kein eigener
  Korrektur-Step nötig, solange der öffentliche Vertrag (`Catalog`/`Solution`-Property,
  write-once, lazy-genug für den gefilterten Einzeltestlauf) erhalten bleibt.

## Intention

Die beiden nach step-006 verbleibenden Testplattform-Bausteine aus `konzept.md` §2 real anlegen:
`IsolatedFixtureLease` (isolierte Temp-Kopie einer kanonischen Mini-Solution, wiederverwendbar in
`TestKit`) und `MsBuildFixtureHost` (einmaliger echter `MSBuildWorkspace`-Load einer solchen Kopie,
geteilt über eine xUnit-v3-Assembly-Fixture in `AiNetLinter.IntegrationTests`). Damit ist die
Testplattform-Infrastruktur für alle vier Konzept-Bausteine vollständig vorhanden, bevor die
MSBuild-lastige Testkohorte in EPIC-5 migriert wird — analog zu step-006, das den In-Memory-Pfad für
EPIC-3/4 vorbereitet hat.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.TestKit/IsolatedFixtureLease.cs` (neu)

- **Was:** `sealed class IsolatedFixtureLease : IDisposable`. Statische Factory-Methode
  `CopyFixture(string solutionRoot, string fixtureFolderName, string tempPrefix = "AiNetTestKit_")`,
  die `tests/Fixtures/<fixtureFolderName>/` unter Auslassung von `bin`/`obj`-Unterordnern nach
  `Directory.CreateTempSubdirectory(tempPrefix).FullName` kopiert (gleiche Kopier-/Auslassungslogik
  wie `FixtureWorkspaceBase.CopyFixture`/`IsGeneratedPath` im Legacy-Projekt, hier eigenständig
  implementiert wegen der Abhängigkeitsrichtung). Property `RootPath`. `Dispose()` löscht das
  Temp-Verzeichnis rekursiv, Cleanup-Fehler werden verschluckt (identisches Verhalten zu
  `TestTempDirectory.Dispose`).
- **Warum:** Baustein 4 aus `konzept.md` §2 („isolierte Kopie" für Mutations-Tests); muss ohne
  MSBuild-/xUnit-Abhängigkeit auskommen, damit er sowohl von `AiNetLinter.TestKit` selbst als auch
  von `MsBuildFixtureHost` (Datei 2) verwendbar ist, ohne die Dependency-Guard-Deny-Liste zu
  berühren.

### Datei 2: `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHost.cs` (neu)

- **Was:** `sealed class MsBuildFixtureHost : IAsyncLifetime`. `InitializeAsync()` ermittelt die
  Solution-Root (gleiches `FindSolutionRoot`-Muster wie in
  `ProjectOverrideRealSolutionTests.cs`/`LegacyProjectBuildGateTests.cs`), erzeugt via
  `IsolatedFixtureLease.CopyFixture(root, "BaselineMini")` eine isolierte Kopie und lädt sie genau
  einmal über `SourceFileCatalog.LoadAsync(lease.RootPath)`. Öffentliche Properties `Catalog`
  (`SourceFileCatalog`) und `Solution` (`Microsoft.CodeAnalysis.Solution`, delegiert an
  `Catalog.Solution`). `DisposeAsync()` entsorgt `Catalog` und die `IsolatedFixtureLease` in dieser
  Reihenfolge.
- **Warum:** Baustein 3 aus `konzept.md` §2 — der echte `MSBuildWorkspace`-Ladepfad für Tests, die
  ausdrücklich die Integration-Ebene brauchen, ohne dass jede Testklasse ihre eigene Temp-Kopie samt
  MSBuild-Load baut (aktuell dupliziert in mehreren `IntegrationTests`-Klassen als lokale
  `FindSolutionRoot`-Helper plus Direktaufruf).

### Datei 3: `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHostAssemblyFixture.cs` (neu)

- **Was:** Reine Registrierungsdatei, analog zu
  `src/AiNetLinter.FastTests/Platform/PreparedSolutionAssemblyFixture.cs`:
  `[assembly: AssemblyFixture(typeof(MsBuildFixtureHost))]`, kein Testcode.
- **Warum:** Macht `MsBuildFixtureHost` als geteilte, assembly-weite xUnit-v3-Fixture für
  `AiNetLinter.IntegrationTests` verfügbar (Konstruktor-Injektion in Testklassen), analog zum bereits
  verifizierten Muster aus step-006.

### Datei 4: `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHostTests.cs` (neu)

- **Was:** Vertragstests, `[Trait("Category", "Integration")]`. Mindestens: (a) `Solution`/`Catalog`
  sind nach Injektion nicht `null` und enthalten mindestens ein Projekt aus `BaselineMini`; (b) zwei
  Testklassen, die beide `MsBuildFixtureHost` per Konstruktor injizieren, erhalten dieselbe
  `Solution`-Objektidentität (Nachweis „einmal geladen", analog zum Referenz-Caching-Test aus
  step-006); (c) Fehlerpfad/Diagnose bei fehlendem Fixture-Ordner ist nicht Teil dieses Vertrags
  (kein synthetischer Negativfall nötig, `IsolatedFixtureLease.CopyFixture` wirft bereits die
  natürliche `DirectoryNotFoundException`, falls `tests/Fixtures/BaselineMini` fehlt — kein
  Sonderfall zu bauen).
- **Warum:** Belegt den Vertrag „einmaliger Load, geteilter read-only Snapshot" mechanisch, wie
  `RoslynTestSolutionFactoryTests`/`PreparedSolutionFixtureTests` für den In-Memory-Pfad in step-006.

### Datei 5: `src/AiNetLinter.FastTests/Platform/IsolatedFixtureLeaseTests.cs` (neu)

- **Was:** Vertragstests, `[Trait("Category", "Component")]` (reine Datei-I/O gegen eine kopierte
  `BaselineMini`, kein MSBuild, kein Prozess — passt auf die Component-Ebene). Mindestens: (a)
  `CopyFixture` liefert einen existierenden `RootPath` mit den erwarteten Quelldateien; (b) zwei
  parallele `CopyFixture`-Aufrufe für dieselbe `fixtureFolderName` liefern unterschiedliche,
  voneinander unabhängige Temp-Pfade (Isolation zwischen Leases); (c) `Dispose()` löscht das
  Temp-Verzeichnis; (d) kopierte `bin`/`obj`-Unterordner der Quell-Fixture fehlen im Ziel (falls
  `tests/Fixtures/BaselineMini` aktuell keine `bin`/`obj`-Ordner enthält: stattdessen synthetisch
  einen `bin`-Ordner mit Dummy-Datei in einer temporären Kopie der Quelle simulieren, bevor
  `CopyFixture` darauf angewendet wird — kein Test, der von einem zufälligen Bestandszustand der
  Fixture abhängt).
- **Warum:** `IsolatedFixtureLease` liegt in `AiNetLinter.TestKit` (keine eigene Testinfrastruktur),
  Vertragstests dafür gehören analog zu step-006 in `AiNetLinter.FastTests/Platform` (Component-Ebene,
  kein MSBuild nötig für den reinen Kopiervertrag).

### Datei 6: `tasks/speedup-tests/codemap.md`

- **Was:** Neue Einträge für `IsolatedFixtureLease.cs`, `MsBuildFixtureHost.cs`,
  `MsBuildFixtureHostAssemblyFixture.cs` und die beiden neuen Testdateien; TestKit-Zeile um
  `IsolatedFixtureLease` ergänzen.
- **Warum:** Pointer-Pflicht für kommende Steps (`../spec.md` §5), analog zum codemap-Update in
  step-006.

## Tests

- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~IsolatedFixtureLeaseTests`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~MsBuildFixtureHostTests`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~FastTestsDependencyGuardTests`
      (muss weiterhin grün bleiben — Nachweis, dass `TestKit.dll` trotz `IsolatedFixtureLease` keine
      MSBuild-Referenz zieht)
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestCategoryProfileGuardTests`
      (neue Testklassen tragen genau einen gültigen Kategorie-Trait)

Kein voller `Category!=Stress`-Lauf für diesen Step (Konzept §7 „Sparsame Verifikation", identisch zur
Begründung in step-006).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Gezielte Test-Filter oben grün (kein Vollauf nötig laut Konzept §7)
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-007/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 „Testsuite-Parallelität bewahren" (Zeile 82-93) —
  `MsBuildFixtureHost` als Assembly-Fixture statt zwangsserialisierender Collection; falls die
  `IAsyncLifetime`-Kombination nicht trägt und ein Fallback nötig wird, muss der Fallback ebenfalls
  ohne pauschale Collection-Serialisierung auskommen (begründete gezielte Lösung, nicht die ganze
  Assembly sperren — `AiNetLinter.IntegrationTests` hat ohnehin bereits `parallelizeAssembly: false`
  im `xunit.runner.json`, das reicht als Rahmen).
- `.agents/rules/AiNetLinter.mdc` — `sealed` auf `IsolatedFixtureLease`/`MsBuildFixtureHost`,
  `#nullable enable`, Parameterzahl-Grenzwert (`CopyFixture` hat 3 Parameter, unkritisch).

## Bekannte Ausnahmen

- Keine.

## Notes

- **Kein Legacy-Migrations-Zwang in diesem Step:** `ProjectOverrideRealSolutionTests.cs` bleibt
  unverändert (lädt bewusst die volle Solution, kein `MsBuildFixtureHost`-Kandidat, siehe „Aktueller
  Projektzustand"). Die MSBuild-lastige Kohorte (`SourceFileCatalogRegisterMSBuildTests`,
  `SourceFileCatalogBlazorPartialTests` u. a., siehe `codemap.md` „Laufzeit-Hotspots") wird in EPIC-5
  migriert, nicht hier.
- **`IsolatedFixtureLease` bewusst nicht xUnit-abhängig:** `AiNetLinter.TestKit` referenziert laut
  `codemap.md` weiterhin keine xUnit-Pakete — reine `System.IO`-Logik, kein `IAsyncLifetime`/
  `IDisposable`-Xunit-Interface außer dem Standard-`System.IDisposable`.
- **Falls die `IAsyncLifetime`+`AssemblyFixture`-Kombination nicht funktioniert** (siehe offene
  Verifikationsfrage oben): Fallback synchron im Konstruktor, Vertrag (Properties, Einmal-Load,
  Dispose-Reihenfolge) bleibt unverändert — kein Grund für einen Korrektur-Step, nur eine im
  `step-result.md` dokumentierte Abweichung vom ursprünglich skizzierten Umsetzungsweg.
