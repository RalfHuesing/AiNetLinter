# AiNetLinter — False-Positive-Erweiterungen

**Kontext:** Analyse des San.smart.Planner.Platform-Projekts (212 Violations, 2026-06-17) hat vier systematische
False-Positive-Muster aufgedeckt. Dieses Dokument beschreibt die geplanten AiNetLinter-Erweiterungen
als vollständig umsetzbaren Plan (Config, Analyzer-Logik, Tests, Docs).

---

## Übersicht der Änderungen

| # | Feature | Regel | Typ | Priorität |
|---|---------|-------|-----|-----------|
| 1 | `AllowOutParametersInPrivateMethods` | `AllowOutParameters` | Neue Config-Option | Hoch |
| 2 | `SemanticNamingExemptMethodNames` | `EnforceSemanticNaming` | Neue Config-Option | Hoch |
| 3 | `FootprintIgnoreTypeNames` | `AIContextFootprint` | Neue Config-Option | Mittel |
| 4 | `SemanticNamingAllowSubstringOfMethodName` | `EnforceSemanticNaming` | Neue Config-Option | Niedrig |

---

## Feature 1 — `AllowOutParametersInPrivateMethods`

### Problem

`CheckOutParameter` → `ShouldReportOutParameter` in `LinterAnalyzer.State.cs` prüft
**alle** Methoden, egal ob public oder private. Private Implementierungsdetails wie

```csharp
private static void ParseHorizontalSplit(string splitId, out int rowIndex, out int paneIndex) { … }
private static string? BuildNavigationSiblingKeys(…, out List<string>? keys) { … }
```

werden gemeldet, obwohl die Intention der Regel nur die **öffentliche API-Oberfläche** betrifft.
Private Methoden sind ein Implementierungsdetail; `out` ist dort ein etabliertes C#-Idiom
für interne Zerlegungshelfer.

### Lösung

Neue bool-Option in `GlobalConfig`. Standard `false` (kein Behavior-Change, backward-compatible).

#### 1a — `LinterConfig.cs` — `GlobalConfig`

```csharp
/// <summary>
/// Wenn true: <c>out</c>-Parameter in privaten Methoden werden nicht gemeldet.
/// Nützlich wenn private Hilfsmethoden intern das <c>out</c>-Idiom nutzen,
/// die öffentliche API aber trotzdem sauber gehalten werden soll.
/// Standard: false (Regel gilt für alle Sichtbarkeiten).
/// </summary>
public bool AllowOutParametersInPrivateMethods { get; init; } = false;
```

Außerdem in `GlobalConfigOverride` (Datei `LinterConfigOverrides.cs`):

```csharp
public bool? AllowOutParametersInPrivateMethods { get; init; }
```

Und in `GlobalConfig.ApplyCore1a`:

```csharp
AllowOutParametersInPrivateMethods = @override.AllowOutParametersInPrivateMethods
    ?? AllowOutParametersInPrivateMethods,
```

#### 1b — `LinterAnalyzer.State.cs` — `ShouldReportOutParameter`

```csharp
private bool ShouldReportOutParameter(ParameterSyntax node)
{
    if (_config.Global.AllowOutParameters) return false;
    if (!node.Modifiers.Any(SyntaxKind.OutKeyword)) return false;
    if (IsAllowedTryPatternOut(node)) return false;
    if (IsOutParamInInterfaceImplementationOrOverride(node)) return false;
    if (IsOutParamInContractDefinition(node)) return false;
    // NEU:
    if (_config.Global.AllowOutParametersInPrivateMethods && IsPrivateMethod(node)) return false;
    return true;
}

/// <summary>
/// True wenn der Parameter in einer explizit privaten oder standardmäßig privaten Methode liegt.
/// Standardsichtbarkeit (kein Modifier) in einer Klasse = private.
/// </summary>
private static bool IsPrivateMethod(ParameterSyntax node)
{
    var method = node.Ancestors()
        .OfType<MethodDeclarationSyntax>()
        .FirstOrDefault();
    if (method is null) return false;

    // Explizit private
    if (method.Modifiers.Any(SyntaxKind.PrivateKeyword)) return true;

    // Keine Accessibility-Modifier = private (in Klassenkontext)
    var hasAccessibility = method.Modifiers.Any(m =>
        m.IsKind(SyntaxKind.PublicKeyword)
        || m.IsKind(SyntaxKind.ProtectedKeyword)
        || m.IsKind(SyntaxKind.InternalKeyword));

    return !hasAccessibility;
}
```

