# MaxLinqChainLength (N03)

**Kategorie:** Neuer Feature-Vorschlag  
**Typ:** Numerische Metrik  
**Vorgeschlagener rules.json-Schlüssel:** `MaxLinqChainLength`  
**Implementierungsaufwand:** Mittel

---

## Bewertung

🟡 **PRÜFEN**

**Fazit:** Lange LINQ-Chains erzeugen eine Form von "sequenzieller Pfadkomplexität" die weder CC noch CogC messen — sie sind für Menschen lesbar genug um Linter-Regeln zu umgehen, aber für LLM-Agenten ein Fehler-Hotspot wenn Queries umgeschrieben oder erweitert werden müssen; der Grenzwert ist schwer zu kalibrieren.

---

## Was würde dieses Feature tun?

Die Regel zählt die Anzahl verketteter LINQ-Methoden in einer einzelnen Expressions-Kette und meldet einen Fehler ab dem konfigurierten Schwellenwert.

**Beispiel (Chain-Länge = 6):**
```csharp
var result = orders
    .Where(o => o.Status == OrderStatus.Active)
    .SelectMany(o => o.LineItems)
    .Where(li => li.Price > threshold)
    .GroupBy(li => li.ProductId)
    .OrderByDescending(g => g.Sum(li => li.Price))
    .Take(10);
```

**Konfigurationsbeispiel in rules.json:**
```json
"MaxLinqChainLength": 5,
"MaxLinqChainLengthSeverity": "warning"
```

**Empfohlener Schwellenwert:** 5 Methoden (6+ triggert Warnung).  
**Empfohlene Severity:** `warning` (nicht `error`), da LINQ-Chains oft legitime Daten-Transformation ausdrücken.

---

## Evidenz: Warum ist das Problem real?

**Cognitive Complexity und LINQ:** Die Cognitive Complexity-Metrik (Campbell 2018, Paper-Cluster A) wurde primär für imperative Strukturen (if/else, Schleifen, Switch) entwickelt. Eine LINQ-Kette mit 8 Methoden erhält CogC = 0 für die Kette selbst, auch wenn sie semantisch schwer zu verstehen ist. Das ist eine bekannte Lücke der Metrik.

**LM-CC und sequenzielle Komplexität (Xie et al. 2026):** Die LM-CC-Metrik beschreibt, dass LLMs bei jeder Verzweigung an Entropie gewinnen. In einer LINQ-Kette gibt es zwar keine expliziten Branches (jeder Operator ist sequenziell), aber jeder Operator transformiert den Typ der Elemente und kann optionale Semantik einführen (z.B. `.Where` filtert, `.SelectMany` flacht ab). Ein LLM-Agent der eine 8-gliedrige Kette versteht, muss den Zustand nach jedem Operator mental modellieren — das ist eine Form von sequenzieller kognitiver Last, die LM-CC nicht erfasst.

**Du et al. (2025) — Readability:** Lesbarkeit und Formatierung beeinflussen LLM-Leistung messbar. Lange LINQ-Chains sind ein klassischer Fall wo "es liest sich wie ein Satz" für erfahrene Entwickler (kurze Lernkurve), aber für Agenten schwer zu modifizieren ist wenn eine Methode in der Mitte hinzugefügt oder umgebaut werden muss.

**Praktische LLM-Fehlerquelle:** Agenten die LINQ-Queries erweitern sollen, machen häufig Fehler beim Einfügen von Operatoren in die Mitte einer Kette — falsche Typen an der Einschnittstelle, falsche Rückgabetypen von Lambda-Ausdrücken. Je länger die Kette, desto schwieriger ist die Einschnittstelle zu finden.

**Evidenzeinschränkung:** Es gibt keine dedizierte Studie zu "LINQ-Chain-Länge und LLM-Fehlerrate". Die Evidenz ist aus Readability-Forschung und LM-CC-Theorie abgeleitet. Das ist der Hauptgrund für die 🟡-Bewertung statt 🟢.

---

## Abgrenzung zu bestehenden Features

M04 (MaxCyclomaticComplexity) und M05 (MaxCognitiveComplexity) messen Verzweigungskomplexität, nicht LINQ-Kettenlänge. Eine LINQ-Kette mit 10 Operatoren ohne Branches hat CC=1 und CogC=0 — sie "besteht" die bestehenden Komplexitätsregeln problemlos.

M02 (MaxMethodLineCount) erfasst lange LINQ-Chains indirekt, wenn sie über viele Zeilen formatiert sind. Aber eine kompakte, einzeilige LINQ-Chain (z.B. `.Where(...).Select(...).OrderBy(...).Take(5)`) entkommt M02 vollständig.

N03 ist der einzige Mechanismus der speziell LINQ-Ausdrucks-Komplexität adressiert.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Offen

Mit besseren Modellen und verbessertem "Code-Graphen-Verständnis" könnten LLM-Agenten lange LINQ-Chains präziser verarbeiten. Andererseits bleibt das menschliche Lesbarkeits-Problem (7+ Operatoren in einer Kette) strukturell bestehen. Die Frage ist ob die LLM-Schwäche temporär (Modellgeneration-spezifisch) oder strukturell (Sequenz-Komplexität als Grundproblem) ist — das ist nicht klar belegbar.

---

## Implementierungshinweis

**Roslyn-Analyse:**
```csharp
// SyntaxWalker besucht InvocationExpressionSyntax
// Walk die MemberAccessExpression-Kette aufwärts bis das Wurzel-Objekt erreicht ist
// Zähle dabei alle LINQ-Methoden (Where, Select, SelectMany, GroupBy, OrderBy,
//   OrderByDescending, ThenBy, ThenByDescending, Take, Skip, First, FirstOrDefault,
//   Single, SingleOrDefault, Count, Any, All, Distinct, Union, Intersect, Except,
//   Join, GroupJoin, Aggregate, Sum, Min, Max, Average)
// Wenn Kettenanzahl > MaxLinqChainLength → Violation erzeugen
```

**Schwierigkeit:** Das korrekte Erkennen der LINQ-Kette erfordert, den Walk entlang der `MemberAccessExpression.Expression`-Kette durchzuführen und zu stoppen, wenn das Basisobjekt kein InvocationExpression mehr ist. Das ist syntaktisch lösbar, aber benötigt sorgfältige Implementierung um keine falschen Zählungen für reguläre (nicht-LINQ) Methoden-Ketten zu erzeugen.

**Konfigurierbare LINQ-Methoden-Whitelist:** Die Regel sollte nur echte LINQ-Methoden zählen, nicht beliebige Methoden-Ketten (z.B. `builder.AddLogging().AddRouting().Build()` wäre kein LINQ-Problem). Eine konfigurierbare Methoden-Whitelist ist nötig.

---

## Quellen

- Campbell, G.A. (2018) — Cognitive Complexity: A New Way of Measuring Understandability — SonarSource Whitepaper: https://www.sonarsource.com/docs/CognitiveComplexity.pdf
- Xie et al. (2026) — Rethinking Code Complexity Through the Lens of Large Language Models — arXiv:2601.20404
- Du et al. (2025) — The Hidden Cost of Readability: How Code Formatting Silently Affects LLMs — arXiv:2503.17407
- Araújo et al. (2020) — An Empirical Validation of Cognitive Complexity as a Measure of Source Code Understandability — arXiv:2007.12520
- (Ableitung, kein direktes Paper zu LINQ-Chain-Länge und LLM-Fehlerrate)
