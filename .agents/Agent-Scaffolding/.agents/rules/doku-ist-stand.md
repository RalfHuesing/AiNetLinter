---
rule: doku-ist-stand
applies_to: "**/*.md"
---

# Dateien beschreiben den Ist-Stand, nicht ihre Geschichte

Die Markdown-Dateien in diesem Repo sind **Prompts**, kein Archiv. Sie
werden vollständig in den Kontext eines Agenten geladen — `spec.md` einmal
pro Orchestrator-Session, jede `skills/**/SKILL.md` in **jeden** einzelnen
Subagenten-Aufruf. Alles, was ein Agent nicht zum Handeln braucht, wird
bei jedem dieser Aufrufe erneut bezahlt.

Daraus folgt: Eine Datei beschreibt ausschließlich, **wie es ist**.

## Verboten

- **Kein `## Changelog`-Abschnitt**, keine Versions-Historie, keine
  „Was hat sich in 0.3 geändert"-Liste.
- **Kein `version:`-Feld** im Frontmatter. `status:` (z. B. `draft`) ist
  dagegen erlaubt — das ist eine Aussage über den Ist-Zustand.
- **Keine temporalen Formulierungen im Fließtext:** „jetzt vier statt
  drei", „Es gab keinen Mechanismus für X", „(neu)", „vorher
  wurde…", „behebt eine Lücke der alten Fassung", „wie gehabt".
  Streichen oder in eine Ist-Aussage umschreiben.

Wer wissen will, was sich wann geändert hat, liest `git log` — dort steht
es vollständiger, aktueller und kostenlos. Ein handgepflegter Changelog
daneben ist zusätzlich fehleranfällig: er kann von der Datei abweichen,
und niemand merkt es.

## Ausdrücklich erlaubt und erwünscht: Begründungen

**Kausale Aussagen sind kein Changelog.** Der Unterschied:

| | |
|---|---|
| **Temporal** (raus) | *warum die Datei heute anders aussieht als früher* — „Ebene 4 wurde ergänzt", „seit 0.2 liest der Planer den Index" |
| **Kausal** (bleibt) | *warum die Regel so ist, wie sie ist* — „Ein eigener Kanal statt Blocken, weil sonst der Scope des Loops unkontrolliert wächst" |

Abschnitte wie „Warum das existiert", „Warum ein eigener Kanal statt
Blocken" oder „Die Grenze dieser Regel" bleiben unangetastet. Sie sind
der Grund, warum ein Agent eine Regel nicht als überflüssig
wegoptimiert — ohne sie wäre die Datei kürzer und schlechter. Räumst du
nach dieser Regel auf, wirf sie nicht versehentlich mit weg.

## Gilt auch für Task-Artefakte

Dieselbe Linie in `templates/**`: kein chronologisches Ereignis-Log in
`roadmap.md` oder `task-state.md`. Der Zustand steht in der Steps-Tabelle
und in den Checkboxen, die Chronologie in `git log` und in den
`step-NNN/`-Dateien.

Muss eine Begründung erhalten bleiben (etwa: warum ein Epic obsolet ist,
warum ein Epic ergänzt wurde), gehört sie **an das Epic selbst** als
Ist-Aussage — nicht in eine Liste darunter, die festhält, wann das
passiert ist.
