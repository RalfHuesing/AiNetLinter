---
task: features-overview
type: synthese
status: draft
created: 2026-08-06
purpose: Master-Übersicht aller Erkenntnisse aus dem 360-Grad-Überblick
---

# Master-Overview: AiNetLinter MCP-Server-Aufwertung

> **Auftrag des Users (06.08.2026):** „360-Grad-Überblick über CodeGraph und unseren AiNetLinter-MCP-Stand. Features übernehmen, die wir technisch umsetzen können, ggf. MCP-Server anpassen, SubAgenten einsetzen, in `tasks/features/` notieren — Grundlage für weitere Arbeiten. Wir programmieren voll agentisch mit Planer/Coder/Kritiker/Orchestrator — der MCP-Server soll das beschleunigen, verbessern, effizienter machen (Kosten)."

**Diese Datei ist der Einstiegspunkt.** Vier Recon-Berichte wurden parallel erstellt; dieser Master bündelt ihre Erkenntnisse zu einer konsolidierten Sicht und verweist für Details auf die jeweiligen Berichte.

| # | Datei | Inhalt | Bytes |
|:--:|-------|--------|------:|
| 1 | `01-codegraph-recon.md` | CodeGraph-Architektur, MCP-Tools, Killer-Patterns, Übertragbarkeitsanalyse | 61 KB |
| 2 | `02-ainetlinter-mcp-current.md` | AiNetLinter MCP-Stand: 10 Tools, 1 Resource, ~10K LOC, Stärken/Schwächen, Quick-Wins | 57 KB |
| 3 | `03-market-research.md` | Roslyn-MCP-Markt, MCP-Spec 2026, Token-Optimierung, Konkurrenz, Top-Features | 65 KB |
| 4 | `04-explore-vs-flow-tools.md` | Detaillvergleich `codegraph_explore` ↔ AiNetLinter, `trace_flow`-Vorschlag | 41 KB |
| 5 | `05-recommendations-roadmap.md` | Konsolidierte Empfehlungen, priorisierte Roadmap, Epics | (separat) |

---

## 1. TL;DR — die 5 wichtigsten Erkenntnisse

### 🥇 AiNetLinter hat heute schon eine **solide Grundlage** — die Aufwertung ist *additiv*, kein Ersatz

10 MCP-Tools, 1 Resource, MSBuildWorkspace-resident, strukturelle Stdout-Absicherung, inkrementelles Refresh (mtime+SHA-256), JSON-RPC-Framing-Tests, Call-Log (opt-in), 30+ Test-Dateien, deterministische Symbol-Resolution mit `DocumentationCommentId`, Eval-Framework, Map-Generatoren, Playbook, Baseline/Ratchet. **Wir bauen nicht bei Null — wir werten auf.**

### 🥈 Der CodeGraph-Killer ist kategorisch anders — und Roslyn hat einen Vorteil, den CodeGraph nie haben wird

`codegraph_explore` ist kein „besseres `find_references`" — es ist **Multi-Symbol-Flow-Tracing mit Source-Bodies + Blast-Radius in EINEM Call**. Dafür muss der Agent heute 9 sequenzielle Calls machen. CodeGraph misst intern **88% weniger Tool-Calls** durch dieses Pattern. **Roslyn hat aber einen strukturellen Vorteil:** CodeGraphs ASP.NET-Resolver ist ein 220-Zeilen-Regex-Hack; Roslyn kann Controller-Routes, Middleware-Pipeline, DI-Container, gRPC, Minimal-APIs **strukturell** parsen. Wir können CodeGraphs Pattern in C#-Domäne *besser* umsetzen als CodeGraph selbst es für C# je könnte.

### 🥉 Die zwei kritischsten Design-Prinzipien aus CodeGraph, die AiNetLinter ADOPTIEREN muss

1. **„`isError: true` ist Gift."** CodeGraph-Maintainer (empirisch validiert): „One or two `isError: true` early in a session and the agent stops calling the tool entirely." Recoverable conditions (Symbol nicht gefunden, File nicht im Index, uncommitted vs. committed) kommen als **success-shaped Text-Result mit Anleitung**. AiNetLinter macht das **schon** in vielen Fällen (`SOLUTION_NOT_LOADED`, `Loading`-Hinweis) — aber wir brauchen eine **explizite Policy** und Audit aller Tools.
2. **„Sufficiency Doctrine" — sage dem Agent explizit, dass er aufhören soll.** CodeGraph-ServerInstructions sagen wortwörtlich: *„treat the source it returns as already Read. Trust codegraph's results — don't re-verify with grep."* AiNetLinter hat das **nicht** — kein Tool sagt dem Agent „das ist komplett, du musst nicht nochmal lesen."

