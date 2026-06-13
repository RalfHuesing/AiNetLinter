# Epic: EnforceExplicitStateImmutability (Strikte Zustand-Immutabilität erzwingen)

## Big Picture & Intention
Für ein autonomes Sprachmodell (LLM), das Code autoregressiv – also sequentiell, Token für Token – generiert, stellt die Verfolgung von veränderlichem Zustand (mutable State) eine erhebliche kognitive Belastung dar. Wenn private Felder oder Properties innerhalb einer Klasse an verschiedenen, weit auseinanderliegenden Stellen modifiziert werden können, muss das LLM diesen Zustand mental mitsimulieren. Das führt in der Praxis extrem häufig zu **State Management Failures (Failure Category 2)**. 

Ein typisches Fehlerbild: Der KI-Agent ändert eine Eigenschaft einer Klasse, übersieht aber, dass ein anderes Feld synchron dazu angepasst werden müsste, was zu korrupten In-Memory-Zuständen führt. 

Durch das Erzwingen von absoluter Immutabilität für alle Klassen (die nicht explizit als reine Datencontainer bzw. DTOs deklariert sind) wird der Datenfluss deklarativ und funktional. Zustandstransitionen müssen als explizite Zuweisungen modelliert werden (z. B. `GameState ApplyMove(Move m)` statt einer void-Methode, die interne Zustände mutiert). Dadurch wird der Datenfluss für das LLM rein funktional und lokal nachvollziehbar, was die Halluzinationsrate bei der Code-Generierung massiv senkt.

---

## Realistischer Impact
* **Reduktion von Logikfehlern (Kategorie 2):** Sehr hoch. Da Zustände nicht mehr unbemerkt mutieren können, schlagen unvollständige Zustandsänderungen direkt zur Kompilierzeit fehl.
* **Reduktion der Token-Kosten & Context Clutter:** Mittel. Der Code wird durch funktionale Übergabestrukturen kompakter und sauberer.
* **Compiler-Feedback:** Sofortige Rückmeldung bei Zuweisungsversuchen, wodurch der Agent in seiner Self-Correction-Loop direkt geleitet wird.

---

## Konkrete Regeln & Heuristik
1. **Ausschluss-Kriterien (DTO-Ausnahme):** Typen, die als reine Datenübertragungs-Objekte fungieren, sind von der Immutabilitäts-Prüfung ausgenommen. Ein Typ gilt als DTO/Entität, wenn:
   - Der Klassenname auf `Dto`, `Entity`, `Model`, `Request` oder `Response` endet.
   - Die Klasse mit einem benutzerdefinierten Attribut wie `[Dto]` oder `[Entity]` annotiert ist.
2. **Property-Prüfung:** Jede Eigenschaft der Klasse darf keinen `set`-Accessor besitzen. Sie muss entweder get-only sein oder den `init`-Modifier verwenden (`public string Name { get; init; }`).
3. **Feld-Prüfung:** Alle privaten und geschützten Felder müssen als `readonly` deklariert sein.
4. **Zuweisungs-Prüfung:** Es dürfen keine Methoden existieren, die Felder außerhalb des Konstruktors modifizieren.

---

## Code-Vorschläge & Referenzen

### 1. Erweiterung der Konfiguration
In [LinterConfig.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/LinterConfig.cs#L34-L55) fügen wir die neue Regel hinzu:

```csharp
public sealed record GlobalConfig
{
    // ... bestehende Regeln ...
    
    /// <summary>
    /// Erzwingt, dass Klassen (außer DTOs/Entities) strukturell unveränderlich (immutable) sein müssen.
    /// </summary>
    public bool EnforceExplicitStateImmutability { get; init; } = true;
}
```

Und in [RuleMetadataRegistry.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/RuleMetadataRegistry.cs#L8-L28):

```csharp
private static readonly IReadOnlyDictionary<string, RuleMetadataEntry> Defaults =
    new Dictionary<string, RuleMetadataEntry>(StringComparer.Ordinal)
    {
        // ...
        ["EnforceExplicitStateImmutability"] = new() { Severity = "error", Intent = "agent-resilience" },
    };
```

### 2. Implementierung des Analyzers
Wir erstellen eine neue Datei namens `LinterAnalyzer.Immutability.cs` im Verzeichnis [Core](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core) oder ergänzen die bestehende [LinterAnalyzer.State.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.State.cs):

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
    private void CheckClassImmutability(ClassDeclarationSyntax node)
    {
        if (!_config.Global.EnforceExplicitStateImmutability) return;
        if (_isTestFile) return;

        var className = node.Identifier.Text;
        
        // DTOs/Entities überspringen
        if (IsDtoOrEntity(node, className)) return;

        // 1. Prüfe Eigenschaften auf veränderbare Set-Accessor
        foreach (var prop in node.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (prop.AccessorList != null)
            {
                var setAccessor = prop.AccessorList.Accessors
                    .FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));

                if (setAccessor != null && !setAccessor.Modifiers.Any(SyntaxKind.InitKeyword))
                {
                    _violations.Add(new RuleViolation
                    {
                        FilePath = _filePath,
                        LineNumber = GetLineNumber(prop),
                        RuleName = "EnforceExplicitStateImmutability",
                        Details = $"Die Eigenschaft '{prop.Identifier.Text}' der Klasse '{className}' hat einen 'set'-Accessor statt 'init'.",
                        Guidance = "Verwende 'init' oder mache die Eigenschaft get-only, um Immutability zu garantieren."
                    });
                }
            }
        }

        // 2. Prüfe private/protected Felder auf readonly
        foreach (var fieldDecl in node.Members.OfType<FieldDeclarationSyntax>())
        {
            var isConst = fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword);
            var isReadonly = fieldDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
            
            if (!isConst && !isReadonly)
            {
                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    _violations.Add(new RuleViolation
                    {
                        FilePath = _filePath,
                        LineNumber = GetLineNumber(variable),
                        RuleName = "EnforceExplicitStateImmutability",
                        Details = $"Das Feld '{variable.Identifier.Text}' in der Klasse '{className}' ist nicht als 'readonly' deklariert.",
                        Guidance = "Füge den Modifikator 'readonly' hinzu."
                    });
                }
            }
        }
    }

    private bool IsDtoOrEntity(ClassDeclarationSyntax node, string className)
    {
        // Namens-Heuristik
        var suffixes = new[] { "Dto", "Entity", "Model", "Request", "Response", "Command" };
        if (suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Attribut-Heuristik
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            var attributes = symbol.GetAttributes();
            return attributes.Any(attr => 
            {
                var name = attr.AttributeClass?.Name ?? "";
                return name.Contains("Dto") || name.Contains("Entity");
            });
        }

        return false;
    }
}
```

Diese Prüfmethode rufen wir in `VisitClassDeclaration` innerhalb von [LinterAnalyzer.Architecture.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Architecture.cs#L23-L49) auf:

```csharp
public override void VisitClassDeclaration(ClassDeclarationSyntax node)
{
    // ... bestehende Prüfungen ...
    CheckClassImmutability(node);
    // ...
    base.VisitClassDeclaration(node);
}
```
