# Symbolnavigation – MCP-Live-/Vertragsaudit

## Audit-Rahmen

- Zeitpunkt: 2026-08-31
- Scope: `find_symbol`, `find_references`, `get_call_tree` und
  `get_type_hierarchy` auf Assembly-Zielen; `inspect_assembly` diente als
  metadata-only Einstieg zur Auswahl repräsentativer Symbole.
- Ziele: `repo-provided assembly`, `installed vendor assembly A` und
  `installed vendor assembly B`. Alle drei Live-Ziele wurden mit
  `targetType=assembly` und absoluten `targetPath`-Parametern angesprochen;
  die Pfade erscheinen absichtlich nicht in diesem Bericht.
- Alle drei Sessions waren `origin=decompiled`, `sourcePath=none`,
  `snapshot=none`, `confidence=medium`, `trust=untrusted` und
  `sessionStatus=partial`. Die Generation war beim repo-provided Ziel 2,
  bei den beiden installierten Zielen 1.
- Keine Produktions-/Testdatei, kein Build und kein Testlauf wurde ausgeführt.

## Kurzfazit

Die vier Navigationswerkzeuge unterscheiden Root-Snapshot und bounded
Referenzsuche grundsätzlich sichtbar: `includeReferences=false` bleibt auf
dem Ziel, `includeReferences=true` ergänzt Herkunftsmarker, gezählte bzw.
gekürzte Referenz-Sessions und partielle Diagnosen. Ein leerer Call-Site-Baum
ist bei den dekompilierten Proben reproduzierbar, darf aber wegen
`origin=decompiled`, `completeness=partial` und fehlenden Referenz-Sessions
nicht als globale Negativaussage gelesen werden.

Die wichtigsten agentischen Einschränkungen sind:

1. `get_call_tree` liefert bei `includeReferences=false` nur Text; die
   strukturierte Root-/Truncation-Projektion erscheint erst im bounded Lauf.
   `get_type_hierarchy` lieferte in allen erfolgreichen Proben ebenfalls nur
   Text und kein `structuredContent`.
2. Bounded Läufe bleiben durch Session-, Diagnose- und Referenzmetadaten groß.
   Kleine `maxResults`-/`topN`-Werte begrenzen die sichtbaren Knoten, nicht
   zuverlässig die Gesamtnutzlast.
3. `find_symbol` gibt für die Folgeabfrage Namen und Positionen, aber keine
   direkt wiederverwendbare generationsgebundene Assembly-Symbol-ID aus.
   Eine selbst gebildete `M:`-Form wurde deshalb als veraltete bzw. ungültige
   Generation abgewiesen; die qualifizierte Namensform funktionierte.

## Repräsentative Einstiegssymbole

`inspect_assembly` wurde auf allen drei Assembly-Zielen mit kleinem
`publicOnly=true`, `maxResults=3` und `maxMembers=5` aufgerufen. Anschließend
wurden pro Ziel sichtbare Typ-/Member-Funde für die Navigation redigiert:

| Zielklasse | Einstieg | Beobachtung |
|---|---|---|
| repo-provided assembly | `Type_1`, `Member_1` | Typ und Methode über `find_symbol` gefunden; Root-Analyse `partial`, Truncation sichtbar. |
| installed vendor assembly A | `Type_1`, `Member_1` | Typ gefunden; fehlende bzw. versionsabweichende Referenzen in Diagnostics sichtbar. |
| installed vendor assembly B | `Type_1`, `Member_1` | Typ und Methode gefunden; Referenz-Session für mindestens eine Framework-Referenz nicht eröffnungsfähig. |

Die kleinen API-Slices meldeten bei allen Zielen dekompilierte Herkunft und
partielle Analyse. Das repo-provided Ziel zeigte 69 Typen im aktuellen Slice-
Kontext, vendor A 126 und vendor B 180; die angezeigte Typ-/Member-Menge war
jeweils zusätzlich begrenzt. Diese Counts sind Bestands-/Sessionhinweise,
keine Zusage vollständiger navigierbarer Syntax.

## `find_symbol`

