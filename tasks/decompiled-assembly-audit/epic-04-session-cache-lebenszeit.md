# Epic 4 — Session-, Cache- und Lebenszeit-Audit

## Evidence-/Scope

Geprüft wurden ausschließlich `AssemblyAnalysisRegistry`,
`AssemblyAnalysisSession` und die direkt dafür zuständigen Entry-, Fingerprint-,
Cache-, Eviction-, Resource- und Health-Komponenten. Im Fokus standen
Generation und Content-Identität, Cache-Reuse, Refresh bei Dateiänderung,
Snapshot-/Registry-Leases, Cancellation, LRU/TTL, Disposal, Parallelität,
Registry-Isolation, Ressourcen-/Prozessbudgets und die Health-/Session-Sicht.

Nicht bewertet wurden fachliche Navigation, Referenzauflösungsdetails und die
bereits in der Code-Map dokumentierten Befunde zur Dokumentmetadaten- oder
Body-Aufbereitung, außer soweit sie die Session- oder Cache-Grenze berühren.
Die lokale Prüffall-Matrix wurde nur über ihre opaken Labels und als Quelle für
read-only MCP-Zielpfade verwendet. Im Bericht erscheinen keine externen
Identitäten, Pfade, URLs, Hashwerte oder dekompilierten Inhalte.

Ergebnisbild: Die normalen Reuse-/Lease-/Retirement-Invarianten sind im Code
klar erkennbar und durch statische Tests abgedeckt. Es gibt jedoch fünf
belastbare Fehlerbefunde, drei Optimierungen und drei Missing Features. Keine
Kategorie ist leer.

## Befundübersicht

| Kategorie | ID | Priorität | Größe | Vertrauen |
|---|---|---:|---:|---:|
| Bug | E4-BUG-01 | P1 | M | hoch |
| Bug | E4-BUG-02 | P1 | L | hoch |
| Bug | E4-BUG-03 | P1 | S | hoch |
| Bug | E4-BUG-04 | P2 | M | hoch |
| Bug | E4-BUG-05 | P2 | M | mittel-hoch |
| Optimierung | E4-OPT-01 | P2 | S | hoch |
| Optimierung | E4-OPT-02 | P2 | S | hoch |
| Optimierung | E4-OPT-03 | P2 | S | mittel-hoch |
| Missing Feature | E4-MF-01 | P2 | L | hoch |
| Missing Feature | E4-MF-02 | P2 | L | hoch |
| Missing Feature | E4-MF-03 | P2 | L | mittel |

## Findings — Bug

### E4-BUG-01 — Refresh unterschlägt geänderte Ressourcen-Dimensionen

- **Priorität / Größe / Vertrauen:** P1 / M / hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:250-285`
  (`TryLeaseEntry`, Refresh und Retirement), `:354-378` (`CreateEntry`);
  `AssemblyAnalysisResourceBudget.cs:110-153` (`Acquire`, `CreateRequest`);
  `ExternalResourceRegistry.cs:77-110` (`TryAcquireCore`).
- **MCP-Parameter und Ergebnis:** `get_symbol_body` mit aktuellem
  `targetType=project` und absolutem Projekt-`targetPath` (im Bericht
  redigiert), auf `TryLeaseEntry`, `CreateEntry`, `CreateRequest` und
  `TryAcquireCore`; alle Bodies `available`, vollständig, nicht trunkiert.
  `get_feature_context` für `ExternalResourceRegistry` lieferte vollständige
  Testzuordnung ohne Datei-Violation.
- **Befund:** Ein Content-Mismatch erzeugt für denselben kanonischen Pfad einen
  neuen Registry-Entry, während der alte Entry bis zum Lease-Drain erhalten
  bleibt. Die Ressourcenbuchung verwendet aber den Pfad als alleinige Identity.
  Bei einer bereits residenten Identity wird die neue Disk-/Memory-Anforderung
  nicht übernommen; es werden nur Lease-Zähler und Last-Use aktualisiert.
- **Auswirkung:** Ändert sich die Dateigröße zwischen zwei Generationen, wird
  das konfigurierte Disk-/Memory-Budget für die Überlappung nicht verlässlich
  repräsentiert. Ein größerer neuer Inhalt kann mit den alten, kleineren
  Dimensionen akzeptiert werden; aktive alte Leases bleiben dabei geschützt.
- **Empfehlung:** Die Resource-Identity an die Inhaltsgeneration koppeln oder
  die Dimensionen nur über eine atomare, referenzgezählte Übergabe aktualisieren.
  Während alter und neuer Generation überlappen, müssen beide Kosten entweder
  separat oder nachweislich konservativ verbucht werden. Ergänzend braucht es
  einen Test für denselben Pfad mit geänderter Größe und aktivem Alt-Lease.
- **Abgrenzung / Unsicherheit:** Die Identity-Deduplizierung für unveränderte
  Ressourcen ist durch `ExternalResourceRegistryTests` ausdrücklich abgedeckt
  und bleibt sinnvoll. Nicht ausgeführt wurde ein Laufzeittest für den
  Same-Path-Size-Change; der Befund ist aus den vollständigen Methodenkörpern
  abgeleitet.

### E4-BUG-02 — Kein Stabilitätscheck nach Read/Decompilation vor Commit

- **Priorität / Größe / Vertrauen:** P1 / L / hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyFingerprint.cs:11-28,62-66`
  (`Create`, `Canonicalize`),
  `AssemblyAnalysisSession.cs:113-130` (`RefreshCoreAsync`),
  `:132-196` (`RefreshGenerationAsync`, `BuildFreshGenerationAsync`),
  `:197-231` (`CreateAndInstallGenerationAsync`),
  `AssemblyDecompilationCache.cs:68-103` (`Publish`).
