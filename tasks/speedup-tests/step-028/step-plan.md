---
status: done
type: step-plan
task: speedup-tests
step: 028
corrects: step-027
title: "Korrektur: enge Step-027-Matrixevidenz nachweisen"
epic: EPIC-6
estimated_risk: low
step_type: single
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-14T13:00:00+02:00
related_to:
  - step-027/step-review.md
---

# Step 028: Korrektur: enge Step-027-Matrixevidenz nachweisen

## Bezug und Scope

- Dieser Fix-Step korrigiert ausschließlich Finding 1 aus
  `step-027/step-review.md`: Die breiten Ersatzläufe mit 318 Fast- und 112
  Integrationstests werden durch nachvollziehbare enge Matrixevidenz ersetzt.
- Die in `399a463` geprüften Codefixes bleiben unverändert. Ebenso unverändert
  bleiben die Doku-/Review-Commits `479a7a7` und `32b0150`; Step 028 ergänzt
  ausschließlich neue Evidenz und korrigiert die falsche Aussage im
  Step-027-Result.
- Erlaubte Änderungen: `step-027/step-result.md`, `step-028/step-plan.md`, das
  neue `step-028/step-result.md`, `task-state.md` und `low-cost-handoff.md`.
  `TestResults/step028-*` sind lokale, nicht zu commitende Evidenzartefakte.
- Verboten: C#-, Projekt-, Ledger-, Roadmap-, CodeMap-, Tech-Debt- oder
  öffentliche Dokuänderungen; Tests löschen, umbenennen, umkategorisieren oder
  Assertions/Filter zur Fallzahlkompensation ändern.

## Aktueller Projektzustand

- `step027-fast-matrix.trx` enthält 318 und
  `step027-integration-matrix.trx` 112 Tests, weil Namespace-Breitfilter auch
  fremde MCP-Kohorten erfasst haben. Diese TRX bleiben historische
  Fehlevidenz und werden nicht überschrieben.
- Die Step-025/026-Inventur und das Ledger ergeben 66 historische Fast- und
  55 historische Integrationverträge. Fast ergänzt Kategorieguard und zwei
  Dependencyguards: **69**. Integration ergänzt den Budgetvertrag, Handshake,
  drei File-Discovery-Verträge, Kategorieguard, Process-Callsiteguard und den
  Fixture-Selbstvertrag; der neue Cleanup-Ursachevertrag aus Step 027 erhöht
  die Step-026-Summe 63 auf **64**.
- Die vorhandenen Einzel-TRX belegen bereits Cleanup 1/1, Command 13/13 sowie
  die Kategorieguards. Weil die 69er-/64er-Matrix diese Verträge selbst
  enthält, werden die Einzelgates nur bei einer konkret fehlenden oder roten
  Matrixzeile diagnostisch wiederholt.
- Seit `399a463` ist Produkt-/Testcode unverändert. Deshalb kein Build, kein
  Drift-Audit und keine Vollprofile.

## Exakte Klassenfilter und Sollzerlegung

### Fast: 69

| Klassenfilter | Fälle |
|---|---:|
| `FastTestsDependencyGuardTests` | 2 |
| `TestCategoryProfileGuardTests` | 1 |
| `McpCallLogTests` | 14 |
| `McpCodeGraphServerConstructorTests` | 2 |
| `McpServerCommandCacheBypassTests` | 1 |
| `McpServerCommandCallLogTests` | 9 |
| `McpServerCommandLoadingStateTests` | 3 |
| `McpServerCommandTests` | 10 |
| `McpServerOptionsBuilderTests` | 9 |
| `McpServerOptionsFactoryTests` | 1 |
| `McpTestClientRetryOptionsTests` | 2 |
| `OverviewResourceRegistrationTests` | 5 |
| `SymbolGraphToolRegistrationsTests` | 1 |
| `GetImpactToolTests` | 9 |
| **Gesamt** | **69** |

### Integration: 64

