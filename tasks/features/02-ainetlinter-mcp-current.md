# AiNetLinter MCP-Server — Recon-Bericht (IST-Zustand)

**Datum:** Stand `rules.json` v1.0.81, `AiNetLinter.csproj` net10.0
**Repo:** `C:/Daten/Entwicklung/Ralf/AiNetLinter`
**Scope:** Vollständige Bestandsaufnahme der MCP-Server-Integration, ohne Implementierung
**Charakter:** Internes Recon-Dokument, ehrliche Schwächen-Analyse

---

## 1. Executive Summary

AiNetLinter ist heute ein reifer C#/.NET 10 Roslyn-Linter mit **drei orthogonalen Verwendungsmodi**:

1. **CLI-Batch** (Default) — `ainetlinter --config rules.json --path .` gibt einen Markdown-Report aus.
2. **Discovery-CLI** (agentisch) — `--list-rules`, `--describe-rule`, `--map`, `--eval` für eigenständige Agent-Exploration.
3. **MCP-Server** (stdio) — `ainetlinter --mcp-server` exposeiert 10 Tools + 1 Resource auf einem resident gehaltenen Roslyn-Workspace.

**Der MCP-Server ist das Herzstück für agentische Workflows** und der Schwerpunkt dieses Reports. Er ist produktionsreif, gut getestet, strukturell abgesichert (kein stdout-Leak, keine Race-Conditions, sauberes Refresh-Modell), aber **primär read-only und auf C#-Code fokussiert**. CodeGraph-artige Capabilities (Dep-Graph, Edit-Preview, Symbol-Resolution mit Kontext) sind nur teilweise vorhanden.

**Kernzahl:** 10 MCP-Tools, 1 MCP-Resource, 6 Tool-Registrar-Klassen, 11 Tool-Implementierungen, 6 Scanner/Helper-Klassen, 1 Server-Engine-Klasse, ~50 C#-Dateien in `src/AiNetLinter/Mcp/` (~10.000 LOC total).

---

## 2. Phase 1 — Repo-Überblick

### 2.1 Tech-Stack

| Komponente | Wert |
| :--- | :--- |
| Laufzeit | .NET 10 (`net10.0`) |
| Sprache | C# (`#nullable enable` durchgängig, `TreatWarningsAsErrors`) |
| Roslyn | `Microsoft.CodeAnalysis.CSharp` 5.6.0, `MSBuild.Workspaces` 5.6.0 |
| MCP-SDK | `ModelContextProtocol` 2.0.0 (offizielles .NET-SDK) |
| CLI | `System.CommandLine` 2.0.9 |
| Web-Asset | `ExCSS` 4.3.1, `Esprima` 3.0.6 |
| Test-Framework | xUnit v3, Microsoft.Testing.Platform |
| Transport | **stdin/stdout** (StdioServerTransport) — kein HTTP/SSE |
| BuildHost-Patch | `BuildHostPatcher.PatchBuildHostForVs2026()` für VS 2026 MSBuild-Locator |

### 2.2 Solution-Layout

```
AiNetLinter.slnx
src/
  AiNetLinter/          ← CLI + Engine + MCP (exe, net10.0)
    Baseline/           ← SourceFileCatalog, FileChecksumCalculator, BuildHostPatcher
    Cache/              ← AnalysisCacheManager (Disk-Cache zwischen CLI-Läufen)
    Cli/                ← CliCommandBuilder, CliOptions, LinterArgs, CliOptionFactory
    Commands/           ← 14 *Command.cs-Klassen (Audit, McpServer, Map, Eval, ...)
    Configuration/      ← Config, GlobalConfig, MetricsConfig, ConfigLoader, ConfigOverrides
    Core/               ← LinterEngine, LinterAnalyzer, RuleRegistry (3 partial files), 25 Checker
    Diagnostics/        ← PerformanceProfiler, NullPerformanceProfiler
    Evals/              ← EvalAssembler, EvalRegistry, SpecLoader
    Generators/         ← AgentRulesGenerator, RepoPlaybookGenerator
    Maps/               ← HotspotMapBuilder, StructureMapBuilder, VocabularyMapBuilder
    Maps/Skeleton/      ← SkeletonMapBuilder, SkeletonMarkdownRenderer, SkeletonSyntaxWalker
    Mcp/                ← ★ MCP-Server (siehe Phase 2)
    Metrics/            ← AIContextFootprintCalculator, ComplexityCalculator
    Models/             ← ClassInfo, RuleViolation
    Output/             ← ViolationMarkdownFormatter, LinterErrorFormatter, McpLintConsole
    Scope/              ← GitChangedFilesResolver, ViolationScopeFilter
    Suppression/        ← DisableAllDetector, IgnoreSuppressionsFilter
    Web/                ← CssAnalyzer, JsAnalyzer, RazorAnalyzer, WebFileCatalog
  AiNetLinter.Tests/    ← xUnit v3, 200+ Tests
rules.json              ← aktive Regel-Konfiguration (siehe §2.4)
Docs/                   ← agent-api.md, configuration.md, integration.md, rationale.md, ROADMAP.md, Evals/
.agents/rules/          ← AiNetLinter.mdc (auto-generiert), AiNetLinterRichtlinien.mdc (manuell)
```

### 2.3 Eingebettete Ressourcen

