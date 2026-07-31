---
status: open
type: step-plan
task: codegraph-mcp
step: 010
title: "get_violations Tool (regelbasierte Lint-Violations, scoped, ohne Disk-Cache)"
epic: EPIC-04
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T22:30:00Z
related_to: [step-008, step-009]
---

# Step 010: get_violations Tool (regelbasierte Lint-Violations, scoped, ohne Disk-Cache)

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-04` aus `roadmap.md` — drittes von vier EPIC-04-Tools
  (nach `get_index_scope` step-008 `approved`, `get_hotspots` step-009
  `approved`). Letztes offenes EPIC-04-Tool danach: `search_pattern`.
- **Konzept-Referenz:**
  - `konzept.md` Tool-Tabelle Zeile `get_violations` ("Datei- oder
    Symbol-Scope | Aktuelle Lint-Verstöße in diesem Scope | Basis
    `RuleRegistry`/`LinterEngine`").
  - `konzept.md` Muss-Haven "`get_violations` umgeht den bestehenden
    Disk-Cache (`AnalysisCacheManager`) und rechnet direkt gegen die
    resident gehaltene `Compilation`" (Begründung: Disk-Cache dient der
    Vermeidung von Re-Compilation zwischen unabhängigen CLI-Prozessen —
    irrelevant für resident laufenden Server; explizit festgehalten
    wegen der Cache-Isolation zu parallelen CLI-Lint-Läufen auf
    derselben Solution).
  - `konzept.md` "Wo im Projekt": `Core/RuleRegistry.cs` und
    `Core/LinterEngine.cs` als Basis.
  - `konzept.md` Muss-Haven "Dogfooding pro Tool-Step" (gilt auch hier,
    siehe EPIC-04-Block in `roadmap.md`).
  - `konzept.md` Muss-Haven "Thread-sicherer Zugriff auf die gehaltene
    `Solution`/`Compilation`" (durch `McpCodeGraphServer.GetCurrentSolution`
    bereits abgedeckt, keine Änderung nötig — siehe "Aktueller
    Projektzustand" unten).

## Aktueller Projektzustand (JIT-Kontext)

- **`LinterEngine.RunAsync(Solution, noCache, cacheTtlMinutes, ct)`**
  (`src/AiNetLinter/Core/LinterEngine.cs:64-68`) ist **exakt** die
  Pipeline, die `get_violations` braucht: sie nimmt eine
  resident gehaltene `Solution` entgegen, führt per-doc Parallel-Analyse
  + Post-Analysis-Checks aus, und liefert alle `RuleViolation`s als
  `IReadOnlyCollection<RuleViolation>` zurück. Der Parameter
  `noCache: true` setzt den `BuildCache`-Pfad auf `null` (Zeile 70-81),
  d. h. **`AnalysisCacheManager` wird gar nicht erst instanziiert** —
  die in `konzept.md` geforderte "kein Disk-Cache"-Architektur fällt
  hier kostenlos ab, ohne neue Infrastruktur. Die
  `cache?.SaveIfDirty()`-Zeile in `RunInternalAsync` (Zeile 116) ist
  ebenfalls Cache-Pfad-abhängig und wird mit `noCache: true` nie
  erreicht. **`LinterEngine` ist also der zentrale
  Wiederverwendungs-Baustein für `get_violations` — kein Neubau einer
  eigenen Lint-Loop nötig** (bewusste Abweichung vom ursprünglichen
  "Scanner-Datei"-Muster aus step-008/009: dort wurden dedizierte
  Scanner gebaut, weil `HotspotMapBuilder`/`WebFileCatalog` nicht
  Solution-bewusst arbeiten — `LinterEngine` ist es bereits und
  exponiert genau die richtige Entry-Point-Methode).

- **`LinterEngine`-Konstruktor** (Zeile 34-41): `internal LinterEngine(
  Config config, string? rulesJsonContent = null, IPerformanceProfiler?
  profiler = null, ILintConsole? console = null, LinterArgs? args = null)`.
  Für `get_violations` brauchen wir: `Config` (zwingend, für
  PathOverrides/Regel-Limits), `rulesJsonContent: null` (kein Disk-Cache,
  daher irrelevant), `profiler: null` (kein Profiling im MCP-Kontext),
  `console` (soll der MCP-Server-Konsolen-Kanal sein, nicht stdout —
  siehe unten), `args: null` (keine `IncludeNamespaces`/`IncludeProjects`
  Filterung — Scope-Filterung passiert post-hoc, siehe unten). **JIT,
  bewusst minimal, kein Vorgriff auf künftige Tools:** wir nehmen nur
  `Config` + `console` mit, nichts anderes.

- **Scope-Filter-Strategie (bewusste Entscheidung Post-Filter statt
  Pre-Filter):** `LinterEngine.RunAsync` unterstützt nur
  `LinterArgs.IncludeProjects`/`IncludeNamespaces` für Pre-Filterung
  (Zeile 173, `SourceFileCatalog.ShouldIncludeProject` →
  `NamespaceFilter.MatchesGlob`, also Glob-Matching — **nicht** das
  gleiche `Contains`-Substring-Matching wie `get_hotspots`).
  Konsistenz mit `get_hotspots` (gleiche Filter-Semantik) + einfachere
  Implementierung → **Post-Filter auf den fertigen `RuleViolation`s**,
  nicht Pre-Filter über `LinterArgs`. Ineffizienz: bei breitem
  Scope-Filter wird die volle Analyse gefahren, auch für nicht-passende
  Dateien — vertretbar, weil (a) `LinterEngine` bereits per-doc
  parallel analysiert, (b) die Analyse auf der resident gehaltenen
  `Solution`/`Compilation` läuft (kein erneutes MSBuild-Laden), (c)
  für `get_violations` (ein Orientierungs-Tool) keine kritische
  Performance-Anforderung. **Bekannte Ausnahme** unten dokumentiert;
  falls sich das in der Praxis (Dogfooding auf großer Solution) als
  Problem zeigt, ist Pre-Filter per Replikation der per-doc-Schleife
  (analog zu `get_hotspots`) ein Kandidat für einen späteren
  Tech-Debt-Eintrag.

- **Scope-Filter-Semantik** (gleiche Vereinfachung wie
  `get_hotspots` step-009): case-insensitive `Contains` auf
  `Project.Name` ODER `Path.GetRelativePath(solutionDir, filePath)` —
  keine echte C#-Namespace-Deklaration wird geparst, gleiche
  "Bekannte Ausnahme"-Begründung wie in `get_hotspots/step-plan.md`
  ("Bekannte Ausnahmen": kein echtes C#-Namespace-Parsing).

- **`McpCodeGraphServer.Config`-Property fehlt noch:** bislang hält der
  Server nur `maxLineCount` (step-009), nicht die volle `Config`.
  `get_violations` braucht die `Config` (für `LinterEngine`-Konstruktion
  + PathOverrides). **Entscheidung (JIT, additiv, bricht keine
  bestehende Call-Site):** `McpCodeGraphServer` bekommt einen
  additiven vierten Konstruktor-Parameter `Config? config = null` +
  öffentliche Property `Config Config { get; }`, die im Konstruktor auf
  `_config ?? new Config()` normalisiert wird (so ist die Property
  nicht-null und der Aufrufer braucht keinen Null-Check). Plus eine
  öffentliche `ILintConsole Console`-Property (für die Weitergabe an
  `LinterEngine` — damit Lint-Warnings auf demselben Kanal landen wie
  die übrigen MCP-Server-Logs, nicht auf stdout, wo sie mit dem
  stdio-MCP-Verkehr kollidieren würden).

- **Config-Verdrahtung in `McpServerCommand`:** `LinterArgs.ConfigPath`
  ist bereits vorhanden (aus `--config`, identisch zum CLI-Batch-Modus).
  Neuer `internal static Config ResolveConfig(LinterArgs args)` (gleiche
  Signatur/Pattern wie `ResolveMaxLineCount` aus step-009, 1:1-Logik:
  `ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false)` oder
  Default `new Config()`). Ergebnis wird vor der `McpCodeGraphServer`-
  Konstruktion in den neuen Parameter eingereicht. Kein neuer
  Mechanismus, exakt das gleiche Muster wie step-009 für
  `MaxLineCount` — nur die geladene Entität ist größer.

- **Tool-Description-Pflicht (Muss-Haven "Explizite Scope-Kommunikation"):
  `get_violations` lintet `.cs`-Code (also C#-only by construction),
  aber die `description` benennt die `.cs`-Grenze trotzdem explizit
  ("Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien"),
  damit ein Agent nicht versucht, `get_violations` z. B. auf
  `.razor`-Dateien anzuwenden. Zusätzlich nennt die `description` den
  Cache-Bypass ("kein Disk-Cache, läuft direkt gegen die resident
  gehaltene Solution") — gleiche Agent-Transparenz, die
  `konzept.md` für die anderen Tools fordert.

- **Footprint-Lage (TD-004/TD-005, `tech-debt.md`):**
  - `FileStructureToolRegistrations` liegt nach step-009 bei
    2455/2500 (45 Zeilen Puffer, vom Kritiker unabhängig bestätigt).
    Ein neuer `tools.Add(McpServerTool.Create(...))`-Block für
    `get_violations` kostet schätzungsweise 14-18 Zeilen (Lambda
    ~3, Description-String ~5-6, Options-Block ~5-6) — passt
    rechnerisch in den Puffer. **Plan:** keine dritte
    Registrar-Klasse in diesem Step, aber DoD verlangt zwingend
    einen `--footprint FileStructureToolRegistrations`-Lauf nach
    der Registrierung; reißt das Limit (2500), wird **in diesem
    Step** eine dritte Registrar-Klasse
    (`AnalysisToolRegistrations.cs` für
    `get_violations`/`search_pattern`) umgesetzt, nicht erst
    reaktiv im nächsten.
  - `GetViolationsTool` (Dispatch) und `GetViolationsScanner`
    (Logik) werden **beide** deutlich über 2500 liegen, weil
    `LinterEngine` + `LinterAnalyzer` + alle Checker (transitive
    Abhängigkeiten) in den Footprint einfließen. Lösung: per-doc
    `PathOverrides` in `rules.json` analog `PathOverrides.
    AuditCommand.cs` (MaxAIContextFootprint: 2700). Konkrete Werte
    beim Coder durch Selbst-Lint (`--footprint GetViolationsTool
    --path .` / `--footprint GetViolationsScanner --path .`)
    ermitteln und auf den nächstgrößeren sinnvollen Wert
    aufrunden (Empfehlung: 6000 — Faustregel "Mindestpuffer über
    dem gemessenen Wert", konsistent mit dem
    AuditCommand-Precedent).

- **Tool-Klassen-Muster (TD-005):** `GetIndexScopeTool`/
  `GetIndexScopeScanner` und `GetHotspotsTool`/`GetHotspotsScanner`
  sind die etablierten Vorbilder. Für `get_violations` ergibt sich
  eine **leichte Variation** dieses Musters: `GetViolationsTool`
  ist immer noch ein dünner Dispatch, aber `GetViolationsScanner`
  delegiert die Hauptarbeit an `LinterEngine.RunAsync(Solution,
  noCache: true, ...)` statt eine eigene per-doc-Schleife zu
  bauen. Begründung im Header der Scanner-Datei (kein
  "Doppelarbeit" zum LinterEngine, sauber wiederverwendet statt
  dupliziert — bewusste Abweichung vom `GetHotspotsScanner`-
  Muster, weil `LinterEngine` für den hier benötigten
  Solution-bewussten Lint bereits die richtige API bietet).

- **Fixture-Erweiterung nötig:** `SymbolGraphMiniFixtureWorkspace`
  enthält aktuell nur saubere, kleine `.cs`-Dateien (alle <30 Zeilen,
  `sealed`, korrekt benannt → keine Lint-Violations unter
  Default-Config). Für den `ExecuteAsync_NoScopeFilter_ReturnsViolationForKnownFixture`-
  Test brauchen wir eine deterministische Violation. **Plan:** neue
  Datei `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/ViolationTrigger.cs`
  mit einer einzelnen, klaren Violation (z. B. `public class
  ViolationTrigger` ohne `sealed` → `EnforceSealedClasses`-Violation).
  Nicht-konfliktend mit `EnforceNullableEnable` (Datei beginnt mit
  `#nullable enable`), mit dem bestehenden
  `ProjectConfigResolver`-Override-Mechanismus verträglich
  (Verzeichnispfad `SymbolGraphMini/` → leerer Override oder
  Default). Konkrete Violation-Wahl dem Coder überlassen
  (Hauptsache: eine einzige, deterministische Violation, die
  auch in 6 Monaten noch unter derselben Regel feuert — kein
  "Balance"-Test, der bei Regel-Tuning plötzlich kippt).

