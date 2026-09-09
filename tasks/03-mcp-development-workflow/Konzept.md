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

## Verbindliche Leitentscheidung: atomarer Hard Cut

Dieser Task liefert den einzigen aktiven MCP-Entwicklungsworkflow für
Source-Analyse aus. Es gibt keinen Migrationspfad, keine Übergangsphase und
keine Kompatibilität zu einer vorherigen Vertragsgeneration. Alte Requests,
Response-Felder, IDs, Statuswerte, Konfigurationspfade, Aliase, Adapter,
Fallbacks, Dual-Read-/Dual-Write-Logik und Laufzeitumschaltungen werden weder
akzeptiert, erzeugt noch still umgedeutet.

Der Schnitt umfasst Runtime, öffentliche MCP-Schemas, StructuredContent,
Formatter, Registrierungen, Tests, Fixtures, Sample-Payloads, aktive
Dokumentation, README, MCP-Instructions und aktive Agentenregeln. Entfernte
Verträge hinterlassen keine toten Modelle, Parserzweige, Serializer,
Optionsmodelle oder Feature-Flags, die den alten Vertrag reaktivieren könnten.
Generierte Regeldateien werden ausschließlich aus ihrer vorgesehenen Source of
Truth erzeugt; ein manuell gepflegter Parallelvertrag ist unzulässig.

Historische Auditunterlagen und abgeschlossene Task-Konzepte sind read-only
Evidenz. Sie dürfen alte Begriffe enthalten, sind aber weder Produktvertrag
noch aktive Dokumentation und rechtfertigen keine Kompatibilitätslogik.

## Ziel und primärer Agentennutzen

Vor einer Source-Änderung erhält ein Agent belastbaren Kontext,
Produktionsimpact und statische Testkandidaten. Danach bewertet er Lint,
Metriken und präzisionskritische Quality-Signale ohne falsches Grün. Die
Schleife besitzt genau eine Scope-, Snapshot-, Status-, Completeness- und
Handoff-Wahrheit:

`Kontext → Impact → statische Testkandidaten → Änderung → Violations/Metriken → gezielte Quality-Prüfung`

Ein sichtbarer Treffer ist nur dann ein Folgeinput, wenn er als solcher
gekennzeichnet und für konkretes Target und Snapshot gültig ist. Statische
Kandidaten sind niemals Runtime-, Coverage- oder Löschbeweise. Composites
beschleunigen die Orientierung, ersetzen aber keinen erforderlichen Detailcall.

## Verbindliche Entscheidungen und Ownership

- Task 02 gilt fachlich als abgeschlossen. Jeder bei Task 03 noch vorgefundene
  Task-02-Vertragsrest wird vollständig in diesem Task entfernt oder auf den
  neuen Vertrag gebracht. Er wird nicht zurückdelegiert und erzeugt keinen
  zweiten Release.
- Task 01 und 02 liefern unveränderliche Rahmenbedingungen für `targetPath`,
  Origin, ID, Snapshot, Status, Completeness, Budget und Handoff. Task 03 darf
  ihre Restimplementierung abschließen, aber keinen zweiten Rahmen einführen.
- Task 03 besitzt die Source-Fachsemantik der Workflow- und Precision-Tools.
  Gemeinsame Infrastruktur darf für beide Origins bereinigt werden, wenn dies
  für die Task-02-Restübernahme notwendig ist.
- Task 04 besitzt ausschließlich Assembly-Fachqualität: Decompiled-Katalog,
  -Navigation, Decompilerdiagnostik, Assembly-Snapshotqualität, Lease,
  Lifecycle und Consumeraussagen. Task 03 verändert keine dieser
  Assembly-Fachpayloads; sie erhalten Schutztests gegen Regression.
- `Docs/` einschließlich `Docs/ROADMAP.md`, `README.md`, MCP-Resources,
  Server-Instructions, Toolbeschreibungen und `.agents/rules/` sind aktiv und
  keine historischen Ausnahmen.

## Source of Truth und Vertragskatalog

Vor der ersten Runtime-Änderung wird für die gesamte betroffene Oberfläche ein
einziger maschinenprüfbarer Vertragskatalog festgelegt. Er ist die normative
Quelle. Runtime, Registrierung, `tools/list`, StructuredContent, Formatter,
Resources, Dokumentation und Tests werden daraus abgeleitet oder dagegen
geprüft. Vorhandene Code- oder Testformen sind keine konkurrierende Source of
Truth.

Der Katalog enthält je Toolfamilie:

- kanonische Required-/Optional-Inputs mit Typ, Default, Cap und Ausschlüssen;
- verbotene entfernte Inputs, Aliasnamen und Konfigurationspfade;
- StructuredContent-Wurzel, `navigation` und tool-spezifische Payloadpfade;
- Status-, Error-, Completeness-, Confidence- und `next`-Semantik je
  Ergebnisfeld oder Composite-Abschnitt;
- Scope, getrennte Counts, Ranking, Truncation und Handoff-Fähigkeit;
- die zulässige Textprojektion. Text darf IDs, Counts, Status und den für einen
  Folgecall nötigen nächsten Schritt weder verändern noch verschweigen.

| Familie | Tooloberfläche | Task-03-Verantwortung |
| --- | --- | --- |
| Workflow | `get_feature_context`, `get_impact`, `get_test_context`, `get_violations`, `metrics_lookup` | Source vollständig |
| Precision | `safeguard`, `pattern_detect`, `find_dead_code`, `find_duplicates`, `find_magic_values` | Source vollständig |
| Gemeinsamer Rahmen | Registrierungen, Target-/Snapshot-/Handoff-Projektion, Formatter, Resources, Server-Instructions | Task-02-Reste und Source-Projektion |

Ein bestehendes tool-spezifisches Feld bleibt nur erhalten, wenn es im Katalog
als kanonisch bestätigt ist, genau eine Bedeutung hat und dem Task-01/02-
Rahmen entspricht. Andernfalls wird es entfernt, niemals als Alias geführt.

## Scope

- Alle Workflowtools verwenden für Source dieselbe interne Scope-Projektion:
  `targetPath`, geprüfter Snapshot, Produktions-/Test-/Artefaktklassifikation,
  Datei-/Namespace-/Symbolbezug und gegebenenfalls Richtung. Das ist kein
  zweiter öffentlicher Targetinput.
- `get_feature_context` liefert begrenzte, nicht duplizierte Abschnitte für
  Deklaration, Impact, statische Tests, Violations und Metriken.
- `get_impact` trennt Symbolimpact und Git-/Change-Kontext als fachlich
  getrennte Modi. Ein leerer Diff-Scope bedeutet nie „kein Impact im
  Repository“. Fehlende Git-Evidenz erzeugt keine vollständige Negativaussage.
- `get_test_context` liefert ausschließlich statische Testkandidaten samt
  Heuristik, Scope, Count, Truncation, Confidence, Evidenzgrenze und sicherem
  nächsten Schritt; nie Runtime-Coverage oder Testausführung.
- `get_violations`, `metrics_lookup` und `safeguard` verwenden denselben
  fachlichen Source-Scope. Fehlende oder ungültige Regeln können nicht als
  sauberes Lint- oder Quality-Ergebnis erscheinen.
- `safeguard` zeigt nur Violations und Remediation aus seinem geprüften Scope.
  Ein Score ersetzt weder Scope noch Abschnittsstatus.
- `pattern_detect` weist für jede angeforderte Kategorie aus, ob sie geprüft,
  leer, begrenzt, nicht konfiguriert oder nicht entscheidbar ist.
- `find_dead_code` und `find_duplicates` liefern Kandidaten statt
  Löschaufträgen. Reflection, DI, Generatoren und dynamische Auflösung werden
  als nachweisbare Evidenzgrenze behandelt.
- `find_magic_values` verwendet ausschließlich disjunkte, im Vertragskatalog
  festgelegte Kategorien. Kategorie, Evidenz, Scope und Empfehlung dürfen
  Security-, Localization-, Framework- und Config-Aussagen nicht vermischen.

## Status-, Fehler-, Snapshot- und Lebenszeitsemantik

Die gemeinsame `navigation` enthält für erreichbare zielgebundene Antworten
`target`, `origin`, `snapshot`, `capabilities`, `operationStatus`, `result`,
`completeness` und `next`. `complete`, `partial` und `truncated` gelten immer
für ein benanntes Ergebnisfeld oder einen Composite-Abschnitt, nie unpräzise
für eine Session.

