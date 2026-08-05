---
status: open
type: step-plan
task: mcp-call-logging-fuer-agenten-analyse
step: 003
title: "Error-Hook im MCP-Server-Lifecycle (per-Tool-Wrapper via Shared-Helper)"
epic: EPIC-03
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T14:45:00+02:00
related_to: []
---

# Step 003: Error-Hook im MCP-Server-Lifecycle (per-Tool-Wrapper via Shared-Helper)

## Bezug

- **Task:** `mcp-call-logging-fuer-agenten-analyse`
- **Epic:** `EPIC-03` aus `roadmap.md` — "Error-Hook im MCP-Server-Lifecycle". EPIC-01 und EPIC-02 sind bereits abgeschlossen; das in `McpCallLog.RecordError` (step-002) bereitgestellte Sink wird in diesem Step an die Tool-Handler angeschlossen.
- **Konzept-Referenz:** `konzept.md` §Muss-Habe 5 ("Error-Hook im MCP-Server-Lifecycle") + DoD 2 (künstlich ausgelöste Exception in einem Tool-Handler → JSONL-Zeile mit `level=error`).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen vorgefunden:

- **`McpCallLog.RecordError(toolName, args, exception)` existiert** (`src/AiNetLinter/Mcp/McpCallLog.cs:99`). Sichtbarkeit `internal`, akzeptiert `Exception` (per `ArgumentNullException.ThrowIfNull` exogen validiert), 4 KB Stack-Trace-Cap, identische `_writeLock`-Serialisierung wie `RecordEnd`. Bereit für Aufrufer.
- **`callLog` wird in `McpServerCommand.RunAsync` (Z. 67) instanziiert** und an `McpServerOptionsFactory.Create(mcpState, callLog)` weitergereicht. `McpServerOptionsFactory.BuildToolCollection` (Z. 58-70) reicht `callLog` an alle vier `*ToolRegistrations.Register`-Methoden durch. Damit ist `callLog` an jedem Tool-Handler verfügbar.
- **Die 10 Tool-Handler (nicht 8 wie in der Roadmap geschätzt) folgen einem einheitlichen Muster** (Stichproben in `AnalysisToolRegistrations.cs:46-62`, `FileStructureToolRegistrations.cs:44-60`, `SymbolBodyToolRegistrations.cs:36-52`, `SymbolGraphToolRegistrations.cs:42-58`):
  ```csharp
  async (args...) =>
  {
      if (callLog is null) return await Tool.ExecuteAsync(mcpState, args, ct);
      await using var scope = callLog.StartRecording(toolName, args);
      var result = await Tool.ExecuteAsync(mcpState, args, ct);
      scope.Complete(result);
      return result;
  }
  ```
  → **3 Zeilen pro Wrapper** (`StartRecording` + `Complete`), die 1:1 durch einen einzelnen Helper-Aufruf ersetzt werden können. Das Muster ist bereit für eine zentrale try/catch-Erweiterung.
