# Plan 06 — P2: Komplexitäts-Regeln — Switch-Dispatcher-Exemption und Near-Miss-Toleranz

**Priorität:** P2  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

### Problem A — Switch-Dispatcher

Methoden die nur als Routing-Tabelle fungieren, werden durch McCabe/Sonar-Metriken bestraft, obwohl jeder Case trivial ist:

```csharp
// ExecuteCommandAsync — 15 Cases à 1–2 Zeilen
// McCabe-Komplexität: 16 (weit über Limit 5)
// Aber: jeder Case ist ein trivialer Delegationsaufruf
public Task<Result> ExecuteCommandAsync(string cmd, Params p)
{
    if (cmd == "extend-item")  return Task.FromResult(HandleExtendItem(p));
    if (cmd == "move-item")    return Task.FromResult(HandleMoveItem(p));
    if (cmd == "delete-item")  return Task.FromResult(HandleDeleteItem(p));
    // ... 12 weitere triviale Cases
    return base.ExecuteCommandAsync(cmd, p);
}
```

Diese Methode ist für LLM-Agenten **sehr gut lesbar** — eine lesbare Routing-Tabelle. Sie zu extrahieren würde sie unlesbarer machen.

### Problem B — Near-Miss-Tolerance

Viele Verstöße sind `6 bei Limit 5` oder `9 bei Limit 8` — nur 1 Punkt über der Grenze. Ein Error für eine Grenzfall-Methode signalisiert das gleiche wie für eine wirklich komplexe Methode (Komplexität 20). Das verzerrt das Signal.

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterAnalyzer.Complexity.cs` | Zeile 62–92 — `CheckMethodComplexities()` |
| `src/AiNetLinter/Metrics/ComplexityCalculator.cs` | Ganzes File (Komplexitäts-Berechnung) |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `MetricsConfig` |
| `src/AiNetLinter/Models/RuleViolation.cs` | ggf. Severity-Feld ergänzen |
| `rules.json` | `Metrics`-Sektion |

---

## Konfigurationsänderung

### `rules.json`:
```json
"Metrics": {
  "MaxCyclomaticComplexity": 5,
  "MaxCognitiveComplexity": 5,
  "ComplexityNearMissTolerance": 1,
  "ExcludeSwitchDispatcherCases": true,
  "SwitchDispatcherMaxCaseBodyLines": 3
}
```

- `ComplexityNearMissTolerance`: Wenn Komplexität im Bereich `(Limit, Limit + Toleranz]`, nur **Warning** statt Error
- `ExcludeSwitchDispatcherCases`: Erkannte Dispatcher-Methoden aus Komplexitätszählung ausnehmen
- `SwitchDispatcherMaxCaseBodyLines`: Max. Zeilen pro Case damit er als „trivial" gilt (Default: 3)

---

## Implementierungsvorschlag

### `LinterConfig.cs` — `MetricsConfig` erweitern:

```csharp
public sealed record MetricsConfig
{
    // ...
    /// <summary>
    /// Toleranzbereich über dem Komplexitätslimit für Warning statt Error.
    /// Bei 0 (Default): alle Überschreitungen sind Error.
    /// Bei 1: Wert im Bereich (Limit, Limit+1] → Warning; darüber → Error.
    /// </summary>
    public int ComplexityNearMissTolerance { get; init; } = 0;

    /// <summary>
    /// Switch-Dispatcher-Methoden aus der Komplexitätsmessung ausnehmen.
    /// Dispatcher: Methode deren Cases alle ≤ SwitchDispatcherMaxCaseBodyLines Zeilen sind
    /// und nur return/await + Methodenaufruf enthalten.
    /// </summary>
    public bool ExcludeSwitchDispatcherCases { get; init; } = false;