| Fall | Verpflichtende Semantik |
| --- | --- |
| Vollständig geprüfte Leermenge | `operationStatus=ok`, Ergebnis `empty`, `totalCount=0`; keine Aussage außerhalb des Scopes. |
| Begrenzter bekannter Rest | `truncated` mit `totalCount`, `returnedCount`, `truncatedBy` und genau einem passenden `next`. |
| Fehlende Referenzen oder dynamische Grenze | betroffener Abschnitt `partial` oder `not_decidable`, mit Ursache, Confidence und sicherer Alternative. |
| Fehlende Regeln | nur Lint-/Quality-Capability `not_configured`; Navigation bleibt nutzbar. |
| Ungültige Regeln oder exogener Fehler | klarer Fehlercode, Ursache/Feldpfad und sicherer nächster Schritt; kein Default und kein stiller Retry. |
| Falsches Target oder Capability | `target_mismatch` beziehungsweise `unsupported`, nie `empty`. |
| Snapshotwechsel | `stale_snapshot` mit Wiederholungsanweisung, nie `symbol_not_found`. |
| Unbekannter, alter oder nichtkanonischer Input | `invalid_argument`; keine Aliasauflösung und kein Umformen. |

Workflow-DTOs besitzen keine Lease-, Session- oder Dateibesitzsemantik. Die
Registry bleibt Besitzerin der Project- und Assembly-Leases. IDs und
Continuations sind nur für gebundenes Target und Snapshot gültig; interne
Cache-Generationen werden niemals Agenteninput.

## Hard-Cut-Nachweis ohne falsche historische Treffer

Vor der Implementierung wird im Task-Verzeichnis eine read-only
Legacy-Entfernungsmatrix als Prüfgrundlage angelegt. Sie enthält alle konkret
vorgefundenen alten API-Namen, Response-Felder, ID- und Statuswerte,
Konfigurationspfade, Aliasrouten, Registrierungen, Fixtureformen und
Dokumentationsbeispiele samt ihrer zulässigen historischen Evidenzquelle. Sie
ist keine Produktdokumentation und keine Runtime-Abhängigkeit.

Der Release-Scan prüft `src/`, produktive MCP-Test- und Fixturepfade, `Docs/`,
`README.md`, `.agents/rules/`, `ainetlinter-rules.json`, MCP-Resources,
Registrierungen und Server-Instructions. `tasks/**` einschließlich Audit und
abgeschlossener Konzepte ist ausschließlich historische Evidenz und wird vom
Aktivscan ausgenommen. Ein Treffer im aktiven Prüfpfad blockiert den Release.

Alte Requests bleiben nicht als Fixtures erhalten. Ein generischer Raw-Wire-
Test beweist die Ablehnung unbekannter Properties. Schema-/Response-Allowlist,
Runtime-Validatoren und Aktivscan beweisen den vollständigen Hard Cut, ohne
alte Namen in den Produkt- oder Testvertrag zurückzuholen.

## Muss-Kriterien

- Für denselben Source-Scope widersprechen sich die fünf Workflowtools nicht
  bei Target, Snapshot, Produktions-/Testtrennung, Lintstatus, Counts,
  Completeness oder Handoff.
- Produktion, Tests, Artefakte und Diagnostics werden getrennt gezählt,
  gefiltert und gerankt. Produktion wird nicht durch Tests, Artefakte oder
  Diagnostics verdrängt.
- Jeder navigierbare Treffer enthält kanonische Handoff-ID, `handoff=true` und
  erlaubte Folge-Tools. Nicht navigierbare Ergebnisse enthalten
  `handoff=false`, Grund und sicheren nächsten Schritt.
- Kein begrenztes, partielles oder nicht entscheidbares Ergebnis behauptet
  Sauberkeit, globale Abwesenheit, Runtime-Coverage oder Löschbarkeit.
- Dead-Code- und Duplicate-Kandidaten enthalten Confidence, Evidenzgrenze und
  Gegenprüfung, aber keine Löschanweisung.
- Die öffentliche Oberfläche akzeptiert ausschließlich den Vertragskatalog.
  Entfernte Parameter, Response-Felder, IDs, Statuswerte, Config-Routen,
  Aliasnamen und Varianten werden weder gelesen, erzeugt, registriert noch
  dokumentiert.
- Runtime, Schema, StructuredContent, Formatter, Resources,
  Server-Instructions, Tests, Fixtures, Docs, README und Regeln enthalten
  keine zweite Vertragsoberfläche.
- Keine Feature-Flag-, Default- oder Fallback-Variante kann den entfernten
  Vertrag wiederherstellen.

## Messbare Akzeptanzkriterien

- Eine gemeinsame Source-Fixture enthält Produktion, Testprojekt, generiertes
  Artefakt, echte Violation, Reflection-/DI-Grenze und statisches Testsignal.
  Die Workflowtools liefern die im Katalog erwarteten getrennten Counts,
  Status-, Scope- und Snapshotwerte.
