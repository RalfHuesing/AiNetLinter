---
status: draft
execution_mode: requires_user_decision
open_questions:
  - id: restart_uniqueness
    question: "Gilt ein pro MCP-Hoststart kryptographisch zufälliger 128-Bit-Session-Nonce als ausreichende Garantie dafür, dass ein Handle aus einer früheren Sitzung niemals auf ein neues Symbol zeigt?"
    recommendation: "Ja. Ohne persistente Zustandsablage ist dies die stärkste praktikable Lösung. Eine Kollision ist theoretisch möglich, kryptographisch aber vernachlässigbar."
---

# Konzept: Token-effiziente MCP-Handoff-Handles

## 1. Ziel und Nicht-Ziel

AiNetLinter ersetzt die langen öffentlichen Symbol-Handoff-IDs durch kurze, opaque Handles der Form `h:…`.

Das Vorhaben optimiert ausschließlich die technische Adressierung zwischen MCP-Tools:

- Der fachliche Informationsgehalt einer Antwort bleibt erhalten.
- Bestehende Toolketten und Eingabemöglichkeiten bleiben erhalten.
- Verständliche Symbolart, Name bzw. Signatur sowie erforderliche Pfad- und Positionsangaben bleiben sichtbar.
- Content-only und Zero-Transformation bleiben unverändert.

Nicht Ziel sind kürzere fachliche Antworten, weniger Treffer, das Entfernen von Navigationsmöglichkeiten oder neue Analysefunktionen.

## 2. Gemessener Ausgangspunkt

Reale Abfragen gegen `AiNetLinter.slnx` zeigen, dass die heutigen `s:`-/`a:`-IDs einen großen Teil der Ausgabe belegen:

| Toolantwort | Gesamtzeichen | Zeichen in Handoff-IDs | Anteil |
|---|---:|---:|---:|
| `find_symbol` | 11.899 | 4.945 | 41,6 % |
| `get_file_skeleton` | 12.057 | 6.730 | 55,8 % |
| `find_references` | 10.534 | 4.000 | 38,0 % |
| `inspect_assembly` | 13.324 | 6.060 | 45,5 % |

Diese Werte sind die Baseline für die spätere Messung. Die Optimierung muss die IDs verkürzen, nicht den übrigen Inhalt.

## 3. Verbindlicher öffentlicher Vertrag

### 3.1 Harter Schnitt

- Neu ausgegebene Symbol-Handoffs beginnen ausschließlich mit `h:`.
- Öffentliche Eingabefelder akzeptieren keine alten `s:`- oder `a:`-Handoff-IDs mehr.
- Dafür gibt es keinen Dual-Parser, keine Übergangsphase und keinen Migrationspfad.
- Direkte semantische Eingaben bleiben erhalten, sofern das jeweilige Tool sie heute unterstützt. Dazu gehören insbesondere qualifizierte Symbolnamen, `Datei:Zeile[:Spalte]` und rohe Roslyn-DocCommentIds. Der harte Schnitt betrifft nur das alte Handoff-Drahtformat.

### 3.2 Keine künstliche Informationsreduktion

Ein Handle ersetzt nur den technischen Identifikator. Es ersetzt nicht die für einen Agenten lesbare Bedeutung.

Beispiel:

```text
method  HandoffRegistry.Resolve(string handle)
        src/AiNetLinter.Mcp/Infrastructure/HandoffRegistry.cs:73
        h:Qm8…x.2f
```

Die genaue Darstellung folgt dem bestehenden Renderer. Entscheidend ist:

- Symbolart und verständlicher Name bzw. Signatur bleiben sichtbar.
- Pfad, Zeile und Spalte bleiben sichtbar, wenn sie heute fachlich relevant sind.
- Reihenfolge, Trefferzahl, Gruppierung, Paging und Detailtiefe bleiben gleich.
- Jeder eigenständig navigierbare Treffer behält ein eigenes übergebbares Handle.

Bei Listen darf dasselbe Root-Handle einmal im Kopf statt in jeder Zeile erscheinen, aber nur wenn alle Zeilen tatsächlich dasselbe Symbol meinen und die unveränderte Weitergabe eindeutig bleibt. Handles unterschiedlicher Symbole dürfen nicht zusammengelegt werden.

### 3.3 Kein `:Zeile`-Suffix am Symbolhandle

Symbolhandles adressieren Symbole, nicht Textzeilen. Daher wird kein `:Zeile` an `h:…` angehängt:

- Eine Zeile kann mehrere Symbole enthalten.
- Positionen ändern sich häufiger als Symbolidentitäten.
- Ein Suffix verlängert jede Wiederholung und vermischt Adressierung mit Anzeige.

Bestehende Positionsverträge bleiben separat als `relativer/Pfad.cs:Zeile[:Spalte]` erhalten. Sollte künftig ein eigener Positions-Handoff nötig werden, benötigt er einen eigenen Typ und Vertrag; er wird nicht in dieses Symbolhandle eingebaut.

### 3.4 Unveränderte Weitergabe

Ein von Tool A ausgegebenes Handle muss ohne Umschreiben an jedes fachlich passende Symbol-Eingabefeld von Tool B übergeben werden können. Der Client darf weder Target, Snapshot, DocCommentId noch Symbolart rekonstruieren müssen.

## 4. Architektur

### 4.1 Eine zentrale Source of Truth

Eine hostweit genau einmal erzeugte `HandoffRegistry` ist die einzige Stelle für:

- `GetOrAdd(SymbolIdentity) -> Handle`
- `Resolve(Handle, RequestContext) -> SymbolIdentity`
- Formatprüfung, Sitzungsprüfung und Fehlerklassifikation
- Deduplizierung identischer Symbolidentitäten
- aggregierte Betriebsmetriken

Producer und Consumer dürfen keine eigenen Handle-Parser, Zähler, Dictionaries oder Auflösungslogik besitzen.

Die Registry lebt:

- im Daemon genau für dessen Laufzeit, gemeinsam für alle parallelen MCP-Verbindungen;
- im direkten stdio-Betrieb genau für die Laufzeit dieses MCP-Hostprozesses.

Eine Registry pro Verbindung oder Request ist unzulässig, weil Toolketten sonst nicht zuverlässig funktionieren.

### 4.2 Handleformat

Vorgabe:

```text
h:<sessionNonce>.<counter>
```

- `sessionNonce`: beim Hoststart einmalig mit CSPRNG erzeugte 128 Bit, Base64url ohne Padding.
- `counter`: hostweit atomar steigender `UInt64`, Base36 ohne führende Nullen.
- Erlaubte Zeichen und Trennzeichen werden exakt geprüft; Groß-/Kleinschreibung ist signifikant.
- Nach Zählerüberlauf werden keine neuen Handles ausgegeben; der Host liefert `HANDOFF_CAPACITY_EXCEEDED`.
- Ein existierendes Handle bleibt davon unberührt.

Der Session-Nonce ist Bestandteil jedes Handles. Dadurch kann die Registry ein Handle einer fremden oder früheren Sitzung vor einem Registry-Lookup erkennen. Ein `h:\...`-Wert bleibt als Windows-Pfad interpretierbar; nur das vollständig gültige Handleformat aktiviert die Handle-Auflösung.

Die offene Grundsatzfrage zur theoretischen Neustart-Kollision steht im Frontmatter.

### 4.3 Gespeicherte Identität

Die Registry speichert keine langlebigen Roslyn-`ISymbol`-, `Compilation`-, `Solution`- oder Snapshot-Objekte. Ein Eintrag enthält mindestens:

- Ursprung: Source oder Assembly;
- ursprüngliches Invocation-Target;
- tatsächliches Owner-Target bei Assembly-Referenzen;
- Target-Identität/Fingerprint;
- Snapshot-Identität/Fingerprint;
- Projekt- bzw. Assembly-Identität;
- Roslyn-DocCommentId;
- Symbolart;
- nötigen Disambiguator für nicht eindeutig per DocCommentId adressierbare Symbole;
- nur die zur robusten Wiederauflösung notwendigen Zusatzdaten.

Lesbare Namen, Signaturen, Pfade und Positionen gehören weiterhin in die Toolausgabe. Sie müssen nicht allein deshalb redundant in jedem Registry-Eintrag liegen.

### 4.4 Idempotente Registrierung

Innerhalb eines Hostlaufs liefert dieselbe normalisierte `SymbolIdentity` immer dasselbe Handle. Registrierung und Deduplizierung sind threadsicher und atomar.

- Parallele Requests dürfen für dieselbe Identität keine zwei dauerhaften Handles erzeugen.
- Verschiedene Targets, Snapshots, Assembly-Versionen oder Symbolvarianten dürfen nicht versehentlich dedupliziert werden.
- Nur tatsächlich gerenderte Handoffs werden registriert. Vor Paging oder Abschneiden verworfene Treffer erzeugen keine Einträge.

