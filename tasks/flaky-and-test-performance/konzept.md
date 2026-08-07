---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-06
open_questions:
  - "Scope: Nur Test-Infrastruktur umbauen, oder auch Produktionscode anfassen, wenn das die Testbarkeit/Performance handfest verbessert?"
  - "Ist ein bewusster Split 'schneller Feedback-Loop' vs. 'vollständiger/CI-Lauf' akzeptabel, oder muss der EINE `dotnet test`-Befehl insgesamt schnell werden?"
  - "Gibt es ein ungefähres Zeitbudget als Zielgröße (z. B. Sekunden für den schnellen Loop, akzeptable Obergrenze für den vollen Lauf)?"
---

# Konzept: Testsuite-Performance + Flaky-Test-Fix

## Ziel (Was)

Zwei zusammenhängende Probleme der Testsuite (`src/AiNetLinter.Tests/`) beheben:

1. **Performance:** Ein voller `dotnet test`-Lauf dauert aktuell ~90 Sekunden für ~1300-1325 Tests und wird vom Nutzer als deutlich zu langsam empfunden. Der Code soll weiterhin durch Tests abgesichert bleiben (kein Abbau von Testabdeckung) — aber das Testen selbst soll spürbar schneller werden.
2. **Flaky Test:** Ein konkreter, bekannter, intermittierend fehlschlagender Test soll zuverlässig gemacht werden.

## Warum / Kontext

