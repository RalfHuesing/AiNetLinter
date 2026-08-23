---
status: done (pending audit)
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 002               # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
corrects: null             # <null | step-NNN> — nur gesetzt, wenn dieser Step eine Korrektur ist
title: "Projektregistry-Kern: Lease, Entry, Registry inkl. Eviction und FAILED-Marker"
epic: EPIC-A          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet
estimated_risk: high  # Nebenläufigkeitskern (Lease-Zähler, TTL/LRU-Tick, Pending-Adoption) — sorgfältigste Review-Runde des Epics
step_type: single  # single (Default) | batch
items: []  # nur bei step_type: batch
created_by: planer  # planer | orchestrator
created_by_model: stealth/ox-alpha (openrouter)
created_by_model_knowledge_cutoff: nicht deklariert (kein Cutoff im eigenen System-Prompt angegeben)
created_at: 2026-08-23T14:40:00+02:00
related_to: [step-001]  # baut auf dem Step-001-Fundament (Loader/Factory/ErrorCodes) auf
---

# Step 002: Projektregistry-Kern: Lease, Entry, Registry inkl. Eviction und FAILED-Marker

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** `EPIC-A` aus `roadmap.md` — offen sind A.3 (harter Cut), A.4-Wiring,
  A.7 (Eviction & Zustandsvertrag), A.8-Rest, A.9/A.x. Dieser Step baut den
  Registry-Kern: die noch fehlenden A.4-Klassen (`ProjectEntry`, `ProjectLease`,
  `ProjectRegistry`) plus den A.7-Eviction-Block und den registry-seitigen Teil
  des zweistufigen Zustandsvertrags. Er ist bewusst der große Fachstep VOR dem
  Wiring-Step: Das Wiring (`_registry.Lease(projectRoot)` in allen Registrations)
  setzt eine funktionsfähige Registry voraus.
- **Konzept-Referenz:** `Konzept.md` A.4 (Sync-Lease, Load-Dedupe im Instanzmuster/
  Review 1, Key-Kanonisierung), A.7 (Idle-TTL/maxProjects/LRU, Busy-Guard/Self-Audit 2,
  Pending-Adoption/Reviews 8/13, FAILED-Aufräumung/Review R2/B, zweistufiger
  Zustandsvertrag — Kalt-Load-Seite), A.8 (Unit-Teilkatalog Registry/Eviction/Lease);
  Vertragsentscheidungen Reviews 1/7/8/13/R2-A/R2-B.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Ist-Zustands per AiNetLinter-MCP (`find_symbol`, `get_file_skeleton`,
`get_symbol_body`, `find_references`, `search_pattern`) vorgefunden — maßgeblich für
diesen Plan:

- **Step-001-Fundament existiert real** (Commit `e0b25033`): `Mcp/Projects/` enthält
  `ProjectDefinition` (Record `SolutionPath`/`RulesPath`), `ProjectDefinitionLoader.Load(string?)`
  → flaches `ProjectDefinitionLoadResult`, `ProjectErrorCodes` (sechs Codes als `const`),
  `ProjectInstanceFactory.MaterializeRules(...)` sowie `Create(ProjectDefinition)`
  → `McpCodeGraphServerOptions` (10 LOC; setzt heute **noch kein** `LoadFunc`
  und keine `Console`). `ProjectEntry`/`ProjectLease`/`ProjectRegistry` fehlen
  (per `find_symbol "ProjectRegistry"`: 0 Treffer).
- **Zustands-/Dedupe-Anker vorhanden:** `ServerLoadState` (`Loading`/`Loaded`/
  `LoadFailed`, `Mcp/ServerLoadState.cs`) wird von `McpCodeGraphServer.LoadState`
  exponiert; `_loadTask`-Adoption im Konstruktor und `DisposeAsync` mit
  LoadTask-Abbruch existieren pro Instanz (F1/F5). Der Load-Dedupe lebt damit
  bereits im Instanzmuster (Review 1) — die Registry braucht **keine** eigene
  Task-Dedupe-Map und darf nie einen Solution-Load unter ihrem Lock ausführen.
