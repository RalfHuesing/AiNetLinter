# Linse 5 — Cache-/Snapshot-Lebenszeit, Generationen und Kapazität

## Reviewurteil

`approved` mit zwei bestätigten S2-Restbefunden und einem bedingten S2-Vertragsrisiko. Es wurde kein S0-Befund gefunden. Die Befunde sind für den Auditabschluss zu berücksichtigen, blockieren den analysierten Codepfad nach der Review-Regel jedoch nicht als S0/S1.

## Linse, Scope und Revision

- **Primärlinse:** Cache-/Snapshot-Lebenszeit, Generationen, Refresh, Wiederverwendung, Kapazitätsbudgets, Idle-TTL, Ownership, parallele Zugriffe und Fehler nach Teilphasen.
- **Geprüfter Scope:** `AssemblyAnalysisRegistry`, `AssemblyAnalysisEntry`, `AssemblyAnalysisSession`, `AssemblyDecompilationCache`, `AssemblyReferenceResolver`, `SourceSnapshotRegistry`, `ExternalResourceRegistry`, externe Repository-Cache-Reader/Writer/Refresh sowie Host-Komposition und die zugeordneten Unit-/Component-/Integrationstests.
- **Revision:** `65c194683597838f69fcb34492837de747d07cc3` bei der letzten MCP-/Codeprüfung; bei der abschließenden Dateiprüfung stand `HEAD` auf `c942350d7478ebb6c1f9aae7c6979bc9dc3d8090`. Die Revisionen dazwischen enthalten in diesem Auditfenster nur Audit-Artefakte; die produktiven Cache-/Snapshot-Quellen wurden in dieser Review nicht geändert.
- **Working Tree:** Parallel erzeugte Audit-Artefakte waren vorhanden und wurden nicht bearbeitet; außer dem ausdrücklich autorisierten Zielreport und der unten genannten Code-Map-Präzisierung erfolgten keine Änderungen an Source-, Test-, Konfigurations- oder Dokumentationsdateien.
- **Nicht geprüft:** vollständige Nicht-Stress-Abschlussläufe beider Testprojekte, Stress-/Lasttests, mehrstündige reale Datenträger-/Speicherläufe, eine source-backed Laufzeitprobe mit nutzbarer externer Quelle sowie privilegierte Reparse-Point-Tests.

Alle MCP-Zielparameter wurden redigiert dokumentiert: `targetType=project`, `targetPath=<absoluter Projektpfad redigiert>`. Externe URLs, lokale Installationspfade, Credentials und konkrete geschützte Beispieldaten erscheinen nicht in diesem Report.

## Executive Summary

### Befunde

1. **F-05-01 — Erfolgreiche Cache-Generationen werden nicht begrenzt bereinigt.** Beide persistenten Generation-Writer erzeugen bei erfolgreichen Änderungen eine neue Generation und lassen die vorherige Generation bestehen. Die vorhandenen Cleanup-Pfade behandeln nur fehlgeschlagene oder nicht veröffentlichte Generationen. Das erzeugt bei wiederholten Änderungen unbeschränkten Datenträgerverbrauch außerhalb der Resident-Ressourcenbudgets.
2. **F-05-02 — Prozessweite Cache-Key-Lock-Tabelle wächst monoton.** `LocalExternalSourceRepositoryCacheWriter` legt für jeden neuen Entry-Pfad ein `SemaphoreSlim` in einer statischen `ConcurrentDictionary` ab. Es gibt keinen `TryRemove`-, `Clear`- oder Dispose-Pfad für diese Einträge. Das ist ein bestätigter Langzeit-Ressourcenverbrauch im Daemon-Prozess.
3. **F-05-03 — Root-Byte-only-Reuse lässt Referenz-/Source-Änderungen aus dem Refresh-Trigger.** Die bestehende Generation wird bei identischem Root-SHA wiederverwendet; Referenzauflösung und Source-Auswahl finden nur bei der Entry-Erzeugung beziehungsweise bei einer neuen Fallback-Generation statt. Ob die konfigurierte Source-Refresh-Frische bereits resident gehaltene Assembly-Einträge zwingend invalidieren soll, ist im Konzept nicht explizit genug festgelegt. Daher wird dies als mittleres Vertragsrisiko und nicht als S1-Blocker eingeordnet.

