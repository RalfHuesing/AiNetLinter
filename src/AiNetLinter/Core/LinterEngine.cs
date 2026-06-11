using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using AiNetLinter.Suppression;

[assembly: InternalsVisibleTo("AiNetLinter.Tests")]

namespace AiNetLinter.Core;

/// <summary>
/// Koordiniert das Laden der Solution via MSBuildWorkspace und die semantische Analyse aller Klassen.
/// </summary>
public sealed class LinterEngine
{
    private readonly LinterConfig _config;

    public LinterEngine(LinterConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Führt die Analyse auf dem angegebenen Pfad aus und liefert alle Regelverstöße.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(string path)
    {
        using var catalog = await SourceFileCatalog.LoadAsync(path);
        return await RunAsync(catalog);
    }

    /// <summary>
    /// Führt die Analyse auf einer geladenen Solution aus.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(SourceFileCatalog catalog)
    {
        return await RunAsync(catalog.Solution, catalog);
    }

    /// <summary>
    /// Führt die Analyse auf einer bestehenden Solution im Speicher aus.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(Solution solution)
    {
        return await RunAsync(solution, catalog: null);
    }

    /// <summary>
    /// Erzeugt MSBuild-Workspace-Eigenschaften für Design-Time-Laden.
    /// </summary>
    public static Dictionary<string, string> CreateWorkspaceProperties() => new()
    {
        ["DesignTimeBuild"] = "true",
        ["SkipCompilerExecution"] = "true",
        ["ProvideCommandLineArgs"] = "true",
        ["RunAnalyzers"] = "false",
        ["RunCodeAnalysis"] = "false",
    };

    private async Task<IReadOnlyCollection<RuleViolation>> RunAsync(Solution solution, SourceFileCatalog? catalog)
    {
        var state = CreateAnalysisState(solution);
        await AnalyzeSolutionAsync(state, catalog);

        var suppressionCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_config.Global.EnableTestSentinel)
        {
            RunTestSentinel(state.TestCoverage, state.SourceClasses, state.Violations, suppressionCache);
        }

        RunInheritanceDepthCheck(state.SourceClasses, state.Violations, suppressionCache);
        AddPartialClassViolations(state.PartialClassParts, state.Violations);

        return state.Violations.ToArray();
    }

    private static AnalysisState CreateAnalysisState(Solution solution)
    {
        return new AnalysisState(
            solution,
            new ConcurrentBag<RuleViolation>(),
            new TestCoverageIndex(),
            new ConcurrentBag<ClassInfo>(),
            new ConcurrentBag<PartialClassPart>());
    }

    private static ParallelOptions CreateParallelOptions() => new()
    {
        MaxDegreeOfParallelism = Environment.ProcessorCount
    };

    private sealed record AnalysisState(
        Solution Solution,
        ConcurrentBag<RuleViolation> Violations,
        TestCoverageIndex TestCoverage,
        ConcurrentBag<ClassInfo> SourceClasses,
        ConcurrentBag<PartialClassPart> PartialClassParts
    );

    private async Task AnalyzeSolutionAsync(AnalysisState state, SourceFileCatalog? catalog)
    {
        var solutionDir = Path.GetDirectoryName(state.Solution.FilePath);
        var workItems = await ResolveWorkItemsAsync(state.Solution, catalog, solutionDir);

        await Parallel.ForEachAsync(workItems, CreateParallelOptions(), (item, _) =>
            AnalyzeWorkItemAsync(item, state));
    }

    private static async Task<IReadOnlyList<CatalogDocumentWorkItem>> ResolveWorkItemsAsync(
        Solution solution,
        SourceFileCatalog? catalog,
        string? solutionDir)
    {
        if (catalog != null)
        {
            return await catalog.CollectDocumentWorkItemsAsync();
        }

        return await CollectDocumentWorkItemsFromSolutionAsync(solution, solutionDir);
    }

    private static async Task<IReadOnlyList<CatalogDocumentWorkItem>> CollectDocumentWorkItemsFromSolutionAsync(
        Solution solution,
        string? solutionDir)
    {
        var workItems = new List<CatalogDocumentWorkItem>();

        foreach (var project in solution.Projects)
        {
            var items = await CollectProjectDocumentsAsync(project, solutionDir);
            workItems.AddRange(items);
        }

        return workItems;
    }

