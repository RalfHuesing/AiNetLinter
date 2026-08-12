---
task: speedup-tests
type: migration-ledger
maintained_by: coder
last_updated: 2026-08-12
---

# Migrationsledger: `AiNetLinter.Tests` → `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests`

Vollständiges Inventar aller heutigen Legacy-Testklassen in `src/AiNetLinter.Tests/`
(Klassen mit mindestens einer `[Fact]`/`[Theory]`-Methode). Wird von
`TestMigrationLedgerConsistencyTests` (`src/AiNetLinter.IntegrationTests/Migration/`)
maschinell gegen den tatsächlichen Bestand geprüft — siehe `konzept.md`
Leitplanke 8, Unterabschnitt „Zwei Mechanismen, die das Ledger von Dokumentation
zu Schutz machen".

## Statuslegende

- **`pending`** — Klasse existiert unverändert im Legacy-Projekt, noch nicht migriert.
- **`migrated`** — Vertrag vollständig in eine neue Zielklasse übernommen, Legacy-Klasse
  physisch gelöscht.
- **`consolidated`** — Vertrag zusammen mit anderen Legacy-Klassen in eine gemeinsame neue
  Zielklasse überführt (mehrere alte Zeilen können auf denselben neuen Abdeckungsort zeigen),
  Legacy-Klasse(n) physisch gelöscht.
- **`removed-trivial`** — Legacy-Klasse ohne eigenständigen Vertrag (reine
  Konstruktor-/Property-Durchreichung, Compiler-verifizierte Record-Semantik, echtes Duplikat),
  ersatzlos gelöscht. Braucht zwingend einen Begründungstext in der Spalte „Neuer Abdeckungsort".

## Konsistenzregeln (durchgesetzt durch den Guard)

1. Jede tatsächliche Legacy-Testklasse (mindestens eine `[Fact]`/`[Theory]`-Methode) hat genau
   eine Zeile hier.
2. Eine Zeile mit Status `migrated`/`consolidated` darf keine noch existierende Legacy-Klasse
   mehr referenzieren (die Quelldatei muss gelöscht sein).
3. Eine Zeile mit Status `migrated`/`consolidated` braucht einen tatsächlich existierenden neuen
   Abdeckungsort (Datei + Klasse in `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests`).
4. Eine Zeile mit Status `removed-trivial` braucht nicht-leeren Begründungstext in der Spalte
   „Neuer Abdeckungsort" statt eines Pfads.

Alle tieferen Pflichtfelder (Risiko, Erfolgs-/Negativ-/Fehlerfall, Evidenz) sind laut Step-002-Plan
erst ab dem Kohorten-Step fällig, in dem eine Zeile den Status wechselt — die Initialbefüllung
bleibt bewusst auf Inventar-Ebene.

## Inventar (183 Legacy-Testklassen, Stand step-002, alle `pending`)