#### 1c — `CursorRulesGenerator.cs`

In `AppendDynamicOutRestrictions` (Zeile ~226) den Hinweis ergänzen wenn die Option aktiv ist:

```csharp
if (!g.AllowOutParameters)
{
    var outText = g.AllowTryPatternOutParameters
        ? "`out` nur in `Try*`-Methoden"
        : "kein `out`";
    if (g.AllowOutParametersInPrivateMethods)
        outText += " (private Methoden ausgenommen)";
    parts.Add(outText);
}
```

#### 1d — Unit-Tests (`LinterAnalyzerTests.cs` oder neue Datei `OutParameterTests.cs`)

```csharp
[Fact]
public void AllowOutParametersInPrivateMethods_True_DoesNotFlagPrivateMethod()
{
    const string code = """
        #nullable enable
        public sealed class Parser
        {
            private static void Split(string s, out int a, out int b)
            {
                a = 1; b = 2;
            }
        }
        """;

    var config = TestConfigFactory.Default() with
    {
        Global = TestConfigFactory.Default().Global with
        {
            AllowOutParameters = false,
            AllowTryPatternOutParameters = true,
            AllowOutParametersInPrivateMethods = true
        }
    };

    var violations = Analyze(code, config);
    Assert.Empty(violations.Where(v => v.RuleName == "AllowOutParameters"));
}

[Fact]
public void AllowOutParametersInPrivateMethods_False_FlagsPrivateMethod()
{
    const string code = """
        #nullable enable
        public sealed class Parser
        {
            private static void Split(string s, out int a, out int b)
            {
                a = 1; b = 2;
            }
        }
        """;

    var config = TestConfigFactory.Default() with
    {
        Global = TestConfigFactory.Default().Global with
        {
            AllowOutParameters = false,
            AllowTryPatternOutParameters = true,
            AllowOutParametersInPrivateMethods = false  // Standard
        }
    };

    var violations = Analyze(code, config);
    Assert.Contains(violations, v => v.RuleName == "AllowOutParameters");
}

[Fact]
public void AllowOutParametersInPrivateMethods_True_StillFlagsPublicMethod()
{
    const string code = """
        #nullable enable
        public sealed class Converter
        {
            public static void GetRange(int n, out int start, out int end)
            {
                start = 0; end = n;
            }
        }
        """;

    var config = TestConfigFactory.Default() with
    {
        Global = TestConfigFactory.Default().Global with
        {
            AllowOutParameters = false,
            AllowTryPatternOutParameters = true,
            AllowOutParametersInPrivateMethods = true
        }
    };

    var violations = Analyze(code, config);
    // public Methode wird TROTZDEM gemeldet
    Assert.Contains(violations, v => v.RuleName == "AllowOutParameters");
}

[Fact]
public void AllowOutParametersInPrivateMethods_True_DoesNotAffectProtectedMethod()
{
    // protected ist nicht private — soll weiterhin gemeldet werden
    const string code = """
        #nullable enable
        public class Base
        {
            protected void GetParts(string s, out int a, out int b)
            {
                a = 1; b = 2;
            }
        }
        """;

    var config = TestConfigFactory.Default() with
    {
        Global = TestConfigFactory.Default().Global with
        {
            AllowOutParameters = false,
            AllowTryPatternOutParameters = false,
            AllowOutParametersInPrivateMethods = true
        }
    };

    var violations = Analyze(code, config);
    Assert.Contains(violations, v => v.RuleName == "AllowOutParameters");
}
```

#### 1e — Dokumentation

