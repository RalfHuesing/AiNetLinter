# Befundmatrix: AiNetLinter-MCP-Verbesserung

Stand: 2026-09-07
Quelle: `tasks/ainetlinter-mcp-usage-audit/` (read-only)

Diese Matrix ist die Synthese der 45 Audit-Dateien: 33 Tool-Findings, 3 Resource-Findings, `tool-discovery` und 8 Combo-Findings. Combos werden nicht als zusätzliche Fehler gezählt, sondern als Evidenz für Root Causes verwendet.

## Dispositionen

| Disposition | Bedeutung |
| --- | --- |
| `fix` | reproduzierbarer Produktfehler oder systemische Agenten-Reibung |
| `document` | Verhalten bleibt zunächst bestehen, muss aber eindeutig erklärt oder korrekt als Capability begrenzt werden |
| `defer` | sinnvoll, aber abhängig von einem früheren Vertrag oder außerhalb der ersten Lieferwelle |
| `keep` | ausreichend brauchbarer Pfad; als Regression-Baseline schützen |
| `discard` | nicht belastbar, redundant oder außerhalb des Produktziels; derzeit kein solcher Befund |

## Prioritätslogik

Die Paketnummer ist die vorläufige fachliche Gruppierung. Die daraus
abgeleiteten Release-Tasks sind in `tasks/01-*` bis `tasks/04-*` beschrieben.
Die Gruppierung kombiniert:

- **Agentenwirkung:** Verhindert der Befund den nächsten sinnvollen Call oder erzeugt er falsches Vertrauen?
- **Reichweite:** Betrifft er viele Tools oder nur einen Spezialpfad?
- **Abhängigkeit:** Muss die Lösung vor anderen Paketen existieren?
- **Kosten/Risiko:** Wie groß ist die Änderung im gemeinsamen MCP-Vertrag?

Severity bleibt der Befundstatus aus dem Audit. `Mittel` bzw. fehlende kanonische Schwere wird bewusst als Normalisierungsproblem markiert.

Die Reihenfolge dient der Abhängigkeits- und Releaseplanung. Eng verbundene
Pakete liegen in einem gemeinsamen Task-Ordner; Assembly-Qualität bleibt wegen
ihres separaten Lifecycles und der seltenen Nutzung ein späterer eigener Task.

## Bounded Exploration statt Seitenbrowser

Die Matrix bewertet Paging nicht als „Agent muss Seite 1 bis N lesen“. Für große Ergebnismengen gilt:

- kleine, fachlich gerankte Defaults;
- ehrliche `totalCount`-/`returnedCount`-/`completeness`-/`truncatedBy`-Metadaten;
- bevorzugt Filter, Scope, Richtung, Detaillevel oder Zusammenfassung als nächster Schritt;
- opaker `continuationToken` nur für einen sinnvollen weiteren Ausschnitt;
- bei nicht sinnvoll rankbaren Massenmengen ein expliziter Scope-/Filter-Hinweis statt beliebiger erster Treffer.

Das Ziel ist eine belastbare nächste Agentenentscheidung, nicht die vollständige Übertragung der Datenmenge in den Kontext.

## Matrix

### 01 – Discovery Contract

