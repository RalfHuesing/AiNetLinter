# AiNetLinter — Vollumfänglicher Code Audit Report

**Datum:** 2026-06-13  
**Scope:** `src/AiNetLinter` + `src/AiNetLinter.Tests`  
**Referenzen:** README.md, ROADMAP.md, DeepResearch, `.cursor/rules/AiNetLinterRichtlinien.mdc`, Playbook

---

## Executive Summary

Der AiNetLinter ist ein ambitioniertes, wissenschaftlich fundiertes .NET 10 CLI-Tool, das C#-Codebasen für die Bearbeitung durch autonome KI-Agenten optimiert. Die Codebasis ist **insgesamt solide**: Der Roslyn-basierte Ansatz ist korrekt gewählt, die Architektur ist schlank (kein DI-Container, kein Over-Engineering), und die umfangreiche Testsuite zeigt Qualitätsbewusstsein.

Dieser Audit identifiziert **26 Findings** in 5 Kategorien:

| Kategorie | Kritisch | Hoch | Mittel | Niedrig |
|-----------|----------|------|--------|---------|
| Fachliche Sinnhaftigkeit der Regeln | 2 | 3 | 2 | — |
| Implementierungslücken (Regel fordert X, Code prüft Y) | 1 | 4 | 2 | — |
| Code-Qualität & eigene Regelverletzungen (Dogfooding) | — | 3 | 3 | 2 |
| Konfiguration & Konsistenz | — | 1 | 2 | 1 |
| **Gesamt** | **3** | **11** | **9** | **3** |

---

## 1. Fachliche Sinnhaftigkeit der Linter-Regeln

### 1.1 ⚠️ KRITISCH: `EnforceStrictBoundaryForBusinessLogic` — Heuristik ist zu fragil

**Problem:** Die Regel erkennt "Logik-Methoden" über eine Text-basierte Heuristik (`HasMathOperations` + `HasComplexLogic`), die auf das Vorhandensein von `+`, `-`, `*`, `/`, `&&`, `||`, `switch` oder `if` im **gesamten Methodentext** prüft.

**Fachlicher Impakt:**
- **False Positives massiv:** Jede Methode mit einem String-Concatenation (`+`), einem einfachen `if`-Guard oder einem switch-Statement wird als "Logik-Methode" klassifiziert. Das betrifft ~80% aller nicht-trivialen Methoden.
- **False Negatives:** Methoden, die komplexe Business-Logik über reine Methodenaufrufe delegieren (ohne lokale arithmetische Operatoren), werden nicht erkannt.