Geprüfte Varianten:

| Probe | Ergebnis |
|---|---|
| einzelnes Pattern `Type_1`, `includeReferences=false` | Ein Root-Treffer mit `analysis.origin=decompiled`, `generation` und `completeness=partial`; keine `navigation`-Sektion. |
| Batch mit zwei Patterns | Zwei getrennte Ergebnisgruppen; die Anfrage blieb deterministisch und pro Pattern auswertbar. |
| Typfilter | `kind=Class` bzw. `kind=Method` wurde angewandt. Bei einem breiten Methodenpattern wurden 3 Gesamt- und 2 gezeigte Treffer textuell ausgewiesen; der kleine `maxResults`-Slice war damit erkennbar. |
| unbekanntes Pattern | Erfolgreiche Leermenge (`matches=[]`), nicht `isError=true`; die Root-Analyse blieb trotzdem `partial`. |
| `maxResults` klein | Sichtbare Treffer wurden begrenzt; die Antwort blieb klein, solange `includeReferences=false` galt. |
| `includeReferences=true` | `navigation` erschien mit bounded Sessionzählung, `searchedAssemblyCount`, `assembliesTruncated=true` und `completeness=partial`. Treffer aus Referenz-Sessions trugen eigene Herkunftsobjekte mit `originKind`, Confidence und Trust. |
| Wiederholung | Gleicher kleiner Aufruf lieferte stabil dieselbe Generation und denselben strukturierten Kern. |

Response-Budget-Beobachtung: Root-only-Proben lagen ungefähr bei 1,2–1,8 k
Zeichen. Bounded Varianten lagen je nach Ziel ungefähr bei 4,8–16 k
Zeichen, obwohl der sichtbare Ergebnis-Slice klein war. Diagnostics und
Referenzlisten waren dabei selbst gekürzt; `maxResults` ist kein globales
Response-Budget.

**Kein Befund:** Batch-, Typfilter-, unbekanntes-Pattern- und
`includeReferences`-Schalter waren semantisch erkennbar. Die unbekannte
Leermenge wurde nicht als globale Aussage über die Assembly projiziert.

## `find_references`

Für einen gefundenen `Type_1` und den dazugehörigen `Member_1` wurden
`depth=1` und `depth=3`, kleine (`maxResults=1/2`) und größere
(`maxResults=20/50`) Limits sowie beide `includeReferences`-Werte geprüft.

### Root-only

- `includeReferences=false` lieferte `callSites=[]` und eine strukturierte
  `completeness` mit `requestedDepth`, `effectiveDepth`,
  `visitedNodeCount=1`, `totalCallSiteCount=0`,
  `shownCallSiteCount=0` sowie getrennten Truncation-Flags.
- `depth=3` wurde nicht stillschweigend auf 1 reduziert; die Antwort meldete
  `effectiveDepth=3`. Wegen der leeren dekompilierten Call-Site-Menge gab es
  bei kleinem und großem `maxResults` keinen inhaltlichen Unterschied.
- Eine Datei-/Zeilenform `File_1:line` mit mehreren Symbolen ergab klar
  `AMBIGUOUS_SYMBOL` und eine Kandidatenliste. Das ist von einer echten
  leeren Call-Site-Menge unterscheidbar.
- Eine ungültige bzw. generationsfremde `M:`-Form ergab klar
  `INVALID_ARGUMENT` oder `SYMBOL_NOT_FOUND`, nicht `callSites=[]`.

### Bounded Referenzsuche

- `includeReferences=true` ergänzte `navigation` mit bounded
  `totalAssemblyCount`, `searchedAssemblyCount`,
  `assembliesTruncated=true`, `completeness=partial` und Diagnosen zu nicht
  eröffnungsfähigen Sessions bzw. synthetischen Decompiler-Compilations.
- Die Call-Site-Liste blieb in den Proben leer; die Referenzsuche war dennoch
  nicht vollständig. Ein leerer Call-Site-Slice und partielle
  Sessiondiagnosen sind deshalb getrennt zu behandeln.
