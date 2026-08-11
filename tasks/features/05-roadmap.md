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

> **Status-Konvention:** `[ ]` offen · `[x]` erledigt. Beim Umsetzen eines Epics hier in der Übersichtstabelle abhaken — die feingranularen Akzeptanzkriterien je Epic stehen in §3. `[ ]*` bedeutet: in Arbeit, Plan existiert (siehe §3 unter dem jeweiligen Epic) — wird erst nach Abschluss des Plans auf `[x]` gesetzt.

### Phase Q — Quick Wins (parallel in 1-2 Tagen, 1 Sprint)

| Status | # | Epic | Score | Aufwand | Quelle |
|:--:|:--:|------|------:|--------:|--------|
| [x] | Q1 | **`isError: true`-Audit + Policy** | 70 | 2-3 Tage | Recon A §2.2 K5, Recon B §3.5 |
| [x] | Q2 | **`reload_config`-Tool** | 55 | 1h | Recon B §8.1 Q2 |
| [x] | Q3 | **`get_server_health`-Tool** | 50 | 1h | Recon B §8.1 Q3 |
| [x] | Q4 | **ServerInstructions als Single-Source-of-Truth** | 60 | 0.5-1 Tag | Recon A §2.2 K6, Recon D §7.2 |
| [x] | Q5 | **Sufficiency-Hinweise in Tool-Outputs** | 65 | 1 Tag | Recon A §2.2 K2+K5, Recon D §2.4 |
| [x] | Q6 | **Tool-Description mit Few-Shot-Examples** | 50 | 0.5 Tag pro Tool | Recon B §8.4 Q15 |
| [x] | Q7 | **Compact-Output-Defaults** (kürzere Standard-Outputs) | 55 | 1 Tag | Recon C §3.3 |

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
| [x] | S1.2 | **`safeguard` (Quality-Contract-Pattern)** | 95 | 3-5 Tage | Recon C §5.1 F2, Recon B §6.3 |
| [x] | S1.3 | **Structured-Output-Mode** (zentraler JSON-Wrapper) | 70 | 2-3h | Recon B §8.1 Q1+Q16 |
| [x] | S1.4 | **`get_call_tree`-Tool** (echter Baum, ASCII/Mermaid) | 65 | 1 Tag | Recon B §8.2 Q5 |

**Gesamt Sprint 1:** 4-6 Tage. Sofort-ROI: `safeguard` als Differentiator + Grundlage für eine echte Baseline-Messung (siehe §7), bevor weiter investiert wird.

### Phase S2 — Sprint 2 (2-3 Wochen)

| Status | # | Epic | Score | Aufwand | Quelle |
|:--:|:--:|------|------:|--------:|--------|
| [x] | S2.2 | **`pattern_detect` (God-Classes, async-void, etc.)** | 90 | 1 Wo | Recon C §5.2 F6, Recon A §3.1 K12 |
| [ ] | S2.3 | **`metrics_lookup` (One-Shot-Metriken)** | 70 | 3-5 Tage | Recon C §5.3 F9 |
| [x] | S2.5 | **`metrics_tree` (interaktive Codebase-Landkarte / Heatmap-Tree-Walk)** | 85 | 2-3 Tage | User-Idee 2026-08-06, Recon C §4.3 (Aider), Recon A §5.1 |

**Gesamt Sprint 2:** ~2-3 Wochen. Sofort-ROI: Solution-übergreifende Pattern-Audits, deterministische Codebase-Exploration.

### Phase M — Mid-Term (Monat 2-3)

| Status | # | Epic | Score | Aufwand | Quelle |
|:--:|:--:|------|------:|--------:|--------|
| [ ] | M1 | **ASP.NET-Framework-Analyzer-Suite** (6 Rules) — siehe Hinweis unten | 95 | 2 Wo | Recon A §7.2 |
| [x] | M2 | **`dependency_graph` (NuGet + Projects)** | 75 | 1-2 Wo | Recon C §5.3 F10 |
| [ ] | M3 | **`feature_context` (One-Shot-Feature-Kontext)** | 80 | 1-2 Wo | Recon C §5.2 F7 |
| [ ] | M5 | **`test_coverage_context` (Coverage-Awareness)** | 70 | 1 Wo | Recon C §5.3 F11, Recon B §6.3 |
| [x] | M8 | **`--eval`/`--map` ersatzlos streichen** (Audit-Prompts + Codebase-Maps) | 60 | 2-3 Tage | Nutzer-Entscheidung 2026-08-11, Dogfooding-Session |
| [ ] | M9 | **Drift-Audit — DRY-Erkennung** (`find_duplicates` + `DuplicateCodeChecker` + Self-Audit-Skill + Refactoring-Drift) | 65 | 8-11 Tage | Nutzer-Anliegen 2026-08-11, [`07-drift-audit-ideen.md`](07-drift-audit-ideen.md), Phase-1-Spec 2026-08-11 |

**Gesamt M-Phase:** 4-6 Wochen. Differenziator-ROI: ASP.NET-Analyse, Coverage-Awareness, deterministische DRY-Erkennung ohne Embeddings.

