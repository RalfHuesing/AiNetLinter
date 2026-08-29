---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 021
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T10:27:36+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 021

## Verdict

- [x] issues
- [ ] approved
- [ ] blocked

Die beiden Step-020-Kernrisiken sind im Step-021-Stand behoben: Der Prozess
wird suspendiert erstellt, vor dem Resume dem Job zugeordnet, und die CTS
werden vor dem nativen Start erzeugt. Die direkte lokale Child-/Grandchild-
Regression sowie der Pre-Start-Timeout-Test laufen erfolgreich.

`issues` bleibt erforderlich, weil der Startfehlerpfad die native
Terminierung nicht fail-closed auswertet und die Test-`finally`-Bereinigung
keinen endlichen Wait-Nachweis erbringt.

## Geprüfte Kriterien

- [x] CREATE_SUSPENDED, AssignProcessToJobObject und ResumeThread sind in
  dieser Reihenfolge implementiert.
- [x] KILL_ON_JOB_CLOSE, SafeHandle-Besitz und idempotente Dispose-Pfade sind
  nachvollziehbar.
- [ ] Jeder Fehler nach CreateProcess garantiert nachweisbar die Beendigung
  eines noch nicht dem Job zugeordneten Prozesses.
- [x] Timeout- und Cancellation-Pfade beenden den Job unabhängig vom Zustand
  des Parent-Prozesses und warten endlich auf Prozess und Reader.
- [ ] Die `finally`-Bereinigung der direkten Integrationstests wartet endlich
  und verifiziert den Prozessbaum auch nach einem Testfehler.
- [x] stdout/stderr sind mengenmäßig begrenzt; Reader-, Prozess- und Cleanup-
  Waits sind endlich.
- [x] CTS-Überläufe werden vor ProcessStart abgewiesen.
- [x] Scope und bestehende Invarianten bleiben erhalten.

## Plan-Erfüllung und Korrektheit

### Prozess- und Cleanup-Bewertung

`ExternalSourceGitProcessLauncher.LaunchProcess` erstellt den Prozess mit
`CREATE_SUSPENDED`, ruft danach `AssignProcessToJobObject` und erst dann
`ResumeThread` auf (`ExternalSourceGitProcessLauncher.cs:128-178`). Das Job
Object setzt `KILL_ON_JOB_CLOSE` (`ExternalSourceGitProcessLauncher.cs:250-277`).
Nach dem erfolgreichen Start übernimmt `TreeScope` den Job-SafeHandle; die
Ressourcenklasse gibt die Ownership gezielt ab. `TreeScope.Dispose` ist über
`Interlocked` idempotent. Der Executor ruft `TerminateJobObject` auch dann
auf, wenn der Parent bereits beendet ist, und schließt anschließend Streams,
wartet begrenzt auf Prozess und Reader und entsorgt die Handles.

Die ursprüngliche Parent-exit-/Grandchild-open-pipe-Race ist damit behoben:
Der Parent darf enden, ohne dass der offene Grandchild-Pipe-Handle den
Executor unbegrenzt blockiert. Output wird bei 64 KiB je Stream abgeschnitten;
die Reader und `Task.WhenAll` werden in den Cleanup-Pfaden mit endlichen
Zeitfenstern beobachtet. `ExecuteAsync` erzeugt Timeout- und Linked-CTS vor
`scope.Start` und fängt dadurch auch den Überlauf-Test vor ProcessStart ab.

