# AiNetLinter - AI-Optimierte .NET-Code-Validierung & Linter

`AiNetLinter` ist ein hochperformantes .NET 10 CLI-Linter-Tool, das speziell dafür entwickelt wurde, C#-Codebases für die Bearbeitung durch autonome AI-Agenten (wie Cursor, Claude Code, GitHub Copilot) zu optimieren und gleichzeitig die kognitive Last für menschliche Entwickler zu minimieren. 

Indem es Code-Metriken und Strukturvorgaben über Roslyn-Syntaxanalysen prüft, stellt das Tool sicher, dass der Code für Sprachmodelle (LLMs) maximal verständlich bleibt und Fehler im agentischen Entwicklungszyklus ("Agentic Loop") automatisiert korrigiert werden können.

---

## 1. Vision & Leitbild: Der "AI-Readability-Index"

Wenn KI-Agenten Code nicht mehr nur vervollständigen, sondern ihn autonom editieren, refaktorieren und erweitern, verschiebt sich das wichtigste Qualitätsmerkmal von Software: **Der Code muss so designt sein, dass eine KI ihn fehlerfrei erfassen und manipulieren kann.**

`AiNetLinter` setzt genau hier an und erzwingt einen modernen, AI-optimierten C#-Programmierstil, der auf wissenschaftlichen Erkenntnissen der LLM-Forschung und der Praxis agentischer Tools basiert.

### Die wissenschaftlichen Grundlagen der AI-Readability

Um zu verstehen, *warum* `AiNetLinter` bestimmte syntaktische Einschränkungen erzwingt, lohnt sich ein Blick auf die Kognitions- und Aufmerksamkeitsforschung von Large Language Models (LLMs). Die Regeln sind keine rein ästhetischen Konventionen, sondern basieren direkt auf den architektonischen Grenzen von Transformer-Modellen.

#### 1. Begrenzung der Dateigröße (`MaxLineCount` / Max. 500 Zeilen)
*   **Wissenschaftlicher Hintergrund:** Die Forschung zum Phänomen **"Lost in the Middle"** (Liu et al., 2023) belegt, dass LLMs Informationen am Anfang und am Ende ihres Kontextfensters hervorragend verarbeiten, in der Mitte jedoch signifikant an Aufmerksamkeit verlieren.
*   **Konsequenz:** In langen C#-Dateien sinkt die Genauigkeit des KI-Agenten drastisch. Beim Generieren von Code-Diffs neigt die KI dazu, mittlere Abschnitte fehlerhaft zu überschreiben oder bestehende Logik stillschweigend zu löschen.
*   **Referenz:** *Liu, N. F. et al. (2023). "Lost in the Middle: How Language Models Use Long Contexts". arXiv:2307.03172.*

#### 2. Kognitive & Zyklomatische Komplexität (`MaxCognitiveComplexity` / `MaxCyclomaticComplexity`)
*   **Wissenschaftlicher Hintergrund:** Da LLMs Code autoregressiv (linear Token für Token) generieren, müssen sie den aktuellen Zustand aller Ausführungspfade im internen Arbeitsspeicher (Hidden States) verwalten. Verschachtelte Schleifen, `if-else`-Kaskaden und logische Operatorenketten erhöhen die Zustandsraum-Komplexität, was zu Halluzinationen führt (Bubeck et al., 2023).
*   **Konsequenz:** Die Begrenzung der zyklomatischen und kognitiven Komplexität auf maximal 5 zwingt Entwickler zu flacherem Code mit Early Returns, was die Schlussfolgerungsfähigkeit (Reasoning) der KI stabilisiert.
*   **Referenz:** 
    * *Campbell, G. D. (2018). "Cognitive Complexity: A new way of measuring misdirection". SonarSource Whitepaper.*
    * *Bubeck, S. et al. (2023). "Sparks of Artificial General Intelligence: Early experiments with GPT-4". arXiv:2303.12712.*

#### 3. Lokale Eindeutigkeit & Shadowing-Verbot (`EnforceNoVariableShadowing`)
*   **Wissenschaftlicher Hintergrund:** Tokenizer zerlegen Code in Byte-Pair-Encoding (BPE) Subwords. Haben Variablen im selben Sichtbarkeitsbereich identische Bezeichner wie Klassenfelder (Shadowing), wird die Zuordnung der Aufmerksamkeitsgewichte (Attention Weights) im Self-Attention-Mechanismus gestört. Das Modell verwechselt den lokalen Scope mit dem äußeren Zustand (Vaswani et al., 2017).
*   **Konsequenz:** Das Verbot von Variable Shadowing stellt sicher, dass jeder Bezeichner im aktuellen Kontext eineindeutig referenziert werden kann.
*   **Referenz:** *Vaswani, A. et al. (2017). "Attention Is All You Need". Advances in Neural Information Processing Systems (NeurIPS).*

#### 4. Statische Zustandsverfolgung & Immutability (`EnforceReadonlyParameters` / `EnforceReadonlyFields`)
*   **Wissenschaftlicher Hintergrund:** Dynamische Zustandsänderungen (wie das Überschreiben von Methodeneingangsparametern oder das nachträgliche Ändern von privaten Feldern außerhalb des Konstruktors) erfordern vom LLM eine mentale Ablaufverfolgung (Symbolic Execution). LLMs sind jedoch primär statische Mustererkenner und scheitern häufig an komplexen Zustandsübergängen über die Zeit (Valmeekam et al., 2022).
*   **Konsequenz:** Indem Parameter und private Felder strikt `readonly` gehalten werden, wird der Datenfluss deklarativ. Die KI muss keinen veränderlichen Zustand über Zeilen hinweg simulieren.
*   **Referenz:** *Valmeekam, K. et al. (2022). "On the Planning Abilities of Large Language Models". arXiv:2206.10498.*

