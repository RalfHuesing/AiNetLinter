---
status: done (pending audit)
type: step-plan
task: codegraph-mcp
step: 002
title: "Resident Server-Zustand: McpCodeGraphServer mit Lazy Staleness-Invalidierung"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T15:00:00Z
related_to: [step-001]
---

# Step 002: Resident Server-Zustand: McpCodeGraphServer mit Lazy Staleness-Invalidierung

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-02` aus `roadmap.md` — Server-Zustand & Staleness-Invalidierung:
  zustandshaltende Server-Klasse ohne DI-Container, Hash/mtime-Cache pro Datei,
  lazy Prüfung vor jeder Tool-Antwort, inkrementelles Update über
  `SourceFileCatalog.WithUpdatedSolution`, Thread-sicherer Zugriff auf
  `Solution`/`Compilation`. Vollständig offen (0 % — `McpServerCommand` hält
  aktuell keinerlei State, siehe unten).
- **Konzept-Referenz:** `konzept.md` Muss-Haben "Server lädt die Solution
  einmal ... und hält sie resident", "Lazy Staleness-Invalidierung",
  "Thread-sicherer Zugriff auf die gehaltene Solution/Compilation"; "Wie" /
  Server-Betrieb Punkt 1-2; "Verworfene Alternativen" (FileSystemWatcher
  bewusst verworfen zugunsten von lazy Hash-Check — bindend für dieses Step).

## Aktueller Projektzustand (JIT-Kontext)

- **`src/AiNetLinter/Commands/McpServerCommand.cs`** (aus step-001): `RunAsync`
  ruft `TryLoadSolutionAsync(solutionPath, ct, c)` auf. Diese Methode lädt den
  `SourceFileCatalog` in einem lokalen `using`-Block
  (`using var catalog = await SourceFileCatalog.LoadAsync(...)`), prüft nur
  `catalog.HasLoadingErrors` für eine Warnmeldung und **disposed den Catalog
  sofort wieder**, bevor der MCP-Server überhaupt startet (Zeile 105-119). Es
  existiert aktuell **keine** Instanz, die die geladene `Solution` über den
  Start hinaus hält — exakt die Lücke, die dieser Step schließt. Der
  Server selbst läuft mit leerem `ToolCollection` (EPIC-03 noch nicht
  begonnen), es gibt also noch keinen Tool-Call, der den neuen Zustand
  tatsächlich über das MCP-Protokoll anspricht — das ist erwartet und kein
  Grund, diesen Step zu verschieben: EPIC-02 baut bewusst die Grundlage, auf
  die EPIC-03 aufsetzt (siehe Epic-Reihenfolge in `roadmap.md`).
- **`src/AiNetLinter/Baseline/SourceFileCatalog.cs`**: `WithUpdatedSolution`
  (Zeile 66-69, `internal`) existiert bereits exakt für den Zweck "neue
  Catalog-Instanz mit aktualisierter In-Memory-Solution" (aktuell für
  Auto-Fix genutzt) — wird hier direkt wiederverwendet, kein Neubau. Ebenso
  `IsValidDocument` (Zeile 145-153, `internal static`) — filtert bereits
  `.cs`-Dokumente außerhalb `obj`/`bin`/generierter Dateien; wird für die
  Auswahl der zu überwachenden Dateien wiederverwendet statt eine zweite
  Filterlogik zu bauen.
- **`src/AiNetLinter/Baseline/FileChecksumCalculator.cs`**: `ComputeSha256Hex`
  (statisch, liest die Datei komplett, liefert lowercase-Hex-SHA-256) ist
  exakt die im Konzept geforderte Hash-Funktion — wird direkt wiederverwendet,
  kein zweiter Hash-Mechanismus.
- **Thread-Safety-Vorbild im Projekt:** `src/AiNetLinter/Cache/AnalysisCacheManager.cs`
  nutzt `private readonly Lock _lock = new();` (der neue .NET-`System.Threading.Lock`-
  Typ, nicht `object`) und einfache `lock (_lock) { ... }`-Blöcke um
  Read/Write-Zugriffe auf einen intern gehaltenen Zustand — dasselbe Muster
  wird hier für den Zugriff auf `Solution`/den Staleness-Cache übernommen,
  statt `SemaphoreSlim`/`ReaderWriterLockSlim` neu einzuführen (Konsistenz,
  „Einfachheit vor Abstraktion").
- **Test-Fixture:** `src/AiNetLinter.Tests/Fixtures/BaselineMiniFixtureWorkspace.cs`
  kopiert `tests/Fixtures/BaselineMini/` in ein temporäres, beschreibbares
  Verzeichnis (`RootPath`, `ViolatingClassPath`) — bereits von
  `McpServerCommandTests.cs` (step-001) genutzt. Wird hier wiederverwendet,
  um eine Datei nach dem initialen Laden gezielt auf der Platte zu ändern
  (Voraussetzung für einen echten Staleness-Test).
- **Neuer Ordner `src/AiNetLinter/Mcp/`:** Es gibt noch keinen Ordner für
  MCP-Server-Zustand/-Logik jenseits des CLI-Einstiegspunkts (`Commands/`
  enthält nur dünne Command-Einstiegspunkte, vgl. `MapCommand.cs`/
  `ImpactCommand.cs`). Analog zu `Baseline/`, `Cache/`, `Core/` als eigene
  fachliche Schicht wird hier ein neuer Ordner `Mcp/` angelegt (Namespace
  `AiNetLinter.Mcp`, `EnforceNamespaceDirectoryMapping` aus
  `.agents/rules/AiNetLinter.mdc` verlangt das ohnehin) — das ist die
  Grundlage, auf der EPIC-03 die eigentlichen Tool-Implementierungen
  ablegen wird, statt sie in `Commands/McpServerCommand.cs` anwachsen zu
  lassen (das Datei-Zeilenlimit von 500 aus `AiNetLinter.mdc` würde das
  ohnehin verbieten).
- **Tech-Debt-Index geprüft** (`tech-debt.md`): `TD-001` (ungenutzte
  transitive Abhängigkeit, relevant EPIC-04) und `TD-002` (Subprozess-E2E-Test
  ohne Fixture-Pool, relevant EPIC-07) berühren beide nicht den Bereich
  dieses Steps (Solution-Zustand/Staleness) — keine Wechselwirkung, nichts
  zusätzlich zu beachten.

## Intention

Nach diesem Step hält der MCP-Server die geladene Solution **resident** über
eine neue, zustandshaltende Klasse `McpCodeGraphServer` (kein DI-Container,
direkte Instanziierung wie der Rest des Projekts). Jeder künftige Tool-Call
(EPIC-03) kann darüber die aktuelle `Solution` abfragen; vor der Rückgabe
prüft die Klasse lazy per Hash/mtime, ob sich bekannte Quelldateien seit dem
letzten Zugriff geändert haben, und aktualisiert nur die betroffenen
`Document`s inkrementell über `WithUpdatedSolution` (kein Komplett-Reload der
`MSBuildWorkspace`). Zugriff ist Thread-sicher (ein `Lock`, analog
`AnalysisCacheManager`). Fehlt eine Solution beim Start (Ladefehler aus
step-001), bleibt `McpCodeGraphServer` funktionsfähig, aber im Zustand
"nicht geladen" (`IsLoaded == false`, `GetCurrentSolution()` liefert `null`)
— kein Crash, konsistent mit dem in EPIC-01 etablierten Fehlerpfad.

## Konkrete Änderungen

### Datei 1 (neu): `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`

- **Was:** Neue `sealed class McpCodeGraphServer : IDisposable` im neuen
  Namespace `AiNetLinter.Mcp`.
  - Konstruktor `McpCodeGraphServer(SourceFileCatalog? catalog, ILintConsole? console = null)`
    — `catalog` ist `null`, wenn die Solution beim Start nicht geladen werden
    konnte (Ladefehler-Fall aus step-001). `console` defaultet auf
    `LinterConsole.Instance`, gleiches Muster wie `McpServerCommand.RunAsync`.
  - `bool IsLoaded => _catalog is not null` (public, read-only).
  - `Solution? GetCurrentSolution()` (public, **synchron** — der Staleness-Check
    ist reines synchrones Datei-IO, kein `async` nötig; vermeidet
    "Fake-Async" ohne echten I/O-Vorteil unter einem `lock`, der ohnehin kein
    `await` erlaubt). Unter `lock (_lock)`:
    1. `null` zurückgeben, falls `_catalog is null`.
    2. Alle bekannten Dokumente durchgehen (siehe unten), stale Dateien
       identifizieren und die `Solution` inkrementell aktualisieren.
    3. Die (ggf. aktualisierte) `_catalog.Solution` zurückgeben.
  - Privater Staleness-Cache: `Dictionary<string, FileState> _fileState`
    (Key: absoluter Dateipfad, `StringComparer.OrdinalIgnoreCase` — Windows-
    Dateisystem), `FileState` als privates `readonly record struct
    FileState(DateTime MtimeUtc, string Hash)`.
  - Initialer Aufbau von `_fileState` im Konstruktor (falls `catalog != null`):
    über alle `Project.Documents` der Solution, gefiltert mit dem
    bestehenden `SourceFileCatalog.IsValidDocument(document, solutionDir)`
    (wiederverwendet, kein zweiter Filter), pro Datei einmalig `mtime` +
    `FileChecksumCalculator.ComputeSha256Hex` berechnen. Fehlt die Datei
    bereits beim Start (unwahrscheinlich, aber möglich), wird sie einfach
    nicht in `_fileState` aufgenommen — kein Fehler.
  - Refresh-Logik (privat, aufgerufen aus `GetCurrentSolution()` unter dem
    Lock): pro bekanntem `Document` (erneut über `IsValidDocument` gefiltert,
    diesmal auf der *aktuellen* `_catalog.Solution`):
    1. `File.Exists(path)` prüfen — fehlt die Datei (gelöscht seit letztem
       Zugriff), wird sie **übersprungen**, kein Crash, kein Update dieser
       Runde (siehe "Bekannte Ausnahmen" unten — Behandlung gelöschter/neuer
       Dateien ist bewusst nicht Teil dieses Steps).
    2. `File.GetLastWriteTimeUtc(path)` mit dem gecachten `MtimeUtc`
       vergleichen — identisch → Datei gilt als unverändert, **kein** Hash
       nötig (Performance: Hashing nur bei tatsächlichem mtime-Unterschied,
       nicht bei jedem Call für jede Datei — wichtig bei 100k+ LOC-Solutions,
       siehe `konzept.md` Kontext).
    3. Bei abweichendem `mtime`: `ComputeSha256Hex` neu berechnen. Ist der
       Hash **identisch** zum gecachten Wert (z. B. reines Touch/Re-Save ohne
       Inhaltsänderung), nur `_fileState` mit neuem `mtime` aktualisieren,
       **kein** Solution-Update (vermeidet unnötige Roslyn-Neuparses).
    4. Bei abweichendem Hash: `File.ReadAllText(path)` lesen,
       `SourceText.From(...)` bilden, `solution.WithDocumentText(document.Id, text)`
       auf einer lokalen `Solution`-Variable akkumulieren (mehrere geänderte
       Dateien in einem Durchlauf werden zu **einer** aktualisierten
       `Solution` zusammengefasst, nicht pro Datei ein Zwischenschritt),
       `_fileState` aktualisieren.
    5. Wurde mindestens ein Dokument aktualisiert: `_catalog =
       _catalog.WithUpdatedSolution(updatedSolution)` — genau das im
       Konzept geforderte inkrementelle Update statt Komplett-Reload.
    6. I/O-Fehler beim Lesen/Hashen einer einzelnen Datei (z. B. Datei wird
       gerade von einem anderen Prozess geschrieben, `IOException`): pro
       Datei einzeln abfangen, `console.WriteError("[WARN]: ...")` loggen,
       diese eine Datei in dieser Runde überspringen (kein leeres `catch`,
       kein Absturz des gesamten Refresh-Durchlaufs wegen einer einzelnen
       Datei).
  - `public void Dispose() => _catalog?.Dispose();` — reicht die Disposal an
    den gehaltenen `SourceFileCatalog` (und damit dessen `MSBuildWorkspace`)
    durch, `sealed`, kein Finalizer nötig (kein natives Handle direkt
    gehalten).
- **Warum:** Zentrale, wiederverwendbare Zustandsklasse für EPIC-02 — Basis,
  auf der alle EPIC-03-Tools künftig `GetCurrentSolution()` statt einer
  eigenen Roslyn-Zugriffsschicht aufrufen.

### Datei 2: `src/AiNetLinter/Commands/McpServerCommand.cs`

- **Was:**
  - `TryLoadSolutionAsync` ändert die Rückgabe von `Task` auf
    `Task<SourceFileCatalog?>`: statt den geladenen `catalog` in einem
    `using`-Block sofort zu disposen, wird er **zurückgegeben** (Erfolgsfall)
    bzw. `null` (Ladefehler-Fall, wie bisher nur geloggt). Die bestehende
    `[WARN]`-Logging-Logik bei `HasLoadingErrors`/Exception bleibt inhaltlich
    unverändert.
  - `RunAsync`: nach `TryLoadSolutionAsync(...)` wird `catalog` in
    `using var mcpState = new McpCodeGraphServer(catalog, c);` gewrappt
    (neuer `using AiNetLinter.Mcp;`-Import) — der Zustand bleibt für die
    komplette Laufzeit von `await server.RunAsync(ct)` erhalten und wird erst
    danach (Server-Ende) disposed. `mcpState` wird in diesem Step noch von
    keinem Tool konsumiert (leeres `ToolCollection` bleibt unverändert aus
    step-001) — das ist explizit erwartet, siehe Intention.
- **Warum:** Das ist die eigentliche Lücke aus dem Roadmap-Abgleich —
  `McpServerCommand` hält aktuell keinen wiederverwendbaren State; nach
  diesem Step tut es das über `McpCodeGraphServer`.

## Tests

- [ ] `McpCodeGraphServerTests.GetCurrentSolution_NotLoaded_ReturnsNull` —
      Konstruktion mit `catalog: null`, `IsLoaded` ist `false`,
      `GetCurrentSolution()` liefert `null`, kein Exception.
- [ ] `McpCodeGraphServerTests.GetCurrentSolution_NoFileChanges_ReturnsSameSolutionVersion` —
      Fixture laden (`BaselineMiniFixtureWorkspace` + `SourceFileCatalog.LoadAsync`),
      `McpCodeGraphServer` konstruieren, zweimal `GetCurrentSolution()`
      aufrufen ohne etwas auf der Platte zu ändern — beide Aufrufe liefern
      dieselbe `Solution`-Version (`Solution.GetHashCode()`/`VersionStamp`
      unverändert, kein unnötiges Re-Parse).
- [ ] `McpCodeGraphServerTests.GetCurrentSolution_FileModifiedOnDisk_ReflectsNewContent` —
      Kernstück (Muss-Haben "Lazy Staleness-Invalidierung"): Fixture laden,
      ersten `GetCurrentSolution()`-Call machen, dann
      `fixture.ViolatingClassPath` auf der Platte mit neuem Inhalt
      überschreiben (`File.WriteAllText` + explizites
      `File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2))`, um
      NTFS-mtime-Aufloesung im Test sicher zu unterscheiden), zweiten Call
      machen, das zugehörige `Document` aus der zurückgegebenen `Solution`
      per Pfad auflösen, `GetTextAsync()` prüfen — enthält den **neuen**
      Inhalt, nicht mehr den beim ersten Laden geparsten.
- [ ] `McpCodeGraphServerTests.GetCurrentSolution_FileTouchedWithoutContentChange_SkipsSolutionUpdate` —
      mtime ändern (`File.SetLastWriteTimeUtc`), Inhalt **nicht** ändern,
      zweiter Call liefert dieselbe `Solution`-Version wie vorher (Hash-Check
      verhindert unnötiges Update trotz geändertem mtime).
- [ ] `McpCodeGraphServerTests.GetCurrentSolution_FileDeletedOnDisk_DoesNotThrow` —
      eine bekannte Datei nach dem ersten Call löschen, zweiter Call wirft
      keine Exception und liefert weiterhin die zuletzt bekannte `Solution`
      (alter Dokumentinhalt bleibt bestehen, siehe "Bekannte Ausnahmen").
- [ ] `McpCodeGraphServerTests.GetCurrentSolution_ConcurrentCalls_DoNotThrow` —
      mehrere parallele `Task.Run(() => server.GetCurrentSolution())`
      (`Task.WhenAll`), während parallel dazu die überwachte Datei einmal
      geändert wird — kein Deadlock, keine Exception, Test terminiert
      innerhalb eines vernünftigen Timeouts (Thread-Sicherheit-Nachweis für
      das Muss-Haben "Thread-sicherer Zugriff").
- [ ] `McpServerCommandTests` (bestehend, step-001) bleibt grün — insbesondere
      `TryLoadSolutionAsync_BrokenSlnx_LogsWarningWithoutThrowing` nach der
      Signaturänderung auf `Task<SourceFileCatalog?>` anpassen (Rückgabewert
      `null` im Broken-Fall zusätzlich assertieren; bestehendes
      `[WARN]`-Verhalten bleibt unverändert) und
      `RunAsync_ValidFixture_ServerRespondsWithEmptyToolList` unverändert
      grün (Server startet weiterhin, Tool-Liste weiterhin leer).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build AiNetLinter.slnx` grün (0 Warnungen, `TreatWarningsAsErrors`)