- Jede Precision-Familie besitzt mindestens einen False-Green-Schutztest und
  einen Partial- oder `not_decidable`-Wire-Test. Jeder benennt erwarteten
  Abschnittsstatus, Evidenzgrenze und verbotene positive Behauptung.
- `tools/list`, Runtime-Validator, StructuredContent-Snapshot,
  Formatter-/Text-Snapshot, Resource oder Server-Instructions und Dokumentation
  beweisen je Tool dieselben kanonischen Namen, Enums, Status und nächsten
  Schritte.
- Jeder Composite bleibt bei UTF-8-Serialisierung seiner vollständigen
  Nutzdaten innerhalb von 12 KiB und innerhalb von 4 KiB je Abschnitt.
  Diagnostics zählen zum Budget und folgen den Nutzdaten. Doppelte Caller-/Body-
  Nutzdaten sind verboten.
- Jeder navigierbare Composite-Treffer wird per Raw-Wire-Folgecall aus
  StructuredContent geprüft. Leere, nicht konfigurierte oder nicht
  entscheidbare Abschnitte erzeugen keine künstliche ID.
- Raw-Wire-Tests prüfen unbekannte Properties, target-fremde IDs, stale
  Snapshots, fehlende/ungültige Regeln, `empty`, `partial`, `truncated`,
  `unsupported` und `not_decidable` mit exaktem Code und sicherem nächsten
  Schritt.
- Entfernungsmatrix, Aktivscan und Schema-/Response-Allowlist ergeben für alle
  aktiven Prüfpfade null alte Vertragsfragmente.
- Wenn `ainetlinter-rules.json` betroffen ist, wird
  `.agents/rules/AiNetLinter.mdc` ausschließlich über
  `--sync-agent-rules-only` regeneriert und der Stand danach geprüft.
- Source- und Assembly-Regressionstests beweisen, dass Task-03-Änderungen
  keine Task-04-Assembly-Fachpayload, keinen Assembly-Lifecycle und keine
  Decompilersemantik verändern.

## Non-Goals

- Keine Migration, Rückwärtskompatibilität, Deprecation-Phase, Alias-,
  Adapter-, Dual-Read-/Dual-Write- oder Fallback-Unterstützung.
- Keine Änderung historischer Auditquellen oder abgeschlossener Task-Konzepte.
- Keine Runtime-Coverage, Testausführung, vollständige Reflection-/Generator-/
  DI-Analyse, automatische Löschung oder automatische Refactorings.
- Keine neuen Lintregeln, Targetmigration, allgemeine Repositorybereinigung
  oder kosmetische Formatterarbeit ohne Einfluss auf eine Agentenentscheidung.
- Keine Task-04-Arbeit an Decompiled-Qualität, Assembly-Consumerwissen,
  Assembly-Leases, Cache, TTL, Eviction oder Lifecycle.

## Risiken und verworfene Alternativen

Ein Hard Cut kann externe Clients und lokale Skripte brechen. Das ist bewusst;
ein Adapter oder Migrationsleitfaden würde Mehrdeutigkeit wieder einführen.

Ein Komplettscan ohne Trennung aktiver Oberflächen von historischer Evidenz
liefert falsche Treffer. Ein Scan nur einzelner Runtime-Dateien übersieht Docs,
Regeln und Fixtures. Deshalb sind Entfernungsmatrix und aktive Prüfpfade beide
erforderlich.

Verworfen sind:

- nur Formattertexte ändern, weil Runtime und StructuredContent weiter
  widersprechen könnten;
- alte und neue Verträge parallel bedienen, weil Scope-, Status- und
  ID-Wahrheiten divergieren;
- die Toolfamilien in Mega-DTO oder gemeinsamen Scanner pressen, weil
  Fachscanner und Toolpayloads getrennt bleiben müssen;
- Task-04-Assemblyqualität in diesen Task ziehen, weil dies Lifecycle- und
  Decompiled-Risiken mit der Source-Schleife vermischt.

## Konkrete Agentenszenarien

