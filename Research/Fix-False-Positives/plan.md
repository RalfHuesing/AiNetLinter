# False-Positive-Behebung in AiNetLinter

> Analysebasis: `San.smart.Planner.Platform` mit `platform-default.rules.json`  
> Datum: 2026-06-16  
> 638 Violations — davon schätzungsweise 140–180 False Positives

---

## Übersicht der Änderungen

| # | Regel | Problem | Fix-Typ | Priorität |
|---|---|---|---|---|
| 1 | `EnforceReadonlyFields` | Cross-partial-class Schreibzugriffe unsichtbar | Bug-Fix Engine | **Hoch** |
| 2 | `EnforceReadonlyFields` | Declaration-after-write Reihenfolgeabhängigkeit | Bug-Fix Engine | **Hoch** |
| 3 | `MaxMethodParameterCount` | Override/Interface-Methoden können Signatur nicht ändern | Bug-Fix Analyzer | **Hoch** |
| 4 | `EnforceNoVariableShadowing` | `_` Discard-Identifier wird fälschlich als Shadowing gewertet | Bug-Fix Analyzer | **Mittel** |
| 5 | `EnforceNoSilentCatch` | Kein sauberer Escape für typisierte leere Catches (z.B. JSInterop) | Neue Config-Option | **Mittel** |
| 6 | `AllowOutParameters` | `AllowTryPatternOutParameters` erkennt nur `bool`-rückgebende `Try*`-Methoden | Config-Erweiterung | **Niedrig** |

---

## Fix 1 — `EnforceReadonlyFields`: Cross-Partial-Class-Bug

### Problem

`FieldReadonlyTracker` ist pro `LinterAnalyzer`-Instanz — d.h. pro Datei. In Blazor-Projekten sind Klassen regelmäßig auf mehrere `.cs`-Dateien aufgeteilt (Partial Classes). Schreibzugriffe auf ein Feld in Datei B werden vom Analyzer von Datei A nie gesehen.

**Konkretes Beispiel (San.smart.Planner.Platform):**

```csharp
// SitePageSplitLayout.razor.cs — Felddeklaration
private string? _activeSplitId;   // → Linter meldet fälschlich "readonly"
private bool _isVerticalDrag;
private double _dragStartPointer;
// ... +7 weitere Drag-State-Felder
```

```csharp
// SitePageSplitLayout.SplitterResize.cs — Schreibzugriffe (unsichtbar für Analyzer!)
_activeSplitId = splitId;
_isVerticalDrag = isVerticalSplit;
_dragStartPointer = e.ClientY;
```

Außerdem betroffen:
- `_isSavingUserConfig` in `Scheduler.UserConfig.razor.cs` → geschrieben in `Scheduler.UserConfig.Save.cs`
- `@ref`-Felder (`_mudTable`, `_tableRoot`, `_rootRef`, `_bodyErrorBoundary`) → Schreibzugriff im generierten `.g.cs` (excluded)

**Betroffene Violations:** ~20–24 der 27 `EnforceReadonlyFields`-Violations

### Root Cause (Code)

`src/AiNetLinter/Core/LinterAnalyzer.cs:26`
```csharp
private readonly FieldReadonlyTracker _fieldTracker = new();  // pro Datei — isoliert
```

`src/AiNetLinter/Core/LinterAnalyzer.State.cs:260-270`
```csharp
private void RegisterFieldWrite(ExpressionSyntax expression)
{
    var symbol = _semanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
    if (symbol == null || !_fieldTracker.IsCandidate(symbol)) return;  // IsCandidate gibt false zurück
    // → Schreibzugriff aus Datei B findet keine Kandidaten aus Datei A
```

### Fix-Strategie

**Zwei-Phasen-Ansatz im `LinterEngine`:**

**Phase 1 — Field-Write-Collection (sequenziell nach Parallel-Analyse):**  
Die `FieldReadonlyTracker`-Instanz an `AnalyzerArgs` übergeben. Für alle Dateien desselben Partial-Class-Verbunds wird eine **geteilte** Tracker-Instanz verwendet.

**Identifikation von Partial-Class-Dateien:**  
Über `INamedTypeSymbol`-Gleichheit aus dem Roslyn-Semantic-Model (exakt, ohne Heuristiken).

**Konkrete Umsetzung:**