| Quelldatei | Testklasse | Produktbereich | Status | Legacy-Filter | Neuer Abdeckungsort |
|---|---|---|---|---|---|
| `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs` | `ArchitectureTests` | Architecture | pending | `FullyQualifiedName~ArchitectureTests` |  |
| `src/AiNetLinter.Tests/Baseline/BaselineCliTests.cs` | `BaselineCliTests` | Baseline | pending | `FullyQualifiedName~BaselineCliTests` |  |
| `src/AiNetLinter.Tests/Baseline/BaselineComparerTests.cs` | `BaselineComparerTests` | Baseline | pending | `FullyQualifiedName~BaselineComparerTests` |  |
| `src/AiNetLinter.Tests/Baseline/BaselineReaderWriterTests.cs` | `BaselineReaderWriterTests` | Baseline | pending | `FullyQualifiedName~BaselineReaderWriterTests` |  |
| `src/AiNetLinter.Tests/Baseline/BaselineViolationFilterTests.cs` | `BaselineViolationFilterTests` | Baseline | pending | `FullyQualifiedName~BaselineViolationFilterTests` |  |
| `src/AiNetLinter.Tests/Baseline/FileChecksumCalculatorTests.cs` | `FileChecksumCalculatorTests` | Baseline | pending | `FullyQualifiedName~FileChecksumCalculatorTests` |  |
| `src/AiNetLinter.Tests/Baseline/FileSystemExclusionHelpersTests.cs` | `FileSystemExclusionHelpersTests` | Baseline | pending | `FullyQualifiedName~FileSystemExclusionHelpersTests` |  |
| `src/AiNetLinter.Tests/Baseline/ProjectRestoreStateTests.cs` | `ProjectRestoreStateTests` | Baseline | pending | `FullyQualifiedName~ProjectRestoreStateTests` |  |
| `src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs` | `SourceFileCatalogBlazorPartialTests` | Baseline | pending | `FullyQualifiedName~SourceFileCatalogBlazorPartialTests` |  |
| `src/AiNetLinter.Tests/Baseline/SourceFileCatalogRegisterMSBuildTests.cs` | `SourceFileCatalogRegisterMSBuildTests` | Baseline | pending | `FullyQualifiedName~SourceFileCatalogRegisterMSBuildTests` |  |
| `src/AiNetLinter.Tests/Baseline/SourceFileCatalogTests.cs` | `SourceFileCatalogTests` | Baseline | pending | `FullyQualifiedName~SourceFileCatalogTests` |  |
| `src/AiNetLinter.Tests/Baseline/WebBaselineTests.cs` | `WebBaselineTests` | Baseline | pending | `FullyQualifiedName~WebBaselineTests` |  |
| `src/AiNetLinter.Tests/Cache/AnalysisCacheManagerIsolationTests.cs` | `AnalysisCacheManagerIsolationTests` | Cache | pending | `FullyQualifiedName~AnalysisCacheManagerIsolationTests` |  |
| `src/AiNetLinter.Tests/Cache/AnalysisCacheManagerTests.cs` | `AnalysisCacheManagerTests` | Cache | pending | `FullyQualifiedName~AnalysisCacheManagerTests` |  |
| `src/AiNetLinter.Tests/Cache/CacheEntryMapperTests.cs` | `CacheEntryMapperTests` | Cache | pending | `FullyQualifiedName~CacheEntryMapperTests` |  |
| `src/AiNetLinter.Tests/Core/Checkers/AsciiIdentifiersTests.cs` | `AsciiIdentifiersTests` | Checkers | migrated | `FullyQualifiedName~AsciiIdentifiersTests` | `src/AiNetLinter.FastTests/Core/Checkers/AsciiIdentifiersTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/AsyncVoidCheckerTests.cs` | `AsyncVoidCheckerTests` | Checkers | migrated | `FullyQualifiedName~AsyncVoidCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/AsyncVoidCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/BlockingTaskCheckerTests.cs` | `BlockingTaskCheckerTests` | Checkers | migrated | `FullyQualifiedName~BlockingTaskCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/BlockingTaskCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/CouplingSemanticTests.cs` | `CouplingSemanticTests` | Checkers | migrated | `FullyQualifiedName~CouplingSemanticTests` | `src/AiNetLinter.FastTests/Core/Checkers/CouplingSemanticTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/DuplicateCodeCheckerTests.cs` | `DuplicateCodeCheckerTests` | Checkers | migrated | `FullyQualifiedName~DuplicateCodeCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/DuplicateCodeCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/DynamicTypeCheckerTests.cs` | `DynamicTypeCheckerTests` | Checkers | migrated | `FullyQualifiedName~DynamicTypeCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/DynamicTypeCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/LinqChainLengthCheckerTests.cs` | `LinqChainLengthCheckerTests` | Checkers | migrated | `FullyQualifiedName~LinqChainLengthCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/LinqChainLengthCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MaxBoolParameterCountTests.cs` | `MaxBoolParameterCountTests` | Checkers | migrated | `FullyQualifiedName~MaxBoolParameterCountTests` | `src/AiNetLinter.FastTests/Core/Checkers/MaxBoolParameterCountTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MaxConstructorDependenciesTests.cs` | `MaxConstructorDependenciesTests` | Checkers | migrated | `FullyQualifiedName~MaxConstructorDependenciesTests` | `src/AiNetLinter.FastTests/Core/Checkers/MaxConstructorDependenciesTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MaxInheritanceDepthTests.cs` | `MaxInheritanceDepthTests` | Checkers | migrated | `FullyQualifiedName~MaxInheritanceDepthTests` | `src/AiNetLinter.FastTests/Core/Checkers/MaxInheritanceDepthTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MaxPartialClassFilesTests.cs` | `MaxPartialClassFilesTests` | Checkers | migrated | `FullyQualifiedName~MaxPartialClassFilesTests` | `src/AiNetLinter.FastTests/Core/Checkers/MaxPartialClassFilesTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MaxPublicMembersPerTypeTests.cs` | `MaxPublicMembersPerTypeTests` | Checkers | migrated | `FullyQualifiedName~MaxPublicMembersPerTypeTests` | `src/AiNetLinter.FastTests/Core/Checkers/MaxPublicMembersPerTypeTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MaxSwitchArmsTests.cs` | `MaxSwitchArmsTests` | Checkers | migrated | `FullyQualifiedName~MaxSwitchArmsTests` | `src/AiNetLinter.FastTests/Core/Checkers/MaxSwitchArmsTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountAccessibilityTests.cs` | `MethodParameterCountAccessibilityTests` | Checkers | migrated | `FullyQualifiedName~MethodParameterCountAccessibilityTests` | `src/AiNetLinter.FastTests/Core/Checkers/MethodParameterCountAccessibilityTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountIgnoreTypePrefixesTests.cs` | `MethodParameterCountIgnoreTypePrefixesTests` | Checkers | migrated | `FullyQualifiedName~MethodParameterCountIgnoreTypePrefixesTests` | `src/AiNetLinter.FastTests/Core/Checkers/MethodParameterCountIgnoreTypePrefixesTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountOverrideTests.cs` | `MethodParameterCountOverrideTests` | Checkers | migrated | `FullyQualifiedName~MethodParameterCountOverrideTests` | `src/AiNetLinter.FastTests/Core/Checkers/MethodParameterCountOverrideTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/MiddleManCheckerTests.cs` | `MiddleManCheckerTests` | Checkers | migrated | `FullyQualifiedName~MiddleManCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/MiddleManCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/NamespaceCouplingCheckerTests.cs` | `NamespaceCouplingCheckerTests` | Checkers | migrated | `FullyQualifiedName~NamespaceCouplingCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/NamespaceCouplingCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/NamespaceDirectoryMappingTests.cs` | `NamespaceDirectoryMappingTests` | Checkers | migrated | `FullyQualifiedName~NamespaceDirectoryMappingTests` | `src/AiNetLinter.FastTests/Core/Checkers/NamespaceDirectoryMappingTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/NamingCheckerTests.cs` | `NamingCheckerTests` | Checkers | migrated | `FullyQualifiedName~NamingCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/NamingCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/NestedTypesCheckerTests.cs` | `NestedTypesCheckerTests` | Checkers | migrated | `FullyQualifiedName~NestedTypesCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/NestedTypesCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/PhantomDependencyCheckerTests.cs` | `PhantomDependencyCheckerTests` | Checkers | migrated | `FullyQualifiedName~PhantomDependencyCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/PhantomDependencyCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/SealedClassCheckerTests.cs` | `SealedClassCheckerTests` | Checkers | migrated | `FullyQualifiedName~SealedClassCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/SealedClassCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/SilentCatchAllowedTypesTests.cs` | `SilentCatchAllowedTypesTests` | Checkers | migrated | `FullyQualifiedName~SilentCatchAllowedTypesTests` | `src/AiNetLinter.FastTests/Core/Checkers/SilentCatchAllowedTypesTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/SwitchDispatcherDetectorTests.cs` | `SwitchDispatcherDetectorTests` | Checkers | migrated | `FullyQualifiedName~SwitchDispatcherDetectorTests` | `src/AiNetLinter.FastTests/Core/Checkers/SwitchDispatcherDetectorTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/UiFileSeparationCheckerTests.cs` | `UiFileSeparationCheckerTests` | Checkers | migrated | `FullyQualifiedName~UiFileSeparationCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/UiFileSeparationCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/ValueObjectCheckerTests.cs` | `ValueObjectCheckerTests` | Checkers | migrated | `FullyQualifiedName~ValueObjectCheckerTests` | `src/AiNetLinter.FastTests/Core/Checkers/ValueObjectCheckerTests.cs` |
| `src/AiNetLinter.Tests/Core/Checkers/WpfCodeBehindTests.cs` | `WpfCodeBehindTests` | Checkers | migrated | `FullyQualifiedName~WpfCodeBehindTests` | `src/AiNetLinter.FastTests/Core/Checkers/WpfCodeBehindTests.cs` |
| `src/AiNetLinter.Tests/Cli/CliCommandBuilderMcpLogTests.cs` | `CliCommandBuilderMcpLogTests` | Cli | pending | `FullyQualifiedName~CliCommandBuilderMcpLogTests` |  |
| `src/AiNetLinter.Tests/Cli/CliIntegrationTests.cs` | `CliIntegrationTests` | Cli | pending | `FullyQualifiedName~CliIntegrationTests` |  |
| `src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs` | `FilterCliIntegrationTests` | Cli | migrated | `FullyQualifiedName~FilterCliIntegrationTests` | `src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs` |
| `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsCliTests.cs` | `IgnoreSuppressionsCliTests` | Cli | pending | `FullyQualifiedName~IgnoreSuppressionsCliTests` |  |
| `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsIntegrationTests.cs` | `IgnoreSuppressionsIntegrationTests` | Cli | pending | `FullyQualifiedName~IgnoreSuppressionsIntegrationTests` |  |
| `src/AiNetLinter.Tests/Cli/ProgramTests.cs` | `ProgramTests` | Cli | pending | `FullyQualifiedName~ProgramTests` |  |
| `src/AiNetLinter.Tests/Commands/AuditCommandTests.cs` | `AuditCommandTests` | Commands | pending | `FullyQualifiedName~AuditCommandTests` |  |
| `src/AiNetLinter.Tests/Commands/CliBatchRegressionTests.cs` | `CliBatchRegressionTests` | Commands | pending | `FullyQualifiedName~CliBatchRegressionTests` |  |
| `src/AiNetLinter.Tests/Commands/DocsCommandTests.cs` | `DocsCommandTests` | Commands | pending | `FullyQualifiedName~DocsCommandTests` |  |
| `src/AiNetLinter.Tests/Commands/ListRulesCommandTests.cs` | `ListRulesCommandTests` | Commands | pending | `FullyQualifiedName~ListRulesCommandTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandAmbiguityE2ETests.cs` | `McpServerCommandAmbiguityE2ETests` | Commands | pending | `FullyQualifiedName~McpServerCommandAmbiguityE2ETests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandCacheBypassTests.cs` | `McpServerCommandCacheBypassTests` | Commands | pending | `FullyQualifiedName~McpServerCommandCacheBypassTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs` | `McpServerCommandCallLogTests` | Commands | pending | `FullyQualifiedName~McpServerCommandCallLogTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandErrorHandlingTests.cs` | `McpServerCommandErrorHandlingTests` | Commands | pending | `FullyQualifiedName~McpServerCommandErrorHandlingTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandFindReferencesTests.cs` | `McpServerCommandFindReferencesTests` | Commands | pending | `FullyQualifiedName~McpServerCommandFindReferencesTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs` | `McpServerCommandFindSymbolTests` | Commands | pending | `FullyQualifiedName~McpServerCommandFindSymbolTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandGetImpactTests.cs` | `McpServerCommandGetImpactTests` | Commands | pending | `FullyQualifiedName~McpServerCommandGetImpactTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs` | `McpServerCommandLoadingStateTests` | Commands | pending | `FullyQualifiedName~McpServerCommandLoadingStateTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandMissHintTests.cs` | `McpServerCommandMissHintTests` | Commands | pending | `FullyQualifiedName~McpServerCommandMissHintTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandStalenessTests.cs` | `McpServerCommandStalenessTests` | Commands | pending | `FullyQualifiedName~McpServerCommandStalenessTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` | `McpServerCommandTests` | Commands | pending | `FullyQualifiedName~McpServerCommandTests` |  |
| `src/AiNetLinter.Tests/Commands/PlaybookCheckCommandTests.cs` | `PlaybookCheckCommandTests` | Commands | pending | `FullyQualifiedName~PlaybookCheckCommandTests` |  |
| `src/AiNetLinter.Tests/Commands/SyncAgentRulesCommandTests.cs` | `SyncAgentRulesCommandTests` | Commands | pending | `FullyQualifiedName~SyncAgentRulesCommandTests` |  |
| `src/AiNetLinter.Tests/Configuration/AgentFeaturesTests.cs` | `AgentFeaturesTests` | Configuration | pending | `FullyQualifiedName~AgentFeaturesTests` |  |
| `src/AiNetLinter.Tests/Configuration/ConfigLoaderRulesJsonTests.cs` | `ConfigLoaderRulesJsonTests` | Configuration | pending | `FullyQualifiedName~ConfigLoaderRulesJsonTests` |  |
| `src/AiNetLinter.Tests/Configuration/ConfigNormalizerTests.cs` | `ConfigNormalizerTests` | Configuration | pending | `FullyQualifiedName~ConfigNormalizerTests` |  |
| `src/AiNetLinter.Tests/Configuration/ConfigSyncerTests.cs` | `ConfigSyncerTests` | Configuration | pending | `FullyQualifiedName~ConfigSyncerTests` |  |
| `src/AiNetLinter.Tests/Configuration/DeveloperExperienceTests.cs` | `DeveloperExperienceTests` | Configuration | pending | `FullyQualifiedName~DeveloperExperienceTests` |  |
| `src/AiNetLinter.Tests/Configuration/FileFilterEvaluatorTests.cs` | `FileFilterEvaluatorTests` | Configuration | pending | `FullyQualifiedName~FileFilterEvaluatorTests` |  |
| `src/AiNetLinter.Tests/Configuration/PathOverridesTests.cs` | `PathOverridesTests` | Configuration | pending | `FullyQualifiedName~PathOverridesTests` |  |
| `src/AiNetLinter.Tests/Configuration/RuleMetadataRegistryTests.cs` | `RuleMetadataRegistryTests` | Configuration | pending | `FullyQualifiedName~RuleMetadataRegistryTests` |  |
| `src/AiNetLinter.Tests/Core/AutoFixerTests.cs` | `AutoFixerTests` | Core | pending | `FullyQualifiedName~AutoFixerTests` |  |
| `src/AiNetLinter.Tests/Core/ClassInfoCollectorTests.cs` | `ClassInfoCollectorTests` | Core | pending | `FullyQualifiedName~ClassInfoCollectorTests` |  |
| `src/AiNetLinter.Tests/Core/CompoundSuppressionEvaluatorTests.cs` | `CompoundSuppressionEvaluatorTests` | Core | pending | `FullyQualifiedName~CompoundSuppressionEvaluatorTests` |  |
| `src/AiNetLinter.Tests/Core/CompoundSuppressionIntegrationTests.cs` | `CompoundSuppressionIntegrationTests` | Core | pending | `FullyQualifiedName~CompoundSuppressionIntegrationTests` |  |
| `src/AiNetLinter.Tests/Core/ControlFlowResilienceTests.cs` | `ControlFlowResilienceTests` | Core | pending | `FullyQualifiedName~ControlFlowResilienceTests` |  |
| `src/AiNetLinter.Tests/Core/DiffImpactAnalyzerTests.cs` | `DiffImpactAnalyzerTests` | Core | pending | `FullyQualifiedName~DiffImpactAnalyzerTests` |  |
| `src/AiNetLinter.Tests/Core/LinterAnalyzerTests.cs` | `LinterAnalyzerTests` | Core | pending | `FullyQualifiedName~LinterAnalyzerTests` |  |
| `src/AiNetLinter.Tests/Core/LinterEngineCacheTests.cs` | `LinterEngineCacheTests` | Core | pending | `FullyQualifiedName~LinterEngineCacheTests` |  |
| `src/AiNetLinter.Tests/Core/LinterEngineProjectRestoreTests.cs` | `LinterEngineProjectRestoreTests` | Core | pending | `FullyQualifiedName~LinterEngineProjectRestoreTests` |  |
| `src/AiNetLinter.Tests/Core/LinterEngineTests.cs` | `LinterEngineTests` | Core | pending | `FullyQualifiedName~LinterEngineTests` |  |
| `src/AiNetLinter.Tests/Core/NamespaceFilterTests.cs` | `NamespaceFilterTests` | Core | pending | `FullyQualifiedName~NamespaceFilterTests` |  |
| `src/AiNetLinter.Tests/Core/NullCoalescingInitializerClassifierTests.cs` | `NullCoalescingInitializerClassifierTests` | Core | pending | `FullyQualifiedName~NullCoalescingInitializerClassifierTests` |  |
| `src/AiNetLinter.Tests/Core/PlaybookGeneratorRound2Tests.cs` | `PlaybookGeneratorRound2Tests` | Core | pending | `FullyQualifiedName~PlaybookGeneratorRound2Tests` |  |
| `src/AiNetLinter.Tests/Core/ResultPatternNamespaceTests.cs` | `ResultPatternNamespaceTests` | Core | pending | `FullyQualifiedName~ResultPatternNamespaceTests` |  |
| `src/AiNetLinter.Tests/Core/RuleRegistryTests.cs` | `RuleRegistryTests` | Core | pending | `FullyQualifiedName~RuleRegistryTests` |  |
| `src/AiNetLinter.Tests/Core/ScopeImmutabilityTests.cs` | `ScopeImmutabilityTests` | Core | pending | `FullyQualifiedName~ScopeImmutabilityTests` |  |
| `src/AiNetLinter.Tests/Core/StaticTestSentinelExemptionTests.cs` | `StaticTestSentinelExemptionTests` | Core | pending | `FullyQualifiedName~StaticTestSentinelExemptionTests` |  |
| `src/AiNetLinter.Tests/Core/TestCoverageResolverTests.cs` | `TestCoverageResolverTests` | Core | pending | `FullyQualifiedName~TestCoverageResolverTests` |  |
| `src/AiNetLinter.Tests/Core/TestProjectDetectorSuffixTests.cs` | `TestProjectDetectorSuffixTests` | Core | pending | `FullyQualifiedName~TestProjectDetectorSuffixTests` |  |
| `src/AiNetLinter.Tests/Core/ViolationDescriptionTests.cs` | `ViolationDescriptionTests` | Core | pending | `FullyQualifiedName~ViolationDescriptionTests` |  |
| `src/AiNetLinter.Tests/Diagnostics/PerformanceProfilerTests.cs` | `PerformanceProfilerTests` | Diagnostics | pending | `FullyQualifiedName~PerformanceProfilerTests` |  |
| `src/AiNetLinter.Tests/Core/DuplicateDetection/DuplicateDetectionEngineFalsePositiveTests.cs`, `src/AiNetLinter.Tests/Core/DuplicateDetection/DuplicateDetectionEngineTests.cs` | `DuplicateDetectionEngineTests` | DuplicateDetection | migrated | `FullyQualifiedName~DuplicateDetectionEngineTests` | `src/AiNetLinter.FastTests/Core/DuplicateDetection/DuplicateDetectionEngineTests.cs` |
| `src/AiNetLinter.Tests/Core/DuplicateDetection/RefactoringDriftEngineTests.cs` | `RefactoringDriftEngineTests` | DuplicateDetection | migrated | `FullyQualifiedName~RefactoringDriftEngineTests` | `src/AiNetLinter.FastTests/Core/DuplicateDetection/RefactoringDriftEngineTests.cs` |
| `src/AiNetLinter.Tests/FalsePositives/FalsePositiveExtensionsTests.cs` | `FalsePositiveExtensionsTests` | FalsePositives | pending | `FullyQualifiedName~FalsePositiveExtensionsTests` |  |
| `src/AiNetLinter.Tests/FalsePositives/FalsePositiveTests.cs` | `FalsePositiveTests` | FalsePositives | pending | `FullyQualifiedName~FalsePositiveTests` |  |
| `src/AiNetLinter.Tests/Fixtures/LoadFixtureBuilderTests.cs` | `LoadFixtureBuilderTests` | Fixtures | pending | `FullyQualifiedName~LoadFixtureBuilderTests` |  |
| `src/AiNetLinter.Tests/Fixtures/LoadFixtureMeasurementsTests.cs` | `LoadFixtureMeasurementsTests` | Fixtures | pending | `FullyQualifiedName~LoadFixtureMeasurementsTests` |  |
| `src/AiNetLinter.Tests/Fixtures/TD016aRefactorTests.cs` | `TD016aRefactorTests` | Fixtures | pending | `FullyQualifiedName~TD016aRefactorTests` |  |
| `src/AiNetLinter.Tests/Maps/HotspotMapBuilderTests.cs` | `HotspotMapBuilderTests` | Maps | pending | `FullyQualifiedName~HotspotMapBuilderTests` |  |
| `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonMapBuilderTests.cs` | `SkeletonMapBuilderTests` | Maps | migrated | `FullyQualifiedName~SkeletonMapBuilderTests` | `src/AiNetLinter.IntegrationTests/Maps/Skeleton/SkeletonMapBuilderAdapterTests.cs` |
| `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonStableIdTests.cs` | `SkeletonStableIdTests` | Maps | pending | `FullyQualifiedName~SkeletonStableIdTests` |  |
| `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonSyntaxWalkerTests.cs` | `SkeletonSyntaxWalkerTests` | Maps | pending | `FullyQualifiedName~SkeletonSyntaxWalkerTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/CallGraphTraversalTests.cs` | `CallGraphTraversalTests` | Mcp | pending | `FullyQualifiedName~CallGraphTraversalTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/CallTreeMermaidRendererTests.cs` | `CallTreeMermaidRendererTests` | Mcp | migrated | `FullyQualifiedName~CallTreeMermaidRendererTests` | `src/AiNetLinter.FastTests/Mcp/Tools/CallTreeMermaidRendererTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/DependencyGraphScannerTests.cs` | `DependencyGraphScannerTests` | Mcp | pending | `FullyQualifiedName~DependencyGraphScannerTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/DependencyGraphToolTests.cs` | `DependencyGraphToolTests` | Mcp | pending | `FullyQualifiedName~DependencyGraphToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/DiRegistrationHeuristicsTests.cs` | `DiRegistrationHeuristicsTests` | Mcp | pending | `FullyQualifiedName~DiRegistrationHeuristicsTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionScannerTests.cs` | `DuplicateDetectionScannerTests` | Mcp | migrated | `FullyQualifiedName~DuplicateDetectionScannerTests` | `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionScannerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionToolRefactoringDriftTests.cs` | `DuplicateDetectionToolRefactoringDriftTests` | Mcp | pending | `FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionToolTests.cs` | `DuplicateDetectionToolTests` | Mcp | pending | `FullyQualifiedName~DuplicateDetectionToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/SymbolGraph/FindReferencesToolTests.cs` | `FindReferencesToolTests` | Mcp | pending | `FullyQualifiedName~FindReferencesToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs` | `FindSymbolScannerTests` | Mcp | pending | `FullyQualifiedName~FindSymbolScannerTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` | `FindSymbolToolTests` | Mcp | pending | `FullyQualifiedName~FindSymbolToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/GetCallTreeToolTests.cs` | `GetCallTreeToolTests` | Mcp | pending | `FullyQualifiedName~GetCallTreeToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/GetFileSkeletonToolTests.cs` | `GetFileSkeletonToolTests` | Mcp | pending | `FullyQualifiedName~GetFileSkeletonToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs` | `GetHotspotsToolTests` | Mcp | pending | `FullyQualifiedName~GetHotspotsToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/SymbolGraph/GetImpactToolTests.cs` | `GetImpactToolTests` | Mcp | pending | `FullyQualifiedName~GetImpactToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs` | `GetIndexScopeToolTests` | Mcp | pending | `FullyQualifiedName~GetIndexScopeToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/GetServerHealthToolTests.cs` | `GetServerHealthToolTests` | Mcp | pending | `FullyQualifiedName~GetServerHealthToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/GetSymbolBodyToolTests.cs` | `GetSymbolBodyToolTests` | Mcp | pending | `FullyQualifiedName~GetSymbolBodyToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs` | `GetTypeHierarchyToolTests` | Mcp | pending | `FullyQualifiedName~GetTypeHierarchyToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs` | `GetViolationsToolTests` | Mcp | pending | `FullyQualifiedName~GetViolationsToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs` | `McpCallLogTests` | Mcp | pending | `FullyQualifiedName~McpCallLogTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerConstructorTests.cs` | `McpCodeGraphServerConstructorTests` | Mcp | pending | `FullyQualifiedName~McpCodeGraphServerConstructorTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerFileDiscoveryTests.cs` | `McpCodeGraphServerFileDiscoveryTests` | Mcp | pending | `FullyQualifiedName~McpCodeGraphServerFileDiscoveryTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerStalenessMtimeCacheTests.cs` | `McpCodeGraphServerStalenessMtimeCacheTests` | Mcp | pending | `FullyQualifiedName~McpCodeGraphServerStalenessMtimeCacheTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerTests.cs` | `McpCodeGraphServerTests` | Mcp | pending | `FullyQualifiedName~McpCodeGraphServerTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs` | `McpDocumentationSmokeTests` | Mcp | pending | `FullyQualifiedName~McpDocumentationSmokeTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs` | `McpLiveRepositoryTests` | Mcp | pending | `FullyQualifiedName~McpLiveRepositoryTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpServerAllToolsE2ETests.cs` | `McpServerAllToolsE2ETests` | Mcp | pending | `FullyQualifiedName~McpServerAllToolsE2ETests` |  |
| `src/AiNetLinter.Tests/Mcp/McpServerCommandJsonRpcFramingTests.cs` | `McpServerCommandJsonRpcFramingTests` | Mcp | pending | `FullyQualifiedName~McpServerCommandJsonRpcFramingTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpServerOptionsBuilderTests.cs` | `McpServerOptionsBuilderTests` | Mcp | pending | `FullyQualifiedName~McpServerOptionsBuilderTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs` | `McpServerOptionsFactoryTests` | Mcp | pending | `FullyQualifiedName~McpServerOptionsFactoryTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpTestClientParallelTests.cs` | `McpTestClientParallelTests` | Mcp | pending | `FullyQualifiedName~McpTestClientParallelTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpTestClientRetryTests.cs` | `McpTestClientRetryTests` | Mcp | pending | `FullyQualifiedName~McpTestClientRetryTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpToolResultsTests.cs` | `McpToolResultsTests` | Mcp | pending | `FullyQualifiedName~McpToolResultsTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRendererTests.cs` | `MetricsTreeRendererTests` | Mcp | migrated | `FullyQualifiedName~MetricsTreeRendererTests` | `src/AiNetLinter.FastTests/Mcp/Tools/MetricsTreeRendererTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRoslynScannerTests.cs` | `MetricsTreeRoslynScannerTests` | Mcp | pending | `FullyQualifiedName~MetricsTreeRoslynScannerTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeToolTests.cs` | `MetricsTreeToolTests` | Mcp | pending | `FullyQualifiedName~MetricsTreeToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/OverviewResourceRegistrationTests.cs` | `OverviewResourceRegistrationTests` | Mcp | pending | `FullyQualifiedName~OverviewResourceRegistrationTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/PatternDetectScannerTests.cs` | `PatternDetectScannerTests` | Mcp | pending | `FullyQualifiedName~PatternDetectScannerTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/PatternDetectToolTests.cs` | `PatternDetectToolTests` | Mcp | pending | `FullyQualifiedName~PatternDetectToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/RefactoringDriftScannerTests.cs` | `RefactoringDriftScannerTests` | Mcp | migrated | `FullyQualifiedName~RefactoringDriftScannerTests` | `src/AiNetLinter.FastTests/Mcp/Tools/RefactoringDriftScannerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/ReloadConfigToolTests.cs` | `ReloadConfigToolTests` | Mcp | pending | `FullyQualifiedName~ReloadConfigToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/SafeguardScannerTests.cs` | `SafeguardScannerTests` | Mcp | pending | `FullyQualifiedName~SafeguardScannerTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/SafeguardToolTests.cs` | `SafeguardToolTests` | Mcp | pending | `FullyQualifiedName~SafeguardToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs` | `SearchPatternToolTests` | Mcp | pending | `FullyQualifiedName~SearchPatternToolTests` |  |
| `src/AiNetLinter.Tests/Mcp/SymbolGraphToolRegistrationsTests.cs` | `SymbolGraphToolRegistrationsTests` | Mcp | pending | `FullyQualifiedName~SymbolGraphToolRegistrationsTests` |  |
| `src/AiNetLinter.Tests/Mcp/Tools/SymbolGraph/SymbolIdentifierResolverTests.cs` | `SymbolIdentifierResolverTests` | Mcp | pending | `FullyQualifiedName~SymbolIdentifierResolverTests` |  |
| `src/AiNetLinter.Tests/Metrics/AIContextFootprintDeduplicationTests.cs` | `AIContextFootprintDeduplicationTests` | Metrics | pending | `FullyQualifiedName~AIContextFootprintDeduplicationTests` |  |
| `src/AiNetLinter.Tests/Metrics/CognitiveComplexityGuidanceTests.cs` | `CognitiveComplexityGuidanceTests` | Metrics | pending | `FullyQualifiedName~CognitiveComplexityGuidanceTests` |  |
| `src/AiNetLinter.Tests/Metrics/CognitiveComplexityWalkerTests.cs` | `CognitiveComplexityWalkerTests` | Metrics | pending | `FullyQualifiedName~CognitiveComplexityWalkerTests` |  |
| `src/AiNetLinter.Tests/Metrics/FileLimitGuidanceTests.cs` | `FileLimitGuidanceTests` | Metrics | pending | `FullyQualifiedName~FileLimitGuidanceTests` |  |
| `src/AiNetLinter.Tests/Metrics/MaxDirectoryChildrenTests.cs` | `MaxDirectoryChildrenTests` | Metrics | pending | `FullyQualifiedName~MaxDirectoryChildrenTests` |  |
| `src/AiNetLinter.Tests/Metrics/MethodLineCounterTests.cs` | `MethodLineCounterTests` | Metrics | pending | `FullyQualifiedName~MethodLineCounterTests` |  |
| `src/AiNetLinter.Tests/Metrics/PostAnalysisChecksPathOverrideTests.cs` | `PostAnalysisChecksPathOverrideTests` | Metrics | pending | `FullyQualifiedName~PostAnalysisChecksPathOverrideTests` |  |
| `src/AiNetLinter.Tests/Output/DebtReportBuilderHeaderTests.cs` | `DebtReportBuilderHeaderTests` | Output | pending | `FullyQualifiedName~DebtReportBuilderHeaderTests` |  |
| `src/AiNetLinter.Tests/Output/DebtReportBuilderTests.cs` | `DebtReportBuilderTests` | Output | pending | `FullyQualifiedName~DebtReportBuilderTests` |  |
| `src/AiNetLinter.Tests/Output/LinterErrorFormatterTests.cs` | `LinterErrorFormatterTests` | Output | pending | `FullyQualifiedName~LinterErrorFormatterTests` |  |
| `src/AiNetLinter.Tests/Output/McpLintConsoleTests.cs` | `McpLintConsoleTests` | Output | pending | `FullyQualifiedName~McpLintConsoleTests` |  |
| `src/AiNetLinter.Tests/Output/OutputRootResolverTests.cs` | `OutputRootResolverTests` | Output | pending | `FullyQualifiedName~OutputRootResolverTests` |  |
| `src/AiNetLinter.Tests/Output/PathNormalizerTests.cs` | `PathNormalizerTests` | Output | pending | `FullyQualifiedName~PathNormalizerTests` |  |
| `src/AiNetLinter.Tests/Output/RuleLegendRegistryTests.cs` | `RuleLegendRegistryTests` | Output | pending | `FullyQualifiedName~RuleLegendRegistryTests` |  |
| `src/AiNetLinter.Tests/Output/ViolationMarkdownFormatterTests.cs` | `ViolationMarkdownFormatterTests` | Output | pending | `FullyQualifiedName~ViolationMarkdownFormatterTests` |  |
| `src/AiNetLinter.Tests/Output/ViolationSummaryBuilderTests.cs` | `ViolationSummaryBuilderTests` | Output | pending | `FullyQualifiedName~ViolationSummaryBuilderTests` |  |
| `src/AiNetLinter.Tests/Suppression/DisableAllCliTests.cs` | `DisableAllCliTests` | Suppression | pending | `FullyQualifiedName~DisableAllCliTests` |  |
| `src/AiNetLinter.Tests/Suppression/DisableAllCommentInjectorTests.cs` | `DisableAllCommentInjectorTests` | Suppression | pending | `FullyQualifiedName~DisableAllCommentInjectorTests` |  |
| `src/AiNetLinter.Tests/Suppression/DisableAllCommentRemoverTests.cs` | `DisableAllCommentRemoverTests` | Suppression | pending | `FullyQualifiedName~DisableAllCommentRemoverTests` |  |
| `src/AiNetLinter.Tests/Suppression/IgnoreSuppressionsFilterTests.cs` | `IgnoreSuppressionsFilterTests` | Suppression | pending | `FullyQualifiedName~IgnoreSuppressionsFilterTests` |  |
| `src/AiNetLinter.Tests/Suppression/SuppressionCommentParserTests.cs` | `SuppressionCommentParserTests` | Suppression | pending | `FullyQualifiedName~SuppressionCommentParserTests` |  |
| `src/AiNetLinter.Tests/Suppression/SuppressionEvaluatorTests.cs` | `SuppressionEvaluatorTests` | Suppression | pending | `FullyQualifiedName~SuppressionEvaluatorTests` |  |
| `src/AiNetLinter.Tests/Suppression/SuppressionFileResolverTests.cs` | `SuppressionFileResolverTests` | Suppression | pending | `FullyQualifiedName~SuppressionFileResolverTests` |  |
| `src/AiNetLinter.Tests/Suppression/SuppressionScannerTests.cs` | `SuppressionScannerTests` | Suppression | pending | `FullyQualifiedName~SuppressionScannerTests` |  |
| `src/AiNetLinter.Tests/Suppression/ViolationPathResolverTests.cs` | `ViolationPathResolverTests` | Suppression | pending | `FullyQualifiedName~ViolationPathResolverTests` |  |
| `src/AiNetLinter.Tests/Web/CssAnalyzerTests.cs` | `CssAnalyzerTests` | Web | migrated | `FullyQualifiedName~CssAnalyzerTests` | `src/AiNetLinter.FastTests/Web/CssAnalyzerTests.cs` |
| `src/AiNetLinter.Tests/Web/JsAnalyzerTests.cs` | `JsAnalyzerTests` | Web | migrated | `FullyQualifiedName~JsAnalyzerTests` | `src/AiNetLinter.FastTests/Web/JsAnalyzerTests.cs` |
| `src/AiNetLinter.Tests/Web/RazorAnalyzerTests.Extended.cs` | `RazorAnalyzerExtendedTests` | Web | migrated | `FullyQualifiedName~RazorAnalyzerExtendedTests` | `src/AiNetLinter.FastTests/Web/RazorAnalyzerTests.Extended.cs` |
| `src/AiNetLinter.Tests/Web/RazorAnalyzerTests.cs` | `RazorAnalyzerTests` | Web | migrated | `FullyQualifiedName~RazorAnalyzerTests` | `src/AiNetLinter.FastTests/Web/RazorAnalyzerTests.cs` |
| `src/AiNetLinter.Tests/Web/WebSuppressionDetectorTests.cs` | `WebSuppressionDetectorTests` | Web | migrated | `FullyQualifiedName~WebSuppressionDetectorTests` | `src/AiNetLinter.FastTests/Web/WebSuppressionDetectorTests.cs` |
