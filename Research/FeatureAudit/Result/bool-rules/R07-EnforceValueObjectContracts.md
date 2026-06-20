# EnforceValueObjectContracts (R07)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true | **Status:** Aktiv  
**Severity:** error  
**Paper-Cluster genutzt:** D, E

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Die Erzwingung von `record` oder `readonly struct` für Klassen mit `*ValueObject`-Suffix ist ein präziser, low-false-positive Mechanismus der DDD-Contracts strukturell durchsetzt und für LLM-Agenten eindeutige Typ-Semantik erzeugt; Behalten.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** Value Objects sind ein fundamentales DDD-Konzept dessen korrekte Implementierung in C# (Wert-Gleichheit statt Referenz-Gleichheit, Immutabilität) ohne Sprachunterstützung fehleranfällig ist. `record`-Typen in C# 9+ implementieren Wertgleichheit und `init`-only-Properties automatisch — die Regel erzwingt genau diese semantisch korrekte Implementierung und vermeidet die klassische Fehlerquelle (vergessene `GetHashCode`/`Equals`-Überschreibungen).

---

## Wissenschaftliche / Empirische Grundlage

Value Objects sind ein zentrales Konzept in Domain-Driven Design (Evans 2003, Fowler 2002). Die Kernaussage: Objekte die durch ihre Attributwerte identifiziert werden (nicht durch Referenz) müssen strukturelle Gleichheit implementieren. In klassischem C#-Code bedeutete das: manuelle Implementierung von `Equals`, `GetHashCode`, den Gleichheitsoperatoren `==` und `!=`, sowie `IEquatable<T>` — eine fehleranfällige, wiederkehrende Boilerplate-Arbeit.

Mit C# 9 (2020) wurden `record`-Typen eingeführt, die all diese Anforderungen automatisch und korrekt implementieren. `readonly struct` bietet für kleine, stack-allozierte Value Objects eine Performance-optimierte Alternative. Microsofts C#-Dokumentation empfiehlt `record` ausdrücklich für "Typen, die Werte repräsentieren" (records as value types in the .NET Design Guidelines).

Aus Architekturperspektive (Cluster E) reduziert korrekte Value-Object-Implementierung die Kopplung: Ein Service der ein ValueObject konsumiert, muss nicht wissen wie Gleichheit implementiert ist — der Typ selbst definiert seinen Kontrakt. Dies korreliert direkt mit dem CBO-Prinzip aus den CK-Metriken: geringere Kopplung durch klar definierte Typ-Kontrakte.

Die Empirical OOD Complexity Study (arXiv:2511.00872, 2025) zeigt, dass LLM-generierte Systeme eine signifikante Tendenz zur "Oversimplification" von OO-Abstraktionen aufweisen. Value Objects die korrekt als `record` implementiert sind, sind für LLMs eine präzise und bekannte Abstraktion.

## KI-Agenten-Perspektive

`record`-Typen in C# sind für LLM-Agenten eine sehr gut verstandene Abstraktion — sie sind syntaktisch eindeutig, ihre Semantik (Wertgleichheit, Immutabilität durch `init`) ist im Prätraining gut repräsentiert. Wenn eine Klasse als `*ValueObject` benannt ist, signalisiert dies dem Agenten eindeutig: "Hier wird Wertgleichheit erwartet." Die Linter-Regel stellt sicher, dass die Implementierung dieser semantischen Erwartung entspricht.

Liu et al. (2024/2025) klassifizieren "Factual Knowledge Conflicts" (falsches Wissen über Typ-Semantik) als Halluzinationsquelle. Ein `*ValueObject` das intern als `class` mit Referenz-Gleichheit implementiert ist, verletzt die semantische Erwartung — und ein Agent der diesen Typ konsumiert und Wertgleichheit annimmt, wird Fehler produzieren. Die Regel verhindert dieses Mismatch präventiv (Ableitung, kein direktes Paper zu ValueObject + LLM-Fehlerrate).

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

`record` ist seit C# 9 (2020) ein stabiles Sprachmerkmal das in .NET 10 weiter gepflegt wird. Die DDD-Konzepte dahinter (Wertgleichheit, Immutabilität) sind fundamentale Designprinzipien. Die Regel ist dauerhaft relevant.

## Risiken / Gegenargumente

Das einzige Risiko ist eine zu enge Kopplung an das Suffix `*ValueObject`. Projekte die Value Objects anders benennen (z.B. `Money`, `Address`, `Temperature` ohne Suffix) werden nicht erfasst. Dies ist eine bekannte Einschränkung Suffix-basierter Linter-Regeln: Sie greifen nur bei disziplinierter Namensgebung. Für Projekte die das Suffix konsequent verwenden (wie bei AiNetLinter offenbar beabsichtigt), ist die Regel präzise. Für gemischte Codebasen ohne Suffix-Konvention ist die Regel wirkungslos. Dies ist ein Tradeoff zwischen Vollständigkeit und False-Positive-Rate — die aktuelle Konfiguration wählt korrekt die Low-False-Positive-Seite.

---

## Quellen

- Microsoft C# Documentation — Records (C# Reference), 2024 (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- Microsoft .NET Design Guidelines, 2024 (https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- Chidamber & Kemerer, 1994 — A Metrics Suite for Object Oriented Design; IEEE TSE (CK-Metriken, CBO-Kopplung)
- Concordia University — OpenClassGen: A Large-Scale Dataset for Class-Level Code Generation, arXiv:2511.00872, 2025/2026
- Liu et al. — LLM Hallucinations in Practical Code Generation, arXiv:2409.20550, 2024/2025 (https://dl.acm.org/doi/epdf/10.1145/3728894)
