---
status: draft
execution_mode: autonomous
open_questions: []
---

# MCP-Qualitätsprüfung auf `verify` konsolidieren

## Ziel und Produktentscheidung

AiNetLinter veröffentlicht für die agentische Qualitätsentscheidung künftig
genau ein MCP-Tool: `verify`. Es ersetzt die bisher getrennten öffentlichen
Gate-, Score-, Lint- und kontextgebundenen Kandidatenwerkzeuge vollständig.
Der Schnitt ist absichtlich hart:
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
`get_violations` und kontextgebundene Kandidatensuchen über weitere Tools.
Das wiederholt Schemas und Navigation, erhöht Kontextkosten und schafft
Cross-Tool-Handoffs, deren Vertrag leicht auseinanderläuft.

Nicht jedes Signal ist jedoch ein Gate. `find_dead_code` und
`find_magic_values` liefern aufgrund von Reflection, Generatoren,
Konfiguration, Domänenkonventionen und unvollständigem Änderungskontext
bewusst Kandidaten. Ein Agent muss diese im betroffenen Feature-Kontext
beurteilen; sie dürfen weder eine harte Freigabe sperren noch ungefragt einen
solutionweiten Bericht über eine lokale Feature-Arbeit legen.

`verify` bildet deshalb nur die zwei echten Agentenabsichten ab, nicht die
technischen Scanner:

| Aufruf | Agentenabsicht | Ergebniswirkung |
|---|---|---|
| `verify(targetPath)` | „Sind meine aktuellen Arbeitsänderungen in Ordnung?“ | Fester Gatekern für den automatisch bestimmten Änderungskontext; ergänzende Dead-Code-/Magic-Value-Kandidaten nur dort und nur advisory. |
| `verify(targetPath, scope: "solution")` | „Ist die gesamte Solution bereit zum Abschluss?“ | Fester Gatekern solutionweit; keine kontextlosen Heuristik-Kandidaten. |

Der Agent muss weder Pfade, Filter, Detektoren, Schwellwerte, Profile noch
Antwortlimits erraten. Die Auswahl der Scanner, der betrachtete Kontext und
die Antwortprojektion sind stabile Serverentscheidungen.

## Öffentlicher Vertrag von `verify`

### Eingang

```text
verify(
  targetPath,
  scope = "changes" | "solution"
)
```

- `targetPath` ist absolut und akzeptiert ausschließlich eine
  Source-Solution (`.sln` oder `.slnx`). `verify` ist explizit **Source-only**:
  `.dll`-, `.exe`- und andere Assembly-Ziele werden vor Lease, Decompilation
  oder fachlicher Analyse als klarer, strukturierter
  `ASSEMBLY_TARGET_UNSUPPORTED`-Fehler zurückgewiesen, nicht still leer.
- `scope` hat genau zwei Enum-Werte. Fehlt der Parameter, gilt `changes`.
  `solution` ist die bewusste vollständige Abschlussprüfung. Andere Werte
  liefern vor Analyse einen Contract-v2-`INVALID_ARGUMENT`-Fehler mit
  Feldpfad und den beiden gültigen Werten.
- `changes` bedeutet den Git-Working-Tree-Diff gegenüber `HEAD`: gestagte,
  ungestagte und relevante neue Source-Dateien. Die Solution wird weiterhin
  vollständig als semantischer Kontext geladen; Evidenz und Kandidaten werden
  auf die Änderungen und ihren notwendigen Impact begrenzt.
- Bei leerem oder nicht bestimmbaren Änderungskontext ist `changes` immer
  `incomplete`, nie `pass`. Die einzige Recovery ist der explizite Aufruf mit
  `scope: "solution"`.
- Der Server verwendet ein festes, dokumentiertes Antwortbudget und gibt eine
  deterministisch gerankte, kleine Menge ganzer Evidenzeinheiten aus. Es gibt
  keinen clientseitigen Budget-, Paging- oder Detektorparameter. Vollständige
  Counts bleiben sichtbar; nach einer Korrektur ruft der Agent denselben
  einfachen Aufruf erneut auf.

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
  totalCount, returnedCount, entries[]
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
Defaultinhalt. Die feste Projektion liefert stets ganze Evidenzeinheiten und
nennt vollständige Counts statt die Antwort durch clientseitige Optionen
aufzublähen.

