# AiNetLinter MCP-Instruktionen

Zielgebundene Tool-Aufrufe verwenden `targetType` und den absoluten `targetPath`. `project` verweist auf eine Source-Solution; `assembly` auf eine vorhandene verwaltete `.dll`- oder `.exe`-Datei. Die Assembly wird nicht geladen oder ausgeführt.

## Assembly-Capability-Matrix

Die Matrix umfasst die 13 Tools, die sowohl den Projekt- als auch den Assembly-Zielvertrag anbieten:

| Tool | Assembly-Vertrag |
| --- | --- |
| `dependency_graph` | Datei-/Typabhängigkeiten im Assembly-Snapshot |
| `find_references` | Aufrufstellen; `includeReferences` standardmäßig `false` |
| `find_symbol` | Symbolsuche; `includeReferences` standardmäßig `false` |
| `get_call_tree` | Aufrufer-/Aufgerufenenbaum; `includeReferences` standardmäßig `false` |
| `get_class_structure` | Struktur eines Typs |
| `get_file_skeleton` | Signaturen; `filePath` ist String-Alias für `filePaths` |
| `get_impact` | nur `symbolIdentifier`, kein Git-Diff/`gitRef` |
| `get_namespace_tree` | Namespace-/Typübersicht mit Assembly-Header |
| `get_symbol_body` | dekompilierter Body, soweit verfügbar |
| `get_type_hierarchy` | Basen, Interfaces und Untertypen |
| `metrics_lookup` | Metriken des Assembly-Snapshots |
| `metrics_tree` | Standardmodus `code_size` |
| `get_server_health` | zielgebundener Status der Assembly-Session |

`inspect_assembly` und `find_assembly_extensions` sind zusätzliche Assembly-only-Tools und deshalb nicht Teil dieser 13-zeiligen Cross-Target-Matrix. `inspect_assembly.memberNames` wählt Member case-insensitive exakt (OR); `memberName` bleibt eine Teiltextsuche. Maßgeblich für Parameter und aktuelle Grenzen ist immer `tools/list`.

Die zentrale Discovery-Antwort in `ServerInstructions.cs` beschreibt denselben Stand kompakt.