Der Nutzer vermutet als Hauptursache der Langsamkeit, dass die Solution wiederholt in Roslyn/MSBuildWorkspace geladen wird — ausdrücklich als **Vermutung** markiert, nicht als Faktum. Eine erste Recherche (siehe „Wo im Projekt") bestätigt die Grundrichtung, präzisiert sie aber: Es ist nicht *eine* Solution, die wiederholt geladen wird, sondern **~60-80 strukturell unabhängige, nie zwischen Testklassen geteilte** Lade-/Subprozessvorgänge (überwiegend Mini-Fixtures, echtes Repo nur in 2 Klassen), die zusätzlich durch ein globales Subprozess-Gate (max. 6 gleichzeitig) serialisiert werden.

Der Flaky Test hängt vermutlich mit demselben Grundproblem zusammen: er pollt mit festem 5s-Timeout auf einen Hintergrund-Task-Übergang (`Task.Run`-Scheduling), der unter der Systemlast der übrigen, gleichzeitig laufenden Subprozess-/Workspace-Tests (Thread-Pool-Konkurrenz) gelegentlich nicht rechtzeitig durchläuft.

Der Nutzer ist ausdrücklich offen für strukturelle Umbauten ("wir können gerne Dinge umstellen") und für Exploration als Teil des Plans, falls die genaue Lösung noch nicht feststeht.

## Scope

### Muss-Haben

- Voller Testlauf spürbar schneller, ohne Verlust an Testabdeckung (kein Streichen von Tests, um Zeit zu sparen).
- Der bekannte Flaky Test (`McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`, siehe „Wo im Projekt") läuft zuverlässig durch — auch unter der vollen Last des restlichen Testlaufs, nicht nur isoliert.
- <Rest hängt von den offenen Fragen ab — wird nach Runde 1 präzisiert.>

### Nice-to-Have (optional, spätere Iteration)

- Konsequente Category-Traits (`Unit`/`Integration`) auf allen Tests, damit gezielt gefiltert werden kann (aktuell >90 % der Tests ungetraggt, siehe „Entdeckte Mängel").
- CI-Workflow, der Tests tatsächlich ausführt (aktuell führt `.github/workflows/release.yml` keine Tests aus).

### Non-Goals (bewusst NICHT Teil davon)

- <noch offen — hängt von der Antwort auf die Scope-Frage oben ab>

## Zielplattformen / Technischer Rahmen

.NET 10, xUnit v3, bestehende Test-Infrastruktur unter `src/AiNetLinter.Tests/Fixtures/`. Kein neuer Test-Runner, keine neue Sprache — Frage ist, ob eine **zusätzliche Projekt-/Fixture-Struktur** (z. B. getrenntes schnelles Testprojekt oder Collection-Fixtures) sinnvoll ist, siehe offene Fragen.

## Verworfene Alternativen

<Noch keine — wird in der nächsten Runde ergänzt, sobald über die Lösungsrichtung gesprochen wurde.>

## Wo im Projekt

Ergebnis einer gezielten Recherche (Pointer-Prinzip — Fundstellen, keine Verhaltens-Garantien):

**Fehlendes Fixture-Sharing (Hauptverdacht für Performance):**
- Projektweit **0 Verwendungen von `ICollectionFixture`** — jede Testklasse mit `IClassFixture<T>` bekommt eine eigene, neue Instanz. xUnit cached nichts über Klassengrenzen hinweg.
- `IClassFixture<SymbolGraphCatalogFixture>` in 18 Testklassen — je eine eigene `MSBuildWorkspace` auf einer Mini-Solution.
- `IClassFixture<SymbolGraphMcpFixture>` in 6 Testklassen — je ein eigener `AiNetLinter.exe`-Subprozess auf einer Mini-Solution (`src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpFixture.cs:15-36`).
- `IClassFixture<McpLiveRepositoryFixture>` in 2 Testklassen (`McpDocumentationSmokeTests`, `McpLiveRepositoryTests`) — je ein eigener Subprozess auf dem **echten** `AiNetLinter.slnx` (`src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs:15-49`) — vermutlich die schwersten Einzel-Ladevorgänge im Lauf.
- `IClassFixture<BaselineMcpFixture>` (1 Klasse), `IClassFixture<BaselineCatalogFixture>` (1 Klasse) — analog.

**Subprozess-Starts außerhalb von Fixtures:**
- `src/AiNetLinter.Tests/Fixtures/CliProcessRunner.cs:73-89` (`RunLinterAsync`) — startet `dotnet AiNetLinter.dll ...` je Aufruf (Muxer-Overhead zusätzlich zum Prozessstart); ca. 22-30 Aufrufe verteilt über `CliIntegrationTests.cs`, `FilterCliIntegrationTests.cs`, `BaselineCliTests.cs`, `CliBatchRegressionTests.cs`, `DisableAllCliTests.cs`, `WebBaselineTests.cs`.
- `src/AiNetLinter.Tests/Baseline/SourceFileCatalogRegisterMSBuildTests.cs:50-97` — 20 parallele `SourceFileCatalog.LoadAsync`-Aufrufe **ohne** Gate-Schutz (Gate deckt nur Subprozesse ab, keine In-Process-Loads).
- `src/AiNetLinter.Tests/Fixtures/LoadFixtureMeasurementsTests.cs:31-98` — 2 Tests mit synthetischen 1k-/5×200-Datei-Solutions, bewusst bis 30s/5s einkalkuliert.

**Serialisierung/Kontention:**
- `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs:15-57` — globales `SemaphoreSlim(6,6)`, begrenzt gleichzeitige `.exe`-Subprozessstarts (60s Wait-Timeout). Grund: `MSBuildLocator` ist prozessglobaler State (Kommentar in `SourceFileCatalog.cs:230-233`). Kapazität wurde laut Kommentar bereits von 4 auf 6 erhöht.
- `src/AiNetLinter.Tests/Mcp/McpTestClientParallelTests.cs:18-37` (`ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly`) — startet 16 echte Subprozesse parallel, durch das 6er-Gate auf mind. 3 serialisierte Runden begrenzt; taucht in Läufen regelmäßig als "Long Running Test" (>1 Min.) auf — plausibel Kontention um die geteilte Ressource, kein Bug im Test selbst.

**Der Flaky Test:**
- `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs:112-150` (`LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`) — pollt mit `Thread.Sleep(25)` bis zu 5s auf einen `LoadState`-Übergang, der von einem via `Task.Run` gescheduleten Hintergrund-Continuation abhängt (Kommentar im Test dokumentiert das Timing-Fenster selbst als bekannt fragil). Läuft isoliert immer grün, schlägt unter voller Last des restlichen Laufs gelegentlich fehl (reproduziert: 2 von 10 bzw. 6 von 10 Wiederholungen in dieser Session) — Hypothese: Thread-Pool-Konkurrenz durch die vielen gleichzeitig laufenden Subprozess-/Workspace-Tests verzögert die Continuation über die 5s-Deadline hinaus.

**CI/Traits:**
- `.github/workflows/release.yml` — einziger Workflow, reiner Build/Publish bei Tag-Push, **führt keine Tests aus**.
- `src/AiNetLinter.Tests/xunit.runner.json` — `parallelizeTestCollections: true`, `maxParallelThreads: 0` (Prozessorzahl), `longRunningTestSeconds: 3`.
- Von ~1087 Testmethoden im Quellcode sind nur 86 mit `[Trait("Category", ...)]` versehen (67 `Unit`, 19 `Integration`) — über 90 % ungetraggt, dadurch aktuell nicht selektiv filterbar.

## Entdeckte Mängel/Redundanzen

- **Ungenutztes Fixture-Sharing-Potenzial**
  - **Gefunden:** `SymbolGraphCatalogFixture` wird 18×, `SymbolGraphMcpFixture` 6× unabhängig instanziiert (je `IClassFixture`), obwohl die zugrunde liegende Mini-Solution/der Mini-Subprozess zwischen den Testklassen identisch sein dürfte.
  - **Bezug:** Kein explizites Regel-Zitat aus `.agents/rules/AiNetLinter.mdc` (betrifft Testcode, nicht die dort dokumentierten Produktionscode-Regeln) — aber offensichtliches Wiederverwendungspotenzial (xUnit `ICollectionFixture` existiert genau für diesen Fall).
  - **Vorschlag:** Prüfen, ob eine Umstellung auf `ICollectionFixture` (eine geteilte Instanz pro Collection statt pro Klasse) möglich ist, ohne Test-Isolation zu gefährden (z. B. wenn Tests den Fixture-State mutieren).
  - **Entscheidung:** offen — hängt von der Antwort auf die Scope-Frage ab (siehe offene Fragen oben).

- **Tote Serialisierungs-Infrastruktur**
  - **Gefunden:** `ConsoleTestCollection`-Definition existiert (`src/AiNetLinter.Tests/.../ConsoleTestCollection.cs:3`), wird aber nirgends mehr referenziert — `DisableParallelization` greift dadurch aktuell nirgends, ersetzt durch `SubprocessConcurrencyGate`.
  - **Bezug:** kein `rules_dir`-Zitat nötig — offensichtlicher toter Code.
  - **Vorschlag:** Entfernen, wenn im Zuge dieser Aufgabe ohnehin an der Fixture-/Parallelitäts-Struktur gearbeitet wird.
  - **Entscheidung:** offen.

- **Fehlende Category-Traits auf >90 % der Tests**
  - **Gefunden:** Nur 86 von ~1087 Testmethoden tragen `[Trait("Category", ...)]`.
  - **Bezug:** kein `rules_dir`-Zitat — verhindert aber gezieltes Filtern (`--filter Category=Unit`) als eigenständige Gegenmaßnahme zur Langsamkeit.
  - **Vorschlag:** Konsequent nachziehen, unabhängig davon, welche strukturelle Lösung für das Kernproblem gewählt wird.
  - **Entscheidung:** offen, siehe Nice-to-Have oben.

## Wie (grober Ansatz)

<Bewusst noch nicht ausgearbeitet — der Nutzer hat explizit gesagt, dass eine Exploration Teil des Plans sein soll, falls die Lösung nicht feststeht. Grobe Kandidaten, die zur Diskussion stehen (keine Vorfestlegung):
1. Fixture-Sharing einführen (`ICollectionFixture` statt `IClassFixture` wo möglich) — reduziert Zahl unabhängiger Ladevorgänge, ohne Testabdeckung zu ändern.
2. Category-Traits konsequent nachziehen + Doku/Skript für "schneller Loop" (`--filter Category=Unit`) vs. "voller Lauf".
3. Getrenntes, bewusst kleines/schnelles Testprojekt oder Test-Segment für die Aspekte, die aktuell nur über schwere Subprozess-/Workspace-Fixtures abgedeckt sind (Nutzer-Vorschlag) — vs. bestehende Struktur behalten und nur Sharing/Traits verbessern. Diese Weichenstellung ist der Kern der ersten offenen Frage.
4. Flaky-Test-Fix: entweder das Timing-Problem strukturell lösen (z. B. `TaskCompletionSource`/Event statt Poll-Loop mit fixer Deadline) oder — falls das Grundproblem (Thread-Pool-Kontention) durch 1-3 entschärft wird — beobachten, ob der Flake dadurch bereits verschwindet, und nur bei Bedarf zusätzlich am Test selbst nachschärfen.
Die konkrete Auswahl/Kombination folgt nach der ersten Fragerunde.>

## Definition of Done / Erfolgskriterien

<Wird nach Klärung der offenen Fragen (insbesondere Zeitbudget) präzisiert. Fest steht bereits:
- Kein Testabdeckungsverlust (Testanzahl bleibt mindestens gleich, keine Assertions ersatzlos gestrichen, um Zeit zu sparen).
- `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` läuft in mindestens 10 aufeinanderfolgenden vollen Testläufen (nicht isoliert) fehlerfrei durch.
- `dotnet build` (TreatWarningsAsErrors) und der volle Testlauf bleiben grün.>

## Offene Punkte

Siehe `open_questions` im Frontmatter — erste Fragerunde noch ausständig.
