---
status: draft
execution_mode: autonomous
open_questions: []
---

# MCP-Qualitätsprüfung auf `verify` konsolidieren

## Ziel und Produktentscheidung

AiNetLinter veröffentlicht für die agentische Qualitätsentscheidung künftig
genau ein MCP-Tool: `verify`. Es ersetzt die bisher getrennten öffentlichen
Qualitäts-, Score-, Lint-, Pattern-, Metrik-, Hotspot- und
Kandidaten-Auditwerkzeuge vollständig. Der Schnitt ist absichtlich hart:
Nach der Umsetzung sind die ersetzten Toolnamen, deren Registrierungen,
Schema-Varianten, DTOs, Formatter, Testpfade, Beispiele und Dokumentation
nicht mehr vorhanden. Es gibt keine Aliase, Feature Flags, Legacy-Adapter,
Dual-Read-/Dual-Write-Pfade oder Migrationshinweise.

Das Produktziel ist nicht, alle statischen Signale als einen großen Report zu
versenden. `verify` beantwortet die Frage eines programmierenden Agenten:

> Ist der adressierte Arbeitskontext nach den verbindlichen Regeln
> akzeptabel, und falls nicht: Welche wenige, konkrete Evidenz muss ich als
> Nächstes bearbeiten?

Der Gate-Entscheid ist strikt: Ein entscheidbarer `pass` ist nur zulässig,
wenn der Quality-Score exakt `10.0` und die Anzahl verbindlicher
Lint-Verstöße exakt `0` ist. Diese Grenzwerte sind Teil des öffentlichen
Vertrags, keine durch den Agenten veränderbaren Parameter. Ein nicht
entscheidbarer, partieller, gekürzter oder fehlerhafter Lauf darf niemals als
`pass` ausgegeben werden.

## Problem und agentische Nutzung

Die bisherige Toolfläche zwingt Agenten, mehrere technisch geschnittene
Teilantworten zu kombinieren: Score über `safeguard`, Regelverstöße über
`get_violations`, Pattern, Metriken und Kandidatensuchen über weitere Tools.
Das wiederholt Schemas und Navigation, erhöht Kontextkosten und schafft
Cross-Tool-Handoffs, deren Vertrag leicht auseinanderläuft.

Nicht jedes Signal ist jedoch ein Gate. `find_dead_code` und
`find_magic_values` liefern aufgrund von Reflection, Generatoren,
Konfiguration, Domänenkonventionen und unvollständigem Änderungskontext
bewusst Kandidaten. Ein Agent muss diese im betroffenen Feature-Kontext
beurteilen; sie dürfen weder eine harte Freigabe sperren noch ungefragt einen
solutionweiten Bericht über eine lokale Feature-Arbeit legen.

`verify` trennt deshalb zwei fachliche Absichten mit einer einzigen,
kleinen Auswahl statt mit vielen Detektor-Schaltern:

| Modus | Zweck | Ergebniswirkung |
|---|---|---|
| `gate` (Default) | Verbindliche Qualitätsentscheidung für den adressierten Scope. | Score `10.0` und `0` Lint-Verstöße sind zwingend für `pass`. |
| `review` | Kontextgebundene, priorisierte Prüfkandidaten zusätzlich zum festen Gate. | Kandidaten sind explizit nicht blockierend und erfordern Agentenurteil. |

Der Agent gibt im Regelfall nur `targetPath` und gegebenenfalls den konkreten
Arbeits-`scope` an. Es gibt weder `minScore`, `maxViolations` noch einzelne
`includeDeadCode`-, `includeMagicValues`-, Pattern- oder Metrik-Flags. Die
Auswahl der Scanner ist eine stabile Serverentscheidung pro Modus, nicht
eine zur Laufzeit neu zusammensetzbare Client-Pipeline.

## Öffentlicher Vertrag von `verify`

### Eingang

```text
verify(
  targetPath,
  scope?,
  mode = "gate",
  continuationToken?,
  maxResponseBytes?
)
```

- `targetPath` ist wie bei allen zielgebundenen Tools absolut und bezeichnet
  eine Source-Solution beziehungsweise -Solution-Datei. Assembly-Ziele sind
  klar als nicht unterstützt zu behandeln, nicht still leer.
- `scope` bezeichnet optional einen einzelnen, solution-relativen
  Arbeitskontext (Projekt, Verzeichnis, Datei oder kanonische Identität nach
  dem dann gültigen gemeinsamen Scopevertrag). Ohne `scope` gilt die gesamte
  Solution. Die Scopeauflösung läuft vor Analyse, Limit und Budget; die
  effektive Menge wird maschinenlesbar ausgewiesen.
