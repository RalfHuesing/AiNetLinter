---
status: done
type: step-review
task: speedup-tests
step: 007
epic: EPIC-2
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-12
verdict: approved
tech_debt_ids: [TD-004]
---

# Review Step 007: Testplattform-Fundament Teil 2 — MsBuildFixtureHost und IsolatedFixtureLease

## Verdict

- [x] **approved** — alle vier Prüfebenen ok

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (kuratierte Auswahl) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

Alle sechs geplanten Dateien real im Bestand, `MsBuildFixtureHost` korrekt in
`AiNetLinter.IntegrationTests/Platform/` statt `TestKit` platziert, `FastTestsDependencyGuardTests`
weiterhin grün (selbst nachgeprüft). Rules-Konformität eingehalten (Assembly-Fixture statt
zwangsserialisierender Collection, `sealed`, `#nullable enable`). Logik korrekt: `IsolatedFixtureLease`
ist reine Datei-I/O ohne MSBuild-/xUnit-Bezug, `MsBuildFixtureHost` lädt via `IAsyncLifetime` genau
einmal und wird über die assembly-weite Fixture geteilt; die klassenübergreifende Identitätsprüfung ist
thread-safe per Lock, was angesichts `parallelizeTestCollections: true` auch nötig ist. Konzept-Treue
gegeben — beide fehlenden Bausteine aus `konzept.md` §2 sind jetzt vollständig vorhanden, keine
Non-Goals berührt.

### Plan-Erfüllung

Alle 6 „Konkrete Änderungen" erfüllt:
1. `IsolatedFixtureLease.cs` (TestKit) — erfüllt, geprüft per `git show`.
2. `MsBuildFixtureHost.cs` (IntegrationTests/Platform) — erfüllt, korrekt **nicht** in TestKit
   platziert (die im Plan als zentrale Architekturentscheidung benannte Platzierung).
3. `MsBuildFixtureHostAssemblyFixture.cs` — erfüllt, reine Registrierung.
4. `MsBuildFixtureHostTests.cs` — erfüllt, zwei Testklassen, Identitätsnachweis über
   `SharedSolutionIdentityWitness` statt Testreihenfolge-Annahme (robuster als im Plan minimal
   gefordert).
5. `IsolatedFixtureLeaseTests.cs` (FastTests/Platform) — erfüllt, 4 Vertragstests inkl. synthetischer
   `bin`/`obj`-Quelle (da echte `BaselineMini`-Fixture aktuell keinen `bin`-Ordner hat — Plan hat
   diesen Fall bereits vorgesehen).
6. `codemap.md` — erfüllt, TestKit-Zeile ergänzt plus neuer Eintrag für `MsBuildFixtureHost.cs`.

Alle vier Test-Checkboxen aus dem Plan selbst nachgeprüft und grün (siehe Build-/Test-Status unten).

### Rules-Konformität

- `AiNetLinterRichtlinien.mdc` §4 „Testsuite-Parallelität bewahren": eingehalten.
  `MsBuildFixtureHostTests`/`MsBuildFixtureHostSharedInstanceTests` sind zwei separate,
  nicht-serialisierte Testklassen (keine `[Collection(...)]`, kein `DisableParallelization`); die
  Shared-Solution-Identität wird stattdessen über einen gezielten `lock` in
  `SharedSolutionIdentityWitness` abgesichert — exakt das von der Regel geforderte Muster
  (gezielte Lösung statt Collection-Zwangsserialisierung). `AiNetLinter.IntegrationTests` hat
  bereits `parallelizeAssembly: false`, aber `parallelizeTestCollections: true` — die beiden neuen
  Testklassen laufen als unterschiedliche Default-Collections potenziell parallel, der Lock ist
  also nicht nur Vorsicht, sondern notwendig.
- `AiNetLinter.mdc`: `sealed` auf `IsolatedFixtureLease`/`MsBuildFixtureHost` vorhanden,
  `#nullable enable` in allen neuen Dateien, `CopyFixture` mit 3 Parametern unkritisch.
