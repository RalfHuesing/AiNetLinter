# AGENTS.md — Agent-Scaffolding

## Was ist das hier?

Kein Software-Projekt — nur Markdown-Dateien, die eine KI-Coding-Session
anweisen, eine Rolle zu übernehmen (Orchestrator, Planer, Coder, Auditer,
Sparringspartner, …). Kein Build, kein Test, keine Dependencies.

**Geltungsbereich:** Diese Datei gilt für Änderungen **innerhalb dieses
Ordners** — auch wenn er per `git subtree` in ein anderes Projekt
eingebunden ist (siehe `README.md`, „Einbindung in ein Projekt"). Für das
übergeordnete Zielprojekt gilt sein eigenes `AGENTS.md`/seine eigenen
Konventionen, nicht diese Datei — verschachtelte `AGENTS.md`-Dateien sind
Teil des Standards, die jeweils nächstgelegene gilt für den Ordner, in
dem gerade gearbeitet wird.

## Konventionen für Änderungen hier

- Sprache: Deutsch, Du-Form/Imperativ in der Prosa. Strukturelle
  Keywords (Frontmatter-Felder, Dateinamen) bleiben Englisch.
- Datei mit `version:` im Frontmatter geändert → Version bumpen + Eintrag
  im `## Changelog`-Abschnitt am Dateiende (falls die Datei einen hat —
  nicht jede ältere Datei hier hat schon einen).
- Dateien mit Verweisen auf andere dev-loop-interne Dateien brauchen
  einen `## Pfad-Hinweis`-Abschnitt (Beispiel: `dev-loop/task-loop/spec.md`).
- `templates/**` nutzen Platzhalter (`<TASK-NAME>`), `prompts/**` bewusst
  nicht — dort kommt der Kontext aus dem, was der Nutzer im Chat direkt
  danach ergänzt.
- Neue Flow-Familie/Kategorie = eigener Ordner mit eigener `README.md`.
- Commits: Conventional Commits, deutsch (`feat(dev-loop): ...`,
  `docs: ...`).
- Git-Historie nie umschreiben (kein amend/rebase/force-push).

## Wo ist was?

- `dev-loop/` — orchestrierte Multi-Step-Workflows (Konzept → Plan →
  Code → Audit). Tiefer: `dev-loop/README.md`,
  `dev-loop/task-loop/spec.md`.
- `prompts/` — einzelne, platzhalterfreie Referenz-Snippets ohne
  Orchestrator. Tiefer: `prompts/README.md`.
- `docs/` — gesammelte externe Quellen/Recherche, siehe
  `docs/references.md`.
