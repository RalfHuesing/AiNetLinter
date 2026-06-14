# Plan 03 — P1: EnforceSealedClasses — Partial-Klassen und Basisklassen-Exemptions

**Priorität:** P1  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

### Problem A — WPF Partial-Klassen
`AllowUnsealedPartialClasses` ist standardmäßig `false`. WPF-Code-Behind-Dateien sind immer `partial` (der XAML-Compiler generiert die zweite Hälfte). Das bedeutet:

```csharp
// MainWindow.xaml.cs — ist IMMER partial in WPF:
public partial class MainWindow : Window { }  // → Verstoß: nicht sealed
```

Technisch wäre `sealed partial` möglich, aber:
- Kein WPF-Template generiert `sealed`
- LLMs schlagen ohne weitere Kontext-Information `sealed` vor
- Agenten, die den Code „fixen", brechen das Muster auf

### Problem B — Basisklassen ohne `abstract`
Klassen die **als Basisklasse designt** sind (erkennbar am Suffix oder an der Tatsache, dass andere Projektklassen davon erben) werden als Verstoß gemeldet, obwohl `sealed` semantisch falsch wäre:

```csharp
// Soll erbbar sein — abstract wäre hier overdefined
public class OrderHandlerBase { }
public class CreateOrderHandler : OrderHandlerBase { }
```

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterAnalyzer.Architecture.cs` | Zeile 93–100 — `ShouldSkipSealedCheck()` |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `GlobalConfig` (ab Zeile 34) und `GlobalConfigOverride` |
| `rules.json` | `Global`-Sektion |

---

## Konfigurationsänderung

### `rules.json`:
```json
"Global": {
  "EnforceSealedClasses": true,
  "AllowUnsealedPartialClasses": false,
  "SealedClassExemptSuffixes": ["Base", "Foundation", "Host"]
}
```

`SealedClassExemptSuffixes`: Klassen, deren Name mit einem dieser Suffixe endet, werden aus der `EnforceSealedClasses`-Prüfung ausgenommen — sie sind als Basisklassen designt.

---

## Implementierungsvorschlag

### `LinterConfig.cs` — `GlobalConfig` erweitern:

```csharp
public sealed record GlobalConfig
{
    // ... bestehende Properties ...

    /// <summary>
    /// Klassen mit diesen Namens-Suffixen werden von EnforceSealedClasses ausgenommen
    /// (designte Basisklassen ohne abstract-Keyword).
    /// </summary>
    public IReadOnlyCollection<string> SealedClassExemptSuffixes { get; init; }
        = Array.Empty<string>();

    public GlobalConfig Apply(GlobalConfigOverride? @override)
    {
        return this with
        {
            // ...
            SealedClassExemptSuffixes = @override?.SealedClassExemptSuffixes
                                        ?? SealedClassExemptSuffixes,
        };
    }
}
```

Gleiche Property in `GlobalConfigOverride`.

### `LinterAnalyzer.Architecture.cs` — `ShouldSkipSealedCheck()` anpassen (Zeile 93):

```csharp
private bool ShouldSkipSealedCheck(ClassDeclarationSyntax node)
{
    if (!_config.Global.EnforceSealedClasses) return true;
    if (IsSealedOrStaticOrAbstract(node)) return true;

    // Partial-Klassen: wenn AllowUnsealedPartialClasses aktiv
    bool isPartial = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
    if (isPartial && _config.Global.AllowUnsealedPartialClasses) return true;

    // NEU: Basisklassen anhand von Suffix-Exemptions
    if (HasExemptSuffix(node.Identifier.Text)) return true;

    return false;
}

private bool HasExemptSuffix(string className)
{
    var suffixes = _config.Global.SealedClassExemptSuffixes;
    if (suffixes == null || suffixes.Count == 0) return false;
    return suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase));
}
```

---

## Empfehlung: `ProjectOverrides` für WPF-Projekte

Statt `AllowUnsealedPartialClasses: true` global zu setzen, empfiehlt sich ein Projekt-Override:

```json
"ProjectOverrides": {
  "MyWpfApp": {
    "Global": {
      "AllowUnsealedPartialClasses": true
    }
  }
}
```

Das schränkt die Ausnahme auf das WPF-Projekt ein, während andere Projekte weiterhin geprüft werden.

---

## Tests

### True Positive — soll weiterhin feuern:
```csharp
// Normale, nicht-partial, nicht-abstract Klasse ohne Suffix
public class OrderService { }  // → Verstoß: nicht sealed
```

### False Positive A — Partial-Klasse mit AllowUnsealedPartialClasses:
```csharp
// AllowUnsealedPartialClasses: true
public partial class MainWindow : Window { }  // → kein Verstoß
```

### False Positive B — Basisklasse mit Exempt-Suffix:
```csharp
// SealedClassExemptSuffixes: ["Base"]
public class OrderHandlerBase { }  // → kein Verstoß
public class CreateOrderHandler : OrderHandlerBase { }  // → Verstoß (kein Suffix, kein Sealed)
```

### Edge Cases:
- `SealedClassExemptSuffixes` leer → kein Suffix-Skip
- Klasse heißt `Base` selbst (genau Suffix = ganzer Name) → exempt
- Klasse ist `abstract` → bereits ausgenommen, Suffix irrelevant
- Partial-Klasse mit `sealed partial` → kein Verstoß (bereits `sealed`)
- Override für `*.Tests` bleibt wie heute: `EnforceSealedClasses: false`

---

## README-Anforderungen

Im README-Abschnitt zu `EnforceSealedClasses`:
- `AllowUnsealedPartialClasses` erklären: wann und warum (WPF)
- `SealedClassExemptSuffixes` erklären
- WPF-Projekt-Override als Beispiel zeigen
- Klarstellen: Partial-Klassen die `sealed` sind werden nicht gemeldet
