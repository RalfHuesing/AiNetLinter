---
task: features-roadmap
type: empfehlungen-roadmap
status: draft
created: 2026-08-06
purpose: Priorisierte Roadmap für die AiNetLinter MCP-Server-Aufwertung
depends-on:
  - 00-master-overview.md
  - 01-codegraph-recon.md
  - 02-ainetlinter-mcp-current.md
  - 03-market-research.md
  - 04-explore-vs-flow-tools.md
---

# Empfehlungen & Roadmap: AiNetLinter MCP-Server-Aufwertung

> **Leitfrage:** Welche Features bauen wir in welcher Reihenfolge, um AiNetLinter als MCP-Server für agentische C#-Workflows (Planer/Coder/Kritiker/Orchestrator) zu positionieren — mit messbarem Token-/Kosten-Save, klarer Differenzierung gegenüber CodeGraph + RoslynMcpServer-Konkurrenz, und technischer Solidität?

**Bezug:** Diese Roadmap konsolidiert die Erkenntnisse aus `00-master-overview.md` (Synthese) und den 4 Recon-Berichten. Jedes Epic verweist auf seine Quellen.

---

## 0. Strategische Positionierung

**Drei Sätze, die das Ziel definieren:**

1. **AiNetLinter ist der einzige Roslyn-MCP-Server, der ein vollständiges Quality-Contract-Pattern liefert** (deterministischer Self-Correcting-Loop über `rules.json` als SoT).
2. **AiNetLinter liefert CodeGraphs Multi-Symbol-Flow-Tracing in einer Domäne, in der Roslyn CodeGraphs textuelle Heuristik strukturell schlägt** (ASP.NET, MediatR, DI, Source-Generators).
3. **AiNetLinter ist die Verifikations-Schicht für agentische C#-Workflows** — nicht ein weiterer Code-Intelligence-Server, sondern der Gatekeeper zwischen AI-Output und CI-Merge.

