# MaxTupleNestingDepth — Verschachtelte Tupel und anonyme Typen

**Impact: niedrig | Aufwand: niedrig | Haltbarkeit: kurzfristig (1–2 Jahre)**

---

## Problem

```csharp
var x = (1, (2, (3, 4)));                       // Tiefe 3 → Violation
var y = new { A = new { B = new { C = 1 } } }; // Tiefe 3 → Violation
```

Anonyme Typen und Tupel haben keine stabilen Namen. Agenten referenzieren sie über
Positionsindizes oder implizite Property-Namen. Ab Tiefe 3 verliert der Agent beim Editieren
die Struktur:

```csharp
// Tiefe 3 — Agent produziert Fehler:
var data = (outer: 1, inner: (mid: 2, deep: (x: 3, y: 4)));

// Agent generiert fälschlich:
var x = data.Item2.Item2.Item1;   // falsch — sollte data.inner.deep.x sein
var x = data.inner.Item2.x;       // falsch — mixed access style
var x = data.inner.deep.Item1;    // falsch — .x ist der korrekte Name
```

**Lösung:** `record` oder Named-Tuple für alles ab Tiefe 2.

---

## Implementierung

### Roslyn-Implementierung

```csharp
int GetTupleDepth(TypeSyntax type)
{
    if (type is TupleTypeSyntax tuple)
        return 1 + tuple.Elements.Max(e => GetTupleDepth(e.Type));
    return 0;
}

// Für anonyme Objekte:
int GetAnonymousDepth(AnonymousObjectCreationExpressionSyntax anon)
{
    return 1 + anon.Initializers
        .Where(i => i.Expression is AnonymousObjectCreationExpressionSyntax)
        .Select(i => GetAnonymousDepth((AnonymousObjectCreationExpressionSyntax)i.Expression))
        .DefaultIfEmpty(0)
        .Max();
}
```

### rules.json-Config

```json
"MaxTupleNestingDepth": 2
```

Tiefe 1: `(int x, int y)` — OK, idiomatisch.
Tiefe 2: `(string name, (int x, int y) coords)` — Grenzfall, noch erlaubt.
Tiefe 3+: Fast immer ein Signal für fehlenden `record`.

### Beispiele

```csharp
// OK: Tiefe 1
(int Id, string Name) result = GetUser();

// OK: Tiefe 2 (Grenzfall, erlaubt)
(string Name, (int X, int Y) Position) entity = GetEntity();

// VIOLATION: Tiefe 3
var nested = (1, (2, (3, 4)));

// FIX: Record verwenden
record Point(int X, int Y);
record Entity(string Name, Point Position);
var entity = new Entity("Test", new Point(3, 4));
```

---

## Praxis-Bewertung

| Dimension | Bewertung |
| :--- | :--- |
| Wartungsaufwand | Keiner |
| False-Positive-Risiko | Niedrig — tief verschachtelte Tupel sind selten und klarer Code-Smell |
| Adoptionsbarriere | Sehr niedrig |

**Einschränkung:** Tritt so selten auf, dass der praktische Nutzen begrenzt ist.
Wenn man `MaxPublicMembersPerType` und `MaxBoolParameterCount` implementiert hat, ist diese
Regel das kleinste verbleibende Problem.

**Empfehlung:** Implementieren, aber Priorität 3. Nice-to-have. Lohnt sich vor allem, weil
die Implementierung trivial ist und das Muster eindeutig ein Code-Smell ist.

---

## Haltbarkeit

Typ-Reasoning verbessert sich schnell. In Verbindung mit C# 12+ Primary Records wird das
Muster ohnehin seltener. Niedrige Langzeit-Relevanz.