#### 5. Semantische Verankerung (`EnforceSemanticNaming` / `EnforceNoMagicValues`)
*   **Wissenschaftlicher Hintergrund:** LLMs verstehen Programmcode über zwei parallele Kanäle: den *strukturellen Kanal* (Syntaxbaum) und den *linguistischen Kanal* (Semantik der Namen). Studien zeigen, dass der linguistische Kanal die stärkste Rolle beim logischen Verstehen spielt. Generische Bezeichner (z. B. `data`, `temp`, `obj`) oder namenlose Magic Literale besitzen im Vektorraum der KI keine semantische Einbettung, was die Vorhersagequalität mindert (Radford et al., 2019).
*   **Konsequenz:** Alle Werte und Parameter müssen sprechend benannt sein, um eine korrekte Vektor-Einbettung (Embedding) und damit fehlerfreie Code-Generierung zu ermöglichen.
*   **Referenz:** *Radford, A. et al. (2019). "Language Models are Unsupervised Multitask Learners". OpenAI Blog.*

#### 6. Expliziter Kontrollfluss mit Fail-Fast-Präzisierung (`EnforceResultPatternOverExceptions` / `EnforceNoSilentCatch`)
*   **Wissenschaftlicher Hintergrund:** Exceptions für den Kontrollfluss verschleiern Zustandstransitionen (Madaan et al., 2023). Allerdings führt das vollständige Verbot aller Exception-Throws bei KIs zu *Silent Failures*, da Modelle aufgrund ihres Reinforcement-Learning-Bias (RLVR) extreme Angst vor Programmabstürzen haben und Fehler stumm schlucken (Karpathy, 2024). Um dies zu beheben, erlaubt `AiNetLinter` das Werfen technischer Standard-Laufzeitausnahmen (wie `ArgumentNullException`, `InvalidOperationException`), damit der Agent bei echten Bugs sofort hart fehlschlägt ("Fail-Fast") und sich anhand des Stacktraces korrigiert.
*   **Konsequenz:** Fachlicher Kontrollfluss nutzt das Result-Pattern (`Result<T>`); echte Programmierfehler oder Infrastruktur-Ausfälle werfen standardisierte Ausnahmen für deterministisches Fail-Fast.
*   **Referenz:** 
    * *Madaan, A. et al. (2023). "Self-Refine: Iterative Refinement with Self-Feedback". arXiv:2303.17651.*
    * *Karpathy, A. (2024). "LLMs are mortally terrified of exceptions". Hacker News Discussion.*

#### 7. Begrenzung der Kopplungsdichte (`MaxConstructorDependencies` / `ForbiddenNamespaceDependencies`)
*   **Wissenschaftlicher Hintergrund:** Je höher die Kopplung (Fan-Out) einer Klasse, desto mehr Abhängigkeiten muss ein AI-Agent laden und in sein Kontextfenster pressen, um eine Änderung durchzuführen. Dies verwässert die Aufmerksamkeit (Attention Dilution) und erhöht die Kosten und Fehlerrate (Ozkaya, 2020).
*   **Konsequenz:** Durch Begrenzung der Konstruktor-Abhängigkeiten (Constructor Injection) auf maximal 5 wird Modularität erzwungen, was die Analyse- und Bearbeitungsaufwände für KIs minimiert.
*   **Referenz:** *Ozkaya, I. (2020). "What Is Technical Debt? It's Not Just About Code Quality". IEEE Software.*

#### 8. Compiler-gestützte Leitplanken (.NET 10 Features)
*   Agenten arbeiten iterativ: Code schreiben -> Compiler ausführen -> Fehler korrigieren. `AiNetLinter` setzt darauf, dass der Compiler selbst zur Leitplanke wird:
    *   `#nullable enable` ist Pflicht (erzwingt Null-Checks).
    *   `required` Properties in Records (verhindert unvollständiges Instanziieren).
    *   Exhaustive Pattern Matching (Compiler wirft Fehler, wenn z. B. ein neues Enum-Mitglied im `switch` vergessen wurde).

#### 9. Strikte Zustand-Immutabilität (`EnforceExplicitStateImmutability`)
*   **Wissenschaftlicher Hintergrund:** Autoregressive Sprachmodelle scheitern überdurchschnittlich oft an der Verfolgung und konsistenten Aktualisierung von veränderlichem Zustand (*State Management Failures*). Das Erzwingen struktureller Unveränderlichkeit (Immutabilität) verlagert Zustandsänderungen in explizite, funktionale Rückgaben, was die kognitive Belastung für KIs minimiert.
*   **Konsequenz:** Klassen, die nicht explizit als DTOs/Entities deklariert sind, müssen als `readonly struct` oder `record` aufgebaut sein bzw. dürfen nur get-only/`init`-Properties und `readonly`-Felder besitzen.
*   **Referenz:** *DAPLab (2026). "9 Critical Failure Patterns of Coding Agents". Columbia University.*

#### 10. Isolierung zustandsloser Berechnungen (`EnforceStrictBoundaryForBusinessLogic`)
*   **Wissenschaftlicher Hintergrund:** Wenn KI-Agenten komplexe Berechnungsregeln mit asynchronem I/O (Datenbank- oder API-Aufrufe) vermischen, kommt es häufig zu logischen Inkonsistenzen und unvollständigen Tests (*Business Logic Mismatch*). Reine zustandslose Berechnungen lassen sich lokal in Millisekunden per Unit-Test validieren.
*   **Konsequenz:** Komplexe Geschäftslogik und Rechenoperationen müssen in als `static` deklarierten, zustandslosen Methoden ohne I/O-Typen gekapselt sein.
*   **Referenz:** *Tian, P. (2026). "Agentic Coding in Production: What SWE-bench Scores Don't Tell You".*