### Semantik von `changes` und `solution`

Beide Scopes führen immer die verbindliche Lint-/Scoreentscheidung aus und
zeigen die beiden unveränderlichen Akzeptanzbedingungen `10.0` und `0`.

Nur `changes` ergänzt für den automatisch bestimmten Änderungskontext
priorisierte advisory-Kandidaten aus den heutigen Dead-Code- und
Magic-Value-Analysen. Jeder solche Eintrag trägt mindestens:

- `kind: advisory_candidate`;
- Confidence, Evidenzgrenze und bekannte Gegenindikatoren;
- `requiresAgentJudgment: true`;
- keine Lösch- oder Änderungsbehauptung und keine Wirkung auf `verdict`.

`solution` führt diese Heuristiken nicht ungefragt aus. Ohne
Änderungskontext wären sie ein großer, schlecht zuordenbarer Kandidatenreport
und keine belastbare Abschlussentscheidung. Das bewahrt Signal-Rausch-
Verhältnis und verhindert, dass ein Agent bei Feature X mit Kandidaten der
ganzen Codebasis überflutet wird.

## Harter Entfall und klare Grenzen

Folgende MCP-Tools entfallen als öffentliche Gate- und
Kandidatenprüfungsoberfläche und werden vollständig entfernt:

- `safeguard`
- `get_violations`
- `find_magic_values`
- `find_dead_code`

Die zugrundeliegende belegbare Analyse kann als interne, eindeutig besessene
Domänenlogik weiterverwendet werden, wenn `verify` sie benötigt. Alte
Safeguard-/Violation-spezifische öffentliche DTOs, Formatter,
Registrierungen und Adapter dürfen jedoch nicht als zweite Vertrags- oder
Antwortschicht fortleben. Eine bestehende Analyse ist nur zu behalten, wenn
sie eine klar abgegrenzte Eingabe für den neuen Gate- oder Kandidatenprojektor
liefert.

Unberührt bleiben in diesem Task `pattern_detect`, `get_hotspots`,
`metrics_tree` und `metrics_lookup` sowie die übrige semantische
Erkundungsoberfläche (Suche, Symbol-, Feature-, Referenz-, Impact-, Datei- und
Assemblytools). Auch eine spätere, weitergehende Konsolidierung zu
`explore`/`inspect` ist eine eigene Produktentscheidung. Dieser Task darf sie
weder vorwegnehmen noch ein universelles Mega-Tool bauen.

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
Argumentvalidierung und Capability-Matrix, Safeguard-/Violation-/Dead-Code-/
Magic-Value-Toolpfade, gemeinsame Navigation- und Budgetprojektoren,
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
   genannten Quality-Gate-, Violation-, Dead-Code- und Magic-Value-Anliegen;
   alle vier ersetzten Namen fehlen aus Runtime, Schema, Tests, Guides und
   Beispielen. Die ausdrücklich unberührten Pattern-, Hotspot- und
   Metrikwerkzeuge bleiben unverändert registriert.
2. Ohne frei konfigurierbaren Grenzwert bedeutet ein entscheidbares `pass`
   stets Score `10.0` und `violationCount=0`; kein anderer Status darf diese
   Freigabe suggerieren.
3. `verify(targetPath)` bestimmt seinen Änderungskontext ohne weiteren
   Agentenparameter. Es prüft den festen Gatekern und ergänzt nur dort
   handlungsfähige, nicht blockierende Dead-Code-/Magic-Value-Kandidaten.
4. `verify(targetPath, scope: "solution")` ist die vollständige
   Abschlussprüfung und führt keine kontextlosen Heuristik-Kandidaten aus.
5. Jeder valide Aufruf liefert mindestens Gate-Summary plus eine vollständige
   fachliche Evidenzeinheit innerhalb des festen Serverbudgets, nie einen
   erfolgreichen leeren oder ein unkontrolliert großes Report.
6. Die Antwort nutzt Contract v2 atomar: Frühe Validierung,
   Source-only-Assemblyfehler und fachliche Fehler haben einheitlich
   `isError=true`, `operation=error`, `completeness=not_applicable`, Code,
   Feldpfad und genau eine Recovery.