In `AiNetLinter.mdc` (auto-generiert durch `CursorRulesGenerator`) wird der Hinweis automatisch
angepasst sobald die Option aktiv ist. Zusätzlich in `README.md` / `--readme`-Output ergänzen:

```
AllowOutParametersInPrivateMethods (Global, bool, default: false)
  Wenn true: out-Parameter in privaten Methoden werden von AllowOutParameters nicht gemeldet.
  Nützlich in Projekten mit no-DI-Architektur, die private Zerlegungshelfer intern nutzen.
```

---

## Feature 2 — `SemanticNamingExemptMethodNames`

### Problem

`CheckSemanticNaming` in `LinterAnalyzer.Naming.cs` flaggt **alle** öffentlichen Methoden,
inklusive Standard-C#-Overrides bei denen der Parametername konventionell ist:

```csharp
public override bool Equals(object? obj) => …   // "obj" ist BCL-Standard
public int CompareTo(object? obj) => …           // "obj" ist BCL-Standard
```

`obj` steht in der `ForbiddenNames`-HashSet. Umbenennen würde den Code nicht-idiomatisch machen.

### Lösung

Neue `IReadOnlyCollection<string>` Option. Wenn der **Methodenname** (exact match, case-insensitive)
in der Liste steht, wird `CheckSemanticNaming` für diese Methode übersprungen.

#### 2a — `LinterConfig.cs` — `GlobalConfig`

```csharp
/// <summary>
/// Methoden-Namen, für die <c>EnforceSemanticNaming</c> nicht geprüft wird.
/// Nützlich für Standard-C#-Overrides wie <c>Equals(object? obj)</c> oder
/// <c>CompareTo(object? obj)</c>, bei denen der Parametername konventionell ist.
/// Standard: ["Equals", "CompareTo", "GetHashCode"] (BCL-Overrides).
/// </summary>
public IReadOnlyCollection<string> SemanticNamingExemptMethodNames { get; init; }
    = ["Equals", "CompareTo", "GetHashCode"];
```

In `GlobalConfigOverride`:

```csharp
public IReadOnlyCollection<string>? SemanticNamingExemptMethodNames { get; init; }
```

In `GlobalConfig.ApplyCore2a` (oder eine neue ApplyCore-Methode wenn voll):

```csharp
SemanticNamingExemptMethodNames = @override.SemanticNamingExemptMethodNames
    ?? SemanticNamingExemptMethodNames,
```

#### 2b — `LinterAnalyzer.Naming.cs` — `CheckSemanticNaming`

Die Methode erhält einen zusätzlichen Parameter `methodName`:

```csharp
private void CheckSemanticNaming(ParameterListSyntax parameterList, bool isPublicMethod, string? methodName = null)
{
    if (ShouldSkipSemanticNaming(isPublicMethod, methodName)) return;

    var genericNames = ForbiddenNames;
    foreach (var param in parameterList.Parameters)
    {
        CheckParameterSemantic(param, genericNames);
    }
}

private bool ShouldSkipSemanticNaming(bool isPublicMethod, string? methodName)
{
    if (!_config.Global.EnforceSemanticNaming) return true;
    if (!isPublicMethod) return true;
    if (_isTestFile) return true;
    // NEU: Exempt-Liste prüfen
    if (methodName is not null
        && _config.Global.SemanticNamingExemptMethodNames.Contains(
               methodName, StringComparer.OrdinalIgnoreCase))
        return true;
    return false;
}
```

**Alle Aufrufstellen** von `CheckSemanticNaming` müssen den Methodennamen übergeben. Suche
im LinterAnalyzer nach dem Aufruf (wahrscheinlich in `VisitMethodDeclaration`):

```csharp
// Vorher:
CheckSemanticNaming(node.ParameterList, isPublic);

// Nachher:
CheckSemanticNaming(node.ParameterList, isPublic, node.Identifier.Text);
```

#### 2c — Unit-Tests

