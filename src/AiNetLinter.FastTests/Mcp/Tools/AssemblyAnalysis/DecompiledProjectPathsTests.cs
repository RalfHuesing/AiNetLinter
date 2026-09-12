#nullable enable

using System.IO;
using AiNetLinter.Mcp.Assemblies.Analysis;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
public sealed class DecompiledProjectPathsTests
{
    [Fact]
    public void Create_DerivesProjectDirectoryAndCommonSourceRoot()
    {
        using var temp = TestTempDirectory.Create("decompiled-project-paths-");
        var projectPath = Path.Combine(temp.DirectoryPath, "decompiled", "project", "Probe.csproj");
        var sourceRoot = Path.Combine(temp.DirectoryPath, "decompiled", "source");
        var documents = new[]
        {
            new DecompiledDocument(Path.Combine(sourceRoot, "First.cs"), "Probe.First", "public sealed class First { }"),
            new DecompiledDocument(Path.Combine(sourceRoot, "Nested", "Second.cs"), "Probe.Nested.Second", "public sealed class Second { }"),
        };

        var paths = DecompiledProjectPaths.Create(projectPath, documents);

        Assert.NotNull(paths);
        Assert.Equal(Path.GetDirectoryName(projectPath), paths!.DecompiledProjectDirectory);
        Assert.Equal(projectPath, paths.DecompiledProjectPath);
        Assert.Equal(sourceRoot, paths.DecompiledSourceRoot);
    }
}