- `mode` besitzt ausschließlich die Werte `gate` und `review`; ungültige
  Werte liefern einen Contract-v2-`INVALID_ARGUMENT`-Fehler mit Feldpfad und
  gültigen Werten.
- `continuationToken` dient ausschließlich zum Abrufen weiterer bereits
  deterministisch gerankter Evidenz eines unveränderten Snapshots. Er verändert
  weder Scope noch Urteil noch Scannerwahl.
- `maxResponseBytes` ist der gemeinsame Wirebudget-Vertrag. Passt mindestens
  die feste Mindestprojektion nicht, lautet der Fehler
  `RESPONSE_BUDGET_TOO_SMALL` und enthält `$.maxResponseBytes`,
  `requestedBytes` und ein beim identischen Snapshot erfolgreiches
  `minimumResponseBytes`.

Es gibt keine weitere öffentliche Option. Insbesondere werden Ergebnismenge,
Schwellwerte, Kandidatenarten und Scanner nicht über optionale Parameter
gesteuert.

### Ausgang und Entscheidungslogik

Jede zielgebundene Antwort verwendet Contract v2 mit genau einem
`structuredContent.navigation.status` als Owner für Operation und
Response-Vollständigkeit. Erfolg, Fehler, leere Kandidaten, Trunkierung und
Analysequalität bleiben getrennte Achsen.

Die Fachwurzel enthält mindestens:

```text
verdict: pass | failed | incomplete | error
gate:
  score: 0..10 | null
  requiredScore: 10.0
  violationCount: non-negative integer | null
  requiredViolationCount: 0
  decisionReason: machine-readable enum
evidence:
  totalCount, returnedCount, entries[], continuationToken?
scope:
  requested, effective, populations, exclusions
```

- `pass`: Analyse ist entscheidbar und vollständig genug für das Gate,
  `score=10.0` und `violationCount=0`.
- `failed`: Analyse ist entscheidbar; mindestens eine der beiden festen
  Gatebedingungen ist verletzt. Die Antwort enthält die bestgerankten,
  handlungsfähigen Evidenzeinträge sowie vollständige Counts.
- `incomplete`: Der Scope, die Analyse oder die Evidenz genügt nicht für eine
  Freigabeentscheidung. Score und Count dürfen nur ausgegeben werden, wenn
  ihre Aussagegrenze eindeutig markiert ist; sie dürfen keinen Pass ableiten.
- `error`: Request- oder exogener Fehler. Der MCP-Envelope hat `isError=true`,
  `operation=error`, `completeness=not_applicable`, Code, Feldpfad soweit
  möglich und genau eine strukturierte Recovery.

Evidenz zeigt pro Eintrag Regel/Kategorie, Severity, zuverlässige
Quellzuordnung, kurze fachliche Begründung und einen kanonischen Handoff.
Keine Volltext-Snippets, Betriebsdaten oder redundante Navigationszeilen sind
Defaultinhalt. Größere Budgets und Folgeseiten erweitern die zuvor sichtbare
Evidenz monoton um ganze Einträge.

### Semantik von `gate` und `review`

`gate` führt ausschließlich die verbindliche Lint-/Scoreentscheidung aus und
gibt bei Fehlern genug Evidenz für die erste Bearbeitung aus. Die beiden
unveränderlichen Akzeptanzbedingungen bleiben immer sichtbar: `10.0` und
`0`.

`review` führt denselben Gate-Kern aus und ergänzt nur für einen explizit
adressierten Arbeitskontext priorisierte advisory-Kandidaten. Dazu zählen die
heutigen Quellen für Dead-Code-, Magic-Value-, Pattern-, Hotspot- und
Metrik-Signale, soweit sie für diesen Scope fachlich sinnvoll und
entscheidbar sind. Jeder solche Eintrag trägt mindestens:

- `kind: advisory_candidate`;
- Confidence, Evidenzgrenze und bekannte Gegenindikatoren;
- `requiresAgentJudgment: true`;
- keine Lösch- oder Änderungsbehauptung und keine Wirkung auf `verdict`.

Ein ungescopter `review` wird nicht als stiller Solution-Vollscan ausgeführt.
Er liefert stattdessen einen feldgenauen, recoverable Hinweis auf einen
einzugrenzenden Scope. Das bewahrt Signal-Rausch-Verhältnis und verhindert,
dass ein Agent bei Feature X mit nicht zuordenbaren Kandidaten der ganzen
Codebasis überflutet wird.

