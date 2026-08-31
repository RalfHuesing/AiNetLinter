# Audit-Konzept: AiNetLinter MCP-Server (Agentische Nutzersicht & Live-Test)

## 1. Ziel und Scope

Dieser Audit testet und bewertet den **AiNetLinter MCP-Server** (v1.0.157+) systematisch aus der Perspektive eines **autonomen KI-Coding-Agenten**.
Im Fokus stehen:
1. **Funktionale Korrektheit & Robustheit**: Verhalten aller 29+ MCP-Tools bei typischen und grenzwertigen Abfragen (Source-Project und Decompiled-Assembly).
2. **Agentic Developer Experience (DX)**: Klarheit, Eindeutigkeit und semantische Aussagekraft der Antworten (Text & Structured Content).
3. **Token-Effizienz & Kontext-Ökonomie**: Vermeidung von Token-Bloat, unnötigen Payload-Größen, exzessiven Diagnose-Listen und Trunkierungsstrategien.
4. **Fehler- und Protokoll-Verträge**: Einhaltung der `isError`-Policy (`isError=true` nur bei echten Systemfehlern/Malfunction vs. `isError=false` bei recoverablen Nutzungsfehlern), Vorhandensein handlungsleitender Fehlermeldungen und Korrekturhilfen.
5. **Dokumentation & Regeltreue**: Synchronisation zwischen Server-Schemas, Agenten-Instruktionen (`instructions.md`), MCP-Workflow-Regeln (`.agents/rules/AiNetLinter-McpWorkflow.mdc`), Richtlinien (`AiNetLinterRichtlinien.mdc`) und eingebetteten Ressourcen (`ainetlinter://agent-guide`, `ainetlinter://overview`, `ainetlinter://rules`).

> [!NOTE]
> Alle in Tests verwendeten Third-Party- und Decompiled-DLLs werden in sämtlichen Protokollen, Reports und Commit-Nachrichten strikt anonymisiert (z. B. `Vendor.Pps.RealTimeData.dll`, `Vendor.Rewe.Buchungserfassung.dll`, `Vendor.Data.dll`, `ThirdParty.Core.dll`).

---

## 2. Struktur der SubAgenten & Test-Linsen

Die Prüfung ist in 7 spezialisierte SubAgenten-Linsen unterteilt:

| SubAgent / Linse | Fokus & Tools | Zieldatei |
| :--- | :--- | :--- |
| **SubAgent 1** | Server Health, Lifecycle, Resource-URIs & Projekt-Discovery (`get_server_health`, `get_file_tree`, `get_index_scope`, `reload_config`, `ainetlinter://*`) | `reports/01-server-health-discovery.md` |
| **SubAgent 2** | Decompiled Assembly Inspection & Extensions (`inspect_assembly`, `find_assembly_extensions`) | `reports/02-decompiled-assembly-inspection.md` |
| **SubAgent 3** | Decompiled Symbols, Navigation, Call Trees & Method Bodies (`find_symbol`, `find_references`, `get_call_tree`, `get_symbol_body`, `get_class_structure`, `get_type_hierarchy`, `get_namespace_tree`) | `reports/03-decompiled-symbols-and-navigation.md` |
| **SubAgent 4** | Source Project Semantik & Code-Comprehension (`get_feature_context`, `find_symbol`, `get_symbol_body`, `get_class_structure`, `get_file_skeleton`, `get_type_hierarchy`, `dependency_graph`, `get_test_context`, `get_impact`, `search_pattern`) | `reports/04-source-project-comprehension.md` |
| **SubAgent 5** | Violations, Safeguard, Metrics & Code Quality Rules (`get_violations`, `safeguard`, `metrics_lookup`, `metrics_tree`, `get_hotspots`, `find_dead_code`, `find_duplicates`, `find_magic_values`, `pattern_detect`, `report_observability_feedback`) | `reports/05-violations-safeguard-metrics.md` |
| **SubAgent 6** | Token-Effizienz, Agentic UX, Error-Formate & MCP-Protokoll-Verträge (Payload-Messungen, Trunkierung, Header, JSON-RPC StructuredContent, IsError-Policy) | `reports/06-token-efficiency-ux-contracts.md` |
| **SubAgent 7** | Dokumentation, Agent-Guide, Workflow-Regeln & Schema-Alignment (`Docs/`, `AiNetLinter-McpWorkflow.mdc`, `instructions.md`, Tool-Parameter-Beschreibungen) | `reports/07-documentation-rules-guide.md` |

---

## 3. Klassifizierungsschema für Befunde

Jeder Befund wird einheitlich nach Schweregrad, Umfang, Beweissicherheit und Dringlichkeit klassifiziert:

- **Schweregrad (Severity)**:
  - `S0` (Blocker): Fataler Fehler, Server-Crash, Deadlock oder Totalausfall wesentlicher MCP-Funktionen.
  - `S1` (Kritisch/High): Schwerer semantischer Fehler, falsche Analyseergebnisse, grobe Protokollverletzung oder massiver Token-Overflow (>15k unangeforderte Tokens).
  - `S2` (Mittel/Medium): Funktionale Einschränkung, unpassende Fehlerbehandlung (z. B. `isError=true` bei recoverable Argumenten), mangelhafte Filterung, suboptimale Defaults.
  - `S3` (Niedrig/Minor/UX): Kosmetische Mängel, unklare Dokumentationstexte, kleinere Redundanzen, Optimierungspotenziale bei UX/Token-Economy.

- **Umfang (Scope)**:
  - `U0` (Lokal): Einzelnes Tool / isolierter Parameterpfad.
  - `U1` (Komponente): Ein ganzes Subsystem (z. B. Assembly-Decompiler-Dispatcher, SymbolGraph-Resolver).
  - `U2` (Systemweit): Alle oder viele MCP-Tools betreffend (z. B. Base-Response-Formatter, Target-Resolver).
  - `U3` (Architektur/Protokoll): Grundlegende Serverarchitektur, Wire-Protokoll oder Regeldefinition.

- **Dringlichkeit (Urgency)**:
  - `P1` (Sofort / Nächster Release-Zyklus)
  - `P2` (Mittelfristig / Roadmap)
  - `P3` (Nice-to-have / Backlog)

---

## 4. Methodik & Ausführung

- Jeder SubAgent führt echte Live-Aufrufe über den MCP-Server aus.
- Ergebnisse, Reaktionszeiten, Payload-Größen und eventuelle Fehler werden erfasst.
- Nach jedem SubAgent-Durchlauf wird die Zieldatei im `reports/`-Verzeichnis gespeichert und ein Git-Commit gemäß den Konventionen durchgeführt.
- Zum Abschluss erstellt der Orchestrator eine zusammenfassende Synthese in `findings-overview.md`, `roadmap.md` und `tech-debt.md`.
