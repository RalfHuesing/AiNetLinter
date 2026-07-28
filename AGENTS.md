# AiNetLinter – Agent Instructions & Development Rules

Willkommen beim **AiNetLinter**-Projekt! Dieses Dokument dient KI-Agenten (Antigravity, Cursor, Windsurf, Roo, etc.) als primäre Orientierung und Handlungsanleitung für Entwicklung, Refactoring und Wartung in diesem Repository.

---

## 1. Projekt-Überblick & Architektur

**AiNetLinter** ist eine hochperformante, Roslyn-basierte C#/.NET 9 Statische-Code-Analyse- & Linter-Engine zur Durchsetzung von Architekturregeln, Clean-Code-Standards und Konventionen.

### Schlüsselkomponenten:
- **Engine & Core CLI**: `src/AiNetLinter/`
  - `Cli/`: Argument-Parsing und CLI-Optionen System (System.CommandLine basiert).
  - `Generators/`: SyntaxWalker, Agent-Rules Sync, Skeleton Map & Playbook Generierung.
  - `Rules/`: Roslyn-basierte Regel-Implementierungen.
  - `Diagnostics/`: Performance-Profiler und Messungen.
- **Unit & Integration Tests**: `src/AiNetLinter.Tests/` (xUnit, Roslyn Workspace/MSBuild Workspaces).
- **Konfiguration**: `rules.json` definiert das aktive Regelwerk und Parameter.
- **Agent-Regeln (.agents)**: `.agents/rules/` enthält generierte Regelsätze (`.mdc`), insbesondere `.agents/rules/AiNetLinter.mdc`.
- **Dokumentation**: `Docs/` enthält Systemdokumentation, CLI-Referenzen und Anleitungen.

---

## 2. Entwicklungs- & Test-Workflow

### Verifikation vor und nach Änderungen
1. **Tests ausführen**: Nach JEDER Code-Änderung MÜSSEN die Tests ausgeführt und bestanden werden:
   ```bash
   dotnet test
   ```
2. **Build prüfen**:
   ```bash
   dotnet build
   ```

> [!IMPORTANT]
> Beende einen Task erst, wenn `dotnet test` grün durchgelaufen ist!

---

## 3. Architektur- & Codier-Richtlinien

1. **Fehlerbehandlung / Result-Pattern**:
   - Methoden in der Linter-Engine nutzen bevorzugt das `Result`- oder `Result<T>`-Pattern für erwartbare Fehler.
   - Exceptions (`throw`) sind Ausnahmesituationen oder unerwarteten Programmierfehlern vorbehalten.
2. **Immutability & Performance**:
   - Roslyn-SyntaxTree & SemanticModel Zugriffe sparsam halten.
   - Record-Types für unveränderliche Datenstrukturen nutzen.
3. **Symptom-Fixing Verboten**:
   - Keine fehlgeschlagenen Unit-Tests einfach auskommentieren oder Assertions abschwächen.
   - Ursachen immer in der underlying Engine oder im Rule-Processor beheben.

---

## 4. Dokumentations- & Regel-Synchronisation

- **Regel- oder CLI-Änderungen**:
  Wenn CLI-Optionen, `rules.json`-Schemata oder Regel-Verhalten geändert werden, MÜSSEN folgende Dokumente aktualisiert werden:
  - `Docs/configuration.md`
  - `Docs/ROADMAP.md` (falls Meilensteine betroffen sind)
- **Agenten-Regeln Sync**:
  Die Agenten-Regeldatei `.agents/rules/AiNetLinter.mdc` wird aus `rules.json` generiert und kann mit folgenden Befehlen synchronisiert werden:
  ```bash
  dotnet run --project src/AiNetLinter -- --sync-agent-rules-only
  ```
  *(Hinweis: `playbook.md` wird nicht mehr in `.agents/rules/` abgelegt).*

---

## 5. Agenten-Verhaltensregeln (Sparring & Planning)

- **Erst Mitdenken, dann Umsetzen**: Bei größeren Vorhaben oder neuen Features erst kurz im Sparring-Modus die Idee spiegeln und Vor-/Nachteile abwägen.
- **Kompakte Antworten**: Antworten prägnant halten – Fazit und Kernaussage zuerst.