    /// <summary>
    /// Max. Code-Zeilen pro Case/If-Zweig damit er als Dispatcher-Zweig gilt.
    /// </summary>
    public int SwitchDispatcherMaxCaseBodyLines { get; init; } = 3;
}
```

### `LinterAnalyzer.Complexity.cs` — `CheckMethodComplexities()` anpassen (Zeile 62):

```csharp
private void CheckMethodComplexities(MethodDeclarationSyntax node)
{
    // Switch-Dispatcher: Komplexität ggf. neuberechnen
    var isDispatcher = _config.Metrics.ExcludeSwitchDispatcherCases
        && SwitchDispatcherDetector.IsDispatcher(node, _config.Metrics.SwitchDispatcherMaxCaseBodyLines);

    var cyclomaticComplexity = isDispatcher
        ? SwitchDispatcherDetector.GetAdjustedCyclomaticComplexity(node)
        : ComplexityCalculator.GetCyclomaticComplexity(node);

    ReportComplexityIfViolation(
        node,
        cyclomaticComplexity,
        _config.Metrics.MaxCyclomaticComplexity,
        nameof(_config.Metrics.MaxCyclomaticComplexity),
        "Zyklomatische Komplexitaet");

    var cognitiveComplexity = isDispatcher
        ? SwitchDispatcherDetector.GetAdjustedCognitiveComplexity(node)
        : ComplexityCalculator.GetCognitiveComplexity(node);

    ReportComplexityIfViolation(
        node,
        cognitiveComplexity,
        _config.Metrics.MaxCognitiveComplexity,
        nameof(_config.Metrics.MaxCognitiveComplexity),
        "Kognitive Komplexitaet");
}

private void ReportComplexityIfViolation(
    MethodDeclarationSyntax node,
    int complexity,
    int limit,
    string ruleName,
    string label)
{
    if (complexity <= limit) return;

    var tolerance = _config.Metrics.ComplexityNearMissTolerance;
    var isNearMiss = tolerance > 0 && complexity <= limit + tolerance;

    // Near-Miss: nur als Hinweis im Details-Text markieren
    // (Severity-Unterschied wird im Report sichtbar — wenn RuleMetadata Severity unterstützt)
    var nearMissHint = isNearMiss ? " [near-miss: knapp über Limit]" : "";

    _violations.Add(new RuleViolation
    {
        FilePath = _filePath,
        LineNumber = GetLineNumber(node),
        RuleName = ruleName,
        Details = $"Die Methode '{node.Identifier.Text}' hat eine {label} von {complexity} " +
                  $"(erlaubt sind maximal {limit}).{nearMissHint}",
        Guidance = /* bestehende Guidance */,
    });
}
```

### Neue Klasse `SwitchDispatcherDetector.cs` (neu in `Metrics/`):

```csharp
// src/AiNetLinter/Metrics/SwitchDispatcherDetector.cs
#nullable enable

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Metrics;

namespace AiNetLinter.Metrics;

/// <summary>
/// Erkennt Switch-Dispatcher-Methoden: Methoden die nur als Routing-Tabelle
/// fungieren und deren Cases alle trivial (Methodenaufruf + return) sind.
/// </summary>
internal static class SwitchDispatcherDetector
{
    /// <summary>
    /// Gibt true zurück wenn die Methode als Switch-Dispatcher klassifiziert wird.
    /// </summary>
    public static bool IsDispatcher(MethodDeclarationSyntax node, int maxCaseBodyLines)
    {
        if (node.Body == null) return false;

        var statements = node.Body.Statements;
        if (statements.Count == 0) return false;

        // Prüfe: Alle Statements sind if-Statements auf dieselbe Variable
        // ODER ein switch-Statement mit trivialen Cases
        return AllStatementsAreTrivialBranches(statements, maxCaseBodyLines);
    }

    private static bool AllStatementsAreTrivialBranches(
        SyntaxList<StatementSyntax> statements,
        int maxCaseBodyLines)
    {
        // Mindestens 3 Branches damit es als Dispatcher gilt
        int branchCount = 0;

        foreach (var stmt in statements)
        {
            if (stmt is IfStatementSyntax ifStmt)
            {
                if (!IsTrivialBranch(ifStmt.Statement, maxCaseBodyLines)) return false;
                if (ifStmt.Else != null && !IsTrivialBranch(ifStmt.Else.Statement, maxCaseBodyLines))
                    return false;
                branchCount++;
            }
            else if (stmt is SwitchStatementSyntax switchStmt)
            {
                foreach (var section in switchStmt.Sections)
                {
                    if (!IsTrivialSwitchSection(section, maxCaseBodyLines)) return false;
                    branchCount++;
                }
            }
            else if (stmt is ReturnStatementSyntax)
            {
                // Abschließendes return (Fallback) ist erlaubt
            }
            else
            {
                return false; // Anderes Statement → kein reiner Dispatcher
            }
        }

        return branchCount >= 3;
    }

