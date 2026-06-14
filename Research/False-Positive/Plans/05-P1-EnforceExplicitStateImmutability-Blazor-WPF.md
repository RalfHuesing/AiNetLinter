# Plan 05 — P1: EnforceExplicitStateImmutability — Blazor- und WPF-Exemptions

**Priorität:** P1  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

`EnforceExplicitStateImmutability` (standardmäßig deaktiviert, in Strict-Profilen aktiv) meldet alle mutablen Properties und Felder als Verstöße. Das trifft fundamental auf WPF-MVVM und Blazor-Komponenten zu, wo mutabler State **unvermeidbar** ist:

**Blazor:**
```csharp
// PlatformAuthStateProvider — 118 Verstöße dieser Art in realem Projekt
public sealed class PlatformAuthStateProvider : AuthenticationStateProvider
{
    private AuthenticationState? _cachedState;  // → Verstoß: nicht readonly
    private bool _isRefreshing;                 // → Verstoß: nicht readonly
    private CancellationTokenSource? _cts;      // → Verstoß: nicht readonly
}
```

**WPF (MVVM):**
```csharp
// Jede ViewModel-Property mit OnPropertyChanged muss mutable sein
public class OrderViewModel : ObservableObject
{
    private string _name = "";        // → Verstoß: nicht readonly
    public string Name                 // → Verstoß: has set
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}
```

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterAnalyzer.Immutability.cs` | Zeile 17–87 — `CheckClassImmutability()`, `IsDtoOrEntity()` |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `GlobalConfig` — neue Felder |

---

## Konfigurationsänderung

### `rules.json`:
```json
"Global": {
  "EnforceExplicitStateImmutability": true,
  "ImmutabilityExemptSuffixes": ["Dto", "Entity", "Model", "Request", "Response", "Command"],
  "ImmutabilityExemptPatterns": [],
  "ImmutabilityExemptBaseTypes": [
    "ComponentBase",
    "LayoutComponentBase",
    "ObservableObject",
    "ObservableRecipient",
    "BackgroundService",
    "AuthenticationStateProvider",
    "INotifyPropertyChanged"
  ]
}
```

`ImmutabilityExemptBaseTypes`: Klassen, die von einem der genannten Basistypen **erben** (Typ-Name, nicht Namespace), werden von der Immutability-Prüfung ausgenommen.

---

## Implementierungsvorschlag

### `LinterConfig.cs` — `GlobalConfig` erweitern:

```csharp
public sealed record GlobalConfig
{
    // ... bestehende Properties ...

    /// <summary>
    /// Basisklassen- oder Interface-Namen, von denen erbende Klassen von
    /// EnforceExplicitStateImmutability ausgenommen sind.
    /// Gilt für direkte und indirekte Basistypen (transitiv).
    /// Beispiel: ["ComponentBase", "ObservableObject", "BackgroundService"]
    /// </summary>
    public IReadOnlyCollection<string> ImmutabilityExemptBaseTypes { get; init; }
        = Array.Empty<string>();

    public GlobalConfig Apply(GlobalConfigOverride? @override)
    {
        return this with
        {
            // ...
            ImmutabilityExemptBaseTypes = @override?.ImmutabilityExemptBaseTypes
                                          ?? ImmutabilityExemptBaseTypes,
        };
    }
}
```

Gleiche Property in `GlobalConfigOverride`.

### `LinterAnalyzer.Immutability.cs` — `IsDtoOrEntity()` erweitern (Zeile 89):

```csharp
private bool IsDtoOrEntity(ClassDeclarationSyntax node, string className)
{
    if (HasImmutabilityExemptSuffix(className)) return true;
    if (HasImmutabilityExemptPattern(className)) return true;
    if (IsConfigurationBindingOrJsonSerializable(node)) return true;
    if (HasDtoOrEntityAttribute(node)) return true;

    // NEU: Basistyp-Exemption
    if (HasExemptBaseType(node)) return true;

    return false;
}

private bool HasExemptBaseType(ClassDeclarationSyntax node)
{
    var exemptTypes = _config.Global.ImmutabilityExemptBaseTypes;
    if (exemptTypes == null || exemptTypes.Count == 0) return false;

    var symbol = _semanticModel.GetDeclaredSymbol(node);
    if (symbol == null) return false;

    return IsSymbolExemptByBaseType(symbol, exemptTypes);
}

