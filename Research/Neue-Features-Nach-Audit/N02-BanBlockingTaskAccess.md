# Implementierungsplan: BanBlockingTaskAccess (N02)

**Basis:** [Research/FeatureAudit/Result/new-features/N02-BanBlockingTaskAccess.md](../FeatureAudit/Result/new-features/N02-BanBlockingTaskAccess.md)  
**Typ:** Boolean-Regel (`GlobalConfig`)  
**Priorität:** 🟢 EMPFOHLEN  
**Aufwand:** ~5–7 h

---

## 1. Ziel

Blockierende Task-Zugriffe (`.Wait()`, `.Result`, `.GetAwaiter().GetResult()`) auf `Task`- und `ValueTask`-Instanzen als Linter-Fehler markieren.  
Konfigurierbare Ausnahmen für `static void Main` und optionale Test-Kontext-Ausnahme.

---

## 2. Änderungsübersicht

| Datei | Art |
|---|---|
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `GlobalConfig` — 3 neue Properties |
| `src/AiNetLinter/Configuration/LinterConfigOverrides.cs` | `GlobalConfigOverride` — 3 neue nullable Properties |
| `src/AiNetLinter/Core/Checkers/BlockingTaskChecker.cs` | **Neu** |
| `src/AiNetLinter/Core/LinterAnalyzer.cs` | `VisitInvocationExpression` + `VisitMemberAccessExpression` erweitern |
| `src/AiNetLinter/Core/RuleRegistry.cs` | `RuleMetadata`-Eintrag ergänzen |
| `src/AiNetLinter.Tests/Core/BlockingTaskCheckerTests.cs` | **Neu** |
| `Docs/ROADMAP.md` | Epic 26 ergänzen (zusammen mit N01) |
| `Docs/configuration.md` | `BanBlockingTaskAccess`-Sektion ergänzen |

---

## 3. Konfiguration (`LinterConfig.cs`)

```csharp
// In GlobalConfig:

/// <summary>
/// Verbietet blockierende Task-Zugriffe: <c>.Wait()</c>, <c>.Result</c>
/// und <c>.GetAwaiter().GetResult()</c> auf <c>Task</c>- und <c>ValueTask</c>-Instanzen.
/// Standard: <c>true</c>.
/// </summary>
public bool BanBlockingTaskAccess { get; init; } = true;

/// <summary>
/// Wenn <c>true</c>: Blockierende Zugriffe in <c>static void Main(...)</c>-Methoden
/// sind erlaubt (Programm-Einstiegspunkt der vor .NET 7.1 kein async Main kannte).
/// Standard: <c>true</c>.
/// </summary>
public bool BanBlockingTaskAccessAllowInMain { get; init; } = true;

/// <summary>
/// Wenn <c>true</c>: Blockierende Zugriffe in Testdateien werden nicht gemeldet.
/// Nützlich für Test-Infrastruktur-Code der kein async-Setup unterstützt.
/// Standard: <c>false</c> (Tests sollten async sein).
/// </summary>
public bool BanBlockingTaskAccessAllowInTests { get; init; } = false;
```

Ergänze in `GlobalConfig.Apply(GlobalConfigOverride?)`:

```csharp
BanBlockingTaskAccess           = o.BanBlockingTaskAccess           ?? BanBlockingTaskAccess,
BanBlockingTaskAccessAllowInMain = o.BanBlockingTaskAccessAllowInMain ?? BanBlockingTaskAccessAllowInMain,
BanBlockingTaskAccessAllowInTests = o.BanBlockingTaskAccessAllowInTests ?? BanBlockingTaskAccessAllowInTests,
```

---

## 4. Override (`LinterConfigOverrides.cs`)

```csharp
// In GlobalConfigOverride:
public bool? BanBlockingTaskAccess { get; init; }
public bool? BanBlockingTaskAccessAllowInMain { get; init; }
public bool? BanBlockingTaskAccessAllowInTests { get; init; }
```

---

## 5. Checker: `BlockingTaskChecker.cs`

**Pfad:** `src/AiNetLinter/Core/Checkers/BlockingTaskChecker.cs`

