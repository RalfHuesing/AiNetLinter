# R02 — `LinterConfig.cs` aufteilen

**Problem:** `LinterConfig.cs` ist 741 Zeilen lang (Limit 500) und enthält 9 verschiedene
Record-/Klassendeklarationen. Die Datei trägt eine dateiweite `// ainetlinter-disable MaxLineCount`
Suppression, die von einem Agenten eingefügt wurde.

Der Code ist strukturell korrekt — die Datei ist zu groß, weil alle Konfigurations-Records
zusammen in einer Datei liegen, nicht weil einzelne Records schlecht designed wären.

---

## Diagnose: Was liegt in der Datei?

| Typ | Zeilen (ca.) | Inhalt |
|:----|:---:|:---|
| `LinterConfig` | 38 | Root-Record mit 7 Properties |
| `NamespaceRule` | 6 | 2 Properties |
| `GlobalConfig` | ~170 | ~30 Bool/Collection-Properties + `Apply()` |
| `MetricsConfig` | ~290 | ~35 Int/Collection-Properties + 4 private Apply-Methoden |
| `TestSentinelConfig` | 55 | 5 Properties + `Apply()` |
| `RuleMetadataEntry` | 6 | 2 Properties |
| `MetricCondition` | 20 | 3 Properties |
| `CompoundSuppression` | 35 | 5 Properties |
| `ProjectOverrideEntry` | 25 | 4 Properties |

---

## Lösungsansatz: Eine Datei pro logischer Gruppe

Aufteilung nach fachlicher Zugehörigkeit — keine willkürlichen Splits:

```
src/AiNetLinter/Configuration/
  LinterConfig.cs          ← nur LinterConfig + NamespaceRule (44 Zeilen)
  GlobalConfig.cs          ← GlobalConfig (170 Zeilen)
  MetricsConfig.cs         ← MetricsConfig + CompoundSuppression + MetricCondition (~345 Zeilen)
  TestSentinelConfig.cs    ← TestSentinelConfig (55 Zeilen)
  ProjectOverrideEntry.cs  ← ProjectOverrideEntry + RuleMetadataEntry (31 Zeilen)
```

Alle 5 Dateien bleiben unter 500 Zeilen. Kein Inhalt ändert sich.

---

## Konkrete Änderungen

### `src/AiNetLinter/Configuration/LinterConfig.cs` (nach Aufteilung)

```csharp
#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Die globale Konfigurationsstruktur für den Linter.
/// </summary>
public sealed record LinterConfig
{
    public required GlobalConfig Global { get; init; }
    public required MetricsConfig Metrics { get; init; }
    public TestSentinelConfig TestSentinel { get; init; } = new();
    public UiSeparationConfig UiSeparation { get; init; } = new();
    public FileFiltersConfig FileFilters { get; init; } = new();
    public IReadOnlyDictionary<string, RuleMetadataEntry> RuleMetadata { get; init; }
        = new Dictionary<string, RuleMetadataEntry>();
    public IReadOnlyCollection<NamespaceRule> ForbiddenNamespaceDependencies { get; init; }
        = Array.Empty<NamespaceRule>();
    public IReadOnlyDictionary<string, ProjectOverrideEntry> ProjectOverrides { get; init; }
        = new Dictionary<string, ProjectOverrideEntry>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, ProjectOverrideEntry> PathOverrides { get; init; }
        = new Dictionary<string, ProjectOverrideEntry>(StringComparer.OrdinalIgnoreCase);
    public string? SolutionBasePath { get; init; }
}

/// <summary>
/// Definition einer verbotenen Abhängigkeit zwischen Namespaces.
/// </summary>
public sealed record NamespaceRule
{
    public required string SourceNamespace { get; init; }
    public required string TargetNamespace { get; init; }
}
```

### `src/AiNetLinter/Configuration/GlobalConfig.cs` (neu)

```csharp
#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Globale Verhaltensregeln und strukturelle Einschränkungen.
/// </summary>
public sealed record GlobalConfig
{
    // [alle bestehenden Properties unverändert übernehmen]
    // [Apply()-Methode unverändert übernehmen — Disable-Kommentare entfernen
    //  sobald R01 (NullCoalescingInitializer-Classifier) implementiert ist,
    //  sonst vorerst behalten]
}
```

### `src/AiNetLinter/Configuration/MetricsConfig.cs` (neu)

```csharp
#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Grenzwerte für verschiedene Code-Metriken.
/// </summary>
public sealed record MetricsConfig
{
    // [alle bestehenden Properties unverändert]
    // [Apply() + 4 private Methoden unverändert]
}

public sealed record CompoundSuppression { /* unverändert */ }
public sealed record MetricCondition { /* unverändert */ }
```

### `src/AiNetLinter/Configuration/TestSentinelConfig.cs` (neu)

```csharp
#nullable enable
namespace AiNetLinter.Configuration;

public sealed record TestSentinelConfig
{
    // [unverändert]
}
```

### `src/AiNetLinter/Configuration/ProjectOverrideEntry.cs` (neu)

```csharp
#nullable enable
namespace AiNetLinter.Configuration;

public sealed record ProjectOverrideEntry { /* unverändert */ }
public sealed record RuleMetadataEntry { /* unverändert */ }
```

---

## Was sich NICHT ändert

- Keine API-Änderungen: alle Typen behalten Namespace `AiNetLinter.Configuration`
- Keine Logik-Änderungen
- `LinterConfigOverrides.cs` bleibt unberührt (hat eigene Overrides-Records)
- Kein Using-Import nötig (same namespace)
- Alle bestehenden Tests kompilieren ohne Änderung

---

## Unit Tests

Kein neuer Testcode erforderlich — es ist ein reines Datei-Split ohne Logikänderung.
Der bestehende Test-Build validiert automatisch, dass alle Typen weiterhin korrekt
aufgelöst werden.

**Empfehlung:** Build + alle Tests nach dem Split laufen lassen:
```powershell
dotnet build && dotnet test
```

---

## Dokumentation

Keine Dokumentationsänderung nötig — die Typen selbst und ihre Konfigurationsoptionen
bleiben identisch. Nur die Dateistruktur ändert sich.

Optional: `Docs/codegraph.md` neu generieren (automatisch durch CLI):
```powershell
dotnet run -- --codegraph
```

---

## Reihenfolge-Empfehlung

Vor diesem Refactoring **R01** umsetzen: Dann kann beim Erstellen von `GlobalConfig.cs`
die `// ainetlinter-disable`-Suppress in `Apply()` direkt weggelassen werden.

Falls R01 noch nicht umgesetzt: Die zwei Suppress-Kommentare in `GlobalConfig.cs` vorerst
mitübernehmen und in einem zweiten Commit nach R01-Umsetzung entfernen.

---

## Commit-Vorschlag

```
refactor: LinterConfig.cs in 5 Dateien nach fachlicher Gruppe aufteilen
```

Entfernt `// ainetlinter-disable MaxLineCount` durch saubere Dateiaufteilung.
Keine Logik- oder API-Änderungen.
