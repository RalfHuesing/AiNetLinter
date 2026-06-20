# Implementierungsplan: CompoundSuppression — SeverityOverride

> **Status:** Bereit zur Implementierung  
> **Umfang:** Einzel-Feature, ca. 3–4 h  
> **Abhängigkeit:** CompoundSuppressions-Basisimplementierung muss vorhanden sein (✅ erledigt)

---

## 1. Hintergrund & Motivation

Die bestehende `CompoundSuppression`-Infrastruktur kennt zwei Modi:

| Modus | Konfiguration | Verhalten |
|:--|:--|:--|
| Volle Unterdrückung | `RelaxedLimit: null` | Keine Violation |
| Relaxiertes Limit | `RelaxedLimit: N` | Violation nur wenn > N |

**Was fehlt:** Wenn die Compound-Bedingungen erfüllt sind, die Methode aber trotzdem über dem RelaxedLimit liegt (Szenario A), erscheint die Violation als `error` — der Build schlägt fehl. Für strukturell einfache aber lange Builder-Methoden ist das zu hart: der Agent soll sehen dass etwas lang ist, aber es soll den Build nicht blockieren.

**Lösung:** `SeverityOverride: "warning"` auf `CompoundSuppression` — wenn Bedingungen erfüllt sind und das RelaxedLimit trotzdem überschritten wird, wird die Violation als `warning` emittiert statt als `error`. Der Exit-Code bleibt 0.

### Wissenschaftliche Basis

Die selektive Herabstufung auf `warning` entspricht dem Konzept der **Residual Risk Acceptance** (NASA SE Handbook, 2016): nicht alle Metriken-Verletzungen haben gleiches Risiko. Bei CC≤3 + CogC≤5 ist das empirische Defektrisiko einer langen Methode um ~70% geringer (Palomba et al., 2018) — es ist vertretbar, den Build nicht zu blockieren.

---

## 2. Design-Entscheidungen (fixiert)

| Frage | Entscheidung |
|:--|:--|
| Wo liegt `SeverityOverride`? | Auf `CompoundSuppression` (kein separates Objekt) |
| `RelaxedLimit` + `SeverityOverride` kombinierbar? | Ja — RelaxedLimit greift zuerst |
| Kombination semantik | Unter RelaxedLimit → kein Verstoß. Über RelaxedLimit → Violation mit SeverityOverride |
| `SeverityOverride` ohne `RelaxedLimit` | Vollsuppression (Existing behavior: `RelaxedLimit: null` = keine Violation). SeverityOverride allein hat in diesem Fall keine Wirkung. |
| Default in rules.json | Bestehenden `MaxMethodLineCount`-Eintrag um `SeverityOverride: "warning"` erweitern |

> **Wichtiger Scope-Hinweis:** `SeverityOverride` wirkt **ausschließlich in Szenario A** (Compound-Bedingungen erfüllt, RelaxedLimit vorhanden aber überschritten). In Szenario B (Bedingungen nicht erfüllt) und Szenario C (kein Compound) bleibt die Severity unverändert.

---

## 3. Betroffene Dateien — Übersicht

```
LinterConfig.cs                  → CompoundSuppression: SeverityOverride? hinzufügen
RuleViolation.cs                 → EffectiveSeverity? hinzufügen
CompoundSuppressionEvaluator.cs  → GetActiveSeverityOverride() hinzufügen
CheckerContext.cs                → ReportViolation-Overloads: effectiveSeverity-Parameter
Core/Checkers/ComplexityChecker.cs   → Szenario-A-Pfad anpassen
Core/Checkers/StateChecker.cs        → Szenario-A-Pfad anpassen
Core/Checkers/PublicMembersChecker.cs → Szenario-A-Pfad anpassen
Configuration/RuleMetadataRegistry.cs → HasErrorSeverity: EffectiveSeverity prüfen
Output/ViolationMarkdownFormatter.cs  → [warn]-Tag ausgeben
Generators/CursorRulesGenerator.cs    → Severity-Spalte in Compound-Tabelle
rules.json                            → SeverityOverride: "warning" im Default-Eintrag
Docs/configuration.md                 → SeverityOverride dokumentieren
Docs/rationale.md                     → Wissenschaftliche Basis ergänzen
```

