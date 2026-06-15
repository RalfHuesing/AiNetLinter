# AiNetLinter — Design-Rationale & wissenschaftliche Grundlagen

Dieser Abschnitt erklärt, *warum* `AiNetLinter` bestimmte syntaktische Einschränkungen erzwingt. Die Regeln sind keine rein ästhetischen Konventionen, sondern basieren auf architektonischen Grenzen von Transformer-Modellen und empirischen Erkenntnissen aus der LLM-Forschung.

→ [README](../README.md) | [Konfigurationsreferenz](configuration.md)

---

## Vision & Leitbild

Wenn KI-Agenten Code nicht mehr nur vervollständigen, sondern ihn autonom editieren, refaktorieren und erweitern, verschiebt sich das wichtigste Qualitätsmerkmal von Software: **Der Code muss so designt sein, dass eine KI ihn fehlerfrei erfassen und manipulieren kann.**

`AiNetLinter` setzt genau hier an und erzwingt einen modernen, AI-optimierten C#-Programmierstil, der auf wissenschaftlichen Erkenntnissen der LLM-Forschung und der Praxis agentischer Tools basiert.

---

## Wissenschaftliche Grundlagen der AI-Readability

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
