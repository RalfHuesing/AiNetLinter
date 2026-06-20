# MaxSwitchArms — Implementierungsplan

**Bewertung:** Sinnvoll und direkt umsetzbar. Alle Infrastruktur-Bausteine (SwitchDispatcherDetector, MetricsConfig/Override-Muster, ComplexityChecker.CheckMethod als Einhängepunkt) sind vorhanden. Aufwand: ~2–3 Stunden.

---

## 1. Intention

Switch-Expressions und Switch-Statements mit mehr als 10 Arms überschreiten die effektive Aufmerksamkeits-Kapazität von KI-Agenten bei lokalen Edits. Symptome: übersehene Überlappungen, vergessene Wildcard-Guards, Referenzen auf nicht existente Enum-Werte.

`MaxSwitchArms` begrenzt die absolute Armzahl **zusätzlich** zur bestehenden Komplexitätsprüfung — unabhängig vom Dispatcher-Pattern. Dispatcher-Methoden (reine Routing-Tabellen) können per `MaxSwitchArmsExcludeDispatcher: true` ausgenommen werden, da `SwitchDispatcherDetector` bereits existiert.

**Default:** `10` (aktiv). Dispatcher-Methoden sind per `MaxSwitchArmsExcludeDispatcher: true` (ebenfalls default) ausgenommen, sodass legitime Routing-Tabellen keinen False-Positive auslösen.

---

## 2. Betroffene Dateien

| Datei | Änderungstyp |
|:------|:-------------|
| `src/AiNetLinter/Configuration/LinterConfig.cs` | Neue Properties in `MetricsConfig` + `Apply` |
| `src/AiNetLinter/Configuration/LinterConfigOverrides.cs` | Neue nullable Properties in `MetricsConfigOverride` |
| `src/AiNetLinter/Core/LinterRuleIds.cs` | Neue Konstante |
| `src/AiNetLinter/Core/Checkers/ComplexityChecker.cs` | Neue Methode `CheckSwitchArms` + Aufruf in `CheckMethod` |
| `src/AiNetLinter.Tests/MaxSwitchArmsTests.cs` | Neue Testdatei (7 Tests) |
| `Docs/configuration.md` | 3 neue Zeilen in der Regel-Tabelle |
| `Docs/ROADMAP.md` | Eintrag als erledigt markieren |

---

## 3. Konkrete Codeänderungen

### 3.1 `MetricsConfig` — neue Properties

**Datei:** `src/AiNetLinter/Configuration/LinterConfig.cs`

Hinter `SwitchDispatcherMaxCaseBodyLines` (Zeile ~353) einfügen:

```csharp
/// <summary>
/// Maximale Anzahl Arms in einem Switch-Expression oder Labels in einem Switch-Statement.
/// 0 = deaktiviert. Empfehlung: 10.
/// Dispatcher-Methoden können mit <see cref="MaxSwitchArmsExcludeDispatcher"/> ausgenommen werden.
/// </summary>
public int MaxSwitchArms { get; init; } = 10;

/// <summary>
/// Wenn true: Methoden, die als Switch-Dispatcher klassifiziert werden
/// (<see cref="SwitchDispatcherMaxCaseBodyLines"/>), werden von MaxSwitchArms ausgenommen.
/// Standard: true.
/// </summary>
public bool MaxSwitchArmsExcludeDispatcher { get; init; } = true;

/// <summary>
/// Einfache Typnamen (kein Namespace), deren Methoden von MaxSwitchArms ausgenommen werden.
/// Nützlich für State-Machine-Klassen mit vielen legitimen States.
/// Standard: [] (keine Ausnahmen).
/// </summary>
public IReadOnlyCollection<string> MaxSwitchArmsExemptTypes { get; init; }
    = Array.Empty<string>();
```

In `ApplyComplexityLimits` (Methode innerhalb `MetricsConfig`) die drei Properties hinzufügen:

```csharp
private MetricsConfig ApplyComplexityLimits(MetricsConfigOverride o) => this with
{
    MaxCyclomaticComplexity = o.MaxCyclomaticComplexity ?? MaxCyclomaticComplexity,
    MaxCognitiveComplexity = o.MaxCognitiveComplexity ?? MaxCognitiveComplexity,
    MinCognitiveComplexityForTest = o.MinCognitiveComplexityForTest ?? MinCognitiveComplexityForTest,
    AggregatePartialClassLineCount = o.AggregatePartialClassLineCount ?? AggregatePartialClassLineCount,
    ComplexityNearMissTolerance = o.ComplexityNearMissTolerance ?? ComplexityNearMissTolerance,
    ExcludeSwitchDispatcherCases = o.ExcludeSwitchDispatcherCases ?? ExcludeSwitchDispatcherCases,
    SwitchDispatcherMaxCaseBodyLines = o.SwitchDispatcherMaxCaseBodyLines ?? SwitchDispatcherMaxCaseBodyLines,
    // NEU:
    MaxSwitchArms = o.MaxSwitchArms ?? MaxSwitchArms,
    MaxSwitchArmsExcludeDispatcher = o.MaxSwitchArmsExcludeDispatcher ?? MaxSwitchArmsExcludeDispatcher,
    MaxSwitchArmsExemptTypes = o.MaxSwitchArmsExemptTypes ?? MaxSwitchArmsExemptTypes,
};
```

---

### 3.2 `MetricsConfigOverride` — nullable Properties

**Datei:** `src/AiNetLinter/Configuration/LinterConfigOverrides.cs`

Hinter `public int? SwitchDispatcherMaxCaseBodyLines { get; init; }` einfügen:

```csharp
/// <summary>
/// Maximale Anzahl Arms/Labels pro Switch. 0 = deaktiviert. Override für MaxSwitchArms.
/// </summary>
public int? MaxSwitchArms { get; init; }

/// <summary>
/// Override für MaxSwitchArmsExcludeDispatcher.
/// </summary>
public bool? MaxSwitchArmsExcludeDispatcher { get; init; }

/// <summary>
/// Override für MaxSwitchArmsExemptTypes (Typnamen, deren Methoden ausgenommen werden).
/// </summary>
public IReadOnlyCollection<string>? MaxSwitchArmsExemptTypes { get; init; }
```

---

### 3.3 `LinterRuleIds` — neue Konstante

**Datei:** `src/AiNetLinter/Core/LinterRuleIds.cs`

Nach `MaxPublicMembersPerType`:

```csharp
internal const string MaxSwitchArms = nameof(MetricsConfig.MaxSwitchArms);
```

---

### 3.4 `ComplexityChecker` — neue Methode + Aufruf

**Datei:** `src/AiNetLinter/Core/Checkers/ComplexityChecker.cs`

**Schritt 1:** Aufruf in `CheckMethod` ergänzen:

```csharp
internal static void CheckMethod(MethodDeclarationSyntax node, CheckerContext ctx)
{
    var (cc, cogC) = ComputeComplexities(node, ctx);
    CheckParamCount(node, ctx, cc, cogC);
    CheckMethodComplexities(node, ctx, cc, cogC);
    CheckMethodLineCount(node, ctx, cc, cogC);
    CheckSwitchArms(node, ctx);  // NEU
}
```

**Schritt 2:** Neue private Methode (am Ende der Klasse, vor der letzten `}`):

```csharp
private static void CheckSwitchArms(MethodDeclarationSyntax node, CheckerContext ctx)
{
    var limit = ctx.Config.Metrics.MaxSwitchArms;
    if (limit <= 0) return;

    // Dispatcher-Exemption: gesamte Methode ausschließen wenn sie als Dispatcher gilt
    if (ctx.Config.Metrics.MaxSwitchArmsExcludeDispatcher
        && SwitchDispatcherDetector.IsDispatcher(node, ctx.Config.Metrics.SwitchDispatcherMaxCaseBodyLines))
        return;

    // Typ-Exemption: Methoden in bestimmten Klassen/Records ausschließen
    var exemptTypes = ctx.Config.Metrics.MaxSwitchArmsExemptTypes;
    if (exemptTypes != null && exemptTypes.Count > 0)
    {
        var typeName = node.Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .Select(t => t.Identifier.Text)
            .FirstOrDefault() ?? string.Empty;

        if (exemptTypes.Contains(typeName, StringComparer.Ordinal)) return;
    }

    // Switch-Expressions: Arms.Count direkt
    foreach (var switchExpr in node.DescendantNodes().OfType<SwitchExpressionSyntax>())
    {
        var count = switchExpr.Arms.Count;
        if (count > limit)
            ctx.ReportViolation(switchExpr, LinterRuleIds.MaxSwitchArms,
                $"Switch-Expression hat {count} Arms (erlaubt: {limit}).",
                $"Refaktoriere zu einem Dictionary-Dispatch oder extrahiere das Switch in eine dedizierte Dispatcher-Methode. Alternativ: 'MaxSwitchArmsExemptTypes' fuer legitime State-Machines nutzen.");
    }

    // Switch-Statements: Labels zaehlen (nicht Sections — eine Section kann mehrere Labels haben)
    foreach (var switchStmt in node.DescendantNodes().OfType<SwitchStatementSyntax>())
    {
        var count = switchStmt.Sections.SelectMany(s => s.Labels).Count();
        if (count > limit)
            ctx.ReportViolation(switchStmt, LinterRuleIds.MaxSwitchArms,
                $"Switch-Statement hat {count} Labels (erlaubt: {limit}).",
                $"Refaktoriere zu einem Dictionary-Dispatch oder extrahiere das Switch in eine dedizierte Dispatcher-Methode. Alternativ: 'MaxSwitchArmsExemptTypes' fuer legitime State-Machines nutzen.");
    }
}
```

