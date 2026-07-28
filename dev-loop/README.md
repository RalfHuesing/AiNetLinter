# dev-loop

Flow-Familie für Software-Änderungen: von einer rohen Idee bis zu
geprüftem, committetem Code. Zwei Phasen, unterschiedlich in ihrem
Ausführungsmodell:

| Phase | Ordner | Modell | Wann benutzen |
|---|---|---|---|
| 1. Planung | [`planning/`](planning/README.md) | Interaktiver Dialog in der laufenden Session | Du hast nur eine grobe Idee, willst sie im Gespräch schärfen |
| 2. Umsetzung | [`task-loop/`](task-loop/README.md) | Autonomer Subagenten-Loop (Planer → Coder → Auditer) | Du hast schon eine solide Aufgaben-Doku, willst sie unbeaufsichtigt abarbeiten lassen |

**Faustregel, was du wann nimmst:** Kannst du die Aufgabe schon in
5 Sätzen mit klarem Ziel, Scope und Definition of Done beschreiben? Dann
direkt zu `task-loop/`. Falls nicht: erst `planning/`, das Ergebnis
davon ist genau der Input, den `task-loop/` braucht (siehe
[`task-loop/spec.md`](task-loop/spec.md) §6 für die exakten
Mindestanforderungen).

## Pipeline

```
grobe Idee
   │
   ▼
planning/orchestrator.md   (Dialog, iterativ)
   │
   ▼
<task-dir>/konzept.md   (status: ready)
   │
   ▼
task-loop/orchestrator.md   (autonomer Loop)
   │
   ▼
<task-dir>/task-summary.md   +   geprüfte, committete Code-Änderungen
```

Beide Phasen arbeiten auf demselben `<task-dir>` (frei wählbar, z. B.
`tasks/<kurzname>/` — der Name/Ort ist reine Konvention deines
Projekts, keine Vorgabe von hier).