### Bestätigte Erwartungen

- Gleichzeitige Erstzugriffe teilen sich eine Creation-Barriere; die Cancellation eines Wartenden beendet nicht die gemeinsame Creation.
- Änderungen der Root-Assembly-Bytes erzeugen eine neue in-memory Generation, während aktive alte Leases lesbar bleiben. Mtime-only-Änderungen dürfen wiederverwenden.
- Idle-/Kapazitäts-Eviction schützt aktive Assembly- und Snapshot-Leases; die Source-Snapshot-Reservation wird atomar in einen residenten Resource-Lease überführt.
- Cancellation oder Fehler nach einer Teilphase veröffentlicht keine partielle Fallback-Generation; ein vorhandener letzter guter Snapshot bleibt als `degraded` verfügbar.
- Cache-Pointer werden atomar beziehungsweise mit erwarteter Vorgängergeneration veröffentlicht; Readback-, Pfad- und Reparse-Schutz sind in den geprüften Pfaden sichtbar.

### Abdeckungsgrenzen

- Die beiden bestätigten Ressourcenbefunde wurden statisch durch MCP-Symbolkörper, Callgraph und ergänzende `rg`-Gegenprüfung belegt. Für die Lock-Tabelle wurde kein produktiver Langzeitprozess instrumentiert; die fehlende Entfernung ist jedoch direkt im Code sichtbar.
- Ein source-backed Refresh nach einer reinen Source-Revision- oder Dependency-Änderung wurde mangels nutzbarer externer Quelle nicht live ausgeführt. F-05-03 bleibt deshalb von der Vertragsauslegung und dieser Umgebung abhängig.
- Der privilegierte Reparse-Point-Test wurde übersprungen, weil die Capability im Testhost nicht verfügbar war; das ist kein Sicherheitsnachweis.

## Code-Map-Abgleich

Die Code-Map ist für diese Linse in ihren Einstiegspfaden und der Lebenszyklus-Kette korrekt: Analyse-/Cache-/Referenzpfade unter `src/AiNetLinter/Mcp/Assemblies/Analysis/`, externe Cache-/Refresh-/Snapshot-Pfade unter `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` sowie Assembly-Tool- und Registrierungspfade sind passend benannt. Die konkreten Cache-Typen wurden durch MCP verifiziert.

Eine konkrete Navigationsformulierung wurde in `tasks/decompiled-assembly-analysis-audit/code-map.md` ausschließlich von „vor jedem Session-Call“ zu „vor jedem Assembly-Tool-Handler“ präzisiert. Weitere Map-Zeilen waren für diese Linse nicht veraltet und wurden nicht verändert.

## Befund F-05-01 — Erfolgreiche Generationen werden nicht begrenzt bereinigt

- **Komponente:** `AssemblyDecompilationCache`; `LocalExternalSourceRepositoryCacheWriter`; zugehörige Storage-/Lifecycle-Cleanup-Pfade.
- **Schweregrad:** S2.
- **Umfang:** U3 — mehrere persistente Cache-Lebenszyklen und wiederholte Refreshes.
- **Beweissicherheit:** hoch.
- **Umgebungsabhängigkeit:** tritt bei jedem erfolgreichen Content-/Revisionwechsel auf; sichtbar relevant bei langlebigem CacheRoot und wiederholten Refreshes.

### Erwartetes Verhalten

Nach erfolgreicher Pointer-Umschaltung sollte eine nicht mehr benötigte Vorgängergeneration entweder sicher entfernt oder durch eine explizite, begrenzte Retention-/Budgetregel geschützt werden. Aktive Leases und eine laufende Rollback-/Race-Phase müssen dabei erhalten bleiben. Die im Settings-Vertrag genannten Resident-Ressourcenbudgets sollten nicht als Ersatz für die persistenten Generationen-Cleanupregeln dienen.

### Beobachtetes Verhalten

