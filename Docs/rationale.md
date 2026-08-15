# AiNetLinter — Design-Rationale & wissenschaftliche Grundlagen

Dieser Abschnitt erklärt, *warum* `AiNetLinter` bestimmte syntaktische Einschränkungen erzwingt. Die Regeln sind keine rein ästhetischen Konventionen, sondern so weit wie möglich auf empirische Erkenntnisse aus der LLM-Forschung und der Praxis agentischer Tools gestützt. Das ist nicht bei jeder Regel gleich gut möglich: Manche Quellen belegen nur das allgemeine Phänomen, nicht den konkreten Schwellenwert; wo eine Regel im Kern eine Engineering-Kalibrierung statt eines direkt aus einer Studie abgeleiteten Werts ist, steht das explizit dabei, statt es stillschweigend als "wissenschaftlich bewiesen" darzustellen (Stand der Zitat-Überarbeitung: 2026-08-13).

→ [README](../README.md) | [Konfigurationsreferenz](configuration.md)

---

## Vision & Leitbild

Wenn KI-Agenten Code nicht mehr nur vervollständigen, sondern ihn autonom editieren, refaktorieren und erweitern, verschiebt sich das wichtigste Qualitätsmerkmal von Software: **Der Code muss so designt sein, dass eine KI ihn mit hoher Wahrscheinlichkeit korrekt erfassen und manipulieren kann — messbar an konkreten Agent-Benchmarks.**

`AiNetLinter` setzt hier an und erzwingt einen C#-Programmierstil, dessen Regeln aus LLM-Forschung und Praxis agentischer Tools abgeleitet sind (Belege in §1–§8).

---

## Wissenschaftliche Grundlagen der AI-Readability

#### 1. Begrenzung der Dateigröße (`MaxLineCount` / Max. 500 Zeilen)
*   **Wissenschaftlicher Hintergrund:** Die Forschung zum Phänomen **"Lost in the Middle"** (Liu et al., 2023) belegt, dass LLMs Informationen am Anfang und am Ende ihres Kontextfensters zuverlässiger verarbeiten als in der Mitte, wo die Aufmerksamkeit messbar abnimmt.
*   **Konsequenz:** In langen C#-Dateien sinkt die Genauigkeit des KI-Agenten drastisch. Beim Generieren von Code-Diffs neigt die KI dazu, mittlere Abschnitte fehlerhaft zu überschreiben oder bestehende Logik stillschweigend zu löschen.
*   **Einordnung:** Liu et al. belegen das allgemeine Phänomen (Attention-Abfall bei langen Kontexten, getestet an Multi-Doc-QA/Key-Value-Retrieval), nicht speziell eine 500-Zeilen-Schwelle für Code-Dateien. Der konkrete Wert 500 ist AiNetLinters eigene praktische Kalibrierung — durch den allgemeinen Befund motiviert, aber nicht direkt aus dem Paper ableitbar.
*   **Referenz:** *Liu, N. F. et al. (2023). "Lost in the Middle: How Language Models Use Long Contexts". arXiv:2307.03172.*

#### 2. Kognitive & Zyklomatische Komplexität (`MaxCognitiveComplexity` / `MaxCyclomaticComplexity`)
*   **Wissenschaftlicher Hintergrund:** Da LLMs Code autoregressiv (linear Token für Token) generieren, müssen sie den aktuellen Zustand aller Ausführungspfade im internen Arbeitsspeicher (Hidden States) verwalten. Verschachtelte Schleifen, `if-else`-Kaskaden und logische Operatorenketten erhöhen diese Zustandsraum-Komplexität. Die Kognitive-Komplexität-Metrik (Campbell, 2018) macht genau diese Verschachtelungstiefe messbar.
*   **Konsequenz:** Die Begrenzung der zyklomatischen und kognitiven Komplexität zwingt Entwickler zu flacherem Code mit Early Returns, was die Nachvollziehbarkeit für die KI erhöht.
*   **Einordnung:** Campbell (2018) begründet die Metrik selbst, nicht den konkreten Schwellenwert — SonarQube nutzt für dieselbe Metrik standardmäßig 15, nicht 5. AiNetLinters Grenze von 5 ist eine bewusst strengere eigene Kalibrierung für den Agentic-Coding-Kontext (siehe `ROADMAP.md` Epic 27), keine aus der Quelle abgeleitete Zahl. Ein zuvor hier zitiertes Paper zur These "Komplexität führt zu Halluzinationen" (Bubeck et al., 2023, "Sparks of AGI") wurde entfernt, da es keine Diskussion von Komplexitätsschwellenwerten enthält — die Verbindung war nicht durch die Quelle gedeckt.
*   **Referenz:** *Campbell, G. D. (2018). "Cognitive Complexity: A new way of measuring understandability". SonarSource Whitepaper.*

