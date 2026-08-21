---
status: done
task: 04_repositoryweite-hybridsuche-und-kontextbudget
derived_from: tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget.md
created_at: 2026-08-21
last_updated: 2026-08-21
created_by_model: GPT-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
---

# Roadmap: Repositoryweite Hybridsuche und Kontextbudget

Diese Roadmap leitet grobe Umsetzungsepen aus dem Konzept ab. Sie erweitert das bestehende `search_pattern` additiv: Die bisherige Textantwort und ihre Aufrufer bleiben kompatibel, während eine deterministische strukturierte Antwort, sichere Repository-Suchbereiche, explizite Budgets und optionaler C#-Kontext hinzukommen.

## Fortschritt und nächste JIT-Grenze

- EPIC-01 ist durch `step-001` und den approved Korrektur-Step `step-002` abgeschlossen: Baseline, sicherer Scan, Cancellation und Legacy-Fehlerstatus sind im aktuellen Code geprüft.
- EPIC-02 ist abgeschlossen: StructuredContent, MatchRanges, Kontext und Legacy-Formatter stammen aus dem gemeinsamen Scanner-Ergebnis.
- EPIC-03 ist abgeschlossen: Solution-Scope, Filter, Standardausschlüsse, Antwort-/Trefferbudgets und Completeness sind implementiert und getestet.
- `step-003` und der approved Korrektur-Step `step-004` haben EPIC-04 sowie die unmittelbar erforderlichen EPIC-05-Vertrags- und Dokumentationsänderungen abgeschlossen. Der Schnitt liegt nach dem stabilisierten lexikalischen Kern und vor jeder Wirksamkeits-/Performance-Messung.
- `step-005` hat EPIC-06 abgeschlossen: Baseline-/Fixture-Evaluation, messbare Antwort-/Treffer-/Datei-/Laufzeit-/Cancellation-/Folgeaufruf-Proxies und eine objektive Abschlussentscheidung liegen vor.
- Der Drift-Audit-Nachweis bleibt unverändert maßgeblich: Im Such-/MCP-Scope gibt es keinen offenen auto-fixbaren Tech-Debt. Außerhalb liegende Audit-Cluster werden nicht opportunistisch konsolidiert.
- `TD-003-001` wurde in `step-004` erledigt. Weitere Audit-Beobachtungen sind im `tech-debt.md` mit Begründung als außerhalb dieses Tasks liegend dokumentiert.

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

- [x] **EPIC-01 — Baseline, Scope-Modell und Messgrundlage** (`step-001`, korrigiert und approved in `step-002`)
  - Bestehendes Textverhalten, MCP-Wire-Verhalten, alle aktuellen Dateityp-Fixtures und die bisherigen Ausschlüsse als Kompatibilitätsbaseline festhalten.
  - Ein neutrales Mehrsprach-Fixture und messbare Proxies für Treffer, Antwortgröße, Laufzeit und notwendige Folgeaufrufe definieren; Repository-, Lösungs- und Snapshot-Grenzen ausdrücklich festlegen.

- [x] **EPIC-02 — Strukturierte lexikalische Suche bei Textkompatibilität** (`step-001`, Korrekturstatus in `step-002` approved)
  - Ein deterministisches Suchergebnis mit relativen Forward-Slash-Pfaden, 1-basierten Positionen, unverändertem Zeilentext, mehreren Trefferbereichen, optionalem Kontext und Projektzuordnung konzipieren und ausgeben.
  - Plain- und Regex-Suche sowie bestehende `pattern`, `isRegex` und `maxResults`-Semantik erhalten; Textformatierung und strukturierte Nutzlast als getrennte Ausgabeschichten behandeln.

- [x] **EPIC-03 — Repositoryweite Scope-, Filter- und Budgetsteuerung** (`step-001`, Korrekturstatus in `step-002` approved)
  - Einen sicheren, generischen Suchbereich mit Include-/Exclude-Filtern und konservativen Standardausschlüssen für Build-, VCS-, temporäre, generierte, binäre und minifizierte Dateien schaffen, ohne projekt- oder agentenspezifische Pfade fest einzubauen.
  - Treffer-, Datei-, Kontext- und Antwortbudgets sowie Regex-Timeout, Cancellation, Encoding/BOM, unlesbare Dateien und Vollständigkeitsstatus modellieren; Trunkierungsgründe und übersprungene Dateien maschinenlesbar machen.

- [x] **EPIC-04 — Optionale Roslyn-Anreicherung für C#** (`step-003`, Korrektur in `step-004`, approved)
  - Lexikalische Treffer bei expliziter Aktivierung mit stabilen C#-Kategorien, Symbol-/Dokumentations-IDs, Projektbezug und vorhandenen Roslyn-Positionen anreichern.
  - Unaufgelöste, mehrdeutige, außerhalb des Snapshots liegende oder nicht anwendbare Semantik explizit kennzeichnen und niemals aus Text allein ableiten.

- [x] **EPIC-05 — MCP-Vertrag, Registrierung, Ressourcen und Dokumentation** (mit `step-003`/`step-004` kompatibel gebündelt und approved)
  - Tool-Signatur und Beschreibung, `McpToolResults`-Struktur, Legacy-Text, Fehler-/Loading-Politik, Overview-Ressource und globale MCP-Hinweise konsistent erweitern; die knappe UTF-8-Grenze der Server-Instruktionen berücksichtigen.
  - Raw-Wire-, SDK-, Fixture-, Ressourcen- und Dokumentationstests ergänzen und README, `Docs/agent-api.md`, `Docs/integration.md`, Overview-/Tool-Beschreibungen sowie gegebenenfalls `Docs/ROADMAP.md` synchronisieren.

- [x] **EPIC-06 — Effektivitäts-, Performance- und Abschlussvalidierung** (`step-005`, approved; einschließlich Entscheidung über einen rein diagnostischen `rg`-Vergleich)
  - Die neue Suche mit dem bestehenden `SymbolGraphMini`-Fixture und kontrolliert erzeugten problematischen Dateien gegen einen unbudgetierten Fixture-Oracle prüfen; Plain-/Regex-, gemischte Dateitypen, C#-Opt-in, Kontext, Binär-/Encoding-/generierte Dateien, Regex-Timeout und Cancellation abdecken.
  - UTF-8-Bytes von Legacy-Text, Structured-Payload und kombinierter Toolantwort sowie sichtbare/gesamte Treffer- und Dateizahlen, begründeten Verlust, Laufzeitverteilungen und explizite Folgeaufrufe messen. Tokenersparnis und allgemeine Performanceversprechen bleiben ausgeschlossen.
  - Einen direkten `rg`-Vergleich nur optional und test-/diagnostikseitig ausführen; Verfügbarkeit, Trefferparität und Laufzeit getrennt dokumentieren, aber keine Produktionsabhängigkeit und kein Pflicht-Gate einführen.
  - Nur bei belastbaren, wiederholbaren Ergebnissen öffentliche Aussagen in `README.md` oder `Docs/ROADMAP.md` ergänzen; andernfalls bleiben die Ergebnisse auf `step-005/step-result.md` beschränkt.
