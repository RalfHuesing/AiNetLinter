---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions:
  - Soll dieser Task als erster MCP-Release freigegeben werden?
depends_on:
  - tasks/ainetlinter-mcp-usage-audit/Konzept.md
  - tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md
  - tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md
related_tasks:
  - tasks/02-mcp-development-workflow
  - tasks/03-mcp-unified-analysis-target
supersedes: null
---

# Task 01: MCP-Agenten-Handoff

## Ziel und Problem

Ein Coding-Agent soll aus einem ersten Discovery-Call zuverlässig zur
passenden C#- oder Textsuche gelangen und eine gefundene Definition ohne
manuelles ID-Raten an den nächsten MCP-Call übergeben können. Die Antwort
muss außerdem ehrlich sagen, ob sie leer, vollständig oder begrenzt ist.

Die derzeitigen Probleme liegen in drei eng verbundenen Ketten:

`file_tree → index_scope → search_pattern`

`find_symbol → structure/body/references/call_tree`

`Ergebnis → Completeness/Drilldown/Folgecall`

Discovery, Identifier und Completeness werden deshalb bewusst in einem Task
geliefert. Eine getrennte Veröffentlichung würde einen scheinbar verbesserten
ersten Call liefern, dessen Folgeaktion weiterhin unsicher bleibt.

## Source of Truth und betroffene Bereiche

- Audit-Evidenz: `tasks/ainetlinter-mcp-usage-audit/`
- Befundmatrix und gemeinsame Regeln:
  `tasks/ainetlinter-mcp-usage-audit/shared/`
- Discovery: `McpServerInstructions`, `Registration/*`,
  `Tools/FileStructure/*`, `FileTreePathResolver`, `PathGlobMatcher`,
  `SearchPatternScanner`
- Symbolidentität und Navigation: `AnalysisSymbolIdentity`,
  `SymbolIdentifierResolver`, `Tools/SymbolGraph/*`, `GetSymbolBodyTool`,
  `GetFileSkeletonTool`, `GetClassStructureTool`, `TypeHierarchy/*`
- Antwortverträge: `McpToolResults`, `McpTruncation`,
  `McpSufficiencyHints`, gemeinsame Response-Envelope und
  `AssemblyAnalysisResponseLimits`, soweit der bestehende Assembly-Happy-Path
  regressionsgesichert werden muss
- Tests: Discovery-/Framing-, Symbolgraph-, Raw-Wire- und MCP-Contract-Tests

Der öffentliche Target-Schnitt bleibt bis Task 03 unverändert. Dieser Task
ändert nicht die Unified-Target-Entscheidung und entwickelt den Decompiler
nicht neu.

## Scope

### Discovery Contract

- echter Verzeichnisbaum ohne Präfix-/Parent-Fehler;
- konsistente Pfad- und Glob-Semantik zwischen `get_file_tree` und
  `search_pattern`;
- `get_index_scope` mit sichtbarem Routing-Hint für C# gegenüber Nicht-C#;
- Tool-Katalog, Einzelschema, Required-Felder, Enums und Capability-Matrix aus
  einem ausführbaren Contract-Katalog;
- sichere Root-Defaults ohne Token-Flut, Lograuschen oder Release-Duplikate;
- `resource-overview` als korrekter Kontext-/Statushinweis statt bloßer
  Pfadbestätigung.

### Symbol IDs und Navigation

- gemeinsame kanonische Identifier für Source und bestehende Assemblypfade;
- eindeutige Regeln für `T:`, `M:`, `P:`, `F:`, Datei-/Zeilen-Fallbacks und
  Partial-Typen;
- IDs in Tabellen, Call-Sites, Implementierungen und Graphkanten sichtbar;
- keine stillen Array-/Alias-Verluste;
- verständliche Target-, Stale- und Ambiguous-Fehler mit kopierbarem nächsten
  Schritt;
- `find_symbol` → Kontext, Body, Referenzen und Call-Tree als echte
  Folge-Call-Tests;
- Memberzeilen ohne gültige ID werden nicht als Agenten-Handoff behauptet;
- Partial-Klassen erhalten eine gemeinsame fachliche Symbolidentität.

### Completeness und bounded Exploration

- gemeinsamer Envelope mit `totalCount`, `returnedCount`, `completeness`,
  `truncatedBy` und optionalem `continuationToken`;
