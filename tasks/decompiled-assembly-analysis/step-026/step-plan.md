---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 026
corrects: null
title: "Persistenten Repository-Cache mit Refresh/Fetch und atomarer Veröffentlichung"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T15:14:17+02:00
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

# Step 026: Persistenter Repository-Cache und atomare Veröffentlichung

## Bezug und Split-Gate

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und
  Fehlersemantik.
- **Voraussetzung:** `step-024` ist nach der Korrektur in `step-025`
  genehmigt. Acquirer→Snapshot-/Workspace-Ownership und Registry-/
  Multi-Owner-Cleanup werden nicht erneut geöffnet.
- **Konzept-Referenz:** `Konzept.md`, insbesondere die Abschnitte zu
  Source-of-Truth, Cache-Identität, Refresh, Manifest, Generationen sowie
  atomarem Session-/Quellenwechsel.

Der eine primäre Vertrag für diesen Step lautet:

> Eine explizit gemappte externe Quelle wird über einen persistenten,
> manifest-validierten Repository-Cache akquiriert oder aktualisiert. Die
> Operation liefert nur einen an eine tatsächlich geladene Revision
> gebundenen, validierten und für den bestehenden Snapshot-Owner
> besitzbaren Checkout. Ein Cache-Refresh wird vollständig in einer neuen
> Generation vorbereitet und erst nach erfolgreicher Integritätsprüfung als
> aktuelle Source-of-Truth veröffentlicht.

Refresh/Fetch, Cache-/Manifest-Integrität, Generationen und atomare
Veröffentlichung gehören hier zusammen: Ein persistenter Cache ohne
Revisionsmanifest oder atomare Veröffentlichung könnte eine halbfertige
oder falsch als aktuell markierte Quelle ausliefern. Die atomare
Veröffentlichung ist dabei eine Sicherheits- und Konsistenzgarantie des
primären Akquisitionsvertrags, kein zweiter unabhängiger Featureblock.

Das Split-Gate ist erfüllt:

- **Ein primärer Vertrag:** cache-gestützte, revisionsgebundene
  `AcquireOrRefresh`-Akquisition.
- **Drei gekoppelte Schichten:** Cache-Identität/Manifest, Refresh-/Fetch-
  Port mit Acquirer-Orchestrierung sowie Generation-/Pointer-
  Veröffentlichung mit besitzgebundenem Request-Checkout.
- **Acht Abnahmekriterien:** genau acht Kriterien sind unten definiert.
- **Höchstens zwölf `read_first`-Dateien:** genau zwölf zentrale Dateien
  sind im Kontextbudget festgelegt.
- **Kein Mini-Sweep und kein monolithischer Block:** Host-Wiring,
  Capability-Matrix, transitive Referenzen, Dirty-/Health-Policy und
  EPIC-05 bleiben eigenständige Folgepakete.

## Tatsächlicher Projektzustand (JIT-Kontext)

Der MCP-Kontext mit absolutem `projectRoot`
`C:/Daten/Entwicklung/Ralf/AiNetLinter` ergibt:

- `ExternalSourceRepositoryAcquirer.AcquireAsync` reserviert derzeit bei
  jedem Aufruf eine neue kontrollierte Staging-Wurzel und ruft den
  injizierten Transport auf. Ein persistenter Repository-Cache oder eine
  Refresh-Entscheidung ist dort nicht vorhanden.
- `IGiteaRepositoryTransport` besitzt aktuell nur
  `CloneDefaultBranchAsync`. `GiteaGitRepositoryTransport` kapselt bereits
  URL-Normalisierung, Credential-Lifetime, Child-Process-Ausführung,
  HEAD-Ermittlung und typisierte Fehler. Der neue Refresh-/Fetch-Port muss
  diese Invarianten wiederverwenden und darf keinen unkontrollierten
  In-Place-Refresh des veröffentlichten Cache-Eintrags einführen.
