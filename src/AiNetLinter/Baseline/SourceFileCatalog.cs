using Microsoft.CodeAnalysis;
using System.Threading;
using AiNetLinter.Core;
using AiNetLinter.Output;
using AiNetLinter.Configuration;
using AiNetLinter.Web;
using AiNetLinter.Cli;

namespace AiNetLinter.Baseline;

/// <summary>
/// Lädt eine Solution und enumeriert analysierbare Quelldateien.
/// </summary>
public sealed class SourceFileCatalog : IDisposable
{
    private readonly Workspace? _workspace;
    private int _disposed;

    internal SourceFileCatalog(Workspace? workspace, Solution solution, bool hasLoadingErrors)
    {
        _workspace = workspace;
        Solution = solution;
        HasLoadingErrors = hasLoadingErrors;
    }

    internal SourceFileCatalog(Solution solution, bool hasLoadingErrors)
    {
        _workspace = null;
        Solution = solution;
        HasLoadingErrors = hasLoadingErrors;
    }

    public Solution Solution { get; }
    public bool HasLoadingErrors { get; }

    /// <summary>
    /// Lädt die Solution aus dem angegebenen Pfad.
    /// </summary>
    public static async Task<SourceFileCatalog> LoadAsync(string path, System.Threading.CancellationToken ct = default)
    {
        return await SourceFileCatalogLoader.LoadAsync(path, ct);
    }

    /// <summary>
    /// Erzeugt eine neue Catalog-Instanz mit einer aktualisierten In-Memory-Solution (z.B. nach AutoFix).
    /// </summary>
    internal SourceFileCatalog WithUpdatedSolution(Solution updatedSolution)
    {
        return new SourceFileCatalog(_workspace, updatedSolution, HasLoadingErrors);
    }

    /// <summary>
    /// Liefert alle gültigen Quelldateien mit relativen Pfaden.
    /// </summary>
    public IReadOnlyList<SourceFileEntry> GetSourceFiles(string outputRoot, Config? config = null, LinterArgs? args = null)
    {
        var solutionDir = Path.GetDirectoryName(Solution.FilePath);
        var entries = new List<SourceFileEntry>();

        foreach (var project in Solution.Projects)
        {
            if (args != null && !ShouldIncludeProject(project, args, config))
            {
                continue;
            }
            AppendProjectSourceFiles(project, solutionDir, outputRoot, entries);
        }

        if (config != null && config.Web.IsEnabled && !string.IsNullOrEmpty(solutionDir))
        {
            var request = new WebFileDiscoveryRequest(
                FileFilters: config.FileFilters,
                CssExemptPaths: config.Web.Css.ExemptPaths,
                JsExemptPaths: config.Web.Js.ExemptPaths);

            var webEntries = WebFileCatalog.Collect(Solution, solutionDir, request);
            foreach (var webEntry in webEntries)
            {
                entries.Add(new SourceFileEntry(webEntry.AbsolutePath, webEntry.RelativePath));
            }
        }

        return entries;
    }

    /// <summary>
    /// Berechnet SHA-256-Checksummen für alle Quelldateien.
    /// </summary>
    public Dictionary<string, string> ComputeChecksums(string outputRoot, Config? config = null, LinterArgs? args = null)
    {
        var checksums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in GetSourceFiles(outputRoot, config, args))
        {
            checksums[entry.RelativePath] = FileChecksumCalculator.ComputeSha256Hex(entry.AbsolutePath);
        }

        return checksums;
    }

    /// <summary>
    /// Sammelt Dokumente für die parallele Linter-Analyse.
    /// </summary>
    public async Task<IReadOnlyList<CatalogDocumentWorkItem>> CollectDocumentWorkItemsAsync(LinterArgs? args = null, Config? config = null)
    {
        var solutionDir = Path.GetDirectoryName(Solution.FilePath);
        var tasks = Solution.Projects
            .Where(project => args == null || ShouldIncludeProject(project, args, config))
            .Select(project => CollectProjectWorkItemsAsync(project, solutionDir));
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
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _workspace?.Dispose();
    }

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

        var isTestProject = TestDetector.IsTestProject(project);
        return CollectValidDocuments(project, solutionDir, isTestProject);
    }

    internal static List<CatalogDocumentWorkItem> CollectValidDocuments(
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

    /// <summary>
    /// Filter fuer MSBuild-generierte Artefakte und verschachtelte Git-Worktrees. Wird sowohl
    /// intern von <see cref="IsValidDocument"/> als auch vom MCP-Server beim Verzeichnis-Sweep
    /// fuer neu angelegte Dateien (<c>McpCodeGraphServerRefresh.SweepForNewFiles</c>) verwendet —
    /// der zentrale Filter erspart eine Duplikation der Regel an anderer Stelle. Der
    /// Worktree-Ausschluss verhindert, dass parallel angelegte Git-Worktrees (z. B.
    /// <c>.claude/worktrees/&lt;agent&gt;/</c> fuer isolierte Subagenten-Laeufe, oder
    /// <c>.worktrees/&lt;name&gt;/</c> aus dem Drift-Loop) beim Sweep als "neue" Dateien
    /// erkannt und Projekten angehaengt werden — sie enthalten volle Kopien des Repos inkl.
    /// absichtlich regelverletzender Test-Fixtures (z. B. <c>AllowDynamic</c> in
    /// <c>DiRegistrationMini</c>), was sonst zu vervielfachten Lint-Ergebnissen fuehrt.
    /// </summary>
    internal static bool IsGeneratedPath(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
               path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
               path.Contains($"{Path.DirectorySeparatorChar}worktrees{Path.DirectorySeparatorChar}") ||
               path.Contains($"{Path.DirectorySeparatorChar}.worktrees{Path.DirectorySeparatorChar}") ||
               path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ShouldIncludeProject(Project project, LinterArgs args, Config? config)
    {
        var testSuffixes = config?.TestSentinel?.TestProjectNameSuffixes;
        var isTest = TestDetector.IsTestProject(project, testSuffixes);

        if (args.ExcludeTests && isTest) return false;
        if (args.TestsOnly && !isTest) return false;

        if (args.IncludeProjects.Count > 0 && !args.IncludeProjects.Any(p => NamespaceFilter.MatchesGlob(project.Name, p)))
            return false;

        if (args.ExcludeProjects.Count > 0 && args.ExcludeProjects.Any(p => NamespaceFilter.MatchesGlob(project.Name, p)))
            return false;

        return true;
    }
}

/// <summary>
/// Dokument mit Testprojekt-Kennzeichnung für die Linter-Analyse.
/// </summary>
public sealed record CatalogDocumentWorkItem(Document Document, bool IsTestProject);
