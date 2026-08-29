---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 021
corrects: step-020
title: "Prozessbaum-Besitz und Timeout-Vorbereitung am Git-Executor schließen"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T09:08:56+02:00
related_to:
  - ../step-020/step-review.md
  - ../step-020/step-result.md
  - ../step-020/step-plan.md
  - ../follow-up-strategy.md
  - ../Konzept.md
---

# Step 021: Prozessbaum-Besitz und Timeout-Vorbereitung am Git-Executor
# schließen

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und Fehlersemantik.
- **Korrektur von:** `step-020`, Review `step-020/step-review.md`, zwei
  CRITICAL-Findings an derselben Prozessbesitzgrenze.
- **Konzept-Referenz:** Sicherheits- und Fehlervertrag in `Konzept.md`,
  insbesondere „Keine Codeausführung“, Timeout/Cancellation und bounded
  Prozessaufrufe.

## Scope und Out-of-scope

### In scope

- Eine dauerhafte, race-resistente Besitzgrenze für den gestarteten
  Prozessbaum einschließlich geerbter stdout-/stderr-Pipes herstellen.
- Den gemeinsamen Cleanup-Pfad für Parent-exit, Grandchild-Race,
  Reader-/Wait-Ausnahme, Timeout und Caller-Cancellation ausnahmesicher
  und vollständig bounded machen.
- Timeout-CTS und verknüpfte Cancellation vor `Process.Start()` prüfen
  und erzeugen, sodass ein übergroßer oder anderweitig nicht darstellbarer
  positiver Timeout keinen Prozessstart mehr passieren lässt.
- Den bestehenden lokalen Real-Executor-Harness um Parent-exit- und
  Pre-Start-Timeout-Regressionen erweitern. Die Tests verwenden ausschließlich
  lokale Child-/Grandchild-Skripte.

### Out of scope

- Jede Änderung an `ExternalSourceRepositoryFailurePolicy`, der
  statusbewussten HTTP-/Git-Klassifikation, der Credential-Redaktion, der
  gemeinsamen URL-Policy oder der Success-Factory aus `TD-005`.
- Provider-/Snapshot-/Workspace-/Host-Wiring, MCP-Registrierung, Fetch,
  Refresh, persistenter Cache, Manifest, Generation und Source-of-Truth.
- Änderungen an `task-state.md`, `roadmap.md`, `codemap.md` oder
  `tech-debt.md`; `TD-005` bleibt erledigt und der Korrektur-Step ändert
  keine Roadmap.
- Git, Gitea, HTTP, Netzwerk, Remote-Repositories, echte Credentials,
  externe Restores, Stress-Tests oder Testprozesse mit unbounded Waits.
- `Assembly.Load`, `AssemblyLoadContext`, Reflection, Systemprivilegien-
  änderungen oder ein globaler Capability-Preflight.

## Aktueller Projektzustand (JIT-Kontext)

Die AiNetLinter-MCP-Abfragen mit
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` bestätigen:

- `ExternalSourceGitProcessExecutor` liegt in
  `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs`.
  `ExecuteAsync` startet den Prozess und delegiert erst danach an den
  Ablauf, der Timeout-CTS und Linked-CTS in Zeile 45 ff. erzeugt. Dadurch
  liegt ein Fehler der CTS-Erzeugung nach `Process.Start()` außerhalb des
  Cleanup-`try`.
- `CleanupProcessAsync` verwendet bereits bounded Reader-/Process-Waits,
  aber `TryKillProcessTree` beendet den Cleanup bei `HasExited == true` mit
  Erfolg, ohne `Kill(entireProcessTree: true)` auszuführen. Ein Parent kann
  daher beendet sein, während ein Grandchild geerbte Pipes offen hält.
- Der Executor ist ein interner, einzelner Real-Executor mit 448 Zeilen,
  25 Membern und 0 aktuellen Linter-Verstößen. Die bestehende
  `IExternalSourceGitProcessExecutor`-Injektion und der
  `CreateStartInfo`-Vertrag sind von `GiteaGitRepositoryTransport` und dem
  Integration-Harness abhängig.
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs`
  weist bereits `ProcessStartInfo`, `ArgumentList`, Redirects, stdin,
  Arbeitsverzeichnis, `GIT_*`-Isolation, bounded Output sowie Timeout- und
  Cancellation-Cleanup für einen noch laufenden Parent nach. Das
  `ProcessTreeScript` beendet den Parent bisher nicht vor dem Cleanup und
  reproduziert den Review-Race deshalb nicht.
