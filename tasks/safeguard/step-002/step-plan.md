---
status: open
type: step-plan
task: safeguard
step: 002
title: "SafeguardTool-Wrapper, Registrierung und ServerInstructions-Erweiterung"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-06T17:00:00+02:00
last_updated: 2026-08-06T17:00:00+02:00
related_to:
  - konzept.md#muss-haven-punkte-1-3-7-9
  - tasks/safeguard/step-001/step-result.md
  - tasks/safeguard/step-001/fix-01/step-result.md
---

# Step 002: SafeguardTool-Wrapper, Registrierung und ServerInstructions-Erweiterung

## Bezug

- **Task:** `safeguard`
- **Epic:** `EPIC-02` aus `roadmap.md` — `safeguard`-Tool (MCP-Wrapper,
  Registrierung, Live-Repo-Integration), aktuell offen
- **Konzept-Referenz:** `konzept.md` §"Muss-Haven" Punkte 1-3+7+9
  (Tool-Registrierung, Input/Output-Schema, structured JSON,
  ServerInstructions-Erweiterung) und §"Wie" Schritt 2 (Tool-Wrapper
  + Registrierung + JSON-Schema-Output)
- **Vorgänger-Step:** `step-001` + `step-001/fix-01` — `SafeguardScanner`
  ist vorhanden, linter-konform (433 Zeilen, 0 Verstöße), 13 Unit-Tests grün
  und deterministisch; `BuildScoreResult` ist isoliert testbar; Records
  `ScoreResult`/`ViolationEntry`/`RemediationHint`/`ScannedClass` und
  `SafeguardScoreResult` exisitieren und sind die JSON-Schema-Bausteine

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Live-Repo-Codes vorgefunden — beeinflusst den Plan direkt:

- **`SafeguardScanner.ComputeScoreAsync` (Commit `7e4e37e`)** liefert
  `SafeguardScoreResult` mit `Score: ScoreResult?` + `IsMalfunction: bool`
  + `Context: string?` (siehe `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs:431-434`).
  `ScoreResult` enthält bereits exakt die Felder, die der Konzept-JSON-Vertrag
  verlangt: `Passed`, `Score`, `Threshold`, `Violations`, `Remediation`,
  `Summary` (siehe `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs:442-448`).
  → **Keine** neuen Records, **keine** Schema-Builder nötig — die
  JSON-Schema-Bausteine sind bereits vollständig vorhanden, der Tool-Wrapper
  muss nur serialisieren.

- **`GetViolationsTool.cs` (1:1-Vorbild, 58 Zeilen):** `internal static
  class`, `internal static async Task<CallToolResult> ExecuteAsync(McpCodeGraphServer
  state, string? scopeFilter, CancellationToken ct)` (Z.23-27). Reihenfolge:
  `Loading` (Z.28) → `SolutionNotLoaded` (Z.30) → `GetConfigSnapshot`
  (Z.35) → `Scanner-Aufruf` (Z.36-43) → `IsMalfunction` → `Error` mit
  `AnalysisFailed`+Retry-Hint (Z.50-55) sonst `McpSufficiencyHints.Append(Text)`
  (Z.56). Exakt diese Struktur 1:1 übernehmen, nur die Scanner-Methode und
  die 3 Input-Parameter unterscheiden sich.

- **`AnalysisToolRegistrations.cs` (aktuell 94 Zeilen, PathOverride-Footprint
  `MaxAIContextFootprint = 2870` in `rules.json` Z.408):** registriert
  aktuell 2 Tools (`get_violations` Z.41-61, `search_pattern` Z.68-88) mit
  identischem `AddXxx`-Pattern: `private static void AddXxx(tools, mcpState,
  callLog)` → `tools.Add(McpServerTool.Create(async (args, ct) => ..., new
  McpServerToolCreateOptions { Name = ..., Description = XxxDescription }))`
  → `private const string XxxDescription = "..."`. Mit `safeguard` als
  3. Tool verdoppelt sich die Verdoppelung — eine `AnalysisToolHelpers`-
  Konsolidierung wäre ein Refactoring außerhalb des Step-Scopes (siehe
  "Bekannte Ausnahmen / Entscheidungen" unten: **kein** Helper in diesem
  Step, PathOverride-Bump akzeptabel).

- **`McpToolResults.cs` (188 Zeilen, 8 Helper):** `Error(...)`,
  `Recoverable(...)`, `SolutionNotLoaded()`, `SymbolNotFound(...)`,
  `AmbiguousSymbol(...)`, `InvalidArgument(...)`, `FileNotFound(...)`,
  `CompilationError(...)`, `Text(...)`, `Loading()`. Konzept
  §"Wo im Projekt"/"Nicht angefasst (bewusst)" sagt explizit:
  "`McpToolResults.cs` — bestehende Helper reichen, kein neuer Helper nötig".
  → **Keine** Änderung an `McpToolResults.cs`; structured content wird
  inline im Tool-Wrapper gebaut (siehe "Konkrete Änderungen" unten).

- **MCP-SDK 2.0.0 (verifiziert via `dotnet run`):** `CallToolResult` hat
  die Properties `Content` (`IList<ContentBlock>`), `IsError` (`bool?`),
  `StructuredContent` (`System.Text.Json.Nodes.JsonObject?`),
  `ResultType` (`string?`). `ContentBlock` ist abstrakte Basisklasse;
  konkrete Subklassen: `TextContentBlock`, `ImageContentBlock`,
  `AudioContentBlock`, `ToolResultContentBlock`, `ToolUseContentBlock`.
  `StructuredContent` ist die MCP-Spec-2025-06-18-konforme
  JSON-Schema-Output-Variante, separate Property (nicht im `Content`-Array).
  Verwendet wird `JsonSerializer.SerializeToNode<T>(value, options)` mit
  `JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`
  (MCP-typische snake_case-Alternative wäre auch möglich, camelCase ist
  aber im Projekt konsistent mit `GetViolationsTool`/`LinterErrorFormatter`-
  String-Konventionen).

- **`McpSufficiencyHints.cs` (33 Zeilen):** `Append(string text)` hängt
  den `[HINWEIS]: Diese Daten sind vollstaendig ...`-Hinweis an
  Text-Outputs. Konzept erwähnt für `safeguard` keinen Sufficiency-Hint
  (der strukturierte JSON-Output ist per Definition "vollständig für den
  Scope", das Score-Feld + Violations-Array + Remediation sind die ganze
  Antwort). → **Keine** Änderung an `McpSufficiencyHints` in diesem Step;
  `McpSufficiencyHints.cs` wird im Konzept §"Wo im Projekt" als
  "ggf. ein safeguard-spezifischer Hint in Schritt 2 prüfen" markiert —
  Prüfungsergebnis: nicht nötig, der strukturierte Output + der
  `Summary`-String im `TextContentBlock` transportieren die gleiche
  Aussage.

- **`IsErrorPolicy.md` Z.23:** "Leere Treffermenge (0 Aufrufstellen, 0
  Violations, Scope-Filter matched keine Datei, 0 Symbole gefunden) |
  `false`" und Z.40: "`get_violations` IsError=true nur für
  `SOLUTION_NOT_LOADED` und echte Malfunction". **Anti-Pattern-Falle
  für `safeguard`:** ein Score mit `Passed=false` (z. B. Score 4.0,
  Threshold 8.0) ist **kein** Malfunction und **kein** Fehler-Zustand —
  es ist der erwartete Output eines Quality-Gate-Tools, das genau für
  diesen Fall existiert. Konzept §"Zielplattformen" Z.77 sagt
  explizit: "`passed=false` ist **kein** `isError: true` — das ist
  erwartetes Verhalten, Agent bekommt Erfolgs-Response mit `passed:false`
  im JSON". → `IsError=false` bei normalem Score-Result, **auch** bei
  `Passed=false`. Der entsprechende Test
  (`ExecuteAsync_LoadedSolution_FailedScore_PassedFalseButIsErrorFalse`)
  ist im Plan explizit aufgeführt, damit diese Falle nicht erst im
  Kritiker-Review auffällt.

