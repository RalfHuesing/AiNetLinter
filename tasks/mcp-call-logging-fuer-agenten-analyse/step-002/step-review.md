---
status: approved
type: step-review
task: mcp-call-logging-fuer-agenten-analyse
step: 002
verdict: approved
verifier: kritiker
verified_by_model: MiniMax-M3
verified_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-05T14:30:00+02:00
tech_debt_ids: [TD-001, TD-002]  # keine neuen IDs
---

# Step 002: Review

## Verdict: approved

Step-002 erfüllt alle Muss-Haben aus Konzept EPIC-02 (Muss-Habe 4 = `McpCallLog.RecordError` mit JSONL-Schema, Lock, 4 KB Stack-Cap) sowie DoD 3/4/5. Der initiale `blocked`-Status (Coder fand +45 Z. in `McpCallLog.cs` → transitive `AIContextFootprint`-Welle in 5 Konsumenten) wurde durch User-Workaround A (PathOverride-Bumps in `rules.json`, Commit `17bda1d`) aufgelöst — Verifikation: 1275/1275 Tests grün, Dogfooding-Lint grün, 0/0 Build.

## Befund pro Ebene

- **Ebene 1 (Plan-Erfüllung):** Vollständig. `McpCallLog.cs` (+50 Z.: 2 consts, ~35 Z. `RecordError`-Body, XML-Doc) und `McpCallLogTests.cs` (+5 neue `[Fact]`s + private nested `TestException`) entsprechen dem Plan; bestehende 5 Tests in `McpCallLogTests.cs` unverändert; alle Helper (`CreateTempLogPath`, `TryDelete`, `ParseSingleEntry`) wiederverwendet; 3 dokumentierte Coder-Abweichungen (Reflection-Setter, expliziter Dispose in Test 3, `TestException` als `error_type` in Test 1) sind im `step-result.md` §"Abweichungen vom Plan" begründet und im Plan-Spirit (insb. Plan §"Bekannte Ausnahmen" hat Reflection-Fallback bereits antizipiert).
- **Ebene 2 (Rules-Konformität):** Konform. `McpCallLog` bleibt `internal sealed`, `#nullable enable` in Z. 1; `RecordError` ist 35 Z. mit 3 Params (unter `MaxMethodLineCount=60`/`MaxMethodParameterCount=4`); sichtbare Member gesamt: 6 (4 alt + 1 neu `RecordError` + `DisposeAsync` aus `IAsyncDisposable`), unter `MaxPublicMembersPerType=15`; `ArgumentNullException.ThrowIfNull` (lint-konform) statt manueller Null-Prüfung; XML-Doc beschreibt nur Was/Warum (kein `step-002`/`EPIC-02`/`TD-xxx`-Bezug, AiNetLinterRichtlinien §5); Architektur-Verbote eingehalten (kein DI, kein `AssemblyLoadContext`, kein Serilog, direkter `StreamWriter` analog `RecordEnd`); Tests mit `[Fact]` + `[Trait("Category", "Unit")]`, keine zwangsserialisierende Collection; alle neuen Identifier ASCII (Konzept-Vorgabe `EnforceAsciiIdentifiers`).
- **Ebene 3 (Logische Korrektheit):** Aussagekräftig. Schema-Test (`RecordError_BasicException_WritesJsonLineWithAllFields`) prüft alle 7 Konzept-Felder + 4 Negativ-Asserts (kein `lines/truncated/duration_ms/empty` im Error-Eintrag); 4-KB-Cap-Test speist 100 KB `string('a', 100_000)` ein, assertet `≤ 4096` + `EndsWith("...")`; zwei sequenzielle Lock-Reihenfolge-Tests decken `RecordEnd→RecordError` UND `RecordError→RecordEnd` ab (Test 3 musste auf explizites `await scope.DisposeAsync()` umgestellt werden, weil deferred `await using` Dispose die Reihenfolge umkehrte — sauber gefixt, im `step-result.md` §"Abweichungen" dokumentiert); Parallel-Test mit 50 Pairs (= 100 Tasks) beweist atomares `_writeLock` über mehrere Threads (validiert durch `JsonDocument.Parse` auf jeder Zeile, ohne Ordnungs-Asserts zur Flakiness-Vermeidung — Plan §"Bekannte Ausnahmen" hat dies so vorgesehen).
- **Ebene 4 (Konzept-Treue):** Passend. EPIC-02-Muss-Habe 4 vollständig erfüllt: `RecordError(tool, args, exception)` mit JSONL-Schema (`level=error / error_type / error_message / stack_trace`), Selber Lock wie `RecordEnd` (`McpCallLog.cs:_writeLock`), 4 KB Stack-Cap; DoD 3 (Lock-Reihenfolge) test-belegt, DoD 4 (Cap) test-belegt, DoD 5 (Tests grün + keine neuen Compiler-Warnungen) verifiziert; DoD 1/2/6/7 korrekt als EPIC-03/EPIC-04-Scope ausgeschlossen (Plan §"Nicht in DoD dieses Steps"); keine Non-Goals umgesetzt (kein Serilog, kein DI-Container, kein Hot-Reload-Fix — alle in Konzept §"Bewusst out of scope" aufgeführt); `McpCallLog`-Edits strikt additiv (nur 2 consts + 1 Methode, kein Eingriff in `LogPath`/`EntryCount`/`StartRecording`/`RecordEnd`/`McpCallLogScope`/`DisposeAsync`).

