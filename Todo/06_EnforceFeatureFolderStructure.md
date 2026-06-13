# Epic: EnforceFeatureFolderStructure (Feature-Ordner-Struktur & Namespace-Mapping erzwingen)

## Big Picture & Intention
Autonome Programmier-Agenten interagieren mit einer Codebasis nicht durch passives "Dauer-Lesen", sondern durch aktives Dateihandling und Tool-Aufrufe (`ls`, `cd`, `grep`, `read_file`). Wenn eine Solution nach klassischen Schichten (z. B. alle DTOs in einem Projekt, alle Business-Services in einem anderen, alle Controller ganz woanders) aufgebaut ist, muss der Agent für eine einzige kleine Feature-Erweiterung dutzende Verzeichnisse durchqueren.

Dies führt zu zwei kritischen Problemen:
1. **Tool-Loop-Latenz:** Jedes Verzeichnis-Listing und jeder Verzeichniswechsel kostet einen separaten Agenten-Schritt (eine LLM-Model-Iteration). Das verlangsamt den Entwicklungsprozess drastisch und erhöht die Kosten.
2. **Context Clutter:** Um ein Feature zu verstehen, muss der Agent Dateien aus völlig unterschiedlichen Pfaden öffnen. Dadurch sammelt sich viel irrelevanter Boilerplate-Context im Arbeitsgedächtnis des Agenten an (**Context Rot / Context Clutter**).

**Die Lösung:** 
Der Linter erzwingt eine **Feature-Folder-Struktur** (Vertical Slices). Alle zusammengehörigen Klassen eines Features (z. B. `CreateUserCommand`, `CreateUserHandler`, `UserDto`, `UserRepository`) müssen physisch im selben Ordner liegen.

Dazu stellt der Linter folgende Bedingungen auf:
1. **Namespace-Ordner-Konformität:** Der Namespace einer C#-Datei muss exakt mit der physischen Verzeichnisstruktur im Projekt übereinstimmen (z. B. muss eine Datei im Ordner `Features/Billing` den Namespace `MyProject.Features.Billing` deklarieren).
2. **Verzeichnistiefe-Begrenzung:** Die Verzeichnisstruktur darf ab dem Projektordner maximal **4 Ebenen** tief sein. Das verhindert verschachtelte Ordner-Labyrinthe.

Dadurch kann der Agent mit einem einzigen Such-/Auflistungsbefehl alle relevanten Quellcodedateien erfassen und in minimalen Schritten bearbeiten.

---

## Realistischer Impact
* **Reduktion von API-Kosten & Zeitaufwand:** Hoch. Reduziert die Anzahl der notwendigen Dateisystem-Toolaufrufe des Agenten um bis zu 40 % bei Feature-Implementierungen.
* **Präziseres RAG & Indexing:** Hoch. Suchvektoren und AST-Chunking-Mechanismen erzielen eine deutlich höhere Treffsicherheit, da physische Verzeichnisgrenzen echten Featuregrenzen entsprechen.

---

## Konkrete Regeln & Heuristik
1. **Namespace-Pfad-Vergleich:** 
   - Ermittle den relativen Pfad der `.cs`-Datei ab dem Speicherort der `.csproj`-Datei (z. B. `Features/Billing/BillingService.cs`).
   - Ermittle den deklarierten Namespace in der Datei (z. B. `MyProject.Features.Billing`).
   - Entferne den Standard-Namespace des Projekts (z. B. `MyProject`).
   - Der verbleibende Namespace-Teil (`Features.Billing`) muss mit den Ordnern im Pfad (`Features/Billing`) übereinstimmen.
2. **Maximale Ordnertiefe:**
   - Zähle die Anzahl der Verzeichnisse im relativen Pfad ab dem Projektordner. Übersteigt diese den Wert `MaxDirectoryDepth` (Standard: 4), wird ein Verstoß gemeldet.

---

## Code-Vorschläge & Referenzen

