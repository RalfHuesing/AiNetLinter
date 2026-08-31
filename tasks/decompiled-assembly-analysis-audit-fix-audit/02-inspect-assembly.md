# inspect_assembly – MCP-Live-/Vertragsaudit

## Audit-Rahmen

- Zeitpunkt: 2026-08-31
- Ziel: ausschließlich agentisches Wire-Verhalten des metadata-only-
  Assembly-Inspektors.
- Targets: `repo-provided assembly`, `installed vendor assembly A`,
  `installed vendor assembly B`. Alle drei erfolgreichen Targets meldeten
  `origin=decompiled`; ein `source-backed` Target wurde in dieser Auswahl
  nicht beobachtet.
- Die Target-Pfade wurden nur als absolute Parameter an MCP übergeben und
  erscheinen nicht in diesem Bericht.
- Keine Produktions-/Testdatei, kein Build und kein Testlauf wurde ausgeführt.

## Kurzfazit

Die Kernfilter sind im Wire-Ergebnis wirksam und Wiederholungsaufrufe sind
konsistent. Herkunft, Trust, Confidence, Session-Status,
Abhängigkeitsauflösung und Completeness sind grundsätzlich agentisch
erkennbar. Zwei Vertragslücken bleiben: Ein explizites Root-Feld für
`loadState`/`metadataOnly` fehlt, und die strukturierten Memberdaten enthalten
bei den geprüften Methoden keine separate Parameterliste. Große Limits schützen
nicht zuverlässig vor großer Gesamtantwort, weil Referenz-, Namespace- und
Diagnosemetadaten mitwachsen.

## Strukturierte Kurzprotokolle

