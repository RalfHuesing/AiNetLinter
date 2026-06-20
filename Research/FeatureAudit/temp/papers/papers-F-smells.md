# Paper-Cluster F: Code Smells & Fehleranfälligkeit

Erstellt: 2026-06-20  
Betrifft Features: M03 (MaxMethodParameterCount), M11 (MaxBoolParameterCount), M15 (MaxSwitchArms), R02 (AllowDynamic), R03 (AllowOutParameters), R13 (EnforceNoSilentCatch), R11 (EnforceSemanticNaming)

---

## Gefundene Quellen

### Fowler & Beck, 1999 — Refactoring: Improving the Design of Existing Code
- **Fundort:** via Web-Suche: "Fowler refactoring code smell classification long method feature envy"; Addison-Wesley
- **Betrifft AiNetLinter-Features:** M03, M11, M15, R03, R13
- **Kernaussagen:**
  - Klassifikation von 22 Code-Smell-Typen als kanonische Referenz
  - "Long Method" und "Long Parameter List" sind explizit als Smells definiert; zu viele Parameter (>4–5 gilt als Warnsignal) verringern Verständlichkeit
  - "Switch Statements" als eigener Smell: Wiederholte Switch-Blöcke deuten auf fehlende Polymorphie hin
  - "Inappropriate Intimacy" und "Feature Envy" als strukturelle Smells
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine formalen Schwellenwerte im Buch; Fowler empfiehlt "mehr als 3–4 Parameter → refaktorieren"
- **Einschränkungen dieser Quelle:** Normativ, keine empirische Studie; Java-fokussiert; 1999 verfasst (Neuauflage 2018 mit modernen Patterns)
- **Zeitliche Einordnung:** 1999 (Erstauflage), 2018 (2. Auflage); klassisch zeitstabil als konzeptuelle Grundlage

### Palomba et al., 2017 — On the Diffuseness and the Impact on Maintainability of Code Smells
- **Fundort:** https://link.springer.com/article/10.1007/s10664-017-9535-z; Empirical Software Engineering (Springer)
- **Betrifft AiNetLinter-Features:** M03, M11, M15, R13
- **Kernaussagen:**
  - Großangelegte empirische Untersuchung: 30 Open-Source-Projekte, 395 Releases, 17.350 validierte Code-Smell-Instanzen, 13 Smell-Typen
  - "Smelly Classes" haben signifikant höhere Change- und Fault-Proneness als smell-freie Klassen
  - Komplexe/lange Code-Smells (z.B. Complex Class, Long Method) sind besonders verbreitet und besonders problematisch
  - God Class erhöht Change-Proneness um ca. 28 %, Spaghetti Code um ca. 21 %
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - God Class: +28 % Change-Proneness; Spaghetti Code: +21 % Change-Proneness
  - 395 Releases analysiert; statistisch signifikante Ergebnisse
- **Einschränkungen dieser Quelle:** Java-/Python-Projekte; Übertragung auf C# plausibel aber nicht direkt belegt; Open-Source-Bias
- **Zeitliche Einordnung:** 2017; aktuell; zeitstabil hinsichtlich OO-Code-Smells

### Yamashita & Moonen, 2013 — Exploring the Impact of Inter-Smell Relations on Software Maintainability
- **Fundort:** via Web-Suche: "code smell defect density empirical Palomba Yamashita meta-analysis"; IEEE ICSM 2013
- **Betrifft AiNetLinter-Features:** M03, M15
- **Kernaussagen:**
  - Quantitative und qualitative Studie: Welche Code-Smells tatsächlich Wartungsprobleme verursachen
  - Inter-Smell-Relationen (Smells die gemeinsam auftreten) verstärken Wartungsprobleme signifikant
  - Long Parameter List und Long Method korrelieren oft mit weiteren Smells
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine direkten Schwellenwerte; qualitative Bewertung durch Entwickler-Interviews
- **Einschränkungen dieser Quelle:** Kleine Stichprobe (Entwicklerinterviews); kombiniert quantitativ und qualitativ
- **Zeitliche Einordnung:** 2013; zeitstabil

### Schankin et al., 2018 — Descriptive Compound Identifier Names Improve Source Code Comprehension
- **Fundort:** https://dl.acm.org/doi/10.1145/3196321.3196332; Proceedings of ICPC 2018
- **Betrifft AiNetLinter-Features:** R11 (EnforceSemanticNaming)
- **Kernaussagen:**
  - Web-basierte Studie mit 88 Java-Entwicklern: Auftrag, semantischen Defekt in Code-Snippets zu finden
  - Entwickler mit beschreibenden, zusammengesetzten Bezeichnernamen fanden den Defekt ca. 14 % schneller
  - Kurze, nicht-beschreibende Namen verlangsamen Code-Verständnis messbar
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - 14 % schnellere Defektlokalisierung bei beschreibenden Namen
- **Einschränkungen dieser Quelle:** Java; kleine Stichprobe (88 Entwickler); Lab-Setting, keine Produktion
- **Zeitliche Einordnung:** 2018; zeitstabil