#### 11. Eindeutige Aufruf-Signaturen (`PreventContextDependentOverloads`)
*   **Wissenschaftlicher Hintergrund:** LLMs verwechseln im Vektorraum sehr leicht überladene Methoden mit identischem Namen, die sich nur durch primitive Typen (wie `Process(int)` vs. `Process(long)`) unterscheiden. Dies führt zu Vertauschungen von Argument-Reihenfolgen bei der Code-Generierung (*Parameter Hallucinations*).
*   **Konsequenz:** Methoden-Überladungen sind auf maximal 3 beschränkt. Überladungen, die sich nur in primitiven Typen bei gleicher Parameteranzahl unterscheiden, sind verboten (fordern explizite Methodennamen).
*   **Referenz:** *DAPLab (2026). "9 Critical Failure Patterns of Coding Agents" (Category 4: Data Management).*

#### 12. Puffer- und Stream-Abschneideschutz (`RequireExplicitTruncationHandling`)
*   **Wissenschaftlicher Hintergrund:** Wenn KI-Agenten Daten über unvollständige Eingaben, Streams oder abgeschnittene Ausgaben verarbeiten, neigen sie dazu, fiktiven "Phantom-Code" (wie nicht-existente Basisklassen) zu erfinden, um die Lücke zu erklären (*Spiraling Hallucination Loops*). Das Erzwingen expliziter Längenguards stoppt diese Spiralen.
*   **Konsequenz:** Alle Dateilese- und Stream-Leseoperationen müssen unmittelbare Längen- oder Vollständigkeits-Checks im Rumpf aufweisen.
*   **Referenz:** *Surge AI (2026). "When Coding Agents Spiral Into 693 Lines of Hallucinations".*

#### 13. Navigations-Hygiene & Feature-Ordner (`EnforceNamespaceDirectoryMapping` / `MaxDirectoryDepth`)
*   **Wissenschaftlicher Hintergrund:** Das passive Durchsuchen großer, verstreuter Klassenstrukturen flutet das Kontextfenster mit irrelevanten Informationen (*Context Rot*). Zudem treiben tiefe Ordnerpfade die Anzahl und Latenz von Agenten-Navigationsbefehlen (`cd`, `ls`) in die Höhe.
*   **Konsequenz:** Der Namespace muss exakt der physischen Ordnerstruktur (Feature Folder) entsprechen; die Ordnertiefe ab csproj wird auf maximal 4 begrenzt.
*   **Referenz:** 
    * *Chroma Research (2025). "Context Rot: How Increasing Input Tokens Impacts LLM Performance".*
    * *Arize AI (2026). "Context management in agent harnesses".*

#### 14. Referenz-Grounding (`DetectAndBanPhantomDependencies`)
*   **Wissenschaftlicher Hintergrund:** LLMs neigen dazu, Paket-Abhängigkeiten oder Klassen zu halluzinieren, die in der realen Codebasis nicht existieren. Bannen von ungelösten Namespace-using-Statements und dynamischer Reflection zwingt die KI zur Compile-Zeit-Verifizierung und verhindert "Phantom-Logik".
*   **Konsequenz:** Der Import von Namespaces, die Roslyn im Kompilierungskontext nicht auflösen kann, sowie String-basierte Reflection (`Type.GetType`) sind verboten.
*   **Referenz:** *Scale AI (2026). "SWE Atlas: Measuring Coding Agents".*

---

## 2. Der "AI-Mittelweg" für DRY vs. WET

