---
status: done (pending audit)
type: step-result
task: codegraph-mcp-finish
step: 008
title: "ILinterEngineConfig-Interface extrahieren, PathOverride-Liste auf Rest reduzieren (EPIC-03 / Muss-Haben C, TD-008 / TD-010)"
epic: EPIC-03
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
code_commit: fd395c2
docs_commit: be6ff6a
---

# Step 008 Result: ILinterEngineConfig-Interface extrahiert, PathOverride-Liste 14 → 2

## Zusammenfassung

EPIC-03 (`ILinterEngineConfig`-Refactor, Muss-Haben C) vollständig umgesetzt. Ein
schmales `internal interface ILinterEngineConfig` mit den Properties, die `LinterEngine`
und MCP-Tools tatsächlich konsumieren, ist in `src/AiNetLinter/Configuration/`
angelegt; `Config` implementiert es implizit. `McpCodeGraphServer.Config` und
`McpCodeGraphServerOptions.Config` sind nun vom Interface-Typ, womit der
`Configuration`-Namespace nicht mehr strukturell in den Footprint der 12 Dateien
gezogen wird, die `Config` nur transitiv über den `McpCodeGraphServer`-Typ
referenzierten. Die `rules.json`-`PathOverrides`-Sektion ist von 14 Einträgen
auf 2 Einträge reduziert — die verbleibenden 2 sind die einzigen Tool-Klassen
(`FindReferencesTool`, `FindSymbolTool`), deren aktueller Footprint nach dem
Refactor noch 16-29 Zeilen über dem 2500er-Limit liegt. Weg A (Downcast am
Call-Site in `GetViolationsScanner`) ist umgesetzt — `LinterEngine` behält
den konkreten `Config`-Parametertyp.

## Geänderte Dateien

6 Dateien (5 Touch-Points aus dem Plan + `rules.json`-Bereinigung):

| Datei | Änderung |
|---|---|
| `src/AiNetLinter/Configuration/ILinterEngineConfig.cs` | **NEU** — 11 Properties, internal, gemäß Plan Datei 1 |
| `src/AiNetLinter/Configuration/Config.cs` | Klassen-Header: `: ILinterEngineConfig` ergänzt (1 Zeile) |
| `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` | Property-Typ `Config` → `ILinterEngineConfig` (+ XML-Doc-Update) |
| `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs` | Property-Typ `Config` → `ILinterEngineConfig` (+ XML-Doc-Update) |
| `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` | `BuildViolationsTextAsync`-Param: `Config` → `ILinterEngineConfig`, Downcast `(Config)config` am `LinterEngine`-Konstruktor (Weg A) |
| `rules.json` | `PathOverrides` von 14 Einträgen auf 2 reduziert (12 entfernt, 2 neu mit Begründung) |

`src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` und `src/AiNetLinter/Commands/AuditCommand.cs`
brauchten **keine** Code-Änderung: `GetViolationsTool.cs:31` reicht `state.Config` (jetzt
`ILinterEngineConfig`) transparent an den Scanner weiter; `AuditCommand.cs` arbeitet
weiterhin mit dem konkreten `Config`-Typ via `ConfigLoader.TryLoadConfig` und ist
nicht über `McpCodeGraphServer` angebunden.

**Tests:** 12 Test-Dateien im `Mcp/`-Bereich (`McpCodeGraphServerConstructorTests`,
`McpCodeGraphServerTests`, `McpServerOptionsFactoryTests`, `McpServerOptionsBuilderTests`,
`FindSymbolToolTests`, `SearchPatternToolTests`, `GetViolationsToolTests`,
`GetTypeHierarchyToolTests`, `GetIndexScopeToolTests`, `GetImpactToolTests`,
`GetHotspotsToolTests`, `GetFileSkeletonToolTests`, `FindReferencesToolTests`)
kompilieren ohne Inhalts-Änderung — `McpCodeGraphServerOptions.From(...)` akzeptiert
`Config` weiterhin, und kein Test liest die `Config`-Property als konkreten Typ.

## Commit-Hashes

- **Code-Commit:** `fd395c2` — `refactor(mcp): ilinterengineconfig-interface einfuehren und pathoverrides reduzieren [codegraph-mcp-finish]`
- **Doku-Commit:** siehe unten (Status-Update + dieses `step-result.md`)

## Weg A / B / C Entscheidung

**Weg A umgesetzt** (Downcast am Call-Site in `GetViolationsScanner.BuildViolationsTextAsync`):