### Butler et al., 2009/2010 — Relating Identifier Naming Flaws and Code Quality: An Empirical Study
- **Fundort:** https://www.researchgate.net/publication/224079441_Relating_Identifier_Naming_Flaws_and_Code_Quality_An_Empirical_Study; The Open University
- **Betrifft AiNetLinter-Features:** R11 (EnforceSemanticNaming)
- **Kernaussagen:**
  - 8 etablierte Open-Source-Java-Bibliotheken ausgewertet; 12 Bezeichner-Namens-Richtlinien angewandt
  - Fehlerhafte Bezeichner (verletzen mindestens eine Richtlinie) korrelieren statistisch signifikant mit statischen Code-Quality-Problemen (FindBugs)
  - Erweiterung der Studie (2010): Assoziation gilt auch auf Method-Ebene und mit breiteren Qualitäts-/Lesbarkeitsmetriken
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Statistisch signifikante Assoziation (p-Werte in Originalstudie; genaue Werte im Paper)
- **Einschränkungen dieser Quelle:** Java; Open-Source; statische Code-Qualitätstools als Proxy für Defekte (keine direkten Bug-Reports)
- **Zeitliche Einordnung:** 2009/2010; zeitstabil

### Wirfs-Brock et al. / Industrie-Konsens — Boolean Parameter Trap
- **Fundort:** via Web-Suche: "boolean parameter trap code smell empirical study"; Medium/Dev.to Artikel; Clean-Code-Literatur
- **Betrifft AiNetLinter-Features:** M11 (MaxBoolParameterCount)
- **Kernaussagen:**
  - Boolean-Flags in Funktionsparametern gelten weit verbreitet als Code Smell ("Boolean Trap")
  - Ein boolean-Flag signalisiert, dass eine Funktion mehr als eine Aufgabe erledigt (Verletzung SRP)
  - Mehrere boolean-Flags multiplizieren die versteckten Pfade (2^n Kombinationen), was Testen und Lesen erschwert
  - Empfehlung: Aufsplitten in separate Methoden oder Enum/Strategy-Pattern verwenden
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine formale Studie mit Schwellenwert; Fowler (1999) empfiehlt generell: Flag-Parameter → Refactoring-Kandidat
- **Einschränkungen dieser Quelle:** Keine dedizierte empirische Studie; Konsens aus Clean-Code-Literatur und Blog-Artikeln
- **Zeitliche Einordnung:** Langfristig konsistenter Konsens; keine neue formelle Studie 2020–2026 gefunden

### Harness.io / Rigerta Demiri, 2022 — Swallowed Exceptions: The Silent Killer of Java Applications
- **Fundort:** https://www.harness.io/blog/swallowed-exceptions-java-applications; via Web-Suche: "empty catch silent exception antipattern impact stability software"
- **Betrifft AiNetLinter-Features:** R13 (EnforceNoSilentCatch)
- **Kernaussagen:**
  - Ca. 20 % der Fehler erscheinen nie in Logs, wenn Exceptions "verschluckt" werden
  - Silent Exceptions verhindern Debugging und Monitoring, führen zu nicht erklärbaren Nutzer-Erlebnissen
  - Fehlerbehandlungs-Antipatterns entstehen oft durch Abwesenheit expliziter Exception-Handling-Richtlinien
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - "Ca. 20 % der Fehler nie in Logs" (Aussage in Artikel; keine Quellenangabe für diese Zahl)
- **Einschränkungen dieser Quelle:** Artikel ohne Peer-Review; Java-fokussiert; 20-%-Zahl nicht belegt
- **Zeitliche Einordnung:** 2022; aktuell

### Casalnuovo et al. (via Springer) — Studying the Evolution of Exception Handling Anti-Patterns in a Long-Lived Large-Scale Project
- **Fundort:** https://link.springer.com/article/10.1186/s13173-019-0095-5; Journal of the Brazilian Computer Society, 2019
- **Betrifft AiNetLinter-Features:** R13 (EnforceNoSilentCatch)
- **Kernaussagen:**
  - Längsschnittstudie zu Exception-Handling-Antipatterns in einem großen kommerziellen Java-Projekt über mehrere Jahre
  - Antipatterns (leere Catch-Blöcke, Catch-Exception-Catch-Throwable) sind persistent und nehmen ohne aktive Gegenmaßnahmen zu
  - Entwickler-Awareness und explizite Richtlinien reduzieren Antipattern-Prävalenz
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Quantitative Zunahme der Antipatterns über Projektzeit belegt; genaue Zahlen im Paper
- **Einschränkungen dieser Quelle:** Ein einzelnes kommerzielles Projekt; Java; Generalisierbarkeit eingeschränkt.
- **Zeitliche Einordnung:** 2019; zeitstabil

