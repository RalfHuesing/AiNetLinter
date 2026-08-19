# Feature-Übersicht & Backlog (AiNetLinter MCP-Server)

Dieses Dokument priorisiert die verbleibenden Features nach **Effizienz (Token-/Kosteneinsparung)** und **Code-Qualität (Prävention von Tech-Debt)**.

---

## 1. Bereits erledigt & im Code verifiziert

Die folgenden 24 MCP-Tools und Kern-Mechanismen sind vollständig implementiert, resident auf `AiNetLinter.slnx` getestet und produktiv:

| Kategorie | Tools | Beschreibung |
|---|---|---|
| **Symbolgraph** | `find_symbol`, `find_references`, `get_call_tree`, `get_impact`, `get_type_hierarchy`, `dependency_graph` | Vollständige Roslyn-Symbolnavigation, Caller-Bäume (ASCII/Mermaid), Git-Diff-Blast-Radius und echte typbasierte Abhängigkeitsgraphen. |
| **Dateistruktur & Navigation** | `get_namespace_tree`, `get_file_skeleton`, `get_class_structure`, `get_index_scope`, `get_hotspots` | Progressive Disclosure (Projekte ➔ Namespaces ➔ Typen), schnelle Übersicht über Signaturen, tabellarische Member-/Zeilen-Details und Erkennung von Dateien nahe dem Zeilenlimit. |
| **Analyse & Quality Gates** | `metrics_lookup`, `get_violations`, `safeguard`, `pattern_detect`, `find_magic_values`, `find_dead_code`, `search_pattern` | Punktgenaue One-Shot-Metriken & Schwellwert-Abgleich (`metrics_lookup`), deterministisches 0-10 Quality-Gate (`safeguard`), Pattern-Gruppierung (God-Classes, async-void etc.), Literale/Secrets-Audit und Dead-Code-Detection. |
| **DRY & Drift** | `find_duplicates` | Token-basiertes Clone-Detection (Exact/Near/Fuzzy) + Refactoring-Drift (`helperSymbol`) + `DuplicateCodeChecker` in `PostAnalysisChecks`. |
| **Wartung & Observability** | `reload_config`, `get_server_health`, `report_observability_feedback` | Hot-Reload von `rules.json`, Status-/Health-Prüfung und Feedback-Kanal für Agenten. |

---

## 2. Priorisierte Abarbeitungs-Reihenfolge (ROI: Token-Save & Qualität)

Die Reihenfolge minimiert Kontext-Roundtrips, senkt API-Kosten pro Agenten-Task und verhindert architektonischen Drift:

### Prio 1: Maximale Token- & Roundtrip-Reduktion vor Edits
1. **[01-feature-context.md](01-feature-context.md) — Composite One-Shot-Exploration (`get_feature_context`)** *(Prio 1 — Höchster Workflow- & Token-Hebel)*
   * **Token-ROI:** Maximal. Bündelt Deklaration, Callers, Tests, Metriken/Budget und offene Violations für ein Ziel-Symbol. **Ersetzt 4–5 aufeinanderfolgende Tool-Calls durch genau 1 Call** vor jedem Refactoring.
2. **[02-test-context.md](02-test-context.md) — Test-Coverage-Awareness (`get_test_context`)** *(Prio 2 — Hohe Token-Ersparnis & Baustein für Feature-Kontext)*
   * **Workflow- & Token-ROI:** Hoch. Exponiert den residenten `TestCoverageResolver` als MCP-Tool, um vor/nach Code-Änderungen sofort die exakten Unit-/Integrationstests einer Methode zu isolieren und gezielt auszuführen, ohne heuristisches Suchen.

### Prio 2: Semantische Qualität & Drift-Prävention (DRY Schicht 3 & 4)
3. **[03-structural-drift-detection.md](03-structural-drift-detection.md) — Semantische DRY-Erkennung via AST-Fingerprints (`find_duplicates` mit `mode="structural"`)** *(Prio 3 — Höchster Qualitäts-Hebel für DRY)*
   * **Qualitäts-ROI:** Maximal. Erkennt parallele Zwillingsmethoden (Typ-4-Drift wie redundante Enum-Switches / Kind-Mapper) über Merkmalsvektoren und Cosine-Similarity. Verhindert Code-Aufblähung nachhaltig.
4. **[04-similar-names.md](04-similar-names.md) — Naming-Drift & Semantische Namensfamilien (`similar_names`)** *(Prio 4 — Hoher Qualitäts-Hebel)*
   * **Qualitäts-ROI:** Hoch. Erkennt inkonsistente DTO-/Model-Familien (`UserDto`, `UserData`) und Hilfsfunktions-Drift rein lexikalisch und signaturbasiert über den Roslyn-Symbolgraphen.

---

## 3. Nachgelagerte Ideen & Verworfene Features

* [05-bedingt-sinnvoll.md](05-bedingt-sinnvoll.md) — Sammeldokument für nachgelagerte Ideen (ASP.NET-Framework-Analyzer-Suite).
* [06-nicht-umsetzen.md](06-nicht-umsetzen.md) — Begründete Ausschlussliste (u. a. `validate_file`, `trace_flow`, `preview_refactor`, RAG/Vektorsuche, `get_fixes`).

