---
status: ready
type: konzept
task: mcp-safeguard
created: 2026-08-06
last_updated: 2026-08-06
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
references:
  - tasks/features/00-master-overview.md
  - tasks/features/05-roadmap.md (§3 S1.2)
  - tasks/features/06-nicht-umsetzen.md
  - src/AiNetLinter/Mcp/IsErrorPolicy.md
  - src/AiNetLinter/Mcp/ServerInstructions.cs
  - src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs
  - src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs
  - src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs
  - src/AiNetLinter/Mcp/McpToolResults.cs
---

# Konzept: `safeguard` (Quality-Contract-Pattern für AiNetLinter MCP-Server)

## Was

Neues MCP-Tool `safeguard`, das als **deterministischer Quality-Gate** zwischen AI-Output und Merge fungiert. Liefert für die aktuell geladene Solution einen einzelnen Quality-Score (0-10) plus Pass/Fail-Entscheidung gegen einen Schwellwert, dazu die kontextspezifischen Remediation-Hinweise für die Top-Violations.

Ergebnis nach Umsetzung: 11. MCP-Tool, ca. 15-25 neue Tests, dokumentiert in `Docs/agent-api.md`, S1.2 in `tasks/features/05-roadmap.md` auf `[x]`.

## Warum

**Höchster Differentiator** aller Roadmap-Epics (Score 95, Aufwand 3-5 Tage). CodeScene misst: ohne strukturierte Lint-Daten repariert ein Frontier-LLM nur ~20% der Code-Health-Issues; mit strukturierten MCP-Lint-Daten 90-100%. AiNetLinter hat `rules.json` als Single-Source-of-Truth, `LinterEngine` und `get_violations` — der Quality-Loop ist genau **ein** Wrapper entfernt.

Konkreter Use-Case im Drift-Loop (siehe `00-master-overview.md` §5):
> **Kritiker** kann derzeit nicht „gib mir alle God-Classes" oder „ist der aktuelle Stand mergeable?" fragen. `safeguard` deckt genau das ab — eine einzige, deterministische Aussage „Score 6.4, Threshold 8.0, hier sind die 7 kritischsten Violations" statt 8-12 sequenzieller `get_violations`-Calls.

Foundation für alle weiteren Sprints (S2.*, M.*) — die Baseline-Messung via `--mcp-log` nach Abschluss liefert die ersten echten Token-Save-Zahlen statt CodeGraph-Analogien.

## Scope

### Muss-Haben

- Tool `safeguard` registriert in `AnalysisToolRegistrations`
- Input: `scopeFilter?` (analog `get_violations`), `minScore?` (Default aus `rules.json`), `maxViolations?` (Default 20)
- Output als **structured JSON** (MCP-Spec 2026-07-28, JSON Schema 2020-12): `{ passed, score, threshold, violations[], remediation, summary }`
- **Deterministische Score-Berechnung**: gleicher Code + gleiche Config → gleicher Score (Berechnungs-Komponenten dokumentiert)
- Score-Komponenten: aktuelle Violations (gewichtet nach Severity) + CC-Durchschnitt + Footprint-Score + Sealed-Quote
- Remediation-Generator: pro Violation-Typ kontextspezifische Empfehlung (analog zu bestehender `LinterEngine`-Logik)
- ServerInstructions-Update: `safeguard` als Quality-Gate erwähnen (im Stil der bestehenden Tool-Liste)
- 10+ Unit-Tests (Score-Berechnung Klassen, Threshold-Logik, Edge-Cases, Determinismus)
- 1 Integration-Test auf Live-Repo (`AiNetLinter.Tests/McpLiveRepositoryTests` / `McpTestClient`)
- `Docs/agent-api.md` mit Use-Cases + Beispiel-Score-Berechnung
- S1.2 in `tasks/features/05-roadmap.md` Status auf `[x]`

### Nice-to-Have (spätere Iteration)

- Trend über Git-Commits (`safeguard --since=HEAD~5`)
- Per-Projekt-Thresholds in `rules.json`
- Auto-Fix-Vorschläge (Roslyn-CodeActions)

### Non-Goals (bewusst NICHT Teil davon)

