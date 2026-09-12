#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.DeadCode;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class FindDeadCodeToolTests
{
    private readonly McpInMemoryTestContext _fixture = new();

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsSolutionNotLoadedError()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var args = new FindDeadCodeToolArgs();
        var result = await FindDeadCodeTool.ExecuteAsync(state, args, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolution_ReturnsCompactCandidateSummary()
    {
        var state = _fixture.CreateServer();

        var args = new FindDeadCodeToolArgs(
            Accessibility: "all",
            Confidence: "both",
            Kind: "all",
            Mode: "members",
            MaxResults: 50);

        var result = await FindDeadCodeTool.ExecuteAsync(state, args, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.StartsWith("dead_code: candidates=", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Zusammenfassung", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Countercheck", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesNoFiles_ReportsEmptyScopeInsteadOfCleanResult()
    {
        var state = _fixture.CreateServer();

        var args = new FindDeadCodeToolArgs(
            Accessibility: "all",
            Confidence: "both",
            Kind: "all",
            ScopeFilter: "NoMatchingScopeFilterXyz",
            Mode: "members",
            MaxResults: 50);

        var result = await FindDeadCodeTool.ExecuteAsync(state, args, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("dead_code: no analyzable documents", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_LocalsModeWithoutDiagnostics_ReportsNoCandidates()
    {
        using var context = new McpInMemoryTestContext(McpInMemoryTestContext.CreateScenario(
            new ProjectSpec("CleanProject", [("Clean.cs", """
                public sealed class Clean
                {
                    public int Value => 1;
                }
                """)])));
        var state = context.CreateServer();

        var result = await FindDeadCodeTool.ExecuteAsync(
            state, new FindDeadCodeToolArgs(Mode: "locals"), CancellationToken.None);

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("dead_code: 0 candidates", textContent.Text);
    }
}
