---
status: active  # active | done
task: <TASK-NAME>
derived_from: konzept.md
created_at: <ISO-8601>
last_updated: <ISO-8601>
created_by_model: <Modell-ID deiner eigenen LLM-Instanz>
created_by_model_knowledge_cutoff: <Knowledge-Cutoff-Datum, z. B. 2026-01>
---

# Roadmap: <TASK-NAME>

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

<Aus dem Projekt abgeleitet, einmalig hier (nicht pro Step neu):>

- **Build-Command:** <...>
- **Test-Command:** <...>
- **Lint-Command:** <falls vorhanden>
- **Code-Style-Kurzfassung:** <aus `<rules_dir>/**`>
- **Commit-Konventionen:** <Conventional Commits? Sprache/Form?>

## Regel-Index

<Ein Eintrag pro Datei in `<rules_dir>/**` — **Kurzbeschreibung, kein
Volltext**. Zweck: Der Step-Modus-Planer ist pro Aufruf eine frische,
isolierte Session ohne Erinnerung an diesen Roadmap-Modus-Aufruf — er
kann `<rules_dir>/**` nicht bei jedem Step neu komplett lesen (Kosten),
liest aber diesen Index (steht ja schon hier in `roadmap.md`) und dann
gezielt nur die 1-2 Dateien, die zum aktuellen Step passen, siehe
`../spec.md` §7.2 / `../skills/planer/SKILL.md` Schritt 4a. Wird laufend
gepflegt: fällt beim Roadmap-Abgleich (Schritt 1, Step-Modus) eine neue,
im Index fehlende Regeldatei auf, wird sie hier ergänzt.>

- `<rules_dir>/<datei-1>.md` — <ein Satz: worum es in dieser Datei geht>
- `<rules_dir>/<datei-2>.md` — <ein Satz>

## Epics

<Ein Epic = grober Cluster mehrerer Steps, kein einzelner Step. Format:>

- [ ] EPIC-01: <Kurztitel> — <1-2 Sätze Ziel, Bezug zu `konzept.md`
      Abschnitt X>
- [ ] EPIC-02: <Kurztitel> — <...>
- [x] EPIC-00: <Kurztitel> — <...> (→ step-001, step-002)

<Bei teilweise erledigten Epics: Notiz statt Haken, z. B.
„- [ ] EPIC-03: ... (in Arbeit → step-004; noch offen: Teil B)".>

<Bei obsolet gewordenen Epics (siehe `../spec.md` §11): nicht löschen,
sondern markieren, z. B. „- [ ] ~~EPIC-05: ...~~ obsolet — <Grund>,
siehe step-007".>

<Begründungen gehören **an das Epic**, nicht in eine Liste darunter: Ein
Epic, das der Planer nachträglich ergänzt hat, trägt den Grund in seiner
eigenen Zeile, z. B. „- [ ] EPIC-03: ... (Muss-Haben aus `konzept.md`
§X, ohne Entsprechung in der ursprünglichen Roadmap — erkannt in
step-004)". Wann das passiert ist, steht in `git log`.>
