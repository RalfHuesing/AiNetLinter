# Research-Agent: AiNetLinter Feature Audit

**Ausgabesprache:** Ausschließlich Deutsch (Quellentitel dürfen englisch bleiben)  
**Arbeitsverzeichnis:** `Research\FeatureAudit\` (relativ zum Repo-Root)  
**Fortschritts-Log:** `Task.md` — nach jedem abgeschlossenen Item Checkbox auf `[x]` setzen

---

## 1. Kontext: Was ist AiNetLinter?

AiNetLinter ist ein .NET 10 CLI-Tool, das C#-Codebases per Roslyn-Syntaxanalyse auf Qualitätsregeln prüft. Der zentrale Unterschied zu anderen Lintern: Die Regeln sind nicht primär für menschliche Entwickler optimiert, sondern für **LLM-Agenten** wie Claude Code, Cursor oder GitHub Copilot. Ziel ist es, die Fehlerrate autonomer Agenten beim Bearbeiten von C#-Code zu senken — "AI-Readability".

Die zu evaluierenden Features stammen aus zwei Quellen:
- `rules.json` — konfigurierbare numerische Metriken und Boolean-Schalter
- CLI-Parameter — Workflow-Features wie `--baseline`, `--fix`, `--list-rules`

Alle Features sind in `FeatureList.md` vollständig beschrieben. Lies diese Datei **bevor** du mit Phase 2 beginnst.

---

## 2. Verzeichnisstruktur

```
Research\FeatureAudit\
  Task.md                  ← Dein Fortschritts-Log (Checkboxen abhaken!)
  Prompt.md                ← Diese Datei
  FeatureList.md           ← Alle 46 Features mit Metadaten (INPUT)
  Extensions\              ← Spätere Zusatzfragen (siehe Abschnitt 9)
  temp\papers\             ← Deine Paper-Zusammenfassungen aus Phase 1 (OUTPUT)
  Result\metrics\          ← Bewertungsdokumente Metriken (OUTPUT, Phase 2)
  Result\bool-rules\       ← Bewertungsdokumente Boolean-Regeln (OUTPUT, Phase 3)
  Result\features\         ← Bewertungsdokumente System-Features (OUTPUT, Phase 4)
  Result\new-features\     ← Vorschläge für neue Features (OUTPUT, Phase 6)
  Result\index.md          ← Gesamtmatrix (OUTPUT, Phase 5)
