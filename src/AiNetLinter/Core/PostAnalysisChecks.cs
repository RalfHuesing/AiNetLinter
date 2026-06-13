#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using AiNetLinter.Metrics;
using AiNetLinter.Suppression;

namespace AiNetLinter.Core;

/// <summary>
/// Führt nachgelagerte Prüfungen nach der primären syntaktischen Analyse aus (z. B. Test Sentinel, Vererbungstiefe und AI-Context-Footprint).
/// </summary>
internal static class PostAnalysisChecks
{
    /// <summary>
    /// Startet die post-analytischen Prüfungen für die gesamte Solution.
    /// </summary>
    /// <param name="state">Der aktuelle Zustand der Analyse.</param>
    /// <param name="config">Die globale Konfiguration.</param>
    public static void Run(AnalysisState state, LinterConfig config)
    {
        RunTestSentinel(state.TestCoverage, state.SourceClasses, state.Violations, state.FileContents, config);
        RunInheritanceDepthCheck(state.SourceClasses, state.Violations, state.FileContents, config);
        RunAIContextFootprintCheck(state.SourceClasses, state.Violations, state.FileContents, config);
        AddPartialClassViolations(state.PartialClassParts, state.Violations, config);
    }

    private static void RunTestSentinel(
        TestCoverageIndex testCoverage,
        ConcurrentBag<ClassInfo> sourceClasses,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents,
        LinterConfig config)
    {
        var context = new TestSentinelContext(testCoverage, violations, fileContents);
        foreach (var srcClass in sourceClasses)
        {
            CheckClassTestSentinel(srcClass, context, config);
        }
    }

    private static void CheckClassTestSentinel(ClassInfo srcClass, TestSentinelContext context, LinterConfig config)
    {
        var effectiveConfig = srcClass.ProjectName != null
            ? ProjectConfigResolver.ResolveForProject(srcClass.ProjectName, config)
            : config;

        if (effectiveConfig.Global.EnableTestSentinel &&
            srcClass.MaxCognitiveComplexity > effectiveConfig.Metrics.MinCognitiveComplexityForTest)
        {
            CheckTestPresence(srcClass, context, effectiveConfig);
        }
    }

    private static void CheckTestPresence(
        ClassInfo srcClass,
        TestSentinelContext context,
        LinterConfig effectiveConfig)
    {
        if (TestCoverageResolver.IsCovered(srcClass.Name, context.TestCoverage, effectiveConfig.TestSentinel))
        {
            return;
        }

        if (IsSuppressedViolation(srcClass.FilePath, "StaticTestSentinel", srcClass.LineNumber, context.FileContents))
        {
            return;
        }

        string expectedTest = $"{srcClass.Name}Tests";
        context.Violations.Add(new RuleViolation
        {
            FilePath = srcClass.FilePath,
            LineNumber = srcClass.LineNumber,
            RuleName = "StaticTestSentinel",
            Details = $"Die Klasse '{srcClass.Name}' hat eine hohe Relevanz (max. Kognitive Komplexitaet: {srcClass.MaxCognitiveComplexity}), aber es wurde keine Testabdeckung gefunden.",
            Guidance = $"Schreibe Unit Tests fuer '{srcClass.Name}' (z. B. '{expectedTest}', typeof-Referenz oder // @covers {srcClass.Name}).",
        });
    }

    private static void RunInheritanceDepthCheck(
        ConcurrentBag<ClassInfo> sourceClasses,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents,
        LinterConfig config)
    {
        foreach (var cls in sourceClasses)
        {
            CheckClassInheritanceDepth(cls, violations, fileContents, config);
        }
    }

    private static void CheckClassInheritanceDepth(
        ClassInfo cls,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents,
        LinterConfig config)
    {
        var effectiveConfig = cls.ProjectName != null
            ? ProjectConfigResolver.ResolveForProject(cls.ProjectName, config)
            : config;

        var depth = cls.InheritanceDepth;
        if (depth > effectiveConfig.Metrics.MaxInheritanceDepth &&
            !IsSuppressedViolation(cls.FilePath, nameof(effectiveConfig.Metrics.MaxInheritanceDepth), cls.LineNumber, fileContents))
        {
            violations.Add(new RuleViolation
            {
                FilePath = cls.FilePath,
                LineNumber = cls.LineNumber,
                RuleName = nameof(effectiveConfig.Metrics.MaxInheritanceDepth),
                Details = $"Die Klasse '{cls.Name}' hat eine Vererbungstiefe von {depth} (erlaubt sind maximal {effectiveConfig.Metrics.MaxInheritanceDepth}).",
                Guidance = "Halte Vererbungshierarchien flach (max. 2 Ebenen) oder nutze Komposition statt Vererbung."
            });
        }
    }

    private static void RunAIContextFootprintCheck(
        ConcurrentBag<ClassInfo> sourceClasses,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents,
        LinterConfig config)
    {
        foreach (var cls in sourceClasses)
        {
            CheckClassAIContextFootprint(cls, violations, fileContents, config);
        }
    }

    private static void CheckClassAIContextFootprint(
        ClassInfo cls,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents,
        LinterConfig config)
    {
        var effectiveConfig = cls.ProjectName != null
            ? ProjectConfigResolver.ResolveForProject(cls.ProjectName, config)
            : config;

        if (effectiveConfig.Metrics.MaxAIContextFootprint <= 0)
        {
            return;
        }

        var footprint = cls.AIContextFootprint;
        if (footprint > effectiveConfig.Metrics.MaxAIContextFootprint &&
            !IsSuppressedViolation(cls.FilePath, "AIContextFootprint", cls.LineNumber, fileContents))
        {
            violations.Add(new RuleViolation
            {
                FilePath = cls.FilePath,
                LineNumber = cls.LineNumber,
                RuleName = "AIContextFootprint",
                Details = $"Die Klasse '{cls.Name}' hat einen AI-Context-Footprint von {footprint} transitiven Zeilen (erlaubt sind maximal {effectiveConfig.Metrics.MaxAIContextFootprint}).",
                Guidance = "Reduziere die Kopplung der Klasse zu anderen Klassen oder lagere Abhaengigkeiten aus, um Attention Dilution fuer KIs zu minimieren."
            });
        }
    }

    private static void AddPartialClassViolations(
        ConcurrentBag<PartialClassPart> parts,
        ConcurrentBag<RuleViolation> violations,
        LinterConfig config)
    {
        foreach (var violation in PartialClassLineAggregator.BuildViolations(parts.ToArray(), config))
        {
            violations.Add(violation);
        }
    }

    private static bool IsSuppressedViolation(
        string filePath,
        string ruleName,
        int lineNumber,
        ConcurrentDictionary<string, string> fileContents)
    {
        if (!fileContents.TryGetValue(filePath, out var fileContent))
        {
            fileContent = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
            fileContents[filePath] = fileContent;
        }

        return SuppressionEvaluator.IsSuppressed(fileContent, ruleName, lineNumber);
    }
}
