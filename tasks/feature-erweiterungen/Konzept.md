---
status: draft
title: C#-only MCP- und Analyseerweiterungen
created: 2026-09-06
updated: 2026-09-06
---

# C#-only MCP- und Analyseerweiterungen

## Ziel

AiNetLinter soll für Coding-Agenten präzise, kompakte und belastbare C#-Kontexte liefern. Die Roslyn-basierte Analyse bleibt die einzige semantische Wahrheitsschicht. Erweiterungen müssen den bestehenden Linter-, Diagnostic- und MCP-Verträgen folgen und dürfen keine zweite C#-Analysearchitektur etablieren.

Der Schwerpunkt liegt auf vier Fähigkeiten:

1. zusammenhängender Kontext für Symbole, Aufrufer, Aufrufe, Tests und Violations;
2. explizite Aussage darüber, wie aktuell und vollständig ein Ergebnis ist;
3. sichtbare Herkunft und Vertrauenswürdigkeit inferierter Beziehungen;
4. begrenzte, deterministische MCP-Antworten mit nachvollziehbarer Truncation.

Frameworkspezifische Beziehungen werden erst danach und ausschließlich als kontrollierte Roslyn-basierte Erweiterung betrachtet.

## Leitentscheidungen

- C# only bleibt eine harte Scope-Grenze.
- Roslyn-Symbole, SemanticModels und stabile Symbolidentitäten bleiben maßgeblich.
- Bestehende Tools werden erweitert und gebündelt; parallele Ersatztools werden vermieden.
- Unsichere Heuristiken werden niemals als sichere semantische Beziehungen ausgegeben.
- Jede begrenzte Antwort beschreibt ihre Lücken maschinenlesbar und menschenlesbar.
- Aktualität und Analysegeneration gehören zum öffentlichen MCP-Vertrag.
- File-Watching bleibt optional; korrekte, kontrollierte Aktualisierung ist wichtiger als maximale Reaktionsgeschwindigkeit.
- Ein konkurrierender Parser-/Indexpfad und Multi-Language-Unterstützung sind keine Ziele.

## Teilkonzepte

| Priorität | Datei | Inhalt | Empfehlung |
|---|---|---|---|
| P1 | 01-csharp-kontext-exploration.md | Kompakter, zusammenhängender Symbol-/Änderungskontext | umsetzen |
| P1 | 02-freshness-snapshot-vertrag.md | Aktualität, Snapshot und degradierte Analysezustände | umsetzen |
| P1 | 03-provenance-confidence.md | Herkunft und Vertrauensgrad von Beziehungen | umsetzen |
| P1 | 04-mcp-response-budgets.md | Größenlimits, Ranking und Vollständigkeit | umsetzen |
| P2 | 05-csharp-framework-inferenz.md | Kontrollierte Roslyn-Beziehungen für DI und ASP.NET | später |
| P1 | 06-mcp-robustheit-und-sicherheit.md | Fehler-, Pfad-, Session- und Lebenszyklusverträge | umsetzen/gegenprüfen |

## Betroffene bestehende Bereiche

- src/AiNetLinter/Mcp/: MCP-Server, Tool-Registrierung, Ergebnisverträge, Sessions und Health.
- src/AiNetLinter/Mcp/Tools/FeatureContext/: gebündelter Symbolkontext.
- src/AiNetLinter/Mcp/Tools/SymbolGraph/: Roslyn-basierte Symbol- und Referenzauflösung.
- src/AiNetLinter/Core/: Solution-Lebenszyklus, Refresh, Diff- und Impact-Analyse.
- src/AiNetLinter/Rules/ und bestehende Heuristiken: Diagnosen und ausdrücklich markierte Inferenz.
- src/AiNetLinter.FastTests/ und src/AiNetLinter.IntegrationTests/: Verifikation.

Die aktuelle Implementierung und die aktuellen MCP-Schemas sind vor jedem Umsetzungspaket erneut zu prüfen.

## Belegte Ausgangslage

Die folgenden bestehenden Bereiche bilden die fachliche Grundlage des Drafts:

- [McpCodeGraphServer.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/McpCodeGraphServer.cs>): residenter Solution-Kontext, Refresh, Staleness und Health.
- [FeatureContextModels.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextModels.cs>): bestehendes Bündeln von Deklaration, Metrics, Callers, Tests und Violations.
- [FindReferencesTool.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs>): Roslyn-basierte Symbol- und Referenzauflösung.
- [ChangeContextResponseModels.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/ChangeContextResponseModels.cs>): bestehender Änderungs-/Impact-Kontext mit Symbolen, Call-Sites, Tests, Violations und Testempfehlungen.

Diese Dateien sind Referenzpunkte, keine unveränderlichen Zielverträge. Vor Umsetzung sind die tatsächlich registrierten Tools, Modelle und Tests erneut zu lesen.

## Muss-Kriterien

- Alle neuen oder erweiterten Project-MCP-Tools arbeiten ausschließlich auf C#-/Roslyn-Kontext.
- Ergebnisse enthalten eine eindeutige Analysegeneration oder einen vergleichbaren Snapshot-Bezug.
- Loading-, Updating-, Stale- und Degraded-Zustände werden nicht als frisch ausgegeben.
- Truncation und fehlende Daten werden strukturiert gemeldet.
- Semantische und inferierte Beziehungen sind anhand von Herkunft und Vertrauensgrad unterscheidbar.
- Fehlerantworten bewahren den Unterschied zwischen echter Fehlfunktion und verwertbarer Unvollständigkeit.
- Bestehende öffentliche Toolverträge bleiben rückwärtskompatibel, sofern keine Versionierung beschlossen wird.
- Build und beide vollständigen Nicht-Stress-Testläufe bleiben grün.

