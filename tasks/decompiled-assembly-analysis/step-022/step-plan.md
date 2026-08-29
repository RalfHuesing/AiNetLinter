---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 022
corrects: step-021
title: "Native Startfehler und Test-Cleanup fail-closed absichern"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T10:39:23+02:00
related_to:
  - ../step-021/step-review.md
  - ../step-021/step-result.md
  - ../step-021/step-plan.md
  - ../follow-up-strategy.md
---

# Step 022: Native Startfehler und Test-Cleanup fail-closed absichern

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und Fehlersemantik.
- **Korrektur von:** `step-021`, Review `step-021/step-review.md`.
- **Konzept-Referenz:** `Konzept.md`, Fehler-, Sicherheits- und
  Vertrauensvertrag mit bounded Cancellation-/Timeout-Grenzen und ohne
  Codeausführung.

## Scope und Out-of-scope

### In scope

- Die Post-Create-Fehlerbehandlung in
  `ExternalSourceGitProcessLauncher` so schärfen, dass die Ergebnisse von
  `TerminateProcess` und `WaitForSingleObject` explizit ausgewertet werden.
- Einen bounded, fail-closed Fallback für den noch nicht sicher einem Job
  zugeordneten, gerade erzeugten Prozess vorsehen. Der Fallback darf nur
  den aus `ProcessInformation` bekannten Prozess betreffen und muss den
  Prozesszustand vor dem Freigeben der nativen Handles nachweisen.
- Die Primär-Exception des Start-/Ownership-Fehlers erhalten und zusätzliche
  Cleanup-Fehler kontrolliert sichtbar machen, ohne sie aus einem Cleanup-
  `catch` heraus zu verschlucken oder die Primärursache still zu ersetzen.
- Den bestehenden lokalen Integrationstest um einen deterministischen
  Post-Create-Ownership-Fehler mit bounded Fallback-Nachweis erweitern.
  Ein dafür nötiger Native-Testseam bleibt intern, pro Aufruf begrenzt und
  verändert weder `IExternalSourceGitProcessExecutor` noch den Transport-
  Vertrag.
- Die `finally`-Bereinigung der bestehenden Parent-/Grandchild-Tests in
  einen asynchronen, endlichen Ablauf überführen: Kill versuchen, danach
  beide aufgezeichneten PIDs bounded abwarten und das Ende ausdrücklich
  verifizieren.

### Out of scope

- `ExternalSourceRepositoryFailurePolicy`, HTTP-/Git-Klassifikation,
  Credentials, Redaction, gemeinsame URL-Policy und Success-Factory aus
  `TD-005`.
- Provider, Snapshot, Refresh, Fetch, Cache, Source-of-Truth und Host-
  Wiring sowie MCP-Registrierungen.
- Die bereits bestätigte `CREATE_SUSPENDED`-Sequenz,
  `AssignProcessToJobObject` vor `ResumeThread`, `KILL_ON_JOB_CLOSE`, die
  normalen TreeScope-Cleanup-Pfade und die 1314-/Reparse-Fallback-Regel;
  sie werden nur als Invarianten geprüft, nicht neu geplant.
- Änderungen an `task-state.md`, `roadmap.md`, `codemap.md` oder
  `tech-debt.md`. Es wird kein neuer Tech-Debt-Eintrag und kein separater
  Audit-/Cleanup-Sweep erzeugt.
- Git-Remote, Gitea, HTTP, Netzwerk, echte Credentials, externe Restores,
  Stress-Tests, unbounded Waits, Assembly-Loading, Reflection oder eine
  Änderung von Systemprivilegien.

## Aktueller Projektzustand (JIT-Kontext)

Die semantischen AiNetLinter-MCP-Abfragen wurden mit
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt; ergänzende
Textprüfung erfolgte gezielt mit `rg`.

- `ExternalSourceGitProcessLauncher.LaunchProcess` erstellt den Prozess
  suspendiert, ordnet ihn dem Job zu und resumed ihn erst danach
  (`ExternalSourceGitProcessLauncher.cs:128-178`). Im Catch wird der noch
  gehaltene native `hProcess` an `TerminateCreatedProcess` übergeben.
