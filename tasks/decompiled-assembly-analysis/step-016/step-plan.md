---
status: blocked
type: step-plan
task: decompiled-assembly-analysis
step: 016
corrects: step-015
title: "Repository-Akquisitionsgrenze sicher korrigieren"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T01:16:47+02:00
related_to:
  - step-015/step-review.md
  - step-015/step-plan.md
  - step-015/step-result.md
  - follow-up-strategy.md
  - Konzept.md
---

# Step 016: Repository-Akquisitionsgrenze sicher korrigieren

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und
  Fehlersemantik.
- **Korrektur von:** Step 015, Review-Commit `b1dac89b`, mit fünf
  zusammengehörigen Findings an der Transport-/Checkout-Besitzgrenze.
- **Konzept-Referenz:** `Konzept.md`, Phase 4 sowie die Abschnitte
  „Fehler-, Sicherheits- und Vertrauensvertrag“ und „Teststrategie“.

## Split-Gate und Entscheidung

Step 016 bleibt ein einzelner, vertikaler Korrektur-Step. Alle fünf
Findings betreffen denselben Übergang: Ein untrusted Transport erhält einen
von der Fassade kontrollierten Checkout und darf nur ein typisiertes,
diagnostisch sicheres Ergebnis oder eine echte Cancellation zurückgeben.
Eine Aufteilung würde entweder den Fehlerpfad ohne Cleanup oder den
Ownership-Nachweis ohne direkte Regressionen zurücklassen.

- **Eng gekoppelte Fachverträge:** genau zwei:
  1. der Transport-/Ergebnisvertrag für Ausnahmeabbildung,
     Cancellation und sichere Diagnosen;
  2. der Checkout-Ownership-Vertrag für Reservierung, Reparse-Grenze,
     Cleanup und Handle-Lebensdauer.
- **Schichten:** genau drei:
  1. Ausnahme-, Cancellation- und Diagnose-Sicherheitsvertrag;
  2. Ownership-, Reparse- und Cleanup-Grenze;
  3. direkte Regressionen sowie die Zentralisierung des gemeinsamen
     Dateisystem-Exception-Helpers.
- **Akzeptanzkriterien:** acht.
- **`read_first`:** zwölf Dateien.
- **Risikoeinstufung:** `high`, weil ein Fehler die Quelle eines
  fremden Checkouts oder geheime Transportdetails offenlegen kann.

Der exakte `IsFileSystemException`-Klon ist Teil der dritten Schicht und
wird in diesem Step zentralisiert. `TD-001` bis `TD-003` bleiben unberührt;
es wird kein unabhängiger DRY-, MagicValues- oder DeadCode-Sweep eröffnet.

## Scope

### In Scope

- Die bestehende `IGiteaRepositoryTransport`-Fassade einschließlich
  `ExternalSourceRepositoryTransportResult` und
  `ExternalSourceCheckoutHandle` korrigieren.
- Alle nicht-Cancellation-Transportausnahmen in vorhandene
  `ExternalSourceProviderFailureKind`-Werte überführen und jeden Pfad über
  den eigenen Cleanup führen.
- Das Transportergebnis nach dem `await` nochmals gegen Cancellation prüfen.
- Transportdiagnosen an der Vertragsgrenze auf stabile Codes, feste
  geheimnisfreie Nachrichten und sichere Locations reduzieren.
- Einen atomaren, besitzgebundenen Checkout-Child reservieren und vor sowie
  nach dem Transport die Parent-Kette, Reparse-Zustände und die
  Ownership-Identität prüfen.
- Cleanup-Ergebnisse am Fehlerpfad und beim Handle-Dispose bewusst und
  diagnostisch sichtbar behandeln; fremde Pfade niemals löschen.
- Den gemeinsamen Exception-Helper in einem internen, von Acquirer und
  PathGuard verwendeten Policy-Typ zentralisieren.
- Direkte Windows-Regressionen für unbekannte Ausnahmen, Cancellation nach
  Transporterfolg, geheime Diagnosen, fremden Arbeitsbaum, tatsächliche
  Reparse-Punkte und Cleanup-Fehler ergänzen.

### Out of Scope

- Keine konkrete `HttpClient`-, LibGit- oder Prozessimplementierung, kein
  echter Netzwerk-, Gitea- oder Git-Aufruf und keine Credential-Bindung.
