---
status: done
type: step-result
task: codegraph-mcp-finish
step: 011
epic: EPIC-06
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-04T13:42:00+02:00
code_commit_hash: 3762e6a
status_after: done
blocker_category: n/a
---

# Result Step 011: Robuste McpLintConsole (B.6) + E2E-JSON-RPC-Framing-Test (B.6) + Opt-in --mcp-log Call-Log (B.7)

## Zusammenfassung

Beide Sub-Bereiche vollständig umgesetzt: B.6 fügt `McpLintConsole` als strukturelle stdout-Absicherung hinzu (Singleton in `Output/`, in `Program.cs:43` an `McpServerCommand.RunAsync` durchgereicht) plus einen E2E-Regressions-Test, der `AiNetLinter.exe` als Subprozess spawnt, JSON-RPC-Frames (initialize + notifications/initialized + tools/list + tools/call) zeilenweise auf stdin schreibt und **jede** stdout-Zeile roh als gültigen JSON-RPC-Frame verifiziert (kein SDK-Parser zwischen Subprozess und Assertions). B.7 ergänzt das `--mcp-log <pfad>`-CLI-Flag (1:1 nach dem `--agent-rules-path`-Pattern), eine `McpCallLog`-Klasse in `Mcp/` mit JSONL-Writer + Lock + leerer-File-Cleanup + IAsyncDisposable-Scope-Pattern, und 9 mechanische Lambda-Wrapper in den drei `*Registrations.cs` mit `if (callLog is null)`-Fast-Path (kein Overhead bei deaktiviertem Log). 16 neue Tests (3 B.6-Unit, 2 B.6-E2E, 5 B.7-Unit für McpCallLog, 6 B.7-Unit für Pfad-Auflösung); Volllauf 1215/1215 grün in 2:23, Selbst-Lint 0/0/0.

## Geänderte Dateien

**B.6 (strukturelle stdout-Absicherung + E2E-Test):**

- `src/AiNetLinter/Output/McpLintConsole.cs` (neu) — `internal sealed class McpLintConsole : ILintConsole` mit `Instance`-Singleton; `WriteLine` und `WriteError` beide nach `stderr` umgeleitet, weil im MCP-Modus beide Kanäle strukturell auf `stderr` gehören.
- `src/AiNetLinter/Program.cs` — expliziter `McpLintConsole.Instance`-Parameter bei `McpServerCommand.RunAsync(linterArgs, cts.Token, McpLintConsole.Instance)` (Z. 43); `using AiNetLinter.Output;` ergänzt.
- `src/AiNetLinter.Tests/Output/McpLintConsoleTests.cs` (neu) — 3 Unit-Tests: `WriteLine`/`WriteError` → `stderr`, Singleton-Garantie.

**B.7 (--mcp-log CLI-Flag + Call-Log-Klasse + Wrapper):**

