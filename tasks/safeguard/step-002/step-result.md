---
status: done (pending audit)
type: step-result
task: safeguard
step: 002
title: "SafeguardTool-Wrapper, Registrierung und ServerInstructions-Erweiterung"
epic: EPIC-02
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
related_to:
  - tasks/safeguard/step-001/step-result.md
  - tasks/safeguard/step-001/fix-01/step-result.md
---

# Step 002 Result: SafeguardTool-Wrapper, Registrierung und ServerInstructions-Erweiterung

## Zusammenfassung

EPIC-02 (Tool-Layer-Teil) umgesetzt: `SafeguardTool` als duenner Dispatch auf den vorhandenen
`SafeguardScanner` registriert in `AnalysisToolRegistrations` als 3. Tool, `ServerInstructions`
um `safeguard` als Quality-Gate erweitert, und 6 Unit-Tests inkl. dem kritischen
`FailedScore_PassedFalseButIsErrorFalse`-Regressionstest fuer die `IsErrorPolicy`-Anti-Pattern-Falle.
PathOverride fuer `SafeguardTool.cs` (2546 → 2800) neu in `rules.json`; `AnalysisToolRegistrations.cs`
unveraendert (2870, weiterhin ohne Violations trotz 3. Tool-Registrierung).

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs` (neu) — `internal static class SafeguardTool` mit
  `ExecuteAsync(state, scopeFilter, minScore, maxViolations, ct)` plus `SafeguardToolParameters`-Record
  fuer 5 Inputs. State-Management, `StructuredContent` (CamelCase JSON via
  `JsonSerializer.SerializeToElement`), `IsError=false` IMMER bei normalem Score, auch bei
  `Passed=false`. `McpToolResults.Error`/`Loading`/`SolutionNotLoaded` fuer Fehler-Pfade.
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` — `AddSafeguard(...)` zwischen
  `AddGetViolations` und `AddSearchPattern`, Lambda mit 4 Inputs (ScopeFilter, MinScore,
  MaxViolations, ct), Defaults aus `SafeguardScanner.DefaultMinScoreThreshold` (8.0) /
  `DefaultMaxRemediationEntries` (20), `SafeguardDescription` mit knapper Wann-nutzen-Doctrine.
- `src/AiNetLinter/Mcp/ServerInstructions.cs` — `safeguard` als Quality-Gate in der Tool-Liste
  und in der C#-only-Grenze-Liste erwaehnt.
- `rules.json` — `PathOverride` fuer `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs` neu
  mit `MaxAIContextFootprint: 2800` (realer Footprint 2546 + ~10% Puffer).
  `AnalysisToolRegistrations.cs`-PathOverride unveraendert bei 2870.
- `src/AiNetLinter.Tests/Mcp/Tools/SafeguardToolTests.cs` (neu) — 6 xUnit-v3-Tests analog
  `GetViolationsToolTests`-Pattern: NoSolutionLoaded, LoadedSolution (JSON-Schema-Felder),
  FailedScore_PassedFalseButIsErrorFalse (kritisch), ScopeFilter, MinScore+MaxViolations-Overrides,
  LinterEngineThrows (Malfunction via ThrowingTextLoader).

## Code-Commit

- **Hash:** `5d25c6f`
- **Subject:** `feat(mcp): safeguard-Tool mit structured output registriert [safeguard]`
- **Body:** 5 Aufzaehlungspunkte (Tool-Wrapper, AddSafeguard, ServerInstructions, PathOverride,
  6 Unit-Tests), `Refs: tasks/safeguard/step-002`,
  `Implements: konzept.md#muss-haven-punkte-1-3-7-9`, Hinweis "Live-Repo-Integration-Test
  folgt in step-003" und Pflicht-`### Commit-Vorschlag`-Block.

## Build-Output

`dotnet build` → 0 Warnungen, 0 Fehler (TreatWarningsAsErrors aktiv).

## Test-Output

- `dotnet test --filter FullyQualifiedName~Safeguard --no-build` → 19/19 gruen, 1 s
  (13 Scanner-Tests + 6 Tool-Tests).
- `dotnet test --filter Category=Unit --no-build` → 141/141 gruen, 16 s
  (keine Regressionen).

## Linter-Output

`dotnet run --project src/AiNetLinter -- --config rules.json --path . --no-cache` →
**0 Verstoesse** in `SafeguardTool.cs`, `AnalysisToolRegistrations.cs`, `ServerInstructions.cs`
und repo-weit. Pflicht-Verifikation, die in `step-001` zunaechst verpasst und in `fix-01`
nachgeholt wurde — diesmal direkt im Erstlauf erfuellt.

## Realer PathOverride-Wert

- `AnalysisToolRegistrations.cs`: 2870 → **2870** (unveraendert, 3. Tool-Registrierung passt
  weiterhin unter den bestehenden Wert, keine neuen Violations).
- `SafeguardTool.cs`: **neu** 2800 (realer Footprint 2546, +~10% Puffer).

## Abweichungen vom Plan

- **`StructuredContent`-Typ:** Plan-Vorgabe war `JsonObject?` (Schritt 6 in "Konkrete Aenderungen"
  Datei 1), realer Typ ist `JsonElement?`. Compiler-Fehler in Build-Phase 1 (`CS0029: JsonNode
  kann nicht in JsonElement? konvertiert werden`) hat das aufgezeigt. Loesung: Serialisierung
  via `JsonSerializer.SerializeToElement` statt `SerializeToNode`, Tests deserialisieren den
  rohen JSON-Text zurueck in `JsonObject` fuer property-Zugriff. Verhalten identisch aus
  Agent-Sicht (Structured-Content-JSON ist MCP-Spec-konform, Schema ist dasselbe).
