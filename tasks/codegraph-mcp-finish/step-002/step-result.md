---
status: done
type: step-result
task: codegraph-mcp-finish
step: 002
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-03
code_commit_hash: a566ea4
status_after: done
blocker_category: n/a
---

# Result Step 002: Testsuite-Performance — CliProcessRunner-Helper (F.2)

## Zusammenfassung

Neue `CliProcessRunner`-Klasse konsolidiert `FindSolutionRoot`/`FindLinterDll`
sowie Prozessstart-/Gate-/Output-Capture-Boilerplate für alle 8 im Konzept
gelisteten Dateien, über drei Varianten (`RunLinterAsync`, `RunAsync`,
`RunSync`). `WebBaselineTests`/`DisableAllCliTests` haben jetzt eine
Gate-Absicherung (waren zuvor eine Lücke aus step-001). Doku-Korrektur an
`SubprocessConcurrencyGate.cs` wie vom step-001-Review gefordert.

## Geänderte Dateien

- `src/AiNetLinter.Tests/Fixtures/CliProcessRunner.cs` (neu) — konsolidierte
  `FindSolutionRoot`/`FindLinterDll`, `RunLinterAsync` (Komfort, dotnet+dll),
  `RunAsync` (generisch, optional Timeout), `RunSync` (gate-frei,
  `BeginOutputReadLine`-Muster), `readonly record struct CliProcessResult`.
- `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs` — Klassen-/
  Methoden-Doc korrigiert (kein universeller „hält bis Prozessende"-Vertrag
  mehr, verweist stattdessen auf die zwei unterschiedlichen Nutzungsmuster
  `McpTestClient.ConnectAsync` vs. `CliProcessRunner`).
- `src/AiNetLinter.Tests/Baseline/BaselineCliTests.cs` — auf
  `CliProcessRunner.RunLinterAsync`/`FindSolutionRoot` umgestellt, private
  Duplikate entfernt.
- `src/AiNetLinter.Tests/Baseline/WebBaselineTests.cs` — beide Testmethoden
  `void` → `async Task`, `RunLinter` → `CliProcessRunner.RunLinterAsync`
  (schließt Gate-Lücke), private Duplikate entfernt.
- `src/AiNetLinter.Tests/Cli/CliIntegrationTests.cs` — alle 8 Testmethoden auf
  `CliProcessRunner.RunLinterAsync`/`FindSolutionRoot`/`FindLinterDll`
  umgestellt, private Duplikate entfernt; `MakeProcess`-Lokalfunktion durch
  `BuildArguments`-Stringbuilder ersetzt (kein Process-Objekt mehr nötig).
- `src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs` — Konstruktor nutzt
  `CliProcessRunner.FindSolutionRoot`/`FindLinterDll` (Felder bleiben als
  Instanz-Cache), private `RunAsync`-Hilfsmethode baut weiterhin ein eigenes
  `ProcessStartInfo` (nutzt `_linterDllPath`), delegiert aber an
  `CliProcessRunner.RunAsync`; private Duplikate entfernt.
- `src/AiNetLinter.Tests/Commands/CliBatchRegressionTests.cs` — auf
  `CliProcessRunner.RunLinterAsync`/`FindSolutionRoot` umgestellt (statt fest
  kodiertem `bin/Debug/net10.0`-Pfad), private Duplikate entfernt.
- `src/AiNetLinter.Tests/Commands/McpServerCommandAmbiguityE2ETests.cs` —
  `Process.Start`+manuelles Gate+`WaitForExit(TimeSpan)` durch
  `CliProcessRunner.RunAsync(processInfo, TimeSpan.FromSeconds(10))` ersetzt,
  Assert auf `!result.TimedOut`.
- `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` —
  `RunGit` baut nur noch `ProcessStartInfo` und wertet
  `CliProcessRunner.RunSync`-Ergebnis aus (Exit-Code-Check + Exception bleibt
  git-spezifisch in `RunGit`), kein Gate (siehe Plan).
