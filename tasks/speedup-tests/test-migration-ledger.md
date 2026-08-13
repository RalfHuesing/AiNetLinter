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
| `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs` | `ArchitectureTests` | Architecture | migrated | `FullyQualifiedName~ArchitectureTests` | `src/AiNetLinter.FastTests/Core/LinterAnalyzerArchitectureRuleTests.cs` |
| `src/AiNetLinter.Tests/Baseline/BaselineCliTests.cs` | `BaselineCliTests` | Baseline | migrated | `FullyQualifiedName~BaselineCliTests` | `src/AiNetLinter.IntegrationTests/Baseline/BaselineCliTests.cs` |
| `src/AiNetLinter.Tests/Baseline/BaselineComparerTests.cs` | `BaselineComparerTests` | Baseline | migrated | `FullyQualifiedName~BaselineComparerTests` | `src/AiNetLinter.FastTests/Baseline/BaselineComparerTests.cs` |
| `src/AiNetLinter.Tests/Baseline/BaselineReaderWriterTests.cs` | `BaselineReaderWriterTests` | Baseline | migrated | `FullyQualifiedName~BaselineReaderWriterTests` | `src/AiNetLinter.IntegrationTests/Baseline/BaselineReaderWriterTests.cs` |
| `src/AiNetLinter.Tests/Baseline/BaselineViolationFilterTests.cs` | `BaselineViolationFilterTests` | Baseline | migrated | `FullyQualifiedName~BaselineViolationFilterTests` | `src/AiNetLinter.FastTests/Baseline/BaselineViolationFilterTests.cs` |
| `src/AiNetLinter.Tests/Baseline/FileChecksumCalculatorTests.cs` | `FileChecksumCalculatorTests` | Baseline | migrated | `FullyQualifiedName~FileChecksumCalculatorTests` | `src/AiNetLinter.FastTests/Baseline/FileChecksumCalculatorTests.cs` |
| `src/AiNetLinter.Tests/Baseline/FileSystemExclusionHelpersTests.cs` | `FileSystemExclusionHelpersTests` | Baseline | migrated | `FullyQualifiedName~FileSystemExclusionHelpersTests` | `src/AiNetLinter.IntegrationTests/Baseline/FileSystemExclusionHelpersTests.cs` |
| `src/AiNetLinter.Tests/Baseline/ProjectRestoreStateTests.cs` | `ProjectRestoreStateTests` | Baseline | migrated | `FullyQualifiedName~ProjectRestoreStateTests` | `src/AiNetLinter.IntegrationTests/Baseline/ProjectRestoreStateTests.cs` |
| `src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs` | `SourceFileCatalogBlazorPartialTests` | Baseline | migrated | `FullyQualifiedName~SourceFileCatalogBlazorPartialTests` | `src/AiNetLinter.IntegrationTests/Baseline/SourceFileCatalogBlazorPartialTests.cs` |
| `src/AiNetLinter.Tests/Baseline/SourceFileCatalogRegisterMSBuildTests.cs` | `SourceFileCatalogRegisterMSBuildTests` | Baseline | pending | `FullyQualifiedName~SourceFileCatalogRegisterMSBuildTests` |  |
| `src/AiNetLinter.Tests/Baseline/SourceFileCatalogTests.cs` | `SourceFileCatalogTests` | Baseline | migrated | `FullyQualifiedName~SourceFileCatalogTests` | `src/AiNetLinter.IntegrationTests/Baseline/SourceFileCatalogAdapterTests.cs` |
| `src/AiNetLinter.Tests/Baseline/WebBaselineTests.cs` | `WebBaselineTests` | Baseline | migrated | `FullyQualifiedName~WebBaselineTests` | `src/AiNetLinter.IntegrationTests/Baseline/WebBaselineTests.cs` |
| `src/AiNetLinter.Tests/Cache/AnalysisCacheManagerIsolationTests.cs` | `AnalysisCacheManagerIsolationTests` | Cache | migrated | `FullyQualifiedName~AnalysisCacheManagerIsolationTests` | `src/AiNetLinter.IntegrationTests/Cache/AnalysisCacheManagerIsolationTests.cs` |
| `src/AiNetLinter.Tests/Cache/AnalysisCacheManagerTests.cs` | `AnalysisCacheManagerTests` | Cache | migrated | `FullyQualifiedName~AnalysisCacheManagerTests` | `src/AiNetLinter.IntegrationTests/Cache/AnalysisCacheManagerTests.cs` |
| `src/AiNetLinter.Tests/Cache/CacheEntryMapperTests.cs` | `CacheEntryMapperTests` | Cache | migrated | `FullyQualifiedName~CacheEntryMapperTests` | `src/AiNetLinter.FastTests/Cache/CacheEntryMapperTests.cs` |
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
| `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsCliTests.cs` | `IgnoreSuppressionsCliTests` | Cli | migrated | `FullyQualifiedName~IgnoreSuppressionsCliTests` | `src/AiNetLinter.FastTests/Cli/IgnoreSuppressionsCliTests.cs` |
| `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsIntegrationTests.cs` | `IgnoreSuppressionsIntegrationTests` | Cli | migrated | `FullyQualifiedName~IgnoreSuppressionsIntegrationTests` | `src/AiNetLinter.FastTests/Cli/IgnoreSuppressionsIntegrationTests.cs` |
| `src/AiNetLinter.Tests/Cli/ProgramTests.cs` | `ProgramTests` | Cli | pending | `FullyQualifiedName~ProgramTests` |  |
| `src/AiNetLinter.Tests/Commands/AuditCommandTests.cs` | `AuditCommandTests` | Commands | pending | `FullyQualifiedName~AuditCommandTests` |  |
| `src/AiNetLinter.Tests/Commands/CliBatchRegressionTests.cs` | `CliBatchRegressionTests` | Commands | pending | `FullyQualifiedName~CliBatchRegressionTests` |  |
| `src/AiNetLinter.Tests/Commands/DocsCommandTests.cs` | `DocsCommandTests` | Commands | pending | `FullyQualifiedName~DocsCommandTests` |  |
| `src/AiNetLinter.Tests/Commands/ListRulesCommandTests.cs` | `ListRulesCommandTests` | Commands | pending | `FullyQualifiedName~ListRulesCommandTests` |  |
| `src/AiNetLinter.Tests/Commands/McpServerCommandAmbiguityE2ETests.cs` | `McpServerCommandAmbiguityE2ETests` | Commands | migrated | `FullyQualifiedName~McpServerCommandAmbiguityE2ETests` | `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandAmbiguityE2ETests.cs` — 1 Integrationsfall, exklusiver Startfehler, 121er-Matrix/E2E-Gate. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandCacheBypassTests.cs` | `McpServerCommandCacheBypassTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandCacheBypassTests` | `src/AiNetLinter.FastTests/Mcp/McpServerCommandCacheBypassTests.cs` — 1 Unit-Fall ohne Prozess, 121er-Matrix/Fast-Gate. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs` | `McpServerCommandCallLogTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandCallLogTests` | `src/AiNetLinter.FastTests/Mcp/McpServerCommandCallLogTests.cs` — 9 Unit-Fälle für Pfad-/Fehlerzweige, 121er-Matrix/CallLog-Gate. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandErrorHandlingTests.cs` | `McpServerCommandErrorHandlingTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandErrorHandlingTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandErrorHandlingTests.cs` — 2 Integrationsfehlerfälle mit eigenem Host, 121er-Matrix. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandFindReferencesTests.cs` | `McpServerCommandFindReferencesTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandFindReferencesTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandFindReferencesTests.cs` — 1 read-only Integrationsfall, 121er-Matrix. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs` | `McpServerCommandFindSymbolTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandFindSymbolTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandFindSymbolTests.cs` — 1 read-only Integrationsfall, 121er-Matrix. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandGetImpactTests.cs` | `McpServerCommandGetImpactTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandGetImpactTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandGetImpactTests.cs` — 2 Integrationszweige (Symbol/Git), 121er-Matrix. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs` | `McpServerCommandLoadingStateTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandLoadingStateTests` | `src/AiNetLinter.FastTests/Mcp/McpServerCommandLoadingStateTests.cs` — 3 Component-Fälle ohne MSBuild/Prozess, 121er-Matrix/Fast-Gate. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandMissHintTests.cs` | `McpServerCommandMissHintTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandMissHintTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandMissHintTests.cs` — 1 read-only Integrationsfall, 121er-Matrix. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandStalenessTests.cs` | `McpServerCommandStalenessTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandStalenessTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandStalenessTests.cs` — 1 mutierender Integrationsfall mit exklusivem Host, 121er-Matrix. |
| `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` | `McpServerCommandTests` | Commands | migrated | `FullyQualifiedName~McpServerCommandTests` | `src/AiNetLinter.FastTests/Mcp/McpServerCommandTests.cs` (10 reine Fälle) + `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs` (13 Hostfälle) — 121er-Matrix/Fast- und Integration-Gate. |
| `src/AiNetLinter.Tests/Commands/PlaybookCheckCommandTests.cs` | `PlaybookCheckCommandTests` | Commands | pending | `FullyQualifiedName~PlaybookCheckCommandTests` |  |
| `src/AiNetLinter.Tests/Commands/SyncAgentRulesCommandTests.cs` | `SyncAgentRulesCommandTests` | Commands | pending | `FullyQualifiedName~SyncAgentRulesCommandTests` |  |
| `src/AiNetLinter.Tests/Configuration/AgentFeaturesTests.cs` | `AgentFeaturesTests` | Configuration | migrated | `FullyQualifiedName~AgentFeaturesTests` | `src/AiNetLinter.FastTests/Configuration/AgentFeaturesTests.cs` |
| `src/AiNetLinter.Tests/Configuration/ConfigLoaderRulesJsonTests.cs` | `ConfigLoaderRulesJsonTests` | Configuration | migrated | `FullyQualifiedName~ConfigLoaderRulesJsonTests` | `src/AiNetLinter.IntegrationTests/Configuration/ConfigLoaderRulesJsonTests.cs` |
| `src/AiNetLinter.Tests/Configuration/ConfigNormalizerTests.cs` | `ConfigNormalizerTests` | Configuration | migrated | `FullyQualifiedName~ConfigNormalizerTests` | `src/AiNetLinter.FastTests/Configuration/ConfigNormalizerTests.cs` |
| `src/AiNetLinter.Tests/Configuration/ConfigSyncerTests.cs` | `ConfigSyncerTests` | Configuration | migrated | `FullyQualifiedName~ConfigSyncerTests` | `src/AiNetLinter.IntegrationTests/Configuration/ConfigSyncerTests.cs` |
| `src/AiNetLinter.Tests/Configuration/DeveloperExperienceTests.cs` | `DeveloperExperienceTests` | Configuration | consolidated | `FullyQualifiedName~DeveloperExperienceTests` | `src/AiNetLinter.IntegrationTests/Configuration/DeveloperExperienceTests.cs` |
| `src/AiNetLinter.Tests/Configuration/FileFilterEvaluatorTests.cs` | `FileFilterEvaluatorTests` | Configuration | migrated | `FullyQualifiedName~FileFilterEvaluatorTests` | `src/AiNetLinter.FastTests/Configuration/FileFilterEvaluatorTests.cs` |
| `src/AiNetLinter.Tests/Configuration/PathOverridesTests.cs` | `PathOverridesTests` | Configuration | migrated | `FullyQualifiedName~PathOverridesTests` | `src/AiNetLinter.FastTests/Configuration/PathOverridesTests.cs` |
| `src/AiNetLinter.Tests/Configuration/RuleMetadataRegistryTests.cs` | `RuleMetadataRegistryTests` | Configuration | migrated | `FullyQualifiedName~RuleMetadataRegistryTests` | `src/AiNetLinter.FastTests/Configuration/RuleMetadataRegistryTests.cs` |
| `src/AiNetLinter.Tests/Core/AutoFixerTests.cs` | `AutoFixerTests` | Core | pending | `FullyQualifiedName~AutoFixerTests` |  |
| `src/AiNetLinter.Tests/Core/ClassInfoCollectorTests.cs` | `ClassInfoCollectorTests` | Core | pending | `FullyQualifiedName~ClassInfoCollectorTests` |  |
| `src/AiNetLinter.Tests/Core/CompoundSuppressionEvaluatorTests.cs` | `CompoundSuppressionEvaluatorTests` | Core | migrated | `FullyQualifiedName~CompoundSuppressionEvaluatorTests` | `src/AiNetLinter.FastTests/Core/CompoundSuppressionEvaluatorTests.cs` |
| `src/AiNetLinter.Tests/Core/CompoundSuppressionIntegrationTests.cs` | `CompoundSuppressionIntegrationTests` | Core | migrated | `FullyQualifiedName~CompoundSuppressionIntegrationTests` | `src/AiNetLinter.FastTests/Core/CompoundSuppressionIntegrationTests.cs` |
| `src/AiNetLinter.Tests/Core/ControlFlowResilienceTests.cs` | `ControlFlowResilienceTests` | Core | pending | `FullyQualifiedName~ControlFlowResilienceTests` |  |
| `src/AiNetLinter.Tests/Core/DiffImpactAnalyzerTests.cs` | `DiffImpactAnalyzerTests` | Core | pending | `FullyQualifiedName~DiffImpactAnalyzerTests` |  |
| `src/AiNetLinter.Tests/Core/LinterAnalyzerTests.cs` | `LinterAnalyzerTests` | Core | migrated | `FullyQualifiedName~LinterAnalyzerTests` | `src/AiNetLinter.FastTests/Core/LinterAnalyzerTests.cs` |
| `src/AiNetLinter.Tests/Core/LinterEngineCacheTests.cs` | `LinterEngineCacheTests` | Core | migrated | `FullyQualifiedName~LinterEngineCacheTests` | `src/AiNetLinter.IntegrationTests/Core/LinterEngineCacheTests.cs` |
| `src/AiNetLinter.Tests/Core/LinterEngineProjectRestoreTests.cs` | `LinterEngineProjectRestoreTests` | Core | migrated | `FullyQualifiedName~LinterEngineProjectRestoreTests` | `src/AiNetLinter.IntegrationTests/Core/LinterEngineProjectRestoreTests.cs` |
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
| `src/AiNetLinter.Tests/Mcp/Tools/CallGraphTraversalTests.cs` | `CallGraphTraversalTests` | Mcp | migrated | `FullyQualifiedName~CallGraphTraversalTests` | `src/AiNetLinter.FastTests/Mcp/Tools/CallGraphTraversalTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/CallTreeMermaidRendererTests.cs` | `CallTreeMermaidRendererTests` | Mcp | migrated | `FullyQualifiedName~CallTreeMermaidRendererTests` | `src/AiNetLinter.FastTests/Mcp/Tools/CallTreeMermaidRendererTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/DependencyGraphScannerTests.cs` | `DependencyGraphScannerTests` | Mcp | migrated | `FullyQualifiedName~DependencyGraphScannerTests` | `src/AiNetLinter.FastTests/Mcp/Tools/DependencyGraphScannerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/DependencyGraphToolTests.cs` | `DependencyGraphToolTests` | Mcp | migrated | `FullyQualifiedName~DependencyGraphToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/DependencyGraphToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/DiRegistrationHeuristicsTests.cs` | `DiRegistrationHeuristicsTests` | Mcp | migrated | `FullyQualifiedName~DiRegistrationHeuristicsTests` | `src/AiNetLinter.FastTests/Mcp/Tools/DiRegistrationHeuristicsTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionScannerTests.cs` | `DuplicateDetectionScannerTests` | Mcp | migrated | `FullyQualifiedName~DuplicateDetectionScannerTests` | `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionScannerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionToolRefactoringDriftTests.cs` | `DuplicateDetectionToolRefactoringDriftTests` | Mcp | migrated | `FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests` | `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionToolRefactoringDriftTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionToolTests.cs` | `DuplicateDetectionToolTests` | Mcp | migrated | `FullyQualifiedName~DuplicateDetectionToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/SymbolGraph/FindReferencesToolTests.cs` | `FindReferencesToolTests` | Mcp | migrated | `FullyQualifiedName~FindReferencesToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/FindReferencesToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs` | `FindSymbolScannerTests` | Mcp | migrated | `FullyQualifiedName~FindSymbolScannerTests` | `src/AiNetLinter.FastTests/Mcp/Tools/FindSymbolScannerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` | `FindSymbolToolTests` | Mcp | migrated | `FullyQualifiedName~FindSymbolToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/FindSymbolToolTests.cs` |

> **Find-Symbol-Coverage (step-020):** Die zwei historischen Plain-No-Match-Methoden werden durch den verbleibenden Scannervertrag in `src/AiNetLinter.IntegrationTests/Mcp/Tools/FindSymbolFileAdapterTests.cs` semantisch konsolidiert. Damit bilden 20 historische Methoden 19 einzigartige Verträge ab (elf FastTests, acht IntegrationTests); die maschinell geprüften Zielpfade und Statuswerte der beiden Zeilen bleiben unverändert.

| `src/AiNetLinter.Tests/Mcp/Tools/GetCallTreeToolTests.cs` | `GetCallTreeToolTests` | Mcp | migrated | `FullyQualifiedName~GetCallTreeToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/GetCallTreeToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/GetFileSkeletonToolTests.cs` | `GetFileSkeletonToolTests` | Mcp | migrated | `FullyQualifiedName~GetFileSkeletonToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/GetFileSkeletonToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs` | `GetHotspotsToolTests` | Mcp | migrated | `FullyQualifiedName~GetHotspotsToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/GetHotspotsToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/SymbolGraph/GetImpactToolTests.cs` | `GetImpactToolTests` | Mcp | migrated | `FullyQualifiedName~GetImpactToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/GetImpactToolTests.cs` (9 Component-Fälle) + `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs` (5 Git-/CompileError-Fälle) — 121er-Matrix. |
| `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs` | `GetIndexScopeToolTests` | Mcp | migrated | `FullyQualifiedName~GetIndexScopeToolTests` | `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetIndexScopeToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/GetServerHealthToolTests.cs` | `GetServerHealthToolTests` | Mcp | migrated | `FullyQualifiedName~GetServerHealthToolTests` | `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetServerHealthToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/GetSymbolBodyToolTests.cs` | `GetSymbolBodyToolTests` | Mcp | migrated | `FullyQualifiedName~GetSymbolBodyToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/GetSymbolBodyToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs` | `GetTypeHierarchyToolTests` | Mcp | migrated | `FullyQualifiedName~GetTypeHierarchyToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/GetTypeHierarchyToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs` | `GetViolationsToolTests` | Mcp | migrated | `FullyQualifiedName~GetViolationsToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/GetViolationsToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs` | `McpCallLogTests` | Mcp | migrated | `FullyQualifiedName~McpCallLogTests` | `src/AiNetLinter.FastTests/Mcp/McpCallLogTests.cs` — 14 Unit-Fälle für Erfolg, Negativ- und Parallelpfade, 121er-Matrix/CallLog-Gate. |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerConstructorTests.cs` | `McpCodeGraphServerConstructorTests` | Mcp | migrated | `FullyQualifiedName~McpCodeGraphServerConstructorTests` | `src/AiNetLinter.FastTests/Mcp/McpCodeGraphServerConstructorTests.cs` — 2 Unit-Fälle, 121er-Matrix/Fast-Gate. |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerFileDiscoveryTests.cs` | `McpCodeGraphServerFileDiscoveryTests` | Mcp | migrated | `FullyQualifiedName~McpCodeGraphServerFileDiscoveryTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpCodeGraphServerFileDiscoveryTests.cs` |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerStalenessMtimeCacheTests.cs` | `McpCodeGraphServerStalenessMtimeCacheTests` | Mcp | migrated | `FullyQualifiedName~McpCodeGraphServerStalenessMtimeCacheTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpCodeGraphServerStalenessMtimeCacheTests.cs` |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerTests.cs` | `McpCodeGraphServerTests` | Mcp | migrated | `FullyQualifiedName~McpCodeGraphServerTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpCodeGraphServerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs` | `McpDocumentationSmokeTests` | Mcp | pending | `FullyQualifiedName~McpDocumentationSmokeTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs` | `McpLiveRepositoryTests` | Mcp | pending | `FullyQualifiedName~McpLiveRepositoryTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpServerAllToolsE2ETests.cs` | `McpServerAllToolsE2ETests` | Mcp | migrated | `FullyQualifiedName~McpServerAllToolsE2ETests` | `src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs` — 24 read-only Integrationsfälle, 121er-Matrix/Read-only-Gate. |
| `src/AiNetLinter.Tests/Mcp/McpServerCommandJsonRpcFramingTests.cs` | `McpServerCommandJsonRpcFramingTests` | Mcp | migrated | `FullyQualifiedName~McpServerCommandJsonRpcFramingTests` | `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandJsonRpcFramingTests.cs` — 3 exklusive Stdio-Fälle, 121er-Matrix/Framing-Wiederholung. |
| `src/AiNetLinter.Tests/Mcp/McpServerOptionsBuilderTests.cs` | `McpServerOptionsBuilderTests` | Mcp | migrated | `FullyQualifiedName~McpServerOptionsBuilderTests` | `src/AiNetLinter.FastTests/Mcp/McpServerOptionsBuilderTests.cs` — 9 Unit-Fälle, 121er-Matrix/Fast-Gate. |
| `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs` | `McpServerOptionsFactoryTests` | Mcp | migrated | `FullyQualifiedName~McpServerOptionsFactoryTests` | `src/AiNetLinter.FastTests/Mcp/McpServerOptionsFactoryTests.cs` — 1 Unit-Fall, 121er-Matrix/Fast-Gate. |
| `src/AiNetLinter.Tests/Mcp/McpTestClientParallelTests.cs` | `McpTestClientParallelTests` | Mcp | pending | `FullyQualifiedName~McpTestClientParallelTests` |  |
| `src/AiNetLinter.Tests/Mcp/McpTestClientRetryTests.cs` | `McpTestClientRetryTests` | Mcp | migrated | `FullyQualifiedName~McpTestClientRetryTests` | `src/AiNetLinter.FastTests/Mcp/McpTestClientRetryOptionsTests.cs` (2 Optionsfälle) + `src/AiNetLinter.IntegrationTests/Mcp/McpTestClientRetryTests.cs` (1 erschöpfter Connect-Retry) — 121er-Matrix/Retry-Gate. |
| `src/AiNetLinter.Tests/Mcp/McpToolResultsTests.cs` | `McpToolResultsTests` | Mcp | migrated | `FullyQualifiedName~McpToolResultsTests` | `src/AiNetLinter.FastTests/Mcp/McpToolResultsTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRendererTests.cs` | `MetricsTreeRendererTests` | Mcp | migrated | `FullyQualifiedName~MetricsTreeRendererTests` | `src/AiNetLinter.FastTests/Mcp/Tools/MetricsTreeRendererTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRoslynScannerTests.cs` | `MetricsTreeRoslynScannerTests` | Mcp | migrated | `FullyQualifiedName~MetricsTreeRoslynScannerTests` | `src/AiNetLinter.FastTests/Mcp/Tools/MetricsTreeRoslynScannerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeToolTests.cs` | `MetricsTreeToolTests` | Mcp | migrated | `FullyQualifiedName~MetricsTreeToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/MetricsTreeToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/OverviewResourceRegistrationTests.cs` | `OverviewResourceRegistrationTests` | Mcp | migrated | `FullyQualifiedName~OverviewResourceRegistrationTests` | `src/AiNetLinter.FastTests/Mcp/OverviewResourceRegistrationTests.cs` — 5 Unit-Fälle, 121er-Matrix/Fast-Gate. |
| `src/AiNetLinter.Tests/Mcp/Tools/PatternDetectScannerTests.cs` | `PatternDetectScannerTests` | Mcp | migrated | `FullyQualifiedName~PatternDetectScannerTests` | `src/AiNetLinter.FastTests/Mcp/Tools/PatternDetectScannerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/PatternDetectToolTests.cs` | `PatternDetectToolTests` | Mcp | migrated | `FullyQualifiedName~PatternDetectToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/PatternDetectToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/RefactoringDriftScannerTests.cs` | `RefactoringDriftScannerTests` | Mcp | migrated | `FullyQualifiedName~RefactoringDriftScannerTests` | `src/AiNetLinter.FastTests/Mcp/Tools/RefactoringDriftScannerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/ReloadConfigToolTests.cs` | `ReloadConfigToolTests` | Mcp | migrated | `FullyQualifiedName~ReloadConfigToolTests` | `src/AiNetLinter.IntegrationTests/Mcp/Tools/ReloadConfigToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/SafeguardScannerTests.cs` | `SafeguardScannerTests` | Mcp | migrated | `FullyQualifiedName~SafeguardScannerTests` | `src/AiNetLinter.FastTests/Mcp/Tools/SafeguardScannerTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/SafeguardToolTests.cs` | `SafeguardToolTests` | Mcp | migrated | `FullyQualifiedName~SafeguardToolTests` | `src/AiNetLinter.FastTests/Mcp/Tools/SafeguardToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs` | `SearchPatternToolTests` | Mcp | migrated | `FullyQualifiedName~SearchPatternToolTests` | `src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternToolTests.cs` |
| `src/AiNetLinter.Tests/Mcp/SymbolGraphToolRegistrationsTests.cs` | `SymbolGraphToolRegistrationsTests` | Mcp | migrated | `FullyQualifiedName~SymbolGraphToolRegistrationsTests` | `src/AiNetLinter.FastTests/Mcp/SymbolGraphToolRegistrationsTests.cs` — 1 Unit-Fall, 121er-Matrix/Fast-Gate. |
| `src/AiNetLinter.Tests/Mcp/Tools/SymbolGraph/SymbolIdentifierResolverTests.cs` | `SymbolIdentifierResolverTests` | Mcp | migrated | `FullyQualifiedName~SymbolIdentifierResolverTests` | `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/SymbolIdentifierResolverTests.cs` |
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
| `src/AiNetLinter.Tests/Suppression/DisableAllCliTests.cs` | `DisableAllCliTests` | Suppression | migrated | `FullyQualifiedName~DisableAllCliTests` | `src/AiNetLinter.IntegrationTests/Suppression/DisableAllCliTests.cs` |
| `src/AiNetLinter.Tests/Suppression/DisableAllCommentInjectorTests.cs` | `DisableAllCommentInjectorTests` | Suppression | migrated | `FullyQualifiedName~DisableAllCommentInjectorTests` | `src/AiNetLinter.IntegrationTests/Suppression/DisableAllCommentInjectorTests.cs` |
| `src/AiNetLinter.Tests/Suppression/DisableAllCommentRemoverTests.cs` | `DisableAllCommentRemoverTests` | Suppression | migrated | `FullyQualifiedName~DisableAllCommentRemoverTests` | `src/AiNetLinter.IntegrationTests/Suppression/DisableAllCommentRemoverTests.cs` |
| `src/AiNetLinter.Tests/Suppression/IgnoreSuppressionsFilterTests.cs` | `IgnoreSuppressionsFilterTests` | Suppression | migrated | `FullyQualifiedName~IgnoreSuppressionsFilterTests` | `src/AiNetLinter.FastTests/Suppression/IgnoreSuppressionsFilterTests.cs` |
| `src/AiNetLinter.Tests/Suppression/SuppressionCommentParserTests.cs` | `SuppressionCommentParserTests` | Suppression | migrated | `FullyQualifiedName~SuppressionCommentParserTests` | `src/AiNetLinter.FastTests/Suppression/SuppressionCommentParserTests.cs` |
| `src/AiNetLinter.Tests/Suppression/SuppressionEvaluatorTests.cs` | `SuppressionEvaluatorTests` | Suppression | migrated | `FullyQualifiedName~SuppressionEvaluatorTests` | `src/AiNetLinter.FastTests/Suppression/SuppressionEvaluatorTests.cs` |
| `src/AiNetLinter.Tests/Suppression/SuppressionFileResolverTests.cs` | `SuppressionFileResolverTests` | Suppression | migrated | `FullyQualifiedName~SuppressionFileResolverTests` | `src/AiNetLinter.IntegrationTests/Suppression/SuppressionFileResolverTests.cs` |
| `src/AiNetLinter.Tests/Suppression/SuppressionScannerTests.cs` | `SuppressionScannerTests` | Suppression | migrated | `FullyQualifiedName~SuppressionScannerTests` | `src/AiNetLinter.IntegrationTests/Suppression/SuppressionScannerTests.cs` |
| `src/AiNetLinter.Tests/Suppression/ViolationPathResolverTests.cs` | `ViolationPathResolverTests` | Suppression | migrated | `FullyQualifiedName~ViolationPathResolverTests` | `src/AiNetLinter.IntegrationTests/Suppression/ViolationPathResolverTests.cs` |
| `src/AiNetLinter.Tests/Web/CssAnalyzerTests.cs` | `CssAnalyzerTests` | Web | migrated | `FullyQualifiedName~CssAnalyzerTests` | `src/AiNetLinter.FastTests/Web/CssAnalyzerTests.cs` |
| `src/AiNetLinter.Tests/Web/JsAnalyzerTests.cs` | `JsAnalyzerTests` | Web | migrated | `FullyQualifiedName~JsAnalyzerTests` | `src/AiNetLinter.FastTests/Web/JsAnalyzerTests.cs` |
| `src/AiNetLinter.Tests/Web/RazorAnalyzerTests.Extended.cs` | `RazorAnalyzerExtendedTests` | Web | migrated | `FullyQualifiedName~RazorAnalyzerExtendedTests` | `src/AiNetLinter.FastTests/Web/RazorAnalyzerTests.Extended.cs` |
| `src/AiNetLinter.Tests/Web/RazorAnalyzerTests.cs` | `RazorAnalyzerTests` | Web | migrated | `FullyQualifiedName~RazorAnalyzerTests` | `src/AiNetLinter.FastTests/Web/RazorAnalyzerTests.cs` |
| `src/AiNetLinter.Tests/Web/WebSuppressionDetectorTests.cs` | `WebSuppressionDetectorTests` | Web | migrated | `FullyQualifiedName~WebSuppressionDetectorTests` | `src/AiNetLinter.FastTests/Web/WebSuppressionDetectorTests.cs` |
