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
internalToExternal: ConcurrentDictionary<string, string>
externalToInternal: ConcurrentDictionary<string, string>
```

Beide Dictionaries verwenden Ordinal-Vergleich.

Die Eingabemethode kapselt die komplette öffentliche Weiche: gültiges `h:…` restaurieren, öffentliches `s:`/`a:` ablehnen, alle anderen heute erlaubten semantischen Eingaben unverändert zurückgeben. Sie parst oder bewertet die restaurierte interne ID nicht. Erwartbare Format-, Lookup- und Kapazitätsfehler werden gemäß Projektkonvention als `Result<string>` zurückgegeben.

### 3.3 Nebenläufigkeit und 1:1-Zuordnung

Die beiden Richtungen müssen jederzeit eine konsistente Bijektion bilden.

- Derselbe interne Wert liefert während des Hostlaufs immer dasselbe externe Handle.
- Ein externes Handle verweist auf genau einen internen Wert.
- Zwei parallele Registrierungen desselben internen Werts erzeugen nicht zwei Handles.
- Ein neu erzeugter externer Wert wird erst sichtbar, wenn beide Richtungen eingetragen sind.

Zwei unabhängige `ConcurrentDictionary.GetOrAdd`-Aufrufe reichen dafür nicht aus. Die Erstellung eines neuen Mappings wird durch einen gemeinsamen kurzen Lock synchronisiert. Innerhalb dieses Locks wird die interne ID erneut gesucht, der nächste externe Wert reserviert und dauerhaft geschrieben und anschließend werden beide Mappingrichtungen ergänzt. Bereits vorhandene Zuordnungen dürfen ohne exklusiven Schreibpfad gelesen werden.

### 3.4 Handleformat

```text
h:<alphabetischerZaehler>
```

- Der erste auf einer frischen Installation ausgegebene Wert ist `h:a`.
- Der letzte ausgegebene Zählerstring wird dauerhaft gespeichert; ein numerischer Counter ist nicht erforderlich.
- Alphabet, in dieser Reihenfolge: `abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ`.
- `a` ist das erste, `Z` das letzte Zeichen. Groß-/Kleinschreibung ist signifikant.
- Inkrementiert wird von rechts. `Z` wird zu `a` und erzeugt einen Übertrag nach links; bei einem Übertrag über die erste Stelle wird links `a` ergänzt.
- Beispiele: `h:a -> h:b`, `h:z -> h:0`, `h:9 -> h:A`, `h:Z -> h:aa` und `h:aaZaZ -> h:aaZba`.
- Gültig ist ausschließlich `h:` gefolgt von mindestens einem Zeichen des Alphabets.

Der persistierte Wert ist eine globale High-Water-Mark: Ein einmal reservierter Zählerwert wird nie wieder vergeben, auch wenn das zugehörige flüchtige Mapping nach einem Neustart nicht mehr existiert. Eine Lücke nach erfolgreicher Persistierung und anschließendem Prozessabbruch ist erlaubt; Wiederverwendung ist verboten.

Die kleine Zustandsdatei liegt updatefest unter `LocalApplicationData/RalfHuesing/AiNetLinter/handoff-counter.json`, also im bereits verwendeten per-user Daemon-State-Verzeichnis und nicht im austauschbaren EXE-Verzeichnis. Sie enthält ausschließlich Formatversion und zuletzt reservierten Zählerwert, beispielsweise `{ "formatVersion": 1, "lastIssued": "aaZaZ" }`, niemals interne IDs, Targets, Pfade oder Symbolinformationen.

Ein Windows-Pfad wie `h:\...` darf nicht als Handle erkannt werden. Nur das vollständig gültige Format aktiviert die Registry-Auflösung.

Beim ersten Start ohne Zustandsdatei wird diese atomar angelegt und `h:a` als erster Wert reserviert. Ein neuer Wert darf erst nach erfolgreicher atomarer Persistierung in den Dictionaries sichtbar und im MCP-Content ausgegeben werden. Die Persistierung darf nicht debounced werden. Bei vorhandenem, aber nicht lesbarem, inkonsistentem oder nicht schreibbarem Counter-State werden keine neuen Handles vergeben und der Server liefert `HANDOFF_COUNTER_UNAVAILABLE`; er setzt niemals still auf `a` zurück.

Derselbe Allocator muss bei möglichen parallelen Hostprozessen den Dateizugriff prozessübergreifend serialisieren. Unter diesem exklusiven Zugriff liest er die aktuelle High-Water-Mark erneut von der Datei, inkrementiert sie und ersetzt die Datei atomar. So können weder zwei Prozesse noch mehrere Thin-Client-Requests denselben Wert reservieren.

Das manuelle Löschen der Counter-Datei ist ein expliziter destruktiver Identitätsreset außerhalb des normalen Betriebsvertrags. Es kann frühere Werte erneut verfügbar machen und muss daher zusammen mit der Anweisung dokumentiert werden, alte Chats danach nicht weiterzuverwenden. Normale Binärupdates lassen das per-user State-Verzeichnis unangetastet.

### 3.5 Lebensdauer

Es gibt genau eine Registry pro MCP-Hostlauf:

- im Daemon gemeinsam für alle parallelen MCP-Verbindungen;
- im direkten stdio-Betrieb für die Laufzeit dieses Hostprozesses.

Jedes ausgegebene Handle bleibt bis zum Host-Shutdown in beiden Dictionaries erhalten. Beim Host-Shutdown werden beide Dictionaries verworfen; nur die High-Water-Mark bleibt bestehen.

Wichtig: Das Entfernen einer Solution- oder Assembly-Session aus dem bestehenden TTL-Cache löscht deren Handle-Mappings nicht. Andernfalls würde ein bereits ausgegebenes Handle vorzeitig ungültig und eine Agenten-Toolkette könnte nach einer Leerlaufphase brechen.

Das ist speicherseitig vertretbar, weil die Registry nur zwei Strings je Mapping hält. Sie hält keine Roslyn-Symbole, Compilations, Solutions, Leases oder Cache-Sessions fest. Nach einem TTL-Reload innerhalb desselben Hostlaufs wird die restaurierte interne ID wie heute vom bestehenden Resolver geprüft.

Nach einem Daemon- oder stdio-Host-Neustart sind alte Handles bewusst nicht mehr auflösbar. Da die persistierte High-Water-Mark eine erneute Vergabe verhindert, liefern sie immer `HANDOFF_UNKNOWN` und können niemals auf ein neues Symbol zeigen. Der Agent ermittelt das Symbol anhand des weiterhin sichtbaren Namens, der Signatur und des Fundorts erneut.

### 3.6 Bereits implementierte Grundlagen (Namespace `AiNetLinter.Mcp.Handoffs`)

Die folgenden Bausteine sind vollständig implementiert und durch Unit-Tests abgesichert:

1. **`HandoffCounterAlphabet`** (`src/AiNetLinter/Mcp/Handoffs/HandoffCounterAlphabet.cs`):
   - Reines Base-62-Zähl- und Validierungsmodul (`a..z`, `0..9`, `A..Z`).
   - Startwert `"a"` (`h:a`), inkrementiert von rechts mit Übertrag nach links (`z -> 0`, `9 -> A`, `Z -> aa`, `aaZaZ -> aaZba`).
   - Validierungsmethoden `IsValidCounter`, `IsValidHandle` und `IsWindowsDrivePath` (schließt `h:\...` als Pfad aus).

2. **`HandoffCounterStore`** (`src/AiNetLinter/Mcp/Handoffs/HandoffCounterStore.cs`):
   - Atomare High-Water-Mark-Persistierung unter `LocalApplicationData/RalfHuesing/AiNetLinter/handoff-counter.json`.
   - Prozessübergreifende Serialisierung über Lock-Datei `handoff-counter.json.lock` (`FileShare.None`).
   - **Interner Prefetch-Puffer (DefaultBatchSize = 1000)**: Reserviert bei leerem Puffer 1.000 IDs auf einen Schlag mit genau einem atomaren Disk-Write. Folgende `Next()`-Aufrufe werden mit ~15 ns direkt aus dem RAM bedient.
   - Meldet bei beschädigter Datei `HANDOFF_COUNTER_UNAVAILABLE` und setzt niemals still auf `"a"` zurück.

3. **`HandoffHandleRegistry`** (`src/AiNetLinter/Mcp/Handoffs/HandoffHandleRegistry.cs`):
   - `HandoffHandleRegistry.Default` als Singleton pro Hostlauf.
   - `GetOrCreateOpaqueHandleForOutput(string internalHandoffId)`: Liefert `Result<string>` mit `h:...`.
   - `RestoreInternalHandoffForInput(string externalHandleOrSemanticInput)`: Zentrale Weiche für Eingaben (`h:...` restaurieren, `s:`/`a:` ablehnen, semantische Eingaben und Windows-Pfade unverändert durchreichen).

4. **`Result<T>` & `ResultError`** (`src/AiNetLinter/Mcp/Handoffs/Result.cs`):
   - Unveränderliche Ergebnistypen für typsichere Fehlerbehandlung ohne Exceptions.

5. **`LinterErrorCodes`** (`src/AiNetLinter/Output/LinterErrorCodes.cs`):
   - `INVALID_HANDOFF`, `UNSUPPORTED_HANDOFF_FORMAT`, `HANDOFF_UNKNOWN`, `HANDOFF_COUNTER_UNAVAILABLE`.

6. **FastTests** (`src/AiNetLinter.FastTests/Mcp/Handoffs/`):
   - `HandoffCounterAlphabetTests.cs`, `HandoffCounterStoreTests.cs`, `HandoffHandleRegistryTests.cs`.

## 4. Öffentlicher Vertrag

### 4.1 Ausgabe

- Öffentlich werden ausschließlich `h:…`-Handoffs ausgegeben.
- Eine bestehende interne `s:`-/`a:`-ID wird unmittelbar vor ihrer Ausgabe externalisiert.
- Der übrige Text bleibt fachlich gleich.
- Ein Mapping wird erst erzeugt, wenn der Handoff tatsächlich in den finalen Content gerendert wird.
- Gleichartige Renderer verwenden denselben zentralen Adapter, keine lokalen Maps.

Ein unverständlicher Output wie

```text
h:aaZaZ Ok
```

ist unzulässig. Er muss mindestens die heute vorhandene fachliche Bedeutung behalten, zum Beispiel:

```text
method HandoffHandleRegistry.RestoreInternalHandoffForInput(string externalHandleOrSemanticInput)
src/AiNetLinter/Mcp/.../HandoffHandleRegistry.cs:73
handoffId: h:aaZaZ
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

