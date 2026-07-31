---
status: done
type: step-result
task: codegraph-mcp
step: 002
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T15:30:00Z
code_commit_hash: 81cf007
status_after: done
blocker_category: n/a
---

# Result Step 002: Resident Server-Zustand: McpCodeGraphServer mit Lazy Staleness-Invalidierung

## Zusammenfassung

`McpCodeGraphServer` (neu, `AiNetLinter.Mcp`) haelt den vom `McpServerCommand`
geladenen `SourceFileCatalog` ueber die Laufzeit des MCP-Servers resident.
`GetCurrentSolution()` prueft unter `lock (_lock)` (`System.Threading.Lock`,
analog `AnalysisCacheManager`) pro bekanntem `Document` zuerst `mtime`, hasht
nur bei Abweichung (`FileChecksumCalculator.ComputeSha256Hex`), und
akkumuliert geaenderte Dateien zu einer einzigen `WithDocumentText`-Solution,
die am Ende in einem Schritt via `SourceFileCatalog.WithUpdatedSolution`
uebernommen wird. `TryLoadSolutionAsync` gibt jetzt `Task<SourceFileCatalog?>`
zurueck statt den Catalog in einem lokalen `using`-Block sofort zu disposen;
`McpServerCommand.RunAsync` haelt ihn ueber `using var mcpState = new
McpCodeGraphServer(catalog, c)` fuer die gesamte `server.RunAsync(ct)`-Laufzeit
offen. `mcpState` wird in diesem Step bewusst von keinem Tool konsumiert
(leeres `ToolCollection` unveraendert aus step-001).

## Geänderte Dateien

- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (neu) — `sealed class
  McpCodeGraphServer : IDisposable`, `IsLoaded`, `GetCurrentSolution()`
  (synchron), privater `Dictionary<string, FileState> _fileState`
  (`FileState` als privates `readonly record struct`), Initial-Hashing im
  Konstruktor, Refresh-Logik in `RefreshStaleDocuments`/`TryRefreshDocument`/
  `TryApplyContentChange` (je ≤60 Zeilen), `Dispose()` reicht an `_catalog`
  durch.
