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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RunTestSentinel(state, config);
        sw.Stop();
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.RecordPostAnalysisStep("TestSentinel", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        RunInheritanceDepthCheck(state.SourceClasses, state.Violations, state.FileContents, config);
        sw.Stop();
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.RecordPostAnalysisStep("InheritanceDepth", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        RunAIContextFootprintCheck(state.SourceClasses, state.Violations, state.FileContents, config);
        sw.Stop();
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.RecordPostAnalysisStep("AIContextFootprint", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        AddPartialClassViolations(state.PartialClassParts, state.Violations, config);
        sw.Stop();
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.RecordPostAnalysisStep("PartialClassLineAggregation", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        UiFileSeparationChecker.Run(state, config);
        sw.Stop();
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.RecordPostAnalysisStep("UiFileSeparation", sw.Elapsed.TotalMilliseconds);
    }

    private static void RunTestSentinel(AnalysisState state, LinterConfig config)
    {
        var context = new TestSentinelContext(state.TestCoverage, state.Violations, state.FileContents);
        foreach (var srcClass in state.SourceClasses)
        {
            CheckClassTestSentinel(srcClass, context, config);
        }
    }

    private static void CheckClassTestSentinel(ClassInfo srcClass, TestSentinelContext context, LinterConfig config)
    {
        var effectiveConfig = srcClass.ProjectName != null
            ? ProjectConfigResolver.ResolveForProject(srcClass.ProjectName, config)
            : config;

        if (!effectiveConfig.Global.EnableTestSentinel) return;
        if (srcClass.MaxCognitiveComplexity <= effectiveConfig.Metrics.MinCognitiveComplexityForTest) return;
        if (IsExemptFromSentinel(srcClass, effectiveConfig.TestSentinel)) return;

        CheckTestPresence(srcClass, context, effectiveConfig);
    }

    private static bool IsExemptFromSentinel(ClassInfo srcClass, TestSentinelConfig sentinelConfig)
    {
        var suffixes = sentinelConfig.ExemptClassNameSuffixes;
        if (suffixes.Count > 0 && suffixes.Any(s => srcClass.Name.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (sentinelConfig.ExemptStaticClasses && srcClass.IsStatic)
            return true;

        var exemptBases = sentinelConfig.ExemptWhenInheritsFrom;
        if (exemptBases.Count > 0 && srcClass.BaseTypeNames.Count > 0)
        {
            if (srcClass.BaseTypeNames.Any(b => exemptBases.Contains(b, StringComparer.OrdinalIgnoreCase)))
                return true;
        }

        return false;
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
                Guidance = "Ersetze tiefe Vererbung durch Komposition: Deklariere Instanzen der Basisklassen als private Felder und delegiere Methoden-Aufrufe explizit (z. B. 'private readonly BaseService _base; void DoX() => _base.DoX()'). Extrahiere gemeinsames Verhalten alternativ in einen eigenen Service, der von allen Beteiligten genutzt wird."
            });
        }
    }

    private static void RunAIContextFootprintCheck(
        ConcurrentBag<ClassInfo> sourceClasses,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents,
        LinterConfig config)
    {
        foreach (var cls in DeduplicatePartialClasses(sourceClasses))
        {
            CheckClassAIContextFootprint(cls, violations, fileContents, config);
        }
    }

    /// <summary>
    /// Gibt jede logische Klasse genau einmal zurück.
    /// Partial-Klassen erzeugen einen <see cref="ClassInfo"/> pro Datei, haben aber denselben
    /// transitiven Footprint (gleicher Roslyn-Symbol). Ohne Dedup würden sie mehrfach gemeldet.
    /// </summary>
    internal static IEnumerable<ClassInfo> DeduplicatePartialClasses(IEnumerable<ClassInfo> sourceClasses)
    {
        var nonPartial = sourceClasses.Where(static c => !c.IsPartial);
        var partialDistinct = sourceClasses
            .Where(static c => c.IsPartial)
            .GroupBy(static c => (c.Name, c.ProjectName))
            .Select(static g => g.OrderBy(static c => c.FilePath, StringComparer.OrdinalIgnoreCase).First());
        return nonPartial.Concat(partialDistinct);
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
            var detailsBuilder = new System.Text.StringBuilder();
            detailsBuilder.AppendLine($"{cls.Name} ({footprint} > {effectiveConfig.Metrics.MaxAIContextFootprint})");
            if (cls.AIContextFootprintDetails != null && cls.AIContextFootprintDetails.Count > 0)
            {
                foreach (var dep in cls.AIContextFootprintDetails)
                {
                    var shortName = dep.Name.Contains('.') ? dep.Name.Substring(dep.Name.LastIndexOf('.') + 1) : dep.Name;
                    detailsBuilder.AppendLine($"  + {shortName} ({dep.Lines})");
                }
                detailsBuilder.Append("  → Top-3 transitive Abhängigkeiten reduzieren oder Facade einführen");
            }

            violations.Add(new RuleViolation
            {
                FilePath = cls.FilePath,
                LineNumber = cls.LineNumber,
                RuleName = "AIContextFootprint",
                Details = detailsBuilder.ToString().TrimEnd('\r', '\n'),
                Guidance = "Fuehre fuer die groessten Abhaengigkeiten (s. Details oben) ein schlankes Interface ein, das nur die tatsaechlich genutzten Methoden deklariert, und injiziere dieses statt der konkreten Klasse. Alternativ: Splitte diese Klasse, sodass jede Haelfte nur die Abhaengigkeiten benoetigt, die sie wirklich nutzt."
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
        var fileContent = fileContents.GetOrAdd(filePath,
            fp => File.Exists(fp) ? File.ReadAllText(fp) : string.Empty);

        return SuppressionEvaluator.IsSuppressed(fileContent, ruleName, lineNumber);
    }
}