## Harter Entfall und klare Grenzen

Folgende MCP-Tools entfallen als öffentliche Qualitätsoberfläche und werden
vollständig entfernt:

- `safeguard`
- `get_violations`
- `pattern_detect`
- `find_magic_values`
- `find_dead_code`
- `get_hotspots`
- `metrics_tree`
- `metrics_lookup`

Die zugrundeliegende belegbare Analyse kann als interne, eindeutig besessene
Domänenlogik weiterverwendet werden, wenn `verify` sie benötigt. Alte
Safeguard-/Violation-/Pattern-spezifische öffentliche DTOs, Formatter,
Registrierungen und Adapter dürfen jedoch nicht als zweite Vertrags- oder
Antwortschicht fortleben. Eine bestehende Analyse ist nur zu behalten, wenn
sie eine klar abgegrenzte Eingabe für den neuen Gate- oder Review-Projektor
liefert.

Unberührt bleibt in diesem Task die übrige semantische Erkundungsoberfläche
(Suche, Symbol-, Feature-, Referenz-, Impact-, Datei- und Assemblytools).
Auch eine spätere, weitergehende Konsolidierung zu `explore`/`inspect` ist
eine eigene Produktentscheidung. Dieser Task darf sie weder vorwegnehmen noch
ein universelles Mega-Tool bauen.

## Source of Truth und voraussichtlich betroffene Bereiche

Fachliche Wahrheit liefern in dieser Reihenfolge:

1. die neuen `verify`-Contract- und Dogfoodtests gegen einen aus dem
   Taskstand erzeugten Host;
2. die gemeinsamen Contract-v2-, Budget-, Scope- und Fehlerregeln unter
   `src/AiNetLinter/Mcp/`;
3. die bestehende Lint-Engine und die wiederverwendbaren, fachlich passenden
   Scannerergebnisse;
4. die zur Laufzeit veröffentlichte Toolregistrierung und `tools/list`;
5. die Endzustandsdokumentation und Agentenregeln.

Voraussichtlich betroffen sind die Analyse-Toolregistrierung, deren
Argumentvalidierung und Capability-Matrix, Safeguard-/Violation-/Pattern-/
Metric-/Candidate-Toolpfade, gemeinsame Navigation- und Budgetprojektoren,
Health-Capabilities, Fast- und Integrationstests sowie die Dogfood- und
Server-Contracttests. Dokumentation und Arbeitsregeln umfassen insbesondere
`Docs/agent-api.md`, `Docs/integration.md`, relevante Rationale-/Guide- und
Server-Instructions-Pfade, `ainetlinter-rules.json` soweit es den
öffentlichen Vertrag beschreibt, sowie die verbindlichen MCP-Workflow- und
Quality-Gate-Regeln. `README.md` bleibt ohne ausdrücklichen Auftrag unberührt.

Der bereits laufende MCP-Server ist nur für semantische Source-Navigation
brauchbar. Runtime-, Schema- und Verhaltensnachweise stammen ausschließlich
aus der vorhandenen C#-Testinfrastruktur mit einem frischen Taskstand-Host.

## Muss-Kriterien

1. `verify` ist das einzige registrierte öffentliche MCP-Tool für die oben
   genannten Quality-Gate-, Violation-, Pattern-, Metrik-, Hotspot-,
   Dead-Code- und Magic-Value-Anliegen; alle acht ersetzten Namen fehlen aus
   Runtime, Schema, Tests, Guides und Beispielen.
2. Ohne frei konfigurierbaren Grenzwert bedeutet ein entscheidbares `pass`
   stets Score `10.0` und `violationCount=0`; kein anderer Status darf diese
   Freigabe suggerieren.
3. `gate` bleibt knapp, deterministisch und für einen Feature-Scope
   handlungsfähig. `review` ergänzt Kandidaten nur für einen expliziten Scope
   und markiert sie klar als nicht blockierende Agentenentscheidung.
4. Jeder valide Default liefert mindestens Gate-Summary plus eine vollständige
   fachliche Evidenzeinheit. Kleine Budgets liefern einen exakt ausführbaren
   `RESPONSE_BUDGET_TOO_SMALL`-Retry, nie einen erfolgreichen leeren oder das
   Budget überschreitenden Report.
