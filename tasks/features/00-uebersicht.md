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

Für die nächsten To-Dos existieren eigenständige Detail-Konzepte:

1. [01-metrics-lookup.md](01-metrics-lookup.md) — **One-Shot-Metriken & AI-Context-Footprint**
   * Bündelt CC, CogC, LOC, Parameter-Anzahl und AI-Context-Footprint für ein Symbol in einem schnellen Call.
2. [02-similar-names.md](02-similar-names.md) — **Naming-Drift-Erkennung**
   * Erkennt inkonsistente Benennungsfamilien (z. B. `UserDto`, `UserData`, `UserModel`) rein lexikalisch über den Roslyn-Symbolgraphen (Schicht 3 der Drift-Audit-Initiative).
3. [03-test-context.md](03-test-context.md) — **Test-Coverage-Awareness (`get_test_context`)**
   * Exponiert den bestehenden `TestCoverageResolver` als MCP-Tool, um vor Refactorings sofort die zugehörigen Unit-/Integration-Tests zu identifizieren.

---

## 3. Nachgelagerte Ideen & Verworfene Features

* [04-bedingt-sinnvoll.md](04-bedingt-sinnvoll.md) — Sammeldokument für nachgelagerte Ideen (Composite-Tools wie `feature_context`, ASP.NET-Analyzer-Suite).
* [05-nicht-umsetzen.md](05-nicht-umsetzen.md) — Begründete Ausschlussliste (u. a. `validate_file`, `trace_flow`, `preview_refactor`, RAG/Vektorsuche, `get_fixes`).
