# Spezifikation der AI-Developer Experience (AI-DX) Features

Dieses Dokument beschreibt Konzepte, LLM-Relevanz, technische Machbarkeit und Implementierungs-Blueprints (C#-Codebeispiele) für die vier geplanten AI-DX Features im `AiNetLinter`.

---

## 1. AI-Context-Footprint (Transitive Codezeilen-Metrik)

### Zielsetzung & Konzept
KI-Agenten benötigen zur sicheren Modifikation einer Klasse oft auch den Kontext aller Klassen, von denen diese direkt oder transitiv abhängt. Ist dieser transitive Kontext zu groß, leidet das LLM unter **Attention Dilution** (Aufmerksamkeitsverlust).
Der **AI-Context-Footprint** berechnet die Summe aller Codezeilen der Klasse selbst plus aller transitiv im Quellcode referenzierten eigenen Klassen/Typen. 

### LLM-Impact
* **Nutzen:** Die Metrik warnt Entwickler vor hoher Kopplung und zeigt der KI an, wie hoch das Risiko für unvollständige Code-Generierung im aktuellen Vektor-Raum ist.
* **Gefahr bei Ignorieren:** Steigt der transitive Kontext über 5.000 Zeilen, steigt die Fehlerrate bei KI-Refactorings nachweislich an.

### Technische Umsetzung mit Roslyn
1. Startend bei einer Klassendeklaration traversieren wir deren Member (Felder, Eigenschaften, Parameter, lokale Variablen).
2. Über das `SemanticModel` ermitteln wir deren Typ-Symbole (`INamedTypeSymbol`).
3. Wir filtern Symbole, die in der eigenen Solution liegen (`Symbol.DeclaringSyntaxReferences` vorhanden).
4. Wir wiederholen diesen Schritt rekursiv (DFS/BFS mit Cycle-Erkennung), um die transitive Menge aller beteiligten Quellcodedokumente zu ermitteln.
5. Wir summieren die Zeilenlängen (`SyntaxTree.GetText().Lines.Count`) dieser Dateien.

### C#-Implementierungs-Blueprint
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public sealed class AIContextFootprintCalculator
{
    public static int Calculate(INamedTypeSymbol classSymbol, SemanticModel semanticModel)
    {
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        QueueSymbols(classSymbol, visited);
        
        int totalLines = 0;
        foreach (var symbol in visited)
        {
            foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
            {
                var syntaxNode = syntaxRef.GetSyntax();
                totalLines += syntaxNode.SyntaxTree.GetText().Lines.Count;
            }
        }
        return totalLines;
    }

    private static void QueueSymbols(INamedTypeSymbol symbol, HashSet<INamedTypeSymbol> visited)
    {
        if (symbol == null || !visited.Add(symbol)) return;

        // Untersuche alle Member (Kopplungspunkte)
        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol field)
                QueueSymbols(field.Type as INamedTypeSymbol, visited);
            else if (member is IPropertySymbol prop)
                QueueSymbols(prop.Type as INamedTypeSymbol, visited);
            else if (member is IMethodSymbol method)
            {
                QueueSymbols(method.ReturnType as INamedTypeSymbol, visited);
                foreach (var param in method.Parameters)
                {
                    QueueSymbols(param.Type as INamedTypeSymbol, visited);
                }
            }
        }
    }
}
```

---

## 2. Automatisch generiertes Repo-Playbook

### Zielsetzung & Konzept
Jedes Softwareprojekt besitzt ungeschriebene Codierungsrichtlinien. Das **Repo-Playbook** scannt die bestehende Codebase und fasst diese Erkenntnisse in einer Markdown-Datei `.cursor/rules/playbook.md` (oder `.github/playbook.md`) zusammen. KIs können diese Datei beim Start laden, um sich an die Gewohnheiten des Repositories anzupassen.

### LLM-Impact
* **Nutzen:** Die KI „weiß“ sofort, ob das Projekt das Result-Pattern verwendet, welche Fehlerklassen üblich sind und welche Linter-Regeln im Team standardmäßig per inline Suppression (`// ainetlinter-disable`) unterdrückt werden.

### Technische Umsetzung mit Roslyn
1. **Suppression-Statistik:** Zählen aller `// ainetlinter-disable [RuleName]` im Projekt zur Identifikation von „Schwachstellen“ oder bewussten Design-Abweichungen.
2. **Architekturmuster-Scan:** Statische Zählung von Entwurfsmustern (z. B. Anzahl Rückgabetypen `Result<T>` vs. `throw`).
3. **Template-Generierung:** Schreiben der aggregierten Daten in eine Markdown-Datei.