- `src/AiNetLinter.Tests/Suppression/DisableAllCliTests.cs` — die 2
  `RunLinter`-nutzenden Testmethoden `void` → `async Task`, auf
  `CliProcessRunner.RunLinterAsync` umgestellt (schließt Gate-Lücke); die 2
  bereits `async Task`-Methoden (`Program.Main`-Aufrufe) unverändert; private
  Duplikate entfernt.

## Commit

- **Code-Commit-Hash:** `a566ea4`
- **Message:**
  ```
  test: CliProcessRunner-Helper fuer Subprozess-Teststellen konsolidiert [codegraph-mcp-finish]

  Neue statische Klasse CliProcessRunner (src/AiNetLinter.Tests/Fixtures)
  buendelt FindSolutionRoot/FindLinterDll-Discovery sowie
  ProcessStartInfo-Aufbau/Gate-Acquire/Output-Capture-Boilerplate, das
  zuvor in 6 Testdateien fast identisch dupliziert war (F.2). Die 8 im
  Konzept gelisteten Dateien nutzen sie jetzt ueber drei Varianten:
  RunLinterAsync (Komfort-Methode, dotnet+dll), RunAsync (generisch,
  optional Timeout-faehig, fuer McpServerCommandAmbiguityE2ETests) und
  RunSync (gate-frei, BeginOutputReadLine-Muster, fuer den
  Konstruktor-Kontext von GitImpactMiniFixtureWorkspace.RunGit).
  SubprocessConcurrencyGate bleibt eigenstaendig, wird aber intern von
  CliProcessRunner aufgerufen statt von jeder Testklasse einzeln.

  WebBaselineTests und DisableAllCliTests hatten bislang keine
  Gate-Absicherung (Luecke aus step-001) - ihre betroffenen
  Testmethoden sind jetzt async Task und laufen ueber
  CliProcessRunner.RunLinterAsync.

  CliBatchRegressionTests nutzte einen fest kodierten
  bin/Debug/net10.0-Pfad statt DLL-Discovery; nach Umstellung auf
  CliProcessRunner.RunLinterAsync gilt einheitlich die
  Discovery-Strategie (aktuell kein Verhaltensunterschied, da nur ein
  Debug-Build-Ausgabeverzeichnis existiert).

  SubprocessConcurrencyGate.cs: Klassen-/Methoden-Doc korrigiert (kein
  universeller "haelt bis Prozessende"-Vertrag mehr behauptet, siehe
  step-001-Review MINOR-Finding - McpTestClient.ConnectAsync haelt das
  Lease bereits nur fuer Start+Handshake).

  Refs: tasks/codegraph-mcp-finish/step-002
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                → grün (0 Warnungen, 0 Fehler)
dotnet test --filter Category=Unit           → grün (100 Tests, 0 Fehler)
dotnet test --filter Category=Integration     → grün (100 Tests, 0 Fehler)
dotnet test AiNetLinter.slnx --no-build (1/2) → grün (1186 Tests, 0 Fehler, 1 m 37 s)
dotnet test AiNetLinter.slnx --no-build (2/2) → grün (1186 Tests, 0 Fehler, 1 m 37 s)
```

Keine offenen `AiNetLinter.exe`/`testhost.exe`-Prozesse vor den Läufen
(geprüft per `tasklist`).

## Abweichungen vom Plan

- **`CliBatchRegressionTests` verliert die explizite
  `Assert.True(File.Exists(linterDllPath), ...)`-Vorab-Diagnose.** Der
  ursprüngliche Test prüfte DLL-Pfad und Config-Pfad getrennt mit eigenen
  Fehlermeldungen, bevor der Prozess gestartet wurde. Nach der Umstellung auf
  `CliProcessRunner.RunLinterAsync` (das die DLL-Discovery intern kapselt)
  bleibt nur noch die Config-Pfad-Prüfung erhalten; fehlt die DLL, wirft
  `CliProcessRunner.FindLinterDll` stattdessen eine
  `FileNotFoundException` mit eigener, ebenfalls aussagekräftiger Meldung.
  Gleiches Muster (Wegfall der separaten `Assert.True(File.Exists(linterDllPath))`
  bei Tests, die zuvor keinen sonstigen Gebrauch von `linterDllPath` hatten)
  auch bei `CliIntegrationTests.RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode`
  und den beiden `SyncAgentRules*`-Tests — dort war die Variable im Original
  ohnehin nur zum Prozessstart genutzt, kein Verlust an Testaussagekraft.
