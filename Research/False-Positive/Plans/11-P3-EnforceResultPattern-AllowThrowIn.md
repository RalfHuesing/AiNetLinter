# Plan 11 — P3: EnforceResultPatternOverExceptions — Namespace-basierte Allow-Liste

**Priorität:** P3  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

Die Regel erlaubt `throw` nur in Konstruktoren oder Methoden mit Suffix `Guard`/`Validate` (`LinterAnalyzer.ControlFlow.cs:139–146`). Das ist zu restriktiv für legitime Throw-Szenarien:

```csharp
// Infrastruktur-Fehler — legitimerweise throw:
public sealed class SqlLoginAuthHandler
{
    public async Task<Result> AuthenticateAsync(...)
    {
        var connStr = _config.ConnectionString
            ?? throw new InvalidOperationException("Connection string missing");
        // ...
    }
}

// ASP.NET Middleware — throw ist Standard-Idiom:
app.Use(async (ctx, next) =>
{
    if (!ctx.User.Identity?.IsAuthenticated == true)
        throw new UnauthorizedAccessException();
    await next();
});
```

Beide sind legitime `throw`-Verwendungen — in Infrastruktur-Code und ASP.NET-Middleware ist das der erwartete Stil, nicht ein Design-Problem.

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterAnalyzer.ControlFlow.cs` | Zeile 98–115 — `CheckResultPatternViolation()` |
| `src/AiNetLinter/Core/LinterAnalyzer.ControlFlow.cs` | Zeile 139–146 — `IsThrowAllowed()` |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `GlobalConfig` |
| `rules.json` | `Global`-Sektion |

---

## Konfigurationsänderung

### `rules.json`:
```json
"Global": {
  "EnforceResultPatternOverExceptions": true,
  "ResultPatternAllowThrowInNamespaceSuffixes": [
    "Infrastructure",
    "Endpoints",
    "Middleware",
    "Program"
  ],
  "ResultPatternAllowCatchRethrow": true
}
```

- `ResultPatternAllowThrowInNamespaceSuffixes`: Namespaces deren Name **mit** einem dieser Segmente endet, dürfen `throw` verwenden
- `ResultPatternAllowCatchRethrow`: Bare `throw;` (Rethrow in Catch) ist immer erlaubt

---

## Implementierungsvorschlag

### `LinterConfig.cs` — `GlobalConfig` erweitern:

```csharp
public sealed record GlobalConfig
{
    // ...
    public bool EnforceResultPatternOverExceptions { get; init; } = false;

    /// <summary>
    /// Namespace-Suffixe für die throw erlaubt ist.
    /// Beispiel: ["Infrastructure", "Endpoints", "Middleware"]
    /// </summary>
    public IReadOnlyCollection<string> ResultPatternAllowThrowInNamespaceSuffixes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Bare "throw;" (Rethrow in Catch-Block) ist immer erlaubt wenn true.
    /// </summary>
    public bool ResultPatternAllowCatchRethrow { get; init; } = true;
}
```

### `LinterAnalyzer.ControlFlow.cs` — `CheckResultPatternViolation()` erweitern (Zeile 98):

```csharp
private void CheckResultPatternViolation(SyntaxNode node)
{
    if (!_config.Global.EnforceResultPatternOverExceptions) return;

    if (IsAllowedFatalExceptionThrow(node)) return;
    if (IsInAllowedNamespace()) return;  // NEU
    if (IsAllowedCatchRethrow(node)) return;  // NEU
    if (!IsThrowAllowed(node)) // bestehende Guard/Validate-Prüfung
    {
        _violations.Add(new RuleViolation { /* ... wie bisher ... */ });
    }
}

