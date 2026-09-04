#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

public sealed partial class AssemblyAnalysisToolTests
{
    [Fact]
    public void FinalWireTrim_FilesTruncatedDoesNotTruncateCompleteDirectories()
    {
        var files = Enumerable.Range(0, 40)
            .Select(index => new { path = $"src/very-long-file-name-{index:D2}.cs", extension = ".cs", sizeBytes = 123L, lineCount = 4, depth = 1 })
            .ToArray();
        var result = McpToolResults.Text(
            "file tree",
            new
            {
                fileTree = new
                {
                    files,
                    directories = new[] { new { path = "src", depth = 0, matchedFileCount = 40, matchedBytes = 4920L, childDirectoryCount = 0 } },
                    summary = new
                    {
                        scannedFileCount = 40,
                        matchedFileCount = 40,
                        scannedDirectoryCount = 1,
                        matchedDirectoryCount = 1,
                        matchedBytes = 4920L,
                        byExtension = new[] { new { extension = ".cs", count = 40, bytes = 4920L } },
                    },
                    completeness = new
                    {
                        scanCompleted = true,
                        truncated = false,
                        truncatedBy = Array.Empty<string>(),
                        shownFileCount = 40,
                        inaccessibleSubtreeCount = 0,
                        skippedExcludedDirectoryCount = 0,
                        skippedReparsePointCount = 0,
                        warnings = Array.Empty<string>(),
                    },
                },
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(
            result,
            AssemblyAnalysisResponseLimits.MinimumResponseBytes,
            0);
        var tree = projected.StructuredContent!.Value.GetProperty("fileTree");
        var completeness = tree.GetProperty("completeness");
        var returnedFiles = tree.GetProperty("files").GetArrayLength();

        Assert.True(returnedFiles < 40);
        Assert.Equal(returnedFiles, tree.GetProperty("returnedCount").GetInt32());
        Assert.True(tree.GetProperty("isTruncated").GetBoolean());
        Assert.Contains("responseBudget", completeness.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(returnedFiles.ToString(), tree.GetProperty("continuationToken").GetString());
        Assert.Contains("maxResponseBytes", tree.GetProperty("detailHint").GetString(), StringComparison.Ordinal);

        Assert.Equal(1, tree.GetProperty("totalDirectoryCount").GetInt32());
        Assert.Equal(1, tree.GetProperty("returnedDirectoryCount").GetInt32());
        Assert.False(tree.GetProperty("directoriesTruncated").GetBoolean());
        Assert.Empty(tree.GetProperty("directoriesTruncatedBy").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, tree.GetProperty("directoriesContinuationToken").ValueKind);
        Assert.Equal(JsonValueKind.Null, tree.GetProperty("directoriesDetailHint").ValueKind);
        Assert.Equal(1, completeness.GetProperty("shownDirectoryCount").GetInt32());
        Assert.False(completeness.GetProperty("directoryTruncated").GetBoolean());
        Assert.Empty(completeness.GetProperty("directoryTruncatedBy").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, completeness.GetProperty("directoryContinuationToken").ValueKind);
        Assert.Equal(JsonValueKind.Null, completeness.GetProperty("directoryDetailHint").ValueKind);
    }

    [Fact]
    public void FinalWireTrim_DirectoriesTruncatedDoesNotTruncateCompleteFiles()
    {
        var directories = Enumerable.Range(0, 40)
            .Select(index => new
            {
                path = $"src/very-long-directory-name-{index:D2}",
                depth = 1,
                matchedFileCount = 1,
                matchedBytes = 123L,
                childDirectoryCount = 0,
            })
            .ToArray();
        var result = McpToolResults.Text(
            "file tree",
            new
            {
                fileTree = new
                {
                    directories,
                    files = new[] { new { path = "src/only-file.cs", extension = ".cs", sizeBytes = 123L, lineCount = 4, depth = 1 } },
                    summary = new
                    {
                        scannedFileCount = 1,
                        matchedFileCount = 1,
                        scannedDirectoryCount = 40,
                        matchedDirectoryCount = 40,
                        matchedBytes = 123L,
                        byExtension = new[] { new { extension = ".cs", count = 1, bytes = 123L } },
                    },
                    completeness = new
                    {
                        scanCompleted = true,
                        truncated = false,
                        truncatedBy = Array.Empty<string>(),
                        shownFileCount = 1,
                        inaccessibleSubtreeCount = 0,
                        skippedExcludedDirectoryCount = 0,
                        warnings = Array.Empty<string>(),
                    },
                },
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(
            result,
            AssemblyAnalysisResponseLimits.MinimumResponseBytes,
            0);
        var tree = projected.StructuredContent!.Value.GetProperty("fileTree");
        var completeness = tree.GetProperty("completeness");

        Assert.Equal(1, tree.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, tree.GetProperty("returnedCount").GetInt32());
        Assert.False(tree.GetProperty("isTruncated").GetBoolean());
        Assert.False(tree.GetProperty("truncated").GetBoolean());
        Assert.Empty(completeness.GetProperty("truncatedBy").EnumerateArray());
        Assert.False(completeness.GetProperty("truncated").GetBoolean());
        Assert.False(tree.TryGetProperty("continuationToken", out var fileToken) && fileToken.ValueKind != JsonValueKind.Null);
        Assert.False(tree.TryGetProperty("detailHint", out var fileDetailHint) && fileDetailHint.ValueKind != JsonValueKind.Null);

        var returnedDirectories = tree.GetProperty("directories").GetArrayLength();
        Assert.True(returnedDirectories < 40);
        Assert.Equal(40, tree.GetProperty("totalDirectoryCount").GetInt32());
        Assert.Equal(returnedDirectories, tree.GetProperty("returnedDirectoryCount").GetInt32());
        Assert.True(tree.GetProperty("directoriesTruncated").GetBoolean());
        Assert.Contains("responseBudget", tree.GetProperty("directoriesTruncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(returnedDirectories.ToString(), tree.GetProperty("directoriesContinuationToken").GetString());
        Assert.Contains("maxResponseBytes", tree.GetProperty("directoriesDetailHint").GetString(), StringComparison.Ordinal);
        Assert.Equal(returnedDirectories, completeness.GetProperty("shownDirectoryCount").GetInt32());
        Assert.True(completeness.GetProperty("directoryTruncated").GetBoolean());
        Assert.Contains("responseBudget", completeness.GetProperty("directoryTruncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(returnedDirectories.ToString(), completeness.GetProperty("directoryContinuationToken").GetString());
        Assert.Contains("maxResponseBytes", completeness.GetProperty("directoryDetailHint").GetString(), StringComparison.Ordinal);
    }
}