### 🏅 Die größte ungenutzte Chance: Quality-Contract-Pattern

CodeScene (kommerziell) misst: ohne strukturierte Lint-Daten repariert ein Frontier-LLM nur **~20%** der Code-Health-Issues; mit MCP-augmentierten Quality-Daten **90–100%**. **AiNetLinter hat bereits `rules.json` als Single-Source-of-Truth.** Wir können ein `safeguard`-Tool bauen, das als **Selbst-korrigierender Loop** fungiert: Agent macht Edit → Server sagt „Score 6.4, Threshold 8.0, hier sind 7 Violations" → Agent repariert → wiederholen. **Kein anderer Roslyn-MCP-Server hat das.** Differentiator pur.

### 💰 Token-/Kosten-Optimierung ist DAS Thema 2026

Anthropic-Trend-Report 2026: Engineers verbringen 34% mit Code-Schreiben (war 58% in 2024), 22% mit Review, 19% mit Task-Definition. „Verification is the new bottleneck." Pro MCP-Tool: 500–1500 Tokens Schema-Overhead bei jedem Connect. Traditionell 33 Tools = **8.000 Tokens nur für Schema-Discovery**. CodeGraph erreicht durch Sufficiency-Doctrine + adaptive Budgets **35% Cost · 57% Tokens · 46% Time · 71% Tool Calls** Save. Aider-Repo-Map (PageRank-Ranking, 1024 Tokens Budget für 100K-LOC-Monorepos) ist das Vorbild für „nicht das ganze Repo, sondern die 50 relevantesten Symbole".

### 🌳 Die Tree-/Heatmap-Idee (User 2026-08-06) — interaktive Codebase-Exploration statt Mega-Context-Dump