### 4.5 Auflösung und Snapshot-Wechsel

Die Auflösung geschieht requestweit gegen genau einen atomar gebundenen Analysezustand:

1. Handle syntaktisch und auf Session prüfen.
2. Eintrag in der zentralen Registry laden.
3. Invocation-Target gegen den Request prüfen.
4. gespeicherten Snapshot/Assembly-Fingerprint gegen den gebundenen Zustand prüfen.
5. Symbol anhand der gespeicherten Identität neu auflösen.
6. Symbolart und Disambiguator validieren.

Ein Snapshot-Wechsel während eines Requests darf nicht zu einer gemischten Auflösung führen. Kann die gespeicherte Identität im aktuellen Zustand nicht mehr eindeutig aufgelöst werden, schlägt der Aufruf klar fehl; das Handle wird niemals auf ein nur ähnlich benanntes Symbol umgebogen.

Assembly-Referenzhandles, die aus einer Abfrage gegen das Root-Target stammen, bleiben mit diesem ursprünglichen Root-Target verwendbar. Die Registry verwaltet das tatsächliche Owner-Assembly intern. Der Agent muss den `targetPath` nicht aus der Ausgabe erraten oder wechseln.

### 4.6 Lebensdauer und Speicher

- Jedes einmal ausgegebene Handle bleibt bis zum Host-Shutdown gültig.
- Es gibt keine TTL-, LRU- oder andere Eviction.
- Nach einem Neustart ist die Registry leer.
- Speichergrenzen dürfen nicht durch stilles Löschen alter Einträge umgesetzt werden.
- Erreicht der Host eine konfigurierte harte Sicherheitsgrenze, dürfen nur neue Registrierungen mit `HANDOFF_CAPACITY_EXCEEDED` fehlschlagen.
- Registry-Einträge dürfen keine Roslyn-Workspaces, Snapshots oder Compilations festhalten.

## 5. Vollständige Toolpfade

Die Umsetzung beginnt mit einer codegestützten Inventur. Maßgeblich sind alle öffentlichen Felder, die heute `s:`-/`a:`-IDs ausgeben oder konsumieren, nicht nur diese Namensliste.

### 5.1 Zu prüfende Producer

- [ ] `find_symbol`
- [ ] `get_file_skeleton`
- [ ] `get_symbol_body`
- [ ] `find_references`
- [ ] `get_impact`
- [ ] `get_type_hierarchy`
- [ ] `find_implementations`
- [ ] `get_class_structure`
- [ ] `metrics_lookup`
- [ ] `get_call_tree`, soweit die Ausgabe heute Handoff-IDs enthält
- [ ] `dependency_graph`, soweit die Ausgabe heute Handoff-IDs enthält
- [ ] `get_feature_context`, soweit die Ausgabe heute Handoff-IDs enthält
- [ ] `get_test_context`, soweit die Ausgabe heute Handoff-IDs enthält
- [ ] `inspect_assembly`
- [ ] `search_assembly`
- [ ] `find_assembly_extensions`
- [ ] `get_assembly_context`
- [ ] alle weiteren per Code-/Textsuche gefundenen Renderer, DTOs und Formatter

Für jeden Producer ist zu prüfen:

- [ ] Jeder heutige Handoff bleibt vorhanden und wird ausschließlich durch `h:…` ersetzt.
- [ ] Lesbare Semantik, Pfad und Position bleiben erhalten.
- [ ] Paging, Begrenzung und Reihenfolge bleiben unverändert.
- [ ] Wiederholte Identitäten verwenden dasselbe Handle.

### 5.2 Zu prüfende Consumer

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
- [ ] alle weiteren per Code-/Textsuche gefundenen Symbol-Eingabefelder

Für jeden Consumer ist explizit festzulegen und zu testen:

- [ ] passende `h:…`-Handles werden akzeptiert;
- [ ] unpassende Symbolarten liefern `HANDOFF_KIND_MISMATCH`;
- [ ] direkte semantische Eingaben funktionieren weiterhin, falls sie heute unterstützt werden;
- [ ] alte `s:`-/`a:`-IDs werden als altes Handoffformat abgelehnt;
- [ ] ein Handle aus jedem fachlich passenden Producer funktioniert unverändert.

Die heute beobachteten Vertragslücken bei `find_duplicates.helperSymbol` und `resolve_type_origin.typeName` müssen vor Freigabe geschlossen sein.

### 5.3 Bewusst nicht als Symbolhandles zu behandeln