| ID | Tool / Zielklasse | Parameterprobe | Status / origin / loadState | Completeness / Diagnostics / Truncation | Antwortgröße / Nutzerwirkung | Umfang / Dringlichkeit | Disposition |
|---|---|---|---|---|---|---|---|
| P-01 | `inspect_assembly` / `repo-provided assembly` | ungefiltert, Default `publicOnly`, `maxResults=3`, `maxMembers=5` | `partial` / `decompiled` / kein explizites Root-`loadState`; `generation=1`, `trust=untrusted`, `confidence=medium` | `partial`; 182 öffentliche Typen, 3 im Textüberblick, strukturierte Liste zusätzlich gekürzt; 48 Diagnosen, Referenzmenge 45, Samples und Listen gekürzt | ca. 22 k Zeichen; brauchbarer Überblick, aber keine Vollständigkeitszusage | lokal / P2 | **IA-001 – Herkunft und Teilanalyse sichtbar.** Für Agenten klar nutzbar, jedoch nur mit Vertrauens- und Completeness-Vorbehalt. |
| P-02 | `inspect_assembly` / `repo-provided assembly` | `publicOnly=false`, `maxResults=2`, `maxMembers=3` | `partial` / `decompiled` / nur indirekter Session-Status | `partial`; 198 Typen; Member-Slice gekürzt; 48 Diagnosen | ca. 22 k Zeichen; private/interne Oberfläche wird sichtbar, aber der Wire-Slice bleibt begrenzt | lokal / P2 | **IA-002 – Sichtbarkeit von Abhängigkeiten ausreichend, aber partiell.** Fehlende Referenzen und synthetische Decompiler-/Referenzdiagnosen werden gemeldet; keine Negativaussage über nicht gezeigte Teile ableiten. |
| P-03 | `inspect_assembly` / `installed vendor assembly A` | Default sowie `publicOnly=false`; `maxResults=3`, `maxMembers=5` | `partial` / `decompiled` / kein explizites Root-`loadState` | `partial`; öffentlich 6, mit `publicOnly=false` 284 Typen; 101 Diagnosen; Antworten gekürzt | ca. 20–22 k Zeichen; gleiche Herkunfts- und Teilanalyse-Semantik wie beim repo-provided Target | lokal / P2 | **Kein zusätzlicher Befund.** Die Projektion ist über ein zweites Target reproduzierbar. |
| P-04 | `inspect_assembly` / `installed vendor assembly B` | Default sowie `publicOnly=false`; `maxResults=3`, `maxMembers=5` | `partial` / `decompiled` / kein explizites Root-`loadState` | `partial`; öffentlich 83, mit `publicOnly=false` 286 Typen; 101 Diagnosen; Antworten gekürzt | ca. 20 k Zeichen im kleinen Budget; gleiche Semantik wie A | lokal / P2 | **Kein zusätzlicher Befund.** Die Status-/Herkunftsprojektion ist targetübergreifend konsistent. |
| P-05 | `inspect_assembly` / alle drei Targets | `namespace=Namespace_1`, `typeName=Type_1`, `memberName=Member_1` aus dem jeweils vorherigen Überblick | Filteraufrufe erfolgreich; `origin=decompiled`; Status blieb `partial` | Namespace- und Type-Filter reduzierten den Typbestand; Member-Filter reduzierte nur die Membermenge; nicht passende Kombination ergab 0 Typen, ohne Truncation | ca. 19–25 k Zeichen; gezielte Folgeabfragen sind möglich | lokal / P2 | **IA-003 – Filtervertrag erfüllt.** Kein Befund bei Namespace-, Teiltext- und Member-Filter. |
| P-06 | `inspect_assembly` / `repo-provided assembly` | `typeName=Type_1`, `exactTypeName=true`; zusätzlich absichtlich nur Teiltext mit `exactTypeName=true` | Erfolgreich; `partial` / `decompiled` | Exakter Treffer ergab 1 Typ; exakter Teiltext ohne vollständigen Namen ergab 0; `truncated=false` | ca. 19–20 k Zeichen; exakte Nachnavigation verlässlich | lokal / P2 | **Kein Befund.** `exactTypeName` schaltet sichtbar von Teiltext auf Exaktsuche um. |
| P-07 | `inspect_assembly` / `repo-provided assembly` | `memberNames=[Member_1,Member_2]` (zwei zuvor beobachtete Member) sowie gemischte Auswahl mit einem Nichttreffer | Erfolgreich; `partial` / `decompiled` | OR-Auswahl lieferte die Union der beobachteten Member; gemischte Auswahl lieferte nur den vorhandenen Anteil; Nichttreffer allein ergab 0 | ca. 20–23 k Zeichen; Folgeabfragen nach mehreren exakten Membern brauchbar | lokal / P2 | **Kein Befund.** `memberNames` arbeitet als exakte OR-Auswahl. |
| P-08 | `inspect_assembly` / alle drei Targets | `maxResults=1,maxMembers=1` gegenüber `maxResults=50,maxMembers=100` | Erfolgreich; Status und Herkunft unverändert | Kleine Limits führten zu Truncation/kleinem Slice; große Limits erweiterten die Nutzlast. Der strukturierte `types`-Slice blieb in mehreren Antworten auf 1 Element begrenzt, obwohl größere Gesamtbestände gemeldet wurden | repo ca. 21,6 k vs. 33,9 k Zeichen; A ca. 20,0 k vs. 25,8 k; B ca. 19,2 k vs. 102,6 k | mehrere Tools / P1 | **IA-004 – Gesamtantwort nicht zuverlässig gegen Token-Flut geschützt.** `maxResults`/`maxMembers` begrenzen Teilmengen, aber nicht alle Metadatenblöcke; ein separates Response-Budget ist im Schema nicht vorhanden. Für Agenten große Limits nur gezielt einsetzen. |
| P-09 | `inspect_assembly` / `repo-provided assembly` | `maxResults=1`; `maxMembers=1` vs. `maxMembers=100` bei gezieltem Typ | Erfolgreich; `partial` / `decompiled` | Member-Slice blieb begrenzt, `totalMembers` blieb als Unterliegerzahl sichtbar; `truncated` und Counts unterscheiden Slice von Gesamtbestand | ca. 21,6 k vs. 22,1 k Zeichen; Member-Limit wirkt, aber Antwortgrundlast bleibt | lokal / P2 | **IA-005 – Limitwirkung ist erkennbar, aber nicht global.** Disposition: Dokumentation/Agentenstrategie anpassen, keine Produktionsänderung in diesem Audit. |
| P-10 | `inspect_assembly` / `repo-provided assembly` | exakter Typ Type_1, `maxMembers=100`, Member-Filter auf einen beobachteten Methodenamen | Erfolgreich; `partial` / `decompiled` | Strukturierte Memberobjekte enthielten `kind`, `name`, `accessibility`, `signature`; kein separates `parameters`-Feld, obwohl die Signatur Parameter textuell enthielt | ca. 20 k Zeichen; Memberidentität und Signatur brauchbar, Parameter für maschinelle Folgeabfragen nur aus String zu parsen | lokal / P1 | **IA-006 – Strukturierte Parameterdaten fehlen im beobachteten Wire-Ergebnis.** Als Vertragsabweichung für Agenten-Folgeabfragen markieren; Signatur bleibt als Fallback nutzbar. |
| P-11 | `inspect_assembly` / alle drei Targets | identischer kleiner Aufruf zweimal seriell | `generation=1` jeweils; `partial` / `decompiled`; gleicher Session-Status | strukturierter Kern bytegleich; gleiche Counts, Truncation-Flags und Diagnosezusammenfassungen | jeweils identische Antwortgröße pro Paar; keine Drift beobachtet | systemisch / P2 | **Kein Befund.** Wiederholungsaufrufe sind deterministisch genug für Agenten-Caching/Retry-Entscheidungen. |
| P-12 | `inspect_assembly` / alle drei Targets | metadata-only Vertragsprobe über Assembly-Ziel, ohne Consumer-Projekt | `origin=decompiled`, `sourcePath=none`, `generation=1`; explizites `metadataOnly`-/Root-`loadState`-Feld nicht geliefert | `sessionStatus=partial` und Referenz-`resolutionState` vorhanden; keine Ausführung oder Quellmodifikation beobachtet | ca. 19–22 k Zeichen; Agent kann Decompilation und partielle Auflösung erkennen, nicht aber einen expliziten Load-/Execution-Nachweis | systemisch / P2 | **IA-007 – Metadata-only und Load-State nur indirekt observierbar.** Contract-Hinweis ist vorhanden, Wire-Beobachtbarkeit sollte explizit werden. |
| P-13 | `inspect_assembly` / `decompiled/source-backed target` als negativer Kontrollaufruf | kleiner, ungefilterter metadata-only Aufruf auf ein installiertes Shared-Target ohne analysierbare Dokumente | `isError=true`, Analysefehler; keine verwertbare origin-/loadState-Projektion | Fehlermeldung: keine analysierbaren Dokumente erzeugt; kein strukturierter Datensatz | ca. 284 Zeichen; klarer, reproduzierbarer Fehler statt falscher Leermenge | lokal / P2 | **IA-008 – Fehlerklassifikation reproduzierbar.** Als Analysefehler behandeln, nicht als „0 Typen“; kein Produktionsbefund. |

