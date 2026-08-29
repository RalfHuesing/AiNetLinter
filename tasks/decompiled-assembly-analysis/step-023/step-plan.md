---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 023
corrects: step-022
title: "Prozessbaum-Fallback und Handle-Cleanup vollständig fail-closed schließen"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T12:05:00+02:00
related_to:
  - ../step-022/step-review.md
  - ../step-022/step-result.md
  - ../step-022/step-plan.md
  - ../follow-up-strategy.md
  - ../Konzept.md
---

# Step 023: Prozessbaum-Fallback und Handle-Cleanup vollständig fail-closed
# schließen

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und
  Fehlersemantik.
- **Korrektur von:** `step-022`, Review
  `step-022/step-review.md`, drei zusammengehörige Befunde an der
  Prozess-/Handle-Besitzgrenze.
- **Konzept-Referenz:** `Konzept.md`, Fehler-, Sicherheits- und
  Vertrauensvertrag: bounded Prozess-/Pipe-Cleanup, sichtbare Fehler und
  keine Ausführung fremder Artefakte.

## Scope und Out-of-scope

### In scope

- Den Startfehler-Fallback nach dem tatsächlichen Prozess-Lifecycle
  unterscheiden: Ein beendeter Parent ist nur dann ausreichend, wenn der
  Prozess nachweislich nie einen Nachfahren ausführen konnte. Für einen
  bereits zugeordneten oder resumierten Prozess muss die noch besessene
  Job-Grenze den gesamten Baum beenden und ihr Status muss nachweisbar sein.
- `TryManagedFallback` so ändern, dass `Process.HasExited` allein nie als
  Prozessbaum-Nachweis gilt. Alle Fallback- und finalen Prüfungen bleiben
  bounded, verwenden nur die bekannte Prozessidentität und melden einen
  nicht nachgewiesenen Baum als Fehler.
- Die `CloseHandle`-Auswertung des
  `ExternalSourceGitProcessNativeJob` in den vorhandenen Cleanup-Vertrag
  führen. Der `SafeHandle` bleibt Besitzer; ein expliziter, idempotenter
  Statuspfad sammelt `false` samt Win32-Fehler sowohl bei
  `StartupResources` als auch bei `TreeScope`.
- Primär-Exceptions bei Start-, Timeout-, Cancellation- und normalen
  Cleanup-Pfaden erhalten. Zusätzliche Termination-, Baum- und Handle-
  Close-Fehler werden kontrolliert angehängt oder aggregiert, ohne die
  primäre Ursache oder das Cancellation-Token zu ersetzen.
- Die drei `IsUsableHandle`-Kopien und die zwei `CombineFailures`-Kopien
  in diesem Prozesspaket in jeweils einen kleinen gemeinsamen internen
  Helper überführen. Bestehende Diagnose-Texte und native ABI-Semantik
  bleiben erhalten.
- Den vorhandenen lokalen Integration-Harness um direkte Regressionen für
  Parent-exit/Grandchild, fehlgeschlagenes Handle-Close sowie erfolgreiche
  und fehlgeschlagene Fallback-Ausgänge ergänzen.

### Out of scope

- Änderungen an HTTP-/Git-Fehlerklassifikation, Credentials, Redaction,
  URL-Policy, Success-Factory oder dem bereits erledigten `TD-005`.
- Änderungen am `ERROR_PRIVILEGE_NOT_HELD (1314)`-/Reparse-Fallback;
  der transparente, capability-bedingte Skip bleibt unverändert.
- Provider, Snapshot, Refresh, Fetch, Cache, Manifest,
  Source-of-Truth, Host-Wiring, MCP-Registrierung und Mapping-Verträge.
- Assembly.Load, AssemblyLoadContext, Reflection, Runtime-Ausführung
  fremder Assemblies, externe Restores, Git-/Gitea-Netzwerkzugriff,
  echte Credentials oder Systemprivilegienänderungen.
- Eine allgemeine Prozess- oder Handle-Abstraktion, ein unabhängiger
  DRY-/MagicValues-/DeadCode-Sweep oder Änderungen an fachfremden
  Produktions-/Testbereichen.
- Entfernen oder Umbenennen der nativen ABI-Strukturfelder, die der
  Dead-Code-Audit nur mit niedriger Confidence meldet.
- Änderungen an `task-state.md`, `roadmap.md`, `codemap.md` oder
  `tech-debt.md`. Dieser Korrektur-Step ändert keine Roadmap und erzeugt
  keinen neuen Tech-Debt-Eintrag.

## Aktueller Projektzustand (JIT-Kontext)

Die semantischen AiNetLinter-MCP-Abfragen wurden mit dem absoluten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt; gezielte
Textarbeit erfolgte anschließend mit `rg`.

