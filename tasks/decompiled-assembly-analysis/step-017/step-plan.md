---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 017
corrects: step-016
title: "Cancellation-Cleanup beobachten und Reparse-Test privilegienbewusst ausführen"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T02:22:46+02:00
related_to:
  - step-016/step-review.md
  - step-016/step-plan.md
  - step-016/step-result.md
  - follow-up-strategy.md
  - Konzept.md
  - ../../../Docs/integration.md
---

# Step 017: Cancellation-Cleanup beobachten und Reparse-Test privilegienbewusst ausführen

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und
  Fehlersemantik.
- **Korrektur von:** Step 016, Review-Commit `3be96cf1`; der Step bleibt
  wegen des nicht privilegierten Windows-Hosts und des verworfenen
  Cancellation-Cleanup-Status blockiert.
- **Konzept-Referenz:** `Konzept.md`, Phase 4 sowie „Fehler-, Sicherheits-
  und Vertrauensvertrag“ und „Teststrategie“.

## Split-Gate und Capability-Entscheidung

Dies bleibt ein zusammenhängender Korrektur-Step innerhalb derselben
Akquisitions-/Besitzgrenze. Die beiden Findings liegen am selben
`AcquireReservedCheckoutAsync`-Abbruchpfad und an dessen direkter
Windows-Regression; eine weitere Aufteilung würde entweder die
Beobachtbarkeit ohne Regression oder die ehrliche Testausführung ohne
Vertragskorrektur liefern.

- **Eng gekoppelte Verträge:** genau zwei:
  1. Cancellation-/Cleanup-Beobachtbarkeit am bestehenden Acquirer;
  2. echte Reparse-Testausführung mit expliziter Capability-Gate-
     Entscheidung.
- **Unmittelbar betroffene Schichten:** zwei:
  1. bestehender Produktionspfad mit der vorhandenen Serilog-
     Beobachtbarkeit;
  2. FastTests-Testharness, Regression und Ausführungsdokumentation.
- **Akzeptanzkriterien:** acht.
- **`read_first`:** zwölf Dateien.
- **Risikoeinstufung:** `high`, weil die Änderung einen Cancellation-
  Vertrag und den Nachweis gegen fremde Reparse-Ziele berührt.

**Capability-Gate-Entscheidung:** Ja, ein test-only Gate ist zulässig.
Das Projekt verwendet xUnit v3, die vorhandene
`DaemonEndpointJanitor`-Infrastruktur nutzt bereits `Assert.Skip(...)`,
und `Docs/integration.md` beschreibt Skips bei fehlender lokaler
Testvoraussetzung als zulässige Konvention. Das Gate darf ausschließlich
  die tatsächliche Symlink-Fähigkeit vorab prüfen und ausschließlich
  `ERROR_PRIVILEGE_NOT_HELD` (Win32 `1314`, auch aus dem .NET-HResult-
  Low-Word zu erkennen) als fehlende Symlink-Berechtigung überspringen.
  Eine pauschale `UnauthorizedAccessException`-Behandlung oder ein anderer
  Fehlercode ist kein zulässiger Skip-Grund. Ein Skip ist kein ausgeführter
  Sicherheitsnachweis.

Der aktuelle Host ist nach den read-only-Prüfungen nicht berechtigt:
`SeCreateSymbolicLinkPrivilege` ist nicht als `Enabled` sichtbar und es
gibt keinen auslesbaren Developer-Mode-Nachweis. Ein privilegierter Lauf
ist daher ohne Nutzer-/Umgebungsänderung nicht erreichbar. Diese Umgebung
bleibt harte Out-of-scope-Infrastruktur: Ein unprivilegierter Lauf darf
mit genau einem begründeten Skip technisch grün enden, aber der Step darf
dadurch nicht als vollständiger Reparse-Nachweis genehmigt werden.

## Scope

### In Scope