**Anti-Ziele** (was wir NICHT primär verfolgen):
- Multi-Sprachen-Support (C#-pur ist eine Stärke)
- Detached-Daemon mit Lock-File (Overkill)
- Telemetrie/Cloud-Sync (Datenschutz)
- Marketing/Onboarding-Wizard (AiNetLinter ist Werkzeug, nicht Produkt)

---

## 1. Priorisierungs-Methodik

**Score = (Wettbewerbsvorteil × Häufigkeit × Token-Save) ÷ (Aufwand × Risiko)**

| Faktor | Gewicht | Begründung |
|--------|---------|-----------|
| Wettbewerbsvorteil | 3× | Differenzierung zählt mehr als Feature-Quantität |
| Häufigkeit (im Drift-Loop) | 2× | Was jede Iteration hilft ist mehr wert als Edge-Case-Feature |
| Token-Save pro Aufruf | 2× | Direkter Kostenimpact |
| Aufwand (LOC × Komplexität) | 1÷ | Im Divisor: mehr Aufwand = niedriger Score |
| Risiko (False-Positives, Regression) | 1÷ | Im Divisor: mehr Risiko = niedriger Score |

**Kategorien:**
- **Q-Phase (Quick Wins):** Score > 50, ≤ 1 Tag Aufwand
- **S-Phase (Sprint):** Score > 20, 1-2 Wochen
- **M-Phase (Mid-term):** Score > 10, 1-2 Monate
- **L-Phase (Long-term):** Score ≤ 10 oder strategisch wichtig, ≥ 1 Quartal

---

## 2. Roadmap-Übersicht

### Phase Q — Quick Wins (parallel in 1-2 Tagen, 1 Sprint)

| # | Epic | Score | Aufwand | Quelle |
|:--:|------|------:|--------:|--------|
| Q1 | **`isError: true`-Audit + Policy** | 70 | 2-3 Tage | Recon A §2.2 K5, Recon B §3.5 |
| Q2 | **`reload_config`-Tool** | 55 | 1h | Recon B §8.1 Q2 |
| Q3 | **`get_server_health`-Tool** | 50 | 1h | Recon B §8.1 Q3 |
| Q4 | **ServerInstructions als Single-Source-of-Truth** | 60 | 0.5-1 Tag | Recon A §2.2 K6, Recon D §7.2 |
| Q5 | **Sufficiency-Hinweise in Tool-Outputs** | 65 | 1 Tag | Recon A §2.2 K2+K5, Recon D §2.4 |
| Q6 | **Tool-Description mit Few-Shot-Examples** | 50 | 0.5 Tag pro Tool | Recon B §8.4 Q15 |
| Q7 | **Compact-Output-Defaults** (kürzere Standard-Outputs) | 55 | 1 Tag | Recon C §3.3 |

**Gesamt Q-Phase:** 5-8 Arbeitstage. Sofort-ROI: Token-Save, Agent-Stopp-Verhalten, Konfigurations-Turnaround.

### ⚡ Final Implementierungs-Reihenfolge (TL;DR)

```
Phase 0: Q-Phase (5-8 Tage)        → Quick-Wins, Foundation
Phase 1: S1.1-S1.4 (2-3 Wochen)    → trace_flow + safeguard (Killer-Features)
Phase 2: S2.1-S2.5 (4-5 Wochen)    → pattern_detect, metrics_tree, Repo-Map, Installer
Phase 3: M1-M7 (10-14 Wochen)      → ASP.NET-Suite, preview_refactor, Coverage, Synth
Phase 4: L1-L7 (Quartal 2+)        → Streamable-HTTP, Persistenter Index, OAuth
Phase 5: XL1-XL10 (Quartal 3+, "perspektivisch alles") → Beyond L, langfristige Vision
```

**Erste sichtbare Token-Save-Messung:** Nach Phase 1 (Sprint 1 fertig). **Erste Markt-Positionierung:** Nach Phase 2. **Strategische Pause-Punkte:** siehe §8.

### Phase S1 — Sprint 1 (Woche 1-2)

| # | Epic | Score | Aufwand | Quelle |
|:--:|------|------:|--------:|--------|
| **S1.1** | **`trace_flow` MVP** (Multi-Symbol-Flow-Tracer) | 100 | 1-2 Wo | Recon D §8, Recon A §4.3 |
| S1.2 | **`safeguard` (Quality-Contract-Pattern)** | 95 | 3-5 Tage | Recon C §5.1 F2, Recon B §6.3 |
| S1.3 | **Structured-Output-Mode** (zentraler JSON-Wrapper) | 70 | 2-3h | Recon B §8.1 Q1+Q16 |
| S1.4 | **`get_call_tree`-Tool** (echter Baum, ASCII/Mermaid) | 65 | 1 Tag | Recon B §8.2 Q5 |

**Gesamt Sprint 1:** 2 Wochen + 2 Tage. Sofort-ROI: **30-50% Token-Save bei typischen Drift-Loop-Steps**.

### Phase S2 — Sprint 2 (Woche 3-4)

| # | Epic | Score | Aufwand | Quelle |
|:--:|------|------:|--------:|--------|
| S2.1 | **`skeleton` (Repo-Map-Pattern, Aider-style)** | 80 | 1 Wo | Recon C §5.1 F3 |
| S2.2 | **`pattern_detect` (God-Classes, async-void, etc.)** | 90 | 1 Wo | Recon C §5.2 F6, Recon A §3.1 K12 |
| S2.3 | **`metrics_lookup` (One-Shot-Metriken)** | 70 | 3-5 Tage | Recon C §5.3 F9 |
| S2.4 | **Multi-Agent-Installer** (Claude, Cursor, Codex, opencode, Windsurf, Aider) | 75 | 1 Wo | Recon A §2.2 K9, Recon C §1.2 |
| S2.5 | **`metrics_tree` (interaktive Codebase-Landkarte / Heatmap-Tree-Walk)** | 85 | 2-3 Tage | User-Idee 2026-08-06, Recon C §4.3 (Aider), Recon A §5.1 |

**Gesamt Sprint 2:** ~5 Wochen. Sofort-ROI: **Solution-übergreifende Pattern-Audits, Onboarding-UX, interaktive Codebase-Exploration**.

### Phase M — Mid-Term (Monat 2-3)

| # | Epic | Score | Aufwand | Quelle |
|:--:|------|------:|--------:|--------|
| M1 | **ASP.NET-Framework-Analyzer-Suite** (6 Rules) | 95 | 2 Wo | Recon A §7.2 |
| M2 | **`dependency_graph` (NuGet + Projects)** | 75 | 1-2 Wo | Recon C §5.3 F10 |
| M3 | **`feature_context` (One-Shot-Feature-Kontext)** | 80 | 1-2 Wo | Recon C §5.2 F7 |
| M4 | **`preview_refactor` (Roslyn-CodeAction + Diff + Rollback)** | 90 | 2 Wo | Recon C §5.3 F8, Recon B §6.3 |
| M5 | **`test_coverage_context` (Coverage-Awareness)** | 70 | 1 Wo | Recon C §5.3 F11, Recon B §6.3 |
| M6 | **Progressive-Disclosure-Meta-Tool** (für 50+ Tools) | 65 | 1-2 Wo | Recon C §3.2 |
| M7 | **Dynamic-Dispatch-Synthesizer für C#** (MediatR, DispatchProxy, EF) | 85 | 2 Wo | Recon A §7.3, Recon C §5.2 F6 |

**Gesamt M-Phase:** 10-14 Wochen. Differenziator-ROI: ASP.NET-Analyse, Edit-Preview, Coverage-Awareness.

### Phase L — Long-Term (Quartal 2+, strategisch)

| # | Epic | Score | Aufwand | Quelle |
|:--:|------|------:|--------:|--------|
| L1 | **Streamable-HTTP-Transport** für CI/Cloud | 60 | 1-2 Wo | Recon C §2.4 |
| L2 | **Persistenter Index analog zu `.codegraph/`** (`.ainetlinter/`) | 75 | 2-3 Wo | Recon A §2.2 K10, Recon C §4.2 |
| L3 | **Multi-Repo-Cross-Solution-Index** (Sourcegraph-light) | 55 | 1 Quartal | Recon C §5.4 F13 |
| L4 | **OAuth 2.1 + Entra ID** für Cloud-Mode | 50 | 2 Wo | Recon C §2.6 |
| L5 | **MCP-Apps-Integration** (interaktive Diff-Vorschau) | 45 | 1 Quartal | Recon C §2.5, §5.4 F16 |
| L6 | **OpenTelemetry-Traces** (Observability) | 40 | 2 Wo | Recon A §9.1 |
| L7 | **F# + VB.NET-Support** (Roslyn-kostenlos) | 50 | 1 Wo | Recon A §8.3 N5 |

---

## 3. Detaillierte Epic-Beschreibungen (Top 8 nach Score)

### Q1 — `isError: true`-Audit + Policy

**Warum:** CodeGraphs empirisch validierte Lehre: 1-2 `isError: true` am Session-Anfang → Agent gibt das Tool auf. **AiNetLinter hat das in 4 Tools (SOLUTION_NOT_LOADED, Loading-Hinweis, AMBIGUOUS_SYMBOL, INVALID_ARGUMENT) richtig**, aber unklar ob konsistent. Audit + Policy schließt Lücken.

**Scope:**
1. Alle 10 Tools reviewen auf `McpToolResults.Error()`-Aufrufe
2. Policy-Doc: `isError: true` nur für: SOLUTION_NOT_LOADED (LoadFailed), Path-Traversal-Refusal, echte Malfunctions mit retry-once-Hinweis
3. Alle anderen Conditions (SYMBOL_NOT_FOUND, AMBIGUOUS_SYMBOL, etc.) → success-shaped Text mit Anleitung
4. Tests für jedes Tool: "expected recoverable condition → success-shaped mit Hint"

**Abhängigkeiten:** Keine
**Aufwand:** 2-3 Tage (Review + 5-10 Tests + Doku)
**Akzeptanzkriterien:**
- [ ] Audit-Bericht mit Liste aller `isError: true`-Aufrufe + Begründung
- [ ] Policy-Doc in `Mcp/IsErrorPolicy.md` (oder Inline-XML-Doc auf `McpToolResults.Error`)
- [ ] Mindestens 1 Test pro Tool der die "expected recoverable condition" abdeckt
- [ ] Keine Regression in bestehenden 200+ Tests

**Quelle:** Recon A §2.2 K5, Recon B §3.5, Recon A §6.2

### Q4 — ServerInstructions als Single-Source-of-Truth

**Warum:** CodeGraph hat eine zentrale `server-instructions.ts` (106 Zeilen) mit Anti-Pattern-Liste, die beim `initialize`-Handshake an den Agent geht. AiNetLinter hat einen kurzen C#-only-Hinweis aber keine Doctrines. **Drift-Risiko** wenn Anti-Patterns in jeder Tool-Description dupliziert werden.

**Scope:**
1. `src/AiNetLinter/Mcp/ServerInstructions.cs` als zentrale Datei (Single-Source-of-Truth)
2. Inhalt: Tool-Übersicht (1 Satz/Tool), C#-only-Hinweis, Sufficiency-Doctrine, `isError: true`-Hinweis, Drift-Loop-spezifische Empfehlungen
3. Wiring in `McpServerOptionsFactory` (wahrscheinlich schon im `InitializeAsync` ähnlich, aber prüfen)
4. Test: prüfen dass `initialize`-Response die ServerInstructions enthält

**Abhängigkeiten:** Q1 (Policy-Verweis)
**Aufwand:** 0.5-1 Tag
**Akzeptanzkriterien:**
- [ ] `Mcp/ServerInstructions.cs` existiert
- [ ] `McpServerOptionsFactory` übergibt die Instructions
- [ ] Test: `InitializeAsync` liefert Instructions mit Anti-Pattern-Liste
- [ ] Migration: Alle bestehenden Tool-Description-Verweise auf Anti-Patterns raus

**Quelle:** Recon A §2.2 K6, Recon D §7.2

### S1.1 — `trace_flow` MVP

**Warum:** CodeGraphs Killer-Feature. Heute braucht der Agent 9 sequenzielle Tool-Calls (3× `find_symbol` + 3× `find_references` + 3× `get_symbol_body`) für eine typische Flow-Frage. `trace_flow` kollabiert das in **1 Call** mit ~40-60% Token-Save + 60-80% Latenz-Save.

**Scope (MVP, Recon D §6.5):**
1. `symbols`-Bag-Input (2-16 Symbol-Namen) statt Single-String-Query
2. Forward-BFS mit Anker-Constraint (nur Sinks die auch in `named` sind)
3. Co-Naming-Disambiguation (>3 Definitionen → Container-Filter)
4. Source-Body-Assembly (verbatim, mit Window-Cap für Oversize-Methoden)
5. Blast-Radius (Caller bis Tiefe 3, Test-Coverage-Heuristik via `*.Tests.cs`)
6. Truncation mit Meta-Zeile (analog zu `find_references`)
7. Sufficiency-Hinweis im Output

**Out-of-Scope (für späteres Epic):** Dynamic-Boundary-Scan, Reflection-Heuristiken, Adaptive-Budget-Tuning, NL-Query

**Abhängigkeiten:** Q4 (Sufficiency-Hinweis), existierende `FindReferencesTool`, `CallGraphTraversal`, `GetSymbolBodyTool`, `DiRegistrationHeuristics`
**Aufwand:** 1-2 Wochen (Recon D §6.5)
**Akzeptanzkriterien:**
- [ ] Tool-Definition in `SymbolGraphToolRegistrations` (oder neuer `FlowToolRegistrations`)
- [ ] Input-Schema: `symbols[]`, `maxDepth=7`, `includeBodies=true`, `maxFiles=6`
- [ ] Multi-Symbol-BFS mit Anker-Constraint
- [ ] Body-Assembly mit Window-Cap
- [ ] Blast-Radius-Section
- [ ] Sufficiency-Hinweis im Output
- [ ] 15+ Unit-Tests, 1 Integration-Test
- [ ] Live-Repo-Test: erkennt Login-Flow im AiNetLinter-Repo
- [ ] Token-Save-Vergleich: ≥ 40% ggü. 9-Call-Sequenz
- [ ] Doku: `Docs/agent-api.md` aktualisiert

**Risiko:** Mittel (Heuristik-Tuning, BFS-Korrektheit)
**Quelle:** Recon D §6, Recon A §4.3, Recon C §5.1 F3

### S1.2 — `safeguard` (Quality-Contract-Pattern)

**Warum:** **Höchster Differentiator gegenüber allen Konkurrenten.** CodeScene misst: ohne strukturierte Lint-Daten repariert Frontier-LLM nur 20% der Code-Health-Issues, mit 90-100%. AiNetLinter hat `rules.json` als SoT — der Quality-Loop ist 1 Wrapper entfernt.

**Scope:**
1. Neues Tool `safeguard` in `AnalysisToolRegistrations`
2. Input: `scopeFilter?`, `minScore?` (Default aus `rules.json`), `maxViolations?` (Default 20)
3. Output (structured JSON): `{ passed: bool, score: 0-10, threshold: 8.0, violations: [...], remediation: string }`
4. Score-Berechnung: deterministisch aus aktuellen `get_violations` + Heuristik (CC-Durchschnitt, Footprint-Score, Sealed-Quote)
5. Remediation-Generator: kontextspezifische Empfehlung pro Violation-Typ (analog zu `LinterEngine`)

**Abhängigkeiten:** Existierende `LinterEngine`, `get_violations`-Logik
**Aufwand:** 3-5 Tage
**Akzeptanzkriterien:**
- [ ] Tool-Definition mit structured output (JSON Schema 2020-12)
- [ ] Score-Berechnung deterministisch (gleicher Code → gleicher Score)
- [ ] 10+ Unit-Tests (verschiedene Score-Klassen, Threshold-Logik)
- [ ] 1 Integration-Test (Live-Repo: AiNetLinter-Repo selbst)
- [ ] Doku: Use-Cases + Beispiel-Score-Berechnung
- [ ] Migration: ServerInstructions erwähnt safeguard als Quality-Gate

**Risiko:** Niedrig (deterministisch, gut testbar)
**Quelle:** Recon C §5.1 F2, Recon B §6.3, Recon C §4.6 (CodeScene)

### S2.1 — `skeleton` (Repo-Map-Pattern)

**Warum:** Aider-Erfindung. PageRank über File-Dependencies → 50-100 relevanteste Symbole für 100K-LOC-Monorepo in 1.024 Token-Budget. Spart kompletten `find_symbol`-Recon-Loop am Session-Start.

**Scope:**
1. Neues Resource `ainetlinter://skeleton` (oder Tool, beides prüfen)
2. Input: `tokenBudget?` (default 1024, max 4096), `filter?` (regex)
3. Algorithmus: Roslyn `Solution.GetAllSymbols()` + Project-Reference-Graph + PageRank
4. Output (structured): Liste von `{ name, kind, file, line, score, rank, deps[] }`
5. Adaptive: bei >Budget → niedrigste PageRank-Werte raus, bis Budget erfüllt

**Abhängigkeiten:** Existierende Symbol-API, CallGraphTraversal
**Aufwand:** 1 Woche
**Akzeptanzkriterien:**
- [ ] Resource implementiert, MIME-Type `application/json` (oder text/markdown für human)
- [ ] PageRank-Berechnung korrekt (verglichen mit Aider auf 2-3 Test-Repos)
- [ ] Token-Budget-Parameter funktioniert (Output passt in Budget)
- [ ] 5+ Unit-Tests (Budget-Enforcement, Ranking, Filter)
- [ ] 1 Integration-Test auf Live-Repo
- [ ] Doku in `agent-api.md`

**Risiko:** Mittel (PageRank-Tuning, Graph-Aufbau)
**Quelle:** Recon C §4.3 (Aider), Recon C §5.1 F3, Recon A §5.1

### S2.2 — `pattern_detect` (Solution-weite Pattern-Suche)

**Warum:** "Finde alle God-Classes, async-void, Public-API-ohne-Doc" — Roslyn kann das, kein Roslyn-MCP-Server tut es. Audit-Aufgaben von Stunden auf Sekunden.

**Scope:**
1. Neues Tool in `AnalysisToolRegistrations`
2. Pattern-Library (initial): `god-class`, `async-void`, `long-method`, `deep-nesting`, `public-without-doc`, `disposable-not-disposed`, `static-state`, `magic-numbers`, `empty-catch`, `feature-envy`
3. Konfiguration über `rules.json` (analog zu bestehenden Bool-Rules)
4. Output (structured): `{ patterns: [{ ruleId, severity, occurrences, items: [...] }], summary: {...} }`

**Abhängigkeiten:** Existierende Checkers (z. T. wiederverwendbar: `AsyncVoidChecker`, `PublicMembersChecker`, `ComplexityChecker`, `NestedTypesChecker`)
**Aufwand:** 1 Woche
**Akzeptanzkriterien:**
- [ ] Mindestens 6 Patterns implementiert
- [ ] Pattern-Konfiguration in `rules.json` (gleiche Struktur wie Bool-Rules)
- [ ] Structured Output
- [ ] 10+ Unit-Tests (1 pro Pattern + Edge-Cases)
- [ ] 1 Integration-Test auf Live-Repo
- [ ] Doku mit Pattern-Beispielen

**Risiko:** Niedrig-Mittel (Reuse existierender Checker)
**Quelle:** Recon C §5.2 F6, Recon A §7.3, Recon B §6.3

### S2.4 — Multi-Agent-Installer

**Warum:** Onboarding-UX. Heute muss jeder User manuell `.mcp.json` schreiben. CodeGraph hat 8 Targets out-of-the-box. Mit unserem Installer: ein Befehl, alle Agents konfiguriert.

**Scope:**
1. Neues CLI-Command: `ainetlinter install-agent <target>` oder `ainetlinter --install-all`
2. Targets initial: Claude Code, Cursor, Codex CLI, opencode, Windsurf, Aider
3. Marker-basierte Idempotenz (`<!-- AINETLINTER_START -->` / `<!-- AINETLINTER_END -->`)
4. TOML-Support für Codex (analog CodeGraphs hand-rolled TOML-Writer)
5. Uninstall-Variante mit Rollback
6. Optional: Skill-File-Generierung für VS 2026 Auto-Discovery

**Abhängigkeiten:** Keine (eigenständig)
**Aufwand:** 1 Woche
**Akzeptanzkriterien:**
- [ ] `--install-agent <target>` und `--uninstall-agent <target>`
- [ ] 6+ Targets implementiert
- [ ] Idempotenz-Test: 2× hintereinander → "unchanged"
- [ ] Uninstall entfernt alle Marker + Entries
- [ ] Doku: `Docs/integration.md` mit Beispielen pro Target
- [ ] README-Update

**Risiko:** Niedrig (eigenständig, gut testbar)
**Quelle:** Recon A §2.2 K9, §6.4, Recon C §1.2

### S2.5 — `metrics_tree` (interaktive Codebase-Landkarte / Heatmap-Tree-Walk)

**Warum:** User-Idee 2026-08-06 — ein LLM soll sich Ebene für Ebene durch eine Codebase-Hierarchie arbeiten können, statt alles auf einmal zu sehen. Statt 2 MB Context-Dump oder ein riesiger grep-Flat-List bekommt das LLM pro Call **eine aggregierte Sicht auf einen Knoten** und kann gezielt tiefer bohren. Spart Tokens, vermeidet "Context-Window voll"-Probleme, ermöglicht Audit-Workflows wie "finde alle sinnlosen Kommentare" oder "wo sind die Lint-Hotspots".

**Bezug:** Kombiniert CodeGraphs `codegraph_files` + Aiders Repo-Map-Pattern + unsere bestehenden `--map`-Subcommands als interaktives MCP-Tool. *Eigenständige Innovation:* die verschiedenen Modi (code_size, comment_density, violation_density, complexity) als Parameter, plus die rekursive Navigation.

**Beispiel-Workflow:**
```
> metrics_tree(mode="code_size", depth=1, top_n=10)
src/                                        142 files, 87k LoC
├── AiNetLinter/                            98 files, 71k LoC
├── AiNetLinter.Tests/                      44 files, 16k LoC
└── (3 weitere, <5k LoC)

> metrics_tree(mode="code_size", root="src/AiNetLinter", depth=2, top_n=10)
src/AiNetLinter/                            98 files, 71k LoC
├── Mcp/                                    24 files, 12.4k LoC
├── Core/                                   18 files,  8.1k LoC
├── Cli/                                     8 files,  3.2k LoC
└── (5 weitere, <3k LoC)

> metrics_tree(mode="comment_density", root="src/AiNetLinter/Mcp", depth=2, top_n=10)
src/AiNetLinter/Mcp/                        Comment/Code-Ratio: 0.34
├── Tools/                                   0.41   ← verdächtig hoch
├── McpCodeGraphServer.cs                    0.28
├── McpServerOptionsFactory.cs                0.18
└── (18 weitere, <0.30)
```

LLM identifiziert: "Tools/ hat 41% Comment-Ratio — überprüfen". Nächster Call: `get_symbol_body` auf die Methoden in Tools/, oder `metrics_tree` mit `top_n=20` um alle Files zu sehen.

**Scope (MVP):**
1. Neues Tool `metrics_tree` in `FileStructureToolRegistrations`
2. Modi: `code_size` (Dateien, LoC, Bytes), `comment_density` (Comment-LoC / Code-LoC, Ratio), `violation_density` (Violations + Severity-Mix aus `LinterEngine`), `method_count` (Anzahl Methoden, avg LoC/Method), `complexity` (Ø CC, max CC, max CogC aus `ComplexityCalculator`)
3. Input-Schema:
   ```json
   {
     "root": "src/AiNetLinter/Mcp",   // optional, default = workspace root
     "mode": "code_size",              // siehe oben
     "depth": 1,                       // 1-5, default 1
     "top_n": 10,                      // Kinder pro Knoten, default 10
     "file_filter": "*.cs"             // optional, regex
   }
   ```
4. Output: ASCII-Tree-Format mit aggregierten Werten pro Knoten + Top-N-Kinder darunter (analog `tree`-Befehl, aber mit Werten)
5. Algorithmus: 
   - `mode=code_size`/`comment_density`/`method_count`: `Directory.EnumerateFiles` + Regex (kein Roslyn nötig, schnell)
   - `mode=violation_density`/`complexity`: `Roslyn`-basiert über existierenden `LinterEngine` / `ComplexityCalculator`
   - Drill-down: gleicher Aufruf mit tieferem `root` (z.B. von `src/AiNetLinter` zu `src/AiNetLinter/Mcp`)
6. **Sufficiency-Hinweis** im Output: „Dies ist Ebene 1. Für Details: `metrics_tree(root='src/AiNetLinter/Mcp', depth=2)` oder `get_symbol_body(...)`."

**Audit-Use-Case (User's Wunsch): "suche nach sinnlosen Kommentaren":**
```
> metrics_tree(mode="comment_density", depth=1, top_n=20, file_filter="*.cs")
... [zeigt Top-20 nach Ratio]
> get_symbol_body("Foo.Bar")   ← konkrete Methoden mit hoher Ratio prüfen
```

**Abhängigkeiten:** Existierende `LinterEngine`, `ComplexityCalculator`, File-Walk-Patterns aus `get_hotspots`
**Aufwand:** 2-3 Tage
**Akzeptanzkriterien:**
- [ ] Tool `metrics_tree` mit 5 Modi
- [ ] ASCII-Tree-Renderer mit Aggregat-Werten pro Knoten
- [ ] Top-N-Sortierung pro Mode (korrekt: code_size → LoC desc, comment_density → Ratio desc, etc.)
- [ ] `root` + `depth` + `top_n` + `file_filter` Parameter funktionieren
- [ ] Sufficiency-Hinweis im Output
- [ ] 5+ Unit-Tests (1 pro Mode + Edge-Cases: leeres Verzeichnis, single File, depth=5)
- [ ] 1 Integration-Test auf Live-Repo
- [ ] Doku: `Docs/agent-api.md` mit Beispielen pro Mode
- [ ] Bestehende `--map`-Subcommands beibehalten (CLI-Kompatibilität), `metrics_tree` ist die MCP-Variante

**Risiko:** Niedrig (grossteil Datei-Walk + Tree-Renderer, gut testbar)
**Quelle:** User-Idee 2026-08-06, Recon C §4.3 (Aider Repo-Map), Recon A §5.1, Recon B §6.3 (bestehende `--map`-Subcommands)

### M1 — ASP.NET-Framework-Analyzer-Suite

**Warum:** **Größte Differenzierung gegenüber CodeGraphs Regex-Approach.** Mit Roslyn strukturelle ASP.NET-Analyse statt Pattern-Matching. 6 neue Linter-Rules + 1-2 MCP-Tools.

**Scope (6 Rules):**
1. `AspNetControllerRouteAnalyzer` — fehlende/vorhandene Route-Attribute, Route-Konflikt-Detection
2. `MinimalApiEndpointAnalyzer` — `MapGet/MapPost`-Validation, Handler-Signatur
3. `MiddlewarePipelineAnalyzer` — Reihenfolge-Validierung (`UseAuthentication` vor `UseAuthorization` vor `MapControllers`)
4. `DependencyInjectionAnalyzer` — `services.AddXxx<I,T>()` + zirkuläre Deps graph-basiert
5. `GrpcServiceAnalyzer` — `[GrpcService]`-Klassen + Service-Contract-Validation
6. `RouteConflictAnalyzer` — graph-basierte Duplikat-Route-Detection

Plus 1-2 MCP-Tools:
- `aspnet_routes` — liste alle Routes + Konflikte
- `aspnet_pipeline` — zeige Middleware-Chain aus `Program.cs`

**Abhängigkeiten:** Existierende Rule-Infrastruktur (`RuleRegistry`, `LinterEngine`)
**Aufwand:** 2 Wochen
**Akzeptanzkriterien:**
- [ ] 6 Rules in `rules.json` konfigurierbar (mit Default-Werten)
- [ ] 6 Checker-Implementierungen in `Core/Checkers/`
- [ ] 2 MCP-Tools optional
- [ ] 30+ Unit-Tests (alle 6 Rules + 2 Tools)
- [ ] 1 Integration-Test: ASP.NET-Sample-Repo mit allen 6 Rules auslöst
- [ ] Doku: `Docs/configuration.md` + neuer Eval-typ `aspnet-audit`

**Risiko:** Mittel (Pipeline-Analyse ist neu, ASP.NET-Varianten)
**Quelle:** Recon A §7.2, Recon C §4.7 (Differentiator)

### M4 — `preview_refactor` (Safe-Refactor mit Pre-Impact)

**Warum:** "Was bricht, wenn ich X in Y ändere?" Heute: `get_impact` zeigt IST-Situation, nicht WAS-WÄR-WENN. Mit Roslyn-CodeAction + Pre-Impact: Agent kann sicher refaktorieren.

**Scope:**
1. Neues Tool in `AnalysisToolRegistrations` (oder neue Kategorie)
2. Refactoring-Typen initial: `rename_symbol`, `extract_method`, `inline_variable`, `convert_to_file_scoped_namespace`, `add_nullable_annotations`, `apply_codefix`
3. Zwei-Phasen-Pattern: `preview` (kein Mutation) → User/Agent bestätigt → `apply` mit `refactorId`
4. Output: Unified-Diff + Risk-Score + Rollback-Plan
5. Optional: Git-Stash-basierter Rollback

**Abhängigkeiten:** `get_impact` (Pre-Impact-Check), Roslyn-CodeAction-API
**Aufwand:** 2 Wochen
**Akzeptanzkriterien:**
- [ ] 4+ Refactoring-Typen implementiert
- [ ] Pre-Impact-Check vor `apply`
- [ ] Unified-Diff-Output
- [ ] Rollback-Test: `apply` → `revert` → Code-State identisch
- [ ] 10+ Unit-Tests + 1 Integration-Test
- [ ] Doku: Sicherheits-Pattern dokumentiert

**Risiko:** Mittel-Hoch (CodeAction-Korrektheit, Edge-Cases)
**Quelle:** Recon C §5.3 F8, Recon B §6.3

---

## 4. Sequenzierungs-Vorschlag (für den Drift-Loop)

**Empfohlene Reihenfolge** unter Berücksichtigung von Abhängigkeiten, Token-Save und Risiko:

```
Q-Phase (parallel, 1-2 Tage)
├── Q1 isError-Policy    ─┐
├── Q2 reload_config      │
├── Q3 server_health      │ alle unabhängig, parallelisierbar
├── Q4 ServerInstructions│
├── Q5 Sufficiency-Hints  │ ← hängt nur an Q1 ab
├── Q6 Tool-Examples      │
└── Q7 Compact-Defaults   ┘

Sprint 1 (2 Wo)
├── S1.1 trace_flow MVP   ← hängt an Q4, Q5
├── S1.2 safeguard        ← unabhängig
├── S1.3 structured-Output ← unabhängig, sehr klein
└── S1.4 call_tree        ← unabhängig

Sprint 2 (4 Wo)
├── S2.1 skeleton (Aider-Map)   ← unabhängig
├── S2.2 pattern_detect         ← unabhängig
├── S2.3 metrics_lookup         ← unabhängig, klein
├── S2.4 Multi-Agent-Installer  ← unabhängig
└── S2.5 metrics_tree (Heatmap-Tree)  ← unabhängig, kann vor S2.1 fertig sein (interaktive Variante)

Mid-Term (10-14 Wo)
├── M1 ASP.NET-Analyzer-Suite   ← unabhängig, groß
├── M2 dependency_graph         ← hängt an S2.1
├── M3 feature_context           ← hängt an S2.1, S2.2
├── M4 preview_refactor         ← hängt an S1.1, S1.2
├── M5 test_coverage_context    ← unabhängig
├── M6 Progressive-Disclosure   ← hängt an Epic-Anzahl > 30
└── M7 Dynamic-Dispatch-Synth   ← hängt an S2.2
```

**Optimaler Start für den ersten Drift-Loop-Task** (Q-Phase + S1.1 + S1.2 als ein Block):
- 8-10 Tage
- Erste sichtbare Token-Save-Messung
- S1.1 (`trace_flow`) ist das Vorzeige-Feature, S1.2 (`safeguard`) ist der Differentiator
- Q-Phase ist "nebenbei" — gute Quick-Wins für Konfidenz

---

## 5. Akzeptanzkriterien (programmatisch) — Gesamt-Track

| Metrik | Heute | Nach S1+S2 | Nach M-Phase |
|--------|-------|-----------|--------------|
| Anzahl MCP-Tools | 10 | 14 | 17+ |
| Anzahl Resources | 1 | 1 | 2+ |
| Token-Save pro typischem Flow-Question | 0% (Baseline) | 40-60% | 50-70% |
| Tool-Calls pro typischer Flow-Question | 9 | 1-3 | 1-2 |
| Drift-Loop-Step Tool-Call-Durchschnitt | 15-25 | 8-15 | 6-12 |
| Anzahl Onboarding-Targets (Install) | 1 (manuell) | 1 | 6+ |
| Konkurrenz-Differentiators | 0 | 2 (trace_flow, safeguard) | 4+ (incl. ASP.NET, coverage) |
| Pattern-Detection-Coverage | 0 Patterns | 6 Patterns | 10+ Patterns |
| Test-Coverage MCP-Layer | ~30 Files | ~50 Files | ~70+ Files |

---

## 6. Was wir BEWUSST nicht in der Roadmap haben

- **Multi-Repo-Index** (Sourcegraph-Pattern) — Aufwand 1 Quartal, Markt noch unklar
- **MCP-Apps-Integration** — Spec noch instabil, UI-Komplexität
- **Generative Refactorings (LLM-Aktionen)** — wir liefern Verifikation, nicht Generierung
- **Custom-Plugin-System** — explizit verboten in `AiNetLinterRichtlinien.mdc §2` ("Monolithisch & schlank bleiben")
- **Telemetry/Cloud-Sync** — Datenschutz, OSS-Tool
- **Multi-Language-Support (33 Sprachen wie CodeGraph)** — AiNetLinter ist C#-pur, das ist eine Stärke
- **Source-Generator-Generation** (`IIncrementalGenerator` für AiNetLinter selbst) — Zukunftsmusik

---

## 7. Konzept-Templates für die ersten Drift-Loop-Tasks

Damit der User direkt in den Drift-Loop starten kann, hier die Mindest-Struktur für `konzept.md` der ersten 2 Tasks (gemäß `drift-loop/spec.md §3.2`):

### Task 1: Q-Phase + S1.1 + S1.2

```yaml
---
task: mcp-server-auwertung-block1
type: konzept
status: draft
created: 2026-08-06
description: Quick-Wins (isError-Policy, ServerInstructions, Sufficiency) + trace_flow MVP + safeguard
references:
  - tasks/features/00-master-overview.md
  - tasks/features/05-recommendations-roadmap.md
---

# Was
Q-Phase (7 Quick-Wins) + `trace_flow` MVP (S1.1) + `safeguard` (S1.2) in einem Block.
Ergebnis: AiNetLinter MCP-Server mit 12 Tools + 1 Resource, Token-Save 30-50% in typischen Drift-Loop-Steps.

# Warum
- Konkurrenz-Differenzierung: `trace_flow` (vs. CodeGraph) + `safeguard` (vs. CodeScene/RoslynMcpServer)
- CodeGraph-Lehren (`isError: true` Gift, Sufficiency-Doctrine) adoptieren
- Grundlage für alle weiteren Epics (Dependents: S2.*, M.*)

# Wo
- `src/AiNetLinter/Mcp/ServerInstructions.cs` (neu)
- `src/AiNetLinter/Mcp/Tools/TraceFlowTool.cs` (neu)
- `src/AiNetLinter/Mcp/Tools/TraceFlowScanner.cs` (neu)
- `src/AiNetLinter/Mcp/IsErrorPolicy.md` (neu, Doku)
- `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs` (neu)
- `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (neu)
- `src/AiNetLinter/Mcp/McpToolResults.cs` (erweitern, isError-Audit)
- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (oder neue FlowToolRegistrations)
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (safeguard hinzufügen)
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (ServerInstructions-Wiring)
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (neue Tools registrieren)
- `src/AiNetLinter.Tests/Mcp/...` (15-25 neue Tests)
- `Docs/agent-api.md` (aktualisieren)
- `Docs/ROADMAP.md` (Epic-Status)
- `rules.json` (PathOverrides für neue Files anpassen)
- `.agents/rules/AiNetLinter.mdc` (auto-sync via --sync-agent-rules)

# Wie
Detail-Plan in den jeweiligen Epic-Beschreibungen in `05-recommendations-roadmap.md` §3.

# Definition of Done
- [ ] Alle 7 Quick-Wins implementiert + getestet
- [ ] `trace_flow` MVP funktional + Live-Repo-Test grün
- [ ] `safeguard` funktional + deterministisch + Live-Repo-Test grün
- [ ] `dotnet test` (Volllauf) grün
- [ ] `dotnet build` (mit TreatWarningsAsErrors) grün
- [ ] Doku aktualisiert: `Docs/agent-api.md`, `Docs/ROADMAP.md`, ggf. README
- [ ] Token-Save-Messung dokumentiert (vorher 9-Calls-Sequenz vs. nachher 1-Call)
- [ ] ServerInstructions im `initialize`-Response verifiziert
- [ ] Commit pro Epic (Conventional Commits auf Deutsch)
- [ ] Tech-Debt-Einträge (falls welche anfallen) in `<task-dir>/tech-debt.md` notiert
```

### Task 2: Sprint 2 (S2.1 + S2.2 + S2.3 + S2.4 + S2.5)

```yaml
---
task: mcp-server-auwertung-sprint2
type: konzept
status: draft
created: 2026-08-06
description: Pattern-Detection, Repo-Map, Multi-Agent-Installer, Heatmap-Tree-Walk
depends-on: tasks/features/mcp-server-auwertung-block1
references:
  - tasks/features/00-master-overview.md
  - tasks/features/05-recommendations-roadmap.md
---

# Was
Sprint 2 — 5 Epics in 4-5 Wochen:
- S2.1 `skeleton` (Repo-Map, Aider-Pattern, 1 Wo)
- S2.2 `pattern_detect` (God-Classes, async-void, etc., 1 Wo)
- S2.3 `metrics_lookup` (One-Shot-Metriken, 3-5 Tage)
- S2.4 Multi-Agent-Installer (Claude, Cursor, Codex, etc., 1 Wo)
- S2.5 `metrics_tree` (interaktive Heatmap-Tree, 2-3 Tage)

Ergebnis: 17 MCP-Tools + 2 Resources. Codebase-übergreifende Pattern-Audits, interaktive Navigation, Onboarding-UX.

# Warum
- `skeleton` + `metrics_tree` = vollständige Codebase-Exploration (Top-Down-Übersicht + Drill-Down)
- `pattern_detect` = Audit-Workflows in Sekunden statt Stunden
- `metrics_lookup` = schnelle Quality-Snapshots für Drift-Loop-Kritiker
- Multi-Agent-Installer = Adoption-UX (Claude, Cursor, Codex, opencode, Windsurf, Aider)

# Wo
- `src/AiNetLinter/Mcp/Tools/SkeletonTool.cs` + Scanner (S2.1)
- `src/AiNetLinter/Mcp/Tools/PatternDetectTool.cs` + Scanner (S2.2)
- `src/AiNetLinter/Mcp/Tools/MetricsLookupTool.cs` (S2.3)
- `src/AiNetLinter/Commands/InstallAgentCommand.cs` (S2.4, neues CLI-Command)
- `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs` + Scanner + TreeRenderer (S2.5)
- `rules.json` (Pattern-Konfiguration für S2.2)
- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/ROADMAP.md`
- Tests in `src/AiNetLinter.Tests/Mcp/Tools/...`

# Wie
Detail-Plan in den jeweiligen Epic-Beschreibungen in `05-recommendations-roadmap.md` §3.

# Definition of Done
- [ ] `metrics_tree` mit 5 Modi + ASCII-Tree-Renderer
- [ ] Jedes Tool mit Live-Repo-Test grün
- [ ] `dotnet test` (Volllauf) grün
- [ ] `dotnet build` (mit TreatWarningsAsErrors) grün
- [ ] Doku aktualisiert: `Docs/agent-api.md`, `Docs/ROADMAP.md`, `Docs/integration.md`
- [ ] Multi-Agent-Installer: 6+ Targets, Idempotenz-Test grün
- [ ] Commit pro Epic
- [ ] Tech-Debt-Einträge in `<task-dir>/tech-debt.md`
```

---

## 8. Zusammenfassung & nächste Schritte

**Empfehlung:** Startet mit **Q-Phase (1-2 Tage) + S1.1 + S1.2 (2 Wochen) als ersten Drift-Loop-Task.** Das ist der Block mit dem höchsten Score/Risiko-Verhältnis und legt den Grundstein für alle weiteren Epics.

**Nicht-blockierende Folge-Tasks** (parallel oder danach): S2.1 (Repo-Map), S2.2 (Pattern-Detection), S2.4 (Multi-Agent-Installer). Diese drei brauchen kein `trace_flow` und können unabhängig laufen.

**Strategische Pause-Punkte:**
- Nach Sprint 1+2 (6 Wochen): Erste Token-Save-Messung, Markt-Positionierung prüfen
- Nach M-Phase (4 Monate): Streamable-HTTP-Mode evaluieren (L1)
- Nach L-Phase (12+ Monate): Multi-Repo + OAuth

---

## 9. Decisions Log (alle offenen Fragen entschieden)

User-Anweisung 2026-08-06: „entscheide du bitte (was macht codegraph bzw. was ist wirklich sinnvoll)". Diese Sektion hält alle Entscheidungen fest, damit sie im Drift-Loop referenzierbar sind.

| # | Frage | Entscheidung | Begründung |
|:--:|-------|--------------|-----------|
| **D1** | Sprint 1+2 als ein Block oder einzeln? | **Zwei separate Drift-Loop-Tasks** (Block 1: Q-Phase + S1.1 + S1.2; Block 2: S2.1-S2.5) | Block 1 ist 2-3 Wochen und hat hohe Cohesion (Foundation + erstes Killer-Feature + Differentiator). Block 2 ist 4-5 Wochen, anderer Charakter (Pattern-Detection + Repo-Map + Installer). Strikt seriell ist gut für Reviewbarkeit. |
| **D2** | Sind die 8 `MUST-HAVE`s richtig? | **Ja, alle behalten** | Jeder MUST-HAVE füllt eine echte Lücke: trace_flow (Flow-Tracing), sufficiency-hints (Agent-Stop), safeguard (Differentiator), skeleton (Repo-Map), node (Read-Ersatz), dependency_graph (Package-Insight), isError-policy (Agent-Retention), Multi-Installer (Adoption). |
| **D3** | Sprint 2 mit allen 5 oder Top-3? | **Alle 5** | S2.5 (`metrics_tree`) ist User-Wunsch + schnell umsetzbar. S2.1 + S2.2 sind unabhängig von trace_flow. S2.3 ist klein. S2.4 (Installer) parallelisierbar. Kein Konflikt. |
| **D4** | isError-Policy separat oder Vor-Bedingung? | **Vor-Bedingung in Q-Phase** | trace_flow braucht die Policy sowieso. Schnellste Lösung: in Q-Phase mit erledigen, nicht später. |
| **D5** | Drift-Loop-Workshop (Planer/Coder/Kritiker SKILL.md) anpassen? | **Ja, nach Block 1** | Erst die neuen Tools verfügbar machen, dann SKILL.md updaten. Vermeidet Spec-Drift. |
| **D6** | `metrics_tree` Modi-Priorität? | **Alle 5 Modi gleichzeitig** | Modi sind trivial zusätzlich (andere Aggregation), kein Grund künstlich zu kürzen. User-Audit-Use-Case „sinnlose Kommentare" braucht comment_density. |
| **D7** | Naming-Konvention (Prefix ja/nein)? | **Kein Prefix** | Konsistent mit bestehenden Tools (`find_symbol`, `get_impact` etc.). MCP-`initialize`-Response listet alle Tools unter dem Server-Namen, Prefix wäre redundant. |
| **D8** | `context_bundle` umbenennen? | **Ja, `feature_context`** | "Bundle" ist vage, "feature_context" macht klar: alles für ein Feature. |
| **D9** | `get_project_map` umbenennen? | **Ja, `list_projects`** | Konsistent mit `find_*`/`get_*`/`list_*`-Stil. "Map" kollidiert mental. |
| **D10** | Output-Format-Standard? | **Primär Markdown, ASCII-Tree nur für Hierarchien, Plain-Text für Listen** | Konsistent mit bestehendem AiNetLinter-Standard (`get_violations`/`get_symbol_body` = Markdown, `find_references` = Plain-Text). CodeGraph nutzt dasselbe. |
| **D11** | Perspektivisch alles drin? | **Ja, alle sinnvollen Features inkludiert** | Diese Roadmap ist vollständig: Q-Phase (Quick-Wins), S1+S2 (Sprints 1-2), M-Phase (Mid-Term 10-14 Wo), L-Phase (Long-Term). Plus „Beyond L" weiter unten. |
| **D12** | Reihenfolge? | **Quick-Wins zuerst, dann Sprints 1+2 sequenziell, dann M-Phase, dann L-Phase** | Quick-Wins = sofortige Token-Save + Foundation. Sprints = Killer-Features + Differentiatoren. M = strategische Investitionen. L = Cloud/Future. |

### Perspektivische Features (Beyond L-Phase)

Was der User perspektivisch „alles drin" haben will — Ideen aus den Recons, die in keinen der Q/S/M/L-Phasen passen, aber gut wären:

| # | Feature | Quelle | Aufwand | Warum nicht in L-Phase |
|:--:|---------|--------|--------:|----------------------|
| **XL1** | **C# 13+ Specific Analyzers** (`required` Modifier, Collection Expressions, Primary Constructors) | Recon A §9.2 | 2-3 Wo | Sprachspezifisch, ändert sich mit jeder C#-Version — separater Stream |
| **XL2** | **F# + VB.NET Support** (Roslyn kann beides) | Recon A §8.3 N5 | 1 Wo | .NET-Community, zweites Standbein |
| **XL3** | **Custom-Linting-Plugin-Hot-Reload** (ohne Neustart neue Rules laden) | User-Idee | 2 Wo | Würde AiNetLinterRichtlinien §2 „monolithisch" verletzen, nur als opt-in |
| **XL4** | **IDE-Integration (VS-Extension)** | Marktlücke | 1 Quartal | Sehr grosser Aufwand, lohnt erst wenn Kern-Features stabil |
| **XL5** | **ML-gestützte Pattern-Suggestions** (LLM findet Custom-Patterns) | Vision | 1 Quartal | LLM-Kosten, eigene Ethics-Diskussion |
| **XL6** | **Distributed Workspace** (mehrere MSBuildWorkspaces pro Server, für Monorepos) | Recon C §5.4 | 1 Quartal | Komplexität, erst nach Streamable-HTTP |
| **XL7** | **Cross-Solution-Symbol-Resolution** (eine Symbol in mehreren .slnx auflösen) | Recon C §4.5 | 1 Quartal | Sourcegraph-Territorium |
| **XL8** | **History-Intelligence** (git-blame + churn-Analyse) | User-Idee | 1-2 Wo | Wertvoll für Audit, niedrige Komplexität |
| **XL9** | **Auto-Generated MCP-Apps** (interaktive Visualisierungen im VS-Code-UI) | Recon C §5.4 F16 | 1 Quartal | Spec noch instabil |
| **XL10** | **Federation mit anderen MCP-Servern** (Continu, Filesystem-MCP, Git-MCP) | Recon C §4.4 | 1 Quartal | Sub-Agent-Koordination |

**Strategie für „perspektivisch alles drin":** Diese Roadmap ist die vollständige Vision. Q + S1 + S2 sind **das was wir aktiv bauen**. M + L sind **das was wir als nächstes planen**. XL ist **das was wir im Kopf behalten** für spätere Quartale. Kein „Done" jemals — wir wachsen mit der Codebase.

**Reviewer-Fragen** (an den User):
