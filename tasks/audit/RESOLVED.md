# MCP-UX-Audit – Behobene Befunde

| ID | Zusammenfassung | Testreferenz |
| --- | --- | --- |
| C-001 | Die Assembly-Symbolauflösung bleibt über identische Symbol-IDs hinweg an die deklarierende Assembly gebunden; Assembly-Impact nutzt denselben Navigationspfad. | `AssemblyRoute_FindSymbolHandoffIdStaysBoundToItsReferencedAssembly` |
| A-001 | Bei explizit angeforderten Diagnostics bleibt die strukturierte Assembly-/Diagnose-Ausgabe auch ohne Sessions sichtbar. | `Build_IncludeDiagnosticsWithoutSessions_EmitsExplicitEmptyDiagnosticsArray`, `Build_DefaultHealthIsCompact_AndDetailDiagnosticsStayBounded` |
| B-001 | Strukturierte Namespace-Ergebnisse folgen dem angeforderten Limit und stimmen mit Zählern und Trunkierung überein. | `ScanProjectNamespacesAsync_TruncatesStructuredNamespacesToMaxResults` |
| B-002 | Assembly-Trefferlisten signalisieren ihre Vollständigkeit unabhängig von begrenzten Diagnose- oder Referenzdetails. | `AssemblyRoute_FilteredEmptyResultDoesNotInheritReferenceTruncationInNavigation` |
| C-002 | Begrenzte Symbol-Traversierungen weisen Navigation und Folgeschritt konsistent als unvollständig aus. | `WithNavigation_SymbolTraversalMaxResultsProjectsTruncation` |
| C-003 | Assembly-Symbolsuchen melden Treffer-, Rückgabe- und Trunkierungszähler konsistent zum gelieferten Scope. | `AssemblyRoute_FindSymbolIncludeReferencesKeepsMatchCountsConsistentUnderStandardBudget` |
| D-001 | Typfehler in MCP-Argumenten werden vor der Bindung feldgenau als strukturierte Eingabefehler behandelt. | `WrongArgumentType_ReturnsFieldAwareRecoverableInvalidArgument` |
| D-002 | Ressourcenfehler werden in der Navigation als nicht verfügbare, nicht anwendbare Ergebnisse statt als Erfolg ausgewiesen. | `GetFileTree_NonExistentRoot_ReturnsResourceNotFoundWithErrorNavigation` |
| D-003 | Konkurrierende Symbolsuchparameter werden vor der Suche eindeutig und feldgenau abgewiesen. | `FindSymbol_BothPatternFormsProvided_ReturnsRecoverableInvalidArgument` |
| E-001 | Vollständige leere Textsuchen geben im Payload und in der Navigation dieselbe sinnvolle Folgeaktion aus. | `SearchPattern_EmptyResult_UsesSameNextActionInPayloadAndNavigation` |
| C-004 | Gemischte Symbolmuster-Batches mit leeren Elementen werden feldgenau abgewiesen. | `ExecuteAsync_NamePatternsWithEmptyElement_ReturnsRecoverableInvalidArgument` |
| A-002 | Nichtpositive Diagnose-Limits werden als korrigierbarer Eingabefehler abgewiesen. | `MaxDiagnosticsNonPositive_ReturnsRecoverableInvalidArgument` |
| A-003 | Partielle Assembly-Health verwendet dieselbe Folgeaktion im Payload und in der Navigation. | `Build_PartialAssemblyUsesSameNextActionAsNavigation` |
| E-002 | Symbolergebnislimits unter eins werden in Projekt- und Assembly-Routen einheitlich validiert. | `ExecuteAsync_MaxResultsZero_ReturnsRecoverableInvalidArgument` |