```csharp
[Fact]
public void SemanticNaming_EqualsOverride_ObjNotFlagged_ByDefault()
{
    const string code = """
        #nullable enable
        public sealed class MyType
        {
            public override bool Equals(object? obj) => obj is MyType;
            public override int GetHashCode() => 0;
        }
        """;

    var config = TestConfigFactory.WithSemanticNaming(enabled: true);
    var violations = Analyze(code, config);
    Assert.Empty(violations.Where(v => v.RuleName == "EnforceSemanticNaming"));
}

[Fact]
public void SemanticNaming_EqualsOverride_FlaggedWhenRemovedFromExemptList()
{
    const string code = """
        #nullable enable
        public sealed class MyType
        {
            public override bool Equals(object? obj) => obj is MyType;
        }
        """;

    var config = TestConfigFactory.Default() with
    {
        Global = TestConfigFactory.Default().Global with
        {
            EnforceSemanticNaming = true,
            SemanticNamingExemptMethodNames = []  // Leer = kein Exempt
        }
    };

    var violations = Analyze(code, config);
    Assert.Contains(violations, v => v.RuleName == "EnforceSemanticNaming");
}

[Fact]
public void SemanticNaming_CustomExemptMethod_NotFlagged()
{
    // Nutzer-definierte Exempt-Liste für projektspezifische Muster
    const string code = """
        #nullable enable
        public sealed class Processor
        {
            public void Process(object? data) { }  // wäre normalerweise flagged
        }
        """;

    var config = TestConfigFactory.Default() with
    {
        Global = TestConfigFactory.Default().Global with
        {
            EnforceSemanticNaming = true,
            SemanticNamingExemptMethodNames = ["Process"]
        }
    };

    var violations = Analyze(code, config);
    Assert.Empty(violations.Where(v => v.RuleName == "EnforceSemanticNaming"));
}

[Fact]
public void SemanticNaming_NormalPublicMethod_DataStillFlagged()
{
    const string code = """
        #nullable enable
        public sealed class Service
        {
            public void Handle(object? data) { }
        }
        """;

    var config = TestConfigFactory.WithSemanticNaming(enabled: true);
    var violations = Analyze(code, config);
    Assert.Contains(violations, v => v.RuleName == "EnforceSemanticNaming");
}
```

#### 2d — Dokumentation

In `README.md` / `--readme`:

```
SemanticNamingExemptMethodNames (Global, string[], default: ["Equals", "CompareTo", "GetHashCode"])
  Methoden-Namen die von EnforceSemanticNaming ausgenommen sind.
  Standard-Exemptions decken BCL-Overrides ab bei denen Parameternamen wie 'obj' konventionell sind.
  Erweiterbar für projektspezifische Muster (z.B. Framework-Callbacks mit festem Signatur-Vertrag).
```

---

## Feature 3 — `FootprintIgnoreTypeNames`

### Problem

`FootprintIgnoreNamespacePrefixes` (bereits vorhanden) erlaubt nur Namespace-Filterung.
Wenn ein Typ wie `SqlExecutor` (Footprint 625) oder `DataTableDragDropSettings` (456) in
nahezu jeder Klasse transitiv auftaucht und der Footprint dadurch strukturell immer ≈ 1000+
beträgt, ist eine Typ-Name-basierte Exclusion sinnvoller.

Betrifft Klassen die als **Infrastruktur-Omnipräsenz-Typen** bekannt sind und deren Footprint
kein sinnvolles Qualitätssignal mehr ist.

### Lösung

Neue `IReadOnlyCollection<string>` in `MetricsConfig`. Einfache Typ-Namen (kein Namespace).

#### 3a — `LinterConfig.cs` — `MetricsConfig`

```csharp
/// <summary>
/// Einfache Typ-Namen (kein Namespace), die beim AIContextFootprint nicht mitgezählt werden.
/// Nützlich für Infrastruktur-Omnipräsenz-Typen, die praktisch überall transitiv vorhanden
/// sind und das Footprint-Budget strukturell immer ausschöpfen.
/// Beispiel: ["SqlExecutor", "DataTableDragDropSettings"]
/// Standard: [] (alle Typen zählen).
/// </summary>
public IReadOnlyCollection<string> FootprintIgnoreTypeNames { get; init; }
    = Array.Empty<string>();
```

