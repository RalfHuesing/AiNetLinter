# Codegraph (Auto-generiert durch AiNetLinter 1.0.54)
Produktionscode · 166 Typen · 15 Namespaces

## AiNetLinter (1)
- Program → CliParsedArgs, LinterArgs

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

## AiNetLinter.Cli (10)
- CliBaselineOptions [record] → CliBaselineOptions
- CliCommandBuilder → CliOptions, CliParsedArgs
- CliImpactOptions [record] → CliImpactOptions
- CliMaintenanceOptions [record] → CliMaintenanceOptions
- CliOptionFactory
- CliOptions [record] → CliOptions
- CliOutputOptions [record] → CliOutputOptions
- CliParsedArgs [record] → CliBaselineOptions, CliImpactOptions, CliMaintenanceOptions, CliOutputOptions, CliParsedArgs, CliScopeOptions
- CliScopeOptions [record] → CliScopeOptions
- LinterArgs

## AiNetLinter.Commands (10)
- AuditCommand → AuditRunContext, ILintConsole, LinterArgs, PlaybookOptions, SourceFileCatalog
- AuditRunContext [record] → AuditRunContext, ILintConsole, IPerformanceProfiler, LinterArgs, LinterConfig
- DebtReportCommand → ILintConsole, LinterArgs
- DocsCommand → ILintConsole
- FootprintCommand → ILintConsole, LinterArgs
- ImpactCommand → ILintConsole, LinterArgs
- ListRulesCommand → ILintConsole
- MaintenanceCommand → ILintConsole, LinterArgs
- PlaybookCheckCommand → ILintConsole, LinterArgs
- SyncCursorRulesCommand → ILintConsole, LinterArgs

## AiNetLinter.Configuration (21)
- CompoundSuppression [record] → CompoundSuppression
- FileFilterEvaluator → FileFiltersConfig
- FileFiltersConfig [record] → FileFiltersConfig
- GlobalConfig [record] → GlobalConfig, GlobalConfigOverride
- GlobalConfigOverride [record] → GlobalConfigOverride
- LinterConfig [record] → FileFiltersConfig, GlobalConfig, LinterConfig, MetricsConfig, TestSentinelConfig, UiSeparationConfig
- LinterConfigLoader → LinterConfig
- LinterConfigNormalizer → LinterConfig
- LinterConfigSyncer → LinterConfig
- MetricCondition [record] → MetricCondition
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

## AiNetLinter.Core (23)
- AnalysisState [record] → AnalysisState, TestCoverageIndex
- AnalyzerArgs [record] → AnalyzerArgs, LinterConfig
- CacheDestination [record] → AnalysisCacheManager, CacheDestination
- CompoundSuppressionEvaluator → CompoundSuppression
- DiffImpactAnalyzer
- DocumentContext [record] → DocumentContext, LinterConfig
- FixContext [record] → FixContext, ILintConsole
- FixOptions [record] → FixOptions
- LinterAnalyzer → AnalyzerArgs, CheckerContext, LinterConfig
- LinterAutoFixer → FixContext, FixOptions, ILintConsole
- LinterEngine → AnalysisCacheManager, AnalysisState, CacheDestination, CatalogDocumentWorkItem, DocumentContext, ILintConsole, IPerformanceProfiler, LinterAnalyzer, LinterConfig, SourceFileCatalog, TestCoverageIndex, TestSignalsDto
- LinterRuleIds
- MetricNames
- PartialClassLineAggregator → LinterConfig, RuleViolation
- PartialClassPart [record] → PartialClassPart
- PostAnalysisChecks → AnalysisState, ClassInfo, IPerformanceProfiler, LinterConfig, TestSentinelConfig, TestSentinelContext
- RuleMetadata [record] → RuleMetadata
- RuleRegistry → RuleMetadata
- TestCoverageCollector [partial] → TestCoverageIndex, TestSentinelConfig
- TestCoverageIndex
- TestCoverageResolver → TestCoverageIndex, TestSentinelConfig
- TestProjectDetector
- TestSentinelContext [record] → TestCoverageIndex, TestSentinelContext

