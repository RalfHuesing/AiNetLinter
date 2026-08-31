# MCP-Live-/Vertragsaudit: Struktur- und Kontextabfragen auf Assembly-Zielen

Datum: 2026-08-31
Status: abgeschlossen, keine Produktions-/Testcodeänderung

## Audit-Rahmen

Geprüft wurden `inspect_assembly`, `find_symbol`, `get_file_skeleton`,
`get_symbol_body`, `get_class_structure`, `get_namespace_tree`,
`metrics_lookup`, `metrics_tree` und `get_feature_context` auf
Assembly-Zielen. Die drei Ziele werden ausschließlich als `repo-provided
assembly`, `installed vendor assembly A` und `installed vendor assembly B`
bezeichnet. Alle Typen, Member und dekompilierten Dateien sind im Bericht
als `Type_1`/`Type_2`/`Type_3`, `Member_1`/`Member_2`/`Member_3` und
`File_1`/`File_2`/`File_3` redigiert.

Die Pfade wurden nur als MCP-Parameter verwendet und erscheinen nicht im
Bericht. Es wurden weder Produktions- noch Testdateien geändert, kein Build
und kein Testlauf ausgeführt.

## Ergebnisübersicht

| ID | Umfang | Dringlichkeit | Befund | Disposition |
|---|---|---:|---|---|
| SC-STRUCT-001 | lokal: `get_file_skeleton` | P1 | Vom Tool ausgegebene dekompilierte Pfade sind zwischen relativer und absoluter Form nicht einheitlich weiterverwendbar. | Dokumentiert; Pfadnormalisierung und einheitlicher Folgeabfragevertrag im Produktionsscope prüfen. |
| SC-STRUCT-002 | lokal: `get_class_structure`-StructuredContent | P1 | Bei einem Typ mit 21 Membern meldet die Antwort 21 gezeigte Member, liefert strukturiert aber nur 17 Memberobjekte. | Dokumentiert; Counts und Memberarray müssen atomar konsistent sein. |
| F-HEALTH-004 / Bestätigung | systemisch: Fehler-Wirevertrag | P2 | Leere/ungültige Eingaben und Unsupported-Assembly bei `get_feature_context` liefern Textfehler mit `isError=false` und ohne StructuredContent. | Bestehenden systemischen Befund bestätigt; keine zusätzliche Scope-Ausweitung. |
| IA-006 / Bestätigung | mehrere Struktur-Tools | P2 | Signaturen enthalten Parameter textuell; ein separates strukturiertes Parameterfeld ist bei den geprüften Strukturantworten nicht vorhanden. | Bestehenden Befund bestätigt; Signatur nur als Fallback verwenden. |

Zusätzlicher sicherer Befund wurde nicht aus den Metrik-/Namespace-Proben
abgeleitet. Die bereits in den Berichten 01–03 erfassten systemischen
Antwortbudget- und ID-Vertragsbefunde werden unten nur durch neue Evidenz
ergänzt.

## Repräsentative Einstiege

| Zielklasse | `inspect_assembly` / `find_symbol`-Einstieg | Decompilation-Snapshot |
|---|---|---|
| `repo-provided assembly` | `File_1`, `Type_1`, `Member_1` | `origin=decompiled`, `snapshot=none`, `generation=2`, `status=partial`, `completeness=partial` |
| `installed vendor assembly A` | `File_2`, `Type_2`, `Member_2` | `origin=decompiled`, `snapshot=none`, `generation=1`, `status=partial`, `completeness=partial` |
| `installed vendor assembly B` | `File_3`, `Type_3`, `Member_3` | `origin=decompiled`, `snapshot=none`, `generation=1`, `status=partial`, `completeness=partial` |

