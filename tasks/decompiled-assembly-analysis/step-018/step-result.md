---
status: done
type: step-result
task: decompiled-assembly-analysis
step: "018"
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: "gpt-5 (Codex)"
coded_by_model_knowledge_cutoff: "nicht angegeben"
coded_at: "2026-08-29T06:18:44.0625921+02:00"
code_commit_hash: 2b95b3aac45528b98b2d4201b509c4e4f18928ce
status_after: done
blocker_category: n/a
---

# Result Step 018: Repository-spezifische Capability-Nichtverfügbarkeit zum Decompilation-Fallback

## Zusammenfassung

Der Acquirer klassifiziert ausschließlich den konkreten Windows-Capability-Fall `ERROR_PRIVILEGE_NOT_HELD (1314)` sowie tatsächlich erkannte Reparse-Checkouts als `ProviderUnavailable` mit dem stabilen Code `RepositoryCapabilityUnavailable`. Andere Transport-, Auth-/AccessDenied- und Cancellation-Pfade behalten ihre bestehende Semantik; Transportdiagnosen bleiben normalisiert und geheimnisfrei. Eine Failure-only-Projection über den vorhandenen Provider-/Orchestrator-Vertrag erreicht den bestehenden statischen Decompilation-Fallback, ohne erfolgreiches Snapshot-/Workspace-Wiring vorwegzunehmen.

## Geänderte Dateien

- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs` — ergänzt den stabilen Capability-Diagnostic-Code.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs` — erkennt den konkreten 1314-Fall, projiziert den Code und wahrt die bestehende Fehlerklassifikation.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs` — unterscheidet tatsächliche Reparse-Punkte von nicht möglicher Pfadinspektion.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` — verdrahtet Capability- und Reparse-Failures repository-spezifisch in den Acquirer-Vertrag.
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs` — ergänzt die Failure-only-Projection zum bestehenden ProviderResult.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs` — Regressionen für 1314, Reparse-Semantik, sonstige Fehler, Cancellation und Geheimnisfreiheit.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTestTransport.cs` (neu) — ausgelagerter deterministischer Transport-Testhelfer.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs` — verwendet die zentrale Capability-Erkennung im echten Reparse-Preflight.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs` — prüft Failure-Projection und Geheimnisfreiheit.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs` — prüft die Failure-Projection bis zum statischen Decompilation-Fallback.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTestProviders.cs` (neu) — ausgelagerte deterministische Provider-Testhelfer.

## Commit

- **Code-Commit-Hash:** `2b95b3aac45528b98b2d4201b509c4e4f18928ce`
- **Message:**
  ```
  fix(mcp): Capability-Fallback anbinden [decompiled-assembly-analysis]

  Projiziere den konkreten 1314-/Reparse-Fall repository-spezifisch als ProviderUnavailable und halte andere Fehler unverändert.

  Refs: tasks/decompiled-assembly-analysis/step-018
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ExternalSourceRepositoryAcquirerTests --logger "trx;LogFileName=Step018-Acquirer-after-split.trx" → grün (29 Tests bestanden, 1 übersprungen, 0 Fehler; gesamt 30)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ExternalSourceProviderContractTests --logger "trx;LogFileName=Step018-Provider-after-split.trx" → grün (15 Tests bestanden, 0 übersprungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~AssemblyAnalysisToolSupportTests --logger "trx;LogFileName=Step018-Support-after-split.trx" → grün (15 Tests bestanden, 0 übersprungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --logger "trx;LogFileName=Step018-FastTests-final-after-split.trx" → grün (1969 Tests bestanden, 1 übersprungen, 0 Fehler; gesamt 1970)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --logger "trx;LogFileName=Step018-IntegrationTests-final-after-split.trx" → grün (360 Tests bestanden, 0 übersprungen, 0 Fehler)
```

Stress-Tests wurden nicht ausgeführt. Der einzige Skip betrifft `AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`: Der reale `Directory.CreateSymbolicLink`-Preflight meldet auf diesem Host `ERROR_PRIVILEGE_NOT_HELD (1314)`. Das ist ein transparenter Infrastruktur-Skip und ausdrücklich kein Sicherheitsnachweis; der echte privilegierte Reparse-Nachweis bleibt offen.

## Abweichungen vom Plan

Keine fachliche Abweichung — Plan 1:1 umgesetzt. Die bestehenden Test-Transport-/Provider-Helfer wurden wegen des projektspezifischen MaxLineCount aus den betroffenen Testdateien ausgelagert; erfolgreiches Acquirer-zu-Snapshot-/Workspace-Wiring wurde nicht ergänzt.

## Beobachtungen

- AiNetLinter-MCP: 0 Violations in Produktionsscope und in den betroffenen Testscopes; exakter Produktions-DRY-Scan 0 Cluster bei 225 Methoden; Refactoring-Drift 0 Kandidaten bei 159 Methoden.
- MagicValues meldete 3 bereits vorhandene Lokalisierungskandidaten in 4 Dateien; DeadCode meldete ausschließlich die 2 bekannten Low-Confidence-Befunde `AssemblyOrigin.Kind` und `CreateFromSettings`. Keine davon wurde außerhalb dieses Steps erweitert oder bereinigt.
- Es wurden keine Änderungen an `task-state.md`, `roadmap.md`, `codemap.md` oder `tech-debt.md` vorgenommen. Keine echte Netzwerk-/Git-/Gitea-Ausführung, keine Credentials, keine Privilege-/Systemänderung, kein `Assembly.Load` und keine Reflection.
- Zwei in `.agents/Agent-Scaffolding/AGENTS.md` referenzierte Dateien (`.agents/rules/doku-ist-stand.md` und `verweise-aufloesen.md`) waren im Repository nicht vorhanden; die für diesen Step ausdrücklich verlangten Dateien wurden gelesen.

## Bekannte Unschärfen

Der privilegierte echte Reparse-Test konnte auf dem aktuellen Host nicht ausgeführt werden. Die Capability-Regel und der 1314-Test sind deterministisch abgedeckt; die tatsächliche Reparse-Erkennung wurde im Testpfad nicht privilegiert durchlaufen. Für einen Sicherheitsnachweis muss der unveränderte echte Symlink-Test auf einem Host mit erlaubter Symlink-Capability ohne Skip wiederholt werden.