- `TerminateCreatedProcess` liegt bei
  `ExternalSourceGitProcessLauncher.cs:324-333`. Es prüft nur, ob ein Handle
  nutzbar ist, ruft `TerminateProcess` und anschließend
  `WaitForSingleObject` auf, verwirft aber deren Rückgabewerte. Besonders
  beim Fehler von `AssignProcessToJobObject` ist der Prozess noch nicht
  durch `KILL_ON_JOB_CLOSE` geschützt.
- `ExternalSourceGitProcessTreeScope` übernimmt den Job erst nach
  erfolgreichem Launcher-Start und beendet im normalen Executor-Cleanup den
  Job unabhängig vom Parent-Status. Dieser genehmigte Pfad ist nicht der
  betroffene Startfehler.
- `ExternalSourceGitProcessExecutor.ExecuteAsync` hält Timeout- und
  Linked-CTS bereits vor `ExternalSourceGitProcessTreeScope.Start`; der
  bestehende Executor- und Result-Vertrag ist daher nicht Gegenstand dieser
  Korrektur.
- `ExternalSourceGitProcessExecutorTests.TerminateProcesses` liegt bei
  `ExternalSourceGitProcessExecutorTests.cs:256-277`. Der Helper ruft für
  noch laufende PIDs nur `Kill(entireProcessTree: true)` auf und wartet
  anschließend weder bounded auf Parent und Grandchild noch verifiziert er
  deren Ende. Die normalen Testpfade nutzen bereits
  `TestWaiter.WaitForConditionAsync` mit einer endlichen Grenze.
- Die direkte `System.Diagnostics.Process`-Nutzung gehört wegen des
  bestehenden `FastTestsDependencyGuardTests` in den vorhandenen
  `IntegrationTests`-Harness. Die betroffenen Produktions- und Testdateien
  melden aktuell keine AiNetLinter-Verstöße; Low-Confidence-Treffer an
  nativen ABI-Feldern sind kein Entfernungsauftrag.

## Bündelungsentscheidung

Step 022 bleibt ein einzelner, gebündelter Korrektur-Step mit
`step_type: single` und `estimated_risk: high`. Die beiden Findings bilden
eine Prozessbesitzgrenze: Der Launcher muss einen Post-Create-Fehler
fail-closed und mit sichtbarer Primärursache behandeln, während der direkte
Harness genau diesen Endzustand auch im `finally` bounded nachweisen muss.
Sie werden nicht in zwei Mini-Steps oder in einen unabhängigen Audit-Sweep
aufgeteilt.

Das Split-Gate ist eingehalten:

- **Fachverträge:** genau zwei eng gekoppelte Verträge — (1) native
  Startfehler-/Prozessbesitz-Cleanup und (2) verifizierbarer bounded
  Integration-Test-Cleanup.
- **Schichten:** genau drei unmittelbar betroffene Schichten — native
  Launcher-/Handle-Grenze, der bestehende Managed-Lifecycle und der lokale
  Integrationstest-Harness.
- **Akzeptanzkriterien:** acht, siehe unten.
- **`read_first`:** zwölf zentrale Dateien, siehe Kontextbudget.

DRY-, MagicValues- und DeadCode-Prüfungen bleiben auf diese Prozess-/Cleanup-
Grenze begrenzt. Nur ein unmittelbar durch den Fix entstehendes Duplikat,
ein unbenannter neuer Native-Statuswert oder ein sicher unreferenzierter
neuer Helper darf in demselben Paket bereinigt werden. Die bestehenden
Native-ABI-Felder werden nicht als Dead Code entfernt.

## Intention

Ein Fehler zwischen `CreateProcessW` und erfolgreicher Job-Zuordnung darf
keinen ungeschützten Prozess zurücklassen und darf seine Primär-Exception
nicht durch fehlgeschlagenes Cleanup verlieren. Die Testregressionen sollen
den Fallback und den Parent-/Grandchild-Endzustand mit ausschließlich lokalen
Prozessen und endlichen Wartefenstern belastbar sichtbar machen.

## Akzeptanzkriterien

1. `TerminateCreatedProcess` beziehungsweise der dafür extrahierte interne
   Cleanup-Helper wertet `TerminateProcess` und `WaitForSingleObject` aus.
   `TerminateProcess == false` wird nicht als Erfolg behandelt; der Wait
   wird trotzdem bounded ausgeführt, damit ein bereits beendeter Prozess nur
   durch ein signalisiertes Handle als beendet gilt. `WAIT_TIMEOUT`,
   `WAIT_FAILED` und unerwartete Wait-Ergebnisse werden als sichtbare
   Cleanup-Fehler erfasst.