    private static async Task<IReadOnlyList<CatalogDocumentWorkItem>> CollectProjectDocumentsAsync(
        Project project,
        string? solutionDir)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            return [];
        }

        var isTestProject = IsTestProject(project);
        return CollectValidDocuments(project, solutionDir, isTestProject);
    }

    private static List<CatalogDocumentWorkItem> CollectValidDocuments(
        Project project,
        string? solutionDir,
        bool isTestProject)
    {
        var workItems = new List<CatalogDocumentWorkItem>();

        foreach (var document in project.Documents)
        {
            if (!SourceFileCatalog.IsValidDocument(document, solutionDir))
            {
                continue;
            }

            workItems.Add(new CatalogDocumentWorkItem(document, isTestProject));
        }

        return workItems;
    }

    private async ValueTask AnalyzeWorkItemAsync(CatalogDocumentWorkItem item, AnalysisState state)
    {
        await AnalyzeDocumentAsync(item.Document, item.IsTestProject, state);
    }

    private static bool IsTestProject(Project project)
    {
        foreach (var reference in project.MetadataReferences)
        {
            if (IsTestReference(reference.Display))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTestReference(string? display)
    {
        if (string.IsNullOrEmpty(display)) return false;

        var keywords = new[] { "xunit", "nunit", "testplatform", "unittesting" };
        foreach (var keyword in keywords)
        {
            if (display.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task AnalyzeDocumentAsync(Document document, bool isTestProj, AnalysisState state)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return;

        var filePath = document.FilePath ?? document.Name;
        bool isTestFile = isTestProj || IsTestFile(filePath);

        AnalyzeAndCollect(filePath, semanticModel, isTestFile, state);
    }

    private void AnalyzeAndCollect(string filePath, SemanticModel semanticModel, bool isTestFile, AnalysisState state)
    {
        var analyzer = new LinterAnalyzer(filePath, semanticModel, _config, isTestFile);
        analyzer.RunAnalysis();
        CollectAnalyzerResults(analyzer, semanticModel, isTestFile, state);
    }

    private void CollectAnalyzerResults(
        LinterAnalyzer analyzer,
        SemanticModel semanticModel,
        bool isTestFile,
        AnalysisState state)
    {
        foreach (var violation in analyzer.Violations)
        {
            state.Violations.Add(violation);
        }

        if (isTestFile)
        {
            AddTestCoverage(analyzer, semanticModel, state.TestCoverage);
            return;
        }

        foreach (var cls in analyzer.Classes)
        {
            state.SourceClasses.Add(cls);
        }

        foreach (var part in analyzer.PartialClassParts)
        {
            state.PartialClassParts.Add(part);
        }
    }

    private void AddTestCoverage(LinterAnalyzer analyzer, SemanticModel semanticModel, TestCoverageIndex index)
    {
        foreach (var cls in analyzer.Classes)
        {
            if (cls.HasTestMethods)
            {
                index.AddTestClass(cls.Name);
            }
        }

        TestCoverageCollector.Collect(
            semanticModel.SyntaxTree,
            semanticModel,
            index,
            _config.TestSentinel);
    }

    private void AddPartialClassViolations(
        ConcurrentBag<PartialClassPart> parts,
        ConcurrentBag<RuleViolation> violations)
    {
        foreach (var violation in PartialClassLineAggregator.BuildViolations(parts.ToArray(), _config))
        {
            violations.Add(violation);
        }
    }

    private static bool IsTestFile(string file)
    {
        if (file.EndsWith("Tests.cs")) return true;
        if (file.EndsWith("Test.cs")) return true;
        return file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}");
    }

    private void RunTestSentinel(
        TestCoverageIndex testCoverage,
        ConcurrentBag<ClassInfo> sourceClasses,
        ConcurrentBag<RuleViolation> violations,
        Dictionary<string, string> suppressionCache)
    {
        foreach (var srcClass in sourceClasses)
        {
            if (srcClass.MaxCognitiveComplexity > _config.Metrics.MinCognitiveComplexityForTest)
            {
                CheckTestPresence(srcClass, testCoverage, violations, suppressionCache);
            }
        }
    }

    private void CheckTestPresence(
        ClassInfo srcClass,
        TestCoverageIndex testCoverage,
        ConcurrentBag<RuleViolation> violations,
        Dictionary<string, string> suppressionCache)
    {
        if (TestCoverageResolver.IsCovered(srcClass.Name, testCoverage, _config.TestSentinel))
        {
            return;
        }

        if (IsSuppressedViolation(srcClass.FilePath, "StaticTestSentinel", srcClass.LineNumber, suppressionCache))
        {
            return;
        }

        string expectedTest = $"{srcClass.Name}Tests";
        violations.Add(new RuleViolation
        {
            FilePath = srcClass.FilePath,
            LineNumber = srcClass.LineNumber,
            RuleName = "StaticTestSentinel",
            Details = $"Die Klasse '{srcClass.Name}' hat eine hohe Relevanz (max. Kognitive Komplexität: {srcClass.MaxCognitiveComplexity}), aber es wurde keine Testabdeckung gefunden.",
            Guidance = $"Schreibe Unit Tests für '{srcClass.Name}' (z. B. '{expectedTest}', typeof-Referenz oder // @covers {srcClass.Name}).",
        });
    }

    private void RunInheritanceDepthCheck(
        ConcurrentBag<ClassInfo> sourceClasses,
        ConcurrentBag<RuleViolation> violations,
        Dictionary<string, string> suppressionCache)
    {
        foreach (var cls in sourceClasses)
        {
            var depth = GetInheritanceDepth(cls.Symbol);
            if (depth > _config.Metrics.MaxInheritanceDepth &&
                !IsSuppressedViolation(
                    cls.FilePath,
                    nameof(_config.Metrics.MaxInheritanceDepth),
                    cls.LineNumber,
                    suppressionCache))
            {
                violations.Add(new RuleViolation
                {
                    FilePath = cls.FilePath,
                    LineNumber = cls.LineNumber,
                    RuleName = nameof(_config.Metrics.MaxInheritanceDepth),
                    Details = $"Die Klasse '{cls.Name}' hat eine Vererbungstiefe von {depth} (erlaubt sind maximal {_config.Metrics.MaxInheritanceDepth}).",
                    Guidance = "Halte Vererbungshierarchien flach (max. 2 Ebenen) oder nutze Komposition statt Vererbung."
                });
            }
        }
    }

    private static bool IsSuppressedViolation(
        string filePath,
        string ruleName,
        int lineNumber,
        Dictionary<string, string> suppressionCache)
    {
        if (!suppressionCache.TryGetValue(filePath, out var fileContent))
        {
            fileContent = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
            suppressionCache[filePath] = fileContent;
        }

        return SuppressionEvaluator.IsSuppressed(fileContent, ruleName, lineNumber);
    }

    private static int GetInheritanceDepth(INamedTypeSymbol symbol)
    {
        int depth = 0;
        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            depth++;
            if (depth > 20) return depth;
            current = current.BaseType;
        }

        return depth;
    }
}
