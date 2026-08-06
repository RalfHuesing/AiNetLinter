---
task: features-roadmap
type: empfehlungen-roadmap
status: locked
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

**Bezug:** Diese Roadmap konsolidiert die Erkenntnisse aus `00-master-overview.md` (Synthese) und den 4 Recon-Berichten. Jedes Epic verweist auf seine Quellen. Diese Datei enthält ausschließlich, was wir tatsächlich umsetzen wollen — bewusst gestrichene Ideen samt Begründung stehen in [`06-nicht-umsetzen.md`](06-nicht-umsetzen.md).

---

## 0. Strategische Positionierung

**Drei Sätze, die das Ziel definieren:**

1. **AiNetLinter ist der einzige Roslyn-MCP-Server, der ein vollständiges Quality-Contract-Pattern liefert** (deterministischer Self-Correcting-Loop über `rules.json` als SoT).
2. **AiNetLinter nutzt Roslyns strukturelle Präzision dort, wo Konkurrenzprodukte nur textuelle Heuristiken haben** (ASP.NET-Routing, DI-Registrierungen, gRPC-Contracts).
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

---

## 2. Roadmap-Übersicht

> **Status-Konvention:** `[ ]` offen · `[x]` erledigt. Beim Umsetzen eines Epics hier in der Übersichtstabelle abhaken — die feingranularen Akzeptanzkriterien je Epic stehen in §3.

### Phase Q — Quick Wins (parallel in 1-2 Tagen, 1 Sprint)

| Status | # | Epic | Score | Aufwand | Quelle |
|:--:|:--:|------|------:|--------:|--------|
| [x] | Q1 | **`isError: true`-Audit + Policy** | 70 | 2-3 Tage | Recon A §2.2 K5, Recon B §3.5 |
| [ ] | Q2 | **`reload_config`-Tool** | 55 | 1h | Recon B §8.1 Q2 |
| [ ] | Q3 | **`get_server_health`-Tool** | 50 | 1h | Recon B §8.1 Q3 |
| [x] | Q4 | **ServerInstructions als Single-Source-of-Truth** | 60 | 0.5-1 Tag | Recon A §2.2 K6, Recon D §7.2 |
| [x] | Q5 | **Sufficiency-Hinweise in Tool-Outputs** | 65 | 1 Tag | Recon A §2.2 K2+K5, Recon D §2.4 |
| [ ] | Q6 | **Tool-Description mit Few-Shot-Examples** | 50 | 0.5 Tag pro Tool | Recon B §8.4 Q15 |
| [ ] | Q7 | **Compact-Output-Defaults** (kürzere Standard-Outputs) | 55 | 1 Tag | Recon C §3.3 |

**Gesamt Q-Phase:** 5-8 Arbeitstage. Sofort-ROI: Token-Save, Agent-Stopp-Verhalten, Konfigurations-Turnaround.

### ⚡ Implementierungs-Reihenfolge (TL;DR)

```
Phase 0: Q-Phase (5-8 Tage)        → Quick-Wins, Foundation
Phase 1: S1.2-S1.4 (< 1 Woche)     → safeguard (Killer-Feature) + Structured-Output + call_tree
Phase 2: S2.2, S2.3, S2.5 (2-3 Wo) → pattern_detect, metrics_lookup, metrics_tree
Phase 3: M1, M2, M3, M5 (4-6 Wo)   → ASP.NET-Suite (eigenes Vorhaben), dependency_graph, feature_context, Coverage
```

**Erste sichtbare Wirkung:** Nach Phase 1 (`safeguard` fertig + Baseline-Messung via `--mcp-log`). **Nächste Schritte:** siehe §7.

### Phase S1 — Sprint 1 (< 1 Woche)

| Status | # | Epic | Score | Aufwand | Quelle |
|:--:|:--:|------|------:|--------:|--------|
| [ ] | S1.2 | **`safeguard` (Quality-Contract-Pattern)** | 95 | 3-5 Tage | Recon C §5.1 F2, Recon B §6.3 |
| [ ] | S1.3 | **Structured-Output-Mode** (zentraler JSON-Wrapper) | 70 | 2-3h | Recon B §8.1 Q1+Q16 |
| [ ] | S1.4 | **`get_call_tree`-Tool** (echter Baum, ASCII/Mermaid) | 65 | 1 Tag | Recon B §8.2 Q5 |

**Gesamt Sprint 1:** 4-6 Tage. Sofort-ROI: `safeguard` als Differentiator + Grundlage für eine echte Baseline-Messung (siehe §7), bevor weiter investiert wird.

### Phase S2 — Sprint 2 (2-3 Wochen)