- Keine Mapping-/`appsettings.json`-Erweiterung für Credentials,
  Staging-Wurzel, Branch oder Cache.
- Kein Fetch, Refresh, Retry, Branchwechsel, persistenter Repository-Cache,
  Manifest, Snapshot, Workspace-Bau oder atomare Source-Veröffentlichung.
- Keine Änderungen an `IExternalSourceProvider`,
  `AssemblySourceSelectionOrchestrator`, Host-Wiring, MCP-Registrierung,
  `task-state.md`, `codemap.md`, `tech-debt.md` oder `roadmap.md`.
- Kein `Assembly.Load`, keine `AssemblyLoadContext`- oder
  Reflection-Ausführung, keine externen Tests und keine `Stress`-Tests.
- `TD-001` bis `TD-003` werden weder neu bewertet noch in eigene Arbeit
  umgewandelt; Magic Values und Dead Code werden nur im unmittelbar
  berührten Akquisitionspfad vermieden bzw. geprüft.

## Aktueller Projektzustand (JIT-Kontext)

Die bestehende Fassade liegt vollständig in
`src/AiNetLinter/Mcp/Assemblies`:

- `ExternalSourceRepositoryAcquirer.AcquireAsync` reserviert aktuell nur
  einen per Existenzprüfung ausgewählten GUID-Child. Nach dem Transport gibt
  es keine Cancellation-Prüfung; `ExecuteTransportAsync` fängt nur eine
  begrenzte Ausnahmegruppe und reicht erfolgreiche Transportdiagnosen direkt
  weiter.
- `ExternalSourceRepositoryPathGuard` prüft Pfadpräfixe und
  `FileAttributes.ReparsePoint`, verwendet aber einen eigenen exakten
  `IsFileSystemException`-Klon. Die aktuelle Löschlogik kann eine verlorene
  Ownership-Identität nicht als solchen Zustand sichtbar machen.
- `ExternalSourceCheckoutHandle.Dispose` ignoriert das Ergebnis von
  `TryDeleteOwnedCheckout`. Die Akquisitionsfehlerpfade melden Cleanup zwar
  grundsätzlich, aber die neue Korrektur muss das auch für das Handle-Dispose
  bewusst festlegen.
- `ExternalSourceRepositoryTransportResult` materialisiert beliebige
  Diagnosen ohne sichere Projektion. Die vorhandenen stabilen Codes in
  `ExternalSourceConfigurationDiagnosticCodes` können für die Fassade
  wiederverwendet werden; ein neuer öffentlicher Mapping-Vertrag ist nicht
  nötig.

Die MCP-Abfragen mit absolutem `projectRoot`
`C:\Daten\Entwicklung\Ralf\AiNetLinter` ergeben für den Scope:

- `find_symbol`, `get_class_structure` und `get_symbol_body` bestätigen die
  betroffenen Methoden und ihre aktuellen Zeilenbereiche.
- `find_references`/`get_impact` zeigen `AcquireAsync` ausschließlich in der
  bestehenden direkten Acquirer-Testklasse; der PathGuard-Cleanup wird vom
  Acquirer, dem Checkout-Handle und Tests verwendet.
- `get_test_context` ordnet aktuell elf Component-Tests der Acquirer-Fassade
  zu; die vorhandene Reparse-Regression prüft nur das Attribut-Bit.
- Der gezielte `find_duplicates`-Audit findet genau den exakten
  `IsFileSystemException`-Klon in Acquirer und PathGuard. Der gezielte
  `get_violations`- und High-Confidence-`find_dead_code`-Scope meldet keine
  weiteren Befunde; `find_magic_values` meldet dort keine Sicherheits-
  Kandidaten.

Die vorhandenen Tests verwenden `TestTempDirectory` und ein lokales
Transport-Double. Sie müssen auf die neue Reservierungssemantik angepasst
werden: Ein beim Transport bereits vorhandener, aber leer reservierter Child
ist kein wiederverwendeter Arbeitsbaum.

## Intention

Nach diesem Step ist die Akquisitionsfassade eine geschlossene
Sicherheits- und Besitzgrenze: Jede Transportausnahme wird typisiert und
geheimnisfrei zurückgegeben oder als echte Cancellation weitergereicht;
jeder nicht erfolgreich verifizierte Child wird sicher behandelt, ohne
fremde Arbeitsbäume zu betreten oder zu löschen. Die direkten Regressionen
belegen die Vertragsgrenze unter Windows, ohne Netzwerk-, Git- oder
Assembly-Ausführung einzuführen.