**Datei:** [`LinterAnalyzer.BusinessLogic.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.BusinessLogic.cs#L96-L123)

```csharp
// PROBLEM: Diese Heuristiken sind viel zu grob
private static bool HasMathOperations(string text)
{
    return text.Contains('+') || text.Contains('-') || text.Contains('*') || text.Contains('/');
}

private static bool HasComplexLogic(string text)
{
    return text.Contains("&&") || text.Contains("||") || text.Contains("switch") || text.Contains("if");
}
```

**Empfehlung:** Die Text-Suche durch eine semantische Roslyn-basierte Analyse ersetzen. Statt den Methodentext als String zu durchsuchen, sollte die Methode nur als "Logik-Methode" gelten, wenn sie sich in einer Klasse mit entsprechendem Suffix befindet (`Calculator`, `Rule`, `Policy`, `Engine`) ODER wenn sie explizit mit einem Attribut wie `[PureLogic]` markiert ist.

```csharp
// EMPFOHLEN: Nur Suffix-basierte Erkennung behalten, Text-Heuristik entfernen
private bool IsLogicMethod(MethodDeclarationSyntax node)
{
    if (node.Body == null && node.ExpressionBody == null) return false;
    return IsInLogicClass(node);
}
```

**Impakt auf Code-Qualität:** Eliminiert False Positives und macht die Regel für Endanwender vorhersagbar. Derzeit würde ein Nutzer massenhaft `// ainetlinter-disable` Kommentare schreiben müssen, was den Sinn der Regel untergräbt.

---

### 1.2 ⚠️ KRITISCH: `IsExceptionNumeric` — Silent Swallowing im Linter selbst

**Problem:** Die Methode `IsExceptionNumeric` in [`LinterAnalyzer.MagicValues.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.MagicValues.cs#L59-L72) fängt eine Exception stumm ab — genau das Verhalten, das der Linter mit `EnforceNoSilentCatch` verbietet.

```csharp
private static bool IsExceptionNumeric(object? value)
{
    if (value == null) return false;
    try
    {
        var d = Convert.ToDouble(value);
        return d == 0.0 || d == 1.0;
    }
    catch (Exception ignored)
    {
        _ = ignored;
        return false;
    }
}
```

**Fachlicher Impakt:** Obwohl `ignored` als Variable korrekt nach den eigenen Regeln benannt ist, ist der `try/catch` hier ein architektonischer Geruch. `Convert.ToDouble` sollte durch eine Type-Check-basierte Lösung ersetzt werden, die keine Exception wirft.

**Empfehlung:**

```csharp
private static bool IsExceptionNumeric(object? value)
{
    if (value is int i) return i is 0 or 1;
    if (value is long l) return l is 0L or 1L;
    if (value is double d) return d is 0.0 or 1.0;
    if (value is float f) return f is 0f or 1f;
    if (value is decimal m) return m is 0m or 1m;
    if (value is short s) return s is 0 or 1;
    if (value is byte b) return b is 0 or 1;
    return false;
}
```

**Impakt auf Code-Qualität:** Zeigt durch Dogfooding, dass der Linter seine eigenen Regeln einhält. Eliminiert eine unnötige Exception-basierte Logik.

---

### 1.3 🔶 HOCH: `SuppressionEvaluator.IsSuppressed` — Doppelte String-Splits

**Problem:** In [`SuppressionEvaluator.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Suppression/SuppressionEvaluator.cs#L12-L34) wird `fileContent.Split('\n')` **zweimal** in derselben Methode aufgerufen.

```csharp
public static bool IsSuppressed(string fileContent, string ruleName, int lineNumber)
{
    foreach (var line in fileContent.Split('\n'))  // Erster Split
    {
        if (SuppressionCommentParser.MatchesRule(line, ruleName))
            return true;
    }

    // ...
    var lines = fileContent.Split('\n');  // Zweiter Split (redundant!)
    if (lineNumber > lines.Length) return false;
    return SuppressionCommentParser.MatchesRule(lines[lineNumber - 1], ruleName);
}
```

**Fachlicher Impakt:** Bei großen Dateien (z.B. nahe der 500-Zeilen-Grenze) wird die Datei zweimal in ein Array zerlegt. Bei paralleler Analyse über `Parallel.ForEachAsync` mit `Environment.ProcessorCount` Threads summiert sich das auf.

**Empfehlung:**

```csharp
public static bool IsSuppressed(string fileContent, string ruleName, int lineNumber)
{
    var lines = fileContent.Split('\n');

    foreach (var line in lines)
    {
        if (SuppressionCommentParser.MatchesRule(line, ruleName))
            return true;
    }

    if (lineNumber <= 0 || lineNumber > lines.Length)
        return false;

    return SuppressionCommentParser.MatchesRule(lines[lineNumber - 1], ruleName);
}
```

**Impakt:** Eliminiert redundante Allokation. Die Logik wird außerdem semantisch klarer, da der erste Durchlauf dateiweite Suppressions prüft und der zweite zeilenbasierte.

---

### 1.4 🔶 HOCH: `LinterAnalyzer.IsSuppressed` — Dreifach redundante Suppression-Prüfung

**Problem:** In [`LinterAnalyzer.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.cs#L212-L224) wird am Ende von `RunAnalysis()` die Methode `FilterSuppressedViolations()` aufgerufen, die **für jede Violation** den gesamten Dateiinhalt via `_tree.GetText().ToString()` als neuen String allokiert.

```csharp
private bool IsSuppressed(string ruleName, int lineNumber)
{
    return SuppressionEvaluator.IsSuppressed(_tree.GetText().ToString(), ruleName, lineNumber);
}
```

**Fachlicher Impakt:** `_tree.GetText().ToString()` wird **pro Violation** aufgerufen. Bei einer Datei mit 20 Violations wird der gesamte Dateiinhalt 20x als neuer String allokiert.

**Empfehlung:** Den Dateiinhalt einmal cachen:

```csharp
private void FilterSuppressedViolations()
{
    var fileContent = _tree.GetText().ToString();
    var activeViolations = _violations
        .Where(v => !SuppressionEvaluator.IsSuppressed(fileContent, v.RuleName ?? "", v.LineNumber))
        .ToList();
    _violations.Clear();
    _violations.AddRange(activeViolations);
}
```

**Impakt auf Code-Qualität:** Signifikante Memory-Reduktion im Hot-Path. Bei einer Solution mit 1000 Dateien à 10 Violations würde das ~10.000 String-Allokationen einsparen.

---

### 1.5 🔶 HOCH: `IsTestFile` Heuristik in LinterEngine — Fragile Fallback-Logik

**Problem:** In [`LinterEngine.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterEngine.cs#L219) wird `IsTestFile(filePath)` als Fallback-Heuristik aufgerufen, obwohl die Testprojekt-Erkennung bereits über `isTestProj` (via MetadataReferences) korrekt erfolgt. Die Pfad-basierte Heuristik (`file.EndsWith("Tests.cs")`) kann bei Produktionscode mit `Tests` im Namen zu False Positives führen.

```csharp
bool isTestFile = isTestProj || IsTestFile(filePath);
```

**Empfehlung:** Die Pfad-Heuristik `IsTestFile` nur als absoluten Fallback verwenden und dokumentieren, warum sie existiert (z.B. für Projekte ohne xunit/nunit-Referenz). Besser wäre es, sie per Konfiguration abschaltbar zu machen.

---

### 1.6 🔵 MITTEL: `EnforceNoMagicValues` — Attribute-Argumente werden nicht immer erkannt

**Problem:** Die Prüfung in [`LinterAnalyzer.MagicValues.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.MagicValues.cs#L74-L90) erkennt Magic Values korrekt in Methodenrümpfen, aber die Ausnahme für Attribute-Argumente ist nicht implementiert. Das bedeutet, dass `[MaxLength(255)]` oder `[Timeout(30000)]` als Magic Values flagged werden könnten.

**Empfehlung:** Die `IsInsideBody`-Prüfung um einen Check erweitern, ob der Literal-Ausdruck sich in einem `AttributeArgumentSyntax` befindet:

```csharp
private bool IsMagicValue(LiteralExpressionSyntax node)
{
    if (!IsTargetLiteral(node)) return false;
    if (IsExceptionValue(node)) return false;
    if (IsConstDeclaration(node)) return false;
    if (IsAttributeArgument(node)) return false;  // NEU
    return IsInsideBody(node);
}

private static bool IsAttributeArgument(SyntaxNode node)
{
    return node.Ancestors().OfType<AttributeArgumentSyntax>().Any();
}
```

**Hinweis:** Tests existieren bereits für `MagicValues_AttributeArguments_AreAllowed`, was darauf hindeutet, dass die Implementierung möglicherweise über `IsInsideBody` implizit abgedeckt wird. Hier wäre eine explizite Prüfung jedoch sicherer.

---

### 1.7 🔵 MITTEL: Fehlende `-1`-Ausnahme bei Magic Values

**Problem:** Die README.md und Richtlinien erwähnen `0`, `1` und `""` als erlaubte Ausnahmen. Die ROADMAP (Epic 14) erwähnt zusätzlich `-1`. Im Code ([`LinterAnalyzer.MagicValues.cs:59-66`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.MagicValues.cs#L59-L66)) wird aber nur `0.0` und `1.0` geprüft — `-1` fehlt.

**Empfehlung:**
```csharp
private static bool IsExceptionNumeric(object? value)
{
    // ... (nach Umstellung auf Pattern-Matching)
    if (value is int i) return i is 0 or 1 or -1;
    // ...
}
```

---

## 2. Implementierungslücken (Regel fordert X, Code prüft Y)

### 2.1 ⚠️ KRITISCH: `ProjectConfigResolver.MergeConfig` — Neue Regeln aus Epic 20 fehlen im Merge

**Problem:** In [`ProjectConfigResolver.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/ProjectConfigResolver.cs#L59-L110) werden die ProjectOverrides gemergt. Dabei fehlen **5 neuere GlobalConfig-Regeln** im `with`-Block:

| Fehlende Regel | Eingeführt in |
|---|---|
| `EnforceExplicitStateImmutability` | Epic 20 |
| `EnforceStrictBoundaryForBusinessLogic` | Epic 20 |
| `PreventContextDependentOverloads` | Epic 20 |
| `RequireExplicitTruncationHandling` | Epic 20 |
| `EnforceNamespaceDirectoryMapping` | Epic 20 |
| `DetectAndBanPhantomDependencies` | Epic 20 |

**Fachlicher Impakt:** Wenn ein Nutzer z.B. in `*.Tests` Projekten `EnforceExplicitStateImmutability: false` setzen möchte, wird dies **stillschweigend ignoriert**. Die Regel bleibt aktiv, obwohl der Nutzer sie deaktiviert hat.

**Empfehlung:** Den `MergeConfig`-`with`-Block für `mergedGlobal` um alle fehlenden Properties erweitern:

```csharp
mergedGlobal = global.Global with
{
    // ... existierende Zeilen ...
    EnforceNoMagicValues = og.EnforceNoMagicValues ?? global.Global.EnforceNoMagicValues,
    // FEHLENDE ZEILEN:
    EnforceExplicitStateImmutability = og.EnforceExplicitStateImmutability ?? global.Global.EnforceExplicitStateImmutability,
    AllowedExceptions = og.AllowedExceptions ?? global.Global.AllowedExceptions,
    EnforceStrictBoundaryForBusinessLogic = og.EnforceStrictBoundaryForBusinessLogic ?? global.Global.EnforceStrictBoundaryForBusinessLogic,
    PreventContextDependentOverloads = og.PreventContextDependentOverloads ?? global.Global.PreventContextDependentOverloads,
    RequireExplicitTruncationHandling = og.RequireExplicitTruncationHandling ?? global.Global.RequireExplicitTruncationHandling,
    EnforceNamespaceDirectoryMapping = og.EnforceNamespaceDirectoryMapping ?? global.Global.EnforceNamespaceDirectoryMapping,
    DetectAndBanPhantomDependencies = og.DetectAndBanPhantomDependencies ?? global.Global.DetectAndBanPhantomDependencies,
};
```

Ebenso fehlt `MaxDirectoryDepth` im `mergedMetrics`-Block:
```csharp
mergedMetrics = global.Metrics with
{
    // ... existierende Zeilen ...
    MaxAIContextFootprint = om.MaxAIContextFootprint ?? global.Metrics.MaxAIContextFootprint,
    MaxDirectoryDepth = om.MaxDirectoryDepth ?? global.Metrics.MaxDirectoryDepth, // FEHLEND
};
```

**Impakt auf Code-Qualität:** **Hoch**. Dies ist ein **funktionaler Bug**, der dazu führt, dass Nutzer ihre Konfiguration nicht korrekt anwenden können. Der Linter würde Regeln in Testprojekten erzwingen, die dort explizit deaktiviert wurden.

---

### 2.2 🔶 HOCH: `IsTestReference` und `IsTestProject` — Doppelte Implementierung

**Problem:** Die Methoden `IsTestProject` und `IsTestReference` existieren **identisch** in zwei Dateien:
1. [`LinterEngine.cs:181-208`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterEngine.cs#L181-L208)
2. [`SourceFileCatalog.cs:159-186`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Baseline/SourceFileCatalog.cs#L159-L186)

**Fachlicher Impakt:** DRY-Verletzung bei fachlichem Code (nicht bei technischem Boilerplate). Wenn die Test-Erkennungslogik erweitert wird (z.B. um `MSTest.TestAdapter`), muss sie an zwei Stellen synchron geändert werden.

**Empfehlung:** Eine statische Methode in einer gemeinsamen Klasse (z.B. `TestProjectDetector`) extrahieren:

```csharp
// Neue Datei: Core/TestProjectDetector.cs
public static class TestProjectDetector
{
    private static readonly string[] TestKeywords = ["xunit", "nunit", "testplatform", "unittesting"];

    public static bool IsTestProject(Project project)
    {
        foreach (var reference in project.MetadataReferences)
        {
            if (IsTestReference(reference.Display)) return true;
        }
        return false;
    }

    private static bool IsTestReference(string? display)
    {
        if (string.IsNullOrEmpty(display)) return false;
        foreach (var keyword in TestKeywords)
        {
            if (display.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
```

---

### 2.3 🔶 HOCH: `IsPrimitiveName` / `IsPrimitiveSpecialType` — Arrays in Hot Path

**Problem:** In [`LinterAnalyzer.Scope.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Scope.cs#L211-L236) werden bei jedem Überladungsvergleich **neue Arrays allokiert**:

```csharp
private static bool IsPrimitiveName(string name)
{
    var primitives = new[]  // NEUES ARRAY pro Aufruf!
    {
        "Int32", "Int64", "Int16", "String", "Boolean", 
        "Double", "Single", "Decimal", "Char", "Byte", "Guid" 
    };
    return primitives.Contains(name);
}
```

**Empfehlung:** Diese als `static readonly` HashSets deklarieren:

```csharp
private static readonly HashSet<string> PrimitiveNames = new(StringComparer.Ordinal)
{
    "Int32", "Int64", "Int16", "String", "Boolean", 
    "Double", "Single", "Decimal", "Char", "Byte", "Guid"
};

private static bool IsPrimitiveName(string name) => PrimitiveNames.Contains(name);
```

Gleiches gilt für `IsPrimitiveSpecialType` und `IsForbiddenIoType`, `IsStaticIoClass`, `IsForbiddenReflectionCall` — überall werden Arrays in Methoden-Scope allokiert, die auf Klassen-Scope als `static readonly` deklariert werden sollten.

**Impakt:** Reduziert GC-Druck signifikant bei Solutions mit vielen Klassen.

---

### 2.4 🔶 HOCH: `EnforceResultPatternOverExceptions` — `IsThrowAllowed` bricht bei Lambdas

**Problem:** In [`LinterAnalyzer.ControlFlow.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.ControlFlow.cs#L121-L148) sucht `GetEnclosingContainer` den umschließenden Container eines `throw`, stoppt aber an `TypeDeclarationSyntax`. Für Lambdas innerhalb normaler Methoden wird der Test korrekt als "disallowed" erkannt. Jedoch fehlt ein Edge-Case: Ein `throw` in einer lokalen Funktion, die **nicht** `Guard`/`Validate` heißt, wird fälschlicherweise als erlaubt zurückgegeben, wenn die äußere Methode `Guard`/`Validate` heißt.

```csharp
private static bool IsContainer(SyntaxNode node)
{
    if (node is MethodDeclarationSyntax || node is ConstructorDeclarationSyntax) return true;
    return node is LocalFunctionStatementSyntax lf && IsGuardOrValidateName(lf.Identifier.Text);
}
```

**Problem:** Wenn eine lokale Funktion `ProcessData` innerhalb einer Methode `ValidateInput` liegt, wird `ProcessData` nicht als Container erkannt (kein Guard-Name), also wird weiter nach oben gesucht und `ValidateInput` gefunden → `throw` wird **fälschlicherweise erlaubt**.

**Empfehlung:** `IsContainer` sollte ALLE lokalen Funktionen als Container erkennen, nicht nur Guard-Funktionen:

```csharp
private static bool IsContainer(SyntaxNode node)
{
    if (node is MethodDeclarationSyntax || node is ConstructorDeclarationSyntax) return true;
    return node is LocalFunctionStatementSyntax; // Alle lokalen Funktionen stoppen die Suche
}
```

Dann prüft `IsThrowAllowed` ob der gefundene Container Guard/Validate heißt.

**Impakt:** Verhindert False Negatives bei der Kontrollfluss-Prüfung.

---

### 2.5 🔵 MITTEL: `EnforceNoSilentCatch` — `OperationCanceledException` nur per IdentifierName

**Problem:** In [`LinterAnalyzer.ControlFlow.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.ControlFlow.cs#L44-L49) wird `OperationCanceledException` nur über den einfachen Identifier-Text erkannt:

```csharp
if (node.Declaration?.Type is not IdentifierNameSyntax { Identifier.Text: "OperationCanceledException" }) return false;
```

**Fachlicher Impakt:** Wenn ein Entwickler den vollqualifizierten Typ `System.OperationCanceledException` oder einen Alias nutzt, greift die Erkennung nicht. Die semantische Prüfung via `SemanticModel` wäre robuster.

**Empfehlung:**

```csharp
private bool IsAllowedCancellationCatch(CatchClauseSyntax node)
{
    if (!_config.Global.AllowCancellationShutdownCatch) return false;
    if (node.Declaration?.Type == null) return false;
    
    var typeInfo = _semanticModel.GetTypeInfo(node.Declaration.Type);
    var typeName = typeInfo.Type?.ToDisplayString();
    if (typeName != "System.OperationCanceledException" && 
        typeName != "System.Threading.Tasks.TaskCanceledException") return false;
    
    return node.Filter != null;
}
```

---

### 2.6 🔵 MITTEL: `ViolationTextFormatter` — Guidance-Separator inkonsistent mit README

**Problem:** Die README dokumentiert das Format als `→ {Guidance}`, aber [`ViolationTextFormatter.cs:87`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Output/ViolationTextFormatter.cs#L87) verwendet `-> {Guidance}`:

```csharp
line += $" -> {violation.Guidance}";
```

Die README zeigt:
```
src/AiNetLinter/Core/LinterAnalyzer.cs:77 EnforceSealedClasses | ... → Füge den 'sealed' Modifikator hinzu.
```

**Empfehlung:** Entweder README oder Code anpassen. Da `→` (Unicode) Probleme in manchen Terminal-Encodings verursachen kann, ist `->` (ASCII) die robustere Variante → README aktualisieren.

---

## 3. Code-Qualität & Eigene Regelverletzungen (Dogfooding)

### 3.1 🔶 HOCH: `Program.cs` (461 Zeilen) — Nähert sich dem eigenen Limit

**Problem:** [`Program.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Program.cs) hat 461 Zeilen und liegt damit nur 39 Zeilen unter dem eigenen `MaxLineCount`-Limit von 500. Bei der nächsten Feature-Ergänzung wird die Datei das Limit überschreiten.

**Empfehlung:** Die Methoden `AddDisableAllAsync`, `RemoveDisableAllAsync`, `CreateBaselineAsync`, `RunImpactAnalysisAsync` und `RunDebtReportAsync` in separate statische Klassen auslagern (z.B. `MaintenanceExecutor`, `ImpactExecutor`, `DebtReportExecutor`).

---

### 3.2 🔶 HOCH: `LinterEngine.cs` (465 Zeilen) — Nähert sich dem eigenen Limit

**Problem:** [`LinterEngine.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterEngine.cs) hat 465 Zeilen und liegt nur 35 Zeilen unter dem Limit.

**Empfehlung:** Die privaten Records `AnalysisState`, `DocumentContext`, `TestSentinelContext` in eigene Dateien extrahieren. Die `RunTestSentinel`- und `RunInheritanceDepthCheck`-Logik könnte in eine `PostAnalysisChecks`-Klasse extrahiert werden.

---

### 3.3 🔶 HOCH: `_currentNamespace` — Veränderlicher Zustand im Analyzer

**Problem:** [`LinterAnalyzer.cs:21`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.cs#L21) deklariert `_currentNamespace` als nicht-readonly, mutables Feld:

```csharp
private string _currentNamespace = "";
```

Dies wird von der `EnforceExplicitStateImmutability`-Regel unterdrückt, weil `LinterAnalyzer` ein SyntaxWalker ist und zustandsbasiert arbeiten muss. Allerdings widerspricht es dem eigenen Immutabilitäts-Ideal.

**Empfehlung:** Dies ist ein inhärentes Trade-off des Visitor-Patterns. Dokumentieren, warum der LinterAnalyzer als Ausnahme behandelt wird (z.B. via `// ainetlinter-disable EnforceExplicitStateImmutability` mit Kommentar).

---

### 3.4 🔵 MITTEL: Widerspruch in eigenen Richtlinien — MaxConstructorDependencies

**Problem:** Die Richtlinien in [`.cursor/rules/AiNetLinterRichtlinien.mdc`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/.cursor/rules/AiNetLinterRichtlinien.mdc#L18-L20) definieren:
- `Max 20 Constructor-Parameter (Dependencies)`
- `Max 10 Methodenüberladungen`

Die README und der Code (`LinterConfig.cs`) definieren:
- `MaxConstructorDependencies: 5`
- `MaxMethodOverloads: 3`

Die Richtlinien-Datei für den eigenen Entwicklungsprozess hat **viel lockerere Werte** als das Tool selbst erzwingt.

**Empfehlung:** Die Richtlinien-Datei aktualisieren, um die gleichen Werte wie die `rules.json` zu reflektieren (5 und 3 statt 20 und 10). Sonst entsteht Verwirrung, ob die Richtlinien oder die Konfiguration maßgeblich ist.

---

### 3.5 🔵 MITTEL: Fehlende `#nullable enable` in einigen Partial-Dateien

**Problem:** Einige Partial-Dateien haben explizit `#nullable enable` am Anfang (z.B. [`LinterAnalyzer.Immutability.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Immutability.cs#L1), [`LinterAnalyzer.Safety.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Safety.cs#L1), [`LinterAnalyzer.BusinessLogic.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.BusinessLogic.cs#L1)), während andere (z.B. [`LinterAnalyzer.Architecture.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Architecture.cs), [`LinterAnalyzer.Scope.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Scope.cs), [`LinterAnalyzer.Complexity.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Complexity.cs)) es nicht haben.

Da `<Nullable>enable</Nullable>` in der `.csproj` global gesetzt ist, ist dies **funktional harmlos**, aber **stilistisch inkonsistent**. Der Linter selbst nutzt die semantische Prüfung via `NullableContext` und erkennt die globale Einstellung korrekt.

**Empfehlung:** Entweder alle Dateien auf das globale Setting verlassen (dann `#nullable enable` aus allen Partial-Dateien entfernen) oder konsequent in jede Datei schreiben.

---

### 3.6 🔵 MITTEL: `IsDtoOrEntity` — Suffix-Erkennung nicht konfigurierbar

**Problem:** In [`LinterAnalyzer.Immutability.cs:89-109`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Immutability.cs#L89-L109) sind die DTO-Suffixe (`Dto`, `Entity`, `Model`, `Request`, `Response`, `Command`) hartcodiert. Nutzer können keine eigenen Suffixe hinzufügen.

**Empfehlung:** Die Suffixe in die `GlobalConfig` als konfigurierbare Liste aufnehmen:

```csharp
public IReadOnlyCollection<string> ImmutabilityExemptSuffixes { get; init; } = 
    ["Dto", "Entity", "Model", "Request", "Response", "Command"];
```

---

### 3.7 ℹ️ NIEDRIG: `GetForbiddenNames` — HashSet wird pro Aufruf erstellt

**Problem:** In [`LinterAnalyzer.Naming.cs:71-77`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Naming.cs#L71-L77) wird ein neues `HashSet<string>` bei jedem Aufruf erstellt:

```csharp
private static HashSet<string> GetForbiddenNames()
{
    return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "data", "temp", "obj", "val", "tmp", "item", "param"
    };
}
```

**Empfehlung:** Als `private static readonly` Feld deklarieren.

---

### 3.8 ℹ️ NIEDRIG: `ClassInfo` hält Roslyn `INamedTypeSymbol` — Memory Leak Potenzial

**Problem:** [`ClassInfo.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Models/ClassInfo.cs#L14) speichert `INamedTypeSymbol Symbol` in einem Record, der in `ConcurrentBag<ClassInfo>` gesammelt wird. Da `INamedTypeSymbol` transitiv den gesamten Roslyn-Compilation-Graphen referenziert, verhindert dies die Freigabe des Speichers bis nach der gesamten Analyse.

**Empfehlung:** Für den Test Sentinel und Inheritance Check die benötigten Informationen (BaseType-Kette, etc.) direkt beim Collection-Zeitpunkt extrahieren und als einfache Strings speichern, statt den Symbol-Graphen zu halten. Dies ist besonders relevant für große Solutions.

---

## 4. Konfiguration & Konsistenz

### 4.1 🔶 HOCH: Fehlende `RuleInstructions` für neuere Regeln

**Problem:** [`ViolationTextFormatter.cs:93-109`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Output/ViolationTextFormatter.cs#L93-L109) enthält ein Dictionary mit spezifischen LLM-Anweisungen pro Regel. Es fehlen Einträge für:

- `EnforceExplicitStateImmutability`
- `EnforceStrictBoundaryForBusinessLogic`
- `PreventContextDependentOverloads`
- `RequireExplicitTruncationHandling`
- `EnforceNamespaceDirectoryMapping`
- `DetectAndBanPhantomDependencies`
- `MaxConstructorDependencies`
- `MaxMethodOverloads`
- `MaxInheritanceDepth`
- `MaxDirectoryDepth`
- `AIContextFootprint`
- `EnforceNoVariableShadowing`
- `EnforceReadonlyParameters`
- `EnforceReadonlyFields`
- `EnforceNoMagicValues`
- `EnforceValueObjectContracts`
- `EnforceResultPatternOverExceptions`

Für all diese Regeln wird der generische Fallback verwendet: `"-> {ruleName}: Bitte behebe diesen Verstoss gemaess den Richtlinien."`

**Fachlicher Impakt:** Das Tool ist explizit für LLM-Agenten konzipiert. Wenn ein Agent eine `PreventContextDependentOverloads`-Violation erhält, fehlt ihm die kontextuelle Anweisung, *wie* er das beheben soll. Der generische Fallback ist zu unspezifisch.

**Empfehlung:** Spezifische Instruktionen für alle Regeln hinzufügen:

```csharp
["EnforceExplicitStateImmutability"] = "-> EnforceExplicitStateImmutability: Klasse muss unveraenderlich sein. Verwende 'init' statt 'set' fuer Properties und 'readonly' fuer Felder. DTOs/Entities sind ausgenommen.",
["EnforceNoVariableShadowing"] = "-> EnforceNoVariableShadowing: Variable verdeckt ein Feld oder aeusseren Parameter. Benenne die lokale Variable um.",
["EnforceReadonlyParameters"] = "-> EnforceReadonlyParameters: Parameter duerfen nicht ueberschrieben werden. Nutze eine lokale Variable fuer den geaenderten Wert.",
["EnforceReadonlyFields"] = "-> EnforceReadonlyFields: Private Felder, die nur im Konstruktor zugewiesen werden, muessen 'readonly' sein.",
["EnforceNoMagicValues"] = "-> EnforceNoMagicValues: Magische Literale (Zahlen/Strings) muessen als 'const' oder 'static readonly' deklariert werden.",
["EnforceResultPatternOverExceptions"] = "-> EnforceResultPatternOverExceptions: 'throw' ist nur in Konstruktoren und Guard/Validate-Methoden erlaubt. Nutze Result<T> fuer fachliche Fehler.",
["MaxConstructorDependencies"] = "-> MaxConstructorDependencies: Zu viele Konstruktor-Abhaengigkeiten. Teile die Klasse in kleinere Services auf.",
["MaxMethodOverloads"] = "-> MaxMethodOverloads: Zu viele Ueberladungen. Nutze explizite, sprechende Methodennamen.",
["PreventContextDependentOverloads"] = "-> PreventContextDependentOverloads: Ueberladungen unterscheiden sich nur in primitiven Typen. Nutze explizite Methodennamen.",
["EnforceExplicitStateImmutability"] = "-> EnforceExplicitStateImmutability: Eigenschaft oder Feld muss unveraenderlich sein. Nutze 'init' oder 'readonly'.",
["RequireExplicitTruncationHandling"] = "-> RequireExplicitTruncationHandling: I/O-Leseoperation ohne Laengencheck. Pruefe Bytes/Zeilen unmittelbar nach dem Lesen.",
["EnforceNamespaceDirectoryMapping"] = "-> EnforceNamespaceDirectoryMapping: Namespace stimmt nicht mit Ordnerstruktur ueberein. Passe den Namespace an.",
["DetectAndBanPhantomDependencies"] = "-> DetectAndBanPhantomDependencies: Nicht aufloesbarer Namespace oder dynamische Reflection. Entferne den Import oder nutze statische Typen.",
```

---

### 4.2 🔵 MITTEL: `rules.json` Beispiel in README — Unvollständig

**Problem:** Die `rules.json` im README (Zeilen 132-178) enthält nicht alle Regeln, die im Code implementiert sind. Es fehlen:
- `EnforceNoVariableShadowing`
- `EnforceReadonlyParameters`
- `EnforceReadonlyFields`
- `EnforceNoMagicValues`
- `EnforceExplicitStateImmutability`
- `EnforceStrictBoundaryForBusinessLogic`
- `PreventContextDependentOverloads`
- `RequireExplicitTruncationHandling`
- `EnforceNamespaceDirectoryMapping`
- `DetectAndBanPhantomDependencies`
- `MaxDirectoryDepth`
- `MaxAIContextFootprint`

**Empfehlung:** Die `rules.json` im README mit allen aktuell verfügbaren Regeln und ihren Defaults aktualisieren.

---

### 4.3 🔵 MITTEL: Playbook-Ergebnisse zeigen 0 Result-Pattern-Nutzung

**Problem:** Das [Auto-generierte Playbook](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/.cursor/rules/playbook.md#L7) zeigt:
```
- Result-Pattern-Nutzung: 0 Methoden liefern Result oder Result<T> zurueck.
- Kontrollfluss-Exceptions: 25 throw-Anweisungen wurden im Code-Rumpf gefunden.
```

Dies bedeutet, dass der AiNetLinter selbst das Result-Pattern, das er für andere Projekte erzwingt, **gar nicht nutzt**. 25 `throw`-Anweisungen werden im eigenen Code verwendet.

**Fachlicher Impakt:** Dies ist **kein Bug**, da der Linter primär technische Standard-Exceptions wirft (z.B. `ArgumentNullException`, `FileNotFoundException`), die explizit als Fail-Fast-Muster erlaubt sind. Jedoch zeigt es, dass das Playbook keine Differenzierung zwischen erlaubten und verbotenen `throw`s vornimmt.

**Empfehlung:** Den `PlaybookSyntaxWalker` erweitern, um nur fachliche (nicht-erlaubte) `throw`s zu zählen, und die erlaubten Standard-Exceptions separat auszuweisen.

---

### 4.4 ℹ️ NIEDRIG: Version in `.csproj` — Kein automatisches Versioning

**Problem:** Die Version `1.0.18` in [`AiNetLinter.csproj`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/AiNetLinter.csproj#L8) wird manuell gepflegt.

**Empfehlung:** Für CI/CD-Releases auf `dotnet-gitversion` oder ein ähnliches Tool umstellen.

---

## 5. Zusammenfassung der Top-3 Handlungsempfehlungen

### Priorität 1: `ProjectConfigResolver.MergeConfig` fixen (Bug)
Die fehlenden Regeln im Merge-Block sind ein **funktionaler Bug**. Jeder Nutzer mit `ProjectOverrides` für Epic 20-Regeln wird falsche Ergebnisse erhalten.

### Priorität 2: `EnforceStrictBoundaryForBusinessLogic` Heuristik überarbeiten
Die Text-basierte Erkennung von "Logik-Methoden" produziert zu viele False Positives und untergräbt das Vertrauen in den Linter bei Endanwendern.

### Priorität 3: `ViolationTextFormatter` RuleInstructions vervollständigen
Da das Tool explizit für LLM-Agenten gebaut ist, ist jede Regel ohne spezifische Handlungsanweisung ein verpasster Mehrwert. Die generische Guidance "Behebe diesen Verstoss gemäß den Richtlinien" ist für einen KI-Agenten unbrauchbar.

---

## Anhang: Vollständige Datei-Inventur

| Verzeichnis | Dateien | Zeilen (ca.) | Bewertung |
|---|---|---|---|
| `Core/` | 21 Dateien | ~2800 | Kernlogik, gut aufgeteilt über Partial Classes |
| `Configuration/` | 5 Dateien | ~650 | Saubere Record-Hierarchie |
| `Models/` | 2 Dateien | ~37 | Minimal, korrekt |
| `Metrics/` | 4 Dateien | ~470 | Solide Implementierung der Komplexitätsmetriken |
| `Output/` | 7 Dateien | ~540 | Gute Trennung (SARIF, Text, Summary) |
| `Suppression/` | 6 Dateien | ~350 | Funktional korrekt, Performance-Verbesserung möglich |
| `Baseline/` | 10 Dateien | ~460 | Saubere Abstraktion |
| `Cli/` | 3 Dateien | ~320 | System.CommandLine Integration korrekt |
| `Scope/` | 3 Dateien | ~190 | Schlanke Filter-Logik |
| `Program.cs` | 1 Datei | 461 | Nahe am Limit |
| **Tests/** | 12+ Dateien | ~2500+ | Umfangreiche Abdeckung |

**Gesamtbewertung:** Die Codebasis ist **überdurchschnittlich gut strukturiert** für ein Projekt dieser Größe. Die Nutzung von Partial Classes für den `LinterAnalyzer` ist eine pragmatische Lösung, die die Dateigröße unter 500 Zeilen hält. Die größten Risiken liegen in der unvollständigen Synchronisation zwischen neuen Regeln (Epic 20) und der bestehenden Infrastruktur (`ProjectConfigResolver`, `ViolationTextFormatter`).
