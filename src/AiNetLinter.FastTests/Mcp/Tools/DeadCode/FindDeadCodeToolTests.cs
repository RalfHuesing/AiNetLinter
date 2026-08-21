#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.DeadCode;
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
    public async Task ExecuteAsync_LoadedSolution_ReturnsFormattedTextAndStructuredContent()
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
        Assert.Contains("Dead-Code-Analyse", textContent.Text);
        Assert.Contains("Zusammenfassung", textContent.Text);
        Assert.NotNull(result.StructuredContent);
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
        // Leerer Scope ist kein vollstaendiges Analyseergebnis: expliziter Hinweis statt
        // irrefuehrendem "kein toter Code" plus Sufficiency-Hint.
        Assert.Contains("Keine Symbole im Scope gescannt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("includeTests", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "vollstaendig fuer den angefragten Scope",
            textContent.Text,
            StringComparison.Ordinal);
    }
}
