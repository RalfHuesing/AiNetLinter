---
status: done
type: step-result
task: codegraph-mcp
step: 005/fix-01
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T15:00:00Z
code_commit_hash: 8db5f4b
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 005/fix-01: Fix: RunGitDiff haengt im echten stdio-MCP-Serverprozess (get_impact Git-Ref-Zweig)

## Zusammenfassung

Beide vom Plan geforderten Massnahmen zusammen umgesetzt: `RunGitDiff`
bekommt `RedirectStandardInput = true` + sofortiges `StandardInput.Close()`
sowie asynchrones Lesen von `StandardOutput`/`StandardError` via
`OutputDataReceived`/`ErrorDataReceived` statt `ReadToEnd()` +
`WaitForExit()`. Analoge Haertung in `GitImpactMiniFixtureWorkspace.RunGit`.
Neue Methode `CommitCalculatorAddBodyChange()` im Fixture (zweiter Commit,
damit `HEAD~1` einen echten Diff liefert). Zwei neue Subprozess-E2E-Tests
in `McpServerCommandTests.cs`, die den Git-Ref-Zweig von `get_impact` ueber
einen echten `AiNetLinter.exe --mcp-server`-Prozess abdecken.

Der Hang wurde vor dem Fix reproduziert (beide neuen Tests liefen exakt in
den 30s-Timeout der Tests und schlugen mit `TaskCanceledException` fehl,
~35s Wandzeit) und ist nach dem Fix behoben (beide Tests gruen, ~7s).

## Geänderte Dateien

- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` — `RunGitDiff`: `RedirectStandardInput` + `StandardInput.Close()`, asynchrones stdout/stderr-Lesen via `OutputDataReceived`/`ErrorDataReceived` + `BeginOutputReadLine`/`BeginErrorReadLine` statt synchronem `ReadToEnd()`. Rueckgabesemantik (`string?`) unveraendert.
- `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` — `RunGit`: identische Haertung; neue Methode `CommitCalculatorAddBodyChange()` (aendert `Calculator.Add`-Body, committet sofort → zweiter Commit fuer `HEAD~1`).
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (neu, 2 Tests) — `RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite` (`gitRef: "HEAD~1"` gegen `GitImpactMiniFixtureWorkspace` nach `CommitCalculatorAddBodyChange()`) und `RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite` (kein `gitRef`, nach `ChangeCalculatorAddBodyWithoutCommitting()`) — beide echte Subprozess-E2E-Tests analog `RunAsync_ValidFixture_FindReferencesReturnsCallSite`.

## Commit

- **Code-Commit-Hash:** `8db5f4b`
- **Message:**
  ```
  fix(core): prevent RunGitDiff subprocess hang under stdio server [codegraph-mcp]

  RunGitDiff redirected stdout/stderr without redirecting stdin and read
  stdout synchronously before waiting for exit -- both are documented
  Process deadlock risks, and together they caused get_impact's git-ref
  branch to hang indefinitely when invoked through a real --mcp-server
  stdio process (reproduced via new subprocess E2E tests, which timed
  out at 30s before this fix and pass in ~7s after). Applied the same
  RedirectStandardInput + async output/error read hardening to the
  analogous GitImpactMiniFixtureWorkspace.RunGit test helper, and added
  a second fixture commit so HEAD~1 resolves to a real diff.

  Refs: tasks/codegraph-mcp/step-005/fix-01
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → gruen, 0 Warnungen
dotnet test AiNetLinter.slnx  → gruen (1051 Tests, 0 Fehler)
ainetlinter --config rules.json --path . → OK, 0 Violations
dotnet run --project src/AiNetLinter -- --path . --impact HEAD~1 -v → korrektes Ergebnis (CLI-Regressionscheck, siehe unten)
```

## Vor/Nach-Nachweis (Hang-Reproduktion)

**Vor dem Fix** (neue Tests hinzugefuegt, `RunGitDiff` noch unveraendert
auf `main`-Stand): gezielter Lauf nur der beiden neuen Tests
(`dotnet test --filter "FullyQualifiedName~RunAsync_ValidFixture_GetImpactWith"`)
— beide schlagen fehl:
```
Bestanden: 0, Fehler: 2
RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite [FAIL] — 35 s — System.Threading.Tasks.TaskCanceledException
RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite [FAIL] — 35 s — System.Threading.Tasks.TaskCanceledException
```
Beide Stacktraces zeigen den Hang in `McpSessionHandler.SendRequestAsync`
(Client wartet auf Antwort, die nie kommt) — exakt das im Plan
beschriebene Symptom, mit ~35s Wandzeit (30s Test-eigener
`CancellationTokenSource` + Overhead), nicht die erwarteten wenigen
Sekunden eines normalen `git diff` auf dem winzigen Fixture-Repo.

**Nach dem Fix** (beide Massnahmen in `RunGitDiff` + `RunGit` angewendet):
derselbe gezielte Lauf, beide gruen:
```
Bestanden: 2, Fehler: 0
RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite [7 s]
RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite [7 s]
```
Anschliessend volle Suite gruen (1051 Tests, s. o.).

## CLI-Regressionscheck

`dotnet run --project src/AiNetLinter -- --path . --impact HEAD~1 -v`
(gegen die echte `AiNetLinter.slnx`, nutzt denselben `RunGitDiff`-Codepfad
ueber `ImpactCommand`) lieferte korrekt eine Aufrufstelle
(`GitImpactMiniFixtureWorkspace.CommitCalculatorAddBodyChange` in
`McpServerCommandTests.cs:212`), keine Regression durch die Umstellung
auf asynchrones Lesen.

## Dogfooding (Abnahmekriterium des Kritikers)

Erneut durchgefuehrt wie in step-005 (Aufruf 2): gebautes
`AiNetLinter.exe` per `StdioClientTransport` als
`--mcp-server --path C:\Daten\Entwicklung\Ralf\AiNetLinter` (echtes
Repo-Root, kein Fixture) gestartet, `get_impact({ gitRef: "HEAD~1" })`
per MCP-Client aufgerufen. Client-Code lag in einer temporaeren, nicht
committeten Testdatei (`_DogfoodTemp.cs`, nach der Verifikation wieder
entfernt — siehe `git status` vor dem Commit, nicht Teil des Diffs).

**Ergebnis:** `IsError` nicht gesetzt (also kein Fehler), Antwort kam
in ~8s zurueck (statt vorher nie/Timeout nach 5 Minuten aus step-005):
```
IsError=
Text=src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs:212 - Aufruf von 'GitImpactMiniFixtureWorkspace.CommitCalculatorAddBodyChange' in Projekt 'AiNetLinter.Tests'
```
Damit ist das CRITICAL-Finding aus dem step-005-Review (Git-Ref-Modus
antwortet im echten Produktions-Aufrufkontext nicht) durch echten
Dogfooding-Nachweis behoben.

## Abweichungen vom Plan

Keine. Beide „Konkreten Änderungen" (Datei 1 + 2) sowie die zwei neuen
Subprozess-Tests (Datei 3) 1:1 wie im Plan skizziert umgesetzt,
einschliesslich des im Code-Skizze-Abschnitt vorgegebenen
`stdoutBuilder.Append(e.Data).Append('\n')`-Musters zur Vermeidung von
`\r\n`-Verunreinigung des `\n`-getrennten Formats.

## Beobachtungen

- Fuer den Dogfooding-Nachweis war eine temporaere, nicht committete
  Testdatei im Test-Projekt notwendig (Wiederverwendung der
  `ModelContextProtocol`-Client-Infrastruktur, die im Test-Projekt bereits
  vorhanden ist, statt eines separaten Scratch-Projekts wie in step-005).
  Datei wurde nach Verifikation geloescht, ist nicht Teil des Commits.
- Keine sonstigen Beobachtungen ausserhalb des Scopes.

## Bekannte Unschärfen

- Der Plan benennt zwei nicht sich ausschliessende Ursachenkandidaten
  (fehlendes `RedirectStandardInput` und synchrones Lesen). Da beide
  Massnahmen zusammen angewendet wurden, ist nicht isoliert nachgewiesen,
  welche der beiden Massnahmen fuer sich allein bereits ausgereicht
  haette — das war laut Plan auch nicht gefordert (siehe „Notes" im Plan:
  "es gibt keinen Grund, nur eine davon umzusetzen").
- Der neue zweite Commit im Fixture (`CommitCalculatorAddBodyChange`)
  aendert den `Add`-Body auf `a + b + 1` (statt `+ 0` wie im
  uncommitted-Fall), rein um von der bestehenden
  `ChangeCalculatorAddBodyWithoutCommitting`-Aenderung unterscheidbar zu
  bleiben, falls beide je in derselben Testklasse kombiniert wuerden —
  aktuell nicht der Fall, aber als bewusste Entscheidung dokumentiert.
