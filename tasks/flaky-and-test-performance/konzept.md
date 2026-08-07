---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-06
open_questions: []
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

**Entscheidungen aus Runde 1:** Produktionscode darf angefasst werden, wenn es Testbarkeit/Performance handfest verbessert (kein reiner Test-Infrastruktur-Zwang). Ein bewusster Split „schneller Feedback-Loop" (Alltag) vs. „vollständiger Lauf" (seltener/CI) ist akzeptabel — der eine `dotnet test`-Befehl über alle ~1300 Tests muss nicht selbst durchgängig schnell werden. Kein festes Zeitbudget als Zielzahl — „deutlich besser als jetzt" ist das Kriterium.

### Muss-Haben

- Ein spürbar schnellerer Feedback-Loop-Pfad für die tägliche Arbeit (z. B. gefilterter Lauf über die überwiegende Mehrheit der Tests, ohne echte Subprozess-/Solution-Ladevorgänge) — ohne Verlust an Testabdeckung (kein Streichen von Tests, um Zeit zu sparen; ausgelagerte/geskippte Tests laufen weiterhin im vollen Lauf).
- Konsequente Category-Traits (`Unit`/`Integration`, ggf. weitere Kategorie für "startet echten Subprozess/lädt echte Solution") auf **allen** Tests — aktuell nur 86 von ~1087 Testmethoden getraggt. Das ist Voraussetzung für einen gezielt filterbaren Fast-Path, nicht nur Aufräumarbeit.
- Reduktion der ~60-80 unabhängigen, nie geteilten Lade-/Subprozessvorgänge — mindestens für die Fälle mit eindeutig identischer Fixture (`SymbolGraphCatalogFixture` 18×, `SymbolGraphMcpFixture` 6×) auf geteilte Instanzen umstellen (`ICollectionFixture` oder gleichwertig), soweit das ohne Aufgabe der Test-Isolation möglich ist.
- Der volle Testlauf (aktuell ~90s) wird spürbar kürzer, auch wenn kein Split genutzt wird.
- Der bekannte Flaky Test (`McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`, siehe „Wo im Projekt") läuft zuverlässig durch — auch unter der vollen Last des restlichen Testlaufs, nicht nur isoliert. Fix strukturell (z. B. Event-/Continuation-basiertes Warten statt Poll-Loop mit fixer 5s-Deadline), nicht durch Hochsetzen der Deadline oder Ausklammern aus dem Volllauf.
- Produktionscode-seitige Verbesserungen an `SourceFileCatalog`/`McpCodeGraphServer` (o. ä.) sind erlaubt, wenn sie Ladevorgänge testbarer oder wiederverwendbarer machen — dürfen aber das für Nutzer/Agenten sichtbare Verhalten des MCP-Servers/CLI nicht verändern (siehe Non-Goals).

### Nice-to-Have (optional, spätere Iteration)

- CI-Workflow, der Tests tatsächlich ausführt (aktuell führt `.github/workflows/release.yml` keine Tests aus) — naheliegende Folge, sobald ein schneller/voller Split etabliert ist, aber nicht Teil dieser Aufgabe.
- Entfernen der toten `ConsoleTestCollection`-Infrastruktur (siehe „Entdeckte Mängel") — falls es sich im Zuge des Fixture-Umbaus ergibt, sonst eigener Kleinst-Task.

### Non-Goals (bewusst NICHT Teil davon)

- Wechsel des Test-Frameworks (bleibt xUnit v3) — reine Struktur-/Sharing-Frage, kein Tooling-Wechsel.
- Sichtbares Verhalten des MCP-Servers/CLI ändern — Produktionscode-Änderungen sind nur im Rahmen von Testbarkeit/Performance erlaubt, nicht als Gelegenheit für Feature-Änderungen.
- Ein festes, verhandeltes Zeitbudget als Abnahmekriterium (siehe Scope-Entscheidung oben) — Erfolg wird qualitativ ("deutlich besser") plus den konkreten Muss-Haben-Punkten gemessen, nicht an einer Sekundenzahl.
- Einführung eines tatsächlichen CI-Test-Workflows in dieser Aufgabe (siehe Nice-to-Have) — die Testsuite muss dafür bereit sein, der Workflow selbst ist ein Folge-Task.

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

Reihenfolge/Kombination ist Sache des Planers im drift-loop (Datei-/Zeilen-genau), hier nur die grobe Richtung inkl. eines empfohlenen Explorations-Schritts vorab:

1. **Explorations-/Spike-Schritt zuerst** (vom Nutzer explizit gewünscht, falls die Lösung nicht feststeht): 2-3 der am stärksten duplizierten Fixtures (`SymbolGraphCatalogFixture`, `SymbolGraphMcpFixture`) probeweise auf `ICollectionFixture` umstellen und die tatsächliche Zeitersparnis + eventuelle Isolationsprobleme (Tests, die den Fixture-State mutieren und sich dadurch gegenseitig stören) messen, **bevor** der große Umbau über alle ~28 betroffenen Klassen committed wird. Ergebnis entscheidet, ob Sharing im bestehenden Projekt reicht oder ob zusätzlich ein separates schnelles Testsegment (Nutzer-Ausgangsidee) nötig ist.
2. **Category-Traits nachziehen** — alle ~1000 ungetraggten Tests einordnen (`Unit` vs. `Integration`/„startet Subprozess"), damit ein Fast-Path per `--filter` überhaupt möglich wird. Kann parallel/unabhängig von Schritt 1 laufen.
3. **Fixture-Sharing umsetzen**, geleitet vom Spike-Ergebnis aus Schritt 1 — vermutlich `ICollectionFixture` für die klar duplizierten Fälle, ggf. mit expliziten `[Collection]`-Zuweisungen, um die bestehende Parallelität (`parallelizeTestCollections: true`) nicht zu zerstören.
4. **Fast-Path etablieren** — dokumentierter/skriptierter Befehl (z. B. `dotnet test --filter Category!=Integration`) für den Alltag; voller Lauf bleibt verfügbar und grün, wird aber seltener gebraucht.
5. **Produktionscode-Seite prüfen** — ob `SourceFileCatalog`/`McpCodeGraphServer` einen leichteren, mockbaren Lade-Pfad für Tests bekommen können, die keinen echten `.exe`-Subprozess brauchen (z. B. für Tests, die aktuell nur wegen Testbarkeit einen Subprozess starten, nicht weil sie das Subprozess-Verhalten selbst prüfen wollen).
6. **Flaky-Test-Fix**, unabhängig von 1-5 möglich, aber vermutlich einfacher zu verifizieren, sobald die allgemeine Systemlast durch 1-5 sinkt: Poll-Loop mit fixer Deadline durch ein `TaskCompletionSource`/Event-basiertes Warten auf den `LoadState`-Übergang ersetzen (kein Raten über Sleep-Intervalle), damit der Test nicht mehr von Thread-Pool-Timing abhängt.
7. Tote `ConsoleTestCollection`-Infrastruktur entfernen, falls im Zuge von 3 ohnehin angefasst.

## Definition of Done / Erfolgskriterien

- Kein Testabdeckungsverlust — Testanzahl bleibt mindestens gleich, keine Assertions ersatzlos gestrichen, um Zeit zu sparen.
- Voller Testlauf spürbar kürzer als die aktuelle ~90s-Baseline (kein festes Zahlenziel, siehe Non-Goals — aber messbar besser, mit Vorher/Nachher-Zahl im Ergebnis dokumentiert).
- Ein dokumentierter, spürbar schnellerer Fast-Path-Befehl existiert und deckt weiterhin alle `Unit`-Aspekte ab.
- `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` läuft in mindestens 10 aufeinanderfolgenden **vollen** Testläufen (nicht isoliert) fehlerfrei durch.
- Alle Tests tragen einen Category-Trait (keine ungetraggten Tests mehr).
- `dotnet build` (TreatWarningsAsErrors) und der volle Testlauf bleiben grün; Self-Lint bleibt `OK`.

## Offene Punkte

Keine — Runde 1 hat die drei Grundsatzfragen geklärt (Scope, Split, Zeitbudget). Feindetails (genaue Trait-Taxonomie, welche Fixtures im Detail umgestellt werden) sind Sache des Planers im drift-loop, kein Blocker für `status: ready`.
