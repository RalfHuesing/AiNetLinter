# EnforceNamespaceDirectoryMapping (R18)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true (Modus: `suffix-match`, mind. 2 trailing Segmente) | **Status:** Aktiv  
**Severity:** error  
**Paper-Cluster genutzt:** E, C

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Die Übereinstimmung von Namespace und Verzeichnispfad ist eine grundlegende Navigationskonvention in .NET; ihre Verletzung erzeugt für LLM-Agenten nachweislich Kontext-Konflikte bei der Dateisuche und Typ-Auflösung — die Regel ist in diesem Kontext besonders wertvoll.

---

## Empfehlung

**Aktion:** Aktiviert lassen; `suffix-match` mit 2 trailing Segmenten ist ein guter Kompromiss  
**Begründung:** Namespace-Verzeichnis-Mapping ist De-facto-Standard in .NET; Abweichungen erzeugen Verwirrung bei Roslyn-Analyze-Tools, IDEs, und — am relevantesten — bei LLM-Agenten, die Dateipfade und Namespaces zur Navigation nutzen.

---

## Wissenschaftliche / Empirische Grundlage

**Microsoft .NET Konventionen (2024):** Namespaces sollen die Verzeichnisstruktur widerspiegeln — dies ist eine langjährige .NET-Konvention, die in der SDK-Style-Projektstruktur von .NET 5+ durch den MSBuild-Property `<RootNamespace>` unterstützt wird. Das default Verhalten neuer Projekte in Visual Studio und `dotnet new` setzt Namespaces basierend auf dem Verzeichnispfad.

**Sandor Dargo (2023):** In seinem Artikel zu Namespace-Best-Practices hält er fest: Wenn Verzeichnisstruktur die Architektur widerspiegelt, werden Abhängigkeitsprobleme sichtbar — Namespace-Verzeichnis-Mapping ist damit kein kosmetisches Feature, sondern ein Architektur-Signal.

**Aus Cluster E (Ben Morris 2023 — NetArchTest):** Architektur-Enforcement-Tools wie NetArchTest, ArchUnitNET und NDepend arbeiten alle auf Namespace-Basis. Die Verlässlichkeit dieser Tools hängt davon ab, dass Namespaces konsistent mit der physischen Struktur übereinstimmen. Verletzungen unterlaufen Architektur-Enforcement-Mechanismen.

Die Grundlage ist überwiegend konventionell und aus Design-Prinzipien abgeleitet — direkte empirische Studien zu "Namespace-Verzeichnis-Mismatch und Fehlerrate" fehlen (Ableitung, kein direktes Paper).

## KI-Agenten-Perspektive

Dies ist die **relevanteste Regel für LLM-Agenten** in der gesamten R-Gruppe:

Aus Cluster C ("Inside the Scaffold: Agent Failure Taxonomy", arXiv:2604.03515): **Repository-Kontext-Fehler** sind mit über 50 % die häufigste Fehlerquelle von Coding-Agenten. Ein LLM-Agent navigiert Code-Repositories über Dateipfade und Namespaces. Wenn diese nicht übereinstimmen, muss der Agent zwei separate Navigations-Systeme gleichzeitig pflegen:
1. Den physischen Verzeichnispfad (`src/Orders/Infrastructure/Repositories/`)
2. Den Namespace (`Company.Product.Data.Persistence.Repos.Orders`)

Diese Diskrepanz erhöht die Wahrscheinlichkeit von "Phantom-Importen" — der Agent schreibt einen `using`-Statement basierend auf dem Verzeichnispfad, der tatsächliche Namespace weicht aber ab, was zu Compile-Fehlern führt (genau das, was R19 als "Phantom-Dependency" klassifiziert).

Aus Cluster C (Liu et al. 2025, "Project Context Conflicts"): Fehlerhafte Annahmen über Namespace-Struktur gehören zur Kategorie der häufigsten LLM-Halluzinationen in echten Projekten.

**Der `suffix-match`-Modus** (mind. 2 trailing Segmente müssen übereinstimmen) ist ein kluger Kompromiss: Er erlaubt flexible Root-Namespaces (`Company.Product`) während er sicherstellt, dass die architekturisch relevanten Teile des Pfads (`Orders.Infrastructure`) im Namespace sichtbar sind.

**Ignorierte Segmente** (`src`, `Source`, `Domains`, `Handlers`) sind technische Build-Artefakte ohne architektonische Bedeutung — korrekterweise aus dem Matching ausgenommen.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Namespace-Verzeichnis-Mapping ist eine strukturelle .NET-Konvention die seit .NET 1.0 existiert und mit SDK-Style-Projekten gestärkt wurde. LLM-Agenten orientieren sich an dieser Konvention im Trainingskorpus (GitHub-Repositories folgen ihr fast universell). Abweichungen sind strukturell verwirrend — unabhängig von der Modellgeneration.

## Risiken / Gegenargumente

**Legacy-Projekte mit historischen Namespaces:** Ältere Projekte haben oft Namespaces, die aus historischen Gründen nicht der Verzeichnisstruktur entsprechen. Der Baseline-Mechanismus (F01) löst diesen Fall.

**Mono-Repo-Strukturen:** In Mono-Repos mit mehreren Produkten kann die Root-Namespace-Konvention von der Verzeichnisstruktur abweichen. Der `suffix-match`-Modus mit konfigurierbaren ignorierten Segmenten ist genau für diesen Fall gedacht — die Konfiguration muss sorgfältig gepflegt werden.

**False Positives bei generierten Dateien:** Generierter Code (z.B. EF Migrations, Protobuf-Gen) folgt nicht immer Namespace-Konventionen. FileFilters (F07) schließen diese Fälle aus — das Zusammenspiel ist korrekt.

---

## Quellen

- Sandor Dargo, 2023, "How to Use Your Namespaces to Their Best" — https://www.sandordargo.com/blog/2023/12/13/namespace-best-practices
- Ben Morris, 2023, "Writing ArchUnit-Style Tests for .NET and C#" — https://www.ben-morris.com/writing-archunit-style-tests-for-net-and-c-for-self-testing-architectures/
- arXiv, 2025, "Inside the Scaffold: Agent Failure Taxonomy" — arXiv:2604.03515
- Liu et al., 2024/2025, "LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
- Microsoft .NET Naming Conventions: https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
