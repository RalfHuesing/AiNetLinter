# Roadmap und Implementierungsplan: Verschlankung der Integrationssuite

## Arbeitsvertrag

Ziel ist eine deutlich schnellere Integrationssuite, ohne Testaussagen zu verlieren. Varianten, Edge-Cases, Formatter-, Budget- und Validierungslogik werden in FastTests verlagert. Integrations- und E2E-Tests behalten nur reale Systemgrenzen und repräsentative Verträge.

Diese Datei ist zugleich Roadmap, Umsetzungsreihenfolge und Abnahmeprotokoll. Jeder Eintrag beginnt als `[ ]`. Er darf erst nach der tatsächlich ausgeführten Arbeit und dem jeweils angegebenen Nachweis in `[X]` geändert werden. Ein gesetztes `[X]` bedeutet: Code, Tests und Dokumentation des Schritts sind vollständig, nicht nur begonnen.

Nicht Ziele dieser Aufgabe:

- Testaussagen ersatzlos löschen oder Assertions abschwächen.
- Reale Prozess-, Wire-, Dateisystem-, MSBuild-/Roslyn-Lade- oder Assembly-Metadaten-Verträge als FastTests deklarieren.
- Dogfood- oder Performance-Tests stillschweigend aus dem Standard-Gate entfernen.
- Fremde, bereits vorhandene Working-Tree-Änderungen ändern, stagen oder committen.

## Ausgangslage und Leitentscheidung

Die Analyse vom 2026-09-12 ergab 127 C#-Dateien in `AiNetLinter.IntegrationTests` (ca. 760 KB), davon 71 MCP-Dateien (ca. 580 KB). Es existieren 447 `[Fact]`-/`[Theory]`-Deklarationen. Die Zahlen sind Strukturindikatoren, keine gemessenen Laufzeiten.

Die Zuordnung folgt diesen Regeln:

| Behauptung des Tests | Zielprojekt | Erforderlicher Nachweis |
| --- | --- | --- |
| Reine Validierung, Mapping, Formatter, Budget-/Trunkierungsprojektion, Ergebnisinhalt | `AiNetLinter.FastTests` | In-Memory-Daten; weder Prozess noch MSBuild-Laden |
| Tool-Verhalten mit künstlichem `McpCodeGraphServer`-/Solution-Snapshot | `AiNetLinter.FastTests` | Bestehenden In-Memory-Testkontext verwenden |
| Reales Dateisystemverhalten | `AiNetLinter.IntegrationTests` | Isoliertes Verzeichnis über `TestTempDirectory` |
| Reales MSBuild-/Roslyn-Laden, Assembly-Metadaten oder dekompilierter Quellursprung | `AiNetLinter.IntegrationTests` | Repräsentativer End-to-End-Fall |
| MCP-Prozess, JSON-RPC-Frames, Daemon- oder Thin-Client-Lifecycle | `AiNetLinter.IntegrationTests` | Wenige repräsentative Wire-/Lifecycle-Verträge |
| Vollständiges Repository/Dogfood oder Performance-Messung | separates explizites Policy-Gate | Bewusste, dokumentierte Gate-Entscheidung |

Die FastTests besitzen einen technischen Schutz: `Process`, `Microsoft.Build`, `MSBuildWorkspace` und `SourceFileCatalog.LoadAsync` dürfen dort nicht referenziert oder zur Laufzeit geladen werden. Ein Test darf deshalb nicht durch bloßes Verschieben zur FastTest werden; seine Fixture und Abhängigkeiten müssen ebenfalls leichtgewichtig sein.

## Zielbild

1. Pro MCP-Boundary existiert ein kleiner, echter Integrationsvertrag: erfolgreicher Aufruf, mindestens ein repräsentativer Fehlervertrag sowie bei Bedarf ein Handoff oder eine echte Zielart.
2. Jede zusätzliche Parameterkombination, jeder Cap, jeder Retry und jede Payload-Projektion liegt als FastTest beim verantwortlichen Validator, Scanner, Formatter oder Tool.
3. Jeder entfernte Integrationstest ist in einer Umzugstabelle einer konkreten FastTest-Methode zugeordnet; kein Verhalten wird nur durch einen allgemeineren Test „vermutlich“ abgedeckt.
4. Die Standard-Integration läuft ohne Variantenballast; Dogfood und Performance werden anschließend bewusst behandelt, aber nicht versehentlich umklassifiziert.

## Arbeitsartefakte Schritt 0 (2026-09-12)

### Baseline und Working-Tree-Abgrenzung

- Erfasster Ausgangs-HEAD: `9217dcc971a31faa2f9959cce917ae7abb51b95b`.
- `git status --short` und `git diff --name-only` waren vor der Arbeit leer. Es gab damit keine fremden MCP-Änderungen, die vom Staging auszuschließen wären. Falls sie in einer späteren Scheibe erscheinen, bleiben sie ausdrücklich außerhalb des eigenen Stagings.
- Die TRX-Baseline wird je Lauf mit diesen Befehlen erstellt; der fest vereinbarte Ablageort bleibt `TestResults/integrationstest-verschlankung-baseline/`:

  ```powershell
  dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress" --logger "trx;LogFileName=integrationstest-verschlankung-fast-baseline.trx" --results-directory TestResults/integrationstest-verschlankung-baseline
  dotnet test src/AiNetLinter.IntegrationTests --filter "Category!=Stress" --logger "trx;LogFileName=integrationstest-verschlankung-integration-baseline.trx" --results-directory TestResults/integrationstest-verschlankung-baseline
  ```

| Projekt | TRX | Ergebnis der Ausgangsmessung | Dauer |
| --- | --- | --- | --- |
| `AiNetLinter.FastTests` | `integrationstest-verschlankung-fast-baseline.trx` | 2.478 bestanden, 0 fehlgeschlagen, 0 übersprungen | 9 s |
| `AiNetLinter.IntegrationTests` | `integrationstest-verschlankung-integration-baseline.trx` | 499 bestanden, 2 fehlgeschlagen, 0 übersprungen (501 gesamt) | 4 min 05 s (TRX-Wandzeit; Test-Runner meldet 3 min 50 s) |

