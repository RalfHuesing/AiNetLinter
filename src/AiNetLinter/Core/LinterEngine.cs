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
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(string path, bool noCache = false, int cacheTtlMinutes = 60)
    {
        using var catalog = await SourceFileCatalog.LoadAsync(path);
        return await RunAsync(catalog, noCache, cacheTtlMinutes);
    }

    /// <summary>
    /// Führt die Analyse auf einer geladenen Solution aus.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(SourceFileCatalog catalog, bool noCache = false, int cacheTtlMinutes = 60)
    {
        var cache = noCache ? null : BuildCache(catalog, catalog.Solution.FilePath ?? catalog.Solution.Workspace.GetType().Name, cacheTtlMinutes);
        return await RunInternalAsync(catalog.Solution, catalog, cache);
    }

    /// <summary>
    /// Führt die Analyse auf einer bestehenden Solution im Speicher aus.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(Solution solution, bool noCache = false, int cacheTtlMinutes = 60)
    {
        var cache = noCache ? null : BuildCache(null, solution.FilePath ?? solution.Workspace.GetType().Name, cacheTtlMinutes);
        return await RunInternalAsync(solution, catalog: null, cache);
    }

    private AnalysisCacheManager? BuildCache(SourceFileCatalog? catalog, string path, int cacheTtlMinutes)
    {
        if (string.IsNullOrEmpty(_rulesJsonContent))
        {
            return null;
        }
        var exeDir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        var solutionPath = catalog?.Solution?.FilePath ?? path;
        var ttl = cacheTtlMinutes > 0 ? TimeSpan.FromMinutes(cacheTtlMinutes) : TimeSpan.Zero;
        return AnalysisCacheManager.Load(exeDir, solutionPath, _rulesJsonContent, ttl);
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

        EmitPartialClassReadonlyFieldViolations(state);

        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("PostAnalysis");
        PostAnalysisChecks.Run(state, _config);
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("PostAnalysis");

        if (catalog != null && catalog.HasLoadingErrors)
        {
            Console.Error.WriteLine("[WARN]: Linter-Analyse-Cache wird nicht aktualisiert, da beim Laden des Workspaces Fehler/Warnungen aufgetreten sind.");
        }
        else
        {
            cache?.SaveIfDirty();
        }

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
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker>(SymbolEqualityComparer.Default));
    }

    private static ParallelOptions CreateParallelOptions() => new()
    {
        MaxDegreeOfParallelism = Environment.ProcessorCount
    };

    private static void EmitPartialClassReadonlyFieldViolations(AnalysisState state)
    {
        foreach (var (_, tracker) in state.SharedFieldTrackers)
        {
            foreach (var field in tracker.GetReadonlyCandidates())
            {
                var declaringFilePath = field.DeclaringSyntaxReferences.Length > 0
                    ? field.DeclaringSyntaxReferences[0].SyntaxTree.FilePath
                    : "";
                state.Violations.Add(FieldReadonlyTracker.BuildViolation(field, declaringFilePath));
            }
        }
    }



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



    private static string GetSolutionDir(Solution solution) =>
        string.IsNullOrEmpty(solution.FilePath) ? "" : Path.GetDirectoryName(solution.FilePath) ?? "";

    private static string GetRelativePath(string solutionDir, string filePath) =>
        string.IsNullOrEmpty(solutionDir)
            ? Path.GetFileName(filePath)
            : AiNetLinter.Output.PathNormalizer.ToRelative(solutionDir, filePath);

    private async Task AnalyzeDocumentAsync(Document document, bool isTestProj, AnalysisState state, AnalysisCacheManager? cache)
    {
        var filePath = document.FilePath ?? document.Name;
        if (FileFilterEvaluator.IsExcluded(filePath, _config.FileFilters)) return;

        var solutionDir = GetSolutionDir(state.Solution);
        var relativePath = GetRelativePath(solutionDir, filePath);
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
        analyzer.UseSharedFieldTrackers(state.SharedFieldTrackers);
        analyzer.RunAnalysis();
        CollectAnalyzerResults(analyzer, context, state);

        SaveToCache(new CacheDestination(cache, checksum, relativePath), analyzer, context);

        stopwatch.Stop();
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.RecordDocumentAnalysis(relativePath, stopwatch.Elapsed.TotalMilliseconds, analyzer.Violations.Count);
    }

    private sealed record CacheDestination(AnalysisCacheManager? Manager, string? Checksum, string RelativePath);

    private static void SaveToCache(CacheDestination dest, LinterAnalyzer analyzer, DocumentContext context)
    {
        if (dest.Manager == null || dest.Checksum == null) return;
        var testSignals = BuildTestSignals(analyzer, context.SemanticModel, context.EffectiveConfig, context.IsTestFile);
        var entry = CacheEntryMapper.BuildEntry(new BuildEntryParams(
            dest.RelativePath, dest.Checksum, analyzer, analyzer.PartialClassParts, testSignals));
        dest.Manager.Set(dest.RelativePath, entry);
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
