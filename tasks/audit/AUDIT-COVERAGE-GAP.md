# Warum das erste Audit diese Befunde nicht geschlossen hat

## Kurzfassung

Der erste Audit fand und behob reale Probleme, war aber kein vollstaendiger Vertragstest jeder oeffentlichen Route. Das Live-360-Nachaudit mit einem zweiten, strukturell anderen Source-Projekt zeigte deshalb weitere, allgemeingueltige Luecken.

Der laufende Server entsprach dem damaligen Source-Stand. Die neuen Befunde sind folglich **keine** Folge einer veralteten Installation.

## Konkrete Ursachen

1. **Toolgruppen statt Routenmatrix.** Der erste Audit pruefte reprasentative Calls, aber nicht jede Kombination aus globalem, Source-, Assembly- und Daemon-Routing. Deshalb blieb die Source-spezifische Abweichung von `get_server_health(maxDiagnostics <= 0)` sichtbar, obwohl andere Routen die Validierung zeigten.
2. **Zu wenig Fremdprojekt-Variabilitaet.** Das zweite Target hat andere Namespace-Tiefen, Dateimengen, Metrikbaeume und keine Regelkonfiguration. Dadurch wurden Probleme bei Namespace-Praefixen, `topN`, Response-Budgets und Composite-Status erst sichtbar.
3. **Zu viele Handler- statt Transporttests.** Mehrere Korrekturen wurden an Tool-Handlern oder einem spezifischen Dispatch abgesichert. Die oeffentliche MCP-Bindung, globale Delegates und Source-Dispatch durchlaufen nicht immer denselben Pfad.
4. **Keine gemeinsame Grenzwertmatrix.** `maxResults` wurde punktuell korrigiert, aber `0`, negativ, Cap-Ueberschreitung, Byte-Budget und Tiefe wurden nicht fuer alle Parameterklassen und Tools systematisch kontraktiert.
5. **Text und StructuredContent nicht durchgaengig verglichen.** Das Naczaudit zeigt: Manche Tools begrenzen nur Markdown, aber nicht die strukturierte Nutzlast.

## Konsequenz fuer einen Folgeauftrag

1. Routenmatrix je Toolfamilie: global, Source mit/ohne Regeln, Assembly und Daemon.
2. Parametermatrix: fehlend, `null`, leer, Typfehler, fehlerhaftes Array-Element, `0`, negativ, Cap und konkurrierende Parameter.
3. Response-Matrix: Text gegen `structuredContent`, `navigation`, `completeness`, `next`, Zaehlwerte und effektive Limits.
4. Mindestens zwei repo-eigene Source-Fixtures: konfiguriert und nicht konfiguriert, mit tiefen Namespaces und ausreichend grossen Listen.
5. Erst danach gezielte Red-Tests und lokale Fixes nach gemeinsamem Priorisierungsplan.
