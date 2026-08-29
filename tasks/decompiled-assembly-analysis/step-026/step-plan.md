---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 026
corrects: null
title: "Persistente Repository-Cache-Generation aus erfolgreichem Clone atomar veröffentlichen"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T15:31:12+02:00
related_to:
  - ../step-025/step-plan.md
  - ../step-025/step-result.md
  - ../step-025/step-review.md
  - ../roadmap.md
  - ../follow-up-strategy.md
  - ../Konzept.md
  - ../tech-debt.md
  - ../codemap.md
---

# Step 026: Persistente Repository-Cache-Generation atomar veröffentlichen

## Split-Gate-Entscheidung

Der bisherige Step-026-Plan wird **nicht freigegeben**. Er bündelt Cache-
Konfiguration, Cache-Identität/Manifest, Refresh-/Fetch-Transport, Generationen,
Integritätsprüfung, atomaren Current-Pointer, Konkurrenzsynchronisation und
den besitzgebundenen Request-Checkout. Das sind im tatsächlichen Code mehr als
ein schwergewichtiger Primärvertrag und mehr als drei gekoppelte Schichten.

Die Überarbeitung erfolgt ausdrücklich wegen Context-Stabilität. Das Paket
bleibt ein substantieller vertikaler Produktbaustein, aber ein neuer Coder soll
es in einem stabilen Kontext beginnen und fertigstellen können. Gewählt wird
Kandidat A:

> Ein injizierter lokaler Repository-Cache-Generation-Writer bildet aus einem
> erfolgreichen bestehenden Clone-/Acquirer-Ergebnis einen credentialfreien
> Cache-Key, schreibt Manifest und vollständige Generation in isoliertes
> Staging und veröffentlicht den validierten Generation-Namen atomar als
> `current`. Der request-eigene Checkout bleibt Eigentum des bestehenden
> Acquirers und wird nicht in den persistenten Cachebesitz umgewandelt.

Dieser Vertrag liefert sofort einen persistenten, prüfbaren Source-Snapshot für
spätere Wiederverwendung. Er entscheidet jedoch noch nicht, wann ein Current-
Eintrag wiederverwendet oder aktualisiert wird.

## Herausgelöste Folgepakete

- **Cache-backed Initial Acquisition/Reuse:** validierten `current`-Pointer
  lesen, Manifest und Inhalt prüfen und daraus einen neuen, besitzbaren
  Request-Checkout ableiten. Der persistente Generation-Ordner darf niemals an
  `ExternalSourceCheckoutHandle` übergeben werden. Dieses Paket folgt auf die
  hier veröffentlichte Generation.
- **Refresh/Fetch:** eigener Transportvertrag für Fetch/Refresh des Default-
  Branches, Refresh-Intervall, neue Generation und die Semantik eines
  fehlgeschlagenen fälligen Refreshes. Der bestehende Transport bleibt in
  diesem Step ausschließlich bei `CloneDefaultBranchAsync`.
- **Cache-Konfiguration:** `CacheRoot`, Refresh-Policy und die Bindung an
  `appsettings.json` werden zusammen mit dem späteren Reuse-/Refresh-Vertrag
  entschieden. Step 026 verwendet nur einen injizierten Cache-Root mit einem
  deterministischen Default; es verändert keinen öffentlichen Konfigurations-
  oder Credential-Vertrag.
- **Dirty/unbuilt, Health und degraded-Fallback:** eigener Source-Policy-
  Schnitt nach der Cache-Reuse-/Refresh-Semantik.
- **Host-/MCP-Wiring, Capability-Matrix, transitive Referenzen, Retention,
  Garbage Collection, explizite Invalidierung und Telemetrie:** bleiben
  nachgelagerte Pakete; EPIC-05 wird nicht vorgezogen.

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und
  Fehlersemantik.
