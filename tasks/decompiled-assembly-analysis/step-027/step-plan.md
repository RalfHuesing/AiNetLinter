---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 027
corrects: step-026
title: "Fail-closeden Generation-Publish und Testisolation korrigieren"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T17:25:29+02:00
related_to:
  - ../step-026/step-review.md
  - ../step-026/step-plan.md
  - ../step-026/step-result.md
  - ../codemap.md
  - ../follow-up-strategy.md
---

# Step 027: Fail-closeden Generation-Publish und Testisolation korrigieren

## Split-Gate-Entscheidung

Der Step bleibt ein zusammenhängendes Korrekturpaket. Er hat genau einen
primären Vertrag:

> Eine Cache-Generation wird nur dann als gültig veröffentlicht, wenn ihr
> atomarer Publish unter dem synchronisierten Cache-Key vollständig
> rollback- und cleanup-sicher ist, die unabhängige Read-back-Prüfung
> fail-closed und bounded bleibt und Tests keinen persistenten Default-Cache
> benutzen.

Die drei gekoppelten Schichten sind:

1. **Lock-/Rollback-Lifetime:** Der bestehende Same-Key-Lock bleibt bis nach
   Rollback, Pointer-Korrektur und Cleanup gehalten.
2. **Unabhängige bounded Manifest-/Content-Prüfung:** Manifest, unabhängige
   Inventarbindung und Content werden mit harten Grenzen und fail-closed
   geprüft.
3. **Injizierbarer testisolierter Writer-Anschluss:** Betroffene Acquirer-
   und Provider-Tests erhalten einen Writer unter `TestTempDirectory` oder
   den vorhandenen deterministischen Recording-Writer.

Das Split-Gate ist damit erfüllt: ein Vertrag, höchstens drei Schichten,
acht Abnahmekriterien, zehn `read_first`-Dateien und höchstens zwölf
initiale zentrale Dateien. Kein neuer Reuse-, Fetch-, Refresh-, Konfigura-
tions-, Health-, Retention-, Host- oder EPIC-05-Schnitt wird eröffnet.

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` aus `roadmap.md` — persistente Repository-Cache-
  Generationen werden nach erfolgreichem Clone atomar veröffentlicht.
- **Korrektur:** `step-026` ist durch den Kritiker als `issues` markiert;
  dieses Paket korrigiert ausschließlich die drei MAJOR-Findings aus
  `step-026/step-review.md`.
- **Konzept-Referenz:** `Konzept.md`, Cache-Identität, Generationen,
  Manifest-Integrität und atomarer Publish; die spätere Cache-Wiederver-
  wendung bleibt außerhalb dieses Steps.

## Aktueller Projektzustand (JIT-Kontext)

Der bestehende Writer verwendet bereits einen statischen
`ConcurrentDictionary`-Lock je Entry-Pfad, eine Staging-Generation, einen
atomaren Current-Pointer und einen injizierbaren
`IExternalSourceRepositoryCacheWriter`. Das Problem liegt in der Lifetime:
`PublishAsync` gibt den Lock im `finally` frei, bevor sein nachgelagertes
Rollback und `TryDeleteGeneration` laufen. `RestorePreviousCurrent` kann
dadurch einen inzwischen von einem zweiten Publish gesetzten Pointer
überschreiben oder löschen.

Der Reader nutzt bereits strikte JSON-Optionen, Größenkonstanten und
Pfad-/Reparse-Prüfungen. `ValidateInventory` leitet seine erwartete
Dateimenge jedoch aus der veränderbaren `manifest.Files`-Liste ab. Dadurch
kann eine gemeinsam verkürzte Manifest-/Content-Menge ohne den erforder-
lichen Lösungspfad als gültig erscheinen. `ReadBoundedText` prüft aktuell
`FileInfo.Length` und ruft anschließend `File.ReadAllText` auf; zwischen
beiden Operationen besteht ein TOCTOU-Fenster und der Read-Pfad ist nicht
selbst bounded.

Die Acquirer-Produktion hat bereits den korrekten Writer-Injektionspunkt,
behält aber ihren Runtime-Default unter `AppContext.BaseDirectory`. Mehrere
Acquirer-/Provider-Tests lassen den Default greifen. Der Volltest erzeugte
dadurch persistente Generationen unter `bin/Debug/net10.0/cache/source`.
Die vorhandenen `TestTempDirectory`- und `RecordingCacheWriter`-Muster sind
wiederzuverwenden.

## Intention

Der Writer soll einen fehlgeschlagenen oder abgebrochenen Publish vollständig
unter demselben Same-Key-Lock zurückrollen und aufräumen, ohne einen
konkurrierenden erfolgreichen Publish zu beschädigen. Der Reader soll eine
Generation nur bei unabhängiger Vollständigkeit und innerhalb harter
Byte-Grenzen akzeptieren. Alle betroffenen Tests sollen ihren Writer
explizit in einen automatisch bereinigten Testbereich injizieren.

## Architekturgrenze

Die Korrektur bleibt innerhalb des bestehenden lokalen Cache-Writers,
-Readers, der gemeinsamen Cache-Storage-Helfer und ihrer Tests. Der
Same-Key-Lock ist weiterhin ein In-Process-Lock; ein Cross-Process-Lock oder
eine neue Persistenz-/Konfigurationsschicht ist nicht Teil dieses Steps.

Die unabhängige Vollständigkeitsbindung darf als bestehende Cache-Struktur
erweitert werden, beispielsweise durch eine eigene bounded
Inventardatei bzw. einen daraus berechneten stabilen Inventaranker. Sie muss
aus der tatsächlich kopierten Content-Inventur stammen und darf im Reader
nicht aus `manifest.Files` rekonstruiert werden. Die kanonische
`ExpectedSolutionPath`-Datei muss zusätzlich als tatsächlich vorhandene,
reguläre Content-Datei geprüft werden.

Der Runtime-Default des Writers wird nicht testbewusst umgebogen. Die
Testisolation erfolgt an den vorhandenen Konstruktoraufrufen. Es gibt keine
neue öffentliche API und keine Änderung am Acquirer-Fehlervertrag.

## Konkrete Änderungen

### Schicht 1: Lock-/Rollback-Lifetime

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`