Die klassische Regel **DRY** (Don't Repeat Yourself) führt bei extremem Einsatz zu tiefen, generischen Abstraktionen, die für KIs schwer verständlich sind und den gefürchteten "Schmetterlingseffekt" (Änderung an einer Stelle bricht unbemerkt 10 andere Stellen) begünstigen. `AiNetLinter` unterstützt einen pragmatischen Mittelweg:

1.  **Fachliches DRY (Strikt):** Kern-Geschäftslogik und Berechnungen müssen zentral und wiederverwendbar sein (z. B. in Domain-Modellen oder Services). Die KI muss diese Logik nur an einem einzigen Ort ändern.
2.  **Technisches WET (Erlaubt):** Controller, DTOs, Mapper und Queries dürfen redundant bzw. spezifisch pro Use Case (Vertical Slice) aufgebaut sein. Dies minimiert Seiteneffekte und verhindert, dass die KI riesige, geteilte Basisklassen anpassen muss und dabei andere Features beschädigt.

---

## 3. Kernfeatures von AiNetLinter

*   **Roslyn-basierte semantische Analyse:** Evaluierung der gesamten Solution (.sln / .slnx) über einen einzigen Syntax-Walk pro Dokument. Nutzt echte Semantik-Informationen statt textbasierter Heuristiken. MSBuild Design-Time-Properties beschleunigen das Solution-Laden; die Dokument-Analyse läuft parallel bis `Environment.ProcessorCount`.
*   **Feingranulares Regelwerk:** Umfassende Regeln für Klassendesign (Sealed, Value Objects, Vererbungstiefe), Variablen/Typen (kein `dynamic`, keine `out`-Parameter, Nullable Context) und Code-Komplexität (McCabe, SonarSource).
*   **PascalCase- & Namensvalidierung:** Typprüfung auf PascalCase-Konventionen sowie Erkennung nicht-semantischer Bezeichner (z. B. `data`, `temp`, `obj`).
*   **LSP-Dokumentationstests:** Erzwingt die Verwendung von XML-Docs (`/// <summary>`) auf öffentlichen APIs.
*   **Static Test Sentinel:** Statische Test-Präsenzprüfung für komplexe Quellcodeabschnitte anhand von Metadaten-Scans auf referenzierte Testbibliotheken (xunit, nunit etc.).
*   **Namespace-Abhängigkeitsprüfung (Vertical Slices):** Verhindert unerlaubte slice-übergreifende Abhängigkeiten, auch bei vollqualifizierten Typnamen.
*   **Warnungs-Unterdrückung (Suppression):** Flexibles Deaktivieren von Linter-Warnungen über inline Kommentare wie `// ainetlinter-disable [RuleName]`, dateiweit oder komplett per `// ainetlinter-disable all`.
*   **Gezielte Bulk-Suppression (`--add-disable-all` / `--remove-disable-all`):** Audit-basiertes Einfügen des Disable-all-Kommentars nur in Dateien mit Verstößen sowie sicheres Entfernen exakter Disable-all-Zeilen.
*   **SARIF- & Dependency-Graph-Export:** Generierung strukturierter SARIF-Fehlerberichte für CI/CD sowie automatisches Zeichnen von Mermaid-Abhängigkeitsdiagrammen.
*   **Baseline-Ratchet (Checksum):** Inkrementelle Migration bestehender Codebases — unveränderte Dateien werden per SHA-256 eingefroren, Verstöße nur in geänderten Dateien gemeldet.
*   **Projekt-spezifische Regel-Konfiguration (Project Overrides):** Flexibles Überschreiben oder Deaktivieren von Linter-Regeln gezielt für bestimmte Projekte (z. B. über Wildcards wie `*.Tests`) in der Konfiguration.
*   **AI-Context-Footprint (Metrik):** Berechnet die Summe aller Codezeilen einer Klasse inklusive aller transitiv referenzierten eigenen Typen, um hohe Kopplung und große Kontext-Footprints für KIs zu vermeiden.
*   **Automatisch generiertes Repo-Playbook:** Analysiert die Codebase und generiert eine Übersicht über genutzte Muster und Unterdrückungsstatistiken zur automatischen Kontext-Adaption für KI-Agenten.
*   **Roslyn-basierter CLI Auto-Fixer (`--fix`):** Vollautomatische Behebung trivialer Linter-Verstöße (z. B. fehlendes `sealed`, `readonly` oder `#nullable enable`) über Syntaxbaum-Transformationen.
*   **Semantische Diff-Impact-Analyse (`--impact`):** Git-gestützte Auswirkungsanalyse, die bei Signaturänderungen alle betroffenen Aufrufstellen (Call-Sites) in der gesamten Solution ermittelt.


---

## 4. Konfiguration (`rules.json`)

Die Konfiguration erfolgt über eine flache, leicht verständliche JSON-Struktur. Beispiel einer vollständigen Konfiguration:

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowUnsealedPartialClasses": false,
    "AllowDynamic": false,
    "AllowOutParameters": false,
    "EnforceValueObjectContracts": true,
    "EnableTestSentinel": true,
    "EnforcePascalCase": true,
    "EnforceXmlDocumentation": true,
    "EnforceSemanticNaming": true,
    "EnforceNullableEnable": true,
    "EnforceNoSilentCatch": true,
    "AllowTryPatternOutParameters": true,
    "AllowCancellationShutdownCatch": true,
    "EnforceMinimalApiAsParameters": false,
    "EnforceResultPatternOverExceptions": true
  },
  "Metrics": {
    "MaxLineCount": 500,
    "MaxMethodParameterCount": 4,
    "MaxMethodLineCount": 42,
    "MaxCyclomaticComplexity": 5,
    "MaxCognitiveComplexity": 5,
    "MaxInheritanceDepth": 2,
    "MinCognitiveComplexityForTest": 3,
    "AggregatePartialClassLineCount": false,
    "MaxMethodOverloads": 2,
    "MaxConstructorDependencies": 5
  },
  "TestSentinel": {
    "ClassNamePatterns": ["{Name}Tests", "{Name}Test", "{Name}IntegrationTests", "{Name}*Tests"],
    "RecognizeTypeofReference": true,
    "RecognizeCoversComment": true
  },
  "RuleMetadata": {
    "MaxLineCount": { "severity": "error", "intent": "agent-context" },
    "StaticTestSentinel": { "severity": "warning", "intent": "test-coverage" }
  },
  "ForbiddenNamespaceDependencies": [
    {
      "SourceNamespace": "MyFeature.Domain",
      "TargetNamespace": "MyFeature.Infrastructure"
    }
  ]
}
```

### Erklärung der Regeln

| Regel | Bereich | Beschreibung |
| :--- | :--- | :--- |
| `EnforceSealedClasses` | Global | Zwingt alle konkreten Klassen dazu, als `sealed` deklariert zu werden. |
| `AllowUnsealedPartialClasses` | Global | Erlaubt es, `partial` Klassen unsealed zu lassen (Standard: `false`, nützlich z. B. bei Blazor Page-Components). |
| `AllowDynamic` | Global | Verbietet das Typschlüsselwort `dynamic` (verhindert statische Analyse-Lücken). |
| `AllowOutParameters` | Global | Verbietet `out`-Parameter zugunsten von C#-Tuples oder Records. |
| `AllowTryPatternOutParameters` | Global | Erlaubt `out` in `bool Try*`-Methoden (Standard: `true`, idiomatisches C#). |
| `AllowCancellationShutdownCatch` | Global | Erlaubt leere `catch (OperationCanceledException) when (...)` bei Host-Shutdown. |
| `EnforceMinimalApiAsParameters` | Global | Prüft Minimal-API-Endpunkte auf fehlendes `[AsParameters]` bei >4 Parametern (opt-in). |
| `EnforceValueObjectContracts` | Global | Zwingt Klassen mit Suffix `ValueObject` dazu, als `record` oder `readonly struct` deklariert zu sein und nur unveränderliche Eigenschaften (ohne `set`) zu haben. |
| `EnableTestSentinel` | Global | Aktiviert den Test-Präsenzwächter für komplexe Quellcodedateien. |
| `EnforcePascalCase` | Global | Validiert PascalCase-Schreibweise für Klassen, Structs, Records, Interfaces, Methoden und Properties. |
| `EnforceXmlDocumentation` | Global | Erzwingt XML-Dokumentationskommentare an öffentlichen Typ-Deklarationen (Klassen/Interfaces) (Standard: `false`). |
| `EnforceSemanticNaming` | Global | Markiert generische Parameternamen (z. B. `data`, `temp`, `val`) in öffentlichen Methoden als Fehler. |
| `EnforceNullableEnable` | Global | Stellt sicher, dass `#nullable enable` in jeder Datei deklariert ist oder global über csproj erzwungen wird. |
| `EnforceNoSilentCatch` | Global | Verbietet leere `catch`-Blöcke oder solche, die Fehler verschlucken ohne re-throw oder Logging. Variable Namen, die mit `ignored` oder `expected` beginnen (z. B. `catch (Exception ignored)`), werden ignoriert. |
| `EnforceResultPatternOverExceptions` | Global | Verbietet `throw` für fachlichen Kontrollfluss. Technische Standard-Exceptions (wie `ArgumentNullException`) sind für Fail-Fast erlaubt. |
| `EnforceNoVariableShadowing` | Global | Verbietet das Verdecken von Feldern, Eigenschaften und äußeren Parametern durch lokale Variablen und Parameter. |
| `EnforceReadonlyParameters` | Global | Verbietet das Überschreiben von Methodenschnittstellen-Parametern (Verbot von Parameter-Reassignment). |
| `EnforceReadonlyFields` | Global | Prüft, ob private Felder, die nur im Konstruktor/Initialisierer zugewiesen werden, als `readonly` deklariert sind. |
| `EnforceNoMagicValues` | Global | Verbietet Magic Numbers und Magic Strings direkt in Methodenkörpern außerhalb von Konstanten-Deklarationen (Ausnahmen: `0`, `1`, `""`). |
| `EnforceExplicitStateImmutability` | Global | Zwingt alle Klassen (außer DTOs/Entities) zu Immutabilität (init/get-only Eigenschaften und private readonly Felder). |
| `EnforceStrictBoundaryForBusinessLogic` | Global | Zwingt reine Rechen- und Logikfunktionen in zustandslose `static` Methoden ohne I/O-Aufrufe. |
| `PreventContextDependentOverloads` | Global | Verbietet Methodenüberladungen, die sich nur durch primitive Typen bei gleicher Parameteranzahl unterscheiden. |
| `RequireExplicitTruncationHandling` | Global | Erzwingt unmittelbare Validierung (Länge/EOF-Check) nach I/O- und Stream-Leseoperationen. |
| `EnforceNamespaceDirectoryMapping` | Global | Stellt sicher, dass deklarierte Namespaces exakt der physischen Ordnerstruktur entsprechen. |
| `DetectAndBanPhantomDependencies` | Global | Verbietet die Einbindung nicht auflösbarer Namespaces sowie dynamische Reflection-Lade-APIs. |
| `MaxLineCount` | Metrics | Maximale Zeilenanzahl pro Datei (Standard: 500), um "Lost in the Middle"-Effekte zu verhindern. |
| `MaxMethodParameterCount`| Metrics | Maximale Parameteranzahl pro Methode (Standard: 4). |
| `MaxMethodLineCount` | Metrics | Maximale Codezeilenanzahl pro Methode ohne Kommentare/Leerzeilen (Standard: 42). |
| `MaxCyclomaticComplexity`| Metrics | Maximale zyklomatische Komplexität (McCabe) pro Methode (Standard: 5). |
| `MaxCognitiveComplexity` | Metrics | Maximale kognitive Komplexität (SonarSource) pro Methode (Standard: 5). |
| `MaxInheritanceDepth` | Metrics | Maximale Tiefe der Vererbungshierarchie (Standard: 2). |
| `MinCognitiveComplexityForTest` | Metrics | Schwellenwert der kognitiven Komplexität, ab dem der Test Sentinel eine zugehörige Testklasse einfordert. |
| `AggregatePartialClassLineCount` | Metrics | Summiert Zeilenanzahl über alle `partial`-Teile eines Typs (opt-in). |
| `MaxMethodOverloads` | Metrics | Maximale Anzahl von Methoden-Überladungen pro Name in einer Klasse (Standard: 3). |
| `MaxConstructorDependencies` | Metrics | Maximale Parameter-Anzahl pro Konstruktor / Primärkonstruktor (Standard: 5). |
| `MaxDirectoryDepth` | Metrics | Maximale Ordnertiefe ab csproj-Ebene (Standard: 4). |
| `MaxAIContextFootprint` | Metrics | Die maximale Anzahl transitiver Codezeilen von Klassenabhängigkeiten (Standard: 5000). |
| `TestSentinel` | Config | Flexible Testabdeckung: Klassenname-Patterns, `typeof`-Referenz, `// @covers`-Kommentar. |
| `RuleMetadata` | Config | Severity (`error`/`warning`) und Intent-Tags pro Regel für LLM-Priorisierung. |