| Status | # | Epic | Score | Aufwand | Quelle |
|:--:|:--:|------|------:|--------:|--------|
| [ ] | S2.2 | **`pattern_detect` (God-Classes, async-void, etc.)** | 90 | 1 Wo | Recon C §5.2 F6, Recon A §3.1 K12 |
| [ ] | S2.3 | **`metrics_lookup` (One-Shot-Metriken)** | 70 | 3-5 Tage | Recon C §5.3 F9 |
| [ ] | S2.5 | **`metrics_tree` (interaktive Codebase-Landkarte / Heatmap-Tree-Walk)** | 85 | 2-3 Tage | User-Idee 2026-08-06, Recon C §4.3 (Aider), Recon A §5.1 |

**Gesamt Sprint 2:** ~2-3 Wochen. Sofort-ROI: Solution-übergreifende Pattern-Audits, deterministische Codebase-Exploration.

### Phase M — Mid-Term (Monat 2-3)

| Status | # | Epic | Score | Aufwand | Quelle |
|:--:|:--:|------|------:|--------:|--------|
| [ ] | M1 | **ASP.NET-Framework-Analyzer-Suite** (6 Rules) — siehe Hinweis unten | 95 | 2 Wo | Recon A §7.2 |
| [ ] | M2 | **`dependency_graph` (NuGet + Projects)** | 75 | 1-2 Wo | Recon C §5.3 F10 |
| [ ] | M3 | **`feature_context` (One-Shot-Feature-Kontext)** | 80 | 1-2 Wo | Recon C §5.2 F7 |
| [ ] | M5 | **`test_coverage_context` (Coverage-Awareness)** | 70 | 1 Wo | Recon C §5.3 F11, Recon B §6.3 |

**Gesamt M-Phase:** 4-6 Wochen. Differenziator-ROI: ASP.NET-Analyse, Coverage-Awareness.

> **Hinweis zu M1:** Betrifft ausschließlich die ASP.NET-Core-Request-Pipeline (Controller-Routes, Minimal-API-Endpoints, Middleware-Reihenfolge, DI-Registrierungen, gRPC-Services, Route-Konflikte) — **nicht** Blazor (dafür existieren bereits eigene Checker, `BlazorRequireCodeBehind`/`BlazorRequireCssIsolation`) und **nicht** Kestrel (Server-Hosting, TLS, Ports — bisher nirgends spezifiziert). Das sind 6 neue Linter-*Regeln*, kein MCP-Interface-Thema — sollte als eigenständiges Vorhaben/eigener Drift-Loop-Task laufen, getrennt von der MCP-Aufwertung.

---

## 3. Detaillierte Epic-Beschreibungen

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

**Warum:** **Größte Differenzierung gegenüber CodeGraphs Regex-Approach.** Mit Roslyn strukturelle ASP.NET-Analyse statt Pattern-Matching. 6 neue Linter-Rules + 1-2 MCP-Tools. Betrifft die ASP.NET-Core-Request-Pipeline (siehe Hinweis in §2) — nicht Blazor oder Kestrel.

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

Sprint 1 (< 1 Wo)
├── S1.2 safeguard        ← unabhängig
├── S1.3 structured-Output ← unabhängig, sehr klein
└── S1.4 call_tree        ← unabhängig

── Baseline-Messung (siehe §7) — vor Sprint 2 einschieben ──

Sprint 2 (2-3 Wo)
├── S2.2 pattern_detect         ← unabhängig
├── S2.3 metrics_lookup         ← unabhängig, klein
└── S2.5 metrics_tree (Heatmap-Tree)  ← unabhängig

Mid-Term (4-6 Wo)
├── M1 ASP.NET-Analyzer-Suite   ← eigenständiges Linting-Vorhaben, siehe Hinweis oben
├── M2 dependency_graph         ← unabhängig
├── M3 feature_context           ← hängt an S2.2
└── M5 test_coverage_context    ← unabhängig
```

**Optimaler Start für den ersten Drift-Loop-Task** (Q-Phase + S1.2 als ein Block):
- 6-8 Tage
- `safeguard` ist der Differentiator und liefert zugleich die Datenbasis (`get_violations`/Score), um danach echte Token-Save-Zahlen zu messen statt CodeGraph-Analogien zu übernehmen
- Q-Phase ist "nebenbei" — gute Quick-Wins für Konfidenz

---

## 5. Akzeptanzkriterien (programmatisch) — Gesamt-Track

| Metrik | Heute | Nach S1+S2 | Nach M-Phase |
|--------|-------|-----------|--------------|
| Anzahl MCP-Tools | 10 | 13 | 15 |
| Anzahl Resources | 1 | 1 | 1 |
| Konkurrenz-Differentiators | 0 | 1 (`safeguard`) | 2 (+ ASP.NET-Suite, separates Vorhaben) |
| Pattern-Detection-Coverage | 0 Patterns | 6 Patterns | 6 Patterns |
| Test-Coverage MCP-Layer | ~30 Files | ~45 Files | ~55 Files |

> Zahlen sind Zielgrößen, keine gemessenen Werte — belastbare Token-Save-Zahlen kommen erst aus der Baseline-Messung nach Sprint 1 (siehe §7).

---

## 6. Konzept-Templates für die ersten Drift-Loop-Tasks

Damit der User direkt in den Drift-Loop starten kann, hier die Mindest-Struktur für `konzept.md` der ersten 2 Tasks (gemäß `drift-loop/spec.md §3.2`):

### Task 1: Q-Phase + S1.2 (safeguard)

```yaml
---
task: mcp-server-auwertung-block1
type: konzept
status: draft
created: 2026-08-06
description: Quick-Wins (isError-Policy, ServerInstructions, Sufficiency) + safeguard
references:
  - tasks/features/00-master-overview.md
  - tasks/features/05-roadmap.md
