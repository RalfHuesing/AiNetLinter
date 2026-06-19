# 01 — Architektur-Befunde

> Detaillierte Analyse der architektonischen Schwachstellen mit Code-Referenzen.
> Empfehlungen siehe [`03-Architektur-Refactoring-Vorschlaege.md`](03-Architektur-Refactoring-Vorschlaege.md).

---

## A1 — `LinterAnalyzer` ist eine monolithische Visitor-Klasse

**Datei:** `src/AiNetLinter/Core/LinterAnalyzer.cs` (271 LOC)

### Befund

`LinterAnalyzer` ist ein einzelner `CSharpSyntaxWalker`, der **14 Checker direkt aufruft** über starre `VisitXxx`-Methoden. Jede neue Regel erfordert eine Änderung der Klasse selbst:

```csharp
// LinterAnalyzer.cs, Zeilen 79–94
public override void VisitClassDeclaration(ClassDeclarationSyntax node)
{
    if (_ctx.Config.FileFilters.SkipGeneratedCodeAttribute && ArchitectureChecker.IsGeneratedCode(node, _ctx))
        return;
    NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Klasse", _ctx);
    NamingChecker.CheckPascalCase(node.Identifier, "Klasse", _ctx);
    ArchitectureChecker.CheckSealedClass(node, _ctx);
    ArchitectureChecker.CheckValueObjectContract(node, node.Identifier.Text, isRecord: false, _ctx);
    ScopeChecker.CheckMethodOverloads(node, _ctx);
    StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
    ImmutabilityChecker.CheckClass(node, _ctx);
    WpfSeparationChecker.Check(node, _ctx);
    NestedTypesChecker.Check(node, _ctx);
    PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
    ArchitectureChecker.CollectClassInfo(node, _ctx);
    base.VisitClassDeclaration(node);
}
```

**Konsequenzen:**

- **Open/Closed-Prinzip verletzt** — neue Regeln erfordern Modifikation der Basisklasse
- **Reihenfolge der Checker ist hartkodiert** — z. B. `NamingChecker.CheckXmlDoc` läuft vor `ArchitectureChecker.CollectClassInfo`, aber bei Records fehlt `CollectClassInfo` → ClassInfo-Datensätze unvollständig für Records
- **Schwer testbar isoliert** — ein Checker-Test muss immer den ganzen `LinterAnalyzer` hochfahren
- **Hohe kognitive Last** — 271 LOC für eine einzige Klasse mit über 12 Verantwortlichkeiten

### Klassifikation

- **Prio:** 🔴 Hoch
- **Aufwand:** M (1–3 Tage für Plugin-Pipeline-Refactoring)
- **Nutzen:** ★★★★★ — neuer Checker hinzufügen wird 5× schneller

---

## A2 — `Program.cs` als CLI-Mono-Router (568 LOC)

**Datei:** `src/AiNetLinter/Program.cs`

### Befund

`Program.cs` enthält **alle 8 Sub-Befehle** in einer einzigen Klasse:

| Methode                      | Zeile | Zweck                                                   |
| ---------------------------- | ----- | ------------------------------------------------------- |
| `RunAuditAsync`              | 156   | Standard-Lint-Run                                       |
| `RunAuditWithBaselineAsync`  | 203   | Audit mit Baseline-Filter                               |
| `RunSyncCursorRules`         | 487   | Cursor-Regel-Sync                                       |
| `RunPrintReadme`             | 539   | Eingebettete README ausgeben                            |
| `RunPlaybookCheckAsync`      | 350   | Playbook-Drift-Check                                    |
| `TryRunMaintenanceModeAsync` | 374   | Maintenance-Modi (CreateBaseline, Add/RemoveDisableAll) |
| `AuditWithBaselineAsync`     | 395   | Baseline-Workflow                                       |
| `ToLinterArgs`               | 66    | CLI → Domain-Mapping                                    |
| `ApplyAutoFixIfNeededAsync`  | 263   | Auto-Fix-Wrapper                                        |

**Probleme:**

- `ToLinterArgs` (Zeile 66–94) ist ein **25-Felder-1:1-Mapper** ohne Validierung
- Verschachtelte Try-Ketten (`RunAuditAsync` → `RunAuditWithBaselineAsync` → `AuditWithBaselineAsync`) → schwer nachvollziehbar
- Keine Trennung zwischen "was ausführen" und "wie ausgeben" → Program.cs mischt Orchestrierung und IO
- Direkter Zugriff auf `PerformanceProfiler.Instance.StartPhase(...)` an 6 Stellen (Zeilen 164, 167, 175, 177, 180, 184, 186, 188)