`inspect_assembly` meldete bei allen drei Zielen `confidence=medium` und
`trust=untrusted`. `find_symbol` lieferte für alle Ziele relative
dekompilierte Dateipfade und Typfunde; die Antwort enthielt dabei keine
direkt wiederverwendbare generationgebundene Member-ID. `get_file_skeleton`
lieferte diese ID anschließend zuverlässig für die erfolgreiche Folgeprobe.

## Probeprotokolle

Die Größenangaben sind ungefähre Zeichenlängen der MCP-Antworten bzw. des
StructuredContent. Es werden keine Rohantworten oder nicht anonymisierten
Symbole wiedergegeben.

| Tool | Zielklasse / anonymisierte Parameter | Ergebnisstatus | Herkunft / Snapshot / Generation | Completeness / Diagnostics / Truncation | Output-/Tokenbeobachtung | Agentische Nutzbarkeit | Evidenz | Finding-ID | Umfang / Dringlichkeit | Disposition |
|---|---|---|---|---|---|---|---|---|---|---|
| `inspect_assembly` | alle drei Ziele; kleiner API-Slice, öffentliche Typen | erfolgreich | decompiled / none / 2 bzw. 1 | partial; Diagnose- und Referenzslices begrenzt, Truncation sichtbar | ca. 17–20 k Zeichen je Antwort; Metadaten-Grundlast bleibt | Einstieg und Herkunft gut erkennbar | Je Ziel wurden `File_n`, `Type_n`, `Member_n` aus dem Ergebnis für Folgeproben ermittelt. | kein neuer Befund; IA-004/IA-007 beachten | lokal / P2 | akzeptiert, Folgeabfragen klein beginnen |
| `find_symbol` | je Ziel ein anonymisiertes Typmuster; `maxResults=2`, `includeReferences=false` | erfolgreich, bei A mit begrenztem Slice | decompiled / none / 2 bzw. 1 | partial; A meldete mehr Treffer als sichtbar, Truncation textuell erkennbar | ca. 1,2–1,5 k Zeichen | Typ-/Datei-Navigation möglich; direkte Folge-ID fehlt | Relative Dateien `File_1`–`File_3` und passende Typfunde sichtbar. | SN-001 bestätigt | mehrere Tools / P1 | bestehende Disposition beibehalten |
| `get_file_skeleton` | repo; `filePaths=[File_1]`, als vom Ergebnis gelieferter absoluter dekompilierter Pfad | erfolgreich | decompiled / none / 2 | partial; 17 Compile-Diagnosen, keine Body-Truncation; 1 Typ/8 Member | ca. 3,2 k Zeichen | stabile Typ-/Member-IDs für Body-Folgeabfragen; synthetische Quelle sichtbar | ID-Form enthält Assembly-Hash und Generation; `File_1` wird als relativer Dateiname ausgegeben. | kein Befund | lokal / P2 | weiter für Progressive Disclosure verwenden |
| `get_file_skeleton` | repo; `filePaths=[File_1, File_2]` | erfolgreich als Batch | decompiled / none / 2 | partial; beide Datei-Slices mit Compile-Diagnosen, kein globaler Batch-Truncationmarker | ca. 7,3 k Zeichen; ungefähr linear je Datei | Batch brauchbar, aber Antwortgröße wächst je Datei | Zwei unterschiedliche dekompilierte Dateien wurden in einem Turn geliefert. | kein Befund | lokal / P2 | kleine Batches bevorzugen |
| `get_file_skeleton` | repo; `filePaths=[]` bzw. `[null]` | Fehlertext, `isError=false` | decompiled / none / 2 | keine StructuredContent-/Truncation-Metadaten | unter 1 k Zeichen | Fehlerursache textuell verständlich, maschinell nur über Text erkennbar | `INVALID_ARGUMENT`, Pflichtparameter leer. | F-HEALTH-004 bestätigt | systemisch / P2 | Fehlercode strukturiert und `isError`-Semantik vereinheitlichen |
| `get_file_skeleton` | repo; `filePaths=[ungültiger Pfad]` | Fehlertext, `isError=false` | decompiled / none / 2 | keine StructuredContent-/Truncation-Metadaten | unter 1 k Zeichen | nicht mit erfolgreicher Leermenge verwechselbar, aber kein stabiler Fehlerpayload | `RESOURCE_NOT_FOUND` mit Kontext und Hint im Text. | F-HEALTH-004 bestätigt | systemisch / P2 | wie oben |
| `get_file_skeleton` | repo; `filePaths=[source/File_1]` relativ zur ausgegebenen Herkunft | Fehlertext, `isError=false` | decompiled / none / 2 | keine StructuredContent-/Truncation-Metadaten | unter 1 k Zeichen | direkte Wiederverwendung des ausgegebenen Pfads scheitert | Die vom `inspect_assembly`-Text genannte Form `source/File_1` wurde nicht in der Solution gefunden; der absolute Cache-Pfad war erfolgreich. | SC-STRUCT-001 | lokal / P1 | Pfadformen im Wirevertrag explizit normalisieren |
| `get_file_skeleton` | vendor A/B; `filePaths=[File_2]` bzw. `[source/File_2]`, analog `File_3` | erfolgreich | decompiled / none / 1 | partial; synthetische Compile-Diagnosen; keine Truncation bei den kleinen Dateien | ca. 2–3 k Zeichen je Datei | relative Pfade funktionieren in diesen Sessions; Verhalten ist nicht targetübergreifend einheitlich | Beide relativen Formen lieferten Skeletons für `Type_2` bzw. `Type_3`. | SC-STRUCT-001 | lokal / P1 | konsistenten relativen Pfad ausgeben oder absolute Pfade akzeptieren |
| `get_symbol_body` | repo; `symbolIdentifiers=[Member_1]`, `maxBodyLines=80` | erfolgreich | decompiled / none / 2 | partial; keine Truncation; Body ist nur deklarativ | ca. 1 k Zeichen | ID-Folgeabfrage funktioniert, aber Decompiler liefert keinen echten Body | Skeleton-ID wurde unverändert akzeptiert; Ergebnis verweist auf `File_1`. | kein Befund | lokal / P2 | Body immer als dekompilierten Scope bewerten |
| `get_symbol_body` | repo; `symbolIdentifiers=[Member_1]`, `maxBodyLines=1` | erfolgreich, explizit gekürzt | decompiled / none / 2 | partial; `// ... truncated`, Gesamtzeilen und Limit sichtbar | ca. 0,9 k Zeichen | Body-Truncation für Progressive Disclosure klar erkennbar | Grenze wurde gegenüber der Vollprobe wirksam; Gesamtzeilen wurden gemeldet. | kein Befund | lokal / P2 | kleine Grenze für erste Probe geeignet |
| `get_symbol_body` | repo; Batch `[Member_1, Member_2]`, `maxBodyLines=2` | erfolgreich als Batch | decompiled / none / 2 | partial; ein Body gekürzt, je Symbol getrennte Ausgabe | ca. 1,3 k Zeichen | Batch-Folgeabfragen funktionieren; symbolweise auswertbar | Zwei Skeleton-IDs wurden in einem Turn verarbeitet. | kein Befund | lokal / P2 | kleine Batches beibehalten |
| `get_symbol_body` | repo; ungültige generationgebundene ID | Fehlertext, `isError=false` | decompiled / none / 2 | keine StructuredContent-/Truncation-Metadaten | unter 1 k Zeichen | stale ID klar von leerem Body unterscheidbar | `INVALID_ARGUMENT` mit Hinweis auf aktuelle Assembly-Generation. | kein Befund | lokal / P2 | bestehende klare Textdiagnose erhalten, StructuredContent ergänzen |
| `get_symbol_body` | vendor B; `symbolIdentifiers=[Member_3]`, `maxBodyLines=1` | erfolgreich, explizit gekürzt | decompiled / none / 1 | partial; Truncationmarker sichtbar | ca. 1 k Zeichen | Verhalten targetübergreifend reproduzierbar | Skeleton-ID aus `Type_3` akzeptiert. | kein Befund | mehrere Tools / P2 | kein Fix im Berichtsscope |
| `get_class_structure` | repo; `Type_1`, `sortBy=lines`, `maxMembers=2` | erfolgreich | decompiled / none / 2 | partial; `totalMemberCount=8`, `shownMemberCount=2`, `truncated=true` | ca. 2,1 k Zeichen | klare Slice-/Gesamtzahl; Start-/End-Zeilen und Signaturen vorhanden | Strukturierte Member enthalten Kind, Name, Sichtbarkeit, Zeilen und Datei. | kein Befund | lokal / P2 | als erster Struktur-Slice geeignet |
| `get_class_structure` | repo; `Type_1`, `sortBy=name`, `maxMembers=50` | erfolgreich, vollständig im Typ-Scope | decompiled / none / 2 | partial global, aber Typ-Scope `8/8`, `truncated=false` | ca. 4,2 k Zeichen | vollständige kleine Klasse gut navigierbar | Sortierung und Counts reproduzierbar. | kein Befund | lokal / P2 | kein Fix |
| `get_class_structure` | repo; `Type_1`, `sortBy=kind`, `kindFilter=Method` | erfolgreich | decompiled / none / 2 | partial global; Filterbestand vollständig, `8/8` bzw. gefilterter Bestand korrekt | ca. 4,2 k Zeichen | Kindfilter maschinell brauchbar | Nur Member des angeforderten Kinds erscheinen. | kein Befund | lokal / P2 | kein Fix |
| `get_class_structure` | repo; `Type_1`, `nameFilter=Run`, `maxMembers=50` | erfolgreich | decompiled / none / 2 | partial global; `3/3`, `truncated=false` | ca. 2,6 k Zeichen | Namensfilter für Folgeexploration brauchbar | Nur passende Member erscheinen; Nichttreffer ergeben `0/0`. | kein Befund | lokal / P2 | kein Fix |
| `get_class_structure` | vendor A; `Type_2`, `maxMembers=2` vs. `50`/`200` | erfolgreich | decompiled / none / 1 | bei 2: `21/2`, trunciert; bei 50/200: `21/21`, nicht trunciert | ca. 1,9 k vs. 6,9 k Zeichen; 200 nicht größer als 50 | progressive Memberauflösung funktioniert, große Grenze erhöht Payload | Texttabelle zeigt bei großer Grenze 21 Member; StructuredContent siehe nächsten Befund. | kein Befund für Limitsteuerung; SC-STRUCT-002 für Payloadprojektion | lokal / P1 | Array-/Count-Konsistenz herstellen |
| `get_class_structure` | vendor A; `Type_2`, `maxMembers=50`, `sortBy=lines` | formal erfolgreich, StructuredContent inkonsistent | decompiled / none / 1 | `totalMemberCount=21`, `shownMemberCount=21`, `truncated=false`; `members.length=17` | Text enthält alle 21 Zeilen, strukturierter Array-Slice verliert 4 Member ohne Marker | Agenten, die nur StructuredContent verwenden, sehen unbemerkt unvollständige Memberdaten | Reproduziert mit `maxMembers=50` und `200`; fehlend im Array: vier am Textende sichtbare Methoden. | SC-STRUCT-002 | lokal / P1 | `shownMemberCount` an Arraylänge binden oder explizit strukturierte Truncation melden |
| `get_class_structure` | vendor A; `kindFilter=Method`, `nameFilter=Identifier`, Nichttreffer | erfolgreich | decompiled / none / 1 | Filterantworten konsistent: Methoden `13/13`, Namensfilter `6/6`, Nichttreffer `0/0`, jeweils nicht trunciert | ca. 0,5–1,9 k Zeichen | Filter sind verlässlich, auch wenn unfiltered StructuredContent fehlerhaft ist | Strukturarraylängen entsprechen den gefilterten Counts. | kein Befund | lokal / P2 | Regressionstest für große unfiltered Klasse ergänzen |
| `get_class_structure` | vendor B; `Type_3`, `maxMembers=10` | erfolgreich | decompiled / none / 1 | `2/2`, `truncated=false` | ca. 1,7 k Zeichen | kleine Klasse vollständig strukturiert | Constructor und Methode mit Start-/End-Zeile geliefert. | kein Befund | lokal / P2 | kein Fix |
| `get_class_structure` | vendor A; ungültige generationgebundene Typ-ID | Fehlertext, `isError=false` | decompiled / none / 1 | keine StructuredContent-/Truncation-Metadaten | unter 1 k Zeichen | stale ID verständlich abgewiesen | `INVALID_ARGUMENT` nennt veraltete Generation. | F-HEALTH-004 bestätigt | systemisch / P2 | Fehlerpayload vereinheitlichen |
| `get_namespace_tree` | repo; Assemblyziel, `depth=1`, `includeTypes=true`, `maxResults=5` | erfolgreich, synthetische Übersicht | decompiled / none / 2 | partial; `truncated=false` auf Projektübersicht, keine Assemblytypen im Root-Slice | ca. 1,2 k Zeichen | als Überblick brauchbar, aber Text/Schema sprechen von synthetischem Projekt | Antwort projiziert ein Projekt mit Namespace-/Typcounts; `analysis.targetType=assembly` bleibt sichtbar. | kein neuer Befund | lokal / P2 | Scopebezeichnung bei Assemblyziel schärfen |
| `get_namespace_tree` | repo/vendor B; Namespace-Prefix-Drilldown | erfolgreich | decompiled / none / 2 bzw. 1 | partial; `maxResults` wirkt, Truncation sichtbar | ca. 3,5–4,9 k Zeichen bei Drilldown | Progressive Disclosure funktioniert; Typ-/Datei-Slices sind weiterverwendbar | Prefix liefert Namespaces bzw. Typen und relative `File_n`-Pfade. | kein Befund | lokal / P2 | kein Fix im Berichtsscope |
| `metrics_lookup` | repo; `symbolIdentifiers=[Member_1]` | erfolgreich | decompiled / none / 2 | partial global; angefragter Scope vollständig | ca. 2,9 k Zeichen | Codezeilen, zyklomatische/kognitive Komplexität, Parameter und Grenzwerte strukturiert nutzbar | Alle Metriken und Threshold-Checks als strukturierte Felder vorhanden. | kein Befund | lokal / P2 | kein Fix |
| `metrics_tree` | repo; `mode=code_size`, `depth=1`, `topN=3` | erfolgreich, Top-N-Ausschnitt | decompiled / none / 2 | partial; Hinweis „nicht vollständig“, Top-N-Truncation textuell | ca. 1,2 k Zeichen; Aggregate plus drei Kinder | gute progressive Struktur-/Größenexploration | Rootaggregate, Top-N und Hinweis zum weiteren Drilldown vorhanden. | kein Befund; SN-003 beachten | lokal / P2 | kleine Tiefen/Top-N verwenden |
| `metrics_tree` | repo; `fileFilter=File_1`, `depth=1`, `topN=1` | erfolgreich | decompiled / none / 2 | partial; ein Dateitreffer | unter 1 k Zeichen | gezielte Datei-Metrik nutzbar | Filter grenzt den dekompilierten Dateibaum auf `File_1` ein. | kein Befund | lokal / P2 | kein Fix |
| `metrics_tree` | repo; `root=Type_1`, `depth=2` | erfolgreiche Leermenge mit Hinweis | decompiled / none / 2 | partial; keine Dateien unter dem als Namespace/Typ übergebenen Root | unter 1 k Zeichen | Fehlbedienung verständlich, aber Root ist dateipfadbezogen | `root` erwartet einen Datei-/Verzeichnisausschnitt, keinen Typnamen. | kein Befund | lokal / P2 | Agentenhinweis beibehalten |
| `get_feature_context` | repo-provided assembly; `Type_1`, alle Teilbereiche aus | Unsupported, `isError=false` | `origin=assembly-target`, kein Generation-Snapshot | kein StructuredContent, keine Completeness-/Diagnostics-Felder | unter 1 k Zeichen; klarer Fehlercode `ASSEMBLY_TARGET_UNSUPPORTED` | Unsupported-Vertrag textuell verständlich, Retry auf Assembly nicht sinnvoll | Antwort nennt capability/status `unsupported`, Kontext und unterstützte Alternative. | F-HEALTH-004 bestätigt | systemisch / P2 | strukturierten Unsupported-Fehler ergänzen |
| `get_feature_context` | project target; bekannter Projekt-`Member_1`, Teilbereiche aus | erfolgreich | project source, vollständiger Scope | keine Assembly-Diagnostics; Deklaration mit Datei, Zeilen und Doc-ID | unter 1 k Zeichen | Projektvergleich bestätigt klare project-only Grenze | Projektantwort liefert Symbolart, Datei-/Zeilenbereich, Parameter und DocCommentId. | kein Befund | lokal / P2 | kein Fix |

