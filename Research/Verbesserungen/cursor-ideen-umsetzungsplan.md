# Cursor-Ideen: Umsetzungsplan

**Erstellt:** 2026-06-16  
**Basis:** Cursor-Ideensammlung (6 Punkte), bewertet und priorisiert

---

## Vorbemerkung: Bewertungsergebnis

Von den 6 Cursor-Ideen sind **2 reine Doku-Fixes** (Idee 1 teilweise, Idee 2), **2 Code-Fixes mit geringem Aufwand** (Idee 3, 4) und **2 neue Features** (Idee 5, 6). Eine Idee (Idee 5 Teil a: Partials als Einheit) ist bereits implementiert und braucht nur Dokumentation.

| Nr  | Idee                                                                 |      Typ      |   Aufwand   | Priorität |
| :-: | :------------------------------------------------------------------- | :-----------: | :---------: | :-------: |
|  1  | `MethodParameterCountIgnoreTypeNames` in Template dokumentieren      |     Doku      | Sehr gering |   Hoch    |
|  2  | `BlazorCssIsolationOnlyWhenStylesNeeded` in Doku ergänzen            |     Doku      | Sehr gering |   Hoch    |
|  3  | `EnforceReadonlyParameters`: ref/in ignorieren                       |    Bug-Fix    |   Gering    |   Hoch    |
|  4  | `StaticTestSentinel`: IntegrationTest-Projekte explizit unterstützen |  Enhancement  |   Gering    |  Mittel   |
|  5  | `AIContextFootprint`: `FootprintIgnoreNamespacePrefixes`             | Neues Feature |   Mittel    |  Mittel   |
|  6  | `PathOverrides`-Sektion (pfadbasierte Overrides)                     | Neues Feature |   Mittel    |  Mittel   |

---

## Idee 1: `MethodParameterCountIgnoreTypeNames` in Vorlage und Doku

### Analyse

Der Key `MethodParameterCountIgnoreTypeNames` existiert vollständig in `MetricsConfig` (Zeile 220) und `MetricsConfigOverride` (Zeile 152), ist aber in der Beispielkonfiguration in `Docs/configuration.md` nicht aufgeführt. Cursor benennt den Key fälschlicherweise als `IgnoreTypePrefixes` — im Code heißt er `IgnoreTypeNames` (exakter Typnamen-Abgleich, kein Präfix).

**Lücke:** Das analog zu `ConstructorDependencyIgnoreTypePrefixes` nützlichere Präfix-Matching fehlt. `MethodParameterCountIgnoreTypeNames` verlangt den exakten einfachen Namen (z. B. `"CancellationToken"`), deckt aber keine Generics wie `"ILogger<T>"` ab. Für Generics wäre ein separates `MethodParameterCountIgnoreTypePrefixes` nötig.

### Maßnahmen

#### 1.1 Dokumentation (Pflicht)

**`Docs/configuration.md`** — Zeile ~163 nach `MethodParameterCountInTestFiles`:

```markdown
| `MethodParameterCountIgnoreTypeNames` | Metrics | Typ-Namen (einfacher Name, kein Namespace), die bei `MaxMethodParameterCount` nicht mitgezählt werden. Standard: `[]`. Empfehlung: `["CancellationToken"]`. |
| `MethodParameterCountIgnoreTypePrefixes` | Metrics | Typ-Name-Präfixe, die beim Zählen der Parameter-Anzahl ignoriert werden (z. B. `["ILogger"]` deckt `ILogger<T>` ab). Standard: `[]`. |
```

**`Docs/configuration.md`** — Empfohlene Konfiguration ergänzen:

```json
"Metrics": {
  "MaxMethodParameterCount": 4,
  "MaxMethodParameterCountInTestFiles": 6,
  "MethodParameterCountIgnoreTypeNames": ["CancellationToken"],
  "MethodParameterCountIgnoreTypePrefixes": ["ILogger"]
}
```

#### 1.2 Neues Feature: `MethodParameterCountIgnoreTypePrefixes` (empfohlen)

**`src/AiNetLinter/Configuration/LinterConfig.cs`** — In `MetricsConfig`:

```csharp
/// <summary>
/// Typ-Name-Präfixe, die beim Zählen der Parameter-Anzahl ignoriert werden.
/// Ermöglicht z. B. "ILogger" um ILogger&lt;T&gt; auszuschließen.
/// </summary>
public IReadOnlyCollection<string> MethodParameterCountIgnoreTypePrefixes { get; init; }
    = Array.Empty<string>();
```

In `MetricsConfig.Apply` / `ApplyPart1`:

```csharp
MethodParameterCountIgnoreTypePrefixes = @override?.MethodParameterCountIgnoreTypePrefixes ?? MethodParameterCountIgnoreTypePrefixes,
```

**`src/AiNetLinter/Configuration/LinterConfigOverrides.cs`** — In `MetricsConfigOverride`:

```csharp
/// <summary>
/// Typ-Name-Präfixe, die beim Zählen der Parameter-Anzahl ignoriert werden.
/// </summary>
public IReadOnlyCollection<string>? MethodParameterCountIgnoreTypePrefixes { get; init; }
```

**`src/AiNetLinter/Core/LinterAnalyzer.Complexity.cs`** (oder wo Parameter gezählt werden) — Prüf-Logik erweitern:

```csharp
private bool IsIgnoredParameterType(ITypeSymbol typeSymbol, MetricsConfig metrics)
{
    var simpleName = typeSymbol is INamedTypeSymbol named
        ? named.OriginalDefinition.Name
        : typeSymbol.Name;

    if (metrics.MethodParameterCountIgnoreTypeNames.Contains(simpleName, StringComparer.Ordinal))
        return true;

    foreach (var prefix in metrics.MethodParameterCountIgnoreTypePrefixes)
    {
        if (simpleName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;
    }

    return false;
}
```

**`rules.json`** (Root-Vorlage und `tests/Fixtures/BaselineMini/rules.json`) — Key ergänzen.

**`src/AiNetLinter/Core/CursorRulesGenerator.cs`** — In den generierten MDC-Hinweisen `MethodParameterCountIgnoreTypePrefixes` aufnehmen.

#### 1.3 Tests

**Neue Testklasse oder Erweiterung bestehender Tests** (`src/AiNetLinter.Tests/`):

```
MethodParameterCountIgnoreTypePrefixes_IgnoresMatchingPrefix()
MethodParameterCountIgnoreTypePrefixes_DoesNotIgnoreNonMatchingPrefix()
MethodParameterCountIgnoreTypeNames_IgnoresExactName()
MethodParameterCountIgnoreTypeNames_DoesNotIgnorePartialName()
```

---

## Idee 2: `BlazorCssIsolationOnlyWhenStylesNeeded` in Dokumentation ergänzen

### Analyse

`BlazorCssIsolationOnlyWhenStylesNeeded` existiert vollständig in:

- `UiSeparationConfig.cs` (Zeile 59, Default: `true`)
- `UiSeparationConfigOverride.cs`
- `UiSeparationConfig.Apply()`

Fehlt nur in `Docs/configuration.md` — die UiSeparation-Tabelle (um Zeile 465) listet den Key nicht auf.

### Maßnahmen

#### 2.1 Dokumentation (Pflicht)

**`Docs/configuration.md`** — UiSeparation-Tabelle ergänzen:

```markdown
| `BlazorCssIsolationOnlyWhenStylesNeeded` | Boolean | `true` | Wenn `true`, wird `BlazorRequireCssIsolation` nur ausgelöst, wenn die `.razor`-Datei native HTML-Elemente (`<div>`, `<span>` etc.) oder explizite `class=`/`style=`-Attribute enthält. Reine Komponenten-Komposition mit PascalCase-Tags wie `<MudButton>` löst keine Verletzung aus. Empfohlen für MudBlazor-Projekte. |
```

In den **Profil-Vorlagen** (`blazor.rules.json`-Beispiel) ergänzen:

```json
"UiSeparation": {
  "BlazorRequireCssIsolation": true,
  "BlazorCssIsolationOnlyWhenStylesNeeded": true,
  ...
}
```

#### 2.2 Tests

Sicherstellen, dass bestehende Tests für `BlazorCssIsolationOnlyWhenStylesNeeded` existieren (in `UiFileSeparationChecker`-Tests). Fehlende Fälle ergänzen:

