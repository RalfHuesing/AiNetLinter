# Epic: PreventContextDependentOverloads (Kontextabhängige Methodenüberladungen verhindern)

## Big Picture & Intention
Das Vorhandensein von vielen Methodenüberladungen (Method Overloads) mit demselben Namen, aber unterschiedlichen Parametertypen, ist ein massiver Störfaktor für die Code-Generierung durch Sprachmodelle. 

Da LLMs Code Token für Token generieren, versuchen sie beim Aufruf einer Methode, die passende Signatur aus den bekannten Bezeichnern zu ermitteln. Wenn ein Typ beispielsweise 5 verschiedene `Process`-Methoden anbietet (z. B. `Process(int)`, `Process(long)`, `Process(string)`, `Process(Guid)`, `Process(object)`), kann das Modell die genaue Überladung im Vektorraum schwer differenzieren. Dies gilt umso mehr in noch unvollständigen Entwürfen, in denen Typinformationen fließen oder implizit gecastet werden müssen.

Das führt zu **Parameter Hallucinations (Category 4)**, bei denen die KI falsche Argument-Reihenfolgen übergibt oder Typen vertauscht, was erst beim Compiler-Lauf auffliegt.

**Die Lösung:** 
1. Reduktion der maximal erlaubten Überladungen auf **maximal 3** (früherer Standardwert im Linter war großzügiger).
2. Verbot von Überladungs-Signaturen, die sich ausschließlich in primitiven Datentypen (z. B. `int`, `long`, `string`, `bool`) bei gleicher Parameteranzahl unterscheiden. 

Entwickler und KIs werden gezwungen, Methoden explizit und semantisch eindeutig zu benennen (z. B. `ProcessIntId(int id)` und `ProcessGuidId(Guid id)`), was die Mehrdeutigkeit für das LLM eliminiert.

---

## Realistischer Impact
* **Reduktion von Aufruffehlern (Kategorie 4):** Hoch. Der Agent muss nicht rätseln, welche Überladung aufzurufen ist, da jede Methode einen eineindeutigen Namen trägt.
* **Kompakterer Kontext:** Mittel. Weniger Überladungs-Dokumentation im AST und in den XML-Docs entlastet das Arbeitsgedächtnis des LLMs.

---

## Konkrete Regeln & Heuristik
1. **Obergrenze für Überladungen:** Jede Methode darf maximal **3** Überladungen besitzen (konfigurierbar über `MaxMethodOverloads` in `rules.json`).
2. **Primitive Überladungen verbieten:** Zwei Methoden mit identischem Namen dürfen nicht dieselbe Anzahl an Parametern haben, wenn an denselben Positionen primitive C#-Datentypen (`int`, `long`, `string`, `bool`, `double`, `float`, `decimal`, `char`, `byte`, `Guid`) gegeneinander ausgetauscht werden.
3. **Ausnahme:** Reine Testmethoden (in Testklassen) werden von dieser Regel ignoriert.

---

## Code-Vorschläge & Referenzen