#### 3. Semantische Verankerung (`EnforceSemanticNaming`)
*   **Wissenschaftlicher Hintergrund:** LLMs verstehen Programmcode über zwei parallele Kanäle: den *strukturellen Kanal* (Syntaxbaum) und den *linguistischen Kanal* (Semantik der Namen). Generische Bezeichner (z. B. `data`, `temp`, `obj`) tragen für ein Sprachmodell weniger Information als sprechende Namen — das ist ein allgemein anerkanntes Prinzip der Software-Lesbarkeit, unabhängig von KI.
*   **Konsequenz:** Alle Werte und Parameter müssen sprechend benannt sein, um Code-Verständnis und -Generierung zu erleichtern.
*   **Einordnung:** Eine zuvor hier zitierte Quelle (Radford et al. 2019, das GPT-2-Paper) wurde entfernt: Das Paper behandelt Zero-Shot-Task-Transfer, enthält keinerlei Diskussion von Bezeichner-Semantik oder Embedding-Qualität einzelner Namen — die frühere Zuordnung hatte keinen inhaltlichen Bezug zur Quelle. Für diese Regel wurde keine passende, direkt einschlägige Studie gefunden; sie stützt sich auf ein allgemein akzeptiertes Software-Engineering-Prinzip, nicht auf eine LLM-spezifische empirische Untersuchung.

#### 4. Expliziter Kontrollfluss mit Fail-Fast-Präzisierung (`EnforceResultPatternOverExceptions` / `EnforceNoSilentCatch`)
*   **Wissenschaftlicher Hintergrund:** Exceptions für den Kontrollfluss verschleiern Zustandstransitionen und sind für ein Sprachmodell schwerer nachzuvollziehen als ein expliziter Rückgabewert. Allerdings führt das vollständige Verbot aller Exception-Throws bei KIs zu *Silent Failures*, da Modelle aufgrund ihres Reinforcement-Learning-Bias (RLVR) extreme Angst vor Programmabstürzen haben und Fehler stumm schlucken (Karpathy, 2024). Um dies zu beheben, erlaubt `AiNetLinter` das Werfen technischer Standard-Laufzeitausnahmen (wie `ArgumentNullException`, `InvalidOperationException`), damit der Agent bei echten Bugs sofort hart fehlschlägt ("Fail-Fast") und sich anhand des Stacktraces korrigiert.
*   **Konsequenz:** Fachlicher Kontrollfluss nutzt das Result-Pattern (`Result<T>`); echte Programmierfehler oder Infrastruktur-Ausfälle werfen standardisierte Ausnahmen für deterministisches Fail-Fast.
*   **Einordnung:** Eine zuvor hier zitierte Quelle (Madaan et al. 2023, "Self-Refine") wurde entfernt: Das Paper untersucht iteratives Selbst-Feedback über generische Aufgaben (Code-Optimierung, Math Reasoning, Dialog u. a.), enthält aber keine Aussage zu Exceptions als Kontrollfluss-Mechanismus — die frühere Zuordnung hatte keinen inhaltlichen Bezug zur Quelle. Die Fail-Fast-Argumentation selbst bleibt als Engineering-Überlegung bestehen, nur ohne diese Fehlzuordnung.
*   **Referenz:** *Karpathy, A. (2024). "LLMs are mortally terrified of exceptions". Hacker News Discussion (informelle Quelle, keine begutachtete Publikation).*

