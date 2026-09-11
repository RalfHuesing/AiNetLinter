# Gruppe D – Fehlerbehandlung (Agenten-UX-Audit)

## Scope und Evidence

Read-only-Aufrufe gegen den Source-Targetvertrag, die produktive eigene Testassembly sowie einen anonymisierten externen Assembly-Fall (`externe Testassembly A`). Es wurden keine Builds, Tests oder Codeänderungen ausgeführt. Rohantworten, Produktnamen, externe Pfade, Namespaces und Symbolnamen werden bewusst nicht wiedergegeben.

Geprüft wurden fehlende/ungültige Pflichtfelder, nicht vorhandene Targets, falsche JSON-Typen, Enumwerte, leere Pflichtwerte, Verzeichnis-Targets, unbekannte Properties, Limitgrenzen und die direkte Wiederholung nach einem Fehler.

## Positive Fälle

- Fehlendes `targetPath` liefert reproduzierbar `INVALID_ARGUMENT`, `recoverable=true`, konkreten `fieldPath=$.targetPath`, kurzen Hint und keinen Stack-Trace.
- Leeres `targetPath`, nicht existierendes Target und Verzeichnis-Target werden jeweils als `INVALID_ARGUMENT` mit handlungsweisendem Datei-Hinweis behandelt.
- String statt Array bei `namePatterns` wird mit erkanntem JSON-Typ, erwartetem Typ, `fieldPath=$.namePatterns` und Korrekturhinweis abgewiesen.
- Unbekannte Properties werden mit `fieldPath` der konkreten Property und Schema-Hinweis abgewiesen.
- `maxResults=0`/negativ, `maxResponseBytes` unter Minimum oder oberhalb des Caps sowie `depth=0`, `maxBodyLines=0` und `topN=0` bei Assembly-Kontexten werden, wo für das jeweilige Tool gültig, als feldgenaues `INVALID_ARGUMENT` behandelt.
- `maxResponseBytes` oberhalb des Caps wird auch bei Assembly-Extensions und Assembly-Kontext abgewiesen; kein Crash beobachtet.
- Ein korrekter Folgeaufruf nach einem absichtlich ungültigen `maxResults`-Aufruf funktioniert und liefert eine normale, als `truncated` signalisierte Antwort. Der Fehlerzustand verschmutzt den nächsten Call nicht.
- Eine produktive eigene DLL und `externe Testassembly A` konnten im Assembly-Modus read-only angesprochen werden; es wurde eine strukturierte Antwort bzw. ein strukturierter leerer Extension-Satz ohne Prozessausführung erhalten.
- Assembly-Diagnosen werden bei großen/teilweisen Ergebnissen budgetiert und als `partial`/`truncated` mit maschinenlesbaren Zähl-/Fortsetzungsinformationen sichtbar gemacht.

## Findings

### [Major] Befund 1: Pflichtfeld- und Enumfehler sind nicht durchgehend feldgenau

- **Tool(s):** `find_symbol`
- **Evidenz:** `kind=unknownKind` und `namePatterns=[""]` liefern `code=INVALID_ARGUMENT`, aber im StructuredContent keinen `fieldPath`; der Text nennt nur den Parameter indirekt. Vergleichbare Typ-/Targetfehler enthalten dagegen `$.<feld>`.
- **Problem:** Ein Agent kann den fehlerhaften Request bei diesen Validierungsfällen nicht ebenso zuverlässig per Schema-Patch lokalisieren. Das verletzt den Konzeptvertrag „INVALID_ARGUMENT am konkreten Feld“ und erzeugt inkonsistente Fehlerauswertung.
- **Reproduktion:** Source-Target verwenden; einmal unbekannten `kind` senden, einmal ein leeres `namePatterns`-Element senden; StructuredContent auf `fieldPath` prüfen.
- **Impact:** Parser-/Agentenlogik muss Sonderfälle über `message` statt über ein stabiles Feldschema behandeln.
- **Konzeptbezug:** Umsetzungsvertrag („anders geformter Input erhält `INVALID_ARGUMENT` am konkreten Feld“), AK 35/36 (finaler Vertrag, keine Parallelform).
- **Empfehlung:** Für Enum- und inhaltliche Pflichtfeldfehler den gleichen `fieldPath`-Vertrag wie bei Typ-/Targetvalidierung ausgeben (`$.kind` bzw. `$.namePatterns`; bei leerem Element ggf. Indexpfad).

