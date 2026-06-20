# Implementierungsplan: MaxLinqChainLength (N03)

**Basis:** [Research/FeatureAudit/Result/new-features/N03-MaxLinqChainLength.md](../FeatureAudit/Result/new-features/N03-MaxLinqChainLength.md)  
**Typ:** Numerische Metrik (`MetricsConfig`)  
**Priorität:** 🟡 PRÜFEN  
**Aufwand:** ~6–9 h (mittel-komplex durch LINQ-Ketten-Walk)

---

## 1. Ziel

Verkettete LINQ-Methoden pro Expressions-Kette zählen und ab dem konfigurierten Schwellenwert als `warning` melden.  
Nur echte LINQ-Methoden zählen (konfigurierbare Whitelist); Builder-Chains (`.AddLogging().Build()`) werden nicht erfasst.

---

## 2. Änderungsübersicht

| Datei | Art |
|---|---|
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `MetricsConfig` — 2 neue Properties + LINQ-Whitelist |
| `src/AiNetLinter/Core/Checkers/LinqChainLengthChecker.cs` | **Neu** |
| `src/AiNetLinter/Core/LinterAnalyzer.cs` | `VisitInvocationExpression` erweitern |
| `src/AiNetLinter/Core/RuleRegistry.cs` | `RuleMetadata`-Eintrag ergänzen |
| `src/AiNetLinter.Tests/Core/LinqChainLengthCheckerTests.cs` | **Neu** |
| `Docs/ROADMAP.md` | Epic 27 ergänzen |
| `Docs/configuration.md` | `MaxLinqChainLength`-Sektion ergänzen |

> **Hinweis:** N03 hat keinen Override in `MetricsConfigOverride` — die bestehenden numerischen Metriken folgen dem Muster ohne Override-Record. Falls Projekt-Overrides nötig werden, kann das als Phase-2-Feature ergänzt werden.

---

## 3. Konfiguration (`LinterConfig.cs`)

```csharp
// In MetricsConfig:

/// <summary>
/// Maximale Anzahl verketteter LINQ-Methoden in einer einzelnen Ausdruckskette.
/// 0 = deaktiviert. Empfehlung: 5 (ab 6 Methoden: warning).
/// Nur Methoden aus <see cref="LinqMethodNames"/> zählen.
/// </summary>
public int MaxLinqChainLength { get; init; } = 0;

/// <summary>
/// LINQ-Methoden-Namen, die als Teil einer LINQ-Kette gewertet werden.
/// Nicht-LINQ-Chains (z. B. Builder-Chains) werden damit von der Prüfung ausgeschlossen.
/// Konfigurierbar für projektspezifische LINQ-ähnliche APIs (z. B. EF Core Fluent API).
/// </summary>
public IReadOnlyCollection<string> LinqMethodNames { get; init; } =
[
    "Where", "Select", "SelectMany",
    "GroupBy", "GroupJoin", "Join",
    "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
    "Take", "TakeWhile", "Skip", "SkipWhile",
    "First", "FirstOrDefault", "Last", "LastOrDefault",
    "Single", "SingleOrDefault",
    "Count", "LongCount", "Any", "All",
    "Distinct", "DistinctBy", "Union", "UnionBy",
    "Intersect", "IntersectBy", "Except", "ExceptBy",
    "Aggregate", "Sum", "Min", "Max", "Average", "MinBy", "MaxBy",
    "ToList", "ToArray", "ToDictionary", "ToHashSet", "ToLookup",
    "Cast", "OfType", "Append", "Prepend", "Reverse",
    "Zip", "Chunk", "Flatten"
];
```

> **Standard `MaxLinqChainLength = 0`:** Deaktiviert per Default — Evidenz ist 🟡 (nicht 🟢). Bewusstes Einschalten via `rules.json`.

---

## 4. Checker: `LinqChainLengthChecker.cs`

