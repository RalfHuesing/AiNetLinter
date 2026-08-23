---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 002
epic: EPIC-A
step_type: single
reviewed_by: kritiker
reviewed_by_model: stealth/ox-alpha (openrouter)
reviewed_by_model_knowledge_cutoff: nicht deklariert (kein Cutoff im eigenen System-Prompt angegeben)
reviewed_at: 2026-08-23T15:35:00+02:00
verdict: approved
tech_debt_ids: [TD-004, TD-005]
---

# Review Step 002: Projektregistry-Kern: Lease, Entry, Registry inkl. Eviction und FAILED-Marker

## Verdict

- [x] **approved** — alle vier Prüfebenen ok

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (Rules-Refs des Plans) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: Gate-Stichproben selbst nachgeprüft; Vollauftrag gemäß Nutzervorgabe nicht wiederholt (step-result-Nachweise geprüft)
- [x] Tests: Testkatalog vollständig gegen Plan abgeglichen; Vollauftrag gemäß Nutzervorgabe nicht wiederholt

## Befund

Alle vier Plan-Dateien plus Tests sind rein additiv umgesetzt (Diff `a80ec821`: 6 neue Dateien,
868 Insertions, keine Bestandsdatei berührt); alle elf Testkatalog-Punkte des Plans sind
implementiert und decken das ab, was ihr Name verspricht.

### Plan-Erfüllung

Synchrones `Lease` mit FAILED-Hit-Fallthrough als MISS, Adoption+Touch, Loader außerhalb des
Locks, Factory-Kick-off unter Lock (vertraglich nicht-blockierend), LRU-Eviction nur über
`InFlightCount == 0`, TTL-Tick nach MonitorLoop+CTS-Muster mit Busy-Guard/Pending-Adoption und
sofortiger FAILED-Räumung, deterministisches `DisposeAsync`, Key-Kanonisierung per
`GetFullPath`+Separator-Cut+`OrdinalIgnoreCase`, Options-Record mit benannten Defaults, keine
CLI-Flags (A.3 unberührt) — alles wie geplant; Codemap wurde passend zum Diff fortgeschrieben
(Doku-Commit `e8b4e367` geprüft).

### Rules-Konformität

`sealed`/`#nullable enable`/file-scoped Namespaces/ASCII-Bezeichner und -Kommentare überall;
Parameteranzahl ≤ 4 verifiziert (`metrics_lookup`), Options-Record statt Parameterliste (F7);
kein `.Wait()`/`.Result`/`.GetAwaiter().GetResult()` im Step-Code — die OCE-gefilterten
Shutdown-Catches in Tick-Loop und `DisposeAsync` sind genau das erlaubte
`AllowCancellationShutdownCatch`-Muster; Result-Pattern statt Exceptions, Defaults als benannte
Konstanten, Kommentare sparsam ohne Task-/Step-IDs; xUnit v3 mit `TestTempDirectory`-Fixtures
und parallel-fähigen eigenen Registry-Instanzen. Eigene Gate-Stichprobe: `get_violations`
(Scope `Projects`) → 0 Verstöße in 13 Dateien.

### Logische Korrektheit

Das Threading-Modell hält einer eigenen Race-Durchsicht stand: Der Registry-Lock deckt nur
Dictionary-Zugriffe + Factory-Kick-off (Loader-Dateizugriff in `TryAdoptOrCreate` und sämtliche
Server-Disposes laufen außerhalb — der Lock-Hygiene-Test belegt Bedienbarkeit anderer Roots bei
laufendem Hintergrund-Load); der Lease-Zähler läuft ausschließlich über `Interlocked`
(Increment unter Lock in `OpenLease`, genau-einmal-Dekrement per CompareExchange-Guard im
Lease, atomarer Read), sodass sich Lease-Öffnung (unter demselben Gate) und Eviction-
Entscheidung nie überholen können und eine Freigabe außerhalb des Locks keinen entfernten Entry
wiederbeleben kann; das Busy-Guard/Pending-Adoption-Rennen ist dadurch geschlossen, dass
`IsExpired`-Markierung, Adoption und Entfernung unter demselben Lock serialisieren — beide Wege
(adoption/rescue vs. dispose ohne Adoption) sind deterministisch getestet; die FAILED-Marker-
Pfade (Hit entfernt Marker und lädt frisch, Tick räumt sofort, kein negatives Caching, Raced-
Check in `InsertResident` verhindert Doppel-Workspace beim parallelen Nachladen) sind korrekt
und getestet; der Dedupe-Test erzwingt das Rennen über einen künstlichen Factory-Stall unter
Lock — zulässig, weil die Fabrik (nicht ein Solution-Load) gestallt wird und der eigentliche
Lock-Hygiene-Nachweis separat erfolgt.

