#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AiNetLinter.Mcp;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class AnalysisTargetResolverTests
{
    [Fact]
    public void Resolve_TargetPathOnly_InfersSolutionAndCanonicalizesExistingFile()
    {
        using var tempDir = TestTempDirectory.Create("analysis-target-solution-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace", "sub", "..", "sample.slnx");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(solutionPath))!);
        File.WriteAllText(Path.GetFullPath(solutionPath), string.Empty);

        var result = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest(solutionPath));

        Assert.Null(result.Error);
        Assert.NotNull(result.Target);
        Assert.Equal(AnalysisTargetType.Project, result.Target!.TargetType);
        Assert.Equal(Path.GetFullPath(solutionPath), result.Target.CanonicalPath);
        Assert.Equal(Path.GetDirectoryName(result.Target.CanonicalPath), result.Target.AnalysisRoot);
        Assert.NotEmpty(result.Target.Fingerprint);
    }

    [Theory]
    [InlineData("sample.txt")]
    [InlineData("sample.bin")]
    public void Resolve_TargetPathOnly_RejectsUnsupportedExtension(string fileName)
    {
        using var tempDir = TestTempDirectory.Create("analysis-target-extension-");
        var path = Path.Combine(tempDir.DirectoryPath, fileName);
        File.WriteAllText(path, string.Empty);

        var result = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest(path));

        Assert.Null(result.Target);
        Assert.Contains("INVALID_ARGUMENT", TextOf(result.Error!), StringComparison.Ordinal);
        Assert.Contains("Endung", TextOf(result.Error!), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_TargetPathOnly_RejectsRelativeMissingAndDirectoryPaths()
    {
        using var tempDir = TestTempDirectory.Create("analysis-target-path-");
        var directory = Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "sample.slnx")).FullName;
        var missing = Path.Combine(tempDir.DirectoryPath, "missing.slnx");

        foreach (var path in new[] { "relative.slnx", missing, directory })
        {
            var result = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest(path));
            Assert.Null(result.Target);
            Assert.Contains("INVALID_ARGUMENT", TextOf(result.Error!), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("targetType")]
    [InlineData("projectRoot")]
    [InlineData("configPath")]
    [InlineData("ainetlinter.project.json")]
    public void Resolve_FromArguments_RejectsLegacyKeys(string legacyKey)
    {
        var result = AnalysisTargetResolver.Resolve(AnalysisTargetRequest.FromArguments(
            new Dictionary<string, object?>
            {
                ["targetPath"] = "C:\\workspace\\sample.slnx",
                [legacyKey] = "legacy"
            }));

        Assert.Null(result.Target);
        var text = TextOf(result.Error!);
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains(legacyKey, text, StringComparison.Ordinal);
        Assert.Contains("targetPath", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveOptional_WithoutTargetKeepsAggregateModeForHealth()
    {
        var result = AnalysisTargetResolver.ResolveOptional(new AnalysisTargetRequest(null));

        Assert.Null(result.Target);
        Assert.Null(result.Error);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