---

# Was
Q-Phase (7 Quick-Wins) + `safeguard` (S1.2) in einem Block.
Ergebnis: AiNetLinter MCP-Server mit 11 Tools + 1 Resource. Danach: Baseline-Messung via `--mcp-log` als Grundlage für Sprint 2.

# Warum
- Konkurrenz-Differenzierung: `safeguard` (vs. CodeScene/RoslynMcpServer)
- CodeGraph-Lehren (`isError: true` Gift, Sufficiency-Doctrine) adoptieren
- Grundlage für alle weiteren Epics (Dependents: S2.*, M.*)

# Wo
- `src/AiNetLinter/Mcp/ServerInstructions.cs` (neu)
- `src/AiNetLinter/Mcp/IsErrorPolicy.md` (neu, Doku)
- `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs` (neu)
- `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (neu)
- `src/AiNetLinter/Mcp/McpToolResults.cs` (erweitern, isError-Audit)
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (safeguard hinzufügen)
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (ServerInstructions-Wiring)
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (neue Tools registrieren)
- `src/AiNetLinter.Tests/Mcp/...` (15-25 neue Tests)
- `Docs/agent-api.md` (aktualisieren)
- `Docs/ROADMAP.md` (Epic-Status)
- `rules.json` (PathOverrides für neue Files anpassen)
- `.agents/rules/AiNetLinter.mdc` (auto-sync via --sync-agent-rules)

# Wie
Detail-Plan in den jeweiligen Epic-Beschreibungen in `05-roadmap.md` §3.

# Definition of Done
- [ ] Alle 7 Quick-Wins implementiert + getestet
- [ ] `safeguard` funktional + deterministisch + Live-Repo-Test grün
- [ ] `dotnet test` (Volllauf) grün
- [ ] `dotnet build` (mit TreatWarningsAsErrors) grün
- [ ] Doku aktualisiert: `Docs/agent-api.md`, `Docs/ROADMAP.md`, ggf. README
- [ ] ServerInstructions im `initialize`-Response verifiziert
- [ ] Commit pro Epic (Conventional Commits auf Deutsch)
- [ ] Tech-Debt-Einträge (falls welche anfallen) in `<task-dir>/tech-debt.md` notiert
```

### Task 2: Sprint 2 (S2.2 + S2.3 + S2.5)

```yaml
---
task: mcp-server-auwertung-sprint2
type: konzept
status: draft
created: 2026-08-06
description: Pattern-Detection, Metrics-Lookup, Heatmap-Tree-Walk
depends-on: tasks/features/mcp-server-auwertung-block1
references:
  - tasks/features/00-master-overview.md
  - tasks/features/05-roadmap.md
---

# Was
Sprint 2 — 3 Epics in 2-3 Wochen:
- S2.2 `pattern_detect` (God-Classes, async-void, etc., 1 Wo)
- S2.3 `metrics_lookup` (One-Shot-Metriken, 3-5 Tage)
- S2.5 `metrics_tree` (interaktive Heatmap-Tree, 2-3 Tage)

Ergebnis: 13 MCP-Tools + 1 Resource. Codebase-übergreifende Pattern-Audits, interaktive Navigation.

# Warum
- `metrics_tree` = deterministische Top-Down-Codebase-Exploration + Drill-Down
- `pattern_detect` = Audit-Workflows in Sekunden statt Stunden
- `metrics_lookup` = schnelle Quality-Snapshots für Drift-Loop-Kritiker

# Wo
- `src/AiNetLinter/Mcp/Tools/PatternDetectTool.cs` + Scanner (S2.2)
- `src/AiNetLinter/Mcp/Tools/MetricsLookupTool.cs` (S2.3)
- `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs` + Scanner + TreeRenderer (S2.5)
- `rules.json` (Pattern-Konfiguration für S2.2)
- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/ROADMAP.md`
- Tests in `src/AiNetLinter.Tests/Mcp/Tools/...`