- **MCP-Parameter und Ergebnis:** `get_symbol_body` mit
  `targetType=project`, absolutem redigiertem Projekt-`targetPath` und den
  genannten Symbolen; Bodies vollständig und `available`. Die Session-
  Testzuordnung war vollständig (13 Tests), aber ohne Test für eine Änderung
  während Read/Decompilation.
- **Befund:** Der Fingerprint wird vor Referenzauflösung und Cache-/Decompilation
  einmal gelesen. Fresh-Build, Workspace-Erzeugung, synchroner Cache-Publish
  und Generation-Install verwenden danach weiterhin diese Eingabe. Ein zweiter
  Fingerprint direkt vor Publish/Install fehlt; beim Cache-Hit fehlt derselbe
  Commit-Grenzcheck ebenfalls.
- **Auswirkung:** Ändert sich die Datei während des langen Analysefensters,
  können Snapshot und Cache-Datensatz den ursprünglichen Content-Hash tragen,
  obwohl die gelesenen Bytes nicht mehr zu dieser Identität gehören. Der nächste
  Request erkennt die Änderung erst verspätet; die gerade veröffentlichte
  Generation war zwischenzeitlich fachlich inkonsistent.
- **Empfehlung:** Nach Read/Decompilation und vor Cache-Publish/Install erneut
  fingerprinten. Bei Abweichung den uncommitteten Workspace verwerfen, eine
  partielle Cache-Generation nicht veröffentlichen und innerhalb des bereits
  vorhandenen Retry-Limits kontrolliert neu beginnen. Dasselbe gilt für den
  Cache-Hit-Pfad.
- **Abgrenzung / Unsicherheit:** Der vorhandene Registry-Retry schützt
  Änderungen, die zwischen zwei äußeren Fingerprint-Abfragen sichtbar werden;
  er schließt die In-Flight-Lücke nicht. Die Dateiänderung während der
  Decompilation wurde wegen des Read-only-Vertrags nicht provoziert.

### E4-BUG-03 — Retirement-Fehler werden trotz Disposal-Aggregation verschluckt

