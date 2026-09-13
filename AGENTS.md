# AiNetLinter – Agent Instructions & Development Rules

Roslyn-basierte C#/.NET 10 Statische-Code-Analyse- & Linter-Engine zur Durchsetzung von Architekturregeln, Clean-Code-Standards und Konventionen.

---

## 1. Regeln, MCP

- **Pro-aktive C#-Semantikanalyse via MCP (Pflicht)**: Server `ainetlinter` mit `targetPath: "AiNetLinter.slnx"` (absoluter Pfad im Workspace) **immer pro-aktiv** und als primäres Werkzeug nutzen. Für Code-Erkundung, Symbole, Referenzen, Aufrufketten, Impact und Verstöße direkt die semantischen MCP-Tools aufrufen. Textsuche via `rg` ist erlaubt (insb. für Nicht-C#-Dateien, exakte Strings oder als Fallback), aber MCP liefert gezielteren AST-Kontext (Details: `.agents/rules/AiNetLinter-McpWorkflow.mdc`).

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