- **Voraussetzung:** `step-024` ist nach der genehmigten Korrektur in
  `step-025` abgeschlossen. Die Snapshot-/Workspace-Ownership und das
  exception-sichere Registry-Cleanup werden nicht erneut geöffnet.
- **Konzept-Referenz:** `Konzept.md`, Abschnitte „Gitea als gemeinsame
  Wahrheit“, „Fingerprint und Cache-Key“, „Staleness und atomarer
  Session-Wechsel“ sowie die Cache-/Source-of-Truth-Leitplanken.

## Tatsächlicher Projektzustand (JIT-Kontext)

Die MCP-Abfragen wurden mit dem absoluten `projectRoot`
`C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt.

- `ExternalSourceRepositoryAcquirer` umfasst 469 semantisch erfasste Zeilen;
  `AcquireAsync` hat 30 Zeilen und 33 transitive Aufrufstellen in sieben
  Dateien. Der Acquirer reserviert einen neuen Checkout, ruft den Transport
  auf, prüft die geladene Revision und gibt einen
  `ExternalSourceCheckoutHandle` zurück.
- `IGiteaRepositoryTransport` hat aktuell genau einen Vertrag:
  `CloneDefaultBranchAsync`. `GiteaGitRepositoryTransport.CloneDefaultBranchAsync`
  umfasst 58 Zeilen; der Transport-Typ hat 22 direkte/transitive relevante
  Dateien und 116 Treffer. Ein Fetch-/Refresh-Port wäre daher ein neuer
  schwergewichtiger Vertrag mit Prozess-, Credential- und Fehlerkopplung.
- `ExternalSourceRepositoryAcquisitionResult` kann nur einen
  `ExternalSourceCheckoutHandle` als erfolgreichen Besitz ausgeben. Der
  bestehende Handle räumt seinen request-eigenen Checkout. Ein persistenter
  Generation-Pfad darf deshalb nicht als dieser Handle weitergereicht werden.
- `ExternalSourceRepositoryCheckoutReservation` (156 Zeilen) und
  `ExternalSourceRepositoryPathGuard` (247 Zeilen) enthalten bereits die
  Marker-, Root-, Reparse- und Cleanup-Gates. Der neue Writer verwendet diese
  Verträge und dupliziert keinen Besitz- oder Pfad-Guard.
- `GiteaExternalSourceProvider` konsumiert das Acquirer-Ergebnis; der
  Snapshot-Materializer erwartet weiterhin einen besitzbaren Checkout. Der
  Provider-/Snapshot-Vertrag muss für diesen Step unverändert bleiben.
- `ExternalSourceConfiguration` hat 71 semantische Treffer in sieben
  Dateien; `ExternalSourceConfigurationLoader` wird aus zehn Dateien mit
  37 Aufrufstellen erreicht. Eine Erweiterung um CacheRoot und RefreshPolicy
  würde damit einen eigenen Konfigurationsvertrag bilden und bleibt bewusst
  draußen.
- `AssemblyDecompilationCache` ist mit 488 Zeilen, `Publish` (38 Zeilen),
  `TryRead` (26 Zeilen) und `TryPublishPointer` (21 Zeilen) ein brauchbares
  Verhaltensreferenzmuster. `AssemblyCacheContract` und der Assembly-Cache
  werden nicht erweitert und nicht als Repository-Cache-Contract verwendet;
  Assembly- und Repository-Identität bleiben getrennt.

Diese Befunde bestätigen, dass der frühere Gesamtblock zu breit war. Ein
Fetch-/Refresh-Port würde gleichzeitig Transport, Acquirer, Credential-/Git-
Prozesssemantik und Generation-Publish ändern. Ein Reuse-Paket würde zusätzlich
Copy-/Lease-/Checkout-Ownership benötigen. Der hier gewählte Write-through-
Schnitt kann dagegen unterhalb des Host-/MCP-Wirings an den vorhandenen
Erfolgsweg angeschlossen werden.

## Intention

Nach diesem Step erzeugt ein erfolgreicher bestehender Repository-Clone eine
persistente, revisionsgebundene Cache-Generation. Manifest, Datei-Inventar,
Cache-Key und `current`-Pointer werden in isoliertem Staging aufgebaut und
erst nach einem Read-back der Integrität veröffentlicht.

Der Acquirer behält seinen bisherigen request-eigenen Checkout und alle
Transport-, Cancellation-, Credential-, Prozess-, 1314- und Reparse-
Semantiken. Ein Cache-Schreibfehler darf den bereits verfügbaren Checkout nicht
entziehen; er bleibt als typisierte Cache-Warnung sichtbar und lässt den alten
Current-Eintrag unverändert.

## Scope

### Schicht 1: Cache-Key und Manifestmodell

- Führe einen internen `ExternalSourceRepositoryCacheKey` ein, der aus der
  bereits normalisierten Repository-URL, dem sicheren repository-relativen
  Solution-Pfad und einer eigenen Cache-Schema-Version gebildet wird.
- Der physische Entry-Pfad verwendet ausschließlich einen deterministischen
  sicheren Hash-/Segmentwert. Credentials, URL-Userinfo und ungeprüfte URL-
  oder Solution-Pfadsegmente dürfen nicht in den Cache-Pfad gelangen.
- Führe ein internes
  `ExternalSourceRepositoryCacheManifest` mit Cache-Key, kanonischer URL,
  Solution-Pfad, tatsächlich geladener Revision, Generation-Name,
  Erstellungszeitpunkt und vollständigem relativen Datei-Inventar aus
  Dateipfad, Länge und Inhaltshash ein.
- Das Manifest erhält nur den Status eines vollständig geschriebenen und
  validierten Clones. Refresh-Zeitfenster, `nextRefresh`, degraded/Health und
  alte Generationen als Reuse-Entscheidung gehören nicht in dieses Modell.

### Schicht 2: Lokaler Generation-Writer und atomarer `current`

- Führe genau einen injizierbaren Port
  `IExternalSourceRepositoryCacheWriter` mit einem typisierten Publish-
  Request/Result ein. Der Request stammt aus einem validierten Acquirer-
  Ergebnis und referenziert nicht-besitzübernehmend den vorhandenen
  `ExternalSourceCheckoutHandle` sowie Mapping, Solution-Pfad und geladene
  Revision.
- Der konkrete Writer normalisiert einen injizierten Cache-Root, reserviert
  unter dem Entry-Pfad eine neue `generation-*`-Stagingdirectory, prüft den
  besitzten Source-Checkout und kopiert dessen Repository-Inhalt ohne den
  request-eigenen Ownership-Marker. Reparse-Punkte oder unsichere Pfade führen
  fail-closed zum typisierten Publish-Fehler.
- Der Writer schreibt zuerst Inhalt und Manifest, liest die Generation wieder
  ein und verifiziert Cache-Key, Revision, Solution-Pfad, Datei-Inventar und
  kontrollierte Pfade. Der `current`-Pointer enthält nur einen sicheren
  Generation-Namen; Pointer und Manifest werden gegenseitig geprüft.
- Der Pointer wird über temporäre Datei und atomare Replace-/Move-Semantik
  veröffentlicht. Bis zum erfolgreichen Read-back bleibt ein vorheriger
  Current-Eintrag unverändert. Bei Fehler oder Cancellation werden nur die
  unpublizierte Generation und temporäre Pointer-Dateien bereinigt.
- Gleichzeitige Publishes werden über eine lokale, pro Cache-Key begrenzte
  Synchronisationsgrenze serialisiert. Es gibt keinen globalen Host-/Registry-
  Lock und keinen neuen Cross-Process-Lifetime-Vertrag.

### Schicht 3: Write-through-Anschluss am Acquirer

- Erweitere `ExternalSourceRepositoryAcquirer` um den injizierbaren Writer,
  mit einem deterministischen lokalen Default unter
  `AppContext.BaseDirectory/cache/source`, ohne `appsettings.json` oder
  Credential-Konfiguration zu verändern.
- Rufe den Writer nur nach erfolgreicher Transport- und Checkout-Validierung
  auf, bevor der erfolgreiche Acquirer-Result endgültig zurückgegeben wird.
  Der Writer erhält keinen Besitzübergang: der Acquirer-Handle bleibt für den
  Snapshot zuständig, die Generation bleibt cache-eigen.
- Ein erfolgreicher Publish ergänzt höchstens die bestehenden Diagnosen.
  Ein Publish-Fehler lässt den gültigen request-eigenen Acquirer-Erfolg
  bestehen, macht die Cache-Warnung sichtbar und löscht weder Checkout noch
  einen zuvor veröffentlichten Current-Eintrag.
- `IGiteaRepositoryTransport`, `GiteaGitRepositoryTransport`,
  `GiteaExternalSourceProvider`, `ExternalSourceSnapshotMaterializer`,
  `SourceSnapshotRegistry` und das Host-Wiring bleiben fachlich unverändert.

## Out-of-Scope

- Kein Lesen oder Wiederverwenden eines bestehenden Current-Eintrags als
  Acquisition-Erfolg; das ist das nachgelagerte Cache-Reuse-Paket.
- Kein `Fetch`, kein Refresh-Intervall, kein Default-Branch-Refresh, keine
  neue Transportmethode und keine Entscheidung „stale versus current“.
- Keine Erweiterung von `ExternalSourceConfiguration`,
  `ExternalSourceConfigurationLoader`, `appsettings.json`,
  `Docs/configuration.md` oder Credential-Schemata.
- Keine Änderung an `ExternalSourceCheckoutHandle`,
  `ExternalSourceRepositoryCheckoutReservation`, `ExternalSourceSnapshot`,
  `SourceSnapshotRegistry` oder deren Cleanup-/Ownership-Semantik.
- Kein Host-/MCP-Wiring, keine Toolregistrierung, keine Capability-Matrix,
  keine transitive Referenzauflösung und keine EPIC-05-Arbeit.
- Keine Dirty-/unbuilt-Checkout-Policy, kein Health-/degraded-Fallback und
  keine neue Fallback-Entscheidung. Der bestehende typed Provider-/
  Decompilation-Fallback bleibt unangetastet.
- Keine Retention, Garbage Collection, manuelle Invalidierungs-API oder
  Telemetrie.
- Kein Assembly-Cache-Umbau und keine gemeinsame Cache-Basisklasse nur wegen
  ähnlicher Pointer-/Manifest-Form.
- Keine echten Remote-/Gitea-/Git-Prozesse in Tests, keine Credentials,
  keine Netzwerkzugriffe, keine unbounded Retries oder Sleeps.
- Keine `Assembly.Load`, kein `AssemblyLoadContext` und keine Reflection-
  Ausführung.
- Kein allgemeiner DRY-, Magic-Values- oder Dead-Code-Sweep. `TD-001` bis
  `TD-003` bleiben unangetastet.

## Abnahmekriterien

1. **Credentialfreier Key:** Gleiche normalisierte URL, gleicher Solution-
   Pfad und gleiche Schema-Version erzeugen denselben Key; Credentials,
   Userinfo und ungeprüfte Pfadsegmente erscheinen weder im Key noch im
   physischen Cache-Pfad.
2. **Vollständige Generation:** Ein gültiger erfolgreicher Clone wird in
   isoliertem Staging als neue Generation mit vorhandenem Solution-Pfad,
   geladenem Revisionstext und vollständigem Datei-Inventar materialisiert;
   der Ownership-Marker des Request-Checkouts wird nicht persistent kopiert.
3. **Manifest-Integrität:** Der Read-back verwirft Generationen bei falschem
   Key, falscher Revision, falschem Solution-Pfad, fehlenden/zusätzlichen
   Dateien, Hash-/Längenabweichung oder unsicheren/reparse-betroffenen
   Pfaden.
4. **Atomarer Pointer:** `current` verweist erst nach vollständigem Write und
   erfolgreicher Validierung auf die neue Generation. Ein Fehler, eine
   Cancellation oder ein Prozessabbruch hinterlässt keinen halben Pointer;
   ein vorheriger gültiger Current-Eintrag bleibt erhalten.
5. **Lokale Konkurrenz:** Zwei gleichzeitige Publishes desselben Cache-Keys
   erzeugen keine Pointer-/Manifest-Kreuzung und keinen sichtbaren halben
   Current-Eintrag; die Synchronisation bleibt auf diesen Cache-Key begrenzt.
6. **Ownership-Anschluss:** Der Acquirer ruft den Writer nur für ein gültiges
   erfolgreiches Transport-Ergebnis auf. Der Snapshot-Checkout bleibt
   request-eigen und entsorgbar; der persistente Generation-Pfad wird weder
   vom Checkout-Handle noch vom Registry-Cleanup gelöscht.
7. **Fehlerkompatibilität:** Bestehende Clone-, Revision-, Cancellation-,
   Credential-, HTTP-/Git-, Prozessbaum-, Handle-, Native-, 1314- und
   Reparse-Semantik bleibt erhalten. Ein Cache-Publish-Fehler ist sichtbar,
   entzieht aber keinen ansonsten gültigen Acquirer-Erfolg.
8. **Deterministische Verifikation:** Die neuen Writer-/Acquirer-Tests
   decken Key, vollständige Generation, Manifestfehler, Pointerfehler,
   erfolglosen Publish, parallele Publishes, Ownership und Cleanup ab und
   verwenden ausschließlich lokale TestTempDirectory-Fixtures sowie
   injizierte Doubles.

## Kontextbudget und Handoff

```yaml
context_budget:
  max_initial_files: 12
  max_read_first_files: 10
  read_first:
    - tasks/decompiled-assembly-analysis/step-025/step-result.md
    - tasks/decompiled-assembly-analysis/step-025/step-review.md
    - tasks/decompiled-assembly-analysis/codemap.md
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs
    - src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutReservation.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTestTransport.cs
  read_on_demand:
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs
    - src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs
    - src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCancellationTests.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs
    - src/AiNetLinter/Mcp/Assemblies/AssemblyDecompilationCache.cs
    - src/AiNetLinter/Mcp/Assemblies/AssemblyCacheContract.cs
    - src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs
    - src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs
  out_of_scope:
    - src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs
    - src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs
    - src/AiNetLinter/Mcp/Registration/
    - src/AiNetLinter/Mcp/Daemon/
    - src/AiNetLinter/Mcp/Tools/
    - EPIC-05 und transitive Referenzauflösung
    - Retention, GC, Health, Dirty/unbuilt und externe Prozess-/Native-Helfer
