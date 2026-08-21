---
status: vorschlag
type: tech-debt
priority: P1
last_updated: 2026-08-21
verified_against: src/AiNetLinter/Mcp/McpCodeGraphServer.cs, McpCodeGraphServerRefresh.cs
---

# 01 — Staleness-Check: Verzeichnisbaum-Walk bei jedem Tool-Call

## Befund (verifiziert)

Der Aufrufpfad jedes MCP-Tools läuft über `McpCodeGraphServer.GetCurrentSolution()`
(`McpCodeGraphServer.cs:202-231`). Dort wird bei jedem Aufruf unter dem globalen `_lock`
`RefreshStaleDocuments()` ausgeführt (`:228`). Daraus folgt:

1. **`HasSolutionDirChanged` walkt bei jedem Tool-Call den kompletten Verzeichnisbaum.**
   `RefreshStaleDocuments` setzt `ShouldSweep = () => HasSolutionDirChanged(...)`
   (`McpCodeGraphServer.cs:300-314`). `HasSolutionDirChanged` ruft
   `ComputeMaxDirMtimeUtc(solutionDir)` auf, das mit
   `Directory.EnumerateDirectories(..., SearchOption.AllDirectories)` **alle**
   Unterverzeichnisse rekursiv stat-t — einschließlich `.git/`, `node_modules/`,
   `bin/`, `obj/`, Build-Artefakte (`McpCodeGraphServer.cs:332-349`).
2. **Kein Throttling.** Eine gezielte Suche nach `RefreshInterval|Throttle|minInterval|
   Debounce` liefert 0 Treffer. Es gibt keinen Mindestabstand zwischen zwei
   Staleness-Checks.
3. **Alles passiert unter dem globalen `_lock`.** Während des Walks wartet jeder
   konkurrierende Tool-Call. Die Latenz skaliert damit mit der **Verzeichnisanzahl des
   Repos**, nicht mit der Größe der geladenen Solution.
4. **Phase 2 (Sweep) enumeriert `*.cs` über alle Verzeichnisse** inkl. `obj/`-generierter
   Dateien; gefiltert wird erst pro Datei via `IsValidDocument`
   (`SourceFileCatalog.cs:130-138`). Enumerationskosten fallen vor dem Filter an.

Der Kommentar in `ComputeMaxDirMtimeUtc` erklärt korrekt, *warum* der Walk nötig ist
(Windows propagiert Root-mtime nicht nach oben). Das Problem ist nicht die Korrektheit,
sondern die **Frequenz und der Umfang** des Walks.

## Warum das relevant ist

- AiNetLinter positioniert sich als residenter Server für große fremde C#-Repos. Ein
  Monorepo mit 20k+ Verzeichnissen (inkl. `.git`, npm-Artefakte) zahlt den Walk bei
  **jedem** der schnell hintereinander folgenden Tool-Calls eines Agenten.
- Gerade der typische Agenten-Loop (10–30 Tool-Calls in wenigen Minuten) trifft den
  Worst Case: viele Calls, kurze Abstände, gleicher Baum.
- Auf Windows sind Directory-stat-Aufrufe relativ teuer; bei Netzlaufwerken/devcontainer-
  Mounts multipliziert sich das.

## Lösungsoptionen (aufsteigend nach Eingriffstiefe)

### a) TTL-Throttle (empfohlener erster Schritt)
Mindestabstand zwischen zwei vollständigen Staleness-Checks, z. B. 1000–2000 ms
(konfigurierbar in `McpCodeGraphServerOptions`). Innerhalb der TTL wird der letzte
bekannte Zustand geliefert. Deterministisch, wenige Zeilen, kein neues Subsystem.
Restrisiko: ein innerhalb der TTL geänderte Datei wird erst beim nächsten Check gesehen —
für Agenten-Workflows (Edit → nächster Tool-Call liegt i. d. R. > 1 s auseinander) akzeptabel;
der mtime/Hash-Check der **bekannten** Dokumente (Phase 1/3) kann von der TTL ausgenommen
bleiben, da er auf Cache-Zuständen arbeitet und billig ist.

### b) Transiente Verzeichnisse vom Max-mtime-Walk ausschließen
`.git`, `node_modules`, `bin`, `obj` (und generell alles, was `IsValidDocument` ohnehin
ausschließt) können keine gültigen Quelldokumente enthalten — ihr Ausschluss aus dem Walk
ist korrektheitsneutral, reduziert aber auf typischen Repos 50–90 % der Verzeichnisse.
Kleiner, isolierter Change in `ComputeMaxDirMtimeUtc`.

### c) Erst messen, dann weiter optimieren
`get_server_health` führt bereits `RefreshCount`. Ergänze **StalenessCheckCount** und
**kumulative Staleness-Check-Dauer (ms)**. Damit lässt sich vor Option d) belegen, ob sich
ein `FileSystemWatcher`-Ansatz überhaupt lohnt.

### d) FileSystemWatcher als Opt-in (nur nach c))
Ersetzt den Poll-Walk durch Events. Deutlich mehr Komplexität (Buffer-Overflow, Rename-
Semantik, Dispose-Lebenszyklus, Tests). Nur bei belegtem Bedarf.

## Definition of Done (für a+b)

- Staleness-Check führt max. einmal pro TTL den Verzeichnis-Walk aus; bekannte Dokumente
  werden weiterhin bei jedem Call gegen mtime/Hash geprüft.
- `.git`/`node_modules`/`bin`/`obj` erscheinen nicht im Walk.
- Unit-Test: TTL-Verhalten deterministisch (injectable Clock), Ausschluss-Verhalten.
- Integrationstest: Änderung einer Quelldatei wird auch innerhalb der TTL spätestens beim
  nächsten Check nach TTL-Ablauf reflektiert (Staleness-Invalidierung bleibt intakt).
- `get_server_health` weist die neuen Zähler aus (Verbindung zu Finding 02).
