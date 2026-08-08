---
status: done (approved)
type: step-plan
task: flaky-and-test-performance
step: 019               # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
corrects: null             # <null | step-NNN> — nur gesetzt, wenn dieser Step eine Korrektur ist (treibt das Kettenbudget, siehe ../spec.md §10.5/§10.6)
title: "Flaky-Test strukturell fixen — LoadState-Übergang event-/await-basiert statt Poll-Loop"
epic: EPIC-06          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet (bei corrects: vom korrigierten Step übernommen)
estimated_risk: low  # additiv, internal-only Produktionscode-Erweiterung + Umbau von 2 Testmethoden in derselben Datei; kein sichtbares CLI/MCP-Verhalten betroffen
step_type: single  # single (Default) | batch — siehe ../spec.md §10.6. Bei batch: items-Liste unten füllen.
items: []
created_by: planer  # planer | orchestrator (nur bei mechanischem Korrektur-Transkript ohne Ermessen, siehe ../spec.md §6.2.1)
created_by_model: Claude Sonnet 5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08T21:30:00+02:00
related_to: []  # Pointer auf andere step-NNN (Task-interne Abhängigkeiten) oder auf step-review.md (Fix-Modus) — nie Fakten cachen, nur verweisen. Siehe ../spec.md §10.6. Nicht zu verwechseln mit `corrects` oben (eigene, budget-relevante Semantik).
---

