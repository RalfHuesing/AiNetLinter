# Plan 02 — P0: EnforceNoMagicValues — Konfigurierbare Profile und Ignore-Kontexte

**Priorität:** P0 — Kritisch (74 % aller Strict-Verstöße im realen Projekt)  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

Die aktuelle Implementierung meldet **jedes** String- und Numeric-Literal in einem Methoden-Body als magic value, außer `""`, `0`, `1`, `-1`. Sie unterscheidet nicht nach Semantik:

- Routen-Strings (`"/data/{id}"`) → kein echter Magic Value
- Serilog-Templates (`"Sitzungen: {Count}"`) → kein echter Magic Value
- JSON-Keys (`"sqlFile"`, `"columns"`) → kein echter Magic Value
- Format-Strings (`"yyyy-MM-dd"`, `"N2"`) → kein echter Magic Value
- Protokoll-Felder (`"grant_type"`, `"password"`) → kein echter Magic Value

Im realen Consumer-Projekt: 1.033 von 1.398 Strict-Verstößen = **74 % Noise**.

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterAnalyzer.MagicValues.cs` | Zeile 39–131 — komplette Datei |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `GlobalConfig` und `GlobalConfigOverride` |
| `rules.json` | `Global`-Sektion |

---

## Architektur: neues `MagicValuesConfig`-Record

Statt alle Optionen flach in `GlobalConfig` zu packen, wird ein dediziertes Record `MagicValuesConfig` eingeführt, das als optionaler Unterabschnitt in `rules.json` konfiguriert wird.

### Neues Record in `LinterConfig.cs`:

```csharp
/// <summary>
/// Fein-granulare Konfiguration der Magic-Value-Erkennung.
/// </summary>
public sealed record MagicValuesConfig
{
    /// <summary>
    /// Steuert welche Literale als magic gelten.
    /// "all"              — alle String+Numeric (bisheriges Verhalten)
    /// "numeric-only"     — nur Numeric-Literale (außer 0/1/-1)
    /// "numeric-and-short-string" — Numeric + Strings bis MinStringLength Zeichen
    /// </summary>
    public string Mode { get; init; } = "all";

    /// <summary>
    /// Mindestlänge eines Strings damit er als magic gilt (bei Mode numeric-and-short-string).
    /// Default 0 = alle Strings (heutiges Verhalten).
    /// </summary>
    public int MinStringLength { get; init; } = 0;