### [Major] Befund 2: Ungültiger `detailLevel`-Enum wird still akzeptiert

- **Tool(s):** `find_assembly_extensions`, `get_assembly_context`
- **Evidenz:** `detailLevel=wat` führt nicht zu `INVALID_ARGUMENT`, sondern zu einer normalen Assembly-Antwort. Beim Extensions-Call sind zugleich `completeness`/`truncated` in Text und StructuredContent widersprüchlich (`complete` im Status-Text gegenüber `partial`/`truncated` in der Struktur).
- **Problem:** Ein Agent erhält keine Rückmeldung, dass sein Enum falsch ist, und kann dadurch mit einem unbeabsichtigten Detailmodus arbeiten. Die widersprüchliche Vollständigkeitsanzeige erschwert zusätzlich das sichere Weiterverkettungshandling.
- **Reproduktion:** Gültige Assembly als Target verwenden; `detailLevel=wat` senden; `code`, `navigation.status.completeness` und top-level `completeness` vergleichen.
- **Impact:** Falsche oder unvollständige Assembly-Ergebnisse können als gültige Ergebnisse weiterverwendet werden; der Agent verliert die klare Recovery-Aktion.
- **Konzeptbezug:** Pflicht-Prüffall „ungültiger Enum-Wert“; Konzeptvertrag mit feldgenauem `INVALID_ARGUMENT`; AK 31 (Status-/Completeness-Konsistenz).
- **Empfehlung:** `detailLevel` strikt gegen `compact|standard|full` validieren, `$.detailLevel` ausgeben und bei gültigen Calls Text/Structured-Status atomar angleichen.

### [Minor] Befund 3: Ungültige Werte werden in einzelnen Assembly-Fehler-/Statusantworten unnötig mit Targetmetadaten begleitet

- **Tool(s):** insbesondere Assembly-Kontext/Extensions, bei validem Target und ungültiger Option
- **Evidenz:** Einige `INVALID_ARGUMENT`-Antworten sind kurz und feldgenau; andere ungültige/unerwartete Optionen fallen in eine normale Assembly-Projektion mit umfangreichen Analyse-/Diagnosemetadaten. Der Fehlerpfad ist dadurch nicht überall gleich knapp.
- **Problem:** Für einen Agenten steigt das Signal-Rausch-Verhältnis unnötig, und es wird schwieriger, Validierungsfehler von fachlichen Assembly-Diagnosen ausschließlich strukturell zu unterscheiden.
- **Reproduktion:** Gültige Assembly verwenden; ungültiges optionales Enum senden und Antwortform mit einem `targetPath`-Fehler vergleichen.
- **Impact:** Mehr Kontextverbrauch und höhere Gefahr, eine Fallback-/Recovery-Antwort als fachliches Ergebnis zu interpretieren.
- **Konzeptbezug:** Ziel „semantische Dichte“, Fehlerqualität ohne Rauschen sowie Budgetschutz.
- **Empfehlung:** Vor Assembly-Analyse sämtliche Argumentvalidierung durchführen und bei `INVALID_ARGUMENT` ausschließlich den knappen Fehlervertrag zurückgeben.

## Freie Erkundung / Gesamtbewertung

- Positive Wiederholung nach Fehler bestätigt statelesses Verhalten im getesteten Source-Fall.
- `scopeType=""` wurde bei `find_symbol` wie Default/`all` behandelt. Da `scopeType` optional ist, ist dies nicht als sicherer Fehler eingestuft; das Verhalten sollte jedoch im Schema explizit als „leer = Default“ oder als Ablehnung dokumentiert werden.
- Insgesamt sind die Standard-Target-, Typ- und Limitfehler agententauglich. Die beiden Major-Befunde verhindern aber eine durchgehend deterministische, feldgenaue Recovery und sollten vor einem Abschluss des Konzeptvertrags behoben oder ausdrücklich als Restabweichung akzeptiert werden.
