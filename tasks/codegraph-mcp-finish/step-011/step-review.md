---
status: done
type: step-review
task: codegraph-mcp-finish
step: 011
epic: EPIC-06
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-04T13:55:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 011: Robuste McpLintConsole (B.6) + E2E-JSON-RPC-Framing-Test (B.6) + Opt-in --mcp-log Call-Log (B.7)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Fix-Step `step-011/fix-<XX>` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung
Beide Sub-Bereiche vollständig umgesetzt: B.6 liefert `McpLintConsole` in `Output/` als `internal sealed` Singleton (`WriteLine`+`WriteError` → stderr) plus E2E-Framing-Test mit echtem Subprozess und rohem stdout-Read (2 Tests, Integration-Kategorie, `>= 2`-Schwelle begründet gelockert); B.7 liefert `--mcp-log` als vollständiges CLI-Pattern (`LinterArgs`/`CliOptions`/`CliCommandBuilder`/`CliOptionFactory`/`Program.cs`), `McpCallLog` in `Mcp/` mit JSONL-Format + Lock + `IAsyncDisposable`-Scope + Auto-Delete bei 0 Einträgen, Verdrahtung über `McpServerOptionsFactory.Create` und 9 mechanische Lambda-Wrapper (4+3+2) in den drei `*Registrations.cs` mit `if (callLog is null)`-Fast-Path. Alle 5 dokumentierten Abweichungen plausibel und gut begründet (Scope-`Complete`-Pattern, Sub-String-Marker statt literal-Match, Refactor für `MaxMethodLineCount`, Pfad-Auflösung im Aufrufer, `>= 2`-Schwelle).

### Rules-Konformität
`AiNetLinterRichtlinien.mdc` §1-§6 durchgängig eingehalten: `sealed` auf `McpLintConsole`+`McpCallLog`+`McpCallLogScope`+`McpTruncationResult`, Methoden ≤ 60 Zeilen (längste: `RecordEnd` ~31, `DisposeAsync` ~24, alle `Add*`-Wrapper ~28-30), `MaxMethodParameterCount: 4` überall (größte Signatur: `McpCallLog.RecordEnd(scope, result)` = 2), `#nullable enable` in allen 5 neuen Dateien, `sealed` für konkrete Klassen, keine Task-/Planungsartefakt-Refs in Code-Kommentaren (verifiziert per Grep auf `step-011|TD-005|TD-007|EPIC-06|B.6|B.7` → leer; einziger „ehemalige"-Treffer in `GetViolationsScanner.cs:192` ist TD-008, bereits vorher existent). `AiNetLinter.mdc` Kurz-Stil + agent-resilience + general-Regeln eingehalten: `EnforceNoSilentCatch` in `DisposeAsync` (try/catch um `File.Delete`), `BanAsyncVoid` (kein `async void`), `EnforceAsciiIdentifiers` (alle Bezeichner ASCII, Umlaute sind nur in Strings/Kommentaren), `EnforceSemanticNaming` (sprechende Methodennamen). `MaxAIContextFootprint ≤ 2500` für `McpCallLog.cs` (185 Zeilen) und `McpLintConsole.cs` (23 Zeilen) eingehalten; für die 4 Registrations-Factorys sind die 4 PathOverride-Werte (2800/2810/2800/2850) durch das transitive Pull-in aus `ModelContextProtocol.Protocol.CallToolResult` + `System.Text.Json` + `McpCallLog` begründet — Selbst-Lint bestätigt 0/0 Violations, Werte korrekt. Der Forward-Looking-Marker in `McpServerOptionsBuilder.cs:11-13` (Hinweis auf `--mcp-log`-State als „künftige P0/P1-Erweiterung") wurde bewusst nicht angefasst — der Plan hat das ausdrücklich als optional + §5-konform markiert, der Coder hat die Entscheidung dokumentiert.

### Logische Korrektheit
B.6: `McpLintConsole` leitet `WriteLine`+`WriteError` beide nach `Console.Error` um — `Program.cs:43` aktiviert das Singleton im Produktionspfad, `McpServerCommand.RunAsync` bleibt mode-agnostisch (`ILintConsole?` Default-Pattern erhalten, Tests können weiter `TestLintConsole` injizieren). E2E-Test liest roh vom `process.StandardOutput` (kein SDK-Parser dazwischen), prüft jede Zeile mit `JsonDocument.Parse` + `jsonrpc==2.0`-Check — der Test schlägt an, sobald ein einziger `Console.WriteLine`-Leak auf stdout landet. B.7: `McpCallLog` schreibt valides JSONL (verifiziert: jeder Eintrag ein vollständiges JSON-Objekt mit allen 7 Konzept-Feldern `ts`/`tool`/`args`/`lines`/`truncated`/`duration_ms`/`empty`), Trunkierung via Sub-String-Match auf `"Treffer gesamt, "` / `"Dateien mit Textfund, "` (robuster als literal-Match gegen konkrete Zahlen), `DisposeAsync` löscht leere Files (`_entryCount == 0` → `File.Delete` mit `try/catch (IOException)` + stderr-Logging), thread-safe via `Lock _writeLock` (Schreib-Triplett atomar). `if (callLog is null)`-Fast-Path in den 9 Wrapper-Lambdas garantiert Null-Overhead bei deaktiviertem Log (kein `StartRecording`-Aufruf, kein Scope-Allocation). Dispose-Pattern zweiphasig (Flag-Set unter Lock → I/O außerhalb) ist korrekt: nach `_disposed = true` blockt `RecordEnd` jeden weiteren Write, StreamWriter-Cleanup außerhalb des Locks ist sicher. Pfad-Auflösung `ResolveMcpLogPath` ist sauber: absolut → 1:1, relativ → `Path.GetFullPath(Path.Combine(solutionDir, mcpLogPath))` (mit `GetFullPath` zur Separator-Normalisierung). Nicht-getestete Edge-Cases (lange `..`-Pfade, UNC-Roots, hohe parallele Last) sind explizit im `step-result.md` Abschnitt „Bekannte Unschärfen" vermerkt und nicht im Scope — vertretbar.