- Im bestehenden `ExternalSourceRepositoryAcquirer` den Cleanup-Rückgabewert
  im `OperationCanceledException`-Pfad auswerten und bei `false` über den
  vorhandenen Serilog-Weg als stabilen
  `RepositoryCleanupFailed`-Code beobachtbar machen.
- Die Cancellation unverändert weiterreichen: ursprüngliche
  `OperationCanceledException`, ursprünglicher CancellationToken und kein
  nachträgliches Provider-Failure-Result.
- Einen optionalen internen Logger-Seam für den Acquirer nur soweit
  ergänzen, dass die Beobachtung ohne globale Logger-Manipulation
  deterministisch testbar ist; kein DI-Container und kein neuer externer
  Provider-Vertrag.
- Eine direkte Regression für fehlgeschlagenes Cleanup während Cancellation
  ergänzen: sichtbarer stabiler Log-Eintrag, unveränderter CancellationToken
  und kein Löschen des nicht mehr eigenen Checkouts.
- Einen test-only Symlink-Capability-Preflight mit `TestTempDirectory`
  vor der bestehenden echten Reparse-Regression ergänzen. Der Preflight
  muss einen echten Directory-Symlink anlegen und wieder entfernen; er darf
  ausschließlich `ERROR_PRIVILEGE_NOT_HELD` (`1314`) als fehlende
  Symlink-Berechtigung überspringen.
- Den bestehenden Reparse-Testkörper einschließlich
  `Directory.CreateSymbolicLink`, produktiver Reparse-Prüfung, externem
  Sentinel und Assertions unverändert ausführbar lassen.
- Die Ausführungsdokumentation im Testharness klar trennen: Skip auf
  unberechtigtem Host bedeutet „Capability nicht nachgewiesen“; ein
  berechtigter Lauf muss denselben Test ohne Skip ausführen.

### Out of Scope

- Keine Änderung an Reparse-/Ownership-/Reservation-Produktionslogik
  außerhalb des Cancellation-Beobachtungspfads und keine Abschwächung der
  echten Symlink-/Sentinel-Assertion.
- Keine Attributsimulation, kein Fake-Reparse-Objekt, keine alternative
  Assertion, kein Ersatz durch Junction-/Dateiattrappen und keine
  Privilegien-, Developer-Mode-, Registry- oder sonstige Systemänderung.
- Kein Provider-/Host-/MCP-Wiring, kein produktiver HTTP-/Gitea-/Git-
  Transport, keine Credentials, kein Fetch, Refresh, Cache, Snapshot,
  Workspace oder Source-of-Truth-Vertrag.
- Keine Änderungen an `task-state.md`, `codemap.md`, `tech-debt.md` oder
  `roadmap.md`; im Fix-Modus bleibt die Roadmap unverändert.
- Keine unabhängigen DRY-, MagicValues- oder DeadCode-Sweeps. `TD-001`
  bis `TD-003` bleiben unberührt; solche Befunde werden nur berücksichtigt,
  wenn sie unmittelbar durch dieses Korrekturpaket entstehen.
- Kein `Assembly.Load`, keine `AssemblyLoadContext`- oder
  Reflection-Ausführung, keine externen Tests und keine `Stress`-Tests.

## Aktueller Projektzustand (JIT-Kontext)

- `ExternalSourceRepositoryAcquirer.AcquireReservedCheckoutAsync` liegt
  in `ExternalSourceRepositoryAcquirer.cs:62-95`. Der Catch fängt
  `OperationCanceledException`, ruft `ownership.TryCleanup()` auf und
  verwirft dessen Bool-Ergebnis anschließend mit `throw;`.
- `ExternalSourceCheckoutOwnership.TryCleanup` delegiert an
  `ExternalSourceRepositoryPathGuard.TryDeleteOwnedCheckout`; ein
  fehlender Ownership-Marker macht Cleanup sicher ablehnend. Das Handle
  kann seinen `CleanupState` nur für einen bereits zurückgegebenen Handle
  setzen; im Cancellation-Pfad existiert kein Handle für den Aufrufer.
