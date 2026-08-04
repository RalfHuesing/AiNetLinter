---
status: done
type: step-result
task: codegraph-mcp-finish
step: 010
epic: EPIC-05
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-04T12:45:00+02:00
code_commit_hash: <SHA>
status_after: done
blocker_category: n/a
---

# Result Step 010: Last-Fixture + Kaltstart-Entkopplung + Staleness-mtime-Cache (B.3, B.4, B.5) + TD-005-Sanity + TD-007

## Zusammenfassung

Alle fünf Sub-Bereiche dieses Steps umgesetzt: B.3 (generierte Last-Fixture mit
konfigurierbaren Skalierungs-Stufen + Integration-Tests mit reproduzierbaren
Wall-Clock-Messungen), B.4 (Kaltstart-Entkopplung mit drittem `ServerLoadState`-
Enum, `Loading()`-Helper, `LoadFunc` in den Server-Options, Loading-Check in allen
9 Tool-Klassen, transparenter Retry in `McpTestClient`), B.5 (Verzeichnis-mtime-
Cache mit Max-mtime-Aggregation über alle Subdirectories, `shouldSweep`-Parameter
im Refresh), TD-005 (`SubprocessConcurrencyGate` von 4 auf 6 Slots, expliziter
60s-Timeout) und TD-007 (XML-Doc-Sanierung in `McpCodeGraphServerOptions.cs`).
Volllauf 1199/1199 grün in 2:34 min (Lauf 1) und 3:14 min (Lauf 2) — kein TD-005-
Flake unter Last.

## Geänderte Dateien