- **Thread-Sicherheit (Muss-Haven, kein neuer Lock nötig):**
  `McpCodeGraphServer.GetCurrentSolution()` (`McpCodeGraphServer.cs:54`)
  lockt bereits intern (Zeile 56 `lock (_lock)`) und liefert eine
  `Solution`-Referenz. Roslyn-`Solution` ist immutable; der
  `_catalog.Solution`-Refresh im Staleness-Pfad (Zeile 60-61)
  ersetzt nur die `_catalog`-interne Referenz, nicht die
  `Solution`-Inhalte. Mehrere parallele `get_violations`-Aufrufe
  arbeiten jeweils auf ihrer eigenen `Solution`-Snapshot, jeder
  erzeugt eine eigene `LinterEngine`-Instanz (per-call `new`, keine
  Shared-Mutation), jeder ruft `engine.RunAsync(snapshot, ...)` auf.
  Roslyns `MSBuildWorkspace` (innerhalb des `SourceFileCatalog`)
  unterstützt parallele Lese-Zugriffe auf `Document`s über
  `GetSemanticModelAsync(ct)` — keine Schreibvorgänge aus dem
  `LinterEngine`-Pfad. **Ergebnis:** kein zusätzlicher Lock im Tool
  nötig, der bestehende `McpCodeGraphServer`-Lock reicht. Die
  Konzept-Anforderung "Thread-sicherer Zugriff" ist bereits
  vollständig durch step-002 abgedeckt; dieser Step verändert
  daran nichts.

