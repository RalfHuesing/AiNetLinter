---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 028
corrects: step-027
title: "Deterministische Read-back- und Lock-Lifetime-Nachweise ergänzen"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T18:39:10+02:00
related_to:
  - ../step-027/step-review.md
  - ../step-027/step-plan.md
  - ../step-027/step-result.md
  - ../codemap.md
  - ../follow-up-strategy.md
  - ../Konzept.md
---

# Step 028: Deterministische Read-back- und Lock-Lifetime-Nachweise ergänzen

## Split-Gate-Entscheidung

Dieser Korrektur-Step behandelt ausschließlich die zwei MAJOR-Findings aus
`step-027/step-review.md`. Der primäre Vertrag lautet:

> Atomic Generation Publish bleibt auch unter adversarial bounded Read-back
> beweiskräftig: Ein konkurrierender Publish darf durch A's Rollback nicht
> beschädigt werden, und jede relevante Manifest-/Inventargrenze wird
> deterministisch fail-closed nachgewiesen.

Das Paket hat höchstens drei unmittelbar gekoppelte Schichten:

1. ein test-only Async-Seam für die Lock-/Interleaving-Reihenfolge,
2. eine vollständige bounded-Malformed-Input-Matrix für Pointer, Manifest
   und Inventar,
3. lokale Fixture- und Assertion-Wiederverwendung im vorhandenen partiellen
   Cache-Writer-Test.

Damit bleiben ein primärer Fachvertrag, drei Schichten, acht
Abnahmekriterien und höchstens zehn initiale Lesedateien eingehalten. Es
gibt keine neue fachliche Produktionslogik. Nur wenn der Growth-Fall ohne
Kontrolle des Read-Streams nicht reproduzierbar testbar ist, darf ein
kleinstmöglicher interner, per Read-Aufruf übergebener Stream-Seam ergänzt
werden; der Produktionsdefault bleibt unverändert und fail-closed.

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — persistente Repository-Cache-Generationen und
  atomarer `current`-Publish.
- **Korrektur:** `step-027` hat die Produktions-Lifetime und die bounded
  Read-back-Prüfung korrigiert, aber die kritische A/B-Reihenfolge und die
  vollständige Testmatrix nicht deterministisch belegt.
- **Konzept-Referenz:** `Konzept.md`, „Staleness und atomarer
  Session-Wechsel“, „Fehler-, Sicherheits- und Vertrauensvertrag“ sowie
  „Teststrategie für die spätere Umsetzung“.

Der Minor-Befund zur abweichenden Metadatenzeile in
`step-027/step-result.md` wird in diesem Plan nicht zu einem dritten
Finding erweitert. Die beiden MAJOR-Findings bleiben der vollständige
Korrekturscope.

## Aktueller Projektzustand (JIT-Kontext)

Die MCP-Abfragen wurden mit dem absoluten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt.

- `LocalExternalSourceRepositoryCacheWriter.PublishAsync` hält im aktuellen
  Stand den `CacheKeyLockLease` bis `FinalizePublish` abgeschlossen ist.
  `FinalizePublish` führt bei einem fehlgeschlagenen Publish zunächst
  generation-aware Rollback und Cleanup aus und gibt den Lease erst danach
  frei. Der vorhandene `afterPointerPublished`-Callback startet im Test B,
  cancelt A aber ohne eine kontrollierte Beobachtung der kritischen
  Freigabe-/Cleanup-Grenze.
- `ExternalSourceRepositoryCacheReadSupport.ReadBoundedText` liest bereits
  über einen einzelnen strict-UTF-8-Stream mit Überlaufbyte und erkennt
  Größenänderungen; `ReadInventory` und `ValidateInventory` prüfen eine
  separate Inventardatei, Datei-Hashes, den Solution-Anker und die
  Inventargrenzen. Die vorhandenen Tests prüfen davon nur Pointer-Oversize,
  Pointer-UTF-8, ein verkürztes Manifest sowie einzelne Content-Growth- und
  Content-Truncation-Fälle.
- `ExternalSourceRepositoryCacheReader` ruft denselben bounded Reader für
  Manifest und Inventar auf und verwirft unbekannte bzw. doppelte JSON-
  Eigenschaften. Für den Testbeweis fehlen aber je ein kontrollierter
  Nachweis für Manifest und Inventar bei Oversize, ungültigem UTF-8,
  Trunkierung und Wachstum sowie eine systematische Prüfung der Inventar-
  Limits.