`AiNetLinter.csproj` embeddet 11 Dateien (README, Docs/*, rules.json, 2 Eval-Specs) als `EmbeddedResource`. Drei davon (`README.md`, `Docs/integration.md`, `Docs/agent-api.md`) sind über Reflection-Loader für den MCP-Boot-Pfad relevant (`--docs`-Subcommand). `rules.json` ist die Default-Konfiguration, falls neben der Solution-Datei keine projekteigene liegt.

### 2.4 Regel-Inventar (aus `rules.json`)

`Global` (15 aktivierte Bool-Regeln): `EnforceSealedClasses`, `EnforceValueObjectContracts`, `EnableTestSentinel`, `EnforcePascalCase`, `EnforceAsciiIdentifiers`, `EnforceSemanticNaming`, `EnforceNullableEnable`, `EnforceNoSilentCatch`, `EnforceNamespaceDirectoryMapping`, `DetectAndBanPhantomDependencies`, `BanPublicNestedTypes`, `AvoidExcessiveMiddleMen`, `BanAsyncVoid`, `BanBlockingTaskAccess`.

`Metrics` (25 numerische Limits): `MaxLineCount=500`, `MaxMethodParameterCount=4`, `MaxMethodLineCount=60`, `MaxCyclomaticComplexity=12`, `MaxCognitiveComplexity=15`, `MaxInheritanceDepth=3`, `MaxConstructorDependencies=5`, `MaxAIContextFootprint=2500`, `MaxDirectoryDepth=4`, `MaxSwitchArms=10`, `MaxBoolParameterCount=1`, `MaxDirectoryChildren=30`, `MaxPartialClassFiles=2`, `MaxPublicMembersPerType=15`, `MaxLinqChainLength=0` (deaktiviert).

`TestSentinel` (5 Klassen-Pattern-Sets), `UiSeparation` (Blazor + WPF), `FileFilters` (designer.cs, obj, bin), `Web` (CSS/JS/Razor aktiviert), `ProjectOverrides` (`*.Tests` mit gelockerten Limits), `PathOverrides` (per-File-Anpassungen für **alle** MCP-Quelldateien — siehe §5.2).

### 2.5 Was kann AiNetLinter heute? (für Agenten)

- **Strukturqualität erzwingen**: 50+ Regeln mit zitierter Forschung (McCabe 1976, SonarSource, Palomba 2018).
- **Konventionen auditieren**: Markdown-Report auf stdout, Exit-Code 0/1 für CI.
- **Auto-Fix für triviale Verstöße**: `ainetlinter --fix [--dry-run]`.
- **Baseline/Ratchet**: SHA-256-frozen Verstoß-Set, `--only-changed` für inkrementelle Lint-Checks.
- **Codebase-Landkarten**: `--map vocabulary|structure|hotspots|skeleton` für LLM-Audit-Prompts.
- **Eval-Audits**: `--eval naming-drift|architecture-intent` assembliert vollständige Audit-Prompts mit frischer Evidenz.
- **Agent-Regeln sync**: `--sync-agent-rules-only` generiert `.agents/rules/AiNetLinter.mdc` aus `rules.json`.
- **MCP-Server**: 10 Tools für Roslyn-basierte Code-Graph-Queries während des Editierens.

---

## 3. Phase 2 — MCP-Server-Implementierung (Schwerpunkt)

### 3.1 Boot-Sequenz (`Program.cs` → `McpServerCommand`)

```
argv: --mcp-server [--path <slnx>] [--config <rules.json>] [--mcp-log <pfad>]
    │
    ▼
CliCommandBuilder.Build()
    │   System.CommandLine parse → CliParsedArgs
    ▼
Program.Main()
    │   LinterArgs.McpServer == true  (Fast-Path, kein stdout-Header)
    ▼
McpServerCommand.RunAsync(args, ct, McpLintConsole.Instance)
    │
    ├─ 1. ResolveSolutionPathOrError()  (Datei|Verz-mit-Auto|Mehrdeutig|Error)
    ├─ 2. TryResolveRulesJsonPath()     (--config oder rules.json neben slnx)
    ├─ 3. ResolveConfig() / ResolveMaxLineCount()  (best-effort load)
    ├─ 4. new McpCodeGraphServer(options {
    │        LoadFunc = Task.Run(TryLoadSolutionAsync),  ← HINTERGRUND
    │        Console = McpLintConsole.Instance,           ← stderr-only
    │        Config, MaxLineCount, ...
    │     })
    ├─ 5. TryCreateCallLog()  (opt-in, default deaktiviert)
    ├─ 6. McpServerOptionsFactory.Create(mcpState, callLog)
    │       ├─ SymbolGraphToolRegistrations (4 tools)
    │       ├─ FileStructureToolRegistrations (3 tools)
    │       ├─ AnalysisToolRegistrations (2 tools)
    │       ├─ SymbolBodyToolRegistrations (1 tool)
    │       └─ OverviewResourceRegistration (1 resource)
    ├─ 7. StdioServerTransport(serverOptions)
    └─ 8. McpServer.Create(transport, serverOptions).RunAsync(ct)
```

**Drei-Zustands-Lifecycle des Servers** (siehe `McpCodeGraphServer.LoadState`):

| Zustand | Erkennbar an | Reaktion |
| :--- | :--- | :--- |
| `Loading` (transient) | Tool-Response `[INFO]: Server laedt die Solution noch. ...` (`isError == false`) | Sekunden warten, retry |
| `Loaded` (regulär) | Volle Tool-Antworten | Normal arbeiten |
| `LoadFailed` (terminal) | `[ERROR]: SOLUTION_NOT_LOADED: ...` | Server-Log prüfen, neu starten |

**Schlüssel-Designentscheidungen:**

- **Entkoppelter Handshake:** `initialize` antwortet sofort, Solution-Load läuft im `Task.Run` — keine Startup-Timeout-Probleme bei großen Solutions.
- **Best-Effort-Load:** Fehler beim Solution-Load führt nicht zum Server-Crash, sondern zu `SOLUTION_NOT_LOADED` auf jedem Tool-Call.
- **Resident Workspace:** `MSBuildWorkspace` + `SourceFileCatalog` werden über die gesamte Prozesslaufzeit gehalten — kein Re-Load zwischen Tool-Calls.
- **Staleness-Refresh:** Vor jedem `GetCurrentSolution()`-Aufruf prüft der Server pro Document per `mtime` + SHA-256, ob sich Dateien geändert haben; betroffene Dokumente werden inkrementell via `WithDocumentText` aktualisiert. **Kein Komplett-Reload** bei Datei-Änderungen.
- **Verzeichnis-Sweep** (Phase 2 des Refresh): max-mtime über alle Subdirectories; nur bei Änderung wird `Directory.EnumerateFiles("*.cs", AllDirectories)` gefeuert (aufwendig, daher nur bei Bedarf).
- **Kein DI-Container:** Bewusste Architekturentscheidung (siehe `AiNetLinterRichtlinien.mdc §2`) — Tools bekommen `McpCodeGraphServer` per Delegate-Closure.
- **Stdout-Schutz:** `McpLintConsole` leitet `WriteLine` zwingend nach `stderr` um. Strukturelle Absicherung gegen stdout-Leaks, getestet via `McpServerCommandJsonRpcFramingTests` (spawned Subprozess, prüft **jede** stdout-Zeile auf gültiges JSON-RPC-Frame).
- **AIContextFootprint-Disziplin:** Tool-Klassen sind in mehrere Registrar-Klassen aufgeteilt (`SymbolGraphToolRegistrations`, `FileStructureToolRegistrations`, `AnalysisToolToolRegistrations`, `SymbolBodyToolRegistrations`), damit der projektweite `MaxAIContextFootprint`-Limit (2500) pro File eingehalten wird. Jede Klasse hat einen `PathOverrides`-Eintrag in `rules.json` mit eigenem höheren Limit (2520-2870).

### 3.2 Tool-Inventar (10 Tools, 1 Resource)

#### 3.2.1 Symbolgraph-Tools (`SymbolGraphToolRegistrations`)

| Tool | Input-Schema | Output-Format | Zweck | Roslyn-API |
| :--- | :--- | :--- | :--- | :--- |
| `find_symbol` | `namePattern: string` (required), `kind?: "class"|"klasse"|"interface"|"method"|"methode"|"property"`, `maxResults: int = 50` | Plain-Text-Liste `Datei:Zeile - Kind: SymbolDisplayString` | Substring-Suche über C#-Symbole (Klassen, Methoden, Properties, Interfaces) | `SymbolFinder.FindSourceDeclarationsAsync` |
| `find_references` | `symbolIdentifier: string` (DocId ODER Datei:Zeile:Spalte ODER qual. Name), `maxResults: int = 50`, `depth: int = 1 (cap 3)`, `200-Knoten-Cap` | Plain-Text-Liste von Aufrufstellen, bei `depth > 1` aggregierte Top-N | Direkte + transitive Aufrufstellen eines Symbols | `SymbolFinder.FindReferencesAsync` + BFS |
| `get_impact` | `gitRef?: string` (leer=uncommittet) **ODER** `symbolIdentifier?: string` (exklusiv), `maxResults: int = 50`, `depth: int = 1` | Plain-Text-Liste von Aufrufstellen geänderter Signaturen | Impact-Analyse für geplante Refactorings (Git-Diff oder Single-Symbol) | `DiffImpactAnalyzer.AnalyzeAsync` (Git-Branch) oder `FindReferences` (Symbol-Branch) |
| `get_type_hierarchy` | `typeIdentifier: string` (DocId/Position/Name) | Markdown: Basisklassen, Interfaces, abgeleitete Typen, heuristische DI-Registrierungen (`AddScoped<T>`, `AddSingleton<T>`, `AddTransient<T>`) | Vererbungs-Hierarchie + DI-Container-Hinweise | `ITypeSymbol.BaseType`, `AllInterfaces`, `GetTypeMembers` + Regex über Solution-Dateien |

**Symbol-Resolution-Mechanik** (`FindReferencesTool.ResolveSymbolAsync` + `SymbolIdentifierResolver`):
1. Versuch: stabile `DocumentationCommentId` (z. B. `M:AiNetLinter.Mcp.Tools.FindSymbolTool.ExecuteAsync`) → überlebt Zeilenverschiebungen, disambiguiert Overloads.
2. Fallback: `Datei:Zeile:Spalte` → `document.FindToken(position).Parent`.
3. Fallback: qualifizierter/teil-qualifizierter Name → `SymbolFinder.FindSourceDeclarationsAsync` + EndsWith-Match.
4. Bei Mehrdeutigkeit: `AMBIGUOUS_SYMBOL` mit Kandidaten-Liste im `context`-Feld.
5. Accessor-Symbole (Property `get_/set_`, Event `add_/remove_`) werden auf den Owner normalisiert.

#### 3.2.2 Datei-Struktur-Tools (`FileStructureToolRegistrations`)

| Tool | Input-Schema | Output-Format | Zweck | Roslyn-API |
| :--- | :--- | :--- | :--- | :--- |
| `get_file_skeleton` | `filePath: string` (relativ oder absolut) | Markdown-Skelett: Typen + Signaturen ohne Bodies | Struktur-Überblick einer einzelnen `.cs`-Datei | `SkeletonMapBuilder.ExtractFromDocumentAsync` + `SkeletonMarkdownRenderer.Render` |
| `get_index_scope` | — | Plain-Text: `.cs` (N), `.css` (N), `.html` (N), `.js` (N), `.razor` (N), `.xaml` (N) | Orientierung VOR anderen Tool-Calls (welche Dateitypen sind überhaupt indiziert?) | `solution.Projects` → `project.Documents` |
| `get_hotspots` | `scopeFilter?: string` (Projekt-Name oder solution-relativer Pfad) | Plain-Text: Liste von Dateien, die `MaxLineCount` aus `rules.json` (Default 700) nahekommen oder überschreiten | Drift-Signal vor geplantem Edit (welche Dateien sind zu groß?) | File-Walk + `File.ReadAllLines().Length` |

#### 3.2.3 Analyse-Tools (`AnalysisToolRegistrations`)

| Tool | Input-Schema | Output-Format | Zweck | Roslyn-API |
| :--- | :--- | :--- | :--- | :--- |
| `get_violations` | `scopeFilter?: string` | Markdown: Tabelle `Datei \| Zeile \| Regel \| Details` + Severity-Split + optionale `Basis: Default-Regeln`-Header-Zeile | Aktuelle Lint-Verstöße der resident gehaltenen Solution (derselbe Engine wie CLI-Batch, aber **kein Disk-Cache**) | `LinterEngine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0)` |
| `search_pattern` | `pattern: string` (required), `isRegex: bool = false`, `maxResults: int = 50` | Plain-Text: Treffer-Liste | Fallback für Nicht-C#-Dateien (.js/.razor/.xaml/.html/.css/.json/.yml/.md) oder reine String-Suche | `Directory.EnumerateFiles` + `Regex`/`Contains`, alle Dateitypen |

#### 3.2.4 Symbol-Body-Tool (`SymbolBodyToolRegistrations`)

| Tool | Input-Schema | Output-Format | Zweck | Roslyn-API |
| :--- | :--- | :--- | :--- | :--- |
| `get_symbol_body` | `identifier: string` (DocId/Position/Name), `maxBodyLines: int = 80` | Markdown: `### Kind: DisplayName` + `id: DocId` + `csharp-Codeblock` | Source-Body eines Symbols (Hart-Kappung mit Ellipse-Indikator + Voll-Längen-Hinweis) | `ISymbol.DeclaringSyntaxReferences` + `syntax.ToFullString()` |

#### 3.2.5 Resource (`OverviewResourceRegistration`)

| Resource-URI | MimeType | Inhalt | Zweck |
| :--- | :--- | :--- | :--- |
| `ainetlinter://overview` | `text/markdown` | 1-Satz-Summary pro Tool + Server-Status (geladene Solution, verwendete `rules.json` oder Default-Hinweis) | Erstorientierung für Agenten, die den Server zum ersten Mal sehen; explizit per `ServerInstructions` beworben |

### 3.3 Datenfluss

```
MCP-Host (Claude Code/Cursor/...)
    │  JSON-RPC 2.0 über stdin/stdout
    ▼
StdioServerTransport
    │  JsonRpc-Frame-Parsing
    ▼
McpServer (SDK)
    │  ToolDispatch
    ▼
Tool-Registrar-Lambda (Closure auf McpCodeGraphServer)
    │  argument validation
    │  LoadState == Loading ? McpToolResults.Loading() : …
    ▼
Tool-ExecuteAsync (in src/AiNetLinter/Mcp/Tools/*Tool.cs)
    │  delegate to Scanner/Helper (in src/AiNetLinter/Mcp/Tools/*Scanner.cs)
    │  ─────────────────────────────────────────
    │  Scanner: Roslyn-API-Calls (SymbolFinder, GetCompilation, WithDocumentText)
    │  ─────────────────────────────────────────
    │  BuildAggregateWarningAsync → McpCompileDiagnostics (Compile-Fehler pro Tool)
    │  TruncateLines/TruncateFileList → McpTruncation (mit Meta-Zeile)
    ▼
CallToolResult { Content: [TextContentBlock { Text }] }
    │  JSON-RPC response
    ▼
stdout
```

**Drei Schutzmechanismen:**

1. **Stdout-Schutz:** `McpLintConsole.Instance` als 3. Parameter an `McpServerCommand.RunAsync` — `WriteLine` geht zwingend nach `stderr`, niemals nach `stdout`.
2. **Frame-Test:** `McpServerCommandJsonRpcFramingTests` spawned `AiNetLinter.exe` als Subprozess, schreibt `initialize` + `tools/list` + `tools/call` manuell auf stdin und prüft **jede** Zeile auf stdout als gültigen JSON-RPC-Frame (`jsonrpc == "2.0"`).
3. **Trunkierungs-Meta-Zeile:** Einheitliches Format `[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]` (oder Datei-Variante), damit Agent-Loop erkennt, dass es eine unvollständige Antwort ist.

### 3.4 Konfigurations-Lifecycle

```
McpServerCommand.RunAsync
    │  args.ConfigPath explizit? → verwenden
    │  sonst: TryResolveRulesJsonPath() → neben Solution-Datei
    │  sonst: null → usedDefaultConfig = true
    ▼
ResolveConfig() → ConfigLoader.TryLoadConfig(path, isRequired: false)
    │  null? → new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() }
    ▼
McpCodeGraphServer.Config (exposed als ILinterEngineConfig)
    │
    ▼ (nur get_violations)
GetViolationsTool → GetViolationsScanner → LinterEngine.RunAsync(config, …)
    │
    │  UsedDefaultConfig → Header-Zeile "Basis: Default-Regeln, keine rules.json gefunden"
```

### 3.5 Error-Reporting (strukturierte Codes)

| Code | Bedeutung | Beispiel-Tool |
| :--- | :--- | :--- |
| `RESOURCE_NOT_FOUND` | Datei/Solution-Pfad nicht gefunden | `get_file_skeleton`, Server-Boot |
| `AMBIGUOUS_SOLUTION` | Mehrere `.sln`/`.slnx` im `cwd` ohne `--path` | Server-Boot |
| `SOLUTION_NOT_LOADED` | Server startete ohne geladene Solution | alle Tools |
| `SYMBOL_NOT_FOUND` | Identifier löst zu keinem Symbol auf | `find_references`, `get_type_hierarchy`, `get_symbol_body` |
| `AMBIGUOUS_SYMBOL` | Identifier löst zu mehreren Symbolen auf | `find_references` |
| `INVALID_ARGUMENT` | Ungültiger Parameter (z. B. leere Regex, exklusive Parameter beide gesetzt) | `get_impact`, `find_symbol`, `search_pattern` |
| `WORKSPACE_DIAGNOSTIC` | Roslyn/MSBuild-Compile-Fehler | Defensiv-Wrapper in allen Symbolgraph-Tools |
| `ANALYSIS_FAILED` | Analyse-Laufzeit-Fehler (z. B. Git-Diff fehlgeschlagen) | `get_impact` (Git-Branch) |

Format: `[ERROR]: <CODE>: <message> | context: <data> | hint: <suggestion>` (wiederverwendet von `LinterErrorFormatter`).

### 3.6 Beobachtbarkeit (opt-in)

`--mcp-log <pfad>` (oder ohne Wert = Default-Pfad `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`):
- **Default deaktiviert** (kein File I/O, kein Overhead).
- JSONL-Format pro Zeile: `ts`, `tool`, `args` (max 200 Zeichen), `lines`, `truncated`, `duration_ms`, `empty`.
- Error-Entries bei unbehandelten Exceptions: `level=error`, `error_type`, `error_message`, `stack_trace` (max 4 KB).
- Leere Log-Dateien werden beim Dispose automatisch gelöscht.

---

## 4. Phase 3 — Bestehende Regeln, Generatoren, Diagnostics

### 4.1 Regel-Inventar (Roslyn-Checkers, 25 Stück)

In `src/AiNetLinter/Core/Checkers/` und registriert in `RuleRegistry.General.cs` (25.7 KB), `RuleRegistry.Architecture.cs` (5.2 KB), `RuleRegistry.Web.cs` (21.3 KB):

| Checker | Regel | Zweck (1 Satz) |
| :--- | :--- | :--- |
| `SealedClassChecker` | `EnforceSealedClasses` | Klassen `sealed`, wenn nicht explizit zur Vererbung vorgesehen |
| `ValueObjectChecker` | `EnforceValueObjectContracts` | Value Objects mit `Equals`/`GetHashCode`/`==`/`!=`-Konsistenz |
| `NamingChecker` | `EnforcePascalCase`, `EnforceAsciiIdentifiers`, `EnforceSemanticNaming` | Naming-Standards, ASCII-only Identifier, semantische Methodennamen (`Get/Find/Compute/…`) |
| `DynamicTypeChecker` | `AllowDynamic` | `dynamic`-Verwendung unterbinden (Halluzinations-Risiko) |
| `PhantomDependencyChecker` | `DetectAndBanPhantomDependencies` | Nicht auflösbare Namespaces + Reflection-Lade-APIs verbieten |
| `NestedTypesChecker` | `BanPublicNestedTypes` | Öffentliche nested types verbieten (AI-Kontext-Footprint) |
| `MiddleManChecker` | `AvoidExcessiveMiddleMen` | Reine Forwarding-Klassen mit zu hohem Forwarding-Ratio (>60%) entlarven |
| `ImmutabilityChecker` | `EnforceExplicitStateImmutability` | Mutationen an Suffix-Klassen (`Dto`, `Entity`, `Model`, `Request`, `Response`, `Command`) prüfen |
| `MinimalApiChecker` | `EnforceMinimalApiAsParameters` | ASP.NET Minimal API Endpoints sollen (Request, CancellationToken) als Parameter empfangen |
| `StateChecker` | `EnforceNoSilentCatch`, `EnforceResultPatternOverExceptions` | `catch`-Klauseln dürfen nicht leer sein; Result-Pattern statt Exceptions |
| `NamespaceCouplingChecker` | `ForbiddenNamespaceDependencies` | Verbotene Namespace-Abhängigkeiten (Architektur-Constraint) |
| `InheritanceDepthChecker` | `MaxInheritanceDepth` | Vererbungstiefe > 3 flaggen |
| `ComplexityChecker` | `MaxCyclomaticComplexity`, `MaxCognitiveComplexity`, `MaxSwitchArms`, `MaxMethodLineCount` | 4 Metriken in einer Klasse |
| `MethodClassifier` | `MaxMethodParameterCount`, `MaxMethodOverloads`, `MinCognitiveComplexityForTest` | Methoden-Signatur-Konsistenz |
| `LinqChainLengthChecker` | `MaxLinqChainLength` | LINQ-Chain-Länge begrenzen (Whitelist für Builder-Ketten) |
| `PublicMembersChecker` | `MaxPublicMembersPerType` | Pro-Typ max 15 Public Members (AI-Kontext-Footprint) |
| `BoolParameterChecker` | `MaxBoolParameterCount` | Max 1 Bool-Parameter pro Methode (semantische Klarheit) |
| `ControlFlowChecker` | `MaxDirectoryChildren`, `MaxDirectoryDepth`, `MaxPartialClassFiles` | Verzeichnis- und Partial-Class-Layout |
| `AsyncVoidChecker` | `BanAsyncVoid` | `async void` außerhalb von Event-Handlern verbieten |
| `BlockingTaskChecker` | `BanBlockingTaskAccess` | `.Result`/`.Wait()` blockierende Calls verbieten |
| `ScopeChecker` | `--include-projects`, `--exclude-projects`, `--include-namespaces`, `--exclude-namespaces` | Project/Namespace-Scope-Filter (CLI, derzeit nicht im MCP exposeiert) |
| `UiFileSeparationChecker` | `BlazorRequireCodeBehind`, `BlazorRequireCssIsolation` | Blazor-Trennung von Logik/Styles |
| `WpfSeparationChecker` | `WpfRequireMinimalCodeBehind` | WPF-Code-Behind-Disziplin |
| `WebFileSeparationChecker` | Web-Asset-Analyse (CSS/JS/Razor) | Datei-Separation für Web-Assets |
| `ValueObjectChecker` | `EnforceValueObjectContracts` | Value-Object-Konsistenz |

### 4.2 Generatoren

| Generator | Zweck | Output |
| :--- | :--- | :--- |
| `AgentRulesGenerator` | Synchronisiert `rules.json` → `.agents/rules/AiNetLinter.mdc` | Lint-Metrik-Übersicht für Coding-Agents |
| `RepoPlaybookGenerator` + `PlaybookSyntaxWalker` | Analysiert Roslyn-Syntax-Tree und assembliert LLM-Audit-Playbook | Markdown-Playbook mit Code-Skeletons, Method-Listen |
| `--map vocabulary` | Liste aller Identifier-Häufigkeiten | Häufigkeits-Wörterbuch (AI-Vokabular) |
| `--map structure` | Verzeichnis-/Namespace-Hierarchie | Struktur-Übersicht |
| `--map hotspots` | Dateien nahe/über `MaxLineCount` | Drift-Signal |
| `--map skeleton` | Datei-Skelett (Typen + Signaturen ohne Bodies) | Komprimierte Code-Repräsentation |
| `--eval naming-drift` / `--eval architecture-intent` | Vollständige Audit-Prompts mit Spec-Datei + frischer Evidenz | LLM-Audit-Eingabe |

### 4.3 Diagnostics / Performance

`PerformanceProfiler` misst pro Lint-Lauf: Solution-Load-Zeit, Compile-Zeit, Checker-Phase-Zeiten, GetDiagnostics-Overhead, Output-Format-Zeit. Profiler-Ausgabe wird via `--verbose` aktiviert (deaktiviert in `rules.json` via `EnablePerformanceProfiling: false`). Der MCP-Server nutzt `NullPerformanceProfiler` (kein Overhead im Hot-Path).

### 4.4 Konventionen (aus `rules.json`)

- **AI-Kontext-Footprint:** `MaxAIContextFootprint=2500` (transitive Codezeilen pro Klasse), `FootprintIgnoreTypeNames=["LinterEngine", "NamingChecker"]` (Tool-Code exempt). `PathOverrides` hebt das Limit für **alle** MCP-Quelldateien auf 2520-2870 an (siehe §5.2).
- **Compound Suppressions:** Kontextabhängige Regelunterdrückung (z. B. `MaxMethodLineCount` mit `SeverityOverride: "warning"` wenn CC≤3 und CyclomaticComplexity≤3).
- **Project Overrides:** `*.Tests` lockert Limits (kein Sealed, MaxMethodLineCount=100, mehr Exempt-Suffixes).
- **Test-Sentinel:** Klassen-Pattern `{Name}Tests`, `{Name}IntegrationTests`, etc. müssen statische Methoden haben, die auf Test-Attribute hindeuten.

---

## 5. Phase 4 — Tests

### 5.1 Test-Inventar (`src/AiNetLinter.Tests/`)

```
Architecture/    (1 File: ArchitectureTests.cs)
Baseline/        (Linter-Engine + Baseline-Tests)
Cache/           (Disk-Cache-Tests)
Cli/             (CLI-Parser-Tests)
Commands/        (Command-Dispatcher-Tests)
Configuration/   (Config-Loading-Tests)
Core/            (Rule-Registry-Tests)
Diagnostics/     (Performance-Profiler-Tests)
Evals/           (Eval-Generator-Tests)
FalsePositives/  (Regressionstests gegen Checker-False-Positives)
Fixtures/        (LoadFixture, McpLiveRepositoryFixture, SymbolGraphMcpFixture, …)
Maps/            (Map-Builder-Tests)
Mcp/             (★ MCP-Tests, ~30 Files)
Metrics/         (Complexity/AIContext-Tests)
Output/          (Markdown-Formatter-Tests)
Suppression/     (DisableAll/IgnoreSuppressions-Tests)
Web/             (CSS/JS/Razor-Analyser-Tests)
```

### 5.2 MCP-Tests im Detail

**Unit-Tests** (schnell, ohne Live-Repo):
- `McpCodeGraphServerTests` — Server-Lifecycle, Staleness-Refresh, Concurrent-Calls, Solution-Update.
- `McpCodeGraphServerConstructorTests` — Options-Validation.
- `McpCodeGraphServerFileDiscoveryTests` — Verzeichnis-Sweep, neue Dateien hinzufügen.
- `McpCodeGraphServerStalenessMtimeCacheTests` — mtime+SHA-256-Hash-Logik.
- `McpServerOptionsBuilderTests`, `McpServerOptionsFactoryTests` — Options-Builder-Pattern.
- `McpToolResultsTests` — `Error`, `SolutionNotLoaded`, `SymbolNotFound`, `AmbiguousSymbol`, `InvalidArgument`, `FileNotFound`, `Loading`, `CompilationError`.
- `McpCallLogTests` — JSONL-Format, Truncation, Error-Schema, leere-Datei-Autodelete.
- `OverviewResourceRegistrationTests` — Tool-Name-Parität.
- `SymbolGraphToolRegistrationsTests` — Tool-Collection-Wiring.
- `Mcp/Tools/*ToolTests.cs` — Pro Tool (12 Dateien): `FindSymbolToolTests`, `FindReferencesToolTests`, `GetImpactToolTests`, `GetTypeHierarchyToolTests`, `GetFileSkeletonToolTests`, `GetSymbolBodyToolTests`, `GetViolationsToolTests`, `GetHotspotsToolTests`, `GetIndexScopeToolTests`, `SearchPatternToolTests`, `CallGraphTraversalTests`, `DiRegistrationHeuristicsTests`.
- `McpTestClient*` — Test-Harness für STDIO-Roundtrip (mit Retry/Parallel-Tests).

**Integration-Tests** (`[Trait("Category", "Integration")]`):
- `McpServerAllToolsE2ETests` — E2E aller 10 Tools gegen `SymbolGraphMcpFixture` (subprozess-basiert).
- `McpLiveRepositoryTests` — Live-Tests gegen das AiNetLinter-Repo selbst (validiert Real-World-Szenarien).
- `McpServerCommandJsonRpcFramingTests` — Stdout-Schutz, jede Zeile valides JSON-RPC.
- `McpDocumentationSmokeTests` — Embedded-Resources vs. Dateisystem-Konsistenz.
- `McpTestClientParallelTests` — Concurrency-Stresstest.

### 5.3 Test-Strategie

- **Unit/Integration-Split** per xUnit `Trait("Category", "Integration")` (siehe `AGENTS.md §2`).
- **Fixture-Hierarchie:** `LoadFixtureBuilder`/`LoadFixtureHandle` (minimale Roslyn-Workspaces in temp dirs) → `McpLiveRepositoryFixture` (das AiNetLinter-Repo selbst) → `SymbolGraphMcpFixture` (vorkompiliertes Solution + Test-Client).
- **Subprocess-Konflikt-Vermeidung:** `SubprocessConcurrencyGate` serialisiert parallele Test-Klassen, weil `MSBuildLocator` prozessglobal ist.
- **E2E-Tests** starten den `AiNetLinter.exe`-Subprozess mit `--mcp-server` und rufen Tools via JSON-RPC auf — keine SDK-Mocking.

### 5.4 Coverage-Schätzung (qualitativ)

- ✅ **Stark:** Boot-Sequenz, Staleness-Refresh, Error-Reporting, Trunkierung, Tool-Argument-Validierung, Stdout-Schutz.
- ⚠️ **Mittel:** Concurrency, Locking, große Lösungen.
- ❌ **Schwach:** Real-World-Performance (Lasttests gegen 100k+ LOC-Solutions), Memory-Leak-Tests bei Langzeit-Server-Betrieb, langsamer Datei-Walk bei Millionen-Dateien-Solutions.

---

## 6. Phase 5 — Stärken-Schwächen-Analyse

### 6.1 Stärken (für Agent-Workflows)

1. **Strukturell abgesicherter JSON-RPC-Transport:** `McpLintConsole` + `McpServerCommandJsonRpcFramingTests` garantieren, dass keine `Console.WriteLine` aus versehenen Tool-Helpern den Frame zerstört. Das ist in der MCP-Server-Welt ein häufiger Bug und hier konsequent vermieden.
2. **Resident + inkrementell:** Solution wird einmal geladen und pro Tool-Call nur per mtime+SHA-256 inkrementell aktualisiert. Kein wiederholter MSBuildWorkspace-OpenSolutionAsync pro Tool-Call (wäre bei großen Solutions unbenutzbar).
3. **Stabile Symbol-IDs:** `DocumentationCommentId` als primärer Identifier-Format überlebt Zeilenverschiebungen, was für iterative Agent-Loops kritisch ist. Position/Name als Fallbacks.
4. **Best-Effort-Resilience:** Load-Fehler, Compile-Fehler, einzelne Datei-Löschungen crashen den Server nicht — strukturierte Fehler werden an den Agent zurückgegeben. Defensiv-Wrapper in jedem Tool.
5. **Opt-in Beobachtbarkeit:** `--mcp-log` ist default aus. Aktivierung in Production ohne Recompile. JSONL-Format ist trivial parsbar.
6. **C#-only-Klarheit:** `ServerInstructions` nennt beim Handshake explizit die C#-only-Grenze, damit der Agent nicht versucht, ein JavaScript-Symbol via `find_symbol` zu suchen. `search_pattern` als expliziter Fallback dokumentiert.
7. **Konfigurations-Kontinuität:** MCP-Server nutzt dieselbe `rules.json` wie CLI-Batch (`ConfigLoader.TryLoadConfig`), mit explizitem `UsedDefaultConfig`-Marker in `get_violations`-Output. Agent sieht, ob Lint-Ergebnisse aus projekteigener Config stammen.
8. **Auto-Discovery der Solution:** `args: ["--mcp-server"]` ohne `--path` reicht — der Server sucht im `cwd` nach genau einer `.sln`/`.slnx`. Pro-Projekt-Registrierung im Host empfohlen.
9. **AIContextFootprint-Disziplin:** Jede Tool-Registrar-Klasse hat `PathOverrides` mit eigenem höheren Limit (2520-2870) in `rules.json`. Reflektiert ein internes Bewusstsein, dass das Tool-Wachstum pro File begrenzt werden muss.
10. **Mehrdeutigkeits-Handling:** `AMBIGUOUS_SYMBOL` mit Kandidaten-Liste im `context`-Feld gibt dem Agent die nötige Information, um den Identifier zu präzisieren.

### 6.2 Schwächen (für Agent-Workflows)

1. **Read-only MCP-Server.** Es gibt **kein** Tool zum Editieren, Anwenden von Auto-Fixes, oder Vorschau von Änderungen. Agent muss nach dem Tool-Call eigene `Edit`/`Write`-Calls machen. Kein atomares Edit + Verify.
2. **Kein Edit-Preview / Diff-Output.** Kein Tool, das "wenn ich dieses Symbol so umbenenne, sind N Call-Sites betroffen + so-und-so ändern sich". `get_impact` zeigt nur die IST-Situation, nicht die WAS-WÄR-WENN-Situation.
3. **Symbolgraph nur read-only.** Keine API für "gib mir alle Symbole, die diesen Typ als Parameter haben" (Inversion von `find_references`), keine API für "gib mir die generischen Constraints, die dieser Typ erfüllen muss".
4. **Keine Abhängigkeitsgraph-Queries.** Kein `get_dependencies(type)`, kein `get_package_graph()`. NuGet-Package-Beziehungen sind über Roslyn-Workspace prinzipiell zugänglich (`Project.MetadataReferences`), aber nicht exposeiert.
5. **`get_impact` ist Git-Diff-only, nicht Workspace-Diff.** Der Agent kann nicht fragen "wenn ich `X` in `Y` ändere, was passiert?" — er muss tatsächlich auf der Platte ändern, dann den Git-Diff ziehen.
6. **C#-only-Fokus.** `find_symbol`, `find_references`, `get_impact`, `get_type_hierarchy`, `get_file_skeleton`, `get_violations`, `get_symbol_body` — alle 7 explizit als "Deckt nur .cs-Dateien ab" dokumentiert. `search_pattern` ist der einzige Fallback, aber liefert keine Typ-Information. `.razor`/`.xaml`/`.html`/`.css`/`.js`-Tooling fehlt komplett.
7. **Kein Tool für Test-Coverage-Analyse.** Trotz `TestSentinel` in `rules.json` und `TestCoverageResolver` in `Core/` — keine MCP-Exposition. Agent kann nicht fragen "welche Klassen in `X` haben keine direkten Tests?"
8. **Kein `get_call_tree`.** Konzept-Vorgabe: "bewusst kein `get_call_tree`, depth-Parameter in `find_references`/`get_impact` deckt das ab". Aber: depth-Parameter gibt aggregierte Top-N-Liste, keinen Baum. Für strukturelle Analyse (z. B. "Zeige mir den Aufruf-Graphen als ASCII-Tree") unbrauchbar.
9. **Kein `get_namespace_overview` / Projekt-Statistik.** Kein Tool, das "Zeige mir pro Projekt: N Klassen, Ø CyclomaticComplexity, Top 3 Hotspots" liefert. `--map`-Subcommands sind CLI-only, nicht via MCP.
10. **Kein Server-Health/Memory-Tool.** Kein Tool, das "wie viel Memory hält der Workspace, wann war letzter Refresh, wie viele Solution-Updates seit Start?" liefert. `--mcp-log` zeigt nur abgeschlossene Calls, keine Aggregate.
11. **Kein Hot-Reload bei `rules.json`-Änderung.** Ändert der Nutzer die `rules.json` während des Server-Laufs, ignoriert der Server das. `get_violations` würde weiter mit der alten Config laufen. Kein `reload_config`-Tool.
12. **Kein Multi-Solution-Support.** Ein Server-Prozess = eine Solution. Agent mit Multi-Repo-Workflow muss mehrere Server starten. Kein Workspace-übergreifendes `find_symbol` ("in welcher Solution ist `X` definiert?").
13. **Kein Structured-Output-Mode.** Alle Tool-Antworten sind Plain-Text oder Markdown. Agent-LLMs müssen parsen. Kein `--format json` für programmatische Konsumption (z. B. eigene Test-Harness, CI-Integration).
14. **Kein Pagination für grosse Ergebnisse.** `get_violations` kann bei 10.000+ Violations einen riesigen String zurückgeben, der das MCP-Token-Budget sprengt. `maxResults` (50) ist hardcoded für die Symbolgraph-Tools; bei `get_violations` gibt es keine Pagination.
15. **Kein Tool für Auto-Fix-Preview.** `LinterAutoFixer` existiert in `Core/` (12 KB), wird via `--fix` aufgerufen — aber kein MCP-`get_fixes`/`apply_fix`-Tool. Agent kann nicht "Was würde der Auto-Fixer hier ändern?" fragen.
16. **Tool-Description-Sprawl.** Jede Tool-Description ist mehrzeilig und enthält viel C#-only-Hinweis, Trunkierungs-Verhalten, depth-cap-Erklärung. Agent-LLM muss alles lesen, um zu verstehen, ob das Tool passt. Keine kompakte "One-Liner"-Version.
17. **Kein Concurrency-Limit / Rate-Limit.** Ein Agent kann 100 `find_symbol`-Calls parallel feuern — der Server antwortet, aber es gibt keine Throttling. Bei großen Lösungen könnte das den Roslyn-Workspace überlasten.
18. **Kein `list_diagnostics` über alle Tools.** Wenn ein Tool scheitert, bekommt der Agent nur den Tool-spezifischen Error. Kein aggregierter "was läuft gerade schief?"-Channel.
19. **Kein File-Watcher-basierter Push.** Der Staleness-Check ist pull-basiert (bei jedem `GetCurrentSolution()`). Der Server benachrichtigt den Agent nicht aktiv über Änderungen. Für Watch-Mode-Workflows (z. B. Claude-Code-Edit-Loop) müsste der Agent pollen.
20. **Kein "Test-Run"-Tool.** `--map`/`--eval` werden ausschließlich als CLI-Subcommands exponiert, nicht als MCP-Tools. Agent kann nicht "generiere mir einen Naming-Drift-Audit-Prompt" via MCP triggern.

### 6.3 Lücken (häufige Agent-Tasks, die nicht unterstützt werden)

| Agent-Task | Aktuelle Unterstützung | Lücke |
| :--- | :--- | :--- |
| "Wo wird `Foo.Bar()` aufgerufen?" | `find_references` ✅ | — |
| "Wenn ich `Foo.Bar` in `Foo.Baz` umbenenne, was bricht?" | `get_impact` (mit `symbolIdentifier` + `depth`) ⚠️ | Nur IST-Situation, kein Preview |
| "Welche Klassen erben von `MyController`?" | `get_type_hierarchy` ✅ | — |
| "Was sind die Methoden dieser Datei?" | `get_file_skeleton` ✅ | — |
| "Zeig mir den Body von `MyClass.MyMethod`" | `get_symbol_body` ✅ | — |
| "Welche Lint-Fehler hat diese Datei?" | `get_violations` mit `scopeFilter` ✅ | — |
| "Welche Lint-Fehler hat **diese spezifische Zeile**?" | ❌ | Kein zeilengenauer Filter |
| "Zeig mir die Datei-Größe-Top-10" | `get_hotspots` ✅ | — |
| "Was ist der Aufruf-Graphen von `MyService`?" | `find_references` mit `depth` ⚠️ | Aggregierte Top-N, kein Tree |
| "Wo ist `IFoo` als Interface-Constraint verwendet?" | `find_references` + manuelles Filtern ⚠️ | Kein Constraint-Filter |
| "Welche Klassen haben keine direkten Tests?" | ❌ | `TestCoverageResolver` existiert, aber nicht MCP-exposeiert |
| "Zeig mir alle C#-Files, die 'TODO' enthalten" | `search_pattern` ✅ | — |
| "Wie hängt `ProjectA` von `ProjectB` ab?" | ❌ | Project-Reference-Graph fehlt |
| "Was ist der transitive Kontext-Footprint dieser Klasse?" | ❌ | `AIContextFootprintCalculator` existiert, aber nicht MCP-exposeiert |
| "Was wären die Auto-Fix-Änderungen an `Foo.cs`?" | ❌ | `LinterAutoFixer` existiert, aber nicht MCP-exposeiert |
| "Generiere einen Naming-Drift-Audit-Prompt" | ❌ | `--eval` ist CLI-only |
| "Welche Dateien gehören zu welchem Projekt?" | ❌ | Kein `list_projects` |
| "Wo ist die Konfiguration für `Foo` definiert?" (appsettings.json etc.) | `search_pattern` ⚠️ | Kein "config-aware"-Wissen |
| "Editiere `Foo.cs`, Zeile 42: `var x = 1;` → `var x = 2;`" und validiere | ❌ | Kein `apply_edit` / kein `validate_edit` |
| "Welche offenen PRs / Git-Status?" | `get_impact` (Git-Branch) ⚠️ | Nur Diff, kein Status-Report |
| "Liste alle NuGet-Packages + Versionen" | ❌ | Nicht exposeiert |
| "Wo werden `DateTime.Now` Aufrufe gemacht (auf Testbarkeit prüfen)?" | `search_pattern` ✅ | Plain-Text, keine AST-Semantik |
| "Welche Exceptions wirft diese Methode?" | `get_symbol_body` + manuelles Lesen ⚠️ | Kein semantischer "throws"-Filter |
| "Ist diese Datei im aktuellen Baseline-Ratchet?" | ❌ | Baseline-Filterung nicht MCP-exposeiert |

### 6.4 Performance-Indizien

- **Gute Indizien:**
  - Inkrementelles `WithDocumentText` statt Komplett-Reload (`McpCodeGraphServerRefresh.RefreshModifiedDocuments`).
  - Max-200-Knoten-Cap in `CallGraphTraversal` (verhindert exponentielle Explosion).
  - 50-Default-Trunkierung + Meta-Zeile.
  - Lazy `LoadState`-Peek (`_loadTask.IsCompletedSuccessfully` statt `GetAwaiter().GetResult()` im Property).
  - `McpCompileDiagnostics` cached Compilation pro Project.
  - `GetCurrentSolution()` ist synchron, kein per-Call `await` → kein Lock-Contention-Overhead.

- **Risiko-Indizien:**
  - `McpCompileDiagnostics.GetErrorsByFileAsync` ruft `project.GetCompilationAsync()` pro Tool-Call für **jedes** Tool (über 7 Tools, das immer wieder). Kein Compilation-Cache im MCP-Server. Bei großen Solutions potenziell teuer.
  - `search_pattern` läuft in `Task.Run` (CPU-bound Walk), aber kein Caching der Datei-Inhalte. Bei wiederholten `search_pattern`-Calls wiederholter Full-Walk.
  - `get_violations` läuft die volle `LinterEngine.RunAsync` synchron pro Call. Bei großen Lösungen und häufigen Lint-Checks teuer — aber `noCache: true` ist explizit (Cache-Isolation zu parallelen CLI-Läufen).
  - `FindSymbolScanner` läuft `SymbolFinder.FindSourceDeclarationsAsync` ohne sichtbares Caching. Pro Call voller Symbolgraph-Walk.
  - Verzeichnis-Sweep (`SweepForNewFiles`) ist `SearchOption.AllDirectories` + Regex-Filterung — bei großen Solutions mit 10k+ Dateien potenziell langsam. Wird aber nur bei Max-mtime-Change getriggert (gated).
  - **Kein Memory-Profiler, keine Health-Tool, keine Long-Running-Tests.** Real-World-Memory-Leak-Risiko bei 8h-Server-Betrieb nicht abgesichert.

---

## 7. Phase 6 — Vergleichende Tabelle (AiNetLinter heute vs. CodeGraph-Perspektive)

CodeGraph-Perspektive = das, was ein vollständiger agentic Code-Intelligence-Server leisten würde (z. B. was GitHub Copilot Workspace, Cursor Composer, oder ein dedizierter CodeGraph-MCP wie `mcp-server-codegraph` heute bieten).

| Capability | AiNetLinter heute | CodeGraph (Vollbild) | Lücke? |
| :--- | :--- | :--- | :--- |
| **Symbol-Resolution (C#)** | `find_symbol` (Substring), `find_references` (mit depth) | Symbol-Resolution + semantische Nähe, Fuzzy-Match, Concept-Cluster | ⚠️ Teilweise |
| **Editier-Operationen** | ❌ Keine | `apply_edit`, `validate_edit`, `preview_edit`, `rollback_edit` | ❌ **Groß** |
| **Dependency-Graph** | ❌ Keine | `get_dependencies`, `get_dependents`, `get_package_graph`, NuGet-Transitive | ❌ **Groß** |
| **Type-Inference / Constraints** | ❌ Keine | "Wo wird `T : IFoo` constraint verwendet?", Generic-Resolution | ❌ Mittel |
| **Test-Coverage** | `TestSentinel` in `rules.json` (kann via `get_violations` geprüft werden), aber `TestCoverageResolver` nicht MCP-exposeiert | `get_test_coverage`, `get_untested_classes`, `get_mutation_score` | ❌ **Groß** |
| **Linting (C#)** | `get_violations` mit `scopeFilter` (50+ Regeln) | Linting (multi-language) | ✅ C# only |
| **Linting (multi-language)** | `get_violations` ist C#-only, `search_pattern` ist Fallback | Linting über Sprachgrenzen | ⚠️ Teilweise |
| **Performance-Hotspots** | `get_hotspots` (File-Size only) | Hot-Spots nach CPU, IO, Concurrency, Komplexität | ⚠️ Teilweise |
| **Call-Graph (Tree)** | `find_references` mit `depth` (aggregierte Top-N) | `get_call_tree` (echter Baum, z. B. als Mermaid) | ⚠️ Mittel |
| **Inheritance-Hierarchy** | `get_type_hierarchy` + DI-Heuristik | Hierarchie + Mixins + Traits + Extensions | ✅ |
| **Code-Context-Footprint** | `rules.json`-Setting + Linter-Report, nicht MCP | `get_ai_context_footprint(class)`, `get_optimal_edit_window(file, line)` | ❌ **Groß** |
| **Auto-Fix / Suggestion** | `--fix` CLI-only, kein MCP | `get_suggested_fixes(file, line)`, `preview_fix`, `apply_fix` | ❌ **Groß** |
| **Git-Integration** | `get_impact` (Git-Diff only) | `get_uncommitted_diff`, `get_pr_diff`, `get_blame`, `get_history` | ⚠️ Mittel |
| **Multi-Repo / Multi-Solution** | Ein Server = eine Solution | Workspace-übergreifende Queries | ❌ Mittel |
| **Structured-Output-Mode** | Plain-Text/Markdown only | JSON / JSONL / XML für programmatische Konsumption | ❌ Mittel |
| **Pagination** | `maxResults` (50, 200-Knoten-Cap) | `cursor`-basierte Pagination für sehr große Ergebnisse | ⚠️ Mittel |
| **Real-Time-Watch** | Pull-basierter Staleness-Check | Push-Notifications bei Datei-Änderungen | ❌ Mittel |
| **Eval-/Audit-Prompt-Generation** | `--eval` CLI-only | `generate_audit_prompt(type)`, `assemble_review_request` | ❌ Mittel |
| **Project-Map / Module-Overview** | `--map` CLI-only | `get_module_overview`, `get_architecture_summary` | ❌ Mittel |
| **Test-Discovery / Test-Run** | ❌ | `discover_tests`, `run_tests`, `get_test_results` | ❌ **Groß** |
| **Code-Generation** | ❌ | `generate_class`, `scaffold_module` | ❌ (außer Scope) |
| **LLM-Cost-Optimization** | `MaxAIContextFootprint` als Linter-Regel, nicht als Tool | `optimize_context_for(class)`, `minimize_dependencies` | ❌ Mittel |
| **Server-Health / Metrics** | `--mcp-log` (nur pro Call, keine Aggregate) | `get_server_health`, `get_memory_usage`, `get_call_stats` | ⚠️ Mittel |
| **Hot-Reload der Config** | ❌ (Config nur beim Start) | `reload_config`, `validate_config` | ❌ Mittel |
| **Concurrency / Rate-Limit** | Keine | `batch_queries`, `get_throughput_limits` | ❌ Mittel |
| **Authentication / Multi-Tenancy** | Keine (stdio = single-tenant) | OAuth / Tokens / Multi-User | ❌ (out of scope) |
| **Output-Caching** | `LinterEngine` hat Disk-Cache zwischen CLI-Läufen, aber `noCache: true` im MCP | Response-Cache pro Tool-Call, inkrementell invalidierbar | ❌ Mittel |
| **Source-Links / GitHub-Links** | ❌ | `get_source_link(symbol)`, `get_remote_url(file:line)` | ❌ Klein |
| **Locking-Concurrency** | `Lock` um `GetCurrentSolution()` (gut) | Verteilte Locks, Read-Write-Splitting | ✅ |
| **Refresh-Mechanismus** | mtime+SHA-256 inkrementell | FileSystemWatcher + Roslyn-Workspace-Sync | ✅ (etwas besser) |
| **Tool-Discovery-Resource** | `ainetlinter://overview` ✅ | `ainetlinter://overview` + `ainetlinter://stats` + `ainetlinter://config` | ⚠️ Teilweise |
| **Tool-Schema-Quality** | Mehrzeilige deutsche Description mit C#-only-Hinweis + Trunkierungs-Erklärung | Schema + `examples` (Few-Shot) + `tags` (Routing-Hints) | ⚠️ Mittel |
| **Tool-Count** | 10 Tools | 30-50 Tools (typischer CodeGraph-Server) | ⚠️ Mittel |
| **Transport** | stdio only | stdio + SSE + HTTP | ⚠️ Klein (meist ausreichend) |

**Zusammenfassung:** AiNetLinter deckt den **Lint- und Read-Only-Code-Intelligence-Bereich** für C# sehr gut ab. Die **CodeGraph-Kernkompetenzen** (Edit, Dependency-Graph, Test-Coverage, Auto-Fix-Preview) fehlen komplett. Das ist **kein Defizit** im engeren Sinne (das Tool positioniert sich als Linter, nicht als CodeGraph), aber für einen Agentic-Workflow wäre die Lücke spürbar.

---

## 8. Phase 7 — Quick-Win-Inventar (sortiert nach Aufwand)

### 8.1 Quick-Wins (~1 Stunde)

| # | Empfehlung | Use-Case | Aufwand |
| :--- | :--- | :--- | :--- |
| **Q1** | **Structured-Output-Mode als Tool-Parameter** (z. B. `outputFormat: "text"\|"json"`, Default `text`) | Wenn ein Agent programmatische Konsumption braucht (z. B. eigene Tests, CI-Integration), kann er JSON statt parsen. | 1h (10 Tools, simpler Wrapper) |
| **Q2** | **`reload_config`-Tool** (kein Server-Neustart nötig, lädt `rules.json` neu via `ConfigLoader.TryLoadConfig`) | Wenn der Nutzer die Lint-Regeln während des Editierens tunen will, muss er nicht den Server neu starten. | 1h |
| **Q3** | **`get_server_health`-Tool** (liefert `LoadState`, `_loadTask.Status`, `_fileState.Count`, `EntryCount` aus CallLog, `_catalog.Solution.Projects.Count()`) | Wenn der Agent prüfen will, ob der Server noch healthy ist (Memory-Leak-Detection, lange Sessions). | 1h |
| **Q4** | **`list_projects`-Tool** (Liste aller Projekte mit jeweils: Name, FileCount, Type-Count, Dependencies-Liste) | Wenn der Agent die Solution-Struktur erkunden will, ohne `search_pattern` zu nutzen. | 1-2h (Walk über `solution.Projects`, Aggregation) |

### 8.2 Quick-Wins (~1 Tag)

| # | Empfehlung | Use-Case | Aufwand |
| :--- | :--- | :--- | :--- |
| **Q5** | **`get_call_tree`-Tool** (echter Baum, formatierbar als ASCII oder Mermaid; depth-Parameter bis 5) | Wenn der Agent die Architektur eines Service visuell darstellen will, ohne aggregierte Top-N-Liste manuell zu gruppieren. | 1 Tag (BFS + Tree-Format, wiederverwendet `CallGraphTraversal`) |
| **Q6** | **`get_dependencies(type)`-Tool** (rekursiver Project-Reference-Graph + NuGet-Package-Liste aus `Project.MetadataReferences`) | Wenn der Agent verstehen will, was beim Refactoring eines Service mitbricht, jenseits von Code-Call-Sites (z. B. transitive Package-Updates). | 1 Tag |
| **Q7** | **`get_fixes(filePath)`-Tool** (delegiert an `LinterAutoFixer` im Dry-Run-Modus, liefert Diff-Vorschau) | Wenn der Agent Lint-Fehler auto-fixen will, aber zuerst die Änderung sehen muss. | 1-2 Tage (AutoFixer ist dry-run-fähig, nur MCP-Wrapper) |
| **Q8** | **Pagination-Support für `get_violations`** (`offset: int = 0, limit: int = 100`, mit `nextOffset`-Hinweis im Output) | Wenn eine Solution 5.000+ Violations hat, will der Agent seitenweise durchgehen, nicht alles auf einmal. | 1 Tag |
| **Q9** | **`generate_eval_prompt(type)`-Tool** (delegiert an `EvalAssembler`, liefert fertigen LLM-Prompt mit frischer Evidenz) | Wenn der Agent einen Naming-Drift- oder Architecture-Intent-Audit durchführen will, ohne die `--eval`-CLI aufzurufen. | 1 Tag |

### 8.3 Quick-Wins (~1 Woche)

| # | Empfehlung | Use-Case | Aufwand |
| :--- | :--- | :--- | :--- |
| **Q10** | **`get_test_coverage`-Tool** (delegiert an `TestCoverageResolver`, liefert Covered/Uncovered-Klassen-Liste pro Production-Klasse) | Wenn der Agent prüfen will, ob ein Refactoring Test-Änderungen mit sich bringt. | 1 Woche (Resolver existiert, MCP-Wrapper + Statistik-Aggregation) |
| **Q11** | **`apply_edit`-Tool** (mit Validierungs-Pass: schreibt auf Disk, ruft `get_violations`/`find_references`/`get_impact` als atomare Transaktion auf; rollback bei Verstoß) | Wenn der Agent sichere, validierte Edits machen will, ohne eigenes Try/Validate/Revert-Logik zu schreiben. | 1-2 Wochen (Edit-Anwendung, Pre/Post-Validation, optionaler Git-Stash-Rollback) |
| **Q12** | **Multi-Solution-Workspace** (Server akzeptiert `--path` mit Liste von Solutions, verwaltet mehrere Workspaces, übergreifendes `find_symbol`) | Wenn der Agent in einem Microservice-Setup mit mehreren Solutions arbeitet. | 1-2 Wochen (Multi-Workspace-State, übergreifende Symbol-Resolution) |
| **Q13** | **`FileSystemWatcher`-basierter Push-Channel** (MCP-`notifications/tools/list_changed` o.ä., Agent bekommt aktiv mitgeteilt, wenn Solution sich geändert hat) | Für Watch-Mode-Workflows (Claude-Code-Edit-Loop mit Auto-Refresh). | 1 Woche (Watcher, Notification-Bridge, Backpressure-Handling) |
| **Q14** | **`get_ai_context_footprint(type)`-Tool** (delegiert an `AIContextFootprintCalculator`, liefert transitive Codezeilen + Member-Liste + Aufrufstellen + Hot-Score) | Wenn der Agent ein Refactoring plant und das Kontext-Budget prüfen muss, bevor er editiert. | 1 Woche |

### 8.4 Quick-Wins mit Sonder-Charakter (kleiner, aber hochwirksam)

| # | Empfehlung | Use-Case | Aufwand |
| :--- | :--- | :--- | :--- |
| **Q15** | **Tool-Description mit `examples` (Few-Shot)** im `McpServerToolCreateOptions` | Wenn der Agent eine genauere Vorstellung davon braucht, wann er welches Tool benutzen soll — z. B. "Beispiel: `find_symbol` mit `namePattern='MyService'`, `kind='class'` liefert …". | 30 min pro Tool, ~5h total |
| **Q16** | **`outputFormat: "json"` global, nicht pro Tool** (ein Wrapper um `McpToolResults.Text`, der `{ content: "...", metadata: { truncated, totalMatches, durationMs } }` serialisiert) | Wenn der Agent strukturierte Daten braucht, ohne 10 Tools umzubauen. | 2-3h (zentraler Wrapper, 10 Tools adaptieren) |
| **Q17** | **Tool-Call-Statistik im Call-Log** (Aggregat: pro Tool Aufruf-Count, Ø-Dauer, Truncated-Rate) | Wenn der Agent lernen will, welche Tools er über-/unterbenutzt, oder Production-Monitoring. | 2-3h (Aggregation im `McpCallLog`) |

---

## 9. Top 5 Quick-Wins für den MCP-Server (kompakt, actionable)

| Rang | Tool / Feature | Aufwand | Use-Case | Erwarteter Impact |
| :---: | :--- | :---: | :--- | :--- |
| **🥇 1** | **`get_call_tree`-Tool** (echter Baum, ASCII/Mermaid) | 1 Tag | Agent will die Architektur eines Service visuell verstehen, ohne aggregierte Top-N-Liste manuell zu gruppieren. | **Hoch** — fehlt komplett, viel Code-Intelligence-Server-Nutzer erwarten das |
| **🥈 2** | **Structured-Output-Mode** (`outputFormat: "json"` zentral, nicht pro Tool) | 3h | Agent will programmatisch konsumieren (Tests, CI, eigene Tooling). | **Hoch** — alle 10 Tools profitieren sofort, ohne sie umzubauen |
| **🥉 3** | **`get_fixes(filePath)`-Tool** (Auto-Fix-Preview via `LinterAutoFixer`) | 1-2 Tage | Agent will Lint-Fehler auto-fixen, aber zuerst die Diff-Vorschau sehen. | **Hoch** — `LinterAutoFixer` existiert bereits, nur MCP-Wrapper fehlt |
| **4** | **`get_server_health`-Tool** (LoadState, FileState-Count, Call-Log-Aggregat) | 1h | Agent will Server-Stabilität in langen Sessions überwachen. | **Mittel** — Production-Observability, Leak-Detection |
| **5** | **`reload_config`-Tool** (ohne Server-Neustart) | 1h | Nutzer tunen `rules.json` während der Server läuft — Agent sieht Änderungen sofort. | **Mittel** — kleines Feature, große Wirkung für iteratives Tuning |

---

## 10. Anhang — Datei-Referenzen

### 10.1 MCP-Kerndateien

| Datei | Pfad | LOC | Zweck |
| :--- | :--- | ---: | :--- |
| `Program.cs` | `src/AiNetLinter/Program.cs` | 178 | Boot, Fast-Path für `--mcp-server` |
| `McpServerCommand.cs` | `src/AiNetLinter/Commands/McpServerCommand.cs` | 287 | Server-Start, Solution-Auflösung, Config-Resolution, Call-Log |
| `McpCodeGraphServer.cs` | `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` | 207 | Server-Engine, residenter Workspace, Staleness-Refresh |
| `McpCodeGraphServerOptions.cs` | `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs` | 100 | Options-Record |
| `McpCodeGraphServerRefresh.cs` | `src/AiNetLinter/Mcp/McpCodeGraphServerRefresh.cs` | 261 | 3-Phasen-Refresh: deleted → sweep → modified |
| `McpServerOptionsFactory.cs` | `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` | 76 | Server-Options-Bau + Tool/Resource-Aggregation |
| `McpServerOptionsBuilder.cs` | `src/AiNetLinter/Mcp/McpServerOptionsBuilder.cs` | 71 | Fluent-Builder für `McpServerOptions` |
| `McpCallLog.cs` | `src/AiNetLinter/Mcp/McpCallLog.cs` | 245 | Opt-in JSONL-Call-Log |
| `McpToolResults.cs` | `src/AiNetLinter/Mcp/McpToolResults.cs` | 142 | Einheitliche Fehler-Response-Helper |
| `McpTruncation.cs` | `src/AiNetLinter/Mcp/McpTruncation.cs` | 69 | Trunkierungs-Meta-Zeile-Format |
| `ServerLoadState.cs` | `src/AiNetLinter/Mcp/ServerLoadState.cs` | 28 | Enum `Loading`/`Loaded`/`LoadFailed` |
| `McpFileState.cs` | `src/AiNetLinter/Mcp/McpFileState.cs` | 18 | `record(mtime, hash)` pro Datei |
| `OverviewResourceRegistration.cs` | `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs` | 120 | `ainetlinter://overview` Resource |
| `SymbolGraphToolRegistrations.cs` | `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | 162 | 4 Tools: find_symbol, find_references, get_impact, get_type_hierarchy |
| `FileStructureToolRegistrations.cs` | `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` | 121 | 3 Tools: get_file_skeleton, get_index_scope, get_hotspots |
| `AnalysisToolRegistrations.cs` | `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` | 100 | 2 Tools: get_violations, search_pattern |
| `SymbolBodyToolRegistrations.cs` | `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs` | 58 | 1 Tool: get_symbol_body |
| `McpLintConsole.cs` | `src/AiNetLinter/Output/McpLintConsole.cs` | 25 | `ILintConsole`-Impl, schreibt auf `stderr` |

### 10.2 Tool-Implementierungen (`src/AiNetLinter/Mcp/Tools/`)

| Datei | LOC | Tool |
| :--- | ---: | :--- |
| `FindSymbolTool.cs` + `FindSymbolScanner.cs` | 116 + 122 | `find_symbol` |
| `FindReferencesTool.cs` | 187 | `find_references` |
| `GetImpactTool.cs` | 105 | `get_impact` |
| `GetTypeHierarchyTool.cs` + `GetTypeHierarchyFormatter.cs` | 40 + 137 | `get_type_hierarchy` |
| `GetFileSkeletonTool.cs` | 46 | `get_file_skeleton` |
| `GetSymbolBodyTool.cs` | 79 | `get_symbol_body` |
| `GetViolationsTool.cs` + `GetViolationsScanner.cs` | 41 + 199 | `get_violations` |
| `GetHotspotsTool.cs` + `GetHotspotsScanner.cs` | 32 + 132 | `get_hotspots` |
| `GetIndexScopeTool.cs` + `GetIndexScopeScanner.cs` | 30 + 88 | `get_index_scope` |
| `SearchPatternTool.cs` + `SearchPatternScanner.cs` | 70 + 192 | `search_pattern` |
| `SymbolIdentifierResolver.cs` | 119 | Stable-ID + Position + Name → Symbol |
| `CallGraphTraversal.cs` | 133 | BFS für transitive Aufrufstellen |
| `DiRegistrationHeuristics.cs` | 142 | Regex-Suche nach `AddScoped<T>` etc. |
| `McpCompileDiagnostics.cs` | 122 | Compile-Fehler-Aggregation |

### 10.3 Test-Dateien (`src/AiNetLinter.Tests/Mcp/`)

20 Dateien, ~30 Tests-Dateien (`Tools/*ToolTests.cs`), Fixtures (`SymbolGraphMcpFixture`, `McpLiveRepositoryFixture`, `SymbolGraphMiniFixtureWorkspace`, `McpTestClient`, `SubprocessConcurrencyGate`).

### 10.4 Eingebettete Doku (per `EmbeddedResource` in `.csproj`)

- `README.md`
- `Docs/integration.md` (Schritt-für-Schritt-Integration, MCP-Server-Registrierung)
- `Docs/agent-api.md` (Agent-API-Referenz, MCP-Server-Modus § ab Z. 213)
- `Docs/configuration.md` (129 KB, vollständige Config-Referenz)
- `Docs/rationale.md` (Design-Rationale)
- `Docs/ROADMAP.md` (56 KB, Roadmap)
- `Docs/Evals/naming-drift.md`, `Docs/Evals/architecture-intent.md`
- `rules.json` (Default-Config, falls neben Solution keine eigene liegt)

### 10.5 Beispiel-`.mcp.json`-Konfiguration (aus `.example.mcp.json`)

```json
{
  "mcpServers": {
    "ainetlinter": {
      "command": "C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\src\\AiNetLinter\\bin\\Debug\\net10.0\\AiNetLinter.exe",
      "args": [
        "--mcp-server",
        "--path",
        "C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
        "--config",
        "C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\rules.json"
      ]
    }
  }
}
```

Aus `Docs/integration.md § MCP-Server registrieren`: **Empfehlung** ist `args: ["--mcp-server"]` (ohne `--path`/ `--config`) — der Server sucht im `cwd` nach genau einer `.sln`/`.slnx` und entdeckt `rules.json` automatisch neben der Solution.

---

**Report-Ende.** Stand: Recon vom aktuellen Repo-HEAD. Keine Implementierung, keine Code-Änderungen, nur Bestandsaufnahme.
