# 01 – Discovery Contract

## Ziel

Ein Agent soll aus einem unbekannten Projekt zuverlässig zur richtigen Analysefamilie und zum ersten brauchbaren Folge-Call gelangen.

## Scope

- echter Verzeichnisbaum ohne Präfix-/Parent-Fehler;
- konsistente Pfad- und Glob-Semantik zwischen `get_file_tree` und `search_pattern`;
- `get_index_scope` mit sichtbarem Routing-Hint für C# versus Nicht-C#;
- Tool-Katalog, Einzelschema, Required-Felder, Enums und Capability-Matrix aus einem Vertrag;
- sichere Defaults gegen Root-Token-Flut, Logs und Release-Duplikate.

## Ausgangsbefunde

`get_file_tree`, `tool-discovery`, `get_index_scope`, `search_pattern`, `combo-discovery-fallback`, `resource-overview`.

## Voraussichtliche Quellbereiche

`McpServerInstructions`, `Registration/*`, `Tools/FileStructure/*`, `FileTreePathResolver`, `PathGlobMatcher`, `SearchPatternScanner` und die MCP-Discovery-/Framing-Tests.

## Abhängigkeiten

Keine fachliche Abhängigkeit. Dieses Paket liefert jedoch Pfad- und Capability-Verträge, auf denen spätere Pakete aufbauen.

## Abnahme

- Root-Tree zeigt korrekte Eltern;
- ein voller Pfad aus Discovery ist direkt als Search-Scope verwendbar;
- `.cs` routet zu Symboltools, `.js`/`.razor` zu Textsuche;
- ein Agent kann Pflichtfelder und Target-Capability aus dem Einzelschema ableiten;
- Root-Aufrufe erzeugen keine unbeabsichtigte Token-Flut.