| Klassenfilter | Fälle |
|---|---:|
| `McpProcessArchitectureGuardTests` | 1 |
| `TestCategoryProfileGuardTests` | 1 |
| `McpCodeGraphServerFileDiscoveryTests` | 3 |
| `McpHandshakeToolRegistrationTests` | 1 |
| `McpServerAllToolsE2ETests` | 24 |
| `McpServerCommandAmbiguityE2ETests` | 1 |
| `McpServerCommandContractTests` | 13 |
| `McpServerCommandErrorHandlingTests` | 2 |
| `McpServerCommandFindReferencesTests` | 1 |
| `McpServerCommandFindSymbolTests` | 1 |
| `McpServerCommandGetImpactTests` | 2 |
| `McpServerCommandJsonRpcFramingTests` | 3 |
| `McpServerCommandMissHintTests` | 1 |
| `McpServerCommandStalenessTests` | 1 |
| `McpTestClientRetryTests` | 2 |
| `GetImpactToolIntegrationTests` | 6 |
| `SymbolGraphCatalogFixtureTests` | 1 |
| **Gesamt** | **64** |

## Exakte erwartete FQN-Manifeste

Die folgenden sortierten Listen sind die Sollmenge. Sie wurden aus der
Step-025/026-Klassenzuordnung, der aktuellen Zielstruktur und den vorhandenen
Step-027-TRX rekonstruiert. Der Coder legt sie vor dem Lauf als
`TestResults/step028-expected-fast.txt` und
`TestResults/step028-expected-integration.txt` ab, eine FQN pro Zeile, ohne
Backticks oder Aufzählungszeichen.

### Fast-FQNs (69)