In `MetricsConfigOverride`:

```csharp
public IReadOnlyCollection<string>? FootprintIgnoreTypeNames { get; init; }
```

In `MetricsConfig.ApplyPart2a`:

```csharp
FootprintIgnoreTypeNames = @override?.FootprintIgnoreTypeNames ?? FootprintIgnoreTypeNames,
```

#### 3b — Footprint-Berechnung (Datei finden: `PostAnalysisChecks.cs` oder `FootprintExecutor.cs`)

Wo die transitiven Abhängigkeiten gesammelt werden, die Filterung ergänzen:

```csharp
// Existing: Namespace-Prefix-Filter
if (FootprintIgnoreNamespacePrefixes.Any(p => typeNamespace.StartsWith(p))) continue;

// NEU: Typ-Name-Filter (einfacher Name, kein Namespace)
if (config.Metrics.FootprintIgnoreTypeNames.Contains(
        typeName, StringComparer.OrdinalIgnoreCase)) continue;
```

> **Hinweis:** Die genaue Einfügestelle muss beim der Implementierung in `PostAnalysisChecks.cs`
> bzw. dem Footprint-Akkumulierungsloop gesucht werden. Die Logik folgt exakt dem bestehenden
> Namespace-Prefix-Filter, nur auf den einfachen Typ-Namen angewendet.

#### 3c — Unit-Tests

```csharp
[Fact]
public void FootprintIgnoreTypeNames_ExcludesNamedType_FromFootprint()
{
    // Szenario: HeavyService hat Footprint > Limit, aber ist in der Ignore-Liste
    // → kein Verstoß für Klassen die nur über HeavyService verbunden sind

    // Konkreter Test-Code abhängig vom Footprint-Test-Setup im Projekt.
    // Orientierung an bestehenden AIContextFootprint-Tests in LinterAnalyzerTests.cs.
    // Mindestens: Verify dass FootprintIgnoreTypeNames den Footprint-Wert senkt.
}

[Fact]
public void FootprintIgnoreTypeNames_CaseInsensitive()
{
    // "sqlexecutor" und "SqlExecutor" sollen gleich behandelt werden
}
```

#### 3d — Dokumentation

```
FootprintIgnoreTypeNames (Metrics, string[], default: [])
  Einfache Typ-Namen die bei AIContextFootprint nicht mitgezählt werden.
  Ergänzung zu FootprintIgnoreNamespacePrefixes für Infrastruktur-Omnipräsenz-Typen
  die durch den ganzen Dependency-Graphen fließen (z.B. zentrale SqlExecutor-Klassen).
  Nur einfacher Name (kein Namespace): z.B. "SqlExecutor" nicht "MyApp.Infra.SqlExecutor".
```

---

## Feature 4 — `SemanticNamingAllowSubstringOfMethodName`

### Problem

Borderline-Fall: Parameter-Name der semantisch korrekt ist, weil der **Methodenname**
die Domäne trägt:

```csharp
public async Task AppendTimelineItemAsync(string containerDomId, Dictionary<string, object?> item)
//                                         ^ "item" ist im Methodennamen "TimelineItem" enthalten
public static SchedulerMutationEcho FromHandlerData(object? data)
//                                                           ^ "data" in "HandlerData" enthalten
```

### Lösung

Neue bool-Option. Wenn aktiv und der Parametername (case-insensitive) ein Substring des
Methodennamens ist, wird er nicht gemeldet.

#### 4a — `LinterConfig.cs` — `GlobalConfig`

```csharp
/// <summary>
/// Wenn true: Ein Parameter-Name wird von <c>EnforceSemanticNaming</c> nicht gemeldet,
/// wenn er als Teilstring (case-insensitiv) im Methoden-Namen vorkommt.
/// Beispiel: Parameter "item" in Methode "AppendTimelineItemAsync" → nicht flaggen.
/// Standard: false (konservativ, kein Behavior-Change).
/// </summary>
public bool SemanticNamingAllowSubstringOfMethodName { get; init; } = false;
```

