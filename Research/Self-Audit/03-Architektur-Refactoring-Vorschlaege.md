# 03 — Architektur-Refactoring-Vorschläge

> Konkrete Refactoring-Pläne mit Code-Skeletten, basierend auf den Befunden in
> [`01-Architektur-Befunde.md`](01-Architektur-Befunde.md) und [`02-Code-Qualitaet.md`](02-Code-Qualitaet.md).
>
> **Bindende Constraints:** Kein Plugin-System (Verdrahtung bleibt explizit), kein DI-Container (Konstruktor-Parameter statt Framework), statische Kompilierung, monolithisches CLI-Tool.

---

## Inhaltsverzeichnis

- [R4 — `PerformanceProfiler` entkoppeln + optional machen](#r4--performanceprofiler-entkoppeln--optional-machen)
- [R5 — `CancellationToken` durch die Pipeline](#r5--cancellationtoken-durch-die-pipeline)
- [R6 — `Apply`-Refactoring: ein `with`-Block](#r6--apply-refactoring-ein-with-block)
- [R7 — `ILintConsole`-Interface statt `Console.WriteLine`](#r7--ilintconsole-interface-statt-consolewriteline)
- [R8 — Rule-Namen als Const-Klasse (`LinterRuleIds`)](#r8--rule-namen-als-const-klasse-linterruleids)
- [R9 — `ctx.ReportViolation`-Helper](#r9--ctxreportviolation-helper)
- [R11 — Quick-Win: `RepoPlaybookGenerator.RuleDescriptions` reparieren](#r11--quick-win-repoplaybookgeneratorruledescriptions-reparieren)
- [Roadmap: Gesamtreihenfolge](#roadmap-gesamtreihenfolge)

> **✅ R3 (— `Program.cs` in statische Command-Klassen aufteilen) wurde abgeschlossen.**
> `Program.cs` hat jetzt ~80 LOC. Alle 8 Sub-Befehle befinden sich in `src/AiNetLinter/Commands/`.

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

| **1**   | R11 (Bug), R8 (RuleIds), R5 (Cancellation), R6 (Apply)             | Quick Wins |
| **2**   | R4 (Profiler), R7 (ILintConsole), R9 (Helper)                     | Fundament  |

> **✅ R3 (— Command-Klassen) wurde abgeschlossen.** `Program.cs` reduziert auf ~80 LOC; 8 Command-Klassen in `src/AiNetLinter/Commands/`.

### Erwartete Resultate

| Metrik                              | Vor Refactoring      | Nach Refactoring                           |
| ----------------------------------- | -------------------- | ------------------------------------------ |
| Zeit für neuen Checker hinzufügen   | 2–4 Stunden          | **30 Min** (neue Datei + Eintrag in LinterAnalyzer) |
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

1. **R11** — sofortige Bug-Fixes, kein Risiko
2. **R4 + R7** — Fundament für saubere Testbarkeit
3. **R3** — größte strukturelle Hebelwirkung

→ Nach konsequenter Refactoring-Arbeit sind neue Regeln sicher hinzufügbar und Checker direkt testbar.
