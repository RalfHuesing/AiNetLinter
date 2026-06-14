# Plan 09 — P2: EnforceNamespaceDirectoryMapping — Feature-Folder-Strategien

**Priorität:** P2  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

Die aktuelle Implementierung (`LinterAnalyzer.Scope.cs:275–298`) erzwingt, dass der Namespace exakt auf den **vollständigen** Ordnerpfad ab csproj endet:

```
expectedSuffix = "Handlers.Domains.Firmenkalender"
declaredNamespace = "MyApp.Handlers.Kalender"
→ "MyApp.Handlers.Kalender".EndsWith("Handlers.Domains.Firmenkalender") = false → Verstoß
```

**Typische legitime Abweichungen:**

| Ordner-Pfad | Namespace | Abweichung |
|-------------|-----------|------------|
| `Handlers/Domains/Firmenkalender/` | `MyApp.Handlers.Kalender` | Segment `Domains` weggelassen |
| `Features/Admin/Users/Commands/` | `MyApp.Features.Users.Commands` | Segment `Admin` weggelassen |
| `src/Core/Services/` | `MyApp.Core.Services` | Segment `src` nicht im Namespace |
| `Components/Shared/Layout/` | `MyApp.Shared.Layout` | Segment `Components` weggelassen |

Feature-Folder-Architektur (Vertical Slice) ist eine weit verbreitete Konvention, bei der Namespaces bewusst flacher sind als die Ordnerstruktur.

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterAnalyzer.Scope.cs` | Zeile 236–313 — `CheckNamespaceDirectoryMapping()`, `CheckNamespaceMappingRule()` |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `GlobalConfig` |
| `rules.json` | `Global`-Sektion |

---

## Konfigurationsänderung

### `rules.json`:
```json
"Global": {
  "EnforceNamespaceDirectoryMapping": true,
  "NamespaceDirectoryMappingMode": "suffix-match",
  "NamespaceDirectoryMappingIgnorePathSegments": ["src", "Source", "Domains", "Handlers"],
  "NamespaceDirectoryMappingRequiredTrailingSegments": 2
}
```

**Modi:**
- `"exact"` — Bisheriges Verhalten: Namespace muss exakt auf vollständigen Pfad enden
- `"suffix-match"` — Namespace muss auf die letzten N Segmente enden (`RequiredTrailingSegments`)
- `"contains-all"` — Alle Pfad-Segmente müssen im Namespace enthalten sein (Reihenfolge egal)

`NamespaceDirectoryMappingIgnorePathSegments`: Diese Pfad-Segmente werden beim Vergleich ignoriert.

`NamespaceDirectoryMappingRequiredTrailingSegments`: Im `suffix-match`-Modus: die letzten N Ordner-Segmente müssen als Namespace-Suffix vorkommen.

---

## Implementierungsvorschlag

### `LinterConfig.cs` — `GlobalConfig` erweitern:

```csharp
public sealed record GlobalConfig
{
    // ...
    public bool EnforceNamespaceDirectoryMapping { get; init; } = false;

    /// <summary>
    /// "exact" | "suffix-match" | "contains-all"
    /// </summary>
    public string NamespaceDirectoryMappingMode { get; init; } = "exact";

