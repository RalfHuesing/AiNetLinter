# 03 – Completeness & Paging

## Ziel

Ein Agent soll erkennen, ob eine Antwort leer, vollständig, begrenzt, nicht unterstützt oder fehlgeschlagen ist, und bei Begrenzung eine belastbare nächste Entscheidung treffen können. Das Paket baut keinen klassischen Seitenbrowser.

## Scope

- gemeinsamer Envelope mit `totalCount`, `returnedCount`, `completeness`, `truncatedBy` und optionalem `continuationToken`;
- keine „vollständig“-Aussage bei sichtbarer oder fachlicher Kappung;
- getrennte Zählung konkurrierender Richtungen und Abschnitte;
- sichtbare Budgets für Markdown und StructuredContent;
- tokenarme, fachlich gerankte Defaults plus gezielter Drilldown statt blindem `maxResults`-Hochsetzen;
- bei massenhaften oder nicht sinnvoll rankbaren Ergebnissen ein maschinenlesbarer Scope-/Filter-Hinweis;
- Cursor nur für einen sinnvollen weiteren Ausschnitt, nicht als Erwartung, dass ein Agent zehn Seiten abarbeitet.

## Ausgangsbefunde

`get_file_skeleton`, `get_class_structure`, `get_namespace_tree`, `get_test_context`, `get_violations`, `get_hotspots`, `metrics_tree`, `combo-mcp-first-context` sowie die Completeness-Anteile der übrigen Findings.

## Voraussichtliche Quellbereiche

`McpTruncation`, `McpSufficiencyHints`, `McpToolResults`, gemeinsame Response-Envelope, toolbezogene Formatter und `AssemblyAnalysisResponseLimits`.

## Abhängigkeiten

Die Entscheidungen zu Text/StructuredContent und bounded Exploration sind getroffen. Paket 02 muss die Identifier für Fortsetzungs- und Folgecalls festlegen.

## Abnahme

- jede begrenzte Antwort erklärt Ursache und nächste Aktion maschinen- und menschenlesbar;
- gleiche Datenbasis erzeugt Text und StructuredContent;
- ein Folgecall kann ohne Raten die nächste Seite oder das fehlende Detail anfordern;
- normale Leermengen werden nicht mit `unsupported` oder `partial` verwechselt.
