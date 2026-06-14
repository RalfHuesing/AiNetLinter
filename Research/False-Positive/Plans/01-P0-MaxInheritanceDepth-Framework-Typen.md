# [x] Plan 01 — P0: MaxInheritanceDepth — Framework-Typen aus Tiefenzählung ausschließen (Erledigt)

**Priorität:** P0 — Kritisch  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

`MaxInheritanceDepth` zählt die **vollständige** Vererbungskette bis `System.Object`, inklusive Framework-Klassen.
Das macht die Regel für WPF-Projekte **systemisch unbrauchbar**:

| Klasse | Tatsächliche Ketten-Tiefe |
|--------|--------------------------|
| `MyWindow : Window` | 8 |
| `MyControl : UserControl` | 8 |
| `MyPage : Page` | 9 |

Jede WPF-UI-Klasse verletzt sofort `MaxInheritanceDepth: 2` ohne dass der Entwickler dies kontrollieren kann.

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterAnalyzer.Architecture.cs` | Zeile 375–387 — `GetInheritanceDepth()` |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `MetricsConfig` (ab Zeile 125) |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `MetricsConfigOverride` (ab Zeile 325) |
| `rules.json` | `Metrics`-Sektion |

---

## Konfigurationsänderung

### `rules.json` — neue Option in `Metrics`:

```json
"Metrics": {
  "MaxInheritanceDepth": 2,
  "InheritanceDepthFrameworkPrefixes": [
    "System.",
    "Microsoft.AspNetCore.",
    "Microsoft.UI.",
    "System.Windows."
  ]
}
```

`InheritanceDepthFrameworkPrefixes` ist eine Liste von Namespace-Präfixen. Basisklassen, deren vollqualifizierter Typ-Namespace mit einem dieser Präfixe beginnt, werden aus dem Tiefen-Zähler ausgeschlossen.

Der Default für den neuen Key sollte **leer** sein (Liste leer = heutiges Verhalten, Backward-Compatibility).
WPF-Projekte setzen die Liste explizit.

---

## Implementierungsvorschlag

### `LinterConfig.cs` — `MetricsConfig` erweitern:

```csharp
public sealed record MetricsConfig
{
    // ... bestehende Properties ...
    public int MaxInheritanceDepth { get; init; } = 2;

    /// <summary>
    /// Namespace-Präfixe von Framework-Basistypen, die beim Zählen der Vererbungstiefe ignoriert werden.
    /// Beispiel: ["System.", "System.Windows.", "Microsoft.AspNetCore.Components."]
    /// Leer = alle Typen zählen (bisheriges Verhalten).
    /// </summary>
    public IReadOnlyCollection<string> InheritanceDepthFrameworkPrefixes { get; init; }
        = Array.Empty<string>();

    public MetricsConfig Apply(MetricsConfigOverride? @override)
    {
        return this with
        {
            // ... bestehende Felder ...
            InheritanceDepthFrameworkPrefixes = @override?.InheritanceDepthFrameworkPrefixes
                                                ?? InheritanceDepthFrameworkPrefixes,
        };
    }
}
```

Gleiche Erweiterung in `MetricsConfigOverride`:
```csharp
public IReadOnlyCollection<string>? InheritanceDepthFrameworkPrefixes { get; init; }
```

### `LinterAnalyzer.Architecture.cs` — `GetInheritanceDepth()` anpassen (Zeile 375):

```csharp
private int GetInheritanceDepth(INamedTypeSymbol symbol)
{
    int depth = 0;
    var current = symbol.BaseType;
    while (current != null && current.SpecialType != SpecialType.System_Object)
    {
        if (!IsFrameworkBaseType(current))
        {
            depth++;
        }
        if (depth > 20) return depth;
        current = current.BaseType;
    }
    return depth;
}

private bool IsFrameworkBaseType(INamedTypeSymbol symbol)
{
    var prefixes = _config.Metrics.InheritanceDepthFrameworkPrefixes;
    if (prefixes == null || prefixes.Count == 0) return false;

    var ns = symbol.ContainingNamespace?.ToDisplayString();
    if (string.IsNullOrEmpty(ns)) return false;

    foreach (var prefix in prefixes)
    {
        if (ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}
```

**Wichtig:** Die Methode war bisher `static` — durch Zugriff auf `_config` wird sie zu einer Instanz-Methode. Signatur-Anpassung nötig (in `VisitClassDeclaration` wird `GetInheritanceDepth(symbol)` aufgerufen, das bleibt gleich).

---

## Tests

**Datei:** `src/AiNetLinter.Tests/MaxInheritanceDepthTests.cs` (neu oder in bestehende Testdatei integrieren)

### True Positive — soll weiterhin feuern:
```csharp
// 3 echte user-definierte Ebenen → Verstoß bei Limit 2
public class A { }
public class B : A { }
public class C : B { }  // depth = 2 (A, B) → genau am Limit
public class D : C { }  // depth = 3 → Verstoß
```

### False Positive — darf NICHT mehr feuern (WPF):
```csharp
// Fixture: MyControl erbt von UserControl (System.Windows.*)
// Mit InheritanceDepthFrameworkPrefixes: ["System.Windows."]
// → UserControl und alle Framework-Basen werden ignoriert → depth = 0 → kein Verstoß
public sealed partial class MyControl : UserControl { }
```

### Gemischt — Framework + User:
```csharp
// UserControl → (Framework, ignoriert)
// MyBaseControl : UserControl → depth = 0 (nur Framework darunter)
// MyControl : MyBaseControl → depth = 1 (MyBaseControl zählt)
// → kein Verstoß bei Limit 2
public class MyBaseControl : UserControl { }
public sealed class MyControl : MyBaseControl { }
```

### Edge Cases:
- Leere `InheritanceDepthFrameworkPrefixes` Liste → bisheriges Verhalten (Backward-Compatibility)
- Klasse ohne Basisklasse → depth = 0
- Klasse erbt von `object` explizit → depth = 0
- Tiefe genau am Limit → kein Verstoß
- Tiefe = Limit + 1 → Verstoß
- Präfix-Match case-insensitiv prüfen: `"system.windows."` und `"System.Windows."` sind gleich

---

## README-Anforderungen

Im README-Abschnitt zur `Metrics`-Konfiguration:
- Option `InheritanceDepthFrameworkPrefixes` dokumentieren
- Empfohlene WPF-Konfiguration als Code-Snippet aufführen:

```json
"Metrics": {
  "MaxInheritanceDepth": 2,
  "InheritanceDepthFrameworkPrefixes": [
    "System.",
    "Microsoft.UI.",
    "System.Windows."
  ]
}
```

- Erklären: Framework-Typen zählen per Default **nicht** wenn die Liste gesetzt ist; die eigene Vererbungshierarchie zählt
- Hinweis: Blazor-Projekte können `"Microsoft.AspNetCore.Components."` ergänzen wenn nötig

---

## Architektur-Hinweise

- `GetInheritanceDepth` von `static` zu Instanzmethode wird notwendig — das ist konform mit dem Muster der anderen `Check*`-Methoden in der Klasse
- Kein neuer Namespace nötig — Änderung ist rein innerhalb bestehender Strukturen
- `InheritanceDepthFrameworkPrefixes` in `MetricsConfig` (nicht `GlobalConfig`) passt, da es sich um einen Metrik-Parameter handelt