- **Tool-Name und Args-String sind in jedem Wrapper bereits als Closure lokale Variablen verfügbar** — kein zusätzlicher Drill-Down in `RequestContext` o. ä. nötig. Das ist der strukturelle Grund, warum die per-Wrapper-Variante der globalen SDK-Handler-Variante überlegen ist.
- **`AIContextFootprint`-PathOverrides** für die fünf McpCallLog-Konsumenten wurden in step-002 via User-Workaround A angehoben (siehe `tech-debt.md` TD-002: Buffer 201–208 Z. pro Datei). Der in diesem Step geplante Refactor ist net-positiv in McpCallLog selbst (~+15 Z.) und net-negativ in den vier Registrar-Dateien (jeweils 2 Z. weniger pro Wrapper), womit die transitive Welle pro Konsument überschaubar bleibt (siehe "Abwägung" weiter unten).
- **`McpCodeGraphServer` ist `internal sealed`** (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs:25`) — es gibt keine Mock-Interface-Variante. Ein vollständiger E2E-Test über `McpTestClient` mit einem realistisch werfenden Tool-Aufruf wäre nur über `McpLiveRepositoryTests`/`McpTestClient` und eine echte Solution-Umgebung möglich. Der Helper lässt sich aber isoliert und ohne diese Schwere testen (siehe "Tests" + "Notes").
- **`AllowCancellationShutdownCatch`** in `rules.json` (siehe `AiNetLinter.mdc` agent-resilience): `OperationCanceledException` darf herausgefiltert werden, ohne dass `EnforceNoSilentCatch` verletzt wird. Wird in der Helper-Implementierung genutzt.

## Intention

Die 10 Tool-Handler sollen unbehandelte Exceptions abfangen, **bevor** sie in den SDK-Layer propagieren und dort zu "An error occurred invoking X" werden, und an `McpCallLog.RecordError` weiterreichen. Damit landet jeder unbehandelte Tool-Fehler als JSONL-Eintrag mit `level=error / error_type / error_message / stack_trace` in derselben Datei wie die regulären Call-Einträge — exakt das, was DoD 2 verlangt.

Die zentrale Abwägung dieser Planung ist die Wahl der SDK-Stelle: **per-Tool-Wrapper via Shared-Helper** statt globaler SDK-Error-Handler. Begründung: (a) Tool-Name und Args sind bereits in der Closure jedes Wrappers, ein globaler Handler müsste sie aus dem `RequestContext` rekonstruieren oder verliert sie; (b) die 10 Wrapper sind strukturell identisch, ein Shared-Helper bündelt die Error-Hook-Logik an genau einer Stelle (statt 10 try/catch-Klone); (c) der Helper ist isoliert unit-testbar ohne `McpTestClient`-Schwere; (d) der per-Wrapper-Pfad erhält den StartRecording/Complete-Pfad und das Locking 1:1, kein neues Lock-Konstrukt nötig.

## Konkrete Änderungen

**Bei `step_type: single`:**

### Datei 1: `src/AiNetLinter/Mcp/McpCallLog.cs`

- **Was:** Neue `internal async Task<CallToolResult> ExecuteCallAsync(string toolName, string args, Func<Task<CallToolResult>> toolFn)` als zentrale try/catch-Hülle. Body:
  - `ArgumentNullException.ThrowIfNull(toolFn)`.
  - `var scope = StartRecording(toolName, args);`
  - `try { var result = await toolFn().ConfigureAwait(false); scope.Complete(result); return result; }`
  - `catch (Exception ex) when (ex is not OperationCanceledException) { RecordError(toolName, args, ex); throw; }`
  - `finally { await scope.DisposeAsync().ConfigureAwait(false); }` — `DisposeAsync` ist im Exception-Pfad No-Op (weil `Complete` nicht aufgerufen wurde → `_result` ist null → `RecordEnd` wird nicht ausgelöst). Auf dem Erfolgs-Pfad wird der reguläre Call-Eintrag über `RecordEnd` geschrieben.
- **Warum:** Zentralisiert die Error-Hook-Logik, die sonst 10-fach dupliziert würde. Ein einziger Aufruf pro Wrapper statt drei Zeilen (`StartRecording` + `Complete` + return-Pfad). Sichtbarkeit `internal`, deckungsgleich mit `RecordError`/`StartRecording`. Filter auf OCE entspricht `AllowCancellationShutdownCatch` (Regel-Allowlist in `rules.json`).

### Datei 2: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs`

- **Was:** In den beiden Tool-Lambdas (`AddGetViolations` Z. 46-62, `AddSearchPattern` Z. 78-94) wird das 3-Zeilen-Scope-Pattern ersetzt durch:
  ```csharp
  return await callLog.ExecuteCallAsync("get_violations", scopeFilter ?? "",
      () => GetViolationsTool.ExecuteAsync(mcpState, scopeFilter, ct));
  ```
  Die `if (callLog is null) { return await ...; }`-Branch bleibt unverändert. XML-Doc auf `Register` und den AddX-Methoden bleibt strukturell, der Hinweis auf "zeichnet den Tool-Aufruf auf, wenn aktiv" wird beibehalten (kein API-Bruch).
- **Warum:** Zwei Wrapper, beide identische Mechanik. Netto -2 Z. pro Wrapper (3 → 1 Zeile), +0 Z. im Datei-Kopf.

### Datei 3: `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`

- **Was:** In `AddGetFileSkeleton` (Z. 44-60), `AddGetIndexScope` (Z. 73-89), `AddGetHotspots` (Z. 103-119) dieselbe Ersetzung wie in Datei 2. Tool-Name/Args-Strings werden 1:1 aus den bestehenden Closure-Lokalen übernommen.
- **Warum:** Drei Wrapper, identisches Muster. Netto -6 Z.

### Datei 4: `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs`

- **Was:** In `AddGetSymbolBody` (Z. 36-52) dieselbe Ersetzung. Args-String `$"{identifier}|{maxBodyLines}"` bleibt unverändert.
- **Warum:** Ein Wrapper, identisches Muster. Netto -2 Z.

### Datei 5: `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`

- **Was:** In `AddFindSymbol` (Z. 42-58), `AddFindReferences` (Z. 73-89), `AddGetImpact` (Z. 107-124), `AddGetTypeHierarchy` (Z. 145-161) dieselbe Ersetzung. Bei `AddGetImpact` wird der `input`-Record weiterhin vor dem Helper-Aufruf konstruiert (für die Tool-ExecuteAsync-Signatur), der Args-String `$"{gitRef}|{symbolIdentifier}|{maxResults}|{depth}"` bleibt unverändert.
- **Warum:** Vier Wrapper, identisches Muster. Netto -8 Z.

### Datei 6: `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs`

- **Was:** Vier neue `[Fact]`s mit `[Trait("Category", "Unit")]` (siehe "Tests").
- **Warum:** Der Helper ist die zentrale Mechanik für DoD 2 und verdient dieselbe Test-Tiefe wie `RecordError` in step-002.

## Tests

- [ ] `ExecuteCallAsync_SuccessCall_WritesCallEntryAndReturnsResult` — happy path: Wrapper-Delegate liefert `McpToolResults.Text("hit")`, JSONL enthält 1 Call-Eintrag mit `tool=…`, kein `level`, Returnwert wird 1:1 durchgereicht.
- [ ] `ExecuteCallAsync_ThrowingCall_WritesErrorEntryAndRethrows` — **Kerntest für DoD 2**: Delegate wirft `new InvalidOperationException("simuliertes Hot-Reload-Race in get_file_skeleton")` mit Reflection-gesetztem Stack-Trace (`Exception._stackTraceString` über die bestehende `TestException`-Hilfsklasse, siehe step-002). Asserts: (a) die Exception wird vom Helper propagiert (per `await Assert.ThrowsAsync<InvalidOperationException>`); (b) JSONL enthält 1 Eintrag mit `level=error`, `error_type=TestException` bzw. `InvalidOperationException`, `error_message` enthält den Race-String, `stack_trace` enthält die ersten Stack-Zeilen, `tool=get_file_skeleton`, `args=<…>`.
- [ ] `ExecuteCallAsync_OperationCanceled_NotLoggedAndRethrown` — Delegate wirft `OperationCanceledException`. Asserts: keine JSONL-Zeile geschrieben (File leer), Exception propagiert. Schützt vor versehentlichem OCE-Logging bei Shutdown/Cancellation.
- [ ] `ExecuteCallAsync_ParallelThrowingCallsDoNotInterleaveJsonLines` — 50 parallele `ExecuteCallAsync`-Aufrufe, die jeweils werfen. Asserts: 50 Zeilen, jede parsebar als `JsonDocument` (kein halb-geschriebener Eintrag durch Race). Spiegelt `RecordError_ParallelCallsDoNotInterleaveJsonLines` (Z. 274-319) für den neuen Helper.

Keine neuen Test-Dateien — der Helper ist McpCallLog-intern und passt in `McpCallLogTests.cs`. Kein E2E-Test über `McpTestClient`: (a) das Tool-Werfen ist nicht-trivial natürlich herstellbar (`GetFileSkeletonTool.ExecuteAsync` liefert Loading-/SolutionNotFound-/FileNotFound-Antworten statt zu werfen, siehe `src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs:25-32`); (b) der Helper-Unit-Test deckt die exakte DoD-2-Mechanik ab; (c) die 10 Wrapper sind 1:1-Delegationen an den Helper ohne eigene Logik, das Wiring ist per Inspektion bewiesen. Diese Trade-off-Entscheidung ist im `Notes`-Abschnitt weiter unten dokumentiert.

## Definition of Done

- [ ] `McpCallLog.ExecuteCallAsync` implementiert mit `internal async Task<CallToolResult>`, ArgumentNullException-Guard, OCE-Filter, `_writeLock`-Konformität.
- [ ] Alle 10 Tool-Handler in den vier `*ToolRegistrations`-Klassen auf den Helper umgestellt (kein Restbestand des alten 3-Zeilen-Patterns).
- [ ] Vier neue Tests in `McpCallLogTests.cs`, grün.
- [ ] Bestehende 5 `RecordError`-Tests + 5 Call-Tests in `McpCallLogTests.cs` weiterhin grün (Regression-Schutz für die `_writeLock`-Semantik).
- [ ] Bestehende Tests in `McpServerCommandCallLogTests.cs` weiterhin grün (Konzept-Default-Pfad aus step-001 unangetastet).
- [ ] `dotnet build` — 0 Warnungen, 0 Fehler (Zero-Warning-Direktive).
- [ ] `dotnet test --filter Category=Unit` — grün.
- [ ] `dotnet test` (Volllauf) — grün, inkl. Dogfooding-Test `RunLinterCli_OnWholeSolution_ReturnsSuccess`. Falls dieser rot wird (transitive `AIContextFootprint`-Welle über das gepufferte Maß hinaus), siehe "Bekannte Ausnahmen".
- [ ] Code-Commit auf aktuellem Branch (Conventional Commit auf Deutsch, imperativ, Subject ≤72 Zeichen, Pflicht-Suffix `[mcp-call-logging-fuer-agenten-analyse]`, `Refs: <task-dir>/step-003`-Trailer).
- [ ] `step-003/step-result.md` geschrieben mit allen Punkten aus `coder/SKILL.md`.
- [ ] `status` in `step-plan.md` von `open` über `in_progress` auf `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#2` (Architektur-Verbote: kein DI, kein `AssemblyLoadContext`, monolithisch) — `McpCallLog` bleibt statische Wrapper-Klasse, kein neuer Container, kein Reflection-basiertes Tool-Loading.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` (Qualitätsdrift-Prävention: Clean-Code-Kommentar-Politik, Zero-Warning, Result-Pattern-Bevorzugung) — Helper-XML-Doc beschreibt nur Was/Wie, **kein** Verweis auf "step-003", "EPIC-03", "TD-002" o. ä. (Ordner werden nach Task-Abschluss gelöscht → Verweis wertlos). `TreatWarningsAsErrors` ist aktiv; `ArgumentNullException.ThrowIfNull` vermeidet manuelle Null-Prüfung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` (Updates & Tests: xUnit-v3, `[Fact]` + `[Trait("Category", "Unit")]`, keine zwangsserialisierende Collection für parallele Tests).
- `.agents/rules/AiNetLinter.mdc#Grenzwerte` (Produktion): `MaxMethodLineCount=60`, `MaxMethodParameterCount=4` — der neue `ExecuteCallAsync` hat 3 Parameter, Body bleibt unter 20 Z. (3 + 5 + 1 + 1 + finally = 10 Z. + XML-Doc).
- `.agents/rules/AiNetLinter.mdc#agent-resilience`: `EnforceNoSilentCatch` — `catch (Exception ex) when (...) { RecordError(...); throw; }` erfüllt die Regel "Log + sichtbarer Fehler oder throw" (Log via `RecordError`, sichtbar via JSONL-Eintrag, plus `throw;`).
- `.agents/rules/AiNetLinter.mdc#agent-resilience`: `BanAsyncVoid`, `BanBlockingTaskAccess` — Helper ist `async Task<…>`, kein `.Wait()`/`.Result()`, kein `async void`.
- `.agents/rules/AiNetLinter.mdc#general`: `EnforceAsciiIdentifiers` (alle neuen Identifier ASCII: `ExecuteCallAsync` ✓, `toolName/args/toolFn` ✓), `EnforceNullableEnable` (alle Dateien haben `#nullable enable` in Z. 1), `EnforceSealedClasses` (betrifft nur Klassendeklarationen, hier nicht relevant).
- `.agents/rules/AiNetLinter.mdc#architecture`: `EnforceNamespaceDirectoryMapping` — `McpCallLog.cs` in `src/AiNetLinter/Mcp/`, Namespace `AiNetLinter.Mcp`. Konsistent.
- `.agents/rules/AiNetLinter.mdc` (auto-generiert) → `rules.json` → `AIContextFootprint=2500` mit PathOverrides pro Konsument — siehe "Abwägung" unten.

## Bekannte Ausnahmen

- **Kein E2E-Test über `McpTestClient` für DoD 2.** DoD 2 verlangt eine künstlich ausgelöste Exception in einem Tool-Handler. `GetFileSkeletonTool.ExecuteAsync` (das im Konzept genannte Tool) liefert bei den natürlich erreichbaren Failure-Modi nur Loading-/SolutionNotFound-/FileNotFound-Antworten, keine echte Exception. Ein realistischer Throw wäre nur über `McpCodeGraphServer` mit einem absichtlich defekten `Catalog`/`Solution` oder über echte Filesystem-Manipulationen zur Laufzeit herstellbar — beides deutlich schwerer als der semantisch äquivalente Unit-Test auf `ExecuteCallAsync`. Der Helper-Unit-Test beweist die exakte DoD-2-Mechanik (Tool-Delegate wirft → JSONL-Zeile + Rethrow), und die 10 Wrapper sind 1:1-Delegationen ohne eigene Logik, deren Wiring per Code-Inspektion bewiesen ist. Akzeptable Vereinfachung, im Test-Plan dokumentiert.
- **Potenzielle `AIContextFootprint`-Welle in den 5 McpCallLog-Konsumenten (TD-002-Folge).** Geschätzte Netto-Veränderung pro Konsument in diesem Step: ~+11 bis +15 Z. (eigene Refactor-Einsparung 2-8 Z. + transitive McpCallLog-Wachstum ~+15 Z.). Die 5 PathOverrides haben nach step-002 Buffer 201-208 Z. pro Datei (`tech-debt.md` TD-002). Selbst Worst-Case (+15) liegt komfortabel im Puffer. **Falls** der Dogfooding-Test `RunLinterCli_OnWholeSolution_ReturnsSuccess` trotzdem rot wird, ist der Workaround dieselbe PathOverride-Bump-Prozedur wie in step-002 — kein Plan-Re-Design nötig. Wahrscheinlichkeit: niedrig, daher kein prophylaktischer Bump.
- **`OperationCanceledException` wird nicht in der JSONL geloggt.** Bewusste Entscheidung: das SDK behandelt OCE als Cancellation, nicht als Tool-Error — ein OCE-Eintrag im Call-Log würde den Erfolgs-/Fehler-Dichotomie-Sinn verfälschen. Regel `AllowCancellationShutdownCatch` in `rules.json` deckt das Filter-Verhalten ab. Im Test `ExecuteCallAsync_OperationCanceled_NotLoggedAndRethrown` verifiziert.
- **Tool-Name und Args-Kontext gehen nicht verloren**, anders als bei einem globalen SDK-Handler. Beide sind bereits Closure-lokal in jedem der 10 Wrapper, der Helper übernimmt sie als Parameter. Diese Wahl begründet die per-Wrapper-Variante (statt globaler `McpServerOptions`-Handler).

## Abwägung

### SDK-Stelle: per-Tool-Wrapper vs. globaler SDK-Error-Handler

**Gewählt: per-Tool-Wrapper via Shared-Helper `ExecuteCallAsync`.**

Begründung:
- **Tool-Kontext verfügbar**: Name + Args sind im Closure jedes Wrappers, gehen beim Helper-Aufruf 1:1 durch. Ein globaler SDK-Handler (z. B. `McpServerOptions.Handlers.OnError` o. ä.) hätte diese Information nicht strukturell verfügbar; die SDK-`RequestContext`-API würde zusätzliche Rekonstruktion erfordern oder den Kontext ganz verlieren.
- **Weniger Duplikation als 10 separate try/catch**: Der Shared-Helper bündelt die Error-Hook-Logik an einer einzigen Stelle. 10 inline try/catch-Blöcke wären 30-50 Z. redundant (pro Wrapper ~3-5 Z. Fehler-Pfad + Helper-Variablen).
- **Isoliert unit-testbar**: Der Helper hat einen klaren `Func<Task<CallToolResult>>`-Vertrag und lässt sich ohne `McpTestClient`/`McpCodeGraphServer` testen (siehe Test 1-4). Ein globaler Handler würde Integration-Tests gegen den SDK-Transport erzwingen.
- **Locking bereits korrekt**: `ExecuteCallAsync` ruft `StartRecording`/`Complete`/`RecordError` auf, die alle denselben `_writeLock` teilen. Kein neues Lock-Konstrukt nötig.
- **Kein API-Bruch**: Die Tool-Lambdas ändern ihre externe Signatur nicht; der `if (callLog is null)`-Pfad bleibt, der `callLog`-Pfad wird lediglich von 3 auf 1 Zeile reduziert.

**Verworfen: globaler SDK-Error-Handler auf `McpServerOptions`-Ebene.**
- Tool-Name/Args-Kontext nicht strukturell verfügbar (Trade-off: würde den Diagnosewert massiv entwerten — DoD 2 verlangt explizit, dass "aus dem Agent-Verlauf 'An error occurred invoking get_file_skeleton' sich im Log die zugehörigen Args + Stack-Trace nachschlagen lässt", siehe `konzept.md` DoD 2).
- Erfordert deutlich komplexere Tests (SDK-Transport + `McpTestClient`).
- Eingriff in `McpServerOptionsFactory.Create` für einen einzelnen globalen Hook statt 10 lokaler Wrapper-Aufrufe — entgegen dem etablierten "jeder Wrapper nimmt callLog"-Pattern.

### Tech-Debt-Implikation (TD-002-Folge)

| Konsument | Eigene Refactor-Einsparung (Z.) | McpCallLog-Wachstum (Z.) | Netto pro Datei (Z.) | Buffer nach step-002 (Z.) | Buffer nach step-003 (Z., geschätzt) |
|---|---:|---:|---:|---:|---:|
| `AnalysisToolRegistrations` | -4 | +15 | +11 | 204 | ~193 |
| `FileStructureToolRegistrations` | -6 | +15 | +9 | 201 | ~192 |
| `SymbolBodyToolRegistrations` | -2 | +15 | +13 | 208 | ~195 |
| `SymbolGraphToolRegistrations` | -8 | +15 | +7 | 208 | ~201 |
| `McpServerOptionsFactory` | 0 | +15 | +15 | 202 | ~187 |

Worst-Case-Reserve nach diesem Step: ~187 Z. (in `McpServerOptionsFactory`). Das reicht für 12-18 weitere `McpCallLog`-Wachstums-Einheiten à +10-15 Z. **Kein prophylaktischer PathOverride-Bump nötig** — falls der Dogfooding-Test rot wird, wird im selben Commit nachgebumpft (gleiche Prozedur wie step-002, dokumentiert im `step-result.md`).

Die mittelfristigen Optionen aus TD-002 (`MetricsConfig` schlanker, `McpCallLog` partial-splitten, Interface vor Konsumenten schieben) sind weiterhin **User-Sache** und werden in diesem Step nicht angefasst. TD-002 bleibt `offen`.

## Code-Skizze (optional)

```
// In McpCallLog.cs (nach RecordError, vor McpTruncationResult):

/// <summary>
/// Zentrale try/catch-Huelle fuer die Tool-Handler in den vier
/// *ToolRegistrations-Klassen. Startet einen Aufzeichnungs-Scope, ruft das
/// Tool auf, schliesst den Scope bei Erfolg und persistiert einen Error-
/// Eintrag (level=error) bei unbehandelter Exception. Re-Throw der Exception
/// nach dem Logging, damit das SDK sie wie ueblich als JSON-RPC-Error
/// zurueckgeben kann. OperationCanceledException wird herausgefiltert, damit
/// Shutdown-/Cancellation-Signale nicht als Tool-Fehler ins Call-Log laufen.
/// </summary>
internal async Task<CallToolResult> ExecuteCallAsync(
    string toolName, string args, Func<Task<CallToolResult>> toolFn)
{
    ArgumentNullException.ThrowIfNull(toolFn);
    var scope = StartRecording(toolName, args);
    try
    {
        var result = await toolFn().ConfigureAwait(false);
        scope.Complete(result);
        return result;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        RecordError(toolName, args, ex);
        throw;
    }
    finally
    {
        await scope.DisposeAsync().ConfigureAwait(false);
    }
}

// In jedem *ToolRegistrations-Wrapper (Beispiel AddGetFileSkeleton):

tools.Add(McpServerTool.Create(
    async (string filePath, CancellationToken ct = default) =>
    {
        if (callLog is null)
        {
            return await GetFileSkeletonTool.ExecuteAsync(mcpState, filePath, ct);
        }
        return await callLog.ExecuteCallAsync(
            "get_file_skeleton",
            filePath,
            () => GetFileSkeletonTool.ExecuteAsync(mcpState, filePath, ct));
    },
    new McpServerToolCreateOptions
    {
        Name = "get_file_skeleton",
        Description = GetFileSkeletonDescription,
    }));
```

## Notes

- **Wiederverwendung statt Neubau:** Der bestehende `StartRecording`/`McpCallLogScope`/`RecordEnd`/`RecordError`-Mechanismus aus step-001 und step-002 wird 1:1 weitergenutzt — keine neue Lock-/IO-/Serialisierungs-Logik. Der Helper ist 10 Z. plus XML-Doc.
- **Kein API-Bruch:** Die Tool-Handler-Lambdas behalten ihre externe Signatur (Anzahl Parameter, Parametertypen, `CancellationToken`). Der `callLog`-Parameter wird von den vier `Register`-Methoden weiter durchgereicht, die `McpServerOptionsFactory.Create`-Signatur bleibt unverändert.
- **TestException-Wiederverwendung:** Die in step-002 eingeführte `private sealed class TestException : Exception` mit Reflection-Setter auf `Exception._stackTraceString` (siehe `McpCallLogTests.cs:356-373`) wird im neuen Test `ExecuteCallAsync_ThrowingCall_WritesErrorEntryAndRethrown` wiederverwendet — keine neue Test-Hilfsklasse nötig. Alternativ kann ein `new InvalidOperationException(…)` mit natürlichem Stack-Trace verwendet werden; dann ist `error_type=InvalidOperationException` und der Stack-Trace enthält die echten Frames des Test-Codes.
- **Kein McpTestClient-E2E-Test:** Begründung siehe "Bekannte Ausnahmen" oben. Wenn der User im Review darauf besteht, kann in EPIC-04 (Doku + End-to-End-Verifikation) ein `McpLiveRepositoryTests`-Fall mit einem absichtlich defekten Workspace ergänzt werden — der würde aber den Scope dieses Steps sprengen.
- **Roadmap-Korrektur-Hinweis für EPIC-04:** Die Roadmap-Bezeichnung "~8 Wrapper" in `roadmap.md:63` ist ungenau — die tatsächliche Zahl ist 10. Korrektur erfolgt im Doku-Sammel-Step EPIC-04 (analog TD-001, gleicher Pattern).
- **Bezug zu TD-001:** Nicht direkt betroffen — TD-001 betrifft die Roadmap-Notiz zu EPIC-01-Test-Scope aus step-001. Falls der Doku-Sammel-Step EPIC-04 die Roadmap aktualisiert, sollte die "8 Wrapper"-Notiz in EPIC-03 gleich mitkorrigiert werden.
- **Konzept-Wortlaut "Tool-Handler returnen via `McpServerOptionsFactory`-Wrapper"** ist mit dem gewählten Vorgehen konsistent: die Wrapper bleiben im `McpServerOptionsFactory`-Aufrufbaum, nur der Scope-Body wird durch den Helper-Aufruf ersetzt.
- **Konzept-Offene-Frage "konkrete SDK-Stelle verifizieren"** ist mit diesem Plan beantwortet: per-Tool-Wrapper via Shared-Helper (siehe "Abwägung"). Der Planer hat sich bewusst gegen die globale SDK-Handler-Variante entschieden (Begründung oben); falls der User das anders sieht, kann der Step vor Implementierung gegen einen reinen SDK-Handler-Plan ausgetauscht werden — die Helper-Methode selbst wäre dann obsolet.
- **Konzept-Offene-Frage "zählt das Verhalten als 'An error occurred invoking X' im Sinne von DoD 2 nur für Tool-Handler-Exceptions oder auch für Transport-/Initialize-Errors?"** ist mit diesem Plan pragmatisch beantwortet: **nur Tool-Handler-Exceptions**. Transport-/Initialize-Errors liegen unter der `McpServerOptionsFactory`-Ebene (z. B. in `McpServer.Create` / `server.RunAsync` in `McpServerCommand.cs:72-73`) und werden vom SDK vor dem Tool-Dispatch verarbeitet — ein per-Wrapper-Hook sieht sie nicht. Eine Erweiterung auf Transport-/Initialize-Errors wäre nur über einen globalen `McpServerOptions`-Handler möglich, was mit dem oben dokumentierten Kontext-Verlust-Trade-off erkauft wäre. **Falls** der User diese Errors auch im Call-Log sehen will, ist das ein neuer Tech-Debt-Eintrag, nicht Teil dieses Steps.
