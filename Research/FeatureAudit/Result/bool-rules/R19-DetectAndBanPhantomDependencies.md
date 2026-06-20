# DetectAndBanPhantomDependencies (R19)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true | **Status:** Aktiv  
**Severity:** error  
**Paper-Cluster genutzt:** C, D

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Diese Regel adressiert die häufigste und messbar schädlichste Halluzinationsquelle von LLM-Agenten in realen Projekten direkt — nicht-auflösbare `using`-Direktiven und halluzinierte `Activator.CreateInstance`-Aufrufe sind Symptome von "Project Context Conflicts" und die Regel macht diese Fehler sofort sichtbar, statt sie bis zur Compilezeit zu verzögern.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** Phantom-Dependencies sind nach allen vorliegenden Studien zu LLM-Code-Generierung die häufigste Einzelfehlerquelle; die Regel erzwingt unmittelbares Feedback im Linter-Lauf, bevor der Agent weiteren Schaden anrichten kann.

---

## Wissenschaftliche / Empirische Grundlage

**Liu et al. (2025 — "LLM Hallucinations in Practical Code Generation")** klassifizieren Halluzinationen in drei Kategorien: Task Requirement Conflicts, Factual Knowledge Conflicts und **Project Context Conflicts**. Letztere sind die häufigste Kategorie in echten Projekten: Der Agent macht falsche Annahmen über existierende Klassen, Methoden-Signaturen oder Abhängigkeiten im Projekt.

Konkret: Ein Agent schreibt `using MyCompany.Services.Auth;` obwohl dieser Namespace nicht existiert — oder ruft `Activator.CreateInstance(typeof(SomeService))` für einen Typ auf, der ihm halluziniert vorschwebt aber nicht vorhanden ist. Diese Fehler werden üblicherweise erst beim Build erkannt, nach oft mehreren weiteren Agent-Iterationsschritten die auf der fehlerhaften Annahme aufgebaut haben.

**Empirical Agent Framework Studies (arXiv:2511.00872, 2604.03515):** Repository- und Importfehler machen über 50 % der Gesamtausfälle bei komplexen Repositories aus. **Nicht-auflösbare Importe** sind das häufigste Symptom: Der Agent hat eine Klasse oder einen Namespace aus dem Gedächtnis oder aus ähnlichem Code abgeleitet, ohne zu prüfen, ob er im Projekt existiert.

**Microsoft .NET Design Guidelines:** Das `Activator.CreateInstance`/`Type.GetType`-Pattern für Anwendungstypen ist ein bekanntes Anti-Pattern, das die statische Typprüfung (eines der Kernvorteile von C#) umgeht. In DI-Container-freien Architekturen (wie AiNetLinter selbst) ist dieses Pattern für App-Typen nicht legitim.

## KI-Agenten-Perspektive

R19 ist die **direkteste Anti-Halluzinations-Regel** im gesamten Tool. Sie transformiert eine Klasse von LLM-Fehlern (Phantom-Dependencies) von einem Build-Time-Fehler zu einem Linter-Error, der im Edit-Loop des Agenten unmittelbar sichtbar wird.

Aus Cluster C ("On the Impact of AGENTS.md Files", arXiv:2601.20404): Wenn ein Agent über sofortige Tool-Feedback-Loops verfügt (wie einen CLI-Linter mit direkter Fehlermeldung), sinkt seine Fehlerrate messbar. R19 ist genau ein solcher Feedback-Mechanismus: Der Agent versucht, einen Typ zu nutzen, der Linter schlägt an, der Agent korrigiert — ohne dass ein vollständiger Build nötig ist.

**Besonders wertvoll** ist das Verbot von `Type.GetType(string)` und `Activator.CreateInstance(string)` für App-Typen: Diese Patterns treten im LLM-generierten Code auf, wenn ein Agent "sicher" ist, dass ein Typ existiert — es aber nicht verifizieren kann. Das Verbot zwingt zur Nutzung statisch aufgelöster Typen, die Roslyn und der Compiler prüfen können.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Phantom-Dependencies sind ein strukturelles Problem, das aus der Diskrepanz zwischen dem Trainingskorpus eines LLMs und dem spezifischen Projekt-Kontext entsteht. Diese Diskrepanz bleibt bei allen Modellgenerationen bestehen — kein LLM kann vollständige Kenntnis über eine projektspezifische Codebasis haben, ohne explizit darüber informiert zu werden. Die Regel eliminiert die negativen Konsequenzen dieser strukturellen Limitation.

## Risiken / Gegenargumente

**False Positives bei Reflection-Patterns:** In bestimmten Szenarien (Plugin-Systeme, dynamisches Laden, Serialisierung) ist `Type.GetType(string)` legitim. AiNetLiners Ausnahme für "App-Typen" vs. Framework-Typen ist korrekt — aber die Trennlinie kann in der Praxis schwer zu ziehen sein.

**Generierter Code mit Forward-Declarations:** Source-Generator-Code kann Typen referenzieren, die erst nach der Generierung existieren. FileFilters (F07) mit `[GeneratedCode]`-Ausnahme löst diesen Fall.

**Abhängigkeit von Roslyn-Semantic-Model:** Die Auflösungsprüfung von `using`-Direktiven erfordert vollständigen Semantic-Model-Zugriff, nicht nur Syntaxanalyse. Dies ist implementierungsintensiver — aber die Mühe ist es wert.

Das Verhältnis aus Aufwand und Wirkung ist bei R19 besser als bei jeder anderen Regel: Phantom-Dependencies sind häufig, sofort schädlich und durch die Regel direkt adressierbar.

---

## Quellen

- Liu et al., 2024/2025, "LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550 / https://dl.acm.org/doi/epdf/10.1145/3728894
- arXiv:2511.00872, 2024–2026, "A Comprehensive Empirical Evaluation of Agent Frameworks"
- arXiv:2604.03515, 2025/2026, "Inside the Scaffold: Agent Failure Taxonomy"
- Microsoft .NET Design Guidelines — Reflection: https://learn.microsoft.com/en-us/dotnet/fundamentals/reflection/reflection