- Die bounded Antworten lagen ungefähr bei 17–18 k Zeichen, sowohl für
  `maxResults=1` als auch für 20/50. Das sichtbare `maxResults`-Limit schützt
  die Metadatenlast nicht.

**Befund SN-001 (P1, systemisch):** Die direkte Folgeverkettung aus
`find_symbol` ist nicht vollständig selbstbeschreibend. `find_symbol` liefert
für Assembly-Ziele Namen/Positionen, aber keine generationsgebundene
`assembly:<hash>:<generation>:<symbolId>`-ID. Eine daraus selbst gebildete
`M:`-Form wurde abgewiesen; die qualifizierte Namensform `Type_1.Member_1`
war nutzbar.

Disposition: Im aktuellen Audit nur dokumentiert. Für einen stabilen
Agentenvertrag sollten `find_symbol` entweder eine weiterverwendbare
Assembly-Symbol-ID ausgeben oder die erlaubte Namensfolge explizit und
strukturiert markieren.

**Kein Befund:** Fehlerklassifikationen (`INVALID_ARGUMENT`,
`SYMBOL_NOT_FOUND`, `AMBIGUOUS_SYMBOL`) und die strukturierte Trennung von
Call-Sites, Completeness und Navigation waren für Agenten unterscheidbar.

## `get_call_tree`

Mit `Member_1` wurden alle Richtungen (`incoming`, `outgoing`, `both`), beide
Formate (`ascii`, `mermaid`), `depth=1/2/3`, kleine `topN=1/2/3`-Werte sowie
`includeReferences=false/true` geprüft.

- Root-only (`includeReferences=false`) zeigte den Root-Knoten bzw. das
  Mermaid-Flowchart als Text, aber `structuredContent` war leer. Es gab keine
  maschinenlesbare `truncated`-, `navigation`- oder Completeness-Projektion.
- Alle Richtungen und Formate wurden angenommen. Bei `depth=1` bis 3 und
  kleinen `topN`-Werten blieb nur der Root sichtbar; es gab keine sichtbaren
  Call-Sites.
- Bounded (`includeReferences=true`) lieferte `root`, `navigation`,
  `truncated` und `analysis`. `navigation.completeness` war `partial`, die
  Referenz-Sessions waren bounded und die Diagnosen teilweise gekürzt.
- Im bounded Root waren Herkunftsmarker am Display-Knoten sichtbar. Der
  strukturierte `truncated`-Wert war in den leeren Proben `false`; das bedeutet
  nur, dass der angefragte Baum keine Knoten ausgab, nicht dass die
  dekompilierte Analyse global vollständig war.

**Befund SN-002 (P1, systemisch):** Root-only und bounded
`get_call_tree` haben unterschiedliche Response-Formen. Ein Agent, der nur
`structuredContent` verarbeitet, sieht im Root-only-Modus keinen Baum und
keine Vollständigkeitsmetadaten. Bei `get_type_hierarchy` war die erfolgreiche
Antwort ebenfalls nur textuell.

Disposition: Im aktuellen Audit nur dokumentiert. Strukturierte Root-,
Completeness- und Truncation-Felder sollten formatunabhängig auch ohne
Referenzsuche geliefert werden; Text bleibt ein lesbarer Fallback.

## `get_type_hierarchy`

Mit je einem gefundenen `Type_1` pro Ziel und `maxResults=2` wurden erfolgreiche
Typabfragen ausgeführt.

- Die Antworten zeigten Basisklassen, externe Interfaces und abgeleitete
  Typen; die sichtbare abgeleitete Menge war auf den kleinen Wert begrenzt.
- Für die geprüften Typen wurden keine abgeleiteten Typen angezeigt; das ist
  eine echte leere Ergebnismenge innerhalb des angefragten Typscopes, aber
  wegen der global partiellen dekompilierten Session keine Aussage über alle
  möglichen Referenz-Assemblies.
- `origin=decompiled`, `generation` und `completeness=partial` erschienen im
  Textvorspann. Ein separates strukturiertes Ergebnis mit `maxResults`-,
  Truncation- oder Candidate-Feldern erschien nicht.

