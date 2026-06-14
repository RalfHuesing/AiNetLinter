# Plan: Kombinierten Lauf (Test + Playbook + Graph) ohne doppelte Analyse

## Intention

Ziel ist es, die drei häufig kombinierten Operationen

- **a) normaler Lint-Lauf** (`--config rules.json --path ...`)
- **b) Playbook-Ausgabe** (`--playbook path/to/playbook.md`)
- **c) Graph-Ausgabe** (`--graph path/to/graph.md`)

so zu verschmelzen, dass die teure Roslyn-Analyse **genau einmal** ausgeführt wird — auch wenn alle drei gleichzeitig angefordert werden.

---

## Ist-Zustand (Problem)

### Was funktioniert bereits

Die CLI akzeptiert alle drei Optionen gleichzeitig. `GenerateOptionalOutputsAsync` in `Program.cs` erzeugt Graph und Playbook auf derselben geladenen Solution (`catalog.Solution`), **bevor** der eigentliche Audit-Lauf startet. Der Workspace-Ladevorgang (MSBuild, Roslyn) findet also nur einmal statt.

### Das Problem: doppelte `engine.RunAsync`-Aufrufe

```
RunAuditAsync (Program.cs)
│
├── SourceFileCatalog.LoadAsync        ← Workspace laden (einmalig, korrekt)
│
├── GenerateOptionalOutputsAsync
│   ├── CodegraphGenerator.GenerateAsync        ← kein RunAsync, OK
│   └── RepoPlaybookGenerator.GenerateAsync
│       └── ScanSolutionAsync
│           └── engine.RunAsync(solution)       ← ANALYSE #1 (für Migrations-Stats)
│
└── ExecuteAuditAsync
    └── AuditWithoutBaselineAsync (oder WithBaseline)
        └── engine.RunAsync(catalog)            ← ANALYSE #2 (für Lint-Ausgabe)
```

**Beide `RunAsync`-Aufrufe führen die vollständige parallele Dokumentenanalyse durch
(inkl. SemanticModel, SyntaxWalker, alle Linter-Regeln).** Das ist der teuerste
Teil des Tools — bei großen Solutions mehrere Sekunden bis Minuten.

### Betroffene Dateien

| Datei | Relevante Stelle |
|---|---|
| `src/AiNetLinter/Program.cs` | `RunAuditAsync`, `GenerateOptionalOutputsAsync`, `TryGeneratePlaybookAsync` |
| `src/AiNetLinter/Core/RepoPlaybookGenerator.cs` | `ScanSolutionAsync` (Zeile 130–169), `GenerateAsync`, `BuildContentAsync` |

---

## Lösungsansatz

### Kernidee

Den **einen** `engine.RunAsync`-Aufruf nach vorne ziehen und das Ergebnis
(`IReadOnlyCollection<RuleViolation>`) sowohl an den Playbook-Generator als auch
an die Audit-Ausgabe weitergeben — keine zweite Analyse mehr.

### Reihenfolge nach dem Refactoring

```
RunAuditAsync (neu)
│
├── SourceFileCatalog.LoadAsync               ← Workspace laden (wie bisher)
│
├── engine.RunAsync(catalog)                  ← ANALYSE (einmalig)
│   └── → violations
│
├── TryGenerateCodegraphAsync(solution)       ← Graph (kein RunAsync, wie bisher)
│
├── TryGeneratePlaybookAsync(solution,        ← Playbook (violations übergeben,
│     violations)                                kein eigenes RunAsync mehr)
│
└── WriteViolationsAndExit(violations)        ← Ausgabe (wie bisher)
```

---

## Konkrete Änderungen

### 1. `RepoPlaybookGenerator` — Violations von außen akzeptieren

**Datei:** `src/AiNetLinter/Core/RepoPlaybookGenerator.cs`

#### Neue Signatur für `GenerateAsync` und `BuildContentAsync`

```csharp
// Vorher
public static async Task GenerateAsync(
    Solution solution, string outputPath, bool verbose,
    LinterConfig? config = null, string configPath = "rules.json")

// Nachher
public static async Task GenerateAsync(
    Solution solution, string outputPath, bool verbose,
    LinterConfig? config = null, string configPath = "rules.json",
    IReadOnlyCollection<RuleViolation>? precomputedViolations = null)
```

