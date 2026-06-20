# Paper-Cluster C: LLM-Agenten & Code-Qualität (2023–2026)

Erstellt: 2026-06-20  
Betrifft Features: M01, M02, M04, M05, M14, R01–R20, F01–F09

---

## Gefundene Quellen

### Jimenez et al. (2023/2024) — SWE-bench: Can Language Models Resolve Real-World GitHub Issues?
- **Fundort:** ACL Anthology, 2025: https://aclanthology.org/2025.acl-long.189.pdf; SWE-bench Pro: https://arxiv.org/pdf/2509.16941; via Web-Suche: "SWE-bench agentic coding results 2024 2025 agent failure analysis"
- **Betrifft AiNetLinter-Features:** Alle Features (globale Kontextfrage)
- **Kernaussagen:**
  - SWE-bench misst, ob KI-Agenten reale GitHub-Issues lösen können (echte Code-Patches).
  - Top-Modelle erreichen bis zu ~75% auf SWE-bench Verified (z.B. GPT-5.2, Claude-Sonnet-Varianten).
  - Auf dem schwierigeren Full-SWE-bench: ~40% Lösungsrate.
  - Das Benchmark zeigt Sättigungstendenzen — SWE-bench Pro als Nachfolger adressiert dies.
- **Konkrete Zahlen / Grenzwerte:**
  - ~75% Lösungsrate auf SWE-bench Verified (2025)
  - ~40% auf Full SWE-bench (2025)
- **Einschränkungen:** Benchmark zeigt Kontaminierungsrisiken (Training-Set-Überschneidungen). SWE-bench Verified wurde eingeführt um Quality-Assurance zu verbessern.
- **Zeitliche Einordnung:** 2023–2025. Modellgeneration-spezifisch: Zahlen veralten schnell. Failure-Patterns sind zeitstabiler.