### Klassifikation

- **Prio:** 🔴 Hoch
- **Aufwand:** M
- **Nutzen:** ★★★★ — Testbarkeit + Erweiterbarkeit für neue Modi

---

## A3 — Rule-Definitionen sind 3-fach dupliziert

**Dateien:** `CursorRulesGenerator.cs`, `ViolationTextFormatter.cs`, `RepoPlaybookGenerator.cs`

### Befund

Die gleichen Regel-Beschreibungen existieren **dreimal** im Code, jeweils mit unterschiedlichem Inhalt und teils **falschen Werten**:

| Datei                       | Speicherort                                             | Zweck                          | Beispiele                                                                                                                                     |
| --------------------------- | ------------------------------------------------------- | ------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------- |
| `CursorRulesGenerator.cs`   | `GlobalRules[]` (Z. 30–91), `MetricsList[]` (Z. 93–109) | `.mdc` für Cursor generieren   | `"EnforceSealedClasses"` → `"Konkrete Klassen muessen 'sealed' sein"`                                                                         |
| `ViolationTextFormatter.cs` | `RuleInstructions` Dict (Z. 105–134)                    | LLM-Output für jedes Violation | `"EnforceSealedClasses"` → `"-> EnforceSealedClasses: Konkrete Klassen muessen 'sealed' sein. Bei partial Klassen nutze 'sealed partial'..."` |
| `RepoPlaybookGenerator.cs`  | `RuleDescriptions` Dict (Z. 36–59)                      | Playbook-Markdown              | `"EnforceSealedClasses"` → `"Konkrete Klassen muessen 'sealed' sein (oder 'sealed partial')."`                                                |

**Konkrete Inkonsistenz / Bug:**

- `RepoPlaybookGenerator.cs:42` → `"max. 4 Parameter"` → korrekt (Default ist 4)
- `RepoPlaybookGenerator.cs:43` → `"max. 42 Zeilen"` → **falsch** (Default ist 60)
- `RepoPlaybookGenerator.cs:44` → `"max. 5 Zyklomatisch"` → **falsch** (Default ist 12)
- `RepoPlaybookGenerator.cs:45` → `"max. 5 Kognitiv"` → **falsch** (Default ist 15)
- `RepoPlaybookGenerator.cs:40` → `"Dateizeilenlimit (max. 500 Zeilen)"` → **falsch** (Default ist 700)
- `RepoPlaybookGenerator.cs:55` → `"max. 10 Methodenueberladungen"` → **falsch** (Default ist 3)
- `RepoPlaybookGenerator.cs:56` → `"max. 20 Konstruktorabhaengigkeiten"` → **falsch** (Default ist 5)

→ **F10 (Quick Win)** — diese Werte ziehen direkt aus der `LinterConfig`, sind aber hardcoded.

### Klassifikation

- **Prio:** 🔴 Hoch
- **Aufwand:** M (zentrale `IRuleRegistry`)
- **Nutzen:** ★★★★★ — Bugfix + Single-Source-of-Truth für alle Regel-Metadaten

---

## A4 — `PerformanceProfiler` als globaler Singleton

**Datei:** `src/AiNetLinter/Diagnostics/PerformanceProfiler.cs` (349 LOC)

### Befund

```csharp
// PerformanceProfiler.cs, Zeilen 19–24
private static readonly Lazy<PerformanceProfiler> LazyInstance = new(() => new PerformanceProfiler());
public static PerformanceProfiler Instance => LazyInstance.Value;
```

**Probleme:**

- **Singletons in Produktionscode** sind ein klassisches Anti-Pattern (Versteckt Abhängigkeiten, schlecht testbar)
- Schreibt in `measurements/<Solution>-<Timestamp>-<Guid>/performance.json` (Zeile 196) **bei jedem Lauf** wenn aktiviert → ungewollte Disk-IO in Produktion
- `RecordDocumentAnalysis` (Zeile 108) sammelt **alle Datei-Pfade ungefiltert** → Speicher wächst linear mit Solution-Größe
- `_initialized`-Flag (Zeile 26) ist **nicht thread-safe** — bei parallelen Tests potentiell Race-Conditions
- `Console.WriteLine($"[INFO]: Performance-Messdaten erzeugt unter: {targetDir}")` (Zeile 164) → Profil-Output gelangt in LLM-Sicht

