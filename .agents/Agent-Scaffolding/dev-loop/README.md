# dev-loop

Flow-Familie für Software-Änderungen: von einer rohen Idee bis zu
geprüftem, committetem Code. Phase 1 (Planung) dient dem interaktiven
Schärfen der Idee. Phase 2 (Umsetzung) führt die geplante Aufgabe schrittweise
und selbstkorrigierend aus:

| Phase | Ordner | Modell | Wann benutzen |
|---|---|---|---|
| 1. Planung | [`planning/`](planning/README.md) | Interaktiver Dialog in der laufenden Session | Du hast nur eine grobe Idee, willst sie im Gespräch schärfen |
| 2. Umsetzung | [`drift-loop/`](drift-loop/README.md) | Autonomer Subagenten-Loop, ein Step nach dem anderen JIT geplant (Planer → Coder → Kritiker) | Der Weg zum Ziel wird Schritt für Schritt JIT mit Blick auf den echten Codestand geplant |
| 2. Umsetzung (Alternative, **PoC**) | [`dynamic-loop/`](dynamic-loop/README.md) | Derselbe Einstieg, aber die Rollen entstehen zur Laufzeit; festgeschrieben ist ein Regel-Kern aus harten Regeln + benannten Gefahren (~330 Zeilen) | Experiment: Reicht ein kleiner Satz Leitplanken, wenn das Modell die Rollen selbst entwirft? Noch ungetestet |
| 2. Umsetzung (Alternative, **PoC**) | [`asimov-loop/`](asimov-loop/README.md) | Eine Datei, sieben Gesetze, kein vorgegebenes Verfahren (~85 Zeilen) | Dieselbe Frage, eine Stufe radikaler: Reichen die Gesetze allein? Noch ungetestet |

**Ablauf:**
1. Kannst du die Aufgabe schon in 5 Sätzen mit klarem Ziel, Scope und
   Definition of Done beschreiben? Falls nicht: erst `planning/`, das
   Ergebnis (`konzept.md`) ist der Input für die Umsetzung
   (siehe [`drift-loop/spec.md`](drift-loop/spec.md) §3.2 für die exakten
   Mindestanforderungen).
2. Anschließend `drift-loop/` zur schrittweisen Umsetzung nutzen.

## `drift-loop/` — JIT-Planung & Umsetzung

`drift-loop/` plant immer nur den **nächsten** Step, direkt bevor er
drankommt — mit dem tatsächlichen, aktuellen Codestand als Kontext. Ein
grobes `roadmap.md` (Epics statt Detail-Steps) hält dabei fest, was
insgesamt noch offen ist. Das kostet einen Planer-Aufruf pro Step, ist
dafür aber strukturell resistent dagegen, dass mehrere Steps unabhängig
voneinander ähnliche Strukturen erschaffen, weil keiner vom realen
Ergebnis des vorherigen wusste.

Der Kritiker prüft zusätzlich Konzept-Treue (nicht nur Plan/Rules/Logik)
und führt ein separates Tech-Debt-Log für Architektur-Beobachtungen
außerhalb des jeweiligen Step-Scopes — die landen nie automatisch als
neue Arbeit, sondern bleiben für dich sichtbar, bis du selbst
entscheidest, ob und wann sie angegangen werden.

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
drift-loop/orchestrator.md
(JIT: ein Step nach dem anderen, + roadmap.md, + tech-debt.md)
   │
   ▼
<task-dir>/task-summary.md   +   geprüfte, committete Code-Änderungen
```

Beide Ordner arbeiten auf demselben `<task-dir>` (frei wählbar, z. B.
`tasks/<kurzname>/` — der Name/Ort ist reine Konvention deines
Projekts, keine Vorgabe von hier).