```csharp
// Vorher
public static async Task<string> BuildContentAsync(
    Solution solution, bool verbose,
    LinterConfig? config = null, string configPath = "rules.json")

// Nachher
public static async Task<string> BuildContentAsync(
    Solution solution, bool verbose,
    LinterConfig? config = null, string configPath = "rules.json",
    IReadOnlyCollection<RuleViolation>? precomputedViolations = null)
```

#### `ScanSolutionAsync` — bedingter engine.RunAsync

```csharp
private static async Task<PlaybookStats> ScanSolutionAsync(
    Solution solution, LinterConfig? config, string configPath,
    IReadOnlyCollection<RuleViolation>? precomputedViolations = null)
{
    // ... Syntax-Scan wie bisher (Result-Pattern, Throws, Suppressions) ...

    List<RuleViolation> violations = new();
    if (config != null)
    {
        if (precomputedViolations != null)
        {
            // Violations aus dem Haupt-Lauf wiederverwenden — kein zweites RunAsync
            violations.AddRange(precomputedViolations);
        }
        else
        {
            // Standalone-Aufruf (nur --playbook, kein kombinierter Lauf)
            string? rulesJsonContent = null;
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                rulesJsonContent = File.ReadAllText(configPath, Encoding.UTF8);
            var engine = new LinterEngine(config, rulesJsonContent);
            var results = await engine.RunAsync(solution);
            violations.AddRange(results);
        }
    }

    return new PlaybookStats(totalResultMethods, totalThrows,
        suppressionCounts, docInfos, violations);
}
```

Der Syntax-Scan (Result-Pattern, Throws, Suppressions, Namespaces, LineCount)
läuft dabei **immer** — er liest nur SyntaxTrees, nicht SemanticModels für
Violations, und ist deutlich billiger als die vollständige Linter-Analyse.

---

### 2. `Program.cs` — Aufruf-Reihenfolge umstellen

**Datei:** `src/AiNetLinter/Program.cs`

#### `RunAuditAsync` refaktorieren

```csharp
private static async Task<int> RunAuditAsync(LinterArgs args)
{
    var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
    if (config == null) return 1;

    PerformanceProfiler.Instance.Initialize(config.Global.EnablePerformanceProfiling);
    LinterLogger.LogStart(args.Verbose, args.ConfigPath!, args.TargetPath);

    PerformanceProfiler.Instance.StartPhase("WorkspaceLoading");
    using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath);
    PerformanceProfiler.Instance.StopPhase("WorkspaceLoading");

    // AutoFix muss vor der Haupt-Analyse laufen (wie bisher, ändert sich nicht)
    PerformanceProfiler.Instance.StartPhase("AutoFix");
    var (currentCatalog, needsDispose) = await ApplyAutoFixIfNeededAsync(catalog, config, args);
    PerformanceProfiler.Instance.StopPhase("AutoFix");

    try
    {
        // Haupt-Analyse: einmalig ausführen
        PerformanceProfiler.Instance.StartPhase("DocumentAnalysis");
        string? rulesJsonContent = LoadRulesJsonContent(args.ConfigPath);
        var engine = new LinterEngine(config, rulesJsonContent);
        var violations = await engine.RunAsync(currentCatalog, args.NoCache);
        PerformanceProfiler.Instance.StopPhase("DocumentAnalysis");

        // Optionale Ausgaben mit den bereits berechneten Violations
        PerformanceProfiler.Instance.StartPhase("OptionalOutputs");
        await GenerateOptionalOutputsAsync(currentCatalog.Solution, args, config, violations);
        PerformanceProfiler.Instance.StopPhase("OptionalOutputs");

        // Ausgabe der Violations (kein erneutes RunAsync)
        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
        var scoped = ApplyScopeFilters(violations, args, outputRoot, onlyChangedFiles: []);
        var exitCode = WriteViolationsAndExit(scoped, args.Format, outputRoot, config);

        PerformanceProfiler.Instance.WriteReport(args.TargetPath,
            currentCatalog.Solution.FilePath, args.ConfigPath);
        return exitCode;
    }
    finally
    {
        if (needsDispose) currentCatalog.Dispose();
    }
}
```

> **Hinweis:** Die Baseline-Variante (`AuditWithBaselineAsync`) benötigt eine
> separate Behandlung, da sie Checksums berechnet und die Violations filtert —
> sie sollte zunächst unverändert bleiben und in einem Folge-Schritt angepasst
> werden.

