# AiNetLinter – Agent Instructions & Development Rules

Roslyn-basierte C#/.NET 10 Statische-Code-Analyse- & Linter-Engine zur Durchsetzung von Architekturregeln, Clean-Code-Standards und Konventionen.

---

## 1. Regeln, MCP

- **Verbindliche Regeln (`.agents/rules/`)**: Projektregeln stehen in `AiNetLinter-Richtlinien.mdc`, testbezogene Regeln in `AiNetLinter-TestRichtlinien.mdc` und der projektübergreifende C#-Semantik-Workflow in `AiNetLinter-McpWorkflow.mdc`. `README.md` beschreibt nur deren Pflege und ist keine tägliche Arbeitsanweisung.
- **Semantische C#-Analyse via MCP**: Server `ainetlinter` mit `targetPath: "AiNetLinter.slnx"` (absoluter Pfad im Workspace) nutzen. Für Symbole, Referenzen, Aufrufketten, Impact und Verstöße die semantischen MCP-Tools bevorzugen (siehe `.agents/rules/AiNetLinter-McpWorkflow.mdc`); Textsuche via `rg`.

---

## 2. Entwicklungs- & Test-Workflow

### Gates & Verifikation
1. **Taskbeginn**:
   Eine saubere Codebasis wird vorausgesetzt. Vor der ersten Änderung werden
   kein Build, kein Verify und keine Tests ausgeführt.

2. **Incremental Gate (nach jedem kohärenten Coding-Slice)**:
   Produktions- oder Testcode darf erst nach dieser Reihenfolge als nächster
   Arbeitsstand verwendet werden:
   ```text
   dotnet build
   verify(targetPath)
   dotnet test src/AiNetLinter.FastTests --filter Category=Unit
   ```
   Schlägt ein Schritt fehl, muss die Ursache vor dem nächsten Slice behoben
   und die Reihenfolge wiederholt werden. Kein bekannter roter Stand bleibt
   als Zwischen- oder Übergabestand bestehen.

3. **Release Gate bei Codeänderungen (Pflicht)**:
   Vor Abschluss jedes Tasks an Produktions- oder Testcode müssen alle Schritte
   in dieser Reihenfolge grün sein:
   ```text
   dotnet build
   verify(targetPath, scope: "solution")
   dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
   dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
   ```
   Ist ein Schritt rot, ist der Task nicht abgeschlossen: Ursache beheben und
   das Release Gate vollständig wiederholen.

   `dotnet build` muss alle vier Projekte der Solution fehler- und
   warnungsfrei bauen (`TreatWarningsAsErrors = true`). `verify` muss beim
   Release Gate `verdict=pass`, `score=10.0` und `violationCount=0` liefern.

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

- **Doku-Synchronisation**: Bei Änderungen an CLI-Optionen, Schemata oder Regeln `Docs/linter/configuration.md`, `Docs/linter/cli.md` und `ainetlinter-rules.json` synchronisieren.
- **Commits**: Deutsche Conventional Commits im Imperativ (`feat:`, `fix:`, `docs:`, `chore:`). Nur eigene auftragsbezogene Änderungen stagen; fremde Änderungen niemals mitcommitten (Details siehe `.agents/rules/AiNetLinter-Richtlinien.mdc`).
