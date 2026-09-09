---
status: ready
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
  - tasks/03-mcp-development-workflow/Konzept.md
  - tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md
---

# Task 04: Verlässliche Assembly-Analyse für MCP-Agenten

## Ziel

Ein MCP-Agent kann eine lokale verwaltete `.dll` oder `.exe` als eindeutig
gebundenen, unveränderlichen Analyse-Snapshot erkunden. Katalog, Suche,
Typdetail, Member, Body, Referenzen, Call-Tree, Herkunft und Extensions
verwenden dieselbe Target-, Snapshot-, Status- und Handoff-Wahrheit. Der
Agent erhält nur Aussagen, deren Scope und Evidenzgrenze er unmittelbar aus
der Antwort ablesen kann.

Der MCP-Vertrag besitzt genau einen Assemblyeinstieg: Jeder zielgebundene Call
enthält den absoluten `targetPath`. Die Endung `.dll` oder `.exe` bestimmt
`origin=decompiled`. Der Server liest ausschließlich Metadaten und
dekompiliert statisch; er führt die Datei weder aus noch benötigt er ein
Consumerprojekt oder eine Source-Lösung.

## Problem und Agentensicht

Eine Assemblyanalyse ist nur handlungsfähig, wenn ein Agent für jeden Call
eindeutig erkennt:

1. Welche Datei und welche Bytes der Snapshot repräsentiert.
2. Welcher Treffer ein kopierbarer Folgeinput ist, für welches Target und
   welchen Snapshot er gilt und welche Tools ihn annehmen.
3. Ob ein Ergebnis leer, begrenzt, unvollständig, nicht entscheidbar oder
   fehlerhaft ist.
4. Was bei Dateimutationen, Ablauf, Kapazitätsdruck und konkurrierenden
   Aufrufen geschieht.
5. Welche Aussage ausschließlich statische Evidenz ist.

Task 04 behebt alle Befunde dieser Fragen in der Assemblyoberfläche. Es
entsteht kein weiterer Arbeitsauftrag.

## Scope und Ownership

Task 04 besitzt die Assembly-Fachsemantik und ihre vollständige Projektion:

- Resolver und Validierung für Assembly-Targets;
- `inspect_assembly`, `search_assembly`, `get_assembly_context`,
  `find_assembly_extensions`, `resolve_type_origin` sowie die
  Assemblyzweige der allgemeinen Symbol-, Struktur-, Body-, Referenz-,
  Call-Tree-, Impact- und Metriktools;
- Snapshot, Handoff-ID, `continuationToken`, Decompilerdiagnostik,
  Ergebnisbudget, Registry, Lease, Ressourcenbudget, TTL, Eviction und den
  Assemblyanteil von `get_server_health`;
- Registrierung, `tools/list`, Runtimevalidierung, StructuredContent,
  Textprojektion, Resources, Server-Instructions, aktive Dokumentation,
  Fixtures und Tests dieser Oberfläche.

Der Rahmen für `targetPath`, `navigation`, `operationStatus`,
`completeness`, `next`, Handoff und Antwortbudgets wird für Assemblys präzise
angewandt. Source-Workflow, Source-Lint, Git-Analyse, Testausführung,
Runtime-Coverage und Änderungen der Source-Fachpayloads liegen außerhalb des
Scopes.

## Source of Truth

Ein maschinenprüfbarer Assembly-Abschnitt des Vertragskatalogs ist die
normative Quelle. Er enthält für jede Toolfamilie:

- Required- und Optional-Inputs, Typen, Defaults, serverseitige Caps und
  ausgeschlossene Properties;
- Capabilitymatrix für Source und `origin=decompiled`;
- den verbindlichen `navigation`-Kern und den fachlichen Payloadpfad;
- ID-, Continuation-, Scope-, Count-, Ranking-, Budget- und
  Truncation-Semantik;
- Status, Fehlercode, Ursache, betroffenen Abschnitt und genau einen
  passenden `next`-Wert;
- die zulässige Textprojektion.

Registrierung, SDK-Schema, Runtimevalidator, StructuredContent, Formatter,
Resources, Server-Instructions, Dokumentation und Tests leiten ihre Aussage
aus diesem Katalog ab oder prüfen dagegen. Text fasst zusammen; er verändert
keine ID, keinen Count, keinen Status und keinen nächsten Schritt.

