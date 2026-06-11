using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

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
        var slnPath = FindSolutionFile(path);
        RegisterMSBuild();

        using var workspace = MSBuildWorkspace.Create(CreateWorkspaceProperties());
        var solution = await workspace.OpenSolutionAsync(slnPath);
        return await RunAsync(solution);
    }

    /// <summary>
    /// Führt die Analyse auf einer bestehenden Solution im Speicher aus.
    /// </summary>
    public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(Solution solution)
    {
        var violations = new ConcurrentBag<RuleViolation>();
        var testClasses = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var sourceClasses = new ConcurrentBag<ClassInfo>();

        await AnalyzeSolutionAsync(solution, violations, testClasses, sourceClasses);

        if (_config.Global.EnableTestSentinel)
        {
            RunTestSentinel(testClasses, sourceClasses, violations);
        }

        RunInheritanceDepthCheck(sourceClasses, violations);

        return violations.ToArray();
    }

    internal static Dictionary<string, string> CreateWorkspaceProperties() => new()
    {
        ["DesignTimeBuild"] = "true",
        ["SkipCompilerExecution"] = "true",
        ["ProvideCommandLineArgs"] = "true",
        ["RunAnalyzers"] = "false",
        ["RunCodeAnalysis"] = "false",
    };

    private static ParallelOptions CreateParallelOptions() => new()
    {
        MaxDegreeOfParallelism = Environment.ProcessorCount
    };

    private static void RegisterMSBuild()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    private sealed record AnalysisContext(
        ConcurrentBag<RuleViolation> Violations,
        ConcurrentDictionary<string, byte> TestClasses,
        ConcurrentBag<ClassInfo> SourceClasses,
        string? SolutionDir
    );

    private sealed record DocumentWorkItem(Document Document, bool IsTestProject);

    private async Task AnalyzeSolutionAsync(
        Solution solution,
        ConcurrentBag<RuleViolation> violations,
        ConcurrentDictionary<string, byte> testClasses,
        ConcurrentBag<ClassInfo> sourceClasses)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath);
        var context = new AnalysisContext(violations, testClasses, sourceClasses, solutionDir);
        var workItems = await CollectDocumentWorkItemsAsync(solution, solutionDir);

        await Parallel.ForEachAsync(workItems, CreateParallelOptions(), (item, _) =>
            AnalyzeWorkItemAsync(item, context));
    }

    private static async Task<List<DocumentWorkItem>> CollectDocumentWorkItemsAsync(Solution solution, string? solutionDir)
    {
        var workItems = new List<DocumentWorkItem>();

        foreach (var project in solution.Projects)
        {
            var projectItems = await CollectProjectWorkItemsAsync(project, solutionDir);
            workItems.AddRange(projectItems);
        }

        return workItems;
    }

    private static async Task<IReadOnlyList<DocumentWorkItem>> CollectProjectWorkItemsAsync(Project project, string? solutionDir)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            return [];
        }

        bool isTestProject = IsTestProject(project);
        var workItems = new List<DocumentWorkItem>();

        foreach (var document in project.Documents)
        {
            if (!IsValidDocument(document, solutionDir))
            {
                continue;
            }

            workItems.Add(new DocumentWorkItem(document, isTestProject));
        }

        return workItems;
    }

    private async ValueTask AnalyzeWorkItemAsync(DocumentWorkItem item, AnalysisContext context)
    {
        await AnalyzeDocumentAsync(item.Document, item.IsTestProject, context);
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

    private async Task AnalyzeDocumentAsync(Document document, bool isTestProj, AnalysisContext context)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return;

        var filePath = document.FilePath ?? document.Name;
        bool isTestFile = isTestProj || IsTestFile(filePath);

        AnalyzeAndCollect(filePath, semanticModel, isTestFile, context);
    }

    private static bool IsValidDocument(Document document, string? solutionDir)
    {
        var path = document.FilePath ?? document.Name;
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        if (IsGeneratedPath(path)) return false;

        return IsInSolutionDir(document.FilePath, solutionDir);
    }

    private static bool IsInSolutionDir(string? filePath, string? solutionDir)
    {
        if (filePath == null || solutionDir == null) return true;
        return filePath.StartsWith(solutionDir, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedPath(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
               path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
               path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
    }

    private void AnalyzeAndCollect(string filePath, SemanticModel semanticModel, bool isTestFile, AnalysisContext context)
    {
        var analyzer = new LinterAnalyzer(filePath, semanticModel, _config, isTestFile);
        analyzer.RunAnalysis();

        foreach (var violation in analyzer.Violations)
        {
            context.Violations.Add(violation);
        }

        if (isTestFile)
        {
            AddTestClasses(analyzer.Classes, context.TestClasses);
        }
        else
        {
            foreach (var cls in analyzer.Classes)
            {
                context.SourceClasses.Add(cls);
            }
        }
    }

    private static void AddTestClasses(IReadOnlyCollection<ClassInfo> classes, ConcurrentDictionary<string, byte> testClasses)
    {
        foreach (var cls in classes)
        {
            if (cls.HasTestMethods)
            {
                testClasses.TryAdd(cls.Name, 0);
            }
        }
    }

    private static bool IsTestFile(string file)
    {
        if (file.EndsWith("Tests.cs")) return true;
        if (file.EndsWith("Test.cs")) return true;
        return file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}");
    }

    private void RunTestSentinel(
        ConcurrentDictionary<string, byte> testClasses,
        ConcurrentBag<ClassInfo> sourceClasses,
        ConcurrentBag<RuleViolation> violations)
    {
        foreach (var srcClass in sourceClasses)
        {
            if (srcClass.MaxCognitiveComplexity > _config.Metrics.MinCognitiveComplexityForTest)
            {
                CheckTestPresence(srcClass, testClasses, violations);
            }
        }
    }

    private static void CheckTestPresence(
        ClassInfo srcClass,
        ConcurrentDictionary<string, byte> testClasses,
        ConcurrentBag<RuleViolation> violations)
    {
        string expectedTest1 = $"{srcClass.Name}Tests";
        string expectedTest2 = $"{srcClass.Name}Test";

        if (!testClasses.ContainsKey(expectedTest1) && !testClasses.ContainsKey(expectedTest2))
        {
            violations.Add(new RuleViolation
            {
                FilePath = srcClass.FilePath,
                LineNumber = srcClass.LineNumber,
                RuleName = "StaticTestSentinel",
                Details = $"Die Klasse '{srcClass.Name}' hat eine hohe Relevanz (max. Kognitive Komplexität: {srcClass.MaxCognitiveComplexity}), aber es wurde keine Testklasse '{expectedTest1}' im Test-Projekt gefunden.",
                Guidance = $"Schreibe Unit Tests für '{srcClass.Name}' und lege die Testklasse '{expectedTest1}' im Test-Projekt an."
            });
        }
    }

    private void RunInheritanceDepthCheck(ConcurrentBag<ClassInfo> sourceClasses, ConcurrentBag<RuleViolation> violations)
    {
        foreach (var cls in sourceClasses)
        {
            var depth = GetInheritanceDepth(cls.Symbol);
            if (depth > _config.Metrics.MaxInheritanceDepth)
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

    private static string FindSolutionFile(string path)
    {
        if (File.Exists(path)) return GetValidFile(path);
        if (Directory.Exists(path)) return SearchInDirectory(path);
        throw new FileNotFoundException($"Keine .sln oder .slnx Datei gefunden unter: {path}");
    }

    private static string GetValidFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".sln" || ext == ".slnx") return path;
        throw new FileNotFoundException($"Keine gültige Solution-Datei: {path}");
    }

    private static string SearchInDirectory(string dir)
    {
        var files = Directory.GetFiles(dir, "*.slnx")
            .Concat(Directory.GetFiles(dir, "*.sln"))
            .ToArray();
        if (files.Length > 0) return files[0];
        throw new FileNotFoundException($"Keine .sln oder .slnx Datei im Verzeichnis gefunden: {dir}");
    }
}
