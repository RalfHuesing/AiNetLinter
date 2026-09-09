# MCP-UX-Audit – Behobene Befunde

| ID | Zusammenfassung | Testreferenz |
| --- | --- | --- |
| C-001 | Die Assembly-Symbolauflösung bleibt über identische Symbol-IDs hinweg an die deklarierende Assembly gebunden; Assembly-Impact nutzt denselben Navigationspfad. | `AssemblyRoute_FindSymbolHandoffIdStaysBoundToItsReferencedAssembly` |
| A-001 | Bei explizit angeforderten Diagnostics bleibt die strukturierte Assembly-/Diagnose-Ausgabe auch ohne Sessions sichtbar. | `Build_IncludeDiagnosticsWithoutSessions_EmitsExplicitEmptyDiagnosticsArray`, `Build_DefaultHealthIsCompact_AndDetailDiagnosticsStayBounded` |
| B-001 | Strukturierte Namespace-Ergebnisse folgen dem angeforderten Limit und stimmen mit Zählern und Trunkierung überein. | `ScanProjectNamespacesAsync_TruncatesStructuredNamespacesToMaxResults` |