## Zielarchitektur und Betriebsannahmen

### Target und Snapshot

1. Der Resolver akzeptiert nur einen existierenden lokalen absoluten Pfad zu
   `.dll` oder `.exe`. Er normalisiert Laufwerk und Separatoren und löst
   Linkziele auf. Der daraus entstehende kanonische Pfad ist die
   Target-Identität.
2. Vor jeder Materialisierung wird die Datei als verwaltete .NET-Assembly mit
   IL validiert. Eine native, beschädigte, gesperrte oder nicht lesbare Datei
   ist ein Fehler mit präzisem Fehlercode und niemals ein verfügbares,
   vollständiges Analyseergebnis.
3. Der Snapshot besteht aus kanonischem Pfad und kryptographischem
   Content-Hash der tatsächlich gelesenen Bytes. Dateiname, Assemblyname,
   Version, Zeitstempel und Registry-Generation sind keine
   Snapshot-Identität.
4. Das Öffnen erfasst Dateizustand, Hash und Metadaten in einer stabilen
   Leseoperation. Ändern sich Zustand oder Hash während der Materialisierung,
   wird kein Snapshot veröffentlicht. Der Call liefert `stale_snapshot` mit
   einem erneuten Katalogcall als einzigem nächsten Schritt.
5. Ein veröffentlichter Snapshot ist unveränderlich. Jeder Folgecall prüft
   seine Target- und Snapshotbindung, bevor er den fachlichen Payload erzeugt.
   Stimmt der aktuelle Content-Hash nicht, liefert er `stale_snapshot`; er
   verwendet keine Daten eines anderen Hashes.
6. Die Antwort benennt den validierten Snapshot und den Zeitpunkt der
   Validierung. Sie ist eine statische Aussage über diesen Snapshot, keine
   Aussage über einen späteren Dateizustand.

### Registry, Lease und Ressourcen

Die Assembly-Registry ist alleinige Besitzerin von dekompiliertem Workspace,
temporärem Materialisat und zugehörigen Ressourcen. Project- und
Assembly-Registry besitzen getrennte Schlüssel, Kapazitäten, TTLs und
Eviction. DTOs, Handoff-IDs und Continuations übertragen keine Lease-,
Session-, Generation- oder Besitzinformation.

- Ein Registry-Key besteht aus kanonischem Pfad und Content-Hash. Verschiedene
  Pfade bleiben getrennte Targets, auch bei gleichen Bytes.
- Jede Anfrage hält bis zur vollständigen Antwortprojektion einen Lease. Ein
  Snapshot mit aktivem Lease wird weder entsorgt noch aus seinem
  Ressourcenbudget entfernt.
- Nach Lease-Freigabe ist ein Entry bis zur konfigurierten Idle-TTL resident.
  Ablauf und Kapazitätsbereinigung wählen ausschließlich leasefreie Entries
  in deterministischer LRU-Reihenfolge mit kanonischem Pfad als Tie-Breaker.
- Disk-, Memory-, Resident- und Parallelitätsbudget werden vor der
  Materialisierung reserviert und bei jeder fehlgeschlagenen, abgebrochenen
  oder abgeschlossenen Erzeugung vollständig freigegeben.
- Ein Snapshot, dessen Budget größer als die konfigurierte Einzelkapazität
  ist, wird nicht materialisiert. Ausgeschöpfte Kapazität ergibt den
  Fehlercode `capacity_exhausted`; `next` fordert die Wiederholung desselben
  Calls nach Freigabe von Kapazität. Der Server verdrängt keinen aktiven
  Lease.
- TTL und Eviction ändern nur die Residenz. Eine gültige Handoff-ID bleibt
  durch dieselben Target- und Snapshotprüfungen gültig; der Server kann den
  identischen Snapshot erneut materialisieren. Nur abweichende Bytes führen
  zu `stale_snapshot`.

### Parallelität und Isolation

Für denselben Registry-Key gibt es genau eine Materialisierung. Gleichzeitige
Erstaufrufe teilen deren Ergebnis; die Abbruchanforderung eines Callers löst
nur dessen Wartevorgang und beendet nicht die geteilte Erzeugung. Aufrufe für
andere Keys teilen keine Workspaces, IDs, Diagnostics oder Lease-Zähler.