```

---

## 3. Ablauf — Reihenfolge zwingend einhalten

### PHASE 1 zuerst: Paper-Bibliothek aufbauen

**Warum Phase 1 vor allem anderen?**  
Papers und Studien werden von mehreren Features gemeinsam genutzt. Statt bei jedem Feature neu zu suchen (ineffizient, inkonsistent), baust du in Phase 1 eine Bibliothek aus Zusammenfassungen auf. In Phase 2–4 liest du die relevanten Zusammenfassungen und referenzierst sie — du suchst nur dann neu, wenn ein konkretes Feature etwas braucht, das kein Cluster abdeckt.

Du erstellst **7 Cluster-Dateien** in `temp\papers\`. Format → Abschnitt 4.

---

#### Cluster A — Komplexitätsmetriken
**Datei:** `temp\papers\papers-A-komplexitaet.md`

**Was suchen:**
- McCabe, T.J. (1976): "A Complexity Measure" — Originalpaper zu Cyclomatic Complexity
- Halstead-Metriken (1977) und deren empirische Validierung/Kritik
- Campbell, G.A.: "Cognitive Complexity — A new way of measuring understandability" (SonarSource, 2018) — Unterschied zu McCabe
- Empirische Studien: Korrelation Cyclomatic Complexity ↔ Defektdichte (Basili, Shepperd, Gill)
- Metaanalysen zur Validität beider Metriken (2015–2026): Wann versagen sie?
- Welche Grenzwerte empfehlen Forscher und Tool-Hersteller (SonarQube, NDepend, Visual Studio)?

**Suchqueries:**
```
cyclomatic complexity defect density empirical study correlation
cognitive complexity vs cyclomatic complexity empirical comparison
McCabe complexity threshold recommended value
SonarSource cognitive complexity whitepaper Campbell 2018
Halstead software metrics empirical validation criticism
code complexity bug density meta-analysis
```

---

#### Cluster B — Datei- und Methodengrößen
**Datei:** `temp\papers\papers-B-groessen.md`

**Was suchen:**
- "Lost in the Middle" (Liu et al., 2023): LLM Attention bei langen Dokumenten — Kernaussagen und Grenzen
- Neuere Studien (2024–2026): Verbessern große Kontextfenster das Problem, oder bleibt es bestehen?
- Empirische Studien zur optimalen Dateigröße (LOC): Ab wann sinkt Wartbarkeit?
- Palomba et al. (2018): Korrelation Methodenlänge mit Fehleranfälligkeit
- Microsoft Research, Google Engineering: Empfehlungen zu Datei- und Methodengrößen
- Trade-off-Analysen: Lange Dateien vs. starke Fragmentierung — was schadet LLM-Agenten mehr?
- Ergebnisse aus RAG-Systemen: Welche Chunk-Größen funktionieren am besten?

**Suchqueries:**
```
"lost in the middle" long context LLM attention Liu 2023
LLM long context window code understanding 2024 2025
optimal file size lines of code maintainability study
method length bug density Palomba 2018
code fragmentation LLM navigation retrieval augmented generation
large context window GPT-4 Claude Gemini code comprehension
file size code quality empirical study
```

---

#### Cluster C — LLM-Agenten & Code-Qualität (2023–2026)
**Datei:** `temp\papers\papers-C-llm-agenten.md`

**Was suchen:**
- SWE-bench (Jimenez et al., 2023/2024): Ergebnisse, Analyse warum Agenten scheitern, Muster bei Fehlern
- SWE-bench Verified / SWE-bench Multimodal: neuere Versionen
- Anthropic: Veröffentlichungen zu Claude Agents und Code-Qualität
- OpenAI: Research zu GPT-4o und Code-Generierung, bekannte Schwächen
- Microsoft Research: GitHub Copilot Studien, Auswirkung von Code-Struktur auf Completion-Qualität
- Was macht Code für LLMs schwer? (Komplexität, Kopplung, Namensgebung, Kontextgröße)
- LLM-Halluzinationen bei Code: Typen, Ursachen, Häufigkeit
- Agentic Coding Best Practices 2024–2026 (Anthropic, LangChain, AutoGen Literatur)
- Studien zum Einfluss von Code-Konventionen auf LLM-Korrektheit

**Suchqueries:**
```
SWE-bench agentic coding results analysis 2024 2025 agent failure
LLM code hallucination types causes patterns study
Anthropic Claude code quality agentic workflow best practices
GitHub Copilot code structure completion quality study
AI coding agent failure analysis code complexity
LLM code understanding coupling cohesion study
code conventions LLM correctness impact
agentic coding framework best practices 2024
```

---

#### Cluster D — C#-Idiome & .NET Best Practices
**Datei:** `temp\papers\papers-D-csharp.md`

**Was suchen:**
- Microsoft .NET Design Guidelines: sealed, nullable, naming, out-Parameter, nested types
- C# Language Specification und offizielle Empfehlungen zu sealed classes
- Nullable Reference Types (C# 8+): Empirische Daten zur Null-Reference-Exception-Reduzierung
- Performance-Impact von `sealed` in .NET (JIT devirtualization — konkrete Benchmarks)
- `dynamic` in C#: Bekannte Probleme, Empfehlungen gegen den Einsatz
- `out`-Parameter: Wann sinnvoll, wann Code Smell?
- Result Pattern vs. Exceptions: Erfahrungsberichte und Community-Konsens (2020–2026)
- Value Objects / Records: DDD-Empfehlungen, C# record-Typ
- XML-Dokumentation: Aufwand vs. Nutzen in der Praxis
- PascalCase und Naming: .NET Coding Conventions, Auswirkung auf Lesbarkeit

**Suchqueries:**
```
C# sealed class performance JIT devirtualization benchmark
nullable reference types C# 8 null exception reduction study
C# dynamic keyword problems avoid recommendation
out parameters C# best practice when to use
result pattern vs exception handling C# comparison
value objects C# records DDD domain driven design
C# naming conventions PascalCase code readability
XML documentation code quality cost benefit
.NET design guidelines recommendations sealed nullable
```

---

#### Cluster E — Architekturmetriken
**Datei:** `temp\papers\papers-E-architektur.md`

**Was suchen:**
- DIT (Depth of Inheritance Tree): Chidamber & Kemerer (1994) CK-Metriken, empirische Validierung
- CBO (Coupling Between Objects): Studien zu Kopplung und Defektdichte
- LCOM (Lack of Cohesion of Methods): Kohäsions-Metriken und ihre Aussagekraft
- Constructor Injection: Empfehlungen zur maximalen Abhängigkeitsanzahl (DI-Prinzipien)
- Public API Surface (Anzahl öffentlicher Member): Studien zu API-Größe und Wartbarkeit
- Namespace-Organisation: Empfehlungen zu Verzeichnistiefe und -strukturierung
- Partial Classes: Anti-Pattern oder legitimes Feature? Empirische Belege
- ForbiddenNamespaceDependencies / Architektur-Enforcement-Tools (NDepend, ArchUnit, NetArchTest)

**Suchqueries:**
```
CK metrics Chidamber Kemerer inheritance depth defect density study
coupling between objects CBO software quality defect
cohesion LCOM software metrics empirical
constructor injection maximum dependencies best practice
public API surface maintainability study
namespace directory structure organization best practice
partial class C# antipattern legitimate use
architecture enforcement tools NDepend ArchUnit .NET
```

---

#### Cluster F — Code Smells & Fehleranfälligkeit
**Datei:** `temp\papers\papers-F-smells.md`

**Was suchen:**
- Fowler (1999): "Refactoring" — klassische Code-Smell-Definitionen
- Palomba et al.: Empirische Studien Code Smells ↔ Fehleranfälligkeit
- Yamashita & Moonen: Welche Smells sind tatsächlich problematisch?
- Boolean Parameters als Code Smell: Empirische Belege, "Boolean Trap"
- Switch-Statement-Komplexität: Wann ist ein Switch ein Code Smell?
- Empty / Silent Catch: Auswirkungen auf Systemstabilität und Debuggbarkeit
- Semantic Naming / Identifier-Qualität: Studien zum Einfluss auf Code-Verständnis (Schankin et al., Butler et al.)
- Magic Numbers / Nested Types: Bekannte Smells und ihre Evidenzlage

**Suchqueries:**
```
boolean parameter trap code smell empirical study
switch statement complexity code smell refactoring
empty catch silent exception antipattern impact stability
semantic naming identifiers code comprehension study Schankin Butler
code smell defect density empirical Palomba Yamashita
code smell bug correlation empirical meta-analysis
identifier naming quality software maintenance study
```

---

#### Cluster G — Test-Coverage & Testbarkeit
**Datei:** `temp\papers\papers-G-tests.md`

**Was suchen:**
- Test-Coverage und tatsächliche Bug-Reduktion: Metaanalysen (Zhu et al., Inozemtseva & Holmes 2014)
- "Does Code Coverage Matter?" — neuere Studien (2020–2026)
- Welche Klassen/Methoden brauchen wirklich Tests? (risikobasierte Coverage-Strategien)
- Testbarkeit als Design-Kriterium: Studien zum Einfluss von Testbarkeit auf Code-Qualität
- Test-Coverage-Enforcement-Tools: Wie machen es andere Tools? (SonarQube, NDepend, Coverlet)
- Cognitive Complexity als Prädiktor für Test-Notwendigkeit: Gibt es Belege?

**Suchqueries:**
```
test coverage defect detection effectiveness study meta-analysis
code coverage bug reduction empirical Inozemtseva Holmes 2014
which classes need unit tests risk based coverage strategy
testability software design quality empirical study
cognitive complexity test priority metric
test sentinel coverage enforcement tool comparison
```

---

#### Cluster H — Meta-Hypothese: Verbessert AiNetLinter-Compliance tatsächlich die Agenten-Performance?
**Datei:** `temp\papers\papers-H-meta-hypothese.md`

**Hintergrund:** AiNetLinters zentrale These ist: "Code der unseren Regeln entspricht, macht LLM-Agenten fehlerärmer und effizienter." Diese These wird als Grundlage für alle 46 Features vorausgesetzt — aber wurde sie empirisch belegt? Das ist die wichtigste Querschnittsfrage des gesamten Audits. Dieser Cluster wird zuerst in Phase 2 gelesen, bevor irgendein Feature evaluiert wird.

**Was suchen:**
- Direkte Studien: Korreliert Code-Qualität (gemessen durch gängige Metriken) mit LLM-Agenten-Fehlerrate?
- SWE-bench-Analysen: Gibt es Auswertungen welche Code-Eigenschaften erfolgreiche vs. gescheiterte Agent-Runs unterscheiden?
- Microsoft/GitHub: Gibt es Copilot-interne Daten oder veröffentlichte Studien zu diesem Zusammenhang?
- Anthropic: Gibt es Hinweise aus dem Claude-Engineering zu "was macht Code für Agenten leichter"?
- Falls keine direkten Belege: Welche indirekten Ketten gibt es? (z.B. "geringere Komplexität → weniger Missverständnisse → weniger Halluzinationen")
- Gibt es Gegenevidenz? Studien die zeigen dass Agenten mit schlechtem Code genauso gut umgehen?
- Welche Benchmarks oder Messmethoden wären geeignet um die These selbst zu testen?

**Suchqueries:**
```
code quality metrics LLM agent performance correlation study
SWE-bench code complexity agent success rate analysis
GitHub Copilot code structure suggestion accuracy study
clean code LLM code generation correctness empirical
does code quality improve AI coding assistant performance
code readability LLM comprehension empirical evidence
agent coding performance code metrics correlation 2024 2025
```

**Wichtiger Hinweis für die Evaluation:** Falls du keine direkten Belege findest — halte das klar fest. "Keine direkte Evidenz gefunden" ist eine valide und wichtige Aussage. Leite dann aus indirekten Ketten ab und kennzeichne das als "(Ableitung)".

---

### PHASE 2–4: Features evaluieren

**Vor dem Start:** `FeatureList.md` vollständig lesen.  
**Dann:** Features in der Reihenfolge der `Task.md` abarbeiten.

**Für jedes Feature:**
1. Paper-Cluster aus `FeatureList.md` (`Relevante Paper-Cluster:`) lesen
2. Bei echten Lücken: ergänzende Websuche — aber sparsam, Cluster sollten 90% abdecken
3. Result-Datei an den genannten Pfad schreiben (Templates → Abschnitt 5)
4. Checkbox in `Task.md` abhaken: `- [ ]` → `- [x]`

---

### PHASE 5: Gesamtindex

Nach allen 46 Features: `Result\index.md` schreiben (Template → Abschnitt 6).

---

### PHASE 6: Neue Feature-Vorschläge

Nachdem du alle bestehenden Features bewertet hast, führe eine synthetisierende Recherche durch: **Welche C#-Code-Muster sind NICHT von AiNetLinter abgedeckt, verursachen aber nachweislich Probleme für LLM-Agenten oder korrelieren mit Bugs?**

**Ablauf:**
1. Nutze die bereits gesammelten Paper-Cluster aus Phase 1 — suche gezielt nach Lücken
2. Ergänzende Suche nach neuen Patterns die in der bestehenden Literatur auftauchten aber kein passendes Feature haben
3. Erstelle `Result\new-features\proposals.md` (Template → Abschnitt 5d)
4. Für jeden Vorschlag mit starker Evidenz (≥ 2 unabhängige Quellen): eigene Datei `Result\new-features\N[XX]-[Name].md`

**Fokus-Bereiche für Phase 6:**
- Async/await-Anti-Patterns (`async void`, `.Wait()`, `.Result`-Property auf Tasks, ConfigureAwait-Fehler)
- LINQ-Komplexität (lange, verschachtelte Chains als Lesbarkeits-Problem für Agenten)
- Magic Numbers / Magic Strings (keine symbolischen Namen für Konstanten)
- Generics-Komplexität (verschachtelte Generics, die LLM-Reasoning erschweren)
- Fehlende Muster die SWE-bench-Analysen als Halluzinations-Ursache nennen
- Alles was Paper-Cluster C als "LLM-Schwäche" nennt, für die AiNetLinter noch keine Regel hat

**Suchqueries für Phase 6:**
```
async void C# antipattern LLM code generation problem
LINQ complexity readability code comprehension study
magic numbers magic strings code quality LLM
generic type complexity code comprehension
C# antipatterns LLM hallucination code generation
code smell LLM agent failure SWE-bench analysis
```

Checkbox in `Task.md` nach Abschluss: Phase 6 N00 + individuelle Vorschläge.

---

## 4. Template: Paper-Cluster-Zusammenfassung

Speicherort: `temp\papers\papers-[X]-[slug].md`

```markdown
# Paper-Cluster [X]: [Titel]