- Die Anwendung verwendet Serilog bereits als Prozess-Logging. Der
  Acquirer hat aktuell keinen Logger-Seam; die einzige produktive
  Instanziierung bleibt in der bestehenden Acquirer-Komposition, während
  die direkte Testklasse viele Zweiparameter-Konstruktionen enthält. Ein
  optionaler interner Logger hält diese Aufrufer kompatibel und vermeidet
  globale Testzustände.
- `ExternalSourceRepositoryAcquirerTests` ist ein Component-Test mit
  `TestTempDirectory`/`IsolatedFixtureLease`; der echte Symlink wird im
  Testkörper aktuell direkt erzeugt. Die separate Attributprüfung ist nur
  eine ergänzende Unit-Assertion und kein Ersatz für den echten Ausbruch.
- Die Testprojekte referenzieren xUnit v3. Ein dynamischer Capability-Skip
  ist im Repository bereits über `Assert.Skip(...)` etabliert; eine
  zwangsserialisierte Test-Collection oder ein global ersetzter Logger wäre
  wegen der bestehenden Parallelitätsregeln nicht angemessen.
- Der aktuelle Host zeigt kein aktiviertes
  `SeCreateSymbolicLinkPrivilege` und keinen auslesbaren Developer-Mode-
  Wert. Der Fehler in Step 016 ist daher als fehlende Testfähigkeit, nicht
  als Anlass für eine Produktions- oder Assertion-Abschwächung, zu führen.

## Intention

Der Cancellation-Pfad soll echte Cancellation behalten und gleichzeitig
einen fehlgeschlagenen Cleanup-Versuch über die bestehende
Systembeobachtbarkeit sichtbar machen. Die echte Reparse-Regression soll
auf berechtigten Windows-Hosts unverändert laufen; auf dem aktuellen Host
wird nur die fehlende Capability explizit als Skip dokumentiert, ohne den
fehlenden Sicherheitsnachweis umzudeuten.

## Konkrete Änderungen

### Schicht 1: Cancellation-/Cleanup-Beobachtbarkeit

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` (Konstruktor und `AcquireReservedCheckoutAsync`, aktuell etwa Zeile 18 sowie 62-95)

- **Was:** Den bestehenden Acquirer um einen optionalen internen
  `Serilog.ILogger`-Seam mit Default auf den vorhandenen Serilog-Logger
  ergänzen. Der Produktionsaufrufer bleibt ohne zusätzlichen Parameter
  gültig; der Test kann einen instanzlokalen Sink verwenden.
- **Was:** Im Cancellation-Catch das Ergebnis von
  `ownership.TryCleanup()` speichern. Bei `false` genau einen stabilen
  Warning-Eintrag mit dem vorhandenen Code
  `ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed`
  schreiben; Checkout-Pfad, Ownership-Token, URL, Exception-Text und
  Exception-Objekt dürfen nicht in diesen Eintrag gelangen.
- **Was:** Danach weiterhin `throw;` ausführen. Der Logger darf den
  Cancellation-Vertrag weder in ein Acquisition-Failure-Result umwandeln
  noch den ursprünglichen CancellationToken ersetzen.
- **Warum:** Bei Cancellation kann kein `ExternalSourceRepositoryAcquisitionResult`
  zurückgegeben werden. Der bestehende Systemlog ist deshalb die
  beobachtbare, stable-code-basierte Fehlerfläche, während die
  Ownership-Grenze unverändert sicher ablehnt.

### Schicht 2: Testharness und ehrliche Reparse-Ausführung

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCancellationTests.cs` (neu, test-only)