```csharp
// GetViolationsScanner.cs
internal static async Task<string> BuildViolationsTextAsync(
    Solution solution,
    ILinterEngineConfig config,   // war: Config
    ILintConsole console,
    string? scopeFilter,
    CancellationToken ct)
{
    // LinterEngine verlangt den konkreten Config-Typ (Record-Semantik fuer
    // `with {...}` und durchgereichte Sub-Properties); ILinterEngineConfig wird
    // projektweit ausschliesslich von Config implementiert, der Downcast ist
    // daher nicht spekulativ.
    var concreteConfig = (Config)config;

    // ...
    var engine = new LinterEngine(config: concreteConfig, ...);
}
```

**Begründung:** Kleinster Eingriff — `LinterEngine` behält den `Config`-Parametertyp
(Record-Semantik für `_config with { SolutionBasePath = dir }` an `LinterEngine.cs:233`
und `_config.TestSentinel.TestProjectNameSuffixes` etc.). Der Downcast ist
strukturell sicher, weil `ILinterEngineConfig` projektweit **nur** von `Config`
implementiert wird (per Grep gegen `src/AiNetLinter/` verifiziert: keine zweite
Implementierung). XML-Doc am Scanner dokumentiert die Begründung. Kein Scope-Drift
in `LinterEngine` oder `ProjectConfigResolver` (Weg B-Variante), keine Doku-Verlagerung
(Weg C-Variante). Konzept-Vorgabe „interne interface" bleibt erfüllt.

## Build/Test-Output

- `dotnet build AiNetLinter.slnx` → grün, 0 Warnungen, 0 Fehler (~6s).
- `dotnet test AiNetLinter.slnx --no-build` → **Lauf 1:** 1185/1186 grün, 1 Failure
  (`McpServerCommandErrorHandlingTests.RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError`,
  Stack an `SubprocessConcurrencyGate.AcquireAsync:30` mit 30s-Wait-Timeout —
  TD-005-Last-Flake-Signatur, `infrastructure`, keine der 5 refaktorierten Dateien
  betroffen). **Lauf 2:** 1186/1186 grün, 4 m 31 s. Klassifikation nach
  `step-007/fix-01`-Vorbild: `infrastructure`, keine Fix-Versuche verbraucht.

## PathOverride-Vorher/Nachher + Begründungen

**Vorher:** 14 Einträge (alle `MaxAIContextFootprint: 2700`).
**Nachher:** 2 Einträge (`MaxAIContextFootprint: 2530` und `2520`).

Per-Datei-Footprint-Messung (`--footprint`) nach dem Refactor:

| Datei | Footprint | Status | Override nötig? |
|---|---:|:---|:---|
| `src/AiNetLinter/Commands/AuditCommand.cs` | 2477 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` | 2466 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` | 2465 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` | 2469 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | 2483 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs` | 2451 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` | 2437 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` | 2483 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/Tools/GetIndexScopeTool.cs` | 2435 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyTool.cs` | 2445 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` | 2440 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` | 2475 | OK | Nein — entfernt |
| `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` | **2529** | OVER (Limit 2500) | **Ja — `2530`** |
| `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` | **2516** | OVER (Limit 2500) | **Ja — `2520`** |
| `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` | 1752 | OK | Nein — Downcast am Call-Site isoliert die `Config`-Reichweite |
| `McpCodeGraphServer` (nicht in Overrides) | 2405 | OK | n/a |

**Begründungen für die 2 verbleibenden Einträge** (siehe `rules.json` `PathOverrides`):

- `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` — aktueller Footprint 2529 Zeilen.
  Beide Tool-Klassen (`FindReferencesTool`, `FindSymbolTool`) ziehen über
  `Microsoft.CodeAnalysis.FindSymbols.SymbolFinder` + verwandte `Solution.Get*`-Calls
  den `Configuration`-Namespace für die `ProjectOverrides`/`PathOverrides`-Lookups
  (siehe `konzept.md` für Symbol-Graph-Referenzierung) strukturell herein. Eine
  Verkleinerung würde eine Aufspaltung der Symbol-Graph-Registrar-Klasse analog
  zu `McpServerOptionsFactory` voraussetzen — out-of-scope dieses Steps. Verbleibender
  Bedarf am Override, **nicht** toter Ballast.