7. Scope, Generated-/Test-Ausschlüsse, Counts und Trunkierungsgründe werden
   vor Analyse- und Antwortlimits bestimmt, klar benannt und zwischen Text und
   StructuredContent nicht widersprüchlich dargestellt.
8. Evidenz-Handoffs sind kanonisch, nur strukturiert vorhanden und mit den
   weiter bestehenden semantischen Folgewerkzeugen direkt verwendbar.
9. Die interne Wiederverwendung erzeugt keine zweite Status-, Fehler-, Scope-
   oder Budgethierarchie und keine Legacytypen mit alter öffentlicher
   Semantik.

## Akzeptanzkriterien

1. `tools/list` veröffentlicht `verify` mit ausschließlich den beschriebenen
   Eingaben, vollständigen Beschreibungen und korrekten Defaults; keiner der
   vier entfernten Toolnamen ist dort registriert. Die vier unberührten
   Pattern-, Hotspot- und Metrikwerkzeuge bleiben im Inventar erhalten.
2. `verify(targetPath)` für einen vollständig sauberen Änderungskontext liefert
   `pass`, Score `10.0`, `violationCount=0`, vollständige Navigation und nur
   die kontextbezogenen advisory-Kandidaten.
3. `verify(targetPath, scope: "solution")` für eine vollständig saubere
   Solution liefert `pass`, Score `10.0`, `violationCount=0` und keine
   Heuristik-Kandidatenliste.
4. Ein Änderungskontext oder eine Solution mit Regelverstoß oder Score kleiner
   `10.0` liefert `failed`,
   vollständige Counts und mindestens einen ausführbaren Evidenz-Handoff;
   `pass` ist ausgeschlossen.
5. Ein leerer oder nicht bestimmbarer Working-Tree-Diff, ein unkonfigurierter,
   unlesbarer oder fachlich nicht entscheidbarer Kontext liefert `incomplete`
   statt Score `0`, `pass` oder Silent-Empty. Der leere Diff verweist allein
   auf `scope: "solution"`.
6. Nur im Änderungskontext erscheinen deterministisch gerankte,
   `requiresAgentJudgment=true`-Kandidaten. Sie verändern ein sonst
   erfolgreiches Gateurteil nicht.
7. Reflection-/DI-/Generator- und ähnliche Gegenindikatoren werden bei
   Dead-Code-Kandidaten explizit sichtbar; Magic-Value-Kandidaten enthalten
   Kategorie und Evidenzgrenze. Kein Kandidat behauptet eigenständig eine
   sichere Lösch- oder Änderungsaktion.
8. Fehlerfälle für fehlendes/falsches `targetPath`, ungültigen Scopewert und
   Assemblyziel sind vollständig Contract-v2-konform und lassen einen
   anschließenden gültigen Aufruf unverändert funktionieren.
9. Die feste Projektion ist deterministisch, enthält nur vollständige
   Evidenzeinheiten und misst Text sowie StructuredContent inklusive
   Navigation in UTF-8.
10. `verify` akzeptiert ausschließlich `.sln`-/`.slnx`-Source-Ziele. Ein
   Assembly-Ziel wird vor jeder Decompilation oder Analyse mit einem klaren,
   datensparsamen `ASSEMBLY_TARGET_UNSUPPORTED`-Fehler zurückgewiesen. Keine
   Zielpfade, PIDs, Daemon- oder andere fremde Betriebsidentitäten erscheinen
   unnötig im Text.
11. Dogfood prüft reale Agentenabläufe: Working-Tree-Änderungsprüfung,
    vollständige Solution-Abschlussprüfung, fehlgeschlagenes Gate mit Handoff,
    leerer Diff, Assemblyfehler sowie Legacy-Tool-Abwesenheit gegen einen
    frischen Host.
12. Endzustandsdokumentation, Runtime-Schema, Agent-Guide, Workflowregel und
    Testnamen enthalten nur den neuen Vertrag und keine
    Migrations-/Historientexte.

## Architektur- und Betriebssemantik

`verify` ist ein dünner öffentlicher Source-Code-Orchestrator, kein zweiter
Linter, keine Assemblyanalyse und kein allgemeiner Query-Interpreter. Er
bestimmt für `changes` den Working-Tree-Kontext selbst oder nutzt für
`solution` die gesamte Solution, ruft die festen Gatequellen auf und
projiziert ihre Ergebnisse in eine gemeinsame, unveränderliche
Entscheidungsantwort. Nur im Änderungskontext ergänzt er die beiden
Kandidatenquellen. Scanner behalten vollständige Domänenresultate; toolnahe
Projektoren wählen deterministisch ganze Evidenzeinheiten.

