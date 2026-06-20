# MaxInheritanceDepth (M06)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 2 | **Severity:** error | **Status:** Aktiv  
**Paper-Cluster genutzt:** E, C, H

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** Der Grenzwert von 2 ist für C#-Projekte mit Frameworks (ASP.NET, Entity Framework, xUnit) zu restriktiv — Framework-Basisklassen erzwingen häufig DIT=3+, sodass die Regel ohne Ausnahmen praktisch nicht anwendbar ist; die Ausnahme-Konfiguration muss korrekt eingestellt sein, sonst ist der Wert trotz guter theoretischer Grundlage betrieblich nutzlos.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 2 | DIT=1 würde Abstrakte Basisklassen komplett verbieten; selbst einfachste OOP-Strukturen würden verletzt |
| **Empfehlung (beste Evidenz)** | 3 | Erlaubt eine eigene Basisklasse (DIT=1) plus eine Spezialisierung (DIT=2) in reinem Anwendungscode; bei korrekter Framework-Ausnahme-Konfiguration entspricht das der üblichen Clean-Architecture-Praxis |
| **Obergrenze (Nutzen geht verloren)** | 5 | CK-Metriken zeigen ab DIT=5–6 konsistent erhöhte Defektdichten; die Schutzwirkung des Grenzwerts endet hier |
| **Aktueller Wert** | 2 | Potentiell zu eng, wenn Framework-Basisklassen nicht korrekt ausgenommen sind — in diesem Fall fallen ASP.NET Controller, EF-Entities, xUnit-Testklassen als Violations an |

---

## Wissenschaftliche Grundlage

Chidamber & Kemerer (1994) führten DIT als Teil der CK-Suite ein: Tiefe Vererbungshierarchien erschweren das Verständnis von Klassen, da geerbte Methoden aller Vorfahren berücksichtigt werden müssen. Basili et al. (1996) validierten empirisch, dass DIT mit Fehlerrisiko korreliert — schwächer als CBO, aber statistisch signifikant. Subramanyam & Krishnan (2003) bestätigen den Zusammenhang an Java-Systemen.

Kritisch zu beachten: Die Studien zeigen erhöhte Defektdichten ab DIT > 5–6 — nicht ab DIT > 2. Ein Grenzwert von 2 ist deutlich strenger als jeder empirisch belegte Problembereich. Die Begründung für DIT ≤ 2 muss daher aus AI-Readability-Überlegungen kommen, nicht aus klassischen Qualitätsstudien.

Konkrete Gefahr: In modernen C#-Projekten mit ASP.NET Core ergibt sich für einen einfachen Controller bereits DIT=2 (Controller → ControllerBase → object). Ohne korrekte Framework-Ausnahmen ist dieser Wert für .NET-Projekte grundsätzlich unbrauchbar.

## KI-Agenten-Perspektive

Für LLM-Agenten ist DIT aus zwei Gründen problematisch:

1. **Kontextuelles Laden:** Wenn ein Agent eine Klasse verstehen muss, muss er potenziell alle Basisklassen laden. Bei DIT=4 sind das 4 Dateien, die den Kontext belasten. Bei DIT=2 ist das eine überschaubare Tiefe.

2. **Polymorphie-Unklarheit:** Ein Modell, das eine Methode `Render()` aufruft, muss bei tiefer Vererbung prüfen, ob diese in der Klasse selbst, in einer der Basisklassen oder durch ein Interface geerbt ist. Je tiefer die Hierarchie, desto mehr „Project Context Conflicts"-Halluzinationen entstehen (Liu et al. 2024/2025).

Jedoch: Die AI-Readability-Verbesserung entsteht primär dadurch, tiefe Custom-Hierarchien zu verhindern — nicht durch das Blockieren von Framework-Nutzung. Die Regel erfüllt ihren Zweck nur, wenn Framework-Basisklassen korrekt ausgenommen sind.

(Ableitung: Direkter Beleg für DIT=2 als optimalen LLM-Agenten-Grenzwert fehlt; die Begründung leitet sich aus dem Prinzip ab, Kontext-Ladebedarf zu minimieren.)

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Vererbungshierarchien zwingen Agenten zur Kontext-Expansion. Dieser strukturelle Effekt bleibt unabhängig von Modellgenerationen bestehen. Die Schutzwirkung des Grenzwerts ist dauerhaft relevant — solange er betrieblich korrekt konfiguriert ist.

---

## Empfehlung

**Aktion:** Wert auf 3 anpassen oder Framework-Ausnahme-Konfiguration sicherstellen  
**Begründung:** DIT ≤ 2 ist nur dann betrieblich sinnvoll, wenn alle relevanten Framework-Basisklassen korrekt in der Ausnahmeliste stehen; fehlt das, erzeugt die Regel ausschließlich False Positives. Ein Wert von 3 erlaubt eine eigene Abstraktionsschicht und Framework-Nutzung gleichzeitig.

---

## Quellen

- Chidamber & Kemerer (1994): „A Metrics Suite for Object Oriented Design" — IEEE Transactions on Software Engineering
- Basili, Briand & Melo (1996): „A Validation of Object-Oriented Design Metrics as Quality Indicators" — IEEE TSE
- Subramanyam & Krishnan (2003): „Empirical Analysis of CK Metrics for Object-Oriented Design Complexity" — researchgate.net/publication/3188321
- Liu et al. (2024/2025): „LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
- Concordia University / arXiv (2025/2026): „OpenClassGen: A Large-Scale Dataset for Class-Level Code Generation" — arXiv:2511.00872
