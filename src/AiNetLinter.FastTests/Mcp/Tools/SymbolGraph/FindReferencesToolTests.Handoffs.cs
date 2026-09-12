#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

public sealed partial class FindReferencesToolTests
{
    [Fact]
    public async Task ExecuteAsync_Depth1_MatchesCurrentBehavior()
    {
        var result = await FindReferencesTool.ExecuteAsync(
            _fixture.CreateServer(), "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("Caller.cs", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithSymbolIdentifier_ResolvesReferences()
    {
        var result = await FindReferencesTool.ExecuteAsync(
            _fixture.CreateServer(), new FindReferencesRequest("Greeter.Greet", MaxResults: 50, Depth: 1), CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("Caller.cs", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_HandoffIsRenderedOnceBesideItsCallSite()
    {
        var result = await FindReferencesTool.ExecuteAsync(
            _fixture.CreateServer(), "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var id = ExtractHandoffId(text);
        Assert.StartsWith("s:", id, StringComparison.Ordinal);
        Assert.All(
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains("handoffId:", StringComparison.Ordinal)),
            line => Assert.Equal(1, Regex.Matches(line, @"handoffId: `s:[^`]+`", RegexOptions.CultureInvariant).Count));
        Assert.DoesNotContain("targetPath", text, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshot", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ContentCallSiteIdCanBeUsedForSymbolBodyFollowUp()
    {
        var state = _fixture.CreateServer();
        var references = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);
        var id = ExtractHandoffId(Assert.IsType<TextContentBlock>(Assert.Single(references.Content)).Text);

        var body = await AiNetLinter.Mcp.Tools.GetSymbolBodyTool.ExecuteAsync(
            state, [id], 80, CancellationToken.None);

        Assert.NotEqual(true, body.IsError);
        Assert.Contains("Greet", Assert.IsType<TextContentBlock>(Assert.Single(body.Content)).Text, StringComparison.Ordinal);
    }

    private static string ExtractHandoffId(string text)
    {
        var match = Regex.Match(text, @"(?:handoffId|id): `(?<id>s:[^`]+)`", RegexOptions.CultureInvariant);
        return match.Success
            ? match.Groups["id"].Value
            : throw new InvalidOperationException("Der Content muss eine kopierbare Symbol-Handoff-ID enthalten.");
    }

    [Fact]
    public async Task ExecuteAsync_WithMaxResults_TruncatesAndEmitsCountHeader()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 2, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }
}