# Wie
Detail-Plan in den jeweiligen Epic-Beschreibungen in `05-roadmap.md` §3.

# Definition of Done
- [ ] `metrics_tree` mit 5 Modi + ASCII-Tree-Renderer
- [ ] Jedes Tool mit Live-Repo-Test grün
- [ ] `dotnet test` (Volllauf) grün
- [ ] `dotnet build` (mit TreatWarningsAsErrors) grün
- [ ] Doku aktualisiert: `Docs/agent-api.md`, `Docs/ROADMAP.md`, `Docs/integration.md`
- [ ] Commit pro Epic
- [ ] Tech-Debt-Einträge in `<task-dir>/tech-debt.md`
```

---

## 7. Zusammenfassung & nächste Schritte

**Empfehlung:** Startet mit **Q-Phase (1-2 Tage) + S1.2 `safeguard` (3-5 Tage) als ersten Drift-Loop-Task.** Das ist der Block mit dem höchsten Score/Risiko-Verhältnis und legt den Grundstein für alle weiteren Epics.

**Nicht-blockierende Folge-Tasks** (parallel oder danach): S2.2 (Pattern-Detection), S2.3 (Metrics-Lookup), S2.5 (Metrics-Tree). Alle drei sind unabhängig voneinander und können nach Block 1 parallel laufen.

**Nächste Schritte:**
- Nach Sprint 1 (≈1 Woche): Baseline-Messung via `--mcp-log` — echte Tool-Call- und Token-Zahlen aus dem eigenen Drift-Loop statt CodeGraph-Analogien.
- Nach Sprint 1+2 (≈3-4 Wochen): Ergebnis der Baseline-Messung auswerten, M-Phase-Priorisierung ggf. anpassen.

---

## 8. Decisions Log

User-Anweisung 2026-08-06: „entscheide du bitte (was macht codegraph bzw. was ist wirklich sinnvoll)". Diese Sektion hält die aktuell gültigen Entscheidungen fest, damit sie im Drift-Loop referenzierbar sind.

| # | Frage | Entscheidung | Begründung |
|:--:|-------|--------------|-----------|
| **D1** | Sprint 1+2 als ein Block oder einzeln? | **Zwei separate Drift-Loop-Tasks** (Block 1: Q-Phase + S1.2; Block 2: S2.2+S2.3+S2.5) | Block 1 ist < 1 Woche und hat hohe Cohesion (Foundation + Differentiator). Block 2 ist 2-3 Wochen, anderer Charakter (Pattern-Detection + Metrics). Strikt seriell ist gut für Reviewbarkeit. |
| **D4** | isError-Policy separat oder Vor-Bedingung? | **Vor-Bedingung in Q-Phase** | Jedes weitere Tool (`safeguard` etc.) profitiert von einer sauberen Fehler-Policy — schnellste Lösung: in Q-Phase mit erledigen, nicht später. |
| **D5** | Drift-Loop-Workshop (Planer/Coder/Kritiker SKILL.md) anpassen? | **Ja, nach Block 1** | Erst die neuen Tools verfügbar machen, dann SKILL.md updaten. Vermeidet Spec-Drift. |
| **D6** | `metrics_tree` Modi-Priorität? | **Alle 5 Modi gleichzeitig** | Modi sind trivial zusätzlich (andere Aggregation), kein Grund künstlich zu kürzen. User-Audit-Use-Case „sinnlose Kommentare" braucht comment_density. |
| **D7** | Naming-Konvention (Prefix ja/nein)? | **Kein Prefix** | Konsistent mit bestehenden Tools (`find_symbol`, `get_impact` etc.). MCP-`initialize`-Response listet alle Tools unter dem Server-Namen, Prefix wäre redundant. |
| **D8** | `context_bundle` umbenennen? | **Ja, `feature_context`** | "Bundle" ist vage, "feature_context" macht klar: alles für ein Feature. |
| **D10** | Output-Format-Standard? | **Primär Markdown, ASCII-Tree nur für Hierarchien, Plain-Text für Listen** | Konsistent mit bestehendem AiNetLinter-Standard (`get_violations`/`get_symbol_body` = Markdown, `find_references` = Plain-Text). CodeGraph nutzt dasselbe. |
| **D12** | Reihenfolge? | **Quick-Wins zuerst, dann Sprint 1, dann Sprint 2, dann M-Phase** | Quick-Wins = sofortige Token-Save + Foundation. Sprints = Killer-Feature + Audit-Tools. M = strategische Investitionen (ASP.NET-Suite als eigenes Vorhaben). |
