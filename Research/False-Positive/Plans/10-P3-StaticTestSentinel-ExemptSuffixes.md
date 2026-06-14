# Plan 10 — P3: StaticTestSentinel — Klassen-Typ-Exemptions

**Priorität:** P3  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

Der StaticTestSentinel meldet Klassen als nicht abgedeckt, für die sinnvolle Unit-Tests schwer oder unmöglich sind:

| Klassen-Typ | Warum kein Test sinnvoll |
|-------------|--------------------------|
| `StringExtensions`, `DateTimeExtensions` | Extension-Methoden werden indirekt getestet |
| `BoolToVisibilityConverter` (WPF) | IValueConverter — visuelle Komponente |
| `MyComponent` (Blazor) | Blazor-UI-Komponenten brauchen spezielle Test-Infrastruktur |
| `UserProfile : Profile` (AutoMapper) | Mapping-Tests über Service-Tests |
| `AppConstants` | Nur Konstanten, keine Logik |
| `ServiceCollectionExtensions` | DI-Registrierungen — Integrations-Scope |

Der Sentinel feuert nur wenn `MaxCognitiveComplexity > MinCognitiveComplexityForTest` (Standard: 3). Aber eine Extension-Klasse mit 5 Methoden à Komplexität 2 hat eine Max-Komplexität von 2 — feuert nicht. Das Problem tritt auf wenn eine Extension-Methode eine Zyklus-Bedingung hat (Komplexität 4 → Sentinel feuert).

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/PostAnalysisChecks.cs` | Zeile 41–48 — `CheckClassTestSentinel()` |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `TestSentinelConfig` (Zeile 169) |
| `rules.json` | `TestSentinel`-Sektion |

---

## Konfigurationsänderung

### `rules.json`:
```json
"TestSentinel": {
  "ClassNamePatterns": ["{Name}Tests", "{Name}Test", "{Name}IntegrationTests", "{Name}*Tests"],
  "RecognizeTypeofReference": true,
  "RecognizeCoversComment": true,
  "ExemptClassNameSuffixes": [
    "Extensions",
    "Constants",
    "Converter",
    "Profile",
    "Seed",
    "Migration",
    "Startup",
    "Module"
  ],
  "ExemptWhenInheritsFrom": [
    "ComponentBase",
    "IValueConverter",
    "Profile"
  ],
  "ExemptStaticClasses": true
}
```

---

## Implementierungsvorschlag

### `LinterConfig.cs` — `TestSentinelConfig` erweitern (Zeile 169):

```csharp
public sealed record TestSentinelConfig
{
    public IReadOnlyList<string> ClassNamePatterns { get; init; } = [ /* wie bisher */ ];
    public bool RecognizeTypeofReference { get; init; } = true;
    public bool RecognizeCoversComment { get; init; } = true;

