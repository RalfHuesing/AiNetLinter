using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

public sealed class McpCodeGraphServerTests
{
    [Fact]
    public void GetCurrentSolution_NotLoaded_ReturnsNull()
    {
        using var server = new McpCodeGraphServer(catalog: null);

        Assert.False(server.IsLoaded);
        Assert.Null(server.GetCurrentSolution());
    }

    [Fact]
    public async Task GetCurrentSolution_NoFileChanges_ReturnsSameSolutionVersion()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(catalog);

        var first = server.GetCurrentSolution();
        var second = server.GetCurrentSolution();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetCurrentSolution_FileModifiedOnDisk_ReflectsNewContent()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(catalog);

        _ = server.GetCurrentSolution();

        const string NewContent = "namespace BaselineMini; public class ViolatingClass { }";
        File.WriteAllText(fixture.ViolatingClassPath, NewContent);
        File.SetLastWriteTimeUtc(fixture.ViolatingClassPath, DateTime.UtcNow.AddSeconds(2));

        var updatedSolution = server.GetCurrentSolution();
        Assert.NotNull(updatedSolution);

        var document = FindDocument(updatedSolution!, fixture.ViolatingClassPath);
        Assert.NotNull(document);
        var text = await document!.GetTextAsync();
        Assert.Equal(NewContent, text.ToString());
    }

    [Fact]
    public async Task GetCurrentSolution_FileTouchedWithoutContentChange_SkipsSolutionUpdate()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(catalog);

        var first = server.GetCurrentSolution();
        File.SetLastWriteTimeUtc(fixture.ViolatingClassPath, DateTime.UtcNow.AddSeconds(2));

        var second = server.GetCurrentSolution();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetCurrentSolution_FileDeletedOnDisk_DoesNotThrow()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(catalog);

        _ = server.GetCurrentSolution();
        File.Delete(fixture.ViolatingClassPath);

        var exception = Record.Exception(() => server.GetCurrentSolution());

        Assert.Null(exception);
        Assert.NotNull(server.GetCurrentSolution());
    }

    [Fact]
    public async Task GetCurrentSolution_ConcurrentCalls_DoNotThrow()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(catalog);

        _ = server.GetCurrentSolution();

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 5; i++)
            {
                File.WriteAllText(fixture.ViolatingClassPath, $"namespace BaselineMini; public class ViolatingClass {{ /* {i} */ }}");
                File.SetLastWriteTimeUtc(fixture.ViolatingClassPath, DateTime.UtcNow.AddSeconds(i + 1));
            }
        });

        var readers = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => server.GetCurrentSolution()))
            .ToArray();
        var readersAndWriter = Task.WhenAll(readers);
        var combined = Task.WhenAll(readersAndWriter, writer);

        var finished = await Task.WhenAny(combined, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.Same(combined, finished);
        var results = await readersAndWriter;
        Assert.All(results, result => Assert.NotNull(result));
    }

    private static Document? FindDocument(Solution solution, string filePath)
    {
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (string.Equals(document.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }
        }

        return null;
    }
}