| Finding | Audit-Schwere | Disposition | Root Cause / Agentenwirkung | Evidenz |
| --- | --- | --- | --- | --- |
| `get_file_tree` | `broken` | `fix` | Der Default-Baum baut Elternbeziehungen aus einer flachen Präfixsortierung; zusätzlich sind Verzeichnis-Globs, Root-Dateilisten und Truncation missverständlich. Falsche Repository-Landkarte vergiftet alle Folgeentscheidungen. | `findings/get_file_tree.md` |
| `tool-discovery` | `friction` | `fix` | JSON-Schema, Runtime-Pflichtfelder, Enums und Capability-Matrix widersprechen sich; der Agent muss zwischen Katalog, Einzelschema und Prosa raten. | `findings/tool-discovery.md` |
| `get_index_scope` | niedrig/mittel | `fix` | Index-Abdeckung ist nützlich, nennt aber den passenden Fallback nicht und liefert bei Bindungsfehlern keinen maschinenlesbaren Pfad. | `findings/get_index_scope.md` |
| `search_pattern` | `degraded` | `fix` | Default-Korpus, Produktionsfilter, Glob-Semantik und fehlende Cursor können aus einer gültigen Suche eine falsche oder riesige Antwort machen. | `findings/search_pattern.md` |
| `combo-discovery-fallback` | `degraded` | `fix` | Die Kette `file_tree → index_scope → search_pattern` hat kein gemeinsames Pfad- und Routing-Kontrakt; JS/Razor-Agenten können in leere Suche oder Token-Flut laufen. | `findings/combo-discovery-fallback.md` |
| `resource-overview` | `friction` | `fix` | „Overview“ wirkt wie Health-/Readiness-Status, liefert aber nur Pfadbestätigung; Query- und Integrationsfehler sind nicht unterscheidbar. | `findings/resource-overview.md` |

### 02 – Symbol IDs & Navigation

| Finding | Audit-Schwere | Disposition | Root Cause / Agentenwirkung | Evidenz |
| --- | --- | --- | --- | --- |
| `find_symbol` | `degraded` | `fix` | Substring-Ranking, fehlende Attribut-Suffix-Auflösung und frühe Kappung liefern gültige, aber falsche Folge-IDs. | `findings/find_symbol.md` |
| `get_file_skeleton` | `degraded` | `fix` | Member-IDs sind bei Truncation nicht vollständig; Assembly-IDs und Fehlertexte sind nicht konsistent weiterreichbar. | `findings/get_file_skeleton.md` |
| `get_symbol_body` | Mittel | `fix` | Bekannte IDs funktionieren gut, aber Body-Fenster, Partial-Typen und Cross-Target-Fehler erschweren zuverlässige Navigation. | `findings/get_symbol_body.md` |
| `get_class_structure` | `degraded` | `fix` | Memberzeilen sehen wie IDs aus, sind aber keine; große Typen haben keine Fortsetzung und melden widersprüchliche Completeness. | `findings/get_class_structure.md` |
| `find_references` | `degraded` | `fix` | Default-Ranking bevorzugt Tests, IDs an Call-Sites fehlen, Depth/Assembly-Diagnosen erzeugen falsche oder zu große Folgeantworten. | `findings/find_references.md` |
| `get_call_tree` | `degraded` | `fix` | Typ-/Methodenauflösung, Assembly-Referenzen, BCL und Truncation sind nicht als durchgängiger Navigationsvertrag modelliert. | `findings/get_call_tree.md` |
| `find_implementations` | `degraded` | `fix` | Happy Path ist brauchbar, aber IDs, Assembly-Grenzen und Truncation verhindern verlässliche Weiternavigation. | `findings/find_implementations.md` |
| `get_type_hierarchy` | `degraded` | `fix` | Assembly-Implementierer bleiben leer, Partial-Typen duplizieren sich und Hierarchieantworten liefern nicht immer anschlussfähige IDs. | `findings/get_type_hierarchy.md` |
| `dependency_graph` | `degraded` | `fix` | Kanten enthalten keine Symbol-IDs; Incoming-Ranking und Assembly-Leermengen können Abhängigkeiten falsch erscheinen lassen. | `findings/dependency_graph.md` |
| `combo-batch-ids` | `degraded`/`broken` | `fix` | Batch- und Singularverträge sind asymmetrisch; Assembly-Generationen machen kopierte IDs zwischen Turns ungültig. | `findings/combo-batch-ids.md` |
| `combo-type-nav` | `degraded` | `fix` | Navigation emittiert je Tool unterschiedliche Identifier-Formate und wechselt bei Namespace-/Member-Ebenen still den Modus. | `findings/combo-type-nav.md` |

### 03 – Completeness & Paging