2. Wenn die native Terminierung den Prozess nicht nachweist, verwendet der
   Fallback ausschließlich den bekannten, gerade erzeugten Prozess aus
   `ProcessInformation` und beendet ihn mit einer ebenfalls bounded
   Prozess-/Tree-Operation. Vor dem Schließen der nativen Handles wird der
   Endzustand erneut geprüft. Kann das Ende nicht nachgewiesen werden,
   meldet der Launcher einen Fehler statt einen erfolgreichen Start oder
   einen erfolgreichen Cleanup zu behaupten; das Schließen des Job-Handles
   zählt für einen nicht zugeordneten Prozess nicht als Nachweis.
3. Der Fehlerpfad bewahrt die Primär-Exception aus Create-/Ownership-
   Initialisierung. Zusätzliche Fehler aus Termination, Wait, Fallback oder
   Handle-Cleanup werden gesammelt und kontrolliert an diese Ursache
   angehängt beziehungsweise als Aggregate mit erhaltener Primärursache
   sichtbar gemacht. Kein Cleanup-`catch` ist leer und kein Cleanupfehler
   ersetzt still die Primär-Exception.
4. Eine direkte Integration-Regression erzwingt nach dem realen
   `CreateProcessW` einen simulierten Ownership-Initialisierungsfehler und
   lässt `TerminateProcess`/Wait mindestens einmal in den nicht erfolgreichen
   Pfad laufen. Der bounded Fallback beendet den lokalen Prozess; die
   aufgezeichnete PID ist innerhalb einer endlichen Grenze nicht mehr aktiv,
   und die ursprüngliche Start-/Ownership-Exception bleibt beobachtbar.
   Der dafür eingesetzte Native-Testseam ist intern, nicht global zustands-
   behaftet und kein neuer öffentlicher Fachvertrag.
5. Die `finally`-Bereinigung in
   `ExternalSourceGitProcessExecutorTests` führt den Kill-Versuch für alle
   bekannten PIDs aus, wartet danach bounded auf Parent und Grandchild und
   verifiziert beide mit einem abschließenden Nicht-mehr-laufend-Nachweis.
   Ein Kill-/Lookup-Fehler für eine bereits beendete PID verhindert nicht den
   Cleanup-Versuch für die übrigen PIDs; ein nicht verifizierbares Ende bleibt
   als Testfehler sichtbar.
6. Die bestehenden lokalen Timeout- und Caller-Cancellation-Regressionen
   bleiben aussagekräftig: `WasTimedOut`, Original-Cancellation-Token,
   bounded Output und der beendete Parent-/Grandchild-Prozessbaum bleiben
   nach der `finally`-Änderung geprüft. Kein Test wartet ohne endliche
   Grenze und nach dem Test bleiben keine bekannten Harness-PIDs aktiv.
7. `CREATE_SUSPENDED` → `AssignProcessToJobObject` → `ResumeThread`,
   `KILL_ON_JOB_CLOSE`, sichere `ArgumentList`-/Redirect-/WorkingDirectory-
   Übergabe, `GIT_*`-Isolation, Secret-Redaktion und die vorhandenen
   HTTP-/Git-/1314-/Reparse-Verträge bleiben unverändert. Native ABI-Felder
   mit bestehender Low-Confidence-Verwendung bleiben erhalten.
8. Der Coder weist die fokussierte Executor-Regression, `dotnet build` und
   beide vollständigen Nicht-Stress-Gates grün nach. Stress-Tests,
   Netzwerk-/Remote-Aktivität, echte Credentials und Systemprivilegien-
   änderungen werden nicht verwendet.

## Konkrete Änderungen