- Direkte `System.Diagnostics.Process`-Referenzen sind durch das bestehende
  `FastTestsDependencyGuardTests`-Muster auf den Integrationstestbereich
  beschränkt. Der neue Prozessbaum-Nachweis bleibt daher in der vorhandenen
  `IntegrationTests`-Klasse.

## Bündelungsentscheidung

Dieser Step bleibt ein gebündelter `step_type: single`-Korrektur-Step mit
`estimated_risk: high`. Die beiden Findings sind ein gemeinsamer
Prozessbesitzvertrag: Ohne eine beim Start gesicherte Baum-Besitzgrenze kann
Cleanup bei Parent-exit nicht vollständig sein; ohne vorgezogene Timeout-
Vorbereitung kann ein gestarteter Prozess außerhalb genau dieses Cleanups
bleiben. Der Real-Harness muss beide Fälle am selben Executor nachweisen.

Das Split-Gate ist eingehalten:

- **Fachverträge:** genau zwei eng gekoppelte Verträge — (1) OS-gestützte
  Prozessbaum-/Pipe-Besitz- und Cleanup-Semantik und (2) Pre-Start-Timeout- /
  Cancellation-Semantik.
- **Schichten:** (1) interner Prozessbesitz-/Launcher-Helfer, (2) der
  bestehende Executor-Lifecycle, (3) der lokale Integrationstest-Harness.
- **Akzeptanzkriterien:** genau acht, siehe unten.
- **Kontextbudget:** zwölf `read_first`-Dateien, siehe unten.

Das ist kein unabhängiger DRY-, MagicValues- oder DeadCode-Sweep. Solche
Befunde werden nur dann in diesem Paket bereinigt, wenn sie durch die neue
Prozessbesitzgrenze unmittelbar entstehen oder in ihr architektonisch
sinnvoll zentralisiert werden müssen.

## Intention

Nach diesem Step besitzt jeder gestartete Git-Prozess eine belastbare,
dauerhafte Prozessbaumgrenze, die auch nach dem Ende des überwachten Parents
alle Nachfahren und geerbten Pipes kontrolliert. Jeder post-start Fehler
läuft durch bounded Cleanup; ein ungültiger Timeout wird vollständig vor dem
Start abgewiesen. Die bereits genehmigte Fehlerklassifikation und
repository-spezifische Fallback-Semantik bleiben unverändert.

## Akzeptanzkriterien

1. Der Executor etabliert die Prozessbaum-Besitzgrenze vor bzw. atomar mit
   dem Start des Zielprozesses. Dafür wird ein Windows-Job-Object mit
   `KILL_ON_JOB_CLOSE` und atomarer Prozesszuordnung oder eine technisch
   gleichwertige OS-Grenze verwendet; ein bloßes Attach-after-start oder
   `HasExited` als Erfolgsbeweis reicht nicht. Die Grenze erfordert keine
   Privilegienänderung und bewahrt die bestehende sichere
   `ProcessStartInfo`-/`ArgumentList`-/Umgebungssemantik.
2. Jeder Pfad nach erfolgreichem Prozessstart — einschließlich Reader-/Pipe-
   Fehler, Wait-Fehler, Timeout, Caller-Cancellation und sonstiger
   Nicht-Cancellation-Ausnahme — läuft genau einmal durch einen gemeinsamen,
   idempotenten Cleanup. Dieser signalisiert die Reader, beendet die
   Besitzgrenze unabhängig vom Parent-Status, schließt die Pipes und wartet
   auf Prozessbaum und Reader nur mit einer endlichen Grenze. Kein
   `Task.WhenAll`, Reader-Task oder Prozess bleibt unobserved oder unbounded.
3. Schlägt die Prozessbesitz-Initialisierung nach dem Start fehl, wird der
   Prozess fail-closed über die verfügbare Baumgrenze bereinigt; die primäre
   Ausnahme bleibt sichtbar und ein Cleanup-Fehler wird nur kontrolliert
   angehängt. Ein bereits beendeter Parent darf niemals als Beweis gelten,
   dass ein Grandchild bereinigt ist.
