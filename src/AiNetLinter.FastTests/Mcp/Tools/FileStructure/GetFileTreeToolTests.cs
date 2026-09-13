#nullable enable

using System;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
    public async Task ExecuteAsync_ReportsMatchingFilesInContent()
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

    [Theory]
    [InlineData(200)]
    [InlineData(500)]
    [InlineData(512)]
    public async Task ExecuteAsync_TooSmallBudgetReturnsDeterministicRetryForValuableContent(int maxResponseBytes)
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tool-budget-");
        var targetPath = tempDir.CreateFile("app.slnx", string.Empty);
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "important.md"), "important\n");
        for (var index = 0; index < 30; index++)
        {
            File.WriteAllText(
                Path.Combine(tempDir.DirectoryPath, $"entry{index}.extension{index:D2}withvaluableclassification"),
                "content\n");
        }

        var input = GetFileTreeTestData.Input() with { View = "files", MaxResponseBytes = maxResponseBytes };
        var constrained = await GetFileTreeTool.ExecuteAsync(targetPath, input, CancellationToken.None);

        Assert.True(constrained.IsError);
        var constrainedText = TextOf(constrained);
        Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", constrainedText, StringComparison.Ordinal);
        Assert.Contains("fieldPath: $.maxResponseBytes", constrainedText, StringComparison.Ordinal);
        Assert.Contains($"maxResponseBytes={maxResponseBytes}", constrainedText, StringComparison.Ordinal);
        var minimumResponseBytes = int.Parse(
            Regex.Match(constrainedText, "minimumResponseBytes: (\\d+)").Groups[1].Value,
            CultureInfo.InvariantCulture);
        Assert.True(minimumResponseBytes > maxResponseBytes);

        var retry = await GetFileTreeTool.ExecuteAsync(
            targetPath,
            input with { MaxResponseBytes = minimumResponseBytes },
            CancellationToken.None);

        Assert.NotEqual(true, retry.IsError);
        var retryText = TextOf(retry);
        Assert.True(Encoding.UTF8.GetByteCount(retryText) <= minimumResponseBytes);
        Assert.Contains("important.md", retryText, StringComparison.Ordinal);
        Assert.Contains("extension00withvaluableclassification", retryText, StringComparison.Ordinal);
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