- `ExternalSourceRepositoryAcquisitionResult` und der bestehende
  `ExternalSourceRepositoryCheckoutReservation` bilden die geeignete
  Übergabe an den Snapshot-Pfad. Ein Snapshot darf keinen persistenten
  Cache-Ordner als löschbaren Einzelbesitz erhalten. Entweder wird ein
  kontrollierter Request-Checkout aus der veröffentlichten Generation
  abgeleitet oder es wird ein gleichwertiger, explizit freizugebender Lease-
  Owner eingeführt; die bestehende Snapshot-/Workspace-Ownership-Grenze
  bleibt dabei unverändert.
- `ExternalSourceConfiguration` und
  `ExternalSourceConfigurationLoader` kennen derzeit die Mappings, aber
  keine Cache-Wurzel und keine Refresh-Policy. Eine Konfigurationserweiterung
  ist Teil des Cache-Vertrags und muss strikt validiert sowie dokumentiert
  werden; keine Credential-Konfiguration wird neu erfunden.
- `AssemblyDecompilationCache` besitzt bereits ein getrenntes
  `current.json`-/Generation-/Manifest-Muster mit atomarem Pointer und
  beschädigungsresistenter Veröffentlichung. Dieses Muster dient als
  Referenz. Der Assembly-Cache wird weder als Repository-Cache missbraucht
  noch in diesem Step erweitert.
- `SourceSnapshotRegistry.Dispose()` und die Multi-Owner-Fehleraggregation
  sind durch `step-025` genehmigt. Der neue Cache muss deren Eigentumsgrenze
  bedienen, nicht Registry-Cleanup oder Snapshot-Dispose verändern.

Die semantische Impact-Prüfung zeigt die Acquirer-Abhängigkeiten in
`GiteaExternalSourceProvider`, den Acquirer-Tests sowie den bestehenden
Path-Guard-, Failure-Policy- und Transport-Verträgen. Der Assemblies-Bereich
hat keinen exakten Produktions-Duplikatcluster und keinen hochsicheren
Dead-Code-Fund. Der einzige Magic-Values-Fund ist die bereits bestehende,
zweifach vorkommende User-Exception-Nachricht in
`ExternalSourceGitProcessLauncher`; sie ist nicht Teil dieses Cache-Vertrags.

## Intention

Step 026 schließt die bisher offene Lücke zwischen dem produktiven
Default-Branch-Clone und einer wiederverwendbaren Source-of-Truth:

1. Eine Cache-Identität wird aus kanonischer Repository-URL, Solution-Pfad
   und Cache-Schema gebildet; die geladene Revision bleibt ein Ergebnis und
   wird nicht aus der Konfiguration geraten.
2. Ein gültiger, nicht abgelaufener Current-Eintrag wird ohne Transport-
   aufruf wiederverwendet. Bei fehlendem, abgelaufenem oder ungültigem
   Eintrag wird Clone bzw. Fetch/Refresh in einer isolierten Zielgeneration
   ausgeführt.
3. Manifest, Solution-Pfad, geladene Revision und kontrollierte Pfade
   werden vor der Veröffentlichung geprüft. Erst danach wird der Current-
   Pointer atomar gewechselt; der bestehende Snapshot erhält weiterhin nur
   einen request-eigenen, freigebbaren Checkout.

## Scope

### Schicht 1: Cache-Identität, Konfiguration und Manifest

- Einen internen, strikt validierten Cache-Vertrag für Root, Refresh-
  Intervall, Cache-Schema, kanonische URL, Solution-Pfad und Status
  definieren. Cache-Schlüssel enthalten keine Credentials und dürfen keine
  beliebigen Pfadsegmente aus der URL ungeprüft übernehmen.
- Die Konfiguration so erweitern, dass Cache-Root und Refresh-Policy
  deterministisch aus dem bestehenden `ExternalSources`-Bereich kommen.
  Relative Pfade werden kontrolliert aufgelöst; ungültige oder unsichere
  Werte werden als bestehende typed Configuration-/Provider-Diagnose
  sichtbar, nicht stillschweigend korrigiert.