1. **`LinterEngine`**: Nach dem ersten Analyse-Pass alle `IFieldSymbol`-Kandidaten und Schreibzugriffe pro Klasse aggregieren — getrennt von der Violations-Ausgabe.

2. Alternativ (einfacher, sofort umsetzbar): Im `LinterEngine` NACH der Parallel-Analyse eine **zweite, serielle Pass-Schleife** über alle partial-class-Gruppen ausführen. Dabei den `FieldReadonlyTracker` mit den gesammelten Schreibzugriffen befüllen und dann die Violations berechnen.

3. Neues Modell: `FieldReadonlyTracker` aus `LinterAnalyzer` herauslösen und in `AnalysisState` als `ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker>` führen.

**@ref-Felder (Blazor-spezifisch):**  
`ElementReference`-Felder und `[Parameter]`-Felder in Blazor-Komponenten werden vom Razor-Compiler in `.g.cs`-Dateien beschrieben. Pragmatische Lösung: Felder vom Typ `ElementReference` oder mit `@ref`-typischen Namen-Pattern von `EnforceReadonlyFields` ausnehmen. Besser: Falls der Typ `Microsoft.AspNetCore.Components.ElementReference` ist, nicht als Kandidaten registrieren.

### Tests

**Neue Testklasse:** `src/AiNetLinter.Tests/Core/ReadonlyFieldsPartialClassTests.cs`

```
Szenario A: Feld in Datei A, Schreibzugriff in Datei B (Partial Class)
  → Kein ReadonlyFields-Verstoß erwartet

Szenario B: Feld in Datei A, KEIN Schreibzugriff in anderen Dateien
  → ReadonlyFields-Verstoß erwartet

Szenario C: Feld vom Typ ElementReference
  → Kein ReadonlyFields-Verstoß erwartet

Szenario D: Feld in Datei A, Schreibzugriff via `ref`-Argument (z.B. Volatile.Write)
  → Kein ReadonlyFields-Verstoß erwartet (Regression für Declaration-after-write, Fix 2)
```

---

## Fix 2 — `EnforceReadonlyFields`: Declaration-after-Write Reihenfolge

### Problem

Der `CSharpSyntaxWalker` besucht Nodes in Quellcode-Reihenfolge. Wenn ein Feld NACH der Methode deklariert ist, die darauf schreibt, ist das Feld beim `VisitArgument`-Aufruf noch nicht in `_fieldTracker` registriert — der Schreibzugriff wird ignoriert.

**Konkretes Beispiel:**
```csharp
// Zeile 187: Schreibzugriff — BEVOR das Feld bei Zeile 292 deklariert wird
Volatile.Write(ref s_schemaEnsured, 1);

// ...

// Zeile 292: Felddeklaration
private static int s_schemaEnsured;
```

`RegisterFieldWrite` prüft `_fieldTracker.IsCandidate(symbol)` → gibt `false` zurück, weil `RegisterPrivateFieldSymbol` (aus `VisitFieldDeclaration`) noch nicht aufgerufen wurde.

### Fix-Strategie

**Pre-Registration-Pass:** Vor dem Haupt-Walker-Pass alle privaten Felder im Syntax-Tree vorregistrieren.

```csharp
// In LinterAnalyzer.cs — RunAnalysis():
private void PreRegisterPrivateFields()
{
    if (!_config.Global.EnforceReadonlyFields) return;
    
    foreach (var field in _tree.GetRoot()
        .DescendantNodes()
        .OfType<FieldDeclarationSyntax>())
    {
        AnalyzePrivateFields(field);  // bestehende Methode — registriert Kandidaten
    }
}
```

`RunAnalysis` erweitern:
```csharp
internal void RunAnalysis()
{
    CheckLineCount();
    CheckNullableEnable();
    CheckNamespaceDirectoryMapping();
    PreRegisterPrivateFields();   // NEU — vor Visit()
    Visit(_tree.GetRoot());
    CheckReadonlyFields();
    FilterSuppressedViolations();
}
```

Dadurch ist `VisitFieldDeclaration` weiterhin aufgerufen (doppelte Registrierung ist safe, weil `RegisterCandidate` ein `HashSet` verwendet), aber alle Felder sind bereits vor dem ersten `VisitArgument`/`VisitAssignmentExpression` bekannt.

