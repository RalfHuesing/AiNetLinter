# Codegraph (Auto-generiert durch AiNetLinter 1.0.46)
Produktionscode · 155 Typen · 13 Namespaces

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

## AiNetLinter.Configuration (20)
- FileFilterEvaluator → FileFiltersConfig
- FileFiltersConfig [record] → FileFiltersConfig
- GlobalConfig [record] → GlobalConfig, GlobalConfigOverride
- GlobalConfigOverride [record] → GlobalConfigOverride
- LinterConfig [record] → FileFiltersConfig, GlobalConfig, LinterConfig, MagicValuesConfig, MetricsConfig, TestSentinelConfig, UiSeparationConfig
- LinterConfigLoader → LinterConfig
- LinterConfigNormalizer → LinterConfig
- MagicValuesConfig [record] → MagicValuesConfig, MagicValuesConfigOverride
- MagicValuesConfigOverride [record] → MagicValuesConfigOverride
- MetricsConfig [record] → MetricsConfig, MetricsConfigOverride
- MetricsConfigOverride [record] → MetricsConfigOverride
- NamespaceRule [record] → NamespaceRule
- ProjectConfigResolver → LinterConfig, ProjectOverrideEntry
- ProjectOverrideEntry [record] → GlobalConfigOverride, MagicValuesConfigOverride, MetricsConfigOverride, ProjectOverrideEntry, TestSentinelConfigOverride, UiSeparationConfigOverride
- RuleMetadataEntry [record] → RuleMetadataEntry
- RuleMetadataRegistry → LinterConfig, RuleMetadataEntry
- TestSentinelConfig [record] → TestSentinelConfig, TestSentinelConfigOverride
- TestSentinelConfigOverride [record] → TestSentinelConfigOverride
- UiSeparationConfig [record] → UiSeparationConfig, UiSeparationConfigOverride
- UiSeparationConfigOverride [record] → UiSeparationConfigOverride

## AiNetLinter.Core (32)
- AnalysisState [record] → AnalysisState, TestCoverageIndex
- AnalyzerArgs [record] → AnalyzerArgs, LinterConfig
- CacheDestination [record] → AnalysisCacheManager, CacheDestination
- CodegraphGenerator → TypeInfo
- CursorRulesGenerator → GlobalConfig, LinterConfig, ProjectOverrideEntry
- DiffImpactAnalyzer
- DocumentContext [record] → DocumentContext, LinterConfig
- FieldReadonlyTracker → RuleViolation
- FixContext [record] → FixContext
- FixOptions [record] → FixOptions
- LinterAnalyzer → AnalyzerArgs, CheckerContext, LinterConfig
- LinterAutoFixer → FixContext, FixOptions
- LinterEngine → AnalysisCacheManager, AnalysisState, CacheDestination, CatalogDocumentWorkItem, DocumentContext, LinterAnalyzer, LinterConfig, SourceFileCatalog, TestCoverageIndex, TestSignalsDto
- MetricDescriptor [record] → MetricDescriptor
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
- RuleDefinition [record] → RuleDefinition
- TestCoverageCollector [partial] → TestCoverageIndex, TestSentinelConfig
- TestCoverageIndex
- TestCoverageResolver → TestCoverageIndex, TestSentinelConfig
- TestProjectDetector
- TestSentinelContext [record] → TestCoverageIndex, TestSentinelContext
- TypeInfo [record] → TypeInfo
- UiFileSeparationChecker → AnalysisState, LinterConfig, RuleViolation, UiSeparationConfig

## AiNetLinter.Core.Checkers (18)
- ArchitectureChecker → CheckerContext
- BoolParameterChecker → CheckerContext
- BusinessLogicChecker → CheckerContext
- CheckerContext → FieldReadonlyTracker, LinterConfig, RuleViolation
- ComplexityCheck [record] → ComplexityCheck
- ComplexityChecker → CheckerContext, ComplexityCheck
- ControlFlowChecker → CheckerContext
- ImmutabilityChecker → CheckerContext
- MagicValuesChecker → CheckerContext, MagicValuesConfig
- MinimalApiChecker → CheckerContext
- NamingChecker → CheckerContext
- NestedTypesChecker → CheckerContext
- PublicMembersChecker → CheckerContext
- ScopeChecker → CheckerContext
- StateChecker → CheckerContext
- SyntaxHelper
- TruncationChecker → CheckerContext
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

## AiNetLinter.Output (21)
- DebtReportBuilder
- FileViolationCount [record] → FileViolationCount
- FolderCount [record] → FolderCount
- LinterLogger → BaselineComparisonResult
- OutputRootResolver
- PathNormalizer
- RuleViolationCount [record] → RuleViolationCount
- SarifArtifactLocation
- SarifDocument
- SarifDriver
- SarifLocation → SarifPhysicalLocation
- SarifMessage
- SarifPhysicalLocation → SarifArtifactLocation, SarifRegion
- SarifProperties
- SarifRegion
- SarifResult → SarifMessage, SarifProperties
- SarifRun → SarifTool
- SarifTool → SarifDriver
- SarifWriter → LinterConfig
- ViolationSummaryBuilder → LinterConfig
- ViolationTextFormatter → LinterConfig, RuleViolation

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
