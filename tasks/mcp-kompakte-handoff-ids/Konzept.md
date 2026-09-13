---
status: ready
execution_mode: autonomous
open_questions: []
---

# Konzept: Externe opaque Handoff-Handles

## 1. Ziel

AiNetLinter behält seine heutigen vollständigen Symbol-Handoff-IDs intern bei. An der MCP-Grenze werden sie durch kurze, opaque Handles der Form `h:…` ersetzt.

Optimiert wird ausschließlich die technische Adresse:

- Keine Analysefunktion und keine Toolkette geht verloren.
- Treffer, Signaturen, Pfade, Positionen, Paging und Detailinhalt bleiben erhalten.
- Der Agent gibt ein empfangenes `h:…` unverändert an ein passendes Folgetool weiter.
- Content-only und Zero-Transformation bleiben erfüllt.

Nicht Ziel sind kürzere fachliche Antworten, weniger Treffer oder eine neue Symbolauflösung.

## 2. Ausgangspunkt

Die heutigen internen IDs enthalten bereits alles, was AiNetLinter zur sicheren Auflösung benötigt:

- Source oder Assembly;
- Target-Identität;
- Snapshot-Identität;
- Roslyn-DocCommentId und damit die Symbolidentität.

Diese Logik bleibt bestehen. Nur das öffentlich sichtbare Format ändert sich.

Reale Baseline gegen `AiNetLinter.slnx`:

| Toolantwort | Gesamtzeichen | Zeichen in Handoff-IDs | Anteil |
|---|---:|---:|---:|
| `find_symbol` | 11.899 | 4.945 | 41,6 % |
| `get_file_skeleton` | 12.057 | 6.730 | 55,8 % |
| `find_references` | 10.534 | 4.000 | 38,0 % |
| `inspect_assembly` | 13.324 | 6.060 | 45,5 % |

## 3. Kernarchitektur

### 3.1 Interne und externe ID

```text
Ausgabe:
bestehende interne s:/a:-ID
    -> HandoffHandleRegistry.GetOrCreateOpaqueHandleForOutput(...)
    -> externes h:…
    -> MCP-Content

Eingabe:
MCP-Parameter mit h:…
    -> HandoffHandleRegistry.RestoreInternalHandoffForInput(...)
    -> bestehende interne s:/a:-ID
    -> heutiger Resolver und heutige Toollogik
```

Die Registry ist ein Adapter. Sie kennt weder Roslyn-Symbole noch Target-, Snapshot- oder Assembly-Semantik. Sie ordnet ausschließlich Strings einander zu.

Die bisherigen `s:`-/`a:`-Parser, Serializer und Resolver bleiben intern erhalten. Sie dürfen nur nicht mehr als öffentliches Drahtformat ausgegeben oder angenommen werden.

### 3.2 Zentrale Klasse und API

Arbeitsname:

```text
HandoffHandleRegistry
```

Verbindliche fachliche API-Namen:

```text
Result<string> GetOrCreateOpaqueHandleForOutput(string internalHandoffId)
Result<string> RestoreInternalHandoffForInput(string externalHandleOrSemanticInput)
```

`Opaque` und `ForOutput` sind bewusst Teil des Namens. Sie machen beim Programmieren sichtbar, dass der zurückgegebene Wert keine verständliche Semantik enthält.

Die XML-Dokumentation von `GetOrCreateOpaqueHandleForOutput` muss ausdrücklich festhalten:

> Das Handle ist nur eine technische Adresse. Der umgebende MCP-Text muss Symbolart, verständlichen Namen bzw. Signatur und bei Bedarf relativen Pfad sowie Position ausgeben.

Die Registry ist die einzige Source of Truth für beide Richtungen:

```text
internalToExternal: Dictionary<string, string>
externalToInternal: Dictionary<string, string>
```

Beide Dictionaries verwenden Ordinal-Vergleich.

Die Eingabemethode kapselt die komplette öffentliche Weiche: gültiges `h:…` restaurieren, öffentliches `s:`/`a:` ablehnen, alle anderen heute erlaubten semantischen Eingaben unverändert zurückgeben. Sie parst oder bewertet die restaurierte interne ID nicht. Erwartbare Format-, Lookup- und Kapazitätsfehler werden gemäß Projektkonvention als `Result<string>` zurückgegeben.

### 3.3 Nebenläufigkeit und 1:1-Zuordnung

Die beiden Richtungen müssen jederzeit eine konsistente Bijektion bilden.

