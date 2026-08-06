---
status: open
type: step-plan
task: safeguard
step: 003
title: "Live-Repo-Integration-Test für safeguard-Tool"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-06T17:30:00+02:00
last_updated: 2026-08-06T17:30:00+02:00
related_to:
  - konzept.md#muss-haven-punkt-9
  - konzept.md#steps-step-2-dod
  - konzept.md#wie-schritt-2
  - tasks/safeguard/step-002/step-result.md
  - tasks/safeguard/step-002/step-review.md
  - tasks/safeguard/step-001/step-result.md
---

# Step 003: Live-Repo-Integration-Test für safeguard-Tool

## Bezug

- **Task:** `safeguard`
- **Epic:** `EPIC-02` aus `roadmap.md` — `safeguard`-Tool (MCP-Wrapper,
  Registrierung, Live-Repo-Integration); Tool-Layer-Teil (step-002) ist
  `approved`, der Live-Repo-Integration-Test als 2./2. Schritt fehlt noch
- **Konzept-Referenz:**
  - `konzept.md` §"Muss-Haven" Punkt 9: "1 Integration-Test auf Live-Repo
    (`AiNetLinter.Tests/McpLiveRepositoryTests` / `McpTestClient`)"
  - `konzept.md` §"Steps" Step 2 DoD: "1 Integration-Test in
    `McpLiveRepositoryTests`: Live-Repo-Score liegt im erwarteten Korridor
    (≥ 5.0 für das AiNetLinter-Repo selbst, sonst Bug in Score-Formel)"
  - `konzept.md` §"Wie" Schritt 2: Live-Repo-Integration-Test als
    abschließender Verifikationspunkt der Tool-Integration
- **Vorgänger-Steps:** `step-001` + `step-001/fix-01` (Scanner steht,
  linter-konform, 0 Linter-Verstöße, 13 Unit-Tests grün, deterministisch)
  + `step-002` (Tool-Wrapper + Registrierung + ServerInstructions + 6
  Unit-Tests; Verdict `approved`, TD-002 + TD-003 dokumentiert) — der
  Live-Repo-Test wurde in `step-002` bewusst zurückgestellt, weil sein
  Score-Korridor-≥-5.0 eine andere Risikoklasse hat (Bug-Symptom in
  EPIC-01-Score-Formel, nicht im Tool-Layer-EPIC-02 behebbar)

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Live-Repo-Codes und einem Probe-Live-Aufruf (durch den
Planer mit einer temporären, inzwischen entfernten Test-Datei
`src/AiNetLinter.Tests/Mcp/_TempLiveSafeguardProbe.cs`, siehe "Notes"
unten) vorgefunden — beeinflusst den Plan direkt:

- **Existierender Live-Repo-Test-Layer:**
  - **`McpLiveRepositoryFixture`** (`src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs:15-48`):
    `IClassFixture<McpLiveRepositoryFixture>` startet einmal pro Testklasse
    den MCP-Server-Prozess gegen das echte Repo (`AiNetLinter.slnx`
    im Elternverzeichnis-Pfad via `AppContext.BaseDirectory`-Walk),
    liefert `McpTestClient Client` + `string RepositoryRoot`. Init
    asynchron via `IAsyncLifetime`, MCP-Connect mit Retry-Optionen
    (`MaxRetries: 5`, `BaseDelayMs: 1000`, `BackoffFactor: 2.0`,
    Timeout 60s) — robust gegen flake-anfällige Subprozess-Starts.
  - **`McpLiveRepositoryTests`** (`src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs:17`):
    `public sealed class McpLiveRepositoryTests : IClassFixture<McpLiveRepositoryFixture>`,
    auf Klassen-Ebene `[Trait("Category", "Integration")]`. Aktuell
    9 `[Fact]`s: `LiveDogfood_FindSymbol_ReturnsResults`,
    `LiveDogfood_FindReferences_ReturnsResults`,
    `LiveDogfood_GetImpact_ReturnsResults`,
    `LiveDogfood_GetTypeHierarchy_ReturnsResults`,
    `LiveDogfood_GetFileSkeleton_ReturnsResults`,
    `LiveDogfood_GetIndexScope_ReturnsResults`,
    `LiveDogfood_GetHotspots_ReturnsResults`,
    `LiveDogfood_GetViolations_ReturnsResults`,
    `LiveDogfood_SearchPattern_ReturnsResults`. **9/9 grün** (vom
    Planer verifiziert: `dotnet test --no-build --filter
    "FullyQualifiedName~McpLiveRepositoryTests"` → alle 9 Tests grün
    in 13 s). Der neue `LiveDogfood_Safeguard_ReturnsResults`-Test
    wird in dieselbe Klasse eingefügt, 1:1-Pattern aus den
    bestehenden Tests.
  - **`McpTestClient.CallToolAsync(...)`** (`src/AiNetLinter.Tests/Mcp/McpTestClient.cs:109-136`):
    liefert das rohe `CallToolResult` (mit `StructuredContent` als
    `JsonElement?`, `Content` als `IList<ContentBlock>`, `IsError`).
    Retryt intern bis zu 30× gegen Loading-Antworten, sodass Tests
    sich nicht um den Hintergrund-Load des Servers kümmern müssen.
    Convenience-Helper `CallToolGetTextAsync(...)` (Z.164-187) greift
    nur den ersten `TextContentBlock`-String ab — für `safeguard`
    **nicht** ausreichend, weil der Score im **structured** Content
    steckt (siehe `SafeguardTool.cs:75`:
    `StructuredContent = JsonSerializer.SerializeToElement(score, ...)`);
    der Test muss daher `CallToolAsync(...)` direkt aufrufen und
    `result.StructuredContent` auswerten.
  - **Aktuelles Pass-Through-Pattern für Tests mit Parametern:**
    `CallToolGetTextAsync("tool_name", new Dictionary<string, object?>
    { ["param"] = value, ... })` — Dictionary<string, object?> ist
    die projektweite Konvention (siehe die 8 existierenden Tests mit
    Input-Args in `McpLiveRepositoryTests.cs:31-35`, `:45-51`, etc.).