```text
AiNetLinter.FastTests.Architecture.FastTestsDependencyGuardTests.FastTestsAssembly_DoesNotReferenceDeniedInfrastructure
AiNetLinter.FastTests.Architecture.FastTestsDependencyGuardTests.TestKitAssembly_DoesNotReferenceDeniedInfrastructure
AiNetLinter.FastTests.Architecture.TestCategoryProfileGuardTests.EveryTestClass_HasExactlyOneValidCategoryTrait
AiNetLinter.FastTests.Mcp.McpCallLogTests.Dispose_NoRecords_DeletesLogFile
AiNetLinter.FastTests.Mcp.McpCallLogTests.ExecuteCallAsync_OperationCanceled_NotLoggedAndRethrown
AiNetLinter.FastTests.Mcp.McpCallLogTests.ExecuteCallAsync_ParallelThrowingCallsDoNotInterleaveJsonLines
AiNetLinter.FastTests.Mcp.McpCallLogTests.ExecuteCallAsync_SuccessCall_WritesCallEntryAndReturnsResult
AiNetLinter.FastTests.Mcp.McpCallLogTests.ExecuteCallAsync_ThrowingCall_WritesErrorEntryAndRethrows
AiNetLinter.FastTests.Mcp.McpCallLogTests.RecordEnd_EmptyResult_SetsEmptyTrue
AiNetLinter.FastTests.Mcp.McpCallLogTests.RecordEnd_TruncatedResult_SetsTruncatedTrue
AiNetLinter.FastTests.Mcp.McpCallLogTests.RecordError_AfterRecordEnd_PreservesOrderInJsonl
AiNetLinter.FastTests.Mcp.McpCallLogTests.RecordError_BasicException_WritesJsonLineWithAllFields
AiNetLinter.FastTests.Mcp.McpCallLogTests.RecordError_BeforeRecordEnd_PreservesOrderInJsonl
AiNetLinter.FastTests.Mcp.McpCallLogTests.RecordError_ParallelCallsDoNotInterleaveJsonLines
AiNetLinter.FastTests.Mcp.McpCallLogTests.RecordError_StackTraceExceeds4KB_TruncatesToCap
AiNetLinter.FastTests.Mcp.McpCallLogTests.RecordStart_LongArgs_TruncatedToTwoHundredPlusEllipsis
AiNetLinter.FastTests.Mcp.McpCallLogTests.RecordStart_ThenEnd_WritesJsonLineWithAllFields
AiNetLinter.FastTests.Mcp.McpCodeGraphServerConstructorTests.Constructor_AcceptsNullOptions_ThrowsArgumentNullException
AiNetLinter.FastTests.Mcp.McpCodeGraphServerConstructorTests.Constructor_TakesExactlyOneParameter_OfTypeMcpCodeGraphServerOptions
AiNetLinter.FastTests.Mcp.McpServerCommandCacheBypassTests.McpCodeGraphServer_HasNoAnalysisCacheManagerReference
AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests.BuildDefaultLogPath_DateIsLocal
AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests.BuildDefaultLogPath_WithSolution_IncludesSolutionName
AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests.ResolveMcpLogPath_AbsolutePath_ReturnsAsIs
AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests.ResolveMcpLogPath_RelativePath_ResolvedAgainstSolutionDirectory
AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests.TryCreateCallLog_AbsolutePath_CreatesLogFileAtGivenPath
AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests.TryCreateCallLog_PathNotSet_ReturnsNull
AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests.TryCreateCallLog_RelativePath_CreatesLogFileRelativeToSolutionDir
AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests.TryCreateCallLog_WhitespacePath_CreatesDefaultLog
AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests.TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull
AiNetLinter.FastTests.Mcp.McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately
AiNetLinter.FastTests.Mcp.McpServerCommandLoadingStateTests.RunAsync_LoadFuncCompletes_ServerLeavesLoadingState
AiNetLinter.FastTests.Mcp.McpServerCommandLoadingStateTests.RunAsync_LoadFuncStillRunning_ToolReturnsLoadingInfo
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveConfig_ConfigWithCustomMaxLineCount_UsesConfigFromArgs
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveConfig_NoConfigPath_ReturnsDefaultConfig
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveMaxLineCount_ConfigWithCustomMaxLineCount_ReturnsConfiguredValue
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveMaxLineCount_NoConfigPath_ReturnsMetricsConfigDefault
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveSolutionPathOrError_MissingPath_UsesCurrentDirectory
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveSolutionPathOrError_NoSolutionFound_ReportsResourceNotFound
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveSolutionPathOrError_SingleCandidate_ReturnsIt
AiNetLinter.FastTests.Mcp.McpServerCommandTests.ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution
AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests.Build_DefaultInstructions_IsEmpty
AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests.Build_DefaultName_UsesAinetlinter
AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests.Build_DefaultVersion_UsesAssemblyVersion
AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests.Build_WithoutResourceCollection_ProvidesEmptyCollection
AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests.Build_WithoutToolCollection_ProvidesEmptyCollection
AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests.Build_WithResourceCollection_PropagatesToServerOptions
AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests.Build_WithServerInstructions_PropagatesToServerOptions
AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests.Build_WithServerName_PropagatesToServerOptions
AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests.Build_WithServerVersion_PropagatesToServerOptions
AiNetLinter.FastTests.Mcp.McpServerOptionsFactoryTests.Create_ServerInstructionsContainsScopeHint
AiNetLinter.FastTests.Mcp.McpTestClientRetryOptionsTests.McpTestClientRetryOptions_DefaultValues_AreSane
AiNetLinter.FastTests.Mcp.McpTestClientRetryOptionsTests.McpTestClientRetryOptions_OverrideAllProperties
AiNetLinter.FastTests.Mcp.OverviewResourceRegistrationTests.BuildOverviewText_DefaultConfig_MentionsDefaultRulesNotProjectConfig
AiNetLinter.FastTests.Mcp.OverviewResourceRegistrationTests.BuildOverviewText_ExplicitConfig_ShowsResolvedConfigPath
AiNetLinter.FastTests.Mcp.OverviewResourceRegistrationTests.BuildOverviewText_ListsAllEighteenTools
AiNetLinter.FastTests.Mcp.OverviewResourceRegistrationTests.BuildOverviewText_LoadingState_ShowsLoadingPlaceholder
AiNetLinter.FastTests.Mcp.OverviewResourceRegistrationTests.ToolSummaries_MatchesRegisteredToolNames
AiNetLinter.FastTests.Mcp.SymbolGraphToolRegistrationsTests.ToolDescriptions_FindReferencesAndGetImpact_MentionNodeHardCap
AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests.ExecuteAsync_BothGitRefAndSymbolGiven_ReturnsRecoverableInvalidArgument
AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests.ExecuteAsync_NoGitRepository_ReturnsEmptyResultNotCrash
AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests.ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode
AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests.ExecuteAsync_StableSymbolIdentifierGiven_ReturnsCallSites
AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests.ExecuteAsync_SymbolIdentifierGiven_DelegatesToResolveSymbolAndReturnsCallSites
AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests.ExecuteAsync_SymbolIdentifierGivenDepth1_StructuredContentDeserializesToCallSiteEntries
AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests.ExecuteAsync_SymbolIdentifierWithDepth2_StillReturnsCallSite
AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests.ExecuteAsync_SymbolIdentifierWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine
AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests.ExecuteAsync_UnknownSymbolIdentifier_ReturnsRecoverableSymbolNotFound
```