> **Priorisierungs-Update (Dogfooding-Session 2026-08-10/11):** Nach einer Session, in der der
> MCP-Server systematisch als Navigations-Werkzeug für einen "großen Task, Code-Stellen selbst
> finden" durchgetestet wurde (siehe Session-Notizen), ist **M2 `dependency_graph` die naechste
> sinnvolle Prioritaet** — die konkret erlebte Luecke war "welche Dateien haengen an Datei/Modul X"
> zu beantworten, ohne vorher ein einzelnes Symbol zu kennen; aktuell nur muehsam ueber mehrere
> `find_symbol`/`find_references`-Runden rekonstruierbar. **Direkt danach M8** (`--eval`/`--map`
> streichen, Details siehe §3) — Nutzer-Entscheidung 2026-08-11 nach Diskussion, ob das
> Audit-Prompt-Feature (Epic 31, 2026-08-03) durch die MCP-Tools ueberholt ist. **Danach M9**
> (Drift-Audit fuer Naming-Drift/DRY-Verstoesse bei autonomer agentischer Entwicklung) — dem
> Nutzer wichtig genug, um direkt nach der Bereinigung (M8) zu kommen, aber noch nicht
> spezifiziert; nur eine grobe Ideensammlung in
> [`07-drift-audit-ideen.md`](07-drift-audit-ideen.md), muss vor Umsetzung erst ausgearbeitet
> werden (Score/Aufwand fehlen bewusst). **Update 2026-08-11 (Phase 1 abgeschlossen):** M9 ist
> jetzt als Epic mit Score/Aufwand/Akzeptanzkriterien ausgearbeitet, siehe §3 M9 und Decisions
> Log D19. **M5 `test_coverage_context` danach** — sinnvoller
> Folgeschritt, aber weniger dringend, weil `find_references` bereits einen Teil des Bedarfs
> abdeckt (findet Unit-Tests, die eine Methode direkt aufrufen). M1 (eigenstaendiges
> Linting-Vorhaben) und M3 (haengt an S2.2, jetzt entsperrt) bleiben unveraendert nachrangig —
> siehe aktualisiertes Sequenzierungsdiagramm in §4. Zwei zusaetzlich diskutierte Ideen wurden
> bewusst NICHT in die Roadmap aufgenommen: Git-Historie/Blame als eigenes Tool und
> semantische/Fuzzy-Suche allgemein — Begruendung in
> [`06-nicht-umsetzen.md`](06-nicht-umsetzen.md) §9/§10 (§10 ist auch fuer die RAG/Qdrant-Frage
> zu M9 relevant, siehe `07-drift-audit-ideen.md`).

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
- [x] Tool-Definition mit structured output (JSON Schema 2020-12)
- [x] Score-Berechnung deterministisch (gleicher Code → gleicher Score) — Determinismus-Bug behoben: der Verzeichnis-Sweep im MCP-Server (`McpCodeGraphServerRefresh.SweepForNewFiles`) konnte unter Last projektfremde `.cs`-Dateien (z. B. andere Test-Fixture-Projekte im selben Solution-Verzeichnis-Baum) lautlos an das erste Projekt der Solution haengen und damit die Lint-Violations verfaelschen; Fallback entfernt, nur noch Dateien innerhalb eines tatsaechlichen Projekt-Verzeichnisses werden uebernommen. Zusaetzlich haertet `SafeguardScanner` transiente `GetCompilationAsync`-Fehlschlaege unter Last per Retry ab und meldet einen dauerhaften Compile-Fehlschlag als Malfunction statt eines stillschweigend unvollstaendigen Scores.
- [x] 10+ Unit-Tests (verschiedene Score-Klassen, Threshold-Logik) — 23 Unit-Tests in `SafeguardScannerTests`/`SafeguardToolTests`
- [x] 1 Integration-Test (Live-Repo: AiNetLinter-Repo selbst) — `LiveDogfood_Safeguard_ReturnsResults`, stabil unter wiederholten Last-Reproduktionslaeufen
- [x] Doku: Use-Cases + Beispiel-Score-Berechnung — `Docs/agent-api.md#mcp-server-modus`
- [x] Migration: ServerInstructions erwähnt safeguard als Quality-Gate

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
- [x] Mindestens 6 Patterns implementiert — genau 6 von 10: `god-class`, `async-void`,
      `long-method`, `public-without-doc`, `empty-catch`, `feature-envy`. Die anderen 4
      (`deep-nesting`, `disposable-not-disposed`, `static-state`, `magic-numbers`) sind bewusst
      zurückgestellt (analog zum `method_count`-Präzedenzfall bei `metrics_tree`, siehe S2.5
      Akzeptanzkriterien oben): keine existierende Linter-Regel/Checker, würden komplett neue
      Roslyn-Syntax-Walker mit eigenem False-Positive-Risiko erfordern (z. B. sinnvolle
      Magic-Number-Allowlist, Disposal-Tracking über Scopes) — deutlich größerer, eigener Scope.
- [x] Pattern-Konfiguration in `rules.json` (gleiche Struktur wie Bool-Rules) — **bewusst nicht
      umgesetzt**: alle 6 zugrunde liegenden Regeln (`BanAsyncVoid`, `EnforceNoSilentCatch`, ...)
      sind bereits einzeln über die bestehende `rules.json` ein-/ausschaltbar; ein zweiter,
      paralleler Ein-/Ausschalter für dieselbe Sache in `pattern_detect` wäre Config-Drift-Risiko
      (zwei Schalter für dasselbe Verhalten können auseinanderlaufen). Ist eine Regel deaktiviert,
      zeigt das zugehörige Pattern automatisch 0 Treffer.
- [x] Structured Output — `StructuredContent` mit `{ patterns: [{ id, description, occurrences,
      items: [...] }], summary: { patternsWithHits, totalOccurrences } }`, kein Ranking nach
      numerischem Schweregrad (siehe `konzept`-Notiz zu `RuleViolation.Details` als bereits
      formatiertem String).
- [x] 10+ Unit-Tests (1 pro Pattern + Edge-Cases)
- [x] 1 Integration-Test auf Live-Repo
- [x] Doku mit Pattern-Beispielen — `Docs/agent-api.md#mcp-server-modus`.

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
- [x] Tool `metrics_tree` mit **4** (nicht 5) Modi — `method_count` wurde bewusst weggelassen
      (Redundanz zu `complexity`, das Methodenzahl bereits implizit über `MethodCount` mitliefert)
- [x] ASCII-Tree-Renderer mit Aggregat-Werten pro Knoten
- [x] Top-N-Sortierung pro Mode (korrekt: code_size → LoC desc, comment_density → Ratio asc,
      violation_density → Violation-Count desc, complexity → Ø CC desc)
- [x] `root` + `depth` + `top_n` + `file_filter` Parameter funktionieren
- [x] Drill-Down-Hinweis im Output (statt Sufficiency-Hinweis — `metrics_tree` ist strukturell
      trunkiert (Top-N pro Ebene), daher `McpDrillDownHints` statt `McpSufficiencyHints`,
      analog zu anderen trunkierenden Tools wie `find_symbol`)