| Finding | Audit-Schwere | Disposition | Root Cause / Agentenwirkung | Evidenz |
| --- | --- | --- | --- | --- |
| `get_namespace_tree` | `degraded` | `fix` | Truncation ohne Pagination, ignoriertes Budget und Moduswechsel lassen den Agenten Typen bzw. Namespaces unvollständig interpretieren. | `findings/get_namespace_tree.md` |
| `get_test_context` | `degraded` | `fix` | Aktive Truncation wird als vollständig beschrieben; gekürzte Testdateien können wie vollständige Zuordnung wirken. | `findings/get_test_context.md` |
| `get_violations` | `friction`/`degraded` | `fix` | Completeness-Sätze und Nicht-C#-Leermengen verhindern die Unterscheidung zwischen nicht unterstützt, leer und vollständig geprüft. | `findings/get_violations.md` |
| `get_hotspots` | `friction` | `document` | Kernfunktion ist brauchbar; Clamps und „Keine“-Darstellung müssen nur klarer als begrenzte Ergebnismenge beschrieben werden. | `findings/get_hotspots.md` |
| `metrics_tree` | `friction` | `document` | Default-Drilldown ist kompakt; Dauerhinweise, Root-Präfixe und fehlende Nicht-C#-Grenzen verfälschen vor allem bewusst erweiterte Abfragen. | `findings/metrics_tree.md` |
| `combo-mcp-first-context` | `degraded` | `fix` | Mehrere Tools liefern überlappende Caller-/Body-Information, aber keine gemeinsame Offset-/ID-Semantik; der Agent glaubt an Vollständigkeit. | `findings/combo-mcp-first-context.md` |

### 04 – Core Agent Workflows

| Finding | Audit-Schwere | Disposition | Root Cause / Agentenwirkung | Evidenz |
| --- | --- | --- | --- | --- |
| `get_feature_context` | `degraded` | `fix` | One-shot-Kontext ist wertvoll, aber Caller-Ranking, Partial-Violations, Test-Heuristik und Default-Caps erzeugen falsche Sicherheit. | `findings/get_feature_context.md` |
| `get_impact` | `degraded` | `fix` | Impact-Caller, Change-Context, Zeilen-Spans und Tool-IDs sprechen keine gemeinsame Sprache; Default-50 blendet Produktion aus. | `findings/get_impact.md` |
| `metrics_lookup` | `degraded` | `fix` | Metriken sind für bekannte IDs brauchbar, aber Footprint-/Partial-/Assembly-Werte und Batch-Größen können fachlich falsche Entscheidungen auslösen. | `findings/metrics_lookup.md` |
| `combo-impact-lint` | `degraded` | `fix` | Impact, Lint und Metriken verwenden unterschiedliche Scope-/ID-Wahrheiten; ein Agent kann eine Violation übersehen oder eine falsche Entwarnung geben. | `findings/combo-impact-lint.md` |

### 05 – Assembly & Cross-Target

| Finding | Audit-Schwere | Disposition | Root Cause / Agentenwirkung | Evidenz |
| --- | --- | --- | --- | --- |
| `inspect_assembly` | `degraded` | `fix` | Siehe Paket 03; Assembly-Default ist als API-Katalog für Agenten ungeeignet und Diagnose-/Budgetdaten dominieren die Nutzdaten. | `findings/inspect_assembly.md` |
| `search_assembly` | `degraded` | `fix` | Siehe Paket 03; FQN-Suche und Default-Root führen zu False Negatives bzw. Artefakt-Treffern. | `findings/search_assembly.md` |
| `get_assembly_context` | `broken`/`degraded` | `fix` | Siehe Paket 03; der Assembly-Composite muss dieselbe ID-, Envelope- und sichtbare Textsemantik wie Projekttools erhalten. | `findings/get_assembly_context.md` |
| `find_assembly_extensions` | `degraded` | `fix` | Siehe Paket 03; ohne Consumer-Projekt bleibt Anwendbarkeit korrekt `not_decidable`, darf aber nicht wie ein vollständiger Agentenpfad wirken. | `findings/find_assembly_extensions.md` |
| `resolve_type_origin` | `degraded`/`friction` | `fix` | Herkunft, DLL-Pfad, Solution-interne Referenzen und Kurzname-Kollisionen werden uneinheitlich dargestellt; Folge-Target kann falsch gewählt werden. | `findings/resolve_type_origin.md` |
| `combo-assembly` | `degraded` | `fix` | Inspect/Search/Extensions/Context bilden keinen stabilen Katalog→Typ→Detail-Vertrag. | `findings/combo-assembly.md` |
| `combo-cross-target` | `degraded`/`broken` | `fix` | Project- und Assembly-ID-Familien sind nicht falsch vermischt, aber Generation, Fehlertexte und Completeness führen zu falscher Reparatur. | `findings/combo-cross-target.md` |