**Pfad:** `src/AiNetLinter/Core/Checkers/LinqChainLengthChecker.cs`

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class LinqChainLengthChecker
{
    internal static void Check(InvocationExpressionSyntax node, CheckerContext ctx)
    {
        var limit = ctx.Config.Metrics.MaxLinqChainLength;
        if (limit <= 0) return;

        // Nur die Wurzel einer Kette prüfen (nicht jeden inneren Knoten)
        if (IsNestedLinqCall(node)) return;

        var chainLength = CountLinqChain(node, ctx.Config.Metrics.LinqMethodNames);
        if (chainLength <= limit) return;

        ctx.ReportViolation(node,
            nameof(ctx.Config.Metrics.MaxLinqChainLength),
            $"LINQ-Kette hat {chainLength} Methoden (erlaubt: {limit}).",
            BuildGuidance(chainLength, limit));
    }

    /// <summary>
    /// Zählt die LINQ-Methoden in einer Kette.
    /// Walk: Ausgehend vom äußersten Aufruf die Expression-Kette nach innen.
    /// </summary>
    private static int CountLinqChain(
        InvocationExpressionSyntax root,
        IReadOnlyCollection<string> linqNames)
    {
        var count = 0;
        SyntaxNode current = root;

        while (current is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax access)
        {
            var name = access.Name.Identifier.Text;
            if (!IsLinqMethod(name, linqNames)) break;

            count++;
            current = access.Expression;
        }

        return count;
    }

    /// <summary>
    /// Prüft ob dieser Knoten selbst ein innerer LINQ-Aufruf ist
    /// (d.h. ob er als Expression in einem äußeren InvocationExpression vorkommt).
    /// Wenn ja, wird er übersprungen — nur die äußerste Kette zählt.
    /// </summary>
    private static bool IsNestedLinqCall(InvocationExpressionSyntax node)
    {
        if (node.Parent is MemberAccessExpressionSyntax parentAccess
            && parentAccess.Expression == node
            && parentAccess.Parent is InvocationExpressionSyntax)
            return true;
        return false;
    }

    private static bool IsLinqMethod(string name, IReadOnlyCollection<string> linqNames) =>
        linqNames.Contains(name, StringComparer.Ordinal);

    private static string BuildGuidance(int actual, int limit) =>
        $"Eine LINQ-Kette mit {actual} Methoden erzeugt sequenzielle kognitive Last, die weder " +
        $"zyklomatische noch kognitive Komplexitaet erfasst. " +
        $"Alternativen: (1) Zwischenergebnis in benannte Variable extrahieren und Kette aufteilen. " +
        $"(2) Komplex-Teile in private Methoden mit sprechenden Namen auslagern " +
        $"(z. B. 'FilterActiveOrders()', 'RankByRevenue()'). " +
        $"(3) Query-Syntax statt Method-Syntax fuer mehrstufige Abfragen verwenden wenn lesbarkeit wichtiger ist als Kompaktheit.";
}
```

**Algorithmus (Walk):**

```
Eingabe: InvocationExpressionSyntax (äußerster Aufruf der Kette)

1. Falls dieser Knoten selbst innerer Aufruf ist → return (IsNestedLinqCall)
2. Starte count = 0, current = root
3. Schleife:
   a. current muss InvocationExpression sein
   b. Expression von current muss MemberAccessExpression sein
   c. Name des MemberAccess muss in LinqMethodNames sein → count++
   d. current = Expression des MemberAccess (eine Ebene tiefer)