- [ ] 5+ Unit-Tests (1 pro Mode + Edge-Cases: leeres Verzeichnis, single File, depth=5) — **21**
      Unit-Tests ueber beide Steps (deutlich mehr als 5), aber nur 4 statt 5 Modi abgedeckt
- [x] 1 Integration-Test auf Live-Repo — 2 (`violation_density`, `complexity`) in
      `McpLiveRepositoryTests` ergaenzt
- [ ] Doku: `Docs/agent-api.md` mit Beispielen pro Mode — `Docs/agent-api.md` enthaelt eine
      Tabellenzeile mit allen 4 Modi (konsistent zum Stil der uebrigen Tool-Zeilen dort), aber
      keine dedizierten Beispiel-Bloecke pro Mode wie in diesem Dokument oben skizziert
- [x] Bestehende `--map`-Subcommands beibehalten (CLI-Kompatibilität), `metrics_tree` ist die
      MCP-Variante — unveraendert, nicht Teil dieses Tasks

**Risiko:** Niedrig (grossteil Datei-Walk + Tree-Renderer, gut testbar)
**Quelle:** User-Idee 2026-08-06, Recon C §4.3 (Aider Repo-Map), Recon A §5.1, Recon B §6.3 (bestehende `--map`-Subcommands)

---

### M2 — `dependency_graph` (Datei-/Typ- und Projekt-Abhängigkeiten)

**Warum:** Dogfooding-Session 2026-08-10/11 (siehe Session-Notizen, D13) hat als größte
verbleibende Navigationslücke identifiziert: "welche Dateien/Typen hängen an Datei/Modul X" lässt
sich aktuell nur mühsam über mehrere `find_symbol`/`find_references`-Runden rekonstruieren. Der
ursprüngliche F10-Vorschlag (Recon C §5.3) zielte auf Projekt-/NuGet-Ebene (Projekt-Referenzen +
NuGet-Vulnerabilities) — das deckt aber NICHT den beim Dogfooding tatsächlich erlebten Bedarf ab,
der auf Datei-/Typ-Ebene liegt (feinkörniger als Projekt-Referenzen).

**Scope-Entscheidung (klärt Spannung zwischen ursprünglichem Recon-Vorschlag und validiertem
Bedarf):**
- **Kern (muss):** Datei-/Typ-Ebene — "welche Dateien/Typen verwendet Datei/Typ X" (ausgehende
  Abhängigkeiten) und "wer verwendet Datei/Typ X" (eingehende Abhängigkeiten), abgeleitet aus
  tatsächlichen Typ-Referenzen via Roslyn `SemanticModel` (nicht nur `using`-Direktiven — die
  sagen nichts darüber, ob ein importierter Namespace auch wirklich benutzt wird). Das ist der
  Teil, der die Dogfooding-Lücke schließt.
- **Sinnvoll, falls günstig (kann):** Projekt-Ebene (`Solution.Projects` + `ProjectReferences`)
  als grobe Zusatz-Sicht — billig über die Roslyn-Project-API, kein NuGet-API-Call nötig.
- **Explizit NICHT im Scope:** NuGet-Vulnerability-/CVE-Scanning aus dem ursprünglichen
  F10-Vorschlag — erfordert externe Netzwerk-/API-Calls zu einer Vulnerability-Datenbank,
  widerspricht dem Anti-Ziel "kein Modell-/Cloud-Abhängigkeit" (§0). Falls später gewünscht:
  eigenes, separates Epic — nicht mit diesem vermischen.

**Vorschlag Tool-Design (an bestehende Konventionen anlehnen, siehe `find_references`/
`get_call_tree` als Vorbild):**
- Name: `dependency_graph` (wie in der Roadmap benannt)
- Input: `filePath` oder `typeIdentifier` (Datei:Zeile:Spalte oder qualifizierter Name/Pfad, wie
  bei anderen Tools), `direction` (`incoming`/`outgoing`/`both`, Default `both`), optional
  `depth` (transitiv, hard cap analog `find_references`/`get_call_tree`), `maxResults` (Default
  50 — **zwingend von Anfang an**, siehe Akzeptanzkriterien)
- Output: Text (kompakte Liste/kleiner Baum) + `StructuredContent` als **Objekt**
  (`{ nodes: [...], edges: [...] }` o. ä.) — niemals ein nacktes Array (siehe
  `McpToolResultsTests`-Regressionstest als Vorbild, betraf diese Session 3x als echter Bug)
- Eigener Unterordner `src/AiNetLinter/Mcp/Tools/DependencyGraph/` (Tool.cs + Scanner.cs-Split,
  wie alle anderen Tools)

**Abhängigkeiten:** Keine harte Abhängigkeit; kann bestehende Muster (`FindReferencesTool`,
`CallGraphTraversal`) als Vorlage für Traversierung/Truncation wiederverwenden
**Aufwand:** 1-2 Wochen (Kern Datei-/Typ-Ebene realistisch 3-5 Tage, Projekt-Ebene als Zusatz
+1-2 Tage falls Zeit)
**Akzeptanzkriterien:**
- [x] Datei-/Typ-Ebene-Abhängigkeiten funktionieren für `incoming`/`outgoing`/`both` — Knoten sind
      Dateien (Solution-relative Pfade), Kanten Datei-zu-Datei annotiert mit den ueberquerenden
      Typnamen. `typeIdentifier` scoped enger als `filePath` (nur die Deklaration des einen Typs
      statt der ganzen Datei) — direkt getestet in
      `ScanTypeAsync_Incoming_NarrowerThanFile_ExcludesOtherTypeReferences`.
- [x] `maxResults` + Trunkierungs-Meta von Anfang an (kein unbounded Output — siehe die
      `get_violations`/`get_hotspots`/`get_type_hierarchy`-Bugfixes aus der Dogfooding-Session
      2026-08-10/11 als Warnung: alle drei hatten genau diesen Fehler) — zusaetzlich ein eigener
      Scan-Kosten-Hard-Cap (`MaxVisitedFiles` = 150 besuchte Dateien waehrend der BFS), unabhaengig
      von `maxResults` (das nur die angezeigten Kanten begrenzt).