- `ExternalSourceRepositoryCacheWriterReadBackTests.cs` ist ein partieller
  Teil von `ExternalSourceRepositoryCacheWriterTests`. `SourceFixture`,
  `TestTempDirectory`, `ReadCurrent` und der vorhandene
  `RecordingCacheWriter` stehen bereits zur Wiederverwendung bereit. Es
  wird keine zweite Fixture- oder Assertion-Infrastruktur angelegt.
- Die aktuellen scoped MCP-Audits melden 0 Violations im
  `ExternalSourceRepositoryCache`-Scope, 0 Clone-Cluster in 345
  Produktions- bzw. 95 Testmethoden und 0 hochkonfidente Dead-Code-Funde.
  Der Magic-Value-Audit meldet 33 Treffer in 32 bestehenden Einträgen;
  daraus wird kein globaler Sweep abgeleitet.

## Intention

Nach diesem Step beweist ein einzelner, wiederholbarer Test sowohl mit als
auch ohne vorherige Current-Generation, dass B nach A's tatsächlicher
Cleanup-/Rollback-Phase veröffentlicht wird. Eine tabellarische
Read-back-Matrix weist zusätzlich alle geforderten bounded JSON-, UTF-8-,
Truncation-, Growth- und Inventar-Limit-Fälle zurück, während eine gültige
Generation und die bestehenden Content-Hash-Regressionsfälle grün bleiben.

## Konkrete Änderungen

### Schicht 1: Test-only Lock-/Interleaving-Seam

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`

- **Was:** Den bestehenden Pointer-Callback in einen minimalen internen,
  asynchron beobachtbaren Test-Seam überführen oder gleichwertig ergänzen.
  Der Seam darf nur über den Konstruktor-/Methodenpfad eines Tests injiziert
  werden, bleibt per Writer-/Publish-Aufruf isoliert und hat keinen Einfluss
  auf den Runtime-Default. Es darf kein statischer Testzustand und keine
  öffentliche API entstehen.
- **Was:** Die Hook-Punkte müssen diese Reihenfolge abbilden, ohne
  `Thread.Sleep`, `Task.Delay`, `.Wait()`, `.Result` oder
  `GetAwaiter().GetResult()`:
  1. A signalisiert nach seinem Pointer-Publish und startet B; die
     Cancellation von A wird an dieser Stelle ausgelöst.
  2. B wartet vor seinem Pointer-Publish auf eine `SemaphoreSlim`-Freigabe.
  3. Ein Hook unmittelbar nach der tatsächlichen Freigabe von A's
     Cache-Key-Lease gibt diese Semaphore frei und wartet asynchron auf
     eine `TaskCompletionSource`-Bestätigung von B's Pointer-Publish.
  4. B signalisiert den Pointer-Publish über eine mit
     `RunContinuationsAsynchronously` erzeugte TCS.
- **Warum:** Im alten Step-026-Ablauf konnte B nach der verfrühten
  Lease-Freigabe vor A's Rollback laufen. Der Seam macht genau diesen
  Zwischenzustand reproduzierbar: Beim alten Ablauf liegt B's Pointer vor
  A's Cleanup; beim aktuellen Ablauf liegt A's Cleanup vor der Freigabe an
  B. Die bestehende generation-aware Rollback- und Produktionslogik wird
  nicht neu gestaltet, sondern nur beobachtbar gemacht.
- **Randbedingung:** Falls dafür `FinalizePublish` async werden muss, darf
  die Änderung ausschließlich das Awaiting des internen Test-Seams und die
  unveränderte bestehende Reihenfolge betreffen. `CacheKeyLockLease` bleibt
  ein In-Process-Lock; eine Cross-Process-Garantie wird nicht eingeführt.

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs`

- **Was:** Den vorhandenen A/B-Theory-Test in beiden Varianten
  (`hasPreviousCurrent=true/false`) auf den Seam umstellen. A muss nach
  Cancellation als `Cancelled` zurückkehren; B muss erfolgreich bleiben,
  den alleinigen Current-Pointer besitzen und ein gültiges Read-back
  liefern. A's unpublizierte Generation muss entfernt sein; die vorherige
  Generation bleibt bei vorhandenem Vorzustand erhalten.
