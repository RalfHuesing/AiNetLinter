#nullable enable

using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Xunit;
using AiNetLinter.Baseline;

namespace AiNetLinter.IntegrationTests.Baseline;

[Trait("Category", "Integration")]
public sealed class ProjectRestoreStateTests : IDisposable
{
    private readonly TestTempDirectory _tempDir = TestTempDirectory.Create("ainetlinter-restorestate-");

    public void Dispose() => _tempDir.Dispose();

    private string CreateProjectFile(string name = "Sample.csproj")
    {
        return _tempDir.CreateFile(name, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
    }

    private string CreateProjectFileInOwnDirectory(string projectDirName, string fileName)
    {
        var relativePath = Path.Combine(projectDirName, fileName);
        return _tempDir.CreateFile(relativePath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
    }

    private static void CreateFreshProjectAssetsJson(string projectFilePath)
    {
        var objDir = Path.Combine(Path.GetDirectoryName(projectFilePath)!, "obj");
        Directory.CreateDirectory(objDir);
        var assetsPath = Path.Combine(objDir, "project.assets.json");
        File.WriteAllText(assetsPath, "{}");
        // Sicherstellen, dass assets.json garantiert nicht aelter als die .csproj ist (manche
        // Dateisysteme haben eine Aufloesung von nur ~2s bei schnell aufeinanderfolgenden Writes).
        File.SetLastWriteTimeUtc(assetsPath, File.GetLastWriteTimeUtc(projectFilePath).AddSeconds(5));
    }

    [Fact]
    public void NeedsRestore_ReturnsTrue_WhenProjectAssetsJsonMissing()
    {
        var projectFile = CreateProjectFile();

        Assert.True(ProjectRestoreState.NeedsRestore(projectFile));
    }

    [Fact]
    public void NeedsRestore_ReturnsFalse_WhenProjectAssetsJsonFresh()
    {
        var projectFile = CreateProjectFile();
        CreateFreshProjectAssetsJson(projectFile);

        Assert.False(ProjectRestoreState.NeedsRestore(projectFile));
    }

    [Fact]
    public void NeedsRestore_ReturnsTrue_WhenProjectAssetsJsonOlderThanCsproj()
    {
        var projectFile = CreateProjectFile();
        CreateFreshProjectAssetsJson(projectFile);

        // .csproj nachtraeglich "anfassen" (z. B. PackageReference hinzugefuegt) — assets.json
        // stammt jetzt aus einem Stand vor der Aenderung und ist damit veraltet.
        File.SetLastWriteTimeUtc(projectFile, DateTime.UtcNow.AddMinutes(10));

        Assert.True(ProjectRestoreState.NeedsRestore(projectFile));
    }

    [Fact]
    public void NeedsRestore_ReturnsFalse_WhenProjectFilePathIsNull()
    {
        Assert.False(ProjectRestoreState.NeedsRestore((string?)null));
    }

    [Fact]
    public void NeedsRestore_ReturnsFalse_WhenProjectFileDoesNotExist()
    {
        var missingPath = _tempDir.GetPath("DoesNotExist.csproj");

        Assert.False(ProjectRestoreState.NeedsRestore(missingPath));
    }

    [Fact]
    public void ComputeProjectsNeedingRestore_FlagsOnlyUnrestoredProjects()
    {
        var restoredProjectFile = CreateProjectFileInOwnDirectory("RestoredProj", "Restored.csproj");
        CreateFreshProjectAssetsJson(restoredProjectFile);
        var unrestoredProjectFile = CreateProjectFileInOwnDirectory("UnrestoredProj", "Unrestored.csproj");

        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        var restoredId = ProjectId.CreateNewId();
        var restoredInfo = ProjectInfo.Create(restoredId, VersionStamp.Create(), "Restored", "Restored", LanguageNames.CSharp)
            .WithFilePath(restoredProjectFile);
        solution = solution.AddProject(restoredInfo);

        var unrestoredId = ProjectId.CreateNewId();
        var unrestoredInfo = ProjectInfo.Create(unrestoredId, VersionStamp.Create(), "Unrestored", "Unrestored", LanguageNames.CSharp)
            .WithFilePath(unrestoredProjectFile);
        solution = solution.AddProject(unrestoredInfo);

        var result = ProjectRestoreState.ComputeProjectsNeedingRestore(solution);

        Assert.DoesNotContain(restoredId, result);
        Assert.Contains(unrestoredId, result);
    }

    [Fact]
    public void ComputeProjectsNeedingRestore_SkipsInMemoryProjectsWithoutFilePath()
    {
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectId = ProjectId.CreateNewId();
        solution = solution.AddProject(projectId, "InMemory", "InMemory", LanguageNames.CSharp);

        var result = ProjectRestoreState.ComputeProjectsNeedingRestore(solution);

        Assert.Empty(result);
    }
}
