# MaxSwitchArms (M15)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 10 | **Severity:** error | **Status:** Aktiv (Dispatcher-Pattern ausnehmbar)  
**Paper-Cluster genutzt:** A, F

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Switch-Arms erhöhen die Cyclomatic Complexity linear und die LLM-wahrgenommene Pfaddivergenz (LM-CC) noch stärker, da jeder Arm einen eigenständigen Kontrollfluss-Pfad einführt; der Grenzwert 10 ist gut gewählt und entspricht dem klassischen CC-Threshold von McCabe, wobei die Dispatcher-Ausnahme legitim ist.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 5 | Darunter werden legitime Dispatch-Strukturen (z.B. HTTP-Status-Codes, Enum-Verarbeitungen) pauschal verboten |
| **Empfehlung (beste Evidenz)** | 8–12 | McCabe-Threshold: CC ≤ 10 als Grenze für risikoarmen Code; bei reinen Switch-Statements ohne weitere Verschachtelung ist 10–12 das praktische Optimum |
| **Obergrenze (Nutzen geht verloren)** | 15 | Ab 15 Arms ist die kognitive und LLM-verarbeitende Last auch bei strukturell flachen Switches erheblich |
| **Aktueller Wert** | 10 | Angemessen — entspricht dem bewährten McCabe-Threshold |

---

## Wissenschaftliche Grundlage

Switch-Statements sind in der Code-Smell-Literatur als eigenständiger Smell klassifiziert (Fowler 1999/2018): Wiederholte Switch-Blöcke über denselben Typ deuten auf fehlende Polymorphie hin. Palomba et al. (2017) bestätigt empirisch, dass strukturelle Smells dieser Art zur Change-Proneness beitragen. Vartolomei & Craciun (2024) finden Switch-Statement-Smells unter den häufig identifizierten und mit Bug-Dichte korrelierten Mustern in modernen Projekten.

Direkt für die Armanzahl: Eine Switch-Expression oder ein Switch-Statement mit n Arms hat eine Cyclomatic Complexity von n (jeder Arm = ein unabhängiger linearer Pfad). McCabe empfahl CC ≤ 10 als sichere Grenze. Jaber et al. (2018) bestätigt, dass CC ein verlässlicher Prädiktor für Defektdichte ist, auch wenn die Korrelation teilweise durch LOC erklärt wird.

Xie et al. (2026) zeigt, dass LLMs bei Switches mit vielen Arms besonders unter „branching-induced divergence" leiden: Das Modell muss für jeden Arm eine separate Zustandstransaktion modellieren, was die Entropie stark erhöht. Dies ist bei Switch-Strukturen sogar ausgeprägter als bei if/else-Ketten, da alle Branches syntaktisch nebeneinander stehen und das Modell eine globale Wahrscheinlichkeitsverteilung über alle Arms aufrechterhalten muss.

## KI-Agenten-Perspektive

Für LLM-Agenten ist ein Switch mit 20 Arms ein praktisches Problem: Bei einer Änderungsaufgabe (z. B. „füge Case X hinzu") muss der Agent alle bestehenden Fälle berücksichtigen um Duplikate zu vermeiden, den korrekten Rückgabetyp zu inferieren und die Reihenfolge zu wahren. Mit 20 Arms steigt die Wahrscheinlichkeit, dass relevante Cases in der Mitte des Kontexts „verloren gehen" (Liu et al. 2023) — das „Lost in the Middle"-Problem tritt auch innerhalb eines einzelnen Switch-Blocks auf.

Die Dispatcher-Ausnahme ist sachgerecht: Ein Request-Dispatcher, der über 20 Befehlstypen routet, ist strukturell von einem Switch mit komplexer Business-Logik pro Arm verschieden. Bei Dispatcher-Patterns ist die Logik pro Arm typischerweise ein einzelner Methodenaufruf — die kognitive Last ist also proportional flach.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Die Grundlage (CC-Threshold, branching-induzierte Divergenz) ist strukturell in der Verarbeitungsweise sequenzieller Sprachmodelle verankert und wird durch bessere Modelle nicht eliminiert, nur moduliert.

---

## Empfehlung

**Aktion:** Wert beibehalten (10)  
**Begründung:** Entspricht dem McCabe-CC-Threshold und deckt sich mit der LM-CC-Forschung; die Dispatcher-Ausnahme ist korrekt konfiguriert und verhindert False-Positives bei legitimen Routing-Strukturen.

---

## Quellen

- McCabe, T.J. (1976) — A Complexity Measure — IEEE Transactions on Software Engineering
- Xie et al. (2026) — Rethinking Code Complexity Through the Lens of Large Language Models — arXiv:2601.20404
- Fowler, M. & Beck, K. (1999/2018) — Refactoring: Improving the Design of Existing Code — Addison-Wesley
- Palomba, F. et al. (2017) — On the Diffuseness and the Impact on Maintainability of Code Smells — Empirical Software Engineering — https://link.springer.com/article/10.1007/s10664-017-9535-z
- Vartolomei & Craciun (2024) — On the Prevalence, Evolution, and Impact of Code Smells — arXiv:2409.03957
- Jaber et al. (2018) — Evaluation of Halstead and Cyclomatic Complexity Metrics — IEEE Xplore — https://ieeexplore.ieee.org/document/8447959/
- Liu, N. F. et al. (2023) — Lost in the Middle — arXiv:2307.03172