- Derselbe interne Wert liefert während des Hostlaufs immer dasselbe externe Handle.
- Ein externes Handle verweist auf genau einen internen Wert.
- Zwei parallele Registrierungen desselben internen Werts erzeugen nicht zwei Handles.
- Ein neu erzeugter externer Wert wird erst sichtbar, wenn beide Richtungen eingetragen sind.

Zwei unabhängige `ConcurrentDictionary.GetOrAdd`-Aufrufe reichen dafür nicht aus. Die Erstellung eines neuen Mappings wird gemeinsam synchronisiert, beispielsweise mit einem kurzen Lock. Bereits vorhandene Zuordnungen dürfen ohne exklusiven Schreibpfad gelesen werden.

### 3.4 Handleformat

```text
h:<sessionNonce>.<counter>
```

- `sessionNonce`: beim Hoststart einmalig mit CSPRNG erzeugte 128 Bit, Base64url ohne Padding.
- `counter`: hostweit atomar steigender `UInt64`, Base36 ohne führende Nullen.
- Groß-/Kleinschreibung ist signifikant.
- Erlaubte Zeichen, Länge und genau ein Trennpunkt werden strikt geprüft.
- Bei Zählerüberlauf oder einer festen Sicherheitsgrenze schlagen nur neue Registrierungen mit `HANDOFF_CAPACITY_EXCEEDED` fehl.

Ein Zufallswert pro Handle ist nicht erforderlich. Session-Nonce plus Zähler ist kürzer, innerhalb der Sitzung kollisionsfrei und nach einem Neustart praktisch eindeutig.

Entschieden: Der kryptographisch zufällige 128-Bit-Session-Nonce gilt als ausreichende operative Garantie gegen eine falsche Auflösung nach einem Neustart. Eine persistente Zustandsablage ist dafür nicht erforderlich.

Ein Windows-Pfad wie `h:\...` darf nicht als Handle erkannt werden. Nur das vollständig gültige Format aktiviert die Registry-Auflösung.

### 3.5 Lebensdauer

Es gibt genau eine Registry pro MCP-Hostlauf:

- im Daemon gemeinsam für alle parallelen MCP-Verbindungen;
- im direkten stdio-Betrieb für die Laufzeit dieses Hostprozesses.

Jedes ausgegebene Handle bleibt bis zum Host-Shutdown in beiden Dictionaries erhalten.

Wichtig: Das Entfernen einer Solution- oder Assembly-Session aus dem bestehenden TTL-Cache löscht deren Handle-Mappings nicht. Andernfalls würde ein bereits ausgegebenes Handle vorzeitig ungültig und eine Agenten-Toolkette könnte nach einer Leerlaufphase brechen.

Das ist speicherseitig vertretbar, weil die Registry nur zwei Strings je Mapping hält. Sie hält keine Roslyn-Symbole, Compilations, Solutions, Leases oder Cache-Sessions fest. Nach einem TTL-Reload wird die restaurierte interne ID wie heute vom bestehenden Resolver geprüft.

Die Registry wird erst beim Host-Shutdown verworfen. Nach einem Neustart ist sie leer; alte Handles dürfen niemals auf neue Einträge zeigen.

## 4. Öffentlicher Vertrag

### 4.1 Ausgabe

- Öffentlich werden ausschließlich `h:…`-Handoffs ausgegeben.
- Eine bestehende interne `s:`-/`a:`-ID wird unmittelbar vor ihrer Ausgabe externalisiert.
- Der übrige Text bleibt fachlich gleich.
- Ein Mapping wird erst erzeugt, wenn der Handoff tatsächlich in den finalen Content gerendert wird.
- Gleichartige Renderer verwenden denselben zentralen Adapter, keine lokalen Maps.

Ein unverständlicher Output wie

```text
h:Abc.7 Ok
```

ist unzulässig. Er muss mindestens die heute vorhandene fachliche Bedeutung behalten, zum Beispiel:

```text
method HandoffHandleRegistry.RestoreInternalHandoffForInput(string externalHandleOrSemanticInput)
src/AiNetLinter/Mcp/.../HandoffHandleRegistry.cs:73
handoffId: h:Abc.7
```

### 4.2 Eingabe

Für jedes öffentliche Symbol-Eingabefeld gilt dieselbe Reihenfolge:

1. Ein syntaktisch gültiges `h:…` wird durch die Registry in die interne ID zurückübersetzt.
2. Eine öffentlich übergebene alte `s:`-/`a:`-Handoff-ID wird mit `UNSUPPORTED_HANDOFF_FORMAT` abgelehnt.
3. Andere heute erlaubte Eingaben werden unverändert an die vorhandene Logik weitergereicht.

Damit bleiben insbesondere direkte Symbolnamen, rohe DocCommentIds sowie `Datei:Zeile[:Spalte]` gültig.

Der harte Schnitt betrifft ausschließlich alte öffentliche Handoff-IDs. Intern bleiben diese IDs Implementierungsdetail.

### 4.3 Semantische Anzeige und Positionen

Das opaque Handle ersetzt keine Anzeigeinformation:

- Symbolart bleibt sichtbar.
- Verständlicher Name oder Signatur bleibt sichtbar.
- Relativer Pfad, Zeile und Spalte bleiben sichtbar, wenn sie heute ausgegeben werden.
- Reihenfolge, Gruppierung, Trefferzahl, Begrenzung und Paging bleiben unverändert.

Es wird kein `:Zeile` an das Symbolhandle angehängt. Ein Handle adressiert ein Symbol; die Position bleibt separat als `Pfad:Zeile[:Spalte]` sichtbar.

In dieser Umsetzung werden keine weiteren Layout- oder Deduplizierungsänderungen an den Antworten vorgenommen. So ist messbar, dass ausschließlich die ID-Darstellung optimiert wurde.

## 5. Einbaustellen

Die Umstellung muss vollständig sein. Vor der Implementierung wird per Codeinventur jede öffentliche Ausgabe und jedes passende Eingabefeld erfasst. Die folgenden Producer sind Prüfkandidaten, kein Auftrag, einem Tool neue Handoffs hinzuzufügen: Hat ein Tool heute keinen sichtbaren Handoff, wird sein Check mit „nicht betroffen“ und Codebeleg abgeschlossen.

### 5.1 Producer

- [ ] `find_symbol`
- [ ] `get_file_skeleton`
- [ ] `get_symbol_body`
- [ ] `find_references`
- [ ] `get_call_tree`
- [ ] `get_impact`
- [ ] `get_type_hierarchy`
- [ ] `find_implementations`
- [ ] `dependency_graph`
- [ ] `get_class_structure`
- [ ] `metrics_lookup`
- [ ] `get_feature_context`
- [ ] `get_test_context`
- [ ] `inspect_assembly`
- [ ] `search_assembly`
- [ ] `find_assembly_extensions`
- [ ] `get_assembly_context`
- [ ] alle weiteren Renderer, Formatter und DTO-Projektionen mit heutigen Handoff-IDs

Für jeden gefundenen Producer:

- [ ] jede interne ID wird genau an der Ausgabegrenze externalisiert;
- [ ] keine interne `s:`-/`a:`-ID gelangt in den Content;
- [ ] der semantische Begleittext bleibt mindestens gleichwertig;
- [ ] dieselbe interne ID ergibt immer dasselbe externe Handle.

### 5.2 Consumer

- [ ] `get_symbol_body.symbolIdentifiers[]`
- [ ] `metrics_lookup.symbolIdentifiers[]`
- [ ] `find_references.symbolIdentifier`
- [ ] `get_call_tree.symbolIdentifier`
- [ ] `get_impact.symbolIdentifier`
- [ ] `get_type_hierarchy.symbolIdentifier`
- [ ] `find_implementations.symbolIdentifier`
- [ ] `dependency_graph.symbolIdentifier`
- [ ] `get_class_structure.symbolIdentifier`
- [ ] `get_feature_context.symbolIdentifier`
- [ ] `get_test_context.symbolIdentifier`
- [ ] `get_assembly_context.symbolIdentifier`
- [ ] `find_duplicates.helperSymbol`
- [ ] `resolve_type_origin.typeName`
- [ ] alle weiteren öffentlichen Symbol-Eingabefelder

Für jeden gefundenen Consumer:

- [ ] externe Handles werden vor der bisherigen Fachlogik restauriert;
- [ ] direkte semantische Eingaben bleiben unverändert;
- [ ] öffentliche alte Handoff-Formate werden abgelehnt;
- [ ] ein Handle aus jedem fachlich passenden Producer kann unverändert verwendet werden.

Besondere Prüfung: `find_duplicates.helperSymbol` und `resolve_type_origin.typeName` akzeptieren heute nicht zuverlässig jeden ausgegebenen Handoff. Nach der Restaurierung müssen diese Pfade die interne Handoff-ID an den bestehenden passenden Resolver weiterreichen. Die Registry selbst erhält dafür keine Symbolsemantik.

