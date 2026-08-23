---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 002              # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
epic: EPIC-A
step_type: single  # single | batch — aus step-plan.md übernehmen
coded_by: coder
coded_by_model: stealth/ox-alpha (openrouter)
coded_by_model_knowledge_cutoff: nicht deklariert (kein Cutoff im eigenen System-Prompt angegeben)
coded_at: 2026-08-23T15:17:00+02:00
code_commit_hash: a80ec821  # Commit mit Code+Tests
status_after: done  # done | blocked
blocker_category: n/a  # n/a | content | infrastructure
---

# Result Step 002: Projektregistry-Kern: Lease, Entry, Registry inkl. Eviction und FAILED-Marker

## Zusammenfassung

Die Registry-Ebene ist als getestete Einheit vollständig: `Lease(projectRoot)` kehrt
synchron zurück (HIT → Touch + Pending-Adoption; MISS → Definition laden,
nicht-blockierender Factory-Kick-off, LRU-Rahmen), die InFlight-Zählung läuft
ausschließlich über die Lease-Lifetime (Interlocked), Eviction geschieht TTL-/LRU-basiert
mit Busy-Guard und Pending-Adoption, FAILED-Marker werden beim Hit und im Tick entfernt
(kein negatives Caching). Der Registry-Lock deckt nur Dictionary-Zugriffe und den
Factory-Kick-off — Loader-Dateizugriff und Solution-Load laufen außerhalb. Rein additiv:
sechs neue Dateien (4 Produktionstypen + Options/Defaults am Record), keine bestehende
Produktionsdatei berührt.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Projects/ProjectEntry.cs` (neu) — residenter Zustand pro Key;
  `LastUsedUtc`/`PendingEviction` nur unter Registry-Lock, `InFlightCount` nur via
  Interlocked; `OpenLease()` inkrementiert.
- `src/AiNetLinter/Mcp/Projects/ProjectLease.cs` (neu) — Dispose senkt InFlightCount
  genau einmal (CompareExchange-Guard), Doppel-Dispose = no-op.
- `src/AiNetLinter/Mcp/Projects/ProjectLeaseResult.cs` (neu) — flaches Result-Record
  (`Succeeded`, `Lease`, `ErrorCode`, `ErrorMessage`) mit Fabriken `Success`/`Failure`,
  Präzedenz `ProjectDefinitionLoadResult`.
- `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` (neu) — `ProjectRegistry`
  (+ `ProjectRegistryOptions`, `ProjectRegistryDefaults` in derselben Datei):
  Key-Kanonisierung (`GetFullPath` + Trailing-Separator-Cut, `OrdinalIgnoreCase`),
  synchrones `Lease`, LRU-Eviction bei `MaxProjects`, TTL-Tick als MonitorLoop + CTS
  nach `ParentProcessWatchdog`-Muster, Busy-Guard/Pending-Adoption, FAILED-Soforträumung,
  `DisposeAsync` beendet Loop deterministisch und disposed Restbestand; injizierbarer
  BCL-`TimeProvider`.
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTests.cs` (neu) — 12 Unit-Tests
  zu allen Katalogpunkten (Key-Normalisierung, HIT/MISS+Touch, Loader-Fehler ohne Entry,
  Load-Dedupe über Factory-Gate deterministisch, Lock-Hygiene bei laufendem Hintergrund-
  Load, TTL/LRU-Eviction, Busy-Guard/Pending mit und ohne Adoption, FAILED-Hit/FAILED-Tick);
  Harness: `FakeClock : TimeProvider` (Interlocked-ticks) und `TrackingServerFactory`
  (echte minimale `McpCodeGraphServer`-Instanzen; Disposal-Nachweis über Cancellation-
  Registration im Fake-LoadFunc, FAILED-Variante über sofort faulted Task).
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectLeaseTests.cs` (neu) — 2 Tests zur
  Lease-Disziplin (genau-einmal-Dekrement inkl. Doppel-Dispose; InFlightCount bleibt > 0,
  bis eine gehaltene Nutzung freigibt, über Gate-TCS deterministisch).

## Commit

- **Code-Commit-Hash:** `a80ec821`
- **Message:**
  ```
  feat(mcp): Ergaenze Registry-Kern [11_epic-projektregistry-und-daemon]

  Neu unter src/AiNetLinter/Mcp/Projects/: ProjectEntry (residenter Zustand pro Key,
  InFlightCount nur via Interlocked), ProjectLease (Dispose senkt genau einmal,
  Doppel-Dispose no-op) und ProjectLeaseResult (flaches Result-Record nach
  Loader-Praezedenz). ProjectRegistry kanonisiert Keys (GetFullPath,
  OrdinalIgnoreCase), beantwortet Lease synchron (HIT: Touch + Adoption; MISS:
  Loader, nicht-blockierender Factory-Kick-off), evictet LRU-basiert bei
  maxProjects und per TTL-Tick nach ParentProcessWatchdog-Muster (MonitorLoop +
  CTS), schuetzt busy Entries (PendingEviction + Adoption), entfernt FAILED-Marker
  sofort ohne negatives Caching und nimmt eine BCL-TimeProvider-Uhr injiziert.
  Der Lock deckt nur Dictionary-Zugriffe und den Factory-Kick-off, nie einen
  Solution-Load; die Komposition mit TryLoadSolutionAsync macht der Wiring-Step.
  Rein additiv inkl. 14 Unit-Tests.

  Refs: tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-002
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                              → grün (0 Warnungen, 0 Fehler)
Iteration: dotnet test src/AiNetLinter.FastTests --filter
  "Category=Unit&FullyQualifiedName~Projects"                             → grün (30 Tests, 0 Fehler)