- **Priorität / Größe / Vertrauen:** P1 / S / hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:380-393`
  (`RetireEntryAsync`), `:332-350` (`DisposeAsync`);
  `AssemblyAnalysisRegistryDisposal.cs:160-178`
  (`DisposeRetiredEntriesAsync`, `DisposeEntriesAsync`).
- **MCP-Parameter und Ergebnis:** `get_symbol_body` mit
  `targetType=project`, absolutem redigiertem Projekt-`targetPath` und den
  genannten Symbolen; Bodies vollständig, nicht trunkiert. Die direkte
  Entry-Testzuordnung enthält einen Cleanup-Fehlernachweis, aber keinen
  Retirement-Fehlerpfad.
- **Befund:** `RetireEntryAsync` fängt jede Exception aus Entry-Disposal und
  protokolliert sie nur. Dadurch beendet sich der Retirement-Task erfolgreich;
  die nachgelagerte Aggregation in `DisposeAsync` kann den Fehler nicht mehr
  einsammeln. Pending Entries werden im separaten Pfad dagegen aggregiert.
- **Auswirkung:** Ressourcen-, Server- oder Lifetime-Cleanup kann bei LRU-/TTL-
  Retirement fehlschlagen, ohne dass der Registry-Dispose oder ein kontrollierter
  Health-/Fehlerpfad dies meldet. Wiederholte Fehler können Retention oder
  Ressourcenfreigabe unbemerkt beeinträchtigen.
- **Empfehlung:** Retirement-Tasks dürfen Cleanup-Fehler nach zentraler
  Kontextanreicherung weiterreichen. Die Registry muss sie wie Pending-Entry-
  Fehler aggregieren oder einen expliziten, sichtbaren Quarantäne-/Degraded-
  Zustand führen; Logging bleibt zusätzlich möglich.
- **Abgrenzung / Unsicherheit:** Erfolgreiches Retirement und idempotentes
  Entry-Disposal sind nicht betroffen. Der konkrete Fehler wurde nicht injiziert;
  die Kontrollflusswirkung ist aus den vollständigen Bodies direkt belegt.

### E4-BUG-04 — Direkte Session-Disposal-Race mit laufendem Refresh

- **Priorität / Größe / Vertrauen:** P2 / M / hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:71-83`
  (`RefreshAsync`) und `:85-112` (`Dispose`, `DisposeAsync`).
- **MCP-Parameter und Ergebnis:** `get_symbol_body` mit
  `targetType=project`, absolutem redigiertem Projekt-`targetPath` und
  `RefreshAsync`, `Dispose`, `DisposeAsync`; Bodies vollständig,
  nicht trunkiert. `get_feature_context` meldete die Session vollständig,
  ohne offene Violations.
- **Befund:** `RefreshAsync` serialisiert Refreshes über `refreshGate`, aber
  `Dispose` setzt den Zustand und entsorgt das Gate unabhängig von wartenden oder
  laufenden Refreshes. Ein laufender Refresh kann im `finally` noch `Release()`
  auf dem bereits entsorgten Gate ausführen; ein wartender Refresh kann schon
  beim `WaitAsync` eine `ObjectDisposedException` erhalten.
- **Auswirkung:** Die interne Session-Lebenszeit kann statt eines kontrollierten
  Failed-/Cancellation-Ergebnisses eine ungeplante Disposal-Exception liefern.
  Der erzeugte Snapshot wird zwar an der Installationsgrenze erkannt, aber der
  Aufrufer erhält unter der Race-Bedingung keine stabile Vertragsantwort.
- **Empfehlung:** Disposal zweistufig machen: zuerst geschlossen markieren und
  laufende Refreshes drainen oder abbrechen, erst danach das Gate entsorgen.
  Alternativ ein nicht entsorgtes Gate mit explizitem Shutdown-Zustand verwenden
  und den Release-Pfad robust gegen spätes Disposal machen.
- **Abgrenzung / Unsicherheit:** Der Registry-Pfad wartet Creation-Aufgaben vor
  dem Entry-Disposal und reduziert dadurch diese Race-Wahrscheinlichkeit. Der
  direkte interne Session-Vertrag ist trotzdem nicht synchronisiert; ein
  paralleler Dispose-/Refresh-Test fehlt.

### E4-BUG-05 — Cancellation hat keinen Commit-Grenzpunkt

