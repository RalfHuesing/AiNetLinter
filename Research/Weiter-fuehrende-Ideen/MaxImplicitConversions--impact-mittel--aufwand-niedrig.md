# MaxImplicitConversions — Implizite Konversions-Operatoren

**Impact: mittel | Aufwand: niedrig | Haltbarkeit: mittelfristig**

---

## Problem

```csharp
public static implicit operator decimal(Money m) => m.Amount;   // ok
public static implicit operator string(Money m)  => m.ToString(); // ok
public static implicit operator bool(Money m)    => m.Amount > 0; // Violation (> 1)
```

Jede implizite Konversion ist ein **unsichtbarer Typwechsel** an der Call-Site — der Agent
sieht `string s = price` und muss wissen, dass `Money` diesen Operator deklariert. Bei
mehreren impliziten Konversionen auf denselben Zieltyp-Bereich wählt das Modell beim
Generieren neuer Zuweisungen die falsche Konversion oder tippt den Typ falsch.

Das Kern-Problem: Implizite Konversionen erscheinen im AST nicht explizit. Ein Agent der
`decimal tax = order.Price * 0.19m` generiert, muss wissen ob `order.Price` ein `Money`
ist das implizit nach `decimal` konvertiert — oder ob es bereits `decimal` ist. Mehrere
implizite Konversionen erhöhen die Ambiguität.

**Design-Signal:** Mehr als eine implizite Konversion auf einem Typ ist fast immer ein
Zeichen, dass der Typ zu viele Rollen spielen will. `Money` das gleichzeitig als `decimal`,
`string` und `bool` gelesen werden kann, ist semantisch überladen.

---

## Implementierung

### Roslyn-Implementierung

```csharp
var implicitConversions = typeDecl.Members
    .OfType<ConversionOperatorDeclarationSyntax>()
    .Where(c => c.ImplicitOrExplicitKeyword.IsKind(SyntaxKind.ImplicitKeyword))
    .Count();

if (implicitConversions > config.MaxImplicitConversions)
    Report(typeDecl, implicitConversions);
```

`ConversionOperatorDeclarationSyntax` mit `ImplicitKeyword` — das ist die gesamte Implementierung.
Kein Semantic-Model nötig, reine Syntax-Analyse genügt.

### rules.json-Config

```json
"MaxImplicitConversions": 1
```

**Grenzwert 1** ist die richtige Wahl:
- 0 wäre zu aggressiv — eine implizite Konversion (z.B. `Money` → `decimal` für die primitive
  Darstellung) ist idiomatisches C# für Value Objects
- 1 erlaubt genau dieses Muster
- 2+ ist fast nie gerechtfertigt

### Beispiele

```csharp
// OK: Ein impliziter Operator (primitive Darstellung)
public readonly struct Money
{
    public decimal Amount { get; }
    public static implicit operator decimal(Money m) => m.Amount;  // 1 → OK
}

// VIOLATION: Zwei implizite Operatoren
public readonly struct Money
{
    public decimal Amount { get; }
    public static implicit operator decimal(Money m) => m.Amount;  // 1
    public static implicit operator string(Money m)  => m.ToString();  // 2 → Violation
}

// FIX: Explicit-Operator für sekundäre Konversion
public static explicit operator string(Money m) => m.ToString();
// Oder: Methode statt Operator
public string ToDisplayString() => $"{Amount:C}";
```

---

## Praxis-Bewertung

| Dimension | Bewertung |
| :--- | :--- |
| Wartungsaufwand | Keiner |
| False-Positive-Risiko | Niedrig — mehrere implizite Konversionen sind selten und Signal |
| Adoptionsbarriere | Sehr niedrig |

**Empfehlung:** Implementieren, Priorität 2. Geringer Aufwand, klares Signal wenn verletzt.

---

## Haltbarkeit

Typ-Reasoning verbessert sich schnell bei neueren Modellen. `MaxImplicitConversions` wird
vermutlich früher irrelevant als andere Regeln. Als Code-Smell bleibt es aber dauerhaft
sinnvoll — implizite Konversionen sind auch für Menschen verwirrend.
