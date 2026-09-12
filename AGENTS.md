# AiNetLinter – Agent Instructions & Development Rules

Roslyn-basierte C#/.NET 10 Statische-Code-Analyse- & Linter-Engine zur Durchsetzung von Architekturregeln, Clean-Code-Standards und Konventionen.

---

## 1. Regeln, MCP & Skills

- **Verbindliche Regeln (`.agents/rules/`)**: Projektregeln stehen in `AiNetLinter-Richtlinien.mdc`, testbezogene Regeln in `AiNetLinter-TestRichtlinien.mdc` und der projektübergreifende C#-Semantik-Workflow in `AiNetLinter-McpWorkflow.mdc`. `README.md` beschreibt nur deren Pflege und ist keine tägliche Arbeitsanweisung.
- **Semantische C#-Analyse via MCP**: Server `ainetlinter` mit `targetPath: "AiNetLinter.slnx"` (absoluter Pfad im Workspace) nutzen. Für Symbole, Referenzen, Aufrufketten, Impact und Verstöße die semantischen MCP-Tools bevorzugen (siehe `.agents/rules/AiNetLinter-McpWorkflow.mdc`); Textsuche via `rg`.
- **Orchestrierung & Skills (`.agents/skills/`)**: Größere Features, Konzepte oder Refactorings über spezialisierte Skills steuern (`concept-planner`, `project-orchestrator`, `mcp-ux-audit`).

---

## 2. Entwicklungs- & Test-Workflow

### Gates & Verifikation
1. **Schnelle Iteration (während der Entwicklung)**:
   ```bash
   dotnet test src/AiNetLinter.FastTests --filter Category=Unit
   ```

2. **Abschluss-Verifikation bei Codeänderungen (Pflicht)**:
   Vor Abschluss jedes Tasks an Produktions- oder Testcode MUSS ein vollständiger Lauf über beide Testprojekte grün sein (ohne `Stress`):
   ```bash
   dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
   dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
   ```

3. **Build prüfen**:
   ```bash
   dotnet build
   ```
   Baut alle vier Projekte der Solution fehler- und warnungsfrei (`TreatWarningsAsErrors = true`).

4. **`Stress`-Kategorie (nur manuell/gezielt, nie automatisch)**:
   Absichtlich lastintensive Tests laufen nicht im normalen Gate mit, sondern nur auf explizite Anforderung:
   ```bash
   dotnet test src/AiNetLinter.IntegrationTests --filter Category=Stress
   ```

5. **Testdiagnose**:
   Bei unklaren oder abgebrochenen Läufen `--logger "trx;LogFileName=<Name>.trx"` nutzen.

6. **Dokumentations-Ausnahme**:
   Für reine Dokumentations-, Markdown- oder Agentenregel-Änderungen sind Build und Tests nicht erforderlich. Hier genügen Referenzprüfungen, `git diff --check` und eine saubere Diff-Prüfung.

---

## 3. Dokumentation & Commits

- **Doku-Synchronisation**: Bei Änderungen an CLI-Optionen, Schemata oder Regeln `Docs/configuration.md` und `ainetlinter-rules.json` synchronisieren.
- **Commits**: Deutsche Conventional Commits im Imperativ (`feat:`, `fix:`, `docs:`, `chore:`). Nur eigene auftragsbezogene Änderungen stagen; fremde Änderungen niemals mitcommitten (Details siehe `.agents/rules/AiNetLinter-Richtlinien.mdc`).