Die Parallelitätsgrenze begrenzt ausschließlich neue Materialisierungen.
Lesende Calls auf einen fertigen Snapshot bleiben parallel zulässig. Eine
Referenzexpansion besitzt das Root-Lease, begrenzt Tiefe und Zahl der
geöffneten Referenz-Sessions und gibt alle Child-Leases zusammen mit dem
Root-Lease frei. Kein Ergebnis aus einer Referenzsession wird als Ergebnis des
Root-Assembly-Snapshots ausgegeben.

`get_server_health` ohne `targetPath` liefert ausschließlich serverweite
Zähler, Kapazitätszustand und begrenzte Fehlerzähler. Es enthält weder
Targetpfade, Hashes, Konfigurationspfade, Diagnostics, Generationen noch
Lease-Details anderer Agents. `includeSessions` ist kein öffentlicher Input.
`get_server_health(targetPath=...)` liefert ausschließlich die
Statusprojektion dieses einen Targets. Health beobachtet Zustand und erzeugt
keine Lease-Verlängerung.

### Sicherheit und Datenhygiene

Die Assembly wird nur als Datei und Metadaten-/Decompilerinput verarbeitet;
es gibt keine Assemblyladung zur Ausführung, keinen `AssemblyLoadContext`,
keine Initialisierung und keine Netzwerkanfrage. Jeder Assembly-Snapshot wird
ausschließlich dekompiliert; eine Sourcezuordnung beeinflusst weder Herkunft
noch Inhalt der Assemblyantwort.

Materialisate liegen in einem servereigenen, nicht vom Assemblynamen
abgeleiteten Pfad. Erzeugte relative Pfade werden gegen Traversal geprüft.
Öffentliche Payloads enthalten keine Materialisat-, Workspace-, Cache- oder
Generationspfade.

Decompilertext, Metadaten, Typnamen, Attribute, Strings und Diagnostics sind
Nutzdaten. Sie können keine Toolwahl, Statussemantik, Server-Instructions
oder Agentenanweisungen verändern. Diagnostics sind normalisiert,
größenbegrenzt, ohne Geheimnisse und ohne vollständige Fremdpfade. Der
globale Health-Payload enthält keine Diagnose-Samples.

## Öffentlicher Agentenvertrag

### Einstieg und Toolwahl

Der Agent liest zuerst `tools/list` und verwendet ausschließlich die dort
veröffentlichten Properties. Er adressiert die konkrete DLL/EXE in jedem
zielgebundenen Call mit exakt einem absoluten `targetPath`.

| Anliegen | Tool | Ergebnis für den Agenten |
| --- | --- | --- |
| Öffentliche API und Typkatalog | `inspect_assembly` | Gerankte Typen mit Handoff-ID, Member-Count, Status und Detail-Hinweis. |
| Text, Deklarationen, Daten- oder externe Aufrufmuster | `search_assembly` | Treffer mit Scope, Handofffähigkeit, Count und gebundener Fortsetzung oder Verfeinerung. |
| Typ oder Member fachlich einordnen | `get_assembly_context` | Begrenzte Abschnitte für Deklaration, Struktur, Body, Referenzen, Callers, Impact und Metriken. |
| Klassische C#-Extension finden | `find_assembly_extensions` | Treffer oder ein fachlich eindeutiger Extension-Zustand. |
| Herkunft eines Typs bestimmen | `resolve_type_origin` | Statische definierende Assembly und Auflösungsgrenze. |
| Ein ausgegebenes Symbol vertiefen | allgemeine Symbol-, Struktur-, Body-, Referenz-, Call-Tree-, Impact- oder Metriktools | Nur mit der ID aus StructuredContent und demselben `targetPath`. |

Assembly-spezifische Tools mit einem Source-Target und Source-spezifische
Operationen mit einem Assembly-Target liefern `unsupported`, Capability und
einen passenden nächsten Schritt. Sie liefern weder eine Leermenge noch
wechseln sie das Target.

### Navigation, IDs und Continuations

