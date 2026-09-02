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
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "README.md"), "hello\n");

        var result = await GetFileTreeTool.ExecuteAsync(
            tempDir.DirectoryPath,
            GetFileTreeTestData.Input(),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("get_file_tree: root=. view=files", text, StringComparison.Ordinal);
        Assert.Contains("README.md", text, StringComparison.Ordinal);
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value.GetProperty("fileTree");
        Assert.Equal("files", payload.GetProperty("view").GetString());
        Assert.Equal(1, payload.GetProperty("summary").GetProperty("matchedFileCount").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_TreeViewWithTreeDepthZeroShowsOnlyRootFiles()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-tree-");
        Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "nested"));
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "nested", "deep.md"), "deep\n");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "README.md"), "hello\n");

        var result = await GetFileTreeTool.ExecuteAsync(
            tempDir.DirectoryPath,
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
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "README.md"), "hello\n");

        var result = await GetFileTreeTool.ExecuteAsync(
            tempDir.DirectoryPath,
            GetFileTreeTestData.Input() with { View = "summary" },
            CancellationToken.None);

        Assert.Contains("1 Dateien aggregiert", TextOf(result), StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Dateitreffer", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRootIsRecoverableAndDoesNotThrow()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-invalid-");
        var input = GetFileTreeTestData.Input() with { Root = "does-not-exist" };

        var result = await GetFileTreeTool.ExecuteAsync(tempDir.DirectoryPath, input, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("RESOURCE_NOT_FOUND", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsAbsoluteAndTraversalRoots()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-boundary-");
        foreach (var root in new[] { Path.Combine(tempDir.DirectoryPath, "nested"), "..\\outside" })
        {
            var result = await GetFileTreeTool.ExecuteAsync(
                tempDir.DirectoryPath,
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
            var result = await GetFileTreeTool.ExecuteAsync(tempDir.DirectoryPath, input, CancellationToken.None);
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

        Assert.True(result.IsError);
        Assert.Contains("PROJECT_ROOT_INVALID", TextOf(result), StringComparison.Ordinal);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}