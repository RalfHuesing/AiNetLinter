#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Configuration;

/// <summary>
/// Löst die effektive Konfiguration für ein Dokument oder ein Projekt auf Basis von Projekt-Overrides auf.
/// </summary>
public static class ProjectConfigResolver
{
    /// <summary>
    /// Löst die effektive Linter-Konfiguration für ein bestimmtes Roslyn-Dokument auf.
    /// </summary>
    /// <param name="document">Das zu analysierende Roslyn-Dokument.</param>
    /// <param name="globalConfig">Die globale Linter-Konfiguration.</param>
    /// <returns>Die für das Dokument effektive Linter-Konfiguration.</returns>
    public static LinterConfig ResolveForDocument(Document document, LinterConfig globalConfig)
    {
        return ResolveForProject(document.Project.Name, globalConfig);
    }

    /// <summary>
    /// Löst die effektive Linter-Konfiguration für einen Projektnamen auf.
    /// </summary>
    /// <param name="projectName">Der Name des Roslyn-Projekts.</param>
    /// <param name="globalConfig">Die globale Linter-Konfiguration.</param>
    /// <returns>Die für das Projekt effektive Linter-Konfiguration.</returns>
    public static LinterConfig ResolveForProject(string projectName, LinterConfig globalConfig)
    {
        if (globalConfig.ProjectOverrides == null || globalConfig.ProjectOverrides.Count == 0)
        {
            return globalConfig;
        }

        foreach (var pair in globalConfig.ProjectOverrides)
        {
            if (IsMatch(projectName, pair.Key))
            {
                return MergeConfig(globalConfig, pair.Value);
            }
        }

        return globalConfig;
    }

    private static bool IsMatch(string name, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
    }

    // ainetlinter-disable MaxMethodLineCount
    // ainetlinter-disable MaxCognitiveComplexity
    // ainetlinter-disable MaxCyclomaticComplexity
    private static LinterConfig MergeConfig(LinterConfig global, ProjectOverrideEntry overrides)
    {
        var mergedGlobal = global.Global;
        if (overrides.Global != null)
        {
            var og = overrides.Global;
            mergedGlobal = global.Global with
            {
                EnforceSealedClasses = og.EnforceSealedClasses ?? global.Global.EnforceSealedClasses,
                AllowUnsealedPartialClasses = og.AllowUnsealedPartialClasses ?? global.Global.AllowUnsealedPartialClasses,
                AllowDynamic = og.AllowDynamic ?? global.Global.AllowDynamic,
                AllowOutParameters = og.AllowOutParameters ?? global.Global.AllowOutParameters,
                EnforceValueObjectContracts = og.EnforceValueObjectContracts ?? global.Global.EnforceValueObjectContracts,
                EnableTestSentinel = og.EnableTestSentinel ?? global.Global.EnableTestSentinel,
                EnforcePascalCase = og.EnforcePascalCase ?? global.Global.EnforcePascalCase,
                EnforceXmlDocumentation = og.EnforceXmlDocumentation ?? global.Global.EnforceXmlDocumentation,
                EnforceSemanticNaming = og.EnforceSemanticNaming ?? global.Global.EnforceSemanticNaming,
                EnforceNullableEnable = og.EnforceNullableEnable ?? global.Global.EnforceNullableEnable,
                EnforceNoSilentCatch = og.EnforceNoSilentCatch ?? global.Global.EnforceNoSilentCatch,
                AllowTryPatternOutParameters = og.AllowTryPatternOutParameters ?? global.Global.AllowTryPatternOutParameters,
                AllowCancellationShutdownCatch = og.AllowCancellationShutdownCatch ?? global.Global.AllowCancellationShutdownCatch,
                EnforceMinimalApiAsParameters = og.EnforceMinimalApiAsParameters ?? global.Global.EnforceMinimalApiAsParameters,
                EnforceResultPatternOverExceptions = og.EnforceResultPatternOverExceptions ?? global.Global.EnforceResultPatternOverExceptions,
                EnforceNoVariableShadowing = og.EnforceNoVariableShadowing ?? global.Global.EnforceNoVariableShadowing,
                EnforceReadonlyParameters = og.EnforceReadonlyParameters ?? global.Global.EnforceReadonlyParameters,
                EnforceReadonlyFields = og.EnforceReadonlyFields ?? global.Global.EnforceReadonlyFields,
                EnforceNoMagicValues = og.EnforceNoMagicValues ?? global.Global.EnforceNoMagicValues,
                EnforceExplicitStateImmutability = og.EnforceExplicitStateImmutability ?? global.Global.EnforceExplicitStateImmutability,
                AllowedExceptions = og.AllowedExceptions ?? global.Global.AllowedExceptions,
                EnforceStrictBoundaryForBusinessLogic = og.EnforceStrictBoundaryForBusinessLogic ?? global.Global.EnforceStrictBoundaryForBusinessLogic,
                PreventContextDependentOverloads = og.PreventContextDependentOverloads ?? global.Global.PreventContextDependentOverloads,
                RequireExplicitTruncationHandling = og.RequireExplicitTruncationHandling ?? global.Global.RequireExplicitTruncationHandling,
                EnforceNamespaceDirectoryMapping = og.EnforceNamespaceDirectoryMapping ?? global.Global.EnforceNamespaceDirectoryMapping,
                DetectAndBanPhantomDependencies = og.DetectAndBanPhantomDependencies ?? global.Global.DetectAndBanPhantomDependencies,
                ImmutabilityExemptSuffixes = og.ImmutabilityExemptSuffixes ?? global.Global.ImmutabilityExemptSuffixes
            };
        }

        var mergedMetrics = global.Metrics;
        if (overrides.Metrics != null)
        {
            var om = overrides.Metrics;
            mergedMetrics = global.Metrics with
            {
                MaxLineCount = om.MaxLineCount ?? global.Metrics.MaxLineCount,
                MaxMethodParameterCount = om.MaxMethodParameterCount ?? global.Metrics.MaxMethodParameterCount,
                MaxMethodLineCount = om.MaxMethodLineCount ?? global.Metrics.MaxMethodLineCount,
                MaxCyclomaticComplexity = om.MaxCyclomaticComplexity ?? global.Metrics.MaxCyclomaticComplexity,
                MaxCognitiveComplexity = om.MaxCognitiveComplexity ?? global.Metrics.MaxCognitiveComplexity,
                MaxInheritanceDepth = om.MaxInheritanceDepth ?? global.Metrics.MaxInheritanceDepth,
                MinCognitiveComplexityForTest = om.MinCognitiveComplexityForTest ?? global.Metrics.MinCognitiveComplexityForTest,
                AggregatePartialClassLineCount = om.AggregatePartialClassLineCount ?? global.Metrics.AggregatePartialClassLineCount,
                MaxMethodOverloads = om.MaxMethodOverloads ?? global.Metrics.MaxMethodOverloads,
                MaxConstructorDependencies = om.MaxConstructorDependencies ?? global.Metrics.MaxConstructorDependencies,
                MaxAIContextFootprint = om.MaxAIContextFootprint ?? global.Metrics.MaxAIContextFootprint,
                MaxDirectoryDepth = om.MaxDirectoryDepth ?? global.Metrics.MaxDirectoryDepth
            };
        }

        return global with { Global = mergedGlobal, Metrics = mergedMetrics };
    }
}
