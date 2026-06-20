# MaxPublicMembersPerType (M13)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 15 | **Severity:** error | **Status:** Aktiv (Ausnahmen: Extensions, Mapper, Constants, Config, Args)  
**Paper-Cluster genutzt:** E, C

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Eine große öffentliche API-Oberfläche korreliert mit hoher ausgehender Kopplung (CBO), die empirisch als stärkster Defektprädiktor unter den CK-Metriken gilt; der Grenzwert 15 ist konservativ genug um echte God-Class-Kandidaten zu treffen, ohne typische Service-Klassen zu beeinträchtigen.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 8 | Darunter werden auch sauber strukturierte Klassen getroffen; typische CRUD-Services haben 6–10 public Members |
| **Empfehlung (beste Evidenz)** | 12–15 | Ableitung aus CBO-Prinzipien; kein direktes empirisches Paper; Microsoft-DI-Guideline nennt 3–4 Abhängigkeiten als Grenze — öffentliche Members sind weniger restriktiv |
| **Obergrenze (Nutzen geht verloren)** | 20 | Ab 20 public Members handelt es sich nahezu sicher um eine God Class (SRP-Verletzung) |
| **Aktueller Wert** | 15 | Angemessen — obere Empfehlungsgrenze |

---

## Wissenschaftliche Grundlage

Es gibt keine dedizierte Studie zu „MaxPublicMembersPerType" als isolierter Metrik. Die Evidenz leitet sich aus den CK-Metriken ab:

**Kopplung (CBO) als Hauptprädiktor:** Basili et al. (1996) und Subramanyam & Krishnan (2003) belegen übereinstimmend, dass CBO (Coupling Between Objects) der stärkste Defektprädiktor unter den CK-Metriken ist. Al-Subaihin & Sarro (2019) zeigen, dass insbesondere die efferente Kopplung (ausgehende Abhängigkeiten) für Defektvorhersage relevant ist. Eine große öffentliche API-Oberfläche ist eine direkte Ursache für hohe CBO: Jeder Aufrufer, der auf diese Members zugreift, koppelt sich an den Typ.

**LCOM als Kohäsionsindikator:** Klassen mit vielen öffentlichen Members, die voneinander unabhängige Funktionalitäten abbilden, zeigen typischerweise hohe LCOM-Werte (geringe Kohäsion). Die LCOM-Forschung (Pedersen et al.) ist methodisch umstritten, aber der Richtungseffekt ist konsistent: mehr öffentliche Members → tendenziell geringere Kohäsion.

**OpenClassGen (2025/2026):** Das Datensatz-Paper belegt direkt, dass hohe Kopplungswerte (CBO) bei LLM-Generierungen zu mehr „Project Context Conflicts" führen — also zu Halluzinationen über Methoden-Signaturen und fehlerhafte API-Annahmen.

## KI-Agenten-Perspektive

Für LLM-Agenten hat eine große öffentliche API-Oberfläche zwei negative Effekte:

1. **Orientierungsaufwand:** Ein Agent, der eine Klasse mit 30 öffentlichen Methoden versteht, benötigt deutlich mehr Kontext-Tokens um die richtigen Members zu identifizieren. Dies erhöht den effektiven Footprint (vgl. M14).

2. **Halluzinations-Risiko:** Der Empirical Agent Framework Study (2024–2026) zufolge sind Kontext-Fehler (falsche Methodennamen, falsche Signaturen) die häufigste Fehlerursache bei Agenten — besonders in großen Klassen, wo die korrekte Methode nicht sofort erkennbar ist. OpenClassGen (2025/2026) bestätigt direkt den Zusammenhang zwischen hoher CBO und LLM-Fehlerrate.

Die Ausnahmen für Extensions, Mapper, Constants, Config und Args sind sachgerecht: Diese Klassen haben strukturell viele Members, aber keine SRP-Verletzung (Extensions sind Utility-Collections, Constants sind per Design umfangreich).

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Große API-Oberflächen werden auch bei zukünftigen Modellen ein Navigations- und Halluzinations-Risiko darstellen; das Problem ist strukturell in der Aufmerksamkeitsverteilung von Transformer-Architekturen verankert.

---

## Empfehlung

**Aktion:** Wert beibehalten (15)  
**Begründung:** Der Grenzwert entspricht der oberen Grenze vertretbarer Klassen-API-Größe; die konfigurierten Ausnahmen sind korrekt; eine Absenkung auf 12 könnte legitime Service-Klassen treffen und sollte nur projektspezifisch über PathOverrides erfolgen.

---

## Quellen

- Basili, Briand & Melo (1996) — A Validation of Object-Oriented Design Metrics as Quality Indicators — IEEE TSE
- Subramanyam & Krishnan (2003) — Empirical Analysis of CK Metrics for Object-Oriented Design Complexity — https://www.researchgate.net/publication/3188321
- Al-Subaihin & Sarro (2019) — A Comparison and Evaluation of Variants in the CBO Metric — https://www.sciencedirect.com/science/article/abs/pii/S0164121219300305
- Concordia University / arXiv (2025/2026) — OpenClassGen: A Large-Scale Dataset for Class-Level Code Generation — arXiv:2511.00872
- Liu et al. (2024/2025) — LLM Hallucinations in Practical Code Generation — arXiv:2409.20550