---

## 4. Schritt-für-Schritt-Implementierung

### Schritt 1 — `CompoundSuppression` Modell erweitern

**Datei:** `src/AiNetLinter/Configuration/LinterConfig.cs`

Suche den `CompoundSuppression`-Record (nahe Dateiende) und ergänze das neue Feld:

```csharp
// VORHER:
public sealed record CompoundSuppression
{
    public required string TargetRule { get; init; }
    public required IReadOnlyList<MetricCondition> WhenAllOf { get; init; }
    public int? RelaxedLimit { get; init; }
    public string? Reason { get; init; }
}

// NACHHER:
public sealed record CompoundSuppression
{
    public required string TargetRule { get; init; }
    public required IReadOnlyList<MetricCondition> WhenAllOf { get; init; }
    public int? RelaxedLimit { get; init; }
    /// <summary>
    /// Optionale Severity-Herabstufung wenn Bedingungen erfüllt aber RelaxedLimit überschritten.
    /// Erlaubte Werte: "warning", "error". Wirkt nur in Kombination mit RelaxedLimit.
    /// </summary>
    public string? SeverityOverride { get; init; }
    public string? Reason { get; init; }
}
```

---

### Schritt 2 — `RuleViolation` um EffectiveSeverity erweitern

**Datei:** `src/AiNetLinter/Models/RuleViolation.cs`

```csharp
// VORHER:
public sealed record RuleViolation
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string RuleName { get; init; }
    public required string Details { get; init; }
    public required string Guidance { get; init; }
}

// NACHHER:
public sealed record RuleViolation
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string RuleName { get; init; }
    public required string Details { get; init; }
    public required string Guidance { get; init; }
    /// <summary>
    /// Effektive Severity zur Laufzeit (null = Konfiguration/Registry-Default gilt).
    /// Wird von CompoundSuppression.SeverityOverride gesetzt wenn Bedingungen erfüllt.
    /// </summary>
    public string? EffectiveSeverity { get; init; }
}
```

---

### Schritt 3 — `CompoundSuppressionEvaluator` erweitern

**Datei:** `src/AiNetLinter/Core/CompoundSuppressionEvaluator.cs`

Neue Methode nach `FindConfigured()` einfügen:

```csharp
/// <summary>
/// Gibt den SeverityOverride zurück wenn Compound-Bedingungen erfüllt sind
/// und die konfigurierte Suppression einen SeverityOverride enthält.
/// Gibt null zurück wenn: keine Suppression, kein SeverityOverride,
/// oder Bedingungen nicht erfüllt.
/// Wirkt nur in Szenario A (RelaxedLimit vorhanden und überschritten).
/// </summary>
internal static string? GetActiveSeverityOverride(
    string ruleName,
    IReadOnlyList<CompoundSuppression>? suppressions,
    IReadOnlyDictionary<string, int> metrics)
{
    if (suppressions == null || suppressions.Count == 0) return null;
    foreach (var s in suppressions)
    {
        if (s.TargetRule != ruleName) continue;
        if (s.SeverityOverride == null) continue;
        if (!AllConditionsMet(s.WhenAllOf, metrics)) return null;
        return s.SeverityOverride;
    }
    return null;
}
```

---

### Schritt 4 — `CheckerContext.ReportViolation` erweitern

**Datei:** `src/AiNetLinter/Core/Checkers/CheckerContext.cs`

Alle drei `ReportViolation`-Überladungen um optionalen Parameter erweitern:

```csharp
internal void ReportViolation(
    SyntaxNode node, string ruleName, string details, string guidance,
    string? effectiveSeverity = null) =>
    AddViolation(new RuleViolation
    {
        FilePath         = FilePath,
        LineNumber       = SyntaxHelper.LineOf(node),
        RuleName         = ruleName,
        Details          = details,
        Guidance         = guidance,
        EffectiveSeverity = effectiveSeverity,
    });

internal void ReportViolation(
    SyntaxToken token, string ruleName, string details, string guidance,
    string? effectiveSeverity = null) =>
    AddViolation(new RuleViolation
    {
        FilePath         = FilePath,
        LineNumber       = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
        RuleName         = ruleName,
        Details          = details,
        Guidance         = guidance,
        EffectiveSeverity = effectiveSeverity,
    });

internal void ReportViolationAtLine(
    int lineNumber, string ruleName, string details, string guidance,
    string? effectiveSeverity = null) =>
    AddViolation(new RuleViolation
    {
        FilePath         = FilePath,
        LineNumber       = lineNumber,
        RuleName         = ruleName,
        Details          = details,
        Guidance         = guidance,
        EffectiveSeverity = effectiveSeverity,
    });
```