- `AssemblyDecompilationCache.Publish` erzeugt in `AssemblyDecompilationCache.cs:67-102` immer ein neues `generation-<id>`-Verzeichnis. `AssemblyCacheCleanup.DeleteDirectory` wird in `:102` nur ausgeführt, wenn `isPublished` **nicht** gesetzt wurde. Nach erfolgreicher Pointer-Publikation bleibt die vorherige Generation unangetastet.
- Der externe Writer erzeugt in `ExternalSourceRepositoryCacheWriter.cs:113-130` ebenfalls eine neue Generation. `PublishGeneration` veröffentlicht sie in `:130-190`; `FinalizePublishAsync` führt in `ExternalSourceRepositoryCacheWriterLifecycle.cs:18-30` Restore/Delete ausschließlich bei `!published` aus.
- `ExternalSourceRepositoryCacheStorage.TryDeleteGeneration` in `ExternalSourceRepositoryCacheStorage.cs:429-446` löscht nur den übergebenen, nicht aktuellen, sicheren Pfad. Eine erfolgreiche Publikation ruft diesen Pfad nicht für die verdrängte Vorgängergeneration auf. Eine eigenständige Generation-Sweep-, TTL- oder Datenträgerbudgetroutine wurde in den geprüften Cachepfaden nicht gefunden.
- Die Konfiguration beschreibt `MaxDiskBytes`/`MaxMemoryBytes` als Budgets der externen Assembly- und Source-Snapshot-Registries; sie begrenzt nicht die Zahl oder Gesamtgröße persistenter Generation-Verzeichnisse.

### Auswirkung

Jede erfolgreiche Änderung kann ein weiteres vollständiges Generation-Verzeichnis hinterlassen. Bei häufigen Assembly-Änderungen oder Source-Refreshes wächst der persistente Cache unabhängig von Idle-TTL und Resident-LRU. Im langlebigen MCP-Daemon kann das zu unnötiger Datenträgerbelegung und später zu Schreib-/Analysefehlern durch erschöpften Speicher führen. Ein unmittelbarer Verlust eines aktiven Snapshots wurde nicht beobachtet; deshalb S2 statt S0.

### Konkrete Reproduktion

1. Mit dem vorhandenen Writer-Test zwei erfolgreiche Publikationen desselben Cache-Keys mit unterschiedlichen Revisionen ausführen:

   ```powershell
   dotnet test src/AiNetLinter.FastTests --no-restore --filter "FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests.PublishAsync_SerializesSameKeyAndLeavesConsistentCurrent"
   ```

2. Der vorhandene Test bestätigt anschließend in `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs:328-351`, dass zwei `generation-*`-Verzeichnisse bestehen, obwohl nur eines als `current` gezeigt wird. Wiederholte erfolgreiche Publikationen erhöhen diese Anzahl weiter.
3. Für den Assembly-Cache ist der analoge Pfad `RefreshAsync_ChangesGenerationForChangedBytesAndKeepsOldLeaseReadable` in `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs:46-64`. Die Cleanup-Gegenprüfung zeigt, dass `AssemblyCacheCleanup.DeleteDirectory` nur von `AssemblyDecompilationCache.Publish` und dort nur in der Nicht-Publikations-Branch aufgerufen wird.

### Belege und Begründung

- **MCP-Symbolkörper:** `AssemblyDecompilationCache.Publish` (`M:...AssemblyDecompilationCache.Publish(...)`) zeigt die eindeutige Generationserzeugung, `isPublished` und die konditionale Cleanup-Branch. `LocalExternalSourceRepositoryCacheWriter.PublishGeneration` und `FinalizePublishAsync` zeigen die gleiche Retention bei erfolgreicher Veröffentlichung.
- **MCP-Symbolkörper:** `ExternalSourceRepositoryCacheStorage.TryDeleteGeneration` zeigt die Schutzbedingung `!IsCurrentGeneration(...)`; ohne separaten Aufrufer kann die alte, nicht aktuelle Generation nicht automatisch entfernt werden.
- **MCP-Testkontext:** Für `LocalExternalSourceRepositoryCacheWriter` wurden 16 zugeordnete Tests in zwei Testdateien gefunden; die vorhandenen Tests decken Pointer-, Readback-, Cancellation- und Serialisierungsverhalten ab, aber keinen erfolgreichen Retention-Sweep.
- **MCP-Testkontext:** `ExternalSourceRepositoryCacheStorage` besitzt keine direkte statische Testzuordnung.
- **MCP-Impact:** `AssemblyDecompilationCache.Publish` wird aus `AssemblyAnalysisSession.CreateAndInstallGenerationAsync` und damit aus dem Refresh-/Installpfad aufgerufen. `AcquireLockAsync` wird aus `PublishAsyncCore` aufgerufen.
- **Ergänzende PowerShell-Gegenprüfung:**

  ```powershell
  rg -n "AssemblyCacheCleanup|TryDeleteGeneration|GenerationDirectoryPrefix" src/AiNetLinter/Mcp/Assemblies/Analysis src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository
  ```

  Ergebnis: Assembly-Cleanup nur in `AssemblyDecompilationCache.cs:102`; externe Generation-Löschung nur über `ExternalSourceRepositoryCacheWriterLifecycle.cs:28` und die Storage-Hilfsmethode.