- Pro Cache-Key ein Manifest mit kanonischer URL, geladener Revision,
  Solution-Pfad, Cache-Schema, Erstellungs-/Refresh-Zeitpunkt und
  Integritätsstatus führen. Ein Manifest allein ohne validierten Inhalt ist
  nicht ausreichend.
- Generationen unter einer kontrollierten Cache-Wurzel getrennt halten.
  Beschädigte, unvollständige, fremde oder nicht zum Pointer passende
  Generationen werden nicht als aktuell verwendet.

### Schicht 2: Refresh-/Fetch-Port und Acquirer-Orchestrierung

- Den bestehenden injizierten Git-Transport um einen klaren
  Refresh-/Fetch-Vorgang ergänzen oder einen gleichwertigen, typisierten
  Port einführen. Der Port arbeitet ausschließlich auf einer isolierten
  Staging-/Zielgeneration; `current` wird nie direkt verändert.
- Beim Cache-Miss initial klonen, bei gültigem und abgelaufenem Eintrag
  aktualisieren. Die Entscheidung ist deterministisch und begrenzt:
  kein unbounded retry, kein Sleep und kein stilles Wiederverwenden eines
  fehlgeschlagenen Refreshs als aktuelle Quelle.
- Die bestehende Credential-Auflösung, Git-Argument-/Prozessbaum-
  Semantik, Cancellation, HTTP-/Git-Fehlerklassifikation und Cleanup-
  Garantien wiederverwenden. 1314-/Reparse-Erkennung bleibt
  repository-spezifisch und führt weiterhin typed zum vorhandenen
  Decompilation-Fallback.
- Aus einer veröffentlichten Generation einen kontrollierten
  Request-Checkout für `ExternalSourceRepositoryAcquisitionResult`
  ableiten oder einen Lease-Owner mit exakt gleicher Snapshot-Lifetime
  einsetzen. Der persistente Cache-Eintrag bleibt cache-eigen und wird
  nicht vom Snapshot gelöscht.

### Schicht 3: Integritätsprüfung und atomare Source-of-Truth

- Neue Generationen ausschließlich vollständig und validiert veröffentlichen:
  Inhalt, Manifest, Revision, Solution-Pfad, Cache-Key und kontrollierte
  Pfade müssen zusammenpassen.
- Den atomaren Current-Pointer nach dem vorhandenen, getrennten
  Generationen-Muster modellieren. Ein Prozessabbruch, Cancellation,
  Transportfehler oder Integritätsfehler darf keinen halben Eintrag als
  current sichtbar machen.
- Bei Refresh-Fehlern bleibt eine zuvor veröffentlichte Generation als
  nicht aktualisierte Historie/Diagnose erhalten, wird aber nicht als
  erfolgreich aktualisierte aktuelle Revision behauptet. Der Aufrufer
  erhält den typed Fehler bzw. den bestehenden Decompilation-Fallback.
- Gleichzeitige Zugriffe pro Cache-Key dürfen keinen konkurrierenden
  Pointer-Wechsel oder doppelte, unberechenbare Veröffentlichung erzeugen.
  Die Synchronisationsgrenze bleibt lokal beim Cache und wird nicht zu
  einer globalen Host-/Registry-Lifetime umgebaut.

## Out-of-Scope

- Keine Änderung an `SourceSnapshotRegistry`,
  `ExternalSourceSnapshot.Dispose()` oder der durch Step 024/025
  genehmigten Workspace-/Checkout-Ownership- und Cleanup-Semantik.
- Keine Host-Komposition, MCP-Tool-Wiring, Capability-Matrix oder
  transitive Referenzauflösung; keine EPIC-05-Ausweitung. Eine bestehende
  Provider-Schnittstelle darf nur so weit berührt werden, wie sie den
  neuen cache-gestützten Acquirer-Vertrag zwingend konsumieren muss.