### Tests

Im selben `ReadonlyFieldsPartialClassTests.cs`:
```
Szenario E: Feld NACH der schreibenden Methode deklariert (gleiche Datei)
  → Kein ReadonlyFields-Verstoß erwartet

Szenario F: Feld VOR der schreibenden Methode deklariert (gleiche Datei, Baseline)
  → Kein ReadonlyFields-Verstoß erwartet
```

---

## Fix 3 — `MaxMethodParameterCount`: Override / Interface-Implementierungen

### Problem

Override-Methoden und explizite Interface-Implementierungen **können ihre Parameterliste nicht ändern** — sie sind durch den Basistyp oder das Interface fixiert. Der Linter schlägt dennoch das Parameter-Object-Pattern vor, was zu Compile-Fehlern führen würde.

**Konkretes Beispiel (San.smart.Planner.Platform):**

```csharp
// Implementiert ILogger<T>.Log<TState> — 5 Parameter, nicht änderbar
public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
    Exception? exception, Func<TState, Exception?, string> formatter) { ... }

// override — Signatur kommt aus UserConfigService
public override Task<IReadOnlyDictionary<string, string>> LoadPageConfigsAsync(
    string username, string siteSlug, string pageRoute,
    string? configVariant = default, CancellationToken cancellationToken = default) { ... }
```

**Betroffene Violations:** ~60–100 der 247 `MaxMethodParameterCount`-Violations  
(7× `Log`, viele `override`-Methoden in InMemoryUserConfigService u.a.)

### Root Cause (Code)

`src/AiNetLinter/Core/LinterAnalyzer.Complexity.cs:23-37`
```csharp
public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
{
    var paramCount = node.ParameterList.Parameters.Count;
    if (paramCount > _config.Metrics.MaxMethodParameterCount)
    {
        // Kein Check auf override/interface-implementierung!
        _violations.Add(...);
    }
```

### Fix

In `LinterAnalyzer.Complexity.cs`, `VisitMethodDeclaration`:

```csharp
var paramCount = node.ParameterList.Parameters.Count;
if (paramCount > _config.Metrics.MaxMethodParameterCount
    && !IsOverrideOrInterfaceImplementation(node))
{
    _violations.Add(...);
}
```

```csharp
private bool IsOverrideOrInterfaceImplementation(MethodDeclarationSyntax node)
{
    // 1. override-Modifier vorhanden
    if (node.Modifiers.Any(SyntaxKind.OverrideKeyword))
        return true;

    // 2. Explizite Interface-Implementierung (void IFoo.Bar(...))
    if (node.ExplicitInterfaceSpecifier != null)
        return true;

    // 3. Semantisch: Methode implementiert ein Interface-Member
    var symbol = _semanticModel.GetDeclaredSymbol(node);
    if (symbol == null) return false;

    return symbol.ExplicitInterfaceImplementations.Length > 0
        || IsImplicitInterfaceImplementation(symbol);
}

private static bool IsImplicitInterfaceImplementation(IMethodSymbol symbol)
{
    var type = symbol.ContainingType;
    foreach (var iface in type.AllInterfaces)
    {
        foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
        {
            var impl = type.FindImplementationForInterfaceMember(member);
            if (impl != null && SymbolEqualityComparer.Default.Equals(impl, symbol))
                return true;
        }
    }
    return false;
}
```

> **Hinweis:** `IsImplicitInterfaceImplementation` kann bei sehr breiten Interfaces (viele Members) etwas teurer sein. Wenn Performance-Tests Regression zeigen, nur syntaktische Checks (1+2) einsetzen — die decken die häufigsten Fälle ab.

### Tests

**Neue Testklasse:** `src/AiNetLinter.Tests/Core/MethodParameterCountOverrideTests.cs`

```
Szenario A: override-Methode mit 5 Parametern (MaxMethodParameterCount=4)
  → Kein Verstoß erwartet

Szenario B: Explizite Interface-Implementierung mit 6 Parametern
  → Kein Verstoß erwartet

Szenario C: Implizite Interface-Implementierung (ILogger.Log Pattern)
  → Kein Verstoß erwartet

Szenario D: Normale Methode mit 5 Parametern (kein override, kein Interface)
  → Verstoß erwartet (Baseline-Sicherung)

Szenario E: virtual-Methode mit 5 Parametern
  → Verstoß erwartet (virtual ist die Basis, nicht die Implementierung)
```