```

Der Coder liest zuerst dieses Handoff, den Step-Plan, Step-025-Result/Review,
die CodeMap und die zehn `read_first`-Dateien. Die vollständige Solution wird
nicht pauschal geladen. Neue Dateien sind ausschließlich der Cache-Contract,
Cache-Modelle, konkrete Generation-Writer und die zugehörigen lokalen Tests;
die exakten Dateinamen dürfen nur innerhalb dieses Vertrags gewählt werden.

Sicherer Einstiegspunkt:

1. `get_feature_context` für
   `ExternalSourceRepositoryAcquirer.AcquireAsync`,
   `CompleteTransportResult` und `ExternalSourceRepositoryAcquisitionResult.Success`
   ausführen; anschließend `get_symbol_body` für die relevanten Methoden.
2. `get_impact`/`find_references` für Acquirer, Constructor und
   `IGiteaRepositoryTransport.CloneDefaultBranchAsync` sowie
   `dependency_graph` für Acquirer und neue Cache-Modelle verwenden. Vorhandene
   `PathGuard`-/Reservation-Methoden nur wiederverwenden.
3. Den Cache-Contract und den Writer mit injiziertem Root/Writer anlegen; dann
   den Publish-Hook ausschließlich hinter erfolgreicher Checkout-Validierung
   einfügen. Keine neue Transportmethode und kein Host-Wiring anlegen.
4. Erst danach die Writer-Tests und die gezielte Acquirer-Regression ergänzen.
   Bei Context-Compact vor dem Result-Bericht den laufenden Coder schließen und
   einen neuen Coder mit genau diesem Plan/Handoff und dem aktuellen Diff
   starten; keinen bestehenden Sub-Agenten wiederverwenden.

Invarianten für die Implementierung:

- Persistent Cache-Generation und request-eigener Checkout sind zwei
  verschiedene Owner. Der Cache darf keinen Snapshot-/Registry-Cleanup-Pfad
  besitzen.
- `current` ist ein sicherer Pointer auf eine bereits validierte Generation,
  nie der Transport-/Refresh-Status und nie eine Cache-Reuse-Entscheidung.
- Der bestehende Transport liefert weiterhin den tatsächlichen Revisionstext;
  der Writer darf keine Revision aus URL, Branchname oder Dateiname erraten.
- Cache-Fehler sind sichtbar, aber fail-open gegenüber einem bereits gültigen
  request-eigenen Acquirer-Erfolg; kein alter Current-Eintrag wird als frisch
  aktualisiert behauptet.

## Tests

Der Planer führt keine Tests aus. Der Coder ergänzt bzw. erweitert nur lokale
FastTests mit `TestTempDirectory` und deterministischen Doubles:

- `ExternalSourceRepositoryCacheWriterTests`: Key-Normalisierung ohne
  Credentials, sichere Entry-/Generation-Pfade, vollständige Kopie ohne
  Ownership-Marker, Manifest-/Datei-Inventar und validierter Pointer.
- Fehlerregressionen für fehlende Solution, unsicheren/reparse-betroffenen
  Inhalt, beschädigtes Manifest, falschen Key, falsche Revision, unvollständige
  Generation sowie fehlgeschlagenes Pointer-Publish; der alte Current-Eintrag
  und das Staging-Cleanup werden geprüft.
- Eine bounded lokale Parallel-Regression für zwei Publishes desselben Keys
  prüft Pointer-/Manifest-Konsistenz, ohne Stress-Kategorie, Sleep oder
  Netzwerk.
- `ExternalSourceRepositoryAcquirerTests`: Writer wird nach gültigem
  Clone-Ergebnis aufgerufen, erhält Revision und eine nicht-besitzübernehmende
  Referenz auf den `ExternalSourceCheckoutHandle`; Writer-Fehler bleiben als
  Diagnose sichtbar und der request-eigene Handle bleibt erfolgreich
  entsorgbar.
- Bestehende Acquirer-, Cancellation-, Provider- und Snapshot-Tests bleiben
  grün; Registry-/Snapshot-Lifetime aus Step 025 wird nicht neu implementiert.

Abschlussbefehle des Coders:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Stress wird nicht automatisch ausgeführt. Neue Lasttests gehören nur bei
echter absichtlicher Lastprüfung in `Category=Stress`; der bounded Zwei-Publish-
Test ist kein solcher Test.

## MCP-, DRY-, MagicValues- und DeadCode-Disposition

### MCP

- Vor Edits: `get_feature_context`/`get_symbol_body` für Acquirer, Result-
  Factory, Checkout-Ownership und PathGuard; `get_impact`/`find_references`
  für Acquirer und Clone-Port; `dependency_graph` für die tatsächliche
  Anschlussgrenze. Jeder Aufruf nutzt den absoluten
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`.
- Nach Edits: `get_impact` und `get_violations` für den Writer, Acquirer-
  Hook und Testconsumer; `safeguard` nur als gezieltes Assemblies-Scope-Gate.
  Keine semantische C#-Frage wird durch `rg` ersetzt.

