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

## 360°-Bewertung aus Agentensicht

### 1. Erreichbarkeit und Routing

Der Agent muss zuerst wissen, welches Tool für Projektstruktur, C#-Symbole, Nicht-C#-Dateien, Assemblys und Betriebsstatus zuständig ist. Falsche Dateibäume, unvollständige Tool-Kataloge oder fehlende Fallback-Hints sind deshalb höher zu bewerten als ein einzelner Spezialfehler.

### 2. Semantisches Vertrauen

Eine formal erfolgreiche Antwort ist gefährlich, wenn sie falsche Elternbeziehungen, falsche Scopes, Testtreffer statt Produktionsnutzer, widersprüchliche Lint-/Metrik-Werte oder „keine Treffer“ trotz unvollständiger Analyse zeigt. Die Definition von „leer“, „vollständig“, „nicht unterstützt“ und „teilweise“ ist ein Produktvertrag, kein reiner Formatter-Aspekt.

### 3. Folge-Call und Identität

Der zentrale Agentenwert ist nicht die erste Antwort, sondern die Fähigkeit, daraus den nächsten Call ohne Raten zu bauen. Jede relevante Ausgabe braucht daher entweder eine kopierbare kanonische ID oder einen expliziten Hinweis, warum kein Handoff möglich ist. Projekt- und Assembly-Identitäten dürfen nicht stillschweigend austauschbar sein.

### 4. Token- und Latenzbudget

Große Antworten sind nicht automatisch besser. Defaults müssen die fachlich relevanten Treffer priorisieren, Diagnose-/Referenzrauschen begrenzen und eine sichere Fortsetzung anbieten. Composite-Tools dürfen keine mehrfachen Vollberichte erzeugen, wenn ein gezielter Drilldown genügt.

### 5. Projekt- und Assembly-Semantik

Eine dekompilierte Assembly ist ein statischer Snapshot ohne Consumer-Projekt und ohne Laufzeitbeweis. Diese Grenze ist akzeptabel, muss aber in Herkunft, `partial`, `not_decidable`, BCL-/Referenzfiltern und Capability-Fehlern sichtbar sein. Projekt-Tools dürfen nicht so tun, als hätten sie Assembly-Parität, wenn sie sie nicht besitzen.

### 6. Betriebs- und Lebenszeitsemantik

Health, Overview, Rules, Bootstrap, Daemon/stdio und Session-Generationen müssen einen konsistenten Betriebszustand beschreiben. Ein Agent darf aus einer Pfadbestätigung nicht auf „ready“ schließen und darf einen Bootstrap mit potenziellen Schreibschritten nicht ungefragt starten.

### 7. Sicherheits- und Schadensgrenze

Die Analyse bleibt read-only und führt Assemblys nicht aus. Besonders gefährlich sind nicht nur technische Crashes, sondern Agentenentscheidungen wie falsches Löschen von angeblich totem Code, falsche Lint-Entwarnung, falsche Produktions-Impact-Annahmen oder ungefragte Projektintegration. Diese Fälle brauchen konservative Beschreibungen und explizite Confidence-/Scope-Hinweise.

### 8. Wartbarkeit und Vertragsdrift

Schema, Registrierung, Formatter, StructuredContent, Runtime-Instructions, `Docs/agent-api.md` und Tests müssen aus derselben Verhaltensentscheidung aktualisiert werden. Ein einzelner Fix im Formatter ohne Vertrags- und Regressionstest würde die beobachtete Drift nur verschieben.

## Entscheidungsmodell für Findings

Jeder Befund erhält in der Synthese genau eine Disposition:

- `fix` — reproduzierbarer Produktfehler oder systemische Agenten-Reibung
- `document` — Verhalten ist absichtlich oder technisch unvermeidbar, aber unklar beschrieben
- `defer` — sinnvoll, aber nachgelagert oder abhängig von einem früheren Vertrag
- `keep` — funktioniert ausreichend und erhält Regressionstests
- `discard` — nicht belastbar, redundant oder außerhalb des Produktziels; Begründung erforderlich

Severity und Priorität werden getrennt behandelt. Ein `friction`-Befund kann wegen seiner Wirkung auf den ersten Agenten-Call hoch priorisiert werden; ein `wish`-Befund ist nicht automatisch umzusetzen.

## Arbeitsablauf der Synthese

Die Ausarbeitung erfolgt in fünf Planungsrunden, bevor Produktionscode geändert wird:

1. **Inventar und Normalisierung:** Alle Audit-Dateien werden read-only gelesen. Pro Finding werden primäre Schwere, Verdict, Nutzbarkeit, konkrete Evidenz, vermutete Root Cause und betroffene Umsetzungspakete extrahiert. Nicht-kanonische Angaben wie `Mittel` oder fehlende Primär-Schwere werden markiert, nicht stillschweigend umgedeutet.
2. **Deduplizierung und Clustering:** Einzelbefunde und Combos werden nicht addiert, wenn sie dieselbe Ursache belegen. Ein Root Cause erscheint einmal als Problem, die übrigen Findings bleiben als Evidenzverweise erhalten.
3. **Nutzen-/Risiko-Bewertung:** Jede Problemgruppe wird nach Agentenwirkung, Reichweite, Risiko falscher Schlussfolgerungen, Abhängigkeiten und Umsetzungsaufwand bewertet. Severity bleibt dabei ein Befundmerkmal und wird nicht zur alleinigen Prioritätsformel.
4. **Paketverträge:** Für jedes `fix`-Paket werden Scope, Non-Goals, betroffene Symbole/Dateien, API-Vertrag, Regressionstests, Dokumentationsänderungen und Abnahmekriterien festgelegt. `keep`, `document`, `defer` und `discard` werden begründet.
5. **Nutzerfreigabe:** Erst wenn Matrix, Prioritäten und Paketgrenzen konsistent sind, wird dieses Konzept ausdrücklich auf `ready` gesetzt. Danach entscheidet der Nutzer, ob Paket 01 umgesetzt oder zunächst ein anderes Paket gestartet wird.

Das dauerhafte Synthese-Artefakt ist eine Befundmatrix im neuen Verbesserungs-Task. Sie enthält mindestens `Finding`, `Root Cause`, `Disposition`, `Paket`, `Priorität`, `Nutzen`, `Risiko`, `Evidence`, `Quellbereich`, `Testbedarf` und `Doku-Bedarf`.

## Entscheidungs- und Übergabegates

- **Gate A – Evidenz:** Jede Aussage in der Synthese verweist auf mindestens ein Audit-Finding; das Audit selbst bleibt unverändert.
- **Gate B – Root Cause:** Kein Umsetzungspaket enthält nur Symptome oder doppelte Befunde ohne gemeinsame Ursache.
- **Gate C – Priorität:** Die Reihenfolge erklärt sich aus Agentennutzen und Abhängigkeiten, nicht aus der Dateireihenfolge.
- **Gate D – Umsetzung:** Kein Paket startet ohne überprüfbare Akzeptanzkriterien und passende Verifikation.
- **Gate E – Abschluss:** Nach der Umsetzung gelten Build, Nicht-Stress-Testläufe, MCP-Nachweise und der passende Audit gemäß `AGENTS.md`.

## Entscheidungen, die vor der Umsetzung benötigt werden

Die Synthese ist bis hierhin autonom möglich. Vor dem ersten Produktionspaket bleiben diese Produktentscheidungen offen:

1. **Kompatibilität:** Dürfen öffentliche Response-Formate und ID-Semantik breaking geändert werden? Empfehlung: zunächst additive Felder und Übergangskompatibilität; alte IDs nur entfernen, wenn ein klarer Versions-/Migrationspfad existiert.
2. **Priorität der Targets:** Soll der Projekt-C#-Pfad zuerst vollständig agententauglich werden und Assembly danach folgen? Empfehlung: ja; Assembly bleibt im Scope, blockiert aber nicht die erste Projektlieferung.
3. **Paging-Vertrag:** Soll ein gemeinsamer `continuationToken`-/Completeness-Envelope für alle begrenzten Tools eingeführt werden? Empfehlung: ja, statt pro Tool eigene Offset- oder „maxResults erhöhen“-Workarounds zu behalten.
4. **Text versus StructuredContent:** Ist die sichtbare Agentenoberfläche primär Markdown/Text, oder dürfen wir StructuredContent als führenden Vertrag voraussetzen? Empfehlung: beide aus derselben typisierten Aggregation erzeugen; kein wichtiges Follow-up nur in StructuredContent verstecken.
5. **Abnahmeschwelle:** Sollen „kein manuelles ID-Raten“ und „kein falsches Grün bei `complete`/leer“ harte Release-Kriterien sein? Empfehlung: ja.
6. **Scope der Wünsche:** Sollen Ranking- und Heuristikverbesserungen (`dead_code`, `magic_values`, Testzuordnung, Produktionspriorisierung) Bestandteil der ersten Lieferung sein? Empfehlung: die gefährlichen False Positives/Negatives ja, reine Komfort-Wishes später.

## Nächster Planungsschritt

Als nächstes wird die Befundmatrix aus den vorhandenen Einzel- und Combo-Findings aufgebaut. Danach prüfen wir zuerst die Cluster `Discovery Contract`, `Symbol IDs & Navigation` und `Completeness & Paging` gegeneinander, weil sie die stärksten Querverbindungen und Abhängigkeiten haben.

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
- Der nächste konkrete Arbeitsgegenstand ist eine Befundmatrix; sie ersetzt keine Umsetzung und keine Änderung am read-only Audit.
- Die Synthese wird iterativ mit dem Nutzer abgestimmt; das Konzept bleibt bis zur ausdrücklichen Freigabe `draft`.
- Die 45/45-Befunde sind in `Befundmatrix.md` genau einmal einer primären Problemgruppe und Disposition zugeordnet.
- Die verbleibende Blockade ist keine Informationslücke im Audit, sondern eine Produktentscheidung zu Kompatibilität, Target-Reihenfolge, Paging, sichtbarer Antwortoberfläche und Abnahmeschwelle.