## Kategorisierte Findings

### Herkunft, Trust, Load-State und fehlende Dependencies

- **IA-001 (P2, lokal):** Die drei verwertbaren Targets melden zuverlässig
  `origin=decompiled`, `sourcePath=none`, `confidence=medium`,
  `trust=untrusted`, `generation=1`, `status=partial` und
  `completeness=partial`. Ein source-backed Ergebnis war nicht Teil der
  beobachteten Auswahl.
- **IA-002 (P2, lokal):** Fehlende Dependencies sind agentisch erkennbar:
  Referenzen tragen `resolutionState=missing`, Sessionstatus `missing` und
  partielle Completeness; Root-/transitive Diagnosen werden gezählt und
  begrenzt ausgegeben. Beim repo-provided Target wurden 45 Referenzen und 48
  Diagnosen gezählt, beim installierten A/B jeweils 101 Diagnosen.
- **IA-007 (P2, systemisch):** Ein explizites Root-`loadState` oder
  `metadataOnly=true` fehlt. Agenten können nur aus Herkunft, Sessionstatus,
  Resolution-State und dem unveränderten Verhalten schließen. Die DLL blieb
  im beobachteten Ablauf metadata-only; ein maschinenlesbarer Attest dafür
  fehlt.

### Filter und Navigation