- `ExternalSourceGitProcessStartFailureCleanup.TryManagedFallback` liegt
  in `ExternalSourceGitProcessStartFailureCleanup.cs:255-316`. Nach
  `Process.GetProcessById` wird bei `HasExited == true` kein Tree-Kill
  versucht; nach `WaitForExit` und erneutem Parent-`HasExited` wird `true`
  zurückgegeben. Ein Grandchild mit geerbten Output-Handles kann dadurch
  weiterlaufen, obwohl der Fallback Erfolg meldet.
- `ExternalSourceGitProcessNativeJob.ReleaseHandle` liegt in
  `ExternalSourceGitProcessLauncher.cs:390-391` und gibt das
  `CloseHandle`-Ergebnis ausschließlich als `bool` an `SafeHandle` zurück.
  `ExternalSourceGitProcessStartupResources.Dispose` und
  `ExternalSourceGitProcessTreeScope.Dispose` können diesen `false`-Status
  derzeit nicht in ihre jeweiligen Fehler-Sammler übernehmen.
- Die Prozessbesitzkette bleibt `CREATE_SUSPENDED` →
  `AssignProcessToJobObject` → `ResumeThread`; der Job nutzt
  `KILL_ON_JOB_CLOSE`. Der Fallback braucht deshalb einen expliziten
  Lifecycle-/Ownership-Kontext, damit der unzugeordnete, suspendierte
  Prozess vom resumierten Prozessbaum unterschieden wird.
- Der gezielte Produktions-DRY-Scan bestätigt einen exakten Cluster aus
  drei `IsUsableHandle`-Methoden in `StartupResources`,
  `StartFailureCleanup` und `LauncherNativeHelpers`. Der strukturelle
  Scan bestätigt zwei identische `CombineFailures`-Methoden in
  `ExternalSourceGitProcessExecutor` und `StartFailureCleanup`.
- Der vorhandene Integration-Harness hat sechs fokussierte Tests. Der
  Post-Create-Test erzwingt bisher einen Assign-/Terminate-/Wait-Fehler,
  startet wegen `CREATE_SUSPENDED` aber keinen Grandchild-Prozess. Die
  bestehenden Timeout-/Cancellation-Skripte schreiben bereits Parent- und
  Grandchild-PIDs, und ihr `finally` wartet bounded.
- `get_violations` meldet im Prozesspaket aktuell keine Verstöße. Die
  Low-Confidence-Dead-Code-Kandidaten betreffen native ABI-Felder und
  sind keine Entfernungsaufträge.

## Bündelungsentscheidung

Dieser Step bleibt ein einziger, größerer `step_type: single`-
Korrektur-Step mit `estimated_risk: high`. Die drei Review-Findings
gehören zu einem Vertrag: Der Fallback darf keinen Prozessbaum als beendet
melden, solange seine Besitzgrenze oder der Grandchild-Nachweis fehlt;
gleichzeitig müssen Fehler beim Freigeben genau dieser Grenze sichtbar in
den bestehenden Cleanup-/Primärfehlerpfad gelangen. Die eng gekoppelten
Helper-Duplikate werden dabei als mechanisch begrenzte Umsetzung dieses
Vertrags zentralisiert, nicht als eigener Sweep.

Das Split-Gate ist eingehalten:

- **Fachverträge:** genau zwei eng gekoppelte Verträge — (1) fail-closed
  Prozessbaum-Fallback und Lifecycle-/Ownership-Nachweis sowie (2)
  statusbewusste Handle-/Cleanup-Fehlerweitergabe mit Erhalt der
  Primär-Exception.
- **Schichten:** genau drei — (1) native Job-/SafeHandle-Grenze und der
  interne Cleanup-Helper, (2) der bestehende Launcher-/Executor-Lifecycle,
  (3) der lokale Integrationstest-Harness.
- **Akzeptanzkriterien:** genau acht, siehe unten.
- **Kontextbudget:** genau zwölf `read_first`-Dateien, siehe unten.

Es ist kein Mini-Sweep und kein vorausgeplanter Folge-Step. DRY-,
MagicValues- und DeadCode-Prüfungen werden nur auf die unmittelbar
berührte Prozess-/Handle-Grenze angewendet; neue benannte Werte dürfen
keine bestehende ABI- oder Fehlersemantik verdecken.

## Intention

Der Korrektur-Step soll jeden Post-Create- und Cleanup-Ausgang gegen den
tatsächlichen Prozessbaum absichern: Parent-Ende allein zählt bei einem
resumierten Prozess nicht, und ein fehlgeschlagenes Job-Handle-Close wird
als Cleanup-Fehler sichtbar. Die bestehenden Timeout-, Cancellation-,
Credential-, Klassifikations- und Fallback-Verträge bleiben dabei
unverändert; direkte lokale Tests beweisen die Erfolgs- und Fehlerpfade.

## Akzeptanzkriterien

