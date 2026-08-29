---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 022
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T11:35:48+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 022: Native Prozessbereinigung

## Verdict

- [ ] approved
- [x] issues
- [ ] blocked

Die beiden Step-021-Findings sind im Kern bearbeitet: `TerminateProcess` und
`WaitForSingleObject` werden über einen Test-Seam ausgewertet, die
CreateSuspended-Ownership-Reihenfolge ist korrekt, der lokale
Post-Create-Fehlerfall hat einen bounded PID-Fallback, und die direkte
Integration-`finally` wartet begrenzt und prüft alle aufgezeichneten PIDs.

`issues` bleibt erforderlich. Der Fallback kann bei bereits beendetem Parent
ohne Grandchild-Nachweis `true` liefern, der Job-SafeHandle verwirft einen
fehlgeschlagenen `CloseHandle`, und der Step führt zwei direkt eingeführte
In-Scope-DRY-Duplikate ein.

## Geprüfte Kriterien

- [x] `CREATE_SUSPENDED` → `AssignProcessToJobObject` → `ResumeThread` ist in
  dieser Reihenfolge implementiert.
- [x] `KILL_ON_JOB_CLOSE`, Job-/Pipe-Ownership und idempotente TreeScope-
  Bereinigung sind nachvollziehbar.
- [ ] Jeder Post-Create-Pfad beweist fail-closed den Prozessbaum; der
  Parent-Exit wird im Fallback noch als ausreichender Endnachweis behandelt.
- [ ] Jeder Handle-Cleanup-Fehler ist sichtbar; der Job-SafeHandle meldet ein
  falsches `CloseHandle`-Ergebnis nicht an die Cleanup-Sammler.
- [x] Primär-Exceptions bleiben erhalten und die geprüften nativen
  Terminate-/Wait-Fehler werden als Cleanup-Failures gesammelt.
- [x] stdout/stderr sind begrenzt; Prozess-, Reader- und Cleanup-Waits sind
  bounded.
- [x] Timeout/Cancellation werden vor dem Start vorbereitet und lokale
  Child-/Grandchild-Prozesse werden im Test-`finally` bounded bereinigt und
  final verifiziert.
- [x] Argumente, Working Directory, `GIT_*`-Isolation und Secret-Schutz sind
  erhalten; kein Remote-/Gitea-/Git-Netzwerkzugriff wird verwendet.
- [ ] Die projektweite DRY-Regel ist im neuen Produktionscode eingehalten.
- [x] Step-018-Capability-Fallback, Step-019-HTTP-/Git-Klassifikation und
  TD-005 bleiben unverändert; es gibt kein Provider-/Snapshot-/Refresh- /
  Cache-/Source-of-Truth-/Host-Wiring-/Assembly.Load-/Reflection-Drift.

## Befund

### Plan-Erfüllung

Die native Startreinigung prüft nun explizit den Bool-Status von
`TerminateProcess` und akzeptiert bei `WaitForSingleObject` ausschließlich
`WAIT_OBJECT_0`; Timeout, `WAIT_FAILED` und unbekannte Status werden sichtbar
gemacht. Der Fallback nutzt die bekannte PID und wartet bounded. Das neue
lokale Seam-Integrationstesting erzwingt einen fehlgeschlagenen Job-Assign,
fehlgeschlagene Terminierung und einen ersten fehlgeschlagenen Wait; die
Primär-Exception bleibt eine `Win32Exception`, Cleanup-Failure werden in
`Exception.Data` angehängt.

Die Prozess-Lifecycle-Reihenfolge ist korrekt. Der native Job wird mit
`KILL_ON_JOB_CLOSE` konfiguriert, die Job-Ownership wird erst nach
erfolgreichem Launcher-Return übertragen, und `TreeScope.Dispose` ist
idempotent. Die bestehenden lokalen Timeout- und Cancellation-Tests starten
einen echten Parent mit Grandchild, warten in `finally` begrenzt und
verifizieren danach Parent und Grandchild. Der Pre-Start-Timeout-Test setzt
keinen Startmarker.

Die vollständige fail-closed-Garantie ist jedoch noch nicht erreicht. Der
managed Fallback in
`ExternalSourceGitProcessStartFailureCleanup.TryManagedFallback` überspringt
`Kill(entireProcessTree: true)`, sobald `process.HasExited` bereits `true`
ist, und liefert nach `WaitForExit` plus erneutem Parent-`HasExited` in Zeile
299 `true`. Ein bereits beendeter Parent beweist damit nicht, dass ein
Grandchild beendet ist. Der neue Fallback-Test läuft zwar lokal und ohne
Netzwerk, erzeugt wegen `CREATE_SUSPENDED` beim simulierten Assign-Fehler
aber noch keinen Grandchild und prüft daher genau diese Regression nicht.