### 06 – Ranking, Scope & Precision

| Finding | Audit-Schwere | Disposition | Root Cause / Agentenwirkung | Evidenz |
| --- | --- | --- | --- | --- |
| `find_dead_code` | `degraded` | `fix` | Heuristische HIGH-/LOW-Kandidaten und Reflection/DI-Grenzen werden zu leicht als Löschauftrag interpretiert. | `findings/find_dead_code.md` |
| `find_duplicates` | `degraded` | `fix` | Scope-Defaults, Kandidat-vs.-Violation-Darstellung und große Testantworten verfälschen Refactoring-Entscheidungen. | `findings/find_duplicates.md` |
| `find_magic_values` | `degraded` | `fix` | Zahlenfilter und Kategorien liefern fachlich falsche oder gemischte Treffer; Security-Empfehlungen erzeugen Fehlentscheidungen. | `findings/find_magic_values.md` |
| `pattern_detect` | `degraded` | `fix` | Default-Patterns mit 0 Treffern werden verschwiegen und teilweise falsch als sauber interpretiert. | `findings/pattern_detect.md` |
| `safeguard` | `degraded` | `fix` | Scoped Score ist brauchbar, aber Top-Violations ignorieren den Scope; ein Quality-Gate kann dadurch falsche Remediation liefern. | `findings/safeguard.md` |

### 07 – Schema & Documentation / Betrieb

| Finding | Audit-Schwere | Disposition | Root Cause / Agentenwirkung | Evidenz |
| --- | --- | --- | --- | --- |
| `get_server_health` | `degraded` | `fix` | Health-Flags und Session-Sichten sind missverständlich; globale und zielgebundene Aussagen können verwechselt werden. | `findings/get_server_health.md` |
| `combo-resources-health` | `degraded` | `fix` | Overview, Guide, Rules und Health widersprechen sich bei Mode, Sessionstatus und Integrationsrouting; unnötiger Bootstrap-Tokenverbrauch. | `findings/combo-resources-health.md` |
| `resource-agent-guide` | `friction` | `document` | Bootstrap ist für ausdrückliche Integration brauchbar, aber Discovery, Token-Duplikation und veraltete Toolzählung müssen klar begrenzt werden. | `findings/resource-agent-guide.md` |
| `resource-rules` | Mittel | `document` | Policy-Zusammenfassung ist nützlich; Query-Pflicht und Fehlerunterscheidung müssen dokumentiert bzw. maschinenlesbar werden. | `findings/resource-rules.md` |
| `reload_config` | `friction` | `document` | Reload und alte Config bei Fehler funktionieren; Schema und Fehlerpfad müssen das tatsächliche project-only-Verhalten widerspiegeln. | `findings/reload_config.md` |

### 08 – Keep & Defer

| Finding | Audit-Schwere | Disposition | Entscheidung | Evidenz |
| --- | --- | --- | --- | --- |
| `report_observability_feedback` | `ok` | `keep` | Als Regression-Baseline schützen, nicht zum Hauptprojekt machen. | `findings/report_observability_feedback.md` |

## Querliegende Root Causes

### A – Vertrag ist zwischen Schema, Prosa, Runtime und Textausgabe verteilt

Viele Fehler entstehen nicht in der Roslyn-Abfrage selbst, sondern weil JSON-`required`, Enums, Description, Runtime-Validierung, Markdown und `structuredContent` verschiedene Wahrheiten darstellen. Das ist der wichtigste Querschnitt über Pakete 01–07.