Jede erreichbare zielgebundene Antwort enthält
`navigation.target`, `origin`, `snapshot`, `capabilities`,
`operationStatus`, `result`, `completeness` und `next`. Der fachliche Payload
enthält pro navigierbarem Treffer:

- `handoff=true`;
- eine kanonische, opaque Handoff-ID;
- Symbolart, Target- und Snapshotbindung;
- die erlaubten Folge-Tools.

Eine Assembly-Handoff-ID bindet kanonischen Targetpfad, Content-Hash und
kanonische DocComment-ID. Sie bleibt bei identischem Target und identischen
Bytes über einen Serverneustart hinweg gleich. FQN, Anzeigename, Pfad,
Zeilennummer, Decompilername und Textzeile sind keine Folgeinputs.

`continuationToken` ist die einzige Continuation-Property. Sie ist opaque und
bindet Target, Snapshot, Tool, Query, Filter, Sortierung und Cursorposition.
Sie wird ausschließlich bei deterministischer Sortierung ausgegeben und
validiert jedes gebundene Feld erneut. Ungültige Token liefern
`invalid_argument`; ein Hashwechsel liefert `stale_snapshot`. Wo keine stabile
Fortsetzung möglich ist, enthält `next` eine Scope-, Filter- oder
Detailverfeinerung statt eines Tokens.

### Status, Fehler und Aussagegrenzen

`operationStatus=ok` beschreibt die erfolgreiche Ausführung, nicht die
Trefferzahl. `completeness` gilt immer für ein benanntes Ergebnis oder einen
benannten Composite-Abschnitt.

| Fall | Verpflichtende Antwort | Erwartete Agentenaktion |
| --- | --- | --- |
| Vollständig geprüfte Leermenge | `ok`, `empty`, `totalCount=0`, geprüfter Scope | Scope gezielt ändern, wenn eine andere Frage besteht. |
| Begrenzte Ergebnismenge | `truncated`, `totalCount`, `returnedCount`, `truncatedBy`, genau ein `next` | Den angegebenen Detail-, Filter- oder Continuation-Schritt ausführen. |
| Fehlende Metadaten, PDB, Referenz oder dekompilierbarer Body | betroffener Abschnitt `partial` oder `not_decidable`, Ursache und Evidenzgrenze | Nur den bezeichneten Teil als unentschieden behandeln. |
| Nicht verwaltete, beschädigte oder nicht lesbare Datei | `error` mit `invalid_assembly` oder `target_unreadable`, Fehlerphase und nächstem Schritt | Ein lesbares .NET-Assembly-Target angeben. |
| Nicht vorhandenes Symbol im gültigen Snapshot | fachlicher Not-found-Code, `result.available=false`, Scope und nächster Schritt | ID, Schreibweise oder Suchscope prüfen. |
| Mehrdeutiger Symbolinput | fachlicher Ambiguitätscode mit begrenzten Kandidaten | Eine ausgegebene kanonische Handoff-ID wählen. |
| Ungültiger Pfad, Property oder Token | `invalid_argument`, Feldpfad und nächster Schritt | Ausschließlich das veröffentlichte Schema verwenden. |
| Falsches Target für ID oder Tool | `target_mismatch` oder `unsupported` mit Capability | Originaltarget verwenden oder ein unterstütztes Tool wählen. |
| Geänderte Targetbytes | `stale_snapshot` mit Snapshotbezug und neuem Katalogcall | Vom Katalog des aktuellen Snapshots neu beginnen. |
| Ressourcen- oder Parallelitätsgrenze | `error` mit `capacity_exhausted` und Kapazitätsgrund | Den identischen Call nach verfügbarer Kapazität wiederholen. |

Eine fehlende Referenz, eine leere Caller-Liste, ein nicht vorhandener Body
oder eine nicht gefundene Extension beweist nur den jeweils ausgewiesenen
Scope. Ohne Consumerprojekt lautet die Anwendbarkeit einer Extension
`not_decidable`. Assemblyanalyse behauptet weder Source-Herkunft,
Originallinien, Laufzeitverhalten, Consumerverwendung, globale
Referenzfreiheit noch Lintsauberkeit.

### Ergebnisbudget und Determinismus