- **Kein Verhaltensunterschied durch die DLL-Discovery-Vereinheitlichung in
  `CliBatchRegressionTests`** (Plan bat um explizite Prüfung/Vermerk): im
  Arbeitsverzeichnis existiert nur `src/AiNetLinter/bin/Debug/net10.0`, kein
  Release-Build-Output — `OrderByDescending(...LastWriteTimeUtc).First()`
  liefert dieselbe DLL wie der zuvor fest kodierte Pfad.
- Ansonsten Plan 1:1 umgesetzt.

## Beobachtungen

- **`CliIntegrationTests.GeneratePlaybook_WithCheckFlag_ReturnsOkWhenUpToDate`**
  nutzte im Original zwei verschachtelte `using`-Blöcke mit eigenem
  Gate-Lease pro Teilschritt (Generieren, dann Prüfen). Nach der Umstellung
  auf zwei sequenzielle `CliProcessRunner.RunLinterAsync`-Aufrufe holt und
  gibt jeder Aufruf sein eigenes Lease unabhängig — funktional identisch,
  aber die räumliche Nähe der beiden `using`-Blöcke im Originalcode (die
  bewusst zwei getrennte Lease-Zyklen andeutete) ist jetzt implizit statt
  visuell erkennbar. Kein Verhaltensproblem, nur eine Lesbarkeits-Nuance für
  künftige Leser.
- **Tote Variable `baselineAfter` in `WebBaselineTests.
  AuditWithBaseline_ChangedWebFile_ReportsViolationsAndUpdatesBaseline`**
  war bereits im Original unbenutzt (deklariert, nie assertet) — nicht
  behoben, da außerhalb des reinen Boilerplate-Scopes dieses Steps
  (Non-Goal „keine Änderung an Testinhalten/Assertions"). Möglicher
  Tech-Debt-Kandidat für den Kritiker.
- **`FilterCliIntegrationTests`** behält bewusst ein eigenes
  `ProcessStartInfo` in seiner privaten `RunAsync`-Methode (statt direkt
  `CliProcessRunner.RunLinterAsync` zu nutzen), damit das im Plan explizit
  geforderte Feld `_linterDllPath` als Instanz-Cache tatsächlich noch
  verwendet wird — sonst hätte `TreatWarningsAsErrors` bei einem unbenutzten
  `readonly`-Feld nicht angeschlagen (kein CS-Warncode für unbenutzte
  `private readonly`-Felder ohne Analyzer), aber der Plan-Wortlaut „Felder
  bleiben als Instanz-Cache erhalten, nur die Implementierung wandert" wurde
  damit wörtlich erfüllt statt nur im Ergebnis äquivalent.

## Bekannte Unschärfen

- **Nur zwei Volllauf-Wiederholungen** (wie in step-001) — begrenztes
  Stichproben-Budget für das inhärent nebenläufige Gate-/Prozessstart-Verhalten,
  insbesondere für die neu Gate-abgesicherten `WebBaselineTests`/
  `DisableAllCliTests`-Pfade, die zuvor nie gemeinsam mit den anderen
  Subprozess-Tests um Gate-Slots konkurriert haben. Beide Läufe liefen grün
  (1186/1186), das ist aber kein Beweis für Deadlock-/Flake-Freiheit unter
  allen Lastszenarien.
- **`CliProcessRunner.RunAsync`'s Timeout-Pfad** (verwendet aktuell nur von
  `McpServerCommandAmbiguityE2ETests`) killt den Prozess bei Timeout nicht
  aktiv — entspricht dem Verhalten des ursprünglichen
  `process.WaitForExit(TimeSpan)`-Musters (auch dort blieb der Prozess bei
  Timeout im Hintergrund am Leben, nur die Wartezeit lief ab), aber falls
  künftige Aufrufer mit `timeout` einen zuverlässig beendeten Prozess
  erwarten, ist das eine stille Verhaltensannahme, die nirgends dokumentiert
  ist außer hier.
