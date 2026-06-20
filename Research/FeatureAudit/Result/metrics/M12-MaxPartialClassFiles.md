# MaxPartialClassFiles (M12)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 2 | **Severity:** error | **Status:** Aktiv (AggregatePartialClassLineCount: false)  
**Paper-Cluster genutzt:** D, C, E

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Partial Classes, die über mehr als zwei Dateien verteilt sind, erzwingen bei LLM-Agenten das Laden mehrerer Dateien für ein einziges Typ-Verständnis und erhöhen damit den Kontext-Footprint sowie das Risiko inkonsistenter Edits; der Grenzwert 2 entspricht dem C#-Community-Konsens und sollte beibehalten werden.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 2 | Wert 1 würde das Feature de facto abschalten (alle Partial Classes verboten, da min. 2 Teile) |
| **Empfehlung (beste Evidenz)** | 2 | NDepend / C#-Konsens: Max. 2 Dateien; 3+ ist Antipattern-Signal für zu viele Verantwortlichkeiten |
| **Obergrenze (Nutzen geht verloren)** | 3 | NDepend-Empfehlung nennt 2–3 als akzeptable Grenze; ab 4 kein sinnvoller Anwendungsfall außerhalb von Code-Generierung |
| **Aktueller Wert** | 2 | Angemessen — entspricht dem Community-Konsens |

---

## Wissenschaftliche Grundlage

Es gibt keine kontrollierte empirische Studie zu „MaxPartialClassFiles" als isolierter Metrik. Die Evidenz leitet sich aus zwei Quellen ab:

**C#-Community-Konsens (NDepend Blog, Patrick Smacchia):** Legitimate Hauptanwendungsfälle für Partial Classes sind (1) die Code-Generator-Erweiterung (z. B. EF-Migrations, WinForms Designer.cs) und (2) die Aufteilung sehr großer Klassen in logische Abschnitte zur Teamarbeit. Drei oder mehr Partial-Dateien einer Klasse sind ein klares Antipattern-Signal: Die Klasse hat zu viele Verantwortlichkeiten und sollte aufgeteilt werden statt über Dateien verteilt zu werden.

**Architekturmetriken (Cluster E):** Hohe Kopplung (CBO) und geringe Kohäsion (LCOM) korrelieren empirisch mit erhöhter Defektdichte (Basili et al. 1996, Subramanyam & Krishnan 2003). Partial Classes über viele Dateien verteilt sind strukturell äquivalent zu einer einzelnen Klasse mit hohem CBO-Risiko, da sämtliche Teile zu derselben Klasse kompiliert werden. Die Fragmentierung macht dieses Kopplungsrisiko nur schwerer sichtbar.

## KI-Agenten-Perspektive

Für LLM-Agenten stellt eine Partial Class, die über drei oder mehr Dateien verteilt ist, ein direktes Kontext-Problem dar: Um den vollständigen Typ zu verstehen, müssen alle Dateifragmente geladen werden. Dies erhöht den effektiven Kontext-Footprint (vgl. M14) und erzeugt das Risiko, dass ein Agent nur einen Teil der Klasse sieht und dadurch Methoden oder Properties übersieht — ein klassisches Szenario für Project Context Conflicts (Liu et al. 2024/2025). Bei M12 = 2 ist das Risiko beherrschbar: Der Agent erwartet maximal eine zweite Datei (typisch: Designer.cs oder Generated.cs).

Durch `AggregatePartialClassLineCount: false` werden die Zeilenlimits (M01, M02) pro Datei-Fragment gemessen — das ist korrekt für generierte Dateien, die nicht manuell editiert werden. Für manuell erstellte Partial-Dateien könnte eine aggregierte Zählung sinnvoller sein, aber dies ist ein separates Konfigurationsproblem.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Partial Classes sind ein C#-spezifisches Feature und werden nicht durch bessere Modelle obsolet. Das Navigationsproblem (mehrere Dateien für einen Typ laden) bleibt strukturell.

---

## Empfehlung

**Aktion:** Wert beibehalten (2)  
**Begründung:** Entspricht dem C#-Community-Konsens; für Code-Generierungs-Szenarien (Designer.cs, Generated.cs) ist 2 ausreichend; mehr als 2 Partial-Dateien für manuell verwalteten Code ist ein klares Refactoring-Signal.

---

## Quellen

- NDepend Blog, Patrick Smacchia — When Is It Okay to Use a C# Partial Class? — https://blog.ndepend.com/okay-use-c-partial-class/
- Basili, Briand & Melo (1996) — A Validation of Object-Oriented Design Metrics as Quality Indicators — IEEE TSE
- Subramanyam & Krishnan (2003) — Empirical Analysis of CK Metrics for Object-Oriented Design Complexity — https://www.researchgate.net/publication/3188321
- Liu et al. (2024/2025) — LLM Hallucinations in Practical Code Generation — arXiv:2409.20550
- Empirical Agent Framework Studies (2024–2026) — Inside the Scaffold — arXiv:2604.03515
