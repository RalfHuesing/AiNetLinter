# Paper-Cluster C: LLM-Agenten & Code-Qualität (2023–2026)

Erstellt: 2026-06-20  
Betrifft Features: M01, M02, M04, M05, M06, M07, M08, M09, M10, M12, M13, M14, M17, R01–R20, F01–F09

---

## Gefundene Quellen

### Jimenez et al. (2023/2024) — SWE-bench: Can Language Models Resolve Real-World GitHub Issues?
- **Fundort:** ACL Anthology, 2025: https://aclanthology.org/2025.acl-long.189.pdf
- **Betrifft AiNetLinter-Features:** Alle Features (globale Kontextfrage)
- **Kernaussagen:**
  - SWE-bench ist das Standard-Benchmark zur Bewertung von LLM-Agenten bei der autonomen Behebung realer GitHub-Issues.
  - Mit der Weiterentwicklung der LLMs stieg die Lösungsrate auf der bereinigten Version (SWE-bench Verified) bis 2025/2026 auf bis zu ~75% an.
  - Das ursprüngliche Benchmark zeigt jedoch Sättigungseffekte und enthielt fehlerhafte Tests/Lecks, weshalb Nachfolger wie **SWE-bench Pro** und **SWE-MERA** etabliert wurden.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - ~75% Lösungsrate auf SWE-bench Verified (Flagship-Modelle 2025/2026).
  - ~40% Lösungsrate auf dem komplexeren Full SWE-bench.
- **Einschränkungen dieser Quelle:** Die absoluten Lösungsraten veralten durch neue Modelle rasch, die Relevanz von Repositories als Benchmark-Umgebung bleibt jedoch hoch.
- **Zeitliche Einordnung:** 2023–2025.

### Empirical Agent Framework Studies (2024–2026) — Inside the Scaffold: Agent Failure Taxonomy
- **Fundort:** arXiv:2511.00872 (*"A Comprehensive Empirical Evaluation of Agent Frameworks..."*) und arXiv:2604.03515 (*"Inside the Scaffold..."*)
- **Betrifft AiNetLinter-Features:** M01–M17 (Kontext-Metriken), F01–F09 (CLI/System-Features)
- **Kernaussagen:**
  - Systematische Untersuchungen von Coding-Agenten-Fehlern zeigen eine klare Hierarchie der Fehlerursachen:
    1. **Repository-Kontext (am häufigsten):** Agenten scheitern nicht an der Codegenerierung selbst, sondern daran, die korrekte Datei zu finden, Importe korrekt aufzulösen oder die richtige Codestelle zu lokalisieren.
    2. **Fehlende Fehlertoleranz:** Agenten können sich nach einem fehlerhaften Edit oft nicht selbst korrigieren und brechen ab (Timeout/Looping).
    3. **Syntax-/API-Fehler:** Syntaktisch inkorrekter Code oder die Verwendung nicht-existenter APIs (Halluzinationen).
  - Je komplexer und fragmentierter ein Repository ist, desto häufiger treten Fehler der Kategorie 1 (Kontext) auf.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Repository- und Importfehler machen über 50% der Gesamtausfälle bei komplexen Repositories aus.
- **Einschränkungen dieser Quelle:** Hängt stark von der verwendeten Agenten-Scaffolding-Architektur ab.
- **Zeitliche Einordnung:** 2024–2026. Zeitstabile Erkenntnisse über die kognitiven Schwachstellen von Agenten.

### Liu et al. (2024/2025) — LLM Hallucinations in Practical Code Generation: Phenomena, Mechanism, and Mitigation
- **Fundort:** arXiv:2409.20550; ACM Digital Library: https://dl.acm.org/doi/epdf/10.1145/3728894
- **Betrifft AiNetLinter-Features:** M01–M05 (Komplexität), R01–R20 (Code-Regeln)
- **Kernaussagen:**
  - Klassifizierung von Code-Halluzinationen in drei Hauptbereiche: *Task Requirement Conflicts*, *Factual Knowledge Conflicts* und *Project Context Conflicts*.
  - **Project Context Conflicts** (fehlerhafte Annahmen über existierende Klassen, Methoden-signatures oder Abhängigkeiten im Projekt) sind die häufigste Halluzinationsart in echten Projekten.
  - Komplexe Datenstrukturen und hohe Kopplung erhöhen das Risiko von "Mapping-Halluzinationen" (Modell versteht Typ-Zusammenhänge falsch).
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - RAG-basierte Kontextbereitstellung und die Durchsetzung von expliziten Typ-Annotationen reduzieren Halluzinationen signifikant.
- **Einschränkungen dieser Quelle:** Wurde primär in industriellen Großprojekten untersucht.
- **Zeitliche Einordnung:** 2024–2025.

