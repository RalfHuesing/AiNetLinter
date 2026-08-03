---
status: done
type: step-review
task: codegraph-mcp-finish
step: 002
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-03
verdict: approved
tech_debt_ids: [TD-002]
---

# Review Step 002: Testsuite-Performance — CliProcessRunner-Helper (F.2)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (referenzierte Dateien) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

Plan-Erfüllung: alle 10 im Plan gelisteten Dateien wie beschrieben umgesetzt, per Diff (`git show a566ea4`) gegen jede einzelne Datei-Sektion des Plans geprüft. Rules-Konformität: `AiNetLinterRichtlinien.mdc` §4 und `AiNetLinter.mdc`-Stilgrenzen sowie `BanAsyncVoid`/`BanBlockingTaskAccess` eingehalten. Logische Korrektheit: Konsolidierung vollständig, keine Duplikate mehr, Verhalten der 8 Konzept-Dateien erhalten (mit einer disclosed, vertretbaren Nuance siehe unten). Konzept-Treue: Scope entspricht exakt F.2, kein Vorgriff auf F.3-F.6, keine unautorisierte Prozess-Pool-Umstellung.

### Plan-Erfüllung

- `CliProcessRunner.cs` (neu, 163 Zeilen) existiert mit allen 5 geplanten Mitgliedern (`CliProcessResult`, `FindSolutionRoot`, `FindLinterDll`, `RunLinterAsync`, `RunAsync`, `RunSync`) — Diff vollständig gelesen, entspricht der Plan-Beschreibung 1:1, inkl. der bewusst gewählten `OrderByDescending(...LastWriteTimeUtc).First()`-Strategie (nicht die abweichende `files[0]`-Variante).
- Alle 8 Konzept-Dateien nutzen den Helper, keine private `FindSolutionRoot`/`FindLinterDll`/`RunLinter`-Kopie mehr übrig — per Grep verifiziert (`private static.*Find(SolutionRoot|LinterDll)` liefert nur noch zwei Treffer in `SourceFileCatalogTests.cs`/`SourceFileCatalogRegisterMSBuildTests.cs`, die **nicht** zu den 8 Konzept-Dateien gehören und auch kein `ProcessStartInfo` verwenden — außerhalb des Scopes, korrekt unangetastet). Ein zweiter Grep nach `Process.Start|ProcessStartInfo` liefert exakt die 4 erwarteten Treffer (`CliProcessRunner.cs` selbst, `FilterCliIntegrationTests.cs`, `GitImpactMiniFixtureWorkspace.cs`, `McpServerCommandAmbiguityE2ETests.cs`) — die anderen 5 „dotnet dll"-Dateien haben nach der Umstellung korrekt keinen eigenen `ProcessStartInfo`-Aufbau mehr.
- `WebBaselineTests`/`DisableAllCliTests`: beide betroffenen Testmethoden-Paare `void` → `async Task` umgestellt, `CliProcessRunner.RunLinterAsync` (mit Gate) genutzt — Diff geprüft, schließt die in step-001-Review dokumentierte Lücke.
- `SubprocessConcurrencyGate.cs`-Doku korrigiert: kein universeller „hält bis Prozessende"-Vertrag mehr, verweist stattdessen konkret auf die zwei Nutzungsmuster (`McpTestClient.ConnectAsync` vs. `CliProcessRunner`) — genau das vom step-001-Review geforderte MINOR-Finding behoben.
- `CliBatchRegressionTests`: auf `CliProcessRunner.RunLinterAsync`/`FindSolutionRoot` umgestellt statt fest kodiertem `bin/Debug/net10.0`-Pfad — Diff bestätigt, DLL-Discovery-Vereinheitlichung wie gefordert im `step-result.md` explizit vermerkt (siehe Konzept-Treue unten für die Risikobewertung).
- Alle übrigen Dateien (`BaselineCliTests`, `CliIntegrationTests`, `FilterCliIntegrationTests`, `McpServerCommandAmbiguityE2ETests`, `GitImpactMiniFixtureWorkspace`) einzeln gegen den jeweiligen Plan-Abschnitt geprüft — Umstellung jeweils vollständig und plan-konform, inkl. der `RedirectStandardInput`-Bedingung in `RunSync`, die korrekt zum bereits vorher in `GitImpactMiniFixtureWorkspace` gesetzten `RedirectStandardInput = true` passt (kein Verhaltensbruch beim `StandardInput.Close()`).

### Rules-Konformität

- `AiNetLinterRichtlinien.mdc` §4: eingehalten. Keine neue `ConsoleTestCollection`-Mitgliedschaft eingeführt, Gate-Nutzung bleibt gezielt statt Zwangsserialisierung.
- `AiNetLinter.mdc`: `CliProcessRunner.cs` hat `#nullable enable` (Zeile 1), 163 Zeilen (≤500), längste Methode (`RunAsync`) ca. 32 Zeilen (≤60/100), max. 3 Parameter (≤4), Klasse `static`. Namespace `AiNetLinter.Tests.Fixtures` entspricht dem Verzeichnispfad (`EnforceNamespaceDirectoryMapping`).
- `BanAsyncVoid`/`BanBlockingTaskAccess` (agent-resilience): kein `async void`, keine der umgestellten Methoden ist `void` geblieben; kein `.Wait()`/`.Result`/`.GetAwaiter().GetResult()` im gesamten Diff gefunden (auch nicht in `RunSync`, das korrekt vollständig synchron ohne jeden `Task`-Bezug bleibt — kein blockierender Zugriff auf einen laufenden `Task`, daher regelkonform gemäß der im Plan selbst dargelegten Begründung).

### Logische Korrektheit