- **Priorität / Größe / Vertrauen:** P2 / M / mittel-hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:167-196`
  (`BuildFreshGenerationAsync`) und `:197-231`
  (`CreateAndInstallGenerationAsync`); `:233-267` (`CreateSnapshotAsync`).
- **MCP-Parameter und Ergebnis:** `get_symbol_body` mit
  `targetType=project`, absolutem redigiertem Projekt-`targetPath` und den
  genannten Symbolen; Bodies vollständig und nicht trunkiert. Die statische
  Testzuordnung bestätigt Cancellation während des Aufbaus, nicht die späte
  Commit-Grenze.
- **Befund:** Der CancellationToken wird an Decompilation und Workspace-Load
  weitergereicht. Nach erfolgreichem Snapshot-Load folgt jedoch kein
  `ThrowIfCancellationRequested` vor synchronem `cache.Publish` und
  `InstallGeneration`. Cancellation genau in diesem Zeitfenster wird ignoriert.
- **Auswirkung:** Ein bereits abgebrochener Aufrufer kann noch eine Generation
  veröffentlichen und installieren und anschließend Erfolg statt Cancellation
  sehen. Das ist besonders relevant, weil Publish synchron und nicht
  cancellable ist.
- **Empfehlung:** Einen expliziten Cancellation-Commitpunkt definieren und vor
  Publish/Install prüfen. Nach dem Commit muss der Vertrag eindeutig festlegen,
  dass ein spätes Cancellation-Signal den erfolgreichen Commit nicht mehr
  zurückrollt; davor müssen Cache- und Snapshot-Ressourcen rollback-sicher sein.
- **Abgrenzung / Unsicherheit:** Cancellation während Decompilation/Workspace
  wird korrekt behandelt und durch vorhandene Tests belegt. Ob späte
  Cancellation laut Produktvertrag noch abbrechen muss, ist nicht dokumentiert;
  deshalb P2 und mittel-hohes Vertrauen statt P1.

## Findings — Optimierung

### E4-OPT-01 — Abgeschlossene Retirement-Tasks bleiben unbegrenzt referenziert

- **Priorität / Größe / Vertrauen:** P2 / S / hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `AssemblyAnalysisRegistry.cs:31` (`retiredEntries`), `:278` und `:343-348`;
  `AssemblyAnalysisRegistryEvictionCoordinator.cs:126-145`.
- **MCP-Parameter und Ergebnis:** `get_symbol_body` und
  `get_class_structure` mit `targetType=project` und absolutem redigiertem
  Projekt-`targetPath`; vollständige Bodies/Struktur, nicht trunkiert.
- **Befund:** Jede Retirement-Task wird der Liste hinzugefügt. Die Liste wird
  nur beim Registry-Dispose kopiert und geleert; abgeschlossene Tasks werden im
  Normalbetrieb nicht entfernt.
- **Auswirkung:** Häufige Dateiwechsel oder viele TTL-/LRU-Retirements erhöhen
  dauerhaft Referenz- und Iterationskosten, obwohl die Entries selbst bereits
  freigegeben sind.
- **Empfehlung:** Abgeschlossene Tasks unter demselben `gate` regelmäßig
  herausfiltern und nur noch laufende Retirements bis zum Dispose halten.
- **Abgrenzung:** Pending Retirements müssen bis zum Abschluss beobachtbar
  bleiben; die Optimierung darf weder Lease-Drain noch Fehleraggregation
  verkürzen.

### E4-OPT-02 — Generation-Counter wächst pro gesehenem Pfad ohne Begrenzung

- **Priorität / Größe / Vertrauen:** P2 / S / hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `AssemblyAnalysisRegistry.cs:30` (`nextGenerations`) und `:354-359`
  (`CreateEntry`).
- **MCP-Parameter und Ergebnis:** `get_symbol_body` mit
  `targetType=project`, absolutem redigiertem Projekt-`targetPath` und
  `CreateEntry`; Body vollständig, nicht trunkiert. Die Generation-/ABA-Tests
  sind in der vollständigen Registry-Testzuordnung sichtbar.
- **Befund:** Der Counter bleibt für jeden jemals gesehenen kanonischen Pfad im
  Dictionary, auch wenn der Entry evicted und der letzte Lease beendet wurde.
  Das schützt die Monotonie, besitzt aber keine Lebenszeitgrenze.
- **Auswirkung:** Viele wechselnde Assembly-Pfade erzeugen kleine, aber
  dauerhafte Registry-Metadaten und machen die residenten Ressourcenbudgets
  unvollständig als Maß für den tatsächlichen Registry-Footprint.
- **Empfehlung:** Monotonie und ABA-Schutz in eine begrenzte Generation-/Epoch-
  Struktur oder einen nicht wiederverwendbaren, persistierbaren Identitätsanteil
  überführen. Ein einfaches Zurücksetzen darf alte Symbol-IDs nicht wieder
  gültig machen.
- **Abgrenzung:** Die aktuelle Map ist absichtlich für ABA-Sicherheit nützlich;
  das ist eine Speicher-/Lebenszeitoptimierung, kein Reuse-Fehler.

### E4-OPT-03 — Registry- und Cache-Identity behandeln Pfad-Case uneinheitlich

- **Priorität / Größe / Vertrauen:** P2 / S / mittel-hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `AssemblyAnalysisRegistry.cs:29` verwendet eine case-insensitive Entry-Map;
  `AssemblyFingerprint.cs:62-66` normalisiert nur den vollständigen Pfad;
  `AssemblyAnalysisSessionModels.cs:78-91` baut den Cache-Key aus dem rohen
  kanonischen Pfad; `AssemblyDecompilationCache.cs:32-36` hasht diesen Key.
- **MCP-Parameter und Ergebnis:** `get_symbol_body` mit
  `targetType=project`, absolutem redigiertem Projekt-`targetPath` und den
  genannten Symbolen; alle Bodies vollständig, nicht trunkiert.
- **Befund:** In der laufenden Registry werden zwei Schreibweisen desselben
  Pfads zusammengeführt. Im persistenten Cache fließt die Schreibweise dagegen
  in den Key ein und kann getrennte Verzeichnisse erzeugen.
- **Auswirkung:** Über Prozessgrenzen oder direkte Session-Erzeugung können
  vermeidbare Cache-Misses und doppelte Cache-Daten entstehen, ohne dass die
  Inhaltsidentität unterschiedlich wäre.
- **Empfehlung:** Eine plattformgerechte Cache-Identity normalisieren, während
  der Anzeige-/Diagnosepfad seine lesbare Schreibweise behalten darf. Die
  Semantik muss für case-sensitive Dateisysteme ausdrücklich erhalten bleiben.
- **Abgrenzung / Unsicherheit:** Der Befund ist bei case-insensitiver
  Dateisystemsemantik relevant; ein plattformübergreifender Lauf wurde nicht
  ausgeführt.

## Findings — Missing Feature

### E4-MF-01 — Kein TTL-/Budget-Cleanup über Content-Key-Verzeichnisse hinweg

- **Priorität / Größe / Vertrauen:** P2 / L / hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `AssemblyDecompilationCache.cs:32-36,68-103,238-255`;
  `AssemblyCacheCleanup.cs:37-75`;
  `AssemblyCacheContract.cs:21`.
- **MCP-Parameter und Ergebnis:** `get_symbol_body` mit
  `targetType=project`, absolutem redigiertem Projekt-`targetPath` und
  `GetEntryDirectory`, `ReadGeneration`, `RetainGenerations`; Bodies vollständig,
  nicht trunkiert. `find_references` für `RetainGenerations`, Tiefe 1,
  `maxResults=50`, meldete zwei vollständige Call-Sites ohne Trunkierung.
- **Befund:** Der Cache-Key enthält die Content-Identität; dadurch erhält jede
  geänderte Datei ein eigenes Key-Verzeichnis. Retention behält nur bis zu zwei
  Generation-Verzeichnisse innerhalb genau dieses Verzeichnisses. Es gibt keinen
  Root-/Pfad-Hash-Cleanup über alte Content-Keys hinweg. Der beim Read
  aktualisierte Last-Access-Wert wird zudem nur in-memory erzeugt.
- **Auswirkung:** Wiederholte Dateiänderungen können alte Cache-Key-Bäume
  dauerhaft anwachsen lassen. Das in-memory Idle-TTL und das
  External-Resource-Diskbudget begrenzen diesen persistenten Cache nicht.
- **Empfehlung:** Einen lazy oder periodischen Root-Cleanup mit TTL, Bytebudget
  und sicherer Lock-/Pointer-Prüfung ergänzen. Aktuelle bzw. gerade gelesene
  Generationen müssen geschützt, Cleanup-Fehler sichtbar und Publish vorab gegen
  das Diskbudget geprüft werden.
- **Abgrenzung:** Die per-Key-Generation-Retention ist bounded und bleibt
  sinnvoll. Der getrennte Source-Cache ist nicht Gegenstand dieses Befunds;
  ebenso wenig der bereits dokumentierte Verlust einzelner Dokumentmetadaten
  beim Cache-Roundtrip.

### E4-MF-02 — Health zeigt keine Lifecycle- oder Resource-Metriken

- **Priorität / Größe / Vertrauen:** P2 / L / hoch.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `AssemblyAnalysisHealthSnapshotProvider.cs:24-77`;
  `AssemblyAnalysisSessionModels.cs:202-214`;
  `GetServerHealthModels.cs:39-72`;
  `GetServerHealthResponseBuilder.cs:16-125`;
  `AssemblyAnalysisRegistry.cs:94` (`ResourceHealth`).
- **MCP-Parameter und Ergebnis:** `get_symbol_body` mit
  `targetType=project`, absolutem redigiertem Projekt-`targetPath` und
  Health-/Payload-Symbolen; Bodies vollständig, nicht trunkiert.
  `find_references` für `AssemblyAnalysisRegistry.ResourceHealth`, Tiefe 1,
  `maxResults=50`, vollständig mit null Call-Sites. Die Docs-Lektüre bestätigt
  Status-/Origin-/Generation-/Diagnosefelder und Sessionlimits, nicht aber
  Lease-/Resource-Lifecyclefelder.
- **Befund:** Health liefert LoadState, Origin, Content-Hash, Generation und
  Diagnosen. Nicht sichtbar sind aktive Lease-Anzahl, Closing-/Retired-Zustand,
  Last-Use/Idle-Ablauf, Cache-Reuse, ResourceHealth, Disk-/Memory-Verbrauch,
  Operation-Slots und getrennte Registry-Budgets.
- **Auswirkung:** Ein Operator oder Agent kann nicht unterscheiden, ob eine
  Session wegen aktiver Leases, Retirement-Drain, Capacity, TTL oder fehlender
  Resource-Slots resident bleibt. Der interne ResourceHealth-Wert ist nach
  MCP-Abfrage unreferenziert.
- **Empfehlung:** Bounded und redigiert Lifecyclefelder je Session sowie
  aggregierte Resource-Snapshots je Registry ergänzen; Default-Compact-Output,
  `maxSessions` und Diagnose-Trunkierung beibehalten. Lease-/Pfad-Details nur
  als Zähler bzw. Statuswerte exponieren.
- **Abgrenzung:** Die bestehende Health-Projektion erfüllt den dokumentierten
  Minimalvertrag für Status, Origin, Generation und Diagnosen. Dies ist daher
  ein Observability-Missing-Feature, kein aktueller Wire-Vertragsbruch.

### E4-MF-03 — Kein optionaler hostweiter Gesamtblick auf getrennte Budgets

- **Priorität / Größe / Vertrauen:** P2 / L / mittel.
- **Aktuelle Dateien, Symbole, Zeilen:**
  `AssemblyAnalysisHostComposition.cs:177-219` erzeugt getrennte Resource-
  Registries für Sessions und Source-Snapshots; `AssemblyAnalysisResourceBudget.cs:20-58`
  definiert die per Registry geltenden Limits; `ExternalResourceRegistry.cs:15-469`
  aggregiert jeweils nur den eigenen Bestand.
- **MCP-Parameter und Ergebnis:** `get_feature_context` für
  `AssemblyAnalysisHostComposition` und `ExternalResourceRegistry` mit
  `targetType=project` und absolutem redigiertem Projekt-`targetPath`; Ergebnisse
  vollständig, Testzuordnung und Limits sichtbar, nicht trunkiert.
  Read-only Testlektüre bestätigt, dass dieselben Konfigurationswerte in beide
  Registries verdrahtet werden.
- **Befund:** Die Isolation ist korrekt und verhindert State-Vermischung, aber
  jedes Register besitzt sein eigenes Resident-/Disk-/Memory-/Parallelitätslimit.
  Ein Prozess-Gesamtbudget und ein aggregierter Health-Wert existieren nicht.
- **Auswirkung:** Beide Register können gleichzeitig ihr jeweiliges Limit
  ausschöpfen; der Prozessverbrauch kann damit über einem einzelnen
  konfigurierten Limit liegen. Der globale Health-Check kann diese Aufteilung
  nicht erklären.
- **Empfehlung:** Falls ein Prozessbudget gewünscht ist, einen hostweiten
  Accounting-Coordinator mit getrennten Identitätsräumen und gemeinsamem Limit
  einführen. Falls die aktuelle Isolation das Soll ist, die per-Registry-Semantik
  und den fehlenden Gesamtwert explizit in Health und Konfiguration surfacen.
- **Abgrenzung / Unsicherheit:** Die vorhandenen Composition-Tests sprechen für
  getrennte Registries als bewusstes Design. Deshalb ist dies ein konditionales
  Missing Feature und nicht als Fehler der Registry-Isolation eingestuft.

## Positiv nachgewiesene Invarianten und Testabdeckung

- `AssemblyAnalysisRegistry` verwendet pro kanonischem Pfad eine gemeinsame
  Creation-Task-Barriere. Caller-Cancellation beendet nicht die geteilte
  Creation; parallele Waiter und interne Creation-Aborts sind statisch getestet.
- Mtime-only-Reuse bleibt bei gleichem Content-Hash erhalten. Content-Wechsel
  erzeugt eine neue Generation, lässt einen aktiv geleasten alten Entry lesbar
  und schützt Generationen/ABA-IDs gegen stale Reuse.
- Snapshot-Leases erhöhen den aktiven Lease-Zähler unter `gate`; alte Snapshots
  werden erst nach Lease-Drain verworfen. Entry-Disposal ist idempotent,
  verweigert neue Leases und wartet auf aktive Leases.
- LRU-/TTL-Eviction berücksichtigt aktive Leases und revalidiert den Kandidaten
  unter dem Registry-Lock. Die externe Resource-Registry schützt aktive
  Ressourcen, begrenzt Operation-Slots und behandelt Reservation-Rollback.
- Host-Komposition und Disposal trennen Session- und Source-Resource-Registries
  und entsorgen Sessions vor ihren abhängigen Ressourcen.
- Cache-Publish verwendet Manifest-Kompatibilität, Pointer-Revalidierung und
  bounded Retention je Content-Key. Diese per-Key-Grenzen ersetzen jedoch nicht
  den in E4-MF-01 fehlenden Root-Cleanup.

## Read-only gelesene Nachweise

- Vollständig gelesen: `AGENTS.md`, die relevanten `.agents/rules/*.mdc`,
  `tasks/decompiled-assembly-audit/Konzept.md`, `roadmap.md`, die vorherige
  `code-map.md` und `.agents/skills/implement/SKILL.md`.
- Produktionscode read-only: Registry, Session, Entry, Registry-Disposition,
  Registry-Identity, Entry-Factory, Eviction, Health-Provider, Resource-Budget,
  External-Resource-Registry, Host-Komposition, Fingerprint, Cache, Cache-
  Cleanup, Session-Modelle und Workspace-Factory.
- Testverträge read-only: Registry-/Freshness-/Retirement-Race-Tests,
  External-Resource-Registry-Tests, Session-Tests, Cache-Cleanup-Tests,
  Host-Kompositions-Tests sowie Health-/Assembly-Health-Tests. Es wurden keine
  Testprozesse gestartet.
- Dokumentation read-only: `Docs/agent-api.md`, `Docs/integration.md` und
  `Docs/configuration.md` für Target-, Session-, Diagnose-, Cache- und
  Ressourcenverträge.

## Tatsächlich ausgeführte MCP-Abfragen

Alle zielgebundenen Projektabfragen verwendeten das aktuelle Schema mit
`targetType=project` und einem absoluten Projekt-`targetPath`; der konkrete
Pfad ist hier wegen des Redaktionsvertrags ausgelassen.

- `get_index_scope`: vollständiger C#-Projektumfang; Ergebnis nicht trunkiert.
- `get_file_tree`: Assembly-Analyse-Unterbaum, `view=tree`, `treeDepth=3`,
  `maxDepth=3`, `maxResults=200`, Metadaten und Zeilenzahlen; 44/44 Einträge,
  nicht trunkiert.
- `get_server_health`: initialer zielgebundener Projekt-Check mit Diagnosen und
  Sessions; der Daemon war geladen, ohne residenten Assembly-Session-Eintrag.
- `get_feature_context`: Registry, Session, Entry, Eviction, Resource-
  Registry, Health-Provider und Cache, jeweils mit Callers, Metrics, Tests und
  Violations. Alle Ergebnisse vollständig; der Eviction-Coordinator meldete
  eine bestehende `AIContextFootprint`-Violation, die hier nicht als
  Lebenszeitbefund klassifiziert wurde.
- `get_class_structure`: Registry, Session, Entry, Eviction, Resource-Budget,
  Cache und Health-Projektion; vollständige Member-/Zeileninformationen.
- `get_symbol_body`: die für die Findings genannten Methoden mit begrenztem
  Body-Limit; die vollständigen Methodenkörper waren verfügbar. Zwei zunächst
  mehrdeutige Kurzidentifikatoren wurden mit vollständiger Methodensignatur
  erneut abgefragt; die Ersatzabfragen waren vollständig.
- `find_references`: `AssemblyCacheCleanup.RetainGenerations` vollständig bei
  Tiefe 1 und `AssemblyAnalysisRegistry.ResourceHealth` vollständig mit null
  Call-Sites. Eine explorative Kurzabfrage zu `Dispose` war wegen des generischen
  Namens trunkiert und wurde nicht als Evidence verwendet.
- Vor dem Map-Edit und nach dem Map-Edit: `inspect_assembly` und zielgebundenes
  `get_server_health` für `LOCAL-01`, `LOCAL-02`, `LOCAL-03` und `FALSE-01`.
  Parameter der Abschlussrunde: `publicOnly=true`, `includeReferences=false`,
  `maxResults=1`, `maxMembers=2` sowie Health mit
  `includeSessions=true`, `includeDiagnostics=true`, `maxSessions=1`,
  `maxDiagnostics=2`. Die Zielpfade waren absolut und bleiben redigiert.
- Nach dem letzten Code-Map-Edit zusätzlich aggregiertes `get_server_health` mit
  `includeSessions=true`, `includeDiagnostics=true`, `maxSessions=4` und
  `maxDiagnostics=2`; die Sessionliste war erwartungsgemäß durch
  `maxSessions` begrenzt.

## Finale redigierte Spotchecks nach dem letzten Code-Map-Edit

| Label | Assembly-Check | Health-/Session-Check | Completeness / Trunkierung |
|---|---|---|---|
| `LOCAL-01` | `isError=false`, `decompiled`, Generation sichtbar, Status `partial` | `isError=false`, eine Session sichtbar, Status `partial` | `partial`; Diagnose-/Session-Samples bounded |
| `LOCAL-02` | `isError=false`, `decompiled`, Generation sichtbar, Status `partial` | `isError=false`, eine Session sichtbar, Status `partial` | `partial`; Diagnose-/Session-Samples bounded |
| `LOCAL-03` | `isError=false`, `decompiled`, Generation sichtbar, Status `partial` | `isError=false`, eine Session sichtbar, Status `partial` | `partial`; Diagnose-/Session-Samples bounded |
| `FALSE-01` | `isError=false`, recoverable `WORKSPACE_DIAGNOSTIC`, kein Snapshot | derselbe recoverable Negativpfad, keine Session | kein Analysepayload; keine Assembly-Ausführung |
| `GIT-01` | kein direkter Assembly-Spotcheck: Label referenziert in der Matrix einen Konfigurationspfad | nicht erneut materialisiert | im Epic-4-Lauf nicht anwendbar |

Der aggregierte Health-Check meldete 90 residente Assembly-Sessions, zeigte 4
und markierte die Sessionliste als durch `maxSessions` gekürzt. Die lokalen
zielgebundenen Checks blieben gegenüber der Vorprüfung unverändert. Die Werte
werden ausschließlich als redigierte Status-/Budget-Evidenz dokumentiert.

## Offene Unsicherheiten und spätere Verifikation

- E4-BUG-01 sollte mit demselben Pfad, zwei Dateigrößen, aktivem Alt-Lease und
  kleinem Resource-Limit reproduziert werden; maßgeblich ist der Resource-
  Accounting-Snapshot, nicht nur der Erfolg des Leases.
- E4-BUG-02 braucht einen kontrollierten File-Change-Hook zwischen Fingerprint,
  Decompilation/Cache-Read und Publish. Erwartet werden Abbruch ohne Publish
  oder ein expliziter, dokumentierter Commitpunkt.
- E4-BUG-03 braucht injizierbare Server-/Lifetime-Cleanup-Fehler während
  Retirement und Registry-Dispose; erwartet wird sichtbare Aggregation.
- E4-BUG-04/05 brauchen Race-Tests für Dispose-vs-Refresh und Cancellation
  unmittelbar vor Publish/Install.
- E4-OPT-02 darf erst nach einer Prüfung der stale-ID-Semantik begrenzt werden;
  Counter-Reset ohne Epoch-/Nonce-Schutz wäre nicht akzeptabel.
- E4-MF-01 braucht eine Entscheidung, ob der Assembly-Decompilation-Cache ein
  eigenes Diskbudget und einen persistenten Last-Access-Vertrag erhalten soll.
- E4-MF-03 braucht eine Produktentscheidung zwischen bewusst unabhängigen
  Budgets und einem zusätzlichen hostweiten Gesamtbudget.

## Auditvertrag und Hand-off

Es wurden ausschließlich dieser Bericht und `code-map.md` geändert. Es gab
keine Produktionscode-, Test-, Konfigurations- oder Produktdokumentations-
änderung, keinen Build, keine Testausführung und keinen Commit. Die Code-Map
wurde vor der finalen redigierten Health-/Session-/Assembly-Runde zuletzt
geändert; danach erfolgten nur die oben dokumentierten MCP-Abfragen und
Redaktionsprüfungen.