- **Mutable Server-State** — `safeguard` ist read-only, keine Edits
- **Auto-Apply von Fixes** — das macht der Agent, nicht der Server
- **Cloud-Storage von Score-History** — Privacy, lokales Tool
- **HTML/Mermaid-Output** — Markdown reicht, Server bleibt schlank
- **Coverage-Integration** — `test_coverage_context` ist separates Epic M5

## Zielplattformen / Technischer Rahmen

- **Sprache:** C# .NET 9 (bestehender Standard)
- **MCP-Spec:** 2026-07-28 (RC) — structured output (JSON Schema 2020-12)
- **Pattern-Reuse:** strikt am bestehenden Tool-Pattern (`<ToolName>Tool.cs` + `<ToolName>Scanner.cs` in `Mcp/Tools/`, Registrierung in `*ToolRegistrations.cs`)
- **Kein DI-Container** (`AiNetLinterRichtlinien.mdc §2`) — statische Klassen wie alle bestehenden Tools
- **Result-Pattern bevorzugt** (`AiNetLinterRichtlinien.mdc §5`) — Fehlerfälle als strukturiertes Result, keine Exceptions
- **TreatWarningsAsErrors** (`AiNetLinterRichtlinien.mdc §5`) — keine neuen Warnings
- **IsError-Policy** (`Mcp/IsErrorPolicy.md`): `passed=false` ist **kein** `isError: true` — das ist erwartetes Verhalten, Agent bekommt Erfolgs-Response mit `passed:false` im JSON

## Verworfene Alternativen

- **Eigenes Severity-Scoring-System:** verworfen, weil `LinterEngine` schon Severity-Grade liefert — wir gewichten, erfinden nicht neu
- **HTML/Mermaid-Output im Tool:** verworfen (D10 in Roadmap) — Markdown/Plain-Text, konsistent mit bestehenden Tools
- **Cloud-basierter Score-Service (CodeScene-Stil):** verworfen, Privacy + lokales Tool bleibt (Roadmap §0 Anti-Goals)
- **Coverage im Score mischen:** verworfen, M5 ist separates Epic, Vermischung verfälscht Quality-Signal
- **Auto-Fix-Rollback-Mechanik:** verworfen (siehe `06-nicht-umsetzen.md` §3) — `preview_refactor` ist bewusst gestrichen, AiNetLinter bleibt Verifikations-Gatekeeper

## Wo im Projekt

**Neue Dateien:**

- `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs` — MCP-Tool-Wrapper, statische `ExecuteAsync`-Methode analog `GetViolationsTool.cs`
- `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` — Score-Berechnung + Remediation-Generator + JSON-Schema-Builder
- `src/AiNetLinter.Tests/Mcp/Tools/SafeguardScannerTests.cs` — Unit-Tests
- `src/AiNetLinter.Tests/Mcp/Tools/SafeguardToolTests.cs` — Unit-Tests für Tool-Wrapper (Loading-State, Solution-Not-Loaded, Scope-Filter)
- `src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.Safeguard.cs` (oder Erweiterung der bestehenden Live-Repo-Tests) — 1 Integration-Test auf AiNetLinter selbst

**Erweiterte Dateien:**

- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` — `AddSafeguard(...)` Methode + Aufruf in `Register(...)`
- `src/AiNetLinter/Mcp/ServerInstructions.cs` — `safeguard` als Quality-Gate in der Tool-Liste erwähnen
- `Docs/agent-api.md` — `safeguard`-Sektion mit Use-Cases, Input/Output-Schema, Beispiel-Call + Beispiel-Antwort
- `Docs/ROADMAP.md` — Status-Update (falls dort S1.2 noch nicht als Roadmap-Item geführt wird; prüfen)
- `tasks/features/05-roadmap.md` — **S1.2 in Tabelle Phase S1 und in §3 auf `[x]` setzen**
- `.agents/rules/AiNetLinter.mdc` — `safeguard` in der automatisch generierten Tool-Liste (via `dotnet run -- --sync-agent-rules-only`)
- `rules.json` — optional `safeguard` Threshold-Default (z.B. `minScoreDefault: 8.0`) ergänzen, falls nicht schon strukturell vorhanden

**Nicht angefasst (bewusst):**

- `McpToolResults.cs` — bestehende Helper reichen, kein neuer Helper nötig
- `LinterEngine` — wir konsumieren nur, ändern nichts an der Engine
- `McpSufficiencyHints.cs` — bestehende Hints reichen, ggf. ein safeguard-spezifischer Hint in Schritt 2 prüfen
- Andere Tool-Registrations-Dateien (`FileStructureToolRegistrations.cs`, `SymbolGraphToolRegistrations.cs`, etc.)

## Entdeckte Mängel/Redundanzen

- **Footprint-Limit (TD-005/006):** Bei jedem neuen Tool riskiert die `AnalysisToolRegistrations`-Klasse das `MaxAIContextFootprint`-Limit (2500). Planer prüft in Step 1, ob Konsolidierung in Helper-Klassen nötig ist; sonst PathOverride moderat anheben. **Entscheidung:** in Step 1 prüfen, entscheiden ad-hoc (kein Vorab-Block).

## Wie (grober Ansatz)

1. **Schritt 1:** Score-Berechnung als reine Funktion (`SafeguardScanner.ComputeScoreAsync(solution, config, scope) → ScoreResult`) — vollständig deterministisch, ohne MCP-Abhängigkeiten, gut unit-testbar. Schema-Definition als C#-Records mit `[JsonSchemaName]`-Attributen (oder per Konvention).
2. **Schritt 2:** Tool-Wrapper (`SafeguardTool.ExecuteAsync`) — nutzt `SafeguardScanner` und übersetzt das `ScoreResult` in `CallToolResult` mit structured content (`McpToolResults` bleibt unverändert; structured content wird separat im `Content`-Block aufgenommen).
3. **Schritt 3:** Registrierung, ServerInstructions, Doku, Verifikation.

**Score-Berechnung (Skizze, Detail-Planung im drift-loop JIT):**

```
score = 10.0
       - 0.1 * (weighted_violations_count)            # CC-weighted
       - 0.05 * (avg_complexity - threshold) * 10     # CC-Durchschnitt
       - 0.02 * (avg_footprint_over_limit)            # Footprint-Score
       + 0.5  * (sealed_quote - 0.5)                  # Sealed-Quote Bonus/Malus
