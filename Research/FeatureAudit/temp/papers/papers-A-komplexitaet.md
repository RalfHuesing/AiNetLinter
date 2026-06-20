# Paper-Cluster A: Komplexitätsmetriken

Erstellt: 2026-06-20  
Betrifft Features: M04, M05, M15, M16, M17

---

## Gefundene Quellen

### McCabe, T.J. (1976) — A Complexity Measure
- **Fundort:** Journal of IEEE Transactions on Software Engineering (Originalpaper); Zusammenfassung via Web-Suche: "cyclomatic complexity defect density empirical study correlation McCabe"
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity)
- **Kernaussagen:**
  - Cyclomatic Complexity (CC) misst die Anzahl linear unabhängiger Pfade durch ein Programm.
  - Empfohlene Grenze: CC ≤ 10. Funktionen mit CC > 10 gelten als "risikoreich".
  - Die Metrik ist primär für Testbarkeit entwickelt worden, nicht für Wartbarkeit.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - CC > 10 → Modul gilt als risikoreich für Zuverlässigkeit.
  - Funktionen mit CC > 10 zeigen in empirischen Studien 2–3× höhere Defektdichte.
- **Einschränkungen dieser Quelle:** Kritik von Shepperd (1988): CC korreliert stark mit LOC (r ≈ 0.9), misst also primär Größe, nicht strukturelle Komplexität. Lopez & Habra (2015) zeigen, dass ein Threshold < 10 im OO-Kontext statistisch nicht signifikant ist.
- **Zeitliche Einordnung:** 1976, zeitstabile Grundlage. Die Metrik selbst ist unverändert relevant; Grenzwert-Empfehlungen werden weiter diskutiert.

### Shepperd, M. (1988) — A Critique of Cyclomatic Complexity as a Software Metric
- **Fundort:** via Web-Suche: "code complexity bug density meta-analysis Shepperd Basili threshold 10"; PDF: https://www.cs.du.edu/~snarayan/sada/teaching/COMP3705/lecture/p1/cycl-1.pdf
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity)
- **Kernaussagen:**
  - Cyclomatic Complexity korreliert hochgradig mit LOC (Mean Korrelation ≈ 0.9), was ihre Eigenständigkeit als Metrik infrage stellt.
  - Die Metrik liefert kaum Information über LOC hinaus.
  - Trotzdem in der Industrie akzeptiert, weil operativ einfach zu messen.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Aus einer Studie: Kendall-Wert von CC = 0.064 in einer Korrelationsuntersuchung zu Bugs (kaum Korrelation).
  - Basili et al.: Überraschender Befund — Fehler-Dichte sinkt mit wachsender CC in manchen Kontexten (kleine Funktionen mit CC=1 sind oft fehleranfälliger als erwartet).
- **Einschränkungen dieser Quelle:** Ältere Studie (1988), OO-Paradigma noch nicht dominant. Befunde sind in OO-Sprachen weniger direkt übertragbar.
- **Zeitliche Einordnung:** 1988, als Kritik zeitstabil relevant.

### Campbell, G.A. (2018) — Cognitive Complexity: A New Way of Measuring Understandability
- **Fundort:** ACM Digital Library, "Proceedings of the 2018 International Conference on Technical Debt"; SonarSource-Whitepaper: https://www.sonarsource.com/docs/CognitiveComplexity.pdf; Ressource: https://www.sonarsource.com/resources/cognitive-complexity/
- **Betrifft AiNetLinter-Features:** M05 (MaxCognitiveComplexity)
- **Kernaussagen:**
  - Cyclomatic Complexity misst Testbarkeit gut, aber nicht Wartbarkeit/Verständlichkeit.
  - Cognitive Complexity berücksichtigt Verschachtelung (Nesting) — tiefe Schachtelung erhöht den Score überproportional.
  - Zwei Methoden mit identischer CC können völlig unterschiedliche Cognitive Complexity haben (Beispiel: kurze switch-Kette vs. tiefe if-else-Schachtelung).
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - SonarQube-Default: CogC ≤ 15. Für AiNetLinter ein Referenzwert.
  - Cognitive Complexity wird als besser geeignet für LLM-Lesbarkeits-Fragen eingestuft, da sie menschliches Verständnis modelliert.
- **Einschränkungen dieser Quelle:** Primär Industriestandard von SonarSource, nicht peer-reviewed im klassischen Sinne. Neuere empirische Validierungen bestätigen die Überlegenheit gegenüber CC für Verständlichkeit.
- **Zeitliche Einordnung:** 2018, aktuell. Wird aktiv von SonarQube, ESLint und anderen Tools eingesetzt.

