# Paper-Cluster B: Datei- und Methodengrößen

Erstellt: 2026-06-20  
Betrifft Features: M01, M02, M14

---

## Gefundene Quellen

### Liu, N. F. et al. (2023) — Lost in the Middle: How Language Models Use Long Contexts
- **Fundort:** arXiv (ursprüngliches Paper); ResearchGate: https://www.researchgate.net/publication/378284067_Lost_in_the_Middle_How_Language_Models_Use_Long_Contexts; Lecture-Folie: https://teapot123.github.io/files/CSE_5610_Fall25/Lecture_12_Long_Context.pdf; via Web-Suche: "'lost in the middle' Liu 2023 LLM long context attention performance"
- **Betrifft AiNetLinter-Features:** M01 (MaxLineCount), M02 (MaxMethodLineCount), M14 (MaxAIContextFootprint)
- **Kernaussagen:**
  - LLMs zeigen eine U-förmige Leistungskurve bei langen Kontexten: beste Performance wenn relevante Information am Anfang oder Ende steht, deutlich schlechtere Performance wenn sie in der Mitte liegt.
  - Das Phänomen ist auf die Softmax-basierte Self-Attention zurückzuführen: Aufmerksamkeitsgewichte werden bei langen Sequenzen verdünnt, insbesondere für mittlere Token.
  - "Primacy Bias" und "Recency Bias" — Anfang und Ende einer Datei werden bevorzugt verarbeitet.
- **Konkrete Zahlen / Grenzwerte:**
  - Performance-Einbruch bei Informationen in der Mitte nachweisbar ab ca. 2k Token, deutlich ab 4k+ Token.
  - Effekt wurde über mehrere Modelle (GPT-3.5, GPT-4, Claude usw.) reproduziert.
- **Einschränkungen:** Paper aus 2023 bezieht sich auf Modell-Generationen ohne Million-Token-Kontextfenster. Neuere Forschung (2024–2026) zeigt, dass das Problem abschwächt aber nicht verschwindet.
- **Zeitliche Einordnung:** 2023. Grundlegendes Phänomen. Neuere Modelle reduzieren den Effekt, können ihn aber nicht vollständig eliminieren (strukturelle Ursache in Attention-Mechanismus).

### Introl/Flow-AI Guides (2024–2025) — LLM Long Context Window Code Understanding
- **Fundort:** Mehrere Quellen: https://introl.com/blog/long-context-llm-infrastructure-million-token-windows-guide; https://flow-ai.com/blog/advancing-long-context-llm-performance-in-2025; https://arxiv.org/pdf/2503.17407; via Web-Suche: "LLM long context window code understanding 2024 2025 large context"
- **Betrifft AiNetLinter-Features:** M01 (MaxLineCount), M14 (MaxAIContextFootprint)
- **Kernaussagen:**
  - Bis Ende 2025: Flagship-Modelle erreichen 200k–1M+ Token Kontextfenster standardmäßig.
  - Trotz Millionen-Token-Fenstern bleibt Long-Context-Code-QA "far from solved" — Perturbationen, die echtes Reasoning erfordern, führen weiter zu Fehlern.
  - Neue Benchmarks: LongCodeU, LongCodeBench für 1M-Kontext-Evaluation.
  - Ein arxiv-Paper (2602.17183) zeigt: "LLMs remain vulnerable to perturbations that require genuine reasoning rather than pattern recognition" in langen Code-Kontexten.
- **Konkrete Zahlen / Grenzwerte:**
  - 2025/2026: 1M+ Token als Standard für Top-Modelle.
  - Aber: Robustheit bleibt trotzdem eingeschränkt bei komplexen, verschachtelten Code-Strukturen.
- **Einschränkungen:** Schnell veralternde Zahlen durch rasche Modell-Entwicklung. Die strukturelle Herausforderung bleibt, auch wenn Fenstergrößen wachsen.
- **Zeitliche Einordnung:** 2024–2025. Modellgeneration-spezifisch bezüglich konkreter Token-Grenzen; strukturelles Problem zeitlos.

### Kochhar et al. / arxiv (2022) — An Empirical Study on Maintainable Method Size in Java
- **Fundort:** arXiv:2205.01842; via Web-Suche: "optimal file size lines of code maintainability software quality empirical"
- **Betrifft AiNetLinter-Features:** M02 (MaxMethodLineCount)
- **Kernaussagen:**
  - Empirisch fundierte Empfehlung: Methoden sollten ≤ 24 SLOC bleiben.
  - Frühere Empfehlungen (≤ 15 SLOC, ≤ 20 SLOC) basieren auf Intuition, nicht auf Evidenz.
  - Methoden > 24 SLOC sind nachweislich schlechter wartbar.
  - Hinweis: "Immer unter 24 SLOC zu halten ist unrealistisch" — als Trade-off zu behandeln.
- **Konkrete Zahlen / Grenzwerte:**
  - **24 SLOC**: empirisch fundierte Obergrenze für gute Wartbarkeit.
  - Weit verbreitete Praxis-Heuristik: Methoden > 50 Zeilen → Entwickler verlieren Kontext beim Scrollen.
- **Einschränkungen:** Java-basiert. C# mit LINQ-Heavy-Code kann in weniger Zeilen mehr Komplexität ausdrücken.
- **Zeitliche Einordnung:** 2022. Zeitstabile Grundlage — strukturelle Argumente gelten unabhängig von LLM-Generationen.