#### 5. Begrenzung der Kopplungsdichte (`MaxConstructorDependencies` / `ForbiddenNamespaceDependencies`)
*   **Wissenschaftlicher Hintergrund:** Je höher die Kopplung (Fan-Out) einer Klasse, desto mehr Abhängigkeiten muss ein AI-Agent laden und in sein Kontextfenster pressen, um eine Änderung durchzuführen. Das erhöht den Kontextbedarf pro Edit und damit potenziell Kosten und Fehlerrate.
*   **Konsequenz:** Durch Begrenzung der Konstruktor-Abhängigkeiten (Constructor Injection) auf maximal 5 wird Modularität erzwungen, was die Analyse- und Bearbeitungsaufwände für KIs minimiert.
*   **Einordnung:** Eine zuvor hier zitierte Quelle ("Ozkaya, I. (2020), 'What Is Technical Debt? It's Not Just About Code Quality', IEEE Software") ließ sich nicht verifizieren — Ozkayas tatsächlicher IEEE-Software-Beitrag 2020 trägt einen anderen Titel und behandelt Besonderheiten beim Engineering von ML-Systemen, nicht Kopplung oder Attention Dilution. Die Referenz wurde entfernt, keine passende Ersatzquelle gefunden. Die Argumentation (mehr Abhängigkeiten = mehr Kontext pro Edit) bleibt als plausible, aber unbelegte Engineering-Überlegung bestehen.

#### 6. Compiler-gestützte Leitplanken (.NET 10 Features)
*   Agenten arbeiten iterativ: Code schreiben -> Compiler ausführen -> Fehler korrigieren. `AiNetLinter` setzt darauf, dass der Compiler selbst zur Leitplanke wird:
    *   `#nullable enable` ist Pflicht (erzwingt Null-Checks).
    *   `required` Properties in Records (verhindert unvollständiges Instanziieren).
    *   Exhaustive Pattern Matching (Compiler wirft Fehler, wenn z. B. ein neues Enum-Mitglied im `switch` vergessen wurde).

#### 7. Strikte Zustand-Immutabilität (`EnforceExplicitStateImmutability`)
*   **Wissenschaftlicher Hintergrund:** Autoregressive Sprachmodelle scheitern überdurchschnittlich oft an der Verfolgung und konsistenten Aktualisierung von veränderlichem Zustand (*State Management Failures*). Das Erzwingen struktureller Unveränderlichkeit (Immutabilität) verlagert Zustandsänderungen in explizite, funktionale Rückgaben, was die kognitive Belastung für KIs minimiert.
*   **Konsequenz:** Klassen, die nicht explizit als DTOs/Entities deklariert sind, müssen als `readonly struct` oder `record` aufgebaut sein bzw. dürfen nur get-only/`init`-Properties und `readonly`-Felder besitzen.
*   **Einordnung:** Die Quelle ist ein informeller Praxisbericht (Einzelautorin, Beobachtungen aus 15 Apps × 5 Coding-Tools), keine begutachtete Publikation — hier als solcher gekennzeichnet, nicht als "Studie" überhöht.
*   **Referenz:** *Vir, R. (2026). "9 Critical Failure Patterns of Coding Agents" (Blogpost, Kategorie 2: "State Management Failures"). DAPLab, Columbia University, 8. Januar 2026.*

#### 8. Eindeutige Aufruf-Signaturen (`PreventContextDependentOverloads`)
*   **Wissenschaftlicher Hintergrund:** LLMs verwechseln bei überladenen Methoden mit identischem Namen leicht Parameter und Rückgabetypen unterschiedlicher Overloads. Eine Untersuchung von API-orientierter Code-Generierung fand, dass 85,6–86,2 % der Fehler mit falschem Rückgabe-/Parametertyp dadurch entstanden, dass das Modell Rückgabetyp und Parameter aus verschiedenen überladenen Methoden derselben API kombinierte.
*   **Konsequenz:** Methoden-Überladungen sind auf maximal 3 beschränkt. Überladungen, die sich nur in primitiven Typen bei gleicher Parameteranzahl unterscheiden, sind verboten (fordern explizite Methodennamen).
*   **Einordnung:** Die Quelle ist ein Preprint (Stand dieser Überarbeitung noch nicht begutachtet), kein peer-reviewtes Paper — der Befund ist thematisch direkt einschlägig, aber mit entsprechend geringerer Evidenzstärke zu werten. Eine zuvor hier zitierte Quelle ("DAPLab, Kategorie 4: Data Management") wurde entfernt: Kategorie 4 behandelt laut Volltext Datenbank-Schema-Unkenntnis, nicht Methodenüberladung — die frühere Zuordnung verfehlte das tatsächliche Thema der Quelle.
*   **Referenz:** *Wu et al. (2024). "A Comprehensive Framework for Evaluating API-oriented Code Generation in Large Language Models". arXiv:2409.15228 (Preprint).*