### Konzept-Treue (Ebene 4)

A.4 (Sync-Lease, Dedupe im Instanzmuster statt Registry-eigener Task-Map, Key-Kanonisierung),
A.7 (Defaults 45 Min/5 Min/4, LRU nach Touch-Reihenfolge, Busy-Guard Self-Audit 2,
Pending-Adoption Reviews 8/13, FAILED-Soforträumung R2/B) und die Review-Entscheidungen
1/7/8/13/R2-A/R2-B sind treu umgesetzt; die Abweichung der `Lease`-Signatur vom Konzept-Skizzen-
Record (`ProjectLeaseResult` statt nacktem `ProjectLease`) ist bereits im Step-Plan als bewusste
Konkretisierung festgeschrieben und kein Coder-Drift; Non-Goals respektiert (keine CLI-Flags,
keine Docs-Berührung, kein neues NuGet, Batch unberührt, keine Tool-Änderungen); der
Wiring-seitige Teil des zweistufigen Zustandsvertrags ist plan-konform ausgeklammert.

### Build-/Test-Status

Vollaufträge gemäß Nutzervorgabe nicht wiederholt; die step-result-Nachweise sind plausibel und
mit dem Testkatalog konsistent (14 neue Unit-Tests innerhalb des gefilterten Laufs 30/30). Die
Quality-Gates habe ich selbst per MCP verifiziert:

```
get_violations (scopeFilter Projects)          → 0 Verstöße (13 Dateien)
metrics_lookup (Registry/Entry/Lease/Kern-Methoden) → alle Grenzwerte OK (Footprint max. 1434 ≤ 2500)
```

## Sonstige Beobachtungen / MINOR / NITPICK

- **Sync-Eviction-Pfad blockiert bis zum Load-Wind-down:** Das bestehende
  `McpCodeGraphServer.Dispose()` ist ein Sync-over-Async-Wrapper
  (`DisposeAsync().AsTask().GetAwaiter().GetResult()`); evictet der Sync-Pfad in
  `ProjectRegistry.Lease` einen noch `Loading`-Eintrag (kapazitätsbedingt möglich, da nur
  `InFlightCount == 0` gefordert ist), blockiert der aufrufende Thread bis zum Abschluss/Abbruch
  des Hintergrund-Loads — totes Risiko (Thread-Pool, `ConfigureAwait(false)`, kein Lock gehalten),
  aber ein Latenz-Puls, dessen Größe erst vom Wiring-Step-Komposition (`LoadFunc` muss den
  CancellationToken zügig honorieren) bestimmt wird. Dort beim Zusammensetzen beachten.
- **Kein Disposed-Guard in `Lease`:** Ein `Lease`-Aufruf nach Abschluss von `DisposeAsync`
  könnte einen neuen Entry einfügen, der nie geräumt wird. Im geplanten Betrieb unerreichbar
  (Prozess-Lifetime-Registry, Graceful-Shutdown-Reihenfolge laut Konzept Epic B) — für den
  Daemon-Bau ggf. einen billigen `disposed`-Check ergänzen. NITPICK.
- **Hinweis für den Wiring-Step (aus den Coder-Beobachtungen bestätigt):** Der Fault-Übergang
  von `_loadTask` ist asynchron — `LoadState` direkt nach `Lease` liefert auch dann `Loading`,
  wenn der Load kurz darauf scheitert; FAILED-Erkennung erfolgt erst beim nächsten Hit/Tick. Das
  passt zum zweistufigen Vertrag (Loading-Antwort → Retry → PROJECT_LOAD_FAILED), aber der
  Wiring-Step darf keine unmittelbare `LoadFailed`-Prüfung nach dem Lease erwarten.

## Tech-Debt-Einträge aus diesem Review

- `TD-004` (siehe `tech-debt.md`) — Soft-Cap bei nur-busy Register: Überschuss über `MaxProjects`
  wird vom TTL-Tick nicht aktiv reklamiert; harte Kapazitätsentscheidung fehlt bis Epic B.
- `TD-005` (siehe `tech-debt.md`) — `[WARN]` auf dem nicht injizierbaren Console-Kanal beim
  Disposal faulted Loads über den Sync-Eviction-Pfad (verwandt zu TD-002).
