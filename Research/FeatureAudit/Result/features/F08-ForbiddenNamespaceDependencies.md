# ForbiddenNamespaceDependencies (F08)

**Kategorie:** Konfigurationsfeature  
**CLI-Flag / Konfiguration:** `rules.json → ForbiddenNamespaceDependencies`  
**Status:** Vorhanden, aktuell leer (keine Verbote konfiguriert)

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Architektur-Enforcement auf Namespace-Ebene ist ein gut etabliertes Mittel gegen Architektur-Erosion — andere Tools (NDepend, NetArchTest) bieten dieselbe Funktionalität, aber AiNetLinters CLI-Integration macht es für Agenten direkter nutzbar als IDE- oder Test-basierte Alternativen.

---

## Empfehlung

**Aktion:** Beibehalten und aktiv nutzen  
**Begründung:** Das Feature ist vorhanden und gut implementiert, aber der Leerstand (keine Verbote konfiguriert) bedeutet, dass es keinen Nutzen bringt. Für Projekte mit mehrschichtiger Architektur (Clean Architecture, Onion Architecture) sollten die grundlegenden Abhängigkeitsverbote (z.B. Infrastructure → Domain verboten) konfiguriert werden.

---

## Nutzen-Analyse

ForbiddenNamespaceDependencies erzwingt Architektur-Constraints durch das explizite Verbieten von Namespace-zu-Namespace-Abhängigkeiten:

**Typische Anwendungsfälle:**

1. **Clean Architecture / Onion Architecture Enforcement:**
   - `MyApp.Infrastructure` → `MyApp.Domain` erlaubt (Domain-Layer hat keine externen Abhängigkeiten)
   - `MyApp.Domain` → `MyApp.Infrastructure` **verboten** (Domain soll frei von Infrastruktur-Details sein)

2. **Feature-Slicing / Vertical Slices:**
   - `MyApp.Features.Orders` → `MyApp.Features.Payments` **verboten** (Features sollen nicht untereinander abhängen)

3. **Schichten-Isolation:**
   - `MyApp.Presentation` → `MyApp.DataAccess` **verboten** (Presentation soll nicht direkt auf Data Access zugreifen)

**Aktuelle Lage (leer):**
Da keine Verbote konfiguriert sind, ist das Feature im aktuellen Projekt-Setup wirkungslos. Das deutet darauf hin, dass das Projekt entweder keine strikte Schichtentrennung erfordert oder die Konfiguration noch nicht vorgenommen wurde.

**Investitionsaufwand:**
Die Konfiguration von ForbiddenNamespaceDependencies erfordert ein klares Architekturmodell — Teams müssen sich einig sein, welche Abhängigkeiten verboten sein sollen. Das ist der eigentliche Bottleneck, nicht die technische Implementierung.

---

## Vergleich: Andere Tools

| Tool | Namespace-Abhängigkeits-Enforcement | Ansatz |
|------|-------------------------------------|--------|
| **NDepend** | Vollständig (LINQ-Queries) | Sehr mächtig; proprietäre Analyse-Engine; komplex |
| **NetArchTest** | Als Unit-Tests | Flexibel; erfordert Schreiben von C#-Test-Code |
| **ArchUnitNET** | Als Unit-Tests | Ähnlich NetArchTest; etwas ausdrucksstärker |
| **Roslyn Analyzers** | Kein Standard-Feature | Möglich mit Custom Analyzers; hoher Implementierungsaufwand |
| **SonarQube** | Architecture Rules (Enterprise) | Nur in Enterprise-Edition verfügbar |
| **AiNetLinter** | `ForbiddenNamespaceDependencies` | CLI-basiert; einfache JSON-Konfiguration |

AiNetLinters Ansatz ist einfacher als NDepend (keine LINQ-Queries nötig) und integrierter als NetArchTest (kein separates Test-Projekt nötig). Der Nachteil ist die geringere Ausdruckskraft — NDepend erlaubt komplexere Abhängigkeitsanalysen (transitive Abhängigkeiten, zyklische Abhängigkeiten).

**Lücke:** Zyklische Namespace-Abhängigkeiten werden von AiNetLinter offenbar nicht erkannt — nur direkte Verbote. NDepend und NetArchTest können auch Zyklen detektieren.

---

## KI-Agenten-Perspektive

ForbiddenNamespaceDependencies hat für LLM-Agenten eine besondere Relevanz:

1. **Architektur-Erosion durch Agenten:** Studien zu "PureAI"-Projekten (arXiv:2511.00872, 2024/2025) zeigen, dass LLMs dazu neigen, Architekturprinzipien zu vereinfachen oder zu ignorieren, wenn sie nicht explizit erzwungen werden. Ein Agent der eine neue Klasse in `Infrastructure` schreibt, wird ohne Linter-Feedback gerne direkt auf `Domain`-Internals zugreifen, auch wenn das gegen Clean-Architecture-Prinzipien verstößt.

2. **Direktes Feedback im Edit-Loop:** Wenn der Agent eine verbotene Abhängigkeit einführt, erhält er beim nächsten `AiNetLinter`-Lauf einen Fehler — mit dem er arbeiten kann. Das ist effizienter als statische Architektur-Dokumentation, die der Agent erst lesen müsste.

3. **Halluzinatins-Prävention für Architektur:** Ohne Enforcement neigen Agenten dazu, Abhängigkeiten zu "erfinden" die logisch plausibel erscheinen aber Architekturprinzipien verletzen. Die Regel zwingt zur korrekten Implementierung.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Architekturelle Trennung von Schichten ist ein fundamentales Software-Engineering-Prinzip, das unabhängig von Modellgenerationen gilt. Der Bedarf, LLM-Agenten daran zu hindern, Architektur-Constraints zu umgehen, wird mit zunehmendem Agenten-Autonomie-Grad eher größer.

---

## Quellen

- Ben Morris (2023) — Writing ArchUnit-Style Tests for .NET and C#: https://www.ben-morris.com/writing-archunit-style-tests-for-net-and-c-for-self-testing-architectures/
- NDepend Documentation (2024) — Dependency Rules: https://www.ndepend.com/features/dependency-analysis
- NetArchTest GitHub (2024): https://github.com/BenMorris/NetArchTest
- arXiv:2511.00872 (2024) — Empirical Study on PureAI Projects and Architectural Quality Deficiencies
- Sandor Dargo (2023) — How to Use Your Namespaces to Their Best: https://www.sandordargo.com/blog/2023/12/13/namespace-best-practices