Die serverseitigen Caps begrenzen Eingaben auch dann, wenn ein Agent höhere
Werte sendet. Die Standardantworten priorisieren Typname, Symbolart,
Handoff-ID, Count, Scope und Status vor Memberdetails, Bodies, Kontext und
Diagnostics. Für jeden begrenzten Abschnitt gelten die Rahmenbudgets; der
Katalog hat Vorrang vor Detaildaten und Diagnostics folgen fachlichen Daten.

Sortierung, Ranking, Count und `truncatedBy` sind für denselben Snapshot und
dieselbe Anfrage deterministisch. Der Katalog verwendet eine definierte,
dokumentierte Reihenfolge mit stabiler Tie-Break-Regel. `totalCount` ist der
ermittelte Gesamtcount oder der Abschnitt ist `partial` mit einer ausdrücklich
benannten Count-Grenze; ein geschätzter Wert wird nicht als Gesamtcount
ausgegeben. Ein Abschnitt besitzt genau einen Vollständigkeitszustand;
`partial` und `truncated` werden nicht für dieselbe Ergebnismenge parallel
projiziert.

## Muss-Kriterien

- Jeder Assembly-Call verwendet ausschließlich `targetPath`; der Resolver
  unterscheidet ungültiges Argument, unlesbares oder nicht verwaltetes Target,
  Capabilityfehler, Targetmismatch, Snapshotwechsel, Kapazitätsgrenze und
  fachliche Leermenge.
- Alle Assemblytools und Resources zeigen dieselben Target-, Origin-,
  Snapshot-, Capability-, Status-, Completeness-, Count- und
  Handoffinformationen in Text und StructuredContent.
- Ein Katalog-, Such-, Kontext-, Struktur-, Body-, Referenz- oder
  Call-Tree-Treffer ist nur mit `handoff=true` und kanonischer ID
  navigierbar. Jeder nicht navigierbare Treffer enthält Grund und nächsten
  Schritt.
- Für identischen kanonischen Pfad und Content-Hash sind Handoff-IDs,
  Sortierung und `continuationToken` reproduzierbar; Registry-Generation,
  Prozess-ID, Lease-Zähler und Materialisatpfad sind kein Agenteninput.
- Ein Content-Hashwechsel trennt den Snapshot atomar. Kein Detailcall nutzt
  Daten eines anderen Hashes oder meldet den Fall als Symbolleere.
- Die Registry hält aktive Leases, reservierte Ressourcen, abgebrochene
  Materialisierungen, TTL, Eviction und konkurrierende Erstöffnungen ohne
  Leak, Doppelmaterialisierung, aktive Verdrängung oder Cross-Target-Leak
  korrekt.
- `includeReferences=true` bleibt statisch, begrenzt und transparent:
  Root- und Referenzsession, Tiefe, Count, Diagnostics und Teilzustand sind
  getrennt sichtbar.
- Decompiler- und Metadatenfehler sind begrenzte Nutzdaten oder präzise
  Fehler; sie dürfen Katalog, Handoff, Counts oder Status anderer Abschnitte
  nicht verfälschen. Diagnostic-Counts benennen die tatsächliche Anzahl und
  sind von der Zahl sichtbarer Samples getrennt.
- Jede Assemblyantwort hat ausschließlich `origin=decompiled`, enthält keine
  Materialisat- oder Cachepfade und veröffentlicht keine Generation.
- `get_server_health` schützt parallele Agenten vor fremden Target- und
  Diagnosedaten und verlängert keine Session-Lebensdauer.
- Aktive Runtime, Schema, DTOs, Registrierungen, Formatter, Fixtures, Tests,
  Dokumentation und Regeln enthalten nur den Assemblyvertrag dieses Konzepts.

## Messbare Akzeptanzkriterien

1. Eine Raw-Wire-Kette `inspect_assembly → get_assembly_context →
   get_symbol_body → get_call_tree` kopiert ausschließlich IDs aus
   StructuredContent. Eine zweite Kette `search_assembly →
   get_class_structure → find_references` beweist denselben Vertrag.
2. Vier Fixtures decken eine verwaltete Assembly, fehlende Referenzen oder
   PDB, eine nicht verwaltete EXE und eine beschädigte oder nicht lesbare
   Eingabe ab. Für jede Fixture sind `origin`, Snapshot, Capabilities, Status,
   betroffener Abschnitt, Diagnostics-Grenze und nächster Schritt als
   Wire-Assertion festgelegt.
