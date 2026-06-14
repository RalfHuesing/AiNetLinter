# Plan 08 — P2: Generierter Code — Datei-Ausschlussmuster

**Priorität:** P2  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

AiNetLinter analysiert derzeit **alle** `.cs`-Dateien ohne Ausnahme für auto-generierte Dateien. Das führt zu sinnlosen Meldungen für Code der:
- nicht bearbeitet werden kann (`*.designer.cs`, `*.g.cs`)
- nicht Ausdruck von Design-Entscheidungen ist (WPF-Designer, Roslyn-Source-Generators)
- Sonderkonventionen folgt die für manuellen Code nicht gelten (globale Usings, AssemblyInfo)

**Betroffene Regeln:**
- `MaxLineCount`: `*.designer.cs` kann tausende Zeilen haben
- `EnforceNullableEnable`: Generierte Dateien setzen `#nullable enable` nicht immer
- `EnforceSealedClasses`: Generierte Partial-Klassen sind nie sealed
- `StaticTestSentinel`: Generierte Klassen sollen nicht auf Testabdeckung geprüft werden
- `EnforcePascalCase`: Generierte Code kann unkonventionelle Namen enthalten

**Typische generierte Dateien:**
| Pattern | Quelle |
|---------|--------|
| `*.designer.cs` | WPF/Winforms XAML-Compiler |
| `*.g.cs` | Roslyn Source Generators |
| `*.generated.cs` | Code-Generatoren (T4, Swagger, etc.) |
| `AssemblyInfo.cs` | SDK-generiert |
| `GlobalUsings.g.cs` | SDK-Implicit-Usings |
| `*.AssemblyAttributes.cs` | SDK-generiert |
| `obj/**/*.cs` | Build-Output (bereits teilweise gefiltert?) |

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterEngine.cs` | Zeile 66+ — `RunInternalAsync()` / `AnalyzeSolutionAsync()` |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | Neue `FiltersConfig` oder direkt in `LinterConfig` |
| `rules.json` | Neue Top-Level-Sektion `FileFilters` |

---

## Konfigurationsänderung

### `rules.json`:
```json
"FileFilters": {
  "ExcludeFilePatterns": [
    "*.designer.cs",
    "*.g.cs",
    "*.generated.cs",
    "AssemblyInfo.cs",
    "*.AssemblyAttributes.cs"
  ],
  "ExcludeDirectoryPatterns": [
    "obj/",
    "bin/"
  ]
}
```

`ExcludeFilePatterns`: Dateinamen-Glob-Muster (nur Dateiname, nicht Pfad). Dateien deren **Name** auf eines dieser Muster passt, werden vollständig übersprungen.

`ExcludeDirectoryPatterns`: Pfad-Segmente. Dateien in Verzeichnissen die diese Segmente enthalten, werden übersprungen.

---

## Implementierungsvorschlag

### Neues Record in `LinterConfig.cs`:

```csharp
/// <summary>
/// Datei- und Verzeichnis-Ausschlüsse für die Linter-Analyse.
/// </summary>
public sealed record FileFiltersConfig
{
    /// <summary>
    /// Glob-Muster die gegen den Dateinamen (ohne Pfad) geprüft werden.
    /// Standard-Wildcards: * und ?
    /// Beispiel: ["*.designer.cs", "*.g.cs"]
    /// </summary>
    public IReadOnlyCollection<string> ExcludeFilePatterns { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Pfad-Segmente: Dateien die eines dieser Segmente im Pfad enthalten, werden übersprungen.
    /// Beispiel: ["obj/", "bin/", ".generated/"]
    /// </summary>
    public IReadOnlyCollection<string> ExcludeDirectoryPatterns { get; init; }
        = ["obj/", "bin/"];
}
```

`LinterConfig` erweitern:
```csharp
public sealed record LinterConfig
{
    // ...
    public FileFiltersConfig FileFilters { get; init; } = new();
}
```

### Hilfsmethode — `FileFilterEvaluator.cs` (neu in `Configuration/`):

```csharp
// src/AiNetLinter/Configuration/FileFilterEvaluator.cs
#nullable enable

namespace AiNetLinter.Configuration;

internal static class FileFilterEvaluator
{
    public static bool IsExcluded(string filePath, FileFiltersConfig filters)
    {
        var fileName = Path.GetFileName(filePath);

        foreach (var pattern in filters.ExcludeFilePatterns)
        {
            if (MatchesGlob(fileName, pattern)) return true;
        }

        // Normalisiere Pfad-Trennzeichen
        var normalizedPath = filePath.Replace('\\', '/');
        foreach (var dirPattern in filters.ExcludeDirectoryPatterns)
        {
            if (normalizedPath.Contains(dirPattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool MatchesGlob(string input, string pattern)
    {
        // Einfaches Glob: * und ? unterstützen
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(
            input, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
```

### Integration in `LinterEngine.cs`

Die genaue Integrationsstelle hängt davon ab, wie `AnalyzeSolutionAsync` Dokumente iteriert. Prinzipiell:

```csharp
// Beim Iterieren der Solution-Dokumente:
foreach (var document in project.Documents)
{
    if (document.FilePath == null) continue;

    // NEU: Ausschluss-Check
    if (FileFilterEvaluator.IsExcluded(document.FilePath, _config.FileFilters))
        continue;

    // ... bestehende Analyse ...
}
```

---

## Alternativ: `[GeneratedCode]`-Attribut-Erkennung

Zusätzlich zur Dateinamen-Filterung können Dateien mit `[System.CodeDom.Compiler.GeneratedCode]`-Attribut auf Klassen-Ebene ebenfalls ausgeschlossen werden:

```csharp
// In LinterAnalyzer beim Besuchen einer Klasse
private bool IsGeneratedCode(ClassDeclarationSyntax node)
{
    var symbol = _semanticModel.GetDeclaredSymbol(node);
    if (symbol == null) return false;

    return symbol.GetAttributes().Any(a =>
        a.AttributeClass?.Name == "GeneratedCodeAttribute");
}
```

Falls erkannt → alle Checks für diese Klasse überspringen.

Diese Erkennung ist Optional (opt-in per Config: `"SkipGeneratedCodeAttribute": true`).

---

## Tests

**Datei:** `src/AiNetLinter.Tests/FileFilterTests.cs` (neu)

### Dateiname-Ausschluss:
```
*.designer.cs → "MainWindow.designer.cs" → ausgeschlossen
*.g.cs       → "MyApp.GlobalUsings.g.cs" → ausgeschlossen
*.g.cs       → "MyService.cs" → NICHT ausgeschlossen
```

### Pfad-Ausschluss:
```
"obj/"  → "C:/src/MyApp/obj/Debug/net10.0/MyApp.g.cs" → ausgeschlossen
"bin/"  → "C:/src/MyApp/bin/Release/MyApp.cs" → ausgeschlossen
"obj/"  → "C:/src/MyApp/src/MyService.cs" → NICHT ausgeschlossen
```

### Integration-Test: Kein MaxLineCount-Verstoß für *.designer.cs:
- Fixture-Datei `MainWindow.designer.cs` mit 600 Zeilen
- Konfiguriert: `ExcludeFilePatterns: ["*.designer.cs"]`
- Erwartung: 0 Verstöße

### Edge Cases:
- `ExcludeFilePatterns` leer → kein File ausgeschlossen
- Muster ohne Wildcard: `"GlobalUsings.cs"` → exakter Dateiname-Match
- Groß-/Kleinschreibung: `"*.Designer.cs"` trifft auch `"form.designer.cs"` (case-insensitive)
- `ExcludeDirectoryPatterns` mit und ohne trailing Slash

---

## README-Anforderungen

Neuer Abschnitt `FileFilters-Konfiguration`:
- `ExcludeFilePatterns` und `ExcludeDirectoryPatterns` erklären
- Standard-Empfehlung für typische .NET-Projekte als Snippet
- WPF-spezifisches Snippet (mit `*.designer.cs`)
- Hinweis: `obj/` und `bin/` sind im Default bereits ausgeschlossen

---

## Architektur-Hinweise

- `FileFiltersConfig` als neues Record in `LinterConfig.cs`
- `FileFilterEvaluator` als neue `internal static` Klasse in `Configuration/`
- Kein Einfluss auf bestehende `SuppressionCommentParser` oder `BaselineReader`
- `obj/`-Ordner-Ausschluss: prüfen ob LinterEngine `obj/`-Dateien bereits excludiert; wenn nicht, `bin/` und `obj/` in den Default-`ExcludeDirectoryPatterns` aufnehmen