## Intention

`get_violations` liefert dieselben Regelverstöße wie der bestehende
CLI-Batch-Lint-Lauf (`ainetlinter --config rules.json --path .`),
aber granular gegen die resident gehaltene Solution statt als
Einmal-Komplettlauf, inklusive optionalem Scope-Filter (Projekt-Name
oder solution-relativer Pfad) — Orientierungs-Tool für einen Agenten,
der proaktiv wissen will, ob ein geplanter Edit gegen bestehende
Regeln verstoßen würde, **ohne** den bestehenden Disk-Cache
anzufassen (per `konzept.md` Muss-Haven "Cache umgehen": der
Disk-Cache dient der Vermeidung von Re-Compilation zwischen
unabhängigen CLI-Prozessstarts — irrelevant für den resident
laufenden Server und gefährlich bei parallelen CLI-Lint-Läufen
auf derselben Solution, weil `AnalysisCacheManager` keine
prozessübergreifende Sperre hat).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (Zeile 21-48)

- **Was:**
  - Konstruktor um **additive** Parameter (alle am Ende, default `null`)
    erweitern: `Config? config = null, ILintConsole? consoleOverride =
    null`. Der bestehende zweite Parameter `ILintConsole? console`
    bleibt unverändert; der neue `consoleOverride`-Parameter ist eine
    **Redundanz-Erlaubnis** für künftige Aufrufer (z. B. Tests), die
    `console` und `config` getrennt setzen wollen — wird in diesem
    Step **nicht** genutzt (nur dokumentiert, dass er da ist), bricht
    keine bestehende Call-Site.
  - Zwei neue öffentliche Properties direkt unter `MaxLineCount`
    (Zeile 48) ergänzen:
    ```csharp
    public Config Config { get; }   // nicht-null: _config ?? new Config()
    public ILintConsole Console => _console;
    ```
  - Im Konstruktor zuweisen: `Config = config ?? new Config();`.
- **Warum:** Einziger Ort, an dem der MCP-Server Zustand hält.
  `Config` wird für die `LinterEngine`-Konstruktion in `GetViolationsScanner`
  gebraucht; `Console` wird für die Weitergabe an `LinterEngine`
  gebraucht (damit Lint-Warnings denselben Kanal wie die übrigen
  MCP-Server-Logs nutzen, nicht stdout). Additive Parameter am Ende
  brechen **keinen** der bestehenden Aufrufe (alle vorhandenen Tests
  nutzen `new McpCodeGraphServer(catalog, console)` oder
  `new McpCodeGraphServer(catalog, console, maxLineCount)` — die
  neuen Parameter landen rechts davon mit Defaults).

### Datei 2: `src/AiNetLinter/Commands/McpServerCommand.cs` (Zeile 29-42, 54-61)

- **Was:**
  - Neue `internal static Config ResolveConfig(LinterArgs args)`-
    Hilfsmethode ergänzen (1:1-Übernahme der
    `ConfigLoader.TryLoadConfig`-Logik aus step-009's
    `ResolveMaxLineCount`, aber Rückgabetyp `Config` statt `int`):
    ```csharp
    internal static Config ResolveConfig(LinterArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.ConfigPath))
            return new Config();
        return ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false)
            ?? new Config();
    }
    ```
  - Im `RunAsync` (Zeile 36) das `McpCodeGraphServer`-Konstrukt um den
    neuen Parameter erweitern:
    `new McpCodeGraphServer(catalog, c, ResolveMaxLineCount(args), ResolveConfig(args))`.