- **Was:** Die Teststeuerung muss ausschließlich über
  `TaskCompletionSource` und `SemaphoreSlim` laufen. Der Test wartet auf
  die expliziten Signale, nicht auf Scheduling, Zeitabstände oder zufällige
  Retries. Ein eventueller großzügiger Abbruchschutz darf nur einen
  Deadlock sichtbar machen, nie die fachliche Reihenfolge bestimmen.
- **Warum:** Ein grüner Lauf darf nicht mehr davon abhängen, ob der
  Threadpool A's Cleanup vor B's Publish zufällig ausführt. Der Test muss
  den alten Lifetime-Fehler reproduzierbar rot machen und den korrigierten
  Lifetime-/Rollback-Vertrag grün belegen.

### Schicht 2: Bounded-Malformed-Input-Matrix

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs`

- **Was:** Eine gültige Generation je Matrixfall veröffentlichen, genau eine
  Metadatendatei mutieren, `TryReadCurrent` ausführen und den gültigen
  Ausgangszustand pro Fall frisch wiederherstellen. Die Mutationen werden
  als rohe UTF-8-Bytes bzw. explizite JSON-Fragmente erzeugt, damit auch
  doppelte Eigenschaften tatsächlich im JSON stehen können.
- **Was:** Mindestens folgende Matrix ausführen und jeden Fall
  fail-closed (`false`, kein gültiges Resultat) erwarten:

  | Artefakt | Oversize | ungültiges UTF-8 | Trunkierung | Wachstum beim Read | unbekanntes Feld | doppeltes Feld |
  |---|---:|---:|---:|---:|---:|---:|
  | `current` | bestehender Nachweis bleibt | bestehender Nachweis bleibt | ergänzen | ergänzen | bestehender Parser-Nachweis | bestehender Parser-Nachweis |
  | `manifest.json` | `MaxManifestJsonBytes + 1` | ergänzen | ergänzen | ergänzen | ergänzen | ergänzen |
  | `inventory.json` | `MaxInventoryJsonBytes + 1` | ergänzen | ergänzen | ergänzen | ergänzen | ergänzen |

- **Was:** Die Inventar-Limitmatrix ergänzt die Metadatenfälle um
  `MaxInventoryEntries + 1`, `MaxInventoryBytes + 1`, eine kumulierte
  Dateigröße oberhalb von `MaxInventoryBytes`, eine Dateilänge oberhalb
  von `MaxFileLength`, einen Pfad oberhalb von
  `MaxRelativePathLength` sowie eine widersprüchliche `fileCount`-/Datei-
  menge. Die Fälle müssen die tatsächlich zuständigen Parser-/Limitpfade
  erreichen und dürfen nicht nur wegen eines zufälligen JSON-Fehlers
  fehlschlagen.
- **Was:** Unbekannte und doppelte Felder sowohl auf der obersten
  Manifest-/Inventarebene als auch, wo der gemeinsame Datei-Parser greift,
  an einem Inventar-Dateieintrag abdecken. Bestehende
  `ExpectedSolutionPath`-, Hash-Growth- und Hash-Truncation-Assertions
  bleiben erhalten und werden nur über gemeinsame lokale Helpers
  wiederverwendet.
- **Warum:** Damit wird die im Step-027-Review benannte Lücke zwischen
  implementiertem fail-closed Reader und tatsächlich bewiesenem
  Read-back-Vertrag geschlossen, ohne Reuse, Refresh oder Cache-Policy zu
  öffnen.

#### Bedingter minimaler Read-Stream-Seam

- **Was:** Wenn der Growth-Fall mit realen Dateien nicht deterministisch
  zwischen Größenprüfung und Read kontrolliert werden kann, ergänze einen
  kleinen internen, per Read-Aufruf übergebenen Stream-/Open-Read-Seam für
  `ReadBoundedText` beziehungsweise den Reader-Aufrufpfad. Der Default muss
  weiterhin den bestehenden `FileStream` öffnen; kein statischer Hook,
  keine Konfiguration und keine neue öffentliche Abstraktion.
- **Wie:** Der Teststream liefert zunächst die gültigen bounded Bytes und
  meldet bzw. liefert anschließend ein zusätzliches Byte oder eine
  veränderte Länge. Die Manifest- und Inventar-Growth-Fälle laufen über
  denselben Produktions-Readpfad mit diesem kontrollierten Stream. Der
  Seam wird nach dem Test nicht global zurückgesetzt, weil er gar keinen
  globalen Zustand besitzen darf.
- **Grenze:** Gibt es eine gleichwertige kontrollierte Fixture-Lösung
  ohne Produktionsänderung, ist diese vorzuziehen. Die strict-UTF-8-,
  Überlaufbyte-, JSON- und fail-closed-Logik wird nicht abgeschwächt oder
  durch ein Test-Sonderformat umgangen.

### Schicht 3: Lokale Fixture-/Assertion-Wiederverwendung

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs`