private static bool IsSymbolExemptByBaseType(
    INamedTypeSymbol symbol,
    IReadOnlyCollection<string> exemptTypes)
{
    // Prüfe Basisklassen transitiv
    var current = symbol.BaseType;
    while (current != null && current.SpecialType != SpecialType.System_Object)
    {
        if (exemptTypes.Contains(current.Name, StringComparer.OrdinalIgnoreCase))
            return true;
        current = current.BaseType;
    }

    // Prüfe implementierte Interfaces
    foreach (var iface in symbol.AllInterfaces)
    {
        if (exemptTypes.Contains(iface.Name, StringComparer.OrdinalIgnoreCase))
            return true;
    }

    return false;
}
```

---

## Ergänzende Option: Feldname-Präfix-Exemption

Mutable Felder die mit `_` beginnen sind in C# die Konvention für private Backing-Felder von Properties. Eine zusätzliche Option erlaubt diese:

### `rules.json`:
```json
"Global": {
  "ImmutabilityAllowPrivateBackingFields": true
}
```

### Implementierung in `CheckFieldsImmutability()` (`LinterAnalyzer.Immutability.cs:56`):

```csharp
private void CheckFieldsImmutability(ClassDeclarationSyntax node, string className)
{
    foreach (var fieldDecl in node.Members.OfType<FieldDeclarationSyntax>())
    {
        if (IsMutableField(fieldDecl))
        {
            // NEU: Private Backing-Felder mit _ Prefix überspringen
            if (_config.Global.ImmutabilityAllowPrivateBackingFields
                && IsPrivateBackingField(fieldDecl))
                continue;

            AddMutableFieldViolations(fieldDecl, className);
        }
    }
}

private static bool IsPrivateBackingField(FieldDeclarationSyntax fieldDecl)
{
    var isPrivate = fieldDecl.Modifiers.Any(SyntaxKind.PrivateKeyword)
        || !fieldDecl.Modifiers.Any(m =>
            m.IsKind(SyntaxKind.PublicKeyword) ||
            m.IsKind(SyntaxKind.ProtectedKeyword) ||
            m.IsKind(SyntaxKind.InternalKeyword));

    if (!isPrivate) return false;

    return fieldDecl.Declaration.Variables
        .All(v => v.Identifier.Text.StartsWith("_", StringComparison.Ordinal));
}
```

---

## Tests

**Datei:** `src/AiNetLinter.Tests/Core/ScopeImmutabilityTests.cs` (bestehend — erweitern)

### ImmutabilityExemptBaseTypes — False Positive (darf NICHT feuern):
```csharp
// ImmutabilityExemptBaseTypes: ["ComponentBase"]
public sealed class MyComponent : ComponentBase
{
    private bool _isLoading;        // → kein Verstoß
    public string? Value { get; set; }  // → kein Verstoß
}
```

### ImmutabilityExemptBaseTypes — transitiv:
```csharp
// MyBaseComponent erbt von ComponentBase, MyComponent erbt von MyBaseComponent
// → beide sollen exempt sein
public class MyBaseComponent : ComponentBase { }
public sealed class MyComponent : MyBaseComponent
{
    private bool _state;  // → kein Verstoß (transitiv exempt)
}
```

### True Positive (soll weiterhin feuern):
```csharp
// Normale Service-Klasse ohne exempt-Basistyp
public sealed class OrderService
{
    private string _mutableField = "";  // → Verstoß: nicht readonly
    public string Name { get; set; }    // → Verstoß: set-Accessor
}
```

### ImmutabilityAllowPrivateBackingFields:
```csharp
// ImmutabilityAllowPrivateBackingFields: true
public sealed class ViewModel
{
    private string _name = "";   // → kein Verstoß (privates _ Feld)
    public string PublicField = "";  // → Verstoß (nicht private)
    public string Name { get; set; }  // → Verstoß (Property hat set)
}
```

### Edge Cases:
- `ImmutabilityExemptBaseTypes` leer → kein Basistyp-Skip
- Interface implementiert (nicht nur Klasse geerbt) → ebenfalls exempt
- Klasse mit `ImmutabilityExemptSuffix` UND schlechtem Basistyp → Suffix-Exemption greift zuerst (kein Doppel-Check nötig)
- Klasse ist `sealed` aber hat exemptible Basisklasse → trotzdem exempt

---

## README-Anforderungen

Im README zu `EnforceExplicitStateImmutability`:
- Hinweis: Regel ist für reinen Domänen-/Service-Code designt
- `ImmutabilityExemptBaseTypes` erklären mit typischen Werten für Blazor/WPF
- `ImmutabilityAllowPrivateBackingFields` erklären
- Empfohlene Config für WPF-MVVM als Snippet
- Empfohlene Config für Blazor-Projekte als Snippet
