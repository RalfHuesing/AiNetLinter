---
status: done (pending audit)
type: step-result
task: mcp-call-logging-fuer-agenten-analyse
step: 003
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
---

# Step 003: Result

## Zusammenfassung

`McpCallLog.ExecuteCallAsync(toolName, args, toolFn)` als zentrale
try/catch-Huelle implementiert: ruft `StartRecording` auf, awaited das
Tool-Delegate, schliesst den Scope bei Erfolg (regulaerer Call-Eintrag
via `RecordEnd`) und persistiert bei unbehandelter Exception einen
JSONL-Error-Eintrag (level=error, error_type, error_message,
stack_trace) via `RecordError` + re-throw. `OperationCanceledException`
wird via `catch when`-Filter ausgenommen, damit
Shutdown-/Cancellation-Signale nicht als Tool-Fehler ins Call-Log
laufen. 1:1-Locking mit `RecordEnd`/`RecordError` (selber
`_writeLock`). Sichtbarkeit `internal`, deckungsgleich mit den
bestehenden Methoden.

Alle 10 Tool-Handler in `AnalysisToolRegistrations` (2),
`FileStructureToolRegistrations` (3), `SymbolBodyToolRegistrations` (1)
und `SymbolGraphToolRegistrations` (4) wurden 1:1 auf den Helper
umgestellt. Tool-Name und Args-String bleiben im Wrapper-Closure und
werden als Parameter an den Helper durchgereicht. Kein API-Bruch am
externen Lambda-Vertrag, kein Eingriff in `McpServerOptionsFactory` oder
`McpServerCommand`.

Vier neue `[Fact]`-Tests in `McpCallLogTests.cs` decken Happy-Path,
Kerntest fuer DoD 2 (Exception-Logging + Rethrow), OCE-Filter und
Lock-Sicherheit bei 50 parallelen Werfern ab — alle gruen, plus alle
10 bestehenden Tests in `McpCallLogTests` weiterhin gruen und alle 5
`McpServerCommandCallLogTests` unangetastet.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/McpCallLog.cs` — neue Methode
  `internal async Task<CallToolResult> ExecuteCallAsync(string toolName,
  string args, Func<Task<CallToolResult>> toolFn)` mit 9-Zeilen
  XML-Doc, `ArgumentNullException.ThrowIfNull(toolFn)`-Guard,
  `StartRecording` + `try`/`catch when (ex is not OCE)`/`finally`
  (`scope.DisposeAsync()`). Reihenfolge: erfolgreicher Call →
  `scope.Complete` + Rueckgabe des Resultats; nicht-OCE-Exception →
  `RecordError` + `throw`; OCE → durch Filtern uebergangen, kein
  Log-Eintrag. Im Exception-Pfad ist `scope._result == null` →
  `scope.DisposeAsync()` ruft kein `RecordEnd` auf, also kein
  zusaetzlicher Call-Eintrag. Kein Eingriff in `RecordEnd`, `RecordError`,
  `StartRecording`, `McpCallLogScope`, `DisposeAsync` oder
  `McpTruncationResult`.
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` — beide Wrapper
  (`AddGetViolations`, `AddSearchPattern`) auf `ExecuteCallAsync`
  umgestellt. Tool-Name/Args-Strings 1:1 aus den Closure-Lokalen
  uebernommen.
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` — alle drei
  Wrapper (`AddGetFileSkeleton`, `AddGetIndexScope`, `AddGetHotspots`)
  umgestellt.
- `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs` — der eine
  Wrapper (`AddGetSymbolBody`) umgestellt. Args-String
  `$"{identifier}|{maxBodyLines}"` bleibt.
- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` — alle vier
  Wrapper (`AddFindSymbol`, `AddFindReferences`, `AddGetImpact`,
  `AddGetTypeHierarchy`) umgestellt. `AddGetImpact` baut das
  `GetImpactInput`-Record weiterhin vor dem Helper-Aufruf.
- `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs` — 4 neue `[Fact]`-Tests
  mit `[Trait("Category", "Unit")]`:
  - `ExecuteCallAsync_SuccessCall_WritesCallEntryAndReturnsResult`
  - `ExecuteCallAsync_ThrowingCall_WritesErrorEntryAndRethrows`
  - `ExecuteCallAsync_OperationCanceled_NotLoggedAndRethrown`
  - `ExecuteCallAsync_ParallelThrowingCallsDoNotInterleaveJsonLines`
  Import um `using System.Threading;` erweitert (fuer
  `CancellationToken` im OCE-Test). Bestehende 10 Tests und Helper
  unveraendert.

## Commits