- [x] `StructuredContent` ist immer ein Objekt, nie ein nacktes Array — eigener Regressionstest
      `ExecuteAsync_StructuredContent_IsJsonObjectNotArray` analog `McpToolResultsTests`.
- [x] Sufficiency-Hinweis korrekt (nur bei echter Vollständigkeit, nicht bei Trunkierung — siehe
      `get_call_tree`-Bugfix derselben Session als Vorbild) — `Truncated` ist ein echtes Bool-Feld
      (nicht wie bei `find_references`/`get_impact` eine String-Heuristik), gesetzt bei
      `maxResults`-Kappung ODER erreichtem Traversierungs-Hard-Cap; der Sufficiency-Hinweis wird
      nur bei `Truncated == false` angehaengt.
- [x] Registrierung in einer `*ToolRegistrations.cs`-Datei (bestehendes Muster), Tool-Beschreibung
      inkl. Parameter-Doku — als sechstes Tool in `SymbolGraphToolRegistrations.cs` (nicht als
      eigene Registrations-Datei), da `dependency_graph` `FindReferencesTool.ResolveSymbolAsync`
      und dasselbe Visited-Set-Traversierungsmuster wie `CallGraphTraversal` wiederverwendet.
- [x] Projekt-Ebene (optional) nur falls ohne großen Mehraufwand über `Solution.Projects`/
      `ProjectReferences` möglich — umgesetzt: ein Eintrag (Zielprojekt + seine direkten
      Projekt-Referenzen), kein vollstaendiger Projektgraph.
- [x] Keine NuGet-Vulnerability-Abfrage (bewusst out of scope)
- [x] 15+ Unit-Tests (Scanner direkt + Tool-Ebene, Edge-Cases: Datei ohne Abhängigkeiten,
      zyklische Abhängigkeiten, Trunkierung) — 25 Unit-Tests (`DependencyGraphScannerTests`:
      14, `DependencyGraphToolTests`: 11), deutlich mehr als gefordert.
- [x] 1 Integration-/Live-Repo-Test — `LiveDogfood_DependencyGraph_ReturnsResults` in
      `McpLiveRepositoryTests`.
- [x] Doku: `Docs/agent-api.md` Tool-Tabelle + `Docs/ROADMAP.md` Epic-Eintrag — zusaetzlich
      eigenes Structured-Output-Beispiel in `agent-api.md` analog `safeguard`/`pattern_detect`,
      `ServerInstructions.cs` und `OverviewResourceRegistration.ToolSummaries` (Tool-Zaehler
      ueberall auf 17 aktualisiert) nachgezogen.
- [x] `dotnet build`/`dotnet test` (Volllauf, `Category!=Stress`) grün

**Risiko:** Mittel (Datei-/Typ-Abhängigkeits-Analyse aus `SemanticModel` ist neu, Zyklen-Erkennung
braucht sorgfältige Traversierung analog `CallGraphTraversal`)
**Quelle:** Recon C §5.3 F10 (Ausgangsidee), Dogfooding-Session 2026-08-10/11 (validierter Bedarf,
Scope präzisiert)

---

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
- [ ] Doku: `Docs/configuration.md` (kein neuer Eval-Typ mehr — das `--eval`-Feature ist mit M8 gestrichen, siehe unten)

**Risiko:** Mittel (Pipeline-Analyse ist neu, ASP.NET-Varianten)
**Quelle:** Recon A §7.2, Recon C §4.7 (Differentiator)

---

### M8 — `--eval`/`--map` ersatzlos streichen (Audit-Prompts + Codebase-Maps)

**Warum:** `--eval` (`naming-drift`/`architecture-intent`, Epic 31, gebaut 2026-08-03) assembliert einen statischen Markdown-Prompt (Spec + Vocabulary-/Structure-Map) zum Copy-Paste in eine beliebige LLM-Session. Das Feature taucht nirgends in der aktiven Drift-Loop-Scaffolding (`.agents/`) auf — kein Beleg fuer aktive Nutzung im echten Workflow. Inhaltlich loest es "gib einer LLM Audit-Kontext als einen grossen statischen Blob" — genau das Muster, das die MCP-Investition (safeguard, pattern_detect, metrics_tree, get_violations) gerade abgeloest hat: ein Agent mit Live-MCP-Zugriff kann sich dieselbe Evidenz gezielt, aktuell und praeziser selbst zusammenstellen. Eine Integration ins MCP ergibt architektonisch keinen Sinn — ein MCP-Tool bedient dieselbe Session, die schon Live-Tool-Zugriff hat, und wuerde sich keinen "Prompt fuer sich selbst" bauen. Nutzer-Entscheidung 2026-08-11 nach Diskussion (siehe Session-Notizen): ersatzlos streichen statt in MCP integrieren.

**Wichtige Abgrenzung — was NICHT betroffen ist:** `HotspotMapBuilder` und `SkeletonMapBuilder` (zwei der vier `--map`-Modi) werden von den MCP-Tools `get_hotspots` bzw. `get_file_skeleton` weiterverwendet (siehe `GetHotspotsScanner`/`GetFileSkeletonTool`) — diese Klassen bleiben als interne Implementierung bestehen, nur ihre CLI-Exposition ueber `--map hotspots`/`--map skeleton` entfaellt zusammen mit dem gesamten `--map`-Flag. `VocabularyMapBuilder`/`StructureMapBuilder` haben dagegen keinen anderen Konsumenten ausser `EvalAssembler`/`MapCommand` (verifiziert 2026-08-11) und koennen vollstaendig geloescht werden. **Vor Umsetzung nochmal bestaetigen:** dass der ersatzlose Wegfall von `--map hotspots`/`--map skeleton` als eigenstaendige CLI-Faehigkeit (nur noch ueber MCP erreichbar) tatsaechlich gewuenscht ist.