### Projekt-spezifische Regel-Konfiguration (Project Overrides)

In großen Solutions können verschiedene Projekte unterschiedliche Qualitätsanforderungen haben. In Testprojekten sind beispielsweise literale Werte (Magic Values) in Assertions erwünscht. Über die Sektion `"ProjectOverrides"` in der `rules.json` können Regeln gezielt für bestimmte Projekte (z. B. über Wildcards wie `*.Tests`) überschrieben werden:

```json
  "ProjectOverrides": {
    "*.Tests": {
      "Global": {
        "EnforceNoMagicValues": false,
        "EnforceSealedClasses": false
      },
      "Metrics": {
        "MaxMethodLineCount": 100
      }
    }
  }
```

### AI-Context-Footprint (Metrik)

Der AI-Context-Footprint berechnet die Summe aller Codezeilen der Klasse selbst plus aller transitiv im Quellcode referenzierten eigenen Klassen/Typen. Steigt diese Metrik über den konfigurierten Schwellenwert (`MaxAIContextFootprint`, standardmäßig `5000` Zeilen), wird ein Regelverstoß gemeldet. Dies hilft Entwicklern, hohe Kopplung zu vermeiden und die Token-Belastung für KIs gering zu halten.

---

## 5. Kompilieren & Bereitstellen (Build & Deployment)