- **LoadFunc-Lücke:** `McpCodeGraphServerOptions.LoadFunc`
  (`Func<CancellationToken, Task<SourceFileCatalog?>>?`) wird produktiv ausschließlich
  in `McpServerCommand.RunAsync` gesetzt (wrapt `TryLoadSolutionAsync`). Damit der
  MISS-Pfad Instanzen MIT Hintergrund-Load erzeugen kann, erhält die Registry einen
  injizierten Instanz-Fabrik-Delegat; dessen echte Komposition mit
  `TryLoadSolutionAsync` macht der Wiring-Step. Dieser Step bleibt dadurch rein
  additiv (neue Dateien + Tests, keine bestehende Produktionsdatei anfassen).
- **Keine Clock-Abstraktion im Bestand** (`find_symbol "TimeProvider"`: 0 Treffer) —
  die injizierbare Uhr für TTL-Tests kommt als BCL-`TimeProvider` neu hinzu
  (Konzept „Abhängigkeiten": BCL-only, kein neues NuGet).
- **Periodik-Muster:** `ParentProcessWatchdog` implementiert Periodik als
  `MonitorLoopAsync`-Task mit `CancellationTokenSource` + `pollingInterval` — dieses
  Bestandsmuster ist Vorbild für den TTL-Tick (kein neuer Timer-Mechanismus).
- **Testbasis:** `McpInMemoryTestContext.CreateServer()` baut Serverinstanzen ohne
  Prozessstart (F4); für Registry-Unit-Tests genügt jedoch ein Fabrik-Delegat, das
  minimale/zählende Fake-Instanzen liefert — kein Roslyn-Load nötig.
- **`PROJECT_LOAD_FAILED` existiert noch nirgends** (`search_pattern`: 0 Treffer) —
  der Dispatch-seitige Antworttext entsteht mit dem Wiring-Step; hier entsteht nur
  die registry-seitige FAILED-Marker-Behandlung.
- **Anti-Loop-Check (Codemap):** Kein Widerspruch zu festgehaltenen Entscheidungen.
  Eine bewusste Konkretisierung der Konzept-Skizze: Dort steht `internal ProjectLease
  Lease(string projectRoot);`. Da Loader-Fehler (A.5) deterministisch **als Daten**
  an den Wiring-Step übergeben werden sollen (Richtlinien §5 „Result-Pattern statt
  Exceptions"; Präzedenz `ProjectDefinitionLoadResult` aus step-001; Lehre
  `BanPublicNestedTypes` → flacher Record), gibt `Lease` ein flaches Ergebnis-Record
  zurück, das entweder Lease oder ErrorCode+ErrorMessage trägt. Die Konzept-Skizze
  ist als vereinfachte Projektion markiert („schlank wegen F7"); das konzipierte
  Verhalten (synchrone Rückkehr, harte Fehler an dieser Stelle) bleibt exakt erhalten.

## Intention

Nach diesem Step existiert eine vollständig unit-getestete, transportunabhängige
Projektregistry: `Lease(projectRoot)` kehrt synchron zurück (HIT → Touch + Lease;
MISS → Definition laden, Instanz mit Hintergrund-Load erzeugen, LRU/TTL-Rahmen),
die InFlight-Zählung läuft strukturell über die Lease-Lifetime (Review 7), Eviction
(TTL/LRU/Busy-Guard/Pending-Adoption) disposed korrekt ohne laufende Calls zu
zerstören, und Kalt-Load-Fehler bleiben als FAILED-Marker adressierbar und werden
ohne negatives Caching frisch geladen (Review R2/B). Der anschließende Wiring-Step
kann `_registry.Lease(projectRoot)` dann rein mechanisch in alle Registrations
einziehen.

## Konkrete Änderungen

**step_type: single** — vier neue Produktionsdateien + Tests; bewusst KEINE Änderung
an bestehenden Produktionsdateien (Komposition des echten Fabrik-Delegaten macht der
Wiring-Step).

### Datei 1 (NEU): `src/AiNetLinter/Mcp/Projects/ProjectEntry.cs`

- **Was:** `internal sealed class ProjectEntry` (oder Record, Coder-Wahl nach Lint):
  `RootPath` (kanonisierter Key), `Definition` (`ProjectDefinition`), `Server`
  (`McpCodeGraphServer`), `LastUsedUtc` (`DateTime`), `InFlightCount` (`int`,
  nur via `Interlocked`), `PendingEviction` (`bool`).
- **Warum:** Träger des residenten Zustands pro Key (Konzept-Strukturbaum A.4:
  „RootPath, Definition, Server, LastUsedUtc, PendingEviction" + InFlightCount
  aus Review 7).

### Datei 2 (NEU): `src/AiNetLinter/Mcp/Projects/ProjectLease.cs`

- **Was:** `internal sealed class ProjectLease : IDisposable` — `{ Server }` plus
  Dispose-Callback in die Registry; `Dispose()` dekrementiert `InFlightCount`
  genau einmal (`Interlocked.CompareExchange`-Guard; Doppel-Dispose = no-op).
- **Warum:** Review 7 verbindlich: InFlight-Tracking strukturell über Lease-Lifetime,
  nie manuell; jedes spätere Tool-Lambda nutzt `using var lease = ...`.

### Datei 3 (NEU): `src/AiNetLinter/Mcp/Projects/ProjectLeaseResult.cs`

- **Was:** Flaches `internal sealed record ProjectLeaseResult` (`Succeeded`, `Lease`,
  `ErrorCode`, `ErrorMessage`) + statische Fabriken `Success`/`Failure` — analog zum
  Bestands-Record `ProjectDefinitionLoadResult`; KEINE verschachtelten Typen.
- **Warum:** Die vier loader-seitigen A.5-Fehler (`PROJECT_NOT_INITIALIZED`,
  `PROJECT_DEFINITION_INVALID`, `SOLUTION_NOT_FOUND`, `RULES_NOT_FOUND`) verlassen die
  Registry als Daten; das Mapping zu `McpToolResults.Error(...)` macht erst der
  Wiring-Step. Kein `throw` für erwartbare Fehlerfälle (Richtlinien §5).

### Datei 4 (NEU): `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs`

- **Was:** `internal sealed class ProjectRegistry : IAsyncDisposable`.
  - **Konstruktion über Options-Record** (F7, `MaxConstructorDependencies ≤ 5`):
    `ProjectRegistryOptions(InstanceFactory: Func<ProjectDefinition, McpCodeGraphServer>,
    Clock: TimeProvider, MaxProjects = 4, IdleTtl, TickInterval)`. Defaults
    (45 Min idle / 5 Min Takt / maxProjects 4) als benannte Konstanten am Record.
    CLI-Flags (`--mcp-project-ttl-minutes`, `--mcp-max-projects`) kommen mit A.3 —
    hier NICHT anfassen.
  - **Key-Kanonisierung (Final-Pass):** `Path.GetFullPath(projectRoot)`, abschließende
    `\` und `/` entfernt; `Dictionary<string, ProjectEntry>(StringComparer.OrdinalIgnoreCase)`.
  - **`Lease(projectRoot)` — synchron (Review 1):**
    - HIT: Wenn `Server.LoadState == LoadFailed` → Entry entfernen, weiter als MISS
      (kein negatives Caching, R2/B). Sonst: `PendingEviction`-Adoption (Flag
      zurücksetzen, Review 8), Touch (`Clock`), Lease zurückgeben.
    - MISS: Check + `InstanceFactory`-Aufruf unter kurzem Lock (Factory-Vertrag:
      nicht-blockierend — konstruiert die Instanz und STARTET nur den Hintergrund-Load;
      siehe Notes). Danach `ProjectDefinitionLoader.Load(root)`; Fehler →
      `Failure(code, message)` ohne Eintrag; Erfolg → bei vollem Register LRU-Eviction
      (nur Entries mit `InFlightCount == 0`; busy Keys bleiben, siehe Busy-Guard) →
      Entry anlegen (`LoadState == Loading`) → Lease zurückgeben.
  - **TTL-Tick** (Default alle 5 Min, Loop nach `ParentProcessWatchdog`-Muster:
    Task + `CancellationTokenSource`): Entries mit `LastUsedUtc > IdleTtl` disposen
    (`Server.DisposeAsync`), aber NUR mit `InFlightCount == 0`; Entries mit laufendem
    Call stattdessen als `PendingEviction = true` markieren (Busy-Guard, Self-Audit 2);
    `FAILED`-Entries SOFORT entfernen/disposen, unabhängig von `LastUsedUtc` (R2/B);
    pending Entries ohne Adoption bis zum Tick disposen.
  - **Lock-Hygiene (Review 1):** kurzer Lock ausschließlich um Dictionary-Manipulationen
    und den nicht-blockierenden Factory-Kick-off; niemals um einen Solution-Load oder
    Loader-Dateisystemaufruf mit Wartezeit. Verschiedene Keys blockieren sich nie.
  - **`DisposeAsync`:** Tick-Loop deterministisch beenden (CTS), alle verbliebenen
    Entries disposen.
- **Warum:** Kern des Epics (A.4-Klassenrest + A.7 komplett auf Registry-Seite).

### Tests (NEU): `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTests.cs`
(+ ggf. `ProjectLeaseTests.cs`, Coder-Wahl)

Siehe Abschnitt „Tests".

## Tests

FastTests, `Category=Unit`; Fabrik-Delegat liefert minimale/zählende
`McpCodeGraphServer`-Instanzen; Synchronisation ausschließlich über
`TaskCompletionSource` und injizierbare `TimeProvider`/Fake-Clock — keine
Sleep-basierten Timing-Assertions:

- [ ] Key-Normalisierung: `C:/repos/foo`, `C:\repos\foo\`, `c:/REPOS/foo` mappen auf denselben Entry (`GetFullPath` + `OrdinalIgnoreCase`)
- [ ] HIT/MISS: erster `Lease` → MISS (Fabrik genau 1×), zweiter → HIT (weiterhin 1 Instanz); `LastUsedUtc` wird beim Hit aktualisiert (via Fake-Clock prüfbar)
- [ ] Loader-Fehler-Durchreichung: Root ohne Definitionsdatei → `Succeeded=false`, `ErrorCode=PROJECT_NOT_INITIALIZED`, kein Registry-Eintrag, Fabrik 0× gerufen
- [ ] Load-Dedupe (Self-Audit 1): zwei parallele `Lease`-Aufrufe auf denselben Root → genau EINE Instanz/genau ein LoadFunc-Start; während eines laufenden Loads bleibt `Lease` auf ANDEREN Roots bedienbar (Lock-Hygiene)
- [ ] Busy-Guard (Self-Audit 2): Entry mit `InFlightCount > 0` wird weder vom TTL-Tick noch von LRU disposet; nach `Lease.Dispose` greift die Eviction
- [ ] TTL-Eviction mit injizierbarer Clock: `LastUsedUtc > IdleTtl` → Tick disposet (Server-Dispose wurde gerufen); darunter nicht
- [ ] LRU + maxProjects: (max+1)-ter Key verdrängt den zuletzt-genutzten (Touch-Reihenfolge, nicht Insert-Reihenfolge); Dispose des Verdrängten erfolgt
- [ ] Pending-Adoption (Review 8/13): Call gegen eviction-pending Key adoptiert (KEIN erneuter Fabrik-Aufruf = kein zweiter Workspace, Flag weg, Touch erneuert); ohne Adoption bis zum nächsten Tick wird disposed
- [ ] Lease-Disziplin: Dispose senkt InFlightCount genau einmal; Doppel-Dispose ist no-op; InFlightCount fällt erst nach Abschluss einer gehaltenen Nutzung (simulierter verzögerter Task) auf 0
- [ ] FAILED-Marker (R2/B): Load endet mit `LoadState == LoadFailed` → Entry bleibt als Marker adressierbar; nächster Hit entfernt ihn und startet frischen Load (Fabrik erneut gerufen); TTL-Tick räumt FAILED sofort weg
- [ ] Zweistufiger Vertrag, Kalt-Load-Seite: fehlgeschlagener Erst-Load erzeugt KEIN negatives Caching (erneuter Aufruf lädt wirklich neu)

Bewusst NICHT in diesem Step (gehören in den Wiring-Step, dort begründet):
Contract-Tests `projectRoot` required im Tool-Schema, uniforme Pflicht +
`PROJECT_ROOT_REQUIRED`/`PROJECT_ROOT_INVALID` (Argumentebene, TD-003-Vorschlag),
Snapshot-Semantik über den Tool-Dispatch, Lease-Lifetime-Nachweis am echten
Registrations-Lambda (R2/A — der Counter-Mechanismus wird hier getestet, das
async-Wiring dort), `PROJECT_LOAD_FAILED`-Antworttext, Health-Aggregation pro Key,
inkrementeller Refresh-Zweig (`[WARN]`-Kopf, `LastGoodStateUtc`/`LastLoadError`).

Iteration während der Entwicklung gefiltert:
`dotnet test src/AiNetLinter.FastTests --filter "Category=Unit&FullyQualifiedName~Projects"`.
Kompletter Nicht-Stress-Stack EINMAL als Abschluss-Gate (siehe DoD).

## Definition of Done

- [ ] Alle „Konkreten Änderungen" umgesetzt (additiv; keine bestehende Produktionsdatei geändert)
- [ ] Build-Command aus Tech-Stack-Notiz (`dotnet build`) fehler- UND warnungsfrei
- [ ] Abschluss-Gate EINMAL je Step: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` UND `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün
- [ ] Quality-Gates VOR dem Code-Commit über AiNetLinter-MCP-Tools (statt grep/Volltext-Lesen): `get_violations` (scopeFilter `Projects`) → 0 Verstöße; `safeguard` (Scope `src/AiNetLinter/Mcp/Projects`) → PASS; `metrics_lookup` für alle neuen Symbole (Grenzwerte F7: ab 5 Parametern Options-Record, `MaxConstructorDependencies ≤ 5`, `AIContextFootprint ≤ 2500`)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Deutsch, imperativ)
- [ ] `step-002/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt
- [ ] Codemap-Einträge für die neuen Dateien durch den Coder fortgeschrieben (vor Doku-Commit)

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` #Grenzwerte/Kurz-Stil — `sealed`, `#nullable enable`,
  file-scoped namespaces, Options-Record statt Parameterlisten (F7),
  `MaxMethodParameterCount 4`, `AIContextFootprint 2500`, `EnforceAsciiIdentifiers`
- `.agents/rules/AiNetLinter.mdc` #agent-resilience — kein `.Wait()`/`.Result` im Tick-/
  Dispose-Pfad, kein leerer `catch` (`AllowCancellationShutdownCatch` gilt für den
  Shutdown des Tick-Loops)
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — xUnit v3, `TestTempDirectory` für
  Fixture-Roots (Definitionsdatei-Fixtures), KEINE zwangsserialisierende Collection —
  Registry-Tests laufen parallel-fähig über eigene Registry-Instanzen
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 — Zero-Warning, Result-Pattern statt
  Exceptions für erwartbare Fehler, Kommentar-Sparsamkeit OHNE Task-/Step-ID-Referenzen,
  DRY/Magic Values (Defaults als benannte Konstanten, Bestandsmuster wiederverwenden)
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 — Dogfooding: MCP-Tools vor rg/grep;
  bei „lädt noch"-Antworten zuerst `get_server_health`

## Bekannte Ausnahmen

Keine erwarteten flaky Tests. Falls der Parallelitäts-Test (Load-Dedupe) unter Last
schwingt: deterministisch über Gate-Tasks (`TaskCompletionSource`) lösen, nicht über
Timeout-Erhöhung oder Sleep.

## Code-Skizze (optional)

```csharp
// Signaturbild, verkürzt — keine vollständige Implementierung
internal sealed record ProjectRegistryOptions(
    Func<ProjectDefinition, McpCodeGraphServer> InstanceFactory,
    TimeProvider Clock,
    int MaxProjects = ProjectRegistryDefaults.MaxProjects,     // 4
    TimeSpan IdleTtl = default,                                // Default 45 min
    TimeSpan TickInterval = default);                          // Default 5 min

internal ProjectLeaseResult Lease(string projectRoot)
{
    var key = Canonicalize(projectRoot);                       // GetFullPath + TrimEnd('\\','/')
    lock (_gate)
    {
        if (_projects.TryGetValue(key, out var entry))
        {
            if (entry.Server.LoadState == ServerLoadState.LoadFailed)
                _projects.Remove(key);                         // weiter als MISS (R2/B)
            else
            {
                entry.PendingEviction = false;                 // Adoption (Review 8)
                entry.LastUsedUtc = _now();
                return ProjectLeaseResult.Success(entry.OpenLease());
            }
        }
        // MISS: Definition laden + Instanz erzeugen (nicht-blockierender Factory-Kick-off),
        // bei vollem Register vorher LRU-Eviction (nur InFlight==0)
    }
}
```

## Notes

- **Factory-Vertrag ist entscheidend (Review 1):** Der `InstanceFactory`-Delegat MUSS
  dokumentiert nicht-blockierend sein — er konstruiert die Instanz und startet den
  Hintergrund-Load (`LoadFunc`), wartet den Solution-Load aber NICHT ab. Nur dann ist
  „Check + Create unter kurzem Registry-Lock" vereinbar mit „der Registry-Lock deckt
  nur Dictionary-Zugriffe, nie einen Solution-Load". Alternative (Key-Reservation mit
  wartenden Callern) wurde verworfen: Sie bricht die synchrone `Lease`-Signatur aus
  der Konzept-Skizze. Die echte Komposition (Fabrik mit `TryLoadSolutionAsync` wrappen)
  macht der Wiring-Step.
- **TD-001/TD-002/TD-003 bewusst NICHT hier:** Defekte `rules.json` im Registry-Pfad
  (TD-001) ist eine Vertragsentscheidung für den Wiring-Step; der Root-Guard liegt laut
  tech-debt.md auf Argumentebene des Wiring-Steps (TD-003 — die Registry bekommt
  validierte, absolute Roots; ein Zweit-Guard wäre Doppelvalidierung); Console.Error-
  Kanal (TD-002) ist Epic B. Die `PROJECT_ROOT_*`-Codes bleiben bis zum Wiring inaktiv.
- Tick-Loop nach Bestandsmuster `ParentProcessWatchdog` (Task + `CancellationTokenSource`
  + Intervall) statt neuem Timer-Typ; `DisposeAsync` muss den Loop deterministisch
  beenden. Keine neuen NuGet-Pakete (`TimeProvider` ist BCL seit .NET 8).
- Doku-/Sync-Pflichten: KEINE Docs/README/AGENTS-Berührung in diesem Step — die
  Registry ist intern und noch nicht aufrufbar; die Doku-Sammelpflichten (A.x) landen
  gebündelt im fachlich berührenden Wiring-Step. drift-audit
  (`find_duplicates`/`find_magic_values`/`find_dead_code`) läuft einmal PRO EPIC vor
  Epic-Abschluss — NICHT in diesem Step.
- Bewusste Wiederverwendung statt Duplikation: flaches Result-Record-Muster
  (`ProjectDefinitionLoadResult`-Präzedenz), `ServerLoadState`-Zustandsanker,
  `ParentProcessWatchdog`-Periodikmuster, `TestTempDirectory`-Fixtures wie in
  `ProjectDefinitionLoaderTests`.