| Fall | Muss enthalten | Darf nicht behaupten | Nächster Schritt |
| --- | --- | --- | --- |
| Änderung an Symbol | Kontextanker, Prod-/Testimpact, statische Testkandidaten und Handoffs | Runtime-Coverage oder vollständige Aufruferliste bei Truncation | Handoff prüfen, ändern, gezielt linten und metrisch prüfen. |
| Leerer Git-Diff | expliziten leeren Diff-Scope samt Grenze | „Kein Impact im Repository“ | Symbolimpact oder anderen Diff-Scope anfordern. |
| Fehlende Regeln | nutzbare Navigation und Lint-/Quality-Abschnitt `not_configured` | „0 Violations“ oder grünen Safeguard | Regeln bereitstellen oder Linturteil aussetzen. |
| Reflection-/DI-Kandidat | Confidence, Evidenzgrenze und Gegenprüfung | Löschbarkeit oder Nichtexistenz einer Referenz | Registrierung, Referenzen oder Laufzeitkonfiguration prüfen. |
| Alter Einstieg | `invalid_argument`, Feldpfad und neuen Einstieg | Aliasauflösung oder Umformen | Kanonischen Input verwenden. |
| Aktiver Alttext | Treffer der Entfernungsmatrix | historische Ausnahme im aktiven Pfad | Text entfernen oder auf Istvertrag korrigieren. |

## Erforderliche Dokumentationsänderungen

- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/ROADMAP.md`, README,
  MCP-Resources, Toolbeschreibungen und Server-Instructions beschreiben nur den
  ausgelieferten Vertrag. Alte Beispiele werden gelöscht oder fachlich ersetzt,
  nie als Migration erklärt.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` und weitere betroffene manuelle
  Regeln beschreiben neuen Agentenablauf, Status, Handoffs und
  Source-/Assembly-Grenzen.
- `ainetlinter-rules.json` wird nur bei tatsächlichem Bedarf geändert. Danach
  wird die generierte `.agents/rules/AiNetLinter.mdc` über den vorgesehenen
  Generator synchronisiert.
- Dokumentation erzeugt keine prosebasierte zweite Source of Truth;
  vollständige Schemaformen stammen aus Vertragskatalog und Schema.

## Verifikation

Die Umsetzung verwendet für C#-Semantik MCP-first. Vor Abschluss erfolgen
passende `get_impact`-, `get_violations`-, `get_hotspots`- und `safeguard`-
Prüfungen sowie der in den Regeln verlangte Auditor-Schritt.

Bei Produktions- oder Testcodeänderungen sind verbindlich:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Zusätzlich sind Contract-, Schema-, Response-Allowlist-, Raw-Wire-, Scope-,
Empty-, Partial-, Truncation-, Precision-, Source-/Assembly-Regression- und
Dogfood-Tests erforderlich. Ein frisch gestarteter MCP-Workflow führt die
vollständige Agentenschleife gegen eine kontrollierte Source-Fixture aus.

Für reine Markdown-, Dokumentations- oder Agenteninfrastrukturänderungen
genügen relevante Referenzprüfungen, Aktivscan, `git diff --check` und eine
sachliche Diff-Prüfung. Sobald Produktions- oder Testcode betroffen ist,
bleiben Build und beide Nicht-Stress-Suiten Pflicht.

## Release-Gate

Der Release ist blockiert, sobald einer dieser Befunde verbleibt:

- ein alter Request, Response-Feld, ID-, Status- oder Konfigurationsroute wird
  akzeptiert, erzeugt, registriert, dokumentiert oder still umgedeutet;
- Entfernungsmatrix, Aktivscan oder Schema-/Response-Allowlist finden eine
  zweite Vertragsoberfläche in einem aktiven Prüfpfad;
- Scope, Snapshot, Lintstatus, Counts, Completeness, Handoff oder `next`
  widersprechen sich zwischen Workflowtools oder zwischen Text und
  StructuredContent;
- ein begrenztes oder nicht entscheidbares Ergebnis behauptet Sauberkeit,
  globale Negativität, Runtime-Coverage oder Löschbarkeit;
- ein Composite überschreitet sein Wirebudget, dupliziert Nutzdaten oder
  erfindet einen Handoff;
- eine Task-04-Assembly-Fachpayload oder Lifecycle-Semantik wurde durch Task 03
  verändert;
- Generator-, Build-, Nicht-Stress-, Wire-, Dogfood-, Dokumentations- oder
  Diff-Gate ist nicht grün.

## Nachgelagerte Beobachtung

Nach Release wird der Nutzen konservativer Kandidaten an echten
Source-Änderungen bewertet. Ein Recall-Ausbau oder zusätzliche Analysefähigkeit
ist ein separater evidenzbasierter Auftrag und darf nicht als
Kompatibilitätsrest in diesen Hard-Cut-Task zurückfließen.
