# 03 — Architektur-Refactoring-Vorschläge

> Konkrete Refactoring-Pläne mit Code-Skeletten, basierend auf den Befunden in
> [`01-Architektur-Befunde.md`](01-Architektur-Befunde.md) und [`02-Code-Qualitaet.md`](02-Code-Qualitaet.md).
>
> **Bindende Constraints:** Kein Plugin-System (Verdrahtung bleibt explizit), kein DI-Container (Konstruktor-Parameter statt Framework), statische Kompilierung, monolithisches CLI-Tool.

---

## Inhaltsverzeichnis

- [R1 — Statische `RuleRegistry`-Klasse einführen](#r1--statische-ruleregistry-klasse-einführen)
- [R2 — Checker-God-Klassen aufteilen (statisch, explizit)](#r2--checker-god-klassen-aufteilen-statisch-explizit)
- [R3 — `Program.cs` in statische Command-Klassen aufteilen](#r3--programcs-in-statische-command-klassen-aufteilen)
- [R4 — `PerformanceProfiler` entkoppeln + optional machen](#r4--performanceprofiler-entkoppeln--optional-machen)
- [R5 — `CancellationToken` durch die Pipeline](#r5--cancellationtoken-durch-die-pipeline)
- [R6 — `Apply`-Refactoring: ein `with`-Block](#r6--apply-refactoring-ein-with-block)
- [R7 — `ILintConsole`-Interface statt `Console.WriteLine`](#r7--ilintconsole-interface-statt-consolewriteline)
- [R8 — Rule-Namen als Const-Klasse (`LinterRuleIds`)](#r8--rule-namen-als-const-klasse-linterruleids)
- [R9 — `ctx.ReportViolation`-Helper](#r9--ctxreportviolation-helper)
- [R10 — Bug-Fix: `VisitRecordDeclaration` ohne `CollectClassInfo`](#r10--bug-fix-visitrecorddeclaration-ohne-collectclassinfo)
- [R11 — Quick-Win: `RepoPlaybookGenerator.RuleDescriptions` reparieren](#r11--quick-win-repoplaybookgeneratorruledescriptions-reparieren)
- [Roadmap: Gesamtreihenfolge](#roadmap-gesamtreihenfolge)

---

## R1 — Statische `RuleRegistry`-Klasse einführen

**Löst:** F1, A3, C7.1, C7.2, C9.1, C11.3  
**Aufwand:** M (2–3 Tage)  
**Nutzen:** ★★★★★

### Problem

Regel-Metadaten (Name, Beschreibung, Intent, Guidance, Grenzwerte) sind an **drei Stellen** definiert, jeweils leicht unterschiedlich und teils mit **falschen Werten**:

- `CursorRulesGenerator.GlobalRules[]` — für `.mdc`-Dateigenerierung
- `ViolationTextFormatter.RuleInstructions` — für LLM-Output
- `RepoPlaybookGenerator.RuleDescriptions` — für Playbook-Markdown

Wenn eine Regel neu hinzukommt oder sich ändert, müssen alle drei Stellen manuell aktualisiert werden — und das passiert offensichtlich nicht zuverlässig (7 falsche Werte, → F9).

### Lösungsansatz

Eine statische Klasse `RuleRegistry` als Single-Source-of-Truth. Kein Interface (nicht nötig ohne DI-Container), keine Reflection-Discovery — einfach eine statische readonly Liste.

**Neue Datei:** `src/AiNetLinter/Core/RuleRegistry.cs`

```csharp
namespace AiNetLinter.Core;

public sealed record RuleMetadata(
    string RuleId,              // "EnforceSealedClasses"
    string DisplayName,         // "Konkrete Klassen muessen 'sealed' sein"
    string ShortDescription,    // 1-Satz für Playbook
    string DetailedGuidance,    // mehrteilige LLM-Anweisung
    string Intent,              // "agent-context" | "agent-resilience" | ...
    string Severity,            // "error" | "warning" | "info"
    string CursorHint,          // Textbaustein fuer .mdc-Datei
    bool HasAutoFix
);

internal static class RuleRegistry
{
    public static readonly IReadOnlyList<RuleMetadata> All = BuildAll();

    public static RuleMetadata Resolve(string ruleId) =>
        TryResolve(ruleId) ?? throw new KeyNotFoundException($"Unknown rule: {ruleId}");

    public static RuleMetadata? TryResolve(string ruleId) =>
        All.FirstOrDefault(r => r.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<RuleMetadata> ByIntent(string intent) =>
        All.Where(r => r.Intent == intent);

    private static IReadOnlyList<RuleMetadata> BuildAll() =>
    [
        new(
            RuleId: nameof(GlobalConfig.EnforceSealedClasses),
            DisplayName: "Konkrete Klassen muessen 'sealed' sein",
            ShortDescription: "Konkrete Klasse ist nicht 'sealed'.",
            DetailedGuidance: "Fuege 'sealed' zur Klassendeklaration hinzu ...",
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "`sealed` fuer konkrete Klassen; Ausnahmen: Suffixe in `rules.json`.",
            HasAutoFix: true
        ),
        // ... alle weiteren Regeln
    ];
}
```

### Migration der 3 Duplikate

| Vorher (Quelle)                           | Nachher                                         |
| ----------------------------------------- | ----------------------------------------------- |
| `CursorRulesGenerator.GlobalRules[]`      | `RuleRegistry.All` (gefiltert nach Intent)      |
| `ViolationTextFormatter.RuleInstructions` | `RuleRegistry.Resolve(ruleId).DetailedGuidance` |
| `RepoPlaybookGenerator.RuleDescriptions`  | `RuleRegistry.Resolve(ruleId).ShortDescription` |

Jede der drei Klassen verliert ihre statische Liste und liest aus der Registry.

### Grenzwerte: konfigurationsbasiert

Für Beschreibungen mit Grenzwerten (MaxLineCount, MaxCyclomaticComplexity etc.) wird die aktuelle `LinterConfig` übergeben:

```csharp
// RepoPlaybookGenerator.cs nach Refactoring:
private static string FormatRuleDescription(RuleMetadata rule, LinterConfig config)
{
    // Statische Beschreibung aus Registry, Grenzwert dynamisch aus Config:
    return rule.RuleId switch
    {
        nameof(MetricsConfig.MaxLineCount) =>
            $"Dateizeilenlimit (max. {config.Metrics.MaxLineCount} Zeilen) ueberschritten.",
        nameof(MetricsConfig.MaxCyclomaticComplexity) =>
            $"Zu hohe zyklomatische Komplexitaet (max. {config.Metrics.MaxCyclomaticComplexity}).",
        _ => rule.ShortDescription
    };
}
```

### Testbarkeit

```csharp
[Fact]
public void AllRules_HaveNonEmptyGuidance()
{
    foreach (var rule in RuleRegistry.All)
    {
        Assert.NotEmpty(rule.DetailedGuidance);
        Assert.NotEmpty(rule.ShortDescription);
    }
}
```

---

## R2 — Checker-God-Klassen aufteilen (statisch, explizit)

**Löst:** F2, A1, C2.1, C11.1, C12.1  
**Aufwand:** M (2–3 Tage, inkl. Tests)  
**Nutzen:** ★★★★★

### Problem

`ArchitectureChecker.cs` (303 LOC, 18 Methoden) sammelt völlig unzusammenhängende Prüfungen in einer Klasse. Tests für `CheckSealedClass` müssen den gesamten `LinterAnalyzer` hochfahren, statt die Methode direkt aufzurufen. Das gleiche gilt für andere große Checker-Klassen.

**Zusatzbug:** `VisitRecordDeclaration` ruft kein `CollectClassInfo` auf — ein direktes Symptom der unstrukturierten Verdrahtung in `LinterAnalyzer`.

### Lösungsansatz

**Kein Plugin-System, keine Reflection.** Die Verdrahtung in `LinterAnalyzer` bleibt vollständig explizit — neue Checker müssen dort manuell eingetragen werden. Das Ziel ist ausschließlich: jede Checker-Klasse hat eine klare Verantwortlichkeit und ist direkt testbar.

**Neue Struktur:**

| Neue Datei                           | Methoden aus `ArchitectureChecker`                             |
| ------------------------------------ | -------------------------------------------------------------- |
| `Checkers/SealedClassChecker.cs`     | `CheckSealedClass`, `IsSealedOrStaticOrAbstract`, `HasExemptSuffix` |
| `Checkers/ValueObjectChecker.cs`     | `CheckValueObjectContract`, `IsStructOrReadOnly`               |
| `Checkers/NamespaceCouplingChecker.cs` | `CheckForbiddenNamespace`, `CheckForbiddenSymbolNamespace`, `NamespaceMatches` |
| `Checkers/PhantomDependencyChecker.cs` | `CheckPhantomNamespace`, `CheckPhantomReflection`, `IsForbiddenReflectionCall` |
| `Checkers/DynamicTypeChecker.cs`     | `CheckDynamic`                                                  |
| `Checkers/ClassInfoCollector.cs`     | `CollectClassInfo`, `GetBaseTypeNames`                         |
| `Checkers/GeneratedCodeDetector.cs`  | `IsGeneratedCode`                                               |
| `Checkers/InheritanceDepthChecker.cs`| `GetInheritanceDepth`, `IsFrameworkBaseType`                   |
| `Checkers/TestAttributeDetector.cs`  | `CheckForTestMethods`, `IsTestAttribute`                       |

Alle bleiben `internal static class` — konsistent mit dem bestehenden Pattern.

### Beispiel: SealedClassChecker.cs

```csharp
// src/AiNetLinter/Core/Checkers/SealedClassChecker.cs
namespace AiNetLinter.Core.Checkers;

internal static class SealedClassChecker
{
    internal static void Check(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceSealedClasses) return;
        if (IsSealedOrStaticOrAbstract(node)) return;
        if (HasExemptSuffix(node.Identifier.Text, ctx.Config.Global.SealedClassExemptSuffixes)) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = nameof(ctx.Config.Global.EnforceSealedClasses),
            Details = $"Die Klasse '{node.Identifier.Text}' ist nicht als 'sealed' deklariert.",
            Guidance = "Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu."
        });
    }

    private static bool IsSealedOrStaticOrAbstract(ClassDeclarationSyntax node) =>
        node.Modifiers.Any(m =>
            m.IsKind(SyntaxKind.SealedKeyword) ||
            m.IsKind(SyntaxKind.StaticKeyword) ||
            m.IsKind(SyntaxKind.AbstractKeyword));

    private static bool HasExemptSuffix(string name, IReadOnlyList<string> suffixes) =>
        suffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));
}
```

### LinterAnalyzer nach Refactoring

Die `VisitClassDeclaration`-Methode ändert sich kaum — nur die Klassen-Namen der Aufrufe werden spezifischer:

```csharp
public override void VisitClassDeclaration(ClassDeclarationSyntax node)
{
    if (GeneratedCodeDetector.IsGenerated(node, _ctx)) return;
    NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Klasse", _ctx);
    NamingChecker.CheckPascalCase(node.Identifier, "Klasse", _ctx);
    SealedClassChecker.Check(node, _ctx);          // war: ArchitectureChecker.CheckSealedClass
    ValueObjectChecker.Check(node, _ctx);           // war: ArchitectureChecker.CheckValueObjectContract
    ScopeChecker.CheckMethodOverloads(node, _ctx);
    StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
    ImmutabilityChecker.CheckClass(node, _ctx);
    WpfSeparationChecker.Check(node, _ctx);
    NestedTypesChecker.Check(node, _ctx);
    PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
    ClassInfoCollector.Collect(node, _ctx);         // war: ArchitectureChecker.CollectClassInfo
    base.VisitClassDeclaration(node);
}
```

Der `LinterAnalyzer` bleibt der **explizite Dispatcher** — jede neue Regel muss dort händisch eingetragen werden.

### Testbarkeit nach Refactoring

```csharp
[Fact]
public void SealedClassChecker_Reports_NonSealedConcreteClass()
{
    var (tree, model) = TestHelper.ParseCode("public class Foo { }");
    var ctx = TestHelper.CreateContext(enableSealedClasses: true);
    var node = tree.GetRoot().DescendantsOfType<ClassDeclarationSyntax>().First();

    SealedClassChecker.Check(node, ctx);

    Assert.Single(ctx.Violations);
}
```

Kein Hochfahren des gesamten Analyzers nötig.

---

## R3 — `Program.cs` in statische Command-Klassen aufteilen

**Löst:** F3, A2, C1.2  
**Aufwand:** M (1–3 Tage)  
**Nutzen:** ★★★★

### Problem

`Program.cs` hat 568 LOC weil die Implementierungslogik aller 8 Sub-Befehle direkt dort liegt. Die if-Kaskade selbst ist kein Problem — sie ist der richtige explizite Router. Das Problem: die eigentlichen Methoden (`RunAuditAsync`, `RunSyncCursorRules`, etc.) belegen hunderte Zeilen in derselben Datei.

### Lösungsansatz

**Kein Interface, keine Discovery.** Die if-Kaskade in `ExecuteLinterAsync` bleibt identisch. Jeder Zweig ruft statt einer lokalen Methode eine externe Command-Klasse auf.

**Neue Dateistruktur:** `src/AiNetLinter/Commands/`

```
Commands/
  AuditCommand.cs
  DebtReportCommand.cs
  FootprintCommand.cs
  ImpactCommand.cs
  MaintenanceCommand.cs
  PlaybookCheckCommand.cs
  ReadmeCommand.cs
  SyncCursorRulesCommand.cs
```

Jede Klasse:

```csharp
// Commands/AuditCommand.cs
namespace AiNetLinter.Commands;

internal static class AuditCommand
{
    internal static async Task<int> RunAsync(LinterArgs args, ILintConsole console)
    {
        // Logik aus RunAuditAsync + RunAuditWithBaselineAsync + AuditWithBaselineAsync
    }
}
```

### Program.cs nach Refactoring

```csharp
private static async Task<int> ExecuteLinterAsync(LinterArgs args, ILintConsole console)
{
    if (args.Readme) return ReadmeCommand.Run(console);
    var validationError = ValidateArgs(args);
    if (validationError.HasValue) return validationError.Value;
    if (args.Check && args.PlaybookPath != null) return await PlaybookCheckCommand.RunAsync(args, console);
    if (args.SyncCursorRules && args.PlaybookPath == null) return SyncCursorRulesCommand.Run(args, console);
    if (args.Footprint != null) return await FootprintCommand.RunAsync(args, console);
    var maintenanceResult = await MaintenanceCommand.TryRunAsync(args, console);
    if (maintenanceResult.HasValue) return maintenanceResult.Value;
    if (args.DebtReport) return await DebtReportCommand.RunAsync(args, console);
    if (args.HasImpact) return await ImpactCommand.RunAsync(args, console);
    return await AuditCommand.RunAsync(args, console);
}
```

`Program.cs` schrumpft auf ~60 LOC: nur `Main`, `ExecuteLinterAsync`, `ToLinterArgs`, `ValidateArgs`.

### Testbarkeit

Jede Command-Klasse kann direkt getestet werden, mit einem `TestLintConsole` (→ R7) statt Console.SetOut-Mocking.

---

## R4 — `PerformanceProfiler` entkoppeln + optional machen

**Löst:** F4, A4, C5.1, C5.2, C5.3  
**Aufwand:** S (< 1 Tag)  
**Nutzen:** ★★★★

### Problem

`PerformanceProfiler.Instance` ist ein globaler Singleton, der in `LinterEngine.AnalyzeDocumentAsync` direkt aufgerufen wird. Tests können ihn nicht ersetzen → ungewollte Disk-IO in jedem Test-Run, wenn Profiling aktiv ist.

### Lösungsansatz

Interface + Null-Implementierung, übergeben via Konstruktor-Parameter (kein DI-Container — explizite Verdrahtung in `Program.cs`).

```csharp
// src/AiNetLinter/Diagnostics/IPerformanceProfiler.cs
public interface IPerformanceProfiler
{
    bool IsEnabled { get; }
    IDisposable BeginPhase(string phaseName);
    void RecordDocumentAnalysis(string filePath, double durationMs, int violationsCount);
    void WriteReport(string targetPath, string? solutionFilePath);
}

// NullPerformanceProfiler — für Tests und den normalen Lauf ohne --profile
public sealed class NullPerformanceProfiler : IPerformanceProfiler
{
    public bool IsEnabled => false;
    public IDisposable BeginPhase(string phaseName) => NoOpDisposable.Instance;
    public void RecordDocumentAnalysis(string filePath, double durationMs, int violationsCount) { }
    public void WriteReport(string targetPath, string? solutionFilePath) { }

    private sealed class NoOpDisposable : IDisposable
    {
        internal static readonly NoOpDisposable Instance = new();
        public void Dispose() { }
    }
}

// PerformanceProfiler — bestehende Implementierung, Singleton entfernt, Konstruktor bleibt
public sealed class PerformanceProfiler : IPerformanceProfiler
{
    public PerformanceProfiler() { }
    // ... bestehende Implementierung, _initialized thread-safe per lock { }
}
```

### LinterEngine nach Refactoring

```csharp
public sealed class LinterEngine
{
    private readonly IPerformanceProfiler _profiler;

    public LinterEngine(LinterConfig config, IPerformanceProfiler profiler, string? rulesJsonContent = null)
    {
        _config = config;
        _profiler = profiler;
    }

    private async Task AnalyzeDocumentAsync(...)
    {
        using (_profiler.BeginPhase("DocumentAnalysis"))
        {
            // ... Analyse
            _profiler.RecordDocumentAnalysis(relativePath, elapsed, violations.Count);
        }
    }
}
```

### Verdrahtung in Program.cs

```csharp
// Program.cs — explizit, kein Framework:
IPerformanceProfiler profiler = args.Profile
    ? new PerformanceProfiler()
    : new NullPerformanceProfiler();

var engine = new LinterEngine(config, profiler, rulesJsonContent);
```

---

## R5 — `CancellationToken` durch die Pipeline

**Löst:** F5, A7, C1.4  
**Aufwand:** S (Routine-Refactoring)  
**Nutzen:** ★★★★

### Problem

Keine `async`-Methode in der Pipeline akzeptiert ein `CancellationToken`. Ctrl+C wird nicht kooperativ behandelt — der Lint-Run läuft bis zum natürlichen Ende durch.

### Lösungsansatz

`CancellationToken ct = default` als letzten Parameter zu allen async-Methoden hinzufügen. Konsistenz: `ct` immer weiterreichen, nie ignorieren.

```csharp
// LinterEngine.cs
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(
    SourceFileCatalog catalog,
    bool noCache = false,
    int cacheTtlMinutes = 60,
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ...
    return await RunInternalAsync(catalog.Solution, catalog, cache, cancellationToken);
}

private async Task AnalyzeSolutionAsync(AnalysisState state, CancellationToken ct)
{
    await Parallel.ForEachAsync(
        workItems,
        new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        },
        async (item, token) => await AnalyzeWorkItemAsync(item, state, cache, token)
    );
}
```

### Main.cs

```csharp
public static async Task<int> Main(string[] args)
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    return await ExecuteLinterAsync(ToLinterArgs(parsed), cts.Token);
}
```

---

## R6 — `Apply`-Refactoring: ein `with`-Block

**Löst:** F6, A6, C4.2  
**Aufwand:** S (< 1 Tag)  
**Nutzen:** ★★★

### Problem

`GlobalConfig.Apply()` erzeugt 5 Zwischen-Records durch 5 verkettete `with`-Klauseln. Lesbarkeit als Begründung, aber 5 Heap-Allokationen pro Override-Anwendung sind unnötig.

### Lösungsansatz

Alle Properties in **einem einzigen `with { }` Block** zusammenfassen. Lesbarkeits-Strukturierung über Zeilengruppen mit Leerzeilen statt Methoden:

```csharp
public GlobalConfig Apply(GlobalConfigOverride? o)
{
    if (o == null) return this;
    return this with
    {
        // Strukturregeln
        EnforceSealedClasses          = o.EnforceSealedClasses          ?? EnforceSealedClasses,
        AllowUnsealedPartialClasses   = o.AllowUnsealedPartialClasses   ?? AllowUnsealedPartialClasses,
        BanPublicNestedTypes          = o.BanPublicNestedTypes          ?? BanPublicNestedTypes,

        // Naming und Stil
        EnforcePascalCaseNames        = o.EnforcePascalCaseNames        ?? EnforcePascalCaseNames,
        EnforceXmlDocumentation       = o.EnforceXmlDocumentation       ?? EnforceXmlDocumentation,

        // Catch-Regeln
        EnforceNoSilentCatch          = o.EnforceNoSilentCatch          ?? EnforceNoSilentCatch,
        // ... alle weiteren Properties in einer with-Klausel
    };
}
```

**1 Kloning** statt 5. Gleiches Muster für `MetricsConfig.Apply`.

---

## R7 — `ILintConsole`-Interface statt `Console.WriteLine`

**Löst:** F7, A8, C1.3, C9.3, C10.2  
**Aufwand:** M (1–2 Tage, alle 25+ Stellen anpassen)  
**Nutzen:** ★★★★

### Problem

25+ `Console.WriteLine`/`Console.Error.WriteLine`-Aufrufe in Produktionsklassen. Tests müssen `Console.SetOut` mocken — aufwändig und fragil. `ConsoleTestCollector.cs` im Test-Projekt ist ein direktes Symptom.

**Wichtig:** Das Interface heißt **`ILintConsole`** (nicht `ILogger`) — `Microsoft.Extensions.Logging.ILogger` ist bereits ein bekanntes Framework-Interface, ein gleichnamiges eigenes Interface wäre verwirrend.

### Skelett

```csharp
// src/AiNetLinter/Output/ILintConsole.cs
public interface ILintConsole
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

// Produktion:
public sealed class ConsoleLintConsole : ILintConsole
{
    public void Info(string message)  => Console.WriteLine($"[INFO]: {message}");
    public void Warn(string message)  => Console.Error.WriteLine($"[WARN]: {message}");
    public void Error(string message) => Console.Error.WriteLine($"[ERROR]: {message}");
}

// Tests:
public sealed class TestLintConsole : ILintConsole
{
    public List<(string Level, string Message)> Entries { get; } = new();
    public void Info(string message)  => Entries.Add(("INFO", message));
    public void Warn(string message)  => Entries.Add(("WARN", message));
    public void Error(string message) => Entries.Add(("ERROR", message));
}
```

### Verwendung

```csharp
// Vorher:
Console.Error.WriteLine($"[WARN]: Workspace-Diagnose: {msg}");

// Nachher:
_console.Warn($"Workspace-Diagnose: {msg}");
```

### Verdrahtung (kein DI-Container)

```csharp
// Program.cs:
ILintConsole console = new ConsoleLintConsole();
var engine = new LinterEngine(config, profiler, console, rulesJsonContent);
```

Tests übergeben `new TestLintConsole()` direkt im Konstruktoraufruf.

---

## R8 — Rule-Namen als Const-Klasse (`LinterRuleIds`)

**Löst:** F8, A10, C9.2  
**Aufwand:** S (< 1 Tag)  
**Nutzen:** ★★★

### Problem

In `CursorRulesGenerator` und `ViolationTextFormatter` stehen String-Literale wie `"EnforceSealedClasses"`. Wird die Config-Property umbenannt, bricht der String-Lookup zur Laufzeit — kein Compile-Fehler.

### Skelett

```csharp
// src/AiNetLinter/Core/LinterRuleIds.cs
public static class LinterRuleIds
{
    // GlobalConfig
    public const string EnforceSealedClasses        = nameof(GlobalConfig.EnforceSealedClasses);
    public const string AllowUnsealedPartialClasses  = nameof(GlobalConfig.AllowUnsealedPartialClasses);
    public const string EnforceNoSilentCatch         = nameof(GlobalConfig.EnforceNoSilentCatch);
    public const string BanPublicNestedTypes         = nameof(GlobalConfig.BanPublicNestedTypes);
    // ... alle weiteren GlobalConfig-Regeln

    // MetricsConfig
    public const string MaxLineCount                 = nameof(MetricsConfig.MaxLineCount);
    public const string MaxMethodParameterCount      = nameof(MetricsConfig.MaxMethodParameterCount);
    public const string MaxCyclomaticComplexity      = nameof(MetricsConfig.MaxCyclomaticComplexity);
    public const string MaxCognitiveComplexity       = nameof(MetricsConfig.MaxCognitiveComplexity);
    // ... alle weiteren MetricsConfig-Regeln
}
```

### Verwendung

```csharp
// Vorher:
new("EnforceSealedClasses", g => g.EnforceSealedClasses, ...)

// Nachher:
new(LinterRuleIds.EnforceSealedClasses, g => g.EnforceSealedClasses, ...)
```

Compile-Time-Sicherheit; Refactoring einer Property propagiert automatisch über `nameof`.

---

## R9 — `ctx.ReportViolation`-Helper

**Löst:** C12.3  
**Aufwand:** S  
**Nutzen:** ★★★

### Problem

Die Violation-Erstellung wiederholt sich 30+ Mal:

```csharp
ctx.AddViolation(new RuleViolation
{
    FilePath   = ctx.FilePath,
    LineNumber = SyntaxHelper.LineOf(node),
    RuleName   = nameof(ctx.Config.Global.EnforceSealedClasses),
    Details    = "...",
    Guidance   = "..."
});
```

`FilePath` ist dabei immer `ctx.FilePath`.

### Skelett

```csharp
// In CheckerContext:
public void ReportViolation(SyntaxNode node, string ruleName, string details, string guidance)
{
    AddViolation(new RuleViolation
    {
        FilePath   = FilePath,
        LineNumber = SyntaxHelper.LineOf(node),
        RuleName   = ruleName,
        Details    = details,
        Guidance   = guidance
    });
}
```

### Verwendung

```csharp
// Vorher: 6 Zeilen
// Nachher:
ctx.ReportViolation(node, LinterRuleIds.EnforceSealedClasses,
    $"Die Klasse '{name}' ist nicht 'sealed'.",
    "Fuege den 'sealed' Modifikator hinzu.");
```

---

## R10 — Bug-Fix: `VisitRecordDeclaration` ohne `CollectClassInfo`

**Löst:** F11, C2.2  
**Aufwand:** XS (< 1 Stunde)  
**Nutzen:** ★★★ (Bug-Fix)

### Problem

`VisitRecordDeclaration` ruft `ArchitectureChecker.CollectClassInfo` nicht auf. Records fehlen dadurch in `ClassInfo`-Statistiken, die vom Playbook-Generator und `--footprint` genutzt werden. Gleiches gilt für `VisitStructDeclaration`.

### Fix

```csharp
// LinterAnalyzer.cs — VisitRecordDeclaration
public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
{
    if (GeneratedCodeDetector.IsGenerated(node, _ctx)) return;
    NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Record", _ctx);
    NamingChecker.CheckPascalCase(node.Identifier, "Record", _ctx);
    ArchitectureChecker.CheckValueObjectContract(node, node.Identifier.Text, isRecord: true, _ctx);
    ScopeChecker.CheckMethodOverloads(node, _ctx);
    StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
    NestedTypesChecker.Check(node, _ctx);
    PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
    ArchitectureChecker.CollectClassInfo(node, _ctx);  // ← BUG-FIX
    base.VisitRecordDeclaration(node);
}

// LinterAnalyzer.cs — VisitStructDeclaration
public override void VisitStructDeclaration(StructDeclarationSyntax node)
{
    // ... bestehende Checks ...
    ArchitectureChecker.CollectClassInfo(node, _ctx);  // ← BUG-FIX
    base.VisitStructDeclaration(node);
}
```

Nach R2 (Checker aufteilen) werden diese Aufrufe automatisch zu `ClassInfoCollector.Collect(node, _ctx)`.

---

## R11 — Quick-Win: `RepoPlaybookGenerator.RuleDescriptions` reparieren

**Löst:** F9, C6.1  
**Aufwand:** XS (< 1 Stunde)  
**Nutzen:** ★★★ (Bug-Fix)

### Problem

7 hardcoded Werte in `RepoPlaybookGenerator.RuleDescriptions` sind falsch — sie wurden einmal definiert und nie aktualisiert, als sich die Defaults änderten.

### Sofort-Fix (bis R1 fertig ist)

Statt statischer Strings: Werte direkt aus der übergebenen `LinterConfig` lesen.

```csharp
// RepoPlaybookGenerator.cs — RuleDescriptions durch Methode ersetzen
private static Dictionary<string, string> BuildRuleDescriptions(LinterConfig config) => new()
{
    ["EnforceSealedClasses"]      = "Konkrete Klassen muessen 'sealed' sein (oder 'sealed partial').",
    ["BanPublicNestedTypes"]      = "Verbot oeffentlicher nested Typen.",
    ["EnforceNoSilentCatch"]      = "Keine stummen catch-Bloecke.",
    // Metriken mit echten Config-Werten:
    ["MaxLineCount"]              = $"Dateizeilenlimit (max. {config.Metrics.MaxLineCount} Zeilen) ueberschritten.",
    ["MaxMethodLineCount"]        = $"Methode hat zu viele Codezeilen (max. {config.Metrics.MaxMethodLineCount}).",
    ["MaxCyclomaticComplexity"]   = $"Zu hohe zyklomatische Komplexitaet (max. {config.Metrics.MaxCyclomaticComplexity}).",
    ["MaxCognitiveComplexity"]    = $"Zu hohe kognitive Komplexitaet (max. {config.Metrics.MaxCognitiveComplexity}).",
    ["MaxMethodParameterCount"]   = $"Zu viele Methodenparameter (max. {config.Metrics.MaxMethodParameterCount}).",
    ["MaxMethodOverloads"]        = $"Zu viele Methodenueberladungen (max. {config.Metrics.MaxMethodOverloads}).",
    ["MaxConstructorDependencies"]= $"Zu viele Konstruktorabhaengigkeiten (max. {config.Metrics.MaxConstructorDependencies}).",
    // ... alle weiteren Regeln
};
```

**Langfristig** (nach R1): komplett aus `RuleRegistry.Resolve(ruleId).ShortDescription` lesen.

---

## Roadmap: Gesamtreihenfolge

| Woche   | Tasks                                                              | Zustand    |
| ------- | ------------------------------------------------------------------ | ---------- |
| **1**   | R10 (Bug), R11 (Bug), R8 (RuleIds), R5 (Cancellation), R6 (Apply) | Quick Wins |
| **2**   | R4 (Profiler), R1 (RuleRegistry), R7 (ILintConsole), R9 (Helper) | Fundament  |
| **3**   | R2 (Checker aufteilen), R3 (Command-Klassen), R10 in R2 aufgehend | Struktur   |

### Erwartete Resultate

| Metrik                              | Vor Refactoring      | Nach Refactoring                           |
| ----------------------------------- | -------------------- | ------------------------------------------ |
| Zeit für neuen Checker hinzufügen   | 2–4 Stunden          | **30 Min** (neue Datei + Eintrag in LinterAnalyzer) |
| `ArchitectureChecker.cs` LOC        | 303 (18 Methoden)    | ~9 Dateien à ~30–80 LOC                    |
| `Program.cs` LOC                    | 568                  | ~60 (Main + Router + ToLinterArgs)         |
| `Console.WriteLine` in Produktion   | 25+                  | 0                                          |
| Test-Isolation für Checker          | ❌ ganzer Analyzer   | ✅ direkte Methode                          |
| Playbook-Grenzwerte korrekt         | ❌ 7 falsche Werte   | ✅ aus LinterConfig                        |

---

## Fazit

**AiNetLinter ist ein solides Werkzeug**, aber die Architektur muss mit dem Feature-Umfang mitwachsen. Die Refactorings folgen dem bestehenden Architektur-Stil:

- Alles explizit verdrahtet (kein Plugin-System, keine Reflection-Discovery)
- Kein DI-Container (Konstruktor-Parameter wie bisher)
- Statische Kompilierung (kein dynamisches Laden)
- Monolithisches CLI (kein Server-Modus)

1. **R10 + R11** — sofortige Bug-Fixes, kein Risiko
2. **R1 + R4 + R7** — Fundament für saubere Testbarkeit
3. **R2 + R3** — größte strukturelle Hebelwirkung

→ Nach 3 Wochen konsequenter Refactoring-Arbeit sind neue Regeln sicher hinzufügbar und Checker direkt testbar.
