#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Diagnostics;
using AiNetLinter.Models;
using AiNetLinter.Metrics;
using AiNetLinter.Suppression;
using AiNetLinter.Web;
using AiNetLinter.Core.Checkers;

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
    public static void Run(AnalysisState state, LinterConfig config, IPerformanceProfiler? profiler = null)
    {
        var p = profiler ?? NullPerformanceProfiler.Instance;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RunTestSentinel(state, config);
        sw.Stop();
        p.RecordPostAnalysisStep("TestSentinel", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        RunInheritanceDepthCheck(state.SourceClasses, state.Violations, state.FileContents, config);
        sw.Stop();
        p.RecordPostAnalysisStep("InheritanceDepth", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        RunAIContextFootprintCheck(state.SourceClasses, state.Violations, state.FileContents, config);
        sw.Stop();
        p.RecordPostAnalysisStep("AIContextFootprint", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        AddPartialClassViolations(state.PartialClassParts, state.Violations, config);
        sw.Stop();
        p.RecordPostAnalysisStep("PartialClassLineAggregation", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        UiFileSeparationChecker.Run(state, config);
        sw.Stop();
        p.RecordPostAnalysisStep("UiFileSeparation", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        RunMaxPartialClassFilesCheck(state.PartialClassParts, state.Violations, config);
        sw.Stop();
        p.RecordPostAnalysisStep("MaxPartialClassFiles", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        RunMaxDirectoryChildrenCheck(state.Violations, config);
        sw.Stop();
        p.RecordPostAnalysisStep("MaxDirectoryChildren", sw.Elapsed.TotalMilliseconds);

        if (config.Web.IsEnabled)
        {
            sw.Restart();
            WebFileSeparationChecker.Run(state, config);
            sw.Stop();
            p.RecordPostAnalysisStep("WebFileSeparation", sw.Elapsed.TotalMilliseconds);
        }
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
        var effectiveConfig = ProjectConfigResolver.ResolveForFile(cls.FilePath, cls.ProjectName, config);

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
                Guidance = "Ersetze tiefe Vererbung durch Komposition: Deklariere Instanzen der Basisklassen als private Felder und delegiere Methoden-Aufrufe explizit (z. B. 'private readonly BaseService _base; void DoX() => _base.DoX()'). Extrahiere gemeinsames Verhalten alternativ in einen eigenen Service. Falls die Tiefe durch Framework-Basisklassen (ASP.NET, EF Core, xUnit) entsteht: Namespace-Praefix in 'rules.json → Metrics.InheritanceDepthFrameworkPrefixes' eintragen, um False Positives auszuschliessen."
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
        var effectiveConfig = ProjectConfigResolver.ResolveForFile(cls.FilePath, cls.ProjectName, config);

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
                Guidance = "Fuehre fuer die groessten Abhaengigkeiten (s. Details oben) ein schlankes Interface ein, das nur die tatsaechlich genutzten Methoden deklariert, und injiziere dieses statt der konkreten Klasse. Alternativ: Splitte diese Klasse nach Verantwortlichkeiten. Hintergrund: LLM-Agenten uebersehen Invarianten bei mehr als ~2.500 transitiven Zeilen ('Lost in the Middle'-Effekt — U-foermige Attention-Kurve bleibt auch bei Modellen mit grossem Kontextfenster bestehen)."
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

    private static void RunMaxPartialClassFilesCheck(
        ConcurrentBag<PartialClassPart> parts,
        ConcurrentBag<RuleViolation> violations,
        LinterConfig config)
    {
        var limit = config.Metrics.MaxPartialClassFiles;
        if (limit <= 0) return;

        var exemptTypes = config.Metrics.MaxPartialClassFilesExemptTypes;

        foreach (var group in parts.GroupBy(p => p.TypeName, StringComparer.Ordinal))
        {
            if (IsExemptType(group.Key, exemptTypes)) continue;

            var distinctFiles = group.Select(p => p.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (distinctFiles.Length <= limit) continue;

            var representative = group.OrderBy(p => p.FilePath, StringComparer.OrdinalIgnoreCase).First();
            violations.Add(new RuleViolation
            {
                FilePath = representative.FilePath,
                LineNumber = representative.LineNumber,
                RuleName = nameof(config.Metrics.MaxPartialClassFiles),
                Details = $"Der partial-Typ '{group.Key}' ist auf {distinctFiles.Length} Dateien verteilt (erlaubt: {limit}). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.",
                Guidance = "Extrahiere Unter-Logik in eigenständige Klassen (z. B. 'XyzChecker', 'XyzValidator') und rufe diese aus einem schlanken Typ auf — so bleiben beide Klassen je in 1–2 Dateien vollständig lesbar. Alternativ: fasse alle partial-Deklarationen in einer einzigen Datei zusammen."
            });
        }
    }

    private static bool IsExemptType(string typeName, IReadOnlyCollection<string> exemptTypes)
    {
        if (exemptTypes.Count == 0) return false;
        var simpleName = typeName.Contains('.') ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;
        return exemptTypes.Contains(typeName, StringComparer.Ordinal)
            || exemptTypes.Contains(simpleName, StringComparer.Ordinal);
    }

    internal static void RunMaxDirectoryChildrenCheck(
        ConcurrentBag<RuleViolation> violations,
        LinterConfig config)
    {
        var limit = config.Metrics.MaxDirectoryChildren;
        if (limit <= 0) return;

        var solutionBase = config.SolutionBasePath;
        if (string.IsNullOrEmpty(solutionBase) || !Directory.Exists(solutionBase)) return;

        RunMaxDirectoryChildrenRecursive(solutionBase, solutionBase, violations, config);
    }

    private static void RunMaxDirectoryChildrenRecursive(
        string directory,
        string solutionBase,
        ConcurrentBag<RuleViolation> violations,
        LinterConfig config)
    {
        var dirName = Path.GetFileName(directory);
        if (IsExemptDirectory(dirName, config.Metrics.MaxDirectoryChildrenExemptNames)) return;

        try
        {
            var entries = Directory.EnumerateFileSystemEntries(directory)
                .Where(e => !IsExemptDirectory(Path.GetFileName(e), config.Metrics.MaxDirectoryChildrenExemptNames))
                .ToArray();

            if (entries.Length > config.Metrics.MaxDirectoryChildren)
            {
                var relativePath = Path.GetRelativePath(solutionBase, directory);
                violations.Add(new RuleViolation
                {
                    FilePath = Path.Combine(directory, "."),
                    LineNumber = 0,
                    RuleName = nameof(config.Metrics.MaxDirectoryChildren),
                    Details = $"Ordner '{relativePath}' enthält {entries.Length} Einträge (erlaubt: {config.Metrics.MaxDirectoryChildren}). Viele Einträge erhöhen den Token-Verbrauch bei 'list_directory'-Aufrufen und reduzieren die Agent-Trefferrate.",
                    Guidance = "Gruppiere verwandte Dateien in Unterordner (z. B. nach Feature oder Typ), sodass kein Ordner mehr als das konfigurierte Limit an Einträgen hat."
                });
            }

            foreach (var subDir in Directory.EnumerateDirectories(directory))
            {
                RunMaxDirectoryChildrenRecursive(subDir, solutionBase, violations, config);
            }
        }
        catch (UnauthorizedAccessException ignored) { _ = ignored; }
        catch (IOException ignored) { _ = ignored; }
    }

    private static bool IsExemptDirectory(string dirName, IReadOnlyCollection<string> exemptNames)
    {
        if (string.IsNullOrEmpty(dirName)) return false;
        return exemptNames.Any(e => dirName.Equals(e, StringComparison.OrdinalIgnoreCase));
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