## Stabilität und Folgeabfragen

- Ein identischer `get_file_skeleton`-Aufruf auf `File_1` wurde zweimal
  seriell wiederholt. Text und StructuredContent waren bytegleich; beide
  Antworten meldeten dieselbe Generation. Damit sind Skeleton-IDs innerhalb
  eines unveränderten Assembly-Snapshots retry-stabil.
- Die aus dem Skeleton gelieferte ID für `Member_1` funktionierte direkt in
  `get_symbol_body` und `metrics_lookup`. Eine absichtlich veraltete,
  generationfremde ID wurde mit `INVALID_ARGUMENT` abgewiesen. IDs sind damit
  nicht generationübergreifend stabil, aber ihr Gültigkeitsbereich ist klar
  markiert.
- Die relative Pfadform ist nicht durchgehend retry-/targetstabil:
  `source/File_1` wurde beim repo-provided Ziel abgewiesen, während
  `File_2`/`source/File_2` und `File_3` in den installierten Zielen
  funktionierten. Das ist SC-STRUCT-001 und verhindert, dass Agenten den
  ausgegebenen Herkunftspfad blind als nächsten `filePaths`-Parameter nutzen.

## Agentische Gesamtbewertung

### Sind synthetische Dateien wie echte Source-Dateien navigierbar?

