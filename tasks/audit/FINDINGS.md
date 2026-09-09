# MCP-UX-Audit – Befunde

| ID | Schweregrad | Status | Betroffene Tools | Kurzbegründung |
| --- | --- | --- | --- | --- |
| A-001 | Major | fixed | `get_server_health` | Angeforderte leere Diagnose-Samples sind nicht strukturiert erkennbar. |
| A-002 | Minor | open | `get_server_health` | Nichtpositive Diagnose-Limits werden stillschweigend auf einen Default geändert. |
| A-003 | Minor | open | `get_server_health` | Partieller Assembly-Zustand weist widersprüchliche nächste Aktionen aus. |
| B-001 | Major | fixed | `get_namespace_tree` | Strukturierte Einträge ignorieren das angeforderte Ergebnislimit. |
| B-002 | Major | fixed | `inspect_assembly`, `find_assembly_extensions` | Vollständigkeit der Trefferliste und Analyse-Diagnosen wird widersprüchlich vermischt. |
| C-001 | Critical | fixed | `find_symbol`, `find_references`, `get_impact` | Assembly-Symbolketten können fremde Referenzen und Impacts liefern. |
| C-002 | Major | fixed | `find_references`, `get_impact` | Navigation signalisiert vollständige Ergebnisse trotz Ergebnisbegrenzung. |
| C-003 | Major | fixed | `find_symbol` | Referenzsuche kann bei Standardbudget unbrauchbare, inkonsistente Antworten liefern. |
| C-004 | Minor | open | `find_symbol` | Leere Einträge eines Musterbatches werden nicht sichtbar validiert. |
| D-001 | Major | fixed | `find_symbol`, `get_file_tree`, `get_server_health` | Schema-Typfehler werden generisch statt handlungsweisend zurückgegeben. |
| D-002 | Major | fixed | `get_file_tree` | Ein Ressourcenfehler wird in der Navigation als erfolgreicher vollständiger Vorgang markiert. |
| D-003 | Major | fixed | `find_symbol` | Konkurrenz zwischen Suchparametern wird ohne Hinweis stillschweigend aufgelöst. |
| E-001 | Major | open | `search_pattern` | Zwei Next-Hinweise widersprechen sich bei leeren Ergebnissen. |
| E-002 | Minor | open | `find_symbol`, `get_file_tree` | Der Grenzwert null für Ergebnislimits wird uneinheitlich behandelt. |
| E-003 | Minor | open | mehrere zielgebundene Tools | Vollständigkeitswerte vermischen Ergebnis- und Verfügbarkeitszustände. |
