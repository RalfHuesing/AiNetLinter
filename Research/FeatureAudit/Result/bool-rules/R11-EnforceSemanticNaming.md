# EnforceSemanticNaming (R11)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true | **Status:** Aktiv  
**Severity:** error  
**Paper-Cluster genutzt:** D, C, F

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Empirisch klar belegt — bedeutungslose Bezeichner (`data`, `temp`, `result`, `obj`, `info`, `value`) verlangsamen menschliches Code-Verständnis nachweislich und verwirren LLM-Tokenizer; die Regel adressiert zudem das beobachtete Muster, dass LLM-Agenten ohne Constraints genau diese generischen Namen bevorzugen.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** Zwei unabhängige empirische Quellen (Schankin et al. 2018, Butler et al. 2009/2010) belegen den negativen Einfluss nichtssagender Namen auf Codequalität; Du et al. (2025) ergänzt die LLM-Dimension. Die Verbotsliste (`data`, `temp`, `obj`, `result`, `info`, `value`) trifft exakt die Bezeichner, die LLMs ohne Kontextbindung am häufigsten erzeugen.

---

## Wissenschaftliche / Empirische Grundlage

**Schankin et al. (2018 — ICPC)** untersuchten in einem Web-Experiment mit 88 Java-Entwicklern, ob beschreibende zusammengesetzte Bezeichner das Code-Verständnis verbessern. Entwickler mit beschreibenden Namen fanden einen semantischen Defekt im Schnitt 14 % schneller. Das Ergebnis ist statistisch signifikant, obwohl die Stichprobengröße moderat ist und die Studie im Lab-Setting stattfand.

**Butler et al. (2009/2010 — The Open University)** analysierten 8 etablierte Open-Source-Java-Bibliotheken mit 12 Bezeichner-Namensrichtlinien. Bezeichner, die mindestens eine Richtlinie verletzten, korrelierten statistisch signifikant mit statisch detektierten Codequalitätsproblemen. Eine Erweiterung (2010) bestätigte die Assoziation auf Methoden-Ebene.

**Empirical LLM Code Smell Analysis (2024/2025)** zeigt, dass LLMs ohne projektspezifisches Constraint Engineering konsistent auf generische Bezeichner wie `data`, `temp`, `result` zurückfallen — exakt die Wörter auf AiNetLiners Verbotsliste. Dies erzeugt eine selbstverstärkende Degradierungsspirale: LLM erzeugt generischen Code → Linter greift nicht ein → nächste LLM-Iteration orientiert sich am schlechten Bestand.

Die Ausnahmen (`Equals`, `CompareTo`, `GetHashCode`) sind fachlich korrekt: Diese Methodennamen sind durch C#-Konventionen und Interface-Verträge (`IEquatable<T>`, `IComparable<T>`) fest vorgegeben und semantisch eindeutig durch ihren Kontext.

## KI-Agenten-Perspektive

**Du et al. (2025)** belegt empirisch, dass inkonsistente und nichtssagende Bezeichner die Tokenisierung und die Attention-Pfade von LLMs (Claude 3.5 Sonnet, GPT-4o, Gemini 2.0 Flash) nachweislich stören — messbar durch sinkende BLEU- und SBERT-Scores bei nachfolgenden Generierungsaufgaben. Konkret: Ein Bezeichner wie `result` gibt einem LLM keinen Hinweis auf Typ, Kontext oder Invarianten; ein Bezeichner wie `parsedUserCommand` kondensiert diese Information direkt in den Token.

Zudem zeigen Studien zur LLM-generierten Code-Qualität (Cluster C, "Empirical Agent Framework Studies"), dass **Project Context Conflicts** die häufigste Halluzinationsquelle sind. Bedeutungslose Namen verstärken genau dieses Problem: Das Modell kann nicht aus dem Namen ableiten, welches Objekt gemeint ist, und halluziniert Zusammenhänge.

Die Regel wirkt als Qualitätsrückkopplung für LLM-Agenten: Ein Linter-Fehler auf `result` zwingt den Agenten, einen kontextspezifischen Namen zu wählen — was das nächste Codegenerations-Fenster verbessert.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Semantische Informationsdichte in Bezeichnern verbessert das Verständnis sowohl für Menschen als auch für Transformer-basierte Sprachmodelle auf einer strukturellen Ebene: Attention-Mechanismen nutzen Token-Ko-Okkurrenzen im Trainingskorpus, und bedeutungsarme Bezeichner haben dort deutlich schwächere Signale. Auch deutlich stärkere Modelle werden von semantisch informierten Namen profitieren, da diese die Wahrscheinlichkeitsverteilung über mögliche Code-Fortsetzungen effektiver einschränken.

## Risiken / Gegenargumente

**Falsch-Positive bei domänenspezifischen Ausnahmen:** Bestimmte Domänen nutzen etablierte Konventionen, die mit der Verbotsliste kollidieren könnten (z.B. `value` in Value-Object-Mustern: `public T Value { get; }`). AiNetLinters Substring-Matching-Ansatz könnte hier zu restriktiv sein — `CustomerValue` wäre erlaubt, aber `Value` als Property-Name einer Wrapper-Klasse würde anschlagen, obwohl es semantisch klar ist.

**Implementierungsaufwand für legitime kurze Namen:** In Iterator-nahem Code oder in Performance-kritischen Transformationsschleifen sind kurze Namen (`x`, `n`) Standard; für öffentliche Signaturen sind diese Fälle jedoch selten und die Regel beschränkt sich ohnehin auf öffentliche Schnittstellen.

Der Gegenargument-Raum ist insgesamt schmal; die Verbotsliste ist eng und trifft ein reales Muster.

---

## Quellen

- Schankin et al., 2018, "Descriptive Compound Identifier Names Improve Source Code Comprehension" — https://dl.acm.org/doi/10.1145/3196321.3196332
- Butler et al., 2009/2010, "Relating Identifier Naming Flaws and Code Quality" — https://www.researchgate.net/publication/224079441
- Du et al., 2025, "The Hidden Cost of Readability: How Code Formatting Silently Affects LLMs" — https://arxiv.org/abs/2503.17407
- Empirical LLM Code Smell Analysis, 2024/2025 — via Web-Suche