Teilweise. Skeletons, relative Dateinamen, Start-/End-Zeilen, Memberarten,
Signaturen und generationgebundene IDs bilden eine brauchbare synthetische
Navigationsoberfläche. `get_class_structure` und `metrics_lookup` erlauben
gezielte nächste Schritte, ohne den gesamten dekompilierten Baum zu laden.
Die Decompilation bleibt jedoch eine synthetische Compilation: Die Antworten
melden `partial`, `trust=untrusted` und Compile-Diagnosen; `get_symbol_body`
lieferte in den Proben überwiegend Deklarationszeilen statt eines verlässlich
ausführbaren Originalbodys. Ein leerer oder kurzer Body ist deshalb kein
Beweis für fehlende Originalimplementierung.

### Sind IDs und Pfade für Retries stabil?

IDs sind innerhalb derselben Assembly-Hash-/Generation stabil und stale IDs
werden verständlich abgewiesen. `find_symbol` allein liefert weiterhin keine
direkte Folge-ID; die robuste Kette lautet `find_symbol`/`inspect_assembly` →
`get_file_skeleton` → `get_symbol_body`/`metrics_lookup`. Pfade benötigen
wegen SC-STRUCT-001 eine Normalisierung bzw. einen klaren relativen
Assembly-Pfadvertrag.

