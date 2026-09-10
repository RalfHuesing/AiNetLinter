# AiNetLinter MCP-UX-Audit: Gesamtübersicht & Status quo (10.09.2026)

## 1. Executive Summary: Wo stehen wir?

Nach der intensiven Umsetzung der architektonischen Grundlagen in den vorangegangenen Aufgaben:
- **Task 01**: Unified Analysis Target (deterministische Bindung an `.sln`/`.slnx` bzw. `.dll`/`.exe`)
- **Task 02**: Agent Handoff (`structuredContent`, maschinenlesbare `navigation`-Blöcke, opake stabile IDs)
- **Task 03**: Development Workflow (Konsolidierung der CLI-/MCP-Verträge, Legacy-Bereinigung)
- **Task 04**: Verlässliche Assembly-Analyse (Metadata, Decompilation-Cache, Snapshot-Invarianz, bounded Reference-Sessions)

befindet sich der **AiNetLinter MCP-Server auf einem sehr hohen architektonischen und funktionalen Reifegrad**.

### Was bereits exzellent funktioniert:
1. **Target-Bindung & Idempotenz**: Alle 33 zielgebundenen Tools erzwingen deterministisch genau einen absoluten `targetPath`. Ein versehentliches Schweifen über das Repository oder Workspace-Verunreinigung ist ausgeschlossen ([AnalysisTargetResolver.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/AnalysisTargetResolver.cs)).
2. **Schema- & Typ-Sicherheit**: Der [McpArgumentValidationFilter.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/McpArgumentValidationFilter.cs) fängt Typ-Mismatches (z. B. String statt Array), fehlende Pflichtfelder und Grenzwerte (`maxResults <= 0`) sauber vor der Tool-Ausführung ab und liefert maschinenlesbare `INVALID_ARGUMENT`-Fehler mit JSON-Feldpfaden (`$.targetPath`, `$.maxResults`).
3. **Unknown Argument Guard**: Unbekannte Argumente werden durch [TargetPathToolRegistrationOptions.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/TargetPathToolRegistrationOptions.cs#L66-L97) sofort abgewiesen (`Unbekanntes Argument: <name>`, `fieldPath: $.<name>`), was Phantom-Parameter verhindert.
4. **Symbol-Chaining**: Die Verkettung `find_symbol` -> opake Symbol-ID -> `get_symbol_body` funktioniert sowohl im Source-Modus (`.slnx`) als auch im Assembly-Modus (`.dll`/`.exe`) auf echten Dekompilaten nahtlos und stabil.
5. **Assembly-Analyse**: Bei lokalen Assemblies (`LOCAL-01`, `LOCAL-02`, `LOCAL-03`) arbeiten Metadata-Inspektion, Decompilation, Typ-/Member-Listen, Paging (`continuationToken` in `search_assembly`) und Fehlerbehandlung bei unmanaged Executables (`FALSE-01` -> `INVALID_ASSEMBLY`) absolut verlässlich.

---

## 2. Reifegrad-Matrix (33 Tools & 3 Resources)

| Kategorie / Tool | Source (`.sln`/`.slnx`) | Assembly (`.dll`/`.exe`) | Reifegrad | Bemerkung |
|---|---|---|---|---|
| **Health & Meta** | | | | |
| `get_server_health` | Unterstützt | Unterstützt | **Sehr gut** (Minor UX) | `PROJECT_NOT_INITIALIZED` bei frühem Source-Call |
| `report_observability_feedback` | Ungebunden | Ungebunden | **Gut** (Minor Schema) | FeedbackType-Validierung weicht von Description ab |
| Resource `ainetlinter://agent-guide` | Ungebunden | Ungebunden | **Exzellent** | Statischer Einstiegs-Guide |
| Resource `ainetlinter://overview` | Unterstützt | Unsupported | **Gut** | URL-kodierter TargetPath |
| Resource `ainetlinter://rules` | Unterstützt | Unsupported | **Gut** | Zeigt Linter-Konfiguration |
| **Discovery & Scope** | | | | |
| `get_file_tree` | Unterstützt | Unterstützt | **Sehr gut** (Minor UX) | Text vs. Navigation `next`-Inkonsistenz |
| `get_namespace_tree` | Unterstützt | Unterstützt | **Exzellent** | Schnelle hierarchische Typübersicht |
| `get_index_scope` | Unterstützt | Unsupported | **Exzellent** | Liefert Routing-Empfehlungen für Non-C# |
| `inspect_assembly` | Unsupported | Unterstützt | **Exzellent** | Detaillierte API-, Referenz- und Typanalyse |
| `find_assembly_extensions` | Unsupported | Unterstützt | **Sehr gut** | Extension-Methoden-Erkennung |
| `search_assembly` | Unsupported | Unterstützt | **Exzellent** | Schnell, mit stabilen Paging-Tokens |
| `get_assembly_context` | Unsupported | Unterstützt | **Gut** (Minor) | Composite-Assembly-Einstieg |
| **Symbol Graph & Code Exploration** | | | | |
| `find_symbol` | Unterstützt | Unterstützt | **Exzellent** | Liefert opake IDs mit `handoff=true` |
| `get_symbol_body` | Unterstützt | Unterstützt | **Exzellent** | Source & Decompilat mit Windowing |
| `get_class_structure` | Unterstützt | Unterstützt | **Exzellent** | Tabellarische Member-Signaturen |
| `get_file_skeleton` | Unterstützt | Unterstützt | **Exzellent** | Signaturen mit IDs für direkte Folgeschritte |
| `find_references` | Unterstützt | Unterstützt | **Sehr gut** | Bounded im Assembly-Modus |
| `get_call_tree` | Unterstützt | Unterstützt | **Exzellent** | Schneller Aufrufbaum mit Handoff-IDs |
| `get_impact` | Unterstützt | Unterstützt | **Befriedigend** (Major) | Bei Einzelsymbol faktisch redundant zu `find_references` |
| `get_type_hierarchy` | Unterstützt | Unterstützt | **Exzellent** | Basisklassen, Interfaces, abgeleitete Klassen |
| `find_implementations` | Unterstützt | Unterstützt | **Exzellent** | Findet alle Overrides/Implementierungen |
| `resolve_type_origin` | Unterstützt | Unterstützt | **Gut** (Minor UX) | Nennt Decompilat "Projekt-Quellcode" im Text |
| `dependency_graph` | Unterstützt | Unterstützt | **Sehr gut** | Ausgehend und eingehend |
| **Qualität, Linting & Metriken** | | | | |
| `get_violations` | Unterstützt | Unsupported | **Major UX-Befund** | 0 Violations führt zu `available=false`/`empty` |
| `safeguard` | Unterstützt | Unsupported | **Exzellent** | 0-10 Quality Gate Score |
| `pattern_detect` | Unterstützt | Unsupported | **Major UX-Befund** | 1 inaktive Regel macht Gesamtstatus `not_configured` |
| `find_dead_code` | Unterstützt | Unsupported | **Exzellent** | Klare Heuristik-Grenzen & `ask_user` |
| `find_magic_values` | Unterstützt | Unsupported | **Exzellent** | Strukturierte Kategorien mit Handlungsanweisungen |
| `find_duplicates` | Unterstützt | Unsupported | **Sehr gut** | Token- und AST-basierte Duplikatsuche |
| `metrics_tree` | Unterstützt | Unterstützt | **Exzellent** | Traversierung von LOC/Größe |
| `metrics_lookup` | Unterstützt | Unterstützt | **Exzellent** | LOC, Footprint, Grenzwerte |
| `get_hotspots` | Unterstützt | Unsupported | **Sehr gut** | Kritische Dateien nahe Zeilengrenzen |
| `get_feature_context` | Unterstützt | Unsupported | **Major UX-Befund** | Text-Budget Duplikation & leere Declaration |
| `get_test_context` | Unterstützt | Unsupported | **Exzellent** | Generiert sofort ausführbare `dotnet test` Filter |
| `search_pattern` | Unterstützt | Unsupported | **Sehr gut** (Minor UX) | Regex/Text-Fallback für Non-C# |
| `reload_config` | Unterstützt | Unsupported | **Exzellent** | Heißes Nachladen der Rules-JSON |

---

## 3. Klassifizierung der identifizierten Befunde

Die Detailbefunde sind in separaten thematischen Berichten dokumentiert:

| ID | Schweregrad | Kategorie | Kurztitel | Detaildatei |
|---|---|---|---|---|
| **F-01** | **Major** | Agent-UX / Navigation | `get_violations` projiziert 0 Verstöße als `available=false`, `completeness=empty` und `next: refine_scope` | `05-befunde-gruppe-e...md` |
| **F-02** | **Major** | Agent-UX / Navigation | `pattern_detect` schaltet bei einer einzigen inaktiven Unterregel die Gesamtausgabe auf `not_configured` (`available=false`) | `05-befunde-gruppe-e...md` |
| **F-03** | **Major** | SNR / Output | `get_feature_context` wiederholt Trimming-Text mehrfach ("Wire-Budget erreicht...") und liefert leere Declaration-Counts | `03-befunde-gruppe-c...md` |
| **F-04** | **Major** | Tool-Semantik | `get_impact` liefert bei Einzelsymbolen exakt identischen Output wie `find_references` (keine Risiko-/Testauswertung) | `03-befunde-gruppe-c...md` |
| **F-05** | **Major** | Lifecycle / Session | `get_server_health` wirft `PROJECT_NOT_INITIALIZED` bei frischem Source-Target, während Assembly-Targets auto-geleast werden | `01-befunde-gruppe-a...md` |
| **F-06** | **Minor** | Cross-Tool-Konsistenz | Text-Output `[NEXT: refine_scope]` widerspricht maschinenlesbarem Navigation-Feld `next: request_detail` in mehreren Tools | `05-befunde-gruppe-e...md` |
| **F-07** | **Minor** | Schema vs. Validation | `report_observability_feedback`: Schema-Description nennt andere `feedbackType`-Werte als die Validierungs-Fehlermeldung | `01-befunde-gruppe-a...md` |
| **F-08** | **Minor** | Error-Codes | Asymmetrie bei Cross-Target-Fehlern: Assembly auf SourceTool liefert `ASSEMBLY_TARGET_UNSUPPORTED`, Source auf AssemblyTool liefert `INVALID_ARGUMENT` | `04-befunde-gruppe-d...md` |
| **F-09** | **Minor** | Error-Navigation | `FALSE-01` (unmanaged EXE) meldet `next: request_detail`, obwohl die Fehlermeldung ausdrücklich "Keine Wiederholung nötig" sagt | `04-befunde-gruppe-d...md` |
| **F-10** | **Minor** | Text-Präzision | `resolve_type_origin` deklariert den Typ aus einer dekompilierten Assembly im Markdown-Text als "Projekt-Quellcode" | `05-befunde-gruppe-e...md` |
| **F-11** | **Minor** | Output-Vollständigkeit | `get_assembly_context` erwähnt im Text den `continuationToken`, druckt ihn aber im Markdown-Text nicht aus | `03-befunde-gruppe-c...md` |

---

## 4. Fazit & Priorisierte Handlungsempfehlung für den nächsten Sprint

1. **Priorität 1 (Agent-Decisioning-Fixes)**:
   - **F-01**: `get_violations` muss bei 0 Violations `available=true`, `completeness=complete`, `next=none` zurückgeben. 0 Fehler ist ein Erfolg, kein "empty search miss"!
   - **F-02**: `pattern_detect` darf den Gesamtstatus nicht auf `not_configured` kippen, wenn nur Teilpatterns nicht konfiguriert sind.
2. **Priorität 2 (SNR & Text-Formatierung)**:
   - **F-03**: Duplikate im Wire-Budget-Trimmer bei Composite-Antworten unterbinden.
   - **F-06**: Vereinheitlichung der `next`-Aktionen zwischen Text-Hinweisen und Navigation-DTO.
3. **Priorität 3 (Architektur & Konsistenz)**:
   - **F-05**: `get_server_health` mit `targetPath` sollte auch für Source-Projekte transparent ein initiales Laden durchführen (oder als Status `not_loaded` statt Exception-Fehler zurückgeben), analog zu Assembly-Leases.