Konsolidierung funktional korrekt: `RunLinterAsync` delegiert an `RunAsync` mit `timeout: null`, `RunAsync` hält das Gate-Lease über die komplette Prozesslaufzeit (`using var lease`, Scope bis Methodenende) — entspricht dem in step-001 etablierten Muster für die 4 direkten `Process.Start`-Aufrufer. `RunSync` übernimmt korrekt das deadlock-sichere `BeginOutputReadLine`-Muster aus dem Original-`RunGit` statt des potenziell deadlock-anfälligen `ReadToEnd()`-Musters der anderen Fälle. Eine kleine, transparent offengelegte Nuance: `CliBatchRegressionTests` und drei weitere Stellen verlieren eine redundante `Assert.True(File.Exists(linterDllPath), ...)`-Vorab-Diagnose — bewertet als vertretbar (siehe „Sonstige Beobachtungen").

### Konzept-Treue (Ebene 4)

Scope entspricht exakt F.2 (`Notes` grenzt F.3-F.6 sauber ab, Diff bestätigt: keine `Core/`-Umgliederung, kein globales `#nullable enable`-Retrofit, kein Prozess-Pool). Die bewusste Nicht-Umsetzung von Prozess-Start-pro-Testklasse (im Plan als „Notes" explizit dem Kritiker zur Bewertung vorgelegt) ist sachlich richtig: eine solche Umstellung wäre eine Verhaltensänderung, kein reines Boilerplate-Refactoring, und würde gegen das Konzept-Non-Goal „Keine Änderung an Testinhalten/Assertions" verstoßen — keine Korrektur nötig, kein Tech-Debt-Kandidat (das ist eine bewusste, im Plan bereits begründete Scope-Grenze, keine übersehene Duplikation).

Zur `CliBatchRegressionTests`-DLL-Discovery-Umstellung (im Auftrag explizit zu bewerten): kein neues Risiko. Die `OrderByDescending(...LastWriteTimeUtc).First()`-Strategie war vor step-002 bereits die etablierte Strategie in 5 der 6 „dotnet dll"-Dateien (u. a. `BaselineCliTests`, `WebBaselineTests`) — `CliBatchRegressionTests` wird lediglich auf das bereits dominante, bestehende Muster vereinheitlicht, nicht neu eingeführt. Die inhärente Ambiguität bei gleichzeitig vorhandenem Debug- **und** Release-Build-Output (dann liefert die neueste Schreibzeit einen nicht deterministisch vorhersagbaren Build) ist eine vorbestehende Eigenschaft dieses Musters, keine durch diesen Step neu geschaffene Schwäche — daher kein Finding, auch kein neuer Tech-Debt-Eintrag nötig (das Muster selbst wurde in step-001/vor step-002 nie moniert und liegt außerhalb dessen, was dieser Step ändert).

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx           → grün (0 Warnung(en), 0 Fehler) — selbst nachvollzogen
dotnet test AiNetLinter.slnx --no-build → grün (1186 Tests, 0 Fehler, 1 m 38 s) — selbst nachvollzogen (ein Lauf, deckt sich mit den zwei vom Coder gemeldeten Läufen à 1186/1186, ~1 m 37 s)
```

## Sonstige Beobachtungen / MINOR / NITPICK

- **Wegfall einzelner `Assert.True(File.Exists(linterDllPath), ...)`-Vorab-Diagnosen** (`CliBatchRegressionTests`, `CliIntegrationTests.RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode`, zwei `SyncAgentRules*`-Tests): eine strikte Wort-für-Wort-Lesart des Konzept-Non-Goals „Keine Änderung an Testinhalten/Assertions" könnte das als Grenzfall werten. In der Sache aber unproblematisch: die entfernten Assertions prüften nicht das eigentliche Testsubjekt (CLI-Verhalten/Exit-Code/Output), sondern eine reine Testumgebungs-Vorbedingung; bei fehlender DLL wirft `CliProcessRunner.FindLinterDll` stattdessen eine `FileNotFoundException` mit vergleichbar aussagekräftiger Meldung — die Diagnosequalität bei einem Fehlschlag bleibt erhalten. Vom Coder transparent unter „Abweichungen vom Plan" offengelegt. Kein Blocker, keine Fix-Empfehlung.
- `CliProcessRunner.RunAsync`'s Timeout-Pfad killt den Prozess bei Timeout nicht aktiv — entspricht exakt der im Plan geforderten Verhaltensparität zum ursprünglichen `WaitForExit(TimeSpan)`-Muster („kein verändertes Fehlerverhalten"), ist also plankonform, nicht neu eingeführt. Vom Coder selbst unter „Bekannte Unschärfen" dokumentiert. Für künftige `RunAsync`-Aufrufer mit Timeout-Bedarf (aktuell nur `McpServerCommandAmbiguityE2ETests`) eine erwähnenswerte, aber nicht blockierende Designeigenschaft.
- Lesbarkeits-Nuance in `CliIntegrationTests.GeneratePlaybook_WithCheckFlag_ReturnsOkWhenUpToDate`: die räumliche Nähe der zwei ursprünglich verschachtelten `using`-Gate-Blöcke (deutete zwei separate Lease-Zyklen an) ist nach der Umstellung auf zwei sequenzielle `RunLinterAsync`-Aufrufe implizit statt visuell erkennbar. Rein kosmetisch, kein Verhaltensproblem.

## Tech-Debt-Einträge aus diesem Review

- `TD-002` (siehe `tech-debt.md`) — tote, vorbestehende Variable `baselineAfter` in `WebBaselineTests.cs:92`, außerhalb des Boilerplate-Scopes dieses Steps.