### Integration-FQNs (64)

```text
AiNetLinter.IntegrationTests.Architecture.McpProcessArchitectureGuardTests.RunnerAndProcessCallsites_StayWithinMcpOwners
AiNetLinter.IntegrationTests.Architecture.TestCategoryProfileGuardTests.EveryTestClass_HasExactlyOneValidCategoryTrait
AiNetLinter.IntegrationTests.Mcp.McpCodeGraphServerFileDiscoveryTests.GetCurrentSolution_FileDeletedAfterStart_RemovedFromSolution
AiNetLinter.IntegrationTests.Mcp.McpCodeGraphServerFileDiscoveryTests.GetCurrentSolution_GeneratedFile_NotAdded
AiNetLinter.IntegrationTests.Mcp.McpCodeGraphServerFileDiscoveryTests.GetCurrentSolution_NewFileAddedAfterStart_AppearsInSolution
AiNetLinter.IntegrationTests.Mcp.McpHandshakeToolRegistrationTests.ConnectAndListTools_AgainstMiniFixture_RegistersExpectedTools
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.FindDuplicates_RefactoringDriftModeWithoutHelperSymbol_ReturnsRecoverableInvalidArgument
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.FindReferences_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgument
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.FindReferences_UnknownSymbol_ReturnsRecoverableSymbolNotFound
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.FindSymbol_KindFilter_ReturnsFilteredSymbolsOnly
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.FindSymbol_MissingNamePattern_ReturnsRecoverableInvalidArgument
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.FindSymbol_ZeroResults_ReturnsNoMatchMessage
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetCallTree_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgument
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetFileSkeleton_MissingFilePath_ReturnsRecoverableInvalidArgument
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetFileSkeleton_NonCsFile_ReturnsRecoverableResourceNotFound
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetFileSkeleton_NonExistentFile_ReturnsErrorOrNotFound
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetHotspots_ValidWorkspace_ReturnsHotspotSummary
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetImpact_BothArgumentsProvided_ReturnsRecoverableInvalidArgumentMessage
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetIndexScope_ValidWorkspace_ReturnsFileTypeBreakdown
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetSymbolBody_MissingIdentifier_ReturnsRecoverableInvalidArgument
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetTypeHierarchy_MissingTypeIdentifier_ReturnsRecoverableInvalidArgument
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetTypeHierarchy_UnknownType_ReturnsRecoverableSymbolNotFound
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetTypeHierarchy_ValidType_ReturnsHierarchyInfo
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetTypeHierarchy_WrongParameterName_ReturnsRecoverableInvalidArgumentInsteadOfCrashing
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.GetViolations_WithScopeFilter_FiltersResults
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.MetricsTree_MissingMode_ReturnsRecoverableInvalidArgument
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.SearchPattern_MissingPattern_ReturnsRecoverableInvalidArgument
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.SearchPattern_PlainTextSearch_ReturnsMatches
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.SearchPattern_RegexSearch_ReturnsMatches
AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests.UnknownTool_Call_ThrowsMcpProtocolException
AiNetLinter.IntegrationTests.Mcp.McpServerCommandAmbiguityE2ETests.RunAsync_DirectoryWithTwoSlnx_AbortsWithAmbiguousSolutionError
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_FindReferencesReturnsCallSite
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_FindSymbolReturnsMatch
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_SearchPatternReturnsExpectedHit
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.RunAsync_ValidFixture_ServerRespondsWithEighteenTools
AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests.TryLoadSolutionAsync_BrokenSlnx_LogsWarningWithoutThrowing
AiNetLinter.IntegrationTests.Mcp.McpServerCommandErrorHandlingTests.RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError
AiNetLinter.IntegrationTests.Mcp.McpServerCommandErrorHandlingTests.RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection
AiNetLinter.IntegrationTests.Mcp.McpServerCommandFindReferencesTests.RunAsync_ValidFixture_FindReferencesWithMaxResultsTruncates
AiNetLinter.IntegrationTests.Mcp.McpServerCommandFindSymbolTests.RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates
AiNetLinter.IntegrationTests.Mcp.McpServerCommandGetImpactTests.RunAsync_ValidFixture_GetImpactGitBranchWithMaxResultsTruncates
AiNetLinter.IntegrationTests.Mcp.McpServerCommandGetImpactTests.RunAsync_ValidFixture_GetImpactSymbolBranchWithMaxResultsTruncates
AiNetLinter.IntegrationTests.Mcp.McpServerCommandJsonRpcFramingTests.HandshakeOnly_AllStdoutLinesAreValidJsonRpcFrames
AiNetLinter.IntegrationTests.Mcp.McpServerCommandJsonRpcFramingTests.Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine
AiNetLinter.IntegrationTests.Mcp.McpServerCommandJsonRpcFramingTests.ToolCallSequence_AllStdoutLinesAreValidJsonRpcFrames
AiNetLinter.IntegrationTests.Mcp.McpServerCommandMissHintTests.RunAsync_NonCsOnlyMatch_ReturnsExplicitMissHint
AiNetLinter.IntegrationTests.Mcp.McpServerCommandStalenessTests.RunAsync_FileChangeBetweenCalls_ReflectedInSecondCall
AiNetLinter.IntegrationTests.Mcp.McpTestClientRetryTests.AcquireAsync_TwoLeasesBlockThirdUntilDisposal
AiNetLinter.IntegrationTests.Mcp.McpTestClientRetryTests.ConnectAsync_AllRetriesExhausted_ThrowsInvalidOperationException
AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph.GetImpactToolIntegrationTests.ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning
AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph.GetImpactToolIntegrationTests.ExecuteAsync_GitRefUncommittedChange_StructuredContentDeserializesToCallSiteEntries
AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph.GetImpactToolIntegrationTests.ExecuteAsync_GitRefUncommittedWithManyCallSites_TruncatesAtMaxResults
AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph.GetImpactToolIntegrationTests.ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite
AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph.GetImpactToolIntegrationTests.ExecuteAsync_UnresolvableGitRef_ReturnsRecoverableAnalysisFailedNotEmptyResult
AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph.GetImpactToolIntegrationTests.GitImpactMiniFixtureWorkspace_DisposeTwice_DeletesRootWithoutThrowing
AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraphCatalogFixtureTests.ReadOnlyServers_DisposeWithoutAffectingParallelOrLaterFixtureReaders
```

