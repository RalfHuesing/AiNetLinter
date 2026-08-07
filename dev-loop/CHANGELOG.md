# Changelog — dev-loop

Nachvollziehbare Änderungen an `dev-loop/` selbst (Workflow-Definitionen,
nicht an damit umgesetzten Projekten). Neueste zuerst.

## 2026-08-07 — Nice-to-Have-Gate, CodeMap, flache Korrektur-Steps, Tech-Debt-Bündelung, Terseness-Regel

Aus einer Sparring-Session heraus entstanden (Rückblick auf den
praktischen Betrieb von `planning/` + `drift-loop/`). Fünf zusammenhängende
Änderungen:

1. **Nice-to-Have-Gate** (`planning/orchestrator.md`, `planning/templates/konzept.md`):
   `status: ready` erfordert jetzt, dass „Nice-to-Have" leer ist — jeder
   Punkt entweder nach Muss-Haben hochgestuft oder nach Non-Goals
   verschoben. Grund: Der Planer im `drift-loop` leitet Epics
   ausschließlich aus Muss-Haben ab, ein reiner Nice-to-Have-Punkt wurde
   nie umgesetzt und blieb unbemerkt für immer liegen.

2. **CodeMap** (neu: `drift-loop/templates/codemap.md`, plus Anpassungen
   in `spec.md`, `orchestrator.md`, allen drei Skills): Task-scoped,
   laufend gepflegte Landkarte relevanter Module (Pointer-Prinzip, wie
   Regel-Index/Tech-Debt-Index). Planer befüllt sie initial im
   Roadmap-Modus, Coder aktualisiert sie vor jedem Doku-Commit, Planer
   liest/ergänzt sie im Step-Modus — inkl. Anti-Loop-Check (Widerspruch
   zu einer bereits getroffenen, dokumentierten Entscheidung muss
   begründet oder die alte Entscheidung als obsolet markiert werden).
   Verlässlich, weil der Loop strikt seriell läuft (`spec.md` §6) — keine
   Drift zwischen Update und nächstem Lesezugriff möglich.

3. **Fix-Steps flach + `corrects`-Kette + weicher Deckel** (`spec.md`
   §6.2.1/§10.5/§10.6, `orchestrator.md`, `skills/planer/SKILL.md`,
   Templates): Der `step-NNN/fix-XX/`-Unterordner entfällt. Korrekturen
   sind jetzt normale, flach durchnummerierte Steps mit `corrects:
   step-NNN` im Frontmatter. Bei eindeutigen, mechanischen Findings
   schreibt der **Orchestrator** den Korrektur-Plan selbst (Transkript,
   kein Planer-Aufruf) — der Kritiker bleibt in jedem Fall Pflicht. Das
   alte Fix-Budget (Ordner-gezählt, Hard-Abort bei `max_total_fix_rounds`)
   wird ersetzt durch: Kettenbudget (3 Korrekturen in derselben
   `corrects`-Kette → `blocked`) + weicher Task-Deckel
   (`soft_step_checkin_interval`, Default 40 — Zwischenfrage an den
   Nutzer statt automatischem Abbruch).

4. **Tech-Debt-Bündelung** (`spec.md` §9.1/§10.6, `skills/kritiker/SKILL.md`,
   `skills/planer/SKILL.md`, `templates/tech-debt.md`): Neues Feld
   `auto_fixable` (`ja`/`nein`) pro Tech-Debt-Eintrag. `ja` nur bei rein
   mechanischer, entscheidungsfreier Korrektur ohne Architektur-Ermessen.
   Solche Einträge dürfen vom Planer opportunistisch als Batch-Item an
   einen ohnehin laufenden Step angehängt werden (auch epic-übergreifend
   — einzige Lockerung von §10.6) statt auf eine separate
   Nutzer-Entscheidung zu warten. Der Kritiker markiert erfolgreich
   umgesetzte `auto_fixable`-Einträge selbst als `erledigt` — einzige
   Stelle, an der ein Subagent den Tech-Debt-Status automatisch ändert.

5. **Terseness-Regel ausgeweitet** (`spec.md` §10.7): Die bisher nur für
   `step-review.md` geltende Kürzungsregel („Prosa nur, wenn sie
   Verhalten ändert") gilt jetzt projektweit für alle generierten
   Artefakte (`step-result.md`, `task-summary.md`, Tech-Debt-Einträge,
   Roadmap-Epic-Zeilen). Neu dazu: Anleitungstext (`<...>`-Blöcke) aus
   den Templates wird beim Ausfüllen ersetzt, nicht zusätzlich stehen
   gelassen. Klarstellung: Der Hebel ist Informationsdichte, nicht die
   Format-Familie — Markdown bleibt, weil es für ein LLM der günstigste
   Ausdrucksweg ist, kein Wechsel zu JSON/YAML.

**Geänderte Dateien:** `planning/orchestrator.md`,
`planning/templates/konzept.md`, `drift-loop/spec.md`,
`drift-loop/orchestrator.md`, `drift-loop/README.md`,
`drift-loop/skills/{planer,coder,kritiker}/SKILL.md`,
`drift-loop/templates/{step-plan,step-result,step-review,task-state,task-summary,tech-debt}.md`,
neu: `drift-loop/templates/codemap.md`.

**Commit:** `07ebf8b`