- Keine Erweiterung des getrennten Assembly-Decompilation-Caches und keine
  Vermischung von Assembly-Fingerprint, Decompiler-Generation und
  Repository-Generation.
- Keine Dirty-/unbuilt-Checkout-Policy, keine neue Health-/degraded-
  Strategie und kein lokaler Checkout als konkurrierende Source-of-Truth.
  Der bereits vorhandene typed Fallback bei nicht verfügbarer externer
  Quelle bleibt lediglich erhalten.
- Keine tatsächlichen Remote-, Gitea- oder Git-Netzwerkzugriffe in Tests.
  Test-Doubles und lokale Fixtures dürfen keine echten Clone-/Fetch-
  Prozesse starten, keine Credentials benötigen und keine unbounded
  Retries/Sleeps verwenden.
- Keine Assembly.Load-, AssemblyLoadContext- oder Reflection-Ausführung;
  die Decompilation bleibt statisch.
- Kein allgemeiner Dry-/MagicValues-/DeadCode-Sweep. `TD-001` bis `TD-003`
  bleiben unangetastet, solange die Cache-Implementierung keinen direkten
  Bezug zu Drive-Path-Duplikation, Origin-Typisierung oder
  `AssemblyOrigin.Kind` herstellt.

## Architekturgrenze

Die Cache-Grenze liegt zwischen dem Gitea-Transport und der bestehenden
Acquirer→Snapshot-Übergabe:

```text
Mapping + Cache-Optionen
        |
        v
Cache-Identity / Manifest / Generation-Pointer
        |
        +-- miss/expired --> isoliertes Clone/Fetch-Staging
        |                         |
        |                         v
        |                 validieren und atomar publishen
        |
        +-- valid current --> kontrollierten Request-Checkout ableiten
                                  |
                                  v
                         bestehender Snapshot-/Workspace-Owner
```

Der Cache besitzt seine persistente Generation. Der Snapshot besitzt wie
bisher seinen Request-Checkout und Workspace. Kein Cache-Pfad wird als
globaler Registry-Besitz eingeführt. Der Source-of-Truth-Status ist immer
an Manifest, Pointer, Inhalt und tatsächlich geladene Revision gebunden.
Provider-/Host-Entscheidungen oberhalb dieser Grenze bleiben unverändert.

## Kontextbudget

### `read_first` (12 Dateien)

