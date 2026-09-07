# 02 – Symbol IDs & Navigation

## Ziel

Ein Agent soll eine gefundene Definition, Memberstruktur oder Call-Site ohne manuelles Raten an das nächste passende Tool übergeben können.

## Scope

- gemeinsame kanonische Identifier für Projekt und Assembly;
- eindeutige Regeln für `T:`, `M:`, `P:`, `F:`, Datei-/Zeilen-Fallbacks und Partial-Typen;
- IDs in Tabellen, Call-Sites, Implementierungen und Graphkanten sichtbar machen;
- keine stillen Array-/Alias-Verluste;
- verständliche Target-/Stale-/Ambiguous-Fehler mit kopierbarem nächsten Schritt.

## Ausgangsbefunde

`find_symbol`, `get_file_skeleton`, `get_symbol_body`, `get_class_structure`, `find_references`, `get_call_tree`, `find_implementations`, `get_type_hierarchy`, `dependency_graph`, `combo-batch-ids`, `combo-type-nav`, `combo-cross-target`.

## Voraussichtliche Quellbereiche

`AnalysisSymbolIdentity`, `Tools/SymbolGraph/*`, `GetSymbolBodyTool`, `Tools/FileStructure/GetFileSkeletonTool`, `GetClassStructureTool`, `TypeHierarchy/*` und die Assembly-Navigation.

## Abhängigkeiten

Der kanonische Endzustand der Identifier und der gemeinsame Completeness-/Paging-Vertrag müssen vor der Umsetzung feststehen; eine Übergangskompatibilität ist nicht erforderlich.

## Abnahme

- `find_symbol` → Kontext/Body/Referenzen/Call-Tree funktioniert mit kopierter ID;
- Assembly-IDs bleiben über den vereinbarten Lebenszyklus gültig oder rebinden nachvollziehbar;
- Memberzeilen ohne ID werden nicht als agententauglicher Handoff behauptet;
- Partial-Klassen werden nicht doppelt als unabhängige Symbolidentität ausgegeben;
- falsches Target und stale ID sind unterscheidbar.