```csharp
#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class BlockingTaskChecker
{
    // Bekannte Task-Typen (Vollnamen und Kurznamen)
    private static readonly string[] TaskTypeNames =
        ["Task", "ValueTask", "Task`1", "ValueTask`1"];

    internal static void CheckInvocation(InvocationExpressionSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.BanBlockingTaskAccess) return;
        if (ctx.IsTestFile && ctx.Config.Global.BanBlockingTaskAccessAllowInTests) return;

        // Muster 1: .Wait() — InvocationExpression mit MemberAccess ".Wait"
        if (node.Expression is MemberAccessExpressionSyntax waitAccess
            && waitAccess.Name.Identifier.Text == "Wait"
            && IsTaskReceiver(waitAccess.Expression, ctx))
        {
            if (IsInMainMethod(node)) return;
            ctx.ReportViolation(node,
                nameof(ctx.Config.Global.BanBlockingTaskAccess),
                "Blockierender Task-Zugriff '.Wait()' erkannt.",
                BuildGuidance(".Wait()", "await task;"));
        }

        // Muster 3: .GetAwaiter().GetResult() — Kette GetAwaiter + GetResult
        if (node.Expression is MemberAccessExpressionSyntax getResultAccess
            && getResultAccess.Name.Identifier.Text == "GetResult"
            && getResultAccess.Expression is InvocationExpressionSyntax getAwaiterCall
            && getAwaiterCall.Expression is MemberAccessExpressionSyntax getAwaiterAccess
            && getAwaiterAccess.Name.Identifier.Text == "GetAwaiter"
            && IsTaskReceiver(getAwaiterAccess.Expression, ctx))
        {
            if (IsInMainMethod(node)) return;
            ctx.ReportViolation(node,
                nameof(ctx.Config.Global.BanBlockingTaskAccess),
                "Blockierender Task-Zugriff '.GetAwaiter().GetResult()' erkannt.",
                BuildGuidance(".GetAwaiter().GetResult()", "await task;"));
        }
    }

    internal static void CheckMemberAccess(MemberAccessExpressionSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.BanBlockingTaskAccess) return;
        if (ctx.IsTestFile && ctx.Config.Global.BanBlockingTaskAccessAllowInTests) return;

        // Muster 2: .Result — MemberAccessExpression mit Name "Result"
        // Nur prüfen wenn der Knoten nicht bereits Teil einer Invocation ist (GetAwaiter().GetResult() hat .Result nicht)
        if (node.Name.Identifier.Text != "Result") return;
        if (node.Parent is InvocationExpressionSyntax) return; // .Result() wäre eine Methode, nicht die Property
        if (!IsTaskReceiver(node.Expression, ctx)) return;
        if (IsInMainMethod(node)) return;

        ctx.ReportViolation(node,
            nameof(ctx.Config.Global.BanBlockingTaskAccess),
            "Blockierender Task-Zugriff '.Result' erkannt.",
            BuildGuidance(".Result", "await task;"));
    }

    private static bool IsTaskReceiver(ExpressionSyntax expression, CheckerContext ctx)
    {
        // Zuerst syntaktischen Schnellcheck (Type-Name im Text)
        var typeInfo = ctx.SemanticModel.GetTypeInfo(expression);
        var type = typeInfo.Type;
        if (type == null) return IsSyntacticTaskHint(expression);

        // Semantischer Check: ist der Typ Task, ValueTask oder ein generisches Derivat?
        var originalDef = type is INamedTypeSymbol named ? named.OriginalDefinition : type;
        var name = originalDef.Name;
        return Array.Exists(TaskTypeNames, t => t.Equals(name, StringComparison.Ordinal))
            && IsSystemTasksNamespace(type.ContainingNamespace);
    }

    private static bool IsSyntacticTaskHint(ExpressionSyntax expression)
    {
        // Fallback ohne Semantic Model: prüfe ob der Bezeichner "Task" oder "ValueTask" heißt
        var name = expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            InvocationExpressionSyntax inv when inv.Expression is MemberAccessExpressionSyntax ma2
                => ma2.Name.Identifier.Text,
            _ => null
        };
        return name != null && (name.Contains("Task") || name.Contains("Awaitable"));
    }

    private static bool IsSystemTasksNamespace(INamespaceSymbol? ns)
    {
        if (ns == null) return false;
        var full = ns.ToDisplayString();
        return full.StartsWith("System.Threading.Tasks", StringComparison.Ordinal)
            || full.Equals("System.Threading.Tasks", StringComparison.Ordinal);
    }

    private static bool IsInMainMethod(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method == null) return false;
        return method.Identifier.Text == "Main"
            && method.Modifiers.Any(SyntaxKind.StaticKeyword);
    }

    private static string BuildGuidance(string pattern, string fix) =>
        $"Ersetze '{pattern}' durch '{fix}'. Blockierende Zugriffe auf Tasks blockieren ThreadPool-Threads " +
        $"und sind in SynchronizationContext-Umgebungen (ASP.NET Classic, WPF) deadlock-anfaellig. " +
        $"Falls die aufrufende Methode nicht async sein kann: Methode zu 'async Task' umwandeln und " +
        $"die Aufrufkette von oben nach async migrieren.";
}
```