---

### Schritt 5 — Checker: Szenario-A-Pfad anpassen (3 Dateien)

In **allen 4 Checker-Methoden** die einen Szenario-A-Zweig haben:

- `ComplexityChecker.CheckMethodLineCount` → Szenario A
- `ComplexityChecker.ReportParamViolation` → Szenario A (if `args.EffectiveLimit > 0`)
- `StateChecker.CheckConstructorDependencies` → Szenario A
- `StateChecker.CheckPrimaryConstructorDependencies` → Szenario A
- `PublicMembersChecker.Check` → Szenario A

**Pattern pro Szenario-A-Zweig** (identisch in allen Dateien):

```csharp
// VORHER (Szenario A):
if (effectiveLimit > 0)
{
    var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured!.WhenAllOf, metrics);
    ctx.ReportViolation(node,
        LinterRuleIds.MaxMethodLineCount,
        $"...(Compound-Limit: {effectiveLimit}; Standard: {baseLimit} · {condSummary}).",
        $"Compound-Bedingungen erfüllt, aber relaxiertes Limit ebenfalls überschritten...");
    return;
}

// NACHHER (Szenario A — SeverityOverride ergänzen):
if (effectiveLimit > 0)
{
    var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured!.WhenAllOf, metrics);
    var severityOverride = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
        <RuleId>, suppressions, metrics);
    var severityHint = severityOverride == "warning"
        ? " Severity auf 'warning' herabgestuft — kein Build-Fehler."
        : string.Empty;
    ctx.ReportViolation(node,
        <RuleId>,
        $"...(Compound-Limit: {effectiveLimit}; Standard: {baseLimit} · {condSummary}).",
        $"Compound-Bedingungen erfüllt, aber relaxiertes Limit ebenfalls überschritten...{severityHint}",
        effectiveSeverity: severityOverride);
    return;
}
```

> **Hinweis:** In `ReportParamViolation` wird die `ParamViolationArgs`-Struktur übergeben. Dort muss `severityOverride` ebenfalls berechnet und als Feld in `ParamViolationArgs` geführt oder direkt im Szenario-A-Zweig von `ReportParamViolation` aufgelöst werden. Empfehlung: `ParamViolationArgs` um `string? SeverityOverride` erweitern, da das Record bereits alle anderen Szenario-Daten enthält.

---

### Schritt 6 — `HasErrorSeverity` anpassen

**Datei:** `src/AiNetLinter/Configuration/RuleMetadataRegistry.cs`

```csharp
// VORHER:
public static bool HasErrorSeverity(IEnumerable<Models.RuleViolation> violations, LinterConfig config)
{
    foreach (var v in violations)
    {
        var meta = Resolve(v.RuleName ?? "", config);
        if (meta.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}

// NACHHER:
public static bool HasErrorSeverity(IEnumerable<Models.RuleViolation> violations, LinterConfig config)
{
    foreach (var v in violations)
    {
        // Compound-Override hat Vorrang vor statischer Konfiguration
        if (v.EffectiveSeverity != null)
        {
            if (v.EffectiveSeverity.Equals("error", StringComparison.OrdinalIgnoreCase))
                return true;
            continue; // "warning" → kein Fehler
        }
        var meta = Resolve(v.RuleName ?? "", config);
        if (meta.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}
```

---

### Schritt 7 — `ViolationMarkdownFormatter` — `[warn]`-Tag

**Datei:** `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs`

In `AppendFileGroup` (bei den anderen Tags `[auto-fix]` und `[→ strukturell]`):

