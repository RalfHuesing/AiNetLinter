# MaxChainedCallDepth — LINQ/Fluent-Kettenlänge (opt-in)

**Impact: mittel | Aufwand: niedrig | Haltbarkeit: kurzfristig (1–2 Jahre)**
**Status: opt-in (default disabled) — Konflikt mit idiomatischem LINQ**

---

## Problem

```csharp
// Kettenlänge 6 — problematisch für LLMs:
var result = orders
    .Where(o => o.Status == Status.Open)
    .Select(o => new { o.CustomerId, Lines = o.Lines })
    .GroupBy(x => x.CustomerId)
    .OrderBy(g => g.Key)
    .Take(20)
    .ToList();
```

Bei jeder Methode in der Kette ändert sich der **Typ unsichtbar**. LLMs tokenisieren die
Kette linear und verlieren ab einer gewissen Tiefe den Überblick, welcher Typ gerade "oben
liegt". Symptome:

- Halluzinierte Properties: `x.CustomerId` auf einem anonymen Typ der diese Property nicht hat
- Falscher Einsprungpunkt: `.Where()` nach `.GroupBy()` eingefügt (falscher Typ)
- Fehlerhafte Lambdas: `g => g.CustomerId` statt `g => g.Key`

**Erzwungene Alternative via Zwischenvariablen:**

```csharp
// Jede Variable ist ein Typ-Anker für den Attention-Mechanismus:
var openOrders    = orders.Where(o => o.Status == Status.Open);
var projected     = openOrders.Select(o => new { o.CustomerId, Lines = o.Lines });
var byCustomer    = projected.GroupBy(x => x.CustomerId);
var sorted        = byCustomer.OrderBy(g => g.Key);
var page          = sorted.Take(20).ToList();
```

---

## Warum opt-in (default disabled)?

LINQ-Ketten von 4–6 sind idiomatisches C# und in fast jedem .NET-Projekt vorhanden.
`items.Where(…).Select(…).OrderBy(…).ToList()` ist Länge 4 und würde bei Limit 3 flaggen —
das ist zu aggressiv für default-on.

**Empfohlene Nutzung:** Teams die stark auf AI-assistierten Code setzen und bereit sind,
LINQ-Ketten konsequent aufzubrechen, aktivieren diese Regel explizit.

---

## Implementierung

### Was wird gezählt?

Eine "Kette" beginnt bei einem Identifier oder Aufruf und endet beim letzten Member-Access:

```csharp
orders                           // Tiefe 0 (Basiswert)
    .Where(...)                  // Tiefe 1
    .Select(...)                 // Tiefe 2
    .GroupBy(...)                // Tiefe 3
    .OrderBy(...)                // Tiefe 4
    .Take(20)                    // Tiefe 5
    .ToList();                   // Tiefe 6 → bei Limit 6: OK, bei Limit 5: Violation
```

### Roslyn-Implementierung

```csharp
int GetChainDepth(ExpressionSyntax expr)
{
    int depth = 0;
    var current = expr;
    while (current is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma })
    {
        depth++;
        current = ma.Expression;
    }
    return depth;
}
```

### rules.json-Config

```json
"MaxChainedCallDepth": 6,
"MaxChainedCallDepthEnabled": false
```

`MaxChainedCallDepthEnabled: false` — explizites opt-in, nicht default-on.

Grenzwert **6** (nicht 5 — 5 ist zu aggressiv für idiomatisches LINQ wie `.Where().Select().OrderBy().Take().ToList()`).

### Ausnahmen

Builder-Patterns und Konfigurationsketten (`services.AddTransient<...>().AddScoped<...>()`)
sind orthogonal zu LINQ — sie haben keine Typ-Transformation pro Glied:

```json
"MaxChainedCallDepthExemptIfBuilderPattern": true
```

---

## Praxis-Bewertung

| Dimension | Bewertung |
| :--- | :--- |
| Wartungsaufwand | Keiner |
| False-Positive-Risiko | Hoch wenn default-on, niedrig wenn opt-in mit Limit 6 |
| Adoptionsbarriere | Mittel — erfordert Umgewöhnung bei LINQ-heavy Teams |

**Empfehlung:** Implementieren als opt-in. Nicht als Default einschalten.

---

## Haltbarkeit

Typ-Inferenz in Ketten ist ein Bereich wo Modelle schnell besser werden. In 1–2 Jahren
vermutlich deutlich weniger kritisch. Trotzdem: explizite Zwischenvariablen mit Typen
sind auch für Menschen besser lesbar.
