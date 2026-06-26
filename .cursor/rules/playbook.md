---
description: Repo-Statistik, bei Architektur-Fragen lesen
globs: 
alwaysApply: false
---
# AI Repository Playbook (Auto-Generated)
Auto-generiert durch AiNetLinter 1.0.68 aus `C:\Daten\Entwicklung\Ralf\AiNetLinter\rules.json`.
Dieses Dokument wurde automatisiert durch den **AiNetLinter** erzeugt.
Es dient als Orientierungshilfe fuer KI-Assistenten (wie Cursor), um sich an die Codierungsrichtlinien, Architekturmuster und Ausnahmen dieser Codebase anzupassen.

## 1. Genutzte Architekturmuster
- **Result-Pattern-Nutzung:** 5 Methoden liefern `Result` oder `Result<T>` zurueck.
- **Kontrollfluss-Exceptions:** 30 `throw`-Anweisungen wurden im Code-Rumpf gefunden.

## 2. Abweichungen / Unterdrueckte Linter-Regeln
Folgende Regeln werden in diesem Projekt bewusst unterdrueckt:

- **without:** 1 mal deaktiviert.
  *Bedeutung:* Regel 'without'.
- **RAZOR_MaxControlFlowBlocks:** 1 mal deaktiviert.
  *Bedeutung:* Zu viele Control-Flow-Bloecke (max. 8).

## 3. Migrations-Status

- **Wave-ready Dateien:** 248 / 266 (93 %)
- **Verstösse nur wave-ready (default rules):** 0
- **Top-Ordner wave-ready-Verstöße:**
  - Keine offenen Verstöße in wave-ready Dateien.

## 4. Architektur-Slices (nach Ordner)

- **src/AiNetLinter/**: 149 files, median Footprint 76 LOC, 6× disable-all
- **src/AiNetLinter.Tests/**: 116 files, median Footprint 121 LOC, 12× disable-all
- **DefaultRunnerReporters.cs/**: 1 files, median Footprint 11 LOC

## 5. Empfohlene Agenten-Priorität (aus RuleMetadata + Counts)

| Intent | Offene Verstöße (wave-ready) | Regeln |
| :--- | ---: | :--- |
| - | 0 | Keine offenen Verstöße |