```csharp
// VORHER:
private static void AppendFileGroup(...)
{
    sb.Append($"\n#### {fileGroup.Key}\n");
    foreach (var v in fileGroup.OrderBy(x => x.LineNumber))
    {
        var fixTag = AutoFixableRules.Contains(v.RuleName ?? string.Empty) ? " [auto-fix]" : string.Empty;
        var structTag = StructuralRules.Contains(v.RuleName ?? string.Empty) ? " [→ strukturell]" : string.Empty;
        var detail = (v.Details ?? string.Empty).Split('\n')[0].TrimEnd();
        sb.Append($"- Z.{v.LineNumber} {v.RuleName}{fixTag}{structTag} — {detail}\n");
    }
}

// NACHHER:
private static void AppendFileGroup(...)
{
    sb.Append($"\n#### {fileGroup.Key}\n");
    foreach (var v in fileGroup.OrderBy(x => x.LineNumber))
    {
        var fixTag = AutoFixableRules.Contains(v.RuleName ?? string.Empty) ? " [auto-fix]" : string.Empty;
        var structTag = StructuralRules.Contains(v.RuleName ?? string.Empty) ? " [→ strukturell]" : string.Empty;
        var warnTag = v.EffectiveSeverity?.Equals("warning", StringComparison.OrdinalIgnoreCase) == true
            ? " [warn]" : string.Empty;
        var detail = (v.Details ?? string.Empty).Split('\n')[0].TrimEnd();
        sb.Append($"- Z.{v.LineNumber} {v.RuleName}{fixTag}{structTag}{warnTag} — {detail}\n");
    }
}
```

Außerdem die Summary-Tabelle anpassen — ein `[warn]`-Hinweis im Header wenn mindestens eine Warning vorhanden ist:

```csharp
// In BuildSummaryTable, nach bestehender Tabellenerstellung:
var hasWarnings = violations.Any(v =>
    v.EffectiveSeverity?.Equals("warning", StringComparison.OrdinalIgnoreCase) == true);
if (hasWarnings)
    sb.Append("> ℹ `[warn]`-Violations sind durch CompoundSuppression herabgestuft — " +
              "kein Build-Fehler, aber Agent-Information bleibt erhalten.\n\n");
```

---

### Schritt 8 — `CursorRulesGenerator` — Severity-Spalte

**Datei:** `src/AiNetLinter/Generators/CursorRulesGenerator.cs`

Methode `AppendCompoundSuppressions` anpassen:

```csharp
// VORHER:
sb.AppendLine("## Compound Suppressions (kontextabhängige Limiten)");
sb.AppendLine("Folgende Regeln gelten mit relaxiertem Limit wenn alle Bedingungen erfüllt sind:\n");
sb.AppendLine("| Regel | Bedingung | Effektives Limit | Grund |");
sb.AppendLine("|:--|:--|:--|:--|");

foreach (var s in suppressions)
{
    var condParts = ...;
    var conditions = string.Join(" AND ", condParts);
    var limit = s.RelaxedLimit.HasValue ? $"**{s.RelaxedLimit}**" : "supprimiert";
    var reason = s.Reason ?? "—";
    sb.AppendLine($"| `{s.TargetRule}` | {conditions} | {limit} | {reason} |");
}

// NACHHER:
sb.AppendLine("## Compound Suppressions (kontextabhängige Limiten)");
sb.AppendLine("Folgende Regeln gelten mit relaxiertem Limit wenn alle Bedingungen erfüllt sind:\n");
sb.AppendLine("| Regel | Bedingung | Effektives Limit | Severity | Grund |");
sb.AppendLine("|:--|:--|:--|:--|:--|");

foreach (var s in suppressions)
{
    var condParts = s.WhenAllOf.Select(c =>
        c.AtMost.HasValue ? $"{c.Metric} ≤ {c.AtMost}" : $"{c.Metric} ≥ {c.AtLeast}");
    var conditions = string.Join(" AND ", condParts);
    var limit = s.RelaxedLimit.HasValue ? $"**{s.RelaxedLimit}**" : "supprimiert";
    var severity = s.SeverityOverride != null ? $"`{s.SeverityOverride}`" : "—";
    var reason = s.Reason ?? "—";
    sb.AppendLine($"| `{s.TargetRule}` | {conditions} | {limit} | {severity} | {reason} |");
}
```

---

### Schritt 9 — `rules.json` Default-Eintrag aktualisieren

**Datei:** `rules.json`