Offen bleibt der nicht dem Job zugeordnete Startfehlerpfad. Der Catch in
`LaunchProcess` ruft `TerminateCreatedProcess` auf
(`ExternalSourceGitProcessLauncher.cs:168-172`), aber
`TerminateCreatedProcess` verwirft sowohl den Rückgabewert von
`TerminateProcess` als auch das Ergebnis von `WaitForSingleObject`
(`ExternalSourceGitProcessLauncher.cs:324-333`). Schlägt insbesondere
`AssignProcessToJobObject` fehl, ist der suspendierte Prozess noch nicht durch
`KILL_ON_JOB_CLOSE` geschützt. Ein fehlschlagendes oder nur abgelaufenes
Terminate/Wait bleibt unsichtbar; das Schließen des Job-Handles kann diesen
Prozess dann nicht zuverlässig beseitigen. Das verletzt das im Step-Plan
geforderte fail-closed Verhalten und die kontrollierte Cleanup-Fehler-
behandlung.

Der direkte Integrationstest startet echte lokale `ProcessStartInfo`-
Prozesse mit `ArgumentList`, WorkingDirectory, Redirects und expliziter
`GIT_*`-Umgebungsbereinigung. Der Parent schreibt PID-Daten, beendet sich
sofort und lässt den Grandchild beide Pipes offen; Timeout und Cancellation
weisen den anschließenden Prozessbaum-Abbau nach. Der Pre-Start-Test verwendet
einen über `UInt32.MaxValue` liegenden Timeout und prüft über einen Startmarker,
dass kein Prozess gestartet wird. Die regulären Erfolgswege sind daher gut
abgedeckt. In `TerminateProcesses` ruft die `finally` jedoch nur
`Kill(entireProcessTree: true)` auf (`ExternalSourceGitProcessExecutorTests.cs:256-277`)
und wartet weder endlich mit `WaitForExit` noch verifiziert sie danach den
Abbau. Wenn eine vorherige Assertion, Operation-Wartezeit oder Test-Harness-
Operation fehlschlägt, kann die Testbereinigung ohne belastbaren Endzustand
enden.

### Konzepttreue und Invarianten

Der Step-021-Code-Commit ändert nur den Prozess-Launcher, die nativen
Job-/Handle-Hilfen, den TreeScope, den Executor und den direkten
Integrationstest. Step-019-HTTP-/Git-Statusklassifikation,
Credential-Schutz, TD-005, der Step-018-1314-/Reparse-Fallback und die
Provider-/Snapshot-/Refresh-/Cache-/Source-of-Truth-Grenzen bleiben
unverändert. Es gibt keinen `Assembly.Load`, keine Reflection-basierte
Ausweitung und keinen Remote-, Gitea- oder Git-Netzwerkzugriff im Review.

## Findings

### 1. Startfehler kann unzugeordneten Prozess überleben

- **Schweregrad:** CRITICAL
- **Kategorie:** Logik / Plan
- **Fundstelle:** `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs:324-333`
- **Reproduktion:** Der native `AssignProcessToJobObject`-Fehlerpfad aus
  `LaunchProcess` ist erreichbar; `TerminateCreatedProcess` ignoriert dort
  die Fehler-/Timeout-Ergebnisse von `TerminateProcess` und
  `WaitForSingleObject`.
- **Auswirkung:** Bei einem Fehler nach `CreateProcessW`, insbesondere vor
  erfolgreicher Job-Zuordnung, gibt es keine nachweisbare fail-closed
  Garantie. Ein unzugeordneter suspendierter Prozess kann trotz geschlossenem
  Job-Handle bestehen bleiben; die Primär-Exception enthält auch keinen
  kontrollierten Cleanup-Fehler.
- **Korrekturscope:** `TerminateCreatedProcess` muss beide nativen Ergebnisse
  prüfen, den endlichen Wait explizit auswerten und Cleanup-Fehler unter
  Erhalt der Primär-Exception sichtbar machen. Für den nicht zugeordneten
  Prozess ist ein belastbarer, bounded Fallback erforderlich. Ergänzend soll
  ein deterministischer Test für den Post-CreateProcess-
  Ownership-Initialisierungsfehler den Fail-Closed-Vertrag absichern.

### 2. Test-`finally` beweist keine endliche Prozessbaum-Bereinigung

