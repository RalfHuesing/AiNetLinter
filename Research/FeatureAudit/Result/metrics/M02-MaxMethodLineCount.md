# MaxMethodLineCount (M02)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 60 (relaxed: 150 via CompoundSuppression wenn CC≤3 und CogC≤5) | **Severity:** error (warning bei CompoundSuppression) | **Status:** Aktiv  
**Paper-Cluster genutzt:** A, B, C

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Der Grenzwert von 60 Zeilen ist empirisch gut vertretbar — er liegt zwischen dem optimalen Labor-Wert (24 SLOC) und der praktischen Toleranzgrenze (80–100 Zeilen) und schützt Agenten vor unüberschaubaren Methoden ohne kontraproduktive Fragmentierung.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 20 | Unter 20 Zeilen erzwingt zu viele Hilfsmethoden, was die Navigationskomplexität für Agenten erhöht |
| **Empfehlung (beste Evidenz)** | 50–60 | Kochhar et al. (2022): empirischer Sweetspot ≤ 24 SLOC; Praxis-Heuristik: ≤ 50 Zeilen vermeidet kognitiven Scrollbedarf; 60 ist ein praktikabler Kompromiss |
| **Obergrenze (Nutzen geht verloren)** | 100 | Ab 100 Zeilen übersteigt eine Methode typischerweise die Bildschirm-Höhe und das Arbeitsgedächtnis; die Schutzwirkung der Regel geht verloren |
| **Aktueller Wert** | 60 | Angemessen — liegt im vertretbaren Praxis-Korridor |

---

## Wissenschaftliche Grundlage

Kochhar et al. (2022) liefern die stärkste empirische Grundlage für Methodengrößen: Methoden mit ≤ 24 SLOC zeigen statistisch signifikant bessere Wartbarkeit. Methoden mit über 50 Zeilen führen zu kognitiver Überlastung durch das notwendige Scrollen und den Verlust des lokalen Kontexts. Der Wert 24 SLOC ist jedoch als Labor-Optimum zu verstehen — in realen C#-Projekten mit LINQ-Chains, Pattern Matching und Async-Strukturen kann eine Methode mit 40–60 Zeilen semantisch kompakt bleiben.

Palomba et al. (2018) identifizieren „Long Method" als einen der sieben schädlichsten Code Smells für Wartbarkeit. Fowler & Beck (1999/2018) sehen lange Methoden als primäres Refactoring-Ziel. Beide Quellen liefern keine Zahlenwerte, bestätigen aber die Richtung.

Die CompoundSuppression-Ausnahme (150 Zeilen bei CC≤3 und CogC≤5) ist ingenious: Sie erlaubt datengefüllte, aber strukturell simple Methoden (z. B. Mapper, Konfigurationsblöcke) ohne die eigentliche Schutzeigenschaft der Regel zu unterlaufen. Das ist empirisch gut begründet, da die Schädlichkeit langer Methoden mit ihrer Komplexität korreliert — eine 120-Zeilen-Methode mit CC=1 (reine Datenzuweisung) ist deutlich weniger problematisch als eine 60-Zeilen-Methode mit CC=15.

## KI-Agenten-Perspektive

Für LLM-Agenten sind lange Methoden aus zwei Gründen problematisch:

1. **Kontextfenster-Belastung:** Eine Methode mit 100+ Zeilen belegt einen erheblichen Teil des effektiven Kontextfensters, wenn der Agent sie vollständig laden muss. Bei Dateien mit mehreren langen Methoden wird die U-förmige Aufmerksamkeitskurve (Liu et al. 2023) besonders schädlich — Code in der Mitte der Methode wird schlechter verarbeitet.

2. **Verzweigungs-induzierte Divergenz:** Xie et al. (2026) zeigen, dass die wirkliche LLM-Schwierigkeit nicht die Länge, sondern die Pfaddivergenzen (branching-induced divergence) ist. Eine 60-Zeilen-Methode mit CC=12 ist für Agenten gefährlicher als eine 100-Zeilen-Methode mit CC=2. Die Kombination aus Längenbeschränkung (M02) und Komplexitätsbeschränkung (M04, M05) ist daher komplementär: M02 begrenzt den Kontext-Fußabdruck, M04/M05 begrenzen die Pfaddivergenzen.

(Ableitung: Kausale Verknüpfung dieser Metriken mit LLM-Fehlerraten ist nicht direkt gemessen, folgt aber aus den etablierten „Lost in the Middle"- und „branching-induced divergence"-Erkenntnissen.)

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Die kognitive Überlastung durch zu lange Methoden ist für Menschen strukturell bedingt. Der „Lost in the Middle"-Effekt für LLMs ist ebenfalls in der Transformer-Architektur verankert. Beide Grundlagen sind unabhängig von Modellgenerationen stabil — selbst stärkere Modelle profitieren von präzise abgegrenzten Methoden als Analyse-Einheiten.

---

## Empfehlung

**Aktion:** Wert beibehalten (60 / 150 via CompoundSuppression)  
**Begründung:** Der aktuelle Wert ist empirisch gut positioniert; die CompoundSuppression-Ausnahme ist logisch korrekt und schützt vor falsch-positiven Verletzungen bei strukturell simplen, aber notwendigerweise langen Methoden.

---

## Quellen

- Kochhar et al. (2022): „An Empirical Study on Maintainable Method Size in Java" — arXiv:2205.01842
- Palomba et al. (2018): „On the diffuseness and the impact on maintainability of code smells" — ICSE 2018, fpalomba.github.io/pdf/Journals/J9.pdf
- Fowler & Beck (1999/2018): „Refactoring: Improving the Design of Existing Code" — Addison-Wesley
- Liu et al. (2023): „Lost in the Middle: How Language Models Use Long Contexts" — arXiv:2307.03172
- Xie et al. (2026): „Rethinking Code Complexity Through the Lens of Large Language Models" — arXiv:2601.20404
