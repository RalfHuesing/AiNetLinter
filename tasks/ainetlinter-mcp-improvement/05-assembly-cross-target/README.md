# 05 – Assembly & Cross-Target

## Ziel

Externe DLL-/EXE-Analyse soll als statischer Snapshot nützlich und ehrlich sein, ohne Projektsemantik, Laufzeitwissen oder Consumer-Kontext vorzutäuschen.

## Scope

- konsistente Assembly-Identität, Herkunft, Generation und stale-Fehler;
- Katalog → Typ → Member/Body/Graph als funktionierende Kette;
- Diagnose-/BCL-/Referenzdaten budgetieren und als partiell markieren;
- `not_decidable` bei Extension-Anwendbarkeit sichtbar halten;
- project-only-Tools mit echtem Capability-Fehler statt irreführender DLL-Validierung.

## Ausgangsbefunde

`inspect_assembly`, `search_assembly`, `get_assembly_context`, `find_assembly_extensions`, `resolve_type_origin`, `combo-assembly`, `combo-cross-target`.

## Voraussichtliche Quellbereiche

`Mcp/Assemblies/Analysis/*`, `Tools/AssemblyAnalysis/*`, `Tools/SymbolGraph/Assembly*`, `TypeResolution/*`, `AnalysisSymbolIdentity` und Assembly-Response-Builder.

## Abhängigkeiten

Pakete 01–03 liefern die gemeinsamen Verträge. Assembly-Parität ist Bestandteil dieses Gesamt-Tasks; die Slice-Reihenfolge verhindert nur, dass dieselben Verträge zweimal unterschiedlich implementiert werden.

## Abnahme

- ein Assembly-Typ lässt sich aus dem Katalog mit einer kopierbaren ID weiterverfolgen;
- alte/aktuelle Generationen sind eindeutig und nicht mit Target-Mismatch verwechselt;
- `partial`, Diagnostics und Body-/Member-Limits sind sichtbar und wahr;
- keine Antwort behauptet Laufzeit- oder Consumer-Anwendbarkeit, die statisch nicht entscheidbar ist.
