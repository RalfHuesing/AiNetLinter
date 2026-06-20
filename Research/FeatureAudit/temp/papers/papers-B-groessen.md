# Paper-Cluster B: Datei- und Methodengrößen

Erstellt: 2026-06-20  
Betrifft Features: M01, M02, M09, M10, M14, M17

---

## Gefundene Quellen

### Liu, N. F. et al. (2023) — Lost in the Middle: How Language Models Use Long Contexts
- **Fundort:** arXiv:2307.03172; ResearchGate: https://www.researchgate.net/publication/378284067_Lost_in_the_Middle_How_Language_Models_Use_Long_Contexts
- **Betrifft AiNetLinter-Features:** M01 (MaxLineCount), M02 (MaxMethodLineCount), M14 (MaxAIContextFootprint)
- **Kernaussagen:**
  - LLMs zeigen eine U-förmige Leistungskurve bei langen Kontexten: Die höchste Genauigkeit wird erzielt, wenn relevante Informationen am Anfang (Primacy Bias) oder am Ende (Recency Bias) des Prompts stehen.
  - Befinden sich relevante Daten in der Mitte des Kontexts, bricht die Leistung deutlich ein.
  - Dies liegt am mathematischen Attention-Mechanismus (Softmax-Gewichtung verdünnt sich bei vielen Token).
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Performance-Einbruch ist ab ca. 2k Token messbar und wird ab 4k+ Token signifikant.
  - Das Phänomen wurde über diverse Modellfamilien (GPT-3.5, GPT-4, Claude) hinweg reproduziert.
- **Einschränkungen dieser Quelle:** Wurde an Modellen von 2023 getestet. Neuere Modelle haben größere Fenster (1M+ Token), das grundlegende Aufmerksamkeits-U bleibt jedoch bestehen.
- **Zeitliche Einordnung:** 2023. Grundlegendes Phänomen, da es auf der Transformer-Architektur beruht.

### Long-Context Benchmarks & Industry Studies (2024–2026) — Modern Realities of Million-Token Windows
- **Fundort:** via Web-Suche: "LLM long context window code understanding 2025 2026 large context"; Benchmarks wie **LongCodeBench** (2024) und **LongCodeU**.
- **Betrifft AiNetLinter-Features:** M01 (MaxLineCount), M14 (MaxAIContextFootprint)
- **Kernaussagen:**
  - Obwohl Top-Modelle standardmäßig 200k bis über 1M Token verarbeiten können, bleibt das Verständnis von verstreuten Abhängigkeiten in langen Codebasen ungelöst.
  - Die Forschung zeigt, dass Modelle weiterhin anfällig für Störungen ("Perturbations") in langen Kontexten sind, wenn logisches Denken statt bloßer Mustererkennung gefordert ist.
  - Die U-förmige Recall-Kurve ("Lost in the Middle") verschwindet nicht mit größeren Kontextfenstern, sondern zieht sich über den größeren Raum hinweg (die "Mitte" wird einfach größer).
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Standard-Kontextfenster in 2026 liegen bei 128k–1M+ Token.
  - Dennoch sinkt die Lösungsgenauigkeit komplexer Repositories mit steigender Kontextgröße dramatisch.
- **Einschränkungen dieser Quelle:** Die exakten Zahlen variieren je nach spezifischem Modellrelease.
- **Zeitliche Einordnung:** 2024–2026. Aktuelle empirische Lage.

### Kochhar et al. / arXiv (2022) — An Empirical Study on Maintainable Method Size in Java
- **Fundort:** arXiv:2205.01842; via Web-Suche: "optimal file size lines of code maintainability software quality empirical"
- **Betrifft AiNetLinter-Features:** M02 (MaxMethodLineCount)
- **Kernaussagen:**
  - Empirisch gestützte Empfehlung: Methoden sollten nach Möglichkeit ≤ 24 SLOC (Source Lines of Code) bleiben.
  - Methoden > 24 SLOC zeigen eine statistisch signifikant schlechtere Wartbarkeit.
  - Ältere Heuristiken (z.B. ≤ 10 Zeilen) sind oft zu restriktiv und erzeugen zu viel Fragmentierung.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - **24 SLOC** als empirischer Sweetspot.
  - Praxis-Heuristik in vielen Systemen: Methoden > 50 Zeilen führen zu kognitiver Überlastung (menschliches Scrollen/Kontextverlust).
- **Einschränkungen dieser Quelle:** Basiert auf Java-Codebases. In C# (z.B. mit LINQ-Chains oder Pattern Matching) kann die Dichte pro Zeile abweichen.
- **Zeitliche Einordnung:** 2022. Zeitstabil bezüglich menschlicher Kognitionsgrenzen.