5. Die Antwort nutzt Contract v2 atomar: Frühe Validierung, Budgetfehler,
   Source-only-Assemblyfehler und fachliche Fehler haben einheitlich
   `isError=true`, `operation=error`, `completeness=not_applicable`, Code,
   Feldpfad und genau eine Recovery.
6. Scope, Generated-/Test-Ausschlüsse, Counts und Trunkierungsgründe werden
   vor Analyse- und Antwortlimits bestimmt, klar benannt und zwischen Text und
   StructuredContent nicht widersprüchlich dargestellt.
7. Evidenz-Handoffs sind kanonisch, nur strukturiert vorhanden und mit den
   weiter bestehenden semantischen Folgewerkzeugen direkt verwendbar.
8. Die interne Wiederverwendung erzeugt keine zweite Status-, Fehler-, Scope-
   oder Budgethierarchie und keine Legacytypen mit alter öffentlicher
   Semantik.

## Akzeptanzkriterien

1. `tools/list` veröffentlicht `verify` mit ausschließlich den beschriebenen
   Eingaben, vollständigen Beschreibungen und korrekten Defaults; keiner der
   acht entfernten Toolnamen ist dort registriert.
2. `verify(gate)` für einen vollständig sauberen Scope liefert `pass`, Score
   `10.0`, `violationCount=0`, vollständige Navigation und keine überflüssige
   Kandidatenliste.
3. Ein Scope mit Regelverstoß oder Score kleiner `10.0` liefert `failed`,
   vollständige Counts und mindestens einen ausführbaren Evidenz-Handoff;
   `pass` ist ausgeschlossen.
4. Ein unkonfigurierter, unlesbarer oder fachlich nicht entscheidbarer Scope
   liefert `incomplete` statt Score `0`, `pass` oder Silent-Empty.
5. `review` ohne konkreten Scope wird feldgenau zurückgewiesen; mit einem
   Feature-Scope erscheinen nur deterministisch gerankte,
   `requiresAgentJudgment=true`-Kandidaten. Sie verändern ein sonst
   erfolgreiches Gateurteil nicht.
6. Reflection-/DI-/Generator- und ähnliche Gegenindikatoren werden bei
   Dead-Code-Kandidaten explizit sichtbar; Magic-Value-Kandidaten enthalten
   Kategorie und Evidenzgrenze. Kein Kandidat behauptet eigenständig eine
   sichere Lösch- oder Änderungsaktion.
7. Fehlerfälle für fehlendes/falsches `targetPath`, ungültigen Modus,
   leeren/ungültigen Scope, fremden Continuation-Token und zu kleines Budget
   sind vollständig Contract-v2-konform und lassen einen anschließenden
   gültigen Aufruf unverändert funktionieren.
8. Kleinster akzeptierter Budgetretry, Werte dazwischen und größere Budgets
   liefern monotone vollständige Einheiten und messen Text sowie
   StructuredContent inklusive Navigation in UTF-8.
9. Source-Scope funktioniert; ein Assembly-Ziel liefert einen klaren,
   datensparsamen `ASSEMBLY_TARGET_UNSUPPORTED`-Fehler. Keine Zielpfade,
   PIDs, Daemon- oder andere fremde Betriebsidentitäten erscheinen unnötig im
   Text.
10. Dogfood prüft reale Agentenabläufe: Feature-Patch-Gate, fokussiertes
    Kandidatenreview, fehlgeschlagenes Gate mit Handoff, Budgetretry sowie
    Legacy-Tool-Abwesenheit gegen einen frischen Host.
11. Endzustandsdokumentation, Runtime-Schema, Agent-Guide, Workflowregel und
    Testnamen enthalten nur den neuen Vertrag und keine
    Migrations-/Historientexte.

## Architektur- und Betriebssemantik

`verify` ist ein dünner öffentlicher Orchestrator, kein zweiter Linter und
kein allgemeiner Query-Interpreter. Er nimmt einen bereits normalisierten
Scope entgegen, ruft die festen Gate- beziehungsweise Reviewquellen auf und
projiziert ihre Ergebnisse in eine gemeinsame, unveränderliche
Entscheidungsantwort. Scanner behalten vollständige Domänenresultate;
Toolnahe Projektoren wählen deterministisch ganze Evidenzeinheiten.

Die Gateentscheidung besitzt genau einen Owner. Advisory-Evidenz kann niemals
den Gatezustand übersteuern. Bei konkurrierender oder fehlender Analyse
gewinnt konservativ `incomplete`, nicht ein gemittelter Score. Ein
Continuation-Token ist snapshot- und requestgebunden; veraltet, fremd oder
manipuliert wird er präzise abgelehnt und öffnet keinen anderen Scope.

