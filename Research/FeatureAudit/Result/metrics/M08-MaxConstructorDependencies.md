# MaxConstructorDependencies (M08)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 5 | **Severity:** error | **Status:** Aktiv  
**Paper-Cluster genutzt:** E, C, H

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Der Grenzwert von 5 Konstruktor-Abhängigkeiten ist gut vertretbar — er liegt leicht über dem Microsoft-Richtwert (3–4), berücksichtigt aber die Praxis realer .NET-Services und ist durch die Ausnahme für Infrastruktur-Typen (ILogger, IOptions usw.) pragmatisch kalibriert.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 3 | Unter 3 echte Abhängigkeiten wäre selbst ein einfacher Service mit Repository + Validator + Logger bereits eine Violation |
| **Empfehlung (beste Evidenz)** | 4–5 | Microsoft DI-Guidelines: 3–4 als Richtwert für SRP-Compliance; 5 erlaubt in der Praxis einen Schritt Spielraum ohne die Warnsignalwirkung zu verlieren |
| **Obergrenze (Nutzen geht verloren)** | 7 | Ab 7 Abhängigkeiten ist SRP definitiv verletzt; kein valides Geschäftsargument für 8+ |
| **Aktueller Wert** | 5 | Leicht über dem Richtwert (3–4), aber durch Infrastruktur-Typ-Ausnahmen kompensiert — angemessen |

---

## Wissenschaftliche Grundlage

CBO (Coupling Between Objects) aus den CK-Metriken (Chidamber & Kemerer 1994) ist der wissenschaftliche Hintergrund für diesen Grenzwert. Al-Subaihin & Sarro (2019) zeigen, dass efferente Kopplung (ausgehend — wie viele Klassen eine Klasse referenziert) der stärkste Defektprädiktor innerhalb der CBO-Familie ist. Konstruktor-Abhängigkeiten sind eine direkte Form efferenter Kopplung: Jede Abhängigkeit im Konstruktor ist ein externer Typ, von dem diese Klasse abhängig ist.

Basili et al. (1996) und Subramanyam & Krishnan (2003) validieren empirisch, dass CBO signifikant mit Defektdichte korreliert — stärker als DIT. Hohe Konstruktor-Abhängigkeiten sind also ein valider Defektprädiktor.

Microsoft .NET Design Guidelines und die DI-Community empfehlen explizit: Mehr als 3–4 Abhängigkeiten im Konstruktor ist ein Signal für SRP-Verletzung. Dies ist eine normative Empfehlung (kein kontrolliertes Experiment), aber von Microsoft offiziell dokumentiert und in der Community breit akzeptiert.

Die Ausnahme für Infrastruktur-Typen (`ILogger`, `IOptions`, `IConfiguration`, `IHttpClientFactory` etc.) ist methodisch korrekt: Diese Typen sind Framework-bedingt und keine Geschäftslogik-Abhängigkeiten. Sie erhöhen zwar technisch die Parameteranzahl, aber nicht die semantische Kopplung.

## KI-Agenten-Perspektive

Aus LLM-Perspektive sind viele Konstruktor-Abhängigkeiten aus zwei Gründen problematisch:

1. **Context-Navigation:** Ein Agent der eine Klasse instanziieren oder testen soll, muss alle Abhängigkeiten kennen und bereitstellen. Bei 8 Abhängigkeiten muss der Agent 8 weitere Typen auflösen — das ist ein erheblicher Kontext-Aufbau und erhöht die Wahrscheinlichkeit von „Project Context Conflicts"-Halluzinationen (Liu et al. 2024/2025).

2. **Muster-Reproduktion:** Empirische Studien (2024–2025) zeigen, dass KI-generierter Code dazu neigt, die vorgefundene Architektur zu kopieren. Ist eine Klasse mit 8 Abhängigkeiten vorhanden, fügen Agenten oft weitere Abhängigkeiten hinzu statt zu refaktorieren. Der Grenzwert verhindert dieses Muster.

OpenClassGen-Studien (Concordia University 2025/2026) zeigen, dass Klassen mit hoher CBO-Kopplung bei LLM-Generierungsaufgaben nachweislich häufiger zu Halluzinationen über Methoden-Signaturen und nicht-existente APIs führen.

(Evidenzebene: indirekte Ableitung — direkte empirische Bestätigung der Agenten-Performance-Korrelation für genau diesen Grenzwert fehlt.)

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

CBO als Defektprädiktor ist empirisch zeitstabil seit den 1990ern. Die kognitive und kontextuelle Last durch viele Abhängigkeiten bleibt sowohl für Menschen als auch für LLM-Agenten strukturell relevant, unabhängig von Modellgenerationen.

---

## Empfehlung

**Aktion:** Wert beibehalten (5, mit Infrastruktur-Typ-Ausnahmen)  
**Begründung:** Der Wert 5 ist ein guter Kompromiss zwischen dem Richtwert (3–4) und der Praxis realer .NET-Services; die Infrastruktur-Typ-Ausnahme stellt sicher, dass die Regel keine Framework-bedingte Kopplung bestraft.

---

## Quellen

- Chidamber & Kemerer (1994): „A Metrics Suite for Object Oriented Design" — IEEE Transactions on Software Engineering
- Basili, Briand & Melo (1996): „A Validation of Object-Oriented Design Metrics as Quality Indicators" — IEEE TSE
- Al-Subaihin & Sarro (2019): „A Comparison and Evaluation of Variants in the CBO Metric" — Science of Computer Programming, DOI:10.1016/j.scico.2019.04.004
- Microsoft .NET Dependency Injection Guidelines — learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines
- Liu et al. (2024/2025): „LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
- Concordia University / arXiv (2025/2026): „OpenClassGen: A Large-Scale Dataset for Class-Level Code Generation" — arXiv:2511.00872