3. Zwei gleichnamige Assemblies an verschiedenen kanonischen Pfaden,
   identische Bytes an verschiedenen Pfaden, ein Bytewechsel am selben Pfad
   und ein Linkpfad beweisen Targettrennung, Snapshotbindung,
   Handoff-Reproduzierbarkeit und `stale_snapshot`.
4. Parallele Erstöffnung desselben Keys beweist genau eine Materialisierung;
   parallele Öffnungen verschiedener Keys beweisen Kapazitätsgrenzen,
   Isolation und vollständige Ressourcenfreigabe. Ein Abbruch eines Wartenden
   beendet die geteilte Materialisierung nicht.
5. Tests erzwingen, dass TTL- und LRU-Bereinigung nur leasefreie Entries
   entfernt, dass ein aktiver Lease seinen Snapshot bis zur
   Antwortprojektion hält und dass ein späterer gleichbyteiger Call denselben
   Handoffvertrag erhält.
6. Tests für `invalid_argument`, `unsupported`, `target_mismatch`,
   `stale_snapshot`, `capacity_exhausted`, `invalid_assembly`,
   `target_unreadable`, Not-found, Ambiguität, `empty`, `partial`,
   `truncated` und `not_decidable` prüfen exakten Code, Feld oder Abschnitt,
   Ursache und genau einen nächsten Schritt.
7. Eine große Fixture beweist, dass Katalogzeilen mit ID und Count innerhalb
   der publizierten Wirebudgets vor Details und Diagnostics erhalten bleiben.
   Begrenzte Listen besitzen wahre Counts und Truncation-Ursachen. Kein
   Ergebnisabschnitt enthält gleichzeitig `partial` und `truncated`.
8. `includeReferences=false` und `includeReferences=true` beweisen jeweils
   Root-Scope beziehungsweise begrenzten Referenzscope. Eine fehlende
   Referenz oder Caller-Leermenge erzeugt keine globale Negativaussage.
9. Globales Health zeigt keine Targetdaten anderer Agents, auch nicht durch
   optionale Sessionparameter; zielgebundenes Health zeigt nur das angefragte
   Target. Beide Formen sind durch Schema-, StructuredContent- und Texttests
   abgesichert.
10. `tools/list`, Runtimevalidator, StructuredContent, Formatter, Resource,
    Server-Instructions und aktive Dokumentation stimmen je Tool bei Inputs,
    Enums, Status, IDs, Budgets und nächsten Schritten überein. Dabei sind
    `cursor`, `isTruncated`, öffentliche Generationen, Sourcezuordnungen und
    Materialisatpfade nicht veröffentlicht.

## Non-Goals

- Ausführung, dynamisches Laden oder Initialisieren einer Assembly;
- Source-, PDB-, Originallinien-, Consumer-, Runtime-, Coverage- oder
  Lintbeweis aus dekompilierten Daten;
- Consumerprojekt-Erzeugung, Build, Testausführung, Git-Analyse oder
  Assembly-Refactoring;
- unbegrenzte Referenzauflösung, globale Aussage über Aufrufer oder
  Referenzfreiheit und vollständige Analyse dynamischer Auflösung;
- Zusammenlegung von Project- und Assembly-Registry;
- allgemeine Änderungen an Source-Workflows, Quality-Heuristiken oder
  Lintregeln.

## Risiken und verbindliche Entscheidungen

| Risiko | Entscheidung |
| --- | --- |
| Große oder beschädigte Eingaben binden CPU, Speicher und Disk. | Vorabreservierung, feste Parallelitäts- und Ressourcenlimits, aktive Leases und begrenzte Antworten schützen Server und Agentenschleife. |
| Gleichzeitige Mutationen können Snapshotdaten vermischen. | Stable-read-Prüfung, Hashbindung, unveröffentlichte abgebrochene Materialisierung und `stale_snapshot` verhindern Mischzustände. |
| Decompilerdiagnostik verdrängt handlungsfähige Daten oder führt Instruktionen ein. | Katalog und Handoffs werden priorisiert; Diagnostics sind begrenzte, als Daten behandelte Nebeninformation. |
| Ein globaler Health-Payload offenbart Aktivitäten anderer Agents. | Globales Health enthält nur Aggregate; Targetdetails sind ausschließlich nach exakter Targetbindung sichtbar. |
| Ein einziger allgemeiner DTO-Typ verschleiert Toolfachlichkeit. | Ein schmaler gemeinsamer Navigationkern ergänzt klar getrennte Toolpayloads. |
| Unklare Extension-Leermengen führen zu falschen Integrationsentscheidungen. | `none_in_assembly`, `receiver_unresolved`, `not_applicable` und `not_decidable` sind getrennte, getestete Ergebniszustände. |