```
BlazorCssIsolation_PureComponentComposition_NoViolation_WhenOnlyStylesNeededEnabled()
BlazorCssIsolation_NativeHtmlPresent_Violation_WhenOnlyStylesNeededEnabled()
BlazorCssIsolation_StyleAttributePresent_Violation_WhenOnlyStylesNeededEnabled()
```

---

## Idee 3: `EnforceReadonlyParameters` — ref/in ignorieren (Bug-Fix)

### Analyse

**Datei:** `src/AiNetLinter/Core/LinterAnalyzer.State.cs`, Zeile ~247:

```csharp
// IST:
if (symbol is IParameterSymbol parameter && parameter.RefKind != RefKind.Out)

// SOLL:
if (symbol is IParameterSymbol parameter
    && parameter.RefKind is not (RefKind.Out or RefKind.Ref or RefKind.In))
```

**Begründung:**

- `out`-Parameter: bereits korrekt ausgeschlossen (Schreiben ist die Aufgabe von `out`)
- `ref`-Parameter: explizit als veränderlich deklariert — Schreiben ist der Sinn des `ref`-Modifikators
- `in`-Parameter: Roslyn-readonly-Referenz — der Compiler verbietet Reassignment ohnehin; kein Bug aus Linter-Sicht, aber semantisch falsch zu melden

Aktuell löst `ref`-Parameter-Reassignment einen False-Positive aus. Das ist ein echter Bug.

### Maßnahmen

#### 3.1 Code-Fix

**`src/AiNetLinter/Core/LinterAnalyzer.State.cs`** — Zeile ~247:

```csharp
// Vorher:
if (symbol is IParameterSymbol parameter && parameter.RefKind != RefKind.Out)

// Nachher:
if (symbol is IParameterSymbol parameter
    && parameter.RefKind is not (RefKind.Out or RefKind.Ref or RefKind.In))
```

#### 3.2 Tests

```
EnforceReadonlyParameters_RefParameter_NoViolation()
EnforceReadonlyParameters_InParameter_NoViolation()
EnforceReadonlyParameters_OutParameter_NoViolation()           // bereits abgedeckt?
EnforceReadonlyParameters_NormalParameter_Violation()
EnforceReadonlyParameters_NormalParameterReassignment_WithRefMethod_Regression()
```

#### 3.3 Doku-Update

**`Docs/configuration.md`** — Tabelleneintrag `EnforceReadonlyParameters` präzisieren:

```
Verbietet das Überschreiben von Parametern (Parameter-Reassignment). Ausgenommen: `ref`-, `out`- und `in`-Parameter.
```

---

## Idee 4: `StaticTestSentinel` — Indirekte Abdeckung / IntegrationTest-Projekte

### Analyse

**Was bereits funktioniert:**  
`typeof(MyHandler)` in Attribut-Argumenten eines Test-Files (`[SiteHandler(typeof(MyHandler))]`) wird bereits erkannt, weil `TestCoverageCollector.CollectTypeReferences` alle `TypeOfExpressionSyntax`-Knoten via `root.DescendantNodes()` sammelt — das schließt Attribut-Argumente ein.

**Was nicht funktioniert:**  
`TestProjectDetector.IsTestProject` erkennt ein Projekt nur als Testprojekt, wenn seine Metadatenreferenzen die Schlüsselwörter `xunit`, `nunit`, `testplatform` oder `unittesting` enthalten. Integration-Test-Projekte in manchen Setups (z. B. nur `Microsoft.AspNetCore.Mvc.Testing`) werden NICHT erkannt und ihre `typeof`-Signale werden ignoriert.

**Echte Lücke:** Wenn ein IntegrationTest-Projekt keinen direkten xunit/nunit-Verweis hat (z. B. nur via `Microsoft.TestPlatform.Common`) und trotzdem `typeof(MyHandler)` enthält, wird dieses Signal nicht gesammelt.

### Maßnahmen

#### 4.1 `TestProjectDetector` — Projekt-Name-Suffix als Fallback

**`src/AiNetLinter/Core/TestProjectDetector.cs`** — Fallback via Projektname:

```csharp
private static readonly string[] TestProjectNameSuffixes =
    ["Tests", "Test", "IntegrationTests", "Specs", "Spec"];

public static bool IsTestProject(Project project)
{
    foreach (var reference in project.MetadataReferences)
    {
        if (IsTestReference(reference.Display))
            return true;
    }

    // Fallback: Projektname-Suffix
    return TestProjectNameSuffixes.Any(suffix =>
        project.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        || project.Name.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase));
}
```

#### 4.2 Konfigurierbare Test-Projekt-Name-Muster (empfohlen)

**`TestSentinelConfig`** — Neues Feld:

```csharp
/// <summary>
/// Projekt-Name-Suffixe, die ein Projekt als Testprojekt kennzeichnen,
/// wenn keine bekannten Testrahmenbibliotheken in den Metadaten gefunden wurden.
/// Standard: ["Tests", "Test", "IntegrationTests", "Specs"].
/// </summary>
public IReadOnlyList<string> TestProjectNameSuffixes { get; init; } =
    ["Tests", "Test", "IntegrationTests", "Specs", "Spec"];
```

**`TestSentinelConfigOverride`** — entsprechendes nullable Feld.

**`TestProjectDetector`** — `config` als Parameter aufnehmen oder über neues Interface.

#### 4.3 Dokumentation

**`Docs/configuration.md`** — TestSentinel-Tabelle ergänzen:

```markdown
| `TestSentinel.TestProjectNameSuffixes` | Config | Projekt-Name-Suffixe, die ein Projekt als Testprojekt markieren, wenn keine Testrahmenbibliothek in den Metadaten erkannt wird. Standard: `["Tests", "Test", "IntegrationTests", "Specs", "Spec"]`. |
```

Erklärung hinzufügen:

> Der Sentinel erkennt Testprojekte primär über Metadaten-Referenzen (xunit, nunit etc.). Als Fallback gilt ein Projektname, der mit einem der konfigurierten Suffixe endet. Das deckt reine Integration-Test-Projekte ohne direkten xunit-Verweis ab.

#### 4.4 Tests

```
TestProjectDetector_IntegrationTestsProjectNameSuffix_IsRecognizedAsTest()
TestProjectDetector_SpecsProjectNameSuffix_IsRecognizedAsTest()
TestCoverageCollector_TypeofInAttributeArgument_IsCollected()   // Regression-Absicherung
StaticTestSentinel_HandlerCoveredViaTypeofInIntegrationTestAttribute_NoViolation()
```

---

## Idee 5: `AIContextFootprint` — `FootprintIgnoreNamespacePrefixes`

### Analyse

**Was bereits korrekt funktioniert:**  
`AIContextFootprintCalculator.QueueNamedSymbol` überspringt jeden Typ, der keine `DeclaringSyntaxReferences` hat (`if (originalSymbol.DeclaringSyntaxReferences.Length == 0) return;`). Das schließt alle echten Framework-Typen (MudBlazor, System._, Microsoft._) automatisch aus — sie haben keinen Quellcode in der Solution.

**Was NICHT abgedeckt ist:**  
Wenn ein Nutzer Drittanbieter-Quellcode direkt in die Solution eingebunden hat (z. B. als Git-Submodule oder NuGet-Source-Package), zählen diese Typen zum Footprint. Für solche Szenarien fehlt eine konfigurierbare Ausschlussliste.

**Partials-Problem:** Bereits korrekt gelöst durch `PostAnalysisChecks.DeduplicatePartialClasses` und die Tatsache, dass Roslyn für partial-Klassen denselben `INamedTypeSymbol` liefert. Nur Dokumentation fehlt.

### Maßnahmen

#### 5.1 Dokumentation: Partials-Verhalten explizit beschreiben

**`Docs/configuration.md`** — Abschnitt AI-Context-Footprint:

> Bei Blazor-Komponenten, die aus mehreren `partial`-Dateien bestehen (`.razor.cs` + weitere partials), werden alle Teile als eine logische Einheit behandelt. Der Footprint der Komponente wird einmal berechnet und einmal gemeldet — unabhängig von der Anzahl der Partial-Dateien.

#### 5.2 Neues Feature: `FootprintIgnoreNamespacePrefixes`

**`src/AiNetLinter/Configuration/LinterConfig.cs`** — In `MetricsConfig`:

```csharp
/// <summary>
/// Namespace-Präfixe von Typen, die beim Footprint nicht mitgezählt werden.
/// Nützlich wenn Drittanbieter-Quellcode in der Solution liegt.
/// Beispiel: ["MudBlazor.", "Vendor.Legacy."]
/// </summary>
public IReadOnlyCollection<string> FootprintIgnoreNamespacePrefixes { get; init; }
    = Array.Empty<string>();
```

In `MetricsConfig.Apply` / `ApplyPart2`:

```csharp
FootprintIgnoreNamespacePrefixes = @override?.FootprintIgnoreNamespacePrefixes ?? FootprintIgnoreNamespacePrefixes,
```

**`src/AiNetLinter/Configuration/LinterConfigOverrides.cs`** — In `MetricsConfigOverride`:

```csharp
/// <summary>
/// Namespace-Präfixe von Typen, die beim Footprint-Check ignoriert werden.
/// </summary>
public IReadOnlyCollection<string>? FootprintIgnoreNamespacePrefixes { get; init; }
```

**`src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs`** — Signatur erweitern:

```csharp
// Neue Überladung:
public static int Calculate(INamedTypeSymbol classSymbol, IReadOnlyCollection<string> ignoreNamespacePrefixes)
    => CalculateDetailed(classSymbol, ignoreNamespacePrefixes).TotalLines;

public static (int TotalLines, List<(string Name, int Lines)> TopDependencies) CalculateDetailed(
    INamedTypeSymbol classSymbol,
    IReadOnlyCollection<string>? ignoreNamespacePrefixes = null)
{
    var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    QueueSymbols(classSymbol, visited, ignoreNamespacePrefixes ?? []);
    // ...
}

private static void QueueNamedSymbol(
    INamedTypeSymbol namedType,
    HashSet<INamedTypeSymbol> visited,
    IReadOnlyCollection<string> ignoreNamespacePrefixes)
{
    var originalSymbol = namedType.OriginalDefinition;
    if (originalSymbol.DeclaringSyntaxReferences.Length == 0) return;

    // Namespace-Präfix-Ausschluss
    if (ignoreNamespacePrefixes.Count > 0)
    {
        var ns = originalSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ignoreNamespacePrefixes.Any(p => ns.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return;
    }
    // ...
}
```

**`src/AiNetLinter/Core/PostAnalysisChecks.cs`** — Footprint-Aufruf anpassen:

```csharp
var (footprint, topDeps) = AIContextFootprintCalculator.CalculateDetailed(
    cls.Symbol,
    effectiveConfig.Metrics.FootprintIgnoreNamespacePrefixes);
```

_(Hinweis: Falls `ClassInfo` das Symbol nicht cached, muss die Übergabe über den Analyse-Flow erfolgen.)_

#### 5.3 Dokumentation

**`Docs/configuration.md`** — Metrik-Tabelle ergänzen:

```markdown
| `FootprintIgnoreNamespacePrefixes` | Metrics | Namespace-Präfixe von Typen, die beim Footprint nicht gezählt werden. Nützlich wenn Drittanbieter-Quellcode direkt in der Solution liegt. Framework-Typen ohne Quellcode (MudBlazor NuGet, System.\*) werden immer automatisch ausgeschlossen. Standard: `[]`. |
```

#### 5.4 Tests

```
AIContextFootprintCalculator_IgnoredNamespacePrefix_NotCounted()
AIContextFootprintCalculator_NonIgnoredNamespacePrefix_IsCounted()
AIContextFootprintCalculator_EmptyIgnoreList_CountsAll()
PostAnalysisChecks_FootprintWithIgnoredPrefix_NoViolation()
```

---

## Idee 6: `PathOverrides` — Pfadbasierte Konfigurations-Overrides

### Analyse

Aktuell matcht `ProjectConfigResolver.ResolveForProject` nur den **Projektnamen** (z. B. `*.Tests`). Es gibt keine Möglichkeit, für bestimmte **Ordner innerhalb eines Projekts** andere Regeln zu definieren (z. B. `Handlers/**` oder `Components/**`).

**Anwendungsfälle:**

- `Handlers/**`: Handlers dürfen längere Methoden haben (komplexe Use-Case-Koordination)
- `Components/**`: Blazor-Komponenten brauchen keine `sealed`-Deklaration
- `Generated/**` oder `Migrations/**`: Komplett überspringen