- `src/AiNetLinter/Commands/McpServerCommand.cs` — `TryLoadSolutionAsync`
  liefert jetzt `Task<SourceFileCatalog?>` (Catalog bei Erfolg, `null` bei
  Ladefehler, `[WARN]`-Logging unveraendert); `RunAsync` wrappt das Ergebnis
  in `using var mcpState = new McpCodeGraphServer(catalog, c)` vor dem
  Server-Start.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` —
  `TryLoadSolutionAsync_BrokenSlnx_LogsWarningWithoutThrowing` an die neue
  Signatur angepasst, prueft zusaetzlich `Assert.Null(catalog)`.
- `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerTests.cs` (neu) — alle 6 im
  Step-Plan geforderten Testfaelle (`NotLoaded_ReturnsNull`,
  `NoFileChanges_ReturnsSameSolutionVersion`,
  `FileModifiedOnDisk_ReflectsNewContent`,
  `FileTouchedWithoutContentChange_SkipsSolutionUpdate`,
  `FileDeletedOnDisk_DoesNotThrow`, `ConcurrentCalls_DoNotThrow`).

## Commit

- **Code-Commit-Hash:** `81cf007`
- **Message:**
  ```
  feat(mcp): keep loaded solution resident with lazy staleness check [codegraph-mcp]

  Add McpCodeGraphServer holding the SourceFileCatalog for the MCP server
  lifetime instead of disposing it right after load. GetCurrentSolution()
  lazily checks known documents via mtime/hash and applies incremental
  Solution updates through WithUpdatedSolution, guarded by a Lock analogous
  to AnalysisCacheManager. TryLoadSolutionAsync now returns the catalog
  (or null on load failure) instead of disposing it inline.

  Refs: tasks/codegraph-mcp/step-002
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → grün (0 Warnung(en), 0 Fehler)
dotnet test AiNetLinter.slnx  → grün (1027 Tests, 0 Fehler, davon 6 neu in McpCodeGraphServerTests.cs)
```

## Abweichungen vom Plan

- **`ConcurrentCalls_DoNotThrow`-Test nutzt `Task.WhenAll`/`Task.WhenAny` statt
  `.Result`**: Ein erster Entwurf griff auf `task.Result` zu, um die Resultate
  paralleler `Task.Run(() => server.GetCurrentSolution())`-Aufrufe zu
  assertieren — das schlug gegen die projekteigene `BanBlockingTaskAccess`-Regel
  fehl (`dotnet test` lief zunaechst rot, weil `CliIntegrationTests`, die den
  Linter gegen die eigene Solution laufen lassen, den Verstoss in der eigenen
  Testdatei fanden). Ersetzt durch `await Task.WhenAll(readers)` und
  `Assert.All(results, ...)` — kein Blocking-Access mehr, danach grün. Kein
  Abweichen vom fachlichen Testinhalt aus dem Plan, nur eine Stilkorrektur.
- Ansonsten 1:1 wie geplant umgesetzt (Konstruktor-Signatur, `Lock`,
  Staleness-Reihenfolge mtime→Hash→Content, `WithUpdatedSolution`-Aufruf nur
  bei tatsaechlicher Aenderung, `IsValidDocument`-Wiederverwendung).

## Beobachtungen

- Der interessante Nebeneffekt aus "Abweichungen": `dotnet test` fuehrt als
  Teil der Suite auch `CliIntegrationTests`, die den Linter gegen die eigene
  Solution (inkl. `src/AiNetLinter.Tests/`) laufen lassen — Verstoesse im
  frisch geschriebenen Testcode selbst schlagen dadurch bereits im
  `dotnet test`-Lauf durch, nicht erst in einem separaten Lint-Schritt. Gut
  zu wissen fuer folgende Steps: neuer Testcode muss ebenfalls
  rules-konform sein, nicht nur Produktionscode.
- `McpCodeGraphServer` bleibt in diesem Step ungenutzt vom eigentlichen
  MCP-Tool-Protokoll (wie in "Was du NICHT tun darfst" vorgegeben) — es gibt
  entsprechend noch keinen End-to-End-Nachweis über einen echten
  MCP-Tool-Call, nur die Unit-Tests direkt gegen die Klasse. Das ist laut
  Step-Scope erwartet (EPIC-03 baut darauf auf).

## Bekannte Unschärfen

- `_fileState` hat keinen Eintrag fuer Dateien, die bereits beim
  Konstruktor-Aufruf nicht mehr existierten (`TryCacheInitialFileState`
  ueberspringt sie still). Wird eine solche Datei spaeter doch valide (z. B.
  durch ein Sync-Artefakt), wuerde `TryRefreshDocument` sie beim ersten
  Treffer als "geaendert" behandeln (kein gecachter State → `known` ist
  `default(FileState)`, Hash-Vergleich schlaegt fehl → Content wird
  uebernommen) — das ist im Plan nicht explizit spezifiziert, aber
  konsistent mit "gilt als geaendert, wenn kein gültiger gecachter Zustand
  vorliegt". Kein Fehlverhalten, aber nicht wortwoertlich im Plan
  beschrieben.
- Der "Bekannte Ausnahmen"-Abschnitt des Plans (gelöschte/neue Dateien nicht
  behandelt) ist 1:1 uebernommen — `GetCurrentSolution_FileDeletedOnDisk_DoesNotThrow`
  verifiziert nur "kein Crash, alter Stand bleibt", nicht "Datei wird aus der
  Solution entfernt" (bewusst ausserhalb des Scopes, siehe Step-Plan).

## Falls Status `blocked`

Nicht zutreffend — Status `done (pending audit)`.