- [ ] `dotnet test AiNetLinter.slnx` grün, inkl. aller neuen
      `McpCodeGraphServerTests`-Fälle und der angepassten
      `McpServerCommandTests`
- [ ] Commit auf aktuellem Branch (Conventional Commit, Englisch,
      Suffix `[codegraph-mcp]`, siehe Tech-Stack-Notiz in `roadmap.md`)
- [ ] `step-002/step-result.md` geschrieben
- [ ] `status` in diesem `step-plan.md` von `open`/`in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#2` (Architektur-Verbote) — kein
  DI-Container: `McpCodeGraphServer` wird direkt instanziiert
  (`new McpCodeGraphServer(catalog, c)` in `McpServerCommand.RunAsync`), kein
  `IServiceCollection`, keine Registrierung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` (Qualitätsdrift-Prävention) —
  Zero-Warning-Direktive (`TreatWarningsAsErrors`), Result-Pattern-Präferenz
  (hier: `Solution?`/`SourceFileCatalog?` als Nullable-Rückgabe statt
  Exception für den erwartbaren "nicht geladen"-Fall, konsistent mit dem in
  step-001 etablierten Muster).
- `.agents/rules/AiNetLinter.mdc` (Kurz-Stil/Grenzwerte) — `sealed` für
  `McpCodeGraphServer`, `#nullable enable` am Dateianfang, kein leeres
  `catch` (I/O-Fehler beim Refresh werden geloggt, nicht verschluckt),
  `MaxMethodParameterCount` ≤4 (Konstruktor hat 2 Parameter),
  `MaxBoolParameterCount` 1 (keine `bool`-Parameter in der neuen Klasse),
  `EnforceNamespaceDirectoryMapping` — Namespace `AiNetLinter.Mcp` für Dateien
  unter `src/AiNetLinter/Mcp/`, `MaxMethodLineCount` 60 (Refresh-Methoden
  klein genug halten, ggf. weiter in private Hilfsmethoden aufteilen statt
  eine große Methode).