**Design-Entscheidung:** Neue Sektion `PathOverrides` (nicht im bestehenden `ProjectOverrides`), um Rückwärtskompatibilität zu wahren. Ein Override-Key wird als Glob-Muster gegen den **relativen Dateipfad** ab Solution-Root gemacht.

### Maßnahmen

#### 6.1 Konfigurationsstruktur

**`src/AiNetLinter/Configuration/LinterConfig.cs`** — In `LinterConfig`:

```csharp
/// <summary>
/// Pfadbasierte Konfigurations-Überschreibungen. Der Key ist ein Glob-Muster
/// (z. B. "src/MyApp/Handlers/**") gegen den relativen Dateipfad ab Solution-Root.
/// Wird NACH ProjectOverrides angewendet; gewinnt bei Konflikt.
/// </summary>
public IReadOnlyDictionary<string, ProjectOverrideEntry> PathOverrides { get; init; }
    = new Dictionary<string, ProjectOverrideEntry>(StringComparer.OrdinalIgnoreCase);
```

#### 6.2 `ProjectConfigResolver` erweitern

**`src/AiNetLinter/Configuration/ProjectConfigResolver.cs`** — Neue Methode + Aufruf:

```csharp
public static LinterConfig ResolveForDocument(Document document, LinterConfig globalConfig)
{
    // Erst Projekt-Override anwenden:
    var config = ResolveForProject(document.Project.Name, globalConfig);

    // Dann Path-Override anwenden (hat höhere Priorität):
    if (document.FilePath != null && globalConfig.PathOverrides.Count > 0)
        config = ResolveForPath(document.FilePath, globalConfig.SolutionBasePath, config, globalConfig.PathOverrides);

    return config;
}

private static LinterConfig ResolveForPath(
    string filePath,
    string? solutionBasePath,
    LinterConfig config,
    IReadOnlyDictionary<string, ProjectOverrideEntry> pathOverrides)
{
    var relativePath = ResolveRelativePath(filePath, solutionBasePath);

    foreach (var pair in pathOverrides)
    {
        if (MatchesGlobPath(relativePath, pair.Key))
            return MergeConfig(config, pair.Value);
    }

    return config;
}

private static string ResolveRelativePath(string filePath, string? solutionBasePath)
{
    if (string.IsNullOrEmpty(solutionBasePath))
        return filePath;

    if (filePath.StartsWith(solutionBasePath, StringComparison.OrdinalIgnoreCase))
        return filePath[solutionBasePath.Length..].TrimStart('/', '\\').Replace('\\', '/');

    return filePath.Replace('\\', '/');
}

private static bool MatchesGlobPath(string relativePath, string pattern)
{
    // Unterstützte Muster: ** (multi-segment), * (single-segment)
    var regexPattern = "^" +
        Regex.Escape(pattern.Replace('\\', '/'))
             .Replace("\\*\\*", ".*")
             .Replace("\\*", "[^/]*")
        + ".*$";
    return Regex.IsMatch(relativePath, regexPattern, RegexOptions.IgnoreCase);
}
```

**`LinterConfig`** braucht `SolutionBasePath`:

```csharp
/// <summary>
/// Basis-Pfad der Solution (für relative Pfadberechnung bei PathOverrides).
/// Wird vom LinterEngine beim Laden gesetzt.
/// </summary>
public string? SolutionBasePath { get; init; }
```

Alternativ: `solutionBasePath` als separater Parameter in `ResolveForDocument` übergeben.

#### 6.3 `LinterEngine` anpassen

Die `LinterEngine` kennt den Solution-Pfad und kann ihn beim Config-Laden als `SolutionBasePath` setzen.

#### 6.4 Dokumentation

**`Docs/configuration.md`** — Neue Sektion nach `ProjectOverrides`:

````markdown
### Pfadbasierte Konfigurations-Overrides (PathOverrides)

`PathOverrides` erlaubt es, Regeln gezielt für bestimmte **Ordner** innerhalb einer Solution zu überschreiben — unabhängig vom Projektnamen. Der Key ist ein Glob-Muster gegen den relativen Dateipfad ab Solution-Root. `PathOverrides` werden NACH `ProjectOverrides` angewendet und gewinnen bei Konflikten.

