---
status: blocked
type: step-result
task: mcp-call-logging-fuer-agenten-analyse
step: 002
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
blocker_category: content
---

# Step 002: Result

## Zusammenfassung

`McpCallLog.RecordError(tool, args, exception)` wurde nach Plan-Vorgabe
umgesetzt: ArgumentNullException-Guard, 200-Zeichen-Args-Trunkierung
analog `RecordEnd`, 4 KB Stack-Trace-Cap per `string.Concat(span, marker)`,
JSONL-Schema-Erweiterung um `level=error / error_type / error_message /
stack_trace`, identische `_writeLock`-Serialisierung wie `RecordEnd`. 5
neue Unit-Tests in `McpCallLogTests.cs` decken Schema, Stack-Cap,
Lock-Reihenfolge in beide Richtungen und parallele Aufrufe ab — alle
grün. **Step ist blockiert**, weil die +45 Zeilen in `McpCallLog.cs`
fünf andere Klassen über deren `AIContextFootprint`-PathOverrides in
`rules.json` treiben und damit die Dogfooding-Integration
`RunLinterCli_OnWholeSolution_ReturnsSuccess` (und nur diese) rot wird.
Fix liegt außerhalb des Step-Scopes (`rules.json` ist im Plan nicht
gelistet), der Planer muss entscheiden, ob er den Threshold-Bump in
diesen Step zieht oder als eigenen Micro-Step anlegt.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/McpCallLog.cs` — zwei neue Konstanten
  (`MaxStackTraceLength = 4096`, `StackTraceTruncationMarker = "..."`)
  neben den bestehenden `MaxArgsLength/ArgsEllipsis`; neue Methode
  `RecordError(string toolName, string args, Exception exception)`
  mit XML-Doc, exogenem `ArgumentNullException`-Guard, Args-Trunkierung,
  Stack-Trace-Cap, JSONL-Serialisierung und `_writeLock`-Block analog
  `RecordEnd`. Kein Eingriff in `LogPath`, `EntryCount`, `StartRecording`,
  `RecordEnd`, `McpCallLogScope`, `DisposeAsync` oder `McpTruncationResult`.
- `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs` — 5 neue
  `[Fact]`-Tests mit `[Trait("Category", "Unit")]`:
  - `RecordError_BasicException_WritesJsonLineWithAllFields`
  - `RecordError_StackTraceExceeds4KB_TruncatesToCap`
  - `RecordError_AfterRecordEnd_PreservesOrderInJsonl`
  - `RecordError_BeforeRecordEnd_PreservesOrderInJsonl`
  - `RecordError_ParallelCallsDoNotInterleaveJsonLines`
  - Private Hilfsklasse `TestException` mit Reflection-Setter auf
    `Exception._stackTraceString` (siehe "Abweichungen vom Plan").
  - Bestehende 5 Tests und Helper unverändert.

## Commits

- **Code-Commit:** `c3fe3c5f8eb5bb695c4b7e160a76f662cd51eb6e` —
  `feat: McpCallLog.RecordError mit JSONL-Error-Schema und 4 KB Stack-Cap [mcp-call-logging-fuer-agenten-analyse]`
- **Doku-Commit:** siehe unten (im Anschluss).

## Build- und Test-Output

- `dotnet build` — 0 Warnungen, 0 Fehler, ~2 s
- `dotnet test --filter FullyQualifiedName~McpCallLogTests` — 10/10 grün (5 alt + 5 neu, 142 ms)
- `dotnet test` (Volllauf) — **1274/1275 grün, 1 Failure**:
  - `AiNetLinter.Tests.Cli.CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`
    — `Assert.Contains("OK", result.Output)` schlägt fehl, Linter meldet
    stattdessen `# AiNetLinter - 5 violations` (5 × `AIContextFootprint`,
    siehe "Beobachtungen" unten). Dauer 2 min 7 s.

## Abweichungen vom Plan

