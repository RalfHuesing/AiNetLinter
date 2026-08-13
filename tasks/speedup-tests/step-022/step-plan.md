---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 022
corrects: step-021
title: "Korrektur: globales MSBuild-Loadgate und read-only Server-Ownership"
epic: EPIC-5
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-021/step-review.md
---

# Step 022: Korrektur: globales MSBuild-Loadgate und read-only Server-Ownership

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-5` aus `roadmap.md` — korrigiert ausschliesslich die zwei MAJOR-Findings aus
  Step 021; die weitere EPIC-5-Migration bleibt ausserhalb.
- **Korrigierter Step:** `step-021`; Fixbudget 6, erste Korrektur dieser Kette.
- **Review-Quelle:** `step-021/step-review.md` Findings 1 und 2.

## Aktueller Projektzustand (JIT-Kontext)

`LoadedFixture` besitzt bereits das einzige statische `SemaphoreSlim` mit Kapazitaet 2, fuehrt
den Permit-Code aber doppelt in `CreateAsync` und `LoadCatalogAsync`. Alle in Step 021
hinzugekommenen Loads nutzen diesen Pfad; zwei aeltere reale Loads in
`FilterMiniFidelityTests` und `ProjectOverrideRealSolutionTests` rufen
`SourceFileCatalog.LoadAsync` dagegen direkt auf. Damit ist das Budget nicht assembly-weit
durchgesetzt und sein Maximalwert wird bisher nur aus der Implementierung, nicht durch einen
deterministischen Vertrag belegt.

`SymbolGraphCatalogFixture` besitzt einen per `LoadedFixture` geladenen
`SourceFileCatalog`, exponiert ihn aber direkt. `GetServerHealthToolTests`,
`GetIndexScopeToolTests` und `SearchPatternToolTests` reichen diesen gemeinsamen Owner an
`McpCodeGraphServer` weiter. Der normale Catalog-Pfad des Servers adoptiert Ownership und
`Dispose()` schliesst deshalb den Fixture-Workspace. Der seit Step 018 vorhandene
`ReadOnlySolutionSnapshot`-Pfad ist genau die benoetigte bestehende Produkt-Seam: Er baut einen
server-eigenen `SourceFileCatalog` ohne `MSBuildWorkspace`, deaktiviert Refresh und kann sicher
disposed werden. Eine neue Ownership-Option, ein Testschalter im Produkt oder eine Aenderung an
`SourceFileCatalog.Dispose()` waere daher unnoetig und ist nicht zu planen.

Die CodeMap weist `LoadedFixture` und die produktive Snapshot-Seam bereits aus, aber noch nicht
die Besitzgrenze der neuen `SymbolGraphCatalogFixture`; dieser Pointer wird mit dem Plan ergaenzt.
Roadmap und Migrationsledger bleiben unveraendert: Der Korrekturstep aendert weder Epic-Scope noch
Migrationsstatus oder Zielpfade. TD-010 bleibt in Wortlaut und Status unveraendert.

## Intention

Nach dem Step kann innerhalb von `AiNetLinter.IntegrationTests` kein realer
`SourceFileCatalog.LoadAsync`-Aufruf das gemeinsame Maximalbudget 2 umgehen. Das Gate ist bei
Erfolg, Exception und Cancellation permit-sicher und sein Vertrag wird ohne reale Last oder
Timing-Raten deterministisch nachgewiesen.

Read-only SymbolGraph-Server erhalten nur noch eine nichtbesitzende `Solution`-Sicht und besitzen
jeweils ausschliesslich ihren workspace-losen Adapter. Das Fixture bleibt alleiniger Owner des
gemeinsamen MSBuild-Katalogs; das Dispose eines Servers darf weder einen weiteren parallelen Leser
noch einen spaeteren Leser desselben Snapshots beeintraechtigen.

## Konkrete Änderungen

### Globalen Load-Pfad zentralisieren — `src/AiNetLinter.IntegrationTests/Platform/LoadedFixture.cs`

- Genau einen exception-sicheren Gate-Kern fuer reale Loads behalten, beispielsweise
  `ExecuteWithinLoadBudgetAsync<T>(Func<CancellationToken, Task<T>>, CancellationToken)`, mit
  genau einem `WaitAsync`/`try-finally`/`Release`-Pfad und expliziter Konstante
  `MaxConcurrentLoads = 2`.
- `LoadCatalogAsync` delegiert den echten `SourceFileCatalog.LoadAsync`-Aufruf samt
  `CancellationToken` an diesen Kern;
  `CreateAsync` verwendet wiederum `LoadCatalogAsync`, statt das Semaphore-Protokoll zu
  duplizieren. Der vorhandene Catch muss die bereits erzeugte `IsolatedFixtureLease` auch bei
  Cancellation oder Loadfehler weiter entsorgen.
- Der generische Gate-Kern bleibt `internal` in der Integrationstest-Plattform, damit ein kleiner
  kontrollierter Vertrag Dummy-Loads blockieren/werfen lassen kann. Er ist keine Produkt-Seam und
  darf weder nach `AiNetLinter` noch nach `TestKit` verschoben werden.

### Die zwei Umgehungen entfernen —
`src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs` und
`src/AiNetLinter.IntegrationTests/Configuration/ProjectOverrideRealSolutionTests.cs`

- `FilterMiniFidelityTests` verwendet `await using var loaded = await
  LoadedFixture.CreateAsync("FilterMini")`. Damit besitzt ein Owner Kopie plus Katalog und der
  lokale `FindSolutionRoot`-/manuelle Drei-Objekt-Cleanup kann entfallen; alle fachlichen
  Fidelity-Assertions bleiben unveraendert.
- `ProjectOverrideRealSolutionTests` laedt das echte Repository weiterhin pro Theory-Fall, aber
  ausschliesslich ueber `LoadedFixture.LoadCatalogAsync(rootDir)` und disposed den von diesem Test
  besessenen Katalog. Keine Umstellung auf eine Fixture, weil der Vertrag bewusst die aktuelle
  reale Solution prueft.
- Assembly-weiter statischer Guard: Unter `src/AiNetLinter.IntegrationTests/**/*.cs` darf die
  Zeichenfolge `SourceFileCatalog.LoadAsync(` danach nur noch in `LoadedFixture.cs` vorkommen.
  Dadurch belegt der Max-2-Vertrag nicht nur den Semaphore-Helper, sondern zusammen mit dem Guard
  auch die Vollstaendigkeit des Pfads fuer alle heutigen Step-021-Real-Loads.

### Loadbudget-Verträge ergänzen —
`src/AiNetLinter.IntegrationTests/Platform/LoadedFixtureTests.cs`

- Einen kleinen, deterministischen Test mit drei kontrollierten asynchronen Delegates schreiben:
  zwei duerfen gleichzeitig in den Gate-Kern eintreten, die dritte bleibt bis zur Freigabe
  draussen; `Interlocked`-Zaehler und `TaskCompletionSource` belegen exakt
  `maxObservedConcurrency == LoadedFixture.MaxConcurrentLoads == 2`. Keine Sleeps, keine echten
  MSBuild-Loads und keine Stress-Kategorie.
- Separat einen werfenden bzw. abgebrochenen Delegate-Pfad pruefen und danach zwei neue Delegates
  erfolgreich gleichzeitig eintreten lassen. So wird gezeigt, dass kein Permit nach Exception
  oder Cancellation verloren geht; Timeouts nur als Deadlock-Sicherung, nicht als
  Erfolgskriterium.
- Den statischen Vollstaendigkeitsguard fuer direkte `SourceFileCatalog.LoadAsync`-Aufrufe in
  derselben Plattform-Testklasse oder einer schmalen Architektur-Testklasse ablegen.

### Fixture-Ownership auf Snapshot begrenzen —
`src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraphCatalogFixture.cs`

- Die oeffentliche `Catalog`-Property entfernen, damit der besessene Katalog nicht mehr an
  Server weitergereicht werden kann. Nur `Workspace.RootPath` und eine read-only
  `Solution`-/`Snapshot`-Property exponieren; `DisposeAsync` des Fixtures bleibt allein fuer
  `LoadedFixture` verantwortlich.
- Einen schmalen Fixture-Helper fuer Serveroptionen oder Servererzeugung bereitstellen, der
  `Catalog: null` und `ReadOnlySolutionSnapshot: Workspace.Solution` setzt. Optionale bestehende
  Parameter wie `UsedDefaultConfig` muessen weiter explizit uebergeben werden koennen; keine neue
  allgemeine TestHelper-Schicht.

### Alle gemeinsamen SymbolGraph-Leser korrigieren —
`src/AiNetLinter.IntegrationTests/Mcp/Tools/GetServerHealthToolTests.cs`,
`GetIndexScopeToolTests.cs` und `SearchPatternToolTests.cs`

- Saemtliche `_fixture.Catalog`-Konstruktionen auf den Fixture-Helper bzw. direkt auf den
  vorhandenen `ReadOnlySolutionSnapshot`-Optionspfad umstellen. Nicht nur die im Review als
  konkrete Dispose-Ausloeser genannten `using`-Stellen aendern: Die entfernte Catalog-Property
  erzwingt fuer alle drei read-only Konsumenten dieselbe Ownership-Grenze.
- Jeden erzeugten Snapshot-Server lokal disposen. Mutierende Tests mit eigener Lease und eigenem
  per `LoadedFixture.LoadCatalogAsync` geladenem Catalog bleiben auf dem besitzenden Catalog-Pfad,
  weil sie Refresh/File-Discovery pruefen und ihr Server den privaten Catalog entsorgen soll.
- Fachliche Tool-Assertions, Kategorien und parallele xUnit-Ausfuehrung unveraendert lassen; keine
  serialisierende Collection einfuehren.

### Ownership-Vertrag ergänzen —
`src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraphCatalogFixtureTests.cs`

- Zwei oder wenige Server aus demselben Fixture-Snapshot erzeugen, einen davon disposen und danach
  mit dem zweiten sowie direkt ueber den Fixture-Snapshot eine echte Roslyn-Leseoperation
  erfolgreich ausfuehren. Ein kleiner `Task.WhenAll`-Abschnitt darf parallele Leser belegen, ohne
  einen Last-/Stressfall zu erzeugen.
- Zusaetzlich Objekt- und Ownership-Grenze pruefen: Jeder Server liefert dieselbe vorbereitete
  `Solution`-Sicht, aber kein Server erhaelt den Fixture-Katalog. Der Test darf nicht per
  Ausfuehrungsreihenfolge davon abhaengen, dass ein anderer Test den Workspace zuvor disposed.
- Kein Produktcode wird fuer diesen Nachweis geaendert. Falls der bestehende
  `ReadOnlySolutionSnapshot`-Pfad wider Erwarten den Vertrag nicht erfuellt, Step blockieren statt
  eine zweite Ownership-Semantik neben `Catalog`/`ReadOnlySolutionSnapshot` zu erfinden.

### Task-Nachweise — `tasks/speedup-tests/tech-debt.md` und Step-Result

- TD-004 darf nur geschlossen bleiben, wenn der statische Kommentarcheck keinen dauerhaften
  Step-/Task-Verweis in `MsBuildFixtureHostTests.cs` findet und dessen enger Plattformtest gruen
  ist.
- TD-009 darf nur geschlossen bleiben, wenn Load-Maximum, Loadpfad-Guard und Snapshot-Ownership-
  Vertrag gruen sind und `SymbolGraphCatalogFixture` keinen `SourceFileCatalog` mehr exponiert.
  Bei fehlendem Nachweis den Eintrag wieder auf offen setzen; nicht allein aufgrund der
  Helper-Existenz geschlossen lassen.
- TD-010 weder textlich noch im Status aendern. Roadmap und Ledger bleiben unveraendert. Das
  `step-result.md` nennt die konkreten API-/Dateiaenderungen und jeden ausgefuehrten Nachweis.

## Tests

- [ ] `dotnet build`
- [ ] Load-/Plattform-/Umgehungspfade:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~LoadedFixtureTests|FullyQualifiedName~MsBuildFixtureHostTests|FullyQualifiedName~MsBuildFixtureHostSharedInstanceTests|FullyQualifiedName~FilterMiniFidelityTests|FullyQualifiedName~ProjectOverrideRealSolutionTests"`
- [ ] Ownership und alle gemeinsamen read-only Konsumenten:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~SymbolGraphCatalogFixtureTests|FullyQualifiedName~GetServerHealthToolTests|FullyQualifiedName~GetIndexScopeToolTests|FullyQualifiedName~SearchPatternToolTests"`
- [ ] Betroffene Migrations-/Kategorieguards:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] Statisch `SourceFileCatalog.LoadAsync(` unter `src/AiNetLinter.IntegrationTests` pruefen:
  einzig erlaubter Aufrufort ist `Platform/LoadedFixture.cs`; `_fixture.Catalog` darf in den drei
  SymbolGraph-Konsumenten nicht mehr vorkommen.
