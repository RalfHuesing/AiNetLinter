# MaxCyclomaticComplexity (M04)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 12 | **Severity:** error | **Status:** Aktiv  
**Paper-Cluster genutzt:** A, C, F, H

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Der Grenzwert von 12 ist empirisch vertretbar und liegt im Korridor zwischen McCabes Original-Empfehlung (10) und dem SonarQube-Default (15); er sollte beibehalten werden, ergänzt durch das Wissen, dass CC für LLMs ein unvollständiger Proxy ist — die eigentliche Schutzwirkung entfaltet sich durch das Zusammenspiel mit M05 (Cognitive Complexity).

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 5 | Unter CC=5 werden selbst einfache Switch-Statements oder typische Validierungslogiken zur Violation; kontraproduktive Fragmentierung |
| **Empfehlung (beste Evidenz)** | 10–12 | McCabe (1976): Grenzwert 10 als Risikogrenze; SonarQube: 15 als Default. 12 liegt im bewährten Mittelfeld und entspricht dem Industriekonsens für risikobehaftete Methoden |
| **Obergrenze (Nutzen geht verloren)** | 15 | Ab CC=15 beginnt der Bereich, in dem empirische Studien konsistent erhöhte Defektdichten zeigen; darüber ist die Schutzwirkung des Grenzwerts gesichert |
| **Aktueller Wert** | 12 | Angemessen — im validen Korridor |

---

## Wissenschaftliche Grundlage

McCabe (1976) definiert CC=10 als Risikogrenze: Methoden mit CC > 10 gelten als schwer testbar und zeigen empirisch 2–3× höhere Defektdichten. Dieser Wert wurde zum De-facto-Standard in der Industrie, obwohl das Originalpaper sich auf Testbarkeit fokussierte, nicht auf Wartbarkeit.

Shepperd (1988) kritisiert CC grundlegend: Die hohe Korrelation mit LOC (r ≈ 0,9) bedeutet, dass CC kaum eigenständige Information über LOC hinaus liefert. Basili et al. (1996) fanden sogar, dass in manchen Kontexten kleinere Methoden mit CC=1 fehleranfälliger sind als erwartet. Diese Kritik relativiert die Bedeutung eines exakten CC-Grenzwerts.

Jaber et al. (2018) zeigen, dass Halstead Volume und CC hochgradig korrelieren (r ≈ 0,904), also weitgehend redundante Information liefern. Das bestätigt Shepperds Kritik. Trotzdem bleibt CC industriell unverzichtbar, weil es operativ einfach zu berechnen und zu kommunizieren ist.

Die neue Metrik LM-CC (Xie et al. 2026) zeigt, dass CC die wirkliche Verarbeitungshürde für LLMs nur unzuverlässig abbildet: Die entscheidende Größe für Agenten ist die „branching-induced divergence" — an Verzweigungen steigt die Entropie des Modells. CC korreliert mit dieser Pfaddivergenz, aber nicht perfekt, da Switch-Statements mit vielen Cases anders gewichtet werden als tiefe if-else-Schachtelungen.

## KI-Agenten-Perspektive

Für LLM-Agenten ist CC ein nützlicher, aber unvollständiger Proxy. Die direkte Relevanz ergibt sich aus der „branching-induced divergence" (Xie et al. 2026): Jede Verzweigung in einer Methode (if, switch, catch, Ternary) erhöht die Anzahl der logischen Pfade, die ein Autoregressive Transformer-Modell beim Lesen verfolgen muss. Bei CC=12 hat eine Methode theoretisch 12 linear unabhängige Pfade — das ist deutlich mehr, als ein Modell zuverlässig im Arbeitsgedächtnis halten kann.

Empirisch zeigen API-Komplexitätsstudien (arXiv:2601.00268, 2025): Jede zusätzliche Komplexitätsdimension reduziert die LLM-Agenten-Performance um durchschnittlich 12 %; kumulative Komplexität reduziert sie um bis zu 63 %. CC=12 bei gleichzeitig hoher Cognitive Complexity und langer Methode ist daher besonders toxisch für Agenten.

(Ableitung: Die direkte Kausalverknüpfung CC-Wert → LLM-Fehlerrate in C# ist nicht direkt gemessen; sie folgt aus der „branching-induced divergence"-Theorie.)

### Bekannte False Positives / Ausnahmen

- **Null-Coalescing-Initializer:** Methoden mit `return this with { A = o.A ?? A, … }` oder `return new T { A = o.A ?? A, … }` erzeugen einen hohen McCabe-Wert, sind aber semantisch trivial (flacher Kontrollfluss). Mit `ExcludeNullCoalescingInitializerComplexity: true` (Standard) werden solche Methoden ausgenommen.


## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Die mathematische Grundlage — Autoregressiver Transformer erhöht Entropie an Verzweigungen — ist in der Architektur verankert und unabhängig von Modellgenerationen. Auch menschliche Kognitionsgrenzen bei hoher Verzweigungsdichte (Basis von McCabes ursprünglicher Theorie) bleiben stabil. Der CC-Grenzwert bleibt dauerhaft relevant.

---

## Empfehlung

**Aktion:** Wert beibehalten (12)  
**Begründung:** Der Wert 12 liegt im empirisch validen Korridor (10–15); der `ComplexityNearMissTolerance: 1`-Puffer ist ein sinnvoller Pragmatismus, der Methoden mit CC=13 als Warnzeichen, nicht als harten Fehler behandelt.

---

## Quellen

- McCabe, T.J. (1976): „A Complexity Measure" — IEEE Transactions on Software Engineering
- Shepperd, M. (1988): „A Critique of Cyclomatic Complexity as a Software Metric" — cs.du.edu/~snarayan/sada/teaching/COMP3705/lecture/p1/cycl-1.pdf
- Jaber et al. (2018): „Evaluation of Halstead and Cyclomatic Complexity Metrics in Measuring Defect Density" — IEEE Xplore, DOI:10.1109/ICAIET.2018.8447959
- Palomba et al. (2018): „On the diffuseness and the impact on maintainability of code smells" — ICSE 2018
- Xie et al. (2026): „Rethinking Code Complexity Through the Lens of Large Language Models" — arXiv:2601.20404 / ICML 2026
- arXiv:2601.00268 (2025): „Beyond Perfect APIs: API Complexity and LLM Agent Performance"
