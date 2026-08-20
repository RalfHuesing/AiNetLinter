# Feature-Übersicht & Backlog (AiNetLinter MCP-Server)

Dieses Dokument priorisiert die verbleibenden Features nach **Effizienz (Token-/Kosteneinsparung)** und **Code-Qualität (Prävention von Tech-Debt)**.

---

## 1. Bereits erledigt & im Code verifiziert

Die folgenden MCP-Tools und Kern-Mechanismen sind vollständig implementiert, resident auf `AiNetLinter.slnx` getestet und produktiv:

| Kategorie | Tools | Beschreibung |
|---|---|---|
| **Feature & Kontext** | `get_feature_context`, `get_test_context` | Composite One-Shot-Exploration vor Edits/Refactorings: bündelt 5 Dimensionen (`get_feature_context`) sowie dedizierte Test-Isolation mit kopierbaren `dotnet test` Filterbefehlen (`get_test_context`). |
| **Symbolgraph** | `find_symbol`, `find_references`, `get_call_tree`, `get_impact`, `get_type_hierarchy`, `dependency_graph` | Vollständige Roslyn-Symbolnavigation, Caller-Bäume (ASCII/Mermaid), Git-Diff-Blast-Radius und echte typbasierte Abhängigkeitsgraphen. |
| **Dateistruktur & Navigation** | `get_namespace_tree`, `get_file_skeleton`, `get_class_structure`, `get_index_scope`, `get_hotspots` | Progressive Disclosure (Projekte ➔ Namespaces ➔ Typen), schnelle Übersicht über Signaturen, tabellarische Member-/Zeilen-Details und Erkennung von Dateien nahe dem Zeilenlimit. |
| **Analyse & Quality Gates** | `metrics_lookup`, `get_violations`, `safeguard`, `pattern_detect`, `find_magic_values`, `find_dead_code`, `search_pattern` | Punktgenaue One-Shot-Metriken & Schwellwert-Abgleich (`metrics_lookup`), deterministisches 0-10 Quality-Gate (`safeguard`), Pattern-Gruppierung (God-Classes, async-void etc.), Literale/Secrets-Audit und Dead-Code-Detection. |
| **DRY & Drift** | `find_duplicates` | Token-basiertes Clone-Detection (Exact/Near/Fuzzy) + Refactoring-Drift (`helperSymbol`) + `DuplicateCodeChecker` in `PostAnalysisChecks`. |
| **Wartung & Observability** | `reload_config`, `get_server_health`, `report_observability_feedback` | Hot-Reload von `rules.json`, Status-/Health-Prüfung und Feedback-Kanal für Agenten. |

---

## 2. Priorisierte Abarbeitungs-Reihenfolge (ROI: Token-Save & Qualität)

Die verbleibenden Features minimieren Kontext-Roundtrips, senken API-Kosten pro Agenten-Task und verhindern architektonischen Drift:

### Prio 1: Semantische Qualität & Drift-Prävention (DRY Schicht 3 & 4)
1. **[04-similar-names.md](04-similar-names.md) — Naming-Drift & Semantische Namensfamilien (`similar_names`)** *(Prio 2 — Hoher Qualitäts-Hebel)*
   * **Qualitäts-ROI:** Hoch. Erkennt inkonsistente DTO-/Model-Familien (`UserDto`, `UserData`) und Hilfsfunktions-Drift rein lexikalisch und signaturbasiert über den Roslyn-Symbolgraphen.

---

## 3. Nachgelagerte Ideen & Verworfene Features

* [05-bedingt-sinnvoll.md](05-bedingt-sinnvoll.md) — Sammeldokument für nachgelagerte Ideen (ASP.NET-Framework-Analyzer-Suite).
* [06-nicht-umsetzen.md](06-nicht-umsetzen.md) — Begründete Ausschlussliste (u. a. `validate_file`, `trace_flow`, `preview_refactor`, RAG/Vektorsuche, `get_fixes`).