## Konkrete Änderungen

### Schicht 1: Ausnahme-, Cancellation- und Diagnose-Sicherheitsvertrag

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs` (neu)

- **Was:** Einen kleinen internen Policy-Helper einführen, der die bisher
  duplizierte `IsFileSystemException`-Klassifikation genau einmal enthält,
  Transportausnahmen klassifiziert und die sichere Transportdiagnose-
  projektion bündelt.
- **Warum:** Ausnahmeabbildung, Diagnose-Sicherheit und DRY dürfen nicht
  als voneinander abweichende lokale Regeln in Acquirer und PathGuard
  weiterlaufen.
- **Vertrag:** `OperationCanceledException` ist kein normaler Failure.
  `HttpRequestException` wird als `NetworkUnavailable`,
  `TimeoutException` als `Timeout`, ein Berechtigungsfehler als
  `AccessDenied` und sonstige nicht-Cancellation-Ausnahmen als
  `InvalidResponse` abgebildet. Die bestehende
  `ExternalSourceProviderFailureKind`-Enumeration bleibt die einzige
  Failure-Typquelle.

#### `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs`

- **Was:** `ExternalSourceRepositoryTransportResult` an der Konstruktor-
  grenze normalisieren: nur bekannte/stabile Diagnosecodes oder ein
  zentraler generischer Transportcode, feste erlaubte Severity-Werte,
  eine sichere feste Location und eine feste geheimnisfreie Nachricht
  dürfen weitergegeben werden. URL-Userinfo, Token-/Passwort-Fragmente,
  Header- oder Exception-Texte dürfen weder in `Code`, `Message` noch
  `Location` aus dem Transportergebnis austreten.
- **Warum:** Der Port ist die erste Stelle, an der untrusted
  Transportdiagnosen in den Acquirer-Vertrag gelangen.
- **Was bleibt:** `IsAvailable`, geladene Revision und typisierte
  `FailureKind` behalten ihre bestehende Validierung; Diagnosewerte werden
  immutable nach der sicheren Projektion gespeichert.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`

- **Was:** `ExecuteTransportAsync` so schließen, dass die Reihenfolge
  `OperationCanceledException` zuerst weiterreicht, danach die vollständig
  definierte nicht-Cancellation-Ausnahmeabbildung greift und jeder
  Fehlerweg den reservierten eigenen Child über `FailAfterCleanup`
  behandelt. Keine Exception-Nachricht und kein `ToString()` darf in eine
  Rückgabe-Diagnose gelangen.
- **Was:** Direkt nach dem erfolgreichen `await` des Transports und vor
  jeder Handle-Übergabe `cancellationToken.ThrowIfCancellationRequested()`
  im cleanup-besitzenden Pfad ausführen. Wird der Token während eines
  scheinbar erfolgreichen Transports gesetzt, wird der Child bereinigt und
  die originale `OperationCanceledException` unverändert weitergereicht.
- **Was:** Die Fassade darf nur bereits normalisierte Diagnosen in ein
  Acquisition-Ergebnis übernehmen; beim Erzeugen eigener Diagnosen werden
  die vorhandenen zentralen Codes und feste Texte verwendet.

### Schicht 2: Ownership-, Reparse- und Cleanup-Grenze

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`

- **Was:** `TryCreateCheckoutPath` von einer
  „nicht vorhanden“-Prüfung auf eine atomare Reservierung umstellen. Der
  erzeugte Child erhält ein eindeutiges Ownership-Token bzw. eine
  nachprüfbare Verzeichnisidentität; die Reservierung liegt innerhalb der
  kanonischen Staging-Wurzel und ist bei Kollision kein fremder
  Arbeitsbaum.
- **Was:** Vor der Transportübergabe und nach deren Rückkehr die gesamte
  Parent-Kette, die kanonische Checkout-Identität und die Reparse-Zustände
  prüfen. Ein ersetztes Verzeichnis, ein Symlink/Junction/Reparse-Punkt,
  ein fremder Arbeitsbaum oder ein nicht mehr belegbarer Owner führt zu
  `RepositoryCheckoutInvalid` und niemals zu einer Handle-Übergabe.
- **Was:** Die Reservierungssemantik im Transportvertrag festhalten: Der
  Transport erhält einen eigens reservierten, leeren Ziel-Child und darf
  keinen vorhandenen Checkout still wiederverwenden. Die Verifikation darf
  nicht allein auf Stringpräfixen und `Directory.Exists` beruhen.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs`

