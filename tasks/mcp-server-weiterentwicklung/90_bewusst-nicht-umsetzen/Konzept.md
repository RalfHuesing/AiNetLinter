---
status: festlegung
type: entscheidungsregister
project_kind: brownfield
estimated_scope: small
priority: P9
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
herkunft: "Konsolidierung 2026-08-21 aus mcp-agenten-effizienz/90, features/06 und features/05"
---

# Bewusst nicht umsetzen: konsolidiertes Entscheidungsregister

## Ziel

Dieses Register hält verworfene und zurückgestellte Optionen mit ihren überprüfbaren
Gründen fest. Es verhindert, dass ein späterer Agent dieselben Ideen ohne neue Evidenz als
vermeintliche Optimierung implementiert. Es ersetzt `tasks/mcp-agenten-effizienz/90_...`
und `tasks/features/06-nicht-umsetzen.md` sowie die Zurückstellung aus
`tasks/features/05-bedingt-sinnvoll.md`.

## Entscheidungsregel

Eine verworfene Idee darf nur wieder geöffnet werden, wenn mindestens eine neue Bedingung
erfüllt ist:

- reproduzierbare Nutzungs-/Hostdaten zeigen ein konkretes Problem (Basis: Aufgabe 01,
  `--analyze-mcp-log`),
- der MCP-/SDK-Vertrag ändert die technischen Voraussetzungen,
- ein neutraler C#-Benchmark zeigt einen Qualitäts- oder Größengewinn,
- der Nutzer erweitert den erlaubten Tech-Stack ausdrücklich.

---

## A. Verworfene Tool-Neuentwürfe

### A.1 Kein Natural-Language-Tool `locate_task`
Ohne Embeddings/RAG kann eine freie Aufgabenbeschreibung nur lexikalisch gematcht werden.
`find_symbol`, `search_pattern`, `get_namespace_tree`, `metrics_tree` und
`get_feature_context` decken dies deterministisch ab. Ein neues Tool würde denselben
Bestand mit schwer belegbarer Ranking-Heuristik erneut exponieren.
**Entscheidung:** nicht implementieren.

### A.2 Kein neues `get_change_context`
Ein weiteres Composite-Tool erhöht Toolkatalog und Auswahlraum. Git-Diff-Impact liegt
bereits in `get_impact`; Aufgabe 03 erweitert genau dieses Tool additiv.
**Entscheidung:** bestehendes Tool erweitern, keinen Alias und kein zweites Schema.

### A.3 Kein `validate_file` / `get_diagnostics` (Post-Edit-Validierung)
1. Redundant: Coding-Agenten führen standardmäßig `dotnet build`/`dotnet test` aus;
   Compiler-Fehler werden von Roslyn/MSBuild schnell, vollständig und präzise ausgegeben.
2. Bereits abgedeckt: Linter-/Architekturverstöße liefern `get_violations` und `safeguard`;
   Workspace-Diagnose erscheint als Warnhinweise in Tool-Antworten.
3. Tool-Sprawl: Ein zusätzlicher Wrapper für In-Memory-Kompilierung auf Dateiebene erhöht
   Komplexität ohne Mehrwert.
**Entscheidung:** keinen neuen Diagnostics-Endpunkt.

### A.4 Kein `trace_flow` (Multi-Symbol-Flow-Tracer)
CodeGraph benötigt `codegraph_explore` als Alleinstellung, weil es keine guten Einzeltools
hat. AiNetLinter läuft als Ergänzung zu Host-Agenten, die mit `find_references`,
`get_call_tree`, `dependency_graph` und `get_symbol_body` gezielter und token-effizienter
navigieren. Hoher Heuristik- und Wartungsaufwand bei Reflection/Dynamic Dispatch ohne
deterministische Garantien.
**Entscheidung:** nicht implementieren.

### A.5 Kein `get_fixes` / MCP-Auto-Fix-Generator
`get_violations` und `safeguard` liefern klare Hinweise; LLMs korrigieren Code direkt in
der Datei schneller und kontextbezogener als ein statischer Fix-Generator.
**Entscheidung:** nicht implementieren.