Der Integrationstestlauf ist **eine Messbasis, kein grüner Gate-Nachweis**. Die beiden vor der Migration festgestellten Infrastrukturfehler sind: `AssemblyAnalysisRegistryRetirementRaceTests.LeaseAsync_FingerprintRefreshClearsPendingRequestForRetiredEntry` (gesperrte temporäre DLL) und `ThinClientDiscoveryContractTests.NewClient_StopsAfterWelcomeWithoutFingerprint_WithoutReadinessRetry` (`Pipe is broken`). Ein erster Versuch wurde ohne TRX abgebrochen, weil sein eigener Testhost-Prozessbaum hängen blieb; dieser Prozessbaum wurde beendet, bevor der oben dokumentierte, kontrollierte TRX-Lauf begann. Ein späterer Abschlusslauf muss die beiden Non-Stress-Gates grün nachweisen; aus dieser Baseline wird kein bestehender Fehler der Umzugsscheiben zugerechnet.

### FastTests-Dependency-Guard-Fitness

Die künftigen FastTests verwenden ausschließlich einen In-Memory-`McpCodeGraphServer` mit synthetischem `Solution`-Snapshot und leichtgewichtigen Argument-/Response-Objekten. Sie dürfen weder `SymbolGraphCatalogFixture`/`LoadedFixture` noch einen Prozess oder MSBuild-Laden indirekt verwenden. Der vorhandene Schutz wird bei jeder Scheibe mit ausgeführt:

- `FastTestsDependencyGuardTests` prüft statisch auf `Microsoft.Build`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`, `MSBuildWorkspace`, `System.Diagnostics.Process` und `SourceFileCatalog.LoadAsync`.
- Die Assembly-Fixture `FastTestsRuntimeDependencyGuardFixture` prüft die geladenen Assemblies zu Initialisierung und Dispose gegen `Microsoft.Build` und `Microsoft.CodeAnalysis.Workspaces.MSBuild`.
- Der erfolgreiche FastTests-Baseline-Lauf belegt den aktuellen Guard-Stand. Jede neu angelegte Fixture muss diesen Lauf erneut bestehen, bevor eine Integrationstestmethode entfernt wird.

### Vollständige Umzugstabelle

Die Tabelle ist der verbindliche Nachweis: Eine Integrationstestmethode darf erst entfernt werden, wenn ihre benannte FastTest-Methode Text, `StructuredContent`, Navigation und Fehlercode mindestens gleich präzise abdeckt. „Neu“ bedeutet eine in der benannten FastTest-Datei anzulegende Methode; der Name ist die beabsichtigte Zielmethode. Methoden, die als „verbleibt“ markiert sind, werden nicht nach FastTests kopiert.

| Scheibe | Bisheriger Integrationstest / Aussage | FastTest-Ziel | Verbleibender Integrationsvertrag / Begründung |
| --- | --- | --- | --- |
| 1 | `GetServerHealthToolTests.ValidateOptions_NonPositiveDiagnosticLimit_ReturnsFieldAwareInvalidArgument`: Feldvalidierung. | Neu: `Mcp/Tools/ServerMaintenance/GetServerHealthValidationTests.ValidateOptions_NonPositiveDiagnosticLimit_ReturnsFieldAwareInvalidArgument`. | Keiner; reine Argumentvalidierung. |
| 1 | `Build_DefaultHealthIsCompact_AndDetailDiagnosticsStayBounded`; `Build_TargetAssemblyHealthContainsOnlyTheRequestedTarget`; `Build_IncludeDiagnosticsWithoutSessions_EmitsExplicitEmptyDiagnosticsArray`; `Build_DetailedDiagnosticsProjectCompleteStatusToPartial`; `Build_IncludeSessionsRespectsMaxSessionsAndReportsTruncation`; `Build_PartialAssemblyUsesSameNextActionAsNavigation`: Health-Response-Projektion. | Neu/erweitert: `Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilderTests` mit gleichnamigen Methoden. | `ExecuteAsync_LoadFailed_*`, `ExecuteAsync_NonPositiveDiagnosticLimit_*`, `ExecuteAsync_Loaded_*`, `ExecuteAsync_NoRulesFile_*`, `ExecuteAsync_GlobalHealthRemainsAggregateWhenDetailFlagIsSet`: geladener Snapshot bzw. Registry-Vertrag. |
| 1 | `AssemblyAnalysisDispatcherCapabilityTests.DiagnosticsProjection_DeduplicatesAfterDisplayTruncation`; `DiagnosticsProjection_UsesOneGlobalSampleBudget`; `DiagnosticsProjection_TruncatedBy_DoesNotIncludeMaxDiagnosticBytesWhenOnlySlotLimitHit`: reine Diagnoseprojektion. | Neu: `Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimitsTests` mit gleichnamigen Methoden. | Alle `AssemblyRoute_*`-Fälle einschließlich synthetisch kompilierter Assemblys; sie belegen die Assembly-/Dispatcher-Grenze. |
| 2 | `McpServerArgumentValidationE2ETests`: `GetImpact_BothArgumentsProvided_*`; `GetTypeHierarchy_UnknownParameter_*`; `FindSymbol_MissingNamePatterns_*`; `FindSymbol_ZeroMaxResults_*`; `FindSymbol_InvalidKindAndEmptyBatchElementReturnPrecisePaths_AndValidRetrySucceeds`; `MetricsTree_MissingMode_UsesCodeSizeDefault`; `TargetPathTools_RejectUnknownArguments`; `FindDuplicates_RefactoringDriftModeWithoutHelperSymbol_*`; `WrongArgumentType_*`; `CallTree_InvalidFormat*`; `CallTree_NonIntegerDepth*`; `NamespaceTree_ExceedsAdvertisedCap*`; `DiscoveryTools_NegativeResponseBudget*`; `NamespaceTree_TinyPositiveResponseBudget*`; `Safeguard_NonPositiveMaxViolations*`; `GetViolations_InvalidMinSeverity*`; `GetViolations_ContextLinesOutsideRange*`; `GetTestContext_ExceedingMaxResultsCap*`; `CallTree_NonPositiveLimit*`; `FindDuplicates_NonPositiveMaxResults*`; `FindMagicValues_NonPositiveMaxResults*`: Varianten, Typen, Caps und Feldpfade. | `Mcp/Registration/McpArgumentValidationFilterTests`, `Mcp/McpBatchArgumentsTests` und die Tool-spezifischen FastTests (`SymbolGraph`, `CallTree`, `FileStructure`, `Analysis`, `FindMagicValues`); neue Methoden behalten jeweils Fehlercode, `fieldPath` und Navigation im Namen/Assert. | Verbleiben exakt `FindReferences_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgument`, `WrongArrayElementType_ReturnsIndexedFieldAwareInvalidArgument` und `UnknownTool_Call_ThrowsMcpProtocolException`: Pflichtfeld, indexierter Feldpfad und unbekanntes Tool über echten Wire-Transport. |
| 2 | `GetCallTree_MissingSymbolIdentifier_*`; `GetTypeHierarchy_MissingSymbolIdentifier_*`; `GetSymbolBody_MissingSymbolIdentifiers_*`; `GetFileSkeleton_MissingFilePaths_*`; `SearchPattern_MissingPattern_*`: weitere Pflichtfelder. | Jeweilige verantwortliche Tool-Validator-FastTests; die konkrete Zielmethode trägt den bisherigen Methodennamen. | Sie werden durch den einen verbleibenden Pflichtfeld-Wire-Fall repräsentiert; die Varianten selbst sind keine Transportaussage. |
| 3 | `McpServerToolBehaviorE2ETests`: `FindSymbol_KindFilter_*`, `FindSymbol_KindClass_*`, `FindSymbol_ZeroResults_*`, `FindReferences_UnknownSymbol_*`, `GetCallTree_ClampedDepth*`, `FindReferences_ClampedDepth*`, `NamespaceTree_ClampedDepth*`, beide `NamespaceTree_*ResponseBudget*`, beide `ClassStructure_*ResponseBudget*`, `DependencyGraph_ClampedDepth*`, beide `GetTypeHierarchy_*`, beide `GetFileSkeleton_*Error*`, `GetFileSkeleton_ResponseBudget*`, `GetIndexScope_ValidWorkspace*`, beide `GetHotspots_*`, `GetViolations_WithScopeFilter*`, `SearchPattern_RegexSearch_*`, `SearchPattern_StructuredResponse_*`, `SearchPattern_EmptyResult_*`, `GetFileTree_NonExistentRoot_*`, `GetServerHealth_TargetedResponse_*`: Filter, Not-found, Caps, Budget und Projektion. | Bestehende FastTests in `Mcp/Tools/SymbolGraph`, `CallTree`, `FileStructure`, `Analysis`, `TypeHierarchy`, `Common` und `ServerMaintenance`; neue Methoden behalten die bisherigen präzisen Methodennamen. | Pro Boundary-Gruppe bleibt ein erfolgreicher Standardfall; `GetFileSkeleton_HandoffIdCanBePassedToGetSymbolBody` und `FeatureContext_CallerHandoffIdCanBePassedToFeatureContext` bleiben als echte Cross-Tool-Handoffs. |
| 3 | `McpServerAssemblyHealthE2ETests`: `GetAssemblyContext_ZeroPositiveLimit*`; `GetAssemblyContext_UpperLimit*`; `AssemblyCallTree_InvalidFormat*`; `AssemblyDiscovery_NegativeResponseBudget*`; alle drei `AssemblyDiscovery_Capped*`; `AssemblyRoute_ArrayElementTypeMismatch*`; beide `AssemblyDetailLevel_*`; `AssemblyDetailLevelContract_*`: Limits, Array-Typen, Detaillevel und Budgets. | `Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests*`, `AssemblyAnalysis*BudgetTests`, `Mcp/Registration/McpArgumentValidationFilterTests` und `Mcp/McpBatchArgumentsTests`. | `InspectAssembly_StandaloneCallReturnsStructuredMetadata`, `GetServerHealth_UsesAggregateProjectAndAssemblyTargetVariants`, `GetServerHealth_WithIncludeDiagnostics_ReturnsDetailedDiagnosticsPayload` und `InspectAssembly_RegistrationAdvertisesGenericFiltersAndParameterMetadata` bleiben für echte Assembly, Health und Schema. `InspectAssembly_DoesNotExposeMaterializationPaths` wird als Security-/Assembly-Vertrag beibehalten. |
| 4 | `SearchPatternToolTests`: `ExecuteAsync_PlainTextTruncates*`, `NoMatch*`, `GeneratedObjBin*`, `WorktreeSubdirectory*`, `InvalidRegex*`, `EmptyPattern*`, `CompileErrorFixture*`, `StructuredContent*`, `MultipleMatchesAndContext*`, `MaxResultsAndMaxFiles*`, `MaxResponseBytes*`, `ScopeAndFilters*`, `InvalidBudgets*`, `OverCap*`, `InvalidScopeType*`, `DefaultCall*`: Scanner-, Filter-, Range- und Budgetmatrix. | `Mcp/Tools/Analysis/SearchPatternScannerTests`, `SearchPatternScannerEvaluationTests`, `SearchPatternPromotionTests`; neue Tool-FastTests für Text-/`StructuredContent`-Parity. | `ExecuteAsync_PlainTextSubstring_FindsExpectedHitsInFixture`, `ExecuteAsync_EnrichCSharp_ReturnsSemanticObjectAndKeepsTextPayload` und `ExecuteAsync_InvalidRegex_ReturnsRecoverableInvalidArgument` beweisen geladenen Snapshot, semantische Anreicherung und repräsentativen Fehler. |
| 4 | `FindMagicValuesToolTests`: `LoadingState*`, `UnknownValueType*`, `UnknownCategoryFilter*`, `MaxResultsClamped*`, `MinOccurrencesClamped*`, `StructuredContentShape*`, `ScopeFilterNoMatch*`, `AddFindMagicValues_ToolAppearsInRegistrationList`, `AllFiltersDefault*`; `GetIndexScopeToolTests`: alle `MixedFixture_*`, `GeneratedObjBin*`, `CompileErrorFixture*`. | `Mcp/Tools/FindMagicValues/FindMagicValuesScanner*`, Tool-Validator-/Renderer-FastTests sowie neue `GetIndexScope`-Scanner-/Renderer-FastTests mit In-Memory-Snapshot. | Je ein real geladener Solution-Fall für FindMagicValues und GetIndexScope; `NoSolutionLoaded` bleibt jeweils als Lifecycle-Fehlervertrag. |
| 5 | `Mcp/Daemon/**`, `McpServerCommandJsonRpcFramingTests*`, `McpToolAnnotationsWireTests`, CLI-Prozess-Tests, `MsBuildFixtureHost*` und Tests mit `AssemblyTestHelper.Emit*`. | Keiner, außer klar abtrennbare Helper-/Formattermethoden. | Verbleiben Integration: Prozess, JSON-RPC-Frames, Daemon/Thin-Client-Lifecycle, echtes Dateisystem, MSBuild-/Roslyn-Laden und synthetisch kompilierte Assemblys sind reale Grenzen. |
| 6 | Klassen mit Kategorien `Dogfood` oder `Performance` im Non-Stress-Filter. | Keiner; Tests werden nicht gelöscht oder umklassifiziert. | Laufzeit und Policy werden separat erhoben; jede Änderung der Gate-Zugehörigkeit benötigt ausdrückliche fachliche Freigabe. |

## Umsetzungsscheiben

### 0. Vorbereitung, Schutz des Working Trees und Messkonzept

- [X] Vor der ersten Codeänderung `git status --short`, `git diff --name-only` und den aktuellen HEAD erfassen; die bereits fremd geänderten MCP-Dateien ausdrücklich aus dem eigenen Staging ausschließen.
- [X] Für jede betroffene Klasse eine Umzugstabelle anlegen: bisherige Testmethode, Behauptung, neue oder erweiterte FastTest-Methode, verbliebener Integrationsvertrag und Begründung.
- [X] Eine wiederholbare Laufzeitbasis per TRX für beide Non-Stress-Testprojekte festlegen und vor der ersten Umzugsscheibe einmal erfassen. Die Baseline enthält Dauer, Anzahl und fehlgeschlagene Tests; sie ist kein Performance-Benchmark einzelner Tests.
- [X] Prüfen, dass alle künftigen FastTest-Fixtures die FastTests-Dependency-Guards einhalten und weder Prozess noch MSBuild-/Katalogladen verwenden.

Abnahme: Die Umzugstabelle ist vollständig für alle nachfolgenden Scheiben; Baseline und Working-Tree-Abgrenzung sind nachvollziehbar dokumentiert.

### 1. Reine Response- und Validierungslogik sofort verlagern

Diese Scheibe enthält Tests, deren aktueller Methodenrumpf keine reale Boundary berührt.

- [X] Die Theorie `ValidateOptions_NonPositiveDiagnosticLimit_ReturnsFieldAwareInvalidArgument` aus `GetServerHealthToolTests` nach `FastTests/Mcp/Tools/ServerMaintenance` verschieben oder dort fachlich passend ergänzen.
- [X] Die sechs `Build_*`-Fälle aus `GetServerHealthToolTests` als FastTests beim `GetServerHealthResponseBuilder` verlagern: Compact/Detailed-Ansicht, leere Diagnosen, Session-Limit, Partial-Status und zielgerichtete Assembly-Auswahl.
- [X] Die drei `DiagnosticsProjection_*`-Fälle aus `AssemblyAnalysisDispatcherCapabilityTests` in einen FastTest neben `AssemblyAnalysisResponseLimits` überführen: Deduplizierung, Trunkierungsgrund und globales Sample-Budget.
- [X] Die bisherigen Integrationstestmethoden erst entfernen, wenn die zugeordneten FastTests exakt dieselbe fachliche Aussage einschließlich StructuredContent und Text-Projektion belegen.
- [X] Den kleinsten passenden FastTest-Lauf für die neuen Klassen ausführen und die FastTests-Dependency-Guards prüfen.

Verbleibt als Integration: alle `ExecuteAsync_*`-Health-Fälle mit realem geladenem Snapshot sowie alle Assembly-Dispatcher-Fälle mit synthetisch kompilierten Assemblies.

Abnahme: Keine reine Builder-/Validator-/Projektionslogik aus dieser Scheibe bleibt im Integrationstestprojekt zurück.

### 2. MCP-Argumentvarianten aus dem echten Host entkoppeln

`McpServerArgumentValidationE2ETests` enthält rund 30 Varianten über den gemeinsam gestarteten `ReadOnlyMcpHostFixture`. Der Prozessvertrag ist real, die Mehrzahl der Variationen jedoch nicht.

#### Detailzuordnung Schritt 2

| Bisherige E2E-Varianten | FastTest-Nachweis |
| --- | --- |
| fehlende Pflichtfelder (`GetCallTree`, `GetTypeHierarchy`, `GetSymbolBody`, `GetFileSkeleton`, `SearchPattern`, `FindReferences`) | `McpArgumentValidationFilterTests.ValidateArguments_MissingRequiredField_ReturnsPreciseErrorWithNavigation`; verantwortliche Tooltests behalten die fachliche Fehlermeldung. |
| unbekannte Argumente (`GetTypeHierarchy`, `TargetPathTools`) | `McpArgumentValidationFilterTests.ValidateArguments_UnknownArgument_ReturnsPreciseFieldPath`. |
| falsche JSON-Typen und `FindSymbol`-Arrayelement | `McpArgumentValidationFilterTests.ValidateArguments_WrongJsonType_ReturnsPreciseFieldPath` und `ValidateArguments_WrongArrayElementType_ReturnsIndexedFieldPath`. |
| positive/nicht-negative Limits, Caps, Budgetuntergrenze und `GetTestContext`-Cap | `McpArgumentValidationFilterTests.ValidateArguments_LimitOutsideContract_ReturnsPreciseFieldPath` und `ValidateArguments_ResponseBudgetBelowPublicMinimum_ReturnsPreciseFieldPath`. |
| `GetImpact`-Exklusivität, `FindSymbol`-Kind/Batch, `MetricsTree`-Default, `FindDuplicates`-Helper, CallTree-Format | bestehende `GetImpactToolTests`, `FindSymbolToolTests`/`FindSymbolValidationToolTests`, `MetricsTreeToolTests`, `DuplicateDetectionToolRefactoringDriftTests` und `GetCallTreeToolTests`. |
| `GetViolations`-Enum/Range und `FindMagicValues`-Clamp-Kompatibilität | verantwortliche `GetViolationsToolTests` bzw. `FindMagicValues`-FastTests. |
| `Safeguard`-Kompatibilität für `maxViolations <= 0` | `McpArgumentValidationFilterTests.ValidateArguments_SafeguardNonPositiveMaxViolations_PreservesToolCompatibility`. |

Die drei verbliebenen Wire-Tests sind absichtlich `UnknownTool_Call_ThrowsMcpProtocolException`, `FindReferences_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgumentWithNavigation` und `WrongArrayElementType_ReturnsIndexedFieldAwareInvalidArgument`.

- [X] Für jedes Tool die gegenwärtigen Fehlervarianten aus `McpServerArgumentValidationE2ETests` in der Umzugstabelle den verantwortlichen FastTest-Zielen zuordnen: `McpArgumentValidationFilterTests`, `McpBatchArgumentsTests` sowie die jeweiligen Tool-Validator-/Tool-Tests.
- [X] Fehlende FastTests für Pflichtfelder, unbekannte Argumente, falsche Typen, Array-Elementtypen, nicht positive Limits, Obergrenzen und exakte `fieldPath`-Werte ergänzen.
- [X] Die FastTests müssen sowohl Fehlercode als auch `fieldPath` und, wo bisher behauptet, die Navigation prüfen; nur den Text zu prüfen reicht nicht.
- [X] Im Integrationstest genau drei repräsentative Fälle belassen: ein fehlendes Pflichtfeld mit Navigation, ein falsch typisiertes Feld mit indexiertem `fieldPath` und ein unbekanntes Tool mit Protokollfehler.
- [X] Alle übrigen Varianten nach bestandenen FastTests aus `McpServerArgumentValidationE2ETests` entfernen und die Klasse auf die drei Wire-Verträge reduzieren.
- [X] Den fokussierten FastTest- und den fokussierten Integrationstestlauf ausführen.

Abnahme: Jede entfernte E2E-Argumentvariante besitzt einen spezifischen FastTest; der verbleibende Integrationstest beweist weiterhin die komplette Fehlerhülle über echten MCP-Transport.

### 3. Tool-Verhalten auf repräsentative E2E-Verträge reduzieren

`McpServerToolBehaviorE2ETests` und `McpServerAssemblyHealthE2ETests` wiederholen viele Tool-spezifische Varianten über denselben Prozess.

#### Detailzuordnung Schritt 3

| Bisherige E2E-Varianten | Konkreter FastTest-Nachweis |
| --- | --- |
| FindSymbol-Kindfilter, Filter-vor-Limit und Leerresultat | `FindSymbolToolTests.ExecuteAsync_ClassKind_ExcludesRecordDeclarations`, `FindSymbolToolTests.ExecuteAsync_MultiplePatterns_OneMatchOneMiss_ContinuesAndIncludesMissHint` und `FindSymbolToolTests.ExecuteAsync_MultiplePatterns_TruncatesIndividuallyPerPattern`. |
| FindReferences unknown, Scope-vor-Limit und Depth-Clamp | `FindReferencesToolTests.ExecuteAsync_UnknownSymbol_ReturnsRecoverableSymbolNotFound`, `ExecuteAsync_ScopeFiltersBeforeLimit_AndRanksProductionFirst` und `ExecuteAsync_DepthAboveCap_ClampsToThreeAndReturnsResult`. |
| CallTree-, NamespaceTree- und DependencyGraph-Depth | `GetCallTreeToolTests.ExecuteAsync_DepthAboveCap_ClampsAndStillReturnsResult`, `GetNamespaceTreeToolTests.ExecuteAsync_DepthAboveCap_ReportsRequestedAndEffectiveDepth` und `DependencyGraphScannerTests.ScanFileAsync_DepthAboveCap_ClampsToThree`. |
| NamespaceTree- und ClassStructure-Budget einschließlich Text-/StructuredContent-Gleichlauf | `GetNamespaceTreeToolTests.ExecuteAsync_MaxResponseBytes_ProjectsSameVisibleTypesIntoTextAndStructuredContent`, `ExecuteAsync_BudgetBelowLeafMinimum_ReturnsExactRetryableBudgetError` sowie `GetClassStructureToolTests.ExecuteAsync_MaxResponseBytes_UsesSameVisibleMembersInTextAndStructuredContent`. |
| TypeHierarchy-, Skeleton-, Hotspots- und Violations-Varianten | `GetTypeHierarchyToolTests.ExecuteAsync_UnknownTypeIdentifier_ReturnsRecoverableSymbolNotFound`, `GetFileSkeletonToolTests.ExecuteAsync_UnknownFilePath_ReturnsRecoverableResourceNotFound`, `GetHotspotsToolTests.ExecuteAsync_MaxResultsAndMinLinePercentage_KeepDeterministicBoundedResults` und `GetViolationsToolTests.ExecuteAsync_ScopeFilterMatchesProjectName_StructuredContentDeserializesToRuleViolations`. |
| SearchPattern-Regex, Ergebnisform, Leerresultat und Budget | `SearchPatternScannerTests.Scan_RegexUsesSameRangeModelAndStableOrdering`, `Scan_PlainText_EmitsAllMatchRangesAndStablePositions`, `Format_ZeroHitsWithWildcardAndPlainSearch_AppendsWildcardHint` und `Scan_MaxFilesAndMaxResponseBytes_ReportSeparateTruncationReasons`. |
| Assembly-Limits, ungültige `memberNames`-Elemente und Navigation der Fehlerhülle | `McpArgumentValidationFilterTests.GetAssemblyContext_ZeroPositiveLimitReturnsFieldAwareInvalidArgument`, `GetAssemblyContext_UpperLimitReturnsFieldAwareInvalidArgument` und `AssemblyRoute_ArrayElementTypeMismatchReturnsIndexedFieldAwareInvalidArgument`. |
| Assembly-Budgets und `detailLevel` | `AssemblyAnalysisToolTests.InspectAssembly_GlobalResponseBudgetUsesOneTypedSelectionForTextAndJson`, `AssemblyAnalysisPostNavigationResponseBudgetTests` sowie `AssemblyAnalysisResponseLimitsTests.AssemblyDetailLevelContract_UsesCanonicalValuesForToolsAndValidator`. |

Die verbleibenden E2E-Tests prüfen ausschließlich je einen Standardaufruf der Symbol- und Struktur-Boundary, den SearchPattern-Transport, die zwei Cross-Tool-Handoffs sowie die echten Assembly-, Health-, Sicherheits- und Registrierungsszenarien.

- [X] Für `McpServerToolBehaviorE2ETests` die Varianten zu FindSymbol, FindReferences, CallTree, NamespaceTree, ClassStructure, Skeleton, Hotspots, TypeHierarchy, Violations und SearchPattern den bereits vorhandenen FastTest-Bereichen zuordnen.
- [X] In diesen FastTest-Bereichen fehlende Assertions für Filter vor Limit, „nicht gefunden“, Depth-Clamping, Response-Budget, Text-/StructuredContent-Gleichlauf und Navigation ergänzen.
- [X] In `McpServerToolBehaviorE2ETests` nur einen erfolgreichen Standardaufruf je Boundary-Gruppe sowie mindestens einen echten Cross-Tool-Handoff belassen; keine bloßen Parameter- oder Cap-Varianten.
- [X] Die Assembly-Health-Varianten zu Limits, `detailLevel`, ungültigen Array-Elementen und Budgetfehlern in die FastTests für Assembly-Tool, Response-Budget und Argumentvalidierung verlagern.
- [X] In `McpServerAssemblyHealthE2ETests` mindestens einen echten `inspect_assembly`-Durchlauf, einen Health-Aufruf und einen Registrierungs-/Schema-Vertrag behalten.
- [X] Je verschobenem Verhalten prüfen, dass Text, StructuredContent und Navigation im FastTest ebenso präzise abgedeckt sind wie zuvor im Wire-Test.
- [X] Fokussierte FastTest- und Integrationstestläufe ausführen.

Abnahme: Der echte MCP-Prozess deckt Verträge und Zusammensetzung ab; Toolvarianten werden ausschließlich im schnellen, verantwortlichen Testbereich gepflegt.

### 4. Geladene Solution gezielt minimieren, nicht leugnen

`SearchPatternToolTests`, `FindMagicValuesToolTests`, `GetIndexScopeToolTests` und verwandte Klassen verwenden einen geladenen MSBuild-/Roslyn-Snapshot. Das macht ihre gegenwärtige Fixture legitim als Integration, aber nicht jede einzelne Variante.

- [X] Für `SearchPatternToolTests` zwei bis drei reale Laden-/Ausführungsverträge festlegen: ein normaler Treffer, ein semantisch angereicherter Treffer und ein repräsentativer Fehler-/Completeness-Vertrag.
- [X] Regex-, Filter-, Scope-, Exclusion-, Range- und Budgetvarianten in die bestehenden SearchPattern-Scanner-/Tool-FastTests mit In-Memory-Solution verlagern.
- [X] Für FindMagicValues und GetIndexScope jeweils einen echten geladenen-Solution-Vertrag behalten; Heuristiken, Filter, Limits, Renderer und Payloadvarianten in die vorhandenen FastTest-Suiten überführen.
- [X] Keine der FastTest-Fixtures darf `LoadedFixture`, `SourceFileCatalog.LoadAsync`, `MSBuildWorkspace` oder einen Prozess indirekt referenzieren.
- [X] Nach jeder Klasse zuerst die neuen FastTests, dann den verbleibenden Integrationsvertrag ausführen.

#### Konkrete Umzugstabelle Schritt 4 (2026-09-12)

| Bisherige Integrationstestmethoden | FastTest-Nachweis | Verbleibender Integrationsvertrag |
| --- | --- | --- |
| `SearchPatternToolTests`: Regex, NoMatch, Trunkierung, obj/bin- und Worktree-Ausschluss, leeres Pattern, Compile-Error, StructuredContent, Mehrfachtreffer/Context, Limits, Scope/Filter, Budgets, Caps, ScopeType und Default | `SearchPatternScannerTests`, `SearchPatternScannerEvaluationTests`, `SearchPatternPromotionTests` sowie `SearchPatternToolContractTests` (Text-/StructuredContent-/Fehler-Parität) | `ExecuteAsync_PlainTextSubstring_FindsExpectedHitsInFixture`, `ExecuteAsync_EnrichCSharp_ReturnsSemanticObjectAndKeepsTextPayload`, `ExecuteAsync_InvalidRegex_ReturnsRecoverableInvalidArgument` |
| `FindMagicValuesToolTests`: Loading, Filter-Validierung, Clamp, StructuredContent, Scope-Renderer, Registrierung und Default-Payload | `FindMagicValuesScanner*` sowie `FindMagicValuesToolContractTests` (Text, Objekt-Payload und Registrierung) | `ExecuteAsync_NoSolutionLoaded_ReturnsIsErrorTrueWithSolutionNotLoadedCode`, `ExecuteAsync_LoadedSolution_ReturnsStructuredCandidatePayload` |
| `GetIndexScopeToolTests`: Mixed-File-Matrix, Generated-Ausschluss, Compile-Error und Routing-/Payloadvarianten | `GetIndexScopeToolContractTests` (Text, `breakdown`, Population und Routing) | `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`, `ExecuteAsync_LoadedSolution_ReturnsRealCatalogBreakdown` |

Die neuen FastTests verwenden ausschließlich `RoslynTestSolutionFactory`-Snapshots und `TestTempDirectory`; sie referenzieren weder die geladenen Integration-Fixtures noch `SourceFileCatalog.LoadAsync`, `MSBuildWorkspace` oder Prozesse.

Abnahme: Real geladene Lösungen beweisen weiterhin die Laden-Grenze; die Fallmatrix läuft ohne MSBuild im FastTestprojekt.

### 5. Bewusst unverändert lassen: echte Boundary-Tests

#### Boundary-Nachweis Schritt 5 (2026-09-12)

Die folgenden Bereiche wurden gegen die Umzugstabelle und ihre tatsächlichen
Abhängigkeiten geprüft. Sie bleiben bewusst im Integrationstestprojekt: Die
jeweilige Behauptung kann nicht mit einem In-Memory-Snapshot oder einem
Formatter-/Validator-FastTest belegt werden.

| Nicht verschobener Bereich | Geprüfte Tests bzw. Testhilfe | Reale Boundary-Begründung |
| --- | --- | --- |
| Daemon-Host und benannte Pipes | `Mcp/Daemon/DaemonHostProcessContractTests`, `DaemonHostMcpProcessContractTests`, `DaemonProcessContractHarness` | Start, Exklusivität und Freigabe zweier realer Daemon-Prozesse sowie der Welcome-Handshake über den betriebssystemweiten Pipe-Endpunkt sind Prozess- und IPC-Verträge. Der MCP-Host-Test ergänzt dies um die Zusammensetzung einer laufenden Session mit registrierten Tools. |
| Thin-Client-Lifecycle | `Mcp/Daemon/ThinClientMcpProcessContractTests`, `ThinClientDiscoveryContractTests`, `ThinClientsSharedWarmthProcessContractTests`, `DaemonEndpointJanitor` | Diese Fälle prüfen den tatsächlich gestarteten Thin-Client/Daemon, die Discovery-Datei, PID- und Resident-Project-Zustand sowie die Wiederverwendung eines warmen Prozessschlüssels. Das sind keine isolierbaren Argument- oder Payloadvarianten. |
| Thin-Client-Proxy-Fehlerpfade | `Mcp/Daemon/ThinClientProxySessionContractTests` | Die Skript-Pipes steuern den Fehler deterministisch, aber behauptet werden Lebenszyklus-Eigenschaften der Pipe-Session: genau ein Replay, Timeout-Verhalten und dass ein identifizierter fremder Prozess nicht beendet wird. Die Verwendung von `StandInProcess` belegt dabei die Prozessgrenze. |
| JSON-RPC-Frames und Discovery-Wire-Format | `McpServerCommandJsonRpcFramingTests` (einschließlich `.Instructions.cs`), `McpToolAnnotationsWireTests`, `McpRawWireTestHarness` | Ein separat gestarteter CLI-Prozess erhält rohe JSON-RPC-Eingaben über stdin; dessen stdout wird als einzelne Frames geparst. Init-/Tools-Listen-Varianten, Annotations und `structuredContent` sind hier Transportverträge, nicht bloße Renderer-Ausgaben. |
| Weitere MCP-/CLI-Prozessverträge | `McpProcessHost`, `McpProcessRunner`, `McpServerLifetimeTests`, `McpServerCommandAmbiguityE2ETests`, `McpTestClientParallelTests`, `McpTestClientRetryTests` sowie die CLI-Runner-Verwender | Diese Tests beobachten Exit-Code, Start, Stdio, Retry und Parallelität eines externen Prozesses. Ein Ersatz mit einer direkten Tool-Instanz würde gerade den behaupteten Prozessvertrag umgehen. Dogfood-spezifische CLI-Tests bleiben zusätzlich von Schritt 6 erfasst. |
| Echte Dateisystemwirkung | Die nicht verlagerten Datei- und Cache-Verträge in `Baseline`, `Cache`, `Suppression`, `Core`, `Configuration`, `Fixtures`, `Metrics`, `Maps`, `Output` und `Diagnostics` | Soweit diese Tests erhalten bleiben, prüfen sie erzeugte, gelöschte, gefilterte oder erneut geladene Dateien/Verzeichnisse in einem isolierten `TestTempDirectory`. Reine Scanner-, Filter- und Renderer-Matrizen wurden bereits in den Schritten 1 bis 4 nach FastTests verlagert; ein verbliebener Test muss die persistente Wirkung selbst beobachten. |
| Einmaliges MSBuild-/Roslyn-Laden | `Platform/MsBuildFixtureHost`, `MsBuildFixtureHostAssemblyFixture`, `MsBuildFixtureHostTests` | Die Assembly-Fixture lädt eine isolierte `BaselineMini`-Kopie mit `LoadedFixture` und damit echtem `MSBuildWorkspace`/`SourceFileCatalog.LoadAsync`. Die Tests belegen außerdem Katalog, Projektpräsenz und die geteilte `Solution`-Instanz über Testklassen hinweg. Dies ist genau die ausgeschlossene FastTests-Abhängigkeit. |
| Synthetisch kompilierte Assemblies und Metadaten | Alle Verwendungen von `AssemblyTestHelper.EmitAssembly` in `Mcp/Assemblies/**` sowie `Mcp/Daemon/DaemonHostMcpContractTests` | Die Tests kompilieren IL in eine echte temporäre DLL und prüfen anschließend Decompilation, PE-Fehler, Referenzauflösung, Assembly-Session-, Routing- und Handoff-Lebenszyklus. Ein `Solution`-Snapshot kann weder Metadatenroute noch native PE-Fehler oder Session-Provenienz ersetzen. |

Gemischte Klassen wurden ebenfalls geprüft. Die privaten Hilfen in den genannten
Wire-/Lifecycle-Klassen erzeugen oder lesen ausschließlich Frames, Prozesse,
Pipes und Assemblies für den jeweiligen Boundary-Fall; keine testbare
Helper-/Formatterbehauptung ist davon unabhängig. Deshalb wurde nichts nach
FastTests extrahiert und kein Boundary-Fall nachgebildet.

- [X] Die Daemon-, Thin-Client-, Lifecycle- und JSON-RPC-Frame-Tests in `IntegrationTests/Mcp/Daemon`, `McpServerCommandJsonRpcFramingTests` und `McpToolAnnotationsWireTests` gegen die Umzugstabelle prüfen und als Integration bestätigen.
- [X] CLI-Prozess-Tests, echte Dateisystemeffekte, `MsBuildFixtureHost` sowie Tests mit synthetisch kompilierten Assemblies als Integration bestätigen, sofern ihre jeweilige Aussage die reale Grenze betrifft.
- [X] Bei gemischten Klassen nur die reinen Helper-/Formattermethoden extrahieren; den echten Boundary-Fall nie in FastTests nachbilden.

Abnahme: Für jeden nicht verschobenen Bereich ist die Boundary-Begründung in der Umzugstabelle festgehalten.

### 6. Dogfood- und Performance-Policy separat entscheiden

Die Kategorien `Dogfood` und `Performance` sind aktuell nicht `Stress`; damit laufen sie im Standardfilter `Category!=Stress` mit.

- [X] Laufzeiten und Aussage der Dogfood-/Performance-Klassen separat erfassen.
- [X] Entscheiden und dokumentieren, ob sie absichtlich Teil des Non-Stress-Gates bleiben oder als explizites manuelles/nächtliches Gate geführt werden sollen.
- [X] Diese Kategorieentscheidung nur mit ausdrücklicher fachlicher Freigabe umsetzen; sie ist keine automatische Folge der FastTest-Migration.

#### Policy-Nachweis (2026-09-12)

Die Kategorien bleiben **bewusst im Non-Stress-Gate**. Es gibt keine fachliche
Freigabe, sie in ein manuelles oder nächtliches Gate zu verschieben; daher wurden
keine Traits geändert und keine Tests entfernt. Der verpflichtende
Integrations-Gate bleibt unverändert:

```powershell
dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress"
dotnet test src/AiNetLinter.IntegrationTests --filter "Category!=Stress"
```

Für eine getrennte, reproduzierbare Messung der beiden Kategorien werden die
folgenden Befehle verwendet (die Messung ist kein Ersatz für das Non-Stress-Gate):

```powershell
dotnet test src/AiNetLinter.IntegrationTests --filter "Category=Dogfood" --logger "trx;LogFileName=integrationstest-verschlankung-dogfood-policy.trx" --results-directory TestResults/integrationstest-verschlankung-policy
dotnet test src/AiNetLinter.IntegrationTests --filter "Category=Performance" --logger "trx;LogFileName=integrationstest-verschlankung-performance-policy.trx" --results-directory TestResults/integrationstest-verschlankung-policy
```

| Kategorie | Klasse | Aussage | TRX-Ergebnis und Klassenzeit |
| --- | --- | --- | --- |
| Dogfood | `CliRepositoryDogfoodTests` | Führt die CLI gegen die vollständige eigene Solution aus und erwartet Erfolg. | 1/1 bestanden, 20,153 s |
| Dogfood | `McpLiveRepositoryTests` | Prüft 26 MCP-Tool-, Handoff-, Audit- und Scope-Verträge gegen das echte Repository. | 26/26 bestanden, 2 min 18,875 s |
| Dogfood | `McpLiveRepositoryResourceTests` | Prüft Resource-URIs, Discovery und Inhalte gegen den kodierten Repository-Pfad. | 1/1 bestanden, 1,108 s |
| Dogfood | `McpDocumentationSmokeTests` | Prüft Live-Toolaufrufe sowie die veröffentlichte Agent- und Integrationsdokumentation. | 6/6 bestanden, 45,870 s |
| Dogfood | `McpServerToolBehaviorE001E2ETests` | Prüft die übereinstimmende Folgeaktion eines leeren `search_pattern`-Ergebnisses über den echten MCP-Host. | 1/1 bestanden, 1,576 s |
| Dogfood | `McpServerToolBehaviorD002E2ETests` | Prüft den navigierbaren `RESOURCE_NOT_FOUND`-Vertrag für einen fehlenden Dateibaum-Root über den echten MCP-Host. | 1/1 bestanden, 2,674 s |
| Performance | `LoadFixtureMeasurementsTests` | Misst Cold-Start bei ca. 1.000 LOC sowie zehn `GetCurrentSolution`-Proben bei ca. 10.000 LOC; behauptet nur endliche, nichtnegative Messwerte. | 2/2 bestanden; 0,698 s und 2,038 s |

Die Dogfood-Messung benötigte für 36 bestandene Tests 2 min 36,347 s TRX-Wandzeit
(Summe der Testzeiten wegen Parallelität 3 min 30,257 s). Die Performance-Messung
benötigte für zwei bestandene Tests 7,473 s TRX-Wandzeit. Diese Einzelmessung
ist ein Beobachtungswert, keine Laufzeitgarantie.

Abnahme: Die Gate-Policy ist explizit, die zugehörigen Befehle sind dokumentiert und die Tests bleiben erhalten.

### 7. Vollständige Verifikation und Abschluss

- [X] Alle verschobenen Tests gegen die Umzugstabelle prüfen: keine alte Integrationstestmethode ohne Ersatz, kein Ersatz ohne ursprüngliche Behauptung.
- [X] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` erfolgreich ausführen.
- [X] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` erfolgreich ausführen.
- [X] `dotnet build` erfolgreich und warnungsfrei ausführen.
- [X] TRX-Daten vor/nach der Umstellung vergleichen: Gesamtdauer, Anzahl Tests und auffällige Klassen. Das Ergebnis als Messwert dokumentieren, ohne aus einer einzelnen Messung eine Garantie abzuleiten.
- [X] `git diff --check` und eine auftragsbezogene Diff-Prüfung durchführen; ausschließlich eigene Pfade stagen und gemäß Projektregel committen.

#### Verifikationsnachweis (2026-09-12)

Der Abschlusslauf verwendet die finalen TRX-Dateien unter
`TestResults/integrationstest-verschlankung-final/`. Sie wurden gegen die
Baseline unter `TestResults/integrationstest-verschlankung-baseline/`
ausgewertet:

| Projekt | Baseline | Final | TRX-Wandzeit Baseline → Final | Auffällige Klassen im Final-Lauf |
| --- | --- | --- | --- | --- |
| `AiNetLinter.FastTests` | 2.478 bestanden | 2.536 bestanden | 9,298 s → 8,714 s | `AssemblyAnalysisToolTests` (27 Tests, 6,011 s), `AssemblyAnalysisRegistryTests` (15 Tests, 5,004 s) |
| `AiNetLinter.IntegrationTests` | 499 bestanden, 2 Infrastrukturfehler (501 gesamt) | 371 bestanden, 0 Fehler | 240,862 s → 187,794 s (−53,068 s; rund 22,0 %) | `McpLiveRepositoryTests` (26 Tests, 170,369 s), `McpServerCommandJsonRpcFramingTests` (8 Tests, 137,994 s), `AssemblyAnalysisRouteTests` (12 Tests, 55,716 s) |

Die Zeiten sind einzelne Beobachtungswerte, keine Laufzeitgarantie. Die
Integration verlor gegenüber der Baseline 130 Testfälle; die FastTests gewannen
58 Testfälle. Die weiterhin längsten Integrationsklassen sind bewusst erhaltene
Dogfood-, Raw-Wire- und Assembly-Boundary-Verträge.

Der Diff-Audit gegen die Ausgangsbasis `9217dcc` erfasste 106 entfernte
Integrationstestmethoden in den neun geänderten Klassen. Jede Methode ist einer
konkreten Zeile der Umzugstabellen aus Schritt 1 bis 4 zugeordnet: 10
Builder-/Diagnose-Fälle, 27 Argumentvarianten, 35 Tool-/Assembly-Varianten und
34 geladene-Solution-/Toolfälle. Die 27 in diesen Klassen behaltenen Methoden
sind die jeweils dokumentierten Boundary-Verträge. Die sieben neu angelegten
FastTest-Klassen und die in den Tabellen benannten bestehenden FastTest-Klassen
prüfen die ursprünglichen Aussagen; bei den verschobenen Response-,
Validierungs- und Vertragstests einschließlich Fehlercode, `fieldPath`,
Text-/`StructuredContent`-Projektion und Navigation. Damit blieb kein
entfernter Test ohne spezifischen Nachweis und kein neu hinzugefügter Ersatz
ohne dokumentierte Ursprungsaussage.

Der finale Abschlussnachweis lautet: `FastTests` 2.536/2.536 bestanden,
`IntegrationTests` 371/371 bestanden und `dotnet build` mit 0 Warnungen sowie
0 Fehlern. Der FastTests-Dependency-Guard lief als Teil des vollständigen
FastTest-Gates mit.

Das finale MCP-Quality-Gate erreichte in allen geänderten C#-Scopes 10,00/10
bei 0 Verstößen: `src/AiNetLinter.FastTests` (301 Klassen),
`src/AiNetLinter.IntegrationTests` (153 Klassen) und
`src/AiNetLinter/Mcp/Registration` (18 Klassen). Die zugehörigen
`get_violations`-Aufrufe lieferten jeweils 0 Verstöße. Die scoped
Dead-Code-/Magic-Value-Audits enthielten keine neue blockierende Feststellung;
ihre Heuristik-Kandidaten außerhalb der geänderten Dateien wurden nicht als
Teil dieser Verschlankung verändert.

Abnahme: Beide Non-Stress-Gates und der Build sind grün, die FastTests-Guards bestehen, die Umzugstabelle ist vollständig und die Integrationslaufzeit ist anhand der TRX-Vergleiche nachvollziehbar reduziert.

## Künftige Pflege

Bei jedem neuen MCP-Test zuerst die Behauptung formulieren. Nur wenn sie einen echten Prozess-, Wire-, Datei-, MSBuild-/Roslyn-Lade- oder Assembly-Vertrag benötigt, gehört sie in `IntegrationTests`. Alle anderen Varianten beginnen als FastTest. Bei Zweifel wird zunächst ein FastTest plus ein einzelner repräsentativer Boundary-Vertrag angelegt, nicht dieselbe Fallmatrix auf beiden Ebenen.