4. Timeout-CTS und Linked-CTS werden vor `Process.Start()` validiert und
   erzeugt. Ein positiver, für `CancellationTokenSource` nicht darstellbarer
   Timeout führt vor dem Start zu einer sichtbaren
   `ArgumentOutOfRangeException`; es existiert dann weder ein gestarteter
   Prozess noch ein offener Prozess-/Pipe-Besitz. Caller-Cancellation bleibt
   `OperationCanceledException` mit dem Caller-Token, Request-Timeout bleibt
   als `WasTimedOut` unterscheidbar.
5. Der direkte Integrationstest startet ein lokales Child, das ein
   Grandchild mit geerbten stdout-/stderr-Handles erzeugt, den Parent danach
   beendet und die PID-Information lokal markiert. Timeout und Cancellation
   müssen jeweils bounded zurückkehren und Parent sowie Grandchild innerhalb
   einer endlichen Wait-Grenze beenden; der Test assertiert den Grandchild-
   Zustand ausdrücklich und räumt in `finally` ebenfalls endlich auf.
6. Eine direkte Regression übergibt einen übergroßen positiven Timeout an
   den Executor und prüft anhand eines lokalen Startmarkers, dass
   `Process.Start()` nicht erreicht wurde. Der Test verwendet keine Git-
   Installation, kein Netzwerk, keinen Remote und keine Credentials; alle
   Warte- und Cleanup-Grenzen sind explizit endlich.
7. Die bestehenden Real-Executor-Nachweise für `UseShellExecute=false`,
   sichere `ArgumentList`-Übergabe, Redirects, deaktiviertes stdin,
   Arbeitsverzeichnis, bounded stdout/stderr, Trunkierungsmarker und
   `GIT_*`-Umgebungsisolation bleiben grün. Die bestehende Platzierung in
   `IntegrationTests` bleibt wegen des FastTests-Dependency-Gates erhalten.
8. Der Step ändert keine statusbewusste HTTP-/Git-Klassifikation, keine
   Credential-Sicherheit, keine gemeinsame URL-/Success-Factory, keine
   Ownership-/Reparse-/1314-Projektion und kein Provider-/Snapshot-/Host-
   Wiring. Nach der Implementierung sind `dotnet build` sowie beide
   vollständigen Nicht-Stress-Gates grün; Stress-Tests werden nicht
   ausgeführt.

## Konkrete Änderungen

### Prozessbesitz und Cleanup

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessTreeScope.cs` (neu)

- **Was:** Einen kleinen internen, `sealed` Prozessbesitz-Helper für Windows
  anlegen. Er kapselt den Job-Object-Handle bzw. die gleichwertige
  OS-Besitzgrenze, stellt die Prozesszuordnung beim Start sicher und bietet
  eine idempotente Beendigung sowie ein bounded Dispose.
- **Wie:** Die Zielprozess-Erzeugung muss aus dem bestehenden
  `ProcessStartInfo`-Vertrag abgeleitet werden. Untrusted Argumente dürfen
  nicht zu einer frei verketteten Shell-Kommandozeile werden. Benannte
  native Flags/Handles und `SafeHandle`-Lebensdauer verwenden; keine
  `SeDebugPrivilege`- oder andere Systemprivilegien-Anpassung.
- **Warum:** Nur eine dauerhafte Besitzgrenze kann ein Grandchild nach dem
  Ende des Parents zuverlässig terminieren. `Process.Kill` auf einem bereits
  beendeten Parent ist dafür nicht ausreichend.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs:22-233,398-411`

- **Was:** Timeout-CTS und Linked-CTS vor dem Prozessstart vorbereiten und
  den post-start Ablauf in einen gemeinsamen, ausnahmesicheren Cleanup-
  Rahmen überführen. Den Prozessbesitz in `ProcessExecutionState` halten,
  bei jedem Cleanup die Besitzgrenze auch bei `HasExited` auslösen und den
  Besitz-Helper erst nach bounded Prozess-/Reader-Teardown freigeben.
