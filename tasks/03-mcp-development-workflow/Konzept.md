---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
execution_mode: autonomous
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions: []
depends_on:
  - tasks/01-mcp-unified-analysis-target/Konzept.md
  - tasks/02-mcp-agent-handoff/Konzept.md
  - tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md
  - tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md
related_tasks:
  - tasks/04-mcp-assembly-quality/Konzept.md
supersedes: null
---

# Task 03: Sicherer MCP-Entwicklungsworkflow

## Ziel und Problem

Vor einer Source-Änderung soll ein Agent belastbaren Kontext, Produktionsimpact
und statische Testkandidaten erhalten; danach soll er Lint und Metriken ohne
falsches Grün bewerten können. Heute verwenden Impact, Tests, Violations,
Metriken und Quality-Gates unterschiedliche Scope- und Completeness-Wahrheiten.

## Primärer Agentennutzen

Der primäre Agentennutzen ist eine sichere Schleife statt einer großen
Composite-Antwort: Kontext → Impact → Testkandidaten → Änderung →
Violations/Metriken. Dies ist ein Source-Release; Assemblyqualität bleibt
explizit Task 04.

## Source of Truth und betroffene Bereiche

- Audit: Befundmatrix-Pakete 04 und 06 sowie `combo-impact-lint` und
  `combo-mcp-first-context`.
- Rahmen: Handoff-, Scope-, Status- und Budgetsemantik.
- Code: `FeatureContext/*`, `SymbolGraph/GetImpactTool`, References,
  `TestContext/*`, Violations, Metrics, `Safeguard`, DeadCode, Duplicates,
  MagicValues, PatternDetect und ihre Contract-/Integrationstests.

## Scope

- Einen gemeinsamen Source-Anker für Symbol, Datei und Produktions-/Testscope
  in Context, Impact, Testcontext, Violations und Metrics verwenden.
- Composite-Tools geben nur klar begrenzte Abschnitte aus, vermeiden doppelte
  Caller/Body-Daten und verweisen mit Handoff auf Details.
- Impact zeigt Produktions- und Testreferenzen getrennt, Git-/Change-Kontext
  als eigene Capability und keine vollständige Negativbehauptung bei Grenzen.
- Testcontext benennt „statische Kandidaten“, Heuristik, Scope und
  Truncation; nie Runtime-Coverage.
- Lint/Metriken und Quality-Gates verwenden denselben Scope und zeigen
  fehlende Regeln, nicht entscheidbare Teile und Partialität sichtbar.
- Die präzisionskritischen Auditbefunde werden korrigiert: scoped safeguard,
  sichtbare ungeprüfte Patternkategorien, Kandidatenstatus/Confidence für
  Dead Code und Duplicates, korrekte Magic-Value-Kategorien.

## Muss-Kriterien

- Produktion, Tests, Diagnosen und Artefakte sind jeweils getrennt gezählt,
  gefiltert und gerankt.
- `get_feature_context`, `get_impact`, `get_test_context`,
  `get_violations` und `metrics_lookup` widersprechen sich für denselben
  Anker nicht bei Scope, Lintstatus oder Vollständigkeit.
- Kein begrenztes/teilweises Lint-, Pattern- oder Quality-Ergebnis behauptet
  „sauber“, „keine“ oder vollständige Löschbarkeit.
- Kandidaten enthalten Confidence, Evidenzgrenzen und sichere nächste Prüfung;
  sie enthalten keinen Löschauftrag.

## Messbare Akzeptanzkriterien

- Eine Fixture mit Produktion, Tests, generiertem Artefakt, Reflection-/DI-
  Grenzfall und einer echten Violation hat in allen fünf Workflowtools
  übereinstimmende Scope-Counts und erwartete Status.
- Jede der fünf Precision-Familien erhält mindestens einen False-Green- und
  einen `not_decidable`-/partial-Wire-Test.
- Composite-Antworten bleiben unter 12 KiB, verdoppeln keinen Body/Caller und
  verweisen mit mindestens einer gültigen ID auf den Detailcall.

## Non-Goals