1. **`Exception.StackTrace` ist in .NET 10 nicht mehr setzbar.**
   Der Plan ging davon aus, dass die Property per Objekt-Initialisierer
   setzbar ist (`new InvalidOperationException("...") { StackTrace = "..." }`).
   Tatsächlich ist `StackTrace` seit .NET 10 eine get-only `virtual`
   Property ohne `override`-fähigen Setter (`error CS0546`). Mitigation:
   private nested Klasse `TestException` mit Reflection-Setter auf
   `Exception._stackTraceString` (per `BindingFlags.Instance |
   NonPublic`). `StackTraceStringField` wird statisch gecached; ein
   `InvalidOperationException`-Throw beim Klassen-Init fängt
   zukünftige .NET-Änderungen ab. Test-API-Aufruf:
   `new TestException("...")` + `ex.SetStackTrace("...")`. Der Plan
   dokumentiert diese Eventualität explizit ("Tagesrand-anfälliger
   Stack-Trace-String" + "fallback synthetic exception type" im
   Abschnitt "Bekannte Ausnahmen"), daher im Plan-Spirit.
2. **`RecordError_AfterRecordEnd_PreservesOrderInJsonl` musste den
   Scope explizit disposen.** Erste Iteration nutzte
   `await using var scope = log.StartRecording(...)` (deferred Dispose),
   wodurch `RecordEnd` erst NACH `RecordError` lief und die
   Reihenfolge umgekehrt war. Fix: `var scope = log.StartRecording(...)`
   + expliziter `await scope.DisposeAsync()` zwischen `Complete()` und
   `RecordError(...)`. Reihenfolge jetzt deterministisch korrekt.
3. **`error_type` heißt im Basic-Test `TestException` statt
   `InvalidOperationException`.** Konsequenz aus Abweichung 1: weil
   die Tests `TestException` (statt einer Built-in-Exception) nutzen,
   landet `TestException` im `error_type`-Feld. Assertion entsprechend
   angepasst. In Produktion ruft EPIC-03 weiterhin `Exception`-
   abgeleitete Typen auf, dort erscheint der echte Typ-Name.

## Beobachtungen (nicht Teil dieses Steps, ggf. Tech-Debt)

### AIContextFootprint-Regression in 5 Konsumenten von `McpCallLog`

**Root Cause:** `McpCallLog.cs` wuchs um 45 Zeilen (+24 %).
`AIContextFootprint` zählt die transitiven Source-Lines aller
Typen-Abhängigkeiten. Die fünf Klassen, die `McpCallLog` per Parameter
nutzen, schlucken den Zuwachs in ihre jeweilige Footprint-Summe:

| Datei | Footprint | Limit (rules.json) | Über Limit |
|---|---:|---:|---:|
| `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` | 2846 | 2800 | +46 |
| `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` | 2869 | 2830 | +39 |
| `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` | 2818 | 2800 | +18 |
| `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs` | 2802 | 2800 | +2 |
| `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | 2912 | 2870 | +42 |

**Verifiziert:** Ohne meine Änderungen (per `git stash`) meldet der
Linter `# AiNetLinter - 0 violations` (OK), das entspricht step-001's
"1270/1270 grün". Mit Änderungen: `# AiNetLinter - 5 violations`,
alle fünf in obiger Tabelle. Kausalität eindeutig.

**Plan-Bewertung:** Der Plan listet in §Rules-Refs nur Lint-Limits
für `McpCallLog` selbst (`MaxPublicMembersPerType`, `MaxMethodLineCount`,
`MaxMethodParameterCount`, `EnforceAsciiIdentifiers` …), nicht für
dessen transitive Konsumenten. Das `AIContextFootprint`-Limit wird in
`rules.json` über `PathOverrides` pro Datei gesetzt und ist im Plan
nicht erwähnt. Konsequenz: der Plan konnte die zu erwartende
Reichweite der +45 Zeilen nicht abschätzen — der geschätzte Risk
`low` traf nicht zu, real war er `medium` (ein einziger
downstream-Test, aber außerhalb des Datei-Scopes).

**Vorgeschlagene Fixes** (zur Auswahl durch Planer/Nutzer):

- **A) PathOverrides anheben (5 Zahlen in `rules.json`):** minimal
  invasiv, deckt den genauen Mehrtopf ab. Beispiel für
  `AnalysisToolRegistrations.cs`: `2800` → `2850` (oder
  `AIContextFootprint=0` für diese Datei, was den Check deaktiviert
  — die Default-Grenze 2500 ist ohnehin für die Datei wirkungslos).
  Reihenfolge im `PathOverrides`-Block ist alphabetisch.