# Step 019: Flaky-Test strukturell fixen — LoadState-Übergang event-/await-basiert statt Poll-Loop

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-06` aus `roadmap.md` — bislang komplett offen (letzter
  unbearbeiteter Muss-Haben-Punkt aus dem Konzept neben EPIC-08). EPIC-05
  wurde in diesem Planer-Aufruf als obsolet markiert (siehe Roadmap-Diff
  unten) — EPIC-06 ist damit das nächste sinnvolle Epic.
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 6, §"Muss-Haben"
  Flaky-Punkt, §"Definition of Done" Flaky-Kriterium, §"Wo im Projekt"
  Abschnitt "Der Flaky Test" (Zeile ~81-82).

## Aktueller Projektzustand (JIT-Kontext)

Vollständig gelesen: `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs`
(174 Zeilen, 3 `[Fact]`-Methoden) und `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`
(Konstruktor, `LoadState`-Property, `GetCurrentSolution()`, `Dispose()`).

**Root-Cause-Mechanik (verifiziert am Code, nicht nur vermutet):**

- `McpCodeGraphServer`-Konstruktor (Z. 48-53): bei gesetztem `LoadFunc`
  wird `_loadTask = Task.Run(() => loadFunc(CancellationToken.None))`
  gestartet — `Task.Run(Func<Task<T>>)` "unwrapped" die innere Task, d. h.
  der äußere `_loadTask` wird erst **fertig, sobald der Task-Scheduler
  eine an die innere Task angehängte Continuation auf dem Thread-Pool
  ausführt** — das ist ein echter, asynchroner Hop, kein synchroner
  Seiteneffekt von `TaskCompletionSource.SetResult(...)`.
- `LoadState` (Z. 70-78) liest `_loadTask.IsCompletedSuccessfully` direkt
  — korrekt und race-frei für den Produktionscode-Pfad (jeder Tool-Aufruf
  fragt einmal ab, kein Polling nötig, da der MCP-Client selbst erneut
  anfragt).
- Der **Test** (`LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`,
  Z. 113-151) ruft `release.SetResult(_fixture.Catalog)` auf und pollt
  danach mit `Thread.Sleep(25)` in einer `while`-Schleife bis zu einer
  fixen 5s-`DateTime.UtcNow`-Deadline auf `server.LoadState ==
  ServerLoadState.Loading`. Unter Last (parallele Subprozess-/Workspace-Tests,
  Thread-Pool-Kontention) kann die oben beschriebene Continuation-
  Ausführung länger als 5s dauern → Poll-Schleife verlässt sich auf ein
  Sampling-Intervall statt auf ein echtes Fertig-Signal → Test schlägt
  fehl, obwohl der Load tatsächlich (nur etwas später) abschließt. Genau
  dieses Muster ist in `konzept.md` als reproduzierte Flakiness
  dokumentiert (2/10 bzw. 6/10 Wiederholungen unter Last).
- **Zusätzlich beim Lesen entdeckt (nicht in `konzept.md`/`roadmap.md`
  vorher als flaky benannt, aber identischer Root Cause):**
  `RunAsync_LoadFuncCompletes_ServerLeavesLoadingState` (Z. 65-111) hat
  exakt dasselbe Poll-Loop-Muster (Z. 101-105, dort bereits `async Task`
  mit `await Task.Delay(25)` statt `Thread.Sleep`, aber dieselbe
  fixe-5s-Deadline-Sampling-Logik). Da EPIC-06 laut `konzept.md` als
  strukturelles Ziel "Poll-Loop mit fixer Deadline eliminieren" nennt
  (nicht nur "diesen einen Test grün bekommen"), wird diese zweite
  Fundstelle im selben Step mitbehoben — siehe „Notes" unten für die
  Begründung, warum das kein Batch ist.
- **Kein bestehender Testbarkeits-Hook:** `_loadTask` ist `private
  readonly` (Z. 30). Es gibt aktuell keinen Weg für Testcode,
  deterministisch auf den Abschluss zu warten, außer zu pollen.
  `InternalsVisibleTo("AiNetLinter.Tests")` ist bereits projektweit
  gesetzt (`src/AiNetLinter/Core/LinterEngine.cs:18`) — ein neues
  `internal`-Member auf `McpCodeGraphServer` ist ohne zusätzliches Setup
  aus dem Testprojekt erreichbar. `McpCodeGraphServer` ist bereits
  `internal sealed class` (kein `public`-API-Zuwachs, kein Risiko für
  sichtbares CLI/MCP-Verhalten — erfüllt den Konzept-Non-Goal
  ausdrücklich).
- **Kein widersprüchlicher CodeMap-Eintrag:** `McpCodeGraphServer.cs` war
  bislang nur als EPIC-05-Kandidat vermerkt (jetzt obsolet); kein
  Anti-Loop-Konflikt, da EPIC-06 dort keine frühere bewusste Entscheidung
  zurückdreht. CodeMap-Eintrag bereits in diesem Planer-Aufruf ergänzt
  (siehe `codemap.md`).

## Intention

Nach diesem Step wartet der Test deterministisch auf den tatsächlichen
Abschluss des Hintergrund-Load-`Task` (echtes Fertig-Signal über `await`),
statt in festen Intervallen zu raten, ob der `LoadState`-Übergang
vermutlich schon passiert ist. Die Thread-Pool-Timing-Abhängigkeit
entfällt strukturell — nicht durch eine großzügigere Deadline (das wäre
reines Symptom-Fixing, siehe `AiNetLinterRichtlinien.mdc` §5, "Symptom-
Fixing verboten"). Ein einziger neuer `internal`-Zugriffspunkt auf
`McpCodeGraphServer` ist der minimal-invasive Weg, der kein `public`-API
verändert.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (nach der `LoadState`-Property, Z. 78)

- **Was:** Neue `internal`-only Property (kein `public`), die den
  privaten `_loadTask` nach außen (nur für `AiNetLinter.Tests` über
  `InternalsVisibleTo`) sichtbar macht:

  ```csharp
  /// <summary>Der laufende Hintergrund-Load (<see langword="null"/>, wenn der Server
  /// synchron mit fertigem Catalog konstruiert wurde). Test-only Zugriffspunkt, um
  /// deterministisch auf den Abschluss des Load-Übergangs zu warten (<c>await</c>
  /// statt Poll-Loop auf <see cref="LoadState"/>) — kein öffentliches API.</summary>
  internal Task<SourceFileCatalog?>? LoadTask => _loadTask;
  ```

- **Warum:** Einziger Weg, dem Testcode ein echtes Fertig-Signal statt
  eines Sampling-Intervalls zu geben, ohne die `private`-Kapselung von
  `_loadTask` öffentlich zu brechen (bleibt `internal`, `McpCodeGraphServer`
  bleibt `internal sealed class`).

### Datei 2: `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs`

- **Was (Methode `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`,
  Z. 113-151):** Signatur von `public void` auf `public async Task`
  ändern. Den Block

  ```csharp
  var deadline = DateTime.UtcNow.AddSeconds(5);
  while (server.LoadState == ServerLoadState.Loading && DateTime.UtcNow < deadline)
  {
      Thread.Sleep(25);
  }
  ```

  ersetzen durch ein einmaliges `await` auf `server.LoadTask`, mit einem
  großzügigen Sicherheitsnetz (kein Sampling, sondern ein einziger
  `Task.WhenAny`-Wettlauf gegen einen Timeout, der nur einen echten Hänger
  abfängt, nicht den Normalfall bestimmt):

  ```csharp
  var safetyTimeout = Task.Delay(TimeSpan.FromSeconds(20));
  var winner = await Task.WhenAny(server.LoadTask!, safetyTimeout);
  Assert.Same(server.LoadTask, winner);
  ```

  Anschließende `Assert.Equal(ServerLoadState.Loaded, server.LoadState)`
  bleibt unverändert bestehen.
- **Warum:** Ersetzt Sampling durch ein echtes Completion-Signal; der
  20s-Timeout ist ein reines Sicherheitsnetz gegen einen tatsächlichen
  Hänger (z. B. Regressions-Bug), nicht die Fehlerquelle selbst — anders
  als die alte 5s-Deadline bestimmt er im Normalfall nie das Ergebnis.

- **Was (Methode `RunAsync_LoadFuncCompletes_ServerLeavesLoadingState`,
  Z. 65-111):** Identisches Muster — den Block

  ```csharp
  var deadline = DateTime.UtcNow.AddSeconds(5);
  while (server.LoadState == ServerLoadState.Loading && DateTime.UtcNow < deadline)
  {
      await Task.Delay(25);
  }
  ```

  durch dieselbe `Task.WhenAny`-Konstruktion wie oben ersetzen (Methode
  ist bereits `async Task`, keine Signaturänderung nötig).
- **Warum:** Gleicher Root Cause (siehe „Aktueller Projektzustand"), im
  selben Step behoben, damit EPIC-06 nicht nur den einen gemeldeten Test,
  sondern das strukturelle Muster in der Datei beseitigt — sonst bliebe
  eine zweite, bislang nicht beobachtete, aber ebenso anfällige Stelle
  liegen.

- **Was (Kommentar-Pflege, Z. 118-122):** Der bestehende XML-/Inline-
  Kommentar über der dritten Testmethode erklärt aktuell, *warum* ein
  TCS-Pattern nötig ist (Task.Run-Scheduling-Fenster) — bleibt inhaltlich
  korrekt und muss nicht entfernt werden. Ergänzend (falls beim Umbau
  hilfreich) ein kurzer *Why*-Satz, dass jetzt auf `LoadTask` gewartet
  wird statt auf `LoadState` gepollt — ohne Verweis auf `step-019`/`EPIC-06`
  (siehe `AiNetLinterRichtlinien.mdc` §5, verboten).

## Tests

- [ ] `dotnet test --filter "FullyQualifiedName~McpServerCommandLoadingStateTests"`
  grün, alle 3 Facts.
- [ ] **Kern-Verifikation (Konzept-DoD, nicht optional):** mindestens 10
  aufeinanderfolgende **volle** `dotnet test`-Läufe (nicht isoliert,
  nicht gefiltert) grün — insbesondere ohne Fehlschlag von
  `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  oder `RunAsync_LoadFuncCompletes_ServerLeavesLoadingState`. Vorher/Nachher
  dokumentieren: falls vor dem Fix reproduzierbar (z. B. durch mehrfaches
  Ausführen vor der Änderung, analog zur `konzept.md`-Reproduktion 2/10
  bzw. 6/10), im `step-result.md` explizit vermerken. 10 volle Läufe
  können je nach aktueller Laufzeit mehrere Minuten dauern — sequenziell
  ausführen (z. B. PowerShell-Schleife `1..10 | ForEach-Object { dotnet
  test }`), Ergebnis je Lauf festhalten (Pass/Fail), nicht nur den letzten
  Lauf.