**Benötigte Usings** (sind bereits vorhanden — `System.Linq` und `Microsoft.CodeAnalysis.CSharp.Syntax` bereits importiert in der Datei).

---

## 4. Tests

**Neue Datei:** `src/AiNetLinter.Tests/MaxSwitchArmsTests.cs`

```csharp
#nullable enable

using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

public sealed class MaxSwitchArmsTests
{
    private static SemanticModel CreateSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSemanticModel(tree);
    }

    private static LinterConfig CreateConfig(
        int maxSwitchArms = 10,
        bool excludeDispatcher = true,
        string[]? exemptTypes = null)
    {
        return new LinterConfig
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false,
            },
            Metrics = new MetricsConfig
            {
                MaxSwitchArms = maxSwitchArms,
                MaxSwitchArmsExcludeDispatcher = excludeDispatcher,
                MaxSwitchArmsExemptTypes = exemptTypes ?? [],
                ExcludeSwitchDispatcherCases = true,
                SwitchDispatcherMaxCaseBodyLines = 3,
            }
        };
    }

    [Fact]
    public void SwitchExpression_WithMoreArmsThanLimit_ReportsViolation()
    {
        const string source = @"
public class Order
{
    public string GetLabel(int status) => status switch
    {
        1 => ""A"", 2 => ""B"", 3 => ""C"", 4 => ""D"", 5 => ""E"",
        6 => ""F"", 7 => ""G"", 8 => ""H"", 9 => ""I"", 10 => ""J"",
        11 => ""K"", _ => ""X""
    };
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxSwitchArms: 10));

        Assert.Single(violations, v => v.RuleId == "MaxSwitchArms");
    }

    [Fact]
    public void SwitchExpression_WithExactlyLimit_IsOk()
    {
        const string source = @"
public class Order
{
    public string GetLabel(int status) => status switch
    {
        1 => ""A"", 2 => ""B"", 3 => ""C"", 4 => ""D"", 5 => ""E"",
        6 => ""F"", 7 => ""G"", 8 => ""H"", 9 => ""I"", _ => ""X""
    };
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxSwitchArms: 10));

        Assert.DoesNotContain(violations, v => v.RuleId == "MaxSwitchArms");
    }

    [Fact]
    public void SwitchStatement_LabelsOverLimit_ReportsViolation()
    {
        const string source = @"
public class Router
{
    public int Route(int cmd)
    {
        switch (cmd)
        {
            case 1: return 1;
            case 2: return 2;
            case 3: return 3;
            case 4: return 4;
            case 5: return 5;
            case 6: return 6;
            case 7: return 7;
            case 8: return 8;
            case 9: return 9;
            case 10: return 10;
            case 11: return 11;
            default: return 0;
        }
    }
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxSwitchArms: 10));

        Assert.Single(violations, v => v.RuleId == "MaxSwitchArms");
    }

    [Fact]
    public void DispatcherMethod_WithManyArms_IsExempt_WhenExcludeDispatcherIsTrue()
    {
        const string source = @"
public class Router
{
    public int Route(int cmd) => cmd switch
    {
        1 => HandleA(cmd), 2 => HandleB(cmd), 3 => HandleC(cmd),
        4 => HandleD(cmd), 5 => HandleE(cmd), 6 => HandleF(cmd),
        7 => HandleG(cmd), 8 => HandleH(cmd), 9 => HandleI(cmd),
        10 => HandleJ(cmd), 11 => HandleK(cmd), _ => 0
    };
    private int HandleA(int x) => x;
    private int HandleB(int x) => x;
    private int HandleC(int x) => x;
    private int HandleD(int x) => x;
    private int HandleE(int x) => x;
    private int HandleF(int x) => x;
    private int HandleG(int x) => x;
    private int HandleH(int x) => x;
    private int HandleI(int x) => x;
    private int HandleJ(int x) => x;
    private int HandleK(int x) => x;
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(maxSwitchArms: 10, excludeDispatcher: true));

        Assert.DoesNotContain(violations, v => v.RuleId == "MaxSwitchArms");
    }

    [Fact]
    public void DispatcherMethod_WithManyArms_ReportsViolation_WhenExcludeDispatcherIsFalse()
    {
        const string source = @"
public class Router
{
    public int Route(int cmd) => cmd switch
    {
        1 => HandleA(cmd), 2 => HandleB(cmd), 3 => HandleC(cmd),
        4 => HandleD(cmd), 5 => HandleE(cmd), 6 => HandleF(cmd),
        7 => HandleG(cmd), 8 => HandleH(cmd), 9 => HandleI(cmd),
        10 => HandleJ(cmd), 11 => HandleK(cmd), _ => 0
    };
    private int HandleA(int x) => x;
    private int HandleB(int x) => x;
    private int HandleC(int x) => x;
    private int HandleD(int x) => x;
    private int HandleE(int x) => x;
    private int HandleF(int x) => x;
    private int HandleG(int x) => x;
    private int HandleH(int x) => x;
    private int HandleI(int x) => x;
    private int HandleJ(int x) => x;
    private int HandleK(int x) => x;
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(maxSwitchArms: 10, excludeDispatcher: false));

        Assert.Single(violations, v => v.RuleId == "MaxSwitchArms");
    }

    [Fact]
    public void ExemptType_WithManyArms_IsOk()
    {
        const string source = @"
public class OrderStateMachine
{
    public string Transition(int state) => state switch
    {
        1 => ""A"", 2 => ""B"", 3 => ""C"", 4 => ""D"", 5 => ""E"",
        6 => ""F"", 7 => ""G"", 8 => ""H"", 9 => ""I"", 10 => ""J"",
        11 => ""K"", _ => ""X""
    };
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(maxSwitchArms: 10, exemptTypes: ["OrderStateMachine"]));

        Assert.DoesNotContain(violations, v => v.RuleId == "MaxSwitchArms");
    }

    [Fact]
    public void MaxSwitchArmsZero_Disabled_NoViolation()
    {
        const string source = @"
public class Order
{
    public string GetLabel(int status) => status switch
    {
        1 => ""A"", 2 => ""B"", 3 => ""C"", 4 => ""D"", 5 => ""E"",
        6 => ""F"", 7 => ""G"", 8 => ""H"", 9 => ""I"", 10 => ""J"",
        11 => ""K"", 12 => ""L"", 13 => ""M"", _ => ""X""
    };
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxSwitchArms: 0));

        Assert.DoesNotContain(violations, v => v.RuleId == "MaxSwitchArms");
    }
}
```

