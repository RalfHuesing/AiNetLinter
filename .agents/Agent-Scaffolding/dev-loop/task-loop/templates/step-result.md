---
status: done
type: step-result
task: <TASK-NAME>
step: <NNN>              # im Fix-Modus: <NNN>/fix-<XX>
step_type: single  # single | batch — aus step-plan.md übernehmen, siehe ../../spec.md §7.7
coded_by: coder
coded_by_model: <Modell-ID deiner eigenen LLM-Instanz>
coded_by_model_knowledge_cutoff: <Knowledge-Cutoff-Datum, z. B. 2026-01>
coded_at: <ISO-8601>
code_commit_hash: <SHA>  # Commit mit Code+Tests (Coder-Skill Schritt 5)
# Hinweis: den Commit, der DIESE Datei enthält (Coder-Skill Schritt 7),
# kann diese Datei denknotwendig nicht selbst zitieren — bei Bedarf per
# `git log --follow -- <Pfad-dieser-Datei>` nachschlagen.
status_after: done  # done | blocked
blocker_category: n/a  # n/a | content | infrastructure — nur relevant falls status_after: blocked, siehe Abschnitt "Falls Status blocked"
---

# Result Step <NNN>: <Titel>

## Zusammenfassung

<2-5 Sätze: Was wurde konkret gemacht? In einem Satz pro Datei.>

## Geänderte Dateien

- `pfad/zu/datei.cs` — <was geändert wurde, in einem Satz>
- `pfad/zu/datei2.cs` — <was>
- `tests/.../NeueTestDatei.cs` (neu) — <was die Tests abdecken>

<Pro Datei ein Bullet. Auch neue Files auflisten.>

<Bei `step_type: batch`: pro Bullet zusätzlich die Item-ID voranstellen,
z. B. `- item-01: pfad/zu/datei.md — Tippfehler korrigiert`, damit
Auditer und ein späterer item-genauer Fix-Step die Zuordnung nachvollziehen
können (siehe `../../spec.md` §7.7).>

## Commit

- **Code-Commit-Hash:** `<SHA>`
- **Message:**
  ```
  <konventioneller Commit-Subject>

  <Body falls vorhanden>
  ```
- **Branch:** <aktueller Branch>
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit für diese Datei +
  `step-plan.md`-Status (siehe Coder-Skill Schritt 7) — dessen Hash steht
  nicht hier drin (Selbstbezug), sondern in `git log`.

## Build-Output

```
<Build-Command aus Tech-Stack-Notiz>
→ Ergebnis: grün / rot (mit gekürztem Fehler-Output falls rot)
```

## Test-Output

```
<Test-Command aus Tech-Stack-Notiz>
→ Ergebnis: grün / rot (mit gekürzter Failure-Liste falls rot)
→ Anzahl Tests: N, davon grün: N, baseline-Ausnahmen: M
```

## Abweichungen vom Plan

<Alles was vom Step-Plan abgewichen ist — auch kleine Umnummerierungen,
Pattern-Wechsel, zusätzliche Refactorings die im Plan standen.>
<Falls keine Abweichungen: "Keine — Plan 1:1 umgesetzt.">

## Beobachtungen

<Dinge die während der Arbeit aufgefallen sind, aber NICHT im Scope dieses
Steps lagen. Pro Beobachtung: kurze Beschreibung + Vorschlag was zu tun
wäre (Folge-Step, separater Task, oder einfach Notiz).>

<Beispiele:>
- „Die Methode `Foo` in `Bar.cs:42` hat eine ähnliche Logik — falls
  konsolidiert werden soll, wäre das ein eigener Refactoring-Step."
- „Im Plan nicht erwähnt, aber `Baz.cs` referenziert die geänderte
  Funktion — habe ich nicht angefasst, sollte aber in einem Folge-Step
  verifiziert werden."

## Bekannte Unschärfen

<Was du nicht 100%ig sicher bist und was der Auditer besonders prüfen
sollte. Z. B. „Das Verhalten bei leerem Input-Array habe ich nicht
getestet — Test fehlt möglicherweise.">

## Falls Status `blocked`

**Blocker-Art:** `content` (fachlich/planerisch — Nutzer-Entscheidung nötig) oder `infrastructure` (Umgebung/Tooling fehlt oder nicht erreichbar — siehe Coder-Skill Schritt 4a)

**Blockiert weil:** <konkrete Begründung — bei `infrastructure`: was genau fehlt/nicht erreichbar ist>

**Brauche von Nutzer:** <klare Frage oder Entscheidung — bei `infrastructure`: konkrete manuelle Handlung, z. B. "Dienst X starten", "Tool Y installieren">

**Bisher erreicht:** <was bereits umgesetzt ist, was noch offen ist>