- **Was:** `SourceFixture.Create`, `TestTempDirectory`, den bestehenden
  `ReadCurrent`-Helper und die vorhandenen Cache-Vertragskonstanten
  wiederverwenden. Neue kleine Helpers dürfen nur wiederholte lokale
  Mutation/Assertion bündeln; sie erhalten sprechende Namen und keine
  globale Testzustandsverwaltung.
- **Was:** Keine neuen Ad-hoc-Temp-Pfade, keine zweite
  `SourceFixture`-Implementierung, kein paralleles Recording-Double und
  keine erzwungene Test-Collection-Serialisierung einführen.
- **Warum:** Das Paket bleibt ein kohärentes Test-/Nachweispaket und
  verändert weder die bereits korrigierte Acquirer-Testisolation noch die
  Runtime-Default-Semantik.

## Out-of-Scope

- Keine eigenmächtige Neugestaltung der Produktions-Lock-, Rollback-,
  Cache-, Pointer-, Manifest- oder Refresh-Logik. Produktionsänderungen
  sind nur als unvermeidbarer interner Test-Seam zulässig.
- Kein Current-Reuse, Fetch, Refresh, Refresh-Intervall, neue
  Cache-Konfiguration, Health, Retention, Garbage Collection oder
  Invalidierung.
- Kein Provider-, Snapshot-, Registry-, Host-/MCP-Wiring-, Transport-,
  Native-/Process-, Credential- oder Assembly-Cache-Umbau.
- Keine transitive Referenzauflösung und keine EPIC-05-Arbeit.
- Keine Änderungen an `rules.json`, `Docs/`, `roadmap.md`, `tech-debt.md`
  oder nicht unmittelbar benannten Produktions-/Testbereichen.
- Kein globaler DRY-, Magic-Values- oder Dead-Code-Sweep. Scoped Audits
  dienen nur der Regressionserkennung für den Cache-Writer-/Read-back-
  Bereich.
- Keine Sleeps, `Task.Delay`, unbounded Retries, Netzwerkanfragen,
  externen Prozesse oder Stress-Tests.

## Abnahmekriterien

1. [ ] Der Lock-/Interleaving-Seam ist intern, per Aufruf isoliert und
   verwendet ausschließlich awaitbare `TaskCompletionSource`-/`SemaphoreSlim`-
   Signale; der Runtime-Default und das In-Process-Lock-Modell bleiben
   unverändert.
2. [ ] Der A/B-Test läuft mit und ohne vorherige Current-Generation. Er
   erzwingt: A pointer-published → B wartet → A gibt den Lease frei → B
   veröffentlicht seinen Pointer → erst danach darf A's fehlerhafte
   Cleanup-Phase im Legacy-Ablauf weiterlaufen. Der aktuelle Ablauf lässt
   B erst nach A's Cleanup weiter.
3. [ ] Nach dem korrigierten Lauf ist B erfolgreich und gültig Current, A's
   fehlgeschlagene Generation ist entfernt und eine vorherige Generation
   bleibt bei vorhandenem Vorzustand erhalten. Der Test wird bei der alten
   Lock-Freigabe reproduzierbar rot.
4. [ ] Manifest und Inventar weisen Oversize, ungültiges UTF-8, Trunkierung
   und kontrolliertes Wachstum beim bounded Read fail-closed zurück; die
   Fälle laufen über den gemeinsamen Reader und verwenden keine
   timingbasierte Synchronisierung.
