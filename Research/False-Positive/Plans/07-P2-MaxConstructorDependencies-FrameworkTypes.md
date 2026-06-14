# Plan 07 — P2: MaxConstructorDependencies — Framework-Typen ignorieren

**Priorität:** P2  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

Cross-Cutting-Concerns wie `ILogger<T>`, `IOptions<T>`, `IHostEnvironment` und `IConfiguration` sind keine fachlichen Abhängigkeiten, erhöhen aber den Zähler für `MaxConstructorDependencies`. Ein Handler mit 5 fachlichen Services plus 3 Framework-Services überschreitet Limit 5, obwohl das Design korrekt ist:

```csharp
// 8 Parameter — 3 davon Cross-Cutting
public sealed class PlannerHandler(
    ILogger<PlannerHandler> logger,      // Cross-Cutting
    IOptions<PlannerOptions> options,    // Cross-Cutting
    IHostEnvironment env,                // Cross-Cutting
    IPlannerRepository repo,             // fachlich
    INotificationService notify,         // fachlich
    ICalendarService calendar,           // fachlich
    IAuditService audit,                 // fachlich
    IUserContextService userCtx)         // fachlich — count = 5 fachlich = OK!
```

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterAnalyzer.State.cs` | Zeile 116–135 — `CheckPrimaryConstructorDependencies()` |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `MetricsConfig` |
| `rules.json` | `Metrics`-Sektion |

---

## Konfigurationsänderung

### `rules.json`:
```json
"Metrics": {
  "MaxConstructorDependencies": 5,
  "ConstructorDependencyIgnoreTypePrefixes": [
    "ILogger",
    "IOptions",
    "IOptionsSnapshot",
    "IOptionsMonitor",
    "IHostEnvironment",
    "IWebHostEnvironment",
    "IConfiguration",
    "IServiceProvider",
    "IHttpContextAccessor"
  ]
}
```

`ConstructorDependencyIgnoreTypePrefixes`: Typen (Interfaces/Klassen) deren Name **mit** einem dieser Präfixe beginnt, werden beim Zählen der Konstruktor-Abhängigkeiten nicht mitgezählt.

---

## Implementierungsvorschlag

### `LinterConfig.cs` — `MetricsConfig` erweitern:

```csharp
public sealed record MetricsConfig
{
    // ...
    public int MaxConstructorDependencies { get; init; } = 5;

    /// <summary>
    /// Typ-Name-Präfixe von Framework-/Cross-Cutting-Abhängigkeiten die nicht
    /// zu MaxConstructorDependencies zählen.
    /// Beispiel: ["ILogger", "IOptions", "IHostEnvironment"]
    /// </summary>
    public IReadOnlyCollection<string> ConstructorDependencyIgnoreTypePrefixes { get; init; }
        = Array.Empty<string>();

    public MetricsConfig Apply(MetricsConfigOverride? @override)
    {
        return this with
        {
            // ...
            ConstructorDependencyIgnoreTypePrefixes
                = @override?.ConstructorDependencyIgnoreTypePrefixes
                  ?? ConstructorDependencyIgnoreTypePrefixes,
        };
    }
}
```

Gleiche Property in `MetricsConfigOverride`.

### `LinterAnalyzer.State.cs` — `CheckPrimaryConstructorDependencies()` anpassen (Zeile 116):

```csharp
private void CheckPrimaryConstructorDependencies(TypeDeclarationSyntax node)
{
    if (node.ParameterList == null) return;

    var ignorePrefixes = _config.Metrics.ConstructorDependencyIgnoreTypePrefixes;
    int count;

    if (ignorePrefixes == null || ignorePrefixes.Count == 0)
    {
        // Bisheriges Verhalten
        count = node.ParameterList.Parameters.Count;
    }
    else
    {
        // Nur nicht-ignorierte Typen zählen
        count = CountNonFrameworkDependencies(node.ParameterList.Parameters, ignorePrefixes);
    }

    if (count > _config.Metrics.MaxConstructorDependencies)
    {
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = nameof(_config.Metrics.MaxConstructorDependencies),
            Details = $"Der Typ '{node.Identifier.Text}' hat {count} Abhaengigkeiten " +
                      $"(erlaubt sind maximal {_config.Metrics.MaxConstructorDependencies}, " +
                      $"Framework-Typen nicht gezaehlt).",
            Guidance = "Kapsle mehrere Abhaengigkeiten in ein Parameter-Object (record) oder " +
                       "teile die Klasse in kleinere Einheiten auf."
        });
    }
}

