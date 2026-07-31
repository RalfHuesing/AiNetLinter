---
status: done
type: step-review
task: codegraph-mcp
step: 005/fix-01
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T15:20:00Z
verdict: approved
tech_debt_ids: []
---

# Review Step 005/fix-01: Fix: RunGitDiff haengt im echten stdio-MCP-Serverprozess (get_impact Git-Ref-Zweig)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

Alle drei Dateien wie im Plan skizziert geändert (`RedirectStandardInput` +
`StandardInput.Close()`, asynchrones `OutputDataReceived`/`ErrorDataReceived`-Lesen
statt `ReadToEnd()`+`WaitForExit()`, zwei neue Subprozess-E2E-Tests) — 1:1 gegen
`git show 8db5f4b` geprüft, keine Abweichung vom Code-Skizze-Abschnitt des Plans.
Rules-Konformität eingehalten (0 Warnungen, kein neuer Verstoß). Logisch korrekt:
kein neuer Deadlock/Race eingeführt (siehe unten). Konzept-Treue wiederhergestellt:
der Git-Ref-Modus von `get_impact` antwortet jetzt im einzigen realen Produktions-
Aufrufkontext (echter stdio-`--mcp-server`-Subprozess).

### Eigene Verifikation des Hang/Fix-Nachweises (kritischster Punkt)

Nicht nur den Coder-Bericht übernommen, sondern selbst reproduziert: `DiffImpactAnalyzer.cs`
per `git checkout 8db5f4b~1 -- src/AiNetLinter/Core/DiffImpactAnalyzer.cs` gezielt auf den
Vorher-Stand zurückgesetzt (Test-Dateien blieben auf dem Fix-Stand), Build grün, dann
`dotnet test --filter "FullyQualifiedName~RunAsync_ValidFixture_GetImpactWith"` ausgeführt:
beide neuen Subprozess-Tests schlagen reproduzierbar mit `TaskCanceledException` nach ~35s
fehl (Stacktrace zeigt Hang in `McpSessionHandler.SendRequestAsync`) — exakt wie im
`step-result.md` behauptet. Anschließend die Datei per `git checkout HEAD -- ...`
wiederhergestellt (Arbeitsbaum danach `git status` sauber bestätigt), Build erneut grün,
derselbe gezielte Testlauf jetzt grün in ~15s Gesamtdauer (zwei Tests), danach volle Suite
(`dotnet test AiNetLinter.slnx`) grün mit 1051 Tests — deckungsgleich mit dem Coder-Bericht.
Zusätzlich eigenes, unabhängiges Dogfooding durchgeführt: echte `AiNetLinter.exe` gebaut,
über `StdioClientTransport`/`McpClient` als `--mcp-server --path <echtes-Repo-Root>` gestartet,
`get_impact({ gitRef: "HEAD~1" })` aufgerufen (temporäre, nicht committete Testdatei im
Testprojekt, nach Verifikation gelöscht, `git status` danach sauber) — Antwort kam in ~6s
zurück, kein Timeout. Damit ist die Behebung unabhängig bestätigt, nicht nur plausibel.

### Korrektheit des Fix-Musters (keine neuen Race Conditions/Deadlocks)

`RedirectStandardInput = true` + sofortiges `StandardInput.Close()` unmittelbar nach
`Process.Start` entkoppelt den Kindprozess-Stdin zuverlässig von der äußeren JSON-RPC-Pipe.
`BeginOutputReadLine`/`BeginErrorReadLine` + anschließendes parameterloses `WaitForExit()`
ist exakt das von Microsoft dokumentierte Standardmuster gegen die Doppel-Redirect-Deadlock-
Klasse; das parameterlose `WaitForExit()` wartet zusätzlich auf den Abschluss der
asynchronen Stream-Reads. `stdout`/`stderr` sind getrennte `StringBuilder`-Instanzen, auf die
je nur der jeweils eigene Event-Handler schreibt (`OutputDataReceived` und `ErrorDataReceived`
laufen nie gegeneinander auf demselben Builder) — kein Data-Race. Das `\n`-Anhängen statt
`AppendLine` (verhindert `\r\n`-Verunreinigung unter Windows) ist wie im Plan vorgegeben
umgesetzt und hält `ParseGitDiffHunks`s zeilenbasiertes Parsing intakt (durch grüne
`DiffImpactAnalyzerTests` bestätigt). Identische Härtung 1:1 im analogen `GitImpactMiniFixtureWorkspace.RunGit`
angewendet, inkl. jetzt tatsächlich gelesenem (vorher nie gelesenem) `stdout` dieses Helfers.

### Scope-Disziplin

`AnalyzeAsync`, `ParseGitDiffHunks` und der Rest der Analyselogik in `DiffImpactAnalyzer.cs`
unangetastet (per Diff und vollständigem Re-Read der Datei bestätigt) — der Fix beschränkt
sich exakt wie gefordert auf den Prozessstart-Mechanismus in `RunGitDiff`. `GetImpactTool.cs`
nicht Teil des Commits.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen (eigener Nachlauf)
dotnet test AiNetLinter.slnx  → grün (1051 Tests, 0 Fehler, eigener Nachlauf)
Selbst-Lint (ainetlinter --config rules.json --path .) → OK, 0 Violations (eigener Nachlauf)
CLI-Regressionscheck (--impact HEAD~1 -v) → kehrt sofort zurück, kein Hang (eigener Nachlauf)
Eigenes Dogfooding (get_impact/gitRef=HEAD~1 über echten --mcp-server-Subprozess) → Antwort in ~6s, kein Timeout
```
