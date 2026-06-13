# Epic: EnforceStrictBoundaryForBusinessLogic (Isolierung von Berechnungen erzwingen)

## Big Picture & Intention
Ein häufiges Versagensmuster autonomer Programmier-Agenten ist der **Business Logic Mismatch (Failure Category 3)**. KIs neigen dazu, reine mathematische und logische Berechnungen (z. B. Steuersätze, Preisrabatte, Zinsberechnungen) untrennbar mit Infrastruktur-Operationen (Datenbank-Abfragen, HTTP-Calls, Dateizugriffen) oder veränderlichen UI-Zuständen zu vermischen.

Das macht den Code extrem schwer testbar. Wenn ein Agent den Algorithmus anpassen und per Unit-Test validieren soll (im Rahmen der RLVR-Feedbackschleife), muss er zuerst komplexe Datenbankverbindungen mocken, was zu langsamen, instabilen Tests führt. Oft schlagen Tests dann aufgrund von Infrastruktur-Fehlern fehl, obwohl die Berechnungslogik der KI korrekt war, was den Agenten verwirrt.

**Die Lösung:** Der Linter erzwingt eine strikte architektonische Trennung (Functional Core, Imperative Shell). Berechnungsintensive oder regelbasierte Logik muss in zustandslosen, als `static` deklarierten Methoden liegen. Diese Methoden dürfen keinerlei I/O-Abhängigkeiten oder Abhängigkeiten zu Datenbank-Contexten besitzen. Sie erhalten alle Eingabedaten als Parameter und geben das berechnete Ergebnis zurück.

Dadurch kann der KI-Agent extrem schnelle, I/O-freie Unit-Tests schreiben und ausführen, um seine logische Änderung in Millisekunden deterministisch abzusichern.

---

## Realistischer Impact
* **Stärkung der Test-Driven Development (TDD) Loop für KIs:** Extrem hoch. Die Ausführung zustandsloser Logik-Tests läuft lokal in Millisekunden ab. Der Agent erhält direktes Feedback ohne Mocking-Overhead.
* **Reduktion von Logikfehlern (Kategorie 3):** Hoch. Der Agent kann Berechnungen lokal fokussieren.
* **Wiederverwendbarkeit:** Erhöht die Code-Qualität auch für menschliche Entwickler deutlich.

---

## Konkrete Regeln & Heuristik
1. **Identifikation von Logik-Methoden:** Eine Methode gilt als logik-/berechnungsintensiv, wenn:
   - Sie zu einer Klasse gehört, die auf `Calculator`, `Rule`, `Policy`, `Engine` oder `Service` endet.
   - ODER sie mathematische Operatoren (`+`, `-`, `*`, `/`, `%`) oder mehr als 2 logische Operatoren (`&&`, `||`) enthält und Kontrollfluss-Strukturen wie `if` oder `switch` nutzt.
2. **Erzwingung von `static`:** Logik-Methoden müssen als `static` deklariert sein.
3. **I/O-Verbot:** Statische Logik-Methoden dürfen keine Typen aufrufen oder als Parameter deklarieren, die I/O repräsentieren. Dazu gehören:
   - Typen, die auf `DbContext`, `Repository`, `Client`, `Connection`, `Store`, `Command` enden.
   - Aufrufe von statischen I/O-Methoden (z. B. `File.Write*`, `HttpClient.Send*`, `Console.Write*`).

---

## Code-Vorschläge & Referenzen