### Nicht umgesetzte Remediation-Hypothese

Eine sichere Sweep-Routine könnte nur unreferenzierte, nicht aktuelle Generationen nach einer definierten Grace-Phase entfernen, aktive Lease-/Rollback-Identitäten schützen und die Gesamtgröße beziehungsweise Anzahl pro Entry begrenzen. Diese Review implementiert das nicht.

## Befund F-05-02 — Prozessweite Cache-Key-Locks wachsen monoton

- **Komponente:** `LocalExternalSourceRepositoryCacheWriter` in `ExternalSourceRepositoryCacheWriter.cs`.
- **Schweregrad:** S2.
- **Umfang:** U3 — langlebiger Prozess, viele Repository-/Solution-Cache-Identitäten oder viele temporäre CacheRoots.
- **Beweissicherheit:** hoch für die fehlende Entfernung; mittel für die konkrete Produktionsgröße.
- **Umgebungsabhängigkeit:** relevant, wenn ein Writer-Prozess viele unterschiedliche Entry-Verzeichnisse über seine Lebensdauer bearbeitet.

### Erwartetes Verhalten

Ein per-Key-Synchronisationsobjekt sollte mindestens bis zum letzten Waiter beziehungsweise Publisher leben und danach aus der processweiten Tabelle entfernt und, soweit erforderlich, entsorgt werden. Die Synchronisation muss dabei trotz Removal-Race für bereits wartende Aufrufer gültig bleiben.

### Beobachtetes Verhalten

- `ExternalSourceRepositoryCacheWriter.cs:22` definiert `private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks`.
- `AcquireLockAsync` in `ExternalSourceRepositoryCacheWriter.cs:330-337` kanonisiert den Entry-Pfad und verwendet `Locks.GetOrAdd(lockKey, ...)`.
- Der Lease-Dispose in `ExternalSourceRepositoryCacheWriter.cs:441-455` gibt das Semaphore frei, entfernt aber den Dictionary-Eintrag nicht. Eine Suche über die Writer-Partialdateien findet keinen `TryRemove`, `Clear` oder sonstigen Lock-Tabellen-Cleanup.

### Auswirkung

Jeder neue kanonische Entry-Pfad hinterlässt dauerhaft einen Dictionary-Eintrag und ein `SemaphoreSlim`-Objekt. Die funktionale Serialisierung bleibt erhalten, aber die verwaltete Prozessbelegung wächst ohne konfiguriertes Limit. Bei wechselnden Source-Identitäten oder vielen isolierten CacheRoots ist dies ein langlebiger Ressourcenverbrauch.

### Konkrete Reproduktion

1. In einem einzigen Prozess `PublishAsync` wiederholt mit N unterschiedlichen, gültigen Cache-Entry-Identitäten aufrufen; jede Publikation muss bis `PublishAsyncCore` und `AcquireLockAsync` laufen.
2. Nach dem jeweiligen Aufruf bleibt der Eintrag für den kanonischen Entry-Pfad in `Locks`, weil nur `GetOrAdd` und `gate.Release()` ausgeführt werden. Bei N neuen Pfaden ist die statische Tabelle daher mindestens um N Einträge größer.
3. Die vorhandene Serialisierung lässt sich mit folgendem Test gegenprüfen; er ist grün, prüft aber nicht die Rückgewinnung der Lock-Objekte:

   ```powershell
   dotnet test src/AiNetLinter.FastTests --no-restore --filter "FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests.PublishAsync_SerializesSameKeyAndLeavesConsistentCurrent"
   ```