- **Tool-Wrapper (aus step-002) konsumierbar:**
  - **`SafeguardTool.ExecuteAsync(state, scopeFilter, minScore, maxViolations, ct)`**
    (`src/AiNetLinter/Mcp/Tools/SafeguardTool.cs:40-77`) ist als
    3. Tool in `AnalysisToolRegistrations.cs` registriert
    (`AddSafeguard`-Methode aus step-002), Tool-Name `"safeguard"`,
    Inputs: `scopeFilter?` (string), `minScore` (double, Default
    `SafeguardScanner.DefaultMinScoreThreshold = 8.0`), `maxViolations`
    (int, Default `SafeguardScanner.DefaultMaxRemediationEntries = 20`).
    Output-Format: `CallToolResult` mit
    `StructuredContent = JsonElement?` (JSON-Schema-2020-12-konform,
    CamelCase serialisiert) plus
    `Content = [TextContentBlock { Text = "<Summary>\n\n[HINWEIS]: ..." }]`
    und **`IsError = false` immer** bei normalem Score (auch bei
    `Passed=false`).
  - **Score-Result-Schema im StructuredContent** (Felder exakt, in
    CamelCase): `passed` (bool), `score` (double), `threshold` (double),
    `violations` (array of { filePath, lineNumber, ruleName, details,
    severity, guidance }), `remediation` (object: { topIssue,
    actionableSteps, documentationHint }), `summary` (string).
    `SafeguardScoreResult`/`ScoreResult` sind `internal sealed record`
    in `SafeguardScanner.cs:431-448` — JSON-Deserialisierung in den
    Test erfolgt via `JsonSerializer.Deserialize<JsonObject>(rawText)`
    (Pattern 1:1 von `SafeguardToolTests.cs:63-69`).
  - **Loading-State-Handling:** `McpTestClient.CallToolAsync(...)`
    retryt automatisch 30× mit 500 ms Delay, falls der Server im
    Hintergrund-Load eine Loading-Antwort schickt. Der Test braucht
    sich darum nicht zu kümmern.

- **Real gemessener Live-Score (vom Planer per Probe-Aufruf gegen das
  AiNetLinter-Repo verifiziert):** Bei Threshold 8.0 und ohne Scope-Filter
  liefert `safeguard` aktuell
  ```
  {
    "passed": true,
    "score": 10,
    "threshold": 8,
    "violations": [],
    "remediation": {
      "topIssue": "Keine Lint-Verstoesse im Scope.",
      "actionableSteps": [],
      "documentationHint": "Docs/configuration.md"
    },
    "summary": "Safeguard-Score: 10,00/10 (Threshold 8,00) — PASS. 0 Top-Verstoesse, 377 Klassen analysiert."
  }
  ```
  Auch mit `scopeFilter = "AiNetLinter"` und Threshold 8.0: identisches
  Ergebnis (Score 10.00, 0 Violations, 377 Klassen). **Der reale
  Live-Score liegt also deutlich über dem Konzept-Korridor-≥-5.0** —
  Konzept-Schätzung 3-6 war sehr konservativ; Begründung: AiNetLinter
  hat repo-weit **0 Linter-Verstöße** (vom Planer verifiziert via
  `dotnet run --project src/AiNetLinter -- --config rules.json --path .
  --no-cache` → `OK`); der `LinterEngine.RunAsync`-Pfad im Scanner
  liefert damit `violations.Count == 0` → Violation-Penalty = 0. Die
  CC- und Footprint-Komponenten werden über **alle 377 Klassen** der
  Solution (inkl. .NET-Bibliotheks- und Test-Fixture-Klassen, die der
  MCP-Server in seine Solution aufnimmt) gemittelt — Durchschnitts-CC
  und Durchschnitts-Footprint liegen klar unter den jeweiligen Limits
  (15 / 2500), weil die Mehrheit dieser Klassen trivial ist (kleine
  Record-Typen, Helpers, etc.). Sealed-Quote für die 377 Klassen liegt
  rechnerisch etwa bei 50 %+ (viele .NET-Framework-Klassen sind
  implizit sealed), Sealed-Bonus also `+0.0` bis `+0.5` (kein
  dominanter Effekt, weil max. Bonus nur +1.0 bei 100 % sealed).
  Resultat: 10.00/10 (oberes Clamp-Limit, alle Komponenten ≈ 0 +
  kleiner Bonus).