**Bessere Alternative:**

- Dependency-Injection von `IPerformanceProfiler` (oder `IProfiler`) in `LinterEngine`
- Profiling nur unter `--profile`-Flag, nie implizit
- Output immer in `obj/`, nie im Zielverzeichnis

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** S
- **Nutzen:** ★★★★ — Testbarkeit + Performance-Klarheit

---

## A5 — `LinterEngine` hat 3× `RunAsync`-Overloads mit ähnlicher Cache-Logik

**Datei:** `src/AiNetLinter/Core/LinterEngine.cs` (Zeilen 35–57)

### Befund

```csharp
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(string path, bool noCache = false, int cacheTtlMinutes = 60) { ... }
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(SourceFileCatalog catalog, bool noCache = false, int cacheTtlMinutes = 60) { ... }
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(Solution solution, bool noCache = false, int cacheTtlMinutes = 60) { ... }
```

Jeder dieser Overloads:

1. Ruft `BuildCache(...)` mit unterschiedlichen `path`-Argumenten auf
2. Delegiert an `RunInternalAsync(solution, catalog, cache)`

→ Die 3 Methoden unterscheiden sich nur in der **Eingabe-Extraktion**, nicht im eigentlichen Verhalten.

**Konsequenzen:**

- Cache-Schlüssel wird 3× leicht unterschiedlich berechnet (`catalog?.Solution?.FilePath ?? path` vs. `solution.FilePath ?? solution.Workspace.GetType().Name`) → Cache-Misses möglich
- `BuildCache` (Zeile 59–70) hat ein `solutionPath`-Argument, das nicht eindeutig die Cache-Identität ist
- `cacheTtlMinutes` ist ein Default mit Magic-Number 60

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** S (Strategy-Pattern oder Eingabe-Pipeline)
- **Nutzen:** ★★★ — Korrektheit + Konsistenz

---

## A6 — Triple-Layer `Apply` mit 5 unnötigen Klonings

**Datei:** `src/AiNetLinter/Configuration/LinterConfig.cs` (Zeilen 195–252, 433–480)

### Befund

```csharp
// GlobalConfig.cs, Zeilen 185–252
public GlobalConfig Apply(GlobalConfigOverride? @override)
{
    if (@override == null) return this;
    return ApplyStructuralRules(@override)
        .ApplyNamingAndStyleRules(@override)
        .ApplyCatchRules(@override)
        .ApplyImmutabilityRules(@override)
        .ApplyNamespaceAndAnalysisRules(@override);
}

private GlobalConfig ApplyStructuralRules(GlobalConfigOverride o) => this with { ... };
private GlobalConfig ApplyNamingAndStyleRules(GlobalConfigOverride o) => this with { ... };
// ... 3 weitere Methoden
```

**Probleme:**

- 5 separate `this with { ... }`-Aufrufe → **5 unnötige Klonings** pro Override-Anwendung
- Bei einem Projekt mit 100 Dateien und 5 Overrides → 500 Klonings statt 1
- Methodenaufteilung dient nur der Lesbarkeit, nicht der Korrektheit
- Gleiches Pattern in `MetricsConfig.Apply` (Zeilen 424–480)

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** N (Extension-Method-Pattern)
- **Nutzen:** ★★★ — Performance + Lesbarkeit

---

## A7 — Fehlende `CancellationToken`-Propagation

**Dateien:** `LinterEngine.cs`, `LinterAnalyzer.cs`, `Program.cs`

### Befund

Keine einzige `async`-Methode akzeptiert ein `CancellationToken`-Argument:

```csharp
// LinterEngine.cs, Zeilen 35, 44, 53
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(string path, ...) { ... }
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(SourceFileCatalog catalog, ...) { ... }
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(Solution solution, ...) { ... }

// LinterEngine.cs, Zeile 131
await Parallel.ForEachAsync(workItems, CreateParallelOptions(), (item, _) =>
    AnalyzeWorkItemAsync(item, state, cache));
//                                  ^ CancellationToken nicht weitergereicht
```

**Konsequenzen:**

