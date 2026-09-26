# 08 – Verbleibende Befunde

Stand: 2026-09-26. Erledigte Punkte wurden aus diesem Task-Ordner entfernt; Umsetzung und Entscheidungen stehen in [Umsetzung-und-Feedback.md](Umsetzung-und-Feedback.md). Die fünf ursprünglichen Nutzungsberichte sind historische Messprotokolle und beschreiben nicht durchgehend den aktuellen Serverstand.

| Punkt | Entscheidung | Nächster Schritt |
|---|---|---|
| [Cursor-Timeout bei kalten Assembly-Zielen](09-Cursor-Timeout-Rest.md) | Offen, relevant für einen Teil der gerouteten Tools. | Nach dem Release den zentralen Assembly-Session-Einstieg so anpassen, dass auch generische Tools binnen der Idle-Grenze antworten; danach mit neuem Server in Cursor prüfen. |
| Verify-Zähler `evidence: returned=X/Y` | Kleine Verständlichkeitslücke bleibt: Der Zähler umfasst mehr als sichtbare Dead-Code-Kandidaten. `deadCode.candidates`, `shown`, `truncatedBy` und der paginierte Abruf sind inzwischen getrennt vorhanden. | Bei einer späteren MCP-Vertragsrunde die Legende oder Feldnamen präzisieren und Parser-/Bytebudget prüfen. Für den Release kein Blocker. |

## Ohne aktuelle Umsetzung

- Die 138,9 Sekunden für einen SAN-Verify und 81,57 Sekunden für einen einzelnen `get_index_scope`-Aufruf sind historische Stichproben ohne isolierte Ursache. Der neue abrufbare Auftrag begrenzt die Stille bei angebundenen langen Tools; er optimiert ihre Rechenzeit nicht. Eine Performance-Änderung braucht reproduzierte Messungen nach Laden und Cache-Aufbau.
- Wiederholte Status-/Count-Hinweise in `get_feature_context` und `get_file_tree` waren geringfügig. Für `search_pattern` ist kein konkreter doppelter Treffer im aktuellen Stand belegt. Ohne reproduzierbaren Befund werden keine Nutzdaten oder Vollständigkeitsmarker gekürzt.
- Semantische Razor-Referenzen bleiben außerhalb des C#-Symbolgraphen. `find_references` kennzeichnet bei null Call-Sites und vorhandenem Razor-Markup die Grenze und nennt eine konkrete `search_pattern`-Folgeabfrage. Eine semantische Razor-Integration wäre ein eigenes Vorhaben, kein Quick Win.
