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
using AiNetLinter.Cache;

[assembly: InternalsVisibleTo("AiNetLinter.Tests")]

namespace AiNetLinter.Core;

/// <summary>
/// Koordiniert das Laden der Solution via MSBuildWorkspace und die semantische Analyse aller Klassen.
/// </summary>
public sealed class LinterEngine
{
    private readonly LinterConfig _config;
    private readonly string? _rulesJsonContent;

    public LinterEngine(LinterConfig config, string? rulesJsonContent = null)
    {
        _config = config;
        _rulesJsonContent = rulesJsonContent;
    }

    /// <summary>
    /// Führt die Analyse auf dem angegebenen Pfad aus und liefert alle Regelverstöße.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(string path, bool noCache = false)
    {
        using var catalog = await SourceFileCatalog.LoadAsync(path);
        return await RunAsync(catalog, noCache);
    }

    /// <summary>
    /// Führt die Analyse auf einer geladenen Solution aus.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(SourceFileCatalog catalog, bool noCache = false)
    {
        var cache = noCache ? null : BuildCache(catalog, catalog.Solution.FilePath ?? catalog.Solution.Workspace.GetType().Name);
        return await RunInternalAsync(catalog.Solution, catalog, cache);
    }

    /// <summary>
    /// Führt die Analyse auf einer bestehenden Solution im Speicher aus.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(Solution solution, bool noCache = false)
    {
        var cache = noCache ? null : BuildCache(null, solution.FilePath ?? solution.Workspace.GetType().Name);
        return await RunInternalAsync(solution, catalog: null, cache);
    }

    private AnalysisCacheManager? BuildCache(SourceFileCatalog? catalog, string path)
    {
        if (string.IsNullOrEmpty(_rulesJsonContent))
        {
            return null;
        }
        var exeDir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        var solutionPath = catalog?.Solution?.FilePath ?? path;
        return AnalysisCacheManager.Load(exeDir, solutionPath, _rulesJsonContent);
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

    private async Task<IReadOnlyCollection<RuleViolation>> RunInternalAsync(
        Solution solution, SourceFileCatalog? catalog, AnalysisCacheManager? cache)
    {
        var state = CreateAnalysisState(solution);
        
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("DocumentAnalysis");
        await AnalyzeSolutionAsync(state, catalog, cache);
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("DocumentAnalysis");

        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("PostAnalysis");
        PostAnalysisChecks.Run(state, _config);
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("PostAnalysis");

        cache?.SaveIfDirty();

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



    private async Task AnalyzeSolutionAsync(AnalysisState state, SourceFileCatalog? catalog, AnalysisCacheManager? cache)
    {
        var solutionDir = Path.GetDirectoryName(state.Solution.FilePath);
        var workItems = await ResolveWorkItemsAsync(state.Solution, catalog, solutionDir);

        await Parallel.ForEachAsync(workItems, CreateParallelOptions(), (item, _) =>
            AnalyzeWorkItemAsync(item, state, cache));
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

    private async ValueTask AnalyzeWorkItemAsync(CatalogDocumentWorkItem item, AnalysisState state, AnalysisCacheManager? cache)
    {
        await AnalyzeDocumentAsync(item.Document, item.IsTestProject, state, cache);
    }



    private async Task AnalyzeDocumentAsync(Document document, bool isTestProj, AnalysisState state, AnalysisCacheManager? cache)
    {
        var filePath = document.FilePath ?? document.Name;
        if (FileFilterEvaluator.IsExcluded(filePath, _config.FileFilters))
        {
            return;
        }

        var solutionDir = !string.IsNullOrEmpty(state.Solution.FilePath) ? Path.GetDirectoryName(state.Solution.FilePath) ?? "" : "";
        var relativePath = !string.IsNullOrEmpty(solutionDir)
            ? AiNetLinter.Output.PathNormalizer.ToRelative(solutionDir, filePath)
            : Path.GetFileName(filePath);

        bool isTestFile = isTestProj || IsTestFile(filePath);

        string? checksum = null;
        if (cache != null && File.Exists(filePath))
        {
            checksum = FileChecksumCalculator.ComputeSha256Hex(filePath);
            if (cache.TryGet(relativePath, checksum, out var cached) && cached != null)
            {
                CacheEntryMapper.RestoreToState(cached, state, isTestFile);
                return;
            }
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return;

        var sourceText = await document.GetTextAsync();
        state.FileContents[filePath] = sourceText.ToString();

        var effectiveConfig = ProjectConfigResolver.ResolveForDocument(document, _config);
        var context = new DocumentContext(filePath, semanticModel, isTestFile, effectiveConfig, document.Project.Name);

        var analyzer = new LinterAnalyzer(context.FilePath, context.SemanticModel, context.EffectiveConfig, context.IsTestFile, context.ProjectName);
        analyzer.RunAnalysis();
        CollectAnalyzerResults(analyzer, context, state);

        if (cache != null && checksum != null)
        {
            var testSignals = BuildTestSignals(analyzer, semanticModel, effectiveConfig, isTestFile);
            var partialParts = analyzer.PartialClassParts;
            var entry = CacheEntryMapper.BuildEntry(relativePath, checksum, analyzer, partialParts, testSignals);
            cache.Set(relativePath, entry);
        }

        stopwatch.Stop();
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.RecordDocumentAnalysis(relativePath, stopwatch.Elapsed.TotalMilliseconds, analyzer.Violations.Count);
    }

    private static TestSignalsDto BuildTestSignals(
        LinterAnalyzer analyzer,
        SemanticModel semanticModel,
        LinterConfig effectiveConfig,
        bool isTestFile)
    {
        if (!isTestFile)
        {
            return new TestSignalsDto();
        }

        var localIndex = new TestCoverageIndex();
        foreach (var cls in analyzer.Classes)
        {
            if (cls.HasTestMethods)
            {
                localIndex.AddTestClass(cls.Name);
            }
        }

        TestCoverageCollector.Collect(
            semanticModel.SyntaxTree,
            semanticModel,
            localIndex,
            effectiveConfig.TestSentinel);

        return new TestSignalsDto
        {
            TestClassNames = localIndex.TestClassNames.ToArray(),
            ReferencedTypeNames = localIndex.ReferencedTypeNames.ToArray(),
            CoversComments = localIndex.CoversComments.ToArray()
        };
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