### C#-Implementierungs-Blueprint
```csharp
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

public sealed class RepoPlaybookGenerator
{
    public static void Generate(string outputPath, ConcurrentDictionary<string, int> suppressionCounts, int resultPatternCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AI Repository Playbook (Auto-Generated)");
        sb.AppendLine("Dieses Dokument wurde automatisiert erzeugt. Verwende es als Orientierung.");
        sb.AppendLine();
        sb.AppendLine("## 1. Genutzte Architekturmuster");
        sb.AppendLine($"- **Result-Pattern-Nutzung:** {resultPatternCount} Methoden liefern `Result<T>` zurueck.");
        sb.AppendLine();
        sb.AppendLine("## 2. Abweichungen / Unterdrueckte Linter-Regeln");
        sb.AppendLine("Folgende Regeln werden in diesem Projekt bewusst unterdrueckt:");
        
        foreach (var item in suppressionCounts.OrderByDescending(x => x.Value))
        {
            sb.AppendLine($"- **{item.Key}:** {item.Value} mal deaktiviert.");
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }
}
```

---

## 3. Roslyn-basierter CLI Auto-Fixer (`--fix`)

### Zielsetzung & Konzept
Triviale Linter-Verstöße (wie das Fehlen von `sealed` bei konkreten Klassen, `readonly` bei Feldern oder `#nullable enable`) kosten KI-Agenten wertvolle Prompt-Zyklen. Die Option `ainetlinter --fix` behebt diese Fehler automatisiert.

### LLM-Impact
* **Nutzen:** Der KI-Agent führt die Validierung durch und behebt 80 % der Fehler in Millisekunden über die CLI. Nur noch komplexe logische Fehler müssen vom LLM manuell gelöst werden.

### Technische Umsetzung mit Roslyn
1. Im `MSBuildWorkspace` deklarieren wir Schreibrechte.
2. Wir nutzen Roslyn Syntax-Transformationen (`Document.WithSyntaxRoot`), um den Syntaxbaum zu manipulieren.
3. Wir wenden die Änderungen direkt auf den Workspace an (`Workspace.TryApplyChanges`).

### C#-Implementierungs-Blueprint
```csharp
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public sealed class LinterAutoFixer
{
    public static async Task<bool> FixSealedClassesAsync(Workspace workspace, ProjectId projectId)
    {
        var project = workspace.CurrentSolution.GetProject(projectId);
        if (project == null) return false;

        var solution = workspace.CurrentSolution;

        foreach (var document in project.Documents)
        {
            var root = await document.GetSyntaxRootAsync();
            if (root == null) continue;

            // Suche unversiegelte konkrete Klassen
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(c => !c.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword) || 
                                                  m.IsKind(SyntaxKind.StaticKeyword) || 
                                                  m.IsKind(SyntaxKind.AbstractKeyword)));

            if (!classes.Any()) continue;

            var newRoot = root.ReplaceNodes(classes, (oldNode, newNode) =>
            {
                var sealedToken = SyntaxFactory.Token(SyntaxKind.SealedKeyword);
                return oldNode.AddModifiers(sealedToken);
            });

            solution = solution.WithDocumentSyntaxRoot(document.Id, newRoot);
        }

        return workspace.TryApplyChanges(solution);
    }
}
```

---

## 4. Semantische Diff-Impact-Analyse

### Zielsetzung & Konzept
Ändert eine KI eine öffentliche Methodensignatur, muss sie alle Aufrufstellen anpassen. Die **Impact-Analyse** scannt das Git Diff, ermittelt die geänderten Methodensymbole und listet alle referenzierten Call-Sites in der Solution auf.

### LLM-Impact
* **Nutzen:** Die KI erhält einen präzisen Fahrplan ("Modifikations-Pfad"). Sie weiß vor der Compilation genau, welche anderen Quelldateien ebenfalls angefasst werden müssen.

### Technische Umsetzung mit Roslyn
1. Git-Diff auswerten, um die geänderten Zeilen zu bestimmen.
2. Den geänderten Syntaxknoten (z. B. `MethodDeclarationSyntax`) im Dokument lokalisieren.
3. Das deklarierte Symbol (`IMethodSymbol`) über das `SemanticModel` auflösen.
4. Mittels `SymbolFinder.FindReferencesAsync(symbol, solution)` alle Aufrufstellen in der gesamten Projektmappe finden.
5. Die Liste der Dateipfade und Zeilennummern als kompakte Liste an stdout ausgeben.