- [ ] TD-004-Kommentarcheck fuer `MsBuildFixtureHostTests.cs`; TD-010-Diff unveraendert.
- [ ] `git --no-pager diff --check`
- [ ] Kein voller Fast-/Integration-/`Category!=Stress`-Lauf; kein Dogfood-, Performance- oder
  Stresslauf.

## Definition of Done

- [ ] Alle realen `SourceFileCatalog.LoadAsync`-Aufrufe in IntegrationTests laufen ueber genau
  einen globalen, exception-/cancellation-sicheren Gate-Kern mit Maximum 2.
- [ ] Ein deterministischer Vertrag belegt Maximum 2 und Permit-Freigabe; ein statischer Guard
  belegt, dass kein heutiger Integrationstest den Pfad umgeht.
- [ ] `SymbolGraphCatalogFixture` ist alleiniger Owner seines Katalogs und exponiert nur eine
  read-only Solution-Sicht; alle drei Fixture-Konsumenten verwenden die bestehende
  `ReadOnlySolutionSnapshot`-Seam.
- [ ] Dispose eines Snapshot-Servers beeintraechtigt weder parallele noch spaetere Leser desselben
  Fixture-Snapshots; mutierende server-eigene Catalog-Pfade behalten Refresh und Ownership.
- [ ] Keine Aenderung an `McpCodeGraphServer`, `McpCodeGraphServerOptions`,
  `SourceFileCatalog.Dispose`, Roadmap oder Migrationsledger; keine test-only Produktverfaelschung
  und keine globale Test-Collection.