- **Was:** Den bestehenden Cache-Key-Lock vom Eintritt in
  `PublishAsync` bis zum Ende sämtlicher Publish-Nacharbeiten halten.
  Rollback, Pointer-Korrektur, fehlgeschlagene Generation- und temporäre
  Pointer-Bereinigung müssen vor `CacheKeyLockLease.Dispose()` erfolgen.
- **Was:** Alle Pointer-Mutationen im Publish-/Rollback-Pfad über diesen
  Lease serialisieren. Der Rollback muss den aktuellen Pointer zunächst
  zustandsbewusst auf die fehlgeschlagene Generation beziehen. Nur wenn er
  noch auf Generation A zeigt, darf A auf die vorherige Generation
  zurückgesetzt bzw. der Pointer bei „keine vorherige Generation“ gelöscht
  werden. Zeigt er bereits auf eine andere Generation, bleibt dieser Pointer
  unangetastet.
- **Was:** Die bisher mögliche doppelte Rollback-Mutation zwischen
  `TryValidatePublishedGeneration` und dem äußeren `finally` auf genau einen
  zuständigen Pfad reduzieren. Die Validierung meldet nur das Ergebnis; die
  zentrale Publish-Finalisierung erledigt Rollback und Cleanup unter dem
  Lease.
- **Warum:** Ein zweiter Publish darf nach A's Pointer-Publish weder durch
  A's verspätetes Rollback zurückgesetzt noch durch A's Cleanup gelöscht
  werden. Ein abgebrochener Publish darf keine gültige konkurrierende
  Generation entwerten.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs`

- **Was:** Vorhandene `TryPublishPointer`,
  `RestorePreviousCurrent` und `TryDeleteGeneration` so aufrufen bzw. eng
  abgrenzen, dass sie keine ungeschützte Pointer-Mutation oder ein
  unbedingtes Löschen eines fremden Current-Pointers ermöglichen. Die
  Zustandsprüfung muss die bestehende sichere Pfad- und Reparse-Logik
  wiederverwenden.
- **Warum:** Die Synchronisierung gehört zum gesamten Lebenszyklus, nicht
  nur zum erfolgreichen Pointer-Publish. Die Storage-Helfer sollen keinen
  zweiten, abweichenden Lock einführen.

### Schicht 2: Unabhängige bounded Manifest-/Content-Prüfung

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs`

- **Was:** Neue Inventar-/Read-back-Konstanten und ein eventuelles internes
  Inventarformat zentral neben den bestehenden Cache-Konstanten definieren.
  Größen-, Pfad- und Schemawerte dürfen nicht über Writer und Reader
  verstreut werden.
