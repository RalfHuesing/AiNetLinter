---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: small-medium
priority: P1
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
herkunft: Review-Finding 2026-08-21 (ox-alpha)
---

# Staleness-Check: Verzeichnisbaum-Walk bei jedem Tool-Call drosseln

## Befund (verifiziert 2026-08-21)

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
  Monorepo mit 20k+ Verzeichnissen zahlt den Walk bei **jedem** der schnell hintereinander
  folgenden Tool-Calls eines Agenten.
- Gerade der typische Agenten-Loop (10–30 Tool-Calls in wenigen Minuten) trifft den
  Worst Case: viele Calls, kurze Abstände, gleicher Baum.
- Auf Windows sind Directory-stat-Aufrufe relativ teuer; bei Netzlaufwerken/devcontainer-
  Mounts multipliziert sich das.

## Lösungsoptionen (aufsteigend nach Eingriffstiefe)

### a) TTL-Throttle (empfohlener erster Schritt)
Mindestabstand zwischen zwei vollständigen Staleness-Checks, z. B. 1000–2000 ms
(konfigurierbar in `McpCodeGraphServerOptions`). Innerhalb der TTL wird der letzte
bekannte Zustand geliefert. Deterministisch, wenige Zeilen, kein neues Subsystem.
Restrisiko: eine innerhalb der TTL geänderte Datei wird erst beim nächsten Check gesehen —
für Agenten-Workflows akzeptabel; der mtime/Hash-Check der **bekannten** Dokumente
(Phase 1/3) kann von der TTL ausgenommen bleiben, da er auf Cache-Zuständen arbeitet
und billig ist.

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
- Integrationstest: Änderung einer Quelldatei wird spätestens beim nächsten Check nach
  TTL-Ablauf reflektiert (Staleness-Invalidierung bleibt intakt).
- `get_server_health` weist die neuen Zähler aus (Verbindung zu Aufgabe 01).
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.

---

# Audit zweiter Pass (2026-08-21): Funde und verschärfte Empfehlungen

Zweiter Audit-Pass gegen den aktuellen Code (`McpCodeGraphServerRefresh.cs` vollständig,
`FileSystemExclusionHelpers.cs`, `SourceFileCatalog.IsGeneratedPath`). Ergebnis: Die
Befunde 1–4 halten, aber das Konzept hatte Lücken — zwei davon betreffen **Bestandsbugs**,
die unabhängig von der Optimierung relevant sind.

## A. Was der erste Pass korrekt festhielt (bestätigt, mit Präzisierung)

- Walk bei jedem Call unter dem globalen Lock, kein Throttling (weiterhin verifiziert).
- **Semantik präzisiert:** Der Max-mtime-Walk erkennt nur *strukturelle* Änderungen
  (Eintrag hinzugefügt/entfernt/umbenannt) — Windows aktualisiert Verzeichnis-mtimes NICHT
  bei reinen Inhaltsänderungen von Dateien. Inhaltsänderungen bekannter Dateien laufen
  deshalb zu Recht über Phase 1/3 (mtime/Hash pro Dokument) und müssen von einer TTL
  **ausgenommen** bleiben. Die TTL verzögert ausschließlich die Erkennung *neuer* Dateien.
- `ReadOnlySnapshot`-Modus (Tests/Fixtures) ruft `RefreshStaleDocuments` nie auf — eine TTL
  betrifft diesen Pfad nicht.

## B. Übersehen 1: Bestandsbug — Junction-/Symlink-Zyklen im Walk

`ComputeMaxDirMtimeUtc` nutzt das alte String-API-Overload
(`Directory.EnumerateDirectories(..., SearchOption.AllDirectories)`). Dessen
Enumeration überspringt **keine Reparse Points** — ein Junction-/Symlink-Zyklus
(z. B. pnpm-Layouts, Worktree-Junctions, Backup-Tools) kann die Enumeration endlos laufen
lassen oder massiv aufblähen — **unter dem globalen Lock**, d. h. der gesamte Server hängt.
Der Hybridsuche-Scanner hat dasselbe Problem bereits gelöst:
`FileSystemExclusionHelpers.SafeEnumerateFilesWithErrors` setzt
`AttributesToSkip = FileAttributes.ReparsePoint`.

**Konsequenz:** Der Walk muss auf `EnumerationOptions` mit
`AttributesToSkip = FileAttributes.ReparsePoint` umgestellt werden — unabhängig von der TTL.

## C. Übersehen 2: Bestandsbug — Ein unzugängliches Verzeichnis degradiert dauerhaft

Wirft `MoveNext` des Enumerators mid-Rekursion eine `UnauthorizedAccessException`
(gesperrter Ordner, Berechtigungsproblem), propagiert sie aus `ComputeMaxDirMtimeUtc` und
wird in `HasSolutionDirChanged` mit `return true` ("geändert") beantwortet. Folge: Der
Sweep läuft ab dann **bei jedem Tool-Call** (Walk + `*.cs`-Vollenumeration), solange der
Ordner unzugänglich ist — genau der Worst Case, den diese Aufgabe beheben will, tritt dann
dauerhaft ein. `FileSystemExclusionHelpers` löst das mit Fehlerzähler + kontrolliertem
Abbruch des betroffenen Asts.