- [x] `find_symbol` (Gruppe 1 migriert)
- [x] `get_file_skeleton` (Gruppe 2 migriert)
- [x] `get_symbol_body` (Gruppe 1 migriert)
- [x] `find_references` (Gruppe 1 migriert, CallGraph-Knoten)
- [ ] `get_call_tree`
- [ ] `get_impact`
- [x] `get_type_hierarchy` (Gruppe 2 migriert)
- [x] `find_implementations` (Gruppe 2 migriert)
- [ ] `dependency_graph`
- [x] `get_class_structure` (Gruppe 2 migriert)
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

- [x] `get_symbol_body.symbolIdentifiers[]` (Gruppe 1 migriert)
- [ ] `metrics_lookup.symbolIdentifiers[]`
- [x] `find_references.symbolIdentifier` (Gruppe 1 migriert)
- [ ] `get_call_tree.symbolIdentifier`
- [ ] `get_impact.symbolIdentifier`
- [ ] `get_type_hierarchy.symbolIdentifier`
- [x] `find_implementations.symbolIdentifier` (Gruppe 2 migriert)
- [ ] `dependency_graph.symbolIdentifier`
- [x] `get_class_structure.symbolIdentifier` (Gruppe 2 migriert)
- [ ] `get_feature_context.symbolIdentifier`
- [ ] `get_test_context.symbolIdentifier`
- [ ] `get_assembly_context.symbolIdentifier`
- [ ] `find_duplicates.helperSymbol`
- [x] `resolve_type_origin.typeName` (Gruppe 2 migriert)
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

