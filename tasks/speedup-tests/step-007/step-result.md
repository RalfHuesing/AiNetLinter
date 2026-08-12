---
status: done
type: step-result
task: speedup-tests
step: 007
epic: EPIC-2
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-12
code_commit_hash: b2ebfbb
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 007: Testplattform-Fundament Teil 2 — MsBuildFixtureHost und IsolatedFixtureLease

## Zusammenfassung

Alle sechs im Plan benannten Dateien umgesetzt: `IsolatedFixtureLease` in `AiNetLinter.TestKit`
(reine Datei-I/O, keine MSBuild-/xUnit-Abhaengigkeit), `MsBuildFixtureHost` plus
Assembly-Fixture-Registrierung in `AiNetLinter.IntegrationTests` (echter einmaliger
`SourceFileCatalog.LoadAsync`-Load ueber `IAsyncLifetime`), sowie Vertragstests fuer beide Bausteine
in den jeweils richtigen Projekten. Die im Plan als unverifiziert markierte Kombination
`IAsyncLifetime` + `[assembly: AssemblyFixture(...)]` funktioniert wie erwartet — kein Fallback auf
synchronen Load im Konstruktor noetig.

## Geänderte Dateien

- `src/AiNetLinter.TestKit/IsolatedFixtureLease.cs` (neu) — `sealed class IsolatedFixtureLease :
  IDisposable`; statische Factory `CopyFixture(solutionRoot, fixtureFolderName, tempPrefix)` kopiert
  `tests/Fixtures/<Name>/` unter Auslassung von `bin`/`obj` in ein
  `Directory.CreateTempSubdirectory`-Verzeichnis; `Dispose()` loescht rekursiv, Cleanup-Fehler
  verschluckt (identisch zu `AiNetLinter.Tests.Fixtures.TestTempDirectory`).
- `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHost.cs` (neu) — `sealed class
  MsBuildFixtureHost : IAsyncLifetime`; `InitializeAsync()` ermittelt Solution-Root (gleiches
  `FindSolutionRoot`-Muster wie `ProjectOverrideRealSolutionTests`), kopiert `BaselineMini` via
  `IsolatedFixtureLease` und laedt sie einmal ueber `SourceFileCatalog.LoadAsync`; Properties
  `Catalog`/`Solution`; `DisposeAsync()` entsorgt Catalog dann Lease.
- `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHostAssemblyFixture.cs` (neu) — reine
  `[assembly: AssemblyFixture(typeof(MsBuildFixtureHost))]`-Registrierung, kein Testcode.
- `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHostTests.cs` (neu) — zwei Testklassen
  (`MsBuildFixtureHostTests`, `MsBuildFixtureHostSharedInstanceTests`), beide beziehen
  `MsBuildFixtureHost` per Konstruktor-Injektion; `Solution`/`Catalog` nicht `null`,
  `Solution.Projects` enthaelt `BaselineMini`; ordnungsunabhaengiger Nachweis "einmal geladen" ueber
  einen internen `SharedSolutionIdentityWitness`-Helfer, der bei jeder Instanziierung einer der
  beiden Klassen die `Solution`-Objektidentitaet gegen den zuerst gesehenen Wert prueft (wirft bei
  Abweichung), statt sich auf eine bestimmte Testreihenfolge zu verlassen.
- `src/AiNetLinter.FastTests/Platform/IsolatedFixtureLeaseTests.cs` (neu) — 4 Vertragstests:
  existierender `RootPath` mit erwarteten Quelldateien, zwei parallele `CopyFixture`-Aufrufe fuer
  denselben Ordnernamen liefern unabhaengige Pfade, `Dispose()` loescht das Temp-Verzeichnis,
  `bin`/`obj`-Auslassung gegen eine synthetisch erzeugte Quell-Kopie mit `bin`/`obj`-Unterordnern
  (die echte `BaselineMini`-Fixture enthaelt aktuell keinen `bin`-Ordner, siehe Plan-Notiz).
- `tasks/speedup-tests/codemap.md` — `AiNetLinter.TestKit`-Zeile um `IsolatedFixtureLease` ergaenzt,
  neuer Eintrag fuer `MsBuildFixtureHost.cs`/`MsBuildFixtureHostAssemblyFixture.cs`.

## Commit

