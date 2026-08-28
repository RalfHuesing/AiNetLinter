#nullable enable

using System;
using System.IO;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class AnalysisTargetResolverTests
{
    [Fact]
    public void Resolve_Project_CanonicalizesPathAndPreservesRawRequest()
    {
        using var tempDir = TestTempDirectory.Create("analysis-target-project-");
        var projectRoot = Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "project")).FullName;
        var rawPath = Path.Combine(projectRoot, ".", "sub", "..");
        var request = new AnalysisTargetRequest("project", rawPath);

        var result = AnalysisTargetResolver.Resolve(request);

        Assert.Null(result.Error);
        Assert.NotNull(result.Target);
        Assert.Equal(AnalysisTargetType.Project, result.Target!.TargetType);
        Assert.Equal(Path.GetFullPath(projectRoot), result.Target.CanonicalPath);
        Assert.Same(request, result.Target.Request);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "C:\\project")]
    [InlineData("invalid", "C:\\project")]
    [InlineData("project", null)]
    [InlineData("project", "relative\\project")]
    public void Resolve_InvalidTarget_ReturnsRecoverableArgumentError(string? targetType, string? targetPath)
    {
        var result = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest(targetType, targetPath));

        Assert.Null(result.Target);
        Assert.NotNull(result.Error);
        Assert.False(result.Error!.IsError);
        Assert.Contains("[ERROR]: INVALID_ARGUMENT", TextOf(result.Error), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_Assembly_RequiresExistingDllFile()
    {
        using var tempDir = TestTempDirectory.Create("analysis-target-assembly-");
        var assemblyPath = Path.Combine(tempDir.DirectoryPath, "sample.dll");
        File.WriteAllBytes(assemblyPath, [0]);

        var result = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest("assembly", assemblyPath));

        Assert.Null(result.Error);
        Assert.Equal(AnalysisTargetType.Assembly, result.Target!.TargetType);
        Assert.Equal(Path.GetFullPath(assemblyPath), result.Target.CanonicalPath);
    }

    [Fact]
    public void Resolve_Assembly_RejectsDirectoryAndWrongExtension()
    {
        using var tempDir = TestTempDirectory.Create("analysis-target-invalid-assembly-");
        var directoryPath = Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "directory.dll")).FullName;
        var textPath = Path.Combine(tempDir.DirectoryPath, "sample.txt");
        File.WriteAllText(textPath, "not an assembly");

        var directoryResult = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest("assembly", directoryPath));
        var extensionResult = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest("assembly", textPath));

        Assert.Contains("[ERROR]: INVALID_ARGUMENT", TextOf(directoryResult.Error!), StringComparison.Ordinal);
        Assert.Contains("vorhandene Datei", TextOf(directoryResult.Error!), StringComparison.Ordinal);
        Assert.Contains("[ERROR]: INVALID_ARGUMENT", TextOf(extensionResult.Error!), StringComparison.Ordinal);
        Assert.Contains("auf eine .dll", TextOf(extensionResult.Error!), StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveOptional_WithoutTargetKeepsAggregateMode()
    {
        var result = AnalysisTargetResolver.ResolveOptional(new AnalysisTargetRequest(null, null));

        Assert.Null(result.Target);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("project", null)]
    [InlineData(null, "C:\\project")]
    public void ResolveOptional_HalfFilledTargetReturnsRecoverableArgumentError(string? targetType, string? targetPath)
    {
        var result = AnalysisTargetResolver.ResolveOptional(new AnalysisTargetRequest(targetType, targetPath));

        Assert.Null(result.Target);
        Assert.NotNull(result.Error);
        Assert.False(result.Error!.IsError);
        Assert.Contains("[ERROR]: INVALID_ARGUMENT", TextOf(result.Error), StringComparison.Ordinal);
    }

    private static string TextOf(ModelContextProtocol.Protocol.CallToolResult result) =>
        Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content)).Text;
}
