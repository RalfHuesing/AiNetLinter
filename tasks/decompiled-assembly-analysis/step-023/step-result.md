---
status: done
type: step-result
task: decompiled-assembly-analysis
step: 023
corrects: step-022
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-29T12:27:06+02:00
code_commit_hash: d1b633d0
status_after: done
blocker_category: n/a
---

# Result Step 023: Prozessbaum-Fallback und Handle-Cleanup vollständig fail-closed schließen

## Zusammenfassung

Der Startfehler-Cleanup führt den tatsächlichen Lifecycle bis in den
Fallback: Nur der nie zugeordnete, suspendierte Prozess darf bounded über
seine bekannte PID als beendet gelten; assigned/resumed Prozesse benötigen
TerminateJobObject und einen bounded Wait auf dem Job-Handle. Der
SafeHandle-Close-Pfad ist idempotent, sammelt CloseHandle-Fehler sichtbar
und erhält Primär-Exceptions. Die beiden benannten Helper-Duplikatgruppen
wurden im Prozesspaket zentralisiert und durch echte lokale
Process-/Job-/Handle-Regressionen abgesichert.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessCleanupHelpers.cs` (neu) — zentraler `IsUsableHandle`- und `CombineFailures`-Helper.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartFailureCleanup.cs` — Lifecycle-abhängiger, fail-closed Fallback sowie sichtbare Baum-/Wait-Fehler.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncher.cs` — Lifecycle-Weitergabe, Job-Wait und einmaliger statusbewusster SafeHandle-Close.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessStartupResources.cs` — Job-Close-Status in den Startressourcen-Failure-Sammler geführt.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessTreeScope.cs` — idempotenter Job-Close-Pfad für normalen Cleanup.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs` — zentralen Failure-Helper verwendet.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessLauncherNativeHelpers.cs` — zentralen Handle-Helper verwendet.
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs` — Parent-exit/Grandchild-, Startfehler- und TreeScope-Close-Regressionen ergänzt.
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessTestSupport.cs` (neu) — gemeinsamer lokaler Process-/PID-/Script-Testharness mit bounded Cleanup.

## Commit

- **Code-Commit-Hash:** `d1b633d0`
- **Message:**
  ```
  fix: Cleanup fail-closed absichern [decompiled-assembly-analysis]

  Lifecycle-Nachweis, Job-Close-Status und gemeinsame Cleanup-Helper zentralisieren.

  Refs: tasks/decompiled-assembly-analysis/step-023
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceGitProcessExecutorTests" → grün (8 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1.994 bestanden, 1 übersprungen, 0 Fehler; 1.995 gesamt)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (368 bestanden, 0 übersprungen, 0 Fehler)
Stress-Tests → nicht ausgeführt
```

Der einzige Skip ist der bestehende Reparse-Test wegen
`ERROR_PRIVILEGE_NOT_HELD (1314)`. Netzwerk, Remote-Repositories, Gitea
und Credentials wurden nicht verwendet.

## Abweichungen vom Plan

- Der lokale Testharness wurde in `ExternalSourceGitProcessTestSupport.cs`
  aufgeteilt, damit die bestehende MaxLineCount-Grenze eingehalten bleibt;
  fachlich bleibt er auf den geplanten realen Process-/Job-/Handle-Pfaden.
- Der erfolgreiche Job-Termination-Pfad wartet zusätzlich direkt auf das
  Job-Handle. Das ist die strengere Umsetzung des geplanten
  Baum-Endnachweises und verlängert keinen Wait über die bestehende finite
  Grenze hinaus.
- `codemap.md` wurde auf ausdrückliche Nutzeranweisung nicht geändert;
  ebenso blieben `task-state.md`, `roadmap.md` und `tech-debt.md` unverändert.

## Beobachtungen

- Der neue Parent-exit-/Grandchild-Test startet lokal einen echten Parent
  und Grandchild mit geerbten stdout-/stderr-Handles. Der Parent wird nach
  dem Grandchild-Start bounded abgewartet; der erzwungene Post-Create-Fehler
  führt in den resumed/assigned Cleanup. Der Job-Termination-/Wait-Nachweis
  wird absichtlich fehlschlagen gelassen, der PID-Fallback lehnt Parent-only
  ab, und der Test weist den sichtbaren Fehler sowie zwei beendete PIDs nach.
- Der Close-Fehler-Test ruft über die per-Aufruf-Seam zunächst das echte
  `CloseHandle` auf und liefert danach kontrolliert `false` mit
  `ERROR_ACCESS_DENIED`. Dadurch wird zugleich die reale
  `KILL_ON_JOB_CLOSE`-Wirkung und die einmalige, sichtbare Fehlerweitergabe
  im Startfehlerpfad geprüft. Der TreeScope-Test prüft denselben Statuspfad
  im normalen Timeout-Cleanup; beide Pfade lassen die Primär-Exception
  sichtbar.
- MCP-Nachprüfung mit absolutem `projectRoot`:
  `get_violations` meldet 0 Verstöße in 9 Produktionsdateien und 0 in 2
  Testdateien. `get_feature_context` meldet für `NativeJob` 0
  Symbol-/Dateiverstöße und 8 zugeordnete Integrationstests;
  `find_references` findet beide zentralen `CombineFailures`-Aufrufer.
- Der scoped DRY-Clone-Scan (`production`, `exact`, `minTokens=10`) findet
  0 Cluster bei 308 gescannten Methoden. Der strukturelle Vergleich zeigt
  nur zwei bereits bekannte, fachfremde Kandidaten außerhalb der
  benannten Prozess-Duplikatgruppen (`AssemblyCacheCleanup` bzw.
  `GiteaGitRepositoryTransport`/`ExternalSourceRepositoryTransportResult`).
- Der passende MagicValues-Scan meldet 25 Treffer in 24 eindeutigen
  Einträgen über 9 Prozessdateien; es sind überwiegend bestehende
  Diagnose-/Lokalisierungsstrings, Handle-Bezeichner und der bestehende
  Buffer-Kandidat. Kein unabhängiger Sweep wurde gestartet.
- Der passende DeadCode-Scan meldet 34 Kandidaten, 0 high und 34 low;
  alle Kandidaten sind native ABI-Strukturfelder in
  `ExternalSourceGitProcessNativeMethods.cs` und wurden unverändert
  erhalten. Kein Assembly.Load, keine Reflection und keine
  Privilegienänderung wurden eingeführt.

## Bekannte Unschärfen

- Die Job-Signalisierung ist der Windows-native Baum-Endnachweis; der
  Kritiker sollte insbesondere die SafeHandle-Lifetime rund um
  `DangerousGetHandle()` und die Reihenfolge von Job-Wait, Process-Wait
  und Close prüfen. Der Besitzer bleibt bis zum expliziten Close aktiv.
- Der Close-Fehler ist im Integrationstest kontrolliert simuliert, aber
  das Handle wird vor dem simulierten `false` tatsächlich geschlossen.
  Ein spontan vom Betriebssystem zurückgegebenes CloseHandle-Fehlerbild
  wird nicht gefakt und ist nicht Bestandteil des Tests.
- Der bestehende 1314-Reparse-Skip bleibt capability-bedingt und wurde
  nicht verändert.