### Vartolomei & Craciun, via arxiv 2024 — On the Prevalence, Evolution, and Impact of Code Smells in Simulation Modelling Software
- **Fundort:** https://arxiv.org/pdf/2409.03957; arXiv 2024
- **Betrifft AiNetLinter-Features:** M03, M11, M15
- **Kernaussagen:**
  - Aktuelle Studie (2024): Code Smells bleiben weit verbreitet auch in modernen wissenschaftlichen Software-Projekten
  - Switch-Statement-Smell und Long Parameter List unter häufig identifizierten Smells
  - Smells korrelieren mit höherer Bug-Dichte in angemerkten Commit-Daten
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Konkrete Zahlen in Originalpaper; Studie zeigt Persistenz von Smells über Projektlaufzeit
- **Einschränkungen dieser Quelle:** Domain-spezifisch (Simulationssoftware); begrenzte Generalisierbarkeit
- **Zeitliche Einordnung:** 2024; aktuell

### Du et al. / arXiv, 2025 — The Hidden Cost of Readability: How Code Formatting Silently Affects LLMs
- **Fundort:** https://arxiv.org/html/2503.17407; via Web-Suche
- **Betrifft AiNetLinter-Features:** R11 (EnforceSemanticNaming), R09 (EnforcePascalCase)
- **Kernaussagen:**
  - Die syntaktische Strukturierung und Benennungskonformität (z.B. einheitliche Bezeichner-Case-Konventionen) beeinflusst die Code-Verständnisleistung von LLMs maßgeblich.
  - Inkonsistenzen bei Variablen- und Methodennamen (wie Abweichungen vom plattformüblichen PascalCase) führen zu Tokenisierungsfehlern und mindern die Präzision der Attention-Pfade im LLM.
  - Das Einhalten strenger Code-Konventionen (z.B. PascalCase, semantische Namen) verbessert die Generierungsqualität und die semantische Ähnlichkeit (SBERT) zum Zielcode signifikant.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Signifikante Einbußen der semantischen Ähnlichkeit (SBERT-Scores) und Funktionsübereinstimmung bei inkonsistenten Namens- und Formatierungskonventionen (getestet mit Modellen wie Claude 3.5 Sonnet, GPT-4o, Gemini 2.0 Flash).
- **Einschränkungen dieser Quelle:** Die Whitespace-Formatierung an sich stört LLMs weniger; kritisch sind vor allem unstrukturierte Benennungen und inkonsistente Case-Konventionen.
- **Zeitliche Einordnung:** 2025; aktuell und zeitstabil bezüglich Transformer-Attention-Mechanismen.

---

## Übergreifende Erkenntnisse

Die empirische Evidenz für Code Smells als Defektprädiktoren ist solide, aber variantenreich. Palomba et al. (2017) liefert die stärkste großangelegte Bestätigung. Fowler (1999/2018) ist die konzeptuelle Grundlage, aber keine Studie.

Für **LLM-Agenten** gewinnen Namenskonventionen und Code Smells eine neue Bedeutung:
1. **R11 (Semantic Naming) und R09 (PascalCase):** Die Studie von Du et al. (2025) liefert den empirischen Beleg, dass unstrukturierte Bezeichnernamen und inkonsistente Gehäuseschreibweise (Case) Tokenizer und Attention-Pfade verwirren. Dies führt zu Leistungseinbußen beim Verständnis und der Codegenerierung.
2. **Boolean-Parameter-Trap (M11):** Hier fehlt eine dedizierte empirische Studie; der Konsens leitet sich aus dem SRP-Prinzip und der Clean-Code-Literatur ab. 
3. **Switch-Statement-Komplexität (M15):** Die Evidenz ist über Cyclomatic-Complexity-Studien indirekt vorhanden, da verzweigte Switch-Strukturen die Pfaddivergenz (LM-CC) massiv erhöhen.
4. **Silent-Catch (R13):** Ist als Antipattern weit anerkannt; die Evidenz ist überwiegend aus Praktiker-Beobachtungen und einer Längsschnittstudie (Casalnuovo 2019) — kein kontrolliertes Experiment mit Fehlerrate.

## Nicht gefunden / Lücken

- Keine dedizierte empirische Studie ausschließlich zu "Boolean Parameter Trap" mit Schwellenwerten.
- Keine Studie speziell zu MaxSwitchArms (Armanzahl) als eigenständige Metrik — nur indirekte Evidenz über CC.
- Keine C#-spezifischen Studien zu diesen Smells; alle klassischen Studien sind Java-lastig.
- Direkte Messungen darüber, wie sich das Verbot von `out`-Parametern oder `dynamic` spezifisch auf die Fehlerrate von KI-Codegenerierung auswirkt, fehlen.
- Für R02 (AllowDynamic) und R03 (AllowOutParameters): Keine empirischen Studien gefunden; nur Microsoft-Designempfehlungen.