- **Was:** Eine fokussierte Component-Regression mit lokalem
  `IGiteaRepositoryTransport`-Double und instanzlokalem Serilog-Sink
  ergänzen. Das Double entfernt den Ownership-Marker, setzt den echten
  CancellationToken und wirft `OperationCanceledException`.
- **Was:** Den unveränderten CancellationToken, die
  `OperationCanceledException`, den sichtbaren
  `RepositoryCleanupFailed`-Logcode und den unangetasteten nicht mehr
  belegbaren Checkout prüfen. Kein globaler Logger, keine neue Collection
  und kein OS-Temp-Pfad.
- **Warum:** Der bisherige Cancellation-Test belegt nur erfolgreiches
  Cleanup; das Review-Finding verlangt den expliziten Fehlpfad.

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs` (neu, test-only)

- **Was:** Einen kleinen, XML-dokumentierten Supporttyp für den
  instanzlokalen Log-Sink und einen
  `WindowsReparseCapabilityGate.Require()`-Preflight bereitstellen.
- **Was:** Der Preflight verwendet `TestTempDirectory`, erzeugt einen
  echten temporären Directory-Symlink, prüft dessen tatsächliche
  Reparse-Eigenschaft und entfernt ihn wieder. Nur ein explizit erkannter
  fehlender Windows-Rechtecode darf `Assert.Skip` mit einer Begründung
  auslösen; alle anderen Fehler bleiben Testfehler.
- **Was:** Die Dokumentation des Supporttyps muss ausdrücklich festhalten,
  dass der Skip keine Reparse-Sicherheitsaussage liefert und der gleiche
  Test unter einer berechtigten Umgebung ohne Skip erneut laufen muss.
- **Warum:** Das Gate verhindert einen irreführenden roten Hostfehler,
  bewahrt aber die echte Assertion und macht Capability, Skip-Grund und
  fehlenden Nachweis sichtbar.

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs` (bestehende echte Reparse-Regression, aktuell etwa Zeile 234-260)