clamped to [0, 10]
passed = score >= threshold (default 8.0, override via input)
```

Diese Formel ist eine **erste Skizze** — der Planer im drift-loop darf sie JIT anpassen, wenn echte Daten (Live-Repo-Score) eine andere Gewichtung nahelegen. Wichtig: **deterministisch** und **dokumentiert** (warum diese Faktoren, warum diese Gewichte).

## Steps (groß, JIT im drift-loop geplant)

Nur 3 Steps — keine Micro-Steps. Jeder Step liefert ein committbares Inkrement.

### Step 1 — `SafeguardScanner` mit Score-Berechnung

- `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` mit `ComputeScoreAsync(solution, config, scopeFilter, ct) → ScoreResult`
- Score-Records: `ScoreResult`, `ViolationEntry`, `RemediationHint`
- 5+ Unit-Tests: leere Solution, einzelne Violation, hoher Score, niedriger Score, Threshold-Logik, Determinismus (zwei Läufe → identischer Score)
- Prüfung: bleibt `AnalysisToolRegistrations` unter dem Footprint-Limit? Wenn nein: Konsolidierung oder PathOverride (siehe Mängel).
- **Definition of Done:** `dotnet test --filter FullyQualifiedName~SafeguardScanner` grün, Scanner-Klasse alleine testbar ohne MCP
- **Commit:** `feat(mcp): SafeguardScanner mit deterministischer Score-Berechnung`

### Step 2 — `safeguard`-Tool & MCP-Integration

- `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs` als dünner Dispatcher auf den Scanner
- `AddSafeguard(...)` in `AnalysisToolRegistrations.Register(...)`
- Input/Output als JSON Schema 2020-12 (structured content im `CallToolResult`)
- ServerInstructions.cs: `safeguard` in der Tool-Liste, mit Verweis „Quality-Gate, vor CI-Merge prüfen"
- 5+ Unit-Tests: Tool-Wrapper, Loading-State, Solution-Not-Loaded, Scope-Filter, passed=false ist NICHT `isError: true`
- 1 Integration-Test in `McpLiveRepositoryTests`: Live-Repo-Score liegt im erwarteten Korridor (≥ 5.0 für das AiNetLinter-Repo selbst, sonst Bug in Score-Formel)
- **Definition of Done:** `dotnet test --filter FullyQualifiedName~Safeguard` grün, Tool im `tools/list` der MCP-`initialize`-Antwort auffindbar
- **Commit:** `feat(mcp): safeguard-Tool mit structured output registriert`

### Step 3 — Verifikation, Doku & Roadmap-Abschluss

- `dotnet test` (Volllauf) grün — keine Regressionen, alle 200+ bestehenden Tests weiterhin grün
- `dotnet build` mit TreatWarningsAsErrors grün
- `Docs/agent-api.md`: neue Sektion `safeguard` mit Use-Cases, Input/Output-Schema, Beispiel-Call, Beispiel-Antwort (Score + Violations + Remediation)
- `Docs/ROADMAP.md`: Status-Update (falls S1.2 dort geführt wird)
- `tasks/features/05-roadmap.md`: S1.2 in Phase-S1-Tabelle auf `[x]` **und** in §3 Akzeptanzkriterien abhaken
- `dotnet run -- --sync-agent-rules-only` für `.agents/rules/AiNetLinter.mdc` Sync
- Tech-Debt-Eintrag in `tasks/safeguard/tech-debt.md` (falls welche anfallen, z.B. Footprint-Limit-Beobachtung)
- **Definition of Done:** `dotnet test` grün, alle 5 §3-Akzeptanzkriterien abgehakt, Doku committet
- **Commit:** `docs: safeguard-Doku und Roadmap-Status S1.2 abgeschlossen`

## Definition of Done (gesamt)

- [ ] `safeguard` Tool funktional registriert, in `tools/list` sichtbar
- [ ] Score-Berechnung deterministisch (verifiziert per Unit-Test)
- [ ] 10+ Unit-Tests grün, 1 Integration-Test auf Live-Repo grün
- [ ] `dotnet test` (Volllauf) grün — keine Regressionen
- [ ] `dotnet build` mit TreatWarningsAsErrors grün
- [ ] `Docs/agent-api.md` mit Use-Cases + Beispiel-Antwort
- [ ] `tasks/features/05-roadmap.md` S1.2 in Tabelle **und** §3 auf `[x]`
- [ ] ServerInstructions erwähnt `safeguard` als Quality-Gate
- [ ] `.agents/rules/AiNetLinter.mdc` via `--sync-agent-rules` synchronisiert
- [ ] Conventional Commits auf Deutsch (einer pro Step, drei total)
- [ ] Tech-Debt-Eintrag (falls angefallen) in `tasks/safeguard/tech-debt.md`

## Quellen-Referenzen

- Epic-Detail: `tasks/features/05-roadmap.md` §3 S1.2
- Konkurrenz-Analyse: `tasks/features/01-codegraph-recon.md` §6.3 (CodeScene-Messung 20% vs. 90-100%)
- Market-Trends: `tasks/features/03-market-research.md` §5.1 F2 (Quality-Contract-Pattern)
- Drift-Loop-Use-Case: `tasks/features/00-master-overview.md` §5 (Kritiker-Rolle)
- Tool-Pattern-Referenz: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` + `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs`
- IsError-Policy: `src/AiNetLinter/Mcp/IsErrorPolicy.md`
- Architektur-Leitplanken: `.agents/rules/AiNetLinterRichtlinien.mdc` §1-§5

## Offene Punkte

Keine — Plan ist umsetzungsreif. Score-Formel ist Skizze, JIT-Verfeinerung im drift-loop ist explizit erlaubt und gewünscht (Step 1 startet mit dem hier dokumentierten Ansatz und passt bei Bedarf an, sobald echte Live-Repo-Daten vorliegen).