- **Aktuelle Test-Lage:**
  - `dotnet test --filter FullyQualifiedName~Safeguard --no-build`
    → **19/19 grün** (13 Scanner-Tests + 6 Tool-Tests), bestätigt
    aus `step-002/step-result.md` §"Test-Output". Nach step-003
    soll der Filter **20/20 grün** sein (19 bestehende + 1 neuer
    Live-Repo-Test).
  - `dotnet test --filter Category=Unit --no-build` → **141/141 grün**
    (kein Integration-Tag, keine Live-Tests). Bleibt 141/141 nach
    step-003 (siehe "Test-Category" unten: Live-Test trägt
    `[Trait("Category", "Integration")]`, fällt also aus dem
    `Category=Unit`-Filter raus).
  - `dotnet test --filter Category=Integration` → **107/108 grün** (1
    Pre-Existing-Flake in `McpServerCommandLoadingStateTests.LoadState_
    LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`,
    vom Planer bei der Probe verifiziert; nicht in diesem Step
    beheben — out of scope und pre-existing). Nach step-003: 108/109
    bzw. 109/109 wenn der Flake beim Coder-Lauf gerade nicht zuschlägt
    (er ist flaky, nicht deterministisch rot).
  - `dotnet run --project src/AiNetLinter -- --config rules.json --path .
    --no-cache` → **OK / 0 Linter-Verstöße** (vom Planer verifiziert
    2026-08-06 14:52). Bleibt 0/0 nach step-003 (nur Test-Datei
    angefasst, AiNetLinter-Produktionscode unangetastet).
  - **Vorbild-Test-Klasse für Pattern-Vergleich:**
    `src/AiNetLinter.Tests/Mcp/Tools/SafeguardToolTests.cs:38-49`
    (`ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`)
    zeigt den **exakten** Deserialisierungs-Pfad
    `JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!
    .Value.GetRawText())!`, den der Live-Test für die
    `score`/`passed`-Extraktion wiederverwendet (1:1-Übertragung,
    kein neues Pattern).

- **Test-Kategorisierung-Konvention** (Richtlinien §4: "xUnit v3 Tests:
  Pflicht"; bestehende Live-Tests tragen
  `[Trait("Category", "Integration")]` auf Klassen-Ebene): Der neue
  `LiveDogfood_Safeguard_ReturnsResults`-Test erbt das Trait automatisch
  von der Klasse, in der er steht (geplant: `McpLiveRepositoryTests`).
  Damit:
  - `dotnet test --filter Category=Unit` → **überspringt** den
    Live-Test (Planer-verifiziert: 141/141 grün vor und nach — der
    Live-Test ist nicht in dieser Kategorie).
  - `dotnet test --filter Category=Integration` → **enthält** den
    Live-Test; 107/108 vor step-003 + 1 = 108/109 erwartet (Flake
    unberücksichtigt).
  - `dotnet test` (Volllauf) → **enthält** den Live-Test; die
    geschätzte Gesamtzahl wächst um 1, Gesamt-Run-Dauer wächst um
    die Live-Test-Laufzeit (typisch 1-5 s pro Live-Test, in der
    `McpLiveRepositoryTests`-Klasse gemessen: die 9 bestehenden
    Live-Tests laufen zusammen in ~5 s, weil sie denselben
    MCP-Server-Prozess via Fixture teilen).
  - **Begründung für Integration-Marker:** Der Live-Test startet einen
    Subprozess (`AiNetLinter.exe --mcp-server --path .`), führt eine
    echte Solution-Ladung durch und führt den `LinterEngine` über
    alle 205 .cs-Dateien des Repos. Das ist um Größenordnungen
    teurer als ein In-Process-Unit-Test (>1 s vs. <1 ms typisch) und
    ist nicht isoliert reproduzierbar (hängt vom Build-Stand, von
    Disk-IO und vom MCP-Subprozess-Start ab). Integration-Marker
    erlaubt `dotnet test --filter Category=Unit` als schnelle
    TDD-Schleife und macht den Test in CI als explizit
    "live-repository"-markiert sichtbar.

