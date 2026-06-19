---
description: Repo-Statistik, bei Architektur-Fragen lesen
globs: 
alwaysApply: false
---
# AI Repository Playbook (Auto-Generated)
Auto-generiert durch AiNetLinter 1.0.50 aus `C:\Daten\Entwicklung\Ralf\AiNetLinter\rules.json`.
Dieses Dokument wurde automatisiert durch den **AiNetLinter** erzeugt.
Es dient als Orientierungshilfe fuer KI-Assistenten (wie Cursor), um sich an die Codierungsrichtlinien, Architekturmuster und Ausnahmen dieser Codebase anzupassen.

## 1. Genutzte Architekturmuster
- **Result-Pattern-Nutzung:** 5 Methoden liefern `Result` oder `Result<T>` zurueck.
- **Kontrollfluss-Exceptions:** 28 `throw`-Anweisungen wurden im Code-Rumpf gefunden.

## 2. Abweichungen / Unterdrueckte Linter-Regeln
Folgende Regeln werden in diesem Projekt bewusst unterdrueckt:

- **MaxLineCount:** 1 mal deaktiviert.
  *Bedeutung:* Dateizeilenlimit (max. 700 Zeilen) ueberschritten.
- **MaxMethodLineCount:** 1 mal deaktiviert.
  *Bedeutung:* Methode hat zu viele Codezeilen (max. 60 Zeilen).

## 3. Migrations-Status

- **Wave-ready Dateien:** 165 / 180 (92 %)
- **Verstösse nur wave-ready (default rules):** 0
- **Top-Ordner wave-ready-Verstöße:**
  - Keine offenen Verstöße in wave-ready Dateien.

## 4. Architektur-Slices (nach Ordner)

- **src/AiNetLinter/**: 101 files, median Footprint 70 LOC, 6× disable-all
- **src/AiNetLinter.Tests/**: 78 files, median Footprint 132 LOC, 9× disable-all
- **DefaultRunnerReporters.cs/**: 1 files, median Footprint 11 LOC

## 5. Empfohlene Agenten-Priorität (aus RuleMetadata + Counts)

| Intent | Offene Verstöße (wave-ready) | Regeln |
| :--- | ---: | :--- |
| - | 0 | Keine offenen Verstöße |

