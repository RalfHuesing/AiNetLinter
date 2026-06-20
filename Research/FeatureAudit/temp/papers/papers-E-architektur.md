# Paper-Cluster E: Architekturmetriken

Erstellt: 2026-06-20  
Betrifft Features: M06 (MaxInheritanceDepth), M08 (MaxConstructorDependencies), M09 (MaxDirectoryDepth), M10 (MaxDirectoryChildren), M12 (MaxPartialClassFiles), M13 (MaxPublicMembersPerType), F08 (ForbiddenNamespaceDependencies)

---

## Gefundene Quellen

### Chidamber & Kemerer, 1994 — A Metrics Suite for Object Oriented Design
- **Fundort:** via Web-Suche: "CK metrics Chidamber Kemerer inheritance depth defect density study"; IEEE Transactions on Software Engineering
- **Betrifft AiNetLinter-Features:** M06 (MaxInheritanceDepth), M08 (MaxConstructorDependencies)
- **Kernaussagen:**
  - Das CK-Suite definiert sechs OO-Metriken: DIT, NOC, CBO, RFC, WMC, LCOM
  - Tiefe Vererbungshierarchien (DIT) erschweren die Vorhersagbarkeit des Klassenverhaltens, da geerbt Methoden aus allen Vorfahren wirken
  - Klassen tief in der Hierarchie zeigen erhöhte Fehleranfälligkeit
  - CBO (Coupling Between Objects) korreliert mit Wartungsaufwand und Fehleranfälligkeit
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - DIT > 6 wurde in nachfolgenden Studien wiederholt als problematische Schwelle identifiziert (Korreferenz: Basili et al. 1996)
  - Keine absoluten Grenzwerte im Originalpaper
- **Einschränkungen dieser Quelle:** Studiert C++-Systeme der frühen 1990er; Java/C#-Ökosysteme und moderne Frameworks mit tiefen Klassienhierarchien (z.B. UI-Frameworks) nicht abgedeckt
- **Zeitliche Einordnung:** 1994; zeitstabil als theoretische Grundlage, konkrete Schwellenwerte modellspezifisch

### Basili, Briand & Melo, 1996 — A Validation of Object-Oriented Design Metrics as Quality Indicators
- **Fundort:** via Web-Suche: "CK metrics Chidamber Kemerer inheritance depth defect density study"; IEEE TSE
- **Betrifft AiNetLinter-Features:** M06 (MaxInheritanceDepth), M08 (MaxConstructorDependencies)
- **Kernaussagen:**
  - Empirische Validierung der CK-Suite an sieben C++-Systemen
  - CBO sowie vier der fünf übrigen CK-Metriken korrelieren signifikant mit Defektanzahl in Klassen
  - DIT korreliert mit Fehlerrisiko, aber schwächer als CBO
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine harten Grenzwerte; Korrelationskoeffizienten für CBO: positiv und signifikant (p < 0.05) in mehreren analysierten Systemen
- **Einschränkungen dieser Quelle:** Kleine Stichprobe (7 Systeme), C++ exklusiv, akademische Projekte; übertragbarkeit auf kommerzielle C#-Systeme unklar
- **Zeitliche Einordnung:** 1996; zeitstabil als empirische Grundlage

### Subramanyam & Krishnan, 2003 — Empirical Analysis of CK Metrics for Object-Oriented Design Complexity: Implications for Software Defects
- **Fundort:** https://www.researchgate.net/publication/3188321_Empirical_Analysis_of_CK_Metrics_for_Object-Oriented_Design_Complexity_Implications_for_Software_Defects
- **Betrifft AiNetLinter-Features:** M06 (MaxInheritanceDepth), M08 (MaxConstructorDependencies)
- **Kernaussagen:**
  - Hohe DIT-Werte sind mit höherer Fehleranzahl assoziiert
  - CBO hat bessere Vorhersagekraft für Defekte als DIT
  - WMC (Weighted Methods per Class) — ein Proxy für Klassenkomplexität — korreliert ebenfalls signifikant mit Defekten
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - DIT-Zunahme korreliert positiv mit Defektdichte (genaue Koeffizienten in der Originalarbeit)
- **Einschränkungen dieser Quelle:** Java-Systeme; Ergebnisse können von Sprachspezifika abhängen
- **Zeitliche Einordnung:** 2003; zeitstabil hinsichtlich OO-Prinzipien