- **Warum:** Die zusätzliche Vollständigkeitsbindung muss denselben
  deterministischen Cache-Vertrag verwenden und darf keine Magic Values
  einführen.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs`

- **Was:** Beim Schreiben die tatsächliche, erfolgreich kopierte Content-
  Inventur unabhängig vom Manifest serialisieren bzw. binden. Die Bindung
  muss mindestens kanonische relative Pfade, Längen und Hashes sowie eine
  Gesamtvollständigkeit enthalten. Sie darf nicht später aus der mutierbaren
  `manifest.Files`-Liste abgeleitet werden.
- **Warum:** Der Reader braucht eine zweite Erwartungsquelle, damit eine
  gemeinsam verkürzte Manifest-/Content-Menge nicht als vollständige
  Generation durchgeht.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs`

- **Was:** `ValidateInventory` auf eine unabhängige Inventarquelle stützen,
  die tatsächliche Content-Dateien gegen ihre erwarteten Pfade, Längen und
  Hashes vergleicht. Zusätzlich die aus dem Read-Request stammende
  kanonische `ExpectedSolutionPath`-Datei als reguläre Datei im Content und
  im unabhängigen Inventar erzwingen. `manifest.Files` darf allein niemals
  die Vollständigkeit begründen.
- **Was:** Pointer und Manifest mit einem einzigen strikten UTF-8-
  `FileStream` bounded lesen: höchstens `Max...Bytes` plus einen
  Überlauf-Byte einlesen, Wachstum während des Reads erkennen und bei
  Übergröße, Trunkierung, ungültiger UTF-8-Sequenz, unbekannten/duplizierten
  Eigenschaften oder JSON-Fehlern fail-closed abbrechen. `FileInfo.Length`
  vor dem Read und `File.ReadAllText` sind im Produktions-Read-Pfad zu
  entfernen.
- **Was:** Auch Content-Hash-/Längenprüfungen mit einer harten
  `MaxFileLength`- bzw. erwarteten Längengrenze durchführen. Nach dem
  Grenzwert muss ein weiterer Byte-Befund als ungültig gelten; kein
  unbounded Read bis EOF und keine Aufweichung bei konkurrierendem Wachstum.
- **Warum:** Metadaten und Content müssen dieselbe fail-closed/bounded
  Sicherheitsgrenze einhalten. Ein Angreifer darf weder durch eine
  manipulierbare Dateiliste noch durch TOCTOU zwischen Größenprüfung und
  Vollread eine ungültige Generation akzeptieren lassen.

### Schicht 3: Injizierbarer testisolierter Writer-Anschluss

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`

- **Was:** Erfolgreiche und potenziell schreibende Acquirer-Testpfade über
  einen lokalen Writer unter `TestTempDirectory` führen. Einen kleinen
  testspezifischen Factory-/Helper-Anschluss verwenden, damit kein direkter
  Konstruktor den Runtime-Default unabsichtlich aktiviert.
- **Warum:** Die Tests dürfen keine Generationen unter
  `AppContext.BaseDirectory` bzw. `bin/.../cache/source` persistieren.

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaExternalSourceProviderTests.cs`

- **Was:** Den vorhandenen Acquirer-Testhelper mit einem Writer unter dem
  jeweiligen `TestTempDirectory`-Root verdrahten; den direkten Acquirer-
  Konstruktoraufruf ebenfalls prüfen.