## Mechanische PowerShell-Ausführung

Alle Befehle aus dem Repository-Root. Die beiden Klassenarrays werden exakt
aus den Tabellen oben übernommen, jeweils mit dem vollständigen Namespace.
Die Filter verwenden den Klassennamen plus Punkt, damit ähnlich benannte
Klassen nicht versehentlich mitlaufen.

```powershell
$planText = Get-Content tasks/speedup-tests/step-028/step-plan.md -Raw
$manifestBlocks = [regex]::Matches($planText, '(?ms)^```text\r?\n(.*?)\r?\n```')
if ($manifestBlocks.Count -ne 2) { throw 'Erwartet exakt zwei FQN-Manifestbloecke.' }
($manifestBlocks[0].Groups[1].Value -split '\r?\n' | Where-Object { $_ } |
  Sort-Object -Unique) | Set-Content TestResults/step028-expected-fast.txt
($manifestBlocks[1].Groups[1].Value -split '\r?\n' | Where-Object { $_ } |
  Sort-Object -Unique) | Set-Content TestResults/step028-expected-integration.txt

$fastClasses = @(
  'AiNetLinter.FastTests.Architecture.FastTestsDependencyGuardTests',
  'AiNetLinter.FastTests.Architecture.TestCategoryProfileGuardTests',
  'AiNetLinter.FastTests.Mcp.McpCallLogTests',
  'AiNetLinter.FastTests.Mcp.McpCodeGraphServerConstructorTests',
  'AiNetLinter.FastTests.Mcp.McpServerCommandCacheBypassTests',
  'AiNetLinter.FastTests.Mcp.McpServerCommandCallLogTests',
  'AiNetLinter.FastTests.Mcp.McpServerCommandLoadingStateTests',
  'AiNetLinter.FastTests.Mcp.McpServerCommandTests',
  'AiNetLinter.FastTests.Mcp.McpServerOptionsBuilderTests',
  'AiNetLinter.FastTests.Mcp.McpServerOptionsFactoryTests',
  'AiNetLinter.FastTests.Mcp.McpTestClientRetryOptionsTests',
  'AiNetLinter.FastTests.Mcp.OverviewResourceRegistrationTests',
  'AiNetLinter.FastTests.Mcp.SymbolGraphToolRegistrationsTests',
  'AiNetLinter.FastTests.Mcp.Tools.SymbolGraph.GetImpactToolTests'
)
$integrationClasses = @(
  'AiNetLinter.IntegrationTests.Architecture.McpProcessArchitectureGuardTests',
  'AiNetLinter.IntegrationTests.Architecture.TestCategoryProfileGuardTests',
  'AiNetLinter.IntegrationTests.Mcp.McpCodeGraphServerFileDiscoveryTests',
  'AiNetLinter.IntegrationTests.Mcp.McpHandshakeToolRegistrationTests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerAllToolsE2ETests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerCommandAmbiguityE2ETests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerCommandErrorHandlingTests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerCommandFindReferencesTests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerCommandFindSymbolTests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerCommandGetImpactTests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerCommandJsonRpcFramingTests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerCommandMissHintTests',
  'AiNetLinter.IntegrationTests.Mcp.McpServerCommandStalenessTests',
  'AiNetLinter.IntegrationTests.Mcp.McpTestClientRetryTests',
  'AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph.GetImpactToolIntegrationTests',
  'AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraphCatalogFixtureTests'
)
$fastFilter = ($fastClasses | ForEach-Object { "FullyQualifiedName~$_." }) -join '|'
$integrationFilter = ($integrationClasses | ForEach-Object { "FullyQualifiedName~$_." }) -join '|'

dotnet test src/AiNetLinter.FastTests --no-build --no-restore --list-tests --filter $fastFilter |
  Tee-Object TestResults/step028-fast-discovery.txt
dotnet test src/AiNetLinter.IntegrationTests --no-build --no-restore --list-tests --filter $integrationFilter |
  Tee-Object TestResults/step028-integration-discovery.txt
```