**Konsequenz:** Fehlerhafte Teilbäume dürfen den Walk nicht in "immer geändert" kippen.
Stattdessen: Fehler zählen und als Health-/Truncation-Metadaten ausweisen (Anschluss an
`get_server_health`, siehe Aufgabe 01).

## D. Übersehen 3: Es gibt bereits einen geteilten Ausschluss-Helper

`FileSystemExclusionHelpers.IsSearchExcludedRelativePath` pflegt genau die benötigte
Segmentliste (`.git`, `.hg`, `.svn`, `.vs`, `.idea`, `obj`, `bin`, `node_modules`,
`worktrees`, `.worktrees`, `testresults`, `artifacts`, `coverage`, `temp`, `packages`) und
wird bereits von WebFileCatalog, GetIndexScopeScanner und dem Hybridsuche-Scanner genutzt.
Das Konzept darf **keine eigene vierte Ausschlussliste** einführen — Option b) ist als
Wiederverwendung dieses Helpers zu spezifizieren. (Bewusste Aufteilung bleibt:
`SourceFileCatalog.IsGeneratedPath` bleibt für Roslyn-Dokumente maßgeblich; der Walk nutzt
die breitere Suchliste.)

## E. Verschärfung von Option b): Walk nur über Projektverzeichnisse (b')

Stärker als Namensausschlüsse und korrektheitsäquivalent: `PickProjectForNewFile`
(`McpCodeGraphServerRefresh.cs:258-268`) liefert für Dateien **außerhalb jedes
Projektverzeichnisses** `null` — der Sweep überspringt sie heute schon. Neue gültige
Dokumente entstehen ausschließlich unterhalb bekannter Projektordner. Damit gilt:

- Der Max-mtime-Walk darf auf die **Vereinigung der Projektverzeichnisse** beschränkt
  werden, ohne dass eine erkennbare neue Datei übersehen wird. `.git`, `node_modules` und
  Build-Artefakte außerhalb von Projektordnern fallen automatisch heraus.
- Innerhalb von Projektordnern bleiben `obj`/`bin` namensbasiert auszuschließen (D) —
  Projektgrenzen allein helfen dort nicht.
- Der Sweep selbst (`EnumerateCsFilesSafe` ab Solution-Root) sollte dieselbe Grenze
  nutzen; das macht ihn billiger und semantisch deckungsgleich mit `PickProjectForNewFile`.
- Randfall unverändert: Eine neue Datei außerhalb aller Projektordner wird auch heute
  schon bewusst ignoriert (Kommentar `McpCodeGraphServerRefresh.cs:93-101`).

**Neue Empfehlungsreihenfolge: c) messen → b')+D) Projektgrenzen + Helper-Wiederverwendung
(+ Reparse-Point-Fix aus B) → a) TTL nur falls noch nötig.** b' allein könnte den Walk so
weit verkleinern, dass eine TTL gar nicht mehr erforderlich ist — das würde den
sichtbaren Verhaltensunterschied (neue Dateien bis zu TTL verzögert) komplett vermeiden.

## F. Implementierungsfallen für die TTL (falls a) gebaut wird)

1. **Baseline-Nebenwirkung:** `HasSolutionDirChanged` aktualisiert
   `_lastSolutionDirMtimeUtc` als Seiteneffekt. Wird der Check während der TTL nur
   "aufgerufen und verworfen", verschiebt sich die Baseline ohne Sweep → **Änderungen
   gehen verloren**. Die TTL muss das boolesche Ergebnis cachen, niemals den
   Baseline-Vergleich während der Skip-Phase ausführen.
2. **Sichtbarkeitsvertrag:** Neue Dateien bleiben bis zum nächsten Sweep (≤ TTL)
   unsichtbar; Agent legt Datei an und fragt sofort `find_symbol` → `SYMBOL_NOT_FOUND`.
   Dokumentieren (Description-Hint) und per Test fixieren. Gegenprobe: Inhaltsänderung
   einer bekannten Datei muss weiterhin **sofort** sichtbar sein (Phase 3 ungehindert).
3. **Clock-Injection** für deterministische Tests (bereits im DoD).

## G. Ergänzte DoD-Punkte

- Walk folgt keinen Reparse Points (Junction-Zyklus-Test mit temporärer Junction).
- Ein unzugänglicher Teilbaum erhöht einen Fehlerzähler, kippt `changed` nicht dauerhaft
  und erscheint in Health-Metadaten; der Sweep läuft NICHT bei jedem Call.
- Walk- und Sweep-Grenze = Vereinigung der Projektverzeichnisse; Ausschlüsse stammen aus
  `FileSystemExclusionHelpers` (keine neue Liste).
- Test: Neue Datei innerhalb der TTL bleibt bis TTL-Ablauf unsichtbar (dokumentierter
  Vertrag); Inhaltsänderung bekannter Datei ist sofort sichtbar.
- Test: Baseline wird während TTL-Skip nicht verschoben (Änderung kurz nach Skip-Ende
  wird erkannt).