### 5.3 Nicht betroffen

Ohne heutigen Symbol-Handoff-Vertrag bleiben unverändert:

- `continuationToken` und andere Paging-Tokens;
- Verify- und Diagnostics-Referenzen im Format `Pfad:Zeile[:Spalte]`;
- Dateibaum-, Namespace-, Metrikbaum-, Hotspot-, Index- und Scope-Parameter;
- Graph-Knoten, Feature- oder Testdaten ohne heutige öffentliche Handoff-ID.

Falls die Codeinventur dort doch eine öffentliche `s:`-/`a:`-ID findet, gehört diese konkrete Stelle wieder in Producer oder Consumer.

## 6. Fehlervertrag

Die Registry erzeugt nur mapperbezogene Fehler. Target-, Snapshot-, Symbolart- und Auflösungsfehler bleiben beim bestehenden internen Resolver.

| Code | Zuständigkeit |
|---|---|
| `INVALID_HANDOFF` | `h:…` ist syntaktisch ungültig. |
| `UNSUPPORTED_HANDOFF_FORMAT` | Eine öffentliche alte `s:`-/`a:`-ID wurde übergeben. |
| `HANDOFF_SESSION_MISMATCH` | Session-Nonce gehört nicht zum laufenden Host. |
| `HANDOFF_UNKNOWN` | Format und Session passen, aber das Mapping fehlt. |
| `HANDOFF_CAPACITY_EXCEEDED` | Kein neues Mapping kann sicher angelegt werden. |
| bestehende Fehler, z. B. `TARGET_MISMATCH` oder `STALE_SNAPSHOT` | Restaurierte interne ID wurde vom bestehenden Resolver abgelehnt. |

Fehlertexte geben keine vollständige interne ID aus. Ein unbekanntes oder altes Handle nennt als nächste Aktion die erneute Symbolermittlung.

## 7. Muss-Kriterien

- [ ] Die bestehenden internen Handoff-IDs und ihre Resolversemantik bleiben erhalten.
- [ ] Öffentlich werden ausschließlich kurze `h:…`-Handles verwendet.
- [ ] Es gibt genau eine zentrale `HandoffHandleRegistry` pro Hostlauf.
- [ ] Die Registry mappt ausschließlich String zu String und kennt keine Symbolsemantik.
- [ ] Beide Mappingrichtungen bleiben unter Parallelität konsistent.
- [ ] Kein Handle wird wegen Solution-/Assembly-TTL entfernt.
- [ ] Kein Registry-Eintrag hält Roslyn- oder Cache-Objekte am Leben.
- [ ] Alle Producer und Consumer verwenden die zentrale Registry.
- [ ] Sichtbare fachliche Informationen werden nicht reduziert.
- [ ] Content-only und Zero-Transformation bleiben erfüllt.

## 8. Akzeptanz- und Testfälle

### 8.1 Mapping

- [ ] Dieselbe interne ID liefert bei 1, 100 und parallelen Aufrufen dasselbe Handle.
- [ ] Unterschiedliche interne IDs liefern unterschiedliche Handles.
- [ ] Für jedes Mapping sind beide Dictionary-Richtungen vorhanden und konsistent.
- [ ] Roundtrip `intern -> extern -> intern` liefert exakt denselben String.
- [ ] Ordinal-Vergleich verhindert kulturabhängiges Verhalten.
- [ ] Zählerüberlauf und Sicherheitsgrenze verändern keine bestehenden Mappings.

### 8.2 Lebenszyklus

- [ ] Zwei parallele MCP-Verbindungen desselben Daemons teilen dieselbe Registry.
- [ ] Direkter stdio-Betrieb behält Handles über mehrere Requests.
- [ ] Solution-TTL-Eviction entfernt die Session, nicht das Mapping.
- [ ] Ein Handle funktioniert nach TTL-Reload weiterhin oder liefert den bisherigen internen Snapshotfehler.
- [ ] Alle Handles bleiben bis Host-Shutdown registriert.
- [ ] Nach Neustart liefert ein altes Handle `HANDOFF_SESSION_MISMATCH` und niemals ein neues Symbol.
- [ ] 10.000 und 100.000 Mappings werden hinsichtlich Speicher, Registrierungszeit und Lookup-Latenz gemessen.

### 8.3 Öffentlicher Vertrag