4. Wenn Bedingung nicht erfüllt → Schleife beendet
5. Wenn count > limit → Violation
```

**Beispiel-Walk für `list.Where(...).Select(...).OrderBy(...).Take(5)`:**

```
root = Take(5)                       → "Take" ∈ LINQ → count=1, current = OrderBy(...)
current = OrderBy(...)               → "OrderBy" ∈ LINQ → count=2, current = Select(...)  
current = Select(...)                → "Select" ∈ LINQ → count=3, current = Where(...)
current = Where(...)                 → "Where" ∈ LINQ → count=4, current = list
current = list (IdentifierName)      → kein InvocationExpression → stop
Ergebnis: chainLength = 4
```

---

## 5. Einbindung in `LinterAnalyzer.cs`

```csharp
public override void VisitInvocationExpression(InvocationExpressionSyntax node)
{
    MinimalApiChecker.Check(node, _ctx);
    PhantomDependencyChecker.CheckPhantomReflection(node, _ctx);
    BlockingTaskChecker.CheckInvocation(node, _ctx);
    LinqChainLengthChecker.Check(node, _ctx);   // ← NEU
    base.VisitInvocationExpression(node);
}
```

---

## 6. `RuleRegistry.cs`

```csharp
new(
    RuleId: "MaxLinqChainLength",
    DisplayName: "Maximale LINQ-Kettenlaenge",
    GetShortDescription: c => $"LINQ-Kette ueberschreitet das Limit (max. {c.Metrics.MaxLinqChainLength} Methoden).",
    Warum: "Lange LINQ-Ketten erzeugen sequenzielle kognitive Last, die weder zyklomatische noch kognitive " +
           "Komplexitaet messen. Ein LLM-Agent der eine 8-gliedrige Kette erweitern soll, macht haeufig " +
           "Typfehler an der Einschnittstelle. (Evidenz: moderat — 0 = deaktiviert per Default.)",
    Alternativen:
    [
        "**Kette aufteilen**: Zwischenergebnis in benannte Variable extrahieren ('var activeOrders = orders.Where(...);').",
        "**Private Hilfsmethoden**: Teilketten in benannte Methoden auslagern ('FilterActiveOrders()', 'RankByRevenue()').",
        "**Query-Syntax**: Fuer mehrstufige Abfragen kann 'from x in ... where ... select ...' lesbarer sein.",
        "**Suppression**: '// ainetlinter-disable MaxLinqChainLength' fuer legitime komplexe Datentransformationen."
    ],
    SicherheitsHinweis: null,
    Intent: "agent-context",
    Severity: "warning",
    CursorHint: "0 = deaktiviert; lange LINQ-Ketten in Teilschritte aufteilen.",
    HasAutoFix: false,
    IsEnabled: c => c.Metrics.MaxLinqChainLength > 0,
    IsMetric: true,
    IncludeInCursorRules: true,
    GetMetricLimit: c => c.Metrics.MaxLinqChainLength,
    ConfigKeyHint: "rules.json → Metrics.MaxLinqChainLength | Metrics.LinqMethodNames"
),
```

---

## 7. Unit-Tests (`LinqChainLengthCheckerTests.cs`)

**Pfad:** `src/AiNetLinter.Tests/Core/LinqChainLengthCheckerTests.cs`

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class LinqChainLengthCheckerTests
{
    // --- Positiv-Tests (Violation erwartet) ---

    [Fact]
    public void LinqChain_ExceedsLimit_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x).Take(5).Skip(1);
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 4), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("MaxLinqChainLength", ctx.Violations[0].RuleName);
    }

    [Fact]
    public void LinqChain_AtLimit_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x).Take(5);
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 4), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    // --- Negativ-Tests ---

    [Fact]
    public void Disabled_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x).Take(5).Skip(1);
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 0), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void BuilderChain_NonLinqMethods_NoViolation()
    {
        // .AddLogging().AddRouting().Build() sind keine LINQ-Methoden → keine Violation
        var (tree, model) = TestHelper.ParseCode("""
            public class Builder
            {
                public Builder AddLogging() => this;
                public Builder AddRouting() => this;
                public Builder AddCaching() => this;
                public Builder AddAuth() => this;
                public Builder AddCors() => this;
                public void Build() { }
            }
            public class Foo
            {
                public void Run(Builder b)
                {
                    b.AddLogging().AddRouting().AddCaching().AddAuth().AddCors().Build();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 3), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void OnlyOneViolation_PerChain()
    {
        // Eine Kette aus 6 Methoden soll genau eine Violation erzeugen, nicht 6
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x)
                            .Take(5).Skip(1).Distinct();
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 3), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Single(ctx.Violations);
    }

    [Fact]
    public void CustomLinqMethod_InWhitelist_Counts()
    {
        // Benutzerdefinierte Methode die zur Whitelist hinzugefügt wird
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public static class MyExtensions
            {
                public static IEnumerable<T> FilterActive<T>(this IEnumerable<T> src) => src;
            }
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x).FilterActive();
            }
            """);
        var customNames = new List<string>(DefaultLinqNames()) { "FilterActive" };
        var ctx = TestHelper.CreateContext(
            config: ConfigWithCustomNames(limit: 3, names: customNames),
            semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Single(ctx.Violations);
    }

    [Fact]
    public void ShortChain_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Take(5);
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 5), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    // --- Hilfsmethoden ---

    private static LinterConfig ConfigWith(int limit) =>
        TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxLinqChainLength = limit }
        };

    private static LinterConfig ConfigWithCustomNames(int limit, IReadOnlyCollection<string> names) =>
        TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxLinqChainLength = limit, LinqMethodNames = names }
        };

    private static IReadOnlyCollection<string> DefaultLinqNames() =>
        new MetricsConfig().LinqMethodNames;
}
```

**Testabdeckung:**

| Szenario | Test |
|---|---|
| Kette überschreitet Limit → Violation | `LinqChain_ExceedsLimit_Reports_Violation` |
| Kette exakt am Limit → keine Violation | `LinqChain_AtLimit_NoViolation` |
| `MaxLinqChainLength = 0` → deaktiviert | `Disabled_NoViolation` |
| Builder-Chain (kein LINQ) → keine Violation | `BuilderChain_NonLinqMethods_NoViolation` |
| Nur eine Violation pro Kette | `OnlyOneViolation_PerChain` |
| Benutzerdefinierte LINQ-Extension in Whitelist | `CustomLinqMethod_InWhitelist_Counts` |
| Kurze Kette → keine Violation | `ShortChain_NoViolation` |