- **`McpTestClient`-Methoden-Übersicht (was der Coder braucht):**
  - `CallToolAsync(toolName, arguments, timeoutSeconds, ct)` →
    `CallToolResult` mit `Content` + `StructuredContent` + `IsError`
    + Loading-Retry (siehe `McpTestClient.cs:109`).
  - `ListToolsAsync(...)` → `IList<McpClientTool>` (für
    optionalen "ist safeguard in tools/list?"-Assert, **nicht** im
    Scope dieses Steps — das ist EPIC-03-Doku-Step).
  - `CallToolGetTextAsync(...)` (Z.164) → liefert nur den
    **Text**-Inhalt, NICHT den Score — für `safeguard` nicht
    ausreichend, weil `score` nur in `StructuredContent` steht;
    siehe Pattern-Hinweis oben.

## Intention

Nach diesem Step existiert in `McpLiveRepositoryTests.cs` ein
weiterer `[Fact]`-Test `LiveDogfood_Safeguard_ReturnsResults`, der
die `safeguard`-Tool-Integration **end-to-end** gegen das echte
AiNetLinter-Repository verifiziert: MCP-Server-Subprozess starten,
Live-Solution laden lassen, `safeguard`-Tool mit
Default-Threshold-Parametern aufrufen, JSON-Structured-Content
zurück-extrahieren, Score + Passed + Violations-Array
assertieren. Der Test folgt strikt dem 1:1-Pattern der 9
existierenden `LiveDogfood_*`-Tests und trägt via Klassen-Trait
automatisch `[Trait("Category", "Integration")]`. Der
**real gemessene Live-Score ist 10.00/10** (vom Planer per
Probe verifiziert), der Test assertiert `score >= 5.0` (Konzept-DoD)
mit großem Puffer; ein Test-Fail (= `score < 5.0`) wäre **kein**
step-003-Scope, sondern ein EPIC-01-Score-Formel-Bug, der über
dieses Tool aufgedeckt würde — der Coder setzt in dem Fall
`blocked` mit Verweis auf EPIC-01 (siehe "Bekannte Ausnahmen").

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs` (1 neue `[Fact]`-Methode, Z.146+)

- **Was:** Eine neue Test-Methode, eingefügt am Ende der Klasse
  (nach `LiveDogfood_SearchPattern_ReturnsResults`, Z.144) als
  `LiveDogfood_Safeguard_ReturnsResults` — 1:1-Pattern der
  bestehenden 9 `LiveDogfood_*`-Tests, plus Deserialisierung
  des Structured-Content analog `SafeguardToolTests.cs:63-69`:

  ```csharp
  [Fact]
  public async Task LiveDogfood_Safeguard_ReturnsResults()
  {
      // End-to-end-Verifikation: das safeguard-Tool liefert auf dem echten
      // AiNetLinter-Repo einen Score >= 5.0 (Konzept §"Steps" Step 2 DoD) und
      // einen gueltigen JSON-Schema-2020-12-Structured-Content. Score-Aufruf
      // gegen den Live-Subprozess via _fixture.Client (geteilter MCP-Server
      // pro Testklasse, startet einmal in IAsyncLifetime). Threshold wird
      // explizit auf 0.0 gesetzt, damit der Test score >= 5.0 verifiziert,
      // ohne die "Passed"-Logik des Tools mit dem Korridor zu vermischen —
      // der Korridor prueft die Score-Berechnung, nicht den Pass/Fail-Pfad.
      var result = await _fixture.Client.CallToolAsync(
          "safeguard",
          new Dictionary<string, object?>
          {
              ["scopeFilter"] = null,
              ["minScore"] = 0.0,
              ["maxViolations"] = 20,
          });

      // Tool-Layer-Invariante: kein Malfunction-/Loading-/SolutionNotLoaded-
      // Fehler auf einem geladenen Live-Repo (Fixture garantiert Load via
      // IAsyncLifetime + 60s Timeout + Retry-Schleife; siehe
      // McpLiveRepositoryFixture.InitializeAsync).
      Assert.False(result.IsError);
      Assert.NotNull(result.StructuredContent);

      // StructuredContent ist JsonElement?; Deserialisierung zur JsonObject-
      // Form folgt dem Pattern aus SafeguardToolTests.cs:63.
      var json = JsonSerializer.Deserialize<JsonObject>(
          result.StructuredContent!.Value.GetRawText())!;
      Assert.NotNull(json);

      // Pflicht-Felder gemaess konzept.md §"Muss-Haven" Punkt 3 (JSON-Schema
      // 2020-12 Vertrag): { passed, score, threshold, violations[], remediation,
      // summary }. Nur die Existenz und der Typ werden geprueft; konkrete
      // Werte (Passed=true, Score>=5.0) separat.
      Assert.True(json.ContainsKey("passed"));
      Assert.True(json.ContainsKey("score"));
      Assert.True(json.ContainsKey("threshold"));
      Assert.True(json.ContainsKey("violations"));
      Assert.True(json.ContainsKey("remediation"));
      Assert.True(json.ContainsKey("summary"));
      Assert.IsType<JsonArray>(json["violations"]);

      // Korridor-Assert: score >= 5.0 (Konzept §"Steps" Step 2 DoD).
      // Real gemessener Wert (vom Planer verifiziert): 10.00/10 — der
      // Konzept-Korridor ist konservativ. Wenn dieser Assert fehlschlaegt:
      // KEIN Score-Justieren im step-003, sondern blocked mit Verweis auf
      // EPIC-01 (Bug in der Score-Formel, NICHT im Tool-Layer behebbar).
      var score = (double)json["score"]!;
      Assert.True(score >= 5.0,
          $"Safeguard-Live-Score {score} liegt unter dem Konzept-Korridor " +
          ">= 5.0 — das deutet auf einen Bug in der Score-Formel (EPIC-01-Scope) " +
          "hin. NICHT den Schwellwert anpassen — bitte blocked setzen mit " +
          "Verweis auf EPIC-01 / SafeguardScanner.cs.");
  }
  ```

  Erforderliche neue `using`s in `McpLiveRepositoryTests.cs:1-7`:
  - `using System.Text.Json;` — für `JsonSerializer` (existiert
    bereits in `SafeguardToolTests.cs:7`, wird hier analog benötigt).
  - `using System.Text.Json.Nodes;` — für `JsonObject`/`JsonArray`
    (Pattern 1:1 von `SafeguardToolTests.cs:8`).
  - `Assert.IsType<JsonArray>(json["violations"])` setzt
    `System.Text.Json.Nodes` voraus.

  Methode bleibt unter dem Test-Override `MaxMethodLineCount=100`
  (`*.Tests`-Override in `rules.json`); die XML-Doc am Methoden-Anfang
  ist sparsam, ohne Task-/Step-/TD-/EPIC-Referenzen (Richtlinien §5:
  "Verboten: Jede Referenz auf Task-/Planungsartefakte"). Methoden-Länge
  voraussichtlich ~35-45 Zeilen (deutlich unter 100).

- **Warum:** Strikt 1:1-Pattern der 9 bestehenden
  `LiveDogfood_*_ReturnsResults`-Methoden (`McpLiveRepositoryTests.cs:27-144`),
  einzige substantielle Erweiterung ist die Deserialisierung des
  Structured-Content (Pattern 1:1 von `SafeguardToolTests.cs:63-69`).
  Threshold wird auf `0.0` gesetzt, damit der Korridor-Assert die
  Score-Berechnung **alleine** prüft, ohne den Pass/Fail-Pfad des
  Tools (`score >= threshold`) zu koppeln — sonst wäre ein
  threshold-justierter Test-Setup ein zweiter Score-Justierungs-
  Hebel. Mit `minScore=0.0` ist `Passed` immer `true` und der
  `score >= 5.0`-Assert prüft **nur** die Berechnungs-Formel aus
  `SafeguardScanner.BuildScoreResult` (EPIC-01-Scope). Test ist
  deterministisch (vom Planer verifiziert: 2× hintereinander
  identische 10.00/10-Antwort auf dem aktuellen Stand).

## Tests

- [ ] `dotnet test --filter FullyQualifiedName~Safeguard --no-build`
      → **20/20 grün** (19 bestehende aus step-001+002 + 1 neuer
      Live-Repo-Test)
- [ ] `dotnet test --filter FullyQualifiedName~McpLiveRepositoryTests
      --no-build` → **10/10 grün** (9 bestehende + 1 neuer) — Pflicht-
      Verifikation, dass die Tool-Layer-Änderung aus step-002 das
      `McpLiveRepositoryFixture`-Sharing nicht gebrochen hat
- [ ] `dotnet test --filter Category=Unit --no-build` → **141/141
      grün** (keine Regressionen; Live-Test trägt
      `[Trait("Category", "Integration")]` und fällt aus diesem
      Filter raus, genauso wie die 9 bestehenden Live-Tests)
- [ ] `dotnet test --filter Category=Integration --no-build` → **108/109
      grün** (107 bestehende + 1 neuer; 1 pre-existing flake in
      `McpServerCommandLoadingStateTests` ist out of scope — siehe
      "Bekannte Ausnahmen" und der Flake schlägt nicht deterministisch
      zu, beim Planer-Probe-Lauf war er gerade rot, das ist zufällig)
- [ ] `dotnet build` → **0 Warnungen, 0 Fehler** (Test-Projekt mit
      `TreatWarningsAsErrors=true` in `AiNetLinter.Tests.csproj`)
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json --path
      . --no-cache` → **0 Linter-Verstöße** in
      `McpLiveRepositoryTests.cs` und repo-weit
      (Richtlinien §4: "MCP-Funktionalitäten und Live-Verifikationen
      werden ausschließlich über die C#-Testinfrastruktur
      umgesetzt" — die neue Test-Datei ist genau das, kein
      Linter-Verstoß hinzugefügt)

