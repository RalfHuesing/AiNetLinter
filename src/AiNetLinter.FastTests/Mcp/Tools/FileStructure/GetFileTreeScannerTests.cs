#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Unit")]
// @covers FileTreeAccumulator
public sealed class GetFileTreeScannerTests
{
    [Fact]
    public void Scan_DefaultTree_ExcludesGeneratedDirectoriesAndAggregatesFiles()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-scan-");
        var root = CreateFixture(tempDir.DirectoryPath);

        var result = GetFileTreeScanner.Scan(root, GetFileTreeTestData.Input(), CancellationToken.None).Payload;

        Assert.Equal(4, result.Summary.MatchedFileCount);
        Assert.Contains(result.Files, file => file.Path == "Docs/guide.md");
        Assert.Contains(result.Files, file => file.Path == "README.md");
        Assert.DoesNotContain(result.Files, file => file.Path.Contains("obj", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Directories, directory => directory.Path == ".");
        Assert.Contains(result.Directories, directory => directory.Path == "Docs");
        Assert.Equal(4, result.Completeness.ShownFileCount);
        Assert.True(result.Completeness.ScanCompleted);
        Assert.True(result.Completeness.SkippedExcludedDirectoryCount >= 1);
    }

    [Fact]
    public void Scan_ExtensionAndGlobFiltersUseAndSemantics()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-filter-");
        var root = CreateFixture(tempDir.DirectoryPath);
        var input = GetFileTreeTestData.Input() with
        {
            IncludeExtensions = ["md"],
            FileFilter = "Docs/**/*.md",
        };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        var file = Assert.Single(result.Files);
        Assert.Equal("Docs/guide.md", file.Path);
        Assert.Equal(".md", file.Extension);
        Assert.Equal(2, file.Depth);
    }

    [Fact]
    public void Scan_SortsBySizeAndCanOmitPerFileMetadata()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-sort-");
        var root = CreateFixture(tempDir.DirectoryPath);
        var input = GetFileTreeTestData.Input() with
        {
            SortBy = "size_desc",
            IncludeMetadata = false,
            IncludeLineCount = true,
        };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        Assert.Equal("Docs/guide.md", result.Files[0].Path);
        Assert.Null(result.Files[0].SizeBytes);
        Assert.NotNull(result.Files[0].LineCount);
        Assert.True(result.Summary.MatchedBytes > 0);
    }

    [Fact]
    public void Scan_MaxDepthZeroScansOnlyFilesAtRoot()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-depth-");
        var root = CreateFixture(tempDir.DirectoryPath);
        var input = GetFileTreeTestData.Input() with { MaxDepth = 0 };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        Assert.Single(result.Files);
        Assert.Equal("README.md", result.Files[0].Path);
        Assert.Equal(1, result.Summary.ScannedDirectoryCount);
        Assert.True(result.Completeness.ScanCompleted);
    }

    [Fact]
    public void Scan_TreeDepthZeroScansOnlyFilesAtRoot()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tree-depth-zero-");
        var root = CreateFixture(tempDir.DirectoryPath);
        var input = GetFileTreeTestData.Input() with { TreeDepth = 0 };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        Assert.Single(result.Files);
        Assert.Equal("README.md", result.Files[0].Path);
        Assert.Equal(1, result.Summary.ScannedDirectoryCount);
        Assert.True(result.Completeness.ScanCompleted);
    }

    [Fact]
    public void Scan_TreeDepthOneIncludesDirectSubdirectoryFiles()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tree-depth-one-");
        var root = CreateFixture(tempDir.DirectoryPath);
        var input = GetFileTreeTestData.Input() with { TreeDepth = 1 };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        Assert.Equal(3, result.Summary.MatchedFileCount);
        Assert.DoesNotContain(result.Files, file => file.Path.StartsWith("src/", StringComparison.Ordinal));
        Assert.Contains(result.Directories, directory => directory.Path == "Docs");
        Assert.DoesNotContain(result.Directories, directory => directory.Path.StartsWith("src", StringComparison.Ordinal));
        Assert.True(result.Completeness.ScanCompleted);
    }

    [Fact]
    public void Scan_TreeDepthTwoReachesNestedProjectFiles()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-tree-depth-two-");
        var root = CreateFixture(tempDir.DirectoryPath);

        var result = GetFileTreeScanner.Scan(root, GetFileTreeTestData.Input(), CancellationToken.None).Payload;

        Assert.Equal(4, result.Summary.MatchedFileCount);
        Assert.Contains(result.Files, file => file.Path == "src/Project/Project.cs");
        Assert.Equal(4, result.Summary.ScannedDirectoryCount);
    }

    [Fact]
    public void Scan_MaxDepthTakesPrecedenceOverTreeDepth()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-max-depth-precedence-");
        var root = CreateFixture(tempDir.DirectoryPath);

        var shallow = GetFileTreeScanner.Scan(
            root,
            GetFileTreeTestData.Input() with { TreeDepth = 2, MaxDepth = 0 },
            CancellationToken.None).Payload;
        var deep = GetFileTreeScanner.Scan(
            root,
            GetFileTreeTestData.Input() with { TreeDepth = 0, MaxDepth = 2 },
            CancellationToken.None).Payload;

        Assert.Single(shallow.Files);
        Assert.Equal("README.md", shallow.Files[0].Path);
        Assert.Equal(4, deep.Summary.MatchedFileCount);
        Assert.Contains(deep.Files, file => file.Path == "src/Project/Project.cs");
    }

    [Fact]
    public void Scan_SummaryViewListsOnlyTopLevelDirectoryAggregates()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-summary-top-level-");
        var root = CreateFixture(tempDir.DirectoryPath);
        var input = GetFileTreeTestData.Input() with { View = "summary" };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        Assert.Empty(result.Files);
        Assert.Equal(4, result.Summary.MatchedFileCount);
        Assert.False(result.Completeness.Truncated);
        Assert.Equal(3, result.Directories.Count);
        Assert.Contains(result.Directories, directory => directory.Path == "Docs");
        Assert.Contains(result.Directories, directory => directory.Path == "src");
        Assert.DoesNotContain(result.Directories, directory => directory.Path.StartsWith("src/", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_MaxResultsMarksResponseTruncationButKeepsSummaryComplete()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-truncate-");
        var root = CreateFixture(tempDir.DirectoryPath);
        var input = GetFileTreeTestData.Input() with { MaxResults = 2 };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        Assert.Equal(4, result.Summary.MatchedFileCount);
        Assert.Equal(2, result.Completeness.ShownFileCount);
        Assert.True(result.Completeness.Truncated);
        Assert.Contains("maxResults", result.Completeness.TruncatedBy);
        Assert.True(result.Completeness.ScanCompleted);
    }

    [Fact]
    public void Scan_SummaryViewExposesNoFileListButMarksMaxResultsDirectoryTruncation()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-summary-");
        var root = CreateFixture(tempDir.DirectoryPath);
        var input = GetFileTreeTestData.Input() with { View = "summary", MaxResults = 1 };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        Assert.Empty(result.Files);
        Assert.Equal(4, result.Summary.MatchedFileCount);
        Assert.True(result.Completeness.Truncated);
        Assert.Contains("maxResults", result.Completeness.TruncatedBy);
        Assert.Equal(0, result.Completeness.ShownFileCount);
        Assert.Equal(".", Assert.Single(result.Directories).Path);
    }

    [Fact]
    public void Scan_CancellationIsVisibleAsIncomplete()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-cancel-");
        var root = CreateFixture(tempDir.DirectoryPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = GetFileTreeScanner.Scan(root, GetFileTreeTestData.Input(), cancellation.Token).Payload;

        Assert.False(result.Completeness.ScanCompleted);
        Assert.True(result.Completeness.Truncated);
        Assert.Contains("cancellation", result.Completeness.TruncatedBy);
    }

    [Fact]
    public void Scan_FileFilterWithoutSlash_MatchesRecursivelyAgainstFileName()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-recursive-filter-");
        var root = CreateFixture(tempDir.DirectoryPath);
        var input = GetFileTreeTestData.Input() with
        {
            FileFilter = "*.cs",
        };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        // Findet src/Project/Project.cs in Unterverzeichnissen, obwohl der Filter "*.cs" keinen Slash enthaelt
        var file = Assert.Single(result.Files);
        Assert.Equal("src/Project/Project.cs", file.Path);
    }

    [Fact]
    public void Scan_FileFilterWithNullTreeDepth_ScansRecursivelyIntoDeepDirectories()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-deep-filter-");
        var root = tempDir.DirectoryPath;
        var deepDir = Path.Combine(root, "src", "A", "B", "C", "D");
        Directory.CreateDirectory(deepDir);
        File.WriteAllText(Path.Combine(deepDir, "DeepFile.cs"), "class DeepFile {}");

        var input = GetFileTreeTestData.Input() with
        {
            TreeDepth = null,
            MaxDepth = null,
            FileFilter = "DeepFile.cs",
        };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        var file = Assert.Single(result.Files);
        Assert.Equal("src/A/B/C/D/DeepFile.cs", file.Path);
        Assert.False(result.Completeness.Truncated);
    }

    [Fact]
    public void Scan_SubdirectoryRootWithNullTreeDepth_ReachesNestedFiles()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-deep-subdir-");
        var root = tempDir.DirectoryPath;
        var deepDir = Path.Combine(root, "src", "Modul", "Sub1", "Sub2", "Sub3");
        Directory.CreateDirectory(deepDir);
        File.WriteAllText(Path.Combine(deepDir, "Nested.cs"), "class Nested {}");

        var input = GetFileTreeTestData.Input() with
        {
            Root = "src/Modul",
            TreeDepth = null,
            MaxDepth = null,
            View = "files",
        };

        var result = GetFileTreeScanner.Scan(root, input, CancellationToken.None).Payload;

        Assert.Contains(result.Files, f => f.Path.EndsWith("Nested.cs", StringComparison.Ordinal));
        Assert.False(result.Completeness.Truncated);
    }

    private static string CreateFixture(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Docs"));
        Directory.CreateDirectory(Path.Combine(root, "src", "Project"));
        Directory.CreateDirectory(Path.Combine(root, "obj"));
        File.WriteAllText(Path.Combine(root, "README.md"), "readme\n");
        File.WriteAllText(Path.Combine(root, "Docs", "guide.md"), "line 1\nline 2\nline 3\n");
        File.WriteAllText(Path.Combine(root, "Docs", "data.json"), "{}\n");
        File.WriteAllText(Path.Combine(root, "src", "Project", "Project.cs"), "class Project {}\n");
        File.WriteAllText(Path.Combine(root, "obj", "generated.cs"), "class Generated {}\n");
        return root;
    }

}