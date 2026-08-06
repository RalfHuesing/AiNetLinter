# CodeGraph Recon – Analysebericht für AiNetLinter

**Repo:** `https://github.com/colbymchenry/codegraph` (lokal: `C:/Daten/Entwicklung/GitHub/codegraph`)
**Version:** 1.5.0 (Stand 2026-08)
**Maintainer:** colbymchenry (kommerzielles Produkt "CodeGraph platform" + OSS-CLI/MCP)
**Autor dieser Analyse:** Recon-Subagent
**Datum:** 2026-08

> **Zielsetzung:** Bewertung, welche Ideen, Architektur-Patterns und MCP-Tool-Konzepte aus CodeGraph
> sich für einen Roslyn-basierten C#-Linter (AiNetLinter) sinnvoll übernehmen lassen – und welche
> nicht. Reine Analyse, **keine Implementierung**.

---

## 1. Executive Summary

CodeGraph ist ein **lokales Code-Intelligence-System** (TypeScript/Rust, MCP-Server), das AI-Agents
wie Claude Code, Cursor oder Codex eine **vorberechnete Wissensgraph-Antwort** auf jede strukturelle
Frage liefert – typischerweise in 1–3 Tool-Calls ohne dass der Agent noch `Read`/`Grep` braucht.
Gemessen auf 7 Open-Source-Repos: **88 % weniger Tool-Calls, 53 % schneller, 0 File-Reads** im
Vergleich zum Datei-Lesen-Agenten.

**Kernidee hinter dem Erfolg ist nicht "noch mehr Tools", sondern Disziplin:**
Ein einziges MCP-Tool (`codegraph_explore`) als **Primary Surface**, klare
**Sufficiency-Doctrine** ("Output muss so vollständig sein, dass der Agent aufhört zu lesen"),
**keine `isError: true`-Antworten** bei erwartbaren Fehlern, und ein **Multi-Agent-Installer**, der
sich um die ganze Pro-Detekt/Schreibe-Werkzeug-Konfiguration kümmert.

**Für AiNetLinter hochinteressant:**
- Das **`codegraph_node`-file-view-Pattern** (statischer Read-Ersatz mit Blast-Radius) ist 1:1
  in Roslyn umsetzbar – mit `SyntaxTree.GetText()` + SymbolFinder.
- Die **Knowledge-Graph-Idee** (Knoten + Kanten + FTS5 + `provenance:'heuristic'`) ist ein
  Architekturmuster, das in einer Linter-DB ohne Verrenkungen wiederverwendet werden kann.
- Der **ASP.NET-Resolver in CodeGraph ist nur ein dünner Regex-Layer** (siehe Kapitel 4.4) – mit
  Roslyn ließen sich Routes, DI-Graph, Middleware-Pipeline und Minimal-API-Handler
  **strukturell** statt textuell extrahieren. **Das ist die größte Übertragungschance.**
- Die **Multi-Agent-Installer-Architektur** (Targets + Registry + Marker-basierte Idempotenz) ist
  nahezu 1:1 für Claude Code, Cursor, Codex, Continue, Windsurf und Aider übernehmbar – alles
  Text-Configs, die Roslyn-/AiNetLinter-MCP-Server-Args eintragen.