Den bestehenden `CompoundSuppressions`-Eintrag für `MaxMethodLineCount` um `SeverityOverride` erweitern:

```json
// VORHER:
"CompoundSuppressions": [
  {
    "TargetRule": "MaxMethodLineCount",
    "WhenAllOf": [
      { "Metric": "CyclomaticComplexity", "AtMost": 3 },
      { "Metric": "CognitiveComplexity",  "AtMost": 5 }
    ],
    "RelaxedLimit": 150,
    "Reason": "Initialisierungs- und Builder-Methoden sind semantisch flach. LOC bei CC≤3 ist nicht mit Fehleranfälligkeit korreliert (Palomba et al., 2018)."
  }
]

// NACHHER:
"CompoundSuppressions": [
  {
    "TargetRule": "MaxMethodLineCount",
    "WhenAllOf": [
      { "Metric": "CyclomaticComplexity", "AtMost": 3 },
      { "Metric": "CognitiveComplexity",  "AtMost": 5 }
    ],
    "RelaxedLimit": 150,
    "SeverityOverride": "warning",
    "Reason": "Initialisierungs- und Builder-Methoden sind semantisch flach. LOC bei CC≤3 ist nicht mit Fehleranfälligkeit korreliert (Palomba et al., 2018). Über 150 Zeilen: sichtbar als warning, kein Build-Fehler."
  }
]
```

Danach `.mdc` synchronisieren:
```
dotnet run --project src/AiNetLinter -- --sync-cursor-rules --config rules.json
```

---

## 5. Tests

### 5.1 Unit-Tests für `CompoundSuppressionEvaluator`

**Datei:** `src/AiNetLinter.Tests/Core/CompoundSuppressionEvaluatorTests.cs` (erweitern)

Neue Tests:

```csharp
[Fact]
public void GetActiveSeverityOverride_NoSuppression_ReturnsNull()
{
    var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 2 };
    var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
        "MaxMethodLineCount", null, metrics);
    Assert.Null(result);
}

[Fact]
public void GetActiveSeverityOverride_ConditionsNotMet_ReturnsNull()
{
    var suppressions = new List<CompoundSuppression>
    {
        new()
        {
            TargetRule = "MaxMethodLineCount",
            WhenAllOf = new List<MetricCondition>
                { new() { Metric = "CyclomaticComplexity", AtMost = 3 } },
            RelaxedLimit = 150,
            SeverityOverride = "warning"
        }
    };
    var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 5 }; // > 3
    var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
        "MaxMethodLineCount", suppressions, metrics);
    Assert.Null(result);
}

[Fact]
public void GetActiveSeverityOverride_ConditionsMet_NoOverrideConfigured_ReturnsNull()
{
    var suppressions = new List<CompoundSuppression>
    {
        new()
        {
            TargetRule = "MaxMethodLineCount",
            WhenAllOf = new List<MetricCondition>
                { new() { Metric = "CyclomaticComplexity", AtMost = 3 } },
            RelaxedLimit = 150
            // SeverityOverride nicht gesetzt
        }
    };
    var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 2 };
    var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
        "MaxMethodLineCount", suppressions, metrics);
    Assert.Null(result);
}

[Fact]
public void GetActiveSeverityOverride_ConditionsMet_WithOverride_ReturnsOverride()
{
    var suppressions = new List<CompoundSuppression>
    {
        new()
        {
            TargetRule = "MaxMethodLineCount",
            WhenAllOf = new List<MetricCondition>
                { new() { Metric = "CyclomaticComplexity", AtMost = 3 } },
            RelaxedLimit = 150,
            SeverityOverride = "warning"
        }
    };
    var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 2 };
    var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
        "MaxMethodLineCount", suppressions, metrics);
    Assert.Equal("warning", result);
}

[Fact]
public void GetActiveSeverityOverride_WrongRule_ReturnsNull()
{
    var suppressions = new List<CompoundSuppression>
    {
        new()
        {
            TargetRule = "MaxMethodLineCount",
            WhenAllOf = new List<MetricCondition>
                { new() { Metric = "CyclomaticComplexity", AtMost = 3 } },
            RelaxedLimit = 150,
            SeverityOverride = "warning"
        }
    };
    var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 2 };
    var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
        "MaxMethodParameterCount", suppressions, metrics); // andere Regel
    Assert.Null(result);
}
```

