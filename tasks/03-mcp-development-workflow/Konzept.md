---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
execution_mode: autonomous
rules_dir: .agents/rules
last_updated: 2026-09-09
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

## Verbindliche Leitentscheidung: harter Schnitt

Dieser Task ersetzt den bisherigen MCP-Entwicklungsworkflow innerhalb seines
Scopes vollständig. Es gibt keinen Migrationspfad, keine Übergangsphase und
keine Kompatibilität zu vorherigen Features, Request-Schemas, Response-Feldern,
IDs, Konfigurationspfaden, Statuswerten oder Fallbacks.

Die Umsetzung darf und muss deshalb veraltete Runtime-Pfade, Adapter, Aliase,
Dual-Read-/Dual-Write-Logik, tote Modelle, Registrierungen, Fixtures und Tests
entfernen. Dasselbe gilt für aktive Dokumentation: `Docs/`, `README.md`,
MCP-Instructions, Toolbeschreibungen und `.agents/rules/` dürfen keine frühere
Vertragsvariante als aktuelle Möglichkeit, Beispiel oder Empfehlung stehen
lassen. Generierte Regeldateien werden aus ihrer Source of Truth neu erzeugt;
manuelle Parallelverträge sind nicht zulässig.

Historische Auditbefunde und abgeschlossene Task-Konzepte bleiben
read-only-Evidenz. Sie sind keine Runtime- oder Dokumentationsquelle und
werden nicht durch Kompatibilitätslogik geschützt. Wird eine historische
Aussage in einer aktiven Dokumentation wiederholt, muss die aktive Stelle
entfernt oder auf den neuen Vertrag korrigiert werden.

## Ziel und Problem

Vor einer Source-Änderung soll ein Agent belastbaren Kontext,
Produktionsimpact und statische Testkandidaten erhalten; danach soll er Lint
und Metriken ohne falsches Grün bewerten können. Der neue Vertrag muss diese
Schleife mit genau einer konsistenten Scope-, Status- und Completeness-Wahrheit
bilden. Alte, parallel weitergeführte Vertragsreste würden erneut
unterschiedliche Interpretationen ermöglichen und sind deshalb ein
Releasefehler.

## Primärer Agentennutzen

Der primäre Agentennutzen ist eine sichere, kopierbare Schleife:

`Kontext → Impact → statische Testkandidaten → Änderung → Violations/Metriken`

Jeder Schritt liefert nur den neuen Vertrag, eindeutig begrenzte Ergebnisse
und gültige Detail-Handoffs. Assemblyqualität bleibt der eigenständige Scope
von Task 04; daraus darf keine zweite Source-Workflow-Wahrheit entstehen.

## Source of Truth und betroffene Bereiche

- Der fachliche Zielvertrag sind die von Task 01 und Task 02 gelieferten
  `targetPath`-, Scope-, ID-, Snapshot-, Status- und Budgetsemantiken.
- Für die Workflow-Fachlichkeit sind die neuen Runtime-Modelle,
  Toolregistrierungen und deren Contract-/Integrationstests die primäre
  Implementierungsquelle.
- Die aktive Dokumentation muss exakt den implementierten Vertrag abbilden:
  `Docs/`, `README.md`, MCP-Instructions, Toolbeschreibungen,
  `.agents/rules/` und gegebenenfalls `ainetlinter-rules.json` samt daraus
  generierten Regeldateien.
- Betroffen sind `FeatureContext/*`,
  `SymbolGraph/GetImpactTool`, References, `TestContext/*`, Violations,
  Metrics, `Safeguard`, DeadCode, Duplicates, MagicValues, PatternDetect
  sowie alle zugehörigen Formatter, Registrierungen, Contract-, Wire-,
  Integration-, Dogfood- und Fixture-Artefakte.
- Auditmatrix und Verbesserungsrahmen werden nur zur Begründung und
  Befundprüfung gelesen. Sie führen keine alte API und keinen Erhaltungszwang
  in die neue Implementierung ein.

## Scope

- Einen gemeinsamen Source-Anker für Symbol, Datei sowie Produktions- und
  Testscope in Context, Impact, Testcontext, Violations und Metrics verwenden.
- Composite-Tools geben nur klar begrenzte Abschnitte aus, vermeiden doppelte
  Caller-/Body-Daten und verweisen mit Handoff auf neue Detailcalls.
- Impact trennt Produktions- und Testreferenzen. Git-/Change-Kontext ist eine
  eigene Capability und erzeugt bei fehlenden Daten keine vollständige
  Negativbehauptung.
- Testcontext benennt statische Kandidaten, Heuristik, Scope und Truncation;
  er behauptet nie Runtime-Coverage.