Keine Runtime-Coverage, vollständige Reflection-/Generator-/DI-Analyse, neue
Lintregeln, Targetmigration, Decompiler-Parität oder kosmetische Formatter-
Arbeit ohne falsche Agentenentscheidung.

## Architekturannahmen

Der gemeinsame Anker ist eine fachliche Projektion über vorhandene Scanner,
kein neues Analyse-Repository. Heuristiken bleiben konservativ; erwartete
Grenzen modellieren Status/Confidence statt Exceptions. Composites koordinieren
Payloads, ersetzen aber keine Detailtools.

## Fehler-, Fallback- und Lebenszeitsemantik

Fehlende Regeln sind `not_configured`, Source-fremde Capability
`unsupported`, dynamische Beweisgrenzen `not_decidable` mit Confidence.
Git-Diff ohne Änderungen ist nur ein leerer Diff-Scope, nicht „kein Impact“.
Alle Teilabschnitte tragen eigenen Status; ein verfügbarer Abschnitt bleibt
nutzbar, wenn ein anderer fehlt. IDs und Snapshotverhalten stammen aus Task 02.

## Abhängigkeiten

Task 01 liefert Target/Capabilities, Task 02 den ID-/Scope-/Budgetvertrag.
Task 04 darf diese Source-Semantik nicht ändern, sondern verwendet nur den
gemeinsamen Rahmen für relevante Assembly-Capabilities.

## Risiken und Sackgassen

Ein „einfacher Gesamtscore“ verschleiert Scope-Fehler; er darf nie die einzige
Wahrheit sein. Zu aggressive konservative Filter können Recall senken, müssen
dann aber als Begrenzung sichtbar sein. Ein vollständiger Heuristik-Umbau ist
nicht releasefähig und bleibt außerhalb des Schnitts.

## Alternativen mit Konsequenzen

- Nur Formattertexte ändern: verhindert keine falschen StructuredContent-
  Entscheidungen.
- Alle Quality-Tools im Task neu erfinden: große, schlecht testbare Sammlung.
- Runtime-Coverage emulieren: fachlich unhaltbar; verworfen.

## Konkrete Agenten-Szenarien

| Fall | Call / muss enthalten / darf nicht behaupten | Nächster Schritt |
| --- | --- | --- |
| B: Änderung an Symbol | Context liefert Anker; Impact trennt Prod/Test; Testcontext nennt statische Kandidaten; nach Änderung Violations und Metrics auf gleichem Scope. | Detail-ID prüfen, ändern, danach gezielt linten. |
| C: große Menge | Impact/Violations zeigen getrennte Counts, Ranking und Kürzungsgrund. | Scope/Richtung verfeinern, statt „keine weiteren“ anzunehmen. |
| D: fehlende Regeln | Navigation/Impact ist nutzbar; Lintabschnitt ist `not_configured`. | Regel bereitstellen oder Linturteil aussetzen. |

## Token-/Antwortbudget-Annahmen

Rahmenlimits gelten. Featurecontext priorisiert Ziel, Status und höchstens
einen repräsentativen Befund pro Abschnitt; vollständige Caller, Bodies und
Viollisten sind Detailcalls. Diagnostics dürfen keine Produktionsbefunde
verdrängen.

## Verifikation

Paritäts-, Precision-, Scope-, Partial-, Empty- und Composite-Wire-Tests,
frischer MCP-Workflow-Dogfood-Nachweis, Build, beide Nicht-Stress-Suiten,
Dokumentationsabgleich und Diff-Gate.

## Dokumentationsbedarf

Agent API erklärt statische Testkandidaten, Scope-Labels, Candidate/Confidence
und warum Lint ohne Regeln keine Aussage liefert; MCP-Instructions nennen die
Schleife mit Detailcalls statt eines All-in-one-Rezepts.

## Release-Gate

Ein reproduzierbares False Green, Scope-Leak im safeguard oder eine
Löschbehauptung ohne Confidence blockiert den Release, auch bei grünen Tests.

## Nächste fachliche Entscheidung

Nach Release an einer echten Änderung bewerten, ob die konservativen
Kandidaten genügend Nutzen liefern; Recall-Ausbau ist dann ein separater,
evidenzbasierter Auftrag.
