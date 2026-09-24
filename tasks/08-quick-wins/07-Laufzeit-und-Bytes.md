# 07 – Laufzeit, Antwortgröße und redundante Ausgabe

**Status:** Messwerte aus Stichproben; keine Benchmarkreihe. **Schweregrad:** mittel für SAN-Verify, niedrig für Textredundanz.

## Messungen

- SAN `verify(scope:"solution")`: 138,9 s bei 4.069 UTF-8-Bytes JSON-serialisiertem MCP-Resultat. Ein früherer paralleler Block dauerte rund 141 s, seine Resultate gingen durch einen Messskriptfehler verloren. Nach dem Indexaufbau lagen begrenzte semantische Abfragen im SAN-Bericht meist unter wenigen Sekunden. [SAN-Protokoll](SAN.md)
- `get_index_scope(AiNetLinter)`: einmal 81,57 s für 1.056 Bytes JSON-Resultat. Ursache/typische Laufzeit sind aus einem Messpunkt nicht ableitbar. [Token-Protokoll](TokenEffizienz.md)
- Begrenzte `get_namespace_tree`-Antworten: 336–1.459 Bytes JSON-Resultat über vier Solutions. `get_feature_context` in SAN: 2.349 Bytes mit 2/5 Call-Sites und `truncated`, 2.692 Bytes mit 5/5 und `complete`. Die zusätzliche Vollständigkeit kostete in dieser Stichprobe 343 Bytes sichtbaren Text.
- Die gemessenen Größen sind UTF-8-Bytes des Tooltexts bzw. des JSON-serialisierten Toolresultats, nicht Transport-Frames. Tokenzahlen sind nur grobe Näherungen.

## Konkrete Ergonomie

Die Antworten transportieren Handoffs, Zähler und Vollständigkeitsmarker meist kompakt. Kleine Redundanzen: wiederholte Status-/Count-Zeilen in `get_feature_context` und mehrere Warn-/Folgeschritt-Sätze bei begrenztem `get_file_tree`. Diese Einsparungen sind gegenüber Handoff- und Review-Zugriff deutlich nachrangig.

## Grundlage für ein mögliches Umsetzungskonzept

Für SAN-Verify erst Laufzeitanteile des Scans messen (Solution-/Index-Laden, Dead-Code-Analyse, Formatierung), dann über Optimierung entscheiden. Für `get_index_scope` nur bei reproduzierter Langsamkeit ein Performance-Issue eröffnen. Textkürzungen nur übernehmen, wenn Status, Vollständigkeit und Folgeschritt erhalten bleiben.