### Belege und Begründung

- **MCP-Symbolkörper:** `LocalExternalSourceRepositoryCacheWriter` zeigt die statische Tabelle; `LocalExternalSourceRepositoryCacheWriter.AcquireLockAsync` zeigt `GetOrAdd`; der innere `CacheKeyLockLease.Dispose` zeigt ausschließlich `gate.Release()`.
- **MCP-Impact:** `AcquireLockAsync` hat den direkten Aufrufer `LocalExternalSourceRepositoryCacheWriter.PublishAsyncCore` in `ExternalSourceRepositoryCacheWriter.cs:70` und wird von den öffentlichen/internalen Publish-Overloads erreicht.
- **MCP-Testkontext:** Die Writer-Tests enthalten einen Same-Key-Parallelitätsfall, aber keinen Test zur Entfernung eines ungenutzten Lock-Objekts. Der fehlende Cleanup-Pfad wurde zusätzlich mit folgender redigierter Textsuche bestätigt:

  ```powershell
  rg -n --glob 'ExternalSourceRepositoryCacheWriter*.cs' "Locks\.(TryRemove|Clear|Remove)|SemaphoreSlim|TryDeleteGeneration" src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository
  ```

  Relevante Treffer waren `:22` (`Locks`), `:335` (`GetOrAdd`), `:443` (`SemaphoreSlim`-Lease); kein Removal-Treffer.

### Nicht umgesetzte Remediation-Hypothese

Ein ref-counted Key-Lock-Holder mit compare-and-remove nach der letzten Lease könnte die Tabelle begrenzen. Die Removal-Operation müsste gegen neue Waiter atomar abgesichert werden; diese Review implementiert das nicht.

## Befund F-05-03 — Root-Byte-only-Reuse ignoriert mögliche Referenz-/Source-Refreshes

- **Komponente:** `AssemblyAnalysisSession`, `AssemblyAnalysisRegistry`, `AssemblyAnalysisEntry` und `AssemblyAnalysisRegistryEntryFactory`.
- **Schweregrad:** S2, als Vertragsrisiko.
- **Umfang:** U3 — mehrere Sessions, Referenzleases und Source-backed-Kontexte.
- **Beweissicherheit:** mittel.
- **Umgebungsabhängigkeit:** nur relevant, wenn eine Dependency, eine Source-Auswahl oder eine Source-Revision wechselt, während die Root-Assembly byte-identisch und der Registry-Eintrag resident bleibt.

### Erwartetes Verhalten

Wenn `RefreshIntervalMinutes` beziehungsweise Source-/Dependency-Generationen auch für bereits residente Assembly-Einträge gelten sollen, muss eine solche Änderung eine neue Analysegeneration oder eine gleichwertige Revalidierung auslösen. Aktive alte Leases müssen dabei wie bisher lesbar bleiben.

### Beobachtetes Verhalten

- `AssemblyAnalysisSession.RefreshCoreAsync` in `AssemblyAnalysisSession.cs:113-128` berechnet den Root-Fingerprint und kehrt bei `TryReuseCurrent` vor `RefreshGenerationAsync` zurück.
- `TryReuseCurrent` in `AssemblyAnalysisSession.cs:342-357` vergleicht ausschließlich `current.Fingerprint.Sha256` mit dem neuen Root-SHA und aktualisiert nur Fingerprint-/Zeitstatus.
- Eine neue Referenzauflösung erfolgt erst in `RefreshGenerationAsync` bei `AssemblyAnalysisSession.cs:131-136`; dort wird `referenceResolver.Resolve(...)` aufgerufen.
- Die Registry entscheidet in `AssemblyAnalysisEntry.Matches` (`AssemblyAnalysisEntry.cs:71-72`) ebenfalls ausschließlich anhand von `Context.Origin.ContentHash`. `AssemblyAnalysisRegistry.TryLeaseEntry` (`AssemblyAnalysisRegistry.cs:321-353`) gibt bei gleichem Root-Hash die vorhandene Entry frei, ohne `CreateEntry` erneut aufzurufen.
- Source-Auswahl und Source-Selection-Lifetime werden in `AssemblyAnalysisRegistryEntryFactory.CreateAsync` beziehungsweise `TryCreateSourceEntryAsync` (`AssemblyAnalysisRegistryEntryFactory.cs:39-150`) nur beim Erzeugen einer neuen Registry-Entry angefordert.
- Der Dispatcher ruft `AssemblyAnalysisLease.ExpandReferencesAsync` vor dem Assembly-Tool-Handler auf (`AnalysisToolCall.cs:161-171`), aber die Expansion arbeitet auf den in der bereits residenten Entry enthaltenen Referenzdaten und ersetzt die Root-Entry nicht.