- Lint, Metriken und Quality-Gates verwenden denselben Scope und zeigen
  fehlende Regeln, nicht entscheidbare Teile und Partialität sichtbar.
- Die präzisionskritischen Auditbefunde werden korrigiert: scoped Safeguard,
  sichtbare ungeprüfte Patternkategorien, Kandidatenstatus/Confidence für
  Dead Code und Duplicates sowie korrekte Magic-Value-Kategorien.
- Alle durch den neuen Vertrag ersetzten Code-, Test-, Dokumentations- und
  Regelreste werden in demselben Task entfernt. Es bleibt keine zweite
  Vertragsoberfläche für vorherige Features bestehen.

## Muss-Kriterien

- Produktion, Tests, Diagnosen und Artefakte sind jeweils getrennt gezählt,
  gefiltert und gerankt.
- `get_feature_context`, `get_impact`, `get_test_context`, `get_violations`
  und `metrics_lookup` widersprechen sich für denselben neuen Anker nicht bei
  Scope, Lintstatus oder Vollständigkeit.
- Kein begrenztes oder teilweises Lint-, Pattern- oder Quality-Ergebnis
  behauptet „sauber“, „keine“ oder vollständige Löschbarkeit.
- Kandidaten enthalten Confidence, Evidenzgrenzen und sichere nächste Prüfung;
  sie enthalten keinen Löschauftrag.
- Die öffentliche Oberfläche akzeptiert ausschließlich den neuen Vertrag.
  Alte Parameter, Aliasfelder, Statusprojektionen, IDs, Konfigurationspfade
  und Response-Varianten werden weder gelesen noch erzeugt noch still
  umgedeutet.
- Entfernte Features hinterlassen keine ungenutzten Adapter, Serializer,
  Registrierungen, Optionsmodelle, Test-Fixtures, Sample-Payloads oder
  Dokumentationsfragmente in aktiven Pfaden.
- Aktive Docs, README und Regeln nennen ausschließlich den neuen Vertrag.
  Eine Migrations-, Deprecation- oder Kompatibilitätsanleitung wird nicht
  erstellt.
- Die Umsetzung enthält keine Feature-Flag- oder Fallback-Variante, mit der
  der alte Vertrag zur Laufzeit wieder aktiviert werden könnte.

## Messbare Akzeptanzkriterien

- Eine Fixture mit Produktion, Tests, generiertem Artefakt,
  Reflection-/DI-Grenzfall und einer echten Violation hat in allen fünf
  Workflowtools übereinstimmende Scope-Counts und erwartete Status.
- Jede der fünf Precision-Familien erhält mindestens einen False-Green- und
  einen `not_decidable`-/Partial-Wire-Test.
- Composite-Antworten bleiben unter 12 KiB, verdoppeln keinen Body/Caller und
  verweisen mit mindestens einer gültigen neuen ID auf den Detailcall.
- Ein repositoryweiter Abgleich der aktiven Runtime-, Test-, Docs-, README-
  und Regeloberfläche findet keinen alten Vertrag, keine Aliasroute und keine
  alte Beispielpayload. Die konkrete Entfernungsliste wird während der
  Umsetzung aus dem tatsächlich vorgefundenen Bestand abgeleitet; sie wird
  nicht als neue Kompatibilitätsinventur im Produkt verewigt.
- `tools/list`, StructuredContent, Formatter, Docs, README und aktive Regeln
  beschreiben dieselben Feldnamen, Statuswerte, Scopebegriffe und nächsten
  Schritte. Kein aktiver Text verweist auf einen nicht mehr implementierten
  Pfad.
- Alte Requests werden nicht als gültige Eingabe verarbeitet oder auf den
  neuen Vertrag umgebogen. Ein notwendiger Schutztest prüft ausschließlich
  die generische Ablehnung unbekannter oder nicht unterstützter Eingaben und
  führt keine alte API als Fixture weiter.

## Non-Goals

- Keine Migration, kein Adapter, keine Aliasunterstützung, kein Dual-Read,
  kein Dual-Write und keine befristete Rückwärtskompatibilität.
- Keine Deprecation-Phase, kein Migrationsleitfaden und keine Erhaltung alter
  README-, Docs- oder Regeltexte aus Gründen der Nutzerkompatibilität.
- Keine Runtime-Coverage, vollständige Reflection-/Generator-/DI-Analyse,
  neue Lintregeln, Targetmigration, Decompiler-Parität oder kosmetische
  Formatter-Arbeit ohne falsche Agentenentscheidung.
- Keine Änderung der historischen Auditquellen oder abgeschlossenen
  Task-Konzepte als Ersatz für die Bereinigung aktiver Oberflächen.