- **Was:** Pfad- und Reparse-Prüfungen auf den reservierten Besitz
  ausrichten. Rekursives Cleanup bleibt nicht-traversierend für Reparse-
  Punkte; bei verlorener Ownership oder unklarer Identität wird der fremde
  Pfad nicht gelöscht und `false` zurückgegeben.
- **Was:** Alle Dateisystemausnahmen über den neuen gemeinsamen Policy-
  Helper klassifizieren. Den lokalen `IsFileSystemException`-Klon
  entfernen; keine zweite semantisch gleiche Methode einführen.
- **Was:** Die Parent-Prüfung muss auch Staging-Wurzel und Checkout-
  Identität vor Cleanup abdecken. Ein echter Reparse-Child darf nicht als
  normales Verzeichnis betreten werden; ein nachweislich eigener Reparse-
  Eintrag darf höchstens als Link selbst, nie als Zielbaum, entfernt werden.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`

- **Was:** `ExternalSourceCheckoutHandle` mit der Ownership-Identität und
  einem expliziten Cleanup-Ergebniszustand ausstatten. `Dispose` bleibt
  idempotent, verwirft aber den Rückgabewert von
  `TryDeleteOwnedCheckout` nicht mehr: Erfolg oder ein sicherer
  `RepositoryCleanupFailed`-Zustand muss für den internen Vertrag
  beobachtbar sein, ohne fremde Pfade zu löschen.
- **Warum:** Ein erfolgreich erzeugter Handle darf bei Dispose keinen
  stillen Cleanup-Fehler vortäuschen. Das Handle bleibt ausschließlich für
  den eigenen reservierten Child zuständig.

### Schicht 3: Direkte Regressionen und DRY-Verifikation

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`

- **Was:** Das bestehende Transport-Double so erweitern, dass es
  kontrolliert unbekannte Ausnahmen wirft, nach dem Schreiben eines gültigen
  Checkouts trotz gesetztem Token Erfolg liefert, geheime Diagnosen liefert,
  den reservierten Child durch einen fremden Arbeitsbaum ersetzt und echte
  Reparse-Punkte erzeugt.
- **Was:** Direkte Tests ergänzen oder bestehende Tests präzisieren für:
  - `HttpRequestException`, `TimeoutException` und eine sonstige
    nicht-Cancellation-Ausnahme: korrekter typed Failure, generische
    Diagnose und eigener Cleanup;
  - Cancellation nach Transporterfolg: exakter Token, keine erfolgreiche
    Handle-Übergabe und Cleanup;
  - Diagnoseprojektion mit URL-Userinfo, Bearer-/Token-Fragment und
    exception-nahem Text: kein Geheimnis in Code, Message oder Location;
  - atomare Reservierung eines leeren eindeutigen Childs sowie Ablehnung
    eines ersetzten/fremden Arbeitsbaums;
  - tatsächlichen Symlink/Junction/Reparse-Ausbruch innerhalb bzw. am
    Checkout und Nachweis, dass das externe Sentinel unverändert bleibt;
  - Cleanup-Fehler bzw. verlorene Ownership: sichtbarer Cleanup-Zustand,
    kein Löschen des fremden Pfads und idempotentes Handle-Verhalten;
  - weiterhin Erfolg, typisierte Provider-Failures, fehlende Solution,
    Mapping-Schutz und Erhalt des fremden Staging-Inhalts.
- **Was:** Die bisherige Erwartung „Destination war beim Transport nicht
  vorhanden“ auf die neue, ausdrücklich leere Reservierung umstellen. Kein
  Test darf `Path.GetTempPath`, eigene OS-Temp-Dateien, Netzwerk, Git,
  Prozesse, Restore-Quellen oder Assembly-Loading verwenden.

## Tests und Verifikation

Während der Implementierung:

```powershell
dotnet test src/AiNetLinter.FastTests --filter Category=Unit
dotnet test src/AiNetLinter.FastTests --filter Category=Component
```

Vor der Übergabe müssen der neue direkte Test-Scope und danach die
vollständigen Nicht-Stress-Gates grün sein:

