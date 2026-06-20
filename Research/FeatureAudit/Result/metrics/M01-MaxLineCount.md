# MaxLineCount (M01)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 700 | **Severity:** error | **Status:** Aktiv  
**Paper-Cluster genutzt:** B, C, H

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Der Grenzwert von 700 LOC liegt an der Obergrenze des Industriestandards (500–700) und sollte auf 500 abgesenkt werden, da Agenten bei fragmentierungsarmen, aber klar begrenzten Dateien deutlich weniger Kontextfehler produzieren.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 150 | Unter 150 Zeilen erzwingt man übermäßige Fragmentierung, die Agenten zwingt, viele Dateipfade zu navigieren und das Kontextfenster mit Import-Chains zu füllen |
| **Empfehlung (beste Evidenz)** | 500 | Industriestandard-Mitte (Ardito et al. 2020, arXiv 2024); gleichzeitig Sweetspot aus „Lost in the Middle"-Perspektive |
| **Obergrenze (Nutzen geht verloren)** | 700 | Ab 700 Zeilen werden Dateien zu Hotspots für technische Schulden (arXiv:2412.06307); der aktuelle Wert liegt genau an dieser Grenze |
| **Aktueller Wert** | 700 | Zu locker — liegt an der definierten Obergrenze; Dateien mit genau 700 Zeilen passieren die Regel und sind trotzdem problematisch |

---

## Wissenschaftliche Grundlage

Der industrielle Konsens aus großen Softwaresystemen setzt Grenzwerte zwischen 500 und 700 LOC für Quelldateien an. Dateien die diese Grenze überschreiten, werden von Entwicklerteams konsistent als Hotspots für technische Schulden identifiziert (arXiv:2412.06307, 2024). Ardito et al. (2020) bestätigen in einer umfassenden Tool-Survey, dass SLoC (Source Lines of Code) die zuverlässigste Einzelmetrik für zukünftige Wartungskosten ist und große Dateien eine starke negative Korrelation mit Wartbarkeit zeigen.

Die Forschung zu Methodengrößen (Kochhar et al. 2022) zeigt, dass Methoden mit ≤ 24 SLOC optimal für Wartbarkeit sind. Überträgt man diesen Befund auf Dateiebene: Eine Datei mit 10–20 Methoden mittlerer Größe landet natürlich im Bereich 200–500 LOC. Dateien die deutlich darüber liegen, verletzen häufig das Single-Responsibility-Prinzip.

Widersprüche zwischen Quellen: Es gibt keine direkte empirische Studie die einen exakten Grenzwert für C#-Dateien mit statistischer Signifikanz belegt. Die Empfehlung von 500 LOC ist ein konsolidierter Industriewert, keine kausal nachgewiesene Schwelle.

## KI-Agenten-Perspektive

Das „Lost in the Middle"-Phänomen (Liu et al. 2023) zeigt, dass LLMs relevante Informationen in der Mitte langer Kontexte übersehen. Bei einer 700-Zeilen-Datei mit Methoden im mittleren Bereich liegt der entscheidende Code häufig in der schwachen „Mitte" des Aufmerksamkeitsfensters. Neuere Studien (Long-Context Benchmarks 2024–2026) bestätigen, dass das U-förmige Attention-Muster auch bei 1M-Token-Modellen bestehen bleibt.

Die Scaffold-Fehleranalyse (arXiv:2604.03515) zeigt: Über 50 % der Agenten-Ausfälle entstehen durch Kontext-Navigationsfehler, nicht durch mangelnde Generierungskompetenz. Kleinere, fokussierte Dateien reduzieren diesen Such-Overhead. Der Sweetspot aus LLM-Perspektive liegt laut Cluster-B-Analyse bei 200–500 LOC: Darunter erzeugt zu viele Navigationspunkte, darüber verstärkt sich das „Lost in the Middle"-Problem.

(Ableitung: Kausale Verknüpfung von „Datei hat 700 LOC" → „Agent macht mehr Fehler" ist nicht direkt gemessen. Der Zusammenhang läuft über die indirekte Kette: Größere Datei → mehr Kontext → schlechtere Attention-Ausnutzung → höhere Fehlerrate.)

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Der „Lost in the Middle"-Effekt ist in der Transformer-Architektur mathematisch verankert (Softmax-Gewichtung verdünnt sich bei vielen Token). Selbst wenn zukünftige Modelle bessere Kontext-Retrieval-Mechanismen entwickeln, bleibt das Prinzip kleiner, fokussierter Code-Einheiten aus Wartbarkeitsgründen gültig — unabhängig von LLM-Fähigkeiten.

---

## Empfehlung

**Aktion:** Wert auf 500 anpassen  
**Begründung:** 700 LOC liegt an der Obergrenze des Industriestandards; eine Absenkung auf 500 senkt das durchschnittliche Datei-Gewicht in Agenten-Kontexten, ohne kontraproduktive Fragmentierung zu erzwingen.

---

## Quellen

- Liu et al. (2023): „Lost in the Middle: How Language Models Use Long Contexts" — arXiv:2307.03172
- Ardito et al. (2020): „A Tool-Based Perspective on Software Code Maintainability Metrics" — Wiley Online Library, DOI:10.1155/2020/8840389
- arXiv:2412.06307 (2024): „Toward Gamification of Software Maintainability" — Industriestandard 500–700 LOC
- Kochhar et al. (2022): „An Empirical Study on Maintainable Method Size in Java" — arXiv:2205.01842
- arXiv:2604.03515 (2026): „Inside the Scaffold: Agent Failure Taxonomy"
- Long-Context Benchmarks, LongCodeBench (2024): Modern Realities of Million-Token Windows