1. `TryManagedFallback` akzeptiert `HasExited` des Parents nicht mehr als
   alleinigen Baum-Nachweis. Der Cleanup-Kontext unterscheidet mindestens
   „nicht dem Job zugeordnet und noch suspendiert“ von „zugeordnet oder
   resumiert“; nur im ersten Zustand darf der bekannte Prozess selbst
   bounded als beendet gelten, weil dort kein Grandchild ausgeführt werden
   konnte. Für den zweiten Zustand wird die noch besessene Job-Grenze
   beendet und ihr erfolgreicher Status sowie der anschließende Endzustand
   werden bounded geprüft.
2. Ein lokaler Parent-exit-/Grandchild-Test lässt den Parent nach dem
   Start des Grandchild-Prozesses enden, hält geerbte stdout-/stderr-Handles
   offen und erzwingt einen nativen Wait-/Post-Create-Fehler. Der Fallback
   darf den Parent-Exit nicht als Erfolg missverstehen: Der Grandchild wird
   über die Prozessbaum-Grenze beendet oder der Cleanup liefert einen
   sichtbaren Fehler; Parent und Grandchild sind vor der Rückkehr innerhalb
   einer endlichen Grenze nicht mehr aktiv.
3. Für den unzugeordneten, suspendierten Prozess bleibt ein erfolgreicher
   bounded PID-Fallback möglich. Ein direkter Fallback-Erfolgstest weist
   nach, dass der bekannte Prozess beendet ist und kein unnötiger
   Prozesslisten-Scan, Namensheuristik oder Zugriff auf fremde PIDs erfolgt;
   ein korrekter Fallback-Fehler bei fehlendem Endnachweis bleibt sichtbar
   und wird nicht in einen False Success umgewandelt.
4. `ExternalSourceGitProcessNativeJob` bleibt ein `SafeHandle`-Besitzer,
   erhält aber einen genau-einmaligen, idempotenten statusbewussten
   Close-Pfad. `CloseHandle == false` wird zusammen mit dem unmittelbaren
   Win32-Fehler in den vorhandenen Failure-Sammler aufgenommen; sowohl
   `ExternalSourceGitProcessStartupResources` als auch
   `ExternalSourceGitProcessTreeScope` verwenden diesen Pfad. Der
   Finalizer-/SafeHandle-Sicherheitsmechanismus wird nicht durch eine
   ungefangene Exception aus `ReleaseHandle` ersetzt.
5. Ein direkter lokaler Close-Fehler-Test erzwingt über die bestehende
   per-Aufruf-Native-Operations-Seam ein fehlgeschlagenes Job-
   `CloseHandle`, führt den Startfehler- und den normalen
   TreeScope-Cleanup-Pfad aus und prüft, dass der Close-Fehler mit Diagnose
   sichtbar ist. Bei einer vorhandenen Primär-Exception bleiben deren Typ,
   Ursache und Stack beobachtbar; der Close-Fehler ersetzt sie nicht.
6. Die drei `IsUsableHandle`-Implementierungen werden durch einen einzigen
   kleinen internen Helper ersetzt; beide `CombineFailures`-Implementierungen
   verwenden ebenfalls eine zentrale Variante, die die unterschiedlichen
   bestehenden Diagnose-Texte als Daten erhält. Der scoped `find_duplicates`
   -Audit findet danach keine dieser beiden Duplikatgruppen. Native
   ABI-Strukturen und ihre Felder bleiben unverändert.
7. Die vorhandenen lokalen Regressionen für Request-Timeout,
   Caller-Cancellation, bounded stdout/stderr, `ArgumentList`, Redirects,
   Working Directory, deaktiviertes stdin und `GIT_*`-Isolation bleiben
   aussagekräftig und grün. Jeder Reader-, Prozess-, Job- und Test-Wait ist
   endlich; kein bekannter Testprozess oder unobserved Task bleibt zurück.
8. Der Step verändert keine HTTP-/Git-Klassifikation, Credentials,
   `TD-005`, den 1314-/Reparse-Fallback, Provider-/Snapshot-/Refresh- /
   Cache-/Source-of-Truth-/Host-Wiring oder Assembly-Load-/Reflection-
   Grenze. Der Coder weist `dotnet build` und beide vollständigen
   Nicht-Stress-Gates grün nach; der bekannte Reparse-Skip ist nur bei
   `ERROR_PRIVILEGE_NOT_HELD (1314)` zulässig.

## Konkrete Änderungen

### Prozessbaum-Fallback und Fehlerweitergabe

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartFailureCleanup.cs`

- **Was:** Den Cleanup-Eingang um den vorhandenen Start-Lifecycle und die
  noch besessene Job-Grenze ergänzen, ohne einen öffentlichen Vertrag zu
  ändern. `TryManagedFallback` in einen expliziten bounded Erfolg-/Fehler-
  pfad überführen: Parent-only-Ende nur für den nachweislich suspendierten,
  nicht zugeordneten Prozess; für zugeordnete/resumierte Prozesse
  `TerminateJobObject` und den statusbewussten Job-Close verwenden. Ein
  bereits beendeter Parent löst dort keinen Erfolg mehr allein aus.
- **Was:** Fehler aus native Termination, Job-Ende, PID-Fallback und
  Handle-Close vollständig in die vorhandene Sammlung einordnen. Die
  primäre Start-Exception wird vor Cleanup gesichert und über den
  bestehenden `ExceptionDispatchInfo`-/Attachment-Vertrag erneut
  ausgelöst; wiederholte Cleanup-Fehler dürfen weder verloren gehen noch
  die primäre Ursache ersetzen.
- **Warum:** Der Review-Befund entsteht an der Grenze zwischen einem
  beendeten Parent und einem weiterlebenden Grandchild; der Fallback muss
  Besitz-/Lifecycle-Wissen statt Parent-Status allein verwenden.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs`