Die Gateentscheidung besitzt genau einen Owner. Advisory-Evidenz kann niemals
den Gatezustand übersteuern. Bei konkurrierender oder fehlender Analyse
gewinnt konservativ `incomplete`, nicht ein gemittelter Score. Git-Diff,
Staging und relevante neue Dateien sind Teil der dokumentierten
`changes`-Bestimmung. Änderungen an Solution-, Projekt-, Regel- oder
Generator-Konfiguration erweitern den effektiven Analysekontext konservativ;
ein nicht sicher bestimmbarer Kontext wird nicht geraten, sondern als
`incomplete` ausgewiesen.

Keine Analyse führt Zielcode aus, lädt untersuchte Assemblies dynamisch oder
ändert Source, Konfiguration oder Git-Zustand. `verify` ist read-only und
Source-only; Assemblyanalyse bleibt bei den dafür vorgesehenen Tools.

## Umsetzungsplan

### Slice 01 – Öffentlichen `verify`-Vertrag festlegen und rot absichern

- Gemeinsame Request-/Responsemodelle, die beiden Scopewerte, Verdicts,
  Working-Tree-Bestimmung und Evidenzsemantik definieren.
- Contracttests für `pass`, `failed`, `incomplete`, `error`, leeren Diff,
  frühe Validierung, Scope und Handoff zuerst rot schreiben.
- Den extern sichtbaren Toolinventar-Sollzustand festlegen: `verify` vorhanden,
  alle vier Altnamen abwesend; die unberührten Tools bleiben nachweisbar
  registriert.

**Exit:** Der neue Vertrag ist als Testsprache eindeutig; keine Testannahme
referenziert einen Legacy-Adapter.

### Slice 02 – Gate-Kern mit festen `10.0`/`0`-Invarianten implementieren

- Lint-/Scorequelle in einen klaren `verify`-Gateprojektor überführen.
- Scoring- und Violationergebnis gemeinsam, aber ohne Vermischung ihrer
  Aussagegrenzen ausgeben.
- `pass` nur bei beiden festen Bedingungen und ausreichender
  Entscheidbarkeit erlauben; Fehler, feste Antwortprojektion und Navigation
  contractweit vereinheitlichen.

**Exit:** Gatefälle, Fehlerhüllen, Scope-Counts und die feste Projektion sind
gegen einen frischen Host grün und handlungsfähig.

### Slice 03 – Kontextgebundene Kandidaten in `changes` integrieren

- Dead-Code- und Magic-Value-Quellen fachlich als advisory-Projektionen
  anbinden, nicht als versteckte Gatebedingungen.
- Working-Tree-Diff, semantischen Änderungskontext, Confidence,
  Gegenindikatoren, Ranking und Datensparsamkeit für Dead Code und Magic
  Values umsetzen.
- Reihenfolge, feste Projektion, Ausschluss bei `solution` und
  Nichtbeeinflussung des Gateurteils mit fokussierten Tests absichern.

**Exit:** `changes` liefert begrenzte, ehrlich unsichere Evidenz; `solution`
bleibt frei von kontextlosem Kandidatenrauschen.

### Slice 04 – Harter Schnitt durch Registrierung, Produktion und Tests

- Alte Toolregistrierungen, Argumentlimits, Capabilities, DTOs, Formatter,
  Adapter, obsolete Projektoren und ausschließlich zugehörige Tests der vier
  ersetzten Tools entfernen oder auf `verify` umstellen.
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
  `verify(targetPath)` während der Arbeit und
  `verify(targetPath, scope: "solution")` für den Abschluss ersetzen; beide
  verlangen `pass`, `10.0` und `0`.
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
- vollständige `verify`-Dogfoodmatrix für Working-Tree-Änderungen,
  Solution-Abschluss, festen Antwortumfang, Scope, Handoff, Validation,
  leeren oder unsicheren Diff und Source-only-Assemblyfehler;
- Negativsuche nach allen vier entfernten Namen in Produktionsregistrierung,
  öffentlicher Dokumentation, Agentenregeln und Server-Instructions;