## AiNetLinter.Core.Checkers (25)
- BoolParameterChecker → CheckerContext
- CheckerContext → LinterConfig, RuleViolation
- ClassInfoCollector → CheckerContext
- ComplexityCheck [record] → ComplexityCheck
- ComplexityChecker → CheckerContext, ComplexityCheck, ParamViolationArgs
- ControlFlowChecker → CheckerContext
- DynamicTypeChecker → CheckerContext
- GeneratedCodeDetector → CheckerContext
- ImmutabilityChecker → CheckerContext
- InheritanceDepthChecker → CheckerContext
- MinimalApiChecker → CheckerContext
- NamespaceCouplingChecker → CheckerContext
- NamingChecker → CheckerContext
- NestedTypesChecker → CheckerContext
- ParamViolationArgs [record] → CheckerContext, CompoundSuppression, ParamViolationArgs
- PhantomDependencyChecker → CheckerContext
- PublicMembersChecker → CheckerContext
- ScopeChecker → CheckerContext
- SealedClassChecker → CheckerContext
- StateChecker → CheckerContext
- SyntaxHelper
- TestAttributeDetector → CheckerContext
- UiFileSeparationChecker → AnalysisState, LinterConfig, RuleViolation, UiSeparationConfig
- ValueObjectChecker → CheckerContext
- WpfSeparationChecker → CheckerContext

## AiNetLinter.Diagnostics (8)
- DocumentPerformanceEntry [record] → DocumentPerformanceEntry
- IPerformanceProfiler [interface]
- NullPerformanceProfiler impl IPerformanceProfiler → NullPerformanceProfiler
- PerformanceProfiler impl IPerformanceProfiler → PhaseDurationSnapshot, ProfilerContext, ProfilerJsonReport
- PhaseDurationSnapshot [record] → PhaseDurationSnapshot
- ProfilerContext [record] → ProfilerContext
- ProfilerJsonReport [record] → ProfilerJsonReport, ProfilerSummary
- ProfilerSummary [record] → ProfilerSummary

## AiNetLinter.Generators (10)
- CodegraphGenerator → TypeInfo
- CursorRulesGenerator → GlobalConfig, LinterConfig, ProjectOverrideEntry
- PlaybookBuildContext [record] → LinterConfig, PlaybookBuildContext, PlaybookStats
- PlaybookDocInfo [record] → PlaybookDocInfo
- PlaybookDocScanResult [record] → PlaybookDocScanResult
- PlaybookOptions [record] → LinterConfig, PlaybookOptions
- PlaybookStats [record] → PlaybookStats
- PlaybookSyntaxWalker
- RepoPlaybookGenerator → LinterConfig, PlaybookBuildContext, PlaybookOptions, RuleViolation
- TypeInfo [record] → TypeInfo

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

## AiNetLinter.Output (15)
- ConsoleLintConsole impl ILintConsole → ConsoleLintConsole
- DebtReportBuilder
- FileViolationCount [record] → FileViolationCount
- FolderCount [record] → FolderCount
- ILintConsole [interface]
- LinterErrorCodes
- LinterErrorFormatter
- LinterLogger → BaselineComparisonResult, ILintConsole
- OutputRootResolver
- PathNormalizer
- RuleLegendEntry [record] → RuleLegendEntry
- RuleLegendRegistry → RuleLegendEntry
- RuleViolationCount [record] → RuleViolationCount
- ViolationMarkdownFormatter → LinterConfig, RuleViolation
- ViolationSummaryBuilder → LinterConfig

## AiNetLinter.Scope (3)
- GitChangedFilesResolver
- ViolationScopeFilter → ViolationScopeOptions
- ViolationScopeOptions [record] → ViolationScopeOptions

## AiNetLinter.Suppression (9)
- DisableAllCommentInjector → DisableAllInjectResult
- DisableAllCommentRemover → DisableAllRemoveResult
- DisableAllDetector [partial]
- DisableAllInjectResult [record] → DisableAllInjectResult
- DisableAllRemoveResult [record] → DisableAllRemoveResult
- SuppressionCommentParser
- SuppressionEvaluator
- SuppressionSourceFileResolver
- ViolatingFilePathResolver