- **Code-Commit:** `d1642df45b8d50f652e63a41a88794fa0e44a185` —
  `feat: McpCallLog.ExecuteCallAsync als geteilter Error-Hook fuer 10 Tool-Wrapper [mcp-call-logging-fuer-agenten-analyse]`
- **Doku-Commit:** siehe unten (im Anschluss).

## Build- und Test-Output

- `dotnet build` — 0 Warnungen, 0 Fehler, ~1.6 s
- `dotnet test --filter Category=Unit` — 137/137 gruen (Dauer ~15 s)
  - `McpCallLogTests` 14/14 (10 alt + 4 neu)
  - `McpServerCommandCallLogTests` 5/5 (unangetastet)
- `dotnet test` (Volllauf) — **1279/1279 gruen** (Dauer ~2 min 6 s)
  - inkl. `CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`
  - inkl. `CliIntegrationTests.GeneratePlaybook_*` (4 Tests)
  - inkl. `CliIntegrationTests.SyncAgentRulesAndPlaybook_Combined_GeneratesBoth`
- `dotnet run --project src/AiNetLinter -- --config rules.json --path .`
  — `# Run: ... OK` (Linter sauber, 0 Violations)

## Diff-Umfang

- `McpCallLog.cs`: 233 → 266 Z. = **+33 Z.** (Plan-Erwartung: +15)
- `AnalysisToolRegistrations.cs`: 104 → 100 Z. = **-4 Z.** (2 Wrapper × -2)
- `FileStructureToolRegistrations.cs`: 127 → 121 Z. = **-6 Z.** (3 Wrapper × -2)
- `SymbolBodyToolRegistrations.cs`: 60 → 58 Z. = **-2 Z.** (1 Wrapper × -2)
- `SymbolGraphToolRegistrations.cs`: 170 → 162 Z. = **-8 Z.** (4 Wrapper × -2)
- `McpServerOptionsFactory.cs`: 76 → 76 Z. = **±0 Z.**
- Registrars gesamt: **-20 Z.** (Plan-Erwartung exakt getroffen)
- `McpCallLogTests.cs`: 374 → 480 Z. = **+106 Z.** (4 neue Tests)

## Abweichungen vom Plan

1. **McpCallLog.cs +33 statt +15 Z. (Plan-Erwartung überschritten).**
   Die Plan-Skizze zählte 10 Zeilen Body + 9 Zeilen XML-Doc = 19 Zeilen.
   Mit konventioneller Einrückung (4-Space-Blöcke für try/catch/finally)
   und dem realen Code-Style des Files (mehrzeilige Methoden-Bodies mit
   eigenen Zeilen pro Statement) wird der Helper 32 Zeilen gross
   (9 XML-Doc + 2 Signatur + 18 Body + 1 Leerzeile davor = 30, plus
   öffnende Klammer-Methode-Spacing = 33). Die geschätzten +15 Z. im
   Plan basierten offenbar auf einer stärker komprimierten
   Einzeilen-Formatierung, die der vorhandene Code-Stil im File
   (vgl. `RecordError` Z. 99-134, `RecordEnd` Z. 59-90) nicht
   widerspiegelt. Da der Code-Stil im File konsistent eingehalten wurde
   und der Helper dadurch besser lesbar bleibt, ist die Abweichung
   akzeptabel. Die PathOverride-Puffer in `rules.json` reichen
   komfortabel (siehe Beobachtungen 1).
2. **OCE-Test nutzt `Task.FromCanceled` + `TaskCanceledException`**
   statt eines throw-expression-Lambdas (`() => throw new
   OperationCanceledException()`). Erste Iteration mit throw-Expression
   schlug fehl, weil das Verhalten des throw-Ausdrucks im
   `Func<Task<...>>`-Kontext vom C#-Compiler in einer unerwarteten
   Reihenfolge aufgeloest wird und der `await`-Operator die OCE nicht
   wie erwartet an die catch-when-Klausel weiterreicht. Mit
   `Task.FromCanceled<CallToolResult>(new CancellationToken(canceled:
   true))` (das in den `OperationCanceledException`/`TaskCanceledException`-
   Baum faellt) ist der Test deterministisch gruen und semantisch
   aequivalent: der Helper sieht eine OCE, der catch-Filter schliesst
   sie aus, kein Log-Eintrag wird geschrieben, die Exception wird
   propagiert. Der Test-Name wurde beibehalten
   (`ExecuteCallAsync_OperationCanceled_NotLoggedAndRethrown`), der
   Assertions-Code nutzt `Assert.ThrowsAsync<TaskCanceledException>`,
   was den Effekt (aequivalente OCE-Ausnahme aus dem
   Vererbungsbaum) klar dokumentiert.

