# R01 — CompoundSuppression auf Komplexitäts-Metriken ausweiten

**Problem:** `GlobalConfig.Apply()` in `LinterConfig.cs:226` trägt zwei Inline-Suppressions
(`MaxCyclomaticComplexity`, `MaxCognitiveComplexity`), weil ~30 Zeilen der Form
`Field = o.Field ?? Field` den McCabe- und SonarSource-Zähler in die Höhe treiben —
obwohl die Methode trivial lesbar ist.

Die bestehende `CompoundSuppression`-Infrastruktur löst dieses Problem bereits für
`MaxMethodLineCount` (relaxt auf 150 wenn CC ≤ 3). Für Komplexitäts-Metriken selbst
gibt es noch keinen Mechanismus.

---

## Diagnose: Warum feuern die Regeln hier?

Jeder `??`-Operator wird von McCabe als Branch gezählt (+1 pro Operator).
`GlobalConfig.Apply()` hat ~30 `??`-Zuweisungen → CC ≈ 31.
Kognitiv ist die Methode flach: kein Nesting, kein Control-Flow, ein einziger `return`.

Das ist ein bekannter False-Positive-Typ für reine "Merge-Methoden" (Null-Coalescing-Initializer).

---

## Lösungsansatz: "NullCoalescingInitializer"-Classifier

Analog zu `ExcludeSwitchDispatcherCases` einen neuen Classifier einführen, der Methoden
erkennt, deren gesamter Body ein einziger `return this with { … }` bzw. `return new T { … }`
Ausdruck ist — alle Branches kommen ausschließlich von `??`- oder `?:`-Operatoren.

Erkannte Methoden werden von `MaxCyclomaticComplexity` und `MaxCognitiveComplexity`
ausgenommen (wie Dispatcher von `MaxSwitchArms`).

---

## Konkrete Änderungen

### 1. `MetricsConfig.cs` — neue Konfigurations-Properties

```csharp
/// <summary>
/// Methoden, deren Body ausschließlich ein 'return this with { … }' oder
/// 'return new T { … }' mit Null-Coalescing-Zuweisungen ist, werden von
/// MaxCyclomaticComplexity und MaxCognitiveComplexity ausgenommen.
/// Standard: true — diese Methoden sind semantisch flach trotz hohem McCabe-Wert.
/// </summary>
public bool ExcludeNullCoalescingInitializerComplexity { get; init; } = true;

/// <summary>
/// Maximaler Anteil an nicht-null-coalescing-Ästen damit eine Methode
/// als NullCoalescingInitializer gilt (0.0–1.0).
/// Standard: 0.0 — alle Branches müssen ?? oder ?: sein.
/// </summary>
public double NullCoalescingInitializerMaxNonCoalescingRatio { get; init; } = 0.0;
```

Entsprechend in `MetricsConfigOverride`:
```csharp
public bool? ExcludeNullCoalescingInitializerComplexity { get; init; }
public double? NullCoalescingInitializerMaxNonCoalescingRatio { get; init; }
```

Und in `MetricsConfig.Apply()` (innerhalb `ApplyComplexityLimits`):
```csharp
ExcludeNullCoalescingInitializerComplexity =
    o.ExcludeNullCoalescingInitializerComplexity ?? ExcludeNullCoalescingInitializerComplexity,
NullCoalescingInitializerMaxNonCoalescingRatio =
    o.NullCoalescingInitializerMaxNonCoalescingRatio ?? NullCoalescingInitializerMaxNonCoalescingRatio,
```

### 2. `MethodClassifier.cs` (neu oder Erweiterung von bestehendem Classifier)

```csharp
internal static bool IsNullCoalescingInitializer(
    MethodDeclarationSyntax method,
    double maxNonCoalescingRatio)
{
    // Muss ein einziger return-Statement sein
    var body = method.Body;
    if (body is null) return false;
    var stmts = body.Statements;
    if (stmts.Count != 1 || stmts[0] is not ReturnStatementSyntax ret) return false;

    // Ausdruck muss WithExpression oder ObjectCreationExpression sein
    var expr = ret.Expression?.UnwrapParentheses();
    if (expr is not (WithExpressionSyntax or ObjectCreationExpressionSyntax)) return false;

    // Alle Initializer-Einträge müssen AssignmentExpression mit ?? auf der rechten Seite sein
    var assignments = expr.DescendantNodes()
        .OfType<AssignmentExpressionSyntax>()
        .ToList();
    if (assignments.Count == 0) return false;

    var coalescing = assignments.Count(a =>
        a.Right is BinaryExpressionSyntax b &&
        b.IsKind(SyntaxKind.CoalesceExpression));

    var ratio = 1.0 - (double)coalescing / assignments.Count;
    return ratio <= maxNonCoalescingRatio;
}
```

