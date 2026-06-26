#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Maps.Skeleton;
using AiNetLinter.Tests.Maps;

namespace AiNetLinter.Tests.Maps.Skeleton;

[Collection("ConsoleTestCollection")]
public sealed class SkeletonMapBuilderTests
{
    [Fact]
    public async Task BuildAsync_WithSolution_ReturnsZeroAndContainsMarkdown()
    {
        var slnPath = FindSlnxFile();
        if (slnPath == null) return; // kein .slnx im CI — überspringen

        var console = new TestLintConsole();
        var result = await SkeletonMapBuilder.BuildAsync(slnPath, console);

        Assert.Equal(0, result);
        var output = console.Output;
        Assert.Contains("# AiNetLinter — Skeleton Map", output);
        Assert.Contains("```csharp", output);
    }

    [Fact]
    public async Task BuildAsync_InvalidPath_ReturnsOne()
    {
        var console = new TestLintConsole();
        // SourceFileCatalog.LoadAsync wirft FileNotFoundException
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => SkeletonMapBuilder.BuildAsync("/nonexistent/path", console));
    }

    private static string? FindSlnxFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var files = dir.GetFiles("*.slnx");
            if (files.Length > 0) return files[0].FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