---

## 8. Dokumentations-Updates

### `Docs/ROADMAP.md`

Neues Epic ergänzen:

```markdown
## Epic 27: LINQ-Komplexitäts-Kontrolle

- [ ] **Regel: MaxLinqChainLength** — Begrenzt die Anzahl verketteter LINQ-Methoden pro Ausdruckskette (Standard: 0 = deaktiviert).
```

### `Docs/configuration.md`

```markdown
### MaxLinqChainLength

| Schlüssel | Typ | Standard |
|---|---|---|
| `MaxLinqChainLength` | `int` | `0` (deaktiviert) |
| `LinqMethodNames` | `string[]` | Vollständige Liste (s. u.) |

Begrenzt die Anzahl verketteter LINQ-Methoden in einer einzelnen Ausdruckskette.
Eine Kette mit mehr Methoden als der Schwellenwert erzeugt eine `warning` (kein `error`).

**Empfohlener Schwellenwert:** 5 (ab 6 Methoden Warnung).

**Konfigurationsbeispiel:**
```json
"Metrics": {
  "MaxLinqChainLength": 5
}
```

**Erweiterung der Whitelist** für projektspezifische LINQ-ähnliche APIs (z. B. EF Core Fluent API):
```json
"Metrics": {
  "LinqMethodNames": ["Where", "Select", "...", "Include", "ThenInclude"]
}
```

> Evidenz: moderat (keine dedizierte Studie zu LINQ-Kettenlänge und LLM-Fehlerrate).
> Deshalb Standard-deaktiviert — bewusstes Opt-in via `rules.json`.
```

---

## 9. `rules.json`-Eintrag

```json
"Metrics": {
  "MaxLinqChainLength": 0
}
```

Wenn aktiviert:

```json
"Metrics": {
  "MaxLinqChainLength": 5
}
```

---

## 10. Commit-Vorschlag

```
feat: Regel MaxLinqChainLength ergänzt

Zaehlt verkettete LINQ-Methoden pro Ausdruckskette und meldet ab
konfigurierbarem Schwellenwert eine Warnung. Standard deaktiviert (0)
weil Evidenz moderat ist. Konfigurierbare LINQ-Methoden-Whitelist
verhindert False Positives bei Builder-Chains.
```

---

## 11. Technische Risiken & Mitigationen

| Risiko | Mitigation |
|---|---|
| False Positives bei langen Builder-Chains | Whitelist-Ansatz: Nur explizit konfigurierte LINQ-Methoden zählen |
| Mehrfach-Violation für eine Kette | `IsNestedLinqCall`-Guard: Nur äußersten Knoten der Kette prüfen |
| Performance bei vielen InvocationExpressions | Walk bricht bei erstem Nicht-LINQ-Knoten ab; O(n) mit n = Kettenlänge |
| Einzeilige vs. mehrzeilige LINQ-Chains | Walk ist AST-basiert, nicht textbasiert — Formatierung irrelevant |
| Konfigurations-Drift bei LinqMethodNames | `LinterConfigSyncer` sorgt für automatisches Ergänzen fehlender Schlüssel bei Versions-Update |

---

## 12. Abwägung: Severity `warning` statt `error`

N03 ist bewusst auf `warning` gesetzt (im Gegensatz zu N01/N02 mit `error`):

- **N01/N02** sind funktionale Anti-Patterns die zur Laufzeit Fehler verursachen → `error` korrekt
- **N03** ist ein Lesbarkeits-/Wartbarkeitsproblem — lange LINQ-Chains **funktionieren**, sind aber schwieriger für LLM-Agenten zu modifizieren → `warning` angemessen
- Konsistent mit anderen Lesbarkeits-Regeln im Projekt (`MaxMethodOverloads`, `MaxBoolParameterCount` sind `warning`)

---

## 13. Offen: Phase-2-Erweiterungen

| Erweiterung | Aufwand | Begründung |
|---|---|---|
| Nested LINQ-Lambdas (LINQ in LINQ) prüfen | Mittel | Lambda-Expressions in LINQ-Operatoren die selbst LINQ enthalten erhöhen Komplexität exponentiell |
| EF Core `Include`/`ThenInclude` Standardliste | Gering | Häufiger Anwendungsfall; einfach zur Default-Whitelist ergänzen |
| `MaxLinqChainLength` per Datei-Typ (`*.Tests.cs` höher) | Mittel | Testdaten-Builder nutzen oft längere LINQ-Chains |
