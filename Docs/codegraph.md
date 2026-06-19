# Codegraph (Auto-generiert durch AiNetLinter 1.0.50)
Produktionscode · 148 Typen · 13 Namespaces

## AiNetLinter (2)
- FootprintExecutor → LinterArgs
- Program → CliParsedArgs, LinterArgs, LinterConfig, PlaybookOptions, SourceFileCatalog

## AiNetLinter.Baseline (12)
- BaselineComparer → BaselineComparisonResult, BaselineFile
- BaselineComparisonResult [record] → BaselineComparisonResult
- BaselineFile [record] → BaselineFile
- BaselineFileDto
- BaselineJsonSerializer → BaselineFile
- BaselineReader → BaselineFile
- BaselineViolationFilter
- BaselineWriter
- CatalogDocumentWorkItem [record] → CatalogDocumentWorkItem
- FileChecksumCalculator
- SourceFileCatalog → SourceFileCatalog, SourceFileEntry
- SourceFileEntry [record] → SourceFileEntry

## AiNetLinter.Cache (10)
- AnalysisCacheEntry [record] → AnalysisCacheEntry, TestSignalsDto
- AnalysisCacheFile [record] → AnalysisCacheFile
- AnalysisCacheManager → AnalysisCacheEntry, AnalysisCacheFile, AnalysisCacheManager
- BuildEntryParams [record] → BuildEntryParams, LinterAnalyzer, TestSignalsDto
- CacheEntryMapper → AnalysisCacheEntry, AnalysisState, BuildEntryParams, ClassInfo, ClassInfoDto, PartialClassPart, PartialPartDto, RuleViolation, RuleViolationDto
- ClassInfoDto [record] → ClassInfoDto
- FootprintDetailDto [record] → FootprintDetailDto
- PartialPartDto [record] → PartialPartDto
- RuleViolationDto [record] → RuleViolationDto
- TestSignalsDto [record] → TestSignalsDto

## AiNetLinter.Cli (13)
- CliBaselineOptions [record] → CliBaselineOptions
- CliCommandBuilder → CliOptions, CliParsedArgs
- CliImpactOptions [record] → CliImpactOptions
- CliMaintenanceOptions [record] → CliMaintenanceOptions
- CliOptionFactory
- CliOptions [record] → CliOptions
- CliOutputOptions [record] → CliOutputOptions
- CliParsedArgs [record] → CliBaselineOptions, CliImpactOptions, CliMaintenanceOptions, CliOutputOptions, CliParsedArgs, CliScopeOptions
- CliScopeOptions [record] → CliScopeOptions
- DebtReportExecutor → LinterArgs
- ImpactExecutor → LinterArgs
- LinterArgs
- MaintenanceExecutor → LinterArgs

## AiNetLinter.Configuration (19)
- FileFilterEvaluator → FileFiltersConfig
- FileFiltersConfig [record] → FileFiltersConfig
- GlobalConfig [record] → GlobalConfig, GlobalConfigOverride
- GlobalConfigOverride [record] → GlobalConfigOverride
- LinterConfig [record] → FileFiltersConfig, GlobalConfig, LinterConfig, MetricsConfig, TestSentinelConfig, UiSeparationConfig
- LinterConfigLoader → LinterConfig
- LinterConfigNormalizer → LinterConfig
- LinterConfigSyncer → LinterConfig
- MetricsConfig [record] → MetricsConfig, MetricsConfigOverride
- MetricsConfigOverride [record] → MetricsConfigOverride
- NamespaceRule [record] → NamespaceRule
- ProjectConfigResolver → LinterConfig, ProjectOverrideEntry
- ProjectOverrideEntry [record] → GlobalConfigOverride, MetricsConfigOverride, ProjectOverrideEntry, TestSentinelConfigOverride, UiSeparationConfigOverride
- RuleMetadataEntry [record] → RuleMetadataEntry
- RuleMetadataRegistry → LinterConfig, RuleMetadataEntry
- TestSentinelConfig [record] → TestSentinelConfig, TestSentinelConfigOverride
- TestSentinelConfigOverride [record] → TestSentinelConfigOverride
- UiSeparationConfig [record] → UiSeparationConfig, UiSeparationConfigOverride
- UiSeparationConfigOverride [record] → UiSeparationConfigOverride

