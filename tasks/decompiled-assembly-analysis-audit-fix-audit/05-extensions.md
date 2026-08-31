# `find_assembly_extensions` – MCP-Live-/Vertragsaudit

Datum: 2026-08-31  
Status: abgeschlossen, keine Produktions-/Testcodeänderung

## Audit-Rahmen

Geprüft wurde ausschließlich das metadata-only-Wire-Verhalten von
`find_assembly_extensions`. Die Zielauswahl bestand aus einem aus der lokalen
Source-Konfiguration bzw. dem vorhandenen Ausgabebestand ermittelten
`repo-provided assembly` sowie zwei installierten `vendor assemblies` A und B.
Alle drei Aufrufe verwendeten `targetType=assembly` und absolute
`targetPath`-Parameter; konkrete Pfade und Assemblynamen werden hier nicht
wiedergegeben. Ein Consumer-Projekt wurde nicht verwendet oder erfunden.

Die Antworten aller erfolgreichen Ziele meldeten `origin=decompiled`,
`snapshot=none`, `status/sessionStatus=partial`, `completeness=partial`,
`confidence=medium` und `trust=untrusted`. Das repo-provided Ziel lief mit
Generation 2, die beiden installierten Ziele mit Generation 1. Eine Ausführung
oder Quellmodifikation wurde nicht beobachtet.

## Ergebnisübersicht

| ID | Kategorie / Umfang | Dringlichkeit | Befund | Disposition |
|---|---|---:|---|---|
| EXT-001 | StructuredContent-Projektion, lokal am Tool | P1 | Bei großen Limits stimmt die strukturierte Extension-Liste nicht mit dem vollständigen Text überein; `truncated=false` verschleiert die fehlenden strukturierten Einträge. | Dokumentiert; strukturierte Liste, Counts und Truncation atomar konsistent machen. |
| EXT-002 | Antwort-/Tokenbudget, systemisch am Toolvertrag | P1 | `maxResults` begrenzt die sichtbare Extensionmenge, schützt aber nicht zuverlässig die Gesamtnutzlast; Referenz- und Diagnosemetadaten sind auch ohne Include-References-Parameter vorhanden. | Dokumentiert; Gesamtbudget bzw. harte serverseitige Nutzlastgrenze ergänzen und Metadaten budgetieren. |
| EXT-003 | `receiverType`-Filter, lokal am Tool | P1 | Ein passender und ein absichtlich unmöglicher `receiverType` lieferten dieselbe Assemblymenge; der Filter griff nicht. | Dokumentiert; Filterauswertung gegen den Receiver-Typ korrigieren oder Unsupported-Verhalten explizit melden. |
| EXT-004 | strukturierte Signaturdaten, lokal am Tool | P2 | Signaturen enthalten Parameter und generische Marker nur als Text; strukturierte Parameter-, Generic- oder Constraint-Felder fehlen. | Dokumentiert; strukturierte Parameter-/Generic-/Constraintdaten ergänzen oder deren Fehlen explizit kennzeichnen. |

Kein sicherer weiterer Produktionsbefund wurde aus den Live-Proben abgeleitet.

## Repräsentative Werte

Aus den Antworten wurden ausschließlich für Folgeproben anonymisierte Werte
verwendet: `Extension_1`/`Extension_2`, `Receiver_1`/`Receiver_2`,
`Namespace_1`/`Namespace_2`, `Type_1` und `Member_1`. Die konkreten externen
Bezeichner erscheinen weder in diesem Bericht noch im Committext.

## Probeprotokoll

Größen sind ungefähre Zeichenlängen der MCP-Textblöcke. Sie dienen der
agentischen Budgetbeobachtung und sind keine Tokenmessung des Modells.

