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

- **Regelquelle:** `rules.json` ist die Single Source of Truth für alle Linter-Regeln. Änderungen dort regenerieren die `.mdc`-Dateien.

## Technologie

- .NET 10, C#, xUnit v3
- Kein DI-Container, kein Plugin-System, statische Kompilierung
- CLI-Tool (monolithisch)

## Commits

Conventional Commits auf Deutsch, imperativ. Beispiel: `feat: Regel XY ergänzt`.

## Begriffs-Glossar (Vocabulary Alignment)

Um zukünftigen Naming-Drift zu vermeiden, gilt folgende Übersetzung von Spezifikations-Begriffen in Code-Identifiers:

- **Ratchet-Prinzip (Baseline-Ratchet)**: Im Code ausschließlich als `Baseline` bezeichnet (z. B. `BaselineViolationFilter`, `CliBaselineOptions`).
- **Vertical Slices (Namespace-Abhängigkeiten)**: Im Code abgebildet durch `NamespaceCoupling` (z. B. `NamespaceCouplingChecker`) und `ForbiddenNamespaceDependencies` (in Konfiguration).
- **Regel-Prüfer**: Generell als `*Checker` benennen (z. B. `SealedClassChecker`). Suffix `*Detector` ist reserviert für rein passive Zustandserkennung ohne Generierung von Violations (z. B. `GeneratedCodeDetector`).
- **AI-Context-Footprint**: Bezeichnet die transitiven Codezeilen einer Klasse für LLM-Agenten. Im Code als `AIContextFootprint` bzw. `AIContextFootprintCalculator` bezeichnet.
- **Bulk-Suppression / Disable-All-Kommentare**: Das Einfügen/Entfernen von dateiweiten Deaktivierungskommentaren. Im Code als `DisableAllCommentInjector` bzw. `DisableAllCommentRemover` bezeichnet.