private int CountNonFrameworkDependencies(
    SeparatedSyntaxList<ParameterSyntax> parameters,
    IReadOnlyCollection<string> ignorePrefixes)
{
    int count = 0;
    foreach (var param in parameters)
    {
        if (!IsFrameworkDependency(param, ignorePrefixes))
            count++;
    }
    return count;
}

private bool IsFrameworkDependency(
    ParameterSyntax param,
    IReadOnlyCollection<string> ignorePrefixes)
{
    if (param.Type == null) return false;

    var typeName = GetSimpleTypeName(param.Type);
    if (typeName == null) return false;

    foreach (var prefix in ignorePrefixes)
    {
        if (typeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}

private static string? GetSimpleTypeName(TypeSyntax type)
{
    return type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        GenericNameSyntax generic => generic.Identifier.Text,
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        _ => null
    };
}
```

---

## Tests

**Datei:** `src/AiNetLinter.Tests/MaxConstructorDependenciesTests.cs` (neu oder in bestehende integrieren)

### Framework-Typen werden ignoriert:
```csharp
// Konfiguriert: ConstructorDependencyIgnoreTypePrefixes: ["ILogger", "IOptions"]
// MaxConstructorDependencies: 3
public sealed class MyHandler(
    ILogger<MyHandler> logger,   // ignoriert
    IOptions<Opts> options,      // ignoriert
    IServiceA a,                 // zählt: 1
    IServiceB b,                 // zählt: 2
    IServiceC c)                 // zählt: 3 → genau am Limit → kein Verstoß
{ }

// Mit einem weiteren Service:
public sealed class TooBig(
    ILogger<TooBig> logger,  // ignoriert
    IServiceA a,             // 1
    IServiceB b,             // 2
    IServiceC c,             // 3
    IServiceD d)             // 4 → Verstoß
{ }
```

### Keine Ignore-Prefixes (Default — Backward-Compatibility):
```csharp
// ConstructorDependencyIgnoreTypePrefixes leer
// ILogger zählt mit → 4 Parameter bei Limit 3 = Verstoß
public sealed class Handler(
    ILogger<Handler> logger,  // zählt
    IServiceA a,              // zählt
    IServiceB b,              // zählt
    IServiceC c)              // zählt → 4 = Verstoß
{ }
```

### Generics werden korrekt erkannt:
```csharp
// IOptions<MyOptions> → Präfix "IOptions" → ignoriert
// ILogger<MyClass> → Präfix "ILogger" → ignoriert
```

### Edge Cases:
- `IOptionsSnapshot<T>` → Präfix `IOptionsSnapshot` in Liste → ignoriert
- `MyCustomLogger` → Präfix `ILogger` trifft nicht zu → zählt
- Klassischer Konstruktor (nicht Primary Constructor) → gleiche Logik
- `ConstructorDependencyIgnoreTypePrefixes` leer → bisheriges Verhalten
- Fehler/kein Typ-Name ableitbar → Parameter zählt (sicherer Default)

---

## README-Anforderungen

Im README zu `MaxConstructorDependencies`:
- `ConstructorDependencyIgnoreTypePrefixes` erklären
- Empfohlene Standard-Ignore-Liste als Snippet (ILogger, IOptions, IHostEnvironment, etc.)
- Erklären: fachliche Abhängigkeiten zählen, Cross-Cutting-Concerns optional ignorierbar
- Hinweis: Auch Primary-Constructor-Syntax (.NET 8+) wird erkannt