### Auswirkung

Eine neue Source-Revision oder eine byte-geänderte Dependency mit unverändertem Root kann an einem residenten Root-Kontext vorbeigehen. Der Agent kann dann weiterhin einen alten Source-/Referenz-Snapshot und eine alte Compilation verwenden, bis der Root ebenfalls byte-geändert, der Eintrag per Idle-TTL evicted oder der Host beendet wird. Das ist kein bestätigter Fehler, falls die Refresh-Konfiguration bewusst nur die persistente Repository-Akquisition und nicht resident gehaltene Assembly-Entries meint; diese Abgrenzung ist aber nicht explizit.

### Konkrete Reproduktion

1. Einen Root mit lokaler Dependency oder Source-Mapping einmal über `AssemblyAnalysisRegistry.LeaseAsync` aufbauen; dabei eine Source-/Referenzgeneration R resident halten.
2. Nur die Dependency-Bytes beziehungsweise die Source-Revision auf R2 ändern, den Root byte-identisch lassen und den Root vor Ablauf der Assembly-Idle-TTL erneut leasen.
3. Erwartet bei einer end-to-end Freshness-Semantik: neuer Generation-/Revisionstand oder explizite Revalidierungsdiagnose. Beobachtet laut Code: gleicher Root-SHA genügt für `AssemblyAnalysisEntry.Matches` beziehungsweise `TryReuseCurrent`; der Erzeugungs-/Resolverpfad wird nicht erneut durchlaufen.
4. Die Reproduktion wurde nicht mit einer echten Source-backed Quelle ausgeführt; die Aussage bleibt deshalb umgebungs- und vertragsabhängig.

### Belege und Begründung

- **MCP-Symbolkörper:** `AssemblyAnalysisSession.RefreshCoreAsync`, `TryReuseCurrent`, `RefreshGenerationAsync`, `AssemblyAnalysisEntry.Matches`, `AssemblyAnalysisRegistry.TryLeaseEntry` und `AssemblyAnalysisRegistryEntryFactory.CreateAsync` wurden mit `targetType=project` und redigiertem absolutem `targetPath` gelesen.
- **MCP-Impact:** `TryReuseCurrent` wird aus `RefreshCoreAsync` und `RefreshAsync` erreicht; `AssemblyAnalysisRegistry.TryLeaseEntry` ist Teil des Registry-Leasepfads.
- **MCP-Testkontext:** `AssemblyAnalysisSessionTests` deckt Mtime-Reuse, Root-Byte-Generationwechsel, Cancellation und Last-good-Degradierung ab. Ein Test „Dependency-/Source-Generation ändert sich bei identischem Root-SHA“ wurde nicht gefunden.
- **Gegenbeleg:** Der Dispatcher erweitert Referenzen vor jedem Assembly-Tool-Handler. Das schützt die Handler vor fehlender Expansion, invalidiert aber nicht die bereits residenten Root-Referenzdaten; deshalb mittlere statt hoher Beweissicherheit für den End-to-end-Vertragsbruch.

### Nicht umgesetzte Remediation-Hypothese

Eine mögliche Lösung wäre ein zusammengesetzter Reuse-Schlüssel aus Root-Fingerprint, Dependency-/Source-Generation und Mapping-Identität oder eine explizite Revalidierung vor Reuse. Die Generationsersetzung müsste weiterhin Lease- und Capacity-sicher erfolgen; diese Review implementiert das nicht.

## Bestätigte Lebenszyklus-Erwartungen und Gegenprüfungen

Die folgenden Bereiche wurden geprüft und in der vorhandenen Implementierung durch MCP und Tests bestätigt; sie sind keine zusätzlichen Befunde:

- `AssemblyAnalysisRegistry`: Cancellation-isolierte Creation, gemeinsame Erstzugriffsbarriere, Retry bei geänderten Bytes, monotone Generationen, LRU-/TTL-Eviction, Kapazitäts-Retirement mit Revalidierung, idempotentes Dispose und Drain aktiver Leases.
- `AssemblyAnalysisSession`: Gate-serialisierter Refresh, Mtime-Reuse, neue Generation nach geänderten Bytes, alte Snapshot-Lesbarkeit während aktiver Leases, Abbruch ohne partielle Cache-Publikation und `degraded`-Last-good-Semantik.
- `SourceSnapshotRegistry`: Identitäts-Deduplication, getrennte Revision/Solution-Komponenten, atomare Reservation-Promotion, unabhängiges Ressourcenbudget, aktive Lease-Erhaltung bei Eviction und terminales, idempotentes Dispose.
- External-Source-Cache: Readback-/Manifest-/Inventory-Prüfung, erwartete Current-Generation bei Publikation, Cleanup nicht veröffentlichter Generationen, Cancellation-Rollback und Same-Key-Serialisierung. Diese positiven Eigenschaften heben F-05-01 und F-05-02 nicht auf, weil sie erfolgreiche Retention beziehungsweise die Lock-Tabelle nicht begrenzen.
- Host-Komposition: Assembly- und Source-Resource-Registries sind getrennt. Dadurch sind die konfigurierten Limits pro Registry sichtbar; ein aggregiertes hostweites Gesamtbudget wurde in dieser Review nicht als Fehler gewertet, weil der Vertrag die Aggregation nicht eindeutig festlegt.

## MCP-, Test- und PowerShell-Belege

### MCP-Überblick

- `get_file_tree`: vollständiger Scan des Projekts; die strukturierten Felder `completeness.scanCompleted=true` und `completeness.truncated=false` bestätigten die Vollständigkeit für die relevanten Analysis-/ExternalSource-/Testverzeichnisse.
- `get_index_scope`: C#-Symbolgraph für 845 Dateien vollständig abgedeckt; die semantischen Abfragen blieben im Projektziel.
- `get_feature_context`: Kernsymbole und zugeordnete Testmethoden wurden für `AssemblyAnalysisRegistry`, `AssemblyDecompilationCache`, `AssemblyAnalysisSession`, `SourceSnapshotRegistry`, `ExternalSourceRepositoryCacheStorage`, Writer und Host-Komposition abgefragt.
- `get_test_context`: Die strukturierten Felder `totalMatchingTests`, `totalTestFiles` und `isTruncated=false` ergaben 18 Registry-Tests, 13 Session-Tests und 16 Writer-Tests; für `ExternalSourceRepositoryCacheStorage` gab es `totalMatchingTests=0` und keine direkte Zuordnung.
- `get_impact`: `structuredContent.callSites` und `structuredContent.completeness` lieferten die Aufruferpfade für `TryReuseCurrent`, `AssemblyDecompilationCache.Publish` und `AcquireLockAsync` mit Tiefe 2; `truncatedByMaxResults=false` und `truncatedByNodeLimit=false`.
- `get_violations`: `structuredContent.violations=[]`, also 0 direkte Linter-Verstöße in 84 Dateien im Scope `src/AiNetLinter/Mcp/Assemblies`.
- `safeguard`: `structuredContent.passed=true`, Score 8,80/10; `totalViolationCount=1`. Der einzige Top-Verstoß betraf den Response-Builder außerhalb des Cache-/Snapshot-Kernpfads und wurde nicht als dieser Linse zugeordnet.

### Ausgeführte Tests

```powershell
dotnet test src/AiNetLinter.FastTests --no-restore --filter "FullyQualifiedName~AssemblyAnalysisSessionTests|FullyQualifiedName~AssemblyAnalysisRegistryTests|FullyQualifiedName~AssemblyAnalysisRegistryRetirementRaceTests|FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests|FullyQualifiedName~ExternalSourceRepositoryCacheRefreshTests|FullyQualifiedName~ExternalSourceRepositoryCacheReuseTests|FullyQualifiedName~SourceSnapshotRegistryTests|FullyQualifiedName~AssemblyAnalysisHostCompositionTests"
```

