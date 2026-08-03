---
status: done
type: step-plan
task: codegraph-mcp-finish
step: 002
title: "Testsuite-Performance: CliProcessRunner-Helper für Subprozess-Teststellen (F.2)"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-03
related_to: ["step-001"]
---

# Step 002: Testsuite-Performance — CliProcessRunner-Helper (F.2)

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-01` aus `roadmap.md` — Testsuite-Performance (Block F,
  Unterpunkte F.1-F.6). F.1 ist mit step-001 (`approved`) abgeschlossen.
  Dieser Step deckt **ausschließlich F.2** ab — F.3-F.6 bleiben für
  spätere Steps desselben Epics offen.
- **Konzept-Referenz:** `Konzept.md` Muss-Haben F, Punkt 2 ("Geteilter/
  gepoolter Subprozess für CLI-Integrationstests"), deckt sich mit
  Muss-Haben D / TD-002 ("Subprozess-E2E-Test ohne Fixture-Pool") — wird
  hier mitgelöst, nicht separat in Block D offen geführt.

## Aktueller Projektzustand (JIT-Kontext)

Verifiziert gegen den tatsächlichen Code (nicht nur gegen die
Konzept-Beschreibung):

- **Grep nach `Process.Start`/`ProcessStartInfo` bestätigt exakt die 8 im
  Konzept gelisteten Dateien**, keine mehr, keine weniger:
  `Baseline/BaselineCliTests.cs`, `Baseline/WebBaselineTests.cs`,
  `Cli/CliIntegrationTests.cs`, `Cli/FilterCliIntegrationTests.cs`,
  `Commands/CliBatchRegressionTests.cs`,
  `Commands/McpServerCommandAmbiguityE2ETests.cs`,
  `Fixtures/GitImpactMiniFixtureWorkspace.cs`,
  `Suppression/DisableAllCliTests.cs`.
- **Drei unterschiedliche Nutzungsmuster, kein einheitlicher Fall:**
  1. **6 Dateien** (`BaselineCliTests`, `WebBaselineTests`,
     `CliIntegrationTests`, `FilterCliIntegrationTests`,
     `CliBatchRegressionTests`, `DisableAllCliTests`) starten `dotnet
     <AiNetLinter.dll-Pfad> <arguments>` und haben dafür **je eine fast
     identische private `FindSolutionRoot()`/`FindLinterDll()`-Methodenpaar-
     Kopie** (kleine Abweichungen: mal `AppDomain.CurrentDomain.
     BaseDirectory`, mal `AppContext.BaseDirectory`; mal
     `OrderByDescending(File.GetLastWriteTimeUtc).First()`, mal
     `files[0]`/`files.OrderByDescending(f => new
     FileInfo(f).LastWriteTimeUtc).First()`) — das ist die eigentliche
     DRY-Verletzung, die F.2/TD-002 beheben sollen.
  2. **`BaselineCliTests`, `CliIntegrationTests`, `FilterCliIntegrationTests`,
     `CliBatchRegressionTests`** rufen bereits `SubprocessConcurrencyGate.
     AcquireAsync()` vor `Process.Start` auf (aus step-001).
     **`WebBaselineTests` und `DisableAllCliTests` tun das nicht** — beide
     haben synchrone (nicht-`async`) Testmethoden/Hilfsmethoden, die
     `Process.Start`+`WaitForExit()` blockierend aufrufen, ohne jede
     Gate-Absicherung. Das ist eine Lücke aus step-001 (Konzept F.2 listet
     beide explizit als Ziel), keine Neuentdeckung dieses Steps.
  3. **`McpServerCommandAmbiguityE2ETests`** startet `AiNetLinter.exe`
     direkt (nicht über `dotnet <dll>`), mit eigenen Argumenten
     (`--mcp-server --path ...`), liest **nur stderr** (kein stdout-Bedarf)
     und nutzt `process.WaitForExit(TimeSpan.FromSeconds(10))` mit
     explizitem Timeout-Assert statt blockierendem `WaitForExit()` — ein
     grundsätzlich anderes Aufrufmuster als die 6 „dotnet dll"-Dateien.
  4. **`GitImpactMiniFixtureWorkspace.RunGit`** startet **kein**
     `AiNetLinter.exe`, sondern `git` (Working-Directory-gebunden,
     `BeginOutputReadLine`-Event-Pattern), aus einem **Konstruktor**
     heraus (`InitializeGitRepoWithInitialCommit()`), also zwingend
     synchron — ein `async`-Umbau des Konstruktors ist nicht ohne
     Restrukturierung (z. B. Async-Factory-Pattern) möglich und würde den
     Scope dieses reinen Boilerplate-Steps sprengen (Non-Goal
     „Keine Änderung an Testinhalten/Assertions" schließt eine solche
     API-Umstrukturierung implizit aus). Kein Ressourcen-Konkurrenz-Bedarf
     wie bei `AiNetLinter.exe` (git-Aufrufe sind kurzlebig, kein
     MSBuild-Overhead) — die Gate-Absicherung ist hier fachlich nicht
     nötig.
- **`SubprocessConcurrencyGate.cs`** (`src/AiNetLinter.Tests/Fixtures/`,
  aus step-001) existiert bereits, ist `public static`, `AcquireAsync()`
  liefert ein `IDisposable`-Lease. **Wird in diesem Step nicht gelöscht
  oder umbenannt** — `McpTestClient.ConnectAsync` (nicht Teil der 8
  F.2-Dateien) ruft sie direkt und unabhängig von `CliProcessRunner` auf,
  das bleibt unverändert.
- **`SubprocessConcurrencyGate.cs:9-13`/`:21-26`** (Klassen-/Methoden-Doc)
  beschreibt einen Vertrag ("Der Aufrufer haelt das Handle fuer die
  gesamte Laufzeit des zugehoerigen Subprozesses"), der laut
  step-001-Review (`step-review.md`, „Sonstige Beobachtungen", MINOR) seit
  step-001 durch `McpTestClient.cs` bereits widerlegt ist (Lease wird dort
  nur für Start+Handshake gehalten). Der Review empfiehlt explizit, das
  „bei einer der nächsten Berührungen dieser Datei (z. B. F.2)" zu
  korrigieren — dieser Step berührt `SubprocessConcurrencyGate.cs`
  ohnehin (neuer interner Aufrufer `CliProcessRunner`), also wird die
  Doku hier mitkorrigiert (siehe „Konkrete Änderungen").
- **Kein bestehender `CliProcessRunner` oder vergleichbarer Helper** in
  `src/AiNetLinter.Tests/Fixtures/` — verifiziert, nur die bestehenden
  `*CatalogFixture`/`*Fixture`-Klassen (In-Process-Setup, kein
  Subprozess-Bezug) und `SubprocessConcurrencyGate` selbst.
- `BanBlockingTaskAccess` (aus `AiNetLinter.mdc`, agent-resilience) verbietet
  `.Wait()`/`.Result`/`.GetAwaiter().GetResult()` — ein rein synchroner
  `CliProcessRunner`-Wrapper, der intern einen asynchronen Gate-Call
  blockierend abwartet, ist damit **nicht zulässig**. Für
  `WebBaselineTests`/`DisableAllCliTests` (aktuell `void`/synchrone
  Testmethoden) bedeutet das: ihre betroffenen Testmethoden werden auf
  `async Task` umgestellt (rein mechanisch, keine Assertion-Änderung) statt
  einen regelwidrigen Sync-über-Async-Wrapper zu bauen. Für
  `GitImpactMiniFixtureWorkspace.RunGit` (Konstruktor-Kontext, kein
  `async` möglich) bleibt ein echt-synchroner Pfad ohne jeden `Task`-Bezug
  nötig — das verstößt nicht gegen `BanBlockingTaskAccess` (die Regel
  betrifft blockierenden Zugriff auf einen bereits laufenden `Task`, nicht
  rein synchronen Code ohne `Task`).

## Intention

Nach diesem Step gibt es einen einzigen `CliProcessRunner`-Helper
(`src/AiNetLinter.Tests/Fixtures/CliProcessRunner.cs`), der die 6
duplizierten `FindSolutionRoot()`/`FindLinterDll()`-Implementierungen
sowie das wiederholte `ProcessStartInfo`-Aufbau-/Gate-Acquire-/
Output-Capture-Boilerplate konsolidiert. Alle 8 im Konzept gelisteten
Dateien nutzen ihn — die 6 „dotnet dll"-Fälle über eine
Komfort-Methode, `McpServerCommandAmbiguityE2ETests` über eine
generische, Timeout-fähige Methode, `GitImpactMiniFixtureWorkspace` über
eine synchrone, gate-freie Variante. `SubprocessConcurrencyGate` bleibt
als eigenständige Klasse bestehen (kein Merge), wird aber **innerhalb**
von `CliProcessRunner` verwendet, statt dass jede Testklasse sie selbst
aufruft — das ist die im step-001-Notes-Abschnitt beschriebene „interne
Aufnahme" durch F.2. Verhalten/Assertions aller 8 Testdateien bleiben
unverändert (Non-Goal aus `Konzept.md`, reines
Boilerplate-/Organisations-Refactoring).

## Konkrete Änderungen

### Datei 1 (neu): `src/AiNetLinter.Tests/Fixtures/CliProcessRunner.cs`

- **Was:** Statische Hilfsklasse mit:
  - `FindSolutionRoot()` / `FindLinterDll(string rootDir)` — konsolidierte,
    einzige Implementierung (Basis: die bereits mehrfach vorhandene
    Grundform; `OrderByDescending(...LastWriteTimeUtc).First()` als
    einheitliche "neueste DLL"-Strategie übernehmen, nicht die abweichende
    `files[0]`-Variante aus `FilterCliIntegrationTests`/`WebBaselineTests`
    — Verhaltensänderung nur bei Mehrfach-Build-Ordnern denkbar, an keiner
    der 6 Aufrufstellen aktuell relevant, da immer nur ein
    Debug/Release-Build-Output vorhanden ist).
  - `Task<CliProcessResult> RunLinterAsync(string arguments, CancellationToken
    cancellationToken = default)` — Komfort-Methode für die 6 „dotnet dll"-
    Fälle: löst Solution-Root/DLL intern auf, baut `ProcessStartInfo`
    (`dotnet` + DLL-Pfad + `arguments`), holt intern ein
    `SubprocessConcurrencyGate`-Lease, startet den Prozess, liest
    stdout/stderr, `await process.WaitForExitAsync(cancellationToken)`,
    gibt Lease frei.
  - `Task<CliProcessResult> RunAsync(ProcessStartInfo startInfo, TimeSpan?
    timeout = null, CancellationToken cancellationToken = default)` —
    generische, Gate-abgesicherte Variante für Aufrufer mit eigenem
    `ProcessStartInfo` (z. B. `McpServerCommandAmbiguityE2ETests`, direkter
    Exe-Start statt `dotnet dll`). Bei gesetztem `timeout`: wartet mit
    `WaitForExitAsync` gegen einen verknüpften Cancellation-Token,
    `CliProcessResult.TimedOut = true` statt Exception, falls die Zeit
    überschritten wird (Aufrufer entscheidet selbst per Assert, wie er
    reagiert — kein verändertes Fehlerverhalten gegenüber dem bisherigen
    `Assert.True(process.WaitForExit(TimeSpan...))`-Muster).
  - `CliProcessResult RunSync(ProcessStartInfo startInfo)` — rein
    synchrone Variante **ohne** Gate-Nutzung (kein `AiNetLinter.exe`-Start,
    kein Ressourcen-Konkurrenz-Bedarf, siehe „Aktueller Projektzustand"),
    für Aufrufer aus nicht-`async`-fähigem Kontext (Konstruktoren). Nutzt
    intern dasselbe Output-Capture-Muster wie `GitImpactMiniFixtureWorkspace.
    RunGit` (`BeginOutputReadLine`-Events + `WaitForExit()`), nicht das
    `ReadToEnd()`-Muster der `dotnet dll`-Fälle (Deadlock-Gefahr bei langen
    stdout/stderr-Strömen ohne asynchrones Lesen — bereits im
    Original-`RunGit` korrekt vermieden, hier beibehalten statt regrediert).
  - `readonly record struct CliProcessResult(int ExitCode, string Output,
    string Error, bool TimedOut = false)` — ersetzt die bisherigen
    `(int ExitCode, string Output, string Error)`-Tupel an allen
    Aufrufstellen.
- **Warum:** Konzept F.2 — ein gemeinsamer Helper statt 6x fast identischem
  Boilerplate; Grundlage für künftiges Prozess-Start-pro-Testklasse (siehe
  „Notes" für die bewusste Nicht-Umsetzung in diesem Step).

### Datei 2: `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs` (Zeile 9-13, 21-26)

- **Was:** Klassen-Doc und `AcquireAsync`-Doc korrigieren — der
  behauptete Vertrag „Aufrufer haelt das Handle fuer die gesamte Laufzeit
  des Subprozesses" gilt seit step-001 nachweislich nicht mehr für alle
  Aufrufer (`McpTestClient.ConnectAsync` gibt das Lease bereits nach dem
  Handshake frei). Neue Formulierung: Bremse begrenzt die Zahl
  gleichzeitiger Slot-Inhaber; **wie lange** ein Aufrufer das Lease hält
  (nur Start/Handshake vs. gesamte Prozesslaufzeit), entscheidet der
  jeweilige Aufrufer — kein universeller Vertrag mehr postulieren.
- **Warum:** step-001-Review, „Sonstige Beobachtungen" (MINOR, explizit
  zur Korrektur bei der nächsten Berührung dieser Datei empfohlen — dieser
  Step berührt sie durch den neuen `CliProcessRunner`-Aufrufer ohnehin).

### Datei 3: `src/AiNetLinter.Tests/Baseline/BaselineCliTests.cs`

- **Was:** Private `RunLinterAsync`/`FindSolutionRoot`/`FindLinterDll`
  entfernen, alle 4 Aufrufstellen auf
  `CliProcessRunner.RunLinterAsync(arguments)` umstellen. Tupel-Rückgabe
  `(ExitCode, Output, Error)` durch `CliProcessResult`-Properties ersetzen
  (`result.ExitCode`/`.Output`/`.Error` statt Tupel-Dekonstruktion, oder
  Dekonstruktion beibehalten falls `CliProcessResult` `Deconstruct`
  anbietet — Konsistenz mit den anderen Aufrufstellen wahren).
- **Warum:** Kern-DRY-Ziel dieses Steps.

### Datei 4: `src/AiNetLinter.Tests/Baseline/WebBaselineTests.cs`

- **Was:** Die 2 Testmethoden (`CreateBaseline_WithWebEnabled_IncludesWebFiles`,
  `AuditWithBaseline_ChangedWebFile_ReportsViolationsAndUpdatesBaseline`)
  von `void` auf `async Task` umstellen, `RunLinter(...)` durch `await
  CliProcessRunner.RunLinterAsync(...)` ersetzen. Private
  `RunLinter`/`FindSolutionRoot`/`FindLinterDll` entfernen. Assertions
  unverändert.
- **Warum:** Schließt die in „Aktueller Projektzustand" identifizierte
  Gate-Lücke (bislang keine `SubprocessConcurrencyGate`-Absicherung),
  ohne einen `BanBlockingTaskAccess`-Verstoß einzuführen.

### Datei 5: `src/AiNetLinter.Tests/Cli/CliIntegrationTests.cs`

- **Was:** Analog Datei 3 — alle `RunLinterAsync`-artigen Aufrufe (7
  Testmethoden inkl. der lokalen `MakeProcess`-Hilfsfunktion in
  `GeneratePlaybook_WithCheckFlag_ReturnsOkWhenUpToDate`) auf
  `CliProcessRunner.RunLinterAsync` umstellen, private
  `FindSolutionRoot`/`FindLinterDll` entfernen.
- **Warum:** Kern-DRY-Ziel dieses Steps.

### Datei 6: `src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs`

- **Was:** Konstruktor cacht aktuell `_rootDir`/`_linterDllPath` per
  eigener `FindSolutionRoot`/`FindLinterDll` — auf
  `CliProcessRunner.FindSolutionRoot()`/`FindLinterDll(...)` umstellen
  (Felder bleiben als Instanz-Cache erhalten, nur die Implementierung
  wandert). Private `RunAsync`-Hilfsmethode nutzt `CliProcessRunner.
  RunAsync` mit selbst gebautem `ProcessStartInfo` oder direkt
  `CliProcessRunner.RunLinterAsync` (Argumente enthalten hier keinen
  DLL-Pfad, nur CLI-Flags — Komfort-Methode passt). Private
  `FindSolutionRoot`/`FindLinterDll` entfernen.
- **Warum:** Kern-DRY-Ziel dieses Steps.

### Datei 7: `src/AiNetLinter.Tests/Commands/CliBatchRegressionTests.cs`

- **Was:** Analog Datei 3 (einzige Testmethode). Beachten: nutzt
  bislang einen fest kodierten `bin/Debug/net10.0`-Pfad statt
  `FindLinterDll`-Discovery — bei der Umstellung auf `CliProcessRunner.
  RunLinterAsync` prüfen, ob der Wechsel auf DLL-Discovery
  (`OrderByDescending(...LastWriteTimeUtc)`) das Testverhalten ändert
  (z. B. falls sowohl Debug- als auch Release-Build-Output vorhanden
  sind); falls ja, im `step-result.md` explizit vermerken statt
  stillschweigend zu übernehmen.
- **Warum:** Kern-DRY-Ziel dieses Steps, zusätzlich Vereinheitlichung auf
  ein einziges DLL-Auflösungsverfahren.

### Datei 8: `src/AiNetLinter.Tests/Commands/McpServerCommandAmbiguityE2ETests.cs`

- **Was:** `Process.Start(processInfo)` + manuelles
  `SubprocessConcurrencyGate.AcquireAsync()` + `WaitForExit(TimeSpan.
  FromSeconds(10))`-Assert durch `CliProcessRunner.RunAsync(processInfo,
  TimeSpan.FromSeconds(10))` ersetzen; Assert auf `!result.TimedOut`
  statt auf den bisherigen `WaitForExit(TimeSpan)`-Rückgabewert, danach
  wie bisher gegen `result.Error`/`result.ExitCode` prüfen.
- **Warum:** Vereinheitlicht das Timeout-Muster im gemeinsamen Helper,
  statt es als Einzelfall in der Testklasse zu belassen.

### Datei 9: `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs`

- **Was:** Private `RunGit`-Methode auf `CliProcessRunner.RunSync(startInfo)`
  umstellen (Output-Capture-Logik wandert in den Helper, `RunGit` baut nur
  noch `ProcessStartInfo` und wertet `CliProcessResult` aus — Exit-Code-
  Check + Exception bei Fehlschlag bleiben in `RunGit`, da git-spezifisch).
  **Kein** Gate-Aufruf (siehe „Aktueller Projektzustand").
- **Warum:** Letzte der 8 Konzept-Dateien; reduziert die
  Process-Start-/Output-Capture-Duplikation auch hier, ohne den
  Konstruktor-Kontext zu brechen.

### Datei 10: `src/AiNetLinter.Tests/Suppression/DisableAllCliTests.cs`

- **Was:** Die 2 `RunLinter`-nutzenden Testmethoden
  (`AddDisableAll_OnViolatingFixture_InjectOnlyIntoViolatingFiles`,
  `RemoveDisableAll_OnFixture_RemovesExactDisableAllLine`) von `void` auf
  `async Task` umstellen, `RunLinter(...)` durch `await CliProcessRunner.
  RunLinterAsync(...)` ersetzen. Die beiden bereits vorhandenen
  `async Task`-Testmethoden (`Main_AddDisableAllWithBaseline_...`,
  `Main_AddAndRemoveDisableAll_...`) rufen `AiNetLinter.Program.Main`
  direkt auf, nicht `RunLinter` — unverändert lassen. Private
  `RunLinter`/`FindSolutionRoot`/`FindLinterDll` entfernen.
- **Warum:** Schließt dieselbe Gate-Lücke wie Datei 4.

## Tests

- [ ] `dotnet build AiNetLinter.slnx` — 0 Warnungen/Fehler.
- [ ] `dotnet test --filter Category=Unit` — grün, schnelle
      Zwischenverifikation.
- [ ] `dotnet test --filter Category=Integration` — grün, deckt alle 8
      geänderten Dateien direkt ab (alle sind `[Trait("Category",
      "Integration")]` bzw. rufen den CLI-Subprozess auf).
- [ ] `dotnet test AiNetLinter.slnx --no-build` — vollständiger Volllauf,
      **mindestens zweimal hintereinander** (gleiche Flake-Vorsicht wie in
      step-001, da erneut Subprozess-/Gate-Verhalten berührt wird). Bei
      Rot: `TestResults/latest.trx` auslesen.
- [ ] Keine Assertion-Texte/-Werte in den 8 Dateien geändert — nur Aufruf-
      Mechanik (Non-Goal-Prüfung aus `Konzept.md`).

## Definition of Done

- [ ] `CliProcessRunner.cs` existiert mit den in „Konkrete Änderungen"
      beschriebenen Mitgliedern
- [ ] Alle 8 im Konzept gelisteten Dateien nutzen `CliProcessRunner`
      (direkt oder über `RunSync`/`RunAsync`), keine eigene
      `FindSolutionRoot`/`FindLinterDll`-Kopie mehr
- [ ] `WebBaselineTests`/`DisableAllCliTests` haben jetzt eine
      `SubprocessConcurrencyGate`-Absicherung (über `CliProcessRunner`)
- [ ] `SubprocessConcurrencyGate.cs`-Doku korrigiert (kein universeller
      „hält bis Prozessende"-Vertrag mehr behauptet)
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün, Volllauf zweimal
      hintereinander grün (Flake-Check)
- [ ] Vor jedem Build/Test: offene `AiNetLinter.exe`/`testhost.exe`-
      Prozesse geprüft und bei Bedarf beendet
- [ ] Commit auf aktuellem Branch (Conventional Commit, Suffix
      `[codegraph-mcp-finish]`)
- [ ] `step-002/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4` (Updates & Tests —
  „Testsuite-Parallelität bewahren") — Gate-Nutzung/gezielte
  Subprozess-Bremse statt Zwangsserialisierung bleibt Grundlage.
- `.agents/rules/AiNetLinter.mdc` — Kurz-Stil-Grenzwerte für die neue
  `CliProcessRunner.cs` (≤500 Zeilen/Datei, ≤60 Zeilen/Methode bzw. ≤100
  im `*.Tests`-Override, ≤4 Methodenparameter, `#nullable enable`);
  `BanAsyncVoid`/`BanBlockingTaskAccess` (agent-resilience) — Grund für
  den `async Task`-Umbau der bisher synchronen `WebBaselineTests`-/
  `DisableAllCliTests`-Testmethoden statt eines blockierenden
  Sync-Wrappers.