```powershell
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ExternalSourceRepositoryAcquirerTests
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Stress bleibt ausgeschlossen. Bei rotem oder abgeschnittenem Lauf ist die
TRX-Ausgabe gezielt auszuwerten; kein Test darf durch abgeschwächte
Assertions grün gemacht werden.

Nach dem Code-Edit sind für den unmittelbar berührten Produktionsscope
erneut die MCP-Prüfungen `get_violations`, `find_duplicates` mit
`minTokens=1` und `similarityThreshold=exact`, `find_magic_values` sowie
`find_dead_code` auszuführen. Der Exact-Cluster zwischen Acquirer und
PathGuard muss verschwunden sein; neue Magic-Value- oder High-Confidence-
Dead-Code-Befunde dürfen nicht liegen bleiben.

## Definition of Done

- [ ] Alle fünf Findings aus `step-015/step-review.md` sind in dieser
  einen Akquisitions-/Ownership-Grenze behoben.
- [ ] Höchstens zwei eng gekoppelte Fachverträge und genau drei Schichten
  bleiben erkennbar; kein Provider-, Host-, Cache- oder Snapshot-Wiring
  wurde vorgezogen.
- [ ] Jede nicht-Cancellation-Transportausnahme wird typisiert, sicher
  diagnostiziert und bereinigt; Cancellation nach Transporterfolg bleibt
  echte Cancellation.
- [ ] Reservierung, Parent-/Reparse-/Identitätsprüfung und Cleanup löschen
  niemals einen fremden Arbeitsbaum; Cleanup-Fehler sind beobachtbar.
- [ ] Die direkten Windows-Regressionen decken alle neuen Sicherheits- und
  Ownership-Aussagen ab und verwenden nur das zentrale TestKit.
- [ ] Der exakte `IsFileSystemException`-Klon ist durch genau einen
  gemeinsamen internen Helper ersetzt; keine neuen Magic Values oder
  unreferenzierten Produktionssymbole bleiben im Scope.
- [ ] `dotnet build`, die vollständigen FastTests- und
  IntegrationTests-Nicht-Stress-Gates sind grün; Stress wurde nicht
  ausgeführt.
- [ ] Der Coder schreibt `step-016/step-result.md`, setzt den Planstatus
  auf `done (pending audit)` und lässt `task-state.md`, `codemap.md`,
  `tech-debt.md` und `roadmap.md` unverändert.

## Invarianten

- `OperationCanceledException` wird vor jeder allgemeinen
  Ausnahmeabbildung behandelt, mit dem ursprünglichen CancellationToken
  weitergereicht und nicht als Providerfehler maskiert.
- Ein erfolgreicher Acquisition-Result trägt nur einen nachweisbar eigenen
  Checkout, eine innerhalb dieses Checkouts liegende Solution und eine
  nichtleere geladene Revision.
- Ein nicht erfolgreich verifizierter oder nach Cancellation verworfener
  Checkout erhält keinen verwendbaren Handle.
- Staging-Wurzel, Parent-Kette, Checkout und Solution werden kanonisch und
  reparse-sicher geprüft; Cleanup folgt keinem Reparse-Ziel.
- Transportdiagnosen enthalten ausschließlich zentral erlaubte Codes,
  feste geheimnisfreie Texte und sichere Locations; Exception-Texte,
  Credentials und URL-Userinfo bleiben außerhalb des Ergebnisses.
- Fehler-Cleanup und Handle-Cleanup dürfen ausschließlich den eigenen,
  noch belegbaren Besitz betreffen. Bei verlorener Ownership bleibt ein
  fremder Pfad unangetastet und der Cleanup-Fehler wird sichtbar.
- Der Step erzeugt oder nutzt weder Netzwerk/Git noch Snapshots, Caches,
  Workspaces, Assembly-Loading oder Reflection.

## Kontextbudget

### `read_first` (genau 12 Dateien)

1. `tasks/decompiled-assembly-analysis/step-015/step-review.md` — die
   fünf verbindlichen Findings und ihre Fix-Anweisungen.
2. `tasks/decompiled-assembly-analysis/step-015/step-plan.md` — der
   ursprüngliche Scope und die bewusst gesetzten Grenzen.
3. `tasks/decompiled-assembly-analysis/step-015/step-result.md` — der
   tatsächlich gelieferte Besitz-/Transportzustand.
4. `tasks/decompiled-assembly-analysis/follow-up-strategy.md` — Split-
   Gates und Handoff-Regeln für Folge-Steps.
5. `tasks/decompiled-assembly-analysis/Konzept.md` — Phase 4,
   Sicherheitsvertrag und Teststrategie.
6. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` —
   aktuelle Akquisitionsreihenfolge, Ausnahme- und Cleanup-Pfade.
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs` —
   Pfad-, Reparse- und Löschgrenze.
8. `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs` — Port
   und Transportergebnis einschließlich Diagnosematerialisierung.
9. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs` —
   Checkout-Handle und Acquisition-Ergebnis.