- **Was:** Ausschließlich am Anfang des bestehenden Tests
  `AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
  `WindowsReparseCapabilityGate.Require()` aufrufen.
- **Was bleibt unverändert:** Der Testkörper nach dem Gate erzeugt weiterhin
  mit `Directory.CreateSymbolicLink` den echten Reparse-Eintrag, ruft die
  produktive Akquisition auf und prüft `RepositoryCheckoutInvalid`, den
  unveränderten externen Sentinel und den nicht gelöschten Link. Die
  bestehende Attribut-Unit-Assertion bleibt ergänzend bestehen.
- **Warum:** Der Test darf auf berechtigtem Windows unverändert als
  Sicherheitsnachweis laufen; ein unberechtigter Host wird nur explizit
  als nicht nachgewiesene Capability markiert.

## Tests und Verifikation

- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCancellationTests" --logger "trx;LogFileName=Step017-Cancellation.trx"` — neuer Cleanup-Fehlpfad; Cancellation bleibt erhalten und der stabile Logcode ist sichtbar.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryAcquirerTests" --logger "trx;LogFileName=Step017-Acquirer.trx"` — alle erreichbaren Regressionen grün; auf unberechtigtem Host höchstens ein gezielter Capability-Skip und kein anderer Skip/Fehler.
- [ ] `dotnet build` — 0 Fehler und 0 Warnungen.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --logger "trx;LogFileName=Step017-FastTests.trx"` — kein Fehler; ein Capability-Skip wird im Result ausdrücklich als nicht ausgeführter Reparse-Nachweis ausgewiesen.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --logger "trx;LogFileName=Step017-IntegrationTests.trx"` — grün; Stress bleibt ausgeschlossen.
- [ ] Privilegierter Windows-Lauf der fokussierten und vollständigen FastTests — derselbe echte Reparse-Test läuft ohne Skip und besteht. Ist diese Umgebung im Step nicht verfügbar, bleibt der Reparse-Nachweis blockiert; kein Nutzer-/Systemeingriff und keine Fake-Assertion.
- [ ] Nach dem Code-Edit gezielte MCP-Prüfung mit absolutem
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`: `get_violations`
  für den Acquirer-Scope sowie `find_duplicates` nur für den unmittelbar
  betroffenen Produktionsscope. Neue Verstöße oder exakte Duplikate werden
  nicht als unabhängiger Sweep verfolgt, sondern nur bei direktem Bezug zu
  dieser Korrektur berücksichtigt.

Bei rotem oder abgeschnittenem Lauf sind die jeweilige TRX-Datei und nicht
ein pauschaler Wiederholungslauf auszuwerten. Ein Skip wird mit Anzahl,
Testname, Capability-Grund und Aussage „Sicherheitsnachweis nicht
ausgeführt“ in `step-result.md` dokumentiert; ein privilegierter Pass wird
separat als tatsächlich ausgeführter Nachweis angegeben.

## Definition of Done

- [ ] Ein fehlgeschlagener Cancellation-Cleanup-Versuch ist über den stabilen Code `RepositoryCleanupFailed` sichtbar, ohne Pfad-, Token-, URL- oder Exception-Geheimnisse zu loggen.
- [ ] Der ursprüngliche `OperationCanceledException`-Pfad bleibt unverändert echt: ursprünglicher CancellationToken, kein Provider-Failure-Result und kein Cleanup-Fehler maskiert die Cancellation.
- [ ] Eine direkte Regression beweist Sichtbarkeit, nicht löschenden Umgang mit verlorener Ownership und deterministisches Verhalten ohne globalen Logger- oder Collection-Zustand.
- [ ] Das Capability-Gate prüft eine echte Directory-Symlink-Fähigkeit, überspringt ausschließlich den explizit erkannten fehlenden Windows-Rechtefall und lässt alle anderen Fehler fehlschlagen.
- [ ] Die echte Reparse-Assertion und der externe Sentinel bleiben unverändert; ein privilegierter Lauf besteht ohne Skip, während ein unprivilegierter Skip ausdrücklich nicht als Sicherheitsnachweis gilt.
- [ ] Build, fokussierter Test und beide vollständigen Nicht-Stress-Gates werden mit Pass-/Skip-/Blockerstatus nachvollziehbar dokumentiert; Stress läuft nicht.
- [ ] Die Korrektur bleibt auf die zwei Verträge und zwei Schichten begrenzt; kein weiterer EPIC-04-Vertrag, kein Systemeingriff und keine Änderung an `task-state.md`, `codemap.md`, `tech-debt.md` oder `roadmap.md` entsteht.
- [ ] Der gezielte MCP-Regel-/DRY-Check bleibt ohne neue In-Scope-Verstöße; `TD-001` bis `TD-003` werden nicht ausgeweitet und es entsteht kein unabhängiger Audit-Step.

## Invarianten

- `OperationCanceledException` wird im Catch nicht in einen Providerfehler
  umgewandelt, sondern mit `throw;` einschließlich des ursprünglichen
  CancellationToken weitergereicht.
- Cleanup wird bei Cancellation genau einmal versucht. `false` ist kein
  Erfolg und erzeugt die stabile `RepositoryCleanupFailed`-Beobachtung;
  verlorene Ownership führt nicht zum Löschen eines fremden Pfads.
- Die Cleanup-Beobachtung enthält nur stabile, geheimnisfreie Vertragsdaten;
  keine Ausnahme-, URL-, Credential-, Token- oder absolute Checkout-Daten.
- Das Capability-Gate ist test-only und verändert keine Systemberechtigung.
  Es lässt den echten Symlink-Test unter Berechtigung unverändert laufen.
- Ein Capability-Skip ist ein Ausführungsstatus, nie ein Ersatz für den
  privilegierten Reparse-Sicherheitsnachweis.
- Testtemp- und Logger-Isolation bleiben lokal; keine globale
  Testserialisierung und kein OS-Temp-Pfad werden eingeführt.
- Netzwerk, Git, Credentials, Refresh, Cache, Snapshot, Workspace,
  Assembly-Loading und Reflection bleiben unberührt.

## Kontextbudget

### `read_first` (genau 12 Dateien)

1. `tasks/decompiled-assembly-analysis/step-016/step-review.md` — die
   zwei verbindlichen Findings und die Capability-Frage.
2. `tasks/decompiled-assembly-analysis/step-016/step-plan.md` — der
   ursprüngliche Akquisitions-/Ownership-Scope.
3. `tasks/decompiled-assembly-analysis/step-016/step-result.md` — der
   tatsächlich blockierte Test- und Cleanup-Zustand.
4. `tasks/decompiled-assembly-analysis/follow-up-strategy.md` —
   Split-Gates und Handoff-Regeln.
5. `tasks/decompiled-assembly-analysis/Konzept.md` — Sicherheitsvertrag
   und Teststrategie aus Phase 4.
6. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` —
   Cancellation-Catch und bestehende Logger-/Acquirer-Grenze.
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs` —
   Ownership- und Handle-Cleanupstatus.
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs` —
   sichere Cleanup- und Reparse-Entscheidung.
9. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs` —
   echter Reparse-Test und bestehende Test-Doubles.
10. `src/AiNetLinter/Logging/SystemLog.cs` — bestehender Serilog-
    Lebenszyklus und Nicht-Throwing-Logging-Vertrag.
11. `src/AiNetLinter.FastTests/AiNetLinter.FastTests.csproj` — xUnit-v3-
    und Projektabhängigkeiten für test-only Skip/Sink.
12. `Docs/integration.md` — bestehende Konvention für begründete
    Capability-/Tool-Skips.

### `read_on_demand`

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs` —
  nur zur Wiederverwendung des stabilen Diagnostic-Code-/Failure-Kontexts.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutReservation.cs` —
  nur zur Prüfung des Ownership-Markers für das neue Cancellation-Double.
- `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs` — nur für
  die exakte Transport-Double-Signatur.
- `src/AiNetLinter.TestKit/TestTempDirectory.cs` — nur für den sicheren
  Preflight- und Cleanup-Anschluss.
- `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonEndpointJanitor.cs` —
  nur als vorhandenes `Assert.Skip`-Muster.
- `src/AiNetLinter.FastTests/Logging/SystemLogClassifyTests.cs` — nur,
  falls der lokale Test-Sink an bestehende Logging-Testmuster angepasst
  werden muss.
- `Directory.Packages.props` und `src/AiNetLinter/AiNetLinter.csproj` —
  nur falls die bereits transitive Serilog-Abhängigkeit konkret bestätigt
  werden muss.

### `out_of_scope`

- Alle nicht unmittelbar am Cancellation-Catch liegenden Akquisitions-,
  Reservation-, Reparse- und Transportänderungen.
- Vollständige Solution-Lektüre, andere MCP-Registrierungen und alle
  EPIC-04-Folgepakete zu HTTP/Git, Credentials, Refresh, Cache und Snapshot.
- `task-state.md`, `codemap.md`, `tech-debt.md`, `roadmap.md` sowie neue
  Epics oder unabhängige Tech-Debt-Sweeps.
- Privilegien-/Developer-Mode-Aktivierung, Registry-/ACL-/Hoständerungen
  und jeder Versuch, den aktuellen Infrastrukturblocker zu umgehen.
- Attribute-Stubs, Fake-Reparse-Assertions, alternative Testpfade,
  globale Logger-/Collection-Manipulation und Stress-Tests.

## Risiken und Gegenmaßnahmen

- **Cancellation wird durch Beobachtung maskiert:** Die Beobachtung wird
  ausschließlich vor dem bestehenden `throw;` ausgeführt, erhält keinen
  Fehler-Resultatpfad und wird mit dem ursprünglichen Token getestet.
- **Logging verrät untrusted Daten oder den Checkout:** Der Log-Eintrag
  führt nur den zentralen Diagnostic-Code und eine feste deutsche Meldung;
  weder Exception noch Pfad, Token oder URL werden übergeben.
- **Logger-Test beeinflusst parallele Tests:** Der Coder verwendet einen
  instanzlokalen `ILogger`-/Sink-Aufbau und verändert weder `Log.Logger`
  global noch Test-Collections.
- **Capability-Gate verschluckt echte Testfehler:** Der Preflight filtert
  nur `ERROR_PRIVILEGE_NOT_HELD` (`1314`), nicht eine breite
  `UnauthorizedAccessException`. BCL-, Pfad-, ACL-, Cleanup- oder
  Reparse-Fehler außerhalb dieses Codes bleiben rot.
- **Skip wird als Sicherheitsnachweis missverstanden:** Der Skip-Grund,
  Testname und die fehlende Nachweisaussage werden im Harness und Result
  festgehalten; die Abnahme verlangt den privilegierten Lauf ohne Skip.
- **Scope-Drift in weitere EPIC-04-Verträge:** Der Coder ändert nur den
  bestehenden Acquirer-Cancellationpfad und test-only Harness-Dateien.

## Sicherer Coder-Einstieg und Handoff

Der Coder beginnt mit dem `read_first`-Block und prüft anschließend den
aktuellen Konstruktor-Call-Scope über `ExternalSourceRepositoryAcquirer`
und die Methodenkörper von `AcquireReservedCheckoutAsync` und
`ExternalSourceCheckoutOwnership.TryCleanup`. Danach gilt folgende
Reihenfolge:

1. Den instanzlokalen Serilog-Seam im bestehenden Acquirer einführen, ohne
   einen neuen öffentlichen oder Provider-Vertrag zu schaffen.
2. Den Cancellation-Catch so ändern, dass Cleanup-`false` genau einmal mit
   dem stabilen Diagnostic-Code beobachtet wird und `throw;` unverändert
   bleibt.
3. Die direkte Cleanup-Fehler-Regression mit einem lokalen Sink und einem
   verlorenen Ownership-Marker ergänzen.
4. Den test-only Capability-Preflight dokumentiert ergänzen und ihn vor
   den bestehenden Reparse-Test setzen; den Testkörper selbst nicht
   umschreiben.
5. Fokussierte Tests, Build und beide Nicht-Stress-Gates ausführen. Bei
   einem Capability-Skip den Nachweisstatus getrennt von der Runner-
   Erfolgsampel dokumentieren; den privilegierten Lauf nicht simulieren.
6. Abschließend nur die für diesen Scope relevanten MCP-Prüfungen und
   gezielte Text-/Diff-Prüfungen durchführen.

Relevante MCP-Symbole:

- `ExternalSourceRepositoryAcquirer.AcquireReservedCheckoutAsync`
- `ExternalSourceRepositoryAcquirer.ExternalSourceRepositoryAcquirer`
- `ExternalSourceCheckoutOwnership.TryCleanup`
- `ExternalSourceRepositoryPathGuard.TryDeleteOwnedCheckout`
- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryAcquirerTests.ReparsePointAttributes_AreRecognizedWithoutCreatingExternalLinks`
- `SystemLog`

