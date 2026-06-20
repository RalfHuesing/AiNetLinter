# MaxDirectoryChildren (M10)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 0 (= deaktiviert) | **Severity:** error (wenn aktiviert) | **Status:** Deaktiviert  
**Paper-Cluster genutzt:** B, C, E

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** Das Konzept ist plausibel — überfüllte Verzeichnisse erschweren die Navigation für Agenten —, aber es gibt keinen empirisch fundierten Schwellenwert, und M09 (Tiefe) deckt das strukturelle Problem bereits aus anderer Richtung ab; eine Aktivierung ohne validierten Grenzwert erzeugt mehr Rauschen als Nutzen.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 10 | Weniger als 10 Kinder pro Verzeichnis führt zu extremer Fragmentierung und erzwingt sinnlose Sub-Hierarchien |
| **Empfehlung (beste Evidenz)** | 20–30 | Industrie-Heuristik; kein Peer-reviewed-Grenzwert; deckt sich mit typischen Feature-Slice-Strukturen |
| **Obergrenze (Nutzen geht verloren)** | 50 | Ab 50 Einträgen ist ein Verzeichnis visuell nicht mehr überschaubar |
| **Aktueller Wert** | 0 (deaktiviert) | Deaktiviert, also kein Effekt |

---

## Wissenschaftliche Grundlage

Zur Metrik „maximale Verzeichnis-Kinder" gibt es keine dedizierte empirische Studie. Die einzige greifbare Referenz ist ein qualitativer Blog-Artikel von Sandor Dargo (2023), der festhält, dass „große Verzeichnisse (viele Kinder) ein visueller Indikator für Architekturprobleme sind" — ohne numerischen Beleg. Cluster E (Architekturmetriken) liefert keine Grenzwerte für diese Dimension.

Cluster B zeigt, dass ein Sweetspot bei 200–500 LOC pro Datei liegt und zu starke Fragmentierung (sehr viele kleine Dateien) das Kontextfenster von Agenten mit Pfad- und Importinformationen füllt. Daraus lässt sich ableiten, dass ein Verzeichnis mit sehr vielen Dateien ein Indikator für Über-Fragmentierung ist — aber eine direkte Kausalität zwischen „Anzahl Kinder" und Qualitätsproblemen ist nicht belegt.

Cluster C bestätigt, dass Kontext-Verwirrung die häufigste Fehlerursache bei LLM-Agenten ist und komplexe, fragmentierte Repositories zu erhöhter Fehlerrate führen. Die Verzeichnisbreite ist ein möglicher Beitrag dazu, aber schwerer zu belegen als die Verzeichnistiefe (M09).

## KI-Agenten-Perspektive

Für einen LLM-Agenten, der eine Codebase navigiert, ist ein Verzeichnis mit 80 Dateien ein praktisches Problem: Tool-Calls wie `ls` oder Dateisuche liefern lange Listen, die wertvolle Kontext-Token verbrauchen. Gleichzeitig löst M09 (Tiefe) das strukturelle Hauptproblem bereits — tiefe und breite Verzeichnisse entstehen oft gemeinsam. M10 würde nur einen orthogonalen Aspekt abdecken, der seltener isoliert auftritt.

## Zeitliche Einordnung

**Grundlagenstabilität:** Offen

Das Problem der Navigationserschwernis durch breite Verzeichnisse bleibt strukturell bestehen, aber es ist unklar, wie relevant es im Verhältnis zu M09 ist und ob bessere Agenten-Scaffoldings (smarte Dateisuche, Indexierung) das Problem wegoptimieren.

---

## Empfehlung

**Aktion:** Deaktiviert lassen  
**Begründung:** Keine empirisch gestützte Grenzwert-Empfehlung verfügbar; das strukturelle Architekturproblem ist bereits über M09 abgedeckt; eine Aktivierung mit willkürlichem Schwellenwert würde mehr False-Positives erzeugen als Probleme aufdecken.

---

## Quellen

- Sandor Dargo (2023) — How to Use Your Namespaces to Their Best — https://www.sandordargo.com/blog/2023/12/13/namespace-best-practices
- Liu, N. F. et al. (2023) — Lost in the Middle: How Language Models Use Long Contexts — arXiv:2307.03172
- Empirical Agent Framework Studies (2024–2026) — Inside the Scaffold: Agent Failure Taxonomy — arXiv:2604.03515
- Industry Code Benchmarks (2024) — Toward Gamification of Software Maintainability — arXiv:2412.06307
