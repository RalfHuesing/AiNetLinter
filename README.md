# AiNetLinter — .NET C# Linter für agentischen Entwicklungsworkflow

`AiNetLinter` ist ein .NET 10 CLI-Tool, das C#-Code per Roslyn-Syntaxanalyse gegen konfigurierbare Qualitätsregeln prüft. Die Regeln sind auf den agentischen Entwicklungsworkflow mit AI-Tools wie Cursor, Claude Code oder GitHub Copilot ausgelegt — mit dem Ziel, die Fehlerrate autonomer Agenten beim Bearbeiten von C#-Code zu senken.

Die wissenschaftlichen Grundlagen der Regelauswahl sind in der [Design-Rationale](Docs/rationale.md) dokumentiert.

---

## Wann einsetzen?

AiNetLinter ist **kein Ersatz für Compiler oder Tests** — es setzt dort an, wo Build und Tests bereits grün sind:

```
dotnet build  ✓
dotnet test   ✓
ainetlinter   ← hier
```

Der Linter prüft keine Syntaxfehler oder Laufzeitverhalten, sondern Designqualität: Komplexität, KI-taugliche Codestruktur, Architektur-Constraints. Er macht den Code besser analysierbar — für Menschen und für AI-Agenten.

---

## Schnellstart

```bash
ainetlinter --config rules.json --path ./src/MeinProjekt.slnx
```

Der Linter gibt einen Markdown-Report auf stdout aus und beendet sich mit Exit-Code `0` (keine neuen Verstöße) oder `1` (Verstöße gefunden — CI-tauglich).

---

## Agentische Integration

AiNetLinter ist selbst-erklärend: Die eingebauten Discovery-Commands ermöglichen einem KI-Agenten, das Tool explorativ zu verstehen und eigenständig in ein Projekt zu integrieren — ohne Vorab-Konfiguration durch den Entwickler.

```bash
# Tool erkunden (kein --path nötig):
ainetlinter --list-rules
ainetlinter --list-evals
ainetlinter --describe-rule EnforceSealedClasses
ainetlinter --docs configuration

# Eval-Audit-Prompts assemblieren (inkl. frischer Evidenz und Spezifikation):
ainetlinter --eval naming-drift --path ./src/ --spec README.md > prompt.md

# Lint-Lauf:
ainetlinter --config rules.json --path ./src/MeinProjekt.slnx

# Auto-Fix für triviale Verstöße (sealed, nullable, PascalCase):
ainetlinter --config rules.json --path ./src/ --fix --dry-run
ainetlinter --config rules.json --path ./src/ --fix
```

**Typischer Einstieg:** `AiNetLinter` in ein eigenes Verzeichnis außerhalb des Projekts installieren (z. B. `C:\Tools\AiNetLinter\`). Das Tool bringt mehrere Dateien mit, lässt sich so von mehreren Projekten gleichzeitig nutzen, und Updates sind an einer einzigen Stelle erledigt. Den Pfad zur Exe einem Agenten im Projektkontext übergeben — dieser exploriert das Tool über die Discovery-Commands und integriert es eigenständig, z. B. als Schritt in einem Test- oder CI-Skript.

Vollständige Agent-API-Referenz (alle Flags, Workflows, Error-Format): [Docs/agent-api.md](Docs/agent-api.md)

---

## Ausgewählte Regeln — aus ca. 35 konfigurierbaren Einstellungen

| Regel | Warum relevant |
| :--- | :--- |
| **Codebase-Landkarten & Evals** (`--map`, `--eval`) | Generiert strukturierte Markdown-Übersichten (vocabulary, structure, hotspots, skeleton) oder vollständig assemblierte LLM-Audit-Prompts mit frischer Evidenz für Drift-Audits. |
| **Baseline / Ratchet** (`--baseline`) | Friert bestehende Verstöße per SHA-256 ein — nur geänderte Dateien werden geprüft. Macht den Linter in Legacy-Projekten mit tausenden Altlasten sofort einsetzbar. |
| **AI-Context-Footprint** (`MaxAIContextFootprint`) | Misst die transitiven Codezeilen, die ein KI-Modell für eine Klasse laden müsste. Direkte Metrik für Kontextbudget-Verbrauch im agentischen Workflow. |
| **Phantom-Dependency-Ban** (`DetectAndBanPhantomDependencies`) | Verbietet nicht auflösbare Namespaces und Reflection-Lade-APIs — verhindert die häufigste Halluzinations-Fehlerquelle in KI-generiertem Code. |
| **Komplexitätsgrenzen** (`MaxCyclomaticComplexity`, `MaxCognitiveComplexity`) | Jahrzehntelange Forschung (McCabe 1976, SonarSource) belegt Komplexität als stärksten Einzel-Prädiktor für Fehlerdichte und schlechte Analysierbarkeit durch KI. |
| **Project Overrides** (`ProjectOverrides`) | Projektscharfe Regelanpassungen (z. B. `*.Tests` mit lockeren Limits) ermöglichen praxistaugliche Konfigurationen ohne eine Einheitslösung für alle Projekttypen. |
| **Compound-Suppressions** (`CompoundSuppressions`) | Ermöglicht kontextabhängige Regelunterdrückung und unterstützt `SeverityOverride: "warning"` — Verstöße in Szenario A (Bedingungen erfüllt, RelaxedLimit überschritten) können auf Warning herabgestuft werden, ohne den Build zu blockieren. |
| **LINQ-Kettenlänge** (`MaxLinqChainLength`) | Begrenzt die Anzahl verketteter LINQ-Methoden pro Ausdruckskette, um kognitive Last zu reduzieren. Durch eine konfigurierbare Whitelist werden Builder-Ketten ignoriert. |
| **Globales Scope-Filtering** (`--project`, `--namespace`) | Ermöglicht die gezielte Eingrenzung der Analyse auf bestimmte Projekte oder C#-Namespaces (inkl. Wildcard-Unterstützung und Ausschluss-Shortcut für Test-Projekte). |
| **Web-Asset-Linting** (CSS, JS, Razor) | Web-Asset-Analyse fuer CSS (ExCSS), JS (Esprima) und Razor. Begrenzt Dateigroessen, erzwingt ES6-Module, verbietet globale Zuweisungen an 'window', limitiert HTML-Verschachtelungstiefe, komplexe Inline-Lambdas, Control-Flow-Bloecke, verschachtelte Foreach-Schleifen, Komponenten-Parameter und Ternaries in HTML-Attributen — da diese Faktoren die Fehlerrate autonomer Agenten bei Web-Edits massiv erhoehen. Opt-in ueber `Web.IsEnabled = true`. |

---

## Dokumentation

| Dokument | Inhalt |
| :--- | :--- |
| [Docs/agent-api.md](Docs/agent-api.md) | Agent-API: alle CLI-Flags, Workflows, Error-Format, Discovery-Commands |
| [Docs/configuration.md](Docs/configuration.md) | Vollständige Konfigurationsreferenz, CLI-Parameter, Workflows |
| [Docs/rationale.md](Docs/rationale.md) | Design-Entscheidungen & wissenschaftliche Grundlagen |

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