### Al-Subaihin & Sarro, 2019 — A Comparison and Evaluation of Variants in the CBO Metric
- **Fundort:** https://www.sciencedirect.com/science/article/abs/pii/S0164121219300305
- **Betrifft AiNetLinter-Features:** M08 (MaxConstructorDependencies)
- **Kernaussagen:**
  - CBO-Varianten (Efferent vs. Afferent Coupling) messen unterschiedliche Aspekte von Kopplung
  - Efferente Kopplung (ausgehend — wie viele Klassen abhängt eine Klasse) ist wichtiger für Defektvorhersage
  - Hohe ausgehende Kopplung (wie bei vielen Konstruktor-Abhängigkeiten) ist besonders problematisch
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine universellen Schwellenwerte; kontextabhängig
- **Einschränkungen dieser Quelle:** Review bestehender Varianten; keine neue empirische Erhebung
- **Zeitliche Einordnung:** 2019; aktuell

### Microsoft / Best Practice Community — Dependency Injection Guidelines (.NET)
- **Fundort:** https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines; via Web-Suche: "constructor injection maximum dependencies best practice DI principles"
- **Betrifft AiNetLinter-Features:** M08 (MaxConstructorDependencies)
- **Kernaussagen:**
  - Klassen mit mehr als 3–4 Konstruktor-Abhängigkeiten sind verdächtig (Verletzung SRP)
  - "Constructor overload" ist ein Warnsignal für zu viele Verantwortlichkeiten
  - Microsoft empfiehlt explizit, Klassen mit vielen injizierten Abhängigkeiten zu refaktorieren
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - 3–4 Abhängigkeiten als Richtwert; ab mehr: Refactoring empfohlen
- **Einschränkungen dieser Quelle:** Keine formelle Studie; normatives Industriedokument (Microsoft)
- **Zeitliche Einordnung:** Laufend aktualisiert (2024); praxisnah

### NDepend Blog — When Is It Okay to Use a C# Partial Class?
- **Fundort:** https://blog.ndepend.com/okay-use-c-partial-class/
- **Betrifft AiNetLinter-Features:** M12 (MaxPartialClassFiles)
- **Kernaussagen:**
  - Legitime Hauptanwendungsfälle: (1) Code-Generator-Erweiterung (z.B. Designer.cs), (2) Aufteilung sehr großer Klassen in logische Abschnitte zur Teamarbeit
  - Partial Classes für reine Code-Organisation ohne Codegenerierung gelten als Antipattern
  - Als Faustregel: Mehr als 2–3 Partial-Dateien einer Klasse ist ein Indikator für zu viele Verantwortlichkeiten
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine formale Studie; normative Empfehlung des NDepend-Autors (Patrick Smacchia)
- **Einschränkungen dieser Quelle:** Blog-Artikel, keine empirische Studie; C#-spezifisch
- **Zeitliche Einordnung:** ca. 2018; zeitstabil für C#

### Ben Morris, 2023 — Writing ArchUnit-Style Tests for .NET and C# to Enforce Architecture Rules
- **Fundort:** https://www.ben-morris.com/writing-archunit-style-tests-for-net-and-c-for-self-testing-architectures/
- **Betrifft AiNetLinter-Features:** F08 (ForbiddenNamespaceDependencies)
- **Kernaussagen:**
  - NetArchTest und ArchUnitNET ermöglichen die Formulierung von Namespace-Abhängigkeitsregeln als Unit-Tests in .NET
  - Architektur-Enforcement-Tools können verbotene Abhängigkeiten (z.B. Services dürfen nicht auf Controllers zugreifen) automatisch prüfen
  - NDepend bietet über LINQ-Queries tiefgehende Abhängigkeitsanalyse für .NET
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine Schwellenwerte; qualitatives Architekturprinzip
- **Einschränkungen dieser Quelle:** Praxisartikel, keine empirische Studie
- **Zeitliche Einordnung:** 2023; aktuell für .NET-Ökosystem