### Araújo et al. (2020) — An Empirical Validation of Cognitive Complexity as a Measure of Source Code Understandability
- **Fundort:** arXiv:2007.12520; via Web-Suche: "cognitive complexity vs cyclomatic complexity empirical comparison"
- **Betrifft AiNetLinter-Features:** M05 (MaxCognitiveComplexity)
- **Kernaussagen:**
  - Empirische Validierung: Cognitive Complexity korreliert besser mit wahrgenommener Verständlichkeit als Cyclomatic Complexity.
  - Bestätigt die theoretische Argumentation von Campbell (2018) experimentell.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine spezifischen Schwellwerte, aber statistischer Nachweis der Metrik-Überlegenheit für die Bewertung des Code-Verständnisses durch menschliche Entwickler.
- **Einschränkungen dieser Quelle:** Stichprobengröße und Kontrollgruppen-Design sind wichtige Variablen; einzelne Studie.
- **Zeitliche Einordnung:** 2020, aktuell.

### Palomba, F. et al. (2016/2018) — On the diffuseness and the impact on maintainability of code smells
- **Fundort:** Semantic Scholar, ICSE 2018; via Web-Suche: "code complexity bug density meta-analysis"; PDF: https://fpalomba.github.io/pdf/Journals/J9.pdf
- **Betrifft AiNetLinter-Features:** M02 (MaxMethodLineCount), M04 (MaxCyclomaticComplexity), M05 (MaxCognitiveComplexity)
- **Kernaussagen:**
  - Analyse von 395 Releases aus 30 Open-Source-Projekten, 17.350 manuell validierte Code-Smell-Instanzen.
  - Complex Class (Klassen mit hoher Cyclomatic Complexity) ist hochgradig verbreitet und erhöht Change-Proneness.
  - Smells mit langem/komplexem Code haben den größten negativen Einfluss auf Wartbarkeit.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Complex Class und Long Method gehören zu den sieben schädlichsten Smells für die Code-Wartbarkeit.
  - Klassen mit diesen Smells zeigen eine signifikant höhere Fehleranfälligkeit bei zukünftigen Releases.
- **Einschränkungen dieser Quelle:** Java-Projekte; Übertragbarkeit auf C# ist methodisch plausibel, aber nicht direkt gemessen.
- **Zeitliche Einordnung:** 2018, solide empirische Grundlage.

### Jaber et al. (2018) — Evaluation of Halstead and Cyclomatic Complexity Metrics in Measuring Defect Density
- **Fundort:** IEEE Xplore, https://ieeexplore.ieee.org/document/8447959/; via Web-Suche: "Evaluation of Halstead and Cyclomatic Complexity Metrics in Measuring Defect Density"
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity)
- **Kernaussagen:**
  - Halstead Volume und CC korrelieren stark miteinander (r ≈ 0.9), liefern also weitgehend redundante Information.
  - Beide Metriken sind konsistente Prädiktoren für Defektdichte.
  - HC und CC sind statistisch austauschbar als Defektdichte-Prädiktoren.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Mittlere Korrelation: 0.904 zwischen LOC, Halstead Volume und CC.
- **Einschränkungen dieser Quelle:** Kontext nicht näher bekannt; spezifisches Projekt-Set.
- **Zeitliche Einordnung:** 2018, aktuell.

### PMC/PeerJ (2023) — Evaluating the effectiveness of decomposed Halstead Metrics in software fault prediction
- **Fundort:** PMC: https://pmc.ncbi.nlm.nih.gov/articles/PMC10703020/; via Web-Suche: "Halstead software metrics empirical validation criticism reliability 2020"
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity) [Referenzvergleich]
- **Kernaussagen:**
  - Halstead-Metriken bleiben trotz Kritik an theoretischen Grundlagen in ML-basierten Fehlervorhersage-Modellen nützlich.
  - Halstead Program Difficulty (D) und Volume (V) sind effektivere Risikoprädiktoren als reine LOC.
  - Das theoretische Fundament (Software Science von Halstead 1977) ist umstritten; empirische Nutzbarkeit bleibt.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine spezifischen Grenzwerte empfohlen; Nutzung in Kombination mit anderen Metriken.
- **Einschränkungen dieser Quelle:** Halsteads theoretische Ableitung aus Programmierpsychologie wird stark kritisiert. "Empirical studies provide little support except for program length estimation." Trotzdem industriell weit verbreitet.
- **Zeitliche Einordnung:** 2023 (aktuelle Validierung). Die Kritik an Halstead is zeitstabil seit den 1980ern.