#### 9. Navigations-Hygiene & Feature-Ordner (`EnforceNamespaceDirectoryMapping` / `MaxDirectoryDepth`)
*   **Wissenschaftlicher Hintergrund:** Das passive Durchsuchen großer, verstreuter Klassenstrukturen flutet das Kontextfenster mit irrelevanten Informationen (*Context Rot*). Zudem treiben tiefe Ordnerpfade die Anzahl und Latenz von Agenten-Navigationsbefehlen (`cd`, `ls`) in die Höhe.
*   **Konsequenz:** Der Namespace muss exakt der physischen Ordnerstruktur (Feature Folder) entsprechen; die Ordnertiefe ab csproj wird auf maximal 4 begrenzt.
*   **Einordnung:** Chroma Research belegt den allgemeinen Context-Rot-Effekt (nicht-uniforme Leistungsdegradation bei wachsender Token-Zahl, getestet an 18 Modellen), nicht spezifisch Ordnertiefe oder `cd`/`ls`-Navigationslatenz — die Übertragung auf Verzeichnisstruktur-Regeln ist eine plausible, aber nicht direkt in der Quelle belegte Konsequenz.
*   **Referenz:** 
    * *Chroma Research (2025). "Context Rot: How Increasing Input Tokens Impacts LLM Performance".*
    * *Arize AI (2026). "Context management in agent harnesses".*

#### 10. Referenz-Grounding (`DetectAndBanPhantomDependencies`)
*   **Wissenschaftlicher Hintergrund:** LLMs neigen dazu, Paket-Abhängigkeiten oder Klassen zu halluzinieren, die in der realen Codebasis nicht existieren ("Package Hallucination"). Eine Analyse von 576.000 generierten Code-Samples über 16 Modelle fand, dass 19,7 % aller referenzierten Pakete halluziniert waren (Open-Source-Modelle 21,7 %, kommerzielle Modelle 5,2 %). Bannen von ungelösten Namespace-using-Statements und dynamischer Reflection zwingt die KI zur Compile-Zeit-Verifizierung und verhindert solche Phantom-Referenzen.
*   **Konsequenz:** Der Import von Namespaces, die Roslyn im Kompilierungskontext nicht auflösen kann, sowie String-basierte Reflection (`Type.GetType`) sind verboten.
*   **Einordnung:** Eine zuvor hier zitierte Quelle ("Scale AI, SWE Atlas") wurde entfernt: SWE Atlas misst laut offizieller Beschreibung Codebase-QnA, Test-Writing und Refactoring, keine Halluzinations-Rate bei Paket-/Klassenreferenzen — die frühere Zuordnung verfehlte das tatsächliche Thema der Quelle.
*   **Referenz:** *Spracklen et al. (2025). "We Have a Package for You! A Comprehensive Analysis of Package Hallucinations by Code Generating LLMs". USENIX Security Symposium 2025.*

