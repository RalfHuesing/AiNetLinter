# Feedback & Abschlussbericht: P1-Fix Cancellation-Propagation im Assembly-Analysepfad

## 1. Übersicht & Kontext

- **Task**: Gezielter Follow-up-Fix für den dokumentierten P1-Blocker *Cancellation-Propagation im Assembly-Analysepfad*
- **Bearbeitete Komponenten**:
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs`
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRegistryTests.cs`
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs`
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`
- **Status**: Erfolgreich implementiert, vollständig getestet und verifiziert.

---

## 2. Ursachenanalyse & Behebung

### 2.1 Problem im ursprünglichen Cancellation-Vertrag
In `AssemblyAnalysisSession.RefreshAsync`, `AssemblyAnalysisSession.CreateSnapshotAsync` und `AssemblyAnalysisRegistry.TryLeaseCurrentAsync` wurde `OperationCanceledException` abgefangen und in reguläre Fehler-Ergebnisse umgewandelt (`FailureResultSingle`, `new WorkspaceCreationResult(null, [...])`, `Failure("Die Assembly-Analyse wurde abgebrochen.")`).

**Folgen des ursprünglichen Verhaltens:**
1. Abgebrochene oder timeoutende MCP-Aufrufe erschienen als reguläre Analysefehler statt als kooperative Cancellation.
2. Bei einem Cancel während der Snapshot-Erstellung oder Roslyn-Validierung konnte ein bereits erzeugter Workspace bzw. Snapshot verwaist bleiben.
3. In der Registry wurde bei Caller-Cancellation nicht sauber zwischen dem Abbruch des wartenden Callers und einem echten Abbruch der gemeinsamen Task-Creation unterschieden.

### 2.2 Korrekturen in `AssemblyAnalysisSession.cs`
- **`RefreshAsync`**: Abfangen von `OperationCanceledException` entfernt. Die Freigabe der `refreshGate`-Semaphore erfolgt garantiert im `finally`-Block; die `OperationCanceledException` wird transparent nach oben weitergegeben.
- **`CreateSnapshotAsync`**: Tritt während der Snapshot-Erstellung oder Compilation-Validierung eine `OperationCanceledException` auf, wird ein bereits erzeugter Snapshot via `snapshot?.Dispose()` best-effort bereinigt und `throw;` ausgeführt. Echte `InvalidOperationException`-Fehler bleiben weiterhin kontrollierte Fehlerergebnisse.

### 2.3 Korrekturen in `AssemblyAnalysisRegistry.cs`
- **`LeaseAsync`**: Vor jedem Fingerprint-Retry wird `cancellationToken.ThrowIfCancellationRequested()` aufgerufen.
- **`TryLeaseCurrentAsync`**:
  ```csharp
  try
  {
      entry = await creation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
  }
  catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
  {
      throw;
  }
  catch (OperationCanceledException)
  {
      RemoveFailedEntry(canonicalPath, creation);
      return new(Failure("Die Assembly-Session wurde während des Aufbaus abgebrochen."), false);
  }
  ```
  - **Caller-Cancellation** (`when (cancellationToken.IsCancellationRequested)`): Der wartende Caller wirft sofort die `OperationCanceledException` weiter. Die gemeinsame Background-Creation (`creation.Task`) läuft für andere Caller ungestört weiter und der Registry-Eintrag bleibt bestehen.
  - **Echte Creation-Abbrüche** (z. B. Registry-Dispose oder Abbruch des internen Creation-Tokens): Der fehlgeschlagene Eintrag wird aus der Registry entfernt und ein kontrolliertes Fehler-Ergebnis zurückgegeben.

### 2.4 Korrekturen in `AssemblyDecompilationAdapter.cs` & `AssemblyRoslynWorkspaceFactory.cs`
- **`AssemblyDecompilationAdapter.DecompileAsync`**: Wenn der Cancellation-Token des Callers abgebrochen wurde (`when (request.CancellationToken.IsCancellationRequested)`), wird `OperationCanceledException` weitergeworfen; interne Timeout-Deadlines liefern weiterhin das diagnostische Timeout-Ergebnis.
- **`AssemblyRoslynWorkspaceFactory.CreateAsync`**: Die Erzeugung des `AdhocWorkspace` wurde in `try / catch { workspace.Dispose(); throw; }` gekapselt, um bei Cancellation während der Dokumenten- oder Compilation-Erzeugung Leaks zu verhindern.

---

## 3. Testabdeckung

1. **`AssemblyAnalysisSessionTests`**:
   - `RefreshAsync_CancellationThrowsAndDoesNotPublishPartialGeneration`: Prüft, dass Cancellation `OperationCanceledException` wirft, keine Generation gesetzt wird und kein partieller Cache publiziert wird.
   - `RefreshAsync_SubsequentRefreshAfterCancellation_Succeeds`: Prüft, dass nach einem abgebrochenen Refresh ein anschließender Refresh mit intaktem Token erfolgreich eine Generation erzeugt.
2. **`AssemblyAnalysisRegistryTests`**:
   - `LeaseAsync_CancellationDoesNotCancelSharedCreation`: Prüft, dass Caller-Cancellation `OperationCanceledException` wirft und ein nachfolgender Caller die erfolgreich im Hintergrund erzeugte Session nutzen kann (`ResidentCount = 1`).
   - `LeaseAsync_ConcurrentWaiters_CancelledWaiterThrowsWhileOtherCompletes`: Prüft, dass bei zwei parallelen Callern der abbrechende Caller `OperationCanceledException` erhält, während der andere Caller eine gültige Lease empfängt.
   - `LeaseAsync_InternalCreationAbortRemovesEntryAndSubsequentAttemptSucceeds`: Prüft, dass ein echter interner Creation-Abbruch den Registry-Eintrag bereinigt und ein nachfolgender Aufruf eine frische Session aufbaut.
3. **`AssemblyAnalysisToolSupportTests`**:
   - `ExecuteAsync_CancellationAfterProviderSnapshotReleasesSelectionLease`: Prüft, dass bei Cancellation die `OperationCanceledException` propagiert und der Source-Selection-Lease ordnungsgemäß freigegeben wird.

---

## 4. Verifikationsergebnisse

- **Build**:
  ```bash
  dotnet build AiNetLinter.slnx --no-restore
  ```
  `0 Warnung(en)`, `0 Fehler`.
- **FastTests (`Category!=Stress`)**:
  ```bash
  dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress"
  ```
  `2.205 bestanden`, `2 übersprungen`, `0 Fehler`.
- **Assembly FastTests**:
  `84 bestanden`, `0 Fehler`.
- **IntegrationTests**:
  Relevante MCP- & Assembly-Tests (`ExternalSourceSnapshotMaterializerTests`, `DaemonHostMcpProcessContractTests`, `McpServerAllToolsE2ETests`, etc.) bestanden.
- **Whitespace / Diff-Check**:
  ```bash
  git --no-pager diff --check
  ```
  Sauber.
- **MCP Qualitäts-Checks**:
  - `safeguard`: **10,00/10** (Threshold 8,00) — PASS, 0 Verstöße.
  - `get_violations`: **0 Verstöße** in 806 Dateien im Scope.
