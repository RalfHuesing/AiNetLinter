---
status: done
type: step-result
task: speedup-tests
step: 006
epic: EPIC-2
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-12
code_commit_hash: f258992
status_after: done
blocker_category: n/a
---

# Result Step 006: Testplattform-Fundament Teil 1 — RoslynTestSolutionFactory und PreparedSolutionFixture

## Zusammenfassung

Alle sechs im Plan benannten Dateien umgesetzt: `RoslynTestSolutionFactory` und
`PreparedSolutionFixture` in `AiNetLinter.TestKit`, die xUnit-v3-Assembly-Fixture-Registrierung
sowie zwei neue Vertragstestklassen in `AiNetLinter.FastTests/Platform`, und die Migration von
`LinterEngineSolutionAnalysisTests` auf die neue Factory. Die im Plan als unverifiziert markierte
xUnit-v3-Assembly-Fixture-API (`Xunit.AssemblyFixtureAttribute`) existiert in 3.2.2 genau wie in der
offiziellen Doku beschrieben — kein Fallback auf `ICollectionFixture` nötig.

## Geänderte Dateien

- `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs` (neu) — statischer, mehrprojekt-faehiger
  `AdhocWorkspace`-Solution-Builder; `ProjectSpec`-Record, `RoslynTestSolution`-Record (`IDisposable`),
  einmalig gecachter `CoreReferences`-Kernsatz (`Lazy<ImmutableArray<MetadataReference>>`).
- `src/AiNetLinter.TestKit/PreparedSolutionFixture.cs` (neu) — `ConcurrentDictionary<string,
  Lazy<RoslynTestSolution>>`-Cache, `GetOrCreate(scenarioName, factory)` mit
  `LazyThreadSafetyMode.ExecutionAndPublication`, `Dispose()` entsorgt nur materialisierte Eintraege.
- `src/AiNetLinter.FastTests/Platform/PreparedSolutionAssemblyFixture.cs` (neu) — reine
  `[assembly: AssemblyFixture(typeof(PreparedSolutionFixture))]`-Registrierung, kein Testcode.
- `src/AiNetLinter.FastTests/Platform/RoslynTestSolutionFactoryTests.cs` (neu) — 5 Vertragstests:
  Mehrprojekt-Referenzaufloesung, Nullable-Context-Diagnosen (CS8600), Preprocessor-Symbole,
  Referenz-Caching (Objektidentitaet der corlib-Referenz), Fehlerpfad bei unbekanntem Projektnamen.
- `src/AiNetLinter.FastTests/Platform/PreparedSolutionFixtureTests.cs` (neu) — 3 Vertragstests: lazy
  Materialisierung (zweite Factory nie ausgefuehrt), Isolation zwischen Szenarien, Thread-Sicherheit
  bei 16 parallelen `GetOrCreate`-Aufrufen fuer denselben neuen Szenarionamen.
- `src/AiNetLinter.FastTests/Core/LinterEngineSolutionAnalysisTests.cs` — lokale
  `CreateAdhocSolution`-Helper-Methode entfernt, nutzt jetzt
  `RoslynTestSolutionFactory.CreateSolution(new ProjectSpec(...))`; Assertions unveraendert.
- `tasks/speedup-tests/codemap.md` — Eintraege fuer `AiNetLinter.TestKit` (jetzt mit Code) und die
  drei neuen `Platform/`-Dateien ergaenzt/aktualisiert.

## Commit

