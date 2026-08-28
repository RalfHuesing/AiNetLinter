---
task: decompiled-assembly-analysis
type: initial-prompt
created_at: 2026-08-28T11:06:28+02:00
---

# Initial-Prompt

Beachte die Regeln `.agents/rules/*`, insbesondere
`.agents/rules/AiNetLinter-McpWorkflow.mdc`.

Deine Rolle: `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md`.

Dein Task: `tasks/decompiled-assembly-analysis`.

Mache immer größere Code-Pakete. Findings vom Kritiker und Tech-Debt usw.
wird in größere Pakete zusammengefasst. Hintergrund: Keine Mini-Pakete, was
ineffizient wäre, da wir drei Agenten pro Step losschicken.

Notiere diesen Initial-Prompt als Markdown-Datei in
`tasks/decompiled-assembly-analysis/` und verweise darauf, damit bei einem
Kontextfenster-Compact die Aufgabe nicht verloren geht und wieder aufgenommen
werden kann.

## Dauerhafte Arbeitsanker

- Konzept: `Konzept.md` (Status `ready`)
- Orchestrator-Zustand: `task-state.md`
- Grobe Planung: `roadmap.md`
- Laufende CodeMap: `codemap.md`
- Kritiker-Log: `tech-debt.md`
- Abschluss: `task-summary.md`
- Rules-Verzeichnis: `.agents/rules`
- Rollenreihenfolge pro Step: Planer → Coder → Kritiker, strikt seriell
- Größere, in sich geschlossene Pakete planen; keine künstlichen Mini-Steps
- Für jeden Rollenaufruf immer einen neuen Sub-Agenten starten; keinen
  bestehenden Sub-Agenten wiederverwenden.
- Erledigte Sub-Agenten nach ihrem Abschluss entfernen/schließen.
- Diese Agenten-Lifecycle-Regel gilt zusätzlich zur seriellen Ausführung und
  wird zusammen mit dieser Dokumentation committed.
- Nutzerpräzisierung: DRY-, MagicValues- und DeadCode-Funde gelten in diesem
  Task als Tech-Debt und sollen proaktiv, architektonisch sinnvoll und
  automatisch in größeren ohnehin laufenden Codepaketen mitbehoben werden.
  Keine künstlichen Einzel-Sweeps oder Mini-Pakete erzeugen; die Änderungen
  bleiben durch Planer und Kritiker prüfbar.

## Kontextbegrenzte Folge-Tasks

Die Pakete bleiben fachlich zusammenhängend und größer als Mini-Pakete, dürfen
aber kein komplettes Epic in einen einzigen Agentenlauf zwingen. Für jeden
Folge-Step gilt daher ein Split-Gate vor dem Coder: höchstens ein primärer
Fachvertrag oder zwei eng gekoppelte Verträge, höchstens drei unmittelbar
betroffene Schichten und höchstens acht Akzeptanzkriterien. Der Planer muss
im Step-Plan zusätzlich `context_budget` mit `read_first` (höchstens zwölf
zentrale Dateien), `read_on_demand` und `out_of_scope` dokumentieren. Wird
dieser Rahmen überschritten, ist das Epic vor dem Coder in mehrere vertikale,
jeweils testbare Pakete zu teilen.

Jeder Step-Plan enthält außerdem einen kurzen Handoff-Abschnitt mit invarianten
Verträgen, relevanten MCP-Symbolen und dem nächsten sicheren Einstiegspunkt.
Coder und Kritiker lesen zuerst nur diesen Handoff, den Step-Plan, Resultate,
Reviews und die gezielt benannten Dateien; die vollständige Solution wird
nicht pauschal in den Kontext geladen. Bei einem Kontext-Compact vor dem
Coder-Abschluss wird der Step mit dem vorhandenen Plan/Handoff von einem neuen
Coder fortgesetzt, niemals mit demselben Sub-Agenten.

Als grobe Folge-Task-Schnittlinie gelten: EPIC-03 zunächst Mapping-Vertrag und
Snapshot-Auflösung getrennt schneiden; EPIC-04 Client/Auth-Fehlersemantik von
Refresh/atomarer Veröffentlichung trennen; EPIC-05 Referenzgraph/Health von
Capability-Routing trennen; EPIC-06 Dokumentation/Verträge von der finalen
Verifikation trennen. Der Planer darf diese Schnitte anhand des Codes
präzisieren, soll aber die Kontextgrenze nicht zugunsten eines größeren
Einzel-Steps aufgeben.