### A.6 Keine Git-History-/Blame-MCP-Tools
Host-Agenten haben direkten Terminalzugriff auf Git. `get_impact` liefert bereits den
spezifischen Mehrwert (Git-Diff kombiniert mit Roslyn-Blast-Radius).
**Entscheidung:** nicht implementieren.

### A.7 Keine ASP.NET-Framework-Analyzer-Suite (zurückgestellt, aus features/05)
6 hochspezifische Analyzer (Routes, Minimal API, Middleware-Reihenfolge, DI-Zyklen, gRPC,
Route-Konflikte) plus 2 MCP-Tools. Starkes Alleinstellungsmerkmal für reine
ASP.NET-Web-APIs, aber Nischen-Bedarf und hoher Aufwand (~2 Wochen).
**Entscheidung:** nur priorisieren, wenn ASP.NET-Projekte in den Produktfokus rücken;
bis dahin zurückgestellt, nicht verworfen. Wiedervorlage mit konkretem Anwendungsfall.

---

## B. Tool-Bestand: keine Entfernung, keine Profile

### B.1 Keine sofortige Abschaffung oder Zusammenlegung bestehender Tools
Für eine Entfernung von `pattern_detect`, `safeguard`, `metrics_tree`, `get_hotspots`
oder anderen liegen keine Nutzungsdaten vor. Eine Entfernung wäre ein Breaking Change.
**Entscheidung:** keine Removal-Aktion. Erst Nutzungsdaten über Aufgabe 01
(`--analyze-mcp-log`) sammeln, danach Deprecation mit mindestens einer Release-Übergangsphase.

### B.2 Keine Toolprofile (`core`/`analysis`/`full`)
Technisch möglich, aber nur hilfreich, wenn ein Host alle Schemas tatsächlich in den
Modellkontext legt. Erzeugen Konfigurationsvarianten, Dokumentationsaufwand und Fälle mit
fehlenden Tools. **Entscheidung:** erst bei Messungen in mindestens zwei unterstützten
Hosts neu bewerten.

## C. Protokoll- und Infrastruktur-Entscheidungen

### C.1 Kein pauschaler Output-Schema-Rollout
SDK 2.2.0 unterstützt `UseStructuredContent` und explizites `OutputSchema`. Ein Rollout
vergrößert jedoch garantiert `tools/list`; ein Qualitäts- oder Token-Nettonutzen ist nicht
gemessen. Zudem sind Antwortformen einzelner Tools noch inkonsistent (transitive Graphen).
**Entscheidung:** höchstens ein messender Pilot für ein Composite-Tool; kein flächiger Rollout.

### C.2 Keine Cursorpagination für Analyseergebnisse
Analysen sind an Solution-Snapshot, Git-Diff und Konfiguration gebunden; Cursor müssten
Snapshotidentität korrekt behandeln oder liefern inkonsistente Seiten. Deterministische
Caps, Scope-Verfeinerung und Completeness-Metadaten reichen. `tools/list`-Pagination ist
bei 26 statischen Tools ebenfalls nicht erforderlich.
**Entscheidung:** keine zustandsbehafteten Cursor.

### C.3 Kein RAG, keine Embeddings, kein Semantic Kernel, kein Vektorspeicher
Positionierungsbruch: AiNetLinter steht für deterministische, Roslyn-präzise Analyse ohne
externe Modell-/Cloud-Abhängigkeiten. Ein Vektorindex müsste zusätzlich zum Staleness-Check
bei jedem Edit synchronisiert werden. Relevante Symbole sind über `find_symbol`,
`search_pattern`, `metrics_tree` deterministisch auffindbar.
**Entscheidung:** vom Nutzer ausgeschlossen; nur mit expliziter Stack-Erweiterung neu bewerten.