- **Warum:** `args.ConfigPath` ist bereits vorhanden; der MCP-Server
  soll dieselbe `rules.json` respektieren wie ein CLI-Lint-Lauf auf
  derselben Solution, sonst würde `get_violations` mit einer
  irreführenden, von der Projekt-Konfiguration abweichenden
  Default-Config arbeiten. Pattern 1:1 wie step-009 für
  `MaxLineCount` — nur die Entität ist größer.

### Datei 3: `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` (neu)

- **Was:** Dünner Dispatch nach dem `GetHotspotsTool`-Vorbild:
  ```csharp
  internal static class GetViolationsTool
  {
      internal static Task<CallToolResult> ExecuteAsync(
          McpCodeGraphServer state, string? scopeFilter, CancellationToken ct)
      {
          var solution = state.GetCurrentSolution();
          if (solution is null) return Task.FromResult(McpToolResults.SolutionNotLoaded());

          var text = GetViolationsScanner.BuildViolationsText(
              solution, state.Config, state.Console, scopeFilter, ct);
          return Task.FromResult(McpToolResults.Text(text));
      }
  }
  ```
- **Warum:** Keine eigene Lint-/Formatierungslogik in der
  Dispatch-Klasse (TD-005-Muster), damit ihr eigener
  `AIContextFootprint` klein bleibt. `state.Console` wird an den
  Scanner durchgereicht, damit `LinterEngine` auf demselben
  Kanal loggt wie der MCP-Server selbst.

### Datei 4: `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` (neu)