5. [ ] Unbekannte und doppelte JSON-Felder auf Manifest-/Inventarebene sowie
   relevante doppelte/unbekannte Datei-Felder werden zurückgewiesen.
6. [ ] Inventar-Limits für Eintragszahl, deklarierte und kumulierte
   Gesamtbytes, einzelne Dateilänge, Pfadlänge und
   `fileCount`-Konsistenz sind jeweils direkt regressiv belegt.
7. [ ] Eine unveränderte gültige Generation, der
   `ExpectedSolutionPath`-Anker sowie bestehende Content-Hash-Growth-/
   Truncation-Fälle bleiben grün; keine Fail-closed-Assertion wird
   abgeschwächt.
8. [ ] Scoped MCP-/DRY-/MagicValues-/DeadCode-Nachweise und der fokussierte
   Testlauf sind dokumentiert; `dotnet build` sowie beide vollständigen
   `Category!=Stress`-Gates sind grün. Ein echter Win32-1314-Skip bleibt
   transparent dokumentiert.

## Kontextbudget

```yaml
context_budget:
  max_initial_files: 12
  max_read_first_files: 10
  read_first:
    - tasks/decompiled-assembly-analysis/step-027/step-review.md
    - tasks/decompiled-assembly-analysis/step-027/step-plan.md
    - tasks/decompiled-assembly-analysis/step-027/step-result.md
    - tasks/decompiled-assembly-analysis/codemap.md
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReadSupport.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs
  read_on_demand:
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheMetadataStorage.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheInventoryValidationParameters.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs
    - src/AiNetLinter.TestKit/TestTempDirectory.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs
  out_of_scope:
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs
    - src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs
    - src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs
    - src/AiNetLinter/Configuration/
    - src/AiNetLinter/Mcp/Registration/
    - src/AiNetLinter/Mcp/Daemon/
    - src/AiNetLinter/Mcp/Tools/
    - tasks/decompiled-assembly-analysis/roadmap.md
    - tasks/decompiled-assembly-analysis/tech-debt.md
    - EPIC-05 und transitive Referenzauflösung
    - Retention, GC, Health, Reuse, Refresh, Transport, Native und Process
```

`read_first` ist der verbindliche Einstieg und umfasst zehn Dateien. Der
Coder und der Kritiker laden keine vollständige Solution pauschal; weitere
Dateien werden nur bei einem konkreten Seam-/Fixturebedarf nachgeladen.

## Coder-Hand-off

Arbeite als neuer Coder-Agent mit frischem Kontext. Lies zuerst die zehn
`read_first`-Dateien und halte den initialen Kontext unter zwölf zentralen
Dateien. Verwende für jede C#-Semantik das AiNetLinter-MCP mit exakt
`projectRoot: C:/Daten/Entwicklung/Ralf/AiNetLinter`; verwende `rg` nur für
Text-, Literal- und Dateinamenfragen.

Sicherer Einstiegspunkt:

1. Prüfe per `get_feature_context` und `get_symbol_body` den aktuellen
   `PublishAsync`-/`FinalizePublish`-/`CacheKeyLockLease`-Pfad sowie
   `ReadBoundedText`, `ReadInventory`, `ValidateInventory` und die beiden
   Reader-Aufrufer. Ergänze `get_impact`, `find_references` und
   `get_test_context` für die tatsächlich betroffenen Testkonsumenten.
2. Ersetze den bloßen Callback nicht durch eine neue Produktions-
   Synchronisationsarchitektur. Implementiere nur den kleinsten internen
   Async-Seam mit der oben beschriebenen A→B→Lease-Release→B-Pointer-
   Reihenfolge. Die tatsächliche Cleanup-/Rollback-Reihenfolge bleibt
   generation-aware und unverändert. Prüfe insbesondere, dass ein Hook nach
   Lease-Freigabe wirklich erst dort wartet und nicht den Lock selbst
   blockiert.
3. Erweitere ausschließlich den vorhandenen partiellen
   `ExternalSourceRepositoryCacheWriterTests`-Testverbund. Nutze
   `SourceFixture`, `TestTempDirectory`, Vertragskonstanten und bestehende
   Assertions. Schreibe die JSON-Fälle mit expliziten Bytes/Fragmenten, damit
   unbekannte und doppelte Properties nicht von einem Objektmodell entfernt
   werden.
