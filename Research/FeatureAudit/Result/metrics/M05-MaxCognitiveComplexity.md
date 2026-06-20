# MaxCognitiveComplexity (M05)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 15 | **Severity:** error | **Status:** Aktiv  
**Paper-Cluster genutzt:** A, C, H

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Der Grenzwert von 15 entspricht exakt dem SonarQube-Default und ist empirisch der stärkste Proxy für menschliche Verständlichkeit sowie eine deutlich bessere Näherung für LLM-Schwierigkeiten als Cyclomatic Complexity — beibehalten.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 8 | Unter CogC=8 werden selbst normal strukturierte Methoden mit einigen verschachtelten Bedingungen zur Violation; praxisuntauglich |
| **Empfehlung (beste Evidenz)** | 15 | SonarQube-Default (Campbell 2018); Araújo et al. (2020) validieren die Metrik empirisch; Industriestandard |
| **Obergrenze (Nutzen geht verloren)** | 25 | Ab CogC=25 übersteigt die mentale Komplexität das menschliche Arbeitsgedächtnis sicher; darüber ist ein Grenzwert ohne praktischen Schutz |
| **Aktueller Wert** | 15 | Optimal — entspricht exakt der besten empirischen Empfehlung |

---

## Wissenschaftliche Grundlage

Campbell (2018) entwickelte Cognitive Complexity als Reaktion auf die bekannten Schwächen der Cyclomatic Complexity: CC misst Testpfade gut, aber nicht die tatsächliche mentale Last beim Lesen. Cognitive Complexity berücksichtigt Verschachtelungstiefe überproportional (tiefes Nesting erhöht den Score stärker als flache Gleichrangigkeit), was dem Arbeitsgedächtnis-Prinzip entspricht: Verschachtelung erhöht den Kontext, den man im Kopf halten muss.

Campbell (2018) ist primär ein Industriestandard von SonarSource, kein klassisch peer-reviewtes Paper. Araújo et al. (2020) liefern die empirische Validierung: Cognitive Complexity korreliert in kontrollierten Experimenten besser mit der wahrgenommenen Verständlichkeit durch menschliche Entwickler als CC. Dies bestätigt Campbells theoretisches Argument experimentell.

Der SonarQube-Default von CogC ≤ 15 ist heute der de-facto-Industriestandard. Er wird von ESLint (JavaScript), SonarSource (alle Sprachen) und Roslyn-basierten Analyzern eingesetzt.

Der entscheidende Unterschied zu CC: Zwei Methoden mit CC=10 können CogC=5 oder CogC=25 haben. Eine flache Switch-Kette mit 10 Cases hat CC=10, aber CogC≈5 (keine Verschachtelung). Eine if-else-if-Kaskade mit 4 Ebenen Tiefe kann CC=5 haben, aber CogC=20 (tiefe Schachtelung wird überproportional bestraft). Für LLM-Lesbarkeit ist die Verschachtelungstiefe (CogC) relevanter als die Pfadanzahl (CC).

## KI-Agenten-Perspektive

Cognitive Complexity ist für LLM-Agenten aus zwei Gründen besonders relevant:

1. **Verschachtelung als Attention-Problem:** Tiefe Schachtelungen zwingen LLMs, den gesamten "Stack" der umgebenden Bedingungen für jede innere Zeile mitzuführen. Bei CogC=20 muss das Modell an einer inneren Zeile gleichzeitig mehrere äußere Bedingungen als Kontext berücksichtigen — das ist exakt das, was die „branching-induced divergence"-Theorie (Xie et al. 2026) als Hauptursache für LLM-Fehler beschreibt.

2. **Bessere Näherung für LM-CC:** Die von Xie et al. (2026) vorgeschlagene LM-CC-Metrik (die wirkliche LLM-Schwierigkeitsmetrik) kann in AiNetLinter nicht direkt berechnet werden. Cognitive Complexity ist aber eine deutlich bessere Näherung als CC, da beide Metriken Verschachtelung überproportional gewichten.

Die Kombination M04 (CC ≤ 12) + M05 (CogC ≤ 15) schützt komplementär: M04 begrenzt die Pfadanzahl (Testbarkeit), M05 begrenzt die Verschachtelungstiefe (Verständlichkeit). Beide zusammen decken die wesentlichen Komplexitätsdimensionen ab.

(Ableitung: Direkte Messung CogC-Wert → LLM-Fehlerrate existiert nicht; die Verbindung läuft über die Analogie zu menschlicher Verständlichkeit und die „branching-induced divergence"-Theorie.)

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Die Transformer-Architektur verarbeitet Code sequentiell mit begrenzter lokaler Aufmerksamkeit. Tiefe Verschachtelung erhöht die Anzahl der relevanten Kontext-Tokens, die das Modell beim Lesen einer inneren Zeile berücksichtigen muss. Dieser Mechanismus ist architektonisch verankert und bleibt auch bei größeren Modellen relevant, solange Autoregression die Basis ist.

---

## Empfehlung

**Aktion:** Wert beibehalten (15)  
**Begründung:** CogC ≤ 15 ist der empirisch stärkste verfügbare Standard für Code-Verständlichkeit; der aktuelle Wert entspricht exakt dem SonarQube-Default und der besten empirischen Empfehlung.

---

## Quellen

- Campbell, G.A. (2018): „Cognitive Complexity: A New Way of Measuring Understandability" — SonarSource Whitepaper, sonarsource.com/docs/CognitiveComplexity.pdf
- Araújo et al. (2020): „An Empirical Validation of Cognitive Complexity as a Measure of Source Code Understandability" — arXiv:2007.12520
- Xie et al. (2026): „Rethinking Code Complexity Through the Lens of Large Language Models" — arXiv:2601.20404 / ICML 2026
- Palomba et al. (2018): „On the diffuseness and the impact on maintainability of code smells" — ICSE 2018
