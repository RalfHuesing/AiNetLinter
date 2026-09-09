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
Zwischenreview. Die Rollen, Builds, Tests und MCP-Prüfungen laufen dabei strikt
seriell; pro Task ist höchstens ein delegierter Agent gleichzeitig aktiv.

MCP-Server aus Agent-Sicht prüfen — Kurzform auf dem eigenen Projekt (Source-Modus):

```text
$mcp-ux-audit
```

Oder explizit mit einem Release-Build (Assembly-Modus, 360°-Sicht):

```text
$mcp-ux-audit

targetPath: C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\bin\Release\net10.0\AiNetLinter.exe
```

Ohne `targetPath` verwendet der Skill automatisch `AiNetLinter.slnx` im
Repository-Root (Source-Modus). Mit `.exe`- oder `.dll`-Pfad läuft der Audit
im Assembly-Modus — funktioniert mit dem eigenen Release-Build, aber auch mit
jeder beliebigen fremden `.NET`-Assembly (z.B. `Newtonsoft.Json.dll` oder eine
Kunden-DLL). So lässt sich vorab prüfen ob AiNetLinter mit einem konkreten
Assembly ordentlich umgeht, bevor man es in der echten Entwicklung einsetzt.
