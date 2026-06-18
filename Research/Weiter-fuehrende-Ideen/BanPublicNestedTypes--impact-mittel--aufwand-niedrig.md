# BanPublicNestedTypes — Verbot öffentlicher verschachtelter Typen

**Impact: mittel | Aufwand: niedrig | Haltbarkeit: dauerhaft**

---

## Problem

Verschachtelte Typen sind für Agent-Harness-Navigation unsichtbar:

```csharp
// PaymentProcessor.cs
public class PaymentProcessor
{
    public enum PaymentStatus { Pending, Processing, Completed, Failed }  // nested!

    public PaymentStatus Process(Payment payment) { ... }
}
```

Wenn ein Agent `grep -n "PaymentStatus"` ausführt, findet er die Datei `PaymentProcessor.cs`.
Er muss sie aber vollständig lesen, um `PaymentStatus` zu finden — Context-Overhead,
den kleine Dateien verhindern sollen. Schlimmer: der Agent weiß nicht a priori, dass
`PaymentStatus` nested ist, bis er die gesamte Klasse gelesen hat.

**Weitere Probleme:**
- Fully-qualified Name: `PaymentProcessor.PaymentStatus` statt `PaymentStatus`. LLMs neigen
  dazu, den kurzen Namen zu verwenden und damit Compile-Fehler zu generieren.
- Refactoring-Blockierung: Nested Types verhindern das Verschieben der Klasse in einen anderen
  Namespace ohne den nested Type mitzuziehen.
- Dokumentations-Tools: XML-Doc, IntelliSense und Analyzers behandeln nested Types teilweise
  unterschiedlich.

---

## Abgrenzung: Nur öffentlich/intern sichtbare Typen

Ein **Totalverbot** wäre zu aggressiv. Private nested Types (Builder-Pattern intern,
State-Machine-Hilfsklassen die nur intern existieren) können bleiben:

```csharp
// OK: private nested Type (nur intern sichtbar)
public class OrderProcessor
{
    private class ProcessingContext { ... }  // → kein Grep-Target von außen
}

// VIOLATION: public nested Type
public class PaymentProcessor
{
    public enum PaymentStatus { ... }  // → sollte eigene Datei sein
    public class PaymentResult { ... }  // → sollte eigene Datei sein
}

// VIOLATION: internal nested Type (über Assembly hinweg sichtbar)
public class OrderService
{
    internal class OrderCache { ... }  // → sollte eigene Datei sein
}
```

---

## Implementierung

### Roslyn-Implementierung

```csharp
var nestedTypes = typeDecl.Members
    .OfType<TypeDeclarationSyntax>()
    .Where(nested =>
        nested.Modifiers.Any(SyntaxKind.PublicKeyword) ||
        nested.Modifiers.Any(SyntaxKind.InternalKeyword));
        // private bleibt erlaubt

foreach (var nested in nestedTypes)
    Report(nested, typeDecl.Identifier.Text);
```

### rules.json-Config

```json
"BanPublicNestedTypes": true,
"BanPublicNestedTypesAllowPrivate": true
```

Kein Limit nötig — es ist ein Boolean-Check: öffentliche/interne nested Types sind verboten.

### Beispiele

```csharp
// VIOLATION: Nested Enum (häufigster Fall)
public class OrderProcessor
{
    public enum ProcessingMode { Fast, Reliable }
}

// FIX: Eigene Datei
// ProcessingMode.cs
public enum ProcessingMode { Fast, Reliable }


// VIOLATION: Nested Result-Klasse
public class PaymentService
{
    public class PaymentResult
    {
        public bool Success { get; }
        public string? ErrorMessage { get; }
    }
}

// FIX: Eigene Datei oder — noch besser — ein Record
// PaymentResult.cs
public record PaymentResult(bool Success, string? ErrorMessage = null);
```

---

## Praxis-Bewertung

| Dimension | Bewertung |
| :--- | :--- |
| Wartungsaufwand | Keiner |
| False-Positive-Risiko | Mittel — nested Enums sind weit verbreitet in bestehendem Code |
| Adoptionsbarriere | Niedrig für Greenfield, Mittel für Brownfield |

**Empfehlung:** Implementieren mit "nur public/internal, private OK". In Brownfield initial
als Warnung einführen — nested Enums sind sehr verbreitet und erzeugen initial viele Findings.

---

## Haltbarkeit

Datei-Navigation über `grep` und `list_directory` bleibt eine fundamentale Eigenschaft von
Agent-Harness-Architekturen. Dauerhaft relevant.