private bool IsInAllowedNamespace()
{
    var allowed = _config.Global.ResultPatternAllowThrowInNamespaceSuffixes;
    if (allowed == null || allowed.Count == 0) return false;
    if (string.IsNullOrEmpty(_currentNamespace)) return false;

    foreach (var suffix in allowed)
    {
        // Segment-basierter Match: "MyApp.Infrastructure" endet mit ".Infrastructure"
        // oder ist exakt "Infrastructure"
        if (_currentNamespace.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase)
            || _currentNamespace.Equals(suffix, StringComparison.OrdinalIgnoreCase)
            || _currentNamespace.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }
    return false;
}

private bool IsAllowedCatchRethrow(SyntaxNode node)
{
    if (!_config.Global.ResultPatternAllowCatchRethrow) return false;

    // Bare "throw;" ohne Expression = Rethrow
    if (node is ThrowStatementSyntax throwStmt && throwStmt.Expression == null)
    {
        // Prüfe ob wir in einem Catch-Block sind
        return node.Ancestors().OfType<CatchClauseSyntax>().Any();
    }
    return false;
}
```

**Hinweis:** `_currentNamespace` ist bereits ein Feld in `LinterAnalyzer` (wird in `VisitNamespaceDeclaration` gesetzt).

---

## Ergänzung: `ResultPatternMode` (optional, für spätere Erweiterung)

Wenn gewünscht, kann ein `Mode`-Feld die Stärke der Regel steuern:

```json
"Global": {
  "ResultPatternMode": "suggest"  // "off" | "suggest" | "enforce"
}
```

- `suggest`: Verstoß wird als Hinweis gemeldet (kein Fehler), Guidance erklärt das Result-Pattern
- `enforce`: Verstoß ist Fehler (heutiges Verhalten wenn aktiv)

---

## Tests

### AllowThrowInNamespaceSuffixes:
```csharp
// Namespace: MyApp.Infrastructure
// ResultPatternAllowThrowInNamespaceSuffixes: ["Infrastructure"]
namespace MyApp.Infrastructure
{
    public sealed class SqlHandler
    {
        public Task Run() {
            throw new InvalidOperationException("no connection");  // → kein Verstoß
        }
    }
}

// Namespace: MyApp.Domain (NICHT in Allow-Liste)
namespace MyApp.Domain
{
    public sealed class OrderService
    {
        public Result Process() {
            throw new BusinessException("invalid");  // → Verstoß
        }
    }
}
```

### AllowCatchRethrow:
```csharp
try { ... }
catch (Exception ex)
{
    _logger.LogError(ex, "Error");
    throw;  // bare rethrow → kein Verstoß (AllowCatchRethrow: true)
}
```

### Weiterhin erlaubt: Konstruktor + Guard/Validate:
```csharp
public sealed class Service(IRepo repo)
{
    // Konstruktor:
    public Service() { throw new ArgumentNullException("repo"); }  // kein Verstoß
}

public void ValidateInput(Input i) { throw new ValidationException(); }  // kein Verstoß (Suffix)
public void GuardAccess() { throw new UnauthorizedAccessException(); }   // kein Verstoß (Suffix)
```

### Edge Cases:
- `ResultPatternAllowThrowInNamespaceSuffixes` leer → nur Guard/Validate-Exemption
- File-Scoped-Namespace → `_currentNamespace` korrekt gesetzt (über `VisitFileScopedNamespaceDeclaration`)
- Namespace-Verschachtelung → innerster Namespace gilt
- `throw;` in lambda (nicht in Catch) → kein Rethrow → Verstoß

---

## README-Anforderungen

Im README zu `EnforceResultPatternOverExceptions`:
- Klarstellen: Regel ist standardmäßig **deaktiviert**
- `ResultPatternAllowThrowInNamespaceSuffixes` erklären mit Beispiel
- `ResultPatternAllowCatchRethrow` erklären
- Empfohlene Guidance für Agenten: fachliche Fehler → Result<T>; Infrastruktur/Unerwartetes → throw + log
- Hinweis: `AllowedExceptions`-Liste (bestehend) bleibt für Typ-basierte Exemptions
