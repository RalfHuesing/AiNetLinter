# Codegraph (auto-generated)
Produktionscode · 107 Typen · 10 Namespaces

## AiNetLinter (1)
- Program → LinterArgs, LinterConfig, ParsedArgs, SourceFileCatalog

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

## AiNetLinter.Cli (13)
- BaselineOptions [record] → BaselineOptions
- CliCommandBuilder → Options, ParsedArgs
- CliOptionFactory
- DebtReportExecutor → LinterArgs
- ImpactExecutor → LinterArgs
- ImpactOptions [record] → ImpactOptions
- LinterArgs
- MaintenanceExecutor → LinterArgs
- MaintenanceOptions [record] → MaintenanceOptions
- Options [record] → Options
- OutputOptions [record] → OutputOptions
- ParsedArgs [record] → BaselineOptions, ImpactOptions, MaintenanceOptions, OutputOptions, ParsedArgs, ScopeOptions
- ScopeOptions [record] → ScopeOptions

## AiNetLinter.Configuration (13)
- GlobalConfig [record] → GlobalConfig, GlobalConfigOverride
- GlobalConfigOverride [record] → GlobalConfigOverride
- LinterConfig [record] → GlobalConfig, LinterConfig, MetricsConfig, TestSentinelConfig
- LinterConfigLoader → LinterConfig
- LinterConfigNormalizer → LinterConfig
- MetricsConfig [record] → MetricsConfig, MetricsConfigOverride
- MetricsConfigOverride [record] → MetricsConfigOverride
- NamespaceRule [record] → NamespaceRule
- ProjectConfigResolver → LinterConfig, ProjectOverrideEntry
- ProjectOverrideEntry [record] → GlobalConfigOverride, MetricsConfigOverride, ProjectOverrideEntry
- RuleMetadataEntry [record] → RuleMetadataEntry
- RuleMetadataRegistry → LinterConfig, RuleMetadataEntry
- TestSentinelConfig [record] → TestSentinelConfig

## AiNetLinter.Core (27)
- AnalysisState [record] → AnalysisState, TestCoverageIndex
- AnalyzerArgs [record] → AnalyzerArgs, LinterConfig
- CodegraphGenerator → TypeInfo
- CursorRulesGenerator → LinterConfig
- DiffImpactAnalyzer
- DocumentContext [record] → DocumentContext, LinterConfig
- FieldReadonlyTracker → RuleViolation
- FixContext [record] → FixContext
- LinterAnalyzer [partial] → AnalyzerArgs, FieldReadonlyTracker, LinterConfig, NamespaceRule
- LinterAutoFixer → FixContext
- LinterEngine → AnalysisState, CatalogDocumentWorkItem, DocumentContext, LinterAnalyzer, LinterConfig, SourceFileCatalog, TestCoverageIndex
- MetricDescriptor [record] → MetricDescriptor
- PartialClassLineAggregator → LinterConfig, RuleViolation
- PartialClassPart [record] → PartialClassPart
- PlaybookDocInfo [record] → PlaybookDocInfo
- PlaybookDocScanResult [record] → PlaybookDocScanResult
- PlaybookStats [record] → PlaybookStats
- PlaybookSyntaxWalker
- PostAnalysisChecks → AnalysisState, ClassInfo, LinterConfig, TestSentinelContext
- RepoPlaybookGenerator → LinterConfig, PlaybookStats
- RuleDefinition [record] → RuleDefinition
- TestCoverageCollector [partial] → TestCoverageIndex, TestSentinelConfig
- TestCoverageIndex
- TestCoverageResolver → TestCoverageIndex, TestSentinelConfig
- TestProjectDetector
- TestSentinelContext [record] → TestCoverageIndex, TestSentinelContext
- TypeInfo [record] → TypeInfo

## AiNetLinter.Metrics (6)
- AIContextFootprintCalculator
- CognitiveComplexityGuidance
- CognitiveComplexityWalker
- ComplexityCalculator
- CyclomaticComplexityWalker
- MethodLineCounter

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
- DisableAllCommentInjector → InjectResult
- DisableAllCommentRemover [partial] → RemoveResult
- InjectResult [record] → InjectResult
- RemoveResult [record] → RemoveResult
- SuppressionCommentParser
- SuppressionEvaluator
- SuppressionSourceFileResolver
- ViolatingFilePathResolver