- **Was:** Lifecycle-Status von erfolgreicher Job-Zuordnung und Resume bis
  in den Startfehler-Cleanup führen. Den `ExternalSourceGitProcessNativeJob`
  mit dem per-Aufruf-Close-Statuspfad erzeugen und
  `ReleaseHandle`/explizites Dispose so koppeln, dass der SafeHandle Besitzer
  bleibt, aber jeder explizite `CloseHandle`-Fehler gesammelt wird.
- **Was:** `TryTerminate` und die Start-/Ressourcenübergabe so belassen,
  dass die bestehende `CREATE_SUSPENDED` → Assign → Resume-Reihenfolge und
  `KILL_ON_JOB_CLOSE` unverändert bleiben. Die Job-Grenze wird im
  Startfehlerfall bis zum bounded Baum-/Handle-Nachweis besessen.
- **Warum:** Fallback und Close-Fehler brauchen dieselbe Ownership-Quelle;
  eine reine PID-Prüfung oder der stumme SafeHandle-Finalizer reicht nicht.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartupResources.cs`

- **Was:** Die vor der Ownership-Übertragung liegenden Ressourcen über den
  statusbewussten Job-Close-Pfad freigeben und dessen Fehler in den bereits
  vorhandenen `failures`-Vertrag aufnehmen. Der Pfad bleibt idempotent und
  räumt Pipes sowie Input-Handle auch bei einem Job-Close-Fehler weiter auf.
- **Warum:** Der Startfehlerpfad darf ein fehlgeschlagenes
  `CloseHandle` nicht als erfolgreiches Freigeben ausgeben und darf die
  ursprüngliche Assign-/Resume-Exception nicht verdecken.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessTreeScope.cs`

- **Was:** Den normalen TreeScope-Cleanup auf denselben expliziten,
  einmaligen Job-Close-/Failure-Sammler umstellen. `TerminateJobObject`,
  Output-Stream-Schließung, Process-Dispose und Job-Close bleiben in einer
  bounded, idempotenten Reihenfolge; ein Close-Fehler wird an den Executor
  zurückgegeben.
- **Warum:** Timeout-/Cancellation-Cleanup und Startfehler-Cleanup müssen
  denselben sichtbaren Handle-Vertrag erfüllen.

### Zentrale Prozess-Helper

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessCleanupHelpers.cs` (neu)

- **Was:** Einen kleinen internen statischen Helper passend
  zur bestehenden Prozess-/Cleanup-Schicht anlegen. Er enthält genau eine
  `IsUsableHandle`-Implementierung sowie eine `CombineFailures`-Variante,
  die die bestehende Fehlermeldung als Parameter übernimmt und damit
  Start- und Laufzeitdiagnosen unverändert hält.
- **Warum:** Die zentrale Ablage beseitigt die drei bzw. zwei direkt
  gekoppelten Kopien ohne eine allgemeine Utility-Schicht zu erzeugen.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs`

- **Was:** Die lokale `CombineFailures`-Kopie entfernen und den zentralen
  Helper verwenden. Nur falls die statusbewusste Job-Close-Weitergabe es
  erfordert, den bestehenden Executor-Cleanup so anpassen, dass dessen
  Failure-Ergebnis weiterhin in Timeout-, Cancellation- und primäre
  Exception-Semantik eingeordnet wird.