- [ ] `dotnet build` (TreatWarningsAsErrors) grün — insbesondere prüfen,
  dass keine neue Warnung durch die geänderte Methoden-Signatur (`void`
  → `async Task`) entsteht (xUnit-v3-Runner erkennt `async Task`-Facts
  automatisch, keine Attribut-Änderung nötig).
- [ ] Self-Lint (`dotnet run --project src/AiNetLinter -- --config
  rules.json --path .`) bleibt `OK` — insbesondere `BanBlockingTaskAccess`
  (neuer Code nutzt ausschließlich `await`, kein `.Wait()`/`.Result`/
  `.GetAwaiter().GetResult()`) und `MaxMethodLineCount` (Testprojekt-
  Override: 100 Zeilen, beide Methoden bleiben weit darunter).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün — inkl. 10 aufeinanderfolgender
  voller Läufe ohne Fehlschlag der beiden betroffenen Testmethoden
- [ ] Commit auf aktuellem Branch (Conventional Commit, Subject ≤ 72
  Zeichen inkl. `[flaky-and-test-performance]`-Suffix — z. B. `fix(tests):
  LoadState-Poll-Loop durch await ersetzen [flaky-and-test-performance]`,
  72 Zeichen exakt — bei Bedarf kürzen, TD-002 beachten)