Ohne bereits vorhandenen Symbol-Handoff-Vertrag bleiben unverändert:

- `continuationToken` und andere Paging-Tokens;
- Verify-Referenzen im Format `Pfad:Zeile[:Spalte]`;
- Diagnostics-/Violation-Referenzen;
- Dateibaum-, Namespace-, Metrikbaum-, Hotspot-, Index-, Scope- und Suchparameter;
- Graph-Knoten ohne heutigen öffentlichen Symbol-Handoff;
- reine Feature-, Health- oder Pattern-Daten.

Es werden dort keine neuen Handles eingeführt. Falls ein solcher Pfad heute doch `s:`/`a:` ausgibt oder annimmt, fällt er durch die Inventur wieder in Producer bzw. Consumer.

## 6. Fehlervertrag

Alle Fehler sind stabile maschinenlesbare Codes mit kurzer verständlicher Meldung. Priorität bei mehreren möglichen Fehlern: Syntax → altes Format → Session → Registry → Target → Snapshot → Symbolart/Auflösung.

| Code | Bedeutung |
|---|---|
| `INVALID_HANDOFF` | `h:…` ist syntaktisch ungültig. |
| `UNSUPPORTED_HANDOFF_FORMAT` | Eine alte `s:`-/`a:`-ID wurde als Handoff übergeben. |
| `HANDOFF_SESSION_MISMATCH` | Das Handle stammt sicher aus einem anderen Hostlauf. |
| `HANDOFF_UNKNOWN` | Format und Session passen, aber der Eintrag existiert nicht. |
| `TARGET_MISMATCH` | Das Request-Target passt nicht zum ursprünglichen Invocation-Target. |
| `STALE_SNAPSHOT` | Target passt, der gespeicherte Source-/Assembly-Zustand ist aber nicht mehr gültig. |
| `HANDOFF_KIND_MISMATCH` | Das Symbol ist für das Eingabefeld fachlich ungeeignet. |
| `HANDOFF_CAPACITY_EXCEEDED` | Es können keine neuen Einträge sicher registriert werden. |

Fehlertexte dürfen die gespeicherte lange Identität nicht vollständig ausgeben und so den Tokengewinn wieder aufheben. Sie sollen erwartete Symbolart, sichtbaren Symbolnamen soweit vorhanden und eine konkrete nächste Aktion nennen.

## 7. Betriebsbeobachtung

`health` oder ein gleichwertiger aggregierter Diagnosepfad weist mindestens aus:

- Registry-Einträge gesamt, Source und Assembly;
- geschätzte Registry-Bytes;
- GetOrAdd-Treffer und neue Registrierungen;
- Resolve-Erfolge und Fehleranzahl je Fehlercode;
- aktuelles Sicherheitslimit, falls konfiguriert.

Keine Liste aller Handles und keine langen gespeicherten Identitäten ausgeben.

## 8. Muss-Akzeptanzkriterien

### 8.1 Informations- und Funktionsgleichheit

- [ ] Für jede Baseline-Antwort sind Treffer, Reihenfolge, Gruppierung, Signaturen, Pfade, Positionen, Paging und Detailinhalt fachlich gleich.
- [ ] Nur die technische Handoff-Darstellung und nachweislich redundante Wiederholungen derselben Identität ändern sich.
- [ ] Kein heute möglicher Source-, Assembly-, Diagnostics-, Verify-, Graph-, Feature- oder Paging-Workflow geht verloren.
- [ ] Content-only und Zero-Transformation bleiben erfüllt.
- [ ] Kein Consumer zwingt den Agenten, Handlebestandteile zu lesen oder umzuschreiben.

### 8.2 Registry und Lebenszyklus

- [ ] Daemon-Verbindungen desselben Hostlaufs können Handles gegenseitig verwenden.
- [ ] Direkter stdio-Betrieb unterstützt Toolketten über mehrere Requests.
- [ ] Paralleles `GetOrAdd` derselben Identität liefert genau ein Handle.
- [ ] Derselbe Symbolwert über verschiedene Producer liefert dasselbe Handle.
- [ ] Unterschiedliche Symbolidentitäten liefern niemals dasselbe Handle.
- [ ] Alle ausgegebenen Handles bleiben bis Shutdown auflösbar.
- [ ] Ein Handle aus dem vorigen Hostlauf liefert `HANDOFF_SESSION_MISMATCH` und niemals ein neues Symbol.
- [ ] Snapshot-Wechsel liefern entweder exakt dasselbe Symbol oder `STALE_SNAPSHOT`.
- [ ] Target-Mismatch wird nicht durch Namenssuche oder Fallback-Auflösung kaschiert.
- [ ] Eine lange Session hält keine Roslyn-Snapshots oder Compilations über Registry-Einträge am Leben.