    /// <summary>
    /// Klassen mit diesen Namens-Suffixen werden vom StaticTestSentinel ausgenommen.
    /// </summary>
    public IReadOnlyCollection<string> ExemptClassNameSuffixes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Klassen die von diesen Typen erben oder diese Interfaces implementieren,
    /// werden vom StaticTestSentinel ausgenommen.
    /// </summary>
    public IReadOnlyCollection<string> ExemptWhenInheritsFrom { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Statische Klassen (nur statische Methoden) werden vom Sentinel ausgenommen.
    /// </summary>
    public bool ExemptStaticClasses { get; init; } = false;
}
```

### `PostAnalysisChecks.cs` — `CheckClassTestSentinel()` erweitern (Zeile 41):

```csharp
private static void CheckClassTestSentinel(
    ClassInfo srcClass,
    TestSentinelContext context,
    LinterConfig config)
{
    var effectiveConfig = srcClass.ProjectName != null
        ? ProjectConfigResolver.ResolveForProject(srcClass.ProjectName, config)
        : config;

    if (!effectiveConfig.Global.EnableTestSentinel) return;
    if (srcClass.MaxCognitiveComplexity <= effectiveConfig.Metrics.MinCognitiveComplexityForTest) return;

    // NEU: Sentinel-Exemptions prüfen
    if (IsExemptFromSentinel(srcClass, effectiveConfig.TestSentinel)) return;

    CheckTestPresence(srcClass, context, effectiveConfig);
}

private static bool IsExemptFromSentinel(ClassInfo srcClass, TestSentinelConfig sentinelConfig)
{
    // Suffix-Exemption
    var suffixes = sentinelConfig.ExemptClassNameSuffixes;
    if (suffixes?.Any(s => srcClass.Name.EndsWith(s, StringComparison.OrdinalIgnoreCase)) == true)
        return true;

    // Static-Klassen-Exemption
    // Hinweis: ClassInfo muss um IsStatic erweitert werden (s. unten)
    if (sentinelConfig.ExemptStaticClasses && srcClass.IsStatic)
        return true;

    return false;
}
```

**Hinweis:** `ClassInfo.IsStatic` muss ergänzt werden (s. Architektur-Hinweise).

Für `ExemptWhenInheritsFrom` ist das Erbt-Von-Wissen in `ClassInfo` nicht verfügbar. Zwei Optionen:
1. **`ClassInfo` um `BaseTypeNames`-Property erweitern** (empfohlen) — listet Basisklassen-/Interface-Namen
2. Exemption nur zur Analyse-Zeit in `LinterAnalyzer.Architecture.cs` prüfen und in ClassInfo als Flag speichern

### Option 1: `ClassInfo` erweitern:

```csharp
// src/AiNetLinter/Models/ClassInfo.cs
public sealed class ClassInfo
{
    // ... bestehende Properties ...
    public bool IsStatic { get; init; }
    public IReadOnlyCollection<string> BaseTypeNames { get; init; } = Array.Empty<string>();
}
```

### Befüllung in `LinterAnalyzer.Architecture.cs` (VisitClassDeclaration):

```csharp
var symbol = _semanticModel.GetDeclaredSymbol(node);
Classes.Add(new ClassInfo
{
    // ...
    IsStatic = node.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
    BaseTypeNames = GetBaseTypeNames(symbol),
});

private static IReadOnlyCollection<string> GetBaseTypeNames(INamedTypeSymbol? symbol)
{
    if (symbol == null) return Array.Empty<string>();
    var names = new List<string>();

    var current = symbol.BaseType;
    while (current != null && current.SpecialType != SpecialType.System_Object)
    {
        names.Add(current.Name);
        current = current.BaseType;
    }

    foreach (var iface in symbol.AllInterfaces)
        names.Add(iface.Name);

    return names.AsReadOnly();
}
```

### `IsExemptFromSentinel()` für InheritsFrom:

```csharp
private static bool IsExemptFromSentinel(ClassInfo srcClass, TestSentinelConfig sentinelConfig)
{
    // Suffix-Exemption
    // ...

    // Static-Exemption
    // ...

    // InheritsFrom-Exemption
    var exemptBases = sentinelConfig.ExemptWhenInheritsFrom;
    if (exemptBases?.Count > 0 && srcClass.BaseTypeNames.Count > 0)
    {
        if (srcClass.BaseTypeNames.Any(b => exemptBases.Contains(b, StringComparer.OrdinalIgnoreCase)))
            return true;
    }

    return false;
}
```

---

## Tests

### Suffix-Exemption:
```csharp
// ExemptClassNameSuffixes: ["Extensions"]
// Klasse: StringExtensions mit Komplexität > 3
// → kein Sentinel-Verstoß

// Klasse: OrderService (kein Suffix) mit Komplexität > 3, kein Test
// → Sentinel-Verstoß
```

### Static-Class-Exemption:
```csharp
// ExemptStaticClasses: true
public static class MyHelpers { ... }  // → kein Sentinel-Verstoß
public sealed class MyService { ... }  // → normaler Sentinel-Check
```

### InheritsFrom-Exemption:
```csharp
// ExemptWhenInheritsFrom: ["ComponentBase"]
public sealed class MyComponent : ComponentBase { ... }
// → kein Sentinel-Verstoß
```

### Kein Einfluss auf echte Coverage-Erkennung:
- Klasse mit `typeof(MyService)` in Testklasse → weiterhin als covered erkannt (unabhängig von Exemptions)
- Klasse mit `// @covers` Kommentar → weiterhin als covered

### Edge Cases:
- `ExemptClassNameSuffixes` leer → kein Suffix-Skip
- Klasse die exakt `"Extensions"` heißt (kein längerer Name) → `EndsWith("Extensions")` = true → exempt
- `ExemptStaticClasses: false` (Default) → static Klassen werden normal geprüft
- `ClassInfo.BaseTypeNames` leer → InheritsFrom-Prüfung trifft nie zu

---

## README-Anforderungen

Im README zu `StaticTestSentinel`:
- `ExemptClassNameSuffixes` erklären mit Empfehlungsliste
- `ExemptWhenInheritsFrom` erklären (Blazor, WPF-Konverter, AutoMapper)
- `ExemptStaticClasses` erklären
- Klarstellung: Die Regel prüft nur Klassen mit `MaxCognitiveComplexity > MinCognitiveComplexityForTest`
- Bestehende Coverage-Muster dokumentieren: Testklassen-Name, `typeof`, `// @covers`

---

## Architektur-Hinweise

- `ClassInfo` um `IsStatic` und `BaseTypeNames` erweitern — minimale Breaking Change (neue init-Felder mit Defaults)
- `BaseTypeNames` wird transitiv befüllt (alle Basisklassen + alle Interfaces) — das ist bereits für Plan 05 (`ImmutabilityExemptBaseTypes`) nützlich und kann dort wiederverwendet werden
- Empfehlung: Plan 05 und Plan 10 koordinieren damit `BaseTypeNames` nur einmal in `ClassInfo` landet
