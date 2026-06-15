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

## Features

- **Roslyn-basierte semantische Analyse** — echte Semantik, kein textbasiertes Heuristik-Grep
- **Feingranulares Regelwerk** — Klassendesign, Komplexität, Immutabilität, Namensgebung
- **Auto-Fixer (`--fix`)** — `sealed`, `readonly`, `#nullable enable` automatisch korrigieren
- **Baseline/Ratchet (`--baseline`)** — inkrementelle Migration bestehender Codebases
- **Playbook-Generator (`--playbook`)** — Repo-Übersicht als Kontext für AI-Agenten
- **Cursor-Regeln-Sync (`--sync-cursor-rules`)** — `.cursor/rules/AiNetLinter.mdc` aus `rules.json` generieren
- **Impact-Analyse (`--impact`)** — betroffene Call-Sites bei Signaturänderungen ermitteln
- **SARIF-Export (`--format sarif`)** — für CI/CD-Integration
- **Analyse-Cache** — inkrementelle Laufzeitoptimierung für den Agentic Loop