#### `GenerateOptionalOutputsAsync` — Violations-Parameter ergänzen

```csharp
private static async Task GenerateOptionalOutputsAsync(
    Solution solution, LinterArgs args, LinterConfig config,
    IReadOnlyCollection<RuleViolation>? violations = null)   // NEU
{
    if (args.GraphPath != null)
    {
        await TryGenerateCodegraphAsync(solution, args.GraphPath, args.Verbose);
    }

    if (args.PlaybookPath != null)
    {
        await TryGeneratePlaybookAsync(solution, args.PlaybookPath,
            args.Verbose, config, args.ConfigPath ?? "rules.json",
            violations);   // NEU: violations weitergeben
    }

    if (args.SyncCursorRules)
    {
        TrySyncCursorRules(args.TargetPath, config, args.Verbose);
    }
}
```

#### `TryGeneratePlaybookAsync` — Parameter durchreichen

```csharp
private static async Task TryGeneratePlaybookAsync(
    Solution solution, string playbookPath, bool verbose,
    LinterConfig config, string configPath,
    IReadOnlyCollection<RuleViolation>? violations = null)   // NEU
{
    try
    {
        if (verbose) Console.WriteLine($"[INFO]: Generiere Repo-Playbook unter: {playbookPath}");
        await RepoPlaybookGenerator.GenerateAsync(
            solution, playbookPath, verbose, config, configPath, violations);   // NEU
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[ERROR]: Fehler beim Generieren des Repo-Playbooks: {ex.Message}");
    }
}
```

#### Hilfsmethode (Extraktion aus dupliziertem Code)

```csharp
private static string? LoadRulesJsonContent(string? configPath)
{
    if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
        return null;
    return File.ReadAllText(configPath, Encoding.UTF8);
}
```

---

### 3. Baseline-Pfad (`AuditWithBaselineAsync`) — vorerst unverändert

Der Baseline-Pfad hat eine eigene Logik (Checksums, ChangedFiles-Filter) und
ruft ebenfalls `engine.RunAsync` auf. Er ist in der Regel kein kombinierter Lauf
mit `--playbook`/`--graph`, daher wird er in diesem Schritt **nicht angefasst**.
Eine Folgeoptimierung wäre möglich, hat aber geringere Priorität.

---

### 4. `RunPlaybookCheckAsync` — kein Handlungsbedarf

Der `--check`-Modus für das Playbook (`RunPlaybookCheckAsync`) läuft ohne
gleichzeitigen Lint-Lauf und bleibt unverändert. Die neuen optionalen Parameter
von `BuildContentAsync` haben Default-Werte (`null`), sodass kein Breaking Change
entsteht.

---

## Risikoabschätzung

| Aspekt | Bewertung |
|---|---|
| **Korrektheit** | Violations für Playbook und Lint-Ausgabe stammen aus derselben Analyse → identisch |
| **Reihenfolge** | AutoFix ändert die Solution; Analyse muss **nach** AutoFix laufen (schon so geplant) |
| **Baseline-Pfad** | Unverändert → kein Regressionsrisiko |
| **Standalone `--playbook`** | `precomputedViolations == null` → fällt auf heutiges Verhalten zurück |
| **Tests** | Existierende Tests für `RepoPlaybookGenerator` bleiben unverändert (Default-Parameter) |

---

## Erwarteter Performance-Gewinn

Der teure Teil ist `engine.RunAsync` — parallele SemanticModel-Auswertung über
alle Dokumente. Durch die Zusammenführung entfällt **ein vollständiger Durchlauf**
bei kombiniertem Aufruf von `--config` + `--playbook`. Der Gain entspricht
grob der Hälfte der heutigen Gesamt-Laufzeit für dieses Szenario.

---

## Umsetzungs-Reihenfolge

1. `RepoPlaybookGenerator.cs` — optionalen `precomputedViolations`-Parameter in
   `ScanSolutionAsync`, `BuildContentAsync` und `GenerateAsync` einbauen
2. `Program.cs` — `RunAuditAsync` umstrukturieren (Analyse vor OptionalOutputs),
   `GenerateOptionalOutputsAsync` und `TryGeneratePlaybookAsync` anpassen
3. Build + bestehende Tests grün halten
4. Manueller Test: `--config rules.json --path . --graph graph.md --playbook playbook.md`
   und Vergleich der Laufzeit mit/ohne Änderung via `--perf` (PerformanceProfiler)
