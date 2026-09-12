# Responsematrix – Ausgangszustand des Dual-Output-Vertrags

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