| Probe | Ziel / anonymisierte Eingabe | Status | Herkunft / Generation | Completeness / Diagnostics / Truncation | Output-/Tokenbeobachtung | Agentennutzen | Evidenz | Finding / Umfang / Dringlichkeit | Disposition |
|---|---|---|---|---|---|---|---|---|---|
| Initial ungefiltert, `maxResults=3` | alle drei Zielklassen, keine Filter | erfolgreich; repo: 0 Extensions, A/B: 124/193 Gesamtbestand | decompiled / 2 bzw. 1; kein Consumer | jeweils `partial`; Diagnose- und Referenzsummaries vorhanden und gekürzt; A/B strukturierte Liste 1/2 Einträge, Textslice 3 | repo ca. 0,9 k; A ca. 1,8 k; B ca. 1,8 k | guter kleiner Einstieg; Counts dürfen nicht mit sichtbarem Slice verwechselt werden | alle drei Ziele seriell mit gleichem Startlimit geprüft | EXT-001/EXT-002; mehrere Tools / P1 | Folgeabfragen klein halten; Projektion im Produktionsscope prüfen |
| Extension-Name | A mit `Extension_1`, B mit `Extension_2`, jeweils `maxResults=3` bzw. größer | erfolgreich; Trefferbestand A 4, B 1 | decompiled / 1 | A blieb bei 4 Treffern partiell, B bei 1 ohne Extension-Truncation; alle sichtbaren Items `not_decidable` | ca. 1,8–2,1 k; größerer Name-Slice blieb klein | exakte Namen aus der Antwort sind für gezielte Folgeabfragen brauchbar | Nichttreffer beim Namen lieferten 0/0, ohne Fehlerstatus | kein Befund; lokal / P2 | Filtersemantik als Regressionstest beibehalten |
| Namespace | A mit beobachtetem `Namespace_2`, `maxResults=1000`; B mit `Namespace_1` | erfolgreich; A reduzierte auf 111, B lieferte den beobachteten Namespacebestand | decompiled / 1 | `partial`; Textcounts und Nichttreffer unterscheidbar; Diagnose-/Referenzsummaries gekürzt | A ca. 50 k bei 111 Texttreffern; strukturiert dennoch nur 1 Eintrag | Namespace-Filter eignet sich für Drilldown, aber nur zusammen mit Truncation-/Projektionprüfung | absichtlich unbekannter Namespace ergab 0, während ein beobachteter Namespace Treffer lieferte | kein Befund zur Filterwirkung; EXT-001/002 / lokal / P1 | Namespacefilter weiter nutzen, große Limits vermeiden |
| Receiver passend | A mit `Receiver_1`, `maxResults=1000` | formal erfolgreich, aber unveränderte Assemblymenge | decompiled / 1 | `partial`; 124 Textsignaturen, strukturierte Liste 1; Diagnose-/Referenzsummaries unverändert | ca. 53,9 k; gleicher Metadatenrahmen wie ungefiltert | Ergebnis ist nicht als Receiver-Drilldown verlässlich | total 124, Text 124, strukturierte Liste 1 | EXT-003 / Tool lokal / P1 | nicht als wirksamen Filter annehmen |
| Receiver Nichttreffer | A mit absichtlich unbekanntem `Receiver_404`, `maxResults=1000` | formal erfolgreich, gleiche Menge wie beim passenden Receiver | decompiled / 1 | `partial`; gleiche Counts und Truncationprojektion | ca. 53,8 k; keine inhaltliche Reduktion | falsche positive Folgeauswahl möglich | passend vs. Nichttreffer: beide total 124 und 124 Textsignaturen | EXT-003 / Tool lokal / P1 | Produktionskorrektur erforderlich; bis dahin Ergebnis als unfiltered markieren |
| Kombinierte Filter | A: `Extension_1` + `Receiver_404`; B: `Extension_2` + unbekannter Namespace | A lieferte weiterhin den Extension-Namenbestand 4; B ergab 0 | decompiled / 1 | `partial`; Name-/Namespacewirkung sichtbar, Receiverwirkung nicht | ca. 2,1 k bei A, unter 1 k bei B | Kombinationen sind nur teilweise vertrauenswürdig | Receiver-Nichttreffer wurde ignoriert; Namespace-Nichttreffer reduzierte korrekt auf 0 | EXT-003 / Tool lokal / P1 | Kombinationen erst nach Einzelprüfung verwenden |
| Leere/null-ähnliche Filter | B mit `extensionName=""`, `receiverType=""`, `namespace=""`; A mit expliziten Nullwerten | erfolgreich wie ungefiltert; kein `isError=true` | decompiled / 1 | `partial`; keine zusätzliche Fehler- oder Truncationsemantik | B ca. 1,8 k bei kleinem Slice; Metadaten unverändert | Verhalten ist vorhersehbar, aber ein leerer Filter ist nicht als eigener Zustand sichtbar | leere Strings und Nullwerte führten nicht zu falscher Leermenge | kein Befund; lokal / P2 | als „kein Filter“ dokumentieren; kein Fix im Scope |
| `maxResults=1` | A und B, ungefiltert | erfolgreich; sichtbarer Textslice je 1 | decompiled / 1 | `partial`, `truncated=true`; strukturierte Liste je 1 | A ca. 1,3 k, B ca. 1,25 k | geeignetes Startbudget | Limit reduziert sichtbare Signaturen; Diagnose-/Referenzsummaries bleiben vorhanden | kein Befund zur Slice-Wirkung; EXT-002 / lokal / P1 | Standard für erste Probe |
| großes `maxResults` | A/B, ungefiltert, `maxResults=1000` | erfolgreich; Text 124/193 Signaturen | decompiled / 1 | `partial`, aber `truncated=false`; Diagnostics weiterhin total 200/105 mit nur 1 Sample; Referenzsummaries weiterhin gekürzt | A ca. 53,8 k, B ca. 62,6 k; kein globales Budget erkennbar | hohe Tokenlast trotz maschinenlesbarer Partialsicht | strukturierte Liste nur 1/2, Text vollständig wirkend; Referenz-/Diagnosezähler bleiben sichtbar | EXT-001/EXT-002 / systemisch / P1 | harte Nutzlastgrenze und konsistente StructuredContentprojektion vorsehen |
| Generische/klassische Signaturen | A/B ungefiltert groß; zusätzlich gezielter `Extension_2`-Slice | erfolgreich; Extensionmethoden textuell erkennbar | decompiled / 1 | `partial`; A 13/124 und B 20/193 Signaturen mit generischen Markern; keine `where`-Constraints beobachtet | viele parametrisierte Signaturen im Text; strukturierter Slice ohne Parameterfelder | Text ist als Lesefallback brauchbar, nicht als stabile maschinelle API | strukturierte Extensionobjekte enthalten nur Namespace, deklarierenden Typ, Name, Signatur, Receiver und Applicability | EXT-004 / Tool lokal / P2 | Signatur nur als Fallback parsen; fehlende Constraints nicht als „keine Constraints“ werten |
| Wiederholung | identischer A-Name-Slice und B-Name-Slice zweimal seriell | erfolgreich und bytegleich im normalisierten StructuredContent | decompiled / gleiche Generation 1 | gleiche `partial`- und `not_decidable`-Semantik; gleiche Counts/Truncation | identische Textlängen je Paar | Retry-/Cache-Entscheidungen innerhalb eines Snapshots möglich | A und B: strukturierte Ergebnisse gleich, Generation unverändert | kein Befund; systemisch / P2 | keine zusätzliche Maßnahme |

