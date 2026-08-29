---
status: done
task: decompiled-assembly-analysis
step: 023
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T12:43:27+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 023: Prozessbaum-Fallback und Handle-Cleanup vollständig fail-closed schließen

## Verdict

- [x] **approved** – Plan, Regeln, Logik und Konzept vollständig erfüllt.
- [ ] **issues** – Nicht zutreffend; es wurden keine blockierenden oder sonstigen Findings festgestellt.
- [ ] **blocked** – Nicht zutreffend.

## Geprüft

- [x] Plan geprüft
- [x] Regeln geprüft
- [x] Logik geprüft
- [x] Konzept geprüft
- [x] Build und Tests ausgeführt

## Plan-Erfüllung

Alle acht Abnahmekriterien des Step-Plans sind erfüllt: Der PID-Fallback ist in `ExternalSourceGitProcessStartFailureCleanup.cs:323` auf `CreatedSuspended` begrenzt; für zugeordnete bzw. fortgesetzte Prozesse bleiben Job- und Prozess-Wait-Bestätigungen erforderlich und ein Parent-Exit allein liefert keinen Erfolg. Die lokalen Parent-/Grandchild-/Job-Regressionsfälle, Post-Create- und Post-Resume-Fehler, Timeout/Cancellation, Output-Limits und Handle-Cleanup sind bounded geprüft.

## Findings

Keine CRITICAL-, MAJOR-, MINOR- oder sonstigen Findings.

## Rules-/Konzeptprüfung

Die Native-Sequenz `CREATE_SUSPENDED → AssignProcessToJobObject → ResumeThread` sowie `KILL_ON_JOB_CLOSE` bleiben erhalten. Argumente, WorkingDirectory, `GIT_*`-Isolation, Secret-Schutz, Step-019-Klassifikation, Step-018-1314-/Reparse-Fallback und TD-005 bleiben außerhalb der Änderung bzw. unverändert; es gibt keine Provider-/Snapshot-/Cache-/Host-Wiring-Ausweitung, kein Remote-/Git-Netzwerk und keine Assembly.Load-/Reflection-/Systemprivilegien-Nutzung.

## Native-, Handle- und Prozessbewertung

`ExternalSourceGitProcessNativeJob.ReleaseHandle` in `ExternalSourceGitProcessLauncher.cs:462` ruft den Close-Delegate über den idempotenten Attempt-Guard höchstens einmal auf, unterscheidet ungültige Handles, sammelt `false`-/Win32- und Cleanup-Fehler und lässt die primäre Exception über Cleanup-Daten bzw. Aggregation beobachtbar. `StartupResources.Dispose` und `TreeScope.Dispose(ICollection<Exception>)` sammeln die Job-/Stream-/Cleanup-Fehler sichtbar. `ExternalSourceGitProcessExecutor.CleanupProcessAsync` in `ExternalSourceGitProcessExecutor.cs:253` hält Terminierung, Prozess-Wait, Reader-Wait und Scope-Dispose bounded.

Die acht fokussierten lokalen Regressionstests prüfen echte Parent-/Grandchild-Prozesse, Job-Lifetime und Handle-Close-Seams; nach den Läufen wurden keine relevanten Testprozesse zurückgelassen.

## MCP- und Qualitätsprüfung

- AiNetLinter-MCP wurde mit absolutem `projectRoot` `C:/Daten/Entwicklung/Ralf/AiNetLinter` für Feature-/Symbol-/Body-, Referenz-, Impact-, Testkontext- und Violation-Prüfungen verwendet; im Step-023-Scope wurden 0 Violations gefunden.
- Die zentrale `IsUsableHandle`-Implementierung und `CombineFailures` sind jeweils ohne semantische Duplikat-Kandidaten referenziert; die Native-ABI-Strukturen und P/Invoke-Signaturen sind unverändert.
- `find_duplicates`: solutionweit wurden nur bestehende, außerhalb des Scopes liegende Cluster gefunden; im Produktionsscope 0 exakte Duplikatcluster bei 308 Methoden, im Testscope 0 bei 19 Methoden, und für beide neuen Helper 0 Refactoring-Drift-Kandidaten. Die fünf strukturellen Kandidaten betreffen ausschließlich bestehende, fachfremde Codebereiche.
- `find_magic_values`: 25 Treffer/24 eindeutige Produktionseinträge und 64/47 einschließlich Tests; sie sind bestehende Diagnose-/Labelwerte, der begrenzte Output-Buffer und lokale Testwerte, ohne neuen sicherheitsrelevanten oder ABI-bezogenen Befund.
- `find_dead_code`: 34 Low-/0 High-Kandidaten; sämtliche Low-Kandidaten sind native ABI-Strukturfelder und dürfen nicht entfernt werden.

## Build- und Teststatus

- `dotnet build`: grün, 0 Warnungen, 0 Fehler.
- Fokussierte Step-023-Tests (`ExternalSourceGitProcessExecutorTests`): 8 bestanden, 0 übersprungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`: 1.994 bestanden, 1 übersprungen, 0 Fehler; der Skip ist transparent `ERROR_PRIVILEGE_NOT_HELD (1314)` beim echten Symlink/Reparse-Test.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`: 368 bestanden, 0 übersprungen, 0 Fehler.
- Stress-Tests wurden gemäß Auftrag nicht ausgeführt.

## Geänderte Dateien

- `tasks/decompiled-assembly-analysis/step-023/step-review.md`

`tech-debt.md` blieb unverändert; es wurde kein neuer oder angepasster Tech-Debt-Fund festgestellt. Produktionscode, `task-state.md`, `roadmap.md` und `codemap.md` wurden durch den Kritiker nicht geändert.

## Folgeaktion

Step 023 kann genehmigt und der Dev-Loop mit dem nächsten geplanten Schritt fortgesetzt werden. Der privilegierte 1314-Skip bleibt als bekannte Capability-Einschränkung transparent und sollte in einer privilegierten Umgebung erneut ausgeführt werden.