- Kein allgemeines Repository-Cleanup ohne Bezug zur Entfernung des alten
  MCP-Entwicklungsworkflows.

## Architektur- und Betriebsannahmen

Der gemeinsame Anker ist eine fachliche Projektion über vorhandene Scanner,
kein neues Analyse-Repository. Heuristiken bleiben konservativ; erwartete
Grenzen modellieren Status und Confidence statt Exceptions. Composites
koordinieren Payloads, ersetzen aber keine Detailtools.

Der Hard Cut wird atomar als neuer aktiver Vertrag ausgeliefert. Es gibt keine
Koexistenz zweier Vertragsgenerationen, keine Laufzeitumschaltung und keinen
Default, der einen entfernten Vertrag wiederherstellt. Fachliche
Implementierungsdetails innerhalb dieses Rahmens entscheidet der spätere
Orchestrator autonom.

## Fehler-, Fallback-, Ownership- und Lebenszeitsemantik

- Fehlende Regeln sind `not_configured`, Source-fremde Capability
  `unsupported`, dynamische Beweisgrenzen `not_decidable` mit Confidence.
- Ein ungültiger oder veralteter Request ist ein Vertragsfehler und wird nicht
  durch Raten, Fallback oder stilles Umformen repariert.
- Git-Diff ohne Änderungen ist nur ein leerer Diff-Scope, nicht „kein Impact“.
- Alle Teilabschnitte tragen ihren eigenen Status; ein verfügbarer Abschnitt
  bleibt nutzbar, wenn ein anderer fehlt.
- IDs und Snapshotverhalten stammen aus Task 02. Sie dürfen nicht durch eine
  parallel erhaltene alte ID- oder Generationsemantik ergänzt werden.
- Neue Workflow-Objekte besitzen keine Lebensdauer- oder Ownership-Semantik,
  die auf entfernte Legacy-Sessions oder Konfigurationsdefaults verweist.

## Abhängigkeiten

Task 01 liefert den einheitlichen Targetvertrag, Task 02 den ID-, Scope-,
Status-, Snapshot- und Budgetvertrag. Task 04 verwendet für relevante
Assembly-Capabilities dieselben Handoff-Grundsätze, darf aber die Source-
Semantik dieses Tasks nicht verändern. Abhängigkeiten sind nur dann gültig,
wenn ihre aktive Vertragsoberfläche ebenfalls frei von den hier entfernten
Altpfaden ist; eine Kompatibilitätsschicht zwischen den Tasks ist nicht
zulässig.

## Risiken und Alternativen

Ein harter Schnitt kann externe Nutzer oder lokale Skripte brechen. Das ist
eine bewusste Folge und kein Anlass für einen Adapter; die aktive Dokumentation
beschreibt nur den neuen Einstieg. Das größere Risiko wäre, alte Fragmente als
scheinbar harmlose Fallbacks zu behalten und dadurch wieder mehrere Wahrheiten
zu erzeugen.

Ein „einfacher Gesamtscore“ verschleiert Scope-Fehler und darf nie die einzige
Wahrheit sein. Zu aggressive konservative Filter können Recall senken, müssen
aber als Begrenzung sichtbar sein. Ein vollständiger Heuristik-Umbau ist nicht
releasefähig und bleibt außerhalb des Schnitts.

Verworfene Alternativen:

- Nur Formattertexte ändern: verhindert keine falschen
  StructuredContent-Entscheidungen und lässt alte Runtimepfade bestehen.
- Alten und neuen Vertrag parallel bedienen: verletzt den Hard Cut und erzeugt
  erneut divergierende Status-, ID- und Scope-Wahrheiten.
- Alle Quality-Tools in diesem Task neu erfinden: große, schlecht testbare
  Sammlung ohne Bezug zur eigentlichen Konsistenzforderung.
- Runtime-Coverage emulieren: fachlich unhaltbar.

## Konkrete Agenten-Szenarien

| Fall | Call / muss enthalten / darf nicht behaupten | Nächster Schritt |
| --- | --- | --- |
| Änderung an Symbol | Context liefert den neuen Anker; Impact trennt Prod/Test; Testcontext nennt statische Kandidaten; nach Änderung Violations und Metrics auf gleichem Scope. | Neue Detail-ID prüfen, ändern, danach gezielt linten. |
| Große Menge | Impact/Violations zeigen getrennte Counts, Ranking und Kürzungsgrund. | Scope/Richtung verfeinern, statt „keine weiteren“ anzunehmen. |
| Fehlende Regeln | Navigation/Impact ist nutzbar; Lintabschnitt ist `not_configured`. | Regel bereitstellen oder Linturteil aussetzen. |
| Alter Einstieg | Der Request wird als nicht unterstützte/ungültige Eingabe beendet; kein Fallback und kein Umformen. | Ausschließlich den dokumentierten neuen Einstieg verwenden. |
| Aktiver Alttext | Repositoryprüfung findet eine veraltete Beschreibung, ein Beispiel oder eine Regel. | Text entfernen oder auf den implementierten neuen Vertrag korrigieren; keine Migrationsanleitung ergänzen. |