Discovery-Abgleich vor jeder Testausführung:

```powershell
$expectedFast = Get-Content TestResults/step028-expected-fast.txt | Sort-Object -Unique
$expectedIntegration = Get-Content TestResults/step028-expected-integration.txt | Sort-Object -Unique
$discoveredFast = Get-Content TestResults/step028-fast-discovery.txt |
  ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^AiNetLinter\.FastTests\.' } |
  Sort-Object -Unique
$discoveredIntegration = Get-Content TestResults/step028-integration-discovery.txt |
  ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^AiNetLinter\.IntegrationTests\.' } |
  Sort-Object -Unique
Compare-Object $expectedFast $discoveredFast |
  Out-File TestResults/step028-fast-discovery.diff.txt
Compare-Object $expectedIntegration $discoveredIntegration |
  Out-File TestResults/step028-integration-discovery.diff.txt
"Fast expected=$($expectedFast.Count) discovered=$($discoveredFast.Count)"
"Integration expected=$($expectedIntegration.Count) discovered=$($discoveredIntegration.Count)"
if ($expectedFast.Count -ne 69 -or $discoveredFast.Count -ne 69 -or
    (Get-Item TestResults/step028-fast-discovery.diff.txt).Length -ne 0) {
  throw 'Fast-Discovery weicht vom 69er-Manifest ab; Abweichungspfad verwenden.'
}
if ($expectedIntegration.Count -ne 64 -or $discoveredIntegration.Count -ne 64 -or
    (Get-Item TestResults/step028-integration-discovery.diff.txt).Length -ne 0) {
  throw 'Integration-Discovery weicht vom 64er-Manifest ab; Abweichungspfad verwenden.'
}
```