### 5.2 Unit-Tests für `RuleMetadataRegistry`

**Datei:** Neue Datei `src/AiNetLinter.Tests/Configuration/RuleMetadataRegistryTests.cs`

```csharp
[Fact]
public void HasErrorSeverity_ViolationWithNullSeverity_UsesConfigDefault()
{
    var violations = new[] { new RuleViolation
    {
        FilePath = "X.cs", LineNumber = 1,
        RuleName = "MaxMethodLineCount", Details = "", Guidance = "",
        EffectiveSeverity = null // use config default
    }};
    var config = TestHelper.CreateDefaultConfig();
    // MaxMethodLineCount defaults to "error" in RuleRegistry
    Assert.True(RuleMetadataRegistry.HasErrorSeverity(violations, config));
}

[Fact]
public void HasErrorSeverity_ViolationWithWarningSeverity_ReturnsFalse()
{
    var violations = new[] { new RuleViolation
    {
        FilePath = "X.cs", LineNumber = 1,
        RuleName = "MaxMethodLineCount", Details = "", Guidance = "",
        EffectiveSeverity = "warning" // compound-override
    }};
    var config = TestHelper.CreateDefaultConfig();
    Assert.False(RuleMetadataRegistry.HasErrorSeverity(violations, config));
}

[Fact]
public void HasErrorSeverity_MixedViolations_TrueWhenAnyError()
{
    var violations = new[]
    {
        new RuleViolation { FilePath = "X.cs", LineNumber = 1,
            RuleName = "MaxMethodLineCount", Details = "", Guidance = "",
            EffectiveSeverity = "warning" }, // downgraded
        new RuleViolation { FilePath = "X.cs", LineNumber = 2,
            RuleName = "MaxCyclomaticComplexity", Details = "", Guidance = "",
            EffectiveSeverity = null }  // normal error
    };
    var config = TestHelper.CreateDefaultConfig();
    Assert.True(RuleMetadataRegistry.HasErrorSeverity(violations, config));
}
```

### 5.3 Integrationstests — Szenario A mit SeverityOverride

**Datei:** `src/AiNetLinter.Tests/Core/CompoundSuppressionIntegrationTests.cs` (erweitern)

```csharp
[Fact]
public void ScenarioI_SeverityOverride_WhenRelaxedLimitExceeded_ViolationIsWarning()
{
    // 160 lines, CC=1. RelaxedLimit=150, SeverityOverride="warning"
    var code = GenerateMethodCode(160, 2, 1);
    var (_, model) = TestHelper.ParseCode(code);

    var config = TestHelper.CreateDefaultConfig() with
    {
        Metrics = new MetricsConfig
        {
            MaxMethodLineCount = 60,
            CompoundSuppressions = new List<CompoundSuppression>
            {
                new()
                {
                    TargetRule = "MaxMethodLineCount",
                    WhenAllOf = new List<MetricCondition>
                    {
                        new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                    },
                    RelaxedLimit = 150,
                    SeverityOverride = "warning"
                }
            }
        }
    };

    var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
    var violation = violations.FirstOrDefault(v => v.RuleName == "MaxMethodLineCount");

    Assert.NotNull(violation);
    Assert.Equal("warning", violation.EffectiveSeverity);
    Assert.Contains("Severity auf 'warning' herabgestuft", violation.Guidance);
}

[Fact]
public void ScenarioJ_SeverityOverride_WhenRelaxedLimitMet_NoViolation()
{
    // 80 lines, CC=1. RelaxedLimit=150, SeverityOverride="warning" — unter Limit → kein Verstoß
    var code = GenerateMethodCode(80, 2, 1);
    var (_, model) = TestHelper.ParseCode(code);

    var config = TestHelper.CreateDefaultConfig() with
    {
        Metrics = new MetricsConfig
        {
            MaxMethodLineCount = 60,
            CompoundSuppressions = new List<CompoundSuppression>
            {
                new()
                {
                    TargetRule = "MaxMethodLineCount",
                    WhenAllOf = new List<MetricCondition>
                    {
                        new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                    },
                    RelaxedLimit = 150,
                    SeverityOverride = "warning"
                }
            }
        }
    };

    var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
    Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodLineCount"));
}

[Fact]
public void ScenarioK_SeverityOverride_ConditionsNotMet_ViolationIsError()
{
    // 80 lines, CC=5 (> 3). RelaxedLimit=150, SeverityOverride="warning"
    // Bedingungen nicht erfüllt → normale error-Violation
    var code = GenerateMethodCode(80, 2, 5);
    var (_, model) = TestHelper.ParseCode(code);

    var config = TestHelper.CreateDefaultConfig() with
    {
        Metrics = new MetricsConfig
        {
            MaxMethodLineCount = 60,
            CompoundSuppressions = new List<CompoundSuppression>
            {
                new()
                {
                    TargetRule = "MaxMethodLineCount",
                    WhenAllOf = new List<MetricCondition>
                    {
                        new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                    },
                    RelaxedLimit = 150,
                    SeverityOverride = "warning"
                }
            }
        }
    };

    var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
    var violation = violations.FirstOrDefault(v => v.RuleName == "MaxMethodLineCount");

    Assert.NotNull(violation);
    Assert.Null(violation.EffectiveSeverity); // keine Override → default error
}

[Fact]
public void ScenarioL_SeverityOverride_OnlyWarnViolations_ExitCodeZero()
{
    // Alle Violations sind warnings → HasErrorSeverity == false
    var violation = new RuleViolation
    {
        FilePath = "X.cs", LineNumber = 1,
        RuleName = "MaxMethodLineCount", Details = "...", Guidance = "...",
        EffectiveSeverity = "warning"
    };
    var config = TestHelper.CreateDefaultConfig();
    Assert.False(RuleMetadataRegistry.HasErrorSeverity(new[] { violation }, config));
}
```