### Unterstützen die Antworten Progressive Disclosure?

Ja, mit Einschränkungen. Kleine Skeletons und Klassen-Slices sind als erste
Stufe geeignet; Body-Limits zeigen explizite Truncation; Batch-Aufrufe und
Filter reduzieren den Kontext. `get_namespace_tree` und `metrics_tree`
liefern Top-N-/Drilldown-Hinweise. Vor jeder inhaltlichen Aussage müssen
`origin`, Generation, `status`, `completeness`, Diagnostics und
Truncationwerte ausgewertet werden.

### Sind project-only-Tools verständlich fehlerhaft?

`get_feature_context` meldet Assemblyziele mit dem klaren Code
`ASSEMBLY_TARGET_UNSUPPORTED` und einer Alternative. Die Verständlichkeit
ist gut, die Wire-Semantik aber wegen `isError=false` und fehlendem
StructuredContent inkonsistent; dies bestätigt F-HEALTH-004. Der
Projektvergleich mit `Member_1` war erfolgreich und vollständig für den
angefragten Scope.

### Können große Limits Response-/Tokenflut auslösen?

Im engen `get_class_structure`-Scope stieg die Antwort für `Type_2` von ca.
1,9 k Zeichen bei `maxMembers=2` auf ca. 6,9 k bei `50`; `200` brachte bei
21 Membern keinen weiteren Anstieg. Die Slice-Limits wirken und werden durch
Counts/Truncation sichtbar. Ein globales Response-Budget ist jedoch auch
hier nicht erkennbar; Assembly-Herkunft, Diagnostics und Workspace-Hinweise
bleiben Grundlast. Die größeren systemischen Befunde IA-004 und SN-003 aus
den Vorberichten bleiben daher maßgeblich.