### Native Post-Create-Besitzgrenze

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs:128-178, 306-333`

- **Was:** Den Catch in `LaunchProcess` so strukturieren, dass die
  Primär-Exception zuerst gesichert wird und der Startfehler-Cleanup alle
  Termination-, Wait- und Fallback-Fehler in einer vorhandenen oder eng
  gekapselten Fehlerstruktur sammelt. `TerminateCreatedProcess` prüft den
  booleschen Rückgabewert von `TerminateProcess` und die konkreten
  `WaitForSingleObject`-Ergebnisse mit benannten Statuswerten.
- **Wie:** Ein `false` von `TerminateProcess` führt nicht zum frühen Return.
  Nach dem bounded Wait darf nur ein signalisiertes Prozesshandle als
  nachgewiesenes Ende gelten. Bei Timeout/Fehler wird für die bekannte PID
  ein enger managed/native Fallback versucht und wiederum bounded auf das
  Ende gewartet. Die native Handles bleiben bis nach diesem Nachweis gültig;
  es gibt keine PID-Suche über fremde Prozesse.
- **Warum:** Bei einem `AssignProcessToJobObject`-Fehler ist der suspendierte
  Prozess noch nicht durch den Job geschützt. Ein ignorierter Native-Status
  kann daher einen Prozess hinterlassen und die Primärursache verschleiern.

#### Interner Native-Testseam im Launcher-/Native-Helper-Scope (nur falls für
den deterministischen Test aus Kriterium 4 erforderlich)

- **Was:** Die drei relevanten Native-Aufrufe für Assignment, Termination und
  Wait über eine schmale, pro Startaufruf übergebene interne Operations-
  Struktur oder gleichwertige Delegates testbar machen. Der Runtime-Pfad
  verwendet weiterhin direkt die bestehenden `kernel32.dll`-Imports.
- **Wie:** Keine statische mutable Test-Hook-Variable und keine Änderung an
  `IExternalSourceGitProcessExecutor`. Der Testseam muss den nach
  `CreateProcessW` bekannten Prozessmarker/PID erfassen können und danach
  einen deterministischen Assignment-Fehler sowie einen fehlgeschlagenen
  Native-Termination-/Wait-Versuch liefern. Falls die bestehende
  `ExternalSourceGitProcessLauncher.cs` dadurch das 500-Zeilen-Limit
  überschreitet, die eng begrenzte Startfehlerlogik in eine kleine interne
  Helper-Datei auslagern, nicht in eine allgemeine Prozessabstraktion.
- **Warum:** Ein echter `AssignProcessToJobObject`-Fehler ist ohne
  privilegierte oder fragile Hostzustände nicht reproduzierbar. Der
  Testseam hält den Nachweis lokal und deterministisch, ohne den
  Produktionsvertrag zu verbreitern.

### Verifizierbarer Test-Cleanup

#### `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs:98-277`

- **Was:** `TerminateProcesses` in einen asynchronen Helper mit endlichem
  Wait umwandeln. Nach dem Kill-Versuch für die bekannten Parent-/Grandchild-
  PIDs `WaitForProcessesToExitAsync` verwenden und anschließend jede PID
  explizit über `IsProcessRunning` als beendet bestätigen. Erwartete
  Already-Exited-/Lookup-Zustände bleiben resilient; ein ausbleibender
  Endnachweis wird nicht still ignoriert.
- **Wie:** Die vorhandenen zwei `finally`-Blöcke müssen den async Cleanup
  abwarten. Der Prozessbaum bleibt lokal und wird nur über die in der
  Fixture aufgezeichneten PIDs angesprochen. Keine Sleeps, Prozesslisten-
  Scans, globalen Testserialisierungen oder unbounded `WaitForExit`-Aufrufe.
  Der neue Ownership-Fehlertest nutzt denselben lokalen
  `TestTempDirectory`-Ansatz und markiert keine Secrets.
- **Warum:** Ein Test darf auch nach einer Assertion oder Harness-Exception
  nicht erfolgreich zurückkehren, während Parent oder Grandchild noch
  laufen. Der Cleanup-Nachweis muss selbst bounded und überprüfbar sein.

### Begrenzter Qualitätscheck

- Nach dem Fix `get_violations`, `get_impact`/`find_references` sowie
  `find_duplicates`, `find_magic_values` und `find_dead_code` ausschließlich
  auf die betroffenen Launcher-/Native-/Executor-Testbereiche anwenden.
- Nur direkt verursachte Befunde im Prozess-/Cleanup-Vertrag bearbeiten.
  Bestehende Low-Confidence-Native-ABI-Felder, `TD-001` bis `TD-005` und
  fachfremde solutionweite Cluster bleiben unangetastet.

## Geplante Implementierungsdateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs` —
  Post-Create-Catch, per-Aufruf-Native-Operationen und unveränderte
  Create/Assign/Resume-Reihenfolge.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartFailureCleanup.cs`
  — neuer, eng begrenzter interner Helper für Statusauswertung, bounded
  Fallback und kontrollierte Fehlerweitergabe; kein allgemeiner Prozess-
  oder Transportabstraktions-Layer.
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs`
  — deterministischer Post-Create-Fallback-Test und bounded, verifizierbarer
  `finally`-Cleanup der bestehenden Child-/Grandchild-Tests.