### SWE-agent Paper / Empirical Agent Framework Study (2024/2025) — Agent Failure Taxonomy
- **Fundort:** arXiv:2511.00872 "A Comprehensive Empirical Evaluation of Agent Frameworks on Code-centric Software Engineering Tasks"; arXiv:2604.03515 "Inside the Scaffold: A Source-Code Taxonomy of Coding Agent Architectures"; via Web-Suche: "SWE-bench agentic coding results 2024 2025 agent failure analysis"
- **Betrifft AiNetLinter-Features:** M01–M17 (Code-Struktur direkt relevant), F01–F09
- **Kernaussagen:**
  - **Klassifizierung von Failure-Patterns:**
    1. Falsche Implementierung (Incorrect Implementation)
    2. Zu spezifische Implementierung (Overly Specific)
    3. Fehler nach Edit nicht behoben (Failed to Recover from Edit)
    4. Edit-Position nicht gefunden (Failed to Find Edit Location)
    5. Relevante Datei nicht gefunden (Failed to Find Relevant File)
    6. Vorzeitiger Abbruch (Gave Up Prematurely)
    7. Bug nicht reproduzierbar (Can't Reproduce)
    8. Timeout
  - Neuere Taxonomie (3 Hauptkategorien): (1) Code-Generierungsfehler (Syntax), (2) unzureichende Repo-Kenntnis (Import-Fehler, falsche Variablen), (3) Missbrauch externer APIs.
  - **Kategorie 2 ("Repo-Kenntnis") ist der häufigste Failure-Typ** — LLMs scheitern an Codebase-Kontext, nicht an algorithmischen Problemen.
- **Konkrete Zahlen / Grenzwerte:**
  - (Keine spezifischen Prozentwerte je Failure-Typ; qualitative Taxonomie)
- **Einschränkungen:** Failure-Taxonomien variieren je Studie; methodische Unterschiede.
- **Zeitliche Einordnung:** 2024–2025. Failure-Pattern-Taxonomie ist zeitstabiler als absolute Lösungsraten.

### Liu et al. (2024/2025) — LLM Hallucinations in Practical Code Generation: Phenomena, Mechanism, and Mitigation
- **Fundort:** arXiv:2409.20550; ACM DL: https://dl.acm.org/doi/epdf/10.1145/3728894; via Web-Suche: "LLM code hallucination types causes patterns study 2024"
- **Betrifft AiNetLinter-Features:** M01–M05 (Komplexität/Größe), R01–R20 (Code-Patterns)
- **Kernaussagen:**
  - **Halluzinations-Taxonomie (3 Hauptkategorien, 8 Subkategorien):**
    1. Task Requirement Conflicts (Functional/Non-Functional Violation)
    2. Factual Knowledge Conflicts (Background/Library/API Knowledge)
    3. Project Context Conflicts (Environment/Dependency/Non-code Resource Conflicts)
  - **Primäre Ursachen:** Datenqualitätsprobleme in Training-Corpora; statistische Natur von LLMs (wahrscheinlichstes nächstes Token, nicht korrektstes).
  - "Mapping hallucinations" entstehen durch fehlendes Verständnis von Datentypen und Strukturen — besonders in komplexem Code.
  - **RAG-basierte Mitigation** zeigt konsistente Wirksamkeit über alle getesteten LLMs.
- **Konkrete Zahlen / Grenzwerte:**
  - (Quantitative Häufigkeit je Subkategorie nicht direkt verfügbar; qualitative Dominanz von "Project Context Conflicts")
- **Einschränkungen:** Praktische Code-Generierung in spezifischem Automotive-Kontext; Übertragbarkeit auf allgemeine Softwareentwicklung plausibel aber nicht direkt belegt.
- **Zeitliche Einordnung:** 2024–2025. Ursachen strukturell; Mitigation-Methoden modellgeneration-spezifisch.

### arXiv (2024) — Beyond Functional Correctness: Exploring Hallucinations in LLM-Generated Code
- **Fundort:** arXiv:2404.00971; via Web-Suche: "LLM code hallucination types causes patterns study 2024"
- **Betrifft AiNetLinter-Features:** R01–R20 (Code-Qualitätsmuster)
- **Kernaussagen:**
  - LLM-generierter Code kann funktional korrekt sein, aber trotzdem problematische Halluzinationen enthalten (z.B. nicht-existente API-Calls, falsche Type-Annotations).
  - Halluzinationen in Code betreffen häufig: API-Missbrauch, nicht-existente Methoden, falsche Parameterlisten.
  - Code-Struktur beeinflusst, wie häufig diese Halluzinationen auftreten.
- **Konkrete Zahlen / Grenzwerte:**
  - (Keine spezifischen Zahlen verfügbar in verfügbaren Zusammenfassungen)
- **Einschränkungen:** Fokus auf funktionale Korrektheit-Prüfung, nicht auf Code-Qualitäts-Korrelation.
- **Zeitliche Einordnung:** 2024.

### GitHub Research (2024) — Quantifying GitHub Copilot's Impact on Code Quality
- **Fundort:** GitHub Blog und Visual Studio Magazine: https://github.blog/ai-and-ml/github-copilot/the-road-to-better-completions-building-a-faster-smarter-github-copilot-with-a-new-custom-model/; https://visualstudiomagazine.com/articles/2024/11/22/article_0github-copilot-research-claims-code-quality-gains-in-addition-to-productivity.aspx; via Web-Suche: "GitHub Copilot code structure completion quality study Microsoft Research"
- **Betrifft AiNetLinter-Features:** Alle (systemische Frage)
- **Kernaussagen:**
  - Copilot-assistierter Code zeigt in GitHub-Studie (Nov. 2024): Lesbarkeit +3.62%, Zuverlässigkeit +2.94%, Wartbarkeit +2.47%, Prägnanz +4.16%, Code-Approval-Rate +5%.
  - Andere Studie (GitClear 2024): Code-Klone stiegen um 8× während 2024, Refactoring-Anteil sank von 25% auf <10%.
  - Widersprüchliche Ergebnisse: GitHub-Eigenstudie positiv, unabhängige Studien teils negativ.
  - AI-generierter Code: 1.7× mehr Gesamtprobleme als menschlich geschriebener Code (Qodo, 2025).
  - Trust-Rate gesunken: 29% Vertrauen in KI-Code-Richtigkeit (2025), runter von 40%.
- **Konkrete Zahlen / Grenzwerte:**
  - KI-Code: 1.7× mehr Issues als menschlicher Code
  - Code-Klone: 8× Anstieg 2024 durch KI-Copilot-Nutzung
- **Einschränkungen:** GitHub-Studie ist von Hersteller durchgeführt (Interessenkonflikt). GitClear-Studie analysiert aggregierte Commit-Daten ohne Kausalnachweis.
- **Zeitliche Einordnung:** 2024–2025. Modellgeneration-spezifisch; Trend-Richtung unklar durch widersprüchliche Studien.

### Du et al. / arXiv (2025) — The Hidden Cost of Readability: How Code Formatting Silently Affects LLMs
- **Fundort:** https://xiaoningdu.github.io/assets/pdf/format.pdf; via Web-Suche: "code readability LLM comprehension empirical evidence code conventions LLM performance"
- **Betrifft AiNetLinter-Features:** R09 (EnforcePascalCase), R11 (EnforceSemanticNaming), M01–M05
- **Kernaussagen:**
  - Alle getesteten LLMs (inkl. Claude-3.5-Sonnet, GPT-4o, Gemini-2.0-Flash, DeepSeek-V3, Qwen2.5) zeigen konsistente Leistungseinbußen bei schlecht lesbarem Code.
  - Gemessen in Text-Similarität (BLEU) und Semantischer Ähnlichkeit (SBERT): beide sinken bei schlechter Lesbarkeit.
  - Umgekehrt: Code-Formatierung ist für LLMs teils unnötig — reine Whitespace-Entfernung beeinflusst Performance kaum.
- **Konkrete Zahlen / Grenzwerte:**
  - Konsistente Reduktion in BLEU und SBERT bei schlechter Lesbarkeit (Betrag nicht konkret verfügbar, aber signifikant)
- **Einschränkungen:** Spezifische Test-Settings; Übertragbarkeit auf alle Code-Typen offen.
- **Zeitliche Einordnung:** 2025. Zeitstabiler Befund — Lesbarkeit als LLM-Einflussfaktor ist strukturell.

### arXiv (2025) — Code Readability in the Age of Large Language Models
- **Fundort:** arXiv:2501.11264; via Web-Suche: "code readability LLM comprehension empirical evidence"
- **Betrifft AiNetLinter-Features:** R09, R11, M01–M05
- **Kernaussagen:**
  - LLM-Fixed Code wird von Entwicklern in 68.63% der Fälle als besser lesbar bewertet (35 von 51 Fällen).
  - Schlechte Lesbarkeit korreliert mit schlechterer LLM-Performance beim Code-Verständnis.
  - Gute Lesbarkeit definiert durch: hohe Benennung-Qualität, niedrige zyklomatische Komplexität, angemessene Dokumentation.
- **Konkrete Zahlen / Grenzwerte:**
  - 68.63% der Fälle: LLM-Fixed Code besser lesbar als Original
- **Einschränkungen:** Subjektive Bewertung durch Entwickler; kleine Stichprobe (51 Fälle).
- **Zeitliche Einordnung:** 2025. Zeitstabiler Befund.

### GitClear (2025) — AI Copilot Code Quality: 2025 Data Suggests 4x Growth in Code Clones
- **Fundort:** https://www.gitclear.com/ai_assistant_code_quality_2025_research; via Web-Suche: "AI coding agent performance code quality metrics correlation clean code 2024 2025"
- **Betrifft AiNetLinter-Features:** Systemische Rahmenfrage
- **Kernaussagen:**
  - Copy-Paste-Code (≥5 duplizierte Zeilen) stieg 2024 um Faktor 8.
  - Refactoring-Anteil sank von 25% (2021) auf <10% (2024).
  - AI-assistierter Code führt zu technischer Schuld durch fehlende Kontext-Sensitivität.
  - "Speed without context often leads to code that looks correct initially but introduces redundancy, inconsistency, or hidden flaws."
- **Konkrete Zahlen / Grenzwerte:**
  - 8× Anstieg code duplication, 2.5× Anstieg Code-Clone-Blöcke (≥5 Zeilen) in 2024
- **Einschränkungen:** Aggregierte Commit-Analyse, kein direkter Kausalnachweis für KI-Werkzeuge.
- **Zeitliche Einordnung:** 2024–2025. Modellgeneration-spezifisch; Trend kann sich mit besseren Tools/Prozessen umkehren.

### arxiv (2601.20404, 2025) — On the Impact of AGENTS.md Files on the Efficiency of AI Coding Agents
- **Fundort:** arXiv:2601.20404; via Web-Suche: "agentic coding best practices 2024 2025 LLM agent code structure context"
- **Betrifft AiNetLinter-Features:** F01–F09 (System-Features)
- **Kernaussagen:**
  - Persistente Kontext-Artefakte (AGENTS.md, CLAUDE.md, Copilot-Instructions) verbessern die Effizienz von Coding-Agenten messbar.
  - Strukturierte Instruktionen mit expliziten Regeln reduzieren Fehler und Missverständnisse.
  - "Broader code context or system architecture information" hilft Agenten, Root Causes statt Symptome zu adressieren.
- **Konkrete Zahlen / Grenzwerte:**
  - (Keine konkreten Zahlen verfügbar; qualitative Effizienzverbesserung)
- **Einschränkungen:** Neuere Studie; empirische Stärke noch nicht etabliert.
- **Zeitliche Einordnung:** 2025. Wahrscheinlich zeitstabiles Prinzip.

---

## Übergreifende Erkenntnisse

**Was macht Code für LLM-Agenten schwer?**
1. **Zu großer Kontext:** "Lost in the Middle" — relevante Information geht in Mitte verloren.
2. **Fehlende Repo-Kenntnis:** Häufigster Failure-Typ in SWE-bench-Analysen. Gut strukturierter Code mit klaren Namensgebungskonventionen hilft.
3. **Schlechte Lesbarkeit:** Empirisch belegt (Du 2025, arXiv 2501.11264) — reduziert BLEU und SBERT-Scores messbar.
4. **Halluzinationen bei API/Type-Verwendung:** Häufiger bei komplexer, wenig dokumentierter Code-Struktur.

**Was NICHT klar ist:** Ob AiNetLinter-spezifische Grenzwerte (z.B. MaxLineCount=700) direkt die Agenten-Fehlerrate senken. Die Kausalkette ist plausibel (kleinerer Kontext → weniger "Lost in Middle" → bessere Performance) aber nicht direkt gemessen.

**Kontroverser Befund:** GitHub-Eigenstudie vs. GitClear-Studie zu Copilot-Codequalität zeigen entgegengesetzte Ergebnisse. Ursache: unterschiedliche Metriken und Methodik. Trust in KI-Code sinkt trotz steigender Capability-Benchmarks.

## Nicht gefunden / Lücken

- Keine direkte Kausal-Studie: AiNetLinter-Compliance → LLM-Fehlerrate (diese Studie müsste AiNetLinter selbst durchführen).
- Keine C#-spezifischen LLM-Agent-Studien (dominiert von Python/Java).
- Keine Anthropic-interne Veröffentlichung zu "was macht C#-Code für Claude einfacher".
- Keine Microsoft Research-Studie explizit zu Code-Konventionen und Copilot-Completion-Qualität.
