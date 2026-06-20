# Paper-Cluster A: Komplexitätsmetriken

Erstellt: 2026-06-20  
Betrifft Features: M04, M05

---

## Gefundene Quellen

### McCabe, T.J. (1976) — A Complexity Measure
- **Fundort:** Journal of IEEE Transactions on Software Engineering (Originalpaper); Zusammenfassung via Web-Suche: "cyclomatic complexity defect density empirical study correlation McCabe"
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity)
- **Kernaussagen:**
  - Cyclomatic Complexity (CC) misst die Anzahl linear unabhängiger Pfade durch ein Programm.
  - Empfohlene Grenze: CC ≤ 10. Funktionen mit CC > 10 gelten als "risikoreich".
  - Die Metrik ist primär für Testbarkeit entwickelt worden, nicht für Wartbarkeit.
- **Konkrete Zahlen / Grenzwerte:**
  - CC > 10 → Modul gilt als risikoreich für Zuverlässigkeit.
  - Funktionen mit CC > 10 zeigen in empirischen Studien 2–3× höhere Defektdichte.
- **Einschränkungen:** Kritik von Shepperd (1988): CC korreliert stark mit LOC (r ≈ 0.9), misst also primär Größe, nicht strukturelle Komplexität. Lopez & Habra (2015) zeigen, dass ein Threshold < 10 im OO-Kontext statistisch nicht signifikant ist.
- **Zeitliche Einordnung:** 1976, zeitstabile Grundlage. Die Metrik selbst ist unverändert relevant; Grenzwert-Empfehlungen werden weiter diskutiert.

### Shepperd, M. (1988) — A Critique of Cyclomatic Complexity as a Software Metric
- **Fundort:** via Web-Suche: "code complexity bug density meta-analysis Shepperd Basili threshold 10"; PDF: https://www.cs.du.edu/~snarayan/sada/teaching/COMP3705/lecture/p1/cycl-1.pdf
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity)
- **Kernaussagen:**
  - Cyclomatic Complexity korreliert hochgradig mit LOC (Mean Korrelation ≈ 0.9), was ihre Eigenständigkeit als Metrik infrage stellt.
  - Die Metrik liefert kaum Information über LOC hinaus.
  - Trotzdem in der Industrie akzeptiert, weil operativ einfach zu messen.
- **Konkrete Zahlen / Grenzwerte:**
  - Aus einer Studie: Kendall-Wert von CC = 0.064 in einer Korrelationsuntersuchung zu Bugs (kaum Korrelation).
  - Basili et al.: Überraschender Befund — Fehler-Dichte sinkt mit wachsender CC in manchen Kontexten (kleine Funktionen mit CC=1 sind oft fehleranfälliger als erwartet).
- **Einschränkungen:** Ältere Studie (1988), OO-Paradigma noch nicht dominant. Befunde sind in OO-Sprachen weniger direkt übertragbar.
- **Zeitliche Einordnung:** 1988, als Kritik zeitstabil relevant.

### Campbell, G.A. (2018) — Cognitive Complexity: A New Way of Measuring Understandability
- **Fundort:** ACM Digital Library, "Proceedings of the 2018 International Conference on Technical Debt"; SonarSource-Whitepaper: https://www.sonarsource.com/docs/CognitiveComplexity.pdf; Ressource: https://www.sonarsource.com/resources/cognitive-complexity/
- **Betrifft AiNetLinter-Features:** M05 (MaxCognitiveComplexity)
- **Kernaussagen:**
  - Cyclomatic Complexity misst Testbarkeit gut, aber nicht Wartbarkeit/Verständlichkeit.
  - Cognitive Complexity berücksichtigt Verschachtelung (Nesting) — tiefe Schachtelung erhöht den Score überproportional.
  - Zwei Methoden mit identischer CC können völlig unterschiedliche Cognitive Complexity haben (Beispiel: kurze switch-Kette vs. tiefe if-else-Schachtelung).
- **Konkrete Zahlen / Grenzwerte:**
  - SonarQube-Default: CC ≤ 15. Für AiNetLinter ein Referenzwert.
  - Cognitive Complexity wird als besser geeignet für LLM-Lesbarkeits-Fragen eingestuft, da sie menschliches Verständnis modelliert.
- **Einschränkungen:** Primär Industriestandard von SonarSource, nicht peer-reviewed im klassischen Sinne. Neuere empirische Validierungen bestätigen die Überlegenheit gegenüber CC für Verständlichkeit.
- **Zeitliche Einordnung:** 2018, aktuell. Wird aktiv von SonarQube, ESLint und anderen Tools eingesetzt.

### Araújo et al. (2020) — An Empirical Validation of Cognitive Complexity as a Measure of Source Code Understandability
- **Fundort:** arXiv:2007.12520; via Web-Suche: "cognitive complexity vs cyclomatic complexity empirical comparison"
- **Betrifft AiNetLinter-Features:** M05 (MaxCognitiveComplexity)
- **Kernaussagen:**
  - Empirische Validierung: Cognitive Complexity korreliert besser mit wahrgenommener Verständlichkeit als Cyclomatic Complexity.
  - Bestätigt die theoretische Argumentation von Campbell (2018) experimentell.
- **Konkrete Zahlen / Grenzwerte:**
  - (Keine spezifischen Schwellwerte, aber Bestätigung der Metrik-Überlegenheit.)