## Non-Goals

- kein zweiter Parser oder zweiter semantischer Index für C#;
- keine allgemeine Multi-Language-Analyse;
- keine UI-, Telemetrie- oder Standalone-Bundle-Arbeit;
- keine Vollständigkeitsbehauptung für Reflection, dynamische DI, Source Generators oder Laufzeit-Routing;
- kein permanenter Watcher als Voraussetzung für korrekte Ergebnisse;
- keine neue generische Graphdatenbank;
- keine automatische Änderung von Quellcode oder Projektdateien.

## Gesamt-Akzeptanzkriterien

- Ein Agent gelangt von einem bekannten C#-Symbol zu Deklaration, relevanten Aufrufern/Aufrufen, Tests, Violations und begrenztem Quellcode ohne unkontrolliert viele Einzelabfragen.
- Jedes gebündelte Ergebnis weist Snapshot und Vollständigkeit aus.
- Jede inferierte Beziehung enthält Herkunft, Evidenz und Vertrauensgrad.
- Große Lösungen erzeugen deterministisch begrenzte Antworten mit nachvollziehbarer Auslassungsinformation.
- Pfad-, Session-, Refresh- und Fehlerfälle sind getestet.
- Kein Teilkonzept führt eine konkurrierende Wahrheitsschicht neben Roslyn ein.

## Betriebs- und Bedrohungsmodell

AiNetLinter läuft als lokaler MCP-Dienst für ein freigegebenes Projekt oder eine Assembly. MCP-Eingaben sind untrusted input. Pfade dürfen nicht aus dem Zielroot ausbrechen oder sensible Verzeichnisse erschließen. Große oder fehlerhafte Eingaben dürfen den Daemon nicht dauerhaft blockieren und keine ungebundenen Antworten erzeugen.

Akzeptierte Grenzen:

- Roslyn kann Laufzeitverhalten, Reflection und dynamische Bindung nicht vollständig beweisen.
- Ein Snapshot kann während einer Bearbeitung kurz hinter dem Dateisystem liegen.
- Framework-Inferenz ist weniger sicher als Compiler-Semantik.
- Eine explizit unvollständige Antwort ist besser als eine unmarkierte Vollständigkeitsbehauptung.

## Fehler-, Fallback- und Lebenszeitsemantik

- Initialisierung und Refresh dürfen MCP-Sessions nicht unnötig lange blockieren.
- Ohne geladenen Snapshot werden keine Analyseergebnisse erfunden.
- Ein recoverable Ergebnis bleibt erfolgreich, wenn damit sicher weitergearbeitet werden kann.
- Pfadverletzungen, interne Ausnahmen und nicht erfüllbare Toolverträge sind echte Fehler.
- Der letzte erfolgreiche Snapshot darf weiterverwendet werden, muss aber als möglicherweise veraltet gekennzeichnet sein.
- Caches, Worker und Watcher gehören zum bestehenden Daemon-/Projektlebenszyklus.

## Geplante Verifikation

Pro Teilkonzept:

- FastTests für Modelle, Ranking, Budgets und Zustandsübergänge;
- IntegrationTests für MCP-JSON, Refresh, Pfadsicherheit und Daemon-Lebenszyklus;
- gezielte MCP-Abfragen gegen das aktuelle AiNetLinter-Projekt;
- Regressionstests für bestehende Toolantworten.

Vor Abschluss des Gesamtvorhabens zwingend:

    dotnet build
    dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
    dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress

Zusätzlich: relevanter MCP-Workflow und abschließender Audit auf DRY, Dead Code, Magic Values und Refactoring-Drift.

## Dokumentationsbedarf

- MCP-Toolreferenz und Ergebnisverträge aktualisieren.
- Aktualität, Snapshotsemantik, Unsicherheit und Truncation dokumentieren.
- Bei sichtbaren Tool- oder CLI-Änderungen Docs/configuration.md und gegebenenfalls Docs/ROADMAP.md aktualisieren.
- Agent-Instructions erst nach stabiler Implementierung synchronisieren.

## Offene Punkte

1. Soll Kontext zuerst bestehende Tools erweitern oder einen eigenen Explore-Namen erhalten? Empfehlung: bestehende Tools erweitern.
2. Soll das harte Antwortbudget in Bytes, Zeichen oder Profilen angegeben werden? Empfehlung: internes Byte-Limit plus strukturierte Truncation.
3. Welche Framework-Inferenz ist nach P1 produktrelevant? Empfehlung: DI und ASP.NET-Routen getrennt bewerten.
4. Welche Confidence-Schwellen gelten? Empfehlung: erst nach positiven und negativen Testfällen festlegen.

## Arbeitsgedächtnis (nur Draft)

- Nutzerentscheidung: Das Vorhaben wird als neue Features/Erweiterungen dokumentiert; die historische Inspirationsquelle wird in den dauerhaften Dokumenten nicht genannt.
- Nutzerziel: Alle relevanten Entscheidungen sollen in den Dokumenten stehen, damit die externe Referenz nicht mehr als Datenbasis benötigt wird.
- Analysebasis: AiNetLinter besitzt bereits residenten Roslyn-Solution-Kontext, Staleness-/Health-Informationen, get_feature_context, Symbolgraph-/Referenztools und change-context/Impact-Funktionen.
- Vorläufige Kernentscheidung: Keine parallele Parser-/Graph-/Persistenzarchitektur. Der Nutzen liegt in MCP-Kontext, Status, Provenance, Budgets und gezielter C#-Inferenz.
- Status bleibt draft, bis Ziel, Scope und offene Vertragsentscheidungen bestätigt oder angepasst wurden.