Da `AiNetLinter` auf Roslyn-Compiler-Diensten und `MSBuildWorkspace` aufbaut, muss das Tool für die Verwendung in anderen Repositories speziell kompiliert und verpackt werden.

### Lokalen Build erzeugen
Um das Tool als eigenständiges, plattformspezifisches CLI-Tool für Windows zu kompilieren:
```bash
dotnet publish src/AiNetLinter/AiNetLinter.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

### WICHTIG: MSBuild-Abhängigkeiten (BuildHost-Ordner)
`MSBuildWorkspace` benötigt externe Host-Prozesse zum Parsen von Visual Studio Projektdateien. Nach dem Build müssen zwingend folgende Unterordner im selben Verzeichnis wie die `AiNetLinter.exe` liegen:
*   `BuildHost-netcore/`
*   `BuildHost-net472/`

Diese Ordner werden standardmäßig beim `dotnet publish` automatisch erzeugt. **Wenn Sie das Tool in ein anderes Repository kopieren (z. B. in einen `tools/`-Ordner), müssen diese beiden Unterordner mitsamt ihren DLLs zwingend mitkopiert werden.** Andernfalls bricht das Tool bei der Analyse einer Solution mit einem fatalen MSBuildWorkspace-Ladefehler ab.

---

## 6. CLI-Schnittstelle

`AiNetLinter` wird als Windows .NET 10 Core CLI-Tool ausgeführt.

### Aufruf-Syntax

```bash
ainetlinter --config <Pfad-zur-rules.json> --path <Pfad-zur-slnx-oder-Verzeichnis> [Optionen]
```

### Parameter

*   `-c`, `--config` (Pfad): Der Pfad zur `rules.json` (Erforderlich für Audit-Läufe; nicht nötig mit `--create-baseline`).
*   `-p`, `--path` (Pfad): Der Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis (Erforderlich).
*   `--create-baseline` (Pfad): Erzeugt eine Baseline-JSON mit SHA-256-Checksummen aller `.cs`-Dateien (Optional).
*   `--baseline` (Pfad): Pfad zur Baseline-JSON für inkrementelle Migration — unterdrückt Verstöße in unveränderten Dateien (Optional).
*   `--add-disable-all` (Flag): Führt einen Audit-Lauf aus und fügt `// ainetlinter-disable all` nur in Dateien mit Verstößen ein; erfordert `--config` (Optional).
*   `--remove-disable-all` (Flag): Entfernt exakte `// ainetlinter-disable all`-Zeilen aus allen `.cs`-Dateien unter `--path`; erfordert keine `--config` (Optional).
*   `-g`, `--graph` (Pfad): Pfad für das zu generierende Mermaid-Abhängigkeitsdiagramm `.md` (Optional).
*   `-pb`, `--playbook` (Pfad): Pfad für das zu generierende AI Repository Playbook `.md` (Optional).
*   `-f`, `--format` (Format): Ausgabeformat: `text` (Standard) oder `sarif` (Optional).
*   `--verbose` (Flag): Aktiviert detaillierte Protokollausgaben (Optional).
*   `--debt-report` (Flag): Tech-Debt-Report (Disable-all nach Ordner, wave-ready Kandidaten); Exit 0 (Optional).
*   `--wave-ready` (Flag): Nur Verstöße in Dateien ohne `// ainetlinter-disable all` (Optional).
*   `--only-changed` (Flag): Nur geänderte Dateien — erfordert `--baseline` (Optional).
*   `--git-since` (Ref): Nur Verstöße in per `git diff` geänderten `.cs`-Dateien seit Ref, z. B. `HEAD~1` (Optional).
*   `--fix` (Flag): Automatische Behebung einfacher Verstöße (z. B. `sealed`, `readonly`, `#nullable enable`) direkt über die CLI (Optional).
*   `-im`, `--impact` (Ref): Semantische Diff-Impact-Analyse ab Git-Referenz (z. B. `HEAD~1` oder leer für uncommitted). Listet alle betroffenen Aufrufstellen (Call-Sites) in der Solution auf (Optional).

### Wellen-Workflow (Agent-Migration)

Für schrittweise Freischaltung von Legacy-Code (z. B. 5 Dateien pro Welle):

