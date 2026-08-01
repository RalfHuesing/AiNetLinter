using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Collection("ConsoleTestCollection")]
public sealed class SearchPatternToolTests
{
    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(null);

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "anything", isRegex: false, maxResults: 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_PlainTextSubstring_FindsExpectedHitsInFixture()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "Greeter", isRegex: false, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // Greeter.cs enthaelt "public class Greeter" und ist ueber den project-dir-Scan erreichbar.
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
        // Echte Treffer (nicht nur Path-Match).
        Assert.Contains("Greeter", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RegexPattern_FindsExpectedHitsInFixture()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            pattern: "^public\\s+(class|interface|record)",
            isRegex: true,
            maxResults: 50,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // Greeter.cs/Hierarchy.cs/Caller.cs/OtherCaller.cs/ViolationTrigger.cs beginnen mit "public class".
        Assert.Contains("public class", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_PlainTextTruncatesAtMaxResults_AppendsMetaLine()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "public", isRegex: false, maxResults: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // "public" matcht in jeder .cs-Datei mehrfach, also N >= 3 garantiert.
        Assert.Contains("[", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);

        // Genau 2 Trefferzeilen (gezaehlt an ":NNN: " Muster, 1-basiert, mind. 3-stellig ist nicht
        // garantiert, also nur auf ":\d+: " matchen). Meta-Zeile beginnt mit "[", Trefferzeilen
        // mit relativen Pfaden — die ersten zwei Zeilen vor der Meta-Zeile sind Treffer.
        var lines = textContent.Text.Split('\n');
        var metaIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("[", System.StringComparison.Ordinal) && lines[i].Contains("Treffer gesamt"))
            {
                metaIndex = i;
                break;
            }
        }
        Assert.True(metaIndex >= 2, $"Erwartete mind. 2 Trefferzeilen vor der Meta-Zeile, Output:\n{textContent.Text}");
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_ReturnsZeroHitsMessage()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            pattern: "thisStringDoesNotExistAnywhere_zzz_999",
            isRegex: false,
            maxResults: 50,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("0 Treffer", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedObjBinDirectories_ExcludedFromHits()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var projectDir = Path.GetDirectoryName(fixture.GreeterPath)!;
        var generatedDir = Path.Combine(projectDir, "obj", "Debug");
        Directory.CreateDirectory(generatedDir);
        File.WriteAllText(Path.Combine(generatedDir, "Generated.cs"), "PATTERN_ANCHOR_999 content");

        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            pattern: "PATTERN_ANCHOR_999",
            isRegex: false,
            maxResults: 50,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // Generated.cs unter obj/ muss durch den IsGeneratedPath-Filter ausgeschlossen sein.
        Assert.DoesNotContain("Generated.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRegex_ReturnsInvalidArgumentError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "(unclosed", isRegex: true, maxResults: 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pruefe pattern auf gueltige Regex-Syntax", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "", isRegex: false, maxResults: 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    }
}