---

## Fix 4 — `EnforceNoVariableShadowing`: `_` Discard-Identifier

### Problem

`_` ist das standardisierte C#-Discard-Symbol. Es ist absichtlich ohne Bedeutung und sollte nie umbenannt werden müssen. AiNetLinter meldet einen Shadowing-Verstoß, wenn `_` in einem Lambda einen äußeren Parameter namens `_` (z.B. `IServiceProvider _`) verdeckt.

**Konkretes Beispiel (San.smart.Planner.Platform):**

```csharp
// AddScoped nimmt IServiceProvider als Lambda-Parameter, hier als _ (discard)
ctx.Services.AddScoped<HandlerFormPresenter>(_ =>
    new HandlerFormPresenter((_, _) => Task.FromResult<...>(null)));
//                           ^^ inneres _ shadowed äußeres _ → Verstoß
```

Linter-Meldung: `'_' verdeckt 'System.IServiceProvider _'`

### Root Cause (Code)

`src/AiNetLinter/Core/LinterAnalyzer.Scope.cs:50-70`
```csharp
private void CheckVariableShadowing(SyntaxToken identifier, SyntaxNode node)
{
    if (!_config.Global.EnforceNoVariableShadowing) return;
    var name = identifier.Text;
    if (string.IsNullOrEmpty(name)) return;
    // Kein Check auf "_"!
    ...
```

### Fix

```csharp
private void CheckVariableShadowing(SyntaxToken identifier, SyntaxNode node)
{
    if (!_config.Global.EnforceNoVariableShadowing) return;
    var name = identifier.Text;
    if (string.IsNullOrEmpty(name)) return;
    if (name == "_") return;  // NEU — C# Discard-Symbol, nie umbenennen
    ...
```

### Tests

**In bestehendem oder neuem `VariableShadowingTests.cs`:**

```
Szenario A: Äußeres Lambda-Parameter _ wird von innerem _ verdeckt
  → Kein Verstoß erwartet

Szenario B: Äußerer Parameter 'value' wird von innerer Variable 'value' verdeckt
  → Verstoß erwartet (Baseline-Sicherung)

Szenario C: Äußeres _ und inneres _ in verschachtelten Lambdas
  → Kein Verstoß erwartet
```

---

## Fix 5 — `EnforceNoSilentCatch`: Neue Config-Option `AllowedSilentCatchExceptionTypes`

### Problem

Blazor JS-Interop muss `JSDisconnectedException` und `ObjectDisposedException` in Dispose-Methoden lautlos schlucken. Das ist das von Microsoft empfohlene Muster — analog zu `OperationCanceledException` bei Shutdown (für das es bereits `AllowCancellationShutdownCatch` gibt).

Das Problem: Bei `catch (JSDisconnectedException) { }` gibt es keine Exception-Variable, die man zu `ignored` umbenennen könnte. Der einzige Ausweg ist ein `// ainetlinter-disable`-Kommentar pro Zeile — bei einem Projekt wie San.smart.Planner.Platform mit ~16 solcher Catches sehr laut.

**Konkretes Beispiel (San.smart.Planner.Platform):**
```csharp
// SchedulerJsInterop.cs — Standard-Blazor-Pattern, ~16x im Projekt
catch (JSDisconnectedException) { }
catch (ObjectDisposedException) { }
```

### Neue Config-Option

In `GlobalConfig` (`src/AiNetLinter/Configuration/LinterConfig.cs`):
```csharp
/// <summary>
/// Exception-Typen, die lautlos abgefangen werden dürfen (leerer catch-Block ohne Variable).
/// Analogon zu AllowCancellationShutdownCatch für projektspezifische Exception-Typen.
/// Nur der einfache Typname, kein Namespace (z.B. "JSDisconnectedException").
/// </summary>
public IReadOnlyList<string> AllowedSilentCatchExceptionTypes { get; init; } = [];
```

In `GlobalConfigOverride` (`src/AiNetLinter/Configuration/LinterConfigOverrides.cs`):
```csharp
public IReadOnlyList<string>? AllowedSilentCatchExceptionTypes { get; init; }
```