```json
"PathOverrides": {
  "src/MyApp/Handlers/**": {
    "Metrics": {
      "MaxMethodLineCount": 80
    }
  },
  "src/MyApp/Components/**": {
    "Global": {
      "EnforceSealedClasses": false
    }
  },
  "src/MyApp/Migrations/**": {
    "Global": {
      "EnforceNoMagicValues": false,
      "EnforceNullableEnable": false
    }
  }
}
```
````

**Glob-Syntax:**

- `**` — matcht beliebig viele Pfadsegmente
- `*` — matcht ein einzelnes Pfadsegment (kein Slash)
- Pfade werden mit Forward-Slashes verglichen (auch auf Windows)

```

**`README.md`** — Feature-Liste ergänzen.

#### 6.5 Tests

```

PathOverrides_FileInMatchingPath_OverrideApplied()
PathOverrides_FileNotInMatchingPath_GlobalConfigUsed()
PathOverrides_AppliedAfterProjectOverride_PathWins()
PathOverrides_DoubleStar_MatchesNestedFolders()
PathOverrides_SingleStar_DoesNotMatchSubfolders()
PathOverrides_CaseInsensitiveMatch_OnWindows()
PathConfigResolver_SolutionBasePath_Resolved_Correctly()

```

#### 6.6 `CursorRulesGenerator` anpassen

Falls `PathOverrides` in der Config vorhanden sind, können sie optional in der `.mdc`-Datei als Hinweis ausgegeben werden.

---

## Reihenfolge der Umsetzung (empfohlen)

| Reihenfolge | Idee | Begründung |
|:-:|:---|:---|
| 1 | Idee 2 | Reiner Doku-Fix, kein Risiko |
| 2 | Idee 3 | Bug-Fix, minimaler Code-Change, verhindert False-Positives |
| 3 | Idee 1 | Doku + kleines Feature, unabhängig von allem anderen |
| 4 | Idee 4 | Verbesserung des Sentinel, geringes Risiko |
| 5 | Idee 5 | Neues Feature, in sich geschlossen |
| 6 | Idee 6 | Größtes Feature, braucht sorgfältige Tests |

Jede Idee kann unabhängig deployed werden (eigener Commit/Release).

---

## Querschnittsaufgaben (für alle Ideen)

### Docs/ROADMAP.md
Erledigte Features aus der Roadmap abstreichen, neue ergänzen.

### rules.json (Root)
Die Root-`rules.json` ist die Single Source of Truth. Für jede neue Config-Eigenschaft:
1. In `rules.json` mit sinnvollem Default ergänzen
2. `--sync-cursor-rules` ausführen → `.cursor/rules/AiNetLinter.mdc` aktualisiert sich automatisch

### Cache-Invalidierung
Neue Config-Keys ändern den JSON-Hash der `rules.json` → Cache wird automatisch invalidiert. Kein manueller Eingriff nötig.

### Codegraph
Nach Code-Änderungen: `--graph Docs/codegraph.md` ausführen um `Docs/codegraph.md` zu aktualisieren.

---

## Abgelehnte / modifizierte Cursor-Ideen

| Cursor-Idee | Status | Begründung |
|:---|:---:|:---|
| `BlazorCssIsolationOnlyWhenStylesNeeded` als neues Feature | Nur Doku | Bereits vollständig implementiert (Default: `true`). Nur Dokumentation fehlt. |
| AIContextFootprint Partials als eine Einheit | Nur Doku | Bereits korrekt durch `DeduplicatePartialClasses` + Roslyn-Symbol-Semantik. Nur Dokumentation fehlt. |
| AIContextFootprint Framework-/Mud-Typen abziehen | Konfigurierbar | Framework-Typen ohne Quellcode werden bereits automatisch ausgeschlossen. Nur für Sonderfälle (Vendor-Quellcode in Solution) ist `FootprintIgnoreNamespacePrefixes` nötig. |
| StaticTestSentinel `[SiteHandler]`-Attribut-Abdeckung | Bereits implementiert + Erweiterung | `typeof(T)` in Attribut-Argumenten wird bereits durch `DescendantNodes` erkannt. Verbesserung: IntegrationTest-Projekt-Erkennung via Namens-Suffix. |
```
