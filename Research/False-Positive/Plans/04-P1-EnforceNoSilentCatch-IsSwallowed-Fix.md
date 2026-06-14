# Plan 04 — P1: EnforceNoSilentCatch — IsSwallowed-Logik und AllowCancellationShutdownCatch

**Priorität:** P1  
**Regeln:** [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc), [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

---

## Problem

### Problem A — IsSwallowed zu restriktiv

Die aktuelle `IsSwallowed`-Implementierung (`LinterAnalyzer.ControlFlow.cs:69–76`) betrachtet einen Catch-Block als „still verschluckt", wenn er **weder** einen `throw` **noch** eine Methoden-Aufruf-Invocation enthält.

Das führt zu False Positives bei Catch-Blöcken, die den Fehler durch **Zuweisungen** oder **Return-Statements** behandeln:

```csharp
// Behandlung durch Zuweisung + Return → wird fälschlich als "silent" gewertet
catch (FormatException ex)
{
    _lastError = ex.Message;  // Zuweisung — keine Invocation
    return false;             // Return — kein throw
}

// TryParse-Muster
catch (ArgumentException)
{
    return null;  // Return ohne Invocation → fälschlich silent
}
```

### Problem B — AllowCancellationShutdownCatch erfordert `when`-Filter

`IsAllowedCancellationCatch` (`LinterAnalyzer.ControlFlow.cs:46–59`) gibt nur `true` zurück wenn `node.Filter != null`. Das bedeutet: ein **leerer** `catch (OperationCanceledException)` wird **trotz** `AllowCancellationShutdownCatch: true` als Verstoß gemeldet — außer wenn ein `when (...)`-Klausel vorhanden ist.

```csharp
// AllowCancellationShutdownCatch: true — TROTZDEM Verstoß (kein Filter):
catch (OperationCanceledException)
{
    // bewusst leer beim Shutdown
}
```

---

## Betroffene Dateien

| Datei | Relevante Stelle |
|-------|-----------------|
| `src/AiNetLinter/Core/LinterAnalyzer.ControlFlow.cs` | Zeile 69–76 — `IsSwallowed()` |
| `src/AiNetLinter/Core/LinterAnalyzer.ControlFlow.cs` | Zeile 46–59 — `IsAllowedCancellationCatch()` |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `GlobalConfig` — keine neuen Felder nötig (nur Bugfix) |

---

## Implementierungsvorschlag

### Fix A — `IsSwallowed()` erweitern (Zeile 69):

```csharp
private static bool IsSwallowed(CatchClauseSyntax node)
{
    if (node.Block.Statements.Count == 0) return true;

    var hasThrow = node.Block.DescendantNodes().OfType<ThrowStatementSyntax>().Any();
    if (hasThrow) return false;

    var hasInvoke = node.Block.DescendantNodes().OfType<InvocationExpressionSyntax>().Any();
    if (hasInvoke) return false;

    // NEU: Return-Statement ohne throw/invoke ist bewusste Behandlung (TryParse-Pattern)
    var hasReturn = node.Block.Statements.OfType<ReturnStatementSyntax>().Any();
    if (hasReturn) return false;

    // NEU: Zuweisung an ein Member/Field ist bewusste Fehlerbehandlung
    var hasAssignment = node.Block.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any();
    if (hasAssignment) return false;

    return true;
}
```

**Begründung:**
- `return false/null` in einem Catch = TryParse-Muster, explizite Rückgabe
- Zuweisung = Fehler wird in State gespeichert (z. B. `_lastError`, `_isAvailable = false`)
- Beide Muster sind **bewusste Behandlung** und kein Silent Swallowing

### Fix B — `IsAllowedCancellationCatch()` korrigieren (Zeile 46):

```csharp
private bool IsAllowedCancellationCatch(CatchClauseSyntax node)
{
    if (!_config.Global.AllowCancellationShutdownCatch) return false;
    if (node.Declaration?.Type == null) return false;

    var typeInfo = _semanticModel.GetTypeInfo(node.Declaration.Type);
    var typeName = typeInfo.Type?.ToDisplayString();

    // FIX: Erlaubt wenn es eine Cancellation-Exception ist —
    // der when-Filter ist OPTIONAL (nicht mehr erzwungen)
    return IsCancellationExceptionName(typeName)
        || IsCancellationExceptionName(node.Declaration.Type.ToString());
}
```

**Begründung:** Der `when`-Filter war nie dokumentiert als Pflicht; die Intention von `AllowCancellationShutdownCatch` ist, stille Cancellation-Exception-Catches zu erlauben. Der Filter ist optional.

---

## Optionale neue Config-Option (falls gewünscht)

Falls man das TryParse-Muster noch weiter einschränken möchte:

```json
"Global": {
  "AllowReturnInSilentCatch": true
}
```

Default: `true` (das neue Verhalten ist der neue Default). So kann man bei Bedarf zum alten strengen Verhalten zurück.

---

## Tests

**Datei:** `src/AiNetLinter.Tests/Core/ControlFlowResilienceTests.cs` (bestehend — erweitern)

### Fix A — TryParse-Muster (darf NICHT mehr feuern):
```csharp
// Variante 1: Return ohne Invocation
catch (FormatException)
{
    return null;
}

// Variante 2: Zuweisung + Return
catch (ArgumentException ex)
{
    _lastError = ex.Message;
    return false;
}

// Variante 3: Nur Zuweisung (kein Return, kein Throw)
catch (FileNotFoundException)
{
    _isAvailable = false;
}
```

### Fix A — True Positive (soll weiterhin feuern):
```csharp
// Komplett leer → Verstoß
catch (Exception)
{
}

// Nur Variable-Deklaration → Verstoß
catch (Exception ex)
{
    var _ = ex;  // nutzlos
}
```

### Fix B — AllowCancellationShutdownCatch ohne `when` (darf NICHT mehr feuern):
```csharp
// AllowCancellationShutdownCatch: true
catch (OperationCanceledException)
{
    // intentional shutdown — kein when-Filter notwendig
}

catch (TaskCanceledException)
{
    // intentional
}
```

### Fix B — True Positive: Andere Exception + leer (soll weiterhin feuern):
```csharp
// AllowCancellationShutdownCatch: true, aber ANDERE Exception
catch (IOException)
{
    // leer → Verstoß
}
```

### Edge Cases:
- `catch (Exception ignored)` → explizit benannte Variable → bereits ausgenommen (bleibt)
- `catch` ohne Typangabe (bare catch) + leer → Verstoß (bleibt)
- Verschachtelte Aufrufe im Return: `return ComputeFallback(ex)` → hasInvoke = true → nicht swallowed (korrekt)
- `when`-Filter weiterhin optional aber erlaubt

---

## README-Anforderungen

Im README-Abschnitt zu `EnforceNoSilentCatch`:
- Erklären wann ein Catch als „still" gilt: leerer Block oder Block ohne throw/invoke/return/assignment
- `AllowCancellationShutdownCatch` klar dokumentieren: erlaubt stille Cancellation-Exception-Catches ohne `when`-Pflicht
- Suppression-Pattern dokumentieren: `catch (Exception ignored)` oder `// ainetlinter-disable EnforceNoSilentCatch`
- Klarstellung: Zuweisung an Field/Property = erlaubte Fehlerbehandlung
