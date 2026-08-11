using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Suppression;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

/// <summary>
/// End-to-End-Regressionstest fuer den Worktree-Ausschluss in
/// <see cref="SuppressionFileResolver.ResolveAbsolutePathsAsync"/> selbst — nicht nur fuer den
/// darunterliegenden <c>SourceFileCatalog.IsGeneratedPath</c>-Helper isoliert (siehe
/// <c>SourceFileCatalogTests</c>). Deckt den solutionlosen Verzeichnis-Scan-Pfad ab
/// (<c>EnumerateCsFilesInDirectory</c>), der ohne diese Absicherung volle Repo-Kopien aus
/// <c>worktrees/</c>/<c>.worktrees/</c>-Unterordnern mit einlesen wuerde.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SuppressionFileResolverTests
{
    [Fact]
    public async Task ResolveAbsolutePathsAsync_DirectoryWithoutSolution_ExcludesWorktreeSubdirFiles()
    {
        using var temp = TestTempDirectory.Create("ainetlinter-suppression-resolver-");
        var keepPath = temp.CreateFile(Path.Combine("src", "Keep.cs"), "public class Keep {}");
        var worktreeFile = temp.CreateFile(
            Path.Combine("worktrees", "agent-x", "src", "Excluded.cs"), "public class Excluded {}");

        var result = await SuppressionFileResolver.ResolveAbsolutePathsAsync(temp.DirectoryPath);

        Assert.Contains(keepPath, result);
        Assert.DoesNotContain(worktreeFile, result);
    }

    [Fact]
    public async Task ResolveAbsolutePathsAsync_DirectoryWithoutSolution_ExcludesDotWorktreesSubdirFiles()
    {
        using var temp = TestTempDirectory.Create("ainetlinter-suppression-resolver-");
        var keepPath = temp.CreateFile(Path.Combine("src", "Keep.cs"), "public class Keep {}");
        var worktreeFile = temp.CreateFile(
            Path.Combine(".worktrees", "agent-y", "src", "Excluded.cs"), "public class Excluded {}");

        var result = await SuppressionFileResolver.ResolveAbsolutePathsAsync(temp.DirectoryPath);

        Assert.Contains(keepPath, result);
        Assert.DoesNotContain(worktreeFile, result);
    }
}