4. Führe den Growth-Fall zuerst mit den vorhandenen Dateioperationen aus.
   Wenn das Timing nicht deterministisch kontrollierbar ist, führe den
   per-Read-Aufruf begrenzten Stream-Seam ein; der Default bleibt der reale
   `FileStream`. Für keinen Test sind Sleep, Delay, Retry-Schleife oder
   statischer globaler Hook zulässig.
5. Prüfe nach jedem Matrixfall: `TryReadCurrent` liefert kein gültiges
   Resultat, die Diagnose bleibt kontrolliert, und der nächste Fall startet
   von einer frischen gültigen Generation. Der positive Baseline-Fall wird
   separat ausgeführt.
6. Führe danach die scoped MCP-/DRY-/MagicValues-/DeadCode-Prüfungen aus.
   Behebe nur neue, in diesem Test-/Seam-Scope entstandene Befunde; globale
   oder fachfremde Treffer werden als außerhalb des Scopes dokumentiert.
7. Führe fokussiert und abschließend aus:

   ```powershell
   dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests"
   dotnet build
   dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
   dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
   ```

   Stress bleibt unaufgerufen. Ein Win32-1314-Skip darf nur als echter
   Capability-Skip bestehen und muss transparent im Resultat stehen.
8. Aktualisiere `step-028/step-result.md` mit geänderten Dateien,
   Interleaving-/Matrix-Nachweisen, MCP-/Audit-Ausgaben und Testresultaten.
   Der Coder erstellt den deutschen Conventional Commit mit dem Suffix
   `[decompiled-assembly-analysis]` und pusht nicht. Der Step-Plan wird erst
   nach dem Coder-Gate und der Kritikerprüfung auf
   `done (pending audit)` gesetzt.

Invarianten für die Übergabe:

- Kein Test darf die A/B-Reihenfolge dem Scheduler überlassen; B's
  Pointer-Signal kommt aus dem kontrollierten Seam.
- Der korrigierte Ablauf hält Rollback und Cleanup vor der Lease-Freigabe;
  B's erfolgreicher Pointer wird nie von A verändert.
- Der Read-back bleibt strikt UTF-8, bounded, unknown-/duplicate-field-
  ablehnend, pfadgeschützt und fail-closed.
- Testseams sind intern und per Aufruf isoliert. Kein Cross-Process-Lock,
  keine öffentliche API und keine Konfigurationsänderung.
- Der nächste sichere Anschluss nach diesem Step ist der bestehende
  Reuse-/Refresh-Folgepaket; es wird in Step 028 nicht geplant oder geöffnet.

## Tests

### Fokussierte Iteration

- [ ] Der vorhandene A/B-Theory-Test läuft deterministisch mit und ohne
  vorheriger Current-Generation.
- [ ] Manifest- und Inventar-Matrix deckt jeweils Oversize, ungültiges
  UTF-8, Trunkierung, Wachstum sowie unbekannte und doppelte Felder ab.
- [ ] Inventar-Entry-, Byte-, Datei- und Pfadgrenzen sowie
  `fileCount`-Konsistenz werden direkt geprüft.
- [ ] Gültige Generation, Solution-Anker sowie Content-Hash-Growth/
  Truncation bleiben regressiv grün.

### Abschluss-Gates

- [ ] `dotnet build`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- [ ] Keine Stress-Tests; echte Umgebungs-Skips und verbleibende
  Audit-Befunde transparent dokumentieren.

## MCP-, DRY-, MagicValues- und DeadCode-Plan

### MCP

- **Vor dem Edit:** `get_feature_context`/`get_symbol_body` für Writer-
  Lifetime, Reader und bounded Helper; `get_impact`/`find_references` für
  Writer-Methoden und Testaufrufe; `get_test_context` für den Writer.
- **Nach dem Edit:** `get_impact` für den geänderten Seam-/Readerpfad,
  `get_violations` auf Cache-Produktions- und Testscope sowie ein gezieltes
  `safeguard` nur für den relevanten Bereich.