### 8.3 Parser und harter Schnitt

- [ ] Gültige Handles werden exakt akzeptiert.
- [ ] abgeschnittene, erweiterte, falsch getrennte und case-veränderte Handles werden abgelehnt.
- [ ] `s:` und `a:` werden in Handoff-Kontexten mit `UNSUPPORTED_HANDOFF_FORMAT` abgelehnt.
- [ ] Windows-Pfade wie `h:\...` werden nicht als Handles fehlklassifiziert.
- [ ] Direkte Namen, Positionen und rohe DocCommentIds bleiben in ihren bisherigen Feldern gültig.

### 8.4 Reale Toolketten

- [ ] `find_symbol -> get_symbol_body`
- [ ] `find_symbol -> find_references`
- [ ] `find_symbol -> get_type_hierarchy`
- [ ] `find_symbol -> metrics_lookup`
- [ ] `get_file_skeleton -> get_symbol_body`
- [ ] `find_references -> get_symbol_body` für einen Symboltreffer
- [ ] Source-Producer -> `find_duplicates.helperSymbol`
- [ ] Type-Producer -> `resolve_type_origin.typeName`
- [ ] `inspect_assembly -> assembly consumer`
- [ ] Assembly-Referenz aus Root-Abfrage -> Consumer mit unverändertem Root-`targetPath`
- [ ] passende Cross-Producer/Cross-Consumer-Kombinationen aus der vollständigen Vertragsmatrix

### 8.5 Token-, Speicher- und Laufzeitmessung

Auf dem festgeschriebenen Baseline-Datensatz:

- [ ] Keine der vier Referenzantworten wächst in UTF-8-Bytes oder Tokens.
- [ ] Die Summe der UTF-8-Bytes der vier Antworten sinkt um mindestens 25 %.
- [ ] Die Summe der Zeichen in Handoff-IDs sinkt um mindestens 70 %.
- [ ] Die Gesamttokenzahl sinkt mit einem dokumentierten, Codex-nahen Tokenizer; Tokenizer und Version stehen im Messergebnis.
- [ ] 10.000 eindeutige Einträge werden hinsichtlich Registry-Bytes, Registrierungszeit und Resolve-Latenz gemessen und dokumentiert.
- [ ] Wiederholte Abfragen desselben Symbols erhöhen die Eintragszahl nach der ersten Registrierung nicht.
- [ ] Parallel- und Langzeittest zeigen keine Handle-Kollision, keine falsche Auflösung und keine Snapshot-Retention.

## 9. Umsetzungsreihenfolge

- [ ] Öffentliche Producer-/Consumer-Matrix aus Codeinventur vervollständigen.
- [ ] gemeinsame `SymbolIdentity`, `HandoffRegistry` und Fehlercodes implementieren.
- [ ] Registry im Daemon und direkten stdio-Host korrekt verankern.
- [ ] alle Producer auf `GetOrAdd` umstellen.
- [ ] alle Consumer auf die zentrale Auflösung umstellen.
- [ ] alte `s:`-/`a:`-Parser und -Renderer vollständig entfernen.
- [ ] Vertrags-, Parallelitäts-, Neustart-, Snapshot-, Target- und Langzeittests ergänzen.
- [ ] reale Toolketten und Baseline-Messungen aus Abschnitt 8 ausführen.
- [ ] öffentliche MCP-Dokumentation und Beispiele aktualisieren.
- [ ] vollständige Nicht-Stress-Testgates und `dotnet build` erfolgreich ausführen.
- [ ] per Code-/Textsuche verifizieren, dass kein öffentlicher `s:`-/`a:`-Producer oder Handoff-Parser verblieben ist.

## 10. Freigabegate

Die Umsetzung ist erst freigabefähig, wenn:

- [ ] die offene Frage im Frontmatter entschieden und dort entfernt oder als Entscheidung dokumentiert ist;
- [ ] alle Muss-Akzeptanzkriterien erfüllt sind;
- [ ] die vollständige Producer-/Consumer-Matrix im Konzept oder Implementierungsnachweis enthalten ist;
- [ ] die Messung tatsächliche Tokenersparnis ohne fachlichen Informationsverlust belegt;
- [ ] keine alten Handoff-IDs mehr ausgegeben oder als Handoff akzeptiert werden.