### DRY / Drift

Der ausgeführte solutionweite `find_duplicates`-Scan mit `scopeDir=src` und
`minTokens=20` fand ein Exact-Cluster außerhalb dieses Steps:
`FindAssemblyExtensionsTool.ExecuteAsync` und
`InspectAssemblyTool.ExecuteAsync`. Der Assemblies-Produktionsscope allein
hat mit 270 gescannten Methoden null Clone-Cluster. Der strukturelle Scan
meldete 16 Kandidaten; die relevanten Assembly-Kandidaten
`AssemblyAnalysisSession.DetermineStatus`/`ResolveManifestStatus` (Score
0,8046) und `GiteaGitRepositoryTransport.Failure`/
`ExternalSourceRepositoryTransportResult.Success` (Score 0,9080) haben
unterschiedliche Verträge und werden nicht mechanisch zusammengelegt.

Disposition: Kein globaler Sweep und keine Änderung an den außerhalb des
Scopes liegenden Tool-Klonen. Der neue Writer darf den Assembly-Cache nicht
kopieren, sondern bildet einen eigenen Repository-Contract, weil Manifest-
Identität und Ownership verschieden sind. Die beiden außerhalb liegenden
Exact-/Structural-Befunde bleiben als explizite Folgeprüfung dokumentiert;
`tech-debt.md` wird gemäß dem erlaubten-Dateien-Gate in diesem Planungsauftrag
nicht geändert.