### Du et al. / arXiv (2025) — The Hidden Cost of Readability: How Code Formatting Silently Affects LLMs
- **Fundort:** arXiv:2503.17407 / https://xiaoningdu.github.io/assets/pdf/format.pdf
- **Betrifft AiNetLinter-Features:** R09 (EnforcePascalCase), R11 (EnforceSemanticNaming), Code-Konventions-Regeln
- **Kernaussagen:**
  - Die Studie belegt empirisch, dass die syntaktische Lesbarkeit und Formatierung von Code die semantische Verständnisleistung von LLMs maßgeblich beeinflusst.
  - Getestete Flagship-Modelle (Claude 3.5 Sonnet, GPT-4o, Gemini 2.0 Flash, DeepSeek-V3) zeigen bei schlecht formatiertem oder inkonsistent benanntem Code signifikante Leistungseinbußen beim Code-Verständnis und nachfolgenden Code-Generierungsaufgaben.
  - Das Einhalten von Code-Konventionen (wie PascalCase in C# oder semantische Variablenbenennung) verbessert nachweislich die Übereinstimmung der Modell-Generate mit dem Zielcode.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Signifikante Verschlechterung der Metriken BLEU (Code-Übereinstimmung) und SBERT (semantische Ähnlichkeit) bei Verstößen gegen Formatierungs- und Namenskonventionen.
- **Einschränkungen dieser Quelle:** Syntaktische Whitespaces an sich stören LLMs kaum; es sind vielmehr unstrukturierte Benennungen und inkonsistente Case-Konventionen, die Tokenizer und Aufmerksamkeitspfade verwirren.
- **Zeitliche Einordnung:** 2025. Zeitstabiler Befund.

### arXiv (2025) — On the Impact of AGENTS.md Files on the Efficiency of AI Coding Agents
- **Fundort:** arXiv:2601.20404; via Web-Suche
- **Betrifft AiNetLinter-Features:** F01–F09 (System- und Entdeckungsfeatures)
- **Kernaussagen:**
  - Das Vorhandensein strukturierter Metadaten und Anweisungskomponentdateien im Projekt-Root (z.B. `.cursorrules`, `AGENTS.md` oder ein verständlicher CLI-Hilfe-Output) verbessert die Autonomie und Effizienz von Agenten nachweislich.
  - Wenn ein Agent über Discovery-Befehle (wie `--list-rules` oder `--docs`) die Regeln des Linters explorativ erfragen kann, sinkt die Fehlerrate bei der Erstellung regelkonformer Patches.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Effizienzgewinn und Fehlerreduktion bei der Verwendung standardisierter Scaffold-Regeln sind statistisch messbar.
- **Einschränkungen dieser Quelle:** Hängt von der Systemintegration der LLM-Entwicklungsumgebung ab.
- **Zeitliche Einordnung:** 2025.

---

## Übergreifende Erkenntnisse

**Was macht Code für LLM-Agenten schwer?**
Aus den empirischen Studien der Jahre 2024–2026 lassen sich vier Hauptfaktoren ableiten:
1. **Kontext-Verwirrung (Häufigste Fehlerursache):** Agenten scheitern am ehesten daran, die Struktur einer fremden Codebase zu navigieren. Hohe Kopplung und unübersichtliche Verzeichnisse verschärfen dies.
2. **Namens- und Case-Inkonsistenz:** Inkonsistente Benennung (z.B. Abweichung von PascalCase) führt zu fehlerhafter Tokenisierung und verwirrt die internen Repräsentationen des LLMs, was die Generierungsqualität nachweislich senkt (Du et al., 2025).
3. **Project Context Hallucinations:** Agenten halluzinieren Methoden oder Typen, wenn diese nicht klar deklariert oder schwer auffindbar sind. Statische Typenprüfung (wie C#-Typisierung) hilft, dies zu verhindern.
4. **Mangelndes Feedback:** Ein Agent benötigt direktes Feedback im Edit-Loop. Linter-Befehle, die der Agent selbst ausführen und deren Output er direkt interpretieren kann (z.B. `--fix`, `--list-rules`), sind essenziell für die autonome Fehlerkorrektur.

## Nicht gefunden / Lücken

- Es gibt keine Studien, die den Nutzen spezifischer Linter-Regeln (wie das Verbot von `out`-Parametern) direkt mit LLM-Benchmark-Ergebnissen verknüpfen. Alle derartigen Regeln beruhen auf der Ableitung: "Menschliche Readability-Verbesserung + Reduktion von Codegröße/Komplexität = bessere AI-Readability" (Ableitung).