### 5.4 Audit- und Rot-Test-Werkzeuge für Folge-Agenten

Um die vollständige Umstellung aller Einbaustellen deterministisch abzusichern und Regressionen auszuschließen, stehen zwei Werkzeuge zur Verfügung:

#### 1. Automatisches Audit-Skript (Rot-Test)
```powershell
pwsh -File scripts/audit-handoff-wiring.ps1
```
- **Zweck:** Durchsucht alle 17 Producer-Dateien nach fehlendem `GetOrCreateOpaqueHandleForOutput` und alle 10 Consumer-Dateien nach fehlendem `RestoreInternalHandoffForInput`.
- **Verhalten:**
  - Solange unmigrierte Stellen vorhanden sind, listet das Skript jede betroffene Datei auf und bricht mit **Exit-Code 1** ab (**Rot-Test**).
  - Sobald alle Stellen angebunden sind, meldet es Erfolg und beendet mit **Exit-Code 0** (**Grün-Test**).
- **Start-Baseline:** Stand zu Beginn von Phase 2 sind genau 27 offene Stellen (17 Producer, 10 Consumer).

#### 2. Ripgrep (`rg`)-Suchmuster für manuelle Verifikation
- **Producer-Ausgaben finden:**
  ```bash
  rg "handoffId:\s*`" src/AiNetLinter/
  rg "(identity|assemblyIdentity|HandoffIdentity)\.Format\(" src/AiNetLinter/Mcp/
  ```
- **Alte Drahtformate im Content aufspüren (Wire-Rot-Test):**
  ```bash
  rg "\b[sa]:[a-zA-Z0-9_-]{22}:[a-zA-Z0-9_-]{22}:"
  ```
  Darf in keiner MCP-Antwort mehr Treffer liefern!
- **Consumer-Eingangsparameter finden:**
  ```bash
  rg "\b(symbolIdentifier|symbolIdentifiers|helperSymbol|typeName)\b" src/AiNetLinter/Mcp/Tools/
  ```


## 6. Fehlervertrag

Die Registry erzeugt nur mapperbezogene Fehler. Target-, Snapshot-, Symbolart- und Auflösungsfehler bleiben beim bestehenden internen Resolver.

| Code | Zuständigkeit |
|---|---|
| `INVALID_HANDOFF` | `h:…` ist syntaktisch ungültig. |
| `UNSUPPORTED_HANDOFF_FORMAT` | Eine öffentliche alte `s:`-/`a:`-ID wurde übergeben. |
| `HANDOFF_UNKNOWN` | Das flüchtige Mapping fehlt, typischerweise nach einem Host-Neustart. |
| `HANDOFF_COUNTER_UNAVAILABLE` | Die High-Water-Mark kann nicht sicher gelesen oder atomar fortgeschrieben werden. |
| bestehende Fehler, z. B. `TARGET_MISMATCH` oder `STALE_SNAPSHOT` | Restaurierte interne ID wurde vom bestehenden Resolver abgelehnt. |

Fehlertexte geben keine vollständige interne ID aus. `HANDOFF_UNKNOWN` erklärt, dass der MCP-Host möglicherweise neu gestartet wurde und fordert zur erneuten Symbolermittlung über `find_symbol`, `get_file_skeleton` oder einen anderen passenden Producer auf.

## 7. Muss-Kriterien

- [ ] Die bestehenden internen Handoff-IDs und ihre Resolversemantik bleiben erhalten.
- [ ] Öffentlich werden ausschließlich kurze `h:…`-Handles verwendet.
- [ ] Es gibt genau eine zentrale `HandoffHandleRegistry` pro Hostlauf.
- [ ] Die Registry mappt ausschließlich String zu String und kennt keine Symbolsemantik.
- [ ] Beide Mappingrichtungen bleiben unter Parallelität konsistent.
- [ ] Nur die alphabetische High-Water-Mark wird dauerhaft gespeichert; die Mappings bleiben flüchtig.
- [ ] Ein externer Wert wird über Host-Neustarts hinweg niemals wiederverwendet.
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
- [ ] Die festgelegten Alphabetgrenzen und Überträge liefern exakt `z -> 0`, `9 -> A`, `Z -> aa` und `aaZaZ -> aaZba`.
- [ ] Ein neuer Wert wird erst nach erfolgreicher atomarer Persistierung ausgegeben.
- [ ] Parallele Registrierungen reservieren keinen Wert doppelt.
- [ ] Ein Persistierungsfehler liefert `HANDOFF_COUNTER_UNAVAILABLE` und setzt den Zähler niemals zurück.

### 8.2 Lebenszyklus

- [ ] Zwei parallele MCP-Verbindungen desselben Daemons teilen dieselbe Registry.
- [ ] Direkter stdio-Betrieb behält Handles über mehrere Requests.
- [ ] Solution-TTL-Eviction entfernt die Session, nicht das Mapping.
- [ ] Ein Handle funktioniert nach TTL-Reload weiterhin oder liefert den bisherigen internen Snapshotfehler.
- [ ] Alle Handles bleiben bis Host-Shutdown registriert.
- [ ] Nach Neustart sind beide Dictionaries leer und die Vergabe beginnt hinter der gespeicherten High-Water-Mark.
- [ ] Ein altes Handle liefert nach Neustart `HANDOFF_UNKNOWN` und niemals ein neues Symbol.
- [ ] Die Counter-Datei enthält keine internen IDs, Targets, Pfade oder Symbolinformationen.
- [ ] Normale EXE-Updates ersetzen die Counter-Datei im per-user State-Verzeichnis nicht.
- [ ] 10.000 und 100.000 Mappings werden hinsichtlich Speicher, Registrierungszeit und Lookup-Latenz gemessen.

### 8.3 Öffentlicher Vertrag

- [ ] In keiner MCP-Antwort erscheint eine interne `s:`-/`a:`-Handoff-ID.
- [ ] Öffentliche alte Handoff-IDs werden nicht akzeptiert.
- [ ] Direkte Namen, DocCommentIds und Positionen funktionieren wie zuvor.
- [ ] Ungültige, abgeschnittene, erweiterte und case-veränderte Handles werden abgelehnt.
- [ ] `h:\...` wird weiterhin als möglicher Windows-Pfad behandelt.
- [ ] Ein Outputvergleich mit normalisierten IDs zeigt keine verlorenen Treffer oder Fachinformationen.

### 8.4 Reale Toolketten

- [x] `find_symbol -> get_symbol_body` (Gruppe 1 Unit & E2E getestet)
- [x] `find_symbol -> find_references` (Gruppe 1 Unit & Integration getestet)
- [x] `find_symbol -> get_type_hierarchy` (Gruppe 2 Unit & Integration getestet)
- [ ] `find_symbol -> metrics_lookup`
- [x] `get_file_skeleton -> get_symbol_body` (Gruppe 2 Unit & Integration getestet)
- [ ] `find_references -> get_symbol_body`
- [ ] Source-Producer -> `find_duplicates.helperSymbol`
- [x] Type-Producer -> `resolve_type_origin.typeName` (Gruppe 2 Unit & Integration getestet)
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
- Keine persistente Zuordnung von externen zu internen IDs.
- Keine TTL-/LRU-Eviction für Handle-Mappings.
- Keine Umstellung von Paging-Tokens oder Positionsreferenzen.
- Keine zusätzliche Kürzung, Zusammenfassung oder Umordnung fachlicher Toolantworten.
- Keine Rückwärtskompatibilität für öffentliche alte Handoff-IDs.

## 10. Umsetzung und Release-Gate

- [x] Vollständige Producer-/Consumer-Inventur erstellen (in Abschnitt 5 & `scripts/audit-handoff-wiring.ps1`).
- [x] `HandoffHandleRegistry` mit zentralem Lebenszyklus und Parallelitätstests implementieren.
- [x] alphabetischen Zähler und atomaren High-Water-Mark-Store mit internem Batch-Prefetch im bestehenden per-user Daemon-State-Verzeichnis implementieren.
- [ ] alle Ausgaben unmittelbar vor dem Rendern externalisieren.
- [ ] alle Symbolparameter unmittelbar nach der Argumentvalidierung restaurieren.
- [ ] direkte semantische Eingaben unverändert durchreichen.
- [ ] öffentliche Altformat-Annahme und -Ausgabe entfernen, interne Resolver erhalten.
- [ ] semantischen Begleittext jeder Ausgabestelle prüfen.
- [ ] FastTests für Mapping, Format, Parallelität und Renderer ergänzen (Mapping/Format/Store-Tests fertig; Renderer-Tests folgen in Phase 2).
- [ ] Integrationstests für MCP-Wire, Toolketten, TTL, Neustart, Persistierungsfehler und mögliche parallele Hostprozesse ergänzen.
- [ ] MCP-Dokumentation, Agent-Guide und Beispiele auf `h:…` aktualisieren; Lebensdauer, Neustartfehler, Counter-State-Pfad und Wiederermittlung ausdrücklich dokumentieren.
- [ ] vollständige Non-Stress-Testgates, `dotnet build` und MCP-`verify(scope: solution)` erfolgreich ausführen.
- [ ] Code-/Textsuche bestätigt: keine öffentliche interne ID und keine unverdrahtete Handoff-Stelle (Audit-Skript Exit-Code 0).
- [ ] Baseline-Messung bestätigt Tokenersparnis ohne Informationsverlust.

Freigabestatus:

- [x] Die Neustart-Eindeutigkeit ist entschieden.
- [x] Der Nutzer hat den Draft ausdrücklich freigegeben.
- [x] Muss-Kriterien und Testfälle gelten als verbindlicher Umsetzungsvertrag.

Der spätere Orchestrator darf den beschriebenen Scope ohne Zwischenfreigaben vollständig bis zum Release-Gate umsetzen. Diese Freigabe startet die Umsetzung nicht automatisch.