Nach der Implementierung führt der Coder den scoped Exact-/Near- und den
relevanten Structural-Scan für den geänderten Repository-/Testbereich erneut
aus. Neue echte Duplikate werden im selben Vertrag konsolidiert; ein Befund,
der ein anderes Epic oder eine neue Vertragsentscheidung erfordert, wird nur
als Tech-Debt gemeldet, nicht in einen Sweep gezogen.

### Magic Values

Der aktuelle Assemblies-Magic-Value-Scan zeigt 99 bestehende Vorkommen, davon
61 Constant-Kandidaten, aber keinen Cache-spezifischen Config-Kandidaten.
Neue Cache-Schema-, Pointer-, Manifest-, Generation- und Statuswerte werden
ausschließlich in einem eigenen `ExternalSourceRepositoryCacheContract`
zentralisiert. Keine verstreuten Dateinamen, Hash-/Encodingwerte oder
unbenannten Retry-/Timeout-Literale; bestehende Git-Prozess- und
Lokalisierungstexte bleiben außerhalb.

### Dead Code

Der aktuelle Assemblies-Scope meldet 36 Low-Confidence-Kandidaten und null
High-Confidence-Kandidaten. Der Coder lässt keine Alternativports oder
unreferenzierten Cache-Factories liegen und stellt sicher, dass Writer,
Models und Contract durch Acquirer bzw. Tests referenziert sind. Danach wird
`find_dead_code` mit hoher Konfidenz auf dem geänderten Production-/Testscope
ausgeführt; bestehende Low-Confidence-Funde werden nicht heuristisch entfernt.

