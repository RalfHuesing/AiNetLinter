using AiNetLinter.Baseline;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

public sealed class SourceFileCatalogTests
{
    [Fact]
    public async Task LoadAsync_MiniFixture_ReturnsSourceFiles()
    {
        var fixtureRoot = Path.Combine(FindSolutionRoot(), "tests", "Fixtures", "BaselineMini");

        using var catalog = await SourceFileCatalog.LoadAsync(fixtureRoot);
        var files = catalog.GetSourceFiles(fixtureRoot);

        Assert.Contains(files, f => f.RelativePath.EndsWith("ViolatingClass.cs", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found.");
    }
}
