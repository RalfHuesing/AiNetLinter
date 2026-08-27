---
status: complete
task: get-file-tree
derived_from: Konzept.md
created_at: 2026-08-26T22:05:00+02:00
last_updated: 2026-08-27
created_by_model: GPT-5 (Codex)
created_by_model_knowledge_cutoff: nicht im Systemkontext angegeben
---

# Roadmap: get-file-tree

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers. Die Pakete bleiben in sich geschlossen; der Coder
führt vor seinem Commit den vollständigen Gate-Lauf aus, während der Kritiker
den grünen Nachweis übernimmt und den vollständigen Lauf nur bei konkreter
Unklarheit oder begründetem Fehlerverdacht wiederholt.

## Tech-Stack-Notiz

Aus den Build-/Test-Konfigurationen und den Projektregeln abgeleitet:

- **Build-Command:** `dotnet build` (Solution `AiNetLinter.slnx`, vier Projekte, `net10.0`, `TreatWarningsAsErrors=true`).
- **Test-Command:** Entwicklungsslices mit `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` oder `Category=Component`; Abschluss-Gate mit `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`. Stresstests nur ausdrücklich mit `Category=Stress`.
- **Lint-Command:** `dotnet run --project src/AiNetLinter -- --config rules.json --path AiNetLinter.slnx`.
- **Code-Style-Kurzfassung:** C#/.NET 10 mit Nullable und impliziten Usings; konkrete Klassen `sealed`, kurze Methoden (max. 60 Produktionszeilen), höchstens vier Parameter, Records für Input-/Outputmodelle, keine stillen `catch`-Blöcke, kein `dynamic`, kein blockierender Task-Zugriff, keine repo-spezifischen Hardcodings oder DI-Container; Windows-/PowerShell-kompatible, plattformbewusst normalisierte Pfade.
- **Commit-Konventionen:** Conventional Commits auf Deutsch im Imperativ (`feat:`, `fix:`, `docs:`, `chore:`); Änderungen erhalten zusätzlich einen `### Commit-Vorschlag`-Block in der Agentenantwort.
- **Review-/Testkadenz:** Der Coder liefert den vollständigen grünen Gate-Nachweis; die unabhängige Prüfung von Plan, Regeln, Logik und Konzepttreue bleibt Pflicht, ohne routinemäßige Wiederholung desselben vollständigen Testlaufs durch den Kritiker.

## Regel-Index

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — Verbindliche MCP-first-Werkzeugwahl für semantische C#-Fragen, absolute `projectRoot`-Übergabe und ergänzende Textsuche für Nicht-C#-Dateien.
- `.agents/rules/AiNetLinter.mdc` — Generierte C#-Qualitätsregeln, Metrikgrenzen und Resilienzvorgaben für nullable, sealed, kurze und warnungsfreie Implementierungen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Architektur-, Windows-, Test-, Dokumentations-, Commit- und Drift-Präventionsregeln einschließlich xUnit-v3- und `TestTempDirectory`-Vorgaben.

## Epics

- [x] **EPIC-01: Projektgebundener Dateisystemzugang** — Den bestehenden `projectRoot`-/Registry-Vertrag um einen eng begrenzten filesystem-only Dispatch und einen boundary-sicheren relativen `root`-Resolver erweitern, sodass physische Enumeration unabhängig von Roslyn-Loading möglich wird und bestehende Roslyn-Toolverträge unverändert bleiben (`Konzept.md`, „MCP- und Projektvertrag“, „Sicherheitskonzept“). Erledigt durch Step 001 und den Gate-Folgeschritt 002; beide Reviews sind `approved`.
- [x] **EPIC-02: Gemeinsame Walk- und Filtergrundlage** — Durch Step 003 umgesetzt: Der zentrale physische Walk unterstützt Tiefe, Cancellation, Standardausschlüsse, Reparse-Point-Schutz und partielle Warnungen; die Glob-/Pfadsemantik wird DRY wiederverwendet.
- [x] **EPIC-03: File-Tree-Scan und Antwortmodell** — Der physische Einmal-Walk besitzt Input-Validierung, Extension-/Pfadfilter, Ausschlüsse, Größen-/Verzeichnisaggregation, stabile Sortierung, Antwortlimits und Completeness-Metadaten. `summary`, `tree` und `files` werden aus demselben Scanresult abgeleitet.
- [x] **EPIC-04: MCP-Wiring, Verifikation und Produktdokumentation** — `get_file_tree` ist in der File-Structure-Gruppe als read-only/idempotentes Tool registriert. Unit-, Component-, MCP-Handshake-, Integrations- und Vertragsabdeckung sowie die betroffenen MCP-/Agent-Dokumente sind synchronisiert.