- **Was:** `TryKillProcessTree` darf `HasExited` nicht mehr als vollständigen
  Tree-Cleanup zurückgeben. Reader-/Pipe-Tasks müssen bei jedem Ausgang
  beobachtet werden; Cleanup darf weder die primäre Ausnahme noch das
  Caller-Token verschlucken. Bereits vorhandene Output-Limits,
  `ProcessTerminationTimeout` und Result-Optionen werden wiederverwendet,
  nicht dupliziert.
- **Warum:** Das schließt beide CRITICAL-Findings an der gemeinsamen
  Prozessbesitzgrenze, ohne den `IExternalSourceGitProcessExecutor`-Port
  oder den Transport-/Fehlervertrag zu erweitern.

### Direkte Regressionen

#### `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs:98-309`

- **Was:** Das vorhandene lokale Script so erweitern, dass es einen
  Parent-exit-Modus mit einem weiterlaufenden Grandchild und geerbten
  Output-Handles unterstützt. Timeout- und Cancellation-Tests prüfen den
  Fall, in dem der überwachte Parent bereits beendet ist, bevor Cleanup
  greift. PID-Abfragen, Waits und Test-`finally` bleiben bounded und
  resilient.
- **Was:** Einen Test für einen übergroßen positiven Timeout ergänzen, der
  vor einem möglichen Prozessstart einen lokalen Marker vorbereitet und
  danach explizit das Ausbleiben des Starts prüft. Bestehende Tests für
  StartInfo, Argumente, Umgebung und Output bleiben bestehen und werden
  nicht in `FastTests` verschoben.
- **Warum:** Damit werden genau die beiden Review-Szenarien real über den
  Executor geprüft, ohne Git, Remote, Netzwerk oder echte Credentials.

### Proaktiver Debt-Check im Scope

- Nach der Änderung `find_duplicates`, `find_magic_values` und
  `find_dead_code` ausschließlich auf den Executor-, neuen
  Prozessbesitz- und Integrationstestbereich begrenzen.
- Nur neue bzw. direkt durch diesen Lifecycle verursachte Duplikate,
  unbenannte native Prozesswerte oder unreferenzierte Helper in diesem
  Paket bereinigen. Keine unabhängige Audit-Runde und keine Erweiterung
  des Tech-Debt-Index.
- `TD-001` bis `TD-005` bleiben außerhalb der neuen Korrektur; `TD-005`
  bleibt gemäß Step-020-Review erledigt.

## Tests

- [ ] Direkter Parent-exit-/Grandchild-open-pipe-Test für Request-Timeout
  mit endlicher Rückkehr, endlichem Prozess-Wait und `WasTimedOut`.
- [ ] Derselbe lokale Prozessbaum-Fall für Caller-Cancellation mit
  erhaltenem Originaltoken und ohne weiterlaufenden Grandchild.
- [ ] Pre-Start-Regression für einen übergroßen positiven Timeout, die
  `ArgumentOutOfRangeException` und einen nicht gesetzten Startmarker prüft.
- [ ] Bestehender Real-Executor-Test für StartInfo, ArgumentList, Redirects,
  stdin, WorkingDirectory, bounded Output und `GIT_*`-Isolation bleibt grün.

Der Planer führt keine Tests aus. Der Coder führt nach der Implementierung
die projektableiteten Gates aus:

```powershell
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceGitProcessExecutorTests"
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Stress-Tests, Netzwerk-/Remote-Aktivität, externe Credentials und
Systemprivilegienänderungen bleiben ausgeschlossen.

## Definition of Done

- [ ] Die acht Akzeptanzkriterien sind umgesetzt und durch direkte lokale
  Regressionen prüfbar.
- [ ] `dotnet build` und beide vollständigen Nicht-Stress-Gates sind grün;
  kein Testprozess bleibt nach dem Lauf zurück.
- [ ] Der Coder schreibt `step-result.md` und setzt den Planstatus auf
  `done (pending audit)`; tatsächliche Planabweichungen werden dort
  dokumentiert.
- [ ] Der Code-Commit verwendet eine deutsche Conventional-Commit-Message
  im Imperativ mit dem Suffix `[decompiled-assembly-analysis]`.

## Kontextbudget

### `read_first` (maximal 12 Dateien)

1. `tasks/decompiled-assembly-analysis/step-020/step-review.md` — die zwei
   nicht behobenen CRITICAL-Findings und die konkrete Race-Reproduktion.
2. `tasks/decompiled-assembly-analysis/step-020/step-result.md` — der
   tatsächlich gelieferte bounded Executor und die aktuelle Harness-Grenze.
3. `tasks/decompiled-assembly-analysis/step-020/step-plan.md` — die bereits
   bestätigten Prozess-, Secret- und Scope-Invarianten.
4. `tasks/decompiled-assembly-analysis/follow-up-strategy.md` — Split-Gate,
   Kontextbudget und Handoff-Regel.
5. `tasks/decompiled-assembly-analysis/Konzept.md` — Timeout-, Cancellation-,
   Sicherheits- und netzwerkfreie Testleitplanken.
6. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs` —
   aktueller Start-, Reader-, Cleanup- und Tree-Kill-Lifecycle.
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessContracts.cs` —
   Request-/Result-Vertrag einschließlich Timeout- und Output-Semantik.
8. `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs`
   — bestehender Real-Harness, PID-Marker und finite Wait-Muster.
9. `src/AiNetLinter.FastTests/Architecture/FastTestsDependencyGuardTests.cs`
   — Grund für die Prozess-Harness-Grenze außerhalb von `FastTests`.
10. `.agents/rules/AiNetLinter.mdc` — Nullable-, Methodengrößen-, Catch-,
    Runtime-Lade- und Testabdeckungsregeln.
11. `.agents/rules/AiNetLinterRichtlinien.mdc` — Windows-, Cancellation-,
    Secret-, TestTemp- und Drift-Prävention.
12. `.agents/rules/AiNetLinter-McpWorkflow.mdc` — verpflichtende MCP-
    Priorität und absoluter `projectRoot` für C#-Semantik.

### `read_on_demand`

- `src/AiNetLinter.IntegrationTests/AiNetLinter.IntegrationTests.csproj`
  und die konkrete `TestWaiter`-Implementierung nur für Compile-/Wait-
  Muster und Testkategorie prüfen.
- `src/AiNetLinter.IntegrationTests/Platform/CliProcessRunner.cs` nur als
  lokales Muster für finite Prozessbeendigung lesen; keine Übernahme seines
  CLI-Scope in den Git-Executor.
- `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs`,
  `ExternalSourceRepositoryFailurePolicy.cs` und
  `ExternalSourceRepositoryAcquirer.cs` nur bei einer konkreten
  Invariantenprüfung; sie werden nicht geändert.
- `Directory.Build.props` und vorhandene Windows-Interop-Patterns nur,
  falls der neue Prozessbesitz-Helper daraus notwendige Build-/Interop-
  Vorgaben ableiten muss.

### `out_of_scope`

- Provider, Snapshot-Registry, Workspace-/Host-Komposition, MCP-Tools,
  Refresh, Fetch, Cache, Manifest, Generation und Source-of-Truth.
- HTTP-/Git-Fehlerklassifikation, Mapping-JSON, Credential-Resolver,
  URL-Policy, Success-Factory sowie der 1314-/Reparse-Fallback.
- `FastTests`-Prozess-Harness, Git-/Gitea-Remotes, Netzwerk, echte
  Credentials, externe Projekt-Restores, Stress-Tests und Privilegien-
  änderungen.
- `task-state.md`, `roadmap.md`, `codemap.md`, `tech-debt.md` sowie
  `TD-001` bis `TD-005`.
- Assembly-Loading, Reflection und jede zusätzliche Source-/Target-
  Abstraktion außerhalb der bestehenden Transportgrenze.

## Invarianten

- `ExternalSourceGitProcessExecutor` bleibt der einzige reale Prozess-
  Executor; `IExternalSourceGitProcessExecutor` und der Gitea-Transport-
  Port erhalten ihre fachliche Rolle.
- stdout/stderr bleiben bounded und cancellation-aware; kein Read, Wait,
  Drain oder Test-Wait wird unbounded. Cleanup beobachtet jeden erzeugten
  Task und lässt Parent, Grandchild und geerbte Pipes nicht zurück.
- Caller-Cancellation, Request-Timeout und primäre Nicht-Cancellation-
  Ausnahme bleiben unterscheidbar; Cleanupfehler ersetzen die primäre
  Ursache nicht.
- Keine Secrets in `ProcessStartInfo`, Argumenten, Umgebungsdiagnosen,
  Logs, Result-Texten oder Test-Markern. `GIT_*`-Isolation und explizite
  marker-only Variablen bleiben erhalten.
- Die bestätigte HTTP-/Git-Statusklassifikation, URL-/Success-DRY aus
  `TD-005`, Acquirer-Ownership und repository-spezifische
  `ERROR_PRIVILEGE_NOT_HELD (1314)`-/Reparse-Fallback-Projektion ändern
  sich nicht.
- Es gibt kein Provider-/Snapshot-/Refresh-/Cache-/Host-Wiring, keinen
  öffentlichen Mapping- oder MCP-Wire-Change und kein Runtime-Laden
  fremder Assemblys.

## Risiken und Gegenmaßnahmen

- **Interop-/Handle-Leak:** Job-Object- und Prozesshandles ausschließlich
  in einem kleinen `SafeHandle`-basierten Helper besitzen; Terminierung,
  Pipe-Schließung und Dispose mit einem gemeinsamen endlichen Budget
  beobachten. Fehler sichtbar anhängen, nicht still schlucken.
- **Start-Race:** Eine reine nachträgliche Zuordnung nach `Process.Start()`
  ist nicht ausreichend. Die Prozessbesitzgrenze muss atomar beim Start
  wirksam werden oder den Start bei fehlender Möglichkeit fail-closed
  verhindern; der Parent-exit-Harness ist der konkrete Nachweis.
- **StartInfo-/Argument-Drift:** `CreateStartInfo` bleibt die semantische
  Quelle für Arbeitsverzeichnis, Redirects, stdin, Umgebung und Argumente.
  Der Real-Harness prüft Sonderzeichen und Leerzeichen weiter direkt.
- **Timeout-Preflight-Änderung:** Der übergroße Timeout muss als lokaler,
  vor dem Start sichtbarer Argumentfehler enden. Ein Marker-Test und die
  bestehenden Timeout-/Cancellation-Tests sichern ab, dass keine
  stillschweigende Prozessstart- oder Resultatänderung entsteht.
- **Hängender Testprozess:** Grandchild-PIDs, `TestWaiter`-Grenzen und
  `finally`-Cleanup explizit beibehalten; bei einem Fehlschlag zusätzlich
  nur die lokal aufgezeichneten Test-PIDs terminieren. Keine Sleeps ohne
  endliche äußere Grenze und keine globale Testserialisierung.
- **Scope-Drift:** Neue Helper, native Konstanten und Testfixture-Logik
  nur für diese Besitzgrenze einführen; keine unabhängigen Audits oder
  Änderungen an `TD-005` und den bereits genehmigten Transportverträgen.

## Coder-Handoff

### Sicherer Einstieg

1. Zuerst diesen Handoff und die zwölf `read_first`-Dateien lesen. Danach
   `get_feature_context`, `get_file_skeleton` und `get_symbol_body` mit
   `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` für
   `ExternalSourceGitProcessExecutor` und seine Verträge verwenden;
   `find_references`/`get_test_context` nur für konkrete Auswirkungen.
   `rg` bleibt auf die genannten Prozess-, Test- und Statusmuster begrenzt.
2. Zuerst die dauerhafte Prozessbaumgrenze samt bounded Dispose umsetzen.
   Die Grenze muss bei Prozessstart aktiv sein und den Prozessbaum auch
   dann terminieren, wenn der überwachte Parent bereits beendet ist.
   Bestehende `ProcessStartInfo`- und Secret-Invarianten unverändert
   weiterführen; keinen Systemprivilegienpfad ergänzen.
3. Danach Timeout-CTS und Linked-CTS vor `Process.Start()` vorbereiten.
   Alle Exceptions nach erfolgreichem Start müssen durch denselben
   Cleanup-Rahmen laufen; Reader-/Process-Tasks beobachten und die
   primäre Fehler- bzw. Cancellation-Semantik erhalten.
4. Anschließend ausschließlich den bestehenden Integration-Harness um
   Parent-exit, Timeout, Cancellation und den ungültigen Timeout ergänzen.
   Parent-/Grandchild-PIDs mit endlichen `TestWaiter`-Grenzen prüfen und
   lokale Prozesse in jedem Testpfad resilient beseitigen.
5. Zum Abschluss scoped MCP-`get_violations`-/Impact-Prüfungen und die
   begrenzten DRY-/MagicValues-/DeadCode-Abfragen für die berührten
   Dateien ausführen. Nur unmittelbar verursachte Befunde beheben;
   `tech-debt.md` nicht ändern. Danach die im Testabschnitt genannten
   Build-/Nicht-Stress-Gates ausführen.

### Übergabeinvarianten

- Der gestartete Prozess und alle von ihm erzeugten Nachfahren stehen unter
  derselben dauerhaften Besitzgrenze; Parent-exit beendet nicht die
  Cleanup-Zuständigkeit.
- Jeder post-start-Ausgang ist bounded, idempotent und beobachtet; keine
  offene Pipe, kein unobserved Reader-Task und kein hängender Prozess wird
  an den Aufrufer zurückgelassen.
- Timeout-/Cancellation-Vorbereitung passiert vor dem Prozessstart; ein
  unrepresentable Timeout startet keinen Prozess.
- `CreateStartInfo`, sichere Argumentübergabe, Redirects, stdin,
  Arbeitsverzeichnis, `GIT_*`-Isolation und Secret-Redaktion bleiben
  unverändert wirksam.
- HTTP-/Git-Statusklassifikation, TD-005, Acquirer-Ownership und
  1314-/Reparse-Fallback sind unveränderte Verträge.

### Nächster sicherer Einstiegspunkt

Beginne in
`ExternalSourceGitProcessExecutor.ExecuteAsync` und dem aktuellen
`TryKillProcessTree`-/`CleanupProcessAsync`-Zusammenspiel. Entwirf zuerst
den neuen Prozessbesitz-Helper mit atomarer Startgrenze; verschiebe danach
die CTS-Erzeugung vor `Process.Start()` und erweitere zuletzt den
vorhandenen Integration-Harness um den Parent-exit-Fall. Öffne
`GiteaGitRepositoryTransport` und die Failure-Policy nur, wenn eine
konkrete unveränderte Invariante nachgeschlagen werden muss.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — C#-Symbol-, Referenz- und
  Impact-Fragen zuerst mit absolutem `projectRoot` über AiNetLinter-MCP
  prüfen; `rg` nur für gezielte Textarbeit ergänzend verwenden.
- `.agents/rules/AiNetLinter.mdc` — Nullable, sealed Helper, kurze Methoden,
  begrenzte Komplexität, kein stiller Catch, keine Runtime-Assembly-Ladung
  und deterministische Testabdeckung.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — statische Architektur,
  Windows-/Cancellation-/Secret-Sicherheit, TestTempDirectory, bounded
  Testprozesse und proaktiver DRY-/MagicValues-/DeadCode-Abbau.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md §6.2.1` — flacher
  Korrektur-Step mit `corrects: step-020` und unveränderter Roadmap.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md §10.5-§10.7` —
  Korrekturkettenbudget, Split-/Batch-Grenzen und dichte Artefakte.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md` —
  Fix-Modus, JIT-Ist-Zustand, Pointer-Referenzen und kein Commit durch den
  Planer.

## Bekannte Ausnahmen

- Der bestehende echte Reparse-Test darf weiterhin ausschließlich bei
  `ERROR_PRIVILEGE_NOT_HELD (1314)` überspringen. Step 021 ändert weder
  diesen Test noch das Capability-Gate.
- Der Real-Executor-Harness bleibt in `IntegrationTests`, weil das
  bestehende FastTests-Dependency-Gate `System.Diagnostics.Process` dort
  nicht zulässt. Das ist eine Projektgrenze, kein fachlicher Testverzicht.
- Die Planer-Sitzung führt keine Tests aus. Der Coder behandelt fehlende
  Infrastruktur oder ein nicht erreichbares Tool gemäß Workflow als
  `blocked`; der neue lokale Test darf keine externe Infrastruktur
  voraussetzen.

## Notes

Step 021 schließt ausschließlich die zwei offenen Step-020-Findings an der
Prozessbesitzgrenze. Nach Genehmigung bleibt als nächster EPIC-04-Schnitt der
erfolgreiche Acquirer→Snapshot-/Workspace-Anschluss offen; der neue
Job-/Prozessbesitz-Helper darf diese späteren Lebenszyklusentscheidungen
nicht vorwegnehmen.