## Bekannte Ausnahmen

- **Gelöschte/neu hinzugekommene Dateien werden in diesem Step bewusst
  nicht behandelt:** Der Staleness-Check erkennt nur *geänderten Inhalt*
  bekannter, weiterhin existierender Dateien (Konzept-Formulierung:
  "Änderung an einer Quelldatei"). Eine zwischen zwei Calls **gelöschte**
  Datei wird beim Refresh übersprungen (alter Solution-Stand bleibt
  bestehen, kein Crash) — eine **neu hinzugekommene** Datei wird gar nicht
  erst erkannt, da `_fileState`/die Iteration nur über bereits in der
  `Solution` bekannte `Document`s läuft, nicht über das Dateisystem direkt.
  Das ist eine bewusste Abgrenzung: `konzept.md`s Definition-of-Done-Punkt zu
  Staleness spricht explizit nur von "Änderung an einer Quelldatei", nicht
  von Hinzufügen/Löschen ganzer Dateien; vollständige Solution-Struktur-
  Änderungen (neue/gelöschte `.cs`-Dateien) würden ohnehin eher zu EPIC-06
  (Robustheit bei Compile-/Solution-Fehlern) gehören als zu EPIC-02. Sollte
  der Kritiker das als fehlenden Muss-Haben-Punkt werten, ist das ein
  Ebene-4-Finding zum Klären — der Planer stuft es hier bewusst als
  außerhalb des engeren EPIC-02-Scopes ein, nicht als Auslassung.

