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

        RunTestSentinel(state.TestCoverage, state.SourceClasses, state.Violations, state.FileContents);
        RunInheritanceDepthCheck(state.SourceClasses, state.Violations, state.FileContents);
        RunAIContextFootprintCheck(state.SourceClasses, state.Violations, state.FileContents);
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
            new ConcurrentBag<PartialClassPart>(),
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
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
        ConcurrentBag<PartialClassPart> PartialClassParts,
        ConcurrentDictionary<string, string> FileContents
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
        var tasks = solution.Projects.Select(project => CollectProjectDocumentsAsync(project, solutionDir));
        var results = await Task.WhenAll(tasks);

        var workItems = new List<CatalogDocumentWorkItem>();
        foreach (var items in results)
        {
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
        ConcurrentDictionary<string, string> fileContents)
    {
        var context = new TestSentinelContext(testCoverage, violations, fileContents);
        foreach (var srcClass in sourceClasses)
        {
            CheckClassTestSentinel(srcClass, context);
        }
    }

    private void CheckClassTestSentinel(ClassInfo srcClass, TestSentinelContext context)
    {
        var effectiveConfig = srcClass.ProjectName != null
            ? ProjectConfigResolver.ResolveForProject(srcClass.ProjectName, _config)
            : _config;

        if (effectiveConfig.Global.EnableTestSentinel &&
            srcClass.MaxCognitiveComplexity > effectiveConfig.Metrics.MinCognitiveComplexityForTest)
        {
            CheckTestPresence(srcClass, context, effectiveConfig);
        }
    }

    private void CheckTestPresence(
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

    private void RunInheritanceDepthCheck(
        ConcurrentBag<ClassInfo> sourceClasses,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents)
    {
        foreach (var cls in sourceClasses)
        {
            CheckClassInheritanceDepth(cls, violations, fileContents);
        }
    }

    private void CheckClassInheritanceDepth(
        ClassInfo cls,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents)
    {
        var effectiveConfig = cls.ProjectName != null
            ? ProjectConfigResolver.ResolveForProject(cls.ProjectName, _config)
            : _config;

        var depth = GetInheritanceDepth(cls.Symbol);
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

    private void RunAIContextFootprintCheck(
        ConcurrentBag<ClassInfo> sourceClasses,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents)
    {
        foreach (var cls in sourceClasses)
        {
            CheckClassAIContextFootprint(cls, violations, fileContents);
        }
    }

    private void CheckClassAIContextFootprint(
        ClassInfo cls,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, string> fileContents)
    {
        var effectiveConfig = cls.ProjectName != null
            ? ProjectConfigResolver.ResolveForProject(cls.ProjectName, _config)
            : _config;

        if (effectiveConfig.Metrics.MaxAIContextFootprint <= 0)
        {
            return;
        }

        var footprint = AIContextFootprintCalculator.Calculate(cls.Symbol);
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

    private sealed record DocumentContext(
        string FilePath,
        SemanticModel SemanticModel,
        bool IsTestFile,
        LinterConfig EffectiveConfig,
        string ProjectName
    );

    private sealed record TestSentinelContext(
        TestCoverageIndex TestCoverage,
        ConcurrentBag<RuleViolation> Violations,
        ConcurrentDictionary<string, string> FileContents
    );
}