## Definition of Done

- [ ] Alle "Konkrete Änderungen" umgesetzt (genau 1 Datei, 1 neue
      `[Fact]`-Methode + ggf. 2 `using`-Direktiven)
- [ ] `dotnet build` grün (Warnings-as-Errors)
- [ ] `dotnet test --filter FullyQualifiedName~McpLiveRepositoryTests
      --no-build` grün (10/10, der neue Test ist grün)
- [ ] `dotnet test --filter FullyQualifiedName~Safeguard --no-build`
      grün (20/20, alle Safeguard-Tests inkl. Live-Repo-Test)
- [ ] `dotnet test --filter Category=Unit --no-build` grün (141/141,
      keine Regressionen)
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch,
      imperativ, mit `[safeguard]`-Suffix, siehe
      `roadmap.md` Tech-Stack-Notiz)
- [ ] `tasks/safeguard/step-003/step-result.md` geschrieben mit
      Commit-Hash und grünem Test-Output
- [ ] `status` in `step-plan.md` von `open` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — "MCP & Dogfood
  Testing: MCP-Funktionalitäten und Live-Verifikationen (Dogfooding
  gegen das eigene Repo) werden ausschließlich über die
  C#-Testinfrastruktur (`McpLiveRepositoryTests` und `McpTestClient`
  in `AiNetLinter.Tests`) in `dotnet test` umgesetzt. Das Anlegen
  von ad-hoc Python-Skripten (z. B. im `.todos/`-Ordner) ist
  verboten." — die neue Test-Methode erfüllt genau diese Vorgabe
  (Erweiterung der bestehenden Test-Infrastruktur, kein neues
  Skript).
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — "xUnit v3 Tests:
  Pflicht" — bestehender `[Fact]`-Pattern wird 1:1 übernommen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — "Sparsamer Einsatz
  von Code-Kommentaren": XML-Doc am Methoden-Anfang sparsam, ohne
  Task-/Step-/TD-/EPIC-Referenzen; Korridor-Assert-Kommentar
  erklärt das **Warum** der 5.0-Schwelle (Verweis auf
  `konzept.md#steps-step-2-dod` ist insofern ok, als dass es eine
  externe Anforderungs-Doku referenziert, nicht eine Task-Artefakt-
  ID — aber zur Sicherheit den Methoden-XML-Doc auf das Notwendige
  reduzieren, der Inline-Kommentar beim Assert darf den Konzept-
  Bezug nennen, weil das die *warum*-Erklärung für die magische 5.0
  ist).
