# planning

Interaktive Konzeptentwicklung. Läuft **in der aktuellen Session**, kein
Subagenten-Loop — Dialog statt Autonomie, weil Konzeptschärfung von
Rückfragen lebt, die ein autonomer Subagent nicht stellen kann.

## Wann benutzen

Du hast eine rohe Idee ("Tetris in Go für Android und Apple Devices"),
aber noch keine klare Vorstellung von Scope, Zielplattform-Entscheidung,
Non-Goals oder Definition of Done. Du willst das im Gespräch schärfen,
nicht selbst durchdenken.

## Wie starten

```
<pfad-zu-dev-loop>/planning/orchestrator.md <task-dir>
```

`<task-dir>` ist ein beliebiges Verzeichnis (frei wählbar, muss nicht
existieren). Enthält es schon eine `konzept.md`, macht die Session dort
weiter statt neu zu starten.

## Enthält

- **`orchestrator.md`** — die eigentliche Handlungsanweisung
- **`templates/konzept.md`** — Ziel-Struktur des Konzept-Dokuments

## Output

`<task-dir>/konzept.md` mit `status: ready` — erfüllt dann automatisch
die Mindestanforderungen aus [`../drift-loop/spec.md`](../drift-loop/spec.md)
§3.2 und kann direkt an [`../drift-loop/orchestrator.md`](../drift-loop/orchestrator.md)
übergeben werden.
