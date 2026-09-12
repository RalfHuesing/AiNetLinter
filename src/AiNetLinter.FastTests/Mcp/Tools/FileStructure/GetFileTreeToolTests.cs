#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Component")]
public sealed class GetFileTreeToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsWrappedStructuredPayloadAndCompactText()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-");
        var targetPath = tempDir.CreateFile("app.slnx", string.Empty);
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "README.md"), "hello\n");

        var result = await GetFileTreeTool.ExecuteAsync(
            targetPath,
            GetFileTreeTestData.Input(),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("get_file_tree: root=. view=files", text, StringComparison.Ordinal);
        Assert.Contains("2 physische Dateien gescannt", text, StringComparison.Ordinal);
        Assert.Contains("README.md", text, StringComparison.Ordinal);
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value.GetProperty("fileTree");
        Assert.Equal("files", payload.GetProperty("view").GetString());
        Assert.Equal(2, payload.GetProperty("summary").GetProperty("matchedFileCount").GetInt32());
        Assert.Equal(2, payload.GetProperty("completeness").GetProperty("shownPhysicalFileCount").GetInt32());
        Assert.False(payload.TryGetProperty("population", out _));
    }

    [Fact]
    public async Task ExecuteAsync_TreeViewWithTreeDepthZeroShowsOnlyRootFiles()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-tree-");
        var targetPath = tempDir.CreateFile("app.slnx", string.Empty);
        Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "nested"));
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "nested", "deep.md"), "deep\n");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "README.md"), "hello\n");

        var result = await GetFileTreeTool.ExecuteAsync(
            targetPath,
            GetFileTreeTestData.Input() with { View = "tree", TreeDepth = 0 },
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains("README.md", text, StringComparison.Ordinal);
        Assert.DoesNotContain("nested/deep.md", text, StringComparison.Ordinal);
        Assert.Contains("Scantiefe begrenzt", text, StringComparison.Ordinal);
        Assert.DoesNotContain("vollstaendig", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SummaryViewExplainsThatFilesAreAggregated()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-summary-");
        var targetPath = tempDir.CreateFile("app.slnx", string.Empty);
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "README.md"), "hello\n");

        var result = await GetFileTreeTool.ExecuteAsync(
            targetPath,
            GetFileTreeTestData.Input() with { View = "summary" },
            CancellationToken.None);

        Assert.Contains("2 physische Dateien aggregiert", TextOf(result), StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Dateitreffer", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRootIsRecoverableAndDoesNotThrow()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-invalid-");
        var targetPath = tempDir.CreateFile("app.slnx", string.Empty);
        var input = GetFileTreeTestData.Input() with { Root = "does-not-exist" };

        var result = await GetFileTreeTool.ExecuteAsync(targetPath, input, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("RESOURCE_NOT_FOUND", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsAbsoluteAndTraversalRoots()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-boundary-");
        var targetPath = tempDir.CreateFile("app.slnx", string.Empty);
        foreach (var root in new[] { Path.Combine(tempDir.DirectoryPath, "nested"), "..\\outside" })
        {
            var result = await GetFileTreeTool.ExecuteAsync(
                targetPath,
                GetFileTreeTestData.Input() with { Root = root },
                CancellationToken.None);

            Assert.NotEqual(true, result.IsError);
            Assert.Contains("INVALID_ARGUMENT", TextOf(result), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidViewBudgetAndGlob()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-arguments-");
        var targetPath = tempDir.CreateFile("app.slnx", string.Empty);
        var cases = new[]
        {
            GetFileTreeTestData.Input() with { View = "unknown" },
            GetFileTreeTestData.Input() with { MaxResults = 0 },
            GetFileTreeTestData.Input() with { MaxDepth = -1 },
            GetFileTreeTestData.Input() with { FileFilter = "../outside/**" },
            GetFileTreeTestData.Input() with { IncludeExtensions = ["*.md"] },
        };

        foreach (var input in cases)
        {
            var result = await GetFileTreeTool.ExecuteAsync(targetPath, input, CancellationToken.None);
            Assert.NotEqual(true, result.IsError);
            Assert.Contains("INVALID_ARGUMENT", TextOf(result), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MissingProjectRootUsesExistingGuard()
    {
        var result = await GetFileTreeTool.ExecuteAsync(
            "relative-root",
            GetFileTreeTestData.Input(),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("INVALID_ARGUMENT", TextOf(result), StringComparison.Ordinal);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
