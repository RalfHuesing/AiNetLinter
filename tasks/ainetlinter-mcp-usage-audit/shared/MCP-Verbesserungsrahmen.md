# Gemeinsamer Rahmen für die AiNetLinter-MCP-Tasks

Stand: 2026-09-07

Dieses Dokument enthält nur Regeln und Entscheidungen, die von mehreren
MCP-Release-Tasks genutzt werden. Die Befundmatrix liegt daneben unter
`tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md`; die ursprünglichen
Audit-Findings bleiben die read-only Evidenzquelle.

## Lieferprinzip

Ein Task-Ordner entspricht genau einem zusammenhängenden, testbaren
Release-Schnitt. Ein Task darf mehrere technische Teilbereiche enthalten,
wenn sie denselben externen Vertrag, dasselbe Abnahme-Gate und denselben
Agentennutzen haben. Ein einzelnes Audit-Finding wird nicht automatisch zu
einem eigenen Task.

Nach jedem grünen Task entstehen ein eigener deutscher Conventional-Commit,
ein fokussierter MCP-Nachweis und ein testbarer Release-Stand. Der nächste
Task beginnt erst danach. Ein rotes Gate stoppt die Kette.

## Gemeinsame Architekturentscheidungen

- MCP-Tools bleiben explizit registriert; kein Plugin-System, keine Reflection-
  Registrierung und kein generischer Command-Bus.
- Es gibt keinen neuen DI-Container und keine künstliche gemeinsame Session-
  API für Source-Solutions und dekompilierte Assemblies.
- Project-Leases und Assembly-Leases bleiben getrennte Lifecycle-Subsysteme.
- Gemeinsame Status-, Capability-, Identitäts- und Completeness-Verträge werden
  zentralisiert; Scanner, Roslyn-Aufrufe und fachliche Heuristiken bleiben in
  ihren Fachbereichen.
- Text und `StructuredContent` werden aus derselben fachlichen Aggregation
  projiziert. Kritische Folgeinformationen dürfen nicht nur im Freitext
  stehen.
- Analyse bleibt read-only. Assemblies werden metadata-/decompilation-basiert
  untersucht und niemals ausgeführt.

## Gemeinsame Agentenwahrheit

Eine Antwort muss unterscheiden können zwischen:

- leerer Ergebnismenge bei aktiver, vollständiger Analyse;
- vollständiger oder begrenzter Antwort;
- `unsupported`, `not_configured`, `not_decidable` und echtem Fehler;
- Source- und Decompiled-Herkunft;
- gültiger Folge-ID und bloß menschenlesbarer Zeile oder Datei.

Ein begrenzter Ausschnitt darf keine globale Negativaussage erzeugen. Für
große Mengen gelten kleine, fachlich gerankte Defaults, sichtbare Counts und
`completeness`-/`truncatedBy`-Informationen. Ein Continuation-Token ist nur
für eine sinnvoll geordnete Restmenge vorgesehen; bevorzugt werden Scope-,
Richtungs- und Detail-Drilldowns.

## Befund-Disposition

| Disposition | Bedeutung |
| --- | --- |
| `fix` | reproduzierbarer Produktfehler oder systemische Agenten-Reibung |
| `document` | Verhalten bleibt bestehen, wird aber korrekt beschrieben oder begrenzt |
| `defer` | sinnvoll, aber abhängig oder nachrangig |
| `keep` | brauchbarer Pfad, der als Regression geschützt wird |
| `discard` | nicht belastbar, redundant oder außerhalb des Produktziels |

Severity bleibt eine Eigenschaft des Audits. Die Task-Priorität richtet sich
zusätzlich nach Agentenwirkung, Reichweite, Abhängigkeiten sowie Aufwand und
Regressionsrisiko.

## Positive Baseline

Diese Pfade werden in passenden Tasks als Regression geschützt:

- `report_observability_feedback` bleibt read-only und nicht zielgebunden;
- bekannte eindeutige IDs funktionieren bei `get_symbol_body` mit begrenztem
  Body-Fenster;
- gezieltes C#-Scoping bei `get_violations` bleibt kompakt;
- `metrics_tree` liefert einen brauchbaren kompakten Drilldown;
- bestehende Projekt-Happy-Paths der Symbolnavigation bleiben erhalten;
- Assemblys bleiben metadata-/decompilation-basiert und read-only.

## Verifikations-Gate pro Task

- passende Fast- und Integration-Tests während der Arbeit;
- vor Taskabschluss `dotnet build`;
- vollständiger Nicht-Stress-Lauf über `FastTests` und
  `IntegrationTests` gemäß `AGENTS.md`;
- fokussierter MCP-/Dogfood-Nachweis für die geänderte Toolkette;
- relevante Dokumentation, Toolbeschreibung, Schema und Agentenregeln gegen
  den implementierten Vertrag prüfen;
- `git diff --check`, gezieltes Staging und eigener Commit.

Der vollständige 45-Finding-Audit ist die Baseline, nicht der tägliche
Volltest. Nach jedem Task genügt ein fokussierter Audit. Nach dem Unified-
Target-Task erfolgt zusätzlich ein Vertrags-Scan; nach dem Assembly-Task der
vollständige Audit.

## Abbruch- und Blockerregeln

- Nach zwei erfolglosen Reparaturversuchen mit derselben Root Cause wird der
  Befund dokumentiert und der Task-Schnitt eingehalten.
- `not_decidable`, `unsupported` und fehlende externe Testvoraussetzungen sind
  fachliche Ergebnisse, die nicht durch Endlos-Retries wegimplementiert werden.
- Ein Live-Test zählt nur gegen eine frisch gebaute und gestartete
  Serverinstanz; stale Binaries, alte Sessions und falsche Targets werden vor
  der Bewertung ausgeschlossen.
- Ein rotes Live-Gate stoppt den Fortschritt. Ein nachgelagerter Task darf
  keinen früheren Vertragsfehler verdecken.
- Gemeinsame Dateien werden nicht parallel von mehreren schreibenden Agenten
  bearbeitet. Vor einem Commit werden Baseline, Index und eigener Diff erneut
  geprüft.

## Dokumentationsprinzip

Dokumentation gehört zum Task, der den Vertrag ändert. Eine gemeinsame
Restdokumentationsphase wird nicht als eigener Task angelegt. Dieses Dokument
und die Befundmatrix werden nur referenziert, nicht in jedem Task dupliziert.