**Scope:**
1. `--eval`, `--list-evals`, `--spec` CLI-Optionen entfernen (`CliOptionFactory`, `LinterArgs`, Parse-Wiring)
2. `Evals/`-Namespace komplett loeschen: `EvalRegistry`, `EvalDefinition`, `EvalAssembler`, `SpecLoader`
3. `EvalCommand`, `ListEvalsCommand` loeschen
4. `Docs/Evals/naming-drift.md`, `Docs/Evals/architecture-intent.md` loeschen, `EmbeddedResource`-Eintrag im `.csproj` entfernen
5. `--map` CLI-Option entfernen, `MapCommand` loeschen
6. `VocabularyMapBuilder`, `StructureMapBuilder` loeschen (kein anderer Konsument)
7. Alle zugehoerigen Tests entfernen
8. Doku bereinigen: `Docs/agent-api.md`, `Docs/integration.md`, `Docs/ROADMAP.md` (Epic-31-Eintrag auf "entfernt, siehe M8" aktualisieren statt kommentarlos loeschen — Historie bleibt nachvollziehbar), README falls vorhanden

**Wichtiger Hinweis zum Fehlerverhalten (Nutzer-Anforderung 2026-08-11):** Ruft jemand die `.exe` nach der Entfernung noch mit `--eval`/`--map`/`--list-evals`/`--spec` auf, muss das ein harter Fehler sein (klare Meldung + Exit-Code ≠ 0), kein stiller No-Op. **Das ist kein Extra-Code:** System.CommandLine liefert dieses Verhalten bereits automatisch fuer jede unbekannte Option — empirisch verifiziert am 2026-08-11 (`ainetlinter --this-flag-does-not-exist` → `Befehl oder Argument '--this-flag-does-not-exist' nicht erkannt.` + Usage-Hilfetext + Exit-Code 1). Reines Loeschen der Options-Registrierung reicht; **kein** Soft-Deprecation-Pfad ("--eval wurde entfernt, nutze stattdessen X") einbauen — das wuerde unnoetigen Code fuer ein bewusst gestrichenes Feature bedeuten. Damit ist auch die allgemeinere Nutzerfrage beantwortet, ob unbekannte Parameter grundsaetzlich einen harten Fehler liefern: **ja, bereits heute der Fall**, kein separates Akzeptanzkriterium noetig.

**Abhängigkeiten:** Keine (reine Entfernung)
**Aufwand:** 2-3 Tage (Loeschen + Doku-Bereinigung + Test-Anpassung)
**Akzeptanzkriterien:**
- [x] `--eval`/`--list-evals`/`--spec` vollstaendig entfernt (Code + Tests + Doku)
- [x] `--map` vollstaendig entfernt (Code + Tests + Doku)
- [x] `VocabularyMapBuilder`/`StructureMapBuilder` geloescht
- [x] `HotspotMapBuilder`/`SkeletonMapBuilder` bleiben bestehen, MCP-Tools (`get_hotspots`, `get_file_skeleton`) unveraendert funktionsfaehig — verifiziert per `GetFileSkeletonToolTests`/`GetHotspotsToolTests`/`HotspotMapBuilderTests` (17/17 gruen)
- [x] Verifiziert: `ainetlinter --eval naming-drift` und `ainetlinter --map hotspots` liefern "nicht erkannt"-Fehlermeldung + Exit-Code 1 (kein Extra-Code, folgt automatisch aus der Entfernung) — empirisch am 2026-08-11 bestaetigt
- [x] `Docs/ROADMAP.md` Epic-30/31-Eintraege aktualisiert (nicht geloescht) mit Verweis auf die Streichung
- [x] `dotnet build`/`dotnet test` (Volllauf, `Category!=Stress`) gruen — 1399/1399

**Risiko:** Niedrig (reine Entfernung, keine neue Logik)
**Quelle:** Nutzer-Entscheidung 2026-08-11, Dogfooding-Session 2026-08-10/11

---

### M9 — Drift-Audit: DRY-Erkennung (`find_duplicates` + `DuplicateCodeChecker` + Self-Audit-Skill + Refactoring-Drift)

**Warum:** Beim autonomen agentischen Entwickeln entsteht Code-Duplikation, weil eine bereits
existierende Lösung nicht wiedergefunden/wiederverwendet wird — keine einzelne Lint-Regel fängt
das, weil es erst im Vergleich über die ganze Codebase sichtbar wird. Konkretes Beispiel aus der
Dogfooding-Session 2026-08-10/11: `JsonSerializerOptions` war vor S1.3 in 4 MCP-Tools einzeln
instanziiert, bevor es zu `McpJsonOptions.Default` zentralisiert wurde. Ausgearbeitet in
[`07-drift-audit-ideen.md`](07-drift-audit-ideen.md) auf Basis etablierter Clone-Detection-Forschung
(Bellon et al. 2007, Roy & Cordy 2007, CCFinder/jscpd/PMD-CPD als Referenz-Implementierungen).

**Scope-Entscheidung (Phase-1-Spec-Runde, 2026-08-11, siehe Decisions Log D19):** Nur der
DRY-Block wird jetzt umgesetzt — Ideen **A** (Token-CPD), **F** (Self-Audit-Skill) und **C**
(Refactoring-Drift via Symbolgraph), in dieser Reihenfolge/parallel wie unten beschrieben.
**Explizit NICHT im Scope:** Idee **E** (Naming-Familien-Erkennung/Naming-Drift, bleibt Skizze
für ein separates Folge-Epic), Idee **B** (AST-CPD, erst bei empirisch belegtem Bedarf nach A),
Idee **D** (Pattern-Cluster-Detection, erst bei 2+ konkreten unbelegten Beispielen).

#### Teil A — Token-CPD (Schicht 1): `find_duplicates` + `DuplicateCodeChecker`

**Methode:** Method-Granularität, Roslyn-Token-Extraktion (`body.DescendantTokens()`), N-Gram-
Shingling (`k=5`, konfigurierbar), SHA-256-Hash pro N-Gram, Inverted Index
(`Dictionary<Hash, List<MethodId>>`), Kandidaten-Pairs ab `minSharedNgrams=3` gemeinsamen
N-Grammen, finaler Score = Jaccard-Similarity über die N-Gram-Mengen. Details/Forschungs-Anker in
`07-drift-audit-ideen.md` §A.2.