- **B) `RecordError` in eigene Datei auslagern** (z. B.
  `McpCallLog.RecordError.cs` als partial class, oder als
  statischer Helper in einer neuen Datei mit eigenem Namespace).
  Die fünf Konsumenten importieren `McpCallLog` (für `RecordEnd`),
  aber nicht zwingend `RecordError`; deren Footprint bliebe
  unverändert. **Kosten:** widerspricht dem Plan ("Kein neuer
  File noetig", "1:1 Pattern aus RecordEnd"). **Nutzen:** löst das
  Problem ohne `rules.json`-Änderung.
- **C) `McpCallLog` in Sub-Dateien partial-splitten,** sodass
  `RecordEnd` und `RecordError` in getrennten Files liegen. Ähnlich
  wie B, aber invasiver.

**Empfehlung an Planer:** Variante A (5 Zahlen in `rules.json`),
weil sie der bisherigen Wartungspraxis im Projekt entspricht
(PathOverrides wurden in der Vergangenheit mehrfach an
Code-Wachstum angepasst, siehe `git log -p -- rules.json`).
Variante B wäre architektonisch sauberer, ist aber Step-Scope-Shift.

### Sonstige Beobachtungen

- **Dogfooding-Test `RunLinterCli_OnWholeSolution_ReturnsSuccess`
  ist Single-Point-of-Failure für Lint-Regressionen:** Ein
  `McpCallLog`-Edit (oder ein Edit in einer seiner Dependencies)
  propagiert sofort in den Lint-Output dieses einen Tests. Ein
  Per-File-Footprint-Check (oder ein Lint-Diff gegen einen Baseline-
  Snapshot) würde robustere Diagnose liefern. Nicht in EPIC-02
  relevant, nur als generelle Beobachtung für den Tech-Debt-Pool.
- **`McpCallLog.LogPath`-Sichtbarkeit (`internal`):** unverändert
  gelassen, wie im Plan vorgesehen. Bleibt für EPIC-04 als
  Re-Evaluationspunkt offen, mit den Public-API-Diskussionen
  zusammen.
- **TestException als private nested class in `McpCallLogTests`:**
  bewusst nicht promoted zu einer geteilten Test-Utility, weil sie
  nur in dieser Datei gebraucht wird. Verletzt keine
  Test-Overrides (Tests dürfen `MaxMethodLineCount ≤ 100` haben,
  und die Klasse ist sehr klein).
- **Step-Plan §Rules-Refs erwähnt `MaxPublicMembersPerType ≤ 15`
  (Plan-Zitat: "4 sichtbare Member + 1 neuer = 5, im Limit"):**
  Verifikation nach Implementierung: 4 (`StartRecording`,
  `RecordEnd`, `EntryCount`, `LogPath`) + 1 (`RecordError`) = 5
  sichtbare Member. Limit eingehalten.

## Bekannte Unschärfen

- **5 Dateien am Lint-Limit, +45 Zeilen:** Der Linter-Output
  (siehe Tabelle) zeigt unterschiedliche Über-Limit-Werte (2 bis 46),
  obwohl alle 5 Dateien dieselbe +45 Zeilen aus `McpCallLog.cs`
  transitiv mitbekommen. Wahrscheinlichste Erklärung: der Linter
  zählt nicht nur Zeilen, sondern Tokens oder eine gewichtete
  Größe, deren Faktor je nach Klassen-Inhalt variiert. Für den
  Fix bedeutet das: einheitlicher Zuschlag von +50 pro
  PathOverride ist eine sichere Untergrenze; konservativ
  +100 wenn man den nächsten Wachstumszyklus mit abdecken will.
- **TestException-Reflection auf `_stackTraceString`:** in
  zukünftigen .NET-Versionen könnte das interne Field umbenannt
  oder entfernt werden. Der statische `StackTraceStringField`-Cache
  wirft beim ersten Klassen-Load eine `InvalidOperationException`
  mit klarem Hinweis, falls das Field fehlt — Test-Failure ist
  also offensichtlich, nicht stumm. Mitigation wäre dann z. B.
  `try { ex.StackTrace = "..."; } catch { /* fallback */ }` (im
  Plan §"Bekannte Ausnahmen" erwähnt).
- **Plan-Inkonsistenz "4 Call-Tests" vs. tatsächlich 5:** weiterhin
  ungelöst, in EPIC-04-Doku-Sync zu korrigieren (im Plan
  §"Bekannte Ausnahmen" dokumentiert, nicht in diesem Step).

## Modell-Info

- `coded_by_model`: MiniMax-M3
- `coded_by_model_knowledge_cutoff`: 2026-01