## Code-Skizze (optional)

```csharp
// src/AiNetLinter/Mcp/McpCodeGraphServer.cs (Auszug, Kernmethode)
public Solution? GetCurrentSolution()
{
    lock (_lock)
    {
        if (_catalog is null) return null;

        var solutionDir = Path.GetDirectoryName(_catalog.Solution.FilePath);
        var updated = _catalog.Solution;
        var anyChanged = false;

        foreach (var project in _catalog.Solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
                if (TryRefreshDocument(document, ref updated)) anyChanged = true;
            }
        }

        if (anyChanged)
        {
            _catalog = _catalog.WithUpdatedSolution(updated);
        }

        return _catalog.Solution;
    }
}
```

## Notes

- Diese Unit-Tests decken bereits einen Teil dessen ab, was `roadmap.md`
  EPIC-07 als "Unit-Tests für die Staleness-Invalidierung" vorsieht. Das ist
  **kein** Scope-Vorgriff auf EPIC-07, sondern schlicht notwendig, um EPIC-02s
  eigenes Muss-Haben ("Lazy Staleness-Invalidierung") in diesem Step
  überhaupt abnahmefähig zu machen — EPIC-07 wird darauf aufbauend noch
  Integrationstests **je Tool** ergänzen (die es hier naturgemäß noch nicht
  geben kann, da EPIC-03 die Tools erst liefert).