10. `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs` —
    vorhandene Diagnosecodes und deren Format.
11. `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs` — einzige
    bestehende `ExternalSourceProviderFailureKind`-Enumeration.
12. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs` —
    bestehendes Double und direkte Regressionen.

### `read_on_demand`

- `src/AiNetLinter.TestKit/TestTempDirectory.cs` und
  `src/AiNetLinter.TestKit/IsolatedFixtureLease.cs` nur zum Anschluss an
  die bestehenden TestKit- und Besitzmuster.
- `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs` nur zum Abgleich
  der bereits verwendeten nicht-traversierenden Reparse-Semantik; keine
  Übernahme eines fremden Vertrags ohne Begründung.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceConfigurationLoader.cs`,
  `AssemblySourceSelectionOrchestrator.cs` und Host-/Support-Tests nur für
  einen gezielten Regression-Check, nicht zur Änderung.
- Die Projektdateien nur, falls die konkrete Windows-/BCL-API für
  Symbolic-Link-/Junction-Regressionen geprüft werden muss; keine neue
  Paketabhängigkeit.

### `out_of_scope`

- Vollständige Solution-Lektüre, nicht betroffene MCP-Registrierungen und
  alle nachgelagerten Snapshot-/Workspace-/Refresh-Dateien.
- Produktiver Gitea-/Git-/HTTP-Transport, Credentials, echte Hosts,
  Netzwerk, externe Repositories und externe Test-Suites.
- `task-state.md`, `codemap.md`, `tech-debt.md`, `roadmap.md` und andere
  Task-Artefakte außerhalb von `step-016/step-plan.md`.
- Unabhängige Audits oder Refactorings für `TD-001` bis `TD-003` sowie
  Änderungen an nicht unmittelbar betroffenen DRY-, MagicValues- oder
  DeadCode-Kandidaten.

## Risiken und Gegenmaßnahmen

- **Reservierung wird nur behauptet:** Der Coder muss eine atomare
  Besitzreservierung und einen vor/nach dem Transport prüfbaren
  Identitätsnachweis verwenden; ein `Exists`-Vorcheck allein ist nicht
  akzeptabel.
- **Fremder Reparse- oder Arbeitsbaum wird gelöscht:** Jede Cleanup-
  Operation prüft Ownership und Reparse-Zustand erneut und verweigert sich
  bei Unsicherheit. Der direkte Test schützt ein außerhalb liegendes
  Sentinel.
- **Cleanup-Fehler wird wieder verschluckt:** Failure-Result und Handle-
  Zustand enthalten eine stabile Cleanup-Diagnose bzw. einen sichtbaren
  Fehlerzustand; `Dispose` darf den booleschen Rückgabewert nicht ignorieren.
- **Secret-Leak über ungeplante Felder:** Der Transport liefert niemals
  rohe Diagnoseobjekte weiter. Codes werden erlaubt/normalisiert,
  Nachrichten und Locations werden aus festen sicheren Werten erzeugt.
- **Cancellation wird in Failure umgewandelt:** Der
  `OperationCanceledException`-Pfad steht vor dem allgemeinen Catch und
  prüft zusätzlich den Token direkt nach dem Transport-`await`.
- **Scope-Drift in Richtung Gitea oder Snapshot:** Der Coder ändert keine
  Provider-/Host-/Cache-Datei; bei fehlender Plattform- oder
  Identitätsgarantie wird die Akquisitionsgrenze sicher verengt und im
  Result als Blocker gemeldet, nicht durch Folgeinfrastruktur umgangen.
- **Testumgebung unterstützt keinen echten Reparse-Punkt:** Der direkte
  Windows-Test darf die Sicherheitsaussage nicht durch einen reinen
  Attribut-Stub ersetzen. Kann die Plattform die erforderliche lokale
  Testanlage nicht ausführen, ist das als Infrastruktur-Blocker zu melden.

