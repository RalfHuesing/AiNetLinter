#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Maps.Skeleton;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Maps.Skeleton;

[Trait("Category", "Integration")]
public sealed class SkeletonMapBuilderAdapterTests
{
    [Fact]
    public async Task BuildAsync_WithFilterMini_ReturnsZeroAndContainsMarkdown()
    {
        var solutionRoot = FindSolutionRoot();
        using var lease = IsolatedFixtureLease.CopyFixture(solutionRoot, "FilterMini");
        var console = new RecordingLintConsole();
        var args = new LinterArgs { TargetPath = lease.RootPath, Verbose = false };

        var result = await SkeletonMapBuilder.BuildAsync(lease.RootPath, CreateConfig(), console, args);

        Assert.Equal(0, result);
        Assert.Empty(console.ErrorLines);
        Assert.Contains("# AiNetLinter — Skeleton Map", console.OutputText, StringComparison.Ordinal);
        Assert.Contains("## FilterMini.Core", console.OutputText, StringComparison.Ordinal);
        Assert.Contains("Widget", console.OutputText, StringComparison.Ordinal);
        Assert.Contains("```csharp", console.OutputText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_InvalidPath_ThrowsFileNotFoundException()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"AiNetLinterMissing_{Guid.NewGuid():N}");
        var console = new RecordingLintConsole();
        var args = new LinterArgs { TargetPath = missingPath, Verbose = false };

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => SkeletonMapBuilder.BuildAsync(missingPath, CreateConfig(), console, args));
    }

    private static Config CreateConfig() => new()
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig(),
    };

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }
}