## Applicability, fehlende Dependencies und metadata-only

Die Antwort verwendet `applicability` als Feld pro Extensionobjekt; separate
Kategorienarrays für `applicable`, `not_applicable` und `not_decidable` wurden
nicht geliefert. Da kein Consumer-Projekt zulässig bzw. vorhanden war, waren
alle beobachteten Extensions `not_decidable`. Es wurde keine Extension fälschlich
als `applicable` oder `not_applicable` ausgegeben. Die positive Trennung aller
Dreiwegefälle ist unter dieser Randbedingung nicht vollständig testbar; das ist
kein Fehlklassifikationsbefund.

`completeness=partial`, `sessionStatus=partial`, Diagnosezählungen mit
Truncation sowie Referenzzählungen mit gekürzten Listen waren auch bei kleinen
Antworten sichtbar. Fehlende oder nicht auflösbare Dependencies bleiben damit
agentisch erkennbar, erlauben aber keine globale Negativaussage.

Ein `includeReferences`-Parameter ist für dieses Tool nicht registriert.
Referenzsummaries und begrenzte Diagnosemetadaten erscheinen daher
unconditionally im Ergebnis. Die beobachtete starke Größensteigerung bei
`maxResults=1000` korreliert primär mit den ausgegebenen Signaturzeilen; die
Referenz-/Diagnoseblöcke blieben in den Proben gezählt und gekürzt. Sie bilden
aber eine feste Antwortgrundlast, selbst bei 0 Treffern oder Nichttreffern.

Die Assembly wurde ausschließlich als metadata-only-Ziel adressiert. Die
Antworten weisen dekompilierte, untrusted und partielle Herkunft aus; eine
Ausführung wurde nicht beobachtet. Ein maschinenlesbarer eigener
`metadataOnly`-/Load-Attest wurde in diesem Toolergebnis nicht geliefert.

## Kategorisierte Findings

### EXT-001 – StructuredContent verliert Einträge ohne sichtbare Truncation

- **Umfang:** `find_assembly_extensions`, alle großen Assembly-Slices.
- **Dringlichkeit:** P1, systemisch am Wirevertrag.
- **Evidenz:** Bei A meldete die Antwort `totalExtensions=124` und `truncated=false`,
  enthielt im Text 124 Signaturen, aber strukturiert nur 1 Extensionobjekt.
  Bei B waren es 193 Textsignaturen gegenüber 2 strukturierten Objekten.