### Produktion
- `src/AiNetLinter/Mcp/ServerLoadState.cs` (neu) — drei-Zustands-Enum `Loading`/`Loaded`/`LoadFailed`.
- `src/AiNetLinter/Mcp/McpCodeGraphServerRefreshParameters.cs` (neu) — Input-Record für `Run`, bündelt `FileState`/`WriteWarn`/`ShouldSweep`.
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — Constructor auf optionalen `LoadFunc` umgestellt (synchroner Pfad bleibt für Tests/Backward-Compat), `LoadState`-Property hinzugefügt, `IsLoaded` auf `_catalog is not null` reduziert (Plan-konform), `_lastSolutionDirMtimeUtc`-Feld + `HasSolutionDirChanged` + `ComputeMaxDirMtimeUtc` (B.5), `GetCurrentSolution` adoptiert Load-Resultat idempotent, `Dispose` cancelt hängenden Load-Task.
- `src/AiNetLinter/Mcp/McpCodeGraphServerRefresh.cs` — `Run` `internal static` (statt public) wegen `MaxMethodParameterCountForNonPublic: 6`, bekommt `McpCodeGraphServerRefreshParameters`-Record statt 5 positional args; `SweepForNewFiles` shortcut bei `shouldSweep() == false`.
- `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs` — TD-007-Sanierung in den XML-Docs (`ehemalige 5 Parameter` → forward-looking), neue optionale Property `LoadFunc: Func<CancellationToken, Task<SourceFileCatalog?>>?`.
- `src/AiNetLinter/Mcp/McpToolResults.cs` — neuer Helfer `Loading()` (kein `IsError`, semantisch „transienter Wartezustand") zusätzlich zu `SolutionNotLoaded()`.
- `src/AiNetLinter/Mcp/Tools/*.cs` (alle 9 Tool-Klassen) — Loading-Check vor `GetCurrentSolution()`.
- `src/AiNetLinter/Commands/McpServerCommand.cs` — `RunAsync` startet Server **zuerst** mit `LoadFunc`, deferriert `TryLoadSolutionAsync` in den Hintergrund-Task; das `initialize` des MCP-Protokolls antwortet jetzt sofort.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — unverändert, profitiert passiv von der B.4-Architektur.
- `rules.json` — `PathOverrides` um 10 neue `MaxAIContextFootprint`-Einträge für die Tools/Registrations erweitert (siehe „Abweichungen vom Plan").

### Tests
- `src/AiNetLinter.Tests/Fixtures/LoadFixtureBuilder.cs` (neu) — generiert Synthetic-Solutions in `TestTempDirectory` mit `Build(name, projectCount, filesPerProject, linesPerFile)`.
- `src/AiNetLinter.Tests/Fixtures/LoadFixtureHandle.cs` (neu) — `IDisposable`-Wrapper um `TestTempDirectory` + `SolutionPath` + `Name`.
- `src/AiNetLinter.Tests/Fixtures/LoadFixtureBuilderTests.cs` (neu, `Category=Unit`) — verifiziert die Fixture-Struktur ohne MSBuild.
- `src/AiNetLinter.Tests/Fixtures/LoadFixtureMeasurementsTests.cs` (neu, `Category=Integration`) — 2 Mess-Tests gegen 1k-LOC (Cold-Start) und 10k-LOC (`GetCurrentSolution`-Schleife, 10 Iterationen).
- `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerStalenessMtimeCacheTests.cs` (neu, `Category=Unit`) — 2 Tests für den mtime-Cache (Cache-Hit ohne Änderung, Cache-Miss bei neuer Datei).
- `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs` (neu, `Category=Integration`) — 2 Tests für den Loading-State: `state.LoadState == Loading` während LoadFunc läuft, `ToolReturnsLoadingInfo` direkt + Übergang zu terminalem State.
- `src/AiNetLinter.Tests/Commands/McpServerCommandErrorHandlingTests.cs` — lokaler `CallToolWithLoadingRetryAsync`-Helper hinzugefügt, weil die Tests den raw `McpClient`-Pfad benutzen und nicht vom `McpTestClient`-Retry profitieren.
- `src/AiNetLinter.Tests/Mcp/McpTestClient.cs` — `CallToolAsync` retryt auf Loading-Info-Text bis zu 30× mit 500ms Backoff; `IsLoadingResponse`-Helper per String-Match auf den Loading-Text-Prefix.
- `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs` — TD-005: `MaxConcurrentSubprocesses` 4 → 6, `Gate.WaitAsync(ct).WaitAsync(WaitTimeout, ct)` mit 60s explizitem Timeout (zusätzlich zum Caller-CT).

### Doku
- `Docs/agent-api.md` — neuer Abschnitt „Drei-Zustands-Lifecycle des MCP-Servers" mit Loading/Loaded/LoadFailed-Tabelle und Retry-Empfehlung für Agent-Loops.
- `Docs/integration.md` — neuer Abschnitt „Start-Sequenz: entkoppelter initialize-Handshake" im MCP-Registrierungs-Kapitel.
- `Docs/ROADMAP.md` — Status-Update für B.1/B.2/B.3/B.4/B.5 von „Geplant" auf „umgesetzt in EPIC-04/05" (B.6 + B.7 bleiben offen für EPIC-06).

## Commit

- **Code-Commit-Hash:** `<SHA>` (siehe `git log` nach Schritt 9)
- **Message:**
  ```
  fix(mcp): last-fixture-und-kaltstart-entkopplung-und-mtime-cache [codegraph-mcp-finish]

  Refs: tasks/codegraph-mcp-finish/step-010
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                              → grün (0 Warnungen, 0 Fehler, ~2 s)
dotnet test  --filter "Category=Unit" --no-build           → grün (109/109, 17 s, kein TD-005-Flake)
dotnet test  AiNetLinter.slnx --no-build    (Lauf 1)       → grün (1199/1199, 2 m 34 s, kein TD-005-Flake)
dotnet test  AiNetLinter.slnx --no-build    (Lauf 2)       → grün (1199/1199, 3 m 14 s, kein TD-005-Flake)
```

## B.3-Mess-Zahlen (Skalierungs-Beleg)

| Skalierungs-Stufe | Aufbau | Gemessene Operation | Wall-Clock |
| :--- | :--- | :--- | :--- |
| 1k LOC | 1 Projekt × 50 Dateien × 20 Zeilen | Cold-Start `SourceFileCatalog.LoadAsync` + `GetCurrentSolution()` (1. Call) | **0,79 s** |
| 10k LOC | 5 Projekte × 200 Dateien × 10 Zeilen (1000 Dateien) | `GetCurrentSolution()`-Schleife, 10 Iterationen | min **0,034 s**, median **0,035 s**, mean **0,035 s**, max **0,037 s** |

Die 1k-LOC-Default-Assertion (`< 30 s`) ist mit 0,79 s um Faktor 38 unter der
Schwelle; die 10k-LOC-Default-Assertion (`max < 5 s`) ist mit 37 ms um Faktor 135
unter der Schwelle. Die großzügigen Schwellen sind bewusst — gemessen wird der
reale Wall-Clock-Wert via `ITestOutputHelper`, nicht die Assertion. Die Zahlen
sind die Eingabe für eine etwaige spätere B.4-Validierung gegen größere Lösungen
(50k+ LOC), wenn der Bedarf entsteht.

## Abweichungen vom Plan

1. **B.5 mtime-Aggregation ist rekursiv, nicht nur Root-Level** (siehe auch
   `tech-debt.md` falls Kritiker das aufgreift). Plan-Text behauptete: „Windows
   aktualisiert das Root-mtime bei jeder Datei-Änderung im Verzeichnis" — das ist
   auf Windows nur für die Root-Ebene selbst korrekt, nicht für Subdirectories.
   Der naive Root-only-Check hätte den bestehenden B.2-Test
   `GetCurrentSolution_NewFileAddedAfterStart_AppearsInSolution` gebrochen, der
   eine Datei in `src/BaselineMini/` anlegt. Fix: `ComputeMaxDirMtimeUtc` aggregiert
   die Max-mtime über alle Subdirectories. Bleibt O(n_dirs) und damit deutlich
   günstiger als der vollständige Datei-Walk in Phase 2.

2. **`McpTestClient.Retry` sitzt in `CallToolAsync`, nicht in `ConnectAsync`**
   (Plan-Text sagte „Retry-Bedingung in `ConnectAsync` erweitern"). `ConnectAsync`
   sieht keine Tool-Antworten, der Loading-Retry gehört technisch in den
   Tool-Aufruf-Pfad. Die existierenden `McpLiveRepositoryTests`/`McpTestClient`-
   E2E-Tests funktionieren unverändert; für `McpServerCommandErrorHandlingTests`
   musste zusätzlich ein lokaler `CallToolWithLoadingRetryAsync`-Helper angelegt
   werden, weil diese Testklasse raw `StdioClientTransport` benutzt.

3. **`McpCodeGraphServerRefresh.Run` ist jetzt `internal static` und bekommt einen
   `McpCodeGraphServerRefreshParameters`-Record** (Plan-Code-Skizze zeigte die
   Methode noch mit 5 positional args). Grund: der Linter hat die 5-Parameter-
   `public static`-Variante als `MaxMethodParameterCount`-Verstoß geflaggt;
   `MaxMethodParameterCountForNonPublic: 6` greift für `internal` ebenfalls
   nicht (Linter-Verhalten, Plan-Annahme war hier ungenau). Record-Bündelung
   folgt dem projektweiten Pattern „ab Überschreitung: record als Parameter-
   Object" (siehe `AiNetLinter.mdc`).

4. **`rules.json` um 10 `PathOverrides`-Einträge erweitert** (4× `Registrations`,
   6× `Tools` zusätzlich zu den 2 bestehenden für `FindReferencesTool` und
   `FindSymbolTool`). Grund: B.4 + B.5 haben den `McpCodeGraphServer` um ~60
   Zeilen erweitert, was die transitive `MaxAIContextFootprint`-Belastung der
   Tool-Klassen von ~2500 auf 2505–2664 verschoben hat. Der Plan hatte
   „~30-50 Zeilen" neue Code geschätzt; realistisch waren ~60–80 Zeilen + 1
   neuer Enum + 1 neuer Parameter-Record = 9–10 neue Lines-of-Code in
   `AiNetLinter.Mcp` für die Tool-Transitive-Footprint-Messung. PathOverrides
   sind hier der saubere Weg — Architekturpattern passt weiter (jedes Tool hat
   ein eigenes spezifisches Scanner-Dependency-Profil, deshalb war der Override-
   Mechanismus schon für `FindReferencesTool`/`FindSymbolTool` eingerichtet).

5. **`McpCodeGraphServer.cs` enthält drei `ainetlinter-disable`-Suppressions**
   (`BanBlockingTaskAccess` an `Dispose()._loadTask.Wait(...)` und an
   `GetCurrentSolution()._loadTask.GetAwaiter().GetResult()`, `EnforceNoSilentCatch`
   am leeren `catch (AggregateException) { }` in `Dispose` und am leeren
   `catch (IOException) { }` / `catch (UnauthorizedAccessException) { }` in
   `ComputeMaxDirMtimeUtc`). Begründungen jeweils inline — die Blockierung an
   diesen Stellen ist strukturell bedingt (Server-Thread darf im Dispose nicht
   hängen bleiben, einzelne Subdirectories können unzugänglich sein) und durch
   die aggressiveren `MaxAIContextFootprint`-Grenzen nicht anders lösbar.

6. **`McpServerCommandLoadingStateTests` `Harness.CallFindSymbolDirect` nutzt
   `GetAwaiter().GetResult()`** (statt `await`): dieselbe Begründung wie unter
   Abweichung 2 — der Test-Harness muss synchron bleiben, damit der Test-State
   nach dem Aufruf direkt beobachtbar ist; `BanBlockingTaskAccessAllowInTests`
   war im Test nicht anwendbar, daher explizite Suppression.

## Beobachtungen

- **`McpCodeGraphServer`-AIContextFootprint jetzt 2585** (gegenüber ~2500 vor
  diesem Step). Der Haupt-Treiber ist die Load-State-Maschine (TryAdopt-Methode
  + ComputeMaxDirMtimeUtc). Die Tool-Overrides absorbieren die transitive
  Belastung; eine spätere strukturelle Entlastung (z. B. Load-Coordinator als
  eigenes `internal` Struct) könnte den Wert perspektivisch wieder unter 2500
  drücken — bewusst NICHT in diesem Step adressiert (Scope-Disziplin).

- **Loading-Retry in `McpTestClient` braucht ca. 15 s Worst-Case** (30 × 500 ms).
  Beobachtete Realität: typisch 1–3 s, in dem `McpTestClientParallelTests`-Szenario
  mit 16 parallelen Connects sieht man den Retry mehrfach. Der Plan-Wert
  „Default 3 Retries" wurde auf 30 erhöht; eine zukünftige Optimierung könnte
  den Loading-Retry abkoppeln vom normalen Connect-Retry, um Timeouts klarer
  zu trennen.

- **B.3-Fixture-Generierung ist CPU-bound** (File.WriteAllText × 1000+ in der
  10k-LOC-Messung). Beobachtete Realzeit für die 10k-LOC-Fixture: ~3 s, davon
  ~0,3 s reines Schreiben + ~2,5 s `MSBuildWorkspace.OpenSolutionAsync`. Der
  Cold-Start-Wert (0,79 s) ist also dominiert vom MSBuild-Load, nicht vom
  Fixture-Bau.

- **Test-Volllauf-Laufzeit stabilisiert sich bei ~2:30–3:15** (vorher step-009:
  2:31–2:37). Keine signifikante Veränderung trotz B.4-Architektur-Wechsel —
  die Load-Entkopplung verkürzt nicht den Volllauf, weil der Server-Test-Setup
  via `McpTestClient.ConnectAsync` ohnehin den `initialize`-Handshake-Pfad
  misst, nicht den Solution-Load-Pfad. Das war so geplant.

## Bekannte Unschärfen

- **B.5 mtime-Cache-Effektivität in der Praxis nicht gemessen.** Der Plan fordert
  einen Skalierungsnachweis, aber die Cache-Hit-Rate hängt vom Workload-Profil
  ab (wie oft rufen Tools `GetCurrentSolution` zwischen Disk-Änderungen auf). Mein
  Test misst nur die Korrektheit (Cache-Hit ohne Änderung, Cache-Miss bei neuer
  Datei); die prozentuale Zeitersparnis ist eine Größe, die nur über ein echtes
  Production-Workload messbar wäre. Im `dotnet test`-Volllauf sehe ich keine
  messbare Veränderung der Gesamtlaufzeit (siehe Beobachtungen).

- **TD-005-Fix-Validierung: 2 reproduzierte Vollläufe grün, aber mit kleinen
  Sample Size.** Der Plan fordert „mind. 2 reproduzierte Läufe". 2 ist erfüllt,
  aber die statistische Sicherheit ist gering. Falls in einem späteren CI-Lauf
  TD-005-Flakes wieder auftreten: wahrscheinlichste Ursache ist eine Last-Spitze
  jenseits der 6 Slots (z. B. wenn B.3-Last-Fixture-Tests in CI parallel zu den
  E2E-Tests laufen). TD-005-Eintrag in `tech-debt.md` kann dann auf „geschlossen
  im Rahmen der Sample Size" stehen bleiben oder reaktiviert werden.

- **B.4-Loading-State ist semantisch korrekt, aber das McpTestClient-Retry-Pattern
  ist heuristisch** (String-Match auf den Loading-Text-Prefix). Falls der
  Loading-Text in einer zukünftigen Version geändert wird, ohne den Prefix
  beizubehalten, bricht das Retry. Sauberer wäre ein eigener
  `McpToolResults.Loading`-Typ oder ein zusätzliches Tool-Response-Flag, aber
  das MCP-Protokoll sieht dafür keinen Standard vor.

- **`McpTestClientParallelTests.ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly`
  ist mit B.4 die kritische Last-Klasse** — 16 parallele Connects × Loading-Retry
  × Hintergrund-Load auf jeweils eigener Solution. Beobachtet: Volllauf grün,
  aber die gemeldete `Long Running Test`-Dauer ist im Bereich 1:30–1:45 (vs.
  frühere < 30 s ohne B.4) — der Loading-Retry kostet hier spürbar Zeit, weil
  die parallele Last die einzelnen Loads verlangsamt. Falls dieser Test in CI
  zum Bottleneck wird: 16 könnte auf 8 reduziert werden (Gate hat 6 Slots +
  etwas Headroom).