Unveränderliche Handoff-Verträge:

- Cancellation bleibt echte Cancellation mit demselben Token.
- Cleanup-Fehler werden stabil und geheimnisfrei beobachtet; fremde Pfade
  bleiben unangetastet.
- Der Reparse-Test bleibt ein echter `Directory.CreateSymbolicLink`-Test.
- Ein unberechtigter Host darf nur den explizit begrenzten Capability-Skip
  erhalten; dieser gilt nicht als Sicherheitsnachweis.
- Kein Code außerhalb des Acquirer-Cancellationpfads und des test-only
  Harnesses wird erweitert.

Nächster sicherer Einstiegspunkt: `ExternalSourceRepositoryAcquirer.cs`
im Catch von `AcquireReservedCheckoutAsync`, danach die neue fokussierte
Regression isoliert gegen einen instanzlokalen Sink testen.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#agent-resilience` — keine stille
  Fehlerbehandlung; der Cleanup-Fehler muss sichtbar bleiben, während
  `OperationCanceledException` korrekt weitergereicht wird.
- `.agents/rules/AiNetLinterRichtlinien.mdc#§3-§5` — xUnit-v3-Regressionen,
  zentrale Testtemp-Verzeichnisse, parallele Tests, keine abgeschwächten
  Assertions, DRY-/MagicValues-/DeadCode-Prävention und Zero-Warning-Gates.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` —
  C#-Semantik und gezielte Audits zuerst über MCP mit absolutem
  `projectRoot`, Textarbeit gezielt über `rg`.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md#§6.2.1` —
  flacher Fix-Step mit `corrects: step-016`, ausschließlicher Findings-
  Scope und verpflichtendem Kritikerlauf.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md#§8.1-§8.3` —
  Cleanup-/Logik-Finding bleibt korrigierbar; externe Architekturbeobach-
  tungen werden nicht automatisch zu neuen Steps.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md#§10.6-§10.7` —
  Pointer-/Scope-Disziplin, kompakte Resultate und getrennte Darstellung
  von Skip- und Nachweisstatus.

