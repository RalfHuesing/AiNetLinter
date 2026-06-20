# MaxBoolParameterCount (M11)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 1 | **Severity:** error | **Status:** Aktiv (private Methoden und Try*-Methoden ausgenommen)  
**Paper-Cluster genutzt:** D, F

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Boolean-Parameter erzwingen bifurkierende Methodenlogik (SRP-Verletzung) und exponenzieren verborgene Pfadkombinationen bei mehrfachem Einsatz; der aktuelle Grenzwert 1 ist der industriell akzeptierte Konsens und für LLM-Agenten besonders wertvoll, da er implizit branching-induzierte Divergenz minimiert.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 0 | Vollständiges Verbot würde legitime Fälle wie `async/await`-Flags oder `includeInactive: true` pauschal verbieten |
| **Empfehlung (beste Evidenz)** | 1 | Industriekonsens: genau 1 Bool-Parameter ist tolerierbar; ab 2 multiplizieren sich die versteckten Pfade (2^2 = 4 versteckte Kombinationen) |
| **Obergrenze (Nutzen geht verloren)** | 2 | Ab 2 Bool-Parametern entstehen 4 potenzielle versteckte Pfade; Fowler empfiehlt hier Refactoring |
| **Aktueller Wert** | 1 | Angemessen — entspricht dem Konsens |

---

## Wissenschaftliche Grundlage

Die „Boolean Trap" ist ein weit verbreiteter Konsens aus der Clean-Code-Literatur, aber keine dedizierte empirische Studie mit kontrollierten Experimenten existiert. Fowler (1999/2018) listet „Flag Arguments" als Refactoring-Kandidat: Ein boolean-Flag signalisiert, dass eine Funktion zwei Aufgaben erledigt (SRP-Verletzung). Bei mehreren boolean-Parametern multiplizieren sich die versteckten Pfade exponentiell (n Booleans → 2^n Kombinationen), was sowohl das Testen als auch das Lesen erheblich erschwert.

Palomba et al. (2017) belegt empirisch, dass Code-Smells der Kategorie „Complex Class" und „Long Method" — die eng mit Boolean-Traps korrelieren — signifikant zur Change-Proneness beitragen (+28% bzw. +21%). Die Boolean Trap selbst ist als separater Smell nicht gemessen, aber als Teil des Smell-Musters belegt.

Vartolomei & Craciun (arXiv 2024) bestätigen, dass Long Parameter Lists weiterhin unter den häufig identifizierten und mit höherer Bug-Dichte korrelierten Smells in modernen Projekten sind.

## KI-Agenten-Perspektive

Für LLM-Agenten ist die Boolean Trap besonders problematisch: Wenn ein Agent eine Methode mit zwei Bool-Parametern aufruft, muss er aus dem Kontext ableiten, welche Kombination (`true, false` vs. `false, true` vs. `true, true`) die korrekte ist. Ohne semantische Namen für die Bool-Parameter (die in Funktionssignaturen oft fehlen) ist dies ein klassischer Pfad zu falschen Annahmen. Die Studie von Xie et al. (2026, Cluster A) zeigt, dass „branching-induced divergence" die Hauptquelle für LLM-Fehler bei Code-Verständnis ist — boolean-Flags in Signaturen erzeugen genau diese Divergenz ohne syntaktisch sichtbare Verzweigung im Aufruf.

Die Ausnahmen für private Methoden und Try*-Methoden sind korrekt konfiguriert: `bool TryParse(string s, out T value)` ist ein anerkanntes Muster und kein Smell.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Das Problem ist struktureller Natur: Verborgene Pfadkombinationen durch Bool-Parameter werden auch bessere Modelle vor Interpretationsprobleme stellen, solange Methodensignaturen ohne Argumentnamen sichtbar sind (z. B. `Process(data, true, false)` aus dem Aufrufer).

---

## Empfehlung

**Aktion:** Wert beibehalten (1)  
**Begründung:** Der Grenzwert entspricht dem industriellen Konsens und ist für LLM-Agenten besonders sinnvoll; die Ausnahmen für private Methoden und Try*-Pattern sind korrekt gewählt.

---

## Quellen

- Fowler, M. & Beck, K. (1999/2018) — Refactoring: Improving the Design of Existing Code — Addison-Wesley
- Palomba, F. et al. (2017) — On the Diffuseness and the Impact on Maintainability of Code Smells — Empirical Software Engineering (Springer) — https://link.springer.com/article/10.1007/s10664-017-9535-z
- Vartolomei & Craciun (2024) — On the Prevalence, Evolution, and Impact of Code Smells in Simulation Modelling Software — arXiv:2409.03957
- Xie et al. (2026) — Rethinking Code Complexity Through the Lens of Large Language Models — arXiv:2601.20404