- **`ServerInstructions.cs` (Single-Source-of-Truth, 70 Zeilen):** ein
  einziger `internal const string Text` mit Tool-Aufzählung in
  Bullet-Form, `C#-only-Grenze`-Abschnitt, `Sufficiency-Doctrine`,
  `isError-Policy`-Absatz. Hinzufügen: 1 Zeile in der Tool-Liste
  ("- safeguard: ...") und ggf. ein Verweis in der `C#-only-Grenze`-
  Aufzählung (`safeguard` arbeitet auf `.cs` via LinterEngine, also in
  der C#-only-Liste, NICHT bei `search_pattern`-Fallback).

- **`GetViolationsToolTests.cs` (211 Zeilen, 7 Tests):** Pattern:
  `public sealed class GetViolationsToolTests : IClassFixture<SymbolGraphCatalogFixture>`,
  jeder Test baut einen eigenen `McpCodeGraphServer` (kein
  Class-Setup-Boilerplate), Test-Naming `ExecuteAsync_<Bedingung>_<Erwartung>`,
  `Assert.IsType<TextContentBlock>(Assert.Single(result.Content))` für
  Inhaltsprüfung, `ThrowingTextLoader`-Fake für Malfunction-Simulation
  (Z.161-173). Für `SafeguardToolTests`: identisches Pattern, 5+ Tests
  (Loading-State, Solution-Not-Loaded, Happy-Path, **Failed-Score-Not-IsError**,
  Scope-Filter, Malfunction, optional `minScore`/`maxViolations`-Override).
  Der **kritische Test für die IsError-Policy-Falle** ist
  `ExecuteAsync_LoadedSolution_FailedScore_PassedFalseButIsErrorFalse`
  — synthetische Mini-Solution, die garantiert `Passed=false` liefert
  (durch `minScore`-Override auf einen unerreichbar hohen Wert, z. B.
  100.0), und prüft `Assert.False(result.IsError)` + `passed == false`
  im strukturierten Content.

- **Existierender Live-Repo-Test-Layer:** `McpLiveRepositoryTests` +
  `McpLiveRepositoryFixture` + `McpTestClient` (siehe
  `src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs:1-145`): startet
  einmal pro Testklasse den MCP-Server-Prozess gegen das echte Repo
  (AiNetLinter.slnx), 9 Tests für die 9 bestehenden Tools, alle mit
  `[Trait("Category", "Integration")]`. Neuer `LiveDogfood_Safeguard_ReturnsResults`-
  Test gehört in dieselbe Klasse (`McpLiveRepositoryTests.cs`, eine
  zusätzliche `[Fact]`-Methode, ~20-30 Zeilen) — **aber NICHT in diesem
  Step** (siehe "Aufteilung 2 Steps" unten: getrennt von den
  Tool-Layer-Änderungen wegen isoliertem Risiko "Live-Score muss ≥ 5.0
  sein, sonst Bug in der Scanner-Formel (EPIC-01-Scope)").

## Intention

Nach diesem Step existiert das MCP-Tool `safeguard` funktional und
sichtbar in der `tools/list`-Antwort des `initialize`-Handshakes: dünner
`SafeguardTool`-Wrapper (~60 Zeilen) ruft den vorhandenen Scanner auf
und liefert einen `CallToolResult` mit `StructuredContent` (JSON-Schema-
2020-12-konform, MCP-SDK 2.0.0) **plus** optionalem `TextContentBlock`
für die Human-Summary, immer mit `IsError=false` außer bei
echter `SOLUTION_NOT_LOADED`/Malfunction. Registriert in
`AnalysisToolRegistrations.Register` als 3. Tool neben `get_violations`
und `search_pattern`, mit `callLog`-Wrapper analog der 2 bestehenden.
`ServerInstructions` um `safeguard` als Quality-Gate erweitert. 5-6
Unit-Tests beweisen: Loading-State, Solution-Not-Loaded, Happy-Path,
**Failed-Score-Not-IsError (kritisch)**, Scope-Filter, Malfunction. Der
Live-Repo-Integration-Test ist **separat** in `step-003` geplant, weil
sein Score-Korridor-≥-5.0 eine andere Risikoklasse hat (EPIC-01-Score-
Formel-Bug-Symptom).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs` (neu)

- **Was:** Neue statische Klasse `internal static class SafeguardTool`,
  `#nullable enable` am Dateianfang (Richtlinien §1, `EnforceNullableEnable`).
  Eine öffentliche Methode:
  `internal static async Task<CallToolResult> ExecuteAsync(
  McpCodeGraphServer state, string? scopeFilter, double minScore,
  int maxViolations, CancellationToken ct)` — **5 Parameter** über
  dem `MaxMethodParameterCount: 4`-Limit, also **Parameter-Object-
  Record** analog `SafeguardScannerParameters`:
  ```csharp
  internal sealed record SafeguardToolParameters(
      McpCodeGraphServer State,
      string? ScopeFilter,
      double MinScore,
      int MaxViolations,
      CancellationToken CancellationToken);

  internal static Task<CallToolResult> ExecuteAsync(SafeguardToolParameters p)
      => ExecuteAsync(p.State, p.ScopeFilter, p.MinScore, p.MaxViolations,
          p.CancellationToken);

  internal static async Task<CallToolResult> ExecuteAsync(
      McpCodeGraphServer state, string? scopeFilter, double minScore,
      int maxViolations, CancellationToken ct) { /* Body */ }
  ```
  Records sind vom `MaxMethodParameterCount: 4`-Limit ausgenommen
  (`AiNetLinter.mdc` Z.22: "Ab Überschreitung: `record` als
  Parameter-Object"); Pattern 1:1 von `SafeguardScannerParameters`
  (Z.417-424). Body-Struktur (1:1 von `GetViolationsTool.cs:25-57`):
  1. `if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();`
  2. `var solution = state.GetCurrentSolution();`
     `if (solution is null) return McpToolResults.SolutionNotLoaded();`
  3. `var configSnapshot = state.GetConfigSnapshot();` — atomarer
     Schnappschuss (siehe `McpCodeGraphServer.cs:131-143`), nur
     `configSnapshot.Config` wird benötigt; `UsedDefaultConfig` ist
     für `safeguard` irrelevant (Konzept-Ausgabe `{ passed, score,
     threshold, violations[], remediation, summary }` enthält keinen
     Default-Config-Marker; `GetViolationsTool` braucht den Marker
     nur, weil der Text-Output für den Agent sichtbar zwischen
     "Default-Regeln" und "projekteigener rules.json" unterscheidet).
  4. `var result = await SafeguardScanner.ComputeScoreAsync(new
     SafeguardScannerParameters(Solution: solution, Config:
     configSnapshot.Config, Console: state.Console, ScopeFilter:
     scopeFilter, CancellationToken: ct, MinScoreThreshold: minScore,
     MaxRemediationEntries: maxViolations));`
  5. `if (result.IsMalfunction) return McpToolResults.Error(LinterErrorCodes.AnalysisFailed,
     "Unerwarteter Fehler bei der Safeguard-Berechnung.",
     context: result.Context, hint: "Einmal erneut versuchen — bleibt
     der Fehler bestehen, LinterEngine-Log pruefen.");` — **kein**
     `SolutionNotLoaded`-Spezialfall hier, der ist oben in Schritt 2
     abgefangen; `AnalysisFailed` ist der korrekte Code für eine echte
     Malfunction (siehe `IsErrorPolicy.md` Z.17, Z.40 für `get_violations`).
  6. **Score-Result-Assembly** (NEU, kein Vorbild in `GetViolationsTool`):
     ```csharp
     var score = result.Score!; // IsMalfunction=false garantiert non-null
     var text = $"{score.Summary}\n\n" +
         $"[HINWEIS]: Diese Daten sind vollstaendig fuer den " +
         $"angefragten Scope — kein zusaetzliches Read/Grep noetig.";
     // text folgt der Sufficiency-Doctrine aus ServerInstructions Z.54-61,
     // damit Agenten den Output wiedererkennen, auch ohne structured-
     // content-Support im Client.
     return new CallToolResult
     {
         IsError = false, // IMMER false bei normalem Score, AUCH bei Passed=false
         Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
         StructuredContent = JsonSerializer.SerializeToNode(score,
             new JsonSerializerOptions
             {
                 PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                 WriteIndented = false,
                 DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
             }),
     };
     ```
     **Wichtig:** `IsError = false` **immer**, auch bei `Passed=false` —
     das ist die Anti-Pattern-Falle (siehe Konzept §"Zielplattformen"
     Z.77 und `IsErrorPolicy.md` Z.23, Z.40). `score.Summary` ist
     nicht null, weil `ScoreResult` ein `record` mit `string Summary`
     ist (kein nullable). `StructuredContent` wird über
     `JsonSerializer.SerializeToNode<ScoreResult>(score, options)`
     gefüllt — SDK 2.0.0 hat diese Methode, `ScoreResult` ist
     `internal sealed record` mit nur Daten-Properties (Records sind
     `JsonSerializer`-kompatibel ohne Attribute).
  - **Validierung der Input-Parameter:** `minScore` muss in `[0.0, 10.0]`
    liegen, `maxViolations` muss `>= 0` sein. Beide via defensivem
    `Math.Clamp` (kein `throw`) → ungültige Werte werden geclampt,
    nicht abgelehnt; das vermeidet `IsError=true`-Pfade für
    Argumentfehler (Policy: `INVALID_ARGUMENT` ist recoverable, also
    `IsError=false`; aber `Math.Clamp` ist noch sauberer, weil es
    gar keinen Fehler produziert und der Agent trotzdem ein
    sinnvolles Ergebnis bekommt). **Konsultations-Hinweis für den
    Coder:** Konzept sagt `minScore?` und `maxViolations?` als
    optionale Inputs — Clamping ist defensiver als Ablehnen, aber
    strikt am Konzept wäre auch `Recoverable(InvalidArgument, ...)`
    (siehe `McpToolResults.cs:108-114`). Empfehlung: clampen, weil
    der MCP-Score-Bereich definitionsgemäß 0-10 ist und der Agent
    bei Out-of-Range eh kein sinnvolles Ergebnis erwarten kann;
    trotzdem **kein** hartes Throw.
- **Warum:** Strikt 1:1-Pattern von `GetViolationsTool.cs:25-57`,
  einzige substantielle Neuerung ist der Structured-Content-Aufbau
  (Schritt 6) — die anderen 5 Schritte sind 1:1-Copy mit angepassten
  Variablennamen. Kein DI, kein ALC, kein Plugin (Richtlinien §1+§2),
  `sealed` (implizit für `static class`), `internal sealed record` für
  Parameter-Object (Richtlinien §5 + AiNetLinter.mdc Z.10).

### Datei 2: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (Z.36-39 + neue Methode)

- **Was:** `Register(...)` (Z.32-39) um `AddSafeguard(tools, mcpState, callLog);`-
  Aufruf ergänzen, **zwischen** `AddGetViolations` und `AddSearchPattern`
  (Reihenfolge: scan-/analyse-orientierte Tools zuerst, dann Fallback-Tools,
  semantisch passt `safeguard` neben `get_violations` als beides Lint-bezogene
  Tools). Neue Methode analog `AddGetViolations` (Z.41-61) +
  `GetViolationsDescription` (Z.63-66):
  ```csharp
  private static void AddSafeguard(
      McpServerPrimitiveCollection<McpServerTool> tools,
      McpCodeGraphServer mcpState,
      McpCallLog? callLog)
  {
      tools.Add(McpServerTool.Create(
          async (string? scopeFilter = null, double minScore = SafeguardScanner.DefaultMinScoreThreshold, int maxViolations = SafeguardScanner.DefaultMaxRemediationEntries, CancellationToken ct = default) =>
          {
              if (callLog is null)
              {
                  return await SafeguardTool.ExecuteAsync(mcpState, scopeFilter, minScore, maxViolations, ct);
              }
              return await callLog.ExecuteCallAsync("safeguard", $"{scopeFilter}|{minScore}|{maxViolations}",
                  () => SafeguardTool.ExecuteAsync(mcpState, scopeFilter, minScore, maxViolations, ct));
          },
          new McpServerToolCreateOptions
          {
              Name = "safeguard",
              Description = SafeguardDescription,
          }));
  }

  private const string SafeguardDescription =
      "Wann nutzen: Quality-Gate-Wert vor CI-Merge pruefen — deterministischer " +
      "0-10-Score + Pass/Fail-Threshold + Top-Violations + Remediation-Hints fuer " +
      "die geladene Solution. scopeFilter (Projekt-Name oder Pfad-Substring) " +
      "grenzt auf einen Teilbereich ein, minScore ueberschreibt den Default-Threshold " +
      "(8.0), maxViolations begrenzt die Top-Violations-Liste (Default 20).";
  ```
  Die Lambda-Signatur hat **4 Parameter** (`scopeFilter`, `minScore`,
  `maxViolations`, `ct`) — genau am `MaxMethodParameterCount: 4`-Limit,
  nicht überschritten. Da das Lambda KEIN Record-Konstruktor ist, zählt
  `MaxMethodParameterCount` voll (Records sind ausgenommen, Lambdas
  nicht).
- **Warum:** Strikt 1:1-Pattern von `AddGetViolations` (Z.41-61) +
  `GetViolationsDescription` (Z.63-66) — einzige Variation ist die
  Default-Wert-Bindung an `SafeguardScanner.DefaultMinScoreThreshold`
  (8.0) und `SafeguardScanner.DefaultMaxRemediationEntries` (20) statt
  hardcoded Konstanten, weil die Scanner-Klasse bereits diese
  Default-Konstanten exportiert (Konzept-Punkt 2: "Default aus
  `rules.json`" — aktuell als Konstante im Scanner statt in
  `rules.json`, EPIC-01 hat das so dokumentiert; ein zukünftiges
  `rules.json`-`safeguard`-Section kann diese Defaults über die
  Konstante hinaus erweitern, aber out of scope für EPIC-02).
  Beschreibungstext im gleichen knappen "Wann nutzen"-Stil wie
  `GetViolationsDescription` (Z.63-66) — passt zum etablierten
  Tool-Description-Pattern.

### Datei 3: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (PathOverride in `rules.json`)

- **Was:** PathOverride in `rules.json` (Z.405-410) von
  `MaxAIContextFootprint: 2870` auf `MaxAIContextFootprint: 3300`
  anheben. Hintergrund: `AnalysisToolRegistrations` pullt jetzt eine
  3. Tool-Implementierung (`SafeguardTool` → `SafeguardScanner` →
  `LinterEngine` + `LinterRuleIds` + `AIContextFootprintCalculator` +
  `ComplexityCalculator` + Records), der transitive
  `AIContextFootprint` steigt um schätzungsweise 200-400 Einheiten
  über den 2870er-Schwellwert. 3300 ist ein vorsichtiger Wert, der
  die anderen `PathOverrides` in `rules.json` (siehe Z.411-435:
  `FileStructureToolRegistrations.cs: 2890`, `SymbolGraphToolRegistrations.cs:
  2900`) konsistent übersteigt und damit signalisiert, dass diese
  Datei die "schwerste" Tool-Registration ist (sie zieht 2 der 3
  Lint-bezogenen Tools an).
- **Warum:** Konzept §"Entdeckte Mängel" Z.116 sagt explizit:
  "Bei jedem neuen Tool riskiert die `AnalysisToolRegistrations`-
  Klasse das `MaxAIContextFootprint`-Limit (2500). Planer prüft in
  Step 1, ob Konsolidierung in Helper-Klassen nötig ist; sonst
  PathOverride moderat anheben." — Konsolidierungs-Entscheidung
  siehe "Bekannte Ausnahmen / Entscheidungen" unten. Coder soll
  nach dem Build `dotnet run --project src/AiNetLinter -- --config
  rules.json --path .` laufen lassen, um den realen Footprint zu
  verifizieren und ggf. den PathOverride-Wert auf den realen
  Bedarf nachzujustieren (3300 ist obere Schätzung, real
  möglicherweise nur 3000-3200).

### Datei 4: `src/AiNetLinter/Mcp/ServerInstructions.cs` (Z.27-47, Tool-Liste + C#-only-Grenze)

- **Was:** Zwei kleine Edits am `Text`-String (Z.24-69):
  1. **Tool-Liste (Z.27-47):** neue Bullet-Zeile nach `get_violations`:
     ```
     - safeguard: Liefert einen deterministischen 0-10-Quality-Score + Pass/Fail-Threshold + Top-Violations + Remediation-Hints fuer die geladene Solution.
     ```
     Position: zwischen `get_violations` (Z.40) und `get_symbol_body`
     (Z.41-42), weil `safeguard` ein analyse-/lint-bezogenes Tool ist
     und konzeptuell neben `get_violations` gehört (gleiche
     LinterEngine-Basis).
  2. **C#-only-Grenze-Liste (Z.48-53):** `safeguard` in die
     `C#-only`-Aufzählung aufnehmen — arbeitet ausschließlich auf
     `.cs`-Quellcode (via `LinterEngine.RunAsync`). Konkret:
     `find_symbol, find_references, get_impact, get_type_hierarchy,
     get_file_skeleton, get_violations, safeguard, und
     get_symbol_body arbeiten ausschliesslich auf .cs-Quellcode
     (Roslyn-Symbolgraph).` — `safeguard` direkt nach
     `get_violations,` einfügen, alphabetisch/logisch passend.
  3. **isError-Policy-Absatz (Z.62-67):** KEIN Edit. Der
     `isError=true`-Hinweis gilt universell (3 Kategorien aus der
     Policy-Tabelle), die `passed=false`-Ausnahme für `safeguard` ist
     ein Tool-spezifisches Verhalten und gehört in die Tool-Beschreibung
     (siehe Datei 2 `SafeguardDescription`), nicht in die globale
     isError-Policy-Erklärung. Konzept §"Zielplattformen" Z.77
     formuliert die Policy abstrakt genug, dass der globale
     `isError`-Absatz nicht erweitert werden muss.
- **Warum:** `ServerInstructions` ist die Single-Source-of-Truth für
  den `initialize`-Handshake (siehe XML-Doc Z.6-19) — Tool-Bullet
  ist der erste Punkt, an dem der Agent erfährt, dass `safeguard`
  existiert. Ohne Bullet: Tool ist in `tools/list` zwar da, aber
  der Agent bekommt es erst beim expliziten Suchen. `C#-only`-Grenze
  ist analog zu pflegen (sonst wirkt es, als könnte `safeguard`
  auch JS/Razor analysieren).

### Datei 5: `src/AiNetLinter.Tests/Mcp/Tools/SafeguardToolTests.cs` (neu)

- **Was:** xUnit v3 Testklasse analog `GetViolationsToolTests.cs`
  (`*.Tests` Override in `rules.json` Z.387: `EnforceSealedClasses`
  aus, daher `public class SafeguardToolTests`, kein `sealed`-Pflicht
  im Test-Projekt). Tests (mindestens diese 5+, Konzept
  §"Muss-Haven" 10 wird kombiniert mit den 13 Scanner-Tests zu
  insgesamt 18-19 safeguard-Tests, deutlich über 10):
  - **`ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`**
    — analog `GetViolationsToolTests.ExecuteAsync_NoSolutionLoaded_*`
    (Z.27-37): `using var state = new McpCodeGraphServer(
    McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));`
    → `Assert.True(result.IsError);` → `Assert.Contains("SOLUTION_NOT_LOADED",
    textContent.Text);`. Edge: auch prüfen, dass `StructuredContent`
    `null` ist (bei IsError=true nicht befüllt).
  - **`ExecuteAsync_LoadedSolution_ReturnsScoreResult`**
    — `using var state = new McpCodeGraphServer(McpCodeGraphServerOptions
    .From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));`
    → `await SafeguardTool.ExecuteAsync(state, null, 8.0, 20, CancellationToken.None);`
    → `Assert.False(result.IsError);` → `Assert.IsType<TextContentBlock>(
    Assert.Single(result.Content));` → `Assert.NotNull(result.StructuredContent);`
    → `Assert.Contains("PASS", textContent.Text)` oder `"FAIL"` (je nach
    Live-Score, Konzept sagt ≥ 5.0 ist plausibel, also bei Threshold 8.0
    wahrscheinlich FAIL — Test toleriert beides). **Wichtig:** Test
    darf den genauen Score-Wert nicht hartcodieren, weil der von der
    Solution-Konfiguration abhängt.
  - **`ExecuteAsync_LoadedSolution_FailedScore_PassedFalseButIsErrorFalse`**
    — **der kritische IsError-Policy-Test** (Anti-Pattern-Falle
    Konzept Z.77 + `IsErrorPolicy.md` Z.23, Z.40): `minScore=100.0` (jeder
    reale Score ist < 100, also `Passed=false` garantiert).
    `Assert.False(result.IsError);` ← **das ist die Anti-Pattern-Falle**;
    zusätzlich `Assert.NotNull(result.StructuredContent);` +
    Parsen des `JsonObject` und prüfen, dass `passed == false` im
    strukturierten JSON steht. Test-Kommentar erklärt explizit
    "Regression-Test für IsErrorPolicy: `passed=false` ist NICHT
    `isError: true`".
  - **`ExecuteAsync_ScopeFilter_IsPassedToScanner`**
    — `await SafeguardTool.ExecuteAsync(state, "SymbolGraphMini", 8.0, 20, ct);`
    vs. ohne Scope-Filter. Beide `Assert.False(result.IsError);` und
    `Assert.NotNull(result.StructuredContent);`. Verifikation: die
    `Summary`-Strings unterscheiden sich (Scope-Filter-Variante
    erwähnt Scope-Filter oder hat andere `violationsCount`) — oder
    einfacher: die `Violations`-Liste ist in beiden Fällen sortiert
    nach `(FilePath, LineNumber)` deterministisch, aber die
    enthaltenen `FilePath`-Werte unterscheiden sich je nach Scope.
    Pragmatischer Test: einfach beide Calls funktionieren lassen und
    nur prüfen, dass `IsError=false` in beiden Fällen; ein
    Tiefenvergleich des Scope-Effekts ist EPIC-01-Scope
    (siehe `SafeguardScannerTests`).
  - **`ExecuteAsync_LinterEngineThrows_ReturnsMalfunctionWithIsErrorTrueAndRetryHint`**
    — analog `GetViolationsToolTests.ExecuteAsync_LinterEngineThrows_*`
    (Z.106-154): `ThrowingTextLoader`-Fake (kann aus
    `GetViolationsToolTests.cs:161-173` per `InternalsVisibleTo`
    wiederverwendet oder 1:1 in `SafeguardToolTests.cs` dupliziert
    werden — `ThrowingTextLoader` ist `private sealed class` in
    `GetViolationsToolTests.cs`, also Duplikation). Test-Setup mit
    `AdhocWorkspace` + `Faulty.cs` (existierende Datei) + Loader-Fake.
    `Assert.True(result.IsError);` + `Assert.Contains("ANALYSIS_FAILED",
    textContent.Text);` + `Assert.Contains("Einmal erneut versuchen",
    textContent.Text);` + `Assert.Contains("Simulierter Lesefehler",
    textContent.Text);`.
  - **`ExecuteAsync_MinScoreAndMaxViolationsOverrides_AreHonored`**
    — `minScore=0.0` (jeder reale Score ist ≥ 0, also `Passed=true`
    garantiert, außer die Solution ist katastrophal) + `maxViolations=1`
    (Top-Violations-Liste hat maximal 1 Eintrag, wenn überhaupt welche
    vorhanden sind). `Assert.False(result.IsError);` +
    `Assert.NotNull(result.StructuredContent);` + Parsen: `passed==true`
    + `violations.length <= 1`. Test verifiziert, dass die
    Tool-Input-Parameter den Scanner erreichen.
- **Optionale Tests** (nice-to-have, nicht Pflicht):
  - `ExecuteAsync_LoadingState_ReturnsLoadingResult` — `LoadState ==
    ServerLoadState.Loading` zu setzen ist im aktuellen
    `McpCodeGraphServer`-Test-Setup nicht trivial (kein direkter
    Konstruktor-Parameter); `McpTestClient.CallToolAsync`
    (Z.109-136) handhabt Loading bereits per Retry. Wenn der Coder
    einen sauberen Weg findet (z. B. über ein internes Test-Setup-
    Helper), gern mit aufnehmen.
- **Warum:** 5+ Unit-Tests pro Konzept §"Muss-Haven" 8 (für
  Tool-Wrapper); kombiniert mit den 13 Scanner-Tests aus
  `step-001/fix-01` sind 18-19 safeguard-Tests insgesamt, deutlich
  über der 10+-Vorgabe. Der `FailedScore_PassedFalseButIsErrorFalse`-
  Test ist explizit als Regressionsschutz für die `IsErrorPolicy`-
  Anti-Pattern-Falle benannt — er gehört in dieselbe Kategorie wie
  der `LinterEngineThrows_ReturnsMalfunctionWithIsErrorTrueAndRetryHint`-
  Test in `GetViolationsToolTests.cs:107-154`, der genau die andere
  Seite der gleichen Policy prüft (Malfunction → IsError=true).
  Symmetrie zwischen den beiden Tests dokumentiert die Policy.

## Aufteilung 2 Steps (EPIC-02 → step-002 + step-003)

**Konzept §"Steps" Schritt 2 (Z.151-160)** bündelt Tool-Wrapper +
Registrierung + ServerInstructions + Unit-Tests + **1 Integration-
Test** in einem Step. **Empfehlung dieses Plans: 2 Steps** trennen.

**Begründung der Aufteilung:**

1. **Tool-Layer-Änderungen vs. Live-Repo-Test sind zwei
   verschiedene Risikoklassen:**
   - Tool-Layer (Schritt 002): deterministisch, testbar mit
     Mini-Fixture (`SymbolGraphCatalogFixture`), Score-Werte
     tolerant (`PASS` oder `FAIL` beides OK), rein
     Architektur-/Pattern-Konformität. Failure-Mode: Build rot
     oder Linter-Verstoß → Coder fixt im selben Step (analog
     `step-001/fix-01`).
   - Live-Repo-Test (Schritt 003): hängt vom **realen** Score
     des AiNetLinter-Repos ab. Konzept sagt "≥ 5.0 — sonst Bug
     in Score-Formel". Failure-Mode: Test scheitert, weil der
     reale Score < 5.0 ist → **das ist kein Tool-Layer-Bug,
     sondern ein Scanner-Formel-Bug** → Fix liegt in EPIC-01-
     Scope (`SafeguardScanner.ComputeScoreResult`/
     `BuildScoreResult`) → **würde** scope-creep in step-002
     verursachen, wenn beides in einem Step wäre.
2. **Reviewer-Last:** ein 500-Zeilen-Diff mit 5+ Tool-Tests ist
   gut reviewbar; ein 550-Zeilen-Diff mit 5+ Tool-Tests **plus**
   ein Live-Repo-Test, der den realen Score gegen einen konzeptuell
   geschätzten Korridor prüft, ist für den Kritiker schwerer zu
   beurteilen — er muss gleichzeitig die Tool-Pattern-Konformität
   und die Live-Repo-Score-Plausibilität bewerten.
3. **Konzept-Vorgabe ist explizit JIT-anpassbar:** Konzept
   §"Wie" Z.136-137 sagt: "Diese Formel ist eine **erste
   Skizze** — der Planer im drift-loop darf sie JIT anpassen,
   wenn echte Daten (Live-Repo-Score) eine andere Gewichtung
   nahelegen." Konzept §"Offene Punkte" Z.199 sagt: "Score-
   Formel ist Skizze, JIT-Verfeinerung im drift-loop ist
   explizit erlaubt und gewünscht (Step 1 startet mit dem hier
   dokumentierten Ansatz und passt bei Bedarf an, sobald echte
   Live-Repo-Daten vorliegen)." — d. h. der Konzept-Autor
   antizipiert explizit, dass die Live-Daten eine Justierung
   triggern können, und billigt die JIT-Aufteilung.
4. **Faustregel aus dem Planer-Auftrag:** "Live-Repo-Integration-
   Test: eigener Brocken, kann separat reviewed werden, isolierter
   Risiko" — exakt dieser Fall.
5. **Roadmap `EPIC-02` Schritt-Anzahl-Range (Z.60):** "Geplante
   Schritt-Anzahl: 2-3" — der 2er-Split (step-002 = intern,
   step-003 = live) bleibt im Korridor.

**Konsequenz für diesen Plan:** `step-002` deckt **nur** die
internen Tool-Layer-Änderungen (Dateien 1-5 oben). Der
Live-Repo-Integration-Test (1 zusätzliche `[Fact]`-Methode in
`McpLiveRepositoryTests.cs`) ist **out of scope** für diesen Step
und wird in `step-003` geplant — nach Abschluss von `step-002`
inkl. Coder-Code-Review. Der Planer-Auftrag sagt explizit: "nur
EIN Step-Plan, der NÄCHSTE Schritt; weitere Steps plane ich in
späteren Aufrufen" — d. h. `step-003` wird nach dem step-002-
Review in einem separaten Planer-Aufruf geplant, mit dann
aktuellem `step-002`-Ergebnis als Input.

## Tests

- [ ] `dotnet build` grün (0 Warnungen, 0 Fehler, `TreatWarningsAsErrors`
      aktiv)
- [ ] `dotnet test --filter FullyQualifiedName~SafeguardTool` grün
      (5+ Tool-Tests, neu in diesem Step)
- [ ] `dotnet test --filter FullyQualifiedName~Safeguard` grün
      (umfasst alle 13 Scanner-Tests + 5+ Tool-Tests = 18+ grün)
- [ ] `dotnet test --filter Category=Unit` grün (keine
      Regressionen in den ~200 bestehenden Unit-Tests; erwartet
      ~141 + 5+ neue = ~146+ grün)
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json
      --path . --no-cache` zeigt **0** Verstöße in
      `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs` und in
      `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` und in
      `src/AiNetLinter/Mcp/ServerInstructions.cs` (Pflicht-Lint;
      siehe `AiNetLinterRichtlinien.mdc` §3 Lint-Command).
      `PathOverride` für `AnalysisToolRegistrations.cs` in
      `rules.json` auf realen Bedarf nachjustieren (Coder misst
      mit Linter-Lauf, plant mit 3300 als obere Schätzung).
- [ ] Deterministische Tests bestehen (Score-Werte exakt
      reproduzierbar — die Scanner-Tests in EPIC-01 decken das
      schon; ein identischer `safeguard`-Call zweimal hintereinander
      muss strukturell identische `StructuredContent`-JSON liefern,
      kein zusätzlicher Tool-Test nötig weil der deterministische
      Pfad im Scanner liegt)
- [ ] `MaxLineCount=500` für `SafeguardTool.cs` eingehalten
      (~60 Zeilen Body + 15 Zeilen Parameter-Record = ~75 Zeilen,
      weit unter 500)
- [ ] `MaxLineCount=500` für `AnalysisToolRegistrations.cs` weiterhin
      eingehalten (aktuell 94 Zeilen + ~30 Zeilen `AddSafeguard` +
      ~5 Zeilen `SafeguardDescription` = ~129 Zeilen, unter 500)
- [ ] `AIContextFootprint` für `AnalysisToolRegistrations.cs` ≤ 3300
      (PathOverride in `rules.json` Z.408 entsprechend anheben,
      Coder verifiziert mit Linter-Lauf)

## Definition of Done

- [ ] Alle 5 "Konkrete Änderungen" umgesetzt (Dateien 1-5)
- [ ] `dotnet build` grün (0 Warnungen, 0 Fehler, `TreatWarningsAsErrors`)
- [ ] `dotnet test --filter FullyQualifiedName~SafeguardTool` grün
      (5+ Tests, inkl. `FailedScore_PassedFalseButIsErrorFalse`)
- [ ] `dotnet test --filter Category=Unit` grün (keine Regressionen)
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json
      --path . --no-cache` zeigt **0** Linter-Verstöße in
      `SafeguardTool.cs`/`AnalysisToolRegistrations.cs`/
      `ServerInstructions.cs`/`SafeguardToolTests.cs`
- [ ] Code-Commit auf aktuellem Branch (Conventional Commit auf
      Deutsch, imperativ, mit `[safeguard]`-Suffix; Subject
      max. 72 Zeichen; Body mit `Refs: tasks/safeguard/step-002`;
      `### Commit-Vorschlag`-Block am Ende nach
      `AiNetLinterRichtlinien.mdc` §4)
- [ ] Doku-Commit (separater Commit per Spec §10.3) trägt
      `tasks/safeguard/step-002/step-result.md` + Status-Update
      in `tasks/safeguard/step-002/step-plan.md` (auf
      `done (pending audit)`) und `Ref Code-Commit: <hash>` im
      Body
- [ ] `tasks/safeguard/step-002/step-result.md` geschrieben mit
      beiden Commit-Hashes, grünem Test-Output (einzeilig pro
      Spec §10.7), Linter-Output (0 Verstöße in den
      berührten Dateien) und kurzer Notiz "Tool-Layer-Step,
      Live-Repo-Test folgt in step-003"
- [ ] `status` in `tasks/safeguard/step-002/step-plan.md` von
      `open` auf `done (pending audit)` gesetzt (vom Coder im
      Doku-Commit)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1` (Grundprinzipien):
  monolithisch & statisch: `internal static class SafeguardTool`,
  `record` für unveränderliche Daten (Parameter-Record
  `SafeguardToolParameters`), keine DI, kein ALC, kein Plugin
- `.agents/rules/AiNetLinterRichtlinien.mdc#2` (Architektur-Verbote):
  keine ALC, kein Plugin-System, kein DI-Overhead — `AddSafeguard`
  ist statische Methode, keine DI
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` (Updates & Tests):
  xUnit v3 Pflicht (genutzt), Commit-Vorschlag-Pflicht (im
  `step-result.md`), MCP-Live-Tests über C#-Infrastruktur
  (`McpLiveRepositoryTests`/`McpTestClient` — **out of scope hier,
  step-003**, aber Architektur-Pattern konsistent)
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` (Qualitätsdrift-Prävention):
  Result-Pattern (`SafeguardScoreResult.IsMalfunction/Context`,
  `McpToolResults.Error/Loading/SolutionNotLoaded`),
  Zero-Warning-Direktive (`TreatWarningsAsErrors`),
  sparsame Kommentare ohne Task-/Step-/TD-/EPIC-Referenzen
  im Produktionscode (verifizieren mit `grep` im Review),
  `sealed` für Records (`internal sealed record SafeguardToolParameters`),
  `record` für unveränderliche Daten
- `.agents/rules/AiNetLinterRichtlinien.mdc#6` (Agenten-Arbeitsstil):
  nicht relevant für Code, nur für Planer/Coder-Antwort-Stil
- `.agents/rules/AiNetLinter.mdc` (Grenzwerte Produktion):
  `MaxLineCount=500`, `MaxMethodLineCount=60`,
  `MaxMethodParameterCount=4` (Parameter-Record als Workaround
  für `ExecuteAsync(McpCodeGraphServer, string?, double, int, CancellationToken)`,
  Lambda in `AddSafeguard` hat 4 Parameter exakt am Limit),
  `MaxCyclomaticComplexity=12`, `MaxCognitiveComplexity=15`,
  `AIContextFootprint=2500` Standard / `2870→3300` PathOverride
  für `AnalysisToolRegistrations.cs`, `EnforceNullableEnable`
  (`#nullable enable` am Dateianfang jeder `.cs`),
  `EnforceSealedClasses` (`SafeguardTool` ist `static class`
  = implizit sealed, Records `internal sealed record`),
  `EnforceAsciiIdentifiers` (keine Umlaute in Bezeichnern,
  ASCII-Transliteration in Kommentaren — etabliertes Muster im
  Projekt, siehe `GetViolationsTool.cs`),
  `EnforcePascalCase` (alle öffentlichen Typen/Properties
  PascalCase), `BanAsyncVoid`, `BanBlockingTaskAccess` (kein
  `.GetAwaiter().GetResult()` o. ä.),
  `EnforceNoSilentCatch` (kein leerer Catch — defensive
  `try/catch` liegt im Scanner, nicht im Tool-Wrapper; Tool-Wrapper
  prüft nur `IsMalfunction`-Flag),
  `DetectAndBanPhantomDependencies` (alle `using`s auflösbar,
  Build grün = verifiziert),
  `AvoidExcessiveMiddleMen` (Tool-Wrapper ist sehr dünn,
  ~30 Zeilen Body, aber kein bloßer Forwarder — er macht
  State-Management, Result-Assembly, JSON-Serialisierung;
  Verteidigung gegen `AvoidExcessiveMiddleMen`-Flag im Review)
- `src/AiNetLinter/Mcp/IsErrorPolicy.md` — Pflicht-Referenz für
  den `FailedScore_PassedFalseButIsErrorFalse`-Test: `passed=false`
  ist KEIN `isError=true` (Z.23, Z.40, Konzept §"Zielplattformen"
  Z.77). Tool-Wrapper setzt `IsError=false` für normale Score-
  Results, `IsError=true` nur bei `IsMalfunction` (Scanner-Code-
  Pfad) oder `SOLUTION_NOT_LOADED` (Pre-Scanner-Pfad).
- `rules.json` Z.405-410 (`PathOverrides.MaxAIContextFootprint`)
  — wird in Datei 3 von 2870 auf 3300 angehoben.

## Bekannte Ausnahmen / Entscheidungen

- **2-Step-Split statt Konzept-Schritt-2 als einzelner Step** (siehe
  "Aufteilung 2 Steps" oben): Tool-Layer-Änderungen (Schritt 002,
  dieser Plan) und Live-Repo-Integration-Test (Schritt 003, in
  späterem Planer-Aufruf geplant) werden getrennt, weil der
  Live-Test eine andere Risikoklasse hat (Score-Korridor-≥-5.0
  ist Scanner-Formel-Symptom, nicht Tool-Layer-Bug). Konzept
  §"Wie" Z.136-137 + §"Offene Punkte" Z.199 billigen JIT-Anpassung
  explizit. Roadmap-Zeile "Geplante Schritt-Anzahl: 2-3" wird
  mit dem Split (2 Steps) eingehalten.

- **Keine `AnalysisToolHelpers`-Konsolidierung in diesem Step:**
  Mit 3 Tool-Registrierungen (`get_violations`, `search_pattern`,
  `safeguard`) wäre eine Helper-Klasse für das `AddXxx`-Pattern
  ein naheliegender Refactor — aber das wäre Scope-Erweiterung
  über "register safeguard" hinaus (würde bestehende
  `AddGetViolations`/`AddSearchPattern` mit-umstellen, ~30 Zeilen
  zusätzlicher Code, eigenes Review-Risiko). Stattdessen:
  `PathOverride` in `rules.json` moderat anheben (2870 → 3300).
  Die Entscheidung "Konsolidierung" kann in einem späteren
  Tech-Debt-Epic angegangen werden, sobald ein 4. Tool dazukommt
  und der Footprint erneut ansteigt. Konzept §"Entdeckte Mängel"
  Z.116 ("Entscheidung ad-hoc, kein Vorab-Block") stützt diese
  Ad-hoc-Entscheidung. **Konsultations-Hinweis für den Coder:**
  nach dem Build einmal `dotnet run --project src/AiNetLinter
  -- --config rules.json --path . --no-cache` laufen lassen, den
  realen `MaxAIContextFootprint` von `AnalysisToolRegistrations.cs`
  im Output prüfen und den `PathOverride` auf den realen Bedarf
  nachjustieren (nicht blind 3300, sondern was der Linter tatsächlich
  meldet + kleiner Puffer).

- **Kein `McpToolResults`-Helper für Structured-Content** (entgegen
  einer naheliegenden Verallgemeinerung): Konzept
  §"Wo im Projekt"/"Nicht angefasst (bewusst)" Z.109 sagt
  explizit: "`McpToolResults.cs` — bestehende Helper reichen, kein
  neuer Helper nötig". Structured Content wird inline in
  `SafeguardTool.ExecuteAsync` gebaut (Schritt 6 in Datei 1).
  Falls eine zukünftige Tool-Erweiterung auch structured content
  braucht, kann ein `McpToolResults.Structured<T>(value, text)`-
  Helper nachgezogen werden — out of scope hier.

- **JSON-Property-Naming-Policy = CamelCase** (nicht snake_case):
  MCP-Spec erlaubt beides. CamelCase passt zur bestehenden
  C#-Code-Konvention (`Record`-Properties PascalCase → CamelCase
  via `JsonNamingPolicy.CamelCase`) und ist in MCP-Tool-Outputs
  verbreiteter als snake_case. snake_case wäre alternativ
  möglich, aber die MCP-Doku-Beispiele der SDK 2.0.0 nutzen
  CamelCase. Falls ein zukünftiges Tool-Ökosystem snake_case
  bevorzugt, ist der Wechsel eine 1-Zeilen-Änderung in
  `SafeguardTool.ExecuteAsync`.

- **Input-Validierung via `Math.Clamp` statt `InvalidArgument`:**
  Konzept sagt `minScore?` (Default 8.0, Wert in [0,10])
  und `maxViolations?` (Default 20, Wert ≥ 0). Out-of-Range-
  Werte werden defensiv geclampt (kein `throw`, kein
  `IsError=true`-Pfad), weil (a) der Score-Bereich definitionsgemäß
  0-10 ist, (b) der Agent bei Out-of-Range eh kein sinnvolles
  Ergebnis erwarten kann, (c) `INVALID_ARGUMENT` als
  `IsError=false` (recoverable) trotzdem einen strukturierten
  Fehlertext produziert, der für den Agenten weniger hilfreich
  ist als ein sinnvoller Score mit geclamptem Threshold.
  Konsequenz: kein Test für "ungültiger Input" nötig, weil
  Clamping garantiert ein gültiges Ergebnis.

- **Live-Repo-Score-Korridor-≥-5.0 Plausibilität (für step-003
  dokumentiert, hier nur als Kontext für die Tool-Inputs):**
  Konzept §"Muss-Haven" Punkt 9 verlangt "1 Integration-Test
  auf Live-Repo" mit "Live-Score im erwarteten Korridor ≥ 5.0
  für das AiNetLinter-Repo selbst, sonst Bug in Score-Formel".
  Schätzung für das AiNetLinter-Repo (rein informativ, Tool-
  Layer-Step ist davon unabhängig): die reale Solution hat
  mehrere hundert Klassen, einige wenige echte Lint-Verstöße
  (eigener Linter ist auf dem Repo lauffähig, also vermutlich
  < 20 Violations), CC-Durchschnitt moderat (gut strukturiertes
  Projekt, avgCC wahrscheinlich 5-10, unter dem Limit 15),
  Footprint ähnlich (avgFootprint wahrscheinlich 800-1500,
  unter dem Limit 2500), Sealed-Quote hoch (`EnforceSealedClasses`
  ist aktiv, viele Klassen sind `sealed` → Bonus ~+0.5 bis
  +1.0). Score-Schätzung: 10.0 - 5-15 (violations) - 0 (cc) -
  0 (footprint) + 0.5-1.0 (sealed) = 3-6. Korridor 5.0 ist
  plausibel, aber knapp — wenn der reale Score < 5.0 ist, ist
  die Score-Formel (`BuildScoreResult`) zu pessimistisch und
  die Penalty-Einheiten (`ViolationPenaltyUnit` 1.5,
  `FootprintPenaltyPerUnitOverLimit` 0.02) müssen in
  `SafeguardScanner.cs` nachjustiert werden (EPIC-01-Scope,
  Folge-Step nach step-003).

- **Keine Änderung an `McpSufficiencyHints.cs`:** Konzept
  §"Wo im Projekt" Z.111 sagt "ggf. ein safeguard-spezifischer
  Hint in Schritt 2 prüfen". Prüfungsergebnis: `safeguard`
  liefert per Definition einen vollständigen deterministischen
  Score für den Scope — der strukturierte `JsonObject` ist
  die Antwort, nicht ein Text-Output, der "weitere Calls
  nötig" signalisieren könnte. Ein zusätzlicher
  Sufficiency-Hint wäre redundant. Der `Summary`-String im
  `TextContentBlock` transportiert die Score-Information
  menschenlesbar; der `[HINWEIS]: Diese Daten sind
  vollstaendig ...`-Hinweis (direkt im Tool-Wrapper hartkodiert
  in Schritt 6 von Datei 1) ist die einzige Sufficiency-Markierung,
  die das Tool braucht. Wenn die projektweite Sufficiency-Doctrine
  eines Tages einen Tool-spezifischen Hint für `safeguard`
  fordert, ist das ein eigener kleiner Folge-Step.

## Code-Skizze (optional)

```csharp
// src/AiNetLinter/Mcp/Tools/SafeguardTool.cs
#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

internal static class SafeguardTool
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static Task<CallToolResult> ExecuteAsync(SafeguardToolParameters p)
        => ExecuteAsync(p.State, p.ScopeFilter, p.MinScore, p.MaxViolations,
            p.CancellationToken);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? scopeFilter, double minScore,
        int maxViolations, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var configSnapshot = state.GetConfigSnapshot();
        var result = await SafeguardScanner.ComputeScoreAsync(new SafeguardScannerParameters(
            Solution: solution,
            Config: configSnapshot.Config,
            Console: state.Console,
            ScopeFilter: scopeFilter,
            CancellationToken: ct,
            MinScoreThreshold: Math.Clamp(minScore, 0.0, 10.0),
            MaxRemediationEntries: Math.Max(0, maxViolations)));

        if (result.IsMalfunction)
        {
            return McpToolResults.Error(
                LinterErrorCodes.AnalysisFailed,
                "Unerwarteter Fehler bei der Safeguard-Berechnung.",
                context: result.Context,
                hint: "Einmal erneut versuchen — bleibt der Fehler bestehen, LinterEngine-Log pruefen.");
        }

        var score = result.Score!;
        var text = $"{score.Summary}\n\n" +
            "[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten " +
            "Scope — kein zusaetzliches Read/Grep noetig.";
        return new CallToolResult
        {
            IsError = false,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
            StructuredContent = JsonSerializer.SerializeToNode(score, SerializerOptions),
        };
    }
}

internal sealed record SafeguardToolParameters(
    McpCodeGraphServer State,
    string? ScopeFilter,
    double MinScore,
    int MaxViolations,
    CancellationToken CancellationToken);
```

```csharp
// src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs — neuer Block
// (zwischen AddGetViolations und AddSearchPattern in Register(...) aufrufen)
private static void AddSafeguard(
    McpServerPrimitiveCollection<McpServerTool> tools,
    McpCodeGraphServer mcpState,
    McpCallLog? callLog)
{
    tools.Add(McpServerTool.Create(
        async (string? scopeFilter = null, double minScore = SafeguardScanner.DefaultMinScoreThreshold, int maxViolations = SafeguardScanner.DefaultMaxRemediationEntries, CancellationToken ct = default) =>
        {
            if (callLog is null)
            {
                return await SafeguardTool.ExecuteAsync(mcpState, scopeFilter, minScore, maxViolations, ct);
            }
            return await callLog.ExecuteCallAsync("safeguard", $"{scopeFilter}|{minScore}|{maxViolations}",
                () => SafeguardTool.ExecuteAsync(mcpState, scopeFilter, minScore, maxViolations, ct));
        },
        new McpServerToolCreateOptions
        {
            Name = "safeguard",
            Description = SafeguardDescription,
        }));
}

private const string SafeguardDescription =
    "Wann nutzen: Quality-Gate-Wert vor CI-Merge pruefen — deterministischer " +
    "0-10-Score + Pass/Fail-Threshold + Top-Violations + Remediation-Hints fuer " +
    "die geladene Solution. scopeFilter (Projekt-Name oder Pfad-Substring) " +
    "grenzt auf einen Teilbereich ein, minScore ueberschreibt den Default-Threshold " +
    "(8.0), maxViolations begrenzt die Top-Violations-Liste (Default 20).";
```

```json
// rules.json Z.405-410 — PathOverride
"src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs": {
  "Metrics": {
    "MaxAIContextFootprint": 3300
  }
},
```

## Commit-Vorschlag

**Code-Commit** (Conventional Commit auf Deutsch, imperativ, mit
`[safeguard]`-Suffix, Subject max. 72 Zeichen, Body mit
`Refs: tasks/safeguard/step-002` und `### Commit-Vorschlag`-Block
am Ende):

```
feat(mcp): safeguard-Tool mit structured output [safeguard]
```

Subject-Länge: 56 Zeichen (inkl. `[safeguard]`-Suffix, unter dem
72-Zeichen-Limit). Body enthält:
- Hauptsächlich 1 Aufzählungspunkt pro konkreter Änderung
  (Tool-Wrapper, Registrierung, ServerInstructions, PathOverride,
  5+ Unit-Tests)
- Verweis auf Konzept §"Muss-Haven" Punkte 1-3+7 und §"Wie"
  Schritt 2
- Verweis `Refs: tasks/safeguard/step-002` für die
  Spec-§10.3-Konvention
- Hinweis "Live-Repo-Integration-Test folgt in step-003 (separater
  Step, siehe EPIC-02-Aufteilung)" für die Reviewer-Nachvollziehbarkeit