### Konzept-Treue (Ebene 4)
`konzept.md` Muss-Haben B Punkt 6: vollständig erfüllt — eigene `McpLintConsole`-Implementierung mit `WriteLine` → stderr + E2E-Test, der jede stdout-Zeile einer Tool-Call-Sequenz als gültigen JSON-RPC-Frame verifiziert. Muss-Haben B Punkt 7: vollständig erfüllt — schlankes Call-Log mit allen 7 spezifizierten Datenfeldern (Zeitstempel ✓, Tool ✓, gekürzte Parameter ✓, Ergebniszeilen ✓, Trunkierung ja/nein ✓, Dauer ✓, Leermenge ja/nein ✓), Default aus (kein File I/O ohne Flag) ✓, Ablage neben `cache/` (relativ zur Solution, gemäß Konzept-Wortlaut; der Plan hat zwischen `exeDir` und `solutionDir` abgewogen und sich für Solution begründet — die Konzept-Zeile „neben `cache/`" ist mehrdeutig, die gewählte Interpretation ist die semantisch konsistentere). DoD Z. 650-653: „Alle sieben Punkte aus Muss-Haben B umgesetzt, reviewt, mit Integrationstest abgesichert" — nach step-011 sind B.1-B.7 abgehakt, alle mit Integrationstest (B.6: 2 E2E; B.7: 11 Unit-Tests; B.1-B.5 schon vorher grün, im Volllauf 1215/1215 bestätigt). Non-Goals eingehalten: keine Änderung an Tool-Implementierungen (`FindSymbolTool.cs` etc. unangetastet), keine Doku-/Config-Schema-Änderungen außerhalb B.6/B.7, kein TD-Mitnahme-Scope-Verstoß (TD-001/TD-006/TD-008 weiterhin offen wie geplant).

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx          → grün (0 Warnungen, 0 Fehler, 2.3 s)
dotnet test  AiNetLinter.slnx --no-build → grün (1215/1215 Tests, 3 m 4 s, kein TD-005-Flake)
dotnet test  --filter "FullyQualifiedName~JsonRpcFraming"  → 2/2 in 4 s
dotnet test  --filter "FullyQualifiedName~McpCallLog"       → 5/5 in 101 ms
dotnet test  --filter "FullyQualifiedName~McpLintConsole"   → 3/3 in 18 ms
dotnet test  --filter "FullyQualifiedName~CallLog"          → 11/11 in 93 ms
dotnet run --project src\AiNetLinter -- --config rules.json --path .  → OK (0 Violations)
```

## Sonstige Beobachtungen / MINOR / NITPICK

- **Forward-Looking-Marker in `McpServerOptionsBuilder.cs:11-13`** ist nach B.7 inhaltlich teilweise obsolet (`--mcp-log`-State ist jetzt umgesetzt) — Plan hat diese Stelle explizit als §5-konform + optional markiert, Coder hat korrekt entschieden sie nicht anzufassen. Empfehlung: bei nächster Berührung des Builders den Kommentar auf „Call-Log-State umgesetzt in step-011; weitere P0/P1-Erweiterungen denkbar" präzisieren — Aufwand 1 Min., kein Finding.
- **12 → 16 PathOverrides** in `rules.json → PathOverrides` — alle 4 neuen (SymbolGraph/FileStructure/Analysis/McpServerOptionsFactory) sauber durch den B.7-Pull-in aus `ModelContextProtocol.Protocol.CallToolResult` + `McpCallLog` + `System.Text.Json` begründet. Coder hat den Footprint korrekt gemessen (2789/2803/2753/2735) und die Override-Werte mit Sicherheitsmarge (2850/2810/2800/2800) eingetragen. Bestätigt durch 0-Violation-Selbst-Lint. Beobachtung der Coder: Konzept-Punkt C (`ILinterEngineConfig`-Refactor) wird dringlicher, nicht weniger — EPIC-07 (TD-008/TD-010) hat einen zählbaren Hebel. Diese Beobachtung gehört in den Scope von EPIC-07, nicht in diesen Step.
- **MCP-Protokoll-Version `2024-11-05`** ist im E2E-Test fest verdrahtet; bei zukünftiger SDK-Default-Änderung könnte der Handshake scheitern. Coder hat das im `step-result.md` Abschnitt „Bekannte Unschärfen" dokumentiert.
- **Coder-Beobachtung im `step-result.md`**: `McpCallLogScope` ist `IAsyncDisposable` aber nicht `IDisposable` — idiomatisch, `await using` ist korrekt. Plan-Skizze Z. 887-908 hatte hier noch `using var _`, was nicht kompiliert hätte; die korrekte Form im fertigen Code ist `await using var _`. Kein Finding, sauber gelöst.

