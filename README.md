# Agent-Scaffolding

Wiederverwendbare Workflow-Bausteine für agentische Coding-Sessions —
Markdown-Dateien, die eine KI-Session anweisen, eine bestimmte Rolle
(Orchestrator, Planer, Coder, Kritiker, …) zu übernehmen und einen
definierten Prozess abzuarbeiten. Kein Code, keine Abhängigkeiten außer
einem Werkzeug, das Subagenten/Sub-Konversationen mit isoliertem Kontext
starten kann.

**Struktur:** eine Flow-Familie pro Top-Level-Ordner, jede mit eigener
`README.md` (Intention, wann benutzt man was). Konventionen für
Änderungen an diesem Repo selbst (Sprache, Frontmatter, Commit-Stil, …)
stehen in [`AGENTS.md`](AGENTS.md).

## Verfügbare Flow-Familien

- **[`dev-loop/`](dev-loop/README.md)** — Konzept schärfen → planen →
  umsetzen → prüfen, für Software-Änderungen jeder Größe (Audits,
  Refactorings, Feature-Implementierungen).

Weitere Flow-Familien (z. B. für Analyse, Recherche, …) kommen als
eigene Geschwister-Ordner dazu, wenn Bedarf entsteht.

## Prompt-Bausteine

Neben den orchestrierten Flow-Familien gibt es
**[`prompts/`](prompts/README.md)** — einzelne, platzhalterfreie
Markdown-Dateien zum direkten Referenzieren im Chat, ohne Orchestrator
oder eigenen Zustand. Für offene Überlegungen/Sparring statt eines
mehrstufigen Prozesses.

## Recherche-Notizen

**[`docs/references.md`](docs/references.md)** sammelt externe Quellen
und Kernaussagen aus Diskussionen über diesen Scaffolding-Ansatz (z. B.
Vergleich mit aktuellen Industriestandards) — Anker für spätere
Überlegungen, kein Anspruch auf Vollständigkeit.

## Einbindung in ein Projekt

Dieses Repo — **https://github.com/RalfHuesing/Agent-Scaffolding** —
wird nicht kopiert, sondern per `git subtree` in Zielprojekte
eingebunden: Änderungen an einem Ort, für alle Projekte verfügbar,
sobald dort nachgezogen wird. Projekt-spezifische Konventionen
(Coding-Rules, Architektur-Leitplanken) bleiben **im jeweiligen Projekt**
und wandern nicht hierher — die Dateien in diesem Repo sind bewusst
projekt-unabhängig. Wo genau diese Konventionen liegen
(`.agents/rules/`, `.cursor/rules/`), muss nicht vorab festgelegt
werden — die Workflows in `dev-loop/` erkennen das selbst (siehe
`dev-loop/drift-loop/spec.md` §3.1).

Wo genau du dieses Repo innerhalb eines Projekts platzierst (`.agents/
Agent-Scaffolding/`, `tools/agent-scaffolding/`, …), ist egal — alle
Verweise zwischen den Dateien hier sind relativ zueinander formuliert.
`--prefix` in den Befehlen unten entsprechend anpassen.

**Wichtig:** `git subtree` synct nicht automatisch — jede Aktion unten
musst du explizit anstoßen, in jedem Zielprojekt einzeln.

### Erstmalig einbinden

```bash
git subtree add --prefix=.agents/Agent-Scaffolding https://github.com/RalfHuesing/Agent-Scaffolding main --squash
```

### Updates reinholen

```bash
git subtree pull --prefix=.agents/Agent-Scaffolding https://github.com/RalfHuesing/Agent-Scaffolding main --squash
```

### Lokale Änderungen zurück in dieses Repo pushen

Selten nötig — nur falls direkt im eingebundenen Ordner eines
Zielprojekts editiert wurde, statt hier zentral:

```bash
git subtree push --prefix=.agents/Agent-Scaffolding https://github.com/RalfHuesing/Agent-Scaffolding main
```
