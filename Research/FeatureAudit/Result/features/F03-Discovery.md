# Discovery-Commands (F03)

**Kategorie:** CLI-Feature  
**CLI-Flag / Konfiguration:** `--list-rules`, `--describe-rule <name>`, `--docs <name>`  
**Status:** Vorhanden

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Discovery-Commands sind das wichtigste differenzierende Feature für die LLM-Agenten-Tauglichkeit von AiNetLinter — sie ermöglichen es einem Agenten, das Tool eigenständig zu verstehen, ohne menschliche Dokumentation konsultieren zu müssen, was direkt mit der Studie zu AGENTS.md-Effizienz korrespondiert.

---

## Empfehlung

**Aktion:** Beibehalten und ausbauen  
**Begründung:** Das ist das Feature mit dem stärksten empirischen Rückhalt für den AI-Agenten-Anwendungsfall. Die arXiv-Studie (2601.20404, 2025) belegt direkt, dass Agenten mit Discovery-Schnittstellen effizienter arbeiten. Erweiterung um maschinenlesbaren Output (JSON) würde den Nutzen weiter steigern.

---

## Nutzen-Analyse

Die drei Discovery-Kommandos decken unterschiedliche Informationsbedürfnisse ab:

1. **`--list-rules`:** Gibt alle konfigurierten Regeln mit ID, Status und aktuellem Wert aus. Erlaubt einem Agenten, den vollständigen Regelkatalog zu erfassen, ohne Konfigurationsdateien zu lesen.

2. **`--describe-rule <name>`:** Gibt eine Beschreibung einer spezifischen Regel aus — inkl. Grenzwert, Severity, Ausnahmen und Beispiele. Erlaubt gezieltes Nachfragen.

3. **`--docs <name>`:** Detailliertere Dokumentation — warum existiert die Regel, was sind die Hintergründe? Für den Agenten relevant wenn er eine Regel verstehen will, bevor er Code entsprechend generiert.

**Szenarien wo es wertvoll ist:**
- Erstkontakt: Ein Agent soll ein neues Projekt mit AiNetLinter lint-konform machen, hat aber keine Vorabkenntnis der Regeln.
- Regelabfrage vor Code-Generierung: Agent fragt ab, welche Einschränkungen gelten, bevor er eine neue Klasse erstellt.
- Fehlerbehebung: Agent erhält einen Linter-Fehler und fragt per `--describe-rule` nach den Details.

**Szenarien wo es irrelevant ist:**
- Erfahrene Entwickler die die Regeln kennen
- Projekte ohne agentic Nutzung

---

## Vergleich: Andere Tools

| Tool | Discovery-Fähigkeit | Ansatz |
|------|---------------------|--------|
| **ESLint** | `--print-config`, `--rule` | Gibt Konfiguration aus; kein Rule-Level-Docs |
| **SonarQube** | Web-UI + REST API | Regeln über Browser oder API abrufbar; nicht CLI-nativ |
| **StyleCop** | Keine CLI-Discovery | Dokumentation nur über externe Website / Wiki |
| **Roslyn Analyzers** | Keine CLI-Discovery | IDs und Docs über IDE oder nuget.org |
| **NDepend** | Keine CLI-Discovery | Regeln über proprietäre UI; kein programmatischer Zugriff |
| **AiNetLinter** | `--list-rules` + `--describe-rule` + `--docs` | Vollständige CLI-Discovery; agenten-optimiert |

AiNetLinter ist hier klar führend unter den verglichenen Tools. Kein anderer verglichener Linter bietet ein vergleichbares Discovery-Feature auf CLI-Ebene. Das ist ein echter Wettbewerbsvorteil für den AI-Agenten-Anwendungsfall.

**Verbesserungsvorschlag:** Maschinenlesbarer JSON-Output für `--list-rules` würde es Agenten erlauben, die Regelliste programmatisch zu verarbeiten, ohne Natural-Language-Parsing — z.B. `--list-rules --format json`.

---

## KI-Agenten-Perspektive

Discovery-Commands sind das Feature mit dem stärksten direkten empirischen Rückhalt für den AI-Anwendungsfall:

Die arXiv-Studie "On the Impact of AGENTS.md Files" (2601.20404, 2025) zeigt statistisch messbare Effizienzgewinne wenn Agenten strukturierte Metadaten über die Werkzeuge in ihrem Kontext haben. Discovery-Commands sind die dynamische, immer aktuelle Version dieser Metadaten — direkter aus der Tool-Konfiguration generiert als eine statische AGENTS.md.

**Konkrete Wirkungskette für LLM-Agenten:**
1. Agent findet AiNetLinter in einem Projekt
2. Agent führt `--list-rules` aus → erhält vollständige Regel-Übersicht
3. Agent generiert Code und läuft Linter → Fehler bei Regel `EnforceSealedClasses`
4. Agent führt `--describe-rule EnforceSealedClasses` aus → versteht Anforderung
5. Agent korrigiert Code und wählt bei der nächsten Generierung sealed direkt

Dieser Loop vermeidet sowohl Halluzinationen (Agent muss keine Annahmen über Regeln machen) als auch Rückfragen an den Benutzer (Agent kann selbst recherchieren). Das passt direkt zu den Befunden in "Inside the Scaffold" (arXiv:2511.00872), dass Agenten vor allem dann scheitern, wenn der Projekt-Kontext nicht selbst-erklärend ist.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Der Bedarf nach maschinenlesbarer Tool-Selbstauskunft wird mit mächtigeren Agenten eher größer als kleiner — ein Agent der mehr autonom handeln kann, braucht auch mehr Werkzeuge, um seinen Kontext selbst zu erschließen. Discovery-Commands bleiben dauerhaft relevant.

---

## Quellen

- arXiv:2601.20404 (2025) — On the Impact of AGENTS.md Files on the Efficiency of AI Coding Agents
- arXiv:2511.00872 (2024) — A Comprehensive Empirical Evaluation of Agent Frameworks: Inside the Scaffold
- ESLint Documentation (2024) — CLI Options: https://eslint.org/docs/latest/use/command-line-interface
- SonarQube Web API Documentation (2024): https://docs.sonarsource.com/sonarqube/latest/extension-guide/web-api/