```bash
# Tech-Debt-Übersicht (kein Audit, Exit 0)
ainetlinter --path ./MeinProjekt.slnx --debt-report

# Nur bereits freigeschaltete Dateien mit Verstößen
ainetlinter --config rules.json --path ./MeinProjekt.slnx --wave-ready

# Diese Woche angefasste, freigeschaltete Dateien
ainetlinter --config rules.json --path ./MeinProjekt.slnx --wave-ready --git-since HEAD~7
```

### Inkrementelle Migration (Baseline / Ratchet)

**Use-Case:** Bestehende („alte“) Projekte mit hunderten oder tausenden Verstößen schrittweise auf AiNetLinter-Stand bringen — ohne Big-Bang-Refactoring und ohne Git-Integration.

**Workflow:**

1. **Einmalig einfrieren** — alle aktuellen Dateien per Checksumme in der Baseline speichern:
   ```bash
   ainetlinter --path ./MeinProjekt.slnx --create-baseline ainetlinter-baseline.json
   ```
2. **Baseline ins Repository committen** — die Datei `ainetlinter-baseline.json` versionieren.
3. **Regulärer Lauf / CI** — nur Verstöße in geänderten Dateien melden:
   ```bash
   ainetlinter --config rules.json --path ./MeinProjekt.slnx --baseline ainetlinter-baseline.json
   ```
4. **Datei bearbeiten** — Verstöße nur in dieser Datei werden ausgegeben; die Baseline wird automatisch mit den aktuellen Checksummen aktualisiert (weicher Ratchet).

**Semantik:**

| Zustand | Verhalten |
| :--- | :--- |
| Checksumme identisch mit Baseline | Datei unverändert → Verstöße werden **nicht** gemeldet |
| Checksumme abweichend oder Datei neu | Datei wurde angefasst → Verstöße werden **gemeldet** |
| Irgendeine Abweichung erkannt | Gesamte Baseline-Datei wird neu geschrieben |

**Weicher Ratchet:** Nach einem Lauf mit geänderten Dateien werden die neuen Checksummen eingefroren — auch wenn noch Verstöße bestehen. Um weitere Verbesserungen zu erzwingen, die Datei erneut bearbeiten.

**Baseline-Format** (relative Pfade mit Forward-Slashes, Basis: `--path`):

```json
{
  "version": 1,
  "files": {
    "src/MyApp/Program.cs": "a1b2c3d4e5f6..."
  }
}
```

### Roslyn-basierter CLI Auto-Fixer (`--fix`)

Triviale Linter-Verstöße kosten KI-Agenten wertvolle Prompt-Zyklen. Die Option `--fix` behebt einfache Verstöße (wie das Fehlen von `sealed` bei konkreten Klassen, `readonly` bei privaten Feldern oder das Fehlen von `#nullable enable` am Dateianfang) vollautomatisiert über Roslyn-Syntaxbaum-Transformationen direkt beim Audit-Lauf.

### Semantische Diff-Impact-Analyse (`--impact` / `-im`)

Bei Änderungen öffentlicher, interner oder geschützter Methodensignaturen hilft die Impact-Analyse, alle davon betroffenen Aufrufstellen (Call-Sites) in der gesamten Solution zu ermitteln. Sie analysiert dazu das Git-Diff (`git diff -U0`), ordnet geänderte Zeilen den deklarierten Methoden zu und sucht deren Referenzen.

Aufrufbeispiel:
```bash
ainetlinter --path ./MeinProjekt.slnx --impact HEAD~1
```

### Automatisch generiertes Repo-Playbook (`--playbook` / `-pb`)

Das Repo-Playbook scannt die bestehende Codebase und fasst Erkenntnisse wie genutzte Architekturmuster (Result-Pattern vs. throw) und Unterdrückungsstatistiken (deaktivierte Linter-Regeln) zusammen. KI-Agenten können dieses Dokument beim Start laden, um sich an die Gewohnheiten des Repositories anzupassen.

Das Playbook wird über das CLI-Argument `--playbook <Pfad>` oder `-pb <Pfad>` generiert, standardmäßig unter `.cursor/rules/playbook.md`:
```bash
ainetlinter --config rules.json --path ./MeinProjekt.slnx --playbook .cursor/rules/playbook.md
```

### Exit-Codes

*   `0`: Erfolg (Keine Regelverstöße gefunden).
*   `1`: Regelbrüche wurden identifiziert und ausgegeben.
*   `2`: Fataler Fehler (z. B. IO-Exception, MSBuildWorkspace-Ladefehler).

### Ausgabeformate

Alle Dateipfade in der Ausgabe sind **relativ zum `--path`-Argument** (Verzeichnis bzw. übergeordnetes Verzeichnis bei `.sln`/`.slnx`), mit Forward-Slashes.

#### Text (Standard, LLM-optimiert)

Token-effiziente Ausgabe für AI-Agenten. Jeder Text-Lauf gibt zuerst einen `# Run: [Datum und Uhrzeit]` Header aus. Bei Erfolg folgt `OK`. Bei Verstößen: kompakter Header mit Handlungsanweisung, parsebare Summary-Segmente (nach Datei und Regel) und sortierte Detail-Einzeiler.

```
# Run: 2026-06-13 09:06:13
# AiNetLinter · 2 violations
Behebe nur die gelisteten Verstöße. Minimaler Diff — kein Refactoring ausserhalb betroffener Stellen/Zeilen.

## Summary · by file
1 src/AiNetLinter/Core/LinterAnalyzer.cs
1 src/AiNetLinter/Models/RuleViolation.cs

## Summary · by rule
| Rule | Count | Intent |
|------|------:|--------|
| EnforceSealedClasses | 1 | general |
| MaxLineCount | 1 | agent-context |

## Violations
src/AiNetLinter/Core/LinterAnalyzer.cs:77 EnforceSealedClasses | Klasse 'Foo' nicht sealed → Füge den 'sealed' Modifikator hinzu.
src/AiNetLinter/Models/RuleViolation.cs:6 MaxLineCount | Datei hat 520 Zeilen (max 500) → Teile die Datei in kleinere Klassen auf.
```