- **Schweregrad:** MAJOR
- **Kategorie:** Logik / Plan
- **Fundstelle:** `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs:256-277`
- **Reproduktion:** Eine Assertion oder die äußere `WaitAsync`-Grenze kann
  fehlschlagen, bevor `WaitForProcessesToExitAsync` den normalen Pfad
  abschließt. Die `finally` ruft dann nur `Kill` auf und wartet nicht auf beide
  zuvor aufgezeichneten PIDs.
- **Auswirkung:** Der Test kann grün/fehlerhaft zurückkehren, während ein
  lokaler Child-/Grandchild-Prozess noch läuft. Das schwächt genau den
  geforderten direkten Nachweis für Prozessbaum-Abbau und kann nachfolgende
  Tests beeinflussen.
- **Korrekturscope:** Die Testbereinigung muss nach dem Kill einen bounded
  `WaitForExit`/PID-Recheck für Parent und Grandchild durchführen, Fehler
  sichtbar behandeln und auch im Fehlerpfad einen endlichen Abschluss
  erzwingen.

## Verifikation

### Build und Tests

- `dotnet build`: **grün**, 0 Fehler, 0 Warnungen.
- Fokussierte Step-021-Tests
  (`FullyQualifiedName~ExternalSourceGitProcessExecutorTests`): **5 passed,
  0 skipped**.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`:
  **1994 passed, 1 skipped, 1995 total**. Der Skip ist der bekannte echte
  Reparse-/Symlink-Test wegen Win32-Fehler 1314:
  `AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`:
  **365 passed, 0 skipped**.
- Stress wurde nicht ausgeführt.
- Nach dem Lauf waren keine Step-021-Test-DLL-, Test-Harness-,
  `tree.ps1`- oder `grandchild.ps1`-Prozesse aktiv.

### MCP-, DRY-, MagicValues- und DeadCode-Prüfung

Alle semantischen AiNetLinter-MCP-Aufrufe wurden mit dem absoluten
`projectRoot` `C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt. Die
betroffenen Produktionsdateien und der Integrationstest melden 0 Violations;
Symbol-, Referenz-, Test- und Impact-Abfragen zeigen den Executor mit fünf
direkt zugeordneten Tests und die erwarteten Aufrufer.

Der solution-weite `find_duplicates`-Lauf fand 28 bestehende Cluster, aber
keinen Step-021-Cluster. Die gezielten Near-/Exact-Läufe für
`src/AiNetLinter/Mcp/Assemblies` und
`src/AiNetLinter.IntegrationTests/Mcp/Assemblies` fanden jeweils 0 Cluster.
Die strukturellen Kandidaten waren ausschließlich Altbestand außerhalb des
Scopes. `find_magic_values` fand im Produktionsscope 7 bestehende bzw.
technisch begründete Werte und im Testscope 12 eindeutige Test-/Tool-Literale;
keinen neuen Refactoring-Bedarf. `find_dead_code` meldete 34 LOW-Kandidaten,
alle ABI-Felder der nativen Job-/Process-Strukturen; diese sind für Layout-
Kompatibilität erforderlich und dürfen nicht entfernt werden.

Gezieltes `rg` bestätigt die Reihenfolge von Create/Assign/Resume, die
bounded Waits, die `GIT_*`-Isolation und das Fehlen von Assembly.Load bzw.
Reflection im Step-021-Scope. Kein neuer Tech-Debt-Eintrag ist gerechtfertigt.

## Geänderte Dateien

- `tasks/decompiled-assembly-analysis/step-021/step-review.md`

`tech-debt.md`, `task-state.md`, `roadmap.md` und `codemap.md` wurden nicht
geändert. Es wurden keine Produktionsfixes vorgenommen.

## Folgeaktion

Die beiden Findings beheben, anschließend Step 021 erneut fokussiert und mit
den vollständigen Nicht-Stress-Läufen verifizieren. Erst danach kann der
Kritiker den Verdict auf `approved` setzen.
