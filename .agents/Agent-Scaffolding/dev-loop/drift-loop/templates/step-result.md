---
status: done
type: step-result
task: <TASK-NAME>
step: <NNN>              # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
epic: <EPIC-NN>
step_type: single  # single | batch — aus step-plan.md übernehmen
coded_by: coder
coded_by_model: <Modell-ID deiner eigenen LLM-Instanz>
coded_by_model_knowledge_cutoff: <Knowledge-Cutoff-Datum, z. B. 2026-01>
coded_at: <ISO-8601>
code_commit_hash: <SHA>  # Commit mit Code+Tests
status_after: done  # done | blocked
blocker_category: n/a  # n/a | content | infrastructure
---

# Result Step <NNN>: <Titel>

<**Wer das liest:** der Kritiker (prüft dich gegen den Plan) und der
Planer beim nächsten Step. Entscheidungsrelevant sind vor allem
„Abweichungen vom Plan", „Beobachtungen" und „Bekannte Unschärfen" —
dort lieber konkret als knapp. Alles andere: knapp halten, nichts aus
dem Step-Plan wiederholen, was unverändert umgesetzt wurde.>

## Zusammenfassung

<2-5 Sätze: Was wurde konkret gemacht?>

## Geänderte Dateien

- `pfad/zu/datei.cs` — <was geändert wurde, in einem Satz>
- `tests/.../NeueTestDatei.cs` (neu) — <was die Tests abdecken>

<Bei `step_type: batch`: pro Bullet zusätzlich die Item-ID voranstellen.>

## Commit

- **Code-Commit-Hash:** `<SHA>`
- **Message:**
  ```
  <konventioneller Commit-Subject>

  <Body falls vorhanden>
  ```
- **Branch:** <aktueller Branch>
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

<**Bei grün: eine Zeile je Command, kein Volldump.** Ein grüner Output
wird von niemandem mehr gelesen — weder vom Kritiker noch vom Planer —
kostet aber in jedem Folge-Kontext mit. Bei rot: gekürzter Fehler-Output,
nur die relevanten Zeilen. Der wird gebraucht.>

```
<Build-Command aus roadmap.md> → grün
<Test-Command aus roadmap.md>  → grün (<N> Tests, 0 Fehler)
```

## Abweichungen vom Plan

<Alles was vom Step-Plan abgewichen ist. Falls keine: "Keine — Plan 1:1
umgesetzt.">

## Beobachtungen

<Dinge die während der Arbeit aufgefallen sind, aber NICHT im Scope
dieses Steps lagen. Wichtig: dies ist der Kanal für Hinweise an den
**Kritiker** (der daraus ggf. einen Tech-Debt-Eintrag macht, siehe
`../spec.md` §8.3) — der Coder selbst legt keinen Tech-Debt-Eintrag an,
das bleibt Aufgabe des Kritikers.>

## Bekannte Unschärfen

<Was du nicht 100%ig sicher bist und was der Kritiker besonders prüfen
sollte.>

## Falls Status `blocked`

**Blocker-Art:** `content` oder `infrastructure`

**Blockiert weil:** <konkrete Begründung>

**Brauche von Nutzer:** <klare Frage oder Entscheidung>

**Aktueller Stand:** <was bereits umgesetzt ist, was noch offen ist>