**Nicht übernehmen:** Detached-Daemon-Modus mit Lock-File-Arbitration (für ein .NET-Tool nicht
nötig – eine in-process `BackgroundService` mit FileSystemWatcher reicht), Rust-Kernel
(tree-sitter-WASM + Roslyn erledigt das für C# ohnehin strukturell besser), Multi-Language-Support
(33 Sprachen irrelevant – Fokus C#).

---

## 2. Was ist CodeGraph? (Kern, Killer-Features, Schwächen)

### 2.1 Projekt-Überblick

CodeGraph ist **drei Dinge in einem npm-Paket** (`@colbymchenry/codegraph`):

1. **Library** (`src/index.ts` exportiert `CodeGraph`-Klasse) – Embedded-Nutzung in eigenen Tools.
2. **CLI** (`codegraph install | init | index | sync | status | query | files | context | explore | node | callers | callees | impact | affected | serve --mcp | daemon | upgrade | uninstall | unlock | telemetry`).
3. **MCP-Server** (`serve --mcp`) – wird vom Agent gestartet, spricht JSON-RPC-2.0 über stdio
   (oder über einen Unix-Domain-Socket, wenn der geteilte Daemon läuft).

Backend: **SQLite (Node-eigenes `node:sqlite`, ≥ 22.5)** mit FTS5, WAL und projektspezifischer
DB-Datei unter `.codegraph/codegraph.db`. Parsing: **Rust-Kernel** (`tree-sitter` via WASM, mit
nativem Fallback) für 20 Sprachen, plus Standalone-Extractoren für Vue/Svelte/Liquid/Razor/Dfm.

### 2.2 Killer-Features (was CodeGraph außergewöhnlich macht)

| # | Feature | Warum es funktioniert | Übertragbarkeit |
|---|---------|----------------------|------------------|
| K1 | **Single-Tool-Doctrine** – nur `codegraph_explore` ist DEFAULT-exposed (sieben weitere Tools existieren, sind aber per `CODEGRAPH_MCP_TOOLS` opt-in). | Verhindert "Tool-Selection-Overload" beim Agent; das `server-instructions.ts`-Manifest bringt ihn dazu, **immer** zuerst `explore` zu versuchen. | **MUST-ÜBERNEHMEN** |
| K2 | **Sufficiency Principle** – Output von `codegraph_explore` enthält verbatim Source + Call-Path + Blast-Radius in einer Antwort, sodass der Agent **nicht mehr `Read` muss**. | Gemessen: 0 File-Reads in 7/7 Benchmark-Repos. Wording: "treat returned source as already Read". | **MUST-ÜBERNEHMEN** |
| K3 | **File-View-Mode in `codegraph_node`** – `codegraph_node({file: "x.cs"})` ohne Symbol liefert die Datei im exakt gleichen `<n>\t<line>`-Format wie `Read`, **plus** Blast-Radius-Header. | Ersetzt das Built-in-`Read` ohne Token-Mehrkosten. Sicherheit: `validatePathWithinRoot` (kein Pfad-Traversal). | **MUST-ÜBERNEHMEN** |
| K4 | **Per-File Staleness Banner** – MCP-Antworten, die auf eine zwischen Index und Read geänderte Datei verweisen, prependen `⚠️ …` und sagen dem Agent explizit, dass er diese Datei `Read` soll. | Validiert: Claude Code sagt daraufhin wortwörtlich "Reading the file directly for the live content". | **MUST-ÜBERNEHMEN** |
| K5 | **`isError: true` fast nie** – "expected/recoverable" Fehler kommen als **success-shaped** Text-Result mit Anleitung (z. B. "no `.codegraph/` found → pass `projectPath`"). Nur Sicherheitsverweigerungen und echte Malfunctions werfen `isError`. | **Wartungsbeobachtung:** 1–2 `isError`-Antworten am Session-Anfang → Agent benutzt das Tool nie wieder. | **MUST-ÜBERNEHMEN** (extrem wichtig) |
| K6 | **Single Source of Truth für Agent-Guidance** – `src/mcp/server-instructions.ts` (im `initialize`-Response) ist die einzige Stelle, an der Agent-Verhalten beschrieben wird. Issue #529 hat das Installations-Duplikat (`## CodeGraph` in `CLAUDE.md`) explizit entfernt. | Verhindert Drift zwischen Tool-Description, Install-File und MCP-Manifest. | **MUST-ÜBERNEHMEN** |
| K7 | **Adaptive Explore-Budgets** – `getExploreBudget(fileCount)` skaliert 1–5 Calls je nach Repo-Größe; `getExploreOutputBudget(fileCount)` skaliert `maxCharsPerFile` mit **monoton steigender Invariante** (größere Tier ⇒ mehr Bytes/Datei, nie weniger). | Verhindert, dass ein 415-KB-Godfile in 1% zurückkommt und der Agent dann doch `Read` muss. | **MUST-ÜBERNEHMEN** (Konzept, eigene Stufen) |
| K8 | **Framework-Aware Routes** – 17+ Web-Frameworks (Express, FastAPI, Spring, ASP.NET, …) erzeugen `route`-Knoten + `references`-Edges zu Handlern. | Macht aus "grep `'/users'`" eine Graph-Traversal-Antwort. | **MUST-ÜBERNEHMEN** (Roslyn-Variante deutlich mächtiger) |
| K9 | **Multi-Agent-Installer** – 8 Targets (Claude Code, Cursor, Codex CLI, opencode, Hermes Agent, Gemini CLI, Antigravity IDE, Kiro). Jeder Target ist ein einzelnes File in `targets/`, Eintrag in `registry.ts`. | Onboarding-UX: ein Befehl, alle Agents konfiguriert. | **MUST-ÜBERNEHMEN** |
| K10 | **Rust-Kernel für 20 Sprachen** – Parsing in kompiliertem Code, byte-für-byte identische Graphen zur Reference-Engine, per-file Fallback auf TS-Implementation. | Performance: Linux-Kernel (70k Files, 2M Symbole, 6.4M Edges) in <12 Min auf 2-Core-VPS. | **NICHT für C#** – Roslyn ist semantisch tiefer, tree-sitter ist für C# unnötig. |
| K11 | **Detached Daemon mit Lock-File-Arbitration** – ein Daemon pro Repo, geteilt von N Agent-Sessions; Idle-Timeout reapet sich selbst. | Löst das "Session-1-Terminal-schließt-alle" Problem. | **TEILWEISE** – eine in-process `BackgroundService` reicht für Linter. |
| K12 | **Dynamic-Dispatch-Synthesizer** – separate Passes erzeugen Edges für Callbacks, EventEmitter, React-re-render, JSX-Child, Django-ORM, MediatR, Vue Proxy, …, getaggt mit `provenance:'heuristic'`. | Schließt die letzte Meile zwischen "statisch extrahiert" und "Antwort aus Graph möglich". | **MUST-ÜBERNEHMEN** (Roslyn: MediatR, DispatcherProxy, async-continuation, etc.) |

### 2.3 Was CodeGraph **nicht** macht (Schwächen / Lücken)

| Lücke | Relevanz für AiNetLinter |
|-------|--------------------------|
| **Keine statische Semantik** – tree-sitter sieht nur AST, kennt keine Typen, keine Generics-Resolution, keine Nullability, keine Operator-Overloads, keine Extension-Methods, keine LINQ-Expression-Trees. | AiNetLinter nutzt **Roslyn-Symbol-API** → kann semantisch tiefer analysieren als CodeGraph es je für C# könnte. |
| **Roslyn vs. tree-sitter für C#** – CodeGraphs C#-Extractor (`src/extraction/languages/csharp.ts`) hat einen Workaround für einen tree-sitter-Grammar-Bug bei `#if`-Direktiven in Enums und ist gezwungen, den **Methoden-Return-Type** selbst zu extrahieren, um chained calls (`Foo.Create().Bar()`) auflösen zu können. | Genau das löst Roslyn nativ via `ITypeSymbol`/`IMethodSymbol.ReturnType` – AiNetLinter bekommt das geschenkt. |
| **ASP.NET-Support ist dünn** – nur Controller-Routes (Regex über Attribute `[HttpGet/Post/...]`) + Minimal-API-MapGet/MapPost, plus Konventions-Resolution über Verzeichnisnamen (`/Controllers/`, `/Services/`). Keine Middleware-Pipeline, keine Filters, keine Model-Binding-Analyse, keine SignalR-Hubs, keine gRPC-Services, keine AuthN/AuthZ-Policies. | Riesige Übertragungschance für AiNetLinter. Roslyn + Working-Set-Scanner (`Microsoft.AspNetCore.*`) kann das strukturell abdecken. |
| **Kein Caching über Sessions** – `codegraph status` zeigt `walSizeBytes` und Kommentar: "killed sessions left a WAL leak (#1431)". WAL-Healing ist explizit im Code. | DB-Lifecycle-Hygiene ist relevant – `Microsoft.Data.Sqlite` (offiziell .NET-nativ) hat dasselbe Problem. |
| **Kein nativer .NET-Support** – ganzes Tool ist TS+Rust, .NET-User müssen npm-Installer akzeptieren. | AiNetLinter ist nativ .NET → `dotnet tool install` ist vertrauter als `npm i -g`. |
| **Kein Streaming / keine Progress-Events** – lange `init`/`index` blockieren ohne Streaming-Updates an den MCP-Client. MCP-Spec 2025 hat Progress-Notifications. | Modernisierungsspielraum (siehe Kapitel 7). |
| **"Residual context occupancy"** – verbatim zurückgegebene Source bleibt im Context-Window; auf VS-Code 67k Tokens vs 18k beim Read-Agent. Ein Feature, kein Bug, aber Trade-off. | Für AiNetLinter zu beachten: LLM-Agent bekommt großen Source-Block; ggf. trimming/segmentation nötig. |
| **`codegraph_explore` muss raten, welche Symbole zur Frage passen** – die "Symbol-bag-in-query"-Disambiguation ist Heuristik; bei mehrdeutigen Namen werden alle Overloads zurückgegeben (Feature), aber die Reihenfolge ist ein gerankter Score, kein Beweis. | Für Roslyn: `Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindAllOverrides` + `FindReferencesAsync` sind exakt; Roslyn-basierter Linter braucht diese Heuristik nicht. |

---

## 3. Repository-Struktur

```
codegraph/                                 # ~ 1.5.0
├── package.json                           # @colbymchenry/codegraph, node ≥ 20 < 25
├── CLAUDE.md                              # 270-Zeilen-Agent-Guide, GOLDGRUBE
├── README.md                              # 866 Zeilen Marketing + Doku
├── src/
│   ├── index.ts                           # 24 KB – CodeGraph-Class public API
│   ├── types.ts                           # NodeKind, EdgeKind, Language, Node, Edge
│   ├── bin/
│   │   ├── codegraph.ts                   # CLI (commander), 2384 Zeilen
│   │   └── node-version-check.ts          # harten Exit auf Node <20 oder ≥25
│   ├── db/
│   │   ├── schema.sql                     # SQLite + FTS5
│   │   ├── sqlite-adapter.ts              # better-sqlite3-Shape um node:sqlite
│   │   ├── queries.ts                     # prepared statements
│   │   ├── database-connection.ts
│   │   ├── query-pool.ts                  # worker-thread WAL-read pool
│   │   └── wal-valve.ts
│   ├── extraction/                        # 122 KB orchestrator + 28 KB grammars.ts
│   │   ├── index.ts                       # ExtractionOrchestrator
│   │   ├── tree-sitter.ts                 # 317 KB – alle Sprachen-Parsing
│   │   ├── wasm/                          # *.wasm-Grammatiken
│   │   ├── parse-pool.ts                  # off-thread parsing
│   │   ├── parse-worker.ts
│   │   └── languages/                     # 30+ Per-Sprache-Extractoren
│   │       └── csharp.ts                  # tree-sitter-c_sharp, mit #if-Workaround
│   ├── resolution/                        # 61 KB
│   │   ├── index.ts                       # ReferenceResolver Orchestrator
│   │   ├── import-resolver.ts             # tsconfig-paths, go.mod, package.json
│   │   ├── name-matcher.ts                # cross-file name resolution
│   │   ├── path-aliases.ts                # tsconfig-paths, cargo workspace globs
│   │   ├── resolver-pool.ts               # worker-thread pool
│   │   ├── callback-synthesizer.ts        # dynamic-dispatch edges
│   │   ├── cooperative-yield.ts           # event-loop yield (für #850-watchdog)
│   │   ├── lru-cache.ts                   # bounded LRU
│   │   └── frameworks/                    # 17+ Framework-Resolver
│   │       ├── csharp.ts                  # ASP.NET/MVC/Minimal-API
│   │       ├── python.ts                  # Django, Flask, FastAPI
│   │       ├── java.ts                    # Spring
│   │       ├── go.ts, goframe.ts
│   │       ├── rust.ts
│   │       ├── swift.ts                   # SwiftUI, UIKit, Vapor
│   │       └── …
│   ├── graph/
│   │   ├── index.ts                       # GraphQueryManager (high-level)
│   │   ├── traversal.ts                   # BFS/DFS, Impact-Radius, Path-Finding
│   │   └── queries.ts
│   ├── context/
│   │   ├── index.ts                       # ContextBuilder
│   │   └── formatter.ts                   # markdown/json für LLM
│   ├── search/                            # FTS5-Query-Parser
│   ├── sync/                              # FileWatcher, git-hooks, worktree
│   │   ├── watcher.ts
│   │   ├── watch-policy.ts
│   │   ├── git-hooks.ts
│   │   └── worktree.ts
│   ├── mcp/                               # 314 KB MCP-Server (siehe §4)
│   │   ├── index.ts                       # MCPServer (Lifecycle)
│   │   ├── transport.ts                   # Stdio + Socket Transport
│   │   ├── server-instructions.ts         # SINGLE SOURCE OF TRUTH für Agent
│   │   ├── tools.ts                       # 314 KB – alle Tool-Defs + Handlers
│   │   ├── engine.ts                      # shared engine (cg + watcher)
│   │   ├── daemon.ts                      # detached background server
│   │   ├── proxy.ts                       # stdio↔socket bridge
│   │   ├── session.ts                     # initialize / list / call
│   │   ├── query-pool.ts                  # worker-thread read pool
│   │   ├── explore-*.ts                   # explore dedup, ranking, budget
│   │   ├── dynamic-boundaries.ts
│   │   └── liveness-watchdog.ts           # #850 main-thread wedge detector
│   ├── installer/                         # 5 KB index, targets/ für jedes Agent
│   │   ├── index.ts                       # runInstaller(WithOptions)
│   │   ├── config-writer.ts               # JSON-MCP-Config
│   │   ├── instructions-template.ts       # nur noch Marker-Konstanten
│   │   └── targets/
│   │       ├── registry.ts                # 8 Targets
│   │       ├── shared.ts, types.ts, toml.ts
│   │       ├── claude.ts, cursor.ts, codex.ts, opencode.ts
│   │       └── hermes.ts, gemini.ts, antigravity.ts, kiro.ts
│   ├── telemetry/                         # anonymous usage
│   ├── upgrade/                           # self-update logic
│   └── ui/                                # terminal-shimmer
├── docs/                                  # ~ 20 Design/Benchmark-Docs
│   ├── design/
│   │   ├── agent-codegraph-adoption.md
│   │   ├── dynamic-dispatch-coverage-playbook.md
│   │   ├── callback-edge-synthesis.md
│   │   ├── dispatch-synthesizer-backlog.md
│   │   └── csharp-kernel-port-checklist.md
│   ├── benchmarks/
│   │   ├── explore-sufficiency.md
│   │   ├── explore-allocation-efficiency.md
│   │   ├── call-sequence-analysis.md
│   │   └── residual-context-occupancy.md
│   └── core-concepts/, getting-started/, guides/
├── __tests__/                             # 200+ Tests (Vitest)
├── scripts/agent-eval/                    # A/B-Harness für Claude runs
├── site/                                  # Astro-Doku-Site
└── telemetry-worker/, telemetry-dashboard/
```

**Wichtige Beobachtungen:**

1. **CodeGraph-CSHarp-Extractor (`src/extraction/languages/csharp.ts`) ist real und existiert** – mit
   spezifischem Workaround für den tree-sitter-Grammar-Bug bei `#if`-Direktiven in Enums (Issue
   #237) und einer `extractCsharpReturnType`-Funktion, die genau die Method-Return-Types für
   chained-call-Resolution extrahiert (Issue #645).

2. **Der `tools.ts` ist mit 314 KB mit Abstand die größte Datei** – das ist nicht "Spaghetti",
   sondern die ca. 5000 Zeilen MCP-Tool-Handler-Logik (vor allem `codegraph_explore`'s
   Flow-Section + adaptive Budgets + Disambiguation).

3. **Der Installer ist bewusst Marker-basiert** (`<!-- CODEGRAPH_START -->`/`<!-- CODEGRAPH_END -->`)
   – Idempotenz ohne JSON-Parser-Drift. Diese Technik ist 1:1 für AiNetLinter übernehmbar.

---

## 4. MCP-Server-Inventar (alle Tools)

### 4.1 Server-Lifecycle

- **Modes:** `direct` (1 Prozess, stdio) | `proxy` (lokales Handshake, forward an geteilten
  Daemon) | `daemon` (detached, geteilt per Unix-Socket/Windows named pipe).
- **Decision order** in `src/mcp/index.ts:267`:
  1. `CODEGRAPH_NO_DAEMON=1` → `direct`
  2. `CODEGRAPH_DAEMON_INTERNAL=1` → wir **sind** der Daemon
  3. Kein `.codegraph/` erreichbar → `direct`
  4. Sonst → `proxy` (mit Daemon-Connect im Hintergrund)
- **`isError: true` ist reserviert für "stop trying"** – Sicherheitsverweigerungen
  (`PathRefusalError`) und echte Malfunctions. **Alle erwartbaren Fehler kommen als
  Success-Text-Result mit Anleitung.** Das ist eine der zentralen Doctrines (Kapitel 2.2, K5).

### 4.2 Tool-Inventar

Quelle: `src/mcp/tools.ts:960-1170` (Tool-Definitionen), `src/mcp/tools.ts:2030+` (Handler).

| # | Tool-Name | DEFAULT? | Zweck | Input | Output-Form |
|---|-----------|----------|-------|-------|-------------|
| 1 | **`codegraph_explore`** | ✅ (Primary) | Eine Capped-Antwort: Verbatim-Source der relevanten Symbole gruppiert nach Datei + Call-Path zwischen ihnen + Blast-Radius. | `query: string`, `maxFiles?: number=12`, `projectPath?` | Markdown: Header, File-Sections (`<n>\t<line>`), Flow-Section, "Not shown above"-Footer |
| 2 | `codegraph_search` | optional | Symbol-Suche nach Name – liefert **nur Locations, kein Code**. | `query`, `kind?`, `limit?=10`, `projectPath?` | Liste von `{name, kind, filePath, line}` |
| 3 | `codegraph_callers` | optional | Wer ruft `<symbol>` auf? | `symbol`, `file?`, `limit?=20`, `projectPath?` | Liste von Aufrufer-Sites |
| 4 | `codegraph_callees` | optional | Was ruft `<symbol>` auf? | dito | Liste von Calle-Sites |
| 5 | `codegraph_impact` | optional | Symbol-Impact-Radius (BFS-Transversal) | `symbol`, `file?`, `depth?=2`, `projectPath?` | Subgraph-Outline |
| 6 | **`codegraph_node`** | optional | **Doppelmodus:** (a) `file:`-allein → Read-Ersatz mit Blast-Radius-Header. (b) `symbol:` → eine Symbol-Definition + Caller/Callee-Trail. Bei mehrdeutigen Namen **alle** Overloads. | `symbol?` \| `file?`, `includeCode?`, `offset?`, `limit?`, `symbolsOnly?`, `line?`, `projectPath?` | Verbatim Source (file-view) oder Symbol-Detail mit Trail |
| 7 | `codegraph_status` | optional | Index-Health (Files/Nodes/Edges), Pending-Files, WAL-Info. | `projectPath?` | JSON-Text-Output |
| 8 | `codegraph_files` | optional | Indexed File-Tree mit Language + Symbol-Counts. | `path?`, `pattern?`, `format?='tree'\|'flat'\|'grouped'`, `includeMetadata?=true`, `maxDepth?`, `projectPath?` | Tree-Listing |

**Tool-Annotations** (alle read-only): `readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false`.

### 4.3 Tool-im-Detail: `codegraph_explore`

Das ist das zentrale Tool. Aus `server-instructions.ts` und `tools.ts:3095-5087`:

**Input-Disambiguation** (welche Symbole passen zur Query?):
- `query` kann ein "Bag of Symbol-Namen" sein (z. B. `"AuthService loginUser session-manager"`) ODER
  eine natürlichsprachliche Frage ODER beides gemischt.
- **Co-naming disambiguation**: PascalCase-Token in der Query biasen overloaded Names
  (Beispiel: `DataRequest task` → DataRequest's `task`, nicht die abstract base).
- **Overload-aware**: Bei mehrdeutigen Method-Signaturen werden alle Definitionen in einem Call
  zurückgegeben (validiert auf Alamofire/gin, wo Agent sonst File liest um den richtigen Overload
  zu finden).

**Output-Struktur:**
1. Header: "Found N symbols across M files."
2. Per-File-Sektionen mit `<n>\t<line>`-formatierter Source (cat -n kompatibel).
3. **Flow-Sektion**: Wenn die Query mehrere Symbole enthält, die durch Calls verbunden sind, wird
   der kürzeste Pfad zwischen ihnen gezeigt, inkl. `provenance:'heuristic'`-Hops (z. B. React
   re-render, callback, JSX-child).
4. **Blast-Radius**: One-liner "Used by N files: …"
5. **"Not shown above"**-Footer: Symbole, die zur Query passen würden, aber aus Budget-Gründen
   gekürzt wurden → Agent soll **eine weitere `codegraph_explore`-Call** machen, **NICHT** `Read`.
6. Bei gestaleness: `⚠️ Some files referenced below were edited since the last index sync…` mit
   expliziter Anweisung an den Agent, **welche** Files er selbst lesen soll.

**Adaptive Budgets** (`src/mcp/tools.ts:161-185`):

| Files | explore-Calls | chars/call | chars/file |
|-------|---------------|------------|------------|
| < 500 | 1 | 18K | 3 800 |
| 500 – 5 000 | 2 | 28K | 6 500 |
| 5 000 – 15 000 | 3 | 35K | 7 000 |
| 15 000 – 25 000 | 4 | 38K | 7 000 |
| ≥ 25 000 | 5 | 38K+ | 7 000+ |

**Invariante:** `maxCharsPerFile` muss monoton mit Tier wachsen (sonst kollabiert ein 415-KB-Godfile
auf <1% und der Agent macht doch `Read`).

### 4.4 ASP.NET-Resolver im Detail (relevant für C#)

`src/resolution/frameworks/csharp.ts` – **220 Zeilen, Regex-basiert, kein Roslyn**:

1. **Detection** (Wann läuft der Resolver?):
   - `.csproj` mit `Microsoft.AspNetCore` / `Microsoft.NET.Sdk.Web` / `System.Web.Mvc`.
   - `Program.cs` mit `WebApplication` / `CreateHostBuilder` / `UseStartup`.
   - Vorhandensein einer `Startup.cs`.
   - `*.cs` mit `[ApiController]` / `[Route]` / `[HttpGet|Post|Put|Patch|Delete]`-Attributen
     ODER `: Controller` / `ControllerBase` / `MapControllers` / `WebApplication` / `Microsoft.AspNetCore`.
   - Der Source-Scan ist **explizit dafür da**, um Feature-Folder-Apps zu erkennen, wo es
     kein `/Controllers/`-Verzeichnis und kein `.csproj` im Index gibt (Issue: realworld sample).

2. **Route-Extraction** (Regex):
   - Klassen-Level `[Route("api/[controller]")]` wird als Prefix für Action-Routen genommen
     (`joinCsPath`).
   - `[HttpGet]` / `[HttpPost]` / etc. – **bare oder mit String-Argument**.
   - Minimal APIs: `app.MapGet("/path", handler)`, `app.MapPost(...)`, etc. – extrahiert Route +
     Handler-Name (Last-Identifier einer dotted expression).

3. **Resolution-Strategien** (Dependency-Resolution):
   - `*Controller` → Suche in `/Controllers/`.
   - `*Service` / `I*` (interfaces) → Suche in `/Services/`, `/Application/`.
   - `*Repository` → Suche in `/Repositories/`, `/Data/`, `/Infrastructure/`.
   - `[A-Z][a-zA-Z]+` (Modellnamen) → Suche in `/Models/`, `/Entities/`, `/Domain/`.
   - `*ViewModel` / `*Dto` → Suche in `/ViewModels/`, `/DTOs/`.

**Schwächen** (warum AiNetLinter hier viel besser sein kann):
- **Keine** SignalR-Hubs, keine gRPC-Services, keine GraphQL-Endpoints.
- **Keine** Middleware-Pipeline-Analyse (`app.UseAuthentication()…`).
- **Keine** Filters / Authorization-Policies / Model-Binding-Analysis.
- **Keine** `IDistributedCache` / `IOptions<T>` / `IServiceCollection`-Resolution – die
  "Dependency Injection"-Resolution läuft nur über Verzeichnis-Konventionen, nicht über
  tatsächliches `IServiceCollection.AddXxx(...)`.
- **Regex-basiert** – ein `// [HttpGet("x")]`-Kommentar wird genauso erfasst wie echter Code.

**Mit Roslyn** ließen sich alle diese Lücken strukturell schließen:

| CodeGraph-Pattern | Roslyn-Äquivalent |
|-------------------|-------------------|
| Regex-Attribut-Parsing | `AttributeSyntax` → `AttributeData` (semantisch!) |
| Konventions-Resolution per Verzeichnis | `IServiceCollection.AddXxx<TService>()`-Analysis via `SyntaxNode.DescendantNodes()` + `SemanticModel.GetSymbolInfo()` |
| Kein Minimal-API-Support für handler-classes | `InvocationExpression` + `MapGet(string, Delegate)` mit `IMethodSymbol` für Handler-Typ |
| Kein Middleware-Pipeline | `app.Use*(...)`-Sequence aus `Program.cs` parsen → Pipeline-Knoten |
| Kein gRPC | `[GrpcService]`-Attribute + Interface mit `[ServiceContract]` |

### 4.5 Server-Instructions (Single Source of Truth)

`src/mcp/server-instructions.ts` enthält **zwei** Varianten:
- `SERVER_INSTRUCTIONS` – Standard (Root ist indexed): "One tool: `codegraph_explore` – use it
  instead of reading files…"
- `SERVER_INSTRUCTIONS_NO_ROOT_INDEX` – wenn der Server ohne eigenes `.codegraph/`-Projekt startet
  (Monorepo-Use-Case, "pass `projectPath` to a project that has a `.codegraph/`").

**Wichtige Designentscheidungen** (in den Comments dokumentiert):
- **Kurz halten** – Agent liest das in jeder Session; lange Instructions verbrennen Tokens.
- **Anti-Patterns explizit nennen** – "Don't re-verify with grep" + "Don't reconstruct a flow by
  hand" + "After editing, check the staleness banner" + "Already sent earlier ist ein Pointer,
  kein Gap".
- **Limitations honest auflisten** – "Index lags ~1s", "Best-effort name-matching", "No live
  correctness validation".

→ **Direkt übernehmbar für AiNetLinter**, **allerdings mit anderer Doctrine**: AiNetLinter ist ein
**Linter** und kein Code-Intelligence-Tool – der Agent soll es für *Regel-Checks* und
*Auto-Fixes* nutzen, nicht primär für Source-Retrieval (das macht der Agent besser selbst).

---

## 5. Architektur & Datenmodell

### 5.1 Pipeline

```
files → ExtractionOrchestrator (tree-sitter/Roslyn) → DB (nodes/edges/files)
              ↓
       ReferenceResolver (imports, name-matching, framework patterns)
              ↓
       GraphQueryManager / GraphTraverser (callers, callees, impact)
              ↓
       ContextBuilder (markdown/JSON for AI consumption)
```

Für AiNetLinter: `ExtractionOrchestrator` ist **schon Roslyn-Syntax-Tree-basierte Analyse** (in
den Rules, siehe AGENTS.md) – wir müssen nicht "extrahieren", sondern **die Roslyn-APIs direkt
verwenden** und nur bei Bedarf eine **Materialisierungs-Schicht** (DB) einziehen, wenn wir
zwischen Sessions cachen wollen.

### 5.2 NodeKind (22 Typen)

`src/types.ts:22` – `as const`-Array, Order matters (Wire-Format mit Native-Kernel):

```
file, module, class, struct, interface, trait, protocol,
function, method, property, field, variable, constant,
enum, enum_member, type_alias, namespace, parameter,
import, export, route, component
```

→ **C#-spezifisch brauchen wir nicht:** `trait`, `protocol`, `module`, `component` (nur für
React/Vue/SolidJS). Können **1:1 übernommen** werden, weil Roslyn die gleichen Konzepte
repräsentiert (`INamedTypeSymbol.TypeKind`).

### 5.3 EdgeKind (12 Typen)

`src/types.ts:56`:

```
contains       – Parent contains child (file→class, class→method)
calls          – Function/method calls another
imports        – File imports from another
exports        – File exports a symbol
extends        – Class/interface extends another
implements     – Class implements interface
references     – Generic reference
type_of        – Variable/parameter has type
returns        – Function returns type
instantiates   – Creates instance of class
overrides      – Method overrides parent
decorates      – Decorator applied
```

→ **Roslyn-Äquivalente:**
| CodeGraph | Roslyn |
|----------|--------|
| `contains` | `Parent → Child` via `SyntaxNode.Parent` |
| `calls` | `SymbolFinder.FindReferencesAsync` / `IOperation` (semantisch!) |
| `imports` | `UsingDirectiveSyntax` / `IImportScope` |
| `extends` | `BaseType` / `ITypeSymbol.BaseType` |
| `implements` | `ITypeSymbol.AllInterfaces` |
| `references` | generisch – `SymbolFinder.FindReferencesAsync` |
| `type_of` | `ITypeSymbol` von `IParameterSymbol.Type` / `ILocalSymbol.Type` |
| `returns` | `IMethodSymbol.ReturnType` |
| `instantiates` | `IOperation` `IObjectCreationOperation` |
| `overrides` | `IMethodSymbol.OverriddenMethod` |
| `decorates` | `AttributeData` |

**Insight:** Roslyn liefert **bereits alle** Edges semantisch korrekt. CodeGraph ist für statische
Sprachen wie C# (mit tree-sitter als Limitation) gezwungen, manche Edges heuristisch zu raten
(`provenance:'heuristic'`). **Für AiNetLinter kann `provenance` immer `'roslyn'` oder
`'roslyn-symbolic'` sein – nie `'heuristic'`.** Außer für explizite dynamische Patterns
(MediatR, Reflection).

### 5.4 DB-Schema (`src/db/schema.sql`)

- `nodes` (id, kind, name, qualified_name, file_path, language, start/end_line, start/end_col,
  docstring, signature, visibility, is_exported, is_async, is_static, is_abstract, decorators,
  type_parameters, return_type, updated_at).
- `edges` (id, source, target, kind, metadata JSON, line, col, provenance DEFAULT NULL).
- `files` (path, content_hash, language, size, modified_at, indexed_at, node_count, errors JSON,
  generated).
- `unresolved_refs` (id, from_node_id, reference_name, reference_kind, line, col, candidates,
  file_path, language, status pending/failed, name_tail).
- FTS5 Virtual Table `nodes_fts` auf (name, qualified_name, docstring, signature).
- `name_segment_vocab` (segment, name) WITHOUT ROWID – camelCase-Splitting für NL-Query-Disambiguation.
- Indexes: `idx_edges_identity` UNIQUE auf (source, target, kind, line, col) – verhindert
  Duplikat-Edges.
- `project_metadata` (key, value, updated_at) – für Version/Provenance.

→ **Für AiNetLinter zu übernehmen** ist sinnvoll, wenn man **zwischen Linter-Invocations
persistieren** will. Alternativ: **in-memory** pro Run (Roslyn-SyntaxTree hält alles, was
gebraucht wird, ohne DB).

### 5.5 Framework-Resolver-System

`src/resolution/frameworks/index.ts` listet **27** Resolver, jeder mit Interface
`FrameworkResolver { name, languages?, detect, resolve, extract?, postExtract? }`.

- `detect(ctx)` → boolean: "Ist dieses Framework in diesem Projekt aktiv?"
- `resolve(ref, ctx)` → ResolvedRef|null: "Kann ich diesen UnresolvedRef auflösen?"
- `extract(filePath, content)` → { nodes, references }: "Synthesisiere Framework-spezifische
  Knoten direkt aus Source."
- `postExtract?` → Node[]: "Nach-Pass für cross-file framework logic."

→ **Für AiNetLinter** ist das exakt das Muster für **Linter-Regel-Plugins** (siehe `rules.json`
in AGENTS.md). Statt `FrameworkResolver` nennen wir es `AnalyzerRule` mit
`detect(solution)`, `analyze(compilation)`, `registerDiagnostic(DiagnosticDescriptor)`.

---

## 6. Performance-Philosophie

### 6.1 Sufficiency Principle (Kerndoktrin)

> "An agent falls back to Read/Grep the instant a codegraph answer is insufficient."

CodeGraph misst das empirisch: Nach jedem `codegraph_explore`-Call wird die nächste Agent-Action
klassifiziert (`docs/benchmarks/explore-sufficiency.md`):

| Next action | Bucket | Was repariert man? |
|-------------|--------|---------------------|
| weitere `codegraph_*`-Call | `explore again` | Insufficient → Tool-Output erweitern |
| `Read` einer Datei, die **wir** zurückgegeben haben | `allocation` | Richtige Datei, falsche Bytes → Source-Länge/Bereich |
| `Read` einer Datei, die **wir nicht** zurückgegeben haben | `recall` | Datei hat nie gesurfacet → Search-Recall verbessern |
| `Grep`/`Glob` | recall (schwächer) | Ebenfalls Recall |
| `Edit`, Build, finale Antwort | `moved on / answered` | **Ausreichend** |

Zentrale These: **Tool-Description-Wording und Hooks bewegen den Agent nur low-salience.** Was
*wirklich* zählt, ist **Coverage** (alle dynamischen Dispatches im Graph) und **Sufficiency**
(der Output ist so komplett, dass der Agent aufhört zu lesen). "Test before building: does this
make a tool the agent *already calls* do more with the input it *already gives*?"

→ **Für AiNetLinter:** wir haben **kein** Agent-Loop-Problem, weil AiNetLinter *nicht* ein
"MCP-Tool-für-Code-Intelligence" ist, sondern ein **Linter mit Regeln** und optionalem **MCP-Server
für LLM-Agent-Integration**. Die Doctrine "isError-frei, success-shaped mit Anleitung" gilt aber
**genauso**.

### 6.2 `isError: true` – Die Lehre

> "One or two `isError: true` responses early in a session and the agent stops calling codegraph
> entirely (maintainer-observed, repeatedly)."

`isError` ist reserviert für:
- **Sicherheitsverweigerungen** (`PathRefusalError` – Path-Traversal-Versuche).
- **Echte Malfunctions** (mit "retry-once" Note).

Alles andere (Project not indexed, Symbol not found, File not in index, Stale file, etc.) kommt als
**Success-Text-Result** mit klarer Anleitung.

→ **Kritisch für AiNetLinter-MCP-Integration:** wenn wir MCP-Tools für den Agent exposen
("`check`", "`fix`", etc.), müssen wir **exakt diese Doctrine** übernehmen.
Ein `check` der "no project found" per `isError: true` zurückgibt, wird vom Agent
innerhalb von 2 Calls komplett ignoriert.

### 6.3 Adaptive Budgets

Vergleiche Kapitel 4.3. **Konzept übernehmbar** für AiNetLinter z. B. bei "welche Regeln soll
der Linter in einem Mono-Repo ausführen, wenn nur ein Subset aktiv ist?" – wir brauchen
vielleicht keine riesigen Budgets, aber die Idee "skaliere mit was messbar da ist" ist wertvoll.

### 6.4 Multi-Agent-Installer

`src/installer/` Architektur ist **nahezu perfekt wiederverwendbar** für AiNetLinter:

```
src/installer/
├── index.ts             # runInstaller + runInstallerWithOptions
├── config-writer.ts     # writeMcpConfig, writePermissions, hasMcpConfig
├── instructions-template.ts  # NUR NOCH Marker (CODEGRAPH_START/END)
├── beta-signup.ts       # optionales Marketing – NICHT übernehmen
└── targets/
    ├── registry.ts      # ALL_TARGETS = [claude, cursor, codex, opencode,
    │                    #               hermes, gemini, antigravity, kiro]
    ├── types.ts         # AgentTarget interface
    ├── shared.ts        # JSON I/O, Marker-Handling, jsonDeepEqual
    ├── toml.ts          # hand-rolled TOML writer für Codex
    ├── claude.ts        # schreibt ~/.claude.json oder ./.mcp.json
    ├── cursor.ts        # tricky: --path injection wegen cwd-bug
    ├── codex.ts         # TOML-Config
    ├── opencode.ts      # opencode.jsonc (jsonc-parser)
    ├── hermes.ts
    ├── gemini.ts
    ├── antigravity.ts
    └── kiro.ts
```

**Installer-Ablauf** (`runInstaller`):
1. Agent-Detection (für jeden Target: `detect()`).
2. CLI-Install auf PATH (optional, `npm install -g`).
3. Location-Frage (global vs. project-local).
4. Auto-Allow-Permissions (nur Claude).
5. Telemetry-Toggle (einmalig).
6. Per Target: `target.install(location, options)` → schreibt MCP-Config + ggf. Instructions-Marker.
7. **Idempotenz**: Marker-basierte Block-Entfernung + `byte-equal re-runs return 'unchanged'`.

**Cursor-Spezialfall** (dokumentiert in `CLAUDE.md:84-86`): Cursor launched MCP-Subprozesse mit
wrong cwd und passiert kein `rootUri` in `initialize` → Installer injiziert `--path` in Cursor's
MCP-Args (absolut für local, `${workspaceFolder}` für global). "If you touch Cursor wiring,
preserve this."

→ **Für AiNetLinter direkt übernehmbar.** Targets für Claude/Cursor/Codex sind die gleichen.
Aider und Windsurf sind nicht im CodeGraph-Set, aber **genau so** implementierbar (siehe
Kapitel 7).

---

## 7. Übertragbarkeitsanalyse (C# / Roslyn / AiNetLinter)

### 7.1 Bewertungs-Matrix

| CodeGraph-Pattern | Aufwand für AiNetLinter | Begründung | Priorität |
|-------------------|------------------------|------------|-----------|
| **K1: Single-Tool-Doctrine** (1 DEFAULT-MCP-Tool) | **1:1** | Wir exposen z. B. nur `check` als Primary. Andere Tools opt-in. | MUST |
| **K2: Sufficiency-Output** (komplett in einer Antwort) | **1:1** | Bei Linter-Check: alle Violations + Auto-Fix-Hints + Edge-Context in einem Tool-Call. | MUST |
| **K3: File-View-Mode** (Read-Ersatz mit Blast-Radius) | **1:1** | `SyntaxTree.GetText().GetSubText(span).ToString()` + `dependents`-Liste. | MUST |
| **K4: Staleness-Banner** (⚠️ bei nicht-synced Files) | **1:1** | Letzte `index`-Hash merken, beim Read-Versuch warnen. | SHOULD |
| **K5: `isError: true` vermeiden** | **1:1** | Direkt übernehmen, doctrine-kritisch. | MUST |
| **K6: Single-Source-of-Truth** (server-instructions.ts) | **1:1** | Eine Datei für Agent-Guidance, **nicht** in jede Agent-Config duplizieren. | MUST |
| **K7: Adaptive Budgets** | **Anpassbar** | Linter-Output skaliert mit Solution-Größe, aber weniger dramatisch. | NICE |
| **K8: Framework-Aware Routes** | **Anpassbar** (eigentlich: **besser machbar!**) | Roslyn kann ASP.NET-Sachen **strukturell** parsen, nicht nur per Regex. | **MUST (größte Chance)** |
| **K9: Multi-Agent-Installer** | **1:1** | 8 Targets aus CodeGraph übernehmen + Windsurf/Aider/Continue/VisualStudioChatGPT ergänzen. | MUST |
| **K10: Rust-Kernel für 20 Sprachen** | **Nicht umsetzbar** (und unnötig) | Roslyn löst C# strukturell. tree-sitter für C# ist Overkill. | NICHT |
| **K11: Detached Daemon** | **Anpassbar** | In-process `BackgroundService` mit FileSystemWatcher + Mutex. Kein Lock-File nötig (Linter läuft eh pro Solution). | SHOULD |
| **K12: Dynamic-Dispatch-Synthesizer** | **1:1, viel besser** | Roslyn kann MediatR, Castle.DynamicProxy, Reflection.Emit, async-continuations, source-generators strukturell erfassen. | MUST |
| Per-Project-`.codegraph/`-Verzeichnis | **1:1 als `.ainetlinter/`** | Gleiches Pattern: Solution-Root + lokale DB/Cache. | SHOULD |
| Wal-Healing-Logic | **1:1 in C#** | `Microsoft.Data.Sqlite` hat dasselbe WAL-Issue. | SHOULD |
| Watcher mit FSEvents/inotify/RDCW | **1:1** | .NET 8 hat `FileSystemWatcher` + `System.Threading.Channels`. | 1:1 |
| `node:sqlite` als Embedded-DB | **1:1** | `Microsoft.Data.Sqlite` (offiziell, kein Native-Build). | 1:1 |
| FTS5 | **1:1** | SQLite FTS5 ist sprachunabhängig. | 1:1 |
| Segment-Vocab für NL-Query-Disambiguation | **Anpassbar** | PascalCase-Splitting in C# ist einfacher (Identifier-Segments sind trivial). | NICE |
| TOML-Hand-Writer | **1:1** | Brauchen wir für Codex. | 1:1 |
| Telemetry | **NICHT-ÜBERNEHMEN** | Privates OSS-Tool, kein SaaS-Produkt. Kein Bedarf. | NICHT |
| Beta-Signup / Waitlist | **NICHT-ÜBERNEHMEN** | Marketing. | NICHT |
| Uninstall / Multi-Format-Rollback | **1:1** | Beim Uninstall alle geschriebenen Files/Config-Entries rückgängig. | MUST |
| Per-Repo `codegraph status` Health-Check | **1:1 als `ainetlinter status`** | WAL-Size, File-Counts, Last-Index-Zeit, Pending-Sync. | SHOULD |
| Worktree-Detection (Git) | **1:1** | `LibGit2Sharp` für Worktree-Erkennung. | SHOULD |
| Per-File-Staleness-Banner | **1:1** | `contentHash` beim Read prüfen. | SHOULD |
| Per-Call-Annotations (`readOnlyHint`, etc.) | **1:1** | MCP-Spec 2025-Spec für Annotations. | MUST |
| `roots/list` Server-Request | **1:1** | Workspace-Root-Erkennung wie in `transport.ts:114`. | SHOULD |
| Bidirektionale MCP-Transports (stdio + socket) | **NICHT-ÜBERNEHMEN** | Linter ist per-Process; eine stdio-Session reicht. | NICHT |
| `codegraph_context` (entfernt) und `codegraph_trace` (entfernt) | **Lerneffekt** | "Fuzzy-Input-Tools" wurden entfernt, weil Agent sie schlecht wählte. | LEARN |

### 7.2 ASP.NET: Konkrete Roslyn-Übertragung

Der **ASP.NET-Resolver in CodeGraph** ist ein 220-Zeilen-Regex-Hack. Mit Roslyn:

| CodeGraph macht | Roslyn kann |
|------------------|-------------|
| Regex über `[HttpGet("path")]` | `AttributeSyntax` + `AttributeData` (semantisch) |
| Konvention `/Controllers/` | `IServiceCollection.AddControllers()` + `AddScoped<TInterface, TImpl>()` |
| `[Route("api/[controller]")]` als Prefix | `AttributeData.ConstructorArguments` |
| Minimal-API `MapGet("/path", handler)` | `IInvocationOperation` + `IMethodSymbol` (Handler-Type, anonyme Lambdas, Method-Group) |
| **NICHTS** für Middleware-Pipeline | **`app.Use*()`-Chain extrahierbar** via `SyntaxNode`-Sequenz in `Program.cs` |
| **NICHTS** für SignalR-Hubs | `[Hub]`-Klasse + `On<THub>`-Methoden via `INamedTypeSymbol` |
| **NICHTS** für gRPC | `[GrpcService]`-Attribut + Service-Contract-Interface |
| **NICHTS** für Authorization-Policies | `services.AddAuthorization(o => o.AddPolicy(...))` + `RequireClaim(...)` |
| **NICHTS** für Filters | `[TypeFilter]`, `[ServiceFilter]`, `IFilterMetadata` |
| **NICHTS** für Model-Binding | `[FromBody]`, `[FromQuery]`, `[FromRoute]`-Attributes |
| **NICHTS** für GraphQL (HotChocolate) | `[GraphQLName]`, `[Query]`, `[Mutation]` |

**Konkrete Linter-Regel(n)**, die daraus folgen:

1. **`AspNetControllerRouteAnalyzer`** – findet alle Controller-Actions, prüft ob Route-Attribut
   vorhanden, prüft ob Route-Konflikt mit anderen Actions.
2. **`MinimalApiEndpointAnalyzer`** – findet alle `MapGet/MapPost/...`-Aufrufe, prüft ob
   Handler-Methode existiert und async-signatur hat.
3. **`MiddlewarePipelineAnalyzer`** – extrahiert die Middleware-Chain aus `Program.cs`/`Startup.cs`
   und prüft Reihenfolge (UseAuthentication vor UseAuthorization vor MapControllers, etc.).
4. **`DependencyInjectionAnalyzer`** – findet `services.AddXxx<I,T>(...)`-Aufrufe, prüft dass
   `T : I`, warnt bei zirkulären Abhängigkeiten (graph-basiert!).
5. **`GrpcServiceAnalyzer`** – prüft dass `[GrpcService]`-Klassen das richtige
   Service-Contract-Interface implementieren, alle Methoden virtuals sind.
6. **`RouteConflictAnalyzer`** – findet zwei Actions mit identischer Route (basemap) **graph-basiert**.

### 7.3 Dynamic-Dispatch-Patterns die CodeGraph kann und wir auch brauchen

| Pattern | CodeGraph | AiNetLinter/Roslyn |
|---------|-----------|-------------------|
| MediatR `IRequestHandler<TReq, TRes>` | Immerhin in `__tests__/mediatr-dispatch-synthesizer.test.ts` | `INamedTypeSymbol.AllInterfaces` + Generic-Type-Argument-Analyse |
| Castle.DynamicProxy / DispatchProxy | Nicht implementiert | `DispatchProxy`-Subclass-Detection via `ITypeSymbol.BaseType` |
| System.Text.Json Source Generators | Nicht implementiert | `[JsonSerializable]`-Attribut-Analyse |
| EF Core ModelBuilder | Nicht implementiert | `IEntityTypeConfiguration<T>`-Pattern-Detection |
| AutoMapper `CreateMap<A, B>()` | Nicht implementiert | Profil-Klasse + Config-Call-Analyse |
| Source Generators (IIncrementalGenerator) | Nicht implementiert | `IIncrementalGenerator`-Implementation + `[Generator]`-Attribut |
| Microsoft.Extensions.Logging | Nicht implementiert | `ILogger<T>`-Injection + Konvention-Checks |

→ **AiNetLinter hat strukturellen Vorteil**: Roslyn sieht **mehr** als tree-sitter, also können
**mehr** Patterns zuverlässig erkannt werden.

---

## 8. Feature-Liste für AiNetLinter (priorisiert)

### 8.1 MUST-HAVE (direkt umsetzbar, hoher Mehrwert)

| # | Feature | Beschreibung | Pattern-Quelle |
|---|---------|--------------|----------------|
| M1 | **MCP-Server für AiNetLinter** | Stdio-MCP-Server mit `check` als Primary-Tool (Single-Tool-Doctrine). Input: Solution-Pfad, optionale Rule-Filter. Output: Violations + Auto-Fix-Hints + Symbol-Context. | K1, K2, K6, server-instructions.ts |
| M2 | **`isError: true` vermeiden** | Alle "Solution nicht gefunden", "Rule-Filter ergibt nichts", "Symbol existiert nicht" → success-shaped Text-Result. | K5 |
| M3 | **Multi-Agent-Installer** | 8 Targets aus CodeGraph (Claude, Cursor, Codex, opencode, Hermes, Gemini, Antigravity, Kiro) + 4 neue (Windsurf, Aider, Continue, VisualStudioChatGPT). Marker-basierte Idempotenz. | K9 |
| M4 | **Single Source of Truth für Agent-Guidance** | `src/mcp/server-instructions.ts`-Pattern, nicht dupliziert in jede Agent-Config. | K6 |
| M5 | **ASP.NET/Roslyn-basierte Analyzer-Suite** | Mindestens 6 Rules aus Kapitel 7.2 (Controller-Routes, Minimal-APIs, Middleware, DI, gRPC, Conflicts). | K8 + K12 |
| M6 | **File-View-Mode im `node`** | `node({file: "X.cs"})` ohne Symbol → Datei-Source + Violations in dieser Datei + Symbols-Overview. | K3 |
| M7 | **Staleness-Banner** | `⚠️ Diese Datei wurde nach dem letzten Index geändert.` wenn Hash-Mismatch. | K4 |
| M8 | **Uninstall mit Marker-basierter Idempotenz** | Sauberes Rollback über alle 12 Agent-Targets. | CodeGraph `installer` |
| M9 | **Adaptive Violations-Budget** | Mehr Code → mehr Violations im Output, aber **monoton** (kein Tier mit weniger Output). | K7 |
| M10 | **Per-Project `.ainetlinter/`-Verzeichnis** | Lokaler Cache + DB + Config, analog zu `.codegraph/`. | CodeGraph `directory.ts` |

### 8.2 SHOULD-HAVE (Klarer Mehrwert, mittlerer Aufwand)

| # | Feature | Beschreibung |
|---|---------|--------------|
| S1 | **Detached Linter-Service** | `BackgroundService` mit FileSystemWatcher (debounced) + Mutex. Verarbeitet geänderte Files automatisch. |
| S2 | **FTS5-basierte Symbol-Suche** | `search` für "finde alle Methoden die 'X' heißen" – in MCP und CLI. |
| S3 | **`impact`** | "Welche Regeln würden feuern, wenn ich Symbol X ändere?" – vor dem Edit. |
| S4 | **`callers` / `callees`** | Symbolische Aufrufer/Callee-Listen, Roslyn-basiert (keine Heuristik). |
| S5 | **Worktree-Detection** | `LibGit2Sharp` für Git-Worktree-Erkennung → pro Worktree eigene DB. |
| S6 | **Health-Status-Tool** | `status` – DB-Size, WAL-Size, Last-Index-Zeit, Pending-Files. |
| S7 | **TOML-Writer für Codex** | Hand-rolled (kein Dependency). |
| S8 | **WAL-Healing** | `Microsoft.Data.Sqlite`-WAL-Cleanup bei Process-Death. |
| S9 | **`roots/list` Server-Request** | Workspace-Root-Erkennung. |
| S10 | **Read-Annotationen** | `readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`. |

### 8.3 NICE-TO-HAVE (Zukunftsmusik)

| # | Feature | Beschreibung |
|---|---------|--------------|
| N1 | **Bidirektionale MCP-Transports** | Stdio + named-pipe für Multi-Session-Sharing. Komplexität rechtfertigt sich nur bei Multi-Agent-Use. |
| N2 | **Streaming Progress (MCP Progress-Notifications 2025)** | Lange `index`/`check`-Calls senden Progress-Events. |
| N3 | **Adaptive Solution-Loading** | `MSBuildWorkspace` vs. `AdhocWorkspace` je nach Solution-Komplexität. |
| N4 | **Source-Generator-Introspection** | Eigener Source-Generator der `node` automatisch exposed. |
| N5 | **Multi-Language-Support (F#, VB.NET)** | C# ist primär, aber Roslyn kann F#/VB.NET – wir könnten. |
| N6 | **Aider-/Continue-/Windsurf-Targets** | Aider: `~/.aider.conf.yml`; Continue: `~/.continue/config.json`; Windsurf: `~/.codeium/windsurf/mcp_config.json`. |
| N7 | **Context-Occupancy-Tracking** | Analog zu CodeGraph: "diese 6 Tool-Calls haben ~30k Tokens Context hinterlassen" – für User-Transparenz. |
| N8 | **Trockenlauf-Modus** | `fix --dry-run` zeigt Fixes, schreibt nicht. |
| N9 | **Solution-Snapshots** | `.ainetlinter/snapshots/<git-sha>.json` für reproduzierbare Check-Results über Commits. |
| N10 | **Graph-basierte Lint-Regeln** | "Wenn X geändert wird, müssen auch Y und Z manuell geprüft werden" – nutzt Roslyn-Symbol-Graph statt Heuristik. |

### 8.4 NICHT-ÜBERNEHMEN (mit Begründung)

| CodeGraph-Pattern | Warum nicht |
|-------------------|-------------|
| Rust-Kernel / tree-sitter-WASM | Roslyn löst C# strukturell besser, kein Bedarf. |
| Multi-Sprachen-Support (33 Sprachen) | AiNetLinter ist C#/Roslyn-only. |
| Telemetry / Beta-Signup / Waitlist | OSS-Tool, kein SaaS. Datenschutz. |
| Detached-Daemon mit Lock-File | Overkill für Linter (per-Solution, per-Process). |
| Multi-Session-Sharing | AiNetLinter-Runs sind isoliert. |
| Heuristic-Edge-Synthesizer (tree-sitter-Limitation) | Roslyn hat diese Limitation nicht. |
| WAL-Size-Monitoring (für geheilte Sessions) | In-process, kein Risiko. |
| Per-File-Staleness via Watcher-Diff | Mit MSBuild-Workspace + Hot-Reload eleganter. |
| `codegraph_context` (entfernt) und `codegraph_trace` (entfernt) | Lehre: zu fuzzy, Agent wählt schlecht. Wir starten bereits ohne. |
| `node-sqlite` mit `node:sqlite` ≥22.5 | `Microsoft.Data.Sqlite` (offiziell, .NET-nativ). |

---

## 9. Zusätzliche Insights (weiter gedacht)

### 9.1 Was CodeGraph **nicht** macht – was wir **besser** machen können

1. **Kein Streaming/Progress.** MCP-Spec 2025 hat Progress-Notifications. Für lange
   `codegraph init`/`ainetlinter check` auf großen Solutions wäre das **sehr wertvoll** –
   der Agent sieht sonst nur "still working" und kann nicht mit Stream-Token rechnen.

2. **Keine "structured output" für MCP-Tool-Responses** (außerhalb von `codegraph_status`).
   MCP-Spec 2025 erlaubt strukturierte Outputs, die der Agent direkt in Variablen weiterreichen
   kann. Wir könnten `check` mit `outputSchema` definieren, sodass der Agent
   strukturierte Violation-Objekte bekommt.

3. **Keine MCP-Resources.** MCP-Resources sind **passiv lesbarer** State (Files, DB-Content,
   Logs) im Gegensatz zu Tools (aktiv aufrufbar). CodeGraph nutzt nur Tools. Wir könnten z. B.
   `.ainetlinter/logs/latest.jsonl` als Resource exposen, die der Agent jederzeit lesen kann.

4. **Keine `sampling`-Nutzung.** MCP-Sampling erlaubt dem Server, den Client zu bitten, eine
   LLM-Completion auszuführen. Könnte für "Auto-Fix-Vorschlag generieren" genutzt werden –
   AiNetLinter hat das Wissen (Symbol + Violation), der LLM-Agent hat die Intelligenz (Fix
   formulieren).

5. **Keine "Beobachtbarkeit".** CodeGraph hat Telemetry; wir könnten stattdessen
   **OpenTelemetry-Traces** exposen, die der User in seinem Observability-Stack (Jaeger, Honeycomb)
   sehen kann.

6. **Kein "first-class symbol" für Aider.** Aider hat ein `--watch`-Feature, das mit
   Linter-Output umgehen könnte. Ein dedizierter Aider-Target mit Watcher-Integration wäre
   ein Differentiator.

### 9.2 C#-spezifische Patterns die CodeGraph **nicht** sieht, wir aber **billig** könnten

- **Nullable-Reference-Types** (`#nullable enable` + `?`-Suffixe) → null-safety-Lint-Regeln.
- **`record`** / **`record struct`** / **`readonly record struct`** → Immutability-Analysen.
- **`init`-only Setters** → "alle Felder müssen in `init`/`ctor` gesetzt sein".
- **`IAsyncEnumerable<T>`-Pattern** → "verwende `await foreach` statt `MoveNextAsync`".
- **Pattern Matching** (`is { X: var x }`) → Style-Regeln, Exhaustiveness für `switch`.
- **Target-Typed `new()`** → Konsistenz-Warnungen.
- **`global using`-Directiven** → Erkennung dass Symbole in `global using`-File deklariert sind.
- **File-scoped Namespaces** (`namespace Foo;`) → Konsistenz.
- **`required`-Modifier** (C# 11) → "alle `required` Properties müssen im Initializer gesetzt sein".
- **`nameof()`-Empfehlungen** statt Magic-Strings.
- **`Span<T>`/`ReadOnlySpan<T>`-Empfehlungen** für Hot-Path-Allocations.
- **Source-Generators** (`[Generator]`-Attribut) → Custom-Linting der Generator-Outputs.

### 9.3 State-of-the-Art agentic-coding Patterns 2026

| Pattern | Was CodeGraph macht | Was wir zusätzlich tun könnten |
|---------|---------------------|--------------------------------|
| **Streaming MCP** | Nicht | Progress-Notifications für lange Check-Calls |
| **MCP Resources** | Nicht | `.ainetlinter/`-State als Resource exposen |
| **Structured Output** | Nur `codegraph_status` | `check` mit `outputSchema` |
| **Tool Annotations** | `readOnlyHint` etc. | dito + `openWorldHint` für externe Tools |
| **Bidirektionale Transports** | stdio + named pipe | Nicht relevant (Linter single-process) |
| **Multi-Agent-Setup** | 8 Installer-Targets | 12 (mit Aider, Continue, Windsurf, etc.) |
| **Self-Healing Install** | Marker-basierte Idempotenz | dito |
| **Persistent Daemon** | Detached-Daemon mit Lock | `BackgroundService` in-process |
| **Auto-Sync** | FSEvents/inotify | `FileSystemWatcher` + debounce |
| **Server-Instructions SoT** | Ja | Ja |
| **`isError: true`-Vermeidung** | Ja | Ja |
| **MCP `sampling`** | Nicht | "Auto-Fix generieren" via Client-LLM |
| **MCP `elicitation`** | Nicht | "Soll der Fix angewendet werden?" – Confirmation-Request |

### 9.4 Häufige CodeGraph-Complaints (aus dem Repo selbst ableitbar)

Aus dem Issue-Index (abgeleitet aus CLAUDE.md-Verweisen):

- **#207** – Installer schrieb in `./.claude.json`, das Claude Code nicht liest. Fix: jetzt `./.mcp.json`. Lektion für uns: **frühe Installer-Bugs sind häufig und silent**.
- **#411** – Detached-Daemon mit Lock-File. Frühere In-Process-Daemon wurde vom ersten Host-SIGKILL mitgerissen.
- **#529** – Doppelte Instructions (`server-instructions.ts` UND `CLAUDE.md`-Block). Fix: Installations-Duplikat entfernt. Lektion für uns: **immer Single Source of Truth**.
- **#850** – Main-Thread-Wedge durch synchrone DB-Operationen. Fix: liveness-watchdog, lazy-loading, cooperative-yield.
- **#964** – Empty-`tools/list` Gate brach Monorepos. Fix: Tools werden immer exposed, mit `SERVER_INSTRUCTIONS_NO_ROOT_INDEX`-Variante.
- **#1185** – Launcher-Kill vor EARLY_PPID → startup-orphaned Server. Fix: `armStartupHandshakeTimeout`.
- **#1269** – Cleanup-Delete entfernte Sibling-Rows. Fix: `rowId`-Tracking.
- **#1431** – WAL-Leak durch SIGKILL'd Daemons. Fix: Watchdog + WAL-Healing.
- **#1500** – Generiertes Go-File neben Hand-Written: path-basierte Detection unzuverlässig. Fix: Generation-Banner-Parsing in File-Header.

**Wichtigste Lehren für AiNetLinter:**
1. **Stille Installer-Fehler sind die häufigsten Bug-Klasse** – früh testen, gegen `--print-config` validieren.
2. **Marker-basierte Idempotenz funktioniert besser als JSON-Deep-Equal** (für Instructions).
3. **WAL-Lifecycle bei Crash** ist real und braucht explizite Heilung.
4. **Multi-Language-Detection braucht mehr als Path-Pattern** (Inhalt-Signale).
5. **Main-Thread-Wedge durch zu lange Sync-Operationen** ist ein klassisches Problem – wir
   werden es auch haben (`MSBuildWorkspace.OpenSolutionAsync` kann minutenlang blockieren).

---

## 10. Top-5 konkrete Empfehlungen für AiNetLinter

| # | Empfehlung | Aufwand | Impact |
|---|-----------|---------|--------|
| **1** | **MCP-Server mit `check` als Primary-Tool** – Single-Tool-Doctrine, `isError: true` vermeiden, server-instructions.ts als Single-Source-of-Truth, `readOnlyHint: true` + `outputSchema` für strukturierte Violation-Objekte. | 2-3 Tage | 🚀🚀🚀 AI-Agent-Workflow wird sofort produktiv |
| **2** | **Multi-Agent-Installer für 12 Targets** – 8 aus CodeGraph (Claude, Cursor, Codex, opencode, Hermes, Gemini, Antigravity, Kiro) + 4 neue (Windsurf, Aider, Continue, VisualStudioChatGPT). Marker-basierte Idempotenz, TOML-Writer für Codex. | 4-5 Tage | 🚀🚀🚀 Onboarding-UX wie CodeGraph, breite Adoption |
| **3** | **Roslyn-basierte ASP.NET-Framework-Analyzer-Suite** – mindestens 6 Rules (Controller-Routes, Minimal-APIs, Middleware-Pipeline, DI-Graph, gRPC, Model-Binding). Strukturell statt textuell. | 1-2 Wochen | 🚀🚀🚀 Größte Differenzierung gegenüber CodeGraphs Regex-Approach |
| **4** | **File-View-Mode für `node`** – `node({file: "X.cs"})` ohne Symbol → Datei-Source mit Violations + Symbol-Map. Staleness-Banner bei Hash-Mismatch. | 2-3 Tage | 🚀🚀 LLM-Agent kann Code lesen ohne sein Built-in-Read zu nutzen, behält Violation-Kontext |
| **5** | **In-process BackgroundService mit FileSystemWatcher** – debounced (2s, clamp [100ms, 60s]), pro Solution, mit Mutex gegen Doppel-Runs. WAL-Healing bei Crash. | 1-2 Tage | 🚀🚀 Auto-Run bei File-Change – Killer-Feature für Live-Linting |

**Bonus-Tipp:** Wenn Zeit knapp ist, **Reihenfolge 1 → 2 → 4 → 3 → 5**. Mit den ersten drei
Punkten ist AiNetLinter **bereits als MCP-Server produktiv nutzbar** und vis-à-vis zu CodeGraph
**besser positioniert** für die .NET-Welt (strukturelle statt textuelle ASP.NET-Analyse,
Roslyn-Semantik statt tree-sitter).

---

## Anhang A: Wichtige Datei-Pfade im CodeGraph-Repo

| Was | Pfad |
|-----|------|
| MCP-Server-Lifecycle | `src/mcp/index.ts` |
| MCP-Server-Instructions (SoT) | `src/mcp/server-instructions.ts` |
| MCP-Tool-Definitionen + Handlers | `src/mcp/tools.ts` (314 KB) |
| MCP-Transports (stdio + socket) | `src/mcp/transport.ts` |
| Shared MCP-Engine | `src/mcp/engine.ts` |
| Detached Daemon | `src/mcp/daemon.ts` |
| Stdio-Socket-Proxy | `src/mcp/proxy.ts` |
| Public API (CodeGraph-Class) | `src/index.ts` |
| NodeKind/EdgeKind | `src/types.ts` |
| SQLite-Schema | `src/db/schema.sql` |
| SQL-Query-Builder | `src/db/queries.ts` |
| Reference-Resolver-Orchestrator | `src/resolution/index.ts` |
| ASP.NET-Resolver | `src/resolution/frameworks/csharp.ts` |
| C#-Extractor (tree-sitter) | `src/extraction/languages/csharp.ts` |
| C#-Port-Checklist (für Rust-Kernel) | `docs/design/csharp-kernel-port-checklist.md` |
| Dynamic-Dispatch-Coverage | `docs/design/dynamic-dispatch-coverage-playbook.md` |
| Adoption-Patterns (Hooks etc.) | `docs/design/agent-codegraph-adoption.md` |
| Sufficiency-Benchmarks | `docs/benchmarks/explore-sufficiency.md` |
| Call-Sequence-Analysis | `docs/benchmarks/call-sequence-analysis.md` |
| Agent-Eval Feedback-Metrics | `docs/benchmarks/agent-eval-feedback-metrics.md` |
| Installer-Orchestrator | `src/installer/index.ts` |
| Installer-Targets-Registry | `src/installer/targets/registry.ts` |
| Claude-Target (mit allen Lessons-Learned) | `src/installer/targets/claude.ts` |
| Instructions-Template (nur noch Marker) | `src/installer/instructions-template.ts` |
| TOML-Writer (für Codex) | `src/installer/targets/toml.ts` |
| Bin/CLI (alle Subcommands) | `src/bin/codegraph.ts` |
| `serve --mcp`-Mode | `src/bin/codegraph.ts:1741-1810` |
| Adaptive-Budget-Funktionen | `src/mcp/tools.ts:161-200` |

## Anhang B: Wichtige Issues (zur Einordnung)

- **#207** – Installer-Write-Location falsch
- **#411** – Detached-Daemon
- **#529** – Doppelte Instructions → SoT
- **#850** – Main-Thread-Wedge
- **#964** – Empty-`tools/list` Gate
- **#1185** – Startup-Handshake-Timeout
- **#1269** – Cleanup-Delete-Sibling-Bug
- **#1431** – WAL-Leak
- **#1500** – Generated-File-Detection
- **#277** – PPID-Watchdog (Linux)
- **#799** – POLLHUP-Busy-Spin
- **#196** – `roots/list`-Bidirektionalität

## Anhang C: Glossar

| Begriff | Bedeutung |
|---------|-----------|
| **MCP** | Model Context Protocol – offener Standard für AI-Agent ↔ Tool-Kommunikation |
| **JSON-RPC 2.0** | Transport-Format für MCP |
| **Knowledge Graph** | Graph aus Knoten (Symbole) + Kanten (Beziehungen) |
| **FTS5** | SQLite Full-Text-Search-Extension |
| **WAL** | Write-Ahead-Log (SQLite-Sidecar) |
| **MSBuildWorkspace** | Roslyn-Workspace der .sln/.csproj-Loading kapselt |
| **AdhocWorkspace** | Roslyn-Workspace ohne Project-Loading (nur SyntaxTrees) |
| **`provenance:'heuristic'`** | CodeGraph-Markierung dass Edge nicht aus AST-Syntax kam, sondern synthetisiert wurde |
| **Sufficiency** | CodeGraph-Doctrine: Output ist so komplett, dass Agent nicht mehr `Read` muss |
| **Single Source of Truth** | Eine Datei/Variable, die alle anderen referenzieren |
| **Marker-basierte Idempotenz** | Sections in User-Files durch `<!-- MARKER_START -->` / `<!-- MARKER_END -->` markiert, Updates nur innerhalb |
| **Detached Daemon** | Hintergrund-Prozess, vom Launcher-Prozess gelöst, eigenständig lebend |
| **PPID Watchdog** | Überwacht ob Parent-Prozess lebt; bei SIGKILL des Parents → Daemon beendet sich |
| **O_EXCL Lock** | Exklusive File-Lock-Erstellung als Singleton-Guard |

---

**Ende des Reports.**

**Reviewer-Hinweis:** Dieser Report extrahiert CodeGraph-Design-Patterns, die in AiNetLinter
übernommen werden können. **Keine Implementierung** in dieser Datei – die Tasks für die
tatsächliche Umsetzung müssen separat in `tasks/features/`-Issues definiert werden.