## Token- und Antwortbudget-Annahmen

Rahmenlimits gelten. Featurecontext priorisiert Ziel, Status und höchstens
einen repräsentativen Befund pro Abschnitt; vollständige Caller, Bodies und
Viollisten sind Detailcalls. Diagnostics dürfen keine Produktionsbefunde
verdrängen. Das Budget darf nicht durch parallele alte und neue Payloads
verbraucht werden.

## Erforderliche Dokumentationsänderungen

- `Docs/agent-api.md`, `Docs/integration.md` und alle betroffenen MCP-
  Referenzen werden auf den neuen Ablauf mit Detailcalls, Scope-Labels,
  Candidate/Confidence und ehrlichen Lintstatus aktualisiert.
- `README.md` wird bereinigt, wenn es den betroffenen MCP-Einstieg oder die
  entfernten Features erwähnt; alte Beispiele werden gelöscht, nicht als
  Migration erklärt.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` und alle weiteren betroffenen
  aktiven Regeln werden synchronisiert. Die generierte
  `.agents/rules/AiNetLinter.mdc` wird nur über ihre vorgesehene Source of
  Truth aktualisiert.
- `ainetlinter-rules.json` wird nur geändert, wenn der neue Vertrag oder die
  Regeldefinition es tatsächlich erfordert; danach erfolgt der vorgeschriebene
  Sync der generierten Agentenregeln.
- Veraltete Dokumente, Beispiele, Snippets und aktive Regelpassagen werden
  entfernt. Es wird kein separater Legacy- oder Migrationsabschnitt angelegt.

## Verifikation

Die Umsetzung verwendet für C#-Semantik den MCP-first-Workflow und prüft
gezielt Impact, Violations, Hotspots und vor dem Abschluss den passenden
Safeguard. Für Produktions- oder Testcodeänderungen sind verbindlich:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Zusätzlich erforderlich sind Paritäts-, Precision-, Scope-, Partial-, Empty-
und Composite-Wire-Tests sowie ein frischer MCP-Workflow-Dogfood-Nachweis.
Die aktive Oberfläche wird mit gezielter Text-/Dateisuche gegen die während
der Umsetzung ermittelte Entfernungsliste geprüft. `tools/list`,
StructuredContent, Formatter und Dokumentation werden auf Schema- und
Statusgleichheit abgeglichen.

Bei ausschließlich Markdown-, Dokumentations- oder Agenteninfrastruktur-
änderungen genügen relevante Referenzprüfungen, `git diff --check` und eine
sachliche Diff-Prüfung; sobald Produktions- oder Testcode betroffen ist,
gelten Build und beide vollständigen Nicht-Stress-Suiten.

## Release-Gate

Der Release ist blockiert, sobald einer der folgenden Befunde verbleibt:

- alter Request, alte Response, alte ID-, Status- oder Config-Route wird noch
  akzeptiert, erzeugt, registriert oder still umgedeutet;
- Legacy-Code, Adapter, tote Modelle, Fixtures, Sample-Payloads oder aktive
  Docs-/README-/Regelreste bilden weiterhin eine zweite Vertragsoberfläche;
- ein begrenztes Ergebnis behauptet vollständige Sauberkeit, globale
  Negativität oder Löschbarkeit ohne Confidence;
- Scope, Lintstatus, Completeness oder Handoff widersprechen sich zwischen den
  fünf Workflowtools;
- Build, vorgeschriebene Nicht-Stress-Suiten, Wire-/Dogfood-Prüfung,
  Dokumentationsabgleich oder Diff-Gate sind nicht grün.

Ein reproduzierbares False Green, Scope-Leak im Safeguard oder eine
Löschbehauptung ohne Confidence blockiert den Release auch bei ansonsten
grünen Tests.

## Nachgelagerte Beobachtung

Nach dem Release wird an einer echten Änderung bewertet, ob die konservativen
Kandidaten genügend Nutzen liefern. Ein Recall-Ausbau ist ein separater,
evidenzbasierter Auftrag und darf nicht als nachträgliche Kompatibilitäts-
oder Restarbeiten in diesen Hard-Cut-Task zurückfließen.