## Bekannte Ausnahmen

- Keine.

## Notes

- **Nicht Scope dieses Steps:** F.3 (`Core/`-Sub-Gliederung), F.4
  (Test-Data-Builder), F.5 (`#nullable enable`-Retrofit), F.6 (formale
  Laufzeitmessung) — bleiben offene Teile von `EPIC-01`.
- **Bewusst nicht umgesetzt:** Umstellung von
  Prozess-Start-pro-Testmethode auf Prozess-Start-pro-Testklasse
  (`IClassFixture`-basiertes Pooling, wie es Konzept F.2/TD-002 als
  mögliche Folgestufe erwähnt — „Grundlage, um **wo fachlich vertretbar**
  umzustellen"). Dieser Step legt mit `CliProcessRunner` die Grundlage,
  geht aber nicht so weit, tatsächlich einen geteilten Prozess-Pool
  einzuführen — das wäre eine Verhaltensänderung (Prozess-Lebensdauer,
  potenzieller State-Leak zwischen Testmethoden) und keine reine
  Boilerplate-Konsolidierung mehr, was gegen das Non-Goal „Keine Änderung
  an Testinhalten/Assertions" liefe, wenn dabei etwas schiefgeht. Falls
  der Kritiker das anders bewertet: als Tech-Debt-Kandidat vermerken, kein
  eigenmächtiger Ausbau in diesem Step.
- **`GitImpactMiniFixtureWorkspace` bewusst ohne Gate:** siehe „Aktueller
  Projektzustand" — kein `AiNetLinter.exe`-Start, keine
  Ressourcen-Konkurrenz-Sorge, die die Gate-Einführung rechtfertigt.
- **Bestehendes Muster für neue `Fixtures/`-Dateien:**
  `SubprocessConcurrencyGate.cs` (step-001) zeigt bereits Namensschema und
  Ablageort (`src/AiNetLinter.Tests/Fixtures/`) für projektweit geteilte
  Test-Infrastruktur — `CliProcessRunner.cs` folgt demselben Muster.
