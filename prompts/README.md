# prompts

Einfache, **platzhalterfreie** Markdown-Dateien zum direkten Referenzieren
im Chat — kein Orchestrator, keine Subagenten, kein mehrstufiger Workflow.
Der Unterschied zu [`../dev-loop/`](../dev-loop/README.md): `dev-loop/`
orchestriert einen mehrstufigen Prozess mit eigenem Zustand
(`task-state.md` & Co.); die Dateien hier sind einzelne, in sich
geschlossene Verhaltens-Anweisungen für **die laufende Session** — du
referenzierst eine Datei, ergänzt direkt danach im selben Prompt dein
eigenes Anliegen, fertig.

## Wie benutzen

```
<Verweis auf die gewünschte Datei, z. B. prompts/dev/sparring.md>

<dein eigenes Anliegen, in eigenen Worten>
```

Das Ganze im Kontext des Workspaces/Projekts, in dem du gerade arbeitest
— die Dateien leiten Kontext (welches Projekt, welches Anliegen) aus
deinem Text und dem Arbeitsverzeichnis ab, nicht aus Platzhaltern, die du
vorher ausfüllen müsstest.

## Kategorien

- **[`dev/`](dev/README.md)** — technische/entwicklungsnahe Anliegen.

Weitere Kategorien kommen als eigene Geschwister-Ordner dazu, wenn Bedarf
entsteht.