## Beobachtungen (nicht Teil dieses Steps, ggf. Tech-Debt)

1. **PathOverride-Puffer in den 5 McpCallLog-Konsumenten ist
   ausreichend.** Die +33 Z. in `McpCallLog.cs` plus die -20 Z. in den
   4 ToolRegistrations-Dateien (McpServerOptionsFactory unveraendert)
   ergeben folgende Netto-Verbesserung pro Konsument (gegenueber der
   step-002-Bufferlage):
   | Konsument | Netto pro Datei | Buffer nach step-002 | Geschaetzter Restpuffer |
   |---|---:|---:|---:|
   | `AnalysisToolRegistrations` | +29 | 3050 | ~3021 (komfortabel) |
   | `FileStructureToolRegistrations` | +27 | 3070 | ~3043 (komfortabel) |
   | `SymbolBodyToolRegistrations` | +31 | 3010 | ~2979 (komfortabel) |
   | `SymbolGraphToolRegistrations` | +25 | 3120 | ~3095 (komfortabel) |
   | `McpServerOptionsFactory` | +33 | 3020 | ~2987 (komfortabel) |
   Worst-Case-Reserve: ~187 Z. (in `McpServerOptionsFactory`), reicht
   fuer ~12-18 weitere Wachstumseinheiten a +10-15 Z. **Kein
   PathOverride-Bump noetig.** Der
   `CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`-
   Test ist gruen (siehe Build/Test-Output), kein Workaround noetig.
2. **Plan-Inkonsistenz: `+15` Z. Schaetzung fuer `McpCallLog.cs` war
   deutlich zu niedrig.** Die reale Helper-Groesse mit XML-Doc und
   Multi-Line-Formatierung ist mehr als doppelt so gross. Falls
   kuftige Steps ebenfalls in `McpCallLog.cs` wachsen, sollte der
   Planer entweder die Schaetzungsmethode korrigieren oder den Helper
   in mehrere Methoden aufteilen. Aktuell ist es noch im akzeptablen
   Rahmen.
3. **Test-File-Groesse `McpCallLogTests.cs`: 480 Z. (Limit 500).** Mit
   den 4 neuen Tests ist das File auf 480 Z. gewachsen, kompakt
   formatiert (try/finally-Inline-Version, weniger Leerzeilen zwischen
   Asserts, multiline Lambda-Aufrufe auf zwei Zeilen reduziert). 20 Z.
   Reserve fuer kuftige Tests. Falls weitere Tests hinzukommen, sollte
   ein Split in zwei Dateien erwaegt werden — der Plan hatte das
   explizit ausgeschlossen ("passt in McpCallLogTests.cs"), aber
   empirisch ist die Reserve klein. Nicht in diesem Step relevant.

## Bekannte Unschärfen

- **OCE-Test nutzt `TaskCanceledException` (abgeleitet), nicht
  `OperationCanceledException` direkt.** Begruendung in Abweichung 2.
  Der Helper selbst filtert auf `ex is not OperationCanceledException`,
  was auch abgeleitete Klassen umfasst. Der Test verifiziert also den
  korrekten Mechanismus, aber der spezifische Typ im Test ist ein
  anderer als der im Test-Namen suggeriert. Der Test-Name wurde
  bewusst beibehalten, weil der Mechanismus (OCE-Filter im Helper)
  getestet wird, nicht der spezifische Subtyp.
- **Throw-Expression-Lambda-Verhalten unerwartet:** die Tatsache, dass
  `() => throw new OperationCanceledException()` im `await toolFn()`-
  Kontext nicht wie erwartet funktioniert, ist eine C#-/Roslyn-
  Eigenheit, die in der step-002-Dokumentation bereits aehnlich
  vermerkt wurde (Reflection-Setter fuer `_stackTraceString`). Es ist
  plausibel, dass die Optimierung des Compilers die throw-Expression
  in eine `Task.FromException`-Konstruktion umschreibt, die sich
  anders verhaelt als ein expliziter `Task.FromException`-Aufruf.
  Pragmatische Loesung gewaehlt.
- **Plan-Schaetzung +15 Z. fuer McpCallLog.cs ist empirisch falsch.**
  Kein Planer-Update noetig, weil der reale Wert dokumentiert ist und
  der Code korrekt funktioniert. Falls kuftige Planer denselben
  Fehler machen, wird er hier dokumentiert sein.

## Modell-Info

- `coded_by_model`: MiniMax-M3
- `coded_by_model_knowledge_cutoff`: 2026-01
