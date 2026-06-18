# MaxGenericTypeParameters — Generische Typ-Parameter-Anzahl

**Impact: mittel | Aufwand: niedrig | Haltbarkeit: kurzfristig (1–2 Jahre)**

---

## Problem

Generische Typen mit vielen Typ-Parametern erhöhen die Reasoning-Komplexität pro Parameter:

```csharp
// 4 Typ-Parameter → Violation
public class Repository<TEntity, TKey, TContext, TFilter>
    where TEntity : class
    where TKey : struct
    where TContext : DbContext
    where TFilter : IFilter<TEntity>
{ ... }
```

Jeder Typ-Parameter ist eine zusätzliche Variable im Reasoning des LLMs. Bei 4+ Parametern:
- Der Agent muss beim Instanziieren alle 4 Typen gleichzeitig korrekt zuordnen
- Type-Constraints (where-Klauseln) multiplizieren die Komplexität
- Refactoring-Aufrufe generieren oft falsche Reihenfolgen der Typ-Argumente

**BCL-Standard:** Das .NET BCL verwendet max. 2 generische Typ-Parameter für Kerntypen
(`Dictionary<TKey, TValue>`, `KeyValuePair<TKey, TValue>`, `Func<T, TResult>`).
Ab 3 Parametern (`Func<T1, T2, TResult>`) nimmt die Verständlichkeit messbar ab.

---

## Implementierung

### Roslyn-Implementierung

```csharp
// Für Typ-Deklarationen:
var typeParams = typeDecl.TypeParameterList?.Parameters.Count ?? 0;

// Für Methoden-Deklarationen:
var methodTypeParams = methodDecl.TypeParameterList?.Parameters.Count ?? 0;
```

Getrennte Limits für Typen und Methoden sind sinnvoll:
- Typen: max 3 (selten mehr nötig)
- Methoden: max 2 (generische Methoden mit 3+ Typ-Parametern sind ein seltenes Red Flag)

### rules.json-Config

```json
"MaxGenericTypeParameters": 3,
"MaxGenericMethodTypeParameters": 2
```

**Grenzwert 3 für Typen:** Im eigenen Code braucht man selten 3+ generische Parameter.
Ausnahmen sind meist Framework-Typen (die man eh nicht schreibt) oder spezifische
Result/Either-Typen (`Either<TLeft, TRight, TError>`).

### Beispiele

```csharp
// OK: 2 Parameter (Standard)
public class Pair<TFirst, TSecond> { }
public class Repository<TEntity, TId> { }

// OK: 3 Parameter (Grenzfall, noch erlaubt)
public class Either<TLeft, TRight, TError> { }

// VIOLATION: 4+ Parameter
public class Handler<TCommand, TResult, TValidator, TLogger> { }
// FIX: TLogger und TValidator als Konstruktor-Dependencies statt Typ-Parameter

// VIOLATION: Methode mit 3 Typ-Parametern
public TResult Convert<TSource, TIntermediate, TResult>(TSource input) { }
// FIX: In separate Methoden aufteilen oder TIntermediate intern ableiten
```

### Ausnahmen

```json
"MaxGenericTypeParametersExemptTypes": ["Func", "Action", "Tuple"]
```

BCL-Typen wie `Func<T1, T2, T3, TResult>` — aber die schreibt man selbst nicht, daher
keine praktische Relevanz für eigenen Code.

---

## Praxis-Bewertung

| Dimension | Bewertung |
| :--- | :--- |
| Wartungsaufwand | Keiner |
| False-Positive-Risiko | Niedrig bei Limit 3, sehr niedrig bei Limit 4 |
| Adoptionsbarriere | Sehr niedrig |

**Empfehlung:** Implementieren, Priorität 3. Niedrig-hängende Frucht, wenig Impact.
Nice-to-have, kein Must-have. Macht mehr Sinn als Qualitäts-Cleanup als als AI-Metrik.

---

## Haltbarkeit

Typ-Reasoning verbessert sich bei neueren Modellen schnell. Diese Regel adressiert primär
Typ-Inferenzprobleme — in 2 Jahren vermutlich weniger kritisch. Als Code-Qualitätssignal
(auch für Menschen) bleibt sie sinnvoll.
