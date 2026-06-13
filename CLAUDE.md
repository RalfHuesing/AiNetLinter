# AiNetLinter — Claude Code Guidelines

## Projektregeln

Die verbindlichen Coding-Richtlinien, Architektur-Constraints und Linter-Metriken liegen in `.cursor/rules/`. **Bitte diese Dateien beim Start einer Aufgabe lesen:**

| Datei | Inhalt | Wann lesen |
| :--- | :--- | :--- |
| [.cursor/rules/AiNetLinter.mdc](.cursor/rules/AiNetLinter.mdc) | C#-Qualitätsmetriken & Grenzwerte (auto-generiert aus `rules.json`) | Immer — bei jeder Code-Änderung |
| [.cursor/rules/AiNetLinterRichtlinien.mdc](.cursor/rules/AiNetLinterRichtlinien.mdc) | Architektur-Verbote, Workflow, Update-Pflichten | Immer — bei jeder Code-Änderung |
| [.cursor/rules/playbook.md](.cursor/rules/playbook.md) | Repo-Statistik, Migrations-Status, Architektur-Slices | Bei Architektur- oder Refactoring-Fragen |

> Die `.mdc`-Dateien enthalten Cursor-spezifisches Frontmatter (`alwaysApply`, `globs`) — das ignorieren. Der Inhalt darunter gilt unverändert auch für Claude Code.

## Orientierungshilfen

- **Abhängigkeitsgraph:** [Docs/codegraph.md](Docs/codegraph.md) — auto-generierter Mermaid-Graph der Klassenstruktur. Bei unbekannter Architektur als erstes lesen.
- **Regelquelle:** `rules.json` ist die Single Source of Truth für alle Linter-Regeln. Änderungen dort regenerieren die `.mdc`-Dateien.

## Technologie

- .NET 10, C#, xUnit v3
- Kein DI-Container, kein Plugin-System, statische Kompilierung
- CLI-Tool (monolithisch)

## Commits

Conventional Commits auf Deutsch, imperativ. Beispiel: `feat: Regel XY ergänzt`.