### 1. Erweiterung der Konfiguration
In [LinterConfig.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/LinterConfig.cs#L34-L55) fügen wir die neue Regel hinzu:

```csharp
public sealed record GlobalConfig
{
    // ... bestehende Regeln ...
    
    /// <summary>
    /// Erzwingt die Isolierung reiner Berechnungslogik in zustandslosen (statischen) Methoden ohne I/O.
    /// </summary>
    public bool EnforceStrictBoundaryForBusinessLogic { get; init; } = true;
}
```

Und in [RuleMetadataRegistry.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/RuleMetadataRegistry.cs#L8-L28):

```csharp
private static readonly IReadOnlyDictionary<string, RuleMetadataEntry> Defaults =
    new Dictionary<string, RuleMetadataEntry>(StringComparer.Ordinal)
    {
        // ...
        ["EnforceStrictBoundaryForBusinessLogic"] = new() { Severity = "error", Intent = "architecture" },
    };
```

### 2. Implementierung des Analyzers
Wir erstellen eine neue Datei namens `LinterAnalyzer.BusinessLogic.cs` im Verzeichnis [Core](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core):

```csharp
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Bestehende Logik aus Complexity.cs aufrufen (XML-Docs, PascalCase, ParameterCount, etc.)
        base.VisitMethodDeclaration(node);

        CheckBusinessLogicBoundary(node);
    }

    private void CheckBusinessLogicBoundary(MethodDeclarationSyntax node)
    {
        if (!_config.Global.EnforceStrictBoundaryForBusinessLogic) return;
        if (_isTestFile) return;

        var isPureLogic = IsLogicMethod(node);
        if (!isPureLogic) return;

        // 1. Muss statisch sein
        var isStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword);
        if (!isStatic)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "EnforceStrictBoundaryForBusinessLogic",
                Details = $"Die Logik-Methode '{node.Identifier.Text}' ist nicht als 'static' deklariert.",
                Guidance = "Deklariere die Methode als 'static', um Zustandslosigkeit zu garantieren und Mocks in Unit Tests zu vermeiden."
            });
        }

        // 2. Keine I/O-Abhängigkeiten in Parametern
        foreach (var param in node.ParameterList.Parameters)
        {
            if (param.Type != null)
            {
                var typeSymbol = _semanticModel.GetTypeInfo(param.Type).Type;
                if (typeSymbol != null && IsForbiddenIoType(typeSymbol.Name))
                {
                    _violations.Add(new RuleViolation
                    {
                        FilePath = _filePath,
                        LineNumber = GetLineNumber(param),
                        RuleName = "EnforceStrictBoundaryForBusinessLogic",
                        Details = $"Die Logik-Methode '{node.Identifier.Text}' akzeptiert ein verbotenes I/O-Objekt '{param.Identifier.Text}' vom Typ '{typeSymbol.Name}'.",
                        Guidance = "Uebergib stattdessen nur die notwendigen primitiven Werte oder einfache Value Objects/Records."
                    });
                }
            }
        }

        // 3. Keine I/O-Aufrufe innerhalb des Rumpfes
        if (node.Body != null)
        {
            var invocations = node.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol;
                if (symbol != null)
                {
                    var containingType = symbol.ContainingType?.Name ?? "";
                    if (IsForbiddenIoType(containingType) || IsStaticIoClass(containingType))
                    {
                        _violations.Add(new RuleViolation
                        {
                            FilePath = _filePath,
                            LineNumber = GetLineNumber(invocation),
                            RuleName = "EnforceStrictBoundaryForBusinessLogic",
                            Details = $"Unerlaubter I/O-Aufruf von '{symbol.ToDisplayString()}' innerhalb der zustandslosen Logik-Methode.",
                            Guidance = "Kapsle Berechnungen so, dass sie keine Datenbanken, APIs oder Dateisysteme direkt aufrufen."
                        });
                    }
                }
            }
        }
    }

    private bool IsLogicMethod(MethodDeclarationSyntax node)
    {
        // Klasse prüfen
        var classDecl = node.Parent as ClassDeclarationSyntax;
        if (classDecl != null)
        {
            var className = classDecl.Identifier.Text;
            var logicSuffixes = new[] { "Calculator", "Rule", "Policy", "Engine" };
            if (logicSuffixes.Any(s => className.EndsWith(s, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        // Rumpf prüfen auf Rechenoperatoren oder komplexe Logik-Operatoren
        if (node.Body == null && node.ExpressionBody == null) return false;
        
        var text = node.ToString();
        var hasMath = text.Contains("+") || text.Contains("-") || text.Contains("*") || text.Contains("/");
        var hasComplexLogic = text.Contains("&&") || text.Contains("||") || text.Contains("switch") || text.Contains("if");

        return hasMath && hasComplexLogic;
    }

    private static bool IsForbiddenIoType(string typeName)
    {
        var forbiddenSuffixes = new[] { "DbContext", "Repository", "Client", "Connection", "Store", "HttpClient" };
        return forbiddenSuffixes.Any(s => typeName.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStaticIoClass(string className)
    {
        var ioClasses = new[] { "File", "Directory", "Console", "Path", "Socket" };
        return ioClasses.Contains(className);
    }
}
```