**Schwellwert-Staffelung (bestätigt 2026-08-11):** drei Buckets statt hartem Cut —
`exact ≥ 0.95`, `near 0.80–0.95`, `fuzzy 0.65–0.80`; unter `0.65` kein Output. Werden nach dem
ersten Dogfooding-Lauf ggf. empirisch nachjustiert (kein Commitment auf ewig).

**False-Positive-Disziplin:** Method-Granularität (kein Block/File), Min-Token-Filter `30`
(bestätigt 2026-08-11), `bin/`/`obj/`/`.ainetlinter/`/`tests/Fixtures/` ausgeschlossen (Lehre aus
Safeguard-Fix-Review 2026-08-06: Fixture-Verzeichnisse mit absichtlichen Verstößen nie in
Drift-Audits einbeziehen), `[GeneratedCode]`-Methoden übersprungen. Identifier-Normalisierung
(schaltet Type-2-Klon-Erkennung an) **Default aus**, Opt-in per Tool-Argument (bestätigt
2026-08-11).

**Konfiguration in `rules.json` — Korrektur gegenüber dem Entwurf in `07-drift-audit-ideen.md`
§A.5:** Dieses Repo nutzt ein flaches `Global`-Objekt mit skalaren Keys (siehe `BanAsyncVoid`,
`MaxCyclomaticComplexity` in `rules.json`), kein verschachteltes `{id, enabled, params}`-Schema.
Neue Keys folgen der bestehenden Konvention:
```json
{
  "Global": {
    "EnableDuplicateCodeCheck": true,
    "DuplicateCodeMinTokens": 30,
    "DuplicateCodeNgramSize": 5,
    "DuplicateCodeMinSharedNgrams": 3,
    "DuplicateCodeExactThreshold": 0.95,
    "DuplicateCodeNearThreshold": 0.80,
    "DuplicateCodeFuzzyThreshold": 0.65,
    "DuplicateCodeNormalizeIdentifiers": false,
    "DuplicateCodeMaxResults": 20
  }
}
```

**`DuplicateCodeChecker` — Integrations-Punkt (Architektur-Klärung 2026-08-11):** Anders als die
meisten Checker in `Core/Checkers/` (die als Node-Walker pro Datei laufen, z. B.
`AsyncVoidChecker.CheckMethod`) braucht Duplicate-Detection eine solution-weite Sicht über alle
Methoden hinweg. Passender Integrationspunkt ist **`PostAnalysisChecks.Run`** (analog
`UiFileSeparationChecker.Run(state, config)`), das nach der Per-Dokument-Analyse auf dem
aggregierten `AnalysisState` läuft — dort ist der N-Gram-Index über die ganze Solution bereits
sinnvoll aufbaubar, ohne die bestehende Parallel-Walk-Architektur der Haupt-Checker zu verändern.
Meldet Violations über die normale `RuleViolation`-Infrastruktur, geht in den `safeguard`-Score
ein.

**MCP-Tool `find_duplicates`** (Ordner `src/AiNetLinter/Mcp/Tools/DuplicateDetection/`, Split
`DuplicateDetectionTool.cs` + `DuplicateDetectionScanner.cs`, wie bei `dependency_graph`/
`pattern_detect`):
- Input: `minTokens` (Default aus `rules.json`), `similarityThreshold` (Default `fuzzy`),
  `normalizeIdentifiers` (Default `false`), `scopeDir` (optional, Default Solution-Root),
  `maxResults` (Default 20, **zwingend von Anfang an**, wie bei `dependency_graph`/M2)
- Output: Markdown-Cluster-Liste (Methoden-Signatur + Datei:Zeile + Jaccard-Score, Cluster
  gruppiert statt isolierter Paare — "Methode A, B, C sind 0.91 ähnlich") +
  `StructuredContent` als **Objekt** (`{ clusters: [...], summary: {...} }`), niemals ein
  nacktes Array (Regressionstest analog `McpToolResultsTests`/M2)
- Trunkierungs-Meta-Zeile statt Sufficiency-Hinweis, wenn `maxResults` greift (analog
  `dependency_graph`s `Truncated`-Bool-Feld) — Sufficiency-Hinweis nur bei echter Vollständigkeit
- Registrierung in einer eigenen `*ToolRegistrations.cs` bzw. Erweiterung der passendsten
  bestehenden Registrations-Datei (finale Entscheidung beim Implementieren, analog der
  M2-Erfahrung mit `SymbolGraphToolRegistrations.cs`)

**Funktionsgarantie:** Erkennt garantiert alle Type-1-Klone (identische Token-Streams) oberhalb
`minTokens`; Type-2 nur mit `normalizeIdentifiers=true`; Type-3 abhängig vom Score.
False-Positive-Budget < 10 % der gemeldeten Cluster, False-Negative-Budget < 20 % verpasste Klone
≥ `minTokens` (Ziel-Messwerte, siehe `07-drift-audit-ideen.md` §A.7).

#### Teil F — Self-Audit-Skill als Hülle

