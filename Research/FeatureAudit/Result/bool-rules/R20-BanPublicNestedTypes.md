# BanPublicNestedTypes (R20)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true (private Nested Types erlaubt) | **Status:** Aktiv  
**Severity:** error  
**Paper-Cluster genutzt:** D, C, E

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Öffentliche verschachtelte Typen vergrößern die semantische Komplexität von Typsignaturen (`Outer.Inner`-Qualifizierung), erhöhen die effektive API-Oberfläche einer Klasse und erzeugen für LLM-Agenten Mehrdeutigkeit bei der Typauflösung — die Regel folgt dem Microsoft-Konsens und ist empirisch durch Kopplung-Komplexitäts-Studien fundiert.

---

## Empfehlung

**Aktion:** Aktiviert lassen; die Ausnahme für private Nested Types ist korrekt  
**Begründung:** Öffentliche Nested Types sind in der .NET-Community weitgehend als Antipattern klassifiziert; sie verstecken Typen hinter einem anderen Typ, was Discover-Mechanismen (IntelliSense, LLM-Kontext-Fenster, Namespace-Suche) erschwert.

---

## Wissenschaftliche / Empirische Grundlage

**Microsoft .NET Design Guidelines (Framework Design Guidelines, Krzysztof Cwalina & Brad Abrams, 2. Auflage 2008):** Das Buch — quasi die kanonische Quelle für .NET-API-Design — empfiehlt explizit: "DO NOT use public nested types as a logical scoping construct." Öffentliche Nested Types sollen nur in dem seltenen Fall verwendet werden, wo der Typ tatsächlich nur im Kontext des äußeren Typs Sinn ergibt und niemals unabhängig instanziiert wird. In der Praxis ist dies sehr selten.

**Aus Cluster E (Chidamber & Kemerer 1994, Basili et al. 1996):** Öffentliche Nested Types erhöhen die effektive CBO-Kopplung (Coupling Between Objects) des äußeren Typs: Jeder Nutzer des äußeren Typs muss implizit auch den inneren kennen. Hohe CBO korreliert empirisch mit erhöhter Defektdichte. Der Bann öffentlicher Nested Types reduziert CBO direkt.

**Aus Cluster E (Al-Subaihin & Sarro 2019):** Efferente Kopplung (ausgehend) ist besonders problematisch für Defektvorhersage. Öffentliche Nested Types erhöhen die afferente Kopplung des äußeren Typs: Clients müssen ihn kennen um den inneren zu nutzen — das ist eine erzwungene, strukturelle Kopplung.

**M13 (MaxPublicMembersPerType) Synergie:** Verschachtelte öffentliche Typen werden häufig nicht in die Public-Member-Zählung einbezogen, obwohl sie de facto die API-Oberfläche vergrößern. R20 schließt diese Lücke komplementär zu M13.

## KI-Agenten-Perspektive

Aus Cluster C (Liu et al. 2025, "Project Context Conflicts"): LLM-Agenten haben Schwierigkeiten mit verschachtelten Qualifizierungen. Ein Agent, der `Orders.LineItem` aufrufen will, muss wissen: Ist `LineItem` ein eigener Typ im `Orders`-Namespace, oder ein öffentlicher Nested Type innerhalb von `Order`? Diese Mehrdeutigkeit führt zu Halluzinierungen: Der Agent schreibt entweder `Orders.LineItem` (Namespace-Annahme) oder `Order.LineItem` (Nested-Type-Annahme) — ohne zu wissen, welches korrekt ist.

Aus Cluster C ("Inside the Scaffold", arXiv:2604.03515): **Syntaxfehler und API-Fehler** sind die dritthäufigste Agenten-Fehlerquelle. Falsch qualifizierte Nested-Type-Pfade (`Outer.Inner` statt `Inner`) gehören genau in diese Kategorie.

**Private Nested Types** (korrekt als Ausnahme belassen): Sie sind der Klasse intern — LLM-Agenten sehen und nutzen sie nicht direkt. Der Bann betrifft nur öffentliche, also von außen sichtbare Nested Types. Private Nested Types als Implementation-Detail (z.B. `State`-Klassen in State-Machine-Implementierungen, `Builder`-Klassen) sind ein legitimes Kapselungsmittel.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Öffentliche Nested Types erhöhen die Navigationskomplexität strukturell — sowohl für Menschen (IntelliSense muss den äußeren Typ kennen) als auch für LLM-Agenten (Tokenisierung und Typ-Auflösung werden schwerer). Das ist unabhängig von der Modellkapazität eine strukturelle Eigenschaft verschachtelter Namensräume.

## Risiken / Gegenargumente

**Enumerations als Exception:** In C# ist es gängig, Enums zu verschachteln wenn sie konzeptionell zum äußeren Typ gehören (z.B. `OrderStatus` innerhalb von `Order`). AiNetLiners Regel schließt Enums explizit in das Verbot ein — hier könnten projektspezifische Muster Ausnahmen benötigen. Eine `AllowedNestedTypePatterns`-Konfiguration (z.B. `*Status`, `*Kind`) würde dies lösen.

**Builder-Pattern:** `Outer.Builder`-Muster (öffentlicher Builder als Nested Class) ist in der Java-Welt verbreitet; in C# ist es seltener, aber existent. Das Verbot trifft dieses Pattern — in C# ist aber alternativ ein separater `OrderBuilder`-Typ im selben Namespace idiomatisch und besser sichtbar.

**Keine direkten empirischen Studien zu Nested Types:** Die Evidenz ist überwiegend aus Design-Guidelines und CBO-Kopplung abgeleitet. Direkte Studien zu "öffentliche Nested Types und Defektdichte" fehlen — aber der Design-Konsens ist breit genug um die Regel zu stützen.

---

## Quellen

- Microsoft Framework Design Guidelines (Cwalina & Abrams, 2008) — .NET Design Recommendations
- Chidamber & Kemerer, 1994, "A Metrics Suite for Object Oriented Design" — IEEE TSE
- Al-Subaihin & Sarro, 2019, "A Comparison and Evaluation of Variants in the CBO Metric" — https://www.sciencedirect.com/science/article/abs/pii/S0164121219300305
- Liu et al., 2024/2025, "LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
- arXiv:2604.03515, 2025/2026, "Inside the Scaffold: Agent Failure Taxonomy"
