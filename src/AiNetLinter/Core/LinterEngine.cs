using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using AiNetLinter.Suppression;
using AiNetLinter.Metrics;

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
        return await RunInternalAsync(catalog.Solution, catalog);
    }

    /// <summary>
    /// Führt die Analyse auf einer bestehenden Solution im Speicher aus.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(Solution solution)
    {
        return await RunInternalAsync(solution, catalog: null);
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

    private async Task<IReadOnlyCollection<RuleViolation>> RunInternalAsync(Solution solution, SourceFileCatalog? catalog)
    {
        var state = CreateAnalysisState(solution);
        await AnalyzeSolutionAsync(state, catalog);

        PostAnalysisChecks.Run(state, _config);

        return state.Violations.ToArray();
    }

    private static AnalysisState CreateAnalysisState(Solution solution)
    {
        return new AnalysisState(
            solution,
            new ConcurrentBag<RuleViolation>(),
            new TestCoverageIndex(),
            new ConcurrentBag<ClassInfo>(),
            new ConcurrentBag<PartialClassPart>(),
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static ParallelOptions CreateParallelOptions() => new()
    {
        MaxDegreeOfParallelism = Environment.ProcessorCount
    };



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
        var tasks = solution.Projects.Select(project => CollectProjectDocumentsAsync(project, solutionDir));
        var results = await Task.WhenAll(tasks);

        var workItems = new List<CatalogDocumentWorkItem>();
        foreach (var items in results)
        {
            workItems.AddRange(items);
        }

        return workItems;
    }

    private static Task<IReadOnlyList<CatalogDocumentWorkItem>> CollectProjectDocumentsAsync(
        Project project,
        string? solutionDir)
    {
        if (!project.SupportsCompilation)
        {
            return Task.FromResult<IReadOnlyList<CatalogDocumentWorkItem>>([]);
        }

        var isTestProject = TestProjectDetector.IsTestProject(project);
        return Task.FromResult<IReadOnlyList<CatalogDocumentWorkItem>>(
            CollectValidDocuments(project, solutionDir, isTestProject));
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



    private async Task AnalyzeDocumentAsync(Document document, bool isTestProj, AnalysisState state)
    {
        var filePath = document.FilePath ?? document.Name;
        if (FileFilterEvaluator.IsExcluded(filePath, _config.FileFilters))
        {
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return;

        var sourceText = await document.GetTextAsync();
        state.FileContents[filePath] = sourceText.ToString();

        bool isTestFile = isTestProj || IsTestFile(filePath);

        var effectiveConfig = ProjectConfigResolver.ResolveForDocument(document, _config);
        var context = new DocumentContext(filePath, semanticModel, isTestFile, effectiveConfig, document.Project.Name);

        AnalyzeAndCollect(context, state);
    }

    private void AnalyzeAndCollect(DocumentContext context, AnalysisState state)
    {
        var analyzer = new LinterAnalyzer(context.FilePath, context.SemanticModel, context.EffectiveConfig, context.IsTestFile, context.ProjectName);
        analyzer.RunAnalysis();
        CollectAnalyzerResults(analyzer, context, state);
    }

    private void CollectAnalyzerResults(
        LinterAnalyzer analyzer,
        DocumentContext context,
        AnalysisState state)
    {
        foreach (var violation in analyzer.Violations)
        {
            state.Violations.Add(violation);
        }

        if (context.IsTestFile)
        {
            AddTestCoverage(analyzer, context.SemanticModel, context.EffectiveConfig, state.TestCoverage);
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

    private void AddTestCoverage(LinterAnalyzer analyzer, SemanticModel semanticModel, LinterConfig effectiveConfig, TestCoverageIndex index)
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
            effectiveConfig.TestSentinel);
    }

    private static bool IsTestFile(string file)
    {
        if (file.EndsWith("Tests.cs")) return true;
        if (file.EndsWith("Test.cs")) return true;
        return file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}");
    }
}