## Sicherer Coder-Einstieg und Handoff

Der Coder startet ausschließlich an der bestehenden Fassade und liest zuerst
den Abschnitt „Aktueller Projektzustand“, die Invarianten und den
`read_first`-Block. Danach führt er diese Reihenfolge aus:

1. `ExternalSourceRepositoryFailurePolicy` als einzigen gemeinsamen
   Exception-/Diagnose-Policy-Typ entwerfen und die vorhandenen
   Failure-/Diagnostic-Werte gegen `IExternalSourceProvider.cs` und
   `ExternalSourceConfiguration.cs` abgleichen.
2. `ExternalSourceRepositoryTransportResult` und
   `ExternalSourceRepositoryAcquirer` korrigieren; zuerst Exception- und
   Cancellation-Pfade, dann die sichere Diagnoseprojektion.
3. Reservierung, Ownership-Token/-Identität, Reparse-Nachprüfung und
   Cleanup-Status in Acquirer, PathGuard und Handle schließen.
4. Die bestehende Acquirer-Testklasse um echte, lokale Windows-Repros
   erweitern und die alte Reservierungsassertion anpassen.
5. Die gezielten MCP-Audits und anschließend die vollständigen
   Nicht-Stress-Gates ausführen.

Relevante MCP-Symbole für die erneute Prüfung:

- `ExternalSourceRepositoryAcquirer.AcquireAsync`
- `ExternalSourceRepositoryAcquirer.ExecuteTransportAsync`
- `ExternalSourceRepositoryAcquirer.TryCreateCheckoutPath`
- `ExternalSourceRepositoryAcquirer.TryValidateCheckout`
- `ExternalSourceRepositoryAcquirer.FailAfterCleanup`
- `ExternalSourceRepositoryPathGuard.TryDeleteOwnedCheckout`
- `ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath`
- `ExternalSourceRepositoryPathGuard.ContainsReparsePointInTree`
- `ExternalSourceRepositoryTransportResult` und sein Konstruktor
- `ExternalSourceCheckoutHandle.Dispose`
- `IGiteaRepositoryTransport.CloneDefaultBranchAsync`
- `ExternalSourceConfigurationDiagnosticCodes`

Der Coder darf `IExternalSourceProvider`, Orchestrator, Host-Wiring,
Snapshot-/Cache-Dateien und Mapping-Konfiguration nicht ändern. Er darf
keinen echten Transport anschließen. Bei einem nicht belastbar beweisbaren
Ownership-/Reparse-Vertrag muss er den Step anhalten und den konkreten
Blocker im Result vermerken.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — nullable-/sealed-konformer C#-Code,
  kurze Methoden, keine stille Fehlerbehandlung, keine Reflection oder
  Runtime-Assembly-Ausführung.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — keine DI-/Plugin-
  Infrastruktur, zentrale Test-Temp-Verzeichnisse, xUnit-v3-Regressionen,
  DRY-/MagicValues-/DeadCode-Prävention und Zero-Warning-Gates.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — AiNetLinter-MCP mit
  absolutem `projectRoot` für Symbole, Referenzen, Impact und Audits;
  `rg` nur für gezielte Textarbeit.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` — Fix-Step-
  Semantik, Scope-Disziplin, Kontextbudget, Cleanup der Artefakte und
  Korrekturketten.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` —
  serieller Coder-/Kritiker-Zyklus und Zuständigkeit für Task-State,
  Roadmap und Tech-Debt.

## Bekannte Ausnahmen

- Die Reparse-Regression ist bewusst Windows-spezifisch, weil genau dort
  die produktive Pfad- und Linksemantik geprüft wird. Eine fehlende lokale
  Berechtigung oder BCL-Unterstützung ist kein Anlass, den Test auf eine
  Attributsimulation zurückzustufen; sie ist als Infrastruktur-Blocker zu
  melden.

## Notes

Dieser Fix-Step ändert die Akquisitions-/Ownership-Fassade, nicht die
nachgelagerte Source-Solution-Auflösung. Produktiver Transport,
Credential-Bindung, Refresh, Cache, atomare Veröffentlichung und
Snapshot-/Workspace-Materialisierung bleiben die späteren EPIC-04-/EPIC-05-
Grenzen. `roadmap.md` wird im Fix-Modus nicht geändert.