`ExternalSourceGitProcessNativeMethods.cs` bleibt als bestehende ABI-/P/Invoke-
Quelle unverändert, sofern die Statusauswertung ohne Signaturänderung möglich
ist. Eine Erweiterung dieser Datei ist nur dann Teil des Scopes, wenn der
interne Testseam die bereits vorhandenen Imports dort ohne ABI-Änderung
präziser kapseln muss.

## Tests

- [ ] Direkte Integration-Regression für einen Post-Create-
  Ownership-Fehler: lokaler Prozess, erfasste PID, erzwungenes
  `TerminateProcess`-/Wait-Fehlschlagen, bounded Fallback, kein aktiver
  Prozess und sichtbare Primär-Exception.
- [ ] Bestehender lokaler Timeout-Test für Child/Grandchild: bounded
  Rückkehr mit `WasTimedOut`, beide PIDs beendet und `finally`-Cleanup mit
  abschließendem PID-Nachweis.
- [ ] Bestehender lokaler Caller-Cancellation-Test: bounded
  `OperationCanceledException` mit Originaltoken, beide PIDs beendet und
  derselbe `finally`-Nachweis.
- [ ] Bestehender Real-Executor-Test für StartInfo, ArgumentList, Redirects,
  stdin, WorkingDirectory, bounded Output und `GIT_*`-Isolation bleibt grün.

Der Planer führt keine Tests aus. Der Coder führt nach der Implementierung
mindestens diese fokussierten und projektweiten Gates aus:

```powershell
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceGitProcessExecutorTests"
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Der bekannte echte Reparse-Test darf ausschließlich wegen
`ERROR_PRIVILEGE_NOT_HELD (1314)` übersprungen werden. Stress-Tests,
Netzwerk, Remote-Repositories, Gitea, Credentials und Privilegienänderungen
bleiben ausgeschlossen.

## Definition of Done

- [ ] Alle acht Akzeptanzkriterien sind umgesetzt und durch direkte lokale
  Regressionen prüfbar.
- [ ] Jeder native Termination-/Wait-Status ist behandelt; ein nicht
  nachgewiesenes Ende führt fail-closed zu einer sichtbaren Fehlerursache.
- [ ] Die Primär-Exception bleibt bei Cleanupfehlern beobachtbar und der
  Test-`finally`-Cleanup verifiziert Parent und Grandchild bounded.
- [ ] Der Coder schreibt `step-result.md` und setzt den Planstatus auf
  `done (pending audit)`; Planabweichungen werden dort dokumentiert.
- [ ] Build und beide vollständigen Nicht-Stress-Gates sind grün; kein
  lokaler Testprozess bleibt zurück.
- [ ] Der Code-Commit verwendet eine deutsche Conventional-Commit-Message
  im Imperativ mit dem Suffix `[decompiled-assembly-analysis]`.

## Kontextbudget

### `read_first` (maximal 12 Dateien)

1. `tasks/decompiled-assembly-analysis/step-021/step-review.md` — die zwei
   offenen CRITICAL-/MAJOR-Findings und ihre konkreten Fundstellen.
2. `tasks/decompiled-assembly-analysis/step-021/step-result.md` — der
   tatsächlich implementierte Job-/CTS-/Cleanup-Stand.
3. `tasks/decompiled-assembly-analysis/step-021/step-plan.md` — die bereits
   bestätigten Besitz-, Secret- und Scope-Invarianten.
4. `tasks/decompiled-assembly-analysis/follow-up-strategy.md` — Split-Gate,
   Kontextbudget und neuer Agenten-Handoff.
5. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs` —
   nativer Start, Job-Zuordnung und fehlerhafter Post-Create-Cleanup.
6. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessNativeMethods.cs`
   — P/Invoke-Signaturen, Wait-Status und ABI-Strukturen.
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs` —
   bestehender Managed-Lifecycle, CTS und normaler Cleanup-Aufruf.
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessTreeScope.cs` —
   Job-/Pipe-Ownership und Abgrenzung zum erfolgreichen Startpfad.
9. `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs`
   — lokale Child-/Grandchild-Fixture, PID-Marker und `finally`.
10. `.agents/rules/AiNetLinter.mdc` — Nullable-, sealed-, Größen-, Catch-,
    Test- und ABI-relevante C#-Regeln.
11. `.agents/rules/AiNetLinterRichtlinien.mdc` — Windows-, Secret-,
    bounded-Cleanup-, TestTemp- und Drift-Regeln.
12. `.agents/rules/AiNetLinter-McpWorkflow.mdc` — MCP-Priorität und absoluter
    `projectRoot` für semantische C#-Abfragen.

### `read_on_demand`

- `src/AiNetLinter.TestKit/TestWaiter.cs` — nur zur Bestätigung der
  vorhandenen endlichen Polling-Grenze und ihres Fehlerverhaltens.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartupResources.cs`
  — nur falls die Fehlerweitergabe beim Ressourcen-Dispose die
  Primär-Exception berührt.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessContracts.cs` —
  nur für eine konkrete Prüfung, dass Request-/Result-/Cancellation-Verträge
  unverändert bleiben.
- `src/AiNetLinter.FastTests/Architecture/FastTestsDependencyGuardTests.cs`
  — nur falls die Testseam-Platzierung gegen das Prozessverbot für FastTests
  abgeglichen werden muss.
- `src/AiNetLinter.IntegrationTests/AiNetLinter.IntegrationTests.csproj` und
  `src/AiNetLinter/Core/LinterEngine.cs` — nur für Testprojekt-/Internals-
  Visible-Kontext oder Compile-Vorgaben.
- `Directory.Build.props` — nur falls eine neue interne Datei konkrete
  Build-/Nullable-Vorgaben benötigt.

### `out_of_scope`

- `ExternalSourceRepositoryFailurePolicy.cs`,
  `GiteaGitRepositoryTransport.cs`,
  `ExternalSourceRepositoryAcquirer.cs`, Provider-/Snapshot-/Host-Wiring,
  Refresh, Fetch, Cache, Mapping und Source-of-Truth.
- HTTP-/Git-Statusklassifikation, Credential-Resolver, URL-/Success-Policy,
  `TD-005` sowie die 1314-/Reparse-Projektion.
- `FastTests`-Prozess-Harness, echte Git-/Gitea-Remotes, Netzwerk,
  Credentials, externe Restores, Stress-Tests und Systemprivilegien.
- `task-state.md`, `roadmap.md`, `codemap.md`, `tech-debt.md` und alle
  nicht unmittelbar durch diesen Start-/Test-Cleanup verursachten Audits.
- Assembly.Load, AssemblyLoadContext, Reflection und allgemeine neue
  Artefakt-/Prozessabstraktionen außerhalb des bestehenden Executors.

## Invarianten

- Der Prozessstart bleibt `CREATE_SUSPENDED` →
  `AssignProcessToJobObject` → `ResumeThread`; `KILL_ON_JOB_CLOSE` und die
  SafeHandle-/TreeScope-Ownership werden nicht gelockert.
- Nur ein nachgewiesenes, signalisiertes Handle oder ein bounded, final
  verifizierter Fallback gilt als Prozessende. `HasExited` des Parents und
  das Schließen eines nicht zugeordneten Jobs sind kein Tree-Nachweis.
- Die Primär-Exception bleibt sichtbar; Cleanupfehler werden beobachtet und
  kontrolliert angehängt. Caller-Cancellation, Request-Timeout und normale
  Prozessresultate behalten ihre aktuelle Semantik.
- stdout/stderr bleiben bounded und cancellation-aware. Jeder Reader-,
  Prozess- und Test-Wait besitzt eine endliche Grenze; kein erzeugter Task
  bleibt unobserved.
- `ProcessStartInfo`, `ArgumentList`, Redirects, deaktiviertes stdin,
  Arbeitsverzeichnis, `GIT_*`-Isolation und Secret-Redaktion bleiben
  unverändert.
- HTTP-/Git-Klassifikation, Credentials, `TD-005`, 1314-/Reparse-Fallback,
  Provider, Snapshot, Refresh, Cache, Source-of-Truth und Host-Wiring
  bleiben unverändert.
- Es gibt keinen öffentlichen API-/MCP-/Mapping-Change, kein
  Assembly-Loading, keine Reflection und keine Systemprivilegienänderung.
- Bestehende native ABI-Low-Confidence-Felder sind wegen ihrer
  Layout-Bedeutung kein Dead-Code und werden nicht entfernt.

## Risiken und Gegenmaßnahmen

- **Fallback trifft einen falschen Prozess:** Nur die unmittelbar aus
  `ProcessInformation` stammende PID und der weiterhin gehaltene native
  Handle dürfen verwendet werden; keine globale Prozesssuche oder
  Namensheuristik. Der bounded Endnachweis erfolgt vor Handle-Freigabe.
- **False Success bei `TerminateProcess == false`:** Den Status nicht
  ignorieren; den Wait immer ausführen und nur `WAIT_OBJECT_0` als
  Nachweis akzeptieren. Timeout, Fehler und unerwartete Status werden
  sichtbar gesammelt.
- **Cleanup maskiert die Primär-Exception:** Vor jedem Cleanup die Ursache
  capturen, Fehler sammeln und die Ursache über die bestehende
  Attachment-/Aggregate-Semantik erhalten. Kein Cleanup-Fehler darf durch
  ein ungeschütztes `throw` die Ursache ersetzen.
- **Nicht reproduzierbarer Testfehler:** Den Native-Testseam als per-Aufruf-
  Wert übergeben, keine globale mutable Hook verwenden, und die reale
  `CreateProcessW`-/PID-Kette mit einem lokalen Child testen. Die
  `IntegrationTests`-Platzierung bleibt erhalten.
- **Test-`finally` bleibt selbst unbounded:** Den Cleanup-Helper async
  machen, `TestWaiter` mit fester Zeitgrenze verwenden, Kill-/Lookup-Fehler
  pro PID resilient behandeln und danach jede PID nochmals explizit prüfen.
- **Scope-/Regeldrift:** Keine Änderungen an den bereits genehmigten
  Transport-, Fallback- oder Sessionverträgen. Scoped MCP-/DRY-/MagicValues-
  /DeadCode-Prüfungen dienen nur der unmittelbaren Prozessgrenze.
- **ABI-/Dateigrößenrisiko:** Neue Statuswerte benennen und die bestehende
  Interop-Struktur unverändert lassen. Falls der Launcher über 500 Zeilen
  wächst, nur die Startfehlerlogik in einen kleinen internen Helper teilen.

## Coder-Handoff

### Sicherer Einstieg

1. Diesen Handoff sowie die zwölf `read_first`-Dateien lesen. Danach mit
   `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` zuerst
   `get_feature_context`/`get_symbol_body` für
   `ExternalSourceGitProcessLauncher.LaunchProcess`,
   `ExternalSourceGitProcessLauncher.TerminateCreatedProcess`,
   `ExternalSourceGitProcessTreeScope` und
   `ExternalSourceGitProcessExecutorTests.TerminateProcesses` ausführen.
   `find_references` und `get_impact` nur für konkrete Call-Sites benutzen;
   `rg` auf die genannten Native-Status-, PID- und Wait-Muster begrenzen.
2. Zuerst den Startfehlerpfad modellieren: Primär-Exception sichern,
   `TerminateProcess`- und `WaitForSingleObject`-Ergebnisse mit benannten
   Statuswerten behandeln, bounded Fallback nur auf den bekannten
   Prozess anwenden und den Endzustand vor dem Handle-Cleanup nachweisen.
   Die bestehende Create/Assign/Resume-Reihenfolge und Job-Grenze bleiben
   unverändert.
3. Den erforderlichen Testseam klein und per-Aufruf halten. Er darf nur
   Assignment/Termination/Wait für den deterministischen Test beeinflussen;
   `CreateProcessW`, `ProcessInformation` und der managed Fallback bleiben
   real. Kein öffentlicher Port, keine globale Hook und kein privilegierter
   Fehlertrigger.
4. Danach die vorhandene Integrationstestklasse ändern: der Ownership-
   Fehlerfall prüft lokale PID und sichtbare Primär-Exception; die beiden
   vorhandenen `finally`-Pfade warten bounded auf Parent und Grandchild und
   verifizieren deren Ende explizit. Keine neuen externen Testabhängigkeiten.
5. Zum Abschluss scoped `get_violations`/`get_impact` sowie begrenzte
   `find_duplicates`, `find_magic_values` und `find_dead_code` für die
   geänderten Dateien ausführen. Native ABI-Felder nicht als Dead Code
   entfernen und keine fachfremden Debt-Einträge anfassen. Danach die vier
   im Testabschnitt genannten Commands ausführen und `step-result.md`
   schreiben; dieser Planer führt keine Tests aus.

### Übergabeinvarianten

- Ein Post-Create-Fehler bleibt fail-closed: kein erfolgreicher Start ohne
  Job-Zuordnung, kein stiller Termination-/Wait-Fehler und kein behauptetes
  Prozessende ohne bounded Nachweis.
- Die Primär-Exception bleibt beobachtbar; zusätzliche Cleanupfehler sind
  sichtbar, kontrolliert und nicht unobserved.
- Der Test-Finally-Cleanup ist bounded, versucht alle bekannten PIDs und
  verifiziert Parent sowie Grandchild nach dem Kill ausdrücklich.
- Alle bestätigten Prozess-, Pipe-, Output-, Secret-, Cancellation-,
  Klassifikations- und Fallback-Invarianten aus Step 021 bleiben erhalten.

### Nächster sicherer Einstiegspunkt

Beginne in
`ExternalSourceGitProcessLauncher.LaunchProcess` und
`ExternalSourceGitProcessLauncher.TerminateCreatedProcess` bei
`src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs:128`
beziehungsweise `:324`. Prüfe danach die nativen Signaturen und Statuswerte
in `ExternalSourceGitProcessNativeMethods.cs`. Erst anschließend den
lokalen Test-`finally`-Helper bei
`ExternalSourceGitProcessExecutorTests.cs:256` und den neuen deterministischen
Post-Create-Fehlerfall ergänzen.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — C#-Symbol-, Referenz- und
  Impact-Fragen zuerst über AiNetLinter-MCP mit absolutem `projectRoot`,
  `rg` nur ergänzend für gezielte Textarbeit.
- `.agents/rules/AiNetLinter.mdc` — Nullable, sealed, kurze Methoden,
  begrenzte Komplexität, sichtbare Fehler, Testsentinel und Erhalt der
  nativen ABI-Strukturen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — statische Architektur,
  Windows-/Cancellation-/Secret-Sicherheit, zentrale TestTempDirectory,
  bounded Testprozesse und begrenzter DRY-/MagicValues-/DeadCode-Abbau.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md §6.2.1` — flacher
  Korrektur-Step mit `corrects: step-021`; keine Roadmap-Änderung.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md §10.3, §10.5,
  §10.7` — Commit-Suffix, Korrekturkettenbudget und dichte Artefakte.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md`
  — Fix-Modus, Ist-Zustand, Kontextbudget und kein Commit durch den Planer.

## Bekannte Ausnahmen

- Der bestehende echte Reparse-Test darf weiterhin ausschließlich bei
  `ERROR_PRIVILEGE_NOT_HELD (1314)` übersprungen werden. Step 022 ändert
  diesen Test und die Capability-Projektion nicht.
- Der reale Child-/Grandchild-Harness bleibt in `IntegrationTests`, weil
  `FastTestsDependencyGuardTests` direkte `System.Diagnostics.Process`-
  Referenzen dort nicht zulässt.
- Der Planer führt keine Tests aus. Fehlende lokale Test-Infrastruktur oder
  ein nicht erreichbares Tool wird vom Coder gemäß Drift-Loop als
  Infrastruktur-Blocker behandelt, nicht durch externe Aktivität umgangen.

## Notes

Step 022 ist ausschließlich die Korrektur der zwei Step-021-Findings. Der
Native-Testseam darf den vorhandenen Prozess-/Cleanup-Vertrag prüfbar machen,
aber keine allgemeine Prozessabstraktion oder neue fachliche Transportgrenze
einführen. Nach der Genehmigung bleibt der erfolgreiche
Acquirer-zu-Snapshot-/Workspace-Anschluss das nächste EPIC-04-Paket.