### 3. `ComplexityChecker.cs` — Classifier einbinden

In der Stelle wo `MaxCyclomaticComplexity` und `MaxCognitiveComplexity` geprüft werden:

```csharp
if (config.Metrics.ExcludeNullCoalescingInitializerComplexity
    && MethodClassifier.IsNullCoalescingInitializer(
           method, config.Metrics.NullCoalescingInitializerMaxNonCoalescingRatio))
{
    return; // kein Report
}
```

### 4. Suppress-Kommentare in `LinterConfig.cs` entfernen

```diff
-// ainetlinter-disable MaxCyclomaticComplexity
-// ainetlinter-disable MaxCognitiveComplexity
 public GlobalConfig Apply(GlobalConfigOverride? @override)
```

---

## Unit Tests

Datei: `src/AiNetLinter.Tests/Core/NullCoalescingInitializerClassifierTests.cs`

```csharp
public class NullCoalescingInitializerClassifierTests
{
    [Fact]
    public void WithExpression_AllNullCoalescing_ReturnsTrue()
    {
        var code = """
            class C {
                C Apply(C? o) {
                    if (o == null) return this;
                    return this with { X = o.X ?? X, Y = o.Y ?? Y };
                }
            }
            """;
        // Arrange: parse, get MethodDeclarationSyntax
        // Act: IsNullCoalescingInitializer(method, 0.0)
        // Assert: false — wegen Guard-if-Statement (2 Statements, nicht 1)
    }

    [Fact]
    public void SingleReturnWithExpression_AllNullCoalescing_ReturnsTrue()
    {
        var code = """
            record R(int X, int Y) {
                R Apply(R? o) => o == null ? this : this with { X = o.X ?? X };
            }
            """;
        // Expression-body — kein Body, daher: false
        // Nur Block-Body wird klassifiziert
    }

    [Fact]
    public void BlockBody_SingleReturn_AllCoalescing_ReturnsTrue()
    {
        // Body mit genau 1 return this with { A = o.A ?? A, B = o.B ?? B }
        // → IsNullCoalescingInitializer == true
    }

    [Fact]
    public void BlockBody_HasNonCoalescingAssignment_ReturnsFalse()
    {
        // Body mit return this with { A = o.A ?? A, B = o.B.GetValueOrDefault() }
        // → IsNullCoalescingInitializer == false (ratio > 0.0)
    }
}
```

> Hinweis: Die konkreten Test-Implementierungen parsen via `SyntaxFactory.ParseSyntaxTree`.
> Das Muster folgt bestehenden `ControlFlowResilienceTests`.

---

## Dokumentation

**`Docs/DeepResearch/metrics/M04-MaxCyclomaticComplexity.md`** — Abschnitt "Bekannte False Positives" ergänzen:

> **Null-Coalescing-Initializer:** Methoden mit `return this with { A = o.A ?? A, … }` erzeugen
> hohen McCabe-Wert, sind aber semantisch trivial. Mit `ExcludeNullCoalescingInitializerComplexity: true`
> (Standard) werden solche Methoden ausgenommen.

**`Docs/configuration.md`** — Abschnitt `MetricsConfig` um die zwei neuen Properties erweitern.

**`Docs/ROADMAP.md`** — Als implementiert markieren sobald umgesetzt.

---

## Commit-Vorschlag

```
feat: NullCoalescingInitializer-Classifier für MaxCyclomaticComplexity/MaxCognitiveComplexity ergänzen
```

Entfernt die zwei Inline-Suppressions in `LinterConfig.cs` durch einen konfigurierbaren
Classifier, der reine Null-Coalescing-Merge-Methoden von Komplexitäts-Checks ausnimmt.