## Risiken und Gegenmaßnahmen

- **Ownership-Verwechslung:** Der Writer kopiert aus dem validierten
  request-eigenen Checkout, übernimmt ihn aber nicht. Tests entsorgen den
  Handle und prüfen, dass die veröffentlichte Generation bestehen bleibt.
- **Halber Current:** Manifest und Inhalt werden vor Pointer-Publish gelesen
  und geprüft; temporäre Pointer werden bei Fehlern entfernt, der alte Pointer
  bleibt unverändert.
- **Unsichere Kopie/Reparse:** Bestehende PathGuard-/Ownership-Methoden werden
  wiederverwendet; Reparse oder Pfadausbruch führt zu keinem Publish.
- **Stale-as-current:** Dieser Step führt kein Refresh aus und setzt keine
  Refresh-Zeit. Er veröffentlicht nur die tatsächlich vom bestehenden
  Transport gemeldete Revision; Staleness entscheidet erst das Folgepaket.
- **Cache-Schreibfehler:** Der gültige Acquirer-Erfolg bleibt nutzbar, die
  Diagnose bleibt sichtbar und kein alter Current-Eintrag wird überschrieben.
- **Konfigurationsdrift:** Es gibt in diesem Step bewusst keine öffentliche
  Cache-Konfiguration. Der Default/Injection-Punkt wird im späteren
  Cache-Konfigurationsvertrag ersetzt oder bestätigt.