- **Code-Commit-Hash:** `f258992`
- **Message:**
  ```
  feat(testkit): baue RoslynTestSolutionFactory und PreparedSolutionFixture [speedup-tests]

  Legt die deklarative Testplattform-Kernbausteine aus konzept.md §2 real an:
  RoslynTestSolutionFactory (mehrprojekt-faehiger AdhocWorkspace-Solution-Builder
  mit einmalig gecachtem MetadataReference-Kernsatz) und PreparedSolutionFixture
  (thread-sicherer, pro Szenario lazy materialisierender Assembly-Fixture-Cache,
  registriert ueber xunit.v3 AssemblyFixtureAttribute). Migriert die lokale
  CreateAdhocSolution-Helper-Methode aus LinterEngineSolutionAnalysisTests auf die
  neue Factory als ersten echten Konsumenten und ergaenzt Vertragstests fuer beide
  Bausteine (Mehrprojekt-Referenzen, Nullable-Context, Preprocessor-Symbole,
  Referenz-Caching, Fehlerpfad, Lazy-Materialisierung, Isolation, Thread-Sicherheit).

  Refs: tasks/speedup-tests/step-006
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                                                                      → grün, 0 Warnungen/Fehler, 5 Projekte
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~LinterEngineSolutionAnalysisTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~PreparedSolutionFixtureTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (12 Tests, 0 Fehler)
```

Kein voller `Category!=Stress`-Lauf durchgeführt — laut Plan-Tests-Abschnitt und Roadmap
Tech-Stack-Notiz für diesen Step bewusst nicht vorgesehen ("kein voller Category!=Stress-Lauf für
diesen Step; die oben gefilterten Läufe decken den geänderten Vertrag ab").

## Abweichungen vom Plan

Keine inhaltlichen Abweichungen — Plan 1:1 umgesetzt. Eine Klärung, die der Plan als offen markiert
hatte:

- **xUnit-v3-Assembly-Fixture-API verifiziert, kein Fallback nötig.** Der Plan markierte die
  `Xunit.v3`-Assembly-Fixture-Syntax in 3.2.2 ausdrücklich als beim Planen nicht aus Bytecode
  verifiziert und erlaubte einen `ICollectionFixture`-Fallback nur für die neuen Platform-Tests.
  Verifiziert vor der Implementierung gegen die tatsächliche `xunit.v3.core.xml`-Dokumentation im
  lokalen NuGet-Cache (`xunit.v3.extensibility.core/3.2.2/lib/netstandard2.0/`, da
  `xunit.v3.core` selbst nur Platzhalter-DLLs enthält und die echte Implementierung über die
  `mtp-v1`-Kette aus `xunit.v3.extensibility.core` kommt): `Xunit.AssemblyFixtureAttribute`
  existiert genau wie in der offiziellen Doku (https://xunit.net/docs/shared-context) beschrieben —
  `[assembly: AssemblyFixture(typeof(T))]`, Fixture braucht öffentlichen parameterlosen Konstruktor,
  Testklassen erhalten Zugriff über einen Konstruktorparameter vom exakten Fixture-Typ. Datei 3
  nutzt diese echte API direkt, kein Fallback.

## Beobachtungen

- Keine Beobachtungen außerhalb des Scopes dieses Steps.

## Bekannte Unschärfen

- **`PreparedSolutionFixtureTests` und `RoslynTestSolutionFactoryTests` laufen nicht in der
  `FastTestsRuntimeDependencyGuard`-Collection** (anders als `FastTestsDependencyGuardTests`) —
  bewusst so belassen, da der Plan das nicht verlangt und die neuen Platform-Tests keine
  MSBuild-/Workspace-Infrastruktur laden; der Deny-Listen-Guard (statisch, über Metadaten) deckt
  `AiNetLinter.TestKit.dll` bereits vollständig ab.
- **Reference-Caching-Test prüft nur die corlib-Referenz stellvertretend** (wie im Plan als Beispiel
  vorgegeben: "z. B. der mscorlib/System.Private.CoreLib-Referenz"), nicht den gesamten
  `CoreReferences`-Satz Eintrag für Eintrag — für den Nachweis "wirklich einmal gebaut" ausreichend,
  aber ein Kritiker könnte eine vollständige Mengen-Prüfung (`SequenceEqual` über alle Referenzen als
  Objektidentität) als robusteren Nachweis vorziehen.