### B – Ein Ergebnis ist nicht automatisch ein Agenten-Handoff

Pfade, Zeilen, Membernamen und sichtbare Tabellen sehen für Menschen plausibel aus, sind aber oft keine gültigen Folge-IDs. Wo IDs existieren, wechseln sie zwischen Projekt, Assembly und Generation. Paket 02 muss deshalb einen kanonischen Handoff-Vertrag definieren.

### C – `complete`/`partial`/`empty` ist nicht durchgängig belastbar

Falsche oder zu starke Completeness-Sätze sind gefährlicher als sichtbare Fehler: Ein Agent unterlässt dann die Gegenprobe. Paket 03 muss die Semantik zentralisieren und die Toolbeschreibungen daran ausrichten.

### D – Defaultwerte sind für Menschen plausibel, aber für Agenten riskant

Default-50, Test-Ranking, Root-Präfixe, Diagnose-Samples und Artefaktdateien führen dazu, dass die formal erste Seite nicht die fachlich wichtigste Seite ist. Defaults und Ranking müssen aus Agentenaufgaben abgeleitet werden.

### E – Composite-Tools verdoppeln nicht automatisch den Nutzen

`get_feature_context`, Assembly-Context und Health/Resources liefern viel in einem Call, aber ohne gemeinsame Budgets und Hints entsteht doppelte Tokenlast oder falsche Vollständigkeit. Composite-Verträge müssen eine klare Sufficiency- und Drilldown-Strategie haben.

### F – Projekt und Assembly sind unterschiedliche Analyseprodukte

Assembly-Dekompilat ist statisch und wertvoll, aber nicht identisch mit Source-Solution-Analyse. `partial`, `not_decidable`, Diagnostics, BCL-Filter und fehlende Consumer-Kontexte müssen sichtbar bleiben, statt Projektsemantik zu imitieren.

## Positive Baseline

Folgende Fähigkeiten sollen ausdrücklich erhalten bleiben:

- `report_observability_feedback` akzeptiert korrekt markiertes Feedback ohne Target-Vertrag.
- `resource-rules` liefert bei bekanntem, URL-kodiertem `projectRoot` eine kompakte und brauchbare Policy-Zusammenfassung.
- `get_symbol_body` ist mit eindeutigen `T:`-/`M:`-IDs und begrenzten Body-Fenstern gut nutzbar.
- `get_violations` ist für gezielte C#-Dateiscopes klein und praktisch.
- `metrics_tree` ist als kompakter erster Größen-/Komplexitäts-Drilldown brauchbar.
- Projekt-Happy-Paths vieler Symbol- und Navigations-Tools funktionieren mit FQN, DocCommentId oder bewusst gesetzten Limits.
- Assemblys werden metadata-/decompilation-basiert und read-only untersucht; sie werden nicht ausgeführt.

## Nicht als eigene Probleme zählen

- Dass Assemblies ohne Consumer-Projekt nicht die tatsächliche Laufzeit-Anwendbarkeit einer Extension beweisen können (`not_decidable` ist fachlich korrekt).
- Normale Leermengen für nicht existierende Symbole, sofern sie klar als Leermenge und nicht als vollständige globale Negativaussage ausgegeben werden.
- Hostseitige Einschränkungen, wenn `structuredContent` korrekt vorhanden ist, aber die untersuchte Agentenoberfläche nur Text rendert; dafür braucht es eine Produktentscheidung zur sichtbaren Agentenoberfläche.
- Reine Markdown-Nummerierungs- oder kosmetische Darstellungsfehler ohne falsche Agentenentscheidung.

## Synthese-Status

- 45/45 Audit-Dateien erfasst.
- 0 Findings als endgültig verworfen.
- `keep` nur für klar brauchbare Basispfade; `document` für begrenzte oder absichtlich eingeschränkte Fähigkeiten.
- Die Matrix ist eine vorläufige Erstklassifikation. Paketgrenzen und öffentliche Vertragsänderungen brauchen Nutzerentscheidung vor der Umsetzung.
