---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 021
corrects: step-020
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-29T10:11:57+02:00
code_commit_hash: 510600149406f909d9cca03a1b9974a5b0e94c9f
status_after: done
blocker_category: n/a
---

# Result Step 021: Prozessbaum-Besitz und Timeout-Vorbereitung am Git-Executor schließen

## Zusammenfassung

Der Git-Executor startet den Prozess jetzt in einem suspendierten Windows-Job,
ordnet ihn vor dem `ResumeThread` dem Job zu und aktiviert
`KILL_ON_JOB_CLOSE`. Timeout und Linked-CTS werden vor der nativen
Prozesserzeugung angelegt; ein Fehler nach `CreateProcessW` beendet den
angelegten Prozess explizit und wartet bounded auf sein Ende. Der Cleanup-Pfad
beendet den Job unabhängig vom `HasExited`-Zustand, schließt stdout/stderr und
wartet endlich auf Prozess und Leser.

Der Integration-Harness weist den Parent-exit-/Grandchild-open-pipe-Race mit
echtem `pwsh`-Child und Grandchild nach und prüft außerdem Caller-Cancellation
mit Originaltoken, Timeout-Klassifikation, bounded Output sowie den
Pre-Start-Fall eines nicht darstellbaren Timeouts ohne Startmarker.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs` — CTS-/Cleanup-Lifecycle und Job-basierte Terminierung.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs` — suspendierter nativer Start, Job-Zuordnung, Environment-/Argument-/Pipe-Setup.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessNativeMethods.cs` — Windows-Interop und ABI-Structs.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartupResources.cs` — fehlersicherer Besitz der Startressourcen.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessTreeScope.cs` — idempotenter Prozess-/Pipe-/Job-Besitz.
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs` — echte Child-/Grandchild- und Pre-Start-Timeout-Regressionen.
- `tasks/decompiled-assembly-analysis/step-021/step-plan.md`, `tasks/decompiled-assembly-analysis/step-021/step-result.md` — Step-Dokumentation.

Unverändert blieben `task-state.md`, `roadmap.md`, `codemap.md` und
`tech-debt.md`; insbesondere wurde der TD-005-Status nicht verändert.

## Commit

- **Code-Commit-Hash:** `510600149406f909d9cca03a1b9974a5b0e94c9f`
- **Message:** `fix: Prozessbaum-Timeout absichern [decompiled-assembly-analysis]`
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** folgt als separater zweiter Commit.

## Build-/Test-Output

- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceGitProcessExecutorTests"` — grün, 5 bestanden, 0 übersprungen, 5 gesamt.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1994 bestanden, 1 übersprungen, 1995 gesamt. Der einzige Skip ist der bekannte Win32-1314-Reparse-Skip `AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 365 bestanden, 0 übersprungen, 365 gesamt.
- Stress-Tests — nicht ausgeführt.

## Lifecycle-/Sicherheitsnachweise

- Der Parent läuft erst nach `AssignProcessToJobObject` und erfolgreichem
  `ResumeThread`; dadurch ist die Job-Grenze vor dem ersten User-Code aktiv.
- `TerminateJobObject` wird im Cleanup nicht wegen eines bereits beendeten
  Parent-Prozesses übersprungen. Der echte Harness lässt den Parent sofort
  enden, während der Grandchild die stdout/stderr-Pipes offen hält; Timeout und
  Cancellation beenden beide PIDs innerhalb endlicher Waits.
- stdout/stderr bleiben auf `OutputCaptureLimit` begrenzt. Die bestehende
  Argument-, WorkingDirectory-, `GIT_*`-Umgebungs-, Secret- und
  HTTP-/Git-Statusklassifikation blieb erhalten.
- Ein überlanger Timeout wirft vor dem nativen Start; der Testmarker bleibt
  absent. Nach `CreateProcessW` werden Startfehler per `TerminateProcess` und
  bounded `WaitForSingleObject` bereinigt.
- Nach dem finalen Testlauf wurden keine Harness-Prozesse und keine aktiven
  AiNetLinter-Testprozesse gefunden. Es wurden kein Git-Remote, Netzwerk,
  Gitea, Credential, `Assembly.Load`, Reflection oder Systemprivilegien
  verwendet.

## MCP-/DRY-/MagicValues-/DeadCode-Ergebnis

- MCP `get_feature_context`: Executor 435 Zeilen, 398 Codezeilen,
  AI-Context-Footprint 1012, 8 Aufrufer, 5 statisch zugeordnete Integration-
  Tests, 0 Violations. `get_impact` zeigte 8 Call-Sites; `find_references`
  bestätigte `TryTerminate` ausschließlich über `TreeScope`.
- MCP `get_violations` für `ExternalSourceGitProcess`: 0 Violations.
- DRY-Audit mit `find_duplicates` im Produktionsscope: 243 Methoden,
  0 Clone-Cluster. Im Testscope: 13 Methoden, 0 Clone-Cluster.
- `find_magic_values` im vollständigen Scope meldete 19 bestehende bzw.
  absichtliche Testmarker-/Diagnosewerte; der `changedOnly`-Audit meldete nur
  den bestehenden Bufferwert `4096`. Es wurde kein passender Refactoringbedarf
  innerhalb dieses Lifecycle-Pakets erzwungen.
- `find_dead_code`: 34 Low-Confidence-Feldtreffer, 0 High-Confidence-Treffer.
  Alle Treffer sind Felder der nativen ABI-Structs und werden für die
  Layout-Kompatibilität benötigt; sie wurden nicht als Dead Code entfernt.

## Abweichungen vom Plan

Die geplante Job-Grenze wurde fachlich 1:1 umgesetzt. Wegen der projektweiten
500-Zeilen- und Nested-Type-Regeln ist die native Startlogik in Launcher,
Interop-, Startup-Resources- und Tree-Scope-Dateien aufgeteilt. Die
`PROC_THREAD_ATTRIBUTE_JOB_LIST`-Variante wurde zugunsten der äquivalenten
OS-Grenze `CREATE_SUSPENDED` → `AssignProcessToJobObject` → `ResumeThread`
verworfen, nachdem der reale Windows-Harness damit intermittierende
`ERROR_INVALID_HANDLE`-Starts zeigte; der suspendierte Prozess führt vor der
Zuordnung keinen Code aus. `codemap.md` wurde entsprechend der expliziten
Step-Vorgabe nicht geändert.

## Beobachtungen

Die Regressionen liefen auf dem Windows-Host mit realen Prozessen, Pipes und
Job Objects stabil. Die vollständigen IntegrationTests benötigen wegen
bestehender Daemon-/MCP-Vertragstests mehrere Minuten, blieben aber innerhalb
der vorhandenen endlichen Test-Waits vollständig grün. Kein neuer Tech-Debt-
Fund wurde angelegt; TD-005 bleibt erledigt.

## Bekannte Unschärfen

Die Job-/Pipe-Implementierung ist bewusst Windows-spezifisch und wurde auf dem
vorliegenden Windows-Host verifiziert. Der einzige bekannte Test-Skip bleibt
der bestehende Win32-1314-Reparse-Skip; weitere Skips oder OS-Blocker traten
nicht auf.
