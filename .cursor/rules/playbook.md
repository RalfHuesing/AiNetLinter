---
description: Repo-Statistik, bei Architektur-Fragen lesen
globs: 
alwaysApply: false
---
# AI Repository Playbook (Auto-Generated)
Auto-generiert durch AiNetLinter 1.0.53 aus `C:\Daten\Entwicklung\Ralf\AiNetLinter\rules.json`.
Dieses Dokument wurde automatisiert durch den **AiNetLinter** erzeugt.
Es dient als Orientierungshilfe fuer KI-Assistenten (wie Cursor), um sich an die Codierungsrichtlinien, Architekturmuster und Ausnahmen dieser Codebase anzupassen.

## 1. Genutzte Architekturmuster
- **Result-Pattern-Nutzung:** 5 Methoden liefern `Result` oder `Result<T>` zurueck.
- **Kontrollfluss-Exceptions:** 28 `throw`-Anweisungen wurden im Code-Rumpf gefunden.

## 2. Abweichungen / Unterdrueckte Linter-Regeln
Folgende Regeln werden in diesem Projekt bewusst unterdrueckt:

- **MaxCyclomaticComplexity:** 1 mal deaktiviert.
  *Bedeutung:* Zu hohe zyklomatische Komplexitaet (max. 12).
- **MaxCognitiveComplexity:** 1 mal deaktiviert.
  *Bedeutung:* Zu hohe kognitive Komplexitaet (max. 15).
- **MaxLineCount:** 1 mal deaktiviert.
  *Bedeutung:* Dateizeilenlimit (max. 700 Zeilen) ueberschritten.
- **MaxMethodLineCount:** 1 mal deaktiviert.
  *Bedeutung:* Methode hat zu viele Codezeilen (max. 60 Zeilen).

## 3. Migrations-Status

- **Wave-ready Dateien:** 194 / 208 (93 %)
- **Verstösse nur wave-ready (default rules):** 3
- **Top-Ordner wave-ready-Verstöße:**
  - `src/AiNetLinter/Core/Checkers/`: 3

## 4. Architektur-Slices (nach Ordner)

- **src/AiNetLinter/**: 117 files, median Footprint 66 LOC, 5× disable-all
- **src/AiNetLinter.Tests/**: 90 files, median Footprint 117 LOC, 9× disable-all
- **DefaultRunnerReporters.cs/**: 1 files, median Footprint 11 LOC

## 5. Empfohlene Agenten-Priorität (aus RuleMetadata + Counts)

| Intent | Offene Verstöße (wave-ready) | Regeln |
| :--- | ---: | :--- |
| agent-context | 3 | MaxMethodParameterCount |

