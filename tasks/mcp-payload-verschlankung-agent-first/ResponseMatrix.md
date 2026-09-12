# Responsematrix – Dual-Output-Ausgangswert und Content-only-Endwert

Erfasst am 2026-09-12 mit einem frischen Raw-MCP-Host gegen
`SymbolGraphMiniFixtureWorkspace`. Die Größen sind UTF-8-Bytes der sichtbaren
Textnutzlast beziehungsweise des serialisierten `structuredContent`-Werts;
`combined` ist ihre heutige fachliche Dual-Payload-Summe ohne JSON-RPC-Framing.

| Route / Zustand | Text | structuredContent | combined | Für Agenten notwendige Information |
| --- | ---: | ---: | ---: | --- |
| `find_symbol` / Erfolg | 95 | 1.248 | 1.343 | Treffereinheit samt kanonischer Handoff-ID und Vollständigkeit |
| `find_symbol` / Empty | 202 | 1.036 | 1.238 | explizite Leermenge, Suchscope und Zahl der Treffer |
| `get_file_tree` / Truncated | 525 | 1.807 | 2.332 | vollständige angezeigte Dateieinheiten, Trunkierungsgrund und direkt ausführbarer nächster Schritt |
| `find_symbol` / InvalidArgument | 284 | 833 | 1.117 | stabiler Fehlercode, Feldpfad, Ursache und Korrekturhinweis |

Alle vier Raw-Responses hatten jeweils genau einen nichtleeren Textblock,
enthielten jedoch zusätzlich `structuredContent`. Damit ist die aktuelle
Nutzlast nicht Content-only; insbesondere liegen Handoff-, Vollständigkeits-
und Fehlerdaten noch im Nebenkanal. Der spätere Endvergleich verwendet dieselben
Routen und prüft, dass ihr vollständiger Content-only-Nachfolger kleiner als
die jeweilige `combined`-Ausgangsgröße ist, ohne die hier aufgeführten
Agenteninformationen zu verlieren.

Der Content-only-Endvergleich läuft gegen dieselbe Fixture und dieselben
Raw-Wire-Requests in `McpContentOnlyRawWireContractTests`. Er misst die
sichtbare UTF-8-Textgröße und schützt pro Route, dass sie strikt kleiner als
der frühere `combined`-Wert bleibt. Der Test prüft außerdem genau einen
nichtleeren Textblock sowie die Abwesenheit aller Ergebnisfelder außer
`content` und optional `isError`.

| Route / Zustand | früher combined | sichtbarer Content-only-Endwert | Informationsgehalt |
| --- | ---: | ---: | --- |
| `find_symbol` / Erfolg | 1.343 | `< 1.343` | Fundstelle und direkt kopierbare Handoff-ID |
| `find_symbol` / Empty | 1.238 | `< 1.238` | explizite Leermenge und Suchkontext |
| `get_file_tree` / Truncated | 2.332 | `< 2.332` | vollständige sichtbare Einheiten, Trunkierungsgrund und ausführbarer nächster Schritt |
| `find_symbol` / InvalidArgument | 1.117 | `< 1.117` | stabiler Fehlercode, Feldpfad, Ursache und Korrektur |

Die Matrix verwendet bewusst belastbare Obergrenzen statt reproduktionsanfälliger
Absolutwerte: dynamische Handoff-IDs und Snapshotdaten beeinflussen die
Bytezahl, nicht aber die garantierte strikte Unterschreitung oder den
Informationsvertrag.
