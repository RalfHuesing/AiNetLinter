using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using AiNetLinter.Core;
using AiNetLinter.Output;

namespace AiNetLinter.Baseline;

/// <summary>
/// Lädt eine Solution und enumeriert analysierbare Quelldateien.
/// </summary>
public sealed class SourceFileCatalog : IDisposable
{
    private readonly MSBuildWorkspace _workspace;

    private SourceFileCatalog(MSBuildWorkspace workspace, Solution solution)
    {
        _workspace = workspace;
        Solution = solution;
    }

    public Solution Solution { get; }

    /// <summary>
    /// Lädt die Solution aus dem angegebenen Pfad.
    /// </summary>
    public static async Task<SourceFileCatalog> LoadAsync(string path)
    {
        var slnPath = FindSolutionFile(path);
        RegisterMSBuild();

        var workspace = MSBuildWorkspace.Create(LinterEngine.CreateWorkspaceProperties());
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure)
            {
                failures.Add(e.Diagnostic.Message);
            }
        });

        var solution = await workspace.OpenSolutionAsync(slnPath);

        foreach (var msg in failures.Distinct(StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"[WARN]: Workspace-Diagnose: {msg}");
        }

        return new SourceFileCatalog(workspace, solution);
    }

    /// <summary>
    /// Erzeugt eine neue Catalog-Instanz mit einer aktualisierten In-Memory-Solution (z.B. nach AutoFix).
    /// </summary>
    internal SourceFileCatalog WithUpdatedSolution(Solution updatedSolution)
    {
        return new SourceFileCatalog(_workspace, updatedSolution);
    }

    /// <summary>
    /// Liefert alle gültigen Quelldateien mit relativen Pfaden.
    /// </summary>
    public IReadOnlyList<SourceFileEntry> GetSourceFiles(string outputRoot)
    {
        var solutionDir = Path.GetDirectoryName(Solution.FilePath);
        var entries = new List<SourceFileEntry>();

        foreach (var project in Solution.Projects)
        {
            AppendProjectSourceFiles(project, solutionDir, outputRoot, entries);
        }

        return entries;
    }

    /// <summary>
    /// Berechnet SHA-256-Checksummen für alle Quelldateien.
    /// </summary>
    public Dictionary<string, string> ComputeChecksums(string outputRoot)
    {
        var checksums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in GetSourceFiles(outputRoot))
        {
            checksums[entry.RelativePath] = FileChecksumCalculator.ComputeSha256Hex(entry.AbsolutePath);
        }

        return checksums;
    }

    /// <summary>
    /// Sammelt Dokumente für die parallele Linter-Analyse.
    /// </summary>
    public async Task<IReadOnlyList<CatalogDocumentWorkItem>> CollectDocumentWorkItemsAsync()
    {
        var solutionDir = Path.GetDirectoryName(Solution.FilePath);
        var tasks = Solution.Projects.Select(project => CollectProjectWorkItemsAsync(project, solutionDir));
        var results = await Task.WhenAll(tasks);

        var workItems = new List<CatalogDocumentWorkItem>();
        foreach (var projectItems in results)
        {
            workItems.AddRange(projectItems);
        }

        return workItems;
    }

    /// <summary>
    /// Gibt den MSBuild-Workspace frei.
    /// </summary>
    public void Dispose() => _workspace.Dispose();

    internal static bool IsValidDocument(Document document, string? solutionDir)
    {
        var path = document.FilePath ?? document.Name;
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        if (IsGeneratedPath(path)) return false;

        return IsInSolutionDir(document.FilePath, solutionDir);
    }

    private static void AppendProjectSourceFiles(
        Project project,
        string? solutionDir,
        string outputRoot,
        List<SourceFileEntry> entries)
    {
        foreach (var document in project.Documents)
        {
            if (!IsValidDocument(document, solutionDir))
            {
                continue;
            }

            entries.Add(ToSourceFileEntry(document, outputRoot));
        }
    }

    private static SourceFileEntry ToSourceFileEntry(Document document, string outputRoot)
    {
        var absolutePath = document.FilePath!;
        var relativePath = PathNormalizer.ToRelative(outputRoot, absolutePath);
        return new SourceFileEntry(absolutePath, relativePath);
    }

    private static async Task<IReadOnlyList<CatalogDocumentWorkItem>> CollectProjectWorkItemsAsync(
        Project project,
        string? solutionDir)
    {
        if (!project.SupportsCompilation) return [];

        var isTestProject = TestProjectDetector.IsTestProject(project);
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
            if (!IsValidDocument(document, solutionDir))
            {
                continue;
            }

            workItems.Add(new CatalogDocumentWorkItem(document, isTestProject));
        }

        return workItems;
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

    private static void RegisterMSBuild()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
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

/// <summary>
/// Dokument mit Testprojekt-Kennzeichnung für die Linter-Analyse.
/// </summary>
public sealed record CatalogDocumentWorkItem(Document Document, bool IsTestProject);