- gezielte Architekturprüfung auf tote Adapter, doppelte Vertragsmodelle,
  Magic-Values und nicht verwendete Legacybestandteile;
- ein abschließender read-only MCP-UX-Audit nur für den neuen `verify`-Vertrag
  und seine weiter bestehenden Handoffs.

Der frühere Gateaufruf `safeguard`/`get_violations` wird in diesem Release-Gate
nicht mehr verwendet, weil seine Existenz nach dem harten Schnitt selbst ein
Fehler wäre. Der Abschlussnachweis erfolgt über
`verify(targetPath, scope="solution")`: `verdict=pass`, `score=10.0`,
`violationCount=0` und ausreichende Vollständigkeit.

## Dokumentationsbedarf

- `Docs/agent-api.md`: ein kompakter Vertrag für die beiden Scopewerte,
  Working-Tree-Bestimmung, Verdicts, Gate-/Kandidatensemantik und die feste
  Evidenzprojektion; keine frühere Toolliste als Übergang darstellen.
- `Docs/integration.md` und der Runtime-Agent-Guide: einen Agentenfluss
  `Arbeitsänderungen → verify(targetPath) → Abschluss → verify(targetPath,
  scope: "solution")` beschreiben.
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
| `verify` wird ein schwer verständliches Mega-Tool. | Nur `targetPath` und die zwei Scopewerte; keine Modi, Detektor-, Paging-, Budget- oder Schwellenwertflags. |
| Kandidaten werden fälschlich als Gateblocker gelesen. | Getrennte `verdict`-Ownership, `advisory_candidate`, `requiresAgentJudgment` und keine Kandidatenwirkung auf den Gateentscheid. |
| Der feste Score verdeckt unvollständige Analyse. | `pass` setzt Entscheidbarkeit und ausreichende Completeness voraus; sonst konservativ `incomplete`. |
| Das Entfernen öffentlicher Tools löscht nützliche Fachlogik. | Scanner nur nach klarer Ownership weiterverwenden; öffentliche DTO-/Formatterpfade trotzdem vollständig löschen. |
| Breite Kandidatensuche erzeugt hohe Kosten und Rauschen. | Kandidaten nur im automatisch bestimmten Änderungskontext, stabile Top-Evidenz und feste Antwortprojektion. |
| Ein leerer oder unklarer Git-Diff wird fälschlich als sauber gelesen. | Konservativ `incomplete` und eine einzige Recovery: `scope: "solution"`. |
| Alte Runtime verfälscht Nachweise. | Ausschließlich frischen Taskstand-Host für Wire- und Schema-Verifikation verwenden. |
| Dokumentation oder Regeln behalten alte Quality-Gates. | Negativsuche plus Inventar-Contracttests und abschließende manuelle Diffprüfung. |

## Verworfene Alternativen

- **Bestehende Tools behalten und nur `verify` ergänzen:** Erhält
  Kontextkosten, Mehrdeutigkeit und Cross-Tool-Contractflächen; widerspricht
  dem harten Schnitt.
- **Alle bisherigen Scanner über zahlreiche `verify`-Booleans oder Profile
  schaltbar machen:** Reduziert nur Toolnamen, nicht die agentische
  Entscheidungskomplexität.
- **Dead Code und Magic Values stets in den Gateentscheid einrechnen:**
  Verwandelt bewusst heuristische Hinweise in False-Positive-Blocker und
  ignoriert Featurekontext.
- **Kandidaten immer solutionweit ausführen:** Liefert bei lokaler Arbeit hohes
  Rauschen und keine zuordenbare Entscheidung.
- **Sofort die gesamte MCP-Oberfläche auf vier Universalschnittstellen
  reduzieren:** Vermischt diesen begrenzten Quality-Schnitt mit einer zweiten,
  größeren Produktentscheidung und erhöht unnötig das Risiko.

## Offene Entscheidungen

Keine. Die Parameteroberfläche ist bewusst auf `targetPath` und zwei
Scopewerte begrenzt; fachliche Detailauswahl liegt beim Server. Der Draft
bleibt bis zur ausdrücklichen Nutzerfreigabe `draft`; danach kann ein
Orchestrator innerhalb dieses vollständig beschriebenen Scopes autonom
umsetzen.
