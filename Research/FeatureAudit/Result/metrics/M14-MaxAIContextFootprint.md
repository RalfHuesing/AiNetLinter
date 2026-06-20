# MaxAIContextFootprint (M14)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 5000 (transitive Zeilen) | **Severity:** error | **Status:** Aktiv  
**Paper-Cluster genutzt:** C, E, B

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** MaxAIContextFootprint ist das innovativste und LLM-spezifischste Feature des gesamten Toolsets — es operationalisiert direkt das „Lost in the Middle"-Problem als statische Metrik; der aktuelle Schwellenwert von 5.000 transitiven Zeilen ist allerdings zu weit gefasst und sollte auf 2.000–3.000 abgesenkt werden, um den empirisch beobachteten Aufmerksamkeitsabfall ab ~4k Token zu adressieren.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 500 | Sehr einfache Klassen mit wenigen Abhängigkeiten überschreiten kaum 500 transitive Zeilen; darunter wird fast nichts getroffen |
| **Empfehlung (beste Evidenz)** | 2.000–3.000 | „Lost in the Middle" zeigt Leistungsabfall ab ~2k Token (ca. 500–800 Zeilen); 2.000–3.000 Zeilen ≈ 6.000–10.000 Token — die obere Grenze des zuverlässigen Attention-Bereichs moderner Frontier-Modelle |
| **Obergrenze (Nutzen geht verloren)** | 5.000 | Aktueller Wert; 5.000 Zeilen ≈ 15.000–20.000 Token — bereits im problematischen Bereich der U-Kurve |
| **Aktueller Wert** | 5.000 | Zu locker — empirisch gesehen bereits im problematischen Aufmerksamkeitsbereich |

---

## Wissenschaftliche Grundlage

MaxAIContextFootprint ist ein in anderen Lintern nicht vorhandenes Feature, das die Kopplung einer Klasse (wie viele eigene Typen müssen für ein vollständiges Verständnis geladen werden) mit der kumulierten Zeilenanzahl (LOC) dieser transitiven Abhängigkeiten multipliziert. Dies adressiert zwei empirisch belegte Probleme:

**1. „Lost in the Middle" (Liu et al. 2023):** LLMs zeigen eine U-förmige Leistungskurve bei langen Kontexten. Der Leistungsabfall ist ab ca. 2.000 Token messbar und wird ab 4.000+ Token signifikant. Dies entspricht bei typischem C#-Code (ca. 3–5 Zeichen pro Token) einem Kontext von 600–1.300 Zeilen. Der aktuelle Grenzwert von 5.000 transitiven Zeilen liegt weit über diesem Bereich.

**2. Neuere Benchmarks (2024–2026):** Auch bei Frontier-Modellen mit 128k–1M+ Token Kontextfenstern verschwindet die U-Kurve nicht, sondern zieht sich nur über einen größeren Raum. Die Forschung zeigt, dass Modelle bei logisch verteilten Abhängigkeiten in langen Kontexten weiterhin anfällig für Störungen sind.

**3. Kopplung als Defektprädiktor (Cluster E):** CBO ist der stärkste empirisch belegte Defektprädiktor der CK-Metriken (Basili et al. 1996). MaxAIContextFootprint kombiniert CBO mit LOC zu einer einzigen, für LLM-Workflows optimierten Metrik — das ist eine konzeptionell überzeugende Synthese.

**4. OpenClassGen (2025/2026):** Direkte empirische Bestätigung: Hohe CBO-Werte korrelieren bei LLM-Klassen-Generierungen mit erhöhten Project Context Conflicts.

## KI-Agenten-Perspektive

MaxAIContextFootprint ist der direkteste Messwert für das, was einen LLM-Agenten beim Arbeiten an einer Klasse tatsächlich verlangsamt und fehleranfällig macht: die Menge an Code, die der Agent gleichzeitig im Arbeitskontext halten müsste um vollständig korrekte Änderungen vorzunehmen. Eine Klasse, die direkt und indirekt 8.000 Zeilen eigener Typen referenziert, ist für einen Agenten praktisch unbearbeitbar ohne selektive Kontextauswahl — und diese Selektion ist fehleranfällig.

Das Feature ist in keinem anderen Linter-Tool (SonarQube, StyleCop, NDepend, ESLint) vorhanden — es ist eine genuine Innovation von AiNetLinter und adressiert genau die Kernfrage des Audits.

Der aktuelle Schwellenwert von 5.000 ist problematisch: Bei angenommenen 3 Zeichen pro Token entspricht das ca. 1.700 Token reiner Code — in einem komplexen Agent-Workflow mit System-Prompt, Tool-Outputs und Gesprächskontext bleiben damit oft weniger als 10.000 Token für den eigentlichen Code-Kontext übrig. 2.000–3.000 Zeilen wäre ein sinnvollerer Grenzwert.

## Zeitliche Einordnung

**Grundlagenstabilität:** Modellgeneration-spezifisch

Mit deutlich verbesserten Modellen und besseren Scaffolding-Ansätzen (selektive RAG-Suche, Code-Graphen-Navigation) könnte ein höherer Grenzwert tolerierbar werden. Die U-Kurve selbst ist jedoch fundamental in der Transformer-Architektur verankert und wird nicht vollständig verschwinden.

---

## Empfehlung

**Aktion:** Wert auf 2.500–3.000 anpassen  
**Begründung:** Der aktuelle Wert von 5.000 transitiven Zeilen überschreitet den empirisch belegten kritischen Bereich für zuverlässige LLM-Aufmerksamkeit; eine Absenkung auf 2.500–3.000 Zeilen würde Klassen mit echter Kopplungs-Problematik zuverlässiger treffen ohne legitim kompakte Klassen zu belasten.

---

## Quellen

- Liu, N. F. et al. (2023) — Lost in the Middle: How Language Models Use Long Contexts — arXiv:2307.03172
- Long-Context Benchmarks & Industry Studies (2024–2026) — LongCodeBench; LongCodeU
- Concordia University / arXiv (2025/2026) — OpenClassGen — arXiv:2511.00872
- Basili, Briand & Melo (1996) — A Validation of Object-Oriented Design Metrics as Quality Indicators — IEEE TSE
- Empirical Agent Framework Studies (2024–2026) — Inside the Scaffold — arXiv:2604.03515