Erstellt: [Datum]  
Betrifft Features: [z.B. M01, M02, M04, M05, R01]

---

## Gefundene Quellen

### [Autor, Jahr] — [Titel]
- **Fundort:** [URL oder DOI oder "via Web-Suche: [Query]"]
- **Betrifft AiNetLinter-Features:** [IDs aus FeatureList.md]
- **Kernaussagen:**
  - [Kernaussage 1]
  - [Kernaussage 2]
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - [z.B. "CC > 10 korreliert mit 2.4× höherer Defektdichte"]
- **Einschränkungen dieser Quelle:** [Alter, Stichprobengröße, Programmiersprache, Kontext]
- **Zeitliche Einordnung:** [Erscheinungsjahr; sind die Erkenntnisse zeitstabil oder modellgenerations-spezifisch? Kurze Begründung.]

### [nächste Quelle]
...

---

## Übergreifende Erkenntnisse

[Was ist der gemeinsame Tenor? Was ist Konsens, was ist umstritten? Was fehlt in der Forschung?]

## Nicht gefunden / Lücken

[Was wurde gesucht aber nicht gefunden? Wo gibt es keine belastbare Evidenz?]
```

---

## 5. Templates: Result-Dokumente

### 5a. Template für Numerische Metriken (M01–M17)

```markdown
# [Metrik-Name] ([ID])

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** [Wert] | **Severity:** [error/warning] | **Status:** [aktiv/deaktiviert]  
**Paper-Cluster genutzt:** [z.B. A, B, C]