### Ardito et al. (2020) — A Tool-Based Perspective on Software Code Maintainability Metrics: A Systematic Literature Review
- **Fundort:** Wiley Online Library: https://onlinelibrary.wiley.com/doi/10.1155/2020/8840389; via Web-Suche: "optimal file size lines of code maintainability software quality empirical"
- **Betrifft AiNetLinter-Features:** M01 (MaxLineCount), M02 (MaxMethodLineCount)
- **Kernaussagen:**
  - Starke negative Korrelation zwischen SLoC und Wartbarkeit auf Datei-Ebene (je größer, desto schlechter wartbar).
  - "Vollständige Einigkeit in der Forschungsgemeinschaft über den Nutzen von Größe als Indikator für zukünftige Wartungskosten."
  - Kein einheitlicher Konsens über die optimale Metrik-Suite, aber SLoC als einzelne Metrik gilt als zuverlässig.
  - Große, problematische Dateien existieren unabhängig von der Programmiersprache.
- **Konkrete Zahlen / Grenzwerte:**
  - Kein spezifischer Schwellwert, aber klare Evidenz: größere Dateien = schlechtere Wartbarkeit.
- **Einschränkungen:** Kein einheitlicher Schwellwert; Kontext (Projekttyp, Sprache) beeinflusst optimale Grenzen.
- **Zeitliche Einordnung:** 2020. Zeitstabile Grundlage.

### arxiv (2024) — Industrial Code Quality Benchmarks: Toward Gamification of Software Maintainability
- **Fundort:** arXiv:2412.06307; via Web-Suche: "optimal file size lines of code maintainability software quality empirical"
- **Betrifft AiNetLinter-Features:** M01 (MaxLineCount)
- **Kernaussagen:**
  - Industrie-Benchmarks zeigen konsistente Muster: Dateien mit > 500–700 LOC werden in großen Projekten als Wartungs-Risiko identifiziert.
  - Metrik-Gamification kann Entwickler motivieren, Dateigrößen zu reduzieren.
- **Konkrete Zahlen / Grenzwerte:**
  - Industrie-Benchmarks: 500–700 LOC als häufige Grenzen in Tooling.
- **Einschränkungen:** Industrielle Benchmarks, keine randomisierten Experimente.
- **Zeitliche Einordnung:** 2024. Aktuell.

### Palomba, F. et al. (2018) — On the Diffuseness and Impact on Maintainability of Code Smells
- **Fundort:** ICSE 2018; fpalomba.github.io: https://fpalomba.github.io/pdf/Journals/J9.pdf; via Web-Suche: "method length bug density Palomba 2018 empirical code smells maintainability"
- **Betrifft AiNetLinter-Features:** M02 (MaxMethodLineCount), M04 (MaxCyclomaticComplexity)
- **Kernaussagen:**
  - Long Method ist unter den sieben schädlichsten Code Smells für Wartbarkeit.
  - Long Method erhöht Change-Proneness und Bug-Anfälligkeit signifikant.
  - 395 Releases von 30 Open-Source-Projekten, 17.350 manuell validierte Smell-Instanzen.
- **Konkrete Zahlen / Grenzwerte:**
  - Long Method-Klassen: signifikant höhere Bug-Anfälligkeit vs. nicht-smell-behaftete Klassen.
- **Einschränkungen:** Java-Projekte.
- **Zeitliche Einordnung:** 2018. Solide, zeitstabile Evidenz.

---

## Übergreifende Erkenntnisse

**Kernbotschaft zu Dateigrößen:** Es besteht wissenschaftlicher Konsens, dass größere Dateien schlechtere Wartbarkeit zeigen. Die Schwelle, ab der der Effekt stark wird, liegt zwischen 300–700 LOC je nach Quelle und Kontext. AiNetLiners aktueller Wert von 700 liegt am oberen Ende des Empfehlungsbereichs.

**Kernbotschaft zu Methodengrößen:** ≤ 24 SLOC ist die einzige empirisch fundierte Grenze (Kochhar et al., 2022). Praxis-Heuristiken nennen 20–50 Zeilen. AiNetLinters Wert sollte in diesem Bereich liegen.

**LLM-Spezifisch:** "Lost in the Middle" (Liu 2023) ist das wichtigste Paper für AiNetLinter-Kontext: Lange Dateien bedeuten mehr Code in der "Mitte des Kontextfensters" — genau dort wo LLM-Attention schwächer wird. Selbst mit 1M-Token-Fenstern (2025) bleibt die strukturelle U-Kurve ein Thema. **Der Effekt rechtfertigt kleinere Dateien aus LLM-Perspektive unabhängig von menschlicher Wartbarkeit.**

**Trade-off:** Zu kleine Dateien → Fragmentierung → LLMs müssen mehr Dateien im Kontext laden. Basili et al. fanden sogar, dass sehr kleine Module (wenige Zeilen) manchmal fehleranfälliger sind als mittelgroße.

## Nicht gefunden / Lücken

- Keine direkte Studie: "C#-Datei mit X LOC → LLM-Fehlerrate Y"
- Keine spezifische Studie zu RAG-Chunk-Größen für C#-Code (die meisten RAG-Studien nutzen prose/markdown, nicht Quellcode)
- Keine empirische Validierung des optimalen AiNetLinter-Grenzwerts (700 LOC) für LLM-Agenten spezifisch