#### 11. Kontextabhängige Metrik-Gewichtung (`CompoundSuppressions`)
*   **Wissenschaftlicher Hintergrund:** Palomba et al. (2018) zeigen empirisch, dass Klassen mit mehreren gleichzeitig auftretenden Code-Smells überproportional fehleranfälliger sind als Klassen mit nur einem oder keinem Smell: Klassen mit drei gleichzeitigen Smells sind bis zu 350 % change-anfälliger und 300 % fehleranfälliger als smell-freie Klassen im selben System; eine Folgeuntersuchung derselben Autorengruppe zu Smell-Co-Occurrence findet, dass Klassen mit mehr als einem Smell bis zu 350 % change-anfälliger und 100 % fehleranfälliger sind als Klassen mit nur einem Smell. Das allgemeine Muster — Kombinationen von Qualitätsproblemen sind überproportional riskanter als isolierte Einzelfunde — überträgt AiNetLinter analog auf die Kombination LOC+Komplexität, auch wenn Palomba et al. nicht genau diese beiden Metriken, sondern Kombinationen verschiedener Code-Smell-Typen untersuchen. NDepend löst ein verwandtes Problem seit Jahren über CQL (Code Query Language), indem LOC-Schwellenwerte nur dann gelten, wenn CC ebenfalls kritisch ist.
*   **Konsequenz für AI-Readability:** Lange, semantisch flache Methoden (DI-Setup, Builder-Chains, Enum-zu-String-Tabellen) sind für LLMs trivial erfassbar — das „Lost in the Middle"-Problem entsteht erst bei verschachtelter Komplexität. False Positives aus `MaxMethodLineCount` erzwingen künstliches Refactoring, das den Code schlechter lesbar macht.
*   **Einordnung (Gyimothy et al.):** Die Studie validiert mehrere OO-Metriken (u. a. CBO, WMC, LOC) einzeln als Fehler-Prädiktoren gegen Fault-Daten aus Mozilla — CBO schneidet am besten ab, LOC am zweitbesten. Sie untersucht diese Metriken nicht in Kombination (LOC+Komplexität); die Übertragung auf eine kombinierte Schwelle ist plausibel, aber nicht direkt aus der Studie ableitbar.
*   **Referenz:**
    * *Palomba, F., Bavota, G., Di Penta, M., Fasano, F., Oliveto, R. & De Lucia, A. (2018). "On the diffuseness and the impact on maintainability of code smells: a large scale empirical investigation". Empirical Software Engineering 23(3), 1188–1221.*
    * *Palomba, F. et al. (2018). "A Large-Scale Empirical Study on the Lifecycle of Code Smell Co-occurrences". Information and Software Technology 99, 1–10.*
    * *Gyimóthy, T., Ferenc, R. & Siket, I. (2005). "Empirical Validation of Object-Oriented Metrics on Open Source Software for Fault Prediction". IEEE Transactions on Software Engineering 31(10), 897–910.*

#### 12. Selektive Severity-Herabstufung (`CompoundSuppression.SeverityOverride`)

*   **Wissenschaftlicher Hintergrund:** Das NASA Systems Engineering Handbook (2016) definiert unter dem Begriff **"Acceptable Risk"** (Anhang B; siehe auch Abschnitt 6.4 zur Risiko-Mitigation), dass ein verstandenes, einvernehmlich akzeptiertes Restrisiko keine weitere Mitigation erfordert. Übertragen auf Lint-Verstöße: Nicht jede Metrik-Verletzung trägt dasselbe Risiko. Ein strukturell flacher, aber langer Initialisierer (z. B. DI-Setup) ist keine Architekturverletzung, sondern eine legitime Entwurfsentscheidung mit geringerem Risikoprofil als dieselbe Zeilenzahl kombiniert mit hoher Komplexität (siehe Regel 11).
*   **Konsequenz:** `SeverityOverride: "warning"` erlaubt es, solche Violations im Output des Agenten sichtbar zu halten (Informationswert), ohne den CI-Build zu blockieren (kein Exit-Code 1). Der Agent sieht die Violation, kann aber entscheiden, ob Handlungsbedarf besteht.
*   **Einordnung:** Für den konkreten Schwellenwert (CC≤3/CogC≤5 als Kriterium für die Herabstufung) gibt es keine direkt passende externe Studie mit einer exakten Prozentzahl — das ist eine Engineering-Kalibrierung, kein empirisch abgeleiteter Wert. Eine zuvor hier zitierte Zahl ("~70 % niedrigere Defektwahrscheinlichkeit bei CC≤3", zugeschrieben an Palomba et al. 2018) ließ sich im Volltext des Papers nicht finden und wurde entfernt — Palomba et al. untersuchen Kombinationen von Code-Smell-Typen (siehe Regel 11), keine CC/CogC-Schwellenwert-Bins. Der allgemeine Zusammenhang zwischen Komplexität und Fehleranfälligkeit ist in der Software-Engineering-Literatur dokumentiert (u. a. Gyimothy et al., 2005), eine auf CC≤3 vs. CC>5 bezogene Studie mit dieser exakten Zahl wurde jedoch nicht gefunden.
*   **Referenz:** *NASA Office of the Chief Engineer (2016). "NASA Systems Engineering Handbook". NASA/SP-2016-6105, Anhang B ("Acceptable Risk") und Abschnitt 6.4.*