- **Assembly-Cache-Drift:** `AssemblyDecompilationCache` dient nur als
  on-demand Referenz; keine gemeinsame Basisklasse und kein gemeinsames
  Manifestmodell werden eingeführt.

## Definition of Done

- Die acht Abnahmekriterien sind durch Produktionscode und lokale Tests
  nachgewiesen.
- Der Scope enthält genau einen primären Write-through-Cache-Publish-
  Vertrag mit drei unmittelbar gekoppelten Schichten; Reuse und Refresh/
  Fetch sind nicht enthalten.
- Acquirer-, Transport-, Credential-, Cancellation-, Prozessbaum-,
  1314-/Reparse- sowie Snapshot-/Registry-Ownership-Invarianten bleiben
  erhalten.
- `get_impact`, `get_violations`, scoped `find_duplicates`,
  `find_magic_values` und `find_dead_code` sind für den geänderten Scope
  dokumentiert; es gibt keinen unbegründeten globalen Sweep.
- `dotnet build` sowie beide projektweiten `Category!=Stress`-Testläufe sind
  grün; Stress wird nicht automatisch ausgeführt.
- Keine Änderung an Host-/MCP-Wiring, Configuration-Loader,
  `IGiteaRepositoryTransport`, Assembly-Cache, Snapshot-Registry oder
  EPIC-05.