Gate: dotnet test src/AiNetLinter.FastTests --filter Category!=Stress     → grün (1656 Tests, 0 Fehler)
Gate: dotnet test src/AiNetLinter.IntegrationTests --filter
  Category!=Stress                                                        → grün (350 Tests, 0 Fehler)
```

Quality-Gates (vor dem Code-Commit): `get_violations` (Scope `Projects`) → 0 Verstöße;
`safeguard` (Scope `src/AiNetLinter/Mcp/Projects`) → 10/10 PASS; `metrics_lookup`
(6 Typen + 5 Kernmethoden) → alle Grenzwerte OK (größter Footprint 1434 ≤ 2500 bei
`ProjectRegistry`, 199 LOC ≤ 500; CC max. 4, Parameter max. 1).

## Abweichungen vom Plan

- **`RunEvictionTickAsync` als interne Methode exponiert:** Die Tick-Arbeit (Sweep)
  liegt in einer eigenen internen Methode, die der MonitorLoop je Intervall aufruft und
  die Tests direkt triggern — so sind alle Eviction-Assertions deterministisch ohne
  Sleep/Timeout (Plan: „keine Sleep-basierten Timing-Assertions"; FakeClock steuert nur
  die TTL-Bewertung, nicht das reale Task.Delay).
- **Sync-Eviction-Pfad disposed über bestehendes synchrones `Dispose()`:** Im synchronen
  `Lease` (LRU-Verdrängung, FAILED-Marker-Ersetzung) kann nicht auf `DisposeAsync`
  gewartet werden; verdrängte Server werden dort über das bereits vorhandene
  `McpCodeGraphServer.Dispose()` außerhalb des Locks frei gegeben. Tick-Loop und
  `DisposeAsync` nutzen wie geplant `await Server.DisposeAsync()`.
- **Options-Record + Defaults in `ProjectRegistry.cs`:** Verschachtelte Typen sind
  verboten (`BanPublicNestedTypes`); um die im Plan genannten „vier neuen Produktionsdateien"
  nicht zu sprengen, liegen `ProjectRegistryOptions` und `ProjectRegistryDefaults` mit in
  der Registry-Datei (Bestandsmuster: zwei Typen je Datei, vgl. `McpCodeGraphServerOptions.cs`).

## Beobachtungen

- **Soft-Cap bei nur-busy Register:** Findet die LRU-Eviction bei vollem Register keinen
  idle-Entry (`InFlightCount > 0` überall), wird der neue Entry trotzdem angelegt
  (kurzzeitiger Überschuss über `MaxProjects`). Der TTL-Tick reklamiert Überhang nicht
  aktiv — er räumt nur nach Idle/FAILED. Für den Daemon-Betrieb (Epic B) könnte eine
  harte Kapazitätsentscheidung oder tick-seitige Cap-Reklamation nötig werden; dem
  Kritiker als potenziellen Tech-Debt-/Konzeptpunkt gemeldet.
- **Console-Warnung beim Disposal faulted Loads:** `McpCodeGraphServer.DisposeAsync`
  schreibt bei nicht-abbrechbarem Hintergrund-Load eine `[WARN]`-Zeile über seinen
  Console-Kanal. Beim Räumen eines FAILED-Markers im Sync-Pfad (synkrones `Dispose`)
  kann diese Zeile erscheinen — kein injizierbarer Kanal vorhanden (verwandt mit TD-002).
- **Fault-Übergang ist asynchron:** `_loadTask` entsteht per `Task.Run(...)` im
  Server-Konstruktor; `LoadState == LoadFailed` ist erst nach Abschluss des Faults
  sichtbar. Die FAILED-Tests warten daher deterministisch über die interne
  `LoadTask`-Eigenschaft ab. Für den Wiring-Step relevant: Eine unmittelbare
  `LoadState`-Prüfung nach `Lease` sieht frische Fails noch als `Loading`.
- **`BanBlockingTaskAccess` greift auch im Testprojekt:** Der Dedupe-Test brauchte
  ursprünglich ein blockierendes `Task.Wait(...)` in der (synchronen) Factory — das Gate
  meldete einen Verstoß, ersetzt durch `ManualResetEventSlim.Wait(timeout)`. Bestehende
  Tests außerhalb des Scopes (z. B. `McpServerCommandLoadingStateTests`) nutzen ein
  solches Muster noch; falls das Gate solution-weit scharf gestellt wird, betrifft das
  Bestandscode (nicht hier behoben).

## Bekannte Unschärfen

- **Laufwerks-Root als projectRoot:** `Canonicalize("C:\\")` liefert `"C:"`. Für valide
  absolute Verzeichnis-Roots laut Registry-Vertrag (Wiring liefert validierte Roots,
  TD-003) unerreichbar; ein Zweit-Guard wurde bewusst nicht eingebaut (Doppelvalidierung).
- **Zustandslücke nach FAILED-Hit mit anschließendem Loader-Fehler:** Entfernt der Hit
  den FAILED-Marker und schlägt danach der Definitionsload fehl (Datei zwischenzeitlich
  weg), existiert weder Marker noch Entry — R2/B-konform (kein negatives Caching),
  nächster Aufruf lädt neu. Verhalten bewusst so, aber erwähnenswert für die Review.
- **Dedupe-Test erzwingt das Rennen über einen Factory-Stall** (`ManualResetEventSlim`
  innerhalb des Registry-Locks). Das ist ein künstlicher Stall der Fabrik, kein
  Solution-Load; die eigentliche Lock-Hygiene („andere Roots bedienbar während eines
  Loads") ist separat über den Hintergrund-Load-Test abgesichert.

## Falls Status `blocked`

**Blocker-Art:** n/a

**Blockiert weil:** n/a — Step fertiggestellt.

**Brauche von Nutzer:** n/a

**Aktueller Stand:** n/a