- Zufällig aufgefallen (nicht Teil der kuratierten Rules-Refs, daher kein Ebene-2-Finding, siehe
  Tech-Debt-Eintrag unten): `MsBuildFixtureHostTests.cs:14` referenziert „step-006" in einem
  XML-Doc-Kommentar — verstößt gegen `AiNetLinterRichtlinien.mdc` §5 „Sparsamer Einsatz von
  Code-Kommentaren" (Verbot von Task-/Planungsartefakt-Referenzen im Code). Der Plan zitiert für
  diesen Step nur §4, nicht §5.

### Logische Korrektheit

- `IsolatedFixtureLease.CopyFixture`: kopiert rekursiv, lässt `bin`/`obj` auf jeder Verzeichnisebene
  aus (`IsGeneratedPath` prüft alle Pfadsegmente, nicht nur Top-Level) — korrekt und robuster als
  nötig. `Dispose()` verschluckt Cleanup-Fehler wie spezifiziert.
- `MsBuildFixtureHost`: `InitializeAsync` lädt einmal, `Catalog`/`Solution`-Properties werfen vor
  Initialisierung eine sprechende `InvalidOperationException` statt `NullReferenceException`.
  `DisposeAsync` entsorgt in der geplanten Reihenfolge (Catalog vor Lease).
  `IAsyncLifetime`+`[assembly: AssemblyFixture(...)]` funktioniert wie vom Coder berichtet — selbst
  reproduziert (3 grüne Tests, `Solution`-Identität über zwei Testklassen hinweg bestätigt, siehe
  unten).
- Abweichung „Filter `Platform.MsBuildFixtureHost` statt `MsBuildFixtureHostTests`" selbst verifiziert
  (siehe Build-/Test-Status): der im Plan vorgeschlagene engere Filter listet tatsächlich nur die 2
  Tests aus `MsBuildFixtureHostTests`, nicht die 1 aus `MsBuildFixtureHostSharedInstanceTests`
  (`dotnet test --list-tests` bestätigt). Der vom Coder gewählte Filter `Platform.MsBuildFixtureHost`
  trifft beide Klassen (3 Tests gesamt) und erbringt damit tatsächlich den Nachweis der geteilten
  `Solution`-Identität über beide Testklassen — die Abweichung ist korrekt begründet und der Nachweis
  ist erbracht.
- `FastTestsDependencyGuardTests` selbst neu ausgeführt: weiterhin grün, `TestKitAssembly_...` prüft
  weiterhin `TestKit.dll`, keine MSBuild-Referenz eingeschleppt.

### Konzept-Treue (Ebene 4)

Beide nach step-006 verbleibenden Bausteine aus `konzept.md` §2 sind jetzt vollständig vorhanden.
Kein Non-Goal umgesetzt (keine Legacy-Migration, wie im Plan explizit ausgeschlossen).
`IsolatedFixtureLease` liegt bewusst in `TestKit`, `MsBuildFixtureHost` bewusst in
`IntegrationTests` — deckt sich mit der Testebenen-Tabelle (`konzept.md` Zeile 306-319: MSBuild
gehört auf die Integration-Ebene). Assembly-Fixture-Wahl statt Collection-Fixture deckt sich mit
Zeile 365-376 („Assembly-Fixture darf immutable read-only Snapshots teilen, ohne Testklassen allein
wegen des Sharings in eine gemeinsame serielle Collection zu zwingen"). Scope entspricht der
Plan-Intention, weder größer noch kleiner.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx                                                                              → grün, 0 Warnungen/Fehler
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~IsolatedFixtureLeaseTests               → grün (4 Tests)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~FastTestsDependencyGuardTests           → grün (2 Tests)
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~Platform.MsBuildFixtureHost"    → grün (3 Tests, beide Klassen)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestCategoryProfileGuardTests    → grün (1 Test)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~MsBuildFixtureHostTests --list-tests
    → listet nur 2 Tests (bestätigt die vom Coder gemeldete Filter-Lücke des Plan-Vorschlags)
```

## Tech-Debt-Einträge aus diesem Review

- `TD-004` (siehe `tech-debt.md`) — `MsBuildFixtureHostTests.cs:14` referenziert „step-006" in einem
  XML-Doc-Kommentar, verstößt gegen das Code-Kommentar-Verbot für Task-Artefakt-Referenzen
  (`AiNetLinterRichtlinien.mdc` §5), auto-fixable.