- **Linter-Verstoesse bei Erstlauf:** Plan hatte die `PathOverride`-Diskussion unter
  "AnalysisToolRegistrations" erwartet (realer Wert 2546 < Schwellwert 2870 → kein Bump noetig).
  Stattdessen tauchten 2 Verstoesse in `SafeguardTool.cs` auf (2546 > 2500), die einen
  eigenen `PathOverride` rechtfertigen — exakt das im Plan vorausgesagte Szenario "reale
  Fussabdruck-Messung kann vom Plan abweichen". Mit dem Hinzufuegen des Eintrags in `rules.json`
  ist der Linter wieder 0-Verstoesse.
- **Test-Kategorisierung:** Die 6 Tool-Tests sind nicht mit `[Trait("Category", "Unit")]`
  attributiert, genauso wenig wie die 13 Scanner-Tests aus `step-001`. Beide werden ueber
  `FullyQualifiedName~Safeguard` gefunden, erscheinen aber nicht in `Category=Unit` (141
  bleibt konstant). Plan-Schaetzung "~141 + 5+ neue = ~146+" basierte auf der Annahme, die
  Tool-Tests waeren als Unit kategorisiert — sie sind es projektweit nicht fuer Tool-Tests
  dieses Layers. Kein Issue, weil die Tests ueber `FullyQualifiedName~Safeguard` voll
  abgedeckt sind.

## Beobachtungen

- **`Math.Clamp` vs. `InvalidArgument` als Input-Validierung:** Wie im Plan diskutiert,
  defensive `Math.Clamp(0.0, 10.0)` und `Math.Max(0, …)` fuer `minScore` / `maxViolations`
  statt eines harten `Recoverable(InvalidArgument, …)`. Konsequenz: ein Agent, der
  `minScore=999.0` schickt, bekommt einen normalen Score (statt eines strukturierten
  Fehlertexts), was im Quality-Gate-Kontext sinnvoller ist. Kein expliziter Test noetig,
  weil Clamping deterministisch wirkt.
- **`LoadingState`-Test weggelassen:** Der optionale
  `ExecuteAsync_LoadingState_ReturnsLoadingResult`-Test wurde nicht implementiert, weil der
  aktuelle `McpCodeGraphServer`-Konstruktor keinen direkten Weg bietet, `LoadState ==
  Loading` im Test-Setup zu setzen (siehe Plan-Hinweis "nice-to-have, nicht Pflicht"). Der
  Loading-Pfad selbst ist getestet via `McpTestClientRetryTests.ConnectAsync_*`-Familie im
  Integration-Layer.
- **`LinterErrorCodes.AnalysisFailed` fuer Malfunction:** Konsequente Anwendung der
  `IsErrorPolicy`: `McpToolResults.Error(AnalysisFailed, …, hint: "Einmal erneut versuchen ...")`
  ist 1:1 das Muster aus `GetViolationsTool.cs:51-55`. Pattern-Konsistenz zwischen den
  beiden Tool-Wrappern ist explizit gewollt.
- **`Internal sealed record SafeguardToolParameters`:** Pattern 1:1 von
  `SafeguardScannerParameters`. 5 Felder ueber dem 4-Parameter-Limit nur via Record moeglich
  — `internal sealed` wegen `InternalsVisibleTo("AiNetLinter.Tests")` in `LinterEngine.cs:18`.
- **`McpCallLog` CallKey:** `"{scopeFilter}|{minScore}|{maxViolations}"` — patternkonform
  mit `search_pattern`'s `"{pattern}|{isRegex}|{maxResults}"`. Nicht-trivial, weil das
  CallLog-Aggregat sonst bei gleichen Scope-Filtern aber unterschiedlichen Thresholds
  kollidieren wuerde.

## Bekannte Unschärfen

- **Kein expliziter Test fuer `IsMalfunction` ohne Retry-Hinweis:** Der Malfunction-Pfad
  wird via `ThrowingTextLoader` getestet (deterministische Exception). Andere Malfunction-
  Quellen (z. B. Roslyn-Walk-Fehler in `EnumerateConcreteClassesAsync`) sind im Scanner
  abgefangen (`_ = ignored; return null;`) und produzieren nie `IsMalfunction=true`. Damit
  ist der Malfunction-Pfad im Tool-Wrapper nur fuer LinterEngine-Fehler erreichbar, und
  dieser Pfad ist durch den Test abgedeckt.
- **Plan-Schaetzung PathOverride fuer `AnalysisToolRegistrations` 3300 war zu hoch:** Der
  reale Footprint bleibt unter dem bestehenden 2870-Limit. Erklaerung: `SafeguardScanner`
  wird nicht direkt von `AnalysisToolRegistrations` referenziert (nur ueber
  `SafeguardTool` → `SafeguardScanner.ComputeScoreAsync`), und `SafeguardTool` wiederum
  ist ueber `AiNetLinter.Mcp.Tools`-Namespace abgetrennt. Der transitive Footprint
  steigt nur um den Lambda-Body, nicht um den ganzen Scanner-Apparat. Falls in einem
  zukuenftigen Schritt die Scanner-Klassen direkt in `AnalysisToolRegistrations` injected
  werden (z. B. via Helper-Klasse), muss der Wert neu kalibriert werden.
- **Kein `AnalysisToolHelpers`-Refactor in diesem Step:** Mit 3 Tools waere eine Helper-
  Konsolidierung fuer das `AddXxx`-Pattern sinnvoll, aber ausserhalb des Step-Scopes
  (Plan hatte das explizit ausgeschlossen). `PathOverride`-Diskussion oben ist die
  einzige Konsequenz der jetzigen Struktur.

## Modell-Info

- `coded_by_model`: MiniMax-M3
- `coded_by_model_knowledge_cutoff`: 2026-01
