#nullable enable

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Verifiziert, dass der MCP-Server den Verzeichnis-Sweep in Phase 2 (SweepForNewFiles)
/// ueberspringt, wenn das Solution-Verzeichnis seine <c>LastWriteTimeUtc</c> seit der
/// letzten Beobachtung nicht geaendert hat. Bei einer echten Datei-Aenderung auf Disk
/// (neue Datei oder mtime-Touch auf dem Verzeichnis) wird der Sweep wieder ausgefuehrt,
/// sodass neu angelegte Dateien in die Solution einghaengt werden.
/// </summary>
[Trait("Category", "Unit")]
public sealed class McpCodeGraphServerStalenessMtimeCacheTests
{
    [Fact]
    public async Task GetCurrentSolution_CalledTwiceWithoutDirChange_SkipsSweepOnSecondCall()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog)));

        // Erster Call: etabliert den mtime-Cache und initialisiert _fileState.
        var first = server.GetCurrentSolution();
        Assert.NotNull(first);

        // Zweiter Call ohne Disk-Aenderung: muss den Verzeichnis-Sweep ueberspringen,
        // die bekannte Solution-Sicht darf sich nicht aendern.
        var second = server.GetCurrentSolution();
        Assert.NotNull(second);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetCurrentSolution_CalledAfterNewFile_TriggersSweepAgain()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog)));

        // Erster Call: legt die mtime-Baseline ueber alle Subdirectories fest.
        _ = server.GetCurrentSolution();

        // Neue Datei in einem Unterverzeichnis anlegen — Windows aktualisiert dabei die
        // mtime des Subdirectory-Knotens, nicht des Solution-Roots. Der mtime-Cache
        // aggregiert ueber alle Subdirectories, daher schlaegt diese Aenderung durch und
        // der Sweep muss wieder laufen.
        var newFile = Path.Combine(fixture.RootPath, "src", "BaselineMini", "LateArrival.cs");
        File.WriteAllText(newFile, "namespace BaselineMini; public class LateArrival { }");

        var updatedSolution = server.GetCurrentSolution();
        Assert.NotNull(updatedSolution);

        var knownPaths = updatedSolution!.Projects
            .SelectMany(p => p.Documents)
            .Where(d => d.FilePath != null)
            .Select(d => d.FilePath!)
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        Assert.Contains(newFile, knownPaths);
    }
}