- **Warum:** Der zentrale Failure-Builder muss in beiden gekoppelten
  Cleanup-Pfaden dieselbe Aggregationssemantik liefern.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncherNativeHelpers.cs`

- **Was:** Die lokale `IsUsableHandle`-Kopie entfernen und den zentralen
  Helper verwenden. Argument-/Environment-/Handle- und Close-Semantik
  bleiben unverändert; keine Shell-Kommandozeile und kein ABI-Umbau.
- **Warum:** Die einzige Handle-Gültigkeitsprüfung soll nicht zwischen
  Start-, Fallback- und Input-Handle-Pfad auseinanderdriften.

### Direkte lokale Regressionen

#### `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs`

- **Was:** Den bestehenden Prozessbaum-Harness so erweitern, dass ein
  Parent nach dem Start des Grandchild-Prozesses endet, beide Output-Handles
  geerbt bleiben und der Post-Create-/Wait-Fehlerpfad den noch besessenen
  Baum behandelt. Parent- und Grandchild-PIDs werden mit vorhandenen
  bounded Waits gelesen und nach der Rückkehr ausdrücklich als beendet
  geprüft.
- **Was:** Den vorhandenen Suspend-/Assign-Fehlertest als
  Fallback-Erfolgspfad erhalten und einen deterministischen Fallback-
  Fehlerpfad ergänzen, der fehlenden Baum-/Endnachweis sichtbar macht.
  Für beide Pfade werden nur die bekannte Prozess-ID, der lokale Marker
  und die per-Aufruf-Native-Seam verwendet.
- **Was:** Einen Close-Fehler über die per-Aufruf-Native-Operations-Seam
  erzwingen. Der Test prüft im Startfehler- oder TreeScope-Pfad die
  CloseHandle-Diagnose und stellt bei vorhandener Primär-Exception deren
  Typ/Ursache sowie den zusätzlichen Cleanup-Fehler fest. Bestehende
  Timeout-/Cancellation-`finally`-Pfade bleiben bounded und resilient.
- **Warum:** Die Tests müssen genau die drei Review-Lücken beweisen, ohne
  Git, Netzwerk, Credentials, Privilegien oder fremde Testprojekte zu
  benötigen.

### Begrenzter Qualitätscheck

- Nach dem Edit `get_violations`, `find_references`/`get_impact` sowie
  `find_duplicates`, `find_magic_values` und `find_dead_code` ausschließlich
  auf die Prozess-/Handle-Dateien und den Integration-Harness anwenden.
- Nur die beiden benannten Duplikatgruppen und unmittelbar verursachte
  neue Befunde bearbeiten. Die nativen ABI-Low-Confidence-Felder bleiben
  erhalten; `TD-001` bis `TD-005` und fachfremde solutionweite Kandidaten
  bleiben unberührt.

## Tests

- [ ] Direkter Parent-exit-/Grandchild-Test mit erzwungenem Wait- oder
  Post-Create-Fehler: Parent-only-`HasExited` führt nicht zu False Success;
  beide lokalen PIDs sind bounded beendet.
- [ ] Fallback-Erfolg und Fallback-Fehler für den suspendierten bzw.
  resumierten Lifecycle: Erfolg ist nur mit zulässigem Endnachweis möglich,
  fehlender Baum-Nachweis bleibt als sichtbarer Fehler erhalten.
- [ ] Direkter erzwungener Job-`CloseHandle`-Fehler in den vorhandenen
  Ownership-Pfaden: Diagnose ist sichtbar und eine Primär-Exception bleibt
  unverändert beobachtbar.
- [ ] Bestehende fokussierte Real-Executor-Tests für StartInfo, Argumente,
  Redirects, stdin, Working Directory, bounded Output, Timeout,
  Cancellation und `GIT_*`-Isolation bleiben grün.

Der Planer führt keine Tests aus. Der Coder führt nach der Implementierung
mindestens diese Commands aus:

```powershell
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceGitProcessExecutorTests"
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Der bekannte echte Reparse-Test darf ausschließlich wegen
`ERROR_PRIVILEGE_NOT_HELD (1314)` übersprungen werden. Stress-Tests,
Netzwerk, Remote-Repositories, Gitea, echte Credentials und
Systemprivilegienänderungen bleiben ausgeschlossen.

## Definition of Done

- [ ] Alle acht Akzeptanzkriterien sind umgesetzt und durch direkte lokale
  Regressionen prüfbar.
- [ ] `TryManagedFallback` akzeptiert keinen Parent-only-Endzustand mehr
  als Baum-Nachweis für zugeordnete/resumierte Prozesse; Fallback-Erfolg und
  Fallback-Fehler sind sichtbar unterschieden.
- [ ] Jeder explizite Job-`CloseHandle`-Fehler gelangt in den bestehenden
  Cleanup-/Primärfehlervertrag; Primär-Exceptions und Cancellation-Tokens
  bleiben erhalten.
- [ ] `IsUsableHandle` und `CombineFailures` sind innerhalb des Pakets
  jeweils zentral implementiert; ABI-Felder bleiben erhalten.
- [ ] Build und beide vollständigen Nicht-Stress-Gates sind grün; kein
  lokaler Testprozess bleibt zurück.
- [ ] Der Coder schreibt `step-result.md` und setzt den Planstatus auf
  `done (pending audit)`; Abweichungen werden dort dokumentiert.
- [ ] Der Code-Commit verwendet eine deutsche Conventional-Commit-Message
  im Imperativ mit dem Suffix `[decompiled-assembly-analysis]`.

## Kontextbudget

### `read_first` (maximal 12 Dateien)

1. `tasks/decompiled-assembly-analysis/step-022/step-review.md` — die
   drei offenen Findings und ihre konkreten Fundstellen.
2. `tasks/decompiled-assembly-analysis/step-022/step-result.md` — der
   tatsächlich implementierte Native-/Fallback-/Test-Seam-Stand.
