# Baseline / Ratchet-Mechanismus (F01)

**Kategorie:** CLI-Feature  
**CLI-Flag / Konfiguration:** `--baseline <pfad>`  
**Status:** Vorhanden

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Der Ratchet-Mechanismus ist State of the Art für die inkrementelle Einführung von Linting in Legacy-Projekten — jeder führende Linter bietet eine vergleichbare Funktionalität, und ohne ihn ist AiNetLinter in Legacy-Kontexten praktisch nicht einsetzbar.

---

## Empfehlung

**Aktion:** Beibehalten  
**Begründung:** Ohne Baseline-Mechanismus würden Legacy-Codebases beim ersten Lauf Hunderte von Verstößen erzeugen, was die Adoption blockiert. Der SHA-256-basierte Ansatz (nur geänderte Dateien werden gegen neue Regeln geprüft) ist eine praxiserprobte Lösung — identisch mit dem Vorgehen von ESLint, SonarQube und Roslyn Analyzers.

---

## Nutzen-Analyse

Der Baseline-Mechanismus löst ein fundamentales Adoptionsproblem: Ein neuer Linter in einem bestehenden Projekt mit tausenden von Verstößen ist nicht sofort durchsetzbar. Er erzeugt drei konkrete Werte:

1. **Sofortiger Onboarding-Nutzen:** Entwickler können AiNetLinter in ein bestehendes Projekt integrieren, ohne alle historischen Verstöße zuerst beheben zu müssen. Der Linter "wächst" mit dem Projekt.
2. **Ratchet-Effekt:** Bestehende Verstöße werden eingefroren; neue oder geänderte Dateien müssen regelkonform sein. Die Qualität kann nur steigen, nie sinken — sofern die Baseline nicht zurückgesetzt wird.
3. **Selektive Durchsetzung:** Teams können entscheiden, welche Verstöße sie in der Baseline einfrieren und welche sie sofort angehen. Das ermöglicht priorisierte Migration.

**Szenarien wo es wertvoll ist:**
- Migration eines Legacy-Projekts auf AiNetLinter
- Einführung neuer Regeln in einem laufenden Projekt
- Schrittweise Verschärfung von Schwellenwerten

**Szenarien wo es irrelevant ist:**
- Greenfield-Projekte, die von Anfang an AiNetLinter nutzen
- Projekte mit minimalen Verstößen, die vollständig bereinigt werden können

---

## Vergleich: Andere Tools

| Tool | Baseline-Mechanismus | Ansatz |
|------|---------------------|--------|
| **ESLint** | `eslint --cache` + `.eslintcache` | Datei-Hash-basiertes Caching, nicht dasselbe wie ein Freeze-Mechanismus |
| **SonarQube** | New Code Definition | Nur neue Commits werden analysiert; bestehende Issues als "Legacy" markiert |
| **StyleCop / Roslyn** | `.editorconfig` Suppression + Baseline-Dateien | Manuelle Suppression, kein automatischer Freeze |
| **NDepend** | Baseline Snapshot | Vergleich von Snapshot zu Snapshot; Trend-Analyse |
| **AiNetLinter** | SHA-256 Freeze per Datei | Granularer als SonarQube (Dateiebene), einfacher als NDepend |

AiNetLinters Ansatz (SHA-256 per Datei) ist granularer als der SonarQube-Ansatz (commit-basiert) und einfacher als NDepends Snapshot-System. Der Ansatz ist jedoch nicht besonders innovativ — er entspricht einer pragmatischen Umsetzung eines etablierten Patterns.

**Lücke im Vergleich:** SonarQube bietet zusätzlich eine "Qualitätsgate"-Funktion, die Builds blockiert wenn Quality-Gate-Regeln verletzt werden — AiNetLinter hat dafür keinen expliziten CI-Integration-Layer.

---

## KI-Agenten-Perspektive

Der Baseline-Mechanismus ist für LLM-Agenten aus zwei Richtungen relevant:

1. **Als Werkzeug für den Agenten:** Ein Agent, der AiNetLinter in ein bestehendes Projekt integriert, profitiert erheblich davon, dass er nicht alle historischen Verstöße auf einmal beheben muss. Er kann `--baseline` setzen und dann schrittweise Verbesserungen vornehmen — ein realistischeres Szenario für autonome Integration.

2. **Als Schutz vor Agenten-Regression:** Wenn ein Agent Änderungen vornimmt und dabei versehentlich neue Verstöße in bereits konformen Dateien einführt, zeigt die Baseline genau diese Regression an. Damit ist die Baseline ein wichtiger Qualitätssicherungs-Layer im agentic Workflow.

Die Studie "On the Impact of AGENTS.md Files" (arXiv:2601.20404, 2025) zeigt, dass Agenten effizienter arbeiten, wenn sie klare, inkrementelle Feedback-Schleifen haben — der Ratchet-Mechanismus unterstützt genau dieses Muster.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Der Bedarf, neue Qualitätsregeln inkrementell in bestehende Codebases einzuführen, ist unabhängig von der Modellgeneration ein strukturelles Problem jeder Software-Engineering-Organisation. Auch deutlich bessere LLM-Modelle werden Legacy-Codebases vorfinden, die nicht vollständig neu geschrieben werden können. Der Baseline-Mechanismus bleibt daher dauerhaft relevant.

---

## Quellen

- arXiv:2601.20404 (2025) — On the Impact of AGENTS.md Files on the Efficiency of AI Coding Agents
- SonarQube Documentation (2024) — New Code Definition: https://docs.sonarsource.com/sonarqube/latest/project-administration/clean-as-you-code/
- NDepend Documentation (2024) — Baseline and Trend Analysis: https://www.ndepend.com/docs/using-ndepend-incremental-analysis
- ESLint Documentation (2024) — Caching: https://eslint.org/docs/latest/use/command-line-interface#caching