#### 13. Restore-Erkennung statt Auto-Restore (`ProjectRestoreState`)

*   **Problem:** `MSBuildWorkspace.OpenSolutionAsync` läuft als Design-Time-Build (`DesignTimeBuild=true`, `SkipCompilerExecution=true`, siehe `LinterEngine.CreateWorkspaceProperties()`) und führt — anders als `dotnet build` — **keinen impliziten NuGet-Restore** aus. Ist ein Zielprojekt nicht restored (`obj/project.assets.json` fehlt oder ist veraltet), bleiben externe Paketreferenzen im geladenen Roslyn-Workspace unauflösbar. `DetectAndBanPhantomDependencies` meldete das früher als tausende Einzel-Violations (ein Fund pro unauflösbarem `using`) und ließ den `safeguard`-Score einer sonst fehlerfreien Solution auf 0 einbrechen — ein reiner Folgefehler des Lade-Zustands, kein echter Befund.
*   **Entscheidung (Option 2 — Erkennen & klar diagnostizieren, nicht automatisch restoren):** `ProjectRestoreState.NeedsRestore` prüft rein dateisystembasiert (kein Netzwerk, kein Prozessstart), ob `obj/project.assets.json` fehlt oder älter als die `.csproj` ist. Bei Treffer meldet `LinterEngine.ReportRestoreDiagnostics` **einmal pro betroffenem Projekt** eine klare `PROJECT_NOT_RESTORED`-Diagnose samt `dotnet restore`-Hinweis — statt automatisch einen Restore auszulösen. Gegen Option 1 (automatischer Restore vor dem Laden) spricht das Projektprinzip „Wenig Magic" (`AiNetLinterRichtlinien.mdc` §1): ein Linter/MCP-Server, der beim Lesen einer Solution ungefragt einen netzwerkabhängigen, potenziell lang laufenden Seiteneffekt auslöst (Schreibzugriffe unter `obj/`, Fehlschlag bei fehlender Konnektivität, unvorhersagbare Laufzeit für einen residenten MCP-Prozess), ist genau der versteckte Mechanismus, den das Prinzip ausschließt. Erkennen statt Ausführen bleibt deterministisch, offline-fähig und lässt die Entscheidung („wann restoren") beim Menschen/Agenten statt beim Linter.
*   **Konsequenz für den Checker:** `DetectAndBanPhantomDependencies` (konkret `PhantomDependencyChecker.CheckPhantomNamespace`) prüft zusätzlich `CheckerContext.ProjectHasLoadDiagnostics` — gespeist aus `ProjectRestoreState.ComputeProjectsNeedingRestore`, pro Projekt granular (nicht mehr nur das globale `SourceFileCatalog.HasLoadingErrors`-Bool für die gesamte Solution). Ein nicht restoretes Projekt A unterdrückt so keine Funde in einem sauber geladenen Projekt B derselben Solution. Die Berechnung erfolgt direkt aus der `Solution` (nicht aus dem `SourceFileCatalog`), weil mehrere MCP-Tools (`get_violations`, `safeguard`, `pattern_detect`, `metrics_tree`) `LinterEngine.RunAsync(Solution, …)` ohne Catalog aufrufen — eine rein Catalog-basierte Lösung hätte diese Tools nicht erreicht.
*   **Referenz:** Bug-Report gegen ein externes, per MCP gelintetes Projekt (nicht restored, `dotnet build` mit Exit 0) — `safeguard`-Score fiel auf 0,00/10 durch tausende Phantom-Folgefehler statt einer einzigen, actionable Diagnose.

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