    /// <summary>
    /// Regex-Muster für String-Literale, die grundsätzlich ignoriert werden.
    /// Beispiel: ["^/[\\w/{}\\-]*$"] ignoriert Routen wie "/api/{id}"
    /// </summary>
    public IReadOnlyCollection<string> IgnoreStringPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Erweiterter Satz ignorierter Numeric-Werte (zusätzlich zu 0/1/-1).
    /// Beispiel: [2, 100, 1000] für bekannte Timeout/Batch-Größen.
    /// </summary>
    public IReadOnlyCollection<double> IgnoreNumericValues { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Wenn true: String-Literale als direkte Argumente von Methoden deren Name mit einem der
    /// Einträge in IgnoreInvocationNames beginnt, werden ignoriert.
    /// </summary>
    public IReadOnlyCollection<string> IgnoreInvocationPrefixes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Wenn true: Literale innerhalb von Collection/Dictionary-Initialisierern werden ignoriert.
    /// Für Metadata-over-Code-Muster (JSON-Keys, OAuth-Felder).
    /// </summary>
    public bool IgnoreCollectionInitializers { get; init; } = false;
}
```

### `LinterConfig` erweitern:

```csharp
public sealed record LinterConfig
{
    // ...
    public MagicValuesConfig MagicValues { get; init; } = new();
}
```

Der bestehende bool-Schalter `Global.EnforceNoMagicValues` bleibt als Haupt-Ein/Aus-Schalter.
`MagicValues` steuert das **Wie**, wenn die Regel aktiv ist.

---

## Implementierungsvorschlag

### `LinterAnalyzer.MagicValues.cs` — neue Filtermethoden:

```csharp
private bool IsMagicValue(LiteralExpressionSyntax node)
{
    if (!IsTargetLiteral(node)) return false;
    if (IsExceptionValue(node)) return false;
    if (IsConstDeclaration(node)) return false;
    if (IsAttributeArgument(node)) return false;
    if (!IsInsideBody(node)) return false;

    // Neue Checks:
    if (IsIgnoredByMode(node)) return false;
    if (IsIgnoredByStringPattern(node)) return false;
    if (IsIgnoredByInvocationContext(node)) return false;
    if (IsIgnoredByCollectionInitializer(node)) return false;

    return true;
}

private bool IsIgnoredByMode(LiteralExpressionSyntax node)
{
    var mode = _config.MagicValues.Mode;
    if (mode == "numeric-only")
    {
        // Im numeric-only Mode: Strings sind nie magic
        return node.IsKind(SyntaxKind.StringLiteralExpression);
    }
    if (mode == "numeric-and-short-string")
    {
        if (node.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var text = node.Token.ValueText;
            var minLen = _config.MagicValues.MinStringLength;
            return text.Length < minLen;
        }
    }
    return false;
}

private bool IsIgnoredByStringPattern(LiteralExpressionSyntax node)
{
    if (!node.IsKind(SyntaxKind.StringLiteralExpression)) return false;
    var patterns = _config.MagicValues.IgnoreStringPatterns;
    if (patterns == null || patterns.Count == 0) return false;

    var value = node.Token.ValueText;
    foreach (var pattern in patterns)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(value, pattern))
            return true;
    }
    return false;
}