## Bekannte Ausnahmen

- Der aktuelle Windows-Host besitzt nach read-only-Prüfung weder sichtbar
  `SeCreateSymbolicLinkPrivilege` noch einen auslesbaren Developer-Mode-
  Nachweis. Ein Capability-Skip kann deshalb den fokussierten Runner ohne
  Testfehler beenden, ersetzt aber nicht den erforderlichen privilegierten
  Lauf und darf den Step nicht als vollständig nachgewiesen ausweisen.
- Ein anderer Fehlercode oder eine unerwartete Ausnahme beim Preflight ist
  kein zulässiger Skip-Grund und muss den Test fehlschlagen lassen. Wird der
  fehlende Rechtezustand nicht als `ERROR_PRIVILEGE_NOT_HELD` (`1314`)
  sichtbar, ist kein ehrlicher Capability-Skip möglich; dann bleibt die
  Umgebung als harter Infrastrukturblocker dokumentiert.

## Code-Skizze (optional)

```csharp
catch (OperationCanceledException)
{
    if (!ownership.TryCleanup())
    {
        logger.Warning(
            "Externer Repository-Checkout konnte nach Cancellation nicht bereinigt werden. Code={Code}",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed);
    }

    throw;
}
```

Der Capability-Preflight prüft denselben echten Symlink-Aufruf wie der
Reparse-Test, verwendet nur einen isolierten `TestTempDirectory` und
überspringt ausschließlich den benannten fehlenden Rechtecode. Er ersetzt
weder den Testkörper noch dessen Sentinel-Assertion.

## Notes

- Dies ist ein Fix-Modus-Step; `roadmap.md` wird nicht verändert. Der
  Orchestrator prüft nach dem Coder-/Kritiker-Zyklus erneut, ob der
  privilegierte Reparse-Nachweis noch als Infrastrukturblocker besteht.
- `step-result.md` muss bei einem unprivilegierten Lauf die Runner-Ampel
  (Pass mit gezieltem Skip) und den fachlichen Nachweisstatus (Reparse nicht
  ausgeführt) getrennt ausweisen. Ein privilegierter Lauf darf für denselben
  Test keinen Skip melden.
- Die geplanten Coder-Commits bleiben lokal und tragen den Task-Suffix
  `[decompiled-assembly-analysis]`; kein Push und keine Historienänderung.
