# MaxSwitchArms — Switch-Expression-Armzahl

**Impact: mittel | Aufwand: niedrig | Haltbarkeit: mittelfristig (2–5 Jahre)**

---

## Problem

```csharp
var label = status switch {
    A => "Offen",   B => "Wartend",  C => "Aktiv",   D => "Pausiert",  E => "Fertig",
    F => "Storniert", G => "Archiviert", H => "Gelöscht", I => "Fehler",  J => "Retry",
    K => "Timeout", _  => "Unbekannt"   // 12 Arms → Violation
};
```

Ein Agent, der einen einzelnen Arm editiert, muss alle anderen Arms auf Überlappung und
Vollständigkeit prüfen. Ab ca. 8–10 Arms übersteigt das die effektive Attention-Kapazität
für lokale Edits.

Symptome in der Praxis:
- Agent fügt einen neuen Arm ein, übersieht aber, dass ein bestehender Arm denselben Fall
  bereits teilweise abdeckt
- Agent ändert einen Arm und vergisst das `_ => throw new UnreachableException()` zu entfernen,
  obwohl der Wildcard-Fall nun redundant ist
- Agent referenziert in einem neuen Arm einen Enum-Wert der zur Compile-Zeit nicht existiert

**Mehr Arms = fehlendes Polymorphismus- oder Dispatch-Pattern.** Ein Switch mit 12+ Arms
ist fast immer ein Zeichen, dass eine Visitor-Implementierung oder ein Dictionary-Dispatch
angemessener wäre.

---

## Abgrenzung zu bestehender Logik

AiNetLinter hat bereits `SwitchDispatcherDetector` und `ExcludeSwitchDispatcherCases`.
Einfache Dispatcher-Switches (Caseheader → kurzer Methodenaufruf) werden bereits aus der
Komplexitätsmessung ausgenommen. `MaxSwitchArms` würde *zusätzlich* die absolute Armzahl
begrenzen, unabhängig vom Dispatcher-Pattern.

---

## Implementierung

### Roslyn-Implementierung

```csharp
// Switch-Expression:
var switchExpr = node as SwitchExpressionSyntax;
int armCount = switchExpr.Arms.Count;

// Switch-Statement (klassisch):
var switchStmt = node as SwitchStatementSyntax;
int sectionCount = switchStmt.Sections.Count;
// Achtung: Sections können mehrere Labels haben (case A: case B: goto...) — 
// Labels zählen, nicht Sections
int labelCount = switchStmt.Sections.SelectMany(s => s.Labels).Count();
```

### rules.json-Config

```json
"MaxSwitchArms": 10,
"MaxSwitchArmsExcludeDispatcher": true
```

`MaxSwitchArmsExcludeDispatcher: true` — aktiviert die bestehende `ExcludeSwitchDispatcherCases`-
Logik auch für diesen Check. Dispatcher-Switches mit 15 Arms sind legitim.

Empfohlener Grenzwert: **10** (nicht 8 — zu aggressiv für idiomatisches Enum-Dispatch).

### Beispiele

```csharp
// VIOLATION: 12 Arms in Status-Switch
OrderStatus? label = status switch
{
    Open => "Offen", ... (12 Arms)
};

// OK: Dispatch an Methoden (Dispatcher-Pattern, exempt):
return status switch
{
    Open      => HandleOpen(order),
    Cancelled => HandleCancelled(order),
    ...  // 12 Arms, aber jeder Arm ist ein einzelner Methodenaufruf
};

// EMPFEHLUNG bei vielen Arms: Dictionary-Dispatch
private static readonly Dictionary<OrderStatus, string> Labels = new()
{
    { Open, "Offen" }, { Cancelled, "Storniert" }, ...
};
```

### Ausnahmen

State-Machine-Switches mit vielen States sind oft legitim. Wenn der `ExcludeDispatcher`-
Exemption nicht greift, kann über TypeLevel-Exemptions ausgenommen werden:

```json
"MaxSwitchArmsExemptTypes": ["OrderStateMachine"]
```

---

## Praxis-Bewertung

| Dimension | Bewertung |
| :--- | :--- |
| Wartungsaufwand | Keiner |
| False-Positive-Risiko | Niedrig-Mittel — Dispatcher-Exemption deckt Hauptfall ab |
| Adoptionsbarriere | Niedrig |

**Empfehlung:** Implementieren, Priorität 2. Dispatcher-Exemption aktivieren.

---

## Haltbarkeit

Pattern-Reasoning verbessert sich mit besseren Modellen. Mittelfristig wird der Grenzwert
weniger kritisch — überladene Switch-Expressions sind aber ohnehin schlechtes Design,
unabhängig von LLMs. Die Regel bleibt als Code-Quality-Signal dauerhaft sinnvoll.
