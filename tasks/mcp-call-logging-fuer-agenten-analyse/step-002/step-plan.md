---
status: done (pending audit)  # Status via Orchestrator-Workaround nach Coder-Block 2026-08-05: PathOverride-Bumps in rules.json (User-Workaround A) haben Dogfooding-Lint grün gemacht; 1275/1275 Tests grün.
type: step-plan
task: mcp-call-logging-fuer-agenten-analyse
step: 002
title: "Error-Record-Methode in McpCallLog (RecordError mit Schema, Lock, 4 KB Stack-Trace-Cap)"
epic: EPIC-02
estimated_risk: low
step_type: single
items: []  # single-step; keine Micro-Batch-Buendelung
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T13:00:00+02:00
related_to: []  # step-001 ist Epic-Vorgaenger (LogPath, _writeLock-Infrastruktur), aber kein direkter Step-to-Step-Dependency-Pointer noetig; die Infrastruktur steht im Code.
---

# Step 002: Error-Record-Methode in `McpCallLog`

## Bezug

- **Task:** `mcp-call-logging-fuer-agenten-analyse`
- **Epic:** `EPIC-02` aus `roadmap.md` — neue `McpCallLog.RecordError(tool, args, exception)`-Methode mit JSONL-Schema-Erweiterung (`level/error_type/error_message/stack_trace`), 4 KB Stack-Trace-Cap und Serialisierung unter demselben `_writeLock` wie `RecordEnd`, sodass Call- und Error-Eintraege zeitlich geordnet erscheinen.
- **Konzept-Referenz:** `konzept.md` Muss-Habe 4 (Error-Sink in `McpCallLog`, Schema, Lock, Stack-Trace-Cap) und DoD 2, 3, 4, 5.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Codes in diesem Bereich vorgefunden:

- **`McpCallLog` (`src/AiNetLinter/Mcp/McpCallLog.cs:22`)** — `internal sealed class : IAsyncDisposable`. Felder: `_writer` (StreamWriter, UTF-8 ohne BOM, `FileMode.Append`/`FileShare.Read`), `_logPath` (string), `_writeLock` (`new Lock()` aus C# 13, Zeile 29), `_entryCount` (int), `_disposed` (bool). Konstruktor legt das Zielverzeichnis via `Directory.CreateDirectory(dir)` automatisch an, falls noetig (Z. 36-37) — d. h. der Pfad-Teil ist bereits konventionell konsistent mit dem Default-Pfad-Builder aus step-001 und braucht keine Anpassung fuer Error-Eintraege.
- **`RecordEnd`-Pattern (Z. 57-88)** — verwendet den anonymen-Objekt-Pattern fuer den JSON-Eintrag (`new { ts, tool, args, lines, truncated, duration_ms, empty }`), serialisiert via `JsonSerializer.Serialize(entry)`, schreibt unter `lock (_writeLock) { if (_disposed) return; _writer.WriteLine(json); _writer.Flush(); _entryCount++; }`. **Dieses Pattern wird in `RecordError` 1:1 uebernommen** (nur Schema-Felder unterscheiden sich). Kein neues Lock, kein neuer Writer, kein neues Disposal-Handling.
- **`McpCallLogScope` lebt in derselben Datei** (Z. 133-165) — kein separater File. Konzept-Hinweis „falls vorhanden" erfuellt: kein neuer Scope noetig, weil `RecordError` ohne Stopwatch und ohne `Complete` auskommt.
- **`McpCallLog.LogPath` (Z. 55)** — `internal string LogPath { get; }`, von step-001 als test-only Beobachtbarkeit eingefuehrt. Wird im step-001-Review explizit als „bei EPIC-02/03-API-Erweiterung ggf. neu zu bewerten" markiert. **Kein Aktion in diesem Step** — `RecordError` ist ebenfalls `internal` und passt damit zur bestehenden Sichtbarkeitsentscheidung; Re-Evaluation bleibt EPIC-04 vorbehalten.
- **`McpCallLogScope` ist NICHT zu erweitern** — `RecordError` nimmt `args` direkt als `string` entgegen (nicht via Scope), weil Errors keinen Tool-Lifecycle mit Complete/Dispose haben.
- **Bestehende Tests (`McpCallLogTests.cs`, 5 Stueck)** — nutzen die Helper `CreateTempLogPath()`, `TryDelete(path)` und `ParseSingleEntry(lines)`. Diese werden in den neuen Tests wiederverwendet, keine neuen Helper noetig. Konzept DoD 5 sagt „4 Call-Tests bleiben unveraendert gruen" — tatsaechlich sind es 5 Tests in der Datei (kleine Konzept-Ungenauigkeit, kein Blocker; im Planer-Audit dokumentiert, faellt in den EPIC-04-Doku-Sync).
- **JSONL-Schema-Konvention** — bemaengt: bestehender Call-Eintrag hat die Felder `ts/tool/args/lines/truncated/duration_ms/empty`. Der Konzept-Satz „identisches Basisschema wie bestehende Call-Eintraege plus Error-Felder" wird so interpretiert: **gemeinsame Felder** = `ts/tool/args` (alle drei in beiden Eintragstypen vorhanden), **call-spezifisch** = `lines/truncated/duration_ms/empty` (im Error-Eintrag NICHT vorhanden, weil sie semantisch an das erfolgreiche `CallToolResult`-Ergebnis gebunden sind), **error-spezifisch** = `level/error_type/error_message/stack_trace`. Diese Lesart ist die einzige, die den Konzept-Satz konsistent mit dem `RecordEnd`-Schema macht.

## Intention

Nach diesem Step existiert in `McpCallLog` die neue `RecordError(toolName, args, exception)`-Methode, die einen JSONL-Eintrag mit `level="error"`, Exception-Typ-Name, Exception-Message und auf 4 KB gekapptem Stack-Trace unter demselben `_writeLock` wie `RecordEnd` schreibt. Damit ist die Voraussetzung dafuer geschaffen, dass EPIC-03 (Error-Hook im MCP-Server-Lifecycle) die Methode an Tool-Handler-Wrappern aufrufen kann, ohne dass Call- und Error-Eintraege sich in der Datei zeitlich ueberholen. Die Tests beweisen das JSONL-Schema, die Lock-Serialisierung und den Stack-Trace-Cap; bestehende Tests bleiben unveraendert gruen (DoD 5).

## Konkrete Aenderungen

**Bei `step_type: single`** (Standard-Struktur):

### Datei 1: `src/AiNetLinter/Mcp/McpCallLog.cs` (Zeile 22-127, primaer Erweiterung der Felder + neue Methode)

- **Was:**
  1. Zwei neue private Konstanten einfuegen (neben `MaxArgsLength`/`ArgsEllipsis`, Z. 24-25):
     - `private const int MaxStackTraceLength = 4096;`
     - `private const string StackTraceTruncationMarker = "...";` (Dreifach-Punkt analog `ArgsEllipsis`; Coder-Entscheidung: bei Stack-Traces ist ein expliziter Marker wie `"...[truncated]"` vertretbar — Konsistenz mit `ArgsEllipsis` ist Default-Empfehlung).
  2. Neue Methode `internal void RecordError(string toolName, string args, Exception exception)` direkt nach `RecordEnd` (also nach Z. 88). Pattern exakt wie `RecordEnd` (anonymes Objekt -> `JsonSerializer.Serialize` -> `lock (_writeLock) { ... }`). Schema-Felder: `ts` (UTC ISO-8601, identisch zu `RecordEnd`), `tool` (`toolName`), `args` (mit derselben 200-Zeichen-Trunkierung wie in `RecordEnd`, weil lange Args die Zeile ebenfalls aufblaehen), `level` (= Konstante `"error"`), `error_type` (= `exception.GetType().Name`), `error_message` (= `exception.Message`, **ohne Cap** — Begruendung: die meisten Exception-Messages sind kurz; ein zusaetzlicher Cap wuerde Diagnose-Information verlieren, der Stack-Trace-Cap reicht als Anti-Bloat), `stack_trace` (= `exception.StackTrace ?? string.Empty`, gekappt auf `MaxStackTraceLength` inklusive Marker).
  3. **ArgumentNullException.ThrowIfNull(exception)** am Methodeneingang (exogener Guard, nicht ueber das Result-Pattern — die Methode ist `void`, und ein fehlender `Exception`-Parameter ist ein Programmierfehler, nicht ein erwarteter Fehlerfall).
  4. `_entryCount++` im Lock-Block (analog `RecordEnd`, Z. 86). Begruendung: wenn ein Fehler vor `RecordEnd` auftritt, ist `RecordError` der einzige Eintrag — die Datei darf dann **nicht** durch `DisposeAsync` (Z. 115-125, „`_entryCount == 0` -> File.Delete") automatisch geloescht werden, sonst ist DoD 2 („zusaetzliche JSONL-Zeile mit `level=error` in derselben Datei") unmoeglich.
  5. XML-Doc-Kommentar an `RecordError`: beschreibt **Was** (persistiert unbehandelte Exception) und **Warum-Why** (gleicher Lock wie `RecordEnd`, damit zeitliche Reihenfolge stimmt; 4 KB Cap verhindert Log-Bloat). **Keine** Konzept-/EPIC-/Step-/TD-Verweise (Clean-Code-Kommentar-Politik, `AiNetLinterRichtlinien.mdc` §5).
  6. **Keine Aenderung** an `LogPath`, `EntryCount`, `StartRecording`, `RecordEnd`, `McpCallLogScope`, `DisposeAsync` oder den `McpTruncationResult`-Helper. `LogPath`-Sichtbarkeit bleibt `internal` (Re-Evaluation explizit fuer EPIC-04 zurueckgestellt, siehe step-001-review.md „Sonstige Beobachtungen").
- **Warum:** Erfuellt Muss-Habe 4 (Error-Sink) und ist die Voraussetzung fuer EPIC-03 (Error-Hook-Aufrufe). Pattern-Wiederverwendung aus `RecordEnd` minimiert neues Risiko und macht den Code diff-freundlich.

### Datei 2: `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs` (Erweiterung, keine Modifikation bestehender Tests)

- **Was:** Vier bis fuenf neue `[Fact]`s mit `[Trait("Category", "Unit")]` unterhalb der bestehenden Tests anhengen (nach Z. 148, vor den privaten Helper-Methoden ab Z. 150). Bestehende Helper (`CreateTempLogPath`, `TryDelete`, `ParseSingleEntry`) werden wiederverwendet. Neue Helper nur, wenn unbedingt noetig (siehe Tests-Liste unten).
- **Warum:** DoD 3 (Lock-Reihenfolge) und DoD 4 (Stack-Trace-Cap) sind explizit test-pflichtig; Schema-Validierung verhindert Drift zwischen `RecordError` und dem Konzept-Schema; Interaktionstest mit `RecordEnd` schuetzt vor versehentlicher Aufhebung der zeitlichen Ordnung.

## Tests

- [ ] `RecordError_BasicException_WritesJsonLineWithAllFields` — neuer Test. Erstellt `new InvalidOperationException("something went wrong")` mit synthetischem `StackTrace` (z. B. `"at Foo.Bar() in C:\\\\Foo.cs:line 42"`), ruft `log.RecordError("get_file_skeleton", "args|42", ex)`, liest die JSONL, parst den Eintrag und assertet: `level == "error"`, `error_type == "InvalidOperationException"`, `error_message == "something went wrong"`, `stack_trace` enthaelt den synthetischen Substring, `tool == "get_file_skeleton"`, `args == "args|42"`, `ts` ist nicht leer, und es gibt **keine** Call-spezifischen Felder (`lines`, `truncated`, `duration_ms`, `empty` — als `Assert.False(entry.TryGetProperty("lines", out _))` o. ae.). Verifiziert das JSONL-Schema gemaess Konzept.
- [ ] `RecordError_StackTraceExceeds4KB_TruncatesToCap` — neuer Test. Erstellt Exception mit `new string('a', 100_000)` (100 KB) als `StackTrace`, ruft `RecordError`, liest JSONL und assertet: `stack_trace.Length <= 4096` (DoD 4) und der Wert endet auf dem Truncation-Marker (`StackTraceTruncationMarker`). Verifiziert DoD 4 explizit.
- [ ] `RecordError_AfterRecordEnd_PreservesOrderInJsonl` — neuer Test. Sequenz: `StartRecording("find_symbol", "args")` -> `scope.Complete(McpToolResults.Text("hit"))` (Disposed triggert `RecordEnd`) -> `log.RecordError("find_symbol", "args", ex)`. Liest JSONL, erwartet genau 2 Zeilen, assertet: Zeile 0 ist Call-Eintrag (`entry.GetProperty("tool").GetString() == "find_symbol"`, `entry.TryGetProperty("level", out _) == false`), Zeile 1 ist Error-Eintrag (`level == "error"`). Verifiziert DoD 3 fuer den sequenziellen Fall.
- [ ] `RecordError_BeforeRecordEnd_PreservesOrderInJsonl` — neuer Test (Symmetrie-Test). Sequenz: `log.RecordError(...)` -> `StartRecording` -> `Complete` -> Dispose. Erwartet 2 Zeilen, Zeile 0 = Error, Zeile 1 = Call. Verifiziert DoD 3 in der Gegenrichtung; schuetzt davor, dass `RecordError` versehentlich nach `RecordEnd` einsortiert wird.
- [ ] `RecordError_ParallelCallsDoNotInterleaveJsonLines` — neuer Test. Eine einzige `McpCallLog`-Instanz, 50 Tasks (Mix aus `StartRecording/Complete` und `RecordError`) via `Task.WhenAll`. Liest die resultierenden 100 Zeilen, assertet: **alle 100 Zeilen parsen als valides JSON** (kein Parse-Throw; ohne Lock-Hold wuerden halbe Zeilen entstehen, die `JsonDocument.Parse` scheitern lassen). Verifiziert, dass `_writeLock` atomar ueber mehrere Threads haelt; toleriert beliebige Reihenfolge der Zeilen untereinander (Concurrency-Tests mit Ordnungs-Asserts sind flaky).

**Optional** (nur bei klarer Notwendigkeit, sonst weglassen, um Step-Umfang schlank zu halten):
- [ ] `RecordError_NullStackTrace_HandlesGracefully` — synthetische Exception ohne expliziten `StackTrace` (Default = `null`), assertet dass `stack_trace` als leerer String `""` geschrieben wird (kein NPE, kein `null` im JSON, kein Schema-Bruch). Sinnvoll als Schutz gegen Regressions in .NET-Versionen, wo `Exception.StackTrace` lazy-throw-Verhalten zeigen kann.

**Bestehende Tests (alle unveraendert):** `RecordStart_ThenEnd_WritesJsonLineWithAllFields`, `RecordEnd_TruncatedResult_SetsTruncatedTrue`, `RecordEnd_EmptyResult_SetsEmptyTrue`, `Dispose_NoRecords_DeletesLogFile`, `RecordStart_LongArgs_TruncatedToTwoHundredPlusEllipsis`. Konzept DoD 5 sagt „4 Call-Tests" — tatsaechlich 5, kein Blocker; in EPIC-04-Doku-Sync korrigierbar.

**Helper-Wiederverwendung:** `CreateTempLogPath()`, `TryDelete(path)`, `ParseSingleEntry(lines)`. **Neuer Helper** (nur fuer den Parallel-Test): `private static async Task<string[]> RunParallelLog(int callCount, int errorCount, McpCallLog log)` — erzeugt `Task.WhenAll` mit deterministisch erzeugten `Exception`-Instanzen (z. B. `new InvalidOperationException("err " + i)`) und gibt die gelesenen Zeilen zurueck. Wenn der Coder den Helper inline in den Test schreibt statt ihn zu extrahieren, ist das gleichwertig — kein `MaxMethodLineCount`-Verstoss zu erwarten (Tests haben das Doppel-Limit 100).

## Definition of Done

- [ ] Alle „Konkrete Aenderungen" in `McpCallLog.cs` umgesetzt
- [ ] 4 neue Tests (bzw. 5 inkl. optionalem NullStackTrace-Test) in `McpCallLogTests.cs` ergaenzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) gruen: `dotnet build` mit 0 Warnungen, 0 Fehlern unter `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- [ ] Test-Command aus Tech-Stack-Notiz gruen: `dotnet test` (Volllauf) mit 1270 + N neuen Tests gruen, keine Regression; zusaetzlich schneller Smoke-Check via `dotnet test --filter FullyQualifiedName~McpCallLogTests` und `dotnet test --filter Category=Unit`
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch, imperativ, mit Pflicht-Suffix `[mcp-call-logging-fuer-agenten-analyse]`); Commit-Body mit `Refs: <task-dir>/step-002`; **Commit-Vorschlag-Block** am Ende der Coder-Antwort (Regel `AiNetLinterRichtlinien.mdc` §4)
- [ ] `step-002/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt
- [ ] **DoD-spezifisch** (Konzept):
  - [ ] **DoD 3 abgedeckt:** Tests 3+4 beweisen sequenzielle Reihenfolge Call-Error und Error-Call unter demselben Lock; Test 5 beweist Lock-Hold ueber parallele Tasks.
  - [ ] **DoD 4 abgedeckt:** Test 2 beweist 100 KB Stack-Trace -> `stack_trace.Length <= 4096`.
  - [ ] **DoD 5 (Teilbereich) abgedeckt:** Bestehende 5 `McpCallLogTests` unveraendert; Volllauf gruen; keine neuen Compiler-Warnungen.
- [ ] **Nicht in DoD dieses Steps** (ausdruecklich zurueckgestellt, gehoeren in EPIC-03 bzw. EPIC-04): DoD 1 (Default-Pfad-Existenz), DoD 2 (Error-Hook ruft `RecordError` auf — EPIC-03), DoD 6 (Doku), DoD 7 (Konzept-Status `ready`).

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Clean-Code-Kommentar-Politik: XML-Doc an `RecordError` beschreibt Was/Warum, **keine** Verweise auf `step-002`, `EPIC-02`, `konzept.md` oder Tech-Debt-IDs. „Formatierung ausgelagert, damit…" / „gleicher Lock wie RecordEnd, damit Reihenfolge stimmt" — beide Begruendungen sind Code-lokales *Why*, nicht External-Reference.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Zero-Warning-Direktive: `dotnet build` muss unter `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` 0/0 liefern. Wichtig fuer die `RecordError`-Methode: `ArgumentNullException.ThrowIfNull(exception)` ist die explizit bevorzugte Variante (kein `if (exception is null) throw new ArgumentNullException(...)` per Hand → wuerde wahrscheinlich Lint-Warnung ausloesen wegen manueller Nullpruefung).
- `.agents/rules/AiNetLinterRichtlinien.mdc#2` — Architektur-Verbote eingehalten: kein DI-Container, kein `AssemblyLoadContext`, kein Plugin-System. `McpCallLog` bleibt direkter `StreamWriter` (kein Serilog, keine `Microsoft.Extensions.Logging`, wie in Konzept „Bewusst out of scope" festgehalten).
- `.agents/rules/AiNetLinter.mdc` — Kurz-Stil: `McpCallLog` bleibt `sealed` (ist es schon), `#nullable enable` bleibt in Zeile 1 (ist es schon). `MaxMethodLineCount ≤ 60` (Produktion): `RecordError` wird voraussichtlich ~25-30 Zeilen, deutlich unter Limit. `MaxMethodParameterCount ≤ 4`: `RecordError(toolName, args, exception)` hat 3 Parameter, im Limit. `MaxPublicMembersPerType ≤ 15`: aktuell 4 sichtbare Member (`StartRecording`, `RecordEnd`, `EntryCount`, `LogPath`) + 1 neuer (`RecordError`) = 5, im Limit. `MaxConstructorDependencies ≤ 5`: unveraendert. `EnforceAsciiIdentifiers`: alle neuen Identifier (`RecordError`, `MaxStackTraceLength`, `StackTraceTruncationMarker`) reines ASCII.
- `.agents/rules/AiNetLinter.mdc` — `EnforceNoSilentCatch`: keine neuen `catch`-Bloecke in `RecordError` noetig (kein IO-Error-Handling; analog zu `RecordEnd`, das auch nicht faengt). Wenn der Coder doch einen `catch` einbaut (z. B. fuer „Lock failed, Server soll weiterlaufen"), muss er geloggt + sichtbar machen oder `throw;` — kein leeres Catch. **Empfehlung: kein Catch, konsistent mit `RecordEnd`.**
- `.agents/rules/AiNetLinter.mdc` — `EnforceSemanticNaming`: `RecordError` ist semantisch klar (Verb + Substantiv, passend zu `RecordEnd`); Parameternamen `toolName/args/exception` sprechend.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — xUnit-v3-Pflicht eingehalten: neue Tests verwenden `[Fact]` + `[Trait("Category", "Unit")]`, parallel-faehig (kein `[Collection("...")]`-Zwang, da kein globales `Console.Out`/`Error`-Redirection noetig).

## Bekannte Ausnahmen

- **Stack-Trace-Cap als Char-Cap, nicht Byte-Cap:** 4 KB werden als `MaxStackTraceLength = 4096` (Zeichen) interpretiert, nicht als `Encoding.UTF8.GetByteCount(...)`-Cap. Begruendung: Stack-Traces sind in der Praxis ASCII, das `RecordEnd`-Pattern verwendet ebenfalls Char-Caps (`MaxArgsLength = 200`), und der Char-Cap ist ohne zusaetzliche Encoding-Allokation messbar. Bei einem Test-Input aus `new string('a', 100_000)` (ASCII) sind 4096 Chars = 4096 Bytes = genau 4 KB; DoD 4 ist erfuellt. **Falls** spaeter ein nicht-ASCII-reicher Stack-Trace auftritt und der User den Cap als hartes 4 KB-Byte-Limit interpretiert wissen will, ist das ein EPIC-04-Re-Eval (kein Step-002-Change).
- **Stack-Trace-Truncation-Marker `...` (Dreifach-Punkt) statt `"...[truncated]"`:** Default-Empfehlung ist Konsistenz mit `ArgsEllipsis`. Der Coder darf einen expliziteren Marker waehlen, wenn er die Lesbarkeit im JSONL-Viewer hoeher bewertet — beides ist lint-konform.
- **`error_message` ohne Cap:** Konzept schreibt keinen Cap vor. Bewusste Entscheidung, weil (a) die meisten Exception-Messages kurz sind, (b) ein zusaetzlicher Cap Diagnose-Information zerstoeren wuerde, (c) der 4 KB-Cap auf `stack_trace` bereits die Anti-Bloat-Garantie liefert. Falls der Coder einen Cap moechte, ist das eine begruessete Erweiterung, aber nicht Step-Pflicht.
- **`args`-Trunkierung auf 200 Zeichen + `...` fuer Error-Eintraege:** Konzept schreibt es nicht explizit vor, aber Konsistenz mit `RecordEnd` ist die einzige sinnvolle Lesart („identisches Basisschema plus Error-Felder"). Falls der Coder den Cap fuer Error-Eintraege weglassen will, ist das eine bewusste Abweichung, die er im `step-result.md` unter „Abweichungen vom Plan" dokumentieren muss.
- **Kein neuer Helper `McpTruncationResult`-aehnlich fuer Stack-Trace:** Die Stack-Trace-Truncation ist ein 4-Zeilen-if-Block — Extraktion in einen Helper wuerde gegen `AiNetLinterRichtlinien.mdc` §1 „Einfachheit vor Abstraktion" verstossen. Inline im Lock-Block-Vorbereitungscode.
- **Konzept-Inkonsistenz „4 Call-Tests" vs. tatsaechlich 5 Tests:** In `konzept.md` DoD 5 heisst es „Bestehende `McpCallLogTests` (4 Call-Tests) bleiben unveraendert gruen", in `McpCallLogTests.cs` stehen aber 5 Tests. Kein Blocker fuer diesen Step (die 5 Tests bleiben unveraendert). Korrektur gehoert in EPIC-04-Doku-Sync.
- **Parallel-Test `RecordError_ParallelCallsDoNotInterleaveJsonLines` ist kein Ordnungs-Test:** Assertet nur „alle Zeilen parsen als valides JSON", nicht „Zeile N ist Call, Zeile M ist Error". Ordnungs-Asserts ueber parallele Tasks sind flaky und gehoeren in den sequenziellen Tests 3+4, die deterministisch sind.
- **Tagesrand-anfaelliger Stack-Trace-String:** `new string('a', 100_000)` als `StackTrace`-Setter ist legitim (Exception-Property ist setter-faehig, kein Internal-Only-Feld), aber falls in einer .NET-Version der Setter ploetzlich wegfiele, schlaegt Test 2 fehl. Mitigation: Exception-Instanziierung bleibt im Test lokal; bei Misserfolg kann auf `try { ex.StackTrace = "..."; } catch { /* fallback synthetic exception type */ }` umgestellt werden. Risiko gering (alle aktuellen .NET-Versionen erlauben Setter).

## Code-Skizze (optional)

```csharp
// In McpCallLog.cs, direkt unter RecordEnd (um Z. 88) einzufuegen:

private const int MaxStackTraceLength = 4096;
private const string StackTraceTruncationMarker = "...";

/// <summary>
/// Persistiert einen Fehler-Eintrag in derselben JSONL-Datei wie <see cref="RecordEnd"/>.
/// Schema erweitert Call-Eintrag um level/error_type/error_message/stack_trace; gemeinsame
/// Felder (ts/tool/args) bleiben identisch. Selber Lock wie RecordEnd serialisiert
/// zeitliche Reihenfolge; Stack-Trace wird auf 4 KB gekappt, damit eine einzelne
/// Exception das Log nicht aufblaet.
/// </summary>
internal void RecordError(string toolName, string args, Exception exception)
{
    ArgumentNullException.ThrowIfNull(exception);

    var argsTruncated = args.Length > MaxArgsLength
        ? args[..MaxArgsLength] + ArgsEllipsis
        : args;

    var stackTrace = exception.StackTrace ?? string.Empty;
    if (stackTrace.Length > MaxStackTraceLength)
    {
        stackTrace = string.Concat(
            stackTrace.AsSpan(0, MaxStackTraceLength - StackTraceTruncationMarker.Length),
            StackTraceTruncationMarker);
    }

    var entry = new
    {
        ts = DateTime.UtcNow.ToString("O"),
        tool = toolName,
        args = argsTruncated,
        level = "error",
        error_type = exception.GetType().Name,
        error_message = exception.Message,
        stack_trace = stackTrace,
    };
    var json = JsonSerializer.Serialize(entry);

    lock (_writeLock)
    {
        if (_disposed) return;
        _writer.WriteLine(json);
        _writer.Flush();
        _entryCount++;
    }
}
```

Anmerkungen zur Skizze:
- `string.Concat(ReadOnlySpan<char>, string)` ist seit .NET 6 verfuegbar, vermeidet `Substring`-Allokation; bei Praktikabilitaetsbedenken ersetzbar durch `stackTrace[..cap] + marker`.
- `exception.GetType().Name` (nicht `FullName`) entspricht dem Konzept-Wortlaut „Exception-Typ-Name" (Name, nicht FullName). Falls Vollname gewuenscht: `FullName` einsetzen und im `step-result.md` unter „Abweichungen" notieren.
- `ArgumentNullException.ThrowIfNull` (statische Methode, .NET 6+) ist die moderne, lint-konforme Form; `if (exception is null) throw new ArgumentNullException(nameof(exception));` waere die Legacy-Variante (vermutlich Lint-Warnung „manual null check").

## Notes

- **Re-Evaluation von `McpCallLog.LogPath`-Sichtbarkeit:** `LogPath` ist `internal` (von step-001 als test-only Beobachtbarkeit). `RecordError` ist ebenfalls `internal` (member-Sichtbarkeit erbt von Klasse). **Kein Aktion in diesem Step.** Re-Evaluation der Sichtbarkeit (intern vs. public) bleibt EPIC-04 vorbehalten, wo die Doku-Sync-Diskussion auch die API-Oberflaeche umfasst. Im `step-result.md` und ggf. in einem neuen Tech-Debt-Eintrag explizit festhalten, dass diese Sichtbarkeitsentscheidung getroffen, aber noch nicht endgueltig ist.
- **Was bewusst NICHT geaendert wird:**
  - `McpCallLogScope` (Datei-intern, Z. 133-165) — `RecordError` braucht keinen Scope (kein Stopwatch, kein `Complete`).
  - `DisposeAsync` (Z. 107-126) — die Logik „`_entryCount == 0` -> File.Delete" funktioniert weiterhin korrekt, weil `RecordError` `_entryCount++` macht.
  - `McpTruncationResult` (Z. 173-186) — der Helper ist auf Call-Result-Text-Truncation spezialisiert, nicht auf Stack-Traces; keine Erweiterung noetig.
  - `LogPath` (Z. 55), `EntryCount` (Z. 53) — unveraendert.
  - Kein neuer `using`-Block noetig (`Exception`, `string` sind bereits in Reichweite durch bestehende `using`-Liste; `System.Text.Json` ist ebenfalls schon importiert fuer `JsonSerializer`).
- **Kein neuer File noetig** — alle Aenderungen passen in `McpCallLog.cs` (aktuelle Dateigroesse ~187 Zeilen, weit unter `MaxLineCount = 500`).
- **Wiederverwendete Strukturen** (im Sinne „bestehendes Pattern statt Neubau"):
  - Anonymes-Objekt-Pattern fuer JSONL-Entry (1:1 aus `RecordEnd`).
  - `lock (_writeLock) { if (_disposed) return; _writer.WriteLine(...); _writer.Flush(); _entryCount++; }` (1:1 aus `RecordEnd`).
  - Args-Trunkierung mit `MaxArgsLength` + `ArgsEllipsis` (Konstante, 1:1 aus `RecordEnd`).
  - `DateTime.UtcNow.ToString("O")` (1:1 aus `RecordEnd`).
  - Test-Helper `CreateTempLogPath`, `TryDelete`, `ParseSingleEntry` (aus `McpCallLogTests`).
- **Vorbereitung fuer EPIC-03:** Diese Methode ist die API, die EPIC-03 in den Tool-Wrappern (`SymbolGraphToolRegistrations`/`FileStructureToolRegistrations`/`AnalysisToolRegistrations`/`SymbolBodyToolRegistrations`) aufrufen wird. Die `internal`-Sichtbarkeit passt, weil die Tool-Wrapper im selben Assembly `AiNetLinter` liegen. Kein `public`-Upgrade noetig.
- **Stack-Trace-String-Behandlung:** `.NET 9` setzt `Exception.StackTrace` als normale Property; Setter ist `public` und akzeptiert beliebige Strings. Tests koennen daher synthetische Stack-Traces frei setzen. In Produktion kommt der Wert vom .NET-Runtime-Aufruf, ist also echt.
- **`EnforceNamespaceDirectoryMapping`:** `McpCallLog.cs` liegt in `src/AiNetLinter/Mcp/`, Namespace ist `AiNetLinter.Mcp` — konform. Kein Refactor noetig.