Skill-Datei `.agents/skills/drift-audit/SKILL.md`, 4 Schritte: (1) `find_duplicates` über
`src/`, (2) pro `exact`-Cluster sofort konsolidieren oder Tech-Debt-Eintrag, (3) pro `near`-Cluster
manueller Blick auf 1-2 Beispiele, (4) bei auffälligem Helper optional `find_references` +
Idee-C-Logik nach Aufrufer-Lücken suchen. **Cadence (bestätigt 2026-08-11):** pro Step optional,
pro Epic-Abschluss verpflichtend — referenziert im Drift-Loop-Skill/Kritiker-Workflow, damit die
Lehre aus dem Safeguard-Fix-Review 2026-08-06 ("Skills, die 'irgendwann laufen sollen', laufen oft
nicht") nicht wiederholt wird.

#### Teil C — Refactoring-Drift-Detection (Schicht 2, baut auf Teil A auf)

Erkennt, ob ein existierender Helper (`McpJsonOptions.Default`) von einzelnen Aufrufern inline
dupliziert statt aufgerufen wird ("absence-of-calls"-Heuristik, Murphy-Hill 2005). Nutzt den
bereits vorhandenen Roslyn-Symbolgraphen (`find_references`/`FindReferencesTool.ResolveSymbolAsync`,
wiederverwendbar wie bei M2) kombiniert mit dem N-Gram-Index aus Teil A: Aufrufer-Liste von `H`
via `find_references`, Candidate-Set = Methoden mit Jaccard-Score ≥ `near` zu `H.Body`, die `H`
nicht aufrufen. **Design-Entscheidung (zur Bestätigung mit dem Epic):** als zusätzlicher `mode`-
Parameter auf `find_duplicates` (`mode="clone"` Default, `mode="refactoring-drift"` mit
zusätzlichem `helperSymbol`-Argument) statt eines separaten Tools — spart eine zweite
Tool-Registrierung für dieselbe zugrunde liegende Engine, analog zum `mode`-Parameter-Muster bei
`metrics_tree`/`dependency_graph`. Ausgabe explizit als "Kandidaten", nicht als "Verstöße" (höheres
False-Positive-Budget < 25 %, da strukturelle Ähnlichkeit nicht zwingend Drift bedeutet — z. B.
50 strukturell ähnliche `Dispose`-Implementierungen).

**Abhängigkeiten:** Teil F und Teil C setzen Teil A voraus (Skill braucht das Tool, C nutzt den
N-Gram-Index). Keine externe Abhängigkeit (kein Embedding-Modell, kein neuer Persist-Layer, siehe
Ablehnung von RAG/Qdrant in `07-drift-audit-ideen.md`).
**Aufwand:** 8-11 Tage gesamt (Teil A: 5-7 Tage inkl. Tests/Doku, Teil F: 1 Tag, Teil C: 2-3 Tage)
**Akzeptanzkriterien:**
- [ ] Teil A: N-Gram-Index + Jaccard-Score korrekt (Type-1-Klon zu 100 % erkannt oberhalb
      `minTokens`, verifiziert an Ground-Truth-Fixture mit 5 künstlichen Klonen + 5
      künstlichen Nicht-Klonen)
- [ ] Teil A: Schwellwert-Staffelung (`exact`/`near`/`fuzzy`) korrekt im Tool-Output UND im
      Checker
- [ ] Teil A: `DuplicateCodeChecker` in `PostAnalysisChecks.Run` integriert, Violations über
      normale `RuleViolation`-Infrastruktur, fließt in `safeguard`-Score ein
- [ ] Teil A: `find_duplicates` mit `maxResults`-Trunkierung von Anfang an, `StructuredContent`
      immer Objekt (Regressionstest), Cluster-Gruppierung statt isolierter Paare
- [ ] Teil A: `rules.json`-Keys (flach, `Global`-Objekt) dokumentiert in `Docs/configuration.md`
- [ ] Teil A: historischer Regressionstest — Tool auf den Code-Stand vor S1.3 angewendet muss die
      4 `JsonSerializerOptions`-Instanziierungen als Cluster finden (False-Negative-Ground-Truth,
      siehe `07-drift-audit-ideen.md` §A.7)
- [ ] Teil F: `.agents/skills/drift-audit/SKILL.md` existiert, referenziert `find_duplicates`,
      im Drift-Loop-Kritiker-Workflow als "pro Epic verpflichtend" verankert
- [ ] Teil C: `mode="refactoring-drift"` findet den `McpJsonOptions.Default`-Fall, wenn rückwirkend
      auf den Code-Stand vor S1.3 angewendet (zweiter historischer Regressionstest)
- [ ] 30+ Unit-Tests (Scanner, Checker, Tool je Teil, Edge-Cases: keine Duplikate, identische
      Methoden, Trunkierung, Refactoring-Drift-Kandidat gefunden/nicht gefunden)
- [ ] 2 Integration-/Live-Repo-Tests (`find_duplicates` auf dem eigenen Repo; die beiden
      historischen Regressionstests oben zählen zusätzlich)
- [ ] Doku: `Docs/configuration.md` (neue `DuplicateCode*`-Keys), `Docs/agent-api.md`
      (`find_duplicates` in der Tool-Tabelle inkl. `mode`-Parameter), `Docs/ROADMAP.md`
      (Epic-Eintrag), `tasks/features/05-roadmap.md` (M9-Status `[x]`)
- [ ] `dotnet build`/`dotnet test` (Volllauf, `Category!=Stress`) grün

**Risiko:** Mittel (neue solution-weite Analyse-Logik, False-Positive-Risiko bei Teil C höher als
bei Teil A — mitigiert durch "Kandidaten statt Verstöße"-Framing; keine neue Infrastruktur nötig,
alles auf bestehendem Roslyn-Symbolgraphen/Checker-Framework aufgebaut)
**Quelle:** Nutzer-Anliegen 2026-08-11, [`07-drift-audit-ideen.md`](07-drift-audit-ideen.md)
(Bellon et al. 2007, Roy & Cordy 2007, CCFinder/jscpd/PMD-CPD, Murphy-Hill 2005), Dogfooding-Session
2026-08-10/11 (`JsonSerializerOptions`-Beispiel)

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

Mid-Term (4-6 Wo) — Reihenfolge aktualisiert nach Dogfooding-Session 2026-08-10/11 (siehe §2)
├── M2 dependency_graph         ← unabhängig, HÖCHSTE PRIORITÄT (größte Navigationslücke laut Dogfooding)
├── M8 --eval/--map streichen   ← unabhängig, direkt danach (Nutzer-Entscheidung 2026-08-11)
├── M9 Drift-Audit (DRY)        ← Teil A (find_duplicates+Checker), dann F (Skill) + C (Refactoring-Drift)
├── M5 test_coverage_context    ← unabhängig, danach
├── M1 ASP.NET-Analyzer-Suite   ← eigenständiges Linting-Vorhaben, siehe Hinweis oben, nachrangig
└── M3 feature_context           ← hängt an S2.2 (jetzt entsperrt), nachrangig
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
| **D13** | M-Phase-Reihenfolge nach Q/S1/S2-Abschluss? | **M2 `dependency_graph` vor M8 `--eval`/`--map`-Streichung vor M5 `test_coverage_context` vor M1/M3** | Dogfooding-Session 2026-08-10/11 (siehe Session-Notizen): systematischer Live-Test aller Tools gegen das eigene Repo aus Sicht "großer Task, Code-Stellen selbst finden" ergab die Datei-/Modul-Abhängigkeitsfrage als größte verbleibende Navigationslücke — aktuell nur über mehrere `find_symbol`/`find_references`-Runden mühsam rekonstruierbar. M8 direkt danach eingeschoben (Nutzer-Entscheidung 2026-08-11, kleiner Aufwand, reine Bereinigung). Test-Coverage-Bedarf ist teilweise schon durch `find_references` gedeckt (findet direkte Unit-Test-Aufrufer), daher nachrangig. |
| **D14** | Git-Historie/Blame als eigenes MCP-Tool bauen? | **Nein** | Gleiches Argument wie bei `search_pattern`/grep: der Host-Agent hat bereits nativen Zugriff auf `git log`/`git blame`/`git show` per Bash-Tool — ein reiner Wrapper liefert keinen Mehrwert. Volle Begründung inkl. Revival-Bedingung in [`06-nicht-umsetzen.md`](06-nicht-umsetzen.md) §9. |
| **D15** | Semantische/Fuzzy-Codesuche (Embeddings) bauen? | **Nein** | Widerspricht der strategischen Positionierung (§0: deterministisch, Roslyn-präzise, kein Modell-/Cloud-Abhängigkeit). Volle Begründung in [`06-nicht-umsetzen.md`](06-nicht-umsetzen.md) §10. |
| **D16** | `--eval`/`--map` (Audit-Prompts + Codebase-Maps) behalten, streichen oder in MCP integrieren? | **Ersatzlos streichen (M8)** | Nutzer-Entscheidung 2026-08-11 nach Diskussion: kein Beleg für aktive Nutzung (nicht in `.agents/`-Drift-Loop-Scaffolding referenziert), inhaltlich vom MCP-Ansatz überholt (statischer Evidenz-Blob vs. gezielte Live-Tool-Calls), Integration in MCP architektonisch sinnlos (MCP-Tool bedient dieselbe Session, die schon Live-Zugriff hat). `HotspotMapBuilder`/`SkeletonMapBuilder` bleiben intern bestehen (Wiederverwendung durch `get_hotspots`/`get_file_skeleton`), nur die CLI-Fläche entfällt. Details in §3 M8. |
| **D17** | Müssen unbekannte CLI-Parameter einen harten Fehler liefern (allgemeine Anforderung, nicht nur für `--eval`/`--map`)? | **Bereits der Fall, kein Handlungsbedarf** | Empirisch verifiziert 2026-08-11: `ainetlinter --this-flag-does-not-exist` liefert automatisch `Befehl oder Argument '...' nicht erkannt.` + Usage-Hilfetext + Exit-Code 1, via System.CommandLine-Standardverhalten. Gilt automatisch auch für `--eval`/`--map` nach deren Entfernung in M8 — kein Soft-Deprecation-Sonderfall nötig. |
| **D18** | Drift-Audit (Naming-Drift + DRY) als neues Epic aufnehmen — mit welchem Detailgrad? | **Nur als offene Ideensammlung (M9), noch nicht spezifiziert** | Nutzer-Anliegen 2026-08-11: typische Drift-Probleme bei autonomer agentischer Entwicklung (Naming-Drift-Beispiel "A" → "A23456", DRY-Beispiel JsonSerializerOptions-Duplikation aus dieser Session). Direkt nach M8 priorisiert, aber absichtlich ohne Score/Aufwand/Akzeptanzkriterien in die Roadmap aufgenommen — muss erst ausgearbeitet werden. Ideen (Duplicate-Detection via AST-Vergleich, Naming-Familien via String-Ähnlichkeit, Self-Audit-Skill) in [`07-drift-audit-ideen.md`](07-drift-audit-ideen.md). Im selben Gespräch RAG/Vektor-Suche (Qdrant) diskutiert und für die beiden konkreten Beispiele als falsches Werkzeug eingeschätzt (lexikalisches bzw. strukturelles Problem, kein semantisches) — Details ebenfalls in `07-drift-audit-ideen.md`, keine endgültige Entscheidung, nur vorläufige Einschätzung. **Abgelöst durch D19** (M9 jetzt spezifiziert). |
| **D19** | Phase-1-Spec-Runde für M9 (2026-08-11): DRY-Scope final festlegen (Ideen A/F/C aus D18), Threshold-/Default-Werte, Tool+Checker-Umfang | **DRY-Block (A, F, C) jetzt spezifiziert und in Roadmap aufgenommen; Idee E (Naming-Drift) bleibt bewusst Skizze für ein separates Folge-Epic, B/D zurückgestellt bis Empirie-Bedarf.** Konkrete Defaults, alle mit Nutzer bestätigt: Jaccard-Schwellwerte `0.95/0.80/0.65` (CCFinder-/jscpd-üblich, unverändert übernommen), Min-Token-Filter `30` (konservativ für die kleinere Codebase), Identifier-Normalisierung **Default aus** (Opt-in pro Aufruf, verhindert semantisch falsche Klon-Meldungen), sowohl das MCP-Tool `find_duplicates` **als auch** der Linter-Checker `DuplicateCodeChecker` werden in derselben Runde gebaut (teilen sich die Scan-Engine, kein Mehraufwand durch Trennung), Self-Audit-Skill (Idee F) läuft **parallel zu Idee A** statt danach (sonst Risiko dass Tool ungenutzt bleibt), Cadence im Drift-Loop: **pro Step optional, pro Epic verpflichtend**. Architektur-Korrektur gegenüber dem Ideensammlungs-Entwurf: `rules.json`-Konfiguration folgt dem bestehenden flachen `Global`-Schema (nicht das dort skizzierte verschachtelte `{id,enabled,params}`-Objekt), `DuplicateCodeChecker` hängt sich in `PostAnalysisChecks.Run` (solution-weite Prüfung, analog `UiFileSeparationChecker`) statt in die Pro-Datei-Node-Walker der übrigen Checker. Volles Epic in §3 M9. |