### C.4 Keine PageRank-Repo-Map (`skeleton` im Aider-Stil)
Aider benötigt PageRank für heuristisches Tree-Sitter-Parsing; Roslyn liefert exakte
semantische Auflösung. `metrics_tree`, `get_index_scope` und `get_hotspots` decken den
Orientierungsbedarf ab.
**Entscheidung:** nicht implementieren.

### C.5 Kein Multi-Agent-Installer & kein Detached-Daemon
Unnötige Komplexität: `ainetlinter --mcp-server` (stdio) reicht für alle gängigen Clients;
der residente Workspace mit Staleness-Check benötigt keine Daemon-Infrastruktur.
**Entscheidung:** nicht implementieren.

### C.6 Keine Cloud-/Enterprise-Features (Multi-Tenancy, OAuth, OTel-Export, Cluster)
AiNetLinter ist ein schlankes lokales Entwickler- und Agenten-Tool, kein Cloud-Service.
**Entscheidung:** nicht implementieren.

---

## D. Server-Rolle und Sprachumfang

### D.1 Keine mutierenden Refactoring-Tools (`preview_refactor` / `apply_refactor`)
Architektur-Verletzung: Der MCP-Server ist strikt **read-only** und dient als
deterministisches Quality-Gate/Verifikations-Layer. Coding-Agenten führen Edits und
Diff-Prüfungen nativ über ihre Editor-Tools aus; Mutation über MCP erhöht Fehlerrisiko und
Komplexität drastisch (Transaktions-, Preview-, Konflikt-, Approval-Verträge).
**Entscheidung:** read-only Analyse beibehalten; Agent/IDE führt Edits aus.

### D.2 Kein sprachübergreifender semantischer Graph
AiNetLinter ist als allgemeines C#-Tool positioniert. Roslyn kann Razor-, XAML-,
JavaScript- oder SQL-Semantik nicht vollständig und typsicher abbilden. `search_pattern`
bleibt der dokumentierte Textfallback.
**Entscheidung:** keine Parser-/Framework-Ausweitung unter dem Etikett der
MCP-Tokenoptimierung.

### D.3 MCP Prompts Primitive — zurückgestellt (aus Aufgabe 08, Teil 2)
Host-Support heterogen; Workflows sind bereits über Instructions und Overview transportiert.
**Bedingung zur Wiederöffnung:** Call-Log-Analyse (Aufgabe 01) zeigt, dass Agenten die
empfohlenen Ketten nicht von selbst laufen, oder ein konkreter Ziel-Host unterstützt Prompts.

### D.4 Multi-Solution-Unterstützung — EPIC-A umgesetzt (aus Aufgabe 08, Teil 4)
Die frühere Annahme „eine Solution pro Prozess“ wurde wegen der belegten
Host-Realität wieder geöffnet. EPIC-A implementiert die transportneutrale
Registry für mehrere Projekte: Jeder Tool- und Overview-Resource-Aufruf wird
über den absoluten `projectRoot` an einen Registry-Key gebunden; die
Definitionsdatei `ainetlinter.project.json` liefert `solution` und `rules`.
Die eigene Repo-/Hermes-Registrierung und der URL-kodierte Overview-Resource-
Read sind in `11_epic-projektregistry-und-daemon/step-008` read-only bzw. live
nachgewiesen. Der erreichte Host akzeptierte das Query-Template, daher wurde
kein Resource→Tool-Rückfall eingeführt.

**Entscheidung:** Multi-Solution-Routing bleibt als transportneutrale
Registry-Fachlichkeit umgesetzt. Transport-, Thin-Client- und Daemon-
Lebenszyklusfragen sind nicht Bestandteil dieses Vermerks.

---

## Definition of Done

- Die Entscheidungen sind aus `00_uebersicht-und-entscheidungen.md` erreichbar.
- Neue Roadmap-/Taskvorschläge widersprechen diesen Entscheidungen nicht ohne neue
  dokumentierte Evidenz.
- Es wird keine verworfene Idee allein mit „könnte besser sein" wieder geöffnet.