3. `tasks/decompiled-assembly-analysis/step-022/step-plan.md` — die
   bestätigten Prozess-, Secret- und Scope-Invarianten.
4. `tasks/decompiled-assembly-analysis/follow-up-strategy.md` —
   Split-Gate, Kontextbudget und neuer-Agent-Handoff.
5. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartFailureCleanup.cs`
   — Fallback, native Statusauswertung, Fehleraggregation und Handle-
   Schließung.
6. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs`
   — Create/Assign/Resume-Sequenz, SafeHandle und Startfehlerbesitz.
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartupResources.cs`
   — vor der Ownership-Übertragung freizugebende Handles und Pipes.
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessTreeScope.cs`
   — normaler Job-/Pipe-/Process-Cleanup.
9. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs`
   — Timeout-/Cancellation-/Primärfehlervertrag und Failure-Aggregation.
10. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncherNativeHelpers.cs`
    — bestehende Handle-Gültigkeits- und Argument-/Environment-Helfer.
11. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessNativeMethods.cs`
    — unveränderliche Win32-P/Invoke-Signaturen und ABI-Strukturen.
12. `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs`
    — lokaler Child-/Grandchild-Harness, Native-Seam, Marker und bounded
    Test-`finally`.

### `read_on_demand`

- `tasks/decompiled-assembly-analysis/Konzept.md` — nur Fehler-,
  Sicherheits-, Test- und Non-Goal-Abschnitte für einen konkreten
  Vertragsabgleich.
- `.agents/rules/AiNetLinter.mdc` und
  `.agents/rules/AiNetLinterRichtlinien.mdc` — für die in `Rules-Refs`
  genannten Nullable-, Größen-, Catch-, TestTemp-, Secret-, Cancellation-
  und DRY-Regeln.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — nur falls eine zusätzliche
  C#-Symbol-, Referenz- oder Impact-Frage entsteht; immer mit absolutem
  `projectRoot`.
- `src/AiNetLinter.TestKit/TestWaiter.cs` — nur zur Bestätigung der
  vorhandenen bounded Polling-/Timeout-Semantik.
- `src/AiNetLinter.FastTests/Architecture/FastTestsDependencyGuardTests.cs`
  und `src/AiNetLinter.IntegrationTests/AiNetLinter.IntegrationTests.csproj`
  — nur falls die Testplatzierung oder InternalsVisibleTo geprüft werden
  muss.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessContracts.cs`
  — nur zur Prüfung, dass Result-, Timeout- und Cancellation-Verträge
  unverändert bleiben.

### `out_of_scope`

- `ExternalSourceRepositoryFailurePolicy.cs`,
  `GiteaGitRepositoryTransport.cs`,
  `ExternalSourceRepositoryAcquirer.cs` sowie alle Provider-, Snapshot-,
  Refresh-, Fetch-, Cache-, Manifest-, Source-of-Truth- und Host-Dateien.
- HTTP-/Git-Klassifikation, Credentials, Redaction, URL-/Success-Policy,
  `TD-005` und der 1314-/Reparse-Fallback.
- MCP-Registrierungen, Mapping-JSON, externe Projekte, Git-/Gitea-Remotes,
  Netzwerk, echte Credentials, Restores, Stress-Tests und
  Systemprivilegien.
- Alle Task-Zustandsdateien außer diesem Plan, insbesondere
  `task-state.md`, `roadmap.md`, `codemap.md` und `tech-debt.md`.
- Assembly.Load, AssemblyLoadContext, Reflection, allgemeine neue
  Artefakt-/Prozessabstraktionen und native ABI-Feldbereinigung.

## Invarianten

- Der Start bleibt `CREATE_SUSPENDED` → `AssignProcessToJobObject` →
  `ResumeThread`; `KILL_ON_JOB_CLOSE`, SafeHandle-Ownership und der
  bestehende `ProcessStartInfo`-/`ArgumentList`-/Redirect-/Working-
  Directory-Vertrag werden nicht gelockert.
- `HasExited` des Parents ist für einen zugeordneten/resumierten Prozess
  kein Baum-Nachweis. Ein Erfolg benötigt entweder den zulässigen
  suspendierten Parent-only-Zustand oder eine erfolgreiche, bounded
  Job-/Prozessbaum-Prüfung; unbekannte Nachfahren werden nicht per
  Prozesslisten- oder Namensheuristik gesucht.
- `CloseHandle`-Fehler des Job-SafeHandles sind in Startressourcen- und
  TreeScope-Cleanup sichtbar. Der explizite Close-Pfad ist idempotent;
  `ReleaseHandle` wirft keine unkontrollierte Exception und verliert den
  Status nicht im normalen Besitzerpfad.
- Primär-Exceptions bleiben Typ, Ursache und Stack nach Cleanup-Fehlern
  erhalten. Timeout, Caller-Cancellation und normale Resultate behalten
  ihre bisherige Semantik; das Original-Cancellation-Token bleibt sichtbar.