**Kein Befund:** Das kleine `maxResults`-Verhalten sowie die textuelle
Darstellung der Hierarchie waren reproduzierbar; ein falscher globaler
Negativbefund wurde nicht ausgegeben.

## Agentische Bewertung

### Decompilation und fehlende Call-Sites

Die dekompilierten Proben enthielten synthetische Compilation- und
Referenzdiagnosen. `find_references` und `get_call_tree` konnten für
`Member_1` reproduzierbar `callSites=[]` bzw. nur den Root ausgeben. Das ist
ein Befund über den sichtbaren, partiellen Navigation-Scope. Agenten dürfen
daraus nicht ableiten, dass `Member_1` assemblyweit nicht aufgerufen wird.

### Referenz-Sessions und bounded Semantik

`includeReferences=true` öffnete keine unbeschränkte Weltansicht. Beim
repo-provided Ziel wurden von 107 bekannten Assemblies 32 durchsucht, bei
vendor B von 77 ebenfalls 32; jeweils waren Sessions bzw. Diagnosen gekürzt.
Bei vendor A waren die vorhandenen Referenz-Sessions klein, aber mindestens
eine versionsabweichende Framework-Referenz blieb partiell. Herkunftsmarker
trennen Root- und Referenztreffer, ersetzen aber keine Vollständigkeitsgarantie.

### Negative Ergebnisse

Die Live-Proben trennten drei Fälle klar:

| Fall | Agentische Bedeutung |
|---|---|
| `matches=[]` bei unbekanntem Pattern | Keine sichtbare Übereinstimmung im angefragten Scope; wegen `partial` keine globale Negativaussage. |
| `callSites=[]` mit strukturierter Completeness | Keine sichtbare Call-Site im angefragten Navigationsscope; nicht „nirgendwo aufrufbar“. |
| `INVALID_ARGUMENT`/`SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL` | Anfrage- oder Auflösungsproblem; nicht mit einer erfolgreichen Leermenge verwechseln. |

### Response-Budget und Schlussregel

Vor jeder inhaltlichen Schlussfolgerung müssen Agenten mindestens
`origin`, `generation`, `sessionStatus`, `completeness`, `truncated`,
`navigation.completeness`, Herkunftsmarker, Diagnostics und
Referenzzählungen auswerten. Kleine `maxResults`, `maxMembers` oder `topN`
begrenzen nur die sichtbare Ergebnismenge. Für bounded Navigation ist ein
zusätzliches Gesamtbudget im Wire-Vertrag nicht erkennbar.

## Findings und Disposition

| ID | Umfang | Dringlichkeit | Finding | Disposition |
|---|---|---|---|---|
| SN-001 | Symbol-Folgeabfragen auf Assembly-Zielen | P1, systemisch | `find_symbol` liefert keine direkt weiterverwendbare generationsgebundene ID; selbst gebildete `M:`-Formen können als ungültig abgewiesen werden. | Dokumentiert; strukturierte Folge-ID oder explizite Namensfolge im Vertrag ergänzen. |
| SN-002 | `get_call_tree`/`get_type_hierarchy` Response-Form | P1, systemisch | Root-only-Ergebnisse sind bei den geprüften Tools nur textuell; strukturierte Navigation/Completeness fehlt oder ist nicht konsistent. | Dokumentiert; strukturierte Felder auch im Root-only-Modus ausgeben. |
| SN-003 | Bounded Navigation und Antwortbudget | P1, systemisch | Referenz-/Diagnosemetadaten können trotz kleiner sichtbarer Limits mehrere 10 k Zeichen erzeugen; kein globales Response-Budget beobachtet. | Dokumentiert; Agenten mit kleinen Startlimits arbeiten lassen und bounded Metadaten explizit budgetieren. |

**Kein weiterer Befund:** Richtungs-, Format-, Tiefen-, Batch-, Typfilter-,
Negativ- und Herkunftssemantik waren in den geprüften Proben nachvollziehbar.
Die beobachteten leeren Call-Site-Mengen werden ausschließlich als partielle,
dekompilierte Scope-Ergebnisse gewertet.