- Bewusst **kein** `FileSystemWatcher` (siehe `konzept.md` "Verworfene
  Alternativen") — die Staleness-Prüfung passiert ausschließlich lazy beim
  Aufruf von `GetCurrentSolution()`, nie im Hintergrund.
- `McpCodeGraphServer` wird in diesem Step **nicht** an tatsächliche
  MCP-Tools angebunden (es gibt noch keine) — es wird lediglich in
  `McpServerCommand.RunAsync` instanziiert und über die Server-Laufzeit
  offengehalten (`using`-Block um `server.RunAsync(ct)`), damit die
  Anbindung in EPIC-03 nur noch "Tool ruft `mcpState.GetCurrentSolution()`
  auf" bedeutet, statt den State selbst neu zu bauen. Falls das dem Kritiker
  wie ein noch "totes" Objekt vorkommt: das ist beabsichtigt, siehe
  Intention — die Alternative (State-Klasse erst in EPIC-03 zusammen mit dem
  ersten Tool bauen) würde EPIC-02 und EPIC-03 wieder vermischen, genau das,
  was die Epic-Trennung aus step-001 vermeiden sollte.
- Performance-Hinweis (kein Muss-Haben für diesen Step, aber bewusst im
  Design berücksichtigt): der initiale Aufbau von `_fileState` hasht beim
  Start **alle** Quelldateien einmal — das ist ein Fixkosten-Faktor,
  vergleichbar mit dem ohnehin viel teureren initialen MSBuild-Solution-Load.
  Folge-Calls hashen nur Dateien mit geändertem `mtime`. Sollte sich bei
  EPIC-09 (Praxistest gegen ~160k LOC) zeigen, dass das relevant ist, wäre
  das ein Kandidat für einen Tech-Debt-Eintrag, nicht für eine vorgezogene
  Optimierung hier.