Statt grep/rg mit flachem Dump oder `find_symbol` mit unzusammenhängenden Treffern: ein `metrics_tree`-Tool, bei dem das LLM **eine Baumebene pro Call** bekommt und gezielt tiefer bohrt. Modi: `code_size`, `comment_density` (für Audit „sinnlose Kommentare"), `violation_density`, `method_count`, `complexity`. Spart Context, ermöglicht Audit-Workflows wie „suche alle Files mit >40% Comment/Code-Ratio" ohne 1000+ Einträge in den Context zu laden. Basiert auf unseren bestehenden `--map`-Subcommands, ergänzt um rekursive Navigation. **2-3 Tage Aufwand, hoher Audit-Wert, dringend empfohlen für Sprint 2.**

---

## 2. Landschaft: Wo stehen wir, wo ist CodeGraph, wo ist der Markt?

### 2.1 AiNetLinter heute (Recon B, ~10K LOC in `Mcp/`)

**10 MCP-Tools + 1 Resource auf stdio:**

| Tool | Zweck |
|------|-------|
| `find_symbol` | Substring-Symbolsuche (C#-only) |
| `find_references` | Direkte + transitive Aufrufstellen (depth≤3) |
| `get_impact` | Git-Diff- oder Symbol-basierte Impact-Analyse |
| `get_type_hierarchy` | Vererbungs-/Interface-Hierarchie + DI-Heuristik |
| `get_file_skeleton` | Datei-Skelett (Typen + Signaturen) |
| `get_index_scope` | Welche Dateitypen sind geladen |
| `get_hotspots` | Dateien nahe/über `MaxLineCount` |
| `get_violations` | Aktuelle Lint-Verstöße |
| `get_symbol_body` | Body eines Symbols |
| `search_pattern` | Text/Regex in allen Dateien |
| `ainetlinter://overview` | Resource: Server-Status + Tool-Übersicht |

Plus: strukturelle Schutzmaßnahmen (Stdout-Schutz, JSON-RPC-Framing-Tests, inkrementelles Refresh via mtime+SHA-256, opt-in Call-Log).

### 2.2 CodeGraph (Recon A)

**~3.000 Zeilen TS + Rust-Kernel**, npm-Paket `@colbymchenry/codegraph`, MCP-Server + CLI + Library. 8 Installer-Targets (Claude/Cursor/Codex/opencode/Hermes/Gemini/Antigravity/Kiro). SQLite FTS5 (Node-eigenes `node:sqlite`). Rust-Kernel mit tree-sitter für 20 Sprachen + Standalone-Extractoren für Razor/Vue/Svelte. **MCP-Server: 8 Tools, davon 1 DEFAULT-exposé (`codegraph_explore`).**

Kerndogma: **„adapt the tool to the agent, don't try to change the agent"**. Wording in Tool-Description + ServerInstructions hat **low-salience** auf Agent-Verhalten — was zählt ist **Coverage** (alle Flows im Graph) + **Sufficiency** (Output ist so komplett, dass Agent aufhört zu lesen). Gemessen: 88% weniger Tool-Calls, 53% schneller, 0 File-Reads in Benchmark-Repos.

### 2.3 Roslyn-MCP-Markt 2026 (Recon C)

**10+ Roslyn-MCP-Server, keiner offiziell von Microsoft.** Microsoft liefert das MCP C# SDK v2.0 + `dotnet new mcpserver`-Template, integriert MCP in VS 2026 (2026-06-23), aber kein dedizierter C# Dev Kit MCP-Server. **Drei Kategorien:**

- **Schmal & fokussiert** (≤5 Tools): `egorpavlikhin/roslyn-mcp`, `carquiza/RoslynMCP`
- **Breit & flach** (10-67 Tools): `pzalutski-pixel/sharplens-mcp` (67!), `JoshuaRamirez/RoslynMcpServer` (41), `vs-ide-mcp` (18+)
- **VS-integriert**: `sailro/RoslynMcpExtension` (nutzt `VisualStudioWorkspace`, beste Latenz, VS-only)

**Gemeinsame Schwächen:** kein Token-Budget-Design, kein Pattern-Detection auf Solution-Ebene, kein `elicitation`, kein Multi-Solution-Support. **AiNetLinter hat strukturelle Vorteile:** Linter-Hintergrund, Skeletons/Playbooks, konfigurierbare `rules.json`. Differenzierung über **Quality-Contract-Pattern** (siehe Erkenntnis #4).

### 2.4 MCP-Spec-Trends 2026 (Recon C, Phase 2+6)

- **2026-07-28-Spec (RC):** Stateless Core, MRTR (`InputRequiredResult`), structured tool output (full JSON Schema 2020-12), `Mcp-Method`/`Mcp-Method`-Header, OAuth 2.1 mandatory, MCP Apps (interaktive UI-Komponenten, VS Code 2026-01+)
- **Transport:** stdio für lokal, **Streamable HTTP** für Cloud/Enterprise (HTTP+SSE seit 2025-03 deprecated)
- **Tool-Design-Doctrines:** Outcome-oriented, primitive args, concise descriptions, 5-15 ideal, max 50 Tools/Server, ReadOnly/Destructive-Hints mandatory
- **Anthropic 2026 Agentic-Coding-Report:** Multi-Agent-Wins (3.4× Task-Completion auf >500 LOC), 27% AI-Arbeit war vorher nicht existent, „Verification is the new bottleneck" — exakt unser Differentiator

---

## 3. Was CodeGraph kann, was AiNetLinter (noch) nicht kann — priorisiert

### 3.1 MUST-HAVE (hoher Mehrwert, mit Roslyn machbar)

| # | Feature | CodeGraph-Pattern | Roslyn-Realisierung | Aufwand |
|:--:|---------|-------------------|---------------------|--------:|
| **M1** | `trace_flow` (Multi-Symbol-Flow-Tracer) | `codegraph_explore` | `SymbolFinder.FindReferencesAsync` + neuer Forward-BFS + Body-Assembly | **1-2 Wo** |
| **M2** | Sufficiency-Hinweise in Tool-Output | „do NOT Read, already Read"-Boilerplate | Trivial: Textbaustein in jeden relevanten Output | **1 Tag** |
| **M3** | `safeguard` (Quality-Contract) | – (nur CodeScene hat sowas, Cloud-only) | `LinterEngine` + Schwellwert-Logik | **3-5 Tage** |
| **M4** | `skeleton` Resource (Repo-Map-Pattern) | `codegraph_files` + Aider `repo-map` | PageRank über `Project.MetadataReferences` + Symbol-API | **1 Wo** |
| **M5** | `node` (File-Read + Trail) | `codegraph_node` Dual-Mode | `SyntaxTree.GetText().GetSubText(span)` + Min-Variante `find_references` depth=1 | **2-3 Tage** |
| **M6** | `dependency_graph` (NuGet + Projects) | – | `Project.MetadataReferences` + NuGet-API | **3-5 Tage** |
| **M7** | `isError: true`-Audit + Policy | CodeGraph Doctrine | Alle Tools reviewen, Policy-Doc, Tests | **2-3 Tage** |
| **M8** | Multi-Agent-Installer (Claude, Cursor, Codex, …) | `installer/targets/` Architektur | PowerShell-Skript + Marker-basierte Idempotenz | **1 Wo** |

### 3.2 SHOULD-HAVE (klarer Mehrwert, mittlerer Aufwand)

| # | Feature | Aufwand |
|:--:|---------|--------:|
| **S0** | **`metrics_tree` (interaktive Heatmap-Tree, User-Idee 06.08)** — Modi `code_size`/`comment_density`/`violation_density`/`method_count`/`complexity`; LLM bohrt sich Ebene für Ebene durch | **2-3 Tage** |
| S1 | `pattern_detect` (God-Classes, async-void, Public-API-ohne-Doc) | 1 Wo |
| S2 | `metrics_lookup` (CC, CogC, LOC, Param-Count in 1 Call) | 3-5 Tage |
| S3 | `preview_refactor` (Roslyn-CodeAction + Unified-Diff + Rollback-ID) | 1-2 Wo |
| S4 | `test_coverage_context` (Coverage-Awareness via Test-Discovery) | 1 Wo |
| S5 | `reload_config`-Tool (Hot-Reload `rules.json`) | 1h |
| S6 | `get_server_health`-Tool (LoadState, Call-Log-Aggregat) | 1h |
| S7 | `list_projects`-Tool (Projekte, FileCounts, Dependencies) | 1-2h |
| S8 | `get_call_tree` (echter Baum statt aggregierte Top-N) | 1 Tag |
| S9 | Progressive-Disclosure-Meta-Tool (Tool-Curation für 67+ Tools) | 1-2 Wo |
| S10 | Streamable-HTTP-Transport für Cloud/CI | 1 Wo |

### 3.3 NICE-TO-HAVE (Zukunftsmusik)

- MCP-Apps-Integration (interaktive Diff-Vorschau)
- Multi-Repo-Cross-Solution-Index
- `elicitation`-Workflows (User-Confirmation vor Risk-Refactorings)
- Source-Generator-Introspection
- OAuth 2.1 + Entra ID für Streamable-HTTP-Mode

### 3.4 NICHT-ÜBERNEHMEN (mit Begründung)

- Rust-Kernel / tree-sitter-WASM — Roslyn löst C# strukturell besser
- Multi-Sprachen-Support (33 Sprachen) — AiNetLinter ist C#-pur, das ist eine Stärke
- Telemetry / Beta-Signup / Marketing — OSS-Tool, Datenschutz
- Detached-Daemon mit Lock-File-Arbitration — Overkill für Linter
- Heuristic-Edge-Synthesizer für C# (CodeGraphs tree-sitter-Limitation) — Roslyn hat die nicht

---

## 4. Erkenntnisse quer — Patterns die AiNetLinter *besser* umsetzen kann als CodeGraph

### 4.1 ASP.NET-Framework-Analyse (größte Chance)

CodeGraphs ASP.NET-Resolver ist **220 Zeilen Regex**. Mit Roslyn:

| CodeGraph macht | Roslyn kann |
|------------------|-------------|
| Regex über `[HttpGet("path")]` | `AttributeSyntax` + `AttributeData` (semantisch) |
| Konvention `/Controllers/` | `IServiceCollection.AddXxx<TService>()` + `AddScoped<TInterface, TImpl>()` |
| `[Route("api/[controller]")]` als Prefix | `AttributeData.ConstructorArguments` |
| Minimal-API `MapGet("/path", handler)` | `IInvocationOperation` + `IMethodSymbol` (Handler-Type) |
| **NICHTS** für Middleware-Pipeline | `app.Use*()`-Chain aus `Program.cs` extrahierbar |
| **NICHTS** für SignalR-Hubs | `[Hub]`-Klasse + `On<THub>`-Methoden via `INamedTypeSymbol` |
| **NICHTS** für gRPC | `[GrpcService]`-Attribut + Service-Contract-Interface |
| **NICHTS** für Authorization-Policies | `services.AddAuthorization(o => o.AddPolicy(...))` + `RequireClaim(...)` |
| **NICHTS** für Filters | `[TypeFilter]`, `[ServiceFilter]`, `IFilterMetadata` |
| **NICHTS** für Model-Binding | `[FromBody]`, `[FromQuery]`, `[FromRoute]`-Attributes |

**Konkrete Linter-Rules die daraus folgen:** `AspNetControllerRouteAnalyzer`, `MinimalApiEndpointAnalyzer`, `MiddlewarePipelineAnalyzer`, `DependencyInjectionAnalyzer` (zirkuläre Deps graph-basiert!), `GrpcServiceAnalyzer`, `RouteConflictAnalyzer`.

### 4.2 Roslyn-spezifische Patterns die CodeGraph nicht sieht (wir billig könnten)

- **Nullable-Reference-Types** + `#nullable enable`-Audit
- **`record` / `record struct` / `readonly record struct`** — Immutability-Analysen
- **`init`-only Setters** — "alle Felder müssen in `init`/`ctor` gesetzt sein"
- **`IAsyncEnumerable<T>`-Pattern** — `await foreach` statt `MoveNextAsync`
- **Pattern Matching** — Exhaustiveness für `switch`
- **`required`-Modifier** (C# 11) — alle `required` Properties müssen im Initializer gesetzt sein
- **`nameof()`-Empfehlungen** statt Magic-Strings
- **`Span<T>`/`ReadOnlySpan<T>`** für Hot-Path-Allocations
- **Source-Generators** (`[Generator]`-Attribut) — Custom-Linting der Generator-Outputs
- **EF Core ModelBuilder** (`IEntityTypeConfiguration<T>`)
- **AutoMapper `CreateMap<A, B>()`** Profil-Analyse
- **MediatR `IRequestHandler<TReq, TRes>`** — Handler-Resolution
- **Microsoft.Extensions.Logging** `ILogger<T>`-Injection-Checks

### 4.3 Dynamic-Dispatch-Patterns die CodeGraph synthetisieren muss — Roslyn sieht sie direkt

CodeGraph synthetisiert Kanten via Heuristik (`provenance:'heuristic'`), weil tree-sitter keine Semantik kennt. Roslyn **sieht** sie direkt:

| Pattern | CodeGraph (Heuristik) | Roslyn (strukturell) |
|---------|----------------------|---------------------|
| `Activator.CreateInstance(typeof(T))` | String-Scan | `InvocationExpression` + `ITypeSymbol` |
| `IServiceProvider.GetService<T>()` | Pattern-Match | `GenericNameSyntax` + `ITypeSymbol` |
| `dynamic x; x.Foo()` | Member-Access-Tracking | `IDynamicMetaObjectProvider` + `DynamicType`-Symbol |
| `Task.Run(...)` / `ThreadPool.QueueUserWorkItem` | Pattern-Scan | `InvocationExpression` + `IMethodSymbol` |
| `MethodInfo.Invoke(...)` | String-Tracking | nicht statisch machbar (kein Marker) |
| `IRequestHandler<TReq,TRes>` (MediatR) | Pattern-Match | `INamedTypeSymbol.AllInterfaces` + Generic-Argument |

→ **Für AiNetLinter kann `provenance` immer `'roslyn'` oder `'roslyn-symbolic'` sein — nie `'heuristic'`.** Außer für explizite Reflection-Patterns ohne Marker.

---

## 5. Spezifische Auswertung für den Drift-Loop-Workflow

Der User-Workflow: **Planer/Coder/Kritiker/Orchestrator** (in `.agents/Agent-Scaffolding/dev-loop/drift-loop/` dokumentiert). Strikt seriell, JIT-Planung, 4-Prüfebenen-Kritiker.

**Welche MCP-Tools braucht jede Rolle?**

| Rolle | Typische Tasks | Braucht |
|-------|---------------|---------|
| **Planer** (Roadmap-Modus) | Solution-Struktur erkunden, Hotspots finden, Epics ableiten | `get_index_scope`, `get_hotspots`, `skeleton` (geplant), `dependency_graph` (geplant) |
| **Planer** (Step-Modus) | Vor jedem Step: aktuellen Stand verstehen, gezielt 1-2 Regeln lesen | `get_file_skeleton`, `find_symbol`, `get_violations` (mit scopeFilter) |
| **Coder** | Symbol suchen, Edits vorbereiten, Lint-Fehler fixen | `find_symbol`, `find_references`, `get_symbol_body`, `get_violations`, `search_pattern` |
| **Kritiker** | 4 Prüfebenen: Plan, Rules, Logik, Konzept-Treue | `get_type_hierarchy`, `find_references` (depth), `get_violations`, `safeguard` (geplant), `pattern_detect` (geplant) |

**Heutige Lücken im Drift-Loop-Workflow:**
1. Planer muss oft `get_hotspots` + `get_index_scope` + `find_symbol` separat machen → `solution_map` (M4) **konsolidiert das in 1 Call**
2. Kritiker kann nicht „gib mir alle God-Classes" fragen → `pattern_detect` (S1) **deckt das ab**
3. Niemand kann Edit + Validate + Rollback atomar machen → `preview_refactor` (S3) **schließt die Lücke**
4. Coder bekommt keinen „Stopp-Hinweis" wenn der Lint-Score zu niedrig ist → `safeguard` (M3) **fungiert als Quality-Gate**

**Kostenschätzung pro Drift-Loop-Step heute vs. nach Aufwertung:**

Heute typischer Step: 15-25 Tool-Calls (Planer: 3-5, Coder: 8-12, Kritiker: 4-8). Nach M1-M7: 8-15 Tool-Calls bei besserer Information. **Geschätzt 30-40% Token-Save pro Step + 50% Latenz-Save durch Konsolidierung.**

---

## 6. Architektur-Empfehlungen (was wir am MCP-Server grundsätzlich ändern sollten)

### 6.1 Server-Instructions als Single-Source-of-Truth

Heute hat AiNetLinter einen kurzen `ServerInstructions`-Text beim `initialize`-Handshake (C#-only-Hinweis). CodeGraph hat eine ausführliche Datei (`server-instructions.ts`, 106 Zeilen) mit Anti-Pattern-Liste.

**Vorschlag:** Eigene `src/AiNetLinter/Mcp/ServerInstructions.cs` mit:
- 10 Tools + Zweck (1 Satz je Tool)
- C#-only-Hinweis mit Fallback-Empfehlung
- **Sufficiency-Doctrine**: „Wenn ein Tool Source liefert, nicht nochmal lesen"
- **`isError: true`-Hinweis**: „Bei recoverable Fehlern gibt der Server success-shaped Text zurück. Nur bei SOLUTION_NOT_LOADED oder Path-Refusal ist isError=true."
- **Drift-Loop-spezifische Hinweise** (geplant): „Für Quality-Check: `safeguard`. Für Pattern-Detection: `pattern_detect`."

### 6.2 Tool-Curation: 10 → 12-15, dann Progressive Disclosure

Stand der MCP-Design-Lehre 2026: **5-15 ideal, max. 50 Tools/Server**. AiNetLinter hat 10 — gut im Korridor. Mit den 5-7 neuen Tools kommen wir auf 15-17. Wenn wir Pattern-Detection weiter ausbauen, irgendwann >50 — dann **Progressive Disclosure** als Meta-Tool.

### 6.3 Structured Output (JSON Schema 2020-12)

MCP-Spec 2026-07-28 erlaubt `outputSchema` + `structuredContent` für jeden Tool. **Jedes neue Tool bekommt strukturiertes JSON-Output zusätzlich zum Markdown-Text.** Vorteile:
- Agent kann programmatisch weiterverarbeiten
- Weniger Parse-Bugs
- Vorbereitung für MCP-Apps-Integration

### 6.4 Cache-Hints in `list`-Responses

MCP-Spec 2026-07-28 SEP-2549: `tools/list` darf Cache-Hints tragen. **Nutzen wir für teure Lookups** (z. B. `get_index_scope`-Cache für 60s, `get_hotspots` für 30s).

### 6.5 Transport-Erweiterung: stdio + Streamable HTTP

Stand 2026: stdio für Dev, **Streamable HTTP** für Cloud/CI/Enterprise. AiNetLinter ist aktuell stdio-only. **Mittelfristig** Streamable HTTP nachrüsten — vor allem für CI/CD-Integration (GitHub Actions, Azure DevOps).

### 6.6 Multi-Agent-Installer (wie CodeGraph)

PowerShell-Skript `ainetlinter install` (oder `--install-agent <target>`) das:
- Detect vorhandene Agent-Configs (Claude, Cursor, Codex, opencode, Windsurf, Aider, Continue, VS Code)
- Pro Target: MCP-Config-File schreiben (JSON / JSONC / TOML)
- Marker-basierte Idempotenz
- Uninstall mit Rollback

---

## 7. Risiken & Trade-offs

| Risiko | Wahrscheinlichkeit | Mitigation |
|--------|-------------------|-----------|
| **Footprint-Limits** (TD-005/006): MCP-Registrar-Klassen am `MaxAIContextFootprint`-Limit | Hoch (bei jedem neuen Tool) | Konsolidierung in Helper-Klassen, PathOverride-Werte moderat anheben, Konsolidierungs-Epic in Tech-Debt-Liste aufgenommen |
| **Stale Call-Log-Stats**: Agent merkt nicht dass neue Tools da sind | Mittel | ServerInstructions + Resources-Update bei Connect, Tool-Annotations mit `version` |
| **Heuristik-False-Positives** in `trace_flow` / Pattern-Detect | Mittel | Auslieferung **ohne** synthetisierte Kanten starten, klare Doku, iterative Verbesserung |
| **Performance bei großen Solutions** (Cold-Start MSBuildWorkspace) | Mittel | Persistenter Index analog zu `.codegraph/` (`.ainetlinter/`-Verzeichnis), inkrementelles Refresh ausbauen |
| **MCP-Spec-Drift** (2026-07-28 bringt breaking changes) | Hoch | Auf MCP C# SDK v2.0 wechseln, breaking changes absorbieren, structuredContent von Anfang an |
| **C#-only-Blindheit** (Agent versucht Symbol in `.razor` zu suchen) | Niedrig (durch C#-only-Hinweis gemildert) | `search_pattern`-Fallback klar dokumentiert, ggf. erweitern auf Razor-Symbolsuche via Roslyn |

---

## 8. Konkrete nächste Schritte (für die Diskussion)

1. **Review dieser Synthese** + der 4 Sub-Reports mit dem User
2. **Roadmap-Priorisierung** festlegen in `05-recommendations-roadmap.md` (separater Schritt)
3. **Konzept-Dokument** erstellen für die ersten 3-5 Epics (analog zum Drift-Loop-Format)
4. **Drift-Loop** starten mit Epic 1: `trace_flow` MVP (M1) + Sufficiency-Hinweise (M2) + `isError: true`-Audit (M7)
5. **CI-Integration** sicherstellen: Tests müssen grün, Footprint-Limits einhalten, Doku synchron

---

## Anhang A — Datei-Index

| Datei | Zweck | Status |
|-------|-------|--------|
| `tasks/features/00-master-overview.md` | **Dieses Dokument** — Einstiegspunkt, Synthese | ✅ |
| `tasks/features/01-codegraph-recon.md` | CodeGraph-Architektur, MCP-Tools, Patterns | ✅ |
| `tasks/features/02-ainetlinter-mcp-current.md` | AiNetLinter IST-Zustand, Stärken/Schwächen, Quick-Wins | ✅ |
| `tasks/features/03-market-research.md` | Roslyn-MCP-Markt, MCP-Spec 2026, Token-Opt | ✅ |
| `tasks/features/04-explore-vs-flow-tools.md` | Detaillvergleich explore vs find_references, trace_flow-Vorschlag | ✅ |
| `tasks/features/05-recommendations-roadmap.md` | Konsolidierte Roadmap, priorisiert, Epics | 📝 in Arbeit |

## Anhang B — Quellen für Detail-Tiefe

- **CodeGraph-Code:** `C:/Daten/Entwicklung/GitHub/codegraph/src/mcp/` (insb. `tools.ts`, `server-instructions.ts`, `installer/targets/`, `db/schema.sql`)
- **AiNetLinter-Code:** `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/` (insb. `McpCodeGraphServer.cs`, `Mcp/Tools/`, `McpServerOptionsFactory.cs`)
- **AiNetLinter-Doku:** `C:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/agent-api.md`, `Docs/ROADMAP.md`, `Docs/rationale.md`
- **Drift-Loop-Spec:** `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md`
- **Bestehende Tech-Debt:** `C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/tech-debt-konsolidierung/konzept.md` (TD-001 bis TD-006, alle im MCP-Bereich)
- **Bestehende Feature-Audit-Doku:** `C:/Daten/Entwicklung/Ralf/AiNetLinter/Research/FeatureAudit/Result/` (17 Metrics, 20 Bool-Rules, 9 Features, 3 New-Feature-Proposals)