Nur bei leeren Diff-Dateien und 69/64 werden die zwei engen Läufe genau
einmal gestartet:

```powershell
dotnet test src/AiNetLinter.FastTests --no-build --no-restore --filter $fastFilter `
  --logger "trx;LogFileName=step028-fast-matrix.trx"
dotnet test src/AiNetLinter.IntegrationTests --no-build --no-restore --filter $integrationFilter `
  --logger "trx;LogFileName=step028-integration-matrix.trx"
```

Aus beiden TRX die tatsächlich ausgeführten FQNs extrahieren und erneut gegen
das Soll diffen:

```powershell
[xml]$fastTrx = Get-Content TestResults/step028-fast-matrix.trx
[xml]$integrationTrx = Get-Content TestResults/step028-integration-matrix.trx
$actualFast = $fastTrx.TestRun.TestDefinitions.UnitTest | ForEach-Object {
  $_.TestMethod.className + '.' + $_.TestMethod.name
} | Sort-Object -Unique
$actualIntegration = $integrationTrx.TestRun.TestDefinitions.UnitTest | ForEach-Object {
  $_.TestMethod.className + '.' + $_.TestMethod.name
} | Sort-Object -Unique
Compare-Object $expectedFast $actualFast | Out-File TestResults/step028-fast-trx.diff.txt
Compare-Object $expectedIntegration $actualIntegration |
  Out-File TestResults/step028-integration-trx.diff.txt
$fastCounters = $fastTrx.TestRun.ResultSummary.Counters
$integrationCounters = $integrationTrx.TestRun.ResultSummary.Counters
"Fast total=$($fastCounters.total) passed=$($fastCounters.passed) FQNs=$($actualFast.Count)"
"Integration total=$($integrationCounters.total) passed=$($integrationCounters.passed) FQNs=$($actualIntegration.Count)"
if ($fastCounters.total -ne 69 -or $fastCounters.passed -ne 69 -or $actualFast.Count -ne 69 -or
    (Get-Item TestResults/step028-fast-trx.diff.txt).Length -ne 0) {
  throw 'Fast-TRX ist nicht exakt 69/69/manifestscharf.'
}
if ($integrationCounters.total -ne 64 -or $integrationCounters.passed -ne 64 -or
    $actualIntegration.Count -ne 64 -or
    (Get-Item TestResults/step028-integration-trx.diff.txt).Length -ne 0) {
  throw 'Integration-TRX ist nicht exakt 64/64/manifestscharf.'
}
```

## Deterministischer Abweichungspfad

Falls Discovery nicht 69/64 oder ein Diff nicht leer ist: **keinen Testlauf
starten und keine Filter verbreitern**. Im Step-028-Result je FQN die
`Compare-Object`-Richtung dokumentieren (`<=` nur Soll, `=>` nur Ist) und gegen
aktuelle Quelldatei sowie `399a463`/`06fdc20` prüfen.

Eine reine Doku-Korrektur der Sollzahl/-liste ist nur erlaubt, wenn alle
folgenden Bedingungen erfüllt sind:

1. Der Ist-FQN liegt in einer der oben erlaubten Klassen oder ein fehlender
   Soll-FQN ist dort nachweislich umbenannt/konsolidiert.
2. Der Unterschied bestand bereits vor Step 028; es gibt keine Codeänderung.
3. Die Vertragszuordnung aus Step 025/026 bleibt vollständig; insbesondere
   66 Fast/55 Integration historisch und der Step-027-Ursachevertrag gehen
   nicht verloren.