---

## Bewertung

🟢 **WERTVOLL** / 🟡 **UNPRAKTIKABEL** / 🔴 **NUTZLOS**

**Fazit:** [Ein Satz. Klar und handlungsleitend. Beispiel: "Grenzwert zu eng — 900 Zeilen sind besser empirisch begründet; unter 500 Zeilen erzeugt kontraproduktive Fragmentierung."]

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | X | [Warum darunter keine Wirkung?] |
| **Empfehlung (beste Evidenz)** | Y | [Stärkste Quelle dafür] |
| **Obergrenze (Nutzen geht verloren)** | Z | [Ab wann bringt der Grenzwert nichts mehr?] |
| **Aktueller Wert** | [Wert] | [Einordnung: zu eng / angemessen / zu locker] |

---

## Wissenschaftliche Grundlage

[2–4 Absätze. Was sagen die Studien konkret? Was ist empirisch belegt, was ist Konvention oder Heuristik? Wo gibt es Widersprüche zwischen Quellen?]

## KI-Agenten-Perspektive

[Was bedeutet dieser Grenzwert speziell für LLM-Agenten (Claude Code, Cursor, Copilot)? Gibt es Evidenz aus dem LLM-Bereich — oder muss aus allgemeinen Prinzipien abgeleitet werden?]

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos / Modellgeneration-spezifisch / Offen

