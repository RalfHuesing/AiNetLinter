---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 022
corrects: step-021
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-29T11:12:29+02:00
code_commit_hash: 872b4855
status_after: done
blocker_category: n/a
---

# Result Step 022: Native Startfehler und Test-Cleanup fail-closed absichern

## Zusammenfassung

Der native Startpfad prüft jetzt die Rückgabewerte von
`AssignProcessToJobObject`, `TerminateProcess` und
`WaitForSingleObject` inklusive Win32-Fehlerstatus. Bei fehlender
Job-Zuweisung bleibt der Prozess suspendiert; ein bounded PID-basierter
Fallback beendet ihn nur mit verifiziertem `WaitForExit`-/`HasExited`-Nachweis,
andernfalls bleibt der Start fail-closed. Cleanup-Fehler werden an die
primäre Exception angehängt, deren Stack und Typ erhalten bleiben. Der
Integration-Harness wartet im `finally` bounded auf alle bekannten Parent- und
Grandchild-PIDs und prüft ihren finalen Zustand.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs` — native Startsequenz, Job-Zuweisung, Resume- und Handle-Cleanup fail-closed abgesichert.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncherNativeHelpers.cs` (neu) — Environment-, Argument-, Input-Handle- und native Handle-Hilfen aus dem Launcher ausgelagert.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartFailureCleanup.cs` (neu) — native Statusauswertung, bounded Fallback, Fehleraggregation und Primär-Exception-Erhalt.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessNativeMethods.cs` — zentraler generischer Win32-Fehlercode für fehlende native Fehlerstatus ergänzt; ABI-Felder unverändert.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartupResources.cs` — kontrollierte, vollständige Freigabe aller Startressourcen mit sichtbaren Cleanup-Fehlern.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessTreeScope.cs` — native Operations-Seam an den Startpfad durchgereicht.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs` — native Operations-Seam und gemeinsame Cleanup-Fehlerzuordnung verwendet.
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs` — bounded Parent-/Grandchild-Finally, PID-Endzustand und Regression für fehlende Job-Zuweisung, fehlgeschlagenes Terminate und `WAIT_FAILED`.

## Commit