- **Warum:** Provider-Tests teilen denselben Write-through-Anschluss und
  sollen dieselbe Isolationsgarantie haben.

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCancellationTests.cs`
und `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs`

- **Was:** Direkte Acquirer-Konstruktionen, die den Writer-Anschluss
  erreichen können, mit einem lokalen Test-Writer oder einem passenden
  `RecordingCacheWriter` versorgen. Reine Fehlerpfade dürfen weiterhin
  schlank bleiben, sofern MCP-/Testprüfung belegt, dass kein Publish
  erreicht wird.
- **Warum:** Kein betroffener Testpfad darf still auf den Runtime-Default
  zurückfallen. Die Auswahl bleibt auf tatsächlich schreibende Testpfade
  begrenzt.

#### Betroffene bestehende Testdateien

- **Was:** `ExternalSourceRepositoryCacheWriterTests` um deterministische
  Race-, Cleanup- und bounded-Read-back-Fälle erweitern. Die bereits
  injizierenden `ExternalSourceRepositoryCacheAcquirerTests` und
  `RecordingCacheWriter` wiederverwenden, nicht parallel ein zweites
  Test-Double erfinden.
- **Warum:** Die drei Findings müssen reproduzierbar abgenommen werden,
  ohne Sleeps, Timing-Annahmen oder externe Prozesse.

## Exakte Korrekturprinzipien

1. **Lease-Lifetime ist transaktional:** Nach `AcquireLockAsync` bleibt der
   Lease bis einschließlich Rollback und Cleanup aktiv; Cancellation darf
   die notwendige Aufräumphase nicht überspringen.
2. **Rollback ist generation-aware:** Nur der Pointer, der noch auf die
   fehlgeschlagene Generation zeigt, darf korrigiert werden. Ein neuerer
   Pointer wird nie überschrieben oder gelöscht.
3. **Eine Rollback-Stelle:** `TryValidatePublishedGeneration` verändert
   keinen Pointer; die zentrale Finalisierung entscheidet genau einmal über
   Restore/Delete und löscht danach nur die fehlgeschlagene Generation.
4. **Unabhängige Vollständigkeit:** Die erwartete Content-Menge kommt aus
   einer separaten, bounded Inventarbindung. `manifest.Files` ist eine zu
   prüfende Darstellung, keine alleinige Vollständigkeitsquelle.
5. **Kanonischer Anker:** Der aus dem Request bekannte
   `ExpectedSolutionPath` muss als sichere reguläre Datei tatsächlich in
   Content und Inventar vorhanden sein.
6. **Bounded vom Stream an:** Pointer, Manifest und Content lesen selbst
   bis zu ihren harten Grenzen und erkennen ein zusätzliches Byte. Kein
   vorgelagerter `FileInfo`-Vertrauensanker und kein `ReadAllText` im
   Produktions-Reader.
7. **Runtime-Default bleibt Runtime-Default:** Nur Tests injizieren einen
   isolierten Writer; Produktionscode erkennt Tests nicht und ändert keine
   AppContext-/Konfigurationssemantik.
8. **Keine verdeckte Ausweitung:** Keine Änderungen an Reuse, Fetch,
   Refresh, Konfiguration, Health, Retention, Host/MCP, Provider-/Transport-
   Semantik, Snapshot/Registry oder EPIC 05.

## Abnahmekriterien

1. [ ] Der Same-Key-Lease bleibt vom Publish-Eintritt bis nach Rollback,
   Pointer-Korrektur und fehlgeschlagener Generation-/Temp-Pointer-
   Bereinigung aktiv; jede Pointer-Mutation des Pfads ist darüber
   serialisiert.
2. [ ] Ein deterministischer Test bricht Publish A unmittelbar nach dessen
   Pointer-Publish ab und startet Publish B über denselben Key. Das Ergebnis
   bleibt sowohl mit als auch ohne vorherige Current-Generation konsistent:
   B bleibt Current und gültig, A wird nicht als fremde Generation gelöscht
   und ein fehlerhafter A-Pointer wird korrekt bereinigt.
3. [ ] Der Reader weist eine Generation zurück, wenn Manifest und Content
   gemeinsam verkürzt werden oder `ExpectedSolutionPath` fehlt, selbst wenn
   `manifest.Files` entsprechend verkürzt wurde. Die unabhängige
   Inventarbindung und der kanonische Dateianker werden beide geprüft.
4. [ ] Pointer- und Manifest-Reads verwenden strikt UTF-8, harte Byte-
   Grenzen und Überlauf-Erkennung im Stream. Oversize, Wachstum während des
   Reads, Trunkierung, unbekannte oder doppelte JSON-Eigenschaften führen
   ohne unbounded Allocation zu fail-closed.
5. [ ] Content-Längen-/Hash-Reads sind ebenfalls hart bounded, erkennen
   Wachstum bzw. Überlänge und akzeptieren unveränderte gültige Generationen
   weiterhin; Pfad- und Reparse-Schutz bleiben erhalten.
6. [ ] Alle tatsächlich schreibenden Acquirer-/Provider-/Cancellation-
   Testpfade injizieren einen Writer unter `TestTempDirectory` oder den
   vorhandenen Recording-Writer. Ein Testlauf erzeugt keine neuen
   Generationen unter `AppContext.BaseDirectory/cache/source`; isolierte
   Roots werden nach dem Test bereinigt.
7. [ ] Bestehende Runtime-Defaults, Cache-Fehlerwarnung/Fail-open des
   Acquirers, Ownership-/Transport-/Cancellation-Verträge und das
   In-Process-Lock-Modell bleiben unverändert; keine Out-of-Scope-Datei
   wird geändert.
8. [ ] Der fokussierte Testlauf, `dotnet build` sowie beide vollständigen
   Testläufe mit `Category!=Stress` sind grün. Bekannte Umgebungs-Skips
   wie Win32-Fehler 1314 werden transparent dokumentiert und nicht durch
   Sleeps oder Capability-Fakes verdeckt.

## Kontextbudget

```yaml
max_initial_files: 12
max_read_first: 10
read_first:
  - tasks/decompiled-assembly-analysis/step-026/step-review.md
  - tasks/decompiled-assembly-analysis/step-026/step-plan.md
  - tasks/decompiled-assembly-analysis/step-026/step-result.md
  - tasks/decompiled-assembly-analysis/codemap.md
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs
read_on_demand:
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaExternalSourceProviderTests.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCancellationTests.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs
  - src/AiNetLinter.TestKit/TestTempDirectory.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs
out_of_scope:
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySnapshotMaterializer.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySnapshotStore.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceAssemblyCache.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceConfiguration.cs
  - src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs
  - src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs
  - src/AiNetLinter/Mcp/Host/ExternalSourceRepositoryMcpTools.cs
  - rules.json
  - tasks/decompiled-assembly-analysis/roadmap.md
  - tasks/decompiled-assembly-analysis/tech-debt.md
```

Die ausgenommenen Provider- und Transportdateien sind nur dann zu ändern,
wenn ein betroffener Acquirer-Konstruktor dort tatsächlich angepasst werden
müsste; ihre Testanschlüsse dürfen gelesen werden. Die Liste begrenzt
Architekturänderungen, nicht die notwendige JIT-Prüfung.

## Coder-Hand-off

Arbeite als neuer Coder-Agent mit einem frischen Kontext. Lies zuerst die
zehn `read_first`-Dateien, initial insgesamt höchstens zwölf zentrale
Dateien. Nutze für C#-Semantik ausschließlich AiNetLinter-MCP mit exakt

`projectRoot: C:/Daten/Entwicklung/Ralf/AiNetLinter`.

1. Ermittle per `get_feature_context`, `get_symbol_body`,
   `find_references` und `get_impact` den aktuellen Lebenszyklus von
   `PublishAsync`, `PublishGeneration`, `TryValidatePublishedGeneration`,
   `ReadBoundedText`, `ValidateInventory`, `TryPublishPointer`,
   `RestorePreviousCurrent` und `TryDeleteGeneration`. Verwende `rg` nur
   für Text-/Literal-/Konstruktor-Suchen.
2. Implementiere zuerst die Lock-Lifetime und den generation-aware
   Rollback. Halte den Cleanup-Pfad ohne Cancellation-Abkürzung unter dem
   Lease und entferne die doppelte Rollback-Mutation.
3. Implementiere danach die unabhängige Inventarbindung, den
   `ExpectedSolutionPath`-Anker und die bounded Stream-Prüfungen für
   Metadaten und Content. Bewahre bestehende JSON-, Pfad-, Reparse- und
   Fehlersemantik auf.
4. Führe anschließend die betroffenen Testfabriken auf isolierte Writer
   um. Ändere den Runtime-Default nicht. Verwende für deterministische
   Race-/TOCTOU-Tests einen kleinen internen Test-Seam oder eine
   synchronisierte Test-Stream-/Pointer-Abstraktion; keine Sleeps,
   `Task.Delay`, FileSystemWatcher, zufällige Retries oder Cross-Process-
   Hilfsprozesse.
5. Ergänze die Abnahmetests aus den Kriterien 2 bis 6. Nutze vorhandene
   `TestTempDirectory`, `RecordingCacheWriter` und Source-Fixture-Patterns.
   Prüfe nach Testdisposal, dass der isolierte Root verschwunden ist und
   kein neuer Default-Cache angelegt wurde.
6. Aktualisiere `step-027/step-result.md` mit geänderten Dateien,
   Testergebnissen, MCP-/Audit-Ergebnissen und bekannten Ausnahmen. Setze
   den Step-Plan erst nach erfolgreichem Coder- und Critic-Gate auf
   `done (pending audit)`.

## Tests

### Fokussierte Iteration

- [ ] Writer-, Cache-Acquirer-, Acquirer-, Gitea-Provider- und
  Cancellation-Tests mit einem `FullyQualifiedName`-Filter ausführen:

  ```powershell
  dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests|FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~GiteaExternalSourceProviderTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests"
  ```
- [ ] Race mit und ohne vorherige Current-Generation deterministisch
  ausführen und Pointer, Generationen sowie Cleanup nachweisen.
- [ ] Gemeinsame Manifest-/Content-Verkürzung, fehlende
  `ExpectedSolutionPath`-Datei, Oversize, ungültige UTF-8-Daten und
  kontrolliertes Wachstum während eines bounded Reads prüfen.
- [ ] Erfolgreiche gültige Generation, bestehende Fehlerpfade und
  TestTempDirectory-Cleanup regressiv prüfen.

### Abschluss-Gates

- [ ] `dotnet build`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- [ ] Keine Stress-Tests automatisch starten; bekannte Skips und
  Umgebungsbefunde im Resultat festhalten.

## MCP-, DRY-, MagicValues- und DeadCode-Plan

- **MCP vor und nach der Änderung:** Mit absolutem `projectRoot` die
  genannten Symbole, Referenzen, Auswirkungen und Testkontexte prüfen.
  Nachher `get_violations` und `safeguard` auf den geänderten Assemblies
  sowie `find_duplicates`, `find_magic_values` und `find_dead_code` nur
  auf den relevanten Writer-/Reader-/Testisolations-Scope anwenden.
- **DRY:** Bestehenden `CacheKeyLockLease`, Storage-Pointer-Helfer,
  `TestTempDirectory` und `RecordingCacheWriter` wiederverwenden. Nur
  Duplikate beseitigen, die durch dieses Paket im Publish-/Read-back- oder
  Test-Factory-Scope entstehen. Kein globaler Refactor.
- **Magic Values:** Inventar-Dateiname, Schema-/Byte-/Dateigrenzen und
  Bufferwerte in den bestehenden Cache-Vertragskonstanten zentralisieren;
  keine lokale Wiederholung in Writer, Reader oder Tests.
- **Dead Code:** Nach dem Umbau unbenutzte Rollbackpfade, Test-Seams,
  Overloads oder Test-Helpers im betroffenen Scope entfernen. Nur
  hochsichere Befunde behandeln; keine globale Bereinigung.
- **MCP-Grenze:** `rg` bleibt auf Textsuche beschränkt. Keine semantische
  C#-Suche durch `rg` ersetzen und kein globaler Audit außerhalb des
  Korrekturvertrags ausführen.

## Risiken und Gegenmaßnahmen

- **Längere Lock-Holding-Zeit:** Read-back und Cleanup bleiben pro Cache-Key
  seriell und können die Latenz erhöhen. Nur den bestehenden Entry-Lock
  halten, keine globale Sperre und keine neue Cross-Process-Semantik.
- **Rollback während konkurrierendem Publish:** Ein bedingter Restore/
  Delete anhand des noch aktuellen Generation-Namens schützt B. Der
  Race-Test muss beide Anfangszustände abdecken.
- **Fehler im Dateisystem während Cleanup:** Bestehende typed Cache-Failure-
  Semantik und sichere Pfad-/Reparse-Prüfungen bleiben erhalten. Ein
  Cleanup-Fehler darf keinen neueren Current-Pointer überschreiben.
- **Cache-Schema-/Altbestand:** Eine fehlende oder ungültige unabhängige
  Inventarbindung wird fail-closed behandelt. Es gibt in diesem Step keine
  Migration, Reuse- oder Refresh-Logik.
- **Bounded-Read-Implementierung:** Überlauf-Byte, Integer-Grenzen,
  strict UTF-8 und kontrollierte Stream-Lebensdauer müssen getestet werden.
  Ein interner deterministischer Seam ist enger zu halten als eine neue
  öffentliche Abstraktion.
- **Unvollständige Testumstellung:** Konstruktoraufrufe mit `rg` plus MCP-
  Impact prüfen; erfolgreiche und nur potenziell schreibende Pfade
  isolieren. Den Runtime-Default nicht testbedingt ändern.
- **Umgebungs-Skips:** Win32-1314-Fälle bleiben echte Capability-Skips;
  keine Fake-Berechtigung und keine Änderung am Scope.

## Definition of Done

- [ ] Alle drei MAJOR-Findings des Step-026-Kritikers sind durch die acht
  Kriterien abgedeckt und im Code mit deterministischen Tests belegt.
- [ ] Lock-/Rollback-Lifetime, unabhängige bounded Read-back-Prüfung und
  testisolierter Writer bleiben innerhalb der Architekturgrenze.
- [ ] Keine Produktionsänderung außerhalb des beschriebenen Publish-/
  Read-back-Scope und keine Roadmap-/Konfigurations-/EPIC-05-Ausweitung.
- [ ] MCP-, DRY-, MagicValues- und DeadCode-Prüfungen sind scoped
  durchgeführt; relevante Befunde sind behoben oder begründet.
- [ ] `dotnet build` und beide vollständigen Nicht-Stress-Test-Gates sind
  grün; Stress bleibt unaufgerufen.
- [ ] Coder erstellt `step-027/step-result.md`; danach prüft ein neuer
  Critic-Agent den Step. Erst nach bestandenem Review wird der Status auf
  `done (pending audit)` gesetzt.
- [ ] Der Orchestrator committe nur Plan-/Status-/gegebenenfalls
  Roadmap-Dateien dieses Planungsschritts mit deutschem Conventional
  Commit und Suffix `[decompiled-assembly-analysis]`; kein Push.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — C#-Qualitätsregeln, Warnungsfreiheit,
  keine dynamische Assembly-/ALC-Ausweitung.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §2 — JIT-Kontext, TestKit,
  TestTempDirectory, Testparallelität und vollständige Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 — MCP-first für Semantik,
  `rg` nur für Text und nachvollziehbare Verifikation.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — Commitformat,
  Status-/Resultatführung und kein Push.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — absolutes `projectRoot`,
  Symbol-/Impact-/Violation-Abfragen, scoped Safeguard und Audits.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` —
  Fix-Modus, `corrects: step-026`, ein Coder-/Critic-Gate, kein Planner-
  Commit und keine Roadmap-Änderung im Fix-Modus.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md` —
  flacher Korrektur-Step, Kontextbudget, nur Findings, kein Produktions-
  Code durch den Planer.

## Bekannte Ausnahmen

- Die bekannte Win32-Fehler-1314-Ausnahme bei Reparse-/Symlink-Tests darf
  als dokumentierter Skip bestehen bleiben, sofern der Testlauf ansonsten
  grün ist. Sie ist kein Grund, Berechtigungen, Testsicherheit oder
  Produktionssemantik zu fälschen.
- `roadmap.md` wird nicht geändert: Step 027 korrigiert die bereits
  geplante EPIC-04-Publish-Grenze und verändert weder Meilenstein noch
  Reihenfolge. `tech-debt.md` wird ebenfalls nicht geändert, da keine der
  vorhandenen TD001-TD005-Feststellungen in diesen Korrekturvertrag fällt.

## Code-Skizze

```text
lease = AcquireSameKeyLease(entry, cancellationToken)
try:
    result = PublishAndReadBack(context)
    if result.failed:
        RollbackOnlyIfCurrentIs(context.failedGeneration,
                                context.previousGeneration)
        DeleteOnlyFailedGeneration(context)
    return result
finally:
    lease.Dispose()
```

Die Skizze ist eine Lifetime-Invariante, kein Auftrag für eine neue
Abstraktionsschicht. `RollbackOnlyIfCurrentIs` darf einen inzwischen
erfolgreichen B-Pointer nicht anfassen.

## Notes

- Dies ist ein Fix-Modus-Step und korrigiert ausschließlich
  `step-026/step-review.md`; die Korrekturkette bleibt bei `step-026` →
  `step-027`.
- Für den Plan wurden die relevanten C#-Symbole bereits per
  AiNetLinter-MCP mit `projectRoot` gleich
  `C:/Daten/Entwicklung/Ralf/AiNetLinter` geprüft. Der Coder muss den
  aktuellen Stand nach der Übergabe erneut JIT-validieren.
- Der Planer ändert keinen Produktionscode, führt keinen globalen
  DRY-/MagicValues-/DeadCode-Sweep aus und erstellt keine neue Task-
  Wiederverwendung.