**Designentscheidungen:**
- Semantisches Modell für den Task-Type-Check: verhindert False Positives bei anderen Typen die `.Result` oder `.Wait()` anbieten (z.B. eigene Klassen).
- Syntaktischer Fallback (`IsSyntacticTaskHint`) für Fälle wo das Semantic Model keinen Typ liefert.
- `.Wait()` mit Timeout-Parameter (`task.Wait(1000)`) wird auch erkannt (syntaktisch: InvocationExpression mit Name "Wait").
- `IsInMainMethod` prüft nur auf `static Main` — kein Semantic Model benötigt.

---

## 6. Einbindung in `LinterAnalyzer.cs`

`VisitInvocationExpression` erweitern:

```csharp
public override void VisitInvocationExpression(InvocationExpressionSyntax node)
{
    MinimalApiChecker.Check(node, _ctx);
    PhantomDependencyChecker.CheckPhantomReflection(node, _ctx);
    BlockingTaskChecker.CheckInvocation(node, _ctx);   // ← NEU
    base.VisitInvocationExpression(node);
}
```

Neue Override für MemberAccess:

```csharp
public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
{
    BlockingTaskChecker.CheckMemberAccess(node, _ctx);   // ← NEU
    base.VisitMemberAccessExpression(node);
}
```

> **Achtung:** `VisitMemberAccessExpression` gibt es in `LinterAnalyzer` noch nicht — sie muss neu hinzugefügt werden. Sie wird für alle `MemberAccessExpressionSyntax`-Knoten aufgerufen; der Checker prüft intern die Bedingungen.

---

## 7. `RuleRegistry.cs`

```csharp
new(
    RuleId: "BanBlockingTaskAccess",
    DisplayName: "Kein blockierender Task-Zugriff",
    GetShortDescription: c => "'.Wait()', '.Result' und '.GetAwaiter().GetResult()' auf Tasks sind verboten.",
    Warum: "Blockierende Task-Zugriffe blockieren ThreadPool-Threads und sind in SynchronizationContext-Umgebungen " +
           "(ASP.NET Classic, WPF) deadlock-anfaellig. Agenten produzieren dieses Muster systematisch " +
           "wenn sie synchrone Methoden mit async-APIs verbinden.",
    Alternativen:
    [
        "**'await task'**: Methode zu 'async Task' umwandeln und await verwenden — loest das Problem vollstaendig.",
        "**Aufrufkette async machen**: Von der blockierenden Methode nach oben migrieren bis alle Aufrufer async sind.",
        "**'BanBlockingTaskAccessAllowInMain: true'**: Fuer Programm-Einstiegspunkte die kein async Main haben.",
        "**Suppression** (letztes Mittel): '// ainetlinter-disable BanBlockingTaskAccess' fuer unvermeidliche Stellen."
    ],
    SicherheitsHinweis: null,
    Intent: "agent-resilience",
    Severity: "error",
    CursorHint: "'.Wait()'/'.Result'/'.GetAwaiter().GetResult()' verboten; verwende 'await'.",
    HasAutoFix: false,
    IsEnabled: c => c.Global.BanBlockingTaskAccess,
    IsMetric: false,
    IncludeInCursorRules: true,
    ConfigKeyHint: "rules.json → Global.BanBlockingTaskAccess | BanBlockingTaskAccessAllowInMain | BanBlockingTaskAccessAllowInTests"
),
```

---

## 8. Unit-Tests (`BlockingTaskCheckerTests.cs`)

**Pfad:** `src/AiNetLinter.Tests/Core/BlockingTaskCheckerTests.cs`

```csharp
#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class BlockingTaskCheckerTests
{
    // --- .Wait() ---

    [Fact]
    public void TaskWait_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public void Run()
                {
                    Task.Delay(100).Wait();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("BanBlockingTaskAccess", ctx.Violations[0].RuleName);
    }

    // --- .Result ---

    [Fact]
    public void TaskResult_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public int Run()
                {
                    return Task.FromResult(42).Result;
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            BlockingTaskChecker.CheckMemberAccess(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("BanBlockingTaskAccess", ctx.Violations[0].RuleName);
    }

    // --- .GetAwaiter().GetResult() ---

    [Fact]
    public void GetAwaiterGetResult_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public int Run()
                {
                    return Task.FromResult(42).GetAwaiter().GetResult();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        // Nur eine Violation: die äußerste GetResult()-Invocation
        Assert.Single(ctx.Violations);
        Assert.Equal("BanBlockingTaskAccess", ctx.Violations[0].RuleName);
    }

    // --- Negativ-Tests ---

    [Fact]
    public void AwaitedTask_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public async Task Run()
                {
                    await Task.Delay(100);
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void CustomClass_Result_Property_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            public class MyResult { public int Result { get; set; } }
            public class Foo
            {
                public int Run()
                {
                    var r = new MyResult();
                    return r.Result;
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            BlockingTaskChecker.CheckMemberAccess(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void StaticMain_Wait_Allowed_When_Flag_True()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Program
            {
                static void Main(string[] args)
                {
                    Task.Delay(100).Wait();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(
            config: ConfigWith(ban: true, allowInMain: true),
            semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void RuleDisabled_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public void Run() { Task.Delay(100).Wait(); }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: false), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void TestFile_Wait_Allowed_When_Flag_True()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class FooTests
            {
                public void Setup()
                {
                    Task.Delay(100).Wait();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(
            config: ConfigWith(ban: true, allowInTests: true),
            semanticModel: model,
            isTestFile: true);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    // --- Hilfsmethode ---

    private static LinterConfig ConfigWith(
        bool ban = true,
        bool allowInMain = true,
        bool allowInTests = false) =>
        TestHelper.CreateDefaultConfig() with
        {
            Global = new GlobalConfig
            {
                BanBlockingTaskAccess = ban,
                BanBlockingTaskAccessAllowInMain = allowInMain,
                BanBlockingTaskAccessAllowInTests = allowInTests
            }
        };
}
```