- Der Coder schreibt `step-026/step-result.md`, setzt den Planstatus erst
  nach seiner Implementierung auf `done (pending audit)`, erstellt seinen
  deutschen Conventional Commit mit Suffix
  `[decompiled-assembly-analysis]` und pusht nicht.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — nullable C#, kurze Methoden, kleine
  Kopplung, keine dynamische Ausführung und zentrale Konstanten.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2` — keine
  AssemblyLoadContext-/Reflection-Ausführung und keine repo-spezifischen
  Produktionshardcodings; der Default-Cachepfad ist EXE-relativ und kein
  Repository-Pfad.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — xUnit-v3-Tests,
  `TestTempDirectory`, keine Ad-hoc-Tempverzeichnisse und finale
  Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Zero-Warning-, DRY-,
  Magic-Values- und Dead-Code-Disposition.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — semantische MCP-Abfragen mit
  absolutem `projectRoot` vor C#-Änderungen; `rg` bleibt Textsuche.
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md` — Split-Gate
  für Vertrag, Schichten, Kriterien und initialen Leseumfang.

## Bekannte Ausnahmen

- Keine Testausnahme ist geplant. Ein echter Reparse-/Symlink-Fall darf nur
  nach der bestehenden Step-017/018-Regel repository-spezifisch wegen
  Win32-1314 transparent behandelt werden; dieser Step erzeugt keine neue
  Capability-Ausnahme und simuliert keine Berechtigung.

## Notes

- Diese Datei ist die revidierte Step-026-Planung und kein Korrektur-Step.
  Step 025 bleibt mit Review `acdfe70e` abgeschlossen.
- Die Überarbeitung ist ein Context-Stabilitäts-Split, kein Verkleinern zu
  einem Mini-Sweep. Der Writer veröffentlicht einen echten persistenten
  Generation-Stand aus dem produktiven Clone-/Acquirer-Erfolg.
- Keine Produktionsänderung, kein Testlauf und keine Coder-/Kritikerarbeit
  wurde durch diese Planung ausgeführt.