## Workaround-Verifikation (User-Entscheidung 2026-08-05T13:55)

5 PathOverride-Bumps in `rules.json`, Werte angemessen und konsistent (~+200 Z. Puffer pro Datei über dem gemessenen Wert):

| Datei | Vorher | Nachher | Gemessen (lt. Coder) | Buffer |
|---|---:|---:|---:|---:|
| `AnalysisToolRegistrations.cs` | 2800 | **3050** | 2846 | +204 |
| `FileStructureToolRegistrations.cs` | 2830 | **3070** | 2869 | +201 |
| `McpServerOptionsFactory.cs` | 2800 | **3020** | 2818 | +202 |
| `SymbolBodyToolRegistrations.cs` | 2800 | **3010** | 2802 | +208 |
| `SymbolGraphToolRegistrations.cs` | 2870 | **3120** | 2912 | +208 |

- `rules.json` syntaktisch korrekt (validiert via `ConvertFrom-Json` → "JSON OK")
- Diff `9d87c7f..17bda1d` zeigt **ausschließlich** die 5 Zahl-Änderungen — alle anderen `PathOverrides`, Default-Werte und Top-Level-Keys unverändert
- Kein Architektur-Verstoß: minimal-invasive Erhöhung bestehender PathOverrides, entspricht etablierter Wartungspraxis im Projekt (siehe `git log -- rules.json`: Vorgänger-Bumps in `49be2c7`, `3762e6a`, `8a663c7` u. a.)
- Workaround durch Tests implizit abgedeckt: Der Dogfooding-Test `RunLinterCli_OnWholeSolution_ReturnsSuccess` (verifiziert grün) übt den Lint-End-to-End-Pfad; ohne die Bumps wäre genau dieser Test rot (Coder-Verifikation per `git stash` im `step-result.md` §"Beobachtungen"), mit Bumps ist er grün. → Workaround-Logik ist nicht durch eine eigene Test-Klasse gesichert, aber durch den unveränderten Dogfooding-Pfad implizit verifiziert. Akzeptabel, weil die bumps reine Zahlenwerte sind und keine Verhaltensänderung im Linter selbst.

## Adversariell-Probe (Pflichtbestandteil)

**Probe 1: Wäre der Workaround überhaupt nötig?** Coder behauptet im `step-result.md` §"Beobachtungen" einen kausalen Zusammenhang: ohne `RecordError` (per `git stash`) meldet Linter `# AiNetLinter - 0 violations`, mit `RecordError` meldet er `# AiNetLinter - 5 violations` in genau den 5 in der Tabelle aufgeführten Dateien. Kausalität ist damit **stark** (1:1-Korrespondenz zwischen Datei-Set und Bump-Set). → Workaround ist angemessen, nicht "Voodoo-Bump".