- [ ] TD-004 und TD-009 bleiben nur mit dokumentierter gruener Evidenz geschlossen; TD-010 ist
  unveraendert offen.
- [ ] Build, enge Plattform-/Konsumenten-/Guard-Filter und `git --no-pager diff --check` sind gruen;
  kein Voll-/Dogfood-/Performance-/Stresslauf.
- [ ] Ein Code-Commit auf dem aktuellen Branch mit deutschem Conventional-Commit und
  `[speedup-tests]`; `step-022/step-result.md` geschrieben und Planstatus auf
  `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — Nullable, Methoden-/Parametergrenzen und keine
  stillen Ausnahmewege.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — gezielte Semaphore statt breiter
  Collection-Serialisierung, xUnit-Vertraege und keine Ad-hoc-MCP-Skripte.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitaetsdrift-Praevention` — Ursache statt
  Assertion-Abschwaechung und keine Task-IDs in dauerhaftem Code.

## Bekannte Ausnahmen

- TD-010 bleibt als dokumentierter Strangler-Uebergang offen; seine Legacy-Konsumenten sind nicht
  Teil der zwei Step-021-Findings.
- Die ausgeschlossene `SourceFileCatalogRegisterMSBuildTests`-Stressklasse bleibt pending und ist
  weder Implementierungs- noch Gate-Scope dieses Korrektursteps.

## Notes

- Die xUnit-Assembly darf Test-Collections weiterhin parallel ausfuehren. Der Max-2-Nachweis muss
  deshalb gegen den statischen assembly-weiten Gatezustand robust sein und darf keine globalen
  Counter zuruecksetzen; kontrollierte Delegates messen nur ihren eigenen Eintritt.
- Der statische Callsite-Guard und der dynamische Gate-Vertrag sind gemeinsam erforderlich: Der
  eine beweist Pfadvollstaendigkeit, der andere die Laufzeiteigenschaft. Einer allein reicht fuer
  das Review-Finding nicht.
- Server auf privaten, mutierbaren Catalogs duerfen weiterhin Catalog-Ownership uebernehmen.
  Ausschliesslich der assembly-weit geteilte read-only Fixture-Katalog darf nie ueber den normalen
  `Catalog`-Optionspfad an einen Server gelangen.
