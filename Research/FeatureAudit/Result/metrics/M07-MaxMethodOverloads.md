# MaxMethodOverloads (M07)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 3 | **Severity:** error | **Status:** Aktiv  
**Paper-Cluster genutzt:** D, C, H

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** Der Grenzwert von 3 Overloads pro Methodenname ist zu eng für Standard-.NET-Patterns — viele etablierte .NET-Bibliotheken-Konventionen (Extension Methods, Builder Pattern, Fluent API) erzeugen regulär 4–6 Overloads; die Evidenzlage für diesen spezifischen Grenzwert ist zudem schwach.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 2 | Unter 3 wäre selbst die klassische `Parse`/`TryParse`-Muster-Kombination mit optionalem `IFormatProvider` nicht möglich |
| **Empfehlung (beste Evidenz)** | 5 | Erlaubt Standard-.NET-Overload-Muster (z. B. mit/ohne CancellationToken, mit/ohne IProgress, mit/ohne Timeout) ohne unnötige Verletzungen |
| **Obergrenze (Nutzen geht verloren)** | 8 | Ab 8 Overloads derselben Methode ist Refactoring zu einer Konfigurationsobjekt-Variante (Parameter-Record) klar sinnvoll |
| **Aktueller Wert** | 3 | Zu eng — Standard-.NET-Patterns wie `Send(message)`, `Send(message, ct)`, `Send(message, timeout, ct)` verletzen bereits die Grenze |

---

## Wissenschaftliche Grundlage

Keine direkte empirische Studie zu maximaler Overload-Anzahl als Qualitätsmetrik wurde gefunden. Der Grenzwert von 3 ist eine pragmatische Designentscheidung, die aus dem allgemeinen Prinzip abgeleitet ist, dass zu viele Overloads ein API-Design erschweren und Agenten bei der Auswahl der richtigen Overload-Variante verwirren können.

Das .NET-Ökosystem hat historisch eine starke Overload-Kultur: `Console.WriteLine`, `Task.Run`, `string.Format`, `File.ReadAllText` — viele BCL-Methoden haben 5–10 Overloads. Microsoft .NET Design Guidelines (API Design Guidelines, Brad Abrams/Krzysztof Cwalina) nennen Overloads als legitimes API-Design-Pattern für ergonomische Nutzung.

Die einzige empirische Näherung: Liu et al. (2024/2025) identifizieren „Project Context Conflicts" als häufigste Halluzinationsart. Viele ähnlich benannte Overloads könnten dazu beitragen, dass Agenten die falsche Überladung aufrufen — insbesondere wenn Parameter-Typen ähnlich sind.

## KI-Agenten-Perspektive

Aus LLM-Perspektive sind viele Overloads tatsächlich problematisch: Ein Agent muss beim Aufruf einer Methode entscheiden, welche Überladung die richtige ist. Bei vielen Overloads erhöht sich die Wahrscheinlichkeit, die falsche Variante auszuwählen — vor allem wenn Overloads sich nur in optionalen Parametern unterscheiden.

Jedoch ist der Grenzwert von 3 zu restriktiv. Die übliche .NET-Praxis ist:
- Methode ohne optionale Parameter
- Methode mit `CancellationToken`
- Methode mit weiteren Optionen

Das sind bereits 3 Overloads für ein völlig normales async-.NET-Pattern. Ein Grenzwert von 5 würde legitime Patterns zulassen und trotzdem API-Überladung (6+ Overloads) verhindern.

(Ableitung: Keine direkte Studie zu Overload-Anzahl und LLM-Fehlerrate. Begründung aus allgemeinen Prinzipien.)

## Zeitliche Einordnung

**Grundlagenstabilität:** Offen

Das Problem entsteht aus der LLM-Fähigkeit, beim Lesen von Aufrufen die richtige Überladung auszuwählen. Bessere Reasoning-Modelle könnten dies verbessern. Gleichzeitig bleibt das menschliche Verständlichkeitsproblem bei vielen Overloads bestehen.

---

## Empfehlung

**Aktion:** Wert auf 5 anpassen  
**Begründung:** 3 Overloads ist für Standard-.NET-Patterns zu eng und erzeugt False Positives bei gut strukturiertem Code; 5 erlaubt gängige .NET-Überladungskonventionen und verhindert trotzdem API-Design-Wildwuchs ab 6+.

---

## Quellen

- Liu et al. (2024/2025): „LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
- Microsoft .NET API Design Guidelines — learn.microsoft.com/en-us/dotnet/standard/design-guidelines/member-overloading
- Microsoft .NET Design Guidelines (Abrams & Cwalina): „Framework Design Guidelines" — Addison-Wesley
- Du et al. (2025): „The Hidden Cost of Readability: How Code Formatting Silently Affects LLMs" — arXiv:2503.17407