In `ApplyCore1b`:
```csharp
AllowedSilentCatchExceptionTypes = @override.AllowedSilentCatchExceptionTypes
    ?? AllowedSilentCatchExceptionTypes,
```

### Fix in Analyzer

`src/AiNetLinter/Core/LinterAnalyzer.ControlFlow.cs`, `ShouldSkipSilentCatch`:
```csharp
private bool ShouldSkipSilentCatch(CatchClauseSyntax node)
{
    if (!_config.Global.EnforceNoSilentCatch) return true;
    if (_isTestFile) return true;
    if (!IsSwallowed(node)) return true;
    if (IsAllowedCancellationCatch(node)) return true;
    if (IsAllowedSilentCatchByConfig(node)) return true;  // NEU
    return IsExplicitlyIgnored(node);
}

private bool IsAllowedSilentCatchByConfig(CatchClauseSyntax node)
{
    var allowedTypes = _config.Global.AllowedSilentCatchExceptionTypes;
    if (allowedTypes == null || allowedTypes.Count == 0) return false;
    if (node.Declaration?.Type == null) return false;

    var typeInfo = _semanticModel.GetTypeInfo(node.Declaration.Type);
    var typeName = typeInfo.Type?.Name
        ?? node.Declaration.Type.ToString().Split('.').Last();

    return allowedTypes.Contains(typeName, StringComparer.Ordinal);
}
```

### Nutzung in `platform-default.rules.json`

```json
"Global": {
  "AllowedSilentCatchExceptionTypes": [
    "JSDisconnectedException",
    "JSException"
  ]
}
```

### Doku-Update

In `AiNetLinterRichtlinien.mdc` und `--readme`-Output: Option dokumentieren, Analogie zu `AllowCancellationShutdownCatch` hervorheben.

### Tests

**In bestehendem `ControlFlowResilienceTests.cs` oder neuem `SilentCatchAllowedTypesTests.cs`:**

```
Szenario A: catch (JSDisconnectedException) { } mit JSDisconnectedException in AllowedSilentCatchExceptionTypes
  → Kein Verstoß erwartet

Szenario B: catch (SomeOtherException) { } — nicht in der Liste
  → Verstoß erwartet

Szenario C: AllowedSilentCatchExceptionTypes leer (Default)
  → Verhalten wie bisher (kein Verstoß-Verzicht)

Szenario D: catch (JSDisconnectedException e) { } — mit Variable, leer
  → Verstoß erwartet (Variable nicht "ignored" — soll weiterhin auffallen)
```

---

## Fix 6 — `AllowOutParameters`: Erweiterung des TryPattern auf boolesche `Is*`-Methoden

### Problem

`AllowTryPatternOutParameters: true` erlaubt `out`-Parameter nur wenn:
1. Methodenname beginnt mit `Try`
2. Rückgabetyp ist `bool`

Das Pattern `IsZoomCommand(string? command, out string presetId)` (aus San.smart.Planner.Platform) — ein `bool`-rückgebender `Is*`-Check mit einem Out-Ergebnis — ist ein legitimes C#-Idiom und wird trotzdem gemeldet.

### Fix

`src/AiNetLinter/Core/LinterAnalyzer.State.cs`, `IsAllowedTryPatternOut`:

```csharp
private bool IsAllowedTryPatternOut(ParameterSyntax node)
{
    if (!_config.Global.AllowTryPatternOutParameters) return false;
    if (node.Parent?.Parent is not MethodDeclarationSyntax method) return false;

    var returnsBool = method.ReturnType is PredefinedTypeSyntax 
        { Keyword.RawKind: (int)SyntaxKind.BoolKeyword };
    if (!returnsBool) return false;

    // Try* (bestehendes Pattern)
    if (method.Identifier.Text.StartsWith("Try", StringComparison.Ordinal))
        return true;

    // NEU: Is* — bool-rückgebende Prüfmethode mit Out-Ergebnis (z.B. IsZoomCommand)
    if (method.Identifier.Text.StartsWith("Is", StringComparison.Ordinal))
        return true;

    return false;
}
```

> Diese Änderung ist bewusst konservativ: nur `bool`-rückgebende `Is*`-Methoden mit `out`-Parameter. `void`-Methoden mit mehreren `out`-Parametern (wie `GetVisTimelineInstantsFromInclusiveSqlDates`) bleiben gemeldet — dort ist ein Tuple die richtige Lösung.

