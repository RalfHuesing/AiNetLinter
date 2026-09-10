# AiNetLinter MCP-UX-Audit: Gesamtübersicht & Status quo (10.09.2026)

## 1. Executive Summary: Wo stehen wir?

Nach der intensiven Umsetzung der architektonischen Grundlagen in den vorangegangenen Aufgaben:
- **Task 01**: Unified Analysis Target (deterministische Bindung an `.sln`/`.slnx` bzw. `.dll`/`.exe`)
- **Task 02**: Agent Handoff (`structuredContent`, maschinenlesbare `navigation`-Blöcke, opake stabile IDs)
- **Task 03**: Development Workflow (Konsolidierung der CLI-/MCP-Verträge, Legacy-Bereinigung)
- **Task 04**: Verlässliche Assembly-Analyse (Metadata, Decompilation-Cache, Snapshot-Invarianz, bounded Reference-Sessions)
- **UX-Audit Fix-Pakete 1 & 2**: Behebung aller 10 identifizierten Audit-Befunde (F-01 bis F-10)

befindet sich der **AiNetLinter MCP-Server auf einem exzellenten architektonischen und funktionalen Reifegrad ohne offene Befunde**.

### Was exzellent funktioniert:
1. **Target-Bindung & Idempotenz**: Alle 33 zielgebundenen Tools erzwingen deterministisch genau einen absoluten `targetPath`. Ein versehentliches Schweifen über das Repository oder Workspace-Verunreinigung ist ausgeschlossen ([AnalysisTargetResolver.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/AnalysisTargetResolver.cs)).
2. **Schema- & Typ-Sicherheit**: Der [McpArgumentValidationFilter.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/McpArgumentValidationFilter.cs) fängt Typ-Mismatches (z. B. String statt Array), fehlende Pflichtfelder und Grenzwerte (`maxResults <= 0`) sauber vor der Tool-Ausführung ab und liefert maschinenlesbare `INVALID_ARGUMENT`-Fehler mit JSON-Feldpfaden (`$.targetPath`, `$.maxResults`).
3. **Unknown Argument Guard**: Unbekannte Argumente werden durch [TargetPathToolRegistrationOptions.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/TargetPathToolRegistrationOptions.cs#L66-L97) sofort abgewiesen (`Unbekanntes Argument: <name>`, `fieldPath: $.<name>`), was Phantom-Parameter verhindert.
4. **Symbol-Chaining & Exploration**: Die Verkettung `find_symbol` -> opake Symbol-ID -> `get_symbol_body` funktioniert sowohl im Source-Modus (`.slnx`) als auch im Assembly-Modus (`.dll`/`.exe`) auf echten Dekompilaten nahtlos und stabil.
5. **Semantischer Mehrwert**: `get_impact` liefert im Einzelsymbol-Modus betroffene Downstream-Projekte und zugeordnete Testmethoden; `get_feature_context` liefert vollständige Typ-, Interface- und Member-Deklarationen.
6. **Health & Lifecycle**: `get_server_health` lädt noch nicht residente Source-Solutions transparent on-demand via Registry-/Runtime-Leasing, symmetrisch zum Assembly-Verhalten.
7. **Assembly-Analyse**: Bei lokalen Assemblies (`LOCAL-01`, `LOCAL-02`, `LOCAL-03`) arbeiten Metadata-Inspektion, Decompilation, Typ-/Member-Listen, Paging (`continuationToken` in `search_assembly`) und Fehlerbehandlung bei unmanaged Executables (`FALSE-01` -> `INVALID_ASSEMBLY`) absolut verlässlich.

---

## 2. Reifegrad-Matrix (33 Tools & 3 Resources)

| Kategorie / Tool | Source (`.sln`/`.slnx`) | Assembly (`.dll`/`.exe`) | Reifegrad | Bemerkung |
|---|---|---|---|---|
| **Health & Meta** | | | | |
| `get_server_health` | Unterstützt | Unterstützt | **Exzellent** | On-Demand Leasing bei ungeladenem Target |
| `report_observability_feedback` | Ungebunden | Ungebunden | **Exzellent** | Strikte FeedbackType-Validierung & synchrone Description |
| Resource `ainetlinter://agent-guide` | Ungebunden | Ungebunden | **Exzellent** | Statischer Einstiegs-Guide |
| Resource `ainetlinter://overview` | Unterstützt | Unsupported | **Gut** | URL-kodierter TargetPath |
| Resource `ainetlinter://rules` | Unterstützt | Unsupported | **Gut** | Zeigt Linter-Konfiguration |
| **Discovery & Scope** | | | | |
| `get_file_tree` | Unterstützt | Unterstützt | **Exzellent** | Synchrones refine_scope bei Truncation |
| `get_namespace_tree` | Unterstützt | Unterstützt | **Exzellent** | Schnelle hierarchische Typübersicht |
| `get_index_scope` | Unterstützt | Unsupported | **Exzellent** | Liefert Routing-Empfehlungen für Non-C# |
| `inspect_assembly` | Unsupported | Unterstützt | **Exzellent** | Detaillierte API-, Referenz- und Typanalyse |
| `find_assembly_extensions` | Unsupported | Unterstützt | **Sehr gut** | Extension-Methoden-Erkennung |
| `search_assembly` | Unsupported | Unterstützt | **Exzellent** | Schnell, mit stabilen Paging-Tokens |
| `get_assembly_context` | Unsupported | Unterstützt | **Exzellent** | Composite-Assembly-Einstieg mit Paging-Token |
| **Symbol Graph & Code Exploration** | | | | |
| `find_symbol` | Unterstützt | Unterstützt | **Exzellent** | Liefert opake IDs mit `handoff=true` |
| `get_symbol_body` | Unterstützt | Unterstützt | **Exzellent** | Source & Decompilat mit Windowing |
| `get_class_structure` | Unterstützt | Unterstützt | **Exzellent** | Tabellarische Member-Signaturen |
| `get_file_skeleton` | Unterstützt | Unterstützt | **Exzellent** | Signaturen mit IDs für direkte Folgeschritte |
| `find_references` | Unterstützt | Unterstützt | **Sehr gut** | Bounded im Assembly-Modus |
| `get_call_tree` | Unterstützt | Unterstützt | **Exzellent** | Schneller Aufrufbaum mit Handoff-IDs |
| `get_impact` | Unterstützt | Unterstützt | **Exzellent** | Semantische Downstream- und Testkandidaten-Anreicherung |
| `get_type_hierarchy` | Unterstützt | Unterstützt | **Exzellent** | Basisklassen, Interfaces, abgeleitete Klassen |
| `find_implementations` | Unterstützt | Unterstützt | **Exzellent** | Findet alle Overrides/Implementierungen |
| `resolve_type_origin` | Unterstützt | Unterstützt | **Exzellent** | Präzise Herkunftstrennung Quellcode vs. Dekompilierte Assembly |
| `dependency_graph` | Unterstützt | Unterstützt | **Sehr gut** | Ausgehend und eingehend |
| **Qualität, Linting & Metriken** | | | | |
| `get_violations` | Unterstützt | Unsupported | **Exzellent** | 0 Violations liefert `complete`, `available=true`, `next=none` |
| `safeguard` | Unterstützt | Unsupported | **Exzellent** | 0-10 Quality Gate Score |
| `pattern_detect` | Unterstützt | Unsupported | **Exzellent** | Partiell vollständige Aggregation mit available=true |
| `find_dead_code` | Unterstützt | Unsupported | **Exzellent** | Klare Heuristik-Grenzen & `ask_user` |
| `find_magic_values` | Unterstützt | Unsupported | **Exzellent** | Strukturierte Kategorien mit Handlungsanweisungen |
| `find_duplicates` | Unterstützt | Unsupported | **Sehr gut** | Token- und AST-basierte Duplikatsuche |
| `metrics_tree` | Unterstützt | Unterstützt | **Exzellent** | Traversierung von LOC/Größe |
| `metrics_lookup` | Unterstützt | Unterstützt | **Exzellent** | LOC, Footprint, Grenzwerte |
| `get_hotspots` | Unterstützt | Unsupported | **Sehr gut** | Kritische Dateien nahe Zeilengrenzen |
| `get_feature_context` | Unterstützt | Unsupported | **Exzellent** | Typ- und Member-Deklarationen, bereinigtes Composite-Wire-Budget |
| `get_test_context` | Unterstützt | Unsupported | **Exzellent** | Generiert sofort ausführbare `dotnet test` Filter |
| `search_pattern` | Unterstützt | Unsupported | **Sehr gut** | Regex/Text-Fallback für Non-C# |
| `reload_config` | Unterstützt | Unsupported | **Exzellent** | Heißes Nachladen der Rules-JSON |

---

## 3. Status aller Befunde

Alle identifizierten Befunde wurden behoben und durch automatisierte Tests verifiziert:

- **F-01**: Behoben (Navigation `result.available` bei 0 Violations).
- **F-02**: Behoben (`pattern_detect` liefert partiell aggregierte Ergebnisse mit `available=true`).
- **F-03**: Behoben (`get_feature_context` liefert vollständige Typ- und Memberdeklarationen sowie bereinigtes Wire-Budget).
- **F-04**: Behoben (`get_impact` liefert im Einzelsymbol-Modus betroffene Downstream-Projekte und zugeordnete Testmethoden).
- **F-05**: Behoben (`get_server_health` leaset ungeladene Source-Targets on-demand).
- **F-06**: Behoben (`get_file_tree` Navigation-Sync).
- **F-07**: Behoben (`get_file_tree` Root-Validation).
- **F-08**: Behoben (`PROJECT_TARGET_UNSUPPORTED` für projekt-exklusive Tools).
- **F-09**: Behoben (`report_observability_feedback` Type-Validierung).
- **F-10**: Behoben (`search_pattern` Konsistenz).

Aktuell liegen **keine offenen Befunde** vor.