private bool IsIgnoredByInvocationContext(LiteralExpressionSyntax node)
{
    var prefixes = _config.MagicValues.IgnoreInvocationPrefixes;
    if (prefixes == null || prefixes.Count == 0) return false;

    // Prüfe ob das Literal direkt als Argument einer bestimmten Methode übergeben wird
    if (node.Parent is not ArgumentSyntax arg) return false;
    if (arg.Parent is not ArgumentListSyntax argList) return false;
    if (argList.Parent is not InvocationExpressionSyntax invocation) return false;

    var methodName = GetInvocationMethodName(invocation);
    if (methodName == null) return false;

    foreach (var prefix in prefixes)
    {
        if (methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}

private static string? GetInvocationMethodName(InvocationExpressionSyntax invocation)
{
    return invocation.Expression switch
    {
        MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
        IdentifierNameSyntax id => id.Identifier.Text,
        _ => null
    };
}

private bool IsIgnoredByCollectionInitializer(LiteralExpressionSyntax node)
{
    if (!_config.MagicValues.IgnoreCollectionInitializers) return false;
    return node.Ancestors().OfType<InitializerExpressionSyntax>().Any();
}
```

Auch `IsExceptionNumeric` erweitern für `IgnoreNumericValues`:

```csharp
private bool IsExceptionNumeric(object? value)
{
    // ... bestehende 0/1/-1 Prüfung ...

    // Neue: konfigurierbare Werte-Whitelist
    var extras = _config.MagicValues.IgnoreNumericValues;
    if (extras != null && extras.Count > 0)
    {
        var d = Convert.ToDouble(value);
        if (extras.Contains(d)) return true;
    }
    return false;
}
```

---

## Empfohlene `rules.json`-Defaults für verschiedene Profile

### Default (heute — kein Breaking Change):
```json
"Global": { "EnforceNoMagicValues": false }
// MagicValues-Sektion fehlt → alle Defaults greifen
```

### Pragmatic-Profile (für platform-default mit aktivierter Regel):
```json
"Global": { "EnforceNoMagicValues": true },
"MagicValues": {
  "Mode": "numeric-only"
}
```

### Metadata-Aware-Profile (für API-/Metadaten-lastige Projekte):
```json
"Global": { "EnforceNoMagicValues": true },
"MagicValues": {
  "Mode": "numeric-only",
  "IgnoreStringPatterns": [
    "^/[\\w/{}\\-]*$",
    "^[a-z][a-zA-Z0-9_]*$"
  ],
  "IgnoreInvocationPrefixes": [
    "Log", "MapGet", "MapPost", "MapPut", "MapDelete", "MapGroup",
    "GetSection", "GetValue", "GetRequiredSection",
    "TypedResults.Problem", "Results.Problem"
  ],
  "IgnoreCollectionInitializers": true
}
```

---

## Tests

**Datei:** `src/AiNetLinter.Tests/MagicValuesTests.cs` (neu)

### Mode: numeric-only
```csharp
// True Positive (soll feuern):
int timeout = 30000;  // Numeric magic

// False Positive (darf NICHT feuern):
string key = "grant_type";  // String → ignoriert bei numeric-only
string route = "/api/users"; // String → ignoriert
```

### IgnoreStringPatterns
```csharp
// Konfiguriert: IgnoreStringPatterns: ["^/[\\w/{}\\-]*$"]
// False Positive (darf NICHT feuern):
app.MapGet("/data/{id}", ...);
app.MapPost("/api/users", ...);

// True Positive (soll feuern — Muster trifft nicht zu):
string label = "Bitte Wert eingeben";  // Länger, kein Routen-Pattern
```

### IgnoreInvocationPrefixes
```csharp
// Konfiguriert: IgnoreInvocationPrefixes: ["Log", "MapGet"]
// False Positive (darf NICHT feuern):
logger.LogInformation("Prozess gestartet: {Id}", id);
app.MapGet("/health", () => "ok");

// True Positive (soll feuern — nicht in Prefix-Liste):
var result = Process("wichtiger-schluessel");
```

### IgnoreCollectionInitializers
```csharp
// Konfiguriert: IgnoreCollectionInitializers: true
// False Positive (darf NICHT feuern):
var form = new Dictionary<string, string>
{
    ["grant_type"] = "password",
    ["client_id"] = "myapp"
};

// True Positive (soll feuern — kein Initializer):
var key = "grant_type";
```

### IgnoreNumericValues
```csharp
// Konfiguriert: IgnoreNumericValues: [404, 500]
// False Positive (darf NICHT feuern):
if (response.StatusCode == 404) return NotFound();

// True Positive (soll feuern — nicht in Liste):
var timeout = 30000;
```

### Edge Cases:
- `MagicValues`-Sektion fehlt in JSON → alle Defaults aktiv, kein Fehler
- `IgnoreStringPatterns` mit ungültigem Regex → Fehler beim Laden loggen, Pattern ignorieren (kein Crash)
- Leere `IgnoreStringPatterns`-Liste → kein Filter aktiv
- `Mode: "all"` → bisheriges Verhalten

---

## README-Anforderungen

Neuer Abschnitt `MagicValues-Konfiguration`:
- Alle `Mode`-Werte beschreiben
- Alle Sub-Optionen mit Typen und Defaults
- 3 vorgefertigte Profile als JSON-Snippets (default, pragmatic, metadata-aware)
- Hinweis: Bool-Schalter `EnforceNoMagicValues` ist weiterhin der Haupt-Switch

---

## Architektur-Hinweise

- `MagicValuesConfig` als neues Record in `LinterConfig.cs` (kein neues Namespace erforderlich)
- `MagicValuesConfig` muss in `ProjectOverrideEntry` ergänzt werden wenn Projekt-spezifische Überschreibung gewünscht
- Regex-Kompilierung: Patterns bei erster Verwendung zu `Regex`-Objekten vorkompilieren und cachen (z. B. als `Lazy<Regex[]>` in einem Hilfsfeld) — wichtig für Performance bei großen Solutions
- `LinterConfigNormalizer` prüfen ob neues Feld normalisiert werden muss
