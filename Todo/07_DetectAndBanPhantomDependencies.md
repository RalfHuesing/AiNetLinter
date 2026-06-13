# Epic: DetectAndBanPhantomDependencies (Erzwingung strikter Referenz-Pfade & Phantom-Pakete verbieten)

## Big Picture & Intention
Ein berüchtigtes Versagensmuster von KI-Agenten in fremden Codebases ist das Halluzinieren von Paket-Abhängigkeiten oder Klassen (**Contextual Disconnect / Category 8**). Da LLMs auf riesigen Mengen Open-Source-Code trainiert wurden, "erinnern" sie sich oft an Bibliotheken, die im aktuellen Projekt gar nicht referenziert oder im .NET-Ökosystem gar nicht vorhanden sind (z. B. das Annehmen von Python-ähnlichen Paketen in C#). 

Wenn der Agent solche Abhängigkeiten per `using` importiert, wirft der Compiler Fehler. Manchmal versucht der Agent auch, Typen dynamisch per Reflection über Strings zu laden (`Type.GetType("MyHallucinatedClass")`), um statische Linter-Prüfungen zu umgehen. Dies führt zu **Phantom-Klassen**, die erst zur Laufzeit krachen.

**Die Lösung:** 
Der Linter bindet die statische Roslyn-Kompilierung ein, um:
1. **Nicht-auflösbare using-Anweisungen** als sofortigen Linter-Fehler zu markieren (noch vor dem regulären Build-Prozess).
2. **Dynamische Reflection-Typauflösungen via String** komplett zu verbieten. Jede Typen-Referenz muss statisch typisiert sein (z. B. über `typeof(MyClass)`), damit der Compiler (und der Roslyn-Syntaxbaum) die Existenz der Klasse garantieren können.

Dies zwingt das Sprachmodell zur absoluten Verankerung (Grounding) in der realen, statisch deklarierten Codebasis.

---

## Realistischer Impact
* **Vermeidung von Build-Fehlern durch Halluzinationen:** Hoch. Stoppt den Agenten sofort, wenn er versucht, nicht installierte NuGet-Pakete einzubinden.
* **Typensicherheit zur Compile-Zeit:** Hoch. Verhindert unsaubere C#-Reflection-Hacks, die für statische Code-Analysen der KI unsichtbar sind.

---

## Konkrete Regeln & Heuristik
1. **Validierung von `using`-Directives:**
   - Der Linter wertet jede `UsingDirectiveSyntax` aus.
   - Über das semantische Modell `_semanticModel.GetSymbolInfo(node.Name)` prüfen wir, ob das Symbol auflösbar ist. Ist das Symbol `null` (weil die Assembly oder der Namespace nicht in den Referenzen der `.csproj` vorhanden ist), schlägt die Regel an.
2. **Reflection-Verbot:**
   - Der Linter verbietet Aufrufe von `Type.GetType(...)`, `Assembly.Load(...)`, `Assembly.LoadFrom(...)` sowie die Verwendung von `Activator.CreateInstance(string, string)`.
   - Ausgenommen sind typisierte Aufrufe wie `Activator.CreateInstance(typeof(T))` oder `typeof(MyClass)`.

---

## Code-Vorschläge & Referenzen

### 1. Erweiterung der Konfiguration
In [LinterConfig.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/LinterConfig.cs#L34-L55) fügen wir die Regel hinzu:

```csharp
public sealed record GlobalConfig
{
    // ... bestehende Regeln ...
    
    /// <summary>
    /// Verbietet using-Anweisungen zu nicht-referenzierten Namespaces sowie dynamische Reflection.
    /// </summary>
    public bool DetectAndBanPhantomDependencies { get; init; } = true;
}
```

In [RuleMetadataRegistry.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/RuleMetadataRegistry.cs#L8-L28):
```csharp
private static readonly IReadOnlyDictionary<string, RuleMetadataEntry> Defaults =
    new Dictionary<string, RuleMetadataEntry>(StringComparer.Ordinal)
    {
        // ...
        ["DetectAndBanPhantomDependencies"] = new() { Severity = "error", Intent = "architecture" },
    };
```

### 2. Implementierung des Analyzers
Wir erweitern die bestehende [LinterAnalyzer.Architecture.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Architecture.cs) um die neuen Prüfungen:

#### Namespace-Prüfung in `VisitUsingDirective`
Wir passen [LinterAnalyzer.Architecture.cs:L14-21](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Architecture.cs#L14-L21) an:

```csharp
public override void VisitUsingDirective(UsingDirectiveSyntax node)
{
    if (node.Name != null)
    {
        CheckForbiddenNamespaceString(node.Name.ToString(), node);
        CheckPhantomNamespace(node); // Neuer Aufruf
    }
    base.VisitUsingDirective(node);
}

private void CheckPhantomNamespace(UsingDirectiveSyntax node)
{
    if (!_config.Global.DetectAndBanPhantomDependencies) return;
    if (_isTestFile) return;

    if (node.Name != null)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node.Name);
        
        // Wenn Roslyn den Namespace im Kompilierungskontext ueberhaupt nicht kennt
        if (symbolInfo.Symbol == null)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "DetectAndBanPhantomDependencies",
                Details = $"Der importierte Namespace '{node.Name}' kann nicht aufgeloest werden. Ist die NuGet-Abhaengigkeit in der csproj deklariert?",
                Guidance = "Entferne das using-Statement oder fuege die entsprechende Projektreferenz/.csproj-Abhaengigkeit hinzu."
            });
        }
    }
}
```

#### Reflection-Verbot in `VisitInvocationExpression`
Wir ergänzen [LinterAnalyzer.Architecture.cs:L265-270](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Architecture.cs#L265-L270):

```csharp
public override void VisitInvocationExpression(InvocationExpressionSyntax node)
{
    CheckMinimalApiAsParameters(node);
    CheckPhantomReflection(node); // Neuer Aufruf
    base.VisitInvocationExpression(node);
}

private void CheckPhantomReflection(InvocationExpressionSyntax node)
{
    if (!_config.Global.DetectAndBanPhantomDependencies) return;
    if (_isTestFile) return;

    var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
    if (symbol == null) return;

    var containingType = symbol.ContainingType?.ToDisplayString() ?? "";
    var methodName = symbol.Name;

    // Verbiete dynamische Reflection-Lade-APIs
    if (IsForbiddenReflectionCall(containingType, methodName))
    {
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = "DetectAndBanPhantomDependencies",
            Details = $"Die Verwendung von dynamischer Reflection '{containingType}.{methodName}' ist fuer KI-Lesbarkeit nicht gestattet.",
            Guidance = "Verwende statische Typ-Ausdruecke wie 'typeof(MyClass)' oder Generics, um die Compile-Zeit-Sicherheit zu wahren."
        });
    }
}

private static bool IsForbiddenReflectionCall(string containingType, string methodName)
{
    if (containingType == "System.Type" && methodName == "GetType")
    {
        return true;
    }

    if (containingType.StartsWith("System.Reflection.Assembly") && 
        (methodName.StartsWith("Load") || methodName.StartsWith("LoadFrom")))
    {
        return true;
    }

    if (containingType == "System.Activator" && methodName == "CreateInstance")
    {
        // Weitere Pruefung, ob String-basierte Ueberladungen aufgerufen werden,
        // kann hier verfeinert werden.
        return true;
    }

    return false;
}
```
