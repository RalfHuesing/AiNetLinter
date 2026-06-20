---
description: Repo-Statistik, bei Architektur-Fragen lesen
globs: 
alwaysApply: false
---
# AI Repository Playbook (Auto-Generated)
Auto-generiert durch AiNetLinter 1.0.55 aus `C:\Daten\Entwicklung\Ralf\AiNetLinter\rules.json`.
Dieses Dokument wurde automatisiert durch den **AiNetLinter** erzeugt.
Es dient als Orientierungshilfe fuer KI-Assistenten (wie Cursor), um sich an die Codierungsrichtlinien, Architekturmuster und Ausnahmen dieser Codebase anzupassen.

## 1. Genutzte Architekturmuster
- **Result-Pattern-Nutzung:** 5 Methoden liefern `Result` oder `Result<T>` zurueck.
- **Kontrollfluss-Exceptions:** 28 `throw`-Anweisungen wurden im Code-Rumpf gefunden.

## 2. Abweichungen / Unterdrueckte Linter-Regeln
Folgende Regeln werden in diesem Projekt bewusst unterdrueckt:

- **MaxMethodParameterCount:** 3 mal deaktiviert.
  *Bedeutung:* Zu viele Methodenparameter (max. 4).

## 3. Migrations-Status

- **Wave-ready Dateien:** 206 / 220 (94 %)
- **Verstösse nur wave-ready (default rules):** 0
- **Top-Ordner wave-ready-Verstöße:**
  - Keine offenen Verstöße in wave-ready Dateien.

## 4. Architektur-Slices (nach Ordner)

- **src/AiNetLinter/**: 124 files, median Footprint 69 LOC, 5× disable-all
- **src/AiNetLinter.Tests/**: 95 files, median Footprint 121 LOC, 9× disable-all
- **DefaultRunnerReporters.cs/**: 1 files, median Footprint 11 LOC

## 5. Empfohlene Agenten-Priorität (aus RuleMetadata + Counts)

| Intent | Offene Verstöße (wave-ready) | Regeln |
| :--- | ---: | :--- |
| - | 0 | Keine offenen Verstöße |

