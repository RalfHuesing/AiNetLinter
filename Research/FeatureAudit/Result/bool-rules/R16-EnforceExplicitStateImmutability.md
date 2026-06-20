# EnforceExplicitStateImmutability (R16)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** false (deaktiviert) | **Status:** Deaktiviert  
**Severity:** error (wenn aktiviert)  
**Paper-Cluster genutzt:** D, E, C

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** Immutabilität verbessert nachweislich Vorhersagbarkeit und Testbarkeit von Code und reduziert die Halluzinationsrate von LLM-Agenten bei der Zustandsverwaltung — aber die Erzwingung als Linter-Regel ist zu invasiv, weil "Klasse mit State" schwer maschinell klassifizierbar ist und die C# `record`/`readonly`-Semantik legitime Ausnahmen erzeugt.

---

## Empfehlung

**Aktion:** Deaktiviert lassen  
**Begründung:** Die präzise Klassifizierung "hat State und ist nicht immutable" erfordert Semantic-Analyse jenseits reiner Syntaxprüfung; false positives bei Entity-Klassen, Builder-Pattern und DTOs wären zu häufig, um die Regel als `error` zu führen.

---

## Wissenschaftliche / Empirische Grundlage

**Immutabilität als Design-Prinzip** ist in der C#-Community durch Microsoft-Empfehlungen zu Records und `readonly struct` gut unterstützt. C# 9+ Records bieten Value-Semantik mit eingebauter Immutabilität (`init`-only Properties, `with`-Ausdrücke). Das `readonly`-Schlüsselwort für Structs und `init`-Properties für Klassen sind offizielle Sprachmittel mit explizitem Immutabilitäts-Versprechen.

Aus **Cluster E (Architekturmetriken)**: Klassen mit hoher Zustandsmutabilität (measurable via CBO und hoher Write-Accessor-Dichte) korrelieren mit erhöhter Fehleranfälligkeit. Das ist der theoretische Unterbau — direkte Studien zu "explizite Immutabilitäts-Deklaration reduziert Defekte" fehlen jedoch.

Aus **Cluster D (Microsoft Design Guidelines)**: Value Objects als `record` oder `readonly struct` zu implementieren ist Best Practice in Domain-Driven Design. AiNetLiners R07 adressiert genau den DDD-Fall (`*ValueObject`-Suffix). R16 würde weiter gehen und alle State-Klassen abdecken — das ist der Streitpunkt.

**Das Erkennungsproblem:** Um zu prüfen, ob eine Klasse "State hat", müsste der Linter semantisch analysieren, welche Felder und Properties Zustand darstellen. Syntaxanalyse allein ist unzureichend: Eine Klasse mit nur privaten Settern kann trotzdem mutabel sein (durch interne Methoden); eine mit `public set` kann durch Konventionen unveränderlich sein.

## KI-Agenten-Perspektive

Aus Cluster C ("Empirical Agent Framework Studies", arXiv:2511.00872): **LLMs neigen zur Oversimplification** bei der Zustandsverwaltung — sie erzeugen flache, stark mutable Klassen ohne Kapselungsstrategie, was zu schwer nachvollziehbarem Zustandsmanagement führt. Eine Linter-Regel, die explizite Immutabilitätsdeklaration erzwingt, würde dem entgegenwirken.

**Aber:** Wenn die Regel zu viele false positives erzeugt (weil "State" schwer zu erkennen ist), verliert sie ihre Wirkung — der Agent würde lernen, die `error`-Ausgabe zu ignorieren oder durch formale Umgehungen zu unterdrücken (z.B. `// @suppress R16`).

Die LLM-Perspektive unterstützt das Prinzip, nicht die konkrete Linter-Implementierung.

## Zeitliche Einordnung

**Grundlagenstabilität:** Offen

Das Prinzip der Immutabilität als Design-Qualitätsmerkmal ist zeitlos. Ob es per Syntaxanalyse erzwingbar ist, hängt von der Entwicklung von Roslyn-Analysefähigkeiten ab. Mit besseren semantic-model-basierten Analysen könnte die Regel in Zukunft präziser implementiert werden.

## Risiken / Gegenargumente

**Legitime mutable Klassen:** Entity-Klassen in ORM-Szenarien (Entity Framework, Dapper), Command-Handler-State, Builder-Pattern-Klassen, UI-ViewModel-Bindungen — all diese erfordern Mutabilität als erstes Design-Kriterium. Eine flächendeckende Immutabilitätspflicht würde diese Patterns brechen.

**Überlappung mit R07:** Die Regel für ValueObjects (R07) deckt den klar abgrenzbaren Immutabilitäts-Fall bereits ab. R16 würde in dieses Terrain eingreifen, ohne dass der Mehrwert die Komplexität aufwiegt.

**Konfigurationsaufwand:** Die nötigen Ausnahmelisten (Entity-Suffixe, ViewModel-Suffixe, Builder-Suffixe) wären umfangreicher als der eigentliche Regelkern.

---

## Quellen

- Microsoft — C# Records: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record
- Chidamber & Kemerer, 1994, "A Metrics Suite for Object Oriented Design" — IEEE TSE
- arXiv:2511.00872, 2025/2026, "OpenClassGen: A Large-Scale Dataset for Class-Level Code Generation"
- Liu et al., 2024/2025, "LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