### C#-Implementierungs-Blueprint
```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

public sealed class DiffImpactAnalyzer
{
    public static async Task<List<string>> FindImpactedCallSitesAsync(ISymbol changedSymbol, Solution solution)
    {
        var impactList = new List<string>();
        var references = await SymbolFinder.FindReferencesAsync(changedSymbol, solution);

        foreach (var referencedSymbol in references)
        {
            foreach (var location in referencedSymbol.Locations)
            {
                var lineSpan = location.Location.GetLineSpan();
                var filePath = lineSpan.Path;
                var line = lineSpan.StartLinePosition.Line + 1;
                impactList.Add($"{filePath}:{line} - Aufrufstelle von '{changedSymbol.Name}'");
            }
        }

        return impactList;
    }
}
```

---

## 5. Dynamischer, LLM-orientierter Codegraph

### Zielsetzung & Konzept
Um sich in einer fremden oder großen Codebase zurechtzufinden, benötigt eine KI eine kompakte, semantische Übersicht über die Systemarchitektur. Die Option `ainetlinter --graph <pfad.md>` (oder `-g`) analysiert die gesamte Solution und generiert ein aktualisiertes, visuelles Abhängigkeitsdiagramm im Mermaid-Format sowie eine Übersicht der Rollen direkt in den Projekt-Dokumenten.

### LLM-Impact
* **Nutzen:** Anstatt dass die KI mühsam Dutzende Quelldateien einlesen muss, um die Beziehungen von Klassen und Interfaces zu verstehen, lädt sie einfach das generierte Diagramm. Dies schützt vor Fehlannahmen bezüglich Vererbungshierarchien, spart Kontext-Token und verkürzt die Einarbeitungszeit autonomer Agenten signifikant.

### Technische Umsetzung mit Roslyn
1. Traversieren aller Projekte und Dokumente in der Solution.
2. Ermittlung aller deklarierten Typen (Klassen, Interfaces, Structs) über die Syntax-Deklarationen und das `SemanticModel`.
3. Auswertung der Vererbung (`BaseType`) und der Interface-Implementierung (`Interfaces`).
4. Erfassung von Assoziationsbeziehungen (Typen von privaten Feldern, Properties, Konstruktor-Parametern).
5. Filterung auf projektinterne Typen (Ausschluss von Typen aus `System`, `Microsoft` oder Third-Party-NuGet-Paketen), um das Diagramm übersichtlich zu halten.
6. Generierung und Ausgabe von Mermaid-Klassendiagramm-Syntax (`classDiagram`) in eine Markdown-Datei.

### C#-Implementierungs-Blueprint
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public sealed class CodegraphGenerator
{
    public static async Task GenerateAsync(Solution solution, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Codebase-Architektur & Abhaengigkeitsgraph (Auto-Generated)");
        sb.AppendLine("Dieses Dokument visualisiert die interne Klassenstruktur und deren Beziehungen.");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("classDiagram");

        var allTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        
        // 1. Sammle alle im Quellcode deklarierten Typen der Solution
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                if (semanticModel == null) continue;
                
                var root = await document.GetSyntaxRootAsync();
                if (root == null) continue;
                
                var classNodes = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
                foreach (var classNode in classNodes)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(classNode);
                    if (symbol is INamedTypeSymbol namedType)
                    {
                        allTypes.Add(namedType);
                    }
                }
            }
        }

        // 2. Generiere Mermaid-Klassen und Beziehungen
        foreach (var type in allTypes)
        {
            var typeName = type.Name;
            sb.AppendLine($"    class {typeName} {{");
            
            // Fuege wichtige public Methoden hinzu
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.DeclaredAccessibility == Accessibility.Public && !m.IsImplicitlyDeclared))
            {
                sb.AppendLine($"        +{method.Name}()");
            }
            sb.AppendLine("    }");

            // Vererbung & Interface-Implementierung (Generalisierung)
            if (type.BaseType != null && allTypes.Contains(type.BaseType))
            {
                sb.AppendLine($"    {type.BaseType.Name} <|-- {typeName} : erbt");
            }
            
            foreach (var iface in type.Interfaces)
            {
                if (allTypes.Contains(iface))
                {
                    sb.AppendLine($"    {iface.Name} <|.. {typeName} : implementiert");
                }
            }

            // Abhaengigkeiten / Kompositionen (Felder & Properties)
            var referencedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.Type is INamedTypeSymbol fieldType && allTypes.Contains(fieldType))
                    referencedTypes.Add(fieldType);
            }
            foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (prop.Type is INamedTypeSymbol propType && allTypes.Contains(propType))
                    referencedTypes.Add(propType);
            }

            foreach (var dep in referencedTypes)
            {
                sb.AppendLine($"    {typeName} --> {dep.Name} : nutzt");
            }
        }

        sb.AppendLine("```");
        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
}
}
```

