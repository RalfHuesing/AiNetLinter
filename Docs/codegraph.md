# Codebase-Architektur & Abhaengigkeitsgraph (Auto-Generated)
Dieses Dokument visualisiert die interne Klassenstruktur und deren Beziehungen.

```mermaid
classDiagram
    class AgentFeaturesTests {
        +Analyze_TryParseOutParameter_NoViolationWhenTryPatternEnabled()
        +Analyze_VoidMethodWithOut_ReturnsViolation()
        +Analyze_OperationCanceledCatchWithFilter_NoSilentCatchViolation()
        +Format_IncludesGuidanceInDetailLine()
        +TestCoverageResolver_TypeofReference_CoversSourceClass()
        +TestCoverageResolver_WildcardPattern_MatchesIntegrationTests()
        +TestCoverageResolver_CoversComment_CoversSourceClass()
        +Run_WithTypeofTestReference_NoSentinelViolation()
        +Analyze_MinimalApiWithoutAsParameters_ReturnsViolation()
        +PartialClassLineAggregator_SumsAcrossFiles()
        +DisableAllDetector_RecognizesExactComment()
        +RuleMetadataRegistry_ResolvesKnownRule()
        +Analyze_SwallowedCatchWithIgnoredVariable_NoSilentCatchViolation()
        +Analyze_SwallowedCatchWithExpectedVariable_NoSilentCatchViolation()
    }
    class ArchitectureTests {
        +Analyze_WithCompliantCode_ReturnsZeroViolations()
        +Analyze_WithViolations_ReturnsExpectedViolations()
        +Analyze_WithFileTooLong_ReturnsLineCountViolation()
        +Analyze_WithHighCyclomaticComplexity_ReturnsCyclomaticComplexityViolation()
        +Analyze_WithHighCognitiveComplexity_ReturnsCognitiveComplexityViolation()
        +Analyze_WithForbiddenNamespaceDependency_ReturnsViolation()
        +Analyze_WithValueObjectNotRecordOrStruct_ReturnsViolation()
        +Analyze_WithValueObjectMutableProperty_ReturnsViolation()
        +Analyze_WithNonPascalCaseTypeName_ReturnsViolation()
        +Analyze_WithPublicTypeMissingXmlDoc_ReturnsViolation()
        +Analyze_WithGenericParameterName_ReturnsViolation()
        +Analyze_WithFileMissingNullableEnable_ReturnsViolation()
        +Analyze_WithSilentCatch_ReturnsViolation()
    }
    class AutoFixerTests {
        +FixSealedClasses_SealsConcreteClassesWithoutDescendants()
        +FixNullableEnable_PrependsNullableDirective()
        +FixReadonlyFields_AddsReadonlyKeyword()
    }
    class BaselineCliTests {
        +CreateBaseline_WithoutConfig_WritesJsonAndReturnsSuccess()
        +AuditWithBaseline_UnchangedFiles_ReturnsSuccess()
        +AuditWithBaseline_ChangedFile_ReportsViolationsAndUpdatesBaseline()
        +Main_ConflictingBaselineFlags_ReturnsExitCodeOne()
    }
    class BaselineComparerTests {
        +Compare_IdenticalChecksums_ReturnsNoChanges()
        +Compare_ChangedChecksum_MarksFileAsChanged()
        +Compare_NewFile_MarksFileAsChanged()
        +Compare_RemovedFile_MarksAsRemoved()
    }
    class BaselineReaderWriterTests {
        +WriteAndRead_Roundtrip_PreservesChecksums()
        +Write_SortsKeysDeterministically()
    }
    class BaselineViolationFilterTests {
        +Filter_SuppressesUnchangedFiles()
        +Filter_NoChangedFiles_ReturnsEmpty()
    }
    class FileChecksumCalculatorTests {
        +ComputeSha256Hex_KnownContent_ReturnsExpectedHash()
    }
    class SourceFileCatalogTests {
        +LoadAsync_MiniFixture_ReturnsSourceFiles()
    }
    class CliIntegrationTests {
        +RunLinterCli_OnWholeSolution_ReturnsSuccess()
        +GeneratePlaybook_ForSolution_GeneratesAndUpdatesPlaybook()
        +RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode()
    }
    class CognitiveComplexityWalkerTests {
        +GetCognitiveComplexity_WithFlatCode_ReturnsZero()
    }
    class LinterConfigNormalizerTests {
        +Normalize_RestoresDefaultPatterns_WhenClassNamePatternsIsNull()
        +Normalize_ThrowsForNullPatternEntry()
        +Normalize_ThrowsForPatternWithoutNamePlaceholder()
    }
    class ConsoleTestCollection {
    }
    class CodegraphGeneratorTests {
        +GenerateAsync_CorrectlyExtractsRelationshipsAndMethods()
    }
    class ControlFlowResilienceTests {
        +Throw_InConstructor_IsAllowed()
        +Throw_InStaticConstructor_IsAllowed()
        +Throw_InMethodEndingWithGuard_IsAllowed()
        +Throw_InMethodEndingWithValidate_IsAllowed()
        +Throw_InLocalFunctionEndingWithGuard_IsAllowed()
        +Throw_InLocalFunctionNestedInGuardMethod_IsDisallowed()
        +ThrowStatement_InNormalMethod_IsDisallowed()
        +ThrowExpression_InNormalMethod_IsDisallowed()
        +Throw_InPropertyAccessor_IsDisallowed()
        +Throw_InLambda_InsideNormalMethod_IsDisallowed()
        +Throw_InNormalMethod_WhenDisabled_IsAllowed()
        +Throw_InNormalMethod_WithSuppressionComment_IsAllowed()
    }
    class CouplingSemanticTests {
        +ConstructorDependencies_Exceeded_IsDisallowed()
        +PrimaryConstructorDependencies_Exceeded_IsDisallowed()
        +MagicValues_IntAndStringInMethodBody_IsDisallowed()
        +MagicValues_Exceptions_AreAllowed()
        +MagicValues_ConstDeclarations_AreAllowed()
        +MagicValues_AttributeArguments_AreAllowed()
    }
    class ScopeImmutabilityTests {
        +Shadowing_ParameterShadowsField_IsDisallowed()
        +Shadowing_LocalFunctionParameterShadowsOuterParameter_IsDisallowed()
        +Overloads_CountExceeded_IsDisallowed()
        +ParameterReassignment_NormalAssignment_IsDisallowed()
        +ParameterReassignment_OutParameter_IsAllowed()
        +ParameterReassignment_PropertyModification_IsAllowed()
        +ReadonlyFields_PrivateFieldUnmodifiedOutsideConstructor_IsDisallowed()
        +ReadonlyFields_PrivateFieldModifiedOutsideConstructor_IsAllowed()
        +ReadonlyFields_ReadonlyFieldUnmodifiedOutsideConstructor_IsAllowed()
    }
    class TestCoverageResolverTests {
        +IsCovered_SkipsNullTestClassName_InWildcardPattern()
        +IsCovered_ThrowsForEmptySourceClassName()
        +IsCovered_ThrowsForMissingClassNamePatterns()
    }
    class DeveloperExperienceTests {
        +ProjectConfigResolver_NoOverrides_ReturnsGlobalConfig()
        +ProjectConfigResolver_WithWildcardMatch_MergesOverrides()
        +AIContextFootprintCalculator_SimpleClass_CalculatesLines()
        +RepoPlaybookGenerator_ScansAndGeneratesMarkdown()
        +RepoPlaybookGenerator_WithAllowedException_FiltersThrowFromMetric()
        +LinterConfigLoader_WithNonExistentFile_ReturnsNull()
        +LinterConfigLoader_WithValidJson_LoadsConfig()
    }
    class DiffImpactAnalyzerTests {
        +ParseGitDiffHunks_WithValidDiff_ParsesHunksCorrectly()
    }
    class BaselineMiniFixtureWorkspace {
        +Dispose()
    }
    class LinterAnalyzerTests {
        +Analyze_WithValidCode_HasNoViolations()
        +Analyze_WithForbiddenNamespaceInStatement_ReturnsViolation()
        +Analyze_WithSuppressionComment_IgnoresViolation()
        +Analyze_WithDisableAllComment_IgnoresAllViolations()
        +Analyze_WithVariableNamedDynamic_DoesNotThrowViolation()
        +Analyze_WithUnsealedPartialClass_AllowUnsealedPartialClasses_NoViolation()
        +Analyze_WithUnsealedPartialClass_DefaultEnforced_HasViolation()
        +Analyze_Immutability_MutableProperty_HasViolation()
        +Analyze_Immutability_DtoClass_NoViolation()
        +Analyze_Exceptions_AllowedFatalException_NoViolation()
        +Analyze_Exceptions_CustomLogicException_HasViolation()
        +Analyze_BusinessLogic_NonStaticLogik_HasViolation()
        +Analyze_PrimitiveOverloads_HasViolation()
        +Analyze_TruncationSafety_ReadWithoutGuard_HasViolation()
        +Analyze_PhantomDependency_ReflectionCall_HasViolation()
        +Analyze_PhantomDependency_UnresolvedNamespace_HasViolation()
    }
    class LinterEngineTests {
        +LinterEngine_CanBeInitialized()
        +Run_WithHighlyRelevantClassMissingTestClass_ReturnsSentinelViolation()
        +Run_WithDisableAllComment_SuppressesStaticTestSentinel()
        +Run_WithDisableAllComment_SuppressesMaxInheritanceDepth()
        +Run_WithInheritanceDepthExceeded_ReturnsViolation()
        +Run_WithDuplicateClassNamesInDifferentNamespaces_NoCrashAndResolvesCorrectly()
        +Run_WithConfigurableSentinelThreshold_RespectsConfigValue()
        +Run_WithTestFileButNoTestMethods_SentinelFails()
        +CreateWorkspaceProperties_ContainsDesignTimeBuildKeys()
        +Run_WithManyDocuments_ProducesExpectedViolations()
    }
    class MethodLineCounterTests {
        +GetCodeLineCount_WithShortMethod_ReturnsExpectedCount()
        +GetCodeLineCount_IgnoresCommentsAndBlankLines()
        +GetCodeLineCount_ForAbstractMethod_ReturnsZero()
        +Analyze_WithLongMethod_ReturnsMaxMethodLineCountViolation()
    }
    class OutputRootResolverTests {
        +Resolve_ReturnsFullPathForDirectory()
        +Resolve_ReturnsParentDirectoryForSolutionFile()
        +Resolve_ThrowsWhenPathDoesNotExist()
    }
    class PathNormalizerTests {
        +ToRelative_ConvertsAbsolutePathToRelativeWithForwardSlashes()
        +ToRelative_ReturnsFileNameWhenOutsideOutputRoot()
        +ToRelative_ReturnsEmptyForNullOrEmptyPath()
    }
    class SarifWriterTests {
        +Write_UsesRelativeUriInArtifactLocation()
    }
    class ViolationSummaryBuilderTests {
        +BuildByFile_GroupsMultipleViolationsPerFile()
        +BuildByFile_SortsDescendingByCountThenAlphabetically()
        +BuildByRule_GroupsAndSortsDescendingByCount()
        +BuildByRule_TieBreaksAlphabeticallyByRuleName()
    }
    class ViolationTextFormatterTests {
        +Format_ReturnsEmptyForNoViolations()
        +Format_IncludesHeaderAndInstruction()
        +Format_IncludesSummarySectionsBeforeViolations()
        +Format_SortsByFilePathThenLineNumber()
        +Format_UsesRelativePathsAndCompactLineFormat()
        +Format_IncludesDynamicRuleInstructions()
    }
    class ProgramTests {
        +Main_WithEmptyArgs_ReturnsExitCodeOne()
        +Main_WithValidArgs_PrintsRunHeaderInTextMode()
    }
    class DisableAllCliTests {
        +AddDisableAll_OnViolatingFixture_InjectOnlyIntoViolatingFiles()
        +RemoveDisableAll_OnFixture_RemovesExactDisableAllLine()
        +Main_AddDisableAllWithBaseline_ReturnsExitCodeOne()
        +Main_AddAndRemoveDisableAll_ReturnsExitCodeOne()
    }
    class DisableAllCommentInjectorTests {
        +PrependDisableAll_AddsCommentAtTop()
        +PrependDisableAll_PreservesUtf8Bom()
        +TryInjectIntoFile_SkipsWhenDisableAllAlreadyPresent()
        +TryInjectIntoFile_PrependsCommentWhenMissing()
    }
    class DisableAllCommentRemoverTests {
        +RemoveDisableAllLines_RemovesExactLineWithLf()
        +RemoveDisableAllLines_RemovesExactLineWithCrLf()
        +RemoveDisableAllLines_KeepsIndentedOrPartialMatches()
        +RemoveDisableAllLines_RemovesLineInMiddleOfFile()
        +TryRemoveFromFile_RemovesOnlyExactDisableAllLine()
    }
    class SuppressionCommentParserTests {
        +MatchesRule_EvaluatesDisableComment()
        +ContainsDisableAll_DetectsExistingComment()
        +IsExactDisableAllLine_MatchesOnlyExactLine()
        +ContainsDisableAll_IgnoresRuleSpecificComment()
    }
    class SuppressionEvaluatorTests {
        +IsSuppressed_WithDisableAllAtFileStart_ReturnsTrueForAnyRule()
        +IsSuppressed_WithRuleSpecificComment_ReturnsTrueOnlyForMatchingRule()
    }
    class ViolatingFilePathResolverTests {
        +ResolveAbsolutePaths_ReturnsDistinctExistingFiles()
    }
    class SelfRegisteredExtensions {
        +AddSelfRegisteredExtensions()
    }
    class XunitAutoGeneratedEntryPoint {
        +Main()
    }
    class BaselineComparer {
        +Compare()
    }
    class BaselineComparisonResult {
    }
    class BaselineFile {
    }
    class BaselineJsonSerializer {
        +Deserialize()
        +Serialize()
    }
    class BaselineFileDto {
    }
    class BaselineReader {
        +Read()
    }
    class BaselineViolationFilter {
        +Filter()
    }
    class BaselineWriter {
        +Write()
    }
    class FileChecksumCalculator {
        +ComputeSha256Hex()
    }
    class SourceFileCatalog {
        +LoadAsync()
        +GetSourceFiles()
        +ComputeChecksums()
        +CollectDocumentWorkItemsAsync()
        +Dispose()
    }
    class CatalogDocumentWorkItem {
    }
    class SourceFileEntry {
    }
    class CliCommandBuilder {
    }
    class Options {
    }
    class ParsedArgs {
    }
    class CliOptionFactory {
    }
    class DebtReportExecutor {
        +RunDebtReportAsync()
    }
    class ImpactExecutor {
        +RunImpactAnalysisAsync()
    }
    class LinterArgs {
    }
    class MaintenanceExecutor {
        +AddDisableAllAsync()
        +RemoveDisableAllAsync()
        +CreateBaselineAsync()
    }
    class LinterConfig {
    }
    class NamespaceRule {
    }
    class GlobalConfig {
    }
    class MetricsConfig {
    }
    class TestSentinelConfig {
    }
    class RuleMetadataEntry {
    }
    class ProjectOverrideEntry {
    }
    class GlobalConfigOverride {
    }
    class MetricsConfigOverride {
    }
    class LinterConfigLoader {
        +TryLoadConfig()
    }
    class LinterConfigNormalizer {
        +Normalize()
    }
    class ProjectConfigResolver {
        +ResolveForDocument()
        +ResolveForProject()
    }
    class RuleMetadataRegistry {
        +Resolve()
        +ToSarifLevel()
    }
    class AnalysisState {
    }
    class CodegraphGenerator {
        +GenerateAsync()
    }
    class DiffImpactAnalyzer {
        +AnalyzeAsync()
    }
    class DocumentContext {
    }
    class LinterAnalyzer {
        +VisitUsingDirective()
        +VisitClassDeclaration()
        +VisitRecordDeclaration()
        +VisitStructDeclaration()
        +VisitNamespaceDeclaration()
        +VisitFileScopedNamespaceDeclaration()
        +VisitIdentifierName()
        +VisitInvocationExpression()
        +VisitMethodDeclaration()
        +VisitCatchClause()
        +VisitThrowStatement()
        +VisitThrowExpression()
        +Analyze()
        +Analyze()
        +Analyze()
        +VisitLiteralExpression()
        +VisitInterfaceDeclaration()
        +VisitPropertyDeclaration()
        +VisitVariableDeclarator()
        +VisitForEachStatement()
        +VisitCatchDeclaration()
        +VisitSingleVariableDesignation()
        +VisitParameter()
        +VisitConstructorDeclaration()
        +VisitAssignmentExpression()
        +VisitPostfixUnaryExpression()
        +VisitPrefixUnaryExpression()
        +VisitArgument()
        +VisitFieldDeclaration()
    }
    class AnalyzerArgs {
    }
    class LinterAutoFixer {
        +FixAsync()
    }
    class FixContext {
    }
    class LinterEngine {
        +RunAsync()
        +RunAsync()
        +RunAsync()
        +CreateWorkspaceProperties()
    }
    class PartialClassLineAggregator {
        +BuildViolations()
    }
    class PartialClassPart {
    }
    class PostAnalysisChecks {
        +Run()
    }
    class RepoPlaybookGenerator {
        +GenerateAsync()
    }
    class PlaybookStats {
    }
    class PlaybookSyntaxWalker {
        +VisitMethodDeclaration()
        +VisitThrowStatement()
        +VisitThrowExpression()
    }
    class TestCoverageCollector {
        +Collect()
    }
    class TestCoverageIndex {
        +AddTestClass()
        +AddReferencedType()
        +AddCoversComment()
    }
    class TestCoverageResolver {
        +IsCovered()
    }
    class TestProjectDetector {
        +IsTestProject()
    }
    class TestSentinelContext {
    }
    class AIContextFootprintCalculator {
        +Calculate()
    }
    class CognitiveComplexityGuidance {
        +Build()
    }
    class ComplexityCalculator {
        +GetCyclomaticComplexity()
        +GetCognitiveComplexity()
    }
    class CyclomaticComplexityWalker {
        +VisitIfStatement()
        +VisitWhileStatement()
        +VisitDoStatement()
        +VisitForStatement()
        +VisitForEachStatement()
        +VisitCatchClause()
        +VisitConditionalExpression()
        +VisitSwitchSection()
        +VisitSwitchExpressionArm()
        +VisitBinaryExpression()
    }
    class CognitiveComplexityWalker {
        +VisitIfStatement()
        +VisitElseClause()
        +VisitWhileStatement()
        +VisitDoStatement()
        +VisitForStatement()
        +VisitForEachStatement()
        +VisitCatchClause()
        +VisitSwitchStatement()
        +VisitSwitchExpression()
        +VisitConditionalExpression()
        +VisitBinaryExpression()
        +VisitLocalFunctionStatement()
        +VisitSimpleLambdaExpression()
        +VisitParenthesizedLambdaExpression()
    }
    class MethodLineCounter {
        +GetCodeLineCount()
    }
    class ClassInfo {
    }
    class RuleViolation {
    }
    class DebtReportBuilder {
        +BuildAsync()
    }
    class FolderCount {
    }
    class LinterLogger {
        +LogStart()
        +LogBaselineCreate()
        +LogDisableAllInject()
        +LogDisableAllRemove()
        +LogBaselineUpdate()
    }
    class OutputRootResolver {
        +Resolve()
    }
    class PathNormalizer {
        +ToRelative()
    }
    class SarifWriter {
        +Write()
    }
    class SarifDocument {
    }
    class SarifRun {
    }
    class SarifTool {
    }
    class SarifDriver {
    }
    class SarifResult {
    }
    class SarifMessage {
    }
    class SarifLocation {
    }
    class SarifPhysicalLocation {
    }
    class SarifArtifactLocation {
    }
    class SarifRegion {
    }
    class ViolationSummaryBuilder {
        +BuildByFile()
        +BuildByRule()
    }
    class FileViolationCount {
    }
    class RuleViolationCount {
    }
    class ViolationTextFormatter {
        +Format()
    }
    class Program {
        +Main()
    }
    class DisableAllDetector {
        +HasDisableAll()
        +FileHasDisableAll()
    }
    class GitChangedFilesResolver {
        +ResolveSince()
    }
    class ViolationScopeFilter {
        +Apply()
    }
    class ViolationScopeOptions {
    }
    class DisableAllCommentInjector {
        +InjectIntoFiles()
        +TryInjectIntoFile()
    }
    class InjectResult {
    }
    class DisableAllCommentRemover {
        +RemoveAsync()
        +TryRemoveFromFile()
    }
    class RemoveResult {
    }
    class SuppressionCommentParser {
        +MatchesRule()
        +ContainsDisableAll()
        +MatchesDisableAll()
        +IsExactDisableAllLine()
    }
    class SuppressionEvaluator {
        +IsSuppressed()
    }
    class SuppressionSourceFileResolver {
        +ResolveAbsolutePathsAsync()
    }
    class ViolatingFilePathResolver {
        +ResolveAbsolutePaths()
    }
    LinterConfig --> GlobalConfig : nutzt
    LinterConfig --> MetricsConfig : nutzt
    LinterConfig --> TestSentinelConfig : nutzt
    ProjectOverrideEntry --> GlobalConfigOverride : nutzt
    ProjectOverrideEntry --> MetricsConfigOverride : nutzt
    AnalysisState --> TestCoverageIndex : nutzt
    DocumentContext --> LinterConfig : nutzt
    LinterAnalyzer --> LinterConfig : nutzt
    AnalyzerArgs --> LinterConfig : nutzt
    LinterEngine --> LinterConfig : nutzt
    TestSentinelContext --> TestCoverageIndex : nutzt
    SarifRun --> SarifTool : nutzt
    SarifTool --> SarifDriver : nutzt
    SarifResult --> SarifMessage : nutzt
    SarifLocation --> SarifPhysicalLocation : nutzt
    SarifPhysicalLocation --> SarifArtifactLocation : nutzt
    SarifPhysicalLocation --> SarifRegion : nutzt
```