Zusätzlich bleibt ein nativer Job-Handle-Close außerhalb der sichtbaren
Fehleraggregation: `ExternalSourceGitProcessNativeJob.ReleaseHandle` reicht
das Bool-Ergebnis von `CloseHandle` direkt an `SafeHandle` zurück. Die
Aufrufer `StartupResources.Dispose` und `TreeScope.Dispose` können ein
`false` deshalb nicht als Cleanup-Failure sammeln. Damit ist ein Fehler beim
Schließen des Handles, das `KILL_ON_JOB_CLOSE` auslöst, nicht sichtbar.

### Rules-Konformität

Die MCP-Lintabfrage meldet für die acht betroffenen Produktionsdateien und
den direkten Integrationstest jeweils 0 Violations. ABI-Low-Confidence-
Felder wurden nicht entfernt. Die DRY-Regel ist dennoch verletzt: Der
gezielte `find_duplicates`-Clone-Scan findet drei identische
`IsUsableHandle`-Methoden; der strukturelle Scan findet zusätzlich zwei
identische `CombineFailures`-Methoden. Beide Duplikatgruppen liegen im
Step-022-Produktionsscope und sind ohne Architekturentscheidung in einen
gemeinsamen internen Helper konsolidierbar.

Die Magic-Value-Abfrage liefert 21 Treffer in 20 eindeutigen Einträgen im
Produktionsscope und 31 Treffer in 13 eindeutigen Test-Literalen. Die
numerischen/PowerShell-/Marker-Werte sind benannte Buffer- oder lokale
Fixture-Werte; daraus ergibt sich kein zusätzlicher blockierender
Step-022-Befund. Die Dead-Code-Heuristik meldet 34 LOW-Kandidaten und 0 HIGH;
alle sind native ABI-Strukturfelder in
`ExternalSourceGitProcessNativeMethods.cs` und bleiben daher erhalten.

### Logische Korrektheit

Die Statusauswertung der nativen Termination- und Wait-Operationen ist
fail-closed und bounded, die Primär-Exception wird per
`ExceptionDispatchInfo` erneut ausgelöst, und Ausgabe-Pipes werden begrenzt.
Die verbleibende Parent-only-Erfolgsauswertung im PID-Fallback kann aber
einen lebenden Grandchild als bereinigt melden. Der Fehler ist nicht durch
den normalen Job-Erfolgspfad entschärft, wenn ein späterer Post-Create-Fehler
bereits nach `ResumeThread` eintritt.

### Konzept-Treue

Der Scope bleibt auf lokalen, read-only Prozessstart und Cleanup begrenzt.
Die Tests verwenden ausschließlich temporäre lokale PowerShell-Skripte,
keine Remotes, Gitea, Git-Netzwerkzugriffe, Credentials oder Secrets. Die
bestehenden Provider-/Snapshot-/Cache-/Source-of-Truth-Grenzen sowie die
statische metadata-only Assembly-Analyse sind nicht erweitert worden.

## Findings

1. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartFailureCleanup.cs:271-299` — **[CRITICAL] [Logik/Plan]** Der PID-Fallback behandelt einen bereits beendeten Parent nach bounded `WaitForExit` und erneutem `HasExited` als vollständigen Cleanup-Erfolg, ohne `Kill(entireProcessTree: true)` auszuführen oder ein Grandchild zu verifizieren. Dadurch kann ein lebender Grandchild nach einem nativen `WAIT_FAILED`/Timeout als bereinigt gelten. **Fix:** Den Fallback nach Lifecycle-Zustand trennen und `HasExited` niemals allein als Baum-Nachweis akzeptieren; entweder für den unzugeordneten suspendierten Prozess die zulässige Invariante explizit beweisen und für resumed/assigned Prozesse die Job-basierte Baum-Bereinigung mit überprüftem Handle-Close verwenden, oder einen bounded Baum-Fallback mit finaler Prüfung aller bekannten Nachfahren implementieren. Einen lokalen Parent-exit-/Grandchild-Regresstest mit erzwungenem native Wait-Fehler ergänzen.

2. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs:390-391`, `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartupResources.cs:42`, `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessTreeScope.cs:53` — **[MAJOR] [Plan/Logik]** `ExternalSourceGitProcessNativeJob.ReleaseHandle` gibt das Bool-Ergebnis von `CloseHandle` an `SafeHandle` zurück, aber die `Dispose`-Pfade können ein `false` nicht sammeln oder an die Primär-Exception anhängen. Ein fehlgeschlagener Job-Close und damit ein nicht nachgewiesenes `KILL_ON_JOB_CLOSE` bleiben unsichtbar. **Fix:** Einen internen, genau-einmaligen Job-Close-Pfad mit injizierbarem Status einführen, das `CloseHandle`-Ergebnis in Startfehler- und normalem TreeScope-Cleanup als Failure sammeln und bei Primär-Exceptions anhängen; den SafeHandle weiterhin als Besitzer verwenden und den Pfad mit einem erzwungenen Close-Fehler testen.

3. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs:369-375`, `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartFailureCleanup.cs:364-373`, `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncherNativeHelpers.cs:94-95`, `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartupResources.cs:81-82` — **[MAJOR] [Rules]** Step 022 lässt zwei direkt eingeführte Produktions-Duplikatgruppen bestehen: `CombineFailures` ist strukturell identisch, `IsUsableHandle` token-identisch dreifach vorhanden. Das widerspricht der projektweiten DRY-/DuplicateCode-Leitplanke und vergrößert gerade den sensiblen Cleanup-Scope unnötig. **Fix:** Jeweils einen gemeinsamen internen Helper als einzige Implementierung verwenden, alle Aufrufer darauf umstellen, den nativen ABI-Code unverändert lassen und den scoped `find_duplicates`-Scan danach erneut ausführen.

## Verifikation

### Build und Tests

- `dotnet build` → **grün**, 0 Fehler, 0 Warnungen.
- Fokussierte Step-022-Tests
  (`FullyQualifiedName~ExternalSourceGitProcessExecutorTests`) → **grün**,
  6 bestanden, 0 übersprungen.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` →
  **grün**, 1994 bestanden, 1 übersprungen, 1995 gesamt. Der einzige Skip
  ist der bekannte echte Reparse-/Symlink-Test
  `AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
  wegen Win32-Fehler 1314.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` →
  **grün**, 366 bestanden, 0 übersprungen.
- Stress wurde nicht ausgeführt.
- Nach den Läufen waren keine Test-`pwsh`-/`tree.ps1`-/`grandchild.ps1`-
  Prozesse aktiv.

### MCP-, DRY-, MagicValues- und DeadCode-Prüfung

Alle AiNetLinter-MCP-Abfragen wurden mit dem absoluten
`projectRoot` `C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt. Die
Feature-/Symbol-/Body-Abfragen, Referenzen und Impact-Abfragen decken
Launcher, StartFailureCleanup, NativeMethods, TreeScope, Executor und den
direkten Integrationstest ab; der Testkontext enthält sechs fokussierte
Tests. `get_violations` meldet im Produktionsscope und im Testscope 0.

Der solutionweite Clone-Scan mit `scopeDir=src`, `minTokens=20` und
`scopeType=production` fand einen bestehenden Exact-Cluster außerhalb des
Step-022-Scopes. Der gezielte Scope-Scan mit `minTokens=10` fand die drei
`IsUsableHandle`-Kopien; der strukturelle Scope-Scan fand die beiden
`CombineFailures`-Kopien. Refactoring-Drift-Prüfungen für die betroffenen
Native- und Cleanup-Helper ergaben keine weiteren Kandidaten. Die
Magic-Value- und Dead-Code-Zahlen sind oben dokumentiert; native ABI-Felder
werden nicht als entfernbarer Dead Code behandelt. Gezieltes `rg` bestätigt
die Create-/Assign-/Resume-Reihenfolge, bounded Waits, lokale
`GIT_*`-Isolation und das Fehlen von `Assembly.Load`, Reflection und
Remote-/Gitea-/Git-Zugriff im Scope.

## Geänderte Dateien

- `tasks/decompiled-assembly-analysis/step-022/step-review.md`

`tech-debt.md`, `task-state.md`, `roadmap.md` und `codemap.md` wurden nicht
geändert. Es wurden keine Produktionsfixes vorgenommen.

## Folgeaktion

Einen Korrektur-Step für die drei Findings anlegen. Darin den Fallback
gegen Parent-only-False-Success absichern, Job-Handle-Close-Fehler über beide
Ownership-Pfade sichtbar machen, die beiden DRY-Gruppen zentralisieren und
lokale Regressionen für Parent-exit/Grandchild sowie erzwungene Close-/Wait-
Fehler ergänzen. Danach Step 022 fokussiert und mit den vollständigen
Nicht-Stress-Läufen erneut verifizieren; erst dann ist `approved` zulässig.