- [ ] `step-019/step-result.md` geschrieben — inkl. Vorher/Nachher-Notiz
  zur Flaky-Reproduktion
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending
  audit)` gesetzt
- [ ] CodeMap (`codemap.md`) nach Umsetzung final aktualisiert (Coder-
  Pflicht vor Doku-Commit — der Planer hat den Eintrag bereits vorbereitet,
  der Coder ergänzt den tatsächlichen Umsetzungsstand)

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#agent-resilience` — `BanBlockingTaskAccess`
  (`.Wait()`/`.Result`/`.GetAwaiter().GetResult()` verboten, `await`
  vorgeschrieben) ist die Kernregel für diesen Step: die neue Lösung nutzt
  ausschließlich `await`/`Task.WhenAny`, keine blockierenden Task-Zugriffe
  — im Gegensatz zu den bereits bestehenden, explizit per
  `ainetlinter-disable BanBlockingTaskAccess` begründeten Ausnahmen in
  `McpCodeGraphServer.cs` (`LoadState`-Property, `GetCurrentSolution()`),
  die synchron bleiben müssen (Tool-Dispatch-Pfad) und hier unverändert
  bleiben.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention` —
  "Symptom-Fixing verboten" (keine großzügigere Deadline statt echtem
  Fix) und "Sparsamer Einsatz von Code-Kommentaren" (kein Verweis auf
  `step-019`/`EPIC-06` im Code-Kommentar) sind für diesen Step direkt
  einschlägig.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4-updates--tests` —
  "xUnit v3 Tests: Pflicht für jede Logik-Änderung" (hier: Test-Änderung
  selbst ist die Logik-Änderung, keine zusätzliche Test-Pflicht) und der
  Hinweis auf `TestResults/latest.trx` für Fehlerdiagnose bei den 10
  Wiederholungsläufen.

## Bekannte Ausnahmen

- Keine — dieser Step behebt die einzige bekannte Ausnahme (den Flaky-Test
  selbst). Nach `approved` sollte es projektweit keinen bekannten Flaky-
  Test mehr geben.

## Code-Skizze (optional)

```csharp
// McpCodeGraphServer.cs — neue Property nach LoadState (Z. 78)
internal Task<SourceFileCatalog?>? LoadTask => _loadTask;

// McpServerCommandLoadingStateTests.cs — Muster für beide Methoden
release.SetResult(...);

var safetyTimeout = Task.Delay(TimeSpan.FromSeconds(20));
var winner = await Task.WhenAny(server.LoadTask!, safetyTimeout);
Assert.Same(server.LoadTask, winner);

Assert.Equal(ServerLoadState.Loaded /* bzw. LoadFailed */, server.LoadState);
```

## Notes

- **Warum kein `step_type: batch`:** Die beiden betroffenen Testmethoden
  liegen in derselben Datei, teilen exakt denselben Root Cause und dieselbe
  Lösung (dieselbe neue Property, dasselbe `Task.WhenAny`-Muster) — das ist
  ein einziger zusammenhängender struktureller Fix, keine Sammlung
  unabhängiger Mini-Befunde wie bei den EPIC-02-Batches (dort: N
  unabhängige Klassen, gleiche Trait-Zeile, aber inhaltlich beliebig
  austauschbare Reihenfolge). Ein Batch-Split hier wäre künstliche
  Fragmentierung eines einzigen Fixes, kein "größeres Coding-Paket" im
  Sinne des Nutzer-Hinweises — der Nutzer-Wunsch nach größeren Paketen
  bezieht sich laut `config.md`-Begründung ausdrücklich auf mechanisch
  uniforme, unabhängige Einzeländerungen, nicht auf das künstliche
  Aufsplitten eines einzelnen zusammenhängenden Bugfixes.
- **Warum `Task.WhenAny` statt direktem `await server.LoadTask!`:** Ein
  ungebremstes `await` ohne Timeout würde bei einem echten Hänger (z. B.
  Regressions-Bug in `McpCodeGraphServer`) den Testlauf unbegrenzt
  blockieren statt kontrolliert fehlzuschlagen. Der 20s-Sicherheitsnetz-
  Timeout ist bewusst weit über jeder unter Last beobachteten Verzögerung
  gewählt (die alte 5s-Deadline war das eigentliche Problem, nicht "5s
  war zu kurz" — ein größeres Sampling-Intervall hätte die Flakiness nur
  seltener gemacht, nicht beseitigt).
- **Warum `SourceFileCatalog?` ohne Namespace-Präfix kompiliert:** `using
  AiNetLinter.Baseline;` ist bereits Z. 8 in `McpCodeGraphServer.cs`
  vorhanden.
- **Kein Einfluss auf Test 1** (`RunAsync_LoadFuncStillRunning_ToolReturnsLoadingInfo`,
  Z. 31-63) — die Methode fragt `LoadState` nur einmal synchron ab (kein
  Poll-Loop, kein Warten auf Abschluss), bleibt unverändert.
- **Doppelte 10-Lauf-Verifikation vs. EPIC-08:** EPIC-08 (Abschluss-
  Validierung) wird später einen eigenen finalen Vorher/Nachher-Vergleich
  über den ganzen Task durchführen — das ersetzt nicht die hier verlangte,
  EPIC-06-spezifische 10-Lauf-Verifikation direkt nach dem Fix (Konzept-
  DoD verlangt sie explizit für den Flaky-Test, nicht erst am Task-Ende).