## Verifikation

Die Umsetzung beginnt mit MCP-first-Kontext für betroffene C#-Symbole und
führt den Assembly-Vertragskatalog vor jeder Runtimeänderung fort. Erforderlich
sind Unit-, Component-, Integration-, Raw-Wire-, Schema-, Formatter-,
StructuredContent-, Lifecycle-, Concurrency-, Budget-, Security- und
Dogfood-Tests. Lastintensive Paralleltests werden ausschließlich mit der
Kategorie `Stress` markiert und nur auf ausdrücklichen Auftrag ausgeführt.

Bei Produktions- oder Testcodeänderungen sind vor Abschluss verbindlich:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Zusätzlich sind ein frisch gestarteter Assembly-Dogfood-Workflow, ein aktiver
Contract-Scan, ein Vergleich von Text und StructuredContent und
`git diff --check` erforderlich. Fehlschläge oder abgeschnittene Ausgaben
werden mit TRX diagnostiziert.

## Dokumentationsbedarf

Aktualisiert werden `Docs/agent-api.md`, `Docs/integration.md`,
`Docs/configuration.md`, `Docs/ROADMAP.md`, MCP-Resources,
Toolbeschreibungen, Server-Instructions und
`.agents/rules/AiNetLinter-McpWorkflow.mdc`. Sie beschreiben ausschließlich:

- die Auswahl eines absoluten verwalteten Assembly-Targets;
- die Capabilitymatrix und den statischen Evidenzumfang;
- Snapshot-, ID-, Continuation-, Stale-, Fehler- und Lebenszeitsemantik;
- Registry- und Health-Isolation, Ressourcenlimits und Agentenverhalten bei
  Kapazitätsgrenzen;
- je ein vollständiges Katalog-zu-Detail- und Such-zu-Referenz-Beispiel mit
  StructuredContent-Handoff.

Der Vertragskatalog bleibt die einzige vollständige Schemabeschreibung.

## Release-Gate

Der Release ist blockiert, sobald einer dieser Befunde verbleibt:

- ein Tool, eine Resource, Text oder StructuredContent weicht vom
  Vertragskatalog ab;
- eine ID, `continuationToken` oder Antwort ist nicht eindeutig an Target und
  Snapshot gebunden;
- ein nicht verwaltetes Target, Snapshotwechsel, Targetmismatch,
  Capacity-Fall, Decompilerfehler, Referenzgrenze oder eine Leermenge erhält
  einen falschen Status oder eine falsche Aussagegrenze;
- ein aktiver Lease wird entsorgt, eine parallele Eröffnung erzeugt doppelte
  Materialisierung oder eine Registrierung gibt Ressourcen nicht frei;
- globales Health zeigt Daten eines anderen Targets oder Diagnostics ohne
  angeforderten Targetbezug;
- eine Antwort veröffentlicht Registry-Generation, Cache-, Workspace- oder
  Materialisatpfad, Sourcezuordnung oder mehr als eine Continuation-Form;
- ein begrenztes Ergebnis verliert Katalognavigation, wahre Counts oder den
  genau einen sicheren nächsten Schritt;
- eine Assemblyantwort behauptet Source-, Consumer- oder Runtimewissen;
- Build, Nicht-Stress-Suiten, Raw-Wire-, Dogfood-, Dokumentations-,
  Contract- oder Diff-Gate ist nicht grün.

## Freigabestatus

Die technische und fachliche Entscheidung ist vollständig beschrieben;
`open_questions: []`, `execution_mode: autonomous` und die ausdrückliche
Nutzerfreigabe gelten. Der Status ist `ready`; die Umsetzung darf autonom
über alle Slices orchestriert werden.
