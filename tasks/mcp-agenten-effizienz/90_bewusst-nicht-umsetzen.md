---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: small
priority: P9
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-20
open_questions: []
---

# Bewusst nicht umsetzen: unbelegte oder redundante MCP-Erweiterungen

## Ziel

Diese Datei hält verworfene Optionen und ihre überprüfbaren Gründe fest. Sie verhindert, dass ein späterer Agent dieselben Ideen ohne neue Evidenz als vermeintliche Optimierung implementiert.

## Entscheidungsregel

Eine verworfene Idee darf nur wieder geöffnet werden, wenn mindestens eine neue Bedingung erfüllt ist:

- reproduzierbare Nutzungs-/Hostdaten zeigen ein konkretes Problem,
- der MCP-/SDK-Vertrag ändert die technischen Voraussetzungen,
- ein neutraler C#-Benchmark zeigt einen Qualitäts- oder Größengewinn,
- der Nutzer erweitert den erlaubten Tech-Stack ausdrücklich.

## 1. Kein neues Natural-Language-Tool `locate_task`

Ohne Embeddings/RAG kann eine freie Aufgabenbeschreibung nur lexikalisch gegen Identifier, Pfade und Kommentare gematcht werden. `find_symbol`, `search_pattern`, `get_namespace_tree`, `metrics_tree` und `get_feature_context` decken diese deterministischen Sichten bereits ab. Ein neues Tool würde denselben Bestand mit schwer belegbarer Ranking-Heuristik erneut exponieren.

**Entscheidung:** nicht implementieren. Neue lexikalische Suchprimitive nur als Erweiterung bestehender Tools und nur mit neutralem Benchmark.

## 2. Kein neues `get_change_context`

Ein weiteres Composite-Tool erhöht Toolkatalog und Auswahlraum. Git-Diff-Impact liegt bereits in `get_impact`; Aufgabe 05 erweitert genau dieses Tool additiv.

**Entscheidung:** bestehendes Tool erweitern, keinen Alias und kein zweites Schema registrieren.

## 3. Kein separates `get_diagnostics` / `validate_file`

Roslyn-/MSBuild-Compile-Probleme werden bereits als Workspace-Diagnostics bzw. Warnhinweise in mehreren MCP-Antworten sichtbar. Ein Agent kann zusätzlich `dotnet build`/`dotnet test` ausführen. Das bestehende `Tasks/features/06-nicht-umsetzen.md` dokumentiert dieselbe Redundanz.

**Entscheidung:** keinen neuen Diagnostics-Endpunkt. Falls Diagnostics schwer auffindbar sind, vorhandene Antwortmetadaten vereinheitlichen, nicht eine dritte Analysepipeline bauen.

## 4. Keine sofortige Abschaffung oder Zusammenlegung bestehender Tools

Für eine Entfernung von `pattern_detect`, `safeguard`, `metrics_tree`, `get_hotspots` oder anderen Tools liegen keine Nutzungsdaten vor. Der rohe `tools/list`-Payload ist messbar, aber daraus folgt nicht, welches Tool entbehrlich ist. Eine Entfernung wäre ein Breaking Change; der gegenwärtige Haupt-Overhead im beobachteten Codex-Host stammt aus wiederholten globalen Instructions, nicht aus einem einzelnen kleinen Tool.

**Entscheidung:** keine Removal-Aktion. Erst Observability-Nutzungsdaten über mehrere fremde C#-Codebasen sammeln, danach Deprecation mit mindestens einer Release-Übergangsphase planen.

## 5. Keine Toolprofile ohne Hostnachweis

Profile wie `core`, `analysis`, `full` sind technisch möglich, helfen aber nur, wenn ein Host alle Toolschemas tatsächlich in den Modellkontext legt und nicht selbst lazy lädt/filtert. Sie erzeugen außerdem Konfigurationsvarianten, Dokumentationsaufwand und Fälle, in denen dem Agenten ein benötigtes Tool fehlt.

**Entscheidung:** vorerst nicht implementieren. Aufgabe 02 beseitigt den belegten globalen Overhead. Profile erst bei Messungen in mindestens zwei unterstützten Hosts neu bewerten.

## 6. Kein pauschaler Output-Schema-Rollout

SDK 2.2.0 unterstützt `UseStructuredContent` und explizites `OutputSchema` auch bei direkter Rückgabe von `CallToolResult`. Technisch ist ein Rollout möglich. Er vergrößert jedoch garantiert `tools/list`; ein Qualitäts- oder Token-Nettonutzen ist im Projekt nicht gemessen. Zudem sind Antwortformen einzelner Tools derzeit noch inkonsistent, insbesondere bei transitiven Graphen.

**Entscheidung:** zuerst Aufgabe 03 und 04 stabilisieren. Output-Schemas danach höchstens als separaten Pilot für ein Composite-Tool messen; kein flächiger Rollout auf Vermutung.

## 7. Keine Cursorpagination für Analyseergebnisse

Die aktuellen Analysen sind an Solution-Snapshot, Git-Diff und Konfiguration gebunden. Cursor müssten Snapshotidentität und Ablauf korrekt behandeln oder könnten zwischen Seiten inkonsistente Daten liefern. Für die derzeitigen Toolmengen reichen deterministische Caps, Scope-Verfeinerung und explizite Completeness-Metadaten.

**Entscheidung:** keine zustandsbehafteten Cursor. MCP-`tools/list`-Pagination ist davon getrennt, bei 26 statischen Tools aber ebenfalls nicht erforderlich.

## 8. Kein RAG, keine Embeddings, kein Semantic Kernel

Vom Nutzer ausgeschlossen und für die beschlossenen Aufgaben nicht nötig. Roslyn-Symbolgraph, Git-Diff, Syntax-/SemanticModel, deterministische Test-Zuordnung und bestehende Linterdaten reichen aus.

## 9. Keine mutierenden Refactoring-Tools

Rename/Extract/Apply-Fix verändern fremde Codebasen und benötigen Transaktions-, Preview-, Konflikt- und Host-Approval-Verträge. Der Server ist heute ein Analysewerkzeug. Die bestehenden Tasks dokumentieren diese Grenze bereits.

**Entscheidung:** read-only Analyse beibehalten; Agent/IDE führt Edits aus.

## 10. Kein sprachübergreifender semantischer Graph in dieser Initiative

AiNetLinter ist als allgemeines C#-Tool positioniert. Roslyn kann Razor-, XAML-, JavaScript- oder SQL-Semantik nicht vollständig und typsicher abbilden. `search_pattern` bleibt der dokumentierte Textfallback.

**Entscheidung:** keine Parser-/Framework-Ausweitung unter dem Etikett der MCP-Tokenoptimierung.

## Definition of Done

- Die Entscheidungen sind aus `00_uebersicht-und-entscheidungen.md` verlinkt.
- Neue Roadmap-/Taskvorschläge widersprechen diesen Entscheidungen nicht ohne neue dokumentierte Evidenz.
- Es wird keine verworfene Idee allein mit „könnte besser sein“ wieder geöffnet.
