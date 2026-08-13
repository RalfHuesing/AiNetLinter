---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 024
corrects: step-023
title: "Korrektur: deterministische EPIC-5-Grenzprofile"
epic: EPIC-5
estimated_risk: high
step_type: batch
items:
  - id: item-01
    title: "Fast-Runtime-Guard und MSBuild-Typgrenze isolieren"
    source: "step-023/step-review.md#Findings-1"
  - id: item-02
    title: "Integration-Loadbudget und Prozesslebensdauer deterministisch machen"
    source: "step-023/step-review.md#Findings-2"
  - id: item-03
    title: "LoadedFixture-Rootsuche konsolidieren"
    source: "tech-debt.md#TD-011"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-023/step-review.md
---

# Step 024: Korrektur: deterministische EPIC-5-Grenzprofile

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-5` aus `roadmap.md` — die Migration aus step-023 ist fachlich vorhanden, aber
  beide dort verbindlich zugesagten Profilgrenzen muessen als vollstaendig beendete gruene Laeufe
  nachgewiesen werden.
- **Korrigierter Step:** `step-023`; dies ist die erste Korrektur dieser Kette bei Fixbudget 6.
- **Review-Quelle:** `step-023/step-review.md`, Findings 1 und 2. Beide MAJORs werden in diesem
  einen Korrekturstep geschlossen; bereits akzeptierte Migrationsitems aus step-023 bleiben
  unangetastet.

## Aktueller Projektzustand (JIT-Kontext)

Der statische `FastTestsDependencyGuardTests` liest die Metadaten von
`AiNetLinter.FastTests.dll` und `AiNetLinter.TestKit.dll` und ist einzeln gruen. Sein dynamisches
Gegenstueck ist dagegen eine `ICollectionFixture`, die nur von der Zweitest-Collection
`FastTestsRuntimeDependencyGuard` besessen wird, beim Dispose aber den globalen
`AppDomain.CurrentDomain` scannt. Die uebrigen Unit-/Component-Collections laufen parallel und
liegen ausserhalb dieses Fixture-Lebenszyklus. Der volle Profilversuch meldete deshalb erst nach
777 fachlich gruennen Faellen `Microsoft.CodeAnalysis.Workspaces.MSBuild`; der Guard kann in der
heutigen Form weder den kompletten Assembly-Lauf sauber umschliessen noch einen parallelen
Ausloeser zuordnen.

Der konkrete Fast-Kandidat ist `SourceFileCatalogPolicyTests`, hinzugekommen in step-021: Die
Klasse ruft zwar nur `ShouldIncludeProject` und `IsGeneratedPath` auf, beide Methoden liegen aber
auf `SourceFileCatalog`. Dieser Typ besitzt unmittelbar ein Feld und einen Konstruktorparameter
vom Typ `MSBuildWorkspace` und enthaelt Registrierung plus echten Load. Dadurch ist die schnelle
Policy fachlich rein, ihre Typgrenze jedoch nicht runtime-rein. Ein Allowlisting der dynamisch
geladenen Assembly oder das Entfernen des Runtime-Guards wuerde eine echte verbotene
Fast-Abhaengigkeit maskieren und ist ausgeschlossen.

In IntegrationTests ist `LoadedFixture` der statisch abgesicherte einzige direkte
`SourceFileCatalog.LoadAsync`-Callsite und begrenzt reale Loads mit einem statischen
`SemaphoreSlim` auf zwei. `LoadedFixtureTests` prueft aber genau diesen produktionsweit geteilten
Semaphor mit kontrollierten Dummy-Delegates, waehrend Assembly-Fixtures und parallele
Integrationklassen denselben Semaphor fuer reale Loads verwenden. Wenn deshalb nicht beide
Dummy-Delegates innerhalb von fuenf Sekunden eintreten, wirft `WaitAsync(TimeSpan)` vor
`release.TrySetResult()` und vor `Task.WhenAll`; bereits eingetretene Delegates bleiben wartend,
halten Permits und blockieren nachfolgende reale Loads. Das erklaert sowohl isoliert gruene
Loadbudget-Tests als auch den Profilhaenger und die nach Abbruch verbleibenden
`testhost`-/`AiNetLinter --mcp-server`-/MSBuild-BuildHost-Ketten.

`xunit.runner.json` laesst Collections bewusst parallel laufen. Die zwei assembly-weiten
`MsBuildFixtureHost`-/`SymbolGraphCatalogFixture`-Initialisierungen und alle heutigen echten Loads
laufen bereits durch das Max-2-Gate. Eine globale Collection, `DisableParallelization`, das
Verschieben fachlicher Integrationvertraege nach Dogfood/Performance/Stress oder ein pauschales
Absenken von `maxParallelThreads` waere daher Symptomkosmetik und ist nicht geplant.

TD-011 trifft exakt denselben Fixture-Infrastruktur-Schnitt: `LoadedFixture` und
`LoadedFixtureTests` duplizieren ihre Rootsuche. TD-008 trifft ihn nicht: Die Compile-Error-
Assertion liegt in FastTests, IntegrationTests und noch im Legacy-Strangler; eine gemeinsame
xUnit-Assertion in `TestKit` wuerde dessen heutige testframework-freie Grenze fuer nur diesen
Profilfix aufweichen.

## Intention

Der Fast-Guard soll den gesamten Unit-/Component-Testhost besitzen und weiterhin jeden echten
MSBuild-Runtime-Load melden. Die schnelle Catalog-Policy bleibt auf der Unit-/Component-Seite,
aber ihre produktive Typgrenze darf beim Aufruf keine MSBuild-Assembly mehr aufloesen.

Der Integration-Loadbudget-Vertrag soll dieselbe Gate-Implementierung wie die realen Loads
pruefen, jedoch mit einer privaten Gate-Instanz und garantiertem Cleanup. Danach muessen die
realen MSBuild- und MCP-Vertraege parallel weiterlaufen, das Profil vollstaendig enden und keine
von diesem Lauf gestartete Prozesskette zuruecklassen.

## Konkrete Änderungen

### item-01: Fast-Runtime-Guard und MSBuild-Typgrenze isolieren

#### Guard-Lebenszyklus — `src/AiNetLinter.FastTests/Architecture/FastTestsRuntimeDependencyGuardFixture.cs` und `src/AiNetLinter.FastTests/Platform/PreparedSolutionAssemblyFixture.cs`

- `FastTestsRuntimeDependencyGuardFixture` als echte xUnit-v3-Assembly-Fixture registrieren, so
  dass ihr Startcheck vor den Tests und ihr Dispose-Check nach allen Unit-/Component-Collections
  laufen. Die vorhandene `PreparedSolutionFixture` bleibt parallel als zweite Assembly-Fixture
  registriert.
- Die `FastTestsRuntimeDependencyGuardCollection` und `[Collection("FastTestsRuntimeDependencyGuard")]`
  aus `FastTestsDependencyGuardTests` entfernen. Der Runtime-Nachweis darf nicht mehr von der
  zufaelligen Laufreihenfolge einer einzelnen Collection abhaengen.
- Sowohl beim Initialisieren als auch beim Abschluss die unveraenderte Deny-Liste
  (`Microsoft.Build*`, `Microsoft.CodeAnalysis.Workspaces.MSBuild*`) pruefen. Ein bereits vor dem
  Profil geladener verbotener Kandidat ist ebenfalls ein Fehler, keine Baseline-Ausnahme. Fuer
  einen Abschlussfehler mindestens Assemblyname und Phase ausgeben; keine Allowlist fuer
  `SourceFileCatalogPolicyTests`, Testhost oder Produkttransitivitaet einbauen.
- `FastTestsDependencyGuardTests` behaelt die statische AssemblyRef-/TypeRef-/MemberRef-Pruefung
  fuer FastTests und TestKit unveraendert als eigenstaendigen Unit-Vertrag. Der volle Profilexit
  braucht beide Ebenen: statische Referenzreinheit und keine tatsaechliche Runtime-Ladung.

#### MSBuild-freie Catalog-Typgrenze — `src/AiNetLinter/Baseline/SourceFileCatalog.cs` und schmale neue Loader-Datei im selben Namespace

- Den echten MSBuild-Adapter (`MSBuildLocator`, `MSBuildWorkspace.Create`, Registrierung,
  BuildHost-Patching und Solution-Dateiaufloesung) in eine schmale interne Loader-Klasse im
  `Baseline`-Bereich verschieben. `SourceFileCatalog.LoadAsync` bleibt als kompatibler oeffentlicher
  Adapter erhalten und delegiert nur dorthin; alle bestehenden Integration-Callsites bleiben
  gueltig.
- `SourceFileCatalog` darf in Feldern und Konstruktoren nur die MSBuild-freie Roslyn-Basis
  `Workspace` besitzen, nicht `MSBuildWorkspace`. Der vom Loader erzeugte Workspace bleibt damit
  weiterhin eindeutig im Catalog-Besitz und wird unveraendert ueber `Dispose()` geschlossen.
- Policy, Snapshot-Konstruktor, `WithUpdatedSolution`, Checksums und Dokumentenumeration bleiben
  semantisch unveraendert. Es entsteht keine zweite Policy-Implementierung und kein test-only
  Schalter. Ziel ist ausschliesslich, dass die Benutzung des Catalog-Typs bzw. seiner reinen
  Methoden die MSBuild-Assembly nicht mehr aufloest.
- Einen engen Fast-Vertrag neben `SourceFileCatalogPolicyTests` bzw. dem Runtime-Guard ergaenzen,
  der nach Ausfuehrung der Policy-Aufrufe belegt, dass keine denied Assembly geladen wurde. Er ist
  Zusatzdiagnose; das verbindliche Profilgate bleibt der assembly-weite Dispose-Check.

### item-02: Integration-Loadbudget und Prozesslebensdauer deterministisch machen

#### Instanzbasierter Gate-Kern — `src/AiNetLinter.IntegrationTests/Platform/LoadedFixture.cs`

- Das `SemaphoreSlim`-Protokoll in einen schmalen internen instanzbasierten Gate-Typ mit
  `ExecuteAsync<T>(Func<CancellationToken, Task<T>>, CancellationToken)` kapseln. Eine einzige
  statische Gate-Instanz mit Kapazitaet `LoadedFixture.MaxConcurrentLoads == 2` bleibt der
  produktive Pfad fuer `LoadedFixture.LoadCatalogAsync`; der bestehende statische Callsite-Guard
  muss weiter genau `Platform/LoadedFixture.cs` als einzigen direkten
  `SourceFileCatalog.LoadAsync(`-Ort sehen.
- Der Gate-Typ besitzt genau einen `WaitAsync`/`try-finally`/`Release`-Pfad. Keine Reset-API,
  globale Counter oder test-only Austauschbarkeit der produktiven statischen Instanz einfuehren.

#### Konkurrenzfeste Loadbudget-Vertraege — `src/AiNetLinter.IntegrationTests/Platform/LoadedFixtureTests.cs`

- Die Max-2-, Exception- und Cancellation-Vertraege gegen jeweils eine frische Gate-Instanz
  ausfuehren. Damit pruefen sie exakt denselben Gate-Code, konkurrieren aber nicht mit echten
  Assembly-Fixture-/Testloads und manipulieren keinen globalen Profilzustand.
- Jeden gestarteten Delegate in `try/finally` freigeben bzw. abbrechen und anschliessend vollstaendig
  awaiten. Auch wenn eine Deadlock-Sicherung oder Assertion fehlschlaegt, darf kein Task weiterlaufen
  oder ein Permit halten. Timeouts bleiben reine Fehlersicherung, nie Synchronisationsannahme.
- Den statischen Callsite-Guard unveraendert fachlich erhalten. Ein zusaetzlicher Vertrag darf
  pruefen, dass `LoadedFixture` die eine statische Gate-Instanz mit Kapazitaet zwei benutzt; er darf
  nicht die Instanz zur Laufzeit ersetzen.

#### Reale Solution nur einmal pro Vertrag laden — `src/AiNetLinter.IntegrationTests/Configuration/ProjectOverrideRealSolutionTests.cs`

- Die drei Theory-Faelle in einen Faktvertrag mit einer Tabellen-/Schleifenassertion konsolidieren,
  sodass die echte `AiNetLinter.slnx` pro Testlauf einmal statt dreimal geladen wird. Alle drei
  Zielprojekte (`FastTests`, `IntegrationTests`, `TestKit`), Override-Werte und
  `TestProjectDetector`-Assertions bleiben erhalten.
- Kategorie `Integration` bleibt fachlich korrekt: Der Vertrag ist die zugesagte reale
  MSBuild-Fidelity zwischen den drei im Build vorhandenen Zielprojekten und der Konfiguration,
  kein Live-Repository-Dogfood-Toolvertrag. Ihn aus dem EPIC-5-Grenzprofil zu entfernen waere
  unzulaessig.

#### MCP-/Prozessbesitz verifizieren — `src/AiNetLinter.IntegrationTests/Mcp/McpHandshakeToolRegistrationTests.cs` und Gate-Diagnose

- Den bestehenden 30-Sekunden-Cancellation- und `await using`-Besitz des `McpClient` beibehalten
  und den Test zunaechst isoliert sowie nach den realen MSBuild-Filtern pruefen. Die
  `StdioClientTransport`-Abstraktion nicht vorsorglich durch einen zweiten MCP-Client kopieren.
- Vor jedem Diagnose-/Gesamtgate eine schreibgeschuetzte PID-/ParentPID-/Commandline-Baseline fuer
  `dotnet test`, `testhost`, `AiNetLinter.exe --mcp-server` und MSBuild-BuildHosts erfassen. Nach
  einem Lauf duerfen nur Prozesse beendet werden, deren PID neu ist und deren Parentkette zum
  gestarteten `dotnet test` gehoert; keine pauschalen Prozessnamen-Kills.
- Falls der isolierte Handshake trotz repariertem Loadgate nach Cancellation einen eigenen
  Kindprozess behaelt, im Test einen explizit besitzenden schmalen Prozess-Transport mit
  `try/finally`, bounded graceful shutdown und anschliessendem `Kill(entireProcessTree: true)`
  einfuehren. Diese Eskalation ist nur durch den isolierten PID-Nachweis gerechtfertigt; ein nach
  gewaltsamem Abbruch des Eltern-Testhosts verbliebener Prozess allein belegt keinen
  Transportdefekt.
- Fuer Baseline-/Diagnoselaeufe `--blame-hang --blame-hang-timeout` und eigene TRX-Dateinamen
  verwenden, damit ein erneuter Fehler beendet, zugeordnet und nicht durch `latest.trx`
  ueberschrieben wird. Die finalen Profilgates muessen ohne manuellen Prozessabbruch normal enden.

#### Runner-/Kategoriegrenze — `src/AiNetLinter.IntegrationTests/xunit.runner.json` und Kategorieguards

- `parallelizeTestCollections: true`, die beiden Assembly-Fixtures und das Max-2-Loadbudget
  beibehalten. `maxParallelThreads` nur dann gezielt auf einen endlichen CPU-Wert kalibrieren, wenn
  der reparierte Reihenfolgefilter weiterhin einen reproduzierbaren Runner-Saettigungsbefund mit
  TRX/Long-Running-Evidenz liefert; nicht als primaeren Fix und nicht auf 1 setzen.
- Keine `CollectionDefinition`, kein `DisableParallelization` und keine Kategorieaenderung fuer
  `LoadedFixtureTests`, reale MSBuild-Adapter oder MCP-Handshake. Der
  `TestCategoryProfileGuardTests` muss unveraendert gruen bleiben und belegen, dass keine Klasse
  dem Profil entzogen wurde.

### item-03: LoadedFixture-Rootsuche konsolidieren — `src/AiNetLinter.IntegrationTests/Platform/SolutionRootLocator.cs`, `LoadedFixture.cs` und `LoadedFixtureTests.cs`

- TD-011 im ohnehin beruehrten Fixture-Infrastruktur-Scope schliessen: eine interne statische
  `SolutionRootLocator.Find()`-API in `Platform/SolutionRootLocator.cs` anlegen und sowohl
  `LoadedFixture.CreateAsync` als auch den Callsite-Guard darauf umstellen. Fehlertext und
  Aufwaertssuche nach `AiNetLinter.slnx` bleiben unveraendert.
- Nicht zugleich alle weiteren lokalen `FindSolutionRoot`-Varianten im Projekt migrieren; deren
  Fixture-/MCP-/Migration-Scope ist nicht Gegenstand der beiden MAJORs. `tech-debt.md` wird vom
  Kritiker erst nach gruenem Nachweis als geschlossen fortgeschrieben.
- TD-008 bleibt offen. Der lokale Integration-CompileError-Helper ist nicht Ursache der
  Profilhaenger; eine gemeinsame Assertion wuerde xUnit-Abhaengigkeit oder neue
  Assembly-Helferarchitektur erfordern und ist fuer diesen Korrekturstep nicht risikoarm.

## Tests

- [ ] **Prozess-/TRX-Baseline vor Aenderungen:** PID-/ParentPID-/Commandline-Snapshot erstellen;
  vorhandene fremde Prozesse nicht beenden. Eigene Lognamen pro Lauf verwenden, weil
  `.runsettings` sonst `TestResults/latest.trx` ueberschreibt.
- [ ] **Fast-Baseline und Ausloeser, jeweils eigener Testhost:**
  `FastTestsDependencyGuardTests` allein; Guard plus `SourceFileCatalogPolicyTests`; danach das
  EPIC-Profil `dotnet test src/AiNetLinter.FastTests --no-build --filter
  "Category=Unit|Category=Component" --blame-hang --blame-hang-timeout 2m --logger
  "trx;LogFileName=step024-fast-baseline.trx"`. Erwartung vor Fix: statischer Guard gruen,
  Policy-Kombination bzw. Gesamtprofil liefert die Runtime-Signatur; exakte Reihenfolge und
  Assemblyname im Result festhalten.
- [ ] `dotnet build` nach den Aenderungen.
- [ ] **Fast-Einzelgates nach Fix:** statischer Dependencyguard; Runtime-Guard plus
  `SourceFileCatalogPolicyTests`; die bestehenden Catalog-Integrationadapter separat. Keine
  denied Assembly darf im Fast-Testhost geladen sein.
- [ ] **Fast-Gesamtgate:** `dotnet test src/AiNetLinter.FastTests --no-build --filter
  "Category=Unit|Category=Component" --logger
  "trx;LogFileName=step024-fast-epic5.trx"` muss vollstaendig normal enden, 0 Fehler melden und
  sowohl statischen als auch assembly-weiten Runtime-Guard enthalten.
- [ ] **Integration-Baseline einzeln:** `LoadedFixtureTests`, danach
  `MsBuildFixtureHostTests|MsBuildFixtureHostSharedInstanceTests|SymbolGraphCatalogFixtureTests`,
  danach `ProjectOverrideRealSolutionTests`, danach `McpHandshakeToolRegistrationTests`; jeder
  Filter in eigenem Testhost mit eigenem TRX und bounded Hangdiagnose.
- [ ] **Integration-Reihenfolgegate in einem Testhost:** Loadbudget-Vertraege + beide
  Assembly-Fixture-Vertraege + `ProjectOverrideRealSolutionTests` + die real-loadenden MCP-
  Klassen + `McpHandshakeToolRegistrationTests`. Mindestens die zuvor fehlschlagende Reihenfolge
  `LoadedFixtureTests` unter parallelen echten Loads abdecken; keine Testreihenfolge im Code
  erzwingen. Der Lauf muss enden und der Nachher-PID-Snapshot darf keine neue zugehoerige
  Prozesskette zeigen.
- [ ] **Kategorie-/Callsiteguards:**
  `TestCategoryProfileGuardTests`, `TestMigrationLedgerConsistencyTests` und
  `LegacyProjectBuildGateTests`; statisch weiterhin genau ein direkter
  `SourceFileCatalog.LoadAsync(`-Callsite in `Platform/LoadedFixture.cs`.
- [ ] **Integration-Gesamtgate:** `dotnet test src/AiNetLinter.IntegrationTests --no-build
  --filter "Category=Integration" --logger
  "trx;LogFileName=step024-integration-epic5.trx"` muss ohne manuellen Kill vollstaendig enden
  und gruen sein. Kein Dogfood-, Performance-, Stress-, Legacy-, Solution- oder
  `Category!=Stress`-Vollprofil.
- [ ] Nach jedem fehlgeschlagenen/abgebrochenen Diagnoseversuch nur die anhand des
  Vorher-Snapshots und der Parentkette eindeutig diesem Lauf gehoerenden Prozesse bereinigen und
  PIDs/Commandlines im Result dokumentieren; vor dem naechsten Lauf leeren Ausgangszustand
  bestaetigen.
- [ ] `git --no-pager diff --check`.

## Definition of Done

- [ ] Der Fast-Runtime-Guard lebt assembly-weit, nicht collection-lokal, und meldet weder beim
  Start noch nach dem vollstaendigen Unit-/Component-Profil eine denied Assembly.
- [ ] `SourceFileCatalog` kann fuer Policy-/Snapshot-Vertraege verwendet werden, ohne
  `Microsoft.CodeAnalysis.Workspaces.MSBuild` zu laden; echter MSBuild-Load und Ownership bleiben
  hinter dem kompatiblen `LoadAsync`-Adapter erhalten.
- [ ] Der statische FastTests-/TestKit-Dependencyguard bleibt unveraendert scharf und gruen; keine
  Allowlist oder Assertion-Abschwaechung maskiert verbotene Referenzen.
- [ ] Loadbudget-Tests verwenden eine private Gate-Instanz, geben bei Erfolg, Fehler, Timeout und
  Cancellation alle Delegates/Permits frei und koennen parallel zu realen Loads nicht mehr den
  globalen Gatezustand vergiften.
- [ ] Die reale Zielprojekt-Fidelity laedt die Solution nur einmal und prueft weiterhin alle drei
  Zielprojekte unter `Category=Integration`; kein Test wurde versteckt oder fachlich
  umkategorisiert.
- [ ] Der MCP-Handshake endet isoliert und im Reihenfolge-/Gesamtprofil innerhalb seines Budgets;
  der normale Gesamtgate-Exit hinterlaesst keine dem Lauf gehoerenden Testhost-, MCP- oder
  BuildHost-Prozesse.
- [ ] TD-011 ist im LoadedFixture-Infrastruktur-Scope konsolidiert. TD-008 bleibt mit der
  dokumentierten Assembly-/xUnit-Begruendung offen.
- [ ] Build, beide exakt definierten EPIC-5-Gesamtprofile, Kategorie-/Ledgerguards und
  `git diff --check` sind gruen. Keine Dogfood-/Performance-/Stress-/Legacy-/Solution-Volltests.
- [ ] Ein kohärenter deutscher Conventional-Commit mit `[speedup-tests]`; kein Amend/Rebase/Push.
  `step-024/step-result.md` geschrieben, Planstatus `done (pending audit)`.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Projekt-Overrides` — Nullable,
  Methoden-/Parametergrenzen und Testprojekt-Overrides.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` — Windows-Prozesse,
  getrennte TRX-Diagnose und projektbezogene Testbefehle.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — gezielte Semaphore-/Fixture-
  Isolation statt globaler Collection-Serialisierung; reale MCP-Nachweise in C#.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — Ursache statt
  Symptomfix, keine abgeschwaechten Assertions oder dauerhaften Task-ID-Kommentare.

## Bekannte Ausnahmen

- TD-008 bleibt offen: Eine gemeinsame Compile-Error-Assertion ueber FastTests,
  IntegrationTests und Legacy ist kein risikoarmer Bestandteil dieser Profilkorrektur.
- `SourceFileCatalogRegisterMSBuildTests` bleibt pending in EPIC-6 und wird nicht in das
  EPIC-5-Integrationprofil gezogen; sein paralleler Registrierungs-Lastvertrag ist Stress.
- Ein expliziter MCP-Prozesstransport wird nur bei isoliert reproduziertem Ownership-Leak gebaut.
  Der heutige Parent-Abbruch nach vergiftetem Loadbudget ist keine ausreichende Evidenz dafuer.

## Notes

- Der Produkt-Split ist eine Ladegrenzen-Korrektur, keine neue Plugin-/Reflection-Architektur:
  `SourceFileCatalog.LoadAsync` bleibt API und delegiert direkt an einen statischen Loader.
- Eine assembly-weite Runtime-Fixture ist absichtlich strenger als die heutige Collection-
  Fixture: Sie beseitigt den Race im Messzeitraum, nicht die Deny-Regel.
- Das Integration-Gate darf weiterhin parallel sein. Determinismus entsteht durch getrennten
  Gatezustand im Vertrag und vollstaendiges Task-Cleanup, nicht durch Ausfuehrungsreihenfolge.