Keine Analyse führt Zielcode aus, lädt untersuchte Assemblies dynamisch oder
ändert Source, Konfiguration oder Git-Zustand. `verify` ist read-only und
Source-only; Assemblyanalyse bleibt bei den dafür vorgesehenen Tools.

## Umsetzungsplan

### Slice 01 – Öffentlichen `verify`-Vertrag festlegen und rot absichern

- Gemeinsame Request-/Responsemodelle, feste Modi, Verdicts, Scope- und
  Evidenzsemantik definieren.
- Contracttests für `pass`, `failed`, `incomplete`, `error`, Budgetretry,
  frühe Validierung, Scope und Handoff zuerst rot schreiben.
- Den extern sichtbaren Toolinventar-Sollzustand festlegen: `verify` vorhanden,
  alle acht Altnamen abwesend.

**Exit:** Der neue Vertrag ist als Testsprache eindeutig; keine Testannahme
referenziert einen Legacy-Adapter.

### Slice 02 – Gate-Kern mit festen `10.0`/`0`-Invarianten implementieren

- Lint-/Scorequelle in einen klaren `verify`-Gateprojektor überführen.
- Scoring- und Violationergebnis gemeinsam, aber ohne Vermischung ihrer
  Aussagegrenzen ausgeben.
- `pass` nur bei beiden festen Bedingungen und ausreichender
  Entscheidbarkeit erlauben; Fehler, Budget und Navigation contractweit
  vereinheitlichen.

**Exit:** Gatefälle, Fehlerhüllen, Scope-Counts und Budgets sind gegen einen
frischen Host grün und handlungsfähig.

### Slice 03 – Kontextgebundenen Review-Modus integrieren

- Kandidatenquellen fachlich als advisory-Projektionen anbinden, nicht als
  versteckte Gatebedingungen.
- Scopepflicht, Confidence, Gegenindikatoren, Ranking, Paging und
  Datensparsamkeit für Dead Code, Magic Values, Patterns, Hotspots und
  Metriksignale umsetzen.
- Reihenfolge, Budgetmonotonie und Nichtbeeinflussung des Gateurteils mit
  fokussierten Tests absichern.

**Exit:** Feature-scoped Review liefert begrenzte, ehrlich unsichere Evidenz;
der Default-Gate bleibt frei von Kandidatenrauschen.

### Slice 04 – Harter Schnitt durch Registrierung, Produktion und Tests

- Alte Toolregistrierungen, Argumentlimits, Capabilities, DTOs, Formatter,
  Adapter, obsolete Projektoren und ausschließlich zugehörige Tests entfernen
  oder auf `verify` umstellen.
- Nur fachlich verwendete interne Scanner in klarer neuer Ownership behalten;
  keine Safeguard-/Violation-spezifische öffentliche Zwischenabstraktion
  zurücklassen.
- Negativtests sichern, dass alte Toolnamen nicht registrierbar sind und kein
  indirekter Legacypfad existiert.

**Exit:** Runtime und Source veröffentlichen nur `verify` als
Qualitätsoberfläche.

### Slice 05 – Dokumentation, Regeln und Endverifikation synchronisieren

- API-, Integrations-, Guide-, Server-Instructions-, Konfigurations- und
  Agentenregeltexte auf den Endvertrag umstellen.
- Den bisherigen verpflichtenden Qualitätsgate in Regeln und Workflow durch
  `verify(gate)` mit `pass`, `10.0` und `0` ersetzen.
- Veraltete Toolnamen und Migrationssprache gezielt negativ suchen; keinen
  historischen Übergangstext publizieren.

**Exit:** Tool-Schema, Runtime, Tests, Dokumentation und Regeln beschreiben
denselben finalen Vertrag.

## Verifikation und Release-Gate

Nach jedem fachlichen Slice laufen nur passende fokussierte Tests. Vor
Abschluss seriell und ohne Stresskategorie:

```powershell
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
git diff --check
```

Zusätzlich sind gegen einen frischen In-Process- oder Prozesshost verpflichtend:

- `tools/list`-Inventar- und Schemavergleich;
- vollständige `verify`-Dogfoodmatrix für Gate, Review, Budget, Scope,
  Handoff, Validation, Continuation und Source-only-Assemblyfehler;
- Negativsuche nach allen acht entfernten Namen in Produktionsregistrierung,
  öffentlicher Dokumentation, Agentenregeln und Server-Instructions;
