# 09 – Cursor-Timeout: offener Assembly-Pfad und Live-Nachweis

**Status:** offen. **Priorität:** hoch für kalte Assembly-Ziele; die angebundenen Source- und vier dedizierten Assembly-Tools sind implementiert und lokal geprüft.

## Befund

Cursor 3.21.16 meldete bei stillen AiNetLinter-`tools/call`-Aufrufen nach etwa 120 Sekunden `MCP tool call timed out (idle)` beziehungsweise `-32001`. Der Client hat laut vorliegendem Log `idleTimeoutMs=120000` und `maxTotalTimeoutMs=3600000`; ein konfigurierbares Timeout wurde nicht gefunden. Ob Cursor einen `_meta.progressToken` mitsendet und ob Progress-Meldungen seine Idle-Uhr zurücksetzen, ist am echten Aufruf nicht belegt.

Seit `2f01f805` und `24f18489` liefern `verify`, der erste `get_verify_advisories`-Scan, `find_duplicates`, `pattern_detect` sowie `search_assembly`, `inspect_assembly`, `find_assembly_extensions` und `get_assembly_context` bei längeren Läufen nach höchstens 15 Sekunden `operation=running` und `operationToken`. Derselbe Aufruf mit identischen Argumenten und Token fragt den laufenden Auftrag ab und erhält schließlich die unveränderte fachliche Antwort. Das ist ein eigener abrufbarer Auftrag, keine MCP-Progress- oder Tasks-Protokollnutzung.

**Restlücke:** Generische Tools mit einem kalten `.dll`-/`.exe`-Ziel, etwa `find_symbol` oder `get_symbol_body`, laufen über den allgemeinen Assembly-Dispatcher. Dessen Dekompilierung kann standardmäßig bis zu 180 Sekunden dauern. Diese Routen besitzen noch keinen kurzen `operationToken`-Fortsetzungsweg und können daher weiterhin Cursors 120-Sekunden-Idle-Grenze treffen. Ein isolierter Fix für die vier dedizierten Assembly-Tools deckt diese Routen nicht ab.

## Abnahme nach dem Release

1. Den zentralen Assembly-Session-/Dispatcher-Einstieg einschließlich Lease-, Cancellation- und Continuation-Lifecycle für generische Tools untersuchen und kohärent an den kurzen Aufrufvertrag anbinden. Die eigentliche Query darf nach dem kalten Laden ebenfalls nicht still länger als 120 Sekunden blockieren.
2. Mit einem **neu gestarteten** AiNetLinter-Server in Cursor einen vormals betroffenen Source-Aufruf und einen kalten generischen Assembly-Aufruf über 120 Sekunden prüfen. Erwartung: vor 120 Sekunden ein Fortsetzungsstatus oder eine Endantwort; Wiederholungen mit demselben Token führen ohne `-32001` zum vollständigen Ergebnis.
3. Zusätzlich prüfen, dass ein unabhängiges kurzes Tool während des Scans antwortet und dass ein verlassener Auftrag zeitgesteuert endet.

Die aktuell verbundene AiNetLinter-MCP-Instanz ist ein alter Stand. Ihr `verify`-Gate bestätigt Source-Qualität, aber nicht den neuen Laufzeitvertrag. Die lokalen C#-Vertragstests und Gates sind grün; ein Cursor-Live-Test steht aus.