- **Code-Commit-Hash:** `b2ebfbb`
- **Message:**
  ```
  feat(testkit): ergaenze MsBuildFixtureHost und IsolatedFixtureLease [speedup-tests]

  Baut die beiden nach step-006 verbleibenden Testplattform-Bausteine aus
  konzept.md §2: IsolatedFixtureLease kopiert eine kanonische Mini-Solution
  isoliert in ein Temp-Verzeichnis (TestKit, keine MSBuild-Abhaengigkeit).
  MsBuildFixtureHost laedt eine solche Kopie einmal echt via MSBuildWorkspace
  und teilt sie ueber eine xUnit-v3-Assembly-Fixture (IntegrationTests, damit
  FastTestsDependencyGuardTests weiterhin gruen bleibt).

  Refs: tasks/speedup-tests/step-007
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                                                                          → grün, 0 Warnungen/Fehler, 5 Projekte
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~IsolatedFixtureLeaseTests  → grün (4 Tests)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~FastTestsDependencyGuardTests → grün (2 Tests, TestKit.dll weiterhin ohne MSBuild-Referenz)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~Platform.MsBuildFixtureHost" → grün (3 Tests, inkl. klassenuebergreifender Identitaetspruefung)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~TestCategoryProfileGuardTests → grün (1 Test)
```

Kein voller `Category!=Stress`-Lauf durchgeführt — laut Plan-Tests-Abschnitt und Roadmap
Tech-Stack-Notiz für diesen Step bewusst nicht vorgesehen.

## Abweichungen vom Plan

Keine inhaltlichen Abweichungen — Plan 1:1 umgesetzt. Eine Klärung, die der Plan als offen markiert
hatte:

- **`IAsyncLifetime` + `[assembly: AssemblyFixture(...)]` funktioniert wie erwartet, kein Fallback
  nötig.** Der Plan markierte die konkrete Kombination aus `IAsyncLifetime`-Fixture und
  Assembly-Fixture-Registrierung ausdrücklich als beim Planen nicht im Bestand belegt (nur die
  Attribut-API selbst war aus step-006 verifiziert). `MsBuildFixtureHost` implementiert
  `IAsyncLifetime` mit `ValueTask InitializeAsync()`/`ValueTask DisposeAsync()`; `dotnet build` und
  der gefilterte Testlauf bestätigen, dass xUnit v3 diese Methoden vor bzw. nach den injizierenden
  Testklassen automatisch aufruft — kein synchroner `.GetAwaiter().GetResult()`-Fallback im
  Konstruktor nötig.
- **Filtertest-Namensueberschneidung entdeckt (kein Plan-Fehler, nur Diagnose-Detail):** Der im Plan
  vorgeschlagene Filter `FullyQualifiedName~MsBuildFixtureHostTests` trifft wegen reiner
  Substring-Matching-Logik von `dotnet test --filter` nur die Klasse `MsBuildFixtureHostTests`, nicht
  `MsBuildFixtureHostSharedInstanceTests` (Substring passt nicht). Fuer den tatsaechlichen
  Vertragstest ("beide Klassen sehen dieselbe Solution") oben stattdessen mit
  `FullyQualifiedName~Platform.MsBuildFixtureHost` verifiziert, das beide Klassen trifft.

## Beobachtungen

- Keine Beobachtungen außerhalb des Scopes dieses Steps.

## Bekannte Unschärfen

- **`bin`/`obj`-Auslassungstest arbeitet mit einer synthetisch erzeugten Quell-Kopie**, weil die
  echte `tests/Fixtures/BaselineMini`-Fixture aktuell keinen `bin`-Ordner enthaelt (nur `obj/`) — wie
  vom Plan bereits vorgesehen ("kein Test, der von einem zufaelligen Bestandszustand der Fixture
  abhaengt"). Die Synthese kopiert die echte Fixture zunaechst 1:1 in ein Temp-Verzeichnis und fuegt
  dort `bin`/`obj`-Dummy-Ordner hinzu, bevor `IsolatedFixtureLease.CopyFixture` darauf angewendet
  wird.
- **`MsBuildFixtureHostTests`/`MsBuildFixtureHostSharedInstanceTests` laufen nicht in einer
  eigenen Collection** — unkritisch, da `AiNetLinter.IntegrationTests/xunit.runner.json` bereits
  `parallelizeAssembly: false` setzt und beide Klassen ausschließlich lesend auf die geteilte
  `Solution` zugreifen.
- **`SharedSolutionIdentityWitness` ist ein interner Test-Helfer in derselben Datei**, keine eigene
  TestKit-Abstraktion — bewusst so belassen, da der Plan keinen wiederverwendbaren
  Identitaets-Pruefmechanismus fuer weitere Assembly-Fixtures verlangt; ein Kritiker koennte das bei
  Bedarf fuer kuenftige Assembly-Fixture-Vertragstests generalisieren.
