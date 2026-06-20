# MinCognitiveComplexityForTest (M16)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 3 | **Severity:** warning (via TestSentinel) | **Status:** Aktiv (Teil des TestSentinel-Systems R08)  
**Paper-Cluster genutzt:** A, G

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Der Schwellenwert 3 für das Erzwingen eines Tests ist empirisch durch das Prinzip des risikobasierten Testens gestützt — Klassen mit Cognitive Complexity > 3 sind nachweislich testbedürftiger —, aber der Wert ist sehr aggressiv und sollte auf 5–7 angehoben werden um die Warning-Dichte auf ein sinnvolles Niveau zu begrenzen.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 2 | Cognitive Complexity 1 oder 2 hat nahezu jede Methode; darunter würde fast jede Klasse einen Test erzwingen |
| **Empfehlung (beste Evidenz)** | 5–7 | Kompromiss zwischen Risikobasierung (höhere Werte brauchen Tests) und Praktikabilität; Kochhar et al. (2015) zeigt, dass hohe Komplexität × Coverage-Lücke besonders kritisch ist — CogC > 5 ist ein vernünftiges Risikolevel |
| **Obergrenze (Nutzen geht verloren)** | 12 | Nahe dem SonarQube-Default (15) für „zu komplex" — hier ist Test schon zwingend |
| **Aktueller Wert** | 3 | Zu niedrig — erzeugt bei typischen Projekten sehr viele Warnings auch für einfache Klassen |

---

## Wissenschaftliche Grundlage

M16 implementiert das Prinzip des risikobasierten Testens: Nicht jede Klasse benötigt einen Test, aber Klassen mit höherer Komplexität sind statistisch fehleranfälliger und sollten priorisiert getestet werden. Dies ist empirisch gut gestützt:

**Kochhar et al. (2015):** Klassen mit höherer Komplexität und niedrigerer Coverage haben tendenziell höhere Defektdichte. Risikobasierte Priorisierung (Komplexität × Coverage-Lücke) zeigt bessere Ergebnisse als reine Coverage-Ziele.

**Antinyan & Staron (2022):** Empirische Validierung, dass Komplexitätsmetriken (CC, Cognitive Complexity) geeignete Prädiktoren für Testbarkeit sind. Weniger testbare Klassen zeigen höhere Defektdichte nach Release.

**SonarQube-Standard:** Cognitive Complexity ≤ 15 als Default für „zu komplex / Refactoring-Kandidat". Der Wert 3 als Mindest-Schwelle für Test-Erzwingung ist deutlich konservativer als Branchen-Empfehlungen.

**Gruner & Marticorena (2020):** Cognitive Complexity korreliert mit Verständlichkeit ähnlich gut wie CC — kein klarer Vorteil als präziserer Prädiktor. Für M16 ist CC genauso geeignet wie CogC.

**Kritische Einschätzung zum Schwellenwert 3:** Ein Cognitive-Complexity-Wert von 3 ist bereits bei einer Methode mit einem einzigen `if-else` und einem `foreach` erreicht. Damit erzwingt M16 für praktisch alle nicht-trivialen Klassen einen Test, was praktisch einer universellen Test-Pflicht entspricht. Das ist eine valide Designentscheidung, aber nicht spezifisch risikobasiert.

## KI-Agenten-Perspektive

Für LLM-Agenten ist der TestSentinel-Mechanismus (R08/M16) hochrelevant: Cover-Agent (CodiumAI, 2024/2025) zeigt, dass Agenten effektiv Tests generieren können, wenn explizite Testbarkeits-Anforderungen bestehen. M16 gibt dem Agenten ein klares Signal: „Diese Klasse benötigt Tests." Ein Agent, der neuen Code schreibt, kann dieses Signal direkt verarbeiten und entsprechend Tests erzeugen.

Der niedrige Schwellenwert 3 ist aus Agenten-Perspektive sogar vorteilhaft: Je früher ein Agent weiß, dass Tests erforderlich sind, desto eher wird er sie im selben Edit-Zyklus erstellen statt nachträglich. Die Warning-Severity (statt error) ist die richtige Wahl, da Tests nicht synchron zum Code-Edit geschrieben werden müssen.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Risikobasiertes Testen und die Korrelation zwischen Komplexität und Defektrisiko sind strukturelle Erkenntnisse, die unabhängig von Modellgenerationen gelten.

---

## Empfehlung

**Aktion:** Wert auf 5 anpassen  
**Begründung:** Der aktuelle Wert 3 erzeugt bei typischen Projekten eine sehr hohe Warning-Dichte, die dazu führt, dass die Warnings als Rauschen wahrgenommen werden; ein Wert von 5 trifft die tatsächlich komplexen Methoden zuverlässiger und erhöht das Signal-Rausch-Verhältnis, ohne das risikobasierte Prinzip aufzugeben.

---

## Quellen

- Kochhar et al. (2015) — Code Coverage and Test Suite Effectiveness: Empirical Study with Real Bugs — SANER 2015 — https://www.researchgate.net/publication/281965977
- Antinyan & Staron (2022) — Measuring and Improving Software Testability at the Design Level — Information and Software Technology — https://www.sciencedirect.com/science/article/abs/pii/S0950584924001162
- SonarSource — Cognitive Complexity White Paper (Campbell 2018) — https://www.sonarsource.com/docs/CognitiveComplexity.pdf
- Gruner & Marticorena (2020/2021) — An Empirical Validation of Cognitive Complexity — ESEM 2020 — https://dl.acm.org/doi/10.1145/3382494.3410636
- Incorpora et al. (2018–2024) — Recent Results on Classifying Risk-Based Testing Approaches — arXiv:1801.06812
- CodiumAI / arXiv (2024/2025) — Cover-Agent: An AI-Powered System for Automated Test Generation — arXiv:2406.00116
