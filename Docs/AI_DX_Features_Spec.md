# Spezifikation der AI-Developer Experience (AI-DX) Features

Dieses Dokument beschreibt Konzepte, LLM-Relevanz, technische Machbarkeit und Implementierungs-Blueprints (C#-Codebeispiele) für die geplanten AI-DX Features im `AiNetLinter`.

---

## 1. Roslyn-basierter CLI Auto-Fixer (`--fix`)

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

## 2. Semantische Diff-Impact-Analyse

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