| Begriff | Bedeutung |
|---------|-----------|
| Zeitlos | Das Problem ist struktureller Natur — auch deutlich bessere Modelle werden davon betroffen sein |
| Modellgeneration-spezifisch | Das Problem existiert bei Modellen 2024–2025, wird sich mit besseren Modellen abschwächen |
| Offen | Unklar, ob strukturell oder temporär |

[1–2 Sätze Begründung: Warum ist diese Einschätzung zutreffend? Falls Offen: Was müsste man wissen, um es zu entscheiden?]

---

## Empfehlung

**Aktion:** [Wert beibehalten | Wert auf X anpassen | Deaktivieren | Entfernen]  
**Begründung:** [1–2 Sätze, direkt handlungsleitend]

---

## Quellen

- [Autor, Jahr, Titel — URL/DOI]
- ...
```

---

### 5b. Template für Boolean-Regeln (R01–R20)

```markdown
# [Regel-Name] ([ID])

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** [true/false] | **Status:** [aktiv/deaktiviert]  
**Severity:** [error/warning/n.a.]  
**Paper-Cluster genutzt:** [z.B. D, F]

---

## Bewertung

🟢 **WERTVOLL** / 🟡 **UNPRAKTIKABEL** / 🔴 **NUTZLOS**

**Fazit:** [Ein Satz. Beispiel: "Klar belegt — sealed classes verbessern JIT-Performance und reduzieren LLM-Halluzinationen durch eindeutigere Typhierarchien; Behalten."]