## AiNetLinter.Core (31)
- AnalysisState [record] → AnalysisState, TestCoverageIndex
- AnalyzerArgs [record] → AnalyzerArgs, LinterConfig
- CacheDestination [record] → AnalysisCacheManager, CacheDestination
- CodegraphGenerator → TypeInfo
- CursorRulesGenerator → GlobalConfig, LinterConfig, ProjectOverrideEntry
- DiffImpactAnalyzer
- DocumentContext [record] → DocumentContext, LinterConfig
- FixContext [record] → FixContext
- FixOptions [record] → FixOptions
- LinterAnalyzer → AnalyzerArgs, CheckerContext, LinterConfig
- LinterAutoFixer → FixContext, FixOptions
- LinterEngine → AnalysisCacheManager, AnalysisState, CacheDestination, CatalogDocumentWorkItem, DocumentContext, LinterAnalyzer, LinterConfig, SourceFileCatalog, TestCoverageIndex, TestSignalsDto
- PartialClassLineAggregator → LinterConfig, RuleViolation
- PartialClassPart [record] → PartialClassPart
- PlaybookBuildContext [record] → LinterConfig, PlaybookBuildContext, PlaybookStats
- PlaybookDocInfo [record] → PlaybookDocInfo
- PlaybookDocScanResult [record] → PlaybookDocScanResult
- PlaybookOptions [record] → LinterConfig, PlaybookOptions
- PlaybookStats [record] → PlaybookStats
- PlaybookSyntaxWalker
- PostAnalysisChecks → AnalysisState, ClassInfo, LinterConfig, TestSentinelConfig, TestSentinelContext
- RepoPlaybookGenerator → LinterConfig, PlaybookBuildContext, PlaybookOptions, RuleViolation
- RuleMetadata [record] → RuleMetadata
- RuleRegistry → RuleMetadata
- TestCoverageCollector [partial] → TestCoverageIndex, TestSentinelConfig
- TestCoverageIndex
- TestCoverageResolver → TestCoverageIndex, TestSentinelConfig
- TestProjectDetector
- TestSentinelContext [record] → TestCoverageIndex, TestSentinelContext
- TypeInfo [record] → TypeInfo
- UiFileSeparationChecker → AnalysisState, LinterConfig, RuleViolation, UiSeparationConfig

## AiNetLinter.Core.Checkers (23)
- BoolParameterChecker → CheckerContext
- CheckerContext → LinterConfig, RuleViolation
- ClassInfoCollector → CheckerContext
- ComplexityCheck [record] → ComplexityCheck
- ComplexityChecker → CheckerContext, ComplexityCheck
- ControlFlowChecker → CheckerContext
- DynamicTypeChecker → CheckerContext
- GeneratedCodeDetector → CheckerContext
- ImmutabilityChecker → CheckerContext
- InheritanceDepthChecker → CheckerContext
- MinimalApiChecker → CheckerContext
- NamespaceCouplingChecker → CheckerContext
- NamingChecker → CheckerContext
- NestedTypesChecker → CheckerContext
- PhantomDependencyChecker → CheckerContext
- PublicMembersChecker → CheckerContext
- ScopeChecker → CheckerContext
- SealedClassChecker → CheckerContext
- StateChecker → CheckerContext
- SyntaxHelper
- TestAttributeDetector → CheckerContext
- ValueObjectChecker → CheckerContext
- WpfSeparationChecker → CheckerContext

## AiNetLinter.Diagnostics (6)
- DocumentPerformanceEntry [record] → DocumentPerformanceEntry
- PerformanceProfiler → PerformanceProfiler, PhaseDurationSnapshot, ProfilerContext, ProfilerJsonReport
- PhaseDurationSnapshot [record] → PhaseDurationSnapshot
- ProfilerContext [record] → ProfilerContext
- ProfilerJsonReport [record] → ProfilerJsonReport, ProfilerSummary
- ProfilerSummary [record] → ProfilerSummary

## AiNetLinter.Metrics (7)
- AIContextFootprintCalculator
- CognitiveComplexityGuidance
- CognitiveComplexityWalker
- ComplexityCalculator
- CyclomaticComplexityWalker
- MethodLineCounter
- SwitchDispatcherDetector

## AiNetLinter.Models (2)
- ClassInfo [record] → ClassInfo
- RuleViolation [record] → RuleViolation

## AiNetLinter.Output (11)
- DebtReportBuilder
- FileViolationCount [record] → FileViolationCount
- FolderCount [record] → FolderCount
- LinterLogger → BaselineComparisonResult
- OutputRootResolver
- PathNormalizer
- RuleLegendEntry [record] → RuleLegendEntry
- RuleLegendRegistry → RuleLegendEntry
- RuleViolationCount [record] → RuleViolationCount
- ViolationMarkdownFormatter → LinterConfig, RuleViolation
- ViolationSummaryBuilder → LinterConfig

## AiNetLinter.Scope (4)
- DisableAllDetector [partial]
- GitChangedFilesResolver
- ViolationScopeFilter → ViolationScopeOptions
- ViolationScopeOptions [record] → ViolationScopeOptions

## AiNetLinter.Suppression (8)
- DisableAllCommentInjector → DisableAllInjectResult
- DisableAllCommentRemover [partial] → DisableAllRemoveResult
- DisableAllInjectResult [record] → DisableAllInjectResult
- DisableAllRemoveResult [record] → DisableAllRemoveResult
- SuppressionCommentParser
- SuppressionEvaluator
- SuppressionSourceFileResolver
- ViolatingFilePathResolver
