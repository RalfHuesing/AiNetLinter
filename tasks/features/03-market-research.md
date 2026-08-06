# Marktrecherche: Roslyn-/C#-MCP-Server, Agentic Coding & Best Practices 2026

**Projekt:** AiNetLinter (Roslyn-basierter C# Linter)
**Datum:** 2026-08-06
**Sprache:** Deutsch
**Zweck:** Vorbereitung der MCP-Server-Aufwertung; Identifikation von Wertversprechen, Konkurrenzprodukten, Token-/Kosten-Optimierung und zukünftigen Trends.
**Quellen:** ~30 Primärquellen (Microsoft, Anthropic, GitHub, NuGet, MCP-Spec-Blog, Industrie-Reports), Stand 2026-Q1/Q2.

---

## Inhalt

1. [Phase 1 — MCP-Server für C#/.NET/Roslyn: Landschaft](#phase-1--mcp-server-für-cnetroslyn-landschaft)
2. [Phase 2 — Agentic Coding Patterns & MCP-Spec 2026](#phase-2--agentic-coding-patterns--mcp-spec-2026)
3. [Phase 3 — Token- und Kosten-Optimierung](#phase-3--token-und-kosten-optimierung)
4. [Phase 4 — Konkurrierende Projekte](#phase-4--konkurrierende-projekte)
5. [Phase 5 — Konkrete Features für unseren Roslyn-MCP-Server](#phase-5--konkrete-features-für-unseren-roslyn-mcp-server)
6. [Phase 6 — Zukunftstrends 2026/2027](#phase-6--zukunftstrends-20262027)
7. [Top 10 Features für unseren Roslyn-MCP-Server (Impact/Aufwand)](#top-10-features-für-unseren-roslyn-mcp-server-impactaufwand)
8. [Quellenverzeichnis](#quellenverzeichnis)

---

## Phase 1 — MCP-Server für C#/.NET/Roslyn: Landschaft

### 1.1 Überblick: Warum diese Nische gerade explodiert

MCP-Server (Model Context Protocol) sind seit 2024-Quartal 4 der Standardweg, AI-Coding-Agenten mit Live-Tooling zu versorgen. **Für C#/.NET existiert mittlerweile ein wilder Dschungel aus 10+ Roslyn-MCP-Servern**, keiner davon offiziell von Microsoft. Die meisten sind Solo-Open-Source-Projekte oder kommerzielle Anbieter, die sich um Standards, Reichweite und Qualität streiten.

**Wichtigste Erkenntnis:** Microsoft selbst hat (Stand 2026-Q2) noch keinen "offiziellen" C# Dev Kit MCP-Server veröffentlicht, liefert aber das offizielle **MCP C# SDK v2.0** (s. Phase 2) und integriert MCP in **Visual Studio 2026** und den **C# Dev Kit** für VS Code. Konkurrenzprodukte füllen also eine Lücke, die Microsoft noch nicht besetzt hat.

### 1.2 Existierende Roslyn-/C#-MCP-Server (Stand 2026-08)

| # | Projekt | Transport | Tool-Anzahl | Lizenz | Reife | Schwerpunkt |
|---|---------|-----------|-------------|--------|-------|-------------|
| 1 | **[egorpavlikhin/roslyn-mcp](https://github.com/egorpavlikhin/roslyn-mcp)** | stdio | 2 | MIT | 30⭐, frühes Stadium | Minimal: `ValidateFile`, `FindUsages` |
| 2 | **[carquiza/RoslynMCP](https://github.com/carquiza/RoslynMCP)** | stdio | ~5 | MIT | Frühes Stadium | Wildcard-Symbol-Suche, Dependency-Analyse, Cyclomatic Complexity |
| 3 | **[JoshuaRamirez/RoslynMcpServer](https://www.nuget.org/packages/RoslynMcp.Server)** (NuGet `RoslynMcp.Server`) | stdio | **41** | MIT | 8.2K Downloads, 0.4.0 (2026-02) | **Breiteste Toolbasis**: 19 Refactorings, 5 Navigation, 6 Metrics, 4 Generation, 7 Conversion; **Preview-Mode**; atomic writes mit Rollback |
| 4 | **[pzalutski-pixel/sharplens-mcp](https://github.com/pzalutski-pixel/sharplens-mcp)** (NuGet `SharpLensMcp`) | stdio | **67** | MIT | 31⭐, v1.5.3 (2026-05) | **Größte Toolzahl am Markt**, Partition via `RoslynService.*.cs` Partials (Navigation, Analysis, Refactoring, Inspection, Validation, TypeDiscovery, Compound, CallAnalysis, ExceptionFlow, StackTrace, ApiSurface, SimilarCode) |
| 5 | **[dotnet/roslyn – sailro/RoslynMcpExtension](https://github.com/sailro/RoslynMcpExtension)** (VS Marketplace) | Streamable HTTP + Legacy SSE | 7 | MIT | VS-Extension, nutzt `VisualStudioWorkspace` (Live-Diagnostics, unsaved changes) | `roslyn_validate_file`, `find_references`, `go_to_definition`, `get_document_symbols`, `search_symbols`, `find_dead_code`, `get_symbol_info` |
| 6 | **[YaroslavHorokhov.RoslynMCP](https://marketplace.visualstudio.com/items?itemName=YaroslavHorokhov.RoslynMCP)** (VS Marketplace) | stdio (per VS-Proxy) | k.A. | MIT | VS-Extension | Code analysis + debugging + UI-Automation, "give your AI assistant full control over Visual Studio" |
| 7 | **[dotnet-roslyn-mcp (vs-ide-mcp)](https://www.nuget.org/packages/dotnet-roslyn-mcp)** | stdio | **18+** | k.A. | 0.0.3 (Beta) | Impact analysis, safe refactoring, dead code detection, automated code fixes, batch ops, dependency visualization |
| 8 | **[remleo/AmazingMCP](https://mcpservers.org/servers/remleo/amazingmcp)** | Streamable HTTP | k.A. | k.A. | Frühes Stadium | Type search, dependency graphs, usage analysis, architecture overviews — "live in-memory compilation" |
| 9 | **[biegehydra/CSharpLangMCPServer](https://github.com/biegehydra/CSharpLangMCPServer)** | stdio (Port 8008) | k.A. | k.A. | Frühes Stadium | Erste VSCode-Extension-Variante; nur `find_usages` produktionsreif |
| 10 | **[SciSharp/Awesome-DotNET-MCP](https://github.com/SciSharp/Awesome-DotNET-MCP)** (Liste) | n/a | n/a | CC | Aktiv gepflegt | Kuratierte Liste mit 25+ .NET-MCP-Servern (Azure MCP, Revit, Unity, Rhino, AvaloniaUI, …) |

**Microsoft-eigene MCP-Assets** (für C#/.NET):
- **Offizielles MCP C# SDK v2.0** (`ModelContextProtocol` + `ModelContextProtocol.AspNetCore` + `ModelContextProtocol.Core` auf NuGet, Repo [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk))
- **Visual Studio 2026 (Release 2026-06-23)** mit integrierter **MCP-Trust-Validation** und **Azure MCP Server out-of-the-box**, **Copilot agents in VS** entdecken automatisch `skills/`
- **[microsoft/mcp-dotnet-samples](https://github.com/microsoft/mcp-dotnet-samples)**: offizielle Sample-Server (`Awesome Copilot`, `Markdown to HTML`, `Outlook Email`, `To-do List`)
- **[dotnet new mcpserver](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server)** Template (Microsoft.Extensions.AI.Templates, ab 9.10.0-preview.3)
- **C# Dev Kit** ([marketplace](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)) hat **kein eingebautes MCP-Server-Modul** (Stand 2026-Q2), liefert aber die Roslyn-Integration, auf der Dritt-Server aufsetzen
- **VS Code** mit kompletter MCP-Spec-Unterstützung (Resources, Prompts, Tools, Sampling, Authorization, MCP Apps)

### 1.3 Was die Konkurrenz gut macht — und was nicht

**Gemeinsame Schwächen der existierenden Roslyn-MCP-Server:**

1. **Keiner hat ein klares Token-Budget-Design.** Die Tools liefern rohe Listen ("alle 47 Dateien", "alle 142 Verweise") und vertrauen darauf, dass der Agent filtert — das ist der Hauptkostentreiber.
2. **Workspace-Bootstrap ist fragil.** Die meisten laden die Solution per `MSBuildWorkspace.OpenSolutionAsync()` — das ist 5–30 Sekunden pro Cold-Start, ohne Caching oder Persistenz. Die VS-Extension von sailro umgeht das durch Nutzung der laufenden VS-Workspace, ist aber an VS gebunden.
3. **Keiner hat Pattern-Detection auf Solution-Ebene** (z. B. "alle God-Classes", "alle async-void", "alle Public-API-Klassen ohne XML-Doc") als fertiges MCP-Tool. Aider löst das über `repo-map` (siehe Phase 4), aber nur für Symbole, nicht für Patterns.
4. **Preview/Atomic-Write-Support** ist selten. JoshuaRamirez/RoslynMcpServer ist hier positiv hervorzuheben (Rollback bei Fehlern).
5. **Multi-Solution / Multi-Repo** wird von keinem Server nativ unterstützt.
6. **Keiner hat `elicitation`-Workflows**, obwohl MCP-Spec 2025-06-18 es erlaubt.

**Was die Konkurrenz richtig macht:**

- **`pzalutski-pixel/sharplens-mcp`**: Riesige Tool-Breite (67 Tools), konsequente Test-Discipline (Testing Charter C1–C9), `suggestedNextTools` für Tool-Chaining, dediziertes `Audit`-Composite für Multi-Tool-Reports.
- **`JoshuaRamirez/RoslynMcpServer`**: Saubere Refactoring-Tool-Suite mit Preview-Mode, Cross-Platform (Windows/Linux/macOS), Single-Command-Install via NuGet, dedizierte CLI-Companion.
- **`sailro/RoslynMcpExtension`**: Nutzt die `VisualStudioWorkspace` — unsaved changes, live diagnostics, kein Cold-Start. Bestes Latenz-Profil überhaupt, aber VS-only.
- **`vs-ide-mcp`**: Saubere Dokumentation, explizite ENV-Variablen (`DOTNET_SOLUTION_PATH`, `ROSLYN_LOG_LEVEL`, `ROSLYN_MAX_DIAGNOSTICS`), guter Onboarding-Flow.

### 1.4 Lücke, die AiNetLinter füllen kann

AiNetLinter hat gegenüber der Konkurrenz **strukturelle Vorteile**, die aktuell kein Server vereint:

- **Linter-Hintergrund**: Eigene Roslyn-basierte Analyseregeln → reichhaltige `Diagnostic`-Quellen jenseits von Stock-Analyzern (z. B. AiNetLinter-spezifische Code-Smells, God-Class-Detection, Layer-Violations)
- **Skeletons & Playbooks**: Existierende Generatoren in `src/AiNetLinter/Generators/` → kontextuelle Bundles ohne zusätzlichen Tool-Aufwand
- **Konfigurierbare Regeln** (`rules.json`): MCP-Tool kann gezielt nach aktiver Regel-Filterung fragen

**Empfehlung Phase 1:** *Differenzierung über "Quality Contract"-Pattern* (siehe Phase 5 + CodeScene-Lessons, Phase 4). Wir liefern nicht "noch mehr Roslyn-Tools", sondern einen **selbst-korrigierenden Loop** mit Token-Discipline.

---

## Phase 2 — Agentic Coding Patterns & MCP-Spec 2026

### 2.1 Drei MCP-Primitives (Server-Sicht)

| Primitiv | Richtung | Zweck | Beispiel |
|----------|----------|-------|----------|
| **Tools** | Client → Server | Ausführbare Funktionen | `find_references`, `apply_refactor`, `run_diagnostics` |
| **Resources** | Client → Server | Read-only Kontext (URI-basiert) | `roslyn://solution/symbols`, `roslyn://file/diagnostics` |
| **Prompts** | Client → Server | Wiederverwendbare Workflow-Templates (Slash-Commands) | `/ai-net-linter.audit-security`, `/ai-net-linter.preview-refactor` |

**Wichtige Klärung:** Tools sind *executable* (mit Side-Effects, ggf. Mutations), Resources sind *read-only* (Kontext für den Agent). Das ist die wichtigste Design-Entscheidung, die man pro Endpoint treffen muss. ([dev.to/thedailyagent](https://dev.to/thedailyagent/building-production-grade-ai-agents-with-mcp-a-complete-guide-for-2026-3bo2))

### 2.2 Client-Primitive: Elicitation (sicherheitskritisch)

**Elicitation** (`elicitation/create`) lässt den Server den User um strukturierte Eingabe bitten — entweder per Form-Mode oder URL-Mode. Form-Mode ist **für Credentials verboten** (MUST NOT), URL-Mode leitet auf vertrauenswürdige externe Seiten um (z. B. OAuth).

**Anwendungsfall für AiNetLinter:** Server entdeckt, dass der Workspace-Build fehlschlägt → elicitiert "Soll ich versuchen, Solution-File zu reparieren? [Y/N]". Oder: Server braucht explizite Bestätigung vor Refactoring mit hoher Impact-Klasse. ([mcginniscommawill.com](https://mcginniscommawill.com/posts/2026-03-25-mcp-sampling-elicitation-guide/))

### 2.3 Client-Primitive: Sampling (rekursive Agenten)

**Sampling** (`sampling/createMessage`) lässt einen MCP-Server das **LLM des Hosts** um einen Completion bitten. Der Server braucht keine eigenen API-Keys. Human-in-the-loop ist garantiert (Client kann ablehnen, editieren, Model wählen).

**Anwendungsfall für AiNetLinter:** Server extrahiert einen Method-Body, der komplex ist → ruft via Sampling das Client-LLM auf, um eine natürlichsprachliche "Was-die-Methode-tut"-Zusammenfassung zu generieren. Diese Summary landet dann als Annotation, ohne dass der Server Tokens für LLM-Generierung zahlt. (Deprecation 2026-07-28: siehe 2.5.)

### 2.4 Transport: stdio vs Streamable HTTP

**Stand 2026-05** ist die Lage eindeutig: ([apigene.ai](https://apigene.ai/blog/mcp-sse-vs-stdio))

| Transport | Status | Wann nutzen |
|-----------|--------|-------------|
| **stdio** | ✅ current, **Standard für lokale Tools** | Dev-Laptop, CLI-Agents, Desktop-AI |
| **HTTP+SSE** | ⚠️ **deprecated** seit 2025-03-26 | – |
| **Streamable HTTP** | ✅ **current**, **Standard für Remote/Cloud** | Container, Enterprise, Multi-Client, Cloud-Deployments, ChatGPT-Integration, Claude.ai Custom Connector |

**Regel:** *STDIO für lokal, Streamable HTTP für Production.* ([note.com/ayato_studio](https://note.com/ayato_studio/n/n61c1ccefbab4?hl=en-US))

**Streamable HTTP — Details:**
- **Single Endpoint** (`POST /mcp`) statt zwei Endpoints
- Antwort als normaler HTTP-Body ODER SSE-Stream-Upgrade für lange Calls
- **Resumable**, **Stateless-Mode** für horizontale Skalierung
- Standard HTTP-Auth, OAuth 2.1 built-in
- Konkret: VS Code MCP-Config nutzt `"type": "http"`, `"url": "http://localhost:PORT/mcp"`

**AiNetLinter-Implikation:** Erste Version **STDIO** für Dev-Loop, **Streamable HTTP** als zweite Variante für Enterprise/CI/CD-Integration.

### 2.5 MCP-Spec 2026-07-28 (RC) — die wichtigsten Änderungen

Das ist **die größte MCP-Revision seit Launch.** ([modelcontextprotocol.io](https://modelcontextprotocol.io/specification/2026-07-28), [blog.modelcontextprotocol.io](https://blog.modelcontextprotocol.io/posts/2026-07-28/))

| Änderung | Bedeutung für AiNetLinter |
|----------|--------------------------|
| **Stateless protocol core** | `initialize`/`initialized` entfällt, `Mcp-Session-Id` entfällt → Server können hinter Load-Balancern liegen; **kein Hot-Workspace-State auf Server-Seite nötig** |
| **Multi Round-Trip Requests (MRTR, SEP-2322)** | `InputRequiredResult` ersetzt server-initiierte Sampling/Elicitation. Server kann innerhalb eines aktiven Calls User-Input anfordern, ohne offene Streams |
| **Method-/Name-Header** (`Mcp-Method`, `Mcp-Name`) | Gateways können auf Headern routen, statt JSON-Bodies zu parsen — **wichtig für Enterprise-Proxies** |
| **Cache-Hints in `list`-Responses** (`ttlMs`, `cacheScope`, SEP-2549) | `tools/list`, `prompts/list`, `resources/list` carry Cache-Hints → Clients cachen Tool-Kataloge |
| **OAuth 2.0 + OpenID Connect hardening** | Resource-Indicators mandatory, **AI-agents müssen via RFC 7591 (DCR) registriert werden** |
| **MCP Apps (offizielles Extension, SEP-1737)** | Server kann interaktive UI-Komponenten zurückgeben, die im Client-Conversation-UI rendern (Dashboards, Forms, Visualisierungen) — VS Code unterstützt das als erstes ([code.visualstudio.com](https://code.visualstudio.com/blogs/2026/01/26/mcp-apps-support)) |
| **Tasks-Extension** (out of core) | Langlebige Tasks mit `input_required` und `completed`-States für Agent-zu-Agent-Delegation |
| **Extensions-Framework** | Reverse-DNS IDs, unabhängige Versionierung → Drittanbieter-Erweiterungen offiziell unterstützt |
| **Deprecations** (12-Monats-Fenster): Roots, Sampling, Logging | Migrations-Pfad: Tool-Parameter, direkte Provider-APIs, stderr/OpenTelemetry |
| **Structured tool output** (vollen JSON Schema 2020-12) | `outputSchema` ist jetzt unrestricted, `structuredContent` darf jeder JSON-Wert sein — **striktere Tool-Verträge, weniger Parsing-Bugs** |

**AiNetLinter-Implikation:**
- Sofort auf **MCP C# SDK v2.0** wechseln (implementiert 2026-07-28-Spec)
- `Mcp-Session-Id` **nicht** mehr als Auth-Hint nutzen
- Bei Tool-Outputs: **structuredContent** nutzen, nicht freie Texte
- **MCP Apps-Extension** evaluieren (z. B. interaktive Diff-Vorschau für Refactorings)
- **Output-Cache-Hints** (`ttlMs`) für teure Workspace-Lookups liefern

### 2.6 Security-Anforderungen 2026

OAuth 2.1 ist **mandatory** für Remote-Server. ([modelcontextprotocol.io](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations), [redhat.com](https://www.redhat.com/en/blog/mcp-security-implementing-robust-authentication-and-authorization))

- **PKCE mandatory** für Public Clients
- **Token-Passthrough verboten** (Server darf empfangene Tokens nicht 1:1 an Upstream-APIs weiterreichen — Confused-Deputy-Vermeidung)
- **Audience-Claim prüfen** (Resource-Indicator, RFC 8707)
- **Kurze Token-Lifetimes** (max. 1h), Refresh-Token-Rotation
- **HTTPS enforced**, TLS 1.2+ Minimum
- **Tool-Level Scopes**, nicht nur Server-Level (Least Privilege)
- **Tool-Description-Signing**: Server-Manifests kryptographisch signieren, Client verifiziert (Anti-Prompt-Injection)
- **Version-Pinning** von MCP-Server-Konfigurationen (kein silent upgrade)

**AiNetLinter:** Lokales stdio-Setup benötigt keine OAuth; sobald Streamable-HTTP-Mode, dann OAuth 2.1 mit Azure AD / Entra ID (siehe Microsoft ACA-Sample, [devblogs.microsoft.com/ise](https://devblogs.microsoft.com/ise/aca-secure-mcp-server-oauth21-azure-ad/)).

### 2.7 "Tool-First"-Designprinzipien (Stand 2026)

Aus den Best-Practice-Reports: ([zenml.io](https://www.zenml.io/llmops-database/best-practices-for-building-production-grade-mcp-servers-for-ai-agents), [dev.to/thedailyagent](https://dev.to/thedailyagent/building-production-grade-ai-agents-with-mcp-a-complete-guide-for-2026-3bo2), [speakeasy.com](https://www.speakeasy.com/blog/how-we-reduced-token-usage-by-100x-dynamic-toolsets-v2/))

1. **Outcome-oriented, not operation-oriented.** "One tool = one agent story." Statt `get_user(id) + update_user(id, …) + send_email(id, …)` ein einzelnes `onboard_user(...)`.
2. **Flache, primitive Argumente.** Kein `config` Dictionary. Lieber Top-Level-Parameter: `email: string`, `includeCancelled: bool`, `status: "active"|"pending"`.
3. **Concise Tool-Descriptions.** Ziel: <100 Token pro Tool, eine Zeile, "When to use" statt "What it does".
4. **Schema-Trennung von Beispielen.** Beispiele sind implizite Verträge — sparsam, klar abgegrenzt.
5. **Tool-Curation: 5–15 ideal, max. 50 pro Server.** Wenn mehr nötig → mehrere Server mit Namespaces.
6. **ReadOnly/Destructive/DestructiveHints** als MCP-Annotations explizit setzen.
7. **Error-Messages sind Teil des Prompts.** Strukturierte Errors, klare Recovery-Pfade.
8. **Token-Budget als härteste Constraint behandeln.** Discovery passiert bei JEDEM Connect, ist teuer. Pro Tool 500–1.500 Tokens bei Standard-Schema.

---

## Phase 3 — Token- und Kosten-Optimierung

### 3.1 Warum das DIE Kernherausforderung ist

Bei 200K-Token-Context und mehreren konkurrierenden MCP-Servern (Filesystem, Git, Roslyn, CI, Test, …) **dominiert der MCP-Overhead oft den Großteil der Rechnung.** Anthropic-Report (2026): "Engineers are now able to fully delegate only 0–20% of tasks" — der Rest ist Tool-Discovery, falsche Aufrufe, Iterations-Loops. ([pathmode.io](https://pathmode.io/blog/orchestration-era-needs-intent))

**Kernzahl:** Traditionell 33 Tools im Context ≈ **8.000 Tokens nur für Tool-Schemas**.

### 3.2 Strategie 1: Progressive Disclosure (95%+ Token-Reduktion)

**Pattern** ([solo.io](https://www.solo.io/blog/mcp-progressive-disclosure), [matthewkruczek.ai](https://matthewkruczek.ai/blog/progressive-disclosure-mcp-servers.html)):

| Ebene | Inhalt | Token-Budget |
|-------|--------|--------------|
| **Index** | Tool-Namen + 1-Satz-Description | <100 Tokens pro Tool |
| **Detail** | Volle Input-Schemas mit Param-Doku | erst on-demand geladen |
| **Deep** | Beispiele, Edge-Cases, Error-Handling, related Tools | erst on-demand geladen |

**Implementierung:** Statt 33 Tool-Schemas upfront, **2 Meta-Tools** registrieren:
- `discover_tools(query: string)` → liefert Index-Liste
- `invoke_tool(name, args)` → führt aus

**Empirische Belege:**

| Quelle | Token-Reduktion | Methode |
|--------|-----------------|---------|
| Speakeasy ([speakeasy.com](https://www.speakeasy.com/blog/how-we-reduced-token-usage-by-100x-dynamic-toolsets-v2/)) | **96% Input, 90% total** | Dynamic Toolset v2 + tag-basierte Filter |
| Solo.io Agentgateway | **91,1%** (10.877 → 970 Tokens) | Progressive Disclosure mit `toolMode: "Search"` |
| Synaptic Labs Meta-Tool | **85–95%** (8.000 → 600 + 150/Call) | 2 Meta-Tools statt 33 Schemas |
| mcp-code-execution-enhanced | **98,7–99,6%** | Code-Execution statt direktem Tool-Call |

**AiNetLinter-Implikation:**
- **Phase 1**: Alle 67+ SharpLens-Tools sind ein Anti-Pattern. **Curation auf 8–12 Tools** ist richtig.
- **Phase 2**: Progressive-Disclosure-Pattern als zweite Schicht: "Roslyn MCP" mit 3 Meta-Tools + dynamisch ladbare Detaillisten.

### 3.3 Strategie 2: Compact Output / Token-effiziente Antworten

Aus [mindstudio.ai](https://www.mindstudio.ai/blog/optimize-mcp-server-token-usage) und [reddit r/mcp](https://www.reddit.com/r/mcp/comments/1p0py33/3_tips_to_make_mcp_servers_token_efficient/):

1. **Nur angefragte Felder zurückgeben** (Field-Selection / Projection)
2. **Kompakte Formate** statt verbose: `status: "ok"` statt `status: "Operation completed successfully"`
3. **Codes statt Prose** (z. B. `severity: "E"` statt `severity: "Error"`)
4. **Server-seitig vorverdauen** — keine rohen Logs/Datasets, sondern Summary
5. **Null-Field-Compression** — leere Felder weglassen
6. **TOON** (Tabular Object Oriented Notation) statt JSON — typischerweise 50% weniger Tokens
7. **Pagination mandatory** — Default 10–20 Items, `limit` + `offset` Parameter
8. **Deduplizierung** — gleiche Daten in zwei Calls → zweiter Call liefert nur Reference

**AiNetLinter-Implikation:**
- `find_references` darf **nicht** alle Usages als Volltext liefern. Stattdessen: `{ file, line, column, kind: "call"|"decl"|"ref" }` und `includeSnippet: bool` als Parameter.
- `get_diagnostics` muss `severityFilter` (Error/Warning/Info), `limit`, `offset` haben.
- `analyze_complexity` → numerische Werte + Score, **kein Code-Dump**.

### 3.4 Strategie 3: Diff statt Full File

Bei jeder Refactoring-Operation: **Structured Diff** (Structured-Patch-Format), nicht "alter Code + neuer Code" als zwei Vollstrings. Standardformate:
- Unified Diff (klein, etabliert)
- JSON-Patch (RFC 6902)
- Custom Aider-Style-Edit-Blocks (`<<<<<<< SEARCH / ======= / >>>>>>> REPLACE`)

**Aider-Beweis:** Aider verwendet **edit-block** als primäres Edit-Format. Bei jedem Tool-Call wird der diff-Anteil zwischen altem und neuem Zustand in Tokens geschätzt kleiner 1K. ([aider.chat/docs/repomap](https://aider.chat/docs/repomap.html))

### 3.5 Strategie 4: Skeleton/Map statt Raw Code

Das ist **das wichtigste Pattern aus Aider** (Phase 4): Ein **Repo-Map** mit Symbolen + Signaturen statt Source-Code. **Token-Budget: 1.024 Tokens (default), 0–4.096 möglich.** Bei 100.000 Zeilen Monorepo werden nur 50–100 relevanteste Symbole in den Context geladen.

Für Roslyn-Linter:
- `get_solution_map()` liefert **nur** Klassennamen, Methodensignaturen, Namespace-Hierarchie
- `get_class_skeleton(symbol_id)` liefert **nur** Methodennamen + Modifiers + Param-Count
- **Volltext kommt nie** in den Default-Path

### 3.6 Strategie 5: "Sufficiency" / Wann hört der Agent auf?

Pattern aus [zenml.io](https://www.zenml.io/llmops-database/best-practices-for-building-production-grade-mcp-servers-for-ai-agents): **"a single tool = a complete agent story"**. Wenn der Agent eine Refactoring-Anfrage stellt, soll EIN Tool-Aufruf genügen, der die ganze Antwort liefert. Wenn der Agent "iterieren muss, um die Antwort zu bekommen", ist das Tool-Design falsch.

Konkrete Heuristik: **Wenn der Agent typischerweise 2+ Calls für eine User-Story braucht → Tool zusammenlegen.**

### 3.7 Konkrete Token-Budget-Zahlen für AiNetLinter-Design

| Tool | Output-Größe (max) | Default-Page-Size |
|------|---------------------|-------------------|
| `find_references` | 200 Items default, hard-cap 1000 | 50 |
| `get_diagnostics` | 200 Items default, hard-cap 2000 | 100 |
| `get_solution_map` | Token-budget-parameter (default 1k tokens, max 4k) | n/a |
| `get_class_skeleton` | 1 File, max 100 Methoden | 50 |
| `analyze_complexity` | 1 Method/Call | n/a |
| `preview_refactor` | Unified-Diff, kein vollständiger Code | n/a |
| `apply_refactor` | Confirmation + Reference zum Diff | n/a |

---

## Phase 4 — Konkurrierende Projekte

### 4.1 mcp-server-tree-sitter (wrale) — language-agnostic Code-Intelligence

**Repo:** [github.com/wrale/mcp-server-tree-sitter](https://github.com/wrale/mcp-server-tree-sitter), [DeepWiki](https://deepwiki.com/wrale/mcp-server-tree-sitter/1-overview)

**Was es tut:** Tree-Sitter-basierte AST-Analyse für **66+ Sprachen**, structure-aware Code-Suche.

**Tool-Kategorien:**

| Kategorie | Tools |
|-----------|-------|
| Project Management | `register_project_tool`, `list_projects_tool`, `remove_project_tool` |
| Language Management | `list_languages`, `check_language_available` |
| File Operations | `list_files`, `get_file`, `get_file_metadata` |
| AST Analysis | `get_ast`, `get_node_at_position` |
| Code Search | `find_text`, `run_query` |
| Symbol Extraction | `get_symbols`, `find_usage` |
| Project Analysis | `analyze_project`, `get_dependencies`, `analyze_complexity` |
| Cache Management | `clear_cache` |

**Lehren für AiNetLinter:**
- **State-Persistence zwischen Calls** (Project-Registry, Parse-Tree-Cache) — Roslyn-Workspace sollte nach Solution-Load gehalten und nur bei explizitem `reload` invalidiert werden.
- **Tree-Sitter-Pattern "Symbols ohne Source-Code"** lässt sich 1:1 mit Roslyn-Symbols umsetzen (`GetDeclaredSymbols` ohne `GetText()`).
- **Dependencies als First-Class-Tool** ist ein klarer Winner.

**Variante:** [NightTrek/treesitter-mcp](https://github.com/NightTrek/treesitter-mcp) — initialisiert mit `initialize_treesitter_context(languages)`, dann `list_code_elements_by_kind`. Token-effizienter.

### 4.2 Codebase-Memory — Knowledge-Graph-Ansatz

**Repo:** [arxiv.org/html/2603.27277v1](https://arxiv.org/html/2603.27277v1)

**Architektur:**
- 3-Stage-Pipeline: Parse (Tree-Sitter, 66 Sprachen) → Build (Louvain Community Detection, Call-Graph-Traversal) → Serve (SQLite, **14 typed structural query tools**)
- **Sub-millisecond Query-Latency**
- Inkrementelle Index-Updates via File-Watching + Content-Hash

**Tool-Beispiele:** Call-Path-Tracing, Impact-Analysis, Hub-Detection.

**Lehren für AiNetLinter:**
- **Pre-built Index** als SQL/Embedded-Store (SQLite, DuckDB) ist ein klarer Performance-Win gegenüber MSBuildWorkspace-Reload
- **14 typisierte Tools > 67 generische**
- **Community-Detection / Modul-Erkennung** als Built-in → God-Class-Detection, Layer-Violation, High-Cohesion-Clusters

### 4.3 Aider — der Repo-Map-Erfinder

**Repo:** [github.com/aider-ai/aider](https://github.com/aider-ai/aider), Doku: [aider.chat/docs/repomap](https://aider.chat/docs/repomap.html), Deep-Dive: [codeintel.xyz](https://codeintel.xyz/blog/aider-architecture-tool-review-2026/)

**Schlüssel-Innovationen:**

1. **Repo-Map** (Tree-Sitter + PageRank):
   - Parst jede Source-Datei mit Tree-Sitter
   - Extrahiert Symbole (Klassen, Funktionen, Imports, Type-Decls)
   - **PageRank-Ranking** über File-Dependency-Graph
   - Komprimiert auf Token-Budget (default **1.024 Tokens**)
   - Für 100K LOC Monorepo: ~50–100 Top-Symbole im Context

2. **Architect/Editor-Mode**:
   - **Architect** (großes Modell, z. B. Claude Opus 4): high-level Plan in Prosa
   - **Editor** (kleines Modell, z. B. GPT-4o-mini): übersetzt Prosa in Code-Edits
   - **Token-Effizienz: 4,2× weniger Tokens** als äquivalente Claude-Code-Edits (NxCode-Analyse 2026-03)

3. **Git-Native**: Jeder Edit wird auto-committet. Context-Window-Boundaries = Commit-Boundaries.

4. **Edit-Modi**: `diff`, `whole-file`, `udiff`, `architect` — Risk-to-Task-Mapping.

**MCP-Integration:** Aider hat **keine native MCP-Integration** ([toolhalla.ai](https://toolhalla.ai/blog/aider-vs-continue-dev-vs-cody-2026)). Das ist eine **bewusste Design-Entscheidung** — Aider ist ein in sich geschlossenes Tooling-System.

**Lehren für AiNetLinter:**
- **Repo-Map-Pattern portieren** (Roslyn-spezifisch, mit PageRank auf File-Dependencies)
- **Architect/Editor-Split als Two-Stage-Tool**: `plan_refactor` (großes LLM via Sampling) → `apply_plan` (kleines LLM)
- **Token-Budget hard-cap** für jede Tool-Response

### 4.4 Continue.dev — Open-Source-Chat-IDE-Extension

**Doku:** [docs.continue.dev/customize/deep-dives/mcp](https://docs.continue.dev/customize/deep-dives/mcp)

**Stack:** VS Code + JetBrains Extension, Apache 2.0, BYO-LLM, **vollständige MCP-Unterstützung** (Resources, Prompts, Tools — kein Sampling, keine Roots).

**MCP-Config-Format:** `.continue/mcpServers/*.yaml` mit `command`, `args`, `type: stdio|sse|streamable-http`. JSON-Files aus Claude/Cursor werden direkt geparst.

**Lehren:** **MCP-Server-Multiplizität** — Continue-Nutzer erwarten, dass 3–8 MCP-Server parallel laufen. AiNetLinter-Server muss mit **anderen C#-MCP-Servern koexistieren** (z. B. Filesystem-MCP, Git-MCP, Test-MCP). Konflikte bei Overlapping-Tool-Names sind zu vermeiden → **Namespace-Präfix** (`roslyn_*`).

### 4.5 Sourcegraph Cody — Code-Graph als Differentiation

**Übersicht:** [toolhalla.ai](https://toolhalla.ai/blog/aider-vs-continue-dev-vs-cody-2026), [itsdeep.io](https://itsdeep.io/compare/cody-sourcegraph-vs-continue-dev)

**Was es einzigartig macht:** Sourcegraph's **Code-Graph** — indiziert **alle Repos, Branches, Files** einer Organisation. Cody's MCP-Support ist limitiert (nur Resources via OpenCTX).

**Use-Case:** "Which services consume this API?" über Microservice-Grenzen hinweg — Aider, Continue, AiNetLinter können das nicht.

**Lehren:** **Cross-Repo-Indexing** ist ein Markt-Lücke. AiNetLinter könnte "Lightweight Cross-Repo-Lookup" anbieten, ohne Sourcegraph-Investment.

### 4.6 CodeScene CodeHealth-MCP — das "Quality Contract"-Pattern

**Repo:** [github.com/codescene-oss/codescene-mcp-server](https://github.com/codescene-oss/codescene-mcp-server), [codescene.com/product/code-health-mcp](https://codescene.com/product/code-health-mcp)

**Konzept:** **CodeHealth Score (1–10)** als objective, deterministische Metrik — AI-Agent bekommt **klare Quality-Threshold** und self-correcting Loop.

**Tool-Set:**
- `code_health_review` — Continuous, während Code generiert wird
- `pre_commit_code_health_safeguard` — Auf staged/unstaged Files vor Commit
- `analyze_change_set` — Full branch vs base ref check (PR pre-flight)
- `code_health_score` — Numerischer Wert 1–10
- `code_health_auto_refactor` — Falls ACE verfügbar

**Wirkung:** "Without structural guidance, frontier LLMs only fix ~20% of code health issues. With MCP-augmented CodeHealth data, fix rates reach 90–100%." ([codescene.com](https://codescene.com/product/code-health-mcp))

**Lehren für AiNetLinter:** Wir haben bereits ein **Linter-Regelwerk** (`rules.json`). Das ist **unser CodeHealth-Score-Äquivalent**. Wir können:
- **`check`**: Deterministic-Check mit Schwellwert, der Agent in self-correcting loop zwingt
- **`score`**: Numerischer Score (Code-Health-0–10-Mapping)
- **`safeguard`**: Pre-commit-Gate

Das ist **differenzierend gegenüber allen anderen Roslyn-MCP-Servern**, die "nur Daten liefern".

### 4.7 Zusammenfassung Konkurrenzlandschaft

| Konkurrent | Lücke / Lektion für AiNetLinter |
|------------|----------------------------------|
| **SharpLens (67 Tools)** | Zu viele Tools → wir machen **Curation + Progressive Disclosure** |
| **RoslynMcpServer (41 Tools, MIT)** | Solide Refactorings, aber kein Quality-Contract-Pattern |
| **vs-ide-mcp (18+ Tools)** | Kein Pattern-Detection, kein Code-Health |
| **RoslynMcpExtension (VS-only)** | Nicht plattformneutral |
| **mcp-server-tree-sitter** | Kein C#-spezifisches Wissen, aber Pattern "Symbols ohne Source" übernehmen |
| **Codebase-Memory** | Pre-built Index + Community-Detection als Performance-Pattern |
| **Aider** | Repo-Map + Architect/Editor-Split, kein MCP-Support → wir liefern das via MCP |
| **Continue.dev** | Multi-MCP-Koexistenz → Namespace-Konventionen |
| **Cody** | Cross-Repo-Indexing als Markt-Lücke |
| **CodeScene** | **Quality-Contract-Pattern** (self-correcting loop) → **unser Haupt-Differentiator** |

---

## Phase 5 — Konkrete Features für unseren Roslyn-MCP-Server

Priorisierung nach **(Impact × Häufigkeit) ÷ Aufwand**. Jedes Feature bekommt:
- **Use-Case**: Wer ruft wann warum?
- **Tool-Form**: Tool/Resource/Prompt?
- **Token-Budget**: Default-Output-Größe
- **Roslyn-API**: Welche Roslyn-Backend-Funktion?
- **Wettbewerbsvorteil**: Was hat kein anderer?

### 5.1 Tier 1 — Fundament (MVP, 2–4 Wochen)

#### F1. `validate_file` (Tool)

- **Use-Case**: Nach jedem Edit. Liefert Compiler-Errors + AiNetLinter-Diagnostics.
- **Roslyn-API**: `Compilation.GetDiagnostics()` + unsere Rule-Engine
- **Token-Budget**: max 50 Diagnostics, kompakt
- **Output-Schema** (structured, JSON Schema 2020-12):
  ```json
  {
    "file": "src/Foo.cs",
    "summary": { "errors": 2, "warnings": 5, "info": 1, "codeHealth": 7.2 },
    "diagnostics": [
      { "ruleId": "CS0161", "severity": "E", "line": 42, "col": 17,
        "message": "Method 'X' must return a value", "fix": null }
    ],
    "nextSteps": ["fix CS0161", "consider refactoring 'X' (CC=12)"]
  }
  ```
- **Annotation**: `readOnly: true`, `idempotent: true`
- **Wettbewerbsvorteil**: **AiNetLinter-spezifische Diagnostics**, die kein anderer Server liefert (nicht nur Stock-Analyzer)

#### F2. `safeguard` (Tool — Quality-Contract-Pattern)

- **Use-Case**: Pre-commit / Pre-PR. Stellt sicher, dass AI-generierter Code Mindestqualität erfüllt.
- **Output-Schema**:
  ```json
  {
    "passed": false,
    "score": 6.4,
    "threshold": 8.0,
    "violations": [
      { "ruleId": "AiNetLinter.GodClass", "file": "src/OrderProcessor.cs",
        "line": 1, "severity": "W", "rationale": "Class has 18 methods, 850 LOC" }
    ],
    "remediation": "Extract OrderProcessor.Validate() and OrderProcessor.Notify() into separate services"
  }
  ```
- **Annotation**: `readOnly: true`
- **Wettbewerbsvorteil**: **Selbst-korrigierender Loop** — anders als CodeScene brauchen wir keinen Cloud-Account, läuft lokal, integriert mit `rules.json` als Single-Source-of-Truth
- **Inspiration**: CodeScene CodeHealth MCP, aber ohne Cloud-Dependency

#### F3. `solution_map` (Tool — Aider-Pattern)

- **Use-Case**: Bevor irgendetwas editiert wird. Liefert strukturelle Übersicht.
- **Output-Schema**:
  ```json
  {
    "solution": "MyApp.sln",
    "projects": 3,
    "namespaces": 12,
    "tokenBudget": 1024,
    "symbols": [
      { "name": "OrderProcessor", "kind": "class", "file": "src/OrderProcessor.cs",
        "score": 8.4, "rank": 0.92, "deps": ["IOrderRepo", "ILogger"] },
      ...
    ],
    "hotspots": [
      { "file": "src/LegacyHelper.cs", "cc": 47, "loc": 920, "churn": 0.8 }
    ]
  }
  ```
- **Roslyn-API**: `Solution.GetAllSymbols()` + PageRank via Project-References
- **Token-Budget**: 1.024 default, 0–4.096 parameter
- **Wettbewerbsvorteil**: **PageRank-Ranking** statt simpler File-Listing → relevanteste Symbole zuerst
- **Inspiration**: Aider Repo-Map (Tree-Sitter → Roslyn portiert)

### 5.2 Tier 2 — Hoher Impact (4–8 Wochen)

#### F4. `find_usages` (Tool)

- **Use-Case**: Vor jedem Refactor. "Wo wird diese Methode überall aufgerufen?"
- **Output-Schema**:
  ```json
  {
    "symbol": "OrderProcessor.ProcessOrder",
    "kind": "method",
    "totalUsages": 47,
    "truncated": false,
    "usages": [
      { "file": "src/OrderService.cs", "line": 23, "col": 13,
        "context": "orderProcessor.ProcessOrder(order)", "kind": "call" }
    ]
  }
  ```
- **Parameter**: `symbolId: string`, `includeSnippet: bool = false`, `limit: int = 50`, `filterKind: "call"|"decl"|"all" = "all"`
- **Roslyn-API**: `SymbolFinder.FindReferencesAsync()`
- **Token-Budget**: Default 50 Items, kein Snippet-Default
- **Wettbewerbsvorteil**: Konsistente Token-Discipline (vs. raw dumps)

#### F5. `impact_analysis` (Tool — Roslyn kann das!)

- **Use-Case**: Vor größeren Refactorings. "Was bricht, wenn ich diese Methode ändere?"
- **Output-Schema**:
  ```json
  {
    "target": "OrderProcessor.ProcessOrder(Order)",
    "directCallers": 12,
    "transitiveCallers": 47,
    "testsCovering": 8,
    "testRatio": 0.67,
    "riskScore": "medium",
    "affectedProjects": ["MyApp.Core", "MyApp.Api"],
    "recommendation": "Safe to refactor — 67% test coverage, 47 transitive callers, mostly within single project"
  }
  ```
- **Roslyn-API**: `SymbolFinder.FindCallersAsync()` + transitive reachability + test-discovery
- **Wettbewerbsvorteil**: **Roslyn's Call-Graph + Test-Coverage-Awareness in einem Tool** — keiner der Konkurrenten macht das

#### F6. `pattern_detect` (Tool — die Killer-App)

- **Use-Case**: Audit, Refactoring-Planung. "Finde alle God-Classes, alle async-void, alle public classes ohne XML-Doc, etc."
- **Pattern-Library** (konfigurierbar über `rules.json`):
  - `god-class` (>X Methoden, >Y LOC, >Z Dependencies)
  - `async-void` (in non-event-handler)
  - `long-method` (>X LOC)
  - `deep-nesting` (>X Verschachtelungstiefe)
  - `public-without-doc` (Public-API ohne XML-Doc)
  - `disposable-not-disposed`
  - `static-state` (static mutable fields)
  - `magic-numbers`
  - `empty-catch`
  - `feature-envy` (Method nutzt andere Klasse mehr als eigene)
- **Output-Schema**:
  ```json
  {
    "patterns": [
      { "ruleId": "AiNetLinter.GodClass",
        "severity": "W",
        "occurrences": 7,
        "items": [
          { "file": "src/OrderProcessor.cs", "score": 8.4, "metrics": {...} }
        ]
      }
    ],
    "summary": { "totalViolations": 23, "filesAffected": 11 }
  }
  ```
- **Roslyn-API**: Eigene `DiagnosticAnalyzer`-Suite über `rules.json` konfiguriert
- **Wettbewerbsvorteil**: **AIETLinter hat das Regelwerk, keiner der Roslyn-MCP-Konkurrenten hat einen Pattern-Detector auf Solution-Ebene** (SharpLens & Co. liefern nur Roslyn-Stock-Analyzer)

#### F7. `feature_context` (Resource — die "Was-brauche-ich-für-Feature-X"-Funktion)

- **Use-Case**: "Gib mir alles, was ich brauche, um Feature X zu verstehen."
- **Input**: `featureQuery: string`, `depth: "minimal"|"normal"|"deep" = "normal"`
- **Output-Schema** (Resource-URI: `roslyn://bundle/{hash}`):
  ```json
  {
    "feature": "order-processing",
    "files": [
      { "path": "src/OrderProcessor.cs", "role": "core", "loc": 320 },
      { "path": "src/IOrderRepo.cs", "role": "interface", "loc": 80 },
      { "path": "src/OrderService.cs", "role": "consumer", "loc": 150 }
    ],
    "tests": [{ "path": "tests/OrderProcessorTests.cs", "covers": ["ProcessOrder"] }],
    "entryPoints": [{ "name": "ProcessOrder", "kind": "method" }],
    "skeleton": "...concise symbol map, 600 tokens...",
    "estimatedTokens": 1850
  }
  ```
- **Roslyn-API**: Symbol-Graph-Traversal + Test-Discovery + PageRank
- **Token-Budget**: `depth: "minimal"` = 500 tokens, `"normal"` = 2k, `"deep"` = 8k
- **Wettbewerbsvorteil**: **Komplette Bundle-Funktion als One-Shot** — der Agent muss nicht 5 Tools kombinieren
- **Inspiration**: Phase-4-Lehre "one tool = one agent story"

### 5.3 Tier 3 — Differenzierung (8–16 Wochen)

#### F8. `preview_refactor` (Tool — Safe-Refactor-Pattern)

- **Use-Case**: Vor jedem mutierenden Refactor. Liefert Diff + Risiko-Einschätzung.
- **Refactoring-Typen** (initial subset):
  - `rename_symbol` (mit reference-tracking)
  - `extract_method` (Roslyn-Refactoring-API)
  - `inline_variable`
  - `convert_to_file_scoped_namespace`
  - `add_nullable_annotations`
  - `apply_codefix` (für spezifische Diagnostic-IDs)
- **Output-Schema**:
  ```json
  {
    "refactorId": "uuid",
    "type": "rename_symbol",
    "preview": {
      "diff": "...unified-diff-format...",
      "filesAffected": 5,
      "additions": 0,
      "deletions": 0,
      "modifications": 5
    },
    "risk": "low",
    "impactAnalysis": { /* siehe F5 */ },
    "rollbackPlan": "Revert commit after refactor {refactorId}"
  }
  ```
- **Parameter**: `refactorId: string`, `confirm: bool = false`
- **Zwei-Phasen-Pattern**: `preview` ohne Mutation → User/Agent bestätigt → `apply` mit `refactorId`
- **Wettbewerbsvorteil**: **Atomic + Rollback-Pattern** wie RoslynMcpServer, aber **mit Impact-Analysis vorab** (kein Konkurrent)

#### F9. `metrics_lookup` (Tool — Quick-Lookup)

- **Use-Case**: "Wie komplex ist diese Methode?" — One-Shot, kein File-Open nötig.
- **Metriken**: Cyclomatic Complexity, Cognitive Complexity, LOC, Param-Count, Nesting-Depth, Maintainability-Index
- **Output-Schema**:
  ```json
  {
    "symbol": "OrderProcessor.ProcessOrder",
    "metrics": {
      "cyclomaticComplexity": 12,
      "cognitiveComplexity": 18,
      "loc": 67,
      "paramCount": 4,
      "nestingDepth": 4,
      "maintainabilityIndex": 62,
      "codeHealthScore": 6.5
    },
    "thresholds": {
      "cc": { "value": 12, "limit": 10, "status": "over" },
      "loc": { "value": 67, "limit": 50, "status": "over" }
    },
    "recommendations": ["Extract method (CC > 10)", "Reduce nesting"]
  }
  ```
- **Wettbewerbsvorteil**: **One-Shot** statt File+Symbol-Lookup in zwei Schritten

#### F10. `dependency_graph` (Tool — NuGet + Project-Graph)

- **Use-Case**: Refactoring-Planung, Impact-Analyse über Solution-Grenzen.
- **Output-Schema**:
  ```json
  {
    "graph": {
      "nodes": [
        { "id": "MyApp.Core", "kind": "project", "deps": [] },
        { "id": "MyApp.Api", "kind": "project", "deps": ["MyApp.Core", "Newtonsoft.Json@13.0.3"] }
      ],
      "edges": [{ "from": "MyApp.Api", "to": "MyApp.Core", "kind": "project_ref" }],
      "cycles": [["MyApp.Api", "MyApp.Core"]],  // falls vorhanden
      "vulnerabilities": [
        { "package": "Newtonsoft.Json", "version": "13.0.3", "cve": "CVE-2024-XXXX" }
      ]
    },
    "centralPackages": [{ "name": "Microsoft.Extensions.Logging", "version": "8.0.0" }]
  }
  ```
- **Roslyn-API**: `Solution.Projects` + `Project.MetadataReferences` + NuGet-API
- **Wettbewerbsvorteil**: Kombiniert **Projekt-Graph + NuGet-Vulnerabilities** — keiner der Konkurrenten

#### F11. `test_coverage_context` (Tool — Coverage-Awareness)

- **Use-Case**: Vor Refactorings. "Welche Tests decken diese Methode ab?"
- **Input**: Coverage-Report (Coverlet, OpenCover, …) + Symbol
- **Output-Schema**:
  ```json
  {
    "symbol": "OrderProcessor.ProcessOrder",
    "totalLines": 67,
    "coveredLines": 58,
    "coverageRatio": 0.866,
    "coveredBy": [
      { "test": "ProcessOrder_WithValidOrder_ReturnsSuccess", "file": "tests/OrderProcessorTests.cs", "line": 42 }
    ],
    "uncoveredLines": [10, 11, 23, 45, 67],
    "branchCoverage": 0.78
  }
  ```
- **Inspiration**: Anthropic-Trend-Report "Code Coverage as Behavioral Guardrail"
- **Wettbewerbsvorteil**: **Roslyn-Symbol-Resolution + Coverage-Mapping in einem Tool**

#### F12. `skeleton` (Resource — Aider-Map-Pattern)

- **Use-Case**: Workspace-Orientierung. Liefert nur Symbole, keinen Source-Code.
- **Resource-URI**: `roslyn://skeleton/{project}?depth={int}&filter={regex}`
- **Output**: Kompaktes JSON, max 4.096 Tokens
- **Wettbewerbsvorteil**: **C#-spezifisch** (Tree-Sitter ist generisch, wir können NuGet-aware Symbole mitliefern)

### 5.4 Tier 4 — Enterprise / Future (16+ Wochen)

#### F13. `multi_repo_index` (Tool)

- **Use-Case**: Unternehmen mit 10+ Solutions. "In welchen Repos wird `Order` definiert?"
- **Input**: Workspace-Root mit mehreren `.sln` Files
- **Output**: Cross-Repo-Index + Symbol-Resolution
- **Inspiration**: Sourcegraph Cody's Code-Graph
- **Aufwand**: Hoch, eigener Index-Store nötig

#### F14. `elicit_clarification` (Elicitation)

- **Use-Case**: Server entdeckt mehrdeutige Refactoring-Anfrage, fragt User.
- **Beispiel**: User sagt "refactor die Klasse". Server: "Welche? Es gibt 3 Klassen mit ähnlichen Namen: OrderProcessor, OrderService, OrderHelper. Bitte wählen."
- **Spec-Compliance**: 2025-06-18+

#### F15. `analyze_via_sampling` (Sampling)

- **Use-Case**: Server braucht LLM-Summary einer komplexen Methode.
- **Beispiel**: Methode hat 200 Zeilen, hohe Komplexität. Server ruft via Sampling Client-LLM auf für "Was tut diese Methode? 1 Satz."
- **Deprecation-Hinweis**: Sampling ist in 2026-07-28-Spec deprecated — wir sollten den Use-Case stattdessen via **MRTR mit InputRequiredResult** oder via Tool-Design (Agent bekommt die Methode + fasst selbst zusammen) lösen

#### F16. `apps` (MCP Apps Extension)

- **Use-Case**: Interaktive UI-Komponenten für Diff-Vorschau, Coverage-Heatmap, Refactoring-Plan.
- **Beispiel**: Server liefert HTML/Sandboxed-IF-Component für Refactoring-Vorschau, User klickt Methoden an, um Details zu sehen
- **Spec-Compliance**: 2026-01-26+ (MCP Apps live)
- **Voraussetzung**: VS Code Insiders oder kompatibler Client

### 5.5 Workflow-Templates (Prompts)

Wir sollten **Reusable Prompts** für Standard-Workflows anbieten:

- `/ai_net_linter.audit` — Full Code-Health-Audit
- `/ai_net_linter.preview_refactor` — Diff-Preview für ein Refactoring
- `/ai_net_linter.fix_lint_violations` — Self-correcting loop mit safeguard
- `/ai_net_linter.explain_method` — Method-Skeleton + Sampling-basierte Summary
- `/ai_net_linter.find_god_classes` — Pattern-Detection
- `/ai_net_linter.impact_of` — Impact-Analyse
- `/ai_net_linter.scaffold_feature` — Skeleton-Bundle für neues Feature

### 5.6 Tool-Curation-Strategie

**Empfehlung:** Ship **12 Tools im MVP** (Tier 1 + 2), 5 weitere in Tier 3, Rest als **dynamic/on-demand** via Progressive-Disclosure-Meta-Tool `help(topic)`.

```
Tier 1 (3):   validate_file, safeguard, solution_map
Tier 2 (4):   find_usages, impact_analysis, pattern_detect, feature_context
Tier 3 (5):   preview_refactor, metrics_lookup, dependency_graph, test_coverage_context, skeleton
Future (4+):  multi_repo_index, elicit, sampling, apps
```

**Total im MVP: 12 Tools ≈ 2.000–3.000 Tokens Schema-Overhead.** Das ist im 200K-Context **< 1,5 % Budget** — gut.

---

## Phase 6 — Zukunftstrends 2026/2027

### 6.1 Anthropic's "2026 Agentic Coding Trends Report"

Quelle: [resources.anthropic.com/2026-agentic-coding-trends-report](https://resources.anthropic.com/2026-agentic-coding-trends-report), analysiert: [pathmode.io](https://pathmode.io/blog/orchestration-era-needs-intent), [udit.co](https://udit.co/blog/anthropic-agentic-coding-trends-report-multi-agent)

**8 Trends in 3 Kategorien:**

#### Foundation Trends
1. **SDLC verändert sich dramatisch** — Cycle-Times von Wochen auf Stunden
2. **Multi-Agent-Systeme ersetzen Single-Agent-Workflows** — Orchestrator + Spezialisten in parallelen Context-Windows
3. **Long-running Agents** — Tasks von Minuten zu Tagen; dokumentierter Case: 12,5M-LOC-Codebase-Change in einem 7-Stunden-Run

#### Capability Trends
4. **Human-Oversight skaliert durch intelligente Kollaboration** — Agents lernen, wann sie um Hilfe fragen
5. **Agentic Coding expandiert auf neue Surfaces/Users** — Legacy-Sprachen (COBOL, Fortran), Nicht-Developer (Security, Design, Ops)
6. **Productivity-Gains reshapen Economics** — ~27 % der AI-assisted Arbeit existierte vorher nicht

#### Impact Trends
7. **Non-Technical Use Cases** — Sales, Marketing, Legal, Ops bauen eigene Automationen
8. **Dual-Use Risk** — Security-First-Architektur von Anfang an

**Zentrale These:** *Ingenieure werden von "Code-Schreibern" zu "Agent-Orchestratoren".*

**Konkrete Daten:**
- 3,4× bessere Task-Completion mit Multi-Agent auf Tasks >500 LOC oder >8 Files
- 2,1× bessere Code-Quality (Downstream-Test-Pass-Rate)
- Engineers verbringen **34 % der Zeit** mit direktem Code-Schreiben (war 58 % in 2024)
- **22 % Code-Review, 19 % Task-Definition, 16 % Architektur, 9 % Debugging**
- **"Verification as the new bottleneck"** — Quality-Evaluation wird die Kern-Engineering-Skill

### 6.2 Implikation für AiNetLinter

**Wir sitzen an der Verification-Schnittstelle.** Die Trends bestätigen unsere Strategie:

1. **Self-correcting loop** (F2 `safeguard`) ist EXAKT das, was der Report als Bottleneck-Lösung identifiziert.
2. **Quality-Threshold als deterministisches Signal** statt subjektives LLM-Bauchgefühl → unser Haupt-Wettbewerbsvorteil.
3. **Multi-Agent-Support**: Server muss **mit anderen MCP-Servern kooperieren** (z. B. Sub-Agent für Tests, Review, Docs).
4. **Long-running**: Tasks über Stunden erfordern **Stateful Workspace** (Roslyn-Workspace persistent halten, inkrementelle Updates).
5. **Verification is King**: Wir liefern die **deterministische Verifikations-Schicht** zwischen AI-Output und CI-Merge.

### 6.3 Microsoft / VS 2026 Roadmap

[VS 2026 release notes](https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes), [Azure MCP Blog](https://devblogs.microsoft.com/visualstudio/azure-mcp-server-now-built-in-with-visual-studio-2026-a-new-era-for-agentic-workflows/):

- **MCP Trust Validation** in VS 2026 (2026-06-23) — Server-Manifests werden signiert/verifiziert, User wird bei Änderungen re-approven
- **Azure MCP Server out-of-the-box** in VS 2026 — Azure-Resource-Management via Natural Language
- **Copilot agents** entdecken automatisch `skills/` in Repo oder User-Profile
- **MCP Allowlist-Policies** für Enterprise
- **MCP Apps Support in VS Code** ([code.visualstudio.com](https://code.visualstudio.com/blogs/2026/01/26/mcp-apps-support)) — interaktive UI-Komponenten

**Implikation:** Wir müssen **Server-Manifest-Signing** implementieren (für VS 2026 Trust-Validation) und idealerweise eine `skill.md` für Auto-Discovery in Copilot-Agents liefern.

### 6.4 Open-Source-Innovationen 2026/2027 (Hypothesen)

Basierend auf den identifizierten Trends:

| Trend | Wahrscheinlichkeit | Konsequenz für AiNetLinter |
|-------|---------------------|----------------------------|
| **Stateless MCP** wird Default | Hoch | Workspace-Snapshot-Cache statt In-Memory-State |
| **MCP Apps** werden in Claude.ai + Cursor unterstützt | Mittel | Optional, interaktive Refactoring-Preview |
| **Multi-Agent-Workflows** Standard | Hoch | Server liefert "Sub-Agent-Hooks" für Tasks, Reviews, Docs |
| **Deterministic Verification** = Standard | Hoch | AiNetLinter ist Vorreiter in dieser Nische |
| **Code-Health-as-a-Service** (CodeScene-Modell) | Mittel | Wir sind lokale Alternative ohne Cloud |
| **MCP Registry** wird zentrales Verzeichnis | Hoch | AiNetLinter muss in `registry.modelcontextprotocol.io` gelistet sein |
| **OAuth 2.1 + Entra ID** Standard | Hoch | Streamable-HTTP-Mode mit Entra-ID-Integration |
| **Token-Optimierung wird Kern-Concern** | Sehr hoch | Progressive Disclosure + Compact Outputs sind Pflicht |

### 6.5 Was wir in 12 Monaten bauen sollten

**Roadmap-Skizze (Vision, kein Commitment):**

```
Q3 2026: MVP — stdio, 12 Tools, Tier 1+2, Safety-First
Q4 2026: Streamable-HTTP-Mode, OAuth 2.1, VS-Marketplace
Q1 2027: Multi-Repo-Index, MCP Apps, Skill-Definition
Q2 2027: Multi-Agent-Coordination-Tools, Federation mit anderen MCP-Servern
```

---

## Top 10 Features für unseren Roslyn-MCP-Server (Impact/Aufwand)

Sortierung: **Impact** (Wettbewerbsvorteil × Token-Einsparung × Häufigkeit) ÷ **Aufwand** (Komplexität × Roslyn-API-Reife).

### 1. `safeguard` — **Quality-Contract-Pattern** ⭐⭐⭐⭐⭐

**Impact:** Höchster. Direkter Differentiator gegenüber allen Konkurrenten. Löst den "Verification Bottleneck" aus dem Anthropic-Report.
**Aufwand:** Niedrig. Wir haben bereits `rules.json` als Single-Source-of-Truth. Wrapper um `Compilation.GetDiagnostics()` + unsere Engine.
**Sofort-ROI:** Self-correcting loop spart 1–3 Iterationen pro AI-Task.

### 2. `solution_map` — Aider-PageRank-Map ⭐⭐⭐⭐⭐

**Impact:** Sehr hoch. Spart 50–90 % der Token für Erst-Orientierung. Macht Roslyn-MCP-Server für 100K+ LOC-Codebases überhaupt erst nutzbar.
**Aufwand:** Mittel. PageRank-Algorithmus + Roslyn-Symbol-API + Caching.
**Sofort-ROI:** Agent-Cold-Start von "ich sehe nichts" zu "ich sehe die 50 relevantesten Symbole".

### 3. `pattern_detect` — Solution-weite Pattern-Suche ⭐⭐⭐⭐⭐

**Impact:** Sehr hoch. Code-Smells, God-Classes, async-void, etc. — Roslyn kann das, keiner der MCP-Server tut es.
**Aufwand:** Mittel. Eigene Analyzer-Suite + Output-Schema.
**Sofort-ROI:** Audit-Aufgaben von Stunden auf Sekunden.

### 4. `feature_context` — One-Shot-Feature-Kontext ⭐⭐⭐⭐

**Impact:** Hoch. "One tool = one agent story" — Pattern aus Best-Practices.
**Aufwand:** Mittel-Hoch. Symbol-Graph-Traversal + Heuristik.
**Sofort-ROI:** 1 Tool-Call statt 5+ Calls pro Feature-Verständnis.

### 5. `preview_refactor` — Safe-Refactor mit Impact-Pre-Check ⭐⭐⭐⭐

**Impact:** Hoch. Senkt Risiko bei AI-Refactorings drastisch. Roslyn kann das (CodeActions + RoslynMcpServer-Vorbild).
**Aufwand:** Mittel. Roslyn-CodeAction-Wrapper + Diff-Output + Rollback.
**Sofort-ROI:** Vertrauen in AI-Refactorings → mehr Refactorings → bessere Code-Quality.

### 6. `impact_analysis` — Transitive Caller + Test-Coverage ⭐⭐⭐⭐

**Impact:** Hoch. Kombination, die kein Konkurrent macht.
**Aufwand:** Mittel. Symbol-Finder + Test-Discovery (Project-Convention-basiert).
**Sofort-ROI:** AI trifft bessere Refactoring-Entscheidungen.

### 7. Progressive-Disclosure-Pattern für 67+ potenzielle Tools ⭐⭐⭐⭐

**Impact:** Hoch (strukturell). Macht Tool-Bloat (SharpLens-Fehler) unmöglich.
**Aufwand:** Niedrig-Mittel. Meta-Tool-Pattern + JSON-Resource-Liste.
**Sofort-ROI:** Server-Skalierung ohne Context-Explosion.

### 8. `metrics_lookup` — One-Shot-Metriken ⭐⭐⭐

**Impact:** Mittel-Hoch. Quick-Lookup statt File-Open + Scan.
**Aufwand:** Niedrig. Berechnung aus SemanticModel.
**Sofort-ROI:** Spart einen File-Read pro Frage.

### 9. `dependency_graph` — NuGet + Project-Graph ⭐⭐⭐

**Impact:** Mittel. Sicherheitsrelevant (CVE-Check), Architektur-Relevant.
**Aufwand:** Mittel. NuGet-API + Graph-Generierung.
**Sofort-ROI:** Risiko-Transparenz bei Upgrades.

### 10. Compact-Output-Strategien (TOON, Field-Selection, Pagination) ⭐⭐⭐

**Impact:** Mittel-Hoch (strukturell). Reduziert Token-Verbrauch auf ALLEN Tools.
**Aufwand:** Niedrig. Output-Schema-Design + Default-Parameter.
**Sofort-ROI:** ~30–50 % Token-Reduktion ohne Funktionsverlust.

### Was bewusst NICHT in den Top 10 ist

- **Multi-Repo-Index** (zu früh, hoher Aufwand)
- **MCP Apps** (spec-unstable, UI-Komplexität)
- **Sampling/Elicitation** (sampling deprecated in 2026-07-28)
- **Generative Refactorings** (LLM-Aktionen, wir liefern Verifikation)

### Reihenfolge der Implementierung

| Sprint | Features | Geschätzter Aufwand |
|--------|----------|---------------------|
| 1 | #1 safeguard, #2 solution_map, #10 compact outputs | 3 Wochen |
| 2 | #3 pattern_detect, #8 metrics_lookup | 3 Wochen |
| 3 | #7 progressive-disclosure, #5 preview_refactor | 4 Wochen |
| 4 | #4 feature_context, #6 impact_analysis | 4 Wochen |
| 5 | #9 dependency_graph, Streamable-HTTP-Mode | 4 Wochen |

**MVP nach Sprint 1+2:** Wettbewerbsfähiger Roslyn-MCP-Server, der alle Konkurrenten in **Quality-Contract + Repo-Map + Pattern-Detection** schlägt.

---

## Quellenverzeichnis

### Phase 1 — Roslyn-MCP-Landschaft
- [github.com/egorpavlikhin/roslyn-mcp](https://github.com/egorpavlikhin/roslyn-mcp) — ValidateFile + FindUsages
- [github.com/carquiza/RoslynMCP](https://github.com/carquiza/RoslynMCP) — Wildcard-Symbol-Search
- [github.com/pzalutski-pixel/sharplens-mcp](https://github.com/pzalutski-pixel/sharplens-mcp) — 67 Tools
- [github.com/JoshuaRamirez/RoslynMcpServer](https://www.nuget.org/packages/RoslynMcp.Server) — 41 Tools, NuGet
- [github.com/sailro/RoslynMcpExtension](https://github.com/sailro/RoslynMcpExtension) — VS-integriert
- [github.com/biegehydra/CSharpLangMCPServer](https://github.com/biegehydra/CSharpLangMCPServer) — Erste VSCode-Variante
- [marketplace.visualstudio.com/items?itemName=YaroslavHorokhov.RoslynMCP](https://marketplace.visualstudio.com/items?itemName=YaroslavHorokhov.RoslynMCP) — VS-Proxy
- [www.nuget.org/packages/dotnet-roslyn-mcp](https://www.nuget.org/packages/dotnet-roslyn-mcp) — vs-ide-mcp
- [mcpservers.org/servers/remleo/amazingmcp](https://mcpservers.org/servers/remleo/amazingmcp) — AmazingMCP
- [github.com/SciSharp/Awesome-DotNET-MCP](https://github.com/SciSharp/Awesome-DotNET-MCP) — Kuratierte .NET-MCP-Liste
- [github.com/modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Offizielles MCP C# SDK
- [learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server) — Microsoft Quickstart
- [marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) — C# Dev Kit
- [github.com/microsoft/mcp-dotnet-samples](https://github.com/microsoft/mcp-dotnet-samples) — Microsoft .NET MCP-Samples

### Phase 2 — MCP-Spec & Patterns
- [blog.modelcontextprotocol.io/posts/2026-07-28/](https://blog.modelcontextprotocol.io/posts/2026-07-28/) — Stateless Core, MRTR
- [blog.modelcontextprotocol.io/posts/2026-07-28-release-candidate/](https://blog.modelcontextprotocol.io/posts/2026-07-28-release-candidate/) — Spec-RC
- [modelcontextprotocol.io/specification/2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28) — Offizielle Spec
- [modelcontextprotocol.io/docs/2026-07-28/tutorials/security/authorization](https://modelcontextprotocol.io/docs/2026-07-28/tutorials/security/authorization) — Auth-Tutorial
- [devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/](https://devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/) — MCP C# SDK v2.0
- [mcginniscommawill.com/posts/2026-03-25-mcp-sampling-elicitation-guide/](https://mcginniscommawill.com/posts/2026-03-25-mcp-sampling-elicitation-guide/) — Sampling/Elicitation
- [apigene.ai/blog/mcp-sse-vs-stdio](https://apigene.ai/blog/mcp-sse-vs-stdio) — Transport-Vergleich
- [truefoundry.com/blog/mcp-stdio-vs-streamable-http-enterprise](https://www.truefoundry.com/blog/mcp-stdio-vs-streamable-http-enterprise) — Streamable HTTP
- [note.com/ayato_studio/n/n61c1ccefbab4?hl=en-US](https://note.com/ayato_studio/n/n61c1ccefbab4?hl=en-US) — Transport-Decision
- [workos.com/blog/everything-your-team-needs-to-know-about-mcp-in-2026](https://workos.com/blog/everything-your-team-needs-to-know-about-mcp-in-2026) — MCP-Overview 2026
- [zenml.io/llmops-database/best-practices-for-building-production-grade-mcp-servers-for-ai-agents](https://www.zenml.io/llmops-database/best-practices-for-building-production-grade-mcp-servers-for-ai-agents) — Tool-Design
- [dev.to/thedailyagent/building-production-grade-ai-agents-with-mcp-a-complete-guide-for-2026-3bo2](https://dev.to/thedailyagent/building-production-grade-ai-agents-with-mcp-a-complete-guide-for-2026-3bo2) — Production-Grade
- [code.visualstudio.com/blogs/2025/06/12/full-mcp-spec-support](https://code.visualstudio.com/blogs/2025/06/12/full-mcp-spec-support) — VS Code MCP
- [code.visualstudio.com/blogs/2026/01/26/mcp-apps-support](https://code.visualstudio.com/blogs/2026/01/26/mcp-apps-support) — MCP Apps
- [redhat.com/en/blog/mcp-security-implementing-robust-authentication-and-authorization](https://www.redhat.com/en/blog/mcp-security-implementing-robust-authentication-and-authorization) — Security
- [labs.cloudsecurityalliance.org/agentic/agentic-mcp-security-best-practices-v1/](https://labs.cloudsecurityalliance.org/agentic/agentic-mcp-security-best-practices-v1/) — MCP-Security
- [descope.com/blog/post/mcp-auth-spec](https://www.descope.com/blog/post/mcp-auth-spec) — Auth-Spec-Detail

### Phase 3 — Token-Optimierung
- [speakeasy.com/blog/how-we-reduced-token-usage-by-100x-dynamic-toolsets-v2/](https://www.speakeasy.com/blog/how-we-reduced-token-usage-by-100x-dynamic-toolsets-v2/) — 100x-Reduktion
- [solo.io/blog/mcp-progressive-disclosure](https://www.solo.io/blog/mcp-progressive-disclosure) — Progressive Disclosure
- [matthewkruczek.ai/blog/progressive-disclosure-mcp-servers.html](https://matthewkruczek.ai/blog/progressive-disclosure-mcp-servers.html) — Design-Principles
- [blog.synapticlabs.ai/bounded-context-packs-meta-tool-pattern](https://blog.synapticlabs.ai/bounded-context-packs-meta-tool-pattern) — Meta-Tool
- [mindstudio.ai/blog/optimize-mcp-server-token-usage](https://www.mindstudio.ai/blog/optimize-mcp-server-token-usage) — 10 Techniken
- [stackone.com/blog/mcp-token-optimization/](https://www.stackone.com/blog/mcp-token-optimization/) — 4 Approaches
- [reddit.com/r/mcp/comments/1p0py33](https://www.reddit.com/r/mcp/comments/1p0py33/3_tips_to_make_mcp_servers_token_efficient/) — 3 Tips
- [huggingface.co/spaces/MCP-1st-Birthday/mcp-extension-progressive-disclosure](https://huggingface.co/spaces/MCP-1st-Birthday/mcp-extension-progressive-disclosure/blob/main/docs/guide_progressive_disclosure_implementation.md) — Implementation-Guide

### Phase 4 — Konkurrenz
- [github.com/wrale/mcp-server-tree-sitter](https://github.com/wrale/mcp-server-tree-sitter) — Tree-Sitter MCP
- [deepwiki.com/wrale/mcp-server-tree-sitter/](https://deepwiki.com/wrale/mcp-server-tree-sitter/1-overview) — Architektur
- [github.com/NightTrek/treesitter-mcp](https://github.com/NightTrek/treesitter-mcp) — Tree-Sitter-Variante
- [arxiv.org/html/2603.27277v1](https://arxiv.org/html/2603.27277v1) — Codebase-Memory
- [aider.chat/docs/repomap.html](https://aider.chat/docs/repomap.html) — Aider Repo-Map
- [codeintel.xyz/blog/aider-architecture-tool-review-2026/](https://codeintel.xyz/blog/aider-architecture-tool-review-2026/) — Aider-Deep-Dive
- [github.com/aider-ai/aider](https://github.com/aider-ai/aider) — Aider Repo
- [docs.continue.dev/customize/deep-dives/mcp](https://docs.continue.dev/customize/deep-dives/mcp) — Continue MCP
- [toolhalla.ai/blog/aider-vs-continue-dev-vs-cody-2026](https://toolhalla.ai/blog/aider-vs-continue-dev-vs-cody-2026) — Tool-Vergleich
- [itsdeep.io/compare/cody-sourcegraph-vs-continue-dev](https://itsdeep.io/compare/cody-sourcegraph-vs-continue-dev) — Cody vs Continue
- [codescene.com/product/code-health-mcp](https://codescene.com/product/code-health-mcp) — CodeHealth-MCP
- [github.com/codescene-oss/codescene-mcp-server](https://github.com/codescene-oss/codescene-mcp-server) — CodeScene-MCP-Repo
- [codescene.com/hubfs/CodeScene-AI-Playbook-2026.pdf](https://codescene.com/hubfs/CodeScene-AI-Playbook-2026.pdf) — AI-Playbook
- [helpcenter.codescene.com/articles/7208397-what-can-codescene-single-user-mcp-do](https://helpcenter.codescene.com/articles/7208397-what-can-codescene-single-user-mcp-do) — CodeScene-Tool-Liste

### Phase 5 — Roslyn-API
- [devleader.ca/2026/07/16/ioperation-vs-syntaxnode-vs-symbol-in-roslyn](https://www.devleader.ca/2026/07/16/ioperation-vs-syntaxnode-vs-symbol-in-roslyn-choosing-the-right-analysis-api) — IOperation vs SyntaxNode vs Symbol
- [devleader.ca/2026/07/11/roslyn-analyzers-in-c-the-complete-guide](https://www.devleader.ca/2026/07/11/roslyn-analyzers-in-c-the-complete-guide) — Analyzer-Guide
- [learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/semantic-analysis](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/semantic-analysis) — Semantic-Model
- [pvs-studio.com/en/blog/posts/csharp/0399/](https://pvs-studio.com/en/blog/posts/csharp/0399/) — Roslyn-Overview
- [github.com/dotnet/roslyn/blob/main/docs/wiki/Roslyn-Overview.md](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Roslyn-Overview.md) — Roslyn-Wiki

### Phase 6 — Trends
- [resources.anthropic.com/2026-agentic-coding-trends-report](https://resources.anthropic.com/2026-agentic-coding-trends-report) — Anthropic Report Landing
- [resources.anthropic.com/hubfs/2026%20Agentic%20Coding%20Trends%20Report.pdf](https://resources.anthropic.com/hubfs/2026%20Agentic%20Coding%20Trends%20Report.pdf) — Full PDF
- [pathmode.io/blog/orchestration-era-needs-intent](https://pathmode.io/blog/orchestration-era-needs-intent) — Report-Summary
- [udit.co/blog/anthropic-agentic-coding-trends-report-multi-agent](https://udit.co/blog/anthropic-agentic-coding-trends-report-multi-agent) — Multi-Agent-Analysis
- [huggingface.co/blog/Svngoku/agentic-coding-trends-2026](https://huggingface.co/blog/Svngoku/agentic-coding-trends-2026) — Implementation-Guide
- [learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes](https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes) — VS 2026 Release Notes
- [devblogs.microsoft.com/visualstudio/azure-mcp-server-now-built-in-with-visual-studio-2026](https://devblogs.microsoft.com/visualstudio/azure-mcp-server-now-built-in-with-visual-studio-2026-a-new-era-for-agentic-workflows/) — Azure MCP in VS 2026
- [hidekazu-konishi.com/entry/mcp_specification_version_timeline.html](https://hidekazu-konishi.com/entry/mcp_specification_version_timeline.html) — Spec-Timeline

---

**Fazit:** Der MCP-Markt für C#/.NET ist 2026 in einer kritischen Phase — 10+ konkurrierende Roslyn-Server, keiner dominant, Microsoft noch nicht eingestiegen, massive Token-Optimierung als Differentiator. AiNetLinter hat mit seinem bestehenden Linter-Regelwerk (`rules.json`) einen **strukturellen Vorteil** für das "Quality-Contract-Pattern" (CodeScene-Lessons), der mit überschaubarem Aufwand zum **MVP-Differentiator** werden kann.

Empfohlener Startpunkt: **F2 safeguard + F2 solution_map + F3 pattern_detect** (Sprint 1+2), dann iterative Erweiterung.