- Jeder Aufruf erhält den absoluten
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`. `rg` ersetzt keine
  semantische Symbol-, Referenz- oder Impact-Abfrage.

### DRY / Refactoring-Drift

- `find_duplicates` wird getrennt für
  `src/AiNetLinter/Mcp/Assemblies` (production) und
  `src/AiNetLinter.FastTests/Mcp/Assemblies` (tests) mit begrenztem
  Cache-/Writer-Scope ausgeführt.
- `SourceFixture`, `ReadCurrent`, `TestTempDirectory` und das bestehende
  Recording-Muster werden wiederverwendet. Nur eine durch die Matrix neu
  entstehende lokale Duplikation wird im selben Testpaket konsolidiert.
- Bestehende fachfremde oder globale Clone-/Structural-Funde werden nicht
  in den Step gezogen und nicht als neue Epics interpretiert.

### Magic Values

- `find_magic_values` läuft nur auf dem Cache-Writer-/Read-back-Scope mit
  `includeTests=true`. Bestehende Fixture-Präfixe und Vertragswerte werden
  nicht global umgebaut.
- Neue Tests verwenden die vorhandenen
  `Max...`-Konstanten. Wiederholte Testdaten-/JSON-Schlüssel werden nur in
  einem lokalen Helper oder einer bereits zuständigen Contract-Konstante
  gebündelt; keine neuen unbenannten Limits, Delays oder Retry-Werte.

### Dead Code

- `find_dead_code` läuft mit hoher Konfidenz auf dem geänderten Writer-/
  Read-back-Produktions- und Testscope. Unbenutzte Seam-Hooks, Overloads
  oder Matrix-Helper werden entfernt.
- Low-Confidence- und dynamische Serializer-/Testframework-Befunde werden
  nicht heuristisch gelöscht. Es entsteht kein globaler Dead-Code-Sweep.

## Risiken und Gegenmaßnahmen

- **Falsche Interleaving-Beobachtung:** Ein Hook an der falschen Stelle
  würde erneut nur Scheduling testen. Die B-Semaphore wird erst im Hook
  nach der realen Lease-Freigabe geöffnet; der Hook wartet auf B's explizite
  Pointer-TCS. Die Legacy-/aktuelle Reihenfolge ist dadurch unterscheidbar.
- **Deadlock im Test-Seam:** Kein synchrones Warten und kein Warten auf B,
  solange A den Lock hält. Die Semaphore wird erst nach A's tatsächlicher
  Freigabe geöffnet; die TCS signalisiert nur den bereits erreichten
  Pointer-Publish.
- **Async-Refactoring driftet in Produktion:** Der Seam ist intern,
  optional und no-op im Runtimepfad. Keine neue Lock-Abstraktion, kein
  Cross-Process-Vertrag und keine Änderung an Cache-Fehlersemantik.
- **Matrix prüft nur JSON-Fehler statt Limits:** Jeder Limitfall wird mit
  ansonsten strukturell parsebarem JSON aufgebaut; die Assertion benennt
  den erwarteten zuständigen Grenzpfad. Gültige Ausgangsbytes werden je Fall
  frisch erzeugt.
- **Große Oversize-Fixtures:** Es werden nur die notwendigen Metadaten-
  Überlaufbytes erzeugt; keine großen Content-Bäume, Prozesse oder
  parallelen Lasten. Der Test bleibt ein Unit-/Component-Nachweis.
- **Doppelte JSON-Felder verschwinden beim Serialisieren:** Die Fälle werden
  als rohe JSON-Fragmente/Bytes geschrieben, nicht über `JsonNode` oder ein
  Dictionary erzeugt.
- **Parallel laufende Tests:** Kein globaler Seam-Zustand und keine neue
  zwangsserialisierende Collection. Jeder Fall besitzt eigene
  `TestTempDirectory`- und Synchronisationsobjekte.
- **Umgebungs-Skip:** Der bestehende echte Reparse-/Symlink-Fall bleibt
  unverändert. Win32 1314 wird nur transparent als Capability-Skip
  dokumentiert; keine Simulation ersetzt den Sicherheitsnachweis.

## Definition of Done

- [ ] Alle zwei MAJOR-Findings aus `step-027/step-review.md` sind durch die
  acht Kriterien und reproduzierbare Tests geschlossen.
- [ ] Die A/B-Interleaving-Reihenfolge ist über TCS/Semaphore erzwungen und
  belegt die korrigierte Lock-/Rollback-Lifetime mit und ohne Previous
  Current.
- [ ] Manifest-/Inventar-Bounded-Reads sowie JSON-/UTF-8-/Growth-/Truncation-
  und Inventar-Limitfälle sind als Matrix dokumentiert und fail-closed
  regressiv getestet.
- [ ] Bestehende gültige Read-back-, Solution-Anker-, Hash- und
  Testtemp-Invarianten bleiben erhalten; kein Produktionsdefault wurde
  testbewusst verändert.
- [ ] Scoped MCP-, DRY-, MagicValues- und DeadCode-Nachweise sind
  vollständig und neue relevante Befunde sind behoben oder begründet.
- [ ] `dotnet build` sowie beide vollständigen Nicht-Stress-Gates sind
  grün; Stress wurde nicht ausgeführt.
- [ ] Der Coder hat `step-028/step-result.md` geschrieben und den
  Implementierungs-/Result-Commit mit deutschem Conventional Commit und
  Suffix `[decompiled-assembly-analysis]` ohne Push erstellt; danach folgt
  ein neuer Kritiker-Agent.
- [ ] Der Orchestrator setzt den Step erst nach dem Review auf `done`; die
  Korrekturzeiger bleiben `step-026 -> step-027 -> step-028`.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — nullable C#, kurze Methoden,
  Warnungsfreiheit und kein blockierender Task-Zugriff.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2` — keine neue
  dynamische/Reflection-basierte Ausführung oder repo-spezifische
  Produktionsinfrastruktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — xUnit-v3,
  `TestTempDirectory`, keine Ad-hoc-Tempverzeichnisse und keine unnötige
  Testsuite-Serialisierung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Zero-Warning-, DRY-,
  Magic-Values- und Dead-Code-Disposition.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — semantische MCP-Abfragen
  mit absolutem `projectRoot`; `rg` bleibt Textsuche.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` —
  Fix-Modus, neuer Sub-Agent, kein Roadmap-Edit und strikt serielle
  Coder-/Kritiker-Reihenfolge.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md` —
  flacher Korrektur-Step, Kontextbudget, Findings-only und keine
  Codeänderung durch den Planer.
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md` — Split-Gate
  für Fachvertrag, Schichten, Kriterien und initialen Leseumfang.

## Bekannte Ausnahmen

- Der echte Windows-Reparse-/Symlink-Test darf bei
  `ERROR_PRIVILEGE_NOT_HELD (1314)` transparent übersprungen werden. Das
  ist kein Ersatz für einen privilegierten Sicherheitslauf und wird nicht
  durch einen Capability-Fake umgangen.
- `roadmap.md` wird im Fix-Modus nicht geändert: Step 028 korrigiert nur
  Nachweise innerhalb des bereits geplanten EPIC-04-Publish-Vertrags.
- `tech-debt.md` wird nicht geändert, solange die scoped Audits keine neue,
  in diesem Vertrag liegende technische Schuld melden.

## Code-Skizze

```text
A: publish pointer
   -> signal A; start B; cancel A
B: acquire same-key lease
   -> wait on allowBPointer
A: finalize
   -> current implementation: rollback/cleanup
   -> release lease; allowBPointer.Release()
   -> await BPointerPublished
B: publish pointer; signal BPointerPublished
   -> current remains B; A-generation is not current and is cleaned
```

Die Skizze beschreibt ausschließlich die testbare Reihenfolge. Sie ist kein
Auftrag, die Produktions-Lock- oder Rollback-Semantik neu zu entwerfen.

## Notes

- Dies ist ein Fix-Modus-Step und korrigiert ausschließlich die zwei
  MAJOR-Findings des Step-027-Kritikers. Keine Mini-Sweeps und keine
  Vorwegnahme des Reuse-/Refresh-Folgepakets.
- Der Planer hat keinen Test, keinen Build und keine Produktionsänderung
  ausgeführt. Die MCP-/Auditwerte im JIT-Abschnitt sind read-only
  Ausgangsnachweise für den Coder.
- Der Coder und der Kritiker starten jeweils als neuer Sub-Agent. Der
  bestehende Agent wird nicht wiederverwendet.
