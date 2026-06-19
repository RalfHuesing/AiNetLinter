# AiNetLinter — .NET C# Linter für agentischen Entwicklungsworkflow

`AiNetLinter` ist ein .NET 10 CLI-Tool, das C#-Code per Roslyn-Syntaxanalyse gegen konfigurierbare Qualitätsregeln prüft. Die Regeln sind auf den agentischen Entwicklungsworkflow mit AI-Tools wie Cursor, Claude Code oder GitHub Copilot ausgelegt — mit dem Ziel, die Fehlerrate autonomer Agenten beim Bearbeiten von C#-Code zu senken.

Die wissenschaftlichen Grundlagen der Regelauswahl sind in der [Design-Rationale](Docs/rationale.md) dokumentiert.

---

## Schnellstart

```bash
ainetlinter --config rules.json --path ./MeinProjekt.slnx
```

Für AI-Agenten — vollständige Dokumentation auf stdout:
```bash
ainetlinter --readme
```

---

## Dokumentation

| Dokument | Inhalt |
| :--- | :--- |
| [Docs/configuration.md](Docs/configuration.md) | Vollständige Konfigurationsreferenz, CLI-Parameter, Workflows |
| [Docs/rationale.md](Docs/rationale.md) | Design-Entscheidungen & wissenschaftliche Grundlagen |

---

## Top 5 — von ca. 35 konfigurierbaren Einstellungen

| Feature | Kurzbeschreibung |
| :--- | :--- |
| **Baseline / Ratchet** (`--baseline`) | Friert bestehende Verstöße per SHA-256 ein — nur geänderte Dateien werden geprüft. Macht den Linter in Legacy-Projekten mit tausenden Altlasten sofort einsetzbar. |
| **AI-Context-Footprint** (`MaxAIContextFootprint`) | Misst die transitiven Codezeilen, die ein KI-Modell für eine Klasse laden müsste. Direkte Metrik für Kontextbudget-Verbrauch im agentischen Workflow. |
| **Phantom-Dependency-Ban** (`DetectAndBanPhantomDependencies`) | Verbietet nicht auflösbare Namespaces und Reflection-Lade-APIs — verhindert die häufigste Halluzinations-Fehlerquelle in KI-generiertem Code. |
| **Komplexitätsgrenzen** (`MaxCyclomaticComplexity`, `MaxCognitiveComplexity`) | Jahrzehntelange Forschung (McCabe 1976, SonarSource) belegt Komplexität als stärksten Einzel-Prädiktor für Fehlerdichte und schlechte Analysierbarkeit durch KI. |
| **Project Overrides** (`ProjectOverrides`) | Projektscharfe Regelanpassungen (z. B. `*.Tests` mit lockeren Limits) ermöglichen praxistaugliche Konfigurationen ohne eine Einheitslösung für alle Projekttypen. |

---

## Features

- **Roslyn-basierte semantische Analyse** — echte Semantik, kein textbasiertes Heuristik-Grep
- **Feingranulares Regelwerk** — Klassendesign, Komplexität, Immutabilität, Namensgebung
- **Strukturmetriken** — `MaxBoolParameterCount`, `MaxPartialClassFiles`, `MaxPublicMembersPerType`, `MaxDirectoryChildren` begrenzen API-Surface und Typ-Fragmentierung
- **BanPublicNestedTypes** — Verbietet `public`/`internal` nested Typen; verbessert Grep-/File-Listing-Navigation für KI-Agenten und verhindert FQN-Halluzinationen
- **UI-Trennungsregeln** — Blazor CSS-Isolation & Code-Behind-Pflicht, WPF minimales Code-Behind (MVVM)
- **Auto-Fixer (`--fix`)** — `sealed`, `readonly`, `#nullable enable` automatisch korrigieren
- **Baseline/Ratchet (`--baseline`)** — inkrementelle Migration bestehender Codebases
- **Playbook-Generator (`--playbook`)** — Repo-Übersicht als Kontext für AI-Agenten
- **Cursor-Regeln-Sync (`--sync-cursor-rules`)** — `.cursor/rules/AiNetLinter.mdc` aus `rules.json` generieren
- **Impact-Analyse (`--impact`)** — betroffene Call-Sites bei Signaturänderungen ermitteln
- **Markdown-Report** — standardmäßige, token-effiziente und gut lesbare Ausgabe für AI-Agenten
- **Analyse-Cache** — inkrementelle Laufzeitoptimierung für den Agentic Loop