**Summary-Formate:**
- Datei: `{anzahl} {relativerPfad}` — absteigend nach Anzahl
- Regel: Markdown-Tabelle `| Rule | Count | Intent |` — absteigend nach Anzahl

**Detail-Zeilenformat:** `{relativerPfad}:{zeile} {RegelName} | {Details} → {Guidance}` (Guidance nur wenn vorhanden)

#### SARIF (`--format sarif`)

Strukturiertes JSON für CI/CD-Integration. `artifactLocation.uri` enthält relative Pfade (Basis: `--path`).

---

## 7. Lokale Warnungs-Unterdrückung (Suppression)

Sollte es notwendig sein, bestimmte Regeln für eine Datei oder Zeile zu deaktivieren, kann dies über C#-Kommentare gelöst werden:

```csharp
// ainetlinter-disable all
// Deaktiviert alle AiNetLinter-Regeln für die gesamte Datei.

// ainetlinter-disable MaxLineCount
// Deaktiviert nur die MaxLineCount-Prüfung dateiweit.

public void LegacyMethod(int a, int b, int c, int d, int e) // ainetlinter-disable MaxMethodParameterCount
{
    // Deaktiviert den Parameter-Count-Linter exklusiv für diese Zeile
}

try
{
    int.Parse("not-a-number");
}
catch (Exception) // ainetlinter-disable EnforceNoSilentCatch
{
    // Deaktiviert den Silent-Catch-Linter exklusiv für diese catch-Zeile
}
```

### Gezielter Bulk-Ausschluss (nur betroffene Dateien)

Für Legacy-Codebases, in denen vorerst nur Dateien mit aktuellen Verstößen ausgeschlossen werden sollen:

```bash
ainetlinter --config rules.json --path ./MeinProjekt.slnx --add-disable-all
```

**Ablauf:**
1. Vollständiger Audit-Lauf mit der angegebenen `rules.json`
2. Ermittlung aller Dateien mit mindestens einem Verstoß
3. Einfügen von `// ainetlinter-disable all` am Dateianfang — nur in diesen Dateien
4. Bereits markierte Dateien werden übersprungen

Saubere Dateien bleiben unverändert und werden weiterhin geprüft.

### Bulk-Entfernung des Disable-all-Kommentars

Zum Rückbau nach Refactoring oder wenn der Ausschluss nicht mehr nötig ist:

```bash
ainetlinter --path ./MeinProjekt.slnx --remove-disable-all
```

Es werden ausschließlich Zeilen entfernt, die **exakt** `// ainetlinter-disable all` entsprechen (Zeilenanfang bis Zeilenende, `\r\n` und `\n` werden berücksichtigt). Abweichende Varianten wie eingerückte oder erweiterte Kommentare bleiben unangetastet.

---

## 8. Integration in Unit Tests

Um sicherzustellen, dass AI-Agenten (wie Cursor oder Claude Code) die Linter-Regeln im laufenden Entwicklungsbetrieb eines Repositories nicht verletzen, empfiehlt sich die Integration als Unit-Test.

Hier ist ein C#-Integrationsbeispiel für ein beliebiges anderes Projekt:

```csharp
using Xunit;
using System.Diagnostics;
using System.IO;

public sealed class ArchitectureTests
{
    [Fact]
    public void Enforce_AiNetLinter_Rules_On_Solution()
    {
        // Pfade relativ zu diesem Testprojekt auflösen
        var solutionPath = Path.GetFullPath("../../../MyProject.slnx");
        var configPath = Path.GetFullPath("../../../rules.json");
        var baselinePath = Path.GetFullPath("../../../ainetlinter-baseline.json");
        
        // Pfad zur bereitgestellten AiNetLinter.exe (samt den BuildHost-Ordnern im selben Pfad)
        var linterCliPath = Path.GetFullPath("../../../tools/ainetlinter/AiNetLinter.exe");

        var processInfo = new ProcessStartInfo
        {
            FileName = linterCliPath,
            Arguments = $"--config \"{configPath}\" --path \"{solutionPath}\" --baseline \"{baselinePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        Assert.NotNull(process);
        
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // Wenn der Linter Verstöße findet, liefert er Exit-Code 1 und der Test schlägt fehl
        Assert.True(process.ExitCode == 0, $"AiNetLinter hat Verstoesse gefunden:\n{output}");
    }
}
```

> [!IMPORTANT]
> **MSBuild-Abhängigkeiten beachten:**
> Für diesen Test müssen im Verzeichnis `tools/ainetlinter/` neben der `AiNetLinter.exe` auch unbedingt die beiden Unterordner `BuildHost-netcore/` und `BuildHost-net472/` liegen, die beim Build/Publish des Tools erzeugt werden. Andernfalls schlägt die Analyse fehl.

---

## 9. Zukunfts-Roadmap (Ausblick)

*   **Erweiterte semantische Datenflussanalyse:** Statische Überprüfung komplexerer Datenflussketten, um veränderliche Zustandsänderungen über Klassengrenzen hinweg für KIs zu markieren.
*   **Weitere automatische CLI Code-Fixes:** Ausbau des Auto-Fixers zur Behebung komplexerer Strukturverletzungen (z. B. automatisches Auslagern übergroßer Methoden).