- **Code-Commit-Hash:** `872b4855`
- **Message:**
  ```
  fix: Native Cleanup fail-closed absichern [decompiled-assembly-analysis]

  Prüfe native Termination-/Wait-Status und fallbacke bounded über die bekannte PID.
  Verifiziere Testprozess-PIDs auch im finally.

  Refs: tasks/decompiled-assembly-analysis/step-022
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceGitProcessExecutorTests" --logger "console;verbosity=minimal"` — grün, 6 bestanden, 0 übersprungen, 6 gesamt.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1994 bestanden, 1 übersprungen, 1995 gesamt; einziger Skip ist der bekannte Win32-1314-Reparse-Skip `AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 366 bestanden, 0 übersprungen, 366 gesamt.
- Stress-Tests — nicht ausgeführt.

## Lifecycle-/Sicherheitsnachweise

- Die Reihenfolge bleibt `CREATE_SUSPENDED` → `AssignProcessToJobObject` → `ResumeThread`; `KILL_ON_JOB_CLOSE`, SafeHandle-/Job-Lifetime sowie bounded stdout/stderr bleiben erhalten.
- Jeder native Boolean-Rückgabewert wird geprüft. Ein nicht erfolgreicher `AssignProcessToJobObject`-Aufruf erzeugt die primäre `Win32Exception`; `TerminateProcess`-Fehler, `WAIT_TIMEOUT`, `WAIT_FAILED`, `WAIT_ABANDONED` und unbekannte Wait-Status werden als Cleanup-Fehler sichtbar erfasst.
- Vor dem Schließen der Prozesshandles wird ein erster und abschließender bounded `WaitForSingleObject` mit jeweils 5.000 ms ausgeführt. Wenn der native Nachweis fehlt, versucht der Fallback `Process.Kill(entireProcessTree: true)` und `WaitForExit(5.000 ms)`; `HasExited` muss anschließend true liefern. Ohne Nachweis wird kein False Success erzeugt.
- Cleanup-Fehler werden in `Exception.Data` an die primäre Exception angehängt; bei nicht möglicher Zuordnung entsteht ein sichtbares `AggregateException`. Die primäre Exception wird per `ExceptionDispatchInfo` erneut ausgelöst.
- Die beiden bestehenden Prozessbaumtests verwenden im `finally` einen bounded 10-Sekunden-Wait über die distinct bekannten Parent-/Grandchild-PIDs, prüfen anschließend jeden PID-Zustand und melden aktive oder nicht nachweisbar beendete Testprozesse als Fehler. Der neue Fallback-Test verifiziert zusätzlich Parent-PID, mindestens zwei native Wait-Aufrufe, fehlgeschlagene native Operationen, finalen Nicht-Laufzustand und ausbleibenden Startmarker.
- Argumente, WorkingDirectory, `GIT_*`-Isolation, Secret-Schutz, Caller-Cancellation-/Timeout-Semantik und HTTP-/Git-Klassifikation blieben unverändert. TD-005 sowie der Step-018-1314-/Reparse-Fallback wurden nicht verändert.
- Es wurden kein Git-Remote, Netzwerk, Gitea, Credential, `Assembly.Load`, Reflection oder Systemprivilegienänderung verwendet.

## MCP-/DRY-/MagicValues-/DeadCode-Ergebnis

- MCP `find_symbol` fand die beiden neuen Klassen; `get_impact` lief erfolgreich für Cleanup und Native-Operations mit 14 bzw. 50 Call-Sites. `get_violations` meldete im Produktions- und Testscope jeweils 0 Violations.
- Scoped `find_duplicates` (`clone`, `exact`, `minTokens=20`) ergab 256 geprüfte Produktionsmethoden und 15 geprüfte Testmethoden, jeweils 0 Cluster.
- Scoped `find_magic_values` ergab 20 Vorkommen, davon 6 Constant- und 1 Standard-Kategorie; der `changedOnly`-Lauf mit Tests ergab 10 Vorkommen. Die neu benötigten nativen Fehler-/Wait-Werte sind benannte Konstanten; kein zusätzlicher unabhängiger Magic-Value-Sweep war erforderlich.
- Scoped `find_dead_code` ergab 34 Low-Confidence-Feldtreffer, 0 High-Confidence-Treffer; alle betreffen unveränderte native ABI-Struct-Felder. Es wurden keine ABI-Felder entfernt und keine neuen Prozesspaket-Mitglieder als Dead Code gefunden.
- Der scoped `safeguard` lag bei 7,38/10 und schlug wegen `MaxDirectoryChildren` im Assemblies-Verzeichnis sowie eines transitive erfassten `DaemonHostCommand`-`AIContextFootprint`-Befunds fehl. Beide Befunde sind nicht neu durch den Prozesscode; der direkte Prozessscope-Violationslauf ist 0.

## Abweichungen vom Plan

Die Umsetzung blieb fachlich im geplanten Prozesspaket. Wegen des 500-Zeilen-Limits wurde die native Launcher-Hilfslogik zusätzlich in
`ExternalSourceGitProcessLauncherNativeHelpers.cs` ausgelagert; die
Cleanup-Logik liegt separat in
`ExternalSourceGitProcessStartFailureCleanup.cs`. Für die Testbarkeit wurde
eine per-Aufruf-Native-Operations-Seam durch Executor, TreeScope und Launcher
geführt. Die im Plan als fehlend erwartete separate
`context_budget-Handoff`-Datei existiert im Repository nicht; die vorhandenen
Handoff-/Budget-Hinweise aus Step-022 wurden vor der Umsetzung berücksichtigt.

## Beobachtungen

Der Windows-Host hat die realen nativen Prozess-, Pipe- und Job-Object-Pfade
einschließlich des erzwungenen Assign-/Terminate-/Wait-Fehlerpfads
erfolgreich ausgeführt. Der vollständige Integration-Lauf dauerte 2 m 36 s;
die längsten bestehenden Daemon-/MCP-Vertragstests liefen bounded und
beendeten sich vollständig. `task-state.md`, `roadmap.md`, `codemap.md` und
`tech-debt.md` wurden nicht geändert.

## Bekannte Unschärfen

Die native Implementierung bleibt Windows-spezifisch; sie wurde auf dem
vorliegenden Windows-Host verifiziert. Der managed Fallback kann bei einem
bereits verschwundenen PID keinen zusätzlichen PID-Nachweis mehr liefern und
bleibt deshalb konservativ fail-closed, sofern der native Handle-Wait nicht
das Prozessende beweist. Die scoped Safeguard-Warnungen bleiben für einen
späteren, separaten Architektur-/Verzeichnis- beziehungsweise Daemon-Scope
offen.
