# AiNetLinter – Agent Instructions & Development Rules

Roslyn-basierte C#/.NET 10 Statische-Code-Analyse- & Linter-Engine zur Durchsetzung von Architekturregeln, Clean-Code-Standards und Konventionen.

---

## 1. Regeln, MCP

- **Verbindliche Regeln (`.agents/rules/`)**: Projektregeln stehen in `AiNetLinter-Richtlinien.mdc`, testbezogene Regeln in `AiNetLinter-TestRichtlinien.mdc` und der projektübergreifende C#-Semantik-Workflow in `AiNetLinter-McpWorkflow.mdc`. `README.md` beschreibt nur deren Pflege und ist keine tägliche Arbeitsanweisung.
- **Semantische C#-Analyse via MCP**: Server `ainetlinter` mit `targetPath: "AiNetLinter.slnx"` (absoluter Pfad im Workspace) nutzen. Für Symbole, Referenzen, Aufrufketten, Impact und Verstöße die semantischen MCP-Tools bevorzugen (siehe `.agents/rules/AiNetLinter-McpWorkflow.mdc`); Textsuche via `rg`.

---

## 2. Entwicklungs- & Test-Workflow

Der verbindliche Gate-Ablauf steht ausschließlich in
`.agents/rules/AiNetLinter-Richtlinien.mdc`; die Testebenen stehen in
`.agents/rules/AiNetLinter-TestRichtlinien.mdc`. Diese Dateien sind die
einzige Source of Truth für Prüfzeitpunkt, Reihenfolge, Testauswahl und
Abschlusskriterien.

---

## 3. Dokumentation & Commits

- **Doku-Synchronisation**: Bei Änderungen an CLI-Optionen, Schemata oder Regeln `Docs/linter/configuration.md`, `Docs/linter/cli.md` und `ainetlinter-rules.json` synchronisieren.
- **Commits**: Deutsche Conventional Commits im Imperativ (`feat:`, `fix:`, `docs:`, `chore:`). Nur eigene auftragsbezogene Änderungen stagen; fremde Änderungen niemals mitcommitten (Details siehe `.agents/rules/AiNetLinter-Richtlinien.mdc`).
