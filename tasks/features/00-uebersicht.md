# Feature-Übersicht & Backlog (AiNetLinter MCP-Server)

Dieses Dokument fasst den aktuellen Stand der MCP-Server-Features zusammen, verweist auf die detaillierten Konzepte für die nächsten Schritte und dokumentiert die bereits umgesetzten Features.

---

## 1. Bereits erledigt & im Code verifiziert

Die folgenden 18 MCP-Tools und Kern-Mechanismen sind vollständig implementiert, resident auf `AiNetLinter.slnx` getestet und produktiv:

| Kategorie | Tools | Beschreibung |
|---|---|---|
| **Symbolgraph** | `find_symbol`, `find_references`, `get_call_tree`, `get_impact`, `get_type_hierarchy`, `dependency_graph` | Vollständige Roslyn-Symbolnavigation, Caller-Bäume (ASCII/Mermaid), Git-Diff-Blast-Radius und echte typbasierte Abhängigkeitsgraphen. |
| **Dateistruktur & Navigation** | `get_file_skeleton`, `get_class_structure`, `get_index_scope`, `get_hotspots` | Schnelle Übersicht über Signaturen, tabellarische Member-/Zeilen-Details und Erkennung von Dateien nahe dem Zeilenlimit. |
| **Analyse & Quality Gates** | `get_violations`, `safeguard`, `pattern_detect`, `find_magic_values`, `find_dead_code`, `search_pattern` | Deterministisches 0-10 Quality-Gate (`safeguard`), Pattern-Gruppierung (God-Classes, async-void etc.), Literale/Secrets-Audit und Dead-Code-Detection. |
| **DRY & Drift** | `find_duplicates` | Token-basiertes Clone-Detection (Exact/Near/Fuzzy) + Refactoring-Drift (`helperSymbol`) + `DuplicateCodeChecker` in `PostAnalysisChecks`. |
| **Wartung & Observability** | `reload_config`, `get_server_health`, `report_observability_feedback` | Hot-Reload von `rules.json`, Status-/Health-Prüfung und Feedback-Kanal für Agenten. |

---

## 2. Nächste Schritte (Sehr sinnvoll & hoher Hebel)

Priorisierte To-Dos mit eigenständigen Detail-Konzepten:

1. [01-namespace-tree.md](01-namespace-tree.md) — **Hierarchische Code-Exploration (`get_namespace_tree`)** *(P1 - Höchster Hebel)*
   * Ermöglicht stufenweisen Zoom (Projekte ➔ Namespaces ➔ Typen) nach dem Progressive-Disclosure-Prinzip. Verhindert Kontext-Fluten und spart massiv Token bei der Orientierung in unbekannten/großen Codebases.
2. [02-metrics-lookup.md](02-metrics-lookup.md) — **One-Shot-Metriken & AI-Context-Footprint (`metrics_lookup`)**
   * Bündelt CC, CogC, LOC, Parameter-Anzahl und AI-Context-Footprint für ein Symbol in einem schnellen Call.
3. [03-similar-names.md](03-similar-names.md) — **Naming-Drift-Erkennung (`similar_names`)**
   * Erkennt inkonsistente Benennungsfamilien (z. B. `UserDto`, `UserData`, `UserModel`) rein lexikalisch über den Roslyn-Symbolgraphen (Schicht 3 der Drift-Audit-Initiative).
4. [04-test-context.md](04-test-context.md) — **Test-Coverage-Awareness (`get_test_context`)**
   * Exponiert den bestehenden `TestCoverageResolver` als MCP-Tool, um vor Refactorings sofort die zugehörigen Unit-/Integration-Tests zu identifizieren.
5. [05-feature-context.md](05-feature-context.md) — **Composite One-Shot-Exploration (`get_feature_context`)**
   * Bündelt Deklaration, Callers, Tests, Metriken/Budget und offene Violations für ein Ziel-Symbol in einem einzigen Call vor Refactorings.

---

## 3. Nachgelagerte Ideen & Verworfene Features

* [06-bedingt-sinnvoll.md](06-bedingt-sinnvoll.md) — Sammeldokument für nachgelagerte Ideen (ASP.NET-Framework-Analyzer-Suite).
* [07-nicht-umsetzen.md](07-nicht-umsetzen.md) — Begründete Ausschlussliste (u. a. `validate_file`, `trace_flow`, `preview_refactor`, RAG/Vektorsuche, `get_fixes`).