---

## Empfehlung

**Aktion:** [Aktiviert lassen | Aktivieren | Deaktivieren | Aus dem Tool entfernen]  
**Begründung:** [1–2 Sätze]

---

## Wissenschaftliche / Empirische Grundlage

[Was sagen Studien, offizielle Microsoft/C#-Guidelines, Industriestandards? Ist die Regel Community-Konsens oder umstritten?]

## KI-Agenten-Perspektive

[Wie beeinflusst diese Regel die Arbeit von LLM-Agenten? Reduziert sie Halluzinationen, verbessert sie Kontextlesbarkeit, oder ist der Effekt neutral?]

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos / Modellgeneration-spezifisch / Offen

[1–2 Sätze Begründung: Ist dieser Effekt strukturell in LLMs verankert, oder löst ihn ein GPT-6 / Claude 5 auf?]

## Risiken / Gegenargumente

[Was spricht gegen die Regel? Gibt es bekannte Szenarien wo sie Schaden anrichtet? Wie verbreitet ist das Gegenargument?]

---

## Quellen

- [Autor, Jahr, Titel — URL/DOI]
- ...
```

---

### 5c. Template für System- und CLI-Features (F01–F09)

```markdown
# [Feature-Name] ([ID])

**Kategorie:** [CLI-Feature / Konfigurationsfeature]  
**CLI-Flag / Konfiguration:** [--flag oder rules.json → Pfad]  
**Status:** [vorhanden/aktiv/leer]

---

## Bewertung

🟢 **WERTVOLL** / 🟡 **UNPRAKTIKABEL** / 🔴 **NUTZLOS**

**Fazit:** [Ein Satz. Beispiel: "Ratchet-Mechanismus ist State of the Art für Legacy-Integration — alle führenden Linter bieten dies, Behalten."]

---

## Empfehlung

**Aktion:** [Beibehalten | Erweitern | Vereinfachen | Entfernen]  
**Begründung:** [1–2 Sätze]

---

## Nutzen-Analyse

[Was leistet dieses Feature konkret? Für welche Szenarien ist es wertvoll, für welche irrelevant?]

## Vergleich: Andere Tools

[Wie lösen ESLint, SonarQube, StyleCop, Roslyn Analyzers, NDepend dasselbe Problem? Ist AiNetLinters Ansatz vergleichbar, besser oder schlechter?]

## KI-Agenten-Perspektive

[Wie interagiert ein LLM-Agent mit diesem Feature? Erleichtert es die agentic Integration, oder ist es nur für menschliche Entwickler relevant?]

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos / Modellgeneration-spezifisch / Offen

[1–2 Sätze: Bleibt dieses Feature auch in 3 Jahren relevant, oder macht es ein besseres Modell obsolet?]

---

## Quellen

- [Autor, Jahr, Titel — URL/DOI]
- ...
```

---

### 5d. Template für Neue Feature-Vorschläge (N01–N..)

```markdown
# [Feature-Name] ([ID])

**Kategorie:** Neuer Feature-Vorschlag  
**Typ:** [Numerische Metrik | Boolean-Regel | CLI-Feature]  
**Vorgeschlagener rules.json-Schlüssel:** [z.B. MaxLinqChainLength]  
**Implementierungsaufwand:** [Gering / Mittel / Hoch]

---

## Bewertung

🟢 **EMPFOHLEN** / 🟡 **PRÜFEN** / 🔴 **NICHT EMPFOHLEN**

**Fazit:** [Ein Satz. Warum sollte dieses Feature implementiert werden — oder nicht?]

---

## Was würde dieses Feature tun?

[Präzise Beschreibung: Was wird geprüft? Bei welchem Schwellwert/Zustand schlägt es an?]

## Evidenz: Warum ist das Problem real?

[Was sagen die Papers dazu? Konkrete Belege dass das beschriebene Muster tatsächlich Probleme für LLM-Agenten oder für Code-Qualität verursacht.]

## Abgrenzung zu bestehenden Features

[Warum deckt kein bestehendes Feature diesen Fall ab? Wo ist der Unterschied?]

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos / Modellgeneration-spezifisch / Offen

[Würde dieses Feature in 3 Jahren noch relevant sein?]

## Implementierungshinweis

[Was müsste technisch umgesetzt werden? Welche Roslyn-API wäre geeignet? Grobe Abschätzung der Umsetzbarkeit im bestehenden AiNetLinter-Architektur-Stil (monolithisch, statisch kompiliert, kein DI).]

---

## Quellen

- [Autor, Jahr, Titel — URL/DOI]
- ...
```

Zusätzlich: `Result\new-features\proposals.md` ist eine Übersichtsdatei mit allen Vorschlägen als kompakte Tabelle und Zusammenfassung — schreibe sie als erstes in Phase 6, bevor du individuelle N[XX]-Dateien erstellst.

---

## 6. Template: Gesamtindex (Result\index.md)

```markdown
# AiNetLinter Feature Audit — Gesamtübersicht

Erstellt: [Datum]  
Evaluierte Features: 46 (17 Metriken, 20 Boolean-Regeln, 9 System-Features)  
Neue Feature-Vorschläge: [Anzahl]

---

## Zusammenfassung

| Bewertung | Anzahl |
|-----------|--------|
| 🟢 Wertvoll | X |
| 🟡 Unpraktikabel | X |
| 🔴 Nutzlos | X |

**Sofort handeln (höchste Priorität):**
1. [Feature ID + Empfehlung — ein Satz]
2. ...

**Mittelfristig prüfen:**
1. ...

**Neue Features mit stärkster Evidenz:**
1. ...

---

## Matrix: Metriken

| ID | Feature | Aktuell | Bewertung | Zeitlich | Empfehlung |
|----|---------|---------|-----------|----------|------------|
| M01 | MaxLineCount | 700 | 🟢/🟡/🔴 | Zeitlos/Spezifisch/Offen | [kurz] |
| ... | | | | | |

---

## Matrix: Boolean-Regeln

| ID | Feature | Aktuell | Bewertung | Zeitlich | Empfehlung |
|----|---------|---------|-----------|----------|------------|
| R01 | EnforceSealedClasses | true | 🟢/🟡/🔴 | Zeitlos/Spezifisch/Offen | [kurz] |
| ... | | | | | |

---

## Matrix: System-Features

| ID | Feature | Bewertung | Zeitlich | Empfehlung |
|----|---------|-----------|----------|------------|
| F01 | Baseline/Ratchet | 🟢/🟡/🔴 | Zeitlos/Spezifisch/Offen | [kurz] |
| ... | | | | |

---

## Matrix: Neue Feature-Vorschläge

| ID | Vorschlag | Typ | Priorität | Aufwand |
|----|-----------|-----|-----------|---------|
| N01 | [Name] | [Metrik/Regel/CLI] | 🟢/🟡/🔴 | Gering/Mittel/Hoch |
| ... | | | | |

---

## Offene Fragen

[Was konnte nicht abschließend beurteilt werden? Was benötigt praktische Messung statt nur Literaturrecherche?]

## Empfohlene nächste Schritte

[Konkrete Aktionen als Checklist]
- [ ] ...
```

---

## 7. Qualitätsanforderungen

| Anforderung | Details |
|-------------|---------|
| **Sprache** | Ausschließlich Deutsch. Quellentitel/URLs dürfen englisch bleiben. |
| **Klarheit** | Jedes Fazit in einem Satz. Kein "es kommt darauf an" ohne sofortige Auflösung. |
| **Belege** | Mindestens 2 Quellen pro Feature. Mindestens 1 Quelle aus 2020–2026. |
| **Aktualität** | Bei Widersprüchen zwischen Quellen: neuere Quelle hat Vorrang; ältere Quelle trotzdem nennen und einordnen. Ein 1980er-NASA-Paper zählt nur wenn die Erkenntnisse heute noch unbestrittener Konsens sind — dann als "zeitstabile Grundlage" markieren. |
| **Zeitlichkeit** | Jede Bewertung muss angeben ob das Problem struktureller Natur in LLMs ist (= bleibt) oder ob es sich mit besseren Modellen abschwächen wird. |
| **Ehrlichkeit** | Wenn keine Evidenz existiert: klar sagen. Keine erfundenen Quellen. Ableitung aus Prinzipien als solche kennzeichnen: "(Ableitung, kein direktes Paper)". |
| **KI-Fokus** | Immer die LLM-Agenten-Perspektive einbeziehen — das ist der Kern von AiNetLinter. |
| **Neue Features** | Schlage nur vor, was im bestehenden Architekturstil (monolithisch, statisch, kein DI, Roslyn-Analyse) umsetzbar ist. Keine Cloud-Features, keine Laufzeit-Instrumentierung. |
| **Fortschritt** | Nach jeder abgeschlossenen Datei sofort `Task.md` aktualisieren. |

---

## 8. Startbefehl

Lies diese Datei vollständig. Beginne dann mit **Phase 1, Cluster A** (`temp\papers\papers-A-komplexitaet.md`).  
Arbeite die Phasen sequenziell ab. Starte Phase 2 erst wenn alle 7 Paper-Cluster-Dateien existieren. Starte Phase 6 erst wenn Phase 5 (Index) abgeschlossen ist.

---

## 9. Erweiterbarkeit — Nachträgliche Fragen integrieren

Der Audit ist so strukturiert, dass spätere Zusatzfragen integriert werden können, ohne alles neu zu machen.

### Prinzip

Jede Zusatzfrage folgt demselben Muster:
1. Prüfe ob bereits ein Paper-Cluster in `temp\papers\` existiert, der die Frage abdeckt
2. Falls ja: direkt die Frage beantworten mit Referenz auf den Cluster
3. Falls nein: neuen Cluster `papers-[H/I/...]-[slug].md` erstellen; dann die Frage beantworten

### Wie du eine neue Frage hinzufügst

1. Erstelle `Extensions\[Datum]-[slug].md` nach dem Template in `Extensions\README.md`
2. Trage dort die Frage, den benötigten Paper-Cluster (neu oder bestehend) und die gewünschten Output-Dateien ein
3. Bearbeite die Extension wie eine zusätzliche Phase — eigene Checkboxen, eigene Result-Dateien

### Was NICHT neu gemacht werden muss

- Paper-Cluster A–G bleiben erhalten und können weiter referenziert werden
- Bestehende Result-Dateien bleiben unverändert — eine neue Frage ergänzt sie, überschreibt sie nicht
- `Task.md` muss nicht geändert werden — die Extension hat ihre eigene Checklist

### Typische Zusatzfragen

- "Sollten wir Severity-Level der Regeln überarbeiten?"
- "Welche Regeln gelten nicht für Blazor-Projekte?"
- "Wie verhalten sich unsere Grenzwerte im Vergleich zu SonarQube-Defaults?"
- "Gibt es Regeln die sich gegenseitig widersprechen oder redundant sind?"
