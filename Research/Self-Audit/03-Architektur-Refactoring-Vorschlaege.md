# 03 — Architektur-Refactoring-Vorschläge

> Konkrete Refactoring-Pläne mit Code-Skeletten, basierend auf den Befunden in
> [`01-Architektur-Befunde.md`](01-Architektur-Befunde.md) und [`02-Code-Qualitaet.md`](02-Code-Qualitaet.md).

---

## Inhaltsverzeichnis

- [03 — Architektur-Refactoring-Vorschläge](#03--architektur-refactoring-vorschläge)
  - [Inhaltsverzeichnis](#inhaltsverzeichnis)
  - [R1 — Zentrale `IRuleRegistry` einführen](#r1--zentrale-iruleregistry-einführen)
    - [Ziel](#ziel)
    - [Skelett](#skelett)
    - [Definitions-Skelett](#definitions-skelett)
    - [Migration der 3 Duplikate](#migration-der-3-duplikate)
    - [Testbarkeit](#testbarkeit)
  - [R2 — Plugin-Pipeline statt Visitor-Dispatcher](#r2--plugin-pipeline-statt-visitor-dispatcher)
    - [Ziel](#ziel-1)
    - [Skelett](#skelett-1)
    - [Beispiel-Refactor](#beispiel-refactor)
    - [LinterEngine-Refactor](#linterengine-refactor)
    - [Discovery via Reflection](#discovery-via-reflection)
    - [Behebt C2.2](#behebt-c22)
  - [R3 — `Program.cs` → Executor-Pattern](#r3--programcs--executor-pattern)
    - [Ziel](#ziel-2)
    - [Skelett](#skelett-2)
    - [Implementierungen](#implementierungen)
    - [Program.cs nach Refactor](#programcs-nach-refactor)
  - [R4 — `PerformanceProfiler` entkoppeln + optional machen](#r4--performanceprofiler-entkoppeln--optional-machen)
    - [Skelett](#skelett-3)
    - [LinterEngine-Refactor](#linterengine-refactor-1)
  - [R5 — `CancellationToken` durch die Pipeline](#r5--cancellationtoken-durch-die-pipeline)
    - [Refactor-Skelett](#refactor-skelett)
    - [Program.cs](#programcs)
  - [R6 — `Apply`-Refactoring: Extension-Method-Pattern](#r6--apply-refactoring-extension-method-pattern)
    - [Skelett](#skelett-4)
  - [R7 — Strukturiertes `ILogger`-Interface](#r7--strukturiertes-ilogger-interface)
    - [Skelett](#skelett-5)
    - [Verwendung](#verwendung)
  - [R8 — Rule-Namen als Const-Klasse (`LinterRuleIds`)](#r8--rule-namen-als-const-klasse-linterruleids)
    - [Skelett](#skelett-6)
    - [Verwendung](#verwendung-1)
  - [R9 — `ArchitectureChecker` aufteilen](#r9--architecturechecker-aufteilen)
    - [Vorschlag](#vorschlag)
  - [R10 — Bug-Fix: `VisitRecordDeclaration` ohne `CollectClassInfo`](#r10--bug-fix-visitrecorddeclaration-ohne-collectclassinfo)
    - [Fix](#fix)
  - [R11 — Quick-Win: `RepoPlaybookGenerator.RuleDescriptions` reparieren](#r11--quick-win-repoplaybookgeneratorruledescriptions-reparieren)
    - [Fix](#fix-1)
  - [R12 — Lösung der Test-Pfad-Inkonsistenz](#r12--lösung-der-test-pfad-inkonsistenz)
    - [Vorgehen](#vorgehen)
  - [R13 — Optional: Mini-DI für Testbarkeit](#r13--optional-mini-di-für-testbarkeit)
    - [Vorschlag (kein DI-Container, sondern "Constructor Injection Lite")](#vorschlag-kein-di-container-sondern-constructor-injection-lite)
  - [Roadmap: Gesamtreihenfolge](#roadmap-gesamtreihenfolge)
    - [Erwartete Resultate](#erwartete-resultate)
  - [🎯 Fazit](#-fazit)

---

## R1 — Zentrale `IRuleRegistry` einführen

**Löst:** F1, A3, C6.1, C9.1, C7.1, C7.3, C11.3
**Aufwand:** M (2–3 Tage)
**Nutzen:** ★★★★★

### Ziel

Alle Regel-Metadaten (Name, Severity, Intent, Description, Cursor-Hint, Default-Wert, Exempt-Suffixes, Fix-Strategy) werden an **einem einzigen Ort** definiert. CursorRulesGenerator, ViolationTextFormatter und RepoPlaybookGenerator lesen aus dieser Registry.

### Skelett

**Neue Datei:** `src/AiNetLinter/Core/RuleRegistry.cs`

```csharp
namespace AiNetLinter.Core;

public sealed record RuleMetadata(
    string RuleId,                       // "EnforceSealedClasses"
    string DisplayName,                  // "Konkrete Klassen muessen 'sealed' sein"
    string ShortDescription,             // 1-Satz-LLM-Output
    string DetailedGuidance,             // mehrteilige Anweisung
    string Intent,                       // "agent-context" | "agent-resilience" | ...
    string Severity,                     // "error" | "warning" | "info"
    string CursorHint,                   // Textbaustein fuer .mdc-Datei
    bool HasAutoFix,                     // wird durch LinterAutoFixer unterstuetzt
    IReadOnlyList<string> ExemptSuffixes // ImmutabilityExemptSuffixes / SealedClassExemptSuffixes / etc.
);

public sealed class RuleRegistry
{
    private readonly Dictionary<string, RuleMetadata> _rules;

    public RuleRegistry(IEnumerable<RuleMetadata> rules)
    {
        _rules = rules.ToDictionary(r => r.RuleId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<RuleMetadata> All => _rules.Values;
    public RuleMetadata Resolve(string ruleId) =>
        _rules.TryGetValue(ruleId, out var r)
            ? r
            : throw new KeyNotFoundException($"Unknown rule: {ruleId}");

    public RuleMetadata? TryResolve(string ruleId) =>
        _rules.GetValueOrDefault(ruleId);

    public IEnumerable<RuleMetadata> ByIntent(string intent) =>
        _rules.Values.Where(r => r.Intent == intent);
}
```

### Definitions-Skelett

**Neue Datei:** `src/AiNetLinter/Core/BuiltInRules.cs`

```csharp
namespace AiNetLinter.Core;

internal static class BuiltInRules
{
    public static readonly IReadOnlyList<RuleMetadata> All = new RuleMetadata[]
    {
        new(
            RuleId: nameof(GlobalConfig.EnforceSealedClasses),
            DisplayName: "Konkrete Klassen muessen 'sealed' sein",
            ShortDescription: "Konkrete Klasse ist nicht 'sealed'.",
            DetailedGuidance: "Fuege 'sealed' zur Klassendeklaration hinzu ...",
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "`sealed` fuer konkrete Klassen; Ausnahmen: Suffixe in `rules.json`.",
            HasAutoFix: true,
            ExemptSuffixes: ["Base", "Foundation", "Host"]
        ),
        // ... 29 weitere Eintraege
    };
}
```

### Migration der 3 Duplikate

| Vorher (Quelle)                           | Nachher                                          |
| ----------------------------------------- | ------------------------------------------------ |
| `CursorRulesGenerator.GlobalRules[]`      | `RuleRegistry.All.Where(r => r.Intent != "...")` |
| `ViolationTextFormatter.RuleInstructions` | `RuleRegistry.Resolve(ruleId).DetailedGuidance`  |
| `RepoPlaybookGenerator.RuleDescriptions`  | `RuleRegistry.Resolve(ruleId).ShortDescription`  |

→ Jede dieser 3 Klassen verliert ihre statische Liste und liest aus der Registry.

### Testbarkeit

```csharp
[Fact]
public void EnforceSealedClasses_HasAutoFix_True()
{
    var registry = new RuleRegistry(BuiltInRules.All);
    var rule = registry.Resolve(nameof(GlobalConfig.EnforceSealedClasses));
    Assert.True(rule.HasAutoFix);
    Assert.Equal("agent-context", rule.Intent);
}
```

---

## R2 — Plugin-Pipeline statt Visitor-Dispatcher

**Löst:** F2, A1, C2.1, C2.2, C2.3, C11
**Aufwand:** M (2–3 Tage, mit Tests)
**Nutzen:** ★★★★★

### Ziel

Checker sind **Instanzen** einer Klasse, die ein Interface implementieren. Sie werden beim Engine-Start aus einer `IReadOnlyList<ICheckRule>` aggregiert. Neue Regeln = neue Klasse, keine Änderung am Dispatcher.

### Skelett

**Neue Datei:** `src/AiNetLinter/Core/ICheckRule.cs`

```csharp
public interface IClassRule
{
    string RuleId { get; }
    bool IsEnabled(LinterConfig config);
    void CheckClass(ClassDeclarationSyntax node, CheckerContext ctx);
    void CheckRecord(RecordDeclarationSyntax node, CheckerContext ctx) { } // default no-op
    void CheckStruct(StructDeclarationSyntax node, CheckerContext ctx) { } // default no-op
}

public interface IMethodRule
{
    string RuleId { get; }
    bool IsEnabled(LinterConfig config);
    void CheckMethod(MethodDeclarationSyntax node, CheckerContext ctx);
}

public interface IInvocationRule
{
    string RuleId { get; }
    bool IsEnabled(LinterConfig config);
    void CheckInvocation(InvocationExpressionSyntax node, CheckerContext ctx);
}

public interface IFileLevelRule
{
    string RuleId { get; }
    bool IsEnabled(LinterConfig config);
    void CheckFile(SyntaxTree tree, CheckerContext ctx);
}
```

### Beispiel-Refactor

**Vorher:** `ArchitectureChecker.CheckSealedClass` (statisch, in Checker.cs)

**Nachher:**

```csharp
internal sealed class EnforceSealedClassesRule : IClassRule
{
    public string RuleId => nameof(GlobalConfig.EnforceSealedClasses);

    public bool IsEnabled(LinterConfig config) =>
        config.Global.EnforceSealedClasses;

    public void CheckClass(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        if (IsSealedOrStaticOrAbstract(node)) return;
        if (IsExempt(node.Identifier.Text, ctx)) return;

        ctx.ReportViolation(this, node,
            $"Die Klasse '{node.Identifier.Text}' ist nicht als 'sealed' deklariert.",
            "Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu, ..."
        );
    }

    private static bool IsSealedOrStaticOrAbstract(ClassDeclarationSyntax node) =>
        node.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword) ||
                               m.IsKind(SyntaxKind.StaticKeyword) ||
                               m.IsKind(SyntaxKind.AbstractKeyword));

    private static bool IsExempt(string className, CheckerContext ctx)
    {
        var suffixes = ctx.Config.Global.SealedClassExemptSuffixes;
        return suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }
}
```

### LinterEngine-Refactor

```csharp
public sealed class LinterEngine
{
    private readonly IReadOnlyList<IClassRule> _classRules;
    private readonly IReadOnlyList<IMethodRule> _methodRules;
    private readonly IReadOnlyList<IInvocationRule> _invocationRules;
    private readonly IReadOnlyList<IFileLevelRule> _fileRules;

    public LinterEngine(
        LinterConfig config,
        IEnumerable<IClassRule> classRules,
        IEnumerable<IMethodRule> methodRules,
        IEnumerable<IInvocationRule> invocationRules,
        IEnumerable<IFileLevelRule> fileRules,
        string? rulesJsonContent = null)
    {
        // ...
        _classRules = classRules.ToList();
        _methodRules = methodRules.ToList();
        _invocationRules = invocationRules.ToList();
        _fileRules = fileRules.ToList();
    }

    private async Task AnalyzeDocumentAsync(...)
    {
        // File-Level-Regeln
        foreach (var rule in _fileRules.Where(r => r.IsEnabled(config)))
            rule.CheckFile(tree, ctx);

        var walker = new RuleDispatchingWalker(
            ctx,
            _classRules.Where(r => r.IsEnabled(config)),
            _methodRules.Where(r => r.IsEnabled(config)),
            _invocationRules.Where(r => r.IsEnabled(config))
        );
        walker.Visit(tree.GetRoot());
    }
}
```

### Discovery via Reflection

```csharp
public static class LinterRules
{
    public static IReadOnlyList<T> Discover<T>(Assembly assembly) where T : class
    {
        return assembly.GetTypes()
            .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .Select(Activator.CreateInstance)
            .Cast<T>()
            .ToList();
    }
}

// Verwendung:
var classRules = LinterRules.Discover<IClassRule>(typeof(Program).Assembly);
```

→ **Neue Regeln hinzufügen = 1 Datei erstellen**. Kein Anfassen des Dispatchers mehr.

### Behebt C2.2

`VisitRecordDeclaration` ruft `CollectClassInfo` nicht auf → **Bug**. Mit Plugin-System registriert man einfach zwei Regeln:

```csharp
public sealed class CollectClassInfoRule : IClassRule
{
    public void CheckClass(ClassDeclarationSyntax node, CheckerContext ctx) { /* ... */ }
    public void CheckRecord(RecordDeclarationSyntax node, CheckerContext ctx) { /* ... */ }
    public void CheckStruct(StructDeclarationSyntax node, CheckerContext ctx) { /* ... */ }
}
```

→ **Bug behoben + zukunftssicher**.

---

## R3 — `Program.cs` → Executor-Pattern

**Löst:** F3, A2, C1
**Aufwand:** M (1–3 Tage)
**Nutzen:** ★★★★

### Ziel

Jeder CLI-Modus (Audit, Sync-Cursor, Debt-Report, Impact, etc.) wird ein `ICommandExecutor`. `ExecuteLinterAsync` wird zu einer einfachen Pipeline.

### Skelett

**Neue Datei:** `src/AiNetLinter/Cli/Commands/ICommandExecutor.cs`

```csharp
public interface ICommandExecutor
{
    string Name { get; }                              // "audit" | "debt-report" | ...
    int Priority { get; }                             // 100 = hoch, 0 = default
    bool CanHandle(LinterArgs args);
    Task<int> ExecuteAsync(LinterArgs args, CancellationToken ct);
}
```

### Implementierungen

```csharp
public sealed class AuditExecutor : ICommandExecutor
{
    public string Name => "audit";
    public int Priority => 0;
    public bool CanHandle(LinterArgs args) =>
        !args.Readme && args.CreateBaselinePath == null && !args.AddDisableAll &&
        !args.RemoveDisableAll && !args.DebtReport && !args.HasImpact &&
        !args.SyncCursorRules && args.Footprint == null;
    public async Task<int> ExecuteAsync(LinterArgs args, CancellationToken ct) { /* ... */ }
}

public sealed class ReadmeExecutor : ICommandExecutor { /* --readme */ }
public sealed class CreateBaselineExecutor : ICommandExecutor { /* --create-baseline */ }
public sealed class AddDisableAllExecutor : ICommandExecutor { /* --add-disable-all */ }
public sealed class DebtReportExecutor : ICommandExecutor { /* --debt-report */ }
public sealed class ImpactExecutor : ICommandExecutor { /* --impact */ }
public sealed class SyncCursorRulesExecutor : ICommandExecutor { /* --sync-cursor-rules */ }
public sealed class FootprintExecutor : ICommandExecutor { /* --footprint */ }
```

### Program.cs nach Refactor

```csharp
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var (root, options) = CliCommandBuilder.Build();
        var linterArgs = ToLinterArgs(CliCommandBuilder.Parse(root.Parse(args), options));

        var executors = ServiceFactory.CreateExecutors(linterArgs); // DI-Lite
        var matched = executors
            .Where(e => e.CanHandle(linterArgs))
            .OrderByDescending(e => e.Priority)
            .ToList();

        if (matched.Count == 0)
        {
            Console.Error.WriteLine("[ERROR]: Kein Modus ausgewaehlt.");
            return 1;
        }

        return await matched[0].ExecuteAsync(linterArgs, CancellationToken.None);
    }
}
```

→ **Testbar**: jeder Executor einzeln testbar via `ICommandExecutor` Mock.

---

## R4 — `PerformanceProfiler` entkoppeln + optional machen

**Löst:** F4, A4, C5
**Aufwand:** S
**Nutzen:** ★★★★

### Skelett

```csharp
public interface IPerformanceProfiler
{
    bool IsEnabled { get; }
    IDisposable BeginPhase(string phaseName);
    void RecordDocumentAnalysis(string filePath, double durationMs, int violationsCount);
    void WriteReport(string targetPath, string? solutionFilePath);
}

public sealed class NullPerformanceProfiler : IPerformanceProfiler
{
    public bool IsEnabled => false;
    public IDisposable BeginPhase(string phaseName) => new NoOpDisposable();
    public void RecordDocumentAnalysis(string filePath, double durationMs, int violationsCount) { }
    public void WriteReport(string targetPath, string? solutionFilePath) { }
    private sealed class NoOpDisposable : IDisposable { public void Dispose() { } }
}

public sealed class PerformanceProfiler : IPerformanceProfiler
{
    // bestehende Implementierung, refactored:
    // - Konstruktor statt Singleton
    // - Output-Pfad konfigurierbar
    // - Limits für _documentEntries (z.B. Top-N)
}
```

### LinterEngine-Refactor

```csharp
public sealed class LinterEngine
{
    private readonly IPerformanceProfiler _profiler;

    public LinterEngine(
        LinterConfig config,
        IPerformanceProfiler profiler,
        string? rulesJsonContent = null)
    {
        _config = config;
        _profiler = profiler;
        _rulesJsonContent = rulesJsonContent;
    }

    private async Task AnalyzeDocumentAsync(...)
    {
        using (_profiler.BeginPhase("DocumentAnalysis"))
        {
            // ...
        }
    }
}
```

→ Tests nutzen `NullPerformanceProfiler`, Produktion den `PerformanceProfiler`.

---

## R5 — `CancellationToken` durch die Pipeline

**Löst:** F5, A7, C1.4
**Aufwand:** N
**Nutzen:** ★★★★

### Refactor-Skelett

```csharp
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(
    SourceFileCatalog catalog,
    bool noCache = false,
    int cacheTtlMinutes = 60,
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    var cache = noCache ? null : BuildCache(catalog, ...);
    return await RunInternalAsync(catalog.Solution, catalog, cache, cancellationToken);
}

private async Task AnalyzeSolutionAsync(AnalysisState state, CancellationToken ct)
{
    var workItems = await ResolveWorkItemsAsync(state.Solution, ..., ct);
    await Parallel.ForEachAsync(
        workItems,
        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
        async (item, token) => await AnalyzeWorkItemAsync(item, state, cache, token)
    );
}
```

### Program.cs

```csharp
public static async Task<int> Main(string[] args)
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    return await root.Parse(args).InvokeAsync(cts.Token);
}
```

---

## R6 — `Apply`-Refactoring: Extension-Method-Pattern

**Löst:** F6, A6, C4.2
**Aufwand:** N
**Nutzen:** ★★★

### Skelett

```csharp
// Vorher: 5 with-Klonings pro Aufruf
public GlobalConfig Apply(GlobalConfigOverride? o)
{
    return ApplyStructuralRules(o)
        .ApplyNamingAndStyleRules(o)
        .ApplyCatchRules(o)
        .ApplyImmutabilityRules(o)
        .ApplyNamespaceAndAnalysisRules(o);
}

// Nachher: 1 with-Kloning + Helper-Methoden
public static class GlobalConfigExtensions
{
    public static GlobalConfig WithOverrides(this GlobalConfig self, GlobalConfigOverride? o)
    {
        if (o == null) return self;
        return self with
        {
            EnforceSealedClasses = o.EnforceSealedClasses ?? self.EnforceSealedClasses,
            AllowUnsealedPartialClasses = o.AllowUnsealedPartialClasses ?? self.AllowUnsealedPartialClasses,
            // ... alle 30 Felder in EINER with-Klausel
        };
    }
}
```

→ **1 Kloning** statt 5, klarer Code.

---

## R7 — Strukturiertes `ILogger`-Interface

**Löst:** F7, A8, C1.3, C9.3, C10.3
**Aufwand:** M
**Nutzen:** ★★★★

### Skelett

```csharp
public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Info(string format, params object[] args);
}

public sealed class ConsoleLogger : ILogger
{
    public void Info(string message) => Console.WriteLine($"[INFO]: {message}");
    public void Warn(string message) => Console.Error.WriteLine($"[WARN]: {message}");
    public void Error(string message) => Console.Error.WriteLine($"[ERROR]: {message}");
    public void Info(string format, params object[] args) => Console.WriteLine($"[INFO]: {string.Format(format, args)}");
}

public sealed class TestLogger : ILogger
{
    public List<(string Level, string Message)> Entries { get; } = new();
    public void Info(string message) => Entries.Add(("INFO", message));
    public void Warn(string message) => Entries.Add(("WARN", message));
    public void Error(string message) => Entries.Add(("ERROR", message));
    public void Info(string format, params object[] args) => Entries.Add(("INFO", string.Format(format, args)));
}
```

### Verwendung

```csharp
// Vorher:
Console.Error.WriteLine($"[WARN]: Workspace-Diagnose: {msg}");

// Nachher:
_logger.Warn($"Workspace-Diagnose: {msg}");
```

→ Tests können Logger-Inhalt prüfen statt `Console.SetOut` zu mocken.

---

## R8 — Rule-Namen als Const-Klasse (`LinterRuleIds`)

**Löst:** F8, A10, C9.2
**Aufwand:** N
**Nutzen:** ★★★

### Skelett

```csharp
public static class LinterRuleIds
{
    public const string EnforceSealedClasses = nameof(GlobalConfig.EnforceSealedClasses);
    public const string EnforceNoSilentCatch = nameof(GlobalConfig.EnforceNoSilentCatch);
    public const string MaxLineCount = nameof(MetricsConfig.MaxLineCount);
    public const string MaxMethodParameterCount = nameof(MetricsConfig.MaxMethodParameterCount);
    // ... alle Regel-Namen
}
```

### Verwendung

```csharp
// Vorher:
RuleName = nameof(ctx.Config.Global.EnforceSealedClasses)
RuleName = "EnforceSealedClasses"  // inkonsistent

// Nachher:
RuleName = LinterRuleIds.EnforceSealedClasses
```

→ Compile-Time-Sicherheit; Refactoring von Property-Namen propagiert automatisch.

---

## R9 — `ArchitectureChecker` aufteilen

**Löst:** C11.1
**Aufwand:** S
**Nutzen:** ★★★

### Vorschlag

`ArchitectureChecker` (303 LOC, 18 Methoden) sollte aufgeteilt werden in:

| Neue Klasse                  | Verantwortlichkeiten                                                           | LOC ca. |
| ---------------------------- | ------------------------------------------------------------------------------ | ------- |
| `SealedClassRule`            | `CheckSealedClass`, `IsSealedOrStaticOrAbstract`, `HasExemptSuffix`            | 50      |
| `ValueObjectRule`            | `CheckValueObjectContract`, `IsStructOrReadOnly`                               | 40      |
| `NamespaceCouplingRule`      | `CheckForbiddenNamespace`, `CheckForbiddenSymbolNamespace`, `NamespaceMatches` | 80      |
| `PhantomDependencyRule`      | `CheckPhantomNamespace`, `CheckPhantomReflection`, `IsForbiddenReflectionCall` | 60      |
| `DynamicTypeRule`            | `CheckDynamic`                                                                 | 20      |
| `GeneratedCodeDetector`      | `IsGeneratedCode`                                                              | 15      |
| `InheritanceDepthCalculator` | `GetInheritanceDepth`, `IsFrameworkBaseType`                                   | 40      |
| `TestAttributeDetector`      | `CheckForTestMethods`, `IsTestAttribute`                                       | 25      |
| `ClassInfoCollector`         | `CollectClassInfo`, `GetBaseTypeNames`                                         | 60      |

→ Wenn zusätzlich R2 (Plugin-Pipeline) umgesetzt ist, sind das direkte Implementierungen von `IClassRule` etc.

---

## R10 — Bug-Fix: `VisitRecordDeclaration` ohne `CollectClassInfo`

**Löst:** C2.2
**Aufwand:** XS (< 1 Stunde)
**Nutzen:** ★★★ (Bug-Fix)

### Fix

```csharp
// LinterAnalyzer.cs, Zeile 96 ff.
public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
{
    // ... bestehende Checks ...

    // BUG-FIX: CollectClassInfo fehlt(e) fuer Records
    ArchitectureChecker.CollectClassInfo(node, _ctx);

    base.VisitRecordDeclaration(node);
}
```

Und gleichermaßen für `VisitStructDeclaration` (Z. 110).

Wenn R2 umgesetzt ist, ist dieser Bug **automatisch behoben**, weil die Rule für ClassInfo via `CheckRecord`/`CheckStruct` registriert wird.

---

## R11 — Quick-Win: `RepoPlaybookGenerator.RuleDescriptions` reparieren

**Löst:** F10, C6.1
**Aufwand:** XS
**Nutzen:** ★★★

### Fix

**Vorher:** `RepoPlaybookGenerator.cs:43-45`

```csharp
["MaxMethodLineCount"] = "Methode hat zu viele Codezeilen (max. 42 Zeilen).",
["MaxCyclomaticComplexity"] = "Zu hohe zyklomatische Komplexitaet (max. 5).",
["MaxCognitiveComplexity"] = "Zu hohe kognitive Komplexitaet (max. 5).",
```

**Nachher:**

```csharp
private static string FormatLimitDescription(LinterConfig config, string prefix)
{
    return prefix switch
    {
        "MaxLineCount" => $"Dateizeilenlimit (max. {config.Metrics.MaxLineCount} Zeilen) ueberschritten.",
        "MaxMethodLineCount" => $"Methode hat zu viele Codezeilen (max. {config.Metrics.MaxMethodLineCount}).",
        "MaxCyclomaticComplexity" => $"Zu hohe zyklomatische Komplexitaet (max. {config.Metrics.MaxCyclomaticComplexity}).",
        "MaxCognitiveComplexity" => $"Zu hohe kognitive Komplexitaet (max. {config.Metrics.MaxCognitiveComplexity}).",
        "MaxMethodOverloads" => $"Zu viele Methodenueberladungen (max. {config.Metrics.MaxMethodOverloads}).",
        "MaxConstructorDependencies" => $"Zu viele Konstruktorabhaengigkeiten (max. {config.Metrics.MaxConstructorDependencies}).",
        _ => $"Regel '{prefix}'."
    };
}
```

**Oder besser (mit R1):** komplett aus `RuleRegistry` lesen.

---

## R12 — Lösung der Test-Pfad-Inkonsistenz

**Löst:** A11, F9
**Aufwand:** XS
**Nutzen:** ★★

### Vorgehen

1. Verschiebe `src/AiNetLinter.Tests/*` → `tests/AiNetLinter.Tests/*`
2. Aktualisiere `.slnx`:
   ```xml
   <Project Path="tests/AiNetLinter.Tests/AiNetLinter.Tests.csproj" />
   ```
3. Aktualisiere CI-Skripte
4. Aktualisiere `Docs/ROADMAP.md` (Pfad-Erwähnungen)

---

## R13 — Optional: Mini-DI für Testbarkeit

**Löst:** Test-Isolation-Probleme in F4, F7
**Aufwand:** M
**Nutzen:** ★★★

### Vorschlag (kein DI-Container, sondern "Constructor Injection Lite")

```csharp
public static class LinterServices
{
    public static (LinterEngine Engine, IPerformanceProfiler Profiler, ILogger Logger) Create(
        LinterConfig config,
        string? rulesJsonContent = null,
        ILogger? logger = null,
        IPerformanceProfiler? profiler = null)
    {
        logger ??= new ConsoleLogger();
        profiler ??= new NullPerformanceProfiler();

        var ruleRegistry = new RuleRegistry(BuiltInRules.All);
        var classRules = LinterRules.Discover<IClassRule>(typeof(Program).Assembly);
        // ...

        var engine = new LinterEngine(
            config, ruleRegistry, classRules, methodRules,
            invocationRules, fileRules, profiler, logger, rulesJsonContent);

        return (engine, profiler, logger);
    }
}
```

**Kein DI-Container** — explizite Konstruktor-Injektion wie im bestehenden `LinterEngine`-Pattern (das bereits 2 Konstruktor-Parameter hat).

→ **Bleibt konsistent** mit der bestehenden Architektur (statisch, ohne Container).

---

## Roadmap: Gesamtreihenfolge

| Woche | Tasks                                                            | Zustand       |
| ----- | ---------------------------------------------------------------- | ------------- |
| **1** | R10 (Bug-Fix), R11 (Quick-Win), R8 (Rule-IDs), R12 (Test-Pfad)   | Quick Wins    |
| **2** | R1 (RuleRegistry) — Fundament für alles weitere                  |               |
| **3** | R2 (Plugin-Pipeline) — größte Hebelwirkung; C2.2-Fix inklusive   |               |
| **4** | R3 (Executor-Pattern), R5 (Cancellation), R4 (Profiler raus)     |               |
| **5** | R7 (ILogger), R6 (Apply-Ext), R9 (ArchitectureChecker aufteilen) |               |
| **6** | R13 (Mini-DI), Coverage-Setup, Dogfooding-Tests                  | Finalisierung |

### Erwartete Resultate

| Metrik                         | Vor Refactoring        | Nach Refactoring                        |
| ------------------------------ | ---------------------- | --------------------------------------- |
| Zeit für neue Regel hinzufügen | 2–4 Stunden            | **30 Min** (neue Klasse + Registration) |
| `LinterAnalyzer.cs` LOC        | 271                    | ~30 (nur Walker-Orchestrierung)         |
| `Program.cs` LOC               | 568                    | ~30 (nur `Main` + ServiceFactory)       |
| `Console.WriteLine`-Vorkommen  | 25+                    | 0 (in Produktion)                       |
| Test-Isolation                 | ❌ Singleton blockiert | ✅ NullPerformanceProfiler              |
| Agenten-Lookup von Regeln      | ❌ 3 Stellen           | ✅ Eine Registry                        |
| Coverage-Reporting             | ❌                     | ✅ coverlet + Threshold                 |

---

## 🎯 Fazit

**AiNetLinter ist ein solides Werkzeug**, aber die Architektur muss mit dem Feature-Umfang mitwachsen. Die vorgeschlagenen Refactorings sind:

1. **R1 + R2** sind die **Hauptinvestition** (je 2–3 Tage, größter Hebel)
2. **R3, R4, R5, R7** sind **Architektur-Politur** (je 0.5–2 Tage, hoher Qualitätsgewinn)
3. **R6, R8, R9, R10, R11, R12** sind **Quick Wins** (je < 1h, akkumulierter Wert hoch)

→ Nach 4–6 Wochen konsequenter Refactoring-Arbeit ist das Projekt bereit für **Epic 25+** und die nächste Stufe der Agent-Integration (MCP-Server, IDE-Plugins).