- Bei `Ctrl+C` während eines Lint-Runs läuft die Pipeline weiter bis zum nächsten IO-Punkt
- `MSBuildWorkspace.OpenSolutionAsync` ohne Token → unkontrolliertes Warten
- `Parallel.ForEachAsync` mit `_` als Token-Parameter → keine kooperative Stornierung

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** N (Standard-Refactoring)
- **Nutzen:** ★★★★ — UX + Robustheit

---

## A8 — `Console.WriteLine` als universelle Logging-Schnittstelle

**Vorkommen:** 25+ Stellen in Produktionscode

### Befund

Beispiele:

- `Program.cs:51` → `Console.WriteLine($"# Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");`
- `Program.cs:286` → `Console.WriteLine($"[DRY-RUN]: {fixedCount} ...");`
- `Program.cs:528` → `Console.WriteLine($"[INFO]: Cursor-Regeldatei ist bereits aktuell ...");`
- `LinterAutoFixer.cs:119` → `Console.WriteLine($"[DRY-RUN]: Würde {fixedCount} Fix(es) anwenden auf: {document.Name}");`
- `LinterAutoFixer.cs:133` → `Console.WriteLine($"[INFO]: Automatischer Fix angewendet auf: {docName}");`
- `SourceFileCatalog.cs:52` → `Console.Error.WriteLine($"[WARN]: Workspace-Diagnose: {msg}");`
- `CursorRulesGenerator.cs:130, 139` → `Console.WriteLine("[INFO]: ...")`

**Konsequenzen:**

- Für Tests: schwer prüfbar (muss `Console.SetOut` mocken)
- Für Agenten: keine strukturierte Trennung zwischen Info/Warn/Error
- Für Multi-Output (Text + SARIF): nicht differenzierbar
- Übersetzungs-Strings im Code → keine i18n

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** M (zentrale `ILogger`-Abstraktion)
- **Nutzen:** ★★★★ — Testbarkeit + Agent-Integration

---

## A9 — `SourceFileCatalog` ist IDisposable mit mutierbarem internen Workspace

**Datei:** `src/AiNetLinter/Baseline/SourceFileCatalog.cs`

### Befund

```csharp
// SourceFileCatalog.cs, Zeilen 14–21
private readonly MSBuildWorkspace? _workspace;

private SourceFileCatalog(MSBuildWorkspace? workspace, Solution solution, bool hasLoadingErrors)
{
    _workspace = workspace;
    Solution = solution;
    HasLoadingErrors = hasLoadingErrors;
}
```

**Probleme:**

- `MSBuildWorkspace` ist `IDisposable` (Zeile 120) → muss explizit disposed werden
- `WithUpdatedSolution` (Zeile 63–66) erstellt neue Instanz mit **demselben Workspace** → Workspace kann während AutoFix wiederverwendet werden
- Aber: `LinterEngine` verwendet `catalog.Solution` direkt (Zeile 47) und kennt nicht den Lifecycle des Workspace → potenzielle `ObjectDisposedException` möglich
- `internal SourceFileCatalog(Solution solution, bool hasLoadingErrors)` (Zeile 23) → zwei Konstruktoren, zwei Felder-Initialisierungen → Verletzung von DRY

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** S
- **Nutzen:** ★★★ — Lifecycle-Klarheit

---

## A10 — Rule-Namen als String-Literale

**Vorkommen:** 60+ Stellen im Code

### Befund

```csharp
// ArchitectureChecker.cs, Zeile 50–57
ctx.AddViolation(new RuleViolation
{
    ...
    RuleName = nameof(ctx.Config.Global.EnforceSealedClasses),  // OK, via nameof
    ...
});

// CursorRulesGenerator.cs, Zeile 31
new("EnforceSealedClasses", g => g.EnforceSealedClasses, ...)
//                                ^ String-Literal statt nameof

// ViolationTextFormatter.cs, Zeile 107
["EnforceSealedClasses"] = "-> EnforceSealedClasses: Konkrete Klassen muessen 'sealed' sein..."
//  ^ String-Literal
```

**Probleme:**

- `nameof(ctx.Config.Global.EnforceSealedClasses)` ist konsequent genutzt in Checkern → **gut**
- Aber `CursorRulesGenerator` und `ViolationTextFormatter` verwenden String-Literale → Refactoring einer Property bricht sie
- `RuleViolation.RuleName` ist ein String → keine Compile-Time-Validierung der Regel-Schlüssel in `RuleInstructions`, `RuleDescriptions`, `GlobalRules`

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** N (Const-Klasse `LinterRuleIds`)
- **Nutzen:** ★★★ — Refactoring-Sicherheit

