# CompoundSuppressions (M17)

**Kategorie:** Numerische Metrik (Meta-Feature)  
**Aktueller Wert:** 1 aktive Suppression: MaxMethodLineCount → 150 wenn CC≤3 ∧ CogC≤5 | **Severity:** konfigurierbar per SeverityOverride | **Status:** Aktiv  
**Paper-Cluster genutzt:** A, B, C

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** Der Mechanismus ist konzeptionell korrekt — kontextabhängige Grenzwerte sind theoretisch präziser als absolute Schwellenwerte —, aber er erhöht die Regelkomplexität für LLM-Agenten signifikant (Regeln-über-Regeln), führt zu schwer erklärbaren Verletzungs-Reports und sollte auf die aktuelle einzelne Suppression begrenzt werden statt als allgemeines Erweiterungskonzept behandelt zu werden.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 0 | 0 Suppressions = Feature effektiv deaktiviert; der aktuelle Use Case (MaxMethodLineCount bei low-CC-Methoden) ist sinnvoll |
| **Empfehlung (beste Evidenz)** | 1–2 | Maximal 1–2 Suppressions; jede weitere erhöht die mentale Last für Entwickler und Agenten überproportional |
| **Obergrenze (Nutzen geht verloren)** | 3 | Ab 3 Suppressions ist das Regelwerk nicht mehr intuitiv erfassbar; Agenten können nicht mehr zuverlässig vorhersagen, welcher Grenzwert in welchem Kontext gilt |
| **Aktueller Wert** | 1 | Angemessen — die einzige aktive Suppression ist der sinnvollste denkbare Anwendungsfall |

---

## Wissenschaftliche Grundlage

Das zugrunde liegende Prinzip von CompoundSuppressions — dass die Bedeutung einer Metrik vom Kontext anderer Metriken abhängt — ist empirisch gestützt:

**Cluster A (Komplexität):** Die LM-CC-Forschung (Xie et al. 2026) zeigt, dass Pfaddivergenz und Komplexität interagieren: Eine lange Methode mit CC=1 (z. B. eine reine Datentransformation oder ein Dispatcher) ist für LLMs fundamental anders als eine 60-zeilige Methode mit CC=12. Absolute Zeilengrenzen ohne Komplexitätskontext sind daher tatsächlich unterspezifiziert.

**Cluster B (Dateigrößen):** Kochhar et al. (2022) zeigt, dass der Sweetspot für Methodenlänge kontextabhängig ist — einfache Methoden können länger sein als komplexe. Dies stützt den Kerngedanken hinter der einzigen aktuellen Suppression (MaxMethodLineCount → 150 wenn CC≤3 ∧ CogC≤5).

**Problem der Regelkomplexität:** Cluster C belegt jedoch, dass Kontext-Verwirrung die häufigste Fehlerursache bei LLM-Agenten ist. CompoundSuppressions erhöhen diese Kontext-Verwirrung, indem sie das Regelwerk meta-ebenenartig erweitern. Ein Agent, der eine Regelverletzung meldet oder behebt, muss nun auch prüfen: „Gilt die Standard-Regel hier, oder greift eine Compound-Suppression?" Dies ist ein nicht-triviales Reasoning-Problem, das bei größerer Anzahl von Suppressions exponentiell schwieriger wird.

**Keine Vergleichstools:** SonarQube, ESLint, StyleCop und NDepend bieten keinen äquivalenten Mechanismus. Stattdessen lösen diese Tools das Problem durch mehrere unabhängige Regeln mit unterschiedlichen Thresholds oder über Profil-Hierarchien.

## KI-Agenten-Perspektive

Für einen LLM-Agenten, der AiNetLinter-Ausgaben interpretiert, ist ein CompoundSuppression-Effekt schwer zu erklären. Wenn eine Methode 120 Zeilen hat und kein Fehler gemeldet wird (weil CC=2 ≤ 3 und CogC=4 ≤ 5), aber der Agent weiß, dass MaxMethodLineCount = 60, dann ist das Ausbleiben der Fehlermeldung für den Agenten ohne Kenntnis der Suppression unerklärbar. Dies kann dazu führen, dass Agenten das Linter-Feedback falsch interpretieren oder ignorieren.

Die aktuelle einzige Suppression (MaxMethodLineCount bei low-CC-Methoden) ist gut dokumentierbar: Sie entspricht dem Dispatcher-Pattern und ist für einen Agenten mit `--describe-rule MaxMethodLineCount` erklärbar. Aber jede weitere Suppression erhöht diese Erklärbarkeits-Last.

## Zeitliche Einordnung

**Grundlagenstabilität:** Modellgeneration-spezifisch

Bessere Modelle können komplexere Regelwerke verstehen; das Komplexitätsproblem bleibt aber strukturell bestehen. Der Mechanismus selbst ist nicht technisch obsolet, aber sein Nutzen hängt von der Anzahl der Suppressions ab.

---

## Empfehlung

**Aktion:** Beibehalten, aber keine weiteren Suppressions hinzufügen  
**Begründung:** Die aktuelle Konfiguration (1 Suppression für MaxMethodLineCount bei low-CC-Methoden) ist der optimal valide Anwendungsfall des Mechanismus und empirisch gut begründbar; das Hinzufügen weiterer Suppressions würde die Erklärbarkeit des Regelwerks für LLM-Agenten und Entwickler signifikant verschlechtern, ohne proportionalen Nutzen zu bieten.

---

## Quellen

- Xie et al. (2026) — Rethinking Code Complexity Through the Lens of Large Language Models — arXiv:2601.20404
- Kochhar et al. / arXiv (2022) — An Empirical Study on Maintainable Method Size in Java — arXiv:2205.01842
- Campbell, G.A. (2018) — Cognitive Complexity: A New Way of Measuring Understandability — SonarSource — https://www.sonarsource.com/docs/CognitiveComplexity.pdf
- Liu, N. F. et al. (2023) — Lost in the Middle: How Language Models Use Long Contexts — arXiv:2307.03172
- Empirical Agent Framework Studies (2024–2026) — Inside the Scaffold — arXiv:2604.03515
- McCabe, T.J. (1976) — A Complexity Measure — IEEE Transactions on Software Engineering