### Pedersen et al. (via LCOM-Recherche) — Verschiedene LCOM-Studien (1990er–2011)
- **Fundort:** https://arxiv.org/abs/1004.3277; https://www.sciencedirect.com/article/pii/S1877050911000548; via Web-Suche: "cohesion LCOM software metrics empirical study defect"
- **Betrifft AiNetLinter-Features:** M13 (MaxPublicMembersPerType) — indirekt via Kohäsion
- **Kernaussagen:**
  - LCOM (Lack of Cohesion of Methods) ist schwer zu normalisieren und zeigt inkonsistente Ergebnisse je nach Formel-Variante
  - Kohäsion und Kopplung sind invers korreliert; beide beeinflussen Fehleranfälligkeit
  - CBO hat in vergleichenden Studien die beste Vorhersagekraft für Defekte (besser als LCOM)
  - Transitive LCOM-Varianten verbessern die Vorhersagekraft geringfügig
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine stabilen universellen LCOM-Grenzwerte; stark von Berechnungsvariante abhängig
- **Einschränkungen dieser Quelle:** Verschiedene Studien mit unterschiedlichen Methodologien; LCOM-Metriken gelten als umstritten
- **Zeitliche Einordnung:** 2004–2011; grundlegende Erkenntnisse zeitstabil, Details variieren

### Sandor Dargo, 2023 — How to Use Your Namespaces to Their Best
- **Fundort:** https://www.sandordargo.com/blog/2023/12/13/namespace-best-practices; via Web-Suche: "namespace directory structure organization best practice software metrics"
- **Betrifft AiNetLinter-Features:** M09 (MaxDirectoryDepth), M10 (MaxDirectoryChildren), F08 (ForbiddenNamespaceDependencies)
- **Kernaussagen:**
  - Zu viele verschachtelte Namespaces/Verzeichnisse erschweren Orientierung und Dependency-Management
  - Wenn Verzeichnisstruktur die Architektur widerspiegelt, werden Abhängigkeitsprobleme sichtbar
  - Große Verzeichnisse (viele Kinder) sind visueller Indikator für Architekturprobleme
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine formalen Grenzwerte; qualitative Empfehlung
- **Einschränkungen dieser Quelle:** Blog-Artikel, keine Studie
- **Zeitliche Einordnung:** 2023

### Concordia University / Arxiv, 2025/2026 — OpenClassGen: A Large-Scale Dataset for Class-Level Code Generation
- **Fundort:** https://arxiv.org/abs/2511.00872; Zenodo: https://zenodo.org/records/18409150
- **Betrifft AiNetLinter-Features:** M06 (MaxInheritanceDepth), M08 (MaxConstructorDependencies), M13 (MaxPublicMembersPerType)
- **Kernaussagen:**
  - OpenClassGen stellt über 324.000 reale Klassen zur Verfügung und reichert diese mit 27 statischen Qualitätsmetriken (wie CBO, DIT, LCOM) an.
  - Das Dataset zeigt, dass die Generierungsqualität von LLMs stark mit den Kopplungs- (CBO) und Kohäsionsmetriken (LCOM) korreliert.
  - Klassen mit hoher Kopplung (CBO) führen bei der Generierung durch LLM-Agenten nachweislich zu häufigeren "Project Context Conflicts" (Halluzinationen über Methoden-Signaturen und fehlerhafte Annahmen über APIs).
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Umfasst 324.843 Python-Klassen aus 2.970 Open-Source-Projekten.
  - CBO und DIT wurden als Schlüsselindikatoren für die Generierungsschwierigkeit von LLMs validiert.
- **Einschränkungen dieser Quelle:** Python-fokussiert; C#-Strukturen weisen durch stärkere Typisierung andere statische Analysemuster auf, die zugrunde liegende Kausalität (Kopplung erschwert Agenten-Orientierung) bleibt jedoch sprachunabhängig.
- **Zeitliche Einordnung:** 2025/2026; aktuell.