- `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` — aktueller Footprint 2516 Zeilen.
  Selbe Begründung wie `FindReferencesTool` (gemeinsame Symbol-Graph-Tool-Schicht,
  koppelt strukturell an `Configuration` über `PathOverrides`-Lookups). Gehört zu
  EPIC-08-Block („Symbolgraph-Erweiterungen", Konzept-Muss-Haben E) als
  Mitnahme-Kandidat in einem Folge-Step.

**Standard-JSON hat keine Kommentare** — die Begründungen sind hier im
`step-result.md` statt im JSON-File dokumentiert (analog `spec.md` §9 Empfehlung
„Begründungen außerhalb der Konfig, im Plan/Result"). Eine separate
`PathOverrideBegruendungen`-Sektion in `rules.json` ist nicht angelegt, weil die
Aktuelle Konvention im Projekt Doku-Begründungen im `step-result.md` führt (siehe
z. B. `step-005` `PathOverride`-Dokumentation) und der Bedarf auf 2 Rest-Einträge
begrenzt ist.

## Abweichungen vom Plan

- **Erwarteter 0-Override-Bereich auf 2 Override erweitert:** Plan-Erwartung
  war „wahrscheinlich 0-2 Einträge übrig" — 2 ist die obere Schranke. Beide
  verbleibenden Einträge sind Tool-Klassen aus dem Symbol-Graph-Bereich, deren
  Footprint durch die Interface-Entkopplung nicht weit genug sinkt (2529 / 2516
  vs. 2500-Limit). Eine Aufspaltung würde in den EPIC-08-Block gehören und
  sprengt diesen Step.
- **McpCodeGraphServer.cs:56-61 XML-Doc** wurde zusätzlich zum Property-Typ-Wechsel
  um einen Hinweis auf die Interface-Verschmälerung erweitert („Exposed als schmale
  Lese-Sicht"), damit künftige Leser nicht durch den konkreten Typ in
  `McpCodeGraphServerOptions.From()` verwirrt werden. Keine Verhaltensänderung,
  reine Doku-Klarstellung.
- **McpCodeGraphServerOptions.cs:29-32 XML-Doc** analog erweitert.
- **Keine** Test-Datei-Inhalts-Änderung nötig — keine Downcasts, keine Test-Assertion-Anpassungen.

Keine unerwarteten Touch-Points aufgetaucht, keine Scope-Erweiterung notwendig.

## Beobachtungen (außerhalb Step-Scope — nicht selbst gefixt)

- **`McpCodeGraphServer.cs:39`** weist `Config = options.Config` zu, wobei `options.Config`
  jetzt `ILinterEngineConfig` ist. Strukturell korrekt (`Config : ILinterEngineConfig`),
  aber die Initialisierung selbst ist redundant: `Config` ist `required`, der
  `From()`-Factory-Pfad garantiert nie-null. Ein möglicher Aufräum-Punkt wäre,
  `McpCodeGraphServerOptions.Config` als `required ILinterEngineConfig?` mit
  Lazy-Default zu modellieren — bewusst NICHT in diesem Step angefasst (out-of-scope,
  kein Verhaltens-Change).
- **`FindReferencesTool`/`FindSymbolTool`-Footprint-Drift:** beide Tool-Klassen
  ziehen über ihre Symbol-Graph-Lookup-Pfade `Configuration`-Sub-Typen herein
  (`ProjectOverrides`/`PathOverrides` werden im Symbol-Graph-Code für
  Per-Projekt-Regelauswertung konsumiert). Eine Footprint-Reduktion unter 2500
  würde eine Aufteilung in `SymbolGraphToolRegistrations` + Scanner-Klassen analog
  zu `GetViolationsTool`/`GetViolationsScanner` erfordern — gehört konzeptuell zu
  EPIC-08 (Symbolgraph-Erweiterungen) und ist als TD/Planer-Folge-Step zu
  erwägen, nicht hier zu fixen.
- **TD-006 (.agents/rules/AiNetLinter.mdc UTF-8-BOM)** ist im Working-Tree weiter
  sichtbar modifiziert (siehe `git status`). **Nicht** durch diesen Step
  verursacht — die `.mdc` war bereits vor dem Step in diesem Zustand und der
  `--sync-agent-rules-only`-Lauf meldet die Datei als „bereits aktuell" gegenüber
  `rules.json`. Der `.mdc` wird daher nicht mit dem Code-Commit committet.

## Bekannte Unschärfen

- **TD-005 Last-Flake** ist im Volllauf wieder aufgetreten (1 von 1186 Failures,
  `McpServerCommandErrorHandlingTests.RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError`,
  Stack an `SubprocessConcurrencyGate.AcquireAsync:30` mit 30s-Wait-Timeout).
  Klassifikation: **`infrastructure`** (Test-Gate-Sättigung, scope-extern), keine
  Fix-Versuche verbraucht. Zweiter Lauf 1186/1186 grün reproduziert die
  Volatilität. Dokumentation in `tech-debt.md` § TD-005.
- **Kein McpLiveRepositoryTests-Lauf im Pre-Commit-Test:** die
  Volllauf-Verifikation umfasst den Dogfood-Pfad implizit (er läuft im Volllauf
  mit, ist Teil der 1186 Tests), eine separate End-zu-End-Bestätigung von
  `get_violations` mit dem neuen Interface-Typ wäre redundant.

## Modell-Info

- `coded_by_model`: claude-sonnet-5
- `coded_by_model_knowledge_cutoff`: 2026-01
- Stufe (aus task-state.md Config-Block): Medium