- `### Commit-Vorschlag`-Block (Pflicht nach
  `AiNetLinterRichtlinien.mdc` §4)

**Doku-Commit** (separater Commit per Spec §10.3, trägt
`step-result.md` + Status-Update in `step-plan.md`):

```
docs(task): step-002 Result und Status-Update [safeguard]
```

Subject-Länge: 56 Zeichen.

## Notes

- **Pattern-Konsistenz mit `get_violations`:** `SafeguardTool` ist
  1:1-Pattern-Klon von `GetViolationsTool.cs:25-57` mit 3
  substantiellen Variationen: (a) 5 Input-Parameter statt 1
  (gelöst via Parameter-Record), (b) Structured-Content-Assembly
  statt reinem Text-Output, (c) `IsError=false` IMMER bei
  normalem Score (auch bei `Passed=false`).
- **`IsError`-Policy-Konsistenz:** `get_violations` setzt
  `IsError=true` nur für `SOLUTION_NOT_LOADED` + echte
  Malfunction (siehe `IsErrorPolicy.md` Z.40); `safeguard`
  folgt exakt derselben Linie, mit der zusätzlichen
  Klarstellung, dass `Passed=false` (Score < Threshold)
  semantisch zur gleichen Kategorie "leere/negative
  Treffermenge" gehört wie `get_violations` mit 0 Violations
  (siehe `IsErrorPolicy.md` Z.23: "Ein vollstaendiges,
  definitives 'nichts gefunden' ist kein Fehler"). `Passed=false`
  ist die *Antwort* auf die Quality-Gate-Frage, kein Fehler
  beim Beantworten.
- **Wiederverwendungs-Disziplin:** keine neuen Helper-Klassen,
  keine Schema-Builder, keine JSON-Konverter — alles wird aus
  den vorhandenen Strukturen (`SafeguardScanner.ScoreResult`-
  Record, `McpToolResults` Helper, `McpCodeGraphServer`-
  Properties, `System.Text.Json` SDK-Funktionen) zusammengesetzt.
- **Out of Scope explizit benannt** (zur Vermeidung von
  scope-creep beim Coder): Live-Repo-Integration-Test (step-003),
  `rules.json`-Erweiterung um `safeguard.minScoreDefault`-Section
  (Scanner-Konstante reicht für EPIC-02), TD-001 (fehlende
  `GetViolationsScannerTests.cs`, out of scope für safeguard-
  Task, Nutzer-Sache), `McpSufficiencyHints`-Erweiterung um
  safeguard-spezifischen Hint (nicht nötig, strukturierter
  Output signalisiert Vollständigkeit implizit),
  `McpToolResults.Structured<T>`-Helper (nicht nötig, Konzept
  sagt "kein neuer Helper nötig"), Doku-Updates
  (`Docs/agent-api.md`, `Docs/ROADMAP.md`, `tasks/features/05-roadmap.md` —
  alle EPIC-03), `.agents/rules/AiNetLinter.mdc`-Sync via
  `--sync-agent-rules-only` (EPIC-03, nach Abschluss aller
  Tool-Änderungen).
- **Auffälligkeit für den Orchestrator (für step-003 oder
  EPIC-03):** Der `PathOverride` für `AnalysisToolRegistrations.cs`
  in `rules.json` muss bei jedem weiteren Tool, das diese Datei
  pullt, nachjustiert werden. Mit aktuell 3 Tools ist 3300 ein
  robuster Wert, aber bei 4-5 Tools (> 3500) sollte die
  `AnalysisToolHelpers`-Konsolidierung aus Tech-Debt-Perspektive
  neu bewertet werden. Empfehlung an den Planer für
  zukünftige Tasks: nach jedem Tool, das `AddXxx` zu
  `AnalysisToolRegistrations` hinzufügt, den `PathOverride`
  UND die Helper-Frage prüfen.