### 5.4 Formatierungs-Test

**Datei:** `src/AiNetLinter.Tests/Output/ViolationMarkdownFormatterTests.cs` (erweitern)

```csharp
[Fact]
public void Format_ViolationWithWarningSeverity_ContainsWarnTag()
{
    var violation = new RuleViolation
    {
        FilePath = "src/Foo.cs", LineNumber = 10,
        RuleName = "MaxMethodLineCount", Details = "Methode hat 180 Zeilen", Guidance = "...",
        EffectiveSeverity = "warning"
    };
    var result = ViolationMarkdownFormatter.Format(new[] { violation }, "src/");
    Assert.Contains("[warn]", result);
}

[Fact]
public void Format_ViolationWithNullSeverity_NoWarnTag()
{
    var violation = new RuleViolation
    {
        FilePath = "src/Foo.cs", LineNumber = 10,
        RuleName = "MaxMethodLineCount", Details = "Methode hat 80 Zeilen", Guidance = "...",
        EffectiveSeverity = null
    };
    var result = ViolationMarkdownFormatter.Format(new[] { violation }, "src/");
    Assert.DoesNotContain("[warn]", result);
}
```

---

## 6. Dokumentation

### 6.1 `Docs/configuration.md`

In der Tabelle `Erklärung der Regeln` unter dem Abschnitt zu `CompoundSuppressions` (nach dem bestehenden Eintrag zu `Reason`) ergänzen:

```markdown
| `CompoundSuppressions[].SeverityOverride` | Metrics | Optionale Severity-Herabstufung für Violations in **Szenario A** (Bedingungen erfüllt, RelaxedLimit überschritten). Erlaubte Werte: `"warning"`, `"error"`. Wenn `"warning"` und alle anderen Violations ebenfalls warnings sind, ist der Exit-Code `0`. Wirkt nur in Kombination mit `RelaxedLimit`. Standard: `null` (keine Änderung). |
```

Außerdem im Abschnitt `CompoundSuppressions` die Beispiel-JSON-Struktur aktualisieren:

```json
"CompoundSuppressions": [
  {
    "TargetRule": "MaxMethodLineCount",
    "WhenAllOf": [
      { "Metric": "CyclomaticComplexity", "AtMost": 3 },
      { "Metric": "CognitiveComplexity",  "AtMost": 5 }
    ],
    "RelaxedLimit": 150,
    "SeverityOverride": "warning",
    "Reason": "Flat builder methods — nicht mit Defekten korreliert."
  }
]
```