---

## 5. Dokumentation

### 5.1 `Docs/configuration.md` — Regel-Tabelle

In der Regeltabelle (Abschnitt „Erklärung der Regeln") nach dem Eintrag für `SwitchDispatcherMaxCaseBodyLines` drei Zeilen ergänzen:

```markdown
| `MaxSwitchArms` | Metrics | Maximale Anzahl Arms in einem Switch-Expression bzw. Labels in einem Switch-Statement pro Methode. `0` = deaktiviert. Empfehlung: `10`. Dispatcher-Methoden (reine Routing-Tabellen) können per `MaxSwitchArmsExcludeDispatcher` ausgenommen werden. |
| `MaxSwitchArmsExcludeDispatcher` | Metrics | Wenn `true` (Standard): Methoden die als Switch-Dispatcher klassifiziert werden (alle Cases sind triviale Einzeiler-Aufrufe), werden von `MaxSwitchArms` ausgenommen. Deckt den Hauptanwendungsfall "Routing-Tabelle mit 15+ Arms" ab. |
| `MaxSwitchArmsExemptTypes` | Metrics | Einfache Typnamen (kein Namespace), deren Methoden von `MaxSwitchArms` komplett ausgenommen werden. Nützlich für State-Machine-Klassen mit vielen legitimen Zuständen (z. B. `["OrderStateMachine"]`). Standard: `[]`. |
```

### 5.2 `Docs/ROADMAP.md`

Den Eintrag für `MaxSwitchArms` (falls vorhanden) als erledigt markieren oder ergänzen.

### 5.3 `README.md`

Keinen separaten README-Eintrag nötig — die Konfigurationstabelle in `configuration.md` ist die primäre Referenz.

---

## 6. Abgrenzung zum bestehenden `ExcludeSwitchDispatcherCases`

| Aspekt | `ExcludeSwitchDispatcherCases` | `MaxSwitchArmsExcludeDispatcher` |
|:-------|:-------------------------------|:---------------------------------|
| Zweck | Reduziert CC/CogC für Dispatcher-Methoden auf 1 | Exemptiert Dispatcher-Methoden vom Arm-Zahl-Check |
| Steuerung | `MetricsConfig.ExcludeSwitchDispatcherCases` (bool) | `MetricsConfig.MaxSwitchArmsExcludeDispatcher` (bool) |
| Defaultmäßig aktiv | Ja | Ja (aber `MaxSwitchArms=0` deaktiviert den Check) |
| Gemeinsame Logik | `SwitchDispatcherDetector.IsDispatcher()` | `SwitchDispatcherDetector.IsDispatcher()` |

Beide Flags können unabhängig voneinander gesetzt werden. Die Dispatcher-Erkennung (`SwitchDispatcherDetector`) wird wiederverwendet — kein neuer Code in der Detector-Klasse.

---

## 7. Empfohlene `rules.json`-Konfiguration

```json
"Metrics": {
  "MaxSwitchArms": 10,
  "MaxSwitchArmsExcludeDispatcher": true,
  "MaxSwitchArmsExemptTypes": []
}
```

Dies entspricht den Standardwerten — Eintrag in `rules.json` nur bei Abweichung nötig. Zum **Deaktivieren**: `"MaxSwitchArms": 0`.

Für State-Machine-Projekte:

```json
"Metrics": {
  "MaxSwitchArms": 10,
  "MaxSwitchArmsExcludeDispatcher": true,
  "MaxSwitchArmsExemptTypes": ["OrderStateMachine", "PaymentStateMachine"]
}
```

Der Auto-Sync-Mechanismus (beim nächsten `ainetlinter --config rules.json ...`) ergänzt die neuen Felder automatisch mit den Standardwerten in bestehenden `rules.json`-Dateien — keine manuelle Migration nötig.

---

## 8. Commit-Vorschlag

```
feat: MaxSwitchArms-Metrik für Switch-Expression/Statement-Armzahl ergänzt

Neue Metrik begrenzt die Anzahl der Arms in Switch-Expressions und Labels in
Switch-Statements pro Methode. Standard 0 (deaktiviert), Empfehlung 10.
Dispatcher-Methoden sind per MaxSwitchArmsExcludeDispatcher (default: true)
automatisch ausgenommen; Typ-Ausnahmen via MaxSwitchArmsExemptTypes möglich.
Wiederverwendet SwitchDispatcherDetector ohne Änderung an der Detector-Klasse.
```

---

## 9. Risiken und Offene Fragen

| Risiko | Bewertung | Maßnahme |
|:-------|:----------|:---------|
| Switch in Lambda/lokaler Funktion | Niedrig — `DescendantNodes()` findet alle; Lambdas in Methoden werden mitgescannt | Kein Handlungsbedarf |
| Switch auf Property-Ebene (nicht in Methode) | Wird nicht gescannt (nur `VisitMethodDeclaration`) | Akzeptiert — praktisch kein realer Fall |
| Nested Switches (Switch in Switch) | Jeder innere Switch wird separat gezählt — korrekt | Kein Handlungsbedarf |
| Performance | `DescendantNodes()` läuft bereits für andere Checks; vernachlässigbar | OK |