### 1. Erweiterung der Konfiguration
In [LinterConfig.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/LinterConfig.cs#L60-L77) passen wir den Standardwert von `MaxMethodOverloads` in `MetricsConfig` an:

```csharp
public sealed record MetricsConfig
{
    // ...
    // Auf 3 reduziert statt bisher 10
    public int MaxMethodOverloads { get; init; } = 3; 
}
```

Zusätzlich fügen wir eine globale Aktivierung für die primitive Typenprüfung hinzu:
```csharp
public sealed record GlobalConfig
{
    // ...
    /// <summary>
    /// Verbietet Methodenueberladungen, die sich nur durch primitive Typen unterscheiden.
    /// </summary>
    public bool PreventContextDependentOverloads { get; init; } = true;
}
```

In [RuleMetadataRegistry.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/RuleMetadataRegistry.cs#L8-L28):
```csharp
private static readonly IReadOnlyDictionary<string, RuleMetadataEntry> Defaults =
    new Dictionary<string, RuleMetadataEntry>(StringComparer.Ordinal)
    {
        // ...
        ["PreventContextDependentOverloads"] = new() { Severity = "error", Intent = "agent-context" },
    };
```

### 2. Implementierung des Analyzers
Wir ändern die Methode `CheckMethodOverloads` in [LinterAnalyzer.Scope.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Scope.cs#L80-L98) ab:

```csharp
private void CheckMethodOverloads(TypeDeclarationSyntax node)
{
    if (_isTestFile) return;

    var methods = node.Members.OfType<MethodDeclarationSyntax>().ToList();
    
    foreach (var group in methods.GroupBy(static m => m.Identifier.Text))
    {
        var count = group.Count();
        
        // 1. Maximale Anzahl pruefen
        if (count > _config.Metrics.MaxMethodOverloads)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(group.First()),
                RuleName = "MaxMethodOverloads",
                Details = $"Der Typ '{node.Identifier.Text}' deklariert {count} Ueberladungen fuer die Methode '{group.Key}' (erlaubt sind maximal {_config.Metrics.MaxMethodOverloads}).",
                Guidance = "Reduziere die Anzahl der Ueberladungen, indem du unterschiedliche, sprechende Methodennamen waehlst."
            });
        }

        // 2. Primitive Unterscheidung pruefen
        if (_config.Global.PreventContextDependentOverloads && count > 1)
        {
            CheckPrimitiveOverloadConflicts(group.ToList(), node.Identifier.Text);
        }
    }
}

private void CheckPrimitiveOverloadConflicts(List<MethodDeclarationSyntax> methodGroup, string typeName)
{
    for (int i = 0; i < methodGroup.Count; i++)
    {
        for (int j = i + 1; j < methodGroup.Count; j++)
        {
            var methodA = methodGroup[i];
            var methodB = methodGroup[j];

            if (ArePrimitiveOverloadConflicts(methodA, methodB))
            {
                _violations.Add(new RuleViolation
                {
                    FilePath = _filePath,
                    LineNumber = GetLineNumber(methodB),
                    RuleName = "PreventContextDependentOverloads",
                    Details = $"Die Methode '{methodB.Identifier.Text}' steht im Konflikt mit einer Ueberladung in Zeile {GetLineNumber(methodA)}. Beide unterscheiden sich nur in primitiven Typen.",
                    Guidance = "Verwende explizite Methodennamen (z.B. 'ProcessInt' statt 'Process'), um Mehrdeutigkeiten fuer KI-Agenten zu eliminieren."
                });
            }
        }
    }
}

private bool ArePrimitiveOverloadConflicts(MethodDeclarationSyntax a, MethodDeclarationSyntax b)
{
    var paramsA = a.ParameterList.Parameters;
    var paramsB = b.ParameterList.Parameters;

    if (paramsA.Count != paramsB.Count) return false;

    var hasPrimitiveDiff = false;
    for (int i = 0; i < paramsA.Count; i++)
    {
        if (paramsA[i].Type == null || paramsB[i].Type == null) return false;

        var typeSymbolA = _semanticModel.GetTypeInfo(paramsA[i].Type!).Type;
        var typeSymbolB = _semanticModel.GetTypeInfo(paramsB[i].Type!).Type;

        if (typeSymbolA == null || typeSymbolB == null) return false;

        if (!SymbolEqualityComparer.Default.Equals(typeSymbolA, typeSymbolB))
        {
            if (IsPrimitiveType(typeSymbolA) && IsPrimitiveType(typeSymbolB))
            {
                hasPrimitiveDiff = true;
            }
            else
            {
                // Sobald ein komplexer Typ involviert ist, der ungleich ist,
                // gehen wir davon aus, dass die Signaturen ausreichend unterscheidbar sind.
                return false;
            }
        }
    }

    return hasPrimitiveDiff;
}

private static bool IsPrimitiveType(ITypeSymbol symbol)
{
    var name = symbol.Name;
    var primitives = new[] 
    {
        "Int32", "Int64", "Int16", "String", "Boolean", 
        "Double", "Single", "Decimal", "Char", "Byte", "Guid" 
    };
    return primitives.Contains(name) || symbol.SpecialType switch
    {
        SpecialType.System_Int32 => true,
        SpecialType.System_Int64 => true,
        SpecialType.System_String => true,
        SpecialType.System_Boolean => true,
        SpecialType.System_Double => true,
        SpecialType.System_Single => true,
        SpecialType.System_Decimal => true,
        SpecialType.System_Char => true,
        SpecialType.System_Byte => true,
        _ => false
    };
}
```
