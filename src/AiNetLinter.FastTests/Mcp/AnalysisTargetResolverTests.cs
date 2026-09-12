#nullable enable

using System;
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
        Assert.Contains("fieldPath: $.targetPath", TextOf(result.Error!), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sample.sln", "Project")]
    [InlineData("sample.slnx", "Project")]
    [InlineData("sample.dll", "Assembly")]
    [InlineData("sample.exe", "Assembly")]
    public void Resolve_TargetPathOnly_AcceptsSupportedFileKinds(string fileName, string expectedTypeName)
    {
        using var tempDir = TestTempDirectory.Create("analysis-target-kinds-");
        var path = Path.Combine(tempDir.DirectoryPath, "folder with spaces", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

        var result = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest(path));

        Assert.Null(result.Error);
        Assert.NotNull(result.Target);
        var target = result.Target!;
        var expectedType = Enum.Parse<AnalysisTargetType>(expectedTypeName);
        Assert.Equal(expectedType, target.TargetType);
        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(path)), target.AnalysisRoot);
        Assert.Equal(expectedType == AnalysisTargetType.Project ? AnalysisTargetOrigin.Source : AnalysisTargetOrigin.Decompiled, target.Origin);
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