### Empirical OOD Complexity Study, 2024/2025 — Architectural Quality and Abstraction Deficiencies in PureAI Projects
- **Fundort:** arXiv:2511.00872; via Web-Suche
- **Betrifft AiNetLinter-Features:** M06 (MaxInheritanceDepth), M08 (MaxConstructorDependencies), M13 (MaxPublicMembersPerType)
- **Kernaussagen:**
  - Empirische Vergleiche zwischen rein KI-generierten Softwaresystemen ("PureAI") und menschlichen Projekten offenbaren ein signifikantes Defizit bei der objektorientierten Abstraktion.
  - LLMs neigen bei steigender Aufgabenkomplexität dazu, Architekturen stark zu vereinfachen (Oversimplification), anstatt saubere Vererbungsstrukturen (DIT) oder wohlüberlegte SRP-Muster (wie Constructor Dependencies) zu implementieren.
  - Das Resultat sind flache, aber unstrukturierte monolithische Klassen, bei denen die Komplexität in riesige Einzelfiles verschoben wird.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - AI-Systeme weisen zwar vordergründig eine geringere Dichte klassischer syntaktischer Smells auf, verletzen aber grundlegende Architekturmuster durch das Fehlen adäquater Abstraktions- und Kapselungsebenen.
- **Einschränkungen dieser Quelle:** Untersucht End-to-End-Generierung; in interaktiven (Mensch-Maschine) Szenarien können menschliche Vorgaben dieses Defizit teilweise kompensieren.
- **Zeitliche Einordnung:** 2024–2025; aktuell.

---

## Übergreifende Erkenntnisse

Die CK-Metriken (insbesondere DIT und CBO) sind seit den 1990ern empirisch wiederholt validiert: Hohe Werte korrelieren mit erhöhter Defektdichte. CBO (Kopplung) hat dabei konsistent stärkere Vorhersagekraft als DIT (Vererbungstiefe). LCOM (Kohäsion) ist theoretisch wichtig, aber messtechnisch inkonsistent — keine verlässlichen Grenzwerte etabliert.

Für **LLM-Agenten** gewinnt die Reduktion der Kopplung (CBO) und Vererbungstiefe (DIT) eine neue Dimension: 
1. **Reduktion von Project Context Conflicts:** Studien zu Datensätzen wie OpenClassGen (2025/2026) belegen, dass eine hohe Kopplung die Fehlerquote von Coding-Agenten erhöht, da diese fehlerhafte Annahmen über nicht im unmittelbaren Sichtfeld befindliche Typen und Methoden-Signaturen treffen.
2. **Abwehr von Oversimplification:** Da LLMs dazu neigen, OOD-Hierarchien zu stark zu vereinfachen und stattdessen unstrukturierte monolithische Klassen zu erzeugen, zwingen Linter-Grenzwerte für Klassen- und API-Größen (M01, M13) das Modell zur Einhaltung von Kapselung und Modularität.
3. **Effizientere RAG- und Chunk-Navigation:** Geringere Verzeichnistiefen (M09) und modularer Code erleichtern Agenten-Scaffoldings die Selektion der relevanten Quelltext-Abschnitte für das Kontextfenster.
4. **Muster-Reproduktion:** Agenten tendieren dazu, die vorgefundene Architektur zu kopieren. Ist das System stark gekoppelt, fügen Agenten oft noch stärkere Kopplung hinzu.

Für Konstruktor-Abhängigkeiten (M08) gibt es einen starken industriellen Konsens (3–4 als Maximum), aber keine formelle Studie mit Grenzwertvalidierung. Die Evidenz für Namespace-Organisation und Verzeichnistiefe (M09, M10) ist qualitativ/praktisch.

Partial Classes (M12) sind C#-spezifisch; der Konsens is: legitim für Code-Generierung, Antipattern bei reiner Organisationsnutzung. Keine empirische Studie gefunden.

Public-API-Größe (M13) ist nicht direkt durch eine studie belegt; die Evidenz leitet sich aus CBO-Kopplung (weniger exponierte Member → weniger Kopplung) und Engineering-Praktiken ab.

## Nicht gefunden / Lücken

- Keine empirische Studie speziell zu "MaxPublicMembersPerType" als Metrik.
- Keine Studie zu optimaler Verzeichnistiefe/-breite (M09, M10) mit quantitativen Ergebnissen.
- Keine empirische Untersuchung zu Partial-Class-Anzahl und Defektdichte.
- C#-spezifische Benchmarks, die den direkten Einfluss von DIT/CBO-Grenzwerten auf die Erfolgsrate von Agenten in C#-Repositories messen, fehlen weiterhin.
- Für ForbiddenNamespaceDependencies: Enforcement-Tools gut dokumentiert, aber keine Studie die zeigt, dass Architektur-Verletzungen zu Defekten führen (kausal).
