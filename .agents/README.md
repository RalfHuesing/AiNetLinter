# Agenteninfrastruktur

`AGENTS.md` ist der kurze Einstiegspunkt für Coding-Agenten. Dauerhafte
Projektregeln liegen unter `.agents/rules/`; Rollen und ausführbare Abläufe
liegen unter `.agents/roles/` beziehungsweise `.agents/skills/`.

Neue Vorgaben werden nur ergänzt, wenn sie ein wiederkehrendes Problem konkret
verhindern oder eine nachvollziehbare Prüfung ermöglichen. Projektregeln sind
die maßgebliche Quelle; Rollen und Skills wiederholen sie nicht unnötig,
sondern machen Auftrag, Grenzen und Übergabe explizit.

## Chat-Beispiele

Für ein neues oder noch unklares Vorhaben:

```text
$concept-planner

Arbeite im Task-Verzeichnis:
tasks\mcp-unified-analysis-target

Ziel: Vereinheitliche die Analyseziele der MCP-Tools und kläre Scope,
Akzeptanzkriterien sowie die notwendige Verifikation.
```

Nach der ausdrücklichen Konzeptfreigabe für die Umsetzung:

```text
$project-orchestrator

tasks\mcp-unified-analysis-target
```

Der Task-Pfad ist beim `concept-planner` verpflichtend. Beim
`project-orchestrator` verweist er auf das Task-Verzeichnis oder dessen
`Konzept.md`. Ein zusätzlicher Ablaufprompt ist nicht erforderlich. Der
Orchestrator setzt einen Task mit `status: ready`,
`execution_mode: autonomous` und `open_questions: []` vollständig bis zum
Release-Gate um. Er beendet sich nicht nach dem ersten Slice oder einem
Zwischenreview.