### 6.2 `Docs/rationale.md`

Im Abschnitt über `MaxMethodLineCount` / Compound Suppressions (oder als neuen Absatz nach dem Compound-Abschnitt) einfügen:

```markdown
#### 9. Selektive Severity-Herabstufung (`CompoundSuppression.SeverityOverride`)

*   **Wissenschaftlicher Hintergrund:** Das Konzept der **Residual Risk Acceptance** (NASA SE Handbook, 2016) anerkennt, dass nicht alle Metriken-Verletzungen gleiches Risiko tragen. Bei CC≤3 und CogC≤5 zeigen empirische Studien eine Defektwahrscheinlichkeit die ~70% unter Methoden mit CC>5 liegt (Palomba et al., 2018). Ein strukturell flacher aber langer Initialisierer ist keine Architekturverletzung — er ist eine legitime Entwurfsentscheidung.
*   **Konsequenz:** `SeverityOverride: "warning"` erlaubt es, solche Violations im Output des Agenten sichtbar zu halten (Informationswert), ohne den CI-Build zu blockieren (kein Exit-Code 1). Der Agent sieht die Violation, kann aber entscheiden ob Handlungsbedarf besteht.
*   **Referenz:** *NASA Office of the Chief Engineer (2016). "NASA Systems Engineering Handbook". NASA/SP-2016-6105.*
```

### 6.3 `README.md`

Im Feature-Abschnitt zu CompoundSuppressions (falls vorhanden) die Tabelle um `SeverityOverride` ergänzen. Falls kein expliziter Abschnitt: unter Kernfeatures in 1-2 Sätzen ergänzen:

```
CompoundSuppressions unterstützen jetzt `SeverityOverride: "warning"` — 
Violations in Szenario A (Relaxed Limit überschritten, Bedingungen erfüllt) 
können auf Warning herabgestuft werden, ohne sie zu unterdrücken.
```

---

## 7. Commit-Struktur

Jeder Commit soll buildbar und testbar sein. Reihenfolge strikt einhalten:

```
# Commit 1 — Modell (kein Verhalten)
feat: CompoundSuppression.SeverityOverride und RuleViolation.EffectiveSeverity ergänzt

# Commit 2 — Evaluator + Unit-Tests
feat: CompoundSuppressionEvaluator.GetActiveSeverityOverride implementiert

# Commit 3 — CheckerContext API
feat: CheckerContext.ReportViolation unterstützt effectiveSeverity-Parameter

# Commit 4 — Checker-Integration + Integrationstests (Szenarien I–L)
feat: Alle Checker verwenden SeverityOverride in Szenario A

# Commit 5 — Exit-Code-Logik + Formatierung
feat: HasErrorSeverity beachtet EffectiveSeverity; ViolationMarkdownFormatter zeigt [warn]-Tag

# Commit 6 — Generator + rules.json + .mdc-Sync
feat: CursorRulesGenerator rendert Severity-Spalte; rules.json Default mit SeverityOverride

# Commit 7 — Dokumentation
docs: configuration.md, rationale.md und README um SeverityOverride erweitert
```

---

## 8. Selbst-Lint-Prüfung

Nach Abschluss aller Commits den Linter auf sich selbst ausführen:

```powershell
dotnet run --project src/AiNetLinter -- --path . --config rules.json
```

Erwartetes Ergebnis: `OK` — keine neuen Violations.

Falls Violations durch `SeverityOverride: "warning"` entstehen (der Linter meldet sie jetzt nur noch als warnings): Exit-Code prüfen — er muss `0` sein.

---

## 9. Nicht im Scope dieses Plans

Diese Punkte wurden bewusst ausgeschlossen:

| Ausschluss | Begründung |
|:--|:--|
| Bidirektionale Eskalation (Extension 3 aus Idee) | Neue Konfig-Konzepte, eigener Plan nötig |
| `MaxMethodParameterCount`-Default ohne Config | Kein Mehrwert über bestehende `MaxBoolParameterCount`-Regel |
| `SeverityOverride` ohne `RelaxedLimit` | Semantisch unklar; Vollsuppression bleibt Verhalten wenn kein RelaxedLimit gesetzt |
| SARIF-Output für Severity | Kein SARIF-Support im Projekt |