- [ ] In keiner MCP-Antwort erscheint eine interne `s:`-/`a:`-Handoff-ID.
- [ ] Öffentliche alte Handoff-IDs werden nicht akzeptiert.
- [ ] Direkte Namen, DocCommentIds und Positionen funktionieren wie zuvor.
- [ ] Ungültige, abgeschnittene, erweiterte und case-veränderte Handles werden abgelehnt.
- [ ] `h:\...` wird weiterhin als möglicher Windows-Pfad behandelt.
- [ ] Ein Outputvergleich mit normalisierten IDs zeigt keine verlorenen Treffer oder Fachinformationen.

### 8.4 Reale Toolketten

- [ ] `find_symbol -> get_symbol_body`
- [ ] `find_symbol -> find_references`
- [ ] `find_symbol -> get_type_hierarchy`
- [ ] `find_symbol -> metrics_lookup`
- [ ] `get_file_skeleton -> get_symbol_body`
- [ ] `find_references -> get_symbol_body`
- [ ] Source-Producer -> `find_duplicates.helperSymbol`
- [ ] Type-Producer -> `resolve_type_origin.typeName`
- [ ] `inspect_assembly -> get_assembly_context`
- [ ] Assembly-Referenz-Handoff -> passender Assembly-Consumer
- [ ] alle fachlich passenden Kombinationen der fertigen Producer-/Consumer-Matrix

### 8.5 Tokenmessung

Auf dem festgeschriebenen Baseline-Datensatz:

- [ ] Keine der vier Referenzantworten wächst in UTF-8-Bytes oder Tokens.
- [ ] Die Summe ihrer UTF-8-Bytes sinkt um mindestens 25 %.
- [ ] Die Summe der Zeichen in Handoff-IDs sinkt um mindestens 70 %.
- [ ] Die Gesamttokenzahl sinkt mit einem dokumentierten Codex-nahen Tokenizer.
- [ ] Der Vergleich bestätigt, dass die Einsparung aus den Handles und nicht aus entferntem Fachinhalt stammt.

## 9. Non-Goals

- Keine neue Symbolidentität und kein Ersatz der internen `s:`-/`a:`-Logik.
- Keine Änderung der Roslyn-Analyse oder ihrer Ergebnisse.
- Keine persistente Handle-Datenbank.
- Keine TTL-/LRU-Eviction für Handle-Mappings.
- Keine Umstellung von Paging-Tokens oder Positionsreferenzen.
- Keine zusätzliche Kürzung, Zusammenfassung oder Umordnung fachlicher Toolantworten.
- Keine Rückwärtskompatibilität für öffentliche alte Handoff-IDs.

## 10. Umsetzung und Release-Gate

- [ ] Vollständige Producer-/Consumer-Inventur erstellen.
- [ ] `HandoffHandleRegistry` mit zentralem Lebenszyklus und Parallelitätstests implementieren.
- [ ] alle Ausgaben unmittelbar vor dem Rendern externalisieren.
- [ ] alle Symbolparameter unmittelbar nach der Argumentvalidierung restaurieren.
- [ ] direkte semantische Eingaben unverändert durchreichen.
- [ ] öffentliche Altformat-Annahme und -Ausgabe entfernen, interne Resolver erhalten.
- [ ] semantischen Begleittext jeder Ausgabestelle prüfen.
- [ ] FastTests für Mapping, Format, Parallelität und Renderer ergänzen.
- [ ] Integrationstests für MCP-Wire, Toolketten, TTL und Neustart ergänzen.
- [ ] MCP-Dokumentation, Agent-Guide und Beispiele auf `h:…` aktualisieren.
- [ ] vollständige Non-Stress-Testgates, `dotnet build` und MCP-`verify(scope: solution)` erfolgreich ausführen.
- [ ] Code-/Textsuche bestätigt: keine öffentliche interne ID und keine unverdrahtete Handoff-Stelle.
- [ ] Baseline-Messung bestätigt Tokenersparnis ohne Informationsverlust.

Freigabestatus:

- [x] Die Neustart-Eindeutigkeit ist entschieden.
- [x] Der Nutzer hat den Draft ausdrücklich freigegeben.
- [x] Muss-Kriterien und Testfälle gelten als verbindlicher Umsetzungsvertrag.

Der spätere Orchestrator darf den beschriebenen Scope ohne Zwischenfreigaben vollständig bis zum Release-Gate umsetzen. Diese Freigabe startet die Umsetzung nicht automatisch.