### 1. Erweiterung der Konfiguration
In [LinterConfig.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/LinterConfig.cs#L34-L55) fügen wir die Regel hinzu:

```csharp
public sealed record GlobalConfig
{
    // ... bestehende Regeln ...
    
    /// <summary>
    /// Erzwingt, dass Namespaces exakt mit der Ordnerstruktur uebereinstimmen.
    /// </summary>
    public bool EnforceNamespaceDirectoryMapping { get; init; } = true;
}

public sealed record MetricsConfig
{
    // ...
    /// <summary>
    /// Maximale Ordnertiefe ab csproj-Ebene.
    /// </summary>
    public int MaxDirectoryDepth { get; init; } = 4;
}
```

In [RuleMetadataRegistry.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/RuleMetadataRegistry.cs#L8-L28):
```csharp
private static readonly IReadOnlyDictionary<string, RuleMetadataEntry> Defaults =
    new Dictionary<string, RuleMetadataEntry>(StringComparer.Ordinal)
    {
        // ...
        ["EnforceNamespaceDirectoryMapping"] = new() { Severity = "error", Intent = "architecture" },
        ["MaxDirectoryDepth"] = new() { Severity = "warning", Intent = "agent-context" },
    };
```

### 2. Implementierung des Analyzers
Wir können diese Prüfung in der Hauptanalyse-Klasse [LinterAnalyzer.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.cs#L80-L87) aufrufen:

```csharp
internal void RunAnalysis()
{
    CheckLineCount();
    CheckNullableEnable();
    CheckNamespaceDirectoryMapping(); // Neue Methode
    Visit(_tree.GetRoot());
    CheckReadonlyFields();
    FilterSuppressedViolations();
}
```

Die Methode `CheckNamespaceDirectoryMapping` fügen wir in [LinterAnalyzer.Scope.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Scope.cs) hinzu:

```csharp
using System.IO;

public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    private void CheckNamespaceDirectoryMapping()
    {
        if (_isTestFile) return;

        // Bestimme relativen Pfad der Datei
        var fileDirectory = Path.GetDirectoryName(_filePath) ?? "";
        if (string.IsNullOrEmpty(fileDirectory)) return;

        // Finde das Verzeichnis der zugehoerigen .csproj
        var projectDir = FindProjectDirectory(fileDirectory);
        if (string.IsNullOrEmpty(projectDir)) return;

        var relativePath = Path.GetRelativePath(projectDir, fileDirectory);
        if (relativePath == "." || string.IsNullOrEmpty(relativePath)) return;

        var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 1. Maximale Ordnertiefe pruefen
        if (pathParts.Length > _config.Metrics.MaxDirectoryDepth)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = 1,
                RuleName = "MaxDirectoryDepth",
                Details = $"Die Dateitiefe betraegt {pathParts.Length} Ordner (erlaubt sind maximal {_config.Metrics.MaxDirectoryDepth} ab csproj).",
                Guidance = "Verflache die Projektstruktur und nutze Feature-Ordner statt tiefer Hierarchien, um KIs die Navigation zu erleichtern."
            });
        }

        if (!_config.Global.EnforceNamespaceDirectoryMapping) return;

        // 2. Namespace-Konformität ermitteln
        var rootNamespace = _projectName ?? Path.GetFileName(projectDir);
        var expectedNamespaceSuffix = string.Join(".", pathParts);

        // Finde den ersten Namespace-Knoten in der Datei
        var namespaceDeclaration = _tree.GetRoot().DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDeclaration != null)
        {
            var declaredNamespace = namespaceDeclaration.Name.ToString();
            
            // Falls der deklarierte Namespace nicht auf den erwarteten Ordnerpfad endet
            if (!declaredNamespace.EndsWith(expectedNamespaceSuffix, StringComparison.OrdinalIgnoreCase))
            {
                _violations.Add(new RuleViolation
                {
                    FilePath = _filePath,
                    LineNumber = GetLineNumber(namespaceDeclaration),
                    RuleName = "EnforceNamespaceDirectoryMapping",
                    Details = $"Der Namespace '{declaredNamespace}' stimmt nicht mit dem physischen Ordnerpfad '{relativePath}' ueberein.",
                    Guidance = $"Passe den Namespace an, sodass er auf '.{expectedNamespaceSuffix}' endet, oder verschiebe die Datei."
                });
            }
        }
    }

    private static string FindProjectDirectory(string startDir)
    {
        var current = startDir;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.GetFiles(current, "*.csproj").Any())
            {
                return current;
            }
            current = Path.GetDirectoryName(current)!;
        }
        return "";
    }
}
```