4. Die sachliche Ursache, alte und korrigierte Zahl sowie vollständige
   Added-/Removed-FQN-Liste werden in `step-027/step-result.md` und
   `step-028/step-result.md` dokumentiert. Danach Discovery mit dem
   korrigierten Manifest genau einmal neu abgleichen und erst dann laufen.

Nicht erlaubt sind eine Doku-Korrektur für einen FQN außerhalb der
Klassenallowlist, einen verlorenen Vertrag, eine Kategorieänderung, eine
Testlöschung oder eine rote Discovery/Testausführung. In diesen Fällen Step
028 auf `blocked` setzen und stoppen.

## Result-Korrektur und optionale Diagnosegates

- `step-027/step-result.md`: „Keine — Plan 1:1 umgesetzt“ ersetzen. Die
  318/112-Läufe ausdrücklich als zu breit kennzeichnen und auf die neuen
  `step028-*-matrix.trx`, Discoverydateien, FQN-Diffs und realen Zahlen
  verweisen. Historische TRX nicht löschen oder umbenennen.
- `step-028/step-result.md`: exakte Commands, Discovery- und TRX-Zahlen,
  vier leere Diffdateien bzw. den erlaubten begründeten Diff, Exitcodes und
  `git diff --check` dokumentieren. Kein Code-Commit behaupten.
- Das 13er-Command-Gate und beide Kategorieguards sind Teil der 64er bzw.
  69er Matrix. Bei 69/69 und 64/64 **nicht separat wiederholen**. Nur wenn die
  Matrix-TRX genau dort einen Fehler meldet, einmal mit den bereits in Step
  027 dokumentierten engsten Filtern und neuen Namen
  `step028-diagnostic-command.trx`, `step028-diagnostic-fast-category.trx`
  oder `step028-diagnostic-integration-category.trx` ausführen. Ein roter
  Diagnoselauf blockiert; kein zweiter Versuch.

```powershell
dotnet test src/AiNetLinter.IntegrationTests --no-build --no-restore `
  --filter "FullyQualifiedName~AiNetLinter.IntegrationTests.Mcp.McpServerCommandContractTests." `
  --logger "trx;LogFileName=step028-diagnostic-command.trx"
dotnet test src/AiNetLinter.FastTests --no-build --no-restore `
  --filter "FullyQualifiedName~AiNetLinter.FastTests.Architecture.TestCategoryProfileGuardTests." `
  --logger "trx;LogFileName=step028-diagnostic-fast-category.trx"
dotnet test src/AiNetLinter.IntegrationTests --no-build --no-restore `
  --filter "FullyQualifiedName~AiNetLinter.IntegrationTests.Architecture.TestCategoryProfileGuardTests." `
  --logger "trx;LogFileName=step028-diagnostic-integration-category.trx"
```

- Kein `dotnet build`: seit `399a463` ist Code unverändert. Kein Volltest,
  Legacy-, Dogfood-, Performance- oder Stressprofil.

## Definition of Done und Stopkriterien

- Discovery und TRX stimmen FQN-genau mit 69 Fast/64 Integration oder einer
  vollständig begründeten, erlaubten Doku-Korrektur überein; alle ausgeführten
  Tests sind grün.
- `step-027/step-result.md` bezeichnet 318/112 nicht mehr als planmäßige
  Matrixevidenz und enthält den FQN-Abgleich.
- Ausschließlich erlaubte Task-Artefakte sind geändert; `git --no-pager diff
  --check` ist grün. Kein Commit, kein Push.
- Sofort stoppen bei Codeänderungsbedarf, Test-/Kategorie-/Assertionänderung,
  FQN außerhalb der Allowlist, verlorenem historischen Vertrag, rotem Lauf,
  breitem Ersatzfilter oder einem Versuch, 69/64 durch fremde Tests zu
  kompensieren.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln`
  — PowerShell und eigene TRX pro Lauf.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  keine Tests/Assertions zur Symptomkorrektur abschwächen.