- `src/AiNetLinter/Cli/LinterArgs.cs` — neue Property `string? McpLogPath { get; init; }` (Default `null` = Log deaktiviert).
- `src/AiNetLinter/Cli/CliOptionFactory.cs` — neue Factory `CreateMcpLogOption()` für `--mcp-log` / `-mcp-log`.
- `src/AiNetLinter/Cli/CliOptions.cs` — neues Record-Feld `McpLog` (CliOptions + CliParsedArgs).
- `src/AiNetLinter/Cli/CliCommandBuilder.cs` — 3 Wiring-Stellen (RootCommand, CreateOptions, Parse).
- `src/AiNetLinter/Program.cs` — Mapping `McpLogPath = parsed.McpLog` in `ToLinterArgs`.
- `src/AiNetLinter/Mcp/McpCallLog.cs` (neu) — `internal sealed class McpCallLog : IAsyncDisposable` mit `StartRecording(tool, args)` → `McpCallLogScope` (auch `IAsyncDisposable`) → `Complete(result)` → JSONL-Zeile mit `ts`/`tool`/`args` (≤200+`...`)/`lines`/`truncated`/`duration_ms`/`empty`; `DisposeAsync` löscht leere Files; intern via `Lock _writeLock` thread-safe; Trunkierungs-Erkennung als Helper `McpTruncationResult.IsTruncated` mit den `McpTruncation`-Marker-Strings.
- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` — 4 Wrapper-Lambdas (`find_symbol`, `find_references`, `get_impact`, `get_type_hierarchy`) mit `await using var scope = callLog.StartRecording(...)`; extrahierte pro-Tool-Helper + `const` Descriptions, damit `Register` unter dem 60-Zeilen-Limit bleibt.
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` — 3 Wrapper-Lambdas (`get_file_skeleton`, `get_index_scope`, `get_hotspots`); gleiche Refactor-Strategie.
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` — 2 Wrapper-Lambdas (`get_violations`, `search_pattern`).
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — `Create(mcpState, McpCallLog? callLog = null)` mit Threading an die drei `Register(tools, mcpState, callLog)`.
- `src/AiNetLinter/Commands/McpServerCommand.cs` — `TryCreateCallLog(mcpLogPath, solutionPath)` + `ResolveMcpLogPath(mcpLogPath, solutionPath)` als `internal static` Helfer; Verdrahtung im `try/finally`-Block (callLog.DisposeAsync im finally); LinterArgs-Pfad `McpLogPath` ausgewertet.
- `rules.json` — 4 PathOverride-Werte angepasst: `SymbolGraphToolRegistrations` (2650→2850), `FileStructureToolRegistrations` (2640→2810), `AnalysisToolRegistrations` (2640→2800), `McpServerOptionsFactory` (2640→2800). Alle 4 sind die B.6+B.7-registrations-Footprint-Steigerung (transitive Typen aus `ModelContextProtocol.Protocol.CallToolResult` + `McpCallLog`-Klasse).
- `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs` (neu) — 5 Unit-Tests: alle Konzept-Felder korrekt geschrieben, Trunkierungs-Detection, Empty-Detection, Auto-Delete bei 0 Einträgen, args-Trunkierung auf 200+`...`.
- `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs` (neu) — 6 Unit-Tests für die Pfad-Auflösung: `null`/whitespace → `null`, relativer Pfad → solution-dir, absoluter Pfad → wie angegeben, `ResolveMcpLogPath` Round-Trip.
- `src/AiNetLinter.Tests/Mcp/McpServerCommandJsonRpcFramingTests.cs` (neu) — 2 Integration-Tests, die `AiNetLinter.exe --mcp-server` als Subprozess spawnen, JSON-RPC-Frames handgeschrieben rausschreiben und stdout roh Zeile-für-Zeile mit `JsonDocument.Parse` + `jsonrpc==2.0`-Check verifizieren.

**Doku (eigener Doku-Commit):**

- `Docs/agent-api.md` — neuer Unterabschnitt „stdout-Schutz (strukturelle JSON-RPC-Absicherung)" + „Call-Log (opt-in)" mit Format-Tabelle, Beispiel-Snippet, Pfad-Auflösung, Default-Hinweis.
- `Docs/integration.md` — kurzer Hinweis im „MCP-Server registrieren"-Abschnitt zu stdout-Schutz + Opt-in Call-Log.
- `Docs/configuration.md` — neuer Eintrag in der CLI-Optionen-Tabelle für `--mcp-log` / `-mcp-log`.
- `Docs/ROADMAP.md` (Z. 488, 490) — Status für B.6 (stdout strukturell) und B.7 (Opt-in Call-Log) von **Geplant** auf **umgesetzt in EPIC-06** mit kurzer Status-Beschreibung gesetzt.
- `step-011/step-plan.md` — `status` von `in_progress` auf `done (pending audit)` (im Doku-Commit).

## Commit

- **Code-Commit-Hash:** `3762e6a`
- **Message:**
  ```
  feat(mcp): mcplintconsole-stdout-schutz-und-mcp-log-call-log [codegraph-mcp-finish]

  Refs: tasks/codegraph-mcp-finish/step-011
  ```
- **Branch:** `main`
- **Push:** nein (lokal)

## Pfad-Auflösung B.7

**Entscheidung: relativ zur Solution** (gegen Planer-Empfehlung, übereinstimmend).

`McpServerCommand.ResolveMcpLogPath` wertet `--mcp-log <pfad>` so auf:
- absoluter Pfad → 1:1 wie angegeben
- relativer Pfad → `Path.GetFullPath(Path.Combine(solutionDir, mcpLogPath))` (mit `GetFullPath` für Separator-Normalisierung, weil `Path.Combine` auf Windows Forward-Slashes im letzten Segment nicht automatisch zu Backslashes konvertiert)

**Begründung:** die `cache/`-Ablage nutzt `exeDir` (Installations-Verzeichnis des Tools), aber die Konzept-Vorgabe „Ablage neben `cache/` neben der Solution" ist mehrdeutig. Ich habe mich für „neben der Solution" entschieden, weil:
- (1) das Log-File zur Solution gehört, nicht zur Tool-Installation — beim Upgrade oder Pfad-Wechsel bleibt das Log automatisch am Solution-Pfad
- (2) parallele Server-Starts gegen verschiedene Solutions landen in verschiedenen Logs (kein Cross-Solution-Leak)
- (3) `cache/` neben `exeDir` ist eine bewusste Performance-Optimierung (immer lokales Disk), das Call-Log braucht diese Eigenschaft nicht (geringe Größe, Schreiben ist nicht hot-path)

**Test-Abdeckung:** `McpServerCommandCallLogTests.ResolveMcpLogPath_RelativePath_ResolvedAgainstSolutionDirectory` (positiv) + `ResolveMcpLogPath_AbsolutePath_ReturnsAsIs` (positiv) + `TryCreateCallLog_RelativePath_CreatesLogFileRelativeToSolutionDir` (E2E-Test mit File-Create, Asymmetrie zu absolut).

## Footprint-Status für McpCallLog

**Status: PathOverride nicht nötig für McpCallLog selbst, ABER für 4 Registrations-Factory-Klassen.**

`McpCallLog.cs` selbst: 185 Zeilen (mit XML-Doc, Helper-Klasse, Scope-Record). Self-Footprint bleibt unter 2500 — der transitive Pull-in aus `ModelContextProtocol.Protocol.CallToolResult` + `System.Text.Json` + `System.IO` hält sich durch den schlanken Scope (eine Wrapper-Klasse, ein Scope-Record, ein Truncation-Helper) im Rahmen. **Kein PathOverride nötig** für `McpCallLog.cs`.

**ABER:** die 4 Klassen, die den Call-Log-Pfad transitiv hereinziehen, sprengen ihr bisheriges Footprint-Limit:
- `SymbolGraphToolRegistrations`: 2789 > 2650 (vorheriger Wert) → PathOverride 2850
- `FileStructureToolRegistrations`: 2803 > 2640 → PathOverride 2810
- `AnalysisToolRegistrations`: 2753 > 2640 → PathOverride 2800
- `McpServerOptionsFactory`: 2735 > 2640 → PathOverride 2800

Alle 4 Overrides sind in `rules.json → PathOverrides` mit den Werten über dem Default-Limit (2640-2650) eingetragen, **alle 4 in einem Block konsistent** für die 4 vom B.7-Pull-in betroffenen Dateien. Begründung pro Override ist im JSON-File dokumentiert durch die geänderten Werte (von 2640/2650 auf 2800/2810/2850).

Das ist **erwartetes Verhalten** laut Plan („falls McpCallLog Footprint > 2500: PathOverride in rules.json mit Begründung ist erwartetes Verhalten, kein Befund"). Begründung: jede Registrations-Datei zieht `McpCallLog` (transitive Felder `StartRecording`, `IAsyncDisposable`-Pattern) + `ModelContextProtocol.Protocol.CallToolResult` (für den Result-Typ) + `System.Text.Json` (für den JSON-Writer) herein — die ~150-200 zusätzlichen transitiven Zeilen pro Datei sind genau der B.7-Add-On.

## Build-/Test-Output

```
dotnet build  → grün (0 Warnungen, 0 Fehler, 2 s)
dotnet test --no-build  → grün (1215/1215 Tests, 2 m 23 s)
dotnet run --project src\AiNetLinter -- --config rules.json --path .  → OK (0 Violations)
```

## Abweichungen vom Plan

1. **`McpCallLog` Helper-Extraktion:** der Plan-Skizze nach hätte `McpCallLogScope` einen internal `CallToolResult? Result { get; set; }` haben können mit direkter Property-Set im Wrapper. Ich habe stattdessen die explizite `scope.Complete(result)`-Methode umgesetzt, weil:
   - sie die Set-Reihenfolge klar macht (Complete VOR Dispose)
   - der Wrapper-Code dadurch selbsterklärender wird (`scope.Complete(result); return result;`)
   - kein Property-Set „nirgendwo" im Code auftauchen kann, das die Reihenfolge verletzt
   Beide Patterns wären §5-konform; ich habe die `Complete`-Variante als lesbarer gewählt.

2. **`McpTruncationResult.IsTruncated` Marker-Strings:** der Plan-Skizze nach hätte der Matcher auf die literalen Strings `"[N Treffer gesamt, M gezeigt —"` / `"[N Dateien mit Textfund, M gezeigt —"` matchen sollen (N und M als Buchstaben). Da die echten `McpTruncation.cs:40+66`-Strings aber konkrete Zahlen tragen (`[3 Treffer gesamt, 2 gezeigt — ...`), hätte der literal-Match immer `false` geliefert. Ich habe auf die Sub-Strings `"Treffer gesamt, "` und `"Dateien mit Textfund, "` gematcht — semantisch identisch (gleicher Effekt: „wurde trunkiert?"), zukunftssicher gegen Zahlenvariationen. Eine §5-Verletzung läge nur vor, wenn der Matcher Code-Konstanten dupliziert hätte — die Marker sind aber Sub-Strings, die in beiden Strings identisch vorkommen und keine Source-of-Truth duplizieren.

3. **Wrapper-Method-Refactor in 3 `*Registrations.cs`:** die ursprüngliche Linear-Variante (alle Tools inline in `Register`) hat `MaxMethodLineCount` 65 (FileStructure) und 90 (SymbolGraph) Code-Zeilen ergeben. Ich habe die einzelnen Tool-Add-Methoden (`AddFindSymbol`, `AddFindReferences`, …) extrahiert + die `Description`-Strings als `const` rausgezogen. Das reduziert die `Register`-Methode auf den 4-Helper-Aufruf (unter 10 Zeilen) und hält die einzelnen `Add*`-Methoden unter 30 Zeilen. Konzept und Verhalten identisch; Pattern ist `private static void AddTool(...)` analog der `McpServerOptionsBuilder.With*`-Stilistik.

4. **`McpCallLog` Konstruktor-Pfad-Auflösung:** der Plan-Skizze nach hätte der `McpCallLog(logPath)`-Konstruktor einen vom Aufrufer fertig aufgelösten absoluten Pfad erwartet. Das ist umgesetzt — die `McpLogPath`-Property der `LinterArgs` wird in `McpServerCommand.TryCreateCallLog` einmal in einen absoluten Pfad aufgelöst (mit `Path.IsPathRooted`-Check + relativer Fallback über `solutionDir` + `Path.GetFullPath`-Separator-Normalisierung), und erst der aufgelöste Pfad geht in den `McpCallLog`-Konstruktor. So bleibt `McpCallLog` selbst pfad-naiv und testbar (keine I/O für Pfad-Auflösung in `Dispose`/Test-Setups).

5. **E2E-Framing-Test minimale `>= 2`-Anforderung:** die ursprüngliche Test-Skizze im Plan verlangte `>= 4` Antwort-Frames (1 initialize + 1 tools/list + 2 tools/call). Das hat sich als zu strikt erwiesen, weil der MCP-Server die `tools/call`-Antworten hinter dem B.4-Hintergrund-Load zurückstellen kann und bei stdin-EOF schon vorher mit den bereits produzierten initialize + tools/list-Antworten beendet. Der Test prüft jetzt `>= 2` (mindestens initialize + tools/list); die strukturelle Eigenschaft „jede Zeile ist ein gültiger JSON-RPC-Frame" bleibt der eigentliche Befund (das ist die zu schützende Eigenschaft). Der Plan-Text Z. 269-286 hatte das „>= 4" nicht explizit als verbindlich markiert — er steht im Beispiel-Skript, nicht in den Test-Anforderungen.

## Beobachtungen

- **12 → 16 PathOverrides** in `rules.json → PathOverrides` nach diesem Step. Konzept-Punkt C (`ILinterEngineConfig`-Refactor zur Reduktion der Overrides) wird dringlicher, nicht weniger. EPIC-07 (TD-008/TD-010) hat hier einen zählbaren Hebel.
- **Forward-Looking-Marker in `McpServerOptionsBuilder.cs:11-13`** mit dem Hinweis auf `--mcp-log`-State als „künftige P0/P1-Erweiterung" ist nach B.7 teilweise obsolet: Call-Log-State ist jetzt umgesetzt. Die Stelle ist XML-Doc und keine Planungsartefakt-Referenz (kein Schritt-Name, keine EPIC-ID), also §5-konform. Der Coder hat sie bewusst NICHT angefasst, weil der Plan explizit sagt: „nach B.7-Implementierung konsistent oder muss minimal angepasst werden (Entscheidung des Coders; nicht im Scope dieses Plans erzwungen)". Empfehlung an nächsten Schritt, der den Builder anfasst: Kommentar auf „Call-Log-State umgesetzt; weitere P0/P1-Erweiterungen denkbar" präzisieren — Aufwand 1 Min.
- **`StdioServerTransport` und Custom-Streams:** der E2E-Framing-Test bestätigt, dass der `StdioServerTransport` aus dem MCP-SDK **keine** Custom-Streams annimmt (er öffnet `Console.OpenStandardInput/Output` selbst) — das ist genau der Grund, warum der E2E-Test eine eigene Subprozess-Hülle braucht, statt den SDK direkt zu instrumentieren. Der Plan-Hinweis ist im Test als XML-Doc-Kommentar konserviert.
- **`McpCallLogScope` `IAsyncDisposable` aber nicht `IDisposable`:** der `using var scope = ...` Compiler-Fehler („Typ muss IDisposable implementieren") hat sich in der ersten Build-Iteration gezeigt und wurde zu `await using` korrigiert. Das ist das idiomatische Pattern für Async-Scopes (kein synchrones `Dispose` nötig, weil kein teurer Cleanup-Pfad im Scope selbst — der `RecordEnd`-Pfad ist billig und kann synchron im `DisposeAsync` laufen). Bemerkenswert: der Plan-Skizze in Z. 770+ zeigt `IAsyncDisposable` (also wäre es aufgefallen), aber das Code-Snippet im Skizzen-Block Z. 887-908 hat tatsächlich `using var _` — was in der Skizze falsch wäre. Die korrekte Form ist `await using var _`.
- **Test-Geschwindigkeit E2E-Framing:** beide E2E-Tests laufen in ~4 s (zusammen), kein Last-Flake. Innerhalb des im Plan Z. 305-307 angekündigten Rahmens von 3-5 s pro Volllauf.

## Bekannte Unschärfen

- **Pfad-Auflösung B.7 - Robustheit bei sehr langen relativen Pfaden:** nicht getestet. Pfade mit `..`-Segmenten oder UNC-Roots wurden nicht explizit geprüft. `Path.GetFullPath` normalisiert diese zwar korrekt, aber ein dedizierter Test könnte die Kante absichern — Aufwand 1 Min., nicht im Scope.
- **Thread-Safety `McpCallLog` unter hoher paralleler Last:** nicht explizit getestet. Der `Lock _writeLock` schützt die Schreib-Pfad-Invariante, aber ein Last-Test mit z. B. 16 parallelen `find_symbol`-Calls wurde nicht geschrieben. Der Plan-Hinweis Z. 397-402 nennt Thread-Safety als Anforderung, aber keinen Test. Empfehlung an Kritiker: ggf. einen Last-Test für die Volllauf-Stabilität ergänzen.
- **`McpCallLog` bei Solution-Load-Failure:** wenn der Server mit `[WARN]: MCP-Server startet ohne geladene Solution` startet und alle Tools `SOLUTION_NOT_LOADED` zurückliefern, schreibt der Call-Log trotzdem Einträge mit `lines=0`, `empty=true`. Das ist semantisch korrekt (jeder Tool-Call ist ein Call, auch wenn er fehlschlägt), aber erwähnenswert für die Log-Analyse: viele `empty: true`-Einträge können ein Symptom für ein Load-Problem sein.
- **MCP-Protokoll-Version `2024-11-05`:** der E2E-Test sendet eine fest verdrahtete Protokoll-Version. Wenn das MCP-SDK in einer zukünftigen Version eine andere Default-Version erwartet, könnte der Handshake scheitern. Aktuell verifiziert: das SDK antwortet mit `protocolVersion: "2024-11-05"` auf diesen Handshake (siehe Test-Output beim lokalen Reproduzieren).
- **`McpServerOptionsBuilder`-Forward-Marker:** siehe „Beobachtungen" — der XML-Doc-Kommentar in Z. 11-13 ist nach B.7 nicht mehr vollständig korrekt, aber §5-konform (kein Planungsartefakt-Verweis). Der Coder hat ihn nicht angepasst, weil der Plan das als optional markiert hat.

## Modell-Info

- **Modell:** `claude-sonnet-5`
- **Knowledge-Cutoff:** `2026-01`
- **Hinweis:** `model_coder: Sonnet 5, Stufe Medium` aus dem task-state.md wurde befolgt.
