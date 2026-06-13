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

## 5. Projekt-spezifische Regel-Konfiguration (Project Overrides)

### Zielsetzung & Konzept
In großen Solutions (z. B. Monolithen mit mehreren Projekten) haben verschiedene Modultypen unterschiedliche Qualitätsanforderungen. Insbesondere **Testprojekte** erfordern oft pragmatischere Regeln als der Produktionscode:
* In Unit Tests sind literale Werte (Magic Values) in `Assert.Equal("Expected", result)` erwünscht und erhöhen die Lesbarkeit, während sie im Business-Code verboten sind.
* Vererbungs- und Kopplungslimits können in Test-Infrastrukturen anders gewichtet sein.

Dieses Feature führt eine optionale Sektion `ProjectOverrides` in der `rules.json` ein. Dadurch können Regeln gezielt für bestimmte Projekte (z. B. über Wildcards wie `*.Tests`) überschrieben werden, während eine zentrale Konfigurationsdatei erhalten bleibt.

### LLM-Impact
* **Nutzen:** KI-Agenten neigen bei striktem Regel-Dogfooding dazu, in Testdateien künstliche Hilfskonstanten anzulegen (z. B. `private const string Exp = "Expected"`), nur um die Magic-Value-Regel zu umgehen. Dies bläht den Testcode auf und erschwert die lineare Kontextanalyse des Modells. Durch gezieltes Deaktivieren dieser Regeln in Testprojekten bleibt der Testcode flach, direkt und für KIs wesentlich schneller erfassbar.

### Technische Umsetzung mit Roslyn
1. Erweiterung der `LinterConfig`-Klasse um ein Dictionary `ProjectOverrides` (Key: Glob-Muster für den Projektnamen, Value: eine Teil-Konfiguration).
2. Beim Start der Analyse eines Dokuments ermitteln wir dessen Roslyn-Projektnamen (`document.Project.Name`).
3. Wir vergleichen den Namen mit den Wildcard-Patterns aus der Konfiguration.
4. Bei einem Treffer mergen wir die globalen Einstellungen mit den Overrides des Projekts (z. B. Überschreiben von `EnforceNoMagicValues = false`).
5. Die Analyse des jeweiligen Dokuments erfolgt mit dieser angepassten Konfigurations-Instanz.

### C#-Implementierungs-Blueprint

**Erweiterung der Konfigurationsstruktur (`LinterConfig.cs`):**
```csharp
public sealed record LinterConfig
{
    public required GlobalConfig Global { get; init; }
    public required MetricsConfig Metrics { get; init; }
    public IReadOnlyDictionary<string, ProjectOverrideEntry> ProjectOverrides { get; init; } 
        = new Dictionary<string, ProjectOverrideEntry>();
}

public sealed record ProjectOverrideEntry
{
    public GlobalConfigOverride? Global { get; init; }
    public MetricsConfigOverride? Metrics { get; init; }
}

// Ermöglicht das selektive Überschreiben einzelner Eigenschaften (Nullable-Properties)
public sealed record GlobalConfigOverride
{
    public bool? EnforceNoMagicValues { get; init; }
    public bool? EnforceSealedClasses { get; init; }
}
```

**Ermittlung der effektiven Konfiguration pro Dokument:**
```csharp
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

public static class ProjectConfigResolver
{
    public static LinterConfig ResolveForDocument(Document document, LinterConfig globalConfig)
    {
        var projectName = document.Project.Name;
        
        foreach (var pattern in globalConfig.ProjectOverrides.Keys)
        {
            if (IsMatch(projectName, pattern))
            {
                return MergeConfig(globalConfig, globalConfig.ProjectOverrides[pattern]);
            }
        }
        
        return globalConfig;
    }

    private static bool IsMatch(string name, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
    }

    private static LinterConfig MergeConfig(LinterConfig global, ProjectOverrideEntry overrides)
    {
        var mergedGlobal = global.Global;
        if (overrides.Global != null)
        {
            mergedGlobal = global.Global with
            {
                EnforceNoMagicValues = overrides.Global.EnforceNoMagicValues ?? global.Global.EnforceNoMagicValues,
                EnforceSealedClasses = overrides.Global.EnforceSealedClasses ?? global.Global.EnforceSealedClasses
            };
        }
        
        return global with { Global = mergedGlobal };
}
}
```