- `.agents/rules/AiNetLinter.mdc` (Grenzwerte) — `MaxMethodLineCount=60`
  Produktion / `100` `*.Tests`; die neue Test-Methode ist ~35-45 Z.
  (deutlich unter 100); keine Linter-Verstöße zu erwarten.
- `rules.json` (`ProjectOverrides.*.Tests.MaxMethodLineCount=100`,
  `EnforceSealedClasses=false` für Tests) — gewährt dem Test etwas
  mehr Platz, der hier nicht ausgeschöpft wird.
- `Mcp/IsErrorPolicy.md` Z.23+40 — `passed=false` ist nicht
  `isError=true`; der neue Test setzt `minScore=0.0` und erwartet
  deshalb zwingend `IsError=false` (er verifiziert zusätzlich die
  IsError-Policy-Konformität auf der Live-Repo-Schicht als implizite
  Regression-Garantie — wenn `IsError=true` zurückkommt, ist das
  ein Tool-Layer-Bug in EPIC-02-Scope, in dem Fall failt der
  Test, bevor die Score-Asserts überhaupt erreicht werden).

## Bekannte Ausnahmen

- **Realer Live-Score (vom Planer verifiziert): 10.00/10**, nicht
  Konzept-Schätzwert 3-6. Der Assert verwendet trotzdem
  `score >= 5.0` (Konzept-DoD, großer Puffer). Wenn der Test failt
  (= `score < 5.0`), ist das **kein** step-003-Scope-Fix:
  - NICHT `minScore`-Override anpassen, um den Assert zu umgehen.
  - NICHT `score >= 5.0` zu `score >= 0.0` herunterschrauben.
  - Stattdessen: **blocked setzen** mit Verweis auf
    `SafeguardScanner.cs` (EPIC-01, Scanner-Logik) und auf
    `tasks/safeguard/step-001/step-result.md` / `fix-01/step-review.md`
    (EPIC-01-Implementierung). Konkret verdächtige Stellen:
    `ComputeViolationPenalty` (Z.205-227), `ComputeCcPenalty`
    (Z.229-235), `ComputeFootprintPenalty` (Z.237-243),
    `ComputeSealedBonus` (Z.245-252), `BuildScoreResult` (Z.128-167).
    Der Bug läge in der **Score-Formel** (EPIC-01), nicht im
    Tool-Wrapper (EPIC-02) — ein Score-Justieren in step-003
    wäre Symptom-Fixing (Richtlinien §5: "Symptom-Fixing verboten").