Ergebnis: 113 insgesamt, 112 erfolgreich, 0 fehlgeschlagen, 1 übersprungen. Der Skip betraf ausschließlich den privilegierten Reparse-Point-Test wegen fehlender Symlink-Capability.

```powershell
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~ExternalSourceSnapshotMaterializerTests"
```

Ergebnis: 6 insgesamt, 6 erfolgreich, 0 fehlgeschlagen.

Ein buildender Lauf desselben Integrationstestfilters konnte wegen einer von einer bereits laufenden lokalen Analyseprozess-Instanz gesperrten gemeinsamen DLL nicht abgeschlossen werden. Es wurde kein Prozess beendet. Vollständige Nicht-Stress-Läufe und Stressläufe wurden nicht ausgeführt.

## Mögliche Cross-Lens-Überschneidungen

| Andere Linse | Überschneidung | Abgrenzung |
|---|---|---|
| Linse 4 — Checkout-/Ownership-Sicherheit | F-05-01 berührt die sichere Löschung alter Generationen; Reparse-/Pfadschutz muss beim Sweep erhalten bleiben. | Dieser Report bewertet Retention und Budget, nicht die vollständige Checkout-Attestation. |
| Linse 6 — MCP-/Wire-/Komposition | F-05-03 kann den sichtbaren Generation-/Statusvertrag und die getrennten Resource-Registries beeinflussen. | Dieser Report bewertet Reuse und Lebensdauer, nicht Schema-/Payload-Vollständigkeit. |
| Linse 8 — Tests/Dokumentation | F-05-01/F-05-02 haben keine direkten Retention-/Lock-Removal-Assertions; F-05-03 hat keinen Dependency-/Source-Invalidationstest. | Dieser Report meldet die Abdeckungslücke, konsolidiert aber keine Testmatrix. |
| Linsen 2/3 — Source-/Transport-Refresh | F-05-03 hängt von Source-Revision und Refresh-Ergebnis ab. | Keine externe Transport- oder Credential-Bewertung in diesem Report. |

## Coverage-/Limitations-Tabelle

| Bereich | Status | Beleg/Grenze |
|---|---|---|
| Assembly-Registry, Leases, Generationen, Capacity, Idle-TTL | geprüft | MCP-Symbolkörper, `get_impact`, Registry-/Retirement-Race-Tests grün |
| Session-Refresh, Mtime-/Byte-Reuse, Cancellation, Last-good | geprüft | MCP-Symbolkörper, `AssemblyAnalysisSessionTests` grün |
| Source-Snapshot-Ownership, Reservation, Dedup, Eviction | geprüft | MCP-Symbolkörper, `SourceSnapshotRegistryTests` und Materializer-Integration grün |
| Persistenter Assembly-/Source-Cache-Pointer und Readback | geprüft | MCP-Reader/Writer/Storage; Writer-Tests grün |
| Erfolgreiche Generation-Retention über viele Refreshes | Befund statisch bestätigt, kein Langzeitstress | F-05-01; vorhandener Same-Key-Test zeigt zwei Generationen nach zwei Erfolgen |
| Prozessweite Lock-Tabellen-Reclamation | Befund statisch bestätigt, kein instrumentierter Daemonlauf | F-05-02; `GetOrAdd` ohne Removal/Dispose |
| Source-/Dependency-Invalidierung bei identischem Root-SHA | bedingtes Vertragsrisiko | F-05-03; keine echte source-backed Probe und kein direkter Test |
| Reparse-Point-/Symlink-Capability | teilweise | 1 Test übersprungen; Skip ist kein Sicherheitsnachweis |
| Integration-Materialisierung | geprüft | 6/6 mit `--no-build` erfolgreich; buildender Lauf durch DLL-Lock blockiert |
| Vollständige Nicht-Stress-Suite | nicht geprüft | Abschluss-Gate außerhalb dieses gezielten Reviewer-Laufs |
| Stress-/Mehrstunden-/reale OS-Ressourcenlast | nicht geprüft | keine belastbare Aussage zu realem Datenträgerdruck oder Prozesswachstum |

Keine weiteren bestätigten Befunde innerhalb der Primärlinse wurden aus nicht reproduzierbaren Vermutungen abgeleitet.