1. `tasks/decompiled-assembly-analysis/step-025/step-result.md`
2. `tasks/decompiled-assembly-analysis/step-025/step-review.md`
3. `tasks/decompiled-assembly-analysis/roadmap.md`
4. `tasks/decompiled-assembly-analysis/follow-up-strategy.md`
5. `tasks/decompiled-assembly-analysis/Konzept.md`
6. `tasks/decompiled-assembly-analysis/tech-debt.md`
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
9. `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs`
10. `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs`
11. `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
12. `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`

### `read_on_demand`

- `ExternalSourceRepositoryCheckoutReservation.cs`,
  `ExternalSourceRepositoryPathGuard.cs` und
  `ExternalSourceRepositoryFailurePolicy.cs` für kontrollierte Root-,
  Reparse-, 1314- und Cleanup-Gates.
- `GiteaExternalSourceProvider.cs`,
  `ExternalSourceSnapshotMaterializer.cs`, `SourceSnapshotModels.cs` und
  `SourceSnapshotRegistry.cs` nur zur Anschluss- und Ownership-Prüfung;
  die Registry selbst bleibt unverändert.
- `AssemblyDecompilationCache.cs` und `AssemblyCacheContract.cs` als
  Referenz für Pointer-/Manifest-/Generationen- und Retry-Semantik; keine
  direkte Wiederverwendung eines Assembly-Cache-Contracts.
- `ExternalSourceRepositoryAcquirerTests.cs`,
  `ExternalSourceRepositoryAcquirerTestTransport.cs`,
  `ExternalSourceRepositoryTestSupport.cs`,
  `ExternalSourceRepositoryCancellationTests.cs`,
  `GiteaGitRepositoryTransportTests.cs`,
  `GiteaExternalSourceProviderTests.cs` und
  `ExternalSourceProviderContractTests.cs` für vorhandene Doubles,
  Fixtures, Cancellation- und Fehlerassertions.
- `Docs/configuration.md`, `README.md` und die einschlägigen
  Appsettings-Fixtures, falls die gewählte Cache-Option öffentlich
  konfigurierbar wird.

### `out_of_scope`

- Der übrige Host-/MCP-/EPIC-05-Baum sowie nicht direkt referenzierte
  Assembly-Analyse- und Decompilation-Dateien.
- Vollständige Roadmap-/Dokumentationssweeps ohne Bezug zum neuen
  Konfigurations- oder Cache-Vertrag.
- Unveränderte Step-024/025-Historie und bereits genehmigte Cleanup-
  Verträge.

## Abnahmekriterien

1. **Cache-Identität und Konfiguration:** Für URL, Solution-Pfad,
   Cache-Schema, Root und Refresh-Policy existiert ein strikt validierter,
   credential-freier Vertrag; ungültige Werte werden typed diagnostiziert.
2. **Cache-Miss:** Ein fehlender oder unbrauchbarer Cache-Eintrag wird
   ausschließlich in kontrolliertem Staging geklont, auf geladene Revision
   und Solution-Pfad geprüft und als vollständige Generation bereitgestellt.
3. **Reuse und Refresh:** Ein gültiger, nicht abgelaufener Current-Eintrag
   wird ohne Transportaufruf wiederverwendet; ein abgelaufener Eintrag
   führt deterministisch zu Fetch/Refresh oder einem initialen Clone.
4. **Manifest- und Generationsintegrität:** Pointer, Manifest, Cache-Key,
   Revision, Solution-Pfad und Inhalt müssen zusammenpassen; beschädigte,
   unvollständige oder fremde Generationen werden nicht adoptiert.
5. **Atomare Veröffentlichung:** Der Current-Pointer wechselt erst nach
   vollständiger Validierung. Fehler, Cancellation und Prozessabbruch
   hinterlassen keinen halben Current-Eintrag und behaupten keinen
   fehlgeschlagenen Refresh als aktuell.
6. **Bestehende Fehler- und Sicherheitsinvarianten:** Auth-, HTTP-, Git-,
   Timeout-, Prozessbaum-, Handle-, Native-, 1314- und Reparse-Semantik
   sowie typed Decompilation-Fallback bleiben erhalten; Secrets gelangen
   weder in Manifest noch Prozessargumente.
7. **Ownership-Anschluss:** Der Snapshot erhält weiterhin einen
   besitzgebundenen Request-Checkout und Workspace; persistente
   Generationen werden nicht durch Snapshot- oder Registry-Cleanup gelöscht.
   Parallelzugriffe je Cache-Key bleiben konsistent.
8. **Deterministische Verifikation:** Neue und geänderte Verträge sind mit
   lokalen Test-Doubles/Fixtures für Miss, Reuse, Refresh, Revisionwechsel,
   Korruption, atomaren Fehlschlag, Cancellation und Cleanup abgedeckt;
   Tests führen weder Remote-/Gitea-/Git-Netzwerk noch unbounded Retries aus.

## Tests

Der Coder ergänzt ausschließlich lokale, deterministische Regressionen in
den vorhandenen FastTests-/TestKit-Grenzen. Die Test-Doubles sollen Clone,
Fetch, Revision, Manifestfehler, Cancellation, 1314/Reparse und typed
Providerfehler steuern können, ohne einen Git-Prozess zu starten.

Mindestens folgende Verhaltensgruppen sind abzudecken:

- Cache-Miss und Wiederverwendung eines gültigen Current-Eintrags,
  einschließlich Transport-Aufrufzähler.
- Refresh nach Ablauf, neue geladene Revision und atomarer Pointerwechsel.
- Beschädigtes Manifest, fehlende Solution, falscher Cache-Key,
  unvollständige Generation und fehlgeschlagene Veröffentlichung.
- Refresh-Fehler und Cancellation: kein halbfertiger Current-Eintrag,
  korrekte typed Diagnose/Fallback und vollständiges Staging-Cleanup.
- Request-Checkout versus persistente Generation: Snapshot-Dispose darf
  nicht den Cache löschen; bestehende Registry-Multi-Owner-Tests bleiben
  unverändert grün.
- konkurrierende Zugriffe auf denselben Cache-Key mit deterministischer
  Veröffentlichung.

Der Coder führt danach den projektspezifischen Build sowie beide
Abschlussläufe ohne `Stress` aus:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Der Planer führt diese Tests in diesem Planungsschritt nicht aus.

## MCP-, DRY-, MagicValues- und DeadCode-Plan

### MCP-Semantik

- Vor Änderungen `get_feature_context` und `get_symbol_body` für Acquirer,
  Transport, Konfiguration, Path-Guard und Provider-/Materializer-Anschluss
  erneut prüfen; der absolute `projectRoot` bleibt bei jedem Aufruf
  `C:/Daten/Entwicklung/Ralf/AiNetLinter`.
- Vor Portänderungen `find_references` und `get_impact` für
  `IGiteaRepositoryTransport`, `CloneDefaultBranchAsync`, den neuen
  Refresh-/Fetch-Vertrag, `ExternalSourceRepositoryAcquirer.AcquireAsync`
  und `ExternalSourceConfigurationLoader.Load` ausführen.
- `dependency_graph` auf Acquirer und Cache-Symbole nutzen, um keine
  implizite Host-, Registry- oder Test-Fixture-Abhängigkeit zu übersehen.
  Nach der Implementierung `safeguard`/`get_violations` auf den geänderten
  Bereichen sowie die betroffenen Testprojekte prüfen.
- `rg` bleibt auf Textdateien, Konfigurationsschlüssel und verbotene
  Ausführungsmuster begrenzt; C#-Symbolfragen werden nicht durch Textsuche
  ersetzt.

### DRY-Plan und Tech-Debt-Disposition

- `find_duplicates` nur auf den tatsächlich geänderten externen
  Repository-/Configuration-Bereich und die zugehörigen Tests anwenden.
  Der aktuelle Exact-Scan des Assemblies-Produktionsbereichs zeigt keinen
  Duplikatcluster; das rechtfertigt keinen globalen Refactoring-Sweep.
- Pointer-/Manifest-Logik nicht neben dem Assembly-Cache kopieren, sondern
  einen kleinen, externen Cache-Contract mit klarer Verantwortung bilden.
  Vorhandene Path-Guard-, Failure-Policy-, Reservation- und Test-Helper-
  Verträge wiederverwenden.
- `TD-001` bis `TD-003` bleiben offen. Eine Aufnahme ist nur zulässig, wenn
  die neue Cache-Identität unmittelbar dieselbe Drive-Path- oder
  Origin-Typisierung betrifft; dann als eigener, begründeter Teil des
  betroffenen Vertrags und nicht als Sweep.

### MagicValues-Plan

- Cache-Schema, Pointer-/Manifest-Dateinamen, Statuswerte und
  Refresh-Grenzen zentral als Contract-/Value-Object-Konstanten oder
  validierte Optionen modellieren. Keine verstreuten Stringvergleiche oder
  unbenannten Zeit-/Retry-Literale.
- Der bestehende lokalisierungsverdächtige Fehlertext in
  `ExternalSourceGitProcessLauncher` wird nicht künstlich in diesen Step
  gezogen. Nach der Änderung nur `find_magic_values` auf dem geänderten
  Scope ausführen.

### DeadCode-Plan

- Neue Cache-/Fetch-Symbole müssen durch Acquirer, Provider oder Tests
  tatsächlich referenziert sein; unbenutzte Alternativports werden nicht
  als Vorrat angelegt.
- `find_dead_code` mit hoher Konfidenz auf dem geänderten Produktions- und
  Testscope ausführen. Der aktuelle Scope-Scan enthält keinen hochsicheren
  Dead-Code-Fund; öffentliche/serializer-/DI-gebundene Kandidaten werden
  nicht heuristisch entfernt.

## Kandidatenbewertung und Folgepakete

### Zusammengelegte Kandidaten

- **Refresh/Fetch + persistenter Repository-Cache:** gehören zusammen,
  weil Refresh ohne persistente Identität keinen Wiederverwendungsnutzen
  und der Cache ohne Aktualisierungsvertrag keine Source-of-Truth-Semantik
  besitzt.
- **Cache-/Manifest-Integrität + Generationen:** gehören zusammen, weil
  eine Generation erst durch ihr passendes Manifest und ihre Revision
  identifizierbar und prüfbar ist.
- **Atomare Source-of-Truth-Veröffentlichung:** gehört als Commit-/Safety-
  Teilvertrag dazu, weil Cache und Refresh sonst halbfertige oder falsch
  aktuelle Inhalte publizieren könnten.

### Abgetrennte Kandidaten

- Dirty/unbuilt lokale Checkouts, Health-/degraded-Policy und feinere
  Fallback-Auswahl bleiben der nächste Source-Policy-Schnitt. Step 026
  reicht dafür nur typed Refreshfehler bzw. den vorhandenen Fallback weiter.
- Host-Komposition und produktives Provider-Wiring bleiben getrennt, weil
  der Cache-Vertrag unterhalb der Host-/MCP-Grenze testbar ist und Step 024
  ausdrücklich kein Host-Wiring eingeführt hat.
- Transitive Referenzen und Capability-Matrix bleiben EPIC-05.
- Eine spätere Cache-Wartung (Retention, Garbage Collection, explizite
  Cache-Invalidierung und Telemetrie) bleibt nachgelagert, sofern sie nicht
  für die sichere atomare Veröffentlichung zwingend erforderlich ist.

## Risiken

- **In-Place-Fetch:** Ein Fetch direkt in `current` würde den atomaren
  Vertrag brechen. Der Coder muss Fetch auf einer isolierten neuen
  Generation ausführen und erst danach den Pointer wechseln.
- **Ownership-Verwechslung:** Gibt der Cache seinen persistenten Ordner an
  den Snapshot weiter, löscht Snapshot-Cleanup die Source-of-Truth. Der
  Request-Checkout/Lease-Anschluss muss durch Tests nachgewiesen werden.
- **Stale-as-current:** Bei Refreshfehlern darf der alte Eintrag nicht mit
  neuer Refresh-Zeit oder neuer Revision als erfolgreich aktualisiert
  erscheinen. Fehlerstatus und Fallback müssen sichtbar bleiben.
- **Windows-Pfade und Reparse:** Cache-Root, Generation und abgeleiteter
  Checkout brauchen dieselben kontrollierten Path-/Reparse-/1314-Gates wie
  der bestehende Acquirer.
- **Credential-/Prozessleck:** Der neue Fetch-Pfad kann bestehende
  Prozess-, Handle- und Credential-Cleanup-Invarianten nicht verkürzen.
  Dafür sind Impact-Prüfung und lokale Failure-Doubles verpflichtend.
- **Konfigurationsdrift:** Neue Cache-Optionen müssen Loader, Validierung,
  Fixtures und `Docs/configuration.md` gemeinsam ändern; sonst bleibt die
  Laufzeitsemantik nicht reproduzierbar.

## Definition of Done

- Die acht Abnahmekriterien sind durch Code und lokale deterministische
  Tests erfüllt.
- Der neue primäre Vertrag ist auf höchstens drei gekoppelte Schichten
  begrenzt; keine Host-/MCP-/EPIC-05-Ausweitung ist enthalten.
- Snapshot-/Workspace-Ownership, Registry-Cleanup, 1314-/Reparse-Fallback,
  HTTP-/Git-/Credential-/Prozessbaum-/Handle-/Native-Invarianten und die
  statische Decompilation bleiben erhalten.
- Bei einer Konfigurationsänderung sind `Docs/configuration.md`,
  betroffene Appsettings-Beispiele und die präzise EPIC-04-Roadmap-
  Abgrenzung synchronisiert.
- MCP-Impact-/Violations-Checks, der scoped DRY-/MagicValues-/DeadCode-
  Nachweis und die vorhandenen Test-Fixtures sind dokumentiert.
- `dotnet build` sowie beide projektweiten Testläufe mit
  `Category!=Stress` sind grün; Stress wird nicht automatisch ausgeführt.
- Der Coder erstellt nur die vereinbarten Produktions-/Test-/Dokument-
  änderungen und übergibt danach einen nachvollziehbaren Step-Result-
  Bericht. Push, Host-Wiring und Folgepakete bleiben aus.

## Coder-Hand-off

Arbeite ausschließlich im Repository
`C:/Daten/Entwicklung/Ralf/AiNetLinter` und lies zuerst die zwölf
`read_first`-Dateien dieses Plans. Starte die Implementierung an
`ExternalSourceRepositoryAcquirer`, `IGiteaRepositoryTransport` und der
Configuration-Loader-Grenze. Nutze vor jeder C#-Änderung AiNetLinter-MCP
mit absolutem `projectRoot`; ermittle danach per References/Impact alle
Verbraucher des Transport- und Acquirer-Vertrags.

Implementiere genau den einen primären Vertrag: cache-gestützte
`AcquireOrRefresh`-Akquisition mit gültigem Current-Reuse, isoliertem
Clone/Fetch-Refresh, Manifest-/Generationsprüfung und atomarem Pointer-
Publish. Ein Refresh darf den veröffentlichten Current-Eintrag nie in
Place mutieren. Liefere an den bestehenden Snapshot-Pfad weiterhin einen
kontrollierten, besitzbaren Request-Checkout; ändere Registry, Snapshot-
Dispose und Workspace-Lifetime nicht.

Übernimm Path-Guard, Reservation, Failure-Policy, Credential-,
Cancellation-, Prozessbaum-, Handle-, Native- und typed 1314-/Reparse-
Semantik. Schreibe ausschließlich netzwerkfreie Tests mit vorhandenen
Doubles/Fixtures; kein echter Git-/Gitea-/Remote-Zugriff, kein
Assembly.Load/ALC/Reflection und keine unbounded Retries/Sleeps.

Halte Host-/MCP-Wiring, transitive Referenzen, EPIC-05 und Dirty-/Health-
Policy für Folgepakete zurück. Führe nach der Implementierung MCP-
Violations/Impact sowie die scoped DRY-/MagicValues-/DeadCode-Prüfungen
aus und aktualisiere nur bei direktem Konfigurationsbezug die zugehörige
Dokumentation. Verifiziere mit `dotnet build` und beiden
`Category!=Stress`-Abschlussläufen. Übergib anschließend den Coder-Result-
Bericht mit geänderten Dateien, Kriterienstatus, Testausgaben,
Invarianten-Nachweis, Rest-Risiken und Folgepaket-Empfehlung an den
Kritiker; kein Push.

## Notes

- Dieser Plan ist eine neue Step-026-Planung und korrigiert keinen
  bestehenden Step. Die Wiederaufnahme-Korrektur von Step 025 ist mit
  Review `acdfe70e` abgeschlossen.
- Der Planer hat keine Produktionsänderung vorgenommen und keine Tests,
  Coder- oder Kritikerarbeit ausgeführt.