### Tests

Im bestehenden Test für `AllowOutParameters`:
```
Szenario: IsZoomCommand(string? cmd, out string presetId) — AllowTryPatternOutParameters: true
  → Kein Verstoß erwartet
```

---

## Output-Verbesserung: Violations klarer kennzeichnen

### Vorschlag

Im Output-Format bei `MaxMethodParameterCount` klarstellen, **warum** die Methode zu viele Parameter hat, damit ein Agent im San.smart.Planner.Platform-Kontext sofort erkennt, ob eine Änderung möglich ist:

Aktuell:
```
InMemoryUserConfigService.cs:39 MaxMethodParameterCount | Die Methode 'LoadPageConfigsAsync' hat 5 Parameter
```

Besser (nach Fix 3 hat das sich erledigt — aber für andere Fälle):
```
DataTableBaseSqlRules.cs:42 MaxMethodParameterCount | Die Methode 'BuildQuery' hat 6 Parameter (erlaubt sind maximal 4).
-> Erstelle 'sealed record BuildQueryParameters(...)' mit den bisherigen Parametern als Properties.
```

Das ist bereits so. Die eigentliche Verbesserung: **Override-Methoden gar nicht erst melden** (Fix 3) statt den Hinweis anzupassen.

---

## Dokumentation (nur wo relevant)

### `--readme` / `AiNetLinterRichtlinien.mdc`

- **Fix 5**: `AllowedSilentCatchExceptionTypes` in der Config-Referenz ergänzen, direkt nach `AllowCancellationShutdownCatch`
- **Fix 3**: Hinweis ergänzen: "`MaxMethodParameterCount` gilt nicht für `override`- oder Interface-Implementierungen"

### `rules.json`-Schema (falls vorhanden)

`AllowedSilentCatchExceptionTypes` als neues Array-Property mit Beschreibung eintragen.

---

## Umsetzungsreihenfolge

```
Priorität 1 (größte Wirkung, klare Fixes):
  Fix 3 — MaxMethodParameterCount override  (~100 Violations weniger)
  Fix 4 — Discard _ Shadowing              (~6 Violations weniger, 1 Zeile)
  Fix 2 — Declaration-after-write          (Prereq für Fix 1, einfach)

Priorität 2 (Architekturänderung):
  Fix 1 — Cross-partial-class ReadonlyFields (~20 Violations weniger, komplexer)

Priorität 3 (neue Feature):
  Fix 5 — AllowedSilentCatchExceptionTypes  (Konfigurierbarkeit für Blazor-Projekte)
  Fix 6 — Is* TryPattern Erweiterung        (wenige Violations, kleine Änderung)
```

---

## Nicht im Plan (bewusste Entscheidungen)

### `BlazorRequireCssIsolation` — keine Regeländerung

Die Regel ist konzeptuell korrekt: Komponenten mit nativen HTML-Elementen und CSS-Klassen sollten CSS-Isolation nutzen. Die ~26 Violations in San.smart.Planner.Platform sind **echte Verstöße** — die Dateien verwenden komponentenspezifische CSS-Klassnamen, die in CSS-Isolationsdateien gehören.

Ausnahmen wie `App.razor` (Root-Shell) können per `@* ainetlinter-disable BlazorRequireCssIsolation *@` unterdrückt werden. Eine neue Exempt-Config-Option würde die Regeln verwässern.

### `MaxMethodParameterCount` — kein CancellationToken-Exemption

Die `override`-Exemption (Fix 3) deckt die meisten `CancellationToken`-Fälle bereits ab. Für nicht-override-Methoden mit `CancellationToken` ist ein Parameter-Record tatsächlich sinnvoll (der `CancellationToken` wäre eine Property). Keine Ausnahme notwendig.

### `EnforceNoSilentCatch` — keine automatische Erkennung von Kommentaren als "intentional"

Einen Kommentar im catch-Block als "absichtlich" zu interpretieren wäre zu fragil. Die bestehenden Escape-Hatches (`ignored`-Variable, `// ainetlinter-disable`) bleiben die einzigen. Fix 5 (`AllowedSilentCatchExceptionTypes`) löst den konkreten Fall sauber.