- keine `complete`-Aussage bei sichtbarer oder fachlicher Kappung;
- getrennte Zählung konkurrierender Richtungen und Composite-Abschnitte;
- sichtbare Budgets für Markdown und `StructuredContent`;
- kleine, fachlich gerankte Defaults und gezielte Drilldowns;
- Scope-/Filter-Hinweis bei nicht sinnvoll rankbaren Massenmengen;
- Continuation nur für eine sinnvoll geordnete Restmenge, kein klassischer
  Seitenbrowser;
- gleiche Datenbasis für Text und `StructuredContent`;
- normale Leermengen bleiben von `unsupported`, `partial` und Fehlern getrennt.

Die erste Umsetzung konzentriert den gemeinsamen Vertrag auf die meistgenutzte
Discovery-/Symbol-/Strukturkette. Nicht jeder Spezialfall erhält automatisch
einen neuen Seitenbrowser.

## Gemeinsame Architektur

Es wird ein kleiner typisierter Contract-Kern für Status, Scope/Freshness,
Completeness, Capability und Projektion eingeführt. Fachliche Scanner und
Roslyn-Resolver bleiben nahe an ihren Tools. Es gibt keinen Mega-Envelope mit
identischen Nutzfeldern für alle Tools, keine neue DI-Schicht und keine
gemeinsame Session-Abstraktion für Project- und Assembly-Lifecycle.

Der Contract-Katalog prüft drei Ebenen:

1. Input-/Discovery-Vertrag: Name, Pflichtfelder, Aliase, Capability und
   tatsächliches SDK-InputSchema;
2. Output-Struktur: Payload, Status, IDs, Scope/Freshness und Completeness;
3. Verhalten: erlaubte Aussagen bei leer, vollständig, partial, truncated,
   unsupported und not_decidable sowie die echte Verwendbarkeit von IDs.

## Muss-Kriterien

- Der Discovery-Pfad liefert korrekte Eltern und direkt verwendbare Scopes.
- `.cs` wird nachvollziehbar an Symboltools, `.js`/`.razor` an Textsuche
  geroutet.
- Ein Agent kann Pflichtfelder und Target-Capability aus dem Einzelschema
  ableiten.
- Eine ausgegebene Symbol-ID funktioniert im vorgesehenen Folge-Tool.
- Falsches Target, stale ID und Ambiguous ID sind unterscheidbar.
- Jede begrenzte Antwort erklärt Ursache und nächste Aktion maschinen- und
  menschenlesbar.
- Text und `StructuredContent` widersprechen sich nicht.
- Der bestehende eindeutige `get_symbol_body`-Happy-Path bleibt erhalten.

## Non-Goals

- kein neuer öffentlicher Unified-Target-Vertrag;
- keine Entfernung von `targetType` oder `ainetlinter.project.json`;
- keine neue Assembly-/Decompiler-Funktionalität;
- keine vollständige Neuentwicklung aller Heuristiken;
- keine pauschale Ausweitung von `maxResults` und kein Seitenbrowser;
- kein globaler Formatter-, DI- oder Registrierungsumbau ohne Vertragsnutzen.

## Risiken und Alternativen

Ein gemeinsamer Envelope kann zu optionalen Feldern ohne klare Bedeutung
verkommen. Deshalb werden nur Status-/Completeness-/Capability-Metadaten
vereinheitlicht; tool-spezifische Nutzdaten bleiben typisiert.

Eine Umsetzung nur für `find_symbol` wäre kleiner, würde aber die Discovery-
und Folge-Call-Kette nicht stabilisieren. Die Bündelung ist deshalb trotz
mehrerer Quellbereiche fachlich ein Task.

## Verifikation und Dokumentation

- schnelle Unit-/Component-Tests während der Entwicklung;
- Contract- und Raw-Wire-Tests für die betroffenen Toolfamilien;
- repräsentative Folge-Call-Tests mit kopierten IDs;
- Discovery-/Fallback-Proben für C#, JavaScript und Razor;
- Root-, Glob-, Truncation-, Empty- und Capability-Proben;
- `Docs/agent-api.md`, `Docs/integration.md` und relevante MCP-Instructions
  synchronisieren;
- vor Abschluss `dotnet build` sowie beide vollständigen Nicht-Stress-
  Testläufe gemäß `AGENTS.md`;
- fokussierter MCP-Live-Nachweis und Schutz der positiven Baseline aus dem
  gemeinsamen Rahmen.

## Abnahme / Release-Gate

Der Task ist releasefähig, wenn ein Agent von Discovery über Symbolfund bis
zum Body-/Referenz-/Call-Tree-Folgecall navigieren kann, ohne IDs oder
Vollständigkeit zu erraten. Ein rotes Contract- oder Folge-Call-Gate stoppt
die Veröffentlichung und den Start von Task 02.