**Probe 2: Sind die Puffer-Größen konsistent oder streuen sie wild?** Buffer-Werte 201, 202, 204, 208, 208 — alle im engen Band 201-208 Z., kein Ausreißer. → Konsistente Anwendung der "sichere Untergrenze +100, konservativ +200"-Logik aus `step-result.md` §"Bekannte Unschärfen".

**Probe 3: Wurde der Coder ggf. in Versuchung geführt, andere `rules.json`-Werte "nebenbei" mitzudrehen?"** Diff `9d87c7f..17bda1d` zeigt **nur** die 5 Zahl-Änderungen, keine Default-Value-Änderungen, keine `MetricsConfig`-Modifikation, keine Ausnahmen-/Exempt-Listen-Änderungen. → Keine schleichende Scope-Erweiterung.

**Probe 4: Wurde etwas gebaut, das unter Konzept-Non-Goals explizit ausgeschlossen war?** Konzept §"Bewusst out of scope" nennt Serilog, `Microsoft.Extensions.Logging`, DI-Container, Hot-Reload-Hardening, Log-Cleanup-Strategie, `startup.json`, Opt-in→Opt-out-Umkehr. `git show c3fe3c5` zeigt **keine** dieser Substanzen; `McpCallLog.cs` nutzt weiterhin `StreamWriter` und direkten `_writeLock`. → Keine Non-Goal-Verletzung.

## Tech-Debt-Einträge aus diesem Review

Keine neuen Tech-Debt-IDs. TD-001 (Roadmap-Test-Scope-Notiz, EPIC-01) und TD-002 (PathOverride-Bumps als mittelfristiges Architektur-Thema) bleiben unverändert; TD-002 dokumentiert bereits die mittelfristigen Optionen (`MetricsConfig` schlanker, `McpCallLog` partial-splitten, Interface-Schub) und die Notwendigkeit einer Re-Evaluation vor EPIC-03 (Error-Hook wird `RecordError` aus 4 Tool-Registration-Klassen heraus aufrufen, Pfade schwellen erneut an).

**Adversariell beobachtet, nicht eskaliert** (kein Finding, nur zur Dokumentation):

- `TestException` reflektiert auf internes Feld `Exception._stackTraceString`. Coder hat dies mit `?? throw new InvalidOperationException("Exception._stackTraceString field not found")` abgesichert — Feld-Rename oder -Entfernung in einer künftigen .NET-Version würde als lauter Klassen-Init-Throw sichtbar, nicht als stummer Test-Pass. Plan §"Bekannte Ausnahmen" hat diese Eventualität antizipiert. Risiko gering, Mitigation im Code.
- Konzept DoD 5 sagt "4 Call-Tests", tatsächlich 5 in `McpCallLogTests.cs`. Coder hat es in `step-result.md` §"Bekannte Unschärfen" dokumentiert — gleicher Konzept-Drift wie TD-001, aber für EPIC-02 statt EPIC-01. Bleibt für EPIC-04-Doku-Sync.

## Build- und Test-Status (verifiziert)

- `dotnet build` — 0/0, ~3.9 s
- `dotnet test --no-build --filter "FullyQualifiedName~McpCallLogTests"` — 10/10 grün (5 alt + 5 neu), 149 ms
- `dotnet test --no-build --filter "FullyQualifiedName~RunLinterCli_OnWholeSolution_ReturnsSuccess"` — 1/1 grün, 4 s (Dogfooding-Lint, Workaround-verifizierend)
- `dotnet test --no-build` (Volllauf) — 1275/1275 grün, 1 m 56 s

## Modell-Info

- `verified_by_model`: MiniMax-M3
- `verified_by_model_knowledge_cutoff`: 2026-01
