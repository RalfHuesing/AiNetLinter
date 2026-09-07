---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions:
  - Soll dieser Task nach dem grünen Handoff-Release freigegeben werden?
depends_on:
  - tasks/01-mcp-agent-handoff/Konzept.md
  - tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md
  - tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md
related_tasks:
  - tasks/03-mcp-unified-analysis-target
  - tasks/04-mcp-assembly-quality
supersedes: null
---

# Task 02: MCP-Development-Workflow

## Ziel und Problem

Die häufigste Agentenschleife soll belastbar werden:

`Kontext vor Änderung → Impact/Tests vor Änderung → Lint/Metriken nach Änderung`

Ein Agent darf dabei nicht Testtreffer als Produktionsimpact, begrenzte
Violation-Ergebnisse als Entwarnung oder heuristische Kandidaten als direkte
Lösch- bzw. Refactoring-Anweisung interpretieren.

## Source of Truth und betroffene Bereiche

- Befunde und Prioritätsbegründung:
  `tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md`
- Gemeinsame Status-/ID-/Completeness-Regeln:
  `tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md`
- Feature-Kontext: `Tools/FeatureContext/*`
- Impact und Referenzen: `Tools/SymbolGraph/GetImpactTool`,
  `FindReferencesTool`, `DiffImpactAnalyzer`
- Violations und Metriken: `Tools/Analysis/GetViolationsTool`,
  `Tools/MetricsLookup/*`, `Metrics*`
- Testzuordnung: `Tools/TestContext/*`, `TestCoverageScanner`
- Präzisions- und Quality-Gates: `Tools/DeadCode/*`,
  `DuplicateDetection/*`, `MagicValues/*`, `PatternDetect/*`,
  `Safeguard/*`
- Contract-, Fast- und Integrationstests der genannten Toolfamilien

## Scope

### Gemeinsame Agentenschleife

- dieselbe fachliche Einheit für Symbol, Datei und Scope in Impact, Lint und
  Metrics;
- Produktions-/Test-Scope deterministisch und sichtbar unterscheiden;
- Score, Top-Befunde und Completeness auf denselben Scope anwenden;
- Partial-Typen, Testheuristik und Lintstatus ehrlich darstellen;
- Composite-Tools liefern klare Drilldown-Hints und laden Informationen nicht
  mehrfach ohne Nutzen;
- statische Testzuordnung klar von Runtime-Coverage trennen;
- ein `VIOLATION`-Metrikstatus darf nicht gleichzeitig als vollständige
  Lint-Entwarnung erscheinen.

### Sicherheitsrelevante Präzisionsfixes

- `safeguard` begrenzt scoped Top-Befunde tatsächlich auf den angeforderten
  Scope;
- `pattern_detect` macht leere Kategorien sichtbar oder kennzeichnet sie als
  nicht geprüft;
- `find_dead_code` weist Confidence, Reflection-/DI-/Routing-Grenzen und den
  Status „Kandidat, kein Löschauftrag“ aus;
- `find_magic_values` trennt Zahlen-, String- und Security-Kategorien
  fachlich korrekt;
- `find_duplicates` kennzeichnet Scope und Kandidatenstatus und filtert
  Test-/Artefaktfluten;
- `get_feature_context`, `get_impact`, `get_violations` und `metrics_lookup`
  liefern für denselben Anker keine widersprüchliche Vollständigkeits- oder
  Violation-Wahrheit;
- Ranking bevorzugt nicht blind Tests, Diagnosezeilen oder die ersten N
  Treffer.

### Nicht enthaltene Heuristikneuentwicklung

Neue Rankingalgorithmen, vollständige Recall-Abdeckung für Reflection,
Source Generators und dynamische DI sowie kosmetische Formatterverbesserungen
werden nur aufgenommen, wenn ein reproduzierbarer Agentenschaden das
Release-Gate betrifft.

## Architektur- und Fehlersemantik

Task 02 nutzt die im Task 01 eingeführten Identifier-, Scope- und
Completeness-Verträge. Die fachlichen Heuristiken bleiben konservativ und
geben bei unentscheidbaren Fällen `not_decidable`, Confidence oder einen
Kandidatenstatus aus. Fehlende Daten werden nicht als globale Negativaussage
formuliert.

Erwartete Fehler werden strukturiert ausgewiesen. Exceptions bleiben echten
IO-, Prozess- oder exogenen Fehlern vorbehalten. Composite-Tools liefern
verfügbare Abschnitte weiter und markieren nur die nicht verfügbare Teil-
Capability als `unsupported`, `not_configured` oder `not_decidable`.

## Muss-Kriterien

- Produktions- und Testtreffer sind in Impact, Context und Ranking sichtbar
  getrennt.
- Scoped Quality-Gates zeigen nur scoped Top-Befunde.
- Testzuordnung behauptet keine Laufzeitabdeckung.
- Begrenzte oder partielle Analysen werden nicht als vollständige Entwarnung
  ausgegeben.
- Kandidatenlisten enthalten Confidence und keine uneingeschränkte
  Löschanweisung.
- Leere Pattern-Kategorien sind sichtbar oder ausdrücklich als ungeprüft
  markiert.
- `get_feature_context → get_impact → get_test_context → get_violations`
  bildet einen nachvollziehbaren Agentenworkflow.
- Die positive Baseline des gemeinsamen Rahmens bleibt grün.

## Non-Goals

- keine Runtime-Coverage-Engine;
- keine vollständige semantische Lösung dynamischer Reflection-/DI-/Generator-
  Fälle;
- keine Änderung der eigentlichen Lintregeln ohne separaten Auftrag;
- keine öffentliche Target-Vertragsänderung; diese gehört in Task 03;
- keine Assembly-Parität; diese gehört in Task 04.

## Risiken und Alternativen

Eine konservative Heuristik kann Recall verlieren. Das ist für gefährliche
Entscheidungen akzeptabler als ein überzeugend aussehender False Positive-
oder False Negative-Pfad. Jeder reduzierte Recall wird als Grenze oder
`not_decidable` sichtbar gemacht.

Eine vollständige Überarbeitung aller Heuristiken wäre größer und schwerer
zu testen. Der Task beschränkt sich daher auf die Befunde, die konkrete
Lösch-, Refactoring- oder Qualitätsentscheidungen verfälschen.

## Verifikation und Dokumentation

- repräsentative Source-Proben mit Produktions- und Testtreffern;
- Impact-/Lint-/Metrik-Paritätsproben für denselben Anker;
- Scope-, Truncation-, Partial-, Empty- und `not_decidable`-Tests;
- Tests für `safeguard`, Pattern-, Dead-Code-, Magic-Value- und Duplicate-
  Grenzen;
- `Docs/agent-api.md`, `Docs/integration.md` und MCP-Instructions anpassen,
  sofern der öffentliche Vertrag betroffen ist;
- `dotnet build` und beide vollständigen Nicht-Stress-Testläufe vor Abschluss;
- fokussierter MCP-Live-Nachweis am realen Entwicklungsworkflow.

## Abnahme / Release-Gate

Der Task ist releasefähig, wenn ein Agent vor einer Änderung belastbaren
Kontext, Impact und statische Testkandidaten erhält und nach einer Änderung
Lint-/Metrikergebnisse ohne falsches Grün bewerten kann. Ein fachlich
irreführendes Quality-Gate blockiert den Release unabhängig davon, ob die
technischen Tests grün sind.
