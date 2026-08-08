#nullable enable

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Verifiziert das B.2-Verhalten des Verzeichnis-Sweeps in
/// <c>McpCodeGraphServer.RefreshStaleDocuments</c>: waehrend der Server-Session neu angelegte
/// <c>.cs</c>-Dateien werden automatisch in die Solution einghaengt, geloeschte Dateien werden aus
/// dem Solution-Modell entfernt. Generierte Dateien (obj/, bin/, .g.cs) werden ignoriert.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpCodeGraphServerFileDiscoveryTests
{
    [Fact]
    public async Task GetCurrentSolution_NewFileAddedAfterStart_AppearsInSolution()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog)));

        _ = server.GetCurrentSolution();

        var newFile = Path.Combine(fixture.RootPath, "src", "BaselineMini", "NewlyAddedClass.cs");
        File.WriteAllText(newFile, "namespace BaselineMini; public class NewlyAddedClass { }");

        var updatedSolution = server.GetCurrentSolution();
        Assert.NotNull(updatedSolution);

        var knownPaths = updatedSolution!.Projects
            .SelectMany(p => p.Documents)
            .Where(d => d.FilePath != null)
            .Select(d => d.FilePath!)
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        Assert.Contains(newFile, knownPaths);
    }

    [Fact]
    public async Task GetCurrentSolution_FileDeletedAfterStart_RemovedFromSolution()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog)));

        _ = server.GetCurrentSolution();
        var target = fixture.ViolatingClassPath;
        Assert.True(File.Exists(target));

        File.Delete(target);

        var updatedSolution = server.GetCurrentSolution();
        Assert.NotNull(updatedSolution);

        var idsForDeletedFile = updatedSolution!.GetDocumentIdsWithFilePath(target);
        Assert.Empty(idsForDeletedFile);
    }

    [Fact]
    public async Task GetCurrentSolution_GeneratedFile_NotAdded()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog)));

        _ = server.GetCurrentSolution();

        // Generated-File-Pfade (obj/, bin/, .g.cs) sollen vom Sweep ignoriert werden.
        var generatedFile = Path.Combine(fixture.RootPath, "src", "BaselineMini", "obj", "Generated.g.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(generatedFile)!);
        File.WriteAllText(generatedFile, "// generator output");

        var updatedSolution = server.GetCurrentSolution();
        Assert.NotNull(updatedSolution);

        var knownPaths = updatedSolution!.Projects
            .SelectMany(p => p.Documents)
            .Where(d => d.FilePath != null)
            .Select(d => d.FilePath!)
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain(generatedFile, knownPaths);
    }
}