---

## A11 — Tests liegen unter `src/AiNetLinter.Tests/`, nicht `tests/AiNetLinter.Tests/`

**Befund:** `src/AiNetLinter.Tests/` enthält 100+ Test-Klassen, aber `tests/` enthält nur Fixtures (und leere Verzeichnisse). Die `.slnx`-Datei referenziert `src/AiNetLinter.Tests`. Pfad-Inkonsistenz → verwirrend für Mitwirkende.

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** XS (Move + .slnx anpassen)
- **Nutzen:** ★★ — Onboarding-Klarheit

---

## A12 — `Cache` und `SolutionBasePath`-Lifecycle koppelt MSBuildWorkspace an Analyse

**Datei:** `src/AiNetLinter/Core/LinterEngine.cs` (Zeilen 207–217)

### Befund

```csharp
// LinterEngine.cs, Zeile 207
private LinterConfig ResolvePostAnalysisConfig(Solution solution)
{
    if (_config.SolutionBasePath != null)
        return _config;
    var dir = GetSolutionDir(solution);
    return string.IsNullOrEmpty(dir) ? _config : _config with { SolutionBasePath = dir };
}
```

**Problem:** Lösung mutiert die Konfiguration zur Laufzeit über `with { SolutionBasePath = dir }` → das ist **eine Workaround-Lösung**, die nicht sauber durch die Architektur geführt wird. Die Konfiguration sollte **bereits beim Laden** den SolutionBasePath kennen.

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** XS (in `SourceFileCatalog.LoadAsync` verschieben)
- **Nutzen:** ★★★ — Klarheit

---

## A13 — `SuppressionEvaluator.IsSuppressed` ist O(N×M)

**Datei:** `src/AiNetLinter/Suppression/SuppressionEvaluator.cs`

### Befund

```csharp
// SuppressionEvaluator.cs, Zeilen 18–36
public static bool IsSuppressed(string fileContent, string ruleName, int lineNumber)
{
    var lines = fileContent.Split('\n');
    foreach (var line in lines)
    {
        if (SuppressionCommentParser.MatchesRule(line, ruleName))
        {
            return true;
        }
    }
    ...
}
```

**Problem:** Bei jeder Violation wird der gesamte File-Content erneut gesplittet → bei 100 Violations × 500 Zeilen × O(MatchesRule) → quadratische Komplexität pro Datei.

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** N (Index beim ersten Aufbau)
- **Nutzen:** ★★★ — Performance bei großen Dateien

---

## A14 — `LinterConfigNormalizer` nicht inspiziert, aber existiert

**Datei:** `src/AiNetLinter/Configuration/LinterConfigNormalizer.cs` (nicht gelesen, aber im Codegraph referenziert)

→ Im Audit-Bericht nicht detailliert betrachtet; möglicherweise Quelle für Drift-Probleme (TODO: separat prüfen).

---

## A15 — Keine Code-Coverage-Statistik verfügbar

**Befund:** Es gibt keine Coverage-Statistik im Repo. Tests-Fixtures (`tests/Fixtures/BaselineMini/`) sind minimal (16 Dateien). Es ist nicht klar, welche Checker getestet sind und welche nicht.

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** S (coverlet + Coverage-Threshold in CI)
- **Nutzen:** ★★★ — Vertrauen in Refactorings

---

## 🎯 Zusammenfassung Architektur-Befunde

**Hauptproblem:** Der Code wuchs **rule-orientiert** (jeder Epic brachte neue Checker hinzu), aber nicht **architektur-orientiert**. Das Resultat ist ein hochfunktionales, aber strukturell fragiles System:

- 14 Checker sind statisch in `LinterAnalyzer` verdrahtet (A1)
- Regel-Beschreibungen sind 3-fach dupliziert (A3)
- Singleton-Anti-Pattern in `PerformanceProfiler` (A4)
- CLI-Routing vermischt mit Orchestrierung (A2)

→ Die folgenden Refactorings (siehe `03-Architektur-Refactoring-Vorschlaege.md`) sind **Voraussetzung** für die nächsten 5+ Epics ohne weiteres Architektur-Drift.