- **Einschränkungen:** Stichprobengröße und Kontrollgruppen-Design sind wichtige Variablen; einzelne Studie.
- **Zeitliche Einordnung:** 2020, aktuell.

### Palomba, F. et al. (2016/2018) — On the diffuseness and the impact on maintainability of code smells
- **Fundort:** Semantic Scholar, ICSE 2018; via Web-Suche: "code complexity bug density meta-analysis"; PDF: https://fpalomba.github.io/pdf/Journals/J9.pdf
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity), M05 (MaxCognitiveComplexity)
- **Kernaussagen:**
  - Analyse von 395 Releases aus 30 Open-Source-Projekten, 17.350 manuell validierte Code-Smell-Instanzen.
  - Complex Class (Klassen mit hoher Cyclomatic Complexity) ist hochgradig verbreitet und erhöht Change-Proneness.
  - Smells mit langem/komplexem Code haben den größten negativen Einfluss auf Wartbarkeit.
- **Konkrete Zahlen / Grenzwerte:**
  - Complex Class und Long Method unter den sieben schädlichsten Smells.
  - Klassen mit diesen Smells zeigen signifikant höhere Bug-Anfälligkeit.
- **Einschränkungen:** Java-Projekte; Übertragbarkeit auf C# ist methodisch plausibel, aber nicht direkt gemessen.
- **Zeitliche Einordnung:** 2018, solide empirische Grundlage.

### Jaber et al. (2018) — Evaluation of Halstead and Cyclomatic Complexity Metrics in Measuring Defect Density
- **Fundort:** IEEE Xplore, https://ieeexplore.ieee.org/document/8447959/; via Web-Suche: "Evaluation of Halstead and Cyclomatic Complexity Metrics in Measuring Defect Density"
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity)
- **Kernaussagen:**
  - Halstead Volume und CC korrelieren stark miteinander (r ≈ 0.9), liefern also weitgehend redundante Information.
  - Beide Metriken sind konsistente Prädiktoren für Defektdichte.
  - HC und CC sind statistisch austauschbar als Defektdichte-Prädiktoren.
- **Konkrete Zahlen / Grenzwerte:**
  - Mittlere Korrelation: 0.904 zwischen LOC, Halstead Volume und CC.
- **Einschränkungen:** Kontext nicht näher bekannt; spezifisches Projekt-Set.
- **Zeitliche Einordnung:** 2018, aktuell.

### PMC/PeerJ (2023) — Evaluating the effectiveness of decomposed Halstead Metrics in software fault prediction
- **Fundort:** PMC: https://pmc.ncbi.nlm.nih.gov/articles/PMC10703020/; via Web-Suche: "Halstead software metrics empirical validation criticism reliability 2020"
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity) [Referenzvergleich]
- **Kernaussagen:**
  - Halstead-Metriken bleiben trotz Kritik an theoretischen Grundlagen in ML-basierten Fehlervorhersage-Modellen nützlich.
  - Halstead Program Difficulty (D) und Volume (V) sind effektivere Risikoprädiktoren als reine LOC.
  - Das theoretische Fundament (Software Science von Halstead 1977) ist umstritten; empirische Nutzbarkeit bleibt.
- **Konkrete Zahlen / Grenzwerte:**
  - (Keine spezifischen Grenzwerte empfohlen; Nutzung in Kombination mit anderen Metriken.)
- **Einschränkungen:** Halsteads theoretische Ableitung aus Programmierpsychologie wird stark kritisiert. "Empirical studies provide little support except for program length estimation." Trotzdem industriell weit verbreitet.
- **Zeitliche Einordnung:** 2023 (aktuelle Validierung). Die Kritik an Halstead ist zeitstabil seit den 1980ern.

---

## Übergreifende Erkenntnisse

**Konsens:** CC ≥ 10 als Risikogrenze ist Industrie-Konsens (McCabe, SonarQube, NDepend, Visual Studio). Empirisch ist die Korrelation zu Defektdichte positiv, aber nicht linear stark — LOC erklärt einen Großteil.

**Kontroverse:** Cyclomatic Complexity und LOC sind hochkorreliert (r ≈ 0.9). CC misst primär Testbarkeit gut, Verständlichkeit schlechter. Cognitive Complexity ist für Wartbarkeit besser geeignet.

**Grenzwert-Empfehlungen:**
- Cyclomatic Complexity: ≤ 10 (McCabe-Original), ≤ 15 (SonarQube-Default), ≤ 25 (NDepend-Warnstufe)
- Cognitive Complexity: ≤ 15 (SonarQube-Default)

**LLM-Relevanz:** Keine direkte Studie gefunden, die CC-Grenzwerte mit LLM-Agenten-Performance verbindet. Ableitung: Hohe CC → schwerer lesbar für Menschen und LLMs gleichermaßen; Cognitive Complexity als besserer Proxy für "LLM-Verständlichkeit" als CC (wegen Nesting-Faktor).

## Nicht gefunden / Lücken

- Keine direkte Studie: CC-Wert X → LLM-Fehlerrate Y.
- Keine Studie zu C#-spezifischen CC-Schwellwerten (Java/C dominiert).
- Halstead-Metriken in AiNetLinter nicht implementiert; nur zum Vergleich erwähnt.