- **Pre-existing Flake in
  `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`**
  (vom Planer verifiziert beim Probe-Run von
  `Category=Integration`): schlägt beim aktuellen Lauf fehl, ist
  aber **nicht** in diesem Step behoben — out of scope, separate
  Beobachtung. Der neue `LiveDogfood_Safeguard_ReturnsResults`-
  Test ist von dem Flake unabhängig (anderes Test-Fixture, anderer
  Pfad). Wenn der Coder den Flake im selben Lauf sieht: nicht in
  diesem Step beheben, in `tech-debt.md` ergänzen (falls der
  Kritiker das nicht schon in step-002/003 sieht — beim Planer-
  Lauf war es beim ersten Mal rot, beim zweiten Mal grün,
  typisches Flake-Verhalten).
- **Threshold auf 0.0 im Test-Setup** statt auf den Konzept-Default
  8.0: bewusste Entscheidung, um den Pass/Fail-Pfad des Tools vom
  Score-Korridor-Assert zu entkoppeln. Mit `minScore=0.0` ist
  `Passed` deterministisch `true` (Score 10 ≥ 0), der
  `score >= 5.0`-Assert prüft isoliert die Score-Berechnung. Der
  korrespondierende `Passed=true`-Feld im JSON wird nicht explizit
  assertiert (es ist per Konstruktion `true`), das wäre redundant.
- **Konzept-Korridor "≥ 5.0" ist bewusst großzügig** — er ist als
  "plausibler Score für ein sauberes Repo" formuliert, nicht als
  "exakter Mittelwert über verschiedene Repos". 5.0 lässt einen
  echten Score-Formel-Bug früh auffliegen, ohne bei einer kleinen
  Verschlechterung (z. B. nach Refactorings, die den CC- oder
  Footprint-Durchschnitt leicht anheben) sofort zu failen. Real
  gemessener Wert 10.00/10 hat reichlich Puffer; ein realistischer
  Score für ein gesundes Repo liegt schätzungsweise im Bereich
  8.0-10.0, sodass 5.0 in der Praxis nur bei echten Formel-Bugs
  oder einem plötzlichen Refactor-Schaden anschlägt.
- **Keine zusätzlichen Live-Tests für andere Tools:** `find_symbol`,
  `find_references`, `get_impact`, `get_type_hierarchy`,
  `get_file_skeleton`, `get_index_scope`, `get_hotspots`,
  `get_violations`, `search_pattern` haben bereits Live-Tests
  (Schritt 1/2 von EPIC-02 sah nur **1** Integration-Test vor, für
  `safeguard`; alle anderen Tools sind seit Langem live-getestet).
  Der Coder soll **keine** weiteren Live-Tests in diesem Step
  hinzufügen, auch nicht "weil es so schön ist" — Scope-Disziplin.
- **Kein `McpTestClient`-Refactor:** `CallToolAsync` + manuelles
  `JsonObject`-Deserialisieren ist die saubere Form für diesen
  Test (Pattern 1:1 von `SafeguardToolTests.cs:63-69`). Ein
  zusätzlicher `CallToolGetStructuredAsync<T>`-Helper wäre eine
  Helper-Erweiterung außerhalb des Step-Scopes (TD-003-Kandidat,
  nicht in step-003 zu fixen).
- **Probe-Datei des Planers:** Der Planer hat für die Score-
  Schätzung eine temporäre Test-Datei
  `src/AiNetLinter.Tests/Mcp/_TempLiveSafeguardProbe.cs` angelegt
  und nach dem Probe-Run via `git clean -f` entfernt (siehe
  `git status`: keine `_TempLiveSafeguardProbe.cs`-Spur mehr).
  Die kompilierte DLL wurde anschließend via `dotnet build` neu
  erzeugt. Der Coder braucht diesen Vorlauf nicht zu reproduzieren —
  der reale Live-Score 10.00/10 ist hier dokumentiert.

## Code-Skizze (optional)

