#nullable enable

using System.IO;
using AiNetLinter.Baseline;
using AiNetLinter.FastTests.Architecture;
using Xunit;

namespace AiNetLinter.FastTests.Baseline;

[Trait("Category", "Component")]
public sealed class SourceFileCatalogPolicyTests
{
    [Theory]
    [InlineData("obj")]
    [InlineData("bin")]
    [InlineData("worktrees")]
    [InlineData(".worktrees")]
    public void IsGeneratedPath_ExcludedSubdir_ReturnsTrue(string excludedSegment) => Assert.True(SourceFileCatalog.IsGeneratedPath(Path.Combine("repo", "src", excludedSegment, "agent-x", "Foo.cs")));

    [Fact]
    public void IsGeneratedPath_NormalPath_ReturnsFalse() => Assert.False(SourceFileCatalog.IsGeneratedPath(Path.Combine("repo", "src", "Project", "Foo.cs")));

    [Fact]
    public void PolicyCalls_DoNotLoadDeniedInfrastructure()
    {
        Assert.True(SourceFileCatalog.IsGeneratedPath(Path.Combine("repo", "src", "obj", "Foo.cs")));
        Assert.False(SourceFileCatalog.IsGeneratedPath(Path.Combine("repo", "src", "Project", "Foo.cs")));
        Assert.Empty(FastTestsRuntimeDependencyGuardFixture.FindLoadedDeniedAssemblies());
    }
}