- gezielte Architekturprüfung auf tote Adapter, doppelte Vertragsmodelle,
  Magic-Values und nicht verwendete Legacybestandteile;
- ein abschließender read-only MCP-UX-Audit nur für den neuen `verify`-Vertrag
  und seine weiter bestehenden Handoffs.

Der frühere Gateaufruf `safeguard`/`get_violations` wird in diesem Release-Gate
nicht mehr verwendet, weil seine Existenz nach dem harten Schnitt selbst ein
Fehler wäre. Der Nachweis erfolgt über `verify(mode="gate")`: `verdict=pass`,
`score=10.0`, `violationCount=0` und ausreichende Vollständigkeit.

## Dokumentationsbedarf

- `Docs/agent-api.md`: ein kompakter Vertrag für Eingang, Verdicts,
  Gate-/Review-Semantik, Evidenz, Budget und Continuation; keine frühere
  Toolliste als Übergang darstellen.
- `Docs/integration.md` und der Runtime-Agent-Guide: einen Agentenfluss
  `Feature-Scope → verify(gate) → optional verify(review)` beschreiben.
- `.agents/rules/AiNetLinterRichtlinien.mdc` und
  `.agents/rules/AiNetLinter-McpWorkflow.mdc`: alte Pflichtaufrufe,
  Tabellen und Scopehinweise konsistent durch `verify` ersetzen.
- `ainetlinter-rules.json` und weitere Konfigurationsdokumentation nur dort
  anpassen, wo alte öffentliche Toolnamen oder deren Bedeutungen erklärt
  werden.
- Keine README-Änderung ohne separaten ausdrücklichen Auftrag.

## Risiken und Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|---|---|
| `verify` wird ein schwer verständliches Mega-Tool. | Nur `scope`, zwei Modi, Continuation und Budget; keine Detektor- und Schwellenwertflags. |
| Kandidaten werden fälschlich als Gateblocker gelesen. | Getrennte `verdict`-Ownership, `advisory_candidate`, `requiresAgentJudgment` und keine Kandidatenwirkung auf den Gateentscheid. |
| Der feste Score verdeckt unvollständige Analyse. | `pass` setzt Entscheidbarkeit und ausreichende Completeness voraus; sonst konservativ `incomplete`. |
| Das Entfernen öffentlicher Tools löscht nützliche Fachlogik. | Scanner nur nach klarer Ownership weiterverwenden; öffentliche DTO-/Formatterpfade trotzdem vollständig löschen. |
| Breiter Review erzeugt hohe Kosten und Rauschen. | Expliziter konkreter Scope, stabile Top-Evidenz, Paging und kombiniertes UTF-8-Budget. |
| Alte Runtime verfälscht Nachweise. | Ausschließlich frischen Taskstand-Host für Wire- und Schema-Verifikation verwenden. |
| Dokumentation oder Regeln behalten alte Quality-Gates. | Negativsuche plus Inventar-Contracttests und abschließende manuelle Diffprüfung. |

## Verworfene Alternativen

- **Bestehende Tools behalten und nur `verify` ergänzen:** Erhält
  Kontextkosten, Mehrdeutigkeit und Cross-Tool-Contractflächen; widerspricht
  dem harten Schnitt.
- **Alle bisherigen Scanner über zahlreiche `verify`-Booleans schaltbar
  machen:** Reduziert nur Toolnamen, nicht die agentische
  Entscheidungskomplexität.
- **Dead Code und Magic Values stets in den Gateentscheid einrechnen:**
  Verwandelt bewusst heuristische Hinweise in False-Positive-Blocker und
  ignoriert Featurekontext.
- **Review immer solutionweit ausführen:** Liefert bei lokaler Arbeit hohes
  Rauschen und unvorhersehbare Kosten.
- **Sofort die gesamte MCP-Oberfläche auf vier Universalschnittstellen
  reduzieren:** Vermischt diesen begrenzten Quality-Schnitt mit einer zweiten,
  größeren Produktentscheidung und erhöht unnötig das Risiko.

## Offene Entscheidungen

Keine. Die Parameteroberfläche ist bewusst auf Scope, Modus, Continuation und
Budget begrenzt; fachliche Detailauswahl liegt beim Server. Der Draft bleibt
bis zur ausdrücklichen Nutzerfreigabe `draft`; danach kann ein Orchestrator
innerhalb dieses vollständig beschriebenen Scopes autonom umsetzen.