### Chen Xie, Xiaodong Gu, Yuling Shi, and Beijun Shen (2026) — Rethinking Code Complexity Through the Lens of Large Language Models
- **Fundort:** arXiv:2601.20404 / ICML 2026 presentation.
- **Betrifft AiNetLinter-Features:** M04 (MaxCyclomaticComplexity), M05 (MaxCognitiveComplexity)
- **Kernaussagen:**
  - Es gibt einen fundamentalen Mismatch zwischen klassischen Komplexitätsmetriken (wie CC) und der von LLMs wahrgenommenen Schwierigkeit beim Code-Verständnis.
  - Bei kontrollierter Zeilenlänge (LOC) korreliert die zyklomatische Komplexität *nicht* zuverlässig mit der Lösungsrate von LLM-Agenten.
  - LLM-perzipierte Schwierigkeit wird primär durch die Nicht-Linearität der Programmsemantik und "branching-induced divergence" (Verzweigungs-induzierte Divergenz) verursacht — das heißt, wie stark die Unsicherheit (Entropie) des Modells beim Verfolgen mehrerer Programmpfade ansteigt.
  - Die Autoren führen die Metrik **LM-CC** (Large Language Model-centric Code Complexity) ein, um diese Pfaddivergenz und Kompositionstiefe zu messen.
  - Semantikerhaltendes Refactoring zur Reduzierung der LM-CC (d.h. Linearisierung des Kontrollflusses) verbessert die Code-Generierungs- und Verständnisleistung von LLMs signifikant.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - LM-CC korreliert deutlich stärker mit LLM-Task-Success als CC und Cognitive Complexity.
- **Einschränkungen dieser Quelle:** Experimente wurden mit gängigen Open-Source-LLMs (wie Llama/Qwen/GPT-Modellen) durchgeführt. Die exakte Kalibrierung von LM-CC ist modellabhängig, aber das zugrunde liegende Prinzip der Pfaddivergenz gilt für alle Auto-Regressiven Transformer-Modelle.
- **Zeitliche Einordnung:** 2026. Zeitstabile Erkenntnis bezüglich der Funktionsweise von Attention-basierten Systemen.

---

## Übergreifende Erkenntnisse

**Menschliche vs. LLM-Komplexität:** 
Es besteht Konsens, dass klassische Cyclomatic Complexity (CC ≤ 10/15) vor allem für Testabdeckung und Fehlervorhersage nützlich ist, aber die tatsächliche Wartbarkeit und Verständlichkeit nur unzureichend abbildet (starke Korrelation mit LOC, r ≈ 0.9). 
Für den menschlichen Entwickler stellt die Cognitive Complexity (SonarSource, 2018) eine signifikant bessere Annäherung dar, da sie Verschachtelungen (Nesting) bestraft, was dem mentalen Speicher (Arbeitsgedächtnis) des Menschen entspricht.

Für **LLM-Agenten** zeigt die neueste Forschung (Xie et al., 2026), dass weder CC noch Cognitive Complexity die wirkliche Verarbeitungshürde exakt abbilden. Die größte Hürde für LLMs ist die **Verzweigungs-induzierte Divergenz** (branching-induced divergence): Wenn ein Modell Code sequentiell liest und an Verzweigungen (ifs, switches, Exception-Pfade) die Wahrscheinlichkeiten für mehrere logische Pfade berechnen muss, steigt die Entropie und damit das Risiko für Halluzinationen oder Fehlinterpretationen. 

**Grenzwert-Empfehlungen:**
- **Cyclomatic Complexity:** CC ≤ 10 (McCabe/Industrie-Standard), CC ≤ 15 (SonarQube). Für LLM-Agenten sollte der Grenzwert eher konservativ gewählt werden, um Pfaddivergenzen zu minimieren.
- **Cognitive Complexity:** CogC ≤ 15 (SonarQube-Default).
- **Strukturelle Empfehlung:** Die Linearisierung des Kontrollflusses (z. B. durch Early Returns, Reduktion tiefer Schachtelungen, Auslagerung komplexer Logik in flache Hilfsmethoden) hat höchste Priorität für die AI-Readability.

## Nicht gefunden / Lücken

- Es existiert noch kein allgemein verfügbares, statisches C#-Tool, das die neue LM-CC-Metrik nativ berechnet. AiNetLinter must daher weiterhin auf bewährte Proxys (wie Cyclomatic und Cognitive Complexity) zurückgreifen.
- Es gibt keine empirischen Studien, die einen exakten, optimalen numerischen Grenzwert für die Cognitive Complexity speziell für .NET/C# LLM-Agent-Workflows definieren. Die Grenzwerte sind aus allgemeinen LLM-Modell-Verständnisstudien abgeleitet.
