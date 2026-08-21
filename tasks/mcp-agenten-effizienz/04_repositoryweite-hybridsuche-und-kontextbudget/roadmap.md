---
status: active
task: 04_repositoryweite-hybridsuche-und-kontextbudget
derived_from: tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget.md
created_at: 2026-08-21
last_updated: 2026-08-21
created_by_model: GPT-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
---

# Roadmap: Repositoryweite Hybridsuche und Kontextbudget

Diese Roadmap leitet grobe Umsetzungsepen aus dem Konzept ab. Sie erweitert das bestehende `search_pattern` additiv: Die bisherige Textantwort und ihre Aufrufer bleiben kompatibel, während eine deterministische strukturierte Antwort, sichere Repository-Suchbereiche, explizite Budgets und optionaler C#-Kontext hinzukommen.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build` über `AiNetLinter.slnx`; Ziel-Framework ist `net10.0`, Warnungen sind Fehler.
- **Test-Command:** Schnelle Iteration mit `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` beziehungsweise `Category=Component`; Abschluss mit `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
- **Lint-Command:** `dotnet run --project src/AiNetLinter -- --config rules.json --path .`; bei Regeländerungen zusätzlich `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`.
- **Code-Style-Kurzfassung:** Nullable und implizite Usings sind aktiv; konkrete Klassen sind bevorzugt `sealed`, Methoden und Parameter bleiben klein, die Suche bleibt generisch und nutzt zentrale Ausschlusslogik; Roslyn liefert optionale Semantik, nicht textbasierte Scheinsicherheit.
- **Commit-Konventionen:** Deutsche imperative Conventional Commits; bei einer späteren Umsetzung sind Drift-Loop-Suffix und der verpflichtende `### Commit-Vorschlag`-Block der Projektregeln zu beachten. In diesem Planungsschritt wird nicht committed.

## Regel-Index

- `.agents/rules/AiNetLinter.mdc` — Generierte Qualitätsregeln aus `rules.json`, darunter Größen-, Komplexitäts-, Naming-, Nullability- und Duplikatvorgaben.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Manuelle Architektur-, MCP-, Test-, Dokumentations-, Windows/PowerShell- und Agenten-Workflow-Regeln.

## Epics

- [ ] **EPIC-01 — Baseline, Scope-Modell und Messgrundlage**
  - Bestehendes Textverhalten, MCP-Wire-Verhalten, alle aktuellen Dateityp-Fixtures und die bisherigen Ausschlüsse als Kompatibilitätsbaseline festhalten.
  - Ein neutrales Mehrsprach-Fixture und messbare Proxies für Treffer, Antwortgröße, Laufzeit und notwendige Folgeaufrufe definieren; Repository-, Lösungs- und Snapshot-Grenzen ausdrücklich festlegen.

- [ ] **EPIC-02 — Strukturierte lexikalische Suche bei Textkompatibilität**
  - Ein deterministisches Suchergebnis mit relativen Forward-Slash-Pfaden, 1-basierten Positionen, unverändertem Zeilentext, mehreren Trefferbereichen, optionalem Kontext und Projektzuordnung konzipieren und ausgeben.
  - Plain- und Regex-Suche sowie bestehende `pattern`, `isRegex` und `maxResults`-Semantik erhalten; Textformatierung und strukturierte Nutzlast als getrennte Ausgabeschichten behandeln.

- [ ] **EPIC-03 — Repositoryweite Scope-, Filter- und Budgetsteuerung**
  - Einen sicheren, generischen Suchbereich mit Include-/Exclude-Filtern und konservativen Standardausschlüssen für Build-, VCS-, temporäre, generierte, binäre und minifizierte Dateien schaffen, ohne projekt- oder agentenspezifische Pfade fest einzubauen.
  - Treffer-, Datei-, Kontext- und Antwortbudgets sowie Regex-Timeout, Cancellation, Encoding/BOM, unlesbare Dateien und Vollständigkeitsstatus modellieren; Trunkierungsgründe und übersprungene Dateien maschinenlesbar machen.

- [ ] **EPIC-04 — Optionale Roslyn-Anreicherung für C#**
  - Lexikalische Treffer bei expliziter Aktivierung mit stabilen C#-Kategorien, Symbol-/Dokumentations-IDs, Projektbezug und vorhandenen Roslyn-Positionen anreichern.
  - Unaufgelöste, mehrdeutige, außerhalb des Snapshots liegende oder nicht anwendbare Semantik explizit kennzeichnen und niemals aus Text allein ableiten.

- [ ] **EPIC-05 — MCP-Vertrag, Registrierung, Ressourcen und Dokumentation**
  - Tool-Signatur und Beschreibung, `McpToolResults`-Struktur, Legacy-Text, Fehler-/Loading-Politik, Overview-Ressource und globale MCP-Hinweise konsistent erweitern; die knappe UTF-8-Grenze der Server-Instruktionen berücksichtigen.
  - Raw-Wire-, SDK-, Fixture-, Ressourcen- und Dokumentationstests ergänzen und README, `Docs/agent-api.md`, `Docs/integration.md`, Overview-/Tool-Beschreibungen sowie gegebenenfalls `Docs/ROADMAP.md` synchronisieren.

- [ ] **EPIC-06 — Effektivitäts-, Performance- und Abschlussvalidierung**
  - Die neue Suche gegen die Baseline mit großen, gemischten und problematischen Dateien prüfen und Antwortgröße, Trefferverlust, Laufzeit, Abbruch- und Folgeaufrufverhalten nachvollziehbar messen.
  - Einen optionalen `rg`-Vergleich nur als Diagnose-/Messprototyp bewerten; keine Produktionsabhängigkeit einführen, solange der verwaltete Scanner die Anforderungen erfüllt.