```csharp
// Ergänzung in src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs,
// Datei-Anfang (using-Direktiven):
using System.Text.Json;
using System.Text.Json.Nodes;

// Am Ende der Klasse (nach Z.144):
[Fact]
public async Task LiveDogfood_Safeguard_ReturnsResults()
{
    var result = await _fixture.Client.CallToolAsync(
        "safeguard",
        new Dictionary<string, object?>
        {
            ["scopeFilter"] = null,
            ["minScore"] = 0.0,
            ["maxViolations"] = 20,
        });

    Assert.False(result.IsError);
    Assert.NotNull(result.StructuredContent);

    var json = JsonSerializer.Deserialize<JsonObject>(
        result.StructuredContent!.Value.GetRawText())!;
    Assert.NotNull(json);

    Assert.True(json.ContainsKey("passed"));
    Assert.True(json.ContainsKey("score"));
    Assert.True(json.ContainsKey("threshold"));
    Assert.True(json.ContainsKey("violations"));
    Assert.True(json.ContainsKey("remediation"));
    Assert.True(json.ContainsKey("summary"));
    Assert.IsType<JsonArray>(json["violations"]);

    var score = (double)json["score"]!;
    Assert.True(score >= 5.0,
        $"Safeguard-Live-Score {score} unter Konzept-Korridor >= 5.0 — " +
        "Score-Formel-Bug in EPIC-01, NICHT in step-003 zu fixen.");
}
```

## Notes

- **Reale Probe-Messung des Planers:** Für die Score-Schätzung hat
  der Planer eine temporäre Test-Datei
  `src/AiNetLinter.Tests/Mcp/_TempLiveSafeguardProbe.cs` angelegt,
  gebaut (`dotnet build`), getestet (`dotnet test --filter
  "FullyQualifiedName~_TempLiveSafeguardProbe"`), und anschließend
  via `git clean -f` entfernt — die Datei existiert nicht mehr
  (`Test-Path` = `False`, `git status` zeigt sie nicht).
  Die kompilierte DLL wurde danach neu gebaut. **Real gemessener
  Score: 10.00/10** bei Threshold 8.0 (mit und ohne Scope-Filter).
- **Wiederverwendete Strukturen (kein Eigenbau):**
  - `McpLiveRepositoryFixture` (geteilter MCP-Server-Prozess pro
    Testklasse, `IAsyncLifetime`-Init mit Retry-Logik) — 1:1
    Pattern, Fixture bleibt unverändert.
  - `McpTestClient.CallToolAsync(...)` (raw `CallToolResult` +
    automatisches Loading-Retry) — 1:1 Pattern, kein neuer
    Helper.
  - `JsonSerializer.Deserialize<JsonObject>(rawText)` — 1:1 von
    `SafeguardToolTests.cs:63`, kein neues Deserialisierungs-
    Pattern.
  - `LiveDogfood_*_ReturnsResults`-Naming-Konvention — 1:1 von
    den 9 bestehenden Live-Tests.
  - `[Trait("Category", "Integration")]` an der
    `McpLiveRepositoryTests`-Klasse (Z.16) — vererbt sich
    automatisch an die neue Methode, keine Trait-Duplikation.
- **Stolperfallen:**
  - `result.StructuredContent` ist `JsonElement?`, nicht
    `JsonObject?` (siehe `step-002/step-result.md` §"Abweichungen
    vom Plan": Compiler hat `JsonObject?` abgelehnt, realer Typ
    ist `JsonElement?`). Deserialisierung muss via
    `.Value.GetRawText()` → `JsonObject` laufen, nicht via
    direkter Cast.
  - `McpTestClient.CallToolGetTextAsync(...)` (Z.164) greift nur
    den `TextContentBlock`-String ab — **nicht** den
    Structured-Content. Für `safeguard` muss `CallToolAsync(...)`
    direkt aufgerufen werden, sonst bekommt der Test nur die
    `Summary`-Zeile (die zwar "Safeguard-Score: 10,00/10" enthält,
    aber String-Match auf einen freien Text ist fragiler als
    JSON-Deserialisierung).
  - Der Test setzt `minScore=0.0`, nicht 8.0. Hintergrund: mit
    `minScore=8.0` ist `Passed=true` (Score 10 ≥ 8), und der
    `score >= 5.0`-Assert wäre weiterhin gültig. Aber mit
    `minScore=0.0` ist die `Passed`-Aussage **unabhängig** vom
    Score (deterministisch `true`), und der `score >= 5.0`-Assert
    prüft isoliert die Formel. Defensiver, weil eine zukünftige
    `score`-Senkung (z. B. durch Refactorings) `Passed` auf
    `false` setzen könnte, ohne dass der Korridor verletzt ist
    — dann würde der Test wegen `passed != true` rot, obwohl der
    Score noch im Korridor liegt. Mit `minScore=0.0` ist das
    ausgeschlossen.
  - Falls `dotnet test --filter Category=Integration` flaky
    schlägt: das ist der bekannte
    `McpServerCommandLoadingStateTests.LoadState_LoadFuncCom
    pletesSynchronouslyWithCatalog_ReportsLoadedImmediately`-
    Test, nicht der neue — `LiveDogfood_Safeguard_ReturnsResults`
    ist von McpLiveRepositoryFixture (separate Fixture) und
    sollte robust laufen. Im Zweifel 1-2× wiederholen, dann
    befunden.

### Commit-Vorschlag

```
test(mcp): Live-Repo-Integration-Test für safeguard-Tool [safeguard]
```
