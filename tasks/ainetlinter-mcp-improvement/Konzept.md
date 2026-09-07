---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions:
  - Welche systemischen Befunde werden nach der Normalisierung tatsächlich behoben?
  - Welche API-Verträge dürfen zugunsten einer kleineren Agentenoberfläche geändert werden?
depends_on:
  - tasks/ainetlinter-mcp-usage-audit/Konzept.md
related_tasks:
  - tasks/ainetlinter-mcp-usage-audit
supersedes: null
---

# Konzept: AiNetLinter-MCP-Verbesserung

## Ziel

Die AiNetLinter-MCP-Oberfläche soll für Coding-Agenten verlässlich, Folge-Call-tauglich und tokenbewusst werden. Grundlage ist das read-only Audit unter `tasks/ainetlinter-mcp-usage-audit/`.

Das Audit bleibt Evidenzarchiv. Dieses Konzept bündelt wiederkehrende Root Causes, priorisiert den erwarteten Nutzen und bereitet verifizierbare Umsetzungspakete vor.

## Verbindliche Scope-Grenze

- `tasks/ainetlinter-mcp-usage-audit/` bleibt unverändert und read-only.
- Audit-Findings werden nicht gelöscht, umbenannt oder inhaltlich überschrieben.
- Es entstehen keine Einzel-Tasks pro Finding.
- Produktionsänderungen erfolgen erst in den priorisierten Umsetzungspaketen.
- Die bestehende Roslyn-/statische Analysegrenze bleibt erhalten.

## Priorisierte Umsetzungspakete

| Prio | Paket | Hauptziel | Ausgangsbefunde |
| --- | --- | --- | --- |
| 01 | Discovery Contract | Vertrauenswürdige Dateilandkarte, Schema-Entdeckung und C#/Nicht-C#-Fallbacks | `get_file_tree`, `tool-discovery`, `get_index_scope`, `search_pattern`, `combo-discovery-fallback` |
| 02 | Symbol IDs & Navigation | Kanonische, zwischen Folge-Tools nutzbare Symbol-IDs | `find_symbol`, `get_file_skeleton`, `get_symbol_body`, `get_class_structure`, Referenz-/Navigations-Tools, `combo-batch-ids`, `combo-cross-target` |
| 03 | Completeness & Paging | Ehrliche Vollständigkeit, Truncation und deterministische Fortsetzung | toolübergreifende Truncation-/Completeness-Befunde |
| 04 | Core Agent Workflows | Verlässlicher Kontext-, Impact-, Lint- und Test-Workflow | `get_feature_context`, `get_impact`, `get_violations`, `metrics_lookup`, `get_test_context`, Combo-Findings |
| 05 | Assembly & Cross-Target | Robuster Vertrag für externe DLL-/EXE-Snapshots | Assembly-Tools, `resolve_type_origin`, `combo-assembly`, `combo-cross-target` |
| 06 | Ranking, Scope & Precision | Relevante Treffer und weniger irreführende False Negatives/Positives | Caller-Ranking, Partial-Klassen, Glob-/Scope-, Metrik- und Pattern-Befunde |
| 07 | Schema & Documentation | JSON-Schema, Fehlertexte, Runtime-Instructions und Referenzdoku synchronisieren | `tool-discovery`, Resources, `Docs/agent-api.md`, README, MCP-Regeln |
| 08 | Keep & Defer | Funktionierendes schützen, Wünsche und geringe Risiken bewusst zurückstellen | `ok`-Befunde, kosmetische Reibung, unbelegte `wish`-Forderungen |

Die Pakete 01–06 enthalten jeweils ihre eigene Schema-/Dokumentations- und Testarbeit. Paket 07 ist nur für verbleibende, nicht paketgebundene Bereinigung vorgesehen.

## Entscheidungsmodell für Findings

Jeder Befund erhält in der Synthese genau eine Disposition:

- `fix` — reproduzierbarer Produktfehler oder systemische Agenten-Reibung
- `document` — Verhalten ist absichtlich oder technisch unvermeidbar, aber unklar beschrieben
- `defer` — sinnvoll, aber nachgelagert oder abhängig von einem früheren Vertrag
- `keep` — funktioniert ausreichend und erhält Regressionstests
- `discard` — nicht belastbar, redundant oder außerhalb des Produktziels; Begründung erforderlich

Severity und Priorität werden getrennt behandelt. Ein `friction`-Befund kann wegen seiner Wirkung auf den ersten Agenten-Call hoch priorisiert werden; ein `wish`-Befund ist nicht automatisch umzusetzen.

## Muss-Kriterien

- Jeder nicht-triviale Audit-Befund ist genau einer Problemgruppe oder `keep/defer/discard` zugeordnet.
- Wiederkehrende Root Causes werden nur einmal als Umsetzungsthema geführt.
- Positive Befunde werden als kompakte Baseline erhalten, damit funktionierende Verträge nicht regressieren.
- Jedes Umsetzungspaket benennt betroffene Quellbereiche, API-Verträge, Tests, Dokumentation und Abhängigkeiten.
- Die finale AiNetLinter-Verifikation folgt den Repository-Gates aus `AGENTS.md`.

## Vorläufige Akzeptanzkriterien

- Ein Agent kann vom Discovery-Call bis zum relevanten Folge-Call ohne manuelles ID-Raten navigieren.
- Antworten unterscheiden belastbar zwischen leer, vollständig, abgeschnitten, nicht unterstützt und fehlerhaft.
- Truncation liefert entweder einen brauchbaren Fortsetzungsmechanismus oder eine klare, überprüfbare Begrenzung.
- Änderungen an MCP-Verträgen sind in Schema, Runtime-Instructions und `Docs/agent-api.md` konsistent.
- Die als `keep` markierten Happy Paths bleiben durch gezielte Tests abgesichert.

## Geplante Verifikation

- pro Paket: gezielte Fast-/Integration-/MCP-Tests entsprechend dem Risiko;
- nach dem Gesamtumfang: `dotnet build` sowie beide vollständigen Nicht-Stress-Testläufe gemäß `AGENTS.md`;
- nach größeren Änderungen: passender MCP-/Safeguard-Nachweis und abschließender Audit-Skill;
- Dokuprüfung gegen den aktuellen Code, nicht gegen alte Audittexte.

## Arbeitsgedächtnis (nur Draft)

- Das Audit-Konzept ist `status: ready` und bleibt unverändert.
- Die neue Struktur ist als Folge-Task angelegt; noch keine produktive Änderung.
- Die vorläufige Reihenfolge priorisiert zuerst Vertrauenswürdigkeit und Folge-Call-Verträge, danach Vollständigkeit und fachliche Workflows.
- Vor der Freigabe dieses Konzepts müssen Findings normalisiert und auf die Problemgruppen abgebildet werden.