- **Agentennutzen/Risiko:** StructuredContent-only-Agenten verlieren den
  überwiegenden Teil der Ergebnisse und erhalten mit `truncated=false` keinen
  Warnhinweis.
- **Disposition:** Dokumentiert; Arraylänge, `shown`-Count und Truncation müssen
  atomar zusammenpassen oder die strukturierte Projektion muss einen eigenen
  Truncationgrund ausweisen.

### EXT-002 – `maxResults` ist kein Gesamtbudget

- **Umfang:** Antwortbudget, Referenz- und Diagnoseprojektion.
- **Dringlichkeit:** P1, systemisch.
- **Evidenz:** A wuchs von ca. 1,3 k Zeichen bei `maxResults=1` auf ca. 53,8 k
  bei 1000; B von ca. 1,25 k auf ca. 62,6 k. Referenzsummaries und
  Diagnosezähler/-Truncation waren unabhängig vom fehlenden
  `includeReferences`-Parameter vorhanden.
- **Agentennutzen/Risiko:** Kleine Limits sind ein brauchbarer Einstieg, aber
  große Limits können Antwort- und Tokenbudgets unerwartet verbrauchen.
  `partial`, Counts und Diagnose-/Referenzkürzungen bleiben sichtbar.
- **Disposition:** Dokumentiert; separates Gesamtbudget oder serverseitige
  harte Obergrenze ergänzen und den Metadatenanteil explizit budgetieren.

### EXT-003 – `receiverType` wird nicht wirksam gefiltert

- **Umfang:** Receiverfilter und Filterkombinationen.
- **Dringlichkeit:** P1, lokal am Toolvertrag.
- **Evidenz:** Der passende `Receiver_1` und der absichtlich unbekannte
  `Receiver_404` lieferten auf A jeweils 124 Gesamt-/Texttreffer. Eine
  Kombination mit `Extension_1` blieb trotz unbekanntem Receiver bei 4
  Treffern; ein unbekannter Namespace reduzierte dagegen korrekt auf 0.
- **Agentennutzen/Risiko:** Ein Agent kann eine unfiltered Liste fälschlich als
  Receiver-spezifischen Trefferbestand interpretieren.
- **Disposition:** Dokumentiert; Receiverfilter anwenden oder im Ergebnis
  eindeutig als nicht unterstützt/ignoriert kennzeichnen.

### EXT-004 – Parameter-, Generic- und Constraintdaten nicht strukturiert

- **Umfang:** Extensionobjekte und generische Signaturen.
- **Dringlichkeit:** P2, lokal am Toolvertrag.
- **Evidenz:** Strukturierte Objekte enthielten nur sechs Kernfelder; kein
  `parameters`, Generic- oder Constraintfeld. Textsignaturen enthielten
  generische Marker (A 13/124, B 20/193), aber keine beobachteten `where`-
  Constraints. Ein gezielter generischer Extensionname zeigte die Marker nur
  im Textslice.
- **Agentennutzen/Risiko:** Klassische Signaturen sind lesbar und Parameter
  können notfalls aus Text geparst werden; maschinelle Parameter-/Constraint-
  Folgeabfragen bleiben jedoch unsicher. Wegen `partial` ist das Fehlen eines
  Constraints kein Beweis, dass im Original keines existiert.
- **Disposition:** Dokumentiert; strukturierte Parameter, Generic-Arity/-typen
  und Constraints ergänzen oder die Unsicherheit explizit ausweisen.

## Kein Befund / Grenzen

- Extension-Name- und Namespacefilter griffen in den geprüften positiven und
  negativen Proben; Nichttreffer wurden als erfolgreiche 0-Menge und nicht als
  Analysefehler ausgegeben.
- Leere Strings und explizite Nullwerte verhielten sich wie „kein Filter“ und
  erzeugten keine irreführende Leermenge. Das Verhalten ist klar reproduzierbar.
- Alle beobachteten Extensionobjekte waren ohne Consumer korrekt
  `not_decidable`. `applicable` und `not_applicable` konnten ohne Consumer
  nicht erzeugt und damit nicht positiv gegeneinander geprüft werden.
- Wiederholungen desselben Aufrufs waren innerhalb der unveränderten
  Assembly-Generation stabil. Die drei Zielklassen zeigten dieselbe
  decompiled/partial/not_decidable-Grundsemantik.
- Keine Produktions-/Testdatei wurde geändert; kein Build, kein Testlauf und
  kein Push wurde ausgeführt.

### Commit-Vorschlag

docs: dokumentiere Extension-Vertragsproben