#### 4b — `LinterAnalyzer.Naming.cs` — `CheckParameterSemantic`

```csharp
private void CheckParameterSemantic(
    ParameterSyntax param,
    HashSet<string> genericNames,
    string? methodName)
{
    var name = param.Identifier.Text;
    if (!genericNames.Contains(name)) return;

    // NEU: Wenn Option aktiv und Parameter-Name im Methoden-Namen enthalten
    if (_config.Global.SemanticNamingAllowSubstringOfMethodName
        && methodName is not null
        && methodName.Contains(name, StringComparison.OrdinalIgnoreCase))
        return;

    _violations.Add(…);
}
```

#### 4c — Unit-Tests

```csharp
[Fact]
public void SemanticNaming_SubstringOfMethod_NotFlagged_WhenOptionEnabled()
{
    const string code = """
        #nullable enable
        public sealed class Scheduler
        {
            public void AppendTimelineItemAsync(string id, object? item) { }
        }
        """;

    var config = TestConfigFactory.Default() with
    {
        Global = TestConfigFactory.Default().Global with
        {
            EnforceSemanticNaming = true,
            SemanticNamingAllowSubstringOfMethodName = true
        }
    };

    var violations = Analyze(code, config);
    Assert.Empty(violations.Where(v => v.RuleName == "EnforceSemanticNaming"));
}

[Fact]
public void SemanticNaming_SubstringOfMethod_StillFlagged_WhenOptionDisabled()
{
    const string code = """
        #nullable enable
        public sealed class Scheduler
        {
            public void AppendTimelineItemAsync(string id, object? item) { }
        }
        """;

    var config = TestConfigFactory.Default() with
    {
        Global = TestConfigFactory.Default().Global with
        {
            EnforceSemanticNaming = true,
            SemanticNamingAllowSubstringOfMethodName = false
        }
    };

    var violations = Analyze(code, config);
    Assert.Contains(violations, v => v.RuleName == "EnforceSemanticNaming");
}
```

---

## Implementierungsreihenfolge

```
Tag 1 — Feature 1 (AllowOutParametersInPrivateMethods)
  1. GlobalConfig + GlobalConfigOverride erweitern
  2. Apply-Methode ergänzen
  3. ShouldReportOutParameter + IsPrivateMethod implementieren
  4. CursorRulesGenerator anpassen
  5. Tests schreiben + grün machen
  6. README / --readme-Text ergänzen

Tag 1 — Feature 2 (SemanticNamingExemptMethodNames) — parallelisierbar
  1. GlobalConfig + GlobalConfigOverride erweitern (Default: ["Equals", "CompareTo", "GetHashCode"])
  2. Apply-Methode ergänzen
  3. ShouldSkipSemanticNaming + CheckSemanticNaming anpassen
  4. Alle Aufrufstellen methodName übergeben
  5. Tests schreiben + grün machen
  6. README ergänzen

Tag 2 — Feature 3 (FootprintIgnoreTypeNames)
  1. MetricsConfig + MetricsConfigOverride erweitern
  2. Apply-Methode ergänzen
  3. Footprint-Akkumulierungsloop in PostAnalysisChecks/FootprintExecutor anpassen
  4. Tests + README

Tag 2 — Feature 4 (SemanticNamingAllowSubstringOfMethodName) — optional
  1. GlobalConfig + Override
  2. CheckParameterSemantic anpassen (methodName-Parameter)
  3. Tests + README
```

## Release-Hinweis

Alle vier Änderungen sind **backward-compatible** (neue Options mit Default = Vorverhalten).
Kein Behavior-Change für bestehende `rules.json`-Dateien ohne die neuen Felder.
Dokumentation in `--readme` und `CursorRulesGenerator` auto-aktualisiert sich durch die
bestehende Code-Generierung.

Nach Umsetzung: Version bump, `AiNetLinter.mdc`-Regenerierung im San.smart.Planner.Platform-Projekt
auslösen (oder manuell `platform-default.rules.json` um die neuen Options ergänzen).