    /// <summary>
    /// Pfad-Segmente die beim Namespace-Vergleich ignoriert werden.
    /// </summary>
    public IReadOnlyCollection<string> NamespaceDirectoryMappingIgnorePathSegments { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Im suffix-match Modus: Anzahl der letzten Ordner-Segmente die im Namespace enthalten sein müssen.
    /// </summary>
    public int NamespaceDirectoryMappingRequiredTrailingSegments { get; init; } = 2;
}
```

### `LinterAnalyzer.Scope.cs` — `CheckNamespaceMappingRule()` erweitern (Zeile 275):

```csharp
private void CheckNamespaceMappingRule(string[] pathParts, string relativePath)
{
    if (!_config.Global.EnforceNamespaceDirectoryMapping) return;

    var namespaceDeclaration = _tree.GetRoot().DescendantNodes()
        .OfType<BaseNamespaceDeclarationSyntax>()
        .FirstOrDefault();
    if (namespaceDeclaration == null) return;

    var declaredNamespace = namespaceDeclaration.Name.ToString();

    // Relevante Pfad-Teile bestimmen (ignorierte Segmente entfernen)
    var ignoredSegments = _config.Global.NamespaceDirectoryMappingIgnorePathSegments
        ?? Array.Empty<string>();
    var relevantParts = pathParts
        .Where(p => !ignoredSegments.Contains(p, StringComparer.OrdinalIgnoreCase))
        .ToArray();

    bool matches = _config.Global.NamespaceDirectoryMappingMode switch
    {
        "suffix-match" => MatchesSuffix(declaredNamespace, relevantParts,
            _config.Global.NamespaceDirectoryMappingRequiredTrailingSegments),
        "contains-all" => MatchesContainsAll(declaredNamespace, relevantParts),
        _ => MatchesExact(declaredNamespace, relevantParts) // "exact" (Default)
    };

    if (!matches)
    {
        var expectedSuffix = string.Join(".", relevantParts);
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(namespaceDeclaration),
            RuleName = "EnforceNamespaceDirectoryMapping",
            Details = $"Der Namespace '{declaredNamespace}' stimmt nicht mit dem " +
                      $"physischen Ordnerpfad '{relativePath}' ueberein " +
                      $"(Modus: {_config.Global.NamespaceDirectoryMappingMode}).",
            Guidance = $"Passe den Namespace an, sodass er '.{expectedSuffix}' enthaelt, " +
                       $"oder verschiebe die Datei."
        });
    }
}

private static bool MatchesExact(string ns, string[] parts)
{
    var suffix = string.Join(".", parts);
    return ns.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
}

private static bool MatchesSuffix(string ns, string[] parts, int requiredTrailing)
{
    if (parts.Length == 0) return true;
    var trailing = parts.TakeLast(Math.Min(requiredTrailing, parts.Length)).ToArray();
    var suffix = string.Join(".", trailing);
    return ns.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
}

private static bool MatchesContainsAll(string ns, string[] parts)
{
    return parts.All(p => ns.Contains(p, StringComparison.OrdinalIgnoreCase));
}
```

---

## Tests

### Mode: exact (bisheriges Verhalten):
```
Ordner: Features/Admin/Users/
Namespace: MyApp.Features.Admin.Users
→ Suffix "Features.Admin.Users" → match → kein Verstoß

Namespace: MyApp.Features.Users
→ Suffix "Features.Admin.Users" → KEIN match → Verstoß
```

### Mode: suffix-match, RequiredTrailingSegments: 2:
```
Ordner: Handlers/Domains/Firmenkalender/
Namespace: MyApp.Handlers.Kalender
→ Letzte 2 Segmente: "Domains.Firmenkalender"
→ "MyApp.Handlers.Kalender".EndsWith("Domains.Firmenkalender") = false → Verstoß

// Mit IgnorePathSegments: ["Domains"]
→ relevantParts: ["Handlers", "Firmenkalender"]
→ Letzte 2: "Handlers.Firmenkalender"
→ "MyApp.Handlers.Kalender".EndsWith("Handlers.Firmenkalender") = false → noch Verstoß
```

### Mode: suffix-match + IgnorePathSegments:
```
Ordner: Handlers/Domains/Kalender/
IgnorePathSegments: ["Domains"]
relevantParts: ["Handlers", "Kalender"]
RequiredTrailingSegments: 2
Suffix: "Handlers.Kalender"
Namespace: MyApp.Handlers.Kalender
→ EndsWith("Handlers.Kalender") = true → kein Verstoß
```

### Edge Cases:
- Datei im Root-Ordner (pathParts leer) → kein Check (kein Verstoß)
- Alle Segmente in IgnorePathSegments → relevantParts leer → kein Check (kein Verstoß)
- `RequiredTrailingSegments > relevantParts.Length` → alle Segmente nehmen
- `EnforceNamespaceDirectoryMapping: false` → kein Check (wie bisher)

---

## README-Anforderungen

Im README zu `EnforceNamespaceDirectoryMapping`:
- Modi erklären mit je einem Code-Beispiel
- `NamespaceDirectoryMappingIgnorePathSegments` erklären
- `NamespaceDirectoryMappingRequiredTrailingSegments` erklären
- Empfohlene Konfiguration für Feature-Folder-Architektur als Snippet
- Hinweis: Regel ist standardmäßig deaktiviert; nur in Strict-Profilen aktivieren