- stdout/stderr, Reader, Prozesse, Jobs und Test-Cleanup bleiben bounded;
  erzeugte Tasks werden beobachtet und lokale Test-PIDs nach jedem
  Testpfad final als beendet verifiziert.
- Die zentrale Helper-Ablage ändert weder native ABI-Felder noch bestehende
  Diagnose-Texte, Argument-/Environment-Isolation oder Secret-Redaktion.
- HTTP-/Git-Klassifikation, Credentials, `TD-005`, 1314-/Reparse-Fallback,
  Provider, Snapshot, Refresh, Cache, Source-of-Truth und Host-Wiring
  bleiben unverändert.
- Es gibt keinen öffentlichen API-/MCP-/Mapping-Change, kein
  Assembly-Loading, keine Reflection und keine Systemprivilegienänderung.

## Risiken und Gegenmaßnahmen

- **False Success bei Parent-exit:** Lifecycle-Zustand und Job-Ownership
  explizit bis zum Cleanup führen; `HasExited` allein für resumed/assigned
  Prozesse ablehnen. Der lokale Parent-/Grandchild-Test muss genau diesen
  Zustand mit geöffneten geerbten Pipes erzwingen.
- **Job-Close beendet nicht nachweisbar den Baum:** `TerminateJobObject`,
  bounded Endprüfung und statusbewusstes Close sequenziell behandeln;
  `false`, Timeout und unerwartete native Status als Fehler sammeln. Der
  Testseam darf die reale lokale Close-/Teardown-Wirkung ausführen und nur
  zusätzlich einen kontrollierten Fehlerstatus liefern.
- **SafeHandle-Doppel-Close oder Leak:** Den expliziten Close-Pfad mit
  atomarem/idempotentem Zustand versehen; nach dem Besitzerpfad keinen
  zweiten Close versuchen, während der SafeHandle-Finalizer als letzte
  Sicherheitslinie bestehen bleibt.
- **Cleanup maskiert Primär-Exception:** Ursache vor Cleanup sichern,
  `CombineFailures` zentral nutzen und Fehler über den bestehenden
  Attachment-/Aggregate-Vertrag weiterreichen. Kein Cleanup-`catch` bleibt
  leer und kein Close-Fehler ersetzt den Primärfehler.
- **Testseam verändert normalen Ablauf:** Close-/Native-Delegates nur
  per Aufruf übergeben; Runtime verwendet weiterhin die echten Win32-
  Imports. Keine statische mutable Hook, keine PID-Suche und kein globaler
  Prozesszustand.
- **DRY-Zentralisierung erzeugt neue Abstraktion:** Der gemeinsame Helper
  bleibt auf Handle-Gültigkeit und Failure-Aggregation dieses Prozesspakets
  begrenzt. Keine Verallgemeinerung auf Provider-, Cache- oder andere
  Assembly-Pfade.
- **ABI-/Linter-Drift:** Win32-Strukturen unverändert lassen, neue
  Methoden klein halten, `#nullable enable`/`sealed`/sichtbare Fehler
  beachten und Low-Confidence-ABI-Felder nicht als Dead Code entfernen.
- **Scope-Drift:** Keine Berührung der bestätigten HTTP-/Git-, Credential-,
  1314-/Reparse- und Provider-/Snapshot-/Host-Verträge; der Qualitätscheck
  bleibt auf die genannten Prozessdateien begrenzt.

## Coder-Handoff

### Sicherer Einstieg

1. Diesen Handoff und die zwölf `read_first`-Dateien lesen. Danach mit
   `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` zuerst
   `get_feature_context`/`get_symbol_body` für
   `TryManagedFallback`, `ExternalSourceGitProcessNativeJob`,
   `StartupResources.Dispose`, `TreeScope.Dispose` und
   `Executor.CleanupProcessAsync` verwenden; `find_references`/`get_impact`
   nur für konkrete Auswirkungen einsetzen. `rg` bleibt auf die genannten
   Prozess-, Handle-, PID- und Wait-Muster begrenzt.
2. Zuerst den Lifecycle-Kontext modellieren: Assign-/Resume-Status und die
   noch besessene Job-Grenze müssen im Startfehler-Cleanup verfügbar sein.
   Parent-only-Fallback ist nur für den nicht resumierten, nicht
   zugeordneten Prozess zulässig; ein resumed/assigned Parent-exit wird
   über die Job-Grenze als Baum behandelt.
3. Danach den SafeHandle-Close-Pfad klein und per Aufruf testbar machen.
   `CloseHandle == false` samt `Marshal.GetLastWin32Error()` unmittelbar
   sammeln, genau einmal behandeln und in `StartupResources` sowie
   `TreeScope` sichtbar machen. Die primäre Exception wird vor Cleanup
   gesichert und danach mit erhaltener Identität erneut ausgelöst.