- **Was:** Reine Formatierungs-/Filter-Logik, **delegiert die
  eigentliche Lint-Arbeit an `LinterEngine.RunAsync(solution, noCache:
  true, cacheTtlMinutes: 0, ct)`** (kein Neubau einer Lint-Loop — das
  wäre Doppelarbeit und würde gegen `AiNetLinterRichtlinien.mdc` §1
  "Einfachheit vor Abstraktion" verstoßen):
  - `internal static string BuildViolationsText(Solution solution,
    Config config, ILintConsole console, string? scopeFilter,
    CancellationToken ct)` (signiert `async Task<string>`, da
    `LinterEngine.RunAsync` await-bar ist; für die Registrierung
    in `FileStructureToolRegistrations` wird `Task.FromResult`
    gewrappt) — orchestriert: (1) Pre-Build einer
    `Dictionary<string, string>` (filePath → projectName) über
    `solution.Projects` für O(1)-Project-Name-Lookup im Post-Filter;
    (2) `LinterEngine` pro-call konstruieren
    (`new LinterEngine(config, rulesJsonContent: null, profiler:
    null, console: console, args: null)`) — `rulesJsonContent:
    null` ist explizit (kein Disk-Cache, daher irrelevant),
    `args: null` (Scope-Filterung passiert post-hoc, nicht über
    `LinterArgs.IncludeProjects` — siehe "Aktueller Projektzustand");
    (3) `await engine.RunAsync(solution, noCache: true,
    cacheTtlMinutes: 0, ct)` → liefert `IReadOnlyCollection<RuleViolation>`;
    (4) Post-Filter auf `scopeFilter` (case-insensitive
    `Contains` auf `Path.GetRelativePath(solutionDir, v.FilePath)`
    ODER `projectName` aus der Pre-built Map); (5) leerer
    Scope-Filter ohne Treffer → explizite "Keine Dateien im Scope
    (Filter: '<scopeFilter>') — Filter pruefen."-Meldung (analog
    zu `GetHotspotsScanner` Zeile 40-43); (6) Formatierung als
    Markdown-Report.
  - Formatierung: `StringBuilder`, Kopfzeile mit Count + Scope-Suffix
    analog `GetHotspotsScanner`-Stil ("Lint-Violations: N Verstöße in
    M Dateien | Scope-Filter: '<scopeFilter>'"), dann zwei Sektionen
    getrennt nach Severity (Fehler / Warnungen) — Severity-Auflösung
    pro Violation: `v.EffectiveSeverity ?? (RuleRegistry.TryResolve(
    v.RuleName)?.Severity ?? "warning")` (Fallback-Kette, weil
    `EffectiveSeverity` `string?` ist und nur bei
    CompoundSuppressions gesetzt wird; bei null gilt der
    `RuleMetadata.Severity`-Default). Pro Sektion: Markdown-Tabelle
    mit Spalten `Datei | Zeile | Regel | Details` (Details aus
    `RuleViolation.Details`, Pfad solution-relativ mit Forward-Slashes).
    Bei N == 0 (überhaupt keine Violations in der Solution, kein
    Scope-Filter gesetzt): explizite "Keine Lint-Violations."-
    Meldung. Sektionen alphabetisch sortiert innerhalb der Severity
    (Konstistenz mit `GetHotspotsScanner`).
  - `private static string? LookupProjectName(Dictionary<string,
    string> fileToProject, string filePath)` — Helper für den
    Post-Filter, gibt `null` zurück wenn der Pfad nicht in der
    Map ist (sollte nie vorkommen, defensive null-Handling).
  - **Warum:** `LinterEngine.RunAsync(Solution, noCache: true, ...)`
  ist exakt die Pipeline, die `get_violations` braucht (siehe
  "Aktueller Projektzustand" oben). Post-Filter ist die ehrliche
  Variante (LinterEngine macht **alles**, wir entscheiden nur noch,
  was wir dem Agenten zeigen) — kein Versuch, eine parallele
  zweite Lint-Schleife zu erfinden, die am Ende ohnehin nur eine
  Teilmenge von `LinterEngine` reproduzieren würde.

### Datei 5: `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` (Zeile 49-60)

- **Was:** Neuen `tools.Add(McpServerTool.Create(...))`-Block für
  `get_violations` ergänzen (Parameter `string? scopeFilter = null,
  CancellationToken ct = default`, gleiche `= null`-Default-Signatur
  wie `get_hotspots` in Zeile 50 — `= null` am Lambda-Parameter ist
  Pflicht, sonst lehnt das MCP-SDK den Aufruf ohne Argument mit
  internem Fehler ab, siehe step-009 Abweichungen-Block). Description-
  Text benennt explizit: C#-only-Scope, optionaler Filter
  (Projekt/Pfad), **kein Disk-Cache** (läuft direkt gegen die
  resident gehaltene Solution — die Cache-Isolation zu parallelen
  CLI-Lint-Läufen auf derselben Solution ist garantiert). Klassen-
  kommentar (Zeile 9-15) aktualisieren: Tool-Liste um
  `get_violations` ergänzen, "Vorbereitet fuer" auf das verbleibende
  `search_pattern` reduzieren.
- **Warum:** Einziger Registrierungspunkt für dateistruktur-orientierte
  Tools (siehe step-007-Aufteilung `SymbolGraphToolRegistrations`/
  `FileStructureToolRegistrations`). Footprint-Realität siehe DoD:
  falls die ~14-18 Zeilen den 45-Zeilen-Puffer überschreiten
  (2455 + 18 = 2473, also grenzwertig), wird in **diesem Step**
  `AnalysisToolRegistrations.cs` als dritte Registrar-Klasse
  umgesetzt, nicht erst reaktiv im nächsten.

### Datei 6: `rules.json` (Ergänzung in `PathOverrides`)

- **Was:** Zwei neue Einträge in `PathOverrides` hinzufügen, **nur
  wenn der Selbst-Lint-Schritt (DoD) zeigt, dass `GetViolationsTool`
  und/oder `GetViolationsScanner` jeweils > 2500 sind**:
  ```json
  "src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs": {
      "Metrics": { "MaxAIContextFootprint": 6000 }
  },
  "src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs": {
      "Metrics": { "MaxAIContextFootprint": 6000 }
  }
  ```
  Konkrete Schwellwerte sind Empfehlungswerte (Faustregel
  "Mindestpuffer über dem gemessenen Wert", konsistent mit dem
  bestehenden `PathOverrides.AuditCommand.cs`-Precedent von 2700
  über dem Default 2500). Coder soll die tatsächlich gemessenen
  Werte einsetzen, gerundet auf nächste 500er-Stufe.
- **Warum:** Beide Klassen ziehen `LinterEngine` + `LinterAnalyzer` +
  alle Checker transitiv mit — TD-005-Muster in Reinkultur, mit dem
  Unterschied, dass die Pull-in-Menge hier **deutlich** größer ist
  als bei `FindReferencesTool` (das nur `SymbolFinder`/`DiffImpactAnalyzer`
  pulled, nicht alle Checker). `PathOverrides` ist der saubere,
  etablierte Mechanismus (siehe `PathOverrides.AuditCommand.cs`)
  — keine dritte Abstraktionsebene (z. B. "dünner Dispatch +
  dünner Dispatch + Scanner + Sub-Scanner") nötig, die das Problem
  nur verschiebt statt löst.

### Datei 7: `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/ViolationTrigger.cs` (neu)

- **Was:** Eine kleine `.cs`-Datei mit einer **einzigen**,
  deterministischen Lint-Violation. Empfehlung:
  ```csharp
  #nullable enable
  namespace SymbolGraphMini;

  public class ViolationTrigger  // fehlendes `sealed` -> EnforceSealedClasses
  {
      public void DoWork() { }
  }
  ```
  Funktioniert mit `rules.json → Global.EnforceSealedClasses: true`
  (Default) und kollidiert nicht mit `EnforceNullableEnable`
  (`#nullable enable` am Dateianfang) oder einer
  `PathOverrides`-Regel (Pfad `src/SymbolGraphMini/...` ist im
  Default-`PathOverrides`-Block von `rules.json` nicht
  ausgenommen, also gilt die globale Regel). **Endgültige
  Violation-Wahl** dem Coder überlassen — Hauptsache: deterministisch
  (kein Balance-Test, der bei zukünftigem Regel-Tuning kippt),
  eine einzige Violation, mit `sealed`-Verstoß als robustester
  Kandidat.
- **Warum:** Die existierenden Fixture-Dateien (`Greeter.cs` etc.)
  sind unter Default-Config lint-clean. Ohne Fixture-Erweiterung
  könnte der `ExecuteAsync_NoScopeFilter_ReturnsViolationForKnownFixture`-
  Test keine deterministische Assertion treffen (würde bei
  Regel-Tuning plötzlich kippen, wenn eine bisher saubere Datei
  eine Violation entwickelt). `ViolationTrigger.cs` ist eine
  **bewusste** Verletzung, die mit der gleichen Regel-Config auch
  in 6 Monaten noch feuert.

### Datei 8: `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs` (neu)

- Tests gegen `GetViolationsScanner` direkt (kein Subprozess), analog
  zur `GetHotspotsToolTests`-Struktur (siehe Testliste unten).

### Datei 9: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:**
  - `RunAsync_ValidFixture_ServerRespondsWithSevenTools` →
    `RunAsync_ValidFixture_ServerRespondsWithEightTools` (Erwartung
    `8`, zusätzliches `Assert.Contains(tools, t => t.Name ==
    "get_violations")`).
  - Neuer E2E-Test `RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation`:
    ruft `get_violations` ohne `scopeFilter` gegen die um
    `ViolationTrigger.cs` erweiterte `SymbolGraphMini`-Fixture auf,
    prüft dass `IsError != true` und der Text mindestens eine
    `ViolationTrigger`-Referenz enthält (die deterministische
    Violation, die `EnforceSealedClasses` meldet).
  - Neuer Unit-Test `ResolveConfig_ConfigWithCustomRules_UsesConfigFromArgs`:
    analog zum bestehenden `ResolveMaxLineCount`-Test (Temp-Config
    mit `{"Global": {}, "Metrics": {"MaxLineCount": 5}}` schreiben,
    `LinterArgs.ConfigPath` darauf zeigen lassen, prüfen dass
    `ResolveConfig` eine nicht-null `Config` mit dem
    konfigurierten Wert liefert).
  - Optional `ResolveConfig_NoConfigPath_ReturnsDefaultConfig`:
    prüft dass ohne `ConfigPath` eine frische `new Config()` mit
    Default-`MaxLineCount: 700` zurückkommt.
- **Warum:** Bestehendes Testmuster (ein E2E-Smoke-Test pro Tool +
  zentraler Tool-Count-Test + dedizierte Unit-Tests für die
  `McpServerCommand`-Hilfsmethoden).

## Tests

- [ ] `GetViolationsToolTests.ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
      (analog zu `GetHotspotsToolTests:14` — `new McpCodeGraphServer(null)`).
- [ ] `GetViolationsToolTests.ExecuteAsync_LoadedSolutionNoScopeFilter_ReturnsViolationForKnownFixture`
      (gegen erweiterte `SymbolGraphMiniFixtureWorkspace` mit
      `ViolationTrigger.cs`; prüft: `IsError != true` UND Text
      enthält "ViolationTrigger" UND der Markdown-Header ist
      vorhanden — exakte Severity-Zuordnung dem Coder überlassen,
      abhängig davon welche Verletzung am Ende gewählt wird).
- [ ] `GetViolationsToolTests.ExecuteAsync_ScopeFilterMatchesProjectName_RestrictsViolations`
      (Filter = `SymbolGraphMini`; Fixture-Violation in
      `SymbolGraphMini/...` ist enthalten).
- [ ] `GetViolationsToolTests.ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessage`
      (Filter = `"DoesNotExistAnywhere"`; explizite "Keine Dateien
      im Scope"-Meldung, kein `IsError`).
- [ ] `GetViolationsToolTests.ExecuteAsync_LoadedSolutionWithScopeFilterContainingViolation_FormatsViolationsAsMarkdownTable`
      (prüft dass die Markdown-Struktur vorhanden ist:
      Tabellen-Header `| Datei | Zeile | Regel | Details |`
      erscheint im Output).
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithEightTools`
      (umbenannt/erweitert, siehe Datei 9).
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation`
      (neu, E2E mit `ViolationTrigger`-Fixture, siehe Datei 9).
- [ ] `McpServerCommandTests.ResolveConfig_ConfigWithCustomRules_UsesConfigFromArgs`
      (neu, siehe Datei 9).
- [ ] `McpServerCommandTests.ResolveConfig_NoConfigPath_ReturnsDefaultConfig`
      (neu, siehe Datei 9 — optional, aber empfohlen).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (Dateien 1-9)
- [ ] `dotnet build AiNetLinter.slnx` grün, 0 Warnungen
- [ ] `dotnet test AiNetLinter.slnx` grün
- [ ] `ainetlinter --config rules.json --path .` selbst-lintet sauber
      (0 Violations) — **wichtig**, weil die PathOverrides (Datei 6)
      in `rules.json` syntaktisch korrekt sein müssen
- [ ] **Selbst-Lint-Footprint-Kontrolle (Pflicht wegen TD-004/TD-005):**
      ```
      --footprint GetViolationsTool              → dokumentiert in step-result.md
      --footprint GetViolationsScanner           → dokumentiert in step-result.md
      --footprint FileStructureToolRegistrations → dokumentiert in step-result.md
      ```
      - Reißt `GetViolationsTool` oder `GetViolationsScanner` das
        Limit (2500): `PathOverrides` für die jeweilige Datei in
        `rules.json` (Datei 6) hinzufügen, **vor** dem
        `ainetlinter`-Selbst-Lint, damit der sauber durchläuft.
      - Reißt `FileStructureToolRegistrations` das Limit (2500):
        **dritte Registrar-Klasse** (`AnalysisToolRegistrations.cs`)
        in **diesem** Step umsetzen, nicht erst reaktiv im nächsten
        (Ausweich-Option, in `step-009/step-plan.md` bereits
        angedeutet, im TD-004-Update der Tech-Debt-Historie
        dokumentiert).
- [ ] **Cache-Bypass-Verifikation (Muss-Haven):** nach
      `dotnet test` einmal `Get-ChildItem <repo-root>/src/AiNetLinter/bin/Debug/net10.0/cache/*.json -ErrorAction SilentlyContinue`
      ausführen — die Datei darf nach den Tests **nicht neuer**
      sein als der `git log --no-pager -1`-Timestamp des aktuellen
      Commits (Test-Prozess darf den Disk-Cache nicht erzeugt
      haben, weil `LinterEngine` mit `noCache: true` aufgerufen
      wird). Im `step-result.md` unter "Selbst-Lint-Footprint-
      Kontrolle" oder einem neuen "Cache-Bypass-Verifikation"-
      Abschnitt das `ls`-Ergebnis dokumentieren.
- [ ] Commit auf `main` (Conventional Commit,
      `[codegraph-mcp]`-Suffix, `### Commit-Vorschlag`-Abschnitt
      laut `AiNetLinterRichtlinien.mdc` §4)
- [ ] **Dogfooding (Muss-Haben, blockierend):** `get_violations`
      einmal ad-hoc gegen die reale `AiNetLinter.slnx` aufrufen
      (Subprozess wie in step-005..step-009, `--path . --config
      rules.json`), Ergebnis in `step-result.md` Abschnitt
      "Dogfooding" dokumentieren. Plausibilitäts-Stichprobe:
      bekannte Lint-Violations der eigenen Codebase (z. B. via
      `ainetlinter --config rules.json --path .` mit Default-Output)
      vs. Tool-Antwort gegenchecken — die Zahlen sollten
      übereinstimmen (mit dem Caveat, dass der CLI-Lauf den
      Disk-Cache nutzt und der MCP-Lauf nicht; bei einer
      Erstausführung beider Pfade sollten die Counts identisch sein
      oder der MCP-Lauf sogar mehr zeigen, weil der Post-Analysis-
      Pfad immer ausgeführt wird).
- [ ] `step-010/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#AIContextFootprint` — 2500-Zeilen-Limit,
  direkt relevant für `GetViolationsTool`/`GetViolationsScanner`/
  `FileStructureToolRegistrations` (siehe DoD; ggf. PathOverrides in
  `rules.json`).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 "Einfachheit vor
  Abstraktion" — `get_violations` ist die konsequente Anwendung: kein
  Neubau einer Lint-Loop, sondern dünn um den bestehenden
  `LinterEngine`-Eintrittspunkt gewickelt.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §2 — kein DI-Container
  (`GetViolationsTool` erreicht `McpCodeGraphServer` weiter per
  Delegate-Closure wie alle bisherigen Tools), keine
  `AssemblyLoadContext`-Magie.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 — `dotnet build`/
  `dotnet test` PowerShell-konform (siehe DoD).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — Update-Pflicht für
  `rules.json` (PathOverrides in Datei 6), `Commit-Vorschlag`-
  Pflicht (siehe DoD), `Docs/ROADMAP.md`-Sync (von der Roadmap-Pflicht
  explizit **befreit**: der `tasks/codegraph-mcp/roadmap.md` ist die
  Drift-Loop-Roadmap, nicht die `Docs/ROADMAP.md`; Anpassungen
  am Konzept/Code-Stand fließen nur hier ein, nicht in die offizielle
  Projektdoku — die bleibt unverändert, bis EPIC-08 abgeschlossen ist).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 — Result-Pattern statt
  Exceptions: `McpToolResults.SolutionNotLoaded()` für den Fehlerpfad,
  `try/catch (Exception ex) when (ex is not OperationCanceledException)`
  defensiv im Scanner für unerwartete Lint-Errors (überspringt die
  betroffene Datei statt zu crashen — analog zur bestehenden
  `GetHotspotsScanner.TryCountLines`-Defense).

## Bekannte Ausnahmen

- **Kein echtes C#-Namespace-Parsing für den Scope-Filter.** Der
  Filter matched case-insensitive `Contains` auf Projekt-Name
  und solution-relativen Dateipfad, nicht auf die
  `namespace`-Deklaration im Dateikopf. Gleiche Vereinfachung
  wie `get_hotspots` (step-009 "Bekannte Ausnahmen"), gleiche
  Begründung: für die meisten .NET-Projekte (Ordnerstruktur ≈
  Namespace-Konvention) liefert das praktisch gleichwertige
  Ergebnisse, ist aber nicht identisch bei
  Datei-Namespace-Abweichungen vom Ordnerpfad. Falls sich das
  in der Praxis (Dogfooding auf großer heterogener Solution)
  als zu ungenau erweist, ist eine Nachschärfung über
  `NamespaceDeclarationSyntax`-Parsing ein Kandidat für einen
  künftigen Tech-Debt-Eintrag, kein Blocker für diesen Step.
- **Post-Filter statt Pre-Filter** der Scope-Einschränkung
  (siehe "Aktueller Projektzustand"): die volle Lint-Pipeline
  läuft auch für Dateien, die nicht im Scope sind; nur die
  Ausgabe wird gefiltert. Vertretbar, weil (a) Parallel-Analyse,
  (b) resident gehaltene `Solution`/`Compilation` (kein
  Re-Msbuild-Load), (c) kein Hot-Path (Orientierungs-Tool).
  Falls künftige Performance-Messungen das als Bottleneck
  identifizieren, ist Pre-Filter per per-doc-Replikation der
  `LinterEngine`-Schleife der Kandidat.
- **Konzept-Notiz "`get_index_scope` braucht keinen neuen
  Datei-Scan" gilt analog für `get_violations` "kein neuer
  Lint-Loop":** Konzept nennt `RuleRegistry`/`LinterEngine` als
  Basis, also direkte Wiederverwendung. `LinterEngine.RunAsync(
  Solution, noCache: true, ...)` ist exakt der richtige
  Wiederverwendungs-Anker.
- **Post-Analysis-Checks (MiddleMan, etc.) laufen mit:** das
  ist beabsichtigt — `get_violations` soll **alle** aktuellen
  Regelverstöße liefern, nicht nur die per-doc-Syntax-Checks.
  Der Cross-Cutting-Anteil ist ein legitimer Teil der Lint-
  Antwort. Konsequenz: die Tool-Antwort kann auch Violations
  enthalten, deren `RuleName` zu einer Post-Analysis-Regel
  gehört (z. B. `AvoidExcessiveMiddleMan`).
- **Kein `PathOverrides` für `LinterEngine.cs` selbst** — die
  Klasse existiert seit langem und ist bereits im
  Produktions-Footprint-Budget. Nur die zwei neuen
  MCP-Tool-Klassen brauchen ggf. einen Override.

## Notes

- **Wiederverwendung, nicht Neubau — quantifiziert:** vier
  bestehende Strukturen werden direkt wiederverwendet (statt
  neu gebaut): (1) `LinterEngine.RunAsync(Solution, noCache:
  true, ...)` als Lint-Pipeline, (2) `McpCodeGraphServer.
  GetCurrentSolution()` als thread-safe Snapshot-Mechanik,
  (3) `ConfigLoader.TryLoadConfig(...)` als Config-Loader
  (gleiches Pattern wie `ResolveMaxLineCount` aus step-009),
  (4) das `GetHotspotsTool`/`GetHotspotsScanner`-Trennmuster
  (dünner Dispatch + separate Logik-Datei). Ein neues
  Modul wird gebaut: `GetViolationsScanner` (Filter +
  Formatierung) — der Rest sind entweder dünne Adapter oder
  bestehende Strukturen.
- **Begründung "medium"-Risiko trotz dünner Adapter-Architektur:**
  drei nicht-triviale Risiko-Faktoren, die das über `low` heben:
  (1) `McpCodeGraphServer.Config`-Property ist ein neuer,
  MCP-Server-globaler Zustand (erst die zweite echte
  Konfigurations-Erweiterung nach `MaxLineCount` in step-009 —
  wenn sich der Coder bei der Additivität der Parameter
  vertut, bricht er die 30+ Test-Konstruktor-Aufrufe); (2)
  `rules.json`-PathOverrides sind ein neuer Eingriffspunkt,
  der syntaktisch korrekt sein muss (sonst Selbst-Lint rot);
  (3) der Cache-Bypass ist über `LinterEngine.noCache: true`
  *konzeptuell* trivial, aber ein ungewollter Refactor an
  `LinterEngine` (z. B. versehentliche `args`-Nutzung statt
  `config`) könnte den Cache-Weg versehentlich wieder öffnen.
  Daher `medium` — nicht weil der Code groß wäre, sondern
  weil die Eingriffspunkte im MCP-Server und in `rules.json`
  liegen und sorgfältig additiv gemacht werden müssen.
- **Wichtig für den Coder:** zuerst DoD-Punkt "Selbst-Lint-
  Footprint-Kontrolle" ausführen, **bevor** die
  `rules.json`-PathOverrides hinzugefügt werden — sonst
  schlägt der Selbst-Lint fehl, bevor die
  PathOverrides greifen. Reihenfolge: (1) alle Dateien
  umsetzen, (2) `dotnet build`/`dotnet test`, (3) Footprint-
  Checks, (4) `rules.json`-PathOverrides **nur wenn nötig**
  hinzufügen, (5) erneut `dotnet build`/`dotnet test` (jetzt
  müssen die Overrides greifen), (6) `ainetlinter --config
  rules.json --path .` muss 0 Violations zeigen.
- **Wichtig für den Coder:** bei `ResolveConfig`-
  Implementierung darauf achten, dass `Config` ein
  `public sealed record` mit `required GlobalConfig Global`
  ist (siehe `Config.cs:7-9`); ein leeres `new Config()`
  (mit `Global = new GlobalConfig()`-Default) ist die
  sichere Wahl, falls `ConfigLoader.TryLoadConfig` `null`
  liefert (z. B. weil `args.ConfigPath` leer ist ODER
  die JSON-Datei kaputt ist). Im Test-Fix in step-009
  wurde derselbe Fall mit minimalem `rules.json` (nur
  `{"Global": {}, "Metrics": {…}}`) abgefangen — diese
  Erfahrung gilt analog.
- **Für den folgenden `search_pattern`-Step (step-011) relevant:**
  die hier etablierte `McpCodeGraphServer.Config`-Property
  ist die **Brücke** für künftige Tools, die ebenfalls
  `rules.json`-Zustand brauchen (z. B. ein
  `search_pattern`, das `FileFiltersConfig.ExcludeFilePatterns`
  aus `Config` respektiert). Der Folge-Planer sollte das
  `McpCodeGraphServer.Config`-Pattern als etabliertes
  Muster übernehmen, nicht erneut über `McpServerCommand`-
  Hilfsmethoden gehen.
- **Footprint-Schätzung (vom Planer vorab, ohne Selbst-Lint):**
  - `GetViolationsTool.cs` (Dispatch, ~25 Zeilen): 2800-3200
    (transitive Pull-in aus `McpCodeGraphServer` +
    `GetViolationsScanner`).
  - `GetViolationsScanner.cs` (Logik, ~80-100 Zeilen): 4500-6000
    (transitive Pull-in aus `LinterEngine` + `LinterAnalyzer` +
    allen Checkern + `RuleRegistry` + `Config`).
  - `FileStructureToolRegistrations.cs` (Erweiterung, ~14-18
    Zeilen): 2469-2473 (45-Zeilen-Puffer reicht knapp aber
    sicher; bei Beschreibungstext-Wachstum auf 20+ Zeilen
    → `AnalysisToolRegistrations` als dritte Klasse).
  → **Wahrscheinliche Konsequenz:** PathOverrides in
    `rules.json` für `GetViolationsTool` und
    `GetViolationsScanner`; dritte Registrar-Klasse vermutlich
    **nicht** nötig in diesem Step (45-Zeilen-Puffer reicht
    für die ~14-18 Zeilen), aber DoD-Pflicht-Selbst-Lint
    verifiziert das.

### Commit-Vorschlag (Form/Struktur, vom Coder auszufüllen)

**Konvention (vom Planer vorgegeben):** `feat(mcp): add get_violations tool [codegraph-mcp]`

**Body-Struktur-Hinweise für den Coder:**

- Konzept-Referenz: `get_violations` aus `konzept.md` Tool-Tabelle
  + Muss-Haven "Cache umgehen" (kein Disk-Cache, läuft direkt gegen
  die resident gehaltene `Solution`).
- Wesentliche Änderungen:
  - `GetViolationsTool.cs`/`GetViolationsScanner.cs` neu (TD-005-
    Muster: dünner Dispatch + separate Logik-Datei).
  - `McpCodeGraphServer` um additive `Config`-Property erweitert
    (zweite Konfigurations-Erweiterung nach `MaxLineCount` in
    step-009).
  - `McpServerCommand.ResolveConfig`-Helper neu (Pattern identisch
    zu `ResolveMaxLineCount` aus step-009).
  - `FileStructureToolRegistrations` um `get_violations` ergänzt.
  - `rules.json`-PathOverrides für `GetViolationsTool`/
    `GetViolationsScanner` (nur wenn Selbst-Lint-Footprint > 2500).
  - `tests/Fixtures/SymbolGraphMini/.../ViolationTrigger.cs` neu
    (Fixture-Erweiterung für deterministische Violation im
    Unit-Test).
- Begründung Threading: bestehender `McpCodeGraphServer.GetCurrentSolution`-Lock
  deckt `Solution`-Snapshot-Mechanik ab; `LinterEngine` ist
  read-only gegen die `MSBuildWorkspace`, mehrere parallele
  Aufrufe sind sicher.
- TD-Referenz: TD-005 (Scanner-Muster), TD-004 (Footprint-Realität),
  ggf. TD-007 (Konsequenz aus McpCodeGraphServer-Erweiterung —
  unverändert, keine neue Beobachtung).
- E2E-Hinweis: Tool-Anzahl 7 → 8.

**Finale Message-Formulierung ist Sache des Coders** (Conventional
Commit auf Englisch, Body prägnant, Detailgrad wie in den
vorherigen `feat(mcp):` Commits).