## Kein Befund / Grenzen

- Filter nach `sortBy`, `kindFilter` und `nameFilter` funktionieren in den
  geprüften Strukturproben; Nichttreffer sind von Fehlern unterscheidbar.
- `TotalMemberCount`, `ShownMemberCount`, Start-/End-Zeilen, Zeilenanzahl,
  Sichtbarkeit, Kind und Signatur sind grundsätzlich brauchbare strukturierte
  Memberdaten. Die Ausnahme ist die konkrete Array-/Count-Abweichung
  SC-STRUCT-002 bei einer größeren unfiltered Klasse.
- `get_symbol_body`-Batch, Body-Truncation und ungültige generationgebundene
  IDs haben verständliche textuelle Ergebnisse.
- `metrics_lookup` liefert für ein Assembly-Symbol verwertbare Metriken und
  Schwellwertprüfungen. `metrics_tree` liefert aggregate Top-N-Slices und
  unterstützt Datei-Filter.
- `get_namespace_tree` und `metrics_tree` sind für Assemblyziele registriert
  und antworten; Namespace-Antworten verwenden teilweise synthetische
  Projektbegriffe, behalten aber `analysis.targetType=assembly` bei.
- Keine Produktions-/Teständerung, kein Build, kein Testlauf, kein Push.

## Ausgeführte MCP-Abfragen

`inspect_assembly` und `find_symbol` auf dem repo-provided Assembly sowie zwei
installierten vendor Assemblies; `get_file_skeleton` als Einzeldatei, Batch,
leer, null und ungültig; `get_symbol_body` mit Skeleton-ID, kleiner Grenze,
Batch und ungültiger Generation; `get_class_structure` mit kleinen/größeren
Limits, `sortBy`, `kindFilter`, `nameFilter` und ungültiger ID;
`get_namespace_tree` und `metrics_tree` als Assembly-Drilldowns;
`metrics_lookup` auf einem Assembly-Member; `get_feature_context` als
expliziter Assembly-Unsupported-Aufruf und zum Vergleich auf einem bekannten
Projekt-Member.

### Commit-Vorschlag

docs: dokumentiere Assembly-Strukturkontext