4. Anschließend die beiden Helper-Gruppen zentralisieren. Alle fünf
   Aufrufer müssen die gemeinsame Implementierung nutzen; Diagnose-Texte,
   Native-ABI-Felder und `CreateStartInfo` bleiben unverändert.
5. Zuletzt den vorhandenen Integration-Harness erweitern: ein echter
   Parent-exit-/Grandchild-Fall für den resumierten Baum, ein
   suspendierter Fallback-Erfolg, ein sichtbarer Fallback-Fehler und ein
   erzwungener Job-Close-Fehler mit Primär-Exception-Nachweis. PIDs,
   Marker und `finally`-Cleanup bleiben lokal und bounded.
6. Zum Abschluss die scoped MCP- und Audit-Abfragen ausführen, danach die
   vier Test-/Build-Commands im Abschnitt `Tests` ausführen und
   `step-result.md` schreiben. Der Planer führt keine Tests aus.

### Übergabeinvarianten

- Ein beendeter Parent beweist für einen assigned/resumed Prozess nicht den
  Baum-Endzustand; die Job-Grenze oder ein gleichwertiger bounded Nachweis
  muss den Grandchild-Fall abdecken.
- Der suspendierte, nie zugeordnete Startfehler darf bounded über seine
  bekannte Prozess-ID bereinigt werden; kein fremder Prozess und keine
  globale PID-Heuristik wird angesprochen.
- Job-`CloseHandle`-Fehler werden in beiden Besitzerpfaden gesammelt und
  sichtbar gemacht. SafeHandle-Ownership und idempotente Freigabe bleiben
  erhalten.
- Cleanup-Fehler verlieren weder Primär-Exception noch
  Caller-Cancellation-Token; Reader-, Prozess- und Test-Waits bleiben
  bounded und beobachtet.
- HTTP-/Git-Klassifikation, Credentials, `TD-005`, 1314-/Reparse-Fallback
  und Provider-/Snapshot-/Refresh-/Cache-/Host-Wiring bleiben unverändert.

### Nächster sicherer Einstiegspunkt

Beginne bei
`ExternalSourceGitProcessStartFailureCleanup.Cleanup` und
`TryManagedFallback` in
`src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartFailureCleanup.cs`.
Verfolge anschließend `LaunchProcess`/`ExternalSourceGitProcessNativeJob`
und die beiden Dispose-Pfade von `StartupResources` und `TreeScope`. Prüfe
danach die fünf Duplikat-Aufrufer und erweitere erst zuletzt den lokalen
Integration-Harness um den Parent-exit-/Grandchild- und Close-Fehlerfall.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — C#-Symbol-, Referenz- und
  Impact-Fragen zuerst mit absolutem `projectRoot` über AiNetLinter-MCP
  prüfen; `rg` nur ergänzend für konkrete Textarbeit einsetzen.
- `.agents/rules/AiNetLinter.mdc` — Nullable, `sealed`, kurze Methoden,
  begrenzte Komplexität, sichtbare Fehler, Testsentinel und Erhalt der
  nativen ABI-Strukturen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — statische Architektur,
  Windows-/Cancellation-/Secret-Sicherheit, bounded Testprozesse,
  `TestTempDirectory` und proaktiver DRY-/MagicValues-/DeadCode-Abbau.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md §6.2.1` —
  flacher Korrektur-Step mit `corrects: step-022`; Roadmap bleibt
  unverändert.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md §10.3,
  §10.5, §10.7` — Commit-Suffix, Korrekturkettenbudget, Split-Gates und
  dichte Artefakte.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md`
  — Fix-Modus, JIT-Ist-Zustand, Pointer-Referenzen und Planer ohne
  Produktionscode.

## Bekannte Ausnahmen

- Der echte Reparse-/Symlink-Test darf weiterhin ausschließlich bei
  `ERROR_PRIVILEGE_NOT_HELD (1314)` übersprungen werden. Dieser Step ändert
  weder den Test noch die Capability-Projektion.
- Der Real-Executor-Harness bleibt in `IntegrationTests`, weil die
  bestehende FastTests-Dependency-Grenze direkte
  `System.Diagnostics.Process`-Nutzung dort nicht zulässt.
- Der Planer führt keine Tests aus. Der Coder behandelt fehlende lokale
  Test-Infrastruktur gemäß Drift-Loop als Infrastruktur-Blocker und
  umgeht keine Sicherheits- oder Privilegiengrenze.

## Notes

Step 023 korrigiert ausschließlich die drei Findings aus dem Review von
Step 022. Der sichere Ansatz ist die Trennung zwischen einem noch
suspendierten, nie zugeordneten Startfehler und einem bereits resumierten
Prozessbaum sowie ein expliziter, statusbewusster Job-Close vor der
SafeHandle-Freigabe. Die neue gemeinsame Helper-Datei bleibt auf diese
Prozess-/Handle-Grenze begrenzt; keine spätere Provider-/Snapshot-
Lebenszyklusentscheidung wird vorweggenommen.