- **Kein Befund (P-05 bis P-07):** Namespace-, TypeName-,
  `exactTypeName`-, MemberName- und `memberNames`-OR-Filter greifen im
  strukturierten Ergebnis. Absichtlich nicht passende Filter ergeben eine
  echte Leermenge (`totalTypes=0`, `truncated=false`).
- **Kein Befund (P-11):** Wiederholungen desselben Aufrufs waren für alle drei
  Targets im strukturierten Kern exakt gleich.

### Member-/Parameterdaten

- **IA-006 (P1, lokal):** Memberdaten sind mit Kind, Name, Accessibility und
  Signatur brauchbar. Separate strukturierte Parameterdaten wurden bei den
  geprüften Methoden jedoch nicht geliefert; weitere Agentenabfragen müssen
  die Signatur interpretieren oder bleiben unsicher.

### Limits, Truncation und Antwortbudget

- **IA-004 (P1, systemisch):** Die Limits begrenzen sichtbare Typ-/Member-
  Slices, aber nicht die gesamte Antwort. Die größte Probe wuchs bis ca.
  102,6 k Zeichen. Referenz-, Namespace- und Diagnoseblöcke können daher
  trotz kleiner sichtbarer Type-Liste Tokenlast erzeugen.
- **IA-005 (P2, lokal):** `maxMembers` ist in Counts/Slices sichtbar wirksam,
  beseitigt aber die Antwortgrundlast nicht. `truncated` muss gemeinsam mit
  `totalTypes`/`totalMembers` ausgewertet werden.

### Fehlerklassifikation

- **IA-008 (P2, lokal):** Ein nicht analysierbares installiertes Shared-Target
  wird als `isError=true` mit Analysefehler gemeldet und nicht fälschlich als
  erfolgreiche 0-Typ-Antwort dargestellt.

## Geprüfte Bereiche ohne Befund

- Filter-Semantik einschließlich absichtlich nicht passender Filter: kein
  Befund.
- Wiederholungs-Konsistenz und stabile Generation: kein Befund.
- Sichtbarkeit von Completeness, Truncation, Diagnose- und
  Referenzzusammenfassungen: kein Befund; die Angaben sind vorhanden, müssen
  aber wegen IA-004 als partiell interpretiert werden.
- Metadata-only-Ausführung: keine Ausführung/Modifikation beobachtet; der
  verbleibende Befund betrifft ausschließlich die fehlende explizite
  Observability (IA-007).

## Empfohlene Agenten-Disposition

1. Mit einem kleinen ungefilterten Budget beginnen und erst mit konkret
   beobachtetem `Type_1`/`Member_1` verfeinern.
2. `origin`, `trust`, `confidence`, `completeness`, `truncated`, Counts und
   Diagnostics vor jeder inhaltlichen Schlussfolgerung prüfen.
3. Große `maxResults`-/`maxMembers`-Werte wegen IA-004 vermeiden; die
   Gesamtantwort kann trotz kleinem Slice groß werden.
4. Parameter nicht aus dem Fehlen eines strukturierten Feldes ableiten;
   Signaturen nur als Fallback verwenden und Unsicherheit weitergeben.