    private static bool IsTrivialBranch(StatementSyntax stmt, int maxLines)
    {
        // Return-Statement mit Methodenaufruf = trivial
        if (stmt is ReturnStatementSyntax retStmt)
            return retStmt.Expression is InvocationExpressionSyntax
                or AwaitExpressionSyntax
                or MemberAccessExpressionSyntax;

        // Block mit wenigen Zeilen = trivial
        if (stmt is BlockSyntax block)
        {
            var lineCount = MethodLineCounter.GetCodeLineCount(block);
            return lineCount <= maxLines
                && block.Statements.All(s => s is ReturnStatementSyntax
                    or ExpressionStatementSyntax);
        }

        return false;
    }

    private static bool IsTrivialSwitchSection(SwitchSectionSyntax section, int maxLines)
    {
        var stmts = section.Statements;
        if (stmts.Count > maxLines) return false;
        return stmts.All(s => s is ReturnStatementSyntax
            or BreakStatementSyntax
            or ExpressionStatementSyntax);
    }

    /// <summary>
    /// Berechnet die angepasste McCabe-Komplexität ohne Dispatcher-Branches.
    /// Gibt 1 zurück (Basis-Komplexität der Methode selbst).
    /// </summary>
    public static int GetAdjustedCyclomaticComplexity(MethodDeclarationSyntax node) => 1;

    /// <summary>
    /// Berechnet die angepasste Kognitive Komplexität ohne Dispatcher-Branches.
    /// </summary>
    public static int GetAdjustedCognitiveComplexity(MethodDeclarationSyntax node) => 1;
}
```

---

## Tests

**Datei:** `src/AiNetLinter.Tests/Core/ComplexityDispatcherTests.cs` (neu)

### Switch-Dispatcher — darf NICHT feuern:
```csharp
// ExcludeSwitchDispatcherCases: true, MaxCyclomaticComplexity: 5
// 10 If-Cases — alle trivial (1 Zeile return + Methodenaufruf)
public Task<Result> Execute(string cmd, Params p)
{
    if (cmd == "a") return Task.FromResult(HandleA(p));
    if (cmd == "b") return Task.FromResult(HandleB(p));
    if (cmd == "c") return Task.FromResult(HandleC(p));
    // ... 7 weitere
    return base.Execute(cmd, p);
}
// → IsDispatcher: true → kein Verstoß
```

### Kein Dispatcher — soll weiterhin feuern:
```csharp
// Ein Case enthält verschachtelte Logik → kein reiner Dispatcher
public Task<Result> Execute(string cmd, Params p)
{
    if (cmd == "a")
    {
        if (p.Id > 0)         // Inline-Logik → kein Dispatcher
            return Task.FromResult(HandleA(p));
        return Task.FromResult(Result.Fail("bad id"));
    }
    // ...
}
// → IsDispatcher: false → Komplexitäts-Verstoß normal gemeldet
```

### Near-Miss-Tolerance:
```csharp
// ComplexityNearMissTolerance: 1, Limit: 5
// Komplexität 6 → "[near-miss: knapp über Limit]" im Details
// Komplexität 7 → normaler Error (über Toleranz)
```

### Edge Cases:
- Weniger als 3 Branches → kein Dispatcher
- Switch mit einem Fall der inline-Logik hat → kein Dispatcher
- `ExcludeSwitchDispatcherCases: false` → normales Verhalten
- `ComplexityNearMissTolerance: 0` (Default) → bisheriges Verhalten

---

## README-Anforderungen

Im README zu `MaxCyclomaticComplexity` / `MaxCognitiveComplexity`:
- `ComplexityNearMissTolerance` erklären: Bereich in dem nur Warning statt Error
- `ExcludeSwitchDispatcherCases` erklären mit Code-Beispiel
- `SwitchDispatcherMaxCaseBodyLines` erklären
- Klarstellen: Switch-Dispatcher-Muster ist für LLM-Agenten positiv (lesbare Routing-Tabelle)

---

## Architektur-Hinweise

- `SwitchDispatcherDetector` gehört in `src/AiNetLinter/Metrics/` (neben `ComplexityCalculator.cs`)
- `MethodLineCounter.GetCodeLineCount(block)` ist von `BlockSyntax` aufzurufen — prüfen ob die bestehende Überladung das unterstützt; ggf. anpassen
- Near-Miss-Hinweis im `Details`-String (kein neues Severity-Feld) ist die minimal-invasive Lösung; eine vollständige Severity-Unterstützung wäre Plan 12 (README/Architektur)
