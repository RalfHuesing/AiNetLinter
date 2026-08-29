---
status: blocked
type: step-result
task: decompiled-assembly-analysis
step: 017
corrects: step-016
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-29T02:46:00+02:00
code_commit_hash: 5d48472c4504f17cddf3c086fe105f1c0db60808
status_after: blocked
blocker_category: infrastructure
---

# Result Step 017: Cancellation-Cleanup beobachten und Reparse-Test privilegienbewusst ausführen

## Zusammenfassung

Der Cancellation-Pfad des Acquirers wertet fehlgeschlagenes Cleanup aus und
schreibt über einen optionalen internen Serilog-Seam genau eine feste,
geheimnisfreie `RepositoryCleanupFailed`-Warnung. Die ursprüngliche
`OperationCanceledException` einschließlich ihres CancellationTokens wird
weiterhin unverändert weitergereicht; eine direkte Regression prüft außerdem,
dass der verlorene Checkout nicht gelöscht wird.

Der echte Reparse-Testkörper blieb unverändert und erhält ausschließlich einen
test-only Preflight für Directory-Symlink-Fähigkeit. Der aktuelle Host meldet
`ERROR_PRIVILEGE_NOT_HELD` (1314), daher ist der Runner grün, aber der echte
privilegierte Reparse-Sicherheitsnachweis nicht erbracht.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` — ergänzt den instanzlokalen Logger-Seam und beobachtet fehlgeschlagenes Cancellation-Cleanup vor dem unveränderten `throw;`.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCancellationTests.cs` (neu) — prüft Cleanup-Fehlerlogging, unveränderte Cancellation und den unangetasteten Checkout.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs` (neu) — stellt den lokalen Log-Sink und den begrenzten echten Symlink-Capability-Preflight bereit.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs` — ruft den Preflight am Anfang der echten Reparse-Regression auf und behält deren Testkörper bei.

## Commit

- **Code-Commit-Hash:** `5d48472c4504f17cddf3c086fe105f1c0db60808`
- **Message:**
  ```
  fix(mcp): Cancellation-Cleanup beobachten [decompiled-assembly-analysis]

  Mache fehlgeschlagenes Cleanup bei echter Cancellation sichtbar und halte den ursprünglichen Abbruchvertrag unverändert. Ergänze den begrenzten Reparse-Capability-Preflight mit direkter Regression.

  Refs: tasks/decompiled-assembly-analysis/step-017
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit nach diesem Ergebnis.

## Build-/Test-Output

```text
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCancellationTests" --logger "trx;LogFileName=Step017-Cancellation.trx" → grün (1 Test, 0 Fehler, 0 übersprungen)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryAcquirerTests" --logger "trx;LogFileName=Step017-Acquirer-final.trx" → grün (28 bestanden, 1 übersprungen, 29 gesamt, 0 Fehler)
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress" --logger "trx;LogFileName=Step017-FastTests-final.trx" → grün (1966 bestanden, 1 übersprungen, 1967 gesamt, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter "Category!=Stress" --logger "trx;LogFileName=Step017-IntegrationTests-final.trx" → grün (360 bestanden, 0 übersprungen, 360 gesamt, 0 Fehler)
```

Stress-Tests wurden nicht ausgeführt. Der eine fokussierte Skip und der eine
FastTests-Skip betreffen ausschließlich
`AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`.
Die TRX-Ausgabe nennt `ERROR_PRIVILEGE_NOT_HELD (1314)` und ausdrücklich,
dass kein Sicherheitsnachweis erbracht wurde. Ein privilegierter Lauf ohne
Skip steht noch aus.

## Abweichungen vom Plan

Der fachliche Code- und Testumfang wurde planmäßig umgesetzt. Der Status bleibt
wegen der fehlenden lokalen Symlink-Berechtigung `blocked` statt eines
abnahmefähigen Abschlussstatus. Um den unvermeidlichen zusätzlichen
Preflight-Aufruf innerhalb des bestehenden Testdatei-Limits zu halten, wurde
eine überflüssige Leerzeile in derselben Testdatei entfernt; Testlogik und der
echte Reparse-Testkörper blieben unverändert.

`task-state.md`, `roadmap.md`, `codemap.md` und `tech-debt.md` wurden nicht
geändert.

## Beobachtungen

- Die gezielte AiNetLinter-MCP-Prüfung mit absolutem `projectRoot` meldet
  0 Violations im Produktionsscope `src/AiNetLinter/Mcp/Assemblies`.
- Der Exact-DRY-Check mit `minTokens=1` und
  `similarityThreshold=exact` findet 0 Duplikat-Cluster bei 214
  gescannten Methoden.
- Der Magic-Value-Check meldet 69 bestehende Einzelkandidaten in 26
  Dateien; im unmittelbar geänderten Acquirer wurde kein neuer relevanter
  Sicherheitskandidat zurückgelassen. Es wurde kein unabhängiger Sweep
  gestartet.
- Der High-Confidence-Dead-Code-Check findet 0 unreferenzierte Symbole
  im Produktionsscope.
- `TD-001` bis `TD-003` wurden nicht ausgeweitet. Netzwerk, Git,
  Credentials, Refresh, Cache, Snapshot, Workspace-Wiring,
  Assembly-Loading und Reflection blieben unberührt.

## Bekannte Unschärfen

Der lokale Host kann Directory-Symlinks nur mit `ERROR_PRIVILEGE_NOT_HELD`
(1314) ablehnen. Deshalb wurde die produktive Reparse-Prüfung samt externem
Sentinel in diesem Lauf nicht erreicht. Der Skip ist nur ein
Capability-Ausführungsstatus und kein Sicherheitsnachweis. Der fokussierte
Acquirer-Test und das vollständige FastTests-Nicht-Stress-Gate müssen unter
aktiviertem Developer Mode oder vorhandener
`SeCreateSymbolicLinkPrivilege` erneut laufen; dann darf derselbe Test keinen
Skip melden und muss bestehen.

## Falls Status `blocked`

**Blocker-Art:** `infrastructure`

**Blockiert weil:** Die aktuelle Windows-Testumgebung besitzt nicht die
erforderliche Berechtigung für den echten Directory-Symlink. Der zulässige
Capability-Preflight überspringt ausschließlich den fest erkannten Fehler
`ERROR_PRIVILEGE_NOT_HELD` (1314); ein privilegierter Sicherheitsnachweis ist
dadurch nicht entstanden.

**Brauche von Nutzer:** Einen Lauf des fokussierten Acquirer-Tests und des
FastTests-Nicht-Stress-Gates unter einer berechtigten Windows-Umgebung, ohne
System- oder Privilegienänderung durch diesen Step.

**Aktueller Stand:** Produktionskorrektur und direkte Regression sind im
Code-Commit gesichert. Build, fokussierter Test, FastTests-Nicht-Stress und
IntegrationTests-Nicht-Stress sind im aktuellen Runner grün; der Reparse-
Nachweis bleibt bis zum privilegierten Lauf offen.