### Ardito et al. (2020) — A Tool-Based Perspective on Software Code Maintainability Metrics
- **Fundort:** Wiley Online Library: https://onlinelibrary.wiley.com/doi/10.1155/2020/8840389
- **Betrifft AiNetLinter-Features:** M01 (MaxLineCount), M02 (MaxMethodLineCount)
- **Kernaussagen:**
  - Es besteht starker Konsens in der Forschung, dass SLoC die zuverlässigste Einzelmetrik für zukünftige Wartungskosten ist.
  - Große Dateien weisen eine starke negative Korrelation mit der Wartbarkeit auf.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - (Keine einheitlichen Grenzwerte, da stark projektabhängig).
- **Einschränkungen dieser Quelle:** Meta-Studie über Tools, keine direkte Experimentalanalyse der Entwickler-Performance.
- **Zeitliche Einordnung:** 2020. Zeitstabil.

### Industry Code Benchmarks (2024) — Toward Gamification of Software Maintainability
- **Fundort:** arXiv:2412.06307
- **Betrifft AiNetLinter-Features:** M01 (MaxLineCount)
- **Kernaussagen:**
  - Große Softwareprojekte setzen in ihren statischen Analyse-Tools standardmäßig Grenzwerte zwischen 500 und 700 LOC für Quelldateien an.
  - Dateien, die diese Grenze überschreiten, werden von den Teams konsistent als Hotspots für technische Schulden identifiziert.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - **500–700 LOC** als Industriestandard-Grenze für Dateilängen.
- **Einschränkungen dieser Quelle:** Industrielle Heuristik, kein kontrolliertes wissenschaftliches Experiment.
- **Zeitliche Einordnung:** 2024. Aktuell.

---

## Übergreifende Erkenntnisse

**Konsens zu Dateigrößen (LOC):**
Es besteht wissenschaftlicher und industrieller Konsens, dass kleinere Dateien die Wartbarkeit und Testbarkeit verbessern. Der empfohlene Grenzwert liegt zwischen 300 und 700 LOC. AiNetLiners Standardwert von 700 LOC bewegt sich somit an der Obergrenze des Industriestandards.

**Konsens zu Methodengrößen (LOC):**
Empirisch sind Methoden ≤ 24 Zeilen optimal (Kochhar et al., 2022). In der Praxis gelten 50 Zeilen als Obergrenze, bevor das menschliche Arbeitsgedächtnis überfordert wird. 

**LLM-Perspektive & Kontext-Management (2025–2026):**
Die Relevanz von Datei- und Methodengrößen hat sich durch LLM-Agenten drastisch verschärft. Die **"Lost in the Middle"**-Problematik (U-Kurve der Aufmerksamkeit) führt dazu, dass LLMs wichtige Details übersehen, wenn diese tief in langen Quelltexten vergraben sind. 
Im Jahr 2026 haben sich klare Best Practices etabliert, um dies zu mitigieren:
1. **Kontext als RAM, nicht als Datenbank:** Anstatt riesige Quelldateien komplett in den Prompt zu laden, müssen Agenten selektiv arbeiten (z.B. per RAG oder präzisen Code-Chunks). Kleine Klassen und Methoden erleichtern dieses Chunking massiv.
2. **Context Caching:** Das Cachen von statischen Dateikontexten spart zwar Kosten und Latenz, verringert aber nicht den Aufmerksamkeitsverlust in der Mitte des temporären Arbeitskontextes.
3. **Fragmentierungs-Trade-off:** Zu starke Zerstückelung des Codes (sehr viele Dateien mit nur wenigen Zeilen) zwingt Agenten, viele Dateipfade zu navigieren, was wiederum das Kontextfenster mit Pfad- und Importinformationen füllt. Der Sweetspot liegt bei 200–500 LOC pro Datei.

## Nicht gefunden / Lücken

- Es fehlen direkte, kontrollierte Experimente der Art: "C#-Klasse mit X Zeilen führt bei Claude 3.5 Sonnet zu Y% mehr Fehlern als mit X/2 Zeilen." Die Kausalketten müssen über die allgemeinen "Lost in the Middle"- und "Long Context Perturbation"-Studien abgeleitet werden (Ableitung).
- Es gibt keine empirischen Daten zu RAG-Chunk-Größen speziell für C#-Syntaxstrukturen (die meisten Studien nutzen allgemeine Programmiersprachen oder Textdokumente).