**Testabdeckung:**

| Szenario | Test |
|---|---|
| `.Wait()` auf Task → Violation | `TaskWait_Reports_Violation` |
| `.Result` auf Task → Violation | `TaskResult_Reports_Violation` |
| `.GetAwaiter().GetResult()` → Violation | `GetAwaiterGetResult_Reports_Violation` |
| `await task` → keine Violation | `AwaitedTask_NoViolation` |
| Eigene Klasse mit `.Result`-Property → keine Violation | `CustomClass_Result_Property_NoViolation` |
| `static Main` + Flag true → erlaubt | `StaticMain_Wait_Allowed_When_Flag_True` |
| Regel deaktiviert → keine Violation | `RuleDisabled_NoViolation` |
| Testdatei + Flag true → erlaubt | `TestFile_Wait_Allowed_When_Flag_True` |

---

## 9. Dokumentations-Updates

### `Docs/ROADMAP.md`

Ergänzt unter Epic 26 (zusammen mit N01):

```markdown
- [ ] **Regel: BanBlockingTaskAccess** — Verbietet `.Wait()`, `.Result` und `.GetAwaiter().GetResult()` auf Tasks.
```

### `Docs/configuration.md`

```markdown
### BanBlockingTaskAccess

| Schlüssel | Typ | Standard |
|---|---|---|
| `BanBlockingTaskAccess` | `bool` | `true` |
| `BanBlockingTaskAccessAllowInMain` | `bool` | `true` |
| `BanBlockingTaskAccessAllowInTests` | `bool` | `false` |

Verbietet blockierende Task-Zugriffe (`.Wait()`, `.Result`, `.GetAwaiter().GetResult()`).
Diese Muster blockieren ThreadPool-Threads und können in SynchronizationContext-Umgebungen
(ASP.NET Classic, WPF) zu Deadlocks führen.
```

---

## 10. `rules.json`-Eintrag

```json
"Global": {
  "BanBlockingTaskAccess": true,
  "BanBlockingTaskAccessAllowInMain": true,
  "BanBlockingTaskAccessAllowInTests": false
}
```

---

## 11. Commit-Vorschlag

```
feat: Regel BanBlockingTaskAccess ergänzt

Meldet blockierende Task-Zugriffe (.Wait(), .Result, .GetAwaiter().GetResult())
als Linter-Fehler. Ausnahmen für static Main und Testdateien konfigurierbar.
Ergänzt N01 (BanAsyncVoid) zu einem vollständigen Async-Anti-Pattern-Set.
```

---

## 12. Implementierungsreihenfolge (zusammen mit N01)

N01 und N02 teilen keine Abhängigkeiten — sie können parallel implementiert werden.  
Empfehlung: N01 zuerst (einfacher, weniger Visitor-Typen), dann N02.

---

## 13. Offene Fragen / Risiken

| Frage | Empfehlung |
|---|---|
| False Positives bei eigenen Klassen mit `.Result` Property? | Semantisches Modell reduziert das auf Minimum. Syntaktischer Fallback ist konservativ (prüft nur Task/ValueTask im Bezeichnernamen). |
| `.Wait(CancellationToken)` auch blockierend? | Ja, alle Wait()-Überladungen sind blockierend. Der Checker prüft nur den Methodennamen "Wait", nicht die Signatur — erfasst alle Überladungen korrekt. |
| `Task.WaitAll()` und `Task.WhenAll()` prüfen? | `WhenAll` ist async-freundlich (gibt Task zurück). `WaitAll` ist blockierend — als Phase-2-Erweiterung via `BanBlockingTaskAccessBanWaitAll`-Flag. |
| Interaktion mit `AllowedEmptyReads`? | Keine Überschneidung. |
