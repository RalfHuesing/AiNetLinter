# AGENTS.md — Agent-Scaffolding

## Was ist das hier?

Kein Software-Projekt — nur Markdown-Dateien, die eine KI-Coding-Session
anweisen, eine Rolle zu übernehmen (Orchestrator, Planer, Coder, Kritiker,
Sparringspartner, …). Kein Build, kein Test, keine Dependencies.

**Geltungsbereich:** Diese Datei gilt für Änderungen **innerhalb dieses
Ordners** — auch wenn er per `git subtree` in ein anderes Projekt
eingebunden ist (siehe `README.md`, „Einbindung in ein Projekt"). Für das
übergeordnete Zielprojekt gilt sein eigenes `AGENTS.md`/seine eigenen
Konventionen, nicht diese Datei — verschachtelte `AGENTS.md`-Dateien sind
Teil des Standards, die jeweils nächstgelegene gilt für den Ordner, in
dem gerade gearbeitet wird.

## Regeln

Verbindlich, vor Änderungen lesen — `.agents/rules/`:

- [`doku-ist-stand.md`](.agents/rules/doku-ist-stand.md) — Dateien
  beschreiben den Ist-Stand, nicht ihre Geschichte: kein Changelog, keine
  `version:`-Felder, keine „vorher/jetzt/neu"-Formulierungen. Begründungen
  („warum die Regel so ist") bleiben ausdrücklich erhalten.
- [`verweise-aufloesen.md`](.agents/rules/verweise-aufloesen.md) — jeder
  `§`-Verweis muss auflösen; wird ein Verweisziel entfernt, wird der
  Inhalt ausgeschrieben statt paraphrasiert.

Diese Regeln gelten für dieses Repo. Wird es per `git subtree` in ein
Projekt eingebunden, liegt `.agents/rules/` hier **innerhalb** des
Unterordners und ist damit nicht das `rules_dir` des Zielprojekts (das
wird projekt-root-relativ erkannt, siehe `dev-loop/drift-loop/spec.md`
§3.1).

## Konventionen für Änderungen hier

- Sprache: Deutsch, Du-Form/Imperativ in der Prosa. Strukturelle
  Keywords (Frontmatter-Felder, Dateinamen) bleiben Englisch.
- Zeilen im Fließtext bei ~72 Zeichen umbrechen.
- Dateien mit Verweisen auf andere dev-loop-interne Dateien brauchen
  einen `## Pfad-Hinweis`-Abschnitt (Beispiel: `dev-loop/drift-loop/spec.md`).
- `templates/**` nutzen Platzhalter (`<TASK-NAME>`), `prompts/**` bewusst
  nicht — dort kommt der Kontext aus dem, was der Nutzer im Chat direkt
  danach ergänzt.
- Neue Flow-Familie/Kategorie = eigener Ordner mit eigener `README.md`.
- Commits: Conventional Commits, deutsch (`feat(dev-loop): ...`,
  `docs: ...`).
- Git-Historie nie umschreiben (kein amend/rebase/force-push).

## Wo ist was?

- `dev-loop/` — orchestrierte Multi-Step-Workflows (Konzept → Plan →
  Code → Kritik). Tiefer: `dev-loop/README.md`,
  `dev-loop/drift-loop/spec.md`.
- `prompts/` — einzelne, platzhalterfreie Referenz-Snippets ohne
  Orchestrator. Tiefer: `prompts/README.md`.
- `docs/` — gesammelte externe Quellen/Recherche, siehe
  `docs/references.md`.